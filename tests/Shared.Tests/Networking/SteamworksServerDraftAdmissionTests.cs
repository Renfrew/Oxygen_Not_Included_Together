#nullable disable
using System.Collections;
using System.Reflection;
using ONI_United.Core;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transports.Steamworks;
using Steamworks;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[Collection("Lifecycle")]
public sealed class SteamworksServerDraftAdmissionTests : IDisposable
{
    private readonly Lifecycle machine = new();
    private readonly SteamworksServer server = new();
    private IDictionary Connections => (IDictionary)typeof(SteamworksServer)
        .GetField("physicalConnections", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(server);
    private void Invoke(string name) => typeof(Lifecycle)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(machine, null);

    public SteamworksServerDraftAdmissionTests()
    {
        Reset();
        SteamManager.Initialized = true;
        SteamLobbyFixture.SetCurrentLobby(0);
        SteamNetworkingSockets.Connections.Clear();
        SteamNetworkingSockets.CloseFailures.Clear();
        SteamNetworkingSockets.Closed.Clear();
        SteamFriends.Names.Clear();
        Invoke("Awake");
        Lifecycle.Initialize();
        Lifecycle.Handle(Lifecycle.Event.StartHost, server.Start);
    }
    public void Dispose()
    {
        SteamNetworkingSockets.CloseFailures.Clear();
        Lifecycle.Handle(Lifecycle.Event.ClearHostSession, server.Stop);
        Invoke("OnDestroy");
        Session.Reset();
    }
    private static void Notify(HSteamNetConnection handle, ulong id, ESteamNetworkingConnectionState state)
    {
        Callback<SteamNetConnectionStatusChangedCallback_t>.Dispatch(new()
        {
            m_hConn = handle,
            m_info = new()
            {
                m_hListenSocket = new(1), m_eState = state,
                m_identityRemote = new() { SteamId = new(id) }
            }
        });
    }
    private static HSteamNetConnection Connect(uint handleNumber = 1, ulong id = 20, string name = "Remote Steam user", bool complete = true)
    {
        var handle = new HSteamNetConnection(handleNumber);
        SteamFriends.Names[new(id)] = name;
        SteamNetworkingSockets.Connections[handle] = new()
        {
            m_hListenSocket = new(1),
            m_eState = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting,
            m_identityRemote = new() { SteamId = new(id) }
        };
        Notify(handle, id, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting);
        if (complete) Notify(handle, id, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        return handle;
    }

    [Fact]
    public void ConnectedAdmitsRemoteSteamIdentityAndPersonaName()
    {
        var handle = Connect();
        Assert.True(server.TryGetTestConnection(20, out var registered));
        Assert.Equal(handle, Assert.IsType<HSteamNetConnection>(registered));
        var player = Assert.IsType<Player>(Session.Players[20]);
        Assert.Equal("Remote Steam user", player.UserName);
        Assert.Single(Connections);
    }

    [Fact]
    public void AcceptedConnectionIsNotRegisteredBeforeAdmission()
    {
        var handle = Connect(complete: false);
        Assert.True(Connections.Contains(handle));
        Assert.False(Session.Players.ContainsKey(20));
        Assert.False(server.TryGetTestConnection(20, out _));

        Notify(handle, 20, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        var member = Session.Players[20];
        Notify(handle, 20, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Assert.Same(member, Session.Players[20]);
        Assert.True(server.TryGetTestConnection(20, out var registered));
        Assert.Equal(handle, Assert.IsType<HSteamNetConnection>(registered));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[unknown]")]
    public void MissingNameRejectsAttemptAndRequestsCacheForLaterConnection(string name)
    {
        var old = Connect(name: name);
        Assert.Single(Session.Players);
        Assert.Empty(Connections);
        Assert.False(server.TryGetTestConnection(20, out _));
        Assert.Equal(new CSteamID(20), SteamFriends.LastRequestedIdentity);

        SteamFriends.Names[new(20)] = "Cached remote name";
        Notify(old, 20, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected);
        Assert.False(Session.Players.ContainsKey(20));
        Connect(2, name: "Cached remote name");
        Assert.Equal("Cached remote name", Session.Players[20].UserName);
    }

    [Fact]
    public void DuplicateSteamIdentityCannotTakeOverOrRemoveOriginalMembership()
    {
        var originalHandle = Connect();
        var original = Session.Players[20];
        SteamNetworkingSockets.CloseFailures.Add(new(2));
        var rejected = Connect(2);
        Assert.Same(original, Session.Players[20]);
        Assert.True(Connections.Contains(originalHandle));
        Assert.True(Connections.Contains(rejected));

        SteamNetworkingSockets.CloseFailures.Clear();
        Notify(rejected, 20, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer);
        Assert.Same(original, Session.Players[20]);
        Assert.False(Connections.Contains(rejected));
        Assert.True(server.TryGetTestConnection(20, out var registered));
        Assert.Equal(originalHandle, Assert.IsType<HSteamNetConnection>(registered));
    }

    [Fact]
    public void StaleDisconnectCannotRemoveSameIdReconnection()
    {
        var old = Connect();
        Notify(old, 20, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer);
        Assert.False(Session.Players.ContainsKey(20));
        var current = Connect(2);
        var player = Session.Players[20];
        Notify(old, 20, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally);
        Assert.Same(player, Session.Players[20]);
        Assert.True(Connections.Contains(current));
        Assert.True(server.TryGetTestConnection(20, out var registered));
        Assert.Equal(current, Assert.IsType<HSteamNetConnection>(registered));
    }

    [Fact]
    public void RejectedAdmissionDoesNotRemoveExistingLogicalMember()
    {
        var existing = new Player(20, "Existing member");
        Assert.True(Session.TryAddPlayer(existing));
        var rejected = Connect();
        Assert.Same(existing, Session.Players[20]);
        Assert.Empty(Connections);
        Assert.Contains(rejected, SteamNetworkingSockets.Closed);
        Assert.False(server.TryGetTestConnection(20, out _));
    }

    [Fact]
    public void FailedNativeCleanupRetainsOwnershipForExplicitStopRetry()
    {
        var handle = Connect();
        SteamNetworkingSockets.CloseFailures.Add(handle);
        Notify(handle, 20, ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer);
        Assert.False(Session.Players.ContainsKey(20));
        Assert.False(server.TryGetTestConnection(20, out _));
        Assert.True(Connections.Contains(handle));

        Lifecycle.Handle(Lifecycle.Event.ClearHostSession, server.Stop);
        Assert.Equal(Lifecycle.Phase.ClearingSession, Lifecycle.Current);
        SteamNetworkingSockets.CloseFailures.Clear();
        Lifecycle.Handle(Lifecycle.Event.ClearHostSession, server.Stop);
        Assert.Empty(Connections);
        Assert.Equal(Lifecycle.Phase.Idle, Lifecycle.Current);
    }

    [Fact]
    public void StopReleasesCallbackAndAllowsSameInstanceRestart()
    {
        Connect();
        Lifecycle.Handle(Lifecycle.Event.ClearHostSession, server.Stop);
        Assert.Empty(Session.Players);
        Assert.Empty(Connections);
        Assert.False(server.TryGetTestConnection(20, out _));
        Assert.Equal(0, Callback<SteamNetConnectionStatusChangedCallback_t>.Count);
        Lifecycle.Handle(Lifecycle.Event.StartHost, server.Start);
        var current = Connect(2);
        Assert.True(Session.Players.ContainsKey(20));
        Assert.True(server.TryGetTestConnection(20, out var registered));
        Assert.Equal(current, Assert.IsType<HSteamNetConnection>(registered));
    }
}
