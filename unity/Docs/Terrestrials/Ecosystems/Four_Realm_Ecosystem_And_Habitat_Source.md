# Four-Realm Ecosystem And Habitat Source

## Control

- Issue: `#259`
- Primary Codex mode: `terrestrial-design`
- Source version: `tdf-eco-2026-07-27-v001`
- State: `RosterProposed`
- Canonical realms: `crownlands`, `stonehold`, `eldergrove`, `umbral`
- Runtime authority: none
- Narrative authority: none
- Final visual approval: user

This packet completes the missing ecosystem and environment-design half of issue #259. PR #285 already supplied visual source for four outer-warzone bosses and twelve inner-realm elites. This packet connects those anchors to readable habitats and proposes supporting fauna without changing the existing pixels, creature identities, review findings, or approval states.

The work is intentionally pre-visual. It establishes a bounded roster, stable design IDs, ecological relationships, transition logic, and production-aware asset families before more concept images or production assets are created.

## A2 Decisions

### Category consolidation

The original creature and environment examples are directions, not one production folder per adjective. The following taxonomy prevents duplicate rigs, palette-only variants, and culturally incoherent asset growth.

| Input direction | A2 decision | Reason |
| --- | --- | --- |
| animal-like, bird-like, insect-like, lake, swamp, shore | Organize by anatomy, locomotion, scale, and ecological guild | These dimensions determine silhouette, motion, asset reuse, and habitat fit |
| celestial, demonic, mutated, undead, vampiric, magical | Treat as bounded altered-state or arcane-phenotype layers | An altered state must change anatomy, material behavior, or motion; a recolor is not a new family |
| dragons and singular giant creatures | Keep in the existing dragon, boss, or singular-apex source programs | They need bespoke approval and should not inflate the common-fauna kit |
| Elven and Dark Elven creatures | Treat as realm-associated ecotypes only after narrative/cultural review | A people name is not a biological body plan |
| human-like creatures | Exclude from this terrestrial-fauna packet | Humanoid enemies, peoples, and NPCs belong to character and content pipelines |
| sea creatures | Defer pelagic and deep-sea families | No approved sea region or traversal requirement currently gives them a production home |
| forest, swamp, ice, mountain, lake, ruins, caves, canyon, volcanic, grassland | Use as connected habitat modules | Habitats must transition into one another instead of becoming isolated theme parks |
| castles, fortresses, walls, cities, cathedrals, farms, windmills | Use as architecture/settlement overlays on habitats | The approved architecture program already owns building identity |
| desert and oasis | Defer pending an approved realm or neutral-zone geography | Adding them now would silently author the world map |
| “voodoo-inspired” spaces | Replace with fictional ritual-site or occult-remnant overlays pending narrative review | Avoid borrowing a living cultural/religious identity as decorative shorthand |

The production-facing creature categories are:

1. Natural supporting fauna, organized by ecological guild and body plan.
2. Bounded altered or arcane ecotypes that materially change an existing family.
3. Existing singular apex creatures with separate source and approval.

### Roster size

This source version fixes a reviewable foundation of four habitats and four supporting-fauna families per realm:

- one existing boss or elite visually anchors each habitat;
- one supporting family supplies non-apex ecosystem identity;
- every realm forms a four-habitat transition loop;
- the three merged foundation IDs are reused rather than renamed or duplicated;
- thirteen new supporting families remain `ProposedTextOnly`.

This is a source-review roster, not a promise that every family will ship.

## Global Habitat Rules

- A realm must remain identifiable in grayscale through landform, horizon rhythm, surface breakup, and vegetation/organic structure.
- Palette, fog, particles, banners, and emissive effects are supporting cues, never the only identity.
- Habitat borders use shared geology, drainage, vegetation succession, or weather logic. A hard material/color seam is not an acceptable transition.
- Traversable-looking routes preserve a readable foreground, middle-ground, and horizon. Dense atmosphere cannot hide required navigation or creature silhouettes.
- Natural landmarks use geology, water, vegetation, or erosion. Settlement landmarks are consumed from the approved architecture source and are not redesigned here.
- Small props, debris, particles, and secondary foliage disappear before path edges, major cover masses, landmark silhouettes, or creature recognition.
- Reduced-motion presentation removes decorative wind oscillation, looping glow, drifting debris, and rapid water or particle motion while preserving the same static composition and state information.
- Cinematic source may exceed gameplay detail only when it stays outside Player packaging and cannot become the low-tier identity.

## Connected Habitat Roster

