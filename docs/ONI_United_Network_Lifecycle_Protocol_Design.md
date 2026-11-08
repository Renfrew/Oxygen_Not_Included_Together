# ONI United — Network Lifecycle and Protocol Architecture Design

**Date:** 2026-10-30  
**Status:** Agreed NetworkRuntime, Lifecycle and protocol ownership design\
**Implementation:** Shared lifecycle refinement implemented; native runtime verification remains pending\
**Related decision:** D-048  
**Scope:** Networking operation facade, protocol selection, host lifecycle, host/client exclusivity, EventActions, and Session ownership

---

## Shared lifecycle implementation (2026-11-01)

This approved refinement supersedes the host-only phase/event examples below.
Lifecycle remains the only global multiplayer state machine. NetworkRuntime owns
the selected transports and public routing; Session owns logical membership and
derives host/client role. Native transports retain their handles and cleanup.
No menu-hosting or new `InSession` phase is added.

### Phases and grouped transitions

Phases: `Uninitialized`, `Idle`, `StartingHost`, `PreparingSession`,
`InGameSession`, `Joining`, `Joined`, `Synchronizing`, `ClearingSession`.

`InGameSession` represents a loaded colony and an established host/client Session.
Event producers own world readiness; Lifecycle does not inspect Unity/ONI world
state when accepting HostSessionReady or CompleteSync. The phase alone never
grants host authority.

| Event | Source phases | Destination | Additional permission |
|---|---|---|---|
| Initialize | Uninitialized | Idle | Bootstrap only |
| StartHost | Idle | StartingHost | No active membership |
| PrepareHostSession | StartingHost | PreparingSession | Phase only |
| PrepareClientSession | Joining | PreparingSession | Phase only |
| HostSessionReady | PreparingSession | InGameSession | Host Session |
| ClearHostSession | StartingHost, PreparingSession, InGameSession, Synchronizing, ClearingSession | ClearingSession | Phase only |
| Join | Idle | Joining | No active membership |
| ClientAdmitted | PreparingSession | Joined | Client Session |
| StartHostSync | InGameSession | Synchronizing | Host Session |
| StartClientSync | Joined, InGameSession | Synchronizing | Client Session |
| CompleteSync | Synchronizing | InGameSession | Established Session; producers retain host-authorized round completion |
| ClearClientSession | Joining, PreparingSession, Joined, InGameSession, Synchronizing, ClearingSession | ClearingSession | Phase only |
| SessionCleared | ClearingSession | Idle | Phase only |

Definitions remain compact and grouped; a precomputed
`Dictionary<(Event, Phase), Phase>` rejects duplicate transition pairs.
`Handle(Event evt, Action afterAction = null)` keeps validate, transition,
EventAction, afterAction ordering, exception logging/rethrow and nested events.
Rejected events execute neither action.

### Actions and routing

The private `LifecycleEventActions` dictionary maps StartHost, Join, both
preparation events, HostSessionReady, both clear events and SessionCleared.
Host preparation always registers complete host membership before submitting
matching HostSessionReady. Client preparation always sends the existing Hello;
physical establishment never admits gameplay.

StartHost and Join enter their Lifecycle phases before the selected transport
does native work. Native connection establishment prepares a client Session;
only the existing handshake/admission path establishes client membership.
Host preparation registers host membership before submitting HostSessionReady.
Lifecycle permissions and Session membership determine which gameplay actions
are accepted. No application-level connection-attempt token is maintained.
Native resources remain owned by their protocol implementations.

### Synchronization and verification boundaries

ReadyManager produces StartHostSync; authorized HardSync dispatch calls
GameClient, which produces StartClientSync. Host collective readiness and the
client's matching AllReady after world-spawn readiness produce CompleteSync.
Both roles return to InGameSession, without a role-dependent destination or
automatic unpause. Synchronization IDs, packet encodings and the host-controlled
round algorithm are unchanged.

Delayed synchronization work uses the existing synchronization ID. World-spawn
completion only sends readiness; it does not independently grant gameplay
admission.

