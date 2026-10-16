using System;
using Riptide;
using Riptide.Utils;
using ONI_United.DebugTools;
using ONI_United.Networking.Packets.Architecture;
using Shared.Profiling;
using ONI_United.Misc;
using System.Collections.Concurrent;
using ONI_United.UI.Screens;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using ONI_United.Networking.Replication.Components;
using ONI_United.Networking.Session.States;
using Shared;
using static ONI_United.STRINGS.UI.MP_OVERLAY;
using ONI_United;
using ONI_United.Networking;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Session;
using ONI_United.Networking.Transport;

namespace ONI_United.Networking.Transport.Riptide
{
    public class RiptideClient : TransportClient
    {
        private static Client _client;
        private static bool _connectAttemptInFlight;
        public static Client Client
        {
            get { return _client; }
        }

        private static readonly ConcurrentQueue<byte[]> _incomingPackets = new ConcurrentQueue<byte[]>();

        // Network health
        private const int JITTER_SAMPLE_COUNT = 20;
        private readonly Queue<int> _pingSamples = new Queue<int>();

        private ConnectionMetrics Metrics => _client?.Connection?.Metrics;

        // Bandwidth tracking via Connection.Metrics
        private long _lastBytesIn, _lastBytesOut;
        private int _lastMsgIn, _lastMsgOut;
        private float _riptideInBw, _riptideOutBw;
        private int _riptideInPps, _riptideOutPps;
        private float _lastBwPollTime;

        public List<ulong> ClientList { get; private set; } = new();
        public static ulong CLIENT_ID { get; private set; }
        public static int MaxServerCapacity { get; set; }

        public override void Prepare()
        {
            using var _ = Profiler.Scope();

            RiptideLogger.Initialize(DebugConsole.Log, false);
        }

        public override void ConnectToHost(string ip, int port)
        {
            using var _ = Profiler.Scope();

            if (_connectAttemptInFlight)
                return;

            if (_client != null)
            {
                if (!_client.IsNotConnected)
                    return;

                CleanupRiptide();
            }

            MultiplayerSession.ServerIp = ip;
            MultiplayerSession.ServerPort = port;
            _client = new Client("RiptideClient");

            int timeout = Configuration.Instance.Client.TimeoutSeconds;
            _client.Connected += OnConnectedToServer;
            _client.Disconnected += OnDisconnectedFromServer;
            _client.MessageReceived += OnMessageRecievedFromServer;
            _client.ClientConnected += OnOtherClientConnected;
            _client.ClientDisconnected += OnOtherClientDisconnected;
            DebugConsole.Log($"Connecting to {ip}:{port}");
            _connectAttemptInFlight = true;
            CoroutineRunner.RunOne(WaitForConnectionSuccess(timeout));
            _client.Connect($"{ip}:{port}", useMessageHandlers: false);
            _client.TimeoutTime = Configuration.Instance.Client.TimeoutSeconds * 1000;
        }

        private void OnOtherClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            using var _ = Profiler.Scope();

            RemoveClientFromList(e.Id);
            MultiplayerSession.RefreshAllPlayerCursors();
        }

        private void OnOtherClientConnected(object sender, ClientConnectedEventArgs e)
        {
            using var _ = Profiler.Scope();

            AddClientToList(e.Id);
        }

        private void OnMessageRecievedFromServer(object sender, MessageReceivedEventArgs e)
        {
            using var _ = Profiler.Scope();

            byte[] rawData = e.Message.GetBytes();
            _incomingPackets.Enqueue(rawData);
        }

        private void OnConnectedToServer(object sender, EventArgs e)
        {
            using var _ = Profiler.Scope();

            CLIENT_ID = GetClientID();
            AddClientToList(CLIENT_ID);

            var conn = _client.Connection;
            conn.CanQualityDisconnect = false; // prevents auto‑disconnect due to poor delivery
            conn.MaxSendAttempts = 30;         // 15 is default so we'll double it
            conn.MaxAvgSendAttempts = 12;      // Was 5, we'll double it and add a buffer
            conn.AvgSendAttemptsResilience = 128; // was 64, doubled

            ResetBandwidth();

            MultiplayerSession.OnConnectedToHost(1, conn); // Host's client is always 1
            OnClientConnected.Invoke();

            DebugConsole.Log($"[Riptide] Connected to server with Client ID: {CLIENT_ID}");

            //CoroutineRunner.RunOne(Handshake());
            NetworkConfig.TransportClient.OnRequestStateOrReturn.Invoke();
        }

