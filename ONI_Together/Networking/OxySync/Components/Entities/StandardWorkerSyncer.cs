using System;
using System.Collections.Generic;
using HarmonyLib;
using ONI_Together.DebugTools;
using ONI_Together.Networking.Components;
using Shared.OxySync;
using Shared.OxySync.Attributes;
using Shared.Profiling;
using UnityEngine;

namespace ONI_Together.Networking.OxySync.Components.Entities
{
    [SkipSaveFileSerialization]
    [FixedInterestGroup]
	public class StandardWorkerSyncer : NetworkBehaviour
	{
        private static readonly bool ENABLE_LOG = false;

        [MyCmpGet]
        private StandardWorker worker;
        public StandardWorker Worker => worker;

        [MyCmpGet]
        private AnimEventHandler animEventHandler;

        [MyCmpGet]
        private AnimSyncer animSyncer;

        public string EntityName => gameObject?.GetProperName() ?? "Unknown Entity";

        private int workDepth;
        public bool IsInWorkScope => workDepth > 0;

        private bool CanSend => isServer && MultiplayerSession.SessionHasPlayers;

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();

            InterestGroup = -1;

            if (ENABLE_LOG)
                DebugConsole.LogSuccess($"[StandardWorkerSyncer][ON_PREFAB_INIT] {EntityName}:{NetId} initialized.");
        }

        public override void OnSpawn()
        {
            base.OnSpawn();

            worker ??= GetComponent<StandardWorker>();
            animEventHandler ??= GetComponent<AnimEventHandler>();
            animSyncer ??= GetComponent<AnimSyncer>();

            if (ENABLE_LOG)
                DebugConsole.LogSuccess($"[StandardWorkerSyncer][ON_SPAWN] {EntityName}:{NetId} spawned.");
        }

		public override void OnCleanUp()
		{
            if (ENABLE_LOG)
                DebugConsole.LogSuccess($"[StandardWorkerSyncer][ON_CLEANUP] {EntityName}:{NetId} cleaning up.");

			base.OnCleanUp();
		}

        public void RequestStartWork(WorkerBase.StartWorkInfo workInfo)
        {
            using var _ = Profiler.Scope();

            if (!CanSend || !TryGetWorkableInfo(workInfo?.workable, out var workableId, out var workableTypeName))
                return;

            LogRoutine("REQUEST", "START_WORK", workableId, workableTypeName);

            CallClientRpc(nameof(RpcStartWork), workableId, workableTypeName);
        }

        public void RequestBeginComplete()
        {
            using var _ = Profiler.Scope();

            if (!CanSend || !TryGetWorkableInfo(out var workableId, out var workableTypeName))
                return;

            bool isCompleted = worker.successFullyCompleted;
            LogRoutine(
                "REQUEST", "BEGIN_COMPLETE",
                workableId, workableTypeName,
                $"successFullyCompleted={isCompleted}");

            CallClientRpc(nameof(RpcBeginComplete), isCompleted, workableId, workableTypeName);
        }

        public void RequestCompleteWork()
        {
            using var _ = Profiler.Scope();

            if (!CanSend || !TryGetWorkableInfo(out var workableId, out var workableTypeName))
                return;

            LogRoutine("REQUEST", "COMPLETE_WORK", workableId, workableTypeName);

            CallClientRpc(nameof(RpcCompleteWork), workableId, workableTypeName);
        }

        public void RequestAbortWork()
        {
            using var _ = Profiler.Scope();

            if (!CanSend || !TryGetWorkableInfo(out var workableId, out var workableTypeName))
                return;

            bool isCompleted = worker.successFullyCompleted;
            LogRoutine("REQUEST", "ABORT_WORK", workableId, workableTypeName, $"successFullyCompleted={isCompleted}");

            CallClientRpc(nameof(RpcStopWork), isCompleted, workableId, workableTypeName);
        }

        public void RequestUpdateWorkTarget(Vector3 pos)
        {
            if (!CanSend) return;

            if (ENABLE_LOG)
            {
                TryGetWorkableInfo(out var workableId, out var workableTypeName);

                LogRoutine("REQUEST", "UPDATE_WORK_TARGET", workableId, workableTypeName, $"target={pos}");
            }

            CallClientRpc(nameof(RpcUpdateWorkTarget), pos);
        }

