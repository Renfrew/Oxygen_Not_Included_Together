using UnityEngine;

namespace Shared.UI
{
    // Authored structure; native KTabMenu owns registration, selection and visibility.
    public sealed class TabMenu : KTabMenu
    {
        [SerializeField] private KScreen verticalLayout = null!;

        public event System.Action<int, int> AfterTabActivated = null!;

        public KScreen GetContentPrefab() => verticalLayout;

        public override void ActivateTab(int index)
        {
            int previous = PreviousActiveTab;
            base.ActivateTab(index);
            // Native onTabActivated runs before PreviousActiveTab is updated.
            // Notify consumers afterward so synchronous synchronization/rollback is safe.
            AfterTabActivated?.Invoke(index, previous);
        }

        public void Initialize(KButton buttonPrefab)
        {
            InitializeComponent();
            header.prefab = buttonPrefab.GetComponent<RectTransform>();
        }
    }
}
