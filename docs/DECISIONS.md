# Multiplayer: ONI United — Architecture Decision Log

**Last updated:** 2026-10-17

**Status:** Recovered decisions from the `game mod` Project and local repository. Decision status and implementation status are separate: accepting a decision does not mean its implementation is complete.

## Decision status legend

- **Confirmed:** Current architectural rule, supported by implementation or test evidence.
- **Provisional:** Preferred design is implemented or selected, but important tests remain.
- **Proposed:** Recovered target design; not verified as current implementation.
- **Rejected:** Do not return to this approach without new evidence.
- **Deferred:** Intentionally postponed.

Implementation status uses only: **Implemented and tested**, **Implemented but further testing required**, **Partially implemented**, **Not implemented**, or **Not independently verified**. These labels report available evidence rather than changing the architectural decision.

## D-000 — Independent development begins with 0.9.0

**Decision status:** Confirmed

**Implementation status:** Partially implemented

**Decision:** `0.8.0.5` is the final upstream-tracking release and the final inherited baseline from the upstream project's MIT-licensed history. Development version `0.9.0` begins an independent project line. No upstream change after the verified MIT baseline may be copied or imported without separately verified compatible licensing or written permission.

**Compatibility policy:** The independent line uses new package, assembly/static, configuration, development-folder, repository, and Workshop identities. Existing C# namespaces remain unchanged. The external mod-integration API and standalone dedicated server are discontinued in `0.9.0` rather than carried forward as compatibility surfaces.

**Naming status:** The permanent public name is **Multiplayer: ONI United**. The main assembly/static identity is `ONI_United`; inherited C# namespaces remain unchanged.

**Removed modules:** The external ONI Together API project, its reflection bridge, and the standalone dedicated-server project are not part of the independent `0.9.0` product.

**Deferred migration:** Verify PLib configuration behavior and save/enabled-mod upgrade behavior after the identity changes. Namespace migration remains out of scope.

**Rationale:** Removing unsupported auxiliary products reduces an untested compatibility burden while preserving the main multiplayer implementation, `Shared`, internal networking, and OxySync.

**Evidence:** `docs/INDEPENDENT_DEVELOPMENT.md`; Git ancestry and identifier audit performed 2026-10-15.


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
**October 3 audited implementation state:** Operational synchronization was still present in the audited `experiment` release-candidate baseline. The complete-removal commit `484bc805dd13e1755b2648192c55b23bd1e7b11e` existed on `ren/test-0.8.0` but was not in that audited `experiment` baseline. The branch contained `OperationalStatePacket`, `RequestOperationalStatePacket`, `Operational_Patch`, `Operational_Patches`, `EnergyConsumer_Patches`, and `ClientReceiver_Operational`; PR #53 changed behavior so clients used vanilla/local state until host state arrived.  
**Later release evidence:** For 0.8.0.4, the user reported that their custom Operational/power work was removed and an upstream fix was merged. The exact final released source tree, upstream commit, and hash were not independently captured, so no stronger current-implementation claim is made here.  
**Rejected alternative:** Unconditionally replacing derived getters with multiplayer shadow values during object construction.  
**Open question:** If current repository inspection later shows a different Operational/power implementation, evaluate it against the enduring initialization-safety rule and the longer-term input-level synchronization direction rather than assuming either the October 3 PR #53 path or the reported 0.8.0.4 state is still current.  
**Source:** October 3 release-baseline repository audit, October 4 release closeout report, plus prior initialization-crash investigations.

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

## D-025 — Final Ready is the authoritative join completion boundary

