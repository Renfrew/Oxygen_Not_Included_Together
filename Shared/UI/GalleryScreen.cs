using TMPro;
using UnityEngine;

namespace Shared.UI
{
    public sealed class GalleryScreen : KTabMenu
    {
        [SerializeField] private TMP_Text result = null!;
        [SerializeField] private KButton closeButton = null!;
        [SerializeField] private GameObject emptyState = null!;

        public GameObject PageParent => body.gameObject;

        public void AddPage(string title, GalleryContent page, System.Action onSelected)
        {
            int index = AddTab(null, title, page, string.Empty);
            onTabActivated += (current, previous) =>
            {
                if (current == index && current != previous) onSelected();
            };
            // Adding a header item updates its artwork; restore the selected page afterward.
            ActivateTab(PreviousActiveTab < 0 ? index : PreviousActiveTab);
            emptyState.SetActive(false);
        }

        public void SetStatus(string message) => result.SetText(message);

        public override void OnPrefabInit()
        {
            base.OnPrefabInit();
            ConsumeMouseScroll = true;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            closeButton.onClick += Deactivate;
        }

        public override void OnKeyDown(KButtonEvent e)
        {
            base.OnKeyDown(e);
            if (!e.Consumed && e.TryConsume(global::Action.Escape)) Deactivate();
        }
    }
}
