# Creature Diversity And Terrestrial Optimization Source

## Control

- Issue: `#259`
- Primary Codex mode: `terrestrial-design`
- Source version: `tdf-eco-2026-07-27-v002`
- Packet state: `DiversityTaxonomyProposed`
- User creative approval: `NotRequested`
- Runtime integration: `Blocked`
- Narrative naming/localization: `WorkingLabelsOnly`

This source note converts the user creature-diversity and terrestrial-design reference list into production-safe design lanes. It does not approve final creature names, lore, spawn tables, combat roles, AI, rewards, save data, scenes, prefabs, shaders, or asset imports.

The goal is breadth without turning the world into a checklist. A family enters production only when it has a distinct silhouette, habitat function, material language, movement grammar, scale band, accessibility read, and low/mobile presentation path.

## Category Consolidation

| User reference | Source lane | Current decision |
| --- | --- | --- |
| Elven creatures | `realm_symbiotic_fae_fauna` | Allowed only as naturalized ecology: root, moon, lake, wind, or ancient-settlement symbiosis. Requires narrative review before any cultural label becomes player-facing. |
| Dark Elven creatures | `subterranean_umbral_fae_fauna` | Allowed as cave, ashwood, shadow-littoral, or ruined-threshold ecology. Avoids black repaint design; must change anatomy, posture, material, or movement. |
| Dragons | `apex_draconic_source` | Deferred as separate apex/singular source. Small dragon-like ambient creatures may use wing, scale, or heat grammar only when they are not miniature palette copies of future dragons. |
| Terrestrial-specialized creatures | `biome_engineered_body_plan` | Core lane for #259. Body plan follows terrain: climbers, diggers, waders, runners, thermal gliders, cave wallers, ridge browsers. |
| Animal-like | `naturalized_fauna` | Allowed when silhouette is not a direct real-world animal copy. Must have one original structural anchor. |
| Celestial | `rare_cosmic_living_material` | Deferred for limited high-contrast events or high-realm source. Must stay readable without glow on low/mobile. |
| Demonic | `infernal_mutation_or_ash_biome` | Allowed mainly in Umbral and ruin or ritual overlays. Must use anatomy/material consequences, not horns plus red emission. |
| Mutated | `ecological_deformation` | Allowed as habitat pressure: ore pressure, root symbiosis, storm glass, ash scarring, long exposure. No random asymmetry without function. |
| Sea creatures | `coastal_littoral_and_lake_fauna` | Lake, marsh, shore, river-mouth, and shallow sea lanes are allowed; deep pelagic source remains deferred pending world geography. |
| Swamp creatures | `wetland_root_silt_fauna` | Active Eldergrove and Umbral wetland lane; must remain legible when water reflection, fog, and particles are reduced. |
| Undead | `death_preserved_ecology` | Allowed only as restrained ruin/crypt ecology with narrative review. No generic skeleton mobs without source identity. |
| Vampiric | `blood_parasitic_or_nocturnal_ecology` | Allowed as nocturnal feeding grammar, membrane, proboscis, or symbiosis. Player-facing vampiric meaning requires narrative approval. |
| Human-like | `humanoid_out_of_scope_for_terrestrial_fauna` | Out of scope for this terrestrial-fauna packet; belongs to character, NPC, or narrative source. |
| Insect-like | `arthropod_chitin_fauna` | Active lane across caves, ruins, fields, slag, and wetland. Must avoid simple giant bug scaling by changing joint plan, shell geometry, or habitat interaction. |
| Bird-like | `avian_and_glider_fauna` | Active lane for ridges, fields, storm shelves, canopy, and ash ravines. Must define perch/contact logic and distant flight silhouette. |
| Magical creatures | `arcane_material_state` | Allowed as a secondary material or motion layer only after the base creature reads without VFX. |

## Terrestrial Environment Lanes

