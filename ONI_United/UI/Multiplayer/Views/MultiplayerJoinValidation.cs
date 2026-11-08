using System;
using System.Net;

namespace ONI_United.UI.Multiplayer.Views
{
    // Offline form validation shared by Join availability and the final domain action.
    internal static class MultiplayerJoinValidation
    {
        internal static bool TryValidate(string address, string port, string name, int nameLimit, out string error)
        {
            address = address?.Trim() ?? string.Empty;
            name = name?.Trim() ?? string.Empty;
            if (address.Length == 0)
            { error = "Enter a server address."; return false; }
            if (!IsValidTarget(address))
            { error = "Enter a valid IP address or hostname."; return false; }
            if (!int.TryParse(port, out int number) || number < 1 || number > 65535)
            { error = "Enter a port between 1 and 65535."; return false; }
            if (name.Length == 0 || name.Length > nameLimit)
            { error = $"Enter a player name of 1-{nameLimit} characters."; return false; }
            foreach (char character in name)
                if (char.IsControl(character))
                { error = "Player Name cannot contain control characters."; return false; }
            error = string.Empty;
            return true;
        }

        private static bool IsValidTarget(string address)
        {
            // Match LiteNetLib's IPAddress.TryParse path, including IPv6/scoped literals.
            if (IPAddress.TryParse(address, out _)) return true;
            // A malformed numeric IP must not be mistaken for a DNS name.
            bool numeric = true;
            foreach (char c in address)
                if (c != '.' && (c < '0' || c > '9')) { numeric = false; break; }
            if (numeric || Uri.CheckHostName(address) != UriHostNameType.Dns) return false;
            foreach (string label in address.TrimEnd('.').Split('.'))
                if (label.Length == 0 || label.Length > 63 || label[0] == '-' || label[label.Length - 1] == '-') return false;
            return true;
        }
    }
}
