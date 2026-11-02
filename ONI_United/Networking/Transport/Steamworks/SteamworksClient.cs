using ONI_United.Core;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ONI_United.DebugTools;
using ONI_United.Misc;
using ONI_United.Networking.Packets.Architecture;
using Shared.Profiling;
using ONI_United.Networking.Sessions.States;
using Shared;
using Steamworks;
using UnityEngine;
using static ONI_United.UI.Screens.NetworkIndicatorsScreen;
using ONI_United;
using ONI_United.Networking;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transport;

namespace ONI_United.Networking.Transport.Steamworks
{
    public class SteamworksClient : TransportClient
    {
        private Callback<SteamNetConnectionStatusChangedCallback_t> _connectionStatusChangedCallback;
        public static HSteamNetConnection? Connection { get; private set; }


        private static SteamNetConnectionRealTimeStatus_t? connectionHealth = null;

        // Network health
        private const int JITTER_SAMPLE_COUNT = 20;
        private static readonly Queue<int> _pingSamples = new();

        public override object CurrentConnection => Connection.HasValue ? Connection.Value : null;

        public override bool IsCurrentConnection(object connection) =>
            connection is HSteamNetConnection current
            && Connection.HasValue
            && current.m_HSteamNetConnection == Connection.Value.m_HSteamNetConnection;

        public override void Prepare()
        {
            using var _ = Profiler.Scope();

            if (_connectionStatusChangedCallback == null)
            {
                _connectionStatusChangedCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(
                    OnConnectionStatusChanged);
                DebugConsole.Log("[GameClient] Registered connection status callback.");
            }
        }

        // Connects to the host using Steam P2P networking. IP and port are ignored since Steamworks uses host steam id to connect.
        protected override void ConnectProtocol(string ip, int port)
        {
            using var _ = Profiler.Scope();

            if (Connection.HasValue) throw new InvalidOperationException("The previous Steam connection is still owned.");
            if (!SteamManager.Initialized || !SteamLobby.InLobby || !MultiplayerSession.HostUserID.IsValid())
            {
                OnConnectionFailed?.Invoke("Join a Steam lobby with a valid host before connecting.");
                return;
            }
            ulong hostSteamId = MultiplayerSession.HostUserID;
            DebugConsole.Log($"[GameClient] Attempting ConnectP2P to host {hostSteamId}...");

            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(hostSteamId);

            var options = new SteamNetworkingConfigValue_t[2];
            options[0].m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial;
            options[0].m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32;
            options[0].m_val.m_int32 = Configuration.Instance.Client.TimeoutSeconds * 1000;

            options[1].m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected;
            options[1].m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32;
            options[1].m_val.m_int32 = Configuration.Instance.Client.TimeoutSeconds * 1000;

            Connection = SteamNetworkingSockets.ConnectP2P(ref identity, 0, options.Length, options);
            if (Connection.Value == HSteamNetConnection.Invalid)
            {
                Connection = null;
                OnConnectionFailed?.Invoke("Steam did not create a connection.");
            }
        }

