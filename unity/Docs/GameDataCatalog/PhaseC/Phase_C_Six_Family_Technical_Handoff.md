# Phase C Six-Family Technical Handoff

## Status and authority

- Candidate ID: `game-data-phase-c-six-family-technical-source-2026-07-23-v001`
- Primary mode: Codex engineering
- Upstream source-mode merge: `963c4bc6e6db8ae2b87d363ceb229519e97f13b0` (PR #266)
- Binding issue: #183
- Runtime authority: unchanged
- Production eligibility: blocked
- User creative, balance, product-activation, and release approval: pending

This handoff defines strict production record shape and exact technical mappings that can be established from current evidence. It does not create a production catalog, approve a balance value, or authorize a runtime consumer. The machine-readable authority for this handoff is `phase-c-six-family-technical-source.json`; this document explains its boundary.

## Source and provenance

The technical map consumes:

- `unity/Docs/Narrative/GameData/Phase_C_Six_Family_Source_Packet.md`
- `unity/Docs/Narrative/GameData/phase-c-six-family-content-map.json`

The upstream content map is pinned to its committed Git-blob bytes at the PR #266 merge:

```text
SHA-256: 8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353
```

`tools/game-data/Test-PhaseCSixFamilyTechnicalSource.ps1` reads the blob through Git, hashes the exact bytes, rejects a working-source drift, and cross-checks all 35 content references.

## Requiredness and absence

All six families are required for an eventual production catalog set.

| Family | Current mappings | Production disposition | Current query meaning |
| --- | ---: | --- | --- |
| Realms | 4 | Blocked required | No production artifact |
| Buildings | 15 | Blocked required | No production artifact |
| Research | 8 | Blocked required | No production artifact |
| Troops | 0 | Blocked required | `CatalogUnavailable` |
| Champions | 0 | Blocked required | `CatalogUnavailable` |
| Skills | 4 | Blocked required | No production artifact |

Troops and champions are missing required source, not optional product content. They must not be declared `required: false`, emitted as empty artifacts, or represented by placeholder records. `OptionalAbsent` remains unavailable without a later explicit product decision and a reviewed no-content consumer path.

## Exact mapping decisions

### Realms

`stonehold`, `eldergrove`, `crownlands`, and `umbral` map exactly to `RealmId` values 1–4. `RealmId.None` is not a record or fallback. The current rare-resource enum anchors are retained as evidence only; production resource IDs, capability profiles, and asset references remain blocked.

### Buildings

The 15 current definitions use lower-snake canonical IDs and exact case-sensitive PascalCase aliases. Every observed current `max_level` is retained as 10 for migration evidence, but the generic schema uses only a storage-safe positive range; the reviewed production maximum remains blocked rather than being inferred as user-approved balance. `ManaShrine` and `Mine` remain unavailable anchors, not definitions or aliases.

### Research

The technical canonical IDs are:

```text
steel_forging
plate_armor
masonry
irrigation
ballistics
logistics
trade_routes
arcane_study
```

`masonry` preserves the existing stable Android ID. `Advanced Masonry` remains the exact player-facing source and exact legacy display alias; `advanced_masonry` is not introduced as a competing canonical ID. The other display strings are also exact aliases. No case folding, punctuation stripping, fuzzy matching, or normalization-based alias is permitted.

Maximum levels, costs, durations, effects, and prerequisite edges remain unavailable. Current state rows and generic service formulas are not promoted into definition or balance authority.

### Troops and champions

The four `TroopType` enum anchors are retained as unavailable evidence with zero production mappings. Simulator aggregate power and training formulas are not base attack/defense definitions.

No champion record ID, name, realm/class assignment, model, portrait, base-skill list, or stat profile is defensible from current source. Procedural names, customization presets, and enum members are not champion definitions.

### Skills

The four exact IDs and their current nine-field observed rows are retained:

```text
realm_strike
renewing_guard
warzone_burst
warmaster_breaker
```

Legacy slot, role, cooldown, mana, cast time, range, power, bot multiplier, and VFX key are migration evidence only. They do not establish a reviewed required/optional slot policy or complete behavior, presentation, target, audio, asset-address, or accepted-balance authority. Slot IDs and VFX keys must not become skill IDs.

## Production schemas

`GameDataSixFamilySchemas` registers six version-1 schemas in the isolated `AL.GameDataCatalog` assembly. Every schema rejects an empty record array and requires its production fields:

- realms: exact legacy enum name/value, content references, rare-resource ID, capability profiles, and asset reference;
- buildings: content reference, bounded max level, production/cost/duration profiles, and asset reference;
- research: content reference, bounded max level, cost/duration profiles, effect IDs, and prerequisite research IDs;
- troops: exact legacy enum name/value, content reference, base stats, training profile, and asset reference;
- champions: content reference, realm/class mapping, portrait/model references, base-skill references, and stat profile;
- skills: content reference, behavior/presentation profiles, target type, bounded numeric fields, and VFX/audio references.

The registry has no loader registration, records, file access, JSON parser, UnityEngine reference, or service consumer. Exact enum pairing, record counts, aliases, blocker parity, source provenance, and prerequisite DAG policy stay in the external validation/generation boundary because the generic Phase B field-rule model cannot express them all.

## Generation refusal

Phase C2 contains no production writer. The strict validator requires:

- `productionEligible: false`;
- all six families `requiredForProduction: true` and `artifactDisposition: blocked_required`;
- 31 exact mappings and 35 unique source references;
- six explicit unavailable anchors;
- zero troop and champion mappings;
- 32 deterministic blockers, including unresolved building max-level review and required/optional skill-slot policy;
- pending user approvals and unchanged runtime authority;
- zero output paths and no production `StreamingAssets` or `Resources` manifest.

Running with `-RequireProductionEligible` validates the source first and then exits nonzero with a deterministic refusal. It creates no directory or file.

## Optimization impact

- No catalog JSON, texture, mesh, audio, VFX, scene, `StreamingAssets`, or `Resources` bytes are added to a Player.
- The only runtime-assembly addition is a small, non-wired static schema factory. Referencing the type initializes one bounded family-order collection; registry objects allocate only when `CreateRegistry()` is called. There is no recurring or frame-loop allocation, and unused code remains eligible for managed-code stripping.
- No frame loop, polling, cache, fallback object creation, network call, package dependency, save field, or device-quality behavior is introduced.
- Expected frame time, render cost, steady-state memory residency, loading, and mobile/PC compatibility are unchanged while the registry stays unwired. A possible small managed-binary delta is not measured in this phase and must be reported with a Player build before activated catalog acceptance.

## Validation and next phase

Required Phase C2 evidence:

```text
pwsh -NoProfile -File tools/game-data/Test-PhaseCSixFamilyTechnicalSource.ps1
pwsh -NoProfile -File tools/game-data/Test-PhaseCSixFamilyTechnicalSource.ps1 -RequireProductionEligible
```

The first command must pass. The second must refuse production eligibility without writes. Focused and full EditMode tests must verify the schema contract when Unity licensing is available; any unavailable run remains explicitly blocked evidence rather than an inferred pass.

Phase C3 may emit a non-wired production catalog set only after all required family fields, cross-family references, assets/profiles, balance decisions, and user approval are resolved. Phase D alone may claim the `LocalGameDataService.cs` soft lock and switch runtime authority. #183 remains open through consumer migration and packaging evidence.
