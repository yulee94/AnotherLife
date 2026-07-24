# Umbral Veilwright Animation Prototype Handoff

**Status:** Owner-approved fourth-realm motion direction implemented as an isolated Unity graybox

**Date:** 2026-07-24

**Unity version:** 2022.3.62f3

**Approved model reference:** `Assets/AL/Art/Architecture/ConceptSheets/architecture_umbral_modular_veilwright_detail_v001.png`

**Approved motion reference:** `Assets/AL/Art/Architecture/ConceptSheets/architecture_umbral_animation_reference_v001.png`

**Motion contract:** `Docs/Architecture/Umbral_Architecture_Animation_Contract.md`

Umbral is the fourth peer realm, not a progression tier or later era. This package proves its distinctive offset construction, grounded four-anchor convergence, stable-state behavior, roof cutaway ownership, reduced-motion behavior, and mobile control pattern. It is a graybox validation asset, not final production art and not yet connected to the live kingdom building system.

## Delivered assets

- Shared runtime controller: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/ArchitectureConstructionAnimationController.cs`
- Shared profile contract: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/ArchitectureConstructionAnimationProfile.cs`
- Umbral activity component: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/UmbralVeilwrightStableActivity.cs`
- Umbral motion profile: `Assets/AL/Art/Generated/Architecture/Profiles/Umbral_Veilwright_ConstructionProfile.asset`
- Rebuild and preview tool: `Assets/AL/Scripts/Editor/Architecture/UmbralVeilwrightAnimationPrototypeBuilder.cs`
- Generated prefab: `Assets/AL/Art/Generated/Architecture/Umbral/Umbral_Veilwright_AnimationPrototype.prefab`
- Isolated preview scene: `Assets/AL/Scenes/Prototypes/UmbralVeilwrightAnimationPrototype.unity`
- EditMode tests: `Assets/AL/Tests/EditMode/Architecture/UmbralVeilwrightAnimationPrototypeTests.cs`
- Activity tests: `Assets/AL/Tests/EditMode/Architecture/UmbralVeilwrightStableActivityTests.cs`
- Rendered preview: `Docs/Architecture/Previews/umbral_veilwright_graybox_preview_v001.mp4`
- Review contact sheet: `Docs/Architecture/Previews/umbral_veilwright_graybox_contact_sheet_v001.png`

The preview scene is intentionally excluded from production build settings.

## Demonstrated sequence

| State | Prototype proof |
| --- | --- |
| `BoundaryMarked` | Fixed blackened-stone footprint, oblique entrance step, pale ash joints, dormant central boundary, and four physical anchor sockets |
| `OffsetShellRaised` | Offset graphite wall groups establish an asymmetrical shell, sheltered entrance, and side passage through rigid transforms |
| `VeilAnchorsBound` | Four smoked-iron and obsidian frames mechanically seat into prepared physical sockets |
| `SplitRoofsSealed` | Two broad interlocked roof planes lower into deterministic cutaway groups while the offset ward chimney remains available as the confirmation target |
| `ReliquariesGrounded` | Darkglass sealing table, short carved routes, benches, reliquaries, shutter, and canopy install as practical modules |
| `VeilConvergenceOperational` | Four anchors wake in sequence, one focus follows fixed inward routes, one low eclipse ring closes at the core, one thread confirms at the chimney, and all activity returns to silence |

The 16-second demonstration is an art-review rhythm only. It does not establish gameplay construction time or realm progression.

## Mobile safeguards already proven

- One shared building controller; no `Animator` is attached to individual static modules.
- Exactly four authored physical anchors and four fixed inward routes; no procedural path generation.
- One pooled convergence focus, one localized light, one eclipse-ring transform, and one fixed chimney point.
- The reference-aligned graybox contains 188 renderers, remains below the automated 200-renderer proof ceiling, and has no colliders.
- Deterministic evaluation supports direct initialization at any persistent construction state.
- A non-looping presentation disables its controller after reaching the stable state.
- Reduced motion snaps construction states, removes traveled energy and ring rotation, and retains a restrained static confirmation.
- The two roof groups and front inspection façade hide during the convergence; the foundation, side and rear shell, anchors, core, fit-out, and chimney remain fixed and visible.
- The strongest response stays inside the workshop footprint and returns to a long silent hold.
- The prototype scene remains outside production build settings.
- Shared graybox materials enable GPU instancing, while unused motion-vector, light-probe, and reflection-probe work is disabled.
- Construction entry transforms are cached and visibility writes are change-driven; the Umbral event no longer performs duplicate resting-emission writes before active values.

These safeguards validate the control pattern, not final device performance. Production meshes, shaders, textures, overdraw, and several simultaneously visible buildings still require representative iOS profiling.

The joint Android/iOS static-readiness contract is recorded in `Architecture_Mobile_Compatibility_Handoff.md`.

## Visual review

- Score: `92 / 100`
- Verdict: `pass`
- Mobile recognition retained: paired steep gables, pointed central portal, buttressed wall bays, left aubergine awning, roof battens, offset chimney, four anchor points, and the enlarged central darkglass sealing floor
- Approved motion retained: four sequential anchor wakes, short grounded inward routes, one contained eclipse closure, one chimney confirmation, and a silent stable end
- Known visual gap: the graybox intentionally omits engraved masonry, individual tiles, cloth wear, dense reliquary detail, production textures, atmosphere, workers, and final lighting
- Preview: `640 x 640`, `15 fps`, `240 frames`, `16 seconds`
- Preview byte length: `366,068`
- Preview SHA-256: `b32f574d621f6c3b5a2c2505a2bb36c05762969558a42bb02fc10c90cb02c2e3`
- Contact sheet: `1280 x 640`
- Contact-sheet byte length: `163,487`
- Contact-sheet SHA-256: `1a2a992cc9f5b05e6bbb9ff095f3a8b9f3010cb9d6c49bed2efddf542f74c105`
- Reference/implementation/difference comparison: `Docs/Architecture/Previews/umbral_veilwright_graybox_visual_comparison_v001.png`
- Comparison byte length: `739,671`
- Comparison SHA-256: `76ff9129d0e7f69a679ef8328f3a6e1e5e7bf8af88479c8d167fd3bb534ce5ff`

## Verification

The Architecture EditMode suite passed on Unity 2022.3.62f3:

- Total: `17`
- Passed: `17`
- Failed: `0`
- Includes: shared-profile verification, Crownlands regression coverage, Umbral deterministic state evaluation, exactly four anchors, bounded light and renderer counts, no per-part animators, reduced motion, stable-state sleep, cutaway ownership, GPU-instancing eligibility, unused-render-feature removal, concept/runtime dependency isolation, and production-build exclusion

## Rebuild and preview

In Unity, run:

`Another Life > Architecture > Build Umbral Veilwright Animation Prototype`

Open:

`Assets/AL/Scenes/Prototypes/UmbralVeilwrightAnimationPrototype.unity`

Enter Play Mode to loop the review presentation. Production use should keep looping disabled and trigger construction or veil convergence through explicit building states.

## Final-model binding rules

When the production veilwright model arrives:

1. Preserve the same state ownership: boundary, offset shell, bound anchors, split roofs and chimney, grounded fit-out, and operational convergence.
2. Replace graybox transforms with production pivots without changing the shared footprint, sheltered entrance, four anchor sockets, core point, or chimney destination.
3. Bind only the approved fixed anchor-to-core routes; do not introduce procedural branching.
4. Keep both roof planes and the chimney in deterministic occlusion groups.
5. Retain reduced-motion state snapping, a static confirmation value, and the long silent stable hold.
6. Add technicians only through approved activity sockets and a district scheduler.
7. Profile the final mesh, materials, emission, light, overdraw, and multiple visible ateliers on representative iOS hardware.

## Not yet production-authorized

- Live kingdom integration or gameplay construction triggers.
- Final production mesh, topology, pivots, colliders, LODs, materials, textures, or measured budgets.
- Exact gameplay timing, activity frequency, audio, haptics, or lighting exposure.
- Technician population ownership.
- Damage, disruption, repair, or resealing behavior.
- Device performance claims.

Changing Umbral into a progression tier, replacing the fixed convergence with procedural effects, darkening the whole screen, making the building continuously active, or moving load-bearing structure during stable operation is a critical direction change requiring owner approval.
