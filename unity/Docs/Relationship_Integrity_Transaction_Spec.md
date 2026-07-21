# Relationship Integrity, Classification, and Transaction-Planning Specification

**Status date:** 2026-07-16  
**Tracking issue:** #176  
**Specification owner:** GPT  
**Validation/planner/service owner:** Codex engineering mode  
**NPC/faction/persona meaning and player-facing labels:** Codex narrative/content mode  
**Final product/creative approval:** user  
**Audited baseline:** `3319275d192b8d909658fd665af8ca8ef602f9cf`  
**Validated Unity target:** `2022.3.62f3`  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`  
**Catalog authority:** `unity/Docs/Game_Data_Catalog_Authority_Spec.md`  
**Save semantic authority:** `unity/Docs/Save_Semantic_Compatibility_Policy.md`  
**Notification authority:** `unity/Docs/Notification_Delivery_Contract_Spec.md`

## 1. Goal

Make NPC affinity, faction reputation, and persona traits:

- safe to read from malformed/legacy saves;
- stable-ID and catalog validated;
- finite/checked-arithmetic safe;
- duplicate-aware without data-changing repair in ordinary service calls;
- classifiable without hard-coded player-facing English;
- representable as immutable snapshots;
- preparable as no-save mutation plans;
- applicable later to a cloned transaction candidate;
- composable with Gold, quest completion, chapter unlock, and other NVS-01 consequences;
- duplicate-safe through the owning transaction ledger;
- observable after durable commit through typed events/notifications.

The first engineering phase is deliberately pure and nonpersistent:

```text
identity/policy resolver
→ immutable relationship snapshots
→ validation/classification results
→ prepared mutation plans
→ stale-plan/apply seams with fake targets
```

Actual save-backed application, standalone persistence, durable idempotency, and caller migration remain gated by corrected #136 evidence, #137 persistence, #183 catalogs, and the owning transaction issues.

## 2. Binding decisions

1. **Relationship services do not invent unknown NPCs or factions from arbitrary strings.** A production mutation requires a valid immutable catalog identity.
2. **Known relationship collections are sparse by design.** Absence of a valid known NPC/faction row means neutral/unestablished value `0`; a prepared valid mutation may stage creation from zero.
3. **Unknown nonblank IDs are preserved in raw save data but excluded from current supported reads, classification, and mutation.**
4. **Blank IDs, null rows, duplicate supported IDs, non-finite affinity, and contradictory policy data make the affected relationship domain malformed.** Ordinary reads/mutations do not delete, merge, select first/last, clamp, or repair them.
5. **A cleaner save candidate outranks a malformed relationship domain under #137.** If no cleaner candidate exists, the domain remains disabled/degraded until an explicit quarantined repair exists.
6. **The three domains validate independently.** A malformed affinity list does not automatically disable faction/persona when their snapshots are valid, subject to the overall #137 writable-profile policy.
7. **All query methods are pure.** They do not create rows, default objects, classify by mutating, save, emit change events, or reorder collections.
8. **All returned snapshots and classification results are immutable.** Callers never receive the save-backed lists/objects.
9. **Affinity preserves the existing intended numeric range `[-100, 100]`.** Existing current-service clamp semantics are retained for valid prepared mutations.
10. **Affinity current values and deltas must be finite.** `NaN`/infinity are malformed/rejected, never converted into a rank.
11. **Affinity addition is calculated in finite `double`, then clamped once to `[-100,100]`, then converted to finite `float`.** The result exposes requested versus applied delta and `wasClamped`.
12. **A finite current affinity outside `[-100,100]` is malformed historical state.** Ordinary mutation does not silently clamp it.
13. **Faction reputation preserves current signed `int` semantics without adding a new balance range.** Addition is `checked`; overflow/underflow rejects with no change.
14. **Persona trait values preserve current signed `int` semantics without adding a new narrative/balance range.** Addition is `checked`; overflow/underflow rejects with no change.
15. **Negative faction/persona values remain technically supported because current behavior permits them and no approved product rule removes them.** A future narrower policy requires explicit source decision and migration.
16. **Zero delta is a typed `NoChange`.** It mutates, saves, emits, and notifies zero times.
17. **Undefined `PersonaTrait` is rejected visibly.** It cannot log/save as if applied.
18. **Player-facing rank, affiliation, and persona labels are not returned by authoritative technical APIs.** Typed classification IDs/results are returned; labels resolve from approved content/localization.
19. **The existing affinity/faction thresholds are preserved as legacy technical profile values, not as authored English meaning.**
20. **Persona dominant-trait queries never manufacture a unique meaning from missing, all-zero, or tied values.** The typed result distinguishes `Unique`, `Tie`, `AllZero`, and unavailable/malformed states.
21. **The current missing→Sage and all-zero→Warlord inconsistency is not retained in the authoritative API.** Legacy wrapper behavior may remain temporarily for compile compatibility only and emits a migration diagnostic.
22. **Prepared mutation planning is pure.** It does not hold references to save rows and does not mutate current state.
23. **Every plan records an expected immutable snapshot revision/fingerprint.** Application rejects a stale plan if domain state changed.
24. **Prepared plans do not save.** The owning orchestrator applies them to a cloned/transaction candidate and persists once.
25. **One-time narrative idempotency belongs to the owning transaction ledger, not an isolated relationship service.** The plan carries correlation/operation identity but does not invent a competing durable ledger.
26. **Standalone convenience mutations are a later adapter.** They prepare, clone/stage, persist/verify once, publish committed state, then emit event/notification. They do not call the old immediate-save body.
27. **Failed validation, stale plan, apply failure, or persistence failure leaves prior committed relationship state unchanged or recoverable according to #137.**
28. **Events and notifications happen only after verified commit.** Optional subscriber/presenter failure cannot corrupt the committed relationship result.
29. **The typed event contains previous/new numeric state, target ID, domain, operation/correlation identity, and commit revision.** It contains no player-facing prose.
30. **Notification mapping uses #177 definitions after commit.** Low-level planners/services do not format player copy.
31. **Catalog identities, classification profiles, and localization references follow #183 version/hash/provenance/immutable-query rules.**
32. **The first planner PR may use injected fake identity/policy resolvers in tests.** It does not create a hidden production hard-coded catalog.
33. **No `SaveGameData.cs`, `LocalSaveGameService.cs`, `Bootloader.cs`, scene, UI, Android, narrative content, or caller change belongs in the first planner PR.**
34. **Current raw/void interfaces remain as legacy wrappers only until service integration.** No new one-time consequence caller may use them.
35. **The approved NVS-01 `+5` Valerius affinity value is not changed here.** This specification only ensures it can be planned/applied once inside the later atomic report transaction.
36. **No faction/persona consequence is invented for NVS-01.** Only approved source decisions may add them.
37. **User approval is not inferred from numeric/classification infrastructure.** Final relationship meaning/tone/player experience remains user-gated where required.

## 3. Verified current baseline

### 3.1 Save fields

```text
SaveGameData.Reputation            List<NpcAffinityData>
SaveGameData.FactionReputations    List<FactionRepData>
SaveGameData.LordPersona           PersonaData
```

Rows/fields:

```text
NpcAffinityData.NpcId      string
NpcAffinityData.Affinity   float

