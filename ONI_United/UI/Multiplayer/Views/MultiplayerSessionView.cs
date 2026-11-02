using ONI_United.DebugTools;
using System;
using System.Collections.Generic;
using ONI_United.Networking;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Sessions.States;
using ONI_United.Networking.Transport.Steamworks;
using Shared.UI;
using ONI_United.Core;

namespace ONI_United.UI.Multiplayer.Views
{
    internal sealed class MultiplayerSessionView
    {
        private readonly MultiplayerScreen screen;
        private MultiplayerSessionContent view;
        private readonly List<string> players = new();
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
            if (Lifecycle.Current == Lifecycle.Phase.StartingHost || Session.IsHost)
            {
                status = NetworkRuntime.TransportServer.OperationError ?? (Lifecycle.IsHostGameplay
                    ? "Hosting Game" : Lifecycle.Current == Lifecycle.Phase.ClearingSession ? "Stopping host" : "Starting host");
                role = "Role: host";
            }
            else if (Session.IsActive)
            { status = Session.IsHost ? "Hosting Game" : "Connected to a multiplayer session"; role = $"Role: {(Session.IsHost ? "Host" : "Client")}"; }
            else if (Lifecycle.Current != Lifecycle.Phase.Idle)
            { status = $"Connection in progress: {Lifecycle.Current}"; role = "Role: awaiting session admission"; }
            else if (SteamLobby.InLobby)
            { status = "In a Steam lobby; gameplay connection is not confirmed."; role = "Role: lobby member"; }
            else { status = "No active multiplayer session."; role = "Role: none"; }
            view.SetState(status, role, $"Transport: {(NetworkRuntime.IsSteam ? "STEAMWORKS" : "LITENETLIB")}",
                Lifecycle.Current == Lifecycle.Phase.StartingHost || Session.IsHost,
                Lifecycle.Current is Lifecycle.Phase.Joining or Lifecycle.Phase.PreparingSession
                    or Lifecycle.Phase.Joined or Lifecycle.Phase.Synchronizing || Session.IsClient);
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
            if (!(Lifecycle.Current is Lifecycle.Phase.Joining or Lifecycle.Phase.PreparingSession
                or Lifecycle.Phase.Joined or Lifecycle.Phase.Synchronizing) && !Session.IsClient) return;
            GameClient.CancelConnectionAttempt(); screen.RefreshActiveView();
        }
        private void StopHosting()
        {
            NetworkRuntime.Stop();
            screen.RefreshActiveView();
        }
    }
}
