# Realm Content Authoring Template — Troops, Champions, Research, Skills

Status: reusable authoring template derived from the validated Stonehold slice
(kanban task `t_5749ed8f`). Companion to the Stonehold reference implementation
(`unity/Docs/Stonehold_Content_Authoring_Records.md`, the audit
`unity/Docs/Stonehold_Content_Authoring_Audit.md`, and the machine-readable slice
`unity/Docs/Narrative/GameData/stonehold-content-authoring-map.json`).

Audience: the realm-expansion workers authoring the **Eldergrove**, **Crownlands**, and
**Umbral** slices. Apply this template verbatim; do not invent names, stats, costs, or
asset references.

---

## 0. The one finding that shapes every realm

The four content families — `troops`, `champions`, `research`, `skills` — have **zero
authored production records** in the canonical six-family model. After auditing every
canonical source, the honest split is:

| Family | Authorable today | Realm relationship |
| --- | --- | --- |
| research | 8 **name-only** records | **Realm-agnostic** — identical for all four realms |
| skills | 4 **name-only** records | **Realm-agnostic** — identical for all four realms |
| troops | 0 records — explicit absence | **Realm-agnostic** — no source for *any* realm |
| champions | 0 records — explicit absence + visual precursor | **Realm-specific** — only the precursor string differs |

Consequence: the Eldergrove/Crownlands/Umbral slices are **not** new creative content.
They are the same 8 + 4 name-only records, the same troop absence, and a champion absence
that differs from Stonehold only by the realm's visual-precursor citation. Anything more
would be fabrication.

---

## 1. Non-fabrication rule (binding)

1. Every authored record must trace to a cited canonical source. Record the source path
   and exact string.
2. Player-facing names may only be copied from `verbatim_preserved` content references —
   never rewritten, case-folded, or derived from enum names, filenames, or fixtures.
3. Numeric stats, costs, durations, effects, prerequisites, behavior/presentation
   profiles, target types, and asset references are authorable only from an existing
   approved value or a separate approved balance decision. Absence is an explicit
   `blocked_required` / `not_authored_unavailable` placeholder, never a fabricated value.
4. Troops and champions have zero authored source for *every* realm; authoring them
   requires a separate creative/product + balance decision first.
5. A partial/empty artifact must not be reported as a complete production family.
6. Generated artifacts must record provenance: source commit + SHA-256 of the committed
   git-blob bytes.

---

## 2. Disposition vocabulary

| Disposition | Meaning |
| --- | --- |
| `name_only_records_mechanics_blocked` | Identity is authored (ID + `name_ref` + verbatim display name); every mechanical field is `blocked_required`. |
| `not_authored_unavailable` | No approved record or player-facing source exists; absence is represented honestly, never fabricated. |
| `blocked_required` | A field that the production schema requires but that has no approved value yet. |

---

## 3. The slice JSON shape (exact)

Each realm slice is a single JSON file. Top-level object, exact properties (order shown
is the schema order the validator enforces):

```jsonc
{
  "schemaVersion": 1,
  "sliceId": "<realm>-content-authoring-YYYY-MM-DD-vNNN",
  "realmId": "<realm>",                 // stonehold | eldergrove | crownlands | umbral
  "scope": "...",                        // free text, describe the slice
  "authority": {
    "primaryMode": "codex_narrative_content",
    "userFinalCreativeAcceptance": "pending",
    "userBalanceAcceptance": "pending",
    "runtimeAuthority": "unchanged",
    "productionEligible": false
  },
  "sources": {
    "contentMap": {
      "path": "unity/Docs/Narrative/GameData/phase-c-six-family-content-map.json",
      "sourceCommit": "963c4bc6e6db8ae2b87d363ceb229519e97f13b0",
      "gitBlobSha256": "8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353"
    },
    "technicalHandoff": "unity/Docs/GameDataCatalog/PhaseC/Phase_C_Six_Family_Technical_Handoff.md",
    "skillConvergence": "unity/Docs/GameDataCatalog/PhaseC/Phase_C8A_Skill_Authority_Convergence.md",
    "championConvergence": "unity/Docs/GameDataCatalog/PhaseC/Phase_C7A_Champion_Authority_Convergence.md",
    "audit": "unity/Docs/Stonehold_Content_Authoring_Audit.md"
  },
  "provenance": {
    "generatedArtifactsMustRecordSourceCommit": true,
    "generatedArtifactsMustRecordSourceBlobSha256": true
  },
  "nonFabricationRule": "...",          // copy the canonical rule text
  "loaderWiring": {
    "status": "blocked",
    "reason": "..."                      // copy the Stonehold reason verbatim
  },
  "families": [ /* exactly four, in this order */ ]
}
```

**File bytes:** strict UTF-8, **no BOM**, single trailing LF, no duplicate keys, no
trailing commas, no comments. The validator rejects anything else.

The `families` array must contain exactly four entries in this order:
`research`, `skills`, `troops`, `champions`. Each family object has the exact shape
`{ "family", "authoringDisposition", "records", "blockedFields", "absence" }`.

### 3.1 `research` — 8 name-only records (realm-agnostic, copy verbatim)

`authoringDisposition: "name_only_records_mechanics_blocked"`, `absence: null`,
`blockedFields: ["max_level","cost_profile_id","duration_profile_id","effect_ids","prerequisite_research_ids"]`.

Records (identical for every realm):

| id | nameRef | displayName | sourceAnchor |
| --- | --- | --- | --- |
| `steel_forging` | `research.steel_forging.name` | Steel Forging | Steel Forging |
| `plate_armor` | `research.plate_armor.name` | Plate Armor | Plate Armor |
| `masonry` | `research.advanced_masonry.name` | Advanced Masonry | Advanced Masonry |
| `irrigation` | `research.irrigation.name` | Irrigation | Irrigation |
| `ballistics` | `research.ballistics.name` | Ballistics | Ballistics |
| `logistics` | `research.logistics.name` | Logistics | Logistics |
| `trade_routes` | `research.trade_routes.name` | Trade Routes | Trade Routes |
| `arcane_study` | `research.arcane_study.name` | Arcane Study | Arcane Study |

> `masonry` is the canonical ID; its content ref is `research.advanced_masonry.name` and
> its display name is "Advanced Masonry". Do **not** introduce `advanced_masonry` as a
> competing ID.

### 3.2 `skills` — 4 name-only records (realm-agnostic, copy verbatim)

`authoringDisposition: "name_only_records_mechanics_blocked"`, `absence: null`,
`blockedFields: ["behavior_profile_id","presentation_profile_id","target_type","cooldown_seconds","power","mana_cost","cast_time_seconds","range_meters","vfx_asset_ref","audio_asset_ref"]`.

| id | nameRef | displayName | sourceAnchor |
| --- | --- | --- | --- |
| `realm_strike` | `skill.realm_strike.name` | Realm Strike | realm_strike |
| `renewing_guard` | `skill.renewing_guard.name` | Renewing Guard | renewing_guard |
| `warzone_burst` | `skill.warzone_burst.name` | Warzone Burst | warzone_burst |
| `warmaster_breaker` | `skill.warmaster_breaker.name` | Warmaster Breaker | warmaster_breaker |

### 3.3 `troops` — 0 records (realm-agnostic, copy verbatim)

`authoringDisposition: "not_authored_unavailable"`, `records: []`,
`blockedFields: ["records","localization","base_stats","training_profiles","asset_refs"]`,
`absence`:

```jsonc
{
  "disposition": "not_authored_unavailable",
  "reason": "No <realm> troop name, stat, training profile, or asset exists in any canonical source. Troops are missing required source, not optional content; authoring requires a separate creative and balance decision first.",
  "anchors": ["TroopType.Infantry", "TroopType.Cavalry", "TroopType.Ranged", "TroopType.Siege"],
  "visualPrecursor": null
}
```

### 3.4 `champions` — 0 records + realm-specific visual precursor

`authoringDisposition: "not_authored_unavailable"`, `records: []`,
`blockedFields: ["records","localization","realm_class_assignments","asset_refs","base_skill_refs","stat_profiles"]`,
`absence.anchors: []`, `absence.visualPrecursor` is the **only** realm-specific field:

| Realm | `visualPrecursor` | Blender handoff asset id (cite only, do not author) |
| --- | --- | --- |
| stonehold | `Stonehold Vanguard turnaround sheet (FourRealmChampionAnchor.md; champion-character-sheets-blender-handoff.v1.json, runtimeAuthority:false)` | `champion-stonehold-vanguard-turnaround-v001` |
| eldergrove | `Eldergrove Vanguard turnaround sheet (FourRealmChampionAnchor.md; champion-character-sheets-blender-handoff.v1.json, runtimeAuthority:false)` | `champion-eldergrove-vanguard-turnaround-v001` |
| crownlands | `Crownlands Vanguard turnaround sheet (FourRealmChampionAnchor.md; champion-character-sheets-blender-handoff.v1.json, runtimeAuthority:false)` | `champion-crownlands-vanguard-turnaround-v001` |
| umbral | `Umbral Vanguard turnaround sheet (FourRealmChampionAnchor.md; champion-character-sheets-blender-handoff.v1.json, runtimeAuthority:false)` | `champion-umbral-vanguard-turnaround-v001` |

> `Vanguard` is a `SubclassId`, not a `ClassFamily` (`warrior/mage/ranger/assassin`). Do
> **not** turn the precursor into a class assignment (C7A §4).

---

## 4. Realm substitution checklist (what actually changes per realm)

