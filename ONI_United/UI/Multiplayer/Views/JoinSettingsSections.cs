using System;
using ONI_United.Misc;
using Shared.Helpers;
using Shared.UI;
using static ONI_United.STRINGS.UI.MP_SCREEN.MAINMENU;

namespace ONI_United.UI.Multiplayer.Views
{
    internal sealed class SteamSettingsSection
    {
        private readonly KButton friends;
        internal SteamSettingsSection(MultiplayerSection section, System.Action openFriends)
        {
            section.SetHeader(JOINSECTIONS.STEAM_SETTINGS);
            section.AddRow(out var row);
            row.AddUI(ModAssets.UiTemplates.primaryBtnPrefab, out friends, useLeft: false);
            UiUtil.ConfigureButton(friends, JOINSECTIONS.OPEN_FRIENDS, openFriends);
            row.AddUI(ModAssets.UiTemplates.primaryBtnPrefab, out var configure, useLeft: false);
            UiUtil.ConfigureButton(configure, JOINSECTIONS.CONFIGURE);
            configure.isInteractable = false;
        }
        internal void SetAvailability(bool steamReady) => friends.isInteractable = steamReady;
        internal void Release() { if (friends != null) friends.ClearOnClick(); }
    }

    internal sealed class LanSettingsSection
    {
        internal LanSettingsSection(MultiplayerSection section, LanSettings defaults, out KInputTextField playerName)
        {
            section.SetHeader(JOINSECTIONS.LAN_SETTINGS);
            section.AddRow(out var row);
            row.AddUI(ModAssets.UiTemplates.PlainTextPrefab, out var label);
            UiUtil.ConfigureText(label, LANJOIN.PLAYER_NAME);
            row.AddUI(ModAssets.UiTemplates.InputFieldPrefab, out playerName);
            string initialName = string.IsNullOrWhiteSpace(defaults.DisplayName)
                ? Utils.TruncateLocalPlayerName(string.Format(LANJOIN.DEFAULT_PLAYER_NAME, Configuration.Instance.Network.PersistentUserId))
                : defaults.DisplayName;
            UiUtil.ConfigureInputField(playerName, text: initialName, placeholder: LANJOIN.PLAYER_NAME,
                characterLimit: Utils.MaxLocalPlayerNameLength);
            row.AddUI(ModAssets.UiTemplates.primaryBtnPrefab, out var configure, useLeft: false);
            UiUtil.ConfigureButton(configure, JOINSECTIONS.CONFIGURE);
            configure.isInteractable = false;
        }
    }
}
