using System.Collections.Generic;
using ONI_United.DebugTools;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Overlay;
using ONI_United.Networking.Replication;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Transfer;
using Shared.Interfaces.Networking;
using States = ONI_United.Networking.Sessions.States;

namespace ONI_United.Networking.Packets.Architecture
{
	internal interface IGameplayPacketSender
	{
		bool SendToHost(IPacket packet, PacketSendMode sendType = PacketSendMode.ReliableImmediate);
		bool SendToPlayer(ulong playerId, IPacket packet, PacketSendMode sendType = PacketSendMode.ReliableImmediate);
		int Broadcast(IPacket packet, PacketSendMode sendType = PacketSendMode.Reliable, ulong? exclude = null);
	}

	internal sealed class GameplayPacketSender : IGameplayPacketSender
	{
		internal static readonly GameplayPacketSender Instance = new GameplayPacketSender();

		private GameplayPacketSender() { }

		public bool SendToHost(IPacket packet, PacketSendMode sendType = PacketSendMode.ReliableImmediate)
		{
			if (!Session.IsClient || Session.HostPlayerId == 0)
				return false;

			return SendToPlayer(Session.HostPlayerId, packet, sendType);
		}

		public bool SendToPlayer(ulong playerId, IPacket packet, PacketSendMode sendType = PacketSendMode.ReliableImmediate)
		{
			if (!Session.IsActive || !CanSendPacket(packet))
				return false;

			if (!MultiplayerSession.ConnectedPlayers.TryGetValue(playerId, out var player)
				|| player.Connection == null
				|| (Session.IsHost && playerId == Session.HostPlayerId))
				return false;

			return SendToConnection(player.Connection, packet, sendType);
		}

		public int Broadcast(IPacket packet, PacketSendMode sendType = PacketSendMode.Reliable, ulong? exclude = null)
		{
			if (!Session.IsHost || !CanSendPacket(packet))
				return 0;

			var recipients = new List<MultiplayerPlayer>(MultiplayerSession.ConnectedPlayers.Values);
			int sentCount = 0;
			bool cullByViewport = packet is IViewportCullable && WorldStateSyncer.Instance != null;
			int viewportCell = cullByViewport ? ((IViewportCullable)packet).GetViewportCell() : 0;

			foreach (var player in recipients)
			{
				if (player == null || player.Connection == null
					|| player.PlayerId == Session.HostPlayerId
					|| !player.ProtocolVerified
					|| (exclude.HasValue && player.PlayerId == exclude.Value))
					continue;

				if (cullByViewport && !WorldStateSyncer.Instance.IsCellInPlayerViewport(player.PlayerId, viewportCell))
					continue;

				if (SendToConnection(player.Connection, packet, sendType))
					sentCount++;
			}

			return sentCount;
		}

		internal bool SendToConnection(object connection, IPacket packet, PacketSendMode sendType)
		{
			if (connection == null || packet == null || !CanSendPacket(packet))
				return false;

			return NetworkConfig.TransportPacketSender.SendToConnection(connection, packet, sendType);
		}

		internal static bool CanSendPacket(IPacket packet)
		{
			if (packet is IAllowedWithoutWorldPacket)
				return true;

			if (Session.IsClient)
			{
				if (GameClient.State == States.ClientState.InGame)
					return true;

				if (GameClient.State == States.ClientState.LoadingWorld)
					return PacketHandler.IsLoadingWorldRequest(packet);

				return packet is IModApiPacket;
			}

			if (Session.IsHost && ReadyManager.IsSynchronizing)
				return PacketHandler.IsLoadingWorldResponse(packet);

			return true;
		}
	}
}