| Realm | Habitat ID | Working label | Existing visual anchor | Supporting family |
| --- | --- | --- | --- | --- |
| Stonehold | `tdf_habitat_stonehold_faultroad_escarpment` | Faultroad Escarpment | `tdf_boss_stonehold_fault_crowned_colossus` | `tdf_basalt_grazer` |
| Stonehold | `tdf_habitat_stonehold_rimecut_pass` | Rimecut Pass | `tdf_elite_stonehold_rimehorn_breaker` | `tdf_fauna_stonehold_rimefan_kite` |
| Stonehold | `tdf_habitat_stonehold_ore_gallery_mouths` | Ore Gallery Mouths | `tdf_elite_stonehold_oreblind_delver` | `tdf_fauna_stonehold_oreveil_isopod` |
| Stonehold | `tdf_habitat_stonehold_slagfall_quarry` | Slagfall Quarry | `tdf_elite_stonehold_slaghide_gorer` | `tdf_fauna_stonehold_slagwhistle_burrower` |
| Eldergrove | `tdf_habitat_eldergrove_hollowbark_oldgrowth` | Hollowbark Oldgrowth | `tdf_elite_eldergrove_hollowbark_stalker` | `tdf_grove_strider` |
| Eldergrove | `tdf_habitat_eldergrove_mirrorroot_littoral` | Mirrorroot Littoral | `tdf_elite_eldergrove_mirrorfin_lurker` | `tdf_mire_lumenback` |
| Eldergrove | `tdf_habitat_eldergrove_sunmane_edge_meadow` | Sunmane Edge Meadow | `tdf_elite_eldergrove_sunmane_thornstag` | `tdf_fauna_eldergrove_thornburrow_hare` |
| Eldergrove | `tdf_habitat_eldergrove_moonroot_floodbasin` | Moonroot Floodbasin | `tdf_boss_eldergrove_mere_root_leviathan` | `tdf_fauna_eldergrove_moonshell_cicada` |
| Crownlands | `tdf_habitat_crownlands_crownstep_chalkland` | Crownstep Chalkland | `tdf_elite_crownlands_crownstep_lion` | `tdf_fauna_crownlands_broadcrest_aurochs` |
| Crownlands | `tdf_habitat_crownlands_galegrain_roadbelt` | Galegrain Roadbelt | `tdf_elite_crownlands_galeclaw_courser` | `tdf_fauna_crownlands_grainveil_covey` |
| Crownlands | `tdf_habitat_crownlands_reliquary_crypt_garden` | Reliquary Crypt Garden | `tdf_elite_crownlands_reliquary_basilisk` | `tdf_fauna_crownlands_reliquary_shellback` |
| Crownlands | `tdf_habitat_crownlands_meridian_storm_shelf` | Meridian Storm Shelf | `tdf_boss_crownlands_meridian_tempest_roc` | `tdf_fauna_crownlands_stormglass_swift` |
| Umbral | `tdf_habitat_umbral_ashvein_three_fault_rift` | Ashvein Three-Fault Rift | `tdf_boss_umbral_ashvein_triarch` | `tdf_fauna_umbral_sootsail_carrioner` |
| Umbral | `tdf_habitat_umbral_cinder_runoff_shelf` | Cinder Runoff Shelf | `tdf_elite_umbral_cindermaw_salamander` | `tdf_fauna_umbral_cinderplate_scarab` |
| Umbral | `tdf_habitat_umbral_ashwood_veil_ravine` | Ashwood Veil Ravine | `tdf_elite_umbral_veilspine_widow` | `tdf_fauna_umbral_ashstep_bounder` |
| Umbral | `tdf_habitat_umbral_graveglass_cavern_vale` | Graveglass Cavern Vale | `tdf_elite_umbral_gravewing_siphon` | `tdf_fauna_umbral_graveglass_sheller` |

Working labels are nonlocalized review text. They do not override narrative naming.

## Realm Habitat Identity

### Stonehold

Transition loop:

```text
Rimecut Pass
→ Faultroad Escarpment
→ Slagfall Quarry
→ Ore Gallery Mouths
→ Rimecut Pass
```

#### `tdf_habitat_stonehold_faultroad_escarpment`

- Landform: stepped basalt shelves split by a broad visible fault and a broken siege-road grade.
- Horizon: low compressed terraces beneath one fractured crown ridge; no needle-peak fantasy skyline.
- Surface language: charcoal slate, iron-brown scar faces, pale horn-colored scree, sparse black lichen, and restrained copper mineral staining.
- Organic language: wind-flattened grass, crust lichen, tough root mats in protected cracks.
- Weather/light: lateral dust and snow grains reveal the escarpment direction; cool open light with warm reflected stone only near protected cuts.
- Transition logic: snow pockets deepen toward Rimecut; slag plates and quarry spoil increase toward Slagfall.
- Readability: road edge, fault break, and crown ridge remain visible without dust, banners, or forge glow.
- Architecture boundary: may receive the approved Stonehold gate, wall, or workshop overlay; this packet does not alter those assets.

#### `tdf_habitat_stonehold_rimecut_pass`

- Landform: wind-cut ice shelves laid across dark rock ribs with broad safe ledges and avalanche chutes.
- Horizon: alternating snow saddles and exposed stone fins, with one readable pass notch.
- Surface language: compact blue-gray ice, dark slate, wind crust, coarse snow slab, and frost only on exposed faces.
- Organic language: silver sedge clumps and low dark moss in lee pockets.
- Weather/light: sparse directional snow and breath-visible cold; no permanent whiteout.
- Transition logic: ice thins into Faultroad scree and follows melt channels toward the Ore Gallery mouths.
- Readability: pass notch, ledge edge, and major creature footprints survive reduced saturation and reduced weather.
- Reduced motion: replaces blown snow with static drift direction and removes loose ice sparkle.

