# Oxygen Not Included Together — Architecture

**Last updated:** 2026-10-03  
**Document status:** Recovered and consolidated reference. This document preserves the existing architecture recorded in the `game mod` ChatGPT Project and the local repository; it is not a redesign.

## 1. Purpose and source of truth

ONI Together is a host-authoritative multiplayer mod. The host runs the authoritative Oxygen Not Included simulation. Clients send player requests, apply host-owned state, and reproduce selected presentation and state-machine behavior.

This document reconciles these recovered sources, in priority order:

- `ONI_Together_Full_Mod_Architecture.md` on the `experiment` branch (2026-09-23), the whole-mod target architecture and migration guide. This is the primary architecture source. It explicitly does not claim that every described component already exists.
- `ONI_Multiplayer_Animation_Architecture.md` (2026-09-23), the narrower animation/Reactable target design.
- `REACTABLE_SYNC_ARCHITECTURE_STATUS.md` (2026-09-23), the living Reactable design/status record. The recovered conversation reports commit `2bb0b6b938a7e1b54ae0b7426b1a4879b6af7b07` on the then-current `ren/test-0.8.0` branch, but that Git object could not be verified in the currently fetched repository.
- Prior `game mod` Project conversations on Reactables, game speed, worker animations, Navigator ownership, and crash fixes.
- Current local source inspection on 2026-09-27.

`animation_sync_spec.md` is obsolete and is intentionally excluded as a source.

Where those sources differ, this document labels the current implementation separately from proposed or provisional work.

### Implementation status guide

This document contains both current implementation and the accepted target architecture. Architectural acceptance is not evidence of completed implementation.

| Label | Meaning in this document |
| --- | --- |
| **Implemented and tested** | Present in code and supported by recorded multiplayer test evidence for the stated scope |
| **Implemented; further testing required** | Present in inspected source, but the complete behavior or important edge cases have not been verified |
| **Partially implemented** | Some supporting infrastructure or domain behavior exists, but the full architectural contract is incomplete |
| **Proposed / target; not verified** | Accepted direction or migration target without sufficient implementation evidence |

Current evidence supports **implemented and tested** status for ordinary synchronized `StandardWorker` multitool playback/target reconstruction within the September 27 test scope. Reactable host authorization and revisioned game-speed synchronization are **implemented; further testing required**. Existing save transfer/hard sync and identity/network infrastructure are present, but their conformance to the complete target contracts is **partially implemented or not independently verified**.

Unless a section explicitly cites current source or recorded tests, session epochs, per-command IDs and deduplication, replication journals, consistent `T/J` save cursors, ready/catch-up barriers, universal domain versions/hashes, tombstones, and targeted reconciliation describe the **proposed target architecture**, not verified current behavior.

### Release-candidate implementation baseline (2026-10-03)

The merged `experiment` branch is the current integration baseline at commit `0bec442e13893b4da113b363a379f9fe2b74be3a`. That commit reports mod version `0.8.0.3`. A local staged release-candidate state bumps the version to `0.8.0.4` and includes additional logging/documentation edits, so the staged `0.8.0.4` runtime logs are **not** evidence for the clean `0bec442...` HEAD.

Historical multiplayer evidence for Navigator arrival ordering, ordinary `StandardWorker` synchronization, and `AnimSyncer` reliable symbol delivery remains valid for the tested builds in which it was obtained. Those results must not be promoted to exact-release-commit verification until a clean release commit is built and tested.

The October 3 integration audit also identified unresolved release-candidate defects: Pickupable completion still reaches the generic `WorkableSyncer` path and can throw on the client; Reactable authorization can encounter `NetId == 0`; Sweepy can reach patched navigation paths without a `NavigatorSyncer`; and storage-rebuild exceptions require separate investigation. These are implementation/release-status issues, not changes to the enduring host-authoritative architecture.

## 2. Core authority model

The architectural rule is:

> The host decides what happened. Clients reproduce presentation and converge to host-owned state.

Three categories must remain distinct:

