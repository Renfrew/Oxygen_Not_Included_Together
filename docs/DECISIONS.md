# Oxygen Not Included Together — Architecture Decision Log

**Last updated:** 2026-10-03  
**Status:** Recovered decisions from the `game mod` Project and local repository. Decision status and implementation status are separate: accepting a decision does not mean its implementation is complete.

## Decision status legend

- **Confirmed:** Current architectural rule, supported by implementation or test evidence.
- **Provisional:** Preferred design is implemented or selected, but important tests remain.
- **Proposed:** Recovered target design; not verified as current implementation.
- **Rejected:** Do not return to this approach without new evidence.
- **Deferred:** Intentionally postponed.

Implementation status uses only: **Implemented and tested**, **Implemented but further testing required**, **Partially implemented**, **Not implemented**, or **Not independently verified**. These labels report available evidence rather than changing the architectural decision.

## D-001 — Host-authoritative simulation

**Decision status:** Confirmed  
**Implementation status:** Partially implemented  
**Decision:** The host decides gameplay outcomes. Clients submit requests, replay accepted actions, and converge to host-owned state.  
**Rationale:** Client chore/path/state differences must not become competing authorities.  
**Consequence:** A visual replay failure may reduce fidelity, but it must not create persistent world-state divergence.  
**Source:** Recovered animation architecture; Reactable architecture/status; current syncer design.

## D-002 — Stable network entity identity

**Decision status:** Confirmed  
**Implementation status:** Partially implemented  
**Decision:** Cross-machine references use `NetworkIdentity.NetId`, not Unity instance IDs or host runtime object identity.  
**Rationale:** Host and client construct different runtime objects.  
**Consequence:** Packets and authorization keys must resolve through the local identity registry.

## D-003 — Preserve vanilla lifecycle where possible

**Decision status:** Confirmed  
**Implementation status:** Partially implemented  
**Decision:** Synchronization should enter through stable semantic boundaries, then allow the local vanilla lifecycle to perform sequencing, animation callbacks, and cleanup. Harmony patches delegate to syncers rather than becoming protocol/state-machine implementations.  
**Rejected alternative:** Reimplementing complete ONI state machines in network handlers.  
**Rationale:** Local vanilla objects contain valid references and already encode complex lifecycle behavior.

## D-004 — Host-authorized Reactables use a client-local candidate

**Decision status:** Provisional  
**Implementation status:** Implemented but further testing required  
**Decision:** The host’s successful vanilla `Reactable.CanBegin(...)` sends an authorization. The client discovers its own locally initialized Reactable, runs `InternalCanBegin(...)` for local safety, consumes matching host authorization, and then follows the vanilla `ReactionMonitor` lifecycle.  
**Current key:** `(reactable.id.hash, reactor NetworkIdentity.NetId)`.  
**Rationale:** The host owns the gameplay decision; the client-local object owns valid local references and handles.  
**Rejected alternatives:**

- Return `true` from all client `CanBegin()` calls.
- Send/execute the host Reactable runtime instance.
- Permanently authorize by a broad class/type key.
- Directly force `ReactionMonitor.GoTo(reacting)` as the normal final design.
- Add a second navigation/teleport sequencing system.

**Open questions:** Explicit authorization expiry; duplicate same-key authorizations; packet-first/discovery-first retry behavior; sufficiency of the hash/context key for all Reactables; state-mutating `Begin/Run/End` methods.  
**Sources:** `Reactable Step One`; `Patch Reactable Synchronization`; `REACTABLE_SYNC_ARCHITECTURE_STATUS.md`; current `ReactableSyncer.cs` and `ReactablePatch.cs`.

The earlier observation that a client-local Reactable can be stored, enter `ReactionMonitor.reacting`, and reach `Reactable.Begin()` verifies the vanilla lifecycle path. It does not verify the complete host-authorization exchange or the open questions above.

## D-005 — One-shot Reactable authorization

**Decision status:** Provisional  
**Implementation status:** Implemented but further testing required  
**Decision:** An authorization is consumed once and cannot authorize later autonomous reactions.  
**Rationale:** A stale host decision must not become standing client gameplay authority.  
**Implementation note:** The current `HashSet<(reactableId, reactorNetId)>` provides one-shot consumption but no explicit TTL. Expiry remains required by the recovered design.

