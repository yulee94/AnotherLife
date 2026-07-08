# Another Life Skill Effects And Weather Design

This document defines the first VFX and weather design pack for Another Life. The goal is readable mobile-first effects that can later scale up for PC without hiding gameplay information.

## VFX Performance Rules

- Skills must be readable at mobile screen size.
- Major boss telegraphs must never be hidden by friendly effects.
- In RvR, far units should use reduced effects or realm-colored markers.
- Skill VFX should have three quality levels: Hero, Nearby, Distant.
- Avoid long-lived opaque particles near the camera.
- Pool all repeated VFX, projectiles, damage numbers, and hit markers.
- Use short lifetimes and strong silhouettes for combat readability.

## Realm Warmaster VFX Palettes

| Realm | Primary Shape | Particle Mood | Color Notes |
| --- | --- | --- | --- |
| Stonehold Dwarves | Impact rings, stone shards, forge sparks | Heavy, angular, metallic | Slate, iron, copper, orange forge glow. |
| Eldergrove Elves | Leaves, light ribbons, pollen, root circles | Elegant, healing, fast | Green, pale gold, white, blue lake glow. |
| Crownlands Humans | Crests, banners, clean arcs, heroic rays | Balanced, royal, direct | Blue, gold, white, red accent. |
| Umbral Dark Elves | Smoke blades, cracks, curse sigils, ash | Predatory, sharp, unstable | Black, violet, crimson, ember red. |

## Starter Skill Effect Set

| Key | Use | Visual |
| --- | --- | --- |
| stonehold_forge_burst | Guardian/Swordsman impact | Stone ring, copper sparks, small dust pop. |
| eldergrove_healing_bloom | Healer/Elf healing | Green-gold bloom, upward motes, soft circle. |
| crownlands_royal_strike | Human balanced attack | Blue-gold slash arc, brief crest flash. |
| umbral_curse_mark | Dark Elf curse/debuff | Dark smoke, red sigil pulse, inward particles. |
| universal_dodge_trail | Dodge feedback | Short transparent streak behind Champion. |
| boss_warning_circle | Boss telegraph | Ground ring, pulsing danger color, clear timing. |

## Weather Profiles

| Key | Realm/Area | Visual | Gameplay Hook |
| --- | --- | --- | --- |
| mountain_snow_wind | Stonehold | Snow streaks, fog, wind gusts | Reduced distant visibility. |
| eldergrove_sunrain | Eldergrove | Light rain, glowing pollen, soft mist | Healing mood, gentle visibility. |
| crownlands_clear_storm | Crownlands | Clear daylight shifting to royal storm | Balanced default test weather. |
| umbral_ashfall | Umbral | Ash particles, ember sparks, heat haze | Hostile invasion mood. |
| warzone_battle_fog | War Zone | Dust, smoke wisps, banner silhouettes | RvR performance test. |

## Implementation Notes

- Use Unity ParticleSystem for the first vertical slice.
- Keep VFX prefabs under `Assets/AL/Prefabs/VFX` or generated placeholder prefabs under `Assets/AL/Art/Generated/Prefabs/VFX`.
- Keep Weather prefabs under `Assets/AL/Prefabs/Weather` or generated placeholder prefabs under `Assets/AL/Art/Generated/Prefabs/Weather`.
- Later, move high-end effects to VFX Graph for PC while keeping ParticleSystem fallback for mobile.

## Unity Generator

Run:

`Another Life > Generate Design Assets`

The generator creates starter materials, one modular Champion placeholder prefab, four skill effect prefabs, and four weather prefabs. These are prototype assets for blocking and gameplay testing, not final production art.

