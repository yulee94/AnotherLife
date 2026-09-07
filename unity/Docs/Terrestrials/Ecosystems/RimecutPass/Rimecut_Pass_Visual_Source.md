# Rimecut Pass Visual Source

## Design Thesis

Rimecut is a wind-cut pass rather than a whiteout arena or ice palace. Compact
blue-gray ice shelves lie across dark rock ribs. Broad safe ledges and one
recognizable pass notch remain distinct from steep avalanche chutes. Alternating
low snow saddles and exposed stone fins carry the horizon; frost appears only
on exposed faces. Sparse silver sedge and dark moss survive in lee pockets.

The protected identity is
`pass_notch_safe_ledge_edge_stone_rib_direction`. Pass notch, ledge edge, stone
rib direction, and large footprints must survive grayscale, low saturation,
weather-off, sparkle-off, and reduced motion.

## Stable Relationships

- Habitat: `tdf_habitat_stonehold_rimecut_pass`
- Environment kit: `tdf_envkit_stonehold_rimecut_pass`
- Existing placement fauna: `tdf_fauna_stonehold_rimefan_kite`
- Context-only elite: `tdf_elite_stonehold_rimehorn_breaker`
- Shared material family: `tdf_matfam_stonehold_slate_iron`
- Existing Faultroad seam:
  `tdf_transition_stonehold_faultroad_escarpment_to_rimecut_pass`
- Proposed Ore Gallery seam:
  `tdf_transition_stonehold_ore_gallery_mouths_to_rimecut_pass`

The second transition ID is an appearance-only A2 proposal. Neither seam
defines a runtime boundary, traversal rule, streaming cell, encounter, or
spawn.

Toward Faultroad, compact ice and snow thin into pale fracture faces, coarse
scree, restrained frost traces, and a broader broken natural grade. Toward Ore
Gallery Mouths, melt channels expose dusty slate, settled natural debris, and
only a few broad non-emissive collapsed cave throats. No mine, rail, timber,
machinery, or glowing ore meaning is introduced.

## Bounded Environment Kit

Exactly eight semantic families are allowed:

1. `tdf_prop_stonehold_rimecut_wind_cut_ice_shelf_slab`
2. `tdf_prop_stonehold_rimecut_safe_ledge_edge_bank`
3. `tdf_prop_stonehold_rimecut_exposed_stone_rib_cluster`
4. `tdf_prop_stonehold_rimecut_coarse_snow_static_drift_mass`
5. `tdf_prop_stonehold_rimecut_avalanche_chute_settled_fan`
6. `tdf_prop_stonehold_rimecut_cold_melt_channel_cut`
7. `tdf_prop_stonehold_rimecut_lee_sedge_moss_patch`
8. `tdf_prop_stonehold_rimecut_distant_pass_notch_saddle_proxy`

Sheet variations remain inside these families and do not create additional
catalog entries. Required particles, dynamic lights, active-water families,
and emission families are all zero. Reduced motion removes blown snow and
sparkle while retaining authored static drift direction in surface form.

For a future `128 × 128 m` review cell, target `6–8 MiB` unique compressed
habitat data with a hard parent ceiling of `12 MiB`. Prefer two shared material
instances, no more than four simultaneous material families, LOD1 at `50–60%`,
LOD2 at `18–25%`, and distant form at `5–8%`. These are provisional source
targets, not measured runtime results.

## Rimefan Placement Boundary

The packet consumes, without copying:

- `tdf_asset_fauna_stonehold_rimefan_kite_turnaround_v001`;
- `tdf_asset_fauna_stonehold_rimefan_kite_motion_material_v001`.

The existing immutable read remains
`diamond_wing_deep_chest_short_wedge_tail`, at approximately `2.3` Champion
heights wingspan. Placement demonstrates cold-shelf scale, ledge brace, and
ridge-soar intent only. The generated views may not correct or supersede the
existing `PassWithConcern` skull, five-group wing, tail, or measured-scale
findings. No flock, population, nest, prey, variant, behavior frequency, or
spawn authority is created.

## Engineering Handoff Boundary

No topology, slope, ledge width, fall boundary, avalanche simulation, wind,
weather, footprints, navigation, collision, camera, spawn, AI, combat, stats,
loot, quests, save data, Addressables, streaming, or performance acceptance is
defined. Those require user-approved source followed by coordination review.
