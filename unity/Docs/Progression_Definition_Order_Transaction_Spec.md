# Progression Definition, Order, and Transaction Integrity Specification

**Status:** Binding GPT technical specification for issue #165  
**Status date:** 2026-07-16  
**Audited base:** `54ba903ff3edc3edcf30ded9e0b03188b26c4c6d`  
**Primary implementation owner:** Codex engineering  
**Specification/review owner:** GPT  
**Building, research, troop, and player-facing content owner:** Codex narrative/content  
**Final balance, progression feel, and product approval:** User  
**Canonical Unity workspace:** `C:\Users\MY\Documents\AnotherLife\unity`

## 1. Purpose

This specification defines the authoritative technical boundary for the Kingdom progression domains currently represented by buildings, research, troop inventory, and training.

It covers:

- immutable versioned definitions and stable technical identities;
- strict saved-state validation without query-time repair;
- pure immutable queries and effective default views;
- explicit start/completion/cancellation/reconciliation orders;
- checked costs, durations, maximum levels, prerequisites, and eligibility;
- atomic resource, progression, quest, ledger, notification, and persistence operations;
- one-time timer completion and offline reconciliation;
- production, battle, Champion, realm, territory, and UI consumer boundaries;
- legacy ID/state/timer migration;
- concurrency, replay, failure, and deletion behavior;
- phased implementation, file ownership, testing, and evidence.

It replaces the current implicit model:

```text
string/enum supplied by any caller
→ query creates a live save row
→ hard-coded cost/duration/level defaults
→ independently saving resource mutation
→ mutate live progression row
→ independently save again
→ direct quest progress call
→ controller polls completion every second
→ save load completes timers through separate hard-coded rules
→ resource production reads/creates the same rows through another path
```

with:

```text
validated immutable definition/catalog snapshot
→ validated immutable saved-state compatibility snapshot
→ pure query or immutable command request
→ stale-safe checked order/application plan
→ one candidate save transaction
→ persist and verify
→ publish committed state/result
→ emit typed events/notifications once
```

This specification does not rebalance values, author buildings/research/troops, implement territory/Warmaster/Realm Gem systems, redesign Kingdom UI, or activate NVS-01.

## 2. Binding dependencies and sequence

### 2.1 Related contracts

Progression consumes rather than duplicates:

```text
unity/Docs/Game_Data_Catalog_Authority_Spec.md
unity/Docs/Save_Semantic_Compatibility_Policy.md
unity/Docs/Economy_Integrity_Spec.md
unity/Docs/Notification_Delivery_Contract_Spec.md
unity/Docs/Battle_Computation_Result_Transaction_Spec.md
unity/Docs/Champion_Combat_Encounter_Integrity_Spec.md
unity/Docs/Production_Scene_Player_Build_Spec.md
```

### 2.2 Dependency sequence

```text
pure progression definitions/state/order/transaction planners
          ↓
#156 trusted Unity asset baseline
          +
#183 versioned building/research/troop/progression technical source
          +
corrected #163 typed no-save economy operations
          +
corrected #152 typed no-save quest operations
          +
accepted #137 candidate persistence/ledger/outbox
          ↓
production building/research/training service migration
          +
corrected #153 lifecycle/offline owner
          +
corrected #178 non-mutating Kingdom controller
          ↓
#163 production resource generation consumes valid building snapshots
#174 battle consumes valid troop/research snapshots
#180 Champion consumes valid research/skill context where applicable
#166/#169/#171 consume progression through their owning contracts
          ↓
#223/#150 safe Kingdom Player integration and user playtest
```

### 2.3 Phase authorization

The pure contract/validator/planner phase in section 42 may proceed now because it mutates no production service, save, catalog, scene, UI, or balance.

Production integration remains blocked by the listed prerequisites. Current Editor functionality and merged save/economy PRs do not waive reopened semantic gates.

## 3. Verified current-source baseline

### 3.1 Queries create persistent state

Current services use query methods as state constructors:

```text
LocalBuildingService.GetBuildingState(id)
  absent → add BuildingState { id, level = 1 }

LocalResearchService.GetResearchState(id)
  absent → add ResearchState { id, level = 0 }

LocalTrainingService.GetTroopData(type)
  absent → add TroopInventoryData { type, count = 0 }
```

Consequences:

- UI rendering changes the save;
- unknown/blank/unsupported IDs become saved rows;
- malformed or missing catalog data is indistinguishable from a valid initial state;
- querying future/removed IDs can permanently modify a profile;
- state is live and mutable outside validation;
- query order changes serialization/order and later duplicate behavior.

### 3.2 Definition authority is incomplete and inconsistent

Current `IGameDataService`:

- exposes only realm, building, troop, Champion, and skill lookups;
- has no research query;
- returns `null` for every troop lookup;
- returns mutable ScriptableObjects and live collections;
- has no catalog status/version/hash/provenance.

Current fallback building IDs are:

```text
TownHall
Farm
LumberMill
Quarry
GoldMine
Barracks
Academy
Market
Storehouse
Forge
Stable
Workshop
Embassy
Wall
Watchtower
```

Kingdom/resource consumers additionally use:

```text
ManaShrine
Mine
```

Those IDs have no fallback `BuildingDefinition`.

Current research authority is a private dictionary of state-shaped defaults using display strings:

```text
Steel Forging
Plate Armor
Advanced Masonry
Irrigation
Ballistics
Logistics
Trade Routes
Arcane Study
```

It cannot be queried or validated through `IGameDataService`.

`TroopDefinition` exists but current service returns `null`; current runtime instead treats `TroopType` enum values as definitions.

### 3.3 Definition and saved state are conflated

Current `BuildingDefinition` contains only:

```text
Id
DisplayName
Icon
MaxLevel
```

Current `BuildingState` contains:

```text
BuildingId
Level
IsUpgrading
UpgradeCompleteTimestamp
```

There is no definition version, order ID, target level, start time, committed cost snapshot, completion identity, or state revision.

`ResearchState` is both the only current research “definition default” and saved state. No `ResearchDefinition` exists.

`TroopInventoryData` stores only enum type, count, and wounded count. There is no stable technical troop ID, definition version, reservation/deployment count, training order, or state revision.

### 3.4 IDs and enums are unsafe

Current services:

- accept blank building/research strings;
- accept undefined `TroopType` enum values;
- compare raw strings case-sensitively without alias/migration status;
- create saved rows for every supplied value;
- cannot distinguish unknown future IDs from invalid input;
- use display strings as research IDs;
- use enum values as troop identity while `TroopDefinition.Id` also exists;
- have no catalog-set compatibility check.

### 3.5 Building orders are unvalidated and partially committed

Current `StartUpgrade`:

```text
query/create state
→ if not upgrading
→ cost = current level × 100 Stone
→ ConsumeResource(cost) on live wallet
→ mark upgrading
→ end = wall clock + current level × 10 seconds
→ Save()
```

It does not validate:

- blank/unknown definition;
- current or target level;
- maximum level;
- negative/overflowing level/cost/duration;
- duplicate saved rows;
- existing malformed timer/state;
- prerequisites/realm/profile/catalog;
- active order identity;
- stale state/economy revision;
- save failure rollback;
- resource event/save count;
- idempotent duplicate command.

`ConsumeResource` mutates independently and does not save itself, but the current wallet query may destructively repair state. A thrown or failed later operation can leave consumed value in live memory.

### 3.6 Building completion is timer polling with nested side effects