FactionRepData.FactionId   string
FactionRepData.Reputation  int

PersonaData.Warlord        int
PersonaData.Diplomat       int
PersonaData.Sage           int
PersonaData.Rogue          int
```

#136/merged save policy approves only top-level omission normalization:

```text
Reputation == null         → empty list
FactionReputations == null → empty list
LordPersona == null        → default object
```

That does not authorize repair of malformed entries, duplicate IDs, non-finite values, or overflowed semantics.

### 3.2 `IReputationService`

```text
float GetAffinity(string npcId)
void ChangeAffinity(string npcId, float delta)
string GetAffinityRank(string npcId)
```

Current `ReputationService`:

- reads the first matching row and can dereference null entries;
- returns `0` for missing/unknown/malformed conditions without validity status;
- creates a row for any supplied string;
- selects first duplicate;
- calculates `Mathf.Clamp(current + delta, -100, 100)` without finite validation;
- saves independently after every mutation;
- returns hard-coded English labels:

```text
>= 80  Exalted
>= 50  Friendly
>= 0   Neutral
>= -50 Hostile
else   Nemesis
```

`NaN` fails every comparison and falls through to `Nemesis`.

### 3.3 `IFactionService`

```text
int GetReputation(string factionId)
void AdjustReputation(string factionId, int delta)
string GetFactionAffiliation(string factionId)
```

Current `FactionService`:

- has the same null/blank/unknown/duplicate/first-row problems;
- creates any supplied faction ID;
- performs unchecked integer addition;
- saves independently;
- returns hard-coded English labels:

```text
>= 500  Ally
>= 100  Supporter
<= -500 Enemy
<= -100 Opponent
else    Neutral
```

### 3.4 `IPersonaService`

```text
int GetTraitValue(PersonaTrait trait)
void AdjustTrait(PersonaTrait trait, int delta)
PersonaTrait GetDominantTrait()
```

Current `PersonaService`:

- performs unchecked integer addition;
- undefined enum performs no switch mutation but still logs/saves;
- saves independently after every adjustment;
- missing object returns `Sage` as dominant;
- valid all-zero/tied state uses dictionary insertion order and returns `Warlord`;
- returns one trait even when no unique dominant trait exists.

### 3.5 Current identity authority

A `FactionDefinition` ScriptableObject shape exists with:

```text
Id
FactionName
ParentRealm
Description
Emblem
```

There is no verified complete versioned runtime NPC/faction/persona policy catalog, immutable identity resolver, source revision, classification profile, or lookup status.

The technical services cannot safely infer identity validity from the mere existence of a string.

### 3.6 Transaction conflict

The approved NVS-01 report completion eventually needs one recoverable operation containing:

```text
+500 Gold
+5 Valerius affinity
quest completion
selected-realm Chapter 1 unlock
other approved report consequences
idempotency ledger/result
one durable persistence boundary
```

Current `ChangeAffinity` saves immediately and cannot participate safely in that operation.

## 4. Domain and identity model

### 4.1 Relationship domains

```text
NpcAffinity
FactionReputation
PersonaTrait
```

### 4.2 Stable IDs

New NPC/faction technical IDs follow the relevant #183 family schema, recommended:

```text
^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$
```

Existing IDs are preserved or migrated only through explicit alias/migration records.

Rules:

- ordinal case-sensitive;
- no trimming/lowercasing/fuzzy/display-name lookup at runtime;
- blank invalid;
- aliases exact/observable;
- unknown future nonblank IDs preserved in raw saves but unsupported by current operations;
- display names are never IDs by implication.

### 4.3 Persona traits

Current technical enum:

```text
Warlord
Diplomat
Sage
Rogue
```

All production operations require `Enum.IsDefined` or equivalent. A catalog/policy record may associate stable content/localization references; the enum alone does not author player meaning.

### 4.4 Identity resolver

Equivalent immutable interface:

```text
IRelationshipIdentityResolver.ResolveNpc(npcId)
IRelationshipIdentityResolver.ResolveFaction(factionId)
IRelationshipPolicyResolver.ResolvePersonaPolicy()
```

Statuses:

```text
Found
AliasResolved
UnknownId
CatalogPending
CatalogUnavailable
InvalidRecord
UnsupportedVersion
```

A production mutation requires `Found` or explicitly supported `AliasResolved`. Alias resolution does not rewrite the save during a query/planning call.

## 5. Policy snapshot

One immutable `RelationshipPolicySnapshot` or equivalent contains:

```text
schemaVersion
contentVersion
sourceRevision
affinityRange
affinityClassificationProfile
factionArithmeticPolicy
factionClassificationProfile
personaArithmeticPolicy
personaClassificationPolicy
identityCatalogRevision
```

### 5.1 Affinity policy

```text
minimum: -100
maximum: 100
zero: neutral/unestablished numeric value
mutation: finite addition then clamp
```

Legacy thresholds retained exactly as numeric boundaries:

```text
[80, 100]
[50, 80)
[0, 50)
[-50, 0)
[-100, -50)
```

The profile supplies stable band IDs and localization/content references. Engineering does not retain `Exalted`, `Friendly`, `Neutral`, `Hostile`, or `Nemesis` as authoritative service literals.

### 5.2 Faction policy

Arithmetic:

```text
signed Int32
checked addition
zero valid
negative valid
no new clamp/range introduced in #176
```

Legacy thresholds retained exactly:

```text
[500, int.MaxValue]
[100, 500)
(-100, 100)
(-500, -100]
[int.MinValue, -500]
```

A profile supplies stable band IDs/localization references. Player labels are not hard-coded in the service.

### 5.3 Persona policy

Arithmetic:

```text
signed Int32 per trait
checked addition
zero valid
negative valid under current compatibility policy
```

Classification:

```text
Unavailable
Malformed
AllZero
UniqueDominant
Tie
```

- `AllZero` returns no unique dominant trait;
- `Tie` returns an immutable sorted set/list of tied traits;
- `UniqueDominant` returns exactly one trait;
- missing object is unavailable/normalized by #136 before supported operation;
- undefined enum never participates;
- labels/meaning resolve from source content.

## 6. Validation result model

### 6.1 Domain validation status

```text
Valid
ValidSparse
CompatibleNormalizedTopLevel
PreservedUnknown
MalformedNullEntry
MalformedBlankId
MalformedDuplicateId
MalformedNonFinite
MalformedOutOfRange
MalformedPolicyUnavailable
UnavailableNoCurrentSave
UnavailableReadOnlyProfile
UnsupportedDefinitionVersion
```

A snapshot can include multiple ordered diagnostics while exposing one worst status.

### 6.2 Diagnostics

Suggested stable codes:

```text
AL-REL-NO-CURRENT-SAVE
AL-REL-PROFILE-READ-ONLY
AL-REL-POLICY
AL-REL-UNKNOWN-ID
AL-REL-BLANK-ID
AL-REL-NULL-ENTRY
AL-REL-DUPLICATE-ID
AL-REL-NONFINITE
AL-REL-OUT-OF-RANGE
AL-REL-OVERFLOW
AL-REL-STALE-PLAN
AL-REL-CORRELATION
AL-REL-APPLY
AL-REL-PERSISTENCE
AL-REL-EVENT-HANDLER
AL-REL-LEGACY-CLASSIFICATION
```

Diagnostic data:

```text
code
severity
domain
recordPath
targetId when safe
field
sourceRevision
action
mutationDisabled
```

No player-facing copy, raw save contents, or local paths.

## 7. Immutable relationship snapshots

### 7.1 Overall snapshot

Equivalent fields:

```text
snapshotRevision
policyRevision
profileWritable
npcAffinityDomain
factionDomain
personaDomain
diagnostics
```

`snapshotRevision` is a deterministic fingerprint/revision over the relevant candidate state and policy revision, not a mutable object reference.

### 7.2 NPC affinity snapshot

```text
status
supportedValuesByCanonicalNpcId
preservedUnknownIds
duplicateIds
sourceRecordCount
diagnostics
```

Rules:

- enumerate without mutation;
- null entry marks domain malformed;
- blank ID marks malformed;
- duplicate supported/alias-resolved canonical ID marks malformed;
- unknown nonblank ID preserved but excluded;
- any supported non-finite/out-of-range value marks domain malformed;
- absent known ID reads as valid sparse zero;
- map/collections immutable and deterministic.

### 7.3 Faction snapshot

Same structural rules:

- null/blank/duplicate supported canonical ID malformed;
- unknown nonblank preserved/excluded;
- absent known ID valid sparse zero;
- stored `int` is structurally finite; future policy bounds validate when introduced;
- immutable deterministic map.

### 7.4 Persona snapshot

```text
status
warlord
diplomat
sage
rogue
classification result
diagnostics
```

- no mutable `PersonaData` reference;
- policy availability validated;
- all-zero/tie represented honestly;
- no save/mutation/classification side effect.

## 8. Query contract

### 8.1 Numeric query status

```text
Available
AvailableSparseZero
AliasResolved
UnavailableNoSave
UnavailableReadOnly
UnavailableUnknownId
UnavailableMalformedDomain
UnavailablePolicy
UnsupportedVersion
```

Query result contains:

```text
status
domain
requestedId
canonicalId
value
snapshotRevision
policyRevision
diagnostics
```

### 8.2 Classification query

Affinity/faction result:

```text
status
classificationId
value
rangeMinimumInclusive
rangeMaximumExclusive/inclusive metadata
contentReference
```

Persona result:

```text
status: UniqueDominant | Tie | AllZero | Unavailable | Malformed
dominantTrait when unique
tiedTraits when tie
maximumValue
contentReference when defined
```

### 8.3 Legacy wrappers

Current methods remain temporarily:

```text
GetAffinity
GetAffinityRank
GetReputation
GetFactionAffiliation
GetTraitValue
GetDominantTrait
```

Rules:

- numeric wrappers return `0` only at the compatibility boundary and emit/return no implication that the domain is valid;
- string label wrappers are obsolete/migration-only and may retain existing output for current callers until source migration, with `AL-REL-LEGACY-CLASSIFICATION` development diagnostic;
- `GetDominantTrait` cannot faithfully represent tie/all-zero. It remains legacy-only and no new caller may use it;
- authoritative callers use typed results;
- wrappers never mutate/save.

## 9. Mutation request and result contracts

### 9.1 Request

Equivalent immutable request:

```text
domain
targetId or personaTrait
delta
correlationId
operationId
sourceSystemId
occurredAtUtc
expectedSnapshotRevision optional
```

Rules:

- target ID/trait validated through policy;
- correlation required for narrative/reward/transaction mutations;
- operation ID stable and bounded;
- source identifies orchestrator/domain, not player copy;
- no notification text/label included.

### 9.2 Preparation status

```text
Prepared
PreparedClamped
NoChange
RejectedNoCurrentSave
RejectedReadOnlyProfile
RejectedUnknownId
RejectedInvalidTrait
RejectedMalformedDomain
RejectedInvalidDelta
RejectedOverflow
RejectedPolicyUnavailable
RejectedCorrelationRequired
RejectedStaleSnapshot
UnsupportedVersion
```

### 9.3 Prepared plan

Equivalent immutable plan:

```text
planId
domain
canonicalTargetId/trait
requestedDelta
previousValue
newValue
appliedDelta
wasClamped
rowOperation: None | Create | Update
expectedSnapshotRevision
policyRevision
correlationId
operationId
sourceSystemId
diagnostics
```

The plan contains no mutable list/row/save reference.

### 9.4 Affinity preparation

For a valid domain/known ID:

1. current = existing row or sparse zero;
2. require finite current within `[-100,100]`;
3. require finite delta;
4. zero delta → `NoChange`;
5. calculate finite `double raw = current + delta`;
6. clamp once to `[-100,100]`;
7. convert to finite `float`;
8. expose exact `appliedDelta = new - current`;
9. `PreparedClamped` when requested and applied delta differ;
10. no mutation/save/event.

A delta whose magnitude causes non-finite `double` rejects.

### 9.5 Faction/persona preparation

1. validate domain/target;
2. zero → `NoChange`;
3. calculate `checked(current + delta)`;
4. overflow/underflow rejects;
5. missing known NPC/faction row may stage `Create` from zero;
6. persona always updates the selected field in a valid object snapshot;
7. no mutation/save/event.

## 10. Plan application seam

### 10.1 Transaction candidate

The authoritative later application operates on an isolated writable candidate/clone supplied by #137/#133, not directly on the published `CurrentSave` before persistence.

Equivalent internal abstraction:

```text
IRelationshipMutationTarget
  GetCurrentSnapshot(...)
  Apply(plan)
