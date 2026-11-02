using ONI_United.Core;
using LiteNetLib;
using LiteNetLib.Utils;
using ONI_United.DebugTools;
using ONI_United.UI.Screens;
using ONI_United.Misc;
using ONI_United.Networking.Packets.Architecture;
using Shared;
using Shared.Profiling;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using static ONI_United.UI.Screens.NetworkIndicatorsScreen;
using ONI_United;
using ONI_United.Networking;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transport;

namespace ONI_United.Networking.Transport.LiteNetLib
{
    public class LiteNetLibClient : TransportClient
    {
        private static NetManager _client;
        private static EventBasedNetListener _listener;
        private static NetPeer _serverPeer;

        /// <summary>
        /// Set once OnConnectedToServer has run for the current connection attempt.
        ///
        /// The mod only learns it is connected from LiteNetLib's PeerConnectedEvent, which
        /// is delivered by PollEvents. Twice in one evening the host accepted a client's
        /// reconnect after a world load within half a second - "Remote client connected" in
        /// the host log - and the client's event arrived 3.5 seconds later in one case and
        /// never in the other, although Unity was updating and PollEvents was being called.
        /// The peer's own ConnectionState said Connected the whole time. This flag lets
        /// WaitForConnectionSuccess tell that apart from a connection that is still pending.
        /// </summary>
        private static bool _connectedEventDelivered;
        private static bool _connectAttemptInFlight;

        // LAN Discovery
        private static NetManager _discoveryClient;
        private static EventBasedNetListener _discoveryListener;
        public static readonly Dictionary<string, LanDiscoveredHost> DiscoveredHosts = new Dictionary<string, LanDiscoveredHost>();

        public static NetManager Client => _client;
        public static NetPeer ServerPeer => _serverPeer;

        public bool IsConnected => _serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected;
        public override object CurrentConnection => _serverPeer;
        public static int MaxServerCapacity { get; internal set; } = 16;
        public bool IsLoadingReconnect { get; set; }

        private static readonly ConcurrentQueue<(NetPeer peer, byte[] data)> _incomingPackets = new ConcurrentQueue<(NetPeer, byte[])>();

        // Network health
        private const int JITTER_SAMPLE_COUNT = 20;
        private readonly Queue<int> _pingSamples = new Queue<int>();

        // Bandwidth tracking
        private long _lastBytesIn, _lastBytesOut;
        private long _lastPacketsIn, _lastPacketsOut;
        private float _clientInBw, _clientOutBw;
        private int _clientInPps, _clientOutPps;
        private float _lastBwPollTime;

        public override float IncomingBandwidth => _clientInBw;
        public override float OutgoingBandwidth => _clientOutBw;
        public override int IncomingPps => _clientInPps;
        public override int OutgoingPps => _clientOutPps;

        public override bool IsCurrentConnection(object connection) =>
            connection is NetPeer peer && ReferenceEquals(peer, _serverPeer);

        public static void StartLanDiscovery(int targetPort = 8080)
        {
            if (_discoveryClient == null)
            {
                _discoveryListener = new EventBasedNetListener();
                _discoveryListener.NetworkReceiveUnconnectedEvent += OnDiscoveryResponseReceived;
                _discoveryClient = new NetManager(_discoveryListener)
                {
                    UnsyncedEvents = false,
                    BroadcastReceiveEnabled = true
                };
                _discoveryClient.Start();
            }

            var writer = new NetDataWriter();
            writer.Put("ONI_DISCOVERY_REQ");
            _discoveryClient.SendBroadcast(writer, targetPort);
            if (targetPort != 7777)
            {
                _discoveryClient.SendBroadcast(writer, 7777);
            }
        }

        public static void PollDiscovery()
        {
            if (_discoveryClient != null && _discoveryClient.IsRunning)
            {
                _discoveryClient.PollEvents();
            }
        }

        public static void StopLanDiscovery()
        {
            if (_discoveryClient != null)
            {
                _discoveryClient.Stop();
                _discoveryClient = null;
                _discoveryListener = null;
            }
        }

