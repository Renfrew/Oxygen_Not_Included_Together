using System;
using System.Collections.Generic;
using ONI_United.Misc;
using ONI_United.Networking.Sessions.States;
using ONI_United.Networking.Transport;
using ONI_United.Networking.Transport.LiteNetLib;
using ONI_United.Networking.Transport.Steamworks;
using ONI_United.Patches.ToolPatches;
using ONI_United.DebugTools;
using Shared;
using Shared.Profiling;
using Steamworks;
using UnityEngine;
using ONI_United;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Runtime;
using ONI_United.StateMachines;
using ONI_United.Networking.Sessions;
using Shared.Networking;

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
            LITENETLIB = 1
        }
        public static NetworkTransport transport { get; private set; } = NetworkTransport.LITENETLIB;

        public static TransportServer TransportServer { get; set; } = new LiteNetLibServer();
        public static TransportClient TransportClient { get; set; } = new LiteNetLibClient();
        public static TransportPacketSender TransportPacketSender { get; set; } = new LiteNetLibPacketSender();

        public static readonly int LOBBY_SIZE_MIN = 2;
        public static readonly int LOBBY_SIZE_DEFAULT = 4;
        public static readonly int LOBBY_SIZE_MAX = 16;

        public static void StartServer()
        {
            if (!TransportServer.Start(() =>
            {
                SelectToolPatch.UpdateColor();
                SpeedControlScreen.Instance?.Pause(true);
            })) DebugConsole.LogError("[NetworkConfig] Host start is not permitted in the current lifecycle state.");
        }

        /// <summary>
        /// Stops the server based off the current transport
        /// </summary>
        public static void Stop()
        {
            if (Lifecycle.HasHostOperation)
            {
                if (!TransportServer.Stop())
                    DebugConsole.LogError("[NetworkConfig] Stop is not permitted in the current lifecycle state.");
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

        public static void ApplyConfiguredTransport()
        {
            Configuration config = Configuration.Instance;
            config.NormalizeTransportSettings();
            UpdateTransport((NetworkTransport)config.Host.NetworkTransport);
        }

        public static void UpdateLanTransport()
        {
            Configuration config = Configuration.Instance;
            config.NormalizeTransportSettings();
            UpdateTransport((NetworkTransport)config.Host.LanSettings.Transport);
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

            return transport.Equals(NetworkTransport.LITENETLIB);
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
    }
}
