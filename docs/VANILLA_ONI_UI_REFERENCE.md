# Vanilla Oxygen Not Included UI Architecture — Practical Reference

## Current-source supersession note — 2026-10-29

The vanilla observations and historical artifact measurements below retain their original scope. Their descriptions of then-current Multiplayer code are not an authoritative map of the present checkpoint.

Current source uses the authored Shared.UI.MultiplayerDialogScreen : KModalScreen shell and ordinary MultiplayerScreen coordinator. Join and Host compose reusable sections/rows and native controls through supported Shared APIs, using the reusable Shared.UI.TabMenu, instance GetContentPrefab(), inherited AddTab and explicit initial selection. The previous MultiplayerMenuScreen/MultiplayerTransportContent/MultiplayerControls arrangement and concrete InfoDialog shell descriptions below are superseded. Session retains its separate content component. Legacy Host authoring assets remain but are not the new Host runtime loading path.

Host has shared Maximum Players above tabs, two Settings rows per transport, Start Hosting left and Cancel right, with no version/status/validation-message area. Join below-row validation-feedback geometry is also superseded; public-lobby status remains. Do not carry earlier geometry measurements, test counts or class-specific improvement suggestions onto these newer artifacts. Shared owns insertion/interaction mechanics, Unity owns authored presentation, and mod views supply feature composition/domain integration. See UI_DESIGN_PRINCIPLES.md for the current contract, STATUS.md for bounded evidence, and DEVELOPMENT_ROADMAP.md for open gates. Connecting-state progress/cancellation and full runtime verification remain incomplete.

## 2026-10-26 clarification — native right-click and footer geometry

Normal installed assembly inspection confirms KModalScreen.OnKeyDown processes Escape and `TryConsume(Action.MouseRight) && canBackoutWithRightClick` before KScreen's editing check. The public flag defaults false; TryConsume executes first, so false can swallow right-click without closing. Native InfoDialog serializes false. With escapeCloses=true, InfoDialog first handles Escape and PlayerController.ConsumeIfNotDragging(MouseRight), then falls back to KModalScreen. PlayerController excludes `(dragAction == action && dragging)` only. Without PlayerController, the flag controls fallback dismissal. Setting it true also enables fallback during right-drag; KModalScreen itself has no PlayerController drag filter. This distinguishes configuration failure from an inherent shell limitation. Reported in-game failure conditions still require runtime evidence.

InfoDialog's right footer group has spacing 0; the outer group's spacing 5 is between groups. Its button templates have minimum 128 x 48 and 16 horizontal padding. Graphics Apply supplies native 100 x 32 precedent; Mods Manage is 100 x 40 and footer spacing is 10. These are serialized constraints, not guaranteed resolved dimensions. Graphics title/Close are 24 high and native title/button fonts are 14. The authored Multiplayer replacement uses these contracts while keeping native templates intact. Historical current-implementation passages below describe the earlier InfoDialog shell; current ownership is in UI_DESIGN_PRINCIPLES.md.


**Date:** 26 October 2026 (America/Toronto).  
**Status:** Read-only investigation artifact; recommendations are proposals, not approved architecture decisions.  
**Project:** Multiplayer: ONI United.

No source, prefab, bundle, Git state, or existing project documentation was modified. No builds, tests, game launches, asset exports, or Project uploads were performed. Source/asset verification and runtime verification are distinguished throughout.

## A. Executive summary

Vanilla ONI combines serialized screen hierarchies, configured reusable templates, shared component classes, shared sprites/styles, and small screen-specific controllers. It does not build every menu through a universal window factory. Reuse should happen at the smallest useful boundary: an intact modal shell, complete control, style resource, or authored row. Matching appearance does not prove common original prefab ancestry.

The authoritative Multiplayer working implementation already follows several strong native patterns: intact InfoDialogScreen shells, authored Shared content, native KTabMenu, native input/button templates, content caching within each dialog, and domain controllers supplying data and actions. Preserve that structure. The main improvements are focused:

1. Complete native **section** styling in Host. Join uses Graphics section donors; Host still copies an outer-window background and its packaged prefab has three custom Outline effects.
2. Keep native AddOption footer ownership. Remove unused footer-hierarchy discovery and exposure from the dependency cache.
3. Retain focused-input protection, base-only Pause ownership, captured camera restoration and scene-change cleanup. Native source proves why those exceptions exist.
4. Use authored viewport/content/scrollbar structures and focused rows. Current Multiplayer lists have native scrolling but no serialized scrollbars; current lobby rows are display-only strings.
5. Validate version-sensitive donor references at the existing dependency boundary, without creating a discovery framework.
6. Localize captions independently of native styling: cloning LocText does not translate newly assigned English literals.

**Critical lifecycle finding:** KScreen.Show(false) preserves the object. KScreen.Deactivate invokes deactivation, removes the screen from the stack and schedules destruction. Cache content inside surviving dialogs; recreate dismissed InfoDialogs.

**Baseline:** authoritative Mac branch `feature/transport`, HEAD `2cf94cbd808686ad9c39c59fcb5d1b7c31d90c55`, plus staged and unstaged work. Compared UI sources and the Windows bundle match Mac working files by SHA-256. Normal Mac development assemblies also match installed Windows assemblies. These checks establish the inspected items, not the entire Windows snapshot or deployed mod.

The user reports prior runtime testing of stacked dialogs. That remains user-reported evidence for the exercised design. This investigation did not rerun it, establish its exact artifact identities, or verify subsequent visual changes. October 25 architecture/status entries still say runtime verification is pending; reconcile their historical scope later rather than silently treating all changes as tested.

## B. Vanilla UI architecture

### B1. Responsibility boundaries

| Type/resource | Verified responsibility | Consequence |
|---|---|---|
| KMonoBehaviour | Klei initialization, prefab-init/spawn, enable/disable hooks, Klei events, normal/forced cleanup | Initialization, spawn, activation and visibility are different stages. Initialize before depending on Klei events. |
| KScreen | Screen registration, input/scroll dispatch, editing state, show/hide, deactivation/destruction | Authored content can be a KScreen without another modal owner. Visibility does not require an extra input-stack entry. |
| KScreenManager | Instantiation helpers, logical stack, sort keys, focus updates, keyboard dispatch | Logical input order and Unity draw hierarchy are separate. |
| GameScreenManager | Selects configured world-space, camera, overlay, hover-text or screenshot canvas when parent is null | An explicit parent bypasses automatic render-target parenting. |
| KModalScreen | Runtime backdrop, modal sort key 100, scroll consumption, pause bookkeeping, camera disabling, resize listener, Close event, modal keys | Modality has real game side effects, not just a dark image. Camera disabling is not a reference-counted stack. |
| KModalButtonMenu | KButtonMenu plus modality, panel hide/unhide and logical childDialog | A logical child can be a Transform sibling. Hide scales a panel to zero rather than dismissing the menu. |
| InfoDialogScreen | Private serialized content/footer templates and containers, fluent content/options API, title, dismissal policy | Reuse intact shell and APIs; it does not supply tabs, selectable lobbies, session navigation or header Close. |
| ScreenPrefabs | Configured native screen/template references | Prefer configured donors over searches of visible live screens. |
| Assets.UIPrefabs.TableScreenWidgets | Explicit shared control templates consumed by table columns | Reuse a configured widget without adopting its gameplay table controller. |

Inheritance: KScreen derives from KMonoBehaviour (which derives from MonoBehaviour); KModalScreen derives from KScreen; InfoDialogScreen derives from KModalScreen. KModalButtonMenu derives from KButtonMenu, which derives from KScreen: it is a separate branch, not a KModalScreen subclass.

### B2. Instantiation and initialization

Util.KInstantiateUI(prefab,parent,force_active) instantiates with the requested parent and local transform semantics. The generic overload returns the requested component. **force_active=false does not force inactivity**; it leaves activation to the prefab state and initialization. Multiplayer bundle roots are serialized inactive. InfoDialog explicitly disables itself in OnPrefabInit.

KScreenManager.AddChild delegates to this helper. InstantiateScreen creates without explicitly activating; StartScreen creates then Activate; ActivateScreen reparents an existing object and activates it. GameScreenManager chooses its configured canvas only when no explicit parent is supplied.

KScreen.Activate sets active, pushes the screen, invokes OnActivate and records its active flag. OnSpawn can auto-activate when activateOnSpawn is set and the screen is not already active. Thus native examples using Activate and those setting a modal active rely on different initialization details; neither is a universal recipe.

InfoDialog.AddUI forces its content clone active. Whether Awake/Start run immediately also depends on the containing hierarchy. Configure typed Shared content before presenting the completed dialog and tolerate initialization ordering. Current MultiplayerTransportContent restores native tab selection in OnSpawn because child initialization can reset artwork.

### B3. Stack, rendering and modal input

PushScreen appends; RefreshStack filters destroyed references and orders ascending by GetSortKey. Input iterates from the end. LINQ OrderBy preserves relative order for equal keys, so later native dialogs at key 100 receive input first. This does not itself reorder Unity sibling rendering or canvases.

KScreenManager.Update calls active screens' ScreenUpdate(topLevel) from top to bottom and changes the focus flag after a modal. **Keyboard dispatch stops on consumed input, not simply on IsModal.** Modal overrides perform consumption. The backdrop independently intercepts pointer raycasts.

KModalScreen.MakeScreenModal creates a runtime background Image, ignores layout, enables raycasts and scroll consumption, stretches it to its screen root and makes it first sibling. It also enables activate-on-spawn. It is not evidence of a serialized reusable backdrop prefab. Coverage follows the screen root; a small custom root does not automatically create a full-canvas blocker.

ConfirmDialogScreen.PopupConfirmDialog climbs toward a Canvas parent and calls SetAsLastSibling. InfoDialog does not perform the same automatic reparenting. Do not assume every native dialog fixes its own rendering parent.

### B4. Visibility and cleanup contracts

