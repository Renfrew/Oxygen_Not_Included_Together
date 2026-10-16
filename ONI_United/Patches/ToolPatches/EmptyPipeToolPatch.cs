using HarmonyLib;
using ONI_United.Networking.Session;
using ONI_United.Networking.Packets.Tools;
using ONI_United.Networking.Packets.Tools.Deconstruct;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared.Profiling;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Patches.ToolPatches
{
	internal class EmptyPipeToolPatch
	{
		[HarmonyPatch(typeof(EmptyPipeTool), nameof(EmptyPipeTool.OnDragTool))]
		public class EmptyPipeTool_OnDragTool_Patch
		{
			public static void Postfix(int cell, int distFromOrigin)
			{
				using var _ = Profiler.Scope();

				if (!MultiplayerSession.InActiveSession)
					return;

				//prevent recursion
				if (EmptyPipePacket.ProcessingIncoming)
					return;
				PacketSender.SendToAllOtherPeers(new EmptyPipePacket() { cell = cell, distFromOrigin = distFromOrigin });
			}
		}
	}
}
