# Multiplayer: ONI United — Development Roadmap

**Created:** 2026-09-27  
**Last updated:** 2026-10-17

**Status:** Planning and progress guide; verified results retain the evidence limits stated here and in `STATUS.md`  
**References:** `ARCHITECTURE.md`, `DECISIONS.md`, `STATUS.md`

## N14 closure — startup and lifecycle implementation complete

**Status:** Complete in the current `refactor/mod-lifecycle` history through `810e0fde0e62900e6ecc240d8d335e94e80b66ca`.

**Scope:** Startup ordering and diagnostics; configuration/persistent identity and asset ownership; PLib integration; Harmony patch ownership; packet-registration lifecycle; persistent component and event-subscription checks; Discord Rich Presence enablement.

The N14 implementation commits are `ec2fcc9b`, `e6748619`, `91d664ef`, `955fee1d`, `27cb71df`, `626c2d57`, `5cdcf2f6`, and `810e0fde`. N14-A5 through A9 produced no further source changes where no justified fix was found. The current worktree had no outstanding N14 source changes at closure.

Non-deploying Debug and Release builds succeeded during N14. The final A9 builds each had 0 errors and 4 `MSB3277` assembly-version conflict warnings. No ONI runtime/gameplay testing was performed; these builds and source reviews are not evidence of synchronization or release readiness. Default packet registration remains best-effort and logs individual failures without rethrowing. Generic startup rollback remains deferred.

## N15-B — standalone unit-test foundation

**Status:** Implementation independently verified and ready for user commit; uncommitted at the verified point and not yet merged.

**Objective:** Establish Option 3's first isolated Level 1 test project without importing game dependencies or changing production behavior.

The agreed verification model is Level 1 standalone unit tests, Level 2 standalone integration tests, and Level 3 in-game tests. Existing 92 in-game diagnostics migrate incrementally only when their subject can be represented faithfully outside ONI.

Implemented N15-B scope:

- `tests/Shared.Tests` is a separate `net10.0` xUnit project under the solution's `tests` folder.
- It source-links the real `Shared/Replication/SnapshotTimeline.cs`, has no project references, and removes inherited game/mod references and build behavior.
- Five `SnapshotTimeline` tests passed in Debug and Release.
- The GitHub Actions workflow was structurally reviewed and locally validated for a clean runner, but it has not run on GitHub.
- Production boundaries remain clean; no existing diagnostics or N15-C/D/E runtime/networking scope changed.

Verification was performed on `refactor/mod-lifecycle` at unchanged HEAD `4ddb2ca893e29adab0db39b2a0747d45641c1b3f`. The implementation was unstaged/uncommitted. N15-B is not recorded as committed, merged, or CI-successful.

## Current priority after N15-B integration — N15-C

**Status:** Next planned test-infrastructure increment after the user commits and merges N15-B; do not begin it inside the N15-B documentation/PR.

**Objective:** Continue the approved incremental test-infrastructure plan from the isolated Level 1 foundation. Preserve the three-level model, clean production boundaries, and incremental treatment of the existing 92 diagnostics. Define N15-C's exact test candidates and acceptance evidence from the authoritative repository before implementation; do not infer runtime coverage from N15-B.

The previously listed synchronization integration testing remains necessary Level 3 work, but it is no longer the immediate roadmap priority after N15-B. When resumed, begin with a controlled run covering mod startup, configured transport, session entry, world load, and synchronization completion; record the exact build/commit and do not infer synchronization success from connection-only evidence.

**Remaining integration/release gates:**

- Complete the runtime synchronization and transport scenarios applicable to the supported feature set; preserve unsupported/deferred domains such as Sweepy and incomplete special-workable coverage as explicit limits.
- Resolve remaining legal/compliance and copied-source provenance questions, and complete any approved repository-history remediation before release.
- Select a clean release commit, build that exact commit, construct and inspect the actual distribution artifact, and verify required notices and excluded files.
- Run the agreed regression suite on that exact artifact; document platform/scenario coverage and do not claim release readiness before these gates pass.

## Baseline to preserve

