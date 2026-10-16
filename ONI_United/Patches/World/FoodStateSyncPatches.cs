using HarmonyLib;
using ONI_United.Networking.Session;
using ONI_United.Networking.Replication.Components.StateMachines;
using ONI_United.Networking.Replication.Components;
using Shared.Replication;
using UnityEngine;

namespace ONI_United.Patches.World
{
    [HarmonyPatch(typeof(Edible), nameof(Edible.OnPrefabInit))]
    public static class Edible_Syncer_Patch
    {
        public static void Postfix(Edible __instance)
        {
            if (!MultiplayerSession.InActiveSession)
                return;
            if (__instance.IsNullOrDestroyed())
                return;
            __instance.gameObject.AddOrGet<RottableStateSyncer>();
            __instance.gameObject.AddOrGet<EdibleConsumptionSyncer>();
        }
    }

    [HarmonyPatch(typeof(Edible), nameof(Edible.StopConsuming))]
    public static class Edible_StopConsuming_Patch
    {
        public static bool Prefix(Edible __instance, WorkerBase worker)
        {
            if (__instance.IsNullOrDestroyed())
                return false;

            if (__instance.foodInfo == null)
            {
                __instance.isBeingConsumed = false;
                if (__instance.Units < 0.001f)
                    __instance.gameObject.DeleteObject();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(StateMachineController), nameof(StateMachineController.OnTargetDestroyed))]
    public static class StateMachineController_Cleanup_Patch
    {
        public static void Prefix()
        {
            SafeStateMachine.ClearGlobalError();
        }

        public static void Postfix()
        {
            SafeStateMachine.ClearGlobalError();
        }
    }
}