| Category | Examples | Delivery and recovery |
| --- | --- | --- |
| Persistent simulation state | Position, inventory, equipment, health, building state | Host-owned snapshots/deltas; repair by reconciliation or hard sync |
| Transient events | Reaction start, emote, effect, sound | Ordered, short-lived events; deduplicate and drop when stale |
| Persistent presentation state | Current locomotion/work/sleep activity and target | Publish changes and include current activity in join/resync state |

Client camera, cursor, selection, hover, and most UI behavior remain local. A client action that changes the world is a request to the host, not an authoritative mutation.

Host authority is an invariant enforced subsystem by subsystem, not a single switch that makes the whole client passive. A client ONI world may need to run local systems for rendering and object lifecycle. Their non-authoritative side effects must be suppressed, isolated, detected, or reconciled. If a subsystem cannot yet be isolated safely, retain only a narrow mirrored behavior with explicit divergence detection until it can be replaced.

The target deliberately does not assume deterministic lockstep, stream full saves every tick, stream animation frames, promise arbitrary third-party-mod compatibility, or provide host migration. In the current design, host disconnect ends the session.

## 3. System map and ownership ledger

```text
client player input
→ command gateway
→ host ONI simulation
→ host replication journal
→ state deltas and semantic events
→ client apply/replay

host save/snapshot baseline
→ client load and identity bind
→ catch-up after journal cursor
→ live stream
```

Every synchronized domain must record:

- authoritative owner;
- which client code still runs;
- player-command capture and validation boundary;
- durable delta/state representation;
- snapshot/baseline representation;
- verification value, version, or hash;
- targeted recovery action;
- known unsupported cases.

An unlisted domain is not considered synchronized. The host owns grid simulation, buildings, construction, duplicants, creatures, chores, inventories, equipment, simulation networks, research, priorities, schedules, policies, effective speed, and canonical saves. Each player owns local camera, cursor, hover, menus, and selection.

## 4. Protocol, transport, and identity

The mod uses a transport-independent networking layer and OxySync components. The current repository contains `Command`, `ClientRpc`, `SyncVar`, interest-group, and `NetworkBehaviour` infrastructure.

Messages belong to six conceptual families:

| Family | Direction | Meaning |
| --- | --- | --- |
| Session control | Both | Hello, compatibility, keepalive, readiness, resync, disconnect |
| Command request/result | Client → host → requester | Intent and acceptance/rejection |
| Entity lifecycle | Host → clients | Spawn, bind, reparent, despawn |
| Authoritative state | Host → clients | Versioned values, deltas, region refresh |
| Semantic event | Host → clients | One-time outcome or presentation cue |
| Snapshot/save | Host → joining/resyncing client | Baseline plus replication high-water mark |

Target gameplay envelopes carry `ProtocolVersion`, `SessionId`, `Epoch`, and an appropriate sequence/version. `Epoch` changes when a host session starts or a new save baseline is installed. Old-epoch packets cannot modify the current world.

Transport policy is semantic rather than tied to one backend:

1. Control, commands, and lifecycle use reliable ordered delivery.
2. Snapshots use bounded reliable chunked transfer with size checks, checksums, and retry/resume policy.
3. State is coalesced by entity/region; loss-replaceable values may use latest-value/periodic repair while indispensable changes remain reliable.
4. Presentation delivery depends on causal importance and bounded age; cosmetic loss never blocks state.

Architectural requirements:

- Use stable host-assigned `NetworkIdentity.NetId` values to correlate entities across machines.
- Never use Unity instance IDs as network identity.
- Validate entity existence and identity before applying remote work.
- Use reliable immediate delivery for authority transitions whose order matters, including current game-speed and Reactable authorization RPCs.
- Use revisions, sequences, or equivalent freshness checks where an older message could overwrite newer state.
- Deduplicate one-shot events and bound any queue for events whose referenced entities are not ready.
- A missing presentation event must not block authoritative state convergence.

Lifecycle order is `Spawn/Bind → State → Event → Despawn`. Entity IDs are guarded by epoch/generation so a late packet cannot bind to a reused object. Updates that arrive before binding may wait in a bounded, expiring queue; recently despawned IDs remain tombstoned briefly.

