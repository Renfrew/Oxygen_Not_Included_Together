using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ONI_United.Networking.Sessions;

internal static class Session
{
    private static readonly Dictionary<ulong, Player> players = new();
    private static readonly ReadOnlyDictionary<ulong, Player> readOnlyPlayers = new(players);

    public static bool IsActive { get; private set; }
    public static bool IsHost { get; private set; }
    public static bool IsClient => IsActive && !IsHost;
    public static ulong LocalPlayerId { get; private set; }
    public static ulong HostPlayerId { get; private set; }
    public static IReadOnlyDictionary<ulong, Player> Players => readOnlyPlayers;

    internal static bool BeginHosting(ulong localPlayerId)
    {
        if (localPlayerId == 0 || IsActive || players.Count != 0)
            return false;

        IsActive = true;
        IsHost = true;
        LocalPlayerId = localPlayerId;
        HostPlayerId = localPlayerId;
        return true;
    }

    internal static bool AdmitClient(ulong localPlayerId, ulong hostPlayerId)
    {
        if (IsActive
            || players.Count != 0
            || localPlayerId == 0
            || hostPlayerId == 0
            || localPlayerId == hostPlayerId)
            return false;

        IsActive = true;
        IsHost = false;
        LocalPlayerId = localPlayerId;
        HostPlayerId = hostPlayerId;
        return true;
    }

    internal static bool TryAddPlayer(Player player)
    {
        if (player == null || player.PlayerId == 0 || players.ContainsKey(player.PlayerId))
            return false;

        players.Add(player.PlayerId, player);
        return true;
    }

    internal static bool TryGetPlayer(ulong playerId, out Player player)
    {
        if (players.TryGetValue(playerId, out var found))
        {
            player = found;
            return true;
        }

        player = null!;
        return false;
    }

    internal static bool RemovePlayer(ulong playerId) => players.Remove(playerId);

    internal static void Reset()
    {
        IsActive = false;
        IsHost = false;
        LocalPlayerId = 0;
        HostPlayerId = 0;
        players.Clear();
    }

    internal static void SetForTests(bool isHost, bool isActive, ulong localPlayerId, ulong hostPlayerId)
    {
        Reset();
        if (!isActive)
            return;

        IsActive = true;
        IsHost = isHost;
        LocalPlayerId = localPlayerId;
        HostPlayerId = hostPlayerId;
    }
}
