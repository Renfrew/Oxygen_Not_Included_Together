#nullable disable
using System.Reflection;
using ONI_United.Core;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transport;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[Collection("Lifecycle")]
public sealed class ServerTransportTests : IDisposable
{
    private readonly Lifecycle machine = new();
    private readonly NativeServer server = new();
    private void Invoke(string method) => typeof(Lifecycle)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(machine, null);
    public ServerTransportTests() { Reset(); Invoke("Awake"); Lifecycle.Initialize(); }
    public void Dispose() { Clearing = null; Invoke("OnDestroy"); Session.Reset(); }
    private void Start() => Lifecycle.Handle(Lifecycle.Event.Start, server.Start);
    private void Stop() => Lifecycle.Handle(Lifecycle.Event.Stop, server.Stop);

    [Fact]
    public void ValidConnectionAdmitsLogicalPlayerWithoutGameplayReadiness()
    {
        Start(); server.Ready();

        var handle = new object();
        Assert.True(server.ConnectionEstablished(20, " Remote Steam user ", handle));
        Assert.True(server.TryGetConnection(20, out var registered));
        Assert.Same(handle, registered);

        var player = Assert.IsType<Player>(Session.Players[20]);
        Assert.Equal("Remote Steam user", player.UserName);
        Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
    }

    [Theory]
    [InlineData(0, "Remote")]
    [InlineData(10, "Host")]
    [InlineData(20, null)]
    [InlineData(20, "")]
    [InlineData(20, "   ")]
    [InlineData(20, "Bad\nName")]
    public void InvalidIdentityOrNameDoesNotAddPlayer(ulong id, string name)
    {
        Start(); server.Ready();

        Assert.False(server.ConnectionEstablished(id, name, new object()));
        Assert.Single(Session.Players);
        Assert.False(server.TryGetConnection(id, out _));
    }

    [Fact]
    public void DuplicateCapacityAndOversizedNameAreRejectedWithoutMutation()
    {
        Start(); server.Ready();
        Assert.False(server.ConnectionEstablished(20, new string('x', 65), new object()));
        Assert.True(server.ConnectionEstablished(20, "First", new object()));
        var original = Session.Players[20];
        Assert.False(server.ConnectionEstablished(20, "Replacement", new object()));
        Assert.Same(original, Session.Players[20]);

        NetworkRuntime.ServerCapacity = 2;
        Assert.False(server.ConnectionEstablished(30, "Over capacity", new object()));
        Assert.False(Session.Players.ContainsKey(30));
        Assert.False(server.TryGetConnection(30, out _));
    }

    [Fact]
    public void AdmissionDuringStartupOrStoppingIsRejected()
    {
        Start();
        Assert.False(server.ConnectionEstablished(20, "Remote", new object()));
        server.Ready(); Stop();
        Assert.False(server.ConnectionEstablished(20, "Remote", new object()));
        Assert.False(Session.IsActive);
        Assert.Empty(Session.Players);
    }

    [Fact]
    public void ClientSessionCannotAdmitRemotePlayers()
    {
        Start(); server.Ready();
        Session.SetForTests(false, true, 20, 10);

        Assert.False(server.ConnectionEstablished(30, "Remote", new object()));
        Assert.False(Session.Players.ContainsKey(30));
    }

    [Fact]
    public void ConnectionNotificationWithoutHostSessionDoesNotActivateSession()
    {
        Assert.False(server.ConnectionEstablished(20, "Remote player", new object()));

        Assert.Empty(Session.Players);
        Assert.False(Session.IsActive);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
    }

    [Fact]
    public void SuccessfulDisconnectRemovesMembershipAndExactAssociationOnlyOnce()
    {
        Start(); server.Ready();
        var handle = new object();
        Assert.True(server.ConnectionEstablished(20, "Remote", handle));

        Assert.True(server.Disconnected(20, handle));
        Assert.False(server.Disconnected(20, handle));
        Assert.False(server.Disconnected(Session.HostPlayerId, handle));
        Assert.False(server.TryGetConnection(20, out _));
        Assert.False(Session.Players.ContainsKey(20));
        Assert.Single(Session.Players);
        Assert.True(Session.Players.ContainsKey(Session.HostPlayerId));
    }

