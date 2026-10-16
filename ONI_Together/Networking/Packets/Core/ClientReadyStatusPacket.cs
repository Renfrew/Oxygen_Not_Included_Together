using ONI_United.DebugTools;
using ONI_United.Misc;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Session.States;
using ONI_United.Networking.Replication.Components;
using Shared.Profiling;
using System.IO;
using Shared.Interfaces.Networking;
using ONI_United.Networking;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Session;

namespace ONI_United.Networking.Packets.Core
{
	class ClientReadyStatusPacket : IPacket, IAllowedWithoutWorldPacket
	{
		public ulong SenderId;
		public ulong SynchronizationId;
		public ClientReadyState Status = ClientReadyState.Unready;
		public string PlayerName = string.Empty;

		public ClientReadyStatusPacket() { }

		public ClientReadyStatusPacket(ulong senderId, ClientReadyState status)
		{
			using var _ = Profiler.Scope();

			SenderId = senderId;
			Status = status;
		}

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(SenderId);
			writer.Write(SynchronizationId);
			writer.Write((int)Status);
			writer.Write(PlayerName ?? string.Empty);
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			SenderId = reader.ReadUInt64();
			SynchronizationId = reader.ReadUInt64();
			Status = (ClientReadyState)reader.ReadInt32();
			PlayerName = reader.ReadString();
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHost)
			{
				if (string.IsNullOrEmpty(PlayerName))
					return;

				MultiplayerSession.KnownPlayerNames[SenderId] = PlayerName;

				if (SenderId == MultiplayerSession.HostUserID)
				{
					if (MultiplayerSession.ConnectedPlayers.TryGetValue(SenderId, out var host) && host != null)
						host.PlayerName = PlayerName;
				}
				else
				{
					OxySyncChat.AddSystemMessage(
						string.Format(ONI_United.STRINGS.UI.MP_CHATWINDOW.CHAT_CLIENT_JOINED, PlayerName));
				}
				return;
			}

			if (!MultiplayerSession.ConnectedPlayers.TryGetValue(SenderId, out MultiplayerPlayer player))
			{
				DebugConsole.LogError($"Tried to update ready state for unknown player {SenderId}", false);
				return;
			}

			if (Status == ClientReadyState.Loading)
			{
				DebugConsole.LogWarning(
					$"[ClientReadyStatusPacket] ignored client-originated Loading status from {SenderId}; " +
					$"only the host may begin synchronization.");
				return;
			}

			if (Status == ClientReadyState.Ready)
			{
				if (!ReadyManager.IsLoadingParticipant(SenderId, SynchronizationId))
				{
					DebugConsole.LogWarning(
						$"[ClientReadyStatusPacket] rejected Ready for PlayerId={SenderId} " +
						$"while synchronization={ReadyManager.IsSynchronizing} state={player.readyState}");
					return;
				}
			}

			bool nameChanged = !string.IsNullOrEmpty(PlayerName) && player.PlayerName != PlayerName;
			if (nameChanged)
				player.PlayerName = PlayerName;

			if (Status == ClientReadyState.Ready || !ReadyManager.IsSynchronizing)
				ReadyManager.SetPlayerReadyState(player, Status);

			DebugConsole.Log($"[ClientReadyStatusPacket] {SenderId} marked as {Status}");

			if (NetworkConfig.IsLanConfig() && nameChanged)
			{
				OxySyncChat.AddSystemMessage(
					string.Format(ONI_United.STRINGS.UI.MP_CHATWINDOW.CHAT_CLIENT_JOINED, player.PlayerName));

				PacketSender.SendToAllClients(new ClientReadyStatusPacket
				{
					SenderId = MultiplayerSession.HostUserID,
					PlayerName = Utils.GetLocalPlayerName()
				});

				PacketSender.SendToAllClients(new ClientReadyStatusPacket
				{
					SenderId = SenderId,
					PlayerName = player.PlayerName
				});
			}

			ReadyManager.RefreshScreen();
			ReadyManager.RefreshReadyState();
		}
	}
}
