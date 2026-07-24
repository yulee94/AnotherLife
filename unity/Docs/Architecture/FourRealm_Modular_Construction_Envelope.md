# Four-Realm Modular Construction Envelope

**Status:** Provisional proposal for owner and technical-art review

**Date:** 2026-07-24

**Design authority:** Root `DESIGN.md` and `unity/Assets/AL/Art/Designs/FourRealmArchitecture.md`

**Visual source:** `unity/Assets/AL/Art/Architecture/ConceptSheets/architecture_four_realm_modular_construction_master_v001.png`

This document proposes measurable starting values for graybox and profiling. It does not approve production meshes, final performance ceilings, gameplay actions, building relocation, progression, economy, or construction rules.

## Approved direction

- `1 Unity unit = 1 meter`.
- One hidden placement-grid and compatible footprint family serves all four realms.
- Realm identity comes from construction, silhouette, materials, wear, and controlled magic rather than incompatible layout systems.
- Players continuously zoom and pan with a controlled elevated tilt.
- Buildings and approved local elements can be selected and interacted with.
- Unrestricted camera orbit is not approved.

## Provisional spatial grid

| Item | Proposed value | Reason |
| --- | ---: | --- |
| Authoring sub-grid | `0.5 m` | Aligns pivots, trims, sockets, steps, wall thickness, and small attachments |
| Placement cell | `2 m × 2 m` | Keeps mobile placement legible while supporting varied footprints |
| Standard structural bay | `4 m` | Two placement cells; reusable wall, road-edge, and bridge rhythm |
| Vertical tier | `1 m` | Predictable floors, retaining walls, stairs, and camera bounds |
| Minimum clear selection gap | `1 m` | Separates neighboring world-space selection proxies at normal zoom |

These values require a graybox castle test before owner approval.

## Provisional footprint classes

| Class | Grid cells | Metric footprint | Intended source use |
| --- | ---: | ---: | --- |
| S | `4 × 4` | `8 m × 8 m` | Small dwelling, store, compact activity building |
| M | `4 × 6` | `8 m × 12 m` | Workshop, service building, barracks support |
| L | `6 × 6` | `12 m × 12 m` | Civic, market, training, research |
| XL | `6 × 8` | `12 m × 16 m` | Major district building |
| Hero | `8 × 10` | `16 m × 20 m` | Keep or important landmark base before unique extensions |

Decorative roots, buttresses, stairs, awnings, and split planes may extend visually beyond a footprint only when their selection, navigation, and neighboring-placement behavior remains explicit.

## Roads, walls, gates, and bridges

- Service road: `4 m` clear visual width.
- Primary road: `6 m` clear visual width.
- Standard wall bay: `4 m` long, approximately `1–1.5 m` thick, approximately `6 m` high before realm-specific crown detail.
- Wall corner footprint: `4 m × 4 m`.
- Small gate opening: `4 m`; major gate opening: `8 m`.
- Standard watchtower footprint: `4 m × 4 m`; larger defensive towers use an approved footprint class.
- Bridge clear widths match the road family and use `4 m` span increments for modular endpoints.

## Pivot and socket conventions

- Building root pivot: footprint center on the finished ground plane.
- Placement and selection root: identical unless an approved reason requires separation.
- Wall and road piece pivot: base center; end sockets sit on exact bay boundaries.
- Attachment pivot: center of the connection face with forward pointing away from the receiving surface.
- Camera focus anchor: near the visual center of the interactable mass, separated from the root when a roof or tower would frame poorly.
- Entrance/navigation anchor: centered on clear walkable access outside the door or gate swing.
- Interaction sockets: named semantic positions such as `Door`, `Activity_00`, `Output_00`, and `Advisor_00`; socket presence does not authorize gameplay behavior.
- Optional VFX socket: separate from interaction and navigation sockets.

## Selection and occlusion

