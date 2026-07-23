# Phase C Six-Family Narrative/Content Source Packet

## Document control

| Field | Value |
| --- | --- |
| Tracked issue | `#183` |
| Phase | `Phase C — source-mode review` |
| Packet ID | `game-data-phase-c-six-family-source-2026-07-23-v001` |
| Authoritative content map | `unity/Docs/Narrative/GameData/phase-c-six-family-content-map.json` |
| Primary mode | Codex narrative/content |
| Upstream `main` | `38de51138cc8b92c8469c7e9b5c37e84dead7ff1` |
| Governing specification | `unity/Docs/Game_Data_Catalog_Authority_Spec.md` |
| Evidence inventory | `unity/Docs/Game_Data_Source_Inventory.md` |
| Codex source-fidelity status | Ready for coordination/review; the content-reference handoff is ready, while full technical mappings and absent families remain blocked |
| User final creative acceptance | Pending; this packet does not claim it |
| Runtime authority | Unchanged |

This packet supplies the narrative/content references required before Codex engineering encodes the first six technical game-data families. The companion JSON content map is the authoritative machine-readable source for exact references and English strings; this Markdown document controls scope, disposition, approval, and handoff. The pair preserves observed source where it exists and records honest unavailability where it does not. It does not create production catalogs, approve balance, or activate runtime content.

## 1. Scope and disposition vocabulary

The packet covers only realms, buildings, research, troops, champions, and skills.

| Disposition | Meaning |
| --- | --- |
| `verbatim_preserved` | Codex narrative/content accepts the listed reference and exact current string for no-drift engineering encoding. It is not a new creative rewrite or final user approval. |
| `unresolved_user_decision` | A new or changed creative/product decision is required before source can be authored or activated. |
| `not_authored_unavailable` | No approved production record or player-facing source exists. Engineering must represent absence honestly and must not fabricate a placeholder. |
| `out_of_scope` | The observed value is technical, balance, asset, runtime, or consumer behavior outside this source packet. |

Technical IDs, aliases, schemas, versions, numeric values, behavior profiles, asset references, validators, packaging, and runtime queries remain Codex engineering or coordination/review work. The user retains final creative, product, and balance approval.

## 2. Source precedence and provenance

The no-drift baseline is the observed Unity source/value state at the upstream commit, cross-checked against the Phase B inventory. Realm and skill strings are currently rendered; building labels are legacy derived placeholders newly literalized here; research labels are legacy identity strings newly designated here as presentation source.

- realm strings: `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`;
- building IDs and currently derived display values: `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs` and the exact-value inventory;
- research display-string identities: `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs` and the exact-value inventory;
- troop enum evidence only: `unity/Assets/AL/Scripts/Core/Enums/Enums.cs`;
- champion absence: the committed source inventory confirming zero champion assets or records;
- skill IDs and display names: `unity/Assets/AL/Scripts/ChampionMode/Skills/SkillCaster.cs` and `unity/Assets/AL/StreamingAssets/GameData/al_skill_weather_catalog.json`.

The committed alternative `ProjectInitializer` template strings, whose generated realm assets are not committed, plus Android preview strings, UI-only command labels, enum-derived text, procedural GameObjects, customization presets, skill role tokens, VFX keys, effects, and weather rows are not selected as narrative/content authority here.

Engineering-generated artifacts must later retain this packet ID, the merged source commit, the authoritative content-map path, and the SHA-256 of the content map's committed Git-blob content bytes. Working-tree line-ending conversion is not part of that hash. File presence or merge state alone does not imply user creative approval or production activation.

## 3. Realms

The four effective runtime names and descriptions are preserved exactly in the authoritative content map.

| Existing technical anchor | Name reference | Description reference | Disposition |
| --- | --- | --- | --- |
| `RealmId.Stonehold` | `realm.stonehold.name` | `realm.stonehold.description` | `verbatim_preserved` |
| `RealmId.Eldergrove` | `realm.eldergrove.name` | `realm.eldergrove.description` | `verbatim_preserved` |
| `RealmId.Crownlands` | `realm.crownlands.name` | `realm.crownlands.description` | `verbatim_preserved` |
| `RealmId.Umbral` | `realm.umbral.name` | `realm.umbral.description` | `verbatim_preserved` |

Boundary notes:

- `RealmId.None` has no record or content reference.
- The embedded percentage prose is preserved player-facing text only. It does not authorize, define, or change mechanical bonuses.
- Rare-resource mappings, technical capability profiles, icons, and the numeric enum values are `out_of_scope`.
- The alternative `ProjectInitializer` descriptions are not selected and remain `out_of_scope`.
- Copy-quality changes, including expanding `Def`, `Atk`, or the repeated `Perks:` wording, require a later source revision and user creative review.

## 4. Buildings

The current fifteen definition-derived display values are made explicit as literal source strings so engineering does not continue deriving presentation from an ID. Some board and command paths independently shorten `Town Hall` to `Hall`, `Lumber Mill` to `Lumber`, or `Gold Mine` to `Gold`; this packet intentionally selects the full definition labels as the no-drift family source and does not claim to preserve every UI-specific short label.

| Existing technical anchor | Name reference | Disposition |
| --- | --- | --- |
| `TownHall` | `building.town_hall.name` | `verbatim_preserved` |
| `Farm` | `building.farm.name` | `verbatim_preserved` |
| `LumberMill` | `building.lumber_mill.name` | `verbatim_preserved` |
| `Quarry` | `building.quarry.name` | `verbatim_preserved` |
| `GoldMine` | `building.gold_mine.name` | `verbatim_preserved` |
| `Barracks` | `building.barracks.name` | `verbatim_preserved` |
| `Academy` | `building.academy.name` | `verbatim_preserved` |
| `Market` | `building.market.name` | `verbatim_preserved` |
| `Storehouse` | `building.storehouse.name` | `verbatim_preserved` |
| `Forge` | `building.forge.name` | `verbatim_preserved` |
| `Stable` | `building.stable.name` | `verbatim_preserved` |
| `Workshop` | `building.workshop.name` | `verbatim_preserved` |
| `Embassy` | `building.embassy.name` | `verbatim_preserved` |
| `Wall` | `building.wall.name` | `verbatim_preserved` |
| `Watchtower` | `building.watchtower.name` | `verbatim_preserved` |

| Consumer-only ID | Content reference | Disposition | Reason |
| --- | --- | --- | --- |
| `ManaShrine` | none | `not_authored_unavailable` | A visible UI label does not establish an approved definition, identity, or tuning source. |
| `Mine` | none | `not_authored_unavailable` | A visible UI label does not establish an approved definition, identity, or tuning source. |

No building description, cost, duration, production profile, asset, or maximum-level decision is authored here. Existing `MaxLevel = 10` is technical/balance evidence and remains `out_of_scope` for this packet.

## 5. Research

The eight current identity strings are preserved as presentation only. `Steel Forging` and `Plate Armor` are currently rendered in the research panel; the other six are private legacy identity rows newly designated here as presentation source. All eight remain cross-reference anchors and are not selected here as canonical technical IDs.

| Existing Unity identity | Name reference | Disposition |
| --- | --- | --- |
| `Steel Forging` | `research.steel_forging.name` | `verbatim_preserved` |
| `Plate Armor` | `research.plate_armor.name` | `verbatim_preserved` |
| `Advanced Masonry` | `research.advanced_masonry.name` | `verbatim_preserved` |
| `Irrigation` | `research.irrigation.name` | `verbatim_preserved` |
| `Ballistics` | `research.ballistics.name` | `verbatim_preserved` |
| `Logistics` | `research.logistics.name` | `verbatim_preserved` |
| `Trade Routes` | `research.trade_routes.name` | `verbatim_preserved` |
| `Arcane Study` | `research.arcane_study.name` | `verbatim_preserved` |

Canonical IDs, exact aliases, save migration, levels, costs, durations, effects, prerequisites, and stat bonuses remain `out_of_scope`. Engineering and coordination/review must resolve the explicit alias table; runtime must not infer it by changing case, spaces, or punctuation.

## 6. Troops

The repository has four enum identities but zero committed `TroopDefinition` records. Enum existence and combat formulas do not create narrative/content authority.

| Observed technical identity | Content reference | Disposition |
| --- | --- | --- |
| `TroopType.Infantry` | none | `not_authored_unavailable` |
| `TroopType.Cavalry` | none | `not_authored_unavailable` |
| `TroopType.Ranged` | none | `not_authored_unavailable` |
| `TroopType.Siege` | none | `not_authored_unavailable` |

No production troop name, description, identity, stat, training profile, or asset reference is authorized. UI strings such as `Infantry` and `Ranged`, Android strings, enum names, and simulator base-power values are evidence only. New troop source requires a separate creative/product and balance decision.

## 7. Champions

There are zero committed production `ChampionDefinition` records and therefore zero approved champion content references.

| Record set | Content reference | Disposition |
| --- | --- | --- |
| Production champion definitions | none | `not_authored_unavailable` |

Procedural Champion Mode GameObjects, customization option/preset IDs, forge labels, and class/realm enums do not authorize a champion identity. Engineering must not invent a placeholder name, realm, class family, portrait/model reference, base-skill list, or stat profile. New champion source requires a separate creative/product and balance decision.

## 8. Skills

Only the four matching current IDs and display names are preserved as narrative/content source. No description or lore is added.

| Existing technical anchor | Name reference | Disposition |
| --- | --- | --- |
| `realm_strike` | `skill.realm_strike.name` | `verbatim_preserved` |
| `renewing_guard` | `skill.renewing_guard.name` | `verbatim_preserved` |
| `warzone_burst` | `skill.warzone_burst.name` | `verbatim_preserved` |
| `warmaster_breaker` | `skill.warmaster_breaker.name` | `verbatim_preserved` |

Slot indices, role tokens, behavior, target type, VFX/presentation keys, cooldown, mana, cast time, range, power, bot multipliers, effects, weather, audio, and gameplay meaning remain `out_of_scope`. The current partial JSON overlay must not be described as complete merely because its four display names match the hard-coded array.

## 9. Localization and source-text policy

- References are stable ASCII dotted keys and are separate from technical record IDs.
- Lower-snake segments are used within each dotted reference; engineering must not infer a technical ID from a content reference.
- Each of the 35 references in this packet maps to exactly one English source string.
- Exact case, spacing, punctuation, symbols, abbreviations, and line feeds are significant.
- No runtime localization capability is claimed. This packet defines source references and initial English strings only.
- Runtime code must not derive display text from enum names, PascalCase IDs, lower-snake IDs, aliases, or fallback object names.
- Missing content references are intentional unavailable states, not permission to synthesize copy.
- A later source revision must version and review any text change; generated artifacts must not overwrite this document.

## 10. Engineering handoff

This is a content-reference handoff, not a declaration that any family has all required technical fields. A separately reviewed technical mapping must resolve every required ID, alias, profile, asset, cross-reference, and numeric field from approved existing evidence, or keep the affected family unavailable.

Before final user creative acceptance, engineering work using these references is limited to non-wired, non-authoritative shadow/source artifacts. Those artifacts must not become runtime authority, production activation, user-approval evidence, or release evidence.

After coordination/review accepts this packet, Codex engineering may:

- encode the exact `verbatim_preserved` references and source strings without rewriting them;
- associate those references with separately reviewed canonical technical records;
- preserve current technical IDs and values, and implement only aliases explicitly approved by coordination/review;
- after coordination/review explicitly approves schema/manifest requiredness and typed `OptionalAbsent` versus unavailable behavior, represent troop and champion source honestly without fabricated records;
- add strict validators that reject blank/duplicate/missing references and provenance drift;
- record the packet ID, merged source commit, repository path, and SHA-256 of the committed Git-blob content bytes in generated provenance;
- shadow-validate every encoded record against the Phase B inventory and report every difference.

Codex engineering must not:

- derive or rewrite player-facing strings;
- use a content reference as an implicit canonical ID or alias;
- promote `ProjectInitializer`, Android, UI-only, enum, procedural, customization, skill-effect, or weather text to authority;
- invent troop/champion records, `ManaShrine`, `Mine`, missing descriptions, or placeholder content;
- treat an empty troop or champion artifact as an accepted production family;
- interpret embedded realm percentage prose as the mechanical balance source;
- change numeric values, behavior, balance, assets, saves, scenes, or consumers in the source-encoding PR;
- activate `LocalGameDataService` or claim its shared-file lock during Phase C.

This packet alone does not make troop or champion artifacts optional. If coordination/review has not approved exact manifest/schema requiredness, or the technical catalog cannot represent honest absence without fabricated records, engineering must return to coordination/review instead of weakening the source boundary.

## 11. Unresolved decisions