        private static void OnDiscoveryResponseReceived(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            try
            {
                string resp = reader.GetString();
                if (resp == "ONI_DISCOVERY_RESP")
                {
                    var host = new LanDiscoveredHost
                    {
                        EndPoint = remoteEndPoint,
                        HostName = reader.GetString(),
                        WorldName = reader.GetString(),
                        Cycle = reader.GetInt(),
                        PlayerCount = reader.GetInt(),
                        MaxPlayers = reader.GetInt(),
                        Port = reader.GetInt(),
                        LastSeenTime = Time.realtimeSinceStartup
                    };

                    string key = remoteEndPoint.Address.ToString() + ":" + host.Port;
                    DiscoveredHosts[key] = host;
                }
            }
            catch (Exception ex)
            {
                DebugConsole.LogWarning("[LiteNetLibClient] Error parsing discovery response: " + ex.Message);
            }
        }

        public override void Prepare()
        {
            using var _ = Profiler.Scope();
        }

        protected override void ConnectProtocol(string ip, int port)
        {
            using var _ = Profiler.Scope();
            var operation = NetworkRuntime.ActiveOperation;

            StopLanDiscovery();

            if (_connectAttemptInFlight)
                return;

            if (_client != null)
                throw new InvalidOperationException("The previous LAN client manager is still owned.");

            int timeout = Configuration.Instance.Client.TimeoutSeconds;
            ulong clientId = NetworkRuntime.GetLocalID();

            MultiplayerSession.ServerIp = ip;
            MultiplayerSession.ServerPort = port;
            MultiplayerSession.SetHost(Utils.NilUlong());

            _listener = new EventBasedNetListener();
            _listener.PeerConnectedEvent += OnConnectedToServer;
            _listener.PeerDisconnectedEvent += OnDisconnectedFromServer;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.NetworkErrorEvent += OnNetworkError;

            // UnsyncedEvents = false ensures ALL events execute on Main Thread in PollEvents()
            _client = new NetManager(_listener)
            {
                AutoRecycle = true,
                DisconnectTimeout = Configuration.Instance.Client.TimeoutSeconds * 1000,
                UnsyncedEvents = false,
                ChannelsCount = 4,
                EnableStatistics = true
            };

            if (!_client.Start()) throw new InvalidOperationException("LAN client socket startup failed.");
            DebugConsole.Log("[LiteNetLibClient] Connecting to " + ip + ":" + port + "...");

            var writer = new NetDataWriter();
            writer.Put("ONI_TOGETHER");
            writer.Put(clientId);
            _connectedEventDelivered = false;
            _serverPeer = _client.Connect(ip, port, writer);

            _connectAttemptInFlight = true;
            CoroutineRunner.RunOne(WaitForConnectionSuccess(operation, timeout, _client, _serverPeer));
        }

        private IEnumerator WaitForConnectionSuccess(object operation, int timeoutSeconds, NetManager owner, NetPeer attempt)
        {
            try
            {
                float startedAt = Time.realtimeSinceStartup;
                float connectedWithoutEventSince = -1f;

                while (Time.realtimeSinceStartup - startedAt < timeoutSeconds)
                {
                    if (!NetworkRuntime.IsCurrentOperation(this, operation)
                        || !ReferenceEquals(owner, _client) || !ReferenceEquals(attempt, _serverPeer) || !_connectAttemptInFlight)
                        yield break;

                    if (_connectedEventDelivered && MultiplayerSession.InActiveSession)
                        yield break;

                    if (!_connectedEventDelivered
                        && _serverPeer != null
                        && _serverPeer.ConnectionState == ConnectionState.Connected)
                    {
                        float now = Time.realtimeSinceStartup;
                        // The transport is connected; only the event has not reached the mod. Give
                        // PollEvents one more tick in case it is merely queued, then stop waiting for
                        // it - the handler is the same one the event would have run.
                        if (connectedWithoutEventSince < 0f)
                            connectedWithoutEventSince = now;
                        else if (now - connectedWithoutEventSince >= 1f)
                        {
                            DebugConsole.LogWarning($"[LiteNetLibClient] Peer has been Connected for {now - connectedWithoutEventSince:0.0}s ({now - startedAt:0.0}s since connect) but PeerConnectedEvent never arrived - completing the connection from the poll loop.");
                            OnConnectedToServer(_serverPeer);
                        }
                    }

                    yield return null;
                }

                if (!NetworkRuntime.IsCurrentOperation(this, operation)
                    || !ReferenceEquals(owner, _client) || !ReferenceEquals(attempt, _serverPeer) || !_connectAttemptInFlight)
                    yield break;

                if (!_connectedEventDelivered || !MultiplayerSession.InActiveSession)
                {
                    DebugConsole.LogError("[LiteNetLibClient] Connection timed out before the session handshake completed.");
                    var returnToMenu = OnReturnToMenu;
                    OnConnectionFailed?.Invoke("LAN connection timed out.");
                    if (ReferenceEquals(this, NetworkRuntime.TransportClient) && IsCurrentOperation(operation))
                        returnToMenu?.Invoke(
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.CONNECTION_FAILED,
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.CONNECTION_FAILED_DESC
                    );
                }
            }
            finally
            {
                if (NetworkRuntime.IsCurrentOperation(this, operation)
                    && ReferenceEquals(owner, _client) && ReferenceEquals(attempt, _serverPeer))
                    _connectAttemptInFlight = false;
            }
        }

