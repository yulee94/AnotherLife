# Post-MVP World-Asset Budgets and Readability Standard v1

**Status:** controlling preparation and admission ceilings; production remains `HELD`
**Task:** `t_e00af422`
**Applies to taxonomy:** `PostMVP_World_Asset_Taxonomy_v1.md`, SHA-256 `4aed5d7b9b83e9f1a8125eb6ede3ae6fd087cc6c6cc5bd58c3556488d00e4e59`
**Binding mobile-floor configuration:** physical Galaxy A54 5G 6 GB / Exynos 1380 / Mali-G68 candidate, native 2340×1080 landscape output at 60 Hz, Vulkan, `mobile_floor` preset, 30 FPS
**Runtime baseline:** Unity 6000.3.22f1, Built-In Render Pipeline
**Final creative and release authority:** project owner

## 1. Purpose, boundary, and fail-closed rule

This standard supplies the budget authority required by the 242-family world-asset
taxonomy and the `budgetClassId` field in
`al-world-asset-inventory.schema.json`. It creates measurable ceilings for asset
production, mobile-floor residency, streaming, installation, rendering, readability,
and accessibility. It does not create the future inventory payload, generate an asset,
change a prefab or scene, activate Addressables, alter current MVP admission, choose a
biome, or approve a deferred fantasy beast or monster.

The values below are ceilings, not fill targets. Use less whenever the protected read
survives. Production reduces breadth, variant count, hidden detail, secondary motion,
and unique materials before it spends an exception or weakens player-facing quality.

A record is budget-valid only when all of these are true:

1. its `familyId` resolves exactly one class in section 5;
2. its exact measured values are no greater than every applicable per-record class
   ceiling;
3. every runtime artifact is counted once in each applicable aggregate, using immutable
   artifact identity rather than an asset-name estimate;
4. every cell, scene, realm package, and installation aggregate passes sections 7–10;
5. the physical mobile-floor evidence and readability gates pass; and
6. every exception is complete and approved under section 13.

A class pass never grants source, provenance, gameplay, creative, accessibility,
performance, release, generation, or activation approval. Missing measurements are
`BLOCKED`, never zero. A `wbud_reserved_*` class is planning-only and can never support
`production_approved` or `admitted` state.

## 2. Measurement vocabulary

All byte values use binary units: `1 MiB = 1,048,576 bytes`; `1 GiB = 1,073,741,824
bytes`. Source, concept, review, cinematic-only, editable DCC, and unreferenced duplicate
artifacts are excluded from Player packages, but their exclusion must be proven from the
built dependency graph.

- **Base variant (`B`)** — structurally distinct production geometry or atlas identity.
  A palette swap, LOD, quality state, damage band, or animation clip is not a base
  variant.
- **State derivative (`S`)** — a runtime-visible gameplay, construction, depletion,
  damage, availability, or presentation state of one base. Shared geometry/material
  deltas are preferred over complete duplicate assets.
- **LOD0 triangles** — exact post-import rendered triangles at the closest supported
  view. Terrain and water values are measured per 128 m budget cell.
- **Mobile-normal triangles** — the most expensive representation permitted during
  normal mobile-floor play at its declared distance. A camera that exposes LOD0 must
  count LOD0 instead.
- **Draws** — visible renderer passes after batching/instancing, including extra material
  passes and LOD cross-fade overlap. Material slots are not assumed to batch.
- **Resident bytes** — peak runtime mesh, texture, animation, material, VFX, and dependent
  asset bytes attributable to one canonical asset after import at the mobile-floor tier.
- **Compressed-delivery bytes** — bytes transferred for the unique runtime dependency
  closure in the APK/split/asset-pack delivery artifact after store-equivalent
  compression.
- **Installed bytes** — on-device file bytes occupied by that unique dependency closure
  after installation. This is distinct from compressed-delivery, runtime-resident, cache,
  and temporary update bytes.
- **Load-I/O bytes** — actual storage bytes read to make the dependency ready in the
  measured transition. The capture states whether Android read compressed or installed
  representation and records the direct source metric.
- **Activation p95** — main-thread time to instantiate/activate the warmed asset in the
  release-equivalent Player. I/O and decompression are measured separately.
- **Budget cell** — a fixed `128 m × 128 m` accounting overlay used only for repeatable
  density and rollup math. It is not an approved runtime terrain tile, topology chunk,
  biome, or placement authority.
- **Protected cue** — silhouette, route, entrance, traversal state, interaction or
  harvest state, allegiance, hazard, objective, or realm-construction information that
  lower tiers and accessibility modes may not remove.

For each canonical asset the future inventory validator must consume or derive this cost
vector:

```text
C(asset) = {
  base_variants, state_derivatives,
  triangles_by_lod[], draws_by_lod[], material_slots_by_lod[],
  texture_width_height_format_mips[],
  resident_bytes_by_category{}, compressed_delivery_bytes, installed_bytes,
  collider_primitives, collider_proxy_triangles,
  nav_source_triangles, nav_link_pairs, nav_data_bytes,
  active_vfx_sources, live_particles, transparent_draws, dynamic_lights,
  activation_main_thread_ms_p95, load_io_bytes, load_ready_ms_p95
}
```

Null, absent, inferred, or source-only values do not pass a production gate.

## 3. Common mobile-floor production rules

### 3.1 Geometry, LOD, materials, and textures

Unless a stricter class row says otherwise:

- LOD1 is at most `60%` of LOD0 triangles; LOD2 is at most `30%`; a mesh far
  representation is at most `10%`.
