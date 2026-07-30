# Phase C6A Troop Authority Convergence

## Document control

| Field | Value |
| --- | --- |
| Tracked issues | [#183](https://github.com/yulee94/AnotherLife/issues/183), [#165](https://github.com/yulee94/AnotherLife/issues/165), [#174](https://github.com/yulee94/AnotherLife/issues/174) |
| Phase | `Phase C6A — troop-family authority convergence` |
| Primary mode | Codex coordination/review |
| Audited current main | `b6a4cf882049936b7ad46abf702ce6fce5408afd` |
| Frozen v001 candidate | `game-data-phase-c-six-family-technical-source-2026-07-23-v001` |
| Current v003 candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v003` |
| Current v003 raw SHA-256 | `984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439` |
| Binding specifications | `Game_Data_Catalog_Authority_Spec.md`, `Progression_Definition_Order_Transaction_Spec.md`, `Battle_Computation_Result_Transaction_Spec.md` |
| Runtime authority | Unchanged |
| Shared-file lock | None |
| Troop family disposition | `blocked_required` |
| Production eligibility | `false` |
| User product, balance, activation, playtest, and release approval | Pending |

This decision reconciles the four legacy `TroopType` anchors with current
Unity training, save, battle, and presentation behavior plus the pure
progression contracts merged in PR
[#376](https://github.com/yulee94/AnotherLife/pull/376). It accepts exact
observed behavior only as migration evidence. It does not create stable troop
record IDs, turn enum or UI text into authored names, convert simulator power
into base attack or defense, or adopt fake planner limits as balance.

This is a versioned status overlay. It does not edit v001, v002, or v003,
create a troop source candidate, registry, or artifact, change a schema or
runtime service, publish a catalog, or make #165, #174, or #183 complete.

## 1. Scope and non-goals

C6A decides:

- the exact four legacy enum anchors, their order, and serialized values;
- the absence of stable production troop IDs and content references;
- the exact current training cost and immediate-completion behavior that
  later engineering may migrate without accidental value drift;
- the current save shape and its missing definition/provenance fields;
- the difference between simulator battle behavior and required troop base
  stats;
- which Android rows, Unity demo routes, UI labels, and pure-planner
  fixtures are evidence rather than production definition authority;
- the status of every frozen troop blocker.

C6A does not:

- select or infer canonical troop IDs from enum names;
- authorize `Infantry`, `Cavalry`, `Ranged`, or `Siege` as player-facing
  names or localization values;
- create `baseAttack`, `baseDefense`, or any other troop stat;
- approve the simulator's aggregate power, counter, vulnerability, realm,
  or terrain values as production troop definitions;
- select maximum inventory, maximum batch, capacity, queue, recovery,
  prerequisite, realm-eligibility, cancellation, or speedup policy;
- use fake PR #376 fixture IDs, caps, costs, durations, prerequisites,
  battle profiles, or inventory policies as production source;
- change costs, timing, counts, identities, availability, balance,
  presentation, battle behavior, or saved state;
- edit `LocalGameDataService`, `LocalTrainingService`, `ITrainingService`,
  `SaveGameData`, `DeterministicBattleSimulator`, a source JSON, schema,
  asset, scene, workflow, package, or dependency.

## 2. Source precedence

Later troop-source work must consume these sources in order:

1. The Phase C1 content packet and map own the explicit conclusion that no
   troop content record is authored.
2. The frozen v001 candidate owns the four unavailable technical anchors and
   five blocker IDs.
3. `TroopType`, `LocalTrainingService`, and `SaveGameData` own exact observed
   migration behavior only.
4. `DeterministicBattleSimulator` owns exact observed legacy battle behavior
   only; its aggregate formulas do not supply required base attack or
   defense.
5. The three binding specifications own immutable record/profile shape,
   validation, provenance, migration, transaction, battle-snapshot,
   publication, and user-approval requirements.
6. PR #376 owns pure contract, compatibility, transaction, replay, and
   capacity-policy behavior. It owns no real troop definition or balance
   value.

Android `KingdomModels` provides three prototype type strings and seeded
counts. Unity demo controls expose one Infantry training call. The kingdom
readiness UI deliberately renders troop force as unavailable because the
legacy getter creates save state. Those surfaces corroborate consumer drift
but do not define production records, names, profiles, or assets.

## 3. Exact legacy anchors and missing identities

The exact current enum order and implicit serialized values are:

| Order | Legacy anchor | Serialized enum value | Stable production ID | Content reference |
| ---: | --- | ---: | --- | --- |
| 1 | `TroopType.Infantry` | `0` | none | none |
| 2 | `TroopType.Cavalry` | `1` | none | none |
| 3 | `TroopType.Ranged` | `2` | none | none |
| 4 | `TroopType.Siege` | `3` | none | none |

These anchors are migration aliases only. Lower-casing an enum name,
prefixing it, replacing punctuation, or copying a UI string would invent a
canonical ID. No exact alias map can be completed until source authors one
stable ID for each supported record.

The repository contains the `TroopDefinition` type with fields for `Id`,
`Type`, `DisplayName`, `Icon`, `BaseAttack`, and `BaseDefense`, but contains
zero committed `TroopDefinition` records or troop assets.
`IGameDataService.GetTroop(string)` always returns `null`, and no production
caller resolves a troop definition.

Android seeds `Infantry`, `Cavalry`, and `Ranged` prototype rows with counts
100, 50, and 75. It has no `Siege` row. Those strings and counts are
preview/demo values, not localization, supported-record, inventory, or
balance authority.

## 4. Exact observed training and save behavior

### 4.1 Cost and completion

For a caller-provided integer `count`, the current service evaluates:

```text
cost = count × 10 Food
```

The expression is evaluated without an explicit checked guard before its
result is assigned to `long`. If the resource service reports success, the
same call immediately adds `count` to the saved row. There is no active
training order or elapsed duration:

```text
duration_seconds = 0
completion = immediate in StartTraining
```

`CompleteTraining` is empty. The exact 10-Food and zero-duration relations
are migration evidence only. They are not sufficient to publish a bounded
training profile.

### 4.2 Missing validation and policy

Current `StartTraining` does not prove:

- that `count` is positive or within an approved batch maximum;
- that the `TroopType` value is defined;
- that a stable troop definition exists;
- that the current inventory is valid or below an approved cap;
- that a building, research, realm, quest, chapter, or capacity prerequisite
  is met;
- that a queue slot, recovery policy, realm-eligibility rule, or battle
  profile exists.

Current observed behavior has no prerequisite gate and no configured
capacity limit, but absence of those checks is not approval for unlimited
inventory or unrestricted training.

### 4.3 State mutation and persistence

`GetTroopCount` calls `GetTroopState`, which creates and appends a missing
save row. A read therefore mutates the profile. The kingdom readiness UI
explicitly avoids that getter and shows `FORCE / N/A`.

The persisted row contains only:

```text
TroopInventoryData.Type
TroopInventoryData.Count
TroopInventoryData.WoundedCount
```

It has no stable definition ID, content version, source revision/hash,
reserved/deployed count, active order, capacity-policy version, operation
receipt, or state revision. Any serialized integer may enter `Type` without
a catalog-record check.

The service spends before the entire consequence/save boundary is proven,
swallows quest-service failure, exposes a void result, and does not establish
rollback or commit-uncertainty behavior. C6A records those consumer risks;
it does not accept or repair them.

## 5. Battle behavior is not base-stat authority

The current simulator maps enum values to aggregate base power:

| Legacy anchor | Aggregate base power |
| --- | ---: |
| `TroopType.Infantry` | `10` |
| `TroopType.Cavalry` | `15` |
| `TroopType.Ranged` | `12` |
| `TroopType.Siege` | `20` |

It also gives an undefined enum value aggregate power `1`, which the binding
battle specification explicitly prohibits for future authority.

Other exact enum-coupled behavior includes:

- Infantry against Cavalry, Cavalry against Ranged, and Ranged against
  Infantry each add `0.18` to the source counter multiplier;
- Siege adds `0.12` in Boss or Warzone battle;
- vulnerability multipliers are Cavalry `0.92`, Ranged `1.08`, Siege `1.18`,
  and default/Infantry `1.0`;
- realm and terrain calculations inspect the presence of Siege, Ranged, or
  Cavalry stacks in several branches.

These values may be pinned as legacy battle-migration evidence. They cannot
be split, copied, or derived into `TroopDefinition.BaseAttack`,
`TroopDefinition.BaseDefense`, a complete battle profile, or approved
balance. The current battle path and the required catalog schema model
different shapes.

## 6. Pure planner boundary

PR #376 supplies immutable `TroopProgressionDefinition` shape, bounded
compatibility, capacity policy, start/completion/replay planning, and source
identity. It remains unregistered and contains no production troop records.

Its tests use deliberately fake fixtures. The common fixture happens to use
10 Food and zero duration, matching the current legacy service, but also
uses arbitrary values such as maximum inventory 1,000 and maximum batch 100.
The match does not promote any fixture field into source. In particular,
neither cap is approved or observable in production behavior.

Future implementation must consume the accepted planner contracts rather
than creating a second training transaction path, but it cannot instantiate
real definitions until source and balance gates are satisfied.

## 7. Blocker disposition

The literal v003 troop row remains unchanged and `blocked_required`. Current
effective dispositions are:

| Blocking ID | C6A disposition |
| --- | --- |
| `troops.records` | **Open.** Four legacy enum anchors exist, but no stable production IDs, records, exact alias map, or supported-record decision exists. |
| `troops.localization` | **Open.** No authored content reference exists. Enum names, Unity labels, and Android strings are evidence only. |
| `troops.base_stats` | **Open.** No `BaseAttack` or `BaseDefense` values exist. Simulator aggregate power and matchup formulas are not equivalent fields. |
| `troops.training_profiles` | **Partially source resolved; remains open.** Preserve 10 Food per requested unit and zero-duration immediate legacy behavior as migration evidence. Approved batch/inventory bounds, capacity policy, prerequisites, eligibility, queue, recovery, and final balance are missing. |
| `troops.asset_refs` | **Open.** No committed icon, portrait, model, prefab, address, or `TroopDefinition` asset exists. |

No troop blocker is removed from a technical-source candidate in C6A. The
family cannot produce one complete immutable definition while all record,
content, base-stat, bounded-profile, and asset decisions are absent.

## 8. Source and engineering gates

A complete troop source or registry slice is not yet authorized. Before
source authoring can complete, reviewed product/content/balance input must
provide:

1. one stable canonical ID and exact legacy-enum alias for every supported
   troop record;
2. one localization/name reference for every record;
3. explicit base attack and base defense values, or a reviewed schema change
   that replaces those fields without relabeling aggregate simulator power;
4. an accepted maximum inventory, maximum batch, capacity policy,
   prerequisite/eligibility policy, and confirmation or replacement of the
   observed 10-Food and zero-duration values;
5. one valid asset reference policy and exact reference for every record;
6. user product and balance approval for supported identities and numeric
   values.

After those inputs exist, separate focused work may:

1. author the troop source packet without runtime changes;
2. produce a non-production immutable technical candidate with exact
   aliases, records, stats, training profiles, and asset references;
3. validate deterministic order, provenance, references, bounds, and drift;
4. only later integrate the accepted source through PR #376 planners and the
   battle contracts.

Those slices must not infer missing source, use enum-name fallback, mutate a
save during query, wire a production service before the complete family
passes, or treat a green planner fixture as catalog authority.

## 9. Pinned current evidence

Hashes are lower-case SHA-256 over exact committed bytes at the audited main.

| Source | Source revision | Raw SHA-256 | Role |
| --- | --- | --- | --- |
| `Game_Data_Catalog_Authority_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `d8a0e2fdcd4e98bbb6379a8f2b7d7c733f869bd95f99112e1635f25dafb7b74d` | Troop record and publication contract |
| `Progression_Definition_Order_Transaction_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `3ef0ac9f792362f4375d45c1c125b1a717ca43e596036862015ff8ee2923cb4b` | Definition/profile, transaction, capacity, migration, and approval contract |
| `Battle_Computation_Result_Transaction_Spec.md` | `e5910818925bd26dfa8577aa9b5efbc92a333cf9` | `3ee3048b272527534194a0c73a22a76c094e0166a7186c9ea2e676bf3c847cec` | Immutable battle-profile and legacy-alias contract |
| `Game_Data_Source_Inventory.md` | `320fda546d4f12dd1e25452ce9788fa4ef720853` | `3e7e1ad01471d5e1b9aed2e07d613de3435a91b8520bb1185cc52aefe8f03622` | Current identity, record-absence, consumer, and save inventory |
| `Phase_C_Six_Family_Source_Packet.md` | `963c4bc6e6db8ae2b87d363ceb229519e97f13b0` | `aa63db30d2342e95e81d3bd54225bd3fa774ce0eab88136fe1eaf042c6d4a1a2` | Explicit no-authored-content decision |
| `Phase_C_Six_Family_Technical_Handoff.md` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `719ed1c09c39074bf7041edde87131b27e889c3e9b55fa11676fba32b871caf0` | Required fields, absence policy, and generation refusal |
| `phase-c-six-family-technical-source.json` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` | Frozen unavailable anchors and blocker IDs |
| `phase-c-six-family-technical-source-v003.json` | `779e7363fca9ffed9e412f43cc74b20665fa4e9c` | `984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439` | Current inherited troop source and blocker state |
| `phase-c-six-family-content-map.json` | `963c4bc6e6db8ae2b87d363ceb229519e97f13b0` | `8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353` | Four unavailable anchors with empty content |
| `Enums.cs` | `15b8712c83e5ddac218a86d2cadcd8ed517a1434` | `36e3c430d97c39ca6f487b1a682353f157d808052de28919d766b2bba6190d4a` | Exact enum order and implicit values |
| `TroopDefinition.cs` | `a9bffb60a463fad7759ce02e45dff4ac7f8425c7` | `de7c7e1bf66a38025d2e13eb1ee8c97fddadc216d21c938b93c33839d96c24c3` | Uninstantiated mutable legacy definition shape |
| `IGameDataService.cs` | `a9bffb60a463fad7759ce02e45dff4ac7f8425c7` | `370e9bd48d3030db161dfc5aa28a88ec7f53a1b75e00802a786155a49eb5cb42` | Nullable string lookup surface |
| `LocalGameDataService.cs` | `efd64249c96761d2c0f1e0097c4402d46231c09a` | `7be267f64de24718090170af779ce57b5ffd88eb50a55e9d4e5ff011443276f9` | Always-null troop lookup |
| `LocalTrainingService.cs` | `a9bffb60a463fad7759ce02e45dff4ac7f8425c7` | `b1e2bc2d40040aac62df964c21b2b83584ce3f37f7fd5c3854f6f23d626ec3dc` | Exact observed training, read-seeding, quest, and save behavior |
| `SaveGameData.cs` | `320fda546d4f12dd1e25452ce9788fa4ef720853` | `bacfac499e8f2ac359a104054f5aef5f795f58f184c9febeb666ed6a69a15fbf` | Current three-field troop save row |
| `DeterministicBattleSimulator.cs` | `bee6a13bdfb653ab875b3f8f847fe659978dd55b` | `20860acf2fd74d37fe83e8b238049d98037b606886102d3fe6a81c1b69e4e9e3` | Aggregate power, counters, vulnerability, and enum fallback |
| `ProgressionContracts.cs` | `1c914b2e2090c07b440600553b4db42b52d9ff5a` | `a1ba004d0592b2e4194535a347d594e6ab876d3b31543bae251b5d6d09659302` | Pure immutable troop progression contract shape |
| `ProgressionContractPlannerTests.cs` | `1c914b2e2090c07b440600553b4db42b52d9ff5a` | `75e7cd7ac423bf1bcabeb0e09203b04345be9f9be7316e619eb43c23428fcbe2` | Fake structural fixtures, explicitly not production source |
| `KingdomModels.kt` | `e1497ceb0ab666f28477ae814a17da06560d54c7` | `ed9bf740920c9bd84822c9fc37b1ee00cbcf2534ac34d9c4edc0952812641844` | Three Android prototype rows and seeded counts |
| `KingdomSceneController.cs` | `320fda546d4f12dd1e25452ce9788fa4ef720853` | `8f0210e7e6393811a32158e259baa467d74aeda538a74b790ff423480e277cfa` | Deliberate read-seeding containment in readiness UI |

Any drift in a pinned anchor, formula, save field, battle relation, source
byte, or missing-authority conclusion blocks later troop work until a
reviewed superseding decision reconciles it.

## 10. Validation and acceptance

- [x] all four current enum anchors, their order, and implicit serialized
  values are exact;
- [x] zero stable production IDs, content references, committed
  `TroopDefinition` records, and troop assets are confirmed;
- [x] current 10-Food and immediate-completion behavior is recorded as
  migration evidence rather than newly approved balance;
- [x] the mutating read, three-field save row, missing validation, void
  result, and consequence/save risks are explicit;
- [x] simulator aggregate power and matchup behavior are distinguished from
  missing base attack and defense authority;
- [x] Android rows, demo controls, UI labels, and fake planner fixtures are
  not promoted into definition authority;
- [x] no ID, localization, base stat, capacity limit, batch limit,
  prerequisite, asset, or missing source is inferred;
- [x] all five literal v003 troop blockers remain unchanged;
- [x] v001, v002, and v003 remain unchanged;
- [x] production eligibility, runtime authority, and user approval state
  remain unchanged;
- [x] no registry, schema, artifact, runtime, save, battle, asset, scene,
  workflow, package, dependency, or production output changed.

## Impact

This phase adds one coordination document only. It adds no runtime code,
managed assembly bytes, Player content, asset duplication, allocation,
frame-loop work, network call, package, install byte, or device requirement.
Unity, Android, Player, PlayMode, device, profiler, package-size, and visual
evidence are not applicable to this documentation-only decision.
