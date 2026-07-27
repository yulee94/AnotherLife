# World Atlas Source Handoff

**Packet ID:** `al_narrative_world_atlas_source_v001`
**Primary Codex mode:** narrative/content
**Runtime content catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json`
**Related issue:** #181

## Source Intent

This packet extracts the first stable narrative/world-atlas anchors from the four-realm launch catalog, `OMEN_1`, and the complete main quest line source. It gives engineering a compact catalog of location and objective IDs to validate later without turning narrative copy into runtime mutation authority.

The packet is deliberately conservative: every scene reference and objective hook remains `requested`. It does not claim terrain, scene loading, territory ownership, realm gem custody, warzone scoring, neutral-zone PvP enforcement, or Dragon's Concordance behavior is implemented.

## Stable Zone Set

- Four committed-realm inner zones: Crownlands, Stonehold, Eldergrove, and Umbral.
- Four outer-warzone main-gate zones, one per realm.
- One shared bridge crossroads zone.
- One neutral center-island zone: `zone_accordant_isle`.
- One requested NVS-01 anomaly marker: `zone_sky_castle_marker`.

## Stable Objective Set

- `objective_realm_main_gate_defense`
- `objective_crossroads_control`
- `objective_warzone_save_pillar`
- `objective_eight_gem_custody`
- `objective_accordant_isle_concordance`

All objective entries are source anchors only. They must be implemented later through duplicate-safe engineering contracts before they can mutate save, territory, scoring, gem, or PvP state.

## Handoff Rules

Engineering should validate:

- unique lowercase snake-case zone and objective IDs;
- `realmId` values against `al_realm_catalog` or the reserved `shared` marker;
- every display and summary key against `draftLocalization`;
- requested hook status before exposing actionable runtime controls;
- no query mutates territory, resources, gems, quests, scenes, saves, or PvP state.

## Acceptance Status

Source status: ready for Codex coordination/review and later #181 engineering consumption.
User gate: final map presentation, zone layouts, PvP enforcement feel, objective tuning, and integrated world readability remain later user approval gates.
