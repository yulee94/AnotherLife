# Notification Caller Inventory

**Tracking issue/spec:** #177, `unity/Docs/Notification_Delivery_Contract_Spec.md`
**Inventory baseline:** current main after durable history/outbox slice
**Status date:** 2026-09-04
**Status:** Durable history/outbox storage plus bounded catalog-backed publisher migration

This record inventories every production call to the compatibility-only
`ShowMessage`, `ShowError`, and `ShowResourceGain` wrappers. The wrappers remain
obsolete compile-compatibility seams only. New code must submit a typed
`NotificationRequest` through `Enqueue`.

## Raw compatibility callers

| Wrapper | Production caller | Exact call sites | Owning migration | Current disposition |
| --- | --- | ---: | --- | --- |
| `ShowMessage` | zero production callers | 0 | #177 | Migrated. `LocalBossLootService` now publishes `al_notify_reward_committed` through `BossLootCatalogNotificationPublisher`. |
| `ShowError` | zero production callers | 0 | #177 follow-up | Remove only after the compatibility window and downstream implementation checks permit it. |
| `ShowResourceGain` | zero production callers | 0 | #177 follow-up | Remove only after the compatibility window and downstream implementation checks permit it. |

Interface declarations and the `LocalNotificationService` wrapper
implementations are contract boundaries, not production caller sites.

## Typed catalog-backed publishers in this slice

- `LocalBossLootService` → `BossLootCatalogNotificationPublisher` (`al_notify_reward_committed`, source `al_source_boss_loot`). Raw player names are not included.
- `CatalogBackedWorldStateNotificationOutbox` maps authored start/end intents to `al_notify_world_event_started` / `al_notify_world_event_ended`. Unknown definitions including `al_notify_world_event_cancelled` fail closed.

## Rules

- Raw wrappers emit `AL-NTF-LEGACY-RAW`, escape technical text, and never enter
  the typed queue, durable history, or claim a `Presented` receipt.
- No new production caller may be added to the raw wrappers.
- Durable enqueue requires an injected durable store; without it, definitions
  remain `RejectedDurabilityUnavailable`.
- Production catalog activation remains behind #183; typed requests still fail
  closed when the default resolver is unavailable.
- The focused EditMode suite scans production source and fails if
  `ShowMessage`/`ShowError`/`ShowResourceGain` gain a production caller.

## Runtime and optimization impact

Durable history is an optional schema-v2 save extension. Missing legacy saves
admit an empty outbox/history. Retention is 100 completed records plus all
unacknowledged required records. No Android, scene, or player-facing copy
changes belong to this inventory.
