using ONI_Together.Misc.World;
using ONI_United.Networking.Packets.Architecture;
using System.IO;
using Shared.Profiling;
using ONI_Together.Networking.Packets.Architecture;
using ONI_Together.Networking;

namespace ONI_United.Networking.Packets.World
{
	public class SaveFileChunkPacket : IPacket, IAllowedWithoutWorldPacket
	{
		public string FileName;
		public string TransferId;
		public int Offset;
		public int TotalSize;
		public byte[] Chunk;

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(FileName);
			writer.Write(TransferId);
			writer.Write(Offset);
			writer.Write(TotalSize);
			writer.Write(Chunk.Length);
			writer.Write(Chunk);
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			FileName = reader.ReadString();
			TransferId = reader.ReadString();
			Offset = reader.ReadInt32();
			TotalSize = reader.ReadInt32();
			int length = reader.ReadInt32();
			Chunk = reader.ReadBytes(length);
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (!SaveFileRequestPacket.TryGetSynchronizationId(TransferId, out ulong synchronizationId)
				|| !GameClient.CanReceiveSynchronizationSave(synchronizationId))
			{
				return;
			}

			SaveChunkAssembler.ReceiveChunk(this);
		}
	}
}
