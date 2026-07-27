# Notification Content Source Handoff

**Packet ID:** `al_narrative_notification_content_source_v001`
**Primary Codex mode:** narrative/content
**Runtime content catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_notification_content_catalog.json`
**Related issue:** #177

## Source Intent

This packet supplies the first notification definition/content source for the typed notification pipeline described in `unity/Docs/Notification_Delivery_Contract_Spec.md`.

It gives stable notification IDs approved draft titles, body copy, action labels, source labels, parameter names, and privacy guardrails. It does not implement the queue, visible UI, save outbox, caller migration, Android bridge behavior, or notification delivery.

## Source Rules

- Definition IDs are technical identity and not final player copy.
- Callers must pass typed parameters; arbitrary raw strings, rich text, local paths, stack traces, emails, tokens, and internal IDs are not player-facing content.
- Blocking save/catalog failures require acknowledgement in the future presenter.
- Durable notification history remains blocked until #137 and the save/outbox contract exist.
- Low-level services should not format success/failure prose directly; owning committed-result orchestrators publish later through typed definitions.

## Handoff

Engineering should validate:

- unique definition IDs, source IDs, and action IDs;
- `al_notify_*` ID format;
- definition source references;
- localization key coverage;
- body placeholders against declared `parameterNames`;
- acknowledgement requirements for blocking definitions;
- no release fallback to raw keys or internal IDs.

## Acceptance Status

Source status: ready for Codex coordination/review and later #177 engineering consumption.
User gate: final notification tone, UI placement, acknowledgement UX, and release copy remain later approval gates.
