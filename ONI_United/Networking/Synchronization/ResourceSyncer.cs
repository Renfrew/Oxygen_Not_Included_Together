using HarmonyLib;
using System.Collections.Generic;
using Shared.Profiling;
using UnityEngine;
using ONI_United.Networking.Session;

namespace ONI_United.Networking.Synchronization
{
	public class ResourceSyncer : MonoBehaviour
	{
		public static Dictionary<string, float> ClientResources = new Dictionary<string, float>();
	}

	// Client-side patch: Override WorldInventory.GetAmount to return our synced values if we are Client
	[HarmonyPatch(typeof(WorldInventory), "GetAmount")]
	public static class WorldInventoryGetAmountPatch
	{
		public static bool Prefix(Tag tag, bool includeRelatedWorlds, ref float __result)
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.InActiveSession || MultiplayerSession.IsHost) return true;

			if (ResourceSyncer.ClientResources.TryGetValue(tag.Name, out float val))
			{
				__result = val;
				return false; // Skip original method
			}

			return true;
		}
	}
}
