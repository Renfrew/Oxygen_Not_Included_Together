using System.IO;
using ONI_Together.DebugTools;
using ONI_United.Networking.Packets.Architecture;
using Shared.Profiling;
using ONI_Together.Networking.Packets.Architecture;
using ONI_Together.Networking;

namespace ONI_United.Networking.Packets.World
{
	public class TcpFallbackRequestPacket : IPacket, IAllowedWithoutWorldPacket
	{
		public ulong Requester;
		public ulong SynchronizationId;

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(Requester);
			writer.Write(SynchronizationId);
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			Requester = reader.ReadUInt64();
			SynchronizationId = reader.ReadUInt64();
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHost)
				return;

			DebugConsole.Log($"[TcpFallback] Client {Requester} requested UDP fallback for save transfer");
			ReadyManager.RetrySynchronizationTransfer(Requester, SynchronizationId, true);
		}
	}
}
