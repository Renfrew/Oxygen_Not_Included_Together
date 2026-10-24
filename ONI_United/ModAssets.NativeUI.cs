using System;
using System.Reflection;
using ONI_United.DebugTools;
using UnityEngine;
#if DEBUG
using TMPro;
using UnityEngine.UI;
#endif

namespace ONI_United
{
    public static partial class ModAssets
    {
        internal static NativeUiAssets UI { get; private set; }

        internal static void InitializeNativeUI(ScreenPrefabs owner)
        {
            UI = null;
            try
            {
                UI = new NativeUiAssets(owner);
                DebugConsole.Log("[ModAssets] Native UI catalog initialized.");
            }
            catch (Exception error)
            {
                DebugConsole.LogError("[ModAssets] Native UI initialization failed: " + error);
            }
        }

        internal static void ReleaseNativeUI(ScreenPrefabs owner)
        {
            if (UI != null && ReferenceEquals(UI.Owner, owner))
                UI = null;
        }

        internal sealed class NativeUiAssets
        {
            internal ScreenPrefabs Owner { get; }
            internal GameObject PrimaryButton { get; }
            internal GameObject SecondaryButton { get; }
            internal GameObject PlainText { get; }
            internal GameObject SubHeader { get; }
            internal KInputTextField Input { get; }
            internal GameObject WindowShell { get; }
            internal GameObject SessionScroll { get; }
            internal GameObject CollapsiblePanel { get; }
            internal Transform Footer { get; }
            internal Transform FooterLeft { get; }
            internal Transform FooterRight { get; }

            internal NativeUiAssets(ScreenPrefabs owner)
            {
                Owner = Required(owner, "ScreenPrefabs");
                var dialog = Required(owner.InfoDialogScreen, "InfoDialog");
                PrimaryButton = Required(dialog.GetPrimaryButtonPrefab(), "primary button");
                SecondaryButton = Required(dialog.GetSecondaryButtonPrefab(), "secondary button");
                PlainText = Required(dialog.GetPlainTextPrefab()?.gameObject, "plain text");
                SubHeader = Required(dialog.GetSubHeaderPrefab()?.gameObject, "subheader");

                // Join/Session reuse this footer; Host has its own prepared footer.
                FooterLeft = DialogPanel(dialog, "leftButtonPanel");
                FooterRight = DialogPanel(dialog, "rightButtonPanel");
                var footer = FooterLeft;
                while (footer != null && FooterRight != null && !FooterRight.IsChildOf(footer))
                    footer = footer.parent;
                if (FooterRight != null && footer != null && footer != dialog.transform)
                    Footer = footer;
                else
                    DebugConsole.LogWarning("[ModAssets] Native InfoDialog footer is unavailable.");

                var locker = owner.LockerNavigator?.GetComponent<LockerNavigator>();
                var browser = locker?.outfitBrowserScreen?.GetComponent<OutfitBrowserScreen>();
                Input = Required(browser?.inputFieldPrefab, "Outfit Browser input");
                WindowShell = Required(
                    owner.OptionsScreen?.transform.Find("CenterContent/Content/BG")?.gameObject,
                    "Options window shell");
                CollapsiblePanel = owner.CollapsableContentPanel;
                var menu = DlcManager.IsExpansion1Active() ? owner.MainMenuForSpacedOut : owner.MainMenuForVanilla;
                var flow = menu?.GetComponent<NewGameFlow>();
                ColonyDestinationSelectScreen frontend = null;
                if (flow?.newGameFlowScreens != null)
                    foreach (var screen in flow.newGameFlowScreens)
                        if (screen is ColonyDestinationSelectScreen destination)
                        {
                            frontend = destination;
                            break;
                        }
                frontend ??= owner.ColonyDestinationSelectScreen;
                SessionScroll = frontend?.gameSettingsScrollPanel?.gameObject;
#if DEBUG
                ValidateTemplates();
#endif
            }

            private static T Required<T>(T asset, string name) where T : UnityEngine.Object => asset
                ?? throw new InvalidOperationException("Native UI template unavailable: " + name);

            private static Transform DialogPanel(InfoDialogScreen dialog, string name)
            {
                // No public getter exists for these serialized native footer fields.
                var field = typeof(InfoDialogScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                return (field?.GetValue(dialog) as GameObject)?.transform;
            }

#if DEBUG
            private void ValidateTemplates()
            {
                foreach (var template in new[] { PrimaryButton, SecondaryButton, PlainText, SubHeader, Input.gameObject, WindowShell })
                {
                    ValidateUIComponents(template);
                    if (template.GetComponentInChildren<KScreen>(true) != null
                        || template.GetComponentInChildren<CustomGameSettingWidget>(true) != null)
                        throw new InvalidOperationException("Native template contains a specialized controller: " + template.name);
                }
                foreach (var button in new[] { PrimaryButton, SecondaryButton })
                {
                    var layout = button.GetComponent<LayoutElement>();
                    if (button.GetComponent<KButton>() == null || button.GetComponentInChildren<LocText>(true) == null
                        || layout == null || layout.minWidth <= 0f || layout.minHeight <= 0f)
                        throw new InvalidOperationException("Incomplete native button template: " + button.name);
                }
                foreach (var text in new[] { PlainText, SubHeader })
                    if (text.GetComponentInChildren<LocText>(true) == null)
                        throw new InvalidOperationException("Native text template lacks LocText: " + text.name);
                if (Input.textComponent == null || Input.textViewport == null || Input.placeholder is not TMP_Text
                    || WindowShell.GetComponent<RectTransform>() == null)
                    throw new InvalidOperationException("Incomplete native input or window template.");
                if (Footer != null && (FooterLeft == FooterRight
                    || Footer.GetComponentInChildren<KScreen>(true) != null
                    || FooterLeft.GetComponent<LayoutGroup>() == null || FooterRight.GetComponent<LayoutGroup>() == null
                    || !FooterLeft.IsChildOf(Footer)))
                    DebugConsole.LogWarning("[ModAssets] Unsupported native InfoDialog footer hierarchy.");
            }
#endif
        }
    }
}
