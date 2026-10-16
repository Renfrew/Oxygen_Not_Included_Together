using HarmonyLib;
using ONI_United.Networking;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Session;
using ONI_United.Networking.Session.Lobbies;
using ONI_United.Networking.Synchronization.Trackers;
using ONI_United.Networking.Transfer;
using ONI_United.Networking.WorldSync;
using Shared.Interfaces.Networking;
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
