# Territory Ownership, Capture, and Passive-Income Integrity Specification

**Status:** Binding GPT technical specification for issue #166  
**Status date:** 2026-07-16  
**Audited base:** `cfe2c54159063786ba4306539627113d205d4a2f`  
**Primary implementation owner:** Codex engineering  
**Specification/review owner:** GPT  
**Territory display names, descriptions, and localization owner:** Codex narrative/content  
**World-map and terrestrial presentation fidelity owner:** Codex terrestrial-design where applicable  
**Final balance, progression feel, product, playtest, and release approval:** User  
**Canonical Unity workspace:** `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity`

## 1. Purpose

This specification defines the authoritative technical boundary for territory definitions, persisted ownership, capture transitions, capture rewards, passive income, initialization, compatibility, recovery, events, notifications, and consumer integration.

It replaces the current implicit model:

```text
query territory list
→ if null/empty, mutate live save and create T1–T5
→ expose mutable TerritoryData rows containing definition + state
→ caller supplies territory string and arbitrary RealmId
→ select first matching row
→ overwrite owner
→ emit event before persistence
→ try quest progress and +100 credits inside one swallowed exception
→ save independently
```

and the current income model:

```text
query/seeding side effect
→ filter live persisted rows by selected realm and resource enum
→ unchecked Sum(saved BonusAmount)
→ LocalResourceService calls the query six times per production tick
→ divide by 60 and feed mutable economy state
```

with:

```text
validated immutable territory catalog snapshot
+ immutable non-mutating saved-state compatibility snapshot
+ committed profile/realm/session authorization
→ pure immutable query or capture request
→ stale-safe checked ownership/reward/application plan
→ one candidate save transaction
→ persist and verify
→ publish committed ownership/result snapshot
→ emit typed event and notification once
```

This specification does **not**:

- change any current territory ID, initial owner, resource bonus, amount, fortress flag, or capture reward;
- author territory names, lore, descriptions, map art, or narrative consequences;
- implement battle, multiplayer, spawning, world-atlas redesign, or NVS-01;
- authorize an instant production capture button;
- repair save files through ordinary reads;
- modify production services before their dependencies are accepted;
- change Android, scenes, Build Settings, or Player profiles.

## 2. Binding dependencies and phase authorization

### 2.1 Related contracts

Territory integrity consumes rather than duplicates:

```text
unity/Docs/Game_Data_Catalog_Authority_Spec.md
unity/Docs/Save_Semantic_Compatibility_Policy.md
unity/Docs/Economy_Integrity_Spec.md
unity/Docs/Progression_Definition_Order_Transaction_Integrity_Spec.md
unity/Docs/Notification_Delivery_Contract_Spec.md
unity/Docs/World_State_Lifecycle_Transaction_Spec.md
unity/Docs/Battle_Computation_Result_Transaction_Spec.md
unity/Docs/Production_Scene_Player_Build_Spec.md
```

If the exact progression document filename differs, merged PR #238 remains the binding #165 contract.

### 2.2 Dependency sequence

```text
pure territory definition/state/query/capture/income planners
          ↓
#156 trusted Unity asset baseline
          +
#183 versioned territory definition authority
          +
corrected #163 typed no-save economy operations
          +
corrected #152 typed no-save quest operations
          +
accepted #137 candidate persistence, semantic validation, ledger, and outbox
          +
accepted #165 progression consumer snapshots where prerequisites apply
          ↓
production territory service and save migration
          +
corrected #153 lifecycle/reconciliation ownership
          +
corrected #178 non-mutating Kingdom controller and release reachability
          ↓
#163 live/offline production consumes one validated territory-income snapshot
#174 battle/command result may authorize a capture through a focused handoff
#172 world-state effects may consume/modify income only through approved effect descriptors
          ↓
#223/#150 Kingdom scene/Player evidence
          +
corrected #127 profile-safe PlayMode evidence
          +
user integrated acceptance
```

### 2.3 First phase may proceed now

The first Codex engineering phase is pure and nonmutating:

```text
codex/territory-contract-planner
```

It may implement immutable models, current-source inventory, validators, compatibility snapshots, capture/income planners, fake no-save dependencies, deterministic diagnostics, and focused EditMode tests.

It must not edit or change production behavior in:

```text
unity/Assets/AL/Scripts/Core/Interfaces/ITerritoryService.cs
unity/Assets/AL/Scripts/RealmWar/Warzone/WarzoneService.cs
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalSaveGameService.cs
unity/Assets/AL/Scripts/Services/Local/LocalResourceService.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/Core/Bootloader.cs
unity/Assets/AL/Scripts/UI/Kingdom/KingdomSceneController.cs
catalogs, schemas, authored content, maps, scenes, Android, or Build Settings
```

The first phase performs no save mutation, ownership change, resource/credit mutation, quest progress, event, notification, catalog switch, or UI change.

## 3. Verified current-source baseline

### 3.1 Definition and state are conflated

Current `TerritoryData` contains both source-like and profile-state fields:

```text
Id
Name
OwnerRealm
BonusType
BonusAmount
IsFortress
```

Consequences:

- display name and balance-like bonus data are duplicated into every profile;
- saved values can drift from future catalog authority;
- a downgrade cannot distinguish a future definition from malformed current data;
- ownership mutation occurs on the same live object that carries definition fields;
- consumers cannot report catalog/version/hash/provenance;
- a first/duplicate row can silently redefine bonus, owner, name, and fortress status.

### 3.2 Reads seed live profile data

`WarzoneService.Territories` calls `EnsureTerritories()`. `GetTerritories()` and `CalculatePassiveIncome()` therefore create state when the list is null or empty.

The current embedded baseline is:

| ID | Current display name | Initial owner | Bonus resource | Bonus amount | Current income unit | Fortress |
| --- | --- | --- | --- | ---: | --- | --- |
| `T1` | `Iron Peaks` | `Stonehold` | `Stone` | 50 | units/minute | yes |
| `T2` | `Silver Woods` | `Eldergrove` | `Wood` | 40 | units/minute | no |
| `T3` | `Golden Plains` | `Crownlands` | `Gold` | 20 | units/minute | no |
| `T4` | `Shadow Vale` | `Umbral` | `Food` | 60 | units/minute | yes |
| `T5` | `Neutral Borderlands` | `None` | `Gold` | 10 | units/minute | no |

The units-per-minute interpretation is proven by `LocalResourceService`, which multiplies `CalculatePassiveIncome(...)` by `deltaSeconds / 60.0`.

This table is migration inventory, not permission to change balance or player-facing names.

### 3.3 Empty state is ambiguous

The service treats every empty list as a new profile and inserts T1–T5. It cannot distinguish:

- a newly created profile requiring one approved initialization;
- an old profile that has never opened territory UI;
- a truncated/corrupt primary;
- a richer backup that still contains territory history;
- a future profile whose catalog intentionally has no current territories;
- a deliberately unavailable domain;
- a downgrade where only unknown future territories existed and were lost elsewhere.

Read-time insertion can make a materially incomplete candidate appear valid and can prevent #137 from selecting a richer backup.

### 3.4 Null and duplicate rows are unsafe

Current lookups use:

```text
Territories?.FirstOrDefault(t => t.Id == territoryId)
```

A null row can throw. Duplicate IDs silently select the first record. Query order decides which owner/bonus/name/fortress value is used.

### 3.5 Capture validates neither identity nor authorization

Current `CaptureTerritory(string territoryId, RealmId capturer)` does not reject:

- null/blank/whitespace territory ID;
- unknown current ID;
- preserved unknown future ID;
- `RealmId.None`;
- undefined enum values;
- capturer different from the committed profile realm;
- same-owner capture;
- stale expected owner/revision;
- malformed/duplicate territory state;
- unavailable catalog;
- absent capture authorization/prerequisite;
- duplicate operation delivery.

A caller-controlled enum becomes authoritative ownership.

### 3.6 Same-owner capture farms rewards

Every matching call executes:

```text
OwnerRealm = capturer
OnTerritoryCaptured
quest CaptureTerritory +1
Warzone Credits +100
Save()
```

There is no ownership-transition check. Repeating the same command can grant unlimited credits and quest progress.

The current `100` credit value and `+1` capture-quest progress are preserved by this specification but become one versioned reward profile applied exactly once per committed ownership transition.

### 3.7 Events publish before durable persistence

`OnTerritoryCaptured` fires immediately after the live owner field changes, before quest/credit side effects and before save.

A subscriber can observe or act on a transition that later fails to persist. Event payload includes only territory ID and new owner; it omits:

- previous owner;
- ownership revision;
- capture operation/result ID;
- definition/catalog identity;
- reward/progress receipt;
- persistence status;
- failure/rollback state.

Subscriber exceptions can interrupt the remaining operation because event invocation is not isolated.

### 3.8 Partial reward failure is swallowed

Quest progress and credit award are inside one broad `try/catch`. Examples:

```text
quest succeeds
→ credit throws
→ exception swallowed
→ owner remains changed
→ save still runs

quest service missing
→ exception swallowed
→ no credit attempted
→ owner saved without reward
```

The caller receives `void` and cannot distinguish success, no-op, partial failure, missing dependency, or persistence failure.

Current quest/credit methods have no shared capture operation ID and may mutate live state independently. A reload/retry can repeat whichever side effect was not durably identified.

### 3.9 Save failure does not restore live state

Current code mutates owner, invokes event, attempts rewards, then calls `Save()`. Current `Save()` reports failure through service status rather than throwing, but the territory method does not inspect it or restore the previous owner/reward state.

A failed persistence attempt can leave live memory ahead of disk while the `void` method returns normally.

### 3.10 Passive income is unchecked and impure

Current income:

```text
GetTerritories()
.Where(owner == selected realm && bonus type == requested type)
.Sum(saved BonusAmount)
```

Risks:

- null rows throw;
- duplicate IDs multiply income;
- negative bonuses subtract from production;
- invalid/undefined resource enums can participate;
- unsupported/undefined owner enums can compare;
- overflow is not represented by a typed result;
- unknown future records can affect current balance;
- calls seed live save state;
- returned amount has no catalog/state revision or unit;
- six repeated queries can observe different mutable state during one production tick.

### 3.11 Production consumes six separate live queries

`LocalResourceService.AddTerritoryIncome()` calls passive income separately for:

```text
Food
Wood
Stone
Gold
ManaStone
Ore
```

It catches only failure to resolve the territory service, not malformed territory results. There is no single immutable income snapshot or source revision for the tick.

A mid-tick ownership/state change can produce a mixed snapshot across resources.

### 3.12 Save normalization destroys territory evidence

Current `EnsureSaveDefaults()` removes null territory rows before semantic validation. `ValidateSaveSemantics()` then checks only that the list is non-null.

This can:

- erase evidence of a malformed candidate;
- make an incomplete primary appear equivalent to a clean state;
- prevent richer-backup ranking;
- hide the exact domain requiring repair;
- conflict with the non-repairing compatibility policy.

### 3.13 Current save has no territory metadata or ledger

There is no persisted:

```text
territory state schema version
catalog set/content/hash identity
initialization state
ownership revision per territory
last capture operation/result identity
capture reward ledger
migration version/status
```

The service cannot distinguish initialized, legacy, ambiguous, future, corrupted, or replayed state.

### 3.14 Release UI must remain read-only

