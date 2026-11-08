#if DEBUG
using System;
using System.Collections.Generic;
using ONI_United.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ONI_United.DebugTools
{
    // Explicit vanilla construction sources, not arbitrary live-screen subtree searches.
    internal static class NativeGalleryCandidates
    {
        private const string DialogSource = "ScreenPrefabs.InfoDialogScreen (configured holder, not a live dialog); getters / AddOption";
        private const string TableSource = "Assets.UIPrefabs.TableScreenWidgets; Assets.OnPrefabInit; table-column consumers";
        private const string SeedSource = "ScreenPrefabs.ColonyDestinationSelectScreen.newGameSettingsPanel.prefab_seed_input_setting.Input";

        internal static IEnumerable<NativeGalleryCandidate> All()
        {
            var assets = ModAssets.UI;
            var table = ModAssets.UI.TableWidgets;
            yield return Button("dialog.primary", "Buttons", DialogSource + "; left/primary", assets.PrimaryButton);
            yield return Button("dialog.secondary", "Buttons", DialogSource + "; right/secondary (default OK)", assets.SecondaryButton);
            yield return Button("menu.options", "Buttons", "ScreenPrefabs.OptionsScreen.buttonPrefab; KButtonMenu.RefreshButtons; vanilla also sets text colors", assets.OptionsButton);
            yield return Button("menu.main", "Buttons", "MainMenu serialized buttonPrefab on configured MainMenu asset; vanilla applies style/font size AFTER cloning; no overrides here", assets.MainMenuButton);
            yield return Button("table.button", "Buttons", TableSource + "; ButtonLabelColumn", table.ButtonLabel);
            yield return Button("table.button.white", "Buttons", TableSource + "; historical visually unsuitable baseline, including secondary content", table.ButtonLabelWhite);

            yield return Text("dialog.text", assets.PlainText, DialogSource + "; AddPlainText / InfoScreenPlainText.SetText");
            yield return Text("dialog.subheader", assets.SubHeader, DialogSource + "; AddSubHeader / InfoScreenPlainText.SetText");
            yield return Text("table.label", table.Label, TableSource + "; LabelTableColumn; width overrides intentionally omitted");
            yield return Text("table.header", table.LabelHeader, TableSource + "; LabelTableColumn.GetHeaderWidget; sorting controller not attached");

            yield return Input("input.text", "Inputs", assets.SeedInput, SeedSource, false);
            yield return Input("input.numeric", "Inputs", assets.SeedInput, SeedSource, true);
            yield return New("input.number-controller", "Inputs",
                "KNumberInputField : KInputField / KScreen; known donor: DetailsScreen.sideScreens[].screenPrefab (ThresholdSwitchSideScreen).numberInput; no independent frontend prefab provider verified",
                false, entry =>
                {
                    throw new InvalidOperationException("KNumberInputField unavailable: no independent frontend donor verified; known owner is ThresholdSwitchSideScreen.numberInput in DetailsScreen.sideScreens.");
                });
            yield return Input("input.filename", "Inputs", assets.FileNameInput, "ScreenPrefabs.FileNameDialog.inputField; configured input only, no dialog activation", false);
            yield return Input("input.outfit", "Inputs", assets.Input, "ScreenPrefabs.LockerNavigator.outfitBrowserScreen.inputFieldPrefab", false);
            yield return New("selection.checkbox", "Selection", TableSource + "; CheckboxTableColumn.GetDefaultWidget", false, entry =>
            {
                var go = entry.Spawn(table.Checkbox);
                entry.Toggle(go.GetComponentInChildren<MultiToggle>(true), true);
            });
            yield return New("selection.dropdown", "Selection", TableSource + "; NumericDropDownTableColumn.GetWidget", false, entry =>
            {
                var go = entry.Spawn(table.NumericDropDown);
                var dropdown = go.GetComponentInChildren<TMP_Dropdown>(true)
                    ?? throw new InvalidOperationException("NumericDropDown lacks TMP_Dropdown.");
                // Options/values and their callbacks are diagnostic data, not presentation.
                dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
                dropdown.ClearOptions();
                dropdown.AddOptions(new List<string> { "First", "Second", "A longer native dropdown option for overflow inspection" });
                UnityEngine.Events.UnityAction<int> selected = value => entry.Run(() => entry.Event("dropdown value=" + value));
                dropdown.onValueChanged.AddListener(selected);
            });
            yield return New("selection.tabs", "Selection", "ColonyDestinationSelectScreen.menuTabs serialized MultiToggle references; OnPrefabInit/SelectMenuTab; no live transform search", false, entry =>
            {
                var source = assets.MenuTabs;
                if (source == null || source.Length == 0)
                    throw new InvalidOperationException("Configured colony menu tab references unavailable.");
                var tabs = new List<MultiToggle>();
                for (int i = 0; i < Math.Min(3, source.Length); i++)
                {
                    if (source[i] == null) throw new InvalidOperationException("Native tab template unavailable.");
                    var go = entry.Spawn(source[i].gameObject);
                    var toggle = go.GetComponent<MultiToggle>();
                    if (toggle == null || toggle.states == null || toggle.states.Length < 2)
                        throw new InvalidOperationException("Tab has fewer than two configured states.");
                    NativeUiBinding.SetCaption(go, "Tab " + (i + 1));
                    tabs.Add(toggle);
                    int index = i;
                    System.Action click = () => entry.Run(() =>
                    {
                        for (int j = 0; j < tabs.Count; j++) tabs[j].ChangeState(j == index ? 1 : 0);
                        entry.Event("tab=" + (index + 1));
                    });
                    toggle.onClick = click;
                    toggle.ChangeState(i == 0 ? 1 : 0);
                }
            });

            yield return New("list.native.rows", "Lists", DialogSource + "; GetLineItemPrefab / AddLineItem. Native row templates in four authored list slots", true, entry =>
            {
                for (int i = 0; i < 4; i++)
                {
                    var go = entry.Spawn(assets.LineItem, i);
                    var row = go.GetComponent<InfoScreenLineItem>() ?? throw new InvalidOperationException("InfoScreenLineItem missing.");
                    string value = i == 3 ? "Long native row: observe wrapping, typography, clipping and the authored scrolling container without runtime sizing changes." : "Native row " + (i + 1);
                    NativeUiBinding.SetCaption(go, value);
                    row.SetText(value);
                    row.SetTooltip("Gallery row " + (i + 1) + ": tooltip uses the native configured component.");
                }
            });
            yield return New("container.collapsible", "Containers", "ScreenPrefabs.CollapsableContentPanel; CollapsibleDetailContentPanel.OnPrefabInit/SetLabel/Commit/ToggleOpen", true, entry =>
            {
                var go = entry.Spawn(assets.CollapsiblePanel, 0);
                var panel = go.GetComponent<CollapsibleDetailContentPanel>()
                    ?? throw new InvalidOperationException("Configured panel lacks CollapsibleDetailContentPanel.");
                // Technical initialization: this native API needs its OnPrefabInit dictionaries
                // before SetLabel. Native methods own their existing collapse presentation.
                panel.InitializeComponent();
                if (panel.HeaderLabel != null) panel.HeaderLabel.key = string.Empty;
                panel.SetTitle("Native collapsible section");
                panel.SetLabel("gallery-one", "Native content row", "No gameplay selection or networking is bound.");
                panel.SetLabel("gallery-two", "Second content row — click the native header to collapse/expand.", "Native tooltip");
                panel.Commit();
                if (panel.collapseButton == null) throw new InvalidOperationException("Native collapse toggle missing.");
                entry.Toggle(panel.collapseButton, false);
            });

            yield return New("feedback.tooltip", "Feedback", DialogSource + "; InfoScreenLineItem.SetTooltip; retains native tooltip typography and placement", false, entry =>
            {
                var go = entry.Spawn(assets.LineItem);
                var row = go.GetComponent<InfoScreenLineItem>() ?? throw new InvalidOperationException("InfoScreenLineItem missing.");
                NativeUiBinding.SetCaption(go, "Hover this native row to inspect its tooltip.");
                row.SetText("Hover this native row to inspect its tooltip.");
                row.SetTooltip("Native tooltip sample. Longer text checks wrapping. No fonts, colors or tooltip positions are assigned by the gallery.");
                if (go.GetComponentInChildren<ToolTip>(true) == null) throw new InvalidOperationException("Native tooltip missing.");
            });
            yield return Button("feedback.disabled", "Feedback", DialogSource + "; use Enable / disable to compare native interaction states", assets.SecondaryButton);
            yield return Input("feedback.validation", "Feedback", assets.SeedInput, SeedSource, true);
        }

        private static NativeGalleryCandidate Button(string id, string category, string source, GameObject template)
            => New(id, category, source, false, entry =>
            {
                var go = entry.Spawn(template);
                NativeUiBinding.SetCaption(go, "Native test: click me");
                int clicks = 0;
                entry.Click(go.GetComponent<KButton>(), () => entry.Event("click=" + (++clicks)));
            });

        private static NativeGalleryCandidate Text(string id, GameObject template, string source)
            => New(id, "Text", source, false, entry =>
            {
                var go = entry.Spawn(template);
                const string text = "Native template: The quick brown fox jumps over the lazy dog. Longer text lets you compare wrapping and glyphs against the authored LiberationSans labels.";
                NativeUiBinding.SetCaption(go, text);
                var plain = go.GetComponent<InfoScreenPlainText>();
                if (plain != null) plain.SetText(text);
            });

        private static NativeGalleryCandidate Input(string id, string category, KInputTextField template, string source, bool numeric)
            => New(id, category,
                source + "; "
                + (numeric ? "numeric-configured KInputTextField (IntegerNumber; digits-only callback), not KNumberInputField" : "KInputTextField; text-entry test callback"), false, entry =>
            {
                var go = entry.Spawn(template != null ? template.gameObject : null);
                var input = go.GetComponent<KInputTextField>() ?? throw new InvalidOperationException("KInputTextField missing.");
                // Isolate behavioral events: do not carry serialized world-setting callbacks.
                NativeUiBinding.BindInput(input, numeric ? "12345" : "Edit this native input",
                    numeric ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.Standard,
                    input.characterLimit);
                input.onValidateInput = numeric ? Digits : null;
                UnityEngine.Events.UnityAction<string> changed = value => entry.Run(() => entry.Event("text=" + NativeUiGallery.Short(value, 80)));
                UnityEngine.Events.UnityAction<string> edited = value => entry.Run(() => entry.Event("end edit=" + NativeUiGallery.Short(value, 80)));
                input.onValueChanged.AddListener(changed); input.onEndEdit.AddListener(edited);
            });

        private static char Digits(string text, int position, char value) => value >= '0' && value <= '9' ? value : '\0';

        private static NativeGalleryCandidate New(string id, string category, string source, bool list, System.Action<NativeGalleryCandidate> build)
            => new NativeGalleryCandidate { Id = id, Category = category, Source = source, List = list, Build = build };
    }

    internal sealed class NativeGalleryCandidate
    {
        internal string Id, Category, Source;
        internal bool List;
        internal System.Action<NativeGalleryCandidate> Build;

        private Func<GameObject, int, GameObject> spawnSample;
        private System.Action<string> setVerdict;
        private System.Action<string> setFeedback;
        private readonly List<GameObject> samples = [];
        private System.Action<string> report;
        private bool disabled, failed;
        private int events;

        internal GameObject Prefab => Category == "Containers" ? ModAssets.ModUI.GalleryContainerEntry
            : List ? ModAssets.ModUI.GalleryListEntry : ModAssets.ModUI.GalleryEntry;

        internal void PopulateSamples(System.Action<string> report,
            Func<GameObject, int, GameObject> spawnSample,
            System.Action<string> setVerdict, System.Action<string> setFeedback)
        {
            this.report = report;
            this.spawnSample = spawnSample;
            this.setVerdict = setVerdict;
            this.setFeedback = setFeedback;
            try
            {
                Build(this);
                Verdict(Id == "table.button.white"
                    ? "Functional but visually unsuitable — prior status-panel test; compare unchanged"
                    : "Requires additional inspection — visual observation pending");
            }
            catch (Exception error) { Fail(error); }
        }

        internal void HandleAction(string action) => Run(() =>
        {
            if (action == "Disable") ToggleDisabled();
            else Observe(action);
        });

        internal GameObject Spawn(GameObject template, int item = -1)
        {
            if (template == null) throw new InvalidOperationException("Native template unavailable: " + Source);
            // Input/other nested templates must not bring their screen or world-settings controller.
            if (template.GetComponentInChildren<KScreen>(true) != null || template.GetComponentInChildren<CustomGameSettingWidget>(true) != null)
                throw new InvalidOperationException("Template contains a specialized screen/settings controller; not safely isolated.");
            var instance = spawnSample(template, item);
            samples.Add(instance);
            ModAssets.ValidateUIComponents(instance);
            ModLog.Debug($"[NativeUiGallery][{Id}] template={template.name}; owner="
                + (template.transform.parent != null ? template.transform.parent.name : "<root>")
                + $"; source={Source}; prepared, not visually verified.");
            return instance;
        }

        internal void Click(KButton button, System.Action action)
        {
            System.Action handler = () => Run(action);
            if (button == null) throw new InvalidOperationException("Native KButton missing.");
            button.ClearOnClick();
            button.onClick += handler;
            int hover = 0, press = 0;
            System.Action entered = () => { if (++hover <= 2) Event("hover " + button.name); };
            System.Action down = () => { if (++press <= 2) Event("press " + button.name); };
            button.onPointerEnter += entered; button.onPointerDown += down;
        }

        internal void Toggle(MultiToggle toggle, bool advance)
        {
            if (toggle == null || toggle.states == null || toggle.states.Length == 0)
                throw new InvalidOperationException("MultiToggle lacks configured states.");
            System.Action handler = () => Run(() =>
            {
                if (advance) toggle.NextState();
                Event("selection state=" + toggle.CurrentState);
            });
            // Standalone checkboxes are driven here; initialized native panels own their action.
            if (advance) toggle.onClick = handler;
            else toggle.onClick += handler;
        }

        internal void Run(System.Action action)
        {
            if (failed) return;
            try { action(); } catch (Exception error) { Fail(error); }
        }

        internal void Event(string text)
        {
            events++;
            setFeedback(NativeUiGallery.Short("Event " + events + ": " + text, 280));
            if (events <= 12) report("[" + Id + "] " + NativeUiGallery.Short(text, 160));
        }

        private void Verdict(string value) => setVerdict(value);

        private void Observe(string value)
        {
            if (failed) { Event("Cannot approve a candidate that failed construction/interaction."); return; }
            string verdict = value == "Suitable" ? "Visually suitable" : value == "Unsuitable" ? "Functional but visually unsuitable" : "Instantiation/interaction failure";
            Verdict(verdict + " — manually observed");
            report("[" + Id + "] OBSERVER_VERDICT=" + verdict);
        }

        private void Fail(Exception error)
        {
            failed = true;
            string verdict = error is InvalidOperationException && error.Message.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Unavailable" : "Instantiation/interaction failure";
            Verdict(verdict + ": " + NativeUiGallery.Short(error.Message, 180));
            setFeedback("Other entries remain usable. See Player.log for the full exception.");
            report($"[{Id}] {verdict}: {error.Message} — see Player.log for the exception.");
            ModLog.Error($"[NativeUiGallery][{Id}] source={Source}; {verdict}: {error}");
            foreach (var sample in samples) if (sample != null) sample.SetActive(false);
        }

        private void ToggleDisabled()
        {
            if (failed) { Event("No working candidate to disable."); return; }
            bool found = false;
            disabled = !disabled;
            foreach (var sample in samples)
            {
                if (sample == null) continue;
                foreach (var b in sample.GetComponentsInChildren<KButton>(true)) { b.isInteractable = !disabled; found = true; }
                foreach (var s in sample.GetComponentsInChildren<Selectable>(true)) { s.interactable = !disabled; found = true; }
            }
            Event(found ? "Interactable=" + !disabled + "; native disabled visuals retained" : "No native interactable-state API bound for this sample.");
        }

        internal void Inspect()
        {
            if (failed) return;
            int budget = 28;
            foreach (var sample in samples) NativeGalleryInspection.Record(Id, sample, ref budget);
        }
    }
}
#endif
