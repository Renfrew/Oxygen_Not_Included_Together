#nullable disable
using System.Collections;
using System.IO;
using System.Reflection;
using LiteNetLib;
using ModConfiguration = ONI_United.Configuration;
using ONI_United.Core;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transport.LiteNetLib;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[Collection("Lifecycle")]
public sealed class LiteNetLibServerDraftTests : IDisposable
{
    private readonly Lifecycle machine = new();
    private readonly LiteNetLibServerDraft server = new();
    private IDictionary Peers => (IDictionary)typeof(LiteNetLibServerDraft)
        .GetField("physicalPeers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(server);
    private void Invoke(string method) => typeof(Lifecycle)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(machine, null);
    public LiteNetLibServerDraftTests()
    {
        Reset();
        NetManager.Last = null;
        NetManager.StartSucceeds = true;
        NetManager.StartThrows = NetManager.StopThrows = false;
        ModConfiguration.Instance.Host.LanPort = 7777;
        ModConfiguration.Instance.Host.TimeoutSeconds = 30;
        ModConfiguration.Instance.Host.MaxLobbySize = 16;
        ModConfiguration.Instance.Network.BypassProtocolCompatibilityChecks = false;
        Invoke("Awake"); Lifecycle.Initialize();
    }
    public void Dispose()
    {
        NetManager.StopThrows = false;
        Lifecycle.Handle(Lifecycle.Event.Stop, server.Stop);
        Invoke("OnDestroy"); Session.Reset();
    }
    private void Start() => Lifecycle.Handle(Lifecycle.Event.Start, server.Start);
    private void Stop() => Lifecycle.Handle(Lifecycle.Event.Stop, server.Stop);
    private static byte[] RequestData(ulong id = 20, string magic = "ONI_TOGETHER")
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(magic); writer.Write(id); return stream.ToArray();
    }
    private NetPeer Connect(ulong id = 20, int peerId = 1, int port = 0)
    {
        var peer = new NetPeer(peerId, port);
        var request = new ConnectionRequest(peer, RequestData(id));
        NetManager.Last.Events.Enqueue(() => NetManager.Last.Listener.Request(request));
        server.Poll();
        Assert.False(request.Rejected);
        Assert.True(Peers.Contains(peer));
        return peer;
    }
    private void Receive(NetPeer peer, byte[] bytes)
    {
        NetManager.Last.Events.Enqueue(() => NetManager.Last.Listener.Receive(peer, bytes));
        server.Poll();
    }
    private void Hello(NetPeer peer, ulong id = 20, string name = " LAN player ") =>
        Receive(peer, PacketSerialization.Serialize(SessionHandshakePacket.CreateClientHello(id, name)));

