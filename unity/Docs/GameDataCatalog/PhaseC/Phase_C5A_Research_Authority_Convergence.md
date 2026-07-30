# Phase C5A Research Authority Convergence

## Document control

| Field | Value |
| --- | --- |
| Tracked issues | [#183](https://github.com/yulee94/AnotherLife/issues/183), [#165](https://github.com/yulee94/AnotherLife/issues/165) |
| Phase | `Phase C5A — research-family authority convergence` |
| Primary mode | Codex coordination/review |
| Audited current main | `779e7363fca9ffed9e412f43cc74b20665fa4e9c` |
| Frozen v001 candidate | `game-data-phase-c-six-family-technical-source-2026-07-23-v001` |
| Current v003 candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v003` |
| Current v003 raw SHA-256 | `984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439` |
| Binding specifications | `Game_Data_Catalog_Authority_Spec.md`, `Progression_Definition_Order_Transaction_Spec.md` |
| Runtime authority | Unchanged |
| Shared-file lock | None |
| Research family disposition | `blocked_required` |
| Production eligibility | `false` |
| User balance, activation, playtest, and release approval | Pending |

This decision reconciles the eight frozen research identities with current
Unity and Android behavior plus the pure progression contracts merged in PR
[#376](https://github.com/yulee94/AnotherLife/pull/376). It accepts only
exact observed technical behavior as migration evidence. It does not invent a
maximum level, promote unimplemented Android descriptions into gameplay
effects, or treat planner test fixtures as authored definitions.

This is a versioned status overlay. It does not edit v001, v002, or v003,
create a research registry or artifact, change a schema or runtime service,
publish a catalog, or make #165 or #183 complete.

## 1. Scope and non-goals

C5A decides:

- the exact eight research identities, aliases, order, and content
  references;
- the current effective initial-level behavior;
- the exact current cost and duration formulas that later engineering may
  migrate without value drift;
- the exact absence of current prerequisite gating;
- the two research effects that current Unity code actually evaluates;
- which current Android rows, descriptions, command routes, and narrative
  hooks are evidence rather than technical definition authority;
- the status of every frozen research blocker.

C5A does not:

- select or invent a research maximum level;
- add effects for `masonry`, `irrigation`, `ballistics`, `logistics`,
  `trade_routes`, or `arcane_study`;
- declare “no effect” to be an approved design for those six identities;
- convert Android descriptions into technical modifiers;
- turn the `steel_forging` Level-2 or `arcane_study` Level-3 dialogue hooks
  into research effects or prerequisites;
- use fake PR #376 fixture IDs, limits, costs, durations, prerequisites, or
  effects as production source;
- change costs, durations, bonuses, identities, availability, balance,
  presentation, or saved state;
- edit `LocalGameDataService`, `LocalResearchService`, `IResearchService`,
  `KingdomSceneController`, a save, schema, asset, scene, workflow, package,
  or dependency.

## 2. Source precedence

Later research-source work must consume these sources in order:

1. The Phase C1 content map owns the exact eight player-facing name
   references.
2. The frozen v001 candidate owns canonical IDs, exact display-string
   aliases, authored order, Android identity observations, and blocker IDs.
3. `LocalGameDataService` owns the observed eight Unity identity strings and
   its private Level-0 defaults as migration evidence only.
4. `LocalResearchService` owns the currently executed cost, duration, and two
   stat-bonus formulas as migration evidence only.
5. The two binding specifications own immutable definition/profile shape,
   validation, provenance, migration, publication, and user-approval
   requirements.
6. PR #376 owns pure contract, compatibility, transaction, replay, and effect
   planner behavior. It owns no real research definition or balance value.

Android `KingdomModels` provides four prototype IDs and player-facing
descriptions. `BuildingHooks` provides narrative returns for
`steel_forging` and `arcane_study`. `KingdomCommandPolicy` exposes only two
disabled command IDs. Those surfaces corroborate identity/consumer drift but
do not define maximum levels, formulas, prerequisites, or effect profiles.

## 3. Exact research identities

The supported definition order remains:

| Order | Canonical ID | Exact legacy alias | Name reference | Android technical evidence |
| ---: | --- | --- | --- | --- |
| 1 | `steel_forging` | `Steel Forging` | `research.steel_forging.name` | exact prototype ID |
| 2 | `plate_armor` | `Plate Armor` | `research.plate_armor.name` | exact prototype ID |
| 3 | `masonry` | `Advanced Masonry` | `research.advanced_masonry.name` | exact prototype ID; `advanced_masonry` is not an alias |
| 4 | `irrigation` | `Irrigation` | `research.irrigation.name` | exact prototype ID |
| 5 | `ballistics` | `Ballistics` | `research.ballistics.name` | none |
| 6 | `logistics` | `Logistics` | `research.logistics.name` | none |
| 7 | `trade_routes` | `Trade Routes` | `research.trade_routes.name` | none |
| 8 | `arcane_study` | `Arcane Study` | `research.arcane_study.name` | narrative-hook ID only |

Aliases remain exact, case-sensitive, and versioned under migration issue
`#165`. Case variants, whitespace variants, normalized display text,
`advanced_masonry`, and Android description strings do not resolve.

## 4. Exact observed progression behavior

### 4.1 Initial level and missing maximum

Both the private fallback defaults and query-created saved rows use Level 0.
That value is accepted as migration evidence:

```text
initial_level = 0
```

No current maximum level exists. `CompleteResearch` increments without a
definition or bound. A later source must therefore obtain an explicit
positive bounded maximum for every supported research identity. A validator
or registry must not derive one from integer limits, UI capacity, planner
fixtures, building Level 10, or an arbitrary tuning convention.

### 4.2 Cost formula

For a requested target level:

```text
target_level = current_level + 1
cost = target_level × 200 Gold
```

The resource order is exactly one `gold` amount. Later engineering must use
checked integer arithmetic, reject unsupported target levels before
calculation, and cap the profile to the separately approved maximum level.
The formula is migration evidence, not newly approved balance.

### 4.3 Duration formula

For the same requested target level:

```text
duration_seconds = target_level × 15
```

Later engineering must use checked integer arithmetic and the same bounded
target-level domain as the definition. The timer makes an order eligible for
completion; it does not itself mutate state. The formula is migration
evidence, not newly approved pacing.

### 4.4 Prerequisite behavior

Current `StartResearch` performs no building, realm, quest, chapter, research,
or capacity prerequisite check. The source-resolved migration behavior is
therefore one explicit neutral prerequisite profile:

```text
research_prerequisite_none
```

The profile has no required building or research level and no other gate. It
preserves current behavior only. Future authored unlock meaning requires
separate narrative/content, balance, and user review.

## 5. Effect authority

Current Unity evaluates exactly two effect relations. The battle simulator
adds each returned fraction to `1.0` before multiplying the corresponding
army power:

| Research ID | Technical target | Exact observed formula | Status |
| --- | --- | --- | --- |
| `steel_forging` | `StatType.Attack` | `level × 0.05` additive fraction | source-resolved migration evidence |
| `plate_armor` | `StatType.Defense` | `level × 0.05` additive fraction | source-resolved migration evidence |

Later technical profiles must use fixed-point values rather than float source
as authority:

```text
per_level_fraction_millionths = 50000
```

The current method mutates save state while reading and uses display-string
lookups. Those defects are not accepted; only the exact relation and value
are migration evidence.

No implemented technical effect exists for the other six identities:

| Research ID | Current evidence | Disposition |
| --- | --- | --- |
| `masonry` | Android text says it reduces building upgrade times | presentation/prototype claim; not implemented effect authority |
| `irrigation` | Android text says it increases Food production | presentation/prototype claim; not implemented effect authority |
| `ballistics` | identity only | effect missing |
| `logistics` | identity only | effect missing |
| `trade_routes` | identity only | effect missing |
| `arcane_study` | Level-3 dialogue hook | narrative return only; effect missing |

Absence of an implementation is not approval for an empty effect list. The
binding specification requires required missing effects to block affected
consumers rather than silently become zero.

## 6. Blocker disposition

The literal v003 research row remains unchanged and `blocked_required`.
Current effective dispositions are:

| Blocking ID | C5A disposition |
| --- | --- |
| `research.max_levels` | **Open.** No current maximum exists. An explicit bounded value for each identity requires separate balance/user approval. |
| `research.cost_profiles` | **Source resolved; bounded implementation blocked.** Preserve `target level × 200 Gold`; the accepted maximum-level domain is still missing. |
| `research.duration_profiles` | **Source resolved; bounded implementation blocked.** Preserve `target level × 15 seconds`; the accepted maximum-level domain is still missing. |
| `research.effects` | **Partially source resolved; remains open.** Exact 5% Attack/Defense relations exist for two identities; six required effect decisions are missing. |
| `research.prerequisites` | **Source resolved; implementation pending.** `research_prerequisite_none` preserves current no-gating behavior only. |

No research blocker is removed from a technical-source candidate in C5A.
The family cannot produce a complete immutable definition set while maximum
levels and six effect decisions are absent.

## 7. Engineering gate

A complete research registry/schema slice is not yet authorized. Before it
can begin, reviewed source must provide:

1. one explicit positive bounded maximum level for each of the eight
   identities;
2. one ordered nonempty technical effect-profile list for each identity,
   including an explicitly approved neutral effect only where no effect is
   intended;
3. confirmation that the observed cost, duration, and two 5% relations are
   accepted unchanged as migration values;
4. user balance approval for maximums and final effect values.

After those inputs exist, a focused engineering slice may add immutable
research definitions, cost/duration/prerequisite/effect profiles, exact alias
resolution, acyclic prerequisite validation, fixed-point effect values, and
exhaustive drift tests. It must consume PR #376 planners rather than duplicate
them.

That slice must not wire a production service, migrate a save, edit
`LocalResearchService`, publish a family artifact, activate a consumer,
change presentation text, or infer missing source.

## 8. Pinned current evidence

Hashes are lower-case SHA-256 over exact committed bytes at the audited main.

| Source | Source revision | Raw SHA-256 | Role |
| --- | --- | --- | --- |
| `Game_Data_Catalog_Authority_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `d8a0e2fdcd4e98bbb6379a8f2b7d7c733f869bd95f99112e1635f25dafb7b74d` | Research record and publication contract |
| `Progression_Definition_Order_Transaction_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `3ef0ac9f792362f4375d45c1c125b1a717ca43e596036862015ff8ee2923cb4b` | Definition/profile, transaction, effect, migration, and approval contract |
| `Game_Data_Source_Inventory.md` | `320fda546d4f12dd1e25452ce9788fa4ef720853` | `3e7e1ad01471d5e1b9aed2e07d613de3435a91b8520bb1185cc52aefe8f03622` | Current eight-row identity and consumer inventory |
| `phase-c-six-family-technical-source.json` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` | Frozen identities, aliases, observations, and blockers |
| `phase-c-six-family-technical-source-v003.json` | `779e7363fca9ffed9e412f43cc74b20665fa4e9c` | `984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439` | Current inherited research source and blocker state |
| `phase-c-six-family-content-map.json` | `963c4bc6e6db8ae2b87d363ceb229519e97f13b0` | `8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353` | Exact research name references |
| `IResearchService.cs` | `a9bffb60a463fad7759ce02e45dff4ac7f8425c7` | `db115747a0f3a349faa9066a2e1cf5bc276ae83236c36ad3096468d62ed80f94` | Mutable legacy state/query surface |
| `LocalGameDataService.cs` | `efd64249c96761d2c0f1e0097c4402d46231c09a` | `7be267f64de24718090170af779ce57b5ffd88eb50a55e9d4e5ff011443276f9` | Eight private display-string defaults |
| `LocalResearchService.cs` | `a9bffb60a463fad7759ce02e45dff4ac7f8425c7` | `34c6b5165c92cb043a5485efd1a750f530d1149e64be4c601c7091e242ea93e1` | Exact observed cost, duration, prerequisite, and two-effect behavior |
| `DeterministicBattleSimulator.cs` | `bee6a13bdfb653ab875b3f8f847fe659978dd55b` | `20860acf2fd74d37fe83e8b238049d98037b606886102d3fe6a81c1b69e4e9e3` | Current additive-fraction battle consumption |
| `ProgressionContracts.cs` | `1c914b2e2090c07b440600553b4db42b52d9ff5a` | `a1ba004d0592b2e4194535a347d594e6ab876d3b31543bae251b5d6d09659302` | Pure immutable research contract shape |
| `ProgressionCompatibilityPlanner.cs` | `1c914b2e2090c07b440600553b4db42b52d9ff5a` | `35239823e7352a2d472ced26e43409f8245625535aaf1ddc7a1c7d89a949d828` | Pure fail-closed compatibility validation |
| `ProgressionContractPlannerTests.cs` | `1c914b2e2090c07b440600553b4db42b52d9ff5a` | `75e7cd7ac423bf1bcabeb0e09203b04345be9f9be7316e619eb43c23428fcbe2` | Fake structural fixtures, explicitly not production source |
| `KingdomModels.kt` | `e1497ceb0ab666f28477ae814a17da06560d54c7` | `ed9bf740920c9bd84822c9fc37b1ee00cbcf2534ac34d9c4edc0952812641844` | Four Android prototype identities/descriptions |
| `BuildingHooks.kt` | `28d28384d820896d9ad87432866e3eb4a2ddc9fb` | `686a7a3c3f70a84736a7721fdbd3ccc85b21a94a3ac56bef550b754c6e5ca8da` | `arcane_study` narrative-hook evidence |
| `KingdomCommandPolicy.cs` | `efd64249c96761d2c0f1e0097c4402d46231c09a` | `48a9484687541ef6aaae07c243a469caf8fc977267a9dded692e935d88efbed1` | Two currently named research command routes |
| `KingdomSceneController.cs` | `320fda546d4f12dd1e25452ce9788fa4ef720853` | `8f0210e7e6393811a32158e259baa467d74aeda538a74b790ff423480e277cfa` | Save-backed presentation consumer evidence |

Any drift in a pinned identity, alias, formula, effect relation, source byte,
or missing-authority conclusion blocks later research work until a reviewed
superseding decision reconciles it.

## 9. Validation and acceptance

- [x] all eight current research identities, aliases, content references, and
  deterministic order are exact;
- [x] `masonry` remains canonical while `Advanced Masonry` remains only its
  exact legacy/display alias;
- [x] current Level 0, cost, duration, and no-prerequisite behavior is
  recorded as migration evidence rather than newly approved balance;
- [x] the two implemented 5% effects are distinguished from six missing
  effect decisions;
- [x] Android descriptions, narrative hooks, commands, and fake planner
  fixtures are not promoted into definition authority;
- [x] no maximum level or missing effect is inferred;
- [x] all five literal v003 research blockers remain unchanged;
- [x] v001, v002, and v003 remain unchanged;
- [x] production eligibility, runtime authority, and user approval state
  remain unchanged;
- [x] no registry, schema, artifact, runtime, save, asset, scene, workflow,
  package, dependency, or production output changed.

## Impact

This phase adds one coordination document only. It adds no runtime code,
managed assembly bytes, Player content, asset duplication, allocation,
frame-loop work, network call, package, install byte, or device requirement.
Unity, Android, Player, PlayMode, device, profiler, package-size, and visual
evidence are not applicable to this documentation-only decision.
