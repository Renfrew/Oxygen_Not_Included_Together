#nullable disable
using System.Reflection;
using ONI_United.Core;
using ONI_United.Networking.Sessions;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[Collection("Lifecycle")]
public sealed class ClientTransportLifecycleTests : IDisposable
{
    private readonly Lifecycle machine = new();
    private readonly NetworkRuntime.TestClient client;

    public ClientTransportLifecycleTests()
    {
        Reset();
        Invoke("Awake");
        Lifecycle.Initialize();
        client = Assert.IsType<NetworkRuntime.TestClient>(NetworkRuntime.TransportClient);
    }

    private void Invoke(string name) => typeof(Lifecycle).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(machine, null);

    public void Dispose()
    {
        Session.Reset();
        Invoke("OnDestroy");
    }

    [Fact]
    public void ConnectTransitionsBeforeNativeWorkAndRejectsDuplicateOrWrongPhaseRequests()
    {
        client.Connect();

        Assert.Equal(Lifecycle.Phase.Joining, Lifecycle.Current);
        Assert.Equal(1, client.ConnectAttempts);
        Assert.True(NetworkRuntime.HasClientOperation);
        Assert.Contains("native:connect:Joining", Trace);

        client.Connect();
        Assert.Equal(1, client.ConnectAttempts);

        NetworkRuntime.DisconnectClient();
        NetworkRuntime.StartServer();
        client.Connect();
        Assert.Equal(Lifecycle.Phase.StartingHost, Lifecycle.Current);
        Assert.Equal(1, client.ConnectAttempts);
    }

    [Fact]
    public void NativeEstablishmentRunsMandatoryPreparationBeforeNotificationAndOnlyOnce()
    {
        client.Connect();
        client.OnClientConnected = () => Trace.Add("native:connected-notification");

        client.RaiseConnected();
        client.RaiseConnected();

        Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
        Assert.Equal(1, Trace.Count(entry => entry == "client:prepare:PreparingSession"));
        Assert.True(Trace.IndexOf("client:prepare:PreparingSession")
            < Trace.IndexOf("native:connected-notification"));
        Assert.Equal(1, Trace.Count(entry => entry == "native:connected-notification"));
    }

    [Fact]
    public void ApplicationAndProtocolDisconnectUseOneLifecycleCleanupEach()
    {
        client.Connect();
        client.Disconnect();

        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Equal(1, client.CleanupAttempts);
        Assert.Equal(1, Trace.Count(entry => entry == "session:stop"));

        client.Connect();
        client.RaiseProtocolDisconnect();

        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Equal(2, client.CleanupAttempts);
        Assert.Equal(2, Trace.Count(entry => entry == "session:stop"));
    }

    [Fact]
    public void FailedConnectionNotifiesAndUsesTheSameCleanupRouteWithoutDoubleDispatch()
    {
        client.Connect();
        client.OnConnectionFailed = reason =>
        {
            Trace.Add("connection-failed:" + Lifecycle.Current);
            NetworkRuntime.DisconnectClient();
        };

        client.RaiseConnectionFailed("failed");

        Assert.Contains("connection-failed:Joining", Trace);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Equal(1, client.CleanupAttempts);
        Assert.Equal(1, Trace.Count(entry => entry == "session:stop"));

        client.Connect();
        client.OnConnectionFailed = reason => Trace.Add("unhandled-by-caller:" + reason);
        client.RaiseConnectionFailed("failed-without-routing");
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Equal(2, client.CleanupAttempts);
        Assert.Equal(2, Trace.Count(entry => entry == "session:stop"));
    }

    [Fact]
    public void NativeCompletionFollowsLogicalCleanupAndRetainsOwnershipUntilThen()
    {
        client.Connect();
        var operation = NetworkRuntime.ActiveOperation;
        client.CompleteDisconnectImmediately = false;
        var completionPhase = Lifecycle.Phase.Uninitialized;
        client.OnClientDisconnected = () => completionPhase = Lifecycle.Current;

        client.Disconnect();

        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        Assert.Same(operation, NetworkRuntime.ActiveOperation);
        Assert.False(Session.IsActive);
        Assert.Equal(1, Trace.Count(entry => entry == "session:stop"));

        client.Poll();
        Assert.Equal(1, client.PollAttempts);
        client.CompleteDisconnect();

        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Null(NetworkRuntime.ActiveOperation);
        Assert.Equal(Lifecycle.Phase.Idle, completionPhase);
    }

    [Fact]
    public void PollingIsLifecycleGatedAcrossClientPhasesAndDeniedWhileIdleOrHostOwned()
    {
        client.Poll();
        Assert.Equal(0, client.PollAttempts);

        client.Connect();
        client.Poll();
        Assert.Equal(1, client.PollAttempts);

        client.RaiseConnected();
        Assert.True(Session.AdmitClient(new Player(20), new Player(10)));
        Assert.True(NetworkRuntime.ClientAdmitted(client, NetworkRuntime.ActiveOperation));
        client.Poll();
        Assert.True(Lifecycle.Handle(Lifecycle.Event.StartClientSync));
        client.Poll();
        Assert.True(Lifecycle.Handle(Lifecycle.Event.CompleteSync));
        client.Poll();
        Assert.Equal(4, client.PollAttempts);

        client.CompleteDisconnectImmediately = false;
        client.Disconnect();
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        client.Poll();
        Assert.Equal(5, client.PollAttempts);
        client.CompleteDisconnect();

        client.Poll();
        Assert.Equal(5, client.PollAttempts);

        NetworkRuntime.StartServer();
        client.Poll();
        Assert.True(Lifecycle.Handle(Lifecycle.Event.PrepareHostSession));
        client.Poll();
        Assert.True(Lifecycle.Handle(Lifecycle.Event.StartHostSync));
        client.Poll();
        Assert.Equal(5, client.PollAttempts);
        NetworkRuntime.StopHost();
    }
}
