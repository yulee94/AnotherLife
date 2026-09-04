# Unschematized Game-Data Catalog Inventory

**Status date:** 2026-08-18
**Tracking issue:** #183 (catalog authority — "Schematize the 10 unschematized catalogs")
**Inventory source commit:** `dffc7bb8` (`wt/t_09c23756` worktree off `main`)
**Scope:** Complete inventory of the three parallel game-data systems (hardcoded
`LocalGameDataService`, Definitions ScriptableObjects, and test-only Catalogs), with a
full field-level inventory of the ten unschematized `StreamingAssets/GameData` catalogs,
their consumers, and the duplicate/conflict surface.

This document changes no runtime authority. It is the observational baseline for the
schematization follow-up. It supersedes and extends the Phase B
`unity/Docs/Game_Data_Source_Inventory.md` for the catalog-set layer.

---

## 0. Honest coverage metric

| Artifact | Read this session | Notes |
| --- | --- | --- |
| 10 unschematized `GameData/*.json` | 10/10 in full | field-level inventory below |
| 2 schematized `GameData/*.json` | structural only | described via `Game_Data_Source_Inventory.md` + `SharedContracts/README.md` |
| `LocalGameDataService` hardcoded data | via `Game_Data_Source_Inventory.md` (read in full) | exact-value freeze reproduced in §4 |
| Definitions ScriptableObjects (17 types) | via `Game_Data_Source_Inventory.md` GUID table + `find` listing | no committed `.asset` files |
| Runtime loaders `RealmCatalogRuntime`, `WorldAtlasTopologyLoader`, `NotificationContentCatalogResolver` | 3/3 read (resolver ~500/821 lines) | consumer wiring confirmed |
| Six-family schema registry `GameDataSixFamilySchemas.cs` | read (~500/594 lines) | no records, not wired |
| Generic catalog foundation (`GameDataCatalogModels/Sources/Schema/Store/Validator`) | partial | foundation only; no production content wired |

**Not read in full this session (flagged, not hidden):** the remaining ~320 lines of
`NotificationContentCatalogResolver.cs`, `GameDataCatalogSchema.cs`, `GameDataCatalogStore.cs`,
`GameDataCatalogValidator.cs`, `GameDataCatalogLoading.cs`, and the full contents of the
17 Definition `.cs` files. The Definition field schemas were not re-derived line-by-line;
the `Game_Data_Source_Inventory.md` GUID/`CreateAssetMenu` table is authoritative for those.

---

## 1. The three parallel data systems

| System | What it is | Runtime authority? | Schematized? |
| --- | --- | --- | --- |
| **1. `LocalGameDataService`** | Hardcoded C# that constructs mutable `ScriptableObject` definitions in its constructor (`InitializeFallbackData`, `InitializeAutomatedContent`, `InitializeStoryData`) | Legacy effective source for realms/buildings; `GetTroop`/`GetChampion`/`GetSkill` always return `null` | No |
| **2. Definitions ScriptableObjects** | 17 `CreateAssetMenu` `ScriptableObject` types (`BossDefinition`, `BuildingDefinition`, `RealmDefinition`, `SkillDefinition`, `TroopDefinition`, `ChampionDefinition`, `ClassDefinition`, `EquipmentDefinition`, `WarmasterSetDefinition`, + narrative `Artifact/Chapter/Faction/Gem/NPC/Quest/SideQuest/SkillSoulQuest`) | No committed `.asset` files; only folder `.meta` files plus one unrelated `KingdomBuildingModelCatalog.asset` | No |
| **3. Test-only Catalogs** | Six-family schema registry, Phase C shadow artifacts, in-memory test fixtures, SHA-256 test vectors | Test/EditMode only; not registered with any runtime service | Partially (six-family schema registry exists) |

Plus the catalog-set layer itself, which this inventory covers in detail:

| Layer | Files | Schematized |
| --- | --- | --- |
| **GameData JSON catalogs** | 12 files under `unity/Assets/AL/StreamingAssets/GameData/` | 2 schematized, **10 unschematized** |
| NVS-01 canonical artifact | `unity/Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json` | Yes (byte-identical to authored source, strict validator) |

---

## 2. Catalog-set overview (12 GameData catalogs)

The `GameData/` directory contains exactly 12 runtime JSON catalogs. Two have JSON Schemas
in `unity/SharedContracts/Schemas/`; **ten do not**.

### Schematized (2)

| Catalog | Version | Schema | F# record |
| --- | --- | --- | --- |
| `al_character_customization_catalog.json` | `0.5.0` | `al-character-customization.schema.json` | `CharacterCustomizationCatalog` |
| `al_skill_weather_catalog.json` | `0.3.0` | `al-skill-weather.schema.json` | `SkillWeatherCatalog` |

### Unschematized (10) — the subject of this inventory

