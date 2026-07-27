# Avian Soarer Visual QA Disposition

## Scope And State

- Issue: `#259`
- Parent source: `tdf-eco-2026-07-27-v001`
- Visual source: `tdf-eco-soarer-2026-07-27-v001`
- Review mode/date: Codex terrestrial-design, 2026-07-27
- Media: eight final 1536 x 1024 opaque RGB PNGs
- User creative approval: not requested
- Production and runtime integration: blocked

This is direct pixel review, not sculpt, topology, skeleton, normal-speed
animation, material, VFX, Unity import, Player build, or device evidence.

- `ProvisionalPass`: coherent enough for exact user review.
- `PassWithConcern`: reviewable, but named ambiguity blocks production.
- `HoldForRefinement`: pixels cannot become the unchanged identity.

`ReadyForUserReview` does not mean `UserCreativeApproved`.

## Binary And Presentation Checks

- All eight finals decode as PNG, measure 1536 x 1024, use opaque 8-bit RGB,
  and remain below 4 MiB each.
- Finals total `18,328,821` bytes; three directly used refinement inputs add
  `7,259,584`; total wave is `25,588,405`.
- No final contains visible title, label, measurement, logo, signature,
  watermark, decorative card, branded UI, habitat scene, faction heraldry,
  literal armor, production topology, or runtime artifact.
- Exact asset ID, dimensions, bytes, SHA-256, LFS identity, generation record,
  role, and supersession are retained in the companion manifest.

## Asset Dispositions

| Asset | Disposition | Pixel finding | Required follow-up |
| --- | --- | --- | --- |
| Rimefan turnaround | `PassWithConcern` | Deep chest, brow shelf, brace feet, folded views, compact rear mass, and diamond-oriented spread read. Head remains raptor/owlish and exact five-group order varies. | Lock an original compact skull, five-group wing, and short wedge tail in measured orthographic source. |
| Rimefan motion/material | `PassWithConcern` | Launch, downstroke, wind hold, cliff brace, fold, recovery, down, keratin, and contact wear read. Top-down tail/wing broaden toward a familiar raptor. | Treat cadence/contact as evidence; rebuild exact diamond, wedge, and five groups before production. |
| Stormglass turnaround | `ProvisionalPass` | Crescent, compact chest, one-root fork, narrow feet, opaque metallic edge, and spread/fold silhouettes read. Scale cue is illustrative and fork is familiar swift/swallow-adjacent. | Verify `0.95` span and final fork proportion in measured orthographic source. |
| Stormglass motion/material | `ProvisionalPass` | Acceleration, pressure bank, stoop, braking, perch, recovery, underside, root, and material hierarchy are coherent and faster than the other profiles. | Test fork brake and three-notch ceiling at normal speed and 64 px. |
| Sootsail turnaround | `PassWithConcern` | Refinement replaces the rejected naked face with a feathered hood. Plank, deep chest, heavy feet, distal split, and matte material read. Head remains raptor-adjacent; toe order is partly occluded. | Lock an original hood/brow/beak, four terminal groups, and two-forward/two-rear brace. |
| Sootsail motion/material | `PassWithConcern` | Side-slip, landing, mantle, running launch, downstroke, glide, fold, tail root, and ash wear read with distinct weight. Some views exceed four terminal groups. | Preserve cadence/contact only; rebuild exact wing and toe order before production. |
| Shared scale/silhouette | `ProvisionalPass` | Diamond, crescent, and plank remain separate; folded and miniature rows retain useful differences; Roc stays much larger. | Replace illustrative scale with reproducible measured capture during coordination review. |
| Shared control/LOD | `PassWithConcern` | Shared semantic locations, folds, surface grammar, and simplification are visible, but some lower-detail heads/bodies converge. | Use as visual intent only; do not derive topology, skeleton, retargeting, or LOD meshes from it. |

## Profile Disposition

| Profile | Source state | A2 QA | User | Production |
| --- | --- | --- | --- | --- |
| `tdf_fauna_stonehold_rimefan_kite` | `ReadyForUserReview` | `PassWithConcern` | `NotRequested` | `Blocked` |
| `tdf_fauna_crownlands_stormglass_swift` | `ReadyForUserReview` | `ProvisionalPass` | `NotRequested` | `Blocked` |
| `tdf_fauna_umbral_sootsail_carrioner` | `ReadyForUserReview` | `PassWithConcern` | `NotRequested` | `Blocked` |

The state advances because exact pixels, roles, prompts, provenance, hashes, and
QA now exist. Concerns prevent automatic production approval.

## Anti-Palette-Swap Review

- Rimefan: broad diamond, deep body, short rear mass.
- Stormglass: narrow continuous crescent, small body, rigid fork.
- Sootsail: nearly straight plank, large grounded body, long distal split.

They pass the qualitative silhouette gate. Coordination must later make 96 px,
64 px, grayscale, reduced-motion, and provisional 100 m capture reproducible.

## Provenance

- Generator: Codex built-in image generation; model unavailable to operator.
- External images, marketplace previews, named-IP references, logos, fonts, and
  third-party game art supplied: none.
- Three generated-original refinement inputs are retained only because they
  directly produced selected finals.
- Unused outputs are not retained; layered/model-native source is unavailable.
- Nothing is approved for unchanged shipping use.

This is technical provenance, not a legal conclusion.

## User Review Gate

For each profile, the user may approve the exact source version/base
variant/hashes, request targeted refinement while retaining the stable ID, or
reject the visual direction. One decision does not approve the other profiles,
structural ecotypes, habitats, production assets, runtime mapping, or gameplay.
