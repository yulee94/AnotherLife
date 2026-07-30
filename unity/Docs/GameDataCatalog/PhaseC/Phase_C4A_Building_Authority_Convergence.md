# Phase C4A Building Authority Convergence

## Document control

| Field | Value |
| --- | --- |
| Tracked issues | [#183](https://github.com/yulee94/AnotherLife/issues/183), [#165](https://github.com/yulee94/AnotherLife/issues/165) |
| Phase | `Phase C4A — building-family authority convergence` |
| Primary mode | Codex coordination/review |
| Audited current main | `1c914b2e2090c07b440600553b4db42b52d9ff5a` |
| Frozen v001 candidate | `game-data-phase-c-six-family-technical-source-2026-07-23-v001` |
| Current v002 candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v002` |
| Binding specifications | `Game_Data_Catalog_Authority_Spec.md`, `Progression_Definition_Order_Transaction_Spec.md` |
| Runtime authority | Unchanged |
| Shared-file lock | None |
| Building family disposition | `blocked_required` |
| Production eligibility | `false` |
| User balance, activation, playtest, and release approval | Pending |

This decision reconciles the frozen Phase C building inventory with the
gameplay-authoritative construction behavior merged in PR
[#358](https://github.com/yulee94/AnotherLife/pull/358). It accepts exact
current maximum-level, initial-level, construction-cost, duration,
prerequisite, and realm-eligibility behavior as migration input for a later
unwired registry. It does not approve new balance or resolve missing
production and common asset authority.

This is a versioned status overlay. It does not edit v001 or v002, generate a
building artifact, change a schema or runtime service, publish a catalog, or
make #165 or #183 complete.

## 1. Scope and non-goals

C4A decides:

- the exact 15 supported building identities and aliases;
- the current effective initial and maximum levels;
- the exact construction cost and duration source that later engineering may
  migrate without value drift;
- stable IDs for those technical profiles;
- explicit no-prerequisite and all-realm eligibility profiles that preserve
  current behavior;
- which frozen building blockers are source-resolved, narrowed, or open;
- the schema and registry work required before another technical-source
  candidate may remove a building blocker.

C4A does not:

- create `ManaShrine` or `Mine` definitions, aliases, content, costs,
  production, assets, or saved rows;
- change any cost, duration, maximum, initial level, resource split, rounding,
  availability, prerequisite, or realm eligibility;
- select a production rate or reconcile conflicting live/offline rate prose;
- promote realm-specific Town Hall or Workshop models into a common
  per-building asset reference;
- edit `LocalGameDataService`, `LocalBuildingService`, `BuildingDefinition`,
  a save, scene, asset, `.meta` file, workflow, package, or dependency;
- alter runtime authority, publication, activation, playtest, or release
  state.

## 2. Source precedence

Later building-source work must consume these sources in order:

1. The Phase C1 content map owns the exact 15 player-facing name references.
2. The frozen v001 candidate owns canonical IDs, exact PascalCase aliases, and
   the unavailable `ManaShrine`/`Mine` evidence.
3. PR #358's current `LocalGameDataService` construction matrix owns the
   executable migration values for Level 1–10 costs and durations.
4. `LocalBuildingService` owns the current effective Level 0 start,
   definition-backed quote, maximum, exact-cost, and exact-duration behavior.
5. `BuildingConstructionAuthorityTests` owns retained regression vectors for
   the merged construction behavior.
6. The two binding specifications own immutable definition/profile shape,
   validation, provenance, migration, and later publication requirements.

`ProjectInitializer`, UI labels, enum/string formatting, architecture builder
names, layout slots, and presentation catalogs are not building-definition
authority.

PR #376's research/training planners reuse `BuildingConstructionCost` as an
immutable resource-amount value. They add no building identity, progression
profile, registry, catalog record, source approval, or production authority.

Current values are accepted as migration evidence because they are merged,
live, and explicitly tested. They remain `migration_evidence_only`; changing
them requires a separate balance decision and user approval.

## 3. Exact building identities

The supported definition order remains:

| Order | Canonical ID | Exact legacy alias | Name reference |
| ---: | --- | --- | --- |
| 1 | `town_hall` | `TownHall` | `building.town_hall.name` |
| 2 | `farm` | `Farm` | `building.farm.name` |
| 3 | `lumber_mill` | `LumberMill` | `building.lumber_mill.name` |
| 4 | `quarry` | `Quarry` | `building.quarry.name` |
| 5 | `gold_mine` | `GoldMine` | `building.gold_mine.name` |
| 6 | `barracks` | `Barracks` | `building.barracks.name` |
| 7 | `academy` | `Academy` | `building.academy.name` |
| 8 | `market` | `Market` | `building.market.name` |
| 9 | `storehouse` | `Storehouse` | `building.storehouse.name` |
| 10 | `forge` | `Forge` | `building.forge.name` |
| 11 | `stable` | `Stable` | `building.stable.name` |
| 12 | `workshop` | `Workshop` | `building.workshop.name` |
| 13 | `embassy` | `Embassy` | `building.embassy.name` |
| 14 | `wall` | `Wall` | `building.wall.name` |
| 15 | `watchtower` | `Watchtower` | `building.watchtower.name` |

Aliases remain exact, case-sensitive, and versioned under migration issue
`#165`. Case variants, whitespace variants, normalized values, display
strings, and `ManaShrine`/`Mine` do not resolve.

## 4. Exact progression profile decision

Every supported building has:

```text
initial_level = 0
max_level = 10
duration_profile_id = building_upgrade_duration_common
prerequisite_profile_id = building_prerequisite_none
realm_eligibility_profile_id = building_realm_eligibility_all
```

Profile record version is `1`. The prerequisite profile permits no additional
requirement and the realm-eligibility profile permits every supported realm.
Those neutral profiles encode the merged current behavior. They do not
authorize future Town Hall gating, queues, builder capacity, cancellation,
refunds, demolition, speedups, or realm-exclusive buildings.

### 4.1 Duration profile

`building_upgrade_duration_common` accepts target levels 1 through 10 and
returns these exact integer seconds:

| Target level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Seconds | 10 | 30 | 120 | 300 | 900 | 1800 | 3600 | 7200 | 14400 | 28800 |

Zero, negative, above-10, non-integer, overflowing, or unsupported input is
rejected. No clock or speedup policy is implied by the duration value.

### 4.2 Cost profiles

The ordered base budgets by target level 1–10 are:

```text
100, 175, 300, 475, 700, 1000, 1400, 1900, 2500, 3250
```

Each building owns one stable cost profile:

| Canonical building | Cost profile ID | Scale % | Exact ordered resource split |
| --- | --- | ---: | --- |
| `town_hall` | `building_upgrade_cost_town_hall` | 140 | Stone 45%, Wood 35%, Gold remainder |
| `farm` | `building_upgrade_cost_farm` | 80 | Wood 70%, Stone remainder |
| `lumber_mill` | `building_upgrade_cost_lumber_mill` | 80 | Wood 70%, Stone remainder |
| `quarry` | `building_upgrade_cost_quarry` | 90 | Wood 40%, Stone remainder |
| `gold_mine` | `building_upgrade_cost_gold_mine` | 100 | Wood 40%, Stone remainder |
| `barracks` | `building_upgrade_cost_barracks` | 110 | Stone 55%, Wood 30%, Gold remainder |
| `academy` | `building_upgrade_cost_academy` | 120 | Stone 40%, Wood 25%, ManaStone remainder |
| `market` | `building_upgrade_cost_market` | 90 | Wood 45%, Stone 25%, Gold remainder |
| `storehouse` | `building_upgrade_cost_storehouse` | 85 | Wood 60%, Stone remainder |
| `forge` | `building_upgrade_cost_forge` | 115 | Stone 45%, Wood 25%, Ore remainder |
| `stable` | `building_upgrade_cost_stable` | 100 | Wood 55%, Stone 25%, Gold remainder |
| `workshop` | `building_upgrade_cost_workshop` | 110 | Stone 45%, Wood 25%, Ore remainder |
| `embassy` | `building_upgrade_cost_embassy` | 120 | Wood 45%, Stone 25%, Gold remainder |
| `wall` | `building_upgrade_cost_wall` | 95 | Stone 55%, Wood 30%, Gold remainder |
| `watchtower` | `building_upgrade_cost_watchtower` | 100 | Stone 55%, Wood 30%, Gold remainder |

For a target level, the budget is:

```text
ceil(base_budget × scale_percent / 100)
```

For two-resource profiles, the first amount is
`max(1, floor(budget × first_percent / 100))` and the second receives the
exact positive remainder. For three-resource profiles, the first two amounts
use the same `max(1, floor(...))` rule and the third receives the exact
positive remainder. Ordered amounts must sum exactly to the budget. Unknown
resources, duplicate resources, non-positive amounts, gaps, alternative
rounding, or caller-supplied values are invalid.

## 5. Blocker disposition

The literal v002 building row remains unchanged and `blocked_required`.
Current effective dispositions are:

| Blocking ID or prerequisite | C4A disposition |
| --- | --- |
| `buildings.max_level_review` | **Source resolved; registry/schema implementation pending.** Level 0–10 is merged behavior. Maximum 10 is migration evidence, not a new balance approval. |
| `buildings.cost_profiles` | **Source resolved; registry/schema implementation pending.** The exact 15 profile IDs, scale factors, resource order, split, rounding, and Level 1–10 budgets are fixed above. |
| `buildings.duration_profiles` | **Source resolved; registry/schema implementation pending.** The exact shared profile ID and ten durations are fixed above. |
| Initial-level policy | **Source resolved; schema implementation pending.** Missing supported state is effective Level 0 without seeding. The common schema does not yet carry `initial_level`. |
| Prerequisite profile | **Source resolved; schema implementation pending.** `building_prerequisite_none` preserves the explicit current no-gating hold. |
| Realm eligibility profile | **Source resolved; schema implementation pending.** `building_realm_eligibility_all` preserves current non-realm-exclusive definition behavior. |
| `buildings.production_profiles` | **Open.** An immutable provider contract exists, but current main contains no production contribution provider implementation or approved profile records. Conflicting live/offline prose and formulas remain non-authoritative. |
| `buildings.asset_refs` | **Open.** The current presentation catalog covers realm-specific Town Hall and Workshop models only. It does not provide one reviewed common `asset_ref` for all 15 definitions, and its realm dimension cannot be silently collapsed into the current singular field. |

No building blocker is removed from a technical-source candidate in C4A.
After the registry/schema slice passes, a later versioned source may remove
only the first three literal blocker IDs. Production and asset blockers remain
until separately reviewed source decisions and implementations exist.

## 6. Required C4B engineering slice

A focused, unwired engineering slice may add:

1. an immutable 15-record building progression-reference registry;
2. 15 exact cost-profile records and one exact duration-profile record;
3. the two neutral prerequisite/realm-eligibility profiles;
4. exact legacy alias resolution with no normalization;
5. schema fields for `initial_level`, `prerequisite_profile_id`, and
   `realm_eligibility_profile_id`;
6. allowed-value sets and exact record constraints tying every building ID to
   its alias, levels, content reference, cost, duration, prerequisite, and
   eligibility profiles;
7. exhaustive tests for all 150 target-level cost/duration vectors, checked
   arithmetic, resource order, rounding, immutability, unknown/case variants,
   cross-building profile substitution, and drift from the merged live
   construction source.

C4B must not define production profiles or asset references, emit a family
artifact, change current construction behavior, wire a service, migrate a
save, add `ManaShrine`/`Mine`, or alter user-visible balance.

## 7. Pinned current evidence

Hashes are lower-case SHA-256 over exact committed bytes at the audited main.

| Source | Source revision | Raw SHA-256 | Role |
| --- | --- | --- | --- |
| `Game_Data_Catalog_Authority_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `d8a0e2fdcd4e98bbb6379a8f2b7d7c733f869bd95f99112e1635f25dafb7b74d` | Common building record and publication contract |
| `Progression_Definition_Order_Transaction_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `3ef0ac9f792362f4375d45c1c125b1a717ca43e596036862015ff8ee2923cb4b` | Definition/profile, migration, transaction, and approval contract |
| `phase-c-six-family-technical-source.json` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` | Frozen IDs, aliases, unavailable anchors, and blockers |
| `phase-c-six-family-technical-source-v002.json` | `d219472073bee9fcd420d0cac1d94412019b865b` | `60498d1a071ea79eb37c1b8889a1faaa5c7aee69679c1043256535ef4d3c1685` | Current inherited building source and blocker state |
| `phase-c-six-family-content-map.json` | `963c4bc6e6db8ae2b87d363ceb229519e97f13b0` | `8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353` | Exact building name references and unavailable anchors |
| `BuildingDefinition.cs` | `efd64249c96761d2c0f1e0097c4402d46231c09a` | `e69f832145f5382b77af95fbe646c11403d881d43fbd176de585ff16c05c1b63` | Current maximum, cost, duration, and optional icon shape |
| `LocalGameDataService.cs` | `efd64249c96761d2c0f1e0097c4402d46231c09a` | `7be267f64de24718090170af779ce57b5ffd88eb50a55e9d4e5ff011443276f9` | Exact 15-building migration matrix |
| `LocalBuildingService.cs` | `efd64249c96761d2c0f1e0097c4402d46231c09a` | `0989b6523476fdd79296623b9968c47038a58a8dc6874c56b881a3568a8ffc81` | Effective Level 0, quote, maximum, and fail-closed behavior |
| `BuildingConstructionAuthorityTests.cs` | `efd64249c96761d2c0f1e0097c4402d46231c09a` | `a3a0eebcce72c7fd4c7221f265d86200e68dcd7c3beda6936d094787ef431a24` | Merged construction regression evidence |
| `EconomyIntegrityContracts.cs` | `ca435003168d1013a863850ea09f42b2d4dd6c5b` | `2c31e987f4ddf7b1516b7676a1077bca9a9f6c3bbae32f28c3493e2ce6323342` | Production provider seam without authored profile records |
| `KingdomBuildingModelCatalog.asset` | `8104d0cb58c6cf38b64dadd1b6ec452e007dd091` | `917dfa8febd26f34b7d1d87b0f1aff821121b9aa3b52c585e8114bcb8170fd55` | Incomplete realm-specific Town Hall/Workshop presentation evidence |

Any drift in a pinned identity, alias, level, value, resource order, rounding,
profile relation, source byte, or unavailable anchor blocks C4B until a
reviewed superseding decision reconciles it.

## 8. Validation and acceptance

- [x] all 15 current building identities, aliases, and content references are
  exact and deterministically ordered;
- [x] `ManaShrine` and `Mine` remain unavailable, not invented;
- [x] merged Level 0–10 behavior is distinguished from old prototype tuning;
- [x] cost and duration profile IDs, values, order, and rounding are exact;
- [x] prerequisite and realm eligibility preserve current behavior only;
- [x] current values remain migration evidence rather than new balance
  approval;
- [x] production authority is absent and remains open;
- [x] realm-specific Town Hall/Workshop models are not promoted into a common
  all-building asset source;
- [x] v001 and v002 remain unchanged;
- [x] no registry, schema, artifact, runtime, save, asset, scene, workflow,
  package, dependency, or production output changed.

## Impact

This phase adds one coordination document only. It adds no runtime code,
managed assembly bytes, Player content, asset duplication, allocation, frame
loop, network call, package, install byte, or device requirement. Unity,
Android, Player, PlayMode, device, profiler, package-size, and visual evidence
are not applicable to this documentation-only decision.
