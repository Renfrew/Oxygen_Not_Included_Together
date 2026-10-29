using System;
using System.Collections;
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
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Sessions;
using GameServer = ONI_United.Networking.Lifecycle.GameServer;
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

        public static bool TryStartLanHostFromLoadedGame(out string failureReason)
        {
            failureReason = string.Empty;
            DebugConsole.Log("[NetworkConfig] In-game LAN host startup requested.");

            if (!Utils.IsInGame() || Grid.CellCount <= 0)
            {
                failureReason = "Load a colony before starting a LAN host.";
                DebugConsole.LogError("[NetworkConfig] " + failureReason);
                return false;
            }

            if (Session.IsActive
                || GameClient.State != ClientState.Disconnected
                || GameServer.State != ServerState.Stopped)
            {
                failureReason = "A multiplayer session or server is already active.";
                DebugConsole.LogError("[NetworkConfig] " + failureReason);
                return false;
            }

            int port = Configuration.Instance.Host.LanSettings.Port;
            if (port < 1 || port > 65535)
            {
                failureReason = "The configured LAN port is outside the valid range.";
                DebugConsole.LogError("[NetworkConfig] " + failureReason);
                return false;
            }

            UpdateTransport(NetworkTransport.LITENETLIB);
            if (TransportServer is not LiteNetLibServer server)
            {
                failureReason = "The LiteNetLib server transport is unavailable.";
                DebugConsole.LogError("[NetworkConfig] " + failureReason);
                return false;
            }

            DebugConsole.Log("[NetworkConfig] Starting LiteNetLib listener on configured port " + port + ".");
            try
            {
                if (!GameServer.Start())
                {
                    failureReason = server.LastStartFailure ?? "The LiteNetLib server failed to start.";
                    DebugConsole.LogError("[NetworkConfig] LAN host startup failed: " + failureReason);
                    StopFailedLanHost(server);
                    return false;
                }

                ulong localPlayerId = Session.LocalPlayerId;
                if (!server.IsRunning
                    || !Session.IsActive
                    || !Session.IsHost
                    || !Session.Players.ContainsKey(localPlayerId))
                {
                    failureReason = "The listener started, but the local host session is incomplete.";
                    DebugConsole.LogError("[NetworkConfig] " + failureReason);
                    StopFailedLanHost(server);
                    return false;
                }

                DebugConsole.Log("[NetworkConfig] LAN host is listening on port " + port + ".");
                return true;
            }
            catch (Exception ex)
            {
                failureReason = "The LiteNetLib server could not be initialized.";
                DebugConsole.LogError($"[NetworkConfig] LAN host startup failed: {ex}");
                StopFailedLanHost(server);
                return false;
            }
        }

        public static bool StopLanHost()
        {
            DebugConsole.Log("[NetworkConfig] Stop hosting requested.");
            if (!IsLanConfig() || TransportServer is not LiteNetLibServer server)
            {
                DebugConsole.LogError("[NetworkConfig] Cannot stop LAN hosting because the LiteNetLib transport is not selected.");
                return false;
            }

            try
            {
                GameServer.Shutdown();
                if (Session.IsActive)
                    MultiplayerSession.UnRegisterAsHost(null);

                SelectToolPatch.UpdateColor();
                bool stopped = !server.IsRunning && !Session.IsActive;
                if (stopped)
                    DebugConsole.Log("[NetworkConfig] LAN listener stopped and host session cleared.");
                else
                    DebugConsole.LogError("[NetworkConfig] LAN host shutdown did not clear both listener and session state.");
                return stopped;
            }
            catch (Exception ex)
            {
                DebugConsole.LogError($"[NetworkConfig] Error while stopping LAN host: {ex}");
                try
                {
                    server.Stop();
                }
                catch (Exception stopException)
                {
                    DebugConsole.LogError($"[NetworkConfig] LiteNetLib listener shutdown also failed: {stopException}");
                }

                if (Session.IsActive)
                    MultiplayerSession.UnRegisterAsHost(null);

                bool stopped = !server.IsRunning && !Session.IsActive;
                if (stopped)
                    DebugConsole.Log("[NetworkConfig] LAN listener stopped and host session cleared after a shutdown error.");
                return stopped;
            }
        }

        private static void StopFailedLanHost(LiteNetLibServer server)
        {
            try
            {
                if (server.IsRunning || Session.IsActive || GameServer.State != ServerState.Stopped)
                    GameServer.Shutdown();
            }
            catch (Exception cleanupException)
            {
                DebugConsole.LogError($"[NetworkConfig] Failed to clean up after LAN startup failure: {cleanupException}");
                try
                {
                    server.Stop();
                }
                catch (Exception stopException)
                {
                    DebugConsole.LogError($"[NetworkConfig] LiteNetLib listener cleanup also failed: {stopException}");
                }
            }

            try
            {
                if (Session.IsActive)
                    MultiplayerSession.UnRegisterAsHost(null);
            }
            catch (Exception sessionCleanupException)
            {
                DebugConsole.LogError($"[NetworkConfig] Failed to clear host session after LAN startup failure: {sessionCleanupException}");
            }
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
            newTransport = (NetworkTransport)TransportConfigurationMigration.NormalizeNetworkTransport((int)newTransport);

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