Shared.Tests source-links Lifecycle, EventActions, NetworkRuntime routing,
TransportServer, the live Steam client and client synchronization
continuations. Session activation, Unity scheduling/world loading and Steam/LAN
native APIs still use deterministic doubles. Passing these tests or a
non-deploying Debug build is not live multiplayer verification.

Remaining runtime gates: Unity synchronous activation/cleanup subscribers;
LAN listener/TCP partial startup and cleanup retries; Steam CreateLobby/JoinLobby
cancel-before-result, stale resource release and failed release retries; native
connection callback/handle reuse; disconnect during world load and old-world
spawn callbacks; collective host/client synchronization with reused IDs; packet,
save and UI behavior in both roles. Existing Steam UI availability gates remain.

---

## 1. Purpose

ONI United supports LiteNetLib and Steam networking implementations with different startup and shutdown mechanisms. The common architecture must support these and future backends without duplicating host behavior or coupling Lifecycle to networking-library details.

This document records the revised approved target, not an implementation authorization. Refine and rename the existing `NetworkConfig` class to `NetworkRuntime`; do not add a second facade or restore `GameServer`. NetworkRuntime owns public operation routing and the selected live transport set. Lifecycle owns permission, transitions and EventAction dispatch; LifecycleEventActions coordinates essential work through existing owners. TransportServer retains common instance-level hosting behavior and delegates native execution to concrete protocols.

The host implementation checkpoint removes GameServer and implements Lifecycle-owned EventActions with PrepareSession, TransportStarted and SessionPrepared. Reported successful compilation and standalone tests establish neither native LAN/Steam runtime behavior nor implementation of the NetworkRuntime, extracted EventAction and client-exclusivity refinements specified here.

## 2. Core architectural model

### 2.1 Lifecycle — permission, transitions and EventActions

`Lifecycle.cs` defines phases/events, determines event permission, performs transitions and resolves the essential **EventAction** associated with an event. The dispatch mapping remains internal to Lifecycle; a switch or dictionary is sufficient. Essential coordination is extracted into `LifecycleEventActions.cs`. This is code-ownership separation, not a separate event-action manager.

For an accepted event, the destination phase is established before the EventAction, followed by the caller callback last. A rejected event invokes neither. EventActions and callbacks use Action, not bool-returning delegates. An unexpected EventAction exception prevents callback execution, is logged and propagates as a fatal lifecycle integrity failure. Lifecycle does not perform native networking-library operations, own protocol handles or add another copy of backend execution state.

### 2.2 LifecycleEventActions — essential event-linked coordination

LifecycleEventActions executes essential operations associated with accepted events and coordinates existing Session, identity, networking and presentation modules. It delegates domain implementations to their established owners. It owns no independent phase, transition permission, operation state machine, native protocol handle, protocol selection, registry or application-specific continuation. It is not a replacement GameServer coordinator.

Lifecycle retains dispatch authority and invokes this coordination after transition and before the supplied callback. Moving the method bodies out of Lifecycle.cs does not change the execution contract.

### 2.3 TransportServer — common instance-level hosting behavior

NetworkRuntime exposes public host operations and routes them to the selected TransportServer instance. TransportServer provides common instance-level Start/Stop operations through Lifecycle.Handle. It owns protocol operation completion, operation diagnostics and retained application continuations. Concrete protocols supply specialized callbacks and report their outcomes through the common transport path, which submits the appropriate Lifecycle events. TransportServer does not own global protocol selection or a static registry of server implementations.

TransportServer does not supply mandatory EventActions or duplicate Lifecycle permission. In particular, Session activation cannot be omitted by a protocol callback because Lifecycle resolves the EventAction internally. Backend-specific startup/cancellation branches do not belong in common host behavior.

### 2.4 Concrete protocols — execution and resource ownership

Concrete transport implementations own native startup, listeners, connections, discovery/lobby mechanics, callbacks, pending operations, cancellation and physical resources. They determine when their own initialization or cancellation has completed. Completion may be synchronous or asynchronous.

They retain unresolved native work through cancellation, handle repeated cancellation and explicit resource-cleanup retry, and report outcomes without directly assigning Lifecycle phase. Internal callback milestones need not become Lifecycle events.

### 2.5 Session and existing specialized owners

