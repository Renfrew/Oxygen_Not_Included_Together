using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ONI_United.DebugTools;
using ONI_United.Networking.Packets.Architecture;
using Shared.Profiling;
using ONI_United.Networking.Replication.Components;
using Steamworks;
using ONI_United.Networking.Sessions;
using ONI_United.Core;

namespace ONI_United.Networking.Transport.Steamworks
{
    public class SteamworksServer : TransportServer
    {
        public static HSteamListenSocket ListenSocket { get; private set; }
        public static HSteamNetPollGroup PollGroup { get; private set; }
        private Callback<SteamNetConnectionStatusChangedCallback_t> _connectionStatusChangedCallback;
        private static readonly Dictionary<uint, ulong> _clientIdsByConnection = new Dictionary<uint, ulong>();

        private Action<bool, string> cancellation;

        // Bandwidth tracking via per-connection GetConnectionRealTimeStatus
        private float _srvInBw, _srvOutBw;
        private int _srvInPps, _srvOutPps;

        private bool Prepare(object operation)
        {
            if (SteamManager.Initialized && !SteamLobby.InLobby && !SteamLobby.HasPendingJoin) return true;
            ReportStartupFailure(operation, "Steam must be initialized and outside a lobby before hosting.");
            return false;
        }

        protected override void StartProtocol(object operation)
        {
            if (!Prepare(operation)) return;

            // Create listen socket for P2P
            var options = new SteamNetworkingConfigValue_t[2];
            options[0].m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial;
            options[0].m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32;
            options[0].m_val.m_int32 = Configuration.Instance.Host.TimeoutSeconds * 1000;

            options[1].m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected;
            options[1].m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32;
            options[1].m_val.m_int32 = Configuration.Instance.Host.TimeoutSeconds * 1000;

            ListenSocket = SteamNetworkingSockets.CreateListenSocketP2P(
                    0, // Virtual port
                    options.Length,
                    options
            );

            if (ListenSocket.m_HSteamListenSocket == 0)
            {
                ReportStartupFailure(operation, "Steam failed to create its listen socket.");
                return;
            }

            PollGroup = SteamNetworkingSockets.CreatePollGroup();

            if (PollGroup.m_HSteamNetPollGroup == 0)
            {
                ReportStartupFailure(operation, "Steam failed to create its poll group.");
                return;
            }

            _connectionStatusChangedCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(
                data => OnConnectionStatusChanged(operation, data));

            SteamLobby.CreateHostLobby(operation, error =>
            {
                if (!NetworkRuntime.IsCurrentOperation(this, operation)) return;
                if (error != null) ReportStartupFailure(operation, error);
                else
                {
                    OxySyncChat.AddSystemMessage(string.Format(ONI_United.STRINGS.UI.MP_CHATWINDOW.CHAT_SERVER_STARTED, "Steam"));
                    ReportTransportStarted(operation);
                }
            });
        }

        public override void OnSessionPlayerAdmitted(ulong playerId)
        {
            if (playerId != Session.LocalPlayerId) return;
            SteamLobby.UpdateGameInfo();
        }

        protected override void CancelProtocol(Action<bool, string> completed)
        {
            if (cancellation != null) return;
            cancellation = completed;
            string resourceFailure = ReleaseSockets();
            SteamLobby.CancelHostLobby((success, error) =>
            {
                var finished = cancellation;
                cancellation = null;
                if (finished != null && success && resourceFailure == null)
                    OxySyncChat.AddSystemMessage(string.Format(ONI_United.STRINGS.UI.MP_CHATWINDOW.CHAT_SERVER_STOPPED, "Steam"));
                finished?.Invoke(success && resourceFailure == null, resourceFailure ?? error);
            });
        }

