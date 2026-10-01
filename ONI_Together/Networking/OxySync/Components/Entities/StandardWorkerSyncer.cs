using ONI_Together.DebugTools;
using Shared.OxySync;
using Shared.OxySync.Attributes;
using Shared.Profiling;
using UnityEngine;
using System;
using ONI_Together.Networking.Components;
using HarmonyLib;
using static WorkerBase;
using static RancherChore;
using static ClusterTelescope;

namespace ONI_Together.Networking.OxySync.Components.Entities
{
    [SkipSaveFileSerialization]
    [FixedInterestGroup]
	public class StandardWorkerSyncer : NetworkBehaviour
	{
        // Logging flag for debugging
        private static readonly bool ENABLE_LOG = true;

        public enum MethodType: byte
        {
            StartWork,
            BeginComplete,
            CompleteWork,
            StopWork,
            ProgressHiddenm,
            ProgressFabricator
        }

        [MyCmpGet]
        private StandardWorker worker;

        [MyCmpGet]
        private AnimSyncer animSyncer;

        private string EntityName => gameObject?.GetProperName() ?? "Unknown Entity";

        private int workDepth;
        public bool IsInWorkScope => workDepth > 0;

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();

            // Minions and the creatures should and can move outside the view.
            // To ensure that the world is consistent,
            // sync the reactable so the entity's state remains consistent.
            // For exampple, gas masks station and checkpoints would need to update their remaining gas and equiment,
            // whenever a duplicant takes a mask out or put one back.
            InterestGroup = -1;

            if (ENABLE_LOG)
                DebugConsole.LogSuccess($"[StandardWorkerSyncer][ON_PREFAB_INIT]{EntityName}:{NetId} initialized.");
        }

        public override void OnSpawn()
        {
            base.OnSpawn();

            if (worker == null)
                worker = GetComponent<StandardWorker>();
            
            if (animSyncer == null)
                animSyncer = GetComponent<AnimSyncer>();

            if (ENABLE_LOG)
                DebugConsole.LogSuccess($"[StandardWorkerSyncer][ON_SPAWN]{EntityName}:{NetId} spawned.");
        }

		public override void OnCleanUp()
		{
            if (ENABLE_LOG)
                DebugConsole.LogSuccess($"[StandardWorkerSyncer][ON_CLEANUP]{EntityName}:{NetId} cleaning up.");

			base.OnCleanUp();
		}

        public void RequestSyncWorkingState(StartWorkInfo workInfo, MethodType method)
        {
            using var _ = Profiler.Scope();

            if (!isServer || !MultiplayerSession.SessionHasPlayers)
                return;
            
            if (worker == null)
            {
                DebugConsole.LogError($"[StandardWorkerSyncer] Worker component is missing on {EntityName}:{NetId}");
                return;
            }
            
            Workable workable = null;
            int workableId = 0;

            if (method == MethodType.StartWork)
            {
                if (workInfo == null || workInfo.workable == null)
                    return;
                
                workable = workInfo.workable;
                
                if (!workable.TryGetComponent<NetworkIdentity>(out var identity) || identity.NetId == 0)
                    return;
                
                workableId = identity.NetId;
            }
            else
            {
                workable = worker.GetWorkable();
                workableId = workable?.GetComponent<NetworkIdentity>()?.NetId ?? 0;
            }
            
            string workableType = workable?.GetType().AssemblyQualifiedName ?? string.Empty;
            var time = Time.unscaledTime;

            try
            {
                DebugConsole.LogWarning(
                    $"[StandardWorkerSyncer][Request][{method.ToString()}] " +
                    $"{EntityName}:{NetId} state={worker?.GetState()} " +
                    $"workable={workableId}:{workableType}");

                switch (method)
                {
                    case MethodType.StartWork:
                        CallClientRpc(nameof(RpcStartWork), workableId, workableType);
                        break;
                    case MethodType.BeginComplete:
                        CallClientRpc(nameof(RpcBeginComplete), worker.successFullyCompleted, workableId, workableType);
                        break;
                    case MethodType.CompleteWork:
                        CallClientRpc(nameof(RpcCompleteWork), workableId, workableType);
                        break;
                    case MethodType.StopWork:
                        CallClientRpc(nameof(RpcStopWork), worker.successFullyCompleted, workableId, workableType);
                        break;
                }
            }
            catch (Exception e)
            {
                DebugConsole.LogError($"Failed to request sync working state: {e}");
            }
        }

        [ClientRpc(SendMode = (int)PacketSendMode.Reliable)]
        private void RpcStartWork(int workableId, string workableTypeName) {
            DebugConsole.LogWarning(
                $"[StandardWorkerSyncer][RPC][StartWork] " +
                $"{EntityName}:{NetId} state={worker?.GetState()} " +
                $"workable={workableId}:{workableTypeName}");

            var workableType = AccessTools.TypeByName(workableTypeName);
            if (workableType == null)
            {
                string fullName =
                    workableTypeName.Split(',')[0].Trim();

                workableType =
                    AccessTools.TypeByName(fullName);
            }
            if (workableType == null)
            {
                DebugConsole.LogWarning($"Failed to resolve workable type: {workableTypeName}");
                return;
            }

            if (!NetworkIdentityRegistry.TryGet(workableId, out var workableIdentity))
                return;

            var workableCmp = workableIdentity.gameObject.GetComponent(workableType);
            if (workableCmp == null || workableCmp is not Workable workable)
            {
                return;
            }

            BeginWorkerPlayback(workableId);

            RunInWorkScope(() =>
            {
                if (worker.GetState() != WorkerBase.State.Idle)
                {
                    worker.successFullyCompleted = false;
                    worker.StopWork();
                }

                worker.StartWork(new StartWorkInfo(workable));
            });
        }

