using System.IO;
using ONI_United.Networking.Packets.Architecture;
using Shared.Interfaces.Networking;
using ONI_United.Networking.Runtime;

namespace ONI_United.Networking.Packets.Core
{
	public class SynchronizationRequestPacket : IPacket, IAllowedWithoutWorldPacket, IClientIdentityPacket
	{
		public ulong Requester;
		ulong IClientIdentityPacket.ClaimedSenderId => Requester;

		public void Serialize(BinaryWriter writer) => writer.Write(Requester);
		public void Deserialize(BinaryReader reader) => Requester = reader.ReadUInt64();
		public void OnDispatched() => ReadyManager.RequestSynchronization(Requester);
	}
}