#### `tdf_habitat_stonehold_ore_gallery_mouths`

- Landform: broad collapsed cave throats, stepped tailings, load-bearing stone columns, and cold meltwater cuts.
- Horizon: a low dark wall punctured by a few large openings rather than many glowing holes.
- Surface language: matte mineral films, iron-polished contact faces, dusty slate, wet black stone, and pale calcite fractures.
- Organic language: fungal crust and rootless cave moss remain sparse and non-emissive.
- Weather/light: exterior bounce light reaches the first chamber; deeper darkness uses value grouping, not colored fog.
- Transition logic: meltwater traces back to Rimecut; tailings and vitrified waste lead toward Slagfall.
- Readability: cave mouth, safe ground plane, and vertical stone columns remain separable in grayscale.
- Reduced motion: removes dripping and dust-fall loops while retaining wetness and collapse direction.

#### `tdf_habitat_stonehold_slagfall_quarry`

- Landform: terraced quarry cuts filled with cooled slag plates, runoff channels, and abandoned extraction benches.
- Horizon: blunt quarry steps with one broken crane-height rock spur; no architecture silhouette is invented.
- Surface language: glass-dark slag, heat-scarred stone, iron dust, clay runoff, and rare dull red depth visible only in recent fractures.
- Organic language: ash scrub, mineral grass, and dark crusts colonize cooled edges.
- Weather/light: low heat refraction is localized to active cracks; most of the habitat is cool, matte, and physically settled.
- Transition logic: spoil terraces rise into Faultroad and narrow into the Ore Gallery tailings.
- Readability: terrace edges, runoff, and glassy plates read without sparks or emission.
- Reduced motion: disables heat shimmer and falling ash while retaining fracture value and plate orientation.

### Eldergrove

Transition loop:

```text
Sunmane Edge Meadow
→ Hollowbark Oldgrowth
→ Moonroot Floodbasin
→ Mirrorroot Littoral
→ Sunmane Edge Meadow
```

#### `tdf_habitat_eldergrove_hollowbark_oldgrowth`

- Landform: ancient trunk corridors, root buttresses, fallen-log bridges, and an open navigable understory.
- Horizon: tall spaced trunks and two or three dominant root arches; no dense wall of branches.
- Surface language: wet bark, dark soil, pale lichen, moss in protected planes, and weathered stone under roots.
- Organic language: large structural roots, restrained fern masses, leaf litter, fungi, and hanging growth only at focal points.
- Weather/light: broken canopy shafts and moisture haze; visibility is preserved at the ground plane.
- Transition logic: trunks open toward Sunmane; root channels descend and flood toward Moonroot.
- Readability: trunks, path gap, and root arches remain distinct without fireflies, pollen, or green glow.
- Architecture boundary: approved Eldergrove structures may use clearings and root-adjacent sockets but remain separate source.

#### `tdf_habitat_eldergrove_mirrorroot_littoral`

- Landform: clear lake margin, flooded root shelves, mud fans, reed islands, and shallow channels.
- Horizon: low reflective water broken by a few root masses and a distant tree line.
- Surface language: wet umber soil, dark roots, pale water-worn stone, reed tan, and silver-green surface reflections.
- Organic language: emergent reeds, broad low leaves, submerged stems, algae films, and restrained floating growth.
- Weather/light: reflected sky and wind ripples; water clarity varies by depth rather than magical color.
- Transition logic: shore rises into Sunmane meadow and deepens through root islands toward Moonroot.
- Readability: shoreline, safe shallows, root shelf, and deep-water value remain distinct without specular highlights.
- Reduced motion: uses a static water-value gradient and disables dense ripple, reed sway, and reflected flicker.

#### `tdf_habitat_eldergrove_sunmane_edge_meadow`

- Landform: open rolling grass, shallow drainage folds, forest edge, and broad sightline pockets.
- Horizon: low meadow swells under a broken tree line; one distant old-growth mass anchors orientation.
- Surface language: dun soil, layered green-gold grasses, weathered stone, dry seed heads, and dark root exposure.
- Organic language: meadow grasses, seed clusters, low pollinator patches, and sparse thorn scrub.
- Weather/light: warm directional backscatter with clear midtones; no permanent bloom or flower-particle field.
- Transition logic: grass height and root density increase toward Hollowbark; soil darkens and drains toward Mirrorroot.
- Readability: path compression, forest boundary, and large-fauna silhouette survive grayscale.
- Reduced motion: freezes secondary grass waves but preserves broad wind-combed direction in the authored mesh shapes.

#### `tdf_habitat_eldergrove_moonroot_floodbasin`

- Landform: deep flooded roots, broad water channels, root islands, and submerged stone shelves.
- Horizon: a low water plane under a high root canopy with a few large vertical trunks.
- Surface language: dark water, saturated bark, pale root scars, black silt, and restrained green-gold living tissue at real root joins.
- Organic language: hanging roots, submerged growth, moss mats, sparse water flowers, and decomposing wood.
- Weather/light: cool indirect light with brief reflected highlights; no fantasy fog wall.
- Transition logic: water shallows into Mirrorroot and climbs through exposed root ramps toward Hollowbark.
- Readability: island edges, water depth, and root openings remain legible with reflections and VFX disabled.
- Reduced motion: removes floating debris drift and rapid caustics, retaining a stable depth-value hierarchy.

