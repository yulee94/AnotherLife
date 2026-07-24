# Class Progression Catalog Contracts — Engineering Report

- Status: implementation and focused validation complete; runtime activation intentionally unavailable
- Date: 2026-07-24
- Primary Codex mode: engineering
- Current project phase: Phase 1 — NVS-01
- Branch: `codex/class-progression-catalog-contracts`
- Upstream game-data issue: #183
- Adjacent dependencies: #137, #152, #155, #160, #163, #165, #168, #171, #173, #177, #180, #184
- Controlled narrative source: `ANOTHERLIFE_CLASS_IDENTITY_SKILL_TREES`
- Controlled source version: `anotherlife-class-identity-skill-trees-2026-07-24-v001`
- Controlled source validation revision: `2529a170426f1e0cc8c145233e2daf1ca0ac5f6d`
- Coordination source: `unity/Docs/Class_Identity_Skill_Tree_Integration_Spec.md`

## Outcome

This slice adds a strict, immutable, non-wired catalog contract for the accepted
class-identity progression spine. It covers the exact four-family and 16-class
roster, resources, general-tree identities, branches, milestone identities,
optional mastery trials, and class-specific Warmaster identities.

The result is intentionally not a playable skill system. A successful semantic
validation produces `AcceptedIdentitySpine`, while
`ClassProgressionIdentitySnapshot.IsProductionReady` remains `false`. There is
no API in this slice that can promote the snapshot to production-ready state.

## User decision consumed

The user accepted the 16-class direction for continued development and delegated
remaining working product and balance decisions to Codex on 2026-07-24. Final
product, creative, visual-design, balance, irreversible-profile, milestone,
integrated-playtest, and release approval remain user-owned.

## Goal

Provide the first pure engineering boundary that can validate and query the
accepted class source without:

- inventing the missing production skill nodes;
- assigning executable behavior or presentation;
- introducing numeric combat or economy balance;
- wiring services, saves, scenes, UI, or StreamingAssets;
- treating current prototype skills, Forge presets, or legacy visuals as class
  authority;
- weakening the existing six-family game-data schemas.

## Atomic family inventory

| Family | Exact records | Authority in this slice |
|---|---:|---|
| `class_sources` | 1 | packet ID, version, hashes, revisions, component provenance, source-only status |
| `class_families` | 4 | exact family identity, legacy mapping, all-realm availability, class membership |
| `playable_classes` | 16 | explicit family ownership, role/contribution identity, equipment silhouette identity, owned references |
| `class_resources` | 16 | qualitative gain/spend identity only |
| `class_skill_trees` | 16 | level-1 visibility, three non-exclusive branches, five milestones, capstone, four-slot boundary |
| `class_skill_branches` | 48 | ordered class/tree-owned branch identities |
| `class_milestone_skills` | 80 | level 10/20/30/40/50 class-owned identity anchors |
| `class_mastery_trials` | 16 | optional, recoverable, non-critical, non-gating mastery identities |
| `class_warmaster_identities` | 16 | class title, set, relic, True Warmaster skill identity, ten slots, structural eligibility |

All nine artifacts are required and must validate as one complete set.

## Contract decisions

### Explicit mapping

Family ownership is stored and checked directly. It is never derived from
`SubclassId` ordinal ranges. This preserves the non-grouped legacy values:

- Paladin `13` → Warrior;
- Necromancer `14` → Mage;
- Slayer `15` → Assassin;
- Druid `16` → Ranger.

The existing technical `champions.class_family_id` values
`warrior|mage|ranger|assassin` are not silently rewritten. A future
champion-schema migration must explicitly reconcile them with
`family_warrior|family_mage|family_ranger|family_assassin`.

### General-tree identity

Each class has one deterministic tree ID:

```text
skill_tree_<class-token>_general
```

The packet's branches and milestones are represented as an identity spine. The
milestones are not assigned to branches and are not declared active, passive,
granted, purchased, ranked, or executable. Those decisions require the next
accepted content-and-balance source.

### Level-50 boundary

The initial level cap is 50. Mastery-trial availability and the level component
of Warmaster eligibility are therefore encoded as becoming available upon
reaching level 50, never level 51. Mastery trials remain optional and cannot
gate the ordinary level-50 capstone or Warmaster.

### Warmaster boundary

Each class-specific Warmaster record preserves:

- one title;
- one set;
- one relic;
- one True Warmaster skill identity;
- the exact ten distinct piece-slot tokens;
- level 50, realm-contract, committed-Warzone-point, and complete-set structural
  requirements;
- the normal four-slot loadout policy.

It does not define item records, prices, stats, point thresholds, acquisition
transactions, cooldowns, effects, targeting, or combat authorization.

### Authority separation

The class layer does not duplicate the current six-family `skills` row or the
prototype `SkillLoadoutData` shape. Executable skill behavior and presentation
remain owned by #180. Save/profile state remains owned by #137/#184. Runtime
catalog registration remains owned by #183. Warmaster transactions remain
owned by #171/#163.

Missing combat bindings must later yield an explicit unavailable/not-ready
result. Slot-index behavior and prototype fallback are prohibited.

## Semantic validation

`ClassProgressionCatalogValidator` runs after the generic strict JSON, schema,
hash, alias, and cross-reference validator. It rejects the whole set on any
error and publishes no partial typed snapshot.

It checks:

- exact packet ID, version, SHA-256, authored revision, validated revision, four
  component paths, component family mappings, and component SHA-256 values;
