# ANIMATION SYNC - FULL CHANGE SPECIFICATION

## 1. OVERVIEW

```text
BEFORE:
  Host -> PlayAnimPacket -> Client
  If the packet drops, the client can stay on the wrong animation forever.

AFTER:
  Minions:
    Host -> PlayAnimPacket -> Client        (event-driven direct send)
    Host -> DuplicantStatePacket -> Client  on 200ms state checks
    Host -> DuplicantStatePacket -> Client  forced heartbeat every 1000ms

  Critters:
    Host -> PlayAnimPacket -> Client        (event-driven once they have NetId)
    Host -> AnimSyncCoordinator shard scan   every 200ms
    Host -> AnimSyncPacket -> Client        visible 5s / active 10s / request-based

  Animated buildings:
    Host -> AnimSyncCoordinator shard scan   every 200ms
    Host -> AnimSyncPacket -> Client        visible 5s / active 10s / request-based
```

This change stays inside the author's current state-sync model. It adds host-driven animation reconciliation on top of existing event-driven packets. It does not touch the transport layer, does not change `BuildingSyncer`, and does not revive `DuplicantClientController` or `NavigatorTransitionPacket`.

## 2. SCOPE MATRIX

| Entity type | Event-driven packet | Periodic packet | Interval |
|-------------|---------------------|-----------------|----------|
| Minions | `PlayAnimPacket` | `DuplicantStatePacket` | 200ms checks, 1000ms heartbeat |
| Critters | `PlayAnimPacket` | `AnimSyncPacket` | coordinator scan every 200ms, visible 5s, active 10s, on-demand |
| Animated buildings | none | `AnimSyncPacket` | coordinator scan every 200ms, visible 5s, active 10s, on-demand |

Only entities with both `NetworkIdentity` and `KBatchedAnimController` participate. Buildings are included only when they are animated, network-identifiable, and known to switch between active/inactive-style animations.

## 3. PACKET CHANGES

### `DuplicantStatePacket` (minions, every 200ms)

`DuplicantStatePacket` keeps its existing high-level duplicant state fields and now also carries:

- `AnimPlayMode` (`int`)
- `AnimSpeed` (`float`)

Minion reconciliation uses:

- `CurrentAnimName`
- `AnimElapsedTime`
- `AnimPlayMode`
- `AnimSpeed`

This branch keeps the current packet-shape change as-is. Host and clients must run the same mod version. If the protocol-gating branch lands first, animation packets inherit that verification automatically.

### `AnimSyncPacket` (critters and animated buildings, correction snapshot)

`AnimSyncPacket` is a compact periodic reconciliation packet sent directly as `Unreliable`:

- `NetId` (`int`)
- `AnimHash` (`int`)
- `Mode` (`byte`)
- `Speed` (`float`)
- `ElapsedTime` (`float`)

The packet is emitted by a host-side `AnimSyncCoordinator`. `AnimStateSyncer` remains attached to eligible non-minion entities, but only as a lightweight snapshot provider and registry hook.

## 4. RECONCILIATION RULES

Both packet paths use the same reconciliation policy:

1. If the client's current animation differs from the authoritative animation, replay the authoritative animation with the packet's mode and speed, then snap elapsed time.
2. If the animation matches but elapsed-time drift exceeds 150ms, snap elapsed time.
3. If the animation matches and drift is within 150ms, do nothing.

`Mode` and `Speed` are authoritative replay parameters in this increment. They are not treated as standalone mismatch triggers when the current animation already matches.

## 5. ARCHITECTURE FIT

- This is state reconciliation, not lockstep.
- The host remains authoritative.
- The transport layer stays untouched.
- `BuildingSyncer` keeps its existing 30s full-state building reconciliation behavior.
- `PlayAnimPacket` stays event-driven but bypasses the broken bulk path so it preserves the requested send semantics.
- `AnimSyncCoordinator` replaces per-entity heartbeats with a shared 200ms shard scheduler.
- `AnimSyncPacket` is only sent when an entity is visible to at least one client, was recently active, or was explicitly requested by a client resync packet.
- `AnimResyncRequestPacket` is an exception path for join-in-progress and missing initial snapshots, not a standing polling loop.
- Steam hosts back off non-minion correction cadence when pending unreliable bytes or queue time rise above the configured thresholds.
- `DuplicantClientController` remains commented out.
- `NavigatorTransitionPacket` remains commented out.
- The client still runs local animation code; periodic reconciliation is the safety net instead of a hard client-side animation gate.

## 6. FILES IN THIS CHANGE

- `DuplicantStatePacket.cs`
  - Uses shared reconciliation logic for minions.
  - Keeps `AnimPlayMode` and `AnimSpeed`.
- `DuplicantStateSender.cs`
  - Continues 200ms state checks and keeps a 1000ms forced heartbeat.
- `AnimReconciliationHelper.cs`
  - Resolves elapsed-time setters once.
  - Applies shared replay/drift correction logic.
- `AnimSyncPacket.cs`
  - New non-minion animation correction snapshot sent directly as `Unreliable`.
- `AnimStateSyncer.cs`
  - Lightweight registry and snapshot provider for critters and selected animated buildings.
- `AnimSyncCoordinator.cs`
  - Host-only shard scheduler for non-minion animation correction.
  - Uses visibility, recent-activity, request-based resync, and Steam queue backoff.
- `AnimResyncRequester.cs`
  - Client-side join and retry requester for visible animated entities that still need their first authoritative snapshot.
- `AnimResyncRequestPacket.cs`
  - Lightweight client->host request for targeted non-minion animation correction.
- `EntityTemplatesPatch.cs`
  - Gives animated critter prefabs `NetworkIdentity` and `AnimStateSyncer` without template-time registration.
- `BuildingSpawnPatch.cs`
  - Registers identities for selected animated building instances.
- `BuildingComplete_Patches.cs`
  - Attaches `AnimStateSyncer` to eligible building prefabs so instance lifecycle stays correct.
- `AnimSyncTests.cs`
  - Covers reconciliation helper behavior, packet roundtrip, direct-send packet shape, and non-minion sync eligibility.

## 7. KNOWN LIMITATIONS

- Transition-driver position offsets remain out of scope.
- This increment does not restore client-side transition playback controllers.
- If reflection-based elapsed-time restore fails, the client falls back to current event-driven behavior.
- Buildings use periodic reconciliation only in this increment. Immediate event-driven building animation sync is not added here.
- Same-animation `Mode`/`Speed` mismatch is still corrected on replay, not treated as an independent mismatch trigger.

## 8. ACCEPTANCE

- Minion wrong-animation desync self-heals on the next state-changing packet or within one 1000ms heartbeat.
- Critter wrong-animation desync self-heals immediately on `PlayAnimPacket`, within 5s while visible, or faster when the client requests an initial snapshot after joining.
- Animated-building wrong-animation desync self-heals within 5s while visible, within 10s when recently active off-screen, or on the next targeted resync request.
- Drift-only desync snaps elapsed time without replaying a different animation.
- Entities without `NetworkIdentity` or `KBatchedAnimController` fail closed with no crash.
- Join-in-progress clients converge via targeted visible-entity resync without re-enabling transition playback code.
