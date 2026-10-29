using System.Globalization;
using ONI_United.Misc;
using Shared.Helpers;
using Shared.UI;
using TMPro;
using static ONI_United.STRINGS.UI.MP_SCREEN;
using static ONI_United.STRINGS.UI.MP_SCREEN.MAINMENU;

namespace ONI_United.UI.Multiplayer.Views
{
    internal sealed class HostPlayerSettingsSection
    {
        private readonly KInputTextField playerLimit;
        internal string PlayerLimit => playerLimit.text;

        internal HostPlayerSettingsSection(MultiplayerSection section, int initialLimit)
        {
            section.SetHeader("HOST SETTINGS");
            section.AddRow(out var row);
            row.AddUI(ModAssets.UiTemplates.PlainTextPrefab, out var label);
            UiUtil.ConfigureText(label, "Maximum Players (1-4)");
            row.AddUI(ModAssets.UiTemplates.InputFieldPrefab, out playerLimit);
            UiUtil.ConfigureInputField(playerLimit, text: initialLimit.ToString(CultureInfo.InvariantCulture),
                placeholder: "Max Players", contentType: TMP_InputField.ContentType.IntegerNumber, characterLimit: 2);
            playerLimit.onEndEdit.AddListener(MultiplayerInput.EditingEnded);
        }

        internal void StopEditing() => UiUtil.StopEditing(playerLimit);
        internal void Release()
        {
            StopEditing();
            if (playerLimit != null) playerLimit.onEndEdit.RemoveListener(MultiplayerInput.EditingEnded);
        }
    }

    internal sealed class SteamHostSettingsSection
    {
        private readonly KInputTextField password;
        private readonly MultiToggle friendsOnly;

        internal SteamHostSettingsSection(MultiplayerSection section, LobbySettings defaults)
        {
            section.SetHeader(HOSTSECTIONS.SETTINGS);
            section.AddRow(out var passwordRow);
            passwordRow.AddUI(ModAssets.UiTemplates.PlainTextPrefab, out var passwordLabel);
            UiUtil.ConfigureText(passwordLabel, HOSTMENU.STEAMHOSTING.PASSWORD.PASSWORDTITLE);
            passwordRow.AddUI(ModAssets.UiTemplates.InputFieldPrefab, out password);
            UiUtil.ConfigureInputField(password, placeholder: HOSTMENU.STEAMHOSTING.PASSWORDINPUT.TEXTAREA.PLACEHOLDER,
                contentType: TMP_InputField.ContentType.Password);
            // Existing config stores a hash, not a recoverable password. Steam start remains unavailable.
            password.interactable = false;
            password.onEndEdit.AddListener(MultiplayerInput.EditingEnded);

            section.AddRow(out var visibilityRow);
            visibilityRow.AddUI(ModAssets.UI.TableWidgets.Checkbox.GetComponent<MultiToggle>(), out friendsOnly);
            friendsOnly.onClick = null;
            friendsOnly.ChangeState((int)(defaults.IsPrivate ? TableScreen.ResultValues.True : TableScreen.ResultValues.False));
            friendsOnly.enabled = false;
            visibilityRow.AddUI(ModAssets.UiTemplates.PlainTextPrefab, out var friendsLabel);
            UiUtil.ConfigureText(friendsLabel, HOSTMENU.STEAMHOSTING.FRIENDSONLY.STATE);
            visibilityRow.AddUI(ModAssets.UiTemplates.primaryBtnPrefab, out var settings, useLeft: false);
            UiUtil.ConfigureButton(settings, HOSTSECTIONS.SETTINGS);
            settings.isInteractable = false; // No supported Host settings action exists.
        }

        internal void StopEditing() => UiUtil.StopEditing(password);
        internal void Release()
        {
            StopEditing();
            if (password != null) password.onEndEdit.RemoveListener(MultiplayerInput.EditingEnded);
            if (friendsOnly != null) friendsOnly.onClick = null;
        }
    }

    internal sealed class LanHostSettingsSection
    {
        private readonly KInputTextField playerName, port;
        internal string PlayerName => playerName.text;
        internal string Port => port.text;

        internal LanHostSettingsSection(MultiplayerSection section, LanSettings defaults)
        {
            section.SetHeader(HOSTSECTIONS.SETTINGS);
            section.AddRow(out var nameRow);
            nameRow.AddUI(ModAssets.UiTemplates.PlainTextPrefab, out var nameLabel);
            UiUtil.ConfigureText(nameLabel, LANJOIN.PLAYER_NAME);
            nameRow.AddUI(ModAssets.UiTemplates.InputFieldPrefab, out playerName);
            string initialName = string.IsNullOrWhiteSpace(defaults.DisplayName)
                ? Utils.TruncateLocalPlayerName(string.Format(LANJOIN.DEFAULT_PLAYER_NAME, Configuration.Instance.Network.PersistentUserId))
                : defaults.DisplayName;
            UiUtil.ConfigureInputField(playerName, text: initialName, placeholder: LANJOIN.PLAYER_NAME,
                characterLimit: Utils.MaxLocalPlayerNameLength);

            section.AddRow(out var portRow);
            portRow.AddUI(ModAssets.UiTemplates.PlainTextPrefab, out var portLabel);
            UiUtil.ConfigureText(portLabel, HOSTMENU.LANHOSTING.PORT.LABEL);
            portRow.AddUI(ModAssets.UiTemplates.InputFieldPrefab, out port);
            UiUtil.ConfigureInputField(port, text: defaults.Port.ToString(CultureInfo.InvariantCulture),
                placeholder: "Port", contentType: TMP_InputField.ContentType.IntegerNumber, characterLimit: 5);
            portRow.AddUI(ModAssets.UiTemplates.primaryBtnPrefab, out var settings, useLeft: false);
            UiUtil.ConfigureButton(settings, HOSTSECTIONS.SETTINGS);
            settings.isInteractable = false;
            playerName.onEndEdit.AddListener(MultiplayerInput.EditingEnded);
            port.onEndEdit.AddListener(MultiplayerInput.EditingEnded);
        }

        internal void StopEditing() => UiUtil.StopEditing(playerName, port);
        internal void Release()
        {
            StopEditing();
            if (playerName != null) playerName.onEndEdit.RemoveListener(MultiplayerInput.EditingEnded);
            if (port != null) port.onEndEdit.RemoveListener(MultiplayerInput.EditingEnded);
        }
    }
}
