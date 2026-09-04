# environment_stonehold_natural_ecology_v001

**Packet ID:** `environment_stonehold_natural_ecology_v001`

**Catalog families:** 37 (see coverage registry)

**Owner status:** `APPROVE` (concept direction, 2026-09-03; Slagfall soil/ash/water-edge remain production-partial)

**Generation / activation:** `HELD`

**Category:** environment

**Realm:** `stonehold`

**Requested-by:** `t_a4734797`

## 1. Decision identity

Map Stonehold vegetation, ground surfaces, water edges, and plant harvestables
to owner-reviewable concept authority without inventing a realm-wide flora kit.

**Question:** Which of these ecology families, if any, should inherit the
Slagfall Quarry material read as a temporary production candidate, and which
must wait for new 2D sheets after ComfyUI Local versus Cloud is chosen?

**Owner answer (2026-09-03):** APPROVE this packet as planning/concept direction.
ComfyUI Local is chosen. Missing flora sheets stay absent. Slagfall soil/ash/
water-edge remain `PARTIAL_APPROVE` production-partial evidence only.

**Already approved and cannot change here:**

- DESIGN.md Stonehold grammar (defensive mass, basalt/iron/soot, restrained forge amber; no dwarf pastiche).
- Slagfall `iron_soil_wedge` and `braided_runoff_pool` as profiling-scale candidates only.
- Mystical medieval naturalism; no franchise copies; no palette-swap realm identity.

**Still undecided:**

- Canopy/understory tree species and silhouettes for Stonehold.
- Grass, shrub, moss, fungi, crop, vine, and harvest-plant looks.
- Whether snow/ice, shore, or paving families need Stonehold-specific sheets now or later.
- New ComfyUI Local flora sheets (none generated in this lane).

## 2. Required brief (DESIGN.md)

| Field | Value |
| --- | --- |
| Asset ID | `environment_stonehold_natural_ecology_v001` |
| Purpose | Traversal-safe cover, harvest readability, and realm ground identity |
| Approval state | Exploration / selected-concept incomplete |
| Scale | Unity meters; Champion ~1.8 m; trees OPEN; grass <0.6 m intent from taxonomy |
| Camera use | Gameplay + far proxy; no cinematic lock |
| Primary silhouette | OPEN per family. Do not default all Stonehold plants to blocky stumps. |
| Construction | Habitat and function before decoration. Magic only as named mineral/heat accent. |
| Materials | Directional: basalt dust, iron soil, soot-stained bark, dry lichen. Exact swatches OPEN. |
| Palette | Charcoal, iron brown, ash, small mineral accents. Color is not the only cue. |
| Magic / VFX | None baked into meshes. Runtime VFX separate. Reduced-motion: static impostors. |
| Required views | Front / side / back / 3/4, silhouette, grayscale, LOD — **absent** except Slagfall soil/water-edge sources |
| Animation | Wind response OPEN; reduced-motion mesh required by taxonomy for grass/reeds |
| Runtime tier | Mobile floor 30 FPS; atlas/impostor; no unique material per clump |
| Accessibility | Non-color harvest/depleted states; no color-only interaction |
| Exclusions | Cartoon plants; generic AI jungle; Eldergrove root-as-default; orange edge glow; copied BDO/IK flora |
| Evidence | DESIGN.md; taxonomy §§4–5; Slagfall execution JSON |
| Provenance | Human compilation 2026-09-03 from approved docs. No new AI images. |
| Open decisions | Listed above |

## 3. Modular dimensions (known vs OPEN)

| Token | Value | Authority |
| --- | --- | --- |
| Authoring snap | 0.5 m | Civic-hall 2D package (shared grid, not ecology approval) |
| Champion scale | 1 Unity unit = 1 m | DESIGN.md |
| Tree height / canopy radius | OPEN | Do not invent |
| Grass clump footprint | OPEN | Taxonomy requires short/tall, sparse/dense |
| Harvest node contact | OPEN | Gameplay catalog later |

## 4. Intended gameplay

Cover, route read, harvestable/depleted/regrown states, and biome dressing.
Harvest results stay in gameplay catalogs. This packet does not set yields.

## 5. Variants

Young/mature/damaged/stump/far proxy for trees; planted/growing/mature/harvested
for crops; harvestable/depleted for plants, flowers, fungi, deadwood. Seasonal
variants remain OPEN.

## 6. Avoid

- Using Slagfall quarry rock as a tree or grass stand-in.
- Palette-only Stonehold identity.
- Emissive moss as default magic.
- Calling fauna “animals”; later ecosystem work is fantasy beasts.
- Copying BDO/Infinity Kingdom vegetation.

## 7. Mobile simplification

Impostors and atlas for far grass/trees; reduced-motion meshes; no per-instance
unique shaders; protect harvest/depleted silhouette at lowest LOD.

## 8. Family mapping

All 37 `environment_stonehold_natural_ecology_v001` rows in
`stonehold_concept_packet_coverage_v001.json`.

Partial evidence only:

- `waf_terrain_surface_soil_loam` ← Slagfall `iron_soil_wedge`
- `waf_terrain_surface_ash_slag_obsidian` ← Slagfall iron-soil material read only
- `waf_terrain_water_edge_module` ← Slagfall `braided_runoff_pool`

Those three are `PARTIAL_APPROVE` for Slagfall presentation, not realm-wide
family approval.

## 9. Owner ruling

**Recorded 2026-09-03:** `APPROVE` as planning and concept direction. ComfyUI Local
chosen. Slagfall soil/ash/water-edge stay `PARTIAL_APPROVE`. No Meshy. No new
images in this lane.
