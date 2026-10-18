using System;

namespace ONI_United.Networking.Sessions;

internal static class SessionHandshakeRules
{
    internal const int MaximumPlayerNameLength = 64;

    internal static bool TryValidateClient(
        ulong claimedPlayerId,
        ulong transportPlayerId,
        ulong hostPlayerId,
        bool duplicatePlayerId,
        bool protocolCompatible,
        string playerName,
        out string acceptedName,
        out string rejectionReason)
    {
        acceptedName = string.Empty;
        rejectionReason = string.Empty;

        if (!protocolCompatible)
            rejectionReason = "Incompatible protocol or packet registry.";
        else if (claimedPlayerId == 0 || transportPlayerId == 0 || claimedPlayerId != transportPlayerId)
            rejectionReason = "Player identity does not match the physical connection.";
        else if (claimedPlayerId == hostPlayerId)
            rejectionReason = "The host identity cannot join as a client.";
        else if (duplicatePlayerId)
            rejectionReason = "Player identity is already in this session.";
        else if (string.IsNullOrWhiteSpace(playerName))
            rejectionReason = "A player name is required.";
        else if (playerName.Length > MaximumPlayerNameLength)
            rejectionReason = "Player name is too long.";
        else
        {
            foreach (char character in playerName)
            {
                if (char.IsControl(character))
                {
                    rejectionReason = "Player name contains invalid characters.";
                    break;
                }
            }
        }

        if (rejectionReason.Length > 0)
            return false;

        acceptedName = playerName.Trim();
        if (acceptedName.Length == 0)
        {
            rejectionReason = "A player name is required.";
            return false;
        }

        return true;
    }
}
