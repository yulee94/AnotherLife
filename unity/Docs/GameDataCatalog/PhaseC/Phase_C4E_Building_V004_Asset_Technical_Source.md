# Phase C4E Building v004 Asset Technical Source

## Document control

| Field | Value |
| --- | --- |
| Tracked issue | [#183](https://github.com/yulee94/AnotherLife/issues/183) |
| Phase | `Phase C4E — building v004 asset technical source` |
| Primary mode | Codex engineering |
| Starting main | `a9a334fd92d24790efdbb2f3838342b0157a1c07` |
| Asset-authority commit | `b5c8472c71f0d9dd7b832e780e235ecf5e70e099` |
| Frozen Phase C2 candidate | `game-data-phase-c-six-family-technical-source-2026-07-23-v001` |
| Frozen Phase C2 raw SHA-256 | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` |
| Frozen Phase C3H candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v002` |
| Frozen Phase C3H raw SHA-256 | `60498d1a071ea79eb37c1b8889a1faaa5c7aee69679c1043256535ef4d3c1685` |
| Frozen Phase C4C candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v003` |
| Frozen Phase C4C raw SHA-256 | `984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439` |
| New candidate | `game-data-phase-c-six-family-technical-source-2026-07-30-v004` |
| New candidate raw SHA-256 | `4122a43cd11861e7b04d535bacb41129bfd7018d74041fc4c786217f5cb4fc31` |
| Runtime authority | Unchanged |
| Shared-file lock | None |
| Production eligibility | `false` |
| User creative, balance, activation, playtest, and release approval | Pending |

Phase C4E creates a strict v004 overlay on the frozen v003 candidate. It
records the reviewed 15-building icon atlas and exact cell relations as
technical source, resolves only `buildings.asset_refs`, and retains every
other unresolved blocker and approval gate.

The candidate is input for later non-production generation work. It is not a
runtime publication, asset loader, generated family artifact, balance
decision, presentation acceptance, or authority switch.

## 1. Versioned-overlay contract

The v004 candidate uses `schemaVersion: 4` and
`sourceKind: versioned_overlay`. Its `supersedes` object pins v003 by exact
candidate ID, repository path, source revision, and raw SHA-256.

The overlay is resolved in this order:

1. verify the frozen v001, v002, and v003 identities and raw bytes;
2. verify every v004 provenance input against its pinned Git blob and current
   working bytes;
3. inherit the complete realm, research, troop, champion, and skill rows from
   v003 without changing disposition or blockers;
4. inherit the building mappings, unavailable anchors, progression bindings,
   cost profiles, duration profile, prerequisite profile, and realm
   eligibility profile from v003;
5. add the exact atlas and 15 ordered logical asset bindings;
6. mark `buildings.asset_refs` resolved;
7. retain `buildings.production_profiles` as the sole building blocker;
8. recompute the exact ordered top-level blocker list;
9. refuse production generation without writing an output.

An implementation must not permissively merge unknown fields, infer an atlas
cell, normalize a fragment, pair an icon with a different building, change an
inherited family, ignore a source-byte mismatch, or fall back to another
candidate.

## 2. Exact asset transition

The v004 building row adds one exact atlas:

| Property | Value |
| --- | --- |
| Unity path | `Assets/AL/Art/Buildings/RuntimeExports/S_Building_Icon_Atlas_1536x1024_v001.png` |
| Unity GUID | `8cfa4b19fc1e4475873c4ea7560dc9ad` |
| Raw PNG SHA-256 | `874bba1c9fa9ba8435dcf61b29eca2786c049e0abf7d899680011a22e481b3a8` |
| Import metadata SHA-256 | `663f14d76bdf5381cd0b8fb293db68212a01065e7eefec5cd16f78ab20be6d7c` |
| Dimensions | 1536 × 1024 |
| Grid | 5 columns × 3 rows |
| Import | single sprite, sRGB, mipmaps disabled, maximum size 2048 |

The binding order is:

```text
town_hall, farm, lumber_mill, quarry, gold_mine,
barracks, academy, market, storehouse, forge,
stable, workshop, embassy, wall, watchtower
```

Each logical reference uses:

```text
Assets/AL/Art/Buildings/RuntimeExports/S_Building_Icon_Atlas_1536x1024_v001.png#<canonicalId>
```

The exact top-origin cell widths are `307, 307, 308, 307, 307`. Row heights
are `341, 342, 341`, starting at Y positions `0, 341, 683`. Those cells cover
the complete 1536 × 1024 image without gaps or overflow.

The asset relations match the accepted v003 progression order and exact
case-sensitive legacy aliases. The validator rejects asset swaps, coordinate
drift, changed hashes, changed import settings, unknown fragments, and any
identity mismatch.

This common icon source remains separate from the current realm-specific Town
Hall and Workshop production models. It neither replaces those bindings nor
promotes the procedural Kingdom board fallback into catalog authority.

## 3. Blocker transition

The effective building resolved list becomes:

```text
buildings.max_level_review
buildings.cost_profiles
buildings.duration_profiles
buildings.asset_refs
```

The building row retains exactly:

```text
buildings.production_profiles
```

The building family therefore remains `blocked_required`. No production rate,
live/offline accumulation rule, capacity, balance value, or migration outcome
is inferred from the visual asset.

The global blocker count changes from 26 in v003 to 25 in v004:

| Scope | Effective blockers |
| --- | ---: |
| User creative/balance approval | 1 |
| Buildings | 1 |
| Research | 5 |
| Troops | 5 |
| Champions | 6 |
| Skills | 7 |
| **Total** | **25** |

The realm family remains ready only for non-production shadow generation.
The other five required families retain their dispositions. The global gate
remains:

```text
status: blocked
requireProductionEligibleResult: refused_without_writes
outputPaths: []
```

## 4. Pinned provenance

The candidate pins six accepted inputs at the asset-authority commit:

| Role | Pinned source |
| --- | --- |
| Building asset decision | `Phase_C4D_Building_Asset_Reference_Authority.md` |
| Building asset registry | `GameDataBuildingAssetReferences.cs` |
| Six-family schema binding | `GameDataSixFamilySchemas.cs` |
| Building asset registry tests | `GameDataBuildingAssetReferenceTests.cs` |
| Building icon atlas | `S_Building_Icon_Atlas_1536x1024_v001.png` |
| Atlas import authority | `S_Building_Icon_Atlas_1536x1024_v001.png.meta` |

Every row contains the exact repository path, source revision, and raw
SHA-256. The validator reads the pinned Git blob, verifies its hash, and
requires current working bytes to match.

## 5. Validation

Run:

```text
python3 tools/game-data/test_phase_c_six_family_building_v004_asset_source.py
python3 tools/game-data/test_phase_c_six_family_building_v004_asset_source.py
python3 tools/game-data/test_phase_c_six_family_building_v004_asset_source.py --run-negative-fixtures
python3 tools/game-data/test_phase_c_six_family_building_v004_asset_source.py --require-production-eligible
```

The first two runs return the same raw v004 SHA-256. The negative-fixture run
proves that activation, asset swaps, coordinate/hash drift, blocker drift,
approval drift, and production output paths fail closed. The production
command validates the complete source and then returns nonzero with this
deterministic refusal:

```text
production generation refused without writes: productionEligible is false and 25 blockers remain
```

The validator also proves:

- strict UTF-8 v004 JSON with no BOM, duplicate fields, trailing data,
  comments, non-finite numbers, or non-canonical formatting;
- exact frozen v001, v002, and v003 identities and raw hashes;
- exact ordered root, provenance, family, atlas, binding, and blocker fields;
- exact Git blobs and working bytes for every new provenance source;
- exact PNG signature, dimensions, GUID, importer settings, and raw bytes;
- exact parity between all 15 asset bindings and inherited progression
  identities;
- complete grid coverage with no gap, overlap, or overflow;
- exactly one retained building blocker and 25 retained global blockers;
- pending approvals, unchanged runtime authority, and zero output paths.

## 6. Boundaries and next phase

This phase does not add a runtime atlas resolver, slice the image into Unity
sub-sprites, allocate player memory, change a scene, update a consumer, alter
a save, add a dependency, or publish a production catalog.

The next building-authority decision is the remaining production-profile
contract. That work needs explicit rates, resources, capacity/offline rules,
and balance acceptance; it must not be inferred from this asset source.
Runtime slicing and a first visible UI consumer are separate implementation
and visual-acceptance slices.

## 7. Acceptance

- [x] v001, v002, and v003 remain byte-for-byte unchanged and hash-pinned.
- [x] v004 has a new candidate ID and explicit versioned-overlay schema.
- [x] all six C4D inputs are pinned at one source commit.
- [x] all 15 building identities have one exact logical atlas reference.
- [x] grid, path, GUID, importer, dimensions, and raw bytes are exact.
- [x] exactly `buildings.asset_refs` is newly resolved.
- [x] `buildings.production_profiles` remains explicit and required.
- [x] exactly 25 global and non-production blockers remain.
- [x] production eligibility is false and generation refuses without writes.
- [x] runtime authority and user approval state remain unchanged.
- [x] no loader, consumer, scene, save, dependency, generated artifact,
  manifest, or production output is added.
