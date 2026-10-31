#nullable disable
using System.Reflection;
using ONI_United.Networking;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transport;
using ONI_United.Core;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[CollectionDefinition("Lifecycle", DisableParallelization = true)]
public sealed class LifecycleCollection { }

[Collection("Lifecycle")]
public sealed class LifecycleTests : IDisposable
{
    private readonly Lifecycle machine = new();
    private readonly TestTransport server = new();
    private static void Invoke(Lifecycle target, string method) => typeof(Lifecycle)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    public LifecycleTests()
    {
        Reset();
        NetworkRuntime.TransportServer = server;
        Invoke(machine, "Awake");
    }
    public void Dispose()
    {
        Clearing = null;
        Invoke(machine, "OnDestroy");
        Session.Reset();
    }
    private void Initialize() => Lifecycle.Initialize();

    public static IEnumerable<object[]> Permissions()
    {
        foreach (var phase in Enum.GetValues<Lifecycle.Phase>())
        foreach (var evt in Enum.GetValues<Lifecycle.Event>())
        {
            Lifecycle.Phase? destination = (phase, evt) switch
            {
                (Lifecycle.Phase.Idle, Lifecycle.Event.Start) => Lifecycle.Phase.StartingHost,
                (Lifecycle.Phase.StartingHost, Lifecycle.Event.TransportStarted) => Lifecycle.Phase.PrepareSession,
                (Lifecycle.Phase.PrepareSession, Lifecycle.Event.SessionPrepared) => Lifecycle.Phase.Hosting,
                (Lifecycle.Phase.StartingHost or Lifecycle.Phase.PrepareSession or Lifecycle.Phase.Hosting or Lifecycle.Phase.Stopping,
                    Lifecycle.Event.Stop) => Lifecycle.Phase.Stopping,
                (Lifecycle.Phase.Stopping, Lifecycle.Event.Stopped) => Lifecycle.Phase.Idle,
                _ => null
            };
            yield return new object[] { (int)phase, (int)evt, destination.HasValue ? (int)destination.Value : -1 };
        }
    }

    [Theory]
    [MemberData(nameof(Permissions))]
    public void CompletePermissionTable(int phaseValue, int eventValue, int destinationValue)
    {
        var phase = (Lifecycle.Phase)phaseValue;
        var evt = (Lifecycle.Event)eventValue;
        typeof(Lifecycle).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(machine, phase);
        bool called = false;
        bool admitted = Lifecycle.Handle(evt, () =>
        {
            called = true;
            Assert.Equal((Lifecycle.Phase)destinationValue, Lifecycle.Current);
        });
        Assert.Equal(destinationValue >= 0, admitted);
        Assert.Equal(admitted, called);
        Assert.Equal(destinationValue >= 0 ? (Lifecycle.Phase)destinationValue : phase, Lifecycle.Current);
        if (!admitted) Assert.Empty(Trace);
    }

    [Fact]
    public void ImmediateReadinessCanCompleteInsideStart()
    {
        Initialize(); server.SynchronousReady = true;
        bool ready = false;
        Assert.True(server.Start(() => ready = true));
        Assert.True(ready);
        Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
        Assert.True(Session.IsHost);
    }

    [Fact]
    public void UninitializedRejectsEveryEventWithoutActions()
    {
        foreach (var evt in Enum.GetValues<Lifecycle.Event>())
            Assert.False(Lifecycle.Handle(evt, () => throw new Exception("rejected callback")));
        Assert.Empty(Trace);
        Assert.Equal(Lifecycle.Phase.Uninitialized, Lifecycle.Current);
    }