- Every repeated, skyline, or long-range asset has an authored far mesh, opaque
  impostor, or chunk HLOD. `unresolved` fails. Transparent impostors require an
  exception.
- LOD cross-fade overlap is counted as both representations and both draw costs.
- Mobile-normal tiers preserve every protected cue and normally use no more than one
  material slot; architecture hero/common may use two where their class allows it.
- Materials are shared or GPU-instanced. A unique material instance, blended
  transparency, or runtime read/write texture requires measured exception evidence.
- Runtime texture long edge is capped by class. Base color, normal, and packed masks use
  mipmaps and mobile platform compression. The binding-floor default is `ASTC 6×6`;
  `ASTC 4×4` is allowed only for demonstrated critical alpha/normal quality within the
  same byte ceiling. An `ETC2` fallback is a separately measured package dependency and
  does not certify the Vulkan/ASTC floor by itself.
- Common world surfaces use shared `1K` trims/atlases; hero or macro content may use
  `2K` only where the class permits it. No active class permits a unique `4K` runtime
  texture.
- Alpha clip is bounded and included in overdraw evidence. Blended transparent pixels
  are a scarce scene aggregate, never free because the texture is small.

### 3.2 Collision and navigation

Render geometry is never collision or navigation authority. Primitive counts below are
per prefab instance. A class may use either its primitive ceiling or its proxy-triangle
ceiling, not both at the maximum without an exception.

| Physics group | Classes | Collider ceiling per instance | Navigation ceiling per instance |
| --- | --- | --- | --- |
| surface-only | `wbud_surface_layer`, `wbud_decal`, `wbud_vfx_anchor` | none; one trigger only when gameplay authority requires it | none |
| terrain/water cell | `wbud_terrain_macro`, `wbud_water` | one TerrainCollider or one dedicated static proxy ≤ `8,192` tris per budget cell; water adds ≤ `4` primitive triggers | simplified upward source ≤ `16,384` tris per cell; ≤ `4` link pairs |
| major static | `wbud_foliage_major`, `wbud_static_large`, `wbud_traversal_module`, `wbud_architecture_common`, `wbud_architecture_hero`, `wbud_interior_module` | ≤ `8` primitives or one dedicated static proxy ≤ `512` tris (`1,024` for hero architecture) | source/exclusion geometry ≤ `512` tris (`1,024` hero); ≤ `4` link pairs |
| minor static | `wbud_foliage_minor`, `wbud_static_small`, `wbud_prop_small`, `wbud_signage_banner` | ≤ `2` primitives or one convex proxy ≤ `64` tris; foliage collision defaults to none | no source by default; one exclusion ≤ `24` tris |
| large prop | `wbud_prop_large` | ≤ `4` primitives or one convex proxy ≤ `128` tris | one exclusion ≤ `48` tris; no links by appearance |
| interaction | `wbud_interactable` | ≤ `3` physical primitives plus one trigger; convex proxy ≤ `96` tris if needed | source/exclusion ≤ `128` tris; ≤ `2` authored link pairs |
| technical helper | `wbud_technical_helper` | visible render cost `0`; per helper set ≤ `32` primitives or ≤ `2,048` dedicated proxy tris | source/exclusion ≤ `4,096` tris; ≤ `8` link pairs |
| strategic 2.5D | `wbud_derivative_25d_large`, `wbud_derivative_25d_small` | one simple selection footprint; physical world collision is owned by the 3D/gameplay source | no navigation source; explicit selection/placement grid only |

Collision is LOD-independent. Visual LOD changes may not move a standing surface, alter
selection, open a route, close a route, or change interaction range. Dynamic doors,
gates, ladders, teleports, siege damage, and costs require separate gameplay authority.

## 4. Active per-record budget classes

`B/R` is maximum base variants per family per applicable realm. `S/B` is maximum state
derivatives per base. Taxonomy-required states still have to exist; if they do not fit,
production consolidates shared geometry/deltas or uses section 13. LODs and quality modes
do not consume `B/R` or `S/B`.

Resident, delivery, and installed values are per canonical runtime asset, inclusive of
its unique dependency closure but excluding dependencies already hash-deduplicated in
the same aggregate. The `Delivery / installed` pair never substitutes one number for the
other.