### Crownlands

Transition loop:

```text
Crownstep Chalkland
→ Galegrain Roadbelt
→ Reliquary Crypt Garden
→ Meridian Storm Shelf
→ Crownstep Chalkland
```

#### `tdf_habitat_crownlands_crownstep_chalkland`

- Landform: rolling chalk grassland, shallow dry valleys, reserve edges, and long balanced sightlines.
- Horizon: disciplined hill rhythm with one broad distant escarpment, avoiding identical rounded hills.
- Surface language: pale chalk, smoke-tan soil, blue-gray stone, dry grass, and restrained weathered mineral bands.
- Organic language: dense short grass, low scrub, wind-pruned trees, and broad grazing clearings.
- Weather/light: clear daylight and passing cloud shadow; royal color is not painted into the terrain.
- Transition logic: roads and drainage straighten toward Galegrain; stone and elevation increase toward Meridian.
- Readability: ridgeline, route, and cover clumps remain distinct without banners or golden light.
- Architecture boundary: approved Crownlands settlement and wall kits may occupy graded shelves without being redesigned.

#### `tdf_habitat_crownlands_galegrain_roadbelt`

- Landform: exposed trade road, drainage ditches, grain terraces, field margins, and low service crossings.
- Horizon: long road perspective with alternating field blocks and sparse windbreaks.
- Surface language: compact road grit, pale stone, grain straw, dark tilled soil, and weathered timber only through approved farm overlays.
- Organic language: grain, low hedges, ditch reeds, seed scatter, and small field-edge scrub.
- Weather/light: directional wind fronts flatten crops in broad bands; no screen-filling leaf or grain particles.
- Transition logic: fields open into Crownstep; old road stone and mineral soil accumulate toward Reliquary.
- Readability: road spine, field boundary, and shelter line stay clear when crop density is reduced.
- Architecture boundary: farms and windmills are placements of approved architecture families, not new building designs in this packet.

#### `tdf_habitat_crownlands_reliquary_crypt_garden`

- Landform: collapsed cathedral close, mineral-rich crypt soil, low retaining walls, rubble terraces, and garden remnants.
- Horizon: broken vertical masonry from the approved architecture source against low garden masses.
- Surface language: pale rubble, gray-green mineral scutes, dark soil, weathered paving, and sparse blue mineral inclusions without glow.
- Organic language: hardy herbs, clipped shrubs gone wild, root-bound stone, and dry vine mass.
- Weather/light: dust shafts and cool reflected stone light; religious meaning is not authored here.
- Transition logic: intact road fragments return to Galegrain; wind exposure and broken elevation rise toward Meridian.
- Readability: ground route, rubble hazard, and vertical ruin silhouette remain legible without volumetric shafts.
- Narrative boundary: ownership, sanctity, ritual use, and historical meaning require narrative/content source.

#### `tdf_habitat_crownlands_meridian_storm_shelf`

- Landform: exposed high shelf, weathered conductor stone, short grass, pressure-cut gullies, and cliff approaches.
- Horizon: one broad shelf edge beneath an open storm sky; no dense tower or lightning-rod forest.
- Surface language: storm-gray rock, pale keratin-colored stone, flattened blue-green grass, and rain-dark fractures.
- Organic language: short wind mat, low cushion plants, and tough cliff growth.
- Weather/light: cloud pressure, rain shear, and restrained branching static only at real contact or charged source points.
- Transition logic: the shelf descends through chalk ribs toward Crownstep and through broken stone toward Reliquary.
- Readability: shelf edge, safe approach, and colossal avian silhouette remain clear with lightning disabled.
- Reduced motion: removes rain shear, static travel, and rapid cloud shadow while preserving the storm-value composition.

### Umbral

Transition loop:

```text
Ashwood Veil Ravine
→ Ashvein Three-Fault Rift
→ Cinder Runoff Shelf
→ Graveglass Cavern Vale
→ Ashwood Veil Ravine
```

#### `tdf_habitat_umbral_ashvein_three_fault_rift`

- Landform: three converging faults, broad obsidian ribs, ash terraces, and a central pressure basin.
- Horizon: asymmetric split planes forming a readable three-way convergence, not a spike field.
- Surface language: matte black stone, ash gray, glass-dark fracture faces, clay-red depth, and one restrained cold-violet refraction path.
- Organic language: ash crust, heat-dried root remnants, and sparse low black scrub.
- Weather/light: localized ash ingestion by the rift and low heat distortion; ambient midtones remain visible.
- Transition logic: hot runoff moves toward Cinder; ashwood and broken shelves narrow toward Veil Ravine.
- Readability: fault directions and walkable terraces survive without glow, ash particles, or lightning.
- Architecture boundary: approved Umbral structures may occupy protected terraces; this packet does not add spires or ritual buildings.

#### `tdf_habitat_umbral_cinder_runoff_shelf`

