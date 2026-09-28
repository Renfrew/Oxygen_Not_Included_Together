

using System;
using HarmonyLib;
using ONI_Together.DebugTools;
using ONI_Together.Networking;
using ONI_Together.Networking.OxySync.Components.Entities;
using Shared.Profiling;
using UnityEngine;
using static ClusterTelescope;
using static RancherChore;



namespace ONI_Together.Patches.DuplicantActions
{
	internal class StandardWorker_Patches
	{
        [HarmonyPatch(typeof(StandardWorker), nameof(StandardWorker.OnPrefabInit))]
        public class StandardWorker_OnPrefabInit_Patch
        {
            public static void Postfix(StandardWorker __instance)
            {
                using var _ = Profiler.Scope();

                if (__instance == null || __instance.IsNullOrDestroyed())
                    return;
                
                if (!__instance.gameObject.TryGetComponent<KPrefabID>(out var prefabID))
                    return;

                if (!prefabID.HasTag(GameTags.BaseMinion))
                {
                    DebugConsole.Log($"[StandardWorker_Patches] StandardWorker {__instance.name} is not a BaseMinion.");
                    return;
                }

                __instance.gameObject.AddOrGet<StandardWorkerSyncer>();
            }
        }

		[HarmonyPatch(typeof(StandardWorker), nameof(StandardWorker.StartWork))]
		public class StandardWorker_StartWork_Patch
		{
			// SKIP WORKABLE
			private static Type[] workablesToSkip =
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

            public static bool Prefix(StandardWorker __instance, WorkerBase.StartWorkInfo start_work_info)
            {
                using var _ = Profiler.Scope();

                if (__instance == null || __instance.IsNullOrDestroyed())
                    return true;

                if (!__instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    return true;
                
                if (!syncer.CanSync(start_work_info, out var workableId))
                    return true;
                
                if (MultiplayerSession.IsHost && MultiplayerSession.SessionHasPlayers)
                {
                    syncer.BeginWorkerPlayback(workableId);
                    syncer.RequestSyncWorkingState(start_work_info, StandardWorkerSyncer.MethodType.StartWork);
                    return true;
                }

                if (MultiplayerSession.IsClient && !syncer.IsInWorkScope)
                {
                    return false;
                }

                return true;
            }
		}

