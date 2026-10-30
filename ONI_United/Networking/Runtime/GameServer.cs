using ONI_United.DebugTools;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Packets.Handshake;
using Shared.Profiling;
using ONI_United.Networking.Sessions.States;
using Shared;
using System;
using ONI_United.Networking.Presentation;
using ONI_United.Misc;
using UnityEngine;
using ONI_United;
using ONI_United.Networking;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transfer;
using ONI_United.Networking.Transport.LiteNetLib;
using ONI_United.Patches.ToolPatches;
using ONI_United.StateMachines;

namespace ONI_United.Networking.Runtime
{
	public static class GameServer
	{
		private static ServerState _state = ServerState.Stopped;
		public static ServerState State => NetworkConfig.IsLanConfig()
            ? Lifecycle.Current switch
            {
                Lifecycle.Phase.Hosting => ServerState.Started,
                Lifecycle.Phase.StartingHost => ServerState.Starting,
                Lifecycle.Phase.Stopping => ServerState.Stopping,
                _ => ServerState.Stopped
            }
            : _state;

		public const float MaxMissedTicks = 3f;

		private static float _tickAccumulator;
		private static float _tickInterval = 1f / 60f;
		public static float TickInterval => _tickInterval;

		private static void SetState(ServerState newState)
		{
			using var _ = Profiler.Scope();

			if (_state != newState)
			{
				_state = newState;
				DebugConsole.Log($"[GameServer] State changed to: {_state}");
			}
		}

		public static void RefreshTickRate()
		{
			if (Configuration.Instance?.Host?.Server != null)
				_tickInterval = 1f / TickRateToTps(Configuration.Instance.Host.Server.TickRate);
		}

		private static int TickRateToTps(ServerTickRate rate) => rate switch
		{
			ServerTickRate.TPS_20 => 20,
			ServerTickRate.TPS_30 => 30,
			ServerTickRate.TPS_60 => 60,
			ServerTickRate.TPS_90 => 90,
			ServerTickRate.TPS_120 => 120,
			ServerTickRate.TPS_128 => 128,
			_ => 60,
		};

        internal static void InitializeHostTick()
        {
            RefreshTickRate();
            _tickAccumulator = 0f;
        }

        private static LiteNetLibServer lanServer;
        internal static string LanError { get; private set; }

        internal static void StartLanHost()
        {
            LanError = null;
            try
            {
                if (!Utils.IsInGame() || Grid.CellCount <= 0)
                    throw new InvalidOperationException("Load a colony before starting a LAN host.");
                if (Session.IsActive || Session.Players.Count != 0 || GameClient.State != ClientState.Disconnected
                    || (!NetworkConfig.IsLanConfig() && _state != ServerState.Stopped))
                    throw new InvalidOperationException("A multiplayer session or connection is already active.");
                int port = Configuration.Instance.Host.LanSettings.Port;
                if (port < 1 || port > 65535)
                    throw new InvalidOperationException("The configured LAN port is outside the valid range.");

                NetworkConfig.UpdateTransport(NetworkConfig.NetworkTransport.LITENETLIB);
                if (Lifecycle.Current != Lifecycle.Phase.StartingHost) return;
                var server = NetworkConfig.TransportServer as LiteNetLibServer
                    ?? throw new InvalidOperationException("The LAN server transport is unavailable.");
                lanServer = server;
                NetworkIdentityRegistry.RebuildFromScene();
                if (Lifecycle.Current != Lifecycle.Phase.StartingHost) return;
                server.Prepare();
                CursorManager.Instance.AssignColor();
                if (Lifecycle.Current != Lifecycle.Phase.StartingHost) return;
                InitializeHostTick();
                server.Start();
                if (Lifecycle.Current != Lifecycle.Phase.StartingHost) return;
                if (!server.IsRunning)
                    throw new InvalidOperationException(server.LastStartFailure ?? "LAN listener startup failed.");
                bool registered = MultiplayerSession.RegisterAsHost();
                if (Lifecycle.Current != Lifecycle.Phase.StartingHost) return;
                if (!registered) throw new InvalidOperationException("The local host session could not be registered.");
                server.OnSessionPlayerAdmitted(Session.LocalPlayerId);
                if (Lifecycle.Current != Lifecycle.Phase.StartingHost) return;
                if (!server.IsRunning || !Session.IsActive || !Session.IsHost || Session.LocalPlayerId == 0
                    || !Session.Players.ContainsKey(Session.LocalPlayerId))
                    throw new InvalidOperationException("Listener or local host membership confirmation failed.");

                Lifecycle.Handle(Lifecycle.Event.Started,
                    () => DebugConsole.Log("[GameServer] LAN host startup completed."));
            }
            catch (Exception ex) { FailLanStartup(ex.Message); }
        }

        internal static void FailLanStartup(string reason)
        {
            LanError = reason;
            DebugConsole.LogError("[GameServer] LAN host startup failed: " + reason);
            Lifecycle.Handle(Lifecycle.Event.Stop, StopLanHost);
        }

        internal static void StopLanHost()
        {
            StopLanHostAfter(LogLanStopped);
        }

        internal static void StopLanHostAndLoad(string filename)
        {
            StopLanHostAfter(() =>
            {
                LogLanStopped();
                MultiplayerSession.ShouldHostAfterLoad = true;
                LoadScreen.DoLoad(filename);
            });
        }

