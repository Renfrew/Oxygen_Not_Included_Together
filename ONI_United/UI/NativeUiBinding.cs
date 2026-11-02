using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ONI_United.UI
{
    internal static class NativeUiBinding
    {
        internal static void SetText(TMP_Text text, string value)
        {
            if (text == null)
                throw new InvalidOperationException("Native text component is missing.");
            if (text is LocText localized)
                localized.key = string.Empty;
            text.SetText(value);
        }

        internal static void SetCaption(GameObject control, string value, bool clearSecondary = false)
        {
            var labels = control.GetComponentsInChildren<LocText>(true);
            if (labels.Length == 0)
                throw new InvalidOperationException("Native control lacks a LocText caption: " + control.name);
            SetText(labels[0], value);
            if (clearSecondary)
                for (int i = 1; i < labels.Length; i++)
                    SetText(labels[i], string.Empty);
        }

        internal static void BindButton(KButton button, System.Action callback, ICollection<System.Action> cleanup)
        {
            if (button == null)
                throw new InvalidOperationException("Native KButton is missing.");
            button.ClearOnClick();
            button.onClick += callback;
            cleanup.Add(() => { if (button != null) button.onClick -= callback; });
        }

        internal static void StopEditing(params KInputTextField[] inputs)
        {
            foreach (var input in inputs)
            {
                if (input == null) continue;
                var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                var selected = eventSystem?.currentSelectedGameObject;
                input.DeactivateInputField();
                if (eventSystem != null && selected != null
                    && (selected == input.gameObject || selected.transform.IsChildOf(input.transform)))
                    eventSystem.SetSelectedGameObject(null);
            }
        }

        internal static void BindInput(KInputTextField input, string value,
            TMP_InputField.ContentType contentType, int characterLimit)
        {
            input.onValueChanged = new TMP_InputField.OnChangeEvent();
            input.onEndEdit = new TMP_InputField.SubmitEvent();
            input.onValidateInput = null;
            input.contentType = contentType;
            input.characterLimit = characterLimit;
            if (input.textComponent is LocText text)
                text.key = string.Empty;
            input.SetTextWithoutNotify(value ?? string.Empty);
        }
    }
}