| Item | Value |
| --- | --- |
| `realmId` | `eldergrove` / `crownlands` / `umbral` |
| `sliceId` | `<realm>-content-authoring-YYYY-MM-DD-v001` |
| `scope` | reword to name the realm |
| `champions.absence.reason` | reword to name the realm |
| `champions.absence.visualPrecursor` | use the realm row from §3.4 |
| `troops.absence.reason` | reword to name the realm |

Everything else — `authority`, `sources` (content-map pin, technical handoff, C7A/C8A
convergence), `provenance`, `nonFabricationRule`, `loaderWiring`, the 8 research records,
the 4 skill records, the 4 troop anchors — is copied **verbatim** from the Stonehold slice.
`sources.audit` points at the Stonehold audit as the canonical pattern (there is no
per-realm audit yet; if one is authored later, update the pointer).

---

## 5. ID conventions

- Stable technical IDs: lowercase snake-case, regex `^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$`,
  max 128 chars, case-sensitive ordinal, no double/trailing underscores.
- Content references (player-facing text keys): dotted ASCII, e.g.
  `research.steel_forging.name`, `skill.realm_strike.name`; each segment is a canonical
  stable ID. Never derive a content ref from display text at runtime.
- Enums use lowercase canonical strings (`warrior/mage/ranger/assassin`,
  `single/aoe/self/ally/enemy`); `legacy_troop_type` uses the PascalCase legacy strings
  (`Infantry/Cavalry/Ranged/Siege`).

---

## 6. Validation procedure

From the repo root, run the two realm-agnostic Phase C baselines plus the realm slice
validator. (The Stonehold slice additionally validates against its own reference
validator.)

```text
# realm-agnostic baselines (every realm)
pwsh tools/narrative/Test-PhaseCSixFamilyContentMap.ps1
pwsh tools/game-data/Test-PhaseCSixFamilyTechnicalSource.ps1

# realm slice (use the realm's own slice path and precursor)
pwsh tools/game-data/Test-RealmContentAuthoringMap.ps1 -RealmId <realm> -SlicePath unity/Docs/Narrative/GameData/<realm>-content-authoring-map.json -ChampionVisualPrecursor "<Realm> Vanguard"

# Stonehold slice only — the reference validator it was originally authored against
pwsh tools/game-data/Test-StoneholdContentAuthoringMap.ps1
```

> `Test-StoneholdContentAuthoringMap.ps1` remains the Stonehold-specific reference
> validator; `Test-RealmContentAuthoringMap.ps1` is the realm-parameterized successor that
> every realm slice should validate against. `-ChampionVisualPrecursor` is optional — when
> set, the champion absence must name that precursor; when omitted, only non-empty is
> required.

Generation gate (must still refuse production eligibility):

```text
pwsh tools/game-data/Test-PhaseCSixFamilyTechnicalSource.ps1 -RequireProductionEligible
```

This exits nonzero by design — production emission remains blocked until the C7A/C8A
authority is lifted and the mechanics are approved.

---

## 7. Step-by-step checklist

1. Copy `unity/Docs/Narrative/GameData/stonehold-content-authoring-map.json` to
   `unity/Docs/Narrative/GameData/<realm>-content-authoring-map.json`.
2. Set `realmId` and `sliceId`; reword `scope`, `troops.absence.reason`, and
   `champions.absence.reason` to name the realm.
3. Set `champions.absence.visualPrecursor` from the §3.4 table.
4. Leave research (8), skills (4), troop anchors (4), and all `authority`/`sources`/
   `provenance`/`loaderWiring` blocks unchanged.
5. Save as UTF-8 without BOM, ending in a single LF, with no trailing commas or comments.
   The `unity/.gitattributes` wildcard `*-content-authoring-map.json` already pins every
   realm slice to `unity-json` (`eol=lf`), so no per-file `.gitattributes` edit is needed —
   verify with `git check-attr unity-json -- unity/Docs/Narrative/GameData/<realm>-content-authoring-map.json`.
6. Run the validators in §6. All must PASS (the generation gate must still refuse).
7. Commit with a message naming the realm and the verbatim/non-fabrication basis.
8. Record the provenance of any source you cite (source commit + git-blob SHA-256).
9. Do **not** wire a loader, do **not** emit a production catalog, and do **not** mark
   `productionEligible` true — those are blocked by C7A/C8A and the generation gate.

---

## 8. Where the authoritative details live

- Field requirements per family, balance constraints, and cross-family dependency edges:
  `unity/Docs/Stonehold_Content_Authoring_Audit.md` §3–§6.
- Canonical record shape: `unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataSixFamilySchemas.cs`.
- Content map (verbatim strings): `unity/Docs/Narrative/GameData/phase-c-six-family-content-map.json`.
- Champion visual precursors (all four realms): `unity/Assets/AL/Art/Designs/FourRealmChampionAnchor.md` and `unity/Docs/champion-character-sheets-blender-handoff.v1.json`.
- The reference implementation to copy from: `unity/Docs/Narrative/GameData/stonehold-content-authoring-map.json`.
