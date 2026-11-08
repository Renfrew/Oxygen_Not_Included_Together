# Multiplayer: ONI United — UI Architecture and Design Principles

**Status:** Approved design principles  
**Scope:** All new and refactored Multiplayer UI work  
**Purpose:** Permanent Project reference for design, implementation, code review, and AI handoffs

> **Core rule: Unity-authored UI and its `Shared/UI` component own presentation and UI interactions. `ONI_United` composes prepared reusable UI components through intentional Shared APIs and supplies translated captions, domain data, dependencies, and callbacks. Never make the consumer reach into the UI's child hierarchy.**

## 1. Ownership boundaries

### Unity authoring project — serialized presentation

The Unity authoring project defines the prefab hierarchy, anchoring, dimensions, backgrounds, fonts, native control slots, layout groups, scroll viewports, and serialized connections to UI components. The Unity-authored prefabs are packaged into platform-specific `oni_united_ui` AssetBundles.

Do **not** manually build or reposition a prefab's internal visual hierarchy in game-side controllers. The mod may compose a page from prepared sections, rows, and controls by calling Shared APIs such as `AddUI`, `AddRow`, and `AddOption`. Shared implements insertion into private containers and applies authored layout constraints. Requesting insertion through those APIs is not direct hierarchy manipulation.

### `Shared/UI` — actual UI classes and behavior

The mod repository's `Shared/UI` project defines the Unity/Klei components attached to authored prefabs, in the same manner as `GalleryScreen`, `GalleryContent`, and `GalleryEntryView`.

Each UI class owns:
- Its **private** serialized references to labels, panels, native tabs, buttons, input fields, scroll regions, and row prefabs.
- UI setup, view/page activation, selected/disabled/hover presentation, input interaction, UI-level validation feedback, rendering, row lifecycle, and UI event cleanup.
- Public methods for inserting prepared components, supplying data and translated captions, configuring callbacks, selecting views, and setting statuses.

A container owns its containment and interaction mechanics, not necessarily all feature-specific content. The reusable `Shared.UI.TabMenu : KTabMenu` has an embedded native Header, a live Body, and an inactive Unity-authored `Layouts/Vertical` KScreen template outside Body. Shared supplies initialization and the focused `GetContentPrefab()` getter; native tab registration, selection, and visibility remain inherited. Join and Host obtain the template from their instantiated menus, clone two screens into Body, compose prepared sections through Shared APIs, register the screens with native `AddTab` using translated titles, and explicitly select the initial tab. No page-creation or registration wrapper is needed. The mod may retain typed handles to the composed components; the menu need not store Join-specific sections, inputs, buttons, captions, or domain callbacks. Host now follows this same reusable-menu composition; Session retains its existing content implementation pending a separate refactor.

Its public API should express **intent**, not reveal how its child GameObjects are arranged.

### `ONI_United` — domain integration

The game-side code may store **strongly typed references** to Shared components, as `NativeUiGallery` does with `private GalleryScreen screen;`.

It owns:
- Host/join operations, Steam and LAN networking, session/player state, domain validation, transport and game lifecycle rules.
- Prefab loading/instantiation through the established asset pipeline.
- Choosing which prepared components make up a feature and supplying translated captions, initial data, prepared template dependencies, domain callbacks, and subsequent data updates.
- Preparing reusable runtime templates through Shared components/utilities and Unity-authored constraints, without modifying cached vanilla donors or inspecting prefab internals.
- Coordinating top-level game navigation/lifecycle without reaching inside a Shared UI component's private controls.

**Game code must not** call `transform.Find(...)` to discover private UI children, reach through a component to retrieve its internal widgets, set child RectTransforms, copy visual styling manually, or reparent children into private containers. This does not prohibit configuring a typed control returned intentionally by `AddUI`/`AddOption`, supplying callbacks/data through Shared methods, or using Shared utilities such as `UiUtil.ConfigureInputField`. Public component handles expose a supported API; they do not expose internal hierarchy paths.

Domain-level validation must remain authoritative even if the UI also gives immediate input feedback.

## 2. Encapsulate serialized child references

Follow the vanilla `InfoDialogScreen` *API-design concept*: serialized internal fields are private; the screen exposes focused operations and, where justified, selected prefab getters.

