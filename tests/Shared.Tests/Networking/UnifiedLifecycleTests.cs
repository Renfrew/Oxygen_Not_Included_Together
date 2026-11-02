#nullable disable
using System.Reflection;
using ONI_United.Core;
using ONI_United.Networking.Sessions;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[Collection("Lifecycle")]
public sealed class UnifiedLifecycleTests : IDisposable
{
    private readonly Lifecycle machine = new();
    public UnifiedLifecycleTests()
    {
        Reset();
        Invoke("Awake");
        Lifecycle.Initialize();
    }
    private void Invoke(string method) => typeof(Lifecycle).GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(machine, null);
    public void Dispose() { Clearing = null; Session.Reset(); Invoke("OnDestroy"); }
    private static void JoinNative()
    {
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.PrepareClientSession));
    }
    private static void JoinSession()
    {
        JoinNative();
        Assert.True(Session.AdmitClient(new Player(20, "Local"), new Player(10, "Host")));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.ClientAdmitted));
    }
    private static void Host()
    {
        Assert.True(Lifecycle.Handle(Lifecycle.Event.StartHost));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.PrepareHostSession));
    }

    [Fact]
    public void NativeEstablishmentAndAdmissionHaveDistinctPermissions()
    {
        Assert.False(Lifecycle.Handle(Lifecycle.Event.PrepareClientSession));
        JoinNative();
        Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.ClientAdmitted));
        Assert.False(Lifecycle.Handle(Lifecycle.Event.StartClientSync));
        Assert.True(Session.AdmitClient(new Player(20), new Player(10)));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.ClientAdmitted));
        Assert.Equal(Lifecycle.Phase.Joined, Lifecycle.Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedSyncEventsReturnToTheCorrectGameplayRoleAndCanRepeat(bool host)
    {
        if (host) Host(); else JoinSession();
        for (int i = 0; i < 2; i++)
        {
            Assert.True(Lifecycle.Handle(host ? Lifecycle.Event.StartHostSync : Lifecycle.Event.StartClientSync));
            Assert.Equal(Lifecycle.Phase.Synchronizing, Lifecycle.Current);
            Assert.Equal(host, NetworkRuntime.HasHostOperation);
            Assert.Equal(!host, NetworkRuntime.HasClientOperation);
            Assert.False(Lifecycle.Handle(host ? Lifecycle.Event.StartHostSync : Lifecycle.Event.StartClientSync));
            Assert.False(Lifecycle.CanSelectTransport);
            Assert.True(Lifecycle.Handle(Lifecycle.Event.CompleteSync));
            Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
            Assert.False(Lifecycle.Handle(Lifecycle.Event.CompleteSync));
        }
        Assert.DoesNotContain(Trace, x => x.StartsWith("pause:"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void EveryClientPhaseRejectsHostingAndSelectionBeforeActions(int step)
    {
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        if (step >= 1) Assert.True(Lifecycle.Handle(Lifecycle.Event.PrepareClientSession));
        if (step >= 2)
        {
            Assert.True(Session.AdmitClient(new Player(20), new Player(10)));
            Assert.True(Lifecycle.Handle(Lifecycle.Event.ClientAdmitted));
        }
        if (step >= 3) Assert.True(Lifecycle.Handle(Lifecycle.Event.StartClientSync));
        if (step >= 4) Assert.True(Lifecycle.Handle(Lifecycle.Event.CompleteSync));
        if (step >= 5) Assert.True(Lifecycle.Handle(Lifecycle.Event.ClearClientSession));
        var before = Lifecycle.Current;
        bool invoked = false;
        Assert.False(Lifecycle.Handle(Lifecycle.Event.StartHost, () => invoked = true));
        Assert.False(Lifecycle.Handle(Lifecycle.Event.Join, () => invoked = true));
        Assert.False(invoked);
        Assert.Equal(before, Lifecycle.Current);
        Assert.False(Lifecycle.CanSelectTransport);
    }

    [Fact]
    public void ClientClearRepeatsTheResetAndWaitsForConfirmedNativeRelease()
    {
        JoinSession();
        int clears = 0, nativeAttempts = 0;
        Clearing = () => clears++;
        Assert.True(Lifecycle.Handle(Lifecycle.Event.ClearClientSession, () =>
        {
            Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
            Assert.False(Session.IsActive);
            nativeAttempts++;
        }));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.ClearClientSession, () => nativeAttempts++));
        Assert.Equal(2, clears);
        Assert.Equal(2, nativeAttempts);
        Assert.False(Lifecycle.CanSelectTransport);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.PrepareClientSession));
        Assert.False(Lifecycle.Handle(Lifecycle.Event.ClientAdmitted));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.SessionCleared));
        Assert.True(Lifecycle.CanSelectTransport);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.SessionCleared));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
    }

    [Fact]
    public void HostSyncCanStopAndNeverAdmitsClientJoin()
    {
        Host();
        Assert.True(Lifecycle.Handle(Lifecycle.Event.StartHostSync));
        Assert.False(Lifecycle.Handle(Lifecycle.Event.Join));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.ClearHostSession));
        Assert.False(Lifecycle.Handle(Lifecycle.Event.Join));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.SessionCleared));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
    }

    [Fact]
    public void OperationIdentityChangesOnlyForAnAcceptedNewJoin()
    {
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        var original = ONI_United.Core.NetworkRuntime.TransportClient.Operation;
        Assert.False(Lifecycle.Handle(Lifecycle.Event.Join));
        Assert.Same(original, ONI_United.Core.NetworkRuntime.TransportClient.Operation);
        Assert.True(Lifecycle.Handle(Lifecycle.Event.ClearClientSession));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.ClearClientSession));
        Assert.Same(original, ONI_United.Core.NetworkRuntime.TransportClient.Operation);
        Assert.True(Lifecycle.Handle(Lifecycle.Event.SessionCleared));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        Assert.NotSame(original, ONI_United.Core.NetworkRuntime.TransportClient.Operation);
    }

    [Fact]
    public void NestedCancellationDuringNativeCompletionCannotAdmitSession()
    {
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.PrepareClientSession, () =>
        {
            Assert.True(Lifecycle.Handle(Lifecycle.Event.ClearClientSession));
            Assert.False(Lifecycle.Handle(Lifecycle.Event.ClientAdmitted));
        }));
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
    }
}
