# Reactable Synchronization Architecture & Status

**Repository:** Renfrew/Oxygen_Not_Included_Together  
**Branch at time of writing:** `ren/test-0.8.0`  
**Status:** Provisional design — requires further multiplayer testing  
**Last updated:** 2026-09-23

---

## 1. Purpose

This document records the current architecture and investigation state for synchronizing ONI `Reactable` behavior in multiplayer.

It is intentionally a living document. The design may change when testing or new source-code analysis shows that an assumption is incorrect. When that happens, update this file so it reflects the current architectural decision rather than preserving outdated implementation ideas.

The immediate goal is to support reactions such as:

- transition/environment reactions,
- door/gate interactions,
- oxygen-mask / suit-station interactions,
- self-emotes and other duplicant reactions,

while keeping the multiplayer simulation host-authoritative and preserving as much of ONI's native state-machine behavior as possible.

---

## 2. Core Authority Rule

The host is authoritative for gameplay decisions.

For Reactables:

> The host decides whether a reaction is authorized. The client should not independently decide that the same gameplay reaction is allowed.

For navigation:

> The host selects and authorizes navigation transitions. Clients replay those transitions instead of performing their own navigation/path authorization.

The client should still use its own valid local objects and vanilla state-machine lifecycle wherever possible.

---

## 3. Relevant Vanilla Architecture

A `Reactable` is not an isolated animation call. It participates in a larger vanilla lifecycle.

Conceptually:

```text
ReactableTransitionLayer / surrounding system
        |
        v
discover candidate Reactable
        |
        v
TryReact(...)
        |
        v
Reactable.CanBegin(...)
        |
        | true
        v
ReactionMonitor stores Reactable
        |
        v
ReactionMonitor -> reacting
        |
        +--> Reactable.Begin(...)
        +--> Reactable.Update(...)
        +--> Reactable.End(...)
```

The `ReactionMonitor` state machine owns the reaction lifecycle.

A successful reaction is effectively committed when the selected Reactable is stored in the monitor and the monitor enters its `reacting` state.

---

## 4. Navigation and InterruptOverrideLayer

The current multiplayer navigation implementation is already host-authoritative.

Relevant files:

- `ONI_Together/Networking/OxySync/Components/Entities/NavigatorSyncer.cs`
- `ONI_Together/Patches/NavigationPatches/NavigatorPatch.cs`

The host sends the actual navigation transition selected by ONI. The client:

1. receives the host transition,
2. snaps to the host transition anchor,
3. applies the host nav type,
4. calls `Navigator.BeginTransition(...)`,
5. allows vanilla transition-layer behavior to operate around that transition.

Client-side autonomous `Navigator.GoTo` / path progression is blocked except for required cleanup behavior.

This is important because ONI already uses `InterruptOverrideLayer`-based transition layers to temporarily interrupt navigation for interactions.

Examples include Reactable-related transitions and door/gate-style interactions.

Therefore the Reactable system should **not** invent a second position/barrier/teleport system when vanilla navigation-transition logic already places and interrupts the duplicant at the appropriate interaction point.

---

## 5. Important Separation: Navigation Authorization vs Reaction Authorization

Host-authoritative navigation answers questions such as:

- Which path is taken?
- Which transition is used?
- May the duplicant traverse this door/gate?
- Where does the transition begin?

The Reactable layer answers:

- Should this particular reaction execute?
- Which locally discovered Reactable is permitted to start?

These systems cooperate but should not duplicate each other's responsibilities.

Conceptually:

```text
HOST
  |
  +--> authorize navigation transition
  |        |
  |        v
  |    send transition
  |
  +--> vanilla discovers/validates reaction
           |
           v
      CanBegin == true
           |
           v
      authorize reaction


CLIENT
  |
  +--> replay host navigation transition
           |
           v
      vanilla transition layers examine local surroundings
           |
           v
      local valid Reactable is discovered
           |
           v
      host reaction authorization gate
           |
           v
      normal ReactionMonitor lifecycle
```

---

## 6. Current Preferred Reactable Design

### 6.1 Host

The host continues to use vanilla logic.

The host does **not** force `CanBegin`.

The host path remains conceptually:

```text
candidate discovered
      |
      v
TryReact(...)
      |
      v
CanBegin(...)
      |
      | true
      v
reaction authorized
      |
      v
ReactionMonitor enters reacting
```

Once the reaction is authorized, the host sends a short-lived reaction authorization to clients.

The network message represents:

> "The host authorized this reaction for this reactor/context."

It should not be interpreted as:

> "Execute this exact host runtime Reactable object."

---

### 6.2 Client