        [ClientRpc(SendMode = (int)PacketSendMode.Reliable)]
        private void RpcStartWork(int workableId, string workableTypeName) {
            LogRoutine("RPC", "START_WORK", workableId, workableTypeName);

            var workableType = AccessTools.TypeByName(workableTypeName);
            if (workableType == null)
            {
                string fullName = workableTypeName.Split(',')[0].Trim();

                workableType = AccessTools.TypeByName(fullName);
            }
            if (workableType == null)
            {
                LogUnexpected("RpcStartWork", $"Failed to resolve workable type: {workableTypeName}");
                return;
            }

            if (!NetworkIdentityRegistry.TryGet(workableId, out var workableIdentity))
            {
                LogUnexpected("RpcStartWork", $"Failed to resolve workable identity: {workableId}");
                return;
            }

            var workableCmp = workableIdentity.gameObject.GetComponent(workableType);
            if (workableCmp == null || workableCmp is not Workable workable)
            {
                LogUnexpected("RpcStartWork", $"Failed to get workable component: {workableTypeName}:{workableId}");
                return;
            }

            RunInWorkScope(() =>
            {
                if (worker.GetState() != WorkerBase.State.Idle)
                {
                    worker.successFullyCompleted = false;
                    worker.StopWork();
                }

                worker.StartWork(new WorkerBase.StartWorkInfo(workable));
            });
        }

        [ClientRpc(SendMode = (int)PacketSendMode.Reliable)]
        private void RpcBeginComplete(bool isCompleted, int workableId, string workableTypeName)
        {
            LogRoutine("RPC", "BEGIN_COMPLETE", workableId, workableTypeName, $"successFullyCompleted={isCompleted}");

            if (!IsCurrentWork(workableId, workableTypeName))
                return;

            RunInWorkScope(() =>
            {
                worker.successFullyCompleted = isCompleted;
                worker.StartPlayingPostAnim();
            });
        }

        [ClientRpc(SendMode = (int)PacketSendMode.Reliable)]
        private void RpcCompleteWork(int workableId, string workableTypeName)
        {
            LogRoutine("RPC", "COMPLETE_WORK", workableId, workableTypeName);

            if (!IsCurrentWork(workableId, workableTypeName))
                return;

            RunInWorkScope(() =>
            {
                worker.CompleteWork();
            });
        }

        [ClientRpc(SendMode = (int)PacketSendMode.Reliable)]
        private void RpcStopWork(bool isCompleted, int workableId, string workableTypeName)
        {
            LogRoutine("RPC", "STOP_WORK", workableId, workableTypeName, $"successFullyCompleted={isCompleted}");

            if (!IsCurrentWork(workableId, workableTypeName))
                return;

            RunInWorkScope(() =>
            {
                worker.successFullyCompleted = isCompleted;
                worker.StopWork();
            });
        }

        [ClientRpc(SendMode = (int)PacketSendMode.Reliable)]
        private void RpcUpdateWorkTarget(Vector3 pos)
        {
            if (ENABLE_LOG)
            {
                TryGetWorkableInfo(out var workableId, out var workableTypeName);
                var state = worker.GetState();
                LogRoutine("RPC", "UPDATE_WORK_TARGET", workableId, workableTypeName, $"target={pos} state={state}");
            }

            animEventHandler.UpdateWorkTarget(pos);
        }

        private bool IsCurrentWork(int workableId, string workableTypeName)
        {
            var workable = worker.GetWorkable();
            if (workable == null || workable.IsNullOrDestroyed())
            {
                if (ENABLE_LOG)
                    DebugConsole.LogNonImportant(
                        $"[StandardWorkerSyncer][IsCurrentWork] {EntityName}:{NetId} has no current workable");

                return false;
            }

            int currentId = workable.GetComponent<NetworkIdentity>()?.NetId ?? 0;
            string expectedTypeName = workableTypeName.Split(',')[0].Trim();

            bool matches = currentId == workableId && workable.GetType().FullName == expectedTypeName;
            if (!matches && ENABLE_LOG)
                DebugConsole.LogNonImportant(
                    $"[StandardWorkerSyncer][IsCurrentWork] {EntityName}:{NetId} " +
                    $"current={currentId}:{workable.GetType().FullName} " +
                    $"received={workableId}:{expectedTypeName}");

            return matches;
        }

        private void RunInWorkScope(System.Action action)
        {
            if (worker == null)
            {
                DebugConsole.LogError($"[StandardWorkerSyncer] Worker component is missing on {EntityName}:{NetId}");
                return;
            }

            workDepth++;
            try
            {
                action.Invoke();
            }
            finally
            {
                workDepth = Mathf.Max(0, workDepth - 1);
            }
        }