        protected override void DisposeProtocol()
        {
            cancellation = null;
            string failure = ReleaseSockets();
            // A pending native lobby result remains registered, but can only release
            // its original lobby and cannot call the destroyed Lifecycle singleton.
            SteamLobby.CancelHostLobby(null);
            if (failure != null) DebugConsole.LogError("[SteamworksServer] Disposal failed: " + failure);
        }

        private string ReleaseSockets()
        {
            var errors = new List<string>();
            try { CloseConnections(); }
            catch (Exception ex) { errors.Add(ex.ToString()); }
            if (ListenSocket.m_HSteamListenSocket != 0)
            {
                if (SteamNetworkingSockets.CloseListenSocket(ListenSocket))
                {
                    ListenSocket = default;
                    _clientIdsByConnection.Clear(); // Closing a listener closes its accepted connections.
                }
                else errors.Add("Steam did not confirm listener release.");
            }
            if (PollGroup.m_HSteamNetPollGroup != 0)
            {
                if (SteamNetworkingSockets.DestroyPollGroup(PollGroup)) PollGroup = default;
                else errors.Add("Steam did not confirm poll-group release.");
            }
            if (ListenSocket.m_HSteamListenSocket == 0 && PollGroup.m_HSteamNetPollGroup == 0
                && _clientIdsByConnection.Count == 0)
            {
                _connectionStatusChangedCallback?.Dispose();
                _connectionStatusChangedCallback = null;
            }
            return errors.Count == 0 ? null : string.Join("; ", errors);
        }

        public override void CloseConnections()
        {
            // Logical membership has already been deactivated by Stop's EventAction.
            // Physical handles, including connections not yet admitted, remain here.
            foreach (var id in new List<uint>(_clientIdsByConnection.Keys))
            {
                var connection = new HSteamNetConnection(id);
                if (!CloseConnection(connection, "Shutdown"))
                    throw new InvalidOperationException("Steam did not confirm connection release: " + id);
            }
        }

        public override void Update()
        {
            using var _ = Profiler.Scope();

            SteamAPI.RunCallbacks();
            SteamNetworkingSockets.RunCallbacks();
            UpdateServerBandwidth();
        }

        private void UpdateServerBandwidth()
        {
            float totalIn = 0f, totalOut = 0f;
            float totalPpsIn = 0f, totalPpsOut = 0f;

            foreach (var kvp in MultiplayerSession.ConnectedPlayers)
            {
                if (kvp.Value.Connection is HSteamNetConnection conn)
                {
                    SteamNetConnectionRealTimeStatus_t status = default;
                    SteamNetConnectionRealTimeLaneStatus_t laneStatus = default;
                    var res = SteamNetworkingSockets.GetConnectionRealTimeStatus(conn, ref status, 0, ref laneStatus);
                    if (res == EResult.k_EResultOK)
                    {
                        totalIn += status.m_flInBytesPerSec;
                        totalOut += status.m_flOutBytesPerSec;
                        totalPpsIn += status.m_flInPacketsPerSec;
                        totalPpsOut += status.m_flOutPacketsPerSec;
                    }
                }
            }

            _srvInBw = totalIn;
            _srvOutBw = totalOut;
            _srvInPps = (int)totalPpsIn;
            _srvOutPps = (int)totalPpsOut;
        }

        public override float IncomingBandwidth => _srvInBw;
        public override float OutgoingBandwidth => _srvOutBw;
        public override int IncomingPps => _srvInPps;
        public override int OutgoingPps => _srvOutPps;