| Operation/hook | Verified behavior | Rule |
|---|---|---|
| KScreen.Show(false) | Sets GameObject inactive, invokes OnShow(false); no direct stack pop/destruction | Suitable for cached authored content; disabling a modal still has side effects. |
| KScreen.Deactivate | In play mode invokes OnDeactivate, clears active flag, pops stack, schedules Unity Destroy | Dismissed instance is terminal; destruction is not necessarily synchronous. |
| KScreen.OnCleanUp | Deactivates if still logically active | Direct Destroy can still cause logical cleanup. |
| KMonoBehaviour.OnDisable | Suppresses OnCmpDisable during exit/scene loading | Modal Close is not guaranteed during transitions. |
| KMonoBehaviour.OnDestroy | Calls OnForcedCleanUp first, then branches for exit/loading; normal cleanup is not universal | Essential listener release belongs in forced cleanup or explicit owner disposal. |
| InfoDialog.OnShow(false) | Invokes onDeactivateFn | Callback also fires for explicit hiding, not just user dismissal. |
| KModalScreen.OnCmpDisable | Releases camera boolean, triggers Close event 476357528, removes resize listener | Does not count surviving outer modals; restore real outer ownership deliberately. |
| KModalButtonMenu.ActivateChildScreen | Instantiates sibling, stores childDialog, subscribes Close, scales panel to zero | Logical ownership and Transform parenting are distinct. |

Available consumer callbacks include InfoDialog.onDeactivateFn; per-option actions; AddOption's button/text handles; ConfirmDialog confirm/cancel/third actions and onDeactivateCB; Klei Close subscriptions; tab activation callbacks; and control events. Subclass hooks include prefab-init/spawn/activate/show/deactivate/enable/disable/cleanup/forced-cleanup. Their meanings and ordering differ.

Safe caches: configured templates while their providers remain valid, prefabs from retained bundles, and hidden content within a surviving scene-bound dialog. Reacquire scene-bound providers if their lifetime changes. Do not preserve live dialogs globally across scenes, mutate shared templates, or turn UI cleanup into disconnect.

### B5. Escape and Back

KScreen checks editing in its base handler; InfoDialog handles Escape **before** reaching it. With escapeCloses=true, Escape dismisses. With false, it consumes Escape and returns before base handling; other unconsumed keys are not automatically blocked by that early return. Enabled right-click dismissal also consults PlayerController.ConsumeIfNotDragging. AddDefaultCancel enables Escape; AddDefaultOK defaults to disabling it. Configuration order changes the final policy.

KModalScreen handles pause/speed keys, Escape and optional right-click, calls base and consumes input. Options/Audio have their own overrides. New-game flow exposes forward/back callbacks. Load uses Escape to return from a colony's saves to the colony list before ordinary dismissal. Gallery/Closet often hide existing objects instead of destroying them.

Keep Multiplayer's editing guard before native modal handling. The dedicated shell now invokes it directly; the former InfoDialog-specific prefix is obsolete. Keep Host dismissal separate from stopping a network operation.

KTabMenu.AddTab registers a page and creates a header item from its configured template. ActivateTab updates header artwork, toggles page GameObjects, updates a body ScrollRect's content when present, then invokes onTabActivated(current,previous) before storing the new previous index. This is visibility selection, not dialog navigation. KTabMenu.OnDeactivate calls Deactivate on registered pages, so it is destructive to those pages; cached transport content must continue using Show for visibility changes. Header/template ownership stays inside the tab UI component.

## C. Native screen reference catalog

IDs here are PathIDs in installed Windows sharedassets0.assets, not Unity authoring GUIDs. Exact controller IDs and source links are indexed in H. Each entry combines source and serialized inspection; none certifies current rendered appearance.

| Requested screen / actual class | Prefab and hierarchy | Initialization, header/content/footer | Input, cleanup and pattern to adopt |
|---|---|---|---|
| **Game Mode:** ModeSelectScreen : NewGameFlowScreen : KModalScreen | ScreenPrefabs.ModeSelectScreen; root 9814; Panel/Header/{Label,CloseButton}, Content/{RootToggles,Description} | OnPrefabInit loads settings once; OnSpawn wires two authored MultiToggle cards through HierarchyReferences. Cards own selection/header/animation references. Close navigates backward; click sets mode defaults and navigates forward. No AddOption footer | NewGameFlow callbacks own navigation; right-click invokes backward navigation, remaining modal handling inherited. Click deactivates this screen. Adopt authored card + focused state methods, not its animated/domain-heavy tree |
| **Load Game:** LoadScreen : KModalScreen | ScreenPrefabs.LoadScreen; root 9080; Panel/Title; ColonyList and ColonyView, each with ListView/Files, viewport/content/bar; Migration panel | OnPrefabInit creates UIPool<HierarchyReferences> from saveButtonPrefab (component 22815 on GO 8753); OnActivate refreshes. ShowColony/ShowColonyList choose authored panels; UpdateSelected updates row selection. Actions belong to rows/detail views | Close deactivates; Escape returns to colony list first. OnDeactivate clears selected save and invokes native modal deactivation. Adopt authored list/detail ownership, reusable rows, bounded refresh. No universal empty-state widget was established |
| **Save Information:** InfoDialogScreen : KModalScreen | Standard InfoDialog root 8884/controller 20753, parented under LoadScreen | ShowSaveInfo configures header, sprite, body, save-folder action, Cancel, conditional cloud-folder action; sets active. No separate screen class or prefab in this path. No header Close | AddDefaultCancel enables Escape; dismissal destroys. Cached Unity reference permits recreation after destruction. Folder actions do not dismiss unless explicitly coded. Adopt native content/options API |
| **Convert to Local Saves:** InfoDialogScreen | Same standard InfoDialog, parent LoadScreen | ConvertAllToLocal -> ConfirmCloudSaveMigrations adds header/sprite/text/Cancel/right-side confirm; action deactivates before invoking commit callback. Backup text/action parameters are unused in inspected helper | Native Escape and destruction. This is not a separate conversion-screen framework; other Load confirmations use ConfirmDialogScreen. Adopt configure-existing-dialog pattern, not a navigation-stack inference |
| **Colony/Achievements Gallery:** RetiredColonyInfoScreen : KModalScreen | ScreenPrefabs.RetiredColonyInfoScreen; root 9991; HeaderBar, ColonyData, colonies/achievements searches and scrolls, colony/stat/media templates | OnPrefabInit configures buttons, explorer, achievements and scale subscription. ComputeSizeStatGrid/ExplorerGrid adjust columns after layout. Contextual Return/Close/Quit controls belong to screen | Close/Escape/right-click normally Show(false); confirm temporarily disables and opens another screen. OnCmpEnable/Disable captures/restores canvas pixelPerfect. Adopt owned content/entries and deliberate layout completion; avoid importing statistics/history systems |
| **Supply Closet:** LockerMenuScreen : KModalScreen; LockerNavigator : KModalScreen; OutfitBrowserScreen : KMonoBehaviour | Roots 11234/10194/10200. Menu Header/cards/description/logos; navigator ActiveScreen and BottomBar, authored browser/designer children | Menu OnPrefabInit wires MultiToggle cards/Close; Show owns music/account status. Navigator PushScreen/PopScreen manages logical history. OutfitBrowser creates a local prefab pool/GridLayouter and refreshes items | Special sort keys 40/41; show/hide caches and history callbacks differ from InfoDialog lifetime. Also owns textures/inventory/music/account dependencies. Adopt content/entry boundaries, not its full navigation system |
| **Translations:** LanguageOptionsScreen : KModalScreen, SteamUGCService.IClient | ScreenPrefabs.languageOptionsScreen; root 8983, LanguageOptionsMenu; BG/TitleBar/Buttons and ScrollWindow/Viewport/Content/grids/bar; languageButtonPrefab GO 8805 | OnSpawn wires Back/Close/Workshop/Uninstall and rebuilds. Rebuild destroys prior entries then clones/configures title/image through HierarchyReferences | OnActivate registers UGC client; OnDeactivate unregisters. Back/Close deactivate; right-click override plus modal Escape. Confirmation can use alternate locale/font. Adopt grid/entry cleanup and localization. Serialized UGC container points to preinstalled container despite separate UGCContent node: do not infer intent from names |
| **Mods:** ModsScreen : KModalScreen | ScreenPrefabs.modsMenu; root 10044; Panel/{Title,ListView,DetailsView}; Files mask/scroll/content/bar; Entry GO 10015 | OnActivate sets title/actions, builds display and subscribes mod-manager update; BuildDisplay destroys/recreates entries. Entry includes MultiToggle, HierarchyReferences, DragMe, title/version, EnabledToggle, ManageButton. Footer is authored DetailsView | Header/footer Close call Exit, which saves/checks footprint and may request restart. OnDeactivate removes update subscription. Modal keyboard handling is inherited; do not assume every Escape invokes button Exit logic. Adopt compact frame/list/footer, not mod-management row behavior |
| **Options:** OptionsMenuScreen : KModalButtonMenu | ScreenPrefabs.OptionsScreen; root 8980, OptionsMenu; CenterContent/Content/{BG,Buttons,BottomButtons}; configured buttonPrefab GO 8500 | OnPrefabInit supplies button definitions/Close/Back; OnSpawn sets title. OnGraphics/Audio/Game/Metrics/Feedback/Credits use ActivateChildScreen | Transform siblings + separate childDialog/Close subscription and panel scaling. Escape/right-click deactivate. Adopt this ownership distinction for Pause integration, with only base Multiplayer dialog owning Pause |
| **Graphics:** GraphicsOptionsScreen : KModalScreen (internal vanilla type) | OptionsScreen.graphicsOptionsScreenPrefab; root/controller 8830/20267; Content/BG, TitleBar, DisplaySettings, color controls, BottomButtons | OnSpawn populates resolution/color options, MultiToggle, KSlider and Apply/Done. OnApply/ApplyConfirmSettings use timed confirmation and can revert. Section BG/header are focused donors | Close/Done call OnDone (direct Destroy); Escape/right-click hide resolution dropdown and Deactivate; KScreen cleanup handles active destroyed screen. Adopt complete Close, section Image/heading, authored form groups; never its settings controller |
| **Audio:** AudioOptionsScreen : KModalScreen | OptionsScreen.audioOptionsScreenPrefab; root 8858, AudioOptions; Content/{BG,Sliders,Buttons,BottomButtons} | OnSpawn creates UIPool<SliderContainer>; each row has name/KSlider/value. SliderContainer formats values. Checkbox-style rows use HierarchyReferences + KButton + CheckMark. Installed deviceDropdown is null despite device methods in source | Close/Done OnClose records metrics and Destroy; Escape/right-click Deactivate. Adopt repeated labelled control row, not mixer/metrics callbacks |
| **Game:** GameOptionsScreen : KModalButtonMenu | OptionsScreen.gameOptionsScreenPrefab; root 9375; TitleBar, Content/Row/{Buttons,TemperatureUnits}, SaveOptions, BottomButtons | OnSpawn initializes nested unit/save helpers and controls. OnShow changes save-dependent content; domain state controls availability. Controls navigation uses native child-screen ownership | Close/Done deactivate; own key override plus inherited menu modality. Adopt authored multi-column grouping and focused configuration helpers; retain authoritative domain validation |
| **Data Communication:** MetricsOptionsScreen : KModalScreen | OptionsScreen.metricsScreenPrefab; root 8982, MetricsOptionsMenu; Content/BG, text/checkbox Buttons group, account indent, BottomButtons | OnSpawn binds controls; OnClickToggle/Refresh control mark, warning, Close interactability and Done/Restart caption; dirty state decides action | Clean Escape/right-click hides; inherited behavior depends on consumed events. Close/Done have state-dependent semantics. Adopt explanatory group + explicit availability render; omit privacy/restart/account dependencies |
| **Feedback:** FeedbackScreen : KModalScreen | OptionsScreen.feedbackScreenPrefab; root/controller 8981/22102, FeedbackMenu; BG/TitleBar, InfoBox, LinksGroup/BrowseGroup, BottomButtons | OnSpawn sets title and binds Close/Done, forums and folder actions; Steam Deck hides unsupported folder actions and changes group padding | Close/Done deactivate; modal input/cleanup inherited. Adopt mixed descriptive/action panels and owner-controlled platform visibility. Found through prefab reference, not class-name GameObject search |
| **InfoDialog:** InfoDialogScreen : KModalScreen | ScreenPrefabs.InfoDialogScreen; root/controller 8884/20753; Layout/{Header/Label,Content,Buttons/{Left,Spacer,Right}} | Private referenced body/subheader/line/sprite templates; native option panels/templates. Header inspection reconfirms only a label, no Close child | Native modal lifecycle + onDeactivateFn on OnShow(false). Use AddUI and AddOption for composition; full API follows |

