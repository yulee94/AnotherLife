# Boss Loot Result and Transaction Integrity Specification

**Status:** Binding GPT technical specification for issue #168  
**Status date:** 2026-07-16  
**Audited base:** `74df89b62faff610595b0106bc5c0a5374290609`  
**Primary owner:** Codex engineering  
**Specification/review owner:** GPT  
**Named-item, lore, localization, and presentation meaning owner:** Codex narrative/content  
**Final product/creative approval:** User  
**Canonical Unity workspace:** `C:\Users\MY\Documents\AnotherLife\unity`

## 1. Purpose

This specification defines the authoritative boundary for boss reward computation, one-time reward application, owned-equipment validation, persistence, idempotency, notification, and presentation.

It replaces the current implicit sequence:

```text
boss component constructs mutable request
→ loot service repairs/fills missing fields
→ credits save immediately
→ item rolls use process-local randomness
→ inventory mutates
→ inventory may save again
→ raw success strings are logged
→ broad catch fabricates another reward
```

with this explicit sequence:

```text
validated encounter completion
→ immutable reward-computation request
→ deterministic side-effect-free computation
→ immutable computed result
→ prepared reward-application plan
→ one candidate save transaction
→ persist and verify
→ publish committed result
→ emit typed notification/presentation once
```

The specification does not authorize item-grade design, balance changes, narrative writing, save implementation, or Champion encounter redesign.

## 2. Binding dependencies

### 2.1 Required upstream contracts

Boss reward work consumes, rather than duplicates, these controls:

```text
unity/Docs/Save_Semantic_Compatibility_Policy.md
unity/Docs/Economy_Integrity_Spec.md
unity/Docs/Game_Data_Catalog_Authority_Spec.md
unity/Docs/Notification_Delivery_Contract_Spec.md
```

The current implementation sequence is:

```text
merged #156 QuestDefinition/asset authority
merged #163 economy implementation and contract
merged #127 profile-safe PlayMode support
merged #178 production-command containment
          +
#183 equipment/boss/loot-profile technical authority
          +
#137 crash-safe candidate persistence and transaction ledger
          +
#180 typed Champion encounter completion
          ↓
#168 production reward application and consumer migration
```

The pure computation/planning phase defined below may proceed before #137, #180, and production catalog migration because it mutates no service, save, scene, UI, or authored content.

### 2.2 Current blockers by phase

| Phase | Work | Current prerequisite |
| --- | --- | --- |
| B1 | Pure models, validators, deterministic computation, inventory validation, application planning against fake targets | This specification only |
| B2 | Versioned boss/loot/equipment technical source | #183 approved source artifacts |
| B3 | Candidate-save application, result ledger, durable outbox | #137 transaction/persistence implementation and a reviewed no-save economy primitive |
| B4 | Boss/Champion consumer migration | #180 typed encounter lifecycle and committed encounter identity |
| B5 | Visible result presentation | #177 presenter/content and owning scene/UI readiness |
| B6 | NVS-01 reward composition | approved #133 G1 and #134 implementation sequence |

## 3. Verified current-source baseline

### 3.1 Mutable request with reward defaults

Current `BossLootRequest` contains mutable public fields and silently authorizes value:

```text
BossId
BossName
PlayerDisplayName = "Anonymous player"
WarzoneCreditReward = 500
RandomSeed
List<EquipmentDefinition> LootTable
```

A request object is therefore simultaneously:

- encounter identity;
- content source;
- balance source;
- random source;
- player-facing copy source;
- mutable Unity-object container.

Those responsibilities must be separated.

### 3.2 Invalid request becomes a successful fallback reward

Current `LocalBossLootService.RollLoot(...)`:

- replaces a null request with a new default request;
- replaces blank boss identity with `boss_dummy` / `Boss Dummy`;
- clamps a negative credit amount to zero;
- treats an empty loot table as an automatic `ember_crown_shard` drop;
- replaces a blank equipment ID with the mutable Unity object name;
- clamps invalid drop rates into `[0,1]`.

Missing authority, malformed data, and explicit no-loot are therefore indistinguishable.

### 3.3 Partial and nested persistence

Current order is:

```text
IWarzoneCreditService.AddCredits(...)
→ service-local save
→ mutate OwnedEquipment
→ optional second save
→ notification
```

Consequences include:

- credit-only durable rewards;
- item-only in-memory rewards;
- no one-time result ledger;
- duplicate saves;
- no verified commit status;
- no rollback or recoverable pending state.

The current public `AddCredits(int)` wrapper saves independently and is not a valid primitive inside the boss reward transaction.

### 3.4 Process-dependent computation

Current default seeds use combinations of:

```text
string.GetHashCode()
Environment.TickCount
DateTimeOffset.UtcNow.Millisecond
Time.time
```

These values are not stable across process, platform, runtime, reload, or replay. `System.Random` behavior is also not the cross-runtime content contract.

### 3.5 Mutable and malformed inventory

Current `GetOwnedEquipment()` returns the save-backed list as `IEnumerable<OwnedEquipmentState>`. Callers can retain or downcast the collection and mutate persistent state outside validation and persistence rules.

Current stacking:

- dereferences null rows;
- selects the first matching ID;
- uses unchecked quantity addition;
- silently overwrites last-source metadata;
- silently ORs announcement policy;
- does not detect definition/stat mismatch;
- has no stable definition revision/hash;
- has no duplicate policy;
- has no applied-result provenance.

### 3.6 Boss-side reward fabrication

Current `BossDummyAI.Die()` catches every exception from `RollLoot(...)` and then:

