The audit found extensive always-installed behavior, but no global ONI Together change with a concrete path to making `GeoTuner.StartSM()` run early or twice. The strongest architectural finding is different: ONI Together installs hundreds of patches and injects many network components even in single-player, while the transferred-load interval is misclassified as “offline” by `IsClient`.

That creates a broad lifecycle blind spot, but the current source still lacks a specific bridge from persistent connection state to the malformed `GeyserModification`.

## 1. Ordered mod startup

The entry point is [MultiplayerMod.cs](C:/Users/Renfrew/source/ONI_Together/ONI_Together/MultiplayerMod.cs:26).

The verified sequence is:

```text
ONI loads ONI Together assembly
→ MultiplayerMod.OnLoad(harmony)
→ stores Harmony globally
→ initializes PLib
→ registers mod options
→ calls base.OnLoad(harmony)
    → UserMod2.OnLoad calls harmony.PatchAll(mod assembly)
→ loads asset bundles
→ initializes DebugConsole and PacketTracker
→ initializes DebugMenu
→ initializes SteamLobby callbacks/state
→ creates persistent Multiplayer_Modules GameObject
→ marks it DontDestroyOnLoad
→ attaches persistent managers/components
→ registers App.OnPostLoadScene and ReadyManager listeners
→ registers development tools in debug builds
→ selects configured network transport
→ registers version checker
→ subscribes global Unity/AppDomain error handlers
→ OnAllModsLoaded
→ registers packet types
→ initializes OxySync API bridge
→ forces transport selection to Steamworks
→ records main thread and synchronization context
→ database/prefab construction begins
→ patched Db.Initialize and prefab factory methods run
→ main menu
```

Vanilla confirms that `UserMod2.OnLoad` invokes `Harmony.PatchAll(this.assembly)` at [UserMod2.cs](C:/Users/Renfrew/source/repos/ONI%20source/Assembly-CSharp/KMod/UserMod2.cs:27). Therefore all Harmony patches are installed before ONI Together creates its persistent managers.

### Persistent startup components

`Multiplayer_Modules` receives these components regardless of session state:

- `NetworkingComponent`
- `UIVisibilityController`
- `MainThreadExecutor`
- `CursorManager`
- `PingManager`
- `WorldStateSyncer`
- `PlantLifecycleSyncer`
- `ConduitFlowSyncer`
- `BulkPacketMonitor`
- `LogicStateSyncer`
- `OxySyncManager`
- `NetIdActivityTracker`
- `IdCensus`
- `DiscordRichPresence`

The object survives scene changes because it is `DontDestroyOnLoad`.

Most update methods are session-guarded. Their instances, event subscriptions, registries, delegates, queues, and singleton references are nevertheless present before any multiplayer session.

## 2. Harmony inventory

The current source contains:

- 362 non-commented `HarmonyPatch` attributes
- across 161 source files
- two effective transpilers
- no reverse patches found

The 362 figure includes class-level patches and nested method-target attributes, so it is not equivalent to 362 independent method bodies.

### A. Always-active behavior

These patches change objects or behavior without checking session state.

