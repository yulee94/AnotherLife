# Terrestrial Design Brief

## Status

Issue: #194

Owner mode: Codex terrestrial-design

Source version: `tdf-2026-07-15-v001`

This packet establishes an original terrestrial fauna design foundation for review. It intentionally avoids runtime implementation, narrative naming, lore, spawning, combat, rewards, save data, prefabs, shaders, scenes, and gameplay catalogs.

## Visual Tone

The terrestrial direction should feel grounded, readable, and high-fantasy naturalistic rather than monstrous for its own sake. Creatures should look like they belong to functioning land ecosystems: they eat, flee, browse, observe, protect themselves, and occupy space with believable mass. The target audience is an action-RPG/kingdom-sim player who needs creatures to read quickly at gameplay distance while still rewarding close inspection.

The first foundation set uses three ecological shapes:

- Heavy low grazer: broad, protected, slow, landscape-scale presence.
- Tall forest browser: vertical leg/neck read, cautious ambient movement.
- Compact wetland forager: low profile, luminous markings, group-friendly spacing.

## Global Shape Rules

- Non-humanoid only. No hands, tool use, clothing, saddles, weapons, or rider cues.
- Silhouette must remain identifiable when reduced to a black shape.
- Profile proportions should be exaggerated enough for gameplay readability but not cartoon-flat.
- Defensive features are biological or material, not narrative symbols.
- Heads should avoid human facial proportions; eye placement and mouth shapes must stay animal-like.
- Every creature needs a clearly readable front mass, side length, and gait footprint.

## Scale Rules

Scale values are visual design targets, not engine measurements:

- Basalt Grazer: approximately 1.4 Champion heights at shoulder, 2.8-3.2 Champion heights long.
- Grove Strider: approximately 1.8 Champion heights at head, 1.3-1.5 Champion heights at shoulder.
- Mire Lumenback: approximately 0.45 Champion heights at shoulder, 1.1-1.3 Champion heights long.

Champion scale references in the concept sheets are neutral mannequins and are not narrative characters.

## Material And Color Direction

The palette should avoid one-note creature sets. Each profile has a dominant material family and a small accent family:

- Basalt Grazer: charcoal stone plates, ochre hide, tiny teal mineral seams.
- Grove Strider: bark ridges, moss greens, pale lichen, warm eye accents.
- Mire Lumenback: wet peat hide, blue-green skin, clay underside, cyan bioluminescent rings.

Color cannot be the only source of differentiation. Shape, pose, material breakup, and motion must carry the read for color-blind and reduced-saturation presentation.

## Motion Intent

Motion should support creature temperament:

- Heavy creatures use slow weight shifts, shoulder rolls, and ground-aware turns.
- Tall browsers use cautious pauses, head sweeps, ear/leaf-fin reactions, and long-legged stepping.
- Compact wetland creatures use low shuffles, throat-pouch breathing, short alert freezes, and group spacing.

Reduced-motion mode should keep pose changes and silhouette states while disabling or minimizing glow pulsing, idle micro-jitter, and rapid secondary motion.

## Profile: tdf_basalt_grazer

Working display key: `Basalt Grazer`

Silhouette class: `heavy_low_shielded_quadruped`

Intent: A slow herbivore with a low, armored read. Its shield-like shoulder mass should be recognizable from the side, front, and map-camera distance.

Approved variants for this source version:

- `tdf_basalt_grazer_standard`: charcoal basalt plates with ochre hide.
- `tdf_basalt_grazer_ashen`: paler dusted plates for dry volcanic or quarry-adjacent biomes.
- `tdf_basalt_grazer_mineral`: slightly stronger teal seam accents for rare-resource-adjacent zones.

Exclusions:

- Do not turn plate seams into magical runes.
- Do not make the beak or forelimbs predatory.
- Do not add saddle, armor straps, or humanoid domestication props.

## Profile: tdf_grove_strider

Working display key: `Grove Strider`

Silhouette class: `tall_browser_long_neck`

Intent: A cautious forest browser that reads through height, narrow legs, neck arc, leaf-ear fins, and trailing organic texture.

Approved variants for this source version:

- `tdf_grove_strider_standard`: moss and bark tones.
- `tdf_grove_strider_late_autumn`: slightly warmer lichen and brown tendril accents.
- `tdf_grove_strider_mist`: desaturated bark and pale lichen for low-contrast fog biomes.

Exclusions:

- Do not make leaf fins look like decorative crowns.
- Do not turn vine tendrils into hair, clothing, or character styling.
- Do not add player-facing faction markings.

## Profile: tdf_mire_lumenback

Working display key: `Mire Lumenback`

Silhouette class: `compact_low_amphibious_forager`

Intent: A low wetland forager with a rounded body, paddle-foot read, throat-pouch breathing, and luminescent defensive markings.

Approved variants for this source version:

- `tdf_mire_lumenback_standard`: peat, blue-green skin, cyan rings.
- `tdf_mire_lumenback_clay`: warmer underside and reduced blue for muddy wetland edges.
- `tdf_mire_lumenback_night`: stronger ring contrast for low-light previews.

Exclusions:

- Do not make glow patterns into readable language or narrative symbols.
- Do not make whiskers behave like combat tentacles.
- Do not make the creature slimy enough to require complex translucent shader assumptions.

## LOD And Accessibility Intent

LOD targets:

- LOD0: concept-fidelity model with material slots and secondary details.
- LOD1: simplified mesh retaining silhouette, main plate/ridge/pouch forms, and two material families.
- LOD2: silhouette-first shape with baked color blocks; remove small tendrils, fine seam glow, and whisker detail.
- Distant impostor/icon: black or two-tone silhouette must identify profile class.

Accessibility:

- Every profile has a non-color silhouette class.
- Glow accents are optional readability aids, not required state conveyance.
- Reduced-motion mode should remove pulsing glow and excessive idle secondary motion.
- Contrast should remain legible in grayscale concept review.

## Explicit Non-Authority

This packet does not approve:

- player-facing names;
- lore or story meaning;
- biome spawn tables;
- gameplay stats;
- rewards or loot;
- AI behavior;
- combat roles;
- Unity prefabs, rigging, shaders, scenes, or code.

## Unresolved User Decisions

- Final creature roster size.
- Whether any profile becomes interactable, ambient-only, collectible, hostile, or neutral.
- Whether variants map to realm, biome, season, rarity, or progression.
- Final player-facing names and descriptions.
- Final model fidelity budget and target platform performance budget.
