# Game Data Contract Decisions

**Status:** Authoritative canonical decisions for the six known data-contract blockers.
**Owning task:** t_f363bde5 (Resolve the six data-contract blockers with canonical mappings).
**Downstream consumer:** SharedContracts JSON Schema authoring (t_459bd668).
**Date:** 2026-08-18

This document resolves the six data-contract blockers named in the
"Data-driven authority — single catalog source of truth" decomposition. Each
section picks one canonical representation, states its JSON Schema type/format/
unit/ID casing, and documents the legacy-to-canonical mapping. The decisions are
concrete enough to author the ten SharedContracts JSON Schemas without further
ambiguity.

The governing rules applied throughout are from
`Game_Data_Catalog_Authority_Spec.md` §5 (Stable ID and alias policy) and the
already-committed "SixFamily" C# catalog schemas
(`unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/`):

- New technical IDs use **lowercase snake_case**:
  `^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$`.
- IDs are ordinal/case-sensitive strings; no runtime case-folding or guessing.
- Legacy IDs are never silently renamed; they are classified
  `canonical unchanged | canonicalized through migration | legacy alias retained |
  invalid/unapproved consumer reference` and resolved through an explicit alias
  table (`legacyId`, `canonicalId`, `introducedVersion`, `retirementVersion`,
  `migrationIssue`).
- Enum-backed identities (`RealmId`, `TroopType`, `ResourceType`, …) keep their
  PascalCase C# enum names at runtime; JSON catalog records use a lowercase
  stable string ID plus an explicit legacy enum name/value pair.

---

## Decision 1 — ManaShrine / Mine are not canonical buildings

### Current state
- The canonical building authority
  (`GameDataBuildingProgressionRegistry`, 15 buildings, lowercase stable IDs) does
  **not** contain `mana_shrine` or `mine`.
- `ManaShrine` and `Mine` are PascalCase consumer lookup strings referenced by
  `KingdomSceneController`, `KingdomCommandPolicy`, `LocalResourceService`,
  `CityLayoutEngine`, and `KingdomBuildingLayout`, with no definition behind them.
- `KingdomCommandPolicy` already renders both as `UnsupportedBuild`.

### Canonical decision
`mana_shrine` and `mine` are **invalid/unapproved consumer references** and are
**not** building IDs. The buildings catalog contains exactly 15 records:

| # | Canonical `id` | Legacy `legacy_building_id` |
| ---: | --- | --- |
| 1 | `town_hall` | `TownHall` |
| 2 | `farm` | `Farm` |
| 3 | `lumber_mill` | `LumberMill` |
| 4 | `quarry` | `Quarry` |
| 5 | `gold_mine` | `GoldMine` |
| 6 | `barracks` | `Barracks` |
| 7 | `academy` | `Academy` |
| 8 | `market` | `Market` |
| 9 | `storehouse` | `Storehouse` |
| 10 | `forge` | `Forge` |
| 11 | `stable` | `Stable` |
| 12 | `workshop` | `Workshop` |
| 13 | `embassy` | `Embassy` |
| 14 | `wall` | `Wall` |
| 15 | `watchtower` | `Watchtower` |

Consumers that request `mana_shrine` or `mine` must receive an
unavailable/invalid-definition result (never an invented definition, value, max
level, name, or localization key). This matches the existing `UnsupportedBuild`
treatment in `KingdomCommandPolicy`.

### JSON Schema contract
- Buildings `id` is a string whose `enum` is exactly the 15 lowercase IDs above.
  `mana_shrine` and `mine` fail validation. No schema field, default, or enum
  value invents them.
- `legacy_building_id` (PascalCase) is retained only as the legacy enum for
  alias resolution; it never admits `ManaShrine` or `Mine`.

### Legacy-to-canonical mapping
| Legacy | Classification | Canonical |
| --- | --- | --- |
| `ManaShrine` | invalid/unapproved consumer reference | none (reject) |
| `Mine` | invalid/unapproved consumer reference | none (reject) |

`Mine` is additionally ambiguous (a generic mine vs `gold_mine`); it is **not**
aliased to `gold_mine`. If narrative/content later approves either concept, it
enters as a new canonical lowercase ID with a reviewed definition, not as a
resurrected legacy string.

### Rationale
The inventory (`Game_Data_Source_Inventory.md`) and the building authority both
confirm these two IDs have consumers but no definition. Inventing records would
violate "no silent fallback / no improvised content" (authority spec §2 items
5-6, 12). Keeping them rejected makes the schema unambiguous and forces the
authoring decision back to narrative/content ownership.

