# Multiplayer: ONI United — Development Status

**Last updated:** 2026-10-15  
**Scope:** Latest status recoverable from the `game mod` ChatGPT Project, the local reference documents, Local game mod implementation/build/log reports, and point-in-time GitHub/Git evidence through 2026-10-15. Historical runtime/build evidence remains tied to the exact scope in which it was obtained.

## Executive summary

- **Current transition baseline:** The authoritative checkout was clean on `develop` at `c7e876174e0a28bd2d6bfd889a83405d07dc3220` before the unstaged `0.9.0` transition changes. `develop` matched `origin/develop` at that point.
- **Final upstream-tracking release:** PR #63 / 0.8.0.5 is the final release intended to track upstream. Its last imported upstream parent is `1c9d7226c7f9dbde7c303a11af4adf5fd5c2ffbb`, imported through `88e7f06c...` on 2026-10-05 while upstream was MIT-licensed. The upstream restrictive-license change from 2026-10-11 is not an ancestor of `develop`. Future releases are intended to form an independent community-maintained project line and must not automatically import post-transition upstream code.
- **Published release commits:** `c7e876174e0a28bd2d6bfd889a83405d07dc3220` is the `develop` commit that set `0.8.0.5`; `2384d2bc8da37a69965b9409d047acc65af33f3e` is the PR #63 merge on `origin/experiment`. GitHub tag `v0.8.0.5` points to `2384d2bc8da37a69965b9409d047acc65af33f3e`, establishing the release merge as the tagged boundary.
- **Independent development:** Version metadata now targets `0.9.0`. The permanent public name is **Multiplayer: ONI United**. Compatibility-sensitive inherited identifiers remain unchanged pending the migration work in `INDEPENDENT_DEVELOPMENT.md`.
- **Legal preparation remains open:** current packaging must carry `LICENSE.md` and third-party notices; copied-source provenance remains unresolved; and historical committed ONI/Unity/Steam binaries require a separately approved repository-history remediation plan. Every currently tracked image and asset bundle was verified byte-for-byte against the last imported MIT upstream commit and is recorded as inherited MIT-covered material in `ASSET_PROVENANCE.md`. Current `develop` does not track those proprietary DLLs and does not copy development/publicized ONI assemblies into the mod output.
- **Lifecycle/speed simplification:** `ReadyManager` is speed-agnostic; `IsSimulationLocked`, `GameSpeedSyncer.RequestReSync`, and load-time lifecycle guards were rejected/removed. Pause ownership is at `GameServerHardSync.PerformHardSync` after accepted `BeginSynchronization` and at `GameClient.BeginSynchronization`, with no automatic unpause. The source build succeeded with 0 errors and 203 warnings; no runtime test or exact release-artifact verification was established.
- **Session centralization:** the work previously tracked through PR #86 was superseded by the corrected integration and merged through PR #87 at `d5a1326d...` before the final `0.8.0.5` version commit.
- **0.8.0.4 has been released.** The user confirmed the release completed successfully. The exact final release commit, tag, and hash were not independently captured in this Project evidence and must not be invented.
- The October 3 merged `experiment` audit baseline remains useful historical provenance: clean HEAD was `0bec442e13893b4da113b363a379f9fe2b74be3a` (`0.8.0.3`) while the then-staged test state reported `0.8.0.4`. That audit is not being retroactively promoted to the final released hash.
- Historical verified results remain valid for their tested builds: ordinary StandardWorker multitool targeting (**65/65**), Navigator arrival ordering (**33/33** StartWork correspondence), and prior AnimSyncer reliable symbol delivery for already-registered entities (**136/136** synchronized applications with no reorder/duplicate/value mismatch).
- The Storage rebuild crash was narrowed to `BuildingUtils.ClearStorage` dereferencing null/already-destroyed storage entries. A defensive null/destroyed-entry guard was applied and the subsequent paired runtime regression did not reproduce the old exception. Exact `StorageSyncer.OnStorageChanged` invocation was not trace-visible, so that limitation is preserved.
- Sweepy synchronization is **not part of the current supported multiplayer feature set**. The temporary idea of registering Sweepy with the creature synchronization stack was intentionally deferred rather than shipped without dedicated testing.
- The user's custom Operational/power work was removed and an upstream fix was merged. This Project does not independently record the final upstream commit/hash for that change.
- ONI compatibility metadata is generated through the normal game/mod build workflow; the earlier `736649` versus `744825` mismatch is no longer treated as a standalone release blocker.
- Pickupable/special-workable lifecycle support, Reactable edge cases, deliberate navigation interruption, Sweepy support, and broader synchronization coverage remain future development items rather than reasons to reopen the completed 0.8.0.4 release.
- **Lifecycle/readiness work is merged into `develop`.** GitHub reports PR #83 merged on 2026-10-07 at 08:46:45 UTC, from head `cd14ac71bccb1a960f59ba92df0a4abf8cdb1c27`, producing merge commit `c1c93cb5ab2ae839d28d36d32eff9a565b4f111f` on `develop`.
- The join-overlay/readiness correction is runtime verified for one LiteNetLib joining client: the same persistent logical player identity survived the intentional loading disconnect/reconnect, the host overlay remained active, and completion occurred at final `Ready` after `ConnectionFlowCompleted` / `InGame`.
- Steamworks support for the same stable-`PlayerId` pending-join flow is source-reviewed only; no Steam multiplayer runtime verification is claimed. Riptide is intentionally excluded because its current `PlayerId` changes across reconnect.
- **Current client lifecycle is the later transport-preserving rebuild.** Git/source ancestry shows the old pending-join/loading-cache/cached-reconnect mechanisms were removed and normal synchronization/world replacement now preserves the transport connection. Exact conformance to every October 7 generation/reset/admission proposal and the complete cross-transport runtime matrix remain unverified.
- **Synchronization pause ownership is known, full-interval coverage is not.** The host pauses after accepted `BeginSynchronization` in `GameServerHardSync.PerformHardSync`, the client pauses in `GameClient.BeginSynchronization`, and there is no automatic unpause. The source build passed with 0 errors / 203 warnings, but this does not by itself prove that host advancement is blocked for the entire snapshot/load/admission interval.
- The PR #83 disconnect/reconnect and pending-join behavior remains verified historical implementation evidence; it is not the current normal-load architecture.
- **Active next work:** complete the PR #63 legal/compliance remediation, resolve copied-source provenance and repository-history exposure, settle the exact intended release commit, and only then proceed to exact-artifact build/runtime verification and release gating. Re-review provenance for assets first added or changed after the verified MIT baseline.

