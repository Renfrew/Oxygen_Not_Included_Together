using ONI_United.Networking.Sessions;
using System.Collections.Generic;
using Xunit;

namespace Shared.Tests.Networking;

public sealed class SessionMvpTests
{
    [Fact]
    public void HostIsRegisteredBeforeWorldLoads()
    {
        Session.Reset();

        Assert.True(Session.BeginHosting(new Player(10, "Host")));

        Assert.True(Session.IsActive);
        Assert.True(Session.IsHost);
        Assert.False(Session.IsClient);
        Assert.Equal(10UL, Session.LocalPlayerId);
        Assert.Equal(10UL, Session.HostPlayerId);
        Assert.Single(Session.Players);
        Assert.Equal("Host", Session.Players[10].UserName);
    }

    [Fact]
    public void ClientRoleAndMembershipAreCreatedOnlyBySuccessfulAdmission()
    {
        Session.Reset();
        Assert.False(Session.IsActive);
        Assert.False(Session.IsHost);
        Assert.False(Session.IsClient);
        Assert.Equal(0UL, Session.LocalPlayerId);
        Assert.Equal(0UL, Session.HostPlayerId);
        Assert.Empty(Session.Players);

        Assert.True(Session.AdmitClient(
            new Player(21, "Client"), new Player(10, "Host")));

        Assert.True(Session.IsActive);
        Assert.True(Session.IsClient);
        Assert.False(Session.IsHost);
        Assert.Equal(21UL, Session.LocalPlayerId);
        Assert.Equal(10UL, Session.HostPlayerId);
        Assert.Equal(2, Session.Players.Count);
    }

    [Fact]
    public void AuthorizedPlayerInsertionUpdatesLogicalMembership()
    {
        Session.Reset();
        Assert.True(Session.BeginHosting(new Player(10, "Host")));

        Assert.False(Session.Players.ContainsKey(21));
        Assert.True(Session.TryAddPlayer(new Player(21, "Guest")));
        Assert.Equal(2, Session.Players.Count);
    }

    [Fact]
    public void DuplicateAdmissionIsRejectedAndDisconnectRemovesMember()
    {
        Session.Reset();
        Assert.True(Session.BeginHosting(new Player(10, "Host")));

        var admitted = new Player(21, "Guest");
        Assert.True(Session.TryAddPlayer(admitted));
        Assert.False(Session.TryAddPlayer(new Player(21, "Duplicate")));
        Assert.Same(admitted, Session.Players[21]);

        Assert.True(Session.RemovePlayer(21));
        Assert.False(Session.RemovePlayer(21));
        Assert.Single(Session.Players);
    }

    [Fact]
    public void AdmittedPlayerInformationCanBeUpdatedButLobbyOnlyIdentityCannotBeAdded()
    {
        Session.Reset();
        Assert.True(Session.BeginHosting(new Player(10, "Host")));

        Assert.False(Session.UpdatePlayerName(21, "Lobby name"));
        Assert.True(Session.TryAddPlayer(new Player(21, "Old name")));
        Assert.True(Session.UpdatePlayerName(21, "New name"));
        Assert.Equal("New name", Session.Players[21].UserName);
        Assert.False(Session.UpdatePlayerName(22, "Unknown"));
    }

    [Fact]
    public void PlayerCollectionIsReadOnlyAndContainsOnlyAdmittedPlayers()
    {
        Session.Reset();
        Assert.True(Session.BeginHosting(new Player(10, "Host")));
        Assert.True(Session.TryAddPlayer(new Player(21, "Guest")));

        var mutableView = Assert.IsAssignableFrom<IDictionary<ulong, Player>>(Session.Players);
        Assert.Throws<System.NotSupportedException>(() => mutableView.Add(22, new Player(22, "Other")));
        Assert.DoesNotContain(22UL, Session.Players.Keys);
    }

    [Fact]
    public void SessionHostIsNotSetToAnUnverifiedTargetBeforeAdmission()
    {
        Session.Reset();
        Assert.False(Session.IsActive);
        Assert.Equal(0UL, Session.HostPlayerId);

        Assert.True(Session.AdmitClient(
            new Player(21, "Client"), new Player(10, "Host")));
        Assert.Equal(10UL, Session.HostPlayerId);
    }