        private void OnConnectedToServer(NetPeer peer)
        {
            using var _ = Profiler.Scope();

            // Reached either from PeerConnectedEvent or from WaitForConnectionSuccess when
            // the event failed to arrive; whichever comes second must not run this twice.
            if (!ReferenceEquals(peer, _serverPeer) || _connectedEventDelivered
                || Lifecycle.Current != Lifecycle.Phase.Joining)
                return;
            _connectedEventDelivered = true;

            _serverPeer = peer;
            OnClientConnected?.Invoke();
        }

        private void OnDisconnectedFromServer(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            using var _ = Profiler.Scope();

            if (!ReferenceEquals(peer, _serverPeer) || Lifecycle.Current == Lifecycle.Phase.ClearingSession) return;
            var operation = NetworkRuntime.ActiveOperation;
            var returnToMenu = OnReturnToMenu;
            var (reason, message) = GetDisconnectInfo(disconnectInfo);
            OnClientDisconnected?.Invoke();
            DebugConsole.Log($"[LiteNetLibClient] Disconnected from server. Reason: {disconnectInfo.Reason} ({reason})");
            if (ReferenceEquals(this, NetworkRuntime.TransportClient) && IsCurrentOperation(operation))
                returnToMenu?.Invoke(reason, message);
        }

        private (string reason, string message) GetDisconnectInfo(DisconnectInfo info)
        {
            switch (info.Reason)
            {
                case DisconnectReason.Timeout:
                    return (
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.CONNECTION_TIMED_OUT,
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.CONNECTION_TIMED_OUT_DESC
                    );

                case DisconnectReason.RemoteConnectionClose:
                case DisconnectReason.DisconnectPeerCalled:
                    return (
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.HOST_DISCONNECTED,
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.HOST_DISCONNECTED_DESC
                    );

                case DisconnectReason.ConnectionRejected:
                    return (
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.CONNECTION_REJECTED,
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.CONNECTION_REJECTED_DESC
                    );

                case DisconnectReason.ConnectionFailed:
                case DisconnectReason.HostUnreachable:
                case DisconnectReason.NetworkUnreachable:
                case DisconnectReason.UnknownHost:
                    return (
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.CONNECTION_FAILED,
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.CONNECTION_FAILED_DESC
                    );

                default:
                    return (
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.UNKNOWN,
                        ONI_United.STRINGS.UI.MP_OVERLAY.CLIENT.LITENETLIB.UNKNOWN_DESC
                    );
            }
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            byte[] rawData = reader.GetRemainingBytes();
            _incomingPackets.Enqueue((peer, rawData));
        }

        private void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            DebugConsole.LogWarning("[LiteNetLibClient] Network error: " + socketError);
        }