```csharp
public sealed class MultiplayerJoinContent : KScreen
{
    [SerializeField] private TMP_Text statusText = null!;
    [SerializeField] private KButton refreshButton = null!;
    [SerializeField] private KScrollRect lobbyScroll = null!;
    [SerializeField] private GameObject emptyState = null!;

    public void SetStatus(string message) => statusText.text = message;

    public void SetLobbies(IReadOnlyList<LobbyDisplayData> lobbies)
    {
        // This UI component updates its own authored list and empty state.
    }

    public void ConfigureActions(Action refresh, Action openFriends)
    {
        // This UI component owns binding/cleanup on its private controls.
    }
}
```

*Illustrative API only: types and names must be adapted to the existing compiled code, not copied blindly.*

Prefer `AddUI()`, `AddRow()`, `AddOption()`, `SetHeader()`, `SetStatus()`, `SetLobbies()`, `SetPlayers()`, `ConfigureActions()` and `SelectTransport()` over `GetHeaderTransform()`, `GetButtonsContainer()` or exposing serialized child fields. Returning a typed newly inserted component through an `out` parameter is supported. It is not the same as publishing an arbitrary internal widget.

A public getter for a native prefab template is appropriate **only when another component genuinely needs that template to create a control**, as in vanilla `InfoDialogScreen.GetPrimaryButtonPrefab()`. Avoid getters for ordinary internal widgets or hierarchy containers.

## 3. Dependencies and initialization

- **Unity components:** Instantiate the prepared prefab through the established loading/composition API, get its strongly typed Shared component, and configure it through its public methods or Shared utilities. Do not depend on arbitrary parameterized `MonoBehaviour` constructors.
- **Incremental composition:** There is no requirement for one large page-level `Initialize(...)` accepting all strings and callbacks. The mod may add reusable sections/rows and configure them individually. A page-specific `ConfigureXXX` method or callback/data object is optional, not a prerequisite for supplying translated text and domain actions.
- **Ordinary C# values:** Use constructors for required properties. A builder is appropriate when there are many frequently used optional configuration settings, but avoid builders for trivial cases.
- Configure inactive authored instances and register callbacks before activation, following the Gallery pattern.
- Avoid changing shared cached native prefab templates. Clone/configure their instances within the owning UI component.
- Subscribe and unsubscribe deterministically; avoid duplicate listeners across hide/reopen cycles.
- UI callbacks invoke supplied domain operations; UI classes must not directly depend on Steam networking or host/session services.
- Support subsequent **data updates**: “feed data during construction” must not prevent live connection status, lobby lists, and player lists from refreshing.

Example mod-side composition through Shared APIs:

```csharp
// Illustrative API: names/signatures and tab selection must match the implementation.
// steamPage is a KScreen cloned from the instantiated menu's GetContentPrefab().
steamPage.AddUI(templates.SectionPrefab, out var section);
section.SetHeader(translatedJoinHeading);
section.AddOption(translatedRefreshCaption, _ => RefreshLobbies());
section.AddRow(out var row);
row.AddUI(templates.InputFieldPrefab, out var codeInput, useLeft: true);
UiUtil.ConfigureInputField(codeInput, placeholder: translatedCodePlaceholder);
```

The mod chooses the composition and supplies translations/data/actions. Shared performs prefab instantiation, chooses its private insertion destinations, and owns presentation/interaction mechanics. The mod does not need to know child names, transforms, anchors, or dimensions. `useLeft` is an explicit row-API choice, not permission to inspect the row's hierarchy. Configure the inactive page/control instances before showing the assembled page. This example describes the ownership contract, not a claim that all APIs are already implemented.

## 4. Native ONI reuse and layout

- Treat the **Native UI Gallery** as the code/ownership reference: serialized fields, native control binding, native `KTabMenu` page registration, authored `KScrollRect`, and entry components.
- Treat the **vanilla Mods screen** as the primary visual reference: compact frame and header/Close X, vanilla fonts and control proportions, meaningful content grouping, and contextual footer.
- Host and Join use **real native `KTabMenu` transport tabs** (Steam/LAN), not ordinary buttons disguised as tabs. Account for tab activation/deactivation and selection-artwork state.
- Session and lobby lists use authored scroll components and reusable row prefabs. Shared UI components own row creation and cleanup.
- Reuse proven native button/input templates, but do not copy the Gallery's diagnostic control dimensions or diagnostic features.
- Prefer shared components for real repeated behavior (e.g., footers, list entries, native tab setup) while avoiding a second speculative UI framework.

## 5. `InfoDialogScreen`: principle versus concrete shell choice