        public override void OnMessageRecieved()
        {
            using var _ = Profiler.Scope();
            var operation = NetworkRuntime.ActiveOperation;

            var scope = Profiler.Scope();
            int totalBytes = 0;

            int maxMessagesPerPoll = Configuration.GetHostProperty<int>("MaxMessagesPerPoll");
            var messages = new IntPtr[maxMessagesPerPoll];
            int msgCount = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(PollGroup, messages, maxMessagesPerPoll);

            try
            {
                for (int i = 0; i < msgCount; i++)
                {
                    if (!NetworkRuntime.IsCurrentOperation(this, operation)) continue;
                    var msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messages[i]);
                    totalBytes += msg.m_cbSize;
                    byte[] bytes = new byte[msg.m_cbSize];
                    Marshal.Copy(msg.m_pData, bytes, 0, msg.m_cbSize);

                    try
                    {
                        HSteamNetConnection connection = msg.m_conn;
                        if (_clientIdsByConnection.TryGetValue(connection.m_HSteamNetConnection, out ulong senderId))
                            PacketHandler.HandleIncoming(bytes, senderId, connection);
                        else
                            DebugConsole.LogWarning("[SteamworksServer] Received data from an untracked Steam connection.");
                    }
                    catch (Exception ex)
                    {
                        DebugConsole.LogWarning($"[SteamworksServer] Rejected invalid incoming packet: {ex}");
                    }
                }
            }
            finally
            {
                for (int i = 0; i < msgCount; i++)
                {
                    if (messages[i] != IntPtr.Zero)
                        SteamNetworkingMessage_t.Release(messages[i]);
                }
            }
            scope.End(msgCount, totalBytes);
        }

