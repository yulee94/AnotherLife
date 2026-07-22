# Terrestrial Engineering Handoff

## Purpose

This note defines what a later engineering PR may consume after Codex coordination/review and user creative approval. It does not authorize implementation by itself.

## Source Inputs

- Design brief: `unity/Docs/Terrestrials/Terrestrial_Design_Brief.md`
- Manifest: `unity/Docs/Terrestrials/terrestrial_profiles_manifest.json`
- Concept sheets:
  - `unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_basalt_grazer_concept_sheet_v001.png`
  - `unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_grove_strider_concept_sheet_v001.png`
  - `unity/Assets/AL/Art/Terrestrials/ConceptSheets/tdf_mire_lumenback_concept_sheet_v001.png`

## Engineering May Later Derive

- Import settings for approved source images.
- Prototype greybox meshes or model briefs that preserve silhouette, scale, and material slot intent.
- Placeholder collider shapes matching `colliderIntent`.
- VFX anchor transforms matching `vfxAnchorIntent`.
- Animation task briefs matching `requiredAnimationIntent`.
- Runtime catalogs only after a separate approved engineering issue defines schema, persistence, spawning, and performance behavior.

## Engineering Must Not Derive Silently

- Player-facing names, lore, descriptions, or story meaning.
- Spawn tables, rewards, combat stats, AI, behavior trees, save fields, or quest relationships.
- Different silhouettes, material families, scale classes, or biome eligibility without returning to terrestrial-design review.
- Glow, tendrils, whiskers, or plate details as mandatory gameplay state reads without accessibility review.

## Review Checklist

- Every consumed profile ID exists in the manifest.
- Every runtime asset points back to a source concept path and source version.
- Every material slot maps to a documented `materialSlotIntent`.
- Every animation or VFX anchor maps to documented intent.
- Reduced-motion and non-color readability assumptions are preserved.
- Runtime implementation remains separate from design-source changes unless a future PR explicitly declares mixed scope and Codex coordination/review dispositions it.