The corrected #178 controller policy removed production command reachability but the current dashboard still calls `GetTerritories()`, which seeds state. The initial ShellFoundation Player includes Kingdom, so territory display must become a pure immutable query or an explicit unavailable state before that build can be accepted.

Any future capture command must render from a typed committed result. It must not substitute Crownlands when no realm is committed or display success from a `void` call.

## 4. Authority and ownership

### 4.1 Codex engineering

Owns:

- technical territory catalog envelope and stable IDs;
- immutable definition/state/query/plan/result contracts;
- validation, compatibility, migration mechanics, arithmetic, clocks, revisions, and operation IDs;
- candidate persistence, ledger, event, notification, and deletion integration;
- service, production, battle-result, UI adapter, tests, tools, and evidence.

### 4.2 Codex narrative/content

Owns:

- territory display names and descriptions;
- localization keys;
- fortress/faction/world meaning presented to the player;
- player-facing capture/income/unavailable/failure copy;
- authored narrative consequences and content references.

Technical services return stable IDs, content keys, amounts, statuses, and diagnostics—not hard-coded display text.

### 4.3 Codex terrestrial-design

Owns, where applicable:

- map/environment/silhouette/material presentation source;
- visual territory landmarks and fidelity;
- later fidelity review of engineering integration.

This specification does not create or approve map art.

### 4.4 User

Retains final approval of:

- territory/reward/income balance changes;
- capture gameplay feel and authorization flow;
- destructive migration/repair decisions;
- world-map/player-facing integration;
- playtest, milestone, and release acceptance.

## 5. Terminology

### 5.1 Territory definition

Immutable source record describing stable technical identity, initial owner policy, allowed owners, capture policy, bonus profile, fortress flag, content keys, and version/hash provenance.

### 5.2 Raw persisted territory state

Exact save evidence for ownership and legacy fields before compatibility resolution or repair.

### 5.3 Territory compatibility snapshot

Immutable nonmutating classification of the complete raw territory domain against one catalog and save-policy revision.

### 5.4 Effective territory snapshot

Immutable current-query view combining a validated definition with a valid ownership state. It never writes missing rows.

### 5.5 Ownership revision

Monotonically increasing checked integer attached to one territory state. Every committed owner transition increments it exactly once.

### 5.6 Capture authorization

Typed evidence from the owning game rule/result that the committed profile/session may request the owner transition. A UI button, caller-supplied realm enum, or territory ID alone is not authorization.

### 5.7 Capture operation ID

Stable idempotency identity for one requested ownership transition and its complete reward/quest/event/outbox result.

### 5.8 Capture plan

Immutable stale-safe plan containing previous/new ownership, revision, reward/progress operations, candidate-state changes, ledger/outbox entries, and expected source/save revisions.

### 5.9 Passive-income snapshot

Immutable checked totals for every supported resource computed from one catalog/state/profile revision at one logical query point.

### 5.10 Preserved unknown territory

A nonblank stable territory record not defined by the current supported catalog. It remains in raw compatibility data but contributes no capture, income, quest, event, or reward behavior until its definition is available.

## 6. Territory catalog contract

### 6.1 #183 envelope

The territory artifact participates in the versioned game-data catalog set:

```text
gameId
catalogSetId
catalogId = world_territories
familyId = territories
schemaVersion
contentVersion
sourceRevision
rawSha256
requiredness
packagedRelativePath
```

No service-local fallback is production authority.

### 6.2 Definition shape

Conceptual immutable record:

```csharp
public sealed class TerritoryDefinition
{
    public string TerritoryId { get; }
    public string DisplayNameKey { get; }
    public string DescriptionKey { get; }
    public bool IsFortress { get; }
    public RealmId InitialOwner { get; }
    public IReadOnlySet<RealmId> AllowedOwners { get; }
    public TerritoryCapturePolicy CapturePolicy { get; }
    public TerritoryBonusDefinition Bonus { get; }
    public string CaptureRewardProfileId { get; }
    public IReadOnlyList<string> PrerequisiteIds { get; }
    public IReadOnlyList<string> RequiredCapabilityIds { get; }
}
```

### 6.3 Bonus shape

```text
bonusProfileId
resourceType
nonnegative checked amount
unit = units_per_minute for current migration
appliesWhenOwned
optional effect-consumer IDs for later #172 integration
```

The current baseline uses one resource bonus per territory. Multiple future bonuses require a versioned schema extension and balance approval.

### 6.4 Capture reward profile

Current migration profile:

```text
rewardProfileId: territory_capture_current_v1
Warzone Credits: 100
quest progress: CaptureTerritory +1
```

This freezes current behavior; it does not authorize changing it.

The reward profile is source identity, not caller input. Callers cannot supply arbitrary credit/progress amounts.

### 6.5 Initial baseline inventory

The first approved catalog migration must preserve exactly:

```text
T1 initial owner Stonehold, Stone 50/minute, fortress true
T2 initial owner Eldergrove, Wood 40/minute, fortress false
T3 initial owner Crownlands, Gold 20/minute, fortress false
T4 initial owner Umbral, Food 60/minute, fortress true
T5 initial owner None, Gold 10/minute, fortress false
```

Display strings move to content keys without changing their approved visible meaning unless separately reviewed.

### 6.6 Definition validation

Reject:

- null record;
- blank/duplicate/invalid ID;
- invalid content key;
- undefined initial/allowed owner enum;
- `None` missing where neutral ownership is valid or present where forbidden;
- no allowed capture owner;
- negative or overflowing bonus;
- undefined/unsupported bonus resource;
- missing/duplicate reward profile;
- invalid prerequisite/capability reference;
- alias cycle/collision;
- unsupported schema/content version;
- non-deterministic ordering;
- raw hash/provenance mismatch.

Definitions are immutable and defensively copied.

## 7. Stable identity and aliases