Interest groups reduce irrelevant traffic. Some state, including Reactables whose effects can matter off-screen, deliberately uses the fixed/global interest group (`InterestGroup = -1`).

## 5. Command pipeline

A command is a request, not proof that a mutation occurred:

```text
client intent + ClientCommandId + expected version
→ host validates session, player, schema, size, target, permissions, and gameplay preconditions
→ host applies on the correct simulation thread
→ host publishes resulting state/events
→ requester receives accepted/rejected result
```

`Accepted` means the request entered the authoritative flow; replicated state confirms the outcome. Command IDs are monotonic per connection/session, and the host keeps a bounded deduplication record so retries cannot apply the same build, priority, or speed request twice. A reconnect establishes an explicit command boundary or a new connection identity.

Host-local world-changing input follows equivalent validation semantics. Domain adapters capture player intent at the player-control boundary rather than misclassifying arbitrary ONI internal changes as commands.

## 6. Join, save transfer, hard sync, and resynchronization

The existing mod supports save-file transfer and hard sync. The intended boundary is:

1. Client sends compatibility hello.
2. Host accepts/rejects and assigns the current session/epoch.
3. At a consistent cut, the host creates a save at tick `T` and records journal cursor `J`.
4. Host sends a manifest and bounded verified chunks.
5. Client verifies, loads, and builds the entity map.
6. Client reports ready for `(epoch, J)`.
7. Host sends catch-up updates after `J`, then admits the client to the live stream.

Snapshots contain persistent world state and the current long-lived activity. They do not replay historical one-shot animation events. A late joiner should receive the current equipment/activity rather than an obsolete equip or emote animation.

The host retains a bounded catch-up journal while the client loads. If the journal overflows, restart from a newer baseline instead of skipping updates. Hard sync uses an explicit phase barrier: request, host safe point/pause, save/cursor, transfer/verify, client unload/load and identity rebuild, catch-up, readiness, resume. The host creates the canonical save; clients never publish their independent saves as canonical.

Reconnect uses missing updates only when the old baseline/cursor remains valid; otherwise it installs a fresh epoch/baseline. Save chunks are written to a temporary file and atomically promoted only after size, checksum, game/mod compatibility, and format checks succeed.

## 7. World and domain replication

Use domain-specific adapters rather than a universal object serializer. Each adapter defines authority, command input, completed host observation boundary, durable/event representation, dependencies, client apply behavior, baseline, verification, and recovery.

- Grid/elements: partition by world and region/chunk; coalesce changed cells; compare region hashes; repair with region refresh rather than per-cell traffic.
- Entities: lifecycle plus versioned components such as position, world, health/status, equipment, inventory, configuration, and work progress.
- Colony settings: accepted command results plus periodic domain snapshots.
- Relationships: storage membership, equipment slot, target/errand references; apply only after both identities exist or expire safely from a bounded queue.

A domain is complete only when command, live outcome, baseline, verification, and recovery agree. “Looked correct during live play” is insufficient if late join or hard sync fails.

## 8. Navigation ownership

`NavigatorSyncer` and Navigator patches make navigation host-authoritative. The host selects the actual transition. The client resolves and replays it, including the host transition anchor and navigation type, through the vanilla `Navigator.BeginTransition(...)` path.

Client autonomous path progression is suppressed except where required for cleanup. Vanilla transition layers, including `ReactableTransitionLayer` and `InterruptOverrideLayer`, remain responsible for stopping and positioning a duplicant at an interaction.

Do not add a second Reactable teleport/barrier/sequencing system when vanilla transition behavior already supplies the interaction point.

### Navigator arrival publication and animation priority

Navigator ownership is not monolithic:

```text
real Navigator transition
    represents locomotion and may interrupt work

active synchronized StandardWorker playback
    owns the work animation

Navigator idle
    is fallback presentation emitted for the completed navigation arrival
```

Synchronization publishes the logical navigation arrival, not every nested vanilla `Navigator.Stop(...)` invocation. For a successful arrival, the host publishes the arrival Stop before synchronous arrival processing can enter `StartWork`. A nested cleanup Stop belonging to that same arrival is not published again. Genuine unrelated Stops must remain synchronized; the final Milestone 2 logs did not independently classify those Stops.