| Lane | Inputs | Source use |
| --- | --- | --- |
| `geologic_extreme` | volcanic, ice, mountain, canyon, caves | stone, rime, ore, slag, fault, ash, cavern forms |
| `living_wetland_forest` | forest, swamp, lake, oasis | roots, bark, canopy, silt, shallow water, reed, flooded basin |
| `open_civil_land` | grassland, farms, windmills, roads, cities | readable horizon, field rhythm, travel route, settlement adjacency |
| `fortified_and_ruined` | castles, fortress, wall, ruins, cathedral | stonework overlay, ruined gardens, crypt thresholds, parapet ecology |
| `shore_and_sea_edge` | sea, lake, marsh | shallow water, shore vegetation, waterline staining, shell/chitin ecology |
| `ritual_overlay` | ritualistic / voodoo reference | fictional ritual residue requiring narrative review; never a default biome |

Each lane must have one low/mobile landmark silhouette, one reduced-atmosphere version, one traversal-safe ground read, one habitat-fauna interaction rule, and a material/prop reuse plan.

## Sight Range And Streaming Design Intent

| Quality target | Default source-review sight range | User-adjustable intent | Design requirement |
| --- | ---: | --- | --- |
| `low_mobile` | `250 m` habitat / `100 m` fauna validation | small safe increments only after profiling | landmarks use broad shape; fauna uses silhouette and gait |
| `balanced` | `400 m` habitat / `170 m` fauna validation | moderate extension where memory permits | one extra neighbor proxy band; no hidden objective info |
| `high_pc` | `625 m` habitat / `270 m` fauna validation | larger ranges where GPU/CPU/storage profiling passes | distant proxy and LOD identity remain authored |
| `cinematic_offline` | shot dependent | not a gameplay setting | can exceed runtime limits, never packaged as default |

The player may be allowed to enlarge sight range later, but the game must ship with safe defaults. Lower settings can remove detail, density, reflection, particles, shadows, and secondary motion; they cannot remove route, objective, interactable, or threat readability.

## Family Selection Rules

Before any new family receives visual source:

1. Pick one primary lane and at most two secondary altered-state tags.
2. Record why the family cannot be represented by an existing family variant.
3. Define scale, locomotion, habitat contact, silhouette anchor, and material anchor.
4. Define the low/mobile identity first.
5. Define which higher-tier layers are optional.
6. Avoid player-facing cultural, religious, undead, vampiric, celestial, or demonic labels unless Codex narrative/content has approved the intent.
7. Keep variants structural only when needed; palette-only variants are texture or ecotype variations, not new families.

## Immediate Candidate Backlog

These are design-source candidates, not production commitments:

| Candidate lane | Example source direction | Reason to consider |
| --- | --- | --- |
| `subterranean_umbral_fae_fauna` | cave-nocturnal membrane climber with mineral sensory plates | covers dark-elven reference without humanoid culture claim |
| `realm_symbiotic_fae_fauna` | lake-root glider whose wing ribs grow around old bark scars | covers elven/magical reference through ecology |
| `death_preserved_ecology` | crypt-garden shellback with pale tendon-root probes | covers undead, ruin, and cathedral lane without skeleton cliché |
| `blood_parasitic_or_nocturnal_ecology` | moonlit proboscis swift tied to wetland insects | covers vampiric lane without humanoid vampire lore |
| `shore_and_sea_edge` | shallow-sea reef isopod with opaque shell and tide contact | extends sea lane without deep-ocean production scope |
| `ritual_overlay` | ash-charcoal scavenger with woven-object avoidance behavior | supports ritual space only after narrative-safe source review |

## Handoff Boundary

This v002 note can guide the next exact visual-source selection. It does not make any category approved, required, or runtime-ready.

The next terrestrial-design step should select a small batch, preferably `4–6` families, for exact visual source:

- one grounded fauna;
- one avian/glider;
- one arthropod/shell;
- one wetland/littoral;
- one altered magical ecology;
- optional one architecture-adjacent fauna.

Each selected family must return exact source identity, views, scale, material callouts, motion intent, low/mobile reduction, and user-review state before engineering receives a handoff.
