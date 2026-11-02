#nullable disable
using System.Reflection;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transport.Steamworks;
using Steamworks;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[Collection("Lifecycle")]
public sealed class SteamworksClientDraftTests : IDisposable
{
    private readonly SteamworksClientDraft client = new();
    private int connected, disconnected;
    private readonly List<string> failures = new();
    private HSteamNetConnection? Handle => (HSteamNetConnection?)typeof(SteamworksClientDraft)
        .GetField("connection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);

    public SteamworksClientDraftTests()
    {
        Reset(); SteamManager.Initialized = true; SteamLobby.InLobby = true;
        MultiplayerSession.HostUserID = 20;
        SteamNetworkingSockets.Connections.Clear();
        SteamNetworkingSockets.CloseFailures.Clear();
        SteamNetworkingSockets.Closed.Clear();
        SteamNetworkingSockets.PendingCallbacks.Clear();
        SteamNetworkingSockets.Received.Clear();
        SteamNetworkingSockets.NextClientHandle = 10;
        SteamNetworkingSockets.ConnectCalls = SteamNetworkingSockets.RunCallbackCalls = 0;
        SteamNetworkingSockets.ConnectFails = SteamNetworkingSockets.ConnectThrows = false;
        SteamNetworkingMessage_t.Released.Clear();
        SteamNetworkingMessage_t.ReleaseFailures.Clear();
        client.OnClientConnected += () => connected++;
        client.OnClientDisconnected += () => disconnected++;
        client.OnConnectionFailed += failures.Add;
    }
    public void Dispose()
    {
        SteamNetworkingSockets.CloseFailures.Clear();
        client.Disconnect(); SteamLobby.InLobby = false; Session.Reset();
        SteamNetworkingSockets.PendingCallbacks.Clear();
    }
    private HSteamNetConnection Connect() { client.Connect(); return Handle.Value; }
    private void Notify(HSteamNetConnection handle, ESteamNetworkingConnectionState state, ulong host = 20, bool updateNative = true)
    {
        var info = new SteamNetConnectionInfo_t
        {
            m_identityRemote = new() { SteamId = new(host) }, m_eState = state
        };
        if (updateNative && SteamNetworkingSockets.Connections.ContainsKey(handle))
            SteamNetworkingSockets.Connections[handle] = info;
        SteamNetworkingSockets.PendingCallbacks.Enqueue(() =>
            Callback<SteamNetConnectionStatusChangedCallback_t>.Dispatch(new() { m_hConn = handle, m_info = info }));
        client.Poll();
    }
    private void Ready(HSteamNetConnection handle) =>
        Notify(handle, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);

    [Fact]
    public void NativeConnectionUsesSessionHostAndReportsSuccessWithoutSessionAdmission()
    {
        var handle = Connect();
        Assert.Equal(new CSteamID(20), SteamNetworkingSockets.Connections[handle].m_identityRemote.GetSteamID());
        Assert.Equal(0, SteamNetworkingSockets.LastVirtualPort);
        Assert.Empty(SteamNetworkingSockets.LastOptions);
        Assert.Equal(0, connected);
        Ready(handle); Ready(handle);
        Assert.Equal(1, connected); Assert.Empty(failures);
        Assert.False(Session.IsActive); Assert.Equal(handle, Handle);
        Assert.True(SteamNetworkingSockets.RunCallbackCalls > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCreationFailureReleasesCallbackWithoutSuccess(bool throws)
    {
        SteamNetworkingSockets.ConnectFails = true; SteamNetworkingSockets.ConnectThrows = throws;
        client.Connect();
        Assert.Null(Handle); Assert.Single(failures); Assert.Equal(0, connected);
        Assert.Equal(1, disconnected);
        Assert.Equal(0, Callback<SteamNetConnectionStatusChangedCallback_t>.Count);
        Assert.Empty(SteamNetworkingSockets.Connections);
    }

    [Fact]
    public void MissingSteamOrInvalidHostRejectsBeforeNativeAcquisition()
    {
        SteamManager.Initialized = false;
        client.Connect();
        SteamManager.Initialized = true;
        MultiplayerSession.HostUserID = 0;
        client.Connect();
        Assert.Equal(2, failures.Count);
        Assert.Equal(0, SteamNetworkingSockets.ConnectCalls);
        Assert.Null(Handle);
    }

    [Fact]
    public void PollRequiresSteamAndAnOwnedNativeConnection()
    {
        Assert.Throws<InvalidOperationException>(client.Poll);
        Assert.Equal(0, SteamNetworkingSockets.RunCallbackCalls);

        Connect();
        SteamManager.Initialized = false;
        Assert.Throws<InvalidOperationException>(client.Poll);
        Assert.Equal(0, SteamNetworkingSockets.RunCallbackCalls);
        SteamManager.Initialized = true;
    }

    [Fact]
    public void PollServicesConnectingCallbackBeforeAttemptingMessageReception()
    {
        var handle = Connect();
        var message = new IntPtr(1);
        SteamNetworkingSockets.Received.Enqueue(new[] { message });

        client.Poll();

        Assert.Equal(1, SteamNetworkingSockets.RunCallbackCalls);
        Assert.Single(SteamNetworkingSockets.Received);
        Assert.Empty(SteamNetworkingMessage_t.Released);

        Ready(handle);

        Assert.Empty(SteamNetworkingSockets.Received);
        Assert.Equal(new[] { message }, SteamNetworkingMessage_t.Released);
    }

    [Fact]
    public void NativeConnectionUsesCurrentSessionHostWithoutLobbyValidation()
    {
        SteamLobby.InLobby = false;
        var handle = Connect();
        Ready(handle);
        Assert.Equal(1, connected);
        Assert.Empty(failures);
        Assert.False(Session.IsActive);
    }

    [Fact]
    public void NativeFailureBeforeEstablishmentCannotReportSuccess()
    {
        var handle = Connect();
        Notify(handle, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally);
        Assert.Single(failures); Assert.Equal(0, connected); Assert.Equal(1, disconnected);
        Assert.Null(Handle); Assert.Contains(handle, SteamNetworkingSockets.Closed);
        Assert.Equal(0, Callback<SteamNetConnectionStatusChangedCallback_t>.Count);
    }

    [Fact]
    public void NativeIdentityMismatchIsRejected()
    {
        var handle = Connect();
        Notify(handle, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected, host: 30);
        Assert.Single(failures); Assert.Equal(0, connected); Assert.Equal(1, disconnected); Assert.Null(Handle);
    }

    [Fact]
    public void QueuedConnectedEventCannotOverrideCurrentNativeFailure()
    {
        var handle = Connect();
        var info = SteamNetworkingSockets.Connections[handle];
        info.m_eState = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer;
        SteamNetworkingSockets.Connections[handle] = info;
        Notify(handle, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected, updateNative: false);
        Assert.Single(failures); Assert.Equal(0, connected); Assert.Equal(1, disconnected); Assert.Null(Handle);
    }

    [Fact]
    public void DisconnectDuringSetupAndRepeatedDisconnectNotifyOnlyOnce()
    {
        Connect(); client.Disconnect(); client.Disconnect();
        Assert.Empty(failures); Assert.Equal(0, connected); Assert.Equal(1, disconnected); Assert.Null(Handle);
        Assert.Equal(0, Callback<SteamNetConnectionStatusChangedCallback_t>.Count);
    }

    [Fact]
    public void InvalidNativeReceiveHandleInitiatesCleanupAndReportsCompletion()
    {
        var handle = Connect();
        Ready(handle);
        SteamNetworkingSockets.Connections.Remove(handle);

        client.Poll();

        Assert.Null(Handle);
        Assert.Equal(1, disconnected);
        Assert.Empty(failures);
        Assert.Equal(0, Callback<SteamNetConnectionStatusChangedCallback_t>.Count);
    }

    [Fact]
    public void RepeatedConnectCannotReplaceConnectingOrConnectedHandle()
    {
        var handle = Connect();
        Assert.Throws<InvalidOperationException>(client.Connect);
        Ready(handle);
        Assert.Throws<InvalidOperationException>(client.Connect);
        Assert.Equal(1, SteamNetworkingSockets.ConnectCalls); Assert.Equal(handle, Handle);
        client.Disconnect(); client.Disconnect();
        Assert.Equal(1, disconnected); Assert.Empty(failures);
    }

    [Fact]
    public void ExplicitDisconnectNotifiesOnlyAfterNativeHandleIsReleased()
    {
        var handle = Connect(); Ready(handle);
        client.OnClientDisconnected += () =>
        {
            Assert.Null(Handle);
            Assert.False(SteamNetworkingSockets.Connections.ContainsKey(handle));
        };
        client.Disconnect();
        Assert.Equal(1, disconnected);
        Assert.Single(SteamNetworkingSockets.Closed);
        client.Disconnect();
        Assert.Equal(1, disconnected);
    }

    [Fact]
    public void NormalTerminationReleasesHandleAndIgnoresStaleCallbacksAfterReconnect()
    {
        var old = Connect(); Ready(old);
        Notify(old, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer);
        Assert.Equal(1, disconnected); Assert.Null(Handle);
        var current = Connect();
        Notify(old, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally);
        Ready(old);
        Assert.Equal(current, Handle); Assert.Empty(failures); Assert.Equal(1, disconnected);
        Ready(current);
        Assert.Equal(2, connected);
    }

    [Fact]
    public void FailedNativeCloseRetainsHandleAndCallbackUntilOwnershipEnds()
    {
        var old = Connect(); Ready(old);
        SteamNetworkingSockets.CloseFailures.Add(old);
        client.Disconnect();
        Assert.Throws<InvalidOperationException>(client.Connect);
        Assert.Equal(old, Handle); Assert.Equal(1, SteamNetworkingSockets.ConnectCalls);
        Assert.Equal(0, disconnected); Assert.Equal(1, connected);
        Assert.Equal(1, Callback<SteamNetConnectionStatusChangedCallback_t>.Count);

        SteamNetworkingSockets.CloseFailures.Clear();
        client.Disconnect();
        Assert.Null(Handle);
        Assert.Equal(1, disconnected);
        Assert.Equal(0, Callback<SteamNetConnectionStatusChangedCallback_t>.Count);
    }

    [Fact]
    public void DrainReleasesEntireNativeBatchEvenIfOneReleaseFails()
    {
        var handle = Connect(); Ready(handle);
        var first = new IntPtr(1); var failed = new IntPtr(2); var last = new IntPtr(3);
        SteamNetworkingMessage_t.ReleaseFailures.Add(failed);
        SteamNetworkingSockets.Received.Enqueue(new[] { first, failed, last });
        client.Poll();
        Assert.Equal(new[] { first, failed, last }, SteamNetworkingMessage_t.Released);
        Assert.Equal(handle, Handle); Assert.Equal(1, connected);
        Assert.Contains(Trace, line => line.Contains("Native message release failed"));
    }

    [Fact]
    public void UnownedConnectionNotificationsCannotChangeTheClient()
    {
        var current = Connect();
        var other = new HSteamNetConnection(999);
        Notify(other, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Notify(other, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer);
        Assert.Equal(current, Handle);
        Assert.Equal(0, connected);
        Assert.Equal(0, disconnected);
        Assert.Empty(failures);
        Ready(current);
        Assert.Equal(1, connected);
    }

    [Fact]
    public void DisconnectionSubscriberCanReconnectWithoutOldCleanupReleasingNewHandle()
    {
        var old = Connect();
        Ready(old);
        Action reconnect = () =>
        {
            Assert.Null(Handle);
            Assert.False(SteamNetworkingSockets.Connections.ContainsKey(old));
            client.Connect();
        };
        client.OnClientDisconnected += reconnect;
        client.Disconnect();
        Assert.NotNull(Handle);
        Assert.NotEqual(old, Handle.Value);
        Assert.True(SteamNetworkingSockets.Connections.ContainsKey(Handle.Value));
        Assert.Equal(1, disconnected);
        Assert.Equal(1, Callback<SteamNetConnectionStatusChangedCallback_t>.Count);
        client.OnClientDisconnected -= reconnect;
    }
}