This ordering lets the client apply Navigator idle before worker playback begins, so worker animation ownership follows naturally from the synchronized lifecycle. The former client guard that blocked Navigator idle while `IsInWorkerPlayback` was removed after the publication boundary was corrected. Real transitions remain distinct from idle fallback and may interrupt work.

The completed Milestone 2 runtime run supports this boundary: all 33 host `StartWork` publications matched 33 client applications, 31 clearly observed arrival-idle-to-worker-start cases were correctly ordered, and no Navigator-idle request occurred during active worker playback with the guard absent. Exact host Stop publication counts, navigation sequence continuity, and classification of unrelated Stops were not trace-verified because those diagnostics were disabled.

## 9. Animation architecture

The current implementation is event-driven, with explicit replay scopes and specialized lifecycle synchronization:

- `AnimSyncer` sends host animation, queue, KAnim override, and symbol-visibility changes for networked creatures/minions.
- Client animation calls outside an accepted ownership/replay path are generally blocked.
- `EnterSyncedPlaybackScope()` marks synchronous network-driven replay so client patches permit it and host patches avoid echoing it.
- `EnterOverrideScope()` permits matching KAnim override work while a synchronized action is executing.
- Scope counters must be nest-safe and must be released in a `finally`/Harmony finalizer path.

A synchronous scope is an execution marker, not durable authority. It must not be assumed to cover later coroutine, state-machine, or animation callbacks.

## 10. StandardWorker and Workable lifecycle

Ordinary synchronized work uses the vanilla worker lifecycle on both machines, with the host authorizing each phase:

```text
Host StartWork prefix
    BeginWorkerPlayback(workable NetId)
    send StartWork RPC
    run vanilla StartWork

Client StartWork RPC
    BeginWorkerPlayback(workable NetId)
    run local vanilla StartWork

Successful completion
    send/run CompleteWork
    EndWorkerPlayback(workable NetId) in postfix

Abort
    send/run StopWork
    EndWorkerPlayback(workable NetId) in postfix
```

`CompleteWork()` normally reaches `InternalStopWork()` rather than the public `StopWork()` method. Therefore both `CompleteWork` and aborting `StopWork` require finalization hooks. The active workable NetId prevents an unrelated/stale completion from clearing a newer worker session.

The client should derive ordinary multitool target position/direction locally from the synchronized worker lifecycle and vanilla animation events. Do not add target-coordinate networking unless testing disproves that local reconstruction is reliable.

Special workables such as `Pickupable`, `RancherWorkable`, `LiquidPumpingStation`, `Sleepable`, and animation-override workables require separate handling and must not be used to invalidate the ordinary `StandardWorker` design.

## 11. Reactable architecture

### Vanilla lifecycle retained

The vanilla shape remains:

```text
discover local candidate
→ TryReact(...)
→ Reactable.CanBegin(...)
→ ReactionMonitor stores the Reactable
→ ReactionMonitor enters reacting
→ Reactable.Begin / Update / End
```

The host runs the normal eligibility and state-machine path. It does not globally force `CanBegin()`.

### Host authorization and local client object

The client uses its own locally initialized `Reactable`; it does not attempt to execute the host runtime object. The current implementation registers local Reactables in `ReactableSyncer` by `reactable.id.hash` and authorizes a `(reactableId, reactorNetId)` pair.

The implemented flow is:

```text
Host vanilla CanBegin == true
→ ReactableSyncer.RequestSyncAuthorization(...)
→ reliable RpcAuthorizeReactable(...)
→ client stores authorization
→ client discovers its own local Reactable
→ client CanBegin evaluates InternalCanBegin(...)
→ client consumes the matching authorization once
→ vanilla ReactionMonitor lifecycle proceeds
```

This preserves the preferred design: host authorization replaces client gameplay authority, the client uses a locally discovered Reactable, and matching authorization is consumed once without removing local runtime-safety validation. Source inspection confirms that this mechanism is implemented, but its design status remains **provisional** pending broader multiplayer verification.

