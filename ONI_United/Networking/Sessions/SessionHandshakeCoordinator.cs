using System;
using System.Linq;
using ONI_United.DebugTools;
using ONI_United.Misc;
using ONI_United.Networking.Transport;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Transport.Steamworks;
using Shared.Interfaces.Networking;
using Shared;

namespace ONI_United.Networking.Sessions;

internal static class SessionHandshakeCoordinator
{
    private static bool CanAdmitRemotePlayer(int maxPlayers) =>
        Session.IsHost && maxPlayers > 0 && Session.Players.Count < maxPlayers;

    private const int MaximumHandshakeBytes = 2048;

    internal static void SendClientHello(object connection)
    {
        ulong localPlayerId = NetworkConfig.GetLocalID();
        if (connection == null || localPlayerId == 0)
        {
            DebugConsole.LogError("[SessionHandshake] Cannot send hello without a physical connection and valid local player ID.");
            NetworkConfig.TransportClient.Disconnect();
            return;
        }

        var hello = SessionHandshakePacket.CreateClientHello(localPlayerId, Utils.GetLocalPlayerName());
        if (!NetworkConfig.TransportPacketSender.SendPacket(connection, hello, PacketSendMode.ReliableImmediate))
        {
            DebugConsole.LogError("[SessionHandshake] Failed to queue client hello.");
            NetworkConfig.TransportClient.Disconnect();
        }
    }

    internal static void RejectOversized(ulong? senderPlayerId, object physicalConnection)
    {
        if (Session.IsHost)
        {
            RejectHostConnection(physicalConnection, "Session handshake exceeds the allowed size.");
            return;
        }

        if (NetworkConfig.TransportClient.IsCurrentConnection(physicalConnection))
            RejectLocalConnection("Session handshake exceeds the allowed size.");
    }

    internal static void RejectMalformed(object physicalConnection)
    {
        if (Session.IsHost)
        {
            RejectHostConnection(physicalConnection, "Malformed session handshake.");
            return;
        }

        if (NetworkConfig.TransportClient.IsCurrentConnection(physicalConnection))
            RejectLocalConnection("Malformed session handshake.");
    }

    internal static void Handle(
        SessionHandshakePacket packet,
        ulong? senderPlayerId,
        object physicalConnection)
    {
        if (packet.Message == SessionHandshakeMessage.ClientHello)
        {
            HandleClientHello(packet, senderPlayerId, physicalConnection);
            return;
        }

        if (packet.Message == SessionHandshakeMessage.Welcome)
        {
            HandleWelcome(packet, senderPlayerId, physicalConnection);
            return;
        }

        if (packet.Message == SessionHandshakeMessage.Rejected)
        {
            if (NetworkConfig.TransportClient.IsCurrentConnection(physicalConnection))
                RejectLocalConnection(packet.RejectionReason.Length <= 256 && packet.RejectionReason.Length > 0
                    ? packet.RejectionReason
                    : "The host rejected the session connection.");
            return;
        }

        if (Session.IsHost)
            RejectHostConnection(physicalConnection, "Unknown session handshake message.");
        else if (NetworkConfig.TransportClient.IsCurrentConnection(physicalConnection))
            RejectLocalConnection("Unknown session handshake message.");
    }

    private static void HandleClientHello(
        SessionHandshakePacket packet,
        ulong? senderPlayerId,
        object physicalConnection)
    {
        if (!Session.IsHost
            || !senderPlayerId.HasValue
            || !NetworkConfig.TransportServer.TryGetRemotePlayerId(physicalConnection, out ulong transportPlayerId)
            || transportPlayerId != senderPlayerId.Value)
        {
            RejectHostConnection(physicalConnection, "Client hello did not match an active physical connection.");
            return;
        }

        bool validMetadata = packet.HasValidMetadata
            && packet.HostPlayerId == 0
            && packet.RejectionReason.Length == 0;
        bool protocolCompatible = validMetadata
            && (Configuration.Instance.Network.BypassProtocolCompatibilityChecks
                || ProtocolCompatibility.Matches(packet.ProtocolVersion, packet.PacketRegistryFingerprint, packet.ModVersion));
        bool hasExistingMember = Session.Players.TryGetValue(transportPlayerId, out Player existingMember);
        bool duplicate = hasExistingMember
            && (existingMember is not MultiplayerPlayer existingPlayer
                || !Equals(existingPlayer.Connection, physicalConnection));

        if (existingMember is MultiplayerPlayer existingAdmittedPlayer
            && Equals(existingAdmittedPlayer.Connection, physicalConnection))
        {
            SendWelcome(transportPlayerId, physicalConnection);
            return;
        }

        string name;
        string reason;
        bool valid = SessionHandshakeRules.TryValidateClient(
            packet.PlayerId,
            transportPlayerId,
            Session.HostPlayerId,
            duplicate,
            protocolCompatible,
            packet.PlayerName,
            out name,
            out reason);