- Landform: cooling lava channels, black rock shelves, mineral crusts, steam pockets, and hardened overflow fans.
- Horizon: low layered shelves with one dark up-slope rift line.
- Surface language: soot-black wet stone, obsidian scutes, pale heat scars, clay mineral deposits, and dull ember depth only within active cracks.
- Organic language: heat-tolerant crusts, sparse gray reed analogues near runoff, and mineral biofilm.
- Weather/light: steam is contact-driven; heat shimmer stays local and never outlines the full habitat.
- Transition logic: channels lead back to Ashvein and cool into glassy ledges toward Graveglass.
- Readability: wet versus hot surfaces use value, edge, and form rather than red emission alone.
- Reduced motion: replaces steam and heat shimmer with fixed wetness and crack-value cues.

#### `tdf_habitat_umbral_ashwood_veil_ravine`

- Landform: oblique canyon, stepped ledges, sparse ashwood, bounded web spans, and sheltered side cuts.
- Horizon: offset canyon walls with deliberate midtone gaps; not an undifferentiated black trench.
- Surface language: graphite rock, ash-gray bark, pale mortar-like mineral seams, glass-dark spines, and restrained aubergine plant undertones.
- Organic language: sparse ashwood trunks, tough root fans, dry thorn mass, and physical web strands only where anchored.
- Weather/light: lateral ash drift and reflected rift light; violet remains environmental, not a permanent object outline.
- Transition logic: canyon opens into Ashvein and folds into sheltered Graveglass ledges.
- Readability: ledge, trunk, web obstruction, and route remain separable in grayscale and reduced atmosphere.
- Ritual boundary: any occult-remnant overlay must use fictional AnotherLife material culture approved by narrative/content mode.

#### `tdf_habitat_umbral_graveglass_cavern_vale`

- Landform: obsidian cave mouths, sheltered valley floor, crypt-adjacent ledges, and glassy erosion fans.
- Horizon: broad cave arches and a low vale silhouette with visible midtones.
- Surface language: graphite stone, smoked glass, ash timber debris only where architecture supplies it, pale mineral crust, and dark violet backscatter from nearby rifts.
- Organic language: cave films, low shelf fungi, pale root remnants, and sparse hanging growth without bioluminescent carpeting.
- Weather/light: cool indirect light, condensation, and occasional reflected rift color; darkness does not erase geometry.
- Transition logic: exposed glass shelves connect to Cinder; ashwood increases toward Veil Ravine.
- Readability: cave opening, ledge, and ground plane remain distinct without backlight or fog.
- Reduced motion: removes condensation drip, drifting dust, and reflected flicker while preserving value separation.

## Supporting Fauna Design Source

### Shared rules

- Ecological guild describes visual behavior and environmental fit only. It does not define AI, aggression, spawn, reward, or combat role.
- Each family must read as a black silhouette at its target distance and as a material-value block in grayscale.
- Eyes, facial proportions, and posture stay adult and biologically grounded; no mascot expression, decorative costume, or player-faction heraldry.
- A variant must change at least one structural, proportional, surface-growth, or motion dimension. Palette-only variants do not count as roster diversity.
- Reused rig or animation grammar never authorizes mesh, silhouette, gait-timing, or material cloning.
- The thirteen new concepts have no visual source in this packet and cannot advance beyond `ProposedTextOnly`.

### Stonehold supporting fauna

#### `tdf_basalt_grazer`

- Source state: legacy merged proposal from `tdf-2026-07-15-v001`; user approval pending.
- Proposed habitat: Faultroad Escarpment with visual suitability into Slagfall Quarry.
- Guild and scale: large mineral-lichen grazer; approximately `1.4` Champion heights at the shoulder and `2.8–3.2` long.
- Silhouette/anatomy: heavy low shielded quadruped, broad shoulder plate, low head, and short weight-bearing legs.
- Materials: charcoal mineralized keratin, ochre hide, pale wear edges, restrained teal mineral seams.
- Motion: slow shoulder-led walk, deliberate weight shift, ground-aware turn, and full stop before browsing.
- Asset intent: `tdf_rig_quad_low_heavy`; shared opaque hide/keratin material grammar.
- Guardrail: habitat placement is proposed only; the old packet must be normalized before production use.

#### `tdf_fauna_stonehold_rimefan_kite`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Rimecut Pass.
- Guild and scale: cliff scavenger and high-slope carrion cleaner; wingspan approximately `2.3` Champion heights.
- Silhouette/anatomy: diamond wing plan, deep insulated chest, short wedge tail, and a pale hooked beak beneath a low brow shield.
- Materials: blue-gray opaque feather masses, dark down, pale worn keratin, and iron-dust staining at feet and beak.
- Motion: ridge soar, brief hoverless wind hold, two-step launch hop, hard wing fold, and side-on cliff brace.
- Structural variants: `rimefan_open_shelf` increases primary separation; `rimefan_gallery_edge` shortens primaries and strengthens the foot/shoulder brace.
- Asset intent: `tdf_rig_avian_soarer`; reuse flight-control grammar with other soarers but retain the diamond plan and short tail.
- Readability: wing diamond and tail wedge identify it with frost, feather fringe, and color removed.

