# Economy Legacy Caller Inventory

**Tracking issue:** #163
**Inventory baseline:** `cc9f28cc2f81aeaa70125adfa4e2d34a3a7b4aa3`
**Status:** Phase 1 compatibility boundary; building construction migrated to its typed owning transaction

This record inventories every production call to the legacy `AddResource`, `ConsumeResource`, `AddCredits`, and `SpendCredits` wrappers at the #163 Phase 1 baseline. The wrappers remain only so each owning domain can migrate to a typed no-save mutation inside its own validated transaction.

## Resource wrappers

| Caller | Legacy operation | Owning migration | Required disposition |
| --- | --- | --- | --- |
| `unity/Assets/AL/Scripts/Utilities/DemoInitializer.cs` | `AddResource` | #178 / #150 | Keep test/demo-only; do not promote prototype grants into a production transaction. |
| `unity/Assets/AL/Scripts/Kingdom/Quests/LocalQuestService.cs` | `AddResource` | #152 / #133 | Apply typed resource changes inside the durable quest/report consequence transaction. |
| `unity/Assets/AL/Scripts/Kingdom/Research/LocalResearchService.cs` | `ConsumeResource` | #165 | Stage checked cost and research order, consume through the typed primitive, and persist once. |
| `unity/Assets/AL/Scripts/Services/Local/LocalTrainingService.cs` | `ConsumeResource` | #165 | Validate count/inventory/order before typed consumption and one owning commit. |

## Warzone Credit wrappers

| Caller | Legacy operation | Owning migration | Required disposition |
| --- | --- | --- | --- |
| `unity/Assets/AL/Scripts/ChampionMode/AI/BossDummyAI.cs` | `AddCredits` | #168 / #180 | Remove the exception fallback grant; only an authoritative encounter reward transaction may apply credits. |
| `unity/Assets/AL/Scripts/Kingdom/Quests/LocalQuestService.cs` | `AddCredits` | #152 / #133 | Use the typed no-save primitive inside the quest/report transaction; avoid a nested save. |
| `unity/Assets/AL/Scripts/RealmWar/Warzone/WarzoneService.cs` | `AddCredits` | #166 | Use the typed no-save primitive only after a validated, duplicate-safe ownership transition. |
| `unity/Assets/AL/Scripts/Services/Local/LocalBossLootService.cs` | `AddCredits` | #168 | Apply credits with equipment and the reward ledger in one recoverable boundary. |
| `unity/Assets/AL/Scripts/Services/Local/LocalWarmasterService.cs` | `SpendCredits` | #171 | Stage typed spend and piece-state mutation, then persist once or recover visibly. |
| `unity/Assets/AL/Scripts/Utilities/DemoInitializer.cs` | `AddCredits` | #178 / #150 | Keep test/demo-only and excluded from the ShellFoundation production profile. |

## Phase 1 compatibility rules

- Typed resource and credit primitives never save.
- Legacy resource wrappers never save.
- Legacy credit wrappers save exactly once only after an `Applied` typed mutation.
- Zero, invalid, malformed, unsupported, insufficient, overflow, unavailable, and read-only results save zero times.
- A valid amount proves arithmetic safety only; it does not prove entitlement, idempotency, or domain success.
- No new caller may be added to this inventory. New domain work must consume the typed capability interface.
- Building construction is no longer a legacy wrapper caller. It validates the
  exact next-level definition, consumes through `IResourceIntegrityService`,
  commits wallet and order state once, and rolls both back on a known save
  failure.

## Production snapshot blocker

`TickProduction` still cannot safely call the current territory income service,
and construction level alone is not yet an approved production-rate snapshot.
Building queries are now non-seeding and construction spend is typed, but
production reconnection still requires immutable revisioned building-yield and
territory-income contributions. Phase 1 therefore accepts only a bounded
atomic batch/remainder planner over supplied validated contributions and fails
closed when the authoritative provider is absent.
