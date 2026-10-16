using ONI_United.DebugTools;
using ONI_United.Networking.Session;
using ONI_United.Networking.Transfer;
using ONI_United.Networking.Packets.Architecture;
using Steamworks;
using System.IO;
using Shared.Profiling;
using Shared.Interfaces.Networking;

namespace ONI_United.Networking.Packets.World
{
    /// <summary>
    /// Client sends ACK to confirm that it received a specific chunk
    /// Server uses this to detect lost chunks and resend only the necessary ones
    /// </summary>
    public class ChunkAckPacket : IPacket, IAllowedWithoutWorldPacket
    {
        public int SequenceNumber;       // ID of chunk that was received (0, 1, 2, 3...)
        public string TransferId;        // Transfer ID (same as SecureTransferPacket)
        public ulong ClientSteamID;   // Who is sending the ACK

        public void Serialize(BinaryWriter writer)
        {
            using var _ = Profiler.Scope();

            writer.Write(SequenceNumber);
            writer.Write(TransferId);
            writer.Write(ClientSteamID);
        }

        public void Deserialize(BinaryReader reader)
        {
            using var _ = Profiler.Scope();

            SequenceNumber = reader.ReadInt32();
            TransferId = reader.ReadString();
            ClientSteamID = reader.ReadUInt64();
        }

        public void OnDispatched()
        {
            using var _ = Profiler.Scope();

            // Only server processes ACKs
            if (!MultiplayerSession.IsHost)
                return;

            DebugConsole.Log($"[ChunkAck] Received ACK {SequenceNumber} from {ClientSteamID} for transfer {TransferId}");

            // Inform transfer system about the ACK
            SaveFileTransferManager.HandleChunkAck(ClientSteamID, TransferId, SequenceNumber);
        }
    }
}