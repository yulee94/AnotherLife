# World Event Source Handoff

**Packet ID:** `al_narrative_world_event_source_v001`
**Primary Codex mode:** narrative/content
**Runtime content catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_world_event_content_catalog.json`
**Related issue:** #172

## Source Intent

This packet defines the narrative/content authority for the current world-state event copy that is hard-coded in `WorldStateService`: Siege, Realm Festival, Veil Omen, and Void Corruption. It gives #172 engineering localization-ready event names, summaries, start/end/unavailable messages, notification content references, and consumer guardrails.

The packet does not implement lifecycle behavior. It does not persist events, validate duration, expire events, replace events, cancel events, deliver notifications, apply production/training/decay/enemy-strength effects, or mutate saves.

## Stable Source Rules

- World-state player-facing copy belongs to this catalog or later approved localization content.
- `WorldStateService` must not remain an independent story/localization authority after this source is consumed.
- Siege, Festival, and Void Corruption may describe narrative pressure, but they must not claim gameplay modifiers are applied until engineering verifies committed consumers.
- Veil Omen is presentation/foreshadowing source until a future contract gives it technical effects.
- Notification definition IDs are source references only until #177 delivery, acknowledgement, deduplication, persistence, and accessibility exist.
- Duration, priority, stacking, replacement, cancellation, offline expiration, and duplicate delivery remain #172 engineering work.

## Handoff Rules

Engineering should validate:

- unique lowercase event IDs;
- exact mapping from supported `WorldStateEffect` enum names to source records;
- every localization key resolves;
- requested-unverified consumers cannot announce applied effects;
- notification references remain non-delivery claims until #177;
- missing lifecycle, duration, persistence, or consumer authority fails closed with unavailable copy.

## Acceptance Status

Source status: ready for Codex coordination/review and later #172 engineering consumption.
User gate: final world-event UX, timing, balance, integrated gameplay effects, and playtest approval remain later gates.
