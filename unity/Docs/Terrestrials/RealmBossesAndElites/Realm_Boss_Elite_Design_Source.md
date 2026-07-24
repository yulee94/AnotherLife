# Four-Realm Boss And Elite Design Source

## Status

- Issue: #259
- Primary Codex mode: Terrestrial-design
- Source version: `tdf-rbe-2026-07-24-v001`
- State: generated source proposal pending direct review surface and user creative approval
- Runtime authority: none
- Narrative authority: none

This packet defines the visual source for four unique outer-warzone bosses and twelve inner-realm field elites. It does not define combat mechanics, stats, AI, spawn tables, loot, quests, dialogue, save data, or runtime implementation.

The retained source pixels, exact executed prompts, immutable hashes, and direct visual disposition are recorded in `realm_boss_elite_profiles_manifest.json`, `Executed_Generation_Prompts.md`, and `Visual_QA_Disposition.md`. All raster sources remain outside `unity/Assets` so they cannot be imported into or packaged with a Player build.

## Target

The creatures must read as adult, realistic, high-end dark fantasy at gameplay and cinematic distance. They should look photographed from physically built anatomy and materials, not illustrated, toy-like, mascot-like, low-poly, or assembled from unrelated animal parts.

Every profile must survive:

- a black-silhouette test at map-camera distance;
- a grayscale material/value test;
- a close neutral-light anatomy inspection;
- a locomotion and contact test at normal speed;
- a low/mobile presentation with the same threat and objective readability;
- comparison against the approved realm material language without copying champion armor onto an animal.

## Realm Continuity

| Realm | Canonical environment basis | Creature material language | Forbidden shortcut |
| --- | --- | --- | --- |
| Stonehold | mountains, snow shelf, deep ore galleries, quarry and forge edge | compressed mass, slate, iron-rich hide, horn, dust, restrained forge heat | ordinary animal with rocks glued to its back |
| Eldergrove | old-growth forest, lakes and flooded roots, open grassland | living tissue, bark-like keratin, wet hide, lichen, root symbiosis, restrained pollen | deer with decorative leaves or antlers used as a crown |
| Crownlands | central plains, trade roads, royal stormfront, cathedral ruins | disciplined natural pattern, pale keratin, weathered feather/fur/scute, restrained blue-white static | gold-painted generic lion, griffin, or paladin animal |
| Umbral | volcanic rifts, ash canyons, obsidian caves, shadow vale | black volcanic osteoderm, scar tissue, glass, ash, localized ember or cold-violet fracture | black repaint with purple glow on every edge |

## Global Shape Rules

- Bosses use four different primary body plans: six-limbed terrestrial behemoth, semi-aquatic leviathan, colossal avian, and three-necked winged reptile.
- Each realm's three elites use different silhouette classes and gait footprints.
- Eye size remains biologically proportioned. No friendly brow arcs, rounded mascot muzzles, permanent smiles, or oversized pupils.
- Magical material is subordinate to anatomy. Glow reveals stress, respiration, charge, or environmental interaction; it is never a flat emissive costume.
- Realm heraldry may influence rhythm and negative space but may not appear as a literal natural sigil, tattoo, or readable rune.
- Horns, plates, feathers, roots, membranes, and fur must have attachment logic, growth direction, wear, and collision-safe motion intent.
- Marketplace or generated sources cannot become the creature identity unchanged.

## Outer-Warzone Bosses

### `tdf_boss_stonehold_fault_crowned_colossus`

Working name: **Fault-Crowned Colossus**

Biome: tectonic escarpment and broken siege road outside Stonehold.

Scale: approximately 5.8 Champion heights at the shoulder and 12.5 Champion heights long.

Silhouette: a low forward wedge carried by six pillar legs. The front pair braces a deep shield chest; a fractured crown of fused horn and slate rises behind a single broad plow horn. The rear mass stays heavy and short rather than dinosaur-tailed.

Anatomy:

- elephant/rhinoceros weight logic without copying either animal;
- six load-bearing limbs with visible shoulder and hip hierarchy;
- compact eyes protected behind brow plates;
- deep nasal and thoracic cavities that can move dust under exertion;
- stone-like dorsal lamellae grow from mineralized keratin, not loose boulders.

