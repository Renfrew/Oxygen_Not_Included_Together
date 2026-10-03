# Oxygen Not Included Together — Development Status

**Last updated:** 2026-10-03  
**Scope:** Latest status recoverable from the `game mod` ChatGPT Project, the local reference documents, and Local game mod implementation/build/log reports. This revision establishes the October 3 merged `experiment` integration baseline and keeps historical runtime evidence separate from exact-release-commit verification.

## Executive summary

- **Merged integration baseline:** `experiment` HEAD `0bec442e13893b4da113b363a379f9fe2b74be3a`, tracking `origin/experiment` with ahead/behind 0/0 at audit time.
- **Release tree was not clean:** nine staged paths existed. Clean HEAD reports version `0.8.0.3`; staged state bumps to `0.8.0.4`.
- The October 3 host/client logs report `0.8.0.4`, so they describe the staged state rather than exact clean HEAD.
- **Release is blocked** pending resolution/decision on Pickupable generic `WorkableSyncer` completion failure, Reactable `NetId == 0` authorization failures, Sweepy navigation eligibility/missing `NavigatorSyncer`, storage-rebuild exceptions, and exact-hash release regression testing.
- Historical verified results remain evidence for their tested builds: ordinary StandardWorker multitool targeting (65/65), Navigator arrival ordering (33/33 StartWork correspondence), and prior AnimSyncer reliable symbol delivery for already-registered entities (136/136 synchronized applications with no reorder/duplicate/value mismatch). They do not by themselves verify the final release commit.
- The existing architecture and prior conversations were accessible and recovered.
- The missing primary source, `ONI_Together_Full_Mod_Architecture.md`, was recovered from the refreshed `origin/experiment` branch and now governs the whole-mod target architecture.
- Ordinary synchronized `StandardWorker` multitool targeting is strongly verified: **65/65** client jobs reached local `ToolTarget` callbacks and **65/65** target positions matched the host.
- Pickupable is a verified special-workable synchronization defect: `StandardWorkerSyncer` excludes `Pickupable` / `PickupableStartWorkInfo`, so the client lacks the corresponding `StandardWorker` / `MultitoolController` lifecycle even though `WorkableSyncer` still replays workable-level events.
- Milestone 2 is complete. The worker-animation overwrite root cause was a late outer `Navigator.Stop` Postfix publication after synchronous nested arrival flow had already reached `StartWork`.
- Successful arrival publication now occurs before synchronous arrival processing; nested same-arrival cleanup Stop publication is suppressed; the worker-playback Navigator-idle guard was removed.
- The host-authorized, client-local Reactable design is present in current source and uses one-shot `(reactableId, reactorNetId)` authorization. It remains provisional pending broader timing and reaction coverage.
- Revisioned immediate game-speed synchronization is present in current source.
- Operational/power synchronization remains present in `experiment`; PR #53 revised its client fallback behavior. The separate full-removal commit is not merged into `experiment`.
- **Immediate development priority:** finish the release-candidate gate before returning to Milestone 3 deliberate navigation-interruption testing.

## Verified fixes and results

### Ordinary multitool / worker animation

Recovered September 27 verification results:

- 65 ordinary synchronized client jobs reached `ToolTarget.SetTargetPos`.
- 65 ordinary synchronized client jobs reached `ToolTarget.UpdateWorkTarget`.
- Every client event matched the host worker NetId.
- Every client event matched the host workable NetId.
- **65/65 target positions matched the host.**
- Position mismatches: **0**.
- Stale or unmatched client targets: **0**.
- The earlier failure pattern—worker pre-animation immediately overwritten by `idle_default` or `treading_loop`—was absent.
- 106 Navigator idle attempts were blocked while worker playback was active.
- Legitimate floor, swimming, ladder, pole, and other transition animation calls continued.
- Completed jobs cleared worker playback normally.
- No relevant exceptions, asserts, or crashes were found for `StandardWorker`, `NavigatorSyncer`, `AnimSyncer`, `KAnimControllerBase`, `AnimEventHandler`, `MultitoolController`, or `ToolTarget`.