    [Fact]
    public void NullConnectionCannotCreateMembershipOrAssociation()
    {
        Start(); server.Ready();

        Assert.False(server.ConnectionEstablished(20, "Remote", null));

        Assert.Single(Session.Players);
        Assert.False(Session.Players.ContainsKey(20));
        Assert.False(server.TryGetConnection(20, out _));
    }

    [Fact]
    public void DuplicateAdmissionAndWrongHandleRemovalCannotReplaceMember()
    {
        Start(); server.Ready();
        var handle = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 41000);
        var duplicate = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 41000);
        Assert.True(handle.Equals(duplicate));
        Assert.NotSame(handle, duplicate);
        Assert.True(server.ConnectionEstablished(20, "Remote", handle));
        var member = Session.Players[20];

        Assert.False(server.ConnectionEstablished(20, "Duplicate", duplicate));
        Assert.False(server.ConnectionEstablished(20, "Repeated", handle));
        Assert.False(server.Disconnected(20, duplicate));
        Assert.False(server.Disconnected(20, null));

        Assert.True(server.TryGetConnection(20, out var registered));
        Assert.Same(handle, registered);
        Assert.Same(member, Session.Players[20]);
    }

    [Fact]
    public void StaleRemovalCannotRemoveNewAssociationForSameClientId()
    {
        Start(); server.Ready();
        var old = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 42000);
        var current = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 42000);
        Assert.True(old.Equals(current));
        Assert.NotSame(old, current);
        Assert.True(server.ConnectionEstablished(20, "First", old));
        Assert.True(server.Disconnected(20, old));
        Assert.False(server.TryGetConnection(20, out _));
        Assert.False(Session.Players.ContainsKey(20));

        Assert.True(server.ConnectionEstablished(20, "Reconnected", current));
        var member = Session.Players[20];
        Assert.False(server.Disconnected(20, old));
        Assert.True(server.TryGetConnection(20, out var registered));
        Assert.Same(current, registered);
        Assert.Same(member, Session.Players[20]);
    }

    [Fact]
    public void OnDisconnectMatchesSeparatelyBoxedEqualSteamHandles()
    {
        Start(); server.Ready();
        object admitted = new Steamworks.HSteamNetConnection(123);
        object sameHandle = new Steamworks.HSteamNetConnection(123);
        object otherHandle = new Steamworks.HSteamNetConnection(124);
        Assert.NotSame(admitted, sameHandle);
        Assert.True(server.ConnectionEstablished(20, "Steam player", admitted));

        Assert.False(server.Disconnected(20, otherHandle));
        Assert.True(server.TryGetConnection(20, out var current));
        Assert.Same(admitted, current);
        Assert.True(server.Disconnected(20, sameHandle));
        Assert.False(server.TryGetConnection(20, out _));
        Assert.False(Session.Players.ContainsKey(20));
        Assert.False(server.Disconnected(20, sameHandle));
    }

    [Fact]
    public void SessionAdmissionOwnsValidationAndConstruction()
    {
        Start(); server.Ready();
        Assert.False(Session.TryAdmitPlayer(20, "bad\nname"));
        Assert.False(Session.TryAdmitPlayer(20, new string('x', 65)));
        Assert.Single(Session.Players);
        Assert.True(Session.TryAdmitPlayer(20, new string('x', 64)));
        var original = Session.Players[20];
        Assert.False(Session.TryAdmitPlayer(20, "Duplicate"));
        Assert.Same(original, Session.Players[20]);
        NetworkRuntime.ServerCapacity = 2;
        Assert.False(Session.TryAdmitPlayer(30, "Over capacity"));
        Assert.Equal(2, Session.Players.Count);
    }

    [Fact]
    public void FailedSessionPreparationDoesNotCompleteStartup()
    {
        Start(); RegistrationSucceeds = false;

        Assert.Throws<InvalidOperationException>(() => server.Ready());

        Assert.Equal(Lifecycle.Phase.PrepareSession, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.DoesNotContain("[Lifecycle] Hosting", Trace);
        Assert.DoesNotContain("color:Hosting", Trace);
        server.Ready();
        Assert.Single(Trace, line => line == "session:start");
    }

    [Fact]
    public void StopDuringSessionPreparationCannotCompleteStartup()
    {
        Start(); Registered = Stop;

        server.Ready();

        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.DoesNotContain("color:Hosting", Trace);
        Assert.Single(Trace, line => line == "session:start");
    }

    [Fact]
    public void NativeReadinessAndPollingDoNotAdmitRemotePlayers()
    {
        Start(); server.Ready(); server.Poll();

        Assert.Single(Session.Players);
        Assert.True(Session.Players.ContainsKey(Session.LocalPlayerId));
        Assert.False(Session.Players.ContainsKey(20));
    }

    [Fact]
    public void PollServicesNativeWorkWithoutCompletingLifecycleOperations()
    {
        ServerTransport transport = server;
        Start();
        transport.Poll();
        Assert.Equal(1, server.Polls);
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.False(Session.IsActive);

        server.Ready();
        transport.Poll();
        Assert.Equal(2, server.Polls);
        Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
        Assert.True(Session.IsHost);

        Stop();
        transport.Poll();
        Assert.Equal(3, server.Polls);
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.False(Session.IsActive);
    }

    [Fact]
    public void StartInvokesNativeStartWithoutReportingReadiness()
    {
        Start();
        Assert.Equal(1, server.Starts);
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.False(Session.IsActive);
    }

    [Fact]
    public void RepeatedStopIsForwardedForProtocolOwnedCoalescing()
    {
        Start(); Stop(); Stop();
        Assert.Equal(2, server.Stops);
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.False(Session.IsActive);
    }

    [Fact]
    public void NativeReadinessPreparesSessionBeforeHosting()
    {
        Start(); server.Ready();
        Assert.True(Session.IsHost);
        Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
        Assert.True(Trace.IndexOf("session:start") < Trace.LastIndexOf("[Lifecycle] Hosting"));
    }

    [Fact]
    public void ShutdownReportEntersIdleAfterStop()
    {
        Start(); server.Ready(); Stop();
        Assert.False(Session.IsActive);
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        server.Stopped();
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
    }

    [Fact]
    public void ConfirmedStopPermitsRestartOnTheSameInstance()
    {
        Start(); server.Ready(); Stop(); server.Stopped();
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.False(Session.IsActive);

        Start();
        Assert.Equal(2, server.Starts);
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.False(Session.IsActive);
        server.Ready();
        Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
        Assert.True(Session.IsHost);
        Stop(); server.Stopped();
        Assert.Equal(2, server.Stops);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.False(Session.IsActive);
    }

    [Fact]
    public void StartupFailureRequestsCleanupWithoutReportingReadiness()
    {
        Start(); server.StartFailed("native failure");
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.False(Session.IsActive); Assert.Equal(1, server.Stops);
        Assert.Contains("[ServerTransport] native failure", Trace);
        Assert.DoesNotContain("session:start", Trace);
    }

    [Fact]
    public void StopFailureDoesNotCompleteOrAutomaticallyRetry()
    {
        Start(); Stop(); server.StopFailed("");
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.Equal(1, server.Stops);
        Assert.Contains("[ServerTransport] Host cleanup failed.", Trace);
        Stop(); Assert.Equal(2, server.Stops);
        server.Stopped();
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
    }

    [Fact]
    public void LifecycleRejectsCompletionReportsInInvalidPhases()
    {
        server.Ready(); server.Stopped();
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Start(); server.Ready(); server.Stopped();
        Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
        Stop(); server.Ready();
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.False(Session.IsActive);
    }

    private sealed class NativeServer : ServerTransport
    {
        public int Polls;
        public int Starts;
        public int Stops;
        protected override void InternalPoll() => Polls++;
        protected override void InternalStart() => Starts++;
        protected override void InternalStop() => Stops++;
        public bool ConnectionEstablished(ulong clientId, string playerName, object connection) =>
            OnConnected(clientId, playerName, connection);
        public bool Disconnected(ulong clientId, object connection) => OnDisconnect(clientId, connection);
        public void Ready() => OnStarted();
        public void Stopped() => OnStopped();
        public void StartFailed(string reason) => OnStartFailed(reason);
        public void StopFailed(string reason) => OnStopFailed(reason);
    }
}
