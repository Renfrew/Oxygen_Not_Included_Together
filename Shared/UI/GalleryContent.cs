using UnityEngine;

namespace Shared.UI
{
    public sealed class GalleryContent : KScreen
    {
        [SerializeField] private KScrollRect displayScrollRect = null!;

        public void Add(GameObject prefab, string candidate, string source,
            System.Action<string> onAction, System.Action onShown,
            System.Action<System.Func<GameObject, int, GameObject>, System.Action<string>, System.Action<string>> populateSamples)
        {
            var view = Util.KInstantiateUI<GalleryEntryView>(prefab, displayScrollRect.content.gameObject, false);
            view.Initialize(candidate, source, onAction, onShown, populateSamples);
            view.gameObject.SetActive(true);
        }
    }
}
