using ONI_United.Core;
using ONI_United.DebugTools;
using ONI_United.UI.Screens;
using ONI_United.Misc;
using ONI_United.Misc.World;
using ONI_United.Networking.Transfer;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Packets.Handshake;
using ONI_United.Networking.Packets.Core;
using ONI_United.Networking.Packets.World;
using Shared.Profiling;
using ONI_United.Networking.Sessions.States;
using ONI_United.Patches.ToolPatches;
using Shared;
using Shared.Helpers;
using Steamworks;
using System.Collections;
using System.Linq;
using UnityEngine;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Sessions;
using ONI_United;
using ONI_United.Networking;
using ONI_United.Networking.Identity;

namespace ONI_United.Networking.Runtime
{
	public static partial class GameClient
	{

		internal static void RequestSynchronizationTransferRetry(ulong synchronizationId, bool forceGeneric = false)
		{
			if (!CanReceiveSynchronizationSave(synchronizationId))
			{
				DebugConsole.LogWarning($"[Synchronization] Ignored transfer retry for inactive/submitted sync {synchronizationId}.");
				return;
			}

			if (forceGeneric)
			{
				var packet = new TcpFallbackRequestPacket
				{
					Requester = MultiplayerSession.LocalUserID,
					SynchronizationId = synchronizationId
				};
				PacketSender.SendToHost(packet);
				return;
			}

			var normalRetryPacket = new SaveFileRequestPacket
			{
				Requester = MultiplayerSession.LocalUserID,
				SynchronizationId = synchronizationId
			};
			PacketSender.SendToHost(normalRetryPacket);
		}

        public static bool IsHardSyncInProgress => Session.IsClient && Lifecycle.Current == Lifecycle.Phase.Synchronizing;

        // Native establishment has already been confirmed by the owning driver.
        // Lifecycle selects this essential preparation from (Joining, PrepareClientSession).
        internal static void PrepareSession() =>
            SessionHandshakeCoordinator.SendClientHello(NetworkRuntime.TransportClient.CurrentConnection);

        private static void Init()
		{
			using var _ = Profiler.Scope();

            var owner = NetworkRuntime.TransportClient;
            var operation = NetworkRuntime.ActiveOperation;
            bool IsCurrentOperation() => NetworkRuntime.IsCurrentOperation(owner, operation);
            owner.OnClientDisconnected = () =>
            {
                if (IsCurrentOperation() && Lifecycle.Current != Lifecycle.Phase.ClearingSession) NetworkRuntime.DisconnectClient();
            };
            owner.OnConnectionFailed = reason =>
            {
                if (IsCurrentOperation() && Lifecycle.Current != Lifecycle.Phase.ClearingSession) NetworkRuntime.DisconnectClient();
            };
            owner.OnClientConnected = () => { if (IsCurrentOperation()) NetworkRuntime.TransportEstablished(owner, operation); };
			owner.OnContinueConnectionFlow = () => { if (IsCurrentOperation()) ContinueConnectionFlow(); };
			NetworkRuntime.TransportClient.OnReturnToMenu = (reason, message) =>
			{
                if (ReferenceEquals(owner, NetworkRuntime.TransportClient) && owner.IsCurrentOperation(operation)
                    && (!NetworkRuntime.HasActiveOperation || IsCurrentOperation()))
                    CoroutineRunner.RunOne(ShowMessageAndReturnToTitle(reason, message, skipIfReconnected: true));
			};
			NetworkRuntime.TransportClient.OnRequestStateOrReturn = () =>
			{
                if (!IsCurrentOperation() || Lifecycle.Current == Lifecycle.Phase.ClearingSession) return;
                GameplayPacketSender.Instance.SendToHost(GameStateRequestPacket.CreateClientRequest(MultiplayerSession.LocalUserID));
                if (!IsCurrentOperation() || Lifecycle.Current == Lifecycle.Phase.ClearingSession) return;
                MP_Timer.Instance.StartDelayedAction(10, () =>
                {
                    if (IsCurrentOperation() && NetworkRuntime.HasClientOperation && Lifecycle.Current != Lifecycle.Phase.ClearingSession)
                        CoroutineRunner.RunOne(ShowMessageAndReturnToTitle());
                });
            };
            NetworkRuntime.TransportClient.Prepare();
            if (IsCurrentOperation() && Lifecycle.Current == Lifecycle.Phase.Joining)
                CursorManager.Instance.AssignColor();
        }

