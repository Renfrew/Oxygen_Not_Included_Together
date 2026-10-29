using System.Collections.Generic;
using Shared.Helpers;
using Shared.Interfaces;
using TMPro;
using UnityEngine;

namespace Shared.UI
{
    public sealed class MultiplayerSessionContent : KScreen
    {
        [SerializeField] private Transform status = null!, role = null!, transport = null!, members = null!;
        [SerializeField] private KScrollRect playersScroll = null!;
        private readonly List<SessionPlayerRow> rows = new();
        private readonly List<string> displayed = new();
        private TMP_Text statusText = null!, roleText = null!, transportText = null!, membersText = null!;
        private IUiTemplates templates = null!;
        private KButton stopButton = null!, cancelButton = null!;
        public void Initialize(IUiTemplates templates, MultiplayerDialogScreen dialog, System.Action onStop, System.Action onCancel)
        {
            this.templates = templates;

            statusText = UiUtil.CreateText(templates.PlainTextPrefab, status.gameObject, "");
            roleText = UiUtil.CreateText(templates.PlainTextPrefab, role.gameObject, "");
            transportText = UiUtil.CreateText(templates.PlainTextPrefab, transport.gameObject, "");
            membersText = UiUtil.CreateText(templates.PlainTextPrefab, members.gameObject, "");
            dialog.AddOption("STOP HOSTING", _ => onStop(), out stopButton, rightSide: false, accent: true);
            dialog.AddOption("CANCEL CONNECTION", _ => onCancel(), out cancelButton);
        }
        public void SetState(string status, string role, string transport, bool canStop, bool canCancel, bool stopping)
        {
            statusText.SetText(status); roleText.SetText(role); transportText.SetText(transport);
            stopButton.gameObject.SetActive(canStop); stopButton.isInteractable = !stopping;
            cancelButton.gameObject.SetActive(canCancel);
        }
        public override void OnShow(bool show)
        {
            base.OnShow(show);
            if (!show) { stopButton.gameObject.SetActive(false); cancelButton.gameObject.SetActive(false); }
        }
        public void SetPlayers(IReadOnlyList<string> values)
        {
            bool unchanged = displayed.Count == values.Count;
            for (int i = 0; unchanged && i < values.Count; i++) unchanged = displayed[i] == values[i];
            if (unchanged) { membersText.SetText($"Admitted session members: {values.Count}"); return; }
            float position = playersScroll.verticalNormalizedPosition;
            for (int i = 0; i < values.Count; i++)
            {
                if (i == rows.Count)
                {
                    var row = Util.KInstantiateUI<SessionPlayerRow>(templates.PlayerRowPrefab.gameObject, playersScroll.content.gameObject, false);
                    rows.Add(row); row.SetText(values[i]); row.gameObject.SetActive(true);
                }
                else rows[i].SetText(values[i]);
            }
            while (rows.Count > values.Count)
            {
                var row = rows[rows.Count - 1]; rows.RemoveAt(rows.Count - 1);
                row.gameObject.SetActive(false); Destroy(row.gameObject);
            }
            displayed.Clear(); foreach (var value in values) displayed.Add(value);
            membersText.SetText($"Admitted session members: {values.Count}");
            if (playersScroll.isActiveAndEnabled)
            { Canvas.ForceUpdateCanvases(); playersScroll.verticalNormalizedPosition = position; }
        }
        public override void OnForcedCleanUp()
        {
            rows.Clear(); displayed.Clear(); base.OnForcedCleanUp();
        }
    }
}
