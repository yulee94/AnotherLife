# Stonehold Content Authoring Audit — Troops, Champions, Research, Skills

Status: content-authoring audit for the Stonehold slice (kanban task `t_277fb06c`).
Purpose: locate every source file, schema, loader, and validator for the four content
families that currently have **zero authored production records**, document their field
requirements, ID conventions, balance constraints, dependencies, and validation rules,
and produce the exact map of which Stonehold records must be added and which source
documents justify each one.

This document changes no runtime authority, authors no content, and approves no balance.

---

## 0. Executive finding (read this first)

The four families — **troops, champions, research, skills** — are part of the canonical
"six-family" game-data model (`realms`, `buildings`, `research`, `troops`, `champions`,
`skills`). Their production disposition in the authoritative technical source is:

| Family | Mappings | Disposition | Query meaning | Runtime today |
| --- | ---: | --- | --- | --- |
| research | 8 | `blocked_required` | No production artifact | `ResearchState` display strings only |
| troops | 0 | `blocked_required` | `CatalogUnavailable` | `GetTroop()` always `null` |
| champions | 0 | `blocked_required` | `CatalogUnavailable` | `GetChampion()` always `null` |
| skills | 4 | `blocked_required` | No production artifact | `GetSkill()` always `null` |

The non-negotiable constraint from the source authority is: **records must be traceable to
existing design/lore material, and nothing may be fabricated.** After auditing every
canonical source, the honest result is:

- **Research** and **skills** have *verbatim-preserved* names (and research has canonical
  technical IDs) — so *name-only* records are authorable, but their mechanical fields
  (costs, durations, effects, behavior/presentation profiles, stats, prerequisites) have
  **no approved source** and must remain placeholders.
- **Troops** and **champions** have **zero** defensible content records. The only Stonehold
  champion material is an owner-approved *visual* precursor (the "Stonehold Vanguard"
  character sheet); there is **no** gameplay identity, name, class assignment, stat profile,
  or base-skill list. Troops have no Stonehold-specific source at all.

Consequence for the downstream authoring worker: the Stonehold "troops/champions" slice
must be authored as **explicit `not_authored_unavailable` placeholders**, not as invented
records. This is a feature of the canonical design, not a gap to paper over.

---

## 1. Content authority model

The single catalog-driven authority is the six-family model defined in
`unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataSixFamilySchemas.cs`. Its class
doc-comment is explicit: it "defines production record shape only; it contains no records,
performs no loading, and is not registered with a runtime service."

Disposition vocabulary (from `Phase_C_Six_Family_Source_Packet.md` §1):

| Disposition | Meaning |
| --- | --- |
| `verbatim_preserved` | Codex narrative/content accepts the reference and exact current string for no-drift encoding. Not final user approval. |
| `unresolved_user_decision` | A new creative/product decision is required before authoring. |
| `not_authored_unavailable` | No approved production record or player-facing source exists; absence must be represented honestly and never fabricated. |
| `out_of_scope` | Technical/balance/asset/runtime behavior outside the content packet. |

Ownership by field (from `Game_Data_Catalog_Authority_Spec.md` §4.4):
- stable technical ID, schema, version, references, enum/profile IDs, packaging, hash → Codex engineering
- player-facing name/description/lore/quest text/localization → Codex narrative/content + user approval
- balance/tuning values → existing approved value or a separate approved balance decision
- final product/creative/release acceptance → user

---

## 2. Source files, schemas, loaders, validators

### 2.1 The six-family schema (record shape authority)

| Path | Role |
| --- | --- |
| `unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataSixFamilySchemas.cs` | Strict version-1 record shapes for realms/buildings/research/troops/champions/skills. No records, no loading, not wired. `CreateRegistry()` has no caller in the repo. |
| `unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataRealmReferences.cs` | Exact realm identity/reference tuples (stable IDs, legacy name/value, inner/gate/warzone IDs, rare resource, asset ref). Contains the `stonehold` tuple. |
| `unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataRealmCapabilityProfiles.cs` | Exact per-realm battle capability profiles (multipliers in millionths). Contains `battle_realm_stonehold`. |
| `unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataWalletResourceReferences.cs` | Wallet resource stable IDs (core + optional-rare). Contains `deep_ore` (Stonehold rare resource). |
| `unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataBuildingProgressionRegistry.cs` | Exact building progression authority (15 buildings, cost/duration/prerequisite/eligibility profiles). |

