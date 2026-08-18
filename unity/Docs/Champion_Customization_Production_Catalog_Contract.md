# Champion Customization Production Catalog and Stable-ID Contract

Status: **approved contract; production activation blocked**

Decision date: 2026-08-17

Issues: #184; dependencies #183, #137, #450, and #180

Machine-readable authority: `unity/Docs/Champion_Customization_Production_Catalog_Contract.v1.json`

## Decision

This document approves the authoritative identity, compatibility, default, and source contract for Champion customization. It does **not** approve the current legacy JSON as a production runtime artifact and does not make any entry runtime-selectable. The required production model, per-entry asset references, and immutable model-capability mapping do not exist in approved form on current `main`.

Failing closed is the approval decision: downstream code may build against these stable IDs and rules, but must expose `CatalogUnavailable`/`UnavailableMissingCapability` and must not show a mutation control until the referenced machine contract marks that exact entry `runtimeSelectable: true` with a non-null approved `productionAssetRef`.

PR #504 is explicitly source-navigation evidence only. Its five character sheets are not a `.blend`, FBX, rig, prefab, model-capability snapshot, or per-option runtime asset mapping.

## Pinned sources and dispositions

| Source | Pin | Disposition |
| --- | --- | --- |
| Technical catalog | `unity/Assets/AL/StreamingAssets/GameData/al_character_customization_catalog.json` version `0.5.0`, SHA-256 `3c0e265d947fa0e62c3042a4614a2dd50cdb36ee8e0272071ca2d241fdc8ab24` | Approved only as exact compatibility/migration input. Its IDs, values, ordering, and preset composition are frozen; its legacy envelope, raw English, realms, and disputed quality targets are not production authority. |
| Content catalog | `unity/Assets/AL/StreamingAssets/GameData/al_character_customization_content_catalog.json` version `0.1.0`, SHA-256 `ced64c0d9cba02d4e24d73984fbb814f928e476bf2c01a0c3bef54f11b78c844` | Approved presentation-key/source packet. Draft English and integrated release presentation remain under the user gate. |
| Integrity specification | `unity/Docs/Champion_Customization_Integrity_Spec.md` | Binding technical behavior and sequencing. |
| Pure planner | merged commit `e0cbf6c1845489be6bf1032bb8c4d3a8e6dc7103` | Binding dormant technical contracts; no source, save, or real-model activation. |
| Blender handoff | merged commit `2383b61a57f999e59ba4298a7378ea27163e5027` | Navigation/provenance only; specifically not production runtime completion. |

## Authoritative entry set

The machine contract contains all 91 entries one-to-one with source paths, content keys where approved, order, defaults, required stable capability IDs, production asset reference, eligibility, and blocker.

| Family | Kind | Entries | Runtime-selectable now |
| --- | --- | ---: | ---: |
| `flags` | flag | 2 | 0 |
| `armor_styles` | option | 6 | 0 |
| `body_presets` | option | 9 | 0 |
| `face_marks` | option | 9 | 0 |
| `hair_styles` | option | 5 | 0 |
| `offhand_styles` | option | 5 | 0 |
| `weapon_styles` | option | 5 | 0 |
| `accent_colors` | palette | 8 | 0 |
| `eye_colors` | palette | 8 | 0 |
| `hair_colors` | palette | 8 | 0 |
| `primary_colors` | palette | 9 | 0 |
| `skin_colors` | palette | 8 | 0 |
| `forge_presets` | preset | 9 | 0 |

There are **zero** runtime-selectable production entries. No placeholder, procedural object name, generated material, concept sheet, test fixture, or hard-coded controller fallback is substituted for an approved production asset.

## Stable IDs and defaults

IDs are case-sensitive byte identity. They are never trimmed, lowercased, inferred from display text, asset/object names, order, or nearest values. Contract v001 defines no alias, rename, or removal. Unknown nonblank future IDs remain exact in raw committed state and may receive only a typed presentation placeholder; that placeholder is never saved as the user's choice.

Existing defaults are preserved exactly:

```text
body=average; hair=short; armor=realm_basic; face=none
weapon=sword; offhand=shield
primary=(0.20,0.40,1.00); hair=(0.08,0.06,0.04)
skin=(0.72,0.56,0.42); eye=(0.25,0.58,0.92)
accent=(0.85,0.62,0.18); cape=true; helmet=false
```

Defaults are compatibility values, not fallback authority. Catalog/model unavailability never authorizes normalization or persistence of these values over existing state.

## Compatibility and migration contract

- A metadata-free old save is `ValidLegacyNoMetadata` only if every exact raw field validates; metadata may be added only by an explicit successful #137 transaction.
- Current direct finite RGB triples are durable authority. Palette IDs are selectable conveniences/provenance; nearest-color inference is forbidden.
- An explicit edit replaces only its field mask. Presets are draft-only and must identify every replaced field, including preserved-unknown replacement.
- Catalog/model/hash/revision mismatch is `StalePlan`; no silent rebase or partial apply.
- No v001 aliases or destructive migrations are approved. Future aliases require a versioned record and the confirmation policy from the integrity spec.
- Unsupported future schema/state remains preserved and unavailable; it is never downgraded.

## Dependency assumptions and unresolved prerequisites

1. **Production assets/model:** unresolved. A final model/prefab hash plus one concrete asset/capability mapping for every selectable option, flag, and material channel is required. Current `AL_ModularChampion_Base.prefab`, generated materials, procedural builder names, and concept PNGs are prototype/design evidence only.
2. **Body-scale policy:** unresolved. Preserve all nine existing vectors, but none may activate until source-owned bounds and model compatibility are approved.
3. **#183 catalog envelope:** unresolved. Production requires stable `gameId`, `catalogSetId`, `catalogId=character_customization`, `familyId=champion_customization`, schema/content/source revisions, raw hash, requiredness, and packaged path. The legacy `game: Another Life` field is insufficient.
4. **#137/#450 persistence authority:** unresolved. Production commit waits for profile-bound write authority, backward-compatible metadata, candidate persist/verify/publish, and recovery/rollback. This contract does not touch locked save paths.
5. **#180 boundary:** customization is appearance only. It cannot supply Champion identity, records, class/realm assignment, skills, stats, combat, or encounter authority.
6. **User gate:** final visual fidelity, body range, copy, integrated Player/device playtest, product, balance, milestone, and release remain unapproved.

## Downstream activation rule

A loader may publish only a whole validated snapshot matching the pinned/approved successor contract. A preview or Player adapter must reject any entry unless its exact contract row has `runtimeSelectable: true`, a non-null approved `productionAssetRef`, and a current immutable capability mapping. Failure is deterministic and non-mutating; no production fallback is allowed.

This contract is concrete enough for runtime and persistence work to implement IDs, defaults, preserved-unknown behavior, statuses, revisions, and transaction boundaries without inventing content. It intentionally blocks visible selection and persistence until the missing production authority is supplied.
