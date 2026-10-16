using ONI_Together.DebugTools;
using ONI_Together.Menus;
using ONI_Together.Misc;
using ONI_Together.Networking.Packets.Core;
using ONI_Together.Networking.States;
using ONI_Together.Networking.Transport.Steamworks;
using Steamworks;
using Shared.Profiling;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ONI_Together.Networking.Packets.World;

namespace ONI_Together.Networking
{
	public class ReadyManager
	{
		private const float HostOverlayCloseGraceSeconds = 1f;
		private static readonly HashSet<ulong> Participants = new HashSet<ulong>();
		public static ulong LastIssuedSynchronizationId { get; private set; }
		public static ulong ActiveSynchronizationId { get; private set; }
		internal static ulong[] SynchronizationParticipants => Participants.ToArray();

		internal static bool IsCurrentSynchronization(ulong synchronizationId) =>
			IsSynchronizing && synchronizationId != 0 && synchronizationId == ActiveSynchronizationId;

		internal static bool IsLoadingParticipant(ulong clientId, ulong synchronizationId) =>
			IsCurrentSynchronization(synchronizationId) && Participants.Contains(clientId)
			&& MultiplayerSession.ConnectedPlayers.TryGetValue(clientId, out var player)
			&& player.Connection != null && player.ProtocolVerified && player.readyState == ClientReadyState.Loading;

		internal static void RequestSynchronization(ulong requester)
		{
			if (!MultiplayerSession.IsHost)
				return;
			if (!MultiplayerSession.ConnectedPlayers.TryGetValue(requester, out var player)
				|| requester == MultiplayerSession.HostUserID || player.Connection == null || !player.ProtocolVerified)
			{
				DebugConsole.LogWarning($"[Synchronization] Invalid requester {requester}.");
				return;
			}
			if (IsSynchronizing)
			{
				if (Participants.Contains(requester))
					return;
				DebugConsole.LogWarning($"[Synchronization] Rejecting late join from {requester}.");
				NetworkConfig.TransportServer?.KickClient(requester);
				return;
			}
			GameServerHardSync.PerformHardSync();
		}

		internal static void RetrySynchronizationTransfer(ulong requester, ulong synchronizationId, bool forceGeneric)
		{
			if (!MultiplayerSession.IsHost)
				return;
			if (!IsLoadingParticipant(requester, synchronizationId))
			{
				DebugConsole.LogWarning($"[Synchronization] Rejected retry from {requester} for sync {synchronizationId}.");
				return;
			}
			byte[] snapshot = GameServerHardSync.SynchronizationSnapshot;
			if (snapshot == null)
			{
				DebugConsole.LogWarning($"[Synchronization] Snapshot for sync {synchronizationId} is not yet available.");
				return;
			}
			SaveFileRequestPacket.SendSaveFile(requester, SaveHelper.WorldName + ".sav", snapshot, synchronizationId, forceGeneric);
		}

		internal static void RemoveParticipant(ulong clientId)
		{
			if (!Participants.Remove(clientId))
				return;
			SaveFileRequestPacket.CancelTransfer(clientId);
			RefreshReadyState();
		}

		public static bool IsSynchronizing => ActiveSynchronizationId != 0;
		internal static float GameplayDrainUntil { get; private set; }

		internal static void ResetSynchronizationState()
		{
			if (IsSynchronizing)
			{
				foreach (MultiplayerPlayer player in MultiplayerSession.ConnectedPlayers.Values)
				{
					if (player.PlayerId == MultiplayerSession.HostUserID)
						continue;

					if (player.readyState == ClientReadyState.Loading)
						player.readyState = ClientReadyState.Unready;
				}
			}

			ActiveSynchronizationId = 0;
			Participants.Clear();
			GameplayDrainUntil = 0f;
			GameServerHardSync.ResetSynchronizationState();
		}

		public static bool BeginSynchronization()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHost || IsSynchronizing)
				return false;

			ActiveSynchronizationId = checked(++LastIssuedSynchronizationId);
			Participants.Clear();

			foreach (MultiplayerPlayer player in MultiplayerSession.ConnectedPlayers.Values)
			{
				if (player.PlayerId == MultiplayerSession.HostUserID)
				{
					player.readyState = ClientReadyState.Ready;
					continue;
				}

				if (player.Connection == null || !player.ProtocolVerified)
					continue;

				player.readyState = ClientReadyState.Loading;
				Participants.Add(player.PlayerId);
			}

