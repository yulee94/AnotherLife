# Slagwhistle LOD0 — Independent Validation Report vs A1 Budget

**Task:** t_ec244ffa — Write Slagwhistle validation report against handoff budget
**Date:** 2026-08-20
**Validator:** independent measurement (Blender 5.2.0 LTS headless + on-disk PNG/YAML/SHA). Fresh scripts — not the producer cleanup/verify modules.
**Contract:** `unity/Docs/Terrestrials/Ecosystems/SlagfallQuarryV002/Slagfall_Quarry_V002_A1_Technical_Handoff.md` § Slagwhistle Production Budget
**Candidates under review:**
- Blender working file + FBX/GLB/1K maps from merged PR #567 (`fc705580`)
- Unity prefab / material / scene instance from merged PR #569 (`855812c5`)
- Byte-identical authored files in worktrees `t_1690c393` and `t_bb2a487f` (SHA-256 match on blend/FBX/GLB/color/normal/packed)

Unity Editor was **not** re-launched in this task. Live skinned-renderer bounds / forward vector are cited from the producer import report and cross-checked against FBX importer YAML + an independent Blender FBX reimport. Everything else below was re-measured.

---

## 1. Verdict summary

| Measure | Contract | Measured | Verdict |
| --- | --- | --- | --- |
| LOD0 skinned triangles | 8,000–10,000; hard max 10,000 | **9,200** (blend + FBX; two count methods agree) | **PASS** |
| Deform bones | 34–42; hard max 42 | **38** deform / 38 total | **PASS** |
| Material slots | 1 preferred; 2 hard max | **1** (`M_Slagwhistle_LOD0`) | **PASS** |
| Texture set | one 1K color + normal + packed-mask | **3 × 1024×1024** authored PNGs | **PASS** (see derived-map note) |
| Core animation clips | 6 maximum | **0** | **PASS** (ceiling) / **GAP** (none authored) |
| Unique compressed content | target 3–4 MiB; hard max 7 MiB | **5.28 MiB** authored (FBX+3 maps); **6.51 MiB** if derived metallic-gloss is counted | **TARGET MISS** / hard max PASS |
| LOD1 | 55–60% of LOD0 | **absent** | **FAIL** |
| LOD2 | 20–25% of LOD0 | **absent** | **FAIL** |
| Distant / impostor | 6–8% or one authored opaque impostor | **absent** | **FAIL** |
| Required particles | 0 | **0** | **PASS** |
| Required dynamic lights | 0 | **0** | **PASS** |
| Orientation | Unity +Z / Blender −Y | Head bone ΔY = **−0.817 m**; marker empty `FORWARD_Unity+Z` | **PASS** |
| Scale | 1 unit = 1 meter | METRIC `scale_length=1.0`; extents **1.121 × 1.902 × 0.591 m**; FBX `globalScale=1` | **PASS** |
| Pivot | ground-center | `root` empty at (0,0,0); mesh zmin = **−0.0011 m** | **PASS** |
| Asset path | `…/Fauna/Slagwhistle/{Meshes,Materials,Textures,Animations,Prefabs}` | all five folders present on `main` | **PASS** |
| Direct scene reference | allowed; no runtime catalog; slice off Player builds | prefab instanced in `SlagfallQuarryRepresentativeSlice`; 0 GameData hits; slice not in `EditorBuildSettings` | **PASS** |
| Approved source PNGs | SHA-pinned, unmodified | identity + motion hashes match pin | **PASS** |

**Overall:** LOD0 numeric ceilings that this pilot actually shipped (tris, bones, materials, authored 1K set, clip *ceiling*, orientation / scale / pivot, path, no-catalog scene bind) **pass**. The budget *table* is not fully delivered: LOD1, LOD2, impostor, the 3–4 MiB size target, and the six presentation clips are missing. Sidecar GLB also leaked an 80-tri `Icosphere`.

This is a LOD0 candidate report, not production-slice acceptance.

---

## 2. Triangle counts (measured, not copied)

All blend/FBX counts use `len(loop_triangles)` after `calc_loop_triangles()` and the polygon fan `Σ(verts−2)`. Both methods agree.

| Source | Mesh | Verts | Tris | n-gons | Max influences |
| --- | --- | ---: | ---: | ---: | ---: |
| Working `.blend` | `SM_Slagwhistle_LOD0` | 8,691 | **9,200** | 0 | 4 (0 verts >4) |
| Runtime FBX reimport | `SM_Slagwhistle_LOD0` | 8,691 | **9,200** | 0 | 4 |
| Sidecar GLB reimport | `SM_Slagwhistle_LOD0` | 8,523 | **9,200** | 0 | 4 |
| Sidecar GLB reimport | **`Icosphere` (leak)** | 42 | **80** | 0 | 0 |

