using ONI_United.Core;
using LiteNetLib;
using LiteNetLib.Utils;
using ONI_United.DebugTools;
using ONI_United.Misc;
using ONI_United.Networking.Replication.Components;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Sessions.States;
using ONI_United.Networking.Transfer;
using Shared.Profiling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using ONI_United.Misc.World;
using ONI_United;
using ONI_United.Networking;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transport;

namespace ONI_United.Networking.Transport.LiteNetLib
{
    public class LiteNetLibServer : TransportServer
    {
        private const string ClientIdentityHandshake = "ONI_TOGETHER";
        private static NetManager _server;
        private static EventBasedNetListener _listener;

        private TcpFileTransferServer _tcpTransfer;
        private System.Action<bool, string> _shutdownFinished;
        private string _shutdownFailure;
        private readonly Dictionary<ulong, NetPeer> _peersByClientId = new Dictionary<ulong, NetPeer>();
        private readonly Dictionary<int, ulong> _clientIdByPeerId = new Dictionary<int, ulong>();
        private readonly ConcurrentQueue<(ulong clientId, NetPeer peer, byte[] data)> _incomingPackets =
            new ConcurrentQueue<(ulong, NetPeer, byte[])>();

        public static NetManager ServerInstance => _server;
        public bool IsRunning => _server != null && _server.IsRunning;
        public string LastStartFailure { get; private set; }
        private string CleanupFailure => _tcpTransfer?.CleanupFailure;
        private bool IsStopped => _server == null && (_tcpTransfer == null || _tcpTransfer.IsStopped);
        public int ConnectedClientCount => _server != null ? _server.ConnectedPeersCount : 0;
        public TcpFileTransferServer TcpTransfer => _tcpTransfer?.IsRunning == true ? _tcpTransfer : null;

        public List<ulong> ClientList { get; internal set; } = new List<ulong>();

        // Bandwidth and PPS tracking
        private long _srvLastBytesIn, _srvLastBytesOut;
        private int _srvLastMsgIn, _srvLastMsgOut;
        private float _srvInBw, _srvOutBw;
        private int _srvInPps, _srvOutPps;
        private float _srvLastBwPollTime;

        public override float IncomingBandwidth => _srvInBw;
        public override float OutgoingBandwidth => _srvOutBw;
        public override int IncomingPps => _srvInPps;
        public override int OutgoingPps => _srvOutPps;

        public override bool TryGetRemotePlayerId(object connection, out ulong playerId)
        {
            if (connection is NetPeer peer)
                return _clientIdByPeerId.TryGetValue(peer.Id, out playerId);

            playerId = 0;
            return false;
        }

        public override void DisconnectConnection(object connection, string reason)
        {
            if (connection is NetPeer peer)
                peer.Disconnect();
        }

        public override void OnSessionPlayerAdmitted(ulong playerId)
        {
            if (!ClientList.Contains(playerId))
            {
                ClientList.Add(playerId);
                if (playerId == NetworkRuntime.GetLocalID())
                {
                    OxySyncChat.AddSystemMessage(string.Format(ONI_United.STRINGS.UI.MP_CHATWINDOW.CHAT_CLIENT_JOINED, Utils.GetLocalPlayerName()));
                    DebugConsole.Log("[LiteNetLibServer] Host session registered for " + playerId);
                }
            }
        }

        private void Prepare()
        {
            using var _ = Profiler.Scope();
        }

        protected override void StartProtocol()
        {
            using var _ = Profiler.Scope();

            Prepare();
            LastStartFailure = null;
            if (!Utils.IsInGame() || Grid.CellCount <= 0)
            {
                ReportStartupFailure("Load a colony before starting a LAN host.");
                return;
            }
            if (Configuration.Instance.Host.LanPort < 1 || Configuration.Instance.Host.LanPort > 65535)
            {
                ReportStartupFailure("The configured LAN port is outside the valid range.");
                return;
            }
            if (_tcpTransfer != null && !_tcpTransfer.IsStopped)
                throw new InvalidOperationException("The previous TCP transfer server has not finished stopping.");
            _tcpTransfer = null;
            if (_server != null)
            {
                ReportStartupFailure("A LiteNetLib server instance already exists.");
                return;
            }

            int port = Configuration.Instance.Host.LanPort;
            int maxClients = Configuration.Instance.Host.MaxLobbySize;
            ulong hostId = NetworkRuntime.GetLocalID();
            if (hostId == 0)
            {
                LastStartFailure = "Cannot start with an empty persistent host ID.";
                ReportStartupFailure(LastStartFailure);
                return;
            }

            _listener = new EventBasedNetListener();
            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.NetworkReceiveEvent += OnNetworkReceive;
            _listener.NetworkErrorEvent += OnNetworkError;
            _listener.NetworkReceiveUnconnectedEvent += OnNetworkReceiveUnconnected;

            // UnsyncedEvents = false ensures ALL events fire on Unity Main Thread inside PollEvents()
            _server = new NetManager(_listener)
            {
                AutoRecycle = true,
                DisconnectTimeout = Configuration.Instance.Host.TimeoutSeconds * 1000,
                UnsyncedEvents = false,
                ChannelsCount = 4,
                BroadcastReceiveEnabled = true,
                EnableStatistics = true
            };

            bool started;
            try { started = _server.Start(port); }
            catch (SocketException ex)
            {
                ReportStartupFailure(ex.Message);
                return;
            }
            if (!started)
            {
                LastStartFailure = "Failed to bind the LiteNetLib listener to port " + port + ".";
                ReportStartupFailure(LastStartFailure);
                return;
            }

            DebugConsole.Log("[LiteNetLibServer] Listener started on port " + port);
            OxySyncChat.AddSystemMessage(string.Format(ONI_United.STRINGS.UI.MP_CHATWINDOW.CHAT_SERVER_STARTED, "LiteNetLib (LAN)"));

            if (Lifecycle.Current != Lifecycle.Phase.StartingHost || _server == null || !_server.IsRunning) return;

            try
            {
                _tcpTransfer = new TcpFileTransferServer();
                _tcpTransfer.Start(port);
            }
            catch (Exception ex)
            {
                DebugConsole.LogWarning("[LiteNetLibServer] TCP file transfer server failed to start: " + ex.Message);
                // Preserve the optional TCP fallback, but release partial acquisitions and
                // retain the owner until its workers have finished. Cleanup failure cancels startup.
                try { _tcpTransfer?.Stop(); }
                catch (Exception cleanupError)
                {
                    ReportStartupFailure("Partial TCP startup cleanup failed: " + cleanupError);
                    return;
                }
            }

            ClientList.Clear();
            ReportTransportStarted();
        }

