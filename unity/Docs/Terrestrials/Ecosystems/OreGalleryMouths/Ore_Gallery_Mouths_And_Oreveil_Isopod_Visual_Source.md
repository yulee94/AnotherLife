# Ore Gallery Mouths And Oreveil Isopod Visual Source

## Habitat Identity

Ore Gallery is a low natural rock wall punctured by only a few broad,
asymmetric collapsed throats. Massive surviving rock columns remain visibly
tied into fractured bedding. A dusty-slate safe ground plane crosses the
exterior and first chamber; stepped settled debris, cold melt cuts, wet black
contact stone, pale calcite fractures, sparse fungal crust, and rootless cave
moss provide secondary structure.

The protected identity is
`gallery_mouth_load_bearing_columns_ground_plane_value`. Mouth, column, and
safe ground separation must survive grayscale, wet-specular-off, fog-off,
drip-off, dust-off, and emission-off presentation. Exterior bounce may reach
the first chamber; deeper darkness uses midtone grouping rather than colored
fog or black crush.

## Relationships And Seams

- Habitat: `tdf_habitat_stonehold_ore_gallery_mouths`
- Kit: `tdf_envkit_stonehold_ore_gallery_mouths`
- Supporting fauna: `tdf_fauna_stonehold_oreveil_isopod`
- Context-only elite: `tdf_elite_stonehold_oreblind_delver`
- Material family: `tdf_matfam_stonehold_slate_iron`
- Rimecut seam:
  `tdf_transition_stonehold_ore_gallery_mouths_to_rimecut_pass`
- Slagfall seam:
  `tdf_transition_stonehold_ore_gallery_mouths_to_slagfall_quarry`

The Rimecut ID matches the separate Rimecut packet proposal. Both are
appearance-only source IDs. Toward Rimecut, melt channels lead to frost,
compact snow remnants, and wind-cut ribs. Toward Slagfall, debris benches gain
glass-dark cooled plates, iron dust, and clay runoff without active lava,
sparks, heat shimmer, or industrial machinery.

## Eight-Family Kit

1. `tdf_prop_stonehold_ore_gallery_collapsed_throat_shell`
2. `tdf_prop_stonehold_ore_gallery_natural_column_buttress`
3. `tdf_prop_stonehold_ore_gallery_settled_debris_bench`
4. `tdf_prop_stonehold_ore_gallery_cold_melt_channel_cut`
5. `tdf_prop_stonehold_ore_gallery_safe_slate_ground_plane`
6. `tdf_prop_stonehold_ore_gallery_wet_black_contact_patch`
7. `tdf_prop_stonehold_ore_gallery_pale_calcite_fracture_seam`
8. `tdf_prop_stonehold_ore_gallery_fungal_moss_crust_patch`

Required particles, dynamic lights, active-water families, and emission
families are zero. The melt cut is topology plus optional static wetness, not a
required water simulation. For a future `128 × 128 m` review cell, target
`6–8 MiB` unique compressed habitat data and retain a `12 MiB` hard ceiling.

## Standard Adult Oreveil

- Variant: `oreveil_gallery_mouth_adult`
- Guild: cave detritivore
- Scale: `0.60` Champion heights long, `0.36` wide, `0.22` tall
- Rig family: `tdf_rig_multiped_low`
- Protected identity:
  `overlapping_lateral_plates_recessed_head_sensory_front_edge`
- Materials: matte iron-gray chitin, chalky molt seams, polished contact
  edges, pale flexible membrane; no exposed ore or glow

The production anatomy lock is one broad blunt sensory front shield, exactly
nine overlapping primary trunk plates, one compact tail latch, exactly seven
locomotor leg pairs, and two smaller debris-sifting mouth appendages. The
recessed head has no visible eyes and no long antennae. The defensive curl is
an asymmetric oval coil whose shield seats against the tail latch; it is not a
perfect ball and does not authorize rolling locomotion.

Motion intent: vibration freeze, grouped ripple crawl, wall-edge feel, debris
sift, side-slope correction, controlled compression, shield-to-tail latch,
compact curl, uncurl, and recovery. The deeper-gallery plate-count/front-width
variant remains text-only.

Provisional production envelope: `7.5k–9.5k` LOD0 triangles, `28–36` bones,
one material preferred, one `1K` color/normal/packed set, six bounded clips,
`2–3 MiB` target and `6 MiB` hard maximum. Seven leg pairs share three phase
channels; distant tiers replace individual leg motion with a stable underside
fringe. Suggested LOD1 is `50–55%`, LOD2 `18–22%`, and distant `5–7%`.

## Authority Boundary

No cave topology, first-chamber depth, collision, navigation, water, light,
spawns, AI, population, combat, rewards, quests, saves, streaming,
Addressables, or performance acceptance is defined. User approval and a later
coordination specification are required before engineering.
