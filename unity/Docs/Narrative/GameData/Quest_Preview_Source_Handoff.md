# Quest Preview Source Handoff

**Packet ID:** `al_narrative_quest_preview_source_v001`
**Primary Codex mode:** narrative/content
**Runtime content catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_quest_preview_content_catalog.json`
**Related issue:** #186

## Source Intent

This packet defines the approved narrative/content boundary for Android quest preview work. It consumes the approved `OMEN_1` A1 catalog and records which labels, actions, marker names, reward summaries, and unavailable states may be presented later.

The packet is deliberately non-authoritative for progression. It does not accept quests, launch Unity, claim rewards, mutate saves, emit notifications, or expose the Quest route in production.

## Stable Preview Rules

- `OMEN_1` is the only approved quest preview entry in this packet.
- `Deploy Champion` is the approved action label for the Sky Castle handoff; generic `Start Story` is not approved release copy.
- The 500 Gold reward is a report-conclusion consequence from `DLG_OMEN_1_REPORT_CONCLUSION`, not a generic manual claim button.
- `Sky Castle Anomaly` is the player-facing marker label; internal marker IDs such as `SKY_CASTLE` remain debug-only.
- `OMEN_2` and the current Android `Q1` through `Q4` simulation rows remain legacy demo rows with no release authority.
- Invalid or unavailable runtime quest snapshots must show visible nonmutating status instead of clamped or misleading progress.

## Handoff Rules

Engineering should validate:

- unique quest preview IDs;
- every `availableActions` reference resolves to an action;
- every display, summary, objective, reward, title, and description key resolves in this catalog or the approved `OMEN_1` catalog;
- `OMEN_1` source version remains `omen1-a1-2026-07-22-v002`;
- prohibited actions stay hidden in release;
- Android simulation rows cannot become authoritative quest content.

Any Android implementation should consume this source as presentation/availability guidance only. Runtime state, idempotent actions, result contracts, persistence, notifications, navigation exposure, and Unity handoff remain Codex engineering work under #186 and its dependencies.

## Acceptance Status

Source status: ready for Codex coordination/review and later #186 engineering consumption.
User gate: final release wording, route availability, integrated quest preview UX, and playtest approval remain later gates.
