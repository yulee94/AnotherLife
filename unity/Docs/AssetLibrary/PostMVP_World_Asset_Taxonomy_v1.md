# Post-MVP World-Asset Taxonomy v1

**Status:** exhaustive logical inventory and ID reservation; `HELD` for production
**Task:** `t_f68a3fe0`
**Runtime/content impact:** none; this document creates no asset, catalog payload, schema, prefab, scene, source model, or runtime binding
**Final creative authority:** project owner / creative director

## 1. Boundary and authority

This document is the logical-family input to the future catalog assembly task. It
uses the source-role map in
`PostMVP_World_Asset_Authority_Reconciliation_v1.md`; it does not replace any
narrow authority named there. In particular:

- canonical realm IDs remain `crownlands`, `stonehold`, `eldergrove`, and
  `umbral` from `al_realm_catalog.json`;
- dimension, world, chunk, and replacement-socket identity remains in
  `al_world_streaming_catalog.json`;
- gameplay building identity remains in `buildings.json`; structured art binding
  remains in `al_building_catalog.json`; current runtime admission remains in
  the existing Resources catalogs;
- the current first-session terrain is `mvp_procedural_replaceable` and stays
  bound exactly as-is;
- the sixteen `tdf_habitat_*` records are source-review habitat identities, not
  approved biome IDs or production terrain;
- ecosystem-specific fantasy beasts and monsters remain separate, later-scope
  source programs. They are fantasy beasts, never generic environment props;
- the live Gate 0 stop-ship remains separate. Every record below is held from
  generation and activation until its dependencies and owner decisions pass.

No category may infer a biome, narrative meaning, gameplay behavior, prefab,
Addressable, budget, or approval from a folder name or this reservation.

## 2. Stable catalog-ID namespace

### 2.1 ID kinds

All canonical IDs are lowercase ASCII snake case and immutable after publication.

| Kind | Grammar | Purpose |
| --- | --- | --- |
| Logical family | `waf_<domain>_<family>` | Stable identity used by this taxonomy. A semantic change creates a new family; versions do not rename a family. |
| Kit | `wak_<context>_<domain>_<kit>_v###` | Reserved future modular-kit identity. |
| Runtime/source-independent asset | `wa_<context>_<domain>_<family>_<descriptor>_v###` | Reserved future production identity. It never embeds a path, approval word, LOD, platform, or file extension. |
| 2.5D derivative | `wad_<source_asset_token>_<view_or_state>_v###` | A separately approved derivative that references, but never replaces, its 3D source. |
| Alias | exact legacy/source string | Non-canonical lookup key mapped one-to-one to a canonical asset ID. |

`context` is exactly one of `shared`, `neutral`, `crownlands`, `stonehold`,
`eldergrove`, `umbral`, `kingdom_crownlands`, `kingdom_stonehold`,
`kingdom_eldergrove`, `kingdom_umbral`, or an owner-approved event token such as
`event_accordant_isle`. Biome tokens are prohibited until a canonical biome
catalog is approved.

Each family row reserves this collision-free descendant prefix:

```text
wa_<context>_<domain>_<family-token>_*
```

The `<domain>_<family-token>` suffix is the family ID with `waf_` removed. A
future asset may use exactly one family prefix. Canonical IDs, family IDs, kit
IDs, derivative IDs, and aliases must each be unique; an alias may not equal any
canonical ID. Case folding, punctuation normalization, and last-write-wins are
forbidden. Records sort bytewise by canonical ID, aliases sort by exact alias,
and arrays use the owning catalog's canonical order.

### 2.2 Known binding reservations and aliases

The eight existing Town Hall and Workshop bindings keep their paths, GUIDs,
hashes, and runtime authority. The future inventory reserves these canonical
identities only; it does not rebind them.

| Canonical reservation | Exact aliases retained |
| --- | --- |
| `wa_crownlands_architecture_building_town_hall_base_v001` | `building_crownlands_town_hall_production_v1`; `building.crownlands.townhall.production.v1` |
| `wa_stonehold_architecture_building_town_hall_base_v001` | `building_stonehold_town_hall_production_v1`; `building.stonehold.townhall.production.v1` |
| `wa_eldergrove_architecture_building_town_hall_base_v001` | `building_eldergrove_town_hall_production_v1`; `building.eldergrove.townhall.production.v1` |
| `wa_umbral_architecture_building_town_hall_base_v001` | `building_umbral_town_hall_production_v1`; `building.umbral.townhall.production.v1` |
| `wa_crownlands_architecture_building_workshop_base_v001` | `building_crownlands_workshop_production_v1`; `building.crownlands.workshop.production.v1` |
| `wa_stonehold_architecture_building_workshop_base_v001` | `building_stonehold_workshop_production_v1`; `building.stonehold.workshop.production.v1` |
| `wa_eldergrove_architecture_building_workshop_base_v001` | `building_eldergrove_workshop_production_v1`; `building.eldergrove.workshop.production.v1` |
| `wa_umbral_architecture_building_workshop_base_v001` | `building_umbral_workshop_production_v1`; `building.umbral.workshop.production.v1` |

Source-validation IDs such as `neutral-covenant-hall-working-v001`,
`neutral-covenant-terrain-landmark-kit-working-v001`, and
`slagwhistle-burrower-working-v001` remain source references, not aliases, until
an exact source-to-runtime derivation is approved. `tdf_*` habitat and fantasy-
beast IDs likewise remain external design/source references.

## 3. Record legend

Every row is a logical family record with the required fields: applicability,
gameplay purpose, variants, owner authority, status, dependencies, and schedule.

Realm cells:

- `R` — a distinct realm-authored variant is required.
- `S` — one shared implementation applies in this realm; realm identity must not
  be added by an uncontrolled palette swap.
- `0:<reason>` — explicitly not applicable.

Owner codes: `OWNER` project owner/final creative; `WORLD` world design/topology;
`ARCH` architecture; `GAME` gameplay/system authority; `NARR` narrative/content;
`TA` technical art; `ECO` terrestrial/ecosystem source; `UXA` UX/accessibility;
`ENG` runtime/streaming engineering.

Status codes: `CURRENT_MVP_REPLACEABLE`, `CURRENT_BOUND_PARTIAL`,
`SOURCE_ONLY`, `LOGICAL_RESERVED`, `DEFERRED_UNAPPROVED`.

Dependency codes: `REALM` realm catalog; `TOP` world-streaming topology; `BIO`
canonical biome taxonomy (currently unresolved); `BLD` building catalogs;
`MVP` current-binding fingerprint/regression; `SRC` approved source packet and
provenance; `BUD` approved variant/performance/readability budget; `GAME`
gameplay contract; `NARR` narrative/cultural decision; `ACC` accessibility and
readability evidence; `TECH` schema/import/runtime validation; `DEVICE`
physical mobile-floor evidence; `GATE` owner and release gates.

Schedules are exactly `launch`, `post_mvp`, or `later_ecosystem`. `launch` means
preserve or complete an already named launch/MVP family; it is not permission to
replace the approved MVP.