Session remains a simple static logical data owner: local/host identities, admitted membership, invariants and derived activity/role. Its defensive validation remains necessary. It does not authorize Lifecycle events, operate protocols, own callbacks or implement cancellation. D-046's removal of SessionController and SessionActiveChanged is preserved.

The target logical host operation is `Session.BeginHosting(host)`, invoked through LifecycleEventActions after transport readiness. The legacy `MultiplayerSession.RegisterAsHost()` dependency is not the permanent registration route. Removing that dependency must preserve necessary activation notifications and networking integration, including local host name/readiness initialization and name/presentation metadata currently associated with registration. The migration must inspect those side effects and preserve their required ordering around complete logical initialization. They belong with appropriate existing owners; do not silently delete them or copy the legacy method wholesale into LifecycleEventActions.

Network identity, presentation, network ticking, admission policy and application-specific behavior retain their specialized owners. Lifecycle links essential coordination to events; LifecycleEventActions does not absorb their domain implementations. This requirement does not claim completion of the wider MultiplayerSession migration or redesign Session serialization, Player, identity values or host authority.

### 2.6 NetworkRuntime — public facade and live transport ownership

NetworkRuntime is the renamed and refined existing NetworkConfig, not an additional networking manager. The old name was misleading: source investigation established that the class primarily holds runtime transport references, selects protocols, routes operations and answers network queries. Persistent settings remain in `Configuration`.

NetworkRuntime owns public networking operation entry points, selected protocol identity, live server/client/packet-sender references, protocol selection and replacement, runtime routing and relevant queries. It does not own Lifecycle transitions, EventActions, native implementations, Session identity/membership or an independent host/client state machine. Host/client operations must be explicit rather than hidden behind an ambiguous generic Stop.

The existing TransportRegistry remains a driver catalog/factory source. It must not provide a competing active-selection authority. A static protocol registry and global operation API inside TransportServer are superseded; section 8 defines the shared ownership contract.

---

## 3. Event-action dispatch

The conceptual API is:

```csharp
public static bool Handle(Event evt, Action callback = null)
{
    if (!TryTransition(evt))
        return false;

    try
    {
        if (EventActions.TryGetValue(evt, out var eventAction))
            eventAction();

        callback?.Invoke();
        return true;
    }
    catch (Exception ex)
    {
        DebugConsole.LogError(
            $"[Lifecycle] Event {evt} failed in phase {Current}: {ex}");
        throw;
    }
}
```

This is conceptual pseudocode, not a mandatory implementation. `EventActions` denotes Lifecycle's internal lookup of coordination supplied by LifecycleEventActions; a switch or dictionary is sufficient. Use the term **EventAction**, including when it invokes an existing shared operation. LifecycleEventActions supplies no independent dispatch permission or state. No separate event-action manager or failure-aware callback type is introduced.

Accepted execution order is strictly:

```text
validate permission → transition → EventAction (if present)
  → caller callback last (if present) → return true
```

| Situation | Result and execution |
|---|---|
| Event rejected | false; neither EventAction nor callback executes |
| EventAction and callback return normally | true |
| Neither action is present | true after the accepted transition |
| Unexpected EventAction exception | log and rethrow; callback does not execute |
| Unexpected caller callback exception | log and rethrow |

False means event rejection, not an accepted action's failure result. True means the accepted synchronous sequence returned normally, not that asynchronous networking completed. An Action callback may initiate native work and return; Handle then returns true, while actual completion is reported by a later event. The earlier bool-returning callback proposal is superseded.

### Fatal lifecycle integrity failure

An unexpected exception in an accepted EventAction or caller callback is a **fatal lifecycle integrity failure**. Log the event, current phase and exception, then propagate it. Do not swallow it and resume normal multiplayer execution with uncertain state. There is no automatic rollback, reset to Idle, recovery state machine or hard reset. A partial cleanup attempt is not confirmed cleanup. Emergency cleanup is a separate consideration, not approved automatic recovery.

Propagation alone does not guarantee ONI process termination: Unity or an outer handler may catch the exception. The intended policy is fail-fast; this document does not claim that the conceptual catch/rethrow forces the game to exit. A transitioned phase left by such a failure is not evidence that its normal operation completed successfully.