**Assessment:** Fixed for the tested ordinary synchronized `StandardWorker` set. Do not reopen the successful ordinary multitool investigation without new contradictory evidence. This result does not prove deliberate real-transition interruption cases or special workables.

### Pickupable special-workable synchronization defect

Source and paired host/client diagnostics support the following findings:

- `StandardWorkerSyncer` explicitly excludes `Pickupable` / `PickupableStartWorkInfo` from its synchronized worker path.
- The host therefore runs the real Pickupable `StandardWorker` and `MultitoolController` lifecycle, while the client does not receive the corresponding `StandardWorker` `RpcStartWork` and does not construct that Pickupable worker/multitool lifecycle.
- `WorkableSyncer` still replays workable-level events on the client. This does not replace the missing worker lifecycle.
- Client Pickupable completion has thrown because `GetStartWorkInfo` is null.
- Paired diagnostics showed host Pickupable pose selections with no corresponding client `StandardWorker` `RpcStartWork` or Pickupable pose selection.
- Later ordinary Storage synchronization can still match between host and client despite the preceding missing Pickupable lifecycle. That later match does not establish that Pickupable itself synchronized correctly.

**Assessment:** The Pickupable lifecycle defect is verified for the inspected source/log scope. It is separate from the verified ordinary synchronized-work result.

Two future work tracks must remain distinct:

1. **Proper lifecycle synchronization:** implement and verify a Pickupable-specific `StandardWorker` lifecycle path in a later session. This work is deferred and has not been implemented or tested.
2. **Temporary visual-only fallback:** investigate using the existing `AnimSyncer` for explicitly skipped work types, starting narrowly with Pickupable, and transmit only any additional visual state required for facing/direction/target presentation. This is a proposal only; it is unimplemented, unverified, and is not a finalized architectural decision.

### Milestone 2 — Navigator arrival-publication ordering

Verified implementation and build result:

- The root cause was proven as the outer `Navigator.Stop` Postfix publishing arrival idle too late, after synchronous nested arrival processing had already reached `StartWork`.
- Successful arrival Stop publication was moved before synchronous arrival processing can reach `StartWork`.
- Publication of the nested cleanup Stop for the same arrival was suppressed; genuine unrelated Stops remain intended to synchronize.
- The client guard that blocked Navigator idle during active worker playback was removed.
- The build succeeded.

Final runtime verification:

- **33/33** host `StartWork` publications matched client `StartWork` applications exactly, with no entity, workable, or order mismatches.
- **31** clearly observed arrival-idle-to-worker-start cases applied Navigator idle before Worker Start.
- Navigator-idle requests during active worker playback with the guard absent: **0**.
- Active-work animation checks: **225**.
- Completed worker-playback intervals: **31**; two more were still active only when the client disconnected.
- Errors/assertions: **0**.

**Trace limitation:** Exact host Stop publication counts, exact navigation sequence continuity, and classification of unrelated Stops were not trace-verified because those diagnostics were disabled in this build. The runtime result supports the fixed behavior but does not supply those exact trace proofs.

**Assessment:** Milestone 2 is complete and the Navigator arrival-ordering bug is fixed for the tested scope. Milestone 3 deliberate genuine-navigation interruption is next.

### Reactable authorization

Previously verified vanilla lifecycle behavior:

```text
client stores local Reactable
→ ReactionMonitor.GoTo(reacting)
→ IsReacting becomes true
→ Reactable.Begin fires
```

The later preferred architecture is now visible in source:

- `Reactable.Initialize` registers a local instance in `ReactableSyncer`.
- Host successful `CanBegin` sends reliable authorization.
- Client `CanBegin` requires both `InternalCanBegin(...)` and a matching one-shot authorization.
- `Reactable.Begin`/`End` enter synchronized animation/override scopes.

**Assessment:** The preferred host-authorized, client-local, one-shot design is implemented but remains provisional. The earlier state-machine observation verifies that the client-local vanilla reaction lifecycle can reach `Reactable.Begin()`; it does not verify the complete multiplayer authorization exchange. Broader end-to-end coverage remains incomplete.

### Game speed

Current source contains `GameSpeedSyncer` with:

- immediate command/RPC propagation,
- host validation of the three supported speeds,
- monotonic revisions,
- stale-revision rejection,
- nested/re-entrant apply-depth protection,
- join/resync request behavior.

