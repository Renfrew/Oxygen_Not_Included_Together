using Shared.UI;
using TMPro;
using UnityEngine.UI;

namespace Shared.Interfaces
{
    // Ready-to-clone runtime prefabs. Consumers configure data and callbacks only on their clones.
    public interface IUiTemplates
    {
        KButton primaryBtnPrefab { get; }
        KButton secondaryBtnPrefab { get; }
        KInputTextField InputFieldPrefab { get; }
        TMP_Text PlainTextPrefab { get; }
        LocText SectionHeadingPrefab { get; }
        Image SectionPanelPrefab { get; }
        Image DialogPanelPrefab { get; }
        Image DialogTitleBarPrefab { get; }
        LocText DialogTitlePrefab { get; }
        KButton closeBtnPrefab { get; }
        SessionPlayerRow PlayerRowPrefab { get; }
        MultiplayerSection SectionPrefab { get; }
        MultiplayerRow RowPrefab { get; }
    }
}