        private void OnDisconnectedFromServer(object sender, DisconnectedEventArgs e)
        {
            using var _ = Profiler.Scope();

            RemoveClientFromList(CLIENT_ID);
            CLIENT_ID = Utils.NilUlong();

            OnClientDisconnected?.Invoke();
            MultiplayerSession.OnDisconnectedFromHost(null);

            var (reason, message) = GetDisconnectInfo(e);
            switch (e.Reason) {
                case DisconnectReason.Disconnected:
                    // Initiated by client do nothing
                    break;
                default:
                    NetworkConfig.TransportClient.OnReturnToMenu.Invoke(reason, message);
                    break;
            }

            CleanupRiptide();
        }

        public override void Disconnect()
        {
            using var _ = Profiler.Scope();

            if (_client == null)
                return;

            if (_client.IsNotConnected)
            {
                CleanupRiptide();
                return;
            }

            _client.Disconnect();
        }

        public override void OnMessageRecieved()
        {
            using var _ = Profiler.Scope();

            while (_incomingPackets.TryDequeue(out var rawData))
            {
                int size = rawData.Length;

                int packetType = rawData.Length >= 4
                    ? BitConverter.ToInt32(rawData, 0)
                    : 0;

                var scope = Profiler.Scope();

                try
                {
                    PacketHandler.HandleIncoming(rawData);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LanClient] Failed to handle packet {packetType}: {ex}");
                }

                scope.End(1, size);
            }
        }

        private void UpdateBandwidth()
        {
            var metrics = Metrics;
            if (metrics == null || _client == null || !_client.IsConnected)
            {
                _riptideInBw = 0f;
                _riptideOutBw = 0f;
                _riptideInPps = 0;
                _riptideOutPps = 0;
                return;
            }

            float now = Time.realtimeSinceStartup;
            float dt = now - _lastBwPollTime;
            if (dt >= 1f)
            {
                _riptideInBw = (metrics.BytesIn - _lastBytesIn) / dt;
                _riptideOutBw = (metrics.BytesOut - _lastBytesOut) / dt;
                _riptideInPps = (int)((metrics.MessagesIn - _lastMsgIn) / dt);
                _riptideOutPps = (int)((metrics.MessagesOut - _lastMsgOut) / dt);

                _lastBytesIn = metrics.BytesIn;
                _lastBytesOut = metrics.BytesOut;
                _lastMsgIn = (int)metrics.MessagesIn;
                _lastMsgOut = (int)metrics.MessagesOut;
                _lastBwPollTime = now;
            }
        }

        private void ResetBandwidth()
        {
            var metrics = Metrics;
            if (metrics != null)
            {
                _lastBytesIn = metrics.BytesIn;
                _lastBytesOut = metrics.BytesOut;
                _lastMsgIn = (int)metrics.MessagesIn;
                _lastMsgOut = (int)metrics.MessagesOut;
            }
            else
            {
                _lastBytesIn = 0;
                _lastBytesOut = 0;
                _lastMsgIn = 0;
                _lastMsgOut = 0;
            }
            _lastBwPollTime = Time.realtimeSinceStartup;
            _riptideInBw = 0f;
            _riptideOutBw = 0f;
            _riptideInPps = 0;
            _riptideOutPps = 0;
        }

        public override float IncomingBandwidth => _riptideInBw;
        public override float OutgoingBandwidth => _riptideOutBw;
        public override int IncomingPps => _riptideInPps;
        public override int OutgoingPps => _riptideOutPps;

        public override void ReconnectToSession()
        {
            using var _ = Profiler.Scope();

            string ip = MultiplayerSession.ServerIp;
            int port = MultiplayerSession.ServerPort;
            ConnectToHost(ip, port);
        }

        public override void Update()
        {
            using var _ = Profiler.Scope();

            _client?.Update();
            UpdateBandwidth();
        }

        private ulong GetClientID()
        {
            using var _ = Profiler.Scope();

            if (_client == null || _client.IsNotConnected)
                return Utils.NilUlong();

            return _client.Id;
        }

        public void AddClientToList(ulong id)
        {
            using var _ = Profiler.Scope();

            if (ClientList.Contains(id))
                return;

            ClientList.Add(id);

            var boxedId = Boxed<ulong>.Get(id);
			Game.Instance?.Trigger(MP_HASHES.OnPlayerJoined, boxedId);
            boxedId.Release();

		}

        public void RemoveClientFromList(ulong id)
        {
            using var _ = Profiler.Scope();

            if (!ClientList.Contains(id))
                return;

            ClientList.Remove(id);

            string name = MultiplayerSession.KnownPlayerNames.TryGetValue(id, out var cached) ? cached : $"Player {id}";
            OxySyncChat.AddSystemMessage(string.Format(ONI_United.STRINGS.UI.MP_CHATWINDOW.CHAT_CLIENT_LEFT, name));
            Utils.PauseSimOnPlayerLeft();
        }

        public override NetworkIndicatorsScreen.NetworkState GetJitterState()
        {
            using var _ = Profiler.Scope();

            if (_client == null || !_client.IsConnected)
                return NetworkIndicatorsScreen.NetworkState.BAD;

            int ping = _client.RTT;
            if (ping <= 0)
                return NetworkIndicatorsScreen.NetworkState.BAD;

            _pingSamples.Enqueue(ping);
            while (_pingSamples.Count > JITTER_SAMPLE_COUNT)
                _pingSamples.Dequeue();

            if (_pingSamples.Count < 5)
                return NetworkIndicatorsScreen.NetworkState.DEGRADED;

            float mean = 0f;
            foreach (var p in _pingSamples)
                mean += p;
            mean /= _pingSamples.Count;

            float variance = 0f;
            foreach (var p in _pingSamples)
            {
                float diff = p - mean;
                variance += diff * diff;
            }

            float jitter = Mathf.Sqrt(variance / _pingSamples.Count);

            if (jitter <= 10f)
                return NetworkIndicatorsScreen.NetworkState.GOOD;

            if (jitter <= 30f)
                return NetworkIndicatorsScreen.NetworkState.DEGRADED;

            return NetworkIndicatorsScreen.NetworkState.BAD;
        }

        public override NetworkIndicatorsScreen.NetworkState GetLatencyState()
        {
            using var _ = Profiler.Scope();

            if (_client == null || !_client.IsConnected)
                return NetworkIndicatorsScreen.NetworkState.BAD;

            int ping = _client.SmoothRTT;
            if (ping <= 0)
                return NetworkIndicatorsScreen.NetworkState.BAD;

            if (ping <= NetworkConfig.PingRanges.DEGRADED)
                return NetworkIndicatorsScreen.NetworkState.GOOD;

            if (ping <= NetworkConfig.PingRanges.BAD)
                return NetworkIndicatorsScreen.NetworkState.DEGRADED;

            return NetworkIndicatorsScreen.NetworkState.BAD;
        }

        public override NetworkIndicatorsScreen.NetworkState GetPacketlossState()
        {
            using var _ = Profiler.Scope();

            var metrics = Metrics;
            if (metrics == null)
                return NetworkIndicatorsScreen.NetworkState.BAD;

            float lossRate = metrics.RollingNotifyLossRate; // 0–1
            float quality = 1f - lossRate;

            if (quality >= 0.95f)
                return NetworkIndicatorsScreen.NetworkState.GOOD;

            if (quality >= 0.85f)
                return NetworkIndicatorsScreen.NetworkState.DEGRADED;

            return NetworkIndicatorsScreen.NetworkState.BAD;
        }

        public override NetworkIndicatorsScreen.NetworkState GetServerPerformanceState()
        {
            using var _ = Profiler.Scope();

            // Until this is improved later just assume good.
            return NetworkIndicatorsScreen.NetworkState.GOOD;

            if (_client == null || !_client.IsConnected)
                return NetworkIndicatorsScreen.NetworkState.BAD;

            var metrics = Metrics;
            if (metrics == null)
                return NetworkIndicatorsScreen.NetworkState.BAD;

            var reliableSends = metrics.RollingReliableSends;

            double meanResends = reliableSends.Mean;
            double resendStdDev = reliableSends.StandardDev;

            float lossRate = metrics.RollingNotifyLossRate;
            float remoteQuality = 1f - lossRate;


            DebugConsole.Log(
                $"[NET] Resends(mean={meanResends:F2}, std={resendStdDev:F2}) | " +
                $"Loss={lossRate:P2} | Quality={remoteQuality:P2}"
            );

            if (meanResends >= 2.0 ||           // On average needs 2+ sends per reliable
                resendStdDev >= 1.0 ||          // Highly unstable resend behavior
                remoteQuality <= 0.85f)         // Bad server quality
            {
                return NetworkIndicatorsScreen.NetworkState.BAD;
            }

            if (meanResends >= 1.2 ||            // Frequent retransmits
                resendStdDev >= 0.5 ||           // Congestion spikes
                remoteQuality <= 0.95f)          // Degraded server quality
            {
                return NetworkIndicatorsScreen.NetworkState.DEGRADED;
            }

            return NetworkIndicatorsScreen.NetworkState.GOOD;
        }

        IEnumerator WaitForConnectionSuccess(int timeout)
        {
            using var _ = Profiler.Scope();
            try
            {
                float startedAt = Time.realtimeSinceStartup;

                while (Time.realtimeSinceStartup - startedAt < timeout)
                {
                    if (_client == null)
                        yield break;

                    _client?.Update();

                    if (_client.IsConnected)
                    {
                        DebugConsole.Log("[LanClient] Connection successful");
                        MultiplayerOverlay.Close();
                        yield break;
                    }

                    if (GameClient.State != ClientState.Connecting)
                        yield break;

                    yield return null;
                }

                if (_client == null || _client.IsConnected)
                    yield break;

                if (GameClient.State != ClientState.Connecting)
                    yield break;

                DebugConsole.LogWarning("[LanClient] Connection timed out");
                OnConnectionFailed?.Invoke();

                CleanupRiptide();
                NetworkConfig.TransportClient.OnReturnToMenu.Invoke(
                    CLIENT.RIPTIDE.CONNECTION_FAILED,
                    CLIENT.RIPTIDE.CONNECTION_FAILED_DESC
                );
            }
            finally
            {
                _connectAttemptInFlight = false;
            }
        }

        private void CleanupRiptide()
        {
            using var _ = Profiler.Scope();

            if (_client == null)
                return;

            try
            {
                _client.Connected -= OnConnectedToServer;
                _client.Disconnected -= OnDisconnectedFromServer;
                _client.MessageReceived -= OnMessageRecievedFromServer;
                _client.ClientConnected -= OnOtherClientConnected;
                _client.ClientDisconnected -= OnOtherClientDisconnected;
                _client.Disconnect();

                MultiplayerSession.ServerIp = "127.0.0.1";
                MultiplayerSession.ServerPort = 7777;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LanClient] Error during timeout cleanup: {ex}");
            }
            finally
            {
                _client = null;
            }

            CLIENT_ID = Utils.NilUlong();
            ClientList.Clear();
            if (MultiplayerSession.InActiveSession)
                MultiplayerSession.OnDisconnectedFromHost(null);
            else
                MultiplayerSession.SetHost(Utils.NilUlong());
        }

        /*IEnumerator Handshake()
        {
            // Recycle the handshake packet
            HandshakePacket handshake = new HandshakePacket();
            while (_client != null && _client.IsConnected)
            {
                if (_client.Connection == null)
                {
                    Debug.Log("[Handshake] Connection is null, waiting...");
                    yield return null;
                    continue;
                }

                Debug.Log("[Handshake] Sending handshake packet...");
                NetworkConfig.TransportPacketSender.SendToConnection(_client.Connection, handshake, SteamNetworkingSend.Reliable);

                yield return new WaitForSeconds(1f);

                if (_client.IsNotConnected)
                {
                    Debug.Log("[Handshake] Client disconnected, stopping handshake coroutine.");
                    break;
                }
            }
            yield return null;
        }*/

        private (string reason, string message) GetDisconnectInfo(DisconnectedEventArgs e)
        {
            switch (e.Reason)
            {
                case DisconnectReason.NeverConnected:
                    return (
                        CLIENT.RIPTIDE.CONNECTION_FAILED,
                        CLIENT.RIPTIDE.CONNECTION_FAILED_DESC
                    );

                case DisconnectReason.ConnectionRejected:
                    return (
                        CLIENT.RIPTIDE.CONNECTION_REJECTED,
                        CLIENT.RIPTIDE.CONNECTION_REJECTED_DESC
                    );

                case DisconnectReason.TransportError:
                    return (
                        CLIENT.RIPTIDE.NETWORK_ERROR,
                        CLIENT.RIPTIDE.NETWORK_ERROR_DESC
                    );

                case DisconnectReason.TimedOut:
                    return (
                        CLIENT.RIPTIDE.CONNECTION_TIMED_OUT,
                        CLIENT.RIPTIDE.CONNECTION_TIMED_OUT_DESC
                    );

                case DisconnectReason.Kicked:
                    return (
                        CLIENT.RIPTIDE.KICKED,
                        CLIENT.RIPTIDE.KICKED_DESC
                    );

                case DisconnectReason.ServerStopped:
                    return (
                        CLIENT.RIPTIDE.SERVER_CLOSED,
                        CLIENT.RIPTIDE.SERVER_CLOSED_DESC
                    );

                case DisconnectReason.PoorConnection:
                    return (
                        CLIENT.RIPTIDE.CONNECTION_UNSTABLE,
                        CLIENT.RIPTIDE.CONNECTION_UNSTABLE_DESC
                    );

                case DisconnectReason.Disconnected:
                    return ("", ""); // client initiated

                default:
                    return (
                        CLIENT.RIPTIDE.UNKNOWN,
                        CLIENT.RIPTIDE.UNKNOWN_DESC
                    );
            }
        }

        public override int GetPing()
        {
            using var _ = Profiler.Scope();

            if (_client == null || !_client.IsConnected)
                return -1;

            return _client.SmoothRTT;
        }
    }
}
