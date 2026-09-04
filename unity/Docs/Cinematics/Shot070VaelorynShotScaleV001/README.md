# Shot070 Vaeloryn first-run shot scale-out V001

This packet scales the locked Shot070 V002 Wish Dragon / Vaeloryn motion-review
candidate into the first-run eight-shot EDL. It does not regenerate the mesh,
does not spend, and does not claim final cinematic, runtime, or gameplay
authority.

Owner visual approval of the V002 moving-3D candidate remains required before
any final Shot070 performance claim.

## Locked source

The only admissible 3D/motion input is packet
`tdf_packet_vaeloryn_wish_dragon_shot070_source_v002`:

- Blend SHA-256 `10bf9f96380632c983b523172913de8aa31b3187b785bd0b35b23757c7681b89`
- Landscape review MP4 SHA-256 `0f7b66dc3fd6450405cec9cbf5840ba82fd1589ab5fbe73148b1381527169122`
- 960x540 H.264, 24 fps, exactly 168 frames / 7.000s of genuine armature
  articulation (neck, jaw, both wing roots, tail). Not a still-image pan/zoom.

`wish_dragon_review_master.glb` SHA-256
`5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270` remains
rejected and input-ineligible. The validator fail-closes on reuse.

## First-run reuse

Only `Shot070` / `CTMA-BEAT-07` / `AL_FR_MOTION_SRC_070_EIGHTFOLD_WISH_V001`
may bind this candidate. Shots 010, 020, 030, 040, 050, 060, and 080 are
explicitly ineligible: they do not use Vaeloryn, and Wish Dragon may not
substitute for realm dragons or the AL end-card mark.

The first-run 60-second desktop/Android encodes are landscape. This slice
therefore reuses the locked 16:9 articulated clip rather than inventing new
pixels, cropping landscape into portrait, or baking Gems/portal/wish-space
into the clean mesh. Native 9:16 moving blocking remains a later, separate
deliverable.

## Authority

- Status: `MOTION_REVIEW_CANDIDATE`
- `runtimeAuthority`, `gameplayAuthority`, and `finalCinematicApproval` are
  false
- `ownerVisualApprovalRequired` is true
- `didNotRegenerateLockedSource` and `newPixelGeneration=false` are required
- Incremental spend USD 0.00; paid-provider calls 0

## Validate

Run from the repository root:

```text
python tools/cinematics/validate_shot070_vaeloryn_shot_scale.py
python tools/cinematics/test_validate_shot070_vaeloryn_shot_scale.py
```

Any ineligible-shot binding, rejected-source reuse, still-image substitute,
landscape crop, locked-source mutation, paid call, or authority leak fails
validation.