## D-006 — Publish the semantic Navigator arrival before synchronous worker start

**Decision status:** Confirmed  
**Implementation status:** Implemented and tested  
**Decision:** Synchronize the logical navigation arrival at the correct semantic boundary. On a successful arrival, publish the outer arrival Stop before synchronous arrival processing can reach `StartWork`; suppress publication of the nested cleanup Stop for that same arrival. Continue synchronizing genuine unrelated Stops. A real Navigator transition still represents locomotion and may interrupt work.  
**Rationale:** The proven failure was publication order, not an inherent priority conflict: an outer `Navigator.Stop` Postfix published idle only after synchronous nested arrival processing had already reached `StartWork`, allowing late idle to overwrite the work animation. Correct semantic ordering makes arrival idle precede worker ownership.  
**Implementation:** The arrival publication was moved before synchronous arrival processing, nested same-arrival cleanup publication was suppressed, and the client worker-playback Navigator-idle guard was removed. The build succeeded.  
**Evidence:** 33 host `StartWork` publications matched 33 client applications exactly; 31 clearly observed arrival-idle-to-worker-start cases applied idle first; zero Navigator-idle requests occurred during active worker playback with the guard absent; 225 active-work animation checks were recorded; 31 worker intervals completed and two remained active only until disconnect; zero errors or assertions were found. Exact host Stop publication counts, navigation sequence continuity, and classification of unrelated Stops were not trace-verified because those diagnostics were disabled.  
**Superseded workaround evidence:** Before the publication-order root cause was corrected, the guard blocked 106 idle attempts while legitimate transitions continued, and 65/65 ordinary client multitool jobs reached matching target callbacks. This remains historical evidence for the symptom and workaround, not the enduring design.  
**Rejected alternatives:** Permanently block Navigator idle during active worker playback; give every nested `Navigator.Stop` invocation an independent publication; give all Navigator animations unconditional priority.  
**Sources:** `Continue Animation Debugging`; `Fix Tool Direction Animation`; `Investigate Navigator Animation Ownership`; Milestone 2 Local game mod source/log investigation.

## D-007 — Synchronize StandardWorker lifecycle, not multitool target coordinates

**Decision status:** Confirmed  
**Implementation status:** Implemented and tested  
**Implementation scope:** Ordinary synchronized work only.  
**Decision:** Synchronize `StartWork`, `BeginComplete`, `CompleteWork`, and aborting `StopWork`; let the client’s vanilla animation events compute target position/direction locally.  
**Rationale:** 65/65 verified client targets matched host targets, including positions, so additional coordinate traffic is unnecessary.  
**Rejected alternative:** Network every `ToolTarget` coordinate/direction.  
**Scope:** Does not automatically cover special/skipped workables or custom animation-override lifecycles.

## D-008 — End worker playback at both completion paths

**Decision status:** Confirmed  
**Implementation status:** Implemented and tested  
**Implementation scope:** Recorded ordinary synchronized-work cases.  
**Decision:** End ownership after the real method in both `CompleteWork` and aborting `StopWork`. Preserve the workable NetId across prefix/postfix and clear only the matching session.  
**Rationale:** Normal `CompleteWork()` uses `InternalStopWork()` and does not necessarily pass through the public `StopWork()` Harmony patch.  
**Rejected alternative:** Finalize only in `StopWork`.

## D-009 — Synchronous playback scopes are nest-safe execution markers

**Decision status:** Confirmed  
**Implementation status:** Implemented but further testing required  
**Decision:** `SyncedPlaybackScope` and override scopes use depth/counter semantics and are always released via `finally`/Harmony finalizer.  
**Rationale:** Vanilla calls are nested/re-entrant; a boolean can be cleared prematurely.  
**Limitation:** A synchronous scope is not authority for later asynchronous callbacks.

## D-010 — Immediate revisioned game-speed synchronization