| Area | Target/change | Effect |
|---|---|---|
| Prefab initialization | `BuildingComplete.OnPrefabInit` | Adds `NetworkIdentity` to every complete building and `AnimSyncer` to eligible buildings |
| Operational | `Operational.OnPrefabInit` | Adds `ClientReceiver_Operational` |
| Creatures | `EntityTemplates.ExtendEntityToBasicCreature` | Adds identity, position handler, animation syncer, multiplayer initializer |
| Duplicants | `BaseMinionConfig.BaseMinion` | Declares/adds identity, animation, position, and vital-stat syncers |
| Duplicant spawn | `BaseMinionConfig.BaseOnSpawn` | Adds `MinionMultiplayerInitializer` |
| Game clock | `GameClock.OnPrefabInit` | Adds `GameTimeSyncer` and writes static clock tracking fields |
| Speed control | `SpeedControlScreen.OnPrefabInit` | Adds `GameSpeedSyncer` |
| Buildings | numerous component-specific `OnSpawn` postfixes | Add storage, battery, generator, toilet, reactor, plant, printing-pod, or state-machine syncers |
| Edibles | `Edible.OnPrefabInit` | Adds rot and consumption syncers |
| Game | `Game.OnSpawn` | Adds `LogicPortManager` and registers global sync IDs |
| Movement | `Movable.OnSpawn` | Permanently subscribes an `onPickupComplete` callback |
| Trackers | `KPrefabID.OnSpawn/OnCleanUp` | Maintains static mop/disinfect collections |
| Logic ports | `LogicPorts.OnSpawn/OnCleanUp` | Maintains a static global logic-port collection |
| Air conditioner | `AirConditioner.UpdateState` prefix | Suppresses vanilla when its structure-temperature handle is invalid |
| Load screen | `LoadScreen.DoLoad` | Always logs; only changes server state if hosting |
| Main menu/UI | various | Adds multiplayer UI, overlay modes, pause-menu entries, chat/input interception |
| Development | `KImGuiUtil.SetKAssertCB` transpiler | Replaces the entire vanilla method with `ret` |
| Modal screens | `KModalScreen.OnShow` transpiler | Replaces pause/unpause calls with session-aware wrappers |
| Current temporary code | `GeoTuner.ApplyTuning` prefix | Unconditionally inspects and potentially replaces GeoTuner runtime fields |

The first several categories mean “ONI Together installed but offline” is not equivalent to vanilla.

### B. Session-guarded patches

The majority of behavioral patches are guarded by one or more of:

```csharp
MultiplayerSession.InActiveSession
MultiplayerSession.IsClient
MultiplayerSession.IsHost
MultiplayerSession.IsHostInSession
packet.IsApplying
ProcessingIncoming
```

Functional groups include:

- duplicant thoughts, dreams, chat, greetings, effects, flatulence;
- bionic oil-monitor methods;
- navigator movement;
- chores and workable start/stop/complete;
- construction, digging, mopping, disinfecting, moving and sandbox tools;
- building configuration and side-screen controls;
- schedule creation/deletion/assignment;
- storage changes;
- operational state sending and client-side getters;
- battery/generator simulation;
- resource counts;
- animation play/queue/override calls;
- symbol overrides;
- research and printing-pod actions;
- damage and object spawning;
- plant lifecycle;
- conduit flow;
- notifications and status items.

During transferred-save reconstruction, `IsClient` and `InActiveSession` are false. These prefixes generally allow vanilla to execute, producing the same decision as an offline main-menu load.

### C. Transitional-state-aware patches

Only a very small set recognizes the disconnected/reconstructing client explicitly.

The important one is [BatteryTrackerPatch.cs](C:/Users/Renfrew/source/ONI_Together/ONI_Together/Patches/World/BatteryTrackerPatch.cs:27):

```csharp
bool clientOrReconnecting =
    MultiplayerSession.IsClient ||
    (!MultiplayerSession.IsHost && GameClient.HasCachedConnection());

if (clientOrReconnecting && GameClient.State != ClientState.InGame)
    return false;
```

It suppresses a vanilla tracker update while the client world is half constructed.

The other cached-connection consumer is [GamePatch.cs](C:/Users/Renfrew/source/ONI_Together/ONI_Together/Patches/GamePatches/GamePatch.cs:48), which reconnects after `Game.OnSpawn`.

No other Harmony patch was found using `HasCachedConnection()` to distinguish reconstruction from offline loading.

Hard-sync-specific guards exist in battery, pickupable, plant, and synchronization code, but `GameClient.IsHardSyncInProgress` is false during the initial transferred-save load.

### D. Indirect/global lifecycle changes

The broadest indirect changes are:

- persistent managers created before the main menu;
- global event subscriptions;
- packet registry initialization;
- OxySync static delegate installation;
- prefab component injection;
- static identity and behavior registries;
- static tracker collections;
- global operational receivers;
- state-machine syncer injection after supported SMIs start;
- persistent packet and transport queues.

These generally do not replace vanilla methods, but they increase the number of components participating in `OnPrefabInit`, `OnSpawn`, cleanup, and serialization.

## 3. Prefixes that suppress or substitute vanilla

The significant suppression categories are:

