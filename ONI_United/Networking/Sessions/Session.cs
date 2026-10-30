using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ONI_United.Networking.Sessions;

internal static class Session
{
    private static readonly Dictionary<ulong, Player> players = [];
    private static readonly ReadOnlyDictionary<ulong, Player> readOnlyPlayers = new(players);

    public static bool IsActive => HostPlayerId != 0;
    public static bool IsHost => IsActive && LocalPlayerId == HostPlayerId;
    public static bool IsClient => IsActive && !IsHost;
    public static ulong LocalPlayerId { get; private set; }
    public static ulong HostPlayerId { get; private set; }
    public static IReadOnlyDictionary<ulong, Player> Players => readOnlyPlayers;

    internal static bool BeginHosting(Player host)
    {
        if (host == null || host.PlayerId == 0 || IsActive || players.Count != 0)
            return false;

        players.Add(host.PlayerId, host);
        LocalPlayerId = host.PlayerId;
        HostPlayerId = host.PlayerId;
        return true;
    }

    internal static bool AdmitClient(Player local, Player host)
    {
        if (local == null || host == null || local.PlayerId == 0 || host.PlayerId == 0
            || local.PlayerId == host.PlayerId || IsActive || players.Count != 0)
            return false;

        players.Add(local.PlayerId, local);
        players.Add(host.PlayerId, host);
        LocalPlayerId = local.PlayerId;
        HostPlayerId = host.PlayerId;
        return true;
    }

    internal static bool TryAddPlayer(Player player)
    {
        if (!IsActive || player == null || player.PlayerId == 0 || players.ContainsKey(player.PlayerId))
            return false;

        players.Add(player.PlayerId, player);
        return true;
    }

    internal static bool TryGetPlayer(ulong playerId, out Player player) =>
        players.TryGetValue(playerId, out player!);

    internal static bool UpdatePlayerName(ulong playerId, string userName)
    {
        if (!players.TryGetValue(playerId, out var player))
            return false;

        player.UserName = userName ?? string.Empty;
        return true;
    }

    internal static bool RemovePlayer(ulong playerId) =>
        playerId != HostPlayerId && players.Remove(playerId);

    internal static void Reset()
    {
        players.Clear();
        LocalPlayerId = 0;
        HostPlayerId = 0;
    }

    internal static void SetForTests(bool isHost, bool isActive, ulong localPlayerId, ulong hostPlayerId)
    {
        Reset();
        // Diagnostics can request a role without an established transport/target identity.
        LocalPlayerId = isActive ? (localPlayerId == 0 ? 1UL : localPlayerId) : 0;
        ulong diagnosticHostId = !isActive ? 0 : isHost ? LocalPlayerId
            : hostPlayerId != 0 && hostPlayerId != LocalPlayerId ? hostPlayerId
            : LocalPlayerId == 1 ? 2UL : 1UL;
        if (isActive)
        {
            players.Add(LocalPlayerId, new Player(LocalPlayerId));
            if (!isHost) players.Add(diagnosticHostId, new Player(diagnosticHostId));
        }
        HostPlayerId = diagnosticHostId;
    }
}
