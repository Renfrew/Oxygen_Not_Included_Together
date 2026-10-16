using ONI_United.Networking.Packets.Architecture;
using System.Collections.Generic;
using System.IO;
using Shared.Profiling;
using Shared.Interfaces.Networking;
using ONI_United.Networking.Session;

namespace ONI_United.Networking.Packets.World
{
	public class DisinfectStatePacket : IPacket
	{
		public List<int> DisinfectCells = new List<int>();

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(DisinfectCells.Count);
			foreach (var cell in DisinfectCells)
			{
				writer.Write(cell);
			}
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			int count = reader.ReadInt32();
			DisinfectCells = new List<int>(count);
			for (int i = 0; i < count; i++)
			{
				DisinfectCells.Add(reader.ReadInt32());
			}
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (MultiplayerSession.IsHost) return;
			ONI_United.Networking.Synchronization.WorldStateSyncer.Instance?.OnDisinfectStateReceived(this);
		}
	}
}