- independently calls `AddCredits(...)`;
- logs an acquisition string;
- publishes a synthetic `BossLootResult` containing `Ember Crown Shard`;
- does not add that item to owned equipment.

A failure after the original credit save can double-credit. The clear UI can also show an item the player does not own.

### 3.7 Notification is not delivery evidence

Current notification methods accept raw strings and Console-log them. A log line is not a durable reward receipt, visible presentation, acknowledgement, or duplicate-safe announcement.

## 4. Authority and ownership

### 4.1 Technical authority

Codex engineering owns:

- immutable contract types;
- stable technical IDs;
- deterministic computation;
- validators;
- inventory snapshots;
- application plans;
- checked arithmetic;
- result ledger integration;
- persistence orchestration;
- typed notification requests;
- tests and evidence.

### 4.2 Authored source authority

Codex narrative/content owns:

- player-facing boss and item names;
- item descriptions and lore;
- localization keys and parameter meaning;
- world-announcement wording and tone;
- whether an acquisition is narratively world-announced;
- authored reward meaning where applicable.

Technical code does not own English copy or infer story importance from a Boolean alone.

### 4.3 Balance authority

No value changes in this specification.

The following observed current values are inventory evidence, not newly approved balance:

```text
BossDummyAI default Warzone Credits: 500
current equipment DropRate floats
current item stat fields
current independent-per-entry roll behavior
current quantity per successful item roll: 1
```

Migration must preserve an approved existing value exactly or stop for a separate balance decision. Codex engineering must not tune probabilities, credits, stats, or quantities while implementing integrity.

### 4.4 User authority

The user retains final approval of:

- named-item creative meaning;
- reward cadence and feel;
- balance changes;
- visible presentation;
- milestone/release acceptance.

## 5. Terminology

### 5.1 Encounter

A validated gameplay session whose lifecycle is owned by #180 or another approved encounter system.

### 5.2 Encounter completion

A durable or transaction-ready typed outcome proving that the encounter is eligible to request rewards. Boss health reaching zero by itself is not sufficient authority.

### 5.3 Reward computation

Pure deterministic conversion of an immutable validated request and immutable reward profile into an immutable computed result. It performs no save, service mutation, event, notification, Unity object creation, or time read.

### 5.4 Computed result

An immutable proposed reward. It is not owned inventory and must never be displayed as committed.

### 5.5 Reward application plan

An immutable plan that has validated current inventory/economy state and describes candidate mutations, ledger entry, and typed notification intents. It contains no mutable save-row or Unity-object references.

### 5.6 Committed result

A reward result whose credits, inventory, result ledger, and required durable notification outbox have persisted and verified through one transaction boundary.

### 5.7 Exact replay

A second request using the same result ID and the same canonical computation hash. It returns the existing committed record without applying value or emitting notification again.

### 5.8 Correlation conflict

Reuse of an existing result ID with different encounter, profile, content, credit, drops, or computation hash. It is an integrity failure, not a duplicate success.

### 5.9 Explicit no reward

A valid approved reward profile that intentionally grants zero credits and zero items. It is distinct from missing/invalid/unavailable reward data.

## 6. Required stable identities

### 6.1 IDs

The production contract requires:

```text
gameId
catalogSetId
bossDefinitionId
bossDefinitionContentVersion
rewardProfileId
rewardProfileContentVersion
encounterId
encounterCompletionId
rewardResultId
profileId or save identity token
itemDefinitionId per drop
notification correlation ID per committed semantic event
```

### 6.2 ID validation

Every required ID must be:

- non-null;
- nonblank after exact validation without silent trimming into another ID;
- within the shared maximum UTF-8 byte length;
- free of control characters;
- valid under the stable-ID convention approved by #183;
- resolved by the correct immutable catalog snapshot;
- case-sensitive unless an explicit alias table says otherwise.

Unknown future IDs in saves are preserved but unsupported. Unknown IDs in a new production reward request reject.

### 6.3 Result identity source

`rewardResultId` is issued by the encounter-result authority, not generated from wall clock, frame time, object instance, boss name, or random seed.

Recommended semantic derivation when the owning encounter contract permits it:

```text
rewardResultId = encounterCompletionId + ":boss_reward"
```

The exact format belongs to the encounter/result specification, but the mapping must be deterministic and one-to-one.

## 7. Versioned immutable definitions

### 7.1 Boss reward binding

A boss definition references one reward profile by stable technical ID. The runtime boss component must not supply credit amount or mutable loot entries.

Conceptual shape:

```csharp
public sealed class BossRewardBinding
{
    public string BossDefinitionId { get; }
    public string RewardProfileId { get; }
    public string RewardProfileContentVersion { get; }
}
```

### 7.2 Reward profile

Conceptual immutable shape:

```csharp
public sealed class BossRewardProfile
{
    public string GameId { get; }
    public string CatalogSetId { get; }
    public string Id { get; }
    public string SchemaVersion { get; }
    public string ContentVersion { get; }
    public int WarzoneCredits { get; }
    public bool IsExplicitNoReward { get; }
    public IReadOnlyList<BossRewardEntry> Entries { get; }
    public string SourceRevision { get; }
    public string RawSha256 { get; }
}
```

### 7.3 Reward entry

The first production probability representation is fixed-point millionths:

```csharp
public sealed class BossRewardEntry
{
    public string EquipmentDefinitionId { get; }
    public int DropChanceMicros { get; } // 0..1_000_000
    public int Quantity { get; }         // current migration preserves 1
    public string AcquisitionAnnouncementPolicyId { get; }
}
```