| Patch group | Suppression condition during ordinary play | Reconstruction result |
|---|---|---|
| Navigator | active session, object has identity, not host | Original runs because `InActiveSession == false` |
| Workable/worker | active client unless authorized packet | Original runs because `IsClient == false` |
| Operational getters | active client | Original getter runs |
| EnergyConsumer getters/setters | active client | Original runs |
| Battery/generator simulation | active client | Original runs |
| Thought/dream/effect methods | active client | Original runs |
| Tool actions | active client or active session | Original runs |
| Building placement | active session | Original runs |
| Resource count | active client | Original runs |
| Colony diagnostics | active client/cache-specific data | Generally original during disconnected load |
| BatteryTracker | cached reconnecting client | Original is deliberately suppressed |
| AirConditioner | invalid native handle | Original is suppressed in every environment |
| Input manager | chat field focused | Original suppressed only for UI input |
| ImGui callback | unconditional transpiler | Vanilla target never executes |
| Modal pause | active session | Offline/reconstruction pause semantics remain vanilla |

Several apparent suppressors are disabled no-ops:

- `StateMachine.Instance.IsRunning` prefix returns `true`.
- `Pathfinding.UpdateNavGrids` prefix returns `true`.
- `Util.KInstantiate` prefix always returns `true`.
- `GameClock.AddTime` prefix returns `true`.
- `SpeedControlScreen.Pause` prefix returns `true`.

They install Harmony overhead but do not change vanilla results.

## 4. Global/static state surviving connection and disconnect

The significant state established or changed by connecting from the main menu is:

| State | Changed by connection | Cleared by `GameClient.Disconnect()` | Present during load | Read by vanilla patches |
|---|---:|---:|---:|---|
| `GameClient._cachedConnectionInfo` | Yes, immediately before load | No | Yes | BatteryTracker and later Game reconnect |
| `GameClient.State` | Yes | Transport-dependent | `LoadingWorld` or `Disconnected` | BatteryTracker |
| `MultiplayerSession.HostUserID` | Yes | No direct clear in `Disconnect` | Yes | Networking code |
| server IP/port | Yes | No | Yes | Reconnection |
| `InActiveSession` | Yes, then reset on disconnect | Reset to false | False | Hundreds of guards |
| `IsClient` | Derived from session | Becomes false | False | Hundreds of guards |
| `PacketHandler.readyToProcess` | Enabled for handshake | Explicitly set false | False | Packet dispatch only |
| transport client and callbacks | Prepared | Transport disconnects | Objects/callbacks remain | Networking loop |
| packet registry | Startup-global | Never cleared | Yes | Packet layer |
| `OxySyncManager` | Startup-global | Not destroyed | Yes | Registers network behaviours |
| OxySync behavior collections | Populated as objects spawn | Individual cleanup expected | Manager survives scene | OxySync only |
| `NetworkIdentityRegistry` | Populated during connection/world | Explicitly cleared before load | Empty initially | Injected components repopulate it |
| player cursors | Connection state | Explicitly cleared | Empty | UI only |
| packet sender queues/bulk runners | May be populated | No clear in `GameClient.Disconnect` found | Potentially | Packet sending only |
| interest-group/static sync data | May be populated | No comprehensive disconnect clear found | Potentially | OxySync only |
| mop/disinfect/logic-port trackers | Startup/world lifetime | Cleanup-dependent | May retain entries until old objects clean up | Tool/UI patches |
| `BuildingConfigPacket.IsApplyingPacket` and similar flags | Packet-scoped | Normally reset in `finally`/handler flow | Normally false | Configuration patches |
| save chunk assembler state | Transfer-specific | Completed entry removed | Normally empty | Save transfer only |

The central lifecycle defect is:

```text
A reconnecting client retains client intent in cached/static state,
but the primary IsClient predicate discards that identity during load.
```

That means most client-specific suppressions do not run during reconstruction. This is proven by the need for the special BatteryTracker predicate.

No surviving static field identified here directly changes `Geyser.modifications`, GeoTuner state, or vanilla SMI start order.

## 5. Prefab and component changes present offline

Even without hosting or joining, ONI Together changes the component graph.

### Broad injections