        [HarmonyPatch(typeof(StandardWorker), "StartPlayingPostAnim")]
        public class StandardWorker_StartPlayingPostAnim_Patch
        {
            public static bool Prefix(StandardWorker __instance)
            {
                using var _ = Profiler.Scope();

                if (__instance == null || __instance.IsNullOrDestroyed())
                    return true;

                if (!__instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    return true;
                
                var startWorkInfo = __instance.GetStartWorkInfo();
                if (startWorkInfo != null && !syncer.CanSync(startWorkInfo, out var _))
                    return true;
                
                if (MultiplayerSession.IsHost && MultiplayerSession.SessionHasPlayers)
                {
                    syncer.RequestSyncWorkingState(startWorkInfo, StandardWorkerSyncer.MethodType.BeginComplete);
                    return true;
                }

                if (MultiplayerSession.IsClient && !syncer.IsInWorkScope)
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(StandardWorker), nameof(StandardWorker.CompleteWork))]
        public class StandardWorker_CompleteWork_Patch
        {
            public static bool Prefix(StandardWorker __instance, out int __state)
            {
                using var _ = Profiler.Scope();

                __state = 0;

                if (__instance == null || __instance.IsNullOrDestroyed())
                    return true;

                if (!__instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    return true;
                
                var startWorkInfo = __instance.GetStartWorkInfo();
                int workableId = 0;
                if (startWorkInfo != null && !syncer.CanSync(startWorkInfo, out workableId))
                    return true;
                
                if (MultiplayerSession.IsHost && MultiplayerSession.SessionHasPlayers)
                {
                    __state = workableId;
                    syncer.RequestSyncWorkingState(startWorkInfo, StandardWorkerSyncer.MethodType.CompleteWork);
                    return true;
                }

                if (MultiplayerSession.IsClient && !syncer.IsInWorkScope)
                {
                    return false;
                }

                return true;
            }

            public static void Postfix(StandardWorker __instance, int __state)
            {
                using var _ = Profiler.Scope();

                if (__state == 0)
                    return;

                if (__instance == null ||  __instance.IsNullOrDestroyed())
                    return;

                if (!MultiplayerSession.IsHost || !MultiplayerSession.SessionHasPlayers)
                    return;
                
                if (!__instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    return;
                
                syncer.EndWorkerPlayback(__state);
            }
        }
        

		[HarmonyPatch(typeof(StandardWorker), nameof(StandardWorker.StopWork))]
		public class StandardWorker_StopWork_Patch
		{
			public static bool Prefix(StandardWorker __instance, out int __state)
			{
				using var _ = Profiler.Scope();

                __state = 0;

                if (__instance == null || __instance.IsNullOrDestroyed())
                    return true;

                if (!__instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    return true;
                
                var startWorkInfo = __instance.GetStartWorkInfo();
                int workableId = 0;
                if (startWorkInfo != null && !syncer.CanSync(startWorkInfo, out workableId))
                    return true;
                
                if (MultiplayerSession.IsHost && MultiplayerSession.SessionHasPlayers)
                {
                    WorkerBase.State state = __instance.GetState();
                    bool isCompleting = state == WorkerBase.State.PendingCompletion || state == WorkerBase.State.Completing;
                    bool isAbort = state == WorkerBase.State.Working || (!__instance.successFullyCompleted && isCompleting);
                    if (isAbort)
                    {
                        __state = workableId;
                        syncer.RequestSyncWorkingState(startWorkInfo, StandardWorkerSyncer.MethodType.StopWork);
                    }
                    return true;
                }

                if (MultiplayerSession.IsClient && !syncer.IsInWorkScope)
                {
                    return false;
                }

                return true;

				// PacketSender.SendToAllClients(WorkableProgressPacket.CreateHidden(workable), PacketSendMode.ReliableImmediate);

				// if (workable.TryGetComponent<ComplexFabricator>(out var fabricator) && fabricator != null && !fabricator.IsNullOrDestroyed())
				// {
				// 	PacketSender.SendToAllClients(WorkableProgressPacket.CreateComplexFabricator(fabricator, showProgressBar: false), PacketSendMode.ReliableImmediate);
				// }
			}

			public static void Postfix(StandardWorker __instance, int __state)
			{
				using var _ = Profiler.Scope();

                if (__state == 0)
                    return;

				if (__instance == null || __instance.IsNullOrDestroyed())
					return;
                
                if (!__instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    return;
                
                syncer.EndWorkerPlayback(__state);
			}
		}
	}
    internal class AnimEventHandler_Patches
    {
        private static bool TryGetWorker(
            AnimEventHandler handler,
            out StandardWorker worker,
            out Workable workable)
        {
            worker = null;
            workable = null;

            if (!MultiplayerSession.InActiveSession)
                return false;

            if (handler == null ||
                !handler.TryGetComponent<StandardWorker>(out worker) ||
                worker == null)
            {
                return false;
            }

            workable = worker.GetWorkable();

            return workable != null &&
                   !workable.IsNullOrDestroyed();
        }

        [HarmonyPatch(
            typeof(AnimEventHandler),
            nameof(AnimEventHandler.SetTargetPos))]
        public class SetTargetPos_Patch
        {
            public static void Postfix(
                AnimEventHandler __instance,
                Vector3 target_pos)
            {
                if (!TryGetWorker(
                    __instance,
                    out var worker,
                    out var workable))
                {
                    return;
                }

                string side = MultiplayerSession.IsHost
                    ? "HOST"
                    : "CLIENT";

                var anim = worker.GetComponent<KBatchedAnimController>();

                DebugConsole.Log(
                    $"[ToolTarget][{side}][SetTargetPos] " +
                    $"worker={worker.GetProperName()}:{worker.GetNetId()} " +
                    $"workable={workable.GetProperName()}:{workable.GetNetId()} " +
                    $"given={target_pos} " +
                    $"stored={__instance.GetTargetPos()} " +
                    $"anim={anim?.currentAnim}");
            }
        }

        [HarmonyPatch(
            typeof(AnimEventHandler),
            nameof(AnimEventHandler.UpdateWorkTarget))]
        public class UpdateWorkTarget_Patch
        {
            public static void Prefix(
                AnimEventHandler __instance,
                Vector3 pos)
            {
                if (!TryGetWorker(
                    __instance,
                    out var worker,
                    out var workable))
                {
                    return;
                }

                string side = MultiplayerSession.IsHost
                    ? "HOST"
                    : "CLIENT";

                var anim = worker.GetComponent<KBatchedAnimController>();

                DebugConsole.Log(
                    $"[ToolTarget][{side}][UpdateWorkTarget] " +
                    $"worker={worker.GetProperName()}:{worker.GetNetId()} " +
                    $"workable={workable.GetProperName()}:{workable.GetNetId()} " +
                    $"target={pos} " +
                    $"stored={__instance.GetTargetPos()} " +
                    $"anim={anim?.currentAnim}");
            }
        }
    }
}
