# Client Transport Contract — Review Rules

**Status:** Developer-stated design intent for the new client transport draft; not evidence of completed implementation.  
**Scope:** `ClientTransport`, concrete client protocols (including `SteamworksClientDraft`), and their interface with Lifecycle.  
**Protection:** This document does not authorize edits to staged code or existing architecture references.

## 1. Ownership

- **Lifecycle is the operation-permission authority.** It decides when the mod may call `Connect()`, `Disconnect()`, and `Poll()`. It must protect against prohibited calls and host/client overlap.
- **The concrete protocol owns execution and native resources.** It starts and completes connections, obtains the remote identity and name, manages native callbacks, cleans up, and reports outcomes.
- **The `OnXXX` methods are the protocol-to-Lifecycle notification contract.** Do not build an independent client control plane or duplicate Lifecycle permissions inside a concrete protocol.
- Distinguish protocol connection completion from application Session admission where the source establishes separate requirements. Do not claim the native connected event alone satisfies an unverified admission handshake.

## 2. Intended client flow

```text
Idle → Connecting → Connected → Cleaning → Idle
```

This is the developer's intended *client protocol lifecycle*, not a claim that the current `Lifecycle` class already implements these phases. Failure and protocol-initiated termination must be reported through their appropriate notification methods; do not invent additional phases or an autonomous state machine.

When Lifecycle calls `Connect()`, it has already authorized the operation from an Idle/ready state. The protocol starts connection work. Once the protocol has completed its required connection process and obtained an actual `ulong` player ID and player name, it calls `OnConnected(id, name)` to inform Lifecycle. On failure it cleans up and calls `OnConnectFail(reason)`.

When Lifecycle calls `Disconnect()`, the concrete protocol performs cleanup and reports completion through `OnDisconnected()`, including repeated allowed calls. A protocol-initiated disconnect performs protocol cleanup and invokes `OnDisconnect()` to notify the mod; this hook must not recursively call `InternalDisconnect()`.

Lifecycle calls `Poll()` only in phases in which polling is valid (the developer specified Connecting or Connected). A missing required initialized Steam manager or callback registration during a permitted poll is an invariant violation: **throw**, do not silently skip the poll.

## 3. Error handling and callback discipline

- **Fail visibly on broken invariants.** Example: an unexpected previously owned Steam connection at `Connect()` may throw; do not silently treat an invalid protocol state as a normal outcome.
- **Do not silently ignore callbacks or operations to hide lifecycle inconsistencies.** In particular, a connection-handle mismatch/invalid-handle guard needs a documented, concrete reason before it is accepted; examine native callback behavior and actual lifecycle ordering rather than assuming it is harmless or impossible.
- **No speculative defensive programming.** For each check, flag, state variable, exception, `try`/`catch`/`finally`, retry, or cleanup confirmation, identify the specific contract obligation and code/API evidence that requires it. If evidence is absent, ask for approval rather than adding the mechanism.
- **Native API failures are distinct from invariant violations.** Handle supported ordinary failures through the appropriate completion notification. Do not convert unexpected integrity failures into normal success or silence.
- **A notification must match its meaning.** Do not conflate `OnDisconnect()` (protocol-initiated), `OnDisconnected()` (mod-requested cleanup), `OnConnectFail()` (connection failure), and `OnConnected(id, name)` (completed protocol connection).
- **Do not report successful cleanup without evidence.** Cleanup failure handling requires reconciling native resource ownership with the approved design; clearing a field alone does not establish native release.

## 4. Immutable interface and code-change permissions

The developer's manually staged `ONI_United/Networking/Transport/ClientTransport.cs` is the authoritative base contract until expressly changed by the developer. Current agreed shape:

```csharp
public void Connect() => InternalConnect();
public void Disconnect() => InternalDisconnect();
public void Poll() => InternalPoll();
protected abstract void InternalConnect();
protected abstract void InternalDisconnect();
protected abstract void InternalPoll();
protected void OnConnected(ulong clientId, string playerName) { }
protected void OnConnectFail(string reason) { }
protected void OnDisconnect() { }
protected void OnDisconnected() { }
```

The one-line bodies above summarize the API only; they do not authorize replacing the manually staged source. The `OnXXX` hooks are presently **nonvirtual and empty**, so calling them alone does not yet update Lifecycle: integration must be designed and approved, not claimed as implemented.

**Never edit the staged base class, other staged work, production networking, or architecture documents without explicit approval.** Leave AI changes unstaged. Do not add tests or test infrastructure without explicit approval.

## 5. Required review procedure

Before proposing or editing client transport code:

1. Read this document, the developer's staged `ClientTransport.cs`, and relevant sections of `ONI_United_Network_Lifecycle_Protocol_Design.md` and `ARCHITECTURE.md`.
2. State the actual machine, branch, HEAD, and staged/unstaged/untracked status if repository access is available. Never substitute a Windows snapshot for authoritative Mac Git evidence.
3. For every proposed guard, extra state, skip/return, exception handler or cleanup mechanism, point to the exact observed code, native API behavior, or requirement that justifies it. **Unproven safeguards are not approved by default.**
4. Review each `Connect`, `Disconnect`, `Poll`, native callback, and `OnXXX` path against the allowed caller state, owned resources, completion condition, and Lifecycle notification. Invariant violations must not silently return.
5. Separate **documented intent**, **source implementation**, **build/test results**, and **runtime verification**. A successful build does not verify the native callback lifecycle.
6. If documents conflict, **stop and identify the precise conflict**; do not silently choose a design, invent a new state, or alter code. Obtain explicit approval before changing architectural authority.
7. Do not declare a Draft conformant or stage it while a required notification remains a no-op or a required completion path is unimplemented.

## 6. Existing-document reconciliation required

The uploaded `ONI_United_Network_Lifecycle_Protocol_Design.md` states that exact *client Lifecycle* phases/events were not yet approved (§3.4), and its fatal-integrity/cleanup rules (§3.3 and §6) require care with exceptions and unfinished native cleanup. The new five-phase flow above is the developer's *client protocol* intent. Do not automatically claim it supersedes the broader Lifecycle state design or the existing host cleanup policy. Bring any conflict to the developer for a decision before integrating.