| # | Catalog file | `catalogId` | Version field | Runtime consumer? |
| --- | --- | --- | --- | --- |
| 1 | `al_character_customization_content_catalog.json` | `al_character_customization_content_catalog` | `version: 0.1.0` | none |
| 2 | `al_notification_content_catalog.json` | `al_notification_content_catalog` | `version: 0.1.0` | strict resolver, not file-wired |
| 3 | `al_notification_production_catalog.json` | `al_notification_production_catalog` | `schemaVersion: 1` | none |
| 4 | `al_quest_preview_content_catalog.json` | `al_quest_preview_content_catalog` | `version: 0.1.0` | none |
| 5 | `al_realm_catalog.json` | `al_realm_catalog` | `version: 0.1.0` | **auto-loaded at runtime** |
| 6 | `al_realm_gem_wishgate_content_catalog.json` | `al_realm_gem_wishgate_content_catalog` | `version: 0.1.0` | none |
| 7 | `al_relationship_authority_content_catalog.json` | `al_relationship_authority_content_catalog` | `version: 0.1.0` | none |
| 8 | `al_warmaster_content_catalog.json` | `al_warmaster_content_catalog` | `version: 0.1.0` | none |
| 9 | `al_world_atlas_narrative_catalog.json` | `al_world_atlas_narrative_catalog` | `version: 0.2.0` | strict validator, not file-wired |
| 10 | `al_world_event_content_catalog.json` | `al_world_event_content_catalog` | `version: 0.1.0` | ID-format only |

### Common envelope pattern

Nine of the ten unschematized catalogs share the same root envelope (the exception is
`al_notification_production_catalog.json`, which uses `schemaVersion` instead of `version`):

```
version            string   (semver; all 0.1.0 except world atlas 0.2.0)
catalogId          string   (matches filename)
game               string   ("Another Life")
sourcePacketId     string   ("al_narrative_*_source_v00N")
idFormat           string   ("lowercase_snake_case" or a family-specific variant)
sourceAuthorities  object   (primaryMode, consumerIssue, cross-catalog refs)
contentPolicy / authority / policy  object   (family-specific guardrails + nonGoals)
<family payload>   arrays/objects   (family-specific — see §3)
draftLocalization  array    ([{key, text}] — draft player-facing copy)
engineeringHandoff object   (consumerIssue, requiredValidation[], blockedRuntimeClaims[])
```

`al_notification_production_catalog.json` is structurally distinct: it is a *production
registry* whose `source` block points back at `al_notification_content_catalog.json` with a
pinned `byteLength` and `sha256`, plus `requiredDefinitionIds`, `entries`, `blockedRequirements`,
and `resolutionPolicy`.

---

## 3. Field-level inventory of the 10 unschematized catalogs

### 3.1 `al_character_customization_content_catalog.json`

