using System;
using Shared.Helpers;
using Shared.UI;
using TMPro;
using static ONI_United.STRINGS.UI.MP_SCREEN.MAINMENU;

namespace ONI_United.UI.Multiplayer.Views
{
    // Both transport tabs use the same public-lobby presentation and data.
    internal sealed class LobbySection
    {
        private readonly KButton refresh;
        private readonly TMP_Text status;
        internal LobbySection(MultiplayerSection section, System.Action onRefresh)
        {
            section.SetHeader(JOINSECTIONS.PUBLIC_LOBBIES);
            UiUtil.ConfigureLayout(section, ModAssets.ModUI.Config.LobbySection);
            section.AddOption(JOINSECTIONS.REFRESH, _ => onRefresh(), out refresh);
            section.AddRow(out var row);
            row.AddUI(ModAssets.UiTemplates.PlainTextPrefab, out status);
            UiUtil.ConfigureText(status, JOINSECTIONS.LOBBY_PLACEHOLDER);
        }
        internal void SetState(bool canRefresh, string message)
        {
            refresh.isInteractable = canRefresh;
            status.SetText(message);
        }
        internal void Release() { if (refresh != null) refresh.ClearOnClick(); }
    }
}
