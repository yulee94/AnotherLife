# Ecosystem Source Budgets And Asset Layout

## Control

- Issue: `#259`
- Source version: `tdf-eco-2026-07-27-v001`
- Primary Codex mode: `terrestrial-design`
- State: `RosterProposed`
- Budget authority: provisional visual-source envelope
- Runtime authority: none

These values make the A2 packet production-aware. They are starting envelopes for concept, model, texture, animation, and environment-source review. They are not final runtime budgets and do not authorize a scene, rendering architecture, Addressables layout, view distance, spawn population, package split, or minimum device.

Codex coordination/review must convert approved source into a measured technical contract. Codex engineering must profile representative devices before any ceiling becomes production authority.

## Budget Principles

1. Low/mobile is the mandatory complete visual language, not a degraded afterthought.
2. Balanced and high tiers add surface, secondary motion, atmosphere, and close detail; they cannot change required navigation, objective, interactable, or threat information.
3. Silhouette and broad material value are protected before particles, translucency, small props, strand systems, decals, or dynamic lights.
4. Common, realm, habitat, and family assets are separated so one local view does not retain the whole world.
5. Variants reuse topology, rigs, animation grammar, normal maps, packed masks, and material families where the approved identity permits.
6. Concept sheets, editable sources, high-resolution bake sources, marketplace archives, render caches, and cinematic-only assets never enter Player packaging.
7. Cross-realm travel requires low-tier silhouettes and navigation source for all four realms. Account realm lock must not be used to omit the other realms permanently.

## Quality Tiers

| Tier ID | Intent | Required availability |
| --- | --- | --- |
| `low_mobile` | Broad-device baseline with complete silhouette, route, state, and material-value identity | mandatory |
| `balanced` | Normal PC and capable mobile presentation with richer material and environment density | optional scalable tier |
| `high_pc` | Close-view PC presentation with selective high-resolution assets and secondary response | optional add-on |
| `cinematic_offline` | Shot-specific source for promotional or offline rendering | never packaged in Player |

Advanced/custom settings are not approved by this packet. They may expose profiled ranges later, but they cannot exceed safe streaming, memory, thermal, and readability bounds silently.

## Supporting Fauna Envelope

Values are per supporting family and include one base family identity. Optional ecotypes must reuse the approved base topology and sources unless a separate visual review proves a structural change.

| Measure | `low_mobile` | `balanced` | `high_pc` | `cinematic_offline` |
| --- | ---: | ---: | ---: | ---: |
| Maximum LOD0 skinned triangles | `16,000` | `32,000` | `55,000` | shot-dependent |
| Maximum deform bones | `56` | `72` | `96` | `160` source ceiling |
| Maximum material slots | `2` | `2` | `3` | `6` source ceiling |
| Texture intent | one shared/unique `1K` color, normal, packed-mask set | one `2K` set; variants reuse normal/packed maps | `2K`, with one selective `4K` hero map only when pixel coverage proves need | `4K–8K` source only |
| Core animation clips | `5–6` | `7–9` | `9–12` | shot-dependent |
| Maximum compressed animation content | `2.5 MiB` | `5 MiB` | `8 MiB` | not packaged |
| Maximum active particles for ambient identity | `24` | `48` | `96` | offline only |
| Maximum dynamic lights | `0` | `0` normally | `1` optional, shadowless by default | offline only |
| Maximum incremental compressed content | `6 MiB` | `15 MiB` | `32 MiB` optional add-on | `0` Player bytes |

LOD ratios inherit the merged boss/elite source:

- LOD1: `55–65%` of LOD0 silhouette cost.
- LOD2: `20–30%`.
- Distant: `5–10%` or an authored impostor/silhouette proxy.

Small supporting fauna should normally target the lower half of each ceiling. A family may not consume the ceiling merely because the number exists.

### Animation source floor

Every family selected for visual production needs:

- one readable rest/breath or weight-shift pose;
- one contact-verified primary locomotion;
- one turn or direction-change response;
- one alert/freeze or environment-observation state;
- one habitat interaction appropriate to its anatomy;
- one recovery to neutral.

These are motion-source requirements, not AI states. Reduced-motion keeps the same pose sequence and timing information while removing decorative secondary motion, persistent emission, and rapid surface pulsing.

## Habitat Kit Envelope

Values are incremental unique content after common and realm-level deduplication. A natural landmark is a geology, water, root, tree, cliff, or erosion anchor—not a building.

| Measure | `low_mobile` | `balanced` | `high_pc` | `cinematic_offline` |
| --- | ---: | ---: | ---: | ---: |
| Unique prop mesh families | `8–12` | `14–20` | `20–28` | shot-dependent |
| Vegetation/organic mesh families | `2–3` | `4–5` | `6–8` | shot-dependent |
| Natural landmark families | `1` | `1–2` | `2–3` | shot-dependent |
| Ground/surface texture intent | one `2K` atlas/set | up to two `2K` atlases/sets | selective `4K`; shared masks/normals retained | `4K–8K` source only |
| Foliage/decal texture intent | one `1K–2K` atlas | up to two `2K` atlases | selective `4K` only where overdraw remains bounded | source only |
| Maximum pooled ambient systems | `2` | `3` | `6` | offline only |
| Maximum incremental compressed content | `12 MiB` | `36 MiB` | `80 MiB` optional add-on | `0` Player bytes |

