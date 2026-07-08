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

## Runtime Snapshot Contracts

`SharedContracts/Fable/AnotherLife.Contracts.fs` also includes lightweight records for prototype runtime state:

- `TroopInventoryData`
- `ChampionCustomizationState`
- `PrototypeProgressionSnapshot`

These are intended for external tools, Fable dashboards, balance editors, or web-based character editors. They mirror the shape of Unity save data while keeping enum values as strings for easier cross-platform decoding.

## Fable Usage

The F# records intentionally use strings for realm IDs and keys. That makes them easy to decode from JSON in Fable apps with Thoth.Json, Fable.SimpleJson, or a custom decoder.
