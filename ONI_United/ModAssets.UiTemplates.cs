using Shared.Helpers;
using Shared.Interfaces;
using Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ONI_United
{
    public static partial class ModAssets
    {
        private static UiTemplateCache uiTemplates;

        internal static IUiTemplates UiTemplates => uiTemplates;

        internal static void InitializeUiTemplates()
        {
            if (uiTemplates != null) return;
            var donors = UI;
            var configuration = ModUI.Config;

            GameObject root = null;
            try
            {
                root = new GameObject("ONI United UI Prefabs");
                root.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(root);

                T Clone<T>(GameObject donor, string name) where T : Component
                {
                    var instance = Util.KInstantiateUI(donor, root, false);
                    instance.name = name;
                    instance.SetActive(false);
                    return instance.GetComponent<T>();
                }

                // Artwork templates contain only their image, not the donor's screen hierarchy.
                Image Artwork(Image donor, string name)
                {
                    var go = new GameObject(name, typeof(RectTransform));
                    go.SetActive(false);
                    go.transform.SetParent(root.transform, false);
                    var image = go.AddComponent<Image>();
                    UiUtil.CopyImage(image, donor);
                    image.raycastTarget = donor.raycastTarget;
                    return image;
                }

                LocText Caption(LocText donor, string name)
                {
                    var text = Clone<LocText>(donor.gameObject, name);
                    text.key = string.Empty;
                    text.SetText(string.Empty);
                    return text;
                }

                // This local cache is not published until its composite prefabs are also ready.
                var prepared = new UiTemplateCache();
                prepared.primaryBtnPrefab = UiUtil.CreateButtonPrefab(
                    donors.PrimaryButton, root, "primaryBtnPrefab", configuration, donors.SecondaryButton);
                prepared.secondaryBtnPrefab = UiUtil.CreateButtonPrefab(
                    donors.SecondaryButton, root, "secondaryBtnPrefab", configuration, donors.SecondaryButton);
                prepared.closeBtnPrefab = Clone<KButton>(donors.HeaderCloseButton.gameObject, "closeBtnPrefab");
                prepared.closeBtnPrefab.ClearOnClick(); // Preserve the native close icon/caption.

                prepared.InputFieldPrefab = Clone<KInputTextField>(donors.Input.gameObject, "InputFieldPrefab");
                UiUtil.ConfigureLayout(prepared.InputFieldPrefab, configuration.InputField);
                UiUtil.ConfigureInputField(prepared.InputFieldPrefab);

                prepared.PlainTextPrefab = Clone<TMP_Text>(donors.PlainText, "PlainTextPrefab");
                UiUtil.ConfigureText(prepared.PlainTextPrefab, string.Empty);
                prepared.SectionHeadingPrefab = Caption(donors.SectionHeading, "SectionHeadingPrefab");
                prepared.DialogTitlePrefab = Caption(donors.DialogTitle, "DialogTitlePrefab");
                prepared.SectionPanelPrefab = Artwork(donors.SectionPanel, "SectionPanelPrefab");
                prepared.DialogPanelPrefab = Artwork(donors.DialogPanel, "DialogPanelPrefab");
                prepared.DialogTitleBarPrefab = Artwork(donors.DialogTitleBar, "DialogTitleBarPrefab");

                prepared.PlayerRowPrefab = Clone<SessionPlayerRow>(configuration.PlayerRow.gameObject, "PlayerRowPrefab");
                prepared.PlayerRowPrefab.Initialize(donors.PlainText, string.Empty);
                prepared.RowPrefab = Clone<MultiplayerRow>(configuration.RowPrefab.gameObject, "RowPrefab");
                prepared.SectionPrefab = Clone<MultiplayerSection>(configuration.Section.gameObject, "SectionPrefab");
                prepared.SectionPrefab.Initialize(prepared, string.Empty);

                uiTemplates = prepared;
            }
            catch
            {
                if (root != null) UnityEngine.Object.Destroy(root);
                throw;
            }
        }

        private sealed class UiTemplateCache : IUiTemplates
        {
            public KButton primaryBtnPrefab { get; internal set; }
            public KButton secondaryBtnPrefab { get; internal set; }
            public KInputTextField InputFieldPrefab { get; internal set; }
            public TMP_Text PlainTextPrefab { get; internal set; }
            public LocText SectionHeadingPrefab { get; internal set; }
            public Image SectionPanelPrefab { get; internal set; }
            public Image DialogPanelPrefab { get; internal set; }
            public Image DialogTitleBarPrefab { get; internal set; }
            public LocText DialogTitlePrefab { get; internal set; }
            public KButton closeBtnPrefab { get; internal set; }
            public SessionPlayerRow PlayerRowPrefab { get; internal set; }
            public MultiplayerSection SectionPrefab { get; internal set; }
            public MultiplayerRow RowPrefab { get; internal set; }

        }
    }
}