`DropChanceMicros` avoids binary floating-point interpretation drift across JSON, Fable, C#, tests, and future tools.

Existing approved float values migrate only through an explicit converter that proves exact intended mapping. The converter must report the source value and resulting micros; it must not round an ambiguous value silently.

### 7.4 Equipment definition snapshot

Computation consumes immutable technical equipment records, never mutable `EquipmentDefinition` ScriptableObjects.

Minimum technical fields:

```text
equipmentDefinitionId
schemaVersion
contentVersion
slot
attackBonus
defenseBonus
healthBonus
stackPolicyId
acquisitionSnapshotPolicyId
presentationContentKey
sourceRevision
rawSha256
```

Player-facing name/description resolve later through authored content.

### 7.5 Duplicate entries

A reward profile rejects duplicate `EquipmentDefinitionId` entries. Duplicate entries are not treated as two independent rolls unless a future approved schema defines distinct stable reward-entry IDs and the balance owner explicitly approves that meaning.

## 8. Immutable computation request

Conceptual shape:

```csharp
public sealed class BossRewardComputationRequest
{
    public string GameId { get; }
    public string CatalogSetId { get; }
    public string ProfileId { get; }
    public string EncounterId { get; }
    public string EncounterCompletionId { get; }
    public string RewardResultId { get; }
    public string BossDefinitionId { get; }
    public string BossDefinitionContentVersion { get; }
    public string RewardProfileId { get; }
    public string RewardProfileContentVersion { get; }
    public string DeterminismVersion { get; }
}
```

It does not contain:

- player-facing names;
- caller-provided credits;
- mutable loot lists;
- ScriptableObjects;
- Unity object names;
- timestamps used as randomness;
- delegates or services;
- raw display text.

## 9. Typed computation outcomes

Minimum status enum:

```text
Computed
ExplicitNoReward
InvalidRequest
CatalogUnavailable
UnsupportedVersion
UnknownBoss
UnknownRewardProfile
BossRewardBindingMismatch
InvalidRewardProfile
InvalidEquipmentDefinition
DeterminismFailure
```

Conceptual result:

```csharp
public sealed class BossRewardComputationResult
{
    public BossRewardComputationStatus Status { get; }
    public BossRewardComputedValue Value { get; }
    public IReadOnlyList<BossRewardDiagnostic> Diagnostics { get; }
}
```

Only `Computed` and `ExplicitNoReward` contain a valid immutable value.

## 10. Deterministic roll contract

### 10.1 Prohibited entropy

Production computation must not read:

```text
DateTime.Now / UtcNow
Environment.TickCount
Time.time / frame count
UnityEngine.Random
System.Random
string.GetHashCode()
object hash codes
process IDs
thread IDs
platform locale
mutable list enumeration order
```

### 10.2 Canonical input

For each reward entry, compute a canonical length-prefixed UTF-8 byte sequence containing exactly:

```text
determinismVersion
catalogSetId
rewardResultId
encounterCompletionId
bossDefinitionId
rewardProfileId
rewardProfileContentVersion
equipmentDefinitionId
```

Length prefix is an unsigned 32-bit big-endian byte count followed by the exact UTF-8 bytes. This avoids delimiter ambiguity.

Reward entries are validated unique and processed in ordinal `EquipmentDefinitionId` order.

### 10.3 Hash and draw

Use SHA-256 over the canonical bytes.

Read the first four digest bytes as an unsigned 32-bit big-endian value:

```text
draw ∈ [0, 4_294_967_295]
```

For `chance = DropChanceMicros`:

```text
scale = 4_294_967_296
thresholdExclusive = floor(chance * scale / 1_000_000)
```

The multiplication uses unsigned 64-bit checked arithmetic; its maximum is safe:

```text
1_000_000 × 4_294_967_296 = 4_294_967_296_000_000
```

Decision:

```text
chance == 0           → never drops
chance == 1_000_000   → always drops
otherwise             → drops when draw < thresholdExclusive
```

### 10.4 Why entry-specific hashing

Each item roll depends on stable identity rather than mutable list position. Adding an unrelated item in a later content version does not reshuffle every existing item through a shared PRNG sequence. The content-version change remains explicit in the canonical input.

### 10.5 Computation hash

After computing the result, serialize the normalized immutable result through a canonical deterministic writer and record:

```text
computationHash = SHA-256(canonical computed result)
```

The hash covers at minimum:

```text
result ID
encounter completion ID
boss ID/version
reward profile ID/version/hash
credit amount
explicit-no-reward flag
ordered item IDs/definition versions/quantities
ordered diagnostics that block application
algorithm/determinism version
```

## 11. Reward profile validation

Reject/report at minimum:

- null profile;
- blank IDs or versions;
- wrong game/catalog identity;
- unsupported schema/content version;
- boss/profile binding mismatch;
- negative credits;
- credits above approved technical maximum;
- contradictory explicit-no-reward with nonzero credits/items;
- null entries;
- duplicate item IDs;
- blank/unknown item ID;
- invalid item definition/version;
- drop chance outside `0..1_000_000`;
- quantity `<= 0`;
- quantity other than the approved migrated value without balance authorization;
- unknown announcement policy;
- conflicting source hashes;
- nondeterministic order or generated-contract drift.

A profile with zero entries is valid only when `IsExplicitNoReward` is true or when the approved schema explicitly allows credit-only reward. Missing data is not interpreted as no reward.

## 12. Computed reward value

Conceptual immutable shape:

```csharp
public sealed class BossRewardComputedValue
{
    public string RewardResultId { get; }
    public string EncounterId { get; }
    public string EncounterCompletionId { get; }
    public string BossDefinitionId { get; }
    public string RewardProfileId { get; }
    public string RewardProfileContentVersion { get; }
    public string RewardProfileSha256 { get; }
    public int WarzoneCredits { get; }
    public bool IsExplicitNoReward { get; }
    public IReadOnlyList<BossRewardComputedDrop> Drops { get; }
    public string DeterminismVersion { get; }
    public string ComputationHash { get; }
}
```

Drops are sorted by equipment ID and contain stable technical data only.

## 13. Inventory authority

### 13.1 One row per equipment ID

The first production policy keeps one owned-inventory row per stable equipment ID.

A stack increment is allowed only when:

- the existing row is valid;
- the equipment ID matches exactly;
- the acquisition snapshot fingerprint matches;
- the definition migration policy permits stacking;
- checked quantity addition succeeds.

Definition/snapshot drift rejects application until an explicit migration resolves it. It does not silently overwrite or merge incompatible rows.

### 13.2 Acquisition snapshot authority

Current saves already store slot and stat values. To avoid retroactive silent item mutation, the acquired technical stat snapshot remains authoritative for the owned stack until an explicit item migration changes it.

The catalog remains authoritative for:

- stable identity;
- definition/version validation;
- drop eligibility;
- presentation content references;
- migration rules.

The save snapshot remains authoritative for the acquired stack's approved technical values:

```text
slot
attack bonus
defense bonus
health bonus
acquisition snapshot fingerprint
```

Player-facing raw display strings are not authoritative. Existing `DisplayName` is preserved as legacy evidence during migration but later presentation resolves from content keys.

### 13.3 Required future inventory record

Conceptual persisted shape after #137 migration approval:

```text
equipmentDefinitionId
equipmentDefinitionContentVersion
acquisitionSnapshotFingerprint
slot
attackBonus
defenseBonus
healthBonus
quantity
firstAcquiredUtcSeconds
lastAcquiredUtcSeconds
lastSourceBossDefinitionId
lastSourceEncounterCompletionId
lastAppliedRewardResultId
schemaVersion
```

Optional bounded provenance history requires a separate approved retention policy. A single mutable `SourceBossId` is not full provenance.

### 13.4 Legacy fields

Current fields are classified as:

| Field | Migration treatment |
| --- | --- |
| `EquipmentId` | stable identity candidate; validate/alias through #183 |
| `DisplayName` | preserve as legacy opaque evidence; not authoritative copy |
| `Slot` and stat bonuses | acquisition snapshot candidate; validate against migration policy |
| `Quantity` | validate positive/checked |
| `SourceBossId` | legacy last-source evidence |
| `AnnounceWorldDrop` | legacy presentation hint; not future technical authority |
| first/last timestamps | validate UTC-second semantics and ordering |

## 14. Inventory validation result

Minimum domain states:

```text
Valid
Empty
Unavailable
MalformedNullCollection
MalformedNullEntry
MalformedBlankId
MalformedDuplicateId
MalformedUnknownRequiredDefinition
PreservedUnknownFutureDefinition
MalformedQuantity
MalformedSnapshot
MalformedTimestamp
MalformedProvenance
UnsupportedVersion
```

Validation is pure. It does not:

- instantiate a list;
- delete null rows;
- choose first/last duplicate;
- clamp quantity;
- rewrite IDs;
- replace stats;
- repair timestamps;
- save.

A malformed inventory disables reward application. Recovery/repair belongs to #137.

## 15. Read-only inventory queries

`GetOwnedEquipment()` must be replaced or supplemented by a typed query returning immutable snapshots.

Conceptual result:

```csharp
public sealed class OwnedEquipmentQueryResult
{
    public OwnedEquipmentQueryStatus Status { get; }
    public IReadOnlyList<OwnedEquipmentSnapshot> Items { get; }
    public IReadOnlyList<BossRewardDiagnostic> Diagnostics { get; }
    public string InventoryRevision { get; }
}
```

Requirements:

- caller cannot mutate backing state;
- no saved row object is returned;
- ordering is deterministic by equipment ID;
- query allocates predictably or uses a reviewed immutable cache invalidated only after publish;
- query does not save or repair;
- unknown future rows can be represented as preserved opaque snapshots with unsupported status.

## 16. Reward application request

Application consumes an immutable computed result and current immutable domain snapshots.

Conceptual shape:

```csharp
public sealed class BossRewardApplicationRequest
{
    public BossRewardComputedValue ComputedReward { get; }
    public string ExpectedSaveRevision { get; }
    public string ExpectedEconomyRevision { get; }
    public string ExpectedInventoryRevision { get; }
    public string ExpectedCatalogSetId { get; }
    public string ApplicationPolicyVersion { get; }
}
```

The request does not contain mutable service or save references.

## 17. Application planning

### 17.1 Pure planner

The planner validates:

- computation status/hash;
- result ID and encounter identity;
- catalog versions/hashes;
- current save/profile availability;
- economy state and credit arithmetic;
- inventory validity;
- item snapshot compatibility;
- quantity arithmetic;
- current result-ledger state;
- notification-definition availability where durability requires outbox staging;
- expected revisions.

It returns an immutable plan or typed rejection.

### 17.2 Plan shape

Conceptual fields:

```text
rewardResultId
computationHash
expected save/economy/inventory/catalog revisions
credit previous/delta/new
ordered inventory row create/update operations
applied-result ledger record
ordered durable notification outbox records
post-commit event records
application policy version
plan hash
```

The plan contains no save-row references and no `ScriptableObject` references.

### 17.3 Stale plan

