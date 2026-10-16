using HarmonyLib;
using ONI_Together.Networking;
using ONI_United.Networking.Packets.Tools.Harvest;
using Shared.Profiling;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Patches.ToolPatches.Harvest;

[HarmonyPatch(typeof(HarvestTool), nameof(HarvestTool.OnDragTool))]
public class HarvestToolPatch
{
    private static void Postfix(int cell, int distFromOrigin)
    {
        using var _ = Profiler.Scope();

        if (!MultiplayerSession.InActiveSession)
            return;

        if (HarvestToolPacket.ProcessingIncoming)
            return;

        PacketSender.SendToAllOtherPeers(new HarvestToolPacket { cell = cell, distFromOrigin = distFromOrigin });
    }
}