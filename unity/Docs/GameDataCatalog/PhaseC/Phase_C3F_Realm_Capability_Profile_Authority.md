# Phase C3F Realm Capability-Profile Authority

## Document control

| Field | Value |
| --- | --- |
| Tracked issue | [#183](https://github.com/yulee94/AnotherLife/issues/183) |
| Cross-scope implementation issue | [#174](https://github.com/yulee94/AnotherLife/issues/174) |
| Phase | `Phase C3F — realm capability-profile authority` |
| Primary mode | Codex coordination/review |
| Audited current main | `5c8db024165788b5bf46a28a8317b99e6aa64473` |
| Frozen Phase C2 candidate | `game-data-phase-c-six-family-technical-source-2026-07-23-v001` |
| Frozen Phase C2 raw SHA-256 | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` |
| Binding catalog specification | `unity/Docs/Game_Data_Catalog_Authority_Spec.md` |
| Binding battle migration specification | `unity/Docs/Battle_Computation_Result_Transaction_Spec.md` |
| Current runtime authority | Unchanged |
| Shared-file lock | None |
| Realm capability-profile source disposition | Identity and migration source resolved; implementation pending |
| Production eligibility | `false` |
| User balance, activation, playtest, and release approval | Pending |

This decision identifies the only current gameplay source that can safely
define technical realm capability-profile identities: the realm multiplier
table already preserved as migration evidence by the battle computation
specification. It fixes four exact profile IDs, their existing behavior, their
fixed-point representation, and their one-to-one realm references without
changing any value or treating current tuning as final balance approval.

This document is source and coordination evidence only. It does not implement
a profile registry, edit the frozen Phase C2 candidate, produce a replacement
candidate, change the legacy battle simulator, publish a common realm artifact,
or make the realm family or issue `#183` complete.

## 1. Scope and non-goals

This phase decides:

- the exact canonical stable ID for each current realm battle profile;
- the exact authored-order realm-to-profile relation;
- the existing condition and matched/default multiplier for each profile;
- the integer fixed-point representation required by a later pure registry;
- the boundary between gameplay behavior, player-facing copy, specialized
  narrative data, and presentation systems;
- the source, implementation, approval, shadow, and rollback gates that remain.

This phase does not:

- edit
  `unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source.json`;
- change `DeterministicBattleSimulator`, `BattleRequest`, `RealmDefinition`,
  `LocalGameDataService`, `RealmCatalogRuntime`, a schema, save, scene, UI,
  asset, workflow, package, or dependency;
- approve new multipliers, perks, terrain behavior, economy behavior, reward
  behavior, starting values, final copy, or visual meaning;
- infer a capability from a realm name, description, identity pillar, starter
  class bias, UI command label, color, emblem, architecture, weather,
  territory, rare resource, or Champion skill;
- authorize a seventh required catalog family or a new runtime service read;
- authorize production generation, publication, consumer migration,
  activation, integrated playtest, or release.

## 2. Authority boundary

### 2.1 Accepted migration source

`DeterministicBattleSimulator.GetRealmMultiplier` is current executable
gameplay behavior. The merged battle computation specification records the
same four conditions and values as migration evidence and requires
representative battle tuning vectors to remain stable unless separately
approved.

That combination is sufficient to name and reproduce the existing behavior in
a later immutable technical profile registry. It is not evidence that the
values are ideal, final, or user-approved. The registry must describe current
behavior exactly so future battle-integrity work can detect drift while the
product and balance approval gate remains pending.

### 2.2 Rejected sources

| Candidate evidence | Disposition | Reason |
| --- | --- | --- |
| `LocalGameDataService` realm descriptions | Rejected as profile authority | The descriptions claim Stone, defense, Wood, Magic, Gold, attack, critical-hit, and speed perks that are not implemented by the common realm data path. Copy cannot create gameplay authority. |
| `al_realm_catalog.json` identity pillars and starter-class bias | Rejected as profile authority | These fields support narrative identity and selection guidance, not a deterministic capability calculation. |
| `RealmSelectionController` command labels | Rejected as profile authority | `FORTRESS ECONOMY`, `GROWTH ENGINE`, `ROYAL COMMAND`, and `SHADOW WARFARE` are presentation labels, not validated behavior records. |
| Champion skill effects | Rejected as common realm profiles | Champion abilities belong to Champion/battle effect authority and cannot be generalized into a realm capability because a presentation or character source associates them with a realm. |
| Terrain multiplier logic | Excluded from these profiles | Terrain is a separate battle input and profile domain. Realm-aware terrain outcomes do not belong in the realm capability-profile reference array. |
| Weather, architecture, kingdom layouts, colors, emblems, and other visual treatments | Rejected as gameplay capability authority | These systems own presentation or downstream domain data, not common realm gameplay modifiers. |
| Territory and rare-resource relations | Rejected as capability authority | A world or resource relation does not imply a perk, rate, multiplier, or combat condition. |

No rejected source may be used to add a second profile, fill a missing field,
rename a technical profile, derive player-facing copy, or justify a balance
change.

## 3. Exact technical profile identities

All multipliers below are recorded in millionths. This preserves the exact
decimal intent of the current source without making binary floating-point
values part of a future catalog contract.

| Authored realm order | Realm ID | Exact profile ID | Match condition | Matched multiplier | Default multiplier | Observed legacy result |
| ---: | --- | --- | --- | ---: | ---: | --- |
| 1 | `crownlands` | `battle_realm_crownlands` | `constant` | `1,060,000` | `1,060,000` | `1.06f` |
| 2 | `stonehold` | `battle_realm_stonehold` | `own_army_has_siege` | `1,100,000` | `1,060,000` | `1f + 0.10f`, otherwise `1f + 0.06f` |
| 3 | `eldergrove` | `battle_realm_eldergrove` | `own_army_has_ranged` | `1,100,000` | `1,050,000` | `1f + 0.10f`, otherwise `1f + 0.05f` |
| 4 | `umbral` | `battle_realm_umbral` | `own_side_is_attacker_or_battle_is_pvp` | `1,090,000` | `1,040,000` | `1f + 0.09f`, otherwise `1f + 0.04f` |

The exact future realm references are:

| Realm ID | Exact `capability_profile_ids` |
| --- | --- |
| `crownlands` | `["battle_realm_crownlands"]` |
| `stonehold` | `["battle_realm_stonehold"]` |
| `eldergrove` | `["battle_realm_eldergrove"]` |
| `umbral` | `["battle_realm_umbral"]` |

Each array contains exactly one ID. Array order is therefore fixed and still
validated. No aliases exist.

## 4. Behavior semantics

The later technical registry and pure battle computation must apply these
rules:

1. `constant` always returns its matched value.
2. `own_army_has_siege` matches only when the validated immutable army
   snapshot for the evaluated side contains a current `TroopType.Siege` stack
   with a positive validated count.
3. `own_army_has_ranged` matches only when the validated immutable army
   snapshot for the evaluated side contains a current `TroopType.Ranged` stack
   with a positive validated count.
4. `own_side_is_attacker_or_battle_is_pvp` matches when the evaluated side is
   the attacker or the validated battle type is `BattleType.PvP`. Both Umbral
   sides therefore match in PvP.
5. Conditions evaluate only supplied validated request and army snapshots.
   They perform no service lookup, asset lookup, scene lookup, save read,
   mutation, allocation-dependent enumeration, or fallback resolution.
6. `1,000,000` represents neutral `1.0`. Multiplication and rounding policy
   remain owned by the battle computation contract, not by realm catalog
   generation.
7. The current floating-point expressions are observed migration evidence.
   The future profile source stores millionths and must not serialize locale-
   dependent floats.

The `own_` prefix is intentional: conditions are evaluated independently for
the attacker and defender rather than hard-coding request-side field names
inside a reusable profile.

## 5. Identity and validation rules

The following rules are binding:

- profile IDs are exact, lower-snake, case-sensitive canonical stable IDs;
- realm references must equal the one-element arrays in Section 3;
- no whitespace trimming, case folding, separator rewriting, enum-name
  conversion, substring matching, or alias fallback is permitted;
- empty, null, duplicated, reordered, unknown, cross-realm, or extra profile
  references are invalid;
- `RealmId.None`, undefined enum values, unknown realm IDs, and unknown
  condition values are invalid;
- a rejecting input must not become Crownlands, a neutral profile, or any
  other valid record;
- matched and default values must be positive integers and equal the exact
  millionth values in Section 3;
- the Crownlands matched and default values must both remain `1,060,000`;
- terrain IDs and terrain multipliers must not appear in these four records;
- resource rates, rewards, defense, attack, critical-hit, speed, Magic, Wood,
  Gold, Stone, architecture, weather, and visual data must not appear in these
  four records;
- player-facing names or descriptions must not be generated from the
  technical IDs or condition enum names.

A future schema may expose the profile IDs only as an exact allowed set and
must validate the full realm-to-profile relation. Merely accepting any known
profile ID in any realm record is insufficient.

## 6. Relationship to battle-integrity work

Issue `#174` owns the pure deterministic battle computation and its immutable
request/profile inputs. This decision does not pre-implement that work.

The intended boundary is:

```text
versioned realm source
  -> exact capability_profile_ids
  -> immutable technical profile registry
  -> validated profile snapshot supplied to pure battle computation
```

The battle computation must not query a catalog service during calculation.
An orchestration boundary may resolve the exact profile before computation and
supply an immutable snapshot. The legacy simulator remains unchanged until the
separately reviewed battle migration consumes it.

Terrain profiles remain separate immutable battle inputs. This realm profile
registry supplies only the four realm multiplier behaviors in Section 3.

## 7. Blocker disposition

This phase changes the effective source disposition of
`realms.capability_profiles` from “no defensible identity or source” to:

> Exact identity and migration source resolved; registry, schema binding,
> rejecting constraints, and tests pending.

The blocker must remain in any generated candidate until an engineering slice
implements and verifies:

- an immutable exact four-profile registry;
- typed condition identities and fixed-point millionth values;
- the exact four realm reference arrays;
- schema allowed-set and full-record relation constraints;
- rejection and legacy-drift tests.

After that implementation is merged, a later blocker-ledger or v002 source
decision may remove `realms.capability_profiles` from the realm-specific source
blocker array. It must still leave top-level
`approval.user_creative_balance` unresolved and `productionEligible: false`
until the user explicitly approves balance and every other required family and
global gate is resolved.

## 8. Engineering handoff

The next implementation slice must:

1. add an immutable profile record with exact ID, typed condition, matched
   millionths, and default millionths;
2. add an authored-order registry containing exactly the four Section 3
   records and exact typed realm resolution;
3. bind `GameDataSixFamilySchemas.Realms` to the exact profile-ID allowed set
   and exact per-realm array;
4. keep the registry in the pure catalog assembly and free of Unity object,
   scene, service locator, file, network, clock, random, or mutable collection
   dependencies;
5. reject null, empty, unknown, case-variant, separator-variant, duplicate,
   reordered, extra, cross-realm, and undefined-enum inputs;
6. test every matched/default condition, including both Umbral sides in PvP;
7. test the four legacy source expressions against the fixed-point records so
   unreviewed tuning drift fails visibly;
8. preserve the frozen Phase C2 candidate and current runtime behavior;
9. avoid creating a seventh production family, runtime loader, fallback,
   player-facing string, or new dependency.

The implementation may name a typed condition enum for code clarity. Its
serialized technical tokens, if exposed, must equal the exact condition tokens
in Section 3.

## 9. Pinned current evidence

Hashes below are lower-case SHA-256 over the exact committed file bytes visible
at the audited current main.

| Source | Source revision | Raw SHA-256 | Role |
| --- | --- | --- | --- |
| `unity/Assets/AL/Scripts/Battle/Simulator/DeterministicBattleSimulator.cs` | `bee6a13bdfb653ab875b3f8f847fe659978dd55b` | `20860acf2fd74d37fe83e8b238049d98037b606886102d3fe6a81c1b69e4e9e3` | Executable realm multiplier migration source |
| `unity/Docs/Battle_Computation_Result_Transaction_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `3ee3048b272527534194a0c73a22a76c094e0166a7186c9ea2e676bf3c847cec` | Battle migration, purity, compatibility, and drift boundary |
| `unity/Docs/Game_Data_Catalog_Authority_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `d8a0e2fdcd4e98bbb6379a8f2b7d7c733f869bd95f99112e1635f25dafb7b74d` | Common realm record and approval contract |
| `unity/Docs/GameDataCatalog/PhaseC/Phase_C3A_Realm_Authority_Convergence.md` | `27e2d477ad98f131593b991c85ce8388d9216641` | `4d220976127f1c80c407a42ce987aa50a38dbee268ecc7a80258f3178c77925f` | Authored realm order and source-separation decision |
| `unity/Docs/GameDataCatalog/PhaseC/Phase_C3E_Realm_Blocker_Ledger_Convergence.md` | `4edbac7a4f53f8b7287da3c9ecf1299286e8d6fc` | `0eb1f95b22e00b7ffd66f9cfb729a0456beb85bb161a0cd64aa7cfca40257955` | Sole remaining realm blocker and handoff criteria |
| `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs` | `efd64249c96761d2c0f1e0097c4402d46231c09a` | `7be267f64de24718090170af779ce57b5ffd88eb50a55e9d4e5ff011443276f9` | Rejected perk-copy evidence |
| `unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json` | `2119c89bfa985a0a3e273042cf086a99a49b45b0` | `33321936662b98f9c18edf4122ad163053d1aff3017b06556cad694420e9e8d8` | Rejected narrative/selection capability evidence |
| `unity/Assets/AL/Scripts/UI/RealmSelection/RealmSelectionController.cs` | `dcc9411e140990542271277ad44d9d2b89383a58` | `e1e207f1177daa71662cd92e32cdbea7a16480adf7be069cdc63c4c56d60a867` | Rejected selection-label evidence |
| `unity/Assets/AL/Scripts/ChampionMode/Skills/SkillEffectFactory.cs` | `7edc3c28110a66bc228f03dda7e725ca14e49cd3` | `f67baca7e2a9e749a3ee1fe65d700ea60678d652cb70b0b8df8f4c145dd8ae19` | Rejected Champion-domain evidence |
| `unity/Assets/AL/Scripts/RealmWar/Warzone/WeatherProfileData.cs` | `78e5d7eb845e1ee79e1729b6f79a0bec46efaa57` | `e3f3664425ac739a8784b0786f02781d47a5ca76ac6b5def6843af89f99a5b03` | Rejected weather/presentation evidence |
| `unity/Assets/AL/Scripts/Kingdom/Visuals/KingdomVisualizer.cs` | `7ae9eb7f153ed8601c21bb7f7843560e77bb55aa` | `20d8789651ce5d161332af0f264b2f7e500e23b452e5f776e5dd7062a6823c86` | Rejected kingdom-visual evidence |

Any change to a pinned byte, realm multiplier, condition, stable ID, enum
identity, authored order, or referenced source blocks later generation until a
reviewed superseding decision reconciles it. Presentation-only source changes
do not change gameplay profile authority, but they may require their own
domain review.

## 10. Approval, shadow, and rollback boundaries

Technical migration approval in this document does not satisfy user balance
approval. The current values remain reproducible while the user may later
approve, revise, or reject them through an explicit balance decision.

A later registry and schema implementation remains non-production and
unwired. A later v002 technical source remains non-production while any
required family or approval gate is unresolved. Shadow generation,
publication, comparison, runtime migration, activation, and release remain
separate reversible phases.

Rollback for this phase is deletion of this document. Rollback for the future
registry is removal of the unwired registry/schema constraints while leaving
the current simulator, realm selection, saves, scenes, assets, frozen v001
candidate, and runtime authority unchanged.

## 11. Validation and acceptance

Phase C3F is accepted when review verifies:

- [x] the four exact IDs satisfy the canonical stable-ID grammar;
- [x] every realm has exactly one exact profile reference in authored order;
- [x] conditions and values reproduce the current realm multiplier table
  without changing it;
- [x] fixed-point values are exact millionths and not binary float output;
- [x] aliases, fallbacks, normalization, and cross-realm substitution are
  forbidden;
- [x] terrain and all rejected copy, narrative, Champion, weather,
  architecture, resource, and visual evidence remain outside the profiles;
- [x] technical migration identity is separated from user balance approval;
- [x] the relationship to the pure battle implementation in `#174` is
  explicit;
- [x] the engineering registry/schema/test gate remains open;
- [x] the frozen Phase C2 candidate remains byte-for-byte unchanged;
- [x] source revisions and raw hashes are pinned;
- [x] no runtime, schema, source JSON, asset, save, workflow, dependency, or
  generated artifact changes in this phase.

## Impact

This decision adds documentation only. It adds no runtime code, generated
catalog, texture, mesh, audio, scene, save field, loader, allocation, frame
loop, render cost, build byte, install byte, package, or dependency.
Performance, memory, package size, install size, and device compatibility are
unchanged.