        public static void ConnectToHost(bool showLoadingScreen = true, string ip = "", int port = 7777)
            => NetworkRuntime.ConnectClient(showLoadingScreen, ip, port);

        internal static void BeginConnection(bool showLoadingScreen = true, string ip = "", int port = 7777)
        {
			using var _ = Profiler.Scope();

            var owner = NetworkRuntime.TransportClient;
            var operation = NetworkRuntime.ActiveOperation;
            Init();
            if (!NetworkRuntime.IsCurrentOperation(owner, operation) || Lifecycle.Current != Lifecycle.Phase.Joining) return;

			if (showLoadingScreen)
			{
				string hostName = "uknown host";
				if (NetworkRuntime.IsSteam)
				{
					hostName = SteamFriends.GetFriendPersonaName(MultiplayerSession.HostUserID.AsCSteamID());
                }
				else if (NetworkRuntime.IsLan)
				{
					hostName = $"{ip}:{port}";
                }
					MultiplayerOverlay.Show(string.Format(ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.CONNECTING_TO_HOST, hostName));
			}

			try
			{
                if (NetworkRuntime.IsCurrentOperation(owner, operation) && Lifecycle.Current == Lifecycle.Phase.Joining)
				    owner.StartConnection(ip, port);
			}
			catch
			{
                if (NetworkRuntime.IsCurrentOperation(owner, operation)
                    && Lifecycle.Current != Lifecycle.Phase.ClearingSession) NetworkRuntime.DisconnectClient();
				throw;
			}
		}

		public static void Disconnect()
		{
			using var _ = Profiler.Scope();

			NetworkRuntime.TransportClient.Disconnect();
		}

        public static void CancelConnectionAttempt()
        {
            NetworkRuntime.DisconnectClient();
            MultiplayerOverlay.Close();
        }

		public static void ReconnectToSession()
		{
			using var _ = Profiler.Scope();

			NetworkRuntime.TransportClient.ReconnectToSession();
		}

		public static void Poll()
		{
			using var _ = Profiler.Scope();

			NetworkRuntime.TransportClient.Update();

            if (NetworkRuntime.HasClientOperation && Lifecycle.Current != Lifecycle.Phase.ClearingSession)
                NetworkRuntime.TransportClient.OnMessageRecieved();
		}

