# Rimecut Pass Visual Source

## Status

- Issue: `#259`
- Primary Codex mode: `terrestrial-design`
- Source version: `tdf-eco-rimecut-2026-07-29-v001`
- Parent roster: `tdf-eco-2026-07-27-v001`
- Habitat: `tdf_habitat_stonehold_rimecut_pass`
- Placement fauna: `tdf_fauna_stonehold_rimefan_kite`
- Technical packet: `TechnicalReviewReady`
- Habitat source: `ReadyForUserReview`
- Rimefan placement evidence: `ReadyForUserReview`
- Overall visual QA: `PassWithConcern`
- User creative state: `NotRequested`
- Runtime/production integration: `Blocked`

This packet provides exact A2 source for a restrained Stonehold pass, both
neighbor seams, a bounded natural kit, effects-independent readability, and
placement evidence for the existing Rimefan Kite. It does not redesign the
Kite or authorize terrain, weather, routes, spawns, AI, gameplay, or runtime
assets.

## Contents

- `Rimecut_Pass_Visual_Source.md`
- `Visual_QA_Disposition.md`
- `Executed_Generation_Prompts_And_Provenance.md`
- `rimecut_pass_visual_source_manifest.json`
- `rimecut_pass_visual_source_packet.schema.json`
- `ConceptSheets/` — three selected opaque RGB PNG review sheets

## Source Budget

- Final PNGs / retained inputs: `3 / 0`
- New final bytes: `9,195,572`
- Referenced existing Rimefan bytes: `4,810,046`
- Duplicated existing bytes: `0`
- Per-image ceiling / three-final ceiling: `4,194,304 / 12,582,912`
- Player/install and runtime-resident contribution: `0`

All finals are `1536 × 1024`, opaque 8-bit RGB, under `unity/Docs`, and routed
through Git LFS. No production model, texture, terrain, rig, animation, prefab,
scene, shader, particle, weather, catalog, thumbnail, or editable source is
included.

## Dependency Note

The parent ecosystem README and review matrix are deliberately unchanged on
this branch because the unmerged Sunmane packet already owns their next count
update. Coordination should update those indexes after predecessor integration
instead of creating a conflicting parallel edit.
