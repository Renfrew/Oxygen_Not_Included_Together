using HarmonyLib;
using Shared.Replication;

namespace ONI_United.Patches.StateMachines
{
    [HarmonyPatch(typeof(StateMachine.Instance), nameof(StateMachine.Instance.GoTo), typeof(string))]
    public static class StateMachineGoToFreeze_Patch
    {
        public static bool Prefix(StateMachine.Instance __instance, string state_name)
        {
            return !FrozenStateMachineTracker.IsFrozen(__instance);
        }
    }
}
