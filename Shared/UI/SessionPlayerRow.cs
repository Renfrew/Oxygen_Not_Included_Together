using TMPro;
using UnityEngine;

namespace Shared.UI
{
    // A row receives display data, never a networking/session object.
    public sealed class SessionPlayerRow : MonoBehaviour
    {
        [SerializeField] private Transform textSlot = null!;
        [SerializeField] private TMP_Text label = null!;
        public void Initialize(GameObject textPrefab, string value)
        {
            var instance = Util.KInstantiateUI(textPrefab, textSlot.gameObject, false);
            label = instance.GetComponentInChildren<TMP_Text>(true);
            if (label is LocText localized) localized.key = string.Empty;
            label.richText = false;
            label.raycastTarget = false;
            SetText(value);
            instance.SetActive(true);
        }
        public void SetText(string value) => label.SetText(value);
    }
}
