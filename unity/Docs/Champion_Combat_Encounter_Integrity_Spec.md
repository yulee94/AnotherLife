# Champion Combat and Encounter Integrity Specification

**Status:** Binding GPT technical specification for issue #180  
**Status date:** 2026-07-16  
**Audited base:** `26b41cf008740742c1fca0dd435ff2df39fb333b`  
**Primary implementation owner:** Codex engineering  
**Specification/review owner:** GPT  
**Skill, boss, encounter, and player-facing content owner:** Codex narrative/content  
**Final balance, creative, playtest, and release approval:** User  
**Canonical Unity workspace:** `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity`

## 1. Purpose

This specification defines the authoritative technical boundary for the playable Champion action-combat stack:

- validated Champion and boss combat profiles;
- finite/range-safe health, mana, damage, healing, movement, targeting, break, enrage, and timing state;
- immutable versioned skill definitions, loadouts, behavior profiles, and presentation references;
- explicit action/cast/resource/cooldown lifecycle;
- explicit actor life and control lifecycle;
- explicit encounter/session/result lifecycle;
- committed realm and source context with no Crownlands fallback;
- deterministic identity and duplicate-safe result handoffs;
- separation of local combat simulation, authoritative encounter completion, rewards, progression, notifications, and UI;
- safe scene, pause, retry, return, and disposal behavior;
- complete validation and evidence requirements.

It replaces the current implicit model:

```text
runtime primitives and mutable MonoBehaviour fields
→ raw float mutations
→ slot-index behavior
→ partial StreamingAssets overlay on hard-coded arrays
→ ServiceLocator realm fallback
→ coroutines and booleans as action/encounter authority
→ boss death performs loot/economy side effects
→ object destruction/null means encounter complete
→ mutable loot callback drives clear UI
```

with:

```text
validated immutable encounter request
→ validated immutable Champion/boss/skill/loadout/context snapshots
→ explicit actor and encounter state machines
→ typed finite action requests and mutation results
→ one authoritative encounter completion result
→ one durable result/reward/progression handoff
→ typed committed receipt
→ visible presentation once
```

This specification does not rebalance existing values, author new skills/bosses/lore, implement NVS-01, promote ChampionArena into the first Player profile, create network multiplayer authority, or redesign the visual presentation.

## 2. Binding dependencies and sequence

### 2.1 Related contracts

Champion combat consumes rather than duplicates:

```text
unity/Docs/Game_Data_Catalog_Authority_Spec.md
unity/Docs/Battle_Computation_Result_Transaction_Spec.md
unity/Docs/Boss_Loot_Result_Transaction_Spec.md
unity/Docs/Notification_Delivery_Contract_Spec.md
unity/Docs/Save_Semantic_Compatibility_Policy.md
unity/Docs/Economy_Integrity_Spec.md
unity/Docs/Production_Scene_Player_Build_Spec.md
```

### 2.2 Dependency sequence

```text
pure Champion combat/skill/boss/encounter contracts and planners
          ↓
#156 trusted Unity asset baseline
          +
#183 versioned Champion/skill/boss/encounter technical source
          +
#173 committed realm identity
          +
corrected #153 cross-scene lifecycle
          +
corrected #178 release containment
          ↓
production actor/caster/boss migration
          +
#137 durable result/operation ledger and persistence
          +
#168 deterministic boss reward operation
          +
#177 typed visible delivery
          ↓
#223/#150 Champion-capable scene/Player profile when separately approved
          +
corrected #127 safe PlayMode evidence
          ↓
NVS/quest/production integration under #133/#134 and owning issues
```

### 2.3 Phase authorization

The pure contract/planner phase defined in section 42 may proceed now. It mutates no production component, save, service, scene, UI, catalog, content, or balance.

Production integration remains blocked by the listed prerequisites. The existence of a playable Editor prototype does not waive them.

## 3. Verified current-source baseline

### 3.1 Champion health and mana accept invalid numeric state

Current `ChampionCombat`:

- assigns serialized `_maxHealth`, `_currentHealth`, `_maxMana`, `_currentMana`, `_manaRegenPerSecond`, and `_attackPower` without finite/range validation;
- treats `float.NaN` damage/heal/spend values as valid because ordinary comparisons with `NaN` are false;
- can store `NaN` health or mana permanently;
- treats negative mana cost as a successful/free spend;
- permits invalid maximum values and out-of-range current values;
- exposes `void` damage/heal and `bool` spend outcomes that do not distinguish failure causes;
- invokes events inline without subscriber isolation;
- has no explicit life state, mutation identity, source, or encounter state requirement;
- regenerates mana whenever current is below max, regardless of encounter phase or actor defeat.

### 3.2 Skill authority is split and silently hybridized

`SkillCaster` owns hard-coded arrays for:

```text
skill names
skill IDs
VFX keys
cooldowns
mana costs
cast times
ranges
power
bot damage multipliers
```

The StreamingAssets loader then overlays any non-null valid-slot rows onto those arrays.

Current behavior:

- catalog version is parsed but not required or compared;
- game/catalog/loadout identity is absent;
- a nonempty loadout array is treated as success;
- invalid or missing rows are skipped;
- missing fields retain hard-coded values;
- duplicate slots overwrite in iteration order;
- duplicate IDs are not rejected;
- missing required slots are not rejected;
- invalid numeric values silently use hard-coded fallback;
- negative values are clamped to zero;
- unknown role/behavior/VFX IDs are not rejected;
- returned arrays remain mutable;
- file/parse/network errors warn and silently continue with hard-coded runtime authority;
- synchronous and asynchronous paths can produce different timing/state;
- no source version/hash/provenance is retained.

The result can be an apparently valid four-slot loadout whose fields came from multiple unreported authorities.

### 3.3 Skill behavior is bound to slot index

Current `ResolveSkill(int slotIndex)` implements behavior through a switch:

```text
slot 0 → direct/area damage and realm slash
slot 1 → self heal and guard VFX
slot 2 → area damage and shockwave
slot 3 → heavy damage/breaker VFX
```

Changing a catalog ID or role does not change behavior. Moving a skill to another slot silently changes behavior. The catalog therefore does not own skill meaning or execution.

### 3.4 Skill cast/resource/cooldown lifecycle is ambiguous

Current cast flow:

```text
validate slot/casting/cooldown
→ spend mana immediately
→ start coroutine
→ wait cast time
→ resolve effect
→ start cooldown
```

`CancelCurrentSkill()` stops the coroutine and clears fields but:

- does not refund mana;
- does not start cooldown;
- has no cancellation reason;
- has no action ID;
- has no commit point;
- has no death/disable/scene-unload policy;
- has no interruption result;
- has no duplicate-input policy;
- can leave effect/cost/cooldown meaning dependent on coroutine timing.

A cast can continue while the actor or encounter becomes invalid because `SkillCaster` does not require an alive/active encounter state after acceptance.

### 3.5 Targeting is scene/name/component driven

Current skill targeting uses `Physics.OverlapSphere` and:

- destroys any collider whose GameObject name starts with `Dummy_`;
- finds bosses and bots by component lookup;
- uses Unity instance IDs to deduplicate colliders;
- considers bot hostility by realm enum only;
- uses no encounter participant registry, target ID, team ID, layer/profile, line-of-sight policy, target snapshot, or action hit ledger;
- can hit objects outside the owning encounter when components/names overlap;
- cannot prove exactly-once effect delivery across multiple colliders, pooling, disable/enable, or object replacement.

### 3.6 Realm fallback invents Crownlands context

`SkillCaster`, `ChampionController`, `BossDummyAI`, `ChampionArenaSceneController`, and bot configuration catch realm lookup errors or convert `RealmId.None` to `RealmId.Crownlands`.

Consequences:

- an invalid/unselected profile can execute Crownlands VFX/team/context;
- missing service-stack failures are hidden;
- UI, bot hostility, skill presentation, encounter result, and later rewards can use the wrong realm;
- tests can appear healthy without a committed realm.

### 3.7 Controller and movement state are weakly validated

Current `ChampionController`:

- accepts serialized movement, dodge, attack range, attack damage, attack cooldown, and dodge duration without finite/range validation;
- accepts arbitrary movement vectors and target positions;
- uses a hard-coded controller attack damage rather than the Champion combat profile attack power;
- performs Physics overlap and name/component targeting directly;
- has booleans/coroutines rather than typed action/control state;
- can show victory through destroyed dummy counts independently of encounter result;
- does not require alive/active encounter state for every operation;
- has no input command/action identity or duplicate suppression.

### 3.8 Auto combat is ambient and nondeterministic

Current `AutoCombatController`:

- searches the scene for `BossDummyAI`;
- uses `Random.value` for skill choice;
- uses raw interval/range/distance values;
- drives the same direct controller/caster methods without an encounter command boundary;
- has no decision/action ID, seeded entropy, participant identity, or exact control-owner transition;
- can continue acting until component/null checks happen to stop it.

`BotChampionAI` similarly:

- substitutes Crownlands for missing player realm;
- accepts invalid stats/timing/ranges;
- uses global random values and instance IDs;
- finds participants by scene scan/tag/components;
- accepts `NaN` damage;
- has no encounter participant/result identity.

### 3.9 Boss profile and runtime state are partial and unsafe

Current `BossDummyAI` owns serialized:

```text
max health
attack range/cooldown/telegraph/slam damage
enrage threshold/time
break max/recovery/duration/damage multiplier
boss ID/name
credit reward
mutable loot definitions
```

`ApplyBossDefinition()` copies only:

- nonblank ID;
- nonblank display name;
- positive health;
- nonempty loot list.

It ignores definition attack, armor, special abilities, source version/hash, and complete behavior validation.

Current boss state:

- accepts non-finite/negative damage;
- negative damage can heal above maximum;
- `NaN` poisons health/break/phase comparisons;
- divides current by unvalidated maximum;
- accepts invalid attack range/cooldown/telegraph/damage/enrage/break values;
- mutates range/cooldown incrementally across phases/enrage;
- uses overlapping booleans and coroutines for attack/broken/dead/enraged/phase state;
- can start overlapping break routines when repeated damage crosses zero;
- uses `Time.time` as encounter timing authority;
- emits direct VFX/audio/log side effects within state mutation;
- destroys itself immediately after triggering reward behavior.

### 3.10 Boss death fabricates reward and result authority

Current `Die()`:

```text
sets _isDead
→ marks visuals
→ calls IBossLootService.RollLoot
→ on any exception grants credits directly
→ emits a synthetic Ember Crown Shard result
→ destroys the boss object
```

This can:

- double-credit if the first reward operation partially succeeded;
- show an item never added to inventory;
- use boss component fields as balance authority;
- use `GetHashCode()` and `Time.time` as reward seed;
- represent object death as durable reward completion;
- lose authoritative operation state when the object is destroyed.

The complete reward correction belongs to #168; Champion encounter work must remove the boss component as reward owner.

### 3.11 Arena controller invents encounter authority

Current `ChampionArenaSceneController`:

- initializes services itself;
- creates the player and boss from Unity primitives at runtime;
- starts intro/encounter state from local booleans and `Time.time`;
- stores one mutable `BossLootResult` callback;
- grants one Warzone Credit every five seconds near the boss;
- considers boss null/dead to mean defeated;
- calculates grade from elapsed local time and observed booleans;
- displays “loot roll complete” and proposed reward data without a committed receipt;
- sets clear/defeat UI and control lock directly;
- has no encounter request, session ID, result ID, operation ledger, mode, quest context, resume token, persistence boundary, or committed result query;
- cannot distinguish practice/demo play from a reward/quest-authoritative encounter.

### 3.12 UI and event lifecycle can duplicate or disagree

Current behavior can produce:

- `OnDeath` once only if numeric state reaches a comparable zero;
- clear panel once per controller Boolean, not once per encounter result ID;
- reward text before/without durable inventory agreement;
- reward text that remains “syncing” or shows fallback data after boss destruction;
- repeated audio/VFX if controller/component reconstruction resets booleans;
- subscriber exceptions interrupting later observers;
- scene unload destroying the only operation holder;
- no durable “completed but presentation pending” state.

## 4. Authority and ownership

### 4.1 Codex engineering

Owns:

- immutable technical contracts and stable IDs;
- finite/range validators;
- actor/action/encounter state machines;
- load/validation result types;
- behavior execution adapters;
- target/participant registry;
- resource reservation/commit/refund mechanics;
- event isolation and diagnostics;
- result/reward/progression handoff infrastructure;
- tests, tools, and evidence.

### 4.2 Codex narrative/content

Owns:

- player-facing skill, boss, encounter, phase, and result names;
- descriptions, lore, labels, and localization keys;
- skill/boss narrative meaning;
- authored action/phase/encounter copy;
- content references used by presentation;
- whether an encounter/reward has story meaning.

### 4.3 Balance authority

No tuning changes are authorized by this specification.

Observed current values are recorded as migration evidence. A production source migration must preserve an approved existing value exactly or stop for a separate user-approved balance decision.

### 4.4 User

Retains final approval of:

- health/mana/damage/cooldown/range/cast-time/break/enrage tuning changes;
- cancellation/refund/cooldown policy changes that affect gameplay feel;
- boss phase/skill behavior changes;
- visible UI/feedback and integrated combat feel;
- production/milestone/release acceptance.

## 5. Terminology

### 5.1 Technical source snapshot

An immutable, versioned, hash-identified technical definition resolved from #183 authority.

### 5.2 Actor

A registered Champion, boss, bot, or encounter target with a stable participant ID and typed team/role.

### 5.3 Combatant state

The authoritative finite health, mana, life, control, and active-action state for one actor within one encounter.

### 5.4 Action

One stable input/AI/encounter command with an action ID, source, actor, skill/attack/dodge behavior, accepted state, resource/cooldown policy, target intent, and terminal result.

### 5.5 Encounter

One stable session binding participants, technical sources, realm/context, clock/entropy, mode, lifecycle, result ID, and consequence/reward policy.

### 5.6 Practice encounter

A session-only encounter that can compute local combat feedback but is permanently ineligible for durable rewards, quest/progression, territory, or save mutation.

### 5.7 Authoritative encounter

An approved encounter tied to a validated profile and one durable result/consequence operation.

### 5.8 Computed encounter outcome

A validated immutable terminal technical result from the runtime state machine. It is not durable progression/reward until committed.

### 5.9 Committed encounter receipt

A durable or approved session receipt proving the encounter terminal state and every owning consequence/reward operation status.

### 5.10 Presentation receipt

A typed result saying whether a committed/session result was queued, presented, acknowledged, failed, or remains pending. A Console log is not a presentation receipt.

## 6. Stable identities

### 6.1 Required IDs

Production contracts require:

```text
gameId
catalogSetId
profileId or save identity token
committedRealmId/realmDefinitionVersion
encounterDefinitionId/schemaVersion/contentVersion/rawSha256
encounterSessionId
encounterAttemptId
encounterResultId
participantId per actor
ChampionDefinitionId/contentVersion
ChampionCombatProfileId/contentVersion/hash
bossDefinitionId/contentVersion
bossCombatProfileId/contentVersion/hash
skillDefinitionId/contentVersion per skill
skillBehaviorProfileId/contentVersion per skill
skillPresentationProfileId/contentVersion per skill/loadout
skillLoadoutId/contentVersion/hash
combatRulesProfileId/contentVersion/hash
rewardOperationId when applicable
quest/territory/session context IDs when applicable
actionId per accepted action
```

### 6.2 ID rules

IDs are:

- non-null and nonblank;
- case-sensitive unless an approved alias table says otherwise;
- within shared UTF-8 byte limits;
- free of control characters;
- stable technical IDs, not display strings;
- resolved by the exact catalog snapshot;
- never generated from Unity object names, instance IDs, tags, wall clock, frame count, `GetHashCode()`, or localized text.

### 6.3 Attempt and result identity

A retry creates a new `encounterAttemptId` and normally a new `encounterResultId`, while retaining the parent encounter/session context. An exact replay/resume uses the same IDs and must converge idempotently.

The boss, scene, UI, reward service, and quest service do not independently generate competing result IDs.

## 7. Numeric source and runtime representation

### 7.1 Source values

Catalog/source numeric values are stored as:

- integral values when naturally integral;
- fixed decimal strings or fixed-point integers where cross-runtime identity/hash matters;
- explicit unit names in field IDs/contracts;
- finite values only.

Binary float text is not accepted without a deterministic parser and source hash.

### 7.2 Runtime values

Real-time local movement/animation/physics may use `float`, but every authoritative scalar is constructed only through a validated finite wrapper or equivalent guard.

Conceptual type:

```csharp
public readonly struct FiniteCombatScalar
{
    public float Value { get; }
    public string UnitProfileId { get; }
}
```

The constructor/factory rejects `NaN`, positive/negative infinity, values outside the declared technical range, and invalid units.

### 7.3 Technical safety ceilings

These are abuse/arithmetic ceilings, not balance targets:

```text
health/mana/damage/healing/attack power: 0 .. 1_000_000_000
world distance/range:                  0 .. 100_000 meters
time duration/cooldown:                0 .. 86_400 seconds
movement speed:                        0 .. 10_000 meters/second
regen rate:                            0 .. 1_000_000 units/second
multiplier:                            0 .. 1_000
```

Profiles may define narrower approved ranges. Required positive fields reject zero.