        public override bool TryStop(out string error)
        {
            error = null;
            try
            {
                if (Connection.HasValue)
                {
                    var owned = Connection.Value;
                    if (!SteamNetworkingSockets.CloseConnection(owned, 0, "Client disconnecting", false)
                        && SteamNetworkingSockets.GetConnectionInfo(owned, out _))
                    {
                        error = "Steam did not confirm client connection release.";
                        return false;
                    }
                    Connection = null;
                }
                _connectionStatusChangedCallback?.Dispose();
                _connectionStatusChangedCallback = null;
                connectionHealth = null;
                _pingSamples.Clear();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }

        public override void Update()
        {
            using var _ = Profiler.Scope();

            SteamNetworkingSockets.RunCallbacks();
            EvaluateConnectionHealth();
        }

        public override void OnMessageRecieved()
        {
            using var _ = Profiler.Scope();

            if (Connection.HasValue)
                ProcessIncomingMessages(Connection.Value);
            //else
            //    DebugConsole.LogWarning($"[GameClient] Poll() - Connection is null! State: {State}");
        }

        private void ProcessIncomingMessages(HSteamNetConnection conn)
        {
            using var _ = Profiler.Scope();
            var scope = Profiler.Scope();
            int totalBytes = 0;

            int maxMessagesPerConnectionPoll = Configuration.GetClientProperty<int>("MaxMessagesPerPoll");
            IntPtr[] messages = new IntPtr[maxMessagesPerConnectionPoll];
            int msgCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, messages, maxMessagesPerConnectionPoll);

            //if (msgCount > 0)
            //{
            //	DebugConsole.Log($"[GameClient] ProcessIncomingMessages() - Received {msgCount} messages");
            //}

            for (int i = 0; i < msgCount; i++)
            {
                if (!IsCurrentConnection(conn))
                {
                    SteamNetworkingMessage_t.Release(messages[i]);
                    continue;
                }
                var msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messages[i]);
                totalBytes += msg.m_cbSize;
                byte[] data = new byte[msg.m_cbSize];
                Marshal.Copy(msg.m_pData, data, 0, msg.m_cbSize);

                try
                {
                    ulong? senderId = MultiplayerSession.HostUserID.IsValid()
                        ? MultiplayerSession.HostUserID
                        : null;
                    PacketHandler.HandleIncoming(data, senderId, conn);
                }
                catch (Exception ex)
                {
                    DebugConsole.LogWarning($"[GameClient] Failed to handle incoming packet: {ex}"); // Prevent crashes from packet handling
                }

                SteamNetworkingMessage_t.Release(messages[i]);
            }
            scope.End(msgCount, totalBytes);
        }

        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t data)
        {
            using var _ = Profiler.Scope();

            var state = data.m_info.m_eState;
            var remote = data.m_info.m_identityRemote.GetSteamID();

            DebugConsole.Log($"[GameClient] Connection status changed: {state} (remote={remote})");

            if (!Connection.HasValue || data.m_hConn.m_HSteamNetConnection != Connection.Value.m_HSteamNetConnection)
                return;

            switch (state)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (Lifecycle.Current != Lifecycle.Phase.Joining) break;
                    if (!SteamNetworkingSockets.GetConnectionInfo(Connection.Value, out var current)
                        || current.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected
                        || current.m_identityRemote.GetSteamID().m_SteamID != MultiplayerSession.HostUserID)
                    {
                        OnConnectionFailed?.Invoke("Steam did not confirm the intended native host connection.");
                        break;
                    }
                    OnClientConnected?.Invoke();
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    if (Lifecycle.Current == Lifecycle.Phase.ClearingSession) break;
                    var returnToMenu = OnReturnToMenu;
                    OnClientDisconnected?.Invoke();
                    if (ReferenceEquals(this, NetworkRuntime.TransportClient))
                        returnToMenu?.Invoke(
                        state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer
                            ? STRINGS.UI.MP_OVERLAY.CLIENT.STEAMWORKS.HOST_DISCONNECTED
                            : STRINGS.UI.MP_OVERLAY.CLIENT.STEAMWORKS.LOCAL_PROBLEM,
                        state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer
                            ? STRINGS.UI.MP_OVERLAY.CLIENT.STEAMWORKS.HOST_DISCONNECTED_DESC
                            : STRINGS.UI.MP_OVERLAY.CLIENT.STEAMWORKS.LOCAL_PROBLEM_DESC);
                    break;
                // All other native milestones remain protocol details.
                default:
                    break;
            }
        }

        #region Connection Health
        public static SteamNetConnectionRealTimeStatus_t? QueryConnectionHealth()
        {
            using var _ = Profiler.Scope();

            if (Connection.HasValue)
            {
                SteamNetConnectionRealTimeStatus_t status = default;
                SteamNetConnectionRealTimeLaneStatus_t laneStatus = default;

                EResult res = SteamNetworkingSockets.GetConnectionRealTimeStatus(
                        Connection.Value,
                        ref status,
                        0,
                        ref laneStatus
                );

                if (res == EResult.k_EResultOK)
                {
                    return status;
                }
            }
            return null;
        }

        public static void EvaluateConnectionHealth()
        {
            using var _ = Profiler.Scope();

            connectionHealth = QueryConnectionHealth();
        }

        public static SteamNetConnectionRealTimeStatus_t? GetConnectionHealth()
        {
            using var _ = Profiler.Scope();

            return connectionHealth;
        }

        public static float GetLocalPacketQuality()
        {
            using var _ = Profiler.Scope();

            if (!connectionHealth.HasValue)
                return 0f;

            return connectionHealth.Value.m_flConnectionQualityLocal;
        }

        public static float GetRemotePacketQuality()
        {
            using var _ = Profiler.Scope();

            if (!connectionHealth.HasValue)
                return 0f;

            return connectionHealth.Value.m_flConnectionQualityRemote;
        }

        public static int GetUnackedReliable()
        {
            using var _ = Profiler.Scope();

            if (!connectionHealth.HasValue)
                return -1;

            return connectionHealth.Value.m_cbSentUnackedReliable;
        }

        public static int GetPendingUnreliable()
        {
            using var _ = Profiler.Scope();

            if (!connectionHealth.HasValue)
                return -1;

            return connectionHealth.Value.m_cbPendingUnreliable;
        }

        public static long GetUsecQueueTime()
        {
            using var _ = Profiler.Scope();

            if (!connectionHealth.HasValue)
                return -1;

            return (long)connectionHealth.Value.m_usecQueueTime;
        }

        public static float GetSendRate()
        {
            if (!connectionHealth.HasValue) return 0f;
            return connectionHealth.Value.m_flOutBytesPerSec;
        }

        public static float GetRecvRate()
        {
            if (!connectionHealth.HasValue) return 0f;
            return connectionHealth.Value.m_flInBytesPerSec;
        }

        public static float GetInPacketsPerSec()
        {
            if (!connectionHealth.HasValue) return 0f;
            return connectionHealth.Value.m_flInPacketsPerSec;
        }

        public static float GetOutPacketsPerSec()
        {
            if (!connectionHealth.HasValue) return 0f;
            return connectionHealth.Value.m_flOutPacketsPerSec;
        }

        public override float IncomingBandwidth => GetRecvRate();
        public override float OutgoingBandwidth => GetSendRate();
        public override int IncomingPps => (int)GetInPacketsPerSec();
        public override int OutgoingPps => (int)GetOutPacketsPerSec();

        public override NetworkState GetJitterState()
        {
            using var _ = Profiler.Scope();

            var connhealth = GetConnectionHealth();
            if (!connhealth.HasValue)
                return NetworkState.BAD;

            int ping = connhealth.Value.m_nPing;
            if (ping <= 0)
                return NetworkState.BAD;

            // Collect samples
            _pingSamples.Enqueue(ping);
            while (_pingSamples.Count > JITTER_SAMPLE_COUNT)
                _pingSamples.Dequeue();

            // Not enough data yet
            if (_pingSamples.Count < 5)
                return NetworkState.DEGRADED;

            // Calculate mean
            float mean = 0f;
            foreach (var p in _pingSamples)
                mean += p;
            mean /= _pingSamples.Count;

            // Calculate standard deviation
            float variance = 0f;
            foreach (var p in _pingSamples)
            {
                float diff = p - mean;
                variance += diff * diff;
            }

            float jitter = Mathf.Sqrt(variance / _pingSamples.Count);

            if (jitter <= 10f)
                return NetworkState.GOOD;

            if (jitter <= 30f)
                return NetworkState.DEGRADED;

            return NetworkState.BAD;
        }

        public override NetworkState GetLatencyState()
        {
            using var _ = Profiler.Scope();

            var connhealth = GetConnectionHealth();
            if (!connhealth.HasValue)
                return NetworkState.BAD;

            int ping = connhealth.Value.m_nPing;

            // Invalid or unknown ping
            if (ping <= 0)
                return NetworkState.BAD;

            if (ping <= NetworkRuntime.PingRanges.DEGRADED)
                return NetworkState.GOOD;

            if (ping <= NetworkRuntime.PingRanges.BAD)
                return NetworkState.DEGRADED;

            return NetworkState.BAD;
        }

        public override NetworkState GetPacketlossState()
        {
            using var _ = Profiler.Scope();

            var connhealth = GetConnectionHealth();
            if (!connhealth.HasValue)
                return NetworkState.BAD;

            float quality = connhealth.Value.m_flConnectionQualityLocal;

            if (quality >= 0.95f)
                return NetworkState.GOOD;

            if (quality >= 0.85f)
                return NetworkState.DEGRADED;

            return NetworkState.BAD;
        }

        public override NetworkState GetServerPerformanceState()
        {
            using var _ = Profiler.Scope();

            if (!Connection.HasValue)
                return NetworkState.BAD;

            var connHealth = GetConnectionHealth();

            // Connection no longer exists
            if (!connHealth.HasValue)
                return NetworkState.BAD;

            // Authoritative disconnect states
            switch (connHealth.Value.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    return NetworkState.BAD;
            }

            // Hard transport failure
            if (connHealth.Value.m_cbSentUnackedReliable > 64 * 1024 || // 64kb unacked by server
                (long)connHealth.Value.m_usecQueueTime > 1_000_000 || // 1 second old unsent packets
                connHealth.Value.m_flConnectionQualityRemote <= 0.85f) // server side reports badly degraded quality
            {
                return NetworkState.BAD;
            }

            // Soft degradation
            if (connHealth.Value.m_cbSentUnackedReliable > 16 * 1024 || // 16kb unacked by server
                (long)connHealth.Value.m_usecQueueTime > 500_000 || // 500ms old unsent packets
                connHealth.Value.m_flConnectionQualityRemote <= 0.95f) // server side reports degraded quality
            {
                return NetworkState.DEGRADED;
            }

            return NetworkState.GOOD;
        }

        public override int GetPing()
        {
            using var _ = Profiler.Scope();

            if (!connectionHealth.HasValue)
                return -1;

            return connectionHealth.Value.m_nPing;
        }
        #endregion
    }
}
