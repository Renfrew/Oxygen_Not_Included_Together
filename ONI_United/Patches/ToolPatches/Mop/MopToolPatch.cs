using HarmonyLib;
using ONI_United.Networking.Session;
using ONI_United.Networking.Packets.Tools.Mop;
using Shared.Profiling;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Patches.ToolPatches.Mop
{
	[HarmonyPatch(typeof(MopTool), "OnDragTool")]
	public static class MoptoolPatch
	{
		public static void Prefix(int cell, int distFromOrigin)
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.InActiveSession)
				return;

			if (!Grid.IsValidCell(cell))
				return;

			if (MopToolPacket.ProcessingIncoming)
				return;

			PacketSender.SendToAllOtherPeers(new MopToolPacket { cell = cell, distFromOrigin = distFromOrigin });
		}
	}

}
