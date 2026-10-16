using HarmonyLib;
using ONI_United.DebugTools;
using ONI_United.Networking.Session;
using ONI_United.Networking.Packets.Tools.Dig;
using Shared.Profiling;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Patches.ToolPatches.Dig
{
    [HarmonyPatch(typeof(DigTool), nameof(DigTool.PlaceDig))]
    public static class DigTool_PlaceDig_Patch
    {
        public static void Postfix(int cell, int animationDelay)
        {
            using var _ = Profiler.Scope();

            if (!MultiplayerSession.InActiveSession)
            {
                DebugConsole.LogWarning("[PlaceDig Patch] Skipped: MultiplayerSession.InActiveSession is false");
                return;
            }

            if (DiggablePacket.ProcessingIncoming)
                return;

            PacketSender.SendToAllOtherPeers(new DiggablePacket(cell, animationDelay));
        }
    }
}