# Phase C9A Realm Shadow Artifact

## Document control

| Field | Value |
| --- | --- |
| Tracked issue | [#183](https://github.com/yulee94/AnotherLife/issues/183) |
| Phase | `Phase C9A — realm shadow artifact` |
| Primary mode | Codex engineering |
| Starting main | `cca8e4979ada7f09e3792c8d7005b01c1c10a935` |
| Source candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v003` |
| Source candidate raw SHA-256 | `984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439` |
| Inherited realm candidate | `game-data-phase-c-six-family-technical-source-2026-07-29-v002` |
| Inherited realm candidate raw SHA-256 | `60498d1a071ea79eb37c1b8889a1faaa5c7aee69679c1043256535ef4d3c1685` |
| Generator revision | `6e388e94b723008dcdf41b23ca3e0a510db4244e` |
| Generator raw SHA-256 | `71daae1ceef84d28417a4d25e0862d14df101b2df5eb253e4197349038889141` |
| Shadow artifact raw SHA-256 | `265160f0c20b10293a69572fbcc4703ad81add498b20dfb727c353e050b0eccb` |
| Evidence raw SHA-256 | `9aca84e7d937fffcaf26fa3f018d66fef251d6c9a84eeef90b8b251e7d121b83` |
| Binding specification | `unity/Docs/Game_Data_Catalog_Authority_Spec.md` |
| Runtime authority | Unchanged |
| Consumer activation | None |
| Shared-file lock | None |
| Production eligibility | `false` |
| User creative, balance, activation, playtest, and release approval | Pending |

Phase C9A creates the first common-envelope realm artifact as a deliberately
unwired shadow. It proves that the accepted realm source tuple can be emitted
as deterministic bytes and accepted by the existing common realm schema. It
does not create a catalog-set manifest, package a production artifact, publish
a catalog store, switch a service, or activate a consumer.

## 1. Exact output identity

The generated artifact is:

```text
unity/Docs/GameDataCatalog/PhaseC/Shadow/realm-family-shadow-v001.json
```

It uses:

```text
gameId: another-life
catalogId: realms_phase_c9a_shadow_v1
family: realms
schemaVersion: 1
contentVersion: 0.1.0-shadow.1
sourceRevision: game-data-phase-c-six-family-technical-source-2026-07-29-v003
```

The generated machine-readable evidence is:

```text
unity/Docs/GameDataCatalog/PhaseC/Shadow/realm-family-shadow-v001.evidence.json
```

Both files are strict UTF-8 JSON without a BOM, use one deterministic
two-space representation, and end with exactly one LF. The evidence records
the artifact hash but does not create or imitate a production manifest.

The generator writes only those two reviewed documentation paths. It refuses
generation if any of these production-like outputs exist:

```text
unity/Assets/AL/StreamingAssets/GameData/catalog-set.json
unity/Assets/Resources/GameData/catalog-set.json
unity/Docs/GameDataCatalog/PhaseC/Generated/catalog-set.json
unity/Assets/AL/StreamingAssets/GameData/Catalogs/realms.json
unity/Assets/Resources/GameData/Catalogs/realms.json
```

## 2. Source resolution and generation

The generator first runs the complete v003 validator, which in turn validates
the pinned v001 and v002 chain and all effective blockers. It then requires:

- v003 to remain production-ineligible with 26 effective blockers;
- v003 realm disposition to remain
  `ready_for_non_production_shadow_generation`;
- v003 to inherit the realm mappings, unavailable anchors, and resolved
  blocker IDs from the exact v002 candidate;
- v002 to retain four mappings, zero unavailable anchors, and zero realm
  blockers;
- all eight directly consumed inputs to equal both their pinned Git blobs and
  their reviewed raw SHA-256 values;
- every content reference to resolve in the Phase C1 content map;
- every resolved name and description to match the exact legacy
  `LocalGameDataService` source;
- the specialized catalog to retain version `0.1.0`, catalog ID
  `al_realm_catalog`, and the accepted realm order and shared fields;
- the generator working bytes to equal the exact recorded Git revision.

Run:

```text
python3 tools/game-data/generate_phase_c_realm_shadow_artifact.py
```

to verify committed outputs without writing. Regeneration is explicit:

```text
python3 tools/game-data/generate_phase_c_realm_shadow_artifact.py \
  --write \
  --generator-revision 6e388e94b723008dcdf41b23ca3e0a510db4244e
```

Changing a source byte, source revision, raw hash, generator byte, disposition,
blocker count, content relation, specialized relation, output path, or output
byte fails closed.

## 3. Exact realm artifact

The artifact contains four records in the accepted authored order:

```text
crownlands
stonehold
eldergrove
umbral
```

Every record carries exactly:

```text
id
legacy_realm_id
legacy_realm_value
name_ref
description_ref
inner_realm_id
main_gate_id
outer_warzone_id
rare_resource_id
capability_profile_ids
asset_ref
```

The generator translates no meaning. It copies the exact v002 tuple:

- canonical ID;
- exact legacy enum name and integer value;
- exact name and description references;
- exact inner realm, main gate, and outer warzone IDs;
- exact stable rare-resource ID;
- exactly one accepted capability-profile ID;
- exact Arcane Axis flat asset reference.

The artifact contains zero aliases. Case variants, whitespace variants,
display names, enum names, and unknown values are not inferred as aliases.

## 4. Specialized catalog comparison

The specialized
`unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json` remains the
live realm-selection source. C9A compares but does not modify it.

The following common and specialized fields match for all four realms:

| Shadow field | Specialized field |
| --- | --- |
| `id` | `id` |
| `legacy_realm_id` | `legacyRuntimeId` |
| resolved `name_ref` | `peopleName` |
| `inner_realm_id` | `innerRealmId` |
| `main_gate_id` | `mainGateId` |
| `outer_warzone_id` | `outerWarzoneId` |

The accepted authored order also matches exactly.

The following fields are present only in the common shadow record:

```text
legacy_realm_value
name_ref
description_ref
rare_resource_id
capability_profile_ids
asset_ref
```

They come from the accepted Phase C source and registries, not from inference
over the specialized catalog.

Selection policy, narrative continuity, and Realm Gem references remain
specialized-only authority. Their absence from the common realm schema is not
a deletion, migration, or permission to ignore them during a later consumer
switch.

The evidence sidecar records six ordered informational diagnostics: one exact
shared-field match per authored realm, one retained-specialized-scope result,
and one unchanged-runtime-authority result.

## 5. Common-schema validation

The focused EditMode test creates a one-artifact validation manifest only in
memory. No manifest is committed or packaged. It sends the exact committed
artifact bytes through:

```text
GameDataCatalogValidator.ValidateManifest
GameDataCatalogValidator.ValidateCatalogSet
GameDataSixFamilySchemas.CreateRegistry
```

The accepted snapshot must contain one `realms` family, four records, zero
aliases, the exact artifact hash, and every reviewed relation in
`GameDataRealmReferences` and `GameDataRealmCapabilityProfiles`.

Pure snapshot queries must find the four canonical IDs and reject empty,
unknown, case-varied, and whitespace-varied IDs. A combined cross-realm
resource/profile/asset mutation must fail with these exact ordered
diagnostics:

```text
AL-GDC-REALM-WORLD-ASSET-REFERENCE
AL-GDC-REALM-CAPABILITY-PROFILE-REFERENCE
AL-GDC-REALM-RARE-RESOURCE-REFERENCE
```

The order is the common validator's stable artifact, record, field-path, and
code ordering. This is validation-only publication into an immutable test
result, not gameplay or service publication.

## 6. Validation

Run:

```text
python3 tools/game-data/generate_phase_c_realm_shadow_artifact.py
python3 tools/game-data/test_phase_c_realm_shadow_artifact.py
python3 tools/game-data/test_phase_c_realm_shadow_artifact.py \
  --run-negative-fixtures
python3 tools/game-data/test_phase_c_realm_shadow_artifact.py \
  --require-production-eligible
```

Expected results:

- the generator and default validator pass twice with the same artifact and
  evidence hashes;
- fourteen representative artifact/evidence mutations fail closed;
- the production-eligibility command validates first and then returns nonzero
  with the deterministic 26-blocker/no-manifest/no-consumer refusal;
- no command writes outside the two reviewed shadow documentation paths.

Focused Unity validation:

```text
Unity 2022.3.62f3
-runTests
-testPlatform EditMode
-assemblyNames AL.EditMode.Tests
-testFilter AL.Tests.EditMode.GameDataCatalog.RealmShadowArtifactTests
```

Result: 2 total, 2 passed, 0 failed, 0 skipped.

The full local EditMode run compiled and executed 1,249 tests: 1,187 passed,
62 failed, and 0 were skipped. Both C9A tests passed and no compiler error
occurred. All 62 failures are outside the C9A test class and reflect existing
local asset/reference availability: 36 production-launch smoke cases, 12
cross-platform design-asset cases, and 14 architecture, save-candidate,
realm-reference, and production-scene cases. Those unrelated asset-dependent
failures do not weaken the focused common-schema proof, and C9A does not
modify or attempt to reconstruct their missing production inputs.

The artifact is 2,511 bytes and the evidence sidecar is machine-readable
documentation only. Neither enters the Player. The test and Python tooling do
not add runtime dependencies, allocations, asset residency, frame-time work,
package bytes, install bytes, network requirements, or low-end-device
constraints.

## 7. Boundaries and rollback

C9A changes no runtime assembly, loader, store, service, consumer, save,
scene, packaged asset, Android source, workflow, dependency, or player-facing
source. The catalog ID appears in no Unity runtime or Android consumer.

The specialized catalog and legacy service remain the only live realm
authority surfaces. The common shadow has:

```text
productionEligible: false
runtimeAuthority: unchanged
consumerActivation: none
```

Before an authority switch, rollback is deletion or rejection of the two
unpublished shadow files. No save, profile, realm selection, Realm Gem,
Champion, kingdom, territory, quest, or UI state is read or rewritten.

Publication, a complete six-family manifest, whole-set shadow validation,
service migration, consumer migration, packaging, fault/reload/lifecycle
proof, integrated playtest, balance approval, activation approval, and
release approval remain later gates.

## 8. Acceptance

- [x] generation consumes the exact v003 to v002 realm source chain.
- [x] the generator is pinned by Git revision and raw SHA-256.
- [x] the artifact uses the exact common envelope and common realm schema.
- [x] four records retain exact authored order and reviewed relations.
- [x] aliases remain empty and unsupported identifiers are not normalized.
- [x] content references resolve to exact Phase C1 and legacy source.
- [x] specialized shared fields match while specialized-only authority is
  retained.
- [x] artifact and evidence bytes/hashes are identical across two clean runs.
- [x] ordered comparison and invalid-record diagnostics are deterministic.
- [x] fourteen representative mutations fail closed.
- [x] production eligibility is refused with 26 blockers.
- [x] no production manifest, packaged realm artifact, runtime consumer,
  shared-file edit, or authority switch is introduced.
- [x] focused EditMode validation passes 2/2.
