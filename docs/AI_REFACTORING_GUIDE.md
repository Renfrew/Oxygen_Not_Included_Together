# AI Refactoring Guide

The failures this guide addresses were concrete: reopening settled decisions, treating assumptions as findings, misreading UI ownership, adding replacement abstractions during cleanup, and claiming completion while dependent assets remained inconsistent. Apply the checks that affect the current edit; do not turn this guide into another approval framework.

## Recover the actual agreement

Before editing, establish the requested outcome, latest approved design, behavior that must survive, and permitted actions. Use the current source and relevant conversation turns. A later correction supersedes an earlier proposal; an assistant's assurance is not a user decision or test result.

Keep a compact working note when the conversation is long: accepted design, rejected approaches, exact test coverage, current files, and remaining work. Do not record every turn or write repository architecture documents without authorization.

Do not ask the user to repeat an agreement already available in the chat. Do not revive a rejected approach under a new class, helper, asset, or variable name. If evidence genuinely contradicts the requested implementation, state the exact conflict once with source or runtime evidence. Do not pretend it is resolved to obtain agreement.

## Choose the smallest complete correction

Inspect the current diff, affected callers, and existing native APIs before designing replacements. Existing native behavior takes priority over a wrapper that merely duplicates it.

For each proposed addition, identify its present consumer and concrete responsibility. Remove additions justified only by hypothetical future layouts, reuse, failure modes, or configurability. Short code is not automatically simple; a meaningful authored UI component may justify a small class.

For a rename, update the declaration, callers, and affected validation. Do not reorganize assets or change lifecycle behavior. For cleanup, remove the redundant layer and reconnect its consumers; do not introduce an equivalent layer elsewhere.

Keep assets in the user's existing groups. Do not add forwarding properties, single-member containers, duplicate caches, or generic builders without a current need. Names must identify what an object actually represents, especially menu root, live Body, and inactive content template.

When the user requires approval for coding decisions, obtain it for substantive design changes before editing. Apply authorized mechanical updates without repeatedly asking for permission. A request to simplify is not permission to replace the architecture.

## Preserve ownership and evidence

For ONI UI work only, follow the authoritative UI_DESIGN_PRINCIPLES.md and applicable project instructions:

- Unity owns authored hierarchy, geometry, spacing, and layout. Do not replace them with procedural builders or move the same builder into a helper to disguise the violation.
- Shared owns UI initialization and insertion mechanics. The mod owns domain data, translated text, callbacks, validation decisions, and feature composition through supported APIs.
- Typed returned component handles and narrowly justified prefab getters are allowed. Do not invent a blanket prohibition on accessing templates. Prefer the user's requested getter name and contract.
- Determine why a field is serialized. Authored references and references that must survive Unity cloning can be legitimate; runtime assignment alone does not settle that question.
- Distinguish a native component class from a configured prefab. Neither claiming that a prefab exists nor claiming that initialization is missing is justified without inspecting the actual source or instance.
- Preserve proven native tab registration, labels, selection, and visibility. Successful configured-menu tests do not prove that a bare component works; source inspection does not erase successful runtime evidence.

Do not describe source inspection as a test. Distinguish compilation, Editor fixture checks, asset validation, deployment, and in-game verification. Report only checks actually performed and what they establish. Do not add redundant state resets, callbacks, guards, or style overrides merely to guarantee behavior already owned and verified elsewhere.

## Finish without churn

Preserve current staged and unrelated work. Correct your own changes in place; do not restore the staged baseline wholesale unless explicitly instructed, particularly when it would reintroduce rejected code.

When an authorized change affects an asset contract, update its actual references, metadata, consumers, and existing validation together. Keep source, generated DLLs, bundles, and deployment status distinct. Obtain any required authorization before builds, tests, deployment, or external actions; never claim pending binary work is complete.

Use validation proportional to the change. A getter rename needs caller/reference checks and any required project checks, not a new test framework or another native-UI investigation. Stop broadening validation once the relevant evidence is sufficient.

Before reporting completion, compare the result with the latest agreement: removed code is gone, no replacement machinery has crept in, consumers still fit their intended roles, accepted behavior is preserved, and remaining dependencies are identified. Report the outcome and material limits concisely. A promise to improve is not evidence that the improvement worked.