**Assessment:** Implementation present. No fresh test evidence was generated while preparing this document.

### Initialization crash architecture

Previous investigations traced unrelated building crashes to an unsafe client `Operational` shadow/getter architecture that could alter derived state during prefab initialization before dependent components finished `OnSpawn`.

The October 3 repository audit corrected the earlier Project assumption that this synchronization had been removed from `experiment`. The full-removal commit `484bc805dd13e1755b2648192c55b23bd1e7b11e` exists only on `ren/test-0.8.0`. Current `experiment` still contains the Operational packets/patches/receiver, with PR #53 changing the client path so vanilla/local state is used until host state is available.

**Assessment:** The unsafe initialization-time behavior remains architecturally rejected, but Operational synchronization itself is present in the release candidate and requires paired release testing. Longer-term input-level synchronization remains a possible refinement, not the current implementation status.

## Current implementation snapshot

| Area | State | Confidence |
| --- | --- | --- |
| Host authority / client replay | Established core architecture | High |
| Stable entity identity / registry | Present | High |
| Save transfer / hard sync | Existing mod feature | Reported/repository status; not retested here |
| Navigator transition replay | Present | High |
| Navigator semantic arrival publication ordering | Present; build succeeded; runtime-verified for tested arrival-to-work cases | High for tested scope; exact Stop/sequence trace unavailable |
| StandardWorker lifecycle sync | Present and verified for ordinary work | High |
| Local multitool target reconstruction | Verified 65/65 | High for tested set |
| Pickupable StandardWorker lifecycle sync | Excluded/missing on client; defect supported by source and paired diagnostics | High for inspected scope; fix not implemented |
| Reactable one-shot authorization | Present | Medium; broader tests needed |
| Reaction presentation/state separation | Architectural target | Medium / partial |
| Game-speed revisioned RPC | Present | Medium-high; not freshly tested here |
| Operational/power synchronization | Present in `experiment` with PR #53 fallback behavior; release verification required | Medium-high for source presence; runtime release behavior not yet verified |
| Session/epoch envelope | Whole-mod target; implementation coverage not audited here | Unknown/partial |
| Command IDs, results, deduplication | Whole-mod target; implementation coverage not audited here | Unknown/partial |
| Consistent save cursor + catch-up journal + ready barrier | Whole-mod target | Not verified |
| Domain coverage ledger, versions, hashes, targeted recovery | Whole-mod target | Not yet inventoried |
| Compatibility fingerprint | Existing mod validation is reported; full target handshake not audited | Partial/unknown |

## Known limitations and unresolved issues

### Worker/Navigator

- Real-transition interruption has not yet been deliberately stress-tested across all important cases.
- Pickupable is a confirmed skipped-workable defect: the client lacks its `StandardWorker` / `MultitoolController` lifecycle while workable-level events still replay. Other special/skipped workables must also be assessed separately.
- Proper Pickupable lifecycle synchronization is deferred. The possible `AnimSyncer` visual fallback is only an unimplemented, unverified proposal and must not be treated as the final lifecycle design.
- Exact host Stop publication counts, navigation sequence continuity, and classification of unrelated Stops remain unverified because the required diagnostics were disabled during the final run.
- Debug logging is still enabled in some animation/Reactable patches and may be too noisy for normal builds.

### Reactables

- Authorization storage has one-shot consumption but no explicit expiry/TTL.
- A `HashSet` collapses duplicate simultaneous authorization with the same key.
- Packet-first timing is supported by storage; discovery-first timing still depends on vanilla retry/poll behavior.
- The `(reactable.id.hash, reactorNetId)` key may need more context for ambiguous cases.
- Some Reactables change equipment or world state in `Begin`, `Run`, or `End`; presentation replay must not double-apply those mutations.
- Suit/equipment convergence and late-join behavior remain important acceptance tests.

### Persistent activity and reconciliation

- The final snapshot schema for long-lived presentation activity is unresolved.
- Ordering/versioning between state deltas and presentation events needs a documented boundary.

### Whole-mod target architecture gaps

