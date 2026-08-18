# Champion Vanguard — LOD + Rig Handoff (candidate)

Sidecar for `champion_vanguard_working_v001.blend`. Documents the merge, LOD
generation, Unity Humanoid rig, and anchor reconciliation performed by the rig task.

**Date:** 2026-08-18
**Status:** production candidate only — final rig, topology, UVs, textures, and
measured mobile performance require user approval before promotion.

## What this file contains

| Layer | Contents | Triangles |
| --- | --- | ---: |
| LOD0 (high) | 16 modular objects — 10 body (`SM_*`) + 6 equipment, in the 11 slot collections | 11,386 |
| LOD_Medium | per-module `*_LOD1` collapse-decimated copies | 4,480 |
| LOD_Low | per-module `*_LOD2` collapse-decimated copies | 1,084 |
| rig | `Champion_Vanguard_Rig` armature, 22 bones, Unity Humanoid names | — |

All LOD meshes are skinned to the shared armature (Armature modifier + distance-
weighted vertex groups, ≤ 4 influences per vertex). Collapse decimation preserves
vertex groups, so LOD1/LOD2 inherit the LOD0 skinning.

## Rig (Unity Humanoid bone list, 22 bones)

Hips → Spine → Chest → UpperChest → Neck → Head, plus
Left/Right: Shoulder, UpperArm, LowerArm, Hand, UpperLeg, LowerLeg, Foot, Toes.

Authored in the A-pose (arms at sides) matching the modeled neutral stance; Blender
-Z up maps to Unity +Y, and Blender -Y (front) maps to Unity +Z on FBX import.
Unity's Humanoid avatar auto-map recognizes these exact bone names.

## Anchor reconciliation

The scaffold anchors carried Unity Y-up coordinates copied verbatim from
`ProceduralChampionModelBuilder.cs` (a root-at-center placeholder). They are
re-parented to `root` and re-placed at anatomically correct Blender positions:

| Anchor | Blender (X, Y, Z) | Basis |
| --- | --- | --- |
| VFX_ChestAnchor | (0.00, -0.24, 1.34) | chest front (Realm_Ornament emblem) |
| VFX_Hand_L | (-0.34, -0.12, 0.95) | off-hand (Shield_Off grip) |
| VFX_Hand_R | (0.30, -0.05, 0.92) | main hand (Weapon_Main grip) |
| PetAnchor | (-0.95, 0.20, 0.62) | side-rear pet follow point |
| MountAnchor | (0.00, 0.00, 0.24) | center, mount saddle reference |

## Orientation / pivot / scale

- Root at world origin (0,0,0); feet/ground at Z=0.
- Character faces -Y (Blender) = +Z (Unity forward).
- Height ≈ 1.781 m (7.75-head adult).
- Units: Metric, scale 1.0 (1 Unity unit = 1 m).

## Reproducibility

- `tools/champion_vanguard_lod_rig_build.py` — merge + LOD + rig + anchors (re-opens
  the body file and appends equipment; see source paths at top).
- `tools/champion_vanguard_lod_rig_verify.py` — headless acceptance checks (all pass).
- `tools/champion_vanguard_lod_rig_render.py` — turntable + LOD captures.