**Decision status:** Confirmed  
**Implementation status:** Implemented but further testing required  
**Decision:** Pause/speed changes use client command → host validation/application → reliable immediate revisioned RPC. Do not wait for periodic SyncVar observation.  
**Rationale:** Speed changes are latency-sensitive and re-entrant vanilla calls can create feedback loops.  
**Implementation:** `GameSpeedSyncer` uses `_revision`, `_lastAppliedRevision`, and `_applyDepth`.

## D-011 — Do not virtualize derived `Operational` getters during unsafe initialization windows

**Decision status:** Confirmed  
**Implementation status:** Implemented but further testing required  
**Decision:** Do not replace `Operational.IsOperational`, `IsActive`, `IsFunctional`, or `GetFlag(PoweredFlag)` with multiplayer shadow values during incomplete prefab initialization. Clients may temporarily use valid vanilla/local state until authoritative host state is ready. Longer-term input-level synchronization remains preferred where practical.  
**Rationale:** The earlier shadow-getter design changed ONI semantics during construction and could trigger unrelated `OperationalChanged` paths before dependent components were ready.  
**Current `experiment` implementation:** Operational synchronization is still present. The complete-removal commit `484bc805dd13e1755b2648192c55b23bd1e7b11e` exists on `ren/test-0.8.0` but is not in `experiment`. The merged branch contains `OperationalStatePacket`, `RequestOperationalStatePacket`, `Operational_Patch`, `Operational_Patches`, `EnergyConsumer_Patches`, and `ClientReceiver_Operational`; PR #53 changed behavior so clients use vanilla/local state until host state arrives.  
**Rejected alternative:** Unconditionally replacing derived getters with multiplayer shadow values during object construction.  
**Open question:** Whether the current PR #53 approach should remain for release or later migrate toward the input-level design after release evidence is gathered.  
**Source:** October 3 release-baseline repository audit plus prior initialization-crash investigations.

## D-012 — Separate persistent state from presentation events

**Decision status:** Confirmed  
**Migration status:** Proposed  
**Implementation status:** Partially implemented  
**Decision:** Persist outcomes such as equipment state through state replication. Use short-lived events for equip/emote/reaction presentation. Snapshots contain current state/activity, not old one-shot events.  
**Rationale:** Animation failure must not undo or block state convergence; late joiners need current state.  
**Proposed extension:** Semantic event kinds plus `IReactionReplayAdapter` rather than protocol coupling to runtime class names.

## D-013 — Local vanilla source stays local

**Decision status:** Confirmed  
**Implementation status:** Implemented and tested  
**Decision:** Do not upload decompiled/proprietary ONI source or assemblies to the ChatGPT Project. Upload only maintained architecture/status references and task-specific user-owned material when needed.  
**Workflow:** Local Codex/Work tasks inspect source and logs; confirmed findings are distilled into these three documents; revised documents are uploaded to `game mod`.  
**Rationale:** Preserves local-only source access while giving Project chats durable context.

## D-014 — Obsolete animation specification is not authoritative

**Decision status:** Confirmed  
**Implementation status:** Implemented and tested  
**Implementation scope:** Documentation/source-selection policy.  
**Decision:** `animation_sync_spec.md` is obsolete and must not be used to describe either the current architecture or the target architecture.  
**Replacement source:** `ONI_Together_Full_Mod_Architecture.md` on the `experiment` branch is the primary whole-mod architecture. `ONI_Multiplayer_Animation_Architecture.md` is a narrower supporting document, supplemented by later confirmed decisions and current code evidence.

## D-015 — Special workables remain separate

**Decision status:** Confirmed  
**Implementation status:** Partially implemented  
**Decision:** `Pickupable`, `RancherWorkable`, `LiquidPumpingStation`, `Sleepable`, `ComplexFabricator`, and other special/override workables are analyzed through their actual lifecycle, not counted as ordinary `StandardWorker` regressions.  
**Rationale:** Their work, animation, and side-effect boundaries differ.

## D-016 — Session and baseline epochs gate all gameplay updates

**Decision status:** Proposed  
**Implementation status:** Not independently verified  
**Decision:** Gameplay messages carry the current session and epoch plus the appropriate stream sequence/version. Installing a new host session or save baseline advances the epoch; old-epoch packets are rejected.  
**Rationale:** A late packet from an earlier world/baseline must never mutate the newly loaded colony.

