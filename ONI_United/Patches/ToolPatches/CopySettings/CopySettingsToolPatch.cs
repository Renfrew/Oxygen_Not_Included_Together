using HarmonyLib;
using ONI_United.Networking.Session;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Packets.Tools.CopySettingsTool;
using Shared.Profiling;
using UnityEngine;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Patches.ToolPatches.CopySettings;

[HarmonyPatch(typeof(CopySettingsTool), nameof(CopySettingsTool.OnDragTool))]
public class CopySettingsToolPatch
{
    private static void Postfix(int cell, CopySettingsTool __instance)
    {
        using var _ = Profiler.Scope();

        if (!MultiplayerSession.InActiveSession)
            return;

        GameObject src = __instance.sourceGameObject;
        if (src == null)
            return;

        NetworkIdentity identity = src.GetComponent<NetworkIdentity>();
        if (identity == null)
            return;

        PacketSender.SendToAllOtherPeers(new CopySettingsToolPacket(identity.NetId, cell));
    }
}