using ONI_United.Networking.Sessions;
using System.Collections.Generic;
using Xunit;

namespace Shared.Tests.Networking;

public sealed class SessionMvpTests
{
    [Fact]
    public void HostIsRegisteredBeforeWorldLoads()
    {
        SessionController.Leave();

        Assert.True(SessionController.BeginHosting(10, new Player(10, "Host")));

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
        SessionController.Leave();
        Assert.False(Session.IsActive);
        Assert.Equal(0UL, Session.HostPlayerId);
        Assert.Empty(Session.Players);

        Assert.True(SessionController.AdmitClientSession(
            21, 10, new Player(21, "Client"), new Player(10, "Host")));

        Assert.True(Session.IsActive);
        Assert.True(Session.IsClient);
        Assert.False(Session.IsHost);
        Assert.Equal(21UL, Session.LocalPlayerId);
        Assert.Equal(10UL, Session.HostPlayerId);
        Assert.Equal(2, Session.Players.Count);
    }

    [Fact]
    public void RemotePlayerIsAddedOnlyAfterAdmissionAndCapacityUsesAdmittedMembers()
    {
        SessionController.Leave();
        Assert.True(SessionController.BeginHosting(10, new Player(10, "Host")));

        Assert.False(Session.Players.ContainsKey(21));
        Assert.False(SessionController.CanAdmitRemotePlayer(1));
        Assert.True(SessionController.CanAdmitRemotePlayer(2));
        Assert.True(SessionController.AdmitRemotePlayer(new Player(21, "Guest")));
        Assert.False(SessionController.CanAdmitRemotePlayer(2));
        Assert.Equal(2, Session.Players.Count);
    }

    [Fact]
    public void DuplicateAdmissionIsRejectedAndDisconnectRemovesMember()
    {
        SessionController.Leave();
        Assert.True(SessionController.BeginHosting(10, new Player(10, "Host")));

        var admitted = new Player(21, "Guest");
        Assert.True(SessionController.AdmitRemotePlayer(admitted));
        Assert.False(SessionController.AdmitRemotePlayer(new Player(21, "Duplicate")));
        Assert.Same(admitted, Session.Players[21]);

        Assert.True(SessionController.RemovePlayer(21));
        Assert.False(SessionController.RemovePlayer(21));
        Assert.Single(Session.Players);
    }

    [Fact]
    public void AdmittedPlayerInformationCanBeUpdatedButLobbyOnlyIdentityCannotBeAdded()
    {
        SessionController.Leave();
        Assert.True(SessionController.BeginHosting(10, new Player(10, "Host")));

        Assert.False(SessionController.UpdatePlayerInformation(21, "Lobby name"));
        Assert.True(SessionController.AdmitRemotePlayer(new Player(21, "Old name")));
        Assert.True(SessionController.UpdatePlayerInformation(21, "New name"));
        Assert.Equal("New name", Session.Players[21].UserName);
        Assert.False(SessionController.UpdatePlayerInformation(22, "Unknown"));
    }

    [Fact]
    public void PlayerCollectionIsReadOnlyAndContainsOnlyAdmittedPlayers()
    {
        SessionController.Leave();
        Assert.True(SessionController.BeginHosting(10, new Player(10, "Host")));
        Assert.True(SessionController.AdmitRemotePlayer(new Player(21, "Guest")));

        var mutableView = Assert.IsAssignableFrom<IDictionary<ulong, Player>>(Session.Players);
        Assert.Throws<System.NotSupportedException>(() => mutableView.Add(22, new Player(22, "Other")));
        Assert.DoesNotContain(22UL, Session.Players.Keys);
    }

    [Fact]
    public void SessionHostIsNotSetToAnUnverifiedTargetBeforeAdmission()
    {
        SessionController.Leave();
        Assert.False(Session.IsActive);
        Assert.Equal(0UL, Session.HostPlayerId);

        Assert.True(SessionController.AdmitClientSession(
            21, 10, new Player(21, "Client"), new Player(10, "Host")));
        Assert.Equal(10UL, Session.HostPlayerId);
    }

    [Fact]
    public void LeaveResetsRoleAndRemovesAllPlayers()
    {
        SessionController.Leave();
        Assert.True(SessionController.BeginHosting(10, new Player(10, "Host")));
        Assert.True(SessionController.AdmitRemotePlayer(new Player(21, "Guest")));

        SessionController.Leave();

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
        SessionController.Leave();

        Assert.False(SessionController.BeginHosting(0, new Player(0, "Invalid")));
        Assert.False(SessionController.AdmitClientSession(21, 0, new Player(21), new Player(0)));
        Assert.False(SessionController.AdmitClientSession(10, 10, new Player(10), new Player(10)));
        Assert.False(Session.IsActive);

        Assert.True(SessionController.AdmitClientSession(21, 10, new Player(21), new Player(10)));
        Assert.True(Session.IsClient);
        Assert.False(Session.IsHost);
        Assert.False(SessionController.BeginHosting(21, new Player(21)));
        Assert.True(Session.IsClient);
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