### 7.4 Vector validation

Movement, target, hit, and world positions require finite `x/y/z`. A non-finite vector is rejected before normalization, magnitude, rotation, physics query, or transform mutation.

### 7.5 Checked conversion

UI integer conversion, percentages, and timer displays occur only after finite/range validation. `Mathf.CeilToInt(NaN)` or division by invalid maxima is never a recovery strategy.

## 8. Observed current tuning inventory

The following values are migration evidence, not newly approved balance.

### 8.1 Champion combat

```text
max health: 1000
max mana: 100
mana regeneration: 10/second
ChampionCombat attack power: 50
```

### 8.2 Champion controller

```text
move speed: 5
rotation speed: 12
dodge speed: 10
dodge duration: 0.3
attack range: 2.5
attack damage: 125
attack cooldown: 0.6
```

The current mismatch between `ChampionCombat.AttackPower = 50` and controller attack damage `125` must be resolved by one approved technical source without silently changing either value.

### 8.3 Four current skills

| Slot | ID | Cooldown | Mana | Cast | Range | Power | Bot multiplier |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | `realm_strike` | 4 | 20 | 0.05 | 2.6 | 150 | 0.72 |
| 1 | `renewing_guard` | 8 | 30 | 0.35 | 0 | 180 | 0 |
| 2 | `warzone_burst` | 10 | 45 | 0.45 | 4.2 | 115 | 0.72 |
| 3 | `warmaster_breaker` | 14 | 60 | 0.65 | 3.4 | 260 | 0.72 |

Current behavior mapping is slot-based and must not be treated as approved source authority merely because these IDs exist in JSON.

### 8.4 Boss dummy

```text
max health: 1200
attack range: 5
attack cooldown: 3
telegraph: 1.35
slam damage: 80
enrage threshold: 0.3
timed enrage: 90
break max: 100
break recovery: 4/second
broken duration: 3
broken damage multiplier: 1.25
credit field: 500
```

Current phase modifications:

```text
70%: attack range +1
40%: attack cooldown ×0.75
15%: presentation cue
enrage: attack cooldown ×0.5 and range +0.5
break damage: clamp(source damage ×0.18, 8, 30)
```

These values move into a versioned boss combat profile before production use.

## 9. Immutable Champion combat profile

Conceptual shape:

```csharp
public sealed class ChampionCombatProfile
{
    public string Id { get; }
    public string SchemaVersion { get; }
    public string ContentVersion { get; }
    public string CatalogSetId { get; }
    public long MaxHealthMicros { get; }
    public long MaxManaMicros { get; }
    public long ManaRegenPerSecondMicros { get; }
    public long BasicAttackPowerMicros { get; }
    public string BasicAttackBehaviorProfileId { get; }
    public string MovementProfileId { get; }
    public string DodgeProfileId { get; }
    public string TargetingProfileId { get; }
    public string SourceRevision { get; }
    public string RawSha256 { get; }
}
```

Values may be represented through another reviewed fixed unit, but the source and hash must be deterministic.

Validation rejects:

- blank/duplicate IDs;
- unsupported versions;
- nonpositive required maxima;
- negative regen/attack values;
- values over technical/profile limits;
- missing behavior/movement/dodge/targeting references;
- wrong catalog set;
- hash/provenance mismatch.

## 10. Combatant state model

### 10.1 Life state

```text
Uninitialized
Alive
Defeated
Disposed
```

Allowed transitions:

```text
Uninitialized → Alive
Alive → Defeated
Alive → Disposed
Defeated → Disposed
```

No transition returns a defeated actor to alive. Retry creates a new participant/runtime state with a new attempt context.

### 10.2 Control state

Orthogonal typed state:

```text
Disabled
Manual
Assist
Auto
EncounterLocked
ActionLocked
Defeated
Disposed
```

One state owner resolves precedence. Multiple booleans/coroutines do not compete.

### 10.3 Resource snapshot

```csharp
public readonly struct CombatantResourceSnapshot
{
    public long CurrentHealthMicros { get; }
    public long MaxHealthMicros { get; }
    public long CurrentManaMicros { get; }
    public long ReservedManaMicros { get; }
    public long MaxManaMicros { get; }
    public CombatantLifeState LifeState { get; }
    public string Revision { get; }
}
```

Invariants:

```text
0 <= current health <= max health
0 <= current mana <= max mana
0 <= reserved mana <= current mana or separate available accounting per reviewed model
available mana = current mana - reserved mana >= 0
Defeated iff current health == 0
revision changes after every accepted mutation
```

### 10.4 Construction

Actor construction validates the profile and initial values before publication. Invalid source does not produce a partially alive actor. The result distinguishes unavailable profile, invalid profile, unsupported version, invalid initial state, and successful construction.

## 11. Typed health mutation

### 11.1 Request

```text
mutationId
actionId or sourceOperationId
sourceParticipantId
sourceBehaviorId
requested amount
expected target revision
encounterSessionId/attemptId
```

### 11.2 Damage statuses

```text
Applied
AppliedAndDefeated
NoChangeZero
RejectedInvalidAmount
RejectedNegativeAmount
RejectedNotAlive
RejectedWrongEncounter
RejectedStaleRevision
RejectedDuplicateMutation
RejectedTargetUnavailable
ArithmeticFailure
```

Damage amount must be finite and nonnegative. Negative damage is not healing. Healing uses a separate operation.

### 11.3 Healing statuses

```text
Applied
NoChangeZero
NoChangeAtMaximum
RejectedInvalidAmount
RejectedNegativeAmount
RejectedNotAlive
RejectedWrongEncounter
RejectedStaleRevision
RejectedDuplicateMutation
ArithmeticFailure
```

Healing cannot revive a defeated actor unless a future explicit resurrection behavior/profile and product decision is approved.

### 11.4 Defeat

The first accepted transition to zero emits one immutable `CombatantDefeated` technical event after state publication. Exact duplicate mutation returns duplicate status and emits no event.

Defeat immediately makes new actions ineligible and triggers the owning encounter’s typed resolution path. It does not directly show UI, grant rewards, save, or destroy the actor.

## 12. Mana and regeneration

### 12.1 Availability

```text
available mana = current mana - active reservations
```

Every reservation is tied to one action ID.

### 12.2 Typed operations

Minimum operations:

```text
TryReserveMana(actionId, amount, expectedRevision)
CommitManaReservation(actionId)
ReleaseManaReservation(actionId, reason)
ApplyManaRestore(mutationId, amount)
ApplyRegenerationTick(tickId, elapsedDuration, encounterClockRevision)
```

### 12.3 Validation

Reject:

- non-finite/negative cost or restore;
- cost over maximum/technical limit;
- duplicate reservation with changed amount;
- commit/release without matching reservation;
- stale resource revision;
- actor not alive/eligible;
- regeneration outside profile/encounter policy;
- non-finite/negative elapsed duration;
- arithmetic overflow.

### 12.4 Regeneration

Regeneration is driven by the encounter clock/update owner with finite elapsed time. It uses an accumulator/fixed unit or another reviewed method that avoids frame-rate-dependent value drift beyond an explicitly documented tolerance.

Regeneration policy states whether it runs during:

```text
Intro
Active
Resolving
Completed/Failed
Pause
Defeated
```

The initial production policy must be explicit. Current unconditional alive-component Update behavior is not authority.

## 13. Technical skill definition

### 13.1 Definition shape

```csharp
public sealed class CombatSkillDefinition
{
    public string Id { get; }
    public string SchemaVersion { get; }
    public string ContentVersion { get; }
    public string BehaviorProfileId { get; }
    public string TargetingProfileId { get; }
    public string ResourcePolicyId { get; }
    public string CooldownPolicyId { get; }
    public long ManaCostMicros { get; }
    public long CastDurationMicros { get; }
    public long CooldownDurationMicros { get; }
    public long RangeMicros { get; }
    public long PowerMicros { get; }
    public long BotPowerMultiplierMicros { get; }
    public string PresentationProfileId { get; }
    public string SourceRevision { get; }
    public string RawSha256 { get; }
}
```

### 13.2 Behavior is not slot

A stable `BehaviorProfileId` controls execution, for example:

```text
combat.behavior.damage_area
combat.behavior.heal_self
combat.behavior.break_damage_area
```

These examples are technical categories, not approval of exact current behavior IDs.

Moving a skill to another slot does not change behavior. Changing presentation/VFX does not change behavior. Changing behavior requires an explicit definition version/content review.

### 13.3 Presentation reference

Presentation profile contains approved content/localization and visual/audio references. Missing presentation may degrade visibly according to profile policy, but it cannot silently substitute another skill’s meaning or behavior.

### 13.4 Validation

Reject/report:

- blank/duplicate skill ID;
- unsupported schema/content version;
- blank/unknown behavior/target/resource/cooldown/presentation profile;
- missing VFX/audio/content references required by the profile;
- invalid/non-finite/negative numeric source;
- out-of-range values;
- contradictory target/behavior/range;
- heal behavior with hostile target profile;
- damage behavior with no applicable target policy;
- catalog/hash/provenance mismatch;
- unknown required realm/behavior capability;
- duplicate generated/shared contract drift.

## 14. Skill loadout definition

### 14.1 Shape

```csharp
public sealed class CombatSkillLoadoutDefinition
{
    public string Id { get; }
    public string SchemaVersion { get; }
    public string ContentVersion { get; }
    public string ChampionOrClassProfileId { get; }
    public IReadOnlyList<CombatSkillSlotBinding> Slots { get; }
    public string SourceRevision { get; }
    public string RawSha256 { get; }
}
```

Slot binding:

```text
slot index or stable input binding ID
skill definition ID/content version
optional unlock/availability profile ID
```

### 14.2 Initial four-slot policy

The current playable profile requires exactly four unique slots `0..3`. Production validation rejects missing, duplicate, out-of-range, null, or extra required-slot bindings.

A future variable-slot profile requires a versioned control/input contract. It is not inferred from array size.

### 14.3 Atomic publication

A loadout and all referenced skill/behavior/target/resource/cooldown/presentation profiles validate as one immutable snapshot before publication.

Do not publish a partial snapshot or overlay fields onto hard-coded arrays.

### 14.4 Load result

Minimum statuses:

```text
Loaded
MissingArtifact
ReadFailure
ParseFailure
InvalidCatalogIdentity
UnsupportedVersion
InvalidLoadout
InvalidSkill
InvalidReference
HashMismatch
Cancelled
Superseded
DevelopmentFallbackLoaded
```

`DevelopmentFallbackLoaded` is explicit, diagnostically visible, excluded from production authority, and cannot appear as `Loaded`.

## 15. Cross-platform catalog loading

Preserve the file/UnityWebRequest platform seam but require:

- deterministic artifact selection;
- game/catalog/loadout ID and version;
- raw SHA-256/provenance;
- strict validation before callback/publication;
- one request generation token;
- late/superseded callback rejection;
- cancellation/disposal behavior;
- immutable retained snapshot;
- no silent hard-coded fallback;
- exact diagnostics.

A component created and destroyed before an asynchronous callback cannot apply the callback to another attempt/object.

## 16. Action identity and lifecycle

### 16.1 Action request

```csharp
public sealed class CombatActionRequest
{
    public string ActionId { get; }
    public string EncounterSessionId { get; }
    public string EncounterAttemptId { get; }
    public string ActorParticipantId { get; }
    public string BehaviorOrSkillId { get; }
    public CombatActionSource Source { get; }
    public CombatTargetIntent TargetIntent { get; }
    public string ExpectedActorRevision { get; }
    public string ExpectedEncounterRevision { get; }
    public long RequestedAtEncounterMicros { get; }
}
```

Action sources include stable technical values such as:

```text
ManualInput
AssistAI
FullAutoAI
EncounterScript
```

### 16.2 Action states

```text
Requested
Rejected
Validated
ResourceReserved
Windup
Committed
Resolving
Completed
CancelledBeforeCommit
InterruptedAfterCommit
Failed
Disposed
```

Allowed transitions are explicit and tested. No coroutine presence Boolean is the authoritative state.

### 16.3 Action result statuses

Reject/terminal reasons distinguish at minimum:

```text
InvalidRequest
DuplicateExact
CorrelationConflict
ActorUnavailable
ActorDefeated
EncounterNotActive
ControlLocked
SkillUnavailable
UnsupportedVersion
TargetInvalid
OutOfRange
CooldownActive
InsufficientResource
ResourceInvalid
StaleRevision
Cancelled
Interrupted
EffectFailed
Completed
```

### 16.4 Duplicate input

Exact duplicate `ActionId` and identical request returns the existing action state/receipt. Reuse with changed actor/skill/target/revision is a correlation conflict.

Button, keyboard, auto, restored callback, and repeated coroutine paths cannot apply one action twice.

## 17. Resource, cancellation, and cooldown policy

### 17.1 Policy fields

Every skill defines:

```text
mana reservation point
mana commit point
refund policy by cancellation reason
cooldown start point
cooldown behavior on cancellation/interruption/failure
interruptible windup window
interruptible resolution window
```

Allowed values have exact technical semantics.

### 17.2 Compatibility profile

The observed current behavior is:

```text
mana removed at acceptance
no refund from manual cancellation
cooldown starts only after successful resolution
```

This is migration evidence, not blanket approval for every future skill. The initial source migration must either preserve it exactly or obtain user approval for changed gameplay feel.

### 17.3 Integrity constraints

Regardless of chosen balance policy:

- one action reserves/commits/releases each resource at most once;
- no mana is committed for a rejected action;
- no refund occurs twice;
- cooldown is created at most once;
- terminal action state records cost/refund/cooldown outcome;
- actor defeat/encounter termination/scene disposal applies an explicit cancellation reason and policy;
- effect resolution cannot occur after a terminal cancellation;
- a committed resource action cannot vanish without a terminal receipt.

### 17.4 Cooldown model

Cooldown state is keyed by actor and stable skill ID, not slot array/time field alone.

Snapshot:

```text
skill ID/content version
start encounter time
end encounter time
duration
source action ID
state revision
```

Queries distinguish unknown skill, no cooldown, active, completed, invalid clock, and unavailable encounter.

## 18. Target and participant registry

### 18.1 Participant registration

Every encounter participant has:

```text
participant ID
actor definition/profile ID
participant role
team/faction/realm context ID
life/control state
runtime handle generation
targeting/collision profile
encounter session/attempt ID
```

### 18.2 Target intent

Target intent may be:

```text
Self
ParticipantId
Point
Direction
AreaProfile
```

Raw component/name/tag lookup is not authoritative.

### 18.3 Query and hit collection

Physics can discover candidate runtime handles, but each candidate must resolve through the participant registry and pass:

- same active encounter/attempt;
- current handle generation;
- target profile/team rules;
- alive/eligible state;
- finite position/distance;
- range/shape/line-of-sight policy;
- duplicate participant suppression by stable participant ID;
- action hit ledger.

### 18.4 Dummies and ambient targets

Practice dummies are explicit practice participants. GameObject name prefixes do not define victory or target behavior.

### 18.5 Exactly-once effects

One action may affect each target according to the behavior profile. Multi-collider actors, pooled handles, child colliders, and duplicate physics hits cannot apply duplicate damage/heal/break effects.

## 19. Basic attack contract

Basic attack is a normal action behavior with:

```text
stable behavior/profile ID
power source
range/target policy
windup/recovery/cooldown policy
participant hit rules
presentation reference
```

It does not use an unrelated serialized controller value if the Champion combat profile owns attack power.

The current `50` versus `125` authority conflict must be resolved in the source migration report without unapproved balance drift.

## 20. Movement and dodge contract

### 20.1 Movement request

Contains:

```text
input command ID
actor/encounter IDs
finite normalized or raw vector per profile
delta/clock revision
control source
expected actor/control revision
```

### 20.2 Validation

Reject:

- non-finite vectors or elapsed time;
- speed/rotation/dodge values outside profile limits;
- actor defeated/disposed;
- encounter not in movement-eligible phase;
- control source not owning movement;
- stale/duplicate command;
- move outside encounter/nav/bounds policy.

### 20.3 Dodge

Dodge is an action with explicit:

- direction;
- duration/speed/distance profile;
- invulnerability/collision policy if any;
- resource/cooldown policy if any;
- interruption/encounter-end behavior;
- terminal receipt.

Current coroutine presence does not define dodge authority.

## 21. Auto/assist control

### 21.1 Control transfer

Manual, Assist, and Auto changes are typed control-state transitions with revision and source. A stale UI callback cannot take control from a newer state.

### 21.2 AI decision source

AI receives immutable encounter snapshots and submits ordinary action requests. It cannot call mutation methods outside the action boundary.

### 21.3 Entropy

Where AI choice variation is desired, use an encounter-provided entropy interface/seed and stable decision namespace for testability. Full frame-perfect deterministic replay is not required for local action combat, but global `UnityEngine.Random` is not the contract.

### 21.4 Targeting

AI selects registered participants, not `FindObjectOfType`, tags, object names, or arbitrary scene scans.

### 21.5 Stop behavior

AI produces no new action after actor defeat, encounter resolving/terminal state, scene disposal, or control transfer. Pending actions follow the explicit cancellation policy.

## 22. Boss combat profile

Conceptual shape:

```csharp
public sealed class BossCombatProfile
{
    public string Id { get; }
    public string SchemaVersion { get; }
    public string ContentVersion { get; }
    public long MaxHealthMicros { get; }
    public long ArmorMicros { get; }
    public string BasicAttackBehaviorProfileId { get; }
    public IReadOnlyList<BossPhaseDefinition> Phases { get; }
    public BossBreakProfile BreakProfile { get; }
    public BossEnrageProfile EnrageProfile { get; }
    public string TargetingProfileId { get; }
    public string RewardBindingId { get; }
    public string PresentationProfileId { get; }
    public string SourceRevision { get; }
    public string RawSha256 { get; }
}
```

### 22.1 Required validation

Reject:

- blank/duplicate ID;
- unsupported version;
- nonpositive/overflow health;
- negative/invalid armor, damage, range, cooldown, telegraph, recovery, duration, multiplier;
- enrage/phase threshold outside approved range;
- duplicate/unsorted/contradictory phase thresholds;
- unknown behavior/target/reward/presentation reference;
- invalid break profile;
- missing required ability;
- conflicting `BossDefinition`/profile values;
- source hash/provenance mismatch.

### 22.2 `BossDefinition` migration

The existing ScriptableObject is not a complete combat authority. Before production:

- ID/content reference fields are separated from technical profile fields;
- attack, armor, abilities, phase, break, enrage, reward, and presentation mapping is explicit;
- mutable loot lists are removed from runtime combat authority;
- one catalog snapshot resolves the complete boss profile;
- no runtime field remains an unreported fallback.

## 23. Boss state model

Avoid one overlapping Boolean set. Use orthogonal typed states.

### 23.1 Life

```text
Uninitialized
Alive
Defeated
Disposed
```

### 23.2 Action

```text
Idle
Windup
Committed
Recovery
Interrupted
```

### 23.3 Guard/break

```text
Stable
Depleted
Broken
Recovering
```

### 23.4 Enrage

```text
Dormant
TriggeredByHealth
TriggeredByTime
Active
```

### 23.5 Phase

Stable phase ID/index derived from validated threshold definitions and current finite health ratio. Each transition occurs at most once and emits one technical event.

### 23.6 Invariants

- defeated boss has no new action/phase/break/enrage transition;
- active windup has one action ID and one telegraph receipt;
- break transition cannot start duplicate break routines;
- attack parameters derive from base profile plus current phase/enrage snapshot, not destructive cumulative mutation;
- phase/enrage modifiers compose in a deterministic declared order;
- recovery/telegraph timers use the encounter clock;
- current/max ratios are calculated only after validated positive maxima;
- boss object destruction follows encounter/result handoff and does not destroy authoritative result state.

## 24. Boss mutation and defeat

Boss damage uses the same typed finite operation model as Champion damage, plus explicit guard/break effects from behavior profiles.

Negative/non-finite damage rejects. Broken multipliers use validated fixed or finite values with checked overflow/range behavior.

The first valid health transition to zero:

```text
marks boss defeated once
→ cancels/interrupts active boss action through policy
→ freezes further combat mutation
→ emits immutable BossDefeated technical event
→ asks encounter state machine to resolve
```

It does not:

- call loot/economy/notification services;
- create a reward request from component fields;
- generate a seed;
- show clear UI;
- destroy the authoritative encounter operation;
- fabricate fallback value.

## 25. Encounter definition

Conceptual shape:

```csharp
public sealed class ChampionEncounterDefinition
{
    public string Id { get; }
    public string SchemaVersion { get; }
    public string ContentVersion { get; }
    public ChampionEncounterMode Mode { get; }
    public string ChampionProfileRequirementId { get; }
    public string BossDefinitionId { get; }
    public string BossCombatProfileId { get; }
    public string CombatRulesProfileId { get; }
    public string ArenaProfileId { get; }
    public string ResultPolicyId { get; }
    public string RewardBindingId { get; }
    public string QuestOrProgressionContextPolicyId { get; }
    public string PresentationProfileId { get; }
    public string ResumePolicyId { get; }
    public string SourceRevision { get; }
    public string RawSha256 { get; }
}
```

## 26. Encounter modes

Minimum modes:

```text
Practice
DevelopmentDemo
AuthoritativeBoss
AuthoritativeQuest
```

Semantics:

- **Practice:** session result only; no save, reward, quest, territory, or progression.
- **DevelopmentDemo:** explicitly diagnostic and excluded from production acceptance; no durable value.
- **AuthoritativeBoss:** one durable encounter result and one #168 reward operation when valid.
- **AuthoritativeQuest:** one durable result consumed by the owning quest/NVS transaction; reward policy is explicit.

A mode mismatch rejects. The current arena cannot silently infer authoritative mode because it was reached from a scene.

## 27. Encounter request

```csharp
public sealed class ChampionEncounterRequest
{
    public string GameId { get; }
    public string CatalogSetId { get; }
    public string ProfileId { get; }
    public string EncounterDefinitionId { get; }
    public string EncounterDefinitionContentVersion { get; }
    public string EncounterSessionId { get; }
    public string EncounterAttemptId { get; }
    public string EncounterResultId { get; }
    public ChampionEncounterMode Mode { get; }
    public string ChampionDefinitionId { get; }
    public string ChampionCombatProfileId { get; }
    public string SkillLoadoutId { get; }
    public string BossDefinitionId { get; }
    public string BossCombatProfileId { get; }
    public string CommittedRealmId { get; }
    public string QuestOrProgressionContextId { get; }
    public string RewardOperationId { get; }
    public string ResumeToken { get; }
    public string ExpectedProfileRevision { get; }
}
```

Practice/development requests use explicit empty/not-applicable typed fields where allowed, not blank production-required fields.

## 28. Encounter request validation

Reject/report:

- null/blank/oversized/control-character IDs;
- unsupported mode/version;
- wrong game/catalog set;
- missing or mismatched Champion/boss/loadout/profile source;
- missing/invalid committed realm;
- realm definition/version drift;
- unsupported profile/save state;
- missing participant/team/arena rules;
- missing quest/reward/result identity for authoritative mode;
- reward operation supplied for practice/demo;
- duplicate active session/attempt/result ID conflict;
- stale profile/catalog revision;
- unavailable service/lifecycle dependency;
- development fallback source in production mode.

Validation is pure and returns an immutable resolved encounter snapshot before any runtime object is created.

## 29. Encounter lifecycle

### 29.1 States

```text
Created
Validating
Ready
Intro
Active
Resolving
CompletionPendingCommit
Completed
Failed
Cancelled
RecoveryRequired
Disposed
```

### 29.2 Core transitions

```text
Created → Validating
Validating → Ready | Failed
Ready → Intro | Cancelled
Intro → Active | Cancelled | Failed
Active → Resolving | Failed | Cancelled
Resolving → CompletionPendingCommit | Failed | RecoveryRequired
CompletionPendingCommit → Completed | Failed | RecoveryRequired
Completed/Failed/Cancelled/RecoveryRequired → Disposed
```

Practice mode may transition from Resolving to Completed through a session-only receipt without persistence.

### 29.3 Revision and clock

Every accepted transition increments an encounter revision and records encounter-clock time. Wall clock is used only for durable timestamps through an injected validated clock, not for action sequencing or random seeds.

### 29.4 Terminality

Only one terminal outcome exists per attempt. A later callback cannot convert Failed to Completed or duplicate Completed presentation/rewards.

## 30. Encounter clock

The encounter owns a monotonic time source for:

```text
intro duration
action windup/recovery/cooldown
enrage timers
break recovery
grade metrics
AI decision intervals
VFX presentation timing references
```

Requirements:

- finite nonnegative elapsed values;
- pause policy explicit;
- scene/global time-scale changes do not corrupt technical state;
- test clock injection;
- no `Time.time` as result/reward identity;
- no wall-clock rollback effect on active timing;
- terminal metrics freeze at resolution.

Visual coroutines may use Unity timing through an adapter, but technical action/encounter state follows the encounter clock.

## 31. Encounter participant construction

### 31.1 Factories

Participants are built through validated factories from immutable snapshots. The factory returns typed success/failure and one handle generation.

### 31.2 Runtime primitives

Primitive-generated Champion/boss models may remain only for an explicitly labeled Practice/DevelopmentDemo profile while final assets are unavailable. They do not become production source authority or proof of final presentation.

### 31.3 Construction transaction

Prepare:

- participant definitions/profiles;
- target registry entries;
- actor state objects;
- runtime objects/components;
- event subscriptions;
- presentation adapters.

Publish them to the encounter only after all required participants construct successfully. Partial construction rolls back/disposes safely.

### 31.4 Component initialization

`Awake`/`Start` must not independently select source, load profile, choose realm, grant value, or begin actions before the encounter injects a validated initialization context.

## 32. Encounter outcome

### 32.1 Technical outcome

