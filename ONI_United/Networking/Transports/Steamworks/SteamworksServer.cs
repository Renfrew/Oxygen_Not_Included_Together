
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ONI_United.DebugTools;
using Steamworks;

namespace ONI_United.Networking.Transports.Steamworks
{
    internal class SteamworksServer : ServerTransport
    {
        private const int ReceiveBatchSize = 128;
        private HSteamListenSocket listenSocket = HSteamListenSocket.Invalid;
        private HSteamNetPollGroup pollGroup = HSteamNetPollGroup.Invalid;
        private Callback<SteamNetConnectionStatusChangedCallback_t> connectionStatusChanged;
        private bool lobbyCancellationPending;
        // Includes incoming handles whose rejection/retirement is not yet confirmed.
        // Physical ownership does not imply logical player admission.
        private readonly Dictionary<HSteamNetConnection, CSteamID> physicalConnections = [];

        protected override void InternalStart()
        {
            if (!SteamManager.Initialized || SteamLobby.InLobby || SteamLobby.HasPendingJoin)
            {
                OnStartFailed("Steam must be initialized and outside a lobby before hosting.");
                return;
            }

            // TODO: Check configuration options for the listen socket before creating it.
            listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, []);
            if (listenSocket == HSteamListenSocket.Invalid)
            {
                OnStartFailed("Steam failed to create its listen socket.");
                return;
            }

            pollGroup = SteamNetworkingSockets.CreatePollGroup();
            if (pollGroup == HSteamNetPollGroup.Invalid)
            {
                OnStartFailed("Steam failed to create its poll group.");
                return;
            }

            connectionStatusChanged ??= Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
            SteamLobby.CreateHostLobby(error =>
            {
                if (error != null) OnStartFailed(error);
                else OnStarted();
            });
        }

        protected override void InternalStop()
        {
            // SteamLobby retains the original native creation until it can release
            // its result. Do not replace that cancellation's completion on retry.
            if (lobbyCancellationPending) return;
            lobbyCancellationPending = true;

            Reset();

            SteamLobby.CancelHostLobby((success, error) =>
            {
                lobbyCancellationPending = false;
                if (success) OnStopped();
                else OnStopFailed(error);
            });
        }