The client should prefer its **own locally created Reactable**.

The important idea is that host and client runtime Reactable instances do not need identical object identity.

For example:

```text
Host:   EquipSuitReactable instance A
Client: EquipSuitReactable instance B
```

This is acceptable when both represent the same host-authorized gameplay interaction.

The local Reactable has an important advantage: it was created by the client's own vanilla owner/state-machine lifecycle and therefore carries locally valid references, handles, and state.

---

### 6.3 Client CanBegin as the Authorization Gate

The current preferred design is to make client-side `CanBegin` respect host authorization.

This is **not** a global:

```csharp
if (MultiplayerSession.IsClient)
    return true;
```

That would incorrectly allow autonomous client reactions.

Instead, conceptually:

```text
Client discovers local candidate Reactable
        |
        v
CanBegin(...)
        |
        +--> matching one-shot host authorization exists?
                  |
              no  |  yes
              |   |
              v   v
            false validate locally safe execution
                        |
                        v
                 allow reaction
```

A more concrete conceptual structure is:

```csharp
if (MultiplayerSession.IsClient)
{
    if (!ReactionAuthority.TryConsumeAuthorization(
            reactable,
            reactor,
            context))
        return false;

    // Host already made the gameplay-authority decision.
    // Preserve only locally meaningful/runtime-safety validation.
    return InternalCanBegin(reactor, transition);
}

// Host / singleplayer:
// vanilla CanBegin behavior
```

The exact implementation still requires testing and may differ depending on the real `CanBegin` structure.

The important rule is:

> Host authorization replaces client gameplay authority; it should not remove necessary runtime-safety checks.

---

## 7. Authorization Lifetime

Reaction authorization must be short-lived and one-shot.

It must not remain active indefinitely.

Desired properties:

- tied to the correct reactor,
- tied to the reaction/context strongly enough to avoid authorizing a different reaction,
- consumed when used,
- expires if never used,
- cannot authorize repeated future reactions accidentally.

The authorization mechanism is a rendezvous between:

1. the host's authoritative decision, and
2. the client's locally discovered, currently valid Reactable.

This is different from creating another permanent Reactable cache key.

---

## 8. Packet Ordering Cases

The design must tolerate normal network timing.

### Host authorization arrives first

```text
authorization received
        |
        v
store short-lived authorization
        |
        v
client reaches transition interaction point
        |
        v
local Reactable is discovered
        |
        v
CanBegin consumes authorization
        |
        v
reaction runs
```

### Client reaches interaction first

```text
client transition reaches interaction
        |
        v
local Reactable is discovered
        |
        v
authorization not received yet
        |
        v
reaction cannot start yet
        |
        v
authorization arrives
```

This second case still requires testing against vanilla polling/retry behavior.

If vanilla naturally retries the surrounding Reactable check, no extra mechanism may be needed.

If it does not retry, the authorization receiver should trigger the smallest appropriate **vanilla retry/poll path** rather than directly forcing `ReactionMonitor.GoTo(...)`.

---

## 9. Why the Client Cache Still Exists

Current `ReactableSyncer` registers Reactables created by vanilla initialization.

The cache is useful because it records locally available Reactables.

However, previous investigation spent substantial time attempting to make host/client runtime instances map perfectly through cache keys.

That is no longer the primary design direction.

Known conclusions:

- type alone can be ambiguous,
- logical Reactable IDs can be shared by multiple runtime instances,
- local creation generations are not guaranteed to match between host and client,
- host/client lifecycle timing can diverge,
- some Reactables are transient or state-machine-owned.

Therefore:

> Do not redesign the cache key again unless new evidence specifically demonstrates a cache-key ambiguity that must be solved.

For transition/environment interactions, the preferred source of the execution object is the Reactable naturally discovered by the client's local vanilla transition layer.

---

## 10. Approaches Currently Rejected / Deferred

### 10.1 Globally forcing CanBegin to true

Rejected.

It would allow client-autonomous reactions and discard useful validation.

---

### 10.2 Client independently running full vanilla gameplay authorization

Rejected.

The client chore/AI system is intentionally not authoritative and may not have the same `CurrentChore` or decision state as the host.

---

### 10.3 Directly forcing every reaction with Set + GoTo from the RPC

Current code does approximately:

```csharp
smi.sm.reactable.Set(reactableInstance, smi, false);
smi.GoTo(smi.sm.reacting);
```

This has been useful for investigation and proves that the `ReactionMonitor` lifecycle can be driven.

However, it bypasses surrounding vanilla selection/transition behavior and is **not the preferred final path** for transition-driven reactions.

---

