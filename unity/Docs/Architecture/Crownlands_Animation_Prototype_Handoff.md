# Crownlands Stormwright Animation Prototype Handoff

**Status:** Owner-approved motion direction implemented as an isolated Unity graybox

**Date:** 2026-07-24

**Unity version:** 2022.3.62f3

**Approved model reference:** `Assets/AL/Art/Architecture/ConceptSheets/architecture_crownlands_modular_stormwright_detail_v001.png`

**Approved motion reference:** `Assets/AL/Art/Architecture/ConceptSheets/architecture_crownlands_animation_reference_v001.png`

**Motion contract:** `Docs/Architecture/Crownlands_Architecture_Animation_Contract.md`

This package proves the Crownlands construction sequence, calibrated light treatment, stable-state behavior, roof cutaway ownership, and mobile control pattern before a final architecture model or rig exists. It is a graybox validation asset, not final production art and not yet connected to the live kingdom building system.

## Delivered assets

- Shared runtime controller: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/ArchitectureConstructionAnimationController.cs`
- Shared profile contract: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/ArchitectureConstructionAnimationProfile.cs`
- Crownlands activity component: `Assets/AL/Scripts/Kingdom/Visuals/Architecture/CrownlandsStormwrightStableActivity.cs`
- Crownlands motion profile: `Assets/AL/Art/Generated/Architecture/Profiles/Crownlands_Stormwright_ConstructionProfile.asset`
- Rebuild and preview tool: `Assets/AL/Scripts/Editor/Architecture/CrownlandsStormwrightAnimationPrototypeBuilder.cs`
- Generated prefab: `Assets/AL/Art/Generated/Architecture/Crownlands/Crownlands_Stormwright_AnimationPrototype.prefab`
- Isolated preview scene: `Assets/AL/Scenes/Prototypes/CrownlandsStormwrightAnimationPrototype.unity`
- EditMode tests: `Assets/AL/Tests/EditMode/Architecture/CrownlandsStormwrightAnimationPrototypeTests.cs`
- Rendered preview: `Docs/Architecture/Previews/crownlands_stormwright_graybox_preview_v001.mp4`

The preview scene is intentionally excluded from production build settings.

The original one-off Crownlands controller was replaced by the owner-approved reusable construction-state system. Stonehold and Eldergrove now use the same profile contract for their upcoming grayboxes.

## Demonstrated sequence

| State | Prototype proof |
| --- | --- |
| `PlotPrepared` | Foundation, entrance step, grounded cross-channel, and fixed footprint appear first |
| `CivicFrameRaised` | Pale-stone wall shell and paired front piers rise through rigid transforms |
| `SilverRibsLocked` | Two silver structural groups enter through mirrored ordered arcs and settle |
| `RoofAndLanternSet` | Stepped blue roof wings and the raised lantern lower into their fixed positions |
| `InstrumentsGrounded` | Central engine and two practical workstations install as connected modules |
| `CalibratedOperational` | One pulse follows five authored nodes from the engine through the arch and lantern and back to ground |

The 16-second demonstration is an art-review rhythm only. It does not establish gameplay construction time.

## Mobile safeguards already proven

- One centralized building controller; no Animator is attached to individual static modules.
- Deterministic evaluation supports direct initialization at any persistent construction state.
- A non-looping presentation disables its controller after reaching the stable state.
- The pulse uses a fixed five-node route rather than procedural lightning.
- The building remains still between scheduled instrument and pulse events.
- Reduced motion snaps between persistent stages, removes the moving pulse orb, and lowers light intensity.
- Roof wings and lantern are one cutaway ownership group and do not affect the foundation, entrance, engine, or frame.
- The prototype scene remains outside production build settings.

## Visual review

- Score: `93 / 100`
- Verdict: `pass`
- Mobile recognition retained: paired piers, broad silver arch, stepped dark-blue roof, raised lantern, central engine, open entrance
- Approved light treatment retained: brief blue-indigo route, localized response, no random branching, no full-building emission
- Known visual gap: the graybox intentionally omits final engraving, slate texture, instruments, technician rig, and production lighting

## Rebuild and preview

In Unity, run:

`Another Life > Architecture > Build Crownlands Stormwright Animation Prototype`

Open:

`Assets/AL/Scenes/Prototypes/CrownlandsStormwrightAnimationPrototype.unity`

Enter Play Mode to loop the review presentation. Production use should keep looping disabled and trigger construction or calibration through explicit building states.

## Final-model binding rules

When the production stormwright model arrives:

1. Preserve the same stage ownership: plot, civic frame, silver ribs, roof and lantern, instruments, and calibrated operation.
2. Replace graybox transforms with production pivots without changing the shared footprint, entrance, or focus anchors.
3. Bind the broad arch, engine, lantern, and return-to-ground points to the existing authored pulse route.
4. Keep roof and lantern geometry in deterministic occlusion groups.
5. Retain reduced-motion state snapping and static calibrated-state values.
6. Add technicians only through approved activity sockets and a district scheduler.
7. Profile the final mesh, materials, light, overdraw, and multiple visible ateliers on representative iOS hardware.

## Not yet production-authorized

- Live kingdom integration or gameplay construction triggers.
- Final production mesh, topology, pivots, colliders, LODs, materials, or texture budgets.
- Exact gameplay timing, activity frequency, audio, haptics, or lighting exposure.
- Technician population ownership.
- Damage, disconnection, repair, or recalibration behavior.
- Device performance claims.

Changing the authored pulse to procedural lightning, making the building continuously active, or moving load-bearing structure during stable operation remains a critical direction change requiring owner approval.
