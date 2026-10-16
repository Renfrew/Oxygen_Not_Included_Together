using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Packets.Architecture;
using Shared.Profiling;
using System.IO;
using Shared.Interfaces.Networking;
using ONI_United.Networking.Session;

namespace ONI_United.Networking.Packets.World
{
    public class StatusItemsSubscribePacket : IPacket
    {
        public int NetId;
        public bool Subscribe;

        public void Serialize(BinaryWriter writer)
        {
            using var _ = Profiler.Scope();
            writer.Write(NetId);
            writer.Write(Subscribe);
        }

        public void Deserialize(BinaryReader reader)
        {
            using var _ = Profiler.Scope();
            NetId = reader.ReadInt32();
            Subscribe = reader.ReadBoolean();
        }

        public void OnDispatched()
        {
            using var _ = Profiler.Scope();
            if (!MultiplayerSession.IsHost) return;
            if (Subscribe)
            {
                StatusBroadcaster.SubscribedNetIds.Add(NetId);
                StatusBroadcaster.PendingImmediate.Add(NetId);
            }
            else
            {
                StatusBroadcaster.SubscribedNetIds.Remove(NetId);
                StatusBroadcaster.PendingImmediate.Remove(NetId);
            }
        }
    }
}