        private void OnConnectionRequest(ConnectionRequest request)
        {
            if (!Session.IsHost || !Lifecycle.IsHostGameplay
                || Lifecycle.Current != Lifecycle.Phase.InGameSession)
            {
                request.Reject();
                return;
            }
            if (_server.ConnectedPeersCount >= Configuration.Instance.Host.MaxLobbySize)
            {
                request.Reject();
                return;
            }

            if (!request.Data.TryGetString(out string magic)
                || magic != ClientIdentityHandshake
                || !request.Data.TryGetULong(out ulong clientId)
                || clientId == 0
                || request.Data.AvailableBytes != 0)
            {
                DebugConsole.LogWarning($"[LiteNetLibServer] Rejected malformed connection request from {request.RemoteEndPoint}.");
                request.Reject();
                return;
            }

            if (clientId == NetworkRuntime.GetLocalID() || _peersByClientId.ContainsKey(clientId))
            {
                DebugConsole.LogWarning($"[LiteNetLibServer] Rejected duplicate persistent client ID {clientId} from {request.RemoteEndPoint}.");
                request.Reject();
                return;
            }

            var peer = request.Accept();
            if (peer != null)
            {
                _peersByClientId[clientId] = peer;
                _clientIdByPeerId[peer.Id] = clientId;
            }
        }

        private void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            try
            {
                string req = reader.GetString();
                if (req == "ONI_DISCOVERY_REQ")
                {
                    var writer = new NetDataWriter();
                    writer.Put("ONI_DISCOVERY_RESP");
                    writer.Put(Utils.GetLocalPlayerName() ?? "Host");
                    writer.Put(SaveHelper.WorldName ?? "Colony");
                    writer.Put(GameClock.Instance != null ? GameClock.Instance.GetCycle() + 1 : 1);
                    writer.Put(MultiplayerSession.ConnectedPlayers.Count);
                    writer.Put(Configuration.Instance.Host.MaxLobbySize);
                    writer.Put(Configuration.Instance.Host.LanPort);
                    _server.SendUnconnectedMessage(writer, remoteEndPoint);
                }
            }
            catch (Exception ex)
            {
                DebugConsole.LogWarning("[LiteNetLibServer] Error in discovery response: " + ex.Message);
            }
        }

        private void OnPeerConnected(NetPeer peer)
        {
            using var _ = Profiler.Scope();

            if (!_clientIdByPeerId.TryGetValue(peer.Id, out ulong clientId))
            {
                DebugConsole.LogError($"[LiteNetLibServer] Accepted peer {peer.Id} has no client ID mapping; disconnecting.");
                peer.Disconnect();
                return;
            }

            DebugConsole.Log("[LiteNetLibServer] Physical connection established from " + clientId + " (" + peer.Address + ":" + peer.Port + ")");
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            using var _ = Profiler.Scope();

            if (_clientIdByPeerId.TryGetValue(peer.Id, out ulong clientId))
            {
                if (!_peersByClientId.TryGetValue(clientId, out var currentPeer) || !ReferenceEquals(currentPeer, peer))
                    return;
                _peersByClientId.Remove(clientId);
                _clientIdByPeerId.Remove(peer.Id);
                ClientList.Remove(clientId);

                if (MultiplayerSession.UnregisterClient(clientId, null, peer))
                {
                    DebugConsole.Log("[LiteNetLibServer] Player " + clientId + " disconnected. Reason: " + disconnectInfo.Reason);
                }

            }
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            if (_clientIdByPeerId.TryGetValue(peer.Id, out ulong clientId))
            {
                byte[] rawData = reader.GetRemainingBytes();
                _incomingPackets.Enqueue((clientId, peer, rawData));
            }
        }