## 4. Terrain surfaces, water, and decals

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_terrain_surface_macro_landform` | Base readable ground and horizon mass | R | R | R | R | flat, slope, steep, distant/HLOD; transition edges | OWNER+WORLD+TA | LOGICAL_RESERVED | REALM,TOP,BIO,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_terrain_surface_bedrock` | Exposed structural rock ground | R | R | R | R | intact, fractured, weathered, wet; far reduction | OWNER+WORLD+TA | SOURCE_ONLY | BIO,SRC,BUD,TECH,GATE | post_mvp |
| `waf_terrain_surface_soil_loam` | Soil, loam, tilled and compacted ground | R | R | R | R | dry, damp, disturbed, compacted | OWNER+WORLD+TA | SOURCE_ONLY | BIO,SRC,BUD,TECH,GATE | post_mvp |
| `waf_terrain_surface_gravel_scree` | Loose rock and slope breakup | R | R | R | R | fine gravel, scree, talus, compact trail | OWNER+WORLD+TA | SOURCE_ONLY | BIO,SRC,BUD,TECH,GATE | post_mvp |
| `waf_terrain_surface_grass_ground` | Turf/meadow ground below grass meshes | R | R | R | R | short, worn, wet/dry, sparse transition | OWNER+WORLD+TA | SOURCE_ONLY | BIO,SRC,BUD,TECH,GATE | post_mvp |
| `waf_terrain_surface_mud_silt` | Wetland, drainage and shoreline footing | R | R | R | R | damp, saturated, cracked, tracked | OWNER+WORLD+TA | SOURCE_ONLY | BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_terrain_surface_snow_ice` | Cold ground and safe/unsafe ice read | R | R | R | R | snow crust, drift, exposed ice, melt edge; non-specular cue | OWNER+WORLD+TA | SOURCE_ONLY | BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_terrain_surface_ash_slag_obsidian` | Heat/ash/geologic altered ground | R | R | R | R | ash, cooled slag, glass face, active-fracture edge without emission dependency | OWNER+WORLD+TA | SOURCE_ONLY | BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_terrain_surface_paving_floor` | Exterior civic, ruin and settlement paving | R | R | R | R | intact, worn, broken, edge/corner | OWNER+ARCH+TA | LOGICAL_RESERVED | ARCH,SRC,BUD,TECH,GATE | post_mvp |
| `waf_terrain_surface_shore_waterbed` | Dry shore, shallows and submerged bed | R | R | R | R | dry, wet, shallow, deep-value boundary | OWNER+WORLD+TA | SOURCE_ONLY | BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_terrain_water_surface` | Rivers, lakes, flooded basins, channels | R | R | R | R | still, flow, shallow, deep, low-motion/static fallback | OWNER+WORLD+TA | SOURCE_ONLY | TOP,BIO,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_terrain_water_edge_module` | Banks, reedsockets, foam/contact boundaries | R | R | R | R | straight, inner/outer curve, inlet, drop, dry fallback | OWNER+WORLD+TA | LOGICAL_RESERVED | BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_terrain_decal_material_transition` | Blend adjacent surface families | R | R | R | R | linear, patch, corner, multi-surface junction | TA | LOGICAL_RESERVED | BIO,BUD,TECH,GATE | post_mvp |
| `waf_terrain_decal_erosion_drainage` | Explain runoff, wash, cracks and deposition | R | R | R | R | flowline, puddle rim, sediment, erosion scar | OWNER+WORLD+TA | LOGICAL_RESERVED | BIO,SRC,BUD,TECH,GATE | post_mvp |
| `waf_terrain_decal_wetness_stain` | Contact, weather and waterline history | R | R | R | R | waterline, drip, splash, damp patch; opaque fallback | TA | LOGICAL_RESERVED | BUD,TECH,DEVICE,GATE | post_mvp |
| `waf_terrain_decal_wear_tracks` | Road, foot, wheel and work wear | R | R | R | R | foot, cart, drag, quarry/mine, construction | WORLD+GAME+TA | LOGICAL_RESERVED | GAME,BUD,ACC,TECH,GATE | post_mvp |
| `waf_terrain_decal_damage_debris` | Scorch, impact, rubble and repair read | R | R | R | R | old/new damage, repaired, battle-safe, low-clutter | GAME+OWNER+TA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_terrain_decal_route_marking` | Non-color route, boundary and hazard cue | R | R | R | R | route edge, hazard, objective-safe marker, disabled/reduced-effects | GAME+UXA+OWNER | LOGICAL_RESERVED | GAME,NARR,BUD,ACC,TECH,GATE | post_mvp |

The first-session procedural courtyard maps only to
`waf_terrain_surface_macro_landform` and `waf_terrain_surface_paving_floor` as a
preserved `launch` implementation reference; it does not approve the post-MVP
families above or supply biome identity.

## 5. Vegetation and organic world dressing

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_vegetation_tree_canopy` | Major tree/canopy identity and cover | R | R | R | R | young, mature, ancient/landmark where approved, damaged, stump, far proxy | OWNER+WORLD+ECO | SOURCE_ONLY | BIO,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_vegetation_tree_understory` | Small trees and saplings | R | R | R | R | sapling, juvenile, wind-pruned, dead; clustered instancing | OWNER+WORLD+ECO | LOGICAL_RESERVED | BIO,SRC,BUD,TECH,GATE | post_mvp |
| `waf_vegetation_grass_groundcover` | Traversal-safe grass and groundcover | R | R | R | R | short/tall, sparse/dense, wet/dry; reduced-motion mesh | OWNER+WORLD+ECO | SOURCE_ONLY | BIO,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_vegetation_crop_grain` | Farms, fields and harvest-readable crops | R | R | R | R | planted, growing, mature, harvested/stubble, damaged | GAME+OWNER+ECO | LOGICAL_RESERVED | BLD,GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_vegetation_plant_herb` | Common plants and herb harvest sources | R | R | R | R | non-harvestable, harvestable, depleted, regrown; at least three silhouette groups | GAME+OWNER+ECO | LOGICAL_RESERVED | GAME,BIO,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_vegetation_flower` | Flower accents and approved harvest sources | R | R | R | R | sparse patch, focal cluster, harvestable/depleted; no color-only interaction | GAME+OWNER+ECO | LOGICAL_RESERVED | GAME,BIO,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_vegetation_shrub_hedge` | Cover edge, field boundary and scrub | R | R | R | R | low/high, sparse/dense, clipped/wild, damaged | OWNER+WORLD+ECO | SOURCE_ONLY | BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_vegetation_reed_aquatic` | Wetland, shore and drainage structure | R | R | R | R | reed, broadleaf, submerged, dry remnant; reduced-motion | OWNER+WORLD+ECO | SOURCE_ONLY | BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_vegetation_fern_frond` | Forest/wet understory masses | R | R | R | R | small, large, sparse clump, damaged | OWNER+WORLD+ECO | SOURCE_ONLY | BIO,SRC,BUD,TECH,GATE | post_mvp |
| `waf_vegetation_vine_climber` | Wall, ruin, trunk and hanging growth | R | R | R | R | wall, ground, hanging, dead/dry; collision-free | OWNER+WORLD+ECO+ARCH | LOGICAL_RESERVED | BIO,ARCH,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_vegetation_fungi_mushroom` | Cave/forest fungi and harvest sources | R | R | R | R | crust, shelf, cap cluster, harvest/depleted; non-emissive base | GAME+OWNER+ECO | SOURCE_ONLY | GAME,BIO,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_vegetation_moss_lichen_biofilm` | Surface-age and ecology transition | R | R | R | R | moss mat, crust lichen, wet biofilm, mineral crust | OWNER+ECO+TA | SOURCE_ONLY | BIO,SRC,BUD,TECH,GATE | post_mvp |
| `waf_vegetation_root_structural` | Root walls, ramps, arches and shoreline shelves | R | R | R | R | ground, wall, buttress, bridge/ramp, broken; traversal review | OWNER+WORLD+ECO | SOURCE_ONLY | TOP,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_vegetation_deadwood` | Fallen logs, branches, stumps and decomposing wood | R | R | R | R | log, stump, branch scatter, hollow, wet/dry, harvestable/depleted | GAME+OWNER+ECO | SOURCE_ONLY | GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |

## 6. Rocks, cliffs, caves, ores, mineables, and crystals

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_geology_rock_scatter` | Pebble-to-small-rock dressing | R | R | R | R | pebble, small, medium; clustered/instanced; wet/dry | OWNER+WORLD+ECO | SOURCE_ONLY | BIO,SRC,BUD,TECH,GATE | post_mvp |
| `waf_geology_boulder` | Cover, landmark support and breakup | R | R | R | R | small/medium/large, fractured, embedded, far proxy | OWNER+WORLD+ECO | SOURCE_ONLY | BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_geology_cliff_face` | Impassable edge and horizon structure | R | R | R | R | base, mid, top, inner/outer corner, transition, HLOD | OWNER+WORLD+ECO | SOURCE_ONLY | TOP,BIO,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_geology_ledge_overhang` | Traversal/cover ledges and overhangs | R | R | R | R | walkable, non-walkable, overhang, drop edge; non-color cue | WORLD+GAME+ECO | LOGICAL_RESERVED | TOP,GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_geology_scree_rubble` | Talus, collapse and quarry dressing | R | R | R | R | fine, coarse, collapse fan, worked spoil | OWNER+WORLD+ECO | SOURCE_ONLY | BIO,SRC,BUD,TECH,GATE | post_mvp |
| `waf_geology_cave_entrance` | Readable cave threshold | R | R | R | R | open, collapsed, gated, wet/dry, far silhouette | OWNER+WORLD+ECO | SOURCE_ONLY | TOP,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_geology_cave_tunnel_module` | Modular cave traversal | R | R | R | R | straight, bend, slope, junction, choke, transition | WORLD+GAME+ECO | LOGICAL_RESERVED | TOP,GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_geology_cavern_room_landmark` | Cave chamber, lair and objective space | R | R | R | R | small/medium/hero chamber, entrance/descent/lair sockets, HLOD | OWNER+WORLD+GAME+ECO | LOGICAL_RESERVED | TOP,GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_geology_ore_vein_dressing` | Non-mineable ore/geology identity | R | R | R | R | common/rare visual strata, exposed/cut, depleted-looking noninteractive | OWNER+ECO | SOURCE_ONLY | BIO,NARR,SRC,BUD,TECH,GATE | post_mvp |
| `waf_geology_mineable_ore_node` | Authoritative resource interaction | R | R | R | R | available, targeted, depleted, regenerating/locked; tool contact | GAME+OWNER+ECO | LOGICAL_RESERVED | GAME,NARR,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_geology_stone_mineable_node` | Quarryable stone resource | R | R | R | R | available, targeted, depleted, regenerated/locked | GAME+OWNER+ECO | LOGICAL_RESERVED | GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_geology_crystal_formation` | Non-interactive magical/mineral landmark | R | R | R | R | cluster, seam, monolith, broken; readable emission-off | OWNER+ECO+NARR | LOGICAL_RESERVED | BIO,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_geology_magical_crystal_node` | Magical resource/objective interaction | R | R | R | R | dormant, available, active, depleted, corrupted only if narrative-approved | GAME+OWNER+NARR+ECO | LOGICAL_RESERVED | GAME,NARR,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_geology_mine_quarry_dressing` | Worked extraction environment | R | R | R | R | supports, tailings, spoil, track, winch base, safety barriers | ARCH+WORLD+ECO | LOGICAL_RESERVED | BLD,GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |

## 7. Roads, bridges, walls, gates, and traversal pieces

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_traversal_road_primary` | Six-meter principal travel route | R | R | R | R | straight, bend, intersection, grade, damaged, realm-to-shared transition | WORLD+GAME+ARCH | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_road_service` | Four-meter local/service route | R | R | R | R | straight, bend, T/X junction, grade, edge | WORLD+GAME+ARCH | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_trail_path` | Natural/local foot route | R | R | R | R | open, wooded, cave, shore, steep-safe; reduced-density cue | WORLD+GAME+ECO | SOURCE_ONLY | TOP,GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_road_edge_curb` | Road shoulder, ditch, curb and verge | R | R | R | R | hard/soft edge, ditch, corner, crossing | WORLD+ARCH+ECO | LOGICAL_RESERVED | TOP,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_bridge_modular` | Road-width bridge in four-meter spans | R | R | R | R | service/primary width, 1–N spans, ends, supports, damaged | OWNER+WORLD+ARCH+GAME | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_traversal_bridge_natural` | Root, log, rock or erosion crossing | R | R | R | R | narrow/wide, intact/broken, bypass-readable | OWNER+WORLD+ECO+GAME | SOURCE_ONLY | TOP,GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_bridge_rope_suspension` | Bounded flexible crossing | R | R | R | R | short/long, intact/damaged, static low-motion fallback | OWNER+WORLD+GAME+TA | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_traversal_ford_culvert` | Shallow-water/drainage crossing | R | R | R | R | ford, culvert, stepping route, flooded/closed state | WORLD+GAME+ECO | LOGICAL_RESERVED | TOP,GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_stair_step` | Exterior elevation connection | R | R | R | R | straight, landing, turn, broken/blocked; 1 m tier compatible | ARCH+WORLD+GAME | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_ramp_slope` | Accessible cart/player elevation connection | R | R | R | R | short/long, switchback, natural/constructed, blocked | ARCH+WORLD+GAME+UXA | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_ladder_climb` | Authored vertical traversal | R | R | R | R | fixed, mine, siege/service, unavailable/blocked | GAME+ARCH+UXA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_platform_walkway` | Elevated floor, catwalk and dock-like walkway | R | R | R | R | straight, corner, junction, railing/no-railing, damaged | ARCH+WORLD+GAME | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_wall_fortification` | Impassable main/defensive wall | R | R | R | R | 4 m bay, inner/outer corner, end, damaged, HLOD; never climbable by appearance alone | OWNER+ARCH+GAME | CURRENT_BOUND_PARTIAL | TOP,BLD,GAME,MVP,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_fence_barrier` | Soft boundary and local enclosure | R | R | R | R | straight, corner, end, opening, damaged | ARCH+WORLD+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_gate_main_teleport` | Main-wall interaction that teleports across an impassable wall | R | R | R | R | exterior/interior face, open/closed/disabled/broken, interaction and destination sockets | OWNER+GAME+ARCH+UXA | SOURCE_ONLY | REALM,TOP,GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_gate_breakable_war` | Opposing-realm attackable gate state | R | R | R | R | intact, damaged bands, breached, repaired, unavailable; non-color health read | OWNER+GAME+ARCH+UXA | LOGICAL_RESERVED | TOP,GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_traversal_gate_local_doorway` | Non-main gate for settlements/forts | R | R | R | R | small 4 m, major 8 m, open/closed/locked/damaged | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_traversal_teleport_pad_portal_anchor` | Non-wall teleport/wishgate destination presentation | R | R | R | R | dormant, available, channeling, unavailable; physical read without VFX | OWNER+GAME+NARR+UXA | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |

## 8. Enterable architecture and general structures

All `building_*` families require four realm variants because the gameplay
catalog declares all-realm eligibility. Empty model arrays are explicit missing
bindings, not zero demand.

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_architecture_castle_enterable` | Realm castle/capital defensive-civic complex | R | R | R | R | exterior kit, gatehouse, enterable keep/public halls, courtyard, service, damaged/HLOD | OWNER+ARCH+WORLD | LOGICAL_RESERVED | REALM,TOP,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_architecture_fortress_enterable` | Warzone/realm fortress objective | R | R | R | R | wall/gate/towers, enterable command/service spaces, intact/damaged/breached/HLOD | OWNER+ARCH+GAME | LOGICAL_RESERVED | TOP,GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_architecture_city_capital_kit` | Enterable capital/city districts and skyline | R | R | R | R | civic, service, residential, market, defensive, transition and HLOD kits | OWNER+ARCH+WORLD | LOGICAL_RESERVED | REALM,TOP,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_architecture_settlement_village_kit` | Town/village composition below capital scale | R | R | R | R | center, residential, service, farm edge, road transition, HLOD | OWNER+ARCH+WORLD | LOGICAL_RESERVED | TOP,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_town_hall` | Gameplay Town Hall/civic anchor | R | R | R | R | levels 0–10, LOD0–3, settled/construction/unavailable; existing eight aliases preserved | OWNER+ARCH | CURRENT_BOUND_PARTIAL | BLD,MVP,SRC,BUD,ACC,TECH,DEVICE,GATE | launch |
| `waf_architecture_building_workshop` | Gameplay Workshop/service production | R | R | R | R | levels 0–10, LOD0–3, settled/construction/unavailable; existing aliases preserved | OWNER+ARCH | CURRENT_BOUND_PARTIAL | BLD,MVP,SRC,BUD,ACC,TECH,DEVICE,GATE | launch |
| `waf_architecture_building_farm` | Gameplay farm structure and yard | R | R | R | R | levels 0–10, fields/service modules, construction/unavailable | OWNER+ARCH | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_lumber_mill` | Gameplay lumber processing | R | R | R | R | levels 0–10, log yard/saw/service modules | OWNER+ARCH | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_quarry` | Gameplay quarry structure | R | R | R | R | levels 0–10, cut/yard/processing/service modules | OWNER+ARCH | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_gold_mine` | Gameplay gold-mine entrance/service | R | R | R | R | levels 0–10, entrance/headworks/service modules | OWNER+ARCH | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_barracks` | Gameplay military housing/training | R | R | R | R | levels 0–10, muster/training/storage modules | OWNER+ARCH | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_academy` | Gameplay research/training civic building | R | R | R | R | levels 0–10, teaching/research/archive modules | OWNER+ARCH+NARR | LOGICAL_RESERVED | BLD,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_market` | Gameplay market structure | R | R | R | R | levels 0–10, covered/open stalls, storage/service | OWNER+ARCH | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_storehouse` | Gameplay storage/logistics building | R | R | R | R | levels 0–10, loading/storage/guard modules | OWNER+ARCH | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_forge` | Gameplay forge/smithy | R | R | R | R | levels 0–10, hearth/workfloor/storage/vent modules | OWNER+ARCH | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_stable` | Gameplay stable and yard | R | R | R | R | levels 0–10, stalls, tack/feed, yard modules | OWNER+ARCH | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_embassy` | Gameplay diplomatic/civic building | R | R | R | R | levels 0–10, public/private/records/service modules | OWNER+ARCH+NARR | LOGICAL_RESERVED | BLD,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_wall` | Gameplay private-kingdom wall definition | R | R | R | R | levels 0–10, bays/corners/ends/condition states | OWNER+ARCH | LOGICAL_RESERVED | BLD,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_building_watchtower` | Gameplay watchtower definition | R | R | R | R | levels 0–10, base/platform/roof/crown states | OWNER+ARCH | LOGICAL_RESERVED | BLD,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_dwelling` | General enterable residence | R | R | R | R | small/medium/affluent, intact/damaged, furnished/unfurnished | OWNER+ARCH+NARR | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_inn_tavern` | Public rest/social/service structure | R | R | R | R | common room, rooms, kitchen/service, stable option | OWNER+ARCH+NARR | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_shop_service` | Enterable retail/service shell | R | R | R | R | small/medium, open/closed, rear storage/workroom | OWNER+ARCH | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_warehouse_barn` | General bulk storage/agricultural shell | R | R | R | R | warehouse, barn, shed, loading/yard variants | OWNER+ARCH | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_mill_wind_water` | Landscape production structure | R | R | R | R | wind/water only where geography permits; active/static/damaged | OWNER+ARCH+WORLD | LOGICAL_RESERVED | TOP,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_guardpost_watch` | Small defensive/service post | R | R | R | R | road, gate, wall, warzone; staffed/empty/damaged | ARCH+GAME | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_well_fountain_cistern` | Water-service landmark | R | R | R | R | well, cistern, fountain only where culture/geography approves; working/dry/damaged | OWNER+ARCH+NARR | LOGICAL_RESERVED | BIO,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_religious_cultural_structure` | Shrine, chapel, cathedral, sanctum or cultural hall | R | R | R | R | exact function/name/ritual held; exterior/interior/ruin only after narrative approval | OWNER+ARCH+NARR | DEFERRED_UNAPPROVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_ruin_structure` | Traversable ruined settlement/fort/civic overlay | R | R | R | R | wall, room, tower, arch, floor, rubble transitions; no invented history | OWNER+ARCH+NARR+WORLD | LOGICAL_RESERVED | TOP,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_architecture_event_accordant_isle` | Event-only island castle/cavern architecture | 0:event-only | 0:event-only | 0:event-only | 0:event-only | surface, castle, four entrances, cavern descent, Wish Dragon cavern, bridge ring | OWNER+ARCH+NARR+WORLD | LOGICAL_RESERVED | TOP,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |

## 9. Interior room and construction modules

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_interior_shell_wall_floor_ceiling` | Reusable enterable room shell | R | R | R | R | wall, floor, ceiling, corners, openings, damaged/cutaway backing | ARCH+TA | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_door_window_threshold` | Openings and transitions | R | R | R | R | door/window, frame, shutter, locked/broken, interior/exterior trim | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_entry_vestibule` | Public/private entry module | R | R | R | R | civic, service, residential, defensive | ARCH+NARR | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_corridor_junction` | Circulation | R | R | R | R | straight, turn, T/X, threshold, service passage | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_stair_landing` | Multi-level circulation | R | R | R | R | straight, return, spiral only if accessible, landing, blocked | ARCH+GAME+UXA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_great_council_hall` | Civic/assembly room | R | R | R | R | small/hero, occupied/empty, council/public layouts | OWNER+ARCH+NARR | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_throne_royal_hall` | Royal/authority room | R | R | R | R | audience, dais, side/service; exact institution held for narrative | OWNER+ARCH+NARR | DEFERRED_UNAPPROVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_barracks_room` | Military sleeping/muster | R | R | R | R | bunk, muster, officer, gear-storage | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_bedroom_living` | Residential/private room | R | R | R | R | common, affluent, guest, damaged/abandoned | ARCH+NARR | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_kitchen_dining` | Food preparation and dining | R | R | R | R | household, inn, institutional, service | ARCH+NARR | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_storage_pantry_cellar` | Storage/service | R | R | R | R | dry, food, armory, archive, cellar | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_market_shop` | Interior retail | R | R | R | R | counter, display, stockroom, closed state | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_forge_workshop` | Crafting/work room | R | R | R | R | forge, general workshop, clean/active/cold/damaged | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_library_archive` | Books, records and knowledge | R | R | R | R | public/private/forbidden only if narrative-approved, damaged | ARCH+NARR | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_guild_hall` | Guild meeting/service room | R | R | R | R | meeting, contract, trophy, officer/service | ARCH+GAME+NARR | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_religious_cultural_room` | Shrine/chapel/ritual/cultural interior | R | R | R | R | exact room use, symbols and ritual held | OWNER+ARCH+NARR | DEFERRED_UNAPPROVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_prison_dungeon` | Detention/underground service | R | R | R | R | cell, corridor, guard, service, damaged/abandoned | ARCH+GAME+NARR | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_mine_cave_room` | Worked underground interior | R | R | R | R | gallery, junction, extraction, support, collapse, transition to natural cave | ARCH+WORLD+GAME | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_utility_service_room` | Heating, water, maintenance and loading | R | R | R | R | plant/service, wash, loading, refuse | ARCH | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_courtyard_balcony` | Interior/exterior transition | R | R | R | R | courtyard, gallery, balcony, roof access; safe railing variants | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interior_cutaway_occlusion_set` | Complete camera-visible backing/cutaway | S | S | S | S | roof, canopy, upper wall, restored backing, selected/unselected groups | ARCH+TA+UXA | LOGICAL_RESERVED | BUD,ACC,TECH,DEVICE,GATE | post_mvp |

## 10. Furniture and prop classes

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_prop_seating_chair_stool` | Common seating | R | R | R | R | chair, stool, high/low status, damaged | OWNER+ARCH | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_seating_bench_pew` | Shared/public seating | R | R | R | R | bench, backed, communal/ceremonial only if narrative-approved | OWNER+ARCH+NARR | LOGICAL_RESERVED | NARR,SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_surface_table_desk` | Work, dining and records surfaces | R | R | R | R | small/large, dining, writing, council, damaged | OWNER+ARCH | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_sleep_bed_cot_bunk` | Residential/military sleeping | R | R | R | R | bed, cot, bunk, made/unmade, damaged | OWNER+ARCH | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_storage_shelf_bookcase` | Open vertical storage | R | R | R | R | shelf, bookcase, scroll rack, empty/filled modules | ARCH+NARR | LOGICAL_RESERVED | NARR,SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_storage_cabinet_cupboard` | Closed storage | R | R | R | R | low/high, doors/drawers, open/closed/damaged | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_storage_chest_coffer` | Portable valuable storage | R | R | R | R | common/secure/royal, closed/open/locked/looted | GAME+OWNER+ARCH | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_storage_crate_barrel_sack_basket` | Bulk logistics and clutter | R | R | R | R | crate, barrel/cask, sack, basket; sealed/open/empty/damaged | ARCH+TA | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_lighting_candle_lamp` | Small practical light source | R | R | R | R | candle, oil lamp, unlit/lit/spent; no gameplay cue by light alone | OWNER+ARCH+TA | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_prop_lighting_lantern_sconce` | Portable/wall practical light | R | R | R | R | handheld/hanging/wall, lit/unlit/damaged | OWNER+ARCH+TA | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_prop_lighting_brazier_hearth_fireplace` | Large practical heat/light | R | R | R | R | brazier, hearth, fireplace, lit/embers/cold; reduced-effects fallback | OWNER+ARCH+TA | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_prop_lighting_chandelier_hanging` | Large interior fixture | R | R | R | R | civic/common/ceremonial, lit/unlit, static low-motion | OWNER+ARCH | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_prop_market_stall_canopy` | Market/vendor shell | R | R | R | R | open/closed, corner/inline, stocked/empty, weathered | OWNER+ARCH | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_market_counter_display` | Goods presentation | R | R | R | R | counter, display table/rack, empty/stocked | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_market_scale_till` | Trade-role signaling | R | R | R | R | scale, weights, lockbox/till; interactive only by contract | GAME+ARCH | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_forge_hearth_anvil` | Smithing focal equipment | R | R | R | R | forge hearth, anvil, hot/cold, damaged | OWNER+ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_prop_forge_bellows_quench` | Smithing support equipment | R | R | R | R | bellows, quench trough, grindstone, active/static | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_forge_tool_rack` | Smithing tools and storage | R | R | R | R | wall/floor rack, hammer/tong sets, filled/empty | ARCH | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_kitchen_oven_cookfire` | Cooking heat source | R | R | R | R | oven, cookfire, range, lit/cold/damaged | ARCH+NARR | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_prop_kitchen_cauldron_cookware` | Cooking vessels | R | R | R | R | cauldron, pot, pan, kettle; empty/filled | ARCH | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_kitchen_prep_pantry` | Food preparation/storage | R | R | R | R | prep table, pantry rack, drying rack, clean/used | ARCH | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_kitchen_dishes_utensils` | Table/kitchen small props | R | R | R | R | plates, bowls, cups, cutlery/tools; atlas/cluster sets | ARCH | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_military_weapon_rack` | Armory/muster signaling | R | R | R | R | melee/ranged racks, filled/empty, secured | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_military_armor_stand` | Armor storage/display | R | R | R | R | common/officer/ceremonial, filled/empty | OWNER+ARCH | LOGICAL_RESERVED | NARR,SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_military_training_dummy_target` | Training interaction/read | R | R | R | R | melee dummy, archery target, intact/damaged/reset | GAME+ARCH+UXA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_military_siege_ammunition` | Siege staging/read | R | R | R | R | stones, bolts, shot crates, spent/intact; no active weapon behavior | GAME+ARCH | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_military_war_map_table` | Command/strategy focal prop | R | R | R | R | table, map board, markers, idle/active only by contract | GAME+NARR+ARCH | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_royal_throne_dais` | Royal authority furniture | R | R | R | R | throne/seat, dais, audience layout; exact institution held | OWNER+NARR+ARCH | DEFERRED_UNAPPROVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_royal_council_lectern` | Civic/royal proceedings | R | R | R | R | council table, lectern/podium, records desk | OWNER+NARR+ARCH | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_royal_ceremonial_screen` | Hierarchy/ceremonial framing | R | R | R | R | screen, canopy, rope/barrier; symbols held for narrative | OWNER+NARR+ARCH | DEFERRED_UNAPPROVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_guild_contract_board` | Guild tasks/information focal | R | R | R | R | board, postings, active/empty/locked; text separate/localized | GAME+NARR+UXA | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_guild_trophy_display` | Guild history/status display | R | R | R | R | case, rack, plinth; empty/populated/damaged | OWNER+NARR+ARCH | LOGICAL_RESERVED | NARR,SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_guild_meeting_contract_desk` | Guild service/meeting furniture | R | R | R | R | meeting table, officer desk, contract desk | ARCH+GAME+NARR | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_religious_altar_shrine` | Fictional worship/ritual focal | R | R | R | R | exact role, symbols, offerings and state all held | OWNER+NARR+ARCH | DEFERRED_UNAPPROVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_religious_reliquary_offering` | Relic/offering storage/display | R | R | R | R | reliquary, offering stand, sealed/open; meaning held | OWNER+NARR+ARCH | DEFERRED_UNAPPROVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_cultural_instrument_artifact` | Cultural life/heritage object | R | R | R | R | instrument, art object, craft display; exact identity held | OWNER+NARR | DEFERRED_UNAPPROVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_textile_rug_tapestry` | Interior warmth, acoustic and cultural dressing | R | R | R | R | rug, curtain, tapestry, plain/symbolic; symbolic variants narrative-held | OWNER+NARR+ARCH | LOGICAL_RESERVED | NARR,SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_utility_rope_chain` | Rigging, barrier and work dressing | R | R | R | R | coil, hanging, tied, chain, broken; climbability explicit | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_utility_bucket_tub_washbasin` | Water/service dressing | R | R | R | R | bucket, tub, basin, empty/filled | ARCH | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_utility_cart_wagon` | Logistics/market/farm vehicle prop | R | R | R | R | handcart, wagon, loaded/empty/damaged; static/mobile separately bound | GAME+ARCH | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_utility_scaffold_ladder` | Construction/maintenance dressing | R | R | R | R | scaffold bay, plank, ladder, active/removed; traversal explicit | ARCH+GAME | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_utility_tools_workset` | Generic work-role tools | R | R | R | R | farm, quarry, wood, repair sets; rack/loose clusters | ARCH | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_utility_firewood_fuel` | Fuel storage/dressing | R | R | R | R | split wood, charcoal, peat/approved fuel, stacked/loose | ARCH+NARR | LOGICAL_RESERVED | NARR,SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_clutter_books_scrolls_papers` | Knowledge/record clutter | R | R | R | R | books, scrolls, ledgers, loose papers; text separate/localized | NARR+ARCH | LOGICAL_RESERVED | NARR,SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_clutter_pottery_bottles` | Domestic/market clutter | R | R | R | R | ceramic, glass/opaque bottle, intact/broken; atlas sets | OWNER+ARCH | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_clutter_food_goods` | Food/market/dining clutter | R | R | R | R | raw/prepared/dried/containerized; exact species/names held | NARR+ARCH | LOGICAL_RESERVED | NARR,SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_clutter_textile_bedding` | Folded cloth, sacks, bedding | R | R | R | R | folded, draped, bundled, worn | ARCH | LOGICAL_RESERVED | SRC,BUD,TECH,GATE | post_mvp |
| `waf_prop_clutter_debris_rubble` | Damage/abandonment breakup | R | R | R | R | wood, stone, metal, glass-safe; sparse/dense clusters | ARCH+WORLD+TA | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_prop_clutter_personal_items` | Occupancy and character-scale life | R | R | R | R | common/service/military/affluent sets; meaning held | OWNER+NARR+ARCH | LOGICAL_RESERVED | NARR,SRC,BUD,TECH,GATE | post_mvp |

## 11. Interactables and harvestables

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_interactable_door_hatch` | Open/close/lock transition | S | S | S | S | closed/open/locked/blocked/broken; prompt/focus sockets | GAME+ARCH+UXA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interactable_container_loot` | Authorized container interaction | S | S | S | S | available/open/empty/locked/claimed | GAME+UXA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interactable_lever_switch` | Mechanism interaction | R | R | R | R | idle/available/active/disabled; physical state beyond color | GAME+ARCH+UXA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interactable_lift_platform` | Authored moving traversal | R | R | R | R | docked/moving/unavailable/fault; safe fallback | GAME+ARCH+ENG | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_interactable_gate_teleport_control` | Main-gate prompt/confirm/destination | R | R | R | R | friendly/opposing, available/disabled/broken, auto-quest compatible | GAME+UXA | LOGICAL_RESERVED | REALM,TOP,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interactable_quest_objective` | Generic catalog-bound objective prop | S | S | S | S | inactive/available/in-progress/complete/unavailable; exact art source required | GAME+NARR+UXA | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interactable_service_station` | Forge, market, guild and civic service | R | R | R | R | idle/available/busy/locked/unavailable | GAME+ARCH+UXA | LOGICAL_RESERVED | BLD,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_interactable_seat_use` | Optional seat interaction | S | S | S | S | available/occupied/unavailable; animation/socket contract | GAME+ARCH | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_harvestable_plant_herb` | Plant/herb resource node derivative | R | R | R | R | available/targeted/depleted/regrowing/locked | GAME+ECO+UXA | LOGICAL_RESERVED | GAME,BIO,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_harvestable_flower` | Flower resource derivative | R | R | R | R | available/targeted/depleted/regrowing/locked | GAME+ECO+UXA | LOGICAL_RESERVED | GAME,BIO,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_harvestable_fungi` | Fungi resource derivative | R | R | R | R | available/targeted/depleted/regrowing/locked | GAME+ECO+UXA | LOGICAL_RESERVED | GAME,BIO,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_harvestable_tree_wood` | Tree/wood resource derivative | R | R | R | R | available/damaged/felled/stump/regrowing/locked | GAME+ECO+UXA | LOGICAL_RESERVED | GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_harvestable_deadwood` | Deadwood resource derivative | R | R | R | R | available/targeted/depleted/absent | GAME+ECO+UXA | LOGICAL_RESERVED | GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_harvestable_ore` | Mineable ore node derivative | R | R | R | R | available/targeted/depleted/regrowing/locked | GAME+ECO+UXA | LOGICAL_RESERVED | GAME,BIO,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_harvestable_stone` | Quarryable stone derivative | R | R | R | R | available/targeted/depleted/regrowing/locked | GAME+ECO+UXA | LOGICAL_RESERVED | GAME,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_harvestable_magical_crystal` | Magical crystal resource derivative | R | R | R | R | dormant/available/targeted/depleted/locked; emission-off read | GAME+NARR+ECO+UXA | LOGICAL_RESERVED | GAME,NARR,BIO,SRC,BUD,ACC,TECH,GATE | post_mvp |

## 12. Signage, banners, and allegiance dressing

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_sign_direction_route` | Direction and route guidance | R | R | R | R | post, wall, road, cave; icon/shape plus localized text where used | WORLD+NARR+UXA | LOGICAL_RESERVED | TOP,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_sign_facility_service` | Identify facility/function | R | R | R | R | hanging, wall, freestanding; icon/shape plus localized label | GAME+NARR+UXA | LOGICAL_RESERVED | BLD,GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_sign_hazard_boundary` | Warn of danger/restriction | S | S | S | S | hazard classes, blocked route, PvP/war boundary; non-color cue | GAME+UXA | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_sign_notice_contract_board` | Public notices/contracts | R | R | R | R | civic, guild, market, quest; text/content separate | GAME+NARR+UXA | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_sign_plaque_waystone` | Landmark/history/location marker | R | R | R | R | plaque, waystone, memorial only with narrative source | NARR+OWNER+ARCH | DEFERRED_UNAPPROVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_banner_realm_standard` | Realm allegiance at strategic distance | R | R | R | R | flat/micro/3D, hanging/carried/static, damaged; Arcane Axis reference | OWNER+NARR+UXA | LOGICAL_RESERVED | REALM,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_banner_war_objective` | PvP ownership/objective state | R | R | R | R | neutral/friendly/hostile/contested/disabled, color-independent state | GAME+OWNER+UXA | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_banner_civic_pennant` | Settlement/civic dressing | R | R | R | R | wall, pole, street; identity without uncontrolled symbol invention | OWNER+NARR+ARCH | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_banner_guild` | Guild identity surface | S | S | S | S | hanging/pole/2.5D, absent/unresolved fallback; user content constraints | GAME+NARR+UXA | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_banner_event` | Event-only identity | 0:event-only | 0:event-only | 0:event-only | 0:event-only | event token, active/expired; never reused as realm authority | OWNER+NARR+UXA | LOGICAL_RESERVED | TOP,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |

## 13. VFX anchors

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_vfx_anchor_ambient` | Bounded dust, pollen, ash, leaves and motes | R | R | R | R | low/balanced/high/off; pooled, reduced-motion | OWNER+TA+UXA | LOGICAL_RESERVED | BIO,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_vfx_anchor_weather` | Rain, snow, storm and heat contact | R | R | R | R | local/global contact, reduced/off; never sole identity | OWNER+TA+UXA | LOGICAL_RESERVED | BIO,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_vfx_anchor_water` | Water contact, falls, ripples and spray | R | R | R | R | shore, flow, fall, object contact, reduced/off | TA+UXA | LOGICAL_RESERVED | BIO,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_vfx_anchor_fire_smoke` | Practical fire/smoke attachment | R | R | R | R | flame, ember, smoke, cold/off, reduced-motion | TA+UXA | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_vfx_anchor_realm_magic` | Controlled realm-specific phenomenon | R | R | R | R | idle/active/reduced/off; structure/material read survives off | OWNER+NARR+TA+UXA | LOGICAL_RESERVED | REALM,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_vfx_anchor_gate_teleport` | Gate channel/arrival/departure | R | R | R | R | available/channel/arrival/disabled/broken/reduced | GAME+OWNER+TA+UXA | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_vfx_anchor_interaction_feedback` | Focus, use, harvest and denial feedback | S | S | S | S | available/focused/success/denied/cooldown, non-VFX fallback | GAME+UXA+TA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_vfx_anchor_objective_combat` | Objective, siege and encounter feedback | S | S | S | S | allegiance/source tiers, hostile/ally/objective protected cues | GAME+UXA+TA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_vfx_anchor_construction_damage` | Build, repair, damage and destruction feedback | R | R | R | R | construction bands, hit/damage/repair, settled/off | GAME+ARCH+TA+UXA | LOGICAL_RESERVED | BLD,GAME,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_vfx_anchor_event` | Event-only world effect | 0:event-only | 0:event-only | 0:event-only | 0:event-only | active/reduced/off/expired; event token required | OWNER+NARR+TA+UXA | LOGICAL_RESERVED | TOP,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |

## 14. Technical helpers

Technical helpers are catalogued dependencies, not visible creative assets. Their
realm cells are `S` because every realm must receive equivalent functionality.

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_technical_collider_static` | Static physical collision | S | S | S | S | simple/compound, walkable/non-walkable, debug evidence | ENG+TA | CURRENT_BOUND_PARTIAL | TOP,GAME,BUD,TECH,GATE | launch |
| `waf_technical_collider_trigger` | Interaction/volume trigger | S | S | S | S | interaction, area, destination, damage only by contract | ENG+GAME | CURRENT_BOUND_PARTIAL | GAME,BUD,TECH,GATE | launch |
| `waf_technical_selection_proxy` | Stable selectable footprint | S | S | S | S | building/prop/node, selected/unselected debug | ENG+UXA | CURRENT_BOUND_PARTIAL | GAME,BUD,ACC,TECH,GATE | launch |
| `waf_technical_navigation_source` | Navmesh inclusion/source geometry | S | S | S | S | terrain, structure, interior, link landing | ENG+GAME | CURRENT_BOUND_PARTIAL | TOP,GAME,BUD,TECH,GATE | launch |
| `waf_technical_navigation_exclusion` | `NAVEX_` exclusion volumes | S | S | S | S | static/dynamic-owner, debug evidence | ENG+GAME | CURRENT_BOUND_PARTIAL | TOP,GAME,BUD,TECH,GATE | launch |
| `waf_technical_navigation_link` | Authored seam/climb/jump/teleport links | S | S | S | S | source/destination, enabled/disabled, bidirectional policy | ENG+GAME | CURRENT_BOUND_PARTIAL | TOP,GAME,BUD,ACC,TECH,GATE | launch |
| `waf_technical_occlusion_group` | Roof/canopy/wall obstruction control | S | S | S | S | roof, canopy, upper wall, interior backing | ENG+TA+UXA | LOGICAL_RESERVED | BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_technical_occlusion_portal_volume` | Interior/streaming visibility control | S | S | S | S | cell, portal, exterior/interior transition | ENG+TA | LOGICAL_RESERVED | TOP,BUD,TECH,DEVICE,GATE | post_mvp |
| `waf_technical_lod_group` | Per-asset LOD ownership | S | S | S | S | LOD0–N, protected cues, transition evidence | TA+ENG | CURRENT_BOUND_PARTIAL | SRC,BUD,ACC,TECH,DEVICE,GATE | launch |
| `waf_technical_impostor_far_proxy` | Repeated/long-range far representation | S | S | S | S | authored far mesh, opaque impostor, or explicit N/A reason | TA+ENG | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_technical_hlod_chunk_proxy` | Chunk/horizon aggregation | S | S | S | S | terrain, architecture, vegetation, mixed proxy; provenance map | ENG+TA | LOGICAL_RESERVED | TOP,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_technical_streaming_bounds` | Chunk load/unload/prefetch bounds | S | S | S | S | interaction, prefetch, horizon; debug evidence | ENG | LOGICAL_RESERVED | TOP,BUD,TECH,DEVICE,GATE | post_mvp |
| `waf_technical_replacement_socket` | Replaceable MVP/topology binding socket | S | S | S | S | terrain, landmark, structure, helper; exact owner reference | ENG+WORLD | CURRENT_BOUND_PARTIAL | TOP,MVP,BUD,TECH,GATE | launch |
| `waf_technical_interaction_socket` | Door/activity/output/advisor semantic socket | S | S | S | S | entrance, activity, output, advisor, use/focus | ENG+GAME | CURRENT_BOUND_PARTIAL | GAME,BUD,TECH,GATE | launch |
| `waf_technical_camera_focus_anchor` | Stable framing target | S | S | S | S | structure, prop, objective, interior | ENG+UXA | CURRENT_BOUND_PARTIAL | GAME,BUD,ACC,TECH,GATE | launch |
| `waf_technical_vfx_audio_anchor` | Behavior-free VFX/audio attachment | S | S | S | S | VFX, ambience, contact, one-shot; separate from gameplay sockets | ENG+TA | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_technical_spawn_encounter_marker` | Authoring-only spawn/encounter reference | S | S | S | S | player/NPC/fantasy-beast/objective only by gameplay contract | ENG+GAME | LOGICAL_RESERVED | TOP,GAME,BUD,TECH,GATE | post_mvp |
| `waf_technical_probe_volume` | Lighting/reflection/weather/water technical volume | S | S | S | S | light, reflection, weather, water, audio; no gameplay authority | ENG+TA | LOGICAL_RESERVED | TOP,BUD,TECH,DEVICE,GATE | post_mvp |
| `waf_technical_terrain_seam_transition` | Prevent chunk gaps and material seams | S | S | S | S | neighbor edges, fallback ground, debug validation | ENG+WORLD+TA | CURRENT_BOUND_PARTIAL | TOP,MVP,BUD,TECH,DEVICE,GATE | launch |