        protected override void InternalPoll()
        {
            if (!SteamManager.Initialized || listenSocket == HSteamListenSocket.Invalid
                || pollGroup == HSteamNetPollGroup.Invalid || lobbyCancellationPending)
                return;

            var messages = new IntPtr[ReceiveBatchSize];
            int count = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(
                pollGroup, messages, messages.Length);
            try
            {
                if (count < 0)
                {
                    DebugConsole.LogWarning("[SteamworksServerDraft] Steam poll-group receive failed.");
                    return;
                }
                for (int i = 0; i < count; i++)
                {
                    if (messages[i] == IntPtr.Zero) continue;
                    try
                    {
                        var message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messages[i]);
                        if (!physicalConnections.TryGetValue(message.m_conn, out var identity) || !identity.IsValid())
                        {
                            DebugConsole.LogWarning("[SteamworksServerDraft] Dropped message from an unowned connection.");
                            continue;
                        }
                        if (message.m_cbSize < sizeof(int) || message.m_pData == IntPtr.Zero)
                        {
                            DebugConsole.LogWarning("[SteamworksServerDraft] Dropped invalid native packet buffer.");
                            continue;
                        }

                        // Drain only. PacketHandler and handshake authorization resolve the
                        // legacy NetworkRuntime.TransportServer, which cannot own this draft.
                        // Do not copy or dispatch payloads through a different server owner.
                    }
                    catch (Exception ex)
                    {
                        DebugConsole.LogWarning("[SteamworksServerDraft] Rejected native message: " + ex);
                    }
                }
            }
            finally
            {
                // Own the entire returned batch, including messages not processed after
                // a failure. Never retain native pointers beyond this synchronous call.
                for (int i = 0; i < count; i++)
                {
                    if (messages[i] == IntPtr.Zero) continue;
                    try { SteamNetworkingMessage_t.Release(messages[i]); }
                    catch (Exception ex)
                    {
                        // A failed release must not skip release attempts for the rest.
                        DebugConsole.LogWarning("[SteamworksServerDraft] Native message release failed: " + ex);
                    }
                }
            }
        }

        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t data)
        {
            var connection = data.m_hConn;
            switch (data.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    if (physicalConnections.ContainsKey(connection))
                        RetireConnection(connection, null);
                    return;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    AdmitConnection(connection);
                    return;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    break;

                default:
                    return;
            }

            if (listenSocket == HSteamListenSocket.Invalid
                || data.m_info.m_hListenSocket != listenSocket
                || physicalConnections.ContainsKey(connection))
                return;

            // The queued callback can be older than the current native state.
            if (!SteamNetworkingSockets.GetConnectionInfo(connection, out var info))
                return; // Steam confirms that this handle is already invalid.
            if (info.m_hListenSocket != listenSocket) return;

            var identity = info.m_identityRemote.GetSteamID();
            // Own before accepting or rejecting: failed closure must remain retryable.
            physicalConnections.Add(connection, identity);
            if (lobbyCancellationPending
                || pollGroup == HSteamNetPollGroup.Invalid
                || info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
            {
                RetireConnection(connection, "Host is not accepting this connection");
                return;
            }
            if (!identity.IsValid())
            {
                RetireConnection(connection, "Missing remote Steam identity");
                return;
            }

            foreach (var other in physicalConnections)
            {
                if (other.Key != connection && other.Value == identity)
                {
                    // Reject only the newcomer; preserve the original physical handle.
                    RetireConnection(connection, "Duplicate physical Steam identity");
                    return;
                }
            }

            var result = SteamNetworkingSockets.AcceptConnection(connection);
            if (result != EResult.k_EResultOK)
            {
                RetireConnection(connection, "Steam acceptance failed: " + result);
                return;
            }
            if (!SteamNetworkingSockets.SetConnectionPollGroup(connection, pollGroup))
                RetireConnection(connection, "Steam poll-group assignment failed");
        }

        private void AdmitConnection(HSteamNetConnection connection)
        {
            if (!physicalConnections.TryGetValue(connection, out var identity))
                return;
            if (TryGetConnection(identity.m_SteamID, out var admitted) && Equals(admitted, connection))
                return;

            if (listenSocket == HSteamListenSocket.Invalid || lobbyCancellationPending
                || !SteamNetworkingSockets.GetConnectionInfo(connection, out var info)
                || info.m_hListenSocket != listenSocket
                || info.m_identityRemote.GetSteamID() != identity
                || info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                RetireConnection(connection, "Connection is no longer established");
                return;
            }

            string name = SteamFriends.GetFriendPersonaName(identity);
            if (string.IsNullOrWhiteSpace(name) || name == "[unknown]")
            {
                // Warm Steam's cache for a later connection attempt; do not retain
                // pending admission state or admit a placeholder name.
                SteamFriends.RequestUserInformation(identity, true);
                RetireConnection(connection, "Steam persona name is not available yet");
                return;
            }

            if (!OnConnected(identity.m_SteamID, name, connection))
            {
                RetireConnection(connection, "Admission rejected");
                return;
            }
        }

        private bool RetireConnection(HSteamNetConnection connection, string reason)
        {
            if (!physicalConnections.TryGetValue(connection, out var identity)) return true;
            OnDisconnect(identity.m_SteamID, connection);
            try
            {
                if (!SteamNetworkingSockets.CloseConnection(connection, 0, reason, false)
                    && SteamNetworkingSockets.GetConnectionInfo(connection, out var _))
                {
                    DebugConsole.LogWarning("[SteamworksServerDraft] Connection release remains unconfirmed: "
                        + connection.m_HSteamNetConnection);
                    return false;
                }
                physicalConnections.Remove(connection);
                return true;
            }
            catch (Exception ex)
            {
                DebugConsole.LogWarning("[SteamworksServerDraft] Connection release failed: " + ex);
                return false;
            }
        }

        protected override void Reset()
        {
            foreach (var connection in new List<HSteamNetConnection>(physicalConnections.Keys))
            {
                if (!RetireConnection(connection, "Host shutdown"))
                    ModLog.Warning($"[SteamworksServer] Failed to close client connection: {connection.m_HSteamNetConnection}.");
            }

            try
            {
                if (listenSocket != HSteamListenSocket.Invalid)
                {
                    if (!SteamNetworkingSockets.CloseListenSocket(listenSocket))
                       throw new Exception("Steam did not confirm listener release.");

                    listenSocket = HSteamListenSocket.Invalid;
                }
            }
            catch (Exception ex) {
                ModLog.Warning($"[SteamworksServer] Failed to close listen socket: {ex}");
            }

            try
            {
                if (pollGroup != HSteamNetPollGroup.Invalid)
                {
                    if (!SteamNetworkingSockets.DestroyPollGroup(pollGroup))
                        throw new Exception("Steam did not confirm poll-group release.");

                    pollGroup = HSteamNetPollGroup.Invalid;
                }
            }
            catch (Exception ex) {
                ModLog.Warning($"[SteamworksServer] Failed to destroy poll group: {ex}");
            }

            try
            {
                connectionStatusChanged?.Dispose();
                connectionStatusChanged = null;
            }
            catch (Exception ex) {
                ModLog.Warning($"[SteamworksServer] Failed to dispose connection status changed: {ex}");
            }
        }
    }
}