### Expected operational outcomes

Port unavailability, a protocol-reported connection/listener failure, Steam lobby creation failure, explicit cancellation and a reported cleanup failure remain normal operational outcomes. The owning protocol handles expected conditions and reports them through the established events/cancellation rules rather than deliberately crashing the game. Expected native API exceptions may be handled at that protocol boundary and translated into those outcomes; unexpected exceptions escaping the EventAction/callback still follow the fatal policy.

Failed protocol cleanup remains unresolved in Stopping and requires explicit retry. Do not confuse this with recovery from an unexpected lifecycle-integrity exception. Failure status is independent of whether an error message is empty.

### Nested synchronous events

Nested Handle calls validate against the phase that currently exists. If a nested Stop enters Stopping, a later SessionPrepared is rejected and neither of its actions executes. Do not add a second state machine, generation counter or automatic supersession mechanism merely to handle nesting.

An already executing C# EventAction or callback is not automatically interrupted by a nested event. It continues under normal C# control flow. A subsequent event rejection does not undo work already performed. An unexpected exception propagates without automatically rolling back either the outer or nested transition.

---

## 4. Lifecycle state and event model

### 4.1 Approved host transitions

```text
Uninitialized
    │ initialization (bootstrap)
    ▼
Idle
    │ Start
    ▼
StartingHost
    │ TransportStarted
    ▼
PrepareSession
    │ SessionPrepared
    ▼
Hosting
    │ Stop
    ▼
Stopping
    │ Stopped
    ▼
Idle
```

| Event | Allowed source phase | Destination | Meaning |
|---|---|---|---|
| Start | Idle | StartingHost | Host startup is admitted only when client-operation exclusivity also permits it |
| TransportStarted | StartingHost | PrepareSession | Protocol readiness permits logical Session preparation |
| SessionPrepared | PrepareSession | Hosting | Transport and logical host Session preparation succeeded |
| Stop | StartingHost, PrepareSession, Hosting | Stopping | Begin cancellation and shared cleanup |
| Stop | Stopping | Stopping | Repeat the EventAction/callback sequence without another transition |
| Stopped | Stopping | Idle | Required essential and protocol cleanup are complete |

Uninitialized admits no operational hosting event. During normal successful execution, Hosting guarantees transport initialization and host Session preparation, not remote-player presence or client synchronization/gameplay readiness. An unexpected integrity failure is not permission to continue from a partially executed phase. No separate generic Ready phase is needed.

### 4.2 Event meaning and native callback correlation

Phases describe conditions; descriptive events identify occurrences. Stopped means complete host-operation cleanup, not merely a closed listener. Repeatable Stop is intentional and adds no separate cleanup phase. Meaningful shared and protocol-specific events may represent genuine permission changes; internal native milestones need not become events. This does not add any particular event or phase to the approved host route.

An event name does not identify a native asynchronous request. Protocols retain native callback/resource ownership, and Stopping blocks a new Start while earlier obligations remain unresolved. These rules do not require an additional generation counter in Lifecycle.

### 4.3 Host/client mutual exclusion and selection permission

Lifecycle governs mutual exclusion of host and client operations, not only the host route drawn above. A client can be Connecting while Lifecycle is Idle, Session.IsActive is false and Session.Players is empty in the current host-only implementation. Logical membership alone therefore cannot establish permission to host or replace a transport.

The architectural requirements are:

- Reject host Start while a client connection is pending, active or cleaning up.
- Reject client Connect while a host operation is starting, active or stopping.
- Reject protocol selection while either side owns an active or unresolved operation, including pending native work and required cleanup.
- Enforce these permissions centrally in Lifecycle. UI visibility and developer-tool checks are not the permission authority.

The host transition table remains approved. The exact client Lifecycle phases, events and transition table are not defined or approved by this specification; no final client names or implemented client route are asserted here. Direct GameClient.State checks inside host EventActions are a current implementation dependency, not the intended permanent exclusivity mechanism. The target must preserve their necessary pre-admission protection through central permissions rather than simply deleting them.

---