- Current code has not yet been inventoried against the primary architecture’s ownership/coverage ledger.
- Session ID, baseline epoch, per-stream sequence/version, and old-epoch rejection need an implementation audit.
- Join/hard-sync behavior needs verification against the `T/J` consistent-cut, catch-up journal, and readiness-barrier design.
- Entity identity persistence/baseline mapping for existing and nested items remains a gating design spike.
- Command IDs, host deduplication, explicit rejection reasons, and reconnect boundaries need a domain-by-domain audit.
- Region/entity hashes and targeted refresh behavior are target architecture, not confirmed universal implementation.
- Queue, payload, save-transfer, journal, and per-tick budgets require measurement in representative colonies.

### Power/Operational

- Operational/power synchronization remains present in `experiment`; do not describe it as removed.
- PR #53's fallback behavior needs paired release-candidate testing across initialization and normal state changes.
- Input-level synchronization remains a possible longer-term design direction and should not be presented as current implementation.

## Release-candidate gate (2026-10-03)

The current release target is staged version **0.8.0.4**, but no tag should be created until a clean release commit is established and that exact hash is built/tested. The audit found that the authoritative Mac checkout had nine staged paths, so the tested `0.8.0.4` artifact is not identical to clean HEAD `0bec442...` (`0.8.0.3`).

Current blockers/evidence requiring closure:

1. **Pickupable:** generic `WorkableSyncer.RpcUpdateWorkable` can still call `Pickupable.OnCompleteWork` on the client and throw `NullReferenceException`; the StandardWorker exclusion does not solve this second path.
2. **Reactables:** current paired logs show repeated authorization failures where `NetId == 0`.
3. **Sweepy:** patched navigation paths report missing `NavigatorSyncer`; eligibility/ownership must be decided and verified.
4. **Storage rebuild:** current logs contain separate storage-rebuild exceptions requiring focused investigation.
5. **Exact artifact:** create a clean release commit, build that exact hash in Release configuration, and use that artifact for paired host/client regression.
6. **Compatibility metadata:** current project target build is `736649` while October 3 logs ran ONI build `744825`; review supported-build metadata before release rather than changing it without verification.

Release regression should cover Navigator arrival, ordinary StandardWorker work, Pickupable/autosweeper collection, AnimSyncer symbol visibility, Reactables, speed/pause, Operational/power state, and scenario spawning. Preserve previous successful evidence, but mark the final release commit separately as pass/fail.

## Recommended next tests

### Pickupable follow-up tracks (separate from the numbered animation milestones)

For the proper lifecycle track, first design and implement the specialized Pickupable `StandardWorker` start/completion path, then verify paired host/client start, pose selection, multitool lifecycle, completion/abort cleanup, item transfer, and subsequent ordinary Storage work. Do not infer success from Storage convergence alone.

For the optional temporary visual-only track, first trace the existing `AnimSyncer` scope/suppression path for Pickupable. If a narrow fallback is implemented, verify host send, client receive/playback, facing/direction/target presentation, cleanup, and non-regression of ordinary worker, Navigator, and reaction ownership. Keep this experiment explicitly provisional until paired evidence exists.

### 1. Post-release-gate milestone — deliberate real-navigation-transition interruption

While active ordinary work is playing, deliberately trigger:

- fall,
- forced displacement,
- ladder departure / transition,
- swimming exit,
- cancellation as a transition begins,
- door, tube, or other special navigation transition if convenient.

Pass criteria:

```text
real transition starts promptly
→ worker playback yields/ends correctly
→ no stuck IsInWorkerPlayback
→ no stuck work or locomotion animation
→ no stale activeWorkableNetId
```

This is Milestone 3. It tests the boundary intentionally left open by the successful ordinary-worker and Milestone 2 arrival-ordering runs. It is not a rerun of ordinary multitool targeting or arrival publication; reopen those investigations only if this test or new evidence contradicts the recorded results.

### 2. Reactable timing and lifecycle matrix

Test at least:

- authorization arrives before local discovery,
- local discovery occurs before authorization,
- duplicate same-key reaction attempts,
- authorization never consumed,
- equip and unequip at oxygen-mask/suit stations,
- self emote,
- social reaction,
- one longer-running reaction,
- disconnect/resync or late join after persistent equipment/state changed.

