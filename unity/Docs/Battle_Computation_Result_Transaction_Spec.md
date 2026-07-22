# Battle Computation and Result Transaction Integrity Specification

**Status:** Binding GPT technical specification for issue #174  
**Status date:** 2026-07-16  
**Audited base:** `7156719766621e66cc218ed02b537b77779aab1b`  
**Primary implementation owner:** Codex engineering  
**Specification/review owner:** GPT  
**Player-facing battle copy/content owner:** Codex narrative/content  
**Balance and final product approval:** User  
**Canonical Unity workspace:** `C:\Users\MY\Documents\AnotherLife\unity`

## 1. Purpose

This specification defines the authoritative boundary for:

- battle request identity and validation;
- immutable army and modifier snapshots;
- checked fixed-point combat arithmetic;
- cross-runtime deterministic round and reward computation;
- side-effect-free technical results;
- preview versus authoritative execution;
- one-time troop/reward/progression result application;
- result ledger, persistence, notification, and UI handoff;
- compatibility migration from the current mutable models.

It replaces the current implicit flow:

```text
mutable request, possibly null
→ fill missing request/lists/default seed
→ read global research service
→ clamp malformed context into neutral values
→ compute with process-runtime System.Random and floats
→ create report with proposed value and player-facing strings
→ mutate WinBattle quest directly
→ let callers decide whether/how to apply report fields
```

with:

```text
validated immutable context and army snapshots
→ pure deterministic computation
→ immutable computed battle result
→ authoritative result-application plan or preview-only receipt
→ one candidate save transaction
→ persist and verify
→ publish committed battle result
→ emit typed events/notifications/presentation once
```

The specification does not rebalance combat, authorize multiplayer/network authority, redesign Champion action combat, author battle narrative, or implement NVS-01.

## 2. Binding dependencies and phase boundary

### 2.1 Related contracts

Battle integrity consumes rather than duplicates:

```text
unity/Docs/Save_Semantic_Compatibility_Policy.md
unity/Docs/Economy_Integrity_Spec.md
unity/Docs/Game_Data_Catalog_Authority_Spec.md
unity/Docs/Notification_Delivery_Contract_Spec.md
unity/Docs/Boss_Loot_Result_Transaction_Spec.md
```

### 2.2 Dependency sequence

```text
pure battle contract/validator/computation phase
          ↓
#183 versioned troop/terrain/rules/reward technical source
          +
#165 authoritative troop inventory and no-save loss mutation
          +
accepted #163 no-save economy mutation
          +
accepted #152 quest compatibility and typed no-save quest mutation
          +
accepted #137 candidate persistence/result ledger/outbox
          ↓
authoritative battle-result application
          +
#166 territory consequence adapter where applicable
          +
#168 boss reward operation where applicable
          +
#177 visible committed-result delivery
          ↓
NVS/production consumer integration under owning issues
```

### 2.3 Phase authorization

The first pure phase may proceed now because it mutates no save, service, scene, UI, catalog, or player content.

Production application remains blocked by reopened #152, #163, and #137 plus #165 and the relevant consequence owner.

## 3. Verified current-source baseline

### 3.1 Request mutation and fabricated defaults

Current `Simulate(BattleRequest request)`:

```csharp
request ??= new BattleRequest();
request.AttackerTroops ??= new List<TroopStack>();
request.DefenderTroops ??= new List<TroopStack>();
```

`BattleRequest` also supplies implicit authority:

```text
RandomSeed = 12345
AttackerRealm = Crownlands
DefenderRealm = None
AttackerMorale = 1
DefenderMorale = 1
```

A null request or missing lists therefore become a normal request. Empty armies obtain at least one power each through `Mathf.Max(1, ...)` and can produce a winner, losses, XP, credits, loot, summary text, and quest progress.

### 3.2 Malformed armies

Current loops:

- dereference null stacks;
- accept zero/negative counts;
- accept duplicate troop types;
- accept undefined troop enum values and assign default base power `1`;
- use unchecked `int` multiplication and accumulation;
- use unchecked total-count addition;
- clamp negative counts only later in casualty reporting, after they affected power/counters/outcome.

### 3.3 Mutable global modifier reads

The simulator obtains attack/defense modifiers from `ServiceLocator` and catches every exception:

```text
missing service
partial service stack
invalid research state
subscriber/service failure
```

all silently become zero research bonus. The same request can therefore compute differently depending on ambient service state while retaining no modifier revision/provenance.

### 3.4 Invalid context becomes neutral

Current behavior:

- nonpositive morale becomes `1` before clamping;
- `NaN`/infinity is not rejected explicitly;
- `RealmId.None` becomes a neutral `1` multiplier;
- undefined realm enum values become a neutral `1` multiplier;
- terrain is interpreted by lowercase substring matching;
- unknown terrain becomes neutral;
- blank/unknown Boss ID is ignored by computation;
- undefined battle type can pass through several generic paths.

### 3.5 Process/runtime-specific randomness

Current computation uses `System.Random(int)` with a magic seed fallback. The contract does not define cross-runtime output, canonical seed derivation, or result vectors.

### 3.6 Simulation side effect

On attacker victory, the simulator calls:

```csharp
IQuestService.UpdateProgress(QuestType.WinBattle, 1)
```

and catches every exception. Replaying one deterministic request can increment real progression repeatedly. A former visible “War Drill” path demonstrated user reachability; PR #208 removed the obvious button, but the simulator itself remains globally side-effectful and has no authority boundary.

### 3.7 Computed value versus committed value ambiguity

`BattleReport` exposes:

```text
WarzoneCreditsEarned
Loot
XpGained
losses
winner
```

without saying whether value is proposed or committed. Callers can ignore, display, or apply fields independently and more than once.

### 3.8 Reporting and content authority

The technical simulator hard-codes:

- victory/defeat summaries;
- round prose;
- commander contribution prose;
- realm/terrain contribution strings;
- “earned” credit wording.

Technical diagnostics and numeric data belong in the computation result. Player-facing copy resolves later through approved content and #177 delivery.

## 4. Authority and ownership

### 4.1 Codex engineering

Owns:

- immutable technical models;
- validators;
- fixed-point/checked arithmetic;
- deterministic canonical encoding and hashes;
- pure computation;
- immutable results and application plans;
- persistence/result-ledger integration;
- typed events/notification requests;
- tests, tooling, and evidence.

### 4.2 Codex narrative/content

Owns:

- player-facing battle labels and summaries;
- round/outcome/contribution text;
- named encounter meaning;
- localization keys and parameter meaning;
- tone and authored actions.

### 4.3 Balance authority

No balance value changes are authorized here.

Observed current constants are migration evidence only. A source migration must preserve an approved existing value exactly or stop for a separate user-approved balance decision.

### 4.4 User

Retains final approval of:

- combat balance;
- reward cadence;
- battle feel;
- player-facing presentation;
- integrated milestone/release acceptance.

## 5. Terminology

### 5.1 Preview

A validated side-effect-free computation intended for planning, inspection, tests, or UI forecasting. A preview result is never accepted by the application service.

### 5.2 Authoritative battle

A battle request tied to one validated profile/session/encounter identity and eligible for one durable result application.

### 5.3 Rules snapshot

An immutable, versioned collection of troop power, counters, round rules, casualty rules, and reward formulas used by computation.

### 5.4 Context snapshot

Immutable resolved realm, terrain, research, Champion, encounter, and opponent data. Computation never resolves ambient services.

### 5.5 Computed result

An immutable technical proposal. It has no side effects and is not player-owned progression or reward.

### 5.6 Application plan

An immutable plan prepared against current save/troop/economy/quest/catalog/result-ledger revisions. It contains no mutable save-row or service references.

### 5.7 Committed result

A result whose losses, rewards, progression, ledger, and required durable notification outbox have persisted and verified through one candidate transaction.

### 5.8 Exact replay

Reuse of the same result ID with the same computation hash and semantic identity. It returns the existing committed receipt without applying anything again.

### 5.9 Correlation conflict

Reuse of a result ID with a different request, rules/context snapshot, computation hash, losses, or reward proposal. It blocks as an integrity error.

## 6. Stable identity and version contract

### 6.1 Required identities

Every production request contains or resolves:

```text
gameId
catalogSetId
battleRequestId
battleId
battleResultId
profileId or save identity token
executionMode
battleTypeId
battleRulesProfileId/schemaVersion/contentVersion/rawSha256
attackerArmyId/revision
opponentArmyOrBossProfileId/revision
attackerContextId/revision
defenderContextId/revision
terrainProfileId/version/hash
modifierSnapshotId/hash
rewardProfileId/version/hash
expectedResultConsumerId
determinismVersion
determinismSeed
```

### 6.2 ID rules

IDs are:

- non-null and nonblank;
- case-sensitive unless an approved alias table says otherwise;
- within shared UTF-8 byte limits;
- free of control characters;
- stable technical IDs under #183 conventions;
- resolved by the correct immutable catalog snapshot;
- never derived from display text, Unity object name, hash code, wall clock, or list position.

### 6.3 Result identity

`battleResultId` is issued by the authoritative encounter/orchestration owner. Preview IDs use a separate preview namespace and are permanently ineligible for application.

Recommended semantic mapping when the owner permits:

```text
battleResultId = battleId + ":result"
```

The exact format belongs to the owning session contract, but the relationship must be deterministic and one-to-one.

## 7. Execution mode and battle type

### 7.1 Execution mode

```text
Preview
Authoritative
```

No Boolean is used because future modes must fail unsupported rather than defaulting.

### 7.2 Stable battle type IDs

Current enum values become explicit legacy aliases for stable IDs:

```text
battle.pve
battle.pvp
battle.boss
battle.warzone
```

Undefined enum values reject.

### 7.3 Initial context matrix

| Type | Attacker | Opponent | Realm rule | Additional required context |
| --- | --- | --- | --- | --- |
| PvE | nonempty validated army | nonempty validated army or approved PvE opponent snapshot | attacker is an explicit valid realm; opponent may be explicit neutral context | PvE encounter/profile ID |
| PvP | nonempty validated army | nonempty validated army | both explicit valid non-neutral realms and distinct participant identities | PvP session/match ID |
| Warzone | nonempty validated army | nonempty validated army | attacker valid realm; defender valid realm or explicit neutral territory owner | territory/warzone encounter ID |
| Boss | nonempty validated army | nonempty army or resolved positive-power boss battle snapshot | attacker valid realm; boss realm/neutrality explicit in snapshot | boss definition and encounter ID |

Raw `RealmId.None` is not silently interpreted. A neutral context is a typed resolved context with a stable profile ID.

### 7.4 Empty opponent policy

An empty defender list is valid only when a versioned opponent/boss snapshot supplies positive validated combat power and an approved casualty/application policy. The current implementation has no such snapshot; therefore an empty defender rejects during migration.

### 7.5 Terrain

Every request resolves an immutable terrain profile, including explicit neutral terrain:

```text
terrain.neutral
```

Blank values and substring interpretation are prohibited.

## 8. Immutable request model

Conceptual shape:

```csharp
public sealed class BattleComputationRequest
{
    public string GameId { get; }
    public string CatalogSetId { get; }
    public string BattleRequestId { get; }
    public string BattleId { get; }
    public string BattleResultId { get; }
    public string ProfileId { get; }
    public BattleExecutionMode ExecutionMode { get; }
    public string BattleTypeId { get; }
    public ArmySnapshot AttackerArmy { get; }
    public OpponentSnapshot Opponent { get; }
    public BattleContextSnapshot Context { get; }
    public BattleRulesSnapshot Rules { get; }
    public BattleRewardProfileSnapshot RewardProfile { get; }
    public string ExpectedResultConsumerId { get; }
    public string DeterminismVersion { get; }
    public ImmutableByteString DeterminismSeed { get; }
}
```

It contains no:

- mutable list or array;
- live save object;
- service reference;
- `ScriptableObject`;
- player-facing copy;
- default/magic seed;
- direct caller-authorized reward;
- wall-clock/frame/process entropy.

## 9. Army snapshot and validation

### 9.1 Army shape