## 5. Host startup

### 5.1 Common Start entry point

```text
Caller → NetworkRuntime.StartHost(...) → selected TransportServer.Start()
  → Lifecycle.Handle(Start, protocol startup callback)
  → StartingHost
  → Lifecycle dispatches the Start EventAction through LifecycleEventActions
  → protocol startup callback runs last
```

Concrete protocols perform their required validation and native startup through their specialized callback. Shared preparation delegates identity, ticking and presentation operations to existing owners. An Action callback may complete synchronously or initiate asynchronous work and return normally. A true Handle result is not by itself proof that Hosting has been reached.

If an unexpected exception occurs in the Start EventAction, the protocol startup callback is skipped, the event/phase/exception are logged and the exception propagates. The transition is not rolled back and normal multiplayer execution must not silently continue. Expected protocol startup failures instead use the normal Stop/cancellation route.

### 5.2 Transport readiness permits Session preparation

```text
Protocol confirms required listener/lobby readiness
  → common TransportServer completion path
  → Lifecycle.Handle(TransportStarted, callback)
  → PrepareSession
  → LifecycleEventActions coordinates Session.BeginHosting(host)
  → appropriate existing owners preserve required integration and notifications
  → callback executes last only after the EventAction returns normally
```

Session.BeginHosting retains its defensive null/nonzero identity, inactive Session and empty-membership checks. It creates the logical host record and identities; it does not transition Lifecycle or release native resources.

### 5.3 Successful Session preparation grants Hosting

After successful logical activation, the common transport path submits SessionPrepared:

```text
successful Session-preparation EventAction
  → Lifecycle.Handle(SessionPrepared, callback)
  → Hosting
  → associated EventAction, if any
  → caller callback last
```

Session keeps its defensive bool-returning data operation; that does not make EventActions bool-returning. An unexpected inability to establish the required logical Session must not advance to SessionPrepared or be treated as successful execution. Unexpected execution exceptions in the accepted EventAction are fatal, logged and propagated, rather than swallowed into a normal recovery flow. Hosting remains distinct from physical readiness alone, remote admission and client gameplay readiness. Remote admission requires valid logical host membership and appropriate transport readiness.

---

## 6. Host cancellation and shutdown

### 6.1 Common Stop entry point

```text
Caller → NetworkRuntime.StopHost(...) → selected TransportServer.Stop()
  → Lifecycle.Handle(Stop, protocol cancellation callback)
  → Stopping (or remain Stopping)
  → LifecycleEventActions deactivates Session and invokes essential shared cleanup
  → protocol cancellation callback runs last after the EventAction returns normally
```

Session deactivation belongs to Stop, not Stopped. Shared Stop work is repeat-safe and invokes its existing owners. The protocol releases resources from its own handles/maps, independently of logical Session records already cleared.

### 6.2 Unexpected cleanup exceptions versus protocol failure

An unexpected exception in the Stop EventAction is a fatal lifecycle integrity failure. The normal protocol callback is skipped; log the event, current phase and exception and propagate it. Do not swallow the exception, report Stopped, reset to Idle or resume normal multiplayer execution as though partial cleanup succeeded. The already-performed transition remains in effect; emergency cleanup is not an approved automatic reset or recovery state machine.

A protocol-reported cleanup failure is different: it is an expected operation result, remains unresolved in Stopping and permits explicit repeated Stop to retry unfinished cleanup. It must not trigger automatic retries. Essential cleanup and protocol cleanup must both be confirmed before Stopped. Do not confuse an empty error message with success.

### 6.3 Protocol cancellation and confirmed completion

Protocols own pending native operations, cancellation progress, resource release and their completion/failure results. They must not report completion while a native request can still acquire an unhandled resource or affect a newer attempt. TCP cleanup closes its sockets and observes worker termination without adding main-thread blocking joins. No new blocking joins are introduced; this does not claim that an existing dependency's internal shutdown is nonblocking.

```text
required essential cleanup succeeded
  + protocol confirms required cleanup
  → common TransportServer completion path
  → Lifecycle.Handle(Stopped, callback)
  → Idle
  → associated EventAction, if any
  → callback last
```