        private static void StopLanHostAfter(System.Action afterStop)
        {
            var server = lanServer;
            if (server == null)
            {
                // Startup failed before acquiring a LAN owner. Do not disturb an unmigrated session.
                CompleteLanStop(afterStop);
                return;
            }
            // TCP may still be unwinding. The continuation belongs to this network operation,
            // captures a requested filename if necessary, and never captures a dialog.
            server.BeginShutdown((success, error) => FinishLanStop(server, success, error, afterStop));
        }

        private static void FinishLanStop(LiteNetLibServer server, bool success, string error, System.Action afterStop)
        {
            if (!ReferenceEquals(lanServer, server)) return;
            string failure = ClearLanHostState(success ? null : error);
            // Session teardown invokes domain notifications; disposal may run synchronously there.
            if (!ReferenceEquals(lanServer, server)) return;
            if (failure != null)
            {
                LanError = failure;
                DebugConsole.LogError("[GameServer] LAN host cleanup failed: " + failure);
                return;
            }
            CompleteLanStop(afterStop);
        }

        private static void CompleteLanStop(System.Action afterStop)
        {
            lanServer = null;
            LanError = null;
            Lifecycle.Handle(Lifecycle.Event.Stopped, () =>
            {
                try { afterStop(); }
                catch (Exception ex) { DebugConsole.LogError("[GameServer] Post-shutdown action failed: " + ex); }
            });
        }

        private static string ClearLanHostState(string networkFailure)
        {
            var errors = new System.Collections.Generic.List<string>();
            if (networkFailure != null) errors.Add(networkFailure);
            void Clear(System.Action action)
            {
                try { action(); }
                catch (Exception ex) { errors.Add(ex.Message); }
            }
            // Domain owners retain their work and order, after networking has stopped or failed:
            // session membership/tracking, identity lookup, then selection presentation.
            Clear(MultiplayerSession.Clear);
            Clear(NetworkIdentityRegistry.Clear);
            Clear(SelectToolPatch.UpdateColor);
            if (Session.IsActive || Session.Players.Count != 0)
                errors.Add("Host membership was not fully cleared.");
            return errors.Count == 0 ? null : string.Join("; ", errors);
        }

        private static void LogLanStopped() => DebugConsole.Log("[GameServer] LAN host cleanup completed.");

        internal static void DisposeLanHost()
        {
            var server = lanServer;
            lanServer = null;
            if (server == null) return; // Client and Steam resources retain their existing quit path.
            LanError = ClearLanHostState(server.DisposeShutdown());
            if (LanError != null) DebugConsole.LogError("[GameServer] LAN host disposal failed: " + LanError);
        }

        // Steam retains its existing synchronous start API.
		public static bool Start()
		{
			using var _ = Profiler.Scope();

            if (NetworkConfig.IsLanConfig())
                throw new InvalidOperationException("LAN startup requires Lifecycle.Handle(Start, GameServer.StartLanHost).");

			if (_state == ServerState.Started)
				return true;

			// A server started - or restarted - inside a loaded world: NetworkConfig.Stop
			// wiped the identity registry, and the objects of that world never register
			// twice, so every NetId a client sent came back "not found" until the host
			// reloaded the save. Rebuild the registry from the scene first.
			if (Utils.IsInGame())
			{
				int count = NetworkIdentityRegistry.RebuildFromScene();
				DebugConsole.Log($"[GameServer] Identity registry rebuilt from the loaded world: {count} objects");
			}

			SetState(ServerState.Preparing);

			NetworkConfig.TransportServer.OnError = () => SetState(ServerState.Error);
			NetworkConfig.TransportServer.Prepare();
			if (_state == ServerState.Error)
			{
				NetworkConfig.TransportServer.Stop();
				return false;
			}

			CursorManager.Instance.AssignColor();

			RefreshTickRate();
			_tickAccumulator = 0f;

			SetState(ServerState.Starting);

			NetworkConfig.TransportServer.Start();

            if (_state == ServerState.Error)
            {
                NetworkConfig.TransportServer.Stop();
                return false;
            }

			DebugConsole.Log("[GameServer] Game Server started!");

			SetState(ServerState.Started);
			return true;
		}

		public static void Shutdown()
		{
			using var _ = Profiler.Scope();

            if (NetworkConfig.IsLanConfig())
            {
                if (!Lifecycle.Handle(Lifecycle.Event.Stop, StopLanHost))
                    DebugConsole.LogError("[GameServer] Stop is not permitted in the current lifecycle state.");
                return;
            }

			SetState(ServerState.Stopped);

			NetworkConfig.TransportServer.CloseConnections();
			NetworkConfig.TransportServer.Stop();

			DebugConsole.Log("[GameServer] Shutdown complete.");
		}

		public static void Update()
		{
			using var _ = Profiler.Scope();

			_tickAccumulator += Time.unscaledDeltaTime;
			_tickAccumulator = Mathf.Min(_tickAccumulator, _tickInterval * MaxMissedTicks);
			if (_tickAccumulator < _tickInterval)
				return;
			_tickAccumulator -= _tickInterval;

			switch (State)
			{
				case ServerState.Started:
					try
					{
						NetworkConfig.TransportServer.Update();
						NetworkConfig.TransportServer.OnMessageRecieved();

						// Check for lost chunks and retransmit specific missing chunks
						SaveFileTransferManager.CheckForLostChunks();
					}
					catch (Exception ex)
					{
						DebugConsole.LogError($"[GameServer] Error in server update: {ex}");
					}
					break;

				case ServerState.Preparing:
				case ServerState.Starting:
				case ServerState.Stopped:
				case ServerState.Error:
				default:
					// No server activity in these states.
					break;
			}
		}
	}
}
