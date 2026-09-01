# Slagwhistle Blender cleanup — t_1690c393

Candidate built from the crop-retry Meshy GLB (single recumbent burrower).
Source identity PNG was not modified. No Meshy remesh/rig/retexture/convert.

## Contract vs actual

| Measure | Contract | Actual | Result |
|---|---|---|---|
| LOD0 tris | 8000–10000 (hard max 10000) | 9200 | PASS |
| Deform bones | 34–42 (hard max 42) | 38 | PASS |
| Materials | 1 preferred, 2 hard max | 1 | PASS |
| Texture set | one 1K color+normal+packed | 3×1024 | PASS |
| Animation clips | max 6 | 0 | PASS |
| Pivot | ground-center | root (0,0,0), zmin=-0.0011 | PASS |
| Facing | Unity +Z / Blender -Y | Blender -Y, FBX -Z/+Y | PASS |
| Scale | 1 unit/m | metric scale 1.0, length 1.9016 m | PASS |

## Bind pose

The Meshy crop is a recumbent plant. This candidate keeps that pose as the
bind pose. It was not un-posed into a standing rest — that would invent
silhouette the approved crop does not show.

## Animation

Zero clips authored. The six-clip ceiling is reserved for later presentation
moments (rest/vent, scurry, plant-stop, cut, spoil-push, turn). This task
does not invent motion.

## Texture packing

- color: Meshy baseColor downsampled 2K → 1K
- normal: Meshy tangent normal downsampled 2K → 1K
- packed: R=metallic, G=occlusion, B=roughness

## Protected identity (not rescored here)

Wedge skull, no external ears, two vent-fold yoke plates, fused shovel palm
+ two stabilizer claws per forefoot, flattened brace tail. Decimate keeps
the sculpt silhouette; it is not a new retopo cage.

## Honest deviations / later work

- LOD1 / LOD2 / impostor were not authored (out of this card).
- Unique compressed FBX+1K maps = 5.28 MiB (over 3–4 MiB target, under 7 MiB hard max).
- Ground zmin after armature parent is −0.001 m (float), not a floating mesh.
- Recumbent bind is still a folded plant; a standing rest would be a new A2 pose.
- Side preview still reads somewhat mole/armadillo — that silhouette is the Meshy crop, not a cleanup invention.

Independent reopen of the working blend: 9200 tris, 38 deform bones, 1 material, 0 actions, packed images 1024×1024.

Blend: `C:\Users\MY\Documents\AnotherLife\.worktrees\t_1690c393\unity\ArtSource\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\tdf_fauna_stonehold_slagwhistle_burrower_working_v001.blend`
FBX: `C:\Users\MY\Documents\AnotherLife\.worktrees\t_1690c393\unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Meshes\tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.fbx`
GLB: `C:\Users\MY\Documents\AnotherLife\.worktrees\t_1690c393\unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Meshes\tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.glb`
