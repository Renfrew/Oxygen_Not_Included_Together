using System.IO;
using ONI_United.Networking.Session;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Replication.Components;
using ONI_United.Networking.Packets.Architecture;
using Shared.Replication;
using Shared.Profiling;
using Shared.Interfaces.Networking;

namespace ONI_United.Networking.Replication.Packets
{
    public class CommandPacket : IPacket
    {
        public int NetId;
        public int BehaviourId;
        public int MethodHash;
        public byte[] Args;

        public CommandPacket()
        {
            Args = System.Array.Empty<byte>();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(NetId);
            writer.Write(BehaviourId);
            writer.Write(MethodHash);
            writer.Write(Args.Length);
            writer.Write(Args);
        }

        public void Deserialize(BinaryReader reader)
        {
            NetId = reader.ReadInt32();
            BehaviourId = reader.ReadInt32();
            MethodHash = reader.ReadInt32();
            int len = reader.ReadInt32();
            Args = reader.ReadBytes(len);
        }

        public void OnDispatched()
        {
            using var _ = Profiler.Scope();

            if (!MultiplayerSession.IsHost) return;

            if (!SyncBehaviourResolver.TryResolve(NetId, BehaviourId, out var behaviour))
                return;

            behaviour.InvokeCommand(MethodHash, Args);
        }
    }
}