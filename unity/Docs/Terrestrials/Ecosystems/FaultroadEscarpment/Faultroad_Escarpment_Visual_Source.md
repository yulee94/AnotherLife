# Faultroad Escarpment Visual Source

## Decision Record

- Issue: `#259`
- Source version: `tdf-eco-faultroad-2026-07-27-v001`
- Parent roster: `tdf-eco-2026-07-27-v001`
- Foundation normalization:
  `tdf-foundation-fauna-normalization-2026-07-27-v001`
- Primary mode: Codex terrestrial design
- Habitat: `tdf_habitat_stonehold_faultroad_escarpment`
- Environment kit: `tdf_envkit_stonehold_faultroad_escarpment`
- Placement fauna: `tdf_basalt_grazer`
- Habitat state: `ReadyForUserReview`
- Basalt placement evidence: `ReadyForUserReview`
- Overall QA: `PassWithConcern`
- User decision: `NotRequested`
- Runtime integration: `Blocked`

This is the first exact Stonehold habitat-source companion. It pairs one
bounded environment kit with an immutable existing fauna identity to prove
scale, contact, distance reduction, transition, and reuse intent without
creating runtime authority.

## Protected Habitat Identity

The non-color identity is:

`diagonal_fault_broken_natural_grade_fractured_crown_ridge`

The source protects these elements together:

1. one dominant diagonal fault direction across the composition;
2. an eroded ascending shelf band interrupted by displacement and talus;
3. a compressed horizon of irregular tilted crown fins;
4. strong separation between near fault lip, mid travel shelf, and high ridge;
5. open visibility with sparse wind-pruned scrub;
6. asymmetrical natural fractures without built or quarried cadence.

Color, fog, particles, ore sparkle, dramatic weather, and dynamic light cannot
carry route, depth, or habitat identity.

## Landform And Composition

- Review composition unit: one provisional `128 m × 128 m` cell.
- Primary structure: tilted charcoal slate beds cut by one dominant fault.
- Travel appearance: one broad natural erosion shelf, never a constructed road.
- Interruption: angular talus and offset lips break continuous edges.
- Horizon: `tdf_landmark_stonehold_faultroad_fractured_crown`.
- Ground: iron-gray grit with bounded muted ochre soil seams.
- Organic family: one sparse dry scrub family.
- Required particles: `0`.
- Required dynamic or shadowed lights: `0`.

Long straight retaining faces, repeated rectangular joints, even risers,
paving, cut-block rhythm, crenellation, and quarry benches are rejected.

## Materials

| Role | Review color | Requirement |
| --- | --- | --- |
| primary slate | `#2E302F` | rough, dark, non-glossy tilted beds |
| fracture face | `#4B4C48` | cool iron-gray value separation |
| weathered dust | `#6D6659` | sparse protected shelf buildup |
| iron soil | `#70513D` | muted ochre-brown, no red emission |
| mineral inclusion | `#7B7565` | dull physical fleck, never glowing ore |
| dry scrub | `#514B3D` | sparse opaque branch mass |

No obsidian, active lava, bright ore, magical crystal, or metallic spectacle is
required.

## Neighbor Transition Studies

`tdf_transition_stonehold_faultroad_escarpment_to_rimecut_pass`

- the broad shelf narrows into a wind-cut notch;
- paler fracture faces and restrained frost traces increase;
- ridge fins compress around the notch;
- no whiteout, snowstorm, ice crystal, or glow is required.

`tdf_transition_stonehold_faultroad_escarpment_to_slagfall_quarry`

- the natural grade broadens into darker settled slag-like terraces;
- iron soil and rounded weathered debris increase;
- crown fins lower into broken shelves;
- no active lava, mine, machinery, cut quarry, or industrial meaning appears.

These are appearance studies only. They do not define a zone boundary, route,
navmesh, streaming cell, encounter, or spawn rule.

## Environment Kit

Exactly eight proposed natural families:

