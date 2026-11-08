using System;
using Shared.Helpers;
using Shared.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    // Presentation only. The coordinator owns navigation and scene/Pause lifetime.
    public sealed class MultiplayerDialogScreen : KModalScreen
    {
        // Panel background and title bar background
        [SerializeField] private Image background = null!, titleBar = null!;

        [SerializeField] private Transform headerSlot = null!, closeSlot = null!;
        [SerializeField] private Transform content = null!;
        [SerializeField] private Transform footer = null!, footerLeft = null!, footerRight = null!;

        [SerializeField] private LocText versionPrefab = null!;

        private IUiTemplates templates = null!;

        private bool dismissing;
        public event System.Action? Dismissing;

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            pause = false;
            canBackoutWithRightClick = true;
        }

        public void Initialize(IUiTemplates templates)
        {
            InitializeComponent();

            UiUtil.CopyImage(background, templates.DialogPanelPrefab);

            this.templates = templates;

        }

        public MultiplayerDialogScreen SetHeader(string text)
        {
            UiUtil.CopyImage(titleBar, templates.DialogTitleBarPrefab);
            var header = Util.KInstantiateUI<LocText>(templates.DialogTitlePrefab.gameObject, headerSlot.gameObject, false);
            header.key = string.Empty;
            header.SetText(text);
            header.gameObject.SetActive(true);

            var closeButton = Util.KInstantiateUI<KButton>(templates.closeBtnPrefab.gameObject, closeSlot.gameObject, false);
            closeButton.ClearOnClick();
            closeButton.onClick += Deactivate;
            closeButton.gameObject.SetActive(true);

            return this;
        }

        public MultiplayerDialogScreen AddVersion(string text) => AddVersion(text, out _);

        public MultiplayerDialogScreen AddVersion(string text, out LocText version)
        {
            versionPrefab.key = string.Empty;
            versionPrefab.SetText(text);

            version = Util.KInstantiateUI<LocText>(versionPrefab.gameObject, footerLeft.gameObject, true);

            if (!footer.gameObject.activeSelf)
                footer.gameObject.SetActive(true);

            return this;
        }

        public MultiplayerDialogScreen AddUI<T>(T prefab, out T instance, bool activate = true) where T : MonoBehaviour
        {
            instance = Util.KInstantiateUI<T>(prefab.gameObject, content.gameObject, activate);
            return this;
        }

        public MultiplayerDialogScreen AddOption(string text, Action<MultiplayerDialogScreen> action)
            => AddOption(text, action, out _);

        public MultiplayerDialogScreen AddOption(string text, Action<MultiplayerDialogScreen> action,
            out KButton button, bool rightSide = true, bool accent = false)
        {
            button = Util.KInstantiateUI<KButton>(
                (accent ? templates.secondaryBtnPrefab : templates.primaryBtnPrefab).gameObject,
                rightSide ? footerRight.gameObject : footerLeft.gameObject,
                false);

            UiUtil.ConfigureButton(button, text, action != null ? () => action(this) : (System.Action?)null);
            button.gameObject.SetActive(true);

            if (!footer.gameObject.activeSelf)
                footer.gameObject.SetActive(true);

            return this;
        }

        public override void OnKeyDown(KButtonEvent e)
        {
            if (MultiplayerInput.ConsumeEditingInput(this, e)) return;
            base.OnKeyDown(e);
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            if (dismissing) return;
            dismissing = true;
            Dismissing?.Invoke();
        }

        public override void OnForcedCleanUp()
        {
            Dismissing = null;
            base.OnForcedCleanUp();
        }
    }
}