Stopped is a completion event, not a place to perform essential, potentially failing cleanup. Idle means cleanup is sufficiently complete for another permitted hosting or joining operation. Protocol success alone cannot authorize it after essential cleanup failure.

### 6.4 Repeated Stop and retained continuations

Stop remains permitted in StartingHost, PrepareSession, Hosting and Stopping. In Stopping it executes the EventAction/callback sequence again without another transition. No duplicate phase flag or Server cleanup state machine is needed.

In-flight cancellation retains its original unresolved completion and does not schedule duplicate cleanup. After reported cleanup failure, an explicit repeated Stop may retry unfinished work; do not automatically retry. Shared Stop operations also remain safe under repetition.

Preserve D-044's save-load contract: retain the original filename continuation across ordinary repeated Stop while cleanup is pending; invoke it only after confirmed cleanup and entry to Idle. Failure discards the continuation, and ordinary retry does not resume that failed load. Disposal discards it and initiates resource release without requiring another frame, preserving D-045.

---

## 7. Steam asynchronous startup and cancellation

Steam illustrates why concrete protocols own asynchronous native work while the common transport path submits Lifecycle events.

### 7.1 Normal startup

```text
NetworkRuntime.StartHost → selected TransportServer.Start → Start → StartingHost
  → Start EventAction → Steam startup callback
  → correlated lobby creation and required listener readiness
  → common transport reports TransportStarted
  → PrepareSession → LifecycleEventActions coordinates Session.BeginHosting(host)
  → successful continuation submits SessionPrepared → Hosting
```

Steam call handles, lobby IDs, socket handles and callback registrations remain inside their resource owners. Lifecycle does not interpret native Steam callback types or implement lobby mechanics.

### 7.2 Stop while lobby creation is pending

```text
Steam creation pending → StartingHost
Stop → Stopping → Stop EventAction → protocol cancellation callback
  → retain the original pending creation request
  → reject new Start while unresolved
terminal native result arrives
  → resolve the original request and release acquired resources
  → confirm essential and protocol cleanup
  → common transport submits Stopped → Idle
```

The sequence above assumes the Stop EventAction returns normally. An unexpected exception follows section 6.2's fatal policy, not a silent recovery path. A normal protocol cleanup failure retains unresolved native ownership and follows explicit retry rules.

### 7.3 Late transport readiness and native results

A late TransportStarted submitted in Stopping is rejected; neither its Session-preparation EventAction nor its callback executes. An already running native callback is not interrupted automatically. The owning protocol must resolve and release resources from the canceled request before cleanup can be confirmed.

A protocol must not report Stopped merely because its visible listener is closed while lobby creation remains pending. Event permission does not replace native result/resource ownership, and no additional coordinator or automatic supersession mechanism is introduced.

### 7.4 Steam Timeout

Steam may return Timeout or another terminal failure through its API. That is a native operation result, not an ONI United timeout policy. The owner processes it and completes required cleanup; startup failure while StartingHost requires Stop before Stopped can be accepted.

A local timeout does not prove release of unknown native resources. If no result arrives and cleanup cannot be established, the operation remains unresolved in Stopping. No automatic retry or premature Idle is permitted.

---

## 8. NetworkRuntime facade and shared protocol interfaces

### 8.1 Public networking operations

The public facade expresses explicit host/client intent. Conceptual examples, not mandatory final signatures, are:

```csharp
NetworkRuntime.SelectProtocol(...);
NetworkRuntime.StartHost(...);
NetworkRuntime.StopHost(...);
NetworkRuntime.StopHostAndLoad(...);
NetworkRuntime.Connect(...);
NetworkRuntime.CancelConnection(...);
NetworkRuntime.DisconnectClient(...);
```

NetworkRuntime routes operations to the consistent selected live instances under Lifecycle permission. It must not duplicate EventActions, client protocol coordination or native execution. Host stop-and-load retains the existing operation-owned continuation contract; the facade does not place load execution inside Lifecycle or LifecycleEventActions. Client cancellation/disconnection must address the owned connection attempt even before logical Session admission; Session.IsClient alone is not proof of physical ownership or release.

### 8.2 Selected protocol and transport lifetime