        public override bool TryStop(out string error)
        {
            error = null;
            _connectAttemptInFlight = false;
            try
            {
                // Stop discards queued native events. Retain manager/peer on failure for retry.
                _client?.Stop();
                if (_listener != null)
                {
                    _listener.PeerConnectedEvent -= OnConnectedToServer;
                    _listener.PeerDisconnectedEvent -= OnDisconnectedFromServer;
                    _listener.NetworkReceiveEvent -= OnNetworkReceive;
                    _listener.NetworkErrorEvent -= OnNetworkError;
                }
                _listener = null;
                _client = null;
                _serverPeer = null;
                _connectedEventDelivered = false;
                while (_incomingPackets.TryDequeue(out var ignored)) { }
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

            PollDiscovery();

            if (_client == null)
                return;

            _client.PollEvents();
            UpdateMetrics();
        }

        public override void OnMessageRecieved()
        {
            using var _ = Profiler.Scope();

            while (_incomingPackets.TryDequeue(out var packet))
            {
                if (!ReferenceEquals(packet.peer, _serverPeer)
                    || !NetworkRuntime.HasClientOperation || Lifecycle.Current == Lifecycle.Phase.ClearingSession) continue;
                try
                {
                    ulong? senderId = MultiplayerSession.HostUserID.IsValid()
                        ? MultiplayerSession.HostUserID
                        : null;
                    PacketHandler.HandleIncoming(packet.data, senderId, packet.peer);
                }
                catch (Exception ex)
                {
                    DebugConsole.LogError("[LiteNetLibClient] Exception while handling packet from host: " + ex);
                }
            }
        }

        public override int GetPing()
        {
            if (_serverPeer == null) return -1;
            return _serverPeer.Ping * 2; // RTT
        }

        public override NetworkIndicatorsScreen.NetworkState GetJitterState()
        {
            int currentPing = GetPing();
            if (currentPing < 0) return NetworkState.BAD;

            _pingSamples.Enqueue(currentPing);
            if (_pingSamples.Count > JITTER_SAMPLE_COUNT)
                _pingSamples.Dequeue();

            if (_pingSamples.Count < 5) return NetworkState.GOOD;

            int min = int.MaxValue, max = int.MinValue;
            foreach (var p in _pingSamples)
            {
                if (p < min) min = p;
                if (p > max) max = p;
            }

            int jitter = max - min;
            if (jitter > 80) return NetworkState.BAD;
            if (jitter > 40) return NetworkState.DEGRADED;
            return NetworkState.GOOD;
        }

        public override NetworkIndicatorsScreen.NetworkState GetLatencyState()
        {
            int ping = GetPing();
            if (ping < 0) return NetworkState.BAD;
            if (ping > NetworkRuntime.PingRanges.BAD) return NetworkState.BAD;
            if (ping > NetworkRuntime.PingRanges.DEGRADED) return NetworkState.DEGRADED;
            return NetworkState.GOOD;
        }

        public override NetworkIndicatorsScreen.NetworkState GetPacketlossState()
        {
            if (_client == null || _serverPeer == null) return NetworkState.BAD;
            return NetworkState.GOOD;
        }

        public override NetworkIndicatorsScreen.NetworkState GetServerPerformanceState()
        {
            return NetworkState.GOOD;
        }

        private void UpdateMetrics()
        {
            if (_client == null)
                return;

            float now = Time.realtimeSinceStartup;
            float dt = now - _lastBwPollTime;
            if (dt < 1f)
                return;

            long totalBytesIn = _client.Statistics.BytesReceived;
            long totalBytesOut = _client.Statistics.BytesSent;
            long totalPacketsIn = _client.Statistics.PacketsReceived;
            long totalPacketsOut = _client.Statistics.PacketsSent;

            _clientInBw = (totalBytesIn - _lastBytesIn) / dt;
            _clientOutBw = (totalBytesOut - _lastBytesOut) / dt;
            _clientInPps = (int)((totalPacketsIn - _lastPacketsIn) / dt);
            _clientOutPps = (int)((totalPacketsOut - _lastPacketsOut) / dt);

            _lastBytesIn = totalBytesIn;
            _lastBytesOut = totalBytesOut;
            _lastPacketsIn = totalPacketsIn;
            _lastPacketsOut = totalPacketsOut;
            _lastBwPollTime = now;
        }
    }
}