```

The target may wrap a cloned `SaveGameData` candidate but does not expose it publicly.

### 10.2 Apply status

```text
Applied
NoChange
RejectedStalePlan
RejectedTargetInvalid
RejectedTargetReadOnly
RejectedCorrelationConflict
RejectedAlreadyApplied
RejectedApplyFailure
```

### 10.3 Stale validation

Before apply:

- revalidate policy/source version;
- rebuild or verify current domain fingerprint;
- require expected snapshot revision;
- require current target value/row operation still matches the plan;
- reject if another mutation changed the domain;
- do not rebase/recalculate silently during apply.

Caller must prepare a new plan.

### 10.4 Apply behavior

- perform one exact create/update/no-op on candidate;
- never delete/merge malformed rows;
- do not save;
- do not emit event/notification;
- record plan/correlation in the owning transaction ledger when supplied;
- return immutable application result.

## 11. Idempotency and transaction ledger

### 11.1 Ownership

The durable operation ledger belongs to the transaction that owns the one-time consequence:

```text
NVS report result
world event result
reward result
standalone relationship operation
```

The relationship planner does not maintain a separate hidden ledger that can diverge.

### 11.2 Ledger semantics

The owning ledger records equivalent:

```text
operationId
correlationId
sourceSystemId
resultType
relationship plan/result summary
committedAtUtc
```

Duplicate operation/correlation:

- same semantic request returns the prior committed result/no new apply;
- conflicting payload rejects visibly;
- reload/retry does not reapply;
- notification/event replay follows #177 correlation rules.

### 11.3 NVS-01 report composition

Expected future order:

```text
validate report transition/session/result ID
→ verify operation not already committed
→ prepare +500 Gold plan through typed economy contract
→ prepare +5 Valerius affinity plan through this contract
→ prepare quest completion/chapter unlock plans
→ apply all plans to one clone/candidate
→ stage operation ledger + notification outbox
→ persist/verify through #137
→ publish candidate/current result
→ emit typed events/notification receipts
```

Failure before durable publish changes no committed domain. Duplicate report after reload returns prior result.

## 12. Standalone mutation adapter

A later convenience API may support non-orchestrated operations.

Equivalent flow:

```text
resolve policy/identity
→ build current immutable snapshot
→ prepare plan
→ clone/stage candidate
→ apply plan
→ persist/verify once through #137
→ publish candidate
→ emit event
→ enqueue notification if mapped
```

Result statuses distinguish:

```text
AppliedCommitted
NoChange
RejectedValidation
RejectedStale
PersistenceFailedPreviousPreserved
NotificationFailedAfterCommit
```

Rules:

- exactly one save attempt for an applied operation;
- zero/rejected operation saves zero times;
- save failure does not leave untracked in-memory mutation as accepted;
- notification failure does not change committed numeric result;
- no broad exception swallowing.

Current `ChangeAffinity`, `AdjustReputation`, and `AdjustTrait` wrappers migrate to this adapter only after #137. Until then they remain legacy and may not be used for one-time NVS consequences.

## 13. Commit events

Equivalent event:

```text
RelationshipCommittedChange
  domain
  canonicalTargetId/trait
  previousValue
  newValue
  appliedDelta
  wasClamped
  operationId
  correlationId
  sourceSystemId
  commitRevision
  committedAtUtc