NetworkRuntime controls one authoritative selected protocol identity and one consistent live server, client and packet-sender set. External callers cannot arbitrarily replace those references. Factories create objects; getters return the owned live instances. An operation or query requiring a live owner must not silently obtain a fresh implementation through a factory-style getter.

Selection and replacement obey Lifecycle permission and cannot abandon active connections, pending callbacks, native resources or sender queues. Required cleanup and pending ownership remain with the original owner until resolved. Native callbacks and disposal retain access to that owner rather than resolving cleanup against a later selection. An unexpected concrete-protocol/selected-identity mismatch during an accepted operation is an integrity failure, not permission to fall back to a different backend.

TransportRegistry remains the driver catalog/factory source. Its catalog must not create an independent selected protocol. The proposal to place a static protocol registry and global Start/Stop/selection API in TransportServer is superseded.

Permanent caching of all protocol instances is not required. Reuse must be supported by actual safe resource, callback and queue-reset behavior. Do not introduce a redundant active-server reference when the protected selection and owned live references already identify the operation; an additional reference requires concrete resource-lifetime evidence. No additional selection state machine is introduced.

### 8.3 Instance-level protocol behavior

Use existing ITransport, TransportServer and TransportClient abstractions where applicable. NetworkRuntime is the public facade; TransportServer retains common instance-level host Start/Stop dispatch through Lifecycle.Handle, completion, diagnostics and retained continuations. TransportClient retains appropriate client transport behavior. Concrete implementations supply protocol-specific callbacks and native execution. Lifecycle dispatches mandatory EventActions through LifecycleEventActions; a backend callback cannot replace or omit them.

| Responsibility | LiteNetLib | Steam |
|---|---|---|
| Native startup | UDP listener and optional TCP transfer | Lobby and P2P listener initialization |
| Completion timing | May be synchronous | May be asynchronous |
| Discovery | Existing LAN discovery | Existing lobby discovery/membership |
| Physical cleanup | UDP/TCP resources and workers | Pending lobby work, sockets and callbacks |
| Lifecycle reporting | Common instance-level transport completion path | Common instance-level transport completion path |

LiteNetLib retains UDP startup, optional TCP transfer and generic transfer fallback. Keep physical identity mechanics backend-specific while preserving common logical identity and host-authorized admission. Persistent settings remain in Configuration. Session, Player, client synchronization, wire format and save serialization are not redesigned here. Refining NetworkConfig into NetworkRuntime does not authorize another coordinator or umbrella interface.

---

## 9. Lifecycle events and nested execution

The approved host events are Start, TransportStarted, SessionPrepared, Stop and Stopped. Native milestones that do not change Lifecycle permission stay inside their protocol. This revision introduces no additional events or phases.

Subsequent events use the current phase. A nested Stop can make a later SessionPrepared invalid; rejection executes neither its EventAction nor callback. That does not cancel the C# callback already executing or undo earlier side effects.

No second state machine, generation counter or automatic supersession mechanism is added merely for nesting. Protocols continue to own stale native callback/resource handling, and retain unresolved obligations until confirmed cleanup permits Stopped.

---

## 10. Architectural guarantees

