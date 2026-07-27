# Foundation Fauna Normalization

## Status

- Issue: `#259`
- Primary Codex mode: `terrestrial-design`
- Source version: `tdf-foundation-fauna-normalization-2026-07-27-v001`
- Legacy source version: `tdf-2026-07-15-v001`
- Profiles normalized: `3`
- Exact existing sheets: `3`
- Overall visual QA: `PassWithConcern`
- User creative approval: `NotRequested`
- Runtime/production integration: `Blocked`

This companion packet normalizes the exact merged source identities for:

- `tdf_basalt_grazer`;
- `tdf_grove_strider`;
- `tdf_mire_lumenback`.

It does not copy or regenerate their existing images. The immutable Git LFS
objects remain at their existing paths under
`unity/Assets/AL/Art/Terrestrials/ConceptSheets/`.

## Contents

- `Foundation_Fauna_Normalized_Visual_Source.md` — protected identities,
  scale, anatomy, materials, motion, variants, quality reductions, budgets,
  and authority boundaries.
- `Visual_QA_Disposition.md` — native-resolution review and exact missing
  evidence that remains production-blocking.
- `foundation_fauna_normalization_manifest.json` — stable source identities,
  paths, hashes, sizes, states, concerns, and reuse evidence.
- `foundation_fauna_normalization_packet.schema.json` — strict retained
  structural schema.

## Source And Package Impact

- Existing exact PNG count: `3`
- Existing exact PNG bytes: `7,381,817`
- New or duplicated raster bytes: `0`
- New Player/install bytes: `0`
- New runtime-resident bytes: `0`
- New dependencies: `0`

The three existing concept sheets are unreferenced by Unity asset GUID outside
their own `.meta` files. This repository audit does not replace a Player build
dependency report. A later engineering decision may relocate review-only source
out of `unity/Assets` or mark it Editor-only; this A2 packet does not change
importers or production layout.

## State Boundary

The three exact base identities advance from `LegacyMergedProposal` to
`ReadyForUserReview` with `PassWithConcern`. Their palette-led variants remain
`ProposedTextOnly`, because no separate exact pixels prove structural or
material differences.

User approval of exact files, hashes, IDs, variants, and accepted concerns is
required before a coordination handoff. No runtime, gameplay, spawn, AI,
combat, reward, save, scene, prefab, shader, or production authorization is
created here.