Important screen-specific details: RetiredColonyInfoScreen uses two grid columns below 1920 screen width and three otherwise, after frame/layout completion; this is not a universal ONI responsive rule. Supply Closet's cached lifetime also is not a template for caching dismissed InfoDialogs. Load's GetColoniesDetails handles empty input, but that alone does not establish a reusable visual empty-state implementation.

## D. Reusable control and style catalog

### D1. Reuse mechanisms

| Element | Source / serialized identifier | Reuse and mod applicability | Dependencies / limits |
|---|---|---|---|
| Dialog shell | ScreenPrefabs.InfoDialogScreen, 8884/20753 | Clone intact; configure supported API; already adopted | Camera/pause/input/Close lifecycle; no header Close or complete form-layout system |
| Title bars | Graphics Content/TitleBar GO 8568/Image 20572; Mods Panel/Title GO 9851 | Screen-authored bars sharing style resources | No original common title-bar prefab/variant ancestry established; retain native shell |
| Header Close | Graphics closeButton KButton 21019/GO 8775; KImage 21219, ToolTip, LayoutElement, child icon | Clone complete control once per dialog; clear donor actions and bind native dismissal | Publicized private donor field; preserve image/style/sound dependencies and geometry; no ancestry claim |
| Footer | InfoDialog Left GO 10476, Spacer 11460, Right 10850; AddOption | Native templates/panels remain owner; Shared may retain handles to its own actions | No supported remove/clear/context-switch API; do not reparent whole footer |
| Standard buttons | Left GO 8942/right GO 9195; KButton, child KImage, LocText, layout/text style | Clone configured template or AddOption; use isInteractable | Getter names do not establish action priority; preserve hover/sound/disabled behavior; reset unwanted listeners |
| Group backgrounds | Graphics DisplaySettings/BG GO 9581/Image 23027; Audio Sliders/BG Image 21919 | Copy sprite/type/material/color onto authored Image in Shared | Style reuse does not copy layout or make a common panel prefab; hierarchy/padding remain authored |
| Section headings | Graphics DisplaySettings/Header GO 9641/LocText 22784 | Clone styled text into authored slot | 24-point donor has 32-unit min-height reference; allow localization/wrapping |
| Body/subheader | GetPlainTextPrefab component 20436/GO 8559; GetSubHeaderPrefab component 20708/GO 8856 | Native content API or focused template clone | Subheader includes background/container; it is not the same thing as a plain section heading |
| Text input | LockerNavigator.outfitBrowserScreen.inputFieldPrefab GO 10570 | Complete KInputTextField/Image/LayoutElement/Text Area RectMask2D/TMP children clone | Configure type/limit/value/placeholder and reset donor events/validation; do not add another input KScreen |
| Alternate inputs | FileNameDialog.inputField; ColonyDestinationSelectScreen settings seed Input | Configured donor paths inspected in current native cache | Filename/seed rules differ; examine callbacks/content type before use |
| Dropdowns | Graphics resolution Dropdown component 22004/GO 9741; Assets.UIPrefabs.TableScreenWidgets.NumericDropDown | Preserve complete configured label/template/list/viewport; supply options/callbacks | Graphics uses Unity UI Dropdown; table uses TMP_Dropdown. Different families are not interchangeable. Table template source-verified; no measured table-template PathID claimed |
| Checkbox/toggle | Graphics fullscreen MultiToggle; Mods EnabledToggle; Audio/Metrics KButton+CheckMark; table Checkbox | Choose complete control/contract; render checked state explicitly | MultiToggle, KToggle and Unity Toggle differ; matching appearance does not prove same control class |
| Sliders | Audio sliderPrefab component 20494, SliderContainer GO 8716, KSlider + labels | Clone row or author focused Shared row around native slider | SliderContainer already formats values; screen supplies mixer behavior separately |
| Tabs | KTabMenu/KTabMenuHeader, KToggle, TabHeaderIcon, ImageToggleState, ToggleGroup | Register authored pages through native menu; already adopted | Header overloads need different templates: text-only uses Unity Text/KButton; sprite/title uses TMP/TabHeaderIcon/KToggle. Buttons alone are not native tabs |
| Scroll lists | Load/Mods KScrollRect + mask/viewport/content/bar; Gallery grids | Author small container and row; Shared populates | Native controllers contain domain behavior; no universal selectable lobby API; Multiplayer currently has no bars |
| Cards | ModeSelect/LockerMenu MultiToggle + HierarchyReferences | Adopt authored card/selection pattern when useful | Whole cards have animation/inventory dependencies; common original prefab lineage unknown |
| Backdrop | KModalScreen.MakeScreenModal runtime background | Keep one native modal owner | Not established as serialized prefab; root coverage, input and camera/pause all matter |
| Tooltips | ToolTip; InfoScreenLineItem.SetTooltip | Reuse component/API | Keep localization/hover target; no new tooltip framework needed |

**Established:** explicit reusable configured template references, repeated component classes, exact shared style/sprite object identity, runtime instantiation, independently configured screen hierarchies.

**Unresolved:** original Klei nested-prefab and prefab-variant relationships. AssetRipper's PrefabHierarchyObject/Generated Prefabs collections are reconstructions, not proof of original authoring history. Flattened player objects cannot establish missing ancestry. Matching resource IDs prove resource reuse only. None of the recommendations depends on recovering that ancestry.

### D2. Full InfoDialog supported API

| API | Verified behavior |
|---|---|
| SetHeader(string) | Sets header LocText, returns dialog; adds neither Close nor dimensions |
| AddUI<T>(T prefab,out T spawn), T : MonoBehaviour | Clones arbitrary authored content under contentContainer, force-active true; returns typed component. Direct Shared integration point; no domain wiring or additional modality supplied |
| AddOption(string,Action<InfoDialogScreen>,bool rightSide=false) | Clones side-specific template into its native panel, sets caption, binds callback receiving dialog; no automatic dismissal |
| AddOption(bool,out KButton,out LocText) | Creates native option and exposes that option's handles; suitable for fixed Shared-owned visibility/interactability/listener cleanup |
| AddDefaultCancel() | Adds localized Cancel on the **left**, bound to Deactivate; enables Escape. Current Multiplayer intentionally creates right-side Cancel explicitly |
| AddDefaultOK(bool escapeCloses=false) | Adds localized OK on the **right**, bound to Deactivate; sets Escape policy, default false |
| AddSubHeader(string) | Clones configured InfoScreenPlainText composite subheader and sets text |
| AddPlainText(string) | Clones body template and sets text |
| AddLineItem(string,string) | Clones line item, sets label and tooltip |
| AddDescriptors(List<Descriptor>) | Adds line items from descriptors' indented text and tooltips |
| AddSprite(Sprite) | Clones sprite item; preferred width derives from sprite aspect ratio and existing preferred height |
| AddSpacer(float height) | Creates runtime LayoutElement with equal min/preferred height and zero flexible height; a native small procedural-layout helper |
| GetSubHeaderPrefab/GetPlainTextPrefab/GetLineItemPrefab | Return focused content templates |
| GetPrimaryButtonPrefab/GetSecondaryButtonPrefab | Return left/right templates respectively; not a universal visual priority taxonomy |
| Activate/Show/Deactivate | Inherited lifecycle; hiding calls deactivation callback, dismissal schedules destruction |
| onDeactivateFn | Public action invoked from OnShow(false), not a confirmation result |

Configuration handles information, sprites, descriptors, authored forms, fixed/enabled actions and helper-controlled Escape policy. It does not supply header Close, page/session navigation, selectable lobby identity, automatic data binding, responsive layout, per-dialog camera restoration, footer clearing or scene-owner disposal.

ConfigureDialog currently accesses publicized private header/escapeCloses fields. Those are compatibility dependencies, not supported vanilla public API. Keep this small exception inside Shared/native integration. It clones Graphics Close, clears clicks, binds Deactivate, reserves title space, creates right-side Cancel and enables Escape. This is compatible with native small-control composition; it does not prove common Close ancestry or justify stripping another entire screen.

## E. Verified design measurements

### E1. Measurement conventions

Values are serialized Unity UI units/layout constraints, not guaranteed physical pixels after Canvas/game scaling. Minimum/preferred sizes are identified. Sprite border and texture dimensions are not measured rendered corner radii. Actual appearance was not visually inspected in-game in this task.

### E2. Layout, frame and control values