## Current rebuilt transport-preserving client lifecycle

**Evidence classification:** Current Git/source lineage contains the rebuilt lifecycle; exact clause-by-clause conformance and complete runtime matrix are not independently revalidated for the final checked `develop` head.

The verified `develop` ancestry contains the later client-lifecycle rebuild and the cleanup sequence that removed the old loading-client cache, pending joins, legacy cached connection logic, and `readyToProcess`. The current lifecycle is organized around:

```text
Disconnected
→ Connecting
→ Connected
→ LoadingWorld
→ InGame
```

Normal world replacement preserves the transport connection rather than using the PR #83 intentional disconnect/reconnect as the normal success path. The client remains transport-connected through `LoadingWorld`; readiness is submitted from the world-spawn completion path, and the client enters `InGame` only after matching host synchronization completion.

The current lifecycle also separates identities by purpose: persistent/logical player identity identifies who the player is, the active synchronization ID identifies which synchronization is in progress, and transport-specific connection-attempt guards prevent stale callbacks from affecting a replacement attempt.

During loading/synchronization, ordinary in-game `NetworkBehaviour` traffic is gated rather than treated as normal gameplay. However, the detailed October 7 proposal—formal load-generation APIs, complete recursive wrapper classification, a named centralized reset boundary, explicit graph-verification acknowledgement, and exact per-player admission semantics—has not been re-audited clause by clause against the final checked `develop` source. Those details remain an audit checklist, not automatic implementation claims.

