# Champion Customization Source Inventory

Status: observational source freeze for the pure planner phase of issue #184. This document changes no production authority, authored choice, saved state, model, material, UI, scene, or Android behavior.

Inventory source: `main@fd416b23c55941e2e2dc46741fe29b2d408e354a`, audited 2026-07-31. Paths are repository-relative and identities are case-sensitive.

## Source and consumer evidence

| Role | Path | Bytes | SHA-256 | Current finding |
| --- | --- | ---: | --- | --- |
| Packaged technical catalog | `unity/Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json` | 13,342 | `3c0e265d947fa0e62c3042a4614a2dd50cdb36ee8e0272071ca2d241fdc8ab24` | Legacy `version: 0.5.0`, `game: Another Life`; lacks the issue #184 catalog-set envelope |
| Runtime catalog transport/model | `unity/Assets/AL/Scripts/ChampionMode/Customization/CharacterCustomizationCatalog.cs` | 4,790 | `dea5cde24f5c0fd1e3acfd21df006867584897dd2c7e26194dc12ed96c1039e4` | Direct-file/UWR loader; accepts only three required families and does not validate identity, hash, full references, or numeric bounds |
| Save-backed controller | `unity/Assets/AL/Scripts/ChampionMode/Customization/ChampionCustomizationController.cs` | 64,568 | `7ca1b11c2f49c9b1f36a70db75ac37fb2a536909a111b60927d9c4eaa0dac685` | Mixes catalog and hard-coded fallback authority and mutates the live save-backed object before a verified commit |
| Procedural model builder | `unity/Assets/AL/Scripts/ChampionMode/Customization/ProceduralChampionModelBuilder.cs` | 76,134 | `8878ee21a45e583fd8e770347fe95aa259de8950a9bc93d8e8723ac218084e5a` | Model support is inferred from runtime object names; no immutable capability revision exists |
| Durable state definition | `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs` | 7,821 | `bacfac499e8f2ac359a104054f5aef5f795f58f184c9febeb666ed6a69a15fbf` | Stores raw IDs/RGB/flags without catalog, migration, operation, or compatibility identity |
| JSON Schema | `unity/SharedContracts/Schemas/al-character-customization.schema.json` | 6,493 | `d09417a9dda13ed1a1a54e9e0496704215656281918daf9c0320bee306fe3db3` | Requires families including `realms` and `qualityTargets`; no production runtime execution/drift gate |
| Fable duplicate model | `unity/SharedContracts/Fable/AnotherLife.Contracts.fs` | 6,625 | `561089cab8e50dcb16f262847bd256158fa711063eb85d3685b6358f44716c74` | Independently maintained duplicate; no generated drift gate |

Known production callers include `DemoInitializer` and `ChampionArenaSceneController`. The approved narrative/content labels are a separate artifact and are not promoted to technical authority by this inventory.

## Exact catalog records

### Option IDs

| Family | Ordered technical IDs |
| --- | --- |
| Body | `average`, `slim`, `broad`, `tall`, `stout`, `duelist`, `statuesque`, `massive`, `compact` |
| Hair | `short`, `long`, `braid`, `mohawk`, `topknot` |
| Armor | `realm_basic`, `light_scout`, `heavy_plate`, `warmaster_plate`, `arcane_robes`, `assassin_leathers` |
| Face mark | `none`, `scar`, `warpaint`, `realm_mark`, `rune`, `tattoo`, `beard`, `duelist_scar`, `ash_mask` |
| Main weapon | `sword`, `axe`, `staff`, `bow`, `hammer` |
| Offhand | `shield`, `orb`, `dagger`, `tome`, `none` |

### Body scales

| ID | Exact XYZ scale |
| --- | --- |
| `average` | `(1.00, 1.00, 1.00)` |
| `slim` | `(0.86, 1.06, 0.86)` |
| `broad` | `(1.16, 1.00, 1.06)` |
| `tall` | `(0.96, 1.18, 0.96)` |
| `stout` | `(1.08, 0.92, 1.08)` |
| `duelist` | `(0.94, 1.08, 0.92)` |
| `statuesque` | `(1.02, 1.24, 0.98)` |
| `massive` | `(1.24, 1.04, 1.14)` |
| `compact` | `(1.02, 0.86, 1.02)` |

### Palette RGB

Every tuple below is the exact non-HDR RGB value in the audited technical catalog.

