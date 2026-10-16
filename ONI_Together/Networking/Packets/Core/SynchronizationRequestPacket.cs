using System.IO;
using ONI_United.Networking.Packets.Architecture;
using ONI_Together.Networking.Packets.Architecture;
using ONI_Together.Networking;

namespace ONI_United.Networking.Packets.Core
{
	public class SynchronizationRequestPacket : IPacket, IAllowedWithoutWorldPacket
	{
		public ulong Requester;

		public void Serialize(BinaryWriter writer) => writer.Write(Requester);
		public void Deserialize(BinaryReader reader) => Requester = reader.ReadUInt64();
		public void OnDispatched() => ReadyManager.RequestSynchronization(Requester);
	}
}