Current limitation: authorization is one-shot but the inspected implementation has no explicit expiry timestamp. Packet-first versus local-discovery-first timing and retry behavior still require broader testing.

The earlier verified observation—setting the local Reactable, entering `ReactionMonitor.reacting`, and reaching `Reactable.Begin()`—validates the vanilla local reaction lifecycle path. It does **not** by itself verify the complete multiplayer authorization mechanism, including packet timing, duplicate attempts, expiry, authorization-key uniqueness, state-mutating reactions, or late join/resync.

### Reaction playback scopes

`Reactable.Begin` and `Reactable.End` enter `AnimSyncer` synchronized playback and override scopes for their synchronous calls, with Harmony finalizers releasing the scopes.

Some Reactables mutate simulation state in `Begin`, `Run`, or `End`. A future generic presentation adapter must not blindly invoke those methods. Persistent effects such as equipment changes must converge through authoritative state replication even if visual replay fails.

## 12. Game-speed ownership

`GameSpeedSyncer` implements immediate host-authoritative pause/speed synchronization:

- A client calls `RequestSetSpeed(...)`, which sends a command.
- The host validates `Normal`, `Double`, or `Triple`, applies it through `SpeedControlScreen`, increments a revision, and broadcasts the accepted state.
- Clients ignore revisions already applied.
- A depth counter (`IsApplyingNetworkState`) prevents re-entrant vanilla calls from feeding back into the network path.
- A joining client requests a resync.

This is the preferred pattern for small, latency-sensitive authority changes: command to host, host validation/application, revisioned reliable RPC to clients.

The whole-mod target further requires an absolute desired speed (`Paused`, `Normal`, `Double`, `Triple`) rather than a network “toggle,” plus an explicit multiplayer permission policy. Local toggle UI translates to a desired absolute state. Host tick/time is a drift and freshness reference, not a claim of deterministic simulation.

## 13. Derived state and object lifecycle

Do not synchronize a derived ONI result by replacing its getter during prefab initialization.

Earlier investigations rejected the unsafe form of client `Operational` virtualization that replaced derived getters during prefab initialization. That design could alter ONI semantics before dependent components finished `OnSpawn` and trigger unrelated initialization failures.

The preserved architectural rule is:

> Do not let multiplayer shadow state replace derived ONI getters before normal local initialization is valid. Prefer synchronizing authoritative inputs or applying host-owned state only after the local object is ready, while allowing vanilla state to remain the temporary fallback.

Examples of the preferred input-level direction remain:

- power → actual `EnergyConsumer` input/state
- storage/fuel → `Storage` contents
- generator state → the appropriate generator input/state
- `Operational` → derived by vanilla from valid synchronized/local inputs where practical

**Current `experiment` implementation:** complete Operational/power removal is **not** part of the merged release candidate. The removal commit `484bc805dd13e1755b2648192c55b23bd1e7b11e` exists on `ren/test-0.8.0` but is not an ancestor of `experiment`. The merged branch still contains `OperationalStatePacket`, `RequestOperationalStatePacket`, `Operational_Patch`, `Operational_Patches`, `EnergyConsumer_Patches`, and `ClientReceiver_Operational`. PR #53 revised this path so clients use vanilla/local state until host state becomes available. This implementation requires release-candidate verification and must not be described as removed.

## 14. Compatibility, performance, trust, and failure boundaries

Before joining, peers exchange protocol version, ONI build, mod build, relevant enabled-mod fingerprint, and save-format capability. Incompatibility is rejected with an actionable reason. Payload schemas are versioned and optional features are negotiated explicitly.

The host validates all client-provided IDs, coordinates, strings, sizes, permissions, and semantics. Clients accept authoritative world updates only from the current host/session/epoch. Malformed messages and replay exceptions fail the affected update/session cleanly rather than crashing the process.

Interest/subscription policy may prioritize viewed worlds and regions, but it cannot omit command results or leave referenced entities permanently unresolved. Track bytes by domain, queued bytes, apply/serialization time, join time, tick delay, pending references, hash mismatches, and resync frequency. Prioritize control and small commands over save chunks; bound queues, payloads, retries, and per-tick work.