Pass criteria include one client start per host authorization, no autonomous client reaction, correct local animation, correct final persistent state, and no stale authorization reuse.

### 3. Game-speed matrix

Test host and client requests for pause/unpause and speeds 1–3, rapid alternating requests, join while paused, join at each speed, and stale/duplicate RPC handling. Verify no command/RPC feedback loop.

### 4. Whole-mod architecture inventory

Create the coverage ledger from the primary architecture. For every current patch/syncer, record owner, client code still running, command path, delta, baseline, verifier, recovery, and unsupported cases. Then audit:

- session/epoch propagation and old-packet rejection;
- command ID/deduplication/acknowledgement behavior;
- entity identity across transferred saves;
- consistent save cursor and catch-up/readiness boundary;
- domain versions/hashes and targeted resync;
- compatibility fingerprint and limits;
- transport and performance budgets.

## Local-source workflow

Vanilla ONI source remains on the local computer. The upload set for the existing ChatGPT Project is exactly:

- `ARCHITECTURE.md`
- `DECISIONS.md`
- `STATUS.md`
- `DEVELOPMENT_ROADMAP.md`

Use the separate **Local game mod** Work Project for source inspection, builds, log analysis, and code changes. Automatic Project-to-Work handoff is not relied upon because it has failed in practice; manually open Local game mod and paste the focused inspection prompt. After a confirmed result:

- update `STATUS.md` for evidence and next tests;
- add or revise a record in `DECISIONS.md` when a design choice changes;
- update `ARCHITECTURE.md` only when a durable boundary or ownership rule changes;
- upload the revised Project reference files to `game mod`.

The local repository architecture documents were not available for this update and were not modified. Inspect `ONI_Multiplayer_Animation_Architecture.md` separately for the Navigator/worker ownership and arrival-publication details, and inspect `ONI_Together_Full_Mod_Architecture.md` to confirm whether the full-mod synchronization-boundary description needs a corresponding clarification. Compare both against these Project references before editing; do not assume changes propagate between Projects.

Documentation-source exclusions for future audits:

- `docs/Tracking.md` is personal developer tracking and should not be reviewed, updated, or treated as authoritative unless explicitly requested.
- `docs/AI logs.md` is personal AI/session material and should not be reviewed, updated, or treated as authoritative unless explicitly requested.
- `TRELLO_PROGRESS_STATUS.md` is outdated historical status material. Do not inspect or reconcile it during future status/release audits unless explicitly requested. Its stale content is not an active contradiction.

Do not upload vanilla/decompiled source or assemblies. For large logs, analyze locally and preserve only the compact evidence needed to justify status/decisions.

## Source provenance

| Source | Branch | Commit | Inspected | Evidence type |
| --- | --- | --- | --- | --- |
| `ONI_Together_Full_Mod_Architecture.md` | `experiment` / `origin/experiment` | `de5862d731f356a2de899bdade7a9159c942b1a9` (verified) | 2026-09-27 | Primary target architecture; not implementation proof |
| Current local mod source used for `ReactableSyncer`, animation/worker patches, and `GameSpeedSyncer` inspection | Exact branch/commit not verified | Unverified | 2026-09-27 | Read-only implementation inspection |
| September 27 ordinary-worker verification report | `game mod` Project conversation | Not applicable | 2026-09-27 | Previous multiplayer log/test analysis: 65/65 callbacks/positions and 106 blocked idle attempts |
| Milestone 2 Navigator arrival-publication investigation and runtime verification | `game mod` Project conversation plus Local game mod report | Not applicable | 2026-09-28 | Root cause, implementation/build result, and final 33/33 runtime verification; exact Stop counts and sequence continuity not traced |
| Pickupable synchronization investigation | `game mod` Project conversation plus Local game mod source/log report | Not applicable | 2026-10-01 | Source/log-supported exclusion and missing client worker/multitool lifecycle; client null-start-info completion exception; paired host/client pose/start evidence; later ordinary Storage match |
| October 3 release baseline and documentation audit | `experiment` plus staged local `0.8.0.4` state | `0bec442e13893b4da113b363a379f9fe2b74be3a` for clean HEAD | 2026-10-03 | Git/source/log audit: dirty staged tree, release-version split, merged feature presence, Pickupable generic Workable failure, Reactable zero-NetId warnings, Sweepy diagnostics, Operational correction, release mechanism |
| `Reactable Step One` and `Patch Reactable Synchronization` | `game mod` Project conversations | Not applicable | 2026-09-27 | Previous vanilla lifecycle observation and design discussion; not complete authorization verification |
| `REACTABLE_SYNC_ARCHITECTURE_STATUS.md` | Recorded as `ren/test-0.8.0` | `2bb0b6b938a7e1b54ae0b7426b1a4879b6af7b07` reported in conversation; Git object not verified | 2026-09-27 | Provisional Reactable architecture/status |

