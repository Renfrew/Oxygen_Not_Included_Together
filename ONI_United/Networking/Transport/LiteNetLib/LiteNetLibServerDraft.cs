// INACTIVE MIGRATION DRAFT: never constructed or selected by production.
// Poll runs native events on the game thread; gameplay dispatch remains deferred.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using LiteNetLib;
using ONI_United.DebugTools;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Sessions;

namespace ONI_United.Networking.Transport.LiteNetLib
{
    internal sealed class LiteNetLibServerDraft : ServerTransport
    {
        private NetManager server;
        private EventBasedNetListener listener;
        // Request identity belongs to the physical peer, including before admission.
        private readonly Dictionary<NetPeer, ulong> physicalPeers = new(new PeerReferenceComparer());

        // NetPeer inherits endpoint equality; physical ownership follows the peer instance.
        private sealed class PeerReferenceComparer : IEqualityComparer<NetPeer>
        {
            public bool Equals(NetPeer x, NetPeer y) => ReferenceEquals(x, y);
            public int GetHashCode(NetPeer peer) => RuntimeHelpers.GetHashCode(peer);
        }

        protected override void InternalStart()
        {
            int port = Configuration.Instance.Host.LanPort;
            if (port < 1 || port > 65535)
            {
                OnStartFailed("The configured LAN port is outside the valid range.");
                return;
            }

            try
            {
                listener = new EventBasedNetListener();
                listener.ConnectionRequestEvent += OnConnectionRequest;
                listener.PeerDisconnectedEvent += OnPeerDisconnected;
                listener.NetworkReceiveEvent += OnNetworkReceive;
                server = new NetManager(listener)
                {
                    AutoRecycle = true,
                    UnsyncedEvents = false,
                    ChannelsCount = 4,
                    DisconnectTimeout = checked(Configuration.Instance.Host.TimeoutSeconds * 1000)
                };
                if (!server.Start(port))
                    throw new InvalidOperationException("Failed to bind the LAN listener to port " + port + ".");
            }
            catch (Exception ex)
            {
                OnStartFailed(ex.Message);
                return;
            }

            OnStarted();
        }

        protected override void InternalStop()
        {
            try
            {
                foreach (var pair in physicalPeers)
                    OnDisconnect(pair.Value, pair.Key);

                // Stop synchronously closes sockets, joins workers and clears native peers/events.
                // Do not wait for disconnect callbacks: Stop discards pending events.
                server?.Stop();
                physicalPeers.Clear();
                if (listener != null)
                {
                    listener.ConnectionRequestEvent -= OnConnectionRequest;
                    listener.PeerDisconnectedEvent -= OnPeerDisconnected;
                    listener.NetworkReceiveEvent -= OnNetworkReceive;
                }
                server = null;
                listener = null;
            }
            catch (Exception ex)
            {
                OnStopFailed(ex.Message);
                return;
            }
            OnStopped();
        }

        protected override void InternalPoll()
        {
            if (server != null && server.IsRunning)
                server.PollEvents();
        }

        private void OnConnectionRequest(ConnectionRequest request)
        {
            if (server == null || !server.IsRunning
                || physicalPeers.Count >= Configuration.Instance.Host.MaxLobbySize
                || !request.Data.TryGetString(out string magic) || magic != "ONI_TOGETHER"
                || !request.Data.TryGetULong(out ulong clientId) || clientId == 0
                || request.Data.AvailableBytes != 0)
            {
                request.Reject();
                return;
            }

            // Accept queues PeerConnected with UnsyncedEvents=false. Own the returned
            // native peer now, before any subsequent hello or disconnection event.
            var peer = request.Accept();
            if (peer != null)
                physicalPeers.TryAdd(peer, clientId);
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            if (!physicalPeers.TryGetValue(peer, out ulong clientId)) return;
            OnDisconnect(clientId, peer);
            physicalPeers.Remove(peer);
        }

        private void RejectPeer(NetPeer peer)
        {
            if (!physicalPeers.TryGetValue(peer, out ulong clientId)) return;
            OnDisconnect(clientId, peer);
            // Keep physical ownership until the native disconnection event or Stop.
            peer.Disconnect();
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod mode)
        {
            if (!physicalPeers.TryGetValue(peer, out ulong clientId)
                || peer.ConnectionState != ConnectionState.Connected)
                return;
            if (TryGetConnection(clientId, out var admitted) && ReferenceEquals(admitted, peer))
                return; // No gameplay receiving/dispatch in this increment.

            try
            {
                if (reader.AvailableBytes < sizeof(int) || reader.AvailableBytes > 2048)
                    throw new InvalidDataException("Invalid LAN hello size.");

                // TODO redesign
                using var stream = new MemoryStream(reader.GetRemainingBytes());
                using var binary = new BinaryReader(stream);
                var hello = new SessionHandshakePacket();
                if (binary.ReadInt32() != PacketRegistry.GetPacketId(hello))
                    throw new InvalidDataException("Expected a LAN session hello.");
                // Never interpret another packet type as identity metadata.
                hello.Deserialize(binary);
                if (hello.Message != SessionHandshakeMessage.ClientHello
                    || hello.PlayerId != clientId || !hello.HasValidMetadata
                    || hello.HostPlayerId != 0 || hello.RejectionReason.Length != 0
                    || (!Configuration.Instance.Network.BypassProtocolCompatibilityChecks
                        && !ProtocolCompatibility.Matches(hello.ProtocolVersion,
                            hello.PacketRegistryFingerprint, hello.ModVersion)))
                    throw new InvalidDataException("Invalid LAN session hello.");

                if (!OnConnected(clientId, hello.PlayerName, peer))
                {
                    RejectPeer(peer);
                    return;
                }

                // The existing client requires Welcome to establish its logical Session.
                // This is a handshake response only, using the unchanged packet serializer.
                var welcome = SessionHandshakePacket.CreateWelcome(clientId, Session.HostPlayerId,
                    Session.Players[Session.HostPlayerId].UserName);
                peer.Send(PacketSerialization.Serialize(welcome), DeliveryMethod.ReliableOrdered);
            }
            catch (Exception ex)
            {
                DebugConsole.LogWarning("[LiteNetLibServerDraft] Rejected LAN hello: " + ex.Message);
                RejectPeer(peer);
            }
        }
    }
}
