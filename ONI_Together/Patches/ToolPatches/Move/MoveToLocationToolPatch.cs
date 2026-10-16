using HarmonyLib;
using ONI_United.DebugTools;
using ONI_United.Networking.Session;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Replication.Components.Tools;
using Shared.Profiling;

namespace ONI_United.Patches.ToolPatches.Move
{
	[HarmonyPatch(typeof(MoveToLocationTool), nameof(MoveToLocationTool.SetMoveToLocation))]
	public static class MoveToLocationToolPatch
	{
		public static bool Prefix(MoveToLocationTool __instance, out int __state, int target_cell)
		{
			using var _ = Profiler.Scope();
			__state = 0;

			if (__instance == null || __instance.gameObject == null || __instance.gameObject.IsNullOrDestroyed())
			{
				DebugConsole.LogWarning("[MoveToLocationToolPatch] MoveToLocationTool instance is null or destroyed.");
				return true;
			}

			if (!MoveToLocationToolSyncer.GetTargetNetIdFromMoveToLocationTool(__instance, out int target_NetId) || target_NetId == 0)
			{
				DebugConsole.LogWarning("[MoveToLocationToolPatch] Cannot find a valid target NetId.");
				return true; // Allow normal execution to handle the error
			}

			__state = target_NetId; // Store the target NetId for use in Postfix

			// When the request is not from the host, we need to send a packet to the host to authorize the move.
			if (MultiplayerSession.IsClient && !MoveToLocationToolSyncer.IsAuthorized(target_NetId, target_cell))
			{
				DebugConsole.Log($"[MoveToLocationToolPatch] Sent MoveToLocation Request to host for NetId {target_NetId} to move to {target_cell}");
				MoveToLocationToolSyncer.RequestCallMoveToLocation(target_NetId, target_cell);

				return false;
			}

			if (MultiplayerSession.IsHostInSession)
			{
				MoveToLocationToolSyncer.RequestCallMoveToLocation(target_NetId, target_cell);
			}

			return true; // Allow normal execution to proceed
		}

		public static void Postfix(MoveToLocationTool __instance, int __state, int target_cell)
		{
			using var _ = Profiler.Scope();

			if (__instance == null || __instance.gameObject == null || __instance.gameObject.IsNullOrDestroyed())
			{
				DebugConsole.LogWarning("[MoveToLocationToolPatch] MoveToLocationTool instance is null or destroyed.");
				return;
			}

			if (MultiplayerSession.IsClient && MoveToLocationToolSyncer.IsAuthorized(__state, target_cell))
			{
				MoveToLocationToolSyncer.UnAuthorize(__state, target_cell);
			}
		}
	}
}