Materials: charcoal slate lamellae, iron-brown scarred hide, pale worn horn, dark gums, and tiny forge-amber stress seams visible only at plate roots under maximum exertion.

Motion intent: slow anticipatory weight transfer, delayed body follow-through, ground-searching front feet, lateral shoulder shove, horn plow, and a full-body recovery after any charge. Every step compresses soil before dust releases.

Effect language: displaced gravel, directional dust curtains, stone chips from real contact, low heat shimmer at strained plate roots. No clean circular shockwave or orange outline.

Readability gate: the six-leg footprint, shield chest, and broken horn crown must identify it without color or particles.

### `tdf_boss_eldergrove_mere_root_leviathan`

Working name: **Mere-Root Leviathan**

Biome: the deep lake where old-growth roots descend into flooded stone.

Scale: approximately 2.8 Champion heights at the back and 15 Champion heights from jaw to tail.

Silhouette: a low barge-shaped torso, broad crocodilian-salamander skull, two dominant forelimbs, lower rear paddles, and an eel-like tail. A swept root fan begins behind the skull and lies with the water flow instead of forming decorative antlers.

Anatomy:

- dense semi-aquatic rib cage and powerful neck base;
- eyes and nostrils sit high for surface stalking but remain small and predatory;
- forelimbs anchor and pull; rear paddles steer rather than mimic land legs;
- root symbiosis enters armored skin channels and does not replace muscle or bone.

Materials: wet umber-black pebbled hide, pale water-worn throat scutes, cracked bark-keratin ridges, dark moss, and sparse green-gold living tissue visible only where roots meet skin.

Motion intent: submerged body roll, delayed tail wave, heavy bank haul, forelimb pull, jaw-led lunge, root fan drag, and water-surface breathing. Water mass must react before and after the body.

Effect language: opaque wake, mud plume, snapped reeds, displaced pollen, droplets from root tips, and brief subsurface green-gold refraction. No floating leaf spiral.

Readability gate: the root fan, broad skull, dominant forelimbs, and long tail must read through fog and reduced saturation.

### `tdf_boss_crownlands_meridian_tempest_roc`

Working name: **Meridian Tempest Roc**

Biome: the high storm shelf above Crownlands' outer trade roads.

Scale: approximately 5.5 Champion heights standing with an 18 Champion-height wingspan.

Silhouette: colossal avian only, with a deep sternum, long load-bearing legs, hooked beak, blade-like primary feathers, and a split double tail fan. It must not use a lion body or generic griffin profile.

Anatomy:

- eagle, crane, and condor flight logic synthesized into an original heavy storm bird;
- thick shoulder and chest flight muscle;
- weathered keratin brow and beak rather than a crown;
- talons sized for believable load and ground contact;
- feather layers overlap in aerodynamic groups, not individually noisy cards.

Materials: charcoal and storm-gray feather masses, pale weathered keratin, sparse midnight-blue secondary feathers, scarred legs, and blue-white static confined to separated pinion tips during charge.

Motion intent: long ground appraisal, chest compression before launch, violent first downstroke, delayed secondary-feather response, banked stoop, talon rake, wing-assisted ground turn, and downwash recovery.

Effect language: grass flattening, loose banner and dust response, rain shearing, pressure condensation, and branching static that illuminates nearby feathers. No lightning aura around the full silhouette.

Readability gate: sternum, long legs, split tail, and blade-primary wing shape must remain distinct when effects are disabled.

### `tdf_boss_umbral_ashvein_triarch`

Working name: **Ashvein Triarch**

Biome: an open volcanic rift where three fault lines converge.

Scale: approximately 4.5 Champion heights at the shoulder, 13 Champion heights long, with a 15 Champion-height wingspan.

Silhouette: one deep thorax supports three differently carried necks: a heavy central crusher and two narrower lateral hunters. The wing roots and four walking limbs remain structurally clear; necks do not emerge as a tangled bundle.

Anatomy:

- asymmetrical head damage and horn growth distinguish the three heads without color;
- separate neck musculature resolves into a reinforced shared shoulder girdle;
- broad hind feet carry mass while forelimbs brace landing and close turns;
- wings are proportioned for assisted launch and rift soaring, not weightless hovering.

