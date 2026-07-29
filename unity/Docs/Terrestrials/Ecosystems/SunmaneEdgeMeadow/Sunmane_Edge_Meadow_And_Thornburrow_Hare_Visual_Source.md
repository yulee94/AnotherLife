# Sunmane Edge Meadow And Thornburrow Hare Visual Source

## Design Thesis

Sunmane is an adult, natural forest-boundary meadow: low rolling grass and
shallow drainage folds open broad sightline pockets between a broken tree line
and one distant old-growth mass. Dun soil, subdued green-gold grass, weathered
stone, dry seed heads, dark exposed roots, sparse thorn scrub, and low
pollinator plants carry the image. Permanent bloom fields, pollen spectacle,
fantasy emission, constructed root piles, and decorative water are exclusions.

The protected habitat identity is
`forest_boundary_sightline_pockets_drainage_fold`. Route readability comes from
path compression, the forest boundary, terrain folds, and large-fauna value
separation—not particles or color alone.

## Stable Relationships

- Habitat: `tdf_habitat_eldergrove_sunmane_edge_meadow`
- Realm: `eldergrove`
- Kit: `tdf_envkit_eldergrove_sunmane_edge_meadow`
- Supporting fauna: `tdf_fauna_eldergrove_thornburrow_hare`
- Context-only elite: `tdf_elite_eldergrove_sunmane_thornstag`
- Shared material family: `tdf_matfam_eldergrove_wet_bark_root`
- Mirrorroot seam proposal:
  `tdf_transition_eldergrove_mirrorroot_littoral_to_sunmane_edge_meadow`
- Hollowbark seam proposal:
  `tdf_transition_eldergrove_sunmane_edge_meadow_to_hollowbark_oldgrowth`

The transition IDs are appearance-only source proposals and are not
user-approved runtime contracts. No reverse duplicates are introduced.

Toward Hollowbark, grass height and root density increase before spaced trunks
form a readable open understory. Toward Mirrorroot, soil lowers, darkens, and
drains into mud fans, root shelf, pale water-worn stones, and only then a low
water plane. The core Sunmane kit requires no active water.

## Bounded Environment Kit

The kit has exactly eight semantic families:

1. `tdf_prop_eldergrove_sunmane_shallow_drainage_fold_bank`
2. `tdf_prop_eldergrove_sunmane_wind_combed_grass_mass`
3. `tdf_prop_eldergrove_sunmane_weathered_meadow_stone_cluster`
4. `tdf_prop_eldergrove_sunmane_exposed_root_edge`
5. `tdf_prop_eldergrove_sunmane_dry_seed_head_clump`
6. `tdf_prop_eldergrove_sunmane_low_pollinator_plant_patch`
7. `tdf_prop_eldergrove_sunmane_sparse_thorn_scrub`
8. `tdf_prop_eldergrove_sunmane_distant_broken_tree_line_proxy`

Paired sizes in the concept sheet are variations within a family, not extra
families. Required particles, dynamic lights, emission families, and core water
families are all zero. Reduced motion freezes secondary grass waves but retains
one authored broad wind direction.

For a future `128 × 128 m` review cell, target `6–8 MiB` unique compressed
habitat data with a hard parent ceiling of `12 MiB`. These are provisional
source targets, not measured runtime claims.

## Thornburrow Hare

The standard adult is a small meadow grazer and root scraper. Its immutable
identity is `hind_leg_wedge_paired_root_tusks_flat_counterbalance_tail`:

- shoulder height `0.32` Champion units; length `0.75`;
- low adult head and long rear-leg wedge;
- short upright sensory ears;
- two small lower-jaw root-scraping tusks;
- flat blade-like counterbalance tail;
- coarse dun fur, pale belly, dark keratin, seed-caught guard hair;
- prolonged freeze, low-head scrape, two-stage bound, lateral landing
  correction, and rapid cover fold.

Only one standard adult is pictured. Perspective differences do not establish
juveniles, sexual dimorphism, population ratios, or size variants. The
wet-edge structural variant remains text-only and unpictured.

Provisional production envelope: `6k–8k` LOD0 triangles, `26–34` bones, one
material preferred and two maximum, `5–6` clips, one `1K` color/normal/packed
set, `2–3 MiB` target and `6 MiB` hard maximum. Suggested geometric reductions
are LOD1 `55–60%`, LOD2 `20–25%`, and distant `5–8%`. Exact measured blockout,
rig, deformation, texture, and device evidence remain blocked.

## Engineering Handoff Boundary

No runtime routes, terrain topology, water simulation, navmesh, collision,
spawns, AI, combat, stats, loot, quests, saves, catalogs, streaming,
Addressables, or performance acceptance are defined. Engineering must consume
approved source and may not silently correct or replace its identity.
