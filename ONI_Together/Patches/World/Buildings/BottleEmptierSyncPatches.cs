using HarmonyLib;
using ONI_Together.Networking;
using ONI_United.Networking.Replication.Components;
using ONI_United.Networking.Replication.Components.StateMachines;
using Shared.Profiling;

namespace ONI_United.Patches.World.Buildings
{
    [HarmonyPatch(typeof(BottleEmptier), nameof(BottleEmptier.OnSpawn))]
    public static class BottleEmptierSpawnPatch
    {
        public static void Postfix(BottleEmptier __instance)
        {
            using var _ = Profiler.Scope();

            if (__instance.IsNullOrDestroyed())
                return;

            __instance.gameObject.AddOrGet<BottleEmptierStateSyncer>();
        }
    }
}