| Budget class | B/R | S/B | LOD0 tris | Mobile-normal tris | Required far behavior | Texture long edge / format | LOD0 mats; mobile draws | Resident MiB | Delivery / installed MiB | Activation p95 |
| --- | ---: | ---: | ---: | ---: | --- | --- | --- | ---: | ---: | ---: |
| `wbud_terrain_macro` | 1 | 4 | `131,072/cell` | `65,536/cell` | chunk HLOD | `2K`, ASTC 6×6 | `≤4`; `≤4/cell` | 32 | 8 / 16 | 2.0 ms |
| `wbud_surface_layer` | 6 | 2 | `8,192/cell` | `4,096/cell` | HLOD or cull when geometry exists | `2K`, ASTC 6×6 | `1`; `≤1` incremental pass | 8 | 2 / 4 | 0.25 ms |
| `wbud_decal` | 6 | 3 | 512 | 256 | distance cull | `1K`, ASTC 6×6 | `1`; `1` | 2 | 0.5 / 1 | 0.15 ms |
| `wbud_water` | 2 | 5 | `32,768/cell` | `16,384/cell` | far mesh or HLOD | `2K`, ASTC 6×6 | `2`; `≤2/cell` | 16 | 4 / 8 | 1.0 ms |
| `wbud_foliage_major` | 8 | 4 | 20,000 | 8,000 | opaque impostor or HLOD | `2K` shared atlas, ASTC 6×6 | `2`; `≤2` | 10 | 3 / 6 | 0.6 ms |
| `wbud_foliage_minor` | 12 | 4 | 5,000 | 1,500 | opaque impostor or cull | `1K` shared atlas, ASTC 6×6 | `1`; `1` | 3 | 0.75 / 1.5 | 0.2 ms |
| `wbud_static_large` | 8 | 5 | 20,000 | 8,000 | far mesh or HLOD | `2K` shared trim/atlas, ASTC 6×6 | `2`; `≤2` | 10 | 3 / 6 | 0.6 ms |
| `wbud_static_small` | 12 | 5 | 5,000 | 1,500 | far mesh or cull | `1K` shared atlas, ASTC 6×6 | `1`; `1` | 3 | 0.75 / 1.5 | 0.2 ms |
| `wbud_traversal_module` | 12 | 5 | 20,000 | 8,000 | far mesh or HLOD | `2K` shared trim/atlas, ASTC 6×6 | `2`; `≤2` | 10 | 3 / 6 | 0.6 ms |
| `wbud_architecture_common` | 4 | 12 | 20,000 | 12,000 | LOD2 plus HLOD | `2K` shared trim/atlas, ASTC 6×6 | `2`; `≤2` | 16 | 5 / 10 | 1.0 ms |
| `wbud_architecture_hero` | 2 | 12 | 40,000 | 24,000 | LOD2 plus HLOD | `2K`, ASTC 6×6 | `3`; `≤2` | 32 | 10 / 20 | 1.5 ms |
| `wbud_interior_module` | 8 | 6 | 12,000 | 5,000 | HLOD, portal, or justified cull | `2K` shared trim/atlas, ASTC 6×6 | `2`; `≤1` | 8 | 2.5 / 5 | 0.5 ms |
| `wbud_prop_large` | 8 | 5 | 10,000 | 4,000 | far mesh or cull | `1K` shared atlas, ASTC 6×6 | `2`; `≤1` | 5 | 1.5 / 3 | 0.3 ms |
| `wbud_prop_small` | 12 | 4 | 5,000 | 1,500 | cull or cluster proxy | `1K` shared atlas, ASTC 6×6 | `1`; `1` | 2 | 0.5 / 1 | 0.1 ms |
| `wbud_interactable` | 8 | 6 | 8,000 | 3,000 | far mesh or cull; state cue retained | `1K`, ASTC 6×6 | `2`; `≤1` | 5 | 1.5 / 3 | 0.3 ms |
| `wbud_signage_banner` | 8 | 6 | 5,000 | 1,500 | far silhouette or cull | `1K` atlas, ASTC 6×6 | `1`; `1` | 2 | 0.5 / 1 | 0.2 ms |
| `wbud_vfx_anchor` | 5 | 5 | 1,000 | 500 | physical off-state cue | `1K` atlas, ASTC 6×6 | `1`; `≤2` transparent draws | 4 | 1 / 2 | 0.2 ms |
| `wbud_technical_helper` | 4 | 4 | 0 visible | 0 visible | n/a | no visible texture | `0`; `0` | 1 | 0.25 / 0.5 | 0.05 ms |
| `wbud_derivative_25d_large` | 4 | 12 | 20,000 | 8,000 | strategic proxy | `2K` atlas, ASTC 6×6 | `2`; `≤1` | 8 | 2.5 / 5 | 0.5 ms |
| `wbud_derivative_25d_small` | 8 | 12 | 2,000 | 500 | micro representation or cull | `1K` atlas, ASTC 6×6 | `1`; `1` | 2 | 0.5 / 1 | 0.15 ms |

Additional `wbud_vfx_anchor` mobile-floor ceilings are `64` live particles per balanced
anchor, `16` per low/reduced-motion anchor, `0` in off state, no per-particle dynamic
light, no shadow-casting particle, and at most one pooled unshadowed light request per
anchor. High state is not a mobile-floor entitlement and is counted separately when
supported.

## 5. Closed 242-family assignment

The mapping algorithm is ordered and closed:

1. exact assignments and overrides below win;
2. then one default prefix rule applies;
3. the expected count for the selected class is checked;
4. the taxonomy SHA-256 and total `242` family IDs must match this revision; and
5. zero or multiple matches, a changed count, or a changed taxonomy hash is
   `BudgetUnassigned` until this standard is versioned.

### 5.1 Default prefix rules and expected coverage

| Selector after exact overrides | Budget class | Expected families |
| --- | --- | ---: |
| `waf_terrain_*` | `wbud_surface_layer` | 9 |
| `waf_vegetation_*` | `wbud_foliage_minor` | 10 |
| `waf_geology_*` | `wbud_static_small` | 5 |
| `waf_traversal_*` | `wbud_traversal_module` | 18 |
| `waf_architecture_*` | `wbud_architecture_common` | 24 |
| `waf_interior_*` | `wbud_interior_module` | 21 |
| `waf_prop_*` | `wbud_prop_small` | 24 |
| `waf_interactable_*`, `waf_harvestable_*` | `wbud_interactable` | 16 |
| `waf_sign_*`, `waf_banner_*` | `wbud_signage_banner` | 10 |
| `waf_vfx_*` | `wbud_vfx_anchor` | 10 |
| `waf_technical_*` | `wbud_technical_helper` | 19 |
| `waf_derivative_25d_*` | `wbud_derivative_25d_small` | 10 |

### 5.2 Exact terrain overrides

