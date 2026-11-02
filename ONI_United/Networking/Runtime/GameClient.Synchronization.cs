#nullable enable
using System;
using System.Collections;
using ONI_United.Core;
using ONI_United.DebugTools;
using ONI_United.Misc;
using ONI_United.Misc.World;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Sessions.States;
using ONI_United.Networking.Transfer;
using ONI_United.Patches.ToolPatches;
using UnityEngine;

namespace ONI_United.Networking.Runtime
{
    public static partial class GameClient
    {
        public static ulong ActiveSynchronizationId { get; private set; }
        private static bool _loadSubmitted;
        private static bool _worldSpawnComplete;

        internal static bool IsCurrentSynchronization(ulong synchronizationId) =>
            Session.IsClient && Lifecycle.Current == Lifecycle.Phase.Synchronizing
            && synchronizationId != 0 && synchronizationId == ActiveSynchronizationId;

        internal static bool CanReceiveSynchronizationSave(ulong synchronizationId) =>
            IsCurrentSynchronization(synchronizationId) && !_loadSubmitted;

        internal static Func<bool> CaptureSynchronization(ulong synchronizationId)
        {
            return () => IsCurrentSynchronization(synchronizationId);
        }

        internal static void ResetSynchronizationState()
        {
            ActiveSynchronizationId = 0;
            _loadSubmitted = _worldSpawnComplete = false;
            SaveChunkAssembler.Clear();
        }

        internal static bool SubmitSynchronizationSave(ulong synchronizationId, WorldSave save, bool delay)
        {
            if (!CanReceiveSynchronizationSave(synchronizationId))
            {
                DebugConsole.LogWarning($"[Synchronization] Ignored duplicate/stale save completion for sync {synchronizationId}.");
                return false;
            }
            var isCurrent = CaptureSynchronization(synchronizationId);
            _loadSubmitted = true;
            SaveChunkAssembler.Clear();
            CoroutineRunner.RunOne(LoadSynchronizationSave());
            return true;

            IEnumerator LoadSynchronizationSave()
            {
                if (delay) yield return new WaitForSecondsRealtime(1f);
                if (!isCurrent()) yield break;
                SaveHelper.RequestWorldLoad(save, isCurrent);
            }
        }

        public static bool BeginSynchronization(ulong synchronizationId)
        {
            if (!Session.IsClient) return false;
            if (synchronizationId == 0)
            {
                DebugConsole.LogWarning($"[Synchronization] Ignored invalid HardSync {synchronizationId}.");
                return false;
            }
            if (Lifecycle.Current == Lifecycle.Phase.Synchronizing)
            {
                DebugConsole.LogWarning($"[Synchronization] Rejected HardSync {synchronizationId} while loading {ActiveSynchronizationId}.");
                return false;
            }
            if (!Lifecycle.Handle(Lifecycle.Event.StartClientSync)) return false;
            ActiveSynchronizationId = synchronizationId;
            _loadSubmitted = _worldSpawnComplete = false;
            SaveChunkAssembler.Clear();
            SpeedControlScreen.Instance?.Pause(false);
            return IsCurrentSynchronization(synchronizationId);
        }

        public static void OnWorldSpawnComplete()
        {
            if (!IsCurrentSynchronization(ActiveSynchronizationId) || !_loadSubmitted
                || !Utils.IsInGame() || Grid.CellCount <= 0) return;
            _worldSpawnComplete = true;
            ReadyManager.SendReadyStatusPacket(ClientReadyState.Ready);
        }

        public static bool CompleteSynchronization(ulong synchronizationId)
        {
            if (!IsCurrentSynchronization(synchronizationId) || !_worldSpawnComplete)
            {
                DebugConsole.LogWarning($"[Synchronization] Ignored AllReady for inactive/unloaded sync {synchronizationId}.");
                return false;
            }
            if (!Lifecycle.Handle(Lifecycle.Event.CompleteSync)) return false;
            ResetSynchronizationState();
            MultiplayerSession.CreateConnectedPlayerCursors();
            SelectToolPatch.UpdateColor();
            return Lifecycle.Current == Lifecycle.Phase.InGameSession;
        }
    }
}
