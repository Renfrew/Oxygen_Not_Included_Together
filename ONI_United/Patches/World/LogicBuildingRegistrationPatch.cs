using HarmonyLib;
using ONI_United.DebugTools;
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
using ONI_United.Networking.Synchronization;
using Shared.Profiling;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ONI_United.Patches.World
{
    // Registers logic/automation buildings with LogicStateSyncer on spawn
    // and ensures they have a NetworkIdentity for packet routing.
    [HarmonyPatch(typeof(Building), nameof(Building.OnSpawn))]
    public static class LogicBuildingSpawnRegistrationPatch
    {
        private static readonly List<Type> LogicComponentTypes = new()
        {
            typeof(Switch),
            typeof(LogicGate),
            typeof(LogicMemory),
            typeof(LogicRibbonReader),
            typeof(LogicRibbonWriter),
            typeof(Automatable),
        };

        public static void Postfix(Building __instance)
        {
            using var _ = Profiler.Scope();

            try
            {
                if (__instance is not BuildingComplete)
                    return;

                var go = __instance.gameObject;
                if (!HasLogicComponent(go))
                    return;

                go.AddOrGet<NetworkIdentity>().RegisterIdentity();
                LogicStateSyncer.Instance?.Register(go);

                DebugConsole.Log($"[LogicBuildingRegistration] Registered {go.name} with LogicStateSyncer");
            }
            catch (Exception ex)
            {
                DebugConsole.LogError($"[LogicBuildingRegistration] {ex}");
            }
        }

        private static bool HasLogicComponent(GameObject go)
        {
            foreach (var type in LogicComponentTypes)
            {
                if (go.GetComponent(type) != null)
                    return true;
            }
            return false;
        }
    }

    // Unregister from LogicStateSyncer when buildings are destroyed
    [HarmonyPatch(typeof(Building), nameof(Building.OnCleanUp))]
    public static class LogicBuildingCleanupRegistrationPatch
    {
        public static void Postfix(Building __instance)
        {
            using var _ = Profiler.Scope();

            try
            {
                LogicStateSyncer.Instance?.Unregister(__instance.gameObject);
            }
            catch (Exception ex)
            {
                DebugConsole.LogError($"[LogicBuildingCleanup] {ex}");
            }
        }
    }
}
