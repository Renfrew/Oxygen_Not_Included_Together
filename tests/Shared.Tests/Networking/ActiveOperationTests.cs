#nullable disable
using System.Reflection;
using ONI_United.Core;
using ONI_United.Misc.World;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transport;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[Collection("Lifecycle")]
public sealed class ActiveOperationTests : IDisposable
{
    private readonly Lifecycle machine = new();
    private readonly HostTransport host = new();

    public ActiveOperationTests()
    {
        Reset();
        NetworkRuntime.TransportServer = host;
        Invoke("Awake");
        Lifecycle.Initialize();
    }
    private void Invoke(string name) => typeof(Lifecycle).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(machine, null);
    public void Dispose() { Registered = Clearing = PreparingClient = null; Session.Reset(); Invoke("OnDestroy"); }

    private static void JoinSession()
    {
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        NetworkRuntime.TransportEstablished(NetworkRuntime.TransportClient, NetworkRuntime.ActiveOperation);
        Assert.True(Session.AdmitClient(new Player(20), new Player(10)));
        Assert.True(NetworkRuntime.ClientAdmitted(NetworkRuntime.TransportClient, NetworkRuntime.ActiveOperation));
    }

    [Fact]
    public void HostIdentityPrecedesNativeWorkAndSurvivesEveryOwnedPhase()
    {
        NetworkRuntime.StartServer();
        var token = NetworkRuntime.ActiveOperation;
        Assert.Same(token, host.Attempts.Single());
        Assert.Same(host, NetworkRuntime.OperationTransport);
        Registered = () =>
        {
            Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
            Assert.Same(token, NetworkRuntime.ActiveOperation);
            Assert.False(Lifecycle.Handle(Lifecycle.Event.ClientAdmitted));
        };
        host.Ready(token);
        Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
        Assert.True(Lifecycle.IsHostGameplay);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.StartClientSync));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.StartHostSync));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.CompleteSync));
        host.CleanupSucceeds = false;
        NetworkRuntime.StopHost();
        Assert.Same(token, NetworkRuntime.ActiveOperation);
        Assert.False(Session.IsActive);
        Assert.False(Lifecycle.CanSelectTransport);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.StartHost));
        Assert.False(Lifecycle.Handle(Lifecycle.Event.Join));
        int clears = Trace.Count(x => x == "session:stop");
        host.CleanupSucceeds = true;
        NetworkRuntime.StopHost();
        Assert.Equal(clears + 1, Trace.Count(x => x == "session:stop"));
        Assert.Null(NetworkRuntime.ActiveOperation);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
    }

    [Fact]
    public void SameHostRestartInsideRegistrationRejectsOldReadinessFailureAndAdmissionContinuation()
    {
        NetworkRuntime.StartServer();
        var old = NetworkRuntime.ActiveOperation;
        Registered = () =>
        {
            Registered = () => Assert.False(NetworkRuntime.HostSessionReady(host, old));
            NetworkRuntime.StopHost();
            NetworkRuntime.StartServer();
            host.Ready(NetworkRuntime.ActiveOperation);
            Registered = null;
        };
        host.Ready(old);
        Assert.NotSame(old, NetworkRuntime.ActiveOperation);
        Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
        Assert.Equal(1, host.Admissions);
        host.Failed(old);
        Assert.Equal(1, host.CleanupAttempts);
        Assert.Null(host.OperationError);
    }

    [Fact]
    public void StaleNativeOutcomesCannotRegisterOrStopReusedHostTransport()
    {
        NetworkRuntime.StartServer();
        var old = NetworkRuntime.ActiveOperation;
        NetworkRuntime.StopHost();
        NetworkRuntime.StartServer();
        var current = NetworkRuntime.ActiveOperation;
        host.Ready(old);
        host.Failed(old);
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.Same(current, NetworkRuntime.ActiveOperation);
        host.Ready(current);
        Assert.True(Session.IsHost);
    }

    [Fact]
    public void ClientPreparationRejectsHostReadinessAndRoutesClientCleanupBeforeMembership()
    {
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        var client = NetworkRuntime.TransportClient;
        var token = client.Operation;
        Assert.Same(token, NetworkRuntime.ActiveOperation);
        NetworkRuntime.TransportEstablished(client, token);
        Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.HostSessionReady));
        NetworkRuntime.Stop();
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Same(token, client.Operation);
        Assert.False(NetworkRuntime.IsCurrentOperation(client, token));
        Assert.False(NetworkRuntime.HasClientOperation);
    }

    [Fact]
    public void SameClientRestartDuringPreparationRejectsOldEstablishmentAndAdmission()
    {
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        var client = NetworkRuntime.TransportClient;
        var old = NetworkRuntime.ActiveOperation;
        PreparingClient = () =>
        {
            PreparingClient = null;
            NetworkRuntime.DisconnectClient();
            Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
            NetworkRuntime.TransportEstablished(client, NetworkRuntime.ActiveOperation);
        };
        NetworkRuntime.TransportEstablished(client, old);
        Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
        Assert.True(Session.AdmitClient(new Player(20), new Player(10)));
        Assert.False(NetworkRuntime.ClientAdmitted(client, old));
        Assert.True(NetworkRuntime.ClientAdmitted(client, NetworkRuntime.ActiveOperation));
    }

    [Fact]
    public void ClientCleanupFailureRetainsOwnerAndRepeatedClearInvokesResetAgain()
    {
        JoinSession();
        var client = Assert.IsType<NetworkRuntime.TestClient>(NetworkRuntime.TransportClient);
        var token = NetworkRuntime.ActiveOperation;
        client.CleanupSucceeds = false;
        NetworkRuntime.DisconnectClient();
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        Assert.Same(token, NetworkRuntime.ActiveOperation);
        Assert.False(Session.IsActive);
        int clears = Trace.Count(x => x == "session:stop");
        client.CleanupSucceeds = true;
        NetworkRuntime.DisconnectClient();
        Assert.Equal(2, client.CleanupAttempts);
        Assert.Equal(clears + 1, Trace.Count(x => x == "session:stop"));
        Assert.Null(NetworkRuntime.ActiveOperation);
    }

    [Fact]
    public void NestedExplicitStopAndRestartDuringLogicalNotificationPreservesNewHost()
    {
        NetworkRuntime.StartServer();
        host.Ready(NetworkRuntime.ActiveOperation);
        var old = NetworkRuntime.ActiveOperation;
        Clearing = () =>
        {
            Clearing = null;
            NetworkRuntime.StopHost();
            NetworkRuntime.StartServer();
            host.Ready(NetworkRuntime.ActiveOperation);
        };
        NetworkRuntime.StopHost();
        Assert.NotSame(old, NetworkRuntime.ActiveOperation);
        Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
        Assert.True(Session.IsHost);
        Assert.Equal(1, host.CleanupAttempts);
        Assert.Equal(2, Trace.Count(x => x == "session:stop"));
    }

    [Fact]
    public void ClientSynchronizationProducerStillRequiresWorldSpawnReadiness()
    {
        JoinSession();
        Assert.True(GameClient.BeginSynchronization(7));
        Assert.True(GameClient.SubmitSynchronizationSave(7, new WorldSave(), false));
        LoadedWorld = false;
        GameClient.OnWorldSpawnComplete();
        Assert.DoesNotContain("ready:Ready", Trace);
        Assert.False(GameClient.CompleteSynchronization(7));
        Assert.Equal(Lifecycle.Phase.Synchronizing, Lifecycle.Current);
    }

    [Fact]
    public void ClientLoadedWorldDoesNotGrantGameplayBeforeHostCompletion()
    {
        JoinSession();
        Assert.False(Lifecycle.Handle(Lifecycle.Event.StartHostSync));
        Assert.True(GameClient.BeginSynchronization(7));
        Assert.False(GameClient.CompleteSynchronization(7));
        Assert.True(GameClient.SubmitSynchronizationSave(7, new WorldSave(), false));
        GameClient.OnWorldSpawnComplete();
        Assert.Contains("ready:Ready", Trace);
        Assert.Equal(Lifecycle.Phase.Synchronizing, Lifecycle.Current);
        Assert.True(GameClient.CompleteSynchronization(7));
        Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
        Assert.True(Session.IsClient);
        Assert.False(Lifecycle.IsHostGameplay);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.StartHostSync));
        Assert.True(GameClient.BeginSynchronization(8));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OldDelayedSaveCannotLoadWhenNewOperationReusesSynchronizationId(bool delay)
    {
        JoinSession();
        Assert.True(GameClient.BeginSynchronization(7));
        Assert.True(GameClient.SubmitSynchronizationSave(7, new WorldSave(), delay));
        var pending = delay ? null : ScheduledLoads.Single();
        var coroutine = delay ? Coroutines.Single() : null;
        NetworkRuntime.DisconnectClient();
        JoinSession();
        Assert.True(GameClient.BeginSynchronization(7));
        if (delay)
        {
            Assert.False(coroutine.MoveNext());
            Assert.Empty(ScheduledLoads);
        }
        else Assert.False(pending());
        Assert.False(GameClient.CompleteSynchronization(7));
    }

    [Fact]
    public void CommonHostPrerequisiteRejectsNativeStartupWithoutLoadedWorld()
    {
        LoadedWorld = false;
        NetworkRuntime.StartServer();
        Assert.Empty(host.Attempts);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.Null(NetworkRuntime.ActiveOperation);
        Assert.Contains(Trace, x => x.Contains("Load a colony"));
    }

    [Fact]
    public void CapturedTransferCallbacksRejectNewOperationWithSameSynchronizationId()
    {
        JoinSession();
        Assert.True(GameClient.BeginSynchronization(9));
        var current = GameClient.CaptureSynchronization(9);
        Assert.True(current());
        NetworkRuntime.DisconnectClient();
        JoinSession();
        Assert.True(GameClient.BeginSynchronization(9));
        Assert.False(current());
        Assert.True(GameClient.CaptureSynchronization(9)());
    }

    [Fact]
    public void TerminalHostDisposalInvalidatesIdentityAndLateResults()
    {
        NetworkRuntime.StartServer();
        var token = NetworkRuntime.ActiveOperation;
        host.DisposeHost();
        Assert.Null(NetworkRuntime.ActiveOperation);
        host.Ready(token);
        host.Failed(token);
        Assert.False(Session.IsActive);
        Assert.False(Lifecycle.CanSelectTransport);
    }

    private sealed class HostTransport : TransportServer
    {
        public readonly List<object> Attempts = new();
        public bool CleanupSucceeds = true;
        public int Admissions, CleanupAttempts;
        public Action<bool, string> LastCompletion;
        protected override void StartProtocol(object operation)
        {
            Assert.True(NetworkRuntime.IsCurrentOperation(this, operation));
            Attempts.Add(operation);
        }
        public void Ready(object operation) => ReportTransportStarted(operation);
        public void Failed(object operation) => ReportStartupFailure(operation, "failed startup");
        protected override void CancelProtocol(Action<bool, string> completed)
        {
            CleanupAttempts++;
            LastCompletion = completed;
            completed(CleanupSucceeds, CleanupSucceeds ? null : "failed cleanup");
        }
        protected override void DisposeProtocol() { }
        public override void OnSessionPlayerAdmitted(ulong id) => Admissions++;
        public override void CloseConnections() { }
        public override bool TryGetRemotePlayerId(object connection, out ulong id) { id = 0; return false; }
        public override void DisconnectConnection(object connection, string reason) { }
        public override void Update() { }
        public override void OnMessageRecieved() { }
        public override void KickClient(ulong id) { }
    }
}