#### `tdf_fauna_stonehold_oreveil_isopod`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Ore Gallery Mouths.
- Guild and scale: cave detritivore; approximately `0.22` Champion heights tall and `0.6` long.
- Silhouette/anatomy: low oval many-legged body, overlapping lateral plates, recessed head, and broad sensory front edge.
- Materials: matte iron-gray chitin, chalky molt seams, polished contact edges, and pale joint membrane.
- Motion: ripple crawl, wall-edge feel, debris sift, vibration freeze, and a compact defensive curl.
- Structural variants: plate count and front-sensor width may change with gallery depth; glow and exposed ore are prohibited.
- Asset intent: `tdf_rig_multiped_low`; bounded leg-pair phase offsets replace one controller per leg at distance.
- Readability: oval plate rhythm and defensive curl remain clear as a two-tone silhouette.

#### `tdf_fauna_stonehold_slagwhistle_burrower`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Slagfall Quarry.
- Guild and scale: small ash-scrub forager and soil turner; approximately `0.38` Champion heights at the shoulder and `0.9` long.
- Silhouette/anatomy: compact wedge body, heavy paired foreclaws, protected slit nostrils, short counterweight tail, and two heat-shedding ear folds.
- Materials: soot-brown hide, glassy foreclaw caps, pale scar tissue, and dull clay underside.
- Motion: foreclaw dig, ear-fold vent, sentry freeze, short four-beat scurry, and backward spoil push.
- Structural variants: deeper-quarry form broadens foreclaws; cooled-bench form lengthens the hind drive. Neither adds emission.
- Asset intent: `tdf_rig_quad_burrower`; share burrow contact events, not body proportions, with other digging fauna.
- Readability: paired foreclaws and triangular ear folds carry the family read.

### Eldergrove supporting fauna

#### `tdf_grove_strider`

- Source state: legacy merged proposal from `tdf-2026-07-15-v001`; user approval pending.
- Proposed habitat: Hollowbark Oldgrowth.
- Guild and scale: tall forest browser; approximately `1.8` Champion heights at the head and `1.3–1.5` at the shoulder.
- Silhouette/anatomy: long neck, narrow legs, arched back, leaf-like ear fins, and trailing organic surface growth.
- Materials: bark-like keratin, dark hide, moss, pale lichen, and warm eye accents.
- Motion: cautious long step, head sweep, ear-fin reaction, pause, and broad ground-aware turn.
- Asset intent: `tdf_rig_quad_tall_browser`.
- Guardrail: working name, habitat mapping, and old written variants are not user-approved or runtime-ready.

#### `tdf_mire_lumenback`

- Source state: legacy merged proposal from `tdf-2026-07-15-v001`; user approval pending.
- Proposed habitat: Mirrorroot Littoral.
- Guild and scale: compact amphibious forager; approximately `0.45` Champion heights at the shoulder and `1.1–1.3` long.
- Silhouette/anatomy: low rounded torso, paddle feet, throat pouch, short head, and low dorsal arc.
- Materials: wet peat hide, blue-green skin, clay underside, and optional cyan defensive rings.
- Motion: low shuffle, throat-pouch breath, short alert freeze, shallow-water push, and group-spacing sidestep.
- Asset intent: `tdf_rig_amphibious_low`; emission remains optional.
- Guardrail: exact glow, habitat placement, and old written variants remain unapproved.

#### `tdf_fauna_eldergrove_thornburrow_hare`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Sunmane Edge Meadow.
- Guild and scale: small meadow grazer and root-scraper; approximately `0.32` Champion heights at the shoulder and `0.75` long.
- Silhouette/anatomy: low adult head, long rear-leg wedge, short upright sensory ears, small paired root-scraping tusks, and a flat counterbalance tail.
- Materials: coarse dun fur, dark keratin tusks, pale belly, and seed-caught guard hair.
- Motion: prolonged freeze, low head scrape, two-stage bound, lateral landing correction, and rapid cover fold.
- Structural variants: edge-meadow form carries longer rear feet; wet-edge form has broader toes and shorter fur.
- Asset intent: `tdf_rig_quad_hind_drive`; share hind-drive grammar with the Umbral bounder while preserving ear, tail, and landing identity.
- Readability: hind-leg wedge and root tusks remain recognizable without ears or fur motion.

#### `tdf_fauna_eldergrove_moonshell_cicada`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Moonroot Floodbasin.
- Guild and scale: trunk sap feeder and seasonal canopy recycler; body approximately `0.35` Champion heights long with a `0.7` wingspan.
- Silhouette/anatomy: heavy thorax, broad roof-fold wings, gripping forelegs, blunt feeding beak, and a short segmented abdomen.
- Materials: opaque mica-like wing panels, wet bark-brown thorax, pale joint membrane, and sparse moonlit silver edge wear without self-glow.
- Motion: vertical climb, long shell stillness, abdomen breath, one decisive launch, and low canopy arc.
- Structural variants: flood-season form broadens gripping feet; dry-season form thickens the wing roof and shortens the abdomen.
- Asset intent: `tdf_rig_invertebrate_winged`; distant states use a single body-wing control group.
- Readability: roof-wing outline and vertical rest pose carry identity without shimmer or particle swarms.

