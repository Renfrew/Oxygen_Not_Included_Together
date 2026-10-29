using System;
using ONI_United.Misc;
using ONI_United.DebugTools;
using ONI_United.Networking;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Sessions.Lobbies;
using ONI_United.Networking.Sessions.States;
using ONI_United.Networking.Transport.Steamworks;
using Shared.Helpers;
using Shared.UI;
using static ONI_United.STRINGS.UI.MP_SCREEN.MAINMENU;

namespace ONI_United.UI.Multiplayer.Views
{
    internal sealed class MultiplayerJoinView : IDisposable
    {
        private readonly MultiplayerScreen screen;
        private TabMenu view;

        private LobbySection steamLobby;
        private SteamJoinSection steamJoin;
        private SteamSettingsSection steamSettings;

        private LobbySection lanLobby;
        private LanJoinSection lanJoin;

        private KButton hostButton;

        private static bool refreshingLobbies;
        private bool disposed;
        private string lobbyFeedback;
        // SteamLobby.OnLobbyEntered currently ignores entry failure results. Keep requests gated
        // until backend result handling and end-to-end joining have been verified.
        private static bool SteamJoiningVerified => false;
        internal KTabMenu Content => view;
        internal MultiplayerJoinView(MultiplayerScreen owner, MultiplayerDialogScreen dialog)
        {
            screen = owner;

            dialog.AddUI(ModAssets.ModUI.TabMenu, out view, activate: false);
            view.Initialize(ModAssets.UiTemplates.primaryBtnPrefab);
            var contentPrefab = view.GetContentPrefab();
            var steam = Util.KInstantiateUI<KScreen>(contentPrefab.gameObject, view.body.gameObject, false);
            var lan = Util.KInstantiateUI<KScreen>(contentPrefab.gameObject, view.body.gameObject, false);

            steam.AddUI(ModAssets.UiTemplates.SectionPrefab, out var steamLobbySection);
            steamLobby = new LobbySection(steamLobbySection, RefreshLobbies);

            steam.AddUI(ModAssets.UiTemplates.SectionPrefab, out var steamJoinSection);
            steamJoin = new SteamJoinSection(steamJoinSection, IsValidLobbyCode, JoinByCode, OnSteamEdited);

            steam.AddUI(ModAssets.UiTemplates.SectionPrefab, out var steamSettingsSection);
            steamSettings = new SteamSettingsSection(steamSettingsSection, OpenFriends);

            lan.AddUI(ModAssets.UiTemplates.SectionPrefab, out var lanLobbySection);
            lanLobby = new LobbySection(lanLobbySection, RefreshLobbies);

            lan.AddUI(ModAssets.UiTemplates.SectionPrefab, out var lanJoinSection);
            lan.AddUI(ModAssets.UiTemplates.SectionPrefab, out var lanSettingsSection);
            var lanDefaults = Configuration.Instance.Client.LanSettings;
            new LanSettingsSection(lanSettingsSection, lanDefaults, out var playerName);
            lanJoin = new LanJoinSection(lanJoinSection, lanDefaults, playerName, Connect, OnLanEdited);

            dialog.AddOption(HOSTGAMEBUTTON.TEXT, _ => owner.OpenHostSetup(), out hostButton, accent: false);
            hostButton.gameObject.SetActive(true);

            view.AddTab(JOINVIABUTTONS.STEAM.TEXT, steam);
            view.AddTab(JOINVIABUTTONS.LAN.TEXT, lan);
            view.onTabActivated += OnTabActivated;
            view.ActivateTab(0);
            Show(false);
        }