Materials: black volcanic osteoderms, ash-gray membrane, pale healed scar tissue, glass-dark horn, ember-red mouth/throat depth, and one localized cold-violet fracture path across the thorax.

Motion intent: independent head tracking with coordinated torso intent, alternating neck recoil, heavy crawl, assisted launch, off-balance landing recovery, and membrane tension changes. Heads cannot mirror one another.

Effect language: breath-specific heat distortion, ash ingestion and expulsion, glass fragments from contact, localized ember light inside mouths, and short rift refraction near the thoracic fracture. No three-color head gimmick or full-body purple outline.

Readability gate: central-versus-lateral head hierarchy, wing roots, and four-limb stance must survive a black silhouette. The provisional marketplace hydra may be tested only as topology/rig reference after approval; it cannot define this identity unchanged.

## Stonehold Inner-Realm Elites

### `tdf_elite_stonehold_rimehorn_breaker`

Working name: **Rimehorn Breaker**

Biome: glacial shelf and wind-cut pass.

Silhouette: high shoulder hump, low mature skull, split shovel horn, heavy beard and forequarters, narrower rear legs.

Materials: ice-clumped dark guard hair, pale scarred horn, slate brow scales, frost only on windward surfaces.

Motion: deliberate uphill steps, horn-led snow clearing, short explosive shoulder drive, and heavy braking.

Effect language: compressed snow slab, breath vapor, ice granules, and fur shedding. The body must remain readable without white frost.

### `tdf_elite_stonehold_oreblind_delver`

Working name: **Oreblind Delver**

Biome: deep ore gallery and collapsed mine throat.

Silhouette: six low limbs, wedge skull, no visible eyes, broad sensory jaw plates, and a short counterweight tail.

Materials: matte charcoal hide, iron-polished digging claws, pale scar tissue, metallic whisker plates without emissive ore.

Motion: wall-touching jaw sweep, synchronized digging pairs, abrupt vibration freeze, low tunnel sprint, and debris-backed emergence.

Effect language: dust fall before emergence, loose stone displacement, claw sparks only on metal-bearing rock.

### `tdf_elite_stonehold_slaghide_gorer`

Working name: **Slaghide Gorer**

Biome: cooled slag field and abandoned forge quarry.

Silhouette: low fast suid body, long asymmetric tusks, raised vitrified dorsal shield, small aggressive eyes.

Materials: heat-scarred hide, glossy black slag shield, iron-gray tusk roots, dull red skin visible only in old shield cracks.

Motion: restless weight shift, side-scrape, low acceleration, tusk hook, shield-first wall rub, and sliding recovery.

Effect language: glassy flakes, sparks from tusk contact, dark dust, and brief residual heat without molten-body treatment.

## Eldergrove Inner-Realm Elites

### `tdf_elite_eldergrove_hollowbark_stalker`

Working name: **Hollowbark Stalker**

Biome: dense old-growth forest and fallen trunk corridors.

Silhouette: long mustelid-feline torso, high flexible shoulders, low head, split bark tail fan, and large gripping feet.

Materials: short dark fur, keratin plates with bark-like growth direction, restrained lichen, exposed muscle at flexible joints.

Motion: shoulder-led silent walk, trunk-hugging body bend, rear-foot placement, sudden low pounce, and bark-tail counterbalance.

Effect language: compressed leaf litter, bark dust from contact, branch movement, and brief camouflage value shift through natural occlusion, not invisibility glow.

### `tdf_elite_eldergrove_mirrorfin_lurker`

Working name: **Mirrorfin Lurker**

Biome: clear lake margin and flooded root shelf.

Silhouette: broad flat amphibious torso, shovel jaw, four splayed limbs, lateral fin mantle, and tapering rudder tail.

Materials: dark wet hide, silver-green scale islands, translucent but thick fin margins, pale underside, no glass body.

Motion: still surface breathing, fin ripple, mud suction release, sideways burst, jaw scoop, and tail-driven retreat.

Effect language: specular water breakup, mud fan, reed displacement, and localized reflected light from real fin angle.

### `tdf_elite_eldergrove_sunmane_thornstag`

Working name: **Sunmane Thornstag**

Biome: open grassland and forest-edge meadow.

Silhouette: tall mature cervid, deep scarred chest, backward thorn antlers, coarse dorsal mane, and long lower-leg negative spaces.