		public static void OnHostResponseReceived(GameStateRequestPacket packet)
		{
			using var _ = Profiler.Scope();

            var owner = NetworkRuntime.TransportClient;
            var operation = NetworkRuntime.ActiveOperation;
            bool IsCurrent() => NetworkRuntime.IsCurrentOperation(owner, operation)
                && Lifecycle.Current != Lifecycle.Phase.ClearingSession;
			DebugConsole.Log("Gamestate packet received");
			MP_Timer.Instance.Abort();
			if (!TryValidateHostProtocol(packet, out string protocolReason, out string protocolMessage))
			{
				DebugConsole.LogWarning($"[GameClient] Host protocol validation failed: {protocolReason} | {protocolMessage}");
				Disconnect();
				CoroutineRunner.RunOne(ShowMessageAndReturnToTitle(protocolReason, protocolMessage));
				return;
			}

			if (MultiplayerSession.GetPlayer(MultiplayerSession.HostUserID) is MultiplayerPlayer host)
			{
				host.ProtocolVerified = true;
			}

			if (!SaveHelper.SavegameDlcListValid(packet.ActiveDlcIds, out var errorMsg))
			{
				DebugConsole.Log("invalid dlc config detected");
				SaveHelper.ShowMessageAndReturnToMainMenu(errorMsg);
				return;
			}

			if (!SaveHelper.SteamModListSynced(packet.ActiveModIds, out var notEnabled, out var notDisabled, out var missingMods))
			{
				string text = ONI_United.STRINGS.UI.MP_OVERLAY.SYNC.MODSYNC.TEXT + "\n\n";
				if (notEnabled.Any())
					text += string.Format(ONI_United.STRINGS.UI.MP_OVERLAY.SYNC.MODSYNC.TOENABLE, notEnabled.Count) +"\n";
				if (notDisabled.Any())
					text += string.Format(ONI_United.STRINGS.UI.MP_OVERLAY.SYNC.MODSYNC.TODISABLE, notDisabled.Count) + "\n";
				if (missingMods.Any())
					text += string.Format(ONI_United.STRINGS.UI.MP_OVERLAY.SYNC.MODSYNC.MISSING, missingMods.Count) + "\n";

				// Ignore this if we're in game already
				if (Utils.IsInMenu())
				{
					UiUtil.CreateConfirmDialogFrontend(ONI_United.STRINGS.UI.MP_OVERLAY.SYNC.MODSYNC.TITLE, text,
		   ONI_United.STRINGS.UI.MP_OVERLAY.SYNC.MODSYNC.CONFIRM_SYNC,
					() => { if (IsCurrent()) SaveHelper.SyncModsAndRestart(notEnabled, notDisabled, missingMods); },
					ONI_United.STRINGS.UI.MP_OVERLAY.SYNC.MODSYNC.CANCEL,
					() => { if (IsCurrent()) BackToMainMenu(); },
					ONI_United.STRINGS.UI.MP_OVERLAY.SYNC.MODSYNC.DENY_SYNC,
					() => { if (IsCurrent()) ContinueConnectionFlow(); });
					DebugConsole.Log("mods not synced!");
				}
				return;
			}

			ContinueConnectionFlow();
		}

		private static bool TryValidateHostProtocol(GameStateRequestPacket packet, out string reason, out string message)
		{
			using var _ = Profiler.Scope();

			if(Configuration.Instance.BypassProtocolCompatibilityChecks)
			{
				reason = string.Empty;
				message = string.Empty;
				return true;
			}

			reason = ONI_United.STRINGS.UI.PROTOCOL.VALIDATION.TITLE;
			message = string.Empty;

			if (!packet.HasProtocolMetadata)
			{
				message = ONI_United.STRINGS.UI.PROTOCOL.VALIDATION.NO_METADATA;
				return false;
			}

			if (!packet.ProtocolAccepted)
			{
				message = string.IsNullOrEmpty(packet.ProtocolFailureReason)
					? ONI_United.STRINGS.UI.PROTOCOL.VALIDATION.REJECTED
					: packet.ProtocolFailureReason;
				return false;
			}

			if (packet.ProtocolVersion != ProtocolCompatibility.CurrentProtocolVersion)
			{
				message = string.Format(ONI_United.STRINGS.UI.PROTOCOL.VALIDATION.PROTOCOL_MISMATCH, packet.ProtocolVersion, ProtocolCompatibility.CurrentProtocolVersion);
				return false;
			}

			if (packet.PacketRegistryFingerprint != ProtocolCompatibility.PacketFingerprint)
			{
				message = string.Format(ONI_United.STRINGS.UI.PROTOCOL.VALIDATION.FINGERPRINT_MISMATCH, packet.PacketRegistryFingerprint, ProtocolCompatibility.PacketFingerprint);
				return false;
			}

			// The host's mod version, which this side did not check either. Without it a
			// client still joins a host it cannot agree with, even though the host now
			// refuses the reverse.
			if (!string.IsNullOrEmpty(packet.ModVersion)
				&& packet.ModVersion != ProtocolCompatibility.ModVersion)
			{
				message = string.Format(ONI_United.STRINGS.UI.PROTOCOL.MOD_VERSION_MISMATCH,
					ProtocolCompatibility.ModVersion, packet.ModVersion);
				return false;
			}

			return true;
		}
		static void BackToMainMenu()
		{
			using var _ = Profiler.Scope();

			MultiplayerOverlay.Close();
			NetworkIdentityRegistry.Clear();
            if (Lifecycle.Current != Lifecycle.Phase.ClearingSession)
			    NetworkRuntime.Stop();
			App.LoadScene("frontend");
		}