**Decision status:** Confirmed; implementation evolved after PR #83  
**Implementation status:** Current lifecycle implementation present; exact rebuilt-head runtime revalidation incomplete  
**Decision:** Transport connection, registration, save-transfer completion, entry into `LoadingWorld`, and `Game.OnSpawnComplete` are not by themselves proof that the client may resume ordinary gameplay. The client reaches `InGame` only after the current synchronization/readiness completion for the active synchronization. `ReadyManager` remains the host-side synchronization/readiness barrier and does not become a second owner of the `GameClient` state machine.  
**Historical evidence:** PR #83 implemented this boundary with a pending-join barrier and final `ClientReadyState.Ready` after intentional disconnect/reconnect. A single LiteNetLib joining-client flow was runtime verified. The later lifecycle rebuild removed that pending-join/reconnect machinery, so the PR #83 mechanism is historical evidence rather than the current implementation shape.  
**Rationale:** Intermediate transport or world-load milestones must not prematurely admit gameplay.

## D-026 — Derive join/synchronization overlay presentation from authoritative lifecycle state

**Decision status:** Confirmed  
**Implementation status:** Evolved implementation present; multi-client/current-head presentation matrix not fully revalidated  
**Decision:** Session-level loading/synchronization presentation reflects authoritative lifecycle/readiness state; generic overlay visibility is not itself authoritative. Do not make an arbitrary UI open/close RPC the source of synchronization completion or simulation speed.  
**Historical evidence:** PR #83 used `ClientReadyStatusUpdatePacket` and `AllClientsReadyPacket` over the pending-join readiness model and passed the host/joining-client LiteNetLib scenario. The later lifecycle rebuild changed the underlying loading mechanics but did not make the overlay an authority.  
**Rationale:** Presentation should reflect lifecycle/session truth rather than define it.

## D-027 — PR #83 pending-join PlayerId correlation

**Decision status:** Superseded for normal loading  
**Implementation status:** Historical implementation evidence only  
**Historical decision:** PR #83 reused stable `MultiplayerPlayer.PlayerId` to correlate a pending join across intentional loading disconnect/reconnect. LiteNetLib persistent IDs and SteamID64 fit that model; Riptide was excluded because its player ID was connection-scoped.  
**Evidence:** The LiteNetLib single-join path was runtime verified; Steam was source-reviewed only.  
**Superseded by:** The later transport-preserving client lifecycle, which no longer uses pending joins and intentional normal-load reconnect as its core correlation mechanism. Stable logical identity remains important, but this specific pending-join use is no longer the current normal-load architecture.

## D-028 — PR #83 phase-aware pending-join cancellation

**Decision status:** Superseded for normal loading  
**Implementation status:** Historical implementation/source evidence only  
**Historical decision:** In the PR #83 model, an `Unready` abandoned join was removed while a `Loading` join survived the intentional disconnect for reconnect.  
**Rationale:** That distinction fixed premature all-ready closure in the disconnect/reconnect design.  
**Superseded by:** The later lifecycle rebuild removed pending joins and cached reconnect-era normal-load state. Keep this as evidence for the old implementation, not a requirement to restore the mechanism.

## D-029 — Preserve transport and logical session during normal world loading

**Decision status:** Confirmed  
**Implementation status:** Later `develop` contains an evolved transport-preserving lifecycle; exact conformance to every October 7 design clause is not independently re-audited  
**Decision:** Normal host-save/world loading preserves the transport connection and logical session while the client enters `LoadingWorld` and replaces/rebuilds its world. Reconnect remains for actual transport loss/manual reconnect behavior rather than being the normal successful-load mechanism.  
**Evidence:** The verified `develop` ancestry contains the later client-lifecycle rebuild plus commits removing the loading-client cache, pending joins, cached reconnect-era state, and `readyToProcess`. Historical runtime evidence from that development line also showed transport staying connected through world replacement, but no claim is made that every current-head transport/scenario has been runtime revalidated.  
**Rationale:** World replacement and transport lifetime are separate concerns.

## D-030 — Loading admits only synchronization/control traffic needed for the current lifecycle