    [Fact]
    public void SharedActionsPrecedeCallbacksAndReadinessIsNotStartAcceptance()
    {
        Initialize();
        Assert.True(server.Start(() =>
        {
            Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
            Assert.True(Session.IsHost);
            Trace.Add("ready");
        }));
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.False(server.Start());
        server.Ready();
        Assert.Equal(new[] { "[Lifecycle] Idle", "[Lifecycle] StartingHost", "identity:start", "color:start",
            "tick:start", "protocol:start", "[Lifecycle] PrepareSession", "session:start",
            "[Lifecycle] Hosting", "ready" }, Trace);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.TransportStarted));
    }

    [Fact]
    public void NestedStopDuringRegistrationRejectsLaterSessionPrepared()
    {
        Initialize(); server.Start();
        Registered = () => server.Stop();
        server.Ready();
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.SessionPrepared, () => throw new Exception()));
        Assert.Equal(1, server.Cancellations);
    }

    [Fact]
    public void FailedStartEventActionSkipsProtocolExecution()
    {
        Initialize();
        ONI_United.Networking.Runtime.GameClient.State = ONI_United.Networking.Sessions.States.ClientState.Connecting;
        Assert.Throws<InvalidOperationException>(() => server.Start());
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.DoesNotContain("protocol:start", Trace);
    }

    [Fact]
    public void DuplicateComponentCannotDisposeLiveOwner()
    {
        Initialize(); server.Start();
        NetworkRuntime.Instance = new NetworkRuntime();
        var duplicate = new Lifecycle();
        Invoke(duplicate, "Awake");
        Invoke(duplicate, "OnDestroy");
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.Equal(0, server.Disposals);
        Assert.DoesNotContain("runtime:lifetime", Trace);
    }

    [Fact]
    public void FailedEssentialActionSkipsCallbackLogsAndDoesNotRollback()
    {
        Initialize(); server.Start(); RegistrationSucceeds = false;
        Assert.Throws<InvalidOperationException>(() => server.Ready());
        Assert.Equal(Lifecycle.Phase.PrepareSession, Lifecycle.Current);
        Assert.Contains(Trace, line => line.Contains("Event TransportStarted failed in phase PrepareSession"));
        Assert.DoesNotContain("[Lifecycle] Hosting", Trace);
    }

    [Fact]
    public void CallbackExceptionPropagatesAfterTransition()
    {
        Initialize();
        var error = new Exception("callback failure");
        Assert.Same(error, Assert.Throws<Exception>(() => Lifecycle.Handle(Lifecycle.Event.Start, () => throw error)));
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
    }

    [Fact]
    public void RepeatedStopRetainsOriginalLoadAndWaitsForConfirmedCleanup()
    {
        Initialize(); server.Start(); server.Ready();
        Assert.True(server.StopAndLoad("first.sav"));
        Assert.False(Session.IsActive);
        Assert.True(Trace.IndexOf("session:stop") < Trace.IndexOf("protocol:cancel"));
        Assert.True(server.StopAndLoad("second.sav"));
        Assert.Equal(1, server.Cancellations);
        Assert.Empty(LoadScreen.Loaded);
        Assert.False(server.Start());
        server.Complete(true, null);
        Assert.Equal(("first.sav", Lifecycle.Phase.Idle), Assert.Single(LoadScreen.Loaded));
        Assert.Single(Trace, line => line == "[Lifecycle] Stopping");
    }

    [Fact]
    public void CleanupFailureWithEmptyDiagnosticDiscardsLoadAndRequiresExplicitRetry()
    {
        Initialize(); server.Start(); server.StopAndLoad("discard.sav");
        server.Complete(false, "");
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.False(server.Start());
        Assert.False(string.IsNullOrEmpty(server.OperationError));
        Assert.Empty(LoadScreen.Loaded);
        Assert.Equal(1, server.Cancellations);
        server.Stop(); server.Complete(true, null);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Empty(LoadScreen.Loaded);
    }

    [Fact]
    public void EssentialCleanupExceptionCannotBeOverriddenByEarlierNativeSuccess()
    {
        Initialize(); server.Start(); server.StopAndLoad("discard.sav");
        Clearing = () => throw new Exception("shared cleanup failure");
        Assert.Throws<Exception>(() => server.Stop());
        server.Complete(true, null);
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.Empty(LoadScreen.Loaded);
    }

    [Fact]
    public void StartupFailureUsesStopAndNeverHosts()
    {
        Initialize(); server.Start(); server.FailStart();
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        server.Ready(); // A late protocol readiness event has no registration side effects.
        Assert.False(Session.IsActive);
        Assert.DoesNotContain("session:start", Trace);
        server.Complete(true, null);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
    }

    [Fact]
    public void DuplicateCompletionCannotStopLaterAttempt()
    {
        Initialize(); server.Start(); server.Stop();
        var oldCompletion = server.Completion;
        server.Complete(true, null);
        server.Start(); server.Ready();
        oldCompletion(true, null);
        Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
    }

    [Fact]
    public void EssentialLifetimeCleanupAndTransportDisposalRemainRepeatSafe()
    {
        Initialize(); server.Start(); server.StopAndLoad("discard.sav");
        var lateCompletion = server.Completion;
        var phase = Lifecycle.Current;
        int idleEntries = Trace.Count(line => line == "[Lifecycle] Idle");
        // Exercise the real cleanup/disposal boundaries; not the substituted runtime's hooks.
        Lifecycle.CleanupHostForLifetime();
        server.DisposeHost();
        Assert.Equal(1, server.Disposals);
        lateCompletion(true, null);
        Assert.Empty(LoadScreen.Loaded);
        Lifecycle.CleanupHostForLifetime();
        server.DisposeHost();
        Assert.Equal(2, server.Disposals);
        Assert.Equal(phase, Lifecycle.Current);
        Assert.Equal(idleEntries, Trace.Count(line => line == "[Lifecycle] Idle"));
    }

    [Fact]
    public void OwningDestructionHandsOffBeforeClearingLifecycle()
    {
        Initialize(); server.Start();
        NetworkRuntime.Instance = new NetworkRuntime();
        int handoffs = 0;
        RuntimeLifetimeRequested = () =>
        {
            handoffs++;
            Assert.True(Lifecycle.RequiresHostLifetimeCleanup);
            Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        };
        Invoke(machine, "OnDestroy");
        Assert.Equal(1, handoffs);
        Assert.False(Lifecycle.RequiresHostLifetimeCleanup);
    }

    [Fact]
    public void DestructionClearsLifecycleWhenRuntimeHandoffThrows()
    {
        Initialize(); server.Start();
        NetworkRuntime.Instance = new NetworkRuntime();
        var error = new Exception("lifetime handoff failure");
        RuntimeLifetimeRequested = () => throw error;
        var thrown = Assert.Throws<TargetInvocationException>(() => Invoke(machine, "OnDestroy"));
        Assert.Same(error, thrown.InnerException);
        Assert.False(Lifecycle.RequiresHostLifetimeCleanup);
    }

    [Fact]
    public void DestructionAfterRuntimeOwnerClearedDoesNotRequireRuntime()
    {
        Initialize(); server.Start();
        NetworkRuntime.Instance = null;
        Invoke(machine, "OnDestroy");
        Assert.False(Lifecycle.RequiresHostLifetimeCleanup);
        Assert.DoesNotContain("runtime:lifetime", Trace);
    }

    [Fact]
    public void EssentialLifetimeCleanupFailureDoesNotChangePhase()
    {
        Initialize(); server.Start(); server.Ready();
        var error = new Exception("essential lifetime cleanup failure");
        Clearing = () => throw error;
        Assert.Same(error, Assert.Throws<Exception>(() => Lifecycle.CleanupHostForLifetime()));
        Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
    }

    private sealed class TestTransport : TransportServer
    {
        public Action<bool, string> Completion;
        public int Cancellations, Disposals;
        public bool SynchronousReady;
        protected override void StartProtocol()
        {
            Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
            Trace.Add("protocol:start");
            if (SynchronousReady) Ready();
        }
        public void Ready() => ReportTransportStarted();
        public void FailStart() => ReportStartupFailure("native startup failure");
        protected override void CancelProtocol(Action<bool, string> completed)
        {
            if (Completion != null) return;
            Assert.False(Session.IsActive);
            Trace.Add("protocol:cancel"); Cancellations++; Completion = completed;
        }
        public void Complete(bool success, string error)
        {
            var completion = Completion; Completion = null; completion(success, error);
        }
        protected override void DisposeProtocol() { Disposals++; Completion = null; }
        public override void CloseConnections() { }
        public override bool TryGetRemotePlayerId(object connection, out ulong id) { id = 0; return false; }
        public override void DisconnectConnection(object connection, string reason) { }
        public override void Update() { }
        public override void OnMessageRecieved() { }
        public override void KickClient(ulong id) { }
    }
}
