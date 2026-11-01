// INACTIVE MIGRATION DRAFT: never constructed or selected by production.
using System;
using ONI_United.DebugTools;
using ONI_United.Networking.Sessions;
using Steamworks;

namespace ONI_United.Networking.Transport.Steamworks
{
    internal sealed class SteamworksClientDraft : ClientTransport
    {
        private HSteamNetConnection? connection;
        private CSteamID host;
        private Callback<SteamNetConnectionStatusChangedCallback_t> connectionStatusChanged;
        private bool connected;
        // A terminated attempt may still own a handle when native release fails.
        private bool ending;

        protected override void InternalConnect()
        {
            if (connection.HasValue) return; // Includes cleanup still awaiting an explicit Stop retry.

            host = new CSteamID(MultiplayerSession.HostUserID);
            if (!SteamManager.Initialized || !SteamLobby.InLobby || !host.IsValid())
            {
                OnConnectFailed("Join a Steam lobby with a valid host before connecting.");
                return;
            }

            try
            {
                connectionStatusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);

                var identity = new SteamNetworkingIdentity();
                identity.SetSteamID64(host.m_SteamID);

                int timeout = checked(Configuration.Instance.Client.TimeoutSeconds * 1000);
                var options = new SteamNetworkingConfigValue_t[2];
                options[0].m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial;
                options[1].m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected;
                for (int i = 0; i < options.Length; i++)
                {
                    options[i].m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32;
                    options[i].m_val.m_int32 = timeout;
                }

                var handle = SteamNetworkingSockets.ConnectP2P(ref identity, 0, options.Length, options);
                if (handle == HSteamNetConnection.Invalid)
                    throw new InvalidOperationException("Steam did not create a client connection.");
                connection = handle;
            }
            catch (Exception ex)
            {
                ReleaseConnection();
                OnConnectFailed(ex.Message);
            }
        }

        protected override void InternalStop()
        {
            if (!connection.HasValue) return;
            if (ending) ReleaseConnection();
            else EndConnection("Steam connection cancelled before establishment.");
        }

        protected override void InternalPoll()
        {
            if (!SteamManager.Initialized) return;
            SteamNetworkingSockets.RunCallbacks();
            if (!connection.HasValue || !connected || ending) return;

            var messages = new IntPtr[128];
            int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(connection.Value, messages, messages.Length);
            try
            {
                if (count < 0)
                    DebugConsole.LogWarning("[SteamworksClientDraft] Native receive failed.");
                // Drain only. No Session handshake, payload copying or gameplay dispatch.
            }
            finally
            {
                for (int i = 0; i < count; i++)
                {
                    if (messages[i] == IntPtr.Zero) continue;
                    try { SteamNetworkingMessage_t.Release(messages[i]); }
                    catch (Exception ex)
                    {
                        DebugConsole.LogWarning("[SteamworksClientDraft] Native message release failed: " + ex.Message);
                    }
                }
            }
        }

        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t data)
        {
            if (!connection.HasValue || data.m_hConn != connection.Value || ending) return;

            switch (data.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (connected) return;
                    // Queued event data may be older than the actual native state.
                    if (!SteamNetworkingSockets.GetConnectionInfo(connection.Value, out var info)
                        || info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected
                        || info.m_identityRemote.GetSteamID() != host)
                    {
                        EndConnection("Steam did not confirm a connected native peer for the intended host.");
                        return;
                    }
                    connected = true;
                    OnConnected();
                    return;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    EndConnection("Steam connection ended: " + data.m_info.m_eState);
                    return;
            }
        }

        private void EndConnection(string reason)
        {
            ending = true;
            bool wasConnected = connected;
            connected = false;
            // Release before notifying application callbacks, which may synchronously reconnect.
            ReleaseConnection();
            if (wasConnected) OnDisconnected();
            else OnConnectFailed(reason);
        }

        private void ReleaseConnection()
        {
            try
            {
                if (connection.HasValue)
                {
                    if (!SteamNetworkingSockets.CloseConnection(connection.Value, 0, "Client disconnect", false)
                        && SteamNetworkingSockets.GetConnectionInfo(connection.Value, out _))
                    {
                        OnStopFailed("Steam did not confirm client connection release.");
                        return;
                    }
                    connection = null;
                }

                connectionStatusChanged?.Dispose();
                connectionStatusChanged = null;
                host = default;
                ending = false;
            }
            catch (Exception ex)
            {
                OnStopFailed(ex.Message);
            }
        }
    }
}
