using System.IO;
using ONI_United.Networking.Replication.Components;
using ONI_United.Networking.Session;
using ONI_United.Networking.Packets.Architecture;
using Shared.Profiling;
using Shared.Interfaces.Networking;

namespace ONI_United.Networking.Replication.Packets
{
    // Currently unused, the host now manages interest groups. This can technically be deleted
    public class InterestGroupSubscribePacket : IPacket
    {
        public ulong SenderId;
        public int GroupId;
        public bool Subscribe;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(SenderId);
            writer.Write(GroupId);
            writer.Write(Subscribe);
        }

        public void Deserialize(BinaryReader reader)
        {
            SenderId = reader.ReadUInt64();
            GroupId = reader.ReadInt32();
            Subscribe = reader.ReadBoolean();
        }

        public void OnDispatched()
        {
            using var _ = Profiler.Scope();

            if (!MultiplayerSession.IsHost) return;

            if (Subscribe)
            {
                InterestGroupManager.AddPlayerToGroup(SenderId, GroupId);
                OxySyncManager.SendFullStateToPlayerForGroup(SenderId, GroupId);
            }
            else
                InterestGroupManager.RemovePlayerFromGroup(SenderId, GroupId);
        }
    }
}