| Decision | Current status | Required owner/step |
| --- | --- | --- |
| Final product use of all preserved strings | `unresolved_user_decision` | User creative acceptance before final product/release approval |
| Any realm copy cleanup or alternative description | `unresolved_user_decision` | New Codex narrative/content revision plus user creative review |
| `ManaShrine` or `Mine` definitions/content | `not_authored_unavailable` | Separate product/source and balance decision |
| Production troop identities/content | `not_authored_unavailable` | Separate product/source and balance decision |
| Production champion identities/content | `not_authored_unavailable` | Separate product/source and balance decision |
| New building, research, or skill descriptions | `not_authored_unavailable` | Separate narrative/content source decision |
| Canonical technical IDs, aliases, stats, profiles, and behavior | `out_of_scope` | Codex coordination/review and engineering under #183/#165/#180 |

No unresolved item may be converted to production content through inference.

## 12. Validation and impact

- [x] All six in-scope families have an explicit disposition.
- [x] The authoritative map preserves four realm names and four descriptions from current effective runtime strings.
- [x] The authoritative map literalizes fifteen building display values without adding `ManaShrine` or `Mine`.
- [x] The authoritative map preserves eight research strings without selecting canonical IDs or aliases.
- [x] Four troop enum identities are inventoried without promoting enum text to content.
- [x] Champion production source is recorded as zero records.
- [x] The authoritative map preserves four skill display names matching both current hard-coded and packaged sources.
- [x] All 35 authoritative content references are nonblank and unique.
- [x] Technical values, balance, runtime, saves, scenes, Android, terrestrial source, and unrelated narrative are excluded.
- [x] No designated shared file is touched or locked.

This documentation-only packet adds no runtime object, asset, dependency, catalog byte, load path, allocation, render cost, network cost, build size, or install size. PC/mobile performance, memory, sight distance, quality tiers, Player builds, PlayMode, and package measurements are not applicable to this source-only change; they remain required when runtime catalogs or world content are implemented.

### Recorded validation evidence

| Check | Command/evidence | Result |
| --- | --- | --- |
| Branch/base | `codex/narrative-game-data-six-family-source` from `38de51138cc8b92c8469c7e9b5c37e84dead7ff1` | Pass; fetched `origin/main` matched the base before branching |
| Strict JSON, structure, dispositions, reference uniqueness, Markdown↔map references, and legacy-source fidelity | `pwsh -NoProfile -File tools/narrative/Test-PhaseCSixFamilyContentMap.ps1 -VerifyLegacyBaseline` | Pass: six families, 37 observed technical anchors, 35 unique content references, 6 explicit unavailable anchors, and 1 unavailable recordless family; legacy baseline matches 4 realms, 15 buildings, 8 research labels, and 4 skills, and confirms no troop/champion assets or lookup records |
| Patch whitespace | `git diff --cached --check` | Pass |
| Independent source review | Current source, inventory, binding family requirements, approval boundaries, and document conventions | Pass after correcting readiness, provenance, absence, alias, consumer, hash-byte, and machine-source boundaries |

Changed files are limited to this handoff document, its authoritative JSON content map, and the focused `tools/narrative/Test-PhaseCSixFamilyContentMap.ps1` validator. The final branch-head SHA and hosted repository gate results are recorded in the pull request because a source document cannot self-cite its containing commit.

## 13. Completion disposition

| Family | Phase C1 source status | Next engineering disposition |
| --- | --- | --- |
| Realms | Content references ready; final user creative acceptance pending | Encode only after the technical mapping resolves every required field without text or value drift |
| Buildings | Content references ready; two consumer-only IDs unavailable | Encode only approved records; preserve typed unavailable query/validation results, with consumer behavior deferred to #165 |
| Research | Content references ready; technical identity/alias work remains | Encode after coordination/review approves exact canonical IDs and aliases |
| Troops | No authored production source; requiredness unresolved | Do not fabricate records; await coordination/review absence policy |
| Champions | No authored production source; requiredness unresolved | Do not fabricate records; await coordination/review absence policy |
| Skills | Four content references ready; technical behavior remains #180-owned | Encode four technical records only where complete validation can fail closed |

This packet completes only the Phase C source-mode handoff. It does not complete Phase C, close #183, acquire a shared-file lock, or authorize production activation.

Exact next request:

> Codex coordination/review: verify this packet against issue #183, the governing catalog specification, the Phase B inventory, current main, source ownership, unresolved user-approval boundaries, and the no-invention rule. Decide exact technical IDs/aliases and manifest/schema requiredness for missing troop/champion source. If accepted, authorize one separate Phase C engineering PR to encode only the exact preserved source and the approved absence policy with provenance, hashes, strict validation, and no production service migration.
