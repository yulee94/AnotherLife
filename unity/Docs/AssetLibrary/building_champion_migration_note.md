# Building & Champion Art Binding — Migration & Validation Note

Task: `t_13ba4fba` · 2026-08-20
Validates the landed files from this same task (not the 2026-08-18 draft).
Scope: buildings + champions only. Troops, fauna, bosses out of scope.

## 0. Verdict

The landed schemas are valid draft 2020-12. Both production catalogs validate
against them. All 8 bound prefab tuples match on-disk bytes + `.meta` GUIDs.
`migrate_byte_stable_sources.py` still reports **PASS** on every pinned source.
**No byte-stable break.** The SixFamily C# `asset_ref` string field is unchanged
and unused; the JSON catalogs are additive and not registered in
`GameDataCatalogManifest`.

Child draft failures (envelope rejected, `minItems: 15` vs 1-record sample,
placeholder sha256, missing `TownHall/` path segment) are **gone** in the landed
files.

## 1. Method and honest coverage

Actually ran:

- `uv run --with jsonschema python tools/game-data/validate_building_champion_art_catalogs.py`
  → `RESULT: all checks passed`
- `python tools/game-data/migrate_byte_stable_sources.py` (default check, no `--write`)
  → `PASS: all byte-stable sources match their exact reviewed SHA-256 identities`
- `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe build unity/SharedContracts/Fable/AnotherLife.Contracts.fsproj`
  → 0 warnings, 0 errors
- `sha256sum` of the 8 production prefabs (via Python hashlib of file bytes)

Did **not** run: Unity EditMode/PlayMode, `CityLayoutEngine` playtest, or the
private C# `IsCanonicalAssetReference` (it is `.png`-only and would reject every
`.prefab` path — that is a documented future-loader gap, not a current break).

Actually read (full unless noted): both new schemas; both new catalogs;
`al-character-customization.schema.json` header + required list;
`al-skill-weather.schema.json` header; `al_realm_catalog.json` envelope;
`GameDataCatalogIdentifiers.IsCanonicalStableId`;
`GameDataRealmReferences.IsCanonicalAssetReference` / `IsCanonicalContentReference`;
`GameDataSixFamilySchemas` building/champion field rules;
`GameDataBuildingProgressionRegistry` lines 397–426;
`KingdomBuildingModelCatalog.asset` (8 entries);
`SharedContracts/README.md`; `AnotherLife.Contracts.fs` (CatalogPaths tail);
`migrate_byte_stable_sources.py` target list + `main`;
child artifacts from t_2e30a09f / t_7acdd824 / t_1cb456f4 / t_d95885e3.

Not line-read: Unity runtime loaders beyond confirming they do not scan every
GameData JSON; the full Asset Production Manifest; champion `.blend` internals.

## 2. What PASSES

| Check | Result |
| --- | --- |
| `Draft202012Validator.check_schema` on both new schemas | PASS |
| `al_building_catalog.json` vs `al-building.schema.json` | PASS |
| `al_champion_catalog.json` vs `al-champion.schema.json` | PASS |
| `additionalProperties: false` rejects extra top-level key | PASS |
| Missing `catalogId` rejected | PASS |
| 15 building IDs + order + `id ↔ legacy_building_id` | PASS (matches registry 397–426) |
| Every sample ID / `name_ref` / `model_id` vs `IsCanonicalStableId` / `IsCanonicalContentReference` | PASS |
| 8 prefab `path` exist, `guid` = `.meta`, `sha256` = file bytes | PASS |
| Champion art refs absent (no fake portraits/models) | PASS |
| `migrate_byte_stable_sources.py` 18-file pinned manifest | PASS, unchanged |
| `KingdomBuildingModelCatalog.asset` still contains Crownlands TownHall guid `40d5f768…` | PASS, unmodified |
| Fable `AnotherLife.Contracts.fsproj` | PASS, 0/0 |

Pilot tuple (Crownlands Town Hall), re-verified:

- path `Assets/AL/Art/Generated/Architecture/Crownlands/Production/TownHall/Runtime/Crownlands_TownHall_Production.prefab`
- guid `40d5f7687fed640fd8c0d4b1868ff0ef`
- sha256 `71ea52234ec8aea93b91bf39ae41d111fa1a7d54cf181f54894e895d23463b46`

## 3. Schema / catalog changes vs existing serialized data

| Change | Breaks existing JSON? |
| --- | --- |
| New `al-building.schema.json` / `al-champion.schema.json` | No (additive) |
| New `al_building_catalog.json` / `al_champion_catalog.json` | No (not in pinned manifest, not in `GameDataCatalogManifest`) |
| `asset_ref` string → `{path,guid,sha256}` in the **new** JSON only | No (no prior building/champion JSON) |
| Building singular `asset_ref` → `models[]` | No (SixFamily field never populated) |
| Fable records + README rows | No |
| `GameDataSixFamilySchemas.cs` `asset_ref` string rule | Untouched |
| `KingdomBuildingModelCatalog.asset` | Untouched |
| Existing GameData JSON / pinned hashes | Untouched — byte-stable check PASS |

**No schema change in this landing breaks the byte-stable migration.**

Forward-looking: once these two catalogs are added to a SHA-256-pinned manifest,
renaming `id` / `model_id` / the `asset_ref` tuple becomes a byte-stable break
and needs a versioned migration. Keep `version` as a string (`0.1.0`) and do not
register them in the integer-versioned `GameDataCatalogManifest` until a runtime
loader task is scheduled.

## 4. Remaining gaps (honest, not blockers for this task)

1. **C# `.png`-only path check.** `GameDataRealmReferences.IsCanonicalAssetReference`
   is private and rejects `.prefab`. A future JSON loader needs a `.prefab`-aware
   sibling. The JSON `path` pattern is already extension-agnostic.
2. **SixFamily parity.** C# still types `asset_ref` / `portrait_asset_ref` /
   `model_asset_ref` as required strings. JSON is the richer optional object.
   Do not feed these catalogs into `GameDataCatalogValidator` until that schema
   is updated or a separate family is registered.
3. **13/15 buildings and all 4 champions have no art.** Identity records exist;
   `models: []` / omitted champion art refs are intentional.
4. **Runtime still binds via ScriptableObject.** This delta defines the catalog
   surface; it does not switch `CityLayoutEngine`.
5. **`id ↔ legacy_building_id` pairing** is enforced by
   `validate_building_champion_art_catalogs.py`, not by JSON Schema `oneOf`.

## 5. How to re-run

```
uv run --with jsonschema python tools/game-data/validate_building_champion_art_catalogs.py
python tools/game-data/migrate_byte_stable_sources.py
```

PowerShell: same commands; `python` is 3.11 on this machine.
