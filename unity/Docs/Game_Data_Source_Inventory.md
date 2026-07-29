# Game Data Source And Consumer Inventory

Status: Phase B observational freeze for issue #183. This file changes no runtime authority.

Inventory source commit: `7e4ed2828e4e6df9dd33bdab3b3e4560651e45b7` (`main`, merged PR #262).

Audit scope: repository paths and committed values visible at the source commit above. Paths are repository-relative. All string identities below are case-sensitive unless a cited implementation explicitly does otherwise.

## Authority and reading rules

- Current effective source means the code or artifact that supplies values at runtime at the inventory commit. It does not mean that the source is approved as the long-term production authority.
- Codex engineering owns technical IDs, schemas, versions, loaders, validation, packaging, generated artifacts, and runtime integration. Codex narrative/content owns player-facing names, descriptions, lore, dialogue, quest meaning, and localization-facing copy. The user retains the approvals defined in `AGENTS.md`.
- Comments in retained Kotlin packets that call those packets “authoritative” are historical claims. They do not override `AGENTS.md`, do not supply Unity at runtime, and do not transfer ownership to Android or another assistant/tool.
- Phase B must preserve this inventory. It must not promote any legacy fallback, Android preview, terrestrial preview, or unvalidated JSON file to common production authority.
- New technical IDs use lower snake case. Existing PascalCase, uppercase, enum, and display-string identities remain frozen until a reviewed alias/migration decision is implemented.

## Family authority summary

| Family | Effective source at the inventory commit | Technical authority state | Narrative/content state | Schema/version | Runtime or preview path | Missing/invalid/fallback behavior | Migration owner/boundary |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Realms | Four runtime-created `RealmDefinition` objects in `LocalGameDataService` | Legacy effective source; mutable `ScriptableObject` values without provenance | Names, descriptions, and embedded perk prose require Codex narrative/content review | None | Created in memory in the service constructor; optional Editor templates target `Assets/AL/ScriptableObjects/Realms/*.asset`, but no `.asset` is committed | Unknown ID returns `null`; other systems independently substitute Crownlands or `RoyalSigil` in places | #183 catalog, #173 consumer; save compatibility under #137 |
| Buildings | Fifteen runtime-created `BuildingDefinition` objects in `LocalGameDataService` | Legacy effective source; definition lookup is not used by `LocalBuildingService` | Display names are derived technical placeholders | None | In memory; optional Editor templates target `Assets/AL/ScriptableObjects/Buildings/*.asset`, but no `.asset` is committed | Unknown definition returns `null`, while state services create level-1 state for any string; UI references missing `ManaShrine` and `Mine` | #183 catalog, #165 consumer, #137 persisted state |
| Research | Eight private `ResearchState` defaults in `LocalGameDataService` | Not a definition authority; state and definition identity are mixed and not queryable | Display strings double as IDs | None | In memory and discarded with service; actual state lives in the save | `LocalResearchService` creates level-0 state for any requested string | #183 catalog, #165 consumer, #137 aliases/save |
| Troops | `TroopDefinition` type only; enum-driven training/combat | No committed definition records | No current catalog copy | None | No `.asset`, JSON family, or runtime record | `IGameDataService.GetTroop` always returns `null`; training creates enum state without consulting a definition | #183 catalog, #165 consumer |
| Champions | `ChampionDefinition` type only; procedural runtime objects and customization state | No committed champion definition records | Forge labels/presets are mixed with technical customization | None for champion definitions; customization JSON is `0.5.0` | No champion `.asset`; procedural model and customization sources are separate | `GetChampion` always returns `null`; procedural Champion Mode continues independently | #183 catalog foundation, #180 champion/skill; #184 customization |
| Skills | Four hard-coded slot arrays in `SkillCaster`, partially overlaid by one JSON array | Competing, partial technical sources; integer slot still controls behavior | Display names and role strings live in JSON/hard-coded code | JSON `version: 0.3.0`; version is ignored | `Assets/AL/StreamingAssets/GameData/al_skill_weather_catalog.json` | Missing/invalid/partial JSON silently leaves per-field hard-coded values; `GetSkill` always returns `null` | #183 common contract, #180 migration |
| Bosses | `BossDefinition` type, optional `ProjectInitializer` templates, and serialized `BossDummyAI` defaults | No committed boss definition asset or catalog | Names/descriptions are unreviewed legacy copy | None | Optional templates target `Assets/AL/ScriptableObjects/Narrative/Bosses/*.asset`; none committed | Missing definition keeps `boss_dummy` controller defaults | Common envelope later; #168/#180 implementation |
| Equipment | `EquipmentDefinition` type, optional initializer templates, and loot fallback | No committed equipment definition asset or catalog | Display names are legacy copy | None | Optional templates target `Assets/AL/ScriptableObjects/Narrative/Loot/*.asset`; none committed | Empty loot table creates `ember_crown_shard`; invalid rates are clamped | Common envelope later; #168/#180 implementation and #137 save |
| Chapters | Twenty-nine runtime-created `ChapterDefinition` objects in `LocalGameDataService`, all discarded; Android packet copies also exist | No runtime chapter query or retained Unity catalog | All titles/lore require Codex narrative/content authority | None | In-memory construction only; `SaveGameData.CurrentChapterId` is a string | Save normalizer substitutes `C1` when blank; generated chapter objects are unavailable after construction | #128/#133 content/runtime and #137 save |
| Quests | Five runtime-created `QuestDefinition` objects in `LocalQuestService`; strict NVS-01 has its own canonical catalog | Local Q1–Q5 fallback is effective for the prototype; NVS-01 is independently strict and canonical only for `OMEN_1` | Quest copy belongs to Codex narrative/content; canonical NVS A1 packet is the retained source for that quest | Q1–Q5 none; NVS `schemaVersion: 1`, packet `omen1-a1-2026-07-29-v003` | Q1–Q5 in memory; NVS artifact at `Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json` | Q1–Q5 are regenerated; unknown saved quest rows survive but are filtered; NVS fails closed | #128/#133; QuestDefinition safety is #156; save is #137 |
| Customization | JSON plus matching hard-coded controller arrays/presets and save defaults | Separate legacy catalog, not part of Phase B publication | Player-facing option/preset copy needs content review | JSON `0.5.0`; version is ignored | `Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json` | Missing/invalid data uses runtime arrays; invalid saved IDs are normalized to hard-coded defaults | #184; #137 save compatibility |
| Weather/VFX | Weather values are hard-coded in `WeatherProfileData`; JSON contains an unconsumed duplicate; generated prefabs are previews | No single authority | Display names/use descriptions are legacy copy | Shared JSON `0.3.0`; ignored by weather runtime | JSON under `Assets/AL/StreamingAssets/GameData`; generated assets under `Assets/AL/Art/Generated` | Runtime creates a neutral default or per-realm hard-coded profile | #180/owning visual integration; not a Phase B family |
| World atlas | `LocalWorldAtlasService.BuildFallbackAtlas` | Constructor-built mutable fallback; no packaged atlas | Zone/objective names and descriptions require content review | None | In memory | Always constructs fallback zones; no unavailable result | Later shared envelope; #181 consumer/runtime |
| Terrestrials | Source-only packet and three concept sheets | Explicitly not runtime authority | Working display keys are not approved player-facing names | Manifest `schemaVersion: 1`, source `tdf-2026-07-15-v001` | `Docs/Terrestrials/**`, concept sheets under `Assets/AL/Art/Terrestrials/ConceptSheets` | No runtime catalog, spawning, combat, save, or fallback | User creative approval, then separate terrestrial-design handoff and engineering integration |

## Exact generated-value freeze

### Realms

`LocalGameDataService.InitializeFallbackData` creates exactly four runtime objects. `Icon` remains `null`.

| `RealmId` / numeric value | `RealmName` | Exact `Description` value after construction | Rare-resource mapping |
| --- | --- | --- | --- |
| `Stonehold` / `1` | `Stonehold Dwarves` | `Mountain kings and master smiths.\n\nPerks:\n+20% Stone\n+10% Def\n\nPerks: Resilience` | `DeepOre` |
| `Eldergrove` / `2` | `Eldergrove Elves` | `Forest guardians and peerless mages.\n\nPerks:\n+20% Wood\n+15% Magic\n\nPerks: Harmony` | `WorldSap` |
| `Crownlands` / `3` | `Crownlands Humans` | `Adaptive leaders of the central plains.\n\nPerks:\n+15% Gold\n+10% All Atk\n\nPerks: Ambition` | `RoyalSigil` |
| `Umbral` / `4` | `Umbral Dark Elves` | `Masters of shadow and volcanic power.\n\nPerks:\n+20% Crit\n+15% Speed\n\nPerks: Cunning` | `DarkCrystal` |

`RealmId.None` is numeric `0` and has no definition. `ResourceRules.GetRareResourceForRealm` nevertheless maps every unsupported value, including `None`, to `RoyalSigil`; several Champion/RvR paths independently substitute Crownlands. These are consumer fallbacks, not realm definitions.

`ProjectInitializer.SetupProject` can generate four different Editor template copies at `Assets/AL/ScriptableObjects/Realms/{RealmId}.asset`: the same IDs/names, but descriptions are respectively `Mountain kings and master smiths.`, `Ancient protectors of the world tree.`, `The adaptive and numerous people of the plains.`, and `Masters of shadow and clandestine arts.` No such assets are committed at the inventory commit.

### Buildings

Both `LocalGameDataService` and `ProjectInitializer` enumerate the same fifteen IDs. The runtime source creates mutable definitions with `Icon = null`; all have `MaxLevel = 10`.

| Ordinal | ID | Derived display name | Runtime definition committed as asset? |
| ---: | --- | --- | --- |
| 1 | `TownHall` | `Town Hall` | No |
| 2 | `Farm` | `Farm` | No |
| 3 | `LumberMill` | `Lumber Mill` | No |
| 4 | `Quarry` | `Quarry` | No |
| 5 | `GoldMine` | `Gold Mine` | No |
| 6 | `Barracks` | `Barracks` | No |
| 7 | `Academy` | `Academy` | No |
| 8 | `Market` | `Market` | No |
| 9 | `Storehouse` | `Storehouse` | No |
| 10 | `Forge` | `Forge` | No |
| 11 | `Stable` | `Stable` | No |
| 12 | `Workshop` | `Workshop` | No |
| 13 | `Embassy` | `Embassy` | No |
| 14 | `Wall` | `Wall` | No |
| 15 | `Watchtower` | `Watchtower` | No |

`ManaShrine` and `Mine` are consumer lookup IDs but have no current definition. They must remain explicit unavailable/invalid-definition cases; this inventory does not invent them.

### Research

`LocalGameDataService.InitializeAutomatedContent` creates exactly eight private `ResearchState` rows. Every row has `Level = 0`, `IsResearching = false`, and `CompleteTimestamp = 0`. The rows are not exposed through `IGameDataService` and are separate from the save-backed rows used by `LocalResearchService`.

| Ordinal | Current Unity identity | Android/shared competing identity, if present |
| ---: | --- | --- |
| 1 | `Steel Forging` | `steel_forging` |
| 2 | `Plate Armor` | `plate_armor` |
| 3 | `Advanced Masonry` | `masonry` |
| 4 | `Irrigation` | `irrigation` |
| 5 | `Ballistics` | none retained |
| 6 | `Logistics` | none retained |
| 7 | `Trade Routes` | none retained |
| 8 | `Arcane Study` | `arcane_study` appears in Android narrative hooks |

`LocalResearchService.GetStatBonus` looks up only `Steel Forging` for `StatType.Attack` and `Plate Armor` for `StatType.Defense`, then returns `Level * 0.05f`. It creates a save row on lookup. No automatic case/space conversion is an approved migration.

### Chapters generated and discarded by `LocalGameDataService`

Exactly 29 `ChapterDefinition` objects are created. `AddChapter` accepts a realm argument but never stores it because `ChapterDefinition` has no realm field. Each object has empty `InitialDialogueNodeId`, empty `RequiredQuestIds`, and empty `NextChapterId`; all objects are discarded after creation.

| # | Intended realm argument | ID | Title | Lore summary |
| ---: | --- | --- | --- | --- |
| 1 | `Stonehold` | `C1_SH` | `The Echoes of Iron` | Re-opening the ancestral Deep Forge and defeating Ferrum the Iron Dragon to prove your worth. |
| 2 | `Eldergrove` | `C1_EG` | `Whispers of the Sapling` | Investigating a blight on the World Tree and purging Virens the Blighted Dragon. |
| 3 | `Crownlands` | `C1_CL` | `The King's Decree` | Rebuilding the capital and seeking the blessing of Aurelius the Gold Dragon. |
| 4 | `Umbral` | `C1_UM` | `Shadows of the Void` | Rituals to stabilize the volcanic rifts and taming Nox the Void Dragon. |
| 5 | `Stonehold` | `C2_SH` | `The Smuggler's Trail` | Discovering Elven scouting parties deep in the mountain passes searching for the Ring of the Mountain King. |
| 6 | `Eldergrove` | `C2_EG` | `Shadows in the Mist` | Capturing a Human spy attempting to steal the Ring of Forest Harmony. |
| 7 | `Crownlands` | `C2_CL` | `Border Skirmishes` | Countering Dwarven expansion and protecting the Ring of Royal Decree. |
| 8 | `Umbral` | `C2_UM` | `Night's Whisper` | Sabotaging Human trade routes to retrieve the stolen Ring of Shadow Step. |
| 9 | `Stonehold` | `C3_SH` | `Heart of the Mountain` | The discovery of the first Ancestral Gem within the core forge. |
| 10 | `Eldergrove` | `C3_EG` | `The Forest's Tear` | A mystical gem is born from the tree's purest sap. |
| 11 | `Crownlands` | `C3_CL` | `The Sovereign's Jewel` | The discovery of a divine gem buried beneath the royal cathedral. |
| 12 | `Umbral` | `C3_UM` | `The Void Shard` | Retrieving a crystal from the heart of the volcanic rifts. |
| 13 | `Stonehold` | `C7_SH` | `The First King's Anvil` | Locating the legendary weapon of the founder. |
| 14 | `Eldergrove` | `C7_EG` | `Whisper of the Glade` | Restoring the original bow of the Forest Sentinels. |
| 15 | `Crownlands` | `C7_CL` | `The Golden Aegis` | Recovering the shield that stood during the First War. |
| 16 | `Umbral` | `C7_UM` | `Void's Edge` | Forging the blade from the remains of the First Rift. |
| 17 | `Stonehold` | `C10_SH` | `The Celestial Rift` | Ancient portals atop the mountain peaks begin to pulse with sky-light. |
| 18 | `Eldergrove` | `C10_EG` | `Whispers of the Sky` | The highest leaves of the World Tree touch a new realm of magic. |
| 19 | `Crownlands` | `C10_CL` | `The Sun-Gate Opens` | Portals appear in the clouds, revealing the path to the High Celestials. |
| 20 | `Umbral` | `C10_UM` | `Void's Reach` | Shadow magic begins to pierce the heavens themselves. |
| 21 | `Stonehold` | `C11_SH` | `Trial of the Granite King` | Confronting the guardians of the first floating fortress. |
| 22 | `Eldergrove` | `C11_EG` | `Emerald Sky Trial` | Navigating the magical storms of the upper islands. |
| 23 | `Crownlands` | `C11_CL` | `Radiant Vigil` | Proving your faith and strength to the Sky Wardens. |
| 24 | `Umbral` | `C11_UM` | `Midnight Ascent` | Infiltrating the light-fortresses of the sky. |
| 25 | `Stonehold` | `C12_SH` | `Throne of the Mountain Sky` | Reaching the ultimate seat of power and meeting the High Celestial. |
| 26 | `Eldergrove` | `C12_EG` | `Glade of the Stars` | Securing the forest's place among the celestial powers. |
| 27 | `Crownlands` | `C12_CL` | `Empire of Light` | Establishing a holy covenant between earth and sky. |
| 28 | `Umbral` | `C12_UM` | `The Void Throne` | Claiming the sky for the shadows of the rift. |
| 29 | `None` | `C_OMEN` | `The Otherworld Omen` | Strange signals from beyond the celestial rift suggest we are not alone. |

The source comment says “Chapters 7-9,” but only C7 records exist; there are no generated C8 or C9 records.

### Skill-soul quests generated and discarded by `LocalGameDataService`

Exactly 16 `SkillSoulQuestDefinition` objects are created, one for every non-`None` `SubclassId`. Each ID is interpolated as `SQ_{enum name}`. All retain the class defaults `MinLevel = 100`, `RequiredChapterId = C12`, `AscensionSkillId = null`, `RewardResources = null`, and `RewardXP = 0`; every object is discarded.

| # | Subclass | ID | Title | Description |
| ---: | --- | --- | --- | --- |
| 1 | `Vanguard` | `SQ_Vanguard` | `Frontline Eternity` | Stand as the immovable object against the Celestial Tide. |
| 2 | `Guardian` | `SQ_Guardian` | `The Unbreakable Vow` | Protect the Celestial Gate from an infinite onslaught. |
| 3 | `Berserker` | `SQ_Berserker` | `Primal Rage` | Tame a legendary star-lion in a cosmic storm. |
| 4 | `Pyromancer` | `SQ_Pyromancer` | `Sun-Fire Ascension` | Absorb the heat of the celestial sun into your core. |
| 5 | `Cryomancer` | `SQ_Cryomancer` | `Absolute Zero` | Freeze the floating waterfalls of the Sky Castle. |
| 6 | `Archmage` | `SQ_Archmage` | `Void Ascension` | Merge celestial light with shadow rift magic. |
| 7 | `Sharpshooter` | `SQ_Sharpshooter` | `Star-Piercer` | Strike a target on the furthest floating island. |
| 8 | `Stalker` | `SQ_Stalker` | `The Celestial Hunt` | Track a creature made of pure starlight. |
| 9 | `Beastmaster` | `SQ_Beastmaster` | `Sky-Bond` | Tame a High Celestial Gryphon. |
| 10 | `Shadowblade` | `SQ_Shadowblade` | `Event Horizon` | Become one with the shadow cast by the Celestial Gate. |
| 11 | `Infiltrator` | `SQ_Infiltrator` | `Heaven's Ghost` | Bypass the divine sentinels without being detected. |
| 12 | `Nightstalker` | `SQ_Nightstalker` | `Void Reaper` | Execute the shadows lurking in the celestial gardens. |
| 13 | `Paladin` | `SQ_Paladin` | `Divine Resonance` | Synchronize your armor with the High Celestial's song. |
| 14 | `Necromancer` | `SQ_Necromancer` | `Celestial Decay` | Study the life-cycles of the eternal sky-beings. |
| 15 | `Slayer` | `SQ_Slayer` | `God-Killer` | Defeat the guardian of the Forbidden Island. |
| 16 | `Druid` | `SQ_Druid` | `World-Root Reach` | Connect the World Tree to the floating islands. |

### Runtime-created prototype quests

`LocalQuestService` retains exactly five definitions in `_definitions`. Each has one reward row (`Gold`, `1000`); `RewardCredits = 0`, `RewardXP = 0`, hidden/trigger fields retain defaults.

| ID | Title | Description | Type | Target |
| --- | --- | --- | --- | ---: |
| `Q1` | `Foundation` | Upgrade any building to Level 2. | `BuildBuilding` | 1 |
| `Q2` | `Legion` | Train 100 total troops. | `TrainTroops` | 100 |
| `Q3` | `Arcane Study` | Complete 1 research project. | `ResearchTech` | 1 |
| `Q4` | `War Path` | Win 3 tactical battles. | `WinBattle` | 3 |
| `Q5` | `Expander` | Capture 1 territory. | `CaptureTerritory` | 1 |

The Android `KingdomModels.kt` Q1–Q4 titles differ, Android Q3 targets 2 rather than 1, and Android also declares `OMEN_1` and `OMEN_2`. Those values are not Unity runtime authority.

### Optional Editor-generated bosses and equipment

`ProjectInitializer` is Editor-only. It can create these assets, but none is committed at the inventory commit.

| Boss asset ID | Name | Description | Health | Attack | Armor | Target path |
| --- | --- | --- | ---: | ---: | ---: | --- |
| `ferrum` | `Ferrum` | Iron Dragon of Stonehold. High Armor, physical AoE. | 5000 | 150 | 200 | `Assets/AL/ScriptableObjects/Narrative/Bosses/ferrum.asset` |
| `virens` | `Virens` | Green Dragon of Eldergrove. Poison DoT, healing reduction. | 4000 | 180 | 100 | `Assets/AL/ScriptableObjects/Narrative/Bosses/virens.asset` |
| `aurelius` | `Aurelius` | Gold Dragon of Crownlands. High Magic damage, blinding effects. | 4500 | 200 | 120 | `Assets/AL/ScriptableObjects/Narrative/Bosses/aurelius.asset` |
| `nox` | `Nox` | Void Dragon of Umbral. Stealth phases, health lifesteal. | 3500 | 250 | 80 | `Assets/AL/ScriptableObjects/Narrative/Bosses/nox.asset` |
| `ruin_stone` | `The Granite Warden` | Ancient automaton guarding the First King's Anvil. | 12000 | 400 | 600 | `Assets/AL/ScriptableObjects/Narrative/Bosses/ruin_stone.asset` |
| `ruin_elf` | `The Spectral Stag` | Ghostly protector of the Whisper Glade. | 10000 | 500 | 300 | `Assets/AL/ScriptableObjects/Narrative/Bosses/ruin_elf.asset` |
| `ruin_human` | `The Fallen Paladin` | Corrupted guardian of the Golden Aegis. | 11000 | 600 | 450 | `Assets/AL/ScriptableObjects/Narrative/Bosses/ruin_human.asset` |
| `ruin_dark` | `The Abyssal Shade` | Manifestation of the First Rift. | 9000 | 800 | 200 | `Assets/AL/ScriptableObjects/Narrative/Bosses/ruin_dark.asset` |
| `cinders` | `The Behemoth of Cinders` | Massive volcanic colossus. Incomparably hard. Deals massive fire AoE. | 50000 | 1500 | 1000 | `Assets/AL/ScriptableObjects/Narrative/Bosses/cinders.asset` |
| `abyssal` | `The Abyssal Horror` | Eldritch monstrosity from the depths. Incomparably hard. Drains mana and sanity. | 60000 | 1200 | 800 | `Assets/AL/ScriptableObjects/Narrative/Bosses/abyssal.asset` |

Every generated boss has `Icon = null`, empty `SpecialAbilities`, and an empty `PossibleLoot` list because the initializer does not assign those fields.

| Equipment ID | Display name | Slot | Drop rate | Announce | Target path |
| --- | --- | --- | ---: | --- | --- |
| `ring_stonehold` | `Ring of the Mountain King` | `Trinket` | 0.001 | true | `Assets/AL/ScriptableObjects/Narrative/Loot/ring_stonehold.asset` |
| `ring_eldergrove` | `Ring of Forest Harmony` | `Trinket` | 0.001 | true | `Assets/AL/ScriptableObjects/Narrative/Loot/ring_eldergrove.asset` |
| `ring_crownlands` | `Ring of Royal Decree` | `Trinket` | 0.001 | true | `Assets/AL/ScriptableObjects/Narrative/Loot/ring_crownlands.asset` |
| `ring_umbral` | `Ring of Shadow Step` | `Trinket` | 0.001 | true | `Assets/AL/ScriptableObjects/Narrative/Loot/ring_umbral.asset` |
| `amulet_warzone` | `Amulet of the Warzone` | `Trinket` | 0.0005 | true | `Assets/AL/ScriptableObjects/Narrative/Loot/amulet_warzone.asset` |
| `pendant_eternity` | `Pendant of Eternity` | `Trinket` | 0.0005 | true | `Assets/AL/ScriptableObjects/Narrative/Loot/pendant_eternity.asset` |

Every generated equipment row has `Icon = null` and attack/defense/health bonuses of 0. Separately, `BossDummyAI` defaults to `boss_dummy`, `Boss Dummy`, 1200 health, 500 Warzone credits, and no loot definitions. `LocalBossLootService` and the controller exception path both create `ember_crown_shard` (`Ember Crown Shard`, `Trinket`, quantity 1, announced) when no valid loot table is available.

## ScriptableObject definition and GUID inventory

There are no committed `.asset` files anywhere below `unity/Assets` at the inventory commit. The `Assets/AL/ScriptableObjects` family directories are empty except for folder `.meta` files. The complete committed `CreateAssetMenu` definition inventory is:

| Definition type | C# path | Script GUID | `CreateAssetMenu.fileName` | `CreateAssetMenu.menuName` |
| --- | --- | --- | --- | --- |
| `BossDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/BossDefinition.cs` | `f23af44eac208b04c84222c4f8d71ba7` | `New Boss` | `AL/Data/Boss` |
| `BuildingDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/BuildingDefinition.cs` | `a813f6909dfc4ac429a6689c945bffde` | `New Building` | `AL/Data/Building` |
| `ChampionDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/ChampionDefinition.cs` | `98a401e21a1cb094281e642f9c165ccd` | `New Champion` | `AL/Data/Champion` |
| `ClassDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/ClassDefinition.cs` | `896ccde6c70ccfd42a6a54c444c57e5e` | `New Class` | `AL/Data/Class` |
| `EquipmentDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/EquipmentDefinition.cs` | `3ede58e073fadad46b504013f7f9bca5` | `New Equipment` | `AL/Data/Equipment` |
| `RealmDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/RealmDefinition.cs` | `4f5d27c3dbe1dca408f6f67fe65823ec` | `New Realm` | `AL/Data/Realm` |
| `SkillDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/SkillDefinition.cs` | `757bee93161a98d498611fb4593170fa` | `New Skill` | `AL/Data/Skill` |
| `TroopDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/TroopDefinition.cs` | `d609ad4f7bc125843b0f716347aacc2e` | `New Troop` | `AL/Data/Troop` |
| `WarmasterSetDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/WarmasterSetDefinition.cs` | `f844873918bf4c2478c9aefb56f3ae25` | `New Warmaster Set` | `AL/Data/WarmasterSet` |
| `ArtifactDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/Narrative/ArtifactDefinition.cs` | `3694787773aef85489a54501e05df2c5` | `New Artifact` | `AL/Narrative/Artifact` |
| `ChapterDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/Narrative/ChapterDefinition.cs` | `f57ceba55935f484f82e21357830f94a` | `New Chapter` | `AL/Narrative/Chapter` |
| `FactionDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/Narrative/FactionDefinition.cs` | `ca4c7423166f0f745a9ba5c07fc9c193` | `New Faction` | `AL/Narrative/Faction` |
| `GemDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/Narrative/GemDefinition.cs` | `13928cc968ff0c44f9401cae771da054` | `New Gem` | `AL/Narrative/Gem` |
| `NPCDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/Narrative/NPCDefinition.cs` | `737ef00ec440e1e4dbef682bbdf6dba5` | `New NPC` | `AL/Narrative/NPC` |
| `QuestDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/Narrative/QuestDefinition.cs` | `c385b2b183b74184ca75eeffbe2256ef` | `New Quest` | `AL/Narrative/Quest` |
| `SideQuestDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/Narrative/SideQuestDefinition.cs` | `77433c570d0a0c7469488ae941f9aca4` | `New Side Quest` | `AL/Data/SideQuest` |
| `SkillSoulQuestDefinition` | `unity/Assets/AL/Scripts/Data/Definitions/Narrative/SkillSoulQuestDefinition.cs` | `c0406b0d6e13db246bca0b1392bdc111` | `New Skill Soul Quest` | `AL/Narrative/SkillSoulQuest` |

`QuestDefinition` is the only definition with a dedicated authority validator. `QuestDefinitionAssetAuthorityValidator` also retains removed historical root GUID `226022aa7500f3e4abc8ac3757707ad8` solely to detect stale serialized references.

## Packaged, schema, Fable, Kotlin, and C# source paths

### Packaged artifacts and duplicate contracts

| Purpose | Path | Identity/version | SHA-256 at inventory commit | Consumer/validation state |
| --- | --- | --- | --- | --- |
| Character customization JSON | `unity/Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json` | game `Another Life`, version `0.5.0` | `c4b02537edad89ff9ca939e7d449c536e05092b307c1a158035b48d3094059e4` | Loaded by `CharacterCustomizationCatalog`; version/game/hash/schema are not checked |
| Skill/effect/weather JSON | `unity/Assets/AL/StreamingAssets/GameData/al_skill_weather_catalog.json` | version `0.3.0` | `fadbc0dee939f585038c8df552c87f31c2e2ee0112019052e8ffb8cbd3e2061c` | Only `skillLoadouts` is represented/loaded by `SkillLoadoutCatalog`; effect/weather sections are not consumed there |
| Customization JSON Schema | `unity/SharedContracts/Schemas/al-character-customization.schema.json` | JSON Schema draft 2020-12, local `$id` | `8e4362c4ceeea31c665910195c0f37b83e888b48974b183ff66a7b0a08322638` | Documentation/tooling only; no repository test executes it |
| Skill/weather JSON Schema | `unity/SharedContracts/Schemas/al-skill-weather.schema.json` | JSON Schema draft 2020-12, local `$id` | `f3b9f5cf06ffcbc38363a626129eff5c18c3725d1ef860732eb3bdc39e1b5647` | Documentation/tooling only; no repository test executes it |
| Fable/F# duplicate contract | `unity/SharedContracts/Fable/AnotherLife.Contracts.fs` | `CharacterCustomizationCatalog`, `SkillWeatherCatalog`, prototype save records | `93ea3242fcfb13a2fd0b3a51b0d6c8624bf955b41048e243f16e0c7532e775af` | Not generated from schemas and has no drift check |
| Fable project | `unity/SharedContracts/Fable/AnotherLife.Contracts.fsproj` | .NET project wrapper | not a runtime artifact | No Unity/Android runtime reference |
| Shared-contract routing notes | `unity/SharedContracts/README.md` | maps two JSON files to two schemas/F# records | not a runtime artifact | Manual instructions only |
| Canonical NVS source | `unity/Docs/Narrative/NVS_01/OMEN_1_A1.packet.json` | `schemaVersion: 1`, `omen1-a1-2026-07-29-v003`, `NVS-01`, `OMEN_1` | `8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732` | Codex narrative/content source; already matches the canonical runtime bytes |
| Derived NVS runtime artifact | `unity/Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json` | same logical identity; canonical length 8317 | `8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732` | Strict validator, hash/length/version check, direct-file/UWR byte source, fail-closed loader |

The NVS source and artifact are byte-identical canonical UTF-8. The NVS artifact is intentionally under `Assets/StreamingAssets`, while the two legacy shared JSON files are under `Assets/AL/StreamingAssets`; the NVS tests explicitly reject an `Assets/AL/StreamingAssets` duplicate.

### Relevant C# contract, source, loader, consumer, and validation paths

| Role | Paths |
| --- | --- |
| Core ID enums and mappings | `unity/Assets/AL/Scripts/Core/Enums/Enums.cs`; `unity/Assets/AL/Scripts/Core/ResourceRules.cs` |
| Current game-data interface/source | `unity/Assets/AL/Scripts/Core/Interfaces/IGameDataService.cs`; `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs` |
| Realm consumers | `unity/Assets/AL/Scripts/Services/Local/LocalRealmService.cs`; `unity/Assets/AL/Scripts/UI/RealmSelection/RealmSelectionController.cs`; `unity/Assets/AL/Scripts/UI/RealmSelection/RealmSelectionCard.cs` |
| Building/research/troop services | `unity/Assets/AL/Scripts/Services/Local/LocalBuildingService.cs`; `unity/Assets/AL/Scripts/Kingdom/Research/LocalResearchService.cs`; `unity/Assets/AL/Scripts/Services/Local/LocalTrainingService.cs` |
| Building/research UI and simulation consumers | `unity/Assets/AL/Scripts/UI/Kingdom/KingdomSceneController.cs`; `unity/Assets/AL/Scripts/UI/Kingdom/KingdomCommandPolicy.cs`; `unity/Assets/AL/Scripts/Kingdom/CityLayoutEngine.cs`; `unity/Assets/AL/Scripts/Kingdom/Visuals/KingdomVisualizer.cs`; `unity/Assets/AL/Scripts/Services/Local/LocalResourceService.cs`; `unity/Assets/AL/Scripts/Battle/Simulator/DeterministicBattleSimulator.cs`; `unity/Assets/AL/Scripts/Utilities/DemoInitializer.cs` |
| Champion/skill/customization | `unity/Assets/AL/Scripts/ChampionMode/Skills/SkillCaster.cs`; `unity/Assets/AL/Scripts/ChampionMode/Skills/SkillLoadoutCatalog.cs`; `unity/Assets/AL/Scripts/ChampionMode/Customization/CharacterCustomizationCatalog.cs`; `unity/Assets/AL/Scripts/ChampionMode/Customization/ChampionCustomizationController.cs`; `unity/Assets/AL/Scripts/ChampionMode/Customization/ProceduralChampionModelBuilder.cs`; `unity/Assets/AL/Scripts/ChampionMode/ChampionArenaSceneController.cs` |
| Weather and generated design source | `unity/Assets/AL/Scripts/RealmWar/Warzone/WeatherProfileData.cs`; `unity/Assets/AL/Scripts/RealmWar/Warzone/RuntimeWeatherController.cs`; `unity/Assets/AL/Scripts/Utilities/ALDesignAssetGenerator.cs`; `unity/Assets/AL/Art/Designs/ModularChampionCustomization.md`; `unity/Assets/AL/Art/Designs/SkillEffectsAndWeather.md` |
| Boss/equipment runtime | `unity/Assets/AL/Scripts/ChampionMode/AI/BossDummyAI.cs`; `unity/Assets/AL/Scripts/Core/Interfaces/IBossLootService.cs`; `unity/Assets/AL/Scripts/Services/Local/LocalBossLootService.cs` |
| Chapter/quest runtime | `unity/Assets/AL/Scripts/Kingdom/Story/LocalStoryService.cs`; `unity/Assets/AL/Scripts/Kingdom/Quests/LocalQuestService.cs`; `unity/Assets/AL/Scripts/Kingdom/Quests/SideQuestService.cs`; `unity/Assets/AL/Scripts/Core/Interfaces/IQuestService.cs` |
| NVS contract/source/loader/runtime | `unity/Assets/AL/Scripts/Narrative/Nvs01/Contracts/Nvs01CatalogModels.cs`; `unity/Assets/AL/Scripts/Narrative/Nvs01/Nvs01CatalogValidator.cs`; `unity/Assets/AL/Scripts/Narrative/Nvs01/Nvs01CatalogLoader.cs`; `unity/Assets/AL/Scripts/Narrative/Nvs01/Nvs01QuestRuntime.cs`; `unity/Assets/AL/Scripts/Narrative/Nvs01/INvs01QuestRuntime.cs`; `unity/Assets/AL/Scripts/UI/Kingdom/Nvs01KingdomPresenter.cs`; `unity/Assets/AL/Scripts/UI/Kingdom/KingdomSceneController.cs`; `unity/Assets/AL/Scripts/ChampionMode/Narrative/Nvs01ChampionEncounterAdapter.cs`; `unity/Assets/AL/Editor/Narrative/ExportNvs01Catalog.cs` |
| World atlas | `unity/Assets/AL/Scripts/Core/Interfaces/IWorldAtlasService.cs`; `unity/Assets/AL/Scripts/RealmWar/World/LocalWorldAtlasService.cs`; `unity/Assets/AL/Scripts/RealmWar/World/WorldObjectiveMarkerSpawner.cs` |
| Editor preview generator | `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs` |
| Save/reference state | `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`; `unity/Assets/AL/Scripts/Data/Runtime/BuildingState.cs`; `unity/Assets/AL/Scripts/Data/Runtime/WarmasterState.cs`; `unity/Assets/AL/Scripts/Core/Interfaces/IResearchService.cs`; `unity/Assets/AL/Scripts/Core/Interfaces/IQuestService.cs`; `unity/Assets/AL/Scripts/Core/Interfaces/ITerritoryService.cs`; `unity/Assets/AL/Scripts/Services/Local/LocalSaveGameService.cs` |
| Current validators/tests | `unity/Assets/AL/Editor/Validation/QuestDefinitionAssetAuthorityValidator.cs`; `unity/Assets/AL/Tests/EditMode/QuestDefinitionAssetAuthorityTests.cs`; `unity/Assets/AL/Tests/EditMode/QuestSaveCompatibilityTests.cs`; `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01CatalogTests.cs`; `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01QuestRuntimeTests.cs`; `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01KingdomPresenterTests.cs`; `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01KingdomSceneWiringTests.cs`; `unity/Assets/AL/Tests/EditMode/BootloaderServiceStackIntegrityTests.cs`; `unity/Assets/AL/Tests/EditMode/RuntimeContractSmokeTests.cs` |

### Kotlin source and consumer paths

There is no serializer, generated-code step, schema check, file bridge, or runtime API connecting these Kotlin values to the Unity sources.

| Kotlin path | Data/IDs retained there | Current consumers |
| --- | --- | --- |
| `app/src/main/java/com/example/anotherlife/data/simulation/KingdomModels.kt` | buildings `farm`, `lumber_mill`, `quarry`, `gold_mine`, `barracks`; troops `Infantry`, `Cavalry`, `Ranged`; research `steel_forging`, `plate_armor`, `masonry`, `irrigation`; quests `Q1`–`Q4`, `OMEN_1`, `OMEN_2`; territories `T1`–`T5` | `AdaptiveShell.kt`, `AcademyScreen.kt`, `BattleSimulatorScreen.kt`, `DossierScreen.kt`, `KingdomDashboard.kt`, `QuestScreen.kt`, `WarzoneMapScreen.kt` |
| `app/src/main/java/com/example/anotherlife/data/simulation/Chapter1_Packet.kt` | chapters `C1_CL`, `C1_SH`, `C1_EG`, `C1_UM`; quest chains including `OMEN_1`; advisors; `CH2_THE_TREASURE_HUNT` | No external Kotlin consumer at the inventory commit |
| `app/src/main/java/com/example/anotherlife/data/simulation/Chapter1_Quests_Packet.kt` | `CL_REBUILD_1`, `SH_FORGE_1` and handoff/consequence strings | No external Kotlin consumer |
| `app/src/main/java/com/example/anotherlife/data/simulation/NVS_01_Packet.kt` | older `OMEN_1` states/dialogue/consequences and `REW_OMEN_1_TEAR` identity | `AdaptiveShell.kt`, `NarrativeDebugScreen.kt`, `NarrativeDebugTriggers.kt`; not the Unity canonical NVS artifact |
| `app/src/main/java/com/example/anotherlife/data/simulation/RealmHooks.kt` | realm string IDs and advisor/faction hooks | No external Kotlin consumer |
| `app/src/main/java/com/example/anotherlife/data/simulation/BuildingHooks.kt` | lower-snake building/research IDs and narrative hook IDs | No external Kotlin consumer |
| `app/src/main/java/com/example/anotherlife/data/simulation/WorldAtlasHooks.kt` | `SKY_CASTLE`, `SILVER_WOODS`, `CH0_PROLOGUE`, `C1_CL`, `C1_EG` | No external Kotlin consumer |
| `app/src/main/java/com/example/anotherlife/data/simulation/AdvisorPersonas.kt` | four `ADVISOR_*` IDs | `AdaptiveShell.kt` |
| `app/src/main/java/com/example/anotherlife/data/simulation/FactionProfiles.kt` | four `FACT_*` IDs | `AdaptiveShell.kt` |
| `app/src/main/java/com/example/anotherlife/data/simulation/NarrativeModels.kt` | Kotlin-only dialogue/persona/faction/state shapes; initial chapter `CH0_PROLOGUE` | `AdaptiveShell.kt`, `DossierScreen.kt`, `NarrativeDebugScreen.kt`, `NarrativeDebugTriggers.kt`, `StoryDialogueScreen.kt` |
| `app/src/main/java/com/example/anotherlife/data/simulation/NarrativeTemplates.kt` | unused authoring shapes | No external Kotlin consumer |
| `app/src/main/java/com/example/anotherlife/data/simulation/DossierNarrative.kt` | three Chapter 0 UI metadata rows | No external Kotlin consumer |

## Consumer lookup and reference inventory

### Realm IDs

Canonical current enum spellings are `None`, `Stonehold`, `Eldergrove`, `Crownlands`, and `Umbral` in `Enums.cs`. Direct `IGameDataService` consumers are:

- `LocalRealmService.CurrentRealm` calls `GetRealm(CurrentRealmId)` where the ID comes from `SaveGameData.SelectedRealm`.
- `RealmSelectionController` calls `GetAllRealms()` and presents whatever mutable definitions the service returns.
- `LocalBuildingService` and `LocalStoryService` receive `IGameDataService` but never use it at the inventory commit.
- No production caller invokes `IGameDataService.GetBuilding`, `GetTroop`, `GetChampion`, or `GetSkill`.

Realm-coupled consumers using the enum directly include `ResourceRules`, `DeterministicBattleSimulator`, `BossDummyAI`, `BotChampionAI`, `RvrBotSpawner`, `ChampionArenaSceneController`, `ChampionController`, `SkillCaster`, `SkillEffectFactory`, `BattleModels`, `CityLayoutEngine`, `LocalStoryService`, `KingdomVisualizer`, `TerritoryContractPlanner`, `WarzoneService`, `WeatherProfileData`, `LocalWorldAtlasService`, `WorldObjectiveMarkerSpawner`, `LocalRealmGemService`, `LocalResourceService`, `LocalSaveGameService`, `BootController`, `KingdomSceneController`, `DemoInitializer`, and `ProjectInitializer`. This list matters because several paths encode their own fallback or switch mapping rather than querying a realm catalog.

### Building IDs

| Consumer/source path | Exact IDs or matching rule |
| --- | --- |
| `LocalGameDataService.cs`, `ProjectInitializer.cs` | all 15 definition IDs listed above |
| `KingdomSceneController.cs` | `TownHall`, `Farm`, `LumberMill`, `Quarry`, `GoldMine`, `ManaShrine`, `Mine`, `Barracks` |
| `KingdomCommandPolicy.cs` | mutation targets `Farm`, `Quarry`, `Mine` |
| `KingdomVisualizer.cs` | `TownHall`, `Farm`, `Barracks` |
| `LocalResourceService.cs` | `Farm`, `LumberMill`, `Quarry`, `GoldMine`, `ManaShrine`, `Mine`, `TownHall` |
| `DemoInitializer.cs` | `Farm` |
| `CityLayoutEngine.cs` | substring/replace rules for `Hall`, `Barracks`, `Farm`, `Lumber`, `Mana`, `Gold`, `Mine`, `Quarry`, plus replacements for `TownHall`, `LumberMill`, `GoldMine`, `ManaShrine`; this is not exact-ID validation |
| `LocalBuildingService.cs` | accepts any caller-supplied string and creates a level-1 `BuildingState`; injected `IGameDataService` is unused |
| Android `KingdomModels.kt` | lower-snake `farm`, `lumber_mill`, `quarry`, `gold_mine`, `barracks` |
| Android `BuildingHooks.kt` | lower-snake `barracks`, `academy`, `forge`, `gold_mine` |

### Research IDs

| Consumer/source path | Exact IDs |
| --- | --- |
| `LocalGameDataService.cs` | the eight display-string IDs listed above; private and unqueried |
| `LocalResearchService.cs` | caller-supplied strings; direct stat lookups `Steel Forging`, `Plate Armor` |
| `KingdomSceneController.cs` | exact lookups `Steel Forging`, `Plate Armor` from a save-backed enumeration; it does not validate definitions |
| Android `KingdomModels.kt` | `steel_forging`, `plate_armor`, `masonry`, `irrigation` |
| Android `BuildingHooks.kt` | `steel_forging`, `arcane_study` |

### Troop IDs/types

- Unity `TroopType` values are `Infantry`, `Cavalry`, `Ranged`, and `Siege`; those enum values, not stable string record IDs, drive `LocalTrainingService`, `SaveGameData`, battle models, `DeterministicBattleSimulator`, and demo calls.
- `DeterministicBattleSimulator` hard-codes base power 10/15/12/20 for those four enum values and additional enum-specific matchup logic.
- `IGameDataService.GetTroop(string)` returns `null` and has no caller.
- Android uses string values `Infantry`, `Cavalry`, and `Ranged`; it has no `Siege` row in `KingdomModels.kt`.

### Champion, customization, and skill IDs

- There is no committed `ChampionDefinition.Id`, no champion asset, and no `GetChampion` caller. Champion Mode creates procedural GameObjects and uses realm/customization state instead.
- Customization option ID sets duplicated in `ChampionCustomizationController` and JSON are: body `average`, `slim`, `broad`, `tall`, `stout`, `duelist`, `statuesque`, `massive`, `compact`; hair `short`, `long`, `braid`, `mohawk`, `topknot`; armor `realm_basic`, `light_scout`, `heavy_plate`, `warmaster_plate`, `arcane_robes`, `assassin_leathers`; face `none`, `scar`, `warpaint`, `realm_mark`, `rune`, `tattoo`, `beard`, `duelist_scar`, `ash_mask`; weapon `sword`, `axe`, `staff`, `bow`, `hammer`; offhand `shield`, `orb`, `dagger`, `tome`, `none`.
- Forge preset IDs are `vanguard`, `arcanist`, `nightblade`, `dreadknight`, `oracle`, `duelist`, `inquisitor`, `warden`, and `spellblade`. All nine have JSON rows and hard-coded fallback methods; `ChampionArenaSceneController` looks them up by these exact strings.
- Hard-coded `SkillCaster` slot 0–3 IDs are `realm_strike`, `renewing_guard`, `warzone_burst`, `warmaster_breaker`. Corresponding VFX keys are `realm_slash`, `renewing_guard`, `warzone_shockwave`, `warmaster_breaker`.
- The JSON contains the same four slot/skill rows. `SkillLoadoutCatalog` accepts any nonempty `skillLoadouts` array, ignores `version`, and exposes mutable arrays. `SkillCaster` applies only present valid-slot fields; missing slots/blank strings/non-finite numeric values retain hard-coded values, so a partial file is silently mixed with fallback and reported as applied.
- `SkillCaster` still switches gameplay behavior on slot index rather than skill ID. `ChampionArenaSceneController` reads names for slots 0–3. `GetSkillVfxKey` has no external caller, and JSON `skillEffects` keys are not a behavior authority.
- JSON skill-effect keys are `stonehold_forge_burst`, `eldergrove_healing_bloom`, `crownlands_royal_strike`, and `umbral_curse_mark`. The committed generated prefab names mirror those concepts, but runtime `SkillEffectFactory` does not query them by these keys.
- JSON weather keys are `stonehold_mountain_snow_wind`, `eldergrove_sunrain`, `crownlands_clear_storm`, `umbral_ashfall`, and `neutral_battle_fog`. `WeatherProfileData` separately hard-codes the same effective IDs/values and `RuntimeWeatherController` uses that C# source, not the JSON.

`CharacterCustomizationCatalog` represents neither the JSON `realms` nor `qualityTargets` fields, and Unity `JsonUtility` ignores them. It validates only that body, hair, and armor arrays are nonempty. `ChampionCustomizationController` falls back to its arrays/palettes and normalizes unknown saved IDs to `average`, `short`, `realm_basic`, `none`, `sword`, and `shield`.

### Boss and equipment IDs

- Optional generator IDs are the ten boss and six equipment IDs in the exact-value tables above. No generated asset is committed and no runtime registry indexes them.
- `BossDummyAI` consumes an optionally serialized `BossDefinition`; otherwise it retains `boss_dummy`. It only copies definition `Id`, name, positive health, and nonempty loot; it does not copy attack, armor, or abilities.
- `LocalBossLootService` consumes caller-provided `EquipmentDefinition` objects directly. Blank item IDs/names fall back to the Unity object name, drop rates are silently clamped, and an empty table creates `ember_crown_shard`.
- Persisted lookup/reference strings are `OwnedEquipmentState.EquipmentId` and `SourceBossId`; duplicate equipment is merged by exact `EquipmentId` string.

### Chapter, quest, dialogue, and NVS IDs

- Generated chapter IDs are the 29 IDs in the table above; none has a runtime query. World-atlas homeland scene hints refer only to `C3_SH`, `C3_EG`, `C3_CL`, and `C3_UM`.
- `SaveGameData.CurrentChapterId` defaults to `C1` for a new or normalized blank save, even though no generated chapter has exact ID `C1`.
- `LocalQuestService` definition and save lookup IDs are `Q1`, `Q2`, `Q3`, `Q4`, and `Q5`. `SideQuestService` accepts any exact string and classifies any prefix `SQ_`; it does not consult its empty `_definitions` dictionary.
- `LocalStoryService` hard-coded dialogue IDs are `intro_stonehold`, `hint_stonehold_war`, `intro_eldergrove`, `hint_eldergrove_blight`, `intro_crownlands`, `hint_crownlands_trade`, `intro_umbral`, `hint_umbral_revenge`, `c10_intro`, and `c12_victory`; every choice target is `end`. World atlas uses the four `hint_*` IDs above as scene/narrative keys.

Strict NVS-01 lookup identity is fully frozen as follows:

- Catalog identity: `al.narrative.nvs01`; schema `1`; packet `omen1-a1-2026-07-29-v003`; milestone `NVS-01`; quest `OMEN_1`; speaker `NPC_VALERIUS`.
- States: `OFFERED`, `TALK_TO_VALERIUS`, `INVESTIGATE_SKY_CASTLE`, `FAILED`, `REPORT_TO_VALERIUS`, `COMPLETED`.
- Objectives: `OBJ_OMEN_1_TALK`, `OBJ_OMEN_1_ARENA`, `OBJ_OMEN_1_REPORT`.
- Dialogue: `DLG_OMEN_1_OFFER`, `DLG_OMEN_1_START`, `DLG_OMEN_1_LORE`, `DLG_OMEN_1_GO`, `DLG_OMEN_1_ARENA_START`, `DLG_OMEN_1_FAILURE`, `DLG_OMEN_1_REPORT`, `DLG_OMEN_1_REPORT_CONCLUSION`.
- Transition events: `QUEST_ACCEPTED`, `REQUEST_SKY_CASTLE_ARENA`, `EVENT_SKY_CASTLE_ARENA_FAILURE`, `RETRY_SKY_CASTLE_ARENA`, `EVENT_SKY_CASTLE_ARENA_CANCELLED`, `EVENT_SKY_CASTLE_ARENA_SUCCESS`, `SELECT_VALERIUS`, `DLG_OMEN_1_REPORT_CONCLUSION`.
- External capabilities: `LOCATION_SKY_CASTLE_MARKER`, `ACTION_DEPLOY_CHAMPION`, `HOOK_SKY_CASTLE_ARENA`, `EVENT_SKY_CASTLE_ARENA_SUCCESS`, `EVENT_SKY_CASTLE_ARENA_FAILURE`, `EVENT_SKY_CASTLE_ARENA_CANCELLED`, `EVENT_SKY_CASTLE_ARENA_UNAVAILABLE`, `ARTIFACT_CELESTIAL_TEAR`, `CH1_REALM_INTRO`, `KINGDOM_COMMAND_VIEW`.
- Consequence IDs: `ACQUIRE_CELESTIAL_TEAR`, `GRANT_GOLD_500`, `GRANT_VALERIUS_AFFINITY_5`, `COMPLETE_OMEN_1`, `UNLOCK_REALM_CHAPTER_1`; targets are `ARTIFACT_CELESTIAL_TEAR`, `RESOURCE_GOLD`, `NPC_VALERIUS`, `OMEN_1`, `CH1_REALM_INTRO`.
- Localization keys: `quest.omen1.title`, `quest.omen1.description`, `npc.valerius.name`, `npc.valerius.role.veil_watch_liaison`, `objective.omen1.talk`, `objective.omen1.arena`, `objective.omen1.report`, `dialogue.omen1.offer`, `dialogue.omen1.start`, `dialogue.omen1.lore`, `dialogue.omen1.go`, `dialogue.omen1.arena_start`, `dialogue.omen1.failure`, `dialogue.omen1.report`, `dialogue.omen1.report_conclusion`, `choice.omen1.accept`, `choice.omen1.decline`, `choice.omen1.investigate`, `choice.omen1.ask_more`, `choice.omen1.depart`, `choice.omen1.deploy`, `choice.omen1.retry`, `choice.omen1.present_tear`, `choice.omen1.continue`, `artifact.celestial_tear.name`, `artifact.celestial_tear.lore`, `reward.omen1.gold`, `reward.omen1.valerius_affinity`.

The older Kotlin `NVS_01_Packet` has a different dialogue/state/reward shape, including `REW_OMEN_1_TEAR` and `DLG_OMEN_1_SUCCESS`; it is not an alias table and cannot be silently merged with the canonical Unity artifact.

### World-atlas IDs

`LocalWorldAtlasService` constructs nine zones and thirteen objectives. The dictionaries use exact strings and return mutable objects/collections.

| Zone ID | Home realm | Safety layer | Scene/narrative key | Objective IDs |
| --- | --- | --- | --- | --- |
| `stonehold_inner` | `Stonehold` | `InnerRealm` | `C3_SH` | `Stonehold_Heart_Gem`, `Stonehold_Fortress_Gem` |
| `eldergrove_inner` | `Eldergrove` | `InnerRealm` | `C3_EG` | `Eldergrove_Heart_Gem`, `Eldergrove_Glade_Gem` |
| `crownlands_inner` | `Crownlands` | `InnerRealm` | `C3_CL` | `Crownlands_Heart_Gem`, `Crownlands_Capital_Gem` |
| `umbral_inner` | `Umbral` | `InnerRealm` | `C3_UM` | `Umbral_Heart_Gem`, `Umbral_Void_Gem` |
| `neutral_borderlands` | `None` | `WarZone` | `T5` | `neutral_borderlands_objective` |
| `iron_pass` | `Stonehold` | `WarZone` | `hint_crownlands_trade` | `iron_pass_objective` |
| `worldroot_border` | `Eldergrove` | `WarZone` | `hint_eldergrove_blight` | `worldroot_border_objective` |
| `sovereign_road` | `Crownlands` | `WarZone` | `hint_stonehold_war` | `sovereign_road_objective` |
| `ashen_rift` | `Umbral` | `WarZone` | `hint_umbral_revenge` | `ashen_rift_objective` |

The only external Unity consumer, `WorldObjectiveMarkerSpawner`, calls `GetObjectivesForRealm(viewerRealm)`, sorts by `PassiveCreditWeight`, and creates up to eight markers; it does not look up a zone ID. Android `WorldAtlasHooks` separately uses `SKY_CASTLE` and `SILVER_WOODS`, which do not occur in the Unity atlas.

## Saved-state reference and migration inventory

| Persisted field/type | Definition/catalog relationship | Current behavior |
| --- | --- | --- |
| `SaveGameData.SelectedRealm : RealmId` | Realm enum/definition | Numeric enum is serialized; no explicit schema version or unknown-enum migration |
| `BuildingState.BuildingId : string` | Building definition ID | Any string can be created by a read; exact comparison; no definition validation |
| `TroopInventoryData.Type : TroopType` | Troop enum/definition | Any serialized enum number can enter the model; no catalog record check |
| `ResearchState.ResearchId : string` | Research definition ID | Display-string identities are preserved; reads create missing rows |
| `QuestState.QuestId : string` | Quest definition ID | Blank/null/duplicate rows are sanitized; unknown nonblank IDs survive reload but are filtered from known main quests |
| `SaveGameData.CurrentChapterId : string` | Chapter definition ID | Blank becomes `C1`; `C1` has no exact generated chapter record |
| Six `ChampionCustomizationState.*Id` strings | Customization option IDs | Defaults are `average`, `short`, `realm_basic`, `none`, `sword`, `shield`; controller normalizes unknown IDs destructively in memory |
| `OwnedEquipmentState.EquipmentId`, `SourceBossId` | Equipment/boss IDs | Exact string merge; fallback IDs may be persisted |
| `WarmasterState.EquippedSetId`, `UnlockedSetIds`, `PurchasedPieceIds` | Warmaster set/piece IDs | Null lists are normalized; there is no catalog referential validation |
| `TerritoryData.Id`, `OwnerRealm` | Territory/world/realm identity | Constructor fallback data and enum state, not common catalog validated |
| `RealmGemState.GemId`, `HomeRealm`, `CarrierId` | Gem/realm/carrier identity | No definition registry check |
| `WishgateState.LastRewardId` | Reward identity | No catalog check |
| `NpcAffinityData.NpcId` | NPC identity | No catalog check |
| `FactionRepData.FactionId` | Faction identity | No catalog check |

`SaveGameData` contains no save schema/version field. `LocalSaveGameService` supplies backward-compatible object/list defaults, removes null list entries, restores missing resources, and sets blank chapters to `C1`, but it has no definition-version migration or alias table. Definition-ID migrations remain under #137 and must preserve unknown/future data until a reviewed policy says otherwise. Phase B does not edit saves or rename persisted rows.

NVS-01 snapshots carry packet version/hash in their independent runtime contract, but no NVS snapshot field is integrated into `SaveGameData` at the inventory commit.

## Generated and imported asset provenance

### Editor-generated prototype art committed at the inventory commit

`ALDesignAssetGenerator` (`Another Life > Generate Design Assets`) is the generating code. Design intent is retained in `ModularChampionCustomization.md` and `SkillEffectsAndWeather.md`. These are prototype assets, not data-catalog authority. Each listed asset has a committed Unity `.meta` companion.

- `unity/Assets/AL/Art/Generated/Materials/MAT_Champion_Hair_Dark.mat`
- `unity/Assets/AL/Art/Generated/Materials/MAT_Champion_Skin_Neutral.mat`
- `unity/Assets/AL/Art/Generated/Materials/MAT_Crownlands_RoyalBlue.mat`
- `unity/Assets/AL/Art/Generated/Materials/MAT_Eldergrove_LeafGold.mat`
- `unity/Assets/AL/Art/Generated/Materials/MAT_Stonehold_DarkIron.mat`
- `unity/Assets/AL/Art/Generated/Materials/MAT_Stonehold_ForgeGlow.mat`
- `unity/Assets/AL/Art/Generated/Materials/MAT_Umbral_Obsidian.mat`
- `unity/Assets/AL/Art/Generated/Prefabs/Characters/AL_ModularChampion_Base.prefab`
- `unity/Assets/AL/Art/Generated/Prefabs/VFX/VFX_Crownlands_RoyalStrike.prefab`
- `unity/Assets/AL/Art/Generated/Prefabs/VFX/VFX_Eldergrove_HealingBloom.prefab`
- `unity/Assets/AL/Art/Generated/Prefabs/VFX/VFX_Stonehold_ForgeBurst.prefab`
- `unity/Assets/AL/Art/Generated/Prefabs/VFX/VFX_Umbral_CurseMark.prefab`
- `unity/Assets/AL/Art/Generated/Prefabs/Weather/Weather_CrownlandsClearStorm.prefab`
- `unity/Assets/AL/Art/Generated/Prefabs/Weather/Weather_EldergroveSunrain.prefab`
- `unity/Assets/AL/Art/Generated/Prefabs/Weather/Weather_MountainSnowWind.prefab`
- `unity/Assets/AL/Art/Generated/Prefabs/Weather/Weather_UmbralAshfall.prefab`

`ProjectInitializer` is a second Editor generator. Its expected ScriptableObject paths and exact values are inventoried above, but none of its `.asset` outputs is committed. It is a shared-lock file and is not changed in Phase B.

### Terrestrial source-only imports

The source manifest is `unity/Docs/Terrestrials/terrestrial_profiles_manifest.json`, schema 1/source `tdf-2026-07-15-v001`, SHA-256 `3b3eb700c411c60438f64bd5b10163c3bfec92b6c4dccfa09268dd44d5f03c16`. `Source_Prompts_And_Provenance.md` records Codex built-in image generation on 2026-07-15 with no external inputs. The assets are pending user creative approval and have no runtime authority:

| Profile ID | Source preview asset | SHA-256 |
| --- | --- | --- |
| `tdf_basalt_grazer` | `unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_basalt_grazer_concept_sheet_v001.png` | `2e14484df86f685f16b0cf00db9de85bb132651b0f83354dabea0b451bfdc354` |
| `tdf_grove_strider` | `unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_grove_strider_concept_sheet_v001.png` | `4e7864cc02c571357ad3faf8c77631bd9fa1c08944d18e89ad36a6b9dac89920` |
| `tdf_mire_lumenback` | `unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_mire_lumenback_concept_sheet_v001.png` | `39154a3ea94394efabac558e704a54a630e005371123b32ec9dd3803ec2235b0` |

### NVS generated artifact

`ExportNvs01Catalog.cs` reads `Docs/Narrative/NVS_01/OMEN_1_A1.packet.json`, canonicalizes and validates it, then writes `Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json`. EditMode tests prove source-to-artifact byte derivation, idempotent export, canonical identity, one correct artifact path, strict validation, immutable queries, and loader failure behavior.

The two JSON files under `Assets/AL/StreamingAssets/GameData` have no repository generator or import provenance that deterministically maps from their JSON Schemas or Fable records. Their JSON, schema, Fable, C# DTO, and hard-coded fallback shapes can drift independently.

## Validator and test coverage freeze

| Area | Present coverage | Missing coverage relevant to #183 |
| --- | --- | --- |
| QuestDefinition type/assets | `QuestDefinitionAssetAuthorityValidator` and `QuestDefinitionAssetAuthorityTests` enforce one production type, GUID, exact field schema, menu path, Force Text, YAML candidate parsing, duplicate IDs, removed GUIDs, and round trip | No committed quest `.asset`; does not validate LocalQuestService hard-coded content as a catalog |
| Quest save compatibility | `QuestSaveCompatibilityTests` covers null/unknown/malformed quest rows and known quest progression/claim | No catalog-version/alias migration |
| NVS-01 | Catalog/runtime tests cover canonical bytes/hash/version, strict JSON/schema, references/state graph, immutability, loader result/lifetime, runtime transactions, and adapter outcomes. Presenter/scene-wiring tests cover packet-owned copy, committed-realm adaptation, production Kingdom actions, and fail-closed Champion capabilities. | Still a quest-specific contract, not the common #183 manifest/family catalog; scene progress remains transient until C3 persistence and the Champion encounter owner are wired |
| Realm/resource smoke | `RuntimeContractSmokeTests` covers rare-resource mappings for playable realms | Does not validate realm definitions or reject `None` fallback |
| Service construction | `BootloaderServiceStackIntegrityTests` covers service-stack construction/failure containment | Does not validate family records or common catalog readiness |
| Customization/skill JSON | No direct catalog schema/version/hash/reference tests found | Schemas are not executed; DTO/schema/Fable/JSON drift and partial fallback are unguarded |
| Realm/building/research/troop/champion/skill/boss/equipment/world | No common definition/catalog validator or immutable query tests found | Phase B supplies only the standalone common foundation/tests; family content and production switch remain later phases |

## Ambiguities and blocked decisions retained by this inventory

1. Realm copy and perk values are embedded in a technical runtime class and differ from `ProjectInitializer` and Android `RealmHooks`; engineering must not choose replacement prose.
2. Building IDs are PascalCase in Unity and lower snake case in Android. `ManaShrine` and `Mine` have consumers but no definition. No implicit alias is approved.
3. Research uses display strings in Unity/save and lower snake case in Android. A reviewed alias/migration table is required before a switch.
4. There is no current troop or champion record authority and no valid basis for inventing required production records.
5. Skill behavior is slot-indexed while IDs, presentation keys, effects, and weather share a partially consumed JSON. A partial overlay is currently accepted; the common contract must fail it rather than report mixed data as valid.
6. Boss/equipment initializer values are uncommitted previews. Runtime dummy/fallback loot is not production catalog evidence.
7. `CurrentChapterId = C1` does not resolve to any of the 29 generated chapter IDs. Generated chapters also drop their realm argument and all are discarded.
8. Q1–Q5 Unity and Android values conflict. The strict canonical NVS artifact must not be merged with the older Kotlin NVS packet by inference.
9. World atlas and weather always construct fallback data and return mutable objects. Their presence cannot be cited as packaged-catalog availability.
10. Terrestrial IDs, labels, images, biome tags, and variants are source-review intent only and require the declared approval/handoff before runtime integration.
11. Save data has no schema version and contains definition-reference strings/enums across many families. Phase B cannot rename, normalize, or delete them.
12. No common catalog manifest, per-family envelope, raw hash, provenance record, typed readiness/query result, alias diagnostic, immutable whole-set snapshot, or atomic reload publication exists at the inventory commit.

This inventory is the Phase B baseline. Later shadow validation must compare proposed catalogs against these exact effective IDs/values, report every intentional difference or alias, and keep production authority unchanged until the owning family migration and save/consumer gates are accepted.