- `waf_terrain_surface_macro_landform` → `wbud_terrain_macro`.
- `waf_terrain_water_surface`, `waf_terrain_water_edge_module` → `wbud_water`.
- `waf_terrain_decal_material_transition`, `waf_terrain_decal_erosion_drainage`,
  `waf_terrain_decal_wetness_stain`, `waf_terrain_decal_wear_tracks`,
  `waf_terrain_decal_damage_debris`, `waf_terrain_decal_route_marking` →
  `wbud_decal`.

Expected exact counts: terrain macro `1`, water `2`, decal `6`.

### 5.3 Exact major-vegetation overrides

These four map to `wbud_foliage_major`:

- `waf_vegetation_tree_canopy`;
- `waf_vegetation_tree_understory`;
- `waf_vegetation_root_structural`;
- `waf_vegetation_deadwood`.

### 5.4 Exact large-geology overrides

These nine map to `wbud_static_large`:

- `waf_geology_boulder`;
- `waf_geology_cliff_face`;
- `waf_geology_ledge_overhang`;
- `waf_geology_cave_entrance`;
- `waf_geology_cave_tunnel_module`;
- `waf_geology_cavern_room_landmark`;
- `waf_geology_crystal_formation`;
- `waf_geology_magical_crystal_node`;
- `waf_geology_mine_quarry_dressing`.

### 5.5 Exact hero-architecture overrides

These five map to `wbud_architecture_hero`:

- `waf_architecture_castle_enterable`;
- `waf_architecture_fortress_enterable`;
- `waf_architecture_city_capital_kit`;
- `waf_architecture_building_town_hall`;
- `waf_architecture_event_accordant_isle`.

All remaining architecture, including Workshop, settlement kits, walls, watchtowers,
and general enterable structures, remains `wbud_architecture_common`. A specific asset
may request a hero exception; its family does not silently change class.

### 5.6 Exact large-prop overrides

These 25 map to `wbud_prop_large`:

- `waf_prop_seating_bench_pew`, `waf_prop_surface_table_desk`,
  `waf_prop_sleep_bed_cot_bunk`;
- `waf_prop_storage_shelf_bookcase`, `waf_prop_storage_cabinet_cupboard`;
- `waf_prop_lighting_brazier_hearth_fireplace`,
  `waf_prop_lighting_chandelier_hanging`;
- `waf_prop_market_stall_canopy`, `waf_prop_market_counter_display`;
- `waf_prop_forge_hearth_anvil`, `waf_prop_kitchen_oven_cookfire`;
- `waf_prop_military_weapon_rack`, `waf_prop_military_armor_stand`,
  `waf_prop_military_training_dummy_target`, `waf_prop_military_war_map_table`;
- `waf_prop_royal_throne_dais`, `waf_prop_royal_council_lectern`,
  `waf_prop_royal_ceremonial_screen`;
- `waf_prop_guild_contract_board`, `waf_prop_guild_trophy_display`,
  `waf_prop_guild_meeting_contract_desk`;
- `waf_prop_religious_altar_shrine`, `waf_prop_textile_rug_tapestry`;
- `waf_prop_utility_cart_wagon`, `waf_prop_utility_scaffold_ladder`.

### 5.7 Exact large-2.5D overrides

These five map to `wbud_derivative_25d_large`:

- `waf_derivative_25d_building_render`;
- `waf_derivative_25d_building_state`;
- `waf_derivative_25d_castle_core`;
- `waf_derivative_25d_terrain_tile`;
- `waf_derivative_25d_wall_gate_tile`.

### 5.8 Deferred planning-only assignments

These are reservations, not approved content or approved costs. Numeric ranges are only
capacity placeholders so future plans cannot pretend the scope is free. Every such
record remains held, with measured fields empty and performance/release approval blocked,
until a separately approved ecosystem task replaces the reserved class with an active
budget class and physical evidence.

| Family ID | Planning-only class | Reservation envelope, not admission |
| --- | --- | --- |
| `waf_ecosystem_fantasy_beast_supporting` | `wbud_reserved_fantasy_beast_supporting` | plan near ambient/common source ceiling: ≤25k LOD0, ≤2 materials, 1K shared texture, rig/motion/far proxy required |
| `waf_ecosystem_fantasy_beast_ambient_flying` | `wbud_reserved_fantasy_beast_supporting` | same; flock cost unresolved and must be separately profiled |
| `waf_ecosystem_fantasy_beast_aquatic_littoral` | `wbud_reserved_fantasy_beast_supporting` | same; water interaction cost unresolved |
| `waf_ecosystem_monster_common` | `wbud_reserved_monster_common` | plan ≤25k LOD0, ≤2 materials, 1K shared texture, bounded rig/animation/far proxy |
| `waf_ecosystem_monster_elite` | `wbud_reserved_monster_elite` | plan ≤45k LOD0, ≤3 materials, 1K–2K texture, bounded rig/animation/far proxy |
| `waf_ecosystem_monster_boss` | `wbud_reserved_monster_boss` | no approved cost; reserve a distinct boss/arena/cinematic/gameplay analysis category |
| `waf_ecosystem_dragon_realm` | `wbud_reserved_dragon` | no approved cost; one separately approved identity per realm remains owner/narrative-held |
| `waf_ecosystem_dragon_wish` | `wbud_reserved_dragon` | no approved cost; event cinematic/gameplay derivatives remain separate |
| `waf_ecosystem_fantasy_beast_deep_pelagic` | `wbud_reserved_deferred_zero` | exactly zero runtime variants and zero compressed-delivery, installed, resident, or load-I/O bytes until geography/traversal authority exists |

The expected reserved-class counts are `3`, `1`, `1`, `1`, `2`, and `1` respectively.
Any `wbud_reserved_*` reference with positive approval, a nonzero packaged dependency,
or runtime placement is a hard failure.

