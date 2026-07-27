# Warmaster Content Source Handoff

**Packet ID:** `al_narrative_warmaster_content_source_v001`
**Primary Codex mode:** narrative/content
**Runtime content catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_warmaster_content_catalog.json`
**Related issue:** #171

## Source Intent

This packet gives the current Warmaster technical IDs player-facing names, summaries, localization keys, and meaning guardrails without changing purchase logic or runtime authority.

The current runtime IDs are retained for engineering compatibility:

- set ID: `prototype_true_warmaster`
- piece IDs: `warmaster_piece_01` through `warmaster_piece_10`

## Ownership Boundary

Codex narrative/content owns:

- set and piece display-name keys;
- set and piece summary keys;
- draft English source copy;
- prestige/identity meaning guardrails;
- missing-content presentation expectations.

Codex engineering owns:

- purchase price authority;
- Warzone Credit spending;
- required-piece threshold;
- idempotency and duplicate purchase behavior;
- save rollback and migration;
- equipment stats, meshes, VFX, and runtime presentation;
- tests and Player validation.

The user keeps final approval of release wording, visible item presentation, and balance.

## Stable Source Rules

- Internal IDs remain debug-only.
- Piece names do not grant ownership, stats, prices, or completed purchase state.
- `True Warmaster Regalia` describes prestige identity only until engineering validates entitlement.
- Missing localization or unknown future IDs must produce a visible unavailable/status result, not raw ID text.
- The current `prototype_true_warmaster` ID is a legacy runtime ID; engineering may migrate it later only through an explicit compatibility plan.

## Handoff

Engineering should validate:

- unique set and piece IDs;
- every piece references an existing set;
- every set `pieceIds` entry references an existing piece;
- every display and summary key resolves in `draftLocalization`;
- returned query data is immutable and does not expose raw IDs as release copy;
- runtime purchase/equip behavior remains blocked until #171 engineering contracts pass.

## Acceptance Status

Source status: ready for Codex coordination/review and later #171 engineering consumption.
User gate: final Warmaster names, item presentation, purchase balance, and integrated progression feel remain later approval gates.
