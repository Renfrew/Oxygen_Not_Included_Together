using ONI_United.DebugTools;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Packets.Handshake;
using Shared.Profiling;
using ONI_United.Networking.Session.States;
using Shared;
using System;
using ONI_United.Networking.Presentation;
using ONI_United.Misc;
using UnityEngine;
using ONI_United;
using ONI_United.Networking;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Transfer;

namespace ONI_United.Networking.Lifecycle
{
	public static class GameServer
	{
		private static ServerState _state = ServerState.Stopped;
		public static ServerState State => _state;

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

		public static void Start()
		{
			using var _ = Profiler.Scope();

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
			CursorManager.Instance.AssignColor();

			RefreshTickRate();
			_tickAccumulator = 0f;

			SetState(ServerState.Starting);

			NetworkConfig.TransportServer.Start();

			DebugConsole.Log("[GameServer] Game Server started!");

			SetState(ServerState.Started);
		}

		public static void Shutdown()
		{
			using var _ = Profiler.Scope();

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
