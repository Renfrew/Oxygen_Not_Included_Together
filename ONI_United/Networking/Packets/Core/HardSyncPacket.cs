using ONI_United.Networking.Packets.Architecture;
using System.IO;
using Shared.Profiling;
using Shared.Interfaces.Networking;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Session;

namespace ONI_United.Networking.Packets.Core
{
	public class HardSyncPacket : IPacket, IAllowedWithoutWorldPacket
	{
		public ulong SynchronizationId;
		public void Serialize(BinaryWriter writer)
		{
			writer.Write(SynchronizationId);
		}

		public void Deserialize(BinaryReader reader)
		{
			SynchronizationId = reader.ReadUInt64();
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (MultiplayerSession.IsHost)
				return;

			if (!GameClient.BeginSynchronization(SynchronizationId))
				return;

			// Hide cursors until their current positions arrive after synchronization.
			foreach (PlayerCursor cursor in MultiplayerSession.PlayerCursors.Values)
				cursor.SetVisibility(false);
		}

	}
}
