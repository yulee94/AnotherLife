# Champion Customization Label Source Handoff

**Packet ID:** `al_narrative_character_customization_labels_v001`
**Primary Codex mode:** narrative/content
**Runtime content catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_character_customization_content_catalog.json`
**Related issue:** #184

## Source Intent

This packet gives champion customization labels, preset summaries, and localization keys a bounded narrative/content authority without changing the existing technical customization catalog.

The content catalog maps current technical option and forge preset IDs from `al_character_customization_catalog.json` version `0.5.0` to stable localization keys. Draft English text is included as source copy for review and implementation, not as final release localization.

## Ownership Boundary

Codex narrative/content owns:

- option family names;
- option display-name keys;
- forge preset display-name keys;
- forge preset summaries;
- identity meaning guardrails;
- localization-facing fallback expectations.

Codex engineering owns:

- catalog loading, validation, hashing, and schema authority;
- model capability checks;
- immutable query/result APIs;
- save migration and commit ordering;
- unavailable/error states;
- tests and Player packaging evidence.

The user keeps final approval of visible appearance wording, visual fidelity, and release copy.

## Stable Source Rules

- Technical IDs remain unchanged.
- No body scale, RGB value, preset composition, save field, scene, model, or runtime controller behavior changes are authored here.
- Preset names and summaries must not imply class ownership, realm transfer, combat stats, item entitlement, or story progression.
- Missing localization must become a visible unavailable/status result through the later #177/#184 implementation, not a silent hard-coded player-facing fallback.

## Handoff

Engineering should treat `displayNameKey` and `summaryKey` values as presentation references. Runtime systems should continue to consume the technical catalog for appearance composition and use this content catalog only for source copy/key resolution.

Required implementation validation:

- parse the content catalog;
- verify every referenced option and preset ID exists in `al_character_customization_catalog.json`;
- verify every display and summary key resolves in `draftLocalization`;
- keep returned query data immutable;
- prevent internal IDs from appearing as release player-facing text.

## Acceptance Status

Source status: ready for Codex coordination/review and later #184 engineering consumption.
User gate: final release wording and integrated character-creation presentation remain unapproved until playtest/review.