        [ClientRpc(SendMode = (int)PacketSendMode.Reliable)]
        private void RpcBeginComplete(bool isSuccessFullyCompleted, int workableId, string workableTypeName)
        {
            DebugConsole.LogWarning(
                $"[StandardWorkerSyncer][RPC][BeginComplete] " +
                $"{EntityName}:{NetId} state={worker?.GetState()} " +
                $"workable={workableId}:{workableTypeName}");

            if (!IsCurrentWork(workableId, workableTypeName))
                return;

            RunInWorkScope(() =>
            {
                worker.successFullyCompleted = isSuccessFullyCompleted;
                worker.StartPlayingPostAnim();
            });
        }

        [ClientRpc(SendMode = (int)PacketSendMode.Reliable)]
        private void RpcCompleteWork(int workableId, string workableTypeName)
        {
            DebugConsole.LogWarning(
                $"[StandardWorkerSyncer][RPC][CompleteWork] " +
                $"{EntityName}:{NetId} state={worker?.GetState()} " +
                $"workable={workableId}:{workableTypeName}");

            if (!IsCurrentWork(workableId, workableTypeName))
                return;

            RunInWorkScope(() =>
            {
                worker.CompleteWork();
            });

            EndWorkerPlayback(workableId);
        }

        [ClientRpc(SendMode = (int)PacketSendMode.Reliable)]
        private void RpcStopWork(bool isSuccessFullyCompleted, int workableId, string workableTypeName)
        {
            DebugConsole.LogWarning(
                $"[StandardWorkerSyncer][RPC][StopWork] " +
                $"{EntityName}:{NetId} state={worker?.GetState()} " +
                $"workable={workableId}:{workableTypeName}");

            if (!IsCurrentWork(workableId, workableTypeName))
                return;

            RunInWorkScope(() =>
            {
                worker.successFullyCompleted = isSuccessFullyCompleted;
                worker.StopWork();
            });

            EndWorkerPlayback(workableId);
        }

        private bool IsCurrentWork(int workableId, string workableTypeName)
        {
            if (worker == null)
                return false;

            var workable = worker.GetWorkable();
            if (workable == null || workable.IsNullOrDestroyed())
                return false;

            int workableNetId = workable.GetComponent<NetworkIdentity>()?.NetId ?? 0;
            return workableNetId == workableId
                && workable.GetType().FullName == workableTypeName.Split(',')[0].Trim();
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
            
            if (animSyncer == null)
                animSyncer = GetComponent<AnimSyncer>();

            animSyncer?.BeginWorkerPlayback(workableId);
        }

        public void EndWorkerPlayback(int workableId)
        {
            if (workableId == 0)
                return;
            
            if (animSyncer == null)
                animSyncer = GetComponent<AnimSyncer>();

            animSyncer?.EndWorkerPlayback(workableId);
        }

        private static readonly Type[] workablesToSkip =
        {
            typeof(DefragmentationZone),
            typeof(RancherWorkable),
            typeof(LiquidPumpingStation),
            typeof(IceKettleWorkable),
            typeof(Sleepable),
            typeof(Bottler),
            typeof(ClusterTelescopeIdentifyMeteorWorkable),
            typeof(Edible),
            typeof(Pickupable)
        };

        public bool CanSync(StartWorkInfo startWorkInfo, out int workableId)
        {
            workableId = 0;

            if (startWorkInfo == null)
                return false;
            
            if (startWorkInfo.workable == null)
            {
                LogSkippedWorkable("NullWorkable", "null");
                return false;
            }

            Type actualType = startWorkInfo.workable.GetType();
            
            if (!startWorkInfo.workable.gameObject.TryGetComponent<NetworkIdentity>(out var identity))
            {
                LogSkippedWorkable("NoNetworkIdentity", actualType.Name);
                return false;
            }
            
            workableId = identity.NetId;

            if (workableId == 0)
            {
                LogSkippedWorkable("InvalidWorkableId", actualType.Name);
                return false;
            }
            
            if (startWorkInfo is DehydratedFoodPackage.RehydrateStartWorkItem)
            {
                LogSkippedWorkable("RehydrateStartWorkItem", actualType.Name);
                return false;
            }
            
            if (startWorkInfo is Edible.EdibleStartWorkInfo)
            {
                LogSkippedWorkable("EdibleStartWorkInfo", actualType.Name);
                return false;
            }
            
            if (startWorkInfo is Pickupable.PickupableStartWorkInfo)
            {
                LogSkippedWorkable("PickupableStartWorkInfo", actualType.Name);
                return false;
            }

            foreach (Type workableType in workablesToSkip)
            {
                if (actualType == workableType)
                {
                    LogSkippedWorkable("Unknown", actualType.Name);
                    return false;
                }
            }

            return true;
        }

        private void LogSkippedWorkable(string category, string typeName)
        {
            DebugConsole.LogNonImportant(
                $"[StandardWorkerSyncer][SKIPPED] {EntityName}:{NetId} [{category}:{typeName}]");
        }
	}
}