Unity runtime binds the **FBX**, not the GLB. Authoritative LOD0 cost is **9,200** (inside 8k–10k, under hard max 10k).

The GLB is not a clean LOD0-only export: it also contains a leftover `Icosphere` (80 tris, ~2 m AABB, no weights). Do not treat the GLB as the runtime mesh.

Producer Unity import report claimed `skinnedTriangles=9200`. Not re-run here; matches the independent FBX reimport.

---

## 3. Bones

Armature `Slagwhistle_Rig`, 38 edit-bones, all `use_deform=True`, single root `Root`.

`Root, Pelvis, Spine1, Spine2, Spine3, Chest, UpperChest, Neck, Head, Jaw, Yoke_L, Yoke_R, Shoulder_L, UpperArm_L, LowerArm_L, Palm_L, Stab1_L, Stab2_L, Shoulder_R, UpperArm_R, LowerArm_R, Palm_R, Stab1_R, Stab2_R, UpperLeg_L, LowerLeg_L, Foot_L, Toe_L, UpperLeg_R, LowerLeg_R, Foot_R, Toe_R, Tail1–Tail6`

38 is inside 34–42 and under hard max 42. Vertex groups = 38. FBX importer `maxBonesPerVertex: 4`.

---

## 4. Materials, textures, clips

**Materials:** 1 slot, `M_Slagwhistle_LOD0`. Unity Standard shader (`guid: 0000000000000000f000000000000000`) with `_NORMALMAP` + `_METALLICGLOSSMAP`.

**Authored 1K set** (PNG IHDR, not Unity import size):

| Map | Path | Px | PNG type | Bytes |
| --- | --- | --- | --- | ---: |
| color | `…/Textures/…_color_1k_v001.png` | 1024×1024 | RGB (type 2) | 1,918,696 |
| normal | `…/Textures/…_normal_1k_v001.png` | 1024×1024 | RGB (type 2) | 1,687,190 |
| packed | `…/Textures/…_packed_1k_v001.png` | 1024×1024 | **RGBA (type 6)** | 1,155,911 |

Blend packed images: `T_Slagwhistle_{Color,Normal,Packed}_1K` all 1024×1024.

Material YAML assigns:
- `_MainTex` → color GUID `5308ade6…`
- `_BumpMap` → normal GUID `ba40d16b…`
- `_OcclusionMap` → packed GUID `d1aecbdd…`
- `_MetallicGlossMap` → **derived** `…_metallicgloss_derived_1k_v001.png` (1,024×1,024 RGBA, 1,284,179 bytes)

The derived metallic-gloss is an engine binding of the packed set (producer: R + inverted B), not a second authored albedo/normal. It is still a fourth 1K PNG on disk.

**Clips:** 0 Blender actions, 0 NLA tracks. FBX `importAnimation: 0`, `clipAnimations: []`, `referencedClips: []`. `Animations/` contains only `.gitkeep`. Ceiling of 6 is not exceeded. The six authorized presentation moments (rest/vent, scurry, plant-stop, cut, spoil-push, turn) are **not authored**.

---

## 5. Unique compressed size

| Basket | Bytes | MiB | vs 3–4 target | vs 7 hard max |
| --- | ---: | ---: | --- | --- |
| FBX + color + normal + packed | 5,539,377 | **5.28** | over | under |
| same + derived metallic-gloss | 6,823,556 | **6.51** | over | under |

Blend (5.46 MiB) and GLB (5.24 MiB) are excluded: editable source and a duplicate export. Target miss is real; hard-max breach is not.

---

## 6. Orientation, scale, pivot

- Scene units: `METRIC`, `scale_length = 1.0`. Object scales all 1,1,1.
- `root` empty at world origin. Armature and mesh parented under it at origin.
- Mesh world AABB: X 1.121 m, Y 1.902 m, Z 0.591 m. zmin = **−0.0011 m** (float, not a floating mesh).
- Head bone world = (0.000, **−0.817**, 0.284) → snout points Blender −Y = Unity +Z.
- FBX importer: `globalScale: 1`, `useFileUnits: 1`, `useFileScale: 1`, `bakeAxisConversion: 1`.
- Prefab and scene instance both identity transform (pos 0, rot identity).

Producer Unity report: `forward=(0,0,1)`, `lossyScale=(1,1,1)`, skinned bounds `minY=-0.2428`. That minY is a **skinned AABB**, not the authored FBX ground (zmin −0.001). Flagged; not re-measured in-editor here.