### 5.9 Coverage result

For the pinned taxonomy revision, the closed resolver maps `242/242` unique family IDs
to exactly one of `26` classes. Expected active-class counts are the values in section
5.1 plus terrain macro `1`, water `2`, decal `6`, foliage major `4`, static large `9`,
architecture hero `5`, prop large `25`, and derivative-2.5D large `5`; reserved counts
are in section 5.8. The downstream inventory must reproduce this report byte-stably.

## 6. Variant and reuse budgets

1. Count base variants and state derivatives per family, per realm, and globally. A
   realm-authored `R` variant consumes that realm's `B/R`; a shared `S` implementation
   consumes one global base and is referenced, not copied, by four realms.
2. Full duplicated texture sets for palette, wetness, damage, allegiance, construction,
   or depletion states are prohibited when a packed mask, shader parameter, decal, or
   small delta can preserve truth.
3. LODs, impostors, HLODs, collision, navigation, and 2.5D derivatives are separately
   counted artifacts. They do not consume base-variant count, but all bytes and runtime
   costs still roll up.
4. Architecture levels `0–10` use shared modular bases and bounded level deltas. Eleven
   levels do not authorize eleven complete unique texture/material sets.
5. One asset may serve several families only through explicit canonical relationships;
   one artifact hash is charged once to installation/residency, while each visible
   instance still charges triangles, draws, collision, and update cost.
6. If the taxonomy-required states exceed `S/B`, consolidate geometry/deltas or request
   an exception. Omitting a required gameplay/readability state is not optimization.

## 7. Streaming-cell and scene envelopes

### 7.1 Streaming rings

The budget overlay permits at most:

- one interaction cell;
- eight adjacent prefetch cells; and
- sixteen horizon/HLOD cells.

Runtime topology may use differently shaped canonical chunks, but every loaded chunk is
projected onto the 128 m overlay. Fractional overlaps are charged by the maximum visible
or resident contribution, never rounded down to zero. The union of runtime chunks in a
ring must fit the same envelope.

No runtime artifact is duplicated per cell bundle. Shared dependencies have one owning
bundle and deterministic references. One asset may belong to only one residency owner:
shared, one realm, one event pack, or one approved derivative pack.

| Incremental cell payload | Interaction | Each prefetch cell | Each horizon cell |
| --- | ---: | ---: | ---: |
| unique compressed-delivery bytes | ≤64 MiB | ≤32 MiB | ≤8 MiB |
| unique installed bytes | ≤128 MiB | ≤64 MiB | ≤16 MiB |
| measured storage load-I/O bytes | ≤64 MiB | ≤32 MiB | ≤8 MiB |
| unique resident bytes after activation | ≤192 MiB | ≤64 MiB | ≤16 MiB |
| main-thread activation in any frame | ≤4.0 ms | ≤2.0 ms | ≤1.0 ms |
| p95 prefetch-to-ready latency | ≤2,000 ms | ≤2,000 ms | ≤3,000 ms |

Ring-wide unique world-asset residency is ≤`768 MiB`, including cross-fade overlap and
load/unload spikes. Load/unload transient growth is ≤`128 MiB` above the pre-transition
steady state and must return below the steady envelope within `5 s`.

### 7.2 Mobile-floor visible-scene envelope

The worst supported camera anchor and workload state must pass all rows simultaneously:

| Metric | Mobile-floor pass threshold |
| --- | ---: |
| visible world-asset triangles | ≤1,200,000 |
| visible world-asset draws/batches | ≤650 |
| SetPass calls attributable to world assets | ≤180 |
| shadow-casting world draws | ≤120 |
| transparent world/VFX draws | ≤80 |
| active world renderers | ≤1,800 |
| unique resident world textures | ≤384 MiB |
| unique resident world meshes | ≤128 MiB |
| world animation/VFX/material/other residency | ≤128 MiB |
| total unique resident world assets | ≤768 MiB |
| collider primitives/shapes in active physics scene | ≤2,500 |
| dedicated collision-proxy triangles | ≤75,000 |
| nav source triangles before bake per interaction cell | ≤150,000 |
| baked navigation data resident per interaction cell | ≤8 MiB |
| active balanced world VFX anchors | ≤24 |
| total balanced world live particles | ≤1,200 |
| local world dynamic lights | ≤4, of which ≤1 shadowed |
| average full-frame transparent overdraw | ≤1.5×; no gameplay-critical region >3× for >1 s |
| steady world-owned managed allocation | target `0 B/frame`; hard p95 ≤1 KiB/frame |
| world visibility/render submission CPU p95 | ≤6.0 ms |
| world rendering GPU p95 | ≤10.0 ms |
| world streaming main-thread contribution in any frame | ≤4.0 ms |

Instancing reduces draws, not triangle, overdraw, collision, memory, or update charges.
Occlusion-culling claims use captures proving the worst approved camera path; invisible
objects still count resident bytes if loaded.

### 7.3 Scene load and transition thresholds

On the physical mobile floor, a release-equivalent build must satisfy:

- cold app launch to the first interactive approved surface ≤`15 s`;
- warmed golden-scene transition request to player control ≤`10 s`;
- a prefetched world cell ready before the player reaches its boundary at the declared
  maximum travel speed, with p95 ≤`2 s`;
- no streaming/activation frame spends more than `4 ms` of main-thread time;
- no unexplained gameplay stall ≥`100 ms`;
- no shader compilation during the measured warmed workload; first-install and
  first-traversal compilation remains separately captured and may not hide a ≥`100 ms`
  unexplained stall.

