# WIRE family flatten handoff (t_d4892ee5)

Flattened only the 3 inventory WIRE families to the six-family option-C
envelope. SKIP families and the 3 authored `al_*` sources are untouched.

## Outputs

| Family | Source (unchanged) | Flat envelope |
| --- | --- | --- |
| realm_specialized | `al_realm_catalog.json` | `realm_specialized.v1.json` |
| character_customization | `al_character_customization_catalog.json` | `character_customization.v1.json` |
| skill_weather | `al_skill_weather_catalog.json` | `skill_weather.v1.json` |

Path: `unity/Assets/AL/StreamingAssets/GameData/`.

Envelope: `{gameId, catalogId, family, schemaVersion, contentVersion, sourceRevision, records, aliases}`.
`catalogId` is `{family}_v1`. `sourceRevision` is `t_d4892ee5`.

Regenerate:

```
python tools/game-data/flatten_wire_catalogs.py
python tools/game-data/flatten_wire_catalogs.py --check
```

## Record rules

- Every record has `id` + `kind`. No leftover top-level wrappers (`realms`, `skillLoadouts`, `bodyPresets`, …).
- realm_specialized: realm ids stay `crownlands` / `stonehold` / `eldergrove` / `umbral`. Catalog-level objects become records (`selection_policy`, `narrative_continuity`, `realm_order`, localization keys, `engineering_handoff`). Aliases: `Crownlands` → `crownlands`.
- skill_weather: loadout ids stay `realm_strike` etc. Effect/weather `key` is promoted to `id` and kept as `key`. Aliases: `Realm Strike` → `realm_strike`.
- character_customization: colliding short ids (`duelist`, `grove_green`, `none`, `royal_gold`) are namespaced as `{kind}.{legacy_id}`. Original id is `legacy_id`. Ambiguous bare aliases are omitted.

Do **not** merge into `realms.v1.json` or `skills.v1.json`.

## Downstream

- `t_a56bc943`: register C# schemas for these 3 envelopes only.
- `t_a9097b56`: point `RealmCatalogRuntime`, `CharacterCustomizationCatalog`, and `SkillLoadoutCatalog` at the `.v1.json` files. Leave the authored `al_*` sources in place until that wire lands.