- exact catalog-set ID and source revision plus required, ordered, authored
  artifact descriptors with deterministic catalog IDs and paths;
- canonical projection SHA-256
  `d5ae844106f633ef5f92b1f78ec3b65d26513eb84cba0a8768c9636044ede745`
  across every record field and alias, preventing a coherent rewrite from
  hiding behind unchanged declared source hashes;
- exact nine-family inventory, manifest order, and record counts;
- exact family and class IDs, explicit mapping, legacy names, legacy values,
  global order, and family-local order;
- all four launch realms for every family;
- Druid primary healer, Paladin secondary healer, and Necromancer non-healer;
- one reciprocal resource and one reciprocal general tree per class;
- exactly three distinct ordered branches and five distinct milestones at
  levels `10,20,30,40,50`;
- the level-50 milestone as the ordinary capstone;
- stable owner prefixes and all 244 unique owner-derived localization name keys;
- exactly the 16 `SQ_*` mastery aliases and no aliases in any other family;
- optional, recoverable, non-critical, non-gating mastery policy;
- exact class-owned Warmaster title, set, relic, True Warmaster skill namespace,
  ten piece slots, eligibility flags, and standard-slot policy;
- `production_eligible: false` for source, tree, milestone, and Warmaster
  identities;
- deterministic bounded `AL-GDC-CLS-*` diagnostics.

## Files

Production:

- `unity/Assets/AL/Scripts/Data/Catalogs/ClassProgression/GameDataClassProgressionSchemas.cs`
- `unity/Assets/AL/Scripts/Data/Catalogs/ClassProgression/ClassProgressionCatalogContracts.cs`
- `unity/Assets/AL/Scripts/Data/Catalogs/ClassProgression/ClassProgressionCatalogValidator.cs`
- matching Unity metadata

Tests:

- `unity/Assets/AL/Tests/EditMode/GameDataCatalog/GameDataClassProgressionSchemaTests.cs`
- matching Unity metadata

No assembly definition changed. The production types remain inside the
no-engine `AL.GameDataCatalog` assembly.

## Validation

### Narrative source

Command:

```text
C:\Users\MY\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe tools/narrative/test_class_identity_skill_trees.py
```

Result:

```text
accepted: components=4, families=4, classes=16, branches=48,
milestoneSkills=80, masteryTrials=16, warmasterSets=16,
warmasterRelics=16, trueWarmasterSkills=16, localizedNames=244,
legacyVisualLabels=12, forgePresets=9, prototypeSkills=4,
negativeFixtures=19
```

The Unity fixture normalizes checked-out JSON line endings to LF before hashing,
matching the Git-controlled source bytes on Windows and CI.

### Focused EditMode suite

Filter:

```text
AL.Tests.EditMode.GameDataCatalog.GameDataClassProgressionSchemaTests
```

Result:

```text
11 passed, 0 failed, 0 skipped
```

Covered positive and negative cases include source hashes, exact roster,
non-ordinal family mappings, source-only readiness, schema bounds, forbidden
executable fields, aliases, Forge/prototype exclusion, tree uniqueness,
milestone drift, exclusive healer ownership, mastery gating, Warmaster piece
duplication, optional/partial artifact rejection, source-revision drift,
coherent ID renaming, authored-prose drift, deterministic diagnostics, and
immutable publication.

### Catalog regression suite

Filter:

```text
AL.Tests.EditMode.GameDataCatalog
```

Result:

```text
58 passed, 0 failed, 0 skipped
```

This includes the existing catalog foundation and six-family schema suites.

### Complete EditMode regression suite

Result:

```text
575 passed, 0 failed, 0 skipped
```

This confirms repository-wide EditMode compilation and regression behavior for
the current checkout. PlayMode, device, build, install-size, and integrated
playtest checks were not performed by this pure non-wired slice.

## Optimization and device impact

- Runtime wiring: none.
- Per-frame CPU/GPU work: none.
- Scene or startup parsing: none.
- Save growth: none.
- Runtime asset memory: none added.
- VFX/audio/UI assets: none added.
- Build/install-size expectation: negligible contract IL and metadata; player
  stripping behavior was not measured.
- Device compatibility: no UnityEngine dependency and no new package dependency.
- Validation allocations occur only during bounded catalog validation, never in
  gameplay loops.

No device, build-size, install-size, or runtime-memory measurement was
performed because this slice cannot load or activate production records.

## Locks, issue, and PR status

- Shared-file locks: none.
- Designated shared files edited: none.
- Existing save-recovery edits were preserved and excluded from this slice.
- Upstream issue #183: open; this slice is non-wired schema/query evidence only.
- New issue: none.
- PR: none opened.
- Branch publication: not performed.

## Acceptance disposition

- Narrative source acceptance: recorded by the user's 2026-07-24 instruction
  for continued development; final creative approval remains user-owned.
- Engineering implementation: locally complete for the pure schema/validator
  slice.
- Generic and semantic validation: passing.
- Runtime integration: blocked by design.
- NVS-01 or milestone advancement: not claimed.
- Production/release acceptance: not claimed.

## Next mode

The next content step is narrative/content mode: author the complete general-tree
node and balance-candidate packet beneath these 48 branches while preserving the
80 milestones. That packet must define every node, node type, branch/core
ownership, prerequisite, rank, point cost, unlock policy, behavior reference,
presentation reference, and qualitative counterplay before a later coordination
handoff and executable engineering slice.
