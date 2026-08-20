# Building & Champion Art/Asset Reference — Schema Delta

Task: t_13ba4fba (synthesis of t_2e30a09f, t_7acdd824, t_1cb456f4, t_d95885e3)
Date: 2026-08-20
Scope: buildings + champions only. Troops, fauna, bosses out of scope.

This document is the landed schema delta, not a draft. The schemas and catalogs it
describes are on disk in this worktree.

## 0. TL;DR

Buildings and champions had no JSON schema and no JSON catalog. Gameplay data stays
in the SixFamily C# registries. The only prior 3D binding was
`KingdomBuildingModelCatalog.asset` (8 of 60 realm×building slots, PascalCase /
dot-notation IDs).

This delta adds identity + content-ref + art-ref catalogs so 3D models bind to
stable snake_case catalog IDs:

| File | Role |
| --- | --- |
| `unity/SharedContracts/Schemas/al-building.schema.json` | Building art schema |
| `unity/SharedContracts/Schemas/al-champion.schema.json` | Champion art schema |
| `unity/Assets/AL/StreamingAssets/GameData/al_building_catalog.json` | 15 buildings; 8 hash-pinned models |
| `unity/Assets/AL/StreamingAssets/GameData/al_champion_catalog.json` | 4 Vanguard identity records; art unset |
| `unity/SharedContracts/Fable/AnotherLife.Contracts.fs` | `BuildingArtCatalog` / `ChampionArtCatalog` |

`KingdomBuildingModelCatalog.asset` is **not** modified. `CityLayoutEngine` still
loads the ScriptableObject. Runtime loader migration is a later task. These catalogs
are **not** registered in `GameDataCatalogManifest` (string `version` vs integer
manifest version).

## 1. Settled decisions

Settled here from existing repo patterns. Not re-opened.

1. **Asset ref shape** = object `{path, guid, sha256}` (A1 hash-pinned tuple used by
   `GameDataRealmReferences`). JSON has no C# sidecar, so the three values travel
   together. This is a type change vs SixFamily `asset_ref` **string**; no existing
   building/champion JSON exists to migrate.
2. **Building models are realm-scoped** `models[]`. Mirrors
   `KingdomBuildingModelCatalog` lookup `RealmId:BuildingId`. A singular
   `asset_ref` cannot express four realm variants.
3. **Envelope** = realm-catalog convention: required `version`, `catalogId`, `game`,
   `idFormat: "lowercase_snake_case"`. This is the defect the child proposal hit
   (`additionalProperties: false` rejected the sample envelope).
4. **`minItems`**: buildings = 15/15 (closed identity set). Champions = `minItems: 1`
   (family will grow past the four Vanguards). `models` is required and may be `[]`.
5. **`model_id`** is snake_case `building_{realm_id}_{building_id}_{variant}_v{n}`.
   Dot-notation ScriptableObject IDs stay as a legacy alias table (§5).
6. **Art refs optional on champions**; identity required. Crownlands Vanguard has a
   Blender candidate under `unity/ArtSource/Champions/` but no Unity production
   prefab, so `model_asset_ref` stays unset.
7. **Fable parity**: add records + README rows. SharedContracts already pairs every
   schematized catalog with a Fable type.
8. **Champion `class_family_id`** stays deferred (gameplay). SixFamily
   `warrior/mage/ranger/assassin` still disagrees with `vanguard/duelist/…` — reconcile
   before a champion *gameplay* catalog, not this art catalog.

## 2. Shared `assetReference`

```json
{
  "type": "object",
  "required": ["path", "guid", "sha256"],
  "properties": {
    "path": {
      "type": "string",
      "pattern": "^Assets/(?:[A-Za-z0-9_-]+/)+[A-Za-z0-9_.-]+$"
    },
    "guid": { "type": "string", "pattern": "^[0-9a-f]{32}$" },
    "sha256": { "type": "string", "pattern": "^[0-9a-f]{64}$" }
  },
  "additionalProperties": false
}
```

ID / content-ref patterns match `IsCanonicalStableId` /
`IsCanonicalContentReference` (no double/trailing underscore, max 128). Path
pattern is extension-agnostic (`.prefab` models, `.png` portraits). The private C#
`IsCanonicalAssetReference` is still `.png`-only and would reject these prefab
paths — do not call it for building models.

## 3. Building record

| Field | Req | Notes |
| --- | --- | --- |
| `id` | yes | One of the 15 canonical snake_case IDs |
| `legacy_building_id` | yes | PascalCase alias (`TownHall`, …) |
| `name_ref` | yes | `building.<id>.name` |
| `models[]` | yes | Empty until art ships. Each entry: `realm_id`, `model_id`, `asset_ref` |