```

Rules:

- emitted after durable commit/publish only;
- exactly once per committed operation;
- immutable;
- subscriber exceptions isolated/logged with `AL-REL-EVENT-HANDLER`;
- event failure does not roll back commit or prevent later subscribers;
- no player-facing labels/copy;
- duplicate ledger replay does not re-emit unless explicitly replayed through a separate history API.

## 14. Notification mapping

Relationship services/planners do not format player copy.

After a committed operation, the owning presentation/orchestrator may enqueue one #177 request using:

```text
predefined notification definition ID
operation/correlation ID
canonical target ID or content reference
previous/new/applied value parameters when approved
```

Rules:

- definition/content source owns whether a relationship change should be shown;
- no raw NPC/faction internal ID is shown as player copy;
- low-level invalid programmer requests may remain diagnostics only;
- missing notification presenter/content cannot corrupt committed relationship state;
- durable result notification uses the same transaction/outbox when product-required.

## 15. Identity and classification catalogs

### 15.1 NPC record

Equivalent fields:

```text
npcId
legacyAliases
relationshipEnabled
initialAffinity
classificationProfileId
localization/content references
sourceRevision
```

`initialAffinity` for sparse absence is currently `0`. Any nonzero default requires explicit source/migration/product approval.

### 15.2 Faction record

Equivalent fields:

```text
factionId
legacyAliases
relationshipEnabled
initialReputation
classificationProfileId
parentRealm/reference
localization/content references
sourceRevision
```

`initialReputation` is currently `0`.

### 15.3 Persona policy record

Equivalent fields:

```text
supportedTraits
classificationPolicyVersion
allZeroPolicy = NoDominant
 tiePolicy = ReturnTieSet
