# environment_stonehold_geology_minerals_crystals_v001

**Packet ID:** `environment_stonehold_geology_minerals_crystals_v001`  
**Catalog families:** 17  
**Owner status:** `PARTIAL` (Slagfall eight-family kit only)  
**Generation / activation:** `HELD`  
**Category:** environment  
**Realm:** `stonehold`  
**Requested-by:** `t_a4734797`

## 1. Decision identity

**Question:** Confirm that the Slagfall eight-family kit remains the only
owner-approved Stonehold geology production candidates, and keep crystals,
mineables, cave-tunnel modules, and non-Slagfall geology at `PENDING`.

**Already approved and cannot change here:**

- 2026-08-31 owner decision: Approve all eight Slagfall families as profiling-scale Unity production candidates (identity, silhouette, material read).
- Hash-bound sources in `meshy_execution_slagfall_environment_2026-08-31_v001.json`.
- Explicitly **not** approved by that decision: final dimensions, navigation, gallery entry gameplay, placement density, combat/rewards/saves/audio/release.

**Still undecided:**

- Realm-wide boulder/cliff/cave language outside Slagfall Quarry.
- Crystal formation vs magical crystal node look.
- Mineable ore/stone interaction presentation.
- Cave tunnel / cavern room modules.
- ComfyUI Local versus Cloud for any additive 2D.

## 2. Required brief

| Field | Value |
| --- | --- |
| Purpose | Cover, landmark geology, quarry identity, harvest nodes |
| Scale | OPEN for final meters. Existing Slagfall meshes are profiling-scale, not locked dimensions. |
| Camera use | Gameplay + Unity lineup review |
| Primary silhouette | Approved only for the eight Slagfall families listed below |
| Construction | Fractured basalt, worked quarry, fault slabs, talus, gallery mouths |
| Materials | Slagfall atlases (basecolor/normal/metallic-smoothness) for those eight only |
| Palette | Charcoal, iron brown, ash, mineral inclusions |
| Magic / VFX | Not baked into clean meshes. Crystal emission-off readability required by taxonomy |
| Required views | Slagfall Unity lineup `slagfall_environment_kit_unity_lineup_v003.png` plus per-family source 2D. Front/side/back model sheets for non-Slagfall families: absent |
| Runtime tier | Four LODs, shared Standard PBR atlases, lowest-LOD static colliders (Slagfall kit contract) |
| Accessibility | Walkable vs non-walkable ledges need non-color cues (taxonomy); not yet locked |
| Exclusions | Architecture from quarry rocks; orange-edge lava default; copied franchise crystals |
| Provenance | Owner-approved Meshy image-to-3D + Blender cleanup; no new generation in this lane |

## 3. Slagfall eight families (PARTIAL_APPROVE)

| Slagfall family | Taxonomy mapping (this packet, not exclusive) | Source 2D SHA-256 |
| --- | --- | --- |
| `irregular_fracture_raft` | `waf_geology_boulder` | `03f50d970f17d07c56d8d248bd1b712757229d5753b8303be94df06a7edbab39` |
| `broken_fracture_raft` | `waf_geology_boulder` | `c638a084867d76c4feeea82c6fe307fccc2bff62c5f08ad474781fed08225113` |
| `undercut_extraction_ledge` | `waf_geology_ledge_overhang` | `91912bd6c01e3b4fbdb13af46a3c9673f5aa70a2417dc6692b9415ec45e48b29` |
| `talus_apron` | `waf_geology_scree_rubble` | `e552ef57980d8f1a5453a40e4e43f8cdd3263e896b0b9a42a127e05d20b362fc` |
| `collapsed_gallery_mouth` | `waf_geology_cave_entrance` | `1322c7f999f42cef2aa834070f82c7245ea358c84817b17a78964f45e84c35a3` |
| `diagonal_fault_slab` | `waf_geology_cliff_face` | `3f9e12e7dc8953a22f8e74aa0af8b6ad2be47edbbd944fcc389604279246960a` |
| `braided_runoff_pool` | cited on natural-ecology water-edge; geology adjacency only | `f01641719ea80e64ea642523e7331c19d1ea24b914d6c3bd8d31501fc67b441c` |
| `iron_soil_wedge` | cited on natural-ecology soil; geology adjacency only | `76401766197de5deb4df23b3fea122c581bc6aa69b563815aa82091ef060e5f9` |

Also tagged partial (read only, not extra meshes): `waf_geology_rock_scatter`,
`waf_geology_mine_quarry_dressing`.

Unity lineup SHA-256:
`4f5a554df85e1104f01c971eb3cf5c9a1f83e95ac86459e61bfe49a72130038e`

## 4. Still PENDING inside this packet

`waf_geology_cave_tunnel_module`, `waf_geology_cavern_room_landmark`,
`waf_geology_crystal_formation`, `waf_geology_magical_crystal_node`,
`waf_geology_mineable_ore_node`, `waf_geology_ore_vein_dressing`,
`waf_geology_stone_mineable_node`, `waf_harvestable_ore`,
`waf_harvestable_stone`, `waf_harvestable_magical_crystal`.

No silhouettes invented for these.

## 5. Modular dimensions / gameplay / mobile

- Snap 0.5 m authoring grid is architecture authority, not geology scale lock.
- Final terrain/prop dimensions remain OPEN (Slagfall decision text).
- Mineable available/targeted/depleted/locked states are gameplay-owned.
- Mobile: keep shared atlases; do not unique-texture every rock; far proxy required for cliffs/boulders.

## 6. Avoid

- Treating Slagfall approval as architecture or as all-Stonehold geology.
- Baking lightning/smoke/crystals-as-VFX into clean meshes.
- Generic gemstone marketplace crystals.
- Copying BDO/IK ore nodes.

## 7. Owner ruling

Select `APPROVE` (accept this partial mapping), `REVISE`, `REJECT`, or keep
`PENDING` for the non-Slagfall remainder.

Recommended: APPROVE the mapping of the eight Slagfall families as
`owner_approved_partial_slagfall_only`; keep crystals/mineables/tunnels PENDING.
Meshy remains unauthorized.