The host pause-acquisition point is now known: `GameServerHardSync.PerformHardSync` pauses after accepted `BeginSynchronization`; the client pauses in `GameClient.BeginSynchronization`; no automatic unpause is performed at synchronization completion. This simplification built with 0 errors and 203 warnings. What remains unverified is whether the acquired paused state fully covers the entire baseline/load interval in every relevant path; no exact-release-artifact runtime proof is claimed.

## Verified fixes and results

### Historical PR #83 client lifecycle, readiness, and join overlay

The following behavior is verified for the PR #83 implementation and is retained as historical regression evidence. It does not describe the later rebuilt normal-load lifecycle.

Verified implementation behavior:

- `Loading` is retained as actual readiness state rather than being treated as transient presentation only.
- The host retains a pending join across the intentional loading disconnect on supported transports.
- `IsEveryoneReady()` requires no pending joins and every relevant participant to be exactly `Ready`.
- Final completion is the joining client's `Ready` after `ConnectionFlowCompleted` / `InGame`; save-transfer completion, `Game.OnSpawn`, and reconnect are not treated as completion.
- Existing readiness/status packets remain the join-overlay presentation mechanism; no independent overlay authority was added.
- `CancelPendingJoin()` removes an `Unready` pending join on disconnect/unregister, including Steam lobby removal, while preserving a `Loading` join for the intentional reconnect.

Runtime evidence: one LiteNetLib joining-client test passed for the complete join/load/disconnect/reconnect/final-Ready and host-overlay lifetime path. This also supports reuse of LiteNetLib's persistent logical `PlayerId` for pending-join correlation in that scenario.

Evidence limits:

- The three-peer host + already-connected Client A + joining Client B overlay scenario has not been runtime tested.
- Overlapping B/C joins have not been runtime tested.
- Steamworks is source-supported only.
- Riptide does not participate in stable pending-join correlation.
- A player that reaches `Loading` and permanently disappears can leave the pending join indefinitely; no timeout policy is implemented or verified.
- The last source review found no blocker at PR head `cd14ac71...`, but GitHub showed no CI/status checks for that head. This Project has no new exact-head local build result to add; the merge is Git-verified, not newly build- or release-verified here.

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

### Storage rebuild / `ClearStorage`

Source inspection identified the prior client packet-handler failure at `BuildingUtils.ClearStorage`, where a `Storage.items` entry could already be null or Unity-destroyed before `DeleteObject()` was called. Multiplayer item-destruction/replay paths can leave such stale references in the client-side collection even though vanilla storage lifecycle normally removes entries first.

The narrow release fix skips null/destroyed entries while still clearing the collection and rebuilding from authoritative state.

Runtime regression after the fix showed substantial paired multiplayer item/storage-related activity with:

- no recurrence of the old `StorageSyncer.OnStorageChanged -> BuildingUtils.ClearStorage` exception,
- no `NullReferenceException`,
- no packet-handler exception attributable to this path.

**Evidence limit:** the tested build did not emit an explicit `StorageSyncer.OnStorageChanged` positive-control diagnostic, so exact invocation count and exact rebuild-convergence trace cannot be claimed.

**Assessment:** Accepted for the 0.8.0.4 release scope with the trace limitation preserved.

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

The earlier inspected source contained `GameSpeedSyncer` with:

- immediate command/RPC propagation,
- host validation of the three supported speeds,
- monotonic revisions,
- stale-revision rejection,
- nested/re-entrant apply-depth protection,
- join/resync request behavior.

The later lifecycle/speed simplification rejected and removed `RequestReSync` and lifecycle-specific load guards. `ReadyManager` no longer owns `IsSimulationLocked`, and the generic `MultiplayerOverlay` does not own pausing. Instead, pause is initiated once by `GameServerHardSync.PerformHardSync` after accepted `BeginSynchronization` on the host and by `GameClient.BeginSynchronization` on the client. There is no automatic unpause after synchronization.

**Evidence:** The simplification built successfully with 0 errors and 203 warnings. No runtime synchronization/speed matrix or exact-release-artifact verification was completed. The earlier multiplayer matrix therefore remains outstanding for applicable ordinary `GameSpeedSyncer` behavior.