**Decision status:** Confirmed  
**Implementation status:** Evolved/partial implementation present; complete wrapper-by-wrapper conformance not independently re-audited  
**Decision:** While the client is `LoadingWorld` or the host synchronization barrier is active, ordinary in-game `NetworkBehaviour` gameplay traffic must not be admitted as normal live gameplay. Only synchronization/control traffic needed to complete the load may pass. Unknown or stale world traffic must not be replayed into the replacement world.  
**October 7 design detail:** Recursive classification through every envelope and discard-not-buffer diagnostics remain the preferred complete contract. Current source should be audited before claiming universal coverage.

## D-031 — World replacement invalidates old-world networking state

**Decision status:** Confirmed  
**Implementation status:** Rebuilt lifecycle contains world-replacement/reset behavior; exact generation/reset contract not fully re-audited  
**Decision:** Replacing the world must invalidate old-world identity, synchronization, queued/pending-reference, and presentation state while preserving only the transport/session/control state that intentionally survives the load. Delayed work from the old world must not mutate the new world.  
**October 7 design detail:** A monotonic `loadGeneration` and a single named `BeginWorldLoadReset(loadGeneration)` boundary remain a useful target/checklist, but this document does not assert that the final current source implements those exact names/mechanics.

## D-032 — World-spawn completion is a readiness trigger, not independent gameplay authority

**Decision status:** Confirmed  
**Implementation status:** Current lifecycle uses the world-spawn completion path to submit readiness; deeper graph-verification contract not fully re-audited  
**Decision:** `Game.OnSpawnComplete` may trigger client readiness work, but it does not independently authorize gameplay. The client becomes `InGame` only after host completion for the active synchronization.  
**October 7 design detail:** Explicit graph validation plus a separate `WorldLoadCompleted(loadGeneration)` acknowledgement remains a stronger proposed contract unless current source inspection proves equivalent behavior.

## D-033 — Host completion for the active synchronization restores client gameplay

**Decision status:** Confirmed  
**Implementation status:** Implemented in the rebuilt lifecycle at the synchronization barrier; exact October 7 packet naming/phase model not fully re-audited  
**Decision:** A loading client does not enter ordinary gameplay merely because its transport is connected or its world spawned. It transitions to `InGame` only when the host completes the matching active synchronization/readiness barrier.  
**Historical note:** PR #83 expressed this as final `Ready` plus no pending joins. The later lifecycle keeps the authoritative completion principle without the pending-join mechanism.

## D-034 — Synchronization requires a complete host-advancement barrier or catch-up

**Decision status:** Confirmed constraint  
**Implementation status:** Pause acquisition implemented/build-verified; full interval coverage not runtime/source-proven for every path  
**Decision:** A client loaded from a host baseline cannot safely join later live state if authoritative simulation advances through an uncovered interval. The current host synchronization entry pauses once in `GameServerHardSync.PerformHardSync` after `BeginSynchronization` acceptance, and the client pauses once in `GameClient.BeginSynchronization`; there is no automatic completion unpause.  
**Remaining question:** Source/runtime verification must still establish that the host remains effectively blocked for the entire interval that requires protection. If it can advance after snapshot capture before admission, a complete catch-up mechanism or stronger barrier is required.  
**Rationale:** Knowing where pause is acquired resolves the earlier ownership question, but does not by itself prove complete temporal coverage.

## D-035 — Keep readiness and overlay presentation speed-agnostic

**Decision status:** Confirmed  
**Implementation status:** Implemented and build-verified; runtime verification not established  
**Decision:** `ReadyManager` remains a readiness/session barrier and does not own `IsSimulationLocked`. `MultiplayerOverlay` does not pause or resume simulation merely because it is visible. `IsSimulationLocked`, `GameSpeedSyncer.RequestReSync`, and load-time lifecycle guards that coupled speed to those generic surfaces were rejected and removed.  
**Rationale:** Readiness and overlay visibility can represent several conditions and do not uniquely identify the synchronization transition requiring a pause. Coupling speed there creates duplicate ownership and risks stale resync or unintended pause/unpause behavior.  
**Rejected alternatives:** Making `ReadyManager` a speed owner; treating generic overlay visibility as a simulation lock; requesting game-speed resync as a load-lifecycle guard.  
**Evidence limit:** The recorded source change built with 0 errors and 203 warnings. No multiplayer runtime test or exact release-artifact verification was established.