| Native reference | Verified values |
|---|---|
| InfoDialog Layout GO 9092 | Padding L/R/T/B **16/16/16/16**, spacing **12**; min width **520**; ContentSizeFitter horizontal MinSize, vertical PreferredSize |
| InfoDialog Header GO 8633 | Top/stretch-X anchors, sizeDelta Y **24**, anchored Y **24**, top pivot, ignoreLayout true; only Label child |
| InfoDialog Content GO 8604 | Padding **2/2/0/0**, spacing **3** |
| InfoDialog Buttons GO 9488 | Horizontal spacing **5**, top padding **16**; side groups spacing **0**, flexible spacer between |
| Left/right templates GO 8942/9195 | Min **128×48**; internal horizontal padding **16/16**; not fixed final width |
| Graphics TitleBar GO 8568 | SizeDelta Y **24**, anchored Y **24**, top/stretch-X; min height **24** |
| Graphics Close GO 8775 | SizeDelta/min **24×24**, right-center anchors/pivot, position **0,0** |
| Mods Title/Close GO 9851/9496 | Title preferred height **24**; Close sizeDelta/min **24×24**, top-right anchored |
| ModeSelect Header/Close | Header preferred **24**, min **22**; Close **22×22**, top-right anchors, position **(-1,-12)** and right-center pivot. 24 is common, not universal |
| Graphics Content GO 9121 | Padding **20/20/10/10**, spacing **20** |
| Graphics DisplaySettings GO 9721 | Inner padding **12** all sides, spacing **10**, min width **504** |
| Graphics section Header GO 9641 | Font **24**, min height **32**, left/middle alignment |
| Audio Content/Sliders | Outer padding **20/20/10/10**, spacing **20**; inner **12** all sides, spacing **10** |
| Audio SliderContainer GO 8716 | Min **480×32**; name min/preferred **72×32**, slider min **350×32**, value min **36×32**; horizontal spacing **10** |
| Graphics resolution Dropdown GO 9741 | Min **190×30**, flexible width **1** |
| Game Options | Column Row spacing **20**; Buttons/TemperatureUnits padding **12**, spacing **12**; SaveOptions padding/spacing **12** |
| Metrics Buttons GO 8878 | Padding **16/0/10/20**, spacing **4** |
| Feedback InfoBox GO 8877 | Padding **16/16/10/20**, spacing **20**; action-group padding **15**, spacing **10**; between groups **15** |
| ConfirmDialog content GO 8614 | Padding **32/32/40/16**, spacing **10**, min width **400**; action buttons min **375×48**; action container top padding **8** |
| Outfit input GO 10570 | Min **200×28**, flexible width **1** |
| ModeSelect cards | Min **165×260**, row spacing **30**, top/bottom padding **46** |
| Language preinstalled grid | Cell **200×200**, gap **10×10**, fixed columns **3**, top/bottom padding **10** |
| Gallery colony grid GO 9863 | Cell **420×334**, gap **24×24**, top/bottom padding **24**; columns changed at runtime |
| Gallery statistics grid GO 9322 | Cell **420×548**, gap **24×48**, bottom padding **24**; columns changed at runtime |

### E3. Sprite, color and typography resources

IDs are in sharedassets0.assets; matching references establish shared resources only.

| Resource | Exact serialized value |
|---|---|
| **8204 web_rounded** | Rect **12×12**, border **5/5/5/5**; sliced section Images in Graphics/Audio/Feedback/Metrics |
| **8324 web_box** | Rect **16×16**, border **2/2/2/2**; sliced frame/header Images |
| **7179 web_button** | Rect **16×16**, border vector X/Y/Z/W **4/6/6/4** |
| **7314 cancel** | Graphics-style Close glyph |
| Graphics section Image 23027 / Audio 21919 | RGBA **(0.18823531,0.20392159,0.2627451,1)**; Game group Images differ only in last float digits |
| InfoDialog frame Image 20954 | RGBA **(0.1225525,0.13367221,0.16981131,1)** |
| InfoDialog/Graphics header Images 20454/20572 | RGBA **(0.49803922,0.23921569,0.36862746,1)** |
| **20007 colorstyle_button** | Inactive **(0.24313726,0.2627451,0.34117648,1)**; hover **(0.34612888,0.37396193,0.4852941,1)**; active **(0.5033521,0.54444194,0.6985294,1)**; disabled **(0.41568628,0.4117647,0.4,1)** |
| **20008 colorstyle_buttonPink** | Inactive **(0.5294118,0.27249137,0.4009516,1)**; hover **(0.61764705,0.33153114,0.4745891,1)**; active **(0.7941176,0.4496107,0.6242238,1)**; disabled **(0.41568628,0.4117647,0.4,1)** |
| **20040 style_bodyTextWhite** | Font **20151 NotoSans-Regular**, size **14**, white, wrapping enabled |
| **20043 style_buttontitle** | Font **20032 GRAYSTROKE REGULAR SDF**, size **14**, white, wrapping enabled |
| **20047 style_titleText** | GRAYSTROKE, size **14**, white, wrapping enabled |
| Graphics/Audio section heading | Direct LocText settings: font 20032, size **24**, white; no TextStyleSetting reference on measured section labels |
| Modal backdrop | Source-created Color32 **(0,0,0,160)**, raycast target true, stretched under modal root |

InfoDialog's “primary” getter returns the neutral **left** template; “secondary” returns the pink **right** template. Default OK uses the right. Choose side/style/action priority explicitly.

KButton.isInteractable invokes native color updating; KImage style assets provide interaction states. Copying an Image color alone loses that behavior. LocText.ApplySettings resolves a non-empty localization key and applies text style; Awake performs localization fixup/RTL setup and subscribes input changes. SetTextStyleSetting.ApplyStyle applies font/size/wrapping/color/style, so raw serialized fontSize may be superseded. A cloned LocText with its key cleared and an English literal is not automatically localized.

### E4. Lists and current Multiplayer differences

| Container | Verified structure/settings |
|---|---|
| Mods Files GO 9475/KScrollRect 22470 | Mask on Files; content rect 15972; vertical-only, Elastic (1); scrollbar 21905, permanent visibility (0). Serialized viewport is null despite named Viewport child |
| Mods Content GO 9752 | Spacing **10**, padding **10/5/10/0** |
| Load colony Files GO 9355/KScrollRect 22892 | Viewport 14551/content 14733/bar 20754; vertical-only, Elastic; content padding **0/16/6/6**, spacing **9** |
| Load saves Files GO 10501/KScrollRect 24378 | Viewport 16943/content 16189/bar 23605; content padding **12/20/4/4**, spacing **4** |
| Language ScrollWindow GO 8823/KScrollRect 20439 | Explicit viewport/content/bar; AutoHideAndExpandViewport (2); content spacing **10** |
| Multiplayer lobby component -7516559938135865402 | Explicit viewport/content, vertical-only, Clamped (2), sensitivity **24**; both scrollbar references null |
| Multiplayer player component -7478697207130653002 | Explicit viewport/content, Clamped, sensitivity **24**; both scrollbar references null |
| Current SessionPlayerRow | Root -1383524195162126650; row/text-slot min height **38**; display-only component and native text clone |
| Current Join section-title slots | PublicLobbies/Title and JoinByCode/Title min/preferred height **24**; native donor font **24**, native donor min height **32**. Verified mismatch; clipping/wrapping risk, not observed rendered failure |
| Current transport tabs | Authored header template min/preferred **124×32**, navigation height **32**; Multiplayer choices, not universal measured vanilla tab sizes |

Native uses pooling (Load, Audio, OutfitBrowser), full rebuilds (Mods, Languages), and targeted state refresh. Multiplayer already reuses rows by index, trims excess, preserves scroll position, and skips unchanged player data. That is reasonable for small lists; add pooling/virtualization only for a demonstrated workload or interaction requirement.

## F. Multiplayer architecture comparison

### F1. Historical inspected structure (superseded by 2026-10-29 checkpoint)

The earlier Mac working files were inspected directly, then the corresponding Windows files were hash-verified. The following is that historical structure, not the current implementation:

```text
Existing frontend / Pause parent
  Join InfoDialog [base coordinator owns Pause + scene subscription]
    cached Join content OR Session content
  Host InfoDialog [Transform sibling; logical child of base coordinator]
    cached Host content OR Session content
```

Open(origin) parents to origin.transform.parent; OpenHostSetup uses the base dialog's parent. Join survives beneath Host. Host success switches Host to Session; joining switches the base dialog. Closing top Session while still connected refreshes the base as Session. UI closing does not call StopHosting, CancelConnection or disconnect; those remain explicit domain operations.

MultiplayerMenuScreen is an ordinary coordinator, not a KScreen. Shared Host/Join derive from MultiplayerTransportContent : KTabMenu; Session derives from KScreen. Pages are authored KScreen objects with activateOnSpawn false. Show switches content; Deactivate is reserved for terminal dialog dismissal. One Global coroutine detects connection-state changes each frame and refreshes presentation on an unscaled 0.25-second cadence.

Shared owns private serialized controls, native binding/tabs, row/empty-state rendering, status/availability and listener cleanup. Host/Join/Session domain views supply data, dependencies and callbacks. Controllers validate ports, counts, names and session availability. Steam joining remains deliberately gated; visual work must not enable an unverified backend.

Only the base coordinator owns Pause.childDialog. Native Close and captured camera state restore ownership; scene-change/open-failure paths explicitly dispose. This agrees with native lifecycle evidence and is not redundant merely because it uses several state flags.

### F2. What already fits native practice

- Intact modal shell, direct AddUI, native AddOption ownership and complete Close donor clone.
- Private Shared references and typed intent APIs; no content-descendant searching found in the inspected coordinator/domain views.
- Native tab registration/selection/artwork and focus termination when transport changes.
- Scene-bound dialog lifetime and cached hidden content; forced cleanup releases UI listeners.
- Domain validation separate from immediate feedback; UI closure separate from session operations.
- Native input/button/text templates cloned without modifying shared donors.
- Shared-owned rows and authored scroll containers, rather than controller-created layouts.

### F3. Concrete simplifications and ambiguities