Current `CompleteUpgrade`:

```text
query/create state
→ if upgrading and wall clock >= timestamp
→ level++
→ clear flag
→ direct quest UpdateProgress(BuildBuilding, 1)
→ Save()
```

It can:

- exceed `MaxLevel`;
- complete an unknown/malformed definition;
- increment overflowing/negative level;
- complete duplicate rows ambiguously;
- complete repeatedly after failure/reconstruction;
- mutate quest through a separately saving service;
- partially commit level/quest/save;
- run from UI polling, save-load offline progress, or another caller through different rules;
- use no completion/result identity.

### 3.7 Research orders repeat the same defects

Current `StartResearch`:

```text
query/create state
→ cost = (level + 1) × 200 Gold
→ ConsumeResource
→ mark researching
→ end = wall clock + (level + 1) × 15 seconds
→ Save()
```

Current `CompleteResearch` increments level, clears the flag, updates `QuestType.ResearchTech`, saves, and has no maximum/prerequisite/definition/identity validation.

`GetStatBonus` hard-codes:

```text
Attack → "Steel Forging"
Defense → "Plate Armor"
bonus = level × 0.05f
```

The query can create state and mutates the profile while calculating combat stats.

### 3.8 Training is an immediate nested transaction with no definition

Current `StartTraining`:

```text
cost = count × 10 Food  # unchecked int multiplication before long conversion
→ ConsumeResource
→ query/create troop row
→ count += requested count  # unchecked
→ direct quest UpdateProgress(TrainTroops, count)
→ Save()
```

It does not validate:

- count positive;
- multiplication or inventory overflow;
- enum defined/supported;
- troop definition/catalog/version;
- prerequisites/building capacity;
- wounded/active/queue rules;
- state duplicates/malformed values;
- stale revision/idempotency;
- save failure rollback.

`CompleteTraining` is empty, so the interface implies timed behavior while current training is instant.

### 3.9 Saved state can be malformed or duplicate

Current lists can contain:

- null rows;
- blank IDs;
- duplicate building/research IDs;
- duplicate troop enum rows;
- negative/overflowing levels and counts;
- negative wounded counts or wounded greater than total intent;
- undefined enum values;
- contradictory active flags/timestamps;
- timestamps before zero or far outside approved ranges;
- unknown future definitions;
- states above current max level;
- orphaned active orders after definition changes.

Current services select the first row and do not report ambiguity. Merged save code can also delete null rows and normalize top-level state before these domains inspect it.

### 3.10 Three completion authorities disagree

The same building/research timers can be completed by:

1. `KingdomSceneController.Update()` every second;
2. service `CompleteUpgrade` / `CompleteResearch` calls;
3. `LocalSaveGameService.ApplyOfflineProgress()` static timer completion.

Offline completion directly increments level and clears flags without:

- definition/max-level validation;
- quest progress;
- order/result ledger;
- notification;
- cost/source snapshot;
- service event;
- duplicate/recovery protection.

Online completion performs quest/save side effects. The same order therefore has different consequences depending on whether the game was open.

### 3.11 Controller lifecycle mutates progression

`KingdomSceneController`:

- calls `Bootloader.InitializeIfMissing()` and then owns another `Load()` path;
- polls eight building IDs and two research display strings every second;
- calls mutating query methods while refreshing the dashboard;
- includes IDs missing from current definitions;
- can create/complete/save progression merely by opening the scene.

Correcting this lifecycle belongs to reopened #178, while #165 provides the pure query/order/result API it must consume.

### 3.12 Production uses progression through another mutable path

`LocalResourceService.TickProduction()`:

- resolves `IBuildingService` through `ServiceLocator`;
- calls mutating `GetBuildingState` queries;
- defaults missing values to level `1`;
- consumes `ManaShrine` and `Mine` despite missing definitions;
- uses hard-coded production rates unrelated to `BuildingDefinition`;
- stores process-memory floating remainders outside the save;
- calls resource mutations independently;
- reads territory service opportunistically and catches missing service;
- has no catalog/revision/provenance snapshot.

### 3.13 Offline production conflicts with live production

Merged save code applies hard-coded offline rates:

```text
Food       4/second
Wood       2/second
Stone      1/second
Gold       0.5/second
ManaStone  0.25/second
Ore        1/3 per second
rare       town-independent 1/90 per second
cap        12 hours
```

Live production currently uses different building-level rates:

```text
Food       10 × Farm level / second
Wood       5 × LumberMill level / second
Stone      2 × Quarry level / second
Gold       1 × GoldMine level / second
ManaStone  0.35 × ManaShrine level / second
Ore        0.45 × Mine level / second
rare       0.015 × TownHall level / second
```

The two paths are parallel balance/definition authorities. Correct production planning/application belongs to #163 but must consume one #165 building snapshot and one source profile.

### 3.14 Research and troop consumers cannot prove source identity

Battle, Champion, Kingdom, and future systems can currently read:

- research bonuses from a string switch and mutable state;
- troop counts from enum rows;
- building levels from state-creating queries.

They cannot record the definition/catalog/state revision/hash that produced a combat or production result.

## 4. Authority and ownership

### 4.1 Codex engineering

Owns:

- immutable technical definition/state/order/result contracts;
- stable technical IDs and version/hash/provenance plumbing;
- strict validators and compatibility snapshots;
- checked arithmetic and deterministic clocks/order planners;
- candidate transaction, ledger, event, and notification integration;
- pure consumer snapshots;
- migration tooling/tests/evidence.

### 4.2 Codex narrative/content

Owns:

- player-facing building/research/troop names and descriptions;
- localization keys and narrative meaning;
- technology/faction/class flavor;
- authored unlock/prerequisite/reward meaning where not purely technical;
- player-facing order/result text.

### 4.3 Balance authority

No values are changed by this specification.

Observed current costs, durations, initial levels, maximum levels, bonuses, production rates, and counts are migration evidence only. A source migration must preserve an approved value exactly or stop for separate user approval.

### 4.4 User

Retains final approval of:

- costs, durations, maxima, initial levels, production, stat bonuses, troop quantities, queue/cancel/refund policy;
- progression pacing and feel;
- visible Kingdom presentation;
- integrated playtest/milestone/release acceptance.

## 5. Terminology

### 5.1 Definition

Immutable versioned technical source describing one building, research item, troop, order profile, cost, duration, prerequisite, effect, and consumer reference.

### 5.2 Raw saved state

The exact persisted rows before compatibility interpretation or repair.

### 5.3 Compatibility snapshot

An immutable analysis of raw saved state and definitions. It identifies valid, absent, unknown, malformed, duplicate, unsupported, or migration-required state without changing raw data.

### 5.4 Effective initial state

A pure view derived from a valid known definition when no saved row exists. It is not persisted until a successful state-changing transaction.

### 5.5 Progression order

One stable start-to-terminal operation for a building upgrade, research level, or training batch.

### 5.6 Start operation

The atomic transaction that commits cost and active-order state.

### 5.7 Completion operation

The atomic transaction that commits level/inventory change, order terminal state, quest progress, ledger, notification outbox, and publication.

### 5.8 Reconciliation

Pure evaluation of active orders against an injected clock and current source/state revisions, followed by explicit planned completion/cancellation/recovery operations.

### 5.9 Committed receipt

Immutable durable proof of a start/completion/cancellation result. UI and downstream systems do not infer commitment from flags/timestamps.

## 6. Stable identity and version contract

### 6.1 Required IDs