### 7.1 ID policy

Territory IDs are:

- non-null/nonblank;
- case-sensitive ordinal technical identities;
- stable across versions;
- never derived from display names, list order, map coordinates, enum values, or Unity object names;
- length/encoding constrained by the shared catalog policy.

### 7.2 Alias records

Explicit alias:

```text
oldTerritoryId
newTerritoryId
introducedIn
retiredIn
reasonCode
requiresOwnershipMigration
```

No lowercasing, trimming into another ID, fuzzy name match, first-record selection, or nearest-map match is allowed.

### 7.3 Removed definitions

A removed known territory state is preserved as unsupported until an explicit migration/retirement decision states whether ownership history is archived, aliased, or removed. Ordinary queries do not delete it.

## 8. Persisted ownership state

### 8.1 Future normalized shape

After corrected #137 and a declared `SaveGameData.cs` lock, use equivalent durable fields:

```text
territoryStateSchemaVersion
territoryInitializationState
lastValidatedCatalogSetId/contentVersion/rawSha256
territory states[]:
  territoryId
  ownerRealm
  ownershipRevision
  lastCommittedCaptureOperationId optional provenance
lastAppliedTerritoryMigrationVersion
```

Definition-owned name, bonus, amount, unit, and fortress data no longer act as mutable saved authority.

### 8.2 Current legacy row preservation

Current `TerritoryData` rows remain raw migration evidence. A compatibility validator reads them without mutation and compares known fields with the selected catalog.

Legacy definition-field mismatch is reported explicitly. It is not silently copied into current authority or overwritten through a read.

### 8.3 Ownership validity

Owner is valid when:

- enum value is defined;
- it is allowed by the territory definition;
- `RealmId.None` is used only where neutral ownership is permitted;
- no duplicate territory state makes owner ambiguous.

### 8.4 Revision

New initialized states begin at revision `0`. A committed actual transition produces revision `checked(previous + 1)`. Same-owner/no-op does not increment.

Revision overflow blocks the transition and requires recovery; it does not wrap.

## 9. Initialization and legacy-empty policy

### 9.1 New profiles

A new profile created after the new schema exists receives the validated baseline ownership states exactly once during profile candidate creation—never from a query.

The candidate records:

```text
territoryInitializationState = InitializedFromCatalog
catalog identity/hash
initialization migration/operation ID
```

Creation persists and verifies through #137 before publication.

### 9.2 Valid initialized profile

A valid initialized list is never reseeded because it is empty, partially loaded, or temporarily unavailable. Its state and metadata are validated.

### 9.3 Legacy nonempty profile

Known rows are classified against definitions. Unknown stable IDs are preserved. Null/blank/duplicate/mismatched rows remain evidence and can make the domain unavailable pending candidate migration/repair.

### 9.4 Legacy empty profile without metadata

An old empty list is `LegacyAmbiguousEmpty`, not automatically “new”. It might represent uninitialized legacy state or lost data.

Required handling:

1. preserve raw candidate;
2. inspect primary/backup richness under #137;
3. prefer a valid richer candidate when available;
4. record exact diagnosis;
5. apply an approved one-time baseline initialization only in a candidate migration with a durable migration ID;
6. never seed during dashboard, income, or capture queries.

### 9.5 Future intentionally empty/unavailable profile

A future schema/catalog may explicitly declare no active territories. That state requires versioned metadata and is not converted to T1–T5 by current code.

## 10. Compatibility validation

### 10.1 Domain status

```text
Valid
ValidLegacyNeedsMetadata
LegacyAmbiguousEmpty
CatalogPending
CatalogUnavailable
FutureSchemaUnsupported
MalformedNullRow
MalformedBlankId
MalformedDuplicateId
MalformedOwner
MalformedDefinitionSnapshot
PreservedUnknownPresent
IncompleteKnownSet
```

The snapshot can carry multiple diagnostics while one deterministic overall availability state controls mutation/income.

### 10.2 Non-repairing behavior

Validation/query does not:

- create a list or row;
- remove nulls;
- select first/max/latest duplicate;
- change owner;
- copy catalog bonus/name/fortress into save;
- delete unknown future state;
- seed missing known territories;
- save;
- emit events or notifications.

### 10.3 Duplicate groups

Every exact duplicate-ID group is disabled as a group. No row contributes ownership or income and no capture targets it until an explicit repair candidate resolves the group.

### 10.4 Null and blank rows

They remain raw candidate evidence for #137 richness/repair diagnostics. They never crash or contribute behavior.

### 10.5 Unknown future rows

Nonblank unknown IDs are preserved and listed as unsupported. They do not contribute current income or accept capture requests.

### 10.6 Incomplete known set

Missing required known states make the domain incomplete unless the catalog explicitly marks those definitions optional or a migration plan is being prepared. Ordinary reads do not add them.

## 11. Immutable query contract

### 11.1 Query result

Conceptual shape:

```csharp
public sealed class TerritoryQueryResult
{
    public TerritoryQueryStatus Status { get; }
    public CatalogIdentity Catalog { get; }
    public string StateRevisionHash { get; }
    public RealmId CommittedProfileRealm { get; }
    public IReadOnlyList<TerritorySnapshot> Territories { get; }
    public IReadOnlyList<TerritoryDiagnostic> Diagnostics { get; }
}
```

### 11.2 Snapshot

```text
territoryId
content keys
owner realm
ownership revision
bonus resource/amount/unit
fortress flag
capture availability/status
source catalog identity
```

All values are immutable copies. No `TerritoryData` backing object is exposed.

### 11.3 Pure semantics

Queries do not initialize, repair, capture, save, apply income, emit events, or format player-facing text.

### 11.4 Availability

A malformed required domain returns explicit unavailable status and diagnostics. UI may show a safe unavailable state; it must not present a synthesized normal territory list.