			RefreshScreen();
			SendStatusUpdatePacketToClients();
			return true;
		}

		internal static void StartGameplayDrain(float durationSeconds)
		{
			GameplayDrainUntil = UnityEngine.Time.realtimeSinceStartup + durationSeconds;
		}

		public static void SetupListeners()
		{
			using var _ = Profiler.Scope();

			SteamLobby.OnLobbyMembersRefreshed += UpdateReadyStateTracking;
		}

		public static void SendAllReadyPacket()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHost)
				return;
			if (!IsSynchronizing || !IsEveryoneReady())
				return;

			ulong synchronizationId = ActiveSynchronizationId;
			foreach (ulong participant in SynchronizationParticipants)
				PacketSender.SendToPlayer(participant, new AllClientsReadyPacket { SynchronizationId = synchronizationId }, PacketSendMode.Reliable);
			ActiveSynchronizationId = 0;
			Participants.Clear();
			SaveFileRequestPacket.ClearPendingSynchronizationTransfers();
			GameplayDrainUntil = 0f;
			GameServerHardSync.OnSynchronizationCompleted();
			MultiplayerOverlay.Show(STRINGS.UI.MP_OVERLAY.SYNC.FINALIZING_SYNC);
			CoroutineRunner.RunOne(CloseHostOverlayAfterGrace(synchronizationId));
		}

		private static IEnumerator CloseHostOverlayAfterGrace(ulong synchronizationId)
		{
			yield return new UnityEngine.WaitForSecondsRealtime(HostOverlayCloseGraceSeconds);
			if (!IsSynchronizing && LastIssuedSynchronizationId == synchronizationId)
				MultiplayerOverlay.Close();
		}

		public static void SendStatusUpdatePacketToClients()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHost)
				return;

			string text = GetScreenText();
			var packet = new ClientReadyStatusUpdatePacket
			{
				SynchronizationId = ActiveSynchronizationId,
				Message = text
			};
			PacketSender.SendToAllClients(packet);
		}

		public static void SendReadyStatusPacket(ClientReadyState state)
		{
			using var _ = Profiler.Scope();

			// Host is always considered ready so it doesn't send these
			if (MultiplayerSession.IsHost)
				return;

			var packet = new ClientReadyStatusPacket
			{
				SenderId = NetworkConfig.GetLocalID(),
				SynchronizationId = state == ClientReadyState.Ready ? GameClient.ActiveSynchronizationId : 0,
				Status = state,
				PlayerName = Utils.GetLocalPlayerName()
			};
			PacketSender.SendToHost(packet);
		}

		public static void SetPlayerReadyState(MultiplayerPlayer player, ClientReadyState state)
		{
			using var _ = Profiler.Scope();

			if (player.PlayerId == MultiplayerSession.HostUserID)
				return;
			if (state == ClientReadyState.Ready
				&& (!Participants.Contains(player.PlayerId) || player.readyState != ClientReadyState.Loading))
				return;

			player.readyState = state;
		}

		public static void RefreshScreen()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.InActiveSession)
				return;

			string text = GetScreenText();
			MultiplayerOverlay.Show(text);
		}

		private static string GetScreenText()
		{
			using var _ = Profiler.Scope();

			int readyCount = GetReadyCount();
			int maxPlayers = IsSynchronizing ? Participants.Count + 1 : MultiplayerSession.ConnectedPlayers.Count;
			string message = string.Format(STRINGS.UI.MP_OVERLAY.SYNC.WAITING_FOR_PLAYERS_SYNC, readyCount, maxPlayers);
			foreach (MultiplayerPlayer player in MultiplayerSession.ConnectedPlayers.Values)
			{
				if (IsSynchronizing && player.PlayerId != MultiplayerSession.HostUserID && !Participants.Contains(player.PlayerId))
					continue;
				message += $"{player.PlayerName}: {GetReadyText(player.readyState)}\n";
			}
			return message;
		}

		private static int GetReadyCount()
		{
			using var _ = Profiler.Scope();

			int count = 0;
			foreach (MultiplayerPlayer player in MultiplayerSession.ConnectedPlayers.Values)
			{
				if (IsSynchronizing && player.PlayerId != MultiplayerSession.HostUserID && !Participants.Contains(player.PlayerId))
					continue;
				if (player.readyState.Equals(ClientReadyState.Ready))
				{
					count++;
				}
			}
			return count;
		}

		private static string GetReadyText(ClientReadyState readyState)
		{
			using var _ = Profiler.Scope();

			switch (readyState)
			{
				case ClientReadyState.Ready:
					return STRINGS.UI.MP_OVERLAY.SYNC.READYSTATE.READY;
				case ClientReadyState.Unready:
					return STRINGS.UI.MP_OVERLAY.SYNC.READYSTATE.UNREADY;
				case ClientReadyState.Loading:
					return global::STRINGS.UI.FRONTEND.LOADING;
			}
			return STRINGS.UI.MP_OVERLAY.SYNC.READYSTATE.UNKNOWN;
		}

		private static void UpdateReadyStateTracking(CSteamID id)
		{
			using var _ = Profiler.Scope();

			DebugConsole.LogAssert($"Update ready state tracking for {id}");
			if (!MultiplayerSession.IsHost)
				return;
			if (MultiplayerOverlay.IsOpen)
				RefreshScreen();
		}

		/// <summary>
		/// HOST ONLY - Check if all connected clients are ready
		/// </summary>
		/// <returns></returns>
		public static bool IsEveryoneReady()
		{
			using var _ = Profiler.Scope();

			foreach (ulong participant in Participants)
			{
				if (!MultiplayerSession.ConnectedPlayers.TryGetValue(participant, out var player)
					|| player.readyState != ClientReadyState.Ready)
					return false;
			}
			return true;
		}

		internal static void RefreshReadyState()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.InActiveSession)
				return;

			if (MultiplayerSession.IsQuitting)
				return;

			DebugConsole.Log("Refreshing ready state...");
			bool allReady = IsEveryoneReady();
			SendStatusUpdatePacketToClients();
			if (allReady && IsSynchronizing)
			{
				SendAllReadyPacket();
			}
		}
	}
}