- **Version / packet:** `0.1.0` / `al_narrative_character_customization_labels_v001`
- **Owner:** `codex_narrative_content` (consumer issue #184)
- **Purpose:** Player-facing labels for customization options and forge presets. It is the
  *content* twin of the schematized *technical* catalog
  `al_character_customization_catalog` (`0.5.0`), which it references via
  `authority.technicalCatalogId` / `technicalCatalogVersion`.
- **Runtime consumer:** none. No C# file references this filename or `catalogId`.
- **Fields / types / examples:**
  - `authority` object: `primaryMode`, `technicalCatalogId`, `technicalCatalogVersion` (`0.5.0`), `contentScope[]`, `nonGoals[]`.
  - `localizationPolicy` object: `keyPrefix` (`customization`), `missingKeyBehavior` (`technical_unavailable_status`), `releaseCopyApproval`, `internalIdExposure` (`debug_only`).
  - `families[]`: `{id, displayNameKey, optionKeys[]: {id, displayNameKey}}`. Six families:
    `body_presets` (9 options: `average`, `slim`, `broad`, `tall`, `stout`, `duelist`, `statuesque`, `massive`, `compact`),
    `hair_styles` (5: `short`, `long`, `braid`, `mohawk`, `topknot`),
    `armor_styles` (6: `realm_basic`, `light_scout`, `heavy_plate`, `warmaster_plate`, `arcane_robes`, `assassin_leathers`),
    `face_marks` (9: `none`, `scar`, `warpaint`, `realm_mark`, `rune`, `tattoo`, `beard`, `duelist_scar`, `ash_mask`),
    `weapon_styles` (5: `sword`, `axe`, `staff`, `bow`, `hammer`),
    `offhand_styles` (5: `shield`, `orb`, `dagger`, `tome`, `none`).
  - `forgePresets[]`: `{id, displayNameKey, summaryKey, identityMeaning}`. Nine presets:
    `vanguard`, `arcanist`, `nightblade`, `dreadknight`, `oracle`, `duelist`, `inquisitor`, `warden`, `spellblade`.
  - `draftLocalization[]`: `{key, text}` — 49 entries.
  - `engineeringHandoff`: `consumerIssue: 184`, `requiredValidation[]`, `contentGuardrails[]`.

### 3.2 `al_notification_content_catalog.json`

- **Version / packet:** `0.1.0` / `al_narrative_notification_content_source_v001`
- **Owner:** `codex_narrative_content` (consumer issue #177; catalog foundation #183)
- **Purpose:** Authoritative source for notification definitions (sources, actions, 11
  definitions, draft copy).
- **Runtime consumer:** `NotificationContentCatalogResolver` (strict parser, pinned byte
  length `11526` + SHA-256 `3c32ba4f…c1c3`). It is **not** wired to read the file at
  runtime — it receives `byte[]` via constructor and awaits a loader. Tested by
  `NotificationContentCatalogResolverTests`.
- **Fields / types / examples:**
  - `sourceAuthorities`: `primaryMode`, `consumerIssue` (177), `technicalSpec` (`Notification_Delivery_Contract_Spec.md`), `catalogFoundationIssue` (183).
  - `contentPolicy`: `releaseCopyApproval`, `rawStringMethods` (`temporary_compatibility_only`), `missingLocalizationBehavior`, `internalIdExposure`, `privacyRules[]`, `nonGoals[]`.
  - `sources[]`: `{id, displayNameKey}` — 6 (`al_source_save`, `al_source_boss_loot`, `al_source_world_state`, `al_source_nvs`, `al_source_bridge`, `al_source_catalog`).
  - `actions[]`: `{id, labelKey, kind}` — 3 (`acknowledge`, `retry_operation`, `open_recovery_details`).
  - `definitions[]`: `{id, sourceId, severity, category, channel, titleKey, bodyKey, parameterNames[], requiresCorrelation(bool), requiresAcknowledgement(bool), durability}` — 11 rows, e.g. `al_notify_save_recovered_backup` (severity `warning`, channel `acknowledgement`, durability `future_durable_outbox`).
  - `draftLocalization[]`: 31 entries.
  - `engineeringHandoff`: `consumerIssue: 177`, `requiredValidation[]`, `blockedRuntimeClaims[]`.

### 3.3 `al_notification_production_catalog.json`

- **Version / authority:** `schemaVersion: 1` / `authority: production_notification_source_of_truth`
- **Owner:** production registry for the notification family (issues #177, #137, #450, #168, #169, #172, #176, #183).
- **Runtime consumer:** none. No C# references this file or its `catalogId`.
- **Fields / types / examples:**
  - `source` object: pins `al_notification_content_catalog` (`version 0.1.0`, `sourcePacketId`, `byteLength: 11526`, `sha256: 3c32ba4f…c1c3`).
  - `requiredDefinitionIds[]`: 11 IDs (matches content catalog definitions exactly).
  - `entries[]`: `{id, sourceId, consumers[], availability}` — 11 rows, `availability: available`.
  - `blockedRequirements[]`: `{requirementId, consumers[], dependencies[], reason, requiredBeforeActivation(bool)}` — 6 requirements, all `requiredBeforeActivation: true`.
  - `resolutionPolicy` object: `unknownId/missingSource/sourceIdentityMismatch/blockedRequirement = explicit_failure`, `fallbackContent: prohibited`.

### 3.4 `al_quest_preview_content_catalog.json`

- **Version / packet:** `0.1.0` / `al_narrative_quest_preview_source_v001`
- **Owner:** `codex_narrative_content` (consumer issue #186; route boundary #133; notifications #177).
- **Purpose:** Read-only quest-preview content for `OMEN_1` (+ legacy `OMEN_2` / Android Q1–Q4 rows as `not_approved_source`).
- **Runtime consumer:** none. References (not loads) the NVS catalog
  `unity/Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json` and the world-atlas catalog.
- **Fields / types / examples:**
  - `sourceAuthorities`: `primaryMode`, `consumerIssue` (186), `nvsCatalog`, `nvsPacketVersion` (`omen1-a1-2026-07-29-v003`), `worldAtlasCatalog` (`al_world_atlas_narrative_catalog`), `routeBoundaryIssue` (133), `notificationIssue` (177).
  - `previewPolicy`: `releaseRole` (`unavailable_until_engineering_contract`), `approvedDebugRole`, `authoritativeProgressSource`, `internalIdsPlayerFacingPolicy` (`debug_only`), `nonGoals[]`.
  - `actions[]`: `{id, displayNameKey, sourceSemanticAction?, requiredCapability?, requiredState?, status, mutatesAuthoritativeState}` — 4 (`action_preview_read`, `action_deploy_champion`, `action_retry_sky_castle_arena`, `action_present_celestial_tear`).
  - `prohibitedReleaseActions[]`: `{id, reason}` — 3.
  - `locationMarkers[]`: `{id, legacyMarkerId, displayNameKey, summaryKey, worldAtlasZoneId, status}` — 1 (`location_sky_castle_marker` → `SKY_CASTLE`).
  - `questPreviews[]`: `{id, questId, sourceVersion, previewRole, releaseAvailability, titleKey, descriptionKey, speakerId, locationMarkerId, progressModel{kind, validStates[], rawIntegerProgressAllowed, androidProgressBarAuthority}, displayObjectives[], rewardSummaryKeys[], rewardTiming[], availableActions[]}` — 3 rows (1 approved `quest_preview_omen_1`, 2 legacy `not_approved_source`).
  - `statusCopy[]`, `draftLocalization[]` (21 entries), `engineeringHandoff` (`consumerIssue: 186`).

### 3.5 `al_realm_catalog.json` — **the only auto-loaded catalog**

- **Version / packet:** `0.1.0` / (`al_narrative_four_realm_launch_source_v001` under `narrativeContinuity`)
- **Owner:** `codex_narrative_content`; consumer `codex/unity-realm-hooks`; `parseOnLaunch: true`.
- **Runtime consumer (wired):** `RealmCatalogRuntime` (`RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`
  auto-loads via `UnityWebRequest`, parses with `JsonUtility`, publishes an immutable
  `RealmCatalogSnapshot`). Consumers of `RealmCatalogRuntime.Current`:
  `LocalRealmService.cs`, `RealmSelection/RealmSelectionController.cs`, `UI/BootController.cs`,
  `UI/LaunchReadinessContracts.cs`. Tests: `RealmCatalogAndSelectionTests`,
  `RealmSelectionIntegrityTests`, `ProductionPlayerBuilderTests`.
- **Fields / types / examples:**
  - `selectionPolicy`: `selectionMode` (`one_realm_per_account`), `realmLockScope` (`account`), `subCharacterPolicy` (`same_realm_only`), `sharedStoragePolicy`, `crossRealmCreationPolicy` (`reject`), `realmChangePolicy` (`not_supported_after_commit`), `uncommittedProfileState` (`realm_unselected`), `committedProfileState` (`realm_locked`), `narrativeWarningKey`.
  - `narrativeContinuity`: `sourcePacketId`, `sourceMode`, `accountLockSummary`, `selectionWarningMeaning`, `uncommittedMeaning`, `committedMeaning`, `handoffStatus`.
  - `realmOrder[]`: `["crownlands","stonehold","eldergrove","umbral"]`.
  - `realms[]` (4): `{id (lowercase), legacyRuntimeId (PascalCase enum), displayName, peopleName, adjective, languageId, capitalId, innerRealmId, outerWarzoneId, mainGateId, dragonId, realmGemIds[2], sigil, palette{primary,secondary,accent}, namingConventions{characterTitles[],settlementTerms[],assetPrefix}, lore{summary,identityPillars[],playerPromise}, continuityHooks{...}, startingHooks{realmSelectionLineKey, firstQuestArcId, starterClassBias[]}}`.
    - Gems per realm: Crownlands `gem_crownlands_sun`/`gem_crownlands_oath`; Stonehold `gem_stonehold_forge`/`gem_stonehold_depth`; Eldergrove `gem_eldergrove_root`/`gem_eldergrove_moon`; Umbral `gem_umbral_veil`/`gem_umbral_ember` → **8 gems total, 2 per realm** (canon).
  - `localizationKeys[]`, `localizationDrafts[]`, `engineeringHandoff` (`parseOnLaunch: true`, `requiredValidation[]`, `nonGoalsForThisCatalog[]`).
- **Parser validation (`RealmCatalogRuntime.Parse`) enforces:** version/catalogId, selection-policy exact strings, exactly 4 realms + 4 realmOrder, unique lowercase IDs, unique `legacyRuntimeId` mapped back to the `RealmId` enum, exactly 2 stable `realmGemIds` per realm.

### 3.6 `al_realm_gem_wishgate_content_catalog.json`

- **Version / packet:** `0.1.0` / `al_narrative_realm_gem_wishgate_source_v001`
- **Owner:** `codex_narrative_content` (consumer issue #169).
- **Runtime consumer:** none.
- **Fields / types / examples:**
  - `sourceAuthorities`: `primaryMode`, `consumerIssue` (169), `realmCatalog` (`al_realm_catalog`), `worldAtlasCatalog`, `mainQuestPacket` (`ANOTHERLIFE_MAIN_QUEST_LINE`), `notificationCatalog`.
  - `contentPolicy`: `realmGemMeaning`, `custodyRule`, `wishgateRule`, `unavailableRuntimeBehavior`, `debugIdPolicy`, `nonGoals[]`.
  - `realmGems[]` (8): `{id, realmId, displayNameKey, summaryKey, custodyMeaningKey, signatureKey, status: source_ready_runtime_custody_unimplemented}`.
  - `custodyStates[]` (4): `{id, displayNameKey, summaryKey, runtimeMeaning}` — `custody_unseen`, `custody_contested`, `custody_witnessed`, `custody_restored`.
  - `wishgate` object: `{id: wishgate_eightfold_concordance, displayNameKey, summaryKey, entryZoneId: zone_accordant_isle, guardianDragonNameKey: wishgate.vaeloryn.name, eligibilitySource, defaultStatus, rewardPolicy, approvedWishEmphases[3], blockedRuntimeClaims[]}`.
  - `statusCopy[]`, `draftLocalization[]` (large), `engineeringHandoff` (`consumerIssue: 169`).

### 3.7 `al_relationship_authority_content_catalog.json`

- **Version / packet:** `0.1.0` / `al_narrative_relationship_authority_source_v001`
- **Owner:** `codex_narrative_content` (consumer issue #176; spec `Relationship_Integrity_Transaction_Spec.md`).
- **Runtime consumer:** none.
- **Fields / types / examples:**
  - `sourceAuthorities`: `primaryMode`, `consumerIssue` (176), `relationshipSpec`, `mainQuestPacket`, `omenPacket` (`OMEN_1_A1`), `legacyAndroidSources[]` (`AdvisorPersonas.kt`, `FactionProfiles.kt`, `NarrativeGovernance.md`).
  - `contentPolicy`: `identityRule`, `sparseDefaultRule` (default 0), `labelRule`, `mutationRule`, `unknownRule`, `nonGoals[]`.
  - `npcRecords[]` (6): `{id, legacyAliases[], relationshipEnabled(bool), initialAffinity(int, 0), legacyPreviewAffinity(int), classificationProfileId, displayNameKey, roleKey, relationshipContextKey}` — `npc_valerius`, `npc_gruff`, `npc_molly`, `npc_xerath`, `npc_vaeloryn` (disabled), `npc_edras_veyr` (disabled).
  - `factionRecords[]` (5): `{id, legacyAliases[], relationshipEnabled, initialReputation(int, 0), legacyPreviewReputation(int), classificationProfileId, parentRealmId, displayNameKey, summaryKey}`.
  - `affinityClassificationProfiles[]` (1): five-band `[-100,100]` with `{id, minimumInclusive, maximumInclusive/Exclusive, displayNameKey, summaryKey}`.
  - `factionClassificationProfiles[]` (1): five-band signed-Int32 with the same shape.
  - `personaPolicy`: four traits (`warlord`, `diplomat`, `sage`, `rogue`), five classification states, `allZeroPolicy`/`tiePolicy`/`missingPolicy`.
  - `approvedConsequences[]` (1): `{id, sourceQuestId: OMEN_1, sourceConsequenceId: GRANT_VALERIUS_AFFINITY_5, targetNpcId: npc_valerius, delta: 5, repeatability, status}`.
  - `draftLocalization[]` (large), `engineeringHandoff` (`consumerIssue: 176`).

### 3.8 `al_warmaster_content_catalog.json`

- **Version / packet:** `0.1.0` / `al_narrative_warmaster_content_source_v001`
- **Owner:** `codex_narrative_content` (consumer issue #171).
- **Runtime consumer:** none.
- **Fields / types / examples:**
  - `authority`: `primaryMode`, `consumerIssue` (171), `currentRuntimeSetId` (`prototype_true_warmaster`), `currentRuntimePiecePrefix` (`warmaster_piece_`), `contentScope[]`, `nonGoals[]`.
  - `warmasterPolicy`: `releaseCopyApproval`, `internalIdExposure`, `missingContentBehavior`, `purchaseAuthority`, `thresholdAuthority`, `setIdMigrationPolicy`, `meaningGuardrails[]`.
  - `sets[]` (1): `{id: prototype_true_warmaster, displayNameKey, summaryKey, status, pieceIds[10]}`.
  - `pieces[]` (10): `{id: warmaster_piece_01..10, setId, displayNameKey, summaryKey}`.
  - `statusCopy[]` (2), `draftLocalization[]`, `engineeringHandoff` (`consumerIssue: 171`).

### 3.9 `al_world_atlas_narrative_catalog.json`

- **Version / packet:** `0.3.0` / `al_narrative_world_atlas_source_v003`
- **Owner:** `codex_narrative_content` (technical consumer issue #181; topology contract `al_world_atlas_topology_query_contract_v001`, merge commit `a97d5e5c`).
- **Runtime consumer:** `WorldAtlasTopologyLoader.Validate(byte[])` — a strict, fully
  implemented validator (counts, cross-references, global unique IDs, boundary ordering,
  topology ring/bridge constraints). It is **not** wired to read the file at runtime; it
  awaits a byte source. Tested by `WorldAtlasTopologyTests`.
- **Fields / types / examples:**
  - `sourceAuthorities`: `primaryMode`, `realmCatalog`, `mainQuestPacket`, `nvsPacket` (`OMEN_1`), `topologyContract{id, path, mergeCommit}`, `protectedZoneContract{id, path, issue}`, `technicalConsumerIssue` (181).
  - `atlasPolicy`: `viewerRealmRule`, `crossRealmRule`, `unavailableRuntimeBehavior`, `queryAuthority`, `nonGoals[]`.
  - `abstractTopology`: `{topologyId, macroNarrativeZoneId, nodes[5] (ring_slot_01..04 + center_slot), adjacency[4], bridges[12], endpoints[24], placement{status: unresolved_user_gate, assignments[], compassOrientation: unresolved}}`.
  - `transitionZones[]` (4): `{id, realmId, mainGateId, zoneType, sceneReferenceStatus: requested, traversalStatus: requested, mutationAuthority}`.
  - `walls[]` (8): `{id, realmId, boundaryRole (inner_wall/outer_wall), geometryStatus: requested, mutationAuthority}`.
  - `boundaries[]` (4): `{id, realmId, innerRealmId, innerAtlasZoneId, innerWallId, transitionZoneId, mainGateId, outerWallId, outerWarzoneId, outerAtlasZoneId, orderedStages[5], hookStatus: requested, mutationAuthority}`.
  - `protectedZonePolicies[]` (3): immutable city/beginner/town `forced_non_pvp` policies with required effect-application recheck, blocked war override, `contract_only` enforcement, and no mutation authority.
  - `protectedSubzones[]` (12): one city, beginner, and town technical subzone per canonical realm, each referencing its existing inner atlas zone and exact policy ID; scene and boundary hooks remain requested.
  - `zones[]` (11): `{id, realmId, displayNameKey, summaryKey, zoneType, visibility, sceneReferenceStatus, relatedQuestMilestones[], pvpPolicy?}` — 4 inner + 4 warzone gates + `zone_crossroads_bridges` + `zone_accordant_isle` (forced_non_pvp) + `zone_sky_castle_marker`.
  - `objectives[]` (5): `{id, displayNameKey, summaryKey, requiredZoneTypes[]/requiredZoneIds[], hookStatus: requested, mutationAuthority}` — includes `objective_eight_gem_custody`.
  - `draftLocalization[]` (large), `engineeringHandoff` (`consumerIssue: 181`, `requiredValidation[]` pins the exact 5/4/12/24/4/8/4/11/5 counts).

### 3.10 `al_world_event_content_catalog.json`

- **Version / packet:** `0.1.0` / `al_narrative_world_event_source_v001`
- **Owner:** `codex_narrative_content` (consumer issue #172; notifications #177; relationships #176).
- **Runtime consumer:** none (only ID-format validation). `WorldStateValidation.cs` contains
  a regex `\Aal_world_event_[a-z][a-z0-9]*(?:_[a-z0-9]+)*\z` but does **not** load this catalog.
- **Fields / types / examples:**
  - `sourceAuthorities`: `primaryMode`, `consumerIssue` (172), `notificationIssue`, `relationshipIssue`, `worldAtlasCatalog`, `notificationCatalog`, `legacyRuntimeService` (`WorldStateService`).
  - `contentPolicy`: `eventCopyRule`, `technicalEffectRule`, `notificationRule`, `durationRule`, `nonGoals[]`.
  - `eventDefinitions[]` (4): `{id, legacyEffect, displayNameKey, summaryKey, startMessageKey, endMessageKey, unavailableMessageKey, notificationDefinitionId, narrativeIntent, technicalEffectClaims[], consumerStatus}` — `world_event_siege`, `world_event_festival`, `world_event_veil_omen`, `world_event_void_corruption`.
  - `lifecycleStatusCopy[]` (3), `draftLocalization[]`, `engineeringHandoff` (`consumerIssue: 172`, `optimizationImpact{}`).

---

## 4. `LocalGameDataService` hardcoded data (summary)

Reproduced from the Phase B `Game_Data_Source_Inventory.md` exact-value freeze. Full tables are
in that document; this is the family-level summary with counts and consumer status.

| Family | Count / IDs | Runtime exposure | Consumer note |
| --- | --- | --- | --- |
| Realms | 4 `RealmDefinition` (`Stonehold`=1, `Eldergrove`=2, `Crownlands`=3, `Umbral`=4) | `GetRealm`, `GetAllRealms` | `LocalRealmService`, `RealmSelectionController`; enum-coupled fallbacks in ~30 files |
| Buildings | 15 (`TownHall`, `Farm`, `LumberMill`, `Quarry`, `GoldMine`, `Barracks`, `Academy`, `Market`, `Storehouse`, `Forge`, `Stable`, `Workshop`, `Embassy`, `Wall`, `Watchtower`) | `GetBuilding` (unused by services) | `ManaShrine`, `Mine` referenced by consumers but have **no definition** |
| Research | 8 private `ResearchState` rows (display-string IDs `Steel Forging`, `Plate Armor`, `Advanced Masonry`, `Irrigation`, `Ballistics`, `Logistics`, `Trade Routes`, `Arcane Study`) | not queryable | `LocalResearchService` maps only `Steel Forging`→Attack, `Plate Armor`→Defense (`Level*0.05f`) |
| Troops | `TroopDefinition` type only; `TroopType` enum (`Infantry`,`Cavalry`,`Ranged`,`Siege`) drives behavior | `GetTroop` always `null` | `DeterministicBattleSimulator` hardcodes power 10/15/12/20 |
| Champions | `ChampionDefinition` type only | `GetChampion` always `null` | procedural Champion Mode, no committed records |
| Skills | `SkillDefinition` type + 4 hardcoded slots in `SkillCaster` (`realm_strike`, `renewing_guard`, `warzone_burst`, `warmaster_breaker`) | `GetSkill` always `null` | partially overlaid by `al_skill_weather_catalog.json` |
| Chapters | 29 `ChapterDefinition` objects (`C1..C12` × 4 realms + `C_OMEN`), all discarded | none | `SaveGameData.CurrentChapterId` defaults to `C1` (matches no generated ID) |
| Skill-soul quests | 16 `SkillSoulQuestDefinition` objects (`SQ_<Subclass>`), all discarded | none | — |
| Quests (prototype) | 5 `QuestDefinition` (`Q1`..`Q5`) in `LocalQuestService` | `LocalQuestService` | Android Q1–Q4 titles/targets differ; NVS-01 is separately strict/canonical |

---

## 5. Definitions ScriptableObjects (17 types)

No committed `.asset` files exist (only folder `.meta` files + one unrelated
`ScriptableObjects/Resources/KingdomBuildingModelCatalog.asset`). The 17 `CreateAssetMenu`
types and their GUIDs are frozen in `Game_Data_Source_Inventory.md` §"ScriptableObject
definition and GUID inventory". Family summary:

| Family | Types | Committed assets? |
| --- | --- | --- |
| Core definitions | `BossDefinition`, `BuildingDefinition`, `ChampionDefinition`, `ClassDefinition`, `EquipmentDefinition`, `RealmDefinition`, `SkillDefinition`, `TroopDefinition`, `WarmasterSetDefinition` | none |
| Narrative | `ArtifactDefinition`, `ChapterDefinition`, `FactionDefinition`, `GemDefinition`, `NPCDefinition`, `QuestDefinition`, `SideQuestDefinition`, `SkillSoulQuestDefinition` | none |

`QuestDefinition` is the only type with a dedicated authority validator
(`QuestDefinitionAssetAuthorityValidator`).

---

## 6. Test-only Catalogs

These catalogs exist only in EditMode test code / in-memory fixtures; none is registered
with a runtime service.

| Test-only catalog | Where | Shape |
| --- | --- | --- |
| Six-family schema registry | `Data/Catalogs/SixFamily/GameDataSixFamilySchemas.cs` | C# schema rules (`realms`, `buildings`, `research`, `troops`, `champions`, `skills`) with **no records**, not wired. Field rules: `legacy_realm_id/value`, `name_ref`, `description_ref`, `inner_realm_id`, `main_gate_id`, `outer_warzone_id`, `rare_resource_id`, `capability_profile_ids`, `asset_ref`, building `initial/max_level`, `cost/duration/prerequisite/realm_eligibility_profile_id`, troop `base_attack/base_defense/training_profile_id`, champion `realm_id/class_family_id/portrait/model/base_skill_ids/stat_profile_id`, skill `behavior/presentation_profile_id`, `target_type`, `cooldown_seconds`, `power`, `mana_cost`, `cast_time_seconds`, `range_meters`, `vfx/audio_asset_ref` |
| Realm shadow artifact | `GameDataCatalog/PhaseC/Shadow/realm-family-shadow-v001.json` (+ `.evidence.json`) | `catalogId: realms_phase_c9a_shadow_v1`, 4 realm records; consumed by `RealmShadowArtifactTests` |
| In-memory family fixtures | `GameDataCatalogFoundationTests` via `CatalogFixture` | e.g. `skills_v1` ("ember", alias "Old Ember"), `champions_v1` ("warden" with `skill_id` ref) |
| SHA-256 test vectors | `Tests/EditMode/Battle/TestVectors/battle_sha256_v1.json`, `Tests/EditMode/BossRewards/TestVectors/boss_reward_sha256_v1.json` | deterministic hashes, not game-data catalogs |
| Inline realm JSON | `RealmSelectionIntegrityTests.cs` | inline `al_realm_catalog` document for parser tests |
| NVS-01 catalog tests | `Nvs01CatalogTests.cs` | validate the canonical `OMEN_1.catalog.json` (separate strict family) |

---

## 7. Duplicate and conflicting definitions (flagged)

These are the conflicts the schematization follow-up must resolve explicitly; none is
silently merged here.

1. **Realm identity in three encodings.** `al_realm_catalog` uses lowercase snake_case IDs
   (`crownlands`…`umbral`), `LocalGameDataService` + `RealmId` enum use PascalCase
   (`Crownlands`…`Umbral`), and each realm row carries `legacyRuntimeId` to bridge them.
   `RealmCatalogRuntime.Parse` enforces the lowercase↔PascalCase mapping, but
   `LocalRealmService`/`RealmSelectionController`/`BootController` consume the enum path.
   The realm **copy** also differs across three sources (see `Game_Data_Source_Inventory.md`
   ambiguity #1: `LocalGameDataService` descriptions vs `ProjectInitializer` templates vs
   `al_realm_catalog` lore).
2. **8 gems = 2 per realm, but three sources define them.** `al_realm_catalog.realmGemIds`
   (8 IDs), `al_realm_gem_wishgate_content_catalog.realmGems` (8 rows, with a
   `requiredValidation` rule that the two must "exactly match"), and the world-atlas
   `objective_eight_gem_custody` + Wishgate "all eight signatures" eligibility. The wishgate
   catalog is the only place gem names/custody meaning are authored; the realm catalog is the
   only place the 2-per-realm pairing is authoritative. A schema must enforce the cross-catalog
   equality, not a prose rule.
3. **Customization in two catalogs + hardcoded arrays.** The schematized *technical*
   `al_character_customization_catalog` (`0.5.0`) holds option/preset IDs and palettes; the
   unschematized *content* `al_character_customization_content_catalog` (`0.1.0`) holds
   display labels referencing those IDs; `ChampionCustomizationController` separately
   hardcodes the same option/preset ID sets as fallback. The content catalog's
   `requiredValidation` demands its IDs "resolve in" the technical catalog `0.5.0`, but no
   test executes that cross-check.
4. **Notification content vs production catalog.** `al_notification_production_catalog`
   pins the content catalog's `byteLength` (`11526`) and `sha256` in its `source` block, and
   `NotificationContentCatalogResolver` re-pins the same hash/byte length. Any edit to the
   content catalog silently breaks the production registry and the resolver until all three
   are updated. There is no single source-of-truth pointer.
5. **World-event ↔ notification ID mismatch.** `al_world_event_content_catalog` rows use
   `notificationDefinitionId` values like `notification.world_event.siege`, but
   `al_notification_content_catalog` only defines `al_notify_world_event_started` /
   `al_notify_world_event_ended` (no `_cancelled`, no per-event IDs). The production catalog
   already records this as `blockedRequirements.notification_requirement_world_event_cancelled`
   ("world-state planner emits `al_notify_world_event_cancelled`, but the approved notification
   source contains only started and ended").
6. **Quest-preview duplicates OMEN_1 state.** `al_quest_preview_content_catalog` re-declares
   the six OMEN_1 states, objectives, rewards, and actions that are already canonical in the
   NVS `OMEN_1.catalog.json`. The preview catalog's `nvsPacketVersion` and
   `sourceSemanticAction` values (`REQUEST_SKY_CASTLE_ARENA`, `DLG_OMEN_1_REPORT_CONCLUSION`)
   must stay in lockstep with the NVS artifact; no validator enforces this.
7. **Warmaster set/piece IDs duplicated with runtime state.** `al_warmaster_content_catalog`
   pins `currentRuntimeSetId: prototype_true_warmaster` and `currentRuntimePiecePrefix:
   warmaster_piece_`, which must match `WarmasterState` (`EquippedSetId`, `UnlockedSetIds`,
   `PurchasedPieceIds`) — but the catalog's own `nonGoals` explicitly exclude save migration
   and credit mutation, so the runtime still carries its own identity model.
8. **Notification/relationship/quest previews all reference `OMEN_1`** via
   `mainQuestPacket: ANOTHERLIFE_MAIN_QUEST_LINE` / `omenPacket: OMEN_1_A1`, while the strict
   NVS packet identity is `omen1-a1-2026-07-29-v003`. Three different string tokens identify
   "the OMEN_1 source packet"; no single cross-reference authority unifies them.
9. **`version` vs `schemaVersion`.** Nine of ten unschematized catalogs use `version` (semver
   string); `al_notification_production_catalog` uses `schemaVersion` (integer `1`). The
   generic catalog foundation (`GameDataCatalogContract`) and `Game_Data_Catalog_Authority_Spec`
   mandate an integer `schemaVersion` + `contentVersion` envelope — so the nine `version`
   catalogs do not yet conform to the declared production envelope.

---

## 8. Where the canonical StreamingAssets/GameData JSON should live

Current physical layout (two StreamingAssets roots — an inconsistency to resolve):

| Path | Contents | Note |
| --- | --- | --- |
| `unity/Assets/AL/StreamingAssets/GameData/` | all 12 GameData catalogs | "AL-prefixed" root |
| `unity/Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json` | canonical NVS artifact | "non-AL" root, byte-identical to authored source |

Recommendations already in the repo (reproduced, not invented):

- `Game_Data_Catalog_Authority_Spec.md` §4.1 recommends
  `unity/Assets/StreamingAssets/GameData/catalog-set.json` (manifest) plus
  `unity/Assets/StreamingAssets/GameData/Catalogs/{realms,buildings,research,troops,champions,skills}.v1.json`
  for the six technical families.
- The two schematized JSON files and the NVS tests already reject an
  `Assets/AL/StreamingAssets` duplicate of the NVS artifact.

**Net:** the 12 GameData catalogs should converge on the **non-AL** `Assets/StreamingAssets`
root (matching the NVS artifact and the spec's recommended `catalog-set.json` path), with
`Assets/AL/StreamingAssets/GameData` deprecated/archived. The 10 unschematized catalogs are
all *content/narrative-source* families; their canonical home is
`unity/Assets/StreamingAssets/GameData/` under a manifest + per-family envelope
(`schemaVersion`, `contentVersion`, `sourceRevision`, `records[]`), with JSON Schemas added
under `unity/SharedContracts/Schemas/` to match the two already schematized catalogs.

---

## 9. Schematization gaps (what "schematize the 10" must add)

| # | Catalog | Has JSON Schema? | Has C# parser/validator? | Has runtime loader? |
| --- | --- | --- | --- | --- |
| 1 | customization content | ✗ | ✗ | ✗ |
| 2 | notification content | ✗ | ✓ (strict resolver, hash-pinned) | ✗ (awaiting byte source) |
| 3 | notification production | ✗ | ✗ | ✗ |
| 4 | quest preview | ✗ | ✗ | ✗ |
| 5 | realm | ✗ | ✓ (`JsonUtility` parser + policy checks) | ✓ (auto-loaded) |
| 6 | realm gem wishgate | ✗ | ✗ | ✗ |
| 7 | relationship authority | ✗ | ✗ | ✗ |
| 8 | warmaster | ✗ | ✗ | ✗ |
| 9 | world atlas narrative | ✗ | ✓ (strict validator) | ✗ (awaiting byte source) |
| 10 | world event | ✗ | ✗ (ID-format regex only) | ✗ |

Only **one** of the ten (`al_realm_catalog`) is read at runtime today. Three have C#
parsers/validators but no file-reader wiring; six are pure source-only JSON with no consumer.
The schematization follow-up must: (a) add JSON Schemas for all ten, (b) resolve the
cross-catalog reference equalities in §7 via machine-checked rules rather than prose, and
(c) decide which of the six source-only catalogs become runtime-wired vs remain authored
content awaiting their consumer issues (#184, #186, #169, #176, #171, #172).

---

## 10. Acceptance-criteria map

- **Every catalog covered** — §2 lists all 12 GameData catalogs; §3 field-level-inventories
  the 10 unschematized; §4–§6 cover `LocalGameDataService`, Definitions SOs, and test-only
  catalogs. No GameData catalog is missing.
- **Source paths + consumer lists** — each §3 entry records the source file, `catalogId`,
  `sourcePacketId`, owner/issue, and the exact runtime/test consumer paths.
- **Mismatches and duplicates called out** — §7 enumerates nine concrete conflicts.
- **Canonical location identified** — §8.

## 11. Ambiguities retained (do not resolve in this inventory)

1. Which six source-only content catalogs (#184/#186/#169/#176/#171/#172) become runtime-wired
   vs remain authored-source-only is a product decision, not an inventory decision.
2. The exact physical directory for the converged catalog set (`Assets/StreamingAssets/GameData`
   vs the current `Assets/AL/StreamingAssets/GameData`) is a migration decision pending
   approval; this inventory recommends but does not move files.
3. The `version` (semver string) vs `schemaVersion` (integer) envelope reconciliation for the
   nine `version`-style catalogs is a contract decision.
