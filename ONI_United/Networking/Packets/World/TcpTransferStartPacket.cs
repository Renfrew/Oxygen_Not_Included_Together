using System;
using System.IO;
using ONI_United.DebugTools;
using ONI_United.UI.Screens;
using ONI_United.Misc.World;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Transfer;
using Shared.Profiling;
using Shared.Interfaces.Networking;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Session;
using ONI_United;

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
