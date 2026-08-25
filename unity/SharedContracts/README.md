# Another Life Shared Contracts

This folder exists so non-Unity tools can read and validate the same design data used by the Unity prototype.

The current compatibility target is Fable/F# tooling. Unity should continue to use the JSON files in:

`Assets/AL/StreamingAssets/GameData/`

Fable or other external tools can use:

- `SharedContracts/Schemas/*.schema.json` to validate JSON.
- `SharedContracts/Fable/AnotherLife.Contracts.fs` for F# record types.
- `SharedContracts/Fable/AnotherLife.Contracts.fsproj` as a small reusable F# project.

## Compatibility Rule

Do not make Unity-only types the source of truth for cross-tool design data. Keep shared catalogs as plain JSON with simple strings, arrays, numbers, and objects.

## Current Shared Catalogs

| Unity JSON | Schema | Fable Record |
| --- | --- | --- |
| `al_character_customization_catalog.json` | `al-character-customization.schema.json` | `CharacterCustomizationCatalog` |
| `al_skill_weather_catalog.json` | `al-skill-weather.schema.json` | `SkillWeatherCatalog` |
| `al_character_customization_content_catalog.json` | `al-character-customization-content.schema.json` | — |
| `al_notification_content_catalog.json` | `al-notification-content.schema.json` | — |
| `al_notification_production_catalog.json` | `al-notification-production.schema.json` | — |
| `al_quest_preview_content_catalog.json` | `al-quest-preview-content.schema.json` | — |
| `al_realm_catalog.json` | `al-realm.schema.json` | — |
| `al_realm_gem_wishgate_content_catalog.json` | `al-realm-gem-wishgate-content.schema.json` | — |
| `al_relationship_authority_content_catalog.json` | `al-relationship-authority-content.schema.json` | — |
| `al_warmaster_content_catalog.json` | `al-warmaster-content.schema.json` | — |
| `al_world_atlas_narrative_catalog.json` | `al-world-atlas-narrative.schema.json` | — |
| `al_world_event_content_catalog.json` | `al-world-event-content.schema.json` | — |
| `al_building_catalog.json` | `al-building.schema.json` | `BuildingArtCatalog` |
| `al_champion_catalog.json` | `al-champion.schema.json` | `ChampionArtCatalog` |

The character customization catalog includes body presets, hair styles, armor styles, primary/hair/skin/eye/accent palettes, face marks, weapon/offhand styles, realm material keys, and slot names so Unity and Fable tools can present the same customization choices.

The skill and weather catalog includes champion skill loadouts, realm skill VFX keys, plus detailed weather profile parameters for particles, fog, ambient light, directional light, wind, turbulence, and lightning. Skill loadouts include slot IDs, display names, cooldowns, mana costs, cast times, ranges, power values, bot damage multipliers, and VFX keys so Unity and Fable tools can balance combat from the same data without referencing UnityEngine types.

## Canonical contracts and the six technical families

Two additional schemas encode the canonical data-contract decisions
(`unity/Docs/Game_Data_Contract_Decisions.md`) and the six technical game-data
families (realms, buildings, research, troops, champions, skills):

- `al-canonical-contracts.schema.json` — the machine-readable authority for the
  six decisions: the 15-building enum (rejects `mana_shrine`/`mine`), non-negative
  int32 Warzone Credits, per-minute integer territory income (`income_per_minute`),
  the eight gems (two per realm, lowercase IDs), lowercase realm IDs (PascalCase
  is a legacy alias only), and Chapter-1 IDs (`ch01_proof_of_worth` + `ch01_<realm>`)
  with a `C1`/`C1_*`/`CH01_PROOF_OF_WORTH` alias table.
- `al-six-family.schema.json` — the JSON shape for the six technical families,
  mirroring `GameDataSixFamilySchemas.cs`. The downstream data-generation task
  uses this as the target shape when converting the legacy hardcoded data.
  Building/champion **art** bindings are a separate surface
  (`al-building.schema.json` / `al-champion.schema.json`) with hash-pinned
  `{path,guid,sha256}` tuples and realm-scoped `models[]`. Those catalogs do
  not replace the six-family gameplay `asset_ref` string field.

## Validation

`SharedContracts/Tests/` holds a self-contained validation harness:

- `validate.py` — loads every schema, asserts each compiles, validates the real
  `StreamingAssets/GameData` catalogs against their schemas, and checks that
  `fixtures/valid/*.json` pass while `fixtures/invalid/*.json` fail.
- `generate_fixtures.py` — regenerates the fixtures (valid samples and
  one-decision-violation invalid samples).

Run it with:

```bash
cd unity/SharedContracts/Tests
uv run --with jsonschema validate.py
```

The real `al_world_event_content_catalog.json` currently fails validation on its
four `notificationDefinitionId` values (`notification.world_event.*` dotted
placeholders) because the schema enforces the canonical `al_notify_*` form — this
is the known inventory conflict #5, to be corrected by the data-generation task,
not the schema.

## Runtime Snapshot Contracts

`SharedContracts/Fable/AnotherLife.Contracts.fs` also includes lightweight records for prototype runtime state:

- `TroopInventoryData`
- `ChampionCustomizationState`
- `TerritorySnapshot`
- `WarmasterProgression`
- `PrototypeProgressionSnapshot`

These are intended for external tools, Fable dashboards, balance editors, or web-based character editors. They mirror the shape of Unity save data while keeping enum values as strings for easier cross-platform decoding.

## Fable Usage

The F# records intentionally use strings for realm IDs and keys. That makes them easy to decode from JSON in Fable apps with Thoth.Json, Fable.SimpleJson, or a custom decoder.