### 2.2 Catalog foundation (loader/validator/store pipeline)

| Path | Role |
| --- | --- |
| `unity/Assets/AL/Scripts/Data/Catalogs/GameDataCatalogModels.cs` | `GameDataCatalogContract` bounds + envelope/manifest/alias/value/record/snapshot models. |
| `unity/Assets/AL/Scripts/Data/Catalogs/GameDataCatalogSchema.cs` | Field rules, record constraints, family schema, schema registry, `GameDataCatalogIdentifiers` (stable-ID regex, SHA-256/path guards). |
| `unity/Assets/AL/Scripts/Data/Catalogs/GameDataCatalogSources.cs` | Bounded byte-read abstraction (`DirectFileGameDataCatalogSource`, platform delegate seam). |
| `unity/Assets/AL/Scripts/Data/Catalogs/GameDataCatalogLoading.cs` | `GameDataCatalogLoader` (prepare/verify/publish, timeout, cancellation). |
| `unity/Assets/AL/Scripts/Data/Catalogs/GameDataCatalogValidator.cs` | Pure fail-closed manifest + family validation (`ValidateManifest`, strict JSON, hash/envelope/record/cross-reference checks). |
| `unity/Assets/AL/Scripts/Data/Catalogs/GameDataCatalogStore.cs` | Atomic snapshot publication; a failed reload never replaces the last accepted snapshot. |
| `unity/Assets/AL/Scripts/Data/Catalogs/StrictJson.cs`, `SaveSemanticCandidateValidation.cs`, `GameDataCatalogSchema.cs` | Strict JSON parser and supporting validation. |

### 2.3 Legacy definition types (mutable, uninstantiated)

| Path | Type | State |
| --- | --- | --- |
| `unity/Assets/AL/Scripts/Data/Definitions/TroopDefinition.cs` | `TroopDefinition` (`Id, Type, DisplayName, Icon, BaseAttack, BaseDefense`) | No committed `.asset`; no caller. |
| `unity/Assets/AL/Scripts/Data/Definitions/ChampionDefinition.cs` | `ChampionDefinition` (`Id, DisplayName, Realm, Family, Portrait, BaseSkills[]`) | No committed `.asset`; no caller. |
| `unity/Assets/AL/Scripts/Data/Definitions/SkillDefinition.cs` | `SkillDefinition` (`Id, DisplayName, Icon, TargetType, Cooldown, Power`) | No committed `.asset`; no caller. |
| `unity/Assets/AL/Scripts/Data/Definitions/WarmasterSetDefinition.cs` | `WarmasterSetDefinition` (`Id, SetName, Icon`) | No committed `.asset`. |

These are *migration evidence*, not production authority.

### 2.4 Runtime service + interface (current, always-null for the four families)

| Path | Note |
| --- | --- |
| `unity/Assets/AL/Scripts/Core/Interfaces/IGameDataService.cs` | `GetTroop/GetChampion/GetSkill` legacy nullable methods. |
| `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs` | `GetTroop/GetChampion/GetSkill` return `null`; realms/buildings/research are runtime-created mutable fallbacks. |

### 2.5 Legacy skill loaders (competing, partial, slot-indexed)

| Path | Note |
| --- | --- |
| `unity/Assets/AL/Scripts/ChampionMode/Skills/SkillCaster.cs` | Four hard-coded slot arrays; behavior switched on slot index, not skill ID. |
| `unity/Assets/AL/Scripts/ChampionMode/Skills/SkillLoadoutCatalog.cs` | Loads `skillLoadouts`, ignores `version`, returns mutable arrays; any nonempty array succeeds. |
| `unity/Assets/AL/Scripts/ChampionMode/Skills/SkillEffectFactory.cs` | Procedural VFX + synthesized audio (not per-skill assets). |

### 2.6 Existing packaged content catalogs (the only authored JSON)

