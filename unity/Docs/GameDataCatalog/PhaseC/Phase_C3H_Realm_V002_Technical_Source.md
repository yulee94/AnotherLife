# Phase C3H Realm v002 Technical Source

## Document control

| Field | Value |
| --- | --- |
| Tracked issue | [#183](https://github.com/yulee94/AnotherLife/issues/183) |
| Phase | `Phase C3H — realm v002 technical source` |
| Primary mode | Codex engineering |
| Starting main | `1e344644b853c11f618b3d31434c88c9b66d14a6` |
| Frozen Phase C2 candidate | `game-data-phase-c-six-family-technical-source-2026-07-23-v001` |
| Frozen Phase C2 raw SHA-256 | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` |
| New candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v002` |
| New candidate raw SHA-256 | `60498d1a071ea79eb37c1b8889a1faaa5c7aee69679c1043256535ef4d3c1685` |
| Binding specification | `unity/Docs/Game_Data_Catalog_Authority_Spec.md` |
| Runtime authority | Unchanged |
| Shared-file lock | None |
| Production eligibility | `false` |
| User creative, balance, activation, playtest, and release approval | Pending |

Phase C3H creates the first superseding six-family technical-source candidate
without changing the frozen v001 evidence. It applies the accepted Phase C3
realm decisions as a strict versioned overlay, removes the three resolved
realm source blockers, and retains every unresolved global and non-realm
blocker.

The candidate is source for a later non-production shadow generator. It is not
a common family artifact, catalog-set manifest, runtime publication, balance
approval, or authority switch.

## 1. Versioned-overlay contract

The v002 candidate uses `schemaVersion: 2` and
`sourceKind: versioned_overlay`. Its `supersedes` object pins the v001
candidate by:

- exact candidate ID;
- exact repository path;
- exact source revision;
- raw SHA-256 over committed bytes.

The overlay is resolved in this order:

1. verify the frozen v001 bytes and identity;
2. verify every ordered provenance input against its pinned Git blob and
   current working bytes;
3. replace the v001 `realms` family row with the complete v002 realm row;
4. resolve the other five family mappings and unavailable anchors directly
   from their named v001 family rows;
5. retain those five families' exact `blocked_required` dispositions and
   blocker arrays;
6. recompute the ordered top-level blocker array;
7. refuse production generation while the global gate is blocked.

An implementation must not merge the overlay through a permissive object
patch, accept an unpinned base, infer a family by array position alone, ignore
an unknown field, or fall back to another candidate.

## 2. Exact realm transition

The v002 realm row contains four authored-order mappings:

```text
crownlands
stonehold
eldergrove
umbral
```

Every row carries the accepted:

- canonical realm ID and exact legacy enum name/value;
- two content references and empty alias array;
- stable rare-resource ID;
- exactly one stable capability-profile ID;
- inner-realm, main-gate, and outer-warzone IDs;
- Arcane Axis asset path, Unity GUID, and raw PNG SHA-256;
- legacy rare-resource enum anchor as migration evidence only.

The row records these three IDs as resolved:

```text
realms.rare_resource_catalog
realms.capability_profiles
realms.asset_refs
```

Its effective `blockingIds` array is empty and its disposition is
`ready_for_non_production_shadow_generation`. This means the accepted realm
source tuple is complete enough for a later unwired shadow generator. It does
not mean a realm artifact exists or is eligible for production.

## 3. Preserved blockers and approvals

The five other required families remain byte-grounded in v001:

| Family | Disposition | Effective blockers |
| --- | --- | ---: |
| Buildings | `blocked_required` | 5 |
| Research | `blocked_required` | 5 |
| Troops | `blocked_required` | 5 |
| Champions | `blocked_required` | 6 |
| Skills | `blocked_required` | 7 |

Together with `approval.user_creative_balance`, v002 retains exactly 29
ordered blockers. Both user approval fields remain `pending`, balance values
remain migration evidence rather than approved authority, and runtime
authority remains `unchanged`.

The global generation gate remains:

```text
status: blocked
requireProductionEligibleResult: refused_without_writes
outputPaths: []
```

No production manifest, family catalog, generated directory, runtime loader,
or packaged output is created.

## 4. Pinned provenance

The candidate pins eight accepted inputs:

| Role | Pinned source |
| --- | --- |
| Realm authority | `Phase_C3A_Realm_Authority_Convergence.md` |
| Resource authority | `Phase_C3B_Resource_Reference_Authority.md` |
| Realm blocker decision | `Phase_C3E_Realm_Blocker_Ledger_Convergence.md` |
| Capability authority | `Phase_C3F_Realm_Capability_Profile_Authority.md` |
| Resource registry | `GameDataWalletResourceReferences.cs` |
| Realm registry | `GameDataRealmReferences.cs` |
| Capability registry | `GameDataRealmCapabilityProfiles.cs` |
| Six-family schema | `GameDataSixFamilySchemas.cs` |

Every provenance row contains the exact repository path, source revision, and
raw SHA-256. The validator reads the pinned Git blob, verifies its raw hash,
and requires the current working file to equal those bytes. Source drift
therefore blocks v002 instead of silently changing its meaning.

## 5. Validation

Run:

```text
python3 tools/game-data/test_phase_c_six_family_realm_v002_technical_source.py
python3 tools/game-data/test_phase_c_six_family_realm_v002_technical_source.py
python3 tools/game-data/test_phase_c_six_family_realm_v002_technical_source.py --require-production-eligible
```

The first two runs must return the same candidate SHA-256. The third command
must validate the complete candidate and then return nonzero with a
deterministic production-refusal diagnostic. None of the commands writes an
output file.

The validator also proves:

- strict UTF-8 JSON with no BOM, duplicate fields, trailing data, comments,
  non-finite numbers, or non-canonical v002 formatting;
- exact v001 identity and raw hash;
- exact ordered schema, provenance, family, realm, and blocker fields;
- exact retained-family blocker parity with v001;
- exact realm asset bytes and Unity GUIDs;
- absence of common production manifest and family artifact paths;
- pending approvals, 29 retained blockers, zero output paths, and unchanged
  runtime authority.

## 6. Boundaries and next phase

This phase changes documentation and validation tooling only. It does not
change C# code, Android code, Unity assemblies, assets, `.meta` files, scenes,
saves, packages, workflows, dependencies, or runtime behavior. It adds no
Player bytes and has no frame-time, memory, package-size, install-size, or
device-compatibility effect.

A later engineering phase may consume v002 to build an unwired common realm
shadow artifact and compare it with the specialized current authority. That
phase must separately prove deterministic artifact bytes, hashes, envelopes,
ordered diagnostics, and zero consumer activation. Publication, runtime
migration, activation, integrated playtest, balance approval, and release
remain separate gates.

The next six-family source decision may also proceed on one unresolved family.
It must retain the other blockers rather than using realm completion to imply
whole-set readiness.

## 7. Acceptance

- [x] v001 remains byte-for-byte unchanged and pinned by raw SHA-256.
- [x] v002 has a new candidate ID and explicit versioned-overlay schema.
- [x] all required C3 realm decisions and implementation inputs are pinned.
- [x] the complete realm tuple uses the accepted authored order.
- [x] the three realm source blockers are removed only in v002.
- [x] all five unresolved families remain blocked and inherit mappings only
  from the pinned v001 source.
- [x] exactly 29 global and non-realm blockers remain.
- [x] production eligibility is false and generation refuses without writes.
- [x] runtime authority and user approval state remain unchanged.
- [x] no common artifact, manifest, loader, consumer, save, asset, dependency,
  workflow, or production output is added.

## 8. 2026-08-06 historical-validator correction

This dated correction supersedes only the current-working-copy equality rule
recorded in sections 1 and 4. The original rule was valid while C3H was the
current authority surface, but successor overlays intentionally evolve some of
the same repository paths. A frozen C3H candidate must continue to validate
the source that it actually pinned rather than reinterpret that source through
today's working copy.

For the frozen v001 source, the v002 `supersedes` row, and every ordered v002
provenance row, the authoritative evidence remains the exact tuple of:

```text
sourceRevision + repository-relative path + Git blob + raw SHA-256
```

The validator now reads that blob directly from the pinned revision and fails
closed when the revision, path, blob, or raw hash is missing or wrong. It does
not require the current working-tree path to exist or equal the historical
blob. This prevents a legitimate successor overlay or checkout line-ending
policy from rewriting or invalidating historical authority.

The candidate supplied through `--source` remains independently strict. It
must still be canonical UTF-8 JSON without a BOM, carry the exact frozen v002
raw SHA-256 and semantic contract, and retain every production, approval, and
runtime-authority boundary. Current realm asset bytes and GUIDs remain checked
because those asset rows do not carry a historical source revision. This
correction does not alter the v001 or v002 JSON artifacts, realm mappings,
blockers, approval state, production eligibility, runtime authority, or any
later v003/shadow validator.

Run the self-contained regression matrix with:

```text
python3 tools/game-data/test_phase_c_six_family_realm_v002_technical_source.py --run-negative-fixtures
```

The matrix creates an isolated temporary Git history and proves four positive
cases, including current-path evolution and removal, plus eight negative cases
covering invalid revision/path/hash pins and malformed or mutated candidate
bytes. It writes no repository or production output. Working-tree line-ending
normalization remains a separate build-health responsibility.