## D-017 — Commands are requests with IDs and acknowledgements

**Decision status:** Proposed  
**Implementation status:** Partially implemented  
**Decision:** World-changing player input becomes a command with a monotonic per-session `ClientCommandId`. The host validates and deduplicates it, applies it on the simulation thread, returns accepted/rejected, and publishes the resulting state/events.  
**Rationale:** Client intent is not evidence that the mutation occurred; retries must not duplicate build, priority, or speed actions.

## D-018 — Consistent save cursor plus catch-up journal

**Decision status:** Proposed  
**Implementation status:** Not independently verified  
**Decision:** Join/hard sync creates a consistent save at tick `T` and journal cursor `J`. The client verifies and loads the baseline, builds identities, acknowledges readiness, receives changes after `J`, then joins the live stream. Journal overflow restarts with a newer baseline.  
**Rationale:** A raw save followed by arbitrary live packets can lose or double-apply changes.

## D-019 — Lifecycle precedes state and events

**Decision status:** Proposed  
**Implementation status:** Not independently verified  
**Decision:** Network application order is `Spawn/Bind → State → Event → Despawn`. Pre-bind dependencies use bounded queues; destroyed IDs are briefly tombstoned; identity is guarded by epoch/generation.  
**Rationale:** Dependent packets cannot safely apply before the referenced local object exists, and late packets must not resurrect/rebind destroyed objects.

## D-020 — Domain integration contract and coverage ledger

**Decision status:** Proposed  
**Implementation status:** Not implemented  
**Decision:** Every synchronized domain documents authority, input, host observation boundary, representation, dependencies, apply behavior, baseline, verification, recovery, and unsupported cases. A domain is incomplete until live state, join/hard-sync baseline, verification, and recovery agree.  
**Rationale:** A feature that looks correct once can still fail after loss, reconnect, or hard sync.

## D-021 — No deterministic lockstep or first-generation host migration

**Decision status:** Confirmed  
**Implementation status:** Not independently verified  
**Decision:** The architecture uses host authority plus replication/reconciliation, not deterministic lockstep. Host disconnect ends the session until a separate authority-transfer protocol is designed.  
**Rationale:** ONI client worlds cannot be assumed to simulate identically, and safe host migration requires ownership, save, journal, and command-boundary transfer.

## D-022 — Compatibility fingerprint precedes save transfer

**Decision status:** Proposed  
**Implementation status:** Partially implemented  
**Implementation note:** Existing mod validation is reported, but the full target handshake is not independently verified.  
**Decision:** Handshake exchanges protocol version, ONI build, mod build, relevant enabled-mod fingerprint, and save-format capability. Incompatible sessions are rejected with an actionable reason.  
**Rationale:** Matching network schema alone does not establish compatible game state.

## D-023 — Recovery uses the smallest authoritative scope

**Decision status:** Proposed  
**Implementation status:** Not independently verified  
**Decision:** Replaceable state is superseded; missing entities request an entity snapshot; region/entity hash mismatch requests targeted refresh; persistent mismatch or journal gap installs a fresh baseline; cosmetic replay failure logs and continues.  
**Rationale:** Recovery should converge without turning every fault into a full save transfer, while never allowing presentation to block state.

## D-024 — Exclude personal and historical repository notes from automatic Project audits

**Decision status:** Confirmed  
**Implementation status:** Implemented and tested  
**Implementation scope:** Documentation/source-selection workflow.  
**Decision:** `docs/Tracking.md` and `docs/AI logs.md` are personal developer notes and are not authoritative Project sources. `TRELLO_PROGRESS_STATUS.md` is an outdated historical file. Future automatic documentation audits must not inspect, reconcile, or use any of these three files unless the user explicitly asks for them.  
**Rationale:** These files serve personal or historical purposes and repeatedly create false contradictions when treated as current architecture/status sources. The maintained Project references and maintained local architecture documents are the authoritative workflow sources.  
**Consequence:** Their presence, staged state, or stale content does not create a Project-documentation inconsistency by itself.

## Unresolved decisions

