# World-State Definition, Lifecycle, Effect, and Transaction Specification

**Status date:** 2026-07-16  
**Tracking issue:** #172  
**Specification owner:** GPT  
**Definition/lifecycle/effect implementation owner:** Codex engineering mode  
**Player-facing event meaning, names, descriptions, and localization:** Codex narrative/content mode  
**Final product/creative approval:** user  
**Audited baseline:** `83920472143094aa61fe9bee3914ead042cb44f3`  
**Validated Unity target:** `2022.3.62f3`  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`  
**Catalog authority:** `unity/Docs/Game_Data_Catalog_Authority_Spec.md`  
**Save authority:** `unity/Docs/Save_Semantic_Compatibility_Policy.md` and issue #137  
**Notification authority:** `unity/Docs/Notification_Delivery_Contract_Spec.md`

## 1. Goal

Replace the current in-memory enum/string countdown with a versioned, validated, persistent, idempotent world-state lifecycle that can prove all of the following separately:

```text
an event definition exists and is supported
→ an event start request is valid
→ all required effect consumers can prepare
→ one immutable event instance is staged
→ world-state/effects/ledger/outbox are applied to one candidate
→ the candidate is durably persisted and verified
→ the committed instance is published
→ consumers expose the exact active effect revision
→ start/end/cancel notifications are delivered through #177
→ reload/offline advancement ends or resumes the event exactly once
```

A world event must never be announced merely because a string and float duration were assigned. If the event has no validated effect consumers, cannot persist, or cannot be applied coherently, the start request fails visibly and no player-facing start announcement is published.

The first implementation phase is deliberately pure and nonpersistent:

```text
definition/policy validation
→ immutable current-state snapshot
→ start/end/cancel/advance planning
→ effect-consumer preparation seams using fakes
→ stale-plan and idempotency-result seams
```

Save-backed application, durable operation history, real effect consumers, authored event content, notifications, and caller migration are separate later phases.

## 2. Binding decisions

1. **World-event definition and active event instance are separate concepts.** A definition is immutable catalog data; an instance is one scheduled/active/completed occurrence with its own identity and timestamps.
2. **The initial compatibility model permits at most one active global-primary world event.** A second start request does not silently overwrite, stack, merge, extend, or reset the active event.
3. **The initial exclusive group is `global_primary`.** Additional groups or simultaneous events require a later reviewed policy and migration.
4. **Current `WorldEventType` enum values remain legacy aliases only.** They are not production definition authority.
5. **New event definition IDs use stable lower-snake-case technical identity.** Player-facing names/descriptions are separate source/localization references.
6. **A valid definition includes typed effect descriptors and required-consumer policy.** An event with only an announcement and no supported technical effect is either explicitly classified as presentation-only by approved source/product intent or unavailable; it cannot accidentally masquerade as a gameplay modifier.
7. **The current five non-None enum concepts are not automatically approved production events.** They require versioned definitions, effect semantics, source review, and user approval when their meaning/player experience is unresolved.
8. **World-state time authority uses UTC instants and integer duration units.** A mutable float countdown is not authoritative.
9. **The authoritative end instant is calculated once with checked arithmetic from committed start time and approved duration.** It is not recomputed from frame deltas.
10. **Runtime ticking is a reconciliation trigger, not time storage.** It compares an injected current UTC instant with the committed instance boundaries.
11. **`Time.deltaTime`, `Time.timeScale`, frame rate, scene time, and process uptime never determine persistent event duration.**
12. **Clock input is injected and validated.** Backward jumps, implausible forward jumps, malformed persisted timestamps, and arithmetic overflow produce explicit lifecycle status and no duplicate effects.
13. **One event transition has one correlation/operation identity.** Start, end, cancel, recovery, and replay use stable IDs and deterministic revision checks.
14. **Exact replay is idempotent.** A repeated semantic request returns the prior result and does not reapply effects, save, emit events, or notify again.
15. **Conflicting payload for an existing correlation is rejected visibly.**
16. **State transitions are explicit:** `Scheduled`, `Active`, `Ended`, `Cancelled`, `Failed`, and `Superseded` where later policies authorize supersession.
17. **The first implementation does not support automatic supersession.** A new event cannot replace an active one unless a later definition policy explicitly allows and tests that transition.
18. **Cancellation is not deletion.** It produces a completed instance result with reason, revision, end time, effect removal, and history/notification behavior.
19. **Natural expiry and explicit cancellation are different results.**
20. **Effect activation/removal is transaction-oriented.** Required consumers prepare immutable plans before any candidate mutation.
21. **Every required effect consumer must be available and able to prepare before activation.** Missing or failed required consumers reject the start.
22. **Optional consumers are explicitly declared by the definition.** Their absence produces an exact degraded/optional result and cannot silently change event meaning.
23. **No consumer applies directly during definition validation or planning.**
24. **A world event is not committed until world-state instance, all required effect plans, owning operation ledger, and required notification outbox are staged and durably persisted/verified together.**
25. **No partial effect activation is represented as success.**
26. **Effect removal/end follows the same recoverable boundary.** A failed removal/persist operation cannot publish an ended state while modifiers remain active.
27. **Consumers read one immutable committed world-effect snapshot/revision.** They do not parse event IDs, notification copy, or mutable service fields.
28. **Consumers never infer an effect from the current enum name.**
29. **Current production/resource/territory/building/research/warzone systems do not yet consume world effects.** No event may claim those effects until the owning consumer integrations are implemented and tested.
30. **Definition effects reference stable typed profiles, not arbitrary multipliers supplied by a caller.**
31. **Numeric effect parameters are finite, bounded, versioned, and validated by the owning consumer policy.**
32. **A definition cannot change the meaning of an already persisted active instance silently.** The instance records the committed definition/effect version or immutable resolved-effect summary required for safe completion.
33. **Unknown future event definitions/instances are preserved safely.** Unsupported active state becomes read-only/degraded according to #137; it is not discarded or mapped to `None`.
34. **Ordinary queries and lifecycle planning never repair, delete, normalize destructively, or overwrite malformed world-state data.**
35. **Top-level legacy absence may normalize to no active event through a versioned migration.** It does not create a default event.
36. **Completed history is bounded and semantic.** It stores IDs/timestamps/results/revisions, not rendered localized text or raw diagnostics.
37. **Notifications occur only after a committed transition.** Technical services do not format hard-coded English or rich text.
38. **Start/end/cancel notifications use #177 definitions and the same event correlation identity.** Duplicate replay does not duplicate announcements.
39. **Notification failure after a nonmandatory committed transition is reported separately and does not roll back committed state.** Required notification outbox behavior is staged with the owning transaction when product policy demands guaranteed delivery.
40. **Typed transition events occur after commit/publish only.** Subscriber failure is isolated and cannot corrupt the committed state or prevent later subscribers.
41. **The current `TriggerWorldEvent(WorldEventType,int)` and `Tick(float)` methods remain legacy wrappers only.** No new production caller may use them.
42. **Legacy wrappers do not retain raw hard-coded announcement behavior in the authoritative implementation.**
43. **World-state player-facing names, descriptions, tone, and localization belong to Codex narrative/content mode.**
44. **Codex engineering owns IDs, schemas, validators, lifecycle, effect interfaces, transaction mechanics, persistence adapter, and tests.**
45. **GPT owns state/transaction/failure/sequence review.**
46. **User approval remains required for unresolved event meaning, effect/balance, cadence, priority, stacking, and integrated player experience.**
47. **The first planner PR changes no save fields, production service behavior, consumers, notifications, scenes, Android, narrative content, or balance.**
48. **The first planner PR does not edit `Bootloader.cs` while PR #203 holds its lock.**
49. **The eventual runtime service follows the accepted service-stack and cross-scene lifecycle contract.** It does not create a hidden `DontDestroyOnLoad` owner.
50. **No world-event implementation is cited as NVS-01 completion unless A1/G1 explicitly consumes a supported event definition and all transaction/notification/resume rules pass.**

## 3. Verified current baseline

### 3.1 Current interface

`IWorldStateService` currently exposes:

```text
string ActiveEventId
string CurrentEffect
float RemainingDuration
void TriggerWorldEvent(WorldEventType type, int durationSeconds)
void Tick(float deltaTime)
event Action<string> OnWorldStateChanged
```

Current `WorldEventType` values:

```text
None
SkyDisturbance
DistantSignal
RealmAnomaly
WarzoneSurge
PeacefulInterlude
```

Risks:

- nullable/raw strings rather than typed state;
- float duration rather than UTC lifecycle;
- no definition/catalog/version/provenance;
- no result status;
- no correlation/idempotency;
- no cancellation/recovery API;
- no persistence;
- no effect consumer contract;
- no transition revision;
- no commit boundary;
- no typed notification/event result.

### 3.2 Current service behavior

`WorldStateService` stores only:

```text
_activeEventId
_currentEffect
_remainingDuration
```

Current start behavior:

1. silently returns for `None` or nonpositive duration;
2. sets active ID to `type.ToString()`;
3. stores duration as float;
4. formats hard-coded English/rich-text string by enum;
5. calls `ShowMessage`;
6. invokes `OnWorldStateChanged`.

Current expiry behavior:

1. subtracts `deltaTime` every tick;
2. when value reaches zero, clears active state;
3. calls `ShowMessage("The world event has passed.")`;
4. invokes `OnWorldStateChanged`.

### 3.3 Current hard-coded copy

Technical service currently authors:

```text
The skies churn with an unnatural omen.
A distant signal echoes beyond the known realms.
Magical currents destabilize across the realms.
The warzone surges with dangerous energy.
An unusual calm settles over the kingdoms.
The world event has passed.
```

This copy is not retained as technical authority.

### 3.4 Current persistence

`SaveGameData` contains no world-event instance, lifecycle revision, transition ledger, effect summary, history, or notification outbox fields.

Process exit/reload therefore loses the event. Offline time does not end it deterministically, and restarting can recreate/announce unrelated state only through external callers.

### 3.5 Current effects/consumers

No verified current consumer changes resource production, territory, building/research, warzone, economy, combat, world presentation, or another system based on `CurrentEffect`.

The current service therefore announces thematic gameplay effects without proving any effect was applied.

### 3.6 Current registration

`Bootloader` constructs and registers one `WorldStateService` with save and notification dependencies. The service itself is not ticked by the currently audited `Bootloader.Update`; there is no verified production caller driving `Tick(float)`.

Even if a caller existed, float/frame ticking would remain unsuitable for persistence/offline lifecycle.

## 4. World-event definition model

### 4.1 Stable definition ID

New IDs use:

```text
^al_world_event_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$
```

Examples of technical identity only:

```text
al_world_event_sky_disturbance
al_world_event_distant_signal
al_world_event_realm_anomaly
al_world_event_warzone_surge
al_world_event_peaceful_interlude
```

Those examples do not approve their content/effects.

### 4.2 Definition envelope

Equivalent immutable fields:

```text
definitionId
schemaVersion
contentVersion
sourceRevision
legacyAliases
category
scope
exclusiveGroup
priority
startPolicy
durationPolicy
cancellationPolicy
supersessionPolicy
effectDescriptors
requiredConsumerIds
optionalConsumerIds
startNotificationDefinitionId
endNotificationDefinitionId
cancelNotificationDefinitionId
localization/content references
privacyClass
```

Definitions follow #183 catalog identity/version/hash/provenance rules.

### 4.3 Category and scope

Minimum categories:

```text
NarrativeSignal
RealmCondition
WarzoneCondition
ProductionCondition
SystemCondition
```

Initial scope:

```text
Global
```

Realm/territory/region/player-specific scope is deferred until a later reviewed schema and persistence model exists.

### 4.4 Exclusive group

Initial supported value:

```text
global_primary
```

Every first-version production definition belongs to this group.

No more than one `global_primary` instance may be scheduled/active.

### 4.5 Start policy

Equivalent fields:

```text
allowedSourceSystems
requiresNoActiveExclusiveInstance
requiresWritableProfile
requiresValidatedConsumers
requiresCorrelation
```

A source system not listed cannot start the event.

### 4.6 Duration policy

Equivalent fields:

```text
minimumDurationSeconds
maximumDurationSeconds
defaultDurationSeconds
callerMayOverrideDuration
```

Rules:

- positive integer seconds;
- minimum <= default <= maximum;
- checked conversion to UTC ticks/seconds;
- no duration supplied outside the policy;
- a caller override is accepted only when the definition explicitly permits it;
- no floating-point duration;
- product cadence/balance values require approved source/user decision.

The technical platform may impose an absolute representability cap, but it does not silently shorten an approved duration.

### 4.7 Cancellation policy

```text
NotCancellable
CancellableByOwningSource
CancellableByApprovedRecovery
```

No arbitrary UI/service may cancel an event merely because it knows the instance ID.

### 4.8 Supersession policy

Initial supported policy:

```text
RejectWhileExclusiveInstanceActive
```

Future policies such as priority replacement or scheduled queues require a new reviewed schema and complete failure/recovery tests.

### 4.9 Presentation-only definitions

A definition with zero effect descriptors is valid only when:

- category/source explicitly classifies it as presentation-only;
- product/narrative source approves that meaning;
- it does not claim resource/warzone/production/gameplay changes;
- required notification content exists;
- the instance remains lifecycle/persistence/idempotency safe.

Engineering cannot turn an unsupported gameplay event into presentation-only fallback silently.

## 5. Effect descriptor model

### 5.1 Effect identity

New effect profile IDs use:

```text
^al_world_effect_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$
```

### 5.2 Descriptor fields

Equivalent immutable fields:

```text
effectId
effectSchemaVersion
consumerId
operation
parameters
required
applicationOrder
removalOrder
sourceRevision
```

### 5.3 Consumer IDs

New consumer IDs use:

```text
^al_world_consumer_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$
```

Potential future consumers:

```text
al_world_consumer_resource_production
al_world_consumer_territory_income
al_world_consumer_building
al_world_consumer_research
al_world_consumer_warzone
al_world_consumer_world_presentation
```

These names do not mean the consumers currently exist.

### 5.4 Effect operations

Operations are consumer-specific stable enum/profile values, not arbitrary script names or delegates.

Examples of shape only:

```text
Multiplier
AdditiveModifier
CapabilityBlock
PresentationProfile
```

The owning consumer validates exact supported operations.

### 5.5 Parameter validation

- immutable typed values;
- finite/checked numeric values;
- no raw expressions or executable code;
- no player-facing prose;
- no unvalidated resource/realm/territory IDs;
- no parameter outside the consumer schema;
- deterministic canonical ordering/hash.

### 5.6 Required versus optional

Required effect:

- consumer must resolve;
- preparation must succeed;
- plan must participate in activation/removal transaction;
- failure rejects the transition.

Optional effect:

- absence/failure follows exact definition policy;
- result records the omission;
- notification/content cannot claim the omitted effect;
- optional omission cannot change a gameplay-critical definition into misleading success.

## 6. Definition validation

Minimum statuses:

```text
Valid
InvalidId
InvalidEnvelope
UnsupportedVersion
InvalidDurationPolicy
InvalidExclusivePolicy
InvalidCancellationPolicy
InvalidEffect
MissingRequiredConsumer
InvalidNotificationReference
InvalidContentReference
CrossReferenceFailure
CatalogUnavailable
```

Validation rules:

- nonblank/unique IDs and aliases;
- no alias cycles/collisions/shadowing;
- supported schema/content version;
- exact `Global` scope in v1;
- exact `global_primary` exclusive group in v1;
- deterministic priority range;
- valid duration bounds;
- valid source-system allowlist;
- unique effect IDs;
- unique consumer/effect combinations as required by consumer policy;
- deterministic application/removal order with no duplicates;
- every required consumer exists/capability matches;
- every notification definition resolves through #177 definition source when notifications are required;
- content/localization references resolve;
- presentation-only definition explicitly classified;
- no hard-coded raw player copy;
- deterministic diagnostics/order.

Suggested diagnostics:

```text
AL-WST-DEFINITION
AL-WST-ID
AL-WST-DURATION
AL-WST-EXCLUSIVE
AL-WST-CONSUMER
AL-WST-EFFECT
AL-WST-CLOCK
AL-WST-INSTANCE
AL-WST-STALE
AL-WST-CORRELATION
AL-WST-APPLY
AL-WST-PERSISTENCE
AL-WST-NOTIFICATION
AL-WST-EVENT-HANDLER
AL-WST-LEGACY
```

## 7. Event instance model

### 7.1 Instance fields

Equivalent immutable fields:

```text
instanceId
definitionId
definitionVersion
definitionSourceRevision
correlationId
operationId
sourceSystemId
exclusiveGroup
state
scheduledAtUtc
startedAtUtc
expectedEndAtUtc
completedAtUtc
completionReason
revision
resolvedEffectSummary
committedEffectRevision
createdByProfileVersion
```

### 7.2 Instance ID

- unique stable opaque ID;
- generated by the owning orchestrator before persistence;
- maximum 128 UTF-8 bytes;
- no private path/account/token data;
- not derived solely from current time;
- retries use the same ID/correlation for the same semantic event.

### 7.3 States

```text
Scheduled
Active
Ended
Cancelled
Failed
Superseded
```

Initial implementation uses:

```text
Active
Ended
Cancelled
Failed
```

Scheduled/superseded may be represented in the schema but are not activated until their policies are implemented.

### 7.4 Completion reason

Minimum typed reasons:

```text
NaturalExpiry
CancelledByOwner
CancelledByRecovery
ActivationFailed
RemovalFailed
DefinitionUnsupported
ClockInvalid
```

Rendered copy does not come from these enum names.

### 7.5 Resolved effect summary

The instance records enough immutable semantic information to remove/end the same effects even if the catalog later changes:

```text
effectId
consumerId
operation
canonical parameters/hash
consumer plan schema version
```

The exact persisted strategy may retain the full safe typed summary or a definition version guaranteed available through migration. The implementation must prove end/removal after reload and catalog update/downgrade.

## 8. World-state snapshot

Equivalent immutable snapshot:

```text
status
snapshotRevision
policy/catalog revision
activeInstance or null
completedHistory summary
committedEffectRevision
profileWritable
diagnostics
```

Statuses:

```text
AvailableNoActiveEvent
AvailableActive
AvailableReadOnly
UnavailableNoCurrentSave
UnavailableMalformedState
UnavailableCatalog
UnsupportedDefinitionVersion
RecoveryRequired
```

Rules:

- pure read;
- no countdown mutation;
- no save/event/notification;
- no raw backing references;
- unknown future active instance preserved and returned read-only/unavailable rather than cleared;
- remaining duration is derived from injected now and timestamps only as a query value.

### 8.1 Remaining time query

Equivalent result:

```text
status
instanceId
nowUtc
expectedEndAtUtc
remainingSeconds
isExpired
clockDiagnostic
```

- checked integer time difference;
- negative result becomes zero/expired in query semantics without committing end;
- end transition still requires planner/transaction;
- invalid clock/timestamps return unavailable/diagnostic.

## 9. Clock and temporal policy

### 9.1 Clock abstraction

```text
IWorldClock.UtcNow
```

Tests inject deterministic UTC instants.

### 9.2 Timestamp validation

- valid UTC instant/Unix seconds within supported runtime range;
- started <= expected end;
- completed >= started when completed;
- state/timestamp consistency;
- no zero/default timestamp represented as valid active time;
- checked duration addition;
- no local time/time-zone conversion.

### 9.3 Backward clock movement

When observed `nowUtc` is before the last committed/observed trusted lifecycle instant beyond an approved tolerance:

- do not extend/restart the event silently;
- do not reapply effects;
- return `ClockInvalid`/recovery-required status;
- preserve instance/effects;
- suppress normal expiry/notification until a trusted policy resolves;
- log stable technical diagnostic without private data.

The exact anti-tamper/product policy is a later user/product decision. This specification requires safety and nonduplication, not punishment.

### 9.4 Forward clock movement/offline

A now instant at or after expected end prepares one natural-end transition.

- event effects do not accrue repeated rewards merely because time advanced;
- end applies once via ledger/revision;
- reload/retry returns prior result;
- large but representable forward time ends the event, subject to #137 candidate validation;
- impossible timestamp/overflow is malformed, not normal expiry.

### 9.5 Runtime reconciliation

The runtime owner may call:

```text
ReconcileTo(nowUtc)
```

or an equivalent lifecycle processor at controlled cadence.

It does not need per-frame ticking. It may run:

- after load/publish;
- at a bounded periodic interval;
- on relevant query/scene lifecycle checkpoints;
- before applying dependent domain operations;
- on pause/quit through the accepted #153 lifecycle where safe.

## 10. Transition request contracts

### 10.1 Start request

Equivalent immutable fields:

```text
definitionId
instanceId
correlationId
operationId
sourceSystemId
requestedStartAtUtc
requestedDurationSeconds optional
expectedSnapshotRevision optional
```

### 10.2 End/reconcile request

```text
instanceId
correlationId
operationId
sourceSystemId
observedNowUtc
expectedSnapshotRevision
```

Natural end correlation/operation is derived/stored deterministically from the committed instance, not regenerated each tick.

### 10.3 Cancel request

```text
instanceId
correlationId
operationId
sourceSystemId
cancelReason
requestedAtUtc
expectedSnapshotRevision
```

Source and reason must satisfy definition cancellation policy.

## 11. Preparation status and plans

### 11.1 Statuses

```text
Prepared
NoChangeAlreadyInState
AlreadyCommitted
RejectedNoCurrentSave
RejectedReadOnlyProfile
RejectedDefinitionUnavailable
RejectedUnsupportedDefinition
RejectedInvalidRequest
RejectedInvalidDuration
RejectedActiveExclusiveInstance
RejectedNoActiveInstance
RejectedWrongInstance
RejectedCancellationNotAllowed
RejectedConsumerUnavailable
RejectedEffectPreparation
RejectedClockInvalid
RejectedStaleSnapshot
RejectedCorrelationRequired
RejectedCorrelationConflict
RejectedOverflow
```

### 11.2 Transition plan

Equivalent immutable plan:

```text
planId
transitionKind: Start | End | Cancel
previousSnapshotRevision
expectedNewRevision
instanceBefore
instanceAfter
preparedEffectPlans
operationId
correlationId
sourceSystemId
notificationRequests
ledgerEntry
policy/catalog revisions
diagnostics
```

The plan contains no mutable save/service/consumer references.

### 11.3 Start planning

1. validate profile/write status;
2. validate request/correlation/time;
3. resolve definition/version/source;
4. validate no active instance in `global_primary`;
5. calculate start/end with checked UTC arithmetic;
6. resolve every required/optional consumer;
7. ask consumers to prepare activation plans against the candidate snapshot;
8. reject on any required failure;
9. create immutable active instance/effect summary;
10. stage ledger and required #177 start request;
11. return plan with no mutation/save/event/notification.

### 11.4 End planning

1. validate active instance/revision;
2. validate observed now/clock;
3. require natural expiry boundary;
4. resolve effect summary/consumers;
5. prepare removal plans in deterministic removal order;
6. stage ended instance/history/ledger/notification;
7. no mutation/save/event.

### 11.5 Cancel planning

Same as end, plus:

- validate source/reason/cancellation policy;
- record cancel completion reason;
- use cancel notification definition;
- no silent conversion to natural end.

## 12. Effect consumer contract

### 12.1 Interface shape

Equivalent internal interface:

```text
consumerId
supportedSchemaVersions
PrepareActivate(instance, descriptor, candidateSnapshot)
PrepareRemove(instance, resolvedEffectSummary, candidateSnapshot)
Apply(plan, mutationTarget)
```

### 12.2 Preparation result

```text
Prepared
NoChange
RejectedUnsupportedEffect
RejectedInvalidParameter
RejectedDomainUnavailable
RejectedMalformedDomain
RejectedOverflow
RejectedConflict
RejectedDependencyUnavailable
```

### 12.3 Effect plan

Immutable fields:

```text
consumerId
effectId
consumerPlanVersion
transitionKind
previousEffectState
newEffectState
expectedConsumerRevision
parameters/hash
diagnostics
```

### 12.4 Apply rules

- apply only to an isolated candidate/mutation target;
- revalidate expected consumer revision;
- stale plan rejects;
- no independent save;
- no event/notification;
- no partial application represented as complete;
- apply order deterministic;
- rollback/candidate discard handled by owning transaction before publish.

### 12.5 Consumer snapshot publication

After durable world transition publish, consumers expose one immutable committed world-effect revision/snapshot.

Dependent operations validate the revision they consumed. They do not read mutable service fields mid-transaction.

## 13. Transaction and idempotency

### 13.1 Owning ledger

World-state transition ledger records equivalent:

```text
operationId
correlationId
instanceId
definitionId
transitionKind
previousRevision
newRevision
resultStatus
committedAtUtc
```

### 13.2 Apply order

Future save-backed implementation:

```text
validate/rebuild current candidate snapshot
→ verify ledger/correlation not already committed
→ revalidate transition/effect plans
→ apply world instance/history state to clone
→ apply all effect consumer plans to same clone/candidate
→ stage ledger entry
→ stage required notification outbox
→ validate complete candidate
→ persist/verify through #137
→ publish current save/world/effect revisions
→ emit committed transition event
→ enqueue/continue notification delivery
```

### 13.3 Failure behavior

- before persist: discard candidate, no committed change;
- persist failure: prior committed state remains/preservation status from #137;
- final verification failure: no publish, recovery status;
- notification presentation failure after commit: transition remains committed, receipt/outbox reports failure;
- event subscriber failure: transition remains committed, later subscribers attempted;
- retry with same operation returns prior result or resumes required recovery, no duplicate effect.

### 13.4 Conflicting duplicate

Same operation/correlation with different definition, duration, transition, instance, or effect payload:

```text
RejectedCorrelationConflict
AL-WST-CORRELATION
```

No mutation.

## 14. Persistence model after #137

### 14.1 Proposed top-level shape

Equivalent field:

```text
SaveGameData.WorldState
```

Any actual edit requires the `SaveGameData.cs` shared-file lock.

### 14.2 Persisted world-state data

Equivalent:

```text
schemaVersion
revision
activeInstance or null
completedInstances
operationLedger or owning shared ledger references
committedEffectRevision
lastReconciledAtUtc
```

### 14.3 Completed history retention

Initial technical bound:

```text
50 completed instances
plus active instance
plus operation-ledger entries required by owning retention/idempotency policy
```

Rules:

- prune only completed terminal instances;
- deterministic oldest-completed first;
- never prune active/recovery-required instance;
- do not prune ledger evidence still required for duplicate safety;
- content/player history UX is separate;
- no rendered notification copy stored;
- full profile deletion removes world-state/history/outbox artifacts under #137.

A future product-visible event history may use a different separately approved retention/source UI.

### 14.4 Legacy absence migration

Old save with no `WorldState` field:

```text
no active event
revision 0/current migration value
empty history
```

This is non-destructive compatible normalization. It does not seed an event or announcement.

### 14.5 Malformed state

Examples:

- active instance with blank/unknown ID;
- unsupported definition version;
- invalid timestamps/order;
- duplicate active instances;
- revision inconsistency;
- effect summary/consumer mismatch;
- duplicate/conflicting operation ledger;
- terminal instance still active;
- history null entries.

Rules:

- preserve raw candidate;
- disable world-state mutation/effects where safe;
- prefer cleaner backup;
- no silent reset to `None`;
- no announcement claiming end/recovery;
- explicit data-changing repair only through #137 after quarantine and validation.

### 14.6 Forward definitions

An active instance referencing a newer unsupported definition/effect version:

- preserve raw data;
- expose read-only/recovery-required state;
- do not reapply/remove through guessed current definition;
- do not auto-save/downgrade;
- use persisted safe effect summary/migration only when exact support exists;
- player-safe unavailable/recovery notification follows approved #177/#137 behavior.

## 15. Notifications and content

### 15.1 No raw service copy

Remove current hard-coded strings from authoritative service implementation after content migration.

Technical event definitions reference:

```text
startNotificationDefinitionId
endNotificationDefinitionId
cancelNotificationDefinitionId
localization/content source references
```

### 15.2 Publication timing

- start notification after active state/effects are durably committed;
- end notification after effect removal/end state durably committed;
- cancel notification after cancellation/removal durably committed;
- failed start may map to a typed failure notification from the owning UI/orchestrator, not from the low-level planner;
- duplicate ledger replay produces no duplicate #177 request.

### 15.3 Parameters

Typed parameters may include approved stable references:

```text
definition/content reference
start/end time
duration
scope/realm reference when later supported
```

No raw technical event ID is shown as player copy. No hard-coded Unity rich text from the service.

### 15.4 Notification/source ownership

- Codex narrative/content writes names/descriptions/start/end/cancel copy and localization keys;
- Codex engineering validates placeholders/schema and maps definitions;
- GPT reviews semantic outcome mapping;
- user approves unresolved tone/product meaning.

## 16. Commit events and queries

### 16.1 Committed transition event

Equivalent immutable event:

```text
instanceId
definitionId
transitionKind
previousState
newState
previousRevision
newRevision
operationId
correlationId
sourceSystemId
committedAtUtc
committedEffectRevision
```

- after durable publish only;
- exactly once;
- no player copy;
- subscriber failures isolated with `AL-WST-EVENT-HANDLER`;
- duplicate replay does not re-emit.

### 16.2 Query API

Typed queries:

```text
GetWorldStateSnapshot()
GetActiveInstance()
GetRemainingTime(nowUtc)
GetCommittedEffectSnapshot()
```

- immutable;
- pure;
- no tick/mutation/save;
- no hard-coded labels;
- exact unavailable/malformed/read-only statuses.

## 17. Legacy wrapper migration

Current methods:

```text
TriggerWorldEvent(WorldEventType,int)
Tick(float)
ActiveEventId
CurrentEffect
RemainingDuration
OnWorldStateChanged
```

Rules:

- inventory current callers before integration;
- no new caller;
- mark/document legacy;
- enum maps through explicit alias only;
- `TriggerWorldEvent` eventually delegates to the standalone typed transaction adapter and returns no reliable result at the legacy boundary;
- `Tick(float)` becomes a no-op/development diagnostic or invokes reconciliation using injected UTC rather than subtracting delta, subject to migration;
- raw `CurrentEffect` string wrapper may expose a technical ID for compatibility only, not player copy;
- float `RemainingDuration` wrapper is lossy and no new caller may use it;
- event wrapper eventually maps from committed typed transition events only;
- wrappers never publish precommit notifications.

## 18. Consumer integration requirements

### 18.1 Resource production

After #163/#165/#166:

- consumer validates supported production modifier profile;
- contribution remains finite/checked;
- one world-effect revision participates in a production batch;
- transition cannot partially change only some resource paths;
- no event definition supplies arbitrary rate text/numbers outside approved profile.

### 18.2 Territory/warzone

After #166/#174:

- effect uses typed consumer profile;
- capture/reward/result identities remain authoritative;
- world event cannot fabricate credits/progress;
- removal/reload exact once.

### 18.3 Building/research/training

After #165/#183:

- effects reference validated definition/profile IDs;
- no query mutation/seeding;
- cost/time/progress arithmetic checked;
- current world revision recorded in operation result where relevant.

### 18.4 World presentation

A presentation-only consumer may drive sky/material/ambient state after scene/asset/design approval.

- presentation failure cannot claim gameplay effect failure when gameplay committed, but is reported separately;
- no terrestrial/narrative visual meaning invented;
- reduced-motion/accessibility supported;
- source asset/catalog provenance validated.

### 18.5 Privacy/notifications

No event exposes private profile data or raw IDs. #177 handles visible delivery and persistence separately.

## 19. Implementation sequence

### Phase A — this merged specification

No executable/content/save change.

### Phase B — pure definition, snapshot, lifecycle, and effect planning

Branch:

```text
codex/world-state-contract-planner
```

Scope:

- immutable definition/instance/snapshot/request/result/plan/event models;
- definition/policy validator;
- injected definition resolver and UTC clock;
- one-active-exclusive lifecycle planner;
- start/end/cancel/reconcile planning;
- effect-consumer registration/preparation interfaces with fakes;
- stale-plan/idempotency/fake mutation-target seams;
- current caller/interface inventory;
- complete EditMode tests.

Do not include:

```text
SaveGameData.cs / LocalSaveGameService.cs
WorldStateService production behavior
Bootloader.cs / ServiceLocator.cs
real consumers
notifications/content
scenes/UI
Android
balance/effect definitions
caller migration
```

### Phase C — definition/effect/content source

After the applicable #183 catalog foundation and owning consumer contracts:

- Codex engineering supplies schemas/technical IDs/effect profiles/validation;
- Codex narrative/content supplies approved event meaning/copy/localization;
- user approves unresolved event effects/cadence/priority/stacking/player experience;
- generated artifacts retain version/hash/provenance;
- no current enum value becomes approved automatically.

### Phase D — persistence and service integration

Prerequisites:

- #137 accepted clone/persist/publish and shared-file lock;
- Phase B/C accepted;
- #153 service lifecycle accepted;
- #177 typed queue contract available for mapped notifications;
- required consumers accepted.

Scope:

- save fields/migration/history/ledger;
- typed service API and standalone transition adapter;
- legacy wrapper migration;
- load/offline/reconcile/fault/deletion tests;
- committed events/notification outbox;
- no broad consumer implementation.

### Phase E — focused consumer integrations

Separate PRs under owning issues:

```text
resource/production #163/#165/#166
warzone/battle #166/#174
building/research #165
world presentation #181/#183 or focused issue
NVS-01 #133/#134 when approved
```

## 20. Expected file boundary

Phase B likely adds:

```text
unity/Assets/AL/Scripts/Core/Interfaces/WorldState/**
unity/Assets/AL/Scripts/Services/WorldState/** pure validators/planners
small additive typed members in IWorldStateService.cs only if needed
unity/Assets/AL/Tests/EditMode/WorldState/**
unity/Docs/World_State_Caller_Inventory.md
matching .meta files
```

Phase D may change:

```text
unity/Assets/AL/Scripts/Core/Interfaces/IWorldStateService.cs
unity/Assets/AL/Scripts/Kingdom/Narrative/WorldStateService.cs
SaveGameData.cs with declared lock
LocalSaveGameService.cs only through #137
focused tests
```

Prohibited in Phase B:

```text
SaveGameData.cs
LocalSaveGameService.cs
Bootloader.cs
LocalGameDataService.cs
WorldStateService production body
resource/territory/building/research/warzone services
notification service/content
Android
scenes/Build Settings
narrative copy/effect balance
```

## 21. Required tests

### 21.1 Definition validation

- valid global-primary definition;
- blank/duplicate ID;
- legacy alias success/collision/cycle/shadowing;
- unsupported schema/content version;
- invalid category/scope/exclusive group;
- invalid priority;
- invalid source allowlist;
- zero/negative/min>max/default-out-of-range duration;
- duration overflow;
- invalid cancellation/supersession policy;
- no effects without presentation-only approval;
- duplicate/invalid effect ID/order;
- missing required consumer;
- optional consumer policy;
- invalid parameter/non-finite numeric;
- missing notification/content reference;
- deterministic diagnostics/order.

### 21.2 Snapshot/instance validation

- no active event;
- valid active instance;
- blank/unknown definition;
- unsupported definition version;
- invalid state/timestamp combination;
- start after end;
- terminal without completed time;
- multiple active instances fixture;
- invalid revision/effect summary;
- preserved future instance read-only;
- immutable query/repeated purity.

### 21.3 Start planning

- valid start;
- invalid/None legacy alias;
- unknown definition;
- invalid duration/override;
- no current save/read-only;
- active exclusive conflict;
- required correlation missing;
- same request replay already committed;
- conflicting correlation;
- missing required consumer;
- optional consumer absent;
- effect preparation failure;
- arithmetic overflow;
- stale snapshot;
- no mutation/save/event/notification.

### 21.4 End/reconcile planning

- before end no change;
- exact end instant;
- after end;
- repeated reconcile/end;
- wrong instance;
- no active instance;
- clock backward invalid;
- malformed/future timestamp;
- removal consumer missing/fails;
- stale snapshot/consumer revision;
- deterministic removal order;
- no mutation/save/event/notification.

### 21.5 Cancellation

- cancellable owner success;
- not cancellable;
- unauthorized source;
- recovery cancellation;
- wrong/no active instance;
- exact replay/conflict;
- removal failure;
- distinct completion reason/notification mapping;
- no silent natural-end conversion.

### 21.6 Effect consumers

- required prepare/apply success;
- unsupported effect/parameter;
- malformed dependent domain;
- checked overflow/non-finite;
- stale consumer revision;
- apply failure leaves fake candidate unchanged;
- one required failure prevents all commit;
- optional omission recorded;
- immutable committed effect snapshot/revision.

### 21.7 Transaction/idempotency

- apply start to one fake candidate;
- failure before each apply/persist/publish boundary;
- one complete start commit;
- one complete end commit;
- one complete cancel commit;
- duplicate same session/reload;
- conflicting duplicate;
- ledger/outbox staged once;
- event once after publish;
- notification once after publish;
- notification/presenter failure separate;
- subscriber failure isolated.

### 21.8 Persistence/offline phase

- old save missing field;
- active save/reload;
- process offline before end;
- offline crosses end exactly once;
- repeated failed persistence does not duplicate removal/notification;
- backward clock;
- large forward clock;
- malformed timestamps/history/ledger;
- unknown future definition;
- cleaner backup preference;
- bounded history pruning at 50;
- active/recovery/ledger evidence retained;
- full profile deletion.

### 21.9 Legacy wrappers

- enum alias mapping exact;
- invalid enum/duration no silent success;
- no new caller inventory;
- float tick no longer authoritative;
- raw effect string not player copy;
- float remaining lossy/migration diagnostic;
- event only after committed transition;
- no hard-coded English/rich text in production service after migration.

### 21.10 Consumer integrations

- resource production consumes one revision;
- effect start/end exact once;
- missing consumer rejects event;
- no partial modifier set;
- no fabricated credit/progress/reward;
- operation started under one world revision remains deterministic according to owning contract;
- presentation failure separate from gameplay;
- NVS event integration only through approved source/specification.

## 22. Canonical validation

Phase B:

```powershell
$repo = "D:\260711\MY\AndroidStudioProjects\AnotherLife"
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"

& $unity -batchmode -quit -nographics `
  -projectPath "$repo\unity" `
  -logFile "$repo\unity\Logs\WorldStatePlannerCompile.log"

& $unity -batchmode -nographics `
  -projectPath "$repo\unity" `
  -runTests -testPlatform EditMode -assemblyNames AL.EditMode.Tests `
  -testResults "$repo\unity\Logs\WorldStatePlannerEditMode.xml" `
  -logFile "$repo\unity\Logs\WorldStatePlannerEditMode.log"
```

Later phases additionally run:

- corrected #127 PlayMode suite;
- save/offline/fault/deletion tests through #137;
- notification delivery tests through #177;
- consumer integration tests;
- #150 Player build/launch where world-state startup/presentation is packaged.

Report:

- exact base/head SHA;
- changed files and lock state;
- definition/effect schema/content/source versions;
- current caller/interface inventory;
- every definition/instance/start/end/cancel/clock/effect/stale/idempotency test row;
- focused/complete EditMode totals/XML;
- PlayMode/Player applicability;
- no save/service/consumer/content mutation in Phase B;
- final `git diff --check origin/main...HEAD`;
- final repository status;
- every blocked/unperformed check.

Duplicate-workspace, exit `199`, missing XML, float countdown cited as persistence proof, hard-coded announcement cited as effect proof, or notification log cited as committed delivery is blocked validation.

## 23. Acceptance criteria

- [ ] Event definitions are immutable, versioned, source-controlled, and strictly validated.
- [ ] Current enum values are explicit legacy aliases rather than production authority.
- [ ] Initial policy permits exactly one active `global_primary` event and rejects silent stacking/replacement.
- [ ] Event instances have stable IDs/correlation, explicit states/reasons, revisions, and UTC boundaries.
- [ ] Runtime/offline lifecycle derives from injected UTC, not float/frame countdown.
- [ ] Backward/invalid clock state cannot extend, restart, or duplicate an event silently.
- [ ] Every gameplay event has validated typed effect descriptors and required consumers.
- [ ] Missing/failed required consumers reject activation before announcement.
- [ ] Start/end/cancel plans are immutable, save-free, stale-safe, and contain no live references.
- [ ] All effect/world/ledger/outbox changes compose into one clone/persist/verify/publish boundary.
- [ ] Partial activation/removal is never represented as success.
- [ ] Exact replay cannot duplicate effects, history, events, or notifications.
- [ ] Unknown future instances are preserved/read-only rather than cleared or guessed.
- [ ] Queries are pure/immutable and expose honest unavailable/malformed statuses.
- [ ] Hard-coded player-facing strings leave the technical service and resolve from approved content.
- [ ] Notifications/events occur exactly once after committed transition.
- [ ] Persistence supports legacy absence, offline end, bounded history, recovery, and full deletion.
- [ ] The first planner PR edits no saves, production service body, consumers, notifications, content, scenes, Android, or shared files.
- [ ] Canonical compile and complete/focused tests pass with exact evidence.
- [ ] No unapproved event meaning, effect/balance, cadence, stacking, narrative, Android, NVS implementation, or unrelated change is included.

## 24. Codex handoff

```text
Codex engineering: implement only Phase B of issue #172 from current main using unity/Docs/World_State_Lifecycle_Transaction_Spec.md. Create codex/world-state-contract-planner. Add immutable world-event definition/effect/instance/snapshot/request/result/plan/event models, strict definition/instance validators, an injected UTC clock/definition resolver, one-active-global lifecycle planning for start/end/cancel/reconcile, fake effect-consumer registration/preparation/apply seams, stale-plan/idempotency/fake-target tests, and a current caller/interface inventory. Perform no mutation, save, event, notification, or real consumer effect. Do not edit SaveGameData.cs, LocalSaveGameService.cs, Bootloader.cs, LocalGameDataService.cs, WorldStateService production behavior, resource/territory/building/research/warzone services, scenes, Android, narrative/localization content, or balance. Run canonical Unity validation and return one focused draft PR for GPT review.
```
