# Slagfall Quarry And Slagwhistle Burrower Visual Source

## Habitat Identity

Slagfall is an abandoned, weathered, mostly cool quarry—not a permanent lava
biome. Blunt terraced cuts and eroded extraction benches hold settled
overlapping glass-dark slag plates. Clay runoff crosses safe bench bands.
Heat-scarred matte stone, iron dust, dull clay, a rare deep recent-fracture
color, sparse ash scrub, mineral grass, and dark crust show long physical
settling.

The protected identity is
`terrace_edges_runoff_channels_cooled_slag_plate_direction`. Terrace edge,
runoff, safe bench, plate direction, and rock-spur horizon must survive
grayscale and the removal of red color, emission, sparks, ash, smoke, fog, and
heat shimmer.

## Relationships And Seams

- Habitat: `tdf_habitat_stonehold_slagfall_quarry`
- Kit: `tdf_envkit_stonehold_slagfall_quarry`
- Fauna: `tdf_fauna_stonehold_slagwhistle_burrower`
- Context-only elite: `tdf_elite_stonehold_slaghide_gorer`
- Material family: `tdf_matfam_stonehold_slate_iron`
- Existing Faultroad seam:
  `tdf_transition_stonehold_faultroad_escarpment_to_slagfall_quarry`
- Ore Gallery seam:
  `tdf_transition_stonehold_ore_gallery_mouths_to_slagfall_quarry`

Toward Faultroad, plates thin into iron soil, pale scree, and broken natural
grade. Toward Ore Gallery, benches narrow into settled mineral debris, wet
black contact stone, melt cuts, and only a few broad distant throats. These
seams are appearance proposals, not runtime boundaries.

## Eight-Family Kit

1. `tdf_prop_stonehold_slagfall_settled_cooled_slag_plate_cluster`
2. `tdf_prop_stonehold_slagfall_eroded_terrace_bench_bank`
3. `tdf_prop_stonehold_slagfall_clay_runoff_channel_cut`
4. `tdf_prop_stonehold_slagfall_broken_rock_spur_proxy`
5. `tdf_prop_stonehold_slagfall_heat_scarred_rubble_cluster`
6. `tdf_prop_stonehold_slagfall_dull_recent_fracture_seam`
7. `tdf_prop_stonehold_slagfall_ash_scrub_mineral_grass_patch`
8. `tdf_prop_stonehold_slagfall_dark_mineral_crust_edge`

The fracture seam is non-emissive and locally dull; it is not a required
warning color. Required particles, dynamic lights, active water, and emission
families are zero. For a future `128 × 128 m` review cell, target `6–8 MiB`
unique compressed habitat data with a `12 MiB` hard ceiling.

## Standard Adult Slagwhistle

- Variant: `slagwhistle_cooled_bench_adult`
- Scale: `0.90` Champion heights long, `0.38` at the shoulder
- Rig: `tdf_rig_quad_burrower`
- Protected identity:
  `paired_foreclaws_triangular_ear_folds_short_counterweight_tail`
- Materials: soot-brown opaque hide, glass-dark claw caps, pale scar tissue,
  dull clay underside, iron contact dust

The anatomy lock is a non-rodent wedge skull with protected horizontal slit
nostrils; exactly two triangular vascular folds rooted behind the skull and
lying flat along the shoulders for digging; one broad shovel cap plus two
short stabilizer claws on each forefoot; compact hind drive; and one short
flattened counterweight tail.

Motion intent: sentry freeze, shoulder-loaded plant, alternating cut, backward
settled-spoil push, low four-beat scurry, stop, fold-open vent, fold-close,
closed-mouth pressure whistle posture, and recovery. No particle or vapor is
required. Deeper-quarry broad-claw and cooled-bench long-hind variants remain
text-only and unpictured.

Provisional production envelope: `8k–10k` LOD0 triangles, `34–42` bones, one
material preferred and two maximum, one `1K` color/normal/packed set, six
clips, `3–4 MiB` target and `7 MiB` hard maximum. Suggested LOD1 is `55–60%`,
LOD2 `20–25%`, and distant `6–8%`. Preserve wedge, paired shovel caps, two
folds, and tail before hide microdetail.

## Authority Boundary

No terrain dimensions, bench safety, runoff physics, active cracks, burrows,
audio, navigation, collision, spawns, AI, combat, rewards, crafting, saves,
streaming, or device acceptance is defined. User approval and later
coordination review remain required before engineering.