1. Exact TTL and collision handling for Reactable authorization.
2. Whether the vanilla Reactable poll reliably retries when authorization arrives after first local discovery.
3. Safe presentation-only handling for Reactables whose `Begin/Run/End` mutate simulation state.
4. The final persistent activity/snapshot schema for join-in-progress.
5. Reliable ordering/version boundaries between transient presentation and persistent state.
6. Whether current PR #53 Operational synchronization should remain or later migrate toward input-level synchronization after release verification.
7. Consistent ONI save-cut API and whether capture must pause simulation.
8. Durable identity for existing/nested items across save/load and baseline mapping.
9. Which client ONI subsystems can be safely suppressed versus narrowly mirrored.
10. Exact transport modes, payload limits, journal budget, and batching thresholds.
11. Final multiplayer permission policy for pause/speed changes.

## Source provenance

| Source | Branch | Commit | Inspected | Evidence type |
| --- | --- | --- | --- | --- |
| `ONI_Together_Full_Mod_Architecture.md` | `experiment` / `origin/experiment` | `de5862d731f356a2de899bdade7a9159c942b1a9` (verified) | 2026-09-27 | Primary proposal underlying whole-mod decisions, especially D-001–D-003 and D-016–D-023 |
| `ONI_Multiplayer_Animation_Architecture.md` | Exact Git branch not verified | Unverified | 2026-09-27 | Supporting animation/Reactable proposal |
| `REACTABLE_SYNC_ARCHITECTURE_STATUS.md` | Recorded as `ren/test-0.8.0` | `2bb0b6b938a7e1b54ae0b7426b1a4879b6af7b07` reported in conversation; Git object not verified | 2026-09-27 | Reactable design/status evidence for D-004 and D-005 |
| Current local syncer/patch source | Exact commit/branch not verified | Unverified | 2026-09-27 | Source-inspection evidence for implementation status of D-004–D-011 |
| September 27 worker verification report | `game mod` Project conversation | Not applicable | 2026-09-27 | Test/log evidence for D-006–D-008 |
| Milestone 2 Navigator arrival-publication investigation and runtime verification | `game mod` Project conversation plus Local game mod report | Not applicable | 2026-09-28 | Updated decision and implementation/runtime evidence for D-006; trace limitations preserved |
| October 3 release baseline and documentation audit | `experiment` plus staged local release state | `0bec442e13893b4da113b363a379f9fe2b74be3a` for clean HEAD | 2026-10-03 | Git/source/log evidence separating merged implementation from staged `0.8.0.4` runtime evidence; corrected D-011 implementation status |
| Reactable lifecycle conversations | `game mod` Project conversations | Not applicable | 2026-09-27 | Vanilla lifecycle observation; not complete authorization verification |

## Revision history

- **2026-10-03:** Added D-024 to exclude personal `docs/Tracking.md` / `docs/AI logs.md` and outdated `TRELLO_PROGRESS_STATUS.md` from automatic documentation audits unless explicitly requested.
- **2026-10-03:** Corrected D-011 after repository inspection proved Operational synchronization remains in `experiment`; recorded PR #53 behavior, preserved the architectural rejection of unsafe initialization-time getter virtualization, and separated implementation status from the earlier removal proposal. Added the October 3 release-baseline provenance.
- **2026-09-28:** Revised D-006 after Milestone 2 proved a late outer-Stop Postfix publication caused the overwrite. The enduring rule is semantic arrival publication before synchronous worker start, with nested same-arrival cleanup suppressed and genuine unrelated Stops retained; the former active-worker idle guard is no longer part of the design.
- **2026-09-27:** Preserved D-001 through D-023 and separated decision status from implementation status; clarified Reactable evidence boundaries; added verified/unverified provenance. No decision or numbering was removed.
- **2026-09-27:** Promoted `ONI_Together_Full_Mod_Architecture.md` from the refreshed `experiment` branch to primary source; restored session/epoch, command, save-cursor/journal, lifecycle, domain-contract, compatibility, recovery, and migration decisions. Obsolete `animation_sync_spec.md` remains excluded.
- **2026-09-23:** Reactable authorization and target animation architecture recorded as living/provisional designs.
