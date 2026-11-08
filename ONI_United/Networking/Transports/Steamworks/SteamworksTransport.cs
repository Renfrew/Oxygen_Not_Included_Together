using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Shared.Interfaces.Networking;
using Steamworks;

namespace ONI_United.Networking.Transports.Steamworks
{
    public sealed class SteamworksTransport : TransportBase
    {
        private const int maxMessages = 128;
        public override bool Send(object connection, byte[] data, PacketSendMode mode)
        {
            var handle = (HSteamNetConnection)connection;
            IntPtr buffer = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, buffer, data.Length);
                return SteamNetworkingSockets.SendMessageToConnection(
                    handle, buffer, (uint)data.Length, ToSendFlags(mode), out _) == EResult.k_EResultOK;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public override bool OnReceive(object source)
        {

            var messages = new IntPtr[maxMessages];
            int msgCount = -1;
            
            if (source is HSteamNetPollGroup pollGroup)
                msgCount = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(pollGroup, messages, maxMessages);
            else if (source is HSteamNetConnection connection)
                msgCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, messages, maxMessages);

            if (msgCount < 0)
                throw new InvalidOperationException("Steam rejected the poll group handle.");

            var received = new List<(HSteamNetConnection, byte[])>(msgCount);
            for (int i = 0; i < msgCount; i++)
            {
                var message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messages[i]);
                byte[] data = new byte[message.m_cbSize];
                Marshal.Copy(message.m_pData, data, 0, message.m_cbSize);
                SteamNetworkingMessage_t.Release(messages[i]);
                received.Add((message.m_conn, data));
            }

            return received.Count >= 0;
        }

        private static int ToSendFlags(PacketSendMode mode)
        {
            int flags = 0;
            if ((mode & PacketSendMode.Reliable) == PacketSendMode.Reliable)
                flags |= 8; // k_nSteamNetworkingSend_Reliable
            if ((mode & PacketSendMode.Immediate) == PacketSendMode.Immediate)
                flags |= 1; // k_nSteamNetworkingSend_NoNagle
            if ((mode & PacketSendMode.NoDelay) == PacketSendMode.NoDelay)
                flags |= 4; // k_nSteamNetworkingSend_NoDelay
            return flags;
        }
    }
}