## 15. 2.5D derivatives

These families apply only in `dimension_kingdom_25d` /
`world_kingdom_private`. A row marked `R` means a separately approved derivative
for that realm's private kingdom. A 3D approval never approves its 2.5D row.

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_derivative_25d_building_render` | Strategic building presentation | R | R | R | R | levels 0–10, zoom tiers, LOD/proxy, selected/unselected | OWNER+ARCH+UXA | SOURCE_ONLY | BLD,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_derivative_25d_building_state` | Building state overlays/geometry | R | R | R | R | unbuilt, available, upgrading, blocked, damaged, completed, capstone, unavailable | GAME+OWNER+UXA | LOGICAL_RESERVED | BLD,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_derivative_25d_castle_core` | Private-kingdom castle anchor | R | R | R | R | base/progression/condition/overview silhouette | OWNER+ARCH+UXA | LOGICAL_RESERVED | TOP,SRC,BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_derivative_25d_terrain_tile` | Bounded unlocked-cell terrain | R | R | R | R | locked/unlocked, occupied/empty, edge/corner, quality tiers | WORLD+GAME+UXA | LOGICAL_RESERVED | TOP,GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_derivative_25d_road_tile` | Strategic roads and connections | R | R | R | R | straight/bend/T/X/end/bridge approach | ARCH+WORLD+UXA | LOGICAL_RESERVED | TOP,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_derivative_25d_wall_gate_tile` | Strategic wall/gate | R | R | R | R | bay/corner/end/gate, intact/damaged/breached | ARCH+GAME+UXA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_derivative_25d_resource_node` | Strategic resource/production read | R | R | R | R | available/working/depleted/blocked, non-color cue | GAME+UXA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_derivative_25d_structure_icon` | Building/facility identity icon | R | R | R | R | overview/micro, enabled/disabled, exact source link | OWNER+UXA | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_derivative_25d_decoration` | Bounded non-gameplay kingdom dressing | R | R | R | R | small/medium/landmark, selected/not-selectable | OWNER+ARCH+UXA | LOGICAL_RESERVED | SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_derivative_25d_banner_sign` | Realm/guild/facility communication | R | R | R | R | realm/guild/facility, macro/micro, state-safe | OWNER+NARR+UXA | LOGICAL_RESERVED | NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_derivative_25d_selection_footprint` | Stable cell/structure selection | S | S | S | S | valid/invalid/selected/neighbor/blocked | GAME+UXA+ENG | LOGICAL_RESERVED | GAME,BUD,ACC,TECH,GATE | post_mvp |
| `waf_derivative_25d_construction_feedback` | Quote/build/upgrade result feedback | R | R | R | R | preparing/active/complete/failed/cancelled/offline/reduced-motion | GAME+UXA | LOGICAL_RESERVED | GAME,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_derivative_25d_shadow_occlusion` | Compact-screen depth/readability | R | R | R | R | low/balanced/high/off fallback; cannot hide state | OWNER+TA+UXA | LOGICAL_RESERVED | BUD,ACC,TECH,DEVICE,GATE | post_mvp |
| `waf_derivative_25d_map_marker` | Facility/objective/location marker | S | S | S | S | overview/micro, selected/filtered/unavailable | GAME+NARR+UXA | LOGICAL_RESERVED | GAME,NARR,SRC,BUD,ACC,TECH,GATE | post_mvp |
| `waf_derivative_25d_portrait_thumbnail` | Inspector/command-deck asset view | R | R | R | R | current/next level, available/unavailable, deterministic framing | OWNER+UXA | LOGICAL_RESERVED | BLD,SRC,BUD,ACC,TECH,GATE | post_mvp |

