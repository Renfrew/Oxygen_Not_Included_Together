using System;
using Shared.Helpers;
using Shared.UI;
using UnityEngine.Events;
using static ONI_United.STRINGS.UI.MP_SCREEN.MAINMENU;

namespace ONI_United.UI.Multiplayer.Views
{
    internal sealed class SteamJoinSection
    {
        private readonly KInputTextField code;
        private readonly KButton join;
        private readonly Func<string, bool> validate;
        private readonly System.Action<string> onJoin;
        private readonly UnityAction<string> edited;
        private bool available;

        internal SteamJoinSection(MultiplayerSection section, Func<string, bool> validate, System.Action<string> onJoin, System.Action onChanged)
        {
            this.validate = validate;
            this.onJoin = onJoin;
            section.SetHeader(LOBBYCODEJOIN.JOINWITHCODEBUTTON.TEXT);
            section.AddRow(out var row);
            row.AddUI(ModAssets.UiTemplates.InputFieldPrefab, out code);
            UiUtil.ConfigureInputField(code, placeholder: LOBBYCODEJOIN.INPUT.TEXTAREA.PLACEHOLDER, characterLimit: 32);
            row.AddUI(ModAssets.UiTemplates.primaryBtnPrefab, out join, useLeft: false);
            UiUtil.ConfigureButton(join, LOBBYCODEJOIN.JOINWITHCODEBUTTON.TEXT, RequestJoin);
            edited = _ => { Refresh(); onChanged(); };
            code.onValueChanged.AddListener(edited);
            code.onEndEdit.AddListener(MultiplayerInput.EditingEnded);
            Refresh();
        }
        internal void SetAvailability(bool canJoin)
        {
            available = canJoin;
            Refresh();
        }
        private void Refresh()
        {
            bool valid = validate(code.text);
            join.isInteractable = available && valid;
        }
        private void RequestJoin()
        {
            if (available && validate(code.text)) onJoin(code.text);
            else Refresh();
        }
        internal void StopEditing() => UiUtil.StopEditing(code);
        internal void Release()
        {
            StopEditing();
            if (code != null) { code.onValueChanged.RemoveListener(edited); code.onEndEdit.RemoveListener(MultiplayerInput.EditingEnded); }
            if (join != null) join.ClearOnClick();
        }
    }
}
