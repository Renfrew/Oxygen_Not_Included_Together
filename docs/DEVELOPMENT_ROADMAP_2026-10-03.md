# Oxygen Not Included Together — Animation Development Roadmap

**Created:** 2026-09-27  
**Last updated:** 2026-10-03  
**Status:** Planning and progress guide; verified results retain the evidence limits stated here and in `STATUS.md`  
**References:** `ARCHITECTURE.md`, `DECISIONS.md`, `STATUS.md`

## Baseline to preserve

The September 27 ordinary synchronized StandardWorker multitool test passed: 65/65 client jobs reached matching local ToolTarget callbacks and all 65 target positions matched the host. During active worker playback, 106 Navigator idle attempts were blocked. Genuine transition animations continued in that ordinary-work run. These results do **not** verify deliberate navigation interruption or special workables.

Milestone 2 subsequently proved that the overwrite came from late outer `Navigator.Stop` Postfix publication after synchronous nested arrival flow had already reached `StartWork`. Successful arrival is now published before that synchronous processing; the nested cleanup Stop for the same arrival is suppressed; the worker-playback idle guard was removed. The build succeeded, and the final runtime run matched 33 host and client starts with no active-worker Navigator-idle requests.

Preserve the existing host-authoritative model, client-local vanilla lifecycle where practical, and the distinction between genuine Navigator transitions, active worker playback, and Navigator idle fallback. Preserve correct semantic publication ordering rather than restoring the removed guard without new contradictory evidence.

## Release-candidate gate — 0.8.0.4

**Status:** In progress; supersedes Milestone 3 as the immediate priority.  
**Integration baseline:** clean `experiment` HEAD `0bec442e13893b4da113b363a379f9fe2b74be3a` reports `0.8.0.3`; the local staged candidate reports `0.8.0.4` and includes nine staged paths. The currently inspected October 3 runtime logs therefore do not verify the clean HEAD artifact.

**Objective:** Produce a clean, documented, exact-hash release candidate and close known integration blockers before tagging/releasing.

**Required work:**

- Decide which staged source/logging/documentation changes belong in the release and create a clean release commit.
- Fix and retest the remaining Pickupable generic `WorkableSyncer.CompleteWork` path; do not treat the StandardWorker exclusion as a complete Pickupable fix.
- Resolve Reactable authorization cases where `NetId == 0`.
- Decide whether Sweepy should have/require `NavigatorSyncer`, then eliminate or explicitly justify the current missing-syncer diagnostics.
- Investigate the separate storage-rebuild exceptions in the October 3 logs.
- Review ONI supported-build metadata: project target `736649` versus tested game build `744825`.
- Build Release from the exact clean release hash and perform paired host/client regression for Navigator arrival, ordinary StandardWorker, Pickupable/autosweeper, AnimSyncer symbol visibility, Reactables, game speed/pause, Operational/power state, and scenario spawning.

**Evidence rule:** Preserve previous verified Navigator, StandardWorker, and AnimSyncer results as historical evidence for their tested builds. The final release commit receives its own verification record; do not infer it passed merely because the same code path was tested earlier.

**Acceptance:** Clean working tree; release version/compatibility metadata intentionally set; blockers resolved or explicitly documented as accepted known issues; Release build succeeds from the exact release commit; paired host/client release matrix completes with evidence; release notes and Project/local documentation match the tested artifact.

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

**Status:** Planned after the release-candidate gate.  
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

## Next-session starter prompt

> Continue the 0.8.0.4 release-candidate gate. Work from the authoritative Mac `experiment` repository. First preserve/report the exact current HEAD and staged state. Focus on one unresolved release blocker at a time, beginning with the remaining Pickupable generic `WorkableSyncer.CompleteWork` failure unless newer evidence shows a more fundamental blocker. Do not redesign unrelated synchronization. After each fix, build and run the smallest paired host/client verification needed. Keep historical tests separate from exact-release-commit evidence, and do not tag/release until a clean release commit has passed the agreed regression matrix.

## Revision history

- **2026-10-03:** Added the 0.8.0.4 release-candidate gate as the immediate priority ahead of Milestone 3; recorded clean-HEAD versus staged-build separation and current blockers; required exact-hash release verification; and changed the local workflow to manual Local game mod switching because automatic Work handoff is not reliable.
- **2026-09-28:** Marked Milestone 2 complete with its root cause, semantic publication-order fix, removed guard, successful build, runtime evidence, and trace limitations. Made Milestone 3 genuine navigation interruption the next milestone.
- **2026-09-27:** Initial roadmap created from the agreed five-stage plan. No new implementation or tests claimed.
