using System.IO;
using Shared.Interfaces.Networking;

namespace ONI_United.Networking.Packets.Architecture
{
	internal static class PacketSerialization
	{
		internal static byte[] Serialize(IPacket packet)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream);
			writer.Write(PacketRegistry.GetPacketId(packet));
			packet.Serialize(writer);
			return stream.ToArray();
		}
	}
}