| Finding | Verified evidence / implication | Proposed response |
|---|---|---|
| Unused footer discovery | ModAssets.NativeUI exposes Footer/FooterLeft/FooterRight and finds common ancestor; repository C# search found no consumers outside construction/declaration | Remove unused block after a fresh reference search; preserve AddOption. Reduces hierarchy coupling with no replacement abstraction |
| Host/Join section mismatch | HostContent.Initialize calls Frame (Options outer-window donor) and uses subheader text; Join uses SectionFrames/SectionTitle from Graphics. Host bundle contains three Outlines | Adopt section donor for Host; review/remove redundant outlines and keep padding authored |
| Heading constraints differ | Join title slots 24 high vs native section donor font 24/min height 32 | Adjust authored constraints after visual verification; no controller repositioning |
| Version-sensitive dependencies | Exact Options/Graphics hierarchy paths and publicized private fields | Validate needed references/types at acquisition and provide useful controlled failure; no scattered additional searches |
| Small native API exception | ConfigureDialog accesses publicized header and escapeCloses | Retain encapsulated Shared exception and document compatibility; no wrapper framework needed |
| Footer options outlive content visibility | Buttons are dialog children, while content retains handles; OnShow hides, forced cleanup detaches | Preserve explicit ownership. Independently destroying a content object would leave its buttons; current cache-until-dialog-destruction is not a demonstrated leak |
| Loader failure policy | ModUI.Initialize catches/logs; detailed inactive/component checks are DEBUG-only; controllers later require prefabs | Consider explicit UI-unavailable/failure handling at existing loader boundary. This is a robustness proposal, not reproduced missing-asset failure |
| Lobby model is strings only | JoinView maps lobby data to strings; SessionPlayerRow exposes no lobby identity/selection/action | Keep display-only while joining gated; future approved selection needs focused row/data contract, not Mods/Load controllers |
| Localization incomplete | Donor keys cleared; many Shared captions and controller statuses are English literals | Supply localized strings through existing APIs; preserve plain/richText-disabled handling of player/server text |
| Modal pause patch is broad | Conditional Pause/Unpause in both KModalScreen and KModalButtonMenu OnShow, gated on Session.IsActive | Keep intentional simulation authority; verify menu/session transitions. It is not an InfoDialog-only effect |

Some class separation is useful: Host/Join perform different domain operations and Session actions differ from setup actions. MultiplayerTransportContent and MultiplayerControls already capture real repetition. Do not merge them just to reduce class count or add builders/services to replace small existing helpers.

### F4. Presentation ownership

**Unity:** hierarchies, section grouping, anchors, constraints, heading slots, tab template/artwork, masks, viewport/content/bar connections, row prefab, empty-state position and spacing. Editor authoring helpers prepare those artifacts; game-side runtime controllers should not recreate them.

**Shared:** instantiate native controls inside slots, copy native section styles, bind/clear events, render availability/selection/status, own rows/options and focus, expose intent APIs. Current code mostly follows this.

**ONI_United:** dependency acquisition, bundles/prefabs, top-level navigation and scene/Pause ownership, host/join/session operations, authoritative validation, data refresh and domain feedback. Updating a status string is not inherently unwanted presentation manipulation. Searching/repositioning Shared descendants would be.

Donor hierarchy lookups are centralized in ModAssets.NativeUI. They are integration/compatibility dependencies; treating all donor lookup as forbidden would prevent native reuse without identifying a better verified provider.

## G. Prioritized improvement plan

Every implementation item requires a separately authorized task. Verified mechanisms do not automatically approve a layout/product choice.

### G1. Recommendations by area

| Multiplayer area | Native pattern and affected code/prefab | Simplification, risks, evidence |
|---|---|---|
| Outer Join/Host | Keep InfoDialog + AddUI; MultiplayerMenuScreen.Open/OpenHostSetup | Already appropriate. Verify sibling drawing, input and stacked backdrops; native mechanism verified, latest appearance pending |
| Header/title/Close | Keep header; complete Graphics Close clone in ConfigureDialog | Preserves native style/sounds. Publicized fields/title reservation are version-sensitive; alignment pending. No ancestry reinvestigation needed |
| Public Lobbies | Load/Mods authored viewport/content/bar and focused reusable row; JoinView.prefab/JoinContent/eventual lobby model | Maintainable reuse without domain-heavy donor screen. Selection provisional and conditional on backend approval; preserve Steam gate |
| Join By Code | Graphics grouped labelled row + heading + KInputTextField/button; Join prefab/Shared content | Adjust heading constraints, preserve validation/focused Escape; no new form framework |
| LAN form | Audio repeated rows, Metrics explanatory text, Graphics grouping | Keep authored fields and Shared native binding; verify long hostnames/names/errors and wrapping; no networking changes |
| Steam/LAN tabs | Keep KTabMenu/KTabMenuHeader and authored KToggle/TabHeaderIcon template | Preserve initial selection, spawn artwork restoration, focus clearing, cached tab state. Current mechanism verified |
| Host settings | Graphics/Game section BG/headings; HostView.prefab/HostContent/authoring helper | Removes outer-frame treatment and redundant Outline effects; verify heading fit and platform bundles. Mismatch verified, final layout provisional |
| Session players | Load/Mods owner-controlled rows/container; SessionPlayerRow/SessionContent/SessionView | Keep targeted refresh/scroll position; optional authored scrollbar; avoid selection/pooling absent need |
| Footer | InfoDialog fixed AddOption + handles; ModAssets.NativeUI cleanup | Remove unused ancestor discovery; preserve Shared hide/show/listener lifetime. Directly verified simplification |
| Stacking | Load InfoDialogs plus Options logical ownership; existing coordinator | Preserve Join-under-Host/Session-in-place/base-only Pause. ConfirmDialog reparents differently. Native contracts verified; full current runtime matrix pending |

### G2. Small visual refinements

1. **Host section consistency:** use Graphics section Image/heading instead of outer-window style; review/remove three Outlines. **Acceptance:** intended native sprite/color, deliberate serialized padding, no doubled borders or donor mutation; both transports/player-limit content readable at supported scales; each shipped platform bundle verified. **Evidence:** current mismatch verified; final rendering provisional.
2. **Heading/text fit:** revisit 24-high Join slots against native 24-point/32-min-height donor. **Acceptance:** no clipping/overlap across supported scales/locales; long errors wrap without covering actions; constraints remain serialized. **Evidence:** mismatch verified, clipping unobserved.
3. **Scrollbar discoverability:** decide whether native vertical bars are appropriate in lobby/player lists. **Acceptance:** zero/one/many rows, wheel/drag, resize and empty state work; content avoids bar; references authored. **Evidence:** bars absent; product choice provisional.
4. **Header alignment:** verify current title reservation/Close against 24-unit native header. **Acceptance:** one Close per dialog, title avoids hit area, native hover/disabled style; Close/Cancel/Escape dismiss intended layer and never disconnect. **Evidence:** mechanism verified, latest visual result pending.

### G3. Shared UI improvements

5. **Localized captions via existing APIs.** **Acceptance:** donor keys cannot overwrite supplied captions; translations/fallback fonts fit; user text stays plain. **Evidence:** English literals verified.
6. **Validate donors at existing boundary.** **Acceptance:** missing field/path/type yields specific diagnostic and controlled failure, no orphan dialog/camera lock/Pause owner, no donor mutation. Avoid new framework. **Evidence:** fragile paths verified; failure design provisional.
7. **Extend only real repeated behavior.** **Acceptance:** typed intent APIs/private references, deterministic cleanup, no consumer descendant traversal; a future selectable lobby row carries stable identity and callbacks. **Evidence:** current row is display-only; future interaction provisional.

### G4. Architectural simplifications

8. **Remove unused footer ancestry discovery.** **Acceptance:** fresh search confirms no consumers; native AddOption and Gallery button-template samples remain intact; no footer adapters/reparenting introduced. **Evidence:** directly supported by current source search.
9. **Clarify terminal ownership without replacing coordinator.** **Acceptance:** setup→Session→setup has correct options; Show preserves content; dismissal disposes once; late callbacks cannot access destroyed UI; transition/open-failure releases actual side effects. **Evidence:** existing boundary verified; deleting code first requires demonstrating redundancy on those paths.
10. **Unify asset failure reporting within existing loader.** **Acceptance:** required content is available or Open fails cleanly; Release does not rely solely on DEBUG scans to avoid obscure later null references. **Evidence:** catch/log behavior verified, no reproduced failure.

### G5. Changes that should not be made

- Do not replace approved stacking for visual similarity or fewer classes.
- Do not add another modal/backdrop/input screen around InfoDialog content.
- Do not remove editing prefix because KScreen has an editing check; InfoDialog handles Escape first.
- Do not remove scene cleanup/captured camera/Pause restoration assuming native Close always fires.
- Do not equate Show(false), Deactivate, panel scaling and Destroy.
- Do not strip whole Mods/Graphics/Gallery/Closet trees to obtain simple controls.
- Do not expose descendant controls to domain views, rebuild layouts there, or mutate donors.
- Do not attach disconnect/stop to Close or unblock Steam as styling work.
- Do not introduce speculative UI frameworks, virtualization or persistent roots.
- Do not infer prefab ancestry or runtime success from reconstructed assets/build success.

### G6. Runtime acceptance for a later implementation

No scenarios were run here. Later verify frontend/Pause open; Join→Host→Back/Close with preserved Join fields/tabs; host success→Session; joining→base Session; close top Session while connected→base Session; close all UI without disconnect; explicit Stop/Cancel; session ends→setup; focused/end-edit-frame/unfocused Escape and right-click; repeat opens with one listener/action; late callback after close; scene transition/opening failure; camera/Pause restoration with another native menu present; empty/many/long-name rows and scroll position; supported scales/locales; independently packaged platforms. Record actual deployed source/bundle identities, not HEAD alone.

### G7. Potential documentation impacts

- UI_DESIGN_PRINCIPLES: clarify publicized-field exceptions, style-vs-layout reuse, measured heading constraints, footer-option lifetime; retain ownership rule.
- ARCHITECTURE: refine modal/camera/Pause lifetime and surviving-content ownership if approved implementation changes.
- DECISIONS: record only approved decisions; this artifact is not a decision entry.
- STATUS: distinguish user-reported stacking tests from latest visual/exact-artifact verification; reconcile older pending wording by scope.
- Relevant local authoring/Gallery guidance: select verified native controls/resources before authoring; exclude diagnostic dimensions from production conventions.

None was modified. Tracking.md and AI logs.md were not opened or searched.

## H. Evidence and limitations

### H1. Environments and identity

Mac SSH host: studio.renfrew.me; reported hostname Renfrews-Studio.renfrewworld.com. Repository `/Users/renfrew/Projects/ONI_mods/Oxygen_Not_Included_Together`; branch **feature/transport**; HEAD **2cf94cbd808686ad9c39c59fcb5d1b7c31d90c55**. Significant staged and unstaged work exists; HEAD alone does not describe inspected UI. Inventory follows below. No authoritative Git mutation occurred.