localization/content references
sourceRevision
```

### 15.4 Classification profile

Equivalent ordered bands:

```text
classificationId
minimum
maximum
minimumInclusive
maximumInclusive
contentReference
```

Validation:

- bands ordered deterministically;
- no overlap/gap across the supported domain unless intentional and reported;
- finite affinity thresholds;
- integer faction thresholds;
- duplicate IDs invalid;
- label/content reference resolves;
- source/hash/version follows #183.

## 16. Save compatibility and malformed-state behavior

### 16.1 Null top-level fields

Handled only by approved #136 normalization:

```text
null list → empty sparse list
null persona → default all-zero object
```

Repeated normalization is idempotent and preserves unrelated fields.

### 16.2 Null/blank/duplicate rows

- preserve raw candidate bytes;
- classify domain malformed;
- return unavailable typed reads;
- reject mutation planning;
- no first/last/merge/delete;
- #137 prefers cleaner backup;
- explicit future repair requires quarantine, diagnostics, clone, validation, durable install, and tests.

### 16.3 Unknown nonblank IDs

- preserve exact row/value;
- exclude from supported map/classification/mutation;
- do not prefer an older backup solely because stable unknown data exists;
- if definition returns later, value becomes available subject to validity and duplicate checks;
- no generic deletion or alias guessing.

### 16.4 Non-finite/out-of-range affinity

- domain malformed;
- never rank/classify;
- never clamp during query/mutation;
- no progress/consequence/notification;
- explicit repair only through #137 after cleaner candidates fail.

### 16.5 Forward schema/read-only

- exact valid numeric values may be exposed read-only when policy permits;
- every mutation rejects `RejectedReadOnlyProfile`;
- no normalization/save/notification claiming application;
- raw data preserved.

## 17. Legacy interface migration

### 17.1 Current void methods

```text
ChangeAffinity
AdjustReputation
AdjustTrait
```

Rules until replaced:

- no new one-time transaction caller;
- mark/document as legacy;
- caller inventory required;
- production integration PR replaces body with standalone typed adapter only after #137;
- invalid/zero operations must eventually save zero times;
- old direct-save behavior cannot be cited as transaction compatibility.

### 17.2 Current classification methods

```text
GetAffinityRank
GetFactionAffiliation
GetDominantTrait
```

Rules:

- migrate callers to typed classification results;
- remove hard-coded labels from technical services after content source exists;
- legacy string wrappers remain temporary, with no new callers;
- dominant wrapper is explicitly lossy for tie/all-zero and cannot be used for product decisions;
- deletion/removal waits for inventory and compatibility review.

## 18. Implementation sequence

### Phase A — this merged specification

No executable/content/save change.

### Phase B — pure validation, snapshots, and planners

Branch:

```text
codex/relationship-contract-planner
```

Prerequisites:

- current `main`;
- #156 accepted or a separately approved non-asset dependency window with canonical validation;
- no overlapping interface/service PR;
- read the merged save/game-data/notification specifications.

Expected scope:

- immutable relationship policy/identity resolver interfaces;
- typed validation/query/classification/request/plan/result/event models;
- pure snapshot builders/validators;
- pure affinity/faction/persona planners;
- stale-plan/fake mutation-target seam;
- complete EditMode tests;
- current caller/interface inventory.

Do not include:

- edits to `ReputationService`, `FactionService`, or `PersonaService` production behavior unless compile-only and declared;
- save/persistence/idempotency ledger;
- catalog/content data;
- notifications/UI;
- caller migration;
- `Bootloader.cs`;
- Android/narrative copy.

### Phase C — identity/policy/content source

After the applicable #183 catalog foundation:

- Codex engineering supplies schema/technical IDs/policies and exact legacy threshold profiles;
- Codex narrative/content supplies NPC/faction/persona meaning and localization references;
- user approves unresolved narrative/product meaning;
- generated artifacts retain version/hash/provenance;
- no balance/threshold drift without explicit decision.

### Phase D — save-backed service integration

Prerequisites:

- corrected #136 merged;
- #137 clone/persist/publish/fault seam accepted;
- Phase B/C accepted;
- no shared-file conflict.

Scope:

- typed service APIs over immutable snapshots/plans;
- standalone transaction adapter;
- legacy wrapper migration;
- commit events;
- save/reload/fault tests;
- no NVS transaction implementation.

### Phase E — durable idempotency/transaction composition

Under #133/#134 and other owning issues:

- owning ledger/result identity;
- plan composition;
- one persistence boundary;
- duplicate reload/retry;
- typed notifications after commit.

## 19. Expected file boundary

Phase B likely adds/changes:

```text
unity/Assets/AL/Scripts/Core/Interfaces/Relationships/**
small additive typed members in IReputationService.cs / IFactionService.cs / IPersonaService.cs only if needed
unity/Assets/AL/Scripts/Services/Relationships/** pure validators/planners
unity/Assets/AL/Tests/EditMode/Relationships/**
unity/Docs/Relationship_Caller_Inventory.md
matching .meta files
```

Phase D may change:

```text
unity/Assets/AL/Scripts/Kingdom/Narrative/ReputationService.cs
unity/Assets/AL/Scripts/Kingdom/Narrative/FactionService.cs
unity/Assets/AL/Scripts/Kingdom/Narrative/PersonaService.cs
focused tests
```

Phase E may change save/transaction files only with explicit locks/specifications.

Prohibited in Phase B:

```text
SaveGameData.cs
LocalSaveGameService.cs
Bootloader.cs
LocalGameDataService.cs
three production relationship service bodies by default
narrative/content/localization source
Android
scenes/Build Settings
reward values/NVS implementation
notifications/presenter
```

## 20. Required tests

### 20.1 Policy/identity validation

- valid NPC/faction/persona policy;
- blank/duplicate ID;
- alias success/collision/cycle/shadowing;
- catalog pending/unavailable/invalid/unsupported;
- invalid affinity range/threshold overlap/gap;
- exact legacy affinity/faction thresholds;
- invalid persona supported trait set;
- deterministic diagnostics/order;
- no player-facing label required by technical validator.

### 20.2 Affinity snapshot/query

- null top-level normalized fixture;
- empty sparse list;
- known missing ID → available sparse zero;
- valid row at every boundary;
- null row;
- blank ID;
- unknown nonblank preserved/excluded;
- duplicate supported/alias canonical ID;
- NaN/+∞/-∞;
- finite below -100/above 100;
- repeated query pure and immutable;
- typed classification exact boundaries;
- legacy string wrapper behavior inventoried only.

### 20.3 Affinity planning

- positive/negative/zero delta;
- exact min/max;
- clamp upper/lower with requested/applied delta;
- current non-finite/out-of-range rejects;
- delta NaN/+∞/-∞ rejects;
- extremely large finite delta produces bounded finite result;
- unknown/blank/malformed domain rejects;
- sparse known row stages create from zero;
- correlation required for transaction source;
- no mutation/save/event;
- plan immutable.

### 20.4 Faction snapshot/planning

- known missing sparse zero;
- valid positive/negative/zero;
- null/blank/unknown/duplicate rows;
- exact `int.MaxValue`/`int.MinValue` boundary;
- overflow/underflow rejects;
- classification threshold edges ±100/±500;
- no clamp/new bound;
- no mutation/save/event.

### 20.5 Persona snapshot/classification/planning

- valid each trait;
- undefined enum;
- positive/negative/zero delta;
- max/min boundary;
- overflow/underflow rejects;
- missing object fixture before/after #136 normalization;
- all zero → `AllZero` no dominant;
- unique maximum each trait;
- two-/three-/four-way tie returns deterministic sorted tie set;
- negative tied/unique values;
- repeated classification pure;
- legacy dominant wrapper explicitly lossy/inventoried;
- no mutation/save/event.

### 20.6 Stale/apply seam

- valid plan applies to fake candidate;
- stale revision rejects;
- changed value/row operation rejects;
- policy revision changed rejects;
- duplicate operation returns already applied through fake ledger;
- correlation conflict rejects;
- apply failure leaves candidate unchanged;
- malformed target rejects;
- no save/event/notification in planner/applier.

### 20.7 Service integration phase

- standalone valid mutation persists once;
- zero/rejected saves zero times;
- save failure rolls back/preserves prior state;
- reload preserves exact values;
- commit event once after persistence;
- event subscriber failure isolated;
- notification enqueue failure separate from committed result;
- unknown/future rows preserved;
- no first/merge/delete repair;
- legacy wrappers map correctly until removed.

### 20.8 Idempotency/transaction integration

- prepare affinity + fake economy + quest + unlock plans;
- failure before each apply/persist/publish boundary;
- one atomic successful commit;
- duplicate correlation in same session;
- duplicate after reload;
- conflicting duplicate;
- notification outbox once;
- event once;
- approved +5 Valerius exact once;
- no unapproved faction/persona consequence.

### 20.9 Content/ownership

- technical service contains no authoritative English rank/affiliation labels after migration;
- classification content references resolve;
- missing key returns typed unavailable/fallback through #177;
- no raw internal ID shown;
- no narrative source change in engineering PR;
- exact thresholds/amounts unchanged.

## 21. Canonical validation

Phase B:

```powershell
$repo = "C:\Users\MY\Documents\AnotherLife"
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"

& $unity -batchmode -quit -nographics `
  -projectPath "$repo\unity" `
  -logFile "$repo\unity\Logs\RelationshipPlannerCompile.log"

& $unity -batchmode -nographics `
  -projectPath "$repo\unity" `
  -runTests -testPlatform EditMode -assemblyNames AL.EditMode.Tests `
  -testResults "$repo\unity\Logs\RelationshipPlannerEditMode.xml" `
  -logFile "$repo\unity\Logs\RelationshipPlannerEditMode.log"
```

Later integration additionally runs:

- corrected #127 PlayMode suite;
- save/reload/fault/idempotency tests through #137;
- notification/event integration;
- relevant Player/NVS tests.

Report:

- exact base/head SHA;
- changed files and lock state;
- policy/catalog source versions;
- current interface/caller inventory;
- every snapshot/query/classification/plan/stale/apply test row;
- focused/complete EditMode totals/XML;
- PlayMode/integration applicability;
- no save/service/caller/content mutation in Phase B;
- exact legacy threshold/value preservation;
- final `git diff --check origin/main...HEAD`;
- final repository status;
- every blocked/unperformed check.

Duplicate-workspace, exit `199`, missing XML, hard-coded label claimed as source acceptance, or immediate-save wrapper cited as transaction proof is blocked validation.

## 22. Acceptance criteria

- [ ] NPC/faction/persona identities and policies are immutable, versioned, and strictly validated.
- [ ] Blank/unknown/null/duplicate relationship data cannot crash or mutate ambiguously.
- [ ] Unknown stable IDs are preserved but excluded from unsupported operations.
- [ ] Affinity rejects non-finite/out-of-range current data and finite-invalid deltas.
- [ ] Valid affinity preserves `[-100,100]` clamp semantics with requested/applied delta observability.
- [ ] Faction/persona checked arithmetic cannot wrap.
- [ ] Zero/invalid operations save/event/notify zero times.
- [ ] Query/classification is pure and returns immutable typed results.
- [ ] Player-facing labels leave technical services and resolve from approved content.
- [ ] Persona all-zero/tie/missing states are honest and do not invent a unique trait.
- [ ] Pure prepared plans are save-free, reference-free, revision-bound, and stale-safe.
- [ ] Later apply/standalone adapters use clone → apply → persist/verify → publish and one save boundary.
- [ ] Durable idempotency belongs to the owning transaction ledger and duplicate replay cannot repeat consequences.
- [ ] Commit events/notifications occur exactly once after verified commit and cannot corrupt state on subscriber/delivery failure.
- [ ] Approved +5 Valerius affinity can compose atomically without nested save or value change.
- [ ] The first planner PR edits no saves, production service bodies, callers, content, scenes, Android, or shared files.
- [ ] Canonical compile and complete/focused tests pass with exact evidence.
- [ ] No unapproved amount, threshold, narrative, balance, Android, NVS implementation, or unrelated change is included.

## 23. Codex handoff

```text
Codex engineering: implement only Phase B of issue #176 from current main using unity/Docs/Relationship_Integrity_Transaction_Spec.md. Create codex/relationship-contract-planner. Add immutable relationship identity/policy resolver interfaces, typed validation/query/classification/request/plan/apply-result/event models, pure affinity/faction/persona snapshot builders and planners, an injected revision/fake mutation-target seam, current caller/interface inventory, and the complete EditMode matrix. Preserve existing numeric thresholds/ranges exactly, return honest persona tie/all-zero states, and perform no mutation/save/event/notification. Do not edit SaveGameData.cs, LocalSaveGameService.cs, Bootloader.cs, LocalGameDataService.cs, scenes, Android, narrative/localization content, current service bodies, callers, or NVS reward values. Run canonical Unity validation and return one focused draft PR for Codex coordination/review.
```
