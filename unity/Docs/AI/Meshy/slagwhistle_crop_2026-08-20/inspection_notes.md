# Slagwhistle crop mesh — inspection notes

Task: `t_1e666a71`  
Provider task: `01a01c64-08f4-7626-9d9b-718d5f3688ce`  
A1: `a1_slagwhistle_slagfall_fauna_20260820_crop_v001`

## Verdict

**ACCEPT.** One recumbent burrower. Not a sheet-diorama, not multiple animals, not a scale-human.

## Traceability

| Item | Value |
|---|---|
| Approved identity PNG (unmodified) | `unity/Docs/Terrestrials/Ecosystems/SlagfallQuarryV002/ConceptSheets/tdf_fauna_stonehold_slagwhistle_burrower_identity_v002.png` SHA-256 `1a08581ef2a49d56f3e3b5a9925a88ee7eebcb6df2895de61691f74b820eaa05` |
| Meshy input (existing crop, not recropped) | `tdf_fauna_stonehold_slagwhistle_burrower_identity_side_crop_v001.png` 704×336, bbox `(16,24,720,360)`, SHA-256 `efc77b574a757266dd8e19ce7284b6ed2edff27b998d1a59051c2cd01f4a8698` |
| GLB | `…/Meshes/tdf_fauna_stonehold_slagwhistle_burrower_meshy6_crop_raw_v001.glb` 22815020 B, header declared 22815020, SHA-256 `03d4958f6a889d315c8da28d7c0b9d492622b74c9461d3243fcd28c3c20c2a1e` |
| FBX | `…/Meshes/tdf_fauna_stonehold_slagwhistle_burrower_meshy6_crop_raw_v001.fbx` 33738844 B, SHA-256 `3a72d17627cedddbbb6b924d46fe4240be9336ca636a1aa2e2fdeaa09b21170b` |

## Blender 5.2 import (raw GLB)

- meshes: 1 (`Mesh_0`)
- verts: 251163
- tris: **471338** (raw Meshy sculpt; LOD0 hard max 10000 is owned by cleanup `t_1690c393`)
- other objects: none (no armature, no extras)
- bounds XYZ: `(1.119, 1.899, 0.590)` meters
- center: `(-0.001, 0.001, -0.002)` — origin at volume center, not ground-center
- Z range: `-0.297 … 0.293` (straddles the ground plane)

## Identity features present

- Wedge skull, no external ears
- Two vent-fold bracket yoke plates
- Forefeet: fused shovel palm + two stabilizer claws
- Flattened brace tail
- Recumbent / belly-down pose matching the side crop

## Artifacts / cleanup notes (for `t_1690c393`)

1. **Polycount.** 471k tris / 251k verts. Organic triangulated sculpt. Decimate / retopo to 8k–10k (hard max 10k). Do not rerun Meshy for this.
2. **No bones.** Raw mesh only. Rig 34–42 deform bones after retopo. Recumbent pose is not a T/A-pose; cleanup must un-pose or retopo in a bind pose.
3. **Orientation.** Long axis is **Y** (~1.90 m). Head faces roughly **−Y** in the imported GLB. Unity contract is **forward +Z**, 1 unit/m, **ground-center pivot**. Rotate and re-pivot.
4. **Ground.** No fused ground slab (good vs the rejected sheet). Mesh floats around Z=0; raise so belly/feet sit on Z=0 after pivot.
5. **Topology.** Dense uneven sculpt, not edge-looped. Expect fused pads, soft claw sockets, and baked-in recumbent deformation. Retopo; do not keep this cage.
6. **Materials / textures.** Meshy PBR (base color / metallic / roughness / normal / emission) is embedded. Contract is 1–2 materials and one 1K color+normal+packed set — rebuild in cleanup, do not call Meshy retexture.
7. **Rejected prior mesh.** `01a0190e-d06a-7189-bd11-5395b9de97a9` was a 714k-tri identity-board relief. Truncated worktree copies of that object are **not** committed and must not be imported.

## Stop rules honored

- One A1 one-shot; no second POST
- No remesh / rig / retexture / convert
- Source PNGs unmodified
- Stop-on-non-single-burrower did not fire
