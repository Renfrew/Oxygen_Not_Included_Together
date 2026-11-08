using System;
using Shared.Profiling;
using Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.Helpers
{

	public static class UiUtil
	{
		public static void CreateConfirmDialogFrontend(string title = null, string text = null, string confirm_text = null, System.Action on_confirm = null, string cancel_text = null, System.Action on_cancel = null, string configurable_text = null, System.Action on_configurable_clicked = null, Sprite image_sprite = null, bool useScreenSpaceOverlay = false, GameObject parent = null)
		=> CreateConfirmDialog(title, text, confirm_text, on_confirm, cancel_text, on_cancel, configurable_text, on_configurable_clicked, image_sprite, true, useScreenSpaceOverlay, parent);
		public static ConfirmDialogScreen CreateConfirmDialog(string title = null, string text = null, string confirm_text = null, System.Action on_confirm = null, string cancel_text = null, System.Action on_cancel = null, string configurable_text = null, System.Action on_configurable_clicked = null, Sprite image_sprite = null, bool frontend = false, bool useScreenSpaceOverlay = false, GameObject parent = null)
		{
			using var _ = Profiler.Scope();

			if (parent == null)
				parent = frontend && !useScreenSpaceOverlay ? Global.Instance.globalCanvas : GameScreenManager.Instance.GetParent(GameScreenManager.UIRenderTarget.ScreenSpaceOverlay);
			var dialogue = ((ConfirmDialogScreen)KScreenManager.Instance.StartScreen(ScreenPrefabs.Instance.ConfirmDialogScreen.gameObject, parent));

			if (!frontend)
				dialogue.Subscribe(476357528, (_) => CameraController.Instance.DisableUserCameraControl = true);
			dialogue.PopupConfirmDialog(text, on_confirm, on_cancel, configurable_text, on_configurable_clicked, title, confirm_text, cancel_text, image_sprite);
			return dialogue;
		}

        // Existing bundles contain ordinary KScreen pages beneath their native tab menu.
        public static KScreen[] GetTabPages(this KTabMenu menu)
        {
            var pages = new System.Collections.Generic.List<KScreen>();
            foreach (var screen in menu.GetComponentsInChildren<KScreen>(true))
                if (screen.GetType() == typeof(KScreen) && screen.GetComponentInParent<KTabMenu>(true) == menu)
                    pages.Add(screen);
            return pages.ToArray();
        }

        public static void AddUI<T>(this KScreen page, T prefab, out T instance, bool activate = true) where T : MonoBehaviour
        {
            instance = Util.KInstantiateUI<T>(prefab.gameObject, page.gameObject, activate);
        }

		public static void CopyImage(Image destination, Image source)
		{
			destination.sprite = source.sprite;
			destination.type = source.type;
			destination.material = source.material;
			destination.color = source.color;
		}

		public static void ConfigureButtonLayout(KButton button, LayoutElement buttonPrefab)
        {
            ConfigureLayout(button, buttonPrefab);
        }

        public static LayoutElement ConfigureLayout(Component component, LayoutElement layoutPrefab)
        {
            var layout = component.GetComponent<LayoutElement>() ?? component.gameObject.AddComponent<LayoutElement>();

            layout.layoutPriority = layoutPrefab.layoutPriority;

            layout.minWidth = layoutPrefab.minWidth;
            layout.preferredWidth = layoutPrefab.preferredWidth;
            layout.flexibleWidth = layoutPrefab.flexibleWidth;
            
            layout.minHeight = layoutPrefab.minHeight;
            layout.preferredHeight = layoutPrefab.preferredHeight;
            layout.flexibleHeight = layoutPrefab.flexibleHeight;
            return layout;
        }

		/// <summary>Configures an existing label without changing its hierarchy or active state.</summary>
		public static TMP_Text ConfigureText(TMP_Text text, string caption)
		{
			if (text is LocText localized) localized.key = string.Empty;
			text.richText = false;
			text.raycastTarget = false;
			text.textWrappingMode = TextWrappingModes.NoWrap;
			text.overflowMode = TextOverflowModes.Ellipsis;
			text.SetText(caption);
			return text;
		}

		public static TMP_Text CreateText(TMP_Text prefab, GameObject parent, string text)
		{
			return CreateText(prefab.gameObject, parent, text);
		}

		public static TMP_Text CreateText(GameObject prefab, GameObject parent, string text)
		{
			var tmpText = Util.KInstantiateUI<TMP_Text>(prefab, parent, false);
			ConfigureText(tmpText, text);
			tmpText.gameObject.SetActive(true);
			return tmpText;
		}

		/// <summary>Replaces clicks and captions without changing layout, hierarchy or active state.</summary>
		public static KButton ConfigureButton(KButton button, string text, System.Action? onClick = null)
		{
			button.ClearOnClick();

			foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
			{
				if (label is LocText localized) localized.key = string.Empty;
				label.SetText(text);
			}

			if (onClick != null)
				button.onClick += onClick;

			return button;
		}

		public static KButton CreateButtonPrefab(GameObject donor, GameObject parent, string name, UiTemplateConfig configuration, GameObject selectedStyleDonor)
		{
			var button = Util.KInstantiateUI<KButton>(donor, parent, false);
			button.gameObject.SetActive(false);
			button.gameObject.name = name;
			ConfigureLayout(button, configuration.SectionActionButton);
			ConfigureButton(button, string.Empty);
			Util.KInstantiateUI(configuration.ButtonTextPrefab.gameObject, button.gameObject, false);
			var fill = Util.KInstantiateUI<KImage>(configuration.ButtonFillPrefab.gameObject, button.bgImage.gameObject, false);
			fill.colorStyleSetting = button.bgImage.colorStyleSetting;
			button.additionalKImages = new[] { fill };
			button.gameObject.AddComponent<MultiplayerButtonState>().Initialize(fill,
				selectedStyleDonor.GetComponent<KButton>().bgImage.colorStyleSetting);
			return button;
		}

		public static KButton CreateButton(KButton prefab, GameObject parent, string text, System.Action? onClick = null)
		{
			return CreateButton(prefab.gameObject, parent, text, onClick);
		}

		public static KButton CreateButton(GameObject prefab, GameObject parent, string text, System.Action? onClick = null)
		{
			var button = Util.KInstantiateUI<KButton>(prefab, parent, false);
			ConfigureButton(button, text, onClick);
			button.gameObject.SetActive(true);
			return button;
		}

		/// <summary>Resets input rules and events without changing layout, hierarchy or active state.</summary>
		public static KInputTextField ConfigureInputField(
			KInputTextField input,
			string text = "",
			string placeholder = "",
			TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard,
			int characterLimit = 0)
		{
			input.onValueChanged = new TMP_InputField.OnChangeEvent();
			input.onEndEdit = new TMP_InputField.SubmitEvent();
			input.onValidateInput = null;

			input.contentType = contentType;
			input.characterLimit = characterLimit;

			if (input.textComponent is LocText locText) locText.key = string.Empty;

			if (input.placeholder is TMP_Text hint)
			{
				if (hint is LocText loc) loc.key = string.Empty;
				hint.SetText(placeholder);
			}
			input.SetTextWithoutNotify(text ?? string.Empty);
			return input;
		}

		public static KInputTextField CreateInputField(
			KInputTextField prefab,
			GameObject parent,
			string text = "",
			string placeholder = "",
			TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard,
			int characterLimit = 0)
		{
			return CreateInputField(prefab.gameObject, parent, text, placeholder, contentType, characterLimit);
		}

		public static KInputTextField CreateInputField(
			GameObject prefab,
			GameObject parent,
			string text = "",
			string placeholder = "",
			TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard,
			int characterLimit = 0)
		{
			var input = Util.KInstantiateUI<KInputTextField>(prefab, parent, false);
			ConfigureInputField(input, text, placeholder, contentType, characterLimit);
			input.gameObject.SetActive(true);
			return input;
		}

		public static void StopEditing(params KInputTextField[] inputs)
        {
            foreach (var input in inputs)
            {
                if (input == null) continue;
                var events = UnityEngine.EventSystems.EventSystem.current;
                var selected = events?.currentSelectedGameObject;
                input.DeactivateInputField();
                if (selected != null && (selected == input.gameObject || selected.transform.IsChildOf(input.transform)))
                    events?.SetSelectedGameObject(null);
            }
        }
	}
}