A loading screen can explain presentation but cannot erase a failed duration, memory,
crash, or ANR gate.

## 8. Frame, memory, thermal, and mobile-floor pass/fail

Full-frame evidence uses the sustained physical-device procedure. This v1 budget gate is
bound to a physical Galaxy A54 5G 6 GB candidate using Exynos 1380/Mali-G68. If a 6 GB
physical SKU is unavailable, the gate is `BLOCKED`; an 8 GB SKU, emulator, chipset peer,
or different model does not substitute. The captured evidence unit freezes and reports
the exact retail model/SKU/region, Android version/build/security patch, GPU driver,
battery health, and build fingerprint before its first run.

The binding run uses native `2340×1080` landscape output, a locked `60 Hz` display mode,
Vulkan, Android frame pacing, the canonical `mobile_floor` preset, and a `30 FPS` target.
Internal render scale may adapt only from `80%` through `100%`, must be recorded every
minute, and may not violate image stability or protected cues. Testing uses standard
shipping power mode, no game booster/battery saver/RAM expansion, no case or external
cooling, unplugged power, `23 ± 2 °C` ambient, start charge `50–80%`, and Android thermal
status `NONE` under the detailed benchmark procedure. A different OS/build or driver
starts a new evidence unit rather than being averaged with the old one. The final release
SKU remains owner-controlled and can replace this planning gate only through a new
approved standard revision with equivalent evidence.

Every golden-scene repetition runs five minutes warm-up plus at least twenty measured
minutes, three valid repetitions, with the same build, catalog, workload, preset, API,
resolution, render-scale range, and exact device configuration.

A repetition passes only when:

- CPU, GPU, and delivered-frame p95 are each ≤`33.33 ms`;
- p99 is reported and investigated;
- no unexplained gameplay stall is ≥`100 ms`;
- the world-asset CPU/GPU/streaming sub-budgets in section 7 pass;
- peak total process proportional-set or directly comparable physical-memory evidence is
  ≤`3.5 GiB` on the 6 GB device, with no low-memory kill or memory warning attributable
  to the Player;
- ring residency, texture/mesh category residency, allocations, GC, shader, LOD,
  overdraw, draw, triangle, renderer, particle, light, and collision/nav evidence is
  complete and within budget;
- Android frame pacing is enabled/verified or an accepted incompatibility is recorded;
- no visible quality/adaptive oscillation, thermal-only early pass, crash, or ANR occurs;
- protected cues and section 11 remain true in low/reduced/accessibility modes; and
- every mandatory scorecard row is `PASS` or justified `N/A`, with no unresolved
  `BLOCKED` or `FAIL`.

All three repetitions and all required golden scenes must pass on the same exact floor
configuration. An Editor, emulator, higher-RAM SKU, another OEM, another device with the
same chipset, a cold short run, or an average FPS cannot substitute.

## 9. Realm and installation envelopes

Installation rollups are partitioned by artifact residency owner, then hash-deduplicated.
No artifact may be charged to a smaller pack while actually pulled into a larger/base
pack by dependencies.

| Package ownership | Compressed delivery ceiling | Installed world-asset ceiling |
| --- | ---: | ---: |
| shared/neutral launch world assets | 256 MiB | 512 MiB |
| each realm's world assets | 256 MiB | 512 MiB |
| all four realms plus shared/neutral, unique union | 1.25 GiB | 2.5 GiB |
| event pack before event approval | 0 | 0 |
| later-ecosystem reserved classes before replacement/approval | 0 | 0 |

The `2.5 GiB` value is the total installed ceiling for the active world-asset inventory,
not permission for the whole application to ignore code, characters, animation, audio,
UI, localization, cache, patch, or free-storage budgets. The application-wide release
package must report those categories separately and show the world-asset union inside
its total.

Per realm, report:

- unique shared bytes referenced;
- unique realm-owned bytes;
- 3D, 2.5D, VFX, and technical-helper subtotals;
- every family subtotal, including explicit zero/deferred values;
- largest scene resident union and largest interaction/prefetch/horizon ring;
- duplicated hashes across bundles, which must be zero unless an approved platform
  packaging constraint documents the duplication; and
- compressed download, installed bytes, patch delta, and temporary update headroom.

A realm with missing families does not pass because its byte total is low.

## 10. Deterministic rollup equations

Let `A(X)` be the set of canonical assets applicable to scope `X`, and `H(a)` the set of
immutable runtime artifact hashes in asset `a`'s dependency closure.

```text
unique_bytes(X, metric) =
  Σ size(h, metric) for h in UNION(H(a) for a in A(X))

visible_triangles(cell, camera, state) =
  Σ visible_instances(a) × triangles(a, selected_lod)
  + both LODs during cross-fade

visible_draws(cell, camera, state) =
  Σ visible renderer passes after measured batching/instancing
  + cross-fade, shadow, depth, and transparent passes

family_variant_count(family, realm) =
  count(distinct canonical base assets assigned to family and realm)

realm_install(realm) =
  unique_bytes(shared ∪ realm ∪ realm_25d, installed)

scene_resident(scene, state) =
  unique_bytes(active ∪ prefetch ∪ horizon ∪ scene_global, resident)
  + measured runtime-instance/animation/VFX overhead

world_asset_install_total =
  unique_bytes(all active shared, realm, 2.5D, VFX, and technical artifacts, installed)
```