1. `tdf_prop_stonehold_faultroad_tilted_fault_slab`;
2. `tdf_prop_stonehold_faultroad_fractured_lip`;
3. `tdf_prop_stonehold_faultroad_crown_fin_cluster`;
4. `tdf_prop_stonehold_faultroad_medium_talus`;
5. `tdf_prop_stonehold_faultroad_small_scatter`;
6. `tdf_prop_stonehold_faultroad_iron_soil_shelf`;
7. `tdf_prop_stonehold_faultroad_runoff_groove`;
8. `tdf_prop_stonehold_faultroad_wind_scrub`.

Slab, lip, fin, and talus families should reuse topology through rotation,
nonuniform scale within approved bounds, material masks, and sparse decals.
They must not become repeated tiling landmarks.

## Basalt Grazer Placement

The exact fauna identity remains governed by
`unity/Docs/Terrestrials/Ecosystems/FoundationFauna/`.

Placement evidence protects:

- shoulder height `1.4` and length `3.0` Champion units;
- low shield silhouette and broad shoulder plate;
- short planted legs and four-foot ground contact;
- muted charcoal plate and ochre hide hierarchy;
- herbivore posture with no saddle, rider, combat, or domestication cue;
- family presence in the low/mobile composition without making it a landmark.

The sheet does not establish population, spawn density, temperament, herd
behavior, route ownership, hostility, rewards, or habitat exclusivity.

## Exact Source Set

| Asset ID | Review role |
| --- | --- |
| `tdf_asset_habitat_stonehold_faultroad_escarpment_establishing_master_v001` | establishing composition, grayscale, landform silhouette, reduced atmosphere, Grazer contact and low/mobile presence |
| `tdf_asset_habitat_stonehold_faultroad_escarpment_layout_transition_v001` | elevated composition, illustrative depth section, both neighbor transitions, grayscale and distance silhouettes |
| `tdf_asset_habitat_stonehold_faultroad_escarpment_material_lod_v001` | eight kit families, six material groups, LOD silhouettes, reduced composition and natural fracture contact |

## Provisional Low/Mobile Envelope

- Unique rock/ground prop families: `7`
- Organic prop families: `1`
- Natural landmark families: `1`
- Visible terrain/surface layers: `4`
- Required particles: `0`
- Dynamic lights: `0`
- Required water families: `0`
- Target unique compressed habitat content: `5–7 MiB`
- Hard parent ceiling: `10 MiB`
- Preferred shared material instances: `2`
- Maximum simultaneous material families: `4`

These are source targets, not measured runtime budgets.

## Quality Reduction

Remove in this order:

1. mineral flecks, dust variation, small scrub, and smallest scatter;
2. micro-fracture, minor runoff detail, and secondary talus;
3. distant fin subdivisions and nonessential material breakup.

Never remove:

- dominant diagonal fault;
- offset ascending grade;
- near/mid/high value separation;
- fractured crown silhouette;
- Basalt Grazer's low shield presence when the family is meant to be visible.

Reduced quality uses no atmosphere, particles, emission, dynamic light, or
gloss dependency.

## Reuse And Exclusions

Allowed semantic reuse:

- `tdf_matfam_stonehold_slate_iron`;
- shared terrain breakup, packed-mask, shoreline-free ground, and distant
  proxy grammar;
- instanced slab/talus modules and one sparse scrub family;
- existing Basalt Grazer exact identity.

Prohibited reuse:

- Fault-Crowned Colossus body plates or silhouette as terrain;
- Stonehold architecture walls, battlements, stairs, gates, or roads;
- Rimehorn Breaker horn/crystal language;
- cut quarry benches or mine entrance grammar;
- generated pixels as production textures;
- representative placement interpreted as spawn authority.

## Non-Authority And Handoff

This packet does not decide player-facing names, lore, cultural meaning,
routes, terrain topology, navmesh, collision, spawn, AI, combat, stats,
rewards, quests, saves, streaming, pooling, shaders, VFX, scenes, prefabs,
Addressables, device floors, or builds.

After exact user approval, coordination/review must define measured plan and
section evidence, supported camera/pixel coverage, topology and LOD thresholds,
terrain/prop density, streaming, pooling, memory, fallback behavior, and
acceptance captures. Engineering may then build from approved source without
redesigning it.
