using System;
using System.IO;
using ONI_United.DebugTools;
using ONI_United.Networking.Packets.World;
using ONI_United.Networking.Packets.World.Buildings;
using ONI_United.Networking.Overlay;
using ONI_United.Networking.Sessions.States;
using Shared.Profiling;
using Shared.Interfaces.Networking;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Sessions;

namespace ONI_United.Networking.Packets.Architecture
{
	public static class PacketHandler
	{
		[ThreadStatic]
		private static ulong? _currentSenderPlayerId;
		[ThreadStatic]
		private static object _currentPhysicalConnection;

		internal static ulong? CurrentSenderPlayerId => _currentSenderPlayerId;
		internal static object CurrentPhysicalConnection => _currentPhysicalConnection;

		public static void HandleIncoming(byte[] data, ulong? senderPlayerId = null, object physicalConnection = null)
		{
			using var _ = Profiler.Scope();

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            int type = reader.ReadInt32();
            if (!PacketRegistry.HasRegisteredPacket(type))
            {
                DebugConsole.LogError($"Invalid PacketType received: {type}", false);
                return;
            }

            using var scope = Profiler.Scope();
            var packet = PacketRegistry.Create(type);

            bool isSessionControl = packet is ISessionControlPacket;
            if (isSessionControl && data.Length > 2048)
            {
                SessionHandshakeCoordinator.RejectOversized(senderPlayerId, physicalConnection);
                return;
            }

            try
            {
                packet.Deserialize(reader);
            }
            catch (Exception ex) when (isSessionControl)
            {
                DebugConsole.LogWarning("[PacketHandler] Rejected malformed session handshake: " + ex.Message);
                SessionHandshakeCoordinator.RejectMalformed(physicalConnection);
                return;
            }

            if (packet is ISessionControlPacket sessionControl)
            {
                sessionControl.HandleSessionPacket(senderPlayerId, physicalConnection);
                return;
            }

            if (physicalConnection != null || senderPlayerId.HasValue)
            {
                if (!IsAuthorizedGameplaySender(senderPlayerId, physicalConnection))
                    return;
            }

            if (Session.IsHost
                && packet is IClientIdentityPacket identityPacket
                && (!senderPlayerId.HasValue || identityPacket.ClaimedSenderId != senderPlayerId.Value))
            {
                DebugConsole.LogWarning(
                    $"[PacketHandler] Rejected {packet.GetType().Name}: claimed sender does not match its physical connection.");
                return;
            }

            if (!ShouldDispatchPacket(packet))
                return;

            ulong? previousSender = _currentSenderPlayerId;
            object previousConnection = _currentPhysicalConnection;
            _currentSenderPlayerId = senderPlayerId;
            _currentPhysicalConnection = physicalConnection;
            try
            {
                Dispatch(packet);
            }
            finally
            {
                _currentSenderPlayerId = previousSender;
                _currentPhysicalConnection = previousConnection;
            }

            scope.End(packet.GetType().Name, data.Length);
            PacketTracker.TrackIncoming(new PacketTracker.PacketTrackData
            {
                packet = packet,
                size = data.Length
            });

            var tracker = NetIdActivityTracker.Instance;
            if (tracker != null)
            {
                int netId = NetIdActivityTracker.GetNetIdFromPacket(packet);
                if (netId > 0)
                    tracker.RecordActivity(netId, data.Length);
            }
		}

		private static bool IsAuthorizedGameplaySender(ulong? senderPlayerId, object physicalConnection)
		{
			if (!senderPlayerId.HasValue || physicalConnection == null)
				return false;

			if (Session.IsHost)
			{
				if (!NetworkConfig.TransportServer.TryGetRemotePlayerId(physicalConnection, out ulong physicalPlayerId)
					|| physicalPlayerId != senderPlayerId.Value
					|| !MultiplayerSession.ConnectedPlayers.TryGetValue(physicalPlayerId, out var player))
					return false;

				return Equals(player.Connection, physicalConnection);
			}

			return Session.IsClient
				&& senderPlayerId.Value == Session.HostPlayerId
				&& NetworkConfig.TransportClient.IsCurrentConnection(physicalConnection);
		}

		public static bool ShouldDispatchPacket(IPacket packet)
		{
            if (Session.IsClient)
            {
                if (GameClient.State == ClientState.InGame)
                    return true;

                if (GameClient.State == ClientState.LoadingWorld)
                    return packet is IAllowedWithoutWorldPacket || IsLoadingWorldResponse(packet);

                return packet is IAllowedWithoutWorldPacket || packet is IModApiPacket;
            }

            // Single player case
			if (!Session.IsHost || !MultiplayerSession.SessionHasPlayers)
				return true;

            if (ReadyManager.IsSynchronizing)
            {
                if (UnityEngine.Time.realtimeSinceStartup < ReadyManager.GameplayDrainUntil)
                    return true;

                return packet is IAllowedWithoutWorldPacket || IsLoadingWorldRequest(packet);
            }

            return true;
		}

		internal static bool IsLoadingWorldRequest(IPacket packet)
		{
			return packet is RequestOperationalStatePacket or StructureStateRequestPacket;
		}

		internal static bool IsLoadingWorldResponse(IPacket packet)
		{
			return packet is OperationalStatePacket or StructureStatePacket or LogicStatePacket;
		}

		private static void Dispatch(IPacket packet)
		{
			using var _ = Profiler.Scope();

			packet.OnDispatched();
		}
	}
}
