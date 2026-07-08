# Another Life Modular Champion Design

This document defines the first Unity-ready character design direction for Another Life. It is original to this project and is meant to guide generated placeholder prefabs, later Blender/FBX production, and future player customization.

## Asset Goal

The player controls one main Champion/Lord in 3D Champion Mode. The base model should be modular so players can customize identity, silhouette, realm mood, class equipment, and cosmetic details without replacing the whole character.

## Base Character Requirements

- Mobile-first topology target: 8k to 18k triangles for the high-detail player model.
- Medium LOD target: 3k to 6k triangles.
- Low LOD target: 800 to 1500 triangles.
- Marker LOD: banner, silhouette, realm icon, or colored capsule for far RvR crowds.
- Humanoid rig compatible with Unity Humanoid Avatar.
- Separate skinned mesh parts for armor, clothing, hair, cape, and accessories.
- Shared skeleton for all player body variants.
- Pivot at ground center.
- Forward direction: positive Z.
- Scale: 1 Unity unit equals 1 meter.

## Customization Slots

| Slot | Purpose | Notes |
| --- | --- | --- |
| BodyPreset | Height, shoulders, torso, legs, build | Use blend shapes later; runtime currently includes average, slim, broad, tall, and stout presets. |
| Head | Face shape and ears | Realm variants can affect ears, beard, brows, and skin tones. |
| Hair | Hair mesh, beard mesh, color | Dwarves prioritize beard variants; elves prioritize long hair and ears. |
| Eyes | Eye color, glow, pupil style | Warmaster and realm effects can add emissive overlays. |
| Skin | Skin tone and markings | Include scars, tattoos, ash marks, glowing lines. |
| ArmorChest | Main armor silhouette | Realm/class style should be readable from distance. |
| Gloves | Hands and wrist armor | Keep weapon grips compatible. |
| Boots | Footwear and lower leg armor | Must not clip common leg presets. |
| Cape | Cloth or banner silhouette | Use simple cloth later; static mesh for prototype. |
| WeaponMain | Primary class weapon | Sword, staff, bow, scythe, dagger. |
| WeaponOff | Shield, orb, shuriken, second weapon | Optional per subclass. |
| BackAttachment | Quiver, relic, banner, book holster | Important for class readability. |
| PetAnchor | Common pet follow anchor | Non-combat pet attaches/follows here. |
| MountAnchor | Mount seating/preview anchor | Placeholder for later mount system. |

## Realm Visual Language

| Realm | Silhouette | Materials | Custom Details |
| --- | --- | --- | --- |
| Stonehold Dwarves | Broad, grounded, heavy | Granite, dark iron, copper, forge glow | Braids, rune plates, rivets, square armor shapes. |
| Eldergrove Elves | Tall, agile, flowing | Leaf cloth, pale gold, luminous wood, soft crystal | Long hair, curved armor, vine trims, feathered capes. |
| Crownlands Humans | Balanced, heroic, practical | Polished steel, blue cloth, royal gold, leather | Tabards, heraldic capes, clean plate, trade-road gear. |
| Umbral Dark Elves | Sharp, lean, predatory | Obsidian, red glass, ash cloth, shadow metal | Spikes, masks, glowing cracks, curse tattoos. |

## Class Silhouette Rules

- Guardian: shield must read clearly from the front and side.
- Barbarian: dual weapons and aggressive shoulder shape.
- Swordsman: large two-handed weapon and strong stance.
- Enchanter: staff/orb asymmetry and utility glow.
- Warlock: heavy two-handed staff, dark or elemental VFX anchor.
- Healer: large book focus and protective aura shape.
- Hunter: short bow, arrow case, beast command charm.
- Marksman: long bow, no quiver, mana-arrow glow.
- Forest Ranger: bow plus short sword, mobility cloak.
- Nightmare: twin scythes, fear/lifesteal visual accents.
- Shadow Assassin: dagger and shuriken, low-profile armor.
- Cursor: curse sigils, debuff relics, masked face variants.

## Runtime Forge Presets

The Champion Forge includes three adult-facing identity presets that live in the shared customization catalog and write into the same saved customization state as manual edits:

- Vanguard: broad body, warmaster/heavy plate language, sword and shield, scar detail, gold/steel material mood, cape and helmet enabled.
- Arcanist: tall body, arcane robes, long hair, rune mark, staff and tome, blue emissive accents, cape enabled, helmet disabled.
- Nightblade: slim body, assassin leathers, topknot, tattoo mark, bow and dagger, dark leather/crimson accent mood, cape and helmet disabled.

These presets are starting points only. Players can still fine tune every exposed color, body, hair, face mark, armor, weapon, offhand, cape, and helmet option after selecting one.

## Material Channels

Use one character shader with these color controls where possible:

- Primary cloth color
- Secondary cloth color
- Metal tint
- Leather tint
- Hair color
- Skin tone
- Eye color
- Emissive realm color
- Dirt/wear amount
- Warmaster overlay intensity

## First Prototype Model

The included editor generator creates and overwrites a modular placeholder Champion prefab using the same runtime `ProceduralChampionModelBuilder` used by Champion Mode. It is not the final production model, but it establishes:

- correct part names,
- transform hierarchy,
- realm material palette,
- customization slot organization,
- VFX anchor placement,
- mount and pet anchors.

The current premium placeholder includes close-read face planes, eye glints, layered hair variants, plated chest etching, rivets, robe focus detail, assassin mask support, heavier shoulder/forearm silhouette parts, cape folds/runes, and weapon/offhand trims so customization changes read clearly in the inspection showcase.

Run it in Unity from:

`Another Life > Generate Design Assets`

Generated assets should appear under:

`Assets/AL/Art/Generated/`
