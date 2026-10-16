using HarmonyLib;
using ONI_United.DebugTools;
using ONI_United.Networking.Session;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Packets.DuplicantActions;
using Shared.Profiling;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Patches.Duplicant
{
	// Sync personal priorities (the 0-9 matrix)
	[HarmonyPatch(typeof(ChoreConsumer), "SetPersonalPriority")]
	public static class ChoreConsumerPatch
	{
		public static void Postfix(ChoreConsumer __instance, ChoreGroup group, int value)
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.InActiveSession) return;

			// Check if we are currently applying a packet to avoid loops
			if (DuplicantPriorityPacket.IsApplying) return;

			var identity = __instance.GetComponent<NetworkIdentity>();
			if (identity != null)
			{
				var packet = new DuplicantPriorityPacket
				{
					NetId = identity.NetId,
					ChoreGroupId = group.Id,
					Priority = value
				};

				if (MultiplayerSession.IsHost)
				{
					PacketSender.SendToAllClients(packet);
				}
				else
				{
					PacketSender.SendToHost(packet);
				}

				DebugConsole.Log($"[ChoreConsumerPatch] Sent priority update for {identity.name}: {group.Id} = {value}");
			}
		}
	}
}