## 12. Capturer and authorization contract

### 12.1 Committed realm authority

The authoritative capturer realm comes from the committed valid profile/session realm, not an arbitrary caller parameter.

A request can include `expectedCapturerRealm` for stale/mismatch checking. It cannot override the profile.

Reject:

- no committed realm;
- `RealmId.None`;
- undefined enum;
- profile realm mismatch;
- realm not allowed by definition;
- stale profile/session identity.

### 12.2 Capture authorization

A capture request requires a typed authorization/result from its owning rule, for example a validated battle/command outcome:

```text
authorizationId
authorizationType/source
profile/session identity
territoryId
allowed previous owner/revision
capturer realm
source result identity/hash
expiry or one-use policy when applicable
```

This does not invent multiplayer infrastructure. It prevents a raw UI method call from being treated as proof of capture.

### 12.3 Initial containment

Until an approved capture producer exists, production capture commands remain unavailable under #178. The pure planner uses fake authorizations for tests.

## 13. Capture request and result

### 13.1 Request

```text
captureOperationId
territoryId
expectedPreviousOwner
expectedOwnershipRevision
expectedCatalog identity/hash
expected territory-state revision/hash
profile/session identity
expected capturer realm
capture authorization
```

### 13.2 Status

```text
Committed
AlreadyCommittedReplay
NoChangeSameOwner
RejectedBlankId
RejectedUnknownTerritory
RejectedPreservedUnknown
RejectedDomainMalformed
RejectedCatalogUnavailable
RejectedNoCommittedRealm
RejectedInvalidCapturer
RejectedUnauthorized
RejectedStaleOwner
RejectedStaleRevision
RejectedDependencyUnavailable
RejectedRewardPlan
RejectedQuestPlan
RejectedOverflow
RejectedPersistence
CommitUncertain
CorrelationConflict
```

### 13.3 Result payload

```text
captureOperationId
territoryId
previousOwner
newOwner
previousRevision
newRevision
catalog/state identity
reward profile and typed economy receipt
quest progress receipt
persistence receipt/status
committed event/outbox identity
no-op/rejection diagnostics
```

The result contains no mutable save row.

## 14. Capture validation rules

Before any mutation:

1. validate operation ID and request shape;
2. validate catalog and state snapshot;
3. resolve exact territory definition and one valid state;
4. validate committed profile/session and capturer;
5. validate capture authorization;
6. compare expected previous owner and revision;
7. detect exact replay/correlation conflict;
8. compare current/new owner;
9. validate reward and quest dependency availability;
10. prepare checked no-save economy and quest operations;
11. prepare candidate ownership/revision/ledger/outbox changes;
12. validate complete candidate semantics;
13. return one immutable plan.

### 14.1 Same-owner request

Returns `NoChangeSameOwner` when not an exact committed replay.

It performs exactly:

```text
0 ownership mutation
0 revision increment
0 Warzone Credits
0 quest progress
0 save
0 event
0 notification
```

### 14.2 Neutral and hostile transitions

Both can be valid only when allowed by definition and authorized by the owning capture producer. They use the same transaction/idempotency path.

### 14.3 Invalid state

Malformed/duplicate/incomplete domain rejects before ownership or reward planning. No first-row fallback.

## 15. Capture reward and quest transaction

### 15.1 Required dependencies

The current reward profile requires both:

```text
typed no-save Warzone Credit +100 operation

typed no-save quest progress CaptureTerritory +1 operation
```

If either required dependency is unavailable or rejects, the capture plan rejects before state mutation. The implementation does not silently capture without the current promised reward/progress.

A future reward profile may explicitly omit a component, but that is a versioned source/balance decision.

### 15.2 No nested persistence

Economy and quest operations apply to the same candidate save or transaction target and save zero times themselves.

### 15.3 Candidate changes

One candidate contains:

```text
territory owner/revision update
Warzone Credit delta
quest progress delta
capture operation/result ledger entry
notification outbox entry when required
last validated catalog/migration metadata
```

### 15.4 Commit order

```text
prepare complete plan
→ clone current validated candidate
→ apply all ownership/reward/quest/ledger/outbox changes to clone
→ validate complete candidate
→ persist and verify through #137
→ publish committed save/state snapshot
→ emit typed event and notification receipt once
```

### 15.5 No broad exception swallowing

Every dependency/application/persistence failure returns a typed phase/code. Exceptions are caught only at defined boundaries and never converted into success.

## 16. Idempotency, concurrency, and recovery

### 16.1 Exact replay

Same operation ID + same semantic request/plan hash returns the persisted/committed receipt with no mutation, save, event, or notification.

### 16.2 Correlation conflict

Reuse of operation ID with different territory, owner, revision, capturer, authorization, reward profile, catalog, or plan hash rejects.

### 16.3 Stale request

Owner or revision mismatch rejects before mutation. The service does not silently rebase a hostile/neutral transition onto new state.

### 16.4 Concurrent requests

Only one plan whose expected candidate/state revision remains current can commit. Others become stale or exact replay.

### 16.5 Persistence failure

Prior committed state remains authoritative. Candidate changes are not published. No event/notification success is emitted.

### 16.6 Commit uncertainty

Reconcile by capture operation ID and candidate/ledger identity before retry. A blind retry is prohibited.

### 16.7 Reload

After reload, the ledger and ownership revision prove whether the operation committed. Replayed delivery cannot repeat credits or quest progress.

## 17. Committed event and notification

### 17.1 Event

After durable commit:

```text
TerritoryCaptureCommittedEvent
  captureOperationId
  territoryId
  previousOwner
  newOwner
  previousRevision
  newRevision
  catalog/state identity
  reward/quest receipt identity
```

Subscriber failures are isolated and reported; they do not roll back a durable capture.

