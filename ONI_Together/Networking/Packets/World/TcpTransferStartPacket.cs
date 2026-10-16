using System;
using System.IO;
using ONI_Together.DebugTools;
using ONI_Together.Menus;
using ONI_Together.Misc.World;
using ONI_United.Networking.Packets.Architecture;
using ONI_Together.Networking.Transfer;
using Shared.Profiling;
using ONI_Together.Networking.Packets.Architecture;
using ONI_Together.Networking;
using ONI_Together;

namespace ONI_United.Networking.Packets.World
{
	public class TcpTransferStartPacket : IPacket, IAllowedWithoutWorldPacket
	{
		public int TcpPort;
		public string FileName;
		public int FileSize;
		public ulong ClientId;
		public ulong SynchronizationId;

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(TcpPort);
			writer.Write(FileName);
			writer.Write(FileSize);
			writer.Write(ClientId);
			writer.Write(SynchronizationId);
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			TcpPort = reader.ReadInt32();
			FileName = reader.ReadString();
			FileSize = reader.ReadInt32();
			ClientId = reader.ReadUInt64();
			SynchronizationId = reader.ReadUInt64();
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (MultiplayerSession.IsHost || ClientId != MultiplayerSession.LocalUserID
				|| !GameClient.CanReceiveSynchronizationSave(SynchronizationId))
				return;

			string hostIp = Configuration.Instance.Client.LanSettings.Ip;

			DebugConsole.Log($"[TcpTransferStart] Starting TCP download from {hostIp}:{TcpPort}, file '{FileName}' ({FileSize} bytes), clientId={ClientId}");

			MultiplayerOverlay.Show("Connecting for save download...");

			ulong synchronizationId = SynchronizationId;
			Func<bool> isCurrent = () => GameClient.IsCurrentSynchronization(synchronizationId);
			TcpFileTransferClient.Download(hostIp, TcpPort, ClientId, synchronizationId, isCurrent,
				(fileName, data) =>
				{
					DebugConsole.Log($"[TcpTransferStart] TCP download complete: '{fileName}' ({data.Length} bytes)");
					var worldSave = new WorldSave(fileName, data);
					GameClient.SubmitSynchronizationSave(synchronizationId, worldSave, false);
				},
				(error) =>
				{
					DebugConsole.LogWarning($"[TcpTransferStart] TCP download failed: {error}. Requesting UDP fallback.");
					MultiplayerOverlay.Show("TCP connection failed. Downloading save via UDP...");

					GameClient.RequestSynchronizationTransferRetry(synchronizationId, true);
				});
		}
	}
}