```csharp
public sealed class ArmySnapshot
{
    public string ArmyId { get; }
    public string Revision { get; }
    public IReadOnlyList<ArmyStackSnapshot> Stacks { get; }
    public string CatalogSetId { get; }
    public string SnapshotHash { get; }
}

public sealed class ArmyStackSnapshot
{
    public string TroopDefinitionId { get; }
    public string TroopDefinitionContentVersion { get; }
    public long ActiveCount { get; }
}
```

Wounded/reserve counts are not silently included. #165 defines which inventory pool is eligible for deployment.

### 9.2 Duplicate policy

Duplicate troop definition IDs reject. Computation does not aggregate, keep first/last, or mutate the caller’s representation. An explicit request builder may produce a separate normalization proposal, but authoritative computation consumes one canonical stack per troop ID.

### 9.3 Count validation

Reject:

- null stack;
- blank/unknown troop ID;
- unsupported definition version;
- zero/negative count;
- count above the technical or profile maximum;
- total-count overflow;
- total count above `100_000_000` technical safety ceiling;
- stack/catalog revision mismatch;
- unknown future required troop definition.

The technical ceiling prevents arithmetic abuse and is not a gameplay balance cap. A lower profile limit is a separate approved rule.

### 9.4 Definition validation

Every troop definition supplies immutable technical values:

```text
stable ID
schema/content version
base power
counter class/profile IDs
casualty/vulnerability profile ID
realm/terrain eligibility where technical
source revision/hash
```

Current `TroopType` enum values remain migration aliases only. Undefined enum values never receive base power `1`.

### 9.5 Input immutability

Validation and computation do not:

- replace null lists;
- reorder backing caller collections;
- change counts;
- merge duplicates;
- create troop definitions;
- read or mutate saved troop state.

## 10. Context and modifier snapshots

### 10.1 Battle context

Conceptual fields:

```text
attacker realm context ID/version
opponent realm/neutral/boss context ID/version
terrain profile ID/version/hash
research modifier snapshot ID/hash
Champion/commander modifier snapshot ID/hash
battle/territory/boss encounter context ID
optional modifier declarations
```

### 10.2 Research

Computation receives a validated numeric snapshot. It never calls `ServiceLocator` or `IResearchService`.

Missing required research data returns `ModifierSnapshotUnavailable` or `InvalidModifierSnapshot`. An explicitly optional modifier must be represented by a versioned neutral snapshot, not a caught exception.

### 10.3 Morale

Morale is fixed-point millionths:

```text
650_000 <= moraleMicros <= 1_300_000
```

The request must supply a valid value. Zero, negative, non-finite source values, or values outside the range reject; they do not become `1.0`.

### 10.4 Realm and terrain

Realm/terrain modifiers come from immutable rules/profile tables. Unknown values reject. No lowercase substring logic exists in computation.

### 10.5 Snapshot provenance

Every snapshot records:

```text
schemaVersion
contentVersion
sourceRevision
rawSha256
catalogSetId
```

The result records the exact snapshot hashes used.

## 11. Numeric representation

### 11.1 Fixed-point scale

All combat multipliers use:

```text
MultiplierScale = 1_000_000
```

Examples:

```text
1.00 → 1_000_000
1.18 → 1_180_000
0.97 →   970_000
```

Binary floating-point is not the authority at the computation boundary.

### 11.2 Conversion

Existing floats migrate only through a reviewed converter with exact source value, resulting integer, and drift vectors. Ambiguous values reject rather than silently round into balance.

### 11.3 Checked arithmetic

- counts and base powers use signed 64-bit checked arithmetic;
- multiplier products use a mathematically exact intermediate or equivalent limb implementation;
- division uses explicit round-to-nearest, ties-to-even;
- final power must be positive and `<= 1_000_000_000`;
- cumulative round damage and comparison products must remain within proven checked bounds;
- overflow returns a typed failure.

The one-billion final-power ceiling is a technical safety limit aligned with the current `int` report boundary, not a balance decision.

### 11.4 Combined power

Conceptually:

```text
numerator = basePower × attackOrDefense × counter × realm × terrain × morale × approvedOtherModifiers
denominator = MultiplierScale ^ modifierCount
finalPower = RoundToEven(numerator / denominator)
```

Rounding occurs once after the complete multiplier product, not independently after each factor.

## 12. Observed current tuning inventory

These values are recorded for migration tests and are not newly approved balance:

### 12.1 Base power

| Legacy troop | Current base power |
| --- | ---: |
| Infantry | 10 |
| Cavalry | 15 |
| Ranged | 12 |
| Siege | 20 |

### 12.2 Counter and type modifiers

```text
Infantry present vs Cavalry present   +0.18
Cavalry present vs Ranged present     +0.18
Ranged present vs Infantry present    +0.18
Siege in Boss/Warzone                 +0.12
```

### 12.3 Realm modifiers

```text
Stonehold: +0.10 with Siege, otherwise +0.06
Eldergrove: +0.10 with Ranged, otherwise +0.05
Crownlands: +0.06
Umbral: +0.09 when attacker or PvP, otherwise +0.04
```

### 12.4 Terrain intent

```text
mountain/cave: Stonehold 1.08, otherwise 1.00
forest: Eldergrove or Ranged 1.07, otherwise 0.98
road/field: Crownlands or Cavalry 1.05, otherwise 1.00
volcanic/shadow: Umbral 1.08, otherwise 0.97
neutral/unknown current fallback: 1.00
```

The migration replaces substrings with stable terrain profiles; it does not silently approve every arbitrary string that happened to contain a token.

### 12.5 Rounds and casualties

```text
maximum rounds: 20
damage rate per side/round: [0.08, 0.16)
minimum damage: 1 power unit
winner casualty ratio: pressure × 0.38
loser casualty ratio: min(1, pressure × 0.70 + 0.08)
killed share of affected: winner 0.35, loser 0.55
vulnerability: Cavalry 0.92, Ranged 1.08, Siege 1.18, Infantry 1.00
```

### 12.6 Reward proposal

```text
Warzone/PvP/Boss credits:
  base 12 win / 4 loss
  + clamp(defenderPower / 120, 0, 40)
  + clamp(rounds / 2, 0, 10)

victory resource proposal:
  Food: 40 + integer draw [0, 25]
  Gold: 12 + integer draw [0, 13]

XP proposal:
  win: max(8, defenderPower / 18)
  loss: max(3, defenderPower / 36)
```