## 16. Explicitly deferred ecosystem-specific fantasy beasts and monsters

These reservations prevent environmental catalogs from silently absorbing
living-fantasy scope. Existing `tdf_*` IDs remain canonical in their source
program until a separately approved production identity maps them.

| Family ID | Purpose | CRN | STH | ELD | UMB | Required variants | Owner | Status | Dependencies | Schedule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `waf_ecosystem_fantasy_beast_supporting` | Non-apex ecosystem-supporting fantasy beast families | R | R | R | R | approved family plus only structurally distinct ecotypes; LOD/rig/motion/distant proxy | OWNER+ECO | SOURCE_ONLY | BIO,GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | later_ecosystem |
| `waf_ecosystem_monster_common` | Common hostile monster families | R | R | R | R | role/body-plan variants, encounter and non-color threat read; no palette-only families | OWNER+ECO+GAME | DEFERRED_UNAPPROVED | BIO,GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | later_ecosystem |
| `waf_ecosystem_monster_elite` | Inner-realm elite families | R | R | R | R | source-approved named family, combat states, LOD/rig/motion/distant proxy | OWNER+ECO+GAME | SOURCE_ONLY | BIO,GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | later_ecosystem |
| `waf_ecosystem_monster_boss` | Outer-warzone boss families | R | R | R | R | source-approved named boss, arena/cinematic/gameplay tiers | OWNER+ECO+GAME | SOURCE_ONLY | TOP,BIO,GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | later_ecosystem |
| `waf_ecosystem_dragon_realm` | Four unresolved realm dragon identities | R | R | R | R | one separately approved singular identity per realm; cave/lair derivatives separate | OWNER+ECO+NARR | DEFERRED_UNAPPROVED | REALM,TOP,BIO,GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | later_ecosystem |
| `waf_ecosystem_dragon_wish` | Accordant Isle Wish Dragon singular source | 0:event-only | 0:event-only | 0:event-only | 0:event-only | exact approved source, cinematic/gameplay/LOD/rig/motion tiers | OWNER+ECO+NARR | DEFERRED_UNAPPROVED | TOP,GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | later_ecosystem |
| `waf_ecosystem_fantasy_beast_ambient_flying` | Avian/glider ambient fantasy beasts | R | R | R | R | perch/contact, flight silhouette, flock/solo presentation, distant proxy | OWNER+ECO | SOURCE_ONLY | BIO,GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | later_ecosystem |
| `waf_ecosystem_fantasy_beast_aquatic_littoral` | Lake/marsh/shore fantasy beasts | R | R | R | R | shore/shallow-water families only; deep pelagic explicitly excluded | OWNER+ECO | DEFERRED_UNAPPROVED | BIO,GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | later_ecosystem |
| `waf_ecosystem_fantasy_beast_deep_pelagic` | Deep-sea fantasy beasts | 0:no-approved-geography | 0:no-approved-geography | 0:no-approved-geography | 0:no-approved-geography | zero production variants until world geography and traversal are approved | OWNER+ECO+WORLD | DEFERRED_UNAPPROVED | TOP,BIO,GAME,NARR,SRC,BUD,ACC,TECH,DEVICE,GATE | later_ecosystem |