        private void OpenFriends()
        {
            if (global::SteamManager.Initialized) Steamworks.SteamFriends.ActivateGameOverlay("friends");
        }
        private void RefreshLobbies()
        {
            if (refreshingLobbies || !global::SteamManager.Initialized) return;
            refreshingLobbies = true; lobbyFeedback = null; RefreshStatus();
            try
            {
                SteamLobby.RequestLobbyList(lobbies =>
                {
                    refreshingLobbies = false;
                    if (disposed || view == null) return;
                    if (lobbies == null) lobbyFeedback = JOINSECTIONS.LOBBY_EMPTY;
                    else
                    {
                        lobbyFeedback = string.Format(JOINSECTIONS.LOBBY_RESULTS, lobbies.Count);
                    }
                    if (view.isActiveAndEnabled) RefreshStatus();
                });
            }
            catch (Exception ex)
            { refreshingLobbies = false; lobbyFeedback = string.Format(JOINSECTIONS.LOBBY_ERROR, ex.Message); RefreshStatus(); }
        }
        private static bool IsValidLobbyCode(string code)
        {
            string clean = LobbyCodeHelper.CleanCode(code);
            return LobbyCodeHelper.IsValidCodeFormat(clean) && LobbyCodeHelper.TryParseCode(clean, out _);
        }
        private void JoinByCode(string code)
        {
            if (!SteamJoiningVerified)
            { return; }
            if (!CanJoinFromFrontend() || !global::SteamManager.Initialized)
            { return; }
            try
            {
                NetworkConfig.UpdateTransport(NetworkConfig.NetworkTransport.STEAMWORKS);
                SteamLobby.JoinLobbyByCode(code, onJoined: lobby =>
                {
                    if (disposed) return;
                    screen.RefreshActiveView();
                }, onError: message =>
                { if (!disposed) { ModLog.Warning(message); RefreshStatus(); } });
                RefreshStatus();
            }
            catch (Exception ex) { ModLog.Warning(string.Format(JOINSECTIONS.STEAM_JOIN_ERROR, ex.Message)); RefreshStatus(); }
        }
        private static bool CanJoinFromFrontend() => !Utils.IsInGame() && Grid.CellCount == 0
            && !Session.IsActive && GameClient.State == ClientState.Disconnected
            && GameServer.State == ServerState.Stopped && !SteamLobby.InLobby;
        private void Connect()
        {
            string address = lanJoin.Address.Trim();
            string name = lanJoin.PlayerName.Trim();
            if (Utils.IsInGame() || Grid.CellCount > 0)
            { return; }
            if (Session.IsActive || GameClient.State != ClientState.Disconnected || GameServer.State != ServerState.Stopped)
            { return; }
            if (!MultiplayerJoinValidation.TryValidate(address, lanJoin.Port, name, Utils.MaxLocalPlayerNameLength, out _))
            { return; }
            int port = int.Parse(lanJoin.Port);
            try
            {
                var config = Configuration.Instance;
                config.Client.LanSettings.DisplayName = name;
                config.Client.LanSettings.Ip = address;
                config.Client.LanSettings.Port = port;
                config.Save();
                NetworkConfig.UpdateLanTransport();
                GameClient.ConnectToHost(ip: address, port: port);
                screen.RefreshActiveView();
            }
            catch (Exception ex) { ModLog.Warning(string.Format(JOINSECTIONS.CONNECTION_ERROR, ex.Message)); RefreshStatus(); }
        }
        internal void RefreshStatus()
        {
            bool idle = CanJoinFromFrontend();
            lanJoin.SetAvailability(idle);
            bool steamReady = global::SteamManager.Initialized;
            string publicLobbyStatus = !steamReady ? (string)JOINSECTIONS.STEAM_UNAVAILABLE
                : refreshingLobbies ? (string)JOINSECTIONS.SEARCHING
                : lobbyFeedback ?? (string)JOINSECTIONS.LOBBY_PLACEHOLDER;
            bool canRefresh = steamReady && !refreshingLobbies;
            steamLobby.SetState(canRefresh, publicLobbyStatus);
            lanLobby.SetState(canRefresh, publicLobbyStatus);
            steamSettings.SetAvailability(steamReady);
            steamJoin.SetAvailability(SteamJoiningVerified && idle && steamReady);
            hostButton.isInteractable = !Session.IsActive
                && GameClient.State == ClientState.Disconnected && GameServer.State == ServerState.Stopped;
        }
        internal void StopEditing() { lanJoin.StopEditing(); steamJoin.StopEditing(); }
        internal void Show(bool show)
        {
            if (!show) StopEditing();
            view.Show(show);
            if (show && view.PreviousActiveTab >= 0) view.ActivateTab(view.PreviousActiveTab);
            hostButton.gameObject.SetActive(show);
        }
        private void OnTabActivated(int current, int previous)
        {
            if (current == previous) return;
            StopEditing();
            RefreshStatus();
        }
        private void OnSteamEdited() { if (!disposed) RefreshStatus(); }
        private void OnLanEdited()
        {
            if (disposed) return;
            RefreshStatus();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            view.onTabActivated -= OnTabActivated;
            lanJoin.Release();
            steamJoin.Release();
            steamLobby.Release();
            lanLobby.Release();
            steamSettings.Release();
            if (hostButton != null) hostButton.ClearOnClick();
        }
    }
}
