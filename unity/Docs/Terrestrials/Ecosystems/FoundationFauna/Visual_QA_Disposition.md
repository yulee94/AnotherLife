# Foundation Fauna Visual QA Disposition

- Normalization source: `tdf-foundation-fauna-normalization-2026-07-27-v001`
- Legacy source: `tdf-2026-07-15-v001`
- Review date: `2026-07-27`
- Reviewer mode: Codex terrestrial design
- Exact sheets reviewed at native `1536 × 1024`
- Overall disposition: `PassWithConcern`
- User approval: `NotRequested`
- Production/runtime: `Blocked`

`PassWithConcern` means the immutable sheets are coherent enough for exact user
review. It does not turn them into production textures or waive missing views,
motion, measurement, or importer evidence.

## Per-Profile Review

| Profile | Disposition | Passing evidence | Production-blocking concern |
| --- | --- | --- | --- |
| `tdf_basalt_grazer` | `PassWithConcern` | Strong low shield silhouette, front/side/three-quarter agreement, grounded mass, Champion scale, black shape, and material hierarchy | Ankylosaur/armadillo familiarity is high; rear/top/underside, plate roots, limb origins, and motion/contact are absent |
| `tdf_grove_strider` | `PassWithConcern` | Strong neck/leg negative space, consistent front/side/three-quarter identity, Champion scale, black shape, and material separation | Deer/llama familiarity and crown-like ear foliage risk; rear/top/underside, hoof mechanics, attachment roots, and motion/contact are absent |
| `tdf_mire_lumenback` | `PassWithConcern` | Strong low dome/pouch/paddle-foot read, view agreement, Champion scale, black shape, and material breakup | Catfish/salamander familiarity and emission dependence risk; rear/top/underside, pouch/feeler mechanics, swimming contact, and motion are absent |

## Cross-Family Passes

- all three exact assets resolve as `1536 × 1024`, opaque 8-bit RGB PNG;
- front, side, three-quarter, Champion scale, material samples, and black
  silhouette are present;
- non-color mass and gait footprints are distinct;
- no humanoid equipment, rider, weapon, logo, readable text, or narrative
  symbol appears;
- exact SHA-256, LFS OID, byte length, Unity GUID, and source path are retained;
- no new or duplicated raster is added by the normalization packet.

## Required Future Evidence

Each profile still requires:

- measured front/side/rear/top/underside orthographic blockout;
- unobstructed limb, head, appendage, and surface-feature attachment roots;
- neutral-light material chart with emission disabled;
- rest, locomotion, turn, habitat contact, stop, and recovery sequence;
- grayscale and black silhouettes at measured pixel coverage;
- LOD0/LOD1/LOD2/distant proxy comparison;
- reduced-motion capture;
- habitat-placement sheet;
- production topology, rig, texture, animation, and memory measurements.

## Exact Source Placement Finding

The existing sheets are under `unity/Assets` with default texture importers and
mipmaps enabled. No other Unity asset references their GUIDs. This supports,
but does not prove through a Player build report, that they are currently
unreferenced review source.

Production remains blocked until coordination/engineering chooses an
Editor-only, Docs, or explicitly audited import strategy. This PR does not
modify the existing pixels, `.meta` files, importers, runtime references, or
package layout.

## State Disposition

- exact pictured base identities: `ReadyForUserReview`;
- all six unpictured palette-led variants: `ProposedTextOnly`;
- user creative state: `NotRequested`;
- technical handoff: `Blocked`;
- runtime integration: `Blocked`.

Merging this normalization is not user approval, technical handoff, integrated
playtest acceptance, milestone completion, or release approval.