## Contradictions and missing evidence requiring attention

- The primary whole-mod document is a target architecture, while several supporting infrastructures already exist. A repository-wide coverage audit is still required before assigning implementation status to session epochs, command deduplication, journals, consistent save cursors, tombstones, or targeted reconciliation.
- The local source inspected for Reactables, animation, workers, and game speed was not tied to a verified commit/branch. Preserve its findings as source inspection, but record the exact commit before treating them as release evidence.
- The Reactable document’s reported commit `2bb0b6b...` could not be verified in the currently fetched Git object database.
- Legitimate Navigator transitions continued during the tested ordinary-work run, but deliberate interruption of active worker playback by real transitions remains untested and is the next milestone.
- Milestone 2 runtime behavior is strongly supported, but exact Stop publication counts, navigation sequence continuity, and unrelated Stop classification cannot be reconstructed from the final logs because those diagnostics were disabled.
- Pickupable's missing client lifecycle is supported by source and paired diagnostics, but neither the proper lifecycle fix nor the proposed `AnimSyncer` visual fallback has been implemented or verified.
- `GameSpeedSyncer` is present in source, but the requested multiplayer matrix has not been run in the evidence available here.

## Revision history

- **2026-10-03:** Added documentation-source exclusions for personal `docs/Tracking.md` / `docs/AI logs.md` and outdated `TRELLO_PROGRESS_STATUS.md`; these files no longer participate in automatic status/release audits.
- **2026-10-03:** Established merged `experiment` baseline `0bec442e13893b4da113b363a379f9fe2b74be3a`; separated clean HEAD `0.8.0.3` from staged/tested `0.8.0.4`; preserved prior successful runtime evidence without promoting it to exact-release verification; added Pickupable generic Workable, Reactable zero-NetId, Sweepy, storage-rebuild, exact-artifact and compatibility release blockers; corrected Operational synchronization status; and documented manual Local game mod switching after automatic handoff failure.
- **2026-10-01:** Recorded the source/log-supported Pickupable defect: exclusion from `StandardWorkerSyncer`, missing client Pickupable worker/multitool lifecycle, continued workable-level replay, null `GetStartWorkInfo` completion failure, paired host-only pose/start evidence, and later ordinary Storage convergence. Separated deferred proper lifecycle synchronization from the unimplemented/unverified temporary `AnimSyncer` visual-fallback proposal.
- **2026-09-28:** Marked Milestone 2 complete. Recorded the proven late outer-Stop Postfix root cause, corrected pre-`StartWork` arrival publication, same-arrival nested cleanup suppression, removal of the idle guard, successful build, final 33/33 runtime evidence, and explicit trace limitations. Milestone 3 genuine navigation interruption is next.
- **2026-09-27:** Made deliberate real-navigation-transition interruption the explicit next milestone without reopening the verified ordinary multitool result; separated vanilla Reactable lifecycle evidence from complete multiplayer authorization verification; added provenance and missing-evidence notes.
- **2026-09-27:** Recovered `ONI_Together_Full_Mod_Architecture.md` from refreshed `origin/experiment` and made it the primary source; added whole-mod target gaps and inventory work. Obsolete `animation_sync_spec.md` remains excluded. Recorded 65/65 worker verification, current Reactable authorization, game-speed implementation, removed Operational shadow design, and local-source workflow.
- **2026-09-23:** Original Reactable and animation architecture/status records created.