Minimum statuses:

```text
ChampionVictory
ChampionDefeat
Cancelled
ValidationFailure
RuntimeFailure
RecoveryRequired
```

### 32.2 Outcome value

```csharp
public sealed class ChampionEncounterComputedOutcome
{
    public string EncounterSessionId { get; }
    public string EncounterAttemptId { get; }
    public string EncounterResultId { get; }
    public ChampionEncounterMode Mode { get; }
    public ChampionEncounterOutcome Outcome { get; }
    public string ChampionParticipantId { get; }
    public string BossParticipantId { get; }
    public long EncounterDurationMicros { get; }
    public IReadOnlyList<EncounterMetricSnapshot> Metrics { get; }
    public string SourceSnapshotHash { get; }
    public string OutcomeHash { get; }
}
```

### 32.3 Metrics

Metrics may include technical IDs/values such as:

```text
guard break observed count/time
enrage activated/survived
Champion damage taken
mana spent/refunded
skills completed/interrupted
boss phase transitions
```

They are immutable and internally validated. UI grade/recap profiles consume them; object flags are not result authority.

### 32.4 Grade

Grade calculation is a versioned presentation/result-profile decision. Current elapsed-time/guard/enrage grading is recorded as migration evidence only. A grade does not grant value by itself.

## 33. Authoritative result and consequence handoff

### 33.1 Result plan

For authoritative mode, the encounter result planner validates:

- terminal computed outcome/hash;
- current profile/result ledger state;
- encounter/result/reward/quest operation IDs;
- source/catalog revisions;
- expected save/profile revision;
- owning reward/quest adapters;
- notification/outbox definitions.

It prepares immutable operations and contains no live component/save references.

### 33.2 Candidate sequence

```text
1. freeze terminal computed outcome
2. detect exact replay/correlation conflict
3. prepare complete result/consequence plan
4. clone validated save candidate
5. record encounter terminal/result ledger state
6. apply owning quest/progression consequence through typed no-save adapter
7. add or reference #168 reward operation for valid boss victory
8. add required durable #177 notification outbox records
9. validate complete candidate and revisions
10. persist/verify through accepted #137
11. publish committed receipt/current revisions
12. emit post-commit technical events
13. enqueue result/reward presentation once
```

Combat actor/boss components do not call these domains directly.

### 33.3 Exact replay

Same `encounterResultId` and same semantic/outcome hash returns the stored committed receipt with zero reapplication or duplicate presentation.

Changed reuse is `CorrelationConflict`.

### 33.4 Failure

Known-not-committed failure shows no success and permits reviewed retry with the same operation identity.

Commit uncertainty enters `RecoveryRequired`; the encounter does not replay reward/quest operations blindly or show committed clear value.

## 34. Boss reward integration

The encounter completion receipt creates/references one stable `rewardOperationId` consumed by #168.

Requirements:

- only authoritative valid Champion victory is reward-eligible;
- Practice/DevelopmentDemo never call #168;
- boss component fields do not supply credits/items/seed;
- result/reward identities and source hashes match;
- reward failure/pending/commit states remain distinguishable from combat victory;
- clear UI displays only a committed #168 receipt or explicit pending/failure state;
- boss object may be destroyed only after the operation state has an independent owner.

## 35. Quest/NVS integration

Current Champion encounter work does not implement OMEN_1.

Future G1/C1 integration supplies:

- exact encounter request/session/result IDs;
- quest state/node/resume context;
- valid attempt/retry semantics;
- one result handoff into the atomic report/consequence transaction;
- abandonment restrictions;
- transient failure versus terminal failure meaning.

No archived A1 ID or direct quest progress call is hard-coded into combat components.

## 36. Events

### 36.1 Technical events

Examples:

```text
CombatantResourcesChanged
CombatantDefeated
ActionStateChanged
ActionCompleted
ActionCancelled
BossPhaseChanged
BossBreakChanged
BossEnrageChanged
EncounterStateChanged
EncounterOutcomeComputed
EncounterResultCommitted
```

### 36.2 Event rules

- immutable payloads;
- stable encounter/attempt/action/participant IDs;
- state revision before/after;
- emitted after authoritative state publication;
- duplicate operation emits zero duplicate event;
- subscriber exceptions are isolated and diagnosed;
- one subscriber cannot prevent others;
- events cannot mutate the originating transaction reentrantly;
- player-facing copy is not embedded.

### 36.3 Ordering

Within one state transition, order is deterministic and documented. For example:

```text
resource state published
→ resources-changed event
→ defeated event when applicable
→ encounter consumes defeat and transitions
```

## 37. Notification and presentation

### 37.1 Typed notification definitions

Use #177 definitions for:

```text
encounter.validation_failed
encounter.started
encounter.failed
encounter.victory_committed
encounter.reward_pending
encounter.reward_committed
encounter.recovery_required
```

Exact copy/tone is content-owned.

### 37.2 UI state source

HUD/panels bind immutable actor/action/encounter/result snapshots. They do not infer state from:

```text
boss GameObject null
coroutine non-null
local Boolean
last mutable loot callback
Console log
```

### 37.3 Clear/defeat presentation

One attempt/result ID produces at most one clear or defeat presentation sequence.

A controller reconstruction/scene reattach queries the existing receipt and presents according to the receipt state without replaying audio/VFX/rewards unless the presentation policy explicitly records pending presentation.

### 37.4 Reward text

Allowed states:

```text
not applicable
pending computation
pending commit
committed no reward
committed credits/items
failed known-not-committed
recovery required
unavailable
```

“Synced,” “earned,” “loot complete,” and `+value` appear only for committed data.

### 37.5 Accessibility

Presentation supports:

- non-color-only life/action/telegraph/result states;
- readable long/localized text;
- safe areas and text scaling;
- reduced motion/hit-pause/camera-shake alternatives;
- no flashing policy violations;
- control/input-independent acknowledgement/action;
- telegraphs not obscured by result/notification UI;
- blocking recovery state persistent until intentional resolution.

## 38. Retry, return, pause, scene unload, and disposal

### 38.1 Retry

Retry after terminal receipt:

- creates a new attempt ID and runtime participant set;
- retains the parent session/context according to the definition;
- cannot reuse the previous action/result/reward IDs;
- cannot replay committed value;
- resets actor/action/AI/presentation state through disposal and reconstruction, not field mutation in place.

### 38.2 Return to Kingdom

Return requires a typed encounter disposition:

- Practice/DevelopmentDemo can cancel/dispose according to policy;
- active authoritative encounter follows the definition’s abandonment/persistence rules;
- completion-pending/recovery-required state cannot be silently discarded;
- scene transition occurs after disposition ownership is stable.

### 38.3 Pause/background

Pause policy defines:

- encounter clock behavior;
- actor/action suspension;
- active windup/cooldown behavior;
- save/result ownership;
- mobile background timeout/cancel policy;
- resume validation.

Current automatic Unity component/coroutine behavior is not sufficient.

### 38.4 Scene unload

Before participant object destruction:

- stop new input/AI commands;
- resolve/cancel active actions through policy;
- detach presentation subscribers;
- retain authoritative pending/committed result owner;
- dispose participants and registry handles;
- prove no late callback can mutate another attempt/profile.

### 38.5 Component disable/destroy

Every component handles disable/destroy idempotently. Asynchronous catalog callbacks, coroutines, events, and timers cannot write into disposed state.

## 39. Realm integrity

### 39.1 Required realm

Production Champion encounters require one committed valid realm from #173 and a matching immutable realm definition/version from #183.

### 39.2 Invalid realm

`None`, undefined enum, missing definition, profile mismatch, degraded profile, or unavailable realm service returns a typed validation failure. It does not become Crownlands.

### 39.3 Practice neutral context

A Practice/DevelopmentDemo encounter may use an explicit stable neutral/test context profile. It is visibly diagnostic and is not a saved/player realm.

### 39.4 Realm use

Realm affects only fields explicitly declared by validated profiles, such as team eligibility or presentation references. Technical code does not infer unapproved bonuses or lore from an enum.

## 40. Diagnostics

Every diagnostic contains:

```text
stable code
severity
domain
encounter/session/attempt/action/participant ID where safe
source definition/profile ID
field/path
schema/content/policy version
blocks construction/action/encounter/result/presentation boolean
safe developer message
```

Code families:

```text
AL-CHAMPION-PROFILE-*
AL-COMBATANT-STATE-*
AL-COMBAT-ACTION-*
AL-SKILL-CATALOG-*
AL-SKILL-LOADOUT-*
AL-TARGETING-*
AL-BOSS-PROFILE-*
AL-BOSS-STATE-*
AL-ENCOUNTER-REQUEST-*
AL-ENCOUNTER-STATE-*
AL-ENCOUNTER-RESULT-*
AL-ENCOUNTER-PRESENTATION-*
```