Windows snapshot `C:\Users\Renfrew\source\ONI_Together`; no Git initialization/repair/inference. Decompiled source `C:\Users\Renfrew\source\repos\ONI source\Assembly-CSharp`. Authoring project inspected as files: `C:\Users\Renfrew\Projects\Unity\ONI_United_UI`, including MultiplayerPrefabAuthoring, NativeControlAuthoring, NativeTabAuthoring and content prefabs. JoinView meta GUID: `f9c0da5f7b12a7949ba6bc1a3d2a08ca`. No Unity launch/import/build request.

Mac UI_DESIGN_PRINCIPLES was read in full; relevant UI sections of ARCHITECTURE, DECISIONS and STATUS read directly. Prohibited docs were excluded. Authoritative repository search returned no AGENTS.md files.

| Matched item | SHA-256 |
|---|---|
| Normal Assembly-CSharp.dll, Mac ONI_managed and Windows installed Managed | a58e04d0ffdf89b86fb28b71ad900625b3b539db30d67f8c6269f73a9f5ae599 |
| Normal Assembly-CSharp-firstpass.dll, same environments | 72d21f51cda136443bfa1a2f6eadb08167a4c508c3d03f558b8cdb60160f249c |
| Windows oni_united_ui bundle, Mac working file and Windows snapshot | 3c08a749c43b03d73e452dd26f4907eb2f1a440113a41bc3a59b990cdc290497 |
| MultiplayerMenuScreen.cs, Mac and Windows | 5953add0055ee8b4d12372b6c678e76d869cec902f21718c51ee3a44d7ad9b7c |
| Shared MultiplayerJoinContent.cs | a351695de6b98a8b3406d3e6d6770aa2ef0901bd3907a188277e0b9eddef8272 |
| Shared MultiplayerNativeAssets.cs | bd5597452aaca3bb98a9b591f70e88ab230769edaeeb220e01d25236817f776a |

Additional paired source hashes matched: ModAssets.cs, ModAssets.NativeUI.cs, ModalScreenPausePatch.cs, PauseScreenPatch.cs; all three domain views; MultiplayerHostContent/Input/SessionContent/TransportContent, SessionPlayerRow; GalleryScreen/Content/EntryView. Local links for these represent hash-verified Mac counterparts. This is not whole-snapshot or deployed-mod certification.

Windows vanilla installation: `E:\SteamLibrary\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data`; normal assemblies read from Managed using dnlib. Mac Directory.Build.props.user configures `/Users/renfrew/Projects/ONI_mods/ONI_managed`; hashes read there. Publicized assemblies exist but do not prove supported public vanilla API or deployed mod identity.

Decompiled source export has no independently established build provenance. Critical lifecycle/instantiation/input/tab/style/InfoDialog behavior was checked in installed normal assemblies, which hash-match Mac development assemblies. No decompiled-source commit/build identity is invented.

### H2. Asset method and limits

AssetRipper **2.0.0.0** ran headless locally, loaded installed game data for JSON inspection, then loaded the current mod bundle separately. No export/rebuild/save to game or Unity project. Native data reported Unity **6000.3.5f2**. Import logged a globalgamemanagers type-129 read-size mismatch (812 versus 816); target UI trees remained readable. No whole-installation extraction perfection is claimed.

Native values came from loaded sharedassets0.assets objects, not generated-prefab collections. Inspector collection indices 4 (assets)/9 (scripts) were temporary addresses; file + PathID identifies evidence. Original prefab GUIDs/variants were not recovered.

Mod collection `cab-4cace97a9f43018454a9bd906a6d3496`: JoinView root **-7732936448639472199**; HostView **7584615055207584538**; SessionView **2376575502954608481**; SessionPlayerRow **-1383524195162126650**. Production roots are inactive; the three screen-content components have activateOnSpawn false (SessionPlayerRow is an ordinary MonoBehaviour). Host has three Outlines, Join none. Packaged assets were inspected directly, independently of authoring-script intent. Authoring project files were also read; no comprehensive source-prefab-to-bundle equivalence claim is made.

Raw proprietary JSON stays local under `C:\Users\Renfrew\AppData\Local\Temp\oni-ui-reference-investigation`; it is not part of the delivered report or a Project upload. The report summarizes facts/property measurements instead of reproducing large proprietary source/assembly passages. Temporary inspection helpers are outside project source.

### H3. Logs and runtime scope

Host link `C:\Users\Renfrew\source\repos\log-pc\Player.log` resolves to `C:\Users\Renfrew\AppData\LocalLow\Klei\Oxygen Not Included\Player.log`. Internal evidence:

- `[04:11:11.964] Date: 10/26/2026 00:11:11`; bracket clock differs by four hours from date line, consistent with local offset. Preserve both; do not assume bracket clock is Toronto time.
- `[04:11:12.196] Build: U59-744825-SCR (release)`.
- `[04:11:23.184] [ONI_United] [ModAssets] Prepared UI assets loaded from 'oni_united_ui'.`
- `[04:12:09.726]` and `[04:13:29.084]` LoadScreen deactivation messages.
- Repeated U+1F47E missing-glyph messages in GRAYSTROKE near tail identify a font/Title limitation, not a specific Multiplayer control or latest styling failure.

These establish an earlier startup/asset-load event only. They do not identify the exact deployed bundle/DLL for that run or verify interactions/appearance. Symlink dates were not used to date tests.

VM Player.log resolves to `\\10.137.6.2\KleiLogs\Player.log`. Initial access was denied; elevated resolved-target read later reported unavailable/not found. No VM log/runtime result is claimed. Static work remained possible.

No screenshots, in-game pixel measurements, interaction tests or new builds. Final scaled dimensions, rendered corner radius, current hover/fallback font behavior and platform parity require runtime verification. Prior user-reported stacking and older documentation's pending status remain separately scoped evidence.

### H4. Commands and safety

Mac read-only operations: SSH hostname/pwd, branch/HEAD/status, staged/unstaged diff stats/name lists, targeted working-tree diffs, untracked listing, document/source reads, SHA-256, bounded file discovery. rg was absent on Mac, so grep/find were used. Windows: targeted rg/reads/hashes, symlink-target inspection, resolved logs, dnlib assembly reads and local AssetRipper in-memory/JSON requests.

Only this report and temporary inspection data/helpers/logs were written. No Git modifications, source/prefab edits, Unity requests, tests, package installs, asset exports or uploads. Existing source/documentation changes were preserved. Source/asset indices and exact Git inventory follow.

### H5. Authoritative Git inventory
Initial and final reported inventories match: **44 staged paths**, **6 paths with unstaged changes**, **0 untracked paths**. X/Y below are index/worktree status; AM/MM appear in both applicable groups.
```text
M  ONI_United/Misc/Utils.cs
 M ONI_United/ModAssets.NativeUI.cs
M  ONI_United/ModAssets.cs
MM ONI_United/ModAssets/assets/windows/oni_united_ui
M  ONI_United/ModAssets/translations/de.po
M  ONI_United/ModAssets/translations/ko.po
M  ONI_United/ModAssets/translations/pl.po
M  ONI_United/ModAssets/translations/ru.po
M  ONI_United/ModAssets/translations/zh.po
M  ONI_United/Networking/Lifecycle/GameServer.cs
M  ONI_United/Networking/NetworkConfig.cs
M  ONI_United/Networking/Sessions/MultiplayerSession.cs
M  ONI_United/Networking/Transport/LiteNetLib/LiteNetLibServer.cs
M  ONI_United/Patches/MainMenuScreenPatches/MainMenuPatch.cs
A  ONI_United/Patches/ModalScreenPausePatch.cs
M  ONI_United/Patches/PauseScreenPatch.cs
M  ONI_United/STRINGS.cs
D  ONI_United/UI/Components/LobbyEntryUI.cs
AM ONI_United/UI/Multiplayer/MultiplayerMenuScreen.cs
A  ONI_United/UI/Multiplayer/Views/MultiplayerHostView.cs
A  ONI_United/UI/Multiplayer/Views/MultiplayerJoinView.cs
A  ONI_United/UI/Multiplayer/Views/MultiplayerSessionView.cs
D  ONI_United/UI/Screens/HostLobbyConfigScreen.cs
D  ONI_United/UI/Screens/JoinByCodeDialog.cs
D  ONI_United/UI/Screens/LobbyBrowserScreen.cs
D  ONI_United/UI/Screens/MultiplayerInfoScreen.cs
D  ONI_United/UI/Screens/MultiplayerScreen.cs
D  ONI_United/UI/Screens/UnityLobbyStateDialogueUI.cs
D  ONI_United/UI/Screens/UnityMultiplayerScreen.cs
M  ONI_United/UI/Screens/UnityPasswordInputDialogueUI.cs
M  ONI_United/UI/UI_Patches.cs
D  ONI_United/UI/lib/FUI/FCycle.cs
D  ONI_United/UI/lib/FUI/FToggleButton.cs
A  Shared/UI/MultiplayerHostContent.cs
A  Shared/UI/MultiplayerInput.cs
AM Shared/UI/MultiplayerJoinContent.cs
AM Shared/UI/MultiplayerNativeAssets.cs
A  Shared/UI/MultiplayerSessionContent.cs
A  Shared/UI/MultiplayerTransportContent.cs
A  Shared/UI/SessionPlayerRow.cs
M  docs/ARCHITECTURE.md
M  docs/DECISIONS.md
M  docs/DEVELOPMENT_ROADMAP.md
M  docs/STATUS.md
AM docs/UI_DESIGN_PRINCIPLES.md
```
Unstaged refinements specifically concern donor/style dependencies, Graphics Close/title/Cancel setup, Join section styles/footer side, the Windows UI bundle and design principles. Staged work includes replacement of legacy UI screens, new Shared content and domain views, native modal pause integration, networking changes and documentation. All were preserved. Tracking status against remote was not refreshed or inferred.

### H6. Serialized native root/controller index
File: `E:\SteamLibrary\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\sharedassets0.assets`. The following are original loaded object PathIDs.

