// Deterministic native-event doubles. No UDP sockets or worker threads run here.
#nullable disable
using System;
using System.Collections.Generic;
using System.IO;

namespace LiteNetLib
{
    internal enum ConnectionState { Connected, ShutdownRequested, Disconnected }
    internal enum DeliveryMethod { Unreliable, ReliableOrdered }
    internal struct DisconnectInfo { }
    internal sealed class NetPacketReader
    {
        private readonly BinaryReader reader;
        internal NetPacketReader(byte[] bytes) => reader = new(new MemoryStream(bytes));
        internal int AvailableBytes => (int)(reader.BaseStream.Length - reader.BaseStream.Position);
        internal byte[] GetRemainingBytes() => reader.ReadBytes(AvailableBytes);
        internal bool TryGetString(out string value)
        { try { value = reader.ReadString(); return true; } catch { value = null; return false; } }
        internal bool TryGetULong(out ulong value)
        { try { value = reader.ReadUInt64(); return true; } catch { value = 0; return false; } }
    }
    internal sealed class NetPeer : System.Net.IPEndPoint
    {
        internal readonly int Id;
        internal ConnectionState ConnectionState = ConnectionState.Connected;
        internal readonly List<byte[]> Sent = new();
        internal bool SendFails;
        internal NetPeer(int id, int port = 0)
            : base(System.Net.IPAddress.Loopback, port == 0 ? 40000 + id : port) => Id = id;
        internal void Disconnect() => ConnectionState = ConnectionState.ShutdownRequested;
        internal void Send(byte[] bytes, DeliveryMethod mode)
        {
            if (SendFails) throw new IOException("Native send failure");
            if (mode != DeliveryMethod.ReliableOrdered) throw new InvalidOperationException();
            Sent.Add(bytes);
        }
    }
    internal sealed class ConnectionRequest
    {
        internal readonly NetPacketReader Data;
        internal readonly NetPeer Peer;
        internal bool Rejected;
        internal ConnectionRequest(NetPeer peer, byte[] data) { Peer = peer; Data = new(data); }
        internal NetPeer Accept() => Peer;
        internal void Reject() => Rejected = true;
    }
    internal sealed class EventBasedNetListener
    {
        internal event Action<ConnectionRequest> ConnectionRequestEvent;
        internal event Action<NetPeer, DisconnectInfo> PeerDisconnectedEvent;
        internal event Action<NetPeer, NetPacketReader, byte, DeliveryMethod> NetworkReceiveEvent;
        internal void Request(ConnectionRequest request) => ConnectionRequestEvent?.Invoke(request);
        internal void Disconnect(NetPeer peer) => PeerDisconnectedEvent?.Invoke(peer, default);
        internal void Receive(NetPeer peer, byte[] bytes) =>
            NetworkReceiveEvent?.Invoke(peer, new(bytes), 0, DeliveryMethod.ReliableOrdered);
    }
    internal sealed class NetManager
    {
        internal static NetManager Last;
        internal static bool StartSucceeds = true, StartThrows, StopThrows;
        internal readonly EventBasedNetListener Listener;
        internal readonly Queue<Action> Events = new();
        internal bool AutoRecycle, UnsyncedEvents;
        internal byte ChannelsCount;
        internal int DisconnectTimeout, Port, StopCalls;
        internal bool IsRunning;
        internal NetManager(EventBasedNetListener listener) { Listener = listener; Last = this; }
        internal bool Start(int port)
        {
            Port = port;
            if (StartThrows) throw new IOException("Bind failure");
            return IsRunning = StartSucceeds;
        }
        internal void Stop()
        {
            StopCalls++;
            if (StopThrows) throw new IOException("Stop failure");
            IsRunning = false; Events.Clear();
        }
        internal void PollEvents() { while (Events.TryDequeue(out var next)) next(); }
    }
}
namespace ONI_United
{
    internal sealed class Configuration
    {
        internal static readonly Configuration Instance = new();
        internal readonly HostSettings Host = new();
        internal readonly NetworkSettings Network = new();
        internal readonly ClientSettings Client = new();
        internal sealed class ClientSettings { internal int TimeoutSeconds = 30; }
        internal sealed class HostSettings { internal int LanPort = 7777, TimeoutSeconds = 30, MaxLobbySize = 16; }
        internal sealed class NetworkSettings { internal bool BypassProtocolCompatibilityChecks; }
    }
}
namespace ONI_United.Networking
{
    internal static class ProtocolCompatibility
    {
        internal const int CurrentProtocolVersion = 4, PacketFingerprint = 17;
        internal const string ModVersion = "fixture";
        internal static bool Matches(int version, int fingerprint, string modVersion) =>
            version == CurrentProtocolVersion && fingerprint == PacketFingerprint && modVersion == ModVersion;
    }
}
namespace ONI_United.Networking.Packets.Architecture
{
    internal static class PacketRegistry
    {
        // A fixture registry ID; the real serializer and handshake packet are source-linked.
        internal static int GetPacketId(Shared.Interfaces.Networking.IPacket packet) =>
            packet is Sessions.SessionHandshakePacket ? 123 : throw new InvalidOperationException();
    }
}
namespace ONI_United.Networking.Sessions
{
    internal static class SessionHandshakeCoordinator
    {
        internal static void Handle(SessionHandshakePacket packet, ulong? sender, object connection) =>
            throw new InvalidOperationException("Draft must not dispatch through the legacy coordinator.");
    }
}