        public void BeginWorkerPlayback(int workableId)
        {
            if (workableId == 0)
                return;
            
            animSyncer ??= GetComponent<AnimSyncer>();
            if (animSyncer == null)
            {
                LogUnexpected("BeginWorkerPlayback", $"AnimSyncer component is missing.");
            }

            animSyncer.BeginWorkerPlayback(workableId);
        }

        public void EndWorkerPlayback(int workableId)
        {
            if (workableId == 0)
                return;
            
            animSyncer ??= GetComponent<AnimSyncer>();
            if (animSyncer == null)
            {
                LogUnexpected("EndWorkerPlayback", $"AnimSyncer component is missing.");
                return;
            }

            animSyncer.EndWorkerPlayback(workableId);
        }

        private bool TryGetWorkableInfo(out int workableId, out string workableTypeName)
        {
            Workable workable = worker.GetWorkable();
            return TryGetWorkableInfo(workable, out workableId, out workableTypeName);
        }

        private bool TryGetWorkableInfo(Workable workable, out int workableId, out string workableTypeName)
        {
            workableId = 0;
            workableTypeName = "Unknown Type";

            if (workable == null || workable.IsNullOrDestroyed())
                return false;
            
            var identity = workable.GetComponent<NetworkIdentity>();
            if (identity == null || identity.NetId == 0)
                return false;

            workableId = identity.NetId;
            workableTypeName = workable.GetType().AssemblyQualifiedName;

            return true;
        }

        public bool TryGetSyncedWork(out int workableId)
        {
            var startWorkInfo = worker.GetStartWorkInfo();
            workableId = 0;

            return startWorkInfo != null && CanSync(startWorkInfo, out workableId);
        }

        private static readonly HashSet<Type> workablesToSkip =
        [
            // Pickupables
            typeof(Bottler),
            typeof(IceKettleWorkable),
            typeof(LiquidPumpingStation),
            typeof(Pickupable),

            // Edible
            typeof(Edible),

            // DehydratedFoodPackage.RehydrateStartWorkItem
            typeof(DehydratedFoodPackage),

            // Not sure
            typeof(DefragmentationZone),
            typeof(RancherChore.RancherWorkable),
            typeof(Sleepable),
            typeof(ClusterTelescope.ClusterTelescopeIdentifyMeteorWorkable),
        ];

        public bool CanSync(WorkerBase.StartWorkInfo startWorkInfo, out int workableId)
        {
            workableId = 0;

            if (startWorkInfo == null)
                return false;

            string actualTypeName = startWorkInfo.workable?.GetType().Name ?? "Null Workable";
            
            if (startWorkInfo is DehydratedFoodPackage.RehydrateStartWorkItem)
            {
                LogSkippedWorkable("RehydrateStartWorkItem", actualTypeName);
                return false;
            }
            
            if (startWorkInfo is Edible.EdibleStartWorkInfo)
            {
                LogSkippedWorkable("EdibleStartWorkInfo", actualTypeName);
                return false;
            }
            
            if (startWorkInfo is Pickupable.PickupableStartWorkInfo)
            {
                LogSkippedWorkable("PickupableStartWorkInfo", actualTypeName);
                return false;
            }

            if (!CanSync(startWorkInfo.workable, out workableId))
                return false;

            return true;
        }

        public bool CanSync(Workable workable, out int workableId)
        {
            workableId = 0;

            if (workable == null) return false;

            if (!workable.gameObject.TryGetComponent<NetworkIdentity>(out var identity) || identity?.NetId == 0)
            {
                LogUnexpected("CanSync", "Workable has no valid NetworkIdentity");
                return false;
            }

            workableId = identity.NetId;

            if (!workablesToSkip.Contains(workable.GetType()))
                return true;

            LogSkippedWorkable("WorkableToSkip", workable.GetType().Name);
            return false;
        }

        private void LogRoutine(string category, string method, int workableId, string typeName, string msg = null)
        {
            if (!ENABLE_LOG) return;

            DebugConsole.LogNonImportant(
                $"[StandardWorkerSyncer][{category}][{method}] {EntityName}:{NetId} " +
                $"State={worker.GetState()} workable={workableId}:{typeName}" +
                (msg != null ? $" {msg}" : ""));
        }

        private void LogUnexpected(string method, string message)
        {
            DebugConsole.LogWarning(
                $"[StandardWorkerSyncer][{method}] {EntityName}:{NetId} {message}");
        }

        private void LogSkippedWorkable(string category, string typeName)
        {
            if (!ENABLE_LOG) return;

            DebugConsole.LogNonImportant(
                $"[StandardWorkerSyncer][SKIPPED] {EntityName}:{NetId} [{category}:{typeName}]");
        }
	}
}
