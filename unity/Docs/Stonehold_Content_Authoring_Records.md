# Stonehold Content Authoring Records — Troops, Champions, Research, Skills

Status: authored content records for the Stonehold slice (kanban task `t_0c117db9`).
Sibling audit: `unity/Docs/Stonehold_Content_Authoring_Audit.md` (task `t_277fb06c`).
Machine-readable authority: `unity/Docs/Narrative/GameData/stonehold-content-authoring-map.json`.

This document resolves the "zero authored records" state for the four families by
authoring exactly what the canonical source supports and marking everything else as an
explicit placeholder. It authors **no runtime code**, **no production catalog**, and
**no balance value**. Nothing is invented.

---

## 0. Summary of what was authored

| Family | Authored | Placeholder policy |
| --- | ---: | --- |
| research | 8 name-only records (canonical ID + `name_ref` + verbatim display name) | `max_level`, `cost_profile_id`, `duration_profile_id`, `effect_ids`, `prerequisite_research_ids` = `blocked_required` |
| skills | 4 name-only records (canonical ID + `name_ref` + verbatim display name) | behavior/presentation/target/numerics/vfx/audio fields = `blocked_required` |
| troops | 0 records — explicit `not_authored_unavailable` absence | `records`, `localization`, `base_stats`, `training_profiles`, `asset_refs` = `blocked_required` |
| champions | 0 records — explicit `not_authored_unavailable` absence (visual precursor cited) | `records`, `localization`, `realm_class_assignments`, `asset_refs`, `base_skill_refs`, `stat_profiles` = `blocked_required` |

The previous zero-authored state is resolved in the only way the source authority permits:
research and skills identities are now formally authored as traceable records; troops and
champions are now an **explicit** absence rather than an implicit gap.

---

## 1. Content authority and non-fabrication rule

Governing sources (paths relative to repo root):

- `unity/Docs/Narrative/GameData/phase-c-six-family-content-map.json` — authoritative
  machine-readable content map; 35 content references, exact English strings.
- `unity/Docs/GameDataCatalog/PhaseC/Phase_C_Six_Family_Technical_Handoff.md` — canonical
  technical IDs (incl. research), absence policy, generation refusal.
- `unity/Docs/GameDataCatalog/PhaseC/Phase_C8A_Skill_Authority_Convergence.md` — the four
  skill identities/names are preserved; every behavior/balance field is blocked.
- `unity/Docs/GameDataCatalog/PhaseC/Phase_C7A_Champion_Authority_Convergence.md` — the
  champion family has zero records; the Stonehold Vanguard is a visual precursor only.
- `unity/Docs/Stonehold_Content_Authoring_Audit.md` — the parent audit mapping.

Rules applied (audit §9):

1. Every authored record traces to a cited canonical source; the path and exact string are
   recorded.
2. Player-facing names are copied from `verbatim_preserved` content references only. They
   are not rewritten, case-folded, or derived from enum names, display strings, filenames,
   or fixtures.
3. Numeric stats, costs, durations, effects, prerequisites, behavior/presentation profiles,
   target types, and asset references are authorable only from an approved value or a
   separate approved balance decision. Absence is an explicit placeholder, never a
   fabricated value.
4. Troops and champions currently have zero authored source; authoring them requires a
   separate creative/product + balance decision first.

---

## 2. Research — 8 name-only records

All eight are realm-agnostic (no Stonehold-specific research exists in any source).
Canonical IDs are fixed by the technical handoff §Research; names and `name_ref` are
verbatim from the content map.

| Canonical ID | `name_ref` | Display name (verbatim) | Source |
| --- | --- | --- | --- |
| `steel_forging` | `research.steel_forging.name` | Steel Forging | content map; technical handoff §Research |
| `plate_armor` | `research.plate_armor.name` | Plate Armor | same |
| `masonry` | `research.advanced_masonry.name` | Advanced Masonry | same (canonical `masonry`; `advanced_masonry` is NOT introduced) |
| `irrigation` | `research.irrigation.name` | Irrigation | same |
| `ballistics` | `research.ballistics.name` | Ballistics | same |
| `logistics` | `research.logistics.name` | Logistics | same |
| `trade_routes` | `research.trade_routes.name` | Trade Routes | same |
| `arcane_study` | `research.arcane_study.name` | Arcane Study | same |

Blocked mechanics (placeholder `blocked_required`, no approved source): `max_level`,
`cost_profile_id`, `duration_profile_id`, `effect_ids`, `prerequisite_research_ids`.

Note on `masonry`: the canonical technical ID is `masonry` (it preserves the stable Android
ID), while the player-facing name is "Advanced Masonry" and the content reference is
`research.advanced_masonry.name`. `advanced_masonry` must not be introduced as a competing
canonical ID (technical handoff §Research).

---

## 3. Skills — 4 name-only records

All four are realm-agnostic. Canonical IDs and names are verbatim from the content map and
C8A §3.

| Canonical ID | `name_ref` | Display name (verbatim) | Source |
| --- | --- | --- | --- |
| `realm_strike` | `skill.realm_strike.name` | Realm Strike | content map; C8A §3 |
| `renewing_guard` | `skill.renewing_guard.name` | Renewing Guard | same |
| `warzone_burst` | `skill.warzone_burst.name` | Warzone Burst | same |
| `warmaster_breaker` | `skill.warmaster_breaker.name` | Warmaster Breaker | same |

Blocked mechanics (placeholder `blocked_required`, no approved source): `behavior_profile_id`,
`presentation_profile_id`, `target_type`, `cooldown_seconds`, `power`, `mana_cost`,
`cast_time_seconds`, `range_meters`, `vfx_asset_ref`, `audio_asset_ref`.

The nine-field rows observed in `al_skill_weather_catalog.json` / `SkillCaster` are exact
migration evidence only and are **not** copied into production records (C8A §4).

---

## 4. Troops — explicit unavailable absence (0 records)

No Stonehold troop name, stat, training profile, or asset exists in any canonical source.
The four `TroopType` enum identities are `not_authored_unavailable`.

| Anchor | Disposition | Source |
| --- | --- | --- |
| `TroopType.Infantry` / `Cavalry` / `Ranged` / `Siege` | `not_authored_unavailable` | content map §troops; technical handoff §Troops and champions |

Action taken: an explicit absence marker is authored in the content-authoring map. No empty
`troops` artifact is emitted as `required: false`, and no name is invented from enum labels
or simulator values (technical handoff §Requiredness and absence).

---

## 5. Champions — explicit unavailable absence (0 records)

No Stonehold champion identity (name, realm/class assignment, portrait/model reference,
base-skill list, stat profile) is defensible from current source.

| Precursor (not a record) | Type | Source |
| --- | --- | --- |
| "Stonehold Vanguard" turnaround sheet | visual-only concept (`runtimeAuthority: false`) | `FourRealmChampionAnchor.md`; `champion-character-sheets-blender-handoff.v1.json` |
| `starterClassBias: [vanguard, warden, dreadknight]` | realm starting-hook hint, not a record | `al_realm_catalog.json` (Stonehold `startingHooks`) |

Action taken: an explicit absence marker citing the visual precursor is authored. `Vanguard`
is a `SubclassId`, not a `ClassFamily` value, so it is not turned into a class assignment
(C7A §4).

---

## 6. Loader wiring status — blocked

"Wire records into the existing Stonehold loaders" cannot be performed without violating the
source authority:

- The six-family registry `GameDataSixFamilySchemas.CreateRegistry()` has no caller and the
  strict schema requires full concrete records (empty-record arrays are rejected; placeholder
  fields are not representable).
- `LocalGameDataService`, `SkillCaster`, and `SkillLoadoutCatalog` are explicitly listed as
  non-editable in C7A/C8A ("does not edit ...").
- Production catalog emission is refused by the generation gate
  (`Test-PhaseCSixFamilyTechnicalSource.ps1 -RequireProductionEligible` exits nonzero).

The authored records are therefore delivered as a content-authoring source map (the same
pattern as `phase-c-six-family-content-map.json`), which a future Phase D loader can consume
once the blocked mechanics and the creative/balance decisions are resolved.

---

## 7. Provenance

| Source | Source commit | Raw SHA-256 (lower-case) |
| --- | --- | --- |
| `phase-c-six-family-content-map.json` | `963c4bc6e6db8ae2b87d363ceb229519e97f13b0` | `8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353` |

The content map's pinned commit and blob SHA-256 are verified by the existing
`Test-PhaseCSixFamilyTechnicalSource.ps1` (which passes), and the verbatim strings in this
slice are cross-checked against the live content map by the slice validator below.

---

## 8. Validation

```text
pwsh tools/narrative/Test-PhaseCSixFamilyContentMap.ps1
pwsh tools/game-data/Test-PhaseCSixFamilyTechnicalSource.ps1
pwsh tools/game-data/Test-StoneholdContentAuthoringMap.ps1
```

The first two are the existing Phase C baselines (both pass). The third validates this slice:
strict UTF-8 JSON (no BOM, single LF terminator, no duplicate keys), the exact four-family
shape, the 8 + 4 records with verbatim `name_ref`/display-name cross-checked against the live
content map, the canonical-ID sets, the placeholder-flag dispositions, and the zero-record
troops/champions absence.

---

## 9. Honest coverage metric

This authoring run read, end-to-end (not via subagent summaries): the parent audit,
`phase-c-six-family-content-map.json`, `phase-c-six-family-technical-source-v003.json`,
`Phase_C_Six_Family_Technical_Handoff.md`, `Phase_C7A_Champion_Authority_Convergence.md`,
`Phase_C8A_Skill_Authority_Convergence.md`, `GameDataSixFamilySchemas.cs`,
`GameDataCatalogSchema.cs`, `GameDataCatalogModels.cs` (records/manifest/envelope models),
`GameDataCatalogValidator.cs` (manifest + family/record validation path),
`LocalGameDataService.cs`, `Game_Data_Contract_Decisions.md`, `al_realm_catalog.json`, and
both existing validation scripts.

Not read (out of critical path for these four families): `GameDataCatalogValidator.cs`
lines 1301–2072 (cross-reference/alias tail), `GameDataCatalogLoading.cs`, `GameDataCatalogStore.cs`,
`GameDataCatalogSources.cs`, the Terrestrials ecosystem packets, and the Android Kotlin shell.
These do not change the authored records; the record shape and validation contract were read
directly from the schema and validator code plus the binding specs.
