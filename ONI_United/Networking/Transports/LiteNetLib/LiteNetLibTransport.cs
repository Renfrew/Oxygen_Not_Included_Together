using System;
using LiteNetLib;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Packets.Core;
using Shared.Interfaces.Networking;

namespace ONI_United.Networking.Transports.LiteNetLib
{
    // LAN byte sending through a NetPeer owned by the server or client transport. Received data
    // arrives through NetworkReceiveEvent on the owner's listener, inside the owner's PollEvents;
    // the owner reads it with NetPacketReader.GetRemainingBytes and identifies the sender by that peer.
    public sealed class LiteNetLibTransport : TransportBase
    {
        // LiteNetLib limits one unreliable message to about 1023 bytes; larger payloads travel as
        // ChunkedPacket fragments, which the receiving PacketHandler reassembles.
        private const int MaxPayloadBytes = 1000;
        private const int ChunkHeaderBytes = 20;

        public override bool Send(object connection, byte[] data, PacketSendMode mode)
        {
            var peer = (NetPeer)connection;
            if (peer.ConnectionState != ConnectionState.Connected)
                return false;

            var method = (mode & PacketSendMode.Reliable) != 0 && (mode & PacketSendMode.NoDelay) == 0
                ? DeliveryMethod.ReliableOrdered
                : DeliveryMethod.Unreliable;
            if (data.Length <= MaxPayloadBytes)
            {
                peer.Send(data, method);
                return true;
            }

            int chunkDataSize = MaxPayloadBytes - ChunkHeaderBytes;
            int totalChunks = (data.Length + chunkDataSize - 1) / chunkDataSize;
            int sequenceId = ChunkedPacket.GetNextSequenceId();
            for (int i = 0; i < totalChunks; i++)
            {
                int offset = i * chunkDataSize;
                int length = Math.Min(chunkDataSize, data.Length - offset);
                byte[] chunkData = new byte[length];
                Array.Copy(data, offset, chunkData, 0, length);
                var chunk = new ChunkedPacket
                {
                    SequenceId = sequenceId,
                    ChunkIndex = i,
                    TotalChunks = totalChunks,
                    ChunkData = chunkData
                };
                peer.Send(PacketSerialization.Serialize(chunk), method);
            }
            return true;
        }

        public override bool OnReceive(object source)
        {
            var peer = (NetPeer)source;
            if (peer.ConnectionState != ConnectionState.Connected)
                return false;

            // LiteNetLib delivers data through the NetPeer's OnNetworkReceive callback,
            // which should be handled by the owner's listener. This method can be used
            // to process any additional logic if needed.
            return true;
        }
    }
}