## D-036 — Pause only at one-shot synchronization entry points

**Decision status:** Confirmed  
**Implementation status:** Implemented and build-verified; runtime verification not established  
**Decision:** The host pauses in `GameServerHardSync.PerformHardSync` only after `BeginSynchronization` acceptance; the client pauses in `GameClient.BeginSynchronization`. Synchronization completion does not automatically unpause. Ordinary authoritative speed changes remain the responsibility of `GameSpeedSyncer`.  
**Rationale:** These entry points know that synchronization has actually begun and provide narrow, non-duplicated ownership. Avoiding automatic unpause preserves the intended paused state and prevents lifecycle code from guessing the desired post-sync speed.  
**Consequence:** Readiness, overlays, and load-state observation may present or gate synchronization but do not own simulation speed.

## D-037 — Centralize shared session state without absorbing lifecycle or transport mechanics

**Decision status:** Direction confirmed; PR implementation pending regression review  
**Implementation status:** Proposed in PR #86; not merged or runtime-verified in the available session evidence  
**Decision:** Shared session ownership belongs in `MultiplayerSession` and common networking. The current `GameClient` lifecycle, `ReadyManager`/`GameServerHardSync` synchronization ownership, and transport-specific connection mechanics and identity remain with their existing owners.  
**Rationale:** Common host/session/player state should have one authority, while lifecycle, synchronization, and transport details have distinct invariants that should not be folded into a broad session object.  
**Review gate:** PR #86 must be reviewed against current `develop` for lifecycle, identity, cleanup, callback-ordering, and transport regressions before merge.

## D-038 — Keep startup prerequisites ahead of Harmony activation

**Decision status:** Confirmed

**Implementation status:** Implemented but further testing required

**Decision:** `MultiplayerMod.OnLoad` initializes PLib options, required UI assets, persistent configuration identity, and persistent components before calling `base.OnLoad(harmony)`. Harmony activation remains an explicit boundary after those prerequisites. Patch behavior is owned by normal Harmony attributes; startup does not manually register the migrated Door patches.
**Rationale:** A required prerequisite failure must not activate Harmony against an incompletely initialized mod. Configuration, assets, and component owners retain their existing responsibilities; this is an ordering correction, not a new initialization framework.

## D-039 — Keep default packet registration best-effort

**Decision status:** Confirmed

**Implementation status:** Implemented but further testing required

**Decision:** `PacketRegistry.RegisterDefaults` remains in `OnAllModsLoaded`; individual registration failures are logged by `TryRegister` and are not rethrown. Do not abort remaining mod initialization because one packet fails registration.
**Consequence:** The effective packet registry and its ID fingerprint can be incomplete after a registration failure. This is an accepted limitation of the best-effort policy, not evidence that a failed registration is harmless.

## D-040 — Defer generic startup rollback

**Decision status:** Deferred

**Implementation status:** Not implemented

**Decision:** Do not add a generic rollback framework or startup state machine as part of N14. Fatal `OnLoad` exceptions are logged and rethrown, but side effects completed before a failure are not transactionally undone. Revisit only if concrete runtime evidence demonstrates a required recovery path.

## D-041 — Defer N14 runtime verification to synchronization testing

**Decision status:** Confirmed

**Implementation status:** Not independently verified

**Decision:** N14's source review and Debug/Release builds do not establish in-game startup, multiplayer synchronization, transport parity, or release-artifact behavior. Runtime validation remains deferred until the synchronization/integration phase and must be reported with its exact tested build and scenarios.

## D-042 — Adopt three verification levels and Option 3 standalone test projects

**Decision status:** Confirmed

**Implementation status:** Partially implemented; N15-B foundation verified in an uncommitted working tree

