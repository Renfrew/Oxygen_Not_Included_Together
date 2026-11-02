// INACTIVE MIGRATION DRAFT: never constructed or selected by production.
// Lifecycle authorizes Connect, Disconnect and Poll before they reach this protocol.
using System;
using System.IO;
using LiteNetLib;
using LiteNetLib.Utils;
using ONI_United.Core;
using ONI_United.Misc;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Sessions;

namespace ONI_United.Networking.Transport.LiteNetLib
{
    internal sealed class LiteNetLibClientDraft : ClientTransport
    {
        private NetManager client;
        private EventBasedNetListener listener;
        private bool connected;

        protected override void InternalConnect()
        {
            if (client != null)
                throw new InvalidOperationException("Connect was invoked while a LAN client manager is still owned.");

            listener = new EventBasedNetListener();
            listener.PeerConnectedEvent += OnPeerConnected;
            listener.PeerDisconnectedEvent += OnPeerDisconnected;
            listener.NetworkReceiveEvent += OnNetworkReceive;
            client = new NetManager(listener)
            {
                AutoRecycle = true,
                UnsyncedEvents = false,
                ChannelsCount = 4,
                DisconnectTimeout = Configuration.Instance.Client.TimeoutSeconds * 1000
            };
            if (!client.Start())
            {
                Cleanup();
                OnConnectFail("LAN client socket startup failed.");
                return;
            }

            var request = new NetDataWriter();
            request.Put("ONI_TOGETHER");
            request.Put(NetworkRuntime.GetLocalID());
            // An unresolvable address is reported through PeerDisconnectedEvent (UnknownHost).
            client.Connect(MultiplayerSession.ServerIp, MultiplayerSession.ServerPort, request);
        }

        protected override void InternalDisconnect()
        {
            Cleanup();
            OnDisconnected();
        }

        protected override void InternalPoll()
        {
            if (client == null)
                throw new InvalidOperationException("Poll was invoked without an owned LAN client manager.");

            client.PollEvents();
        }

        private void OnPeerConnected(NetPeer peer)
        {
            var hello = SessionHandshakePacket.CreateClientHello(NetworkRuntime.GetLocalID(), Utils.GetLocalPlayerName());
            peer.Send(PacketSerialization.Serialize(hello), DeliveryMethod.ReliableOrdered);
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
        {
            // Gameplay dispatch awaits integration: PacketHandler authorizes against the legacy
            // NetworkRuntime.TransportClient, which cannot own this draft.
            if (connected)
                return;

            var welcome = new SessionHandshakePacket();
            try
            {
                using var binary = new BinaryReader(new MemoryStream(reader.GetRemainingBytes()));
                if (binary.ReadInt32() == PacketRegistry.GetPacketId(welcome))
                    welcome.Deserialize(binary);
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException)
            {
                // Malformed host data is a failed connection, not a local invariant violation.
            }

            if (welcome.Message != SessionHandshakeMessage.Welcome
                || welcome.HostPlayerId == 0 || !welcome.HasValidMetadata)
            {
                Cleanup();
                OnConnectFail("The host did not send a valid session welcome.");
                return;
            }

            connected = true;
            OnConnected(welcome.HostPlayerId, welcome.PlayerName);
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            bool wasConnected = connected;
            Cleanup();
            if (wasConnected) OnDisconnect();
            else OnConnectFail("LAN connection failed: " + info.Reason + ".");
        }

        private void Cleanup()
        {
            if (client == null)
                return;

            // Stop closes the socket, joins the logic thread and discards queued events.
            client.Stop();
            // PollEvents processes its detached batch even after Stop; stop later events reaching us.
            listener.PeerConnectedEvent -= OnPeerConnected;
            listener.PeerDisconnectedEvent -= OnPeerDisconnected;
            listener.NetworkReceiveEvent -= OnNetworkReceive;
            client = null;
            listener = null;
            connected = false;
        }
    }
}
