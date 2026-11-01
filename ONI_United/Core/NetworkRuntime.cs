using System;
using System.Collections.Generic;
using ONI_United.Misc;
using ONI_United.Networking.Sessions.States;
using ONI_United.Networking.Transport;
using ONI_United.Networking.Transport.LiteNetLib;
using ONI_United.Networking.Transport.Steamworks;
using ONI_United.Patches.ToolPatches;
using ONI_United.DebugTools;
using Shared.Profiling;
using Steamworks;
using UnityEngine;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Runtime;
using ONI_United.Networking;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transfer;
using Shared.Networking;

namespace ONI_United.Core
{
    public class NetworkRuntime : MonoBehaviour
    {
        public class PingRanges
        {
            // Anything less then degraded is considered good
            public static readonly int DEGRADED = 120;
            public static readonly int BAD = 150;
        }

        public enum NetworkTransport
        {
            STEAMWORKS = 0,
            LITENETLIB = 1
        }

        private static NetworkRuntime instance = null!;

        public static NetworkRuntime Instance => instance;

        private static NetworkTransport transport = NetworkTransport.LITENETLIB;

        public static bool IsSteam => transport == NetworkTransport.STEAMWORKS;
        public static bool IsLan => transport == NetworkTransport.LITENETLIB;

        private void Awake()
        {
            if (instance != null && !ReferenceEquals(instance, this))
            {
                enabled = false;
                Destroy(this);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (!ReferenceEquals(instance, this)) return;
            try { TransportServer.DisposeHost(); }
            finally { instance = null!; }
        }

        public static void Initialize()
        {
            if (instance == null)
                throw new System.InvalidOperationException("The NetworkRuntime component has not been attached.");

            EnsurePersistentUserId();
            UpdateTransport((NetworkTransport)Configuration.Instance.Network.Transport);

            if (SteamManager.Initialized)
            {
                SteamLobby.Initialize();
                SteamNetworkingUtils.InitRelayNetworkAccess();
            }
        }

        internal static void EnsurePersistentUserId()
        {
            using var _ = Profiler.Scope();

            var config = Configuration.Instance;
            if (config.Network.PersistentUserId != 0)
                return;

            ulong persistentUserId;
            do
            {
                persistentUserId = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
            }
            while (persistentUserId == 0);

            config.Network.PersistentUserId = persistentUserId;
            config.Save();
        }

        public static TransportServer TransportServer { get; private set; } = new LiteNetLibServer();
        public static TransportClient TransportClient { get; private set; } = new LiteNetLibClient();
        public static TransportPacketSender TransportPacketSender { get; private set; } = new LiteNetLibPacketSender();

        public static readonly int LOBBY_SIZE_MIN = 2;
        public static readonly int LOBBY_SIZE_DEFAULT = 4;
        public static readonly int LOBBY_SIZE_MAX = 16;

        public static void StartServer()
        {
            if (!Lifecycle.Handle(Lifecycle.Event.Start, TransportServer.Start))
                DebugConsole.LogError("[NetworkRuntime] Host start is not permitted in the current lifecycle state.");
        }

        /// <summary>
        /// Stops the server based off the current transport
        /// </summary>
        public static void Stop()
        {
            if (Lifecycle.HasHostOperation)
            {
                if (!Lifecycle.Handle(Lifecycle.Event.Stop, TransportServer.Stop))
                    DebugConsole.LogError("[NetworkRuntime] Stop is not permitted in the current lifecycle state.");
                return;
            }
            switch(transport)
            {
                case NetworkTransport.STEAMWORKS:
                    StopSteamworks();
                    break;
                case NetworkTransport.LITENETLIB:
                    StopRaw();
                    break;
            }

            MultiplayerSession.Clear();
            GameClient.Handle(ClientEvent.TransportDisconnected);
        }

        private static void StopSteamworks()
        {
            SteamLobby.LeaveLobby();
        }

        private static void StopRaw()
        {
            if (MultiplayerSession.IsClient)
                GameClient.Disconnect();

            NetworkIdentityRegistry.Clear();

            SelectToolPatch.UpdateColor();
        }

        public static void UpdateTransport(NetworkTransport newTransport)
        {
            newTransport = (NetworkTransport)TransportConfigurationMigration.NormalizeNetworkTransport((int)newTransport);

            // A lobby join owns its native result before GameClient becomes Connecting.
            if (SteamLobby.HasPendingJoin && !transport.Equals(newTransport))
                throw new InvalidOperationException("Wait for the pending Steam lobby join before changing transport.");
            if (Lifecycle.HasHostOperation && !transport.Equals(newTransport))
                throw new InvalidOperationException("Stop the owned host before changing transport.");

            if (transport.Equals(newTransport))
                return;

            transport = newTransport;
            TransportServer = GetTransportServer();
            TransportClient = GetTransportClient();
            TransportPacketSender = GetTransportPacketSender();
            DebugConsole.Log($"Updated network transport to: {newTransport.ToString()}");
        }

        public static void SaveTransport()
        {
            Configuration.Instance.Network.Transport = (int)transport;
            Configuration.Instance.Save();
        }

        private static TransportServer GetTransportServer()
        {
            using var _ = Profiler.Scope();
            return TransportRegistry.GetTransport((TransportProtocol)transport).CreateServer();
        }

        private static TransportClient GetTransportClient()
        {
            using var _ = Profiler.Scope();
            return TransportRegistry.GetTransport((TransportProtocol)transport).CreateClient();
        }

        private static TransportPacketSender GetTransportPacketSender()
        {
            using var _ = Profiler.Scope();
            return TransportRegistry.GetTransport((TransportProtocol)transport).CreatePacketSender();
        }

        public static ulong GetLocalID()
        {
            using var _ = Profiler.Scope();

            switch (transport)
            {
                case NetworkTransport.STEAMWORKS:
                    return SteamUser.GetSteamID().m_SteamID;
                case NetworkTransport.LITENETLIB:
                    return Configuration.Instance.Network.PersistentUserId;
                default:
                    return Utils.NilUlong();
            }
        }

        public static int GetMaxServerCapacity()
        {
            switch (transport)
            {
                case NetworkTransport.STEAMWORKS:
                    if (SteamLobby.InLobby)
                        return SteamMatchmaking.GetLobbyMemberLimit(SteamLobby.CurrentLobby);
                    break;
                case NetworkTransport.LITENETLIB:
                    return Configuration.Instance.Host.MaxLobbySize;
            }
            return Configuration.Instance.Host.MaxLobbySize;
        }

        public static List<ulong> GetConnectedClients()
        {
            using var _ = Profiler.Scope();

            List<ulong> clients = new List<ulong>();
            switch(transport)
            {
                case NetworkTransport.STEAMWORKS:
                    List<CSteamID> members = SteamLobby.GetAllLobbyMembers();
                    foreach(CSteamID member in members)
                    {
                        clients.Add(member.m_SteamID);
                    }
                    break;
                case NetworkTransport.LITENETLIB:
                    if (MultiplayerSession.IsClient)
                    {
                        return new List<ulong>(MultiplayerSession.ConnectedPlayers.Keys);
                    }
                    else
                    {
                        LiteNetLibServer server = TransportServer as LiteNetLibServer;
                        return server?.ClientList ?? clients;
                    }
            }
            return clients;
        }

        public const float MaxMissedTicks = 3f;

        private static float _tickAccumulator;
        private static float _tickInterval = 1f / 60f;
        public static float TickInterval => _tickInterval;

        public static void RefreshTickRate()
        {
            if (Configuration.Instance?.Host?.Server != null)
                _tickInterval = 1f / TickRateToTps(Configuration.Instance.Host.Server.TickRate);
        }

        private static int TickRateToTps(ServerTickRate rate) => rate switch
        {
            ServerTickRate.TPS_20 => 20,
            ServerTickRate.TPS_30 => 30,
            ServerTickRate.TPS_60 => 60,
            ServerTickRate.TPS_90 => 90,
            ServerTickRate.TPS_120 => 120,
            ServerTickRate.TPS_128 => 128,
            _ => 60,
        };

        internal static void InitializeHostTick()
        {
            RefreshTickRate();
            _tickAccumulator = 0f;
        }

        public static UnityTaskScheduler scheduler = new UnityTaskScheduler();

        private void Update()
        {
            if (!ReferenceEquals(instance, this)) return;

            using var _ = Profiler.Scope();

            TransportServer.PollCancellation();

            scheduler.Tick();

            if (transport.Equals(NetworkTransport.STEAMWORKS))
            {
                if (!SteamManager.Initialized)
                    return;
            }

            if (Lifecycle.Current == Lifecycle.Phase.Hosting && MultiplayerSession.IsHost)
            {
                _tickAccumulator += Time.unscaledDeltaTime;
                _tickAccumulator = Mathf.Min(_tickAccumulator, _tickInterval * MaxMissedTicks);
                if (_tickAccumulator >= _tickInterval)
                {
                    _tickAccumulator -= _tickInterval;
                    TransportServer.Update();
                    TransportServer.OnMessageRecieved();
                    SaveFileTransferManager.CheckForLostChunks();
                }
            }
            else
            {
                GameClient.Poll();

                if (MultiplayerSession.IsClient && MultiplayerSession.HostUserID.IsValid())
                {
                    // Check for inactive transfers and request missing chunks
                    ONI_United.Networking.Transfer.SaveChunkAssembler.CheckInactiveTransfers();
                }
            }
            TransportPacketSender.Flush();
        }
    }
}
