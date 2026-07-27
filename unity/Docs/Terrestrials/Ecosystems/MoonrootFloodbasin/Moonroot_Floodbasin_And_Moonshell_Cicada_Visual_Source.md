# Moonroot Floodbasin And Moonshell Cicada Visual Source

## Decision Record

- Issue: `#259`
- Source version: `tdf-eco-moonroot-2026-07-27-v001`
- Parent roster source: `tdf-eco-2026-07-27-v001`
- Primary Codex mode: `terrestrial-design`
- Habitat: `tdf_habitat_eldergrove_moonroot_floodbasin`
- Habitat visual variant: `moonroot_deep_flood`
- Environment kit: `tdf_envkit_eldergrove_moonroot_floodbasin`
- Supporting fauna: `tdf_fauna_eldergrove_moonshell_cicada`
- Visualized fauna ecotype: `moonshell_flood_season`
- Pending ecotype: `moonshell_dry_season`
- State: exact candidate source ready for user review
- User decision: not requested
- Runtime integration: blocked

This is the first exact habitat-source companion to the four-realm ecosystem
roster. It pairs one environment kit with one supporting family so scale,
contact, transition, reduction, and source budgets can be reviewed together.
It does not advance the roadmap phase or rewrite the parent roster.

## Protected Habitat Identity

The non-color identity is:

`root_islands_water_depth_root_openings`

The source protects these elements together:

1. a low horizontal water plane beneath a high root canopy;
2. few dominant spaced trunk columns rather than a dense vegetation wall;
3. asymmetric root islands and exposed natural root ramps;
4. pale submerged shelves, midvalue shallows, and dark channels;
5. large negative-space openings through root masses;
6. a split-buttress landmark with one tall narrow opening and one lower broad
   opening.

Color, reflections, fog, caustics, particles, bloom, and emission may enrich a
higher tier but cannot communicate route, depth, landmark, or habitat identity.

### Landform And Composition

- Review composition unit: one provisional `128 m × 128 m` cell.
- Base landform: black silt and irregular submerged water-worn bedrock.
- Water: one dark family with authored depth-value bands.
- Dominant masses: three trunk/root families with open ground-plane sightlines.
- Landmark: `tdf_landmark_eldergrove_moonroot_split_buttress`.
- Secondary islands: two or three offset low root islands, never a regular
  stepping-stone path.
- Organic families: restrained moss/decay mat and sparse water-edge growth.
- Required particles: `0`.
- Required dynamic or shadowed lights: `0`.

The landmark is ancient root anatomy, not architecture. Its unequal openings
must remain irregular under every LOD and cannot become a gate, vault, bridge,
house, shrine, ruin, carved symbol, or guided-growth structure.

### Water And Material Hierarchy

| Role | Review color | Requirement |
| --- | --- | --- |
| deep water | `#182321` | darkest navigable/non-navigable separation; no glow |
| black silt | `#171715` | matte bank and submerged base |
| saturated root | `#3A2E27` | dominant wet-bark mass |
| moss mat | `#56604A` | protected planes only |
| water-worn stone | `#777A70` | irregular eroded shelf, never paving |
| pale root scar | `#B6AB8B` | sparse structural direction cue |
| living root join | `#8B8644` | restrained non-emissive tissue |
| cool reflection | `#8A9A99` | optional highlight, never identity |

Repeated rectangular slabs, even risers, concentric paving cadence, right-angle
retaining edges, and stair silhouettes are rejected. Production must reauthor
the remaining repeated shelf cadence documented by visual QA.

### Neighbor Transition Studies

`tdf_transition_eldergrove_moonroot_floodbasin_to_hollowbark_oldgrowth`

- channels narrow and recede;
- root islands become broad exposed ramps;
- dark soil and leaf litter increase;
- spaced trunks and an open understory replace the low-water horizon.

`tdf_transition_eldergrove_moonroot_floodbasin_to_mirrorroot_littoral`

- root islands lower into flooded shelves;
- water becomes clearer and shallower;
- reeds and broad low leaves increase;
- overhead root mass opens toward a distant tree-line horizon.