### Session centralization and PR #86

The intended ownership boundary is shared session state in `MultiplayerSession`/common networking, while `GameClient` retains the current client lifecycle, `ReadyManager`/`GameServerHardSync` retain synchronization ownership, and each transport retains its mechanics and identity rules.

At the time checked, PR #86 targeted `develop` `3ddad5ad7fcaace77292c3f4b4a2218c0aa5db1f` from head `a32139b48031202ee41f6bd77bd16701b4b51c35`. GitHub metadata reported mergeable, 2 commits, 12 changed files, and +216/-149. The changed scope was session centralization and transport integration.

**Evidence limit:** Mergeability and diff metadata do not establish behavioral safety. The regression-focused source review and runtime verification were still pending; PR #86 was not established here as merged or release-verified.

### Operational/power synchronization

Previous investigations traced initialization crashes to unsafe client-side `Operational` shadow/getter behavior that could alter derived state before dependent components completed spawning.

For the released 0.8.0.4 work, the user reports that their custom Operational/power changes were removed and the upstream fix was merged. The final upstream commit/hash and exact released source tree were not independently captured in this Project, so no stronger Git claim is made here.

**Assessment:** No separate custom Operational/power release track remains active. Future regressions should be evaluated against the upstream implementation actually present in the current repository.

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
| Pickupable StandardWorker lifecycle sync | Specialized lifecycle remains unsupported/deferred beyond the release scope | High for previously inspected limitation |
| Reactable one-shot authorization | Present | Medium; broader tests needed |
| Reaction presentation/state separation | Architectural target | Medium / partial |
| Game-speed revisioned RPC | Present | Medium-high; not freshly tested here |
| Operational/power synchronization | User reports custom work removed and upstream fix merged for release | User-reported release state; exact upstream commit/hash not captured here |
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
- Sweepy is currently treated as an unsupported/missing multiplayer feature. Dedicated identity/navigation/animation synchronization support is deferred until it can be tested carefully.
- Debug logging may still remain in some animation/Reactable patches and should be reviewed separately if it becomes noisy.

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
- Join/hard-sync lifecycle/readiness behavior was first verified in the PR #83 disconnect/reconnect implementation and was later rebuilt into the current transport-preserving client lifecycle. The active priority is PR #86 regression review, exact release-baseline selection, legal/compliance audit, and exact-artifact verification; broader comparison with the proposed `T/J` consistent-cut and catch-up-journal design remains future architecture work.
- Entity identity persistence/baseline mapping for existing and nested items remains a gating design spike.
- Command IDs, host deduplication, explicit rejection reasons, and reconnect boundaries need a domain-by-domain audit.
- Region/entity hashes and targeted refresh behavior are target architecture, not confirmed universal implementation.
- Queue, payload, save-transfer, journal, and per-tick budgets require measurement in representative colonies.

### Power/Operational

- The user reports that custom Operational/power work was removed before release and the upstream fix was merged.
- The exact final upstream commit/hash is not independently recorded in this Project evidence.
- Do not continue treating the earlier PR #53/custom Operational path as an active 0.8.0.4 release blocker unless current repository inspection shows it is still present.
- Longer-term input-level synchronization remains only a possible future architectural direction.

## 0.8.0.4 release outcome

**Status:** Released successfully, based on the user's confirmation.

The October 3 release-candidate audit remains historical evidence of the integration process, not proof of the final released commit hash. The exact final release commit/tag/hash was not independently captured in the Project and is therefore intentionally left unspecified.

Release-scope disposition of the former blockers:

1. **Storage rebuild:** narrow `ClearStorage` null/destroyed-entry guard accepted; subsequent paired regression did not reproduce the old exception. Positive-control trace limitation preserved.
2. **Sweepy:** not promoted into the synchronization stack for this release; treated as an unsupported/missing feature and deferred for dedicated future testing.
3. **Operational/power:** user's custom changes removed; upstream fix merged according to the user's release report.
4. **Compatibility metadata:** handled through the normal game update/build-generated metadata workflow rather than as a standalone blocker.
5. **Pickupable / Reactable edge cases:** remain future development limitations where applicable; they did not prevent the user from completing the 0.8.0.4 release.