Production contracts require:

```text
gameId
catalogSetId
profileId or save identity token
buildingDefinitionId/contentVersion
researchDefinitionId/contentVersion
troopDefinitionId/contentVersion
costProfileId/contentVersion
durationProfileId/contentVersion
prerequisiteProfileId/contentVersion
effectOrProductionProfileId/contentVersion
progressionOrderId
startOperationId
completionOperationId
cancellationOperationId when applicable
progressionResultId
questOperationId when applicable
notification correlation IDs
state and catalog revisions
```

### 6.2 ID rules

IDs are:

- non-null and nonblank;
- case-sensitive unless an explicit versioned alias exists;
- within shared UTF-8 byte limits;
- free of control characters;
- stable technical IDs, never display names;
- resolved by the correct catalog set/version;
- never derived from Unity object names, list position, wall clock alone, localized text, or enum `ToString()`.

### 6.3 Legacy aliases

Existing values such as:

```text
TownHall
GoldMine
Steel Forging
Plate Armor
TroopType.Infantry
```

remain legacy identifiers until an approved alias/migration table maps them to stable IDs. The system does not silently lowercase, trim, rename, or substitute.

Alias resolution returns:

```text
ExactCanonical
ResolvedLegacyAlias
UnknownLegacyValue
AmbiguousAlias
UnsupportedFutureId
InvalidId
```

The original raw value is retained for migration diagnostics.

## 7. Definition catalog model

### 7.1 Building definition

Conceptual shape:

```csharp
public sealed class BuildingProgressionDefinition
{
    public string Id { get; }
    public string SchemaVersion { get; }
    public string ContentVersion { get; }
    public int InitialLevel { get; }
    public int MaxLevel { get; }
    public string UpgradeCostProfileId { get; }
    public string UpgradeDurationProfileId { get; }
    public string PrerequisiteProfileId { get; }
    public string ProductionProfileId { get; }
    public string RealmEligibilityProfileId { get; }
    public string PresentationContentKey { get; }
    public string SourceRevision { get; }
    public string RawSha256 { get; }
}
```

### 7.2 Research definition

```csharp
public sealed class ResearchProgressionDefinition
{
    public string Id { get; }
    public string SchemaVersion { get; }
    public string ContentVersion { get; }
    public int InitialLevel { get; }
    public int MaxLevel { get; }
    public string ResearchCostProfileId { get; }
    public string ResearchDurationProfileId { get; }
    public string PrerequisiteProfileId { get; }
    public IReadOnlyList<string> EffectProfileIds { get; }
    public string RealmEligibilityProfileId { get; }
    public string PresentationContentKey { get; }
    public string SourceRevision { get; }
    public string RawSha256 { get; }
}
```

### 7.3 Troop definition and training profile

```csharp
public sealed class TroopProgressionDefinition
{
    public string Id { get; }
    public string SchemaVersion { get; }
    public string ContentVersion { get; }
    public string LegacyTroopTypeAlias { get; }
    public string TrainingCostProfileId { get; }
    public string TrainingDurationProfileId { get; }
    public string TrainingCapacityProfileId { get; }
    public string PrerequisiteProfileId { get; }
    public string BattleProfileId { get; }
    public string RecoveryProfileId { get; }
    public string RealmEligibilityProfileId { get; }
    public string PresentationContentKey { get; }
    public string SourceRevision { get; }
    public string RawSha256 { get; }
}
```

### 7.4 Cost profiles

Cost profiles return an immutable ordered set of positive resource costs for a target level/batch and include:

```text
stable ID/version/hash
applicable definition IDs/types
input domain and target-level/batch limits
checked formula or explicit table
required resource definition IDs
rounding policy
maximum result
```

The caller cannot supply arbitrary cost values.

### 7.5 Duration profiles

Duration profiles return checked integer microseconds or UTC-second duration values from immutable inputs. They define:

```text
stable ID/version/hash
input/target level or batch bounds
zero-duration eligibility
time unit and rounding
maximum duration
clock policy
```

### 7.6 Prerequisite profiles

Prerequisites resolve pure immutable requirements such as:

```text
committed realm/eligibility
building/research levels
profile/chapter/quest gates
capacity limits
catalog/source capabilities
```

They return typed unmet reasons, not player-facing strings.

### 7.7 Effect profiles

Research effect profiles expose stable technical modifiers. They do not mutate services or encode player copy.

Examples of technical outputs:

```text
stat modifier snapshot
production modifier reference
training modifier reference
unlock capability reference
```

An unknown/missing effect blocks consumers that require it. It does not become zero unless the definition explicitly declares an optional neutral effect.

## 8. Definition validation

Reject/report at minimum:

- null definitions;
- blank/duplicate IDs;
- unsupported schema/content version;
- wrong game/catalog identity;
- invalid initial/max level;
- initial level above max;
- missing/unknown cost/duration/prerequisite/effect/production/battle references;
- invalid realm eligibility;
- formula/table domain gaps;
- negative/non-finite/out-of-range numeric source;
- cost/duration overflow;
- duplicate effect or resource cost entries;
- unknown required localization/content key;
- generated/shared-contract drift;
- source hash/provenance mismatch;
- conflicting definitions across sources.

Definitions and collections are immutable, deterministically ordered, and published atomically through #183.

## 9. Observed current tuning inventory

These values are recorded for migration tests and are not newly approved balance.

### 9.1 Building

```text
known fallback max level: 10
query-created initial level: 1
upgrade cost: current level × 100 Stone
upgrade duration: current level × 10 seconds
target level: current level + 1
```

Current fallback definitions do not include `ManaShrine` or `Mine`, despite runtime consumers using them.

### 9.2 Research

```text
query-created initial level: 0
cost: target level × 200 Gold
duration: target level × 15 seconds
Attack effect: Steel Forging level × 0.05
Defense effect: Plate Armor level × 0.05
```

No current maximum levels or prerequisite definitions exist.

### 9.3 Training

```text
cost: requested count × 10 Food
duration: immediate in current implementation
inventory increase: requested count
quest progress: requested count
```

No current training capacity, queue, prerequisite, or troop definition lookup exists.

### 9.4 Production

Live and offline rates conflict as recorded in section 3.13. Neither path becomes approved authority through this specification. #163 and source review must choose one explicit production profile/migration outcome.

## 10. Raw saved-state model and validation

### 10.1 Current rows

Current raw rows are preserved exactly:

```text
BuildingState: BuildingId, Level, IsUpgrading, UpgradeCompleteTimestamp
ResearchState: ResearchId, Level, IsResearching, CompleteTimestamp
TroopInventoryData: Type, Count, WoundedCount
```

Ordinary queries/validation do not delete, merge, clamp, seed, reorder, or rewrite them.

### 10.2 Validation domains

Each domain returns one immutable compatibility result:

```text
Valid
EmptyOrAbsent
UnavailableCatalog
UnsupportedCatalogVersion
NullCollection
NullRow
BlankId
UnknownDefinition
PreservedUnknownFutureDefinition
AmbiguousAlias
DuplicateIdentity
InvalidLevel
AboveCurrentMax
InvalidCount
InvalidWoundedState
UndefinedLegacyEnum
ContradictoryOrderState
InvalidTimestamp
OrphanedActiveOrder
DefinitionVersionMismatch
MigrationRequired
```

### 10.3 Duplicate policy

All rows in a duplicate identity group are preserved and the entire group is disabled for mutation and authoritative query.

Never:

- keep first/last;
- sum levels/counts;
- choose max;
- delete duplicate rows;
- use dictionary overwrite;
- repair during a query.

Explicit repair belongs to #137 and requires a reviewed candidate plan.

### 10.4 Unknown future state

Unknown stable future IDs/types are preserved opaquely and excluded from unsupported mutation/effects/production/battle use. Downgrade does not delete or rename them.

### 10.5 Building state invariants

For a valid current-format state:

```text
initialLevel <= level <= maxLevel
no active order → IsUpgrading false and timestamp policy satisfied
active legacy timer → IsUpgrading true and timestamp valid under migration policy
level == maxLevel → no new upgrade order
```

After migration, order identity lives in explicit order records rather than a Boolean/timestamp pair alone.

### 10.6 Research state invariants

Equivalent to building invariants with definition-specific initial/max level and active research order.

### 10.7 Troop state invariants

At minimum:

```text
count >= 0
wounded >= 0
active/deployable/wounded relationship follows #165/#174 approved inventory model
no duplicate stable troop ID
legacy enum resolves exactly or is preserved unsupported
```

Whether current `Count` includes wounded must be decided in the migration record before authoritative battle/training use. Technical code must not assume one interpretation silently.

## 11. Pure effective queries

### 11.1 Query statuses

Minimum statuses:

```text
FoundSavedValid
FoundEffectiveInitialUnpersisted
UnknownDefinition
DefinitionUnavailable
StateUnavailable
StateMalformed
DuplicateState
UnsupportedVersion
PreservedUnknownFuture
```

### 11.2 Effective initial state

When a valid known definition has no saved row, a query may return an immutable effective initial snapshot:

```text
building level = definition.InitialLevel
research level = definition.InitialLevel
troop active/wounded = zero under the approved inventory profile
```

The status explicitly says `FoundEffectiveInitialUnpersisted`. The raw save remains unchanged.

### 11.3 Query purity

Queries:

- save zero times;
- emit zero domain/progression events;
- allocate no runtime ScriptableObject;
- create no raw row;
- return no backing list/row;
- preserve deterministic definition/state ordering;
- include source/state revisions and diagnostics;
- are idempotent across repeated calls.

### 11.4 Collection query

A collection query returns an immutable ordered union or explicitly separate views:

```text
known definitions with saved/effective state
preserved unknown raw state
invalid/duplicate diagnostic groups
```

It never hides malformed/unknown data merely because a UI wants only valid rows.

## 12. Progression snapshot model

Conceptual immutable snapshots:

```csharp
public sealed class BuildingProgressionSnapshot
{
    public string DefinitionId { get; }
    public string DefinitionContentVersion { get; }
    public int Level { get; }
    public ProgressionStateOrigin Origin { get; }
    public ProgressionOrderSnapshot ActiveOrder { get; }
    public string StateRevision { get; }
    public string CatalogSetId { get; }
}

public sealed class ResearchProgressionSnapshot { ... }

public sealed class TroopInventorySnapshot
{
    public string TroopDefinitionId { get; }
    public string DefinitionContentVersion { get; }
    public long ActiveCount { get; }
    public long WoundedCount { get; }
    public long ReservedOrDeployedCount { get; }
    public string StateRevision { get; }
}
```

All values are validated and immutable. Invalid domains return diagnostics rather than a misleading snapshot.

## 13. Order identity and lifecycle

### 13.1 Order types

```text
BuildingUpgrade
ResearchLevel
TroopTrainingBatch
```

### 13.2 Order states

```text
Planned
StartPendingCommit
Active
CompletionEligible
CompletionPendingCommit
Completed
Cancelled
Failed
RecoveryRequired
```

`Planned` is not persisted/player-owned. Persisted orders begin at `Active` only after successful start commit.

### 13.3 Stable operation IDs

Each order records:

```text
progressionOrderId
startOperationId
completionOperationId
optional cancellationOperationId
profileId
definitionId/version
source catalog/revision/hash
requested/target level or batch count
committed cost snapshot
start UTC timestamp
end UTC timestamp
order policy version
```

### 13.4 Transition rules

```text
Planned → StartPendingCommit
StartPendingCommit → Active | Failed | RecoveryRequired
Active → CompletionEligible | Cancelled | Failed | RecoveryRequired
CompletionEligible → CompletionPendingCommit
CompletionPendingCommit → Completed | Failed | RecoveryRequired
```

A zero-duration training profile may use one candidate transaction that records start and completion semantics atomically while retaining both operation identities.

### 13.5 Terminality

One order has one terminal state. Exact replay returns the existing receipt. Changed reuse of an order/operation ID is a correlation conflict.

## 14. Injected clock and timestamp rules

### 14.1 Clock source

Planning and reconciliation receive an injected validated UTC clock. Domain services do not call `DateTimeOffset.UtcNow` internally without the clock interface.

### 14.2 Timestamp validation

Reject/report:

- negative/zero timestamps where not allowed;
- end before start;
- duration mismatch with committed profile snapshot;
- timestamp outside technical retention/future bounds;
- nonmonotonic or rollback state according to #137 clock policy;
- missing timestamp on active order;
- timestamp on nonactive state where prohibited.

### 14.3 Timer meaning

The end timestamp means the order is **eligible** for completion. It does not mutate state automatically.

Only a committed completion operation changes level/inventory/quest/result state.

## 15. Start command request

Conceptual shape:

```csharp
public sealed class ProgressionStartRequest
{
    public string ProfileId { get; }
    public ProgressionOrderType OrderType { get; }
    public string DefinitionId { get; }
    public string ProgressionOrderId { get; }
    public string StartOperationId { get; }
    public int RequestedTargetLevel { get; }
    public long RequestedBatchCount { get; }
    public string ExpectedProgressionRevision { get; }
    public string ExpectedEconomyRevision { get; }
    public string ExpectedCatalogSetId { get; }
    public string RequestPolicyVersion { get; }
}
```

A request does not contain caller-calculated cost, duration, max level, effect, or display text.

## 16. Start planning

The pure planner validates:

- identity/profile/catalog/version;
- definition and compatibility state;
- requested target/batch;
- current/effective initial level/count;
- no conflicting active order;
- max level and prerequisites;
- realm/profile eligibility;
- cost and duration profile result;
- resource wallet status/affordability through a typed snapshot;
- state/economy revisions;
- existing order/start ledger state;
- notification definition availability where durable outbox is required.

It returns an immutable plan or typed rejection.

### 16.1 Start statuses

```text
Ready
AlreadyStarted
AlreadyCompleted
InvalidRequest
UnknownDefinition
DefinitionUnavailable
StateUnavailable
StateMalformed
DuplicateState
UnsupportedVersion
InvalidTarget
AtMaximum
PrerequisiteUnmet
RealmIneligible
OrderAlreadyActive
CostInvalid
InsufficientResources
EconomyInvalid
StaleProgressionRevision
StaleEconomyRevision
CorrelationConflict
ArithmeticOverflow
```

### 16.2 Start plan

Contains:

```text
order/start IDs
definition/source identity
previous/effective state
requested/target state
ordered checked resource costs
start/end timestamps and duration profile
active-order row create/update intent
start ledger record
required durable outbox requests
expected revisions
plan hash
```

It contains no mutable save row, service, ScriptableObject, delegate, or player-facing string.

## 17. Atomic start transaction

Required order:

```text
1. validate request/current snapshots and detect replay/conflict
2. prepare immutable start plan
3. clone validated save candidate
4. apply checked no-save economy cost through corrected #163 primitive
5. create/update progression row and active order in candidate
6. add start operation ledger record
7. add required durable notification outbox record
8. validate complete candidate and expected revisions
9. persist/verify through accepted #137
10. publish candidate/current revisions
11. emit committed order-start event
12. enqueue visible order-start presentation once
```

Inside steps 3–8, do not call compatibility wrappers, `Save()`, quest services, UI, or events.

## 18. Completion eligibility and planning

### 18.1 Pure eligibility query

Given an active order, definition snapshot, state snapshot, and clock:

```text
NotYetEligible
Eligible
AlreadyCompleted
Cancelled
InvalidOrder
StateMismatch
DefinitionDrift
ClockInvalid
RecoveryRequired
```

The query mutates nothing.

### 18.2 Completion request

```text
profileId
progressionOrderId
completionOperationId
expected progression/economy/quest/catalog revisions
completion policy version
```

The caller does not supply level increment, troop count, quest amount, or reward.

### 18.3 Completion planner

Validates:

- active eligible order and exact definition/source snapshot;
- current row/order consistency;
- target/max level or batch count;
- no prior completion/conflict;
- expected revisions;
- quest operation mapping;
- notification/outbox mapping;
- inventory arithmetic and domain invariants;
- definition migration policy if source changed.

### 18.4 Completion statuses

```text
Ready
AlreadyCompleted
NotYetEligible
InvalidRequest
UnknownOrder
OrderMalformed
StateUnavailable
StateMalformed
DefinitionUnavailable
DefinitionDrift
TargetInvalid
AtMaximum
InventoryOverflow
QuestUnavailable
QuestInvalid
StaleRevision
CorrelationConflict
ClockInvalid
RecoveryRequired
```

## 19. Atomic completion transaction

Required order:

```text
1. validate active order/eligibility and detect replay/conflict
2. prepare immutable completion plan
3. clone validated save candidate
4. apply exact target level or training inventory mutation
5. clear/terminalize active order in candidate
6. apply typed no-save quest progress through corrected #152 adapter
7. add completion/result ledger record
8. add required durable notification outbox record
9. validate complete candidate and revisions
10. persist/verify through accepted #137
11. publish candidate/current revisions
12. emit committed completion/progression events
13. enqueue visible completion presentation once
```

Level/count changes and quest progress cannot partially commit.

## 20. Quest progress mapping

Current mappings are migration evidence:

```text
building completion → QuestType.BuildBuilding amount 1
research completion → QuestType.ResearchTech amount 1
training completion → QuestType.TrainTroops amount trained count
```

Production mappings come from a versioned consequence profile or owning quest contract. They include stable quest operation identity and are applied only after valid authoritative completion.

Practice/development/demo orders, if ever allowed, are explicitly ineligible for quest progress.

## 21. Cancellation and refund policy

Current services expose no cancellation. Initial production behavior must choose explicitly:

```text
CancellationUnsupported
or
versioned cancellation/refund policy per order type
```

If unsupported, cancellation requests return a typed status and mutate nothing.

If later supported, definitions specify:

- eligible phases;
- refund percentage/resource rounding;
- cancellation fee;
- quest/notification behavior;
- order/result identity;
- candidate transaction and replay semantics.

Codex engineering cannot invent refunds or cancellation pacing.

## 22. Training semantics

### 22.1 Stable troop identity

Training uses `TroopDefinitionId`, not raw `TroopType`. Legacy enum aliases resolve through #183.

### 22.2 Inventory interpretation

Before production integration, #165 and #174 must approve whether current `Count` includes wounded units. The migrated model separates at minimum:

```text
active/deployable
wounded
reserved/deployed
```

No operation allows negative counts or double-use of reserved troops.

### 22.3 Immediate compatibility profile

The current implementation is instant. A versioned zero-duration training profile may preserve that behavior:

```text
start and completion prepared in one atomic candidate
→ cost, inventory, quest, ledger, outbox persist once
```

It still uses one order/result identity and exact replay protection.

### 22.4 Timed training

Future timed training requires an explicit source/profile/product decision. It is not inferred from the existing empty `CompleteTraining` method.

### 22.5 Count arithmetic

Use checked 64-bit planning and approved persisted range conversion. Reject:

- nonpositive batch;
- per-order/total capacity overflow;
- cost multiplication overflow;
- inventory addition overflow;
- active/wounded/reserved invariant failure;
- unsupported definition/prerequisite.

## 23. Research effects and consumer snapshots

### 23.1 Pure effect query

Given valid research definitions and state, produce an immutable effect snapshot:

```text
catalog/source identity
research state revision
ordered effect profile IDs/versions
validated numeric modifiers
snapshot hash
```

No state is created or saved.

### 23.2 Unknown/invalid research

Unknown future rows are preserved but excluded. Duplicate/malformed/unsupported required research disables affected effect consumers.

### 23.3 Battle and Champion

#174 and #180 consume the immutable effect snapshot and record its hash. They do not call `IResearchService` or use string switches.

### 23.4 Current attack/defense mapping

The observed `5% per level` mappings are migration evidence. Stable research/effect IDs and exact fixed-point values must be sourced/approved before production.

## 24. Building production snapshot

### 24.1 Pure snapshot

A production snapshot contains only valid known building states and their production profile references:

```text
building definition ID/version
validated level
production profile ID/version/hash
progression state revision
catalog set/revision
```

### 24.2 Missing/malformed state

For a known definition with absent row, the effective initial level may be included with `EffectiveInitialUnpersisted` origin. Unknown/missing definitions, duplicate/malformed rows, or missing production profiles block that contribution.

They do not default to level `1` silently.

### 24.3 Economy integration

#163 consumes the immutable building/territory/world/research production snapshots and one clock delta to plan one checked atomic wallet/remainder result.

#165 does not own resource mutation/remainder persistence, and #163 does not create progression rows.

### 24.4 Live/offline parity

Live ticks and offline reconciliation use the same production profile/planner and source revisions. Differences are limited to an explicit offline cap/policy approved separately.

## 25. Reconciliation and offline completion

### 25.1 One reconciliation owner

A lifecycle-owned progression reconciler, not UI Update, evaluates active orders:

```text
validated current candidate
+ immutable definitions
+ injected clock
→ ordered reconciliation plan
```

### 25.2 Deterministic order

Eligible orders are sorted by:

```text
end timestamp
order type stable order
definition ID
progressionOrderId
```

### 25.3 Batch completion

The reconciler may prepare one candidate transaction containing multiple independent eligible order completions when:

- every involved domain is valid;
- every operation ID is unique/idempotent;
- quest/result/outbox operations fit one candidate;
- failure semantics are explicit.

If one order is malformed/ambiguous, the policy must define domain-level blocking or safe independent completion; it cannot silently skip corruption and claim all progress applied.

### 25.4 Offline progress

After #137 selects a validated candidate:

```text
compute eligible progression completions
+ compute #163 production through the same source profiles
+ apply checked candidate operations
+ add ledgers/outbox
+ validate
+ persist/verify/publish once
```

Do not directly increment levels/counts or create resources in `LocalSaveGameService`.

### 25.5 Clock rollback/future timestamp

Invalid clock state returns typed reconciliation failure/recovery status. It does not complete early, extend silently, or rewrite timestamps.

## 26. Legacy active-order migration

### 26.1 Building/research timers

