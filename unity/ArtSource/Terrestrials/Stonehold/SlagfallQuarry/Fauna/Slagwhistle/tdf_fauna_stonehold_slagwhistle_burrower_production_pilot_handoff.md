# Slagwhistle production pilot — root handoff (t_b8b483e2)

**Date:** 2026-08-20
**Role:** synthesis root. Children already produced the candidate; this card independently
re-measured the landed files and records the honest budget table.
**Contract:** `unity/Docs/Terrestrials/Ecosystems/SlagfallQuarryV002/Slagfall_Quarry_V002_A1_Technical_Handoff.md`
§ Slagwhistle Production Budget.
**Unity Editor:** not re-launched here. Live skinned AABB / forward are cited from the
merged Unity import report (`t_bb2a487f` / PR #569). Everything else below was
re-measured on this root from `origin/main` bytes.

## Landing state

| Child | Work | PR | State |
| --- | --- | --- | --- |
| t_bb00cee7 | A1 source/spec brief; SHA-pinned PNGs verified | — | complete (docs only) |
| t_1e666a71 | Meshy image-to-3d crop retry (raw 471k tris) | #566 | OPEN, hygiene FAIL (trailing whitespace). Superseded as runtime source by #567 |
| t_1690c393 | Blender cleanup to LOD0 contract | #567 | MERGED `fc705580` |
| t_bb2a487f | Unity prefab + direct slice instance | #569 | MERGED `855812c5` |
| t_ec244ffa | Independent A1 budget report | #570 | MERGED `f86d53fc` |

Deliverables required by this root:

| Deliverable | Path | Status |
| --- | --- | --- |
| Blender candidate | `unity/ArtSource/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/tdf_fauna_stonehold_slagwhistle_burrower_working_v001.blend` | on `main` |
| Unity prefab | `unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/Prefabs/tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.prefab` | on `main` |
| Validation report | `…/tdf_fauna_stonehold_slagwhistle_burrower_validation_report.md` (PR #570) | on `main` |

No runtime catalog record. Slice is a direct scene reference and is **not** in
`EditorBuildSettings` (Boot / RealmSelection / Kingdom only).

## Root re-measure vs A1 table

Blender 5.2.0 LTS headless (`tools/slagwhistle_root_blender_measure.py`) plus
on-disk SHA/PNG/YAML (`tools/slagwhistle_root_disk_audit.py`). Two triangle
methods (`loop_triangles` and polygon fan) agree.

| Measure | Contract | Root measured | Verdict |
| --- | --- | --- | --- |
| LOD0 skinned triangles | 8,000–10,000; hard max 10,000 | **9,200** (blend + FBX) | **PASS** |
| Deform bones | 34–42; hard max 42 | **38** / 38 total, all deform | **PASS** |
| Material slots | 1 preferred; 2 hard max | **1** (`M_Slagwhistle_LOD0`) | **PASS** |
| Texture set | one 1K color + normal + packed | **3 × 1024×1024** authored PNGs | **PASS** (derived metallic-gloss extra PNG) |
| Core animation clips | 6 maximum | **0** | **PASS** ceiling / **GAP** content |
| Unique compressed | target 3–4 MiB; hard max 7 MiB | **5.28 MiB** authored (FBX+3 maps); **6.51 MiB** with derived map | **TARGET MISS** / hard max **PASS** |
| LOD1 | 55–60% of LOD0 | absent | **FAIL** |
| LOD2 | 20–25% of LOD0 | absent | **FAIL** |
| Distant / impostor | 6–8% or one opaque impostor | absent | **FAIL** |
| Required particles | 0 | 0 | **PASS** |
| Required dynamic lights | 0 | 0 | **PASS** |
| Facing | Unity +Z / Blender −Y | Head world Y = **−0.817 m** | **PASS** |
| Scale | 1 unit = 1 meter | METRIC `scale_length=1.0`; extents 1.121 × 1.902 × 0.591 m | **PASS** |
| Pivot | ground-center | `root` at origin; mesh zmin = **−0.0011 m** | **PASS** |
| Asset path | `…/Fauna/Slagwhistle/{Meshes,Materials,Textures,Animations,Prefabs}` | all five present | **PASS** |
| Direct scene bind | allowed; no catalog; slice off Player builds | prefab GUID `10414d1e3afe5ec43be0ad68ec4336ce` instanced as `Slagwhistle_Burrower_LOD0`; 0 GameData hits | **PASS** |
| Approved source PNGs | SHA-pinned, unmodified | identity + motion + habitat hashes match pins | **PASS** |

Authoritative runtime mesh is the **FBX** (9,200 tris). Sidecar GLB totals **9,280**
because it still contains a leaked 80-tri `Icosphere`. Do not register the GLB.

Byte identity with the PR #570 independent disk audit (FBX
`e7c69c4fd9ab0b1c1a3a7f267e9716a3958199bdbaa03c5c0e305eb1c5664afa`, blend
`493e44350356d9d10d72cde21aa8b2df0298d096dfbc61c4827efd40b7e987d1`).

## Visual / pose (not a budget-table score)

Side and three-quarter previews in `preview/` were inspected on this root.

- Bind pose is **recumbent** (Meshy crop plant kept). Not a standing rest.
- Wedge skull, no external ears, compact hindquarters, short flattened brace tail,
  soot-brown hide and dark keratin claws are present.
- Shoulder keratin reads as plate-like armor; the two-fold vent yoke is not a
  clean closed bracket in these stills.
- Side silhouette still has a mole / armadillo read. That is an A2 fidelity
  question, not a numeric pass. Production must not treat this preview as
  identity acceptance.

Unity import report (not re-run): `forward=(0,0,1)`, `lossyScale=(1,1,1)`,
skinned bounds `minY=-0.2428` (skinned AABB, not authored FBX ground).

## Explicitly unmet vs the full A1 slice

This root accepts a **LOD0 prototype**, not representative-slice completion.

1. No LOD1 / LOD2 / impostor. Handoff failure table says missing cheap tiers
   reject registration.
2. 0 of 6 presentation clips (`Animations/` is `.gitkeep` only).
3. Unique compressed 5.28 MiB over the 3–4 MiB target (under 7 MiB hard max).
4. No EditMode / PlayMode Terrestrials validators.
5. No habitat prop families, no 128 m review cell greybox, no device profiling.
6. PR #566 raw Meshy mesh is not on `main` (hygiene fail). Cleanup FBX is the
   working LOD0 source; raw 471k mesh remains in worktree `t_1e666a71` only.

## What this is not

- Not a 90/100 production safety score.
- Not A2 anatomy acceptance.
- Not a runtime catalog bind. Downstream `t_13ba4fba` must leave fauna out of
  scope until a fauna catalog exists.

## Evidence

| File | Role |
| --- | --- |
| `tools/slagwhistle_root_blender_measure.py` | this root's Blender 5.2 measure |
| `tools/slagwhistle_root_disk_audit.py` | this root's SHA / PNG / catalog / build audit |
| `tdf_fauna_stonehold_slagwhistle_burrower_root_blender.json` | raw Blender numbers |
| `tdf_fauna_stonehold_slagwhistle_burrower_root_disk.json` | raw disk numbers |
| `tdf_fauna_stonehold_slagwhistle_burrower_validation_report.md` | child t_ec244ffa report (agrees) |