- Use a simple authored selection proxy rather than render-mesh topology.
- Selection proxy follows the functional footprint and may add bounded padding without overlapping a neighboring proxy.
- Every interactable building defines one focus anchor and at least one entrance/navigation anchor.
- Roof, canopy, tall foliage, tower crown, and upper-wall obstruction groups are separately addressable.
- Cutaway groups require complete backing walls, floors, and interior-facing surfaces for every approved camera-reachable view.
- Selected state uses footprint shape and a restrained rim; it cannot rely on realm color or emission alone.

## Provisional camera envelope

- Baseline pitch: approximately `42°` downward.
- Adaptive controlled range: approximately `36°–50°` according to zoom and framing.
- Player input: continuous pinch zoom and drag pan.
- Rotation: no unrestricted orbit; any later bounded yaw proposal requires a complete-side and occlusion review.
- Camera bounds: must stop before exposing unfinished map edges.
- Near and far limits: set from the graybox so the largest approved building remains inspectable and the whole castle remains readable.

## Provisional runtime ceilings

Use the root design guide ceilings as maximum planning values, not targets.

### Common building or large prop

| Representation | Triangle planning range | Material slots | Presentation intent |
| --- | ---: | ---: | --- |
| LOD0 close | Up to `20k` | Up to `2` | Continuous-zoom close inspection |
| LOD1 normal | Approximately `10–12k` | `1–2` | Normal district interaction |
| LOD2 strategic | Approximately `4–6k` | Prefer `1` | Castle overview |
| LOD3 proxy | Approximately `1–2k` or silhouette proxy | `1` | Extreme distance |

### Hero kingdom building

| Representation | Triangle planning range | Material slots | Presentation intent |
| --- | ---: | ---: | --- |
| LOD0 close | Up to `40k` | Up to `3` | Important landmark inspection |
| LOD1 normal | Approximately `20–24k` | Up to `2` | Normal district view |
| LOD2 strategic | Approximately `8–12k` | Prefer `1–2` | Castle overview |
| LOD3 proxy | Approximately `2–4k` | `1` | Extreme distance |

## Provisional continuous-distance behavior

- LOD0 is reserved for camera framing that can reveal its construction and material detail.
- LOD1 is the default interactive district representation.
- LOD2 protects footprint, roofline, entrance mass, realm construction cue, and major value grouping.
- LOD3 preserves only footprint, roofline, major void, and realm silhouette.
- Props, banners, fine roots, conductors, smoke, sparks, fracture effects, and ambient activity reduce before primary geometry.
- Lower LODs normally reduce material slots and shadow cost.
- Cross-fade overlap must be included in profiling because two representations can render simultaneously.
- Exact screen-relative thresholds remain open until the controlled camera and representative castle scene exist.

## Texture and material starting point

- Common building: usually shared `1K` trims or atlases; avoid unique material families.
- Hero building: shared `1K–2K` sources where close framing proves value.
- Common building LOD0: normally no more than two visible material slots.
- Prefer opaque materials; use alpha clipping or blending only when the visual role cannot be achieved more cheaply.
- Pack compatible masks and disable read/write unless runtime CPU access is proven necessary.

## Graybox approval test

Before these values become production authority, assemble one representative Crownlands or Stonehold district containing:

- one hero building;
- six common buildings;
- wall, gate, two towers, road, bridge, and two empty expansion plots;
- continuous zoom and pan with controlled tilt;
- selection footprints and local interaction anchors;
- roof or canopy cutaway on one building;
- all four internal representations with visible transitions.

Review on at least one compact iPhone-class viewport and one representative Android viewport. Record camera framing, visible renderers, materials, triangles, draw calls, texture memory, shadows, overdraw, transition quality, selection accuracy, and build-size impact.

## Critical unresolved choices

- Final lowest-supported device and frame-time target.
- Exact camera near/far bounds and distance curve.
- Exact screen-relative LOD thresholds and cross-fade policy.
- Final footprint classes after graybox circulation and UI testing.
- Whether any explicit edit mode permits building relocation or rotation.
- Final per-scene renderer, draw-call, triangle, texture-memory, shadow, and VFX budgets.
