using System;
using ONI_United.DebugTools;
using UnityEngine;

namespace ONI_United
{
    public static partial class ModAssets
    {
        private static NativeUiAssets _nativeUiAssets = null;
        internal static NativeUiAssets UI => _nativeUiAssets ?? InitializeNativeUiAssets();

        private static NativeUiAssets InitializeNativeUiAssets()
        {
            if (ScreenPrefabs.Instance != null)
                _nativeUiAssets = new NativeUiAssets(ScreenPrefabs.Instance);

            return _nativeUiAssets;
        }

        internal sealed class NativeUiAssets
        {
            // Table templates remain owned by vanilla Assets.UIPrefabs.
            internal Assets.TableScreenAssets TableWidgets => Assets.UIPrefabs.TableScreenWidgets;
            internal GameObject PrimaryButton { get; }
            internal GameObject SecondaryButton { get; }
            internal GameObject PlainText { get; }
            internal GameObject SubHeader { get; }
            internal GameObject OptionsButton { get; }
            internal GameObject MainMenuButton { get; }
            internal GameObject LineItem { get; }
            internal GameObject CollapsiblePanel { get; }
            internal MultiToggle[] MenuTabs { get; }
            internal KInputTextField SeedInput { get; }
            internal KInputTextField FileNameInput { get; }
            internal KInputTextField Input { get; }
            internal GameObject WindowShell { get; }
            internal Transform Footer { get; }
            internal Transform FooterLeft { get; }
            internal Transform FooterRight { get; }

            internal NativeUiAssets(ScreenPrefabs provider)
            {
                if (provider == null)
                    ModLog.Warning("[ModAssets] Native UI provider unavailable: ScreenPrefabs.Instance.");

                var dialog = provider.InfoDialogScreen;
                PrimaryButton = dialog?.GetPrimaryButtonPrefab();
                SecondaryButton = dialog?.GetSecondaryButtonPrefab();
                PlainText = dialog?.GetPlainTextPrefab()?.gameObject;
                SubHeader = dialog?.GetSubHeaderPrefab()?.gameObject;
                LineItem = dialog?.GetLineItemPrefab()?.gameObject;
                OptionsButton = provider.OptionsScreen?.buttonPrefab;
                var menu = DlcManager.IsExpansion1Active() ? provider.MainMenuForSpacedOut : provider.MainMenuForVanilla;
                MainMenuButton = menu?.GetComponent<MainMenu>()?.buttonPrefab?.gameObject;
                var colony = provider.ColonyDestinationSelectScreen;
                MenuTabs = colony?.menuTabs;
                SeedInput = colony?.newGameSettingsPanel?.prefab_seed_input_setting
                    ?.GetComponent<CustomGameSettingSeed>()?.Input;
                FileNameInput = provider.FileNameDialog?.inputField;
                Input = provider.LockerNavigator?.GetComponent<LockerNavigator>()
                    ?.outfitBrowserScreen?.GetComponent<OutfitBrowserScreen>()?.inputFieldPrefab;
                CollapsiblePanel = provider.CollapsableContentPanel;
                WindowShell = provider.OptionsScreen?.transform.Find("CenterContent/Content/BG")?.gameObject;

                FooterLeft = dialog?.leftButtonPanel?.transform;
                FooterRight = dialog?.rightButtonPanel?.transform;
                var footer = FooterLeft;
                while (footer != null && FooterRight != null && !FooterRight.IsChildOf(footer))
                    footer = footer.parent;
                if (FooterRight != null && footer != null && footer != dialog.transform)
                    Footer = footer;
                else
                    ModLog.Warning(
                        "[ModAssets] Native InfoDialog footer is unavailable; " +
                        "footer-dependent UI cannot use this template.");

            }
        }
    }
}