### Crownlands supporting fauna

#### `tdf_fauna_crownlands_broadcrest_aurochs`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Crownstep Chalkland.
- Guild and scale: large herd grazer; approximately `1.25` Champion heights at the shoulder and `2.4` long.
- Silhouette/anatomy: high back, deep chest, broad horizontal horn plate grown from separate brow roots, narrow hips, and a long tuftless tail.
- Materials: smoke-tan short hide, pale weathered keratin, dark muzzle, chalk-polished hooves, and scarred shoulder skin.
- Motion: deliberate herd walk, synchronized broad turn, lateral browse, shoulder set, and slow post-contact recovery.
- Structural variants: reserve form has a deeper chest and worn horn ends; open-chalk form has longer legs and a narrower horn span.
- Asset intent: `tdf_rig_quad_tall_heavy`; it may reuse foot-contact grammar, not silhouette, from other heavy quadrupeds.
- Readability: horizontal horn plate, high back, and narrow rear remain the non-color read; it must not collapse into a real-world cattle copy.

#### `tdf_fauna_crownlands_grainveil_covey`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Galegrain Roadbelt.
- Guild and scale: small ground-seed gleaner; approximately `0.18` Champion heights tall and `0.35` long.
- Silhouette/anatomy: wedge body, low spear beak, long ground toes, short rounded forewings, and a fan tail carried flat at rest.
- Materials: cream and blue-gray coarse feathers, dark scaled legs, pale beak, and dry-grain edge staining.
- Motion: seed glean, synchronized freeze, low group scatter, wing-assisted ditch hop, and fast tail-fan brake.
- Structural variants: field form broadens the fan; road-edge form lengthens toes and reduces wing area.
- Asset intent: `tdf_rig_avian_ground_small`; one shared flock-pose library may support visual grouping, not runtime flock AI.
- Readability: wedge, toes, and flat fan remain distinct at a two-tone distant size.

#### `tdf_fauna_crownlands_reliquary_shellback`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Reliquary Crypt Garden.
- Guild and scale: low garden insectivore; approximately `0.35` Champion heights at the shoulder and `0.85` long.
- Silhouette/anatomy: arched mineral-scute back, narrow probe snout, four short limbs, high shoulder hinge, and a blunt brace tail.
- Materials: gray-green pebbled hide, pale mineral scutes, soil-dark joints, and weathered chalk wear without glow.
- Motion: deliberate probe search, scute lift, partial defensive clamp, low wall step, and tail-braced turn.
- Structural variants: rubble form uses fewer larger scutes; garden form uses more overlapping edge scutes and a longer probe.
- Asset intent: `tdf_rig_quad_low_shellback`; shared shell deformation is limited to broad lift and clamp controls.
- Readability: arched shell, probe snout, and brace tail must not resemble the six-limbed Reliquary Basilisk anchor.

#### `tdf_fauna_crownlands_stormglass_swift`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Meridian Storm Shelf.
- Guild and scale: aerial insectivore and storm-front follower; wingspan approximately `0.95` Champion heights.
- Silhouette/anatomy: crescent wings, compact chest, short hooked beak, forked rigid tail, and narrow gripping feet.
- Materials: charcoal feather mass, opaque metallic-blue edge feathers, pale keratin, and rain-dark chest down.
- Motion: long bank, pressure-line climb, fast stoop, shelf-edge perch, and closed-wing wind brace.
- Structural variants: calm-front form shortens the tail fork; high-shelf form deepens the chest and primary sweep.
- Asset intent: `tdf_rig_avian_soarer`; reuse flight-control grammar with Rimefan and Sootsail only after silhouette timing is separately tuned.
- Readability: crescent wing and rigid fork identify it without metallic edge, rain, or static.

### Umbral supporting fauna

#### `tdf_fauna_umbral_sootsail_carrioner`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Ashvein Three-Fault Rift.
- Guild and scale: broad-wing scavenger and ash-terrace cleaner; wingspan approximately `2.0` Champion heights.
- Silhouette/anatomy: plank-like wings, deep keel chest, low hooded skull, long split tail, and heavy ground-bracing feet.
- Materials: matte charcoal feathers, pale scarred face keratin, glass-dark beak, ash-gray legs, and no self-emission.
- Motion: thermal circle, controlled side-slip drop, ground mantle, two-foot carcass brace, and heavy running launch.
- Structural variants: rift form has wider inner wings; ravine form shortens the tail and deepens the ground brace.
- Asset intent: `tdf_rig_avian_soarer`; distinct slow plank-wing timing prevents a palette-swapped Crownlands swift.
- Readability: plank wing, hooded head, and long split tail survive black silhouette.

#### `tdf_fauna_umbral_cinderplate_scarab`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Cinder Runoff Shelf.
- Guild and scale: ash and mineral detritivore; approximately `0.28` Champion heights tall and `0.6` long.
- Silhouette/anatomy: domed six-legged body, broad shovel head, oversized front tibia, closed wingcase, and low abdominal wedge.
- Materials: opaque obsidian wingcase, clay-red underside, pale heat scars, matte joint membrane, and no molten interior.
- Motion: ash push, short burrow, wingcase brace, front-leg rake, and rare low flight with a full settled landing.
- Structural variants: hot-shelf form deepens the shovel head; cooled-channel form lengthens the rear legs and flattens the dome.
- Asset intent: `tdf_rig_invertebrate_six_limb`; share leg phase and wingcase controls with no other silhouette.
- Readability: dome, shovel, and front-tibia mass remain clear without glass highlights.

