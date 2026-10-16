using HarmonyLib;
using ONI_United.Networking.Session;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Packets.World;
using ONI_United.Scripts.Creatures;
using Shared.Profiling;
using UnityEngine;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Patches.Critters
{
	internal class RoverPatches
	{
		[HarmonyPatch(typeof(BaseRoverConfig), nameof(BaseRoverConfig.BaseRover))]
		public static class BaseRoverConfig_BaseRover_Patch
		{
			public static void Postfix(GameObject __result)
			{
				using var _ = Profiler.Scope();

				if (__result == null)
					return;

				__result.AddOrGet<NetworkIdentity>();
				__result.AddOrGet<CreatureMultiplayerInitializer>();
			}
		}

		[HarmonyPatch(typeof(BaseRoverConfig), nameof(BaseRoverConfig.OnSpawn))]
		public static class BaseRoverConfig_OnSpawn_Patch
		{
			public static void Postfix(GameObject inst)
			{
				using var _ = Profiler.Scope();

				if (!MultiplayerSession.IsHostInSession || inst == null)
					return;

				var identity = inst.AddOrGet<NetworkIdentity>();
				identity.RegisterIdentity();
				if (identity.NetId == 0)
					return;

				PacketSender.SendToAllClients(new SpawnPrefabPacket(
					identity.NetId,
					inst.PrefabID().GetHashCode(),
					inst.transform.position));
			}
		}

		[HarmonyPatch(typeof(MorbRoverMaker.Instance), nameof(MorbRoverMaker.Instance.SpawnRover))]
		public static class MorbRoverMaker_SpawnRover_Patch
		{
			public static bool Prefix()
			{
				using var _ = Profiler.Scope();

				// The host sends runtime rover creation through BaseRoverConfig_OnSpawn_Patch.
				return !MultiplayerSession.IsClient;
			}
		}
	}
}