Diagnostics are deterministically ordered. Player UI never exposes stack traces, paths, catalog hashes, raw exceptions, or unsanitized technical IDs without an approved safe fallback policy.

## 41. Security and abuse resistance

Reject or isolate:

- non-finite numeric/vector input;
- negative damage/heal/mana/cooldown/range/time;
- extreme values beyond technical ceilings;
- arbitrary caller-supplied reward/quest value;
- direct ServiceLocator/domain mutation from actor components;
- result/action ID reuse with changed payload;
- stale participant/runtime handles;
- name/tag/component-based authority;
- untrusted catalog version/hash;
- partial/hybrid loadouts;
- unknown behavior/VFX/action IDs;
- development fallback in production mode;
- late async callbacks;
- reentrant subscriber/action execution;
- practice result application;
- raw player text/action/scene/URL injection.

## 42. Required tests

### 42.1 Numeric/profile validation

- valid representative Champion profile;
- zero/negative/non-finite/over-ceiling each health/mana/regen/attack/movement/dodge/range/time/multiplier field;
- invalid initial current resource state;
- missing/unknown references;
- unsupported version;
- source hash/catalog mismatch;
- deterministic diagnostics.

### 42.2 Health/life state

- positive/zero/negative/NaN/infinite damage;
- positive/zero/negative/NaN/infinite healing;
- boundary damage exactly to zero;
- overkill clamp;
- heal at maximum;
- heal while defeated;
- duplicate/stale/wrong-encounter mutation;
- exactly one defeat event;
- subscriber exception isolation;
- disposed actor rejects;
- invariant and revision after every accepted mutation.

### 42.3 Mana/regen

- valid reserve/commit/release;
- insufficient mana;
- negative/NaN/infinite amount;
- duplicate exact reservation;
- action-ID conflict;
- double commit/release;
- stale revision;
- restore at maximum;
- finite elapsed accumulator across frame partitions;
- pause/intro/active/resolving/defeated policies;
- no regen after terminal/disposed state;
- exact resource event counts.

### 42.4 Skill catalog/loadout

- valid complete four-slot snapshot;
- missing file/read/parse error;
- wrong game/catalog/loadout identity;
- unsupported version;
- empty/missing slot;
- duplicate slot;
- duplicate skill ID;
- out-of-range slot;
- null row;
- blank/unknown skill/behavior/target/presentation/VFX;
- invalid numeric fields;
- contradictory behavior/target/range;
- hash/provenance mismatch;
- input arrays mutated after load cannot change snapshot;
- no partial/hybrid publish;
- explicit development fallback status;
- async late/superseded/disposed callback.

### 42.5 Behavior and slot separation

- same skill in a different slot retains behavior;
- different skill in same slot uses its own behavior;
- presentation change does not change behavior;
- unknown behavior rejects;
- behavior registry duplication/conflict;
- generated/shared contract drift.

### 42.6 Action lifecycle

- valid request through complete;
- invalid slot/skill/action ID;
- exact duplicate and conflict;
- actor/encounter/control invalid;
- cooldown active;
- insufficient resource;
- stale revisions;
- target invalid/out of range;
- cancel before reserve/commit/windup/resolve;
- interrupt after commit;
- defeat during each phase;
- encounter terminal during each phase;
- component disable/scene unload during each phase;
- selected refund/cooldown policy matrix;
- one effect/resource/cooldown/terminal receipt;
- no post-terminal resolution.

### 42.7 Targeting

- self/participant/point/direction/area intent;
- finite/non-finite positions;
- wrong encounter/team;
- stale handle generation;
- dead/ineligible target;
- range/shape/line-of-sight boundaries;
- multi-collider target affected once;
- duplicate physics hit;
- pooled/replaced participant;
- practice dummy registry behavior;
- no GameObject-name authority;
- no unrelated scene actor hit.

### 42.8 Basic attack/movement/dodge

- authoritative power source and 50/125 migration resolution fixture;
- invalid movement vector/time/profile;
- movement/control owner state;
- attack target/range/cooldown;
- action duplicate;
- dodge direction/duration/cancel/defeat;
- no action while locked/defeated/disposed;
- no direct victory/quest/reward consequence.

### 42.9 Auto/assist/bots

- control transfer revision;
- manual override;
- deterministic test entropy/vector;
- valid participant targeting;
- no global scene scan/name/tag authority;
- stop after defeat/terminal/dispose;
- pending action cancellation;
- invalid AI timing/range/profile;
- no Crownlands fallback;
- no direct reward/economy mutation.

### 42.10 Boss profile/state

- valid representative boss profile;
- each invalid health/armor/damage/range/cooldown/telegraph/enrage/break/phase value;
- incomplete/conflicting legacy definition;
- phase threshold ordering/duplicates;
- finite positive/zero/negative/NaN/infinite damage;
- overkill;
- break damage/recovery/boundaries;
- duplicate break trigger;
- phase transitions once;
- health and timed enrage once;
- base+phase+enrage composition order;
- telegraph interruption by break/defeat/encounter terminal;
- invalid max ratio never evaluated;
- boss defeat event once;
- no loot/economy/notification/seed/fallback/object-destruction authority.

### 42.11 Encounter request/construction

- valid Practice/DevelopmentDemo/AuthoritativeBoss/AuthoritativeQuest;
- missing/mismatched IDs/source versions;
- invalid committed realm;
- wrong mode/reward/quest context combination;
- duplicate active session/attempt/result conflict;
- stale profile/catalog revision;
- fallback source in production;
- participant construction failure at Champion/boss/registry/presenter stages;
- rollback/disposal after partial construction;
- no action before Ready/Active.

### 42.12 Encounter state machine

- every allowed transition;
- every prohibited transition;
- intro success/cancel/failure;
- Champion defeat;
- boss defeat;
- simultaneous terminal signals;
- duplicate death callbacks;
- failure during Resolving;
- practice session completion;
- authoritative commit success/failure/uncertain/recovery;
- terminality and revision;
- retry new attempt/result IDs;
- return/abandon policy;
- pause/resume/clock behavior;
- scene unload/disposal/late callback.

### 42.13 Result/reward/progression

After dependencies:

- authoritative victory result committed once;
- defeat result committed once where required;
- exact replay before/after reload;
- changed result-ID reuse conflict;
- stale plan;
- missing quest/reward adapter;
- failure at ledger/quest/reward/outbox/persist/verify/publish boundaries;
- commit uncertainty;
- no partial published result;
- Practice/DevelopmentDemo produces zero save/reward/quest/economy calls;
- #168 operation created/referenced once;
- exact event/save/notification counts.

### 42.14 UI/presentation

- actor/action/cooldown snapshots;
- clear once per result ID;
- defeat once per attempt;
- controller reconstruction from receipt;
- result pending/committed/no-reward/failure/recovery states;
- no mutable loot callback authority;
- no computed/uncommitted reward as owned;
- long/localized copy;
- missing content safe fallback;
- reduced motion, no color-only, safe area/text scale;
- presenter detach/reattach and scene transition;
- acknowledgement/pending presentation behavior.

### 42.15 Integration/regression

- observed current valid values migrate without unapproved drift;
- exact four current skill source migration and behavior mapping report;
- no hard-coded/StreamingAssets hybrid;
- no RealmId.None → Crownlands fallback;
- no recurring Arena credit grant;
- no boss fallback reward;
- no controller-owned service load;
- corrected #153 lifecycle;
- corrected #178 containment;
- corrected #127 PlayMode;
- #183 source provenance;
- #173 realm selection;
- #168 reward receipt;
- #177 notification receipt;
- applicable #223/#150 Player profile only when separately activated.

## 43. Retained state-machine/vector artifacts

The first pure phase includes machine-readable test fixtures for:

```text
finite scalar boundaries
health/mana transition tables
action state transition matrix
resource reservation/refund/cooldown matrix
encounter state transition matrix
boss phase/enrage/break transition matrix
skill/loadout valid/invalid catalogs
diagnostic ordering
result/replay/conflict examples
```

Each artifact records schema/policy version and expected status/revision/events.

## 44. Implementation phases

### Phase C1 — pure contracts, validation, and planners

Branch:

```text
codex/champion-combat-contract-planner
```

Allowed:

- immutable contract records;
- finite scalar/vector validators;
- Champion/boss/skill/loadout/encounter validators;
- pure actor resource transition planner;
- pure action/resource/cooldown state machine;
- pure encounter transition planner;
- pure boss phase/break/enrage planner;
- participant/target intent contracts over fake handles;
- immutable result/diagnostic models;
- retained matrices/fixtures;
- focused EditMode tests;
- technical documentation.

Prohibited:

