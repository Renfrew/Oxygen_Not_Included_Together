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
            public GameObject PrimaryButton { get; }
            public GameObject SecondaryButton { get; }
            public GameObject PlainText { get; }
            public GameObject SubHeader { get; }
            public GameObject OptionsButton { get; }
            public GameObject MainMenuButton { get; }
            public GameObject LineItem { get; }
            public GameObject CollapsiblePanel { get; }
            public MultiToggle[] MenuTabs { get; }
            public KInputTextField SeedInput { get; }
            public KInputTextField FileNameInput { get; }
            public KInputTextField Input { get; }
            public KButton HeaderCloseButton { get; }
            public UnityEngine.UI.Image SectionPanel { get; }
            public LocText SectionHeading { get; }
            public UnityEngine.UI.Image DialogPanel { get; }
            public UnityEngine.UI.Image DialogTitleBar { get; }
            public LocText DialogTitle { get; }

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
                var graphics = provider.OptionsScreen.graphicsOptionsScreenPrefab;
                HeaderCloseButton = graphics.closeButton;
                DialogPanel = graphics.transform.Find("Content/BG").GetComponent<UnityEngine.UI.Image>();
                DialogTitleBar = graphics.transform.Find("Content/TitleBar").GetComponent<UnityEngine.UI.Image>();
                DialogTitle = graphics.transform.Find("Content/TitleBar/Title").GetComponent<LocText>();
                SectionPanel = graphics.transform.Find("Content/DisplaySettings/BG").GetComponent<UnityEngine.UI.Image>();
                SectionHeading = graphics.transform.Find("Content/DisplaySettings/Header").GetComponent<LocText>();
            }
        }
    }
}