Bind pose is **recumbent** (Meshy crop plant kept as bind). Not a standing rest. Outside the numeric budget table; honest pose gap.

---

## 7. Asset path, scene reference, catalog, builds

Required folders all exist on `main` (`855812c5`):

```
unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Fauna/Slagwhistle/
  Meshes/     tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.fbx (+ .glb)
  Materials/  M_Slagwhistle_LOD0.mat + derived metallic-gloss PNG
  Textures/   color / normal / packed 1K
  Animations/ .gitkeep only
  Prefabs/    tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.prefab
unity/Assets/AL/Scenes/Prototype/Terrestrials/SlagfallQuarryRepresentativeSlice.unity
```

- Prefab GUID `10414d1e3afe5ec43be0ad68ec4336ce` is instanced in the slice (12 GUID hits, name `Slagwhistle_Burrower_LOD0`). Direct scene reference, as authorized.
- `unity/Assets/AL/StreamingAssets/GameData`: **0** hits for `slagwhistle` / `tdf_fauna_stonehold_slagwhistle`. No runtime catalog record.
- `EditorBuildSettings.asset` enabled scenes: `Boot`, `RealmSelection`, `Kingdom` only. Slice **not** in Player builds.

**Landing state:** cleanup PR #567 and Unity import PR #569 are both MERGED to `main`. Raw Meshy PR #566 may still be OPEN; the cleanup FBX supersedes it as the working LOD0 source.

---

## 8. Approved source pins (unmodified)

| Sheet | Pinned SHA-256 | On-disk | Bytes |
| --- | --- | --- | ---: |
| identity v002 | `1a08581e…20eaa05` | match | 2,521,039 |
| motion/contact v002 | `10999370…4ff2d92` | match | 2,617,228 |

Concept sheets were not copied into the Player art folder. This check does **not** rescore A2 silhouette/anatomy (wedge skull, yoke, shovel+2 stabilizers, brace tail). Producer cleanup notes already say the side preview still reads somewhat mole/armadillo — that is an A2 question, not a budget-table pass.

---

## 9. Every deviation / gap

1. **LOD1 missing** — contract 55–60% of LOD0. No file, no object.
2. **LOD2 missing** — contract 20–25% of LOD0. No file, no object.
3. **Distant / impostor missing** — contract 6–8% or one opaque impostor. Handoff also requires full/medium/low/impostor on the representative slice; missing cheap tiers is a stated reject-registration failure.
4. **Unique compressed 5.28 MiB** (6.51 with derived map) vs 3–4 MiB target. Under 7 MiB hard max.
5. **0 of 6 presentation clips** — ceiling pass, content gap. `Animations/` is empty.
6. **Sidecar GLB leaked `Icosphere` (80 tris).** Runtime FBX is clean. Do not register the GLB as the LOD0 mesh.
7. **Fourth 1K PNG** (derived metallic-gloss) lives next to the authored set. Binding, not a new authored family; still extra bytes.
8. **Packed map is RGBA**, not RGB. Extra alpha is unused vs a 3-channel packed-mask.
9. **Recumbent bind pose** — not an unposed / standing rest.
10. **Unity skinned AABB minY ≈ −0.24** (producer) vs authored zmin −0.001. Skinned bounds, not a floating pivot, but not independently re-measured here.
11. **No EditMode/PlayMode Terrestrials validators.** Handoff § Automated Evidence Required is unmet. `Tests/EditMode/Terrestrials` and `Tests/PlayMode/Terrestrials` do not exist.
12. **Protected identity / grayscale / 96–32 px captures / device profiling** were not validated. Numeric budget only.

---

## 10. What this report is not

- Not a 90/100 production safety score. Handoff says production remains unscored until all six evidence dimensions exist.
- Not an A2 fidelity acceptance. Anatomy was not rescored against the identity sheet.
- Not a claim that the representative slice is complete (habitat families, LOD stack, tests, device runs are out of this card).

---

## 11. Evidence artifacts

| File | Role |
| --- | --- |
| `tools/slagwhistle_independent_blender_validate.py` | fresh Blender 5.2 measurement (blend + FBX + GLB) |
| `tools/slagwhistle_independent_validate.py` | fresh PNG IHDR / SHA / YAML / catalog / build-settings audit |
| `tdf_fauna_stonehold_slagwhistle_burrower_independent_blender.json` | raw Blender numbers |
| `tdf_fauna_stonehold_slagwhistle_burrower_independent_disk.json` | raw disk/YAML numbers |

Producer self-reports (`cleanup_metrics.json`, `unity_import_report.json`) were read for landing state and the Unity bounds claim only. Counts in §2–§5 come from the independent scripts.
