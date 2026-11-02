using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    public sealed class GalleryEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text candidateLabel = null!;
        [SerializeField] private TMP_Text sourceLabel = null!;
        [SerializeField] private TMP_Text verdictLabel = null!;
        [SerializeField] private TMP_Text feedbackLabel = null!;
        [SerializeField] private Transform sample = null!;
        [SerializeField] private Transform[] sampleSlots = null!;
        [SerializeField] private Button suitableButton = null!;
        [SerializeField] private Button unsuitableButton = null!;
        [SerializeField] private Button failedButton = null!;
        [SerializeField] private Button disableButton = null!;
        private Action<string> onAction = null!;
        private System.Action onShown = null!;

        public void Initialize(string candidate, string source, Action<string> onAction, System.Action onShown,
            Action<Func<GameObject, int, GameObject>, Action<string>, Action<string>> populateSamples)
        {
            candidateLabel.SetText(candidate);
            sourceLabel.SetText(source);
            this.onAction = onAction;
            this.onShown = onShown;
            suitableButton.onClick.AddListener(() => OnAction("Suitable"));
            unsuitableButton.onClick.AddListener(() => OnAction("Unsuitable"));
            failedButton.onClick.AddListener(() => OnAction("Failed"));
            disableButton.onClick.AddListener(() => OnAction("Disable"));
            populateSamples(SpawnSample, SetVerdict, SetFeedback);
        }

        private void Start() => onShown();

        public void OnAction(string action) => onAction(action);

        private GameObject SpawnSample(GameObject prefab, int item) =>
            Util.KInstantiateUI(prefab, (item < 0 ? sample : sampleSlots[item]).gameObject, true);

        private void SetVerdict(string message) => verdictLabel.SetText(message);
        private void SetFeedback(string message) => feedbackLabel.SetText(message);
    }
}