- Every `BuildingComplete`: `NetworkIdentity`; often `AnimSyncer`.
- Every `Operational`: `ClientReceiver_Operational`.
- Duplicants: `NetworkIdentity`, `AnimSyncer`, `OxySyncEntityPositionHandler`, `VitalStatsSyncer`, `MinionMultiplayerInitializer`.
- Basic creatures/rovers: corresponding identity, position, animation, and initializer components.
- `GameClock`: `GameTimeSyncer`.
- `SpeedControlScreen`: `GameSpeedSyncer`.
- Supported SMI owners: `NetworkIdentity` and `StateMachineStateSyncer`.
- Edibles: `RottableStateSyncer`, `EdibleConsumptionSyncer`.
- Selected buildings: state-specific OxySync components.
- Batteries/generators/storage/toilets/reactors/plants/telepads: structure syncers.
- Game: `LogicPortManager`, `ResourceSyncer`.

### Lifecycle behavior

`NetworkIdentity` is serialized opt-in and has a serialized `NetId`. It registers during `OnSpawn`, even offline. The registry is therefore rebuilt during every normal save load.

Most syncers are not dangerous merely by existing:

- Their sending logic is host/session guarded.
- Incoming hooks are packet-driven.
- Several are marked `SkipSaveFileSerialization`.
- They do not normally invoke another vanilla component’s `OnSpawn`.

However, OxySync registration itself is not session guarded. `OxySyncManager.Awake()` globally subscribes to `NetworkBehaviour.OnSpawned`, and every injected `NetworkBehaviour` registers into its collections during spawn. This happens in all three environments.

## 6. Global-system semantic changes

### State machines

- `StateMachine.Instance.IsRunning` patch is disabled and returns to vanilla.
- `StartSMIS` has only a postfix.
- The postfix supports only four named SMI types; GeoTuner is not one of them.
- No general SMI-start suppression or replay system currently exists.

### `KPrefabID`

Only mop/disinfect tracker postfixes and cleanup prefixes were found. They do not change spawn order or original execution.

### `Operational`

Every operational object receives an extra component. Operational getters/setters are patched, but client substitution requires `IsClient`, which is false during reconstruction.

### Game and save loading

- `LoadScreen.DoLoad` is only materially altered for a live host server.
- `SaveLoader.Load` has a postfix that may create a host server when `ShouldHostAfterLoad` is set.
- `Game.OnSpawn` performs reconnection and global registrations.
- None runs early enough to alter the failing GeoTuner `KPrefabID.OnSpawn`.

### Animation

Animation methods are heavily patched, and animated buildings receive `AnimSyncer`. The prefixes generally permit vanilla unless an active client is following authoritative animation state. During reconstruction they permit vanilla.

### Pause/speed

The modal-screen transpiler is globally installed, but delegates to vanilla pause/unpause while `InActiveSession` is false.

## 7. What is different with ONI Together installed but offline?

Before ever connecting, these differences already exist:

- all Harmony patches are installed;
- persistent multiplayer managers exist and update each frame;
- Steam lobby and transport infrastructure are initialized;
- global error handlers and scene listeners are subscribed;
- packet/OxySync registries exist;
- custom overlay/UI modes are registered;
- vanilla prefabs receive network identities and sync components;
- identities and OxySync behaviors register during ordinary world loading;
- extra component `OnPrefabInit`, `OnSpawn`, `OnCleanUp`, `Update`, and event subscription code runs;
- AirConditioner invalid-handle calls are suppressed;
- ImGui assert callback configuration is eliminated;
- pause/modal calls pass through a transpiled wrapper;
- KInput processing can be suppressed while chat has focus;
- the current temporary GeoTuner prefix is active even offline.

Thus Environment 1 is already substantially modified relative to unmodded ONI.

## 8. Three-environment comparison

| State before reconstruction | Offline normal load | Load then host | Transferred client load |
|---|---:|---:|---:|
| Harmony patches installed | Yes | Yes | Yes |
| persistent manager object | Yes | Yes | Yes |
| injected prefab components | Yes | Yes | Yes |
| `IsHost` | False during normal load | Usually false until hosting starts | False |
| `InActiveSession` | False | False during initial normal load | False after disconnect |
| `IsClient` | False | False | False |
| cached connection | No | No | **Yes** |
| known host ID/address | No | Not client data | **Yes** |
| packet processing | normal/default | normal/default | **Explicitly false** |
| transport previously initialized and connected | No | Server not yet active during normal load | **Yes** |
| transport callbacks installed | Possibly prepared globally | Server path | **Yes** |
| OxySync manager history | Startup/world only | Startup/world only | **Has already existed through connection** |
| identity registry before reconstruction | empty/new | empty/new | explicitly cleared |
| reconnect at `Game.OnSpawn` | No | No | **Yes** |