Historical successful tests remain evidence for the builds in which they were obtained. Do not attach them to an unknown exact release hash without separate proof.

## Historical PR #83 lifecycle/readiness implementation and later rebuild

PR #83 is preserved as **historical verified implementation evidence**, not as the current normal-load lifecycle.

The PR #83 runtime-supported shape was:

```text
initial client connection
→ game-state exchange
→ host save request/transfer
→ client enters LoadingWorld
→ intentional transport disconnect / teardown
→ world load and Game.OnSpawn
→ reconnect from cached session information
→ hard-sync / readiness path
→ normal gameplay
```

That implementation fixed the premature all-ready/join-overlay boundary by retaining `Loading` readiness and a pending join across the intentional disconnect for stable-identity transports. One LiteNetLib joining-client flow passed at runtime and preserved the same persistent logical player ID across disconnect/reconnect. Steamworks was source-supported only; Riptide was excluded from pending-join correlation.

Later `develop` work superseded that normal-load mechanism. The rebuilt lifecycle removed the loading-client cache, pending joins, legacy cached reconnect state, and `readyToProcess`, preserved transport through normal world replacement, and cleaned up the client connection lifecycle. Therefore the PR #83 disconnect/reconnect sequence and pending-join semantics must not be described as the current shape.

### Current lifecycle status

At the point-in-time checked `develop` `3ddad5ad7fcaace77292c3f4b4a2218c0aa5db1f`, Git ancestry was:

```text
experiment 64a4dd6f...
→ older upstream integration via merge/upstream-next 88e7f06c...
  / upstream tip 1c9d7226...
→ later rebuilt client-lifecycle chain
→ merge/client-lifecycle 42fca889...
→ develop 3ddad5ad...
```

This establishes that the later lifecycle replaced the PR #83 normal-load implementation in current `develop`. It does not by itself prove every detailed October 7 transport-preserving design clause or every transport/runtime scenario.

Historical source findings from the earlier transport audits—Riptide duplicate disconnect notification, Steam stale state/handle paths, LiteNet explicit-disconnect callback dependence, and direct reconnect paths that can skip `Connecting`—remain useful bug history, but they must be rechecked against the rebuilt current source before being treated as current defects.

PR #86 session-centralization review remains the active integration gate. Its changes must not reintroduce superseded disconnect/reconnect lifecycle ownership while centralizing shared session state.

## Recommended next tests

### Pickupable follow-up tracks (separate from the numbered animation milestones)

For the proper lifecycle track, first design and implement the specialized Pickupable `StandardWorker` start/completion path, then verify paired host/client start, pose selection, multitool lifecycle, completion/abort cleanup, item transfer, and subsequent ordinary Storage work. Do not infer success from Storage convergence alone.

For the optional temporary visual-only track, first trace the existing `AnimSyncer` scope/suppression path for Pickupable. If a narrow fallback is implemented, verify host send, client receive/playback, facing/direction/target presentation, cleanup, and non-regression of ordinary worker, Navigator, and reaction ownership. Keep this experiment explicitly provisional until paired evidence exists.

### 1. Deferred animation milestone — deliberate real-navigation-transition interruption

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

