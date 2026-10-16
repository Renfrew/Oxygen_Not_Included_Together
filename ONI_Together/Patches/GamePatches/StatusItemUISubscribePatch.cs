using HarmonyLib;
using ONI_Together.DebugTools;
using ONI_Together.Networking;
using ONI_Together.Networking.Components;
using ONI_United.Networking.Packets.World;
using Shared.Profiling;
using UnityEngine;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Patches.GamePatches
{
    public static class StatusItemUISubscribePatch
    {
        private static int _subscribedNetId;

        [HarmonyPatch(typeof(DetailsScreen), nameof(DetailsScreen.Refresh))]
        public static class DetailsScreen_Refresh_Patch
        {
            public static void Postfix(GameObject go)
            {
                using var _ = Profiler.Scope();
                if (!MultiplayerSession.IsClient) return;

                if (go == null)
                {
                    UnsubscribeCurrent();
                    return;
                }

                if (!go.TryGetNetIdentity(out var identity))
                    return;

                if (identity.NetId == _subscribedNetId)
                    return;

                UnsubscribeCurrent();
                _subscribedNetId = identity.NetId;
                PacketSender.SendToHost(new StatusItemsSubscribePacket
                {
                    NetId = _subscribedNetId,
                    Subscribe = true
                });
            }

            private static void UnsubscribeCurrent()
            {
                if (_subscribedNetId == 0) return;
                PacketSender.SendToHost(new StatusItemsSubscribePacket
                {
                    NetId = _subscribedNetId,
                    Subscribe = false
                });
                _subscribedNetId = 0;
            }
        }
    }
}