#### `tdf_fauna_umbral_ashstep_bounder`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Ashwood Veil Ravine.
- Guild and scale: ledge forager and seed/root consumer; approximately `0.48` Champion heights at the hip and `1.05` long.
- Silhouette/anatomy: hind-limb-heavy wedge, small low ears, long muscular feet, deep pelvis, short forelimbs, and a straight counterbalance tail.
- Materials: ash-gray coarse hide, graphite feet, pale joint scars, dark tail ridge, and restrained aubergine skin at protected folds.
- Motion: lateral hop, prolonged ledge freeze, three-point descent, tail-braced turn, and low cover press.
- Structural variants: ravine form lengthens the tail; cavern-edge form broadens feet and shortens the ears.
- Asset intent: `tdf_rig_quad_hind_drive`; shares only hind-drive control grammar with Thornburrow Hare.
- Readability: pelvis, long feet, and straight tail separate it from rabbit-like anatomy and remain legible without ears.

#### `tdf_fauna_umbral_graveglass_sheller`

- Source state: `ProposedTextOnly`.
- Proposed habitat: Graveglass Cavern Vale.
- Guild and scale: mineral-film grazer and slow cave recycler; approximately `0.25` Champion heights tall and `0.7` long.
- Silhouette/anatomy: low gastropod foot, forward sensory shield, asymmetric opaque shard shell, and two short tactile feelers.
- Materials: wet charcoal body, layered smoked-glass shell, pale abrasion edges, and gray mineral film; shell stays opaque.
- Motion: slow wall traverse, broad foot compression, shell brace, deliberate corner turn, and feeler sweep.
- Structural variants: cavern form raises the shell ridge; vale form widens the foot and lowers the shell for open ground.
- Asset intent: `tdf_rig_gastropod_shell`; body deformation uses a few bounded segments rather than continuous high-bone simulation.
- Readability: asymmetric shell and forward shield remain identifiable with translucency and reflections disabled.

## Category Coverage Without Roster Inflation

| Direction | Covered in this source version |
| --- | --- |
| animal-like | Multiple grounded grazers, browsers, burrowers, and foragers |
| bird-like | Soaring, ground, and swift body plans with three distinct flight/gait reads |
| insect-like/invertebrate | Isopod, cicada, scarab, and sheller body plans |
| lake/swamp | Littoral and floodbasin habitats plus amphibious and winged supporting fauna |
| mountain/ice/cave/canyon/volcanic/forest/grassland/ruins | Explicit connected habitat modules |
| magical/altered | Allowed as material/anatomy/motion-changing ecotypes; not used as palette-only families |
| celestial/demonic/mutated/undead/vampiric | Deferred altered-state layers pending narrative and exact visual source |
| sea/desert/oasis | Deferred pending approved geography |
| human-like | Excluded from A2 terrestrial fauna |

## Existing Source State Preservation

- The sixteen boss/elite profiles from `tdf-rbe-2026-07-24-v001` remain `ReadyForUserReview`. This packet does not change their concept sheets, source hashes, QA findings, or approval state.
- `tdf_basalt_grazer`, `tdf_grove_strider`, and `tdf_mire_lumenback` remain merged legacy proposals from `tdf-2026-07-15-v001`. Their exact pixels exist, but the older packet still requires readiness/media normalization before production consumption.
- The thirteen new supporting families are `ProposedTextOnly`. They have no source assets, hashes, variants ready for approval, or production authority.
- Habitat modules are `RosterProposed`. No habitat concept sheet, terrain source, authored layout, or runtime zone exists.

## Explicit Non-Authority

This packet does not define or approve:

- player-facing names, lore, cultures, languages, ethnicity, religion, or localization;
- aggression, AI, navigation, combat roles, stats, skills, hit boxes, or encounter phases;
- spawn positions, density, camps, timing, population simulation, or despawn rules;
- quests, objectives, world-state meaning, faction use, rewards, loot, or economy;
- runtime zone IDs, spawn-table IDs, game-data records, Addressable labels, or bundle implementation;
- save fields, persistence, scenes, prefabs, shaders, code, materials, rigs, animation clips, or VFX systems;
- purchase or import of marketplace source;
- final device minimums, frame budgets, view distances, memory ceilings, or install-size ceilings;
- user creative approval.

## Next Handoff

1. A2 produces exact habitat and selected-family concept source with full views, scale, material, motion, accessibility, hashes, provenance, and direct review links.
2. The user approves, refines, or rejects the exact source version and IDs.
3. Codex coordination/review defines runtime zone/catalog, streaming, view-distance, memory, population, performance, and acceptance contracts.
4. Codex engineering integrates only approved source and records immutable source-to-runtime mappings.
5. Codex coordination/review and A2 perform technical and design-fidelity dispositions before user integrated approval.