The local repository architecture documents were not available for this update and were not modified. Inspect `ONI_Multiplayer_Animation_Architecture.md` separately for the Navigator/worker ownership and arrival-publication details, and inspect `ONI_Together_Full_Mod_Architecture.md` to confirm whether the full-mod synchronization-boundary description needs a corresponding clarification. The lifecycle/speed and session-centralization decisions also require inspection of any maintained networking/session or lifecycle module architecture documents in **Local game mod**. Compare local documents against these Project references before editing; do not assume changes propagate between Projects.

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
| October 4 release closeout | `game mod` Project conversation | Exact final release hash not captured | 2026-10-04 | User-confirmed 0.8.0.4 release; Storage disposition; Sweepy deferral; custom Operational removal/upstream merge; joining-sequence priority |
| October 5 transport identity/lifecycle audits | `experiment` local repository audit plus paired LiteNetLib runtime logs and user Riptide startup test | `64a4dd6fa2870877868d13be034a3d8cf13f1f19` at audited HEAD; 19 staged paths; one temporary unstaged Steam client edit existed during the audit and was later removed by the user | 2026-10-05 | LiteNetLib persistent-ID join/load/reconnect/Ready runtime evidence; Riptide bootstrap polling fix source proof plus startup runtime check; Steam source-only lifecycle proof; expected-connection stale-close guard; cross-driver client-state/loading findings |
| Lifecycle/speed simplification, current ancestry, and PR #86 preparation | `game mod` Project conversation and Local game mod reports summarized there | `develop` `3ddad5ad7fcaace77292c3f4b4a2218c0aa5db1f`; PR #86 head `a32139b48031202ee41f6bd77bd16701b4b51c35` | 2026-10-15 | Git ancestry; source/build evidence with 0 errors/203 warnings; PR metadata; no PR runtime regression verification |
| Join-overlay/readiness implementation and PR review | `game mod` Project conversation; GitHub issues #78–#82 and PR #83 | PR head `cd14ac71bccb1a960f59ba92df0a4abf8cdb1c27`; GitHub merge commit `c1c93cb5ab2ae839d28d36d32eff9a565b4f111f` | 2026-10-07 | LiteNetLib single-join runtime result; source-reviewed readiness/pending-join and cancellation paths; Steam source-only; Riptide exclusion; GitHub issue/merge states |
| `Reactable Step One` and `Patch Reactable Synchronization` | `game mod` Project conversations | Not applicable | 2026-09-27 | Previous vanilla lifecycle observation and design discussion; not complete authorization verification |
| `REACTABLE_SYNC_ARCHITECTURE_STATUS.md` | Recorded as `ren/test-0.8.0` | `2bb0b6b938a7e1b54ae0b7426b1a4879b6af7b07` reported in conversation; Git object not verified | 2026-09-27 | Provisional Reactable architecture/status |

## Contradictions and missing evidence requiring attention

- The primary whole-mod document is a target architecture, while several supporting infrastructures already exist. A repository-wide coverage audit is still required before assigning implementation status to session epochs, command deduplication, journals, consistent save cursors, tombstones, or targeted reconciliation.
- The local source inspected for Reactables, animation, workers, and game speed was not tied to a verified commit/branch. Preserve its findings as source inspection, but record the exact commit before treating them as release evidence.
- The Reactable document’s reported commit `2bb0b6b...` could not be verified in the currently fetched Git object database.
- Legitimate Navigator transitions continued during the tested ordinary-work run, but deliberate interruption of active worker playback by real transitions remains untested and is deferred behind the current release-preparation work.
- Milestone 2 runtime behavior is strongly supported, but exact Stop publication counts, navigation sequence continuity, and unrelated Stop classification cannot be reconstructed from the final logs because those diagnostics were disabled.
- Pickupable's missing client lifecycle is supported by source and paired diagnostics, but neither the proper lifecycle fix nor the proposed `AnimSyncer` visual fallback has been implemented or verified.
- `GameSpeedSyncer` is present in source, but the requested multiplayer matrix has not been run in the evidence available here.

## Revision history