| Native class | Serialized root name | GO PathID | Controller PathID |
|---|---|---|---|
| InfoDialogScreen | InfoDialog | 8884 | 20753 |
| GraphicsOptionsScreen | GraphicsOptionsScreen | 8830 | 20267 |
| AudioOptionsScreen | AudioOptions | 8858 | 20700 |
| GameOptionsScreen | GameOptionsScreen | 9375 | 21496 |
| MetricsOptionsScreen | MetricsOptionsMenu | 8982 | 20306 |
| OptionsMenuScreen | OptionsMenu | 8980 | 20573 |
| LanguageOptionsScreen | LanguageOptionsMenu | 8983 | 20411 |
| LoadScreen | LoadScreen | 9080 | 21311 |
| ModsScreen | ModsScreen | 10044 | 22385 |
| ModeSelectScreen | ModeSelectScreen | 9814 | 22912 |
| RetiredColonyInfoScreen | RetiredColonyInfoScreen | 9991 | 21493 |
| LockerNavigator | LockerNavigator | 10194 | 26208 |
| LockerMenuScreen | LockerMenuScreen | 11234 | 23471 |
| OutfitBrowserScreen | OutfitBrowserScreen | 10200 | 24917 |
| ConfirmDialogScreen | ConfirmDialog | 8811 | 20221 |
| FeedbackScreen | FeedbackMenu | 8981 | 22102 |

### H7. Inspected source and method locations
Native links point to the supplied decompiled source. Mod links point to hash-verified local counterparts of authoritative Mac working files. These are reference locations, not proprietary source excerpts. Method links use one-based lines observed during report preparation.

| Source | Relevant inspected methods/locations |
|---|---|
| [InfoDialogScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:1>) | [GetSubHeaderPrefab](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:11>); [GetPlainTextPrefab](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:17>); [GetLineItemPrefab](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:23>); [GetPrimaryButtonPrefab](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:29>); [GetSecondaryButtonPrefab](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:35>); [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:41>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:54>); [OnShow](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:75>); [AddDefaultOK](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:85>); [AddDefaultCancel](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:96>); [AddOption](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:107>); [AddOption](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:119>); [SetHeader](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:128>); [AddSprite](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:135>); [AddPlainText](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:142>); [AddLineItem](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:149>); [AddSubHeader](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:158>); [AddSpacer](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:165>); [AddUI](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:179>); [AddDescriptors](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoDialogScreen.cs:186>) |
| [KModalScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalScreen.cs:1>) | [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalScreen.cs:9>); [MakeScreenModal](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalScreen.cs:16>); [ResizeBackground](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalScreen.cs:33>); [OnCmpEnable](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalScreen.cs:44>); [OnCmpDisable](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalScreen.cs:59>); [OnActivate](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalScreen.cs:93>); [OnDeactivate](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalScreen.cs:99>); [OnShow](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalScreen.cs:105>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalScreen.cs:123>); [OnKeyUp](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalScreen.cs:142>) |
| [KModalButtonMenu.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalButtonMenu.cs:1>) | [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalButtonMenu.cs:8>); [OnCmpEnable](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalButtonMenu.cs:15>); [OnCmpDisable](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalButtonMenu.cs:23>); [OnShow](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalButtonMenu.cs:53>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalButtonMenu.cs:75>); [ActivateChildScreen](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalButtonMenu.cs:94>); [Hide](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalButtonMenu.cs:104>); [Unhide](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/KModalButtonMenu.cs:110>) |
| [ScreenPrefabs.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ScreenPrefabs.cs:1>) | [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ScreenPrefabs.cs:14>); [ConfirmDoAction](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ScreenPrefabs.cs:20>) |
| [ModeSelectScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ModeSelectScreen.cs:1>) | [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ModeSelectScreen.cs:10>); [OnSpawn](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ModeSelectScreen.cs:17>); [OnHoverEnterSurvival](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ModeSelectScreen.cs:42>); [OnClickSurvival](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ModeSelectScreen.cs:60>) |
| [NewGameFlowScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/NewGameFlowScreen.cs:1>) | [NavigateBackward](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/NewGameFlowScreen.cs:17>); [NavigateForward](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/NewGameFlowScreen.cs:23>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/NewGameFlowScreen.cs:29>) |
| [LoadScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:1>) | [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:29>); [OnActivate](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:92>); [RefreshColonyList](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:315>); [ConvertAllToLocal](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:845>); [ShowSaveInfo](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:859>); [OnDeactivate](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:880>); [ShowColonyList](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:891>); [ShowColony](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:1026>); [AddColonyToList](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:1136>); [UpdateSelected](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:1310>); [ShowSimpleDialog](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:1425>); [ConfirmCloudSaveMigrations](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:1431>); [ConfirmDoAction](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:1460>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LoadScreen.cs:1473>) |
| [ConfirmDialogScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ConfirmDialogScreen.cs:1>) | [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ConfirmDialogScreen.cs:10>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ConfirmDialogScreen.cs:23>); [PopupConfirmDialog](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ConfirmDialogScreen.cs:34>); [OnSelect_OK](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ConfirmDialogScreen.cs:85>); [OnSelect_CANCEL](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ConfirmDialogScreen.cs:98>); [OnSelect_third](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ConfirmDialogScreen.cs:111>); [OnDeactivate](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ConfirmDialogScreen.cs:124>) |
| [RetiredColonyInfoScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/RetiredColonyInfoScreen.cs:1>) | [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/RetiredColonyInfoScreen.cs:16>); [ConfigButtons](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/RetiredColonyInfoScreen.cs:79>); [Show](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/RetiredColonyInfoScreen.cs:115>); [OnCmpEnable](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/RetiredColonyInfoScreen.cs:148>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/RetiredColonyInfoScreen.cs:157>); [OnCmpDisable](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/RetiredColonyInfoScreen.cs:186>); [PopulateAchievements](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/RetiredColonyInfoScreen.cs:350>); [ComputeSizeStatGrid](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/RetiredColonyInfoScreen.cs:750>); [ComputeSizeExplorerGrid](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/RetiredColonyInfoScreen.cs:764>); [LoadExplorer](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/RetiredColonyInfoScreen.cs:888>) |
| [LockerMenuScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerMenuScreen.cs:1>) | [OnActivate](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerMenuScreen.cs:12>); [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerMenuScreen.cs:36>); [ConfigureHoverForButton](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerMenuScreen.cs:68>); [Show](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerMenuScreen.cs:82>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerMenuScreen.cs:150>) |
| [LockerNavigator.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerNavigator.cs:1>) | [OnActivate](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerNavigator.cs:21>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerNavigator.cs:35>); [Show](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerNavigator.cs:45>); [PushScreen](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerNavigator.cs:62>); [PopScreen](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerNavigator.cs:95>); [PopAllScreens](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LockerNavigator.cs:127>) |
| [OutfitBrowserScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OutfitBrowserScreen.cs:1>) | [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OutfitBrowserScreen.cs:16>); [RefreshGallery](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OutfitBrowserScreen.cs:181>); [PopulateGallery](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OutfitBrowserScreen.cs:190>) |
| [LanguageOptionsScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LanguageOptionsScreen.cs:1>) | [OnSpawn](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LanguageOptionsScreen.cs:18>); [RebuildScreen](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LanguageOptionsScreen.cs:37>); [RebuildPreinstalledButtons](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LanguageOptionsScreen.cs:50>); [OnActivate](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LanguageOptionsScreen.cs:82>); [OnDeactivate](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LanguageOptionsScreen.cs:93>); [ConfirmLanguageChoiceDialog](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LanguageOptionsScreen.cs:103>); [GetConfirmDialog](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LanguageOptionsScreen.cs:189>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LanguageOptionsScreen.cs:197>); [RebuildUGCButtons](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LanguageOptionsScreen.cs:211>) |
| [ModsScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ModsScreen.cs:1>) | [OnActivate](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ModsScreen.cs:12>); [OnDeactivate](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ModsScreen.cs:54>); [Exit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ModsScreen.cs:62>); [RebuildDisplay](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ModsScreen.cs:77>); [BuildDisplay](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/ModsScreen.cs:92>) |
| [OptionsMenuScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OptionsMenuScreen.cs:1>) | [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OptionsMenuScreen.cs:11>); [OnSpawn](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OptionsMenuScreen.cs:29>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OptionsMenuScreen.cs:46>); [OnGraphicsOptions](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OptionsMenuScreen.cs:57>); [OnAudioOptions](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OptionsMenuScreen.cs:63>); [OnGameOptions](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OptionsMenuScreen.cs:69>); [OnMetrics](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OptionsMenuScreen.cs:75>); [OnFeedback](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/OptionsMenuScreen.cs:87>) |
| [GraphicsOptionsScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GraphicsOptionsScreen.cs:1>) | [OnSpawn](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GraphicsOptionsScreen.cs:14>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GraphicsOptionsScreen.cs:265>); [BuildOptions](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GraphicsOptionsScreen.cs:277>); [BuildColorModeOptions](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GraphicsOptionsScreen.cs:304>); [OnApply](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GraphicsOptionsScreen.cs:376>); [OnDone](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GraphicsOptionsScreen.cs:421>); [RefreshApplyButton](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GraphicsOptionsScreen.cs:427>); [ApplyConfirmSettings](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GraphicsOptionsScreen.cs:483>) |
| [AudioOptionsScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/AudioOptionsScreen.cs:1>) | [OnSpawn](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/AudioOptionsScreen.cs:13>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/AudioOptionsScreen.cs:82>); [OnReleaseHandle](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/AudioOptionsScreen.cs:99>); [BuildAudioDeviceList](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/AudioOptionsScreen.cs:127>); [OnClose](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/AudioOptionsScreen.cs:161>) |
| [GameOptionsScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GameOptionsScreen.cs:1>) | [OnSpawn](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GameOptionsScreen.cs:17>); [OnShow](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GameOptionsScreen.cs:63>); [RefreshCloudSaveToggle](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GameOptionsScreen.cs:113>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GameOptionsScreen.cs:120>); [OnKeyBindings](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/GameOptionsScreen.cs:173>) |
| [MetricsOptionsScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/MetricsOptionsScreen.cs:1>) | [IsSettingsDirty](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/MetricsOptionsScreen.cs:10>); [OnKeyDown](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/MetricsOptionsScreen.cs:16>); [OnSpawn](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/MetricsOptionsScreen.cs:26>); [OnClickToggle](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/MetricsOptionsScreen.cs:60>); [ApplySettingsAndDoRestart](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/MetricsOptionsScreen.cs:68>); [Refresh](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/MetricsOptionsScreen.cs:80>) |
| [FeedbackScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/FeedbackScreen.cs:1>) | [OnSpawn](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/FeedbackScreen.cs:11>) |
| [LocText.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LocText.cs:1>) | [ApplySettings](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LocText.cs:39>); [Awake](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LocText.cs:53>); [OnDestroy](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LocText.cs:88>); [SetText](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LocText.cs:120>); [SwapFont](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/LocText.cs:196>) |
| [InfoScreenPlainText.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoScreenPlainText.cs:1>) | [SetText](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoScreenPlainText.cs:9>) |
| [InfoScreenLineItem.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoScreenLineItem.cs:1>) | [SetText](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoScreenLineItem.cs:9>); [SetTooltip](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoScreenLineItem.cs:15>) |
| [InfoScreenSpriteItem.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoScreenSpriteItem.cs:1>) | [SetSprite](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/InfoScreenSpriteItem.cs:10>) |
| [SliderContainer.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/SliderContainer.cs:1>) | [OnSpawn](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/SliderContainer.cs:10>); [UpdateSliderLabel](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/SliderContainer.cs:17>) |
| [NumericDropDownTableColumn.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/NumericDropDownTableColumn.cs:1>) | [GetWidget](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/NumericDropDownTableColumn.cs:32>) |
| [CheckboxTableColumn.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/CheckboxTableColumn.cs:1>) | [GetDefaultWidget](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/CheckboxTableColumn.cs:32>) |
| [UIPool.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/UIPool.cs:1>) | [GetFreeElement](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/UIPool.cs:47>) |
| [UIPrefabLocalPool.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/UIPrefabLocalPool.cs:1>) | [Borrow](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/UIPrefabLocalPool.cs:17>); [ReturnAll](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/UIPrefabLocalPool.cs:44>) |
| [DetailsScreen.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/DetailsScreen.cs:1>) | [OnPrefabInit](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/DetailsScreen.cs:24>); [InitiateSidescreenTabs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/DetailsScreen.cs:67>) |
| [Assets.cs](<C:/Users/Renfrew/source/repos/ONI source/Assembly-CSharp/Assets.cs:758>) | TableScreenWidgets and TableScreenAssets declarations at lines 758–808 |
| [MultiplayerMenuScreen.cs](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/MultiplayerMenuScreen.cs:1>) | [Open](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/MultiplayerMenuScreen.cs:51>); [OpenHostSetup](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/MultiplayerMenuScreen.cs:82>); [RefreshActiveView](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/MultiplayerMenuScreen.cs:107>); [Poll](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/MultiplayerMenuScreen.cs:139>); [OnDeactivated](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/MultiplayerMenuScreen.cs:150>); [Dispose](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/MultiplayerMenuScreen.cs:163>); [OnSceneChanging](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/MultiplayerMenuScreen.cs:165>); [Dispose](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/MultiplayerMenuScreen.cs:176>); [Prefix](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/MultiplayerMenuScreen.cs:214>) |
| [MultiplayerHostView.cs](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerHostView.cs:1>) | [RefreshAvailability](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerHostView.cs:28>); [StartLanHosting](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerHostView.cs:39>) |
| [MultiplayerJoinView.cs](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerJoinView.cs:1>) | [RefreshLobbies](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerJoinView.cs:42>); [JoinByCode](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerJoinView.cs:74>); [Connect](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerJoinView.cs:98>); [RefreshStatus](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerJoinView.cs:127>); [RefreshStatus](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerJoinView.cs:139>); [Dispose](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerJoinView.cs:140>) |
| [MultiplayerSessionView.cs](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerSessionView.cs:1>) | [Refresh](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerSessionView.cs:27>); [CancelConnection](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerSessionView.cs:52>); [StopHosting](<C:/Users/Renfrew/source/ONI_Together/ONI_United/UI/Multiplayer/Views/MultiplayerSessionView.cs:57>) |
| [MultiplayerNativeAssets.cs](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerNativeAssets.cs:1>) | [ConfigureDialog](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerNativeAssets.cs:23>); [Spawn](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerNativeAssets.cs:39>); [Text](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerNativeAssets.cs:54>); [SectionTitle](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerNativeAssets.cs:69>); [SectionFrames](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerNativeAssets.cs:79>); [Button](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerNativeAssets.cs:88>); [Input](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerNativeAssets.cs:102>); [Frame](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerNativeAssets.cs:119>); [StopEditing](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerNativeAssets.cs:126>) |
| [MultiplayerJoinContent.cs](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerJoinContent.cs:1>) | [Initialize](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerJoinContent.cs:30>); [SetAvailability](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerJoinContent.cs:68>); [OnShow](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerJoinContent.cs:78>); [SetLobbies](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerJoinContent.cs:85>); [StopEditing](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerJoinContent.cs:106>); [OnForcedCleanUp](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerJoinContent.cs:107>) |
| [MultiplayerHostContent.cs](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerHostContent.cs:1>) | [Initialize](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerHostContent.cs:17>); [SetAvailability](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerHostContent.cs:37>); [OnShow](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerHostContent.cs:42>); [OnForcedCleanUp](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerHostContent.cs:48>); [StopEditing](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerHostContent.cs:53>) |
| [MultiplayerSessionContent.cs](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerSessionContent.cs:1>) | [Initialize](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerSessionContent.cs:18>); [SetState](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerSessionContent.cs:31>); [OnShow](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerSessionContent.cs:37>); [SetPlayers](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerSessionContent.cs:42>); [OnForcedCleanUp](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerSessionContent.cs:67>) |
| [MultiplayerTransportContent.cs](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerTransportContent.cs:1>) | [InitializeTransport](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerTransportContent.cs:15>); [OnTransportSelected](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerTransportContent.cs:25>); [OnSpawn](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerTransportContent.cs:32>); [OnForcedCleanUp](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerTransportContent.cs:40>) |
| [MultiplayerInput.cs](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerInput.cs:1>) | [EditingEnded](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerInput.cs:9>); [ConsumeEditingInput](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/MultiplayerInput.cs:10>) |
| [SessionPlayerRow.cs](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/SessionPlayerRow.cs:1>) | [Initialize](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/SessionPlayerRow.cs:12>); [SetText](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/SessionPlayerRow.cs:23>) |
| [GalleryScreen.cs](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/GalleryScreen.cs:1>) | [AddPage](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/GalleryScreen.cs:14>); [OnPrefabInit](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/GalleryScreen.cs:28>); [OnSpawn](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/GalleryScreen.cs:34>); [OnKeyDown](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/GalleryScreen.cs:40>) |
| [GalleryContent.cs](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/GalleryContent.cs:1>) | [Add](<C:/Users/Renfrew/source/ONI_Together/Shared/UI/GalleryContent.cs:9>) |
| [ModAssets.cs](<C:/Users/Renfrew/source/ONI_Together/ONI_United/ModAssets.cs:1>) | [Initialize](<C:/Users/Renfrew/source/ONI_Together/ONI_United/ModAssets.cs:21>); [Initialize](<C:/Users/Renfrew/source/ONI_Together/ONI_United/ModAssets.cs:42>); [Initialize](<C:/Users/Renfrew/source/ONI_Together/ONI_United/ModAssets.cs:83>); [LoadPlatformBundle](<C:/Users/Renfrew/source/ONI_Together/ONI_United/ModAssets.cs:239>); [ValidateUIComponents](<C:/Users/Renfrew/source/ONI_Together/ONI_United/ModAssets.cs:257>); [LoadRequiredPrefab](<C:/Users/Renfrew/source/ONI_Together/ONI_United/ModAssets.cs:265>) |
| [ModAssets.NativeUI.cs](<C:/Users/Renfrew/source/ONI_Together/ONI_United/ModAssets.NativeUI.cs:1>) | [InitializeNativeUiAssets](<C:/Users/Renfrew/source/ONI_Together/ONI_United/ModAssets.NativeUI.cs:10>); [InitializeNativeUiAssets](<C:/Users/Renfrew/source/ONI_Together/ONI_United/ModAssets.NativeUI.cs:12>) |
| [ModalScreenPausePatch.cs](<C:/Users/Renfrew/source/ONI_Together/ONI_United/Patches/ModalScreenPausePatch.cs:1>) | [TargetMethods](<C:/Users/Renfrew/source/ONI_Together/ONI_United/Patches/ModalScreenPausePatch.cs:14>); [Transpiler](<C:/Users/Renfrew/source/ONI_Together/ONI_United/Patches/ModalScreenPausePatch.cs:20>); [ConditionalPause](<C:/Users/Renfrew/source/ONI_Together/ONI_United/Patches/ModalScreenPausePatch.cs:47>); [ConditionalUnpause](<C:/Users/Renfrew/source/ONI_Together/ONI_United/Patches/ModalScreenPausePatch.cs:53>) |
| [PauseScreenPatch.cs](<C:/Users/Renfrew/source/ONI_Together/ONI_United/Patches/PauseScreenPatch.cs:1>) | [Postfix](<C:/Users/Renfrew/source/ONI_Together/ONI_United/Patches/PauseScreenPatch.cs:35>) |