| Family | Ordered `id=(r,g,b)` records |
| --- | --- |
| Primary | `crown_blue=(.20,.40,1.00)`; `stone_bronze=(.45,.38,.30)`; `grove_green=(.18,.58,.32)`; `royal_gold=(.85,.62,.18)`; `umbral_violet=(.22,.08,.28)`; `obsidian_steel=(.055,.060,.070)`; `blood_wine=(.28,.035,.045)`; `ivory_battlecloth=(.82,.78,.66)`; `duelist_steel=(.18,.21,.24)` |
| Hair | `raven=(.08,.06,.04)`; `chestnut=(.55,.36,.16)`; `sun_blonde=(.85,.78,.55)`; `silver=(.80,.82,.90)`; `ember_black=(.25,.05,.08)`; `ivory=(.88,.84,.72)`; `ash_black=(.16,.16,.18)`; `copper=(.42,.24,.11)` |
| Skin | `sunlit=(.72,.56,.42)`; `deep_earth=(.55,.38,.26)`; `fair=(.86,.70,.54)`; `rose_ash=(.64,.50,.46)`; `umbral=(.42,.34,.40)`; `warm_umber=(.66,.48,.36)`; `porcelain_rose=(.78,.62,.52)`; `storm_ash=(.46,.34,.32)` |
| Eye | `storm_blue=(.25,.58,.92)`; `grove_green=(.28,.72,.42)`; `amber=(.70,.42,.18)`; `moonlit=(.78,.72,.88)`; `curse_red=(.90,.18,.12)`; `pale_gold=(.95,.64,.20)`; `jade_fire=(.58,1.00,.82)`; `void_red=(.95,.18,.08)` |
| Accent | `royal_gold=(.85,.62,.18)`; `arcane_blue=(.30,.75,1.00)`; `worldsap=(.42,1.00,.48)`; `ember=(.90,.12,.16)`; `shadow_violet=(.68,.28,.96)`; `blood_ember=(.94,.12,.08)`; `oracle_green=(.38,1.00,.74)`; `burnished_gold=(.92,.54,.16)` |

### Forge presets

Columns `P/H/S/E/A` are exact primary, hair, skin, eye, and accent RGB tuples.

| ID | Body / hair / armor / face / weapon / offhand | P / H / S / E / A | Cape / helmet |
| --- | --- | --- | --- |
| `vanguard` | `broad / short / warmaster_plate / scar / sword / shield` | `(.22,.27,.34) / (.07,.055,.045) / (.64,.48,.36) / (.86,.62,.24) / (.92,.64,.20)` | `true / true` |
| `arcanist` | `tall / long / arcane_robes / rune / staff / tome` | `(.08,.14,.32) / (.72,.74,.82) / (.68,.52,.44) / (.40,.82,1.00) / (.26,.78,1.00)` | `true / false` |
| `nightblade` | `slim / topknot / assassin_leathers / tattoo / bow / dagger` | `(.10,.095,.12) / (.18,.035,.055) / (.50,.38,.34) / (.84,.18,.14) / (.78,.12,.18)` | `false / false` |
| `dreadknight` | `massive / mohawk / warmaster_plate / ash_mask / hammer / shield` | `(.055,.060,.070) / (.16,.16,.18) / (.46,.34,.32) / (.95,.18,.08) / (.94,.12,.08)` | `true / true` |
| `oracle` | `statuesque / braid / arcane_robes / realm_mark / staff / orb` | `(.82,.78,.66) / (.88,.84,.72) / (.78,.62,.52) / (.58,1.00,.82) / (.38,1.00,.74)` | `true / false` |
| `duelist` | `duelist / short / light_scout / duelist_scar / sword / dagger` | `(.18,.21,.24) / (.42,.24,.11) / (.66,.48,.36) / (.95,.64,.20) / (.92,.54,.16)` | `false / false` |
| `inquisitor` | `tall / short / heavy_plate / realm_mark / sword / tome` | `(.12,.13,.14) / (.08,.06,.04) / (.72,.56,.42) / (.95,.64,.20) / (.92,.54,.16)` | `true / true` |
| `warden` | `broad / braid / heavy_plate / warpaint / axe / shield` | `(.12,.26,.18) / (.55,.36,.16) / (.55,.38,.26) / (.58,1.00,.82) / (.38,1.00,.74)` | `true / false` |
| `spellblade` | `duelist / long / arcane_robes / rune / sword / orb` | `(.08,.12,.22) / (.80,.82,.90) / (.64,.50,.46) / (.40,.82,1.00) / (.68,.28,.96)` | `true / false` |

### Current save defaults

`body=average`, `hair=short`, `armor=realm_basic`, `face=none`, `weapon=sword`, `offhand=shield`, primary `(.20,.40,1.00)`, hair `(.08,.06,.04)`, skin `(.72,.56,.42)`, eye `(.25,.58,.92)`, accent `(.85,.62,.18)`, cape `true`, helmet `false`.

## Drift and integration boundaries

- The controller fallback option arrays currently match the six catalog option families, but each controller fallback palette contains only the first five catalog records. The catalog contains additional colors.
- The controller also hard-codes nine complete presets and body scales, allowing a single runtime appearance to combine multiple unreported sources.
- The runtime transport omits schema/Fable-required `realms` and `qualityTargets`; Unity deserialization can ignore those fields.
- The current save has no schema/catalog/hash/alias/operation metadata and no raw/effective separation.
- Model capability is inferred from object names and cannot currently participate in a stable plan fingerprint.

This inventory approves no migration, bounds change, option removal, body-scale adjustment, RGB adjustment, label change, or production source switch. Those remain in the later #183/#137 integration sequence and under the ownership/approval boundaries in `Champion_Customization_Integrity_Spec.md`.
