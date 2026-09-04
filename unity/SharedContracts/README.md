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
| `al_first_session_terrain_catalog.json` | `al-first-session-terrain.schema.json` | — |
| `al_world_asset_inventory.json` | `al-world-asset-inventory.schema.json` | — |
| Realm task catalogs (per realm; preparation only) | `al-realm-character-taxonomy.schema.json` | — |
| `al_four_realm_production_taxonomy.json` | `al-four-realm-production-taxonomy.schema.json` | — |
| `al_rig_motion_standard.json` | `al-rig-motion-standard.schema.json` | — |
| `al_required_motion_manifest.json` | `al-required-motion-manifest.schema.json` | — |
| `al_model_motion_skill_vfx_harness.v1.json` | `al-model-motion-skill-vfx-harness.schema.json` | — |
| `al_boss_skill_presentation_catalog.v1.json` | `al-boss-skill-presentation.schema.json` | — |

`al-world-asset-inventory.schema.json` defines the held post-MVP world-asset logical
family, production identity, binding, provenance, standards, budget-measurement, and
readiness contract. `al_world_asset_inventory.json` is the authoritative preparation
payload: it covers all 242 logical families and preserves eight existing prefab tuples,
but it has no runtime loader and authorizes no asset generation, activation, or
replacement of the current MVP/Resources bindings.

`al-realm-character-taxonomy.schema.json` is the gated per-realm production shape
for playable races, NPCs, Champions, fantasy beasts, monsters, modules, rigs,
facial/secondary systems, platform budgets, motion matrices, skill traceability,
and VFX. The four committed realm payloads are preparation-only and have no runtime
loader. Realm production tasks consume
`unity/Docs/AssetLibrary/PostMVP_Realm_Character_Creature_Catalog_Contract_v1.md`,
create their own realm-scoped preparation catalogs, and keep generation and
activation held until the recorded owner and release gates pass.

`al_four_realm_production_taxonomy.json` deterministically integrates those four
held catalogs without renaming or flattening realm identity. It owns normalized
terminology and skill aliases plus master roster, rig, motion, skill-motion,
skill-VFX, platform, budget, provenance, sharing, duplicate, and owner-decision
matrices. `four_realm_production_taxonomy.py --check` proves byte stability and
fails closed on orphan skills, missing cells, unriggable concepts, undocumented
provenance, unbudgeted mobile costs, incompatible duplicate IDs, or unmapped owner
questions. It authorizes no generation, activation, runtime use, or release.

`al_rig_motion_standard.json` is the versioned technical authority for coordinate,
skeleton, bind-pose, socket, facial, retarget, root-motion, layer/mask, interruption,
deformation, contact, and mobile-budget contracts. Its three representative profiles
record the measured Champion Vanguard, Covenant Sentinel, and Slagwhistle gaps without
claiming that any current source is admitted. `al_required_motion_manifest.json`
defines canonical motion keys, event payloads, all skill phases, subject floors,
anatomy exceptions, and fail-closed representative coverage. Slagwhistle remains
bounded to its six owner-authorized presentation slots; combat, defeat, and burrow
motions are explicitly blocked rather than inferred from generic fantasy-beast floors.

The character customization catalog includes body presets, hair styles, armor styles, primary/hair/skin/eye/accent palettes, face marks, weapon/offhand styles, realm material keys, and slot names so Unity and Fable tools can present the same customization choices.

The skill and weather catalog includes champion skill loadouts, realm skill VFX keys, plus detailed weather profile parameters for particles, fog, ambient light, directional light, wind, turbulence, and lightning. Skill loadouts include slot IDs, display names, cooldowns, mana costs, cast times, ranges, power values, bot damage multipliers, and VFX keys so Unity and Fable tools can balance combat from the same data without referencing UnityEngine types.

The first-session terrain catalog is a replaceable MVP physical-terrain profile.
It owns deterministic runtime heightfield, low-cost debug-grid, collision-proxy,
placement, and navigation-socket parameters. Its `futureBakeContract` seam allows
editor-baked height/slope/biome/splat data to replace the runtime procedural source
without changing the capital anchor or gameplay-facing IDs. It does not approve
final biome, terrain-material, or environment art direction.

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
  `fixtures/valid/*.json` pass while `fixtures/invalid/*.json` fail. It also runs the
  world-asset cross-record, binding, budget, gate, and byte-stability validator plus
  the realm character/creature fail-closed semantic tests.
- `generate_fixtures.py` — regenerates the fixtures (valid samples and
  one-decision-violation invalid samples).
- `world_asset_inventory.py` — deterministically assembles or validates the held
  authoritative inventory and its acceptance-evidence report.
- `test_world_asset_inventory.py` — proves two independent generations are
  byte-identical and twelve adversarial catalog mutations fail closed.
- `realm_character_taxonomy.py` — validates cross-record IDs, references, owner
  gates, complete motion matrices, entity motion coverage, and skill/VFX traces.
- `test_realm_character_taxonomy.py` — validates one complete synthetic catalog
  and proves duplicate IDs, orphan references, missing motion, trace mismatch,
  template drift, subjectless decisions, and release-gate bypass fail closed.
- `four_realm_production_taxonomy.py` — deterministically builds and validates the
  integrated four-realm owner-review surface from the four held source catalogs.
  Source fingerprints hash canonical semantic JSON, so LF/CRLF checkout policy
  cannot change the generated taxonomy.
- `test_four_realm_production_taxonomy.py` — proves all master matrices are exact
  source-derived projections and adversarial motion, skill, rig, mobile-budget,
  provenance, owner-packet, sharing, normalization, and duplicate mutations fail
  closed.
- `rig_motion_standard.py` — compiles and validates both rig/motion catalogs, resolves
  every cross-record ID, checks skeleton hierarchy/signatures, mobile budgets, event
  payloads, required motion coverage, representative source paths, and anatomy gates.
- `test_rig_motion_standard.py` — proves the committed contracts have zero acceptance
  gaps and adversarial root, parent, identifier, motion, event, budget, source,
  signature, and Slagwhistle-authorization changes fail closed.
- `model_motion_skill_vfx_harness.py` — fail-closed PASS/FAIL/BLOCKED evaluator for
  Champion, NPC, beast, and monster models plus per-skill motion/VFX axes.
- `test_model_motion_skill_vfx_harness.py` — proves missing walk/run/attack/special/
  cast-use motion or effect, omitted monster representatives, and weighted scores
  fail closed, while absent player-build evidence is BLOCKED.

Run it with:

```bash
cd unity/SharedContracts/Tests
uv run --with jsonschema validate.py
python test_world_asset_inventory.py
uv run --with jsonschema python -m unittest test_realm_character_taxonomy.py -v
uv run --with jsonschema python four_realm_production_taxonomy.py --check
uv run --with jsonschema python -m unittest test_four_realm_production_taxonomy.py -v
uv run --with jsonschema python -m unittest test_rig_motion_standard.py -v
uv run --with jsonschema python -m unittest test_model_motion_skill_vfx_harness.py -v
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