**Approved design principle:** Model our public UI APIs on vanilla `InfoDialogScreen`, including private `[SerializeField]` references and explicit operations such as `SetHeader(...)`, `AddOption(...)`, `AddUI<T>(...)`, plus narrowly justified prefab getters.

**Separate implementation decision:** Whether to use an actual `InfoDialogScreen` prefab as the Multiplayer window root. Its supported arbitrary-content API makes it a candidate, but adopting it requires deliberate handling of:
- Exactly one native modal owner per dialog; no nested custom `KModalScreen`.
- Join/Host/Session navigation and Host Back versus Close.
- Escape while an input is being edited.
- Pause-menu child-screen attachment, Close subscription ordering, hide/unhide and rollback.
- Camera lock and pause behavior.
- Dynamic contextual footer actions and scene-transition cleanup.

Do not treat the earlier decision to keep an authored shell as a permanent prohibition on using `InfoDialogScreen`; likewise, **do not replace the shell merely to achieve the API encapsulation rule**. Re-evaluate the concrete shell using verified prefab and lifecycle evidence, then record the decision separately.

The Multiplayer window uses an authored `Shared.UI.MultiplayerDialogScreen : KModalScreen`.
The full-canvas root is inactive in the bundle; native code creates its sole backdrop.
A centered panel owns header/title, a 24-unit native Close slot, content and left/right
footer groups with 10-unit gaps. Authored option constraints are minimum 100 x 32 with
text-driven width expansion; no fixed outer height or per-transport resizing is used.
The shell clones complete native controls into its private slots without changing donors.

Its small API is Initialize, SetHeader, typed AddUI, AddOption (plus handle/side/accent
parameters for real consumers), and Dismissing. AddOption never automatically dismisses;
the shell owns its listeners, while Shared content owns footer visibility/availability.
No networking, Pause.childDialog, session state, routing or custom screen stack belongs
in this shell. The coordinator creates sibling modal layers and retains cached content,
Session in the layer that connected, the base-only Pause relationship, captured camera
state, native Close final disposal and explicit scene/open-failure cleanup. Dismissing
provides once-only early unlinking before native stack removal. Closing UI never disconnects.

Native canBackoutWithRightClick is explicitly true. MultiplayerInput accepts KScreen
and runs before base OnKeyDown, preserving focused-field and end-edit-frame protection.
There is no InfoDialog-specific Harmony input patch, private-header access or escapeCloses
assignment. KModalScreen's native drag behavior is used without another mouse router.
Existing native InfoDialog control templates remain valid shared donors for other UI.

These are source/authoring contracts, not claims of ONI runtime verification.

### Reusable grouped sections

`Shared.UI.MultiplayerSection : KMonoBehaviour` is a presentation component inside
Unity-authored content, not a screen or modal owner. Its reusable prefab owns a native
section background and `Layout/HeaderRow/{Header,Actions}` plus `Layout/Content`.
Native Graphics Options supplies the actual sliced section sprite/color and heading
font/material/style. Layout owns 12-unit padding, 10-unit body gaps and a 32-unit minimum
heading height; actions have their own horizontal layout beside the flexible heading.

Initialize each authored section with the existing native dependencies, then use
`SetHeader(text)`, `AddOption(text, Action<MultiplayerSection>)`, and optional typed
`AddUI<T>(prefab, out instance)`. AddOption creates a **header action**, never dismisses
anything, and defaults to the neutral native button with compact 100-by-40 minimums
(the Mods Manage reference), rather than the 128-by-48 InfoDialog footer minimums.
Its handle overload lets the owning Shared content update action availability.
AddUI inserts a **body**; existing serialized form/list bodies stay under Content and
must not be split into extra prefabs merely to exercise that API.

Join has corresponding three-section Steam/LAN stacks: lobby list/placeholder, compact
Join row, and compact Settings row. Corresponding sections have equal Unity-authored
minimum/preferred dimensions, heading constraints and gaps. Below-row notices and
validation-feedback UI in Join sections are removed for now; no fixed feedback space is
required. Domain validation remains authoritative, with input handling and validation-driven
button availability preserved. The public-lobby status display remains, with two explicit
section instances, one per transport tab. The authored dialog panel consumes that content;
controllers must not resize the outer dialog.
Friends and disabled Configure actions belong to Settings body rows, using the existing
Shared native-control binding. Section AddOption retains its header-action semantics.
The mod view may compose Host/Join pages from prepared player/Steam/LAN sections
through Shared insertion APIs. The mod supplies translated captions, values, validators,
and domain callbacks; Shared components apply them and own their UI bindings. This does
not require a page class to hardcode feature captions or retain every section/control.
ONI_United domain views do not discover or manipulate section descendants.
Sections release their own button listeners during forced cleanup and live with their
containing content/dialog. They have no navigation, screen-stack, Pause, camera,
networking, backdrop or dialog-destruction responsibilities.