These are appearance studies only. They do not define terrain tiles, routes,
colliders, zone borders, encounters, or streaming.

## Protected Fauna Identity

The non-color identity is:

`roof_fold_wing_vertical_rest_heavy_thorax`

The pictured base is `moonshell_flood_season`. It is a grounded fantasy
invertebrate, not a literal enlarged cicada, beetle, moth, or shell-backed
animal.

### Scale

Adult Champion height is `1.0`.

- Body length: `0.35`
- Spread wingspan: `0.70`
- Folded rest height: `0.18–0.22`
- Folded roof width: `0.18–0.22`

The shared source corrects an oversized generated base. The accepted placement
must remain a large insect, never a boss-scale monster.

### Anatomy Contract

- exactly one head;
- one high, heavy thorax;
- one short abdomen with six ordered broad bands;
- exactly six legs;
- forelegs with opposed bark hooks;
- middle legs forming the widest lateral brace;
- compact hind launch femora without grasshopper proportions;
- exactly two broad opaque forewing panels forming one pitched roof seam;
- exactly two shorter hindwing folding fans;
- two short lateral sensory combs;
- small recessed eyes;
- one ventral blunt feeding rostrum, never a bird beak.

The flood-season ecotype uses broad fore- and midfoot contact pads. The
`moonshell_dry_season` proposal remains text-only: narrower pads, a thicker roof
edge and ridge, and an abdomen shortened by about ten percent. This source does
not establish seasons, transformation, rarity, sex, population, or gameplay.

### Materials

| Role | Review color | Requirement |
| --- | --- | --- |
| heavy thorax | `#49362D` | wet bark-brown, opaque |
| abdomen | `#2B2521` | deep umber, six readable bands |
| wing panels | `#89928A` | opaque mica-like chitin |
| ridge/edge wear | `#B1B39E` | restrained physical wear |
| joint membrane | `#B8AA94` | pale bounded separation |
| hook/contact wear | `#5D5145` | dull, non-emissive |

Wing identity cannot depend on transparency, a vein-net, pearlescent cycling,
specular outline, or glow. Bark resemblance follows material direction and
roughness rather than literal bark pasted onto the body.

### Motion And Contact

Required source sequence:

1. vertical shell-still rest with restrained abdomen expansion;
2. head-up climb: forehooks reach, middle pair braces, hind pair advances;
3. trunk turn retaining at least three contacts;
4. rostrum-to-bark feeding contact;
5. launch anticipation: hind pair compresses and roof seam opens;
6. body clears bark, hindwings separate, one decisive downstroke;
7. low direct canopy arc without hover or ornamental loop;
8. forehook-first landing, middle brace, hind settle;
9. hindwing fold beneath the forewings;
10. roof closure and settled recovery.

Reduced motion removes wing vibration, membrane flutter, specular pulsing,
particles, and foliage response while preserving release, direction, contact,
fold, and recovery.

## Exact Source Sheet Set

| Asset | Required role |
| --- | --- |
| `tdf_asset_habitat_eldergrove_moonroot_floodbasin_establishing_master_v001` | horizon, Champion scale, grayscale, reduced reflection/motion |
| `tdf_asset_habitat_eldergrove_moonroot_floodbasin_layout_transition_depth_v001` | elevated plan, cross-section, depth bands, both neighbor transitions |
| `tdf_asset_habitat_eldergrove_moonroot_floodbasin_material_reduced_atmosphere_lod_v001` | eight prop families, split landmark, materials, LOD, low/mobile composition |
| `tdf_asset_fauna_eldergrove_moonshell_cicada_turnaround_v001` | folded/spread views, anatomy, scale, silhouette, trunk contact |
| `tdf_asset_fauna_eldergrove_moonshell_cicada_motion_material_v001` | climb, turn, feed, launch, flight, land, fold, recovery, materials |
| `tdf_asset_shared_eldergrove_moonroot_moonshell_contact_scale_readability_v001` | corrected placement scale, folded LODs, black silhouettes, reduced habitat, contact |

## Reuse And Optimization Intent

