# World-State Caller and Interface Inventory

**Status date:** 2026-09-04
**Tracking issue:** #172
**Primary mode:** Codex engineering
**Phase:** Veil Omen save-backed start/end/cancel/reconcile

## Current production surface

| Surface | Current location | Verified use |
| --- | --- | --- |
| `WorldStateEffect` legacy enum | `unity/Assets/AL/Scripts/Core/Interfaces/IWorldStateService.cs` | Legacy compatibility only. |
| `IWorldStateService` | same interface | Registered by `Bootloader`; `TriggerStateChange` no longer mutates or announces unverified events. |
| `WorldStateService.GetProductionMultiplier()` | `unity/Assets/AL/Scripts/Kingdom/Narrative/WorldStateService.cs` | Always `1.0`; it does not fabricate production effects. |
| `WorldStateDurableService` | `unity/Assets/AL/Scripts/Services/WorldState/WorldStateDurableService.cs` | Save-backed exact-once start/end/cancel/reconcile for `al_world_event_veil_omen`. |
| Authored definition | `WorldStateAuthoredCatalog` | Veil Omen is presentation-only. Siege/Festival/Corruption stay unresolved. |
| Save field | `SaveGameData.WorldState` | Optional schema-v2 extension. Missing legacy/schema-2 saves load as no active event. |

Hard-coded English/rich-text copy and raw `ShowMessage` have been removed from `WorldStateService`. Committed start/end notifications use `al_notify_world_event_started` / `al_notify_world_event_ended` after persist/verify. Cancel stages typed `al_notify_world_event_cancelled` as planner intent only; player-facing cancel delivery stays blocked until #177 authors that definition.

Cancel/reconcile persist exactly once with rollback on persistence failure. Reconcile before the committed end instant is no-change. Reconcile at/after the end instant is a natural end. Unauthorized cancel sources reject without persisting.

## Remaining (successor)

Bootloader/NVS caller migration off `TriggerStateChange`, remaining authored events and real gameplay consumers, durable notification history (#177), Player/device/playtest evidence, and final user acceptance. Issue #172 stays open.
