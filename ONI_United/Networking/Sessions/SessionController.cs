namespace ONI_United.Networking.Sessions;

internal static class SessionController
{
    internal static event System.Action<bool>? SessionActiveChanged;

    internal static bool BeginHosting(ulong localPlayerId, Player host)
    {
        if (host == null || host.PlayerId != localPlayerId)
            return false;
        if (Session.IsActive)
            return Session.IsHost
                && Session.LocalPlayerId == localPlayerId
                && Session.Players.ContainsKey(localPlayerId);
        if (Session.Players.Count != 0)
            return false;

        if (!Session.BeginHosting(localPlayerId) || !Session.TryAddPlayer(host))
            return false;

        SessionActiveChanged?.Invoke(true);
        return true;
    }

    internal static bool AdmitClientSession(ulong localPlayerId, ulong hostPlayerId, Player local, Player host)
    {
        if (local == null || host == null
            || local.PlayerId != localPlayerId
            || host.PlayerId != hostPlayerId
            || localPlayerId == 0
            || hostPlayerId == 0
            || localPlayerId == hostPlayerId
            || Session.IsActive
            || Session.Players.Count != 0)
            return false;

        if (!Session.AdmitClient(localPlayerId, hostPlayerId))
            return false;

        if (!Session.TryAddPlayer(local) || !Session.TryAddPlayer(host))
        {
            Session.Reset();
            return false;
        }

        SessionActiveChanged?.Invoke(true);
        return true;
    }

    internal static bool AdmitRemotePlayer(Player player)
    {
        if (!Session.IsHost || player == null || player.PlayerId == Session.HostPlayerId)
            return false;

        return Session.TryAddPlayer(player);
    }

    internal static bool CanAdmitRemotePlayer(int maxPlayers) =>
        Session.IsHost && maxPlayers > 0 && Session.Players.Count < maxPlayers;

    internal static bool UpdatePlayerInformation(ulong playerId, string userName)
    {
        if (!Session.TryGetPlayer(playerId, out Player player))
            return false;

        player.UserName = userName ?? string.Empty;
        return true;
    }

    internal static bool RemovePlayer(ulong playerId) =>
        playerId != Session.HostPlayerId && Session.RemovePlayer(playerId);

    internal static void Leave()
    {
        bool wasActive = Session.IsActive;
        Session.Reset();
        if (wasActive)
            SessionActiveChanged?.Invoke(false);
    }

    internal static void SetForTests(bool isHost, bool isActive, ulong localPlayerId, ulong hostPlayerId)
    {
        bool wasActive = Session.IsActive;
        Session.SetForTests(isHost, isActive, localPlayerId, hostPlayerId);
        if (wasActive != Session.IsActive)
            SessionActiveChanged?.Invoke(Session.IsActive);
    }
}
