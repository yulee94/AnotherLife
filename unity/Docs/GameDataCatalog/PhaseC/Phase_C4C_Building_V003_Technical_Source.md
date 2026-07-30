# Phase C4C Building v003 Technical Source

## Document control

| Field | Value |
| --- | --- |
| Tracked issue | [#183](https://github.com/yulee94/AnotherLife/issues/183) |
| Phase | `Phase C4C — building v003 technical source` |
| Primary mode | Codex engineering |
| Starting main | `4bd458086c63da42f2a76d8219a2ee18cb1a5b50` |
| Building registry merge | `1d2d8fa838d31fecd93b2609c7ff67299b07cba1` |
| Frozen Phase C2 candidate | `game-data-phase-c-six-family-technical-source-2026-07-23-v001` |
| Frozen Phase C2 raw SHA-256 | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` |
| Frozen Phase C3H candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v002` |
| Frozen Phase C3H raw SHA-256 | `60498d1a071ea79eb37c1b8889a1faaa5c7aee69679c1043256535ef4d3c1685` |
| New candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v003` |
| New candidate raw SHA-256 | `984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439` |
| Binding specification | `unity/Docs/Game_Data_Catalog_Authority_Spec.md` |
| Runtime authority | Unchanged |
| Shared-file lock | None |
| Production eligibility | `false` |
| User creative, balance, activation, playtest, and release approval | Pending |

Phase C4C creates a strict versioned overlay on the frozen v002 candidate. It
records the accepted building progression registry as technical source,
removes only the three building blockers resolved by C4A and C4B, and retains
every unresolved global, production, asset, research, troop, champion, and
skill blocker.

The candidate is source for later non-production work. It is not a family
artifact, catalog-set manifest, runtime publication, balance approval, or
authority switch.

## 1. Versioned-overlay contract

The v003 candidate uses `schemaVersion: 3` and
`sourceKind: versioned_overlay`. Its `supersedes` object pins v002 by exact
candidate ID, repository path, source revision, and raw SHA-256.

The overlay is resolved in this order:

1. verify the frozen v001 and v002 bytes and identities;
2. verify every ordered provenance input against its pinned Git blob and
   current working bytes;
3. inherit the complete resolved realm row from v002;
4. inherit building identity mappings and unavailable anchors from v001;
5. add the complete C4B building progression bindings and profiles;
6. mark exactly three building blocker IDs as resolved;
7. retain the two unresolved building blocker IDs and the other four
   families' exact v001 blockers;
8. recompute the ordered top-level blocker array;
9. refuse production generation while the global gate is blocked.

An implementation must not perform a permissive object merge, infer a family
by position alone, accept an unpinned source, ignore an unknown field, invent
an unavailable building, or fall back to another candidate.

## 2. Exact building transition

The v003 building row carries 15 exact, authored-order progression bindings.
Every binding records:

- the canonical building ID, exact case-sensitive legacy alias, and name
  reference;
- initial Level 0 and maximum Level 10;
- one exact cost-profile ID;
- the shared duration, no-prerequisite, and all-realm eligibility profile
  IDs.

`ManaShrine` and `Mine` remain unavailable anchors. They are not aliases,
definitions, profiles, or generated content.

The ordered Level 1–10 base budgets remain:

```text
100, 175, 300, 475, 700, 1000, 1400, 1900, 2500, 3250
```

The candidate carries 15 cost profiles with their exact scale percentage,
resource order, explicit percentage shares, and positive final remainder.
The validator recomputes all 150 building/target-level cost vectors with the
accepted ceiling and floor rules, requires each resource amount to be
positive, and requires each ordered vector to sum exactly to its scaled
budget.

The common duration profile carries these exact seconds:

```text
10, 30, 120, 300, 900, 1800, 3600, 7200, 14400, 28800
```

The neutral prerequisite profile adds no gating. The eligibility profile
allows the four exact realm IDs in authored order:

```text
crownlands, stonehold, eldergrove, umbral
```

These values remain migration evidence only. They do not become newly
approved balance authority.

## 3. Blocker transition

The building row records exactly these resolved IDs:

```text
buildings.max_level_review
buildings.cost_profiles
buildings.duration_profiles
```

It retains exactly these required blockers:

```text
buildings.production_profiles
buildings.asset_refs
```

The building disposition therefore remains `blocked_required`. C4C does not
infer production rates from prose and does not collapse realm-specific Town
Hall or Workshop presentation assets into common all-building references.

The global blocker count changes from 29 in v002 to 26 in v003:

| Scope | Effective blockers |
| --- | ---: |
| User creative/balance approval | 1 |
| Buildings | 2 |
| Research | 5 |
| Troops | 5 |
| Champions | 6 |
| Skills | 7 |
| **Total** | **26** |

The realm family remains ready only for non-production shadow generation.
The other five required families retain their exact dispositions. The global
generation gate remains:

```text
status: blocked
requireProductionEligibleResult: refused_without_writes
outputPaths: []
```

## 4. Pinned provenance

The candidate pins six accepted inputs:

| Role | Pinned source |
| --- | --- |
| Building authority decision | `Phase_C4A_Building_Authority_Convergence.md` |
| Wallet resource registry | `GameDataWalletResourceReferences.cs` |
| Realm reference registry | `GameDataRealmReferences.cs` |
| Building progression registry | `GameDataBuildingProgressionRegistry.cs` |
| Building registry tests | `GameDataBuildingProgressionRegistryTests.cs` |
| Six-family schema | `GameDataSixFamilySchemas.cs` |

Every provenance row contains the exact repository path, source revision, and
raw SHA-256. The validator reads the pinned Git blob, verifies its raw hash,
and requires the current working file to equal those bytes. Source drift
therefore blocks v003 instead of silently changing its meaning.

## 5. Validation

Run:

```text
python3 tools/game-data/test_phase_c_six_family_building_v003_technical_source.py
python3 tools/game-data/test_phase_c_six_family_building_v003_technical_source.py
python3 tools/game-data/test_phase_c_six_family_building_v003_technical_source.py --run-negative-fixtures
python3 tools/game-data/test_phase_c_six_family_building_v003_technical_source.py --require-production-eligible
```

The first two runs must return the same raw candidate SHA-256. The negative
fixture run must prove that production eligibility, profile swaps, scale
drift, blocker removal, blocker reintroduction, realm inheritance drift, and
an invented unavailable building all fail closed. The production command
must validate the complete candidate and then return nonzero with a
deterministic refusal diagnostic. None of these validation paths writes a
production output.

The validator also proves:

- strict UTF-8 JSON with no BOM, duplicate fields, trailing data, comments,
  non-finite numbers, or non-canonical v003 formatting;
- exact v001 and v002 identity, source revision, and raw hash;
- exact ordered root, provenance, family, building, profile, and blocker
  fields;
- exact binding parity with the frozen v001 building identities;
- exact 15 cost profiles and 150 computed vectors;
- exact duration, prerequisite, and realm-eligibility profiles;
- exact inherited realm and unresolved-family state;
- exactly three resolved building blockers and 26 retained blockers;
- pending approvals, zero output paths, and unchanged runtime authority;
- absence of common production manifest and family artifact paths.

## 6. Boundaries and next phase

This phase changes documentation and validation tooling only. It does not
change C# code, Android code, Unity assemblies, assets, `.meta` files, scenes,
saves, packages, workflows, dependencies, runtime behavior, or Player bytes.

Building production profiles and common asset references require separately
reviewed source decisions. They must not be inferred merely to unblock the
family. A later source phase may instead converge another unresolved family
while retaining both building blockers. Any later artifact phase must still
prove deterministic bytes, hashes, envelopes, ordered diagnostics, and zero
consumer activation before publication is considered.

## 7. Acceptance

- [x] v001 and v002 remain byte-for-byte unchanged and hash-pinned.
- [x] v003 has a new candidate ID and explicit versioned-overlay schema.
- [x] all required C4A and C4B inputs are pinned.
- [x] all 15 building progression bindings and 150 cost vectors are exact.
- [x] neutral prerequisite and all-realm eligibility preserve current
  behavior only.
- [x] exactly three source-resolved building blockers are removed in v003.
- [x] production-profile and asset-reference blockers remain explicit.
- [x] exactly 26 global and non-production blockers remain.
- [x] production eligibility is false and generation refuses without writes.
- [x] runtime authority and user approval state remain unchanged.
- [x] no common artifact, manifest, loader, consumer, save, asset, dependency,
  workflow, or production output is added.
