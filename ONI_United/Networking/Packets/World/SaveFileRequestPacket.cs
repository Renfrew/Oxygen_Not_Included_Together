using ONI_United.DebugTools;
using ONI_United.Misc;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Transport.LiteNetLib;
using ONI_United.Networking.Transport.Riptide;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shared.Profiling;
using Shared.Interfaces.Networking;
using ONI_United.Networking;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Session;
using ONI_United.Networking.Transfer;
using ONI_United;
using ONI_United.Misc.World;

namespace ONI_United.Networking.Packets.World
{
	public class SaveFileRequestPacket : IPacket, IAllowedWithoutWorldPacket
	{
		public ulong Requester;
		public ulong SynchronizationId;
		private static readonly Dictionary<ulong, object> TransferRuns = new Dictionary<ulong, object>();

		internal static string CreateTransferId(ulong synchronizationId) =>
			synchronizationId.ToString(CultureInfo.InvariantCulture);

		internal static bool TryGetSynchronizationId(string transferId, out ulong synchronizationId) =>
			ulong.TryParse(transferId, NumberStyles.None, CultureInfo.InvariantCulture, out synchronizationId)
			&& synchronizationId != 0;

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

			DebugConsole.Log($"[Packets/SaveFileRequest] Received request from {Requester}");
			ReadyManager.RetrySynchronizationTransfer(Requester, SynchronizationId, false);
		}

		public static void SendSaveFileToAll(byte[] snapshot, ulong synchronizationId)
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHost || snapshot == null || !ReadyManager.IsSynchronizing)
				return;

			string fileName = SaveHelper.WorldName + ".sav";
			foreach (ulong participant in ReadyManager.SynchronizationParticipants)
			{
				if (!ReadyManager.IsLoadingParticipant(participant, synchronizationId))
					continue;

				SendSaveFile(participant, fileName, snapshot, synchronizationId);
			}
		}

		internal static void SendSaveFile(ulong requester, string fileName, byte[] snapshot, ulong synchronizationId, bool forceGeneric = false)
		{
			if (!ReadyManager.IsLoadingParticipant(requester, synchronizationId) || snapshot == null)
			{
				DebugConsole.LogWarning($"[SaveFileRequest] Ignored stale save send for {requester}, sync {synchronizationId}.");
				return;
			}
			CancelTransfer(requester);
			object run = new object();
			TransferRuns[requester] = run;
			try
			{
				if (!forceGeneric && NetworkConfig.IsLanConfig() && NetworkConfig.TransportServer is LiteNetLibServer lnlServer && lnlServer.TcpTransfer != null)
				{
					int tcpPort = Configuration.Instance.Host.LanSettings.Port + 1;
					lnlServer.TcpTransfer.QueueTransfer(requester, synchronizationId, fileName, snapshot);

					var startPacket = new TcpTransferStartPacket
					{
						TcpPort = tcpPort,
						FileName = fileName,
						FileSize = snapshot.Length,
						ClientId = requester,
						SynchronizationId = synchronizationId
					};
					PacketSender.SendToPlayer(requester, startPacket);
					DebugConsole.Log($"[SaveFileRequest] Initiated TCP transfer for '{fileName}' to {requester}");
				}
				else
				{
					CoroutineRunner.RunOne(StreamChunks(snapshot, fileName, requester, synchronizationId, run));
				}
			}
			catch (Exception ex)
			{
				DebugConsole.LogError($"[SaveFileRequest] Failed to send save file to {requester}: {ex}");
			}
		}

		internal static void CancelTransfer(ulong requester)
		{
			TransferRuns.Remove(requester);
			SaveFileTransferManager.CancelClient(requester);
			if (NetworkConfig.TransportServer is LiteNetLibServer server)
				server.TcpTransfer?.CancelClient(requester);
			else if (NetworkConfig.TransportServer is RiptideServer riptide)
				riptide.TcpTransfer?.CancelClient(requester);
		}

		internal static void ClearPendingSynchronizationTransfers()
		{
			SaveFileTransferManager.ClearTransfers();
			TransferRuns.Clear();
			if (NetworkConfig.TransportServer is LiteNetLibServer server)
				server.TcpTransfer?.ClearPendingTransfers();
			else if (NetworkConfig.TransportServer is RiptideServer riptide)
				riptide.TcpTransfer?.ClearPendingTransfers();
		}


        private static IEnumerator StreamChunks(byte[] data, string fileName, ulong steamID, ulong synchronizationId, object run)
		{
			using var _ = Profiler.Scope();

			int chunkSize = SaveHelper.SAVEFILE_CHUNKSIZE_KB * 1024;
			int totalChunks = (int)Math.Ceiling((double)data.Length / chunkSize);

			// Optimization: Send multiple chunks per frame to maximize throughput
			// 2 chunks * 256KB = 512KB per frame. At 60FPS -> ~30MB/s theoretical max.
			int chunksPerFrame = 2;
			int chunksSentThisFrame = 0;

			DebugConsole.Log($"[SaveFileRequest] Starting SECURE transfer of '{fileName}' ({Utils.FormatBytes(data.Length)}) to {steamID} in {totalChunks} chunks.");

			// SUA IDEIA: Registra transferência no manager para rastrear ACKs
			string transferId = CreateTransferId(synchronizationId);
			SaveFileTransferManager.StartTransfer(steamID, transferId, fileName, data, chunkSize);

			for (int offset = 0; offset < data.Length; /* increments manually */)
			{
				if (!ReadyManager.IsLoadingParticipant(steamID, synchronizationId)
					|| !TransferRuns.TryGetValue(steamID, out var currentRun) || !ReferenceEquals(run, currentRun))
					yield break;

				int size = Math.Min(chunkSize, data.Length - offset);
				byte[] chunk = new byte[size];
				Buffer.BlockCopy(data, offset, chunk, 0, size);

				var chunkPacket = new SaveFileChunkPacket
				{
					FileName = fileName,
					TransferId = transferId,
					Offset = offset,
					TotalSize = data.Length,
					Chunk = chunk
				};

				// Wrap in secure transfer packet for integrity validation
				var securePacket = new SecureTransferPacket
				{
					SequenceNumber = offset / chunkSize,  // Calculate chunk index from offset
					TransferId = transferId,
					PayloadBytes = SecureTransferPacket.SerializeSaveFileChunk(chunkPacket)
				};

				bool success = PacketSender.SendToPlayer(steamID, securePacket);

				if (success)
				{
					// SUA IDEIA: Marca chunk como enviado no sistema ACK
					int chunkIndex = offset / chunkSize;
					SaveFileTransferManager.MarkChunkSent(steamID, transferId, chunkIndex);

					offset += chunkSize; // Only advance if sent successfully
					chunksSentThisFrame++;
					if (chunksSentThisFrame >= chunksPerFrame)
					{
						chunksSentThisFrame = 0;
						yield return null; // Wait for next frame
					}
				}
				else
				{
					// Backpressure: Failed to send (buffer likely full). Wait and retry same offset.
					//DebugConsole.LogWarning($"[SaveFileRequest] Buffer full/Send failed. Retrying...");
					chunksSentThisFrame = 0;
					yield return null;
				}
			}

			DebugConsole.Log($"[SaveFileRequest] SECURE transfer complete. Sent {totalChunks} chunks to {steamID}. Client will validate integrity.");
		}

    }
}