- production `ChampionCombat`, `SkillCaster`, controller, AI, boss, or arena behavior changes;
- `ServiceLocator`/Bootloader changes;
- saves/result-ledger fields;
- real catalogs/content;
- economy/loot/quest/progression calls;
- scenes/Build Settings/UI;
- balance changes;
- Android.

Use:

```text
Refs #180
```

Do not close #180.

### Phase C2 — technical source

After #156/#183 and source review:

- Champion combat profiles;
- complete skill definitions/loadouts/behavior/target/resource/cooldown/presentation references;
- boss combat profiles and encounter definitions;
- schemas/generated contracts;
- source hashes/provenance;
- migration report for every current hard-coded/JSON/ScriptableObject value;
- exact behavior and balance drift report.

Content-owned fields change in separate focused source-mode PRs when required.

### Phase C3 — actor/caster/boss runtime migration

After C1/C2 and corrected realm/lifecycle prerequisites:

- inject validated construction context;
- migrate health/mana/movement/attack/dodge/cast/cooldown behavior;
- remove slot-based behavior and hybrid fallback;
- implement participant registry and action IDs;
- migrate boss state and remove reward side effects;
- migrate AI through action requests;
- focused runtime/EditMode tests.

Do not add durable encounter consequences in this phase.

### Phase C4 — encounter/result orchestration

After accepted #137/#168/#177 and owning quest/progression adapters:

- encounter request/state/orchestrator;
- result/operation ledger integration;
- reward/quest/outbox handoffs;
- retry/resume/return/disposal semantics;
- committed receipt query;
- fault/reload/duplicate matrix.

### Phase C5 — scene/UI/Player integration

After #223/#150 activation and corrected #127/#178:

- scene factory/context injection;
- HUD binds immutable snapshots/receipts;
- committed clear/defeat/reward presentation;
- accessibility/reduced-motion validation;
- safe PlayMode and actual Player evidence;
- Codex technical review, narrative/content fidelity review where applicable, and user integrated playtest.

## 45. Expected file boundaries

### C1 likely

```text
unity/Assets/AL/Scripts/ChampionMode/Contracts/**
unity/Assets/AL/Scripts/ChampionMode/Validation/**
unity/Assets/AL/Scripts/ChampionMode/Planning/**
unity/Assets/AL/Tests/EditMode/ChampionCombat/**
unity/SharedContracts/** only with declared generated/shared scope
```

Use existing assemblies/namespaces when narrower. Avoid one monolithic runtime rewrite.

### Later runtime files

```text
unity/Assets/AL/Scripts/ChampionMode/Control/ChampionCombat.cs
unity/Assets/AL/Scripts/ChampionMode/Control/ChampionController.cs
unity/Assets/AL/Scripts/ChampionMode/Skills/SkillCaster.cs
unity/Assets/AL/Scripts/ChampionMode/Skills/SkillLoadoutCatalog.cs
unity/Assets/AL/Scripts/ChampionMode/AI/AutoCombatController.cs
unity/Assets/AL/Scripts/ChampionMode/AI/BotChampionAI.cs
unity/Assets/AL/Scripts/ChampionMode/AI/BossDummyAI.cs
unity/Assets/AL/Scripts/ChampionMode/ChampionArenaSceneController.cs
focused interfaces/factories/orchestrators/tests
approved catalog/schema/source artifacts
```

### Explicitly prohibited in C1

```text
Bootloader.cs
ServiceLocator.cs
SaveGameData.cs
LocalSaveGameService.cs
LocalGameDataService.cs
LocalResourceService.cs
LocalWarzoneCreditService.cs
LocalQuestService.cs
LocalBossLootService.cs
*.unity
EditorBuildSettings.asset
Android source
narrative/terrestrial source
```

## 46. Shared-file and lock policy

C1 requires no designated shared-file lock.

A later encounter ledger/persistence PR must declare the `SaveGameData.cs` lock and coordinate with #137, battle/boss/world/relationship ledgers, notification outbox, and NVS fields.

A later catalog service migration must declare `LocalGameDataService.cs` when that exact phase is approved.

Runtime files listed above are not designated global shared files but require overlap inspection and focused soft ownership when concurrent PRs exist.

No conflict resolution may discard valid current services, fields, tests, contracts, assets, or registrations.

## 47. Validation evidence

### 47.1 C1 canonical evidence

Run from:

```text
D:\260711\MY\AndroidStudioProjects\AnotherLife\unity
Unity 2022.3.62f3
```

Record:

- exact base/head SHA;
- complete changed-file/classification list;
- compile/import command, exit, final markers, error scan;
- focused ChampionCombat EditMode totals/XML/log;
- complete EditMode totals/XML/log;
- retained transition/validation matrix command/output;
- reflection/immutability tests;
- forbidden production/ServiceLocator/save/economy/loot/quest/source token scan;
- `git diff --check`;
- final status and deferred evidence.

### 47.2 Later runtime evidence

Add:

- strict valid/invalid source catalogs;
- async/late callback fixtures;
- actor/action/boss/encounter fault injection;
- exact event/resource/cooldown/result counts;
- duplicate/retry/reload/resume/scene-unload behavior;
- corrected #127 PlayMode profile isolation;
- actual production scene/Player profile when activated;
- severe-log and missing-script/source provenance scans;
- accessibility and integrated user playtest.

Duplicate-workspace, stale-base, skipped, compile-only, missing XML, development fallback, wrong-policy green, or `continue-on-error` results are not passing evidence.

## 48. Review questions

GPT review verifies:

- all numeric/vector inputs are finite and bounded;
- actor/action/boss/encounter state machines are explicit and total;
- resource/cooldown/cancel semantics are unambiguous and duplicate-safe;
- source behavior is not tied to slot or hard-coded fallback;
- loadout publication is atomic and versioned;
- realm fallback is eliminated;
- targeting uses encounter participants and stable IDs;
- boss death has no reward/save/UI authority;
- Practice/DevelopmentDemo cannot mutate progression/value;
- one authoritative result/reward/quest operation identity exists;
- UI uses committed/session receipts, not component/null/callback state;
- no unapproved balance or content drift occurs;
- canonical evidence is complete.

Codex narrative/content review is required when player-facing skill/boss/encounter copy or meaning changes. User approval is required for balance/cancellation-feel changes and final integrated combat acceptance, not for the pure C1 technical contracts.

## 49. Acceptance criteria

### Specification acceptance

- [x] Current Champion, skill catalog, behavior, realm, controller, AI, boss, encounter, reward, and UI defects are inventoried.
- [x] Engineering/content/balance/user authority is separated.
- [x] Stable identity, numeric source, immutable profile, loadout, action, participant, encounter, outcome, and receipt contracts are exact.
- [x] Health/mana/life/event invariants and typed outcomes are exact.
- [x] Resource reservation/commit/refund/cooldown/cancellation lifecycle is exact.
- [x] Skill behavior is separated from slots and presentation.
- [x] Targeting/participant/exact-hit rules are explicit.
- [x] Boss profile/state/defeat rules and reward separation are exact.
- [x] Encounter mode/state/result/retry/resume/disposal and durable handoff are exact.
- [x] Realm, notification, UI, accessibility, failure, replay, and security boundaries are explicit.
- [x] Phase/file/lock/test/evidence boundaries are implementation-ready.
- [x] No balance, authored content, save implementation, runtime behavior, scene, Android, or unrelated change is authorized.

### Issue completion acceptance

#180 remains open until:

- [ ] C1 pure contracts/validators/planners are implemented and accepted.
- [ ] Versioned authoritative Champion/skill/boss/encounter source exists.
- [ ] Runtime actor/caster/controller/AI/boss components consume validated context.
- [ ] Invalid numeric/vector state cannot poison combat.
- [ ] Skill loadout/behavior publication is strict, atomic, and fallback-honest.
- [ ] Realm fallback is removed.
- [ ] Action/resource/cooldown/cancel behavior is duplicate-safe.
- [ ] Boss death performs no direct reward/economy/notification mutation.
- [ ] Encounter lifecycle/result identity is explicit and durable where required.
- [ ] Practice/demo cannot mutate progression/value.
- [ ] Clear/defeat/reward UI consumes valid receipts exactly once.
- [ ] Complete validation, fault, retry, reload, scene-unload, PlayMode, and applicable Player evidence passes canonically.
- [ ] No unapproved tuning, content, VFX, Android, or unrelated change is included.

## 50. Immediate handoff

Codex engineering may now start only:

```text
branch: codex/champion-combat-contract-planner
scope: C1 immutable contracts, finite validators, actor/action/resource/cooldown/boss/encounter transition planners, fake participants/targets, matrices, and tests
completion link: Refs #180
shared locks: none
```

It must not edit production components, services, saves, source catalogs, callers, scenes, UI, Android, balance, or close #180.