**Decision:** Organize automated verification as Level 1 standalone unit tests, Level 2 standalone integration tests, and Level 3 in-game tests. Use Option 3: separate domain-focused projects under `tests/`, beginning with `Shared.Tests`. Eligible production files are source-linked into an isolated test project; do not duplicate production logic or add a project reference that imports ONI, Unity, Harmony, PLib, transport, packaging, or mod-build dependencies.

Migrate the existing 92 in-game diagnostics incrementally. Move a diagnostic only when its behavior can be represented faithfully at Level 1 or Level 2; retain genuinely game-dependent checks at Level 3. Standalone test success never substitutes for in-game integration or exact-artifact runtime evidence.

**Rationale:** This establishes fast deterministic coverage while preserving production assembly boundaries and the value of ONI-dependent diagnostics. Separate test projects can grow by domain without making game dependencies part of the standalone test runtime.

**Verified first increment:** `tests/Shared.Tests` targets `net10.0`, source-links `Shared/Replication/SnapshotTimeline.cs`, has no project references, and removes inherited game/mod build items. Five `SnapshotTimeline` tests passed in Debug and Release. The workflow was locally validated and reviewed for a clean runner but has not run on GitHub. No runtime test was performed. At verification, all N15-B implementation remained uncommitted on `refactor/mod-lifecycle` at HEAD `4ddb2ca893e29adab0db39b2a0747d45641c1b3f`.

## Unresolved decisions

1. Exact TTL and collision handling for Reactable authorization.
2. Whether the vanilla Reactable poll reliably retries when authorization arrives after first local discovery.
3. Safe presentation-only handling for Reactables whose `Begin/Run/End` mutate simulation state.
4. The final persistent activity/snapshot schema for join-in-progress.
5. Reliable ordering/version boundaries between transient presentation and persistent state.
6. What Operational/power implementation is actually present in the current repository, and whether future work should move toward input-level synchronization after current source is re-audited.
7. Consistent ONI save-cut API and whether capture must pause simulation.
8. Durable identity for existing/nested items across save/load and baseline mapping.
9. Which client ONI subsystems can be safely suppressed versus narrowly mirrored.
10. Exact transport modes, payload limits, journal budget, and batching thresholds.
11. Final multiplayer permission policy for pause/speed changes.
12. Whether and how Riptide should gain a stable logical player identity across reconnect.
13. Authoritative permanent-departure event and timeout semantics for issue #82 without confusing intentional loading disconnect with leaving.
14. Whether the current one-shot host pause remains effective across the entire save-capture/client-Loading/final-Ready interval, or whether any uncovered advancement requires catch-up or a stronger barrier.

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
| Join-overlay/readiness session and GitHub tracking | `game mod` Project conversation; issues #78–#82; PR #83 | PR head `cd14ac71bccb1a960f59ba92df0a4abf8cdb1c27`; merge commit `c1c93cb5ab2ae839d28d36d32eff9a565b4f111f` | 2026-10-07 | D-025–D-028 source/decision evidence; LiteNetLib single-join runtime verification; Steam source-only; Riptide exclusion; #82 deferred |
| Transport-preserving loading source review | `game mod` Project conversation and attached Local game mod source-review reports | Not applicable; no new Git/build/runtime evidence | 2026-10-07 | D-029–D-034 approved design; implementation prerequisite and evidence limits recorded |
| Lifecycle/speed simplification and PR #86 preparation | `game mod` Project conversation and Local game mod reports summarized there | `develop` `3ddad5ad7fcaace77292c3f4b4a2218c0aa5db1f`; PR #86 head `a32139b48031202ee41f6bd77bd16701b4b51c35` | 2026-10-15 | D-035–D-037 decision, build, Git, and PR-metadata evidence; no runtime regression verification |
| N14 startup/framework and lifecycle closeout | Local repository `refactor/mod-lifecycle` | `810e0fde0e62900e6ecc240d8d335e94e80b66ca` | 2026-10-17 | Commit ancestry and source inspection; Debug/Release builds; no ONI runtime verification |
| N15-B independent verification report | `game mod` Project conversation | Uncommitted working tree at `4ddb2ca893e29adab0db39b2a0747d45641c1b3f` on `refactor/mod-lifecycle` | 2026-10-17 | Option 3 / `Shared.Tests` isolation review, five Debug/Release tests, and local-only workflow validation |

