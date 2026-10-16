using ONI_Together.Menus;
using ONI_United.Networking.Packets.Architecture;
using System.IO;
using Shared.Profiling;
using ONI_Together.Networking.Packets.Architecture;
using ONI_Together.Networking;

namespace ONI_United.Networking.Packets.Core
{
	public class ClientReadyStatusUpdatePacket : IPacket, IAllowedWithoutWorldPacket
	{
		public string Message;
		public ulong SynchronizationId;

		public ClientReadyStatusUpdatePacket() { }

		public ClientReadyStatusUpdatePacket(string message)
		{
			using var _ = Profiler.Scope();

			Message = message;
		}

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(Message);
			writer.Write(SynchronizationId);
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			Message = reader.ReadString();
			SynchronizationId = reader.ReadUInt64();
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			// Host updates theirs on each ready status packet so we dont do anything here
			if (MultiplayerSession.IsHost)
				return;

			if (SynchronizationId != 0 && !GameClient.IsCurrentSynchronization(SynchronizationId))
				return;
			MultiplayerOverlay.Show(Message);
		}
	}
}