Materials: dun hide, dark bark-keratin antlers, sun-bleached mane tips, scar tissue, sparse grass seed caught in fur.

Motion: lateral appraisal, ground rake, mane bristle, antler-feint, bounding charge, and wide turn that respects antler inertia.

Effect language: grass wake, seed release, earth gouge, and restrained warm backscatter. No flower burst or decorative vine antlers.

## Crownlands Inner-Realm Elites

### `tdf_elite_crownlands_crownstep_lion`

Working name: **Crownstep Lion**

Biome: central plains and abandoned royal hunting reserve.

Silhouette: long adult felid, deep chest, low hips, layered keratin mane shields, and a heavy tuftless tail.

Materials: smoke-tan short fur, weathered pale mane plates, dark muzzle, scarred paws, no metallic gold coat.

Motion: low stalking, scapular roll, measured stare, mane-plate lift, lateral swat, full-weight pounce, and breath recovery.

Effect language: flattened grass, dust from paws, plate clatter, and brief warm rim from actual sun angle.

### `tdf_elite_crownlands_galeclaw_courser`

Working name: **Galeclaw Courser**

Biome: exposed trade road and storm-swept grain fields.

Silhouette: flightless predatory bird, low spear head, deep pelvis, long running legs, short stabilizing forewings, and rigid tail fan.

Materials: blue-gray coarse feathers, dark scaled legs, pale hooked beak, worn cream tail bars.

Motion: head-stable pursuit, long elastic stride, forewing balance, skid turn, beak strike, and heel-claw kick.

Effect language: directional grain wake, road grit, feather loss, and pressure ripple only through affected vegetation.

### `tdf_elite_crownlands_reliquary_basilisk`

Working name: **Reliquary Basilisk**

Biome: collapsed cathedral close and mineral-rich crypt garden.

Silhouette: long six-limbed reptile, narrow armored skull, high shoulder scutes, low hips, and stiff segmented tail.

Materials: gray-green pebbled hide, pale mineral scutes, dark eye shields, soil-stained claws, sparse blue mineral inclusions with no glow.

Motion: alternating three-pair gait, head-lock observation, tail brace, low rush, shoulder scrape, and slow crypt-wall climb.

Effect language: masonry grit, chipped mineral scute, dust shafts, and real reflected light from eye shields. No automatic petrification visual is authorized.

## Umbral Inner-Realm Elites

### `tdf_elite_umbral_cindermaw_salamander`

Working name: **Cindermaw Salamander**

Biome: volcanic runoff channel and cooling lava shelf.

Silhouette: broad low amphibian, wedge mouth, powerful lateral limbs, flattened tail, and dorsal heat fins.

Materials: soot-black wet skin, obsidian dorsal scutes, pale heat scars, dull ember tissue confined inside the mouth and fin roots.

Motion: belly drag, lateral limb push, throat expansion, tail slap, sudden jaw surge, and heat-seeking pause.

Effect language: steam at wet contact, ash paste, heat distortion near the mouth, and cooled-glass flakes.

### `tdf_elite_umbral_veilspine_widow`

Working name: **Veilspine Widow**

Biome: ashwood canyon and suspended web ravine.

Silhouette: narrow high spider stance, small armored abdomen, long unequal legs, vertical dorsal veil spines, and forward sensory palps.

Materials: matte black chitin, ash-gray joint membrane, glass-dark spines, tiny crimson sensory pits visible only in close light.

Motion: alternating high steps, wind-braced crouch, leg-tap sensing, diagonal sprint, controlled drop, and asymmetric recovery.

Effect language: ash caught in web strands, chitin scrape, falling dust, and restrained thread specular. No oversized abdomen, neon web, or humanoid face pattern.

### `tdf_elite_umbral_gravewing_siphon`

Working name: **Gravewing Siphon**

Biome: obsidian cave mouth and ruined shadow-valley crypt.

Silhouette: massive bat with folded-cloak resting shape, long thumb claws, narrow pale sensory face, deep chest, and trailing membrane tabs.

Materials: charcoal fur, thin scarred wing membrane, pale cartilage, black claws, dark violet vascular undertone visible only when backlit.