Explicit exclusions: humanoid enemies/NPCs belong to character/content pipelines;
dragons and singular apex creatures do not inflate common-family counts; desert
and oasis ecosystems have zero realm applicability pending approved geography;
palette-only, glow-only, religious-label-only, or people-name-only variants do
not create new families.

## 17. Habitat-source reservations without biome invention

The following source-review IDs are retained one-to-one and reserve future kit
contexts only. Their `biome_ids` remain `[]` with
`taxonomy_status: unresolved`; none is renamed or promoted here.

| Realm | Source habitat ID | Reserved future kit prefix | Current state |
| --- | --- | --- | --- |
| stonehold | `tdf_habitat_stonehold_faultroad_escarpment` | `wak_stonehold_environment_faultroad_escarpment_v###` | source review only |
| stonehold | `tdf_habitat_stonehold_rimecut_pass` | `wak_stonehold_environment_rimecut_pass_v###` | roster proposed |
| stonehold | `tdf_habitat_stonehold_ore_gallery_mouths` | `wak_stonehold_environment_ore_gallery_mouths_v###` | roster proposed |
| stonehold | `tdf_habitat_stonehold_slagfall_quarry` | `wak_stonehold_environment_slagfall_quarry_v###` | source review only |
| eldergrove | `tdf_habitat_eldergrove_hollowbark_oldgrowth` | `wak_eldergrove_environment_hollowbark_oldgrowth_v###` | source review only |
| eldergrove | `tdf_habitat_eldergrove_mirrorroot_littoral` | `wak_eldergrove_environment_mirrorroot_littoral_v###` | source review only |
| eldergrove | `tdf_habitat_eldergrove_sunmane_edge_meadow` | `wak_eldergrove_environment_sunmane_edge_meadow_v###` | roster proposed |
| eldergrove | `tdf_habitat_eldergrove_moonroot_floodbasin` | `wak_eldergrove_environment_moonroot_floodbasin_v###` | source review only |
| crownlands | `tdf_habitat_crownlands_crownstep_chalkland` | `wak_crownlands_environment_crownstep_chalkland_v###` | roster proposed |
| crownlands | `tdf_habitat_crownlands_galegrain_roadbelt` | `wak_crownlands_environment_galegrain_roadbelt_v###` | roster proposed |
| crownlands | `tdf_habitat_crownlands_reliquary_crypt_garden` | `wak_crownlands_environment_reliquary_crypt_garden_v###` | roster proposed |
| crownlands | `tdf_habitat_crownlands_meridian_storm_shelf` | `wak_crownlands_environment_meridian_storm_shelf_v###` | roster proposed |
| umbral | `tdf_habitat_umbral_ashvein_three_fault_rift` | `wak_umbral_environment_ashvein_three_fault_rift_v###` | roster proposed |
| umbral | `tdf_habitat_umbral_cinder_runoff_shelf` | `wak_umbral_environment_cinder_runoff_shelf_v###` | roster proposed |
| umbral | `tdf_habitat_umbral_ashwood_veil_ravine` | `wak_umbral_environment_ashwood_veil_ravine_v###` | roster proposed |
| umbral | `tdf_habitat_umbral_graveglass_cavern_vale` | `wak_umbral_environment_graveglass_cavern_vale_v###` | roster proposed |

