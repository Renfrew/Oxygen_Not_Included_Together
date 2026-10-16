using HarmonyLib;
using ONI_United.Networking.Session;
using ONI_United.Networking.Packets.Tools.Cancel;
using ONI_United.Networking.Packets.Tools.Deconstruct;
using Shared.Profiling;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Patches.ToolPatches.Deconstruct
{
	[HarmonyPatch(typeof(DeconstructTool), nameof(DeconstructTool.OnDragTool))]
	public static class DeconstructToolPatch
	{
		public static void Postfix(int cell, int distFromOrigin)
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.InActiveSession)
				return;

			//prevent recursion
			if (DeconstructPacket.ProcessingIncoming)
				return;
			PacketSender.SendToAllOtherPeers(new DeconstructPacket() { cell = cell, distFromOrigin = distFromOrigin });
		}
	}
}