The target, before profiling, is to land below the maximum:

- common low habitat identity: `6–8 MiB` unique per habitat;
- common low supporting family: `3–4 MiB` unique per family;
- common realm surface kit: `20 MiB` or less per realm;
- terrestrial shared low kit: `48 MiB` or less.

## Reference Review Cell

The reference cell is a design and profiling unit of `128 m × 128 m`. It does not set a runtime terrain-tile size.

### Visual density

| Measure | `low_mobile` | `balanced` | `high_pc` |
| --- | ---: | ---: | ---: |
| Maximum terrain layers visible in one cell | `4` | `6` | `8` |
| Ambient particles, excluding boss bursts | `80–140` | `160–240` | `300–480` |
| Local dynamic lights | `0` | `1` shadowless maximum | `2` maximum |
| Local shadowed lights | `0` | `0` by default | `1` maximum |
| Active water surface families | `1` | `2` | `3` |
| Unique visible material families | `12` | `20` | `32` |
| Full-detail habitat cells | `1` | `2` | `4` |
| Neighbor proxy cells | `4` | `8` | `12` |

### Resident art-memory target

This is the intended maximum resident working set for the active reference cell and transition reserve. It excludes engine, code, UI, audio, networking, save data, and platform overhead.

| Art-memory category | `low_mobile` | `balanced` | `high_pc` |
| --- | ---: | ---: | ---: |
| Terrain, vegetation, and prop textures | `40 MiB` | `80 MiB` | `160 MiB` |
| Visible supporting-fauna textures | `24 MiB` | `48 MiB` | `96 MiB` |
| Static geometry and instance buffers | `16 MiB` | `32 MiB` | `64 MiB` |
| Skinned geometry and animation clips | `16 MiB` | `32 MiB` | `64 MiB` |
| VFX, decals, water, and atmosphere | `12 MiB` | `24 MiB` | `48 MiB` |
| LOD cross-fade, transition, and streaming reserve | `20 MiB` | `40 MiB` | `80 MiB` |
| **Total resident art target** | **`128 MiB`** | **`256 MiB`** | **`512 MiB`** |

If a protected silhouette exceeds the envelope, remove secondary appendages, material layers, transparency, particles, decals, and close-only detail before changing the approved primary body or landmark shape.

## Provisional Visibility Review

Distances are source-review targets for silhouette sheets and graybox profiling, not final camera or culling values.

| Review target | `low_mobile` | `balanced` | `high_pc` |
| --- | ---: | ---: | ---: |
| Supporting-fauna close material read | `0–18 m` | `0–30 m` | `0–45 m` |
| Supporting-fauna full silhouette read | `80–120 m` | `140–200 m` | `220–320 m` |
| Habitat horizon/landmark silhouette | `200–300 m` | `320–480 m` | `500–750 m` |
| Proposed default fauna validation distance | `100 m` | `170 m` | `270 m` |
| Proposed default habitat validation distance | `250 m` | `400 m` | `625 m` |

At every tier:

- distant fauna identity uses silhouette, gait footprint, and motion cadence;
- required threat/objective state must have an equivalent non-detail presentation from the later technical specification;
- fog, vegetation, particles, and water cannot hide mandatory route or interaction information;
- lower tiers may simplify the scene, not the rules.

## Asset-Family Layout

### Shared source families

```text
tdf_common/
  material_grammar/
    opaque_hide_keratin
    opaque_feather
    opaque_chitin
    wet_hide
    opaque_shell
  motion_grammar/
    heavy_quadruped_contact
    hind_drive_contact
    avian_soarer
    ground_avian
    multiped_phase
    amphibious_low
    winged_invertebrate
    gastropod_segment
  environment_grammar/
    terrain_macro_breakup
    path_and_shore_edges
    packed_surface_masks
    distant_silhouette_proxies
```

These are authoring and deduplication groups. They are not shader, Animator, renderer, or runtime class requirements.

### Realm source families

```text
tdf_realm_stonehold/
  slate_iron_surface_family
  frost_scree_organic_family
  dust_snow_atmosphere_family

tdf_realm_eldergrove/
  wet_bark_root_surface_family
  meadow_littoral_organic_family
  moisture_pollen_atmosphere_family

tdf_realm_crownlands/
  chalk_weathered_stone_surface_family
  grass_grain_garden_organic_family
  wind_storm_atmosphere_family

tdf_realm_umbral/
  ash_obsidian_surface_family
  ashwood_crust_organic_family
  heat_ash_rift_atmosphere_family
```

### Habitat source families

Every habitat receives one bounded unique family:

```text
tdf_envkit_<realm>_<habitat-slug>
```

The family contains only identity that cannot be shared safely:

- one protected landform/horizon set;
- one natural landmark set;
- unique erosion, water, root, or geology pieces;
- a small prop/organic accent set;
- habitat transition pieces for both neighbors;
- low, balanced, high, and distant presentation intent.

