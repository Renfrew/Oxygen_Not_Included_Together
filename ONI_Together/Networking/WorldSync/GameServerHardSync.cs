using ONI_United.DebugTools;
using ONI_United.Networking.Packets.Core;
using ONI_United.Networking.Packets.World;
using System;
using System.Collections;
using Shared.Profiling;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Misc.World;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Session;

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

			_consumeDailyUse = consumeDailyUse;
			ulong synchronizationId = ReadyManager.ActiveSynchronizationId;
			hardSyncDoneThisCycle = false;
			int clientCount = 0;
			foreach (ulong participant in ReadyManager.SynchronizationParticipants)
			{
				clientCount++;
				PacketSender.SendToPlayer(participant, new HardSyncPacket { SynchronizationId = synchronizationId });
			}

			foreach (PlayerCursor cursor in MultiplayerSession.PlayerCursors.Values)
				cursor.SetVisibility(false);

			ReadyManager.StartGameplayDrain(ClientTrafficDrainSeconds);
			DebugConsole.Log($"[HardSync] Synchronization started for {clientCount} client(s).");
			CoroutineRunner.RunOne(DrainClientTrafficThenTransfer(synchronizationId));
			ReadyManager.RefreshReadyState();
		}

		private static IEnumerator DrainClientTrafficThenTransfer(ulong synchronizationId)
		{
			while (ReadyManager.IsCurrentSynchronization(synchronizationId)
				&& UnityEngine.Time.realtimeSinceStartup < ReadyManager.GameplayDrainUntil)
				yield return null;

			if (!ReadyManager.IsCurrentSynchronization(synchronizationId))
				yield break;

			// Close gameplay admission before yielding so admitted dispatches finish before capture.
			yield return null;
			if (!ReadyManager.IsCurrentSynchronization(synchronizationId))
				yield break;

			try
			{
				_synchronizationSnapshot = SaveHelper.GetWorldSave();
				SaveFileRequestPacket.SendSaveFileToAll(_synchronizationSnapshot, synchronizationId);
				ReadyManager.RefreshReadyState();
			}
			catch (Exception ex)
			{
				DebugConsole.LogError($"[HardSync] Failed to capture or start the synchronization snapshot transfer: {ex}");
				ReadyManager.ResetSynchronizationState();
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
