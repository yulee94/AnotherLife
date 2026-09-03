# Stonehold concept packets v001

**Task:** `t_a4734797`

**Created:** 2026-09-03

**Owner status:** `PENDING`

**Generation:** `HELD`

**Activation:** `HELD`

**ComfyUI:** not used (Local versus Cloud is unresolved)

**Meshy / 3D:** not authorized by this packet set

This set maps every world-asset taxonomy family (`242/242`) to a versioned
Stonehold concept packet and an honest approval state. It compiles already
approved AnotherLife sources. It does not invent missing silhouettes, materials,
or floor plans, and it does not treat BDO or Infinity Kingdom as copyable
authority.

## Owner actions

Return **APPROVE**, **REVISE**, or **REJECT** per packet below. Also choose
**ComfyUI Local** or **ComfyUI Cloud** before any new concept images are
generated.

Do not treat a packet APPROVE as Meshy, runtime, save, or release permission.

## Packets

| Packet ID | Families | Owner ruling now | What is already approved | What stays OPEN |
| --- | ---: | --- | --- | --- |
| `environment_stonehold_natural_ecology_v001` | 37 | PARTIAL | DESIGN.md grammar; Slagfall soil/ash/water-edge (`waf_terrain_surface_soil_loam`, `waf_terrain_surface_ash_slag_obsidian`, `waf_terrain_water_edge_module`) | Realm-wide flora/ground look, harvest plants, trees |
| `environment_stonehold_geology_minerals_crystals_v001` | 17 | PARTIAL | Slagfall eight-family kit (profiling-scale candidates) | Dimensions, nav, crystals, mineables, non-Slagfall geology |
| `architecture_stonehold_settlement_silhouettes_v001` | 6 | PENDING | Four-realm settlement/landmark sheets (directional) | Per-family world-space settlement kits |
| `traversal_stonehold_roads_bridges_v001` | 9 | PENDING | Taxonomy widths (6 m / 4 m / 4 m spans) | Stonehold road/bridge look |
| `architecture_stonehold_enterable_structures_v001` | 34 | PARTIAL | Shared civic-hall and fort-gatehouse 2D spatial; kingdom Workshop binding | Stonehold exteriors, castle keep, remaining buildings. Routed to `t_c748138b` |
| `architecture_stonehold_exterior_interior_floorplan_v001` | 21 | PARTIAL | Shared civic-hall and fort-gatehouse plans/sections | Castle-keep and other building plans |
| `prop_stonehold_interior_decor_v001` | 65 | PENDING | Civic furniture zones named, not modeled as families | Every prop/decor family |
| `derivative_stonehold_kingdom_strategic_v001` | 15 | PENDING | Kingdom 2.5D directional sheets | Per-family 2.5D derivatives (after 3D identity) |
| `ecosystem_stonehold_habitats_v001` | 9 | PENDING | Slagfall habitat master + four roster habitats | Non-Slagfall habitat sheets; fauna/monster production |
| `supporting_stonehold_technical_v001` | 29 | PENDING | Non-creative helpers | Not a visual approval |

Coverage registry: `stonehold_concept_packet_coverage_v001.json`
Decision packet: `rct_stonehold_decision_concept_lane_v001.md`

Declared totals in the registry must stay `familyRecords=242`, `mapped=242`,
`unmapped=0`, `meshyAuthorized=0`.

## Already-approved evidence (do not re-approve as new)

- Slagfall Quarry eight environment families, owner-approved 2026-08-31, hash-bound in `unity/Docs/AI/Meshy/meshy_execution_slagfall_environment_2026-08-31_v001.json`. Scope is family identity, silhouette, material read, and profiling-scale Unity presentation. Not architecture. Not final dimensions/navigation/placement.
- Shared civic-hall and fort-gatehouse 2D spatial/modular authority, owner-approved 2026-09-01, PR #664. Stonehold and Umbral civic-hall exteriors are explicitly outside that package.
- Stonehold Workshop kingdom production binding and modular workshop detail sheet. Not a world-space environment kit.
- DESIGN.md Stonehold structural identity: defensive mass, compression, buttresses, layered plates, practical forge construction; basalt / dark iron / aged steel / soot; charcoal, iron brown, ash, restrained forge amber. Avoid “dwarf” pastiche, orange on every edge, identical blocky silhouettes.

## Hard holds

- No ComfyUI until the owner chooses Local versus Cloud.
- No Meshy/model production from this lane.
- Enterable architecture remaining 2D work stays on `t_c748138b`.
- Unresolved creative choices stay `OPEN` / `PENDING`.
- Benchmarks are directional only.

## Honest recon (this worker)

Processed, not claimed unread:

- `al_world_asset_inventory.json` entire `familyRecords` array, 242 records, via Python parse (732,245 bytes).
- DESIGN.md: source-of-truth, realm identity matrix, AI handoff contract, review gates (approx. lines 1–150, 350–500, 623–722, 918–983 of 983).
- Taxonomy v1 sections 1–7 (approx. lines 1–200 of 551) plus inventory-driven family IDs for the remainder.
- Civic-hall / fort-gatehouse manifests and civic layout JSON; Slagfall execution record; Four-realm architecture Stonehold grammar; catalog binding ID rules; habitat profile Stonehold roster.

Not claimed as read end-to-end: remaining taxonomy tables, remaining DESIGN.md implementation-constraint pages, every habitat source packet body.

Corpus note: this is a bounded in-process kanban recon of the authority needed for packets, not a whole-repo coverage audit.