It does not contain settlement architecture, creature prefabs, spawn markers, gameplay colliders, quests, or runtime catalog records.

### Fauna source families

```text
tdf_fauna_<realm>_<family-slug>/
  silhouette_and_scale_source
  anatomy_and_material_source
  motion_and_contact_source
  approved_base_topology_later
  approved_lod_and_impostor_source_later
```

Existing IDs are never renamed to fit this path. Source folders and runtime bundle labels are later engineering decisions.

## Proposed Package Boundaries

These names describe the intended separability of source. They are not Addressables labels or build implementation.

```text
terrestrial-common-low
terrestrial-realm-<realm>-low
terrestrial-habitat-<realm>-<habitat>-low
terrestrial-fauna-<realm>-<family>-low
terrestrial-<scope>-balanced-addon
terrestrial-<scope>-high-addon
cinematic-source-only
```

Rules:

- `low` contains the complete required silhouette, route, habitat, and state language.
- `balanced-addon` and `high-addon` may add texture resolution, secondary props, richer atmosphere, and close motion only.
- high-tier packages depend on the low identity package and never duplicate its full normal, mask, animation, or topology sources unnecessarily.
- inactive local habitats may stream detail, but cross-realm low proxies remain obtainable because the long-term game crosses realm borders.
- cinematic source has no Player dependency.

## Low-Tier Install-Size Planning Check

The following arithmetic is a design guardrail, not a delivery promise:

| Source group | Maximum low-tier contribution |
| --- | ---: |
| Shared terrestrial low kit | `48 MiB` |
| Four realm surface kits at `20 MiB` each | `80 MiB` |
| Sixteen habitat kits at target `8 MiB` each | `128 MiB` |
| Sixteen supporting families at target `4 MiB` each | `64 MiB` |
| **Proposed ecosystem/supporting-fauna subtotal** | **`320 MiB`** |

The subtotal excludes the already specified boss/elite packages, audio, UI, architecture, characters, gameplay, engine, and platform files. It is a maximum planning subtotal before cross-family deduplication. Engineering must report measured imported and compressed size, retained dependency graphs, and the effect of on-demand delivery before acceptance.

No source author may use this subtotal to justify filling unused budget.

## Reuse And Deduplication Rules

- Reuse skeleton topology only when joint count, contact pattern, deformation, and protected silhouette remain compatible.
- Reuse animation grammar before reusing exact clips; gait timing must preserve mass and anatomy.
- Variants share normal maps and packed masks unless a structural surface change proves a separate bake is required.
- Realm tint or weathering uses bounded masks/parameters; it cannot create a new family by itself.
- Feather, fur, moss, web, grass, and membrane density use cards or shell geometry on mobile. Strand systems remain cinematic source.
- Prefer opaque materials. Alpha clipping or blending requires a visual role that cannot be achieved with cheaper geometry.
- Combine small habitat props into atlases and instanced families; do not make one unique material per prop.
- Water, fog, particles, and lighting are quality layers and cannot carry the only path, depth, threat, or objective cue.
- Distant proxies preserve the same protected horizon and family silhouette, not a generic realm-colored blob.

## Architecture And Narrative Boundary

The ecosystem packet may specify:

- terrain grade and clearance where an approved building kit can sit;
- vegetation, erosion, drainage, and natural material transition around an architecture placement;
- natural occlusion and horizon composition;
- fictional ritual-site visual constraints that avoid real-world cultural shorthand.

It may not specify:

- new castles, fortresses, walls, cities, cathedrals, farms, windmills, monuments, or building modules;
- building functions, construction states, grid sizes, economy, or interaction;
- ownership, worship, history, faction meaning, language, or player-facing names.

Those decisions remain in the approved architecture source and later narrative/content source.

## Required Technical Handoff Evidence

Before engineering integration, Codex coordination/review must define:

- final target devices, memory class, frame-time, thermal, and battery criteria;
- runtime cell/terrain representation, streaming lifecycle, load cancellation, fallback, and recovery;
- view-distance and LOD transition policy, including cross-fade overlap;
- renderer, triangle, draw-call, material, texture, overdraw, shadow, water, and VFX ceilings;
- creature population and pooling contracts without using this design roster as spawn authority;
- bundle/addressing/dependency ownership, patching, cache eviction, and unavailable-tier behavior;
- equivalent low-tier navigation, objective, interactable, and threat presentation;
- representative device test scenes and retained profiling evidence;
- source-to-runtime identity mapping and design-fidelity review.

## Unperformed Measurements

No production habitat mesh, terrain, vegetation set, supporting-fauna model, rig, animation, texture set, VFX system, scene, prefab, Addressable group, or Player build exists for this packet. Therefore:

- resident memory is not measured;
- compressed/build/install size is not measured;
- CPU/GPU/frame-time/thermal/battery cost is not measured;
- draw calls, triangles, overdraw, materials, lights, particles, water, and streaming spikes are not measured;
- device compatibility is not proven.

Every numeric value remains provisional until representative production-equivalent assets and devices produce retained evidence.
