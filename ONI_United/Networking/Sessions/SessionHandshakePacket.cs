using System;
using System.IO;
using ONI_United.Networking.Packets.Architecture;
using Shared.Interfaces.Networking;

namespace ONI_United.Networking.Sessions;

internal enum SessionHandshakeMessage : byte
{
    ClientHello = 1,
    Welcome = 2,
    Rejected = 3
}

internal interface ISessionControlPacket
{
    void HandleSessionPacket(ulong? senderPlayerId, object physicalConnection);
}

internal sealed class SessionHandshakePacket : IPacket, IAllowedWithoutWorldPacket, ISessionControlPacket
{
    public SessionHandshakePacket() { }

    internal SessionHandshakeMessage Message;
    internal int ProtocolVersion;
    internal int PacketRegistryFingerprint;
    internal string ModVersion = string.Empty;
    internal ulong PlayerId;
    internal ulong HostPlayerId;
    internal string PlayerName = string.Empty;
    internal string RejectionReason = string.Empty;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)Message);
        writer.Write(ProtocolVersion);
        writer.Write(PacketRegistryFingerprint);
        writer.Write(ModVersion ?? string.Empty);
        writer.Write(PlayerId);
        writer.Write(HostPlayerId);
        writer.Write(PlayerName ?? string.Empty);
        writer.Write(RejectionReason ?? string.Empty);
    }

    public void Deserialize(BinaryReader reader)
    {
        Message = (SessionHandshakeMessage)reader.ReadByte();
        ProtocolVersion = reader.ReadInt32();
        PacketRegistryFingerprint = reader.ReadInt32();
        ModVersion = reader.ReadString();
        PlayerId = reader.ReadUInt64();
        HostPlayerId = reader.ReadUInt64();
        PlayerName = reader.ReadString();
        RejectionReason = reader.ReadString();

        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Session handshake contains trailing data.");
    }

    public void OnDispatched() { }

    public void HandleSessionPacket(ulong? senderPlayerId, object physicalConnection) =>
        SessionHandshakeCoordinator.Handle(this, senderPlayerId, physicalConnection);

    internal static SessionHandshakePacket CreateClientHello(ulong playerId, string playerName) =>
        new SessionHandshakePacket
        {
            Message = SessionHandshakeMessage.ClientHello,
            ProtocolVersion = ProtocolCompatibility.CurrentProtocolVersion,
            PacketRegistryFingerprint = ProtocolCompatibility.PacketFingerprint,
            ModVersion = ProtocolCompatibility.ModVersion,
            PlayerId = playerId,
            PlayerName = playerName
        };

    internal static SessionHandshakePacket CreateWelcome(ulong playerId, ulong hostPlayerId, string hostName) =>
        new SessionHandshakePacket
        {
            Message = SessionHandshakeMessage.Welcome,
            ProtocolVersion = ProtocolCompatibility.CurrentProtocolVersion,
            PacketRegistryFingerprint = ProtocolCompatibility.PacketFingerprint,
            ModVersion = ProtocolCompatibility.ModVersion,
            PlayerId = playerId,
            HostPlayerId = hostPlayerId,
            PlayerName = hostName
        };

    internal static SessionHandshakePacket CreateRejected(string reason) =>
        new SessionHandshakePacket
        {
            Message = SessionHandshakeMessage.Rejected,
            RejectionReason = reason ?? string.Empty
        };

    internal bool HasValidMetadata =>
        !string.IsNullOrWhiteSpace(ModVersion)
        && !string.IsNullOrWhiteSpace(PlayerName)
        && PlayerName.Length <= Session.MaximumPlayerNameLength
        && RejectionReason.Length <= 256;
}