        if (!valid)
        {
            if (!validMetadata)
                reason = "Client hello metadata is invalid.";
            else if (!protocolCompatible)
                reason = ProtocolCompatibility.BuildMismatchReason(
                    packet.ProtocolVersion,
                    packet.PacketRegistryFingerprint,
                    packet.ModVersion,
                    true);
            RejectHostConnection(physicalConnection, reason);
            return;
        }

        if (!CanAdmitRemotePlayer(NetworkConfig.GetMaxServerCapacity()))
        {
            RejectHostConnection(physicalConnection, "Session is full.");
            return;
        }

        if (NetworkConfig.IsSteamConfig()
            && (!SteamLobby.InLobby
                || !SteamLobby.GetAllLobbyMembers().Any(member => member.m_SteamID == transportPlayerId)))
        {
            RejectHostConnection(physicalConnection, "Player is not a member of the Steam lobby.");
            return;
        }

        var player = new MultiplayerPlayer(transportPlayerId, name);
        if (!Session.TryAddPlayer(player))
        {
            RejectHostConnection(physicalConnection, "Host could not register the admitted player.");
            return;
        }

        MultiplayerSession.InitializeAdmittedClient(player, physicalConnection);
        NetworkConfig.TransportServer.OnSessionPlayerAdmitted(transportPlayerId);
        SendWelcome(transportPlayerId, physicalConnection);
    }

    private static void HandleWelcome(
        SessionHandshakePacket packet,
        ulong? senderPlayerId,
        object physicalConnection)
    {
        if (!NetworkConfig.TransportClient.IsCurrentConnection(physicalConnection)
            || Session.IsHost
            || Session.IsActive)
            return;

        ulong localPlayerId = NetworkConfig.GetLocalID();
        ulong transportHostId = senderPlayerId ?? packet.HostPlayerId;
        bool protocolCompatible = Configuration.Instance.Network.BypassProtocolCompatibilityChecks
            || ProtocolCompatibility.Matches(packet.ProtocolVersion, packet.PacketRegistryFingerprint, packet.ModVersion);
        bool hostIdMatches = transportHostId == packet.HostPlayerId
            && (!MultiplayerSession.TargetHostId.HasValue
                || MultiplayerSession.TargetHostId.Value == packet.HostPlayerId);

        if (!protocolCompatible
            || !hostIdMatches
            || packet.PlayerId != localPlayerId
            || packet.HostPlayerId == 0
            || !packet.HasValidMetadata
            || packet.RejectionReason.Length != 0
            || !SessionHandshakeRules.TryValidateClient(
                packet.HostPlayerId,
                transportHostId,
                localPlayerId,
                false,
                true,
                packet.PlayerName,
                out string hostName,
                out _))
        {
            RejectLocalConnection(!protocolCompatible
                ? ProtocolCompatibility.BuildMismatchReason(
                    packet.ProtocolVersion,
                    packet.PacketRegistryFingerprint,
                    packet.ModVersion,
                    true)
                : "Host welcome did not match the active connection.");
            return;
        }

        MultiplayerSession.SetHost(packet.HostPlayerId);
        var localPlayer = new MultiplayerPlayer(localPlayerId, Utils.GetLocalPlayerName());
        var hostPlayer = new MultiplayerPlayer(packet.HostPlayerId, hostName);
        if (!Session.AdmitClient(localPlayer, hostPlayer))
        {
            RejectLocalConnection("Session role could not be established.");
            return;
        }

        Game.Instance?.Trigger(MP_HASHES.OnInSessionChanged, BoxedBools.True);
        MultiplayerSession.OnConnectedToHost(packet.HostPlayerId, physicalConnection);
        if (!Session.IsClient)
        {
            RejectLocalConnection("Session role could not be established.");
            return;
        }

        NetworkConfig.TransportClient.OnClientConnected?.Invoke();
        NetworkConfig.TransportClient.OnRequestStateOrReturn?.Invoke();
    }

    private static void SendWelcome(ulong playerId, object physicalConnection)
    {
        var welcome = SessionHandshakePacket.CreateWelcome(
            playerId,
            NetworkConfig.GetLocalID(),
            Utils.GetLocalPlayerName());
        if (!NetworkConfig.TransportPacketSender.SendPacket(
                physicalConnection,
                welcome,
                PacketSendMode.ReliableImmediate))
        {
            RejectHostConnection(physicalConnection, "Host could not send session welcome.");
        }
    }

    private static void RejectHostConnection(object physicalConnection, string reason)
    {
        if (physicalConnection == null)
            return;

        NetworkConfig.TransportPacketSender.SendPacket(
            physicalConnection,
            SessionHandshakePacket.CreateRejected(reason),
            PacketSendMode.ReliableImmediate);
        NetworkConfig.TransportServer.DisconnectConnection(physicalConnection, reason);
    }

    private static void RejectLocalConnection(string reason)
    {
        DebugConsole.LogWarning("[SessionHandshake] Session join rejected: " + reason);
        NetworkConfig.TransportClient.OnConnectionFailed?.Invoke();
        NetworkConfig.TransportClient.OnReturnToMenu?.Invoke(reason, reason);
        NetworkConfig.TransportClient.Disconnect();
    }
}
