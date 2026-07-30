# Slagfall Quarry And Slagwhistle Burrower Visual Source v002

## Corrective Intent

Version `tdf-eco-slagfall-2026-07-30-v002` is a visual correction of the
frozen v001 lineage. It directly resolves these concerns without changing
stable habitat, fauna, material-family, transition, or rig IDs:

- repeated plate fields that read as paving, stairs, or masonry;
- a volcanic-looking skyline spur;
- road-like continuous clay runoff;
- weak Ore Gallery throat silhouettes;
- a familiar mole/anteater creature read;
- ear-like heat folds;
- drifting forefoot and tail anatomy;
- motion poses that did not prove contact or weight transfer.

## Habitat Identity

Slagfall is an abandoned, weathered, mostly cool extraction basin in
Stonehold. It is not an active lava biome and contains no constructed quarry
architecture.

Protected identity:
`irregular_fracture_rafts_braided_runoff_collapsed_gallery_throats_diagonal_fault_planes`

The protected read is:

- a broad compressed quarry bowl;
- a small number of large, unequal, interlocked basalt fracture rafts;
- diagonal and radial breaks with missing corners, undercuts, soil intrusion,
  and talus interruption;
- discontinuous extraction cuts rather than repeated terraces or stairs;
- narrow runoff that splits, pools, crosses slopes, disappears under talus,
  and re-emerges;
- broad recessed collapsed Ore Gallery mouths;
- basalt thinning into iron soil, scree, and exposed diagonal fault planes
  toward Faultroad.

The quarry has no spire, monument, stable central landmark, gate, road, rail,
wall, stair, masonry, lava, glowing crack, or mandatory particle effect.

## Relationships And Seams

- Habitat: `tdf_habitat_stonehold_slagfall_quarry`
- Kit: `tdf_envkit_stonehold_slagfall_quarry`
- Fauna: `tdf_fauna_stonehold_slagwhistle_burrower`
- Context-only elite: `tdf_elite_stonehold_slaghide_gorer`
- Material family: `tdf_matfam_stonehold_slate_iron`
- Faultroad seam:
  `tdf_transition_stonehold_faultroad_escarpment_to_slagfall_quarry`
- Ore Gallery seam:
  `tdf_transition_stonehold_ore_gallery_mouths_to_slagfall_quarry`

The pictured seams are appearance direction only. They define no runtime
boundary, route, entrance, navigation permission, encounter, or loading gate.

## Eight-Family Kit

1. `tdf_prop_stonehold_slagfall_irregular_fracture_raft`
2. `tdf_prop_stonehold_slagfall_broken_fracture_raft`
3. `tdf_prop_stonehold_slagfall_undercut_extraction_ledge`
4. `tdf_prop_stonehold_slagfall_talus_apron`
5. `tdf_prop_stonehold_slagfall_collapsed_gallery_mouth`
6. `tdf_prop_stonehold_slagfall_diagonal_fault_slab`
7. `tdf_prop_stonehold_slagfall_braided_runoff_pool`
8. `tdf_prop_stonehold_slagfall_iron_soil_wedge`

Kit use must vary rotation, scale, breakup, soil coverage, and adjacency.
Repeated grid placement, clean rows, continuous curb-like edges, and
stair-step stacking are prohibited.

Required particles, dynamic lights, active-water families, and emission
families remain zero. For a future `128 × 128 m` review cell, target `6–8 MiB`
of unique compressed habitat data with a `12 MiB` hard ceiling.

## Standard Adult Slagwhistle

- Variant: `slagwhistle_cooled_bench_adult`
- Scale: `0.90` Champion body lengths; `0.38` Champion shoulder heights
- Rig family: `tdf_rig_quad_burrower`
- Protected identity:
  `wedge_skull_scapular_bracket_yoke_fused_shovel_palms_flattened_brace_tail`
- Materials: soot-brown opaque hide, charcoal mineral dust, dark-iron
  keratin, and restrained pale scar tissue at hinge and claw roots

Anatomy lock:

- low wedge skull integrated into the neck;
- protected horizontal slit nostrils;
- no visible pinnae or mammalian ear canals;
- exactly two broad keratin heat-vent folds rooted behind the skull along the
  scapular arch;
- closed folds lie flush as a bracket-shaped shoulder yoke;
- vented folds hinge outward only slightly and never behave as ears, wings,
  fins, or horns;
- exactly one fused crescent shovel palm per forefoot;
- exactly two short stabilizer claws per forefoot, one on either outer side of
  the shovel palm;
- compact hindquarters for push traction;
- one short, broad, dorsoventrally flattened paddle tail used as a ground
  brace.

The silhouette must stay recognizable without color through the wedge skull,
scapular yoke, fused shovel palms, low body, and flattened tail. It must not
become a mole, anteater, armadillo, pangolin, dog, or enlarged real animal.

## Motion And Contact

The accepted motion sheet establishes:

1. plant with both shovel palms seated and the shoulder yoke closed;
2. cut with one fused palm under a fractured edge and hindquarters compressed;
3. backward spoil push with planted hind drive and light tail brace;
4. normal-speed low scurry with believable foot contact;
5. forward-loaded stop without sliding;
6. calm vent with slight paired-yoke opening from visible scapular roots;
7. closed-yoke recovery to the neutral low stance.

Exactly two stabilizer claws remain present during motion. They do not become
digging fingers. No required dust, debris spray, vapor, speed line, spark,
glow, or other airborne effect is part of the identity.

## Provisional Production Envelope

- LOD0: `8k–10k` triangles
- Bones: `34–42`
- Materials: one preferred, two maximum
- Texture set: one `1K` color/normal/packed set
- Animation clips: six maximum
- Compressed fauna target: `3–4 MiB`
- Fauna hard ceiling: `7 MiB`
- LOD1: `55–60%`
- LOD2: `20–25%`
- Distant: `6–8%`

Reduction priority is wedge skull, shoulder yoke, paired shovel palms, low
body, and flattened tail before hide microdetail.

## Authority Boundary

No terrain measurements, gallery access, runoff physics, burrow behavior,
audio, navigation, collision, spawns, AI, combat, stats, rewards, crafting,
saves, streaming, or device acceptance is defined. User approval and A1
coordination review remain required before production engineering.