A current row with `IsUpgrading`/`IsResearching = true` may migrate to one legacy order only when:

- identity resolves exactly;
- row group is unique;
- level is valid;
- timestamp is valid;
- definition/source migration policy recognizes the legacy order semantics;
- no conflicting order/ledger exists.

### 26.2 Deterministic legacy order identity

A migration tool may derive an ID from a canonical hash of:

```text
profile/save migration identity
order type
definition canonical ID
raw legacy level
raw completion timestamp
migration policy version
```

The ID is not created from display text or wall clock alone.

### 26.3 Paid-cost treatment

Legacy active timers are presumed already paid only under an approved migration rule. Completion must not charge again. The migrated receipt records `LegacyCostAlreadyCommittedUnknownSnapshot` or another exact compatibility status.

### 26.4 Ambiguous legacy state

Duplicate rows, invalid level/timestamp, unknown definition, or contradictory flags are preserved and disabled. They are not converted or auto-completed.

### 26.5 Completed/inactive rows

Valid inactive rows migrate to explicit versioned state without inventing historical order ledgers. Historical completion is not inferred for quest/reward replay.

## 27. Applied-operation ledger

### 27.1 Keys

Primary operation IDs:

```text
startOperationId
completionOperationId
cancellationOperationId
```

The order record references all of them.

### 27.2 Stored semantics

At minimum:

```text
operation/order/result IDs
profile identity
order type
definition ID/version/hash
previous/target/final level or counts
committed costs/refunds
start/end/commit UTC timestamps
quest operation/result IDs
catalog/application policy versions
notification correlations
operation hash
```

### 27.3 Replay/conflict

| Existing state | Outcome |
| --- | --- |
| no record | plan first operation |
| same operation ID and exact semantic/hash data | return stored committed receipt; mutate/notify zero times |
| same ID with changed data | correlation conflict; block |
| pending/uncertain durability | return recovery-required; do not replay blindly |
| malformed ledger | disable affected operation/domain and require #137 recovery |

### 27.4 Shared ledger architecture

Use the shared #137 transaction/operation ledger. Do not create independent service-local save files or in-memory idempotency stores.

## 28. Typed service APIs

### 28.1 Queries

Conceptual operations:

```text
QueryBuilding(id)
QueryAllBuildings()
QueryResearch(id)
QueryAllResearch()
QueryTroopInventory(id)
QueryAllTroopInventory()
QueryProgressionOrder(orderId)
QueryResearchEffects(profile/context)
BuildProductionSnapshot()
```

All return typed immutable results.

### 28.2 Commands

```text
PlanStart(request)
CommitStart(plan)
EvaluateCompletion(orderId, clock)
PlanCompletion(request)
CommitCompletion(plan)
PlanCancellation(request)
CommitCancellation(plan)
PlanReconciliation(snapshot, clock)
CommitReconciliation(plan)
```

Compatibility wrappers may exist temporarily but must not hide status, create state on reads, or bypass the transaction boundary.

## 29. Typed operation outcomes

Minimum common statuses:

```text
Committed
AlreadyCommitted
Ready
NotYetEligible
NoChange
InvalidRequest
UnknownDefinition
DefinitionUnavailable
StateUnavailable
StateMalformed
DuplicateState
UnsupportedVersion
InvalidTarget
AtMaximum
PrerequisiteUnmet
RealmIneligible
OrderAlreadyActive
OrderNotFound
OrderMalformed
CostInvalid
InsufficientResources
EconomyInvalid
QuestUnavailable
QuestInvalid
InventoryOverflow
ClockInvalid
StalePlan
CorrelationConflict
PersistenceFailed
CommitUncertain
RecoveryRequired
InternalInvariantFailure
```

Only committed statuses expose a committed receipt. A plan or eligibility result is never displayed as completed.

## 30. Events and notification integration

### 30.1 Technical events

Examples:

```text
ProgressionOrderStarted
ProgressionOrderCompletionEligible
ProgressionOrderCompleted
ProgressionOrderCancelled
BuildingLevelChanged
ResearchLevelChanged
TroopInventoryChanged
ProgressionReconciliationCompleted
```

### 30.2 Event rules

- immutable payloads;
- stable operation/order/definition/profile IDs;
- previous/new revision;
- emitted after authoritative state publication;
- exact replay emits zero duplicate event;
- subscriber exceptions are isolated;
- no player-facing strings;
- events cannot reenter the transaction.

### 30.3 Notifications

#177 typed definitions may include:

```text
progression.order_started
progression.order_completed
progression.order_cancelled
progression.order_failed
progression.order_recovery_required
progression.prerequisite_unmet
```

Exact copy and presentation are content-owned. Console logging does not mark delivery.

## 31. UI and controller integration

### 31.1 Read-only presentation

Kingdom UI consumes immutable query snapshots/statuses. Rendering or refreshing never creates/repairs/completes state.

### 31.2 Availability

Buttons/commands are enabled only when a pure availability query says ready. Unavailable reasons include:

```text
catalog unavailable
state malformed/duplicate
max level
prerequisite unmet
insufficient resources
active order
save/economy/quest dependency unavailable
release command not authorized
```

### 31.3 Start/completion presentation

UI shows:

```text
request pending
start committed
active with validated remaining time
completion eligible/pending commit
completion committed
failed known-not-committed
recovery required
```

It does not infer completion from timestamp/flag or show success before a committed receipt.

### 31.4 Controller boundaries

Corrected #178 removes:

- controller-owned save load;
- periodic completion calls;
- state-creating queries;
- hidden resource/progression mutation.

A separate lifecycle/reconciler owns eligible order processing.

## 32. Concurrency and stale plans

- transactions serialize per profile/save revision;
- two exact start/completion requests converge to one commit/duplicate receipt;
- two different orders from the same economy/progression revision cannot overspend/overwrite;
- stale second plan rejects;
- active-order uniqueness is validated again at apply time;
- completion and cancellation races resolve by revision/ledger, not last write;
- offline and online reconciliation cannot complete the same order twice;
- UI/event/notification callbacks cannot reenter mutation;
- cancellation after persistence begins cannot claim clean cancellation unless durability is known.

## 33. Failure and recovery semantics

### 33.1 Before persistence

No live state, resource, quest, event, or notification changes. Return known-not-committed failure.

### 33.2 Persistence failure

If durability is known not to have occurred, retain previous published state and allow a reviewed retry with the same operation identity.

### 33.3 Commit uncertainty

Freeze duplicate application and return `CommitUncertain`/`RecoveryRequired`. #137 reconciles the ledger/candidate. Do not recharge, relevel, retrain, or re-notify blindly.

### 33.4 Source drift during active order

The order retains the committed definition/cost/duration snapshot identity. Completion policy distinguishes:

```text
CompatibleCompleteUnderCommittedSnapshot
MigrationRequired
UnsupportedVersion
DefinitionRemovedButLegacyOrderPreserved
```

A catalog update does not silently recalculate paid cost or end time.

### 33.5 Notification failure

Committed progression remains committed. The typed delivery receipt/outbox remains pending/failed without replaying the progression operation.

## 34. Security and abuse resistance

Reject or isolate:

- arbitrary caller IDs/costs/durations/rewards;
- blank/oversized/control-character IDs;
- undefined enums and unknown required definitions;
- negative/overflow levels/counts/costs/durations/timestamps;
- duplicate saved rows/orders/operation IDs;
- stale revisions;
- forged catalog/source hashes;
- direct compatibility-wrapper use inside candidate transactions;
- query-time state creation/repair;
- practice/debug command result application;
- UI/controller/repeated frame mutation;
- raw player string/action/scene/URL injection.

