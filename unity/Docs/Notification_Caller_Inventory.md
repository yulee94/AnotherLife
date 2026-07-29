# Notification Caller Inventory

**Tracking issue/spec:** #177, `unity/Docs/Notification_Delivery_Contract_Spec.md`
**Inventory baseline:** `e535f8232169d73e018bbaf512f2fb2057917b83`
**Status date:** 2026-07-29
**Status:** Phase B typed contract and bounded session queue; production caller migration is intentionally deferred

This record inventories every production call to the compatibility-only
`ShowMessage`, `ShowError`, and `ShowResourceGain` wrappers at the Phase B
baseline. The wrappers remain obsolete compile-compatibility seams only. New
code must submit a typed `NotificationRequest` through `Enqueue`.

## Raw compatibility callers

| Wrapper | Production caller | Exact call sites | Owning migration | Phase B disposition |
| --- | --- | ---: | --- | --- |
| `ShowMessage` | `Kingdom/Narrative/WorldStateService.cs` | 1 | #172 | Retain unchanged. Migrate only with the world-state owning transaction and approved notification definition/content source. |
| `ShowMessage` | `Services/Local/LocalBossLootService.cs` | 3 | #168 | Retain unchanged. Migrate only with the boss-reward owning transaction and approved notification definition/content source. |
| `ShowError` | zero production callers | 0 | #177 follow-up | Remove only after the compatibility window and downstream implementation checks permit it. |
| `ShowResourceGain` | zero production callers | 0 | #177 follow-up | Remove only after the compatibility window and downstream implementation checks permit it. |

Interface declarations and the `LocalNotificationService` wrapper
implementations are contract boundaries, not production caller sites.

## Phase B rules

- Raw wrappers emit `AL-NTF-LEGACY-RAW`, escape technical text, and never enter
  the typed queue or claim a `Presented` receipt.
- Raw wrappers create no durable history.
- No new production caller may be added to this inventory.
- `WorldStateService` and `LocalBossLootService` remain unchanged in Phase B.
- Typed production caller migration is deferred until the owning issue and an
  approved definition/content source are ready.
- The focused EditMode suite scans production source and fails if these exact
  paths or call counts change, or if `ShowError`/`ShowResourceGain` gain a
  production caller.

## Runtime and optimization impact

The inventory itself has no runtime, memory, build-size, install-size, or device
compatibility impact. The Phase B queue remains session-only and bounded to 64
records; it adds no save data, scene object, UI asset, Android dependency, or
player-facing content.