If any expected revision differs at apply time, return `StalePlan`. Do not silently recompute or rebase inside apply. The caller may obtain fresh snapshots and explicitly plan again.

## 18. Applied reward ledger

### 18.1 Ledger key

Primary key:

```text
rewardResultId
```

The record stores:

```text
rewardResultId
encounterId
encounterCompletionId
bossDefinitionId
rewardProfileId/contentVersion/hash
computationHash
credit amount
ordered committed drops
committed UTC timestamp
application policy version
notification correlation IDs
```

### 18.2 Duplicate behavior

| Condition | Outcome |
| --- | --- |
| no existing ledger record | prepare first application |
| same result ID + same computation hash + matching semantic fields | `AlreadyCommitted`; return stored committed result; no mutation/notification |
| same result ID + different hash or semantic fields | `CorrelationConflict`; block |
| existing pending/uncertain transaction | defer to #137 recovery status; do not apply again |
| stored record malformed | disable domain and require recovery |

### 18.3 Ledger ownership

The ledger is part of the owning save transaction framework. Boss loot must not create a second independent persistence mechanism that can disagree with #137 or future NVS transaction ledgers.

## 19. Candidate transaction

### 19.1 Required order

```text
1. read validated current snapshots
2. compute or load immutable computed result
3. detect exact replay/conflict
4. prepare complete reward plan
5. clone validated save candidate
6. apply checked credit mutation through no-save primitive to candidate
7. apply checked inventory operations to candidate
8. add applied-result ledger record to candidate
9. add required durable notification outbox records to candidate
10. validate complete candidate semantics
11. persist candidate through #137
12. reload/verify persisted candidate where policy requires
13. publish candidate/current revisions
14. emit post-commit domain event
15. enqueue session notification/presentation once
```

### 19.2 Prohibited operations

Inside steps 5–10, do not call:

```text
IWarzoneCreditService.AddCredits
IWarzoneCreditService.SpendCredits
ISaveGameService.Save
LocalNotificationService.ShowMessage
Unity events
UI callbacks
BossLootResult display callbacks
```

The economy integration must expose or consume the reviewed no-save candidate mutation primitive. A public compatibility wrapper that saves immediately is not acceptable.

### 19.3 Failure

A failure before durable persistence publishes nothing and returns a typed failed/not-committed result.

A persistence result whose durable state is uncertain returns `CommitUncertain` and enters #137 recovery. It must not retry value application blindly or show success.

A notification presenter failure after verified commit does not roll back value. The typed receipt records notification delivery failure while the committed result remains queryable and the durable outbox remains pending when required.

## 20. Typed application outcomes

Minimum statuses:

```text
Committed
AlreadyCommitted
ExplicitNoRewardCommitted
InvalidComputedResult
CorrelationConflict
SaveUnavailable
CatalogDrift
EconomyUnavailable
EconomyInvalid
InventoryUnavailable
InventoryMalformed
DefinitionSnapshotConflict
QuantityOverflow
CreditOverflow
StalePlan
PersistenceFailed
CommitUncertain
UnsupportedVersion
InternalInvariantFailure
```

Only these statuses may expose `CommittedBossRewardResult`:

```text
Committed
AlreadyCommitted
ExplicitNoRewardCommitted
```

A computed result is never substituted.

## 21. Committed result query

UI and callers query by `rewardResultId` or encounter completion ID.

The query distinguishes:

```text
Committed
ExplicitNoReward
NotFound
PendingRecovery
MalformedLedger
UnsupportedVersion
Unavailable
```

It returns an immutable receipt derived from the persisted ledger, not from boss component memory.

## 22. Notification integration

### 22.1 Notification definitions

Boss rewards use #177 typed definitions, not raw strings.

Conceptual semantic notification IDs:

```text
boss_reward.credits_committed
boss_reward.item_acquired
boss_reward.world_item_acquired
boss_reward.explicit_no_reward
boss_reward.commit_failed_blocking_or_recoverable
```

Exact player-facing copy, keys, tone, and world-announcement meaning are authored source decisions.

### 22.2 Correlation

Recommended correlation IDs:

```text
rewardResultId + ":credits"
rewardResultId + ":item:" + equipmentDefinitionId
rewardResultId + ":failure"
```

Exact replay produces no duplicate presentation.

### 22.3 Privacy

Do not persist or display:

- local file paths;
- exception messages;
- stack traces;
- raw save revisions;
- internal catalog hashes as player copy;
- unsanitized player-provided display names.

### 22.4 Console behavior

Console diagnostics are allowed with stable technical codes. Console logging never changes a notification receipt to `Presented` or a reward result to `Committed`.

## 23. Boss and encounter integration

### 23.1 Boss component responsibility

`BossDummyAI` may own local combat behavior only until #180 migrates it. It must not own reward definition, balance, persistence, fallback value, or final presentation.

The production boss-defeat path becomes conceptually:

```text
boss reaches validated terminal defeat
→ encounter authority commits/produces EncounterCompletion
→ reward orchestrator requests deterministic computation
→ reward orchestrator applies or reports typed status
→ scene observes committed receipt
```

### 23.2 Required removal

Production code removes:

- broad catch that grants credits;
- synthetic fallback drop;
- `CreateFallbackLootResult()`;
- `GrantFallbackLoot()`;
- `Time.time` / `GetHashCode()` reward seed;
- boss-side credit amount authority;
- boss-side mutable loot-table authority after catalog migration;
- raw acquisition text.

### 23.3 Missing service

Missing reward service returns `Unavailable` through the encounter/result boundary. It does not grant a fallback and does not represent the encounter as reward-complete.