Development fallback sources/orders are explicitly marked and excluded from production authority/evidence.

## 35. Required tests

### 35.1 Definition validation

- representative valid building/research/troop definitions;
- null/blank/duplicate IDs;
- unsupported versions;
- invalid initial/max levels;
- missing/unknown cost/duration/prerequisite/effect/production/battle references;
- invalid formula/table coverage;
- negative/non-finite/out-of-range source values;
- duplicate resource/effect entries;
- hash/provenance/generated-contract mismatch;
- deterministic ordering/diagnostics.

### 35.2 Legacy ID/alias mapping

- exact canonical ID;
- every approved legacy building/research/troop value;
- blank/whitespace/control-character;
- case mismatch;
- ambiguous alias;
- unknown future stable ID preservation;
- raw value retained in diagnostics;
- no display-string derivation.

### 35.3 State validation

For buildings/research/troops:

- null collection;
- empty/absent valid state;
- null row;
- blank/unknown/duplicate identity;
- invalid/negative/over-max level;
- negative/overflow active/wounded/reserved count;
- undefined legacy enum;
- contradictory flag/timestamp;
- invalid/future timestamp;
- orphaned active order;
- definition version drift;
- unknown future row preservation;
- deterministic state revision;
- source lists/rows unchanged.

### 35.4 Query purity

- known saved valid;
- effective initial unpersisted;
- unknown definition;
- catalog unavailable;
- malformed/duplicate state;
- collection union/preserved unknown diagnostics;
- repeated query idempotency;
- zero row/list mutation;
- zero save/event/notification;
- returned snapshots cannot mutate backing state.

### 35.5 Cost/duration/prerequisite planning

- every observed current building/research/training vector;
- level/batch boundaries;
- max-level rejection;
- multiplication/addition overflow;
- insufficient resources;
- malformed economy snapshot;
- realm/prerequisite eligible/ineligible;
- zero-duration training profile;
- invalid profile/source version;
- stable plan hash.

### 35.6 Start planning/application

- valid building/research/training start;
- exact duplicate before/after commit;
- correlation conflict;
- active order conflict;
- stale progression/economy/catalog revision;
- row creation only inside candidate for effective initial state;
- exact checked resource deductions;
- failure before/after each candidate step;
- no partial live mutation;
- exact save/event/notification count;
- commit uncertainty/recovery.

### 35.7 Completion eligibility

- before/exactly at/after end time;
- invalid/rollback/future clock;
- inactive/completed/cancelled/malformed order;
- definition compatible/drift/removed;
- target/max-level boundary;
- deterministic eligibility status;
- zero mutation/save.

### 35.8 Completion application

- building level once;
- research level/effect once;
- training count once;
- exact quest progress once;
- duplicate before/after reload;
- completion/cancellation race;
- stale revision;
- inventory/level overflow;
- missing quest adapter;
- candidate/persistence/verification/outbox failure;
- commit uncertainty;
- exact save/event/notification counts;
- no online/offline consequence divergence.

### 35.9 Legacy timer migration

- valid unique building/research timer;
- past/future timestamp;
- invalid level/definition;
- duplicate row;
- contradictory flag/timestamp;
- deterministic derived order ID;
- no second cost charge;
- no invented historical quest credit;
- exact replay/reload;
- unsupported source drift preserved/recovery-required.

### 35.10 Reconciliation/offline

- no eligible orders;
- one/multiple eligible orders deterministic order;
- mix of building/research/training;
- malformed order/domain policy;
- online and offline exact same completion result;
- repeated load/reconciliation no duplicate;
- clock cap/rollback/future behavior;
- production and completions in one candidate where approved;
- failure at each operation/persistence boundary;
- no direct save-service level/resource increment.

### 35.11 Research effects

- current attack/defense migration vectors;
- multiple valid effects deterministic order;
- unknown/duplicate/malformed research row;
- unsupported effect/version;
- immutable snapshot hash;
- battle/Champion consumer gets exact revision;
- no query-created research row;
- no string switch authority.

### 35.12 Production integration

- valid building production snapshot;
- effective initial unpersisted contribution according to approved profile;
- missing `ManaShrine`/`Mine` definition blocks rather than defaults;
- duplicate/malformed building row blocks contribution;
- live/offline planner parity;
- source version change;
- production remainder persistence/reload through #163;
- no ServiceLocator/query-time creation;
- checked resource application.

### 35.13 UI/controller

- Start/build/Refresh/Update/toggle save-state equality;
- no controller `Load()`;
- no periodic completion mutation;
- unavailable/max/prerequisite/resource/order statuses;
- committed receipt updates UI once;
- pending/failure/recovery states;
- long/localized content and non-color-only status;
- repeated scene/controller reconstruction;
- missing/partial services.

### 35.14 Integration/regression

- current approved valid IDs/values migrate without drift;
- absent building initial level remains `1` only through definition source;
- absent research/troop remains zero effective state without persistence;
- current order cost/duration vectors;
- #163 resource transaction;
- #152 quest operation;
- #137 save/reload/recovery/deletion;
- #174 troop/research snapshot;
- #180 research/realm context where applicable;
- corrected #153 lifecycle;
- corrected #178 containment;
- corrected #127 PlayMode;
- #183 source provenance;
- applicable #223/#150 Player evidence.

## 36. Retained matrix/vector artifacts

The pure phase includes machine-readable fixtures for:

```text
definition valid/invalid matrix
legacy alias map
state compatibility matrix
cost/duration/prerequisite vectors
order state transition table
start/completion replay/conflict matrix
legacy timer migration vectors
research effect snapshot vectors
production snapshot vectors
canonical plan/result hashes
```

Each artifact records schema/policy/source version and expected diagnostics/status/revision.

## 37. Implementation phases

### Phase P1 — pure contracts, validation, and planners

Branch:

```text
codex/progression-contract-planner
```

Allowed:

- immutable definition/state/snapshot/order/request/plan/result contracts;
- stable status/diagnostic enums;
- legacy ID alias resolver over supplied data;
- pure building/research/troop compatibility validators;
- pure effective queries over supplied definitions/raw state;
- checked cost/duration/prerequisite planners;
- pure start/completion/cancellation/reconciliation planners;
- pure research effect and building production snapshot planners;
- legacy timer migration planner over fake/raw snapshots;
- fake economy/quest/clock/catalog targets;
- retained matrices/vectors;
- focused EditMode tests;
- technical documentation.

Prohibited:

- production building/research/training/resource/save/quest/controller changes;
- `ServiceLocator` or Bootloader changes;
- save fields/migrations;
- real catalogs/content;
- scenes/Build Settings/UI;
- balance changes;
- Android.

Use:

```text
Refs #165
```

Do not close #165.

### Phase P2 — technical source

After #156/#183 and source review:

- building/research/troop definitions;
- cost/duration/prerequisite/effect/production/battle profiles;
- stable ID/alias migration tables;
- schemas/generated contracts;
- source hashes/provenance;
- migration report for every current hard-coded/runtime/Android ID and value;
- explicit unresolved balance/content decisions.

### Phase P3 — read-only service/query migration

After P1/P2:

- typed immutable query interfaces;
- compatibility snapshots over current saves;
- removal of query-time row creation/repair;
- consumer migration to fail-closed immutable snapshots;
- no command/persistence changes yet where transaction dependencies remain blocked.

