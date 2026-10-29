using System;
using System.Collections.Generic;
using ONI_United.Networking;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Sessions.States;
using ONI_United.Networking.Transport.Steamworks;
using Shared.UI;

namespace ONI_United.UI.Multiplayer.Views
{
    internal sealed class MultiplayerSessionView
    {
        private readonly MultiplayerScreen screen;
        private MultiplayerSessionContent view;
        private readonly List<string> players = new();
        private string actionError;
        private bool stopping;
        internal MultiplayerSessionContent Content => view;
        internal MultiplayerSessionView(MultiplayerScreen owner, MultiplayerDialogScreen dialog)
        {
            screen = owner;
            dialog.AddUI(ModAssets.ModUI.SessionView.GetComponent<MultiplayerSessionContent>(), out view);
            view.Initialize(ModAssets.UiTemplates, dialog, StopHosting, CancelConnection);
            view.Show(false);
        }
        internal void Refresh()
        {
            string status, role;
            if (Session.IsActive)
            { status = Session.IsHost ? "Hosting Game" : "Connected to a multiplayer session"; role = $"Role: {(Session.IsHost ? "Host" : "Client")}"; }
            else if (GameClient.State != ClientState.Disconnected)
            { status = $"Connection in progress: {GameClient.State}"; role = "Role: awaiting session admission"; }
            else if (GameServer.State != ServerState.Stopped)
            { status = $"Server state: {GameServer.State}"; role = "Role: host startup/shutdown"; }
            else if (SteamLobby.InLobby)
            { status = "In a Steam lobby; gameplay connection is not confirmed."; role = "Role: lobby member"; }
            else { status = "No active multiplayer session."; role = "Role: none"; }
            view.SetState(actionError ?? status, role, $"Transport: {NetworkConfig.transport}",
                NetworkConfig.IsLanConfig() && (Session.IsHost || GameServer.State != ServerState.Stopped),
                GameClient.State == ClientState.Connecting, stopping);
            players.Clear();
            foreach (var pair in Session.Players)
            {
                string name = string.IsNullOrWhiteSpace(pair.Value.UserName) ? $"Player {pair.Key}" : pair.Value.UserName;
                if (pair.Key == Session.LocalPlayerId) name += " (You)";
                if (pair.Key == Session.HostPlayerId) name += " (Host)";
                players.Add(name);
            }
            view.SetPlayers(players);
        }
        private void CancelConnection()
        {
            if (GameClient.State != ClientState.Connecting) return;
            actionError = null; GameClient.CancelConnectionAttempt(); screen.RefreshActiveView();
        }
        private void StopHosting()
        {
            if (stopping) return;
            stopping = true; actionError = null; Refresh();
            try
            {
                if (NetworkConfig.StopLanHost() && !Session.IsActive && GameServer.State == ServerState.Stopped)
                { screen.RefreshActiveView(); return; }
                actionError = "Hosting could not be fully stopped. Check the listener and session state.";
            }
            catch (Exception ex) { actionError = $"Stop Hosting failed: {ex.Message}"; }
            finally { stopping = false; if (view != null && view.isActiveAndEnabled) Refresh(); }
        }
    }
}