    [Fact]
    public void StartupPollShutdownAndRestartOwnNativeResources()
    {
        Start();
        var native = NetManager.Last;
        Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
        Assert.Equal(7777, native.Port);
        Assert.True(native.AutoRecycle); Assert.False(native.UnsyncedEvents);
        Assert.Equal(30000, native.DisconnectTimeout); Assert.Equal(4, native.ChannelsCount);
        var peer = Connect(); Hello(peer);
        Stop();
        Assert.False(native.IsRunning); Assert.Equal(1, native.StopCalls);
        Assert.Empty(Peers); Assert.False(server.TryGetConnection(20, out _));
        Assert.Empty(Session.Players); Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        // Old callbacks are detached even if a caller retains the old listener.
        Start();
        var request = new ConnectionRequest(new NetPeer(2), RequestData(30));
        native.Listener.Request(request);
        Assert.Empty(Peers);
        var current = Connect(); Hello(current);
        Assert.True(server.TryGetConnection(20, out var registered)); Assert.Same(current, registered);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeStartupFailureReleasesPartialManager(bool throws)
    {
        NetManager.StartSucceeds = false; NetManager.StartThrows = throws;
        Start();
        Assert.Equal(1, NetManager.Last.StopCalls);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.False(Session.IsActive); Assert.Empty(Peers);
    }

    [Fact]
    public void InvalidPortDoesNotCreateManager()
    {
        ModConfiguration.Instance.Host.LanPort = 0;
        Start();
        Assert.Null(NetManager.Last);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
    }

    [Fact]
    public void PeerIsPhysicalOnlyUntilMatchingHelloAndWelcomeUsesExistingFormat()
    {
        Start(); var peer = Connect();
        Assert.False(server.TryGetConnection(20, out _)); Assert.Single(Session.Players);
        Hello(peer);
        Assert.True(server.TryGetConnection(20, out var registered)); Assert.Same(peer, registered);
        Assert.Equal("LAN player", Session.Players[20].UserName);
        using var reader = new BinaryReader(new MemoryStream(Assert.Single(peer.Sent)));
        Assert.Equal(PacketRegistry.GetPacketId(new SessionHandshakePacket()), reader.ReadInt32());
        var welcome = new SessionHandshakePacket(); welcome.Deserialize(reader);
        Assert.Equal(SessionHandshakeMessage.Welcome, welcome.Message);
        Assert.Equal(20UL, welcome.PlayerId); Assert.Equal(Session.HostPlayerId, welcome.HostPlayerId);
        Hello(peer); // Repeated hello does not repeat admission or dispatch gameplay.
        Assert.Single(peer.Sent);
    }

    [Theory]
    [InlineData(0UL, "Name")]
    [InlineData(30UL, "Name")]
    [InlineData(20UL, "")]
    [InlineData(20UL, "bad\nname")]
    public void InvalidOrMismatchedIdentityCannotAdmit(ulong id, string name)
    {
        Start(); var peer = Connect(); Hello(peer, id, name);
        Assert.Equal(ConnectionState.ShutdownRequested, peer.ConnectionState);
        Assert.Single(Session.Players); Assert.False(server.TryGetConnection(20, out _));
    }

    [Fact]
    public void WrongPacketTypeIsRejectedWithoutParsingItsBodyAsIdentity()
    {
        Start(); var peer = Connect();
        byte[] bytes = PacketSerialization.Serialize(SessionHandshakePacket.CreateClientHello(20, "Valid"));
        BitConverter.GetBytes(999).CopyTo(bytes, 0);
        Receive(peer, bytes);
        Assert.Equal(ConnectionState.ShutdownRequested, peer.ConnectionState);
        Assert.False(server.TryGetConnection(20, out _)); Assert.Single(Session.Players);
    }

    [Fact]
    public void TruncatedAndWrongHandshakeMessageAreRejected()
    {
        Start(); var peer = Connect();
        Receive(peer, new byte[] { 123 });
        Assert.Equal(ConnectionState.ShutdownRequested, peer.ConnectionState);
        var second = Connect(30, 2);
        var welcome = SessionHandshakePacket.CreateWelcome(30, 10, "Host");
        Receive(second, PacketSerialization.Serialize(welcome));
        Assert.Equal(ConnectionState.ShutdownRequested, second.ConnectionState);
        Assert.Single(Session.Players);
    }

    [Fact]
    public void DuplicateAndStaleDisconnectionsPreserveCurrentAssociation()
    {
        Start(); var original = Connect(); Hello(original);
        var member = Session.Players[20];
        var duplicate = Connect(peerId: 2, port: original.Port);
        Assert.True(original.Equals(duplicate)); Assert.NotSame(original, duplicate);
        Assert.Equal(2, Peers.Count);
        Hello(duplicate);
        Assert.Equal(ConnectionState.ShutdownRequested, duplicate.ConnectionState);
        NetManager.Last.Listener.Disconnect(duplicate);
        Assert.Same(member, Session.Players[20]);
        Assert.True(server.TryGetConnection(20, out var current)); Assert.Same(original, current);
        Assert.True(Peers.Contains(original)); Assert.False(Peers.Contains(duplicate));
        NetManager.Last.Listener.Disconnect(original);
        Assert.False(server.TryGetConnection(20, out _)); Assert.False(Session.Players.ContainsKey(20));
        var replacement = Connect(peerId: 1); Hello(replacement);
        Assert.True(original.Equals(replacement)); Assert.NotSame(original, replacement);
        var replacementMember = Session.Players[20];
        NetManager.Last.Listener.Disconnect(original);
        Hello(original); // A stale receive callback cannot affect the replacement.
        Assert.True(server.TryGetConnection(20, out current)); Assert.Same(replacement, current);
        Assert.True(Peers.Contains(replacement)); Assert.Single(Peers);
        Assert.Same(replacementMember, Session.Players[20]);
    }

    [Fact]
    public void SameEndpointPeersKeepSeparatePhysicalIdentityAndRequestIds()
    {
        Start();
        var first = Connect(id: 20, peerId: 1, port: 43000);
        var second = Connect(id: 30, peerId: 2, port: 43000);
        Assert.True(first.Equals(second)); Assert.NotSame(first, second);
        Assert.Equal(2, Peers.Count);
        Assert.Equal(20UL, Peers[first]); Assert.Equal(30UL, Peers[second]);

        NetManager.Last.Listener.Disconnect(first);
        Assert.False(Peers.Contains(first)); Assert.True(Peers.Contains(second));
        Hello(second, id: 30);
        var member = Session.Players[30];
        NetManager.Last.Listener.Disconnect(first);
        Assert.True(server.TryGetConnection(30, out var current)); Assert.Same(second, current);
        Assert.Same(member, Session.Players[30]); Assert.Single(Peers);
    }

    [Fact]
    public void DisconnectBeforeAdmissionOnlyReleasesThatPhysicalPeer()
    {
        Start(); var peer = Connect();
        NetManager.Last.Listener.Disconnect(peer);
        Assert.Empty(Peers); Assert.Single(Session.Players);
        Hello(peer);
        Assert.False(server.TryGetConnection(20, out _));
    }

    [Fact]
    public void FailedStopRetainsOwnerForExplicitRetryWithoutAdmittedAssociation()
    {
        Start(); var peer = Connect(); Hello(peer);
        NetManager.StopThrows = true; Stop();
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.True(Peers.Contains(peer)); Assert.False(server.TryGetConnection(20, out _));
        NetManager.StopThrows = false; Stop();
        Assert.Empty(Peers); Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
    }

    [Fact]
    public void WelcomeSendFailureRemovesAdmissionAndDisconnectsPeer()
    {
        Start(); var peer = Connect(); peer.SendFails = true; Hello(peer);
        Assert.False(server.TryGetConnection(20, out _)); Assert.False(Session.Players.ContainsKey(20));
        Assert.Equal(ConnectionState.ShutdownRequested, peer.ConnectionState);
    }

    [Fact]
    public void MalformedRequestIsRejectedBeforePeerOwnership()
    {
        Start();
        foreach (var data in new[] { RequestData(0), RequestData(magic: "Wrong"), new byte[] { 1 } })
        {
            var request = new ConnectionRequest(new NetPeer(1), data);
            NetManager.Last.Listener.Request(request);
            Assert.True(request.Rejected);
        }
        Assert.Empty(Peers); Assert.Single(Session.Players);
    }
}