These contracts are supported by source, compilation and asset validation; Editor
layout/callback checks and actual in-game visual/lifecycle verification must be reported
separately. No new screen framework or global section cache is introduced.

### Host composition — current approved layout (2026-10-29)

Host uses the same header/Close presentation as Join, with one common Maximum Players section above the reusable transport menu. Each transport has a Settings section with two existing authored rows: Steam password, then Friends Only and a right-aligned Settings button; LAN Player Name, then port and a right-aligned Settings button. Matching authored rows/spacing are the intended equal-height mechanism; equal rendered page height still requires verification. There is no HostSettingsRow-specific serialized constraint, filler row or controller resizing.

The Host footer has accented Start Hosting on the left and Cancel on the right, without a version label. Host status and validation-message UI and their reserved space are removed for now. Domain validation, validation-on-Start timing, availability guards and failure logging remain authoritative. Join's retained public-lobby status is unaffected. Unsupported Steam operations and Settings actions remain unavailable. This describes the approved current source contract, not runtime or gameplay verification.

## 6. Prohibited implementation patterns

- Letting `ONI_United` manipulate UI child fields or hierarchy paths.
- Exposing `[SerializeField]` child controls publicly by default.
- Manually assembling prefab internals, tab artwork, or layout geometry in game-side controllers. Composing prepared screens/sections/rows through Shared `AddUI`/`AddRow`/`AddOption` APIs is explicitly allowed; Shared owns the actual hierarchy operations.
- Duplicating button/tab/input presentation logic across Host and Join when a focused reusable Shared component is practical.
- Creating an additional modal screen merely to host serialized content.
- Cloning a large unrelated vanilla UI tree and stripping its game-specific controllers as a substitute for an authored contract.
- Treating compilation or AssetBundle validation as proof of correct in-game appearance or lifecycle.

## 7. Implementation and review checklist

Before approving UI changes, verify:

- [ ] All UI component classes are in the mod repository's `Shared/UI` project.
- [ ] Internal child controls are private `[SerializeField]` references.
- [ ] Consumer code holds typed component references and calls intent-based public methods, including composition APIs.
- [ ] The mod supplies translated captions, feature composition, initial values, and domain callbacks; the Shared container is not forced to take one giant initializer or expose private hierarchy.
- [ ] Typed controls returned by insertion APIs are configured through supported component APIs/Shared utilities; internal children remain private.
- [ ] No game-side code searches or rearranges a Shared view's internal hierarchy.
- [ ] Unity prefab layout, native templates, component references and asset dependencies are validated.
- [ ] Host/Join native tabs work correctly across selection, hover, hide/reopen and cleanup.
- [ ] Inputs receive focus, edit correctly and do not cause Escape/Back conflicts.
- [ ] UI feedback is separate from domain validation/authorization.
- [ ] Contextual content/footer updates are cohesive.
- [ ] Pause Menu parent-child lifecycle, Close, camera/pause state and scene cleanup are checked.
- [ ] Platform-specific AssetBundles are generated only for supported installed Unity build modules and verified individually.
- [ ] Build, prefab validation, packaging and in-game runtime results are reported as distinct evidence categories.
- [ ] Current Git status is inspected before edits, existing staged/index changes preserved, **all AI changes left unstaged**, and no automatic commit/push/restore/reset/stash occurs.

## 8. Rules for AI implementation prompts

Include this instruction in every Multiplayer UI task:

> **Follow `UI_DESIGN_PRINCIPLES.md`. `Shared/UI` owns Unity UI internals with private serialized references and intent-based public methods; `ONI_United` may compose prepared sections, rows, and controls through Shared insertion APIs and supplies translations, data, dependencies and domain callbacks. Do not confuse `AddUI`/`AddRow`/`AddOption` calls with manual hierarchy construction; do not require one giant initializer. Typed returned handles and Shared configuration utilities are supported. Do not traverse private child hierarchies or recreate layouts procedurally. Preserve existing native UI architecture, lifecycle, and Git state; leave new changes unstaged.**

This document is an **approved design specification**, not evidence that the current source already meets every rule. Implementation compliance and runtime behavior must be verified separately.