### 23.4 Destruction timing

Destroying the boss object must not destroy the authoritative encounter/reward operation. The operation is owned by a durable/session orchestrator with stable identity. Scene object lifetime is presentation/runtime input, not transaction lifetime.

## 24. Champion clear UI integration

The clear UI may display only:

- committed credit amount;
- committed item snapshots/content references;
- explicit no-reward outcome;
- duplicate committed receipt;
- recoverable pending state;
- visible failed/unavailable state.

It must not display:

- computed-but-uncommitted drops;
- fallback drops;
- caller-requested credits;
- a result emitted from a catch block;
- mutable inventory objects;
- raw exception text.

The UI must tolerate the boss GameObject being destroyed before the committed result arrives.

## 25. Explicit no-reward encounters

A valid no-reward encounter requires:

```text
resolved boss definition
resolved reward profile
IsExplicitNoReward = true
credits = 0
entries = empty
valid encounter/result identity
```

It produces an idempotent ledger record so repeated encounter completion cannot later be reinterpreted as missing and rewarded.

Whether a player-visible no-reward message exists is an authored/presentation decision.

## 26. Legacy compatibility and migration

### 26.1 Current saves

Current saves have no applied boss-reward ledger. Existing inventory therefore cannot reliably prove which encounter granted each quantity.

Migration must not fabricate historical ledger entries or infer completed encounter rewards from `SourceBossId`.

Policy:

- preserve valid existing owned equipment as legacy inventory;
- preserve unknown future item IDs opaquely;
- validate and disable malformed inventory domains;
- do not grant compensating credits/items merely because provenance is absent;
- start new result-ledger authority only from an explicit migration/version marker;
- document rollback/downgrade behavior.

### 26.2 Duplicate legacy inventory

Ordinary query/application does not merge, select, delete, or repair duplicate rows. The inventory domain becomes invalid for mutation until #137 explicit repair handles it.

### 26.3 Legacy display/stat drift

Do not overwrite legacy acquisition snapshots from current catalog definitions during query or reward application. A focused migration may reconcile them only with a documented compatibility rule and tests.

### 26.4 Unknown future records

Unknown stable item rows survive save/load/downgrade exactly. They are excluded from new stacking or gameplay-stat use unless a compatible definition resolves them.

## 27. Diagnostics

Every diagnostic includes:

```text
stable code
severity
domain
operation/result ID when safe
boss/profile/item technical ID when safe
field/path
catalog/schema/content version
blocks computation/application/presentation boolean
safe developer message
```

Example code families:

```text
AL-BOSS-REWARD-REQUEST-*
AL-BOSS-REWARD-CATALOG-*
AL-BOSS-REWARD-DETERMINISM-*
AL-BOSS-REWARD-INVENTORY-*
AL-BOSS-REWARD-LEDGER-*
AL-BOSS-REWARD-TRANSACTION-*
AL-BOSS-REWARD-NOTIFICATION-*
AL-BOSS-REWARD-CONSUMER-*
```

Diagnostics order is deterministic by severity, code, record ID, and field path.

## 28. Concurrency and reentrancy

The transaction owner serializes applications per profile/save revision.

Requirements:

- two threads/tasks applying the same result converge to one commit and one duplicate receipt;
- different results cannot both plan from the same revision and silently overwrite;
- stale second plan rejects;
- event subscribers cannot reenter application for the same result before publication completes;
- notification callbacks cannot mutate the reward transaction;
- cancellation after persistence begins does not report a clean cancellation unless durable state is known.

Unity main-thread restrictions for presentation do not define persistence correctness.

## 29. Lifetime and memory

- computed values and snapshots are plain immutable data;
- no runtime `ScriptableObject.CreateInstance` is used for results;
- no backing save lists escape;
- cached immutable inventory snapshots are invalidated only on committed publication;
- repeated computation/application query does not leak Unity objects;
- catalog snapshot lifetime follows #183;
- scene unload does not invalidate committed-result queries.

## 30. Security and abuse resistance

Reject:

- caller-provided credit values;
- caller-provided mutable loot definitions;
- arbitrary item IDs not bound by the reward profile;
- result ID reuse with changed payload;
- extreme collection sizes beyond schema limits;
- integer overflow;
- noncanonical strings/control characters;
- hostile notification parameters;
- arbitrary action/scene/URL injection;
- fabricated debug fallback in production.

Development fixtures are explicitly marked and excluded from production catalog-set manifests and acceptance evidence.

## 31. Required tests

### 31.1 Request and identity

- valid request;
- null request;
- blank each required ID;
- oversized/control-character ID;
- wrong game/catalog set;
- unknown boss;
- unknown reward profile;
- boss/profile binding mismatch;
- unsupported version;
- profile/save identity mismatch;
- distinct encounter/result identities remain distinct.

### 31.2 Profile validation

- valid credit-only profile where explicitly allowed;
- valid item-only profile;
- valid mixed profile;
- valid explicit no-reward profile;
- contradictory no-reward profile;
- negative/overflow credit;
- null entry;
- blank item ID;
- duplicate item ID;
- unknown item;
- unsupported item version;
- chance below/above range;
- chance 0 and 1,000,000;
- quantity zero/negative;
- unapproved quantity change;
- unknown announcement policy;
- hash/source mismatch.

### 31.3 Determinism

- same canonical request/profile produces identical result and computation hash repeatedly;
- result remains identical after process/service reconstruction;
- result remains identical across supported runtime/platform test vectors;
- different result ID changes draws;
- different content version changes canonical hash;
- list input order does not change outcome;
- adding unrelated entry does not change existing entry's draw for that content version policy;
- exact SHA-256 byte-vector fixtures;
- big-endian draw and threshold boundary vectors;
- no call to prohibited entropy sources.