Deferred (still SixFamily C#): `production_profile_ids`, `cost_profile_id`,
`duration_profile_id`, `prerequisite_profile_id`, `realm_eligibility_profile_id`,
`initial_level`, `max_level`.

Extra rules (Python/C# validator, not JSON Schema): unique `id`; unique
`models[].realm_id` per building; `id ↔ legacy_building_id` pairing matches
`GameDataBuildingProgressionRegistry`; `model_id` embeds that record's
`realm_id` + `id`.

## 4. Champion record

| Field | Req | Notes |
| --- | --- | --- |
| `id` | yes | `champion_{realm_id}_{class}` e.g. `champion_crownlands_vanguard` |
| `name_ref` | yes | `champion.<realm>.<class>.name` |
| `realm_id` | yes | crownlands / stonehold / eldergrove / umbral |
| `portrait_asset_ref` | no | unset — concept sheets are not production portraits |
| `model_asset_ref` | no | unset — no Unity production prefab |

Deferred: `class_family_id`, `base_skill_ids`, `stat_profile_id`.

## 5. Legacy → canonical model-ID aliases

| ScriptableObject `modelId` | Canonical `model_id` |
| --- | --- |
| `building.crownlands.townhall.production.v1` | `building_crownlands_town_hall_production_v1` |
| `building.stonehold.townhall.production.v1` | `building_stonehold_town_hall_production_v1` |
| `building.eldergrove.townhall.production.v1` | `building_eldergrove_town_hall_production_v1` |
| `building.umbral.townhall.production.v1` | `building_umbral_town_hall_production_v1` |
| `building.crownlands.workshop.production.v1` | `building_crownlands_workshop_production_v1` |
| `building.stonehold.workshop.production.v1` | `building_stonehold_workshop_production_v1` |
| `building.eldergrove.workshop.production.v1` | `building_eldergrove_workshop_production_v1` |
| `building.umbral.workshop.production.v1` | `building_umbral_workshop_production_v1` |

`townhall` → `town_hall` is not a mechanical dot→underscore replace.

Prefab file names are realm-flavored (`Crownlands_Stormwright_Production`,
`Umbral_Veilwright_Production`) but the catalog ID stays `workshop`. Paths are
copied from on-disk prefabs (town halls live under `Production/TownHall/Runtime/`;
workshops live under `Production/Runtime/`). Do not reconstruct paths.

## 6. Pilot sample binding (Crownlands Town Hall)

This is the one concrete already-shipped production model. It is record
`buildings[0].models[0]` in `al_building_catalog.json`.

```json
{
  "id": "town_hall",
  "legacy_building_id": "TownHall",
  "name_ref": "building.town_hall.name",
  "realm_id": "crownlands",
  "model_id": "building_crownlands_town_hall_production_v1",
  "asset_ref": {
    "path": "Assets/AL/Art/Generated/Architecture/Crownlands/Production/TownHall/Runtime/Crownlands_TownHall_Production.prefab",
    "guid": "40d5f7687fed640fd8c0d4b1868ff0ef",
    "sha256": "71ea52234ec8aea93b91bf39ae41d111fa1a7d54cf181f54894e895d23463b46"
  }
}
```

Provenance: GUID from `Crownlands_TownHall_Production.prefab.meta`; SHA-256 from
on-disk prefab bytes (97170). Matches t_1cb456f4 / t_d95885e3.

## 7. Out of scope

- Troops, fauna, bosses (no catalogs yet; Slagwhistle remains a direct scene bind).
- Gameplay numeric fields (stay in SixFamily C#).
- Switching `CityLayoutEngine` off `KingdomBuildingModelCatalog`.
- Normalizing the two older camelCase schemas.
- Registering these catalogs in the integer-versioned `GameDataCatalogManifest`.
- Binding champion concept sheets as production portraits.

## 8. Child defects fixed in this landing

| Child finding | Fix |
| --- | --- |
| Schema omitted `catalogId`/`game`/`idFormat` | All three required on both schemas |
| `minItems: 15` vs 1-record sample | Production catalog has all 15; sample is an excerpt |
| Placeholder sha256 | Real hashes for all 8 bound prefabs |
| Sample path dropped `TownHall/` | Paths copied from on-disk prefabs |
| ID regex looser than `IsCanonicalStableId` | Tightened (`no __`, no trailing `_`, maxLength 128) |
| Envelope vs older `version`+`game` schemas | New catalogs follow `al_realm_catalog` |
