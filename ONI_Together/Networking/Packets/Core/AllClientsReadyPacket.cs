using ONI_United.DebugTools;
using ONI_United.UI.Screens;
using ONI_United.Networking.Packets.Architecture;
using System.IO;
using Shared.Profiling;
using Shared.Interfaces.Networking;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Session;

namespace ONI_United.Networking.Packets.Core
{
	public class AllClientsReadyPacket : IPacket, IAllowedWithoutWorldPacket
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

			if (MultiplayerSession.IsHost || !GameClient.CompleteSynchronization(SynchronizationId))
				return;

			DebugConsole.Log("[AllClientsReadyPacket] Synchronization completed; closing overlay");
			ProcessAllReady();
		}

		public static void ProcessAllReady()
		{
			using var _ = Profiler.Scope();

			MultiplayerOverlay.Show(ONI_Together.STRINGS.UI.MP_OVERLAY.SYNC.FINALIZING_SYNC);
            MultiplayerOverlay.Close();
		}
	}
}