### 31.4 Computation purity

- no save call;
- no economy call;
- no inventory mutation;
- no notification/event;
- no Unity object creation;
- source definitions remain unchanged;
- repeated computation is idempotent.

### 31.5 Inventory validation

- null collection;
- empty valid collection;
- null entry;
- blank ID;
- duplicate ID;
- unknown future ID preservation;
- missing required definition;
- zero/negative quantity;
- maximum valid quantity;
- quantity overflow plan;
- invalid slot/stat snapshot;
- definition/snapshot fingerprint mismatch;
- missing/reversed/negative timestamp;
- malformed provenance;
- deterministic ordering/revision;
- returned snapshots cannot mutate backing state.

### 31.6 Planning

- valid first application;
- exact credit previous/delta/new;
- new item row;
- valid stack increment;
- multiple ordered item operations;
- explicit no-reward ledger plan;
- existing exact ledger record;
- result-ID conflict;
- malformed ledger;
- invalid economy;
- invalid inventory;
- credit overflow;
- quantity overflow;
- catalog drift;
- stale revision;
- notification-definition unavailable where required;
- plan contains no mutable references;
- plan hash deterministic.

### 31.7 Transaction and persistence

After #137 integration:

- first application commits credits/items/ledger/outbox once;
- exact replay before reload;
- exact replay after reload;
- two concurrent exact applications;
- two different plans from same revision;
- failure before credit apply;
- failure after credit apply in candidate;
- failure during item apply;
- failure adding ledger;
- failure adding outbox;
- semantic candidate validation failure;
- persistence write failure;
- verification failure;
- uncertain commit recovery;
- retry after each known-not-committed boundary;
- no partial published state;
- exact save count;
- exact post-commit event count;
- exact notification count.

### 31.8 Boss consumer

After #180 integration:

- one validated defeat produces one reward request;
- duplicate death callback cannot request twice;
- missing reward service;
- invalid profile;
- computation failure;
- application failure;
- exact replay;
- boss object destroyed before receipt;
- no fallback credits/item/result;
- no raw success string;
- no `Time.time`, `GetHashCode`, or caller credit authority.

### 31.9 Clear UI

- committed result shown;
- explicit no reward shown/handled per content policy;
- duplicate committed receipt shown once;
- pending recovery state;
- failed/unavailable state;
- computed-only result not shown;
- UI drop matches owned inventory;
- long/localized item names;
- missing content key safe fallback;
- scene transition and presenter detach;
- no duplicate announcement.

### 31.10 Regression

- approved current valid item IDs/values migrate without drift;
- current independent-entry roll semantics preserved;
- current approved quantity semantics preserved;
- Warzone Credits use economy checked semantics;
- save/reload round trip;
- profile-safe #127 PlayMode coverage;
- ShellFoundation production UI does not expose Champion reward path before packaging approval;
- Android unaffected.

## 32. Test vector artifact

The deterministic computation PR must include a retained machine-readable vector file containing:

```text
vector schema version
canonical input fields
canonical input bytes or hex
SHA-256 digest
UInt32 draw
chance micros
threshold
expected hit
expected ordered result
expected computation hash
```

At minimum cover:

- chance 0;
- chance 1;
- current representative rare chance values;
- chance 999,999;
- chance 1,000,000;
- multiple IDs and Unicode rejection/handling boundaries;
- cross-runtime producer/consumer verification where shared contracts are generated.

## 33. Implementation phases

### Phase B1 — pure contract and planner

Branch:

```text
codex/boss-loot-contract-planner
```

Allowed:

- immutable contract records;
- validators;
- deterministic SHA-256 computation;
- canonical writer;
- immutable inventory snapshot validator over supplied plain data;
- pure application planner over fake economy/inventory/ledger snapshots;
- deterministic diagnostics;
- test vectors and focused EditMode tests;
- technical documentation updates.

Prohibited:

- `LocalBossLootService.cs` behavior change;
- `BossDummyAI.cs` change;
- `ChampionArenaSceneController.cs` change;
- `SaveGameData.cs`;
- `Bootloader.cs`;
- `LocalWarzoneCreditService.cs` compatibility wrapper;
- real catalog content;
- balance/content/UI/notification presenter;
- scenes/Build Settings/Android.

PR references:

```text
Refs #168
```

It does not close #168.

### Phase B2 — technical source artifacts

After #183 source authority:

- boss reward bindings;
- reward profiles;
- equipment technical snapshots;
- schemas/generated contracts;
- strict validators;
- source hashes/provenance;
- migration report proving no approved numeric drift.

Source-mode content changes remain separate when player-facing meaning is affected.

### Phase B3 — persisted application

After #137 and a reviewed candidate economy primitive:

- save fields/migration under explicit shared-file lock;
- applied-result ledger;
- candidate inventory/economy operations;
- durable notification outbox where required;
- persist/verify/publish integration;
- fault and reload matrix.

### Phase B4 — consumer migration

After #180 encounter identity:

- remove boss fallback reward behavior;
- consume typed encounter completion;
- query committed receipt;
- migrate clear UI;
- prove no duplicate callback or fabricated success.

### Phase B5 — presentation/content

After #177 and content source approval:

- localization/content definitions;
- visible acquisition/commit/failure presentation;
- accessibility and scene-transition behavior;
- exact user review where presentation decisions require it.

## 34. Expected file boundaries

