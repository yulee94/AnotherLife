# Phase C3E Realm Blocker-Ledger Convergence

## Document control

| Field | Value |
| --- | --- |
| Tracked issue | [#183](https://github.com/yulee94/AnotherLife/issues/183) |
| Phase | `Phase C3E — realm blocker-ledger convergence` |
| Primary mode | Codex coordination/review |
| Audited current main | `4f8200efbe4d3054db3765e0eace20035b89557b` |
| Frozen Phase C2 candidate | `game-data-phase-c-six-family-technical-source-2026-07-23-v001` |
| Frozen Phase C2 raw SHA-256 | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` |
| Binding specification | `unity/Docs/Game_Data_Catalog_Authority_Spec.md` |
| Runtime authority | Unchanged |
| Shared-file lock | None |
| Realm artifact disposition | `blocked_required` |
| Production eligibility | `false` |
| User balance, activation, playtest, and release approval | Pending |

This decision converges the Phase C2 realm blocker ledger with the reviewed
source and implementation merged through Phase C3D. It records which realm
source blockers are resolved, preserves the exact implemented four-realm
reference tuple, and identifies the one unresolved realm-specific source
authority that still prevents a common shadow artifact.

This document is a versioned status overlay. It does not rewrite the frozen
Phase C2 candidate, generate a replacement candidate, publish catalog bytes,
change runtime authority, or make the realm family or issue `#183` complete.

## 1. Scope and non-goals

This phase decides:

- how the three realm blocking IDs in the frozen Phase C2 candidate are
  dispositioned after PRs `#385` through `#388`;
- which implemented source, registry, schema, constraint, and provenance
  evidence resolves each technical blocker;
- the exact four-record tuple a later versioned technical source must consume;
- the remaining source and approval gates before deterministic shadow
  generation may begin;
- how a future v002 candidate supersedes the realm portion of v001 without
  altering historical evidence.

This phase does not:

- edit
  `unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source.json`;
- emit a new technical-source JSON file, family artifact, manifest, envelope,
  or generated hash;
- invent realm capability profiles, perks, rates, balance, or presentation
  meaning;
- change `GameDataSixFamilySchemas`, either exact reference registry, a
  runtime service, loader, save, scene, UI, asset, workflow, or dependency;
- change the authority of `RealmCatalogRuntime`, `LocalGameDataService`, or
  any current realm consumer;
- authorize runtime publication, migration, activation, integrated playtest,
  or release.

## 2. Frozen Phase C2 evidence rule

The Phase C2 v001 candidate remains immutable historical evidence. Its three
realm blocking IDs are:

```text
realms.rare_resource_catalog
realms.capability_profiles
realms.asset_refs
```

Its family `artifactDisposition: blocked_required` value remains unchanged,
its top-level `productionEligible` value remains `false`, and its generation
gate remains `blocked`. The v001 file also retains the legacy-enum-order realm
mapping array. Phase C3A already superseded that array order for future
common-family generation without invalidating its identity evidence.

The v001 file must not be edited in place because Phase C3A and C3B pin its
exact path and raw SHA-256. Mutating those bytes would erase the reviewed
historical input rather than version the decision. A later technical source
must use a new candidate ID and explicitly identify v001 plus this decision as
superseded inputs.

This overlay changes the current disposition of realm blockers. It does not
pretend that the literal arrays inside v001 changed.

## 3. Accepted Phase C3 lineage

| Phase | Pull request and merge | Accepted evidence |
| --- | --- | --- |
| C3A | `#385`, `181703ab06f76e0935fc36bbcd33c9cfad9dca58` | Authored realm order, exact identity/content/world tuple, Arcane Axis source selection, shadow and rollback boundaries |
| C3B | `#386`, `4e0164c270d905b4019e8a04cb4a92d9e9ad8db5` | Ten exact stable resource IDs and the four balance-neutral realm-to-resource relations |
| C3C | `#387`, `6a97234db1ac3ab7b395d2ba6d5627ac28caba10` | Immutable resource-reference registry, typed exact resolution, realm/resource record constraint, schema binding, and rejection tests |
| C3D | `#388`, `4f8200efbe4d3054db3765e0eace20035b89557b` | Immutable realm-reference registry, world-boundary schema, exact world/asset record constraint, and GUID/raw-file provenance tests |

These merged decisions form one non-production source chain. Later work must
consume the current code and the pinned decisions together. It must not
reconstruct authority from enum names, fallback methods, UI labels, prose, or
incidental source order.

## 4. Exact implemented realm tuple

Future technical-source and generator work must consume the following authored
order and exact relationships:

| Order | Realm ID | Legacy enum/value | Content references | Inner realm | Main gate | Outer warzone | Rare resource ID | Exact `asset_ref` |
| ---: | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | `crownlands` | `RealmId.Crownlands` / `3` | `realm.crownlands.name`, `realm.crownlands.description` | `inner_crownlands` | `gate_crownlands_meridian` | `warzone_crownlands` | `royal_sigil` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Crownlands_Flat_256_v001.png` |
| 2 | `stonehold` | `RealmId.Stonehold` / `1` | `realm.stonehold.name`, `realm.stonehold.description` | `inner_stonehold` | `gate_stonehold_faultline` | `warzone_stonehold` | `deep_ore` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Stonehold_Flat_256_v001.png` |
| 3 | `eldergrove` | `RealmId.Eldergrove` / `2` | `realm.eldergrove.name`, `realm.eldergrove.description` | `inner_eldergrove` | `gate_eldergrove_greenveil` | `warzone_eldergrove` | `world_sap` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Eldergrove_Flat_256_v001.png` |
| 4 | `umbral` | `RealmId.Umbral` / `4` | `realm.umbral.name`, `realm.umbral.description` | `inner_umbral` | `gate_umbral_ashvein` | `warzone_umbral` | `dark_crystal` | `Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Umbral_Flat_256_v001.png` |

The exact asset provenance carried by those records is:

| Realm ID | Unity GUID | Raw PNG SHA-256 |
| --- | --- | --- |
| `crownlands` | `ba4dfcc7b514049f79f6ec3424193b46` | `f5c7e351ec930aac69f6df02d03034bc38c465ed8dfa787dd4feba044f33f82b` |
| `stonehold` | `94d8d9e2cf04a4b769c213a13c164b8e` | `53d220dc8b938d212963286133ca39e1968fa1421126559dd56bdfde9c437946` |
| `eldergrove` | `53001b27fd9d14914984211765be4391` | `1d45fc8fba82ebb3fdc1c4f819026ea8e45b11c248378371c7b2b6923c6e0cac` |
| `umbral` | `a426041e03b0742999a34b8b5e198406` | `a9daefa3ea6445ba2db680dad92a456db75becebec8848c678b29d5ea2c85aaa` |

The canonical source for this complete tuple is
`GameDataRealmReferences.Entries`. `GameDataWalletResourceReferences` is the
only stable resource ID resolver. `GameDataSixFamilySchemas` binds the allowed
sets and validates the complete realm relation through exact record
constraints.

Unknown IDs, `RealmId.None`, undefined enum values, case or separator variants,
normalized values, changed relations, and fallback outcomes remain invalid.
No rejecting input may become Crownlands, `royal_sigil`, or another valid
record.

## 5. Effective realm blocker disposition

| Blocking ID or prerequisite | C3E disposition | Current evidence and boundary |
| --- | --- | --- |
| `realms.rare_resource_catalog` | **Resolved as input to future non-production generation** | `GameDataWalletResourceReferences` fixes the ten stable resource identities and typed mappings. `GameDataRealmReferences` owns the exact four realm relations. The schema accepts only registered IDs and the record constraint rejects mismatched realm/name/value/resource tuples. This does not approve rates, starting balances, rewards, or production publication. |
| Realm world-boundary schema | **Resolved as input to future non-production generation** | `inner_realm_id`, `main_gate_id`, and `outer_warzone_id` are required, exact-allowed fields. The full world/asset record constraint proves the approved relation for every realm. This prerequisite was absent from the v001 blocker array but was required by C3A. |
| `realms.asset_refs` | **Resolved as input to future non-production generation** | The exact flat Arcane Axis paths are schema-bound through `GameDataRealmReferences`. Editor tests verify Unity GUIDs and raw PNG SHA-256 values. Runtime loading, atlas/residency policy, final colors, first consuming surface, and device budgets remain later activation decisions rather than source blockers. |
| `realms.capability_profiles` | **Open; sole remaining realm-specific source blocker** | No approved profile IDs, profile records, behavior definitions, realm-to-profile mappings, or balance decision exist. Current perk prose, command labels, identity pillars, starter-class bias, Champion behavior, architecture profiles, and visual treatments remain explicitly non-authoritative. |

The effective realm-specific blocker set for a future v002 technical source is
therefore:

```text
realms.capability_profiles
```

The top-level `approval.user_creative_balance` gate and all unresolved blockers
for the other five required families remain in force. Removing two resolved
realm IDs from a future candidate does not make the six-family set production
eligible.

## 6. Capability-profile authority required next

Before a v002 technical source or common realm shadow artifact may be
generated, a separate reviewed source decision must define:

1. the exact canonical stable ID for every capability profile;
2. the exact ordered profile-ID array for each of the four realms;
3. the behavior owned by each profile and the runtime system that may consume
   it;
4. explicit separation between gameplay behavior, balance values,
   player-facing copy, and visual presentation;
5. whether any aliases exist and, if so, their introduced version, retirement
   policy, and migration issue;
6. exact source paths, source revisions, raw hashes, and user balance/product
   approval state;
7. rejecting validation for unknown, empty, duplicated, normalized,
   cross-realm, or unsupported references;
8. rollback behavior that leaves current realm selection, saves, and runtime
   authority unchanged.

Existing values may be audited as evidence, but they may not be promoted
merely because they are already hard-coded or player-visible. If no defensible
authority is available, the realm family remains blocked rather than receiving
neutral, placeholder, inferred, or empty capability profiles.

## 7. Future v002 transition

After capability-profile authority is accepted and implemented, a later
engineering slice may create a new versioned technical source. That candidate
must:

- use a new candidate ID and preserve v001 unchanged;
- name v001, C3A, C3B, this C3E decision, and the eventual capability decision
  in provenance;
- use authored order `crownlands, stonehold, eldergrove, umbral`;
- carry every exact field in Section 4 plus the approved capability-profile
  IDs;
- remove `realms.rare_resource_catalog` and `realms.asset_refs` from its
  blocker arrays only because the merged registries, schema, constraints, and
  tests prove them;
- remove `realms.capability_profiles` only when its accepted source and
  implementation prove it;
- remain `productionEligible: false` while any required family or global
  approval gate is unresolved;
- refuse production output without writes when its generation gate is blocked;
- generate twice from clean inputs and prove identical bytes, hashes, order,
  provenance, and ordered diagnostics.

No common realm artifact may become live authority in that slice. Shadow
generation, publication, consumer migration, and activation remain separate
reversible phases.

## 8. Pinned current evidence

Hashes below are lower-case SHA-256 over the exact committed file bytes visible
at the audited current main.

| Source | Source revision | Raw SHA-256 | Role |
| --- | --- | --- | --- |
| `unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source.json` | `5858967b17a8c802ba4aca6225e1b61e45cdf5d9` | `5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b` | Frozen v001 mapping and blocker evidence |
| `unity/Docs/GameDataCatalog/PhaseC/Phase_C3A_Realm_Authority_Convergence.md` | `27e2d477ad98f131593b991c85ce8388d9216641` | `4d220976127f1c80c407a42ce987aa50a38dbee268ecc7a80258f3178c77925f` | Realm order, exact tuple, source precedence, asset choice, and shadow boundary |
| `unity/Docs/GameDataCatalog/PhaseC/Phase_C3B_Resource_Reference_Authority.md` | `4122483dda7cbba5d87577e70b4280786849be1d` | `c068ec6d24e10d94fe7466355987e9c1161fa4a390443fa77a93c136216121f0` | Stable resource vocabulary and exact realm relations |
| `unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataWalletResourceReferences.cs` | `f44e22cd3a0e334062f1ef8e487ffca1ecba6261` | `07ef09c4bca55278a7db6dd09c9740352829bb677eb1ea4c817b8646ac02c699` | Immutable ten-resource registry and typed exact resolution |
| `unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataRealmReferences.cs` | `f44e22cd3a0e334062f1ef8e487ffca1ecba6261` | `4bb8457c9831756a8cf6c2ddf3f14a5fd5c51866370c870cb074a53313bbdf4f` | Exact authored-order realm identity/content/world/resource/asset tuple |
| `unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataSixFamilySchemas.cs` | `f44e22cd3a0e334062f1ef8e487ffca1ecba6261` | `44d8f4ef9375773b2c9f833c6adc744dd5d3781c91e2564586494ea28749c1bd` | Required fields, exact allowed sets, and realm record constraints |
| `unity/Assets/AL/Scripts/Core/ResourceRules.cs` | `f44e22cd3a0e334062f1ef8e487ffca1ecba6261` | `9831954003e28c4209995c12da9b5b72681cdd2c27c007cc7393a2563829e68d` | Rejecting typed relation used to detect enum/resource drift |

Any change to a pinned byte, enum value, stable ID, authored order, relation,
asset path, GUID, hash, or field contract blocks later generation until a
reviewed superseding decision reconciles it.

## 9. Validation and acceptance

Phase C3E is accepted when review verifies:

- [x] v001 remains byte-for-byte unchanged and parses as JSON;
- [x] the distinction between frozen literal blockers and effective current
  dispositions is explicit;
- [x] the complete four-realm tuple matches the immutable current-main
  registry;
- [x] resource, world-boundary, and asset resolutions cite merged
  implementation evidence;
- [x] capability profiles remain the sole unresolved realm-specific source
  blocker;
- [x] global approval and the other five required families remain blocked;
- [x] no profile, balance, fallback, alias, runtime authority, or production
  eligibility is inferred;
- [x] future v002, shadow generation, activation, rollback, and user approval
  boundaries remain separate;
- [x] exact source revisions and raw hashes are pinned;
- [x] no runtime, schema, source JSON, asset, save, workflow, dependency, or
  generated artifact changed in this phase.

## Impact

This decision adds documentation only. It adds no runtime code, generated
catalog, texture, mesh, audio, scene, save field, loader, allocation, frame
loop, render cost, build byte, install byte, package, or dependency.
Performance, memory, package size, install size, and device compatibility are
unchanged. Player-build, PlayMode, device, and integrated playtest evidence
are not applicable to this source-only convergence slice.
