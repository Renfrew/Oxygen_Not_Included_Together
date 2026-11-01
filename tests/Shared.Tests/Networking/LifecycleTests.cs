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

    // Exercises the production dispatcher/transport contract, not NetworkRuntime routing.
    private bool Start() => Lifecycle.Handle(Lifecycle.Event.Start, server.Start);
    // Stop admission uses the runtime substitute; transport completion remains production code.
    private void Stop() => NetworkRuntime.Stop();

    public static IEnumerable<object[]> Permissions()
    {
        foreach (var phase in Enum.GetValues<Lifecycle.Phase>())
        foreach (var evt in Enum.GetValues<Lifecycle.Event>())
        {
            Lifecycle.Phase? destination = (phase, evt) switch
            {
                (Lifecycle.Phase.Idle, Lifecycle.Event.Start) => Lifecycle.Phase.StartingHost,
                (Lifecycle.Phase.StartingHost, Lifecycle.Event.TransportStarted) => Lifecycle.Phase.Hosting,
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
        Assert.True(Start());
        server.Ready(); // Duplicate readiness must not repeat successful-host actions.
        Assert.False(Lifecycle.Handle(Lifecycle.Event.SessionPrepared, () => throw new Exception("duplicate completion")));
        Assert.Single(Trace, line => line == "color:Hosting");
        Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
        Assert.Single(Trace, line => line == "protocol:admitted");
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
        Assert.True(Start());
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.False(Start());
        Assert.Single(Trace, line => line == "protocol:start");
        Assert.DoesNotContain("color:Hosting", Trace);
        Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
        server.Ready();
        Assert.Equal(new[] { "[Lifecycle] Idle", "[Lifecycle] StartingHost", "identity:start", "color:start",
            "tick:start", "protocol:start", "[Lifecycle] PrepareSession", "session:start",
            "[Lifecycle] Hosting", "color:Hosting", "protocol:admitted" }, Trace);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.TransportStarted, () => throw new Exception("rejected readiness callback")));
    }

    [Fact]
    public void TransportStartedActionPreparesSessionAndCompletesWithoutTransportCallback()
    {
        Initialize(); Start();
        Registered = () => Assert.Equal(Lifecycle.Phase.PrepareSession, Lifecycle.Current);

        Assert.True(Lifecycle.Handle(Lifecycle.Event.TransportStarted, null));

        Assert.True(Session.IsHost);
        Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.TransportStarted, null));
        Assert.Single(Trace, line => line == "session:start");
        Assert.Single(Trace, line => line == "color:Hosting");
    }

    [Fact]
    public void NestedStopDuringRegistrationRejectsLaterSessionPrepared()
    {
        Initialize(); Start();
        Registered = () => Stop();
        server.Ready();
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.SessionPrepared, () => throw new Exception()));
        Assert.Equal(1, server.Cancellations);
        Assert.DoesNotContain("color:Hosting", Trace);
        Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
    }

    [Fact]
    public void FailedStartEventActionSkipsProtocolExecution()
    {
        Initialize();
        ONI_United.Networking.Runtime.GameClient.State = ONI_United.Networking.Sessions.States.ClientState.Connecting;
        Assert.Throws<InvalidOperationException>(() => Start());
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.DoesNotContain("protocol:start", Trace);
        Assert.DoesNotContain("color:Hosting", Trace);
        Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
    }

    [Fact]
    public void DuplicateComponentCannotDisposeLiveOwner()
    {
        Initialize(); Start();
        var duplicate = new Lifecycle();
        Invoke(duplicate, "Awake");
        Invoke(duplicate, "OnDestroy");
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.Equal(0, server.Disposals);
    }

    [Fact]
    public void FailedEssentialActionSkipsCallbackLogsAndDoesNotRollback()
    {
        Initialize(); Start(); RegistrationSucceeds = false;
        Assert.Throws<InvalidOperationException>(() => server.Ready());
        Assert.Equal(Lifecycle.Phase.PrepareSession, Lifecycle.Current);
        Assert.Contains(Trace, line => line.Contains("Event TransportStarted failed in phase PrepareSession"));
        Assert.DoesNotContain("[Lifecycle] Hosting", Trace);
        Assert.DoesNotContain("color:Hosting", Trace);
        Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
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
    public void RepeatedStopRetainsCompletionAndWaitsForConfirmedCleanup()
    {
        Initialize(); Start(); server.Ready();
        Stop();
        Assert.False(Session.IsActive);
        Assert.True(Trace.IndexOf("session:stop") < Trace.IndexOf("protocol:cancel"));
        var completion = server.Completion;
        Stop();
        Assert.Same(completion, server.Completion);
        Assert.Equal(1, server.Cancellations);
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.False(Start());
        server.Complete(true, null);
        completion(true, null); // Duplicate completion must not enter Idle again.
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Single(Trace, line => line == "[Lifecycle] Stopping");
        Assert.Equal(2, Trace.Count(line => line == "[Lifecycle] Idle"));
    }

    [Fact]
    public void RejectedStopEventDoesNotRunTransport()
    {
        Initialize();
        Assert.False(Lifecycle.Handle(Lifecycle.Event.Stop, server.Stop));
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Equal(0, server.Cancellations);
        Assert.Null(server.Completion);
        Assert.Equal(new[] { "[Lifecycle] Idle" }, Trace);
    }

    [Fact]
    public void SynchronousCancellationEntersIdleOnlyAfterConfirmation()
    {
        Initialize(); Start(); server.Ready(); server.SynchronousCancellation = true;
        Stop();
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.Equal(1, server.Cancellations);
        Assert.True(Trace.IndexOf("protocol:cancel") < Trace.LastIndexOf("[Lifecycle] Idle"));
    }

    [Fact]
    public void CleanupFailureWithEmptyDiagnosticRequiresExplicitRetry()
    {
        Initialize(); Start(); Stop();
        server.Complete(false, "");
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.False(Start());
        Assert.False(string.IsNullOrEmpty(server.OperationError));
        Assert.Equal(1, server.Cancellations);
        Stop();
        Assert.Equal(2, server.Cancellations);
        server.Complete(true, null);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Null(server.OperationError);
    }

    [Fact]
    public void StartupFailureUsesStopAndNeverHosts()
    {
        Initialize(); Start(); server.FailStart();
        Assert.Equal("native startup failure", server.OperationError);
        Assert.Equal(1, server.Cancellations);
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.DoesNotContain("color:Hosting", Trace);
        Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
        server.Ready(); // A late protocol readiness event has no registration side effects.
        Assert.False(Session.IsActive);
        Assert.DoesNotContain("session:start", Trace);
        server.Complete(true, null);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
    }

    [Fact]
    public void DuplicateCompletionCannotStopLaterAttempt()
    {
        Initialize(); Start(); Stop();
        var oldCompletion = server.Completion;
        server.Complete(true, null);
        Start(); server.Ready();
        oldCompletion(true, null);
        Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
    }

    [Fact]
    public void TransportDisposalInvalidatesLateCompletion()
    {
        Initialize(); Start(); Stop();
        var lateCompletion = server.Completion;
        var phase = Lifecycle.Current;
        int idleEntries = Trace.Count(line => line == "[Lifecycle] Idle");
        server.DisposeHost();
        Assert.Equal(1, server.Disposals);
        lateCompletion(true, null);
        server.DisposeHost();
        Assert.Equal(2, server.Disposals);
        Assert.Equal(phase, Lifecycle.Current);
        Assert.Equal(idleEntries, Trace.Count(line => line == "[Lifecycle] Idle"));
    }

    [Fact]
    public void OwningDestructionOnlyClearsSingleton()
    {
        Initialize(); Start(); server.Ready();
        Clearing = () => throw new Exception("Lifecycle destruction must not clear Session");
        var trace = Trace.ToArray();
        Invoke(machine, "OnDestroy");
        Assert.Null(typeof(Lifecycle).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
        Assert.True(Session.IsHost);
        Assert.Equal(0, server.Disposals);
        Assert.Equal(trace, Trace);
    }

    [Fact]
    public void EssentialStopFailureSkipsProtocolCancellation()
    {
        Initialize(); Start(); server.Ready();
        var error = new Exception("essential cleanup failure");
        Clearing = () => throw error;
        Assert.Same(error, Assert.Throws<Exception>(() => Stop()));
        Assert.Equal(Lifecycle.Phase.Stopping, Lifecycle.Current);
        Assert.Equal(0, server.Cancellations);
        Assert.Null(server.Completion);
        Assert.Contains(Trace, line => line.Contains("Event Stop failed in phase Stopping"));
    }

    private sealed class TestTransport : TransportServer
    {
        public Action<bool, string> Completion;
        public int Cancellations, Disposals;
        public bool SynchronousReady, SynchronousCancellation;
        protected override void StartProtocol()
        {
            Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
            Trace.Add("protocol:start");
            if (SynchronousReady) Ready();
        }
        public override void OnSessionPlayerAdmitted(ulong playerId)
        {
            Assert.Equal(Lifecycle.Phase.Hosting, Lifecycle.Current);
            Assert.True(Session.IsHost);
            Assert.Single(Trace, line => line == "color:Hosting");
            Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
            Trace.Add("protocol:admitted");
        }
        public void Ready() => ReportTransportStarted();
        public void FailStart() => ReportStartupFailure("native startup failure");
        protected override void CancelProtocol(Action<bool, string> completed)
        {
            if (Completion != null) return;
            Assert.False(Session.IsActive);
            Trace.Add("protocol:cancel"); Cancellations++; Completion = completed;
            if (SynchronousCancellation) Complete(true, null);
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
