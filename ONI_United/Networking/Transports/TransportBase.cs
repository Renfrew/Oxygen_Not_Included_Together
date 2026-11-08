using ONI_United.Networking.Packets.Architecture;
using Shared.Interfaces.Networking;

namespace ONI_United.Networking.Transports
{
    public abstract class TransportBase
    {
        // connection is the protocol's native connection handle; mode is translated to native delivery.
        public abstract bool Send(object connection, byte[] data, PacketSendMode mode);

        public abstract bool OnReceive(object source);

        // The common inbound boundary. PacketHandler authorizes the sender against the physical
        // connection it arrived on, so the connection is passed through unchanged.
        public void Receive(ulong senderId, object connection, byte[] data)
        {
            PacketHandler.HandleIncoming(data, senderId, connection);
        }
    }
}