    [Fact]
    public void LeaveResetsRoleAndRemovesAllPlayers()
    {
        Session.Reset();
        Assert.True(Session.BeginHosting(new Player(10, "Host")));
        Assert.True(Session.TryAddPlayer(new Player(21, "Guest")));

        Session.Reset();

        Assert.False(Session.IsActive);
        Assert.False(Session.IsHost);
        Assert.False(Session.IsClient);
        Assert.Equal(0UL, Session.LocalPlayerId);
        Assert.Equal(0UL, Session.HostPlayerId);
        Assert.Empty(Session.Players);
    }

    [Fact]
    public void RoleTransitionsRejectInvalidIdentitiesAndKeepRolesConsistent()
    {
        Session.Reset();

        Assert.False(Session.BeginHosting(new Player(0, "Invalid")));
        Assert.False(Session.AdmitClient(new Player(21), new Player(0)));
        Assert.False(Session.AdmitClient(new Player(10), new Player(10)));
        Assert.False(Session.IsActive);

        Assert.True(Session.AdmitClient(new Player(21), new Player(10)));
        Assert.True(Session.IsClient);
        Assert.False(Session.IsHost);
        Assert.False(Session.BeginHosting(new Player(21)));
        Assert.True(Session.IsClient);
    }

    [Fact]
    public void PlayerInsertionRequiresAnActiveSessionAndValidUniqueIdentity()
    {
        Session.Reset();
        Assert.False(Session.TryAddPlayer(new Player(21)));
        Assert.False(Session.TryAddPlayer(null!));
        Assert.Empty(Session.Players);

        Assert.True(Session.BeginHosting(new Player(10)));
        Assert.False(Session.TryAddPlayer(new Player(0)));
        Assert.False(Session.TryAddPlayer(null!));
        Assert.False(Session.TryAddPlayer(new Player(10)));
        Assert.Single(Session.Players);
    }

