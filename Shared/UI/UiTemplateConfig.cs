using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    // Authored constraints and structures; native donors remain private to the mod.
    public sealed class UiTemplateConfig : MonoBehaviour
    {
        [SerializeField] private LayoutElement sectionActionButton = null!;
        [SerializeField] private LayoutElement dialogFooterButton = null!;
        [SerializeField] private LayoutElement inputField = null!;
        [SerializeField] private LayoutElement compactInputField = null!, lobbySection = null!;
        [SerializeField] private Text buttonTextPrefab = null!;
        [SerializeField] private KImage buttonFillPrefab = null!;
        [SerializeField] private MultiplayerSection section = null!;
        [SerializeField] private MultiplayerRow rowPrefab = null!;
        [SerializeField] private SessionPlayerRow playerRow = null!;

        public LayoutElement SectionActionButton => sectionActionButton;
        public LayoutElement DialogFooterButton => dialogFooterButton;
        public LayoutElement InputField => inputField;
        public LayoutElement CompactInputField => compactInputField;
        public LayoutElement LobbySection => lobbySection;
        public Text ButtonTextPrefab => buttonTextPrefab;
        public KImage ButtonFillPrefab => buttonFillPrefab;
        public MultiplayerSection Section => section;
        public MultiplayerRow RowPrefab => rowPrefab;
        public SessionPlayerRow PlayerRow => playerRow;

    }
}
