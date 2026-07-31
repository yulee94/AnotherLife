# World-State Caller and Interface Inventory

**Status date:** 2026-07-31
**Tracking issue:** #172
**Audited baseline:** `d015938bf0f939069b54ceb852f576c5dc682e47`
**Primary mode:** Codex engineering
**Phase:** pure definition/lifecycle/effect planning only

## Current production surface

| Surface | Current location | Verified use on the audited baseline |
| --- | --- | --- |
| `WorldStateEffect` legacy enum | `unity/Assets/AL/Scripts/Core/Interfaces/IWorldStateService.cs` | Read and written only by the legacy `WorldStateService`; it is not definition or balance authority. |
| `IWorldStateService.CurrentEffect` | same interface | Implemented by `WorldStateService`; no verified external reader. |
| `IWorldStateService.ActiveEventId` | same interface | Implemented by `WorldStateService`; no verified external reader. |
| `IWorldStateService.TriggerStateChange(...)` | same interface | Implemented by `WorldStateService`; no verified production caller. |
| `IWorldStateService.OnWorldStateChanged` | same interface | Raised by `WorldStateService`; no verified subscriber. |
| `WorldStateService.GetProductionMultiplier()` | `unity/Assets/AL/Scripts/Kingdom/Narrative/WorldStateService.cs` | No verified caller. Resource production does not consume it. |
| `WorldStateService` construction | `unity/Assets/AL/Scripts/Core/Bootloader.cs` | One instance is constructed with save and notification services and registered as `IWorldStateService`. |
| Save dependency | `WorldStateService` constructor field | Injected but unused; no world-state data is persisted. |
| Notification dependency | `WorldStateService.TriggerStateChange(...)` | Formats raw English/rich text and calls the obsolete raw notification wrapper before raising the legacy event. This is prototype behavior, not committed delivery. |

Repository-wide exact-symbol search found no other runtime caller, subscriber, production multiplier consumer, lifecycle tick/reconcile driver, save mapping, or world-state persistence field.

## Phase B additive surface

The pure planner lives under:

```text
unity/Assets/AL/Scripts/Core/Interfaces/WorldState/
unity/Assets/AL/Scripts/Services/WorldState/
```

It adds immutable definitions, effects, instances, snapshots, requests, plans, receipts, diagnostics, notification intents, and post-commit event descriptions. It also adds strict validators, an injected UTC clock/definition resolver, an effect-consumer registry, deterministic start/end/cancel/reconcile planning, and an isolated-candidate effect-application seam.

Nothing in this phase is registered in `Bootloader`, referenced by the legacy service, connected to a save, connected to `INotificationService`, or connected to a real gameplay consumer. Notification intents and post-commit events are immutable plan data only; this phase neither enqueues nor publishes them.

## Preserved migration boundary

The following prototype behavior remains intentionally unchanged in this PR:

- enum/string/float `IWorldStateService` compatibility surface;
- in-memory `WorldStateService` fields;
- hard-coded raw prototype notification copy;
- `GetProductionMultiplier()`;
- the existing `Bootloader` construction and service registration.

Those surfaces may change only in the later save-backed integration phase after #137, #153, #177, approved #183 definition/effect authority, and required consumer contracts are accepted. No new caller should use the legacy API.

## Verification queries

```text
IWorldStateService
WorldStateService
WorldStateEffect
TriggerStateChange
OnWorldStateChanged
GetProductionMultiplier
CurrentEffect
ActiveEventId
```

The inventory must be refreshed from current `main` before the later service-integration phase. A merge, issue close, or source-only catalog change does not by itself prove a production caller, effect consumer, persistence path, or visible notification delivery exists.

## Impact statement

The Phase B implementation is platform-neutral managed code with bounded collection copies and no per-frame work, asset, dependency, scene, Player resource, package, or Android change. It is dormant unless a later caller explicitly constructs it. Runtime memory, frame time, build/install size, and device behavior are therefore unchanged in production for this phase; Player/profiler/device measurements are not applicable until integration.
