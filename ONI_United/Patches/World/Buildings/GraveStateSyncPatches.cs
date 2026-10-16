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
using ONI_United.Networking.Replication.Components.StateMachines;
using Shared.Profiling;

namespace ONI_United.Patches.World.Buildings
{

    [HarmonyPatch(typeof(Grave), nameof(Grave.OnSpawn))]
    public static class GraveSpawnPatch
    {
        public static void Postfix(Grave __instance)
        {
            using var _ = Profiler.Scope();

            if (__instance.IsNullOrDestroyed())
                return;

            // The grave doesn't have its own network identity, so we need to register one ourselves.
            __instance.gameObject.AddOrGet<NetworkIdentity>().RegisterIdentity();
            __instance.gameObject.AddOrGet<GraveStateSyncer>();
        }
    }
}
