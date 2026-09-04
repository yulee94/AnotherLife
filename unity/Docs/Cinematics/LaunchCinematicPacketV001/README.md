# Launch cinematic packaging packet V001

This packet is the fail-closed packaging/encode gate for issue #284. It does **not**
ship a 60-second launch cinematic, does **not** promote Shot070 V002, and does **not**
claim gameplay, runtime playback, or final visual acceptance.

## Authority

- Status: `PACKAGING_BLOCKED_NO_APPROVED_MASTER`
- Runtime / gameplay / final cinematic approval: false
- Owner visual approval of a moving 3D 60-second master is still required
- Shot070 Vaeloryn V002 remains `MOTION_REVIEW_CANDIDATE` (7.000s / 168 frames). Its
  review MP4 SHA-256 `0f7b66dc3fd6450405cec9cbf5840ba82fd1589ab5fbe73148b1381527169122`
  is forbidden as a launch master.
- Rejected Meshy GLB SHA-256 `5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270`
  remains input-ineligible.
- Still-image pans, loops, interpolation, or rigid-review stills cannot substitute for
  genuine 1,440-frame / 24 fps / 60.000s motion.

## Encode contract (not current outputs)

| Variant | Picture | Codec | Rate | Cap |
| --- | --- | --- | --- | ---: |
| Desktop | 1920x1080 | MP4 / H.264 High | 24 fps / 60.000s / 1,440 frames | 95,000,000 bytes |
| Android | 1280x720 | MP4 / H.264 Main | 24 fps / 60.000s / 1,440 frames | 42,000,000 bytes |

No compliant master or platform encode is present. `encodes.desktop` and
`encodes.android` are null. StreamingAssets contains zero launch MP4 files.

## Runtime catalog and controller

- Catalog: `unity/Assets/AL/StreamingAssets/GameData/al_launch_cinematic_runtime.v1.json`
- `approvedForProduction=false`, `reducedMotionFallbackOnly=true`
- Boot consults `LaunchCinematicPlaybackCoordinator` and stays on the existing
  static/reduced-motion fallback. Frame 1440 or media absence never grants readiness.

## Evidence

Windows and representative Android package evidence for this slice is the honest
absence of a launch encode: packaged launch MP4 count is 0, decode of a launch
master was not performed, and presentation remains static fallback. Device-matrix
decode/memory of an approved 60-second film remains a successor.

## Validate

```text
python tools/cinematics/validate_launch_cinematic_packet.py
python tools/cinematics/test_validate_launch_cinematic_packet.py
```

Any authority leak, Shot070 promotion, rejected-source reuse, still-image motion,
packaged MP4 while blocked, catalog hash mismatch, or fake 60-second claim fails
closed.

Issue #284 stays OPEN.
