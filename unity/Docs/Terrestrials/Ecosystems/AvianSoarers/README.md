# Avian Soarer Visual Source

## Control

- Issue: `#259`
- Parent roster source: `tdf-eco-2026-07-27-v001`
- Child visual source: `tdf-eco-soarer-2026-07-27-v001`
- Primary Codex mode: `terrestrial-design`
- Packet state: `TechnicalReviewReady`
- User creative state: `NotRequested`
- Runtime integration state: `Blocked`
- Player/install contribution: `0 bytes`

This directory contains the first exact visual-source wave for the supporting
fauna proposed by the four-realm ecosystem packet. It advances three
`tdf_rig_avian_soarer` families from `ProposedTextOnly` to
`ReadyForUserReview` without approving them for production or granting runtime
authority:

- `tdf_fauna_stonehold_rimefan_kite`
- `tdf_fauna_crownlands_stormglass_swift`
- `tdf_fauna_umbral_sootsail_carrioner`

The wave deliberately groups one reusable anatomy/control family instead of one
realm. Diamond, crescent, and plank wing plans therefore have to survive the
same material and semantic-control reuse review without becoming palette swaps.

## Contents

- `Avian_Soarer_Visual_Source.md` — scale, silhouette, anatomy, material,
  motion, variation, readability, optimization, and non-authority decisions.
- `Executed_Generation_Prompts_And_Provenance.md` — exact generation and
  refinement prompts, inputs, outputs, timestamps, and provenance.
- `Visual_QA_Disposition.md` — direct pixel review and production follow-ups.
- `avian_soarer_visual_source_manifest.json` — immutable asset identity,
  source-state, budget, generation, and review records.
- `avian_soarer_visual_source_packet.schema.json` — structural validation.
- `ConceptSheets/` — eight final 1536 x 1024 PNG review sheets.
- `GenerationInputs/` — three directly used generated-original refinements.

## Final Review Sheets

| Role | Asset |
| --- | --- |
| Shared true-scale and silhouette comparison | `ConceptSheets/tdf_shared_avian_soarer_scale_silhouette_master_v001.png` |
| Rimefan turnaround | `ConceptSheets/tdf_fauna_stonehold_rimefan_kite_turnaround_v001.png` |
| Rimefan motion/material | `ConceptSheets/tdf_fauna_stonehold_rimefan_kite_motion_material_v001.png` |
| Stormglass turnaround | `ConceptSheets/tdf_fauna_crownlands_stormglass_swift_turnaround_v001.png` |
| Stormglass motion/material | `ConceptSheets/tdf_fauna_crownlands_stormglass_swift_motion_material_v001.png` |
| Sootsail turnaround | `ConceptSheets/tdf_fauna_umbral_sootsail_carrioner_turnaround_v001.png` |
| Sootsail motion/material | `ConceptSheets/tdf_fauna_umbral_sootsail_carrioner_motion_material_v001.png` |
| Shared control, LOD, fold, and surface study | `ConceptSheets/tdf_shared_avian_soarer_rig_lod_readability_v001.png` |

Every image is proposal evidence, not a shippable texture, model, animation,
loading screen, or marketplace asset.

## State After This Wave

- Three new supporting fauna: `ReadyForUserReview`.
- Ten new supporting fauna: `ProposedTextOnly`.
- Three foundation fauna: `LegacyMergedProposal`; unchanged.
- Sixteen habitats: `RosterProposed`; unchanged.
- Sixteen existing boss/elite anchors: unchanged.
- Structural ecotypes not pictured here: `ProposedTextOnly`.

## Handoff Boundary

1. The user approves, refines, or rejects the exact source version, profile
   IDs, base variants, and PNG hashes.
2. Coordination/review may specify production topology, animation, import,
   memory, view-distance, streaming, and validation only for approved source.
3. Engineering may integrate only that approved and specified source.

No gameplay, AI, spawning, stats, combat, rewards, narrative, scene, prefab,
shader, material, rig, animation, save, runtime catalog, Addressable, or
package authority originates here.