1. Lifecycle owns event permission, transitions and internal EventAction lookup.
2. Accepted execution is transition, EventAction if present, then caller callback last.
3. Rejection returns false and executes neither action; accepted normal execution returns true, including when neither action is present.
4. EventActions and caller callbacks use Action; normal callback return can mean asynchronous initiation, not completion.
5. Unexpected EventAction/callback exceptions are fatal integrity failures: log event/phase/exception and propagate; do not swallow, roll back, reset to Idle or add automatic recovery. Expected operational failures use protocol outcomes and cancellation rules.
6. NetworkRuntime owns public operation routing, selected identity and live transport references. TransportServer retains common instance-level host dispatch, completion, diagnostics and retained continuations, without duplicating phases.
7. Concrete protocols own native execution, callbacks, connections, resources and cancellation.
8. LifecycleEventActions is stateless essential coordination, not another permission authority. Session.BeginHosting retains defensive validation; necessary legacy integration stays with appropriate existing owners.
9. TransportStarted enters PrepareSession before its Session-preparation EventAction; SessionPrepared grants Hosting after success.
10. Stop deactivates Session and performs essential cleanup; Stopped requires confirmed essential and protocol cleanup.
11. Repeatable Stop retains pending completion and permits explicit retry of unfinished cleanup after failure.
12. Pending native work retains ownership through cancellation; no new Start is permitted while unresolved in Stopping.
13. Already executing callbacks are not automatically interrupted by nested events.
14. GameServer is absent from the target host architecture; no replacement coordinator, generic asynchronous framework or second phase authority is introduced. Meaningful shared/protocol-specific events remain possible without turning every native callback into a phase.
15. No automatic unpause or disconnect merely because UI closes; preserve LAN TCP fallback, discovery, identity distinctions and save-load continuation.
16. Lifecycle centrally excludes overlapping host/client operations and active-operation protocol selection. Exact client phases/events are not specified by this revision.
17. NetworkRuntime alone controls selection and the consistent transport set; TransportRegistry is a catalog/factory source and TransportServer has no global registry.
18. Replacement and disposal preserve original resource/callback ownership; caching requires safe reuse/reset evidence rather than being mandatory.

---

## 11. Relationship to existing architecture decisions

This revision supersedes conflicting earlier ownership and callback semantics; it does not erase the historical decisions or their implementation evidence.

**D-043:** Preserve host transitions, event permission and transition-before-action. Extend permission responsibility to host/client mutual exclusion and selection without defining the client transition table. Supersede the earlier permission-only/caller-owned essential execution description: Lifecycle resolves its EventAction internally and delegates essential coordination to stateless LifecycleEventActions. Handle uses Action callbacks and returns false for rejection or true after normal accepted execution. Unexpected execution exceptions are logged and propagated as fatal integrity failures; earlier bool-returning action-failure handling is superseded.

**D-044:** Preserve repeatable Stop, unresolved completion retention, confirmed cleanup, explicit retry after protocol-reported cleanup failure, no automatic retry and the original save-load continuation guarantees. These are normal operation rules, not authorization to swallow an unexpected EventAction exception or resume normal execution after an integrity failure. Partial cleanup or a local timeout proves no complete native release.

**D-045:** Component destruction and application termination still initiate immediate resource release without requiring another frame. They do not introduce an operational terminal phase; disposal does not promise every asynchronous worker has finished before exit.

**D-046:** Preserve static Session, logical identities/membership, invariants, derived roles and networking-owned notifications. Replace the permanent legacy host registration dependency with Session.BeginHosting(host), preserving necessary notification and integration side effects through appropriate existing owners. This does not assert that wider MultiplayerSession migration is complete. No SessionController, independent Session activity event or redundant lifecycle flags are restored.

**D-047:** Synchronization rounds, snapshot validity and client readiness remain separate and unchanged.

**D-048:** Supersede the required Server/GameServer coordinator, caller-supplied mandatory actions and conflicting recover-and-continue execution policies. NetworkRuntime refines the existing facade for public operations and selected-instance ownership; it does not restore that coordinator. Retain common instance-level transport behavior, native resource ownership and confirmed cancellation. The static TransportServer registry proposal is superseded. Preserve the later PrepareSession refinement: TransportStarted permits Session preparation, and SessionPrepared grants Hosting only after success. Earlier direct Started-to-Hosting activation ordering and the subsequent bool-returning callback proposal are superseded by this EventAction/Action and fatal-exception contract.

The four maintained project documents retain the broader development status, planning and historical records. They are not modified by this documentation update.

---

## 12. Protocol-selection presentation

- When Lifecycle permits selection, setup UI may offer LAN/Steam protocol selection.
- During active host operations, protocol selection is disabled.
- During client connecting, connected or disconnecting operations, protocol-selection controls are hidden.
- Direct APIs and developer tools enforce the same Lifecycle selection permissions independently of UI visibility.
- UI changes follow UI_DESIGN_PRINCIPLES.md: Shared/UI owns presentation and interaction internals; the mod supplies domain decisions through supported APIs. No hierarchy or layout redesign is implied by this policy.
