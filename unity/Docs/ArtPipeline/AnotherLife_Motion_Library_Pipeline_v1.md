# AnotherLife Motion Library Pipeline v1

Status: bounded engineering motion library; not a production-admission or creative-approval artifact.

## Scope

This pipeline authors, measures, catalogs, and binds the required-motion sets for the three locked representatives:

| Family | Motion asset | Source rig | Coverage |
|---|---|---|---|
| Champion | `rmc_motion_asset_champion_vanguard_v001` | `champion_vanguard_rig_clean_v002.blend` | Required plus conditional Champion keys; also the catalog source for shared-humanoid clips. |
| NPC | `rmc_motion_asset_npc_covenant_sentinel_v001` | `covenant_sentinel_rig_clean_v003.blend` | Required plus conditional NPC keys bound to shared-humanoid clip IDs; NPC FBX is retarget-preview evidence only. |
| Fantasy beast | `rmc_motion_asset_slagwhistle_v001` | `slagwhistle_burrower_rig_clean_v002.blend` | Exactly six source-authorized presentation slots. Attack, special attack, defeat, burrow, standing rest, and any seventh clip remain blocked. |

Authority order:

1. `AnotherLife_Blender_Asset_Production_Contract.md`
2. `AnotherLife_Rig_Motion_Catalog_Production_Standard_v1.md`
3. `al_rig_motion_standard.json`
4. `al_required_motion_manifest.json`
5. `al_rig_cleanup_manifest.v1.json`
6. `al_motion_library_source.v1.json`

The library never promotes clips into a runtime-admitted catalog. Every asset and clip records `authorityState: bounded_engineering`, source/licensing evidence, supported skeleton profiles, and known restrictions. Owner visual, combat-readability, production-rights, and Unity round-trip acceptance remain separate gates.

## Entry point

Run from the repository root:

```text
python tools/blender/run_al_motion_library.py --mode validate
python tools/blender/run_al_motion_library.py --mode build
python tools/blender/run_al_motion_library.py --mode repeatability
```

`--mode repeatability` is the acceptance launcher: two isolated Blender passes, semantic signature comparison, catalog assembly, required-manifest binding update, and fail-closed validation. Use `--blender` when Blender 5.2 is not at the default Windows location.

## Binding and reuse

Clips bind through stable catalog identifiers of the form `rmc_clip_<family>_<motion_slug>_v001`. Import order, scene object names, and FBX take order are not authorities.

Shared-humanoid Champion and NPC overlapping keys reuse the same humanoid clip IDs. The NPC representative still receives anatomy-preview actions on its own cleaned rig so retarget compatibility can be inspected without duplicating catalog identity. Slagwhistle is exact-rig only (`slagwhistle_exact_anatomy_only`) and is not humanoid-retargetable.

## Root-motion and events

Motion rules in `al_motion_library_source.v1.json` choose in-place motor-owned treatment for locomotion, idle, combat, and interactions. Jump/fall/land use vertical root visual with horizontal motor ownership. Idle keys match `^idle\..+$` as looping cycles; a prefix-only `^idle\.` pattern is rejected because `re.fullmatch` would drop `idle.neutral` and `idle.variant` into the generic one-shot rule.

Every clip carries standardized gameplay events from the required-motion event vocabulary, plus hitbox windows for attack/commit/impact/release clips. Contact begin/end events name the representative contact bones.

## Cleanup gates

Blender measures loop closure, contact drift, and adjacent-frame transition deltas against:

- loop position 10 mm
- loop rotation 1 degree
- contact drift 20 mm
- transition position 30 mm
- transition rotation 6 degrees

`must_loop` clips must close pose continuity. Non-finite transforms fail closed. Catalog validation also rejects loop-policy mismatches and generator-style drift against the current source-plan rules.

## Determinism policy

Acceptance is `semantic_action_skeleton_event_cleanup_v1`. Two independent Blender passes must emit identical skeleton, action, event, and cleanup signatures. Blend and animated FBX bytes are retained from run one because Blender container metadata and sampled FBX IDs can vary while the semantic content stays identical.

## Generated evidence

- Source plan: `unity/ArtSource/MotionLibrary/al_motion_library_source.v1.json`
- Catalog: `unity/ArtSource/MotionLibrary/al_motion_library_catalog.v1.json`
- Repeatability receipt: `unity/ArtSource/MotionLibrary/al_motion_library_repeatability.v1.json`
- Representatives: `unity/ArtSource/MotionLibrary/Representatives/`
- Exports: `unity/ArtSource/MotionLibrary/Exports/`

Launcher validation reports three assets, zero catalog gaps, complete Champion/NPC/Slagwhistle mandatory bindings, and a passed semantic repeatability receipt.

## Intentional non-production gaps

This work does not claim:

- owner creative or combat-readability approval
- Champion or NPC production-rights clearance
- Unity import, Humanoid avatar, or PlayableGraph round-trip acceptance
- Slagwhistle attack, special attack, defeat, burrow, or standing-rest authorization
- replacement of these engineering samples with production performance capture

Closing any of those gates requires a separately reviewed task. Rerunning this pipeline cannot clear them.
