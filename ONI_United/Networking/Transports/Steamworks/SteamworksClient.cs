using System;
using ONI_United.DebugTools;
using ONI_United.Networking.Sessions;
using Steamworks;

namespace ONI_United.Networking.Transports.Steamworks
{
    internal sealed class SteamworksClient : ClientTransport
    {
        private HSteamNetConnection connection = HSteamNetConnection.Invalid;
        private Callback<SteamNetConnectionStatusChangedCallback_t> connectionStatusChanged;
        private Callback<PersonaStateChange_t> personaStateChanged;
        private CSteamID host = CSteamID.Nil;
        private bool connected;

        protected override void InternalConnect()
        {
            if (connection != HSteamNetConnection.Invalid)
                throw new InvalidOperationException("Connect was invoked while a Steam connection is still owned.");

            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(MultiplayerSession.HostUserID);
            connection = SteamNetworkingSockets.ConnectP2P(
                ref identity, 0, 0, Array.Empty<SteamNetworkingConfigValue_t>());

            if (connection == HSteamNetConnection.Invalid)
            {
                OnConnectFail("Steam did not create a connection.");
                return;
            }

            connectionStatusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
        }

        protected override void InternalDisconnect()
        {
            Cleanup();
            OnDisconnected();
        }

        protected override void InternalPoll()
        {
            if (!SteamManager.Initialized)
                throw new InvalidOperationException("Poll was invoked while Steam is not initialized.");
            if (connectionStatusChanged == null)
                throw new InvalidOperationException("Poll was invoked without an owned Steam connection callback.");

            SteamAPI.RunCallbacks();
            // A status callback may have ended the connection.
            if (connection != HSteamNetConnection.Invalid)
                ModLog.Info("Polling Steam connection.");
        }



        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t data)
        {
            // Steam delivers every connection's status change to every registered handler,
            // including other transports' connections and the destroy notice of a closed handle.
            if (data.m_hConn != connection)
                return;

            switch (data.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    host = data.m_info.m_identityRemote.GetSteamID();
                    // A lobby member's persona name can arrive later through PersonaStateChange_t.
                    if (SteamFriends.RequestUserInformation(host, true))
                        personaStateChanged = Callback<PersonaStateChange_t>.Create(OnPersonaStateChanged);
                    else
                        CompleteConnection();
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    bool wasConnected = connected;
                    Cleanup();
                    if (wasConnected) OnDisconnect();
                    else OnConnectFail("Steam connection failed: " + data.m_info.m_eState + ".");
                    break;
            }
        }

        private void OnPersonaStateChanged(PersonaStateChange_t data)
        {
            // Persona changes are broadcast for every known user and every kind of change.
            if (data.m_ulSteamID != host.m_SteamID || (data.m_nChangeFlags & EPersonaChange.k_EPersonaChangeName) == 0)
                return;

            personaStateChanged.Dispose();
            personaStateChanged = null;
            CompleteConnection();
        }

        private void CompleteConnection()
        {
            connected = true;
            OnConnected(host.m_SteamID, SteamFriends.GetFriendPersonaName(host));
        }

        private void Cleanup()
        {
            // CloseConnection returns false only for an unknown handle; otherwise it frees the handle.
            if (connection != HSteamNetConnection.Invalid)
                SteamNetworkingSockets.CloseConnection(connection, 0, "Client disconnecting", false);

            connection = HSteamNetConnection.Invalid;
            connectionStatusChanged?.Dispose();
            connectionStatusChanged = null;
            personaStateChanged?.Dispose();
            personaStateChanged = null;
            host = CSteamID.Nil;
            connected = false;
        }
    }
}
