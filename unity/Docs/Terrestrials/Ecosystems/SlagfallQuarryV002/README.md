# Slagfall Quarry v002 Visual Source

## Status

- Issue: `#259`
- Primary delivery mode: `A2 terrestrial design`
- Branch convention: `a2/terrestrial-<scope>`
- Source version: `tdf-eco-slagfall-2026-07-30-v002`
- Parent roster: `tdf-eco-2026-07-27-v001`
- Habitat: `tdf_habitat_stonehold_slagfall_quarry`
- Fauna: `tdf_fauna_stonehold_slagwhistle_burrower`
- Technical packet: `TechnicalReviewReady`
- Habitat source: `ReadyForUserReview`
- Standard-adult fauna source: `ReadyForUserReview`
- Overall visual QA: `Pass`
- User creative state: `NotRequested`
- Runtime/production integration: `Blocked`

This is a new A2-owned correction packet. It preserves the stable habitat and
fauna IDs while superseding the visual concerns found in frozen PR #369 at
exact head `d94e3ea38ac37c2481f857cc592811d91d839542`. It does not edit, rebase,
or replace that frozen branch.

## Contents

- `Slagfall_Quarry_And_Slagwhistle_Burrower_Visual_Source_V002.md` — protected
  identity, anatomy, materials, habitat grammar, motion, kit, optimization
  targets, and authority boundary.
- `Executed_Generation_Prompts_And_Provenance.md` — accepted calls, refinement
  lineage, internal references, and exact executed prompts.
- `Visual_QA_Disposition.md` — native-resolution review and remaining
  engineering cautions.
- `slagfall_quarry_visual_source_manifest_v002.json` — exact IDs, source
  states, assets, hashes, byte budgets, and generation calls.
- `ConceptSheets/` — three accepted opaque `1536 × 1024` PNGs routed through
  Git LFS.

## Source Budget

- Final PNG count: `3`
- Final compressed bytes: `8,271,531`
- Three-image ceiling: `12,582,912`
- Per-image ceiling: `4,194,304`
- Maximum simultaneous decoded RGBA bytes: `18,874,368`
- Player/install contribution: `0`
- Runtime-resident contribution: `0`

No production model, texture set, terrain, rig, animation clip, prefab, scene,
shader, VFX, audio, catalog, Addressable, or editable source is included.

## Authority Boundary

This packet owns A2 visual-source intent only. It does not authorize terrain
dimensions, routes, navigation, collision, spawn placement, AI, combat,
rewards, crafting, audio, quests, lore, save data, streaming, device support,
or production integration. User creative approval and a separate A1 technical
handoff remain required before engineering.
