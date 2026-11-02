using UnityEngine;

namespace Shared.UI
{
    public sealed class MultiplayerRow : KMonoBehaviour
    {
        [SerializeField] private Transform left = null!, right = null!;

        public MultiplayerRow AddUI<T>(T prefab, out T instance, bool useLeft = true) where T : MonoBehaviour
        {
            var target = useLeft ? left : right;
            instance = Util.KInstantiateUI<T>(prefab.gameObject, target.gameObject, true);
            if (!target.gameObject.activeSelf)
                target.gameObject.SetActive(true);
            return this;
        }
    }
}
