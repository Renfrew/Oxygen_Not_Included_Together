using HarmonyLib;
using ONI_Together.Networking;
using ONI_Together.Networking.OxySync.Components.Entities;
using Shared.Profiling;
using UnityEngine;

namespace ONI_Together.Patches.DuplicantActions
{
	internal class StandardWorker_Patches
	{
        [HarmonyPatch(typeof(StandardWorker), nameof(StandardWorker.OnPrefabInit))]
        public class StandardWorker_OnPrefabInit_Patch
        {
            public static void Postfix(StandardWorker __instance)
            {
                if (__instance?.GetComponent<KPrefabID>()?.HasTag(GameTags.BaseMinion) == true)
                    __instance.gameObject.AddOrGet<StandardWorkerSyncer>();
            }
        }

		[HarmonyPatch(typeof(StandardWorker), nameof(StandardWorker.StartWork))]
		public class StandardWorker_StartWork_Patch
		{
            public static bool Prefix(StandardWorker __instance, WorkerBase.StartWorkInfo start_work_info)
            {
                using var _ = Profiler.Scope();

                if (!__instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    return true;
                
                if (!syncer.CanSync(start_work_info, out var workableId))
                    return true;
                
                if (MultiplayerSession.IsHostInSession && MultiplayerSession.SessionHasPlayers)
                {
                    syncer.BeginWorkerPlayback(workableId);
                    syncer.RequestStartWork(start_work_info);
                    return true;
                }

                if (MultiplayerSession.IsClient)
                {
                    // Block client from starting work if not in the correct work scope (authorized by the host)
                    if (!syncer.IsInWorkScope) return false;

                    syncer.BeginWorkerPlayback(workableId);
                }

                return true;
            }
		}

        [HarmonyPatch(typeof(StandardWorker), nameof(StandardWorker.StartPlayingPostAnim))]
        public class StandardWorker_StartPlayingPostAnim_Patch
        {
            public static bool Prefix(StandardWorker __instance)
            {
                using var _ = Profiler.Scope();

                if (!__instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    return true;
                
                if (!syncer.TryGetSyncedWork(out var _))
                    return true;
                
                if (MultiplayerSession.IsHostInSession && MultiplayerSession.SessionHasPlayers)
                {
                    syncer.RequestBeginComplete();
                    return true;
                }

                return !MultiplayerSession.IsClient || syncer.IsInWorkScope;
            }
        }

        [HarmonyPatch(typeof(StandardWorker), nameof(StandardWorker.CompleteWork))]
        public class StandardWorker_CompleteWork_Patch
        {
            public static bool Prefix(StandardWorker __instance, out int __state)
            {
                using var _ = Profiler.Scope();

                __state = 0;

                if (!__instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    return true;
                
                if (!syncer.TryGetSyncedWork(out var workableId))
                    return true;
                
                if (MultiplayerSession.IsHostInSession && MultiplayerSession.SessionHasPlayers)
                {
                    __state = workableId;
                    syncer.RequestCompleteWork();
                    return true;
                }

                if (MultiplayerSession.IsClient)
                {
                    if (!syncer.IsInWorkScope) return false;

                    __state = workableId;
                }

                return true;
            }

            public static void Postfix(StandardWorker __instance, int __state)
            {
                using var _ = Profiler.Scope();

                if (__state != 0 && __instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
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

                if (!__instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    return true;
                
                if (!syncer.TryGetSyncedWork(out var workableId))
                    return true;
                
                if (MultiplayerSession.IsHostInSession && MultiplayerSession.SessionHasPlayers)
                {
                    WorkerBase.State state = __instance.GetState();

                    bool IsCompleting = state == WorkerBase.State.Completing
                        || state == WorkerBase.State.PendingCompletion;

                    bool isAbort = state == WorkerBase.State.Working
                        || (!__instance.successFullyCompleted && IsCompleting);

                    // Completed work would also emit a stop work event,
                    // so we need to handle aborts separately to avoid duplicated stop work overwrites the animation.
                    if (isAbort)
                    {
                        __state = workableId;
                        syncer.RequestAbortWork();
                    }

                    return true;
                }

                if (MultiplayerSession.IsClient)
                {
                    if (!syncer.IsInWorkScope) return false;

                    __state = workableId;
                }

                return true;
			}

			public static void Postfix(StandardWorker __instance, int __state)
			{
				using var _ = Profiler.Scope();
                
                if (__state != 0 && __instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    syncer.EndWorkerPlayback(__state);
			}
		}

        [HarmonyPatch(typeof(AnimEventHandler), nameof(AnimEventHandler.UpdateWorkTarget))]
        public class UpdateWorkTarget_Patch
        {
            public static void Prefix(AnimEventHandler __instance, Vector3 pos)
            {
                if (!MultiplayerSession.IsHostInSession || !MultiplayerSession.SessionHasPlayers)
                    return;
                
                if (!__instance.TryGetComponent<StandardWorkerSyncer>(out var syncer))
                    return;

                Workable workable = syncer.Worker.GetWorkable();
                if (workable == null || workable.IsNullOrDestroyed())
                    return;

                // This workable cannot be synced by the workable syncer,
                // Fallback to sync the work target only to fix the animation.
                if (!syncer.CanSync(workable, out var _))
                    syncer.RequestUpdateWorkTarget(pos);
            }
        }
    }
}
