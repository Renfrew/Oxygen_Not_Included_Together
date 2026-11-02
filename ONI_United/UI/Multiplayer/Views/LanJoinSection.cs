using System.Globalization;
using ONI_United.Misc;
using Shared.Helpers;
using Shared.UI;
using TMPro;
using UnityEngine.Events;
using static ONI_United.STRINGS.UI.MP_SCREEN.MAINMENU;

namespace ONI_United.UI.Multiplayer.Views
{
    // Owns LAN form composition and validation, never the containing Unity objects.
    internal sealed class LanJoinSection
    {
        private readonly KInputTextField address, port, playerName;
        private readonly KButton joinButton;
        private readonly System.Action onJoin, onChanged;
        private readonly UnityAction<string> inputChanged;
        private bool canJoin;

        internal string Address => address.text;
        internal string Port => port.text;
        internal string PlayerName => playerName.text;

        internal LanJoinSection(MultiplayerSection section, LanSettings defaults, KInputTextField playerName, System.Action onJoin, System.Action onChanged)
        {
            this.playerName = playerName;
            this.onJoin = onJoin;
            this.onChanged = onChanged;

            var templates = ModAssets.UiTemplates;
            section.SetHeader(LANJOIN.JOINLANBUTTON.TEXT);

            section.AddRow(out var row);
            row.AddUI(templates.InputFieldPrefab, out address);
            UiUtil.ConfigureInputField(address, text: defaults.Ip,
                placeholder: LANJOIN.INPUTS.IPINPUT.TEXTAREA.PLACEHOLDER, characterLimit: 253);
            row.AddUI(templates.InputFieldPrefab, out port, useLeft: false);
            UiUtil.ConfigureLayout(port, ModAssets.ModUI.Config.CompactInputField);
            UiUtil.ConfigureInputField(port, text: defaults.Port.ToString(CultureInfo.InvariantCulture),
                placeholder: LANJOIN.INPUTS.PORT.TEXTAREA.PLACEHOLDER,
                contentType: TMP_InputField.ContentType.IntegerNumber, characterLimit: 5);
            row.AddUI(templates.primaryBtnPrefab, out joinButton, useLeft: false);
            UiUtil.ConfigureButton(joinButton, LANJOIN.JOINLANBUTTON.TEXT, RequestJoin);

            inputChanged = _ => OnEdited();
            address.onValueChanged.AddListener(inputChanged);
            port.onValueChanged.AddListener(inputChanged);
            playerName.onValueChanged.AddListener(inputChanged);
            foreach (var input in new[] { address, port, playerName })
                input.onEndEdit.AddListener(MultiplayerInput.EditingEnded);
            Refresh();
        }

        internal void SetAvailability(bool available)
        {
            canJoin = available;
            Refresh();
        }

        private bool TryValidate(out string error) =>
            MultiplayerJoinValidation.TryValidate(Address, Port, PlayerName, Utils.MaxLocalPlayerNameLength, out error);

        private void Refresh()
        {
            bool valid = TryValidate(out _);
            joinButton.isInteractable = canJoin && valid;
        }

        private void OnEdited()
        {
            Refresh();
            onChanged();
        }

        private void RequestJoin()
        {
            if (canJoin && TryValidate(out _)) onJoin();
            else Refresh();
        }

        internal void StopEditing() => UiUtil.StopEditing(address, port, playerName);

        // The UI owner destroys the objects. Release only this wrapper's input/action bindings.
        internal void Release()
        {
            StopEditing();
            if (address != null) address.onValueChanged.RemoveListener(inputChanged);
            if (port != null) port.onValueChanged.RemoveListener(inputChanged);
            if (playerName != null) playerName.onValueChanged.RemoveListener(inputChanged);
            foreach (var input in new[] { address, port, playerName })
                if (input != null) input.onEndEdit.RemoveListener(MultiplayerInput.EditingEnded);
            if (joinButton != null) joinButton.ClearOnClick();
        }
    }
}