Failure recovery follows the smallest safe scope: newer state supersedes replaceable loss; missing dependencies buffer briefly then request an entity snapshot; cosmetic replay logs and continues; region/entity mismatches request targeted refresh then baseline; a journal gap restarts baseline; host disconnect ends the session.

## 15. Verification strategy

For every domain compare host/client state after a known command, after a short idle interval, and after reconnect/hard sync. Paired logs include session, epoch, sequence, host tick, entity ID, command ID, and version.

Minimum whole-mod scenarios cover:

1. new-colony join, ready barrier, first command, disconnect;
2. busy-colony late join during grid/entity changes;
3. duplicate/reordered traffic under fault injection;
4. build/dig/deconstruct, inventory/equipment, chore/animation, and speed;
5. save/load failure, journal overflow, reconnect, hard sync;
6. multiple worlds/off-screen regions with region/entity hash comparison;
7. incompatible game or third-party-mod fingerprints.

## 16. Module boundaries and migration

| Module | Responsibility |
| --- | --- |
| Transport/session | Connections, delivery modes, lobby, file transfer |
| OxySync core | `NetworkBehaviour`, commands, RPCs, SyncVars, dirty tracking, interest groups |
| Identity/registry | Stable `NetId` mapping and lifecycle |
| State syncers | Host-owned persistent entity/building/world state |
| `NavigatorSyncer` | Authoritative navigation transition replay and semantic arrival/Stop publication ordering |
| `AnimSyncer` | Animation replay, ownership scopes, overrides, symbols |
| `StandardWorkerSyncer` | Host-authorized ordinary worker lifecycle |
| `ReactableSyncer` | Local Reactable registry and host authorization rendezvous |
| `GameSpeedSyncer` | Revisioned pause/speed command and replay |
| Harmony patches | Observe stable vanilla boundaries and delegate; avoid owning protocol logic |

The target code layout separates protocol, session, commands, identity, state, events, save synchronization, diagnostics, and thin ONI patches. Wire schemas should not mirror private ONI constructors or reflection signatures; version-sensitive calls stay behind adapters.

Migration is incremental:

1. Inventory current patches/syncers in the ownership/coverage ledger and measure divergence.
2. Establish session/epoch, compatibility, command IDs, and diagnostics.
3. Establish consistent baseline cursor, identity binding, catch-up, and ready barrier.
4. Route core player intent through a common host command gateway.
5. Add versioned high-value state and region deltas with verification/recovery.
6. Add semantic presentation events and current-activity snapshots.
7. Add targeted refresh, hashes, interest subscriptions, budgets, and soak evidence.

Do not rewrite every existing syncer first. Wrap current behavior in common session, identity, authority, verification, and recovery contracts, then replace one domain at a time.

## 17. Local-source / ChatGPT Project workflow

Vanilla ONI source and decompiled assemblies remain local. They are not uploaded to the ChatGPT Project.

The shared Project contains these maintained reference documents:

- `ARCHITECTURE.md`
- `DECISIONS.md`
- `STATUS.md`
- `DEVELOPMENT_ROADMAP.md`

For source-dependent work:

1. Manually open the separate **Local game mod** Work Project against the mod repository and local vanilla source. Automatic handoff from this Project is not relied upon because it has failed in practice; prepare/copy a focused inspection prompt instead.
2. Perform code inspection, builds, log analysis, and tests locally.
3. Record confirmed architectural changes in `DECISIONS.md`.
4. Update current evidence and next tests in `STATUS.md`.
5. Update `ARCHITECTURE.md` only when an authority boundary or enduring design changes.
6. Upload the revised reference documents to the existing `game mod` Project.

Do not upload proprietary vanilla source, decompiled assemblies, whole logs containing unnecessary local data, secrets, or machine-specific build paths. Refer to vanilla symbols by class/method name and keep exact source excerpts local.

### Documentation source-selection policy

The following repository files are intentionally excluded from automatic architecture/status audits unless the user explicitly asks to review them:

- `docs/Tracking.md` — personal developer tracking notes; non-authoritative for Project architecture, implementation status, or release readiness.
- `docs/AI logs.md` — personal AI/session notes; non-authoritative and not a maintained Project reference.
- `TRELLO_PROGRESS_STATUS.md` — retained historical/outdated status material; non-authoritative. Do not inspect, reconcile, or use it as a current status source unless explicitly requested.

The maintained Project references remain `ARCHITECTURE.md`, `DECISIONS.md`, `STATUS.md`, and `DEVELOPMENT_ROADMAP.md`, supplemented by the maintained local full-mod/module architecture documents when source-dependent work requires them.

## Source provenance

| Source | Branch | Commit | Inspected | Evidence type |
| --- | --- | --- | --- | --- |
| `ONI_Together_Full_Mod_Architecture.md` | `experiment` / `origin/experiment` | `de5862d731f356a2de899bdade7a9159c942b1a9` (verified) | 2026-09-27 | Primary architectural proposal and migration guide |
| `ONI_Multiplayer_Animation_Architecture.md` | Local working copy associated with the animation work; exact Git branch not verified | Unverified | 2026-09-27 | Supporting architectural proposal |
| `REACTABLE_SYNC_ARCHITECTURE_STATUS.md` | Recorded as `ren/test-0.8.0` in the recovered conversation | `2bb0b6b938a7e1b54ae0b7426b1a4879b6af7b07` reported by the conversation but not present in currently fetched Git objects; unverified | 2026-09-27 | Living design/status record |
| `ReactableSyncer.cs`, `ReactablePatch.cs`, `GameSpeedSyncer.cs`, `AnimSyncer.cs`, `KAnimControllerBase_Patches.cs`, and worker patches | Local working source; exact commit/branch not verified | Unverified | 2026-09-27 | Read-only source inspection |
| September 27 worker verification report in `game mod` Project conversation | ChatGPT Project conversation | Not applicable | 2026-09-27 | Previous multiplayer log/test analysis |
| Milestone 2 Navigator arrival-publication investigation and runtime verification | `game mod` Project conversation plus Local game mod report | Not applicable | 2026-09-28 | Root-cause, implementation, build, and runtime evidence; exact Stop counts and sequence continuity were not traced |
| Reactable lifecycle discussions (`Reactable Step One`, `Patch Reactable Synchronization`) | ChatGPT Project conversations | Not applicable | 2026-09-27 | Previous observations plus architectural discussion |

`animation_sync_spec.md` is obsolete and is not evidence for this architecture.

## Revision history

- **2026-10-03:** Added documentation source-selection policy excluding personal `docs/Tracking.md` / `docs/AI logs.md` and historical `TRELLO_PROGRESS_STATUS.md` from automatic audits unless explicitly requested.
- **2026-10-03:** Recorded merged `experiment` integration baseline `0bec442e13893b4da113b363a379f9fe2b74be3a`, separated staged `0.8.0.4` runtime evidence from clean HEAD, added current release blockers, corrected the false assumption that Operational/power synchronization was removed from `experiment`, and documented manual Local game mod switching because automatic Work handoff is not reliable. No enduring authority model was redesigned.
- **2026-09-28:** Recorded the completed Milestone 2 Navigator arrival-publication boundary: publish successful arrival before synchronous `StartWork`, suppress the nested same-arrival cleanup publication, retain unrelated Stops, and remove the worker-playback idle guard. Added runtime evidence and its trace limitations; made genuine navigation interruption the next milestone.
- **2026-09-27:** Added a prominent implementation-versus-target guide; clarified that long-term session/command/journal/reconciliation mechanisms are not verified implementation; separated vanilla Reactable lifecycle evidence from complete multiplayer-authorization verification; added verified/unverified source provenance. No architecture was redesigned.
- **2026-09-27:** Promoted `ONI_Together_Full_Mod_Architecture.md` from the refreshed `experiment` branch to primary source; restored whole-mod session, epoch, command, replication, save/catch-up, compatibility, performance, trust, verification, and migration architecture. Obsolete `animation_sync_spec.md` remains excluded.
- **2026-09-23:** Original animation target architecture and Reactable architecture/status documents created.