Motion: hanging breath, membrane tension, quadrupedal cave crawl, drop launch, close bank, claw brace, and heavy landing fold.

Effect language: cave dust, displaced ash, real membrane translucency, condensation, and brief reflected violet from nearby rift sources rather than self-glow.

## Scalable Presentation Budgets

These are source targets for later coordination review, not implementation approval.

| Role / tier | Skinned triangles | Deform bones | Material slots | Texture intent | Active particles | Dynamic lights | Compressed content target |
| --- | ---: | ---: | ---: | --- | ---: | ---: | ---: |
| Boss low/mobile | <=45k | <=96 | <=3 | one 2K color/normal/packed set, ASTC/target compression | <=180 | 0 | <=24 MB |
| Boss balanced | <=80k | <=128 | <=4 | up to two 2K sets | <=350 | <=1 | <=48 MB |
| Boss high PC | <=130k | <=180 | <=4 | selective 4K color/normal plus packed 2K | <=700 | <=2 | <=96 MB optional tier |
| Elite low/mobile | <=22k | <=64 | <=2 | one 1K-2K color/normal/packed set | <=80 | 0 | <=10 MB |
| Elite balanced | <=45k | <=96 | <=3 | one 2K set | <=160 | <=1 shared/pooled | <=20 MB |
| Elite high PC | <=75k | <=128 | <=4 | selective 4K hero map, otherwise 2K | <=320 | <=1 | <=40 MB optional tier |
| Cinematic source | shot-dependent | shot-dependent | shot-dependent | 4K-8K only where pixel coverage proves need | offline only | offline only | never packaged in Player |

Additional constraints:

- LOD1 targets approximately 55-65% of LOD0 silhouette cost; LOD2 targets 20-30%; distant representation targets 5-10% or an authored impostor.
- Hair, mane, moss, web, and membranes use bounded cards or shell geometry on mobile; strand systems remain offline cinematic source.
- Texture channels are packed and deduplicated. Realm color variants may not duplicate full normal/roughness maps.
- VFX use pooled systems, bounded lifetimes, quality-tier emission limits, and environmental masks. Boss grandeur comes from scale and consequence before particle count.
- High/cinematic tiers may add secondary debris, volumetric response, and localized illumination; they may not alter telegraph timing, silhouette, navigation, or required state readability.
- Source archives, ZBrush/Substance files, 8K maps, render caches, and marketplace packages never enter Player builds.

## Acquisition And Originality Gate

1. Record creator, listing, license, price, format, topology, texture, rig, animation, access, and preview hashes.
2. Reject copied game models, franchise identities, non-commercial terms, unclear authorship, and externally fragmented source.
3. Capture official previews before purchase and compare them against this source packet.
4. Purchase only after the profile role is selected and the user approves the exact listing and expected price.
5. Open with scripts disabled, hash the archive, inventory dependencies, and render neutral anatomy/motion tests.
6. A base model must be re-sculpted, re-proportioned, re-materialed, and re-authored enough to become an original AnotherLife identity.
7. Marketplace motion may be used as timing or rig support only after foot contact, deformation, root motion, and repetition tests.
8. Failed candidates remain evidence and cannot become distant filler merely because money was spent.

## Explicit Non-Authority

This source does not approve:

- combat skills, hit boxes, phases, damage, crowd control, or counters;
- AI state, aggro, leashing, pathing, or spawn density;
- level, health, rewards, loot, economy, quests, dialogue, or lore;
- final player-facing names;
- Unity prefabs, rigs, shaders, catalogs, scenes, Addressables, or code;
- purchase of any candidate;
- visual acceptance by the user.

## Acceptance State

- Roster count: 4 bosses / 12 elites, complete.
- Realm and biome differentiation: source-complete.
- Silhouette, anatomy, material, motion, and effect intent: source-complete.
- Scalability intent: source-complete, pending coordination review and profiling.
- Concept sheets: 16 final candidates generated and directly inspected; all remain `GeneratedUnreviewed`.
- Prompt provenance and immutable source identity: complete for 23 generated outputs, including seven retained refinement inputs or superseded candidates.
- Same-source production fidelity: blocked until approved models, rigs, materials, and normal-speed motion exist.
- Marketplace acquisition: blocked pending role selection and explicit purchase approval.
- User creative approval: pending.