        private void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            DebugConsole.LogWarning("[LiteNetLibServer] Network error from " + endPoint + ": " + socketError);
        }

        protected override void CancelProtocol(System.Action<bool, string> finished)
        {
            if (_shutdownFinished != null)
            {
                DebugConsole.Log("[LiteNetLibServer] Shutdown is already in progress; keeping its original completion. No additional request was scheduled.");
                return;
            }
            _shutdownFinished = finished;
            _shutdownFailure = StopResources();
            // Deliver from PollCancellation after the action stack has unwound, including
            // synchronous failures. No lifecycle state or frame-spread startup work here.
        }

        public override void PollCancellation()
        {
            var finished = _shutdownFinished;
            if (finished == null) return;
            string failure = _shutdownFailure ?? CleanupFailure;
            if (failure == null && !IsStopped) return;
            CancelShutdownObservation();
            finished(failure == null, failure);
        }

        private void CancelShutdownObservation()
        {
            _shutdownFinished = null;
            _shutdownFailure = null;
        }

        protected override void DisposeProtocol()
        {
            // Stop observing completion before terminal resource release.
            CancelShutdownObservation();
            string failure = StopResources();
            if (failure != null) DebugConsole.LogError("[LiteNetLibServer] Disposal failed: " + failure);
        }

        private string StopResources()
        {
            var errors = new List<string>();
            try { CloseConnections(); }
            catch (Exception ex) { errors.Add(ex.Message); }
            try { StopResourcesCore(); }
            catch (Exception ex) { errors.Add(ex.Message); }
            return errors.Count == 0 ? null : string.Join("; ", errors);
        }

        private void StopResourcesCore()
        {
            using var _ = Profiler.Scope();

            var errors = new List<Exception>();
            try
            {
                _server?.Stop();
                _server = null;
                _listener = null;
            }
            catch (Exception ex) { errors.Add(ex); }
            try { _tcpTransfer?.Stop(); }
            catch (Exception ex) { errors.Add(ex); }

            _peersByClientId.Clear();
            _clientIdByPeerId.Clear();
            ClientList.Clear();
            while (_incomingPackets.TryDequeue(out var _)) { }
            if (errors.Count != 0) throw new AggregateException("LAN transport cleanup failed.", errors);
        }

        public override void CloseConnections()
        {
            using var _ = Profiler.Scope();

            if (_server == null)
                return;

            _server.DisconnectAll();
            _peersByClientId.Clear();
            _clientIdByPeerId.Clear();
            ClientList.Clear();
        }

        public override void Update()
        {
            using var _ = Profiler.Scope();

            if (_server == null)
                return;

            _server.PollEvents();
            OnMessageRecieved();
            UpdateMetrics();
        }

        public override void OnMessageRecieved()
        {
            using var _ = Profiler.Scope();

            while (_incomingPackets.TryDequeue(out var packet))
            {
                try
                {
                    PacketHandler.HandleIncoming(packet.data, packet.clientId, packet.peer);
                }
                catch (Exception ex)
                {
                    DebugConsole.LogError("[LiteNetLibServer] Exception while handling packet from " + packet.clientId + ": " + ex);
                }
            }
        }

        public override void KickClient(ulong clientId)
        {
            using var _ = Profiler.Scope();

            if (_peersByClientId.TryGetValue(clientId, out var peer))
            {
                peer.Disconnect();
                _peersByClientId.Remove(clientId);
                _clientIdByPeerId.Remove(peer.Id);
                ClientList.Remove(clientId);
                MultiplayerSession.UnregisterClient(clientId, null, peer);
                DebugConsole.Log("[LiteNetLibServer] Kicked client: " + clientId);
            }
        }

        private void UpdateMetrics()
        {
            if (_server == null)
                return;

            float now = Time.realtimeSinceStartup;
            float dt = now - _srvLastBwPollTime;
            if (dt < 1f)
                return;

            long totalBytesIn = _server.Statistics.BytesReceived;
            long totalBytesOut = _server.Statistics.BytesSent;
            long totalPacketsIn = _server.Statistics.PacketsReceived;
            long totalPacketsOut = _server.Statistics.PacketsSent;

            _srvInBw = (totalBytesIn - _srvLastBytesIn) / dt;
            _srvOutBw = (totalBytesOut - _srvLastBytesOut) / dt;
            _srvInPps = (int)((totalPacketsIn - _srvLastMsgIn) / dt);
            _srvOutPps = (int)((totalPacketsOut - _srvLastMsgOut) / dt);

            _srvLastBytesIn = totalBytesIn;
            _srvLastBytesOut = totalBytesOut;
            _srvLastMsgIn = (int)totalPacketsIn;
            _srvLastMsgOut = (int)totalPacketsOut;
            _srvLastBwPollTime = now;
        }
    }
}
