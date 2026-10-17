using HarmonyLib;
using ONI_United;
using ONI_United.Networking.Session;
using ONI_United.Networking.Packets.World;
using Shared.Profiling;

namespace ONI_United.Patches.World.SideScreen
{
	/// <summary>
	/// Patches for door state synchronization
	/// </summary>

	/// <summary>
	/// Sync door state changes (Open/Close/Auto)
	/// </summary>
	///

	[HarmonyPatch(typeof(Door), nameof(Door.QueueStateChange), typeof(Door.ControlState))]
	public static class Door_QueueStateChange_Patch
	{
		public static void Postfix(Door __instance, Door.ControlState nextState)
		{
			using var _ = Profiler.Scope();

			if (BuildingConfigPacket.IsApplyingPacket) return;
			SideScreenSyncHelper.SyncDoorState(__instance.gameObject, nextState);
		}
	}

	/// <summary>
	/// Prevents the door melt check from destroying doors on clients.
	/// In singleplayer, Door.Sim200ms checks if the door's cells are still solid
	/// and destroys the door if they're not (e.g., melted by extreme heat).
	/// On clients, the sim cell state is synced from the host and may arrive before
	/// the door chore completes (open/close), causing false-positive melt destruction.
	/// This patch forces do_melt_check to false on clients, preventing the issue.
	/// </summary>
	[HarmonyPatch(typeof(Door), nameof(Door.Sim200ms), typeof(float))]
	public static class Door_Sim200ms_Patch
	{
		public static void Prefix(Door __instance)
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHost && MultiplayerSession.InActiveSession)
			{
				__instance.do_melt_check = false;
			}
		}
	}

	/// <summary>
	/// Prevents Door.OnCleanUp from firing on clients as a safety net.
	/// When a door is destroyed on a client (from any cause), this prevents
	/// the ReplaceAndDisplaceElement calls in OnCleanUp that spawn ore debris.
	/// The host is authoritative for sim state changes.
	/// </summary>
	[HarmonyPatch(typeof(Door), "OnCleanUp")]
	public static class Door_OnCleanUp_Patch
	{
		public static bool Prefix(object __instance)
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHost && MultiplayerSession.InActiveSession)
			{
				return false;
			}
			return true;
		}
	}
}