### 17.2 Notification

A typed #177 notification request is staged in the same candidate outbox when policy requires.

Player-facing text resolves after commit from content/localization keys. Console logging does not count as visible delivery.

### 17.3 No-op/rejection

No success event/notification is emitted for same-owner, stale, unauthorized, malformed, or failed requests. UI renders the typed result through approved content.

## 18. Passive-income contract

### 18.1 One immutable snapshot

Production requests one `TerritoryIncomeSnapshot` for all supported resource types from one catalog/state/profile revision.

Conceptual shape:

```csharp
public sealed class TerritoryIncomeSnapshot
{
    public TerritoryIncomeStatus Status { get; }
    public CatalogIdentity Catalog { get; }
    public string TerritoryStateRevisionHash { get; }
    public RealmId ProfileRealm { get; }
    public TerritoryIncomeUnit Unit { get; }
    public IReadOnlyDictionary<ResourceType, long> Amounts { get; }
    public IReadOnlyList<TerritoryIncomeContribution> Contributions { get; }
    public IReadOnlyList<TerritoryDiagnostic> Diagnostics { get; }
}
```

### 18.2 Unit

Current migration unit is:

```text
whole resource units per minute
```

The production planner converts this through one approved deterministic rate/remainder policy owned by #163. Territory service does not apply resources itself.

### 18.3 Contribution rules

A territory contributes only when:

- catalog snapshot is valid;
- state domain is valid and unambiguous;
- profile realm is committed/valid;
- territory is known/currently supported;
- owner equals profile realm;
- definition bonus is supported and nonnegative;
- definition/state revisions are captured consistently.

### 18.4 Source of bonus

Income uses the validated definition bonus, not mutable saved `Name`, `BonusType`, `BonusAmount`, or `IsFortress` values.

Legacy saved fields are compatibility evidence only until migration.

### 18.5 Checked accumulation

Accumulate with checked arithmetic per resource. Overflow returns `Overflow`/unavailable; it never wraps, clamps, or partially returns another resource total as a complete snapshot.

### 18.6 Malformed/duplicate domain

Required malformed or duplicate known state makes the income snapshot unavailable. It does not select one row, sum duplicates, or subtract negative values.

### 18.7 Unknown future territories

Preserved unknown rows contribute nothing under the current catalog. Their raw data remains intact.

### 18.8 Pure repeated reads

Repeated queries with the same catalog/state/profile inputs return semantically identical immutable snapshots and mutate/save nothing.

## 19. Production integration

### 19.1 Resource production

Corrected #163 production consumes one validated income snapshot per production plan/tick/reconciliation, not six live service calls.

It records:

```text
territory catalog identity
territory state revision hash
profile realm
income snapshot hash
rate/remainder policy version
```

If income is unavailable, production fails closed for territory contributions and reports the domain status. It does not fabricate zero as a successful complete snapshot.

### 19.2 Offline parity

Live and offline production use the same territory income snapshot and rate profile. Offline code does not read raw territory rows or seed state separately.

### 19.3 World-state modifiers

#172 effects may transform a prepared income contribution only through a versioned effect consumer after both base snapshot and effect plan validate. No world-state service rewrites territory definitions/ownership.

### 19.4 Quest/progression

Only the committed capture operation applies current `CaptureTerritory +1`. Passive-income queries and ownership reads never update quests.

### 19.5 Battle/authorization producer

A future battle/command producer supplies typed capture authorization/results. Territory service does not simulate or infer battle victory.

## 20. UI and release containment

### 20.1 Dashboard

Kingdom territory display consumes immutable query results and content keys. Refreshing, enabling, disabling, toggling Board View, or rendering an unavailable status mutates nothing.

### 20.2 Capture command

Until a valid producer and production transaction exist, capture is unavailable in release policy.

When later enabled:

- no `RealmId.None → Crownlands` substitution;
- no raw `void` invocation;
- no optimistic success text;
- repeated presses share/disable by operation ID;
- status derives from committed/no-op/rejection result;
- inaccessible state has text/non-color cues.

### 20.3 ShellFoundation

Because the first Player profile includes Kingdom, malformed/unavailable territory state must be displayed safely without saving or throwing even while capture remains unavailable.

## 21. Current-interface migration

### 21.1 `GetTerritories()`

Replace production use with typed immutable query. A temporary compatibility wrapper returns immutable copies and never seeds or exposes backing state.

### 21.2 `CaptureTerritory(...)`

The `void` interface cannot represent integrity. Introduce a typed capture request/result method in a focused compatible transition. Keep/deprecate the old wrapper only while callers migrate; it must not silently claim success.

### 21.3 `CalculatePassiveIncome(...)`

Production migrates to the complete typed snapshot. A temporary scalar wrapper may return a clearly unavailable compatibility value with diagnostics, but may not be used as acceptance evidence or by final production integration.

### 21.4 Event

Replace/augment `Action<string, RealmId>` with the committed event payload after caller migration.

## 22. Save migration and repair

### 22.1 Candidate-only migration

Migration runs on a clone/candidate under #137, never through queries.

### 22.2 Known valid legacy row

Map exact ID/owner to normalized ownership state. Compare legacy definition fields to the approved catalog and record mismatch diagnostics/provenance.

### 22.3 Alias

Apply explicit alias with migration record. Preserve old ID evidence according to save compatibility policy.

### 22.4 Duplicate/null/blank/malformed

Preserve raw evidence, rank richer candidates, and require explicit repair. No first/last/max/sum merge.

### 22.5 Missing known state

Do not insert during a read. A migration plan may add an approved initial state only when candidate policy proves/authorizes initialization and records the operation.

### 22.6 Unknown future state

Preserve exactly. Do not convert its saved bonus/name/owner into current authoritative behavior.

