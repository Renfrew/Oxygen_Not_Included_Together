#nullable disable
using System.Reflection;
using System.Runtime.InteropServices;
using ONI_United.Core;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transports.Steamworks;
using Steamworks;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[Collection("Lifecycle")]
public sealed class LiveSteamClientTests : IDisposable
{
    private readonly Lifecycle machine = new();
    private readonly SteamworksClientLegacy client = new();
    private int established, ended;
    public LiveSteamClientTests()
    {
        Reset(); Invoke("Awake"); Lifecycle.Initialize();
        SteamManager.Initialized = true; SteamLobbyFixture.SetCurrentLobby(1);
        MultiplayerSession.HostUserID = 20;
        SteamNetworkingSockets.Connections.Clear(); SteamNetworkingSockets.CloseFailures.Clear();
        SteamNetworkingSockets.PendingCallbacks.Clear();
        SteamNetworkingSockets.ConnectFails = SteamNetworkingSockets.ConnectThrows = false;
        NetworkRuntime.UseTestClient(client);
        client.OnClientConnected = () =>
        {
            established++;
            Lifecycle.Handle(Lifecycle.Event.PrepareClientSession);
        };
        client.OnClientDisconnected = () => { ended++; NetworkRuntime.DisconnectClient(); };
        client.OnConnectionFailed = reason => NetworkRuntime.DisconnectClient();
    }
    private void Invoke(string name) => typeof(Lifecycle).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(machine, null);
    public void Dispose()
    {
        SteamNetworkingSockets.CloseFailures.Clear(); client.TryStop(out _);
        Session.Reset(); Invoke("OnDestroy"); SteamLobbyFixture.SetCurrentLobby(0);
        NetworkRuntime.UseTestClient(new NetworkRuntime.TestClient());
    }
    private HSteamNetConnection Connect()
    {
        Assert.True(NetworkRuntime.ConnectClient());
        return SteamworksClientLegacy.Connection.Value;
    }
    private void Notify(HSteamNetConnection handle, ESteamNetworkingConnectionState state)
    {
        var info = new SteamNetConnectionInfo_t { m_eState = state,
            m_identityRemote = new() { SteamId = new(20) } };
        if (SteamNetworkingSockets.Connections.ContainsKey(handle)) SteamNetworkingSockets.Connections[handle] = info;
        Callback<SteamNetConnectionStatusChangedCallback_t>.Dispatch(new() { m_hConn = handle, m_info = info });
    }

    [Fact]
    public void NativeConnectedPreparesButDoesNotAdmitSessionAndDuplicatesAreIgnored()
    {
        var handle = Connect();
        Notify(handle, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Notify(handle, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Assert.Equal(1, established);
        Assert.Equal(Lifecycle.Phase.PreparingSession, Lifecycle.Current);
        Assert.False(Session.IsActive);
    }

    [Fact]
    public void FailedNativeReleaseRetainsHandleAndBlocksHostSelectionAndNewJoinUntilRetry()
    {
        var handle = Connect(); SteamNetworkingSockets.CloseFailures.Add(handle);
        NetworkRuntime.DisconnectClient();
        Assert.Equal(handle, SteamworksClientLegacy.Connection);
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        Assert.False(Lifecycle.CanSelectTransport);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.StartHost));
        Assert.False(NetworkRuntime.ConnectClient());
        Notify(handle, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Assert.Equal(0, established);
        SteamNetworkingSockets.CloseFailures.Clear(); NetworkRuntime.DisconnectClient();
        Assert.Null(SteamworksClientLegacy.Connection);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Equal(0, Callback<SteamNetConnectionStatusChangedCallback_t>.Count);
    }

    [Fact]
    public void LateOldHandleCannotCompleteOrDisconnectNewAttempt()
    {
        var old = Connect(); NetworkRuntime.DisconnectClient();
        var current = Connect();
        Notify(old, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Notify(old, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer);
        Assert.Equal(current, SteamworksClientLegacy.Connection); Assert.Equal(0, ended); Assert.Equal(0, established);
        Notify(current, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Assert.Equal(1, established);
    }

    [Fact]
    public void QueuedConnectedNotificationCannotOverrideFailedNativeState()
    {
        var handle = Connect();
        var native = SteamNetworkingSockets.Connections[handle];
        native.m_eState = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally;
        SteamNetworkingSockets.Connections[handle] = native;
        var queued = native;
        queued.m_eState = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected;
        Callback<SteamNetConnectionStatusChangedCallback_t>.Dispatch(new() { m_hConn = handle, m_info = queued });
        Assert.Equal(0, established);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.Null(SteamworksClientLegacy.Connection);
    }

    [Fact]
    public void NativeFailureAndRemoteDisconnectUseTheSameTerminationPath()
    {
        var pending = Connect();
        Notify(pending, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        var admitted = Connect();
        Notify(admitted, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Assert.True(Session.AdmitClient(new Player(10), new Player(20)));
        Assert.True(Lifecycle.Handle(Lifecycle.Event.ClientAdmitted));
        Notify(admitted, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
        Assert.False(Session.IsActive); Assert.Null(SteamworksClientLegacy.Connection);
    }
}
