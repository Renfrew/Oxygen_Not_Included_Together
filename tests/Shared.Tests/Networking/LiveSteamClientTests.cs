#nullable disable
using System.Reflection;
using System.Runtime.InteropServices;
using ONI_United.Core;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transport.Steamworks;
using Steamworks;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[Collection("Lifecycle")]
public sealed class LiveSteamClientTests : IDisposable
{
    private readonly Lifecycle machine = new();
    private readonly SteamworksClient client = new();
    private int established, ended;
    public LiveSteamClientTests()
    {
        Reset(); Invoke("Awake"); Lifecycle.Initialize();
        SteamManager.Initialized = true; SteamLobby.InLobby = true;
        MultiplayerSession.HostUserID = 20;
        SteamNetworkingSockets.Connections.Clear(); SteamNetworkingSockets.CloseFailures.Clear();
        SteamNetworkingSockets.PendingCallbacks.Clear();
        SteamNetworkingSockets.ConnectFails = SteamNetworkingSockets.ConnectThrows = false;
        NetworkRuntime.TransportClient = client;
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
        Session.Reset(); Invoke("OnDestroy"); SteamLobby.InLobby = false;
        NetworkRuntime.TransportClient = new NetworkRuntime.TestClient();
    }
    private HSteamNetConnection Connect()
    {
        Assert.True(NetworkRuntime.ConnectClient());
        return SteamworksClient.Connection.Value;
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
        Assert.Equal(handle, SteamworksClient.Connection);
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        Assert.False(Lifecycle.CanSelectTransport);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.StartHost));
        Assert.False(NetworkRuntime.ConnectClient());
        Notify(handle, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Assert.Equal(0, established);
        SteamNetworkingSockets.CloseFailures.Clear(); NetworkRuntime.DisconnectClient();
        Assert.Null(SteamworksClient.Connection);
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
        Assert.Equal(current, SteamworksClient.Connection); Assert.Equal(0, ended); Assert.Equal(0, established);
        Notify(current, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Assert.Equal(1, established);
    }

    [Fact]
    public void RetiredCallbackCannotAffectNewAttemptEvenWhenNativeHandleIsReused()
    {
        var old = Connect();
        var retired = Callback<SteamNetConnectionStatusChangedCallback_t>.CaptureDispatch();
        NetworkRuntime.DisconnectClient();
        SteamNetworkingSockets.NextClientHandle = old.m_HSteamNetConnection;
        var current = Connect();
        Assert.Equal(old, current);
        var connected = new SteamNetConnectionInfo_t
        {
            m_eState = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected,
            m_identityRemote = new() { SteamId = new(20) }
        };
        SteamNetworkingSockets.Connections[current] = connected;
        retired(new() { m_hConn = old, m_info = connected });
        connected.m_eState = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer;
        retired(new() { m_hConn = old, m_info = connected });
        Assert.Equal(Lifecycle.Phase.Joining, Lifecycle.Current);
        Assert.Equal(0, established);
        Assert.Equal(0, ended);
        Assert.Equal(current, SteamworksClient.Connection);
        Notify(current, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Assert.Equal(1, established);
    }

    [Fact]
    public void OldReceiveBatchReleasesButDoesNotDispatchAfterSameHandleRestart()
    {
        var old = Connect();
        var data = Marshal.AllocHGlobal(1);
        var messages = new[] { Marshal.AllocHGlobal(Marshal.SizeOf<SteamNetworkingMessage_t>()),
            Marshal.AllocHGlobal(Marshal.SizeOf<SteamNetworkingMessage_t>()) };
        int dispatched = 0;
        int released = SteamNetworkingMessage_t.Released.Count;
        try
        {
            foreach (var pointer in messages)
                Marshal.StructureToPtr(new SteamNetworkingMessage_t { m_pData = data, m_cbSize = 1 }, pointer, false);
            SteamNetworkingSockets.Received.Enqueue(messages);
            ONI_United.Networking.Packets.Architecture.PacketHandler.OnIncoming = () =>
            {
                dispatched++;
                NetworkRuntime.DisconnectClient();
                SteamNetworkingSockets.NextClientHandle = old.m_HSteamNetConnection;
                Assert.Equal(old, Connect());
            };
            client.OnMessageRecieved();
            Assert.Equal(1, dispatched);
            Assert.Equal(released + 2, SteamNetworkingMessage_t.Released.Count);
            foreach (var pointer in messages) Assert.Contains(pointer, SteamNetworkingMessage_t.Released);
        }
        finally
        {
            ONI_United.Networking.Packets.Architecture.PacketHandler.OnIncoming = null;
            foreach (var pointer in messages) Marshal.FreeHGlobal(pointer);
            Marshal.FreeHGlobal(data);
        }
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
        Assert.Null(SteamworksClient.Connection);
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
        Assert.False(Session.IsActive); Assert.Null(SteamworksClient.Connection);
    }
}
