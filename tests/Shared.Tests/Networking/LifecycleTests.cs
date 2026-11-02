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
    private bool Start() => Lifecycle.Handle(Lifecycle.Event.StartHost);
    // Cleanup routing and transport completion are source-linked production code.
    private void Stop() => NetworkRuntime.Stop();

    [Theory]
    [InlineData(false, 0, false)] [InlineData(false, 0, true)]
    [InlineData(false, 1, false)] [InlineData(false, 1, true)]
    [InlineData(false, 2, false)] [InlineData(false, 2, true)]
    [InlineData(true, 0, false)] [InlineData(true, 0, true)]
    [InlineData(true, 1, false)] [InlineData(true, 1, true)]
    [InlineData(true, 2, false)] [InlineData(true, 2, true)]
    public void GameplayEventsUsePhaseAndSessionRoleIndependentlyOfWorldState(bool synchronize, int role, bool loadedWorld)
    {
        if (role == 1) Assert.True(Session.BeginHosting(new Player(10, "Host")));
        else if (role == 2) Assert.True(Session.AdmitClient(new Player(20, "Client"), new Player(10, "Host")));
        LoadedWorld = loadedWorld;
        var source = synchronize ? Lifecycle.Phase.Synchronizing : Lifecycle.Phase.PreparingSession;
        var evt = synchronize ? Lifecycle.Event.CompleteSync : Lifecycle.Event.HostSessionReady;
        typeof(Lifecycle).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(machine, source);
        Trace.Clear();
        bool after = false;
        bool permitted = synchronize ? role != 0 : role == 1;
        Assert.Equal(permitted, Lifecycle.Handle(evt, () =>
        {
            after = true;
            Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
            if (!synchronize) Assert.Contains("color:InGameSession", Trace);
        }));
        Assert.Equal(permitted, after);
        Assert.Equal(permitted ? Lifecycle.Phase.InGameSession : source, Lifecycle.Current);
        if (!permitted) Assert.Empty(Trace);
        typeof(Lifecycle).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(machine, Lifecycle.Phase.Idle);
        Trace.Clear();
        Assert.False(Lifecycle.Handle(evt, () => throw new Exception("rejected callback")));
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Empty(Trace);
    }

    // Exhaustive contract matrix, independent of the production permission dictionary.
    // Domain mismatches are intentional: phase membership alone must not grant admission.
    public static IEnumerable<object[]> Permissions()
    {
        foreach (var phase in Enum.GetValues<Lifecycle.Phase>())
        foreach (var evt in Enum.GetValues<Lifecycle.Event>())
        foreach (int role in new[] { 0, 1, 2 }) // No Session, host Session, client Session.
        foreach (int operationRole in new[] { 0, 1, 2 })
        {
            Lifecycle.Phase? destination = (phase, evt) switch
            {
                (Lifecycle.Phase.Uninitialized, Lifecycle.Event.Initialize) => Lifecycle.Phase.Idle,
                (Lifecycle.Phase.Idle, Lifecycle.Event.StartHost) when role == 0 && operationRole == 0 => Lifecycle.Phase.StartingHost,
                (Lifecycle.Phase.StartingHost, Lifecycle.Event.PrepareHostSession) => operationRole == 1
                    ? Lifecycle.Phase.InGameSession : Lifecycle.Phase.PreparingSession,
                (Lifecycle.Phase.Joining, Lifecycle.Event.PrepareClientSession) => Lifecycle.Phase.PreparingSession,
                (Lifecycle.Phase.PreparingSession, Lifecycle.Event.HostSessionReady) when role == 1 => Lifecycle.Phase.InGameSession,
                (Lifecycle.Phase.StartingHost or Lifecycle.Phase.PreparingSession or Lifecycle.Phase.InGameSession or Lifecycle.Phase.ClearingSession,
                    Lifecycle.Event.ClearHostSession) => Lifecycle.Phase.ClearingSession,
                (Lifecycle.Phase.Synchronizing, Lifecycle.Event.ClearHostSession) => Lifecycle.Phase.ClearingSession,
                (Lifecycle.Phase.Idle, Lifecycle.Event.Join) when role == 0 && operationRole == 0 => Lifecycle.Phase.Joining,
                (Lifecycle.Phase.PreparingSession, Lifecycle.Event.ClientAdmitted) when role == 2 => Lifecycle.Phase.Joined,
                (Lifecycle.Phase.Joining or Lifecycle.Phase.PreparingSession or Lifecycle.Phase.Joined
                    or Lifecycle.Phase.InGameSession or Lifecycle.Phase.ClearingSession, Lifecycle.Event.ClearClientSession) => Lifecycle.Phase.ClearingSession,
                (Lifecycle.Phase.Synchronizing, Lifecycle.Event.ClearClientSession) => Lifecycle.Phase.ClearingSession,
                (Lifecycle.Phase.ClearingSession, Lifecycle.Event.SessionCleared) => Lifecycle.Phase.Idle,
                (Lifecycle.Phase.InGameSession, Lifecycle.Event.StartHostSync) when role == 1 => Lifecycle.Phase.Synchronizing,
                (Lifecycle.Phase.Joined or Lifecycle.Phase.InGameSession, Lifecycle.Event.StartClientSync) when role == 2 => Lifecycle.Phase.Synchronizing,
                (Lifecycle.Phase.Synchronizing, Lifecycle.Event.CompleteSync) when role != 0 => Lifecycle.Phase.InGameSession,
                _ => null
            };
            int expected = destination.HasValue ? (int)destination.Value : -1;
            // Existing permission admits preparation; essential registration then fails on pre-existing membership.
            if (phase == Lifecycle.Phase.StartingHost && evt == Lifecycle.Event.PrepareHostSession && role != 0)
                expected = -2;
            yield return new object[] { (int)phase, (int)evt, expected, role, operationRole };
        }
    }

    [Theory]
    [MemberData(nameof(Permissions))]
    public void CompletePermissionTable(int phaseValue, int eventValue, int destinationValue, int role, int operationRole)
    {
        var phase = (Lifecycle.Phase)phaseValue;
        var evt = (Lifecycle.Event)eventValue;
        if (operationRole == 1) NetworkRuntime.BeginHostOperation();
        else if (operationRole == 2) NetworkRuntime.BeginClientOperation();
        if (role == 1) Assert.True(Session.BeginHosting(new Player(10, "Host")));
        else if (role == 2) Assert.True(Session.AdmitClient(new Player(20, "Client"), new Player(10, "Host")));
        typeof(Lifecycle).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(machine, phase);
        bool called = false;
        Action afterAction = () =>
        {
            called = true;
            Assert.Equal((Lifecycle.Phase)destinationValue, Lifecycle.Current);
        };
        if (destinationValue == -2)
        {
            Assert.Throws<InvalidOperationException>(() => Lifecycle.Handle(evt, afterAction));
            Assert.False(called);
            Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
            return;
        }
        bool admitted = Lifecycle.Handle(evt, afterAction);
        Assert.Equal(destinationValue >= 0, admitted);
        Assert.Equal(admitted, called);
        Assert.Equal(destinationValue >= 0 ? (Lifecycle.Phase)destinationValue : phase, Lifecycle.Current);
        if (!admitted) Assert.Empty(Trace);
    }

    public static IEnumerable<object[]> CleanupPermissions()
    {
        foreach (var phase in Enum.GetValues<Lifecycle.Phase>())
        foreach (bool host in new[] { false, true })
        {
            bool permitted = host
                ? phase is Lifecycle.Phase.StartingHost or Lifecycle.Phase.PreparingSession
                    or Lifecycle.Phase.InGameSession or Lifecycle.Phase.Synchronizing or Lifecycle.Phase.ClearingSession
                : phase is Lifecycle.Phase.Joining or Lifecycle.Phase.PreparingSession or Lifecycle.Phase.Joined
                    or Lifecycle.Phase.InGameSession or Lifecycle.Phase.Synchronizing or Lifecycle.Phase.ClearingSession;
            yield return new object[] { (int)phase, host, permitted };
        }
    }

    [Theory]
    [MemberData(nameof(CleanupPermissions))]
    public void ClearPermissionDependsOnPhaseNotOperationOwner(int phaseValue, bool host, bool permitted)
    {
        var phase = (Lifecycle.Phase)phaseValue;
        if (host) NetworkRuntime.BeginClientOperation(); else NetworkRuntime.BeginHostOperation();
        typeof(Lifecycle).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(machine, phase);
        var owner = NetworkRuntime.OperationTransport;
        var operation = NetworkRuntime.ActiveOperation;
        var matching = host ? Lifecycle.Event.ClearHostSession : Lifecycle.Event.ClearClientSession;
        bool after = false;
        Assert.Equal(permitted, Lifecycle.Handle(matching, () =>
        {
            after = true;
            Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
            Assert.Contains("session:stop", Trace);
            Assert.True(NetworkRuntime.IsCurrentOperation(owner, operation));
        }));
        Assert.Equal(permitted, after);
        if (!permitted) { Assert.Empty(Trace); return; }

        int logs = Trace.Count(x => x == "[Lifecycle] ClearingSession");
        Assert.True(Lifecycle.Handle(matching));
        Assert.Equal(2, Trace.Count(x => x == "session:stop"));
        Assert.Equal(logs, Trace.Count(x => x == "[Lifecycle] ClearingSession"));
        Assert.Same(operation, NetworkRuntime.ActiveOperation);
        Assert.True(Lifecycle.Handle(Lifecycle.Event.SessionCleared, () =>
        {
            Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
            Assert.Null(NetworkRuntime.ActiveOperation);
            Assert.Null(NetworkRuntime.OperationTransport);
        }));
        Assert.False(Lifecycle.Handle(Lifecycle.Event.SessionCleared, () => throw new Exception("rejected completion action")));
        Assert.False(Lifecycle.Handle(Lifecycle.Event.SessionCleared));
    }

    [Fact]
    public void BothClearActionsRunOnEveryAcceptedClearingSelfTransition()
    {
        Initialize(); Start();
        Assert.True(Lifecycle.Handle(Lifecycle.Event.ClearHostSession));
        Trace.Clear();
        Assert.True(Lifecycle.Handle(Lifecycle.Event.ClearClientSession, () => Trace.Add("after:client")));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.ClearHostSession, () => Trace.Add("after:host")));
        Assert.Equal(2, Trace.Count(x => x == "session:stop"));
        Assert.True(Trace.IndexOf("session:stop") < Trace.IndexOf("after:client"));
        Assert.True(Trace.LastIndexOf("session:stop") < Trace.IndexOf("after:host"));
        Assert.DoesNotContain("[Lifecycle] ClearingSession", Trace);
        Assert.True(Lifecycle.Handle(Lifecycle.Event.SessionCleared));
        var completed = Trace.ToArray();
        Assert.False(Lifecycle.Handle(Lifecycle.Event.SessionCleared, () => throw new Exception()));
        Assert.False(Lifecycle.Handle(Lifecycle.Event.ClearHostSession, () => throw new Exception()));
        Assert.False(Lifecycle.Handle(Lifecycle.Event.ClearClientSession, () => throw new Exception()));
        Assert.Equal(completed, Trace);
    }

    [Fact]
    public void ConfirmedCompletionCannotEnterIdleBeforeClearIsRequested()
    {
        Initialize(); Start();
        var token = NetworkRuntime.ActiveOperation;
        Trace.Clear();
        Assert.False(Lifecycle.Handle(Lifecycle.Event.SessionCleared));
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.Same(token, NetworkRuntime.ActiveOperation);
        Assert.Empty(Trace);
    }

    [Fact]
    public void AsynchronousClearKeepsOriginalIdentityUntilProtocolConfirms()
    {
        Initialize(); Start(); server.Ready();
        var token = NetworkRuntime.ActiveOperation;
        Stop();
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.Same(token, NetworkRuntime.ActiveOperation);
        Assert.Same(server, NetworkRuntime.OperationTransport);
        Stop();
        Assert.Equal(2, Trace.Count(x => x == "session:stop"));
        Assert.Same(token, NetworkRuntime.ActiveOperation);
        server.Complete(true, null);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Null(NetworkRuntime.ActiveOperation);
    }

    [Fact]
    public void TransitionLookupHasExactlyOneEntryPerGroupedSource()
    {
        var definitions = (System.Collections.IDictionary)typeof(Lifecycle)
            .GetField("Transitions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var lookup = (Dictionary<(Lifecycle.Event, Lifecycle.Phase), Lifecycle.Phase>)typeof(Lifecycle)
            .GetField("TransitionLookup", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var expected = new Dictionary<(Lifecycle.Event, Lifecycle.Phase), Lifecycle.Phase>();
        foreach (System.Collections.DictionaryEntry entry in definitions)
        foreach (object transition in (Array)entry.Value)
        {
            var sources = (Lifecycle.Phase[])transition.GetType().GetProperty("From").GetValue(transition);
            var destination = (Lifecycle.Phase)transition.GetType().GetProperty("To").GetValue(transition);
            foreach (var source in sources) expected.Add(((Lifecycle.Event)entry.Key, source), destination);
        }
        Assert.Equal(expected.Count, lookup.Count);
        foreach (var entry in expected) Assert.Equal(entry.Value, lookup[entry.Key]);
        Assert.Equal(9, Enum.GetValues<Lifecycle.Phase>().Length);
        Assert.Equal(13, Enum.GetValues<Lifecycle.Event>().Length);
    }

    [Fact]
    public void UnknownEventIsRejectedWithoutActions()
    {
        Initialize(); Trace.Clear();
        Assert.False(Lifecycle.Handle((Lifecycle.Event)int.MaxValue, () => throw new Exception("rejected callback")));
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Empty(Trace);
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void UnknownSourcePhaseIsRejectedWithoutActions(int source)
    {
        typeof(Lifecycle).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(machine, (Lifecycle.Phase)source);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.StartHost, () => throw new Exception("rejected callback")));
        Assert.Equal((Lifecycle.Phase)source, Lifecycle.Current);
        Assert.Empty(Trace);
    }

    [Fact]
    public void ImmediateReadinessCanCompleteInsideStart()
    {
        Initialize(); server.SynchronousReady = true;
        Assert.True(Start());
        server.Ready(); // Duplicate readiness must not repeat successful-host actions.
        Assert.False(Lifecycle.Handle(Lifecycle.Event.HostSessionReady, () => throw new Exception("duplicate completion")));
        Assert.Single(Trace, line => line == "color:InGameSession");
        Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
        Assert.Single(Trace, line => line == "protocol:admitted");
        Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
        Assert.True(Session.IsHost);
    }

    [Fact]
    public void UninitializedRejectsEveryEventWithoutActions()
    {
        foreach (var evt in Enum.GetValues<Lifecycle.Event>())
            if (evt != Lifecycle.Event.Initialize)
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
        Assert.DoesNotContain("color:InGameSession", Trace);
        Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
        server.Ready();
        Assert.Equal(new[] { "[Lifecycle] Idle", "[Lifecycle] StartingHost", "identity:start", "color:start",
            "tick:start", "protocol:start", "[Lifecycle] PreparingSession", "session:start",
            "[Lifecycle] InGameSession", "color:InGameSession", "protocol:admitted" }, Trace);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.PrepareHostSession, () => throw new Exception("rejected readiness callback")));
    }

    [Fact]
    public void HostPrepareSessionActionCompletesWithoutTransportCallback()
    {
        Initialize(); Start();
        Registered = () => Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);

        Assert.True(Lifecycle.Handle(Lifecycle.Event.PrepareHostSession, null));

        Assert.True(Session.IsHost);
        Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.PrepareHostSession, null));
        Assert.Single(Trace, line => line == "session:start");
        Assert.Single(Trace, line => line == "color:InGameSession");
    }

    [Fact]
    public void NestedStopDuringRegistrationRejectsLaterHostSessionReady()
    {
        Initialize(); Start();
        Registered = () => Stop();
        server.Ready();
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        Assert.False(Session.IsActive);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.HostSessionReady, () => throw new Exception()));
        Assert.Equal(1, server.Cancellations);
        Assert.DoesNotContain("color:InGameSession", Trace);
        Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
    }

    [Fact]
    public void ClientJoinRejectsHostBeforeTransitionOrActions()
    {
        Initialize();
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        Trace.Clear();
        Assert.False(Start());
        Assert.Equal(Lifecycle.Phase.Joining, Lifecycle.Current);
        Assert.Empty(Trace);
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
        Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
        Assert.Contains(Trace, line => line.Contains("Event PrepareHostSession failed in phase PreparingSession"));
        Assert.DoesNotContain("[Lifecycle] InGameSession", Trace);
        Assert.DoesNotContain("color:InGameSession", Trace);
        Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
    }

    [Fact]
    public void CallbackExceptionPropagatesAfterTransition()
    {
        Initialize();
        var error = new Exception("callback failure");
        Assert.Same(error, Assert.Throws<Exception>(() => Lifecycle.Handle(Lifecycle.Event.StartHost, () => throw error)));
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
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        Assert.False(Start());
        server.Complete(true, null);
        completion(true, null); // Duplicate completion must not enter Idle again.
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Single(Trace, line => line == "[Lifecycle] ClearingSession");
        Assert.Equal(2, Trace.Count(line => line == "[Lifecycle] Idle"));
    }

    [Fact]
    public void RejectedStopEventDoesNotRunTransport()
    {
        Initialize();
        Assert.False(Lifecycle.Handle(Lifecycle.Event.ClearHostSession, server.Stop));
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
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
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
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        Assert.DoesNotContain("color:InGameSession", Trace);
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
        Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
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
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        Assert.Equal(0, server.Cancellations);
        Assert.Null(server.Completion);
        Assert.Contains(Trace, line => line.Contains("Event ClearHostSession failed in phase ClearingSession"));
    }

    private sealed class TestTransport : TransportServer
    {
        private object operation;
        public Action<bool, string> Completion;
        public int Cancellations, Disposals;
        public bool SynchronousReady, SynchronousCancellation;
        protected override void StartProtocol(object operation)
        {
            this.operation = operation;
            Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
            Trace.Add("protocol:start");
            if (SynchronousReady) Ready();
        }
        public override void OnSessionPlayerAdmitted(ulong playerId)
        {
            Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
            Assert.True(Session.IsHost);
            Assert.Single(Trace, line => line == "color:InGameSession");
            Assert.DoesNotContain(Trace, line => line.StartsWith("pause:"));
            Trace.Add("protocol:admitted");
        }
        public void Ready() => ReportTransportStarted(operation);
        public void FailStart() => ReportStartupFailure(operation, "native startup failure");
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