        private void OnConnectionStatusChanged(object operation, SteamNetConnectionStatusChangedCallback_t data)
        {
            if (!NetworkRuntime.IsCurrentOperation(this, operation)) return;
            using var _ = Profiler.Scope();

            var conn = data.m_hConn;
            if (data.m_info.m_hListenSocket != ListenSocket || ListenSocket.m_HSteamListenSocket == 0)
                return;
            var clientId = data.m_info.m_identityRemote.GetSteamID();
            var state = data.m_info.m_eState;

            DebugConsole.Log($"[SteamworksServer] OnConnectionStatusChanged: state={state} from {clientId}");

            switch (state)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    _clientIdsByConnection[conn.m_HSteamNetConnection] = clientId.m_SteamID;
                    if (!Session.IsHost || !Lifecycle.IsHostGameplay
                        || Lifecycle.Current != Lifecycle.Phase.InGameSession)
                    {
                        CloseConnection(conn, "Host is not admitting players");
                        break;
                    }
                    TryAcceptConnection(conn, clientId);
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    // A queued Connected notification must not restore a locally retired handle.
                    if (_clientIdsByConnection.ContainsKey(conn.m_HSteamNetConnection))
                        OnClientConnected(conn, clientId);
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    OnClientClosed(conn, clientId);
                    break;
            }
        }

        private static void TryAcceptConnection(HSteamNetConnection conn, CSteamID clientId)
        {
            using var _ = Profiler.Scope();

            // Get connection info to check actual state
            SteamNetConnectionInfo_t info = default;
            if (!SteamNetworkingSockets.GetConnectionInfo(conn, out info))
            {
                DebugConsole.LogWarning($"[SteamworksServer] TryAcceptConnection: Could not get connection info for {clientId}");
                CloseConnection(conn, "Connection info is unavailable");
                return;
            }
            else
            {
                DebugConsole.Log($"[SteamworksServer] TryAcceptConnection: Connection state for {clientId} is {info.m_eState}");
            }

            // Only accept if in Connecting state
            if (info.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
            {
                DebugConsole.LogWarning($"[SteamworksServer] TryAcceptConnection: Connection {clientId} is not in Connecting state (actual: {info.m_eState}), skipping accept");
                return;
            }

            var result = SteamNetworkingSockets.AcceptConnection(conn);
            if (result == EResult.k_EResultOK)
            {
                if (!SteamNetworkingSockets.SetConnectionPollGroup(conn, PollGroup))
                {
                    RejectConnection(conn, clientId,
                        $"Poll-group assignment failed (connection {conn.m_HSteamNetConnection}, poll group {PollGroup.m_HSteamNetPollGroup})");
                    return;
                }
                DebugConsole.Log($"[SteamworksServer] Connection accepted from {clientId}");
            }
            else
            {
                // k_EResultInvalidState means the connection has already transitioned away
                if (result == EResult.k_EResultInvalidState)
                {
                    DebugConsole.LogWarning($"[SteamworksServer] AcceptConnection returned InvalidState for {clientId} - connection may have already been handled or closed");
                }
                else
                {
                    RejectConnection(conn, clientId, $"Accept failed ({result})");
                }
            }
        }

        private static void RejectConnection(HSteamNetConnection conn, CSteamID clientId, string reason)
        {
            using var _ = Profiler.Scope();

            DebugConsole.LogError($"[SteamworksServer] Rejecting connection from {clientId}: {reason}", false);
            CloseConnection(conn, reason);
        }

        private static bool CloseConnection(HSteamNetConnection connection, string reason)
        {
            if (!SteamNetworkingSockets.CloseConnection(connection, 0, reason, false)
                && SteamNetworkingSockets.GetConnectionInfo(connection, out var _))
            {
                DebugConsole.LogWarning($"[SteamworksServer] Steam did not confirm connection release: {connection.m_HSteamNetConnection}");
                return false;
            }

            if (_clientIdsByConnection.TryGetValue(connection.m_HSteamNetConnection, out ulong playerId))
            {
                _clientIdsByConnection.Remove(connection.m_HSteamNetConnection);
                MultiplayerSession.UnregisterClient(playerId, null, connection);
            }
            return true;
        }

        private static void OnClientConnected(HSteamNetConnection conn, CSteamID clientId)
        {
            using var _ = Profiler.Scope();

            uint connectionId = conn.m_HSteamNetConnection;
            bool duplicateConnection = false;
            foreach (var entry in _clientIdsByConnection)
            {
                if (entry.Key != connectionId && entry.Value == clientId.m_SteamID)
                {
                    duplicateConnection = true;
                    break;
                }
            }

            if (duplicateConnection)
            {
                CloseConnection(conn, "Duplicate session identity");
                return;
            }

            _clientIdsByConnection[connectionId] = clientId.m_SteamID;

            DebugConsole.Log($"[SteamworksServer] Connection to {clientId} fully established!");
            //SaveFileRequestPacket.SendSaveFile(clientId); // Old method
            //GoogleDriveUtils.UploadAndSendToClient(clientId); // Upload to googledrive and send to the client
        }

        private static void OnClientClosed(HSteamNetConnection conn, CSteamID clientId)
        {
            using var _ = Profiler.Scope();

            if (!CloseConnection(conn, null)) return;

            DebugConsole.Log($"[SteamworksServer] Connection closed for {clientId}");

        }

        public override bool TryGetRemotePlayerId(object connection, out ulong playerId)
        {
            if (connection is HSteamNetConnection steamConnection)
                return _clientIdsByConnection.TryGetValue(steamConnection.m_HSteamNetConnection, out playerId);

            playerId = 0;
            return false;
        }

        public override void DisconnectConnection(object connection, string reason)
        {
            if (connection is HSteamNetConnection steamConnection)
                CloseConnection(steamConnection, reason);
        }

        public override void KickClient(ulong clientId)
        {
            if (!MultiplayerSession.ConnectedPlayers.TryGetValue(clientId, out var player))
            {
                DebugConsole.LogWarning($"[SteamworksServer] KickClient: Client {clientId} not found.");
                return;
            }

            if (player.Connection == null)
            {
                DebugConsole.LogWarning($"[SteamworksServer] KickClient: Client {clientId} has no active connection.");
                return;
            }

            if (player.Connection is HSteamNetConnection conn)
            {
                DebugConsole.Log($"[SteamworksServer] Kicking client {clientId}");

                CloseConnection(conn, "Kicked by host");
            }
            else
            {
                DebugConsole.LogError($"[SteamworksServer] KickClient: Invalid connection type for {clientId}");
            }
        }

    }
}