## Revision history

- **2026-10-17:** Added D-042 for the agreed Level 1/2/3 verification model, Option 3 separate test projects, incremental migration of 92 existing diagnostics, and the bounded N15-B `Shared.Tests` evidence. No commit, merge, GitHub Actions success, or runtime test is claimed.
- **2026-10-17:** Added D-038–D-041 for startup prerequisites before Harmony activation, best-effort packet registration, deferred generic startup rollback, and the N14 runtime-verification boundary.
- **2026-10-15 (reconciliation):** Reclassified D-027/D-028 and the pending-join portions of D-025/D-026 as superseded PR #83 historical implementation evidence; updated D-029–D-033 to reflect the later transport-preserving `develop` lifecycle without asserting unverified clause-by-clause conformance; and revised D-034 to distinguish the now-known pause acquisition points from still-unverified full-interval host-advancement coverage.
- **2026-10-15:** Added D-035–D-037: rejected lifecycle/overlay speed ownership and load-time speed resync guards; assigned pause to accepted one-shot host/client synchronization entry points with no automatic unpause; and recorded session centralization as an accepted ownership direction whose PR #86 implementation remains pending regression review.
- **2026-10-07:** Added D-029–D-034 approving transport-preserving normal loading, per-generation gameplay admission, recursive control-only packet gating, discard-not-buffer behavior, centralized world reset, verified `WorldLoadCompleted` semantics, final-`Ready` admission, and the host-pause-or-catch-up prerequisite. The prior intentional disconnect/reconnect design remains historical verified behavior and fallback, not the desired normal path.
- **2026-10-07:** Corrected D-011 to distinguish the October 3 audited PR #53 implementation from the later user-reported 0.8.0.4 custom-removal/upstream-fix state; removed the stale pre-release question and left current source state explicitly unclaimed until re-inspection.
- **2026-10-07:** Added D-025–D-028 for authoritative final-Ready completion, overlay derivation from readiness, stable-`PlayerId` correlation without a new token, Riptide exclusion, and phase-aware pending-join cancellation. Recorded the unresolved abandoned-`Loading` timeout and permanent-departure decisions. GitHub verified PR #83 merged into `develop`.
- **2026-10-03:** Added D-024 to exclude personal `docs/Tracking.md` / `docs/AI logs.md` and outdated `TRELLO_PROGRESS_STATUS.md` from automatic documentation audits unless explicitly requested.
- **2026-10-03:** Corrected D-011 after repository inspection proved Operational synchronization remains in `experiment`; recorded PR #53 behavior, preserved the architectural rejection of unsafe initialization-time getter virtualization, and separated implementation status from the earlier removal proposal. Added the October 3 release-baseline provenance.
- **2026-09-28:** Revised D-006 after Milestone 2 proved a late outer-Stop Postfix publication caused the overwrite. The enduring rule is semantic arrival publication before synchronous worker start, with nested same-arrival cleanup suppressed and genuine unrelated Stops retained; the former active-worker idle guard is no longer part of the design.
- **2026-09-27:** Preserved D-001 through D-023 and separated decision status from implementation status; clarified Reactable evidence boundaries; added verified/unverified provenance. No decision or numbering was removed.
- **2026-09-27:** Promoted `ONI_Together_Full_Mod_Architecture.md` from the refreshed `experiment` branch to primary source; restored session/epoch, command, save-cursor/journal, lifecycle, domain-contract, compatibility, recovery, and migration decisions. Obsolete `animation_sync_spec.md` remains excluded.
- **2026-09-23:** Reactable authorization and target animation architecture recorded as living/provisional designs.