These values move into versioned rules/reward profiles before production use.

## 13. Cross-runtime deterministic entropy

### 13.1 Prohibited entropy

Computation must not read:

```text
DateTime.Now/UtcNow
Environment.TickCount
Time.time/frame count
UnityEngine.Random
System.Random
string.GetHashCode()
object hash codes
process/thread IDs
locale-dependent formatting
mutable dictionary/list enumeration order
```

### 13.2 Seed

The request contains an immutable 32-byte seed or canonical 64-hex representation.

- Preview seed is explicit and visible in technical result data.
- Authoritative seed is issued/derived by the encounter authority.
- Missing seed rejects.
- No magic `12345` fallback exists. `12345` may remain an explicit legacy test vector only.

### 13.3 Canonical draw input

Each draw uses a length-prefixed UTF-8 canonical sequence:

```text
determinismVersion
catalogSetId
battleResultId
battleRequestId
rulesProfileId/contentVersion/hash
contextSnapshotHash
attackerArmySnapshotHash
opponentSnapshotHash
seed hex
drawNamespace
roundIndex
```

Length prefix is unsigned 32-bit big-endian byte count followed by exact bytes.

### 13.4 Hash and range mapping

Use SHA-256. Read the first four digest bytes as unsigned 32-bit big-endian `draw`.

For a half-open integer range `[minimum, minimum + span)`:

```text
offset = floor(draw × span / 2^32)
value = minimum + offset
```

The multiplication uses unsigned 64-bit arithmetic.

### 13.5 Draw namespaces

At minimum:

```text
round.<n>.attacker_damage_rate
round.<n>.defender_damage_rate
reward.food_amount
reward.gold_amount
```

Adding a new draw does not shift existing draws.

### 13.6 Damage rate

Current migration profile maps each round-side draw to:

```text
80_000 <= damageRateMicros < 160_000
```

### 13.7 Test vectors

A retained machine-readable vector artifact records:

```text
input fields
canonical bytes/hex
SHA-256 digest
UInt32 draw
range/span
expected mapped value
expected round/result fields
expected computation hash
```

Vectors must be verified by every supported shared-contract/runtime implementation.

## 14. Pure computation algorithm

### 14.1 Steps

```text
1. validate request identity/mode/type/versions
2. validate immutable armies and context/rules/reward snapshots
3. canonicalize stacks by ordinal stable troop ID
4. calculate checked base powers
5. calculate checked fixed-point multipliers and final powers
6. resolve up to the approved maximum rounds with namespaced SHA-256 draws
7. calculate technical outcome
8. calculate internally consistent casualties
9. calculate a technical reward proposal
10. build immutable technical contribution records
11. canonicalize result and calculate computation hash
12. return typed computed result
```

No step calls a service, save, event, notification, UI, or Unity object creation API.

### 14.2 Round state

Store power/damage in fixed-point power micros:

```text
PowerScale = 1_000_000
```

Initial remaining power:

```text
finalPower × PowerScale
```

Each side’s damage is computed from that side’s remaining power at the start of the round, matching the current simultaneous-round intent:

```text
damage = max(PowerScale, RoundToEven(remainingPowerMicros × damageRateMicros / MultiplierScale))
```

Remaining power is clamped at zero after simultaneous damage calculation. Arithmetic is checked.

### 14.3 Outcome

The current tie rule is preserved as a versioned policy:

```text
attacker victory when normalized defender damage >= normalized attacker damage
```

Compare ratios using a checked exact helper under the documented power/damage ceilings. No floating division decides the winner.

A future draw outcome requires a separate approved rules profile; it is not introduced here.

### 14.4 Round notes

Technical round records contain an enum/profile ID such as:

```text
attacker_pressure
defender_pressure
even_trade
```

and numeric fields. Player prose is not stored in the simulator.

### 14.5 Casualties

Casualty calculation must prove for every troop stack:

```text
0 <= killed
0 <= wounded
0 <= survived
killed + wounded + survived == starting active count
```

Total killed/wounded never exceed the validated army count.

Damage distribution, casualty pressure, vulnerability, affected count, and killed share use fixed-point checked arithmetic and explicit rounding.

### 14.6 Boss opponent

When a Boss snapshot supplies non-army power, the result uses an explicit boss casualty policy and does not fabricate troop loss rows. Boss item rewards are not calculated here; the result carries the stable boss reward operation context required by #168.

## 15. Reward proposal contract

### 15.1 Computed, not committed

The technical result calls these values:

```text
ProposedWarzoneCredits
ProposedResources
ProposedXp
```

They are never named `Earned`, `Granted`, or `Owned` before commit.

### 15.2 Reward profile

An immutable reward profile records:

```text
stable ID
schema/content version/hash
eligible battle types/outcomes
credit formula profile
resource formula profile
XP formula profile
explicit no-reward policy
boss reward handoff policy
```

The caller cannot supply arbitrary credit/resource/XP values.

### 15.3 Preview

Preview results may include illustrative proposals only when the profile permits. Every proposal is marked `PreviewOnly` and is rejected by application.

### 15.4 Boss items

Boss equipment/items are delegated to #168 through a stable post-battle operation/result identity. Battle computation does not duplicate item rolls or inventory logic.

## 16. Typed computation outcomes

Minimum statuses:

```text
Computed
ComputedPreview
ExplicitNoReward
InvalidRequest
UnsupportedExecutionMode
UnsupportedBattleType
CatalogUnavailable
UnsupportedVersion
InvalidArmy
InvalidOpponent
InvalidRealmContext
InvalidTerrainProfile
InvalidModifierSnapshot
InvalidRulesProfile
InvalidRewardProfile
ArithmeticOverflow
DeterminismFailure
InternalInvariantFailure
```

Only `Computed`, `ComputedPreview`, and `ExplicitNoReward` contain an immutable value. `ComputedPreview` is never application-eligible.

## 17. Immutable computed result

Conceptual shape:

```csharp
public sealed class BattleComputedResult
{
    public string BattleRequestId { get; }
    public string BattleId { get; }
    public string BattleResultId { get; }
    public BattleExecutionMode ExecutionMode { get; }
    public string BattleTypeId { get; }
    public BattleTechnicalOutcome Outcome { get; }
    public long AttackerPower { get; }
    public long OpponentPower { get; }
    public IReadOnlyList<BattleRoundSnapshot> Rounds { get; }
    public IReadOnlyList<TroopLossSnapshot> AttackerLosses { get; }
    public IReadOnlyList<TroopLossSnapshot> OpponentLosses { get; }
    public BattleRewardProposal RewardProposal { get; }
    public IReadOnlyList<BattleContributionSnapshot> Contributions { get; }
    public string RulesSnapshotHash { get; }
    public string ContextSnapshotHash { get; }
    public string DeterminismVersion { get; }
    public string ComputationHash { get; }
}
```

All collections are defensive immutable/read-only values. No request or save backing collection escapes.

## 18. Internal consistency validation

Before publication as a computed result, verify:

- IDs and snapshot hashes match request;
- round indices are contiguous and within maximum;
- all numeric fields are in range;
- no non-finite values exist;
- remaining power never increases;
- damage and outcome are consistent;
- each loss row matches a validated input troop ID;
- loss totals equal starting counts;
- no duplicate loss rows;
- reward proposal matches the profile/type/outcome;
- preview status/mode match;
- canonical result hash is stable.

Failure returns `InternalInvariantFailure`, not a partially populated report.

## 19. Computation hash

Canonical result serialization covers at minimum:

```text
request/battle/result IDs
execution mode and battle type
all source IDs/versions/hashes
seed and determinism version
powers
ordered rounds and damage
outcome
ordered losses
ordered reward proposal
ordered technical contributions
```

Use SHA-256. The hash is stored in the application ledger and detects result-ID conflicts.

## 20. Purity and lifetime

The pure computation phase guarantees:

- zero `ServiceLocator` calls;
- zero save calls;
- zero quest/economy/training/territory/boss mutation;
- zero events/notifications/logs represented as delivery;
- zero `ScriptableObject.CreateInstance` calls;
- zero changes to request/snapshot objects;
- deterministic output after service/process reconstruction;
- no Unity scene/object lifetime dependency;
- bounded allocation and no retained mutable backing lists.

Technical diagnostics may be returned as immutable data. Optional developer logging is outside computation and cannot change status.

## 21. Application request and plan

### 21.1 Application request

```csharp
public sealed class BattleResultApplicationRequest
{
    public BattleComputedResult ComputedResult { get; }
    public string ExpectedProfileRevision { get; }
    public string ExpectedTroopRevision { get; }
    public string ExpectedEconomyRevision { get; }
    public string ExpectedQuestRevision { get; }
    public string ExpectedTerritoryRevision { get; }
    public string ExpectedCatalogSetId { get; }
    public string ApplicationPolicyVersion { get; }
}
```

### 21.2 Eligibility

Application rejects unless:

- result mode is `Authoritative`;
- result status is application-eligible;
- result/computation hash is valid;
- battle/session/result identity is active and expected;
- rules/context/catalog versions are supported;
- current profile and domain revisions match;
- army availability still covers the authoritative deployment/loss basis;
- all required no-save mutation adapters are available;
- the result ledger has no conflicting record.

### 21.3 Pure plan

The planner returns immutable ordered operations:

```text
attacker troop killed/wounded/survivor operations via #165
opponent/territory troop operation where owned/persisted
checked credit/resource/XP operations via #163/owning progression service
WinBattle and other quest operation via corrected #152 typed no-save path
territory consequence via #166 when applicable
boss reward operation reference via #168 when applicable
applied-result ledger record
durable notification outbox records via #177/#137
post-commit technical event records
expected revisions and plan hash
```

The plan contains no mutable save rows, services, delegates, Unity objects, or player-facing strings.

### 21.4 Stale plan

Any revision mismatch returns `StalePlan`. Apply does not silently rebase/recompute. The orchestrator obtains fresh snapshots and explicitly plans again.

## 22. One candidate transaction

Required order:

```text
1. validate computed result and active battle identity
2. inspect applied-result ledger
3. prepare full immutable application plan
4. clone the validated current save candidate
5. apply troop losses/wounds through #165 no-save candidate primitive
6. apply credits/resources/XP through checked no-save primitives
7. apply quest progress through corrected typed no-save quest primitive
8. apply territory/boss/NVS consequence references through owning adapters
9. add applied battle-result ledger record
10. add required durable notification outbox records
11. validate complete candidate semantics and expected revisions
12. persist through accepted #137
13. reload/verify durability where policy requires
14. publish candidate and new revisions
15. emit post-commit technical events
16. enqueue visible notification/result presentation once
```

Inside steps 4–11, do not call independently saving compatibility wrappers.

## 23. Applied-result ledger

### 23.1 Key and record

Primary key:

```text
battleResultId
```

Record at minimum:

```text
battleResultId
battleId
battleRequestId
profile/session identity
battle type and execution mode
computation hash
rules/context/army/reward profile hashes
outcome
committed losses
committed credit/resource/XP values
territory/boss consequence IDs
committed UTC timestamp
application policy version
notification correlation IDs
```

### 23.2 Replay behavior

| Existing state | Outcome |
| --- | --- |
| no record | plan first application |
| same result ID and same complete semantic/hash data | `AlreadyCommitted`; return stored receipt; apply/notify zero times |
| same result ID with any changed data | `CorrelationConflict`; block |
| pending/uncertain persistence | return recovery-required status; never apply blindly |
| malformed ledger record | disable application and require #137 recovery |

### 23.3 Ledger ownership

Use the shared save transaction/operation ledger architecture. Do not create an independent battle file or service-local idempotency store.

## 24. Typed application outcomes

Minimum statuses:

```text
Committed
AlreadyCommitted
ExplicitNoRewardCommitted
PreviewRejected
InvalidComputedResult
InactiveOrMismatchedBattle
CorrelationConflict
CatalogDrift
TroopStateUnavailable
TroopStateInvalid
EconomyUnavailable
EconomyInvalid
QuestStateUnavailable
QuestStateInvalid
TerritoryConsequenceUnavailable
BossRewardHandoffUnavailable
StalePlan
ArithmeticOverflow
PersistenceFailed
CommitUncertain
UnsupportedVersion
InternalInvariantFailure
```

