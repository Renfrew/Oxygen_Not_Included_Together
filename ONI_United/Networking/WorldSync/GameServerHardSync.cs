using ONI_United.Core;
using ONI_United.DebugTools;
using ONI_United.Networking.Packets.Core;
using ONI_United.Networking.Packets.World;
using System;
using System.Collections;
using Shared.Profiling;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Misc.World;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Sessions;

namespace ONI_United.Networking.WorldSync
{
	public static class GameServerHardSync
	{
		private const float ClientTrafficDrainSeconds = 0.15f;
		private static byte[] _synchronizationSnapshot;

		public static bool hardSyncDoneThisCycle = false;
		private static bool _consumeDailyUse;
		public static bool IsHardSyncInProgress => ReadyManager.IsSynchronizing;
		internal static byte[] SynchronizationSnapshot => _synchronizationSnapshot;

		public static void PerformHardSync(bool consumeDailyUse = false)
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHost)
			{
				DebugConsole.LogWarning("[HardSync] Only the host can start synchronization.");
				return;
			}
            var owner = NetworkRuntime.TransportServer;
            var operation = NetworkRuntime.ActiveOperation;

			if (ReadyManager.IsSynchronizing)
			{
				DebugConsole.Log("[HardSync] Synchronization is already in progress.");
				return;
			}

			if (!ReadyManager.BeginSynchronization())
			{
				DebugConsole.LogWarning("[HardSync] Could not start synchronization.");
				return;
			}

			if (SpeedControlScreen.Instance?.IsPaused != true)
				SpeedControlScreen.Instance?.TogglePause();
            if (!NetworkRuntime.IsCurrentOperation(owner, operation) || !Session.IsHost) return;

			_consumeDailyUse = consumeDailyUse;
			ulong synchronizationId = ReadyManager.ActiveSynchronizationId;
			hardSyncDoneThisCycle = false;
			int clientCount = 0;
			foreach (ulong participant in ReadyManager.SynchronizationParticipants)
			{
				clientCount++;
				PacketSender.SendToPlayer(participant, new HardSyncPacket { SynchronizationId = synchronizationId });
                if (!NetworkRuntime.IsCurrentOperation(owner, operation)
                    || !ReadyManager.IsCurrentSynchronization(synchronizationId)) return;
			}

			foreach (PlayerCursor cursor in MultiplayerSession.PlayerCursors.Values)
				cursor.SetVisibility(false);

			ReadyManager.StartGameplayDrain(ClientTrafficDrainSeconds);
			DebugConsole.Log($"[HardSync] Synchronization started for {clientCount} client(s).");
			CoroutineRunner.RunOne(DrainClientTrafficThenTransfer(
                owner, operation, synchronizationId));
			ReadyManager.RefreshReadyState();
		}

		private static IEnumerator DrainClientTrafficThenTransfer(
            ONI_United.Networking.Transport.TransportServer owner, object operation, ulong synchronizationId)
		{
            bool IsCurrent() => NetworkRuntime.IsCurrentOperation(owner, operation)
                && ReadyManager.IsCurrentSynchronization(synchronizationId);
			while (IsCurrent()
				&& UnityEngine.Time.realtimeSinceStartup < ReadyManager.GameplayDrainUntil)
				yield return null;

			if (!IsCurrent())
				yield break;

			// Close gameplay admission before yielding so admitted dispatches finish before capture.
			yield return null;
			if (!IsCurrent())
				yield break;

			try
			{
                var snapshot = SaveHelper.GetWorldSave();
                if (!IsCurrent()) yield break;
				_synchronizationSnapshot = snapshot;
				SaveFileRequestPacket.SendSaveFileToAll(_synchronizationSnapshot, synchronizationId);
                if (!IsCurrent()) yield break;
				ReadyManager.RefreshReadyState();
			}
			catch (Exception ex)
			{
				DebugConsole.LogError($"[HardSync] Failed to capture or start the synchronization snapshot transfer: {ex}");
                if (IsCurrent()) NetworkRuntime.StopHost();
			}
		}

		internal static void OnSynchronizationCompleted()
		{
			if (_consumeDailyUse)
				hardSyncDoneThisCycle = true;

			_consumeDailyUse = false;
			_synchronizationSnapshot = null;
		}

		internal static void ResetSynchronizationState()
		{
			_consumeDailyUse = false;
			SaveFileRequestPacket.ClearPendingSynchronizationTransfers();
			_synchronizationSnapshot = null;
		}
	}
}