### Phase B1 likely files

```text
unity/Assets/AL/Scripts/Core/BossRewards/**
unity/Assets/AL/Tests/EditMode/BossRewards/**
unity/SharedContracts/Schemas/** only when generated/shared-contract scope is declared
unity/Docs/Boss_Loot_Result_Transaction_Spec.md references only
```

Use existing assembly boundaries when narrower. Do not create a broad architecture layer unnecessarily.

### Later files only when their phase activates

```text
unity/Assets/AL/Scripts/Core/Interfaces/IBossLootService.cs
unity/Assets/AL/Scripts/Services/Local/LocalBossLootService.cs
unity/Assets/AL/Scripts/ChampionMode/AI/BossDummyAI.cs
unity/Assets/AL/Scripts/ChampionMode/ChampionArenaSceneController.cs
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
approved catalog/schema/source artifacts
```

### Explicitly prohibited in the first PR

```text
Bootloader.cs
ServiceLocator.cs
ProjectInitializer.cs
EditorBuildSettings.asset
*.unity
Android source
narrative packets
terrestrial source
item-grade/VFX source
```

## 35. Shared-file and lock policy

No designated shared-file lock is required for Phase B1.

A later persisted application PR must declare the `SaveGameData.cs` lock before editing and coordinate with #137, notification outbox, relationship/world-state ledgers, and NVS transaction fields.

A later service migration must declare overlap with any active `LocalBossLootService.cs`, `BossDummyAI.cs`, or Champion controller PR before editing.

No conflict resolution may discard valid current economy, save, encounter, catalog, notification, or scene work.

## 36. Validation evidence

### Phase B1 required canonical evidence

Run from:

```text
C:\Users\MY\Documents\AnotherLife\unity
Unity 2022.3.62f3
```

Record:

- exact base/head SHA;
- complete changed-file list;
- compiler/import exit and final markers;
- focused BossRewards EditMode tests with discovered/passed/failed/skipped totals and XML;
- complete EditMode suite totals/XML;
- deterministic vector command/output;
- prohibited entropy/token scan;
- mutable-reference/reflection checks;
- `git diff --check`;
- final status;
- every unavailable or deferred validation.

### Later application evidence

Add:

- old-save fixtures;
- duplicate/malformed/unknown inventory fixtures;
- fault injection at every transaction boundary;
- save-count/event-count/notification-count matrix;
- exact replay before/after reload;
- concurrent application proof;
- safe #127 PlayMode evidence;
- committed-result UI/inventory consistency;
- packaging evidence only when the owning Player profile includes ChampionArena.

Skipped, stale-base, duplicate-workspace, compile-only, missing XML, fabricated fallback, or `continue-on-error` evidence is not passing.

## 37. Required review questions

Codex coordination/review verifies:

- no balance or content change;
- pure computation has no side effects;
- deterministic hash/input contract is exact;
- all definitions and snapshots are immutable;
- invalid data cannot become reward/no-reward silently;
- inventory query cannot expose backing state;
- plan contains no mutable references;
- current nested-save credit wrapper is not used;
- result ledger exact replay/conflict semantics are correct;
- no boss/UI fallback fabrication remains in the relevant phase;
- evidence is canonical and complete.

Codex narrative/content review is required only when authored names, copy, lore, content keys, or world-announcement meaning change.

User approval is required for creative or balance decisions and final integrated reward presentation, not for the pure technical B1 contract implementation.

## 38. Acceptance criteria

### Specification acceptance

- [x] Current request, fallback, randomness, partial persistence, inventory, boss, and notification defects are inventoried.
- [x] Technical/content/balance/user authorities are separated.
- [x] Stable IDs, definitions, request, computed result, plan, committed result, and ledger contracts are exact.
- [x] Cross-runtime deterministic SHA-256 roll behavior is exact and test-vector ready.
- [x] Explicit no reward is distinct from invalid/missing data.
- [x] Owned-equipment snapshot/definition authority and malformed-state policy are explicit.
- [x] Query purity and immutable collection behavior are explicit.
- [x] Credits, items, ledger, outbox, persistence, publication, events, and presentation have one sequence.
- [x] Exact replay, conflict, stale plan, overflow, failure, and uncertain-commit semantics are explicit.
- [x] Boss and clear-UI fallback fabrication removal is specified.
- [x] Phase/file/lock/test/evidence boundaries are exact.
- [x] No balance, item-grade, narrative, save implementation, scene, Android, or unrelated change is authorized.

### Issue completion acceptance

Issue #168 remains open until:

- [ ] Phase B1 pure contracts/planner are implemented and accepted.
- [ ] Approved boss/reward/equipment technical source exists.
- [ ] Persisted candidate application and result ledger are implemented under #137.
- [ ] Credits use a reviewed no-save transaction primitive.
- [ ] Owned inventory is strictly validated and queried immutably.
- [ ] `BossDummyAI` cannot fabricate or duplicate rewards.
- [ ] Champion clear UI consumes committed receipts only.
- [ ] Duplicate/reload/concurrent/fault tests pass.
- [ ] Canonical Unity compile, EditMode, safe PlayMode, and applicable Player evidence pass.
- [ ] No unapproved balance, content, visual, Android, or unrelated change is included.

## 39. Immediate handoff

Codex engineering may now start only:

```text
branch: codex/boss-loot-contract-planner
scope: Phase B1 pure contracts, validators, deterministic computation, snapshots, planner, fake targets, tests
completion link: Refs #168
shared locks: none
```

It must not claim boss rewards fixed, close #168, mutate saves/services, or change player-visible behavior.