### 10.4 Manually constructing every missing Reactable

Deferred / generally avoided.

Many Reactables are owned by other state machines or transient lifecycle contexts. Blind reconstruction can produce invalid state, stale handles, duplicate ownership, or cleanup problems.

---

### 10.5 Synchronizing every Reactable-producing state machine

Rejected as the general solution.

This would couple Reactable synchronization to a very large portion of ONI's internal simulation merely to make matching Reactable instances appear.

---

### 10.6 Adding a second navigation-completion barrier system for Reactables

Not currently planned.

The navigator and vanilla `InterruptOverrideLayer` transition behavior already handle spatial movement and interaction interruption. Reactable synchronization should rely on that behavior unless testing demonstrates a concrete missing case.

---

## 11. Current Repository Implementation vs Target Design

### Current implementation

`ReactablePatch.cs`:

- registers Reactables through `Reactable.Initialize`,
- patches `Reactable.Begin`,
- host sends `RequestSyncReactable(...)`.

`ReactableSyncer.cs`:

- stores local Reactables,
- receives `RpcBeginReactable`,
- finds a local cached Reactable by type hash,
- directly executes:

```csharp
smi.sm.reactable.Set(reactableInstance, smi, false);
smi.GoTo(smi.sm.reacting);
```

### Target direction

Change the host RPC semantics from:

> "Begin this cached Reactable immediately."

toward:

> "The host authorizes this reaction/context."

Then allow the client's vanilla transition/reaction discovery to supply the locally valid Reactable and let client-side `CanBegin` act as the host-authorization gate.

---

## 12. Known Code Issue to Fix Separately

At the time of this document, `ReactablePatch.cs` enters animation synchronization scopes in the `Reactable.Begin` Prefix:

```csharp
animSyncer.EnterSyncedPlaybackScope();
animSyncer.EnterOverrideScope();
```

The Postfix currently calls the same `Enter...` methods again.

Given that `AnimSyncer` uses depth counters and exposes corresponding:

```csharp
ExitSyncedPlaybackScope();
ExitOverrideScope();
```

this appears to be a scope-depth leak and should be reviewed/fixed separately.

Do not confuse this issue with the Reactable authority architecture.

---

## 13. Current Investigation Status

### Confirmed / high confidence

- `ReactionMonitor` owns the Reactable lifecycle.
- Direct `sm.reactable.Set(...)` + `GoTo(sm.reacting)` can cause the reaction lifecycle to run.
- Client AI/chore state can differ from host state and should not be used as gameplay authority.
- Navigation on the current branch is host-authoritative.
- Clients replay host-selected `Navigator` transitions.
- Vanilla transition layers / `InterruptOverrideLayer` already participate in stopping and resuming navigation around interactions.
- Cache-key work alone cannot solve all host/client lifecycle divergence.
- Manually reconstructing arbitrary Reactables is unsafe as a general solution.

### Provisional / requires testing

- Using host authorization as the client `CanBegin` authority gate.
- Which exact identifying/context fields are required for a safe one-shot authorization.
- Whether `InternalCanBegin` is always the correct local validation to retain for every Reactable subclass/family.
- Whether vanilla automatically retries a candidate when the client reaches the interaction before the host authorization packet arrives.
- Whether some Reactable families should follow a different synchronization strategy from transition/environment Reactables.

---

## 14. Next Reactable Work

When development returns to Reactable synchronization, **do not restart the old cache-key or navigation investigations**.

Resume here:

1. Design a small `ReactionAuthority` / authorization store.
2. Change host Reactable synchronization from "remote Begin" to "remote authorization".
3. Patch/integrate client `CanBegin` so:
   - unauthorized client reaction -> false,
   - matching host authorization -> consume authorization,
   - preserve required local/runtime validity checks.
4. Test packet-order cases:
   - authorization before local discovery,
   - local discovery before authorization.
5. Start with a transition/environment interaction that is easy to observe.
6. Re-test oxygen-mask equip/unequip because it previously exposed client chore/authorization divergence.
7. Only after this path is understood, revisit Reactable families that do not naturally come from transition-layer discovery.

---

## 15. Architectural Principle to Preserve

The guiding rule for this work is:

> **Synchronize authority and stable gameplay intent; let the client use valid local vanilla objects and state-machine lifecycles to reproduce that authoritative result.**

For this subsystem:

```text
Host decides
    |
    v
Network communicates authorization
    |
    v
Client vanilla context supplies valid local execution object
    |
    v
Vanilla ReactionMonitor / transition lifecycle executes it
```

This design is provisional. Update this file whenever testing produces evidence that changes the architecture.