### H8. Normal firstpass assembly method references
The supplied directory lacks firstpass source exports for these classes. Exact installed-assembly method tokens identify the inspected behavior; tokens apply to the SHA-256 recorded above.

| Method | Metadata token |
|---|---|
| KMonoBehaviour.InitializeComponent | 0x0600076A |
| KMonoBehaviour.OnDisable | 0x0600076C |
| KMonoBehaviour.OnDestroy | 0x0600076E |
| KScreen.OnKeyDown | 0x06000406 |
| KScreen.Activate | 0x0600040B |
| KScreen.Deactivate | 0x0600040D |
| KScreen.Show | 0x06000413 |
| KScreenManager.StartScreen | 0x06000423 |
| KScreenManager.RefreshStack | 0x06000425 |
| KScreenManager.Update | 0x06000429 |
| KScreenManager.OnKeyDown | 0x0600042A |
| GameScreenManager.StartScreen | 0x06000356 |
| KTabMenu.AddTab (text) | 0x0600045F |
| KTabMenu.AddTab (sprite/title) | 0x06000460 |
| KTabMenu.ActivateTab | 0x06000461 |
| KTabMenuHeader.Add (text) | 0x06000467 |
| KTabMenuHeader.Add (sprite/title) | 0x06000468 |
| Util.KInstantiateUI (non-generic) | 0x06000A19 |
| SetTextStyleSetting.ApplyStyle | 0x060004B1 |

Main-assembly critical checks also reconfirmed InfoDialog OnKeyDown (0x06006D1E), OnShow (0x06006D1F), AddOption (0x06006D22/23), AddUI (0x06006D2A), KModalScreen.MakeScreenModal (0x06006DB7), OnCmpDisable (0x06006DBA), OnShow (0x06006DC0), and KModalButtonMenu.ActivateChildScreen/Hide/Unhide (0x06006DB2–B4).