### 22.7 Deletion

#137 full profile deletion removes territory state, migration metadata, capture ledger/history, notification outbox records, backups, temp/previous files, and quarantines as governed by the save policy.

## 23. First pure planner implementation boundary

Branch:

```text
codex/territory-contract-planner
```

### 23.1 Allowed

- immutable catalog identity/definition/reward-profile models;
- strict definition validator;
- immutable raw-state/compatibility/effective snapshots;
- deterministic domain diagnostics;
- initialization/migration plan models without persistence;
- typed capture request/result/event/authorization models;
- pure capture planner with fake economy/quest/candidate targets;
- passive-income snapshot planner;
- checked arithmetic, state revision, semantic-plan hashing;
- current source/caller/save/catalog inventory;
- complete focused EditMode tests.

### 23.2 Prohibited

- production interface/service/caller changes;
- save schema/service changes;
- real resource/credit/quest mutation;
- Bootloader/shared-file changes;
- catalogs/content/map/UI/scenes/Android;
- changed IDs/names/owners/bonuses/reward values;
- issue closure.

### 23.3 Suggested isolated paths

```text
unity/Assets/AL/Scripts/RealmWar/Territories/Contracts/**
unity/Assets/AL/Tests/EditMode/Territories/**
unity/Docs/Territory_Source_Inventory.md
```

Names may vary, but do not create another mutable service-local source.

## 24. Later phases

### Phase C — #183 territory catalog

- catalog envelope, definitions, aliases, reward profile;
- content keys and approved source inventory;
- schema/C#/shared-contract/hash validation;
- no production fallback.

### Phase D — save compatibility and service migration

After corrected #137 and explicit lock:

- normalized ownership schema/metadata/revision;
- old-save fixtures and candidate migration;
- typed immutable queries;
- no read-time seeding;
- capture candidate transaction/ledger/events/outbox;
- wrapper/caller migration.

### Phase E — production/offline/UI integration

- corrected #163 one-snapshot income consumption;
- corrected #153 reconciliation ownership;
- corrected #178 dashboard/command integration;
- battle/authorization producer;
- #177 presentation;
- #223/#150/#127 Player/PlayMode evidence and user acceptance.

## 25. Required test matrix

### 25.1 Definition catalog

- exact T1–T5 migration inventory;
- null definition;
- blank/duplicate ID;
- invalid/duplicate content key;
- undefined initial/allowed owner;
- neutral allowed/forbidden policy;
- no allowed owner;
- negative bonus;
- `long` overflow boundary;
- undefined/unsupported resource;
- missing/duplicate reward profile;
- alias cycle/collision;
- invalid prerequisite/capability;
- unsupported version/hash;
- immutable collection behavior;
- deterministic diagnostic ordering.

### 25.2 Raw-state compatibility

- valid initialized baseline;
- valid changed ownership/revisions;
- null list;
- empty new initialized metadata contradiction;
- legacy ambiguous empty;
- null row;
- blank/whitespace ID;
- duplicate exact ID group;
- case-different IDs under ordinal policy;
- undefined owner enum;
- owner forbidden by definition;
- missing required known territory;
- unknown future territory;
- removed/aliased territory;
- legacy name/bonus/resource/fortress mismatch;
- no mutation/save during validation;
- immutable snapshot cannot alter backing data.

### 25.3 Initialization/migration planning

- new profile baseline once;
- repeated initialization no-op/reject;
- legacy nonempty migration;
- legacy ambiguous empty with richer backup;
- legacy ambiguous empty without richer backup;
- intentionally empty future schema;
- candidate failure before/after migration;
- migration replay/idempotency;
- preserved unknown rows;
- no query-time insertion.

### 25.4 Query

- valid complete snapshot;
- selected realm none/undefined;
- valid neutral/hostile owners;
- malformed domain unavailable;
- catalog pending/unavailable;
- preserved unknown listed but unsupported;
- repeated pure reads;
- content keys not raw English formatting;
- no mutable TerritoryData exposure.

### 25.5 Capture validation

- neutral-to-realm transition;
- hostile-to-realm transition;
- same-owner no-op;
- blank/unknown/preserved-unknown ID;
- `RealmId.None`/undefined capturer;
- expected capturer differs from profile;
- capturer forbidden by definition;
- missing/invalid/expired/replayed authorization;
- stale previous owner;
- stale ownership revision;
- stale catalog/state/profile revision;
- malformed/duplicate/incomplete domain;
- no mutation on every rejection.

### 25.6 Capture reward transaction

- one valid transition grants exactly +100 credits and quest +1;
- same-owner grants zero;
- exact replay grants zero additional changes;
- operation correlation conflict;
- credit dependency missing/rejects/overflows;
- quest dependency missing/rejects;
- candidate apply failure at ownership, credit, quest, ledger, outbox;
- semantic validation failure;
- persistence failure;
- commit uncertainty/reconciliation;
- reload/retry after every boundary;
- event subscriber failure;
- notification enqueue/presenter failure;
- exact save/event/notification counts;
- previous owner/revision preserved on failure.

### 25.7 Passive income

- exact current T1–T5 totals by each initial realm/resource;
- owned/unowned/neutral behavior;
- multiple valid territories same resource checked sum;
- null row;
- duplicate ID;
- negative legacy bonus;
- invalid resource/owner;
- checked overflow;
- missing definition;
- preserved unknown exclusion;
- catalog/state revision captured;
- one complete snapshot across all resources;
- repeated pure reads;
- current units-per-minute semantics;
- no direct resource mutation.

### 25.8 Production/offline integration later

- one snapshot consumed per tick/plan;
- live/offline identical territory contribution for same duration/remainder state;
- snapshot unavailable fails closed visibly;
- mid-tick owner change cannot mix revisions;
- resource overflow/failure leaves candidate unchanged;
- #172 modifier preparation/failure;
- reload preserves ownership and contribution identity;
- no repeated query seeding.