`metric` is exactly one of `compressed_delivery`, `installed`, `resident`, or
`load_io`; each artifact manifest carries all applicable values as distinct integers.
`compressed_delivery` comes from the final APK/split/asset-pack report, `installed` from
the on-device installed-file inventory, `resident` from the physical-device memory
capture, and `load_io` from the measured transition trace. A value from one metric may
not populate another.

A hash-shared texture is counted once in bytes but each visible material pass still
counts. A multi-family asset is counted once in bytes, once per visible instance in
runtime pressure, and once in each family coverage report. `max`, not average, selects
the passing scene/cell/realm result. Estimates may guide work; production pass uses
measured build and device evidence.

The downstream validator emits canonical UTF-8 JSON reports sorted by class ID, family
ID, realm ID, scene ID, cell ID, asset ID, then artifact hash. Running the rollup twice
against identical bytes must produce an identical SHA-256.

## 11. Accessibility and gameplay-readability gates

These are binary gates. A scene average, beauty score, or strong hero asset cannot hide
a failed ordinary surface.

### 11.1 Silhouette and protected-cue retention

- Each applicable record names its protected cues in the inventory.
- Validate LOD0, mobile-normal, low, far/HLOD/impostor, effects-off, emission-off,
  grayscale, and approved reduced-motion states from at least front, rear, and two
  orthogonal gameplay-relevant views.
- At the taxonomy's intended decision distance, landmarks occupy at least `64 px` in
  their critical dimension; entrances, route branches, gates, hazards, and objectives
  at least `32 px`; interactable/harvestable sources at least `24 px` before focus UI.
  If world scale/camera cannot provide that coverage, add a truthful non-world-space
  cue rather than enlarging collision or inventing glow.
- At `64 px` silhouette height, a lower tier retains at least `80%` binary-mask
  intersection-over-union with the approved protected silhouette after alignment, unless
  an asset-specific packet defines a stricter metric.
- In a five-second still test, at least `9/10` representative participants identify the
  required route/function/threat/state without coaching. A small sample is diagnostic,
  but a result below `9/10` is a failure until corrected and retested.

### 11.2 Contrast and color-independent state

- Critical text and small symbols meet contrast ratio ≥`4.5:1`; large text and essential
  boundaries/icons meet ≥`3:1` against their measured adjacent background.
- Realm, allegiance, threat, route, availability, depletion, lock, damage, construction,
  contest, and teleport state use shape, pattern, position, motion timing, label/icon,
  or physical geometry in addition to hue.
- Every critical state remains distinguishable in grayscale and protan, deutan, and
  tritan simulations. Emission, bloom, particles, reflection, and audio can reinforce
  but never carry the only cue.
- Low effects, reduced motion, reduced flashes, and effects-off preserve the same
  authoritative state and interaction possibility.

### 11.3 Interaction and harvestability

- Available, focused/targeted, active, success, depleted/empty, regenerating/cooldown,
  locked/disabled, and denied states use at least two simultaneous non-color cues: one
  physical/silhouette cue and one icon, prompt, pattern, or localized label cue.
- Focus and prompt sockets are catalogued and stable. The prompt does not appear through
  walls, across an impassable boundary, or for an unavailable action.
- Harvestable and non-harvestable members of the same visual family cannot differ only by
  color or glow. Depleted geometry cannot look more actionable than available geometry.
- Tool contact, selection proxy, interaction trigger, collision, and visual bounds are
  separately validated. A larger visual cue never silently increases interaction range.

### 11.4 Signage and text

- Direction, facility, hazard, notice, and contract signs use an icon/shape grammar plus
  localized text where text is required. No rasterized prose is baked into a shared
  world texture.
- Critical route decisions are visible at least `20 m` or `3 s` of travel at the
  approved route speed before commitment, whichever is stricter.
- World-space critical text renders with at least `18 px` capital height at its intended
  reading point and obeys text scaling, safe area, localization expansion, and
  right-to-left requirements. If it cannot, an accessible focus/interaction panel repeats
  the same semantics.
- Signage supplements topology, landmarks, map/minimap, and route edges; it is never the
  sole warning for a lethal hazard, PvP boundary, or required objective.

### 11.5 Traversal clarity

- Primary routes preserve the approved `6 m` visual width and service routes `4 m` where
  those families apply. A walkable route keeps at least `1.5 m` unobstructed clear width;
  gates, teleport controls, objectives, and interaction clusters keep a `2.5 m` clear
  approach unless gameplay authority specifies a larger envelope.
- Walkable, blocked, drop, climb, teleport, breakable, and decorative openings have
  distinct non-color reads. A visible opening never grants navigation authority.
- Route edges, grade changes, landings, bridge ends, cave thresholds, and destination
  sockets remain readable in low light, effects-off, and far tiers.
- At every authored decision point, the intended next legal route is identified within
  five seconds by `9/10` representative participants without map coaching.

### 11.6 Clutter density and central scan protection

- Non-interactive clutter occupies at most `20%` of the walkable footprint of a 128 m
  budget cell and at most `15%` of pixels in the protected central route/interaction scan
  region at mandatory camera anchors.
- No clutter, foliage, banner, particles, roof/canopy group, or prop may obscure more than
  `10%` of a critical interaction target, entrance, hostile telegraph, objective, or
  route-decision silhouette for longer than `1 s`.
- Repetition is controlled through shared clustered sets, not extra unique materials or
  unbounded random scatter. Low mobile reduces distant small props and foliage density
  before landmarks, route edges, interactables, harvestables, signs, gates, or hazards.
- Occlusion/cutaway groups expose complete backing surfaces. Hiding clutter or roofs may
  not reveal voids, remove collision truth, or select a different target.

### 11.7 Evidence set