The first meaningful difference remains cached connection plus prior transport/packet state. None of the audited vanilla lifecycle patches besides BatteryTracker reads that distinction before `Game.OnSpawn`.

## 9. Earlier fixes showing the same architectural pattern

Two current patches explicitly document premature vanilla work during client reconstruction.

### Battery tracker

[BatteryTrackerPatch.cs](C:/Users/Renfrew/source/ONI_Together/ONI_Together/Patches/World/BatteryTrackerPatch.cs:38) documents `TrackerTool.Update` running against a half-built world. It needed `HasCachedConnection()` because both `IsClient` and sometimes `LoadingWorld` had already become false/stale.

### Air conditioner

[AirConditionerPatch.cs](C:/Users/Renfrew/source/ONI_Together/ONI_Together/Patches/World/AirConditionerPatch.cs:9) documents:

```text
EnergyConsumer.OnPrefabInit
→ Operational.SetFlag
→ UpdateOperational
→ OnOperationalChanged
→ AirConditioner.UpdateState
```

before `AirConditioner.OnSpawn` assigned its native simulation handle.

This is a verified example of ordinary vanilla callbacks becoming re-entrant during prefab initialization and observing incomplete runtime state.

These fixes establish a real category:

```text
client/transferred world construction
→ vanilla callback triggered during prefab initialization
→ callback assumes later OnSpawn/global registration has occurred
→ fatal exception
```

They do not prove the GeoTuner instance belongs to that category, but they make it more plausible than packet-driven mutation during load.

Authoritative Git history was not available from this Windows testing snapshot, per the project’s repository rules. I did not infer deleted commits or hashes from the snapshot.

## Ranked candidates for the GeoTuner symptom

1. **Missing reconstruction-aware lifecycle boundary — strongest category evidence.**

   Evidence: BatteryTracker and AirConditioner already required protection from half-built-world callbacks. Most client guards fail open during reconstruction because `IsClient == false`.

   Missing link: no ONI Together code was found invoking GeoTuner/Geyser during this interval. Vanilla `StartSMIS` invokes it.

2. **Persistent component graph and OxySync registration — moderate global risk, weak direct evidence.**

   ONI Together adds components and registration work to virtually every building. This changes spawn workload and component ordering globally.

   Against it: the same injections occur during the working normal load, and no injected component calls GeoTuner or Geyser.

3. **Stale persistent registries/queues from the pre-load connection — plausible environment difference, weak causal evidence.**

   OxySync, packet, transport, and manager state survive connection/disconnection. Only the identity registry and cursors are explicitly cleared.

   Against it: packet processing is disabled, and no surviving collection is consulted by the GeoTuner/Geyser call chain.

4. **State-machine sync patches — ruled out for GeoTuner.**

   The `StartSMIS` hook is a postfix, and GeoTuner is not in the supported state-machine set.

5. **GeoTuner assignment packet handling — ruled out before this crash.**

   Packet processing is off, the transport is disconnected, and `InActiveSession` guards the sender.

## Conclusion

The startup/global audit proves that ONI Together changes the game globally before multiplayer begins and that transferred-client reconstruction is represented incorrectly by the ordinary `IsClient` predicate.

It does not yet prove that one of those global changes creates the null `originID`. The strongest source-supported interpretation is:

```text
ONI Together has no general “client world is reconstructing” lifecycle state.
Existing client guards therefore disengage during reconstruction.
Other subsystems have already needed one-off protection from vanilla callbacks
running against half-built state.
GeoTuner may be another manifestation, but no direct mod-side trigger is proven.
```

No source or architecture files were modified.

Potential documentation impact, if later confirmed, would be a lifecycle definition in `ARCHITECTURE.md`/`DECISIONS.md` distinguishing:

```text
offline
connecting
reconstructing client
active client
host
```

rather than deriving all client behavior from `InActiveSession`.