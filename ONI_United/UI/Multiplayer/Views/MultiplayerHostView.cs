using System;
using ONI_United.DebugTools;
using ONI_United.Misc;
using ONI_United.Networking;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Sessions.States;
using Shared.Helpers;
using Shared.UI;
using static ONI_United.STRINGS.UI.MP_SCREEN;
using static ONI_United.STRINGS.UI.MP_SCREEN.MAINMENU;
using ONI_United.StateMachines;

namespace ONI_United.UI.Multiplayer.Views
{
    internal sealed class MultiplayerHostView : IDisposable
    {
        private readonly MultiplayerScreen screen;
        private TabMenu view;
        private MultiplayerSection playerSettings;
        private readonly HostPlayerSettingsSection players;
        private readonly SteamHostSettingsSection steamSettings;
        private readonly LanHostSettingsSection lanSettings;
        private KButton startButton;
        private readonly int lanIndex;
        private bool disposed;

        internal KTabMenu Content => view;
        private bool IsLan => view.PreviousActiveTab == lanIndex;

        internal MultiplayerHostView(MultiplayerScreen owner, MultiplayerDialogScreen dialog)
        {
            screen = owner;
            var host = Configuration.Instance.Host;
            dialog.AddUI(ModAssets.UiTemplates.SectionPrefab, out playerSettings, activate: false);
            players = new HostPlayerSettingsSection(playerSettings, host.MaxLobbySize);

            dialog.AddUI(ModAssets.ModUI.TabMenu, out view, activate: false);
            view.Initialize(ModAssets.UiTemplates.primaryBtnPrefab);
            var contentPrefab = view.GetContentPrefab();
            var steam = Util.KInstantiateUI<KScreen>(contentPrefab.gameObject, view.body.gameObject, false);
            var lan = Util.KInstantiateUI<KScreen>(contentPrefab.gameObject, view.body.gameObject, false);
            steam.AddUI(ModAssets.UiTemplates.SectionPrefab, out var steamSection);
            steamSettings = new SteamHostSettingsSection(steamSection, host.Lobby);
            lan.AddUI(ModAssets.UiTemplates.SectionPrefab, out var lanSection);
            lanSettings = new LanHostSettingsSection(lanSection, host.LanSettings);

            dialog.AddOption(HOSTMENU.BUTTONS.STARTHOSTING.TEXT, _ => StartLanHosting(),
                out startButton, rightSide: false, accent: true);
            int steamIndex = view.AddTab(JOINVIABUTTONS.STEAM.TEXT, steam);
            lanIndex = view.AddTab(JOINVIABUTTONS.LAN.TEXT, lan);
            view.onTabActivated += OnTabActivated;
            view.ActivateTab(steamIndex);
            Show(false);
        }

        private bool CanStart => !disposed && IsLan && Utils.IsInGame() && Grid.CellCount > 0
            && !Session.IsActive && GameClient.State == ClientState.Disconnected
            && Lifecycle.Current == Lifecycle.Phase.Idle;

        internal void RefreshAvailability() => startButton.isInteractable = CanStart;

        private void StartLanHosting()
        {
            if (!CanStart) return;
            if (!int.TryParse(lanSettings.Port.Trim(), out int port) || port < 1 || port > 65535)
            { return; }
            if (!int.TryParse(players.PlayerLimit.Trim(), out int maxPlayers)
                || maxPlayers < NetworkConfig.LOBBY_SIZE_MIN || maxPlayers > NetworkConfig.LOBBY_SIZE_MAX)
            { return; }
            string name = lanSettings.PlayerName.Trim();
            // Match the existing LAN form name rules and local identity limit, only on Start.
            if (name.Length == 0 || name.Length > Utils.MaxLocalPlayerNameLength) return;
            foreach (char character in name)
                if (char.IsControl(character)) return;

            try
            {
                NetworkConfig.UpdateTransport(NetworkConfig.NetworkTransport.LITENETLIB);
                if (!NetworkConfig.TransportServer.Start(configure: () =>
                {
                    var config = Configuration.Instance;
                    config.Host.LanSettings.Port = port;
                    config.Host.LanSettings.DisplayName = name;
                    config.Host.MaxLobbySize = maxPlayers;
                    config.Host.NetworkTransport = (int)NetworkConfig.NetworkTransport.LITENETLIB;
                    config.Save();
                })) ModLog.Warning("Host start is not permitted in the current lifecycle state.");
                if (!disposed) screen.RefreshActiveView();
            }
            finally { if (!disposed && view != null && view.isActiveAndEnabled) RefreshAvailability(); }
        }

        private void StopEditing()
        {
            players.StopEditing();
            steamSettings.StopEditing();
            lanSettings.StopEditing();
        }

        internal void Show(bool show)
        {
            if (!show) StopEditing();
            playerSettings.gameObject.SetActive(show);
            view.Show(show);
            if (show && view.PreviousActiveTab >= 0) view.ActivateTab(view.PreviousActiveTab);
            startButton.gameObject.SetActive(show);
        }

        private void OnTabActivated(int current, int previous)
        {
            if (current == previous) return;
            StopEditing();
            RefreshAvailability();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            view.onTabActivated -= OnTabActivated;
            players.Release();
            steamSettings.Release();
            lanSettings.Release();
            if (startButton != null) startButton.ClearOnClick();
        }
    }
}