| Path | Contents |
| --- | --- |
| `unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json` | Four realm records incl. full `stonehold` lore/continuity/starting hooks. |
| `unity/Assets/AL/StreamingAssets/GameData/al_warmaster_content_catalog.json` | Warmaster set `prototype_true_warmaster` + 10 pieces (display names/summaries). |
| `unity/Assets/AL/StreamingAssets/GameData/al_skill_weather_catalog.json` | `skillLoadouts` (4 rows), `skillEffects`, `weatherProfiles`; version `0.3.0` ignored. |
| `unity/Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json` + `..._content_catalog.json` | Appearance options/presets (not champion records). |

### 2.7 Governing specs and source packets (lore/design authority)

| Path | Role |
| --- | --- |
| `unity/Docs/Game_Data_Catalog_Authority_Spec.md` | Binding spec (#183): ID/alias policy, load/query contracts, validation contract, per-family requirements. |
| `unity/Docs/Game_Data_Source_Inventory.md` | Phase B observational freeze: exact current IDs/values/consumers for every family. |
| `unity/Docs/Narrative/GameData/Phase_C_Six_Family_Source_Packet.md` | Narrative/content source packet: dispositions for all six families. |
| `unity/Docs/Narrative/GameData/phase-c-six-family-content-map.json` | **Authoritative machine-readable content map** — 35 content references + exact English strings. |
| `unity/Docs/GameDataCatalog/PhaseC/Phase_C_Six_Family_Technical_Handoff.md` | Production field requirements, canonical IDs (incl. research), absence policy. |
| `unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source-v003.json` | Current technical source + blocker ledger (v003). |
| `unity/Docs/GameDataCatalog/PhaseC/Phase_C7A_Champion_Authority_Convergence.md` | Champion family: zero records, `blocked_required`, six blockers. |
| `unity/Docs/GameDataCatalog/PhaseC/Phase_C8A_Skill_Authority_Convergence.md` | Skill family: four identities/names preserved, all behavior/balance blocked. |
| `unity/Docs/Narrative/GameData/Four_Realm_Launch_Source_Handoff.md` | Realm catalog source (Stonehold stable IDs). |
| `unity/Docs/Narrative/GameData/Warmaster_Content_Source_Handoff.md` | Warmaster set/piece content source. |
| `unity/Docs/champion-character-sheets-blender-handoff.v1.json` + `Champion_Character_Sheets_Blender_Handoff.md` | Stonehold Vanguard character sheet (visual source, `runtimeAuthority: false`). |
| `unity/Assets/AL/Art/Designs/FourRealmChampionAnchor.md` | Stonehold Vanguard visual direction (owner-approved, production not approved). |
| `unity/Docs/Terrestrials/RealmBossesAndElites/Realm_Boss_Elite_Design_Source.md` + `realm_boss_elite_profiles_manifest.json` | Stonehold **enemy** elites/bosses (visual source, not player troops/champions). |

---

## 3. Field requirements per family

Source: `GameDataSixFamilySchemas.cs` (authoritative record shape) + spec §10.

### 3.1 Common envelope (every family file)
`gameId`, `catalogId`, `family`, `schemaVersion`, `contentVersion`, `sourceRevision`, `records[]` (+ optional `aliases[]`).

### 3.2 Common manifest (catalog set)
`gameId`, `catalogSetId`, `schemaVersion`, `contentVersion`, `minimumRuntimeCatalogVersion`, `sourceRevision`, `artifacts[]`.
Each artifact: `family`, `catalogId`, `relativePath`, `schemaVersion`, `contentVersion`, `required`, `sha256`, `mediaType`, `sourceMode`, `sourceRevision`.

### 3.3 `research` (schema field → type → constraint)

| Field | Kind | Constraint |
| --- | --- | --- |
| `name_ref` | string (content ref) | required, nonblank |
| `max_level` | integer | 1 .. int.MaxValue |
| `cost_profile_id` | string (stable ref) | required, nonblank |
| `duration_profile_id` | string (stable ref) | required, nonblank |
| `effect_ids` | string array (stable refs) | 1..64 items |
| `prerequisite_research_ids` | string array (stable refs, family `research`) | 0..64 items |

### 3.4 `troops`

| Field | Kind | Constraint |
| --- | --- | --- |
| `legacy_troop_type` | enum | `Infantry`, `Cavalry`, `Ranged`, `Siege` |
| `legacy_troop_value` | integer | 0..3 |
| `name_ref` | string (content ref) | required, nonblank |
| `base_attack` | integer | 0 .. int.MaxValue |
| `base_defense` | integer | 0 .. int.MaxValue |
| `training_profile_id` | string (stable ref) | required, nonblank |
| `asset_ref` | string (asset ref) | required, nonblank |

### 3.5 `champions`

| Field | Kind | Constraint |
| --- | --- | --- |
| `name_ref` | string (content ref) | required, nonblank |
| `realm_id` | string (stable ref, family `realms`) | required, nonblank |
| `class_family_id` | enum (stable) | `warrior`, `mage`, `ranger`, `assassin` |
| `portrait_asset_ref` | string (asset ref) | required, nonblank |
| `model_asset_ref` | string (asset ref) | required, nonblank |
| `base_skill_ids` | string array (stable refs, family `skills`) | 1..16 items |
| `stat_profile_id` | string (stable ref) | required, nonblank |

### 3.6 `skills`

| Field | Kind | Constraint |
| --- | --- | --- |
| `name_ref` | string (content ref) | required, nonblank |
| `behavior_profile_id` | string (stable ref) | required, nonblank |
| `presentation_profile_id` | string (stable ref) | required, nonblank |
| `target_type` | enum (stable) | `single`, `aoe`, `self`, `ally`, `enemy` |
| `cooldown_seconds` | number | 0 .. float.MaxValue |
| `power` | number | 0 .. float.MaxValue |
| `mana_cost` | number | 0 .. float.MaxValue |
| `cast_time_seconds` | number | 0 .. float.MaxValue |
| `range_meters` | number | 0 .. float.MaxValue |
| `vfx_asset_ref` | string (asset ref) | required, nonblank |
| `audio_asset_ref` | string (asset ref) | required, nonblank |

### 3.7 Anchor families (already sourced, for cross-reference)
- **realms** record for Stonehold (`stonehold`): `legacy_realm_id=Stonehold`, `legacy_realm_value=1`,
  `name_ref=realm.stonehold.name`, `description_ref=realm.stonehold.description`,
  `inner_realm_id=inner_stonehold`, `main_gate_id=gate_stonehold_faultline`,
  `outer_warzone_id=warzone_stonehold`, `rare_resource_id=deep_ore`,
  `capability_profile_ids=[battle_realm_stonehold]`, `asset_ref=Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Stonehold_Flat_256_v001.png`.
- **buildings** (15, realm-agnostic): canonical IDs `town_hall`..`watchtower`; Stonehold does not
  have realm-specific buildings.

---

## 4. ID conventions

From `GameDataCatalogIdentifiers.IsCanonicalStableId` and spec §5:

- **Stable technical IDs**: lowercase snake-case, regex `^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$`,
  max 128 chars, case-sensitive ordinal, no double/trailing underscores.
- **Content references** (player-facing text keys): dotted ASCII, e.g. `skill.realm_strike.name`,
  `research.steel_forging.name`; each segment is a canonical stable ID. Never derived from
  display text at runtime.
- **Aliases**: exact ordinal match only; one legacy ID → one canonical ID; no chains/cycles;
  alias resolution returns `AliasResolved` + both IDs (observable, never silent case folding).
- **Enums**: `RealmId {None=0, Stonehold=1, Eldergrove=2, Crownlands=3, Umbral=4}`;
  `TroopType {Infantry, Cavalry, Ranged, Siege}`; `ClassFamily {Warrior, Mage, Ranger, Assassin}`;
  `SkillTargetType {Single, AoE, Self, Ally, Enemy}`; `SubclassId {None, Vanguard, ... Druid}`.
  The six-family schema uses lowercase canonical enum strings (`warrior/mage/ranger/assassin`,
  `single/aoe/self/ally/enemy`) while `legacy_troop_type` uses the PascalCase legacy strings.

---

## 5. Balance constraints (what is and is not authorable)

- **Research**: names + canonical IDs are fixed; `max_level`, costs, durations, effects, and
  prerequisites have **no approved source** (spec §10.3: "currently no dedicated research
  definition/query; #183 adds one"). `GetStatBonus` hard-codes `Steel Forging`→Attack,
  `Plate Armor`→Defense at `Level * 0.05f` — that is migration evidence, not balance.
- **Troops**: no base attack/defense/training profile is approved. `DeterministicBattleSimulator`
  hard-codes base power 10/15/12/20 for Infantry/Cavalry/Ranged/Siege — migration evidence only.
- **Champions**: no stat profile is approved. `ChampionCombat` defaults (health `1000`, mana `100`,
  regen `7.5`, attack `50`) carry documented drift vs. `10` regen and `125` attack in the binding
  spec — unresolved, not authorable.
- **Skills**: observed nine-field rows (cooldown/mana/cast/range/power/bot-multiplier/vfx) are
  "exact migration evidence" and `skills.balance_acceptance` is **open** (C8A §4). None is
  approved balance.
- **Global rule**: numeric balance "does not change in #183"; only byte/value-equivalent migration
  is allowed, or a separately approved balance decision.

---

## 6. Dependencies between content types

Cross-family reference edges the six-family schema enforces:

- `champions.realm_id` → `realms` (every champion must reference an existing realm record).
- `champions.base_skill_ids` → `skills` (1..16; every referenced skill must resolve in the same
  accepted catalog set).
- `champions.stat_profile_id`, `portrait_asset_ref`, `model_asset_ref` → approved profiles/assets
  (none currently exist).
- `skills.behavior_profile_id` / `presentation_profile_id` / `vfx_asset_ref` / `audio_asset_ref`
  → approved behavior/presentation/asset profiles (none currently exist).
- `research.effect_ids`, `cost_profile_id`, `duration_profile_id` → approved profiles (none exist).
- `research.prerequisite_research_ids` → `research` (acyclic DAG required).
- `troops.training_profile_id`, `asset_ref` → approved profiles/assets (none exist).

Whole-snapshot rule: a required-family error blocks that family; a required cross-family reference
failure blocks the entire catalog set when safe partial use cannot be proven.

---

## 7. Validation rules (binding contract)

From spec §9 and the schema/constraint implementation:

Manifest/envelope — reject/report: wrong `gameId`; blank/duplicate catalog IDs; unsupported
schema version; duplicate family entries; path traversal/absolute path; missing file; malformed
SHA-256; hash mismatch; media-type mismatch; identity mismatch.

Record — reject/report: null record; blank ID; duplicate canonical ID; alias collision/cycle;
invalid enum/profile ID; missing required field; unknown field under strict-version policy;
non-finite numeric; out-of-range/contradictory numeric; missing localization/content reference;
missing asset reference; missing behavior/profile reference; unresolved cross-family reference.

Record constraints (schema-level cross-checks) already implemented for realms/buildings:
`REALM-RARE-RESOURCE-REFERENCE`, `REALM-CAPABILITY-PROFILE-REFERENCE`, `REALM-WORLD-ASSET-REFERENCE`,
`BUILDING-PROGRESSION-REFERENCE`. Research/troops/champions/skills have **no** record constraints
yet (they have no records to constrain).

Publication — whole-snapshot validation; deterministic ordering (manifest order, then record
order/canonical ID, field path, code); failure before publication leaves the previous snapshot
intact; initial failure leaves the service `Unavailable` (never partially populated).

Generation refusal — `tools/game-data/Test-PhaseCSixFamilyTechnicalSource.ps1`
(`-RequireProductionEligible`) must refuse production eligibility (31 mappings, 35 references,
6 unavailable anchors, 32 blockers, zero troop/champion mappings).

---

## 8. Stonehold content-authoring map

"Must be added" is stated against the canonical source. Where the source does not exist, the
correct authoring action is an **explicit `not_authored_unavailable` placeholder**, never an
invented record.

### 8.1 Realms (anchor — already authored, verify only)

| Record | Status | Justifying source |
| --- | --- | --- |
| `stonehold` | Exists in `al_realm_catalog.json` and `GameDataRealmReferences` | `Four_Realm_Launch_Source_Handoff.md`, `al_realm_catalog.json`, `phase-c-six-family-content-map.json` (`realm.stonehold.name`/`.description`), `GameDataRealmReferences.cs` |

No new realm record is required. Stonehold's stable IDs, gems (`gem_stonehold_forge`,
`gem_stonehold_depth`), gate (`gate_stonehold_faultline`), rare resource (`deep_ore`),
capability profile (`battle_realm_stonehold`), starter class bias (`vanguard`, `warden`,
`dreadknight`), and continuity hooks (`forge_gate_keeper`) are the reference anchors for any
future Stonehold champion/troop.

### 8.2 Research — 8 name-only records, canonical IDs fixed, mechanics blocked

All eight are realm-agnostic (no Stonehold-specific research exists in any source). Author the
`name_ref` + canonical ID; leave `max_level`/`cost_profile_id`/`duration_profile_id`/
`effect_ids`/`prerequisite_research_ids` as explicit unavailable placeholders.

| Canonical ID | Display name (verbatim) | Content ref | Source |
| --- | --- | --- | --- |
| `steel_forging` | Steel Forging | `research.steel_forging.name` | `Phase_C_Six_Family_Technical_Handoff.md` §Research; `phase-c-six-family-content-map.json` |
| `plate_armor` | Plate Armor | `research.plate_armor.name` | same |
| `masonry` | Advanced Masonry | `research.advanced_masonry.name` | same (canonical `masonry`, alias `Advanced Masonry`; do NOT introduce `advanced_masonry`) |
| `irrigation` | Irrigation | `research.irrigation.name` | same |
| `ballistics` | Ballistics | `research.ballistics.name` | same |
| `logistics` | Logistics | `research.logistics.name` | same |
| `trade_routes` | Trade Routes | `research.trade_routes.name` | same |
| `arcane_study` | Arcane Study | `research.arcane_study.name` | same |

### 8.3 Skills — 4 name-only records, behavior/balance blocked

All four are realm-agnostic. Author the `name_ref` + canonical ID only; every other six-family
field (behavior/presentation profiles, target_type, cooldown/power/mana/cast/range, vfx/audio
asset refs) is `blocked_required` with no approved source.

| Canonical ID | Display name (verbatim) | Content ref | Source |
| --- | --- | --- | --- |
| `realm_strike` | Realm Strike | `skill.realm_strike.name` | `Phase_C8A_Skill_Authority_Convergence.md` §3; `phase-c-six-family-content-map.json` |
| `renewing_guard` | Renewing Guard | `skill.renewing_guard.name` | same |
| `warzone_burst` | Warzone Burst | `skill.warzone_burst.name` | same |
| `warmaster_breaker` | Warmaster Breaker | `skill.warmaster_breaker.name` | same |

(Observed nine-field rows in `al_skill_weather_catalog.json` / `SkillCaster` are migration
evidence only — do not copy them into production records.)

### 8.4 Troops — 0 records (explicit unavailable placeholder)

No Stonehold troop name, stat, training profile, or asset exists in any canonical source.
The four `TroopType` enum identities are `not_authored_unavailable`.

| Anchor | Disposition | Source |
| --- | --- | --- |
| `TroopType.Infantry` / `Cavalry` / `Ranged` / `Siege` | `not_authored_unavailable` | `phase-c-six-family-content-map.json` §troops; `Phase_C_Six_Family_Source_Packet.md` §6; `Phase_C_Six_Family_Technical_Handoff.md` |

Action: author an explicit absence marker, do **not** emit empty `troops` artifacts as
"required: false" (spec §absence: troops/champions are missing *required* source, not optional
content), and do **not** invent names from enum labels or simulator values.

### 8.5 Champions — 0 records (visual-only precursor, explicit unavailable placeholder)

No Stonehold champion identity (name, realm/class assignment, portrait/model reference,
base-skill list, stat profile) is defensible from current source.

| Precursor (not a record) | Type | Source |
| --- | --- | --- |
| "Stonehold Vanguard" turnaround sheet | Visual-only concept | `FourRealmChampionAnchor.md` §Stonehold Vanguard; `champion-character-sheets-blender-handoff.v1.json` (`champion-stonehold-vanguard-turnaround-v001`, `runtimeAuthority:false`) |
| `starterClassBias: [vanguard, warden, dreadknight]` | Realm starting-hook hint | `al_realm_catalog.json` (Stonehold `startingHooks`) |

Action: author an explicit absence marker citing the visual precursor. Do **not** turn
`Vanguard` into a `ClassFamily` assignment (C7A §4: `Vanguard` is a `SubclassId`, not a
`ClassFamily` value; `ClassFamily` is `Warrior/Mage/Ranger/Assassin`).

### 8.6 Out of scope for this audit (do not conflate)

- **Warmaster set** (`prototype_true_warmaster` + 10 pieces) is *already authored* in
  `al_warmaster_content_catalog.json` and is a separate content family, not troops/champions.
- **Stonehold enemy elites/bosses** (`tdf_elite_stonehold_*`, `tdf_boss_stonehold_fault_crowned_colossus`)
  are terrestrial-design visual source for *enemies*; they are not player troop/champion records.

---

## 9. Non-fabrication rule (explicit)

1. Every authored record must trace to a cited canonical source (content map, technical source,
   spec, or an already-approved runtime value). The source path and exact string must be recorded.
2. Player-facing names, descriptions, and lore may only be copied from `verbatim_preserved`
   content references. They may not be rewritten, case-folded, or derived from enum names,
   display strings, filenames, object names, or test fixtures.
3. Numeric stats, costs, durations, effects, prerequisites, behavior/presentation profiles,
   target types, and asset references are only authorable from an existing approved value or a
   separate approved balance/design decision. Absence is an explicit `not_authored_unavailable`
   placeholder, never a fabricated value.
4. Troops and champions currently have zero authored source; authoring them requires a separate
   creative/product + balance decision first (Phase C dispositions are explicit about this).
5. A partial/empty artifact must not be reported as a complete production family. Fail closed.
6. Generated artifacts must record provenance: source packet ID, merged source commit, repository
   path, and the SHA-256 of the committed Git-blob bytes.

---

## 10. Honest coverage metric

This audit was produced by direct end-to-end reads of the critical path (not subagent summaries):

- **Fully read**: `GameDataSixFamilySchemas.cs`, `GameDataCatalogSchema.cs`, `GameDataCatalogStore.cs`,
  `GameDataRealmReferences.cs`, `GameDataRealmCapabilityProfiles.cs`,
  `GameDataWalletResourceReferences.cs`, `TroopDefinition.cs`, `ChampionDefinition.cs`,
  `SkillDefinition.cs`, `WarmasterSetDefinition.cs`, `LocalGameDataService.cs`, `Enums.cs` (enums),
  `Game_Data_Source_Inventory.md`, `Phase_C_Six_Family_Source_Packet.md`,
  `Phase_C7A_Champion_Authority_Convergence.md`, `Phase_C8A_Skill_Authority_Convergence.md`,
  `Phase_C_Six_Family_Technical_Handoff.md`, `phase-c-six-family-content-map.json`,
  `al_realm_catalog.json`, `al_warmaster_content_catalog.json`,
  `champion-character-sheets-blender-handoff.v1.json`, `FourRealmChampionAnchor.md`,
  `Four_Realm_Launch_Source_Handoff.md`, `Warmaster_Content_Source_Handoff.md`.
- **Partially read (head/tail of large infra files)**: `GameDataCatalogModels.cs`,
  `GameDataCatalogSources.cs`, `GameDataCatalogBuildingProgressionRegistry.cs`,
  `Game_Data_Catalog_Authority_Spec.md` (sections 1–13), `GameDataCatalogValidator.cs` (header),
  `GameDataCatalogLoading.cs` (header), `Realm_Boss_Elite_Design_Source.md` (Stonehold boss + global rules).
- **Not read (out of critical path for these four families)**: the remaining Terrestrials
  ecosystem/fauna packets, per-realm Architecture/TownHall/animation handoffs, NVS-01 narrative
  fidelity dispositions, and the Android Kotlin shell (explicitly out of scope — do-not-touch).

The families named in this task (troops/champions/research/skills) and their governing schemas,
loaders, validators, and source packets are covered end-to-end. The one caveat worth stating: the
full body of `GameDataCatalogValidator.cs` (2072 lines) was characterized from its header and the
binding spec's §9 validation contract rather than read line-by-line; its validation rules are
accurately summarized above from the spec + schema code.
