using Shared.Helpers;
using Shared.Interfaces;
using TMPro;
using UnityEngine;

namespace Shared.UI
{
    public sealed class MultiplayerHostContent : KTabMenu
    {
        [SerializeField] private MultiplayerSection playerSettings = null!, steamSettings = null!, lanSettings = null!;
        [SerializeField] private Transform maxPlayersLabel = null!, maxPlayersInput = null!;
        [SerializeField] private Transform steamMessage = null!, portLabel = null!, portInput = null!, status = null!;
        private KInputTextField playerLimit = null!, port = null!;
        private TMP_Text statusText = null!;
        private KButton startButton = null!;
        private int lanIndex;
        public bool IsLan => PreviousActiveTab == lanIndex;
        public event System.Action<bool>? TransportChanged;
        public string PlayerLimit => playerLimit.text;
        public string Port => port.text;

        public void Initialize(IUiTemplates templates, MultiplayerDialogScreen dialog)
        {
            playerSettings.Initialize(templates, "HOST SETTINGS");
            steamSettings.Initialize(templates, "STEAM HOSTING");
            lanSettings.Initialize(templates, "LAN HOSTING");

            UiUtil.CreateText(templates.PlainTextPrefab, maxPlayersLabel.gameObject, $"Maximum Players ({1}-{4})");

            playerLimit = UiUtil.CreateInputField(templates.InputFieldPrefab, maxPlayersInput.gameObject, "4", "Max Players", TMP_InputField.ContentType.IntegerNumber, 2);
            UiUtil.CreateText(templates.PlainTextPrefab, steamMessage.gameObject, "Steam hosting is unavailable until end-to-end operation is verified.");
            UiUtil.CreateText(templates.PlainTextPrefab, portLabel.gameObject, "Listening Port");

            port = UiUtil.CreateInputField(templates.InputFieldPrefab, portInput.gameObject, "8080", "Port", TMP_InputField.ContentType.IntegerNumber, 5);

            statusText = UiUtil.CreateText(templates.PlainTextPrefab, status.gameObject, string.Empty);
            dialog.AddOption("START HOSTING", null, out startButton, rightSide: false, accent: true);
            var pages = this.GetTabPages();
            int steam = AddTab(null, "Steam", pages[0], string.Empty);
            lanIndex = AddTab(null, "LAN", pages[1], string.Empty);
            onTabActivated += OnTransportSelected;
            ActivateTab(steam);

        }

        public void ConfigureStart(System.Action onStart, string initialLimit, string initialPort)
        {
            startButton.ClearOnClick();
            startButton.onClick += onStart;
            playerLimit.SetTextWithoutNotify(initialLimit);
            port.SetTextWithoutNotify(initialPort);
        }

        public void SetAvailability(bool canStart, string message)
        {
            statusText.SetText(message);
            startButton.isInteractable = canStart && IsLan;
        }
        public override void OnShow(bool show)
        {
            base.OnShow(show);
            startButton.gameObject.SetActive(show);
            if (!show) StopEditing();
        }
        public void StopEditing() => UiUtil.StopEditing(playerLimit, port);
        private void OnTransportSelected(int current, int previous)
        {
            if (current == previous) return;
            StopEditing();
            TransportChanged?.Invoke(current == lanIndex);
        }
        public override void OnSpawn()
        {
            base.OnSpawn();
            if (PreviousActiveTab >= 0) ActivateTab(PreviousActiveTab);
        }
        public override void OnForcedCleanUp()
        {
            onTabActivated -= OnTransportSelected;
            TransportChanged = null;
            StopEditing();
            base.OnForcedCleanUp();
        }
    }
}
