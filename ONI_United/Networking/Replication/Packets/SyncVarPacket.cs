using System.IO;
using ONI_United.Misc;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Session;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Replication.Components;
using ONI_United.Networking.Packets.Architecture;
using Shared.Replication;
using Shared.Profiling;
using UnityEngine;
using Shared.Interfaces.Networking;

namespace ONI_United.Networking.Replication.Packets
{
    public class SyncVarPacket : IPacket
    {
        public int NetId;
        public int BehaviourId;
        public int FieldHash;
        public Variant Value;
        public long Timestamp;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(NetId);
            writer.Write(BehaviourId);
            writer.Write(FieldHash);
            Value.Write(writer);
            writer.Write(Timestamp);
        }

        public void Deserialize(BinaryReader reader)
        {
            NetId = reader.ReadInt32();
            BehaviourId = reader.ReadInt32();
            FieldHash = reader.ReadInt32();
            Value = Variant.Read(reader);
            Timestamp = reader.ReadInt64();
        }

        public void OnDispatched()
        {
            using var _ = Profiler.Scope();

            if (MultiplayerSession.IsHost) return;

            if (!SyncBehaviourResolver.TryResolve(NetId, BehaviourId, out var behaviour))
                return;

            behaviour.RefreshSyncVars();
            int fieldCount = behaviour.SyncVarCount;
            for (int i = 0; i < fieldCount; i++)
            {
                var field = behaviour.GetSyncVar(i);
                if (field.Hash == FieldHash)
                {
                    var obj = VariantHelper.VariantToObject(Value, field.FieldType);
                    behaviour.ApplySyncVar(FieldHash, obj, Timestamp);
                    return;
                }
            }
        }

    }
}