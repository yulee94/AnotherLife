# Territory Source and Caller Inventory

**Status date:** 2026-07-16
**Owner mode:** Codex engineering
**Related issue/spec:** #166, `unity/Docs/Territory_Ownership_Income_Transaction_Spec.md`
**Branch:** `codex/territory-contract-planner`
**Scope:** Current-source inventory only. No production behavior, save schema, catalog, scene, Android, narrative, territory balance, or UI behavior is changed by this document.

## Purpose

This inventory records the current territory definition-like sources, persisted fields, production callers, and future migration boundaries for the pure territory contract/planner phase. It is not a territory catalog and does not authorize production service migration by itself.

## Current Territory Values

The current hard-coded baseline is embedded in `WarzoneService.EnsureTerritories()` and mirrored by the pure planner as the current migration inventory.

| ID | Current display name | Initial owner | Bonus resource | Bonus amount | Unit | Fortress |
| --- | --- | --- | --- | ---: | --- | --- |
| `T1` | `Iron Peaks` | `Stonehold` | `Stone` | 50 | units/minute | yes |
| `T2` | `Silver Woods` | `Eldergrove` | `Wood` | 40 | units/minute | no |
| `T3` | `Golden Plains` | `Crownlands` | `Gold` | 20 | units/minute | no |
| `T4` | `Shadow Vale` | `Umbral` | `Food` | 60 | units/minute | yes |
| `T5` | `Neutral Borderlands` | `None` | `Gold` | 10 | units/minute | no |

This PR does not change any ID, owner, resource, amount, fortress flag, or reward value.

## Definition-Like Sources

| Path | Current role | Classification |
| --- | --- | --- |
| `unity/Assets/AL/Scripts/RealmWar/Warzone/WarzoneService.cs` | Seeds T1-T5 definition-like rows into the live save when the territory list is null/empty. | Legacy embedded source; production behavior unchanged by this PR. |
| `unity/Assets/AL/Scripts/Core/Interfaces/ITerritoryService.cs` | Defines mutable `TerritoryData` with ID, display name, owner, bonus, and fortress fields. | Legacy save/service contract; production behavior unchanged by this PR. |
| `unity/Assets/AL/Scripts/RealmWar/World/LocalWorldAtlasService.cs` | Defines world/warzone objective display-like data and passive credit weights separately from `TerritoryData`. | Related world-map source, not a #166 territory catalog. |
| `unity/Assets/AL/Scripts/RealmWar/Territories/Contracts/TerritoryContractPlanner.cs` | Adds immutable current-baseline definitions, state records, query snapshots, capture plans, and income snapshots for the pure phase. | Nonmutating planner/inventory mirror; not wired into production services. |

## Persisted Territory Shape

Current persisted rows use `TerritoryData`:

```text
Id
Name
OwnerRealm
BonusType
BonusAmount
IsFortress
```

Known risks preserved for later phases:

- display and balance-like values are saved per profile;
- null/blank/duplicate rows can exist after deserialization or manual corruption;
- unknown future rows cannot be distinguished from malformed rows without a catalog/compatibility pass;
- no ownership revision, schema version, migration marker, operation ledger, or capture outbox exists yet.

No save field is added or changed by this PR.

## Production Query and Mutation Callers

| Caller | Method/path | Current behavior | Pure planner relevance |
| --- | --- | --- | --- |
| `WarzoneService.GetTerritories()` | `ITerritoryService.GetTerritories()` | Seeds T1-T5 on null/empty and returns mutable `TerritoryData` rows. | Later service migration should return immutable query results or safe unavailable state. |
| `WarzoneService.CaptureTerritory(string, RealmId)` | `ITerritoryService.CaptureTerritory` | Selects first matching row, overwrites owner, emits event, tries quest progress and +100 Warzone Credits, then saves. | Pure planner now models validation, same-owner no-op, stale checks, authorization, and reward deltas without mutation. |
| `WarzoneService.CalculatePassiveIncome(ResourceType)` | `ITerritoryService.CalculatePassiveIncome` | Reads selected realm and sums matching mutable rows. | Pure planner now models one immutable income snapshot with supported current rows only. |
| `LocalResourceService.AddTerritoryIncome(double)` | Calls `CalculatePassiveIncome` once per supported resource. | Consumes six independent territory reads during production. | Later #163/#166 integration should consume one snapshot per tick. |
| `DemoInitializer` | `CaptureTerritory("T5", realm)` | Development/demo direct capture command. | Remains outside this pure planner. Release command containment is owned by #178. |
| `KingdomSceneController` | Reads territory/warzone state for status surfaces. | UI/controller behavior remains unchanged. | Later UI migration should consume immutable query/unavailable status. |

## Event, Quest, Credit, and Notification Boundaries

| Boundary | Current source | Current risk | Pure phase status |
| --- | --- | --- | --- |
| Capture event | `WarzoneService.OnTerritoryCaptured` | Emits before save and without previous owner/revision identity. | Planner result carries previous/new owner and revision fields, but emits no event. |
| Quest progress | `IQuestService.UpdateProgress(QuestType.CaptureTerritory, 1)` | Nested side effect inside broad `try/catch`. | Planner records `QuestProgressDelta = 1` only for planned real transitions. |
| Warzone Credits | `IWarzoneCreditService.AddCredits(100)` | Nested side effect and compatibility save behavior. | Planner records `WarzoneCreditsDelta = 100` only for planned real transitions. |
| Notifications/outbox | Not currently part of territory capture. | No durable event/outbox identity yet. | Deferred to later #137/#177-backed phases. |

## Shared-File Boundary

No shared-file lock is required for this pure phase.

Untouched shared files:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

## Current Pure Planner Coverage

The new planner/test slice covers:

- exact current T1-T5 inventory;
- immutable query snapshots;
- duplicate known territory rejection without first-row fallback;
- unknown future territory preservation and exclusion from supported income;
- same-owner capture no-op with zero mutation/reward fields;
- neutral capture planning with one revision increment, +100 Warzone Credits, and +1 quest progress;
- stale owner rejection before reward planning;
- `RealmId.None` and missing authorization rejection;
- single-revision passive-income snapshot for current owned totals.

## Deferred Work

Still deferred to later phases:

- authoritative #183 territory catalog artifact;
- save schema/migration and capture ledger after #137;
- production `ITerritoryService`/`WarzoneService` migration;
- no-save quest/economy candidate transaction integration;
- notification/event outbox integration;
- live/offline production consuming one territory-income snapshot;
- Kingdom UI unavailable-state migration;
- PlayMode/Player evidence and user acceptance.