    [Fact]
    public void ClientCanApplyAuthorizedRosterDataWithoutHostAdmissionPolicy()
    {
        Session.Reset();
        Assert.True(Session.AdmitClient(new Player(21), new Player(10)));
        Assert.True(Session.TryAddPlayer(new Player(22, "Admitted peer")));
        Assert.Equal(3, Session.Players.Count);
        Assert.True(Session.RemovePlayer(22));
        Assert.Equal(2, Session.Players.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OrdinaryRemovalCannotRemoveTheHost(bool hosting)
    {
        Session.Reset();
        Assert.True(hosting ? Session.BeginHosting(new Player(10))
            : Session.AdmitClient(new Player(21), new Player(10)));
        Assert.False(Session.RemovePlayer(10));
        Assert.True(Session.IsActive);
        Assert.True(Session.Players.ContainsKey(Session.HostPlayerId));
    }

    [Theory]
    [InlineData(0UL, 10UL)]
    [InlineData(21UL, 0UL)]
    [InlineData(10UL, 10UL)]
    public void InvalidClientInitializationLeavesSessionInactiveAndEmpty(ulong localId, ulong hostId)
    {
        Session.Reset();
        Assert.False(Session.AdmitClient(new Player(localId), new Player(hostId)));
        Assert.False(Session.IsActive);
        Assert.False(Session.IsHost);
        Assert.False(Session.IsClient);
        Assert.Equal(0UL, Session.LocalPlayerId);
        Assert.Equal(0UL, Session.HostPlayerId);
        Assert.Empty(Session.Players);
    }

    [Fact]
    public void FailedInitializationPreservesExistingSessionAndMembers()
    {
        Session.Reset();
        var host = new Player(10, "Original host");
        Assert.True(Session.BeginHosting(host));
        Assert.True(Session.TryAddPlayer(new Player(21)));
        Assert.False(Session.AdmitClient(new Player(22), new Player(11)));
        Assert.False(Session.BeginHosting(new Player(11)));
        Assert.True(Session.IsHost);
        Assert.Equal(10UL, Session.LocalPlayerId);
        Assert.Equal(10UL, Session.HostPlayerId);
        Assert.Same(host, Session.Players[10]);
        Assert.Equal(2, Session.Players.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SuccessfulInitializationCommitsCompleteDataAndResetIsIdempotent(bool hosting)
    {
        Session.Reset();
        Assert.False(Session.BeginHosting(null!));
        Assert.False(Session.AdmitClient(null!, new Player(10)));
        Assert.False(Session.AdmitClient(new Player(21), null!));
        Assert.False(Session.IsActive);
        Assert.Empty(Session.Players);

        Assert.True(hosting ? Session.BeginHosting(new Player(10))
            : Session.AdmitClient(new Player(21), new Player(10)));
        Assert.True(Session.IsActive);
        Assert.True(Session.Players.ContainsKey(Session.LocalPlayerId));
        Assert.True(Session.Players.ContainsKey(Session.HostPlayerId));
        Assert.Equal(hosting ? 1 : 2, Session.Players.Count);
        Assert.Equal(hosting, Session.IsHost);
        Assert.Equal(!hosting, Session.IsClient);

        if (hosting) Assert.False(Session.BeginHosting(new Player(10)));
        Assert.False(Session.AdmitClient(new Player(22), new Player(11)));
        Session.Reset();
        Session.Reset();
        Assert.False(Session.IsActive);
        Assert.False(Session.IsHost);
        Assert.False(Session.IsClient);
        Assert.Equal(0UL, Session.LocalPlayerId);
        Assert.Equal(0UL, Session.HostPlayerId);
        Assert.Empty(Session.Players);
    }

    [Fact]
    public void DiagnosticOverridesKeepValidRecordsAcrossActivationChanges()
    {
        Session.Reset();
        Session.SetForTests(false, true, 0, 0);
        Assert.True(Session.IsClient);
        Assert.NotEqual(0UL, Session.LocalPlayerId);
        Assert.NotEqual(0UL, Session.HostPlayerId);
        Assert.NotEqual(Session.LocalPlayerId, Session.HostPlayerId);
        Assert.True(Session.Players.ContainsKey(Session.LocalPlayerId));
        Assert.True(Session.Players.ContainsKey(Session.HostPlayerId));
        Session.SetForTests(true, true, 9, 0);
        Assert.True(Session.IsHost);
        Assert.Equal(9UL, Session.HostPlayerId);
        Assert.Single(Session.Players);
        Session.SetForTests(true, false, 9, 9);
        Assert.False(Session.IsActive);
        Assert.False(Session.IsHost);
        Assert.False(Session.IsClient);
        Assert.Equal(0UL, Session.LocalPlayerId);
        Assert.Equal(0UL, Session.HostPlayerId);
        Assert.Empty(Session.Players);
    }

    [Theory]
    [InlineData(true, true, 0UL, 0UL, 1UL, 1UL)]
    [InlineData(true, true, 9UL, 10UL, 9UL, 9UL)]
    [InlineData(false, true, 0UL, 0UL, 1UL, 2UL)]
    [InlineData(false, true, 0UL, 1UL, 1UL, 2UL)]
    [InlineData(false, true, 9UL, 0UL, 9UL, 1UL)]
    [InlineData(false, true, 9UL, 9UL, 9UL, 1UL)]
    [InlineData(false, true, 9UL, 10UL, 9UL, 10UL)]
    [InlineData(true, false, 9UL, 9UL, 0UL, 0UL)]
    [InlineData(false, false, 9UL, 10UL, 0UL, 0UL)]
    public void DiagnosticOverridesDeriveRequestedRolesFromSelectedIdentities(
        bool isHost, bool isActive, ulong localId, ulong hostId,
        ulong expectedLocalId, ulong expectedHostId)
    {
        Session.Reset();
        Assert.True(Session.BeginHosting(new Player(10)));

        Session.SetForTests(isHost, isActive, localId, hostId);

        Assert.Equal(isActive, Session.IsActive);
        Assert.Equal(isActive && isHost, Session.IsHost);
        Assert.Equal(isActive && !isHost, Session.IsClient);
        Assert.Equal(expectedLocalId, Session.LocalPlayerId);
        Assert.Equal(expectedHostId, Session.HostPlayerId);
        Assert.Equal(isActive ? isHost ? 1 : 2 : 0, Session.Players.Count);
        if (isActive)
        {
            Assert.Equal(expectedLocalId, Session.Players[expectedLocalId].PlayerId);
            Assert.Equal(expectedHostId, Session.Players[expectedHostId].PlayerId);
        }
    }

    [Fact]
    public void HandshakeRulesBindIdentityAndValidateNames()
    {
        Assert.True(SessionHandshakeRules.TryValidateClient(
            21, 21, 10, false, true, "  Colony Friend  ", out string acceptedName, out _));
        Assert.Equal("Colony Friend", acceptedName);

        Assert.False(SessionHandshakeRules.TryValidateClient(
            22, 21, 10, false, true, "Client", out _, out _));
        Assert.False(SessionHandshakeRules.TryValidateClient(
            21, 21, 10, true, true, "Client", out _, out _));
        Assert.False(SessionHandshakeRules.TryValidateClient(
            21, 21, 10, false, false, "Client", out _, out _));
        Assert.False(SessionHandshakeRules.TryValidateClient(
            21, 21, 10, false, true, "bad\nname", out _, out _));
    }
}