### 25.9 UI/Player later

- dashboard startup/refresh/update/toggle nonmutation;
- malformed/unavailable territory status visible;
- no Crownlands fallback;
- capture unavailable until producer exists;
- repeated presses/idempotency;
- result-derived success/failure/no-op copy;
- long localization/large font/safe area/non-color cues;
- profile-safe PlayMode restoration;
- Windows Player Kingdom startup without territory mutation;
- Android lifecycle later.

## 26. Source and caller inventory requirement

Before production integration, publish machine-readable/current-head inventory of:

- every territory definition-like source and T1–T5 value;
- every `TerritoryData` save/query/mutation path;
- every `GetTerritories`, `CaptureTerritory`, `CalculatePassiveIncome`, and event caller;
- every controller/world-atlas/map display consumer;
- every production/offline income consumer;
- every quest/credit/event/notification dependency;
- every old-save fixture/version;
- every schema/shared-contract reference;
- exact unchanged/alias/preserved/removed classification;
- current shared-file overlaps and lock declarations.

## 27. File and lock boundary

### 27.1 Pure phase

No designated shared-file lock.

### 27.2 Save phase

Any edit to:

```text
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
```

requires the exclusive shared-file lock and current-main overlap review.

### 27.3 Catalog/service phase

Any `LocalGameDataService.cs` integration requires its designated lock. Prefer a focused versioned catalog service rather than expanding mutable fallback authority.

### 27.4 Bootloader

Do not edit `Bootloader.cs` under this issue. Consume the accepted #153 lifecycle/service registration path in a later focused integration.

### 27.5 Prohibited bundling

Do not combine:

- broad save recovery redesign;
- economy or quest implementation rewrite outside narrow adapters;
- battle simulation/combat;
- Warmaster/Realm Gem/Wishgate behavior;
- world-state content/effects;
- map/terrestrial/narrative source;
- Kingdom HUD redesign;
- scenes/Build Settings/Android.

## 28. Canonical validation evidence

Every implementation PR records:

```text
current main/base SHA
head SHA
changed-file inventory
shared locks
canonical workspace
Unity 2022.3.62f3 exact command/exit code
compiler/error scan
focused and complete EditMode totals/XML
PlayMode totals/XML when applicable
Player output/BuildReport when applicable
git diff --check origin/main...HEAD
final status and all unavailable/deferred checks
```

Catalog phase additionally records:

```text
catalog set/catalog/schema/content/source versions
raw SHA-256 and byte length
T1–T5 exact migration inventory
valid/invalid vector totals
packaging path/hash evidence
```

Save/capture phase additionally records:

```text
old-save/backup fixtures
initialization/migration/fault/deletion matrix
candidate/ledger/outbox/persistence receipts
operation replay/conflict/reconciliation
credit/quest/save/event/notification counts
```

Production phase additionally records:

```text
one-snapshot live/offline parity
checked totals/remainders
Kingdom controller nonmutation
Player startup/transition markers
profile isolation and severe-log scan
```

Duplicate-workspace, skipped, missing XML, stale output, compile-only, swallowed exception, or Console-only evidence is not passing.

## 29. Acceptance criteria

### Contract/planner phase

- [ ] Immutable definition, reward-profile, raw-state, compatibility, query, authorization, capture, income, plan, result, event, and diagnostic models exist.
- [ ] Exact T1–T5 current values are inventoried without rebalance.
- [ ] Strict catalog/state validation and deterministic diagnostics pass.
- [ ] Null/blank/duplicate/malformed/unknown state is non-crashing and nonmutating.
- [ ] Same-owner capture produces an exact no-op plan.
- [ ] Pure capture planner applies +100 credits and quest +1 exactly once to a fake candidate.
- [ ] Pure income planner returns one checked immutable all-resource snapshot.
- [ ] No production/save/catalog/UI/content behavior changes.

### Catalog/save/service phase

- [ ] One #183 territory catalog is authoritative and hash/version identified.
- [ ] New profiles initialize once through candidate creation; reads never seed.
- [ ] Legacy ambiguous empty and richer-backup behavior follow #137.
- [ ] Unknown future territories are preserved and excluded safely.
- [ ] Queries return immutable nonmutating snapshots.
- [ ] Actual captures require committed realm and typed authorization.
- [ ] Ownership, revision, credits, quest progress, ledger, outbox, and persistence commit atomically.
- [ ] Replay, stale concurrency, failure, uncertainty, reload, and deletion are proven.
- [ ] Typed committed events/notifications occur once after persistence.

### Production/integrated phase

- [ ] Live/offline production consume one validated territory-income snapshot with checked arithmetic and source identity.
- [ ] Dashboard/Board View/refresh/startup do not mutate territory state.
- [ ] No production Crownlands substitution or optimistic `void` success path remains.
- [ ] Player/PlayMode/accessibility evidence passes from the canonical workspace.
- [ ] No territory ID/name/owner/bonus/reward/balance change occurs without explicit approval.
- [ ] User integrated acceptance precedes milestone/release closure.

## 30. Implementation handoff

First branch:

```text
codex/territory-contract-planner
```

PR references:

```text
Refs #166
```

Do not use `Fixes #166` for the pure planner phase. Issue #166 remains open through catalog authority, save migration, typed capture transaction, production/offline income integration, Kingdom UI migration, Player/PlayMode evidence, and user acceptance.

The first PR body must state:

- no production service/interface/save/catalog/controller change;
- no real credit/quest/ownership mutation;
- no shared lock;
- exact current value/source/caller inventory;
- pure validation/capture/income test totals;
- current base/head evidence;
- later blocked phases.