Each admitted family supplies same-anchor LOD/quality/accessibility captures, measured
contrast, silhouette masks/IoU, grayscale and color-vision simulations, effects-off and
reduced-motion evidence, route/interaction five-second results where applicable, and
clutter/occlusion overlays. Device captures use the physical mobile floor. The inventory
stores evidence references and the independent accessibility gate decision.

## 12. Graceful degradation order

When a scene approaches any aggregate ceiling, reduce in this order:

1. ambient/cosmetic particles and unshadowed decorative lights;
2. noncritical decals, damage-number frequency, weather/reflection/fog density;
3. distant shadows and small-prop/foliage density;
4. secondary motion and distant animation frequency;
5. texture mip bias within the accepted material-read limit;
6. distant model detail and HLOD range;
7. render scale within approved image stability.

Never reduce player/target/attacker visibility, hostile telegraph truth, collision or
navigation truth, objective/interaction/harvest state, support fields, non-color cues,
realm-defining construction/silhouette, navigation landmarks, readable text, or touch
interaction size. A fallback only moves to a cheaper authored representation; a missing
low tier never promotes to an expensive tier or silently culls protected truth.

## 13. Exception process

An exception is narrow, time-bounded, additive evidence. It never edits a class ceiling
in place or authorizes a whole family by precedent.

Every `exceptionRef` resolves one immutable record containing:

- exception ID and revision;
- exact asset IDs, family ID, realm/scene/cell, platform tier, and affected metric;
- current class ceiling, measured value, requested delta, and expiration/review trigger;
- why reducing breadth, reuse, LOD/material/texture cost, or implementation complexity
  cannot preserve the approved read;
- accountable owner and implementation owner;
- profiler/build/device evidence, same-anchor visual/readability evidence, and source
  artifact hashes;
- aggregate offset: what other cost is reduced so every cell, scene, realm, and install
  envelope still passes;
- rollback/fallback plan and regression test;
- technical-art and runtime-engineering decisions;
- gameplay, world/architecture, narrative, or accessibility decision where that
  authority is affected; and
- final project-owner approval with UTC date and evidence references.

An exception fails when rationale, owner, measured evidence, aggregate offset, rollback,
or any required approval is missing. An exception may exceed a per-record ceiling but
may not exceed the frame, cell, scene, realm, installation, protected-cue, or
accessibility envelopes. Changing an aggregate envelope requires a new owner-approved
version of this standard and renewed physical-device evidence, not an exception.

Reserved ecosystem classes cannot use an exception to become active. They require a
separate approved taxonomy/budget transition and all ordinary gates.

## 14. Downstream validation contract

The inventory assembly/validator must fail closed on:

1. taxonomy hash/count drift or any family resolving zero/multiple classes;
2. a class-count report that differs from section 5;
3. an active record using `wbud_reserved_*`, or a reserved record with positive
   production/release approval, runtime placement, or nonzero packaged bytes;
4. variants/states beyond class limits without a complete exception;
5. LOD ratios, triangles, materials, draws, texture dimensions/formats, impostor mode,
   collision, navigation, compressed-delivery/installed/resident/load-I/O bytes,
   activation, or VFX values over class;
6. missing exact measurements for a claimed pass;
7. duplicate artifact bytes hidden by paths/aliases or a dependency charged to the wrong
   package;
8. any interaction/prefetch/horizon cell, scene, realm, or installation overrun;
9. mobile evidence that omits the full-frame and world-asset sub-budgets;
10. absent or failed readability/accessibility evidence;
11. exception reference without immutable rationale/owner/evidence/approval; or
12. non-canonical ordering or non-byte-stable rollup output.

Required deterministic reports are:

- family-to-class coverage and class counts;
- per-record measured-versus-ceiling report;
- per-family/per-realm variant and byte rollups;
- per-cell interaction/prefetch/horizon rollups;
- per-golden-scene maximum visible/resident/runtime rollups;
- realm/shared/event/2.5D package and unique-install union;
- duplicate-hash and cross-bundle dependency report;
- readability/accessibility evidence coverage;
- exception ledger; and
- mobile-floor pass/fail summary with raw evidence references.

## 15. Source account and disposition

Whole-file line-read sources:

- `PostMVP_World_Asset_Taxonomy_v1.md` — 551 lines;
- `PostMVP_World_Asset_Catalog_Binding_And_Production_Standard_v1.md` — 537 lines;
- `PostMVP_Graphics_Benchmark_Spec_2026-08-25.md` — 416 lines;
- `PostMVP_Sustained_Physical_Android_Benchmark_Procedure.md` — 424 lines;
- `PostMVP_Graphics_And_UI_Quality_Standard.md` — 321 lines;
- `AnotherLife_Blender_Asset_Production_Contract.md` — 496 lines; and
- `FourRealm_Modular_Construction_Envelope.md` — 147 lines.

Total directly line-read source: 2,892 lines, plus targeted schema reads covering profile,
record, geometry/material/LOD/impostor/collision/navigation/streaming, approval, and
production-admission conditions in
`al-world-asset-inventory.schema.json`.

Disposition:

- taxonomy families assigned: `242/242`, exactly one class each;
- active budget classes: concrete per-record and aggregate ceilings defined;
- deferred fantasy-beast/monster/dragon scope: planning-only reserved classes, zero
  admission authority;
- mobile-floor thresholds: concrete binary gates defined;
- readability/accessibility: binary evidence gates defined;
- aggregate math: cell, scene, realm, and installation equations defined;
- current MVP/runtime content: unchanged;
- generation and activation: not initiated; held behind the schema's independent gates
  and final project-owner authority.