### Phase P4 — order/persistence transaction

After corrected #152/#163 and accepted #137:

- explicit saved order/ledger/outbox fields under declared `SaveGameData.cs` lock;
- legacy timer/ID/state migration;
- start/completion/cancellation/reconciliation application;
- checked no-save economy/quest adapters;
- fault/reload/concurrency/duplicate matrix;
- committed receipt query.

### Phase P5 — production/offline/controller integration

After corrected #153/#178 and #163 production planner:

- one lifecycle-owned reconciler;
- remove controller polling/load/state creation;
- remove save-service direct timer/resource progression;
- live/offline production parity;
- research/battle/Champion snapshots;
- Kingdom presentation and notifications;
- safe PlayMode and applicable Player evidence.

## 38. Expected file boundaries

### P1 likely

```text
unity/Assets/AL/Scripts/Kingdom/Progression/Contracts/**
unity/Assets/AL/Scripts/Kingdom/Progression/Validation/**
unity/Assets/AL/Scripts/Kingdom/Progression/Planning/**
unity/Assets/AL/Tests/EditMode/Progression/**
unity/SharedContracts/** only with declared generated/shared scope
```

Use existing assemblies/namespaces when narrower. Avoid one monolithic service rewrite.

### Later production files

```text
unity/Assets/AL/Scripts/Core/Interfaces/IBuildingService.cs
unity/Assets/AL/Scripts/Core/Interfaces/IResearchService.cs
unity/Assets/AL/Scripts/Core/Interfaces/ITrainingService.cs
unity/Assets/AL/Scripts/Services/Local/LocalBuildingService.cs
unity/Assets/AL/Scripts/Kingdom/Research/LocalResearchService.cs
unity/Assets/AL/Scripts/Services/Local/LocalTrainingService.cs
unity/Assets/AL/Scripts/Services/Local/LocalResourceService.cs
unity/Assets/AL/Scripts/Services/Local/LocalSaveGameService.cs
unity/Assets/AL/Scripts/UI/Kingdom/KingdomSceneController.cs
approved catalog/schema/source artifacts
focused tests/orchestrators
```

### Explicitly prohibited in P1

```text
Bootloader.cs
ServiceLocator.cs
SaveGameData.cs
LocalSaveGameService.cs
LocalGameDataService.cs
LocalResourceService.cs
LocalQuestService.cs
KingdomSceneController.cs
*.unity
EditorBuildSettings.asset
Android source
narrative/terrestrial source
```

## 39. Shared-file and lock policy

P1 requires no designated shared-file lock.

P2/P3 source/service migration may require the `LocalGameDataService.cs` lock only when that exact approved phase edits it.

P4 must declare the `SaveGameData.cs` lock and coordinate with #137, battle/boss/Champion/world/relationship ledgers, notification outbox, and NVS fields.

A P5 lifecycle change cannot edit `Bootloader.cs` unless it is part of or follows accepted #153 with an explicit lock.

No conflict resolution may discard valid current services, fields, tests, contracts, assets, or registrations.

## 40. Validation evidence

### 40.1 P1 canonical evidence

Run from:

```text
C:\Users\MY\Documents\AnotherLife\unity
Unity 2022.3.62f3
```

Record:

- exact base/head SHA;
- complete changed-file/classification list;
- compile/import command, exit, final markers, and error scan;
- focused Progression EditMode totals/XML/log;
- complete EditMode totals/XML/log;
- retained matrix/vector validation command/output;
- reflection/immutability checks;
- forbidden production/ServiceLocator/save/resource/quest/UI/source token scan;
- `git diff --check`;
- final status and every deferred check.

### 40.2 Later integration evidence

Add:

- strict valid/invalid source catalogs;
- old-save/duplicate/unknown/malformed/timer fixtures;
- fault injection at every candidate boundary;
- exact save/event/notification/resource/quest counts;
- duplicate/retry/reload/offline/clock/concurrency proof;
- corrected #127 PlayMode profile isolation;
- current production scene/Player evidence after #150 activation;
- user integrated progression/production playtest.

Duplicate-workspace, stale-base, skipped, compile-only, missing XML, development fallback, wrong-policy green, or `continue-on-error` results are not passing evidence.

## 41. Review questions

Codex coordination/review verifies:

- definitions and saved state are separated;
- every ID/version/hash/source is explicit;
- raw malformed/duplicate/unknown state is preserved and disabled, not repaired;
- queries are pure and return immutable snapshots;
- effective initial state is not persisted by reads;
- costs/durations/maxima/prerequisites use one source and checked arithmetic;
- start/completion/cancellation/reconciliation use explicit operation identities;
- resource, level/count, quest, ledger, outbox, persistence, and publication are atomic;
- timer means eligibility, not automatic mutation;
- online/offline completion and production use one source/planner;
- current values migrate without unapproved drift;
- no controller/service/save path retains parallel authority;
- canonical evidence is complete.

Codex narrative/content review is required when names, descriptions, unlock meaning, or player-facing copy changes. User approval is required for costs/durations/maxima/production/bonus/training/cancel/refund changes and final integrated pacing—not for pure P1 technical contracts.

## 42. Acceptance criteria

### Specification acceptance

- [x] Current definition, query, state, cost, timer, training, quest, production, offline, controller, and consumer defects are inventoried.
- [x] Engineering/content/balance/user authority is separated.
- [x] Stable identity/version/hash, definition, raw state, compatibility snapshot, effective query, order, request, plan, result, and ledger contracts are exact.
- [x] Duplicate/unknown/malformed/legacy state policy is explicit and nonmutating.
- [x] Costs, durations, prerequisites, maxima, arithmetic, and current tuning migration are exact.
- [x] Start/completion/cancellation/reconciliation state and transaction semantics are complete.
- [x] Research effect, production, battle, Champion, offline, UI, notification, replay, and failure boundaries are explicit.
- [x] Phase/file/lock/test/evidence requirements are implementation-ready.
- [x] No balance, authored content, save implementation, runtime behavior, scene, Android, or unrelated change is authorized.

### Issue completion acceptance

#165 remains open until:

- [ ] P1 pure contracts/validators/planners are implemented and accepted.
- [ ] Versioned authoritative building/research/troop/progression source exists.
- [ ] Read queries are pure and expose immutable typed state.
- [ ] Unknown/duplicate/malformed state is preserved and safely disabled.
- [ ] Start/completion/reconciliation transactions are atomic and idempotent.
- [ ] Legacy IDs/timers/states migrate without value or quest duplication.
- [ ] Economy and quest operations use accepted no-save adapters.
- [ ] Online/offline completion and production use one source/planner.
- [ ] Controller polling/load/state creation is removed.
- [ ] Battle/Champion consumers receive exact troop/research snapshots.
- [ ] Complete validation, fault, reload, duplicate, clock, PlayMode, and applicable Player evidence passes canonically.
- [ ] No unapproved balance, content, visual, Android, or unrelated change is included.

## 43. Immediate handoff

Codex engineering may now start only:

```text
branch: codex/progression-contract-planner
scope: P1 immutable definitions/state/snapshots/orders/results, legacy alias and compatibility validation, checked cost/duration/prerequisite planners, pure start/completion/reconciliation/effect/production snapshots, fake targets, matrices, and tests
completion link: Refs #165
shared locks: none
```

It must not edit production services, saves, catalogs, callers, scenes, UI, Android, balance, or close #165.
