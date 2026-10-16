using System;
using System.Collections;
using System.Collections.Generic;
using ONI_United.Misc;
using ONI_United.Networking.Session.States;
using ONI_United.Networking.Transport;
using ONI_United.Networking.Transport.LiteNetLib;
using ONI_United.Networking.Transport.Riptide;
using ONI_United.Networking.Transport.Steamworks;
using ONI_United.Patches.ToolPatches;
using ONI_United.DebugTools;
using Shared;
using Shared.Profiling;
using Steamworks;
using UnityEngine;
using ONI_United;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Session;
using GameServer = ONI_United.Networking.Lifecycle.GameServer;

namespace ONI_United.Networking
{
    public static class NetworkConfig
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
            LITENETLIB = 1,
            RIPTIDE = 2
        }
        public static NetworkTransport transport { get; private set; } = NetworkTransport.LITENETLIB;

        public static TransportServer TransportServer { get; set; } = new LiteNetLibServer();
        public static TransportClient TransportClient { get; set; } = new LiteNetLibClient();
        public static TransportPacketSender TransportPacketSender { get; set; } = new LiteNetLibPacketSender();

        public static readonly int LOBBY_SIZE_MIN = 2;
        public static readonly int LOBBY_SIZE_DEFAULT = 4;
        public static readonly int LOBBY_SIZE_MAX = 16;

        /// <summary>
        /// Starts a GameServer on the current transport
        /// </summary>
        public static void StartServer()
        {
            switch(transport)
            {
                case NetworkTransport.STEAMWORKS:
                    UpdateTransport(NetworkTransport.STEAMWORKS);
                    StartSteamServer();
                    break;
                case NetworkTransport.LITENETLIB:
                case NetworkTransport.RIPTIDE:
                    UpdateTransport(transport);
                    CoroutineRunner.RunOne(StartRawDelayed(0.5f));
                    break;
            }
        }

        private static void StartSteamServer()
        {
            SteamLobby.CreateLobby(onSuccess: () =>
            {
                //SpeedControlScreen.Instance?.Unpause(false);
                SpeedControlScreen.Instance.Pause(true);
            });
        }

        private static IEnumerator StartRawDelayed(float delay = 1f)
        {
            yield return new WaitForSecondsRealtime(delay);
            StartRawServer();
        }

        private static void StartRawServer()
        {
            MultiplayerSession.Clear();
            try
            {
                GameServer.Start();
            }
            catch (Exception ex)
            {
                DebugConsole.LogError($"Failed to start LAN game server: {ex.Message}\n{ex.StackTrace}");
            }
            SelectToolPatch.UpdateColor();
            SpeedControlScreen.Instance.Pause(true);
        }

        /// <summary>
        /// Stops the server based off the current transport
        /// </summary>
        public static void Stop()
        {
            switch(transport)
            {
                case NetworkTransport.STEAMWORKS:
                    StopSteamworks();
                    break;
                case NetworkTransport.LITENETLIB:
                case NetworkTransport.RIPTIDE:
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
            if (MultiplayerSession.IsHost)
                GameServer.Shutdown();

            if (MultiplayerSession.IsClient)
                GameClient.Disconnect();

            NetworkIdentityRegistry.Clear();

            SelectToolPatch.UpdateColor();
        }

        public static void UpdateTransport(NetworkTransport newTransport)
        {
            if (transport.Equals(newTransport))
                return;
            
            transport = newTransport;
            TransportServer = GetTransportServer();
            TransportClient = GetTransportClient();
            TransportPacketSender = GetTransportPacketSender();
            DebugConsole.Log($"Updated network transport to: {newTransport.ToString()}");
        }

        public static void UpdateLanTransport()
        {
            UpdateTransport((NetworkTransport)Configuration.Instance.Host.LanSettings.Transport);
        }

        public static TransportServer GetTransportServer()
        {
            using var _ = Profiler.Scope();
            return TransportRegistry.GetTransport((TransportProtocol)transport).CreateServer();
        }

        public static TransportClient GetTransportClient()
        {
            using var _ = Profiler.Scope();
            return TransportRegistry.GetTransport((TransportProtocol)transport).CreateClient();
        }

        public static TransportPacketSender GetTransportPacketSender()
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
                case NetworkTransport.RIPTIDE:
                    return MultiplayerSession.IsClient ? RiptideClient.CLIENT_ID : RiptideServer.CLIENT_ID;
                default:
                    return Utils.NilUlong();
            }
        }

        public static bool IsSteamConfig()
        {
            using var _ = Profiler.Scope();

            return transport.Equals(NetworkTransport.STEAMWORKS);
        }

        public static bool IsLanConfig()
        {
            using var _ = Profiler.Scope();

            return transport.Equals(NetworkTransport.LITENETLIB) || transport.Equals(NetworkTransport.RIPTIDE);
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
                case NetworkTransport.RIPTIDE:
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
    }
}
