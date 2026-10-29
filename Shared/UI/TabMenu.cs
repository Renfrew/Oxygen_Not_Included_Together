using UnityEngine;

namespace Shared.UI
{
    // Authored structure; native KTabMenu owns registration, selection and visibility.
    public sealed class TabMenu : KTabMenu
    {
        [SerializeField] private KScreen verticalLayout = null!;

        public KScreen GetContentPrefab() => verticalLayout;

        public void Initialize(KButton buttonPrefab)
        {
            InitializeComponent();
            header.prefab = buttonPrefab.GetComponent<RectTransform>();
        }
    }
}