- **2026-10-15 (reconciliation):** Reclassified the PR #83 intentional disconnect/reconnect and pending-join lifecycle as historical verified evidence; documented the later transport-preserving `develop` rebuild as the current implementation lineage; removed the stale claim that transport-preserving loading is unimplemented; and preserved uncertainty about exact October 7 design conformance and full-interval pause coverage.
- **2026-10-15:** Recorded the verified `develop` ancestry and point-in-time SHAs; lifecycle/speed simplification build result and runtime limits; PR #86 metadata and pending regression review; session-centralization ownership boundary; and the legal/licensing/compliance audit as a pre-release gate. No merge, runtime result, or release artifact was inferred.
- **2026-10-07:** Recorded transport-preserving normal loading as approved source-reviewed design only, with no implementation/build/runtime claim. Added the generation/admission/reset/gate/reconstruction/`WorldLoadCompleted`/final-`Ready` contract, retained reconnect as transport-loss fallback, and made host advancement versus pause/catch-up a blocking prerequisite.
- **2026-10-07:** Removed stale priority wording that still described join/hard-sync investigation as active; PR #83 lifecycle/readiness work is complete for its tested scope, and the next active priority is `develop` → `experiment` release integration and exact-artifact verification.
- **2026-10-07:** Recorded the implemented lifecycle/readiness/join-overlay boundary and its LiteNetLib single-client runtime verification; kept Steam source-only and Riptide excluded; recorded pending-join cancellation and the abandoned-`Loading` limitation. GitHub verification shows issues #78–#82 remain open and PR #83 merged into `develop` at `c1c93cb5ab2ae839d28d36d32eff9a565b4f111f`. The next `develop`→`experiment` integration and release remain unverified future work.
- **2026-10-05:** Recorded the transport identity/lifecycle investigation. LiteNetLib persistent identity is runtime-verified for the tested join/save/load/reconnect/Ready path; the Riptide bootstrap polling fix is source-proven and startup-tested; Steam lifecycle remains source-only, with stale replacement-close protection correctly wired. Recorded source-proven client lifecycle inconsistencies in Riptide, Steam, and LiteNetLib, and the removal of the temporary Steam explicit-disconnect callback edit after it was shown to break `LoadingWorld`. Narrowed the next development priority from the broad joining sequence to the world-loading sequence.
- **2026-10-04:** Recorded the user-confirmed successful 0.8.0.4 release without inventing a final commit/tag/hash; accepted the Storage `ClearStorage` fix with its runtime trace limitation; reclassified Sweepy as unsupported/deferred rather than a release blocker; recorded removal of custom Operational/power work and merge of the upstream fix as user-reported release state; removed compatibility metadata as a standalone blocker; and made the multiplayer client joining sequence the immediate post-release development priority.

- **2026-10-03:** Added documentation-source exclusions for personal `docs/Tracking.md` / `docs/AI logs.md` and outdated `TRELLO_PROGRESS_STATUS.md`; these files no longer participate in automatic status/release audits.
- **2026-10-03:** Established merged `experiment` baseline `0bec442e13893b4da113b363a379f9fe2b74be3a`; separated clean HEAD `0.8.0.3` from staged/tested `0.8.0.4`; preserved prior successful runtime evidence without promoting it to exact-release verification; added Pickupable generic Workable, Reactable zero-NetId, Sweepy, storage-rebuild, exact-artifact and compatibility release blockers; corrected Operational synchronization status; and documented manual Local game mod switching after automatic handoff failure.
- **2026-10-01:** Recorded the source/log-supported Pickupable defect: exclusion from `StandardWorkerSyncer`, missing client Pickupable worker/multitool lifecycle, continued workable-level replay, null `GetStartWorkInfo` completion failure, paired host-only pose/start evidence, and later ordinary Storage convergence. Separated deferred proper lifecycle synchronization from the unimplemented/unverified temporary `AnimSyncer` visual-fallback proposal.
- **2026-09-28:** Marked Milestone 2 complete. Recorded the proven late outer-Stop Postfix root cause, corrected pre-`StartWork` arrival publication, same-arrival nested cleanup suppression, removal of the idle guard, successful build, final 33/33 runtime evidence, and explicit trace limitations. Milestone 3 genuine navigation interruption is next.
- **2026-09-27:** Made deliberate real-navigation-transition interruption the explicit next milestone without reopening the verified ordinary multitool result; separated vanilla Reactable lifecycle evidence from complete multiplayer authorization verification; added provenance and missing-evidence notes.
- **2026-09-27:** Recovered `ONI_Together_Full_Mod_Architecture.md` from refreshed `origin/experiment` and made it the primary source; added whole-mod target gaps and inventory work. Obsolete `animation_sync_spec.md` remains excluded. Recorded 65/65 worker verification, current Reactable authorization, game-speed implementation, removed Operational shadow design, and local-source workflow.
- **2026-09-23:** Original Reactable and animation architecture/status records created.