---

## Decision 2 — Warzone Credits are non-negative integers (int32)

### Current state
Warzone Credits are represented in three numeric shapes across the codebase:

| Shape | Where |
| --- | --- |
| `int` (int32) | `SaveGameData.WarzoneCredits`; `IWarzoneCreditService` (`GetCredits`/`AddCredits`/`SpendCredits`); `BattleRewardProposal.Credits`; `BossRewardApplicationPlanner.WarzoneCredits`/`MaximumWarzoneCredits`; `TerritoryCapturePlan.WarzoneCreditsDelta`; `BossDummyAI._warzoneCreditReward` |
| `long` / `long?` (int64) | `EconomyBalanceReadResult.Balance`; `EconomyMutationResult.RequestedAmount`/`PreviousBalance`/`CurrentBalance` |
| `float` / `double` | never for credits; floats appear only transiently in battle power computation and `double` only in resource production rates. `Battle_Computation_Result_Transaction_Spec.md` states "binary floating-point is not the authority at the computation boundary" and "no binary-float authority in computation." |

### Canonical decision
Warzone Credits is a **whole, non-negative integer currency**. The canonical
stored balance and all operation deltas are **`int` (int32)**.

- JSON Schema type: `"type": "integer"`, `"minimum": 0`.
- No `float`/`double`/`number` schema form is permitted for credits; fractional
  credits are invalid and reject.

### JSON Schema contract
- Any field named `warzone_credits`, `credits`, `warzone_credits_delta`,
  `win_credits_base`, `loss_credits_base`, `power_credit_*`, `rounds_credit_*`,
  or `maximum_warzone_credits` is `"type": "integer"`, `"minimum": 0` (base/divisor
  fields additionally `"minimum": 0`; `power_credit_maximum` and
  `rounds_credit_maximum` are `"minimum": 0`; no fractional form).
- The economy **result envelope** (`Balance`, `PreviousBalance`,
  `CurrentBalance`, `RequestedAmount`) may be modelled as int64 **only** in the
  read/mutation result envelope, because it is a widening container for checked
  arithmetic and overflow diagnostics. It is **not** a second currency type and
  must not appear as the canonical currency field in a catalog or save schema.

### Legacy-to-canonical mapping
- All existing `int` credit fields are already canonical — no numeric migration.
- `long`/`long?` result-envelope fields stay int64 envelopes; the currency value
  they carry is still integral and non-negative. Schema authors model the
  **currency** as integer; they may model the **result envelope** as int64 for
  overflow safety.
- Any `float`/`double` credit value is a defect: reject, do not round.

### Rationale
The stored balance, service interface, battle proposal, boss reward, and
territory-capture delta are all `int`. The only `long` usage is the economy
result envelope's widened fields (checked addition/subtraction with overflow
diagnostics in `LocalWarzoneCreditService`). Floats are explicitly non-authority
at the battle boundary. Declaring integer as canonical removes the type
ambiguity the schema author would otherwise face.

---

## Decision 3 — Territory income is authored per minute (integer)

### Current state
- `TerritoryData.BonusAmount` (runtime save) and
  `TerritoryDefinition.BonusAmount` (contract) are `long` with **no time unit**.
- `TerritoryIncomeContribution.AmountPerMinute` is `long`, explicitly per minute.
- `WarzoneService.CalculatePassiveIncome` sums `BonusAmount` into `long`, unit
  unlabelled (and currently has no production caller).
- `EconomyProductionContribution.Amount` is `double` per `deltaSeconds` — a
  runtime per-second production path.
- `WorldObjectiveData.PassiveCreditWeight` is `float` — a world-atlas objective
  **sort weight**, unrelated to territory income.

### Canonical decision
Territory income is authored and stored as a **whole, non-negative PER-MINUTE
integer**.

- Canonical JSON field name: `income_per_minute`.
- JSON Schema type: `"type": "integer"`, `"minimum": 0`, with an explicit unit
  annotation — use either a named field (`income_per_minute`) or a paired
  `"unit": "per minute"` constant. Prefer the named field so the unit is
  self-documenting.
