using System.IO;
using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Synchronization.Structures;
using ONI_United.Networking.Packets.Architecture;
using Shared.Interfaces.Networking;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Session;

namespace ONI_United.Networking.Packets.World;

public class StructureStateRequestPacket : IPacket
{
    public ulong RequesterId;
    public int NetId;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(RequesterId);
        writer.Write(NetId);
    }

    public void Deserialize(BinaryReader reader)
    {
        RequesterId = reader.ReadUInt64();
        NetId = reader.ReadInt32();
    }

    public void OnDispatched()
    {
        if (!MultiplayerSession.IsHost) return;

        if (!NetworkIdentityRegistry.TryGet(NetId, out var identity))
        {
            LogicStateSyncer.Instance?.SendStateToClient(RequesterId, NetId);
            return;
        }

        var syncers = identity.GetComponents<StructureSyncerBase>();
        foreach (var syncer in syncers)
            syncer.SendStateToClient(RequesterId);

        LogicStateSyncer.Instance?.SendStateToClient(RequesterId, NetId);
    }
}