		private static void ContinueConnectionFlow()
		{
			using var _ = Profiler.Scope();

            var owner = NetworkRuntime.TransportClient;
            var operation = NetworkRuntime.ActiveOperation;
            bool IsCurrent() => NetworkRuntime.IsCurrentOperation(owner, operation)
                && Lifecycle.Current != Lifecycle.Phase.ClearingSession;
			if (!Session.IsClient || !NetworkRuntime.HasClientOperation
                || Lifecycle.Current == Lifecycle.Phase.ClearingSession)
			{
				DebugConsole.Log("[GameClient] ContinueConnectionFlow requires an active admitted client - ignoring");
				return;
			}

			DebugConsole.Log($"[GameClient] ContinueConnectionFlow - IsInMenu: {Utils.IsInMenu()}, IsInGame: {Utils.IsInGame()}, HardSyncInProgress: {IsHardSyncInProgress}");
			ReadyManager.SendReadyStatusPacket(ClientReadyState.Unready);
            if (!IsCurrent()) return;

			if (Utils.IsInMenu())
			{
				DebugConsole.Log("[GameClient] Client is in menu - requesting save file or sending ready status");
				MultiplayerOverlay.Show(string.Format(ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.WAITING_FOR_PLAYER, SteamFriends.GetFriendPersonaName(MultiplayerSession.HostUserID.AsCSteamID())));
                if (!IsCurrent()) return;
				DebugConsole.Log("[GameClient] Requesting synchronized save file from host");
				PacketSender.SendToHost(new SynchronizationRequestPacket { Requester = MultiplayerSession.LocalUserID });
			}
			else if (Utils.IsInGame())
			{
				DebugConsole.Log("[GameClient] Client reconnected while in game; requesting host synchronization");
				PacketSender.SendToHost(new SynchronizationRequestPacket { Requester = MultiplayerSession.LocalUserID });
			}
			else
			{
				DebugConsole.LogWarning("[GameClient] Client is neither in menu nor in game - unexpected state");
			}
		}

		private static IEnumerator ShowMessageAndReturnToTitle(string reason = "", string message = "", bool skipIfReconnected = false)
        {
            var owner = NetworkRuntime.TransportClient;
            return ShowMessageAndReturnToTitle(owner, owner.Operation, reason, message, skipIfReconnected);
        }

        private static IEnumerator ShowMessageAndReturnToTitle(ONI_United.Networking.Transport.TransportClient owner,
            object operation, string reason, string message, bool skipIfReconnected)
		{
			string displayText;
			if (string.IsNullOrEmpty(reason) && string.IsNullOrEmpty(message))
			{
				displayText = ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.MENU_LOST_CONNECTION;
			}
			else if (string.IsNullOrEmpty(reason))
			{
				displayText = message;
			}
			else if (string.IsNullOrEmpty(message))
			{
				displayText = reason;
			}
			else
			{
				displayText = $"{reason}\n\n{message}";
			}

			MultiplayerOverlay.Show(displayText);
			yield return new WaitForSecondsRealtime(3f);
            if (!ReferenceEquals(owner, NetworkRuntime.TransportClient)
                || !owner.IsCurrentOperation(operation)) yield break;
            if (skipIfReconnected && (NetworkRuntime.HasHostOperation
                || (NetworkRuntime.HasClientOperation && Lifecycle.Current != Lifecycle.Phase.ClearingSession))) yield break;
			//PauseScreen.TriggerQuitGame(); // Force exit to frontend, getting a crash here
			if (Utils.IsInGame())
			{
				Utils.ForceQuitGame();
			}
			App.LoadScene("frontend");

			MultiplayerOverlay.Close();
            NetworkIdentityRegistry.Clear();
            // Menu return must not implicitly retry an unresolved native cleanup.
            if (Lifecycle.Current != Lifecycle.Phase.ClearingSession)
                NetworkRuntime.Stop();
		}
	}
}