- Per-second rates are **derived** at runtime (divide by 60) by the production/
  tick engine and are **never stored** as authored definition data. Fractional
  per-second accumulation is a runtime remainder concern (cf.
  `CombatantResourcePlanner`'s checked whole-second + remainder pattern), not a
  definition concern.

### JSON Schema contract
- Territory definition: `income_per_minute` = `"type": "integer"`,
  `"minimum": 0`.
- Territory income contribution snapshot: `amount_per_minute` = integer,
  `"minimum": 0` (the existing `AmountPerMinute` is already canonical naming).
- `passive_credit_weight` (world atlas objective) is a **separate** float sort
  weight and must not be merged into or renamed as territory income.

### Legacy-to-canonical mapping
| Legacy field | Interpretation | Canonical |
| --- | --- | --- |
| `TerritoryData.BonusAmount` / `TerritoryDefinition.BonusAmount` (T1..T5 = 50, 40, 20, 60, 10) | already per-minute intent | `income_per_minute` (same integer) |
| `TerritoryIncomeContribution.AmountPerMinute` | already canonical naming | `amount_per_minute` (unchanged) |
| `CalculatePassiveIncome` return | sum of per-minute contributions | derived total per minute |
| `EconomyProductionContribution.Amount` (double) | runtime per-second fractional production | not a definition field |
| `WorldObjectiveData.PassiveCreditWeight` (float) | objective sort weight, not income | keep separate |

### Rationale
The contract layer already names the quantity `AmountPerMinute`; the runtime
save `BonusAmount` is unlabelled but holds the same per-minute values. Choosing
per-minute integer as the single authored unit removes the min-vs-sec ambiguity
while leaving fractional per-second accrual to the runtime tick engine.

---

## Decision 4 — Eight gems, two per realm (lowercase canonical IDs)

### Current state
- `GemState` stores eight PascalCase booleans: `Stonehold_Heart_Gem`,
  `Stonehold_Fortress_Gem`, `Eldergrove_Heart_Gem`, `Eldergrove_Glade_Gem`,
  `Crownlands_Heart_Gem`, `Crownlands_Capital_Gem`, `Umbral_Heart_Gem`,
  `Umbral_Void_Gem`.
- `LocalWorldAtlasService` uses the same PascalCase IDs as inner-realm objective
  IDs.
- The narrative content authority
  (`al_realm_gem_wishgate_content_catalog.json` + `al_realm_catalog.json`
  `realmGemIds`) already re-authored the gems into **eight lowercase IDs, two
  per realm**, with new names.

### Canonical decision
Canonical gem IDs are the eight lowercase snake_case IDs from the narrative
content catalog, exactly two per realm:

| Realm (`realm_id`) | Gem 1 (`index: 1`) | Gem 2 (`index: 2`) |
| --- | --- | --- |
| `crownlands` | `gem_crownlands_sun` (Sun Gem) | `gem_crownlands_oath` (Oath Gem) |
| `stonehold` | `gem_stonehold_forge` (Forge Gem) | `gem_stonehold_depth` (Depth Gem) |
| `eldergrove` | `gem_eldergrove_root` (Root Gem) | `gem_eldergrove_moon` (Moon Gem) |
| `umbral` | `gem_umbral_veil` (Veil Gem) | `gem_umbral_ember` (Ember Gem) |

### JSON Schema contract
- Gem `id`: string, `enum` of the eight IDs above (or pattern
  `^gem_[a-z][a-z0-9_]*$` plus an `enum`).
- Gem `realm_id`: lowercase realm stable ID (see Decision 5), one of
  `crownlands` / `stonehold` / `eldergrove` / `umbral`.
- Gem `index`: `"type": "integer"`, `"minimum": 1`, `"maximum": 2`.
- Realm catalog `realm_gem_ids`: an array of exactly 2 strings, each resolving
  to a gem whose `realm_id` matches the owning realm. Duplicate or cross-realm
  references reject.

### Legacy-to-canonical mapping
The narrative content author renamed the gems, so the only stable correspondence
between the legacy PascalCase IDs and the canonical IDs is **realm + ordinal
index** (the order each realm lists them). Name-based inference is prohibited.

| Realm | Legacy index 1 → canonical | Legacy index 2 → canonical |
| --- | --- | --- |
| Stonehold | `Stonehold_Heart_Gem` → `gem_stonehold_forge` | `Stonehold_Fortress_Gem` → `gem_stonehold_depth` |
| Eldergrove | `Eldergrove_Heart_Gem` → `gem_eldergrove_root` | `Eldergrove_Glade_Gem` → `gem_eldergrove_moon` |
| Crownlands | `Crownlands_Heart_Gem` → `gem_crownlands_sun` | `Crownlands_Capital_Gem` → `gem_crownlands_oath` |
| Umbral | `Umbral_Heart_Gem` → `gem_umbral_veil` | `Umbral_Void_Gem` → `gem_umbral_ember` |

Migration rules:
- `GemState` (eight PascalCase bools) is a legacy save shape. Save migration
  must translate collected flags into canonical `RealmGemState` rows
  (`GemId` = canonical ID, `HomeRealm` = realm, `GemIndex` = 1..2) using the
  index table above — not by string name.
- `RealmGemState.GemId` (string) must be canonicalized to the eight lowercase
  IDs. Legacy PascalCase `GemId` values are aliases resolved by the same table.
- World-atlas objective IDs referencing legacy gem IDs must be updated to the
  canonical gem IDs (or carry them as legacy aliases) in a later world-atlas
  migration; they are presentation/objective keys, not the gem authority.

### Rationale
The narrative content catalog (`al_realm_gem_wishgate_content_catalog.json`) and
`al_realm_catalog.json` `realmGemIds` are already the committed 2-per-realm
authority with lowercase IDs. The legacy `GemState`/world-atlas PascalCase IDs
pre-date that re-authoring; the index mapping is the deterministic bridge.

---

## Decision 5 — Realm IDs: lowercase snake_case canonical, PascalCase/uppercase legacy

### Current state
Four case variants of realm identity exist:

| Casing | Where |
| --- | --- |
| PascalCase C# enum | `RealmId { None, Stonehold, Eldergrove, Crownlands, Umbral }` (`Enums.cs`) |
| lowercase canonical | SixFamily registry, NVS v003 `eligibleRealmIds`, `al_realm_catalog.json` `id`, `RealmCatalogRuntime`, `GameDataRealmReferences.StableId` |
| PascalCase legacy | `GameDataRealmReferences.LegacyRealmName`, `al_realm_catalog.json` `legacyRuntimeId`, `GameDataSixFamilySchemas` `legacy_realm_id` |
| UPPERCASE legacy | NVS v002 packet (`CROWNLANDS`…), main-quest-line packet `realmVariants[].realmId` (`STONEHOLD`…) |

### Canonical decision
The canonical JSON data ID for a realm is **lowercase snake_case**, in the
committed order:

```
crownlands, stonehold, eldergrove, umbral
```

The PascalCase C# `RealmId` enum remains the runtime enum. The mapping is exact
ordinal (no case-folding):

| C# enum (`RealmId`) | canonical JSON `id` |
| --- | --- |
| `Crownlands` (3) | `crownlands` |
| `Stonehold` (1) | `stonehold` |
| `Eldergrove` (2) | `eldergrove` |
| `Umbral` (4) | `umbral` |
| `None` (0) | not a valid committed realm |

### JSON Schema contract
- Realm `id`: string, `enum` `["crownlands","stonehold","eldergrove","umbral"]`
  (or pattern `^[a-z][a-z0-9_]*$` plus the enum). PascalCase and UPPERCASE
  strings are **not** accepted as canonical `id` values.
- Optional legacy fields `legacy_realm_id` (string) and `legacy_realm_value`
  (integer 1..4) may carry the PascalCase enum name and numeric value for alias
  resolution only (already modelled in `GameDataSixFamilySchemas`).

### Legacy-to-canonical mapping
| Legacy | Classification | Canonical |
| --- | --- | --- |
| `Stonehold`, `Eldergrove`, `Crownlands`, `Umbral` (PascalCase enum / `legacyRuntimeId` / `legacy_realm_id`) | legacy alias retained | `stonehold`, `eldergrove`, `crownlands`, `umbral` |
| `STONEHOLD`, `ELDERGROVE`, `CROWNLANDS`, `UMBRAL` (NVS v002 + main-quest `realmVariants[].realmId`) | legacy alias retained (dev-only) | same as above |
| `None` / numeric `0` | not a committed realm | reject where a committed realm is required |

Known source drift to normalize (not a schema exception): the main-quest-line
packet `realmVariants[].realmId` uses UPPERCASE (`STONEHOLD`). When the chapter
catalog is built, those values must be emitted in lowercase canonical form;
the uppercase value is a legacy source casing, not a second canonical form.

### Rationale
Lowercase snake_case is already the committed canonical form in three independent
authorities (SixFamily registry, NVS v003, `al_realm_catalog.json`). PascalCase
and UPPERCASE are historical artifact casings of the same four identities. One
canonical casing plus an explicit legacy alias pair is sufficient and
unambiguous.

---

## Decision 6 — Chapter-1 ID reconciliation

### Current state
Chapter-1 identity exists in several incompatible families:

| Family | IDs |
| --- | --- |
| Legacy generated chapters | `C1_SH`, `C1_EG`, `C1_CL`, `C1_UM` (plus C2..C12 variants and `C_OMEN`) |
| Save default | `C1` (blank `CurrentChapterId` normalized to `C1`) |
| NVS markers | `CH1_REALM_INTRO` (external capability), `UNLOCK_REALM_CHAPTER_1` (consequence), `POST_REALM_PROLOGUE` |
| Main-quest-line packet | `CH01_PROOF_OF_WORTH` (chapter `id`), `MQ_C1_PROOF_OF_WORTH` (main quest), `MAIN_QUEST_CH01_PROOF_OF_WORTH` (component), `legacyChapterReferences: ["C1_SH","C1_EG","C1_CL","C1_UM"]` |
| Kotlin | `CH0_PROLOGUE`, `CH2_THE_TREASURE_HUNT` |

### Canonical decision
- **Canonical Chapter-1 ID** = `ch01_proof_of_worth` (lowercase snake_case; the
  source packet's `CH01_PROOF_OF_WORTH` lowercased per the global ID convention,
  and matching localization key `chapter.ch01_proof_of_worth.title`).
- **Realm-variant Chapter-1 IDs** = `ch01_<realm>`:
  `ch01_stonehold`, `ch01_eldergrove`, `ch01_crownlands`, `ch01_umbral`.
- The generic legacy `C1` is **not** a canonical chapter ID. It is a
  save-normalization default meaning "chapter marker unset"; it resolves to
  `ch01_<realm>` once the committed realm is known.
- `CH1_REALM_INTRO` is an **external capability / unlock marker**, not a chapter
  record; it must not appear in the chapter catalog `id` namespace.

### JSON Schema contract
- Chapter `id`: string, pattern `^[a-z][a-z0-9_]*$`.
- Chapter `realm_id`: optional lowercase realm stable ID (Decision 5). Realm
  variants carry it; the realm-agnostic `ch01_proof_of_worth` may omit it.
- Chapter `order`: `"type": "integer"`, `"minimum": 1`. Chapter 1 is `order: 1`.
- `legacy_chapter_references`: optional array of legacy IDs (the source packet
  already declares `["C1_SH","C1_EG","C1_CL","C1_UM"]`).

### Legacy-to-canonical mapping
| Legacy | Classification | Canonical |
| --- | --- | --- |
| `C1_SH` | legacy alias retained | `ch01_stonehold` |
| `C1_EG` | legacy alias retained | `ch01_eldergrove` |
| `C1_CL` | legacy alias retained | `ch01_crownlands` |
| `C1_UM` | legacy alias retained | `ch01_umbral` |
| `C1` | save default (chapter marker unset) | `ch01_<realm>` at commit time; never a standalone canonical chapter |
| `CH01_PROOF_OF_WORTH` | legacy source casing | `ch01_proof_of_worth` |
| `CH1_REALM_INTRO` | capability/unlock marker | not a chapter |
| `CH0_PROLOGUE`, `C_OMEN`, `CH2_THE_TREASURE_HUNT`, `POST_REALM_PROLOGUE` | other chapters / prologue | out of Chapter-1 scope; reconcile separately |

### Rationale
The main-quest-line packet (`ANOTHERLIFE_MAIN_QUEST_LINE`) is the retained
Chapter-1 source and already declares the legacy `C1_*` references. The global
lowercase-snake convention plus the existing localization key
(`chapter.ch01_proof_of_worth.title`) make `ch01_proof_of_worth` the unambiguous
canonical ID, with the four `ch01_<realm>` variants and an explicit alias table
for the legacy `C1`/`C1_*`/`CH01_PROOF_OF_WORTH` forms.

---

## Summary table

| # | Blocker | Canonical decision | JSON Schema |
| --- | --- | --- | --- |
| 1 | ManaShrine / Mine | not buildings; reject | `id` enum = 15 lowercase IDs; `mana_shrine`/`mine` absent |
| 2 | Warzone Credits type | non-negative int32 | `"type":"integer"`, `"minimum":0` |
| 3 | Territory income unit | per minute | `income_per_minute` integer, `"minimum":0` |
| 4 | Gems | 8 gems, 2/realm, lowercase | `id` enum of 8 `gem_*`; `realm_id` lowercase; `index` 1..2 |
| 5 | Realm ID casing | lowercase snake_case | `id` enum of 4 lowercase IDs |
| 6 | Chapter-1 ID | `ch01_proof_of_worth` + `ch01_<realm>` | `id` lowercase; `realm_id` optional; alias table for `C1_*` |
