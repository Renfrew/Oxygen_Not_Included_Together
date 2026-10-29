using System;
using Shared.Helpers;
using Shared.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    // A grouped panel inside authored content. The containing dialog owns modality/lifetime.
    public sealed class MultiplayerSection : KMonoBehaviour
    {
        [SerializeField] private Image background = null!;
        [SerializeField] private Transform headerSlot = null!, actions = null!;
        [SerializeField] private Transform content = null!;
        [SerializeField] private LocText header = null!;
        [SerializeField] private KButton actionButtonPrefab = null!;
        [SerializeField] private MultiplayerRow rowPrefab = null!;

        public void Initialize(IUiTemplates templates, string headerText)
        {
            UiUtil.CopyImage(background, templates.SectionPanelPrefab);

            if (header == null)
                header = Util.KInstantiateUI<LocText>(templates.SectionHeadingPrefab.gameObject, headerSlot.gameObject, false);
            header.key = string.Empty;
            header.SetText(headerText);
            header.gameObject.SetActive(true);

            actionButtonPrefab = templates.primaryBtnPrefab;
            rowPrefab = templates.RowPrefab;
        }

        public MultiplayerSection SetHeader(string text)
        {
            header.key = string.Empty;
            header.SetText(text);
            return this;
        }

        // Header actions never dismiss a section or its containing dialog.
        public MultiplayerSection AddOption(string text, Action<MultiplayerSection>? action)
            => AddOption(text, action, out _);

        // Like InfoDialog's handle overload: the owning content may update this action's availability.
        public MultiplayerSection AddOption(string text, Action<MultiplayerSection>? action, out KButton button)
        {
            button = Util.KInstantiateUI<KButton>(actionButtonPrefab.gameObject, actions.gameObject, false);
            UiUtil.ConfigureButton(button, text, action != null ? () => action(this) : (System.Action?)null);

            button.gameObject.SetActive(true);

            if (!actions.gameObject.activeSelf)
                actions.gameObject.SetActive(true);

            return this;
        }

        public MultiplayerSection AddRow(out MultiplayerRow row)
            => AddUI(rowPrefab, out row);

        public MultiplayerSection AddUI<T>(T prefab, out T instance) where T : MonoBehaviour
        {
            instance = Util.KInstantiateUI<T>(prefab.gameObject, content.gameObject, true);
            return this;
        }
    }
}
