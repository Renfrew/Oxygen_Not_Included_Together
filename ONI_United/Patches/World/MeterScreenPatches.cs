using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using ONI_United.Networking.Packets.World;
using ONI_United.Networking.Session;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Patches.World
{
    public class MeterScreenPatches
    {
        [HarmonyPatch(typeof(MeterScreen), nameof(MeterScreen.OnRedAlertClick))]
        public static class MeterScreen_RedAlertPatch
        {
            public static bool IsSyncing = false;

            [HarmonyPostfix]
            public static void Postfix()
            {
                if (IsSyncing) return;
                if (!MultiplayerSession.InActiveSession) return;

                bool state = ClusterManager.Instance.activeWorld.AlertManager.IsRedAlertToggledOn();
                var packet = new RedAlertStatePacket { 
                    ActiveWorldID = ClusterManager.Instance.activeWorldId,
                    IsRedAlert = state
                };
                PacketSender.SendToAllOtherPeers(packet);
            }
        }
    }
}
