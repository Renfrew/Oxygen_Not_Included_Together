**# Reactable Synchronization Architecture & Status**



**\*\*Repository:\*\*** Renfrew/Oxygen_Not_Included_Together  

**\*\*Original branch:\*\*** \`ren/test-0.8.0\`  

**\*\*Current implementation reviewed:\*\*** \`experiment\` at \`0bec442e13893b4da113b363a379f9fe2b74be3a\`  

**\*\*Status:\*\*** Provisional implemented design — broader multiplayer verification required  

**\*\*Originally recorded:\*\*** 2026-09-23  

**\*\*Reconciled with \`experiment\`:\*\*** 2026-10-03



**## 1. Purpose and architecture boundary**



This document is the detailed Reactable synchronization architecture and implementation-design record. It preserves the design first recorded in commit `2bb0b6b938a7e1b54ae0b7426b1a4879b6af7b07` and the host-authorization/client-local implementation introduced by commit `867dcbd0cd1469acb5f9574a3b123f099d0d1904`.



The design remains provisional. Update this document when source inspection or multiplayer testing invalidates an architectural assumption; do not silently substitute a different Reactable architecture.



Keep these categories separate:



- **Architecture:** enduring authority, identity, lifecycle, ordering, replay, and persistent-state boundaries.

- **Integrated design:** the host-authorization/client-local Reactable flow currently used by the mod.

- **Hardening / migration:** expiry, richer authorization context, retry/pending behavior, snapshots, and semantic replay adapters that may be added incrementally.



Temporary release blockers, exact runtime counts, staged-build evidence, and final-release verification belong in `STATUS.md` and `DEVELOPMENT_ROADMAP.md`, not in this document.



**## 2. Core authority rule**



The host is authoritative for gameplay decisions:



\> The host decides whether a reaction is authorized. The client does not independently grant the same gameplay reaction.



The client should nevertheless use locally valid vanilla objects and state-machine lifecycle wherever possible:



\> Synchronize authority and stable gameplay intent; let the client use its local Reactable and vanilla lifecycle to reproduce the authorized result.



Navigation and reaction authority are related but distinct:



\- Navigation authority selects the path and transition and places the duplicant at the interaction.

\- Reactable authority determines whether a locally discovered reaction may begin.



Reactable synchronization must not introduce a second position, teleport, or navigation-completion barrier unless testing proves the existing Navigator/transition-layer lifecycle is insufficient.



**## 3. Relevant vanilla lifecycle**



A \`Reactable\` is part of a larger vanilla lifecycle rather than an isolated animation call:



\`\`\`text

transition/environment system discovers candidate

        |

        v

TryReact(...)

        |

        v

Reactable.CanBegin(...)

        |

        | true

        v

ReactionMonitor stores local Reactable

        |

        v

ReactionMonitor enters reacting

        |

        +--> Reactable.Begin(...)

        +--> Reactable.Update(...)

        +--> Reactable.End(...)

\`\`\`



\`ReactionMonitor\` owns the reaction lifecycle. The host's successful \`CanBegin\` is the current observation point used to publish authorization.



Some Reactables mutate persistent simulation state in \`Begin\`, \`Update\`, or \`End\`. Presentation replay does not by itself guarantee equipment, storage, or other state convergence; those durable effects require their own authoritative state synchronization.



**## 4. Navigation relationship**



Current navigation is host-authoritative through:



\- \`ONI_Together/Networking/OxySync/Components/Entities/NavigatorSyncer.cs\`

\- \`ONI_Together/Patches/NavigationPatches/NavigatorPatch.cs\`



The host publishes the transition selected by ONI. The client snaps to the host transition anchor, applies the host navigation type, and invokes the local transition lifecycle. Vanilla \`InterruptOverrideLayer\` behavior can then discover locally valid interaction objects.



Successful arrival publication is ordered before synchronous \`StartWork\`, and a nested cleanup stop from the same arrival is suppressed. This ordering supports Reactable and work interactions but does not itself authorize a reaction.



**## 5. Implemented authorization design**



**### 5.1 Host behavior**



The host continues to use vanilla discovery and eligibility. It does not force \`CanBegin\` to succeed.



When host \`Reactable.CanBegin\` returns true during an active multiplayer session, \`ReactablePatch\` calls \`ReactableSyncer.RequestSyncAuthorization\`. The syncer validates:



\- its own network identity is nonzero;

\- the Reactable exists;

\- the reactor exists;

\- the reactor has a nonzero \`NetworkIdentity\`.



It then sends a reliable-immediate authorization containing:



\- the host unscaled time used for diagnostics;

\- \`reactable.id.hash\`;

\- reactor NetId.



The message means:



\> The host authorized this reaction id for this reactor.



It does not mean:



\> Execute the host's exact runtime Reactable instance.



**### 5.2 Client behavior**



Each locally initialized Reactable adds or obtains a \`ReactableSyncer\` on its owning object and registers itself by \`reactable.id.hash\`.



The client stores received authorization as a \`(reactableId, reactorNetId)\` pair. When its local vanilla flow calls \`CanBegin\`, the patch requires both:



1\. \`InternalCanBegin(reactor, transition)\` succeeds as local runtime-safety validation; and

2\. \`ReactableSyncer.IsAuthorized\` finds and removes the matching pair.



The authorization is therefore one-shot when consumed. The client uses its own locally created Reactable and the normal local \`ReactionMonitor\` lifecycle.



Conceptually:



\`\`\`text

HOST                                      CLIENT

vanilla CanBegin succeeds                 local candidate discovered

        |                                         |

        v                                         v

publish (reaction id, reactor NetId)       InternalCanBegin safety check

        |                                         |

        +-------------------------------> matching authorization exists?

                                                  |

                                             yes / consume

                                                  |

                                                  v

                                       normal local reaction lifecycle

\`\`\`



**### 5.3 Animation replay scopes**



\`Reactable.Begin\` and \`Reactable.End\` enter the reactor's \`AnimSyncer\` synchronized-playback and override scopes. Harmony finalizers exit both scopes, including exceptional paths. This prevents synchronous local reaction playback from being echoed as a new authoritative animation request.



The older document's suspected double-\`Enter\` scope leak no longer matches current source: current finalizers call the corresponding \`Exit\` methods.



**## 6. Identity and authorization lifecycle**



The current match key is \`(reactable.id.hash, reactor NetId)\`.



This deliberately avoids requiring host and client Reactable object identity to match. Host and client may hold different runtime instances representing the same interaction, and local instances retain valid local state-machine references.



Known properties:



\- Authorization is deduplicated while stored because the implementation uses a \`HashSet\`.

\- Authorization is consumed by the first matching client \`CanBegin\`.

\- A mismatched reaction id or reactor NetId cannot consume it.

\- Reactables are cached locally by logical id; registration warns if the same id is reused by a different type.

\- The owning syncer's cache is cleared on cleanup.



Current limitations:



\- Stored authorizations require an explicit lifetime/expiry policy.

\- The pair does not include transition, target, world, sequence, generation, or session epoch.

\- Without expiry or stronger context, an unused authorization could outlive the interaction that created it and match a later interaction using the same pair.

\- Authorization that arrives after a non-retried local eligibility check needs a bounded retry/pending rule if vanilla discovery does not naturally retry.

\- Join/reconnect behavior needs an explicit rule: transient authorization should not be treated as durable gameplay state, while any persistent reaction outcome must converge through authoritative state.

\- The code does not record a durable authorization sequence or explicit duplicate telemetry.



These are gaps to verify and address incrementally. They do not by themselves justify abandoning the current client-local design.



**## 7. Packet ordering cases**



**### Authorization arrives before local discovery**



The client stores the pair. Later local discovery calls \`CanBegin\`, validates \`InternalCanBegin\`, consumes the pair, and runs the local lifecycle. This is the directly supported case.



**### Local discovery happens before authorization**



The client returns false because no matching authorization exists. The later RPC stores the authorization, but the syncer does not currently trigger a retry.



This case remains unresolved:



\- If vanilla naturally polls or rediscovers the candidate, the stored authorization can be consumed later.

\- If vanilla does not retry, the receiver may need to trigger the smallest appropriate vanilla retry/poll path.

\- Directly forcing \`ReactionMonitor.GoTo(...)\` from the RPC remains rejected for transition-driven reactions unless new evidence requires a narrowly scoped exception.



**### Duplicate or stale authorization**



Exact duplicate pairs coalesce in the \`HashSet\`, but there is no sequence, age, or session boundary. Expiry and stronger context matching remain proposed hardening work.



**## 8. Integrated implementation and verification requirements**



The integrated implementation follows the architecture described above:



- Reactables register through `Reactable.Initialize`.

- Host success from vanilla `CanBegin` publishes authorization.

- Client `CanBegin` requires `InternalCanBegin` plus matching host authorization.

- Authorization delivery is reliable-immediate.

- `Begin`/`End` animation scopes are exited by finalizers.

- Direct RPC-driven `sm.reactable.Set(...)` plus `GoTo(sm.reacting)` is no longer the normal design.



The following behavior remains part of the architecture's verification/hardening surface:



- coverage across transition, environmental, suit/equipment, self-emote, and social Reactable families;

- correct behavior when local discovery occurs before authorization;

- bounded lifetime and expiry for unused authorization;

- safe handling when the owning object or reactor identity is not network-ready yet;

- join, hard-sync, reconnect, and entity-generation behavior;

- persistent equipment/state convergence whether presentation succeeds or fails;

- duplicate, delayed, and stale authorization handling.



These requirements define the subsystem's correctness envelope. Individual test results and active release defects are maintained in `STATUS.md`.

**## 9. Approaches rejected or deferred**



**### Globally forcing client \`CanBegin\` true**



Rejected. It grants autonomous client reactions and removes useful runtime-safety validation.



**### Client independently repeating gameplay eligibility**



Rejected as authority. Client chore/AI state can differ and is intentionally non-authoritative. \`InternalCanBegin\` is retained only as a local safety check after host authorization.



**### Directly forcing every reaction from the RPC**



Rejected as the general transition-driven path. It bypasses surrounding vanilla discovery and transition lifecycle. The earlier direct \`Set\` + \`GoTo\` implementation was investigative evidence, not the current design.



**### Reconstructing arbitrary missing Reactables**



Deferred and generally avoided. State-machine-owned or transient Reactables may contain local handles and ownership that cannot be safely synthesized.



**### Redesigning cache keys as the primary solution**



Rejected without new evidence. Type-only and logical-id mappings can be ambiguous, and creation order can diverge. The preferred execution object is the client's locally discovered Reactable.



**### Synchronizing every Reactable-producing state machine**



Rejected as the general solution because it would couple presentation authorization to a broad portion of ONI simulation.



**### Replacing the current design wholesale with semantic events/adapters**



Not approved. Semantic event IDs, expiry, deduplication, snapshots, and family-specific adapters remain proposed extensions in \`docs/ONI_Multiplayer_Animation_Architecture.md\`. They may be introduced incrementally when evidence shows a need.



**## 10. Incremental hardening and migration order**



1. Define the valid network-identity readiness boundary for the owning `ReactableSyncer` and reactor.

2. Verify both authorization-before-discovery and discovery-before-authorization lifecycles.

3. Add bounded authorization expiry and diagnostics without changing the client-local execution rule.

4. Strengthen the authorization key with transition, target, generation, session, or sequence context only where evidence shows the current pair is ambiguous.

5. Verify that `InternalCanBegin` is the correct retained local safety check for each supported Reactable family.

6. Keep persistent simulation outcomes, such as equipment state, separate from transient reaction presentation.

7. Cover transition/environment reactions, self-emotes, social reactions, equipment reactions, and longer-running reactions incrementally.

8. Define join, hard-sync, reconnect, duplicate-delivery, and late-authorization behavior.

9. Introduce broader semantic events/adapters only where they improve a demonstrated lifecycle or recovery gap; do not replace the current authorization model wholesale without an explicit architecture decision.



**## 11. Revision history**



\- **\*\*2026-09-23 — \`2bb0b6b938a7e1b54ae0b7426b1a4879b6af7b07\`:\*\*** recorded the Reactable investigation, rejected direct/global client authority, and selected host authorization with client-local Reactables as the target direction.

\- **\*\*2026-09-26 — \`867dcbd0cd1469acb5f9574a3b123f099d0d1904\`:\*\*** implemented host authorization and client-local \`CanBegin\` gating in \`ReactableSyncer\`/\`ReactablePatch\`.

\- **\*\*2026-10-03 — documentation reconciliation:\*\*** reconciled the document with the implemented host-authorization/client-local design, removed stale direct-RPC behavior as the current model, and preserved semantic events/adapters as incremental future evolution; no architecture replacement was approved.



**## 12. Principle to preserve**



\> **\*\*The host decides whether the gameplay reaction is authorized. The client consumes that authorization using a valid local Reactable and vanilla lifecycle. Persistent state converges through its own authoritative synchronization, and broader event/adapter machinery remains an explicit future decision.\*\***