The September 27 ordinary synchronized StandardWorker multitool test passed: 65/65 client jobs reached matching local ToolTarget callbacks and all 65 target positions matched the host. During active worker playback, 106 Navigator idle attempts were blocked. Genuine transition animations continued in that ordinary-work run. These results do **not** verify deliberate navigation interruption or special workables.

Milestone 2 subsequently proved that the overwrite came from late outer `Navigator.Stop` Postfix publication after synchronous nested arrival flow had already reached `StartWork`. Successful arrival is now published before that synchronous processing; the nested cleanup Stop for the same arrival is suppressed; the worker-playback idle guard was removed. The build succeeded, and the final runtime run matched 33 host and client starts with no active-worker Navigator-idle requests.

Preserve the existing host-authoritative model, client-local vanilla lifecycle where practical, and the distinction between genuine Navigator transitions, active worker playback, and Navigator idle fallback. Preserve correct semantic publication ordering rather than restoring the removed guard without new contradictory evidence.

## Release gate — 0.8.0.4

**Status:** Complete. The user confirmed that 0.8.0.4 was released successfully.

The October 3 integration audit remains historical evidence of how the candidate was assembled. The exact final release commit/tag/hash was not independently captured in the Project and is intentionally not invented.

Release-scope outcomes:

- Storage `ClearStorage` received the narrow null/destroyed-entry guard; subsequent paired regression did not reproduce the old exception, with an explicit positive-control trace limitation.
- Sweepy synchronization support was deferred. Sweepy remains an unsupported/missing feature rather than being added to the synchronization stack without dedicated testing.
- The user's custom Operational/power work was removed and the upstream fix was merged, according to the release report.
- ONI compatibility metadata is handled through the normal game update/build-generated metadata workflow.
- Historical Navigator, StandardWorker, and AnimSyncer evidence remains preserved for its original tested builds.

The completed release does not imply that all deferred synchronization features are solved.

## Deferred — development-environment walkthrough

**Status:** Not part of N14 and not the next development focus. Reassess separately if the tooling/configuration foundation still needs a dedicated walkthrough.

**Objective:** Review the project from its development-environment foundation, then proceed through the mod lifecycle while testing each stage.

Start with project setup and baseline developer experience, including ESLint, default settings, and related tooling/configuration. Record which settings are authoritative, inherited, generated, or local-only before changing them. After the environment baseline is understood, walk through initialization, connection, loading/synchronization, gameplay admission, ordinary runtime behavior, and shutdown/disconnection in order.

The October 16 N9–N11 cleanup sequence is recorded as committed, with the associated identifier audit and source build reported as passing. Exact hashes and full command output are not present in the uploaded Project evidence, so they remain evidence details to capture from the authoritative repository rather than values to infer.

The reported runtime result was **connection only**. It passed for that scope, but it does not satisfy later lifecycle stages. No additional standalone connection test is currently required; connection can be re-exercised naturally during the lifecycle walkthrough.

**Acceptance:** For each reviewed stage, record the exact source/Git state, configuration findings, build result, runtime scenario, and evidence limits. Do not promote a connection-only result to loading, synchronization, gameplay, reconnect, or release-artifact verification.

## Historical completed track — PR #83 lifecycle/readiness and join overlay

**Status:** Historical implementation merged and LiteNetLib single-join runtime verified; later superseded as the normal-load mechanism by the rebuilt transport-preserving lifecycle.  
**Objective at the time:** Preserve the joining client in the host readiness barrier across the intentional save-load disconnect/reconnect and make host-owned readiness the authoritative join-overlay lifetime.

The PR #83 lifecycle deliberately disconnected during `LoadingWorld`, reconnected from cached session information, retained a pending join for stable-identity transports, and completed at final Ready. The single-joining-client LiteNetLib flow passed at runtime; Steam remained source-only and Riptide was excluded from pending-join correlation.

Preserve this evidence for regression history, especially the final-Ready/readiness and overlay-lifetime lessons. Do **not** restore its pending-join/loading-cache/cached-reconnect machinery merely because it was once verified: later `develop` work removed those mechanisms and rebuilt normal world loading around a transport-preserving lifecycle.

GitHub verified PR #83 merged into `develop` on 2026-10-07 at merge commit `c1c93cb5ab2ae839d28d36d32eff9a565b4f111f`. That merge remains historical provenance, not the current release baseline.

## Historical release-preparation sequence — final upstream-tracking release

**Status:** Preserved point-in-time plan for PR #63 / 0.8.0.5. Its branch and PR disposition are not current-state evidence; revalidate GitHub and the authoritative repository before using it as an active release plan. No final artifact should be inferred from this historical section.
**Objective:** Complete the final upstream-tracking release under the verified MIT boundary, resolve the remaining compliance blockers, and verify the exact artifact before release. Future releases form an independent project line.

### Verified upstream license boundary

- The last upstream parent imported into this release is `1c9d7226c7f9dbde7c303a11af4adf5fd5c2ffbb` through merge `88e7f06cb3c2f1d3cabdc389778bb8c083061016` on 2026-10-05.
- Both parents of that merge carried the MIT License.
- Upstream's restrictive license transition occurred on 2026-10-11 and is not an ancestor of current `develop`.
- PR #63 / 0.8.0.5 is the final release permitted to track upstream as an ordinary integration policy.
- Post-transition upstream changes must not be imported into the independent project unless the specific code is available under compatible terms or written permission is obtained.

**Required order:**

1. Finish and review the legal/compliance files and packaging changes without treating documentation work as proof of artifact compliance.
2. Resolve or replace copied source whose permission is not yet documented, including FUI helpers attributed only to Aki/Sgt_Imalas and the gist-derived helper.
3. Preserve the verified `ASSET_PROVENANCE.md` record: every currently tracked image and asset bundle is identical to the last imported MIT upstream snapshot. Perform a new provenance review for any asset added or changed after that baseline.
4. Approve and execute a separate repository-history remediation plan for historically committed ONI, Unity, and Steam binaries. Run a historical secret scan before rewriting public history.
5. Settle and record the exact intended release-source baseline, distinguishing PR #63 head, staged working-tree documentation, merge commit, and final clean release commit.
6. Build the exact clean release commit, construct the real distribution artifact, and inspect it for required notices and prohibited binaries.
7. Run the agreed runtime regression suite on that exact artifact.
8. Release only after the exact commit/version/tag and evidence limits are recorded.

**Release gate:** PR #63 disposition; exact clean baseline; complete notices and copied-source provenance; preservation of the verified inherited-asset baseline; approved repository-history remediation; exact-commit successful build; inspected artifact; agreed runtime regressions on that artifact; remaining platform/scenario limits documented. Historical tests and the lifecycle/speed build do not verify the future release artifact.

## Rebuilt transport-preserving lifecycle — conformance and runtime audit deferred

**Status:** Later `develop` contains the rebuilt transport-preserving client lifecycle; detailed October 7 design conformance and the full cross-transport runtime matrix remain to be re-audited. This is not the current next-session release-preparation task.  
**Objective:** Preserve normal transport/session continuity through world replacement while keeping gameplay blocked until synchronization completion, without restoring the superseded PR #83 pending-join/reconnect machinery.

Verified Git history for the current line includes the lifecycle rebuild followed by removal of the loading-client cache, pending joins, legacy cached connection logic, and `readyToProcess`, plus later connection cleanup. The current lifecycle uses `Disconnected -> Connecting -> Connected -> LoadingWorld -> InGame` and preserves transport through normal synchronization/world replacement.

The October 7 detailed design remains a **conformance checklist**, not a guaranteed description of every current implementation detail. A future focused audit should verify, against current source, the equivalents of:

1. old-world networking-state invalidation during replacement;
2. loading-time gameplay packet gating and wrapper classification;
3. identity/OxySync graph reconstruction before gameplay admission;
4. the exact Ready/all-ready completion boundary;
5. stale generation/attempt protection;
6. actual-transport-loss reconnect fallback;
7. host pause coverage across the full snapshot/load/admission interval.

The pause-acquisition location itself is no longer unknown: the host pauses in `GameServerHardSync.PerformHardSync` after accepted `BeginSynchronization`, and the client pauses in `GameClient.BeginSynchronization`, with no automatic completion unpause. What remains to establish is full temporal coverage and runtime behavior on the exact current/release artifact.

**Deferred verification gate:** When this track is revisited, source-audit the current implementation first, then run the smallest cross-transport/runtime matrix justified by what is actually present. Do not mark older PR #83 tests as verification of the rebuilt or future release artifact.

## Superseded release-integration plan — develop into experiment

**Status:** Superseded as the immediate release plan by the current verified `develop` ancestry, PR #86 review, exact-baseline selection, compliance audit, and exact-artifact gate. Preserve this section as historical planning context; do not execute it without revalidating current Git state.  
**Objective:** Integrate the merged lifecycle/readiness work from `develop` into `experiment`, then prepare the next release without treating historical tests as verification of the resulting artifact.

**Required sequence:**

1. On the authoritative Mac repository, record branch, clean/dirty state, `develop` HEAD, `experiment` HEAD, and PR #83 merge ancestry.
2. Review the `develop`→`experiment` diff and resolve conflicts according to the established downstream architecture; do not automatically reshape `develop` to match upstream.
3. Build the exact integrated commit and run the agreed join/readiness regression on that exact artifact.
4. At minimum, re-run the LiteNetLib single-join path. If practical, add the host + existing Client A + joining Client B scenario and overlapping joins.
5. Keep Steam support source-only unless a real two-account runtime test becomes available. Do not imply Riptide pending-join support.
6. Record the exact final release commit/tag/version only after established, and do not promote PR-head or historical runtime evidence to release verification.

**Release gate:** clean authoritative release commit; successful exact-commit build; LiteNetLib regression on that artifact; unresolved three-peer/overlapping/Steam/Riptide limits explicitly documented; no misleading player-left message on intentional loading disconnect.

Issue #82 remains separate: first define an authoritative permanent-departure event across leave/kick/lobby/transport paths. Intentional `LoadingWorld` disconnect must not emit a leave notification, and duplicate notifications must be prevented.

### Deferred client-lifecycle cleanup discovered during the audit

Track separately from the completed readiness fix and the next release integration:

- Riptide duplicate `OnDisconnectedFromHost()` and timeout path missing `OnClientDisconnected`.
- Steam remote/failure path leaving `GameClient.State`/connection handle stale.
- LiteNetLib explicit disconnect dependence on library callback delivery.
- direct `ReconnectToSession()` paths skipping `Connecting`.
- broader idempotence of shared connect/disconnect notifications.


## Milestone 1 — Clean up diagnostic logs

**Status:** Planned; independent small task.  
**Objective:** Reduce routine animation/Reactable log noise without losing useful fault diagnostics or changing behavior.

**Tasks:** Inventory temporary logs; remove or revise obsolete guard-related diagnostics; retain selectively enabled diagnostics for Navigator/worker lifecycle, IDs, transition causes, and playback cleanup. Avoid functional refactoring.

**Acceptance:** Build succeeds; logs are appropriately quiet during ordinary work; diagnostics can be enabled for the interruption experiment; the known multitool regression does not reappear. Record actual tests before marking complete.

## Milestone 2 — Correct Navigator arrival publication ordering

**Status:** Complete.  
**Objective:** Determine why Navigator idle overwrote `StandardWorker` playback and correct the synchronization boundary without relying on a worker-playback idle guard.

**Verified root cause:** The outer `Navigator.Stop` Postfix published arrival idle only after synchronous nested arrival processing had already reached `StartWork`, so the late idle could overwrite the work animation.

**Implemented correction:** Publish successful arrival Stop before synchronous arrival processing can reach `StartWork`; suppress publication of the nested cleanup Stop for that same arrival; preserve publication of genuine unrelated Stops by design; remove the client worker-playback Navigator-idle guard. The build succeeded. The final logs did not independently classify unrelated Stops.

**Runtime verification:** 33 host `StartWork` publications matched 33 client applications exactly; 31 clearly observed arrival-idle-to-worker-start cases were ordered correctly; zero Navigator-idle requests occurred during active worker playback with the guard absent; 225 active-work animation checks were recorded; 31 intervals completed and two remained active only at disconnect; zero errors or assertions occurred.

**Evidence limit:** Exact host Stop publication counts, exact navigation sequence continuity, and classification of unrelated Stops were not trace-verified because those diagnostics were disabled. This limits trace reconstruction but does not block completion of the tested behavioral fix.

## Milestone 3 — Test automatic worker interruption

**Status:** Planned; deferred behind the current PR #86 / legal-compliance / exact-artifact release-preparation work.  
**Objective:** Verify that genuine navigation changes interrupt client worker state, not merely change the visible animation.

**Scenarios:** Active ordinary work followed by fall, forced displacement, ladder transition, swimming exit, cancellation during a transition, and other convenient special transitions. Confirm that a subsequent job can start normally.

**Observe:** Transition start timing; worker lifecycle termination; `IsInWorkerPlayback` cleanup; active workable NetId cleanup; final locomotion/work animation; host/client correspondence.

**Experiment:** Start from the completed Milestone 2 implementation with correct arrival publication ordering and no worker-playback idle guard. Deliberately trigger genuine navigation interruptions and compare host/client lifecycle, cleanup, and animation state. Do not restore the old guard unless new evidence demonstrates a separate need.

**Acceptance:** Genuine transitions interrupt promptly, no stale worker playback or workable ID remains, no stuck animation occurs, and ordinary multitool behavior remains intact. Failed or unrun cases remain explicitly open.

## Milestone 4 — Investigate and fix missing sleep/eating animations

**Status:** Planned; separate lifecycle work.  
**Objective:** Identify why sleep and eating presentation is missing on clients without assuming either activity uses the ordinary StandardWorker path.

**Tasks:** For each activity, trace host initiation, vanilla activity/animation owner, transmitted lifecycle boundary, client-local execution, completion, and interruption. Inspect special workables and overrides as appropriate. Test an activity already in progress when a client joins, since persistent presentation state differs from a one-time start event.

**Acceptance:** Host/client activity and animation correspond during start, ongoing activity, interruption, and completion; joining during an ongoing activity is tested or explicitly documented as unresolved. Avoid activity-specific RPCs until the missing semantic boundary is established.

## Milestone 5 — Review and consolidate animation architecture

**Status:** Planned; depends on findings from Milestones 2–4.  
**Objective:** Decide whether narrowly scoped fixes suffice or an enduring ownership/lifecycle refinement is justified.

**Tasks:** Compare findings with the existing architecture and decisions; distinguish transient events, long-lived presentation state, and authoritative simulation effects; identify shared patterns without rewriting working syncers unnecessarily.

**Acceptance:** Any proposed design change includes rationale, alternatives, migration risk, and evidence. Obtain confirmation before substantial architecture changes. Update local module architecture and Project references only when warranted.

## Session workflow

Use one focused task per development session. For local vanilla source, repository code, builds, logs, or local architecture, manually open **Local game mod** through Work and bring the resulting evidence back to the main **game mod** Project. Automatic handoff from this Project is not relied upon because it has failed in practice; prepare and paste a focused inspection prompt instead. Do not assume the two Projects automatically share files or findings.

At session end, review `ARCHITECTURE.md` for enduring design changes, `DECISIONS.md` for decisions and rationale, `STATUS.md` for verified fixes/tests/issues and next milestones, and relevant local full-mod/module architecture files. Preserve provenance and revision history. Do not claim tests, Git commits, or saved updates without verification.

For the completed Milestone 2 change, the local documents requiring separate inspection are `ONI_Multiplayer_Animation_Architecture.md` and `ONI_Together_Full_Mod_Architecture.md`. They were not available in this Project update and were not modified.

The current lifecycle/speed and session-centralization decisions may also affect the maintained local full-mod architecture and any networking/session or lifecycle module architecture documents. Those local files were not accessible here and require inspection in **Local game mod** before reconciliation; no local document update is claimed.

## Next-session starter prompt

> After N15-B is committed and merged, begin N15-C from the authoritative repository. First record the exact branch, HEAD, and clean/dirty state and confirm the merged N15-B baseline. Preserve the Level 1 standalone unit / Level 2 standalone integration / Level 3 in-game model, Option 3 separate test-project structure, clean production boundaries, and incremental migration of the existing 92 diagnostics. Define N15-C's exact scope and acceptance evidence before implementation. Do not treat the five N15-B `SnapshotTimeline` tests or local workflow validation as ONI runtime, GitHub Actions, or release-artifact verification.

## Revision history

- **2026-10-17:** Added the independently verified but uncommitted N15-B Option 3 foundation, its five Debug/Release `SnapshotTimeline` passes and local-only workflow validation, and changed the post-merge next priority to N15-C. Preserved synchronization integration as required Level 3 work rather than claiming it complete.
- **2026-10-17:** Closed N14 implementation with the verified startup/framework commit series and build-only evidence; made synchronization integration testing the next development focus; recorded runtime, compliance, and exact-artifact release gates without claiming runtime verification or release readiness.
- **2026-10-16:** Recorded the completed N9–N11 identifier-migration cleanup sequence, reported passing audit/build evidence, and the passing connection-only runtime result with strict scope limits. Replaced the immediate next-session release-preparation prompt with a development-environment-first walkthrough beginning with ESLint, default settings, and baseline tooling, followed by lifecycle-stage testing. Exact cleanup hashes and full audit/build output remain to be captured from the authoritative repository.
- **2026-10-15 (reconciliation):** Reclassified PR #83 lifecycle/readiness as historical verified evidence, recognized the later transport-preserving lifecycle rebuild in current `develop`, converted the old unimplemented transport-preserving milestone into a deferred conformance/runtime audit, and kept PR #86 review plus legal/compliance and exact-artifact verification as the active release path.
- **2026-10-15:** Replaced the stale next-session transport-preserving-loading priority with the current release sequence: PR #86 regression review, exact baseline selection, legal/licensing/compliance audit, approved remediation, exact-artifact build/runtime verification, then release. Retained the transport-preserving design as deferred and preserved the older `develop`→`experiment` plan as superseded historical context.
- **2026-10-07:** Made transport-preserving normal world loading the active milestone. Added the host pause-or-catch-up prerequisite, ordered implementation plan, diagnostics, incremental build/test expectations, LiteNetLib plus Steam completion gate, and scope controls; deferred `develop` → `experiment` release integration behind this work.
- **2026-10-07:** Removed stale wording that treated the joining-sequence investigation as still active; Milestone 3 is now explicitly deferred behind the planned `develop` → `experiment` integration and exact-artifact release verification.
- **2026-10-07:** Marked the lifecycle/readiness/join-overlay task implemented and merged into `develop` via PR #83, with LiteNetLib single-join runtime verification, Steam source-only support, and Riptide exclusion. Added the next-session `develop`→`experiment` integration and exact-artifact release gate. Preserved open verification work for three-peer/overlapping joins and kept issue #82 separate.
- **2026-10-05:** Completed the first transport identity/client-lifecycle audit enough to narrow the active task. Recorded LiteNetLib runtime-verified persistent-ID load/reconnect behavior, Riptide bootstrap startup verification, Steam source-only lifecycle status, the staged stale-close guard, and the cross-driver loading/state defects. Changed the next-session priority from broad client joining to the world-loading sequence; general client-state normalization remains deferred unless required by the loading fix.

- **2026-10-04:** Marked the 0.8.0.4 release gate complete based on the user's confirmed release; preserved exact-hash uncertainty; recorded Storage acceptance, Sweepy deferral, removal of custom Operational/power work with upstream fix merged, and generated compatibility-metadata workflow; made client joining-sequence reconstruction/improvement the immediate active priority while keeping animation and special-feature milestones deferred.

- **2026-10-03:** Added the 0.8.0.4 release-candidate gate as the immediate priority ahead of Milestone 3; recorded clean-HEAD versus staged-build separation and current blockers; required exact-hash release verification; and changed the local workflow to manual Local game mod switching because automatic Work handoff is not reliable.
- **2026-09-28:** Marked Milestone 2 complete with its root cause, semantic publication-order fix, removed guard, successful build, runtime evidence, and trace limitations. Made Milestone 3 genuine navigation interruption the next milestone.
- **2026-09-27:** Initial roadmap created from the agreed five-stage plan. No new implementation or tests claimed.
