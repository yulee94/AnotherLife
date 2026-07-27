# Economy Legacy Caller Inventory

**Tracking issue:** #163
**Inventory baseline:** `cc9f28cc2f81aeaa70125adfa4e2d34a3a7b4aa3`
**Status:** Phase 1 compatibility boundary; no listed caller is authorized as a new reward source

This record inventories every production call to the legacy `AddResource`, `ConsumeResource`, `AddCredits`, and `SpendCredits` wrappers at the #163 Phase 1 baseline. The wrappers remain only so each owning domain can migrate to a typed no-save mutation inside its own validated transaction.

## Resource wrappers

| Caller | Legacy operation | Owning migration | Required disposition |
| --- | --- | --- | --- |
| `unity/Assets/AL/Scripts/Utilities/DemoInitializer.cs` | `AddResource` | #178 / #150 | Keep test/demo-only; do not promote prototype grants into a production transaction. |
| `unity/Assets/AL/Scripts/Kingdom/Quests/LocalQuestService.cs` | `AddResource` | #152 / #133 | Apply typed resource changes inside the durable quest/report consequence transaction. |
| `unity/Assets/AL/Scripts/Services/Local/LocalBuildingService.cs` | `ConsumeResource` | #165 | Stage checked cost and building order, consume through the typed primitive, and persist once. |
| `unity/Assets/AL/Scripts/Kingdom/Research/LocalResearchService.cs` | `ConsumeResource` | #165 | Stage checked cost and research order, consume through the typed primitive, and persist once. |
| `unity/Assets/AL/Scripts/Services/Local/LocalTrainingService.cs` | `ConsumeResource` | #165 | Validate count/inventory/order before typed consumption and one owning commit. |

## Warzone Credit wrappers

| Caller | Legacy operation | Owning migration | Required disposition |
| --- | --- | --- | --- |
| `unity/Assets/AL/Scripts/Kingdom/Quests/LocalQuestService.cs` | `AddCredits` | #152 / #133 | Use the typed no-save primitive inside the quest/report transaction; avoid a nested save. |
| `unity/Assets/AL/Scripts/RealmWar/Warzone/WarzoneService.cs` | `AddCredits` | #166 | Use the typed no-save primitive only after a validated, duplicate-safe ownership transition. |
| `unity/Assets/AL/Scripts/Services/Local/LocalWarmasterService.cs` | `SpendCredits` | #171 | Stage typed spend and piece-state mutation, then persist once or recover visibly. |
| `unity/Assets/AL/Scripts/Utilities/DemoInitializer.cs` | `AddCredits` | #178 / #150 | Keep test/demo-only and excluded from the ShellFoundation production profile. |

`#168` removed the BossDummyAI fallback credit grant and the LocalBossLootService nested `AddCredits` call. Boss loot now uses the typed no-save credit primitive inside its reward ledger transaction and persists once after credits, equipment, and the applied-result identity are prepared.

## Phase 1 compatibility rules

- Typed resource and credit primitives never save.
- Legacy resource wrappers never save.
- Legacy credit wrappers save exactly once only after an `Applied` typed mutation.
- Zero, invalid, malformed, unsupported, insufficient, overflow, unavailable, and read-only results save zero times.
- A valid amount proves arithmetic safety only; it does not prove entitlement, idempotency, or domain success.
- No new caller may be added to this inventory. New domain work must consume the typed capability interface.

## Production snapshot blocker

`TickProduction` cannot safely call the current building and territory services. Current building queries create/default rows, current territory income queries seed fallback territories, live/offline rates conflict, and newer #165/#166 contracts require immutable revisioned snapshots. Phase 1 therefore accepts only a bounded atomic batch/remainder planner over supplied validated contributions and fails closed when the authoritative provider is absent. #137 must supply the stable save-profile identity, while #183 source approval, #165 building production snapshots, #166 territory income snapshots, and the later #163 integration phase own reconnection.
