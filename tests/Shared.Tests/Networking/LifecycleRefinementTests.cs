#nullable disable
using System.Reflection;
using ONI_United.Core;
using ONI_United.Networking.Sessions;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[Collection("Lifecycle")]
public sealed class LifecycleRefinementTests : IDisposable
{
    private readonly Lifecycle machine = new();
    public LifecycleRefinementTests() { Reset(); Invoke("Awake"); }
    private void Invoke(string name) => typeof(Lifecycle).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(machine, null);
    public void Dispose() { PreparingClient = null; Registered = null; Session.Reset(); Invoke("OnDestroy"); }

    [Fact]
    public void InitializeIsTheOnlyBootstrapEventAndCannotResetAnActiveJoin()
    {
        foreach (var evt in Enum.GetValues<Lifecycle.Event>())
            if (evt != Lifecycle.Event.Initialize) Assert.False(Lifecycle.Handle(evt));
        Assert.Equal(Lifecycle.Phase.Uninitialized, Lifecycle.Current);
        bool invoked = false;
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Initialize, afterAction: () => invoked = true));
        Assert.True(invoked);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        Assert.False(Lifecycle.Handle(Lifecycle.Event.Initialize));
        Assert.Throws<InvalidOperationException>(() => Lifecycle.Initialize());
        Assert.Equal(Lifecycle.Phase.Joining, Lifecycle.Current);
    }

    [Fact]
    public void PrepareHostSessionRunsHostActionBeforeNestedReadiness()
    {
        Lifecycle.Initialize();
        Assert.True(Lifecycle.Handle(Lifecycle.Event.StartHost));
        Registered = () => Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
        Assert.True(Lifecycle.Handle(Lifecycle.Event.PrepareHostSession, afterAction: () =>
        {
            Assert.True(Session.IsHost);
            Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
            Assert.Contains("session:start", Trace);
        }));
        Assert.DoesNotContain(Trace, x => x.StartsWith("client:prepare:"));
    }

    [Fact]
    public void SharedPrepareSessionSelectsClientActionAfterTransitionAndBeforeAfterAction()
    {
        Lifecycle.Initialize();
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        Trace.Clear();
        PreparingClient = () =>
        {
            Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
            Trace.Add("essential");
        };
        Assert.True(Lifecycle.Handle(Lifecycle.Event.PrepareClientSession, afterAction: () => Trace.Add("after")));
        Assert.Equal(new[] { "[Lifecycle] PreparingSession", "client:prepare:PreparingSession", "essential", "after" }, Trace);
        Assert.False(Session.IsActive);
        Assert.DoesNotContain("session:start", Trace);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.ClientAdmitted));
    }

    [Fact]
    public void RejectedSharedPreparationInvokesNeitherActionNorCallback()
    {
        Lifecycle.Initialize(); Trace.Clear();
        PreparingClient = () => throw new Exception("must not execute");
        Assert.False(Lifecycle.Handle(Lifecycle.Event.PrepareClientSession, afterAction: () => throw new Exception("must not execute")));
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Empty(Trace);
    }

    [Fact]
    public void ClientEssentialExceptionSkipsAfterActionLogsAndDoesNotRollback()
    {
        Lifecycle.Initialize(); Lifecycle.Handle(Lifecycle.Event.Join);
        var error = new InvalidOperationException("essential failure");
        PreparingClient = () => throw error;
        bool after = false;
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() =>
            Lifecycle.Handle(Lifecycle.Event.PrepareClientSession, afterAction: () => after = true)));
        Assert.False(after);
        Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
        Assert.Contains(Trace, x => x.Contains("Event PrepareClientSession failed in phase PreparingSession"));
    }

    [Fact]
    public void ClientAfterActionExceptionOccursAfterEssentialWorkAndPropagates()
    {
        Lifecycle.Initialize(); Lifecycle.Handle(Lifecycle.Event.Join);
        var error = new Exception("after failure");
        Assert.Same(error, Assert.Throws<Exception>(() => Lifecycle.Handle(Lifecycle.Event.PrepareClientSession,
            afterAction: () => { Assert.Contains("client:prepare:PreparingSession", Trace); throw error; })));
        Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
        Assert.Contains(Trace, x => x.Contains("after failure"));
    }

    [Fact]
    public void NestedHostStopStillRejectsHostSessionReadyAndRunsNoClientPreparation()
    {
        Lifecycle.Initialize(); Lifecycle.Handle(Lifecycle.Event.StartHost);
        Registered = () => Assert.True(Lifecycle.Handle(Lifecycle.Event.ClearHostSession));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.PrepareHostSession));
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.HostSessionReady));
        Assert.DoesNotContain(Trace, x => x.StartsWith("client:prepare:"));
    }

    [Fact]
    public void AcceptedEventWithoutDictionaryActionStillTransitionsAndRunsAfterAction()
    {
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Initialize, afterAction: () => Trace.Add("after")));
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Equal(new[] { "[Lifecycle] Idle", "after" }, Trace);
    }

    [Fact]
    public void ClientPreparationIsMandatoryWithoutAfterAction()
    {
        Lifecycle.Initialize();
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        int calls = 0;
        PreparingClient = () =>
        {
            Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
            calls++;
        };
        Assert.True(Lifecycle.Handle(Lifecycle.Event.PrepareClientSession));
        Assert.Equal(1, calls);
        Assert.False(Session.IsActive);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.ClientAdmitted));
    }

    [Theory]
    [InlineData((int)Lifecycle.Phase.Idle)]
    [InlineData((int)Lifecycle.Phase.PreparingSession)]
    [InlineData((int)Lifecycle.Phase.ClearingSession)]
    public void PrepareSessionUnexpectedSourceThrowsInsteadOfSkippingPreparation(int source)
    {
        Assert.Throws<InvalidOperationException>(() =>
            LifecycleEventActions.Execute(Lifecycle.Event.PrepareClientSession, (Lifecycle.Phase)source));
        Assert.False(Session.IsActive);
        Assert.Empty(Trace);
    }
}