Allowed semantic reuse:

- `tdf_rig_invertebrate_winged` control grammar;
- `tdf_matfam_opaque_chitin`;
- `tdf_matfam_eldergrove_wet_bark_root`;
- common terrain breakup, shoreline, packed-mask, and distant-proxy grammar;
- one shared opaque chitin micro-normal;
- one water family across the habitat;
- root modules instanced and rotated rather than duplicated;
- topology and packed masks between future ecotypes only when their structural
  deltas remain real.

Prohibited reuse:

- Mere-Root Leviathan root-fan topology, materials, pixels, or placement;
- Eldergrove building roots, roofs, graft collars, or architecture motion;
- literal real cicada, moth, beetle, or luna-moth anatomy;
- transparent-wing identity;
- palette-only seasonal variants;
- generated pixels shipped as production textures;
- representative contact interpreted as spawn or population authority.

### Provisional Low/Mobile Habitat Envelope

- Unique prop families: `8`
- Organic/vegetation families: `2`
- Natural landmark families: `1`
- Visible terrain/surface layers: `4`
- Active water families: `1`
- Required particles: `0`
- Dynamic lights: `0`
- Target unique compressed content: `6–8 MiB`
- Hard parent ceiling: `12 MiB`

### Provisional Low/Mobile Fauna Envelope

- LOD0 skinned triangles: `6,000–8,000`
- Deform bones: `24–32`; provisional hard target `36`
- LOD1 ratio: `55–60%`
- LOD2 ratio: `20–25%`
- Distant ratio: `5–8%` or an authored opaque proxy
- Materials: `1` preferred, `2` maximum
- Textures: one `1K` color/normal/packed-mask set
- Core clips: `6–8`
- Compressed animation target: at most `1.5 MiB`
- Unique family target: `3–4 MiB`; parent maximum `6 MiB`
- Required particles/lights: `0`

These are source targets, not measured implementation authority. Production
topology, bones, clips, streaming, and memory remain coordination/engineering
decisions after user approval.

## Cross-Quality Readability

Remove in this order:

1. floating debris, particles, caustics, reflection flicker, and fog;
2. water flowers, hanging-root density, and secondary moss;
3. cicada micro-ribs, edge wear, sensory-comb detail, and distal toe geometry;
4. secondary wing controls.

Never remove:

- split-buttress openings;
- pale shelf versus dark channel;
- root-island ramp direction;
- pitched roof forewings;
- heavy thorax;
- primary six-leg contact footprint.

The generic parent `100 m` fauna distance is not a measured species-recognition
claim for this small family. Source review separates:

- material read at approximately `0–18 m`;
- family silhouette around `20–40 m`;
- presence-only black shape at `100 m`;
- future `96 px`, `64 px`, and `32 px` coordination captures.

## Non-Authority

This packet does not decide final player-facing names, lore, culture,
localization, ritual meaning, or whether seasons exist. It does not decide
runtime IDs, terrain, navigation, colliders, water simulation, spawning,
population, pooling, AI, combat, stats, rewards, quests, save data, scenes,
prefabs, shaders, VFX, Addressables, device floors, or builds.

The existing Mere-Root Leviathan remains `ReadyForUserReview`,
`PassWithConcern`, and user-unapproved. This packet does not resolve its
recorded salamander-head concern or change any existing boss, elite,
architecture, or legacy-fauna source.

## Acceptance And Handoff

This companion may remain `ReadyForUserReview` only while:

- exact files, hashes, bytes, dimensions, LFS pointers, prompts, and lineage
  resolve;
- habitat depth/openings read without effects;
- anatomy retains six legs and two wing pairs;
- the flood-season ecotype is the only pictured ecotype;
- all concerns remain production-blocking rather than silently accepted;
- user creative state remains `NotRequested`;
- runtime integration remains `Blocked`.

After explicit user approval, Codex coordination/review must define measured
distances, camera/pixel coverage, LOD thresholds, streaming, memory, failure
behavior, population/pooling boundaries, and acceptance captures. Engineering
may then build from the approved source without redesigning it.