Only committed statuses expose an immutable `CommittedBattleResultReceipt`.

## 25. Failure semantics

### 25.1 Before persistence

No live state, event, or notification changes. Return known-not-committed failure.

### 25.2 Persistence failure

If durability is known not to have occurred, retain the previous published state and allow a reviewed retry with the same result identity.

### 25.3 Commit uncertainty

When durable state cannot be proven, return `CommitUncertain`, freeze duplicate application, and defer to #137 recovery. Do not show victory rewards as committed.

### 25.4 Notification/presenter failure after commit

Committed value remains committed. The typed delivery receipt records failure and the durable outbox remains pending where required. Notification failure never causes reward rollback or duplicate reapplication.

### 25.5 Subscriber failure

One subscriber cannot prevent other post-commit observers or alter the committed receipt. Subscriber exceptions are isolated and diagnosed.

## 26. Troop-loss application boundary

#165 must define:

- authoritative active versus wounded inventory semantics;
- valid deployment reservation/snapshot revision;
- killed subtraction;
- wounded transfer/recovery behavior;
- duplicate/unknown/malformed troop row policy;
- checked count arithmetic;
- reload and stale-result behavior.

Battle application must not infer these rules from current `Count`/`WoundedCount` fields.

A preview uses copied army input and never reserves or mutates inventory.

## 27. Quest, economy, territory, and boss boundaries

### 27.1 Quest

Remove direct `TryUpdateWinQuest()`. `WinBattle` progress is an ordered no-save operation in the committed application plan and occurs exactly once after a valid authoritative result.

### 27.2 Economy

Proposed credits/resources are applied only through accepted #163 no-save checked operations. Negative, duplicate, malformed, unsupported, or overflowing wallets block application.

### 27.3 XP

XP ownership and persistence must be identified before application. If no authoritative XP domain exists, the reward profile marks XP unavailable/noncommittable rather than fabricating a field.

### 27.4 Territory

Warzone/capture consequences are delegated to #166. A battle victory does not capture territory merely because a report says winner.

### 27.5 Boss

Boss item/equipment rewards are delegated to #168. One battle result may create/reference one stable boss reward operation; it does not duplicate item logic.

## 28. Preview and UI behavior

### 28.1 Preview receipt

A preview result is clearly typed and includes no committed language. It can be displayed as:

```text
simulation
forecast
estimated technical outcome
```

Final wording is content-owned.

### 28.2 Production UI

UI displays only:

- preview receipt as preview;
- committed receipt as committed;
- already-committed receipt without duplicate animation/value;
- pending recovery;
- unavailable/invalid/failure state.

It never displays computed proposal values as owned rewards.

### 28.3 Former War Drill

Any future drill/test button must construct `Preview` mode, save zero times, advance no quest, and clearly label proposed values as non-authoritative.

### 28.4 Realm fallback

`RealmId.None` never silently becomes Crownlands for an authoritative battle. Missing realm produces a visible unavailable result.

## 29. Compatibility migration

### 29.1 Legacy models

Current classes are mutable and ambiguous:

```text
BattleRequest
TroopStack
BattleReport
BattleRoundReport
TroopLossReport
```

Migration may retain them temporarily as obsolete adapters, but production computation/application uses immutable typed contracts.

### 29.2 Legacy `IBattleSimulator`

Current:

```csharp
BattleReport Simulate(BattleRequest request)
```

is replaced or supplemented by a typed result API. Any compatibility wrapper:

- is side-effect-free;
- returns validation failure for malformed input;
- requires explicit preview mode or a reviewed adapter context;
- does not default a seed/realm/list;
- does not return player-facing strings as authority;
- is marked obsolete and removed after callers migrate.

### 29.3 Legacy reward/report fields

Current report credit/resource/XP fields become proposal fields only. No caller may treat a legacy report as proof of commitment.

### 29.4 Existing saves

No historical applied-battle ledger is inferred from quest progress, troop counts, territories, or logs. New ledger authority starts at an explicit save/policy version marker. Missing historical provenance does not grant compensation or reapply old battles.

### 29.5 Unknown future data

Unknown stable troop/result/ledger records are preserved by save compatibility but excluded from unsupported computation/application.

## 30. Diagnostics

Every diagnostic includes:

```text
stable code
severity
domain
battle request/result ID where safe
record/troop/profile ID where safe
field/path
schema/content/determinism/application version
blocks computation/application/presentation boolean
safe developer message
```

Code families:

```text
AL-BATTLE-REQUEST-*
AL-BATTLE-ARMY-*
AL-BATTLE-CONTEXT-*
AL-BATTLE-RULES-*
AL-BATTLE-DETERMINISM-*
AL-BATTLE-RESULT-*
AL-BATTLE-APPLICATION-*
AL-BATTLE-LEDGER-*
AL-BATTLE-NOTIFICATION-*
```

Ordering is deterministic by severity, code, record ID, and field path.

No raw local path, stack trace, secret/session token, or unsanitized player string appears in player-facing presentation.

## 31. Concurrency and reentrancy

- applications serialize per profile/result-ledger revision;
- two identical concurrent applications converge to one commit and one duplicate receipt;
- two different result plans from the same revision cannot silently overwrite;
- stale second plan rejects;
- reentrant event/notification callbacks cannot apply the same result;
- cancellation after persistence starts does not report clean cancellation unless durability is known;
- computation is thread-safe over immutable inputs where supported;
- Unity main-thread presentation limits do not define transaction correctness.

## 32. Security and abuse resistance

Reject:

- caller-provided arbitrary rewards;
- mutable/live save army lists;
- undefined enum values;
- blank/oversized/control-character IDs;
- extreme collection/count/power inputs;
- result-ID reuse with changed payload;
- preview result application;
- stale army/economy/quest revisions;
- forged snapshot/catalog hashes;
- arbitrary terrain substrings;
- process/time entropy;
- player copy/action/URL/scene injection.

Development fixtures are explicit and excluded from production authority/evidence.

## 33. Required tests

### 33.1 Request identity and mode