## 18. Explicit zero and not-applicable declarations

1. There are zero canonical biome IDs in this document. Every future family
   record starts with `biome_ids: []` and `taxonomy_status: unresolved`.
2. There are zero generated assets, changed bindings, scenes, prefabs,
   Addressables, schemas, runtime catalogs, or hardcoded C# IDs.
3. There are zero realm variants for Accordant Isle event-only architecture,
   banners, VFX, and the Wish Dragon; those use the event context.
4. There are zero realm variants for deep-pelagic fantasy beasts, desert/oasis
   environment kits, and humanoid NPC/enemy families because no owning world
   geography or pipeline authorizes them here.
5. There are zero production-approved families created by this taxonomy.
   `CURRENT_BOUND_PARTIAL` records preserve narrow existing bindings and do not
   claim full family coverage or measured platform approval.
6. There are zero implicit prop classes: each requested lighting, storage,
   market, forge, kitchen, military, royal, guild, religious/cultural, utility,
   and clutter class has a canonical family row above.
7. There are zero implicit realm omissions. Every logical family row has an
   explicit Crownlands, Stonehold, Eldergrove, and Umbral cell.

## 19. Downstream validation contract

The future schema/inventory assembly must fail closed unless it proves:

1. every `waf_` ID and reserved canonical/kit/derivative ID is unique and matches
   its grammar;
2. every alias maps one-to-one and cannot shadow a canonical ID;
3. all four realm applicability cells exist on every family and every `0` has a
   reason;
4. every family has purpose, variants, owner, status, dependencies, and schedule;
5. every `R` family receives a four-realm coverage record or a separately owner-
   approved exclusion; every `S` family proves shared equivalence;
6. unknown biome, prefab, Addressable, source, provenance, budget, approval, or
   platform values remain explicit empty/blocked values rather than fabricated
   defaults;
7. 3D and 2.5D identities, technical/creative approvals, and evidence remain
   independent;
8. current MVP and eight existing Town Hall/Workshop bindings remain
   fingerprint-protected until an approved migration proves replacement;
9. aggregate category/realm coverage, duplicate/alias reports, owner coverage,
   binding coverage, and budget rollups are deterministic and byte-stable;
10. no generation or activation field becomes eligible while `GATE`, `BUD`,
    `TECH`, `DEVICE`, provenance, accessibility, or owner evidence is blocked.

## 20. Source account

Controlling inputs read in whole or in the directly relevant sections for this
taxonomy:

- `unity/Docs/AssetLibrary/PostMVP_World_Asset_Authority_Reconciliation_v1.md`;
- `unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json`;
- `unity/Assets/AL/StreamingAssets/GameData/al_world_streaming_catalog.json`;
- `unity/Assets/AL/StreamingAssets/GameData/al_building_catalog.json` and
  `buildings.json`;
- `unity/Assets/AL/StreamingAssets/GameData/al_first_session_terrain_catalog.json`;
- `unity/Assets/AL/ScriptableObjects/Resources/KingdomBuildingModelCatalog.asset`;
- `unity/Docs/Architecture/FourRealm_Modular_Construction_Envelope.md`;
- `unity/Docs/Architecture/FourRealm_TownHall_Production_Contract.md`;
- `unity/Docs/Architecture/Kingdom_Building_Level_And_Placement_Design.md`;
- `unity/Docs/Terrestrials/Ecosystems/Four_Realm_Ecosystem_And_Habitat_Source.md`;
- `unity/Docs/Terrestrials/Ecosystems/Ecosystem_Source_Budgets_And_Asset_Layout.md`;
- `unity/Docs/Terrestrials/Ecosystems/Creature_Diversity_And_Terrestrial_Optimization_Source.md`;
- `unity/Docs/AssetLibrary/Collaborator_Asset_Library_2026-08-17.md`;
- relevant production/naming and scope sections of root `DESIGN.md`.

The authority reconciliation measured the broader corpus. This child directly
line-read 3,243 lines across the whole files and bounded relevant sections above,
plus targeted exact-ID searches in the 1,318-line world-streaming catalog, the
902-line Blender source manifest, and retained terrestrial records. Unread
tails and unrelated sections are not counted. Binary meshes, textures, `.blend`
files, prefabs, and scenes were not visually inspected because this task creates
no asset and grants no visual or production approval.