- valid Preview request;
- valid Authoritative request;
- null request;
- blank/oversized/control-character each required ID;
- missing/magic seed;
- preview ID supplied as authoritative result ID;
- unsupported execution mode/type/version;
- profile/session/result mismatch;
- wrong expected consumer;
- catalog-set mismatch.

### 33.2 Battle type matrix

- representative PvE;
- representative PvP;
- representative Warzone realm defender;
- representative Warzone neutral territory snapshot;
- representative Boss with army;
- representative Boss with positive boss power snapshot;
- missing required boss/territory/PvP context;
- forbidden Boss ID on non-Boss type;
- raw `RealmId.None`/undefined context;
- same participant on both PvP sides.

### 33.3 Army validation

- null army;
- empty attacker;
- invalid empty opponent by type;
- null stack;
- blank/unknown troop ID;
- zero/negative count;
- per-stack and total-count ceiling;
- checked total-count overflow;
- duplicate troop ID;
- unsupported troop version;
- invalid definition/base power;
- snapshot/catalog hash mismatch;
- input order canonicalization;
- source objects/lists unchanged.

### 33.4 Modifier/context validation

- valid explicit neutral terrain;
- blank/unknown terrain;
- invalid realm/neutral/boss context;
- missing research snapshot;
- explicitly optional neutral modifier;
- non-finite source conversion fixture;
- morale below/above/at boundaries;
- invalid multiplier;
- modifier product overflow;
- context hash mismatch.

### 33.5 Fixed arithmetic

- each observed base power and modifier vector;
- multiplier order/final rounding vectors;
- ties-to-even cases;
- final power 1 and technical maximum;
- final power zero/negative/overflow rejection;
- exact ratio comparison boundaries;
- no binary-float authority in computation.

### 33.6 Determinism

- same request/snapshots/seed produces byte/field-equivalent result repeatedly;
- result remains identical after process/service reconstruction;
- cross-runtime canonical vectors;
- different seed changes approved draws only;
- different round namespace does not shift other draws;
- different rules/content version changes computation hash;
- army input order does not change result;
- exact SHA-256/draw/range vectors;
- prohibited entropy/token scan.

### 33.7 Rounds/outcome

- one-round termination;
- maximum-round termination;
- attacker depletion;
- defender depletion;
- simultaneous zero;
- normalized-damage tie follows versioned attacker tie rule;
- remaining power never increases;
- damage in valid ranges;
- round indices contiguous;
- technical note IDs consistent.

### 33.8 Casualties

- winner/loser pressure boundaries;
- each vulnerability profile;
- zero affected;
- full affected;
- killed/wounded rounding boundaries;
- killed+wounded+survived exact identity;
- totals never exceed army;
- no duplicate/unknown loss row;
- damage distribution totals and finite ranges;
- boss non-army casualty policy.

### 33.9 Reward proposal

- eligible/ineligible battle types;
- win/loss credit vectors;
- credit clamps/boundaries;
- food/gold deterministic ranges;
- XP win/loss boundaries;
- explicit no reward;
- preview-only marking;
- invalid reward profile;
- no boss item calculation;
- no direct economy/resource/XP mutation.

### 33.10 Purity

- no ServiceLocator access;
- no save call;
- no quest/economy/training/territory/boss mutation;
- no event/notification;
- no Unity object creation;
- input snapshots unchanged;
- repeated computation idempotent;
- computation diagnostics deterministic.

### 33.11 Planning

- valid first application plan;
- preview rejected;
- exact losses/reward/quest/territory/boss operations;
- missing no-save adapter;
- invalid current troop/economy/quest state;
- catalog drift;
- stale revision;
- result hash mismatch;
- existing exact ledger;
- correlation conflict;
- malformed ledger;
- plan contains no mutable references;
- deterministic plan hash.

### 33.12 Persistence/application

After dependencies:

- valid win applied once;
- valid loss applied once;
- explicit no-reward result committed once;
- duplicate same session;
- duplicate after reload;
- concurrent exact duplicates;
- two different results from one revision;
- mismatch/late result;
- failure at each troop/economy/quest/territory/boss/ledger/outbox/candidate/persist/verify/publish step;
- known-not-committed retry;
- commit-uncertain recovery;
- no partial published state;
- exact save/event/notification counts;
- no callback reentrancy duplicate.

### 33.13 Integration/regression

- current representative PvE/PvP/Warzone/Boss tuning vectors without unapproved drift;
- former War Drill/preview presses produce zero save/progress/reward;
- authoritative WinBattle progress exactly once;
- RealmId.None never becomes Crownlands;
- #165 troop inventory application;
- #163 economy application;
- #152 quest operation;
- #166 territory consequence;
- #168 boss reward handoff;
- #177 visible committed receipt;
- corrected #127 safe PlayMode;
- applicable Player profile only after scene/release gates.

## 34. Retained vector artifacts

The pure phase includes machine-readable artifacts for:

```text
fixed-point multiplier/power vectors
round entropy/damage vectors
outcome ratio vectors
casualty vectors
reward proposal vectors
canonical result/computation hashes
```

Each vector records schema version, all source IDs/versions/hashes, canonical input bytes, intermediate checked values, and expected final result.

## 35. Implementation phases

### Phase B1 — pure contract, validator, and simulator

Branch:

```text
codex/battle-contract-simulator
```

Allowed:

- immutable contract records;
- stable status/diagnostic enums;
- request/army/context/rules/reward validators;
- fixed-point checked math;
- canonical writer and SHA-256 entropy;
- pure round/outcome/casualty/reward proposal computation;
- immutable result hash;
- fake snapshot builders;
- retained vectors;
- focused EditMode tests;
- technical documentation.

Prohibited:

- production service/caller mutation;
- save fields;
- `ServiceLocator`;
- quest/economy/training/territory/boss services;
- scenes/UI/Build Settings;
- authored copy/content;
- balance changes;
- Android.

Use:

```text
Refs #174
```

Do not close #174.

### Phase B2 — technical source profiles

After #183/#165 authority:

- troop definitions and aliases;
- battle rules profiles;
- stable terrain profiles;
- realm/context modifier profiles;
- reward profiles;
- schemas/generated contracts;
- source hashes/provenance;
- migration vectors proving no approved tuning drift.

### Phase B3 — result application

After corrected #152/#163/#137 and #165:

- result/application service interfaces;
- candidate no-save adapters;
- applied-result ledger/outbox fields under declared save lock;
- persistence/fault/reload/concurrency matrix;
- committed receipt query.

### Phase B4 — consumers

- migrate any preview/drill caller to explicit Preview;
- migrate authoritative territory/PvP/PvE/Boss callers;
- remove direct quest mutation and global research lookup;
- remove ambiguous report reward language;
- consume committed receipts in UI;
- coordinate #166/#168/#177/#178/#180.

### Phase B5 — NVS/integrated acceptance

Only after approved #133 G1 and owning runtime sequence. No archived packet behavior becomes authoritative through this issue.

## 36. Expected file boundaries

### B1 likely

```text
unity/Assets/AL/Scripts/Battle/Contracts/**
unity/Assets/AL/Scripts/Battle/Validation/**
unity/Assets/AL/Scripts/Battle/Computation/**
unity/Assets/AL/Tests/EditMode/Battle/**
unity/SharedContracts/** only when generated/shared scope is declared
```

Use existing assemblies/namespaces when narrower and avoid broad architecture churn.

### Later migration files

```text
unity/Assets/AL/Scripts/Core/Interfaces/IBattleSimulator.cs
unity/Assets/AL/Scripts/Data/Runtime/BattleModels.cs
unity/Assets/AL/Scripts/Battle/Simulator/DeterministicBattleSimulator.cs
focused result-application service
owning callers/tests
SaveGameData.cs only after declared #137 lock
```

### Prohibited in B1

```text
Bootloader.cs
ServiceLocator.cs
SaveGameData.cs
LocalSaveGameService.cs
LocalResourceService.cs
LocalWarzoneCreditService.cs
LocalQuestService.cs
LocalTrainingService.cs
territory/boss services
Kingdom/Champion controllers
*.unity
EditorBuildSettings.asset
Android source
narrative/terrestrial source
```

## 37. Shared-file and lock policy

B1 requires no designated shared-file lock.

A later ledger/persistence PR must declare the `SaveGameData.cs` lock and coordinate with #137, boss/world/relationship ledgers, notification outbox, and NVS fields.

A later caller/service migration must inspect current open PRs and declare overlaps before editing.

No conflict resolution may discard accepted current contracts, tests, services, fields, assets, or registrations.

## 38. Validation evidence

### B1 canonical evidence

Run from:

```text
C:\Users\MY\Documents\AnotherLife\unity
Unity 2022.3.62f3
```

Record:

- exact base/head SHA;
- complete changed-file/classification list;
- compile/import command, exit, final markers, and error scan;
- focused Battle EditMode totals/XML/log;
- complete EditMode totals/XML/log;
- deterministic vector command/output;
- cross-runtime/shared-contract vector proof where implemented;
- prohibited entropy/service/side-effect token scan;
- immutability/reflection tests;
- `git diff --check`;
- final status and every deferred check.

### Later application evidence

Add:

- old/malformed/duplicate/unknown troop/quest/economy fixtures;
- result-ledger migration;
- fault injection at every candidate boundary;
- exact save/event/notification counts;
- duplicate before/after reload and concurrency;
- corrected #127 PlayMode;
- applicable current Player build and launch only after #150 profile includes the consumer.

Duplicate-workspace, stale-base, skipped, compile-only, missing XML, wrong-policy green, or `continue-on-error` results are not passing evidence.

## 39. Review questions

Codex coordination/review verifies:

- computation is fully pure;
- invalid input cannot become a valid battle;
- preview cannot become authoritative;
- no magic defaults or global services remain;
- stable IDs/versions/hashes are exact;
- fixed-point arithmetic/rounding/bounds are deterministic;
- entropy canonicalization and vectors are exact;
- current tuning is preserved without rebalance;
- casualties are internally consistent;
- proposal versus committed value is unambiguous;
- plan/ledger replay/conflict/failure behavior is correct;
- no mutable backing state escapes;
- canonical evidence is complete.

Narrative/content review applies only when player-facing battle copy or meaning changes. User approval applies to balance or final integrated presentation, not the pure B1 technical implementation.

## 40. Acceptance criteria

### Specification acceptance

- [x] Current invalid-request, army, modifier, randomness, side-effect, reward, and reporting defects are inventoried.
- [x] Technical/content/balance/user authority is separated.
- [x] Preview/authoritative modes and battle-type context matrix are exact.
- [x] Immutable request/army/context/rules/reward/result contracts are exact.
- [x] Fixed-point checked arithmetic, bounds, rounding, and tuning migration are exact.
- [x] Cross-runtime SHA-256 entropy and vector requirements are exact.
- [x] Pure round/outcome/casualty/reward proposal behavior is exact.
- [x] Proposal, plan, committed result, ledger, replay/conflict, persistence, and notification boundaries are exact.
- [x] Troop/economy/quest/territory/boss ownership boundaries are preserved.
- [x] Phase/file/lock/test/evidence boundaries are implementation-ready.
- [x] No balance, narrative, save implementation, gameplay consumer, scene, Android, or unrelated change is authorized.

### Issue completion acceptance

#174 remains open until:

- [ ] B1 pure contracts/validation/computation are implemented and accepted.
- [ ] Versioned authoritative troop/rules/terrain/reward source exists.
- [ ] Computation has zero side effects and deterministic vectors pass.
- [ ] Preview and authoritative callers are explicitly separated.
- [ ] Accepted no-save troop/economy/quest/consequence adapters exist.
- [ ] Result ledger and candidate application are implemented under #137.
- [ ] Exact replay/reload/concurrency/fault tests pass.
- [ ] Direct quest/global service mutation and ambiguous report copy are removed.
- [ ] Canonical compile, EditMode, safe PlayMode, and applicable Player evidence pass.
- [ ] No unapproved tuning, content, VFX, Android, or unrelated change is included.

## 41. Immediate handoff

Codex engineering may now start only:

```text
branch: codex/battle-contract-simulator
scope: B1 immutable contracts, validators, fixed-point checked math, SHA-256 deterministic pure computation, vectors, and tests
completion link: Refs #174
shared locks: none
```

It must not mutate services/saves/callers, add ledger fields, close #174, or change balance/player-facing behavior.
