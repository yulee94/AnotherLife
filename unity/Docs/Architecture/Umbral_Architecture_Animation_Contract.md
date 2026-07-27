# Umbral Architecture Animation Contract

**Status:** Owner-approved fourth-realm motion direction; isolated Unity graybox implemented; production budgets require device validation

**Date:** 2026-07-24

**Design authority:** Root `DESIGN.md` and `unity/Assets/AL/Art/Designs/FourRealmArchitecture.md`

**Approved model source:** `unity/Assets/AL/Art/Architecture/ConceptSheets/architecture_umbral_modular_veilwright_detail_v001.png`

**Approved motion reference:** `unity/Assets/AL/Art/Architecture/ConceptSheets/architecture_umbral_animation_reference_v001.png`

**Implemented graybox:** `unity/Docs/Architecture/Umbral_Animation_Prototype_Handoff.md`

**Shared runtime system:** `unity/Docs/Architecture/Reusable_Architecture_Construction_State_System.md`

This contract defines how the Umbral veilwright atelier enters the shared construction lifecycle and how its operational confirmation can feel distinctive, mysterious, and high-impact without implying progression over the other three realms. Umbral is the fourth peer realm, not a later era or upgrade tier. This contract does not establish gameplay construction duration, resource cost, progression order, narrative canon, worker count, upgrade rules, destruction rules, or final device budgets.

## Motion thesis

Umbral motion is **offset tension, deliberate concealment, inward folding, controlled absorption, decisive closure, and long silence**.

- Physical construction establishes the complete load path before any ward response occurs.
- Offset masonry groups arrive through oblique but grounded installation paths.
- Veil anchors pivot into prepared joints and become fixed structural or functional components.
- Split roof planes close around a protected negative space without hovering.
- Magical activity travels through four authored ground channels toward one darkglass core.
- The operational climax briefly closes into a contained near-black eclipse, confirms through one narrow chimney thread, then returns to a quiet readable building.
- Completed masonry, roof planes, chimney, anchor frames, table, and fit-out remain motionless during stable operation.

Changing Umbral to a permanent portal, hovering self-assembly, screen-wide darkness, continuous smoke, random lightning, rapid flashing, procedural folding geometry, or a full-building purple glow would redefine the realm and requires separate project-owner approval.

## Fourth-realm intensity rule

Umbral may use the sharpest inward-convergence presentation among the four realms, but this is a realm-specific motion signature rather than a statement of power or progression. Its impact comes from hierarchy and contrast rather than persistent density.

- The climax is one authored inward-convergence event, not a continuously running spectacle.
- Four small anchor wakes establish anticipation.
- Short grounded routes pull attention inward.
- One low eclipse ring supplies the peak shape.
- One narrow core-to-chimney thread supplies confirmation.
- A long silent hold after the event makes the climax feel deliberate and lets the controller sleep.
- Construction remains readable when all emission, workers, particles, sound, and camera effects are disabled.

This rule is the principal mobile-safety constraint. Increasing the event by adding multiple beams, random branches, broad fog, rapid repetitions, camera shake, or simultaneous district-wide activations is not an approved polish pass.

## State-driven construction

Every state is a persistent, load-bearing appearance. Returning after streaming or offline progress initializes directly into the correct stable state; it does not replay prior transitions.

| Shared runtime state | Umbral presentation | Persistent visual | On-screen transition |
| --- | --- | --- | --- |
| `SitePrepared` | `BoundaryMarked` | Fixed blackened-stone footprint, pale ash joints, four physical anchor sockets, and sorted supplies | Ground courses seat and the four sockets are uncovered or mechanically set |
| `BaseStructureEstablished` | `OffsetShellRaised` | Offset graphite masonry shells, sheltered oblique entrance, side passage, temporary hoists, and braces | Rigid wall groups rise or slide along staggered oblique paths and settle into prepared courses |
| `SignatureStructureEstablished` | `VeilAnchorsBound` | Smoked-iron and sparse-obsidian anchor frames locked into wall and floor joints | Anchor frames pivot mechanically from grounded braces, meet their sockets, and lock without magical lifting |
| `UpperStructureEstablished` | `SplitRoofsSealed` | Two broad asymmetrical roof planes, protected central void, weather closure, and low ward chimney | Roof groups lower from visible support, close around the void, fasten, and remain still |
| `FitoutCompleted` | `ReliquariesGrounded` | Darkglass sealing table, reliquaries, canopy, benches, shutters, clamps, and short carved channels | Practical modules enter, seat, connect, and receive one restrained dormant value |
| `Operational` | `VeilConvergenceOperational` | Complete atelier in a long quiet hold | Four anchors wake in sequence, energy folds inward, one eclipse closes at the core, one thread confirms at the chimney, and all activity sleeps |

Gameplay time remains separate from presentation time. Do not equate the 16-second review rhythm or any authored clip duration with a construction timer.

## Authored veil convergence

The recommended implementation is a deterministic fixed route with a single reusable moving focus.

1. Four physical anchor points wake in a deliberate sequence.
2. One pooled convergence focus traverses each authored anchor-to-core segment.
3. Short route renderers use material-property response to reinforce the inward direction.
4. One grounded eclipse ring rotates and contracts briefly around the darkglass core.
5. The same focus travels from the core to one authored chimney point.
6. The chimney gives one restrained confirmation value.
7. The orb, light, route response, and ring motion stop; the building returns to resting values.

Implementation requirements:

- Use exactly four primary anchor points for this atelier profile.
- Keep the source, route, destination, and chimney resolution visibly grounded.
- Use one pooled focus rather than multiple transparent projectile objects.
- Use material property blocks or an equivalent shared-material-safe technique.
- Use one localized light at most for the prototype presentation.
- Do not calculate routes procedurally at runtime.
- Do not use collision-driven energy, random forks, or full-screen post-processing.
- Do not move load-bearing or silhouette-defining components during the operational event.
- Suspend the activity when off-screen, below useful screen size, or after the authored event.

## Construction transform rules

- Foundation, masonry, anchor frames, roof planes, chimney, benches, reliquaries, table, shutters, and canopy use rigid transforms around production pivots.
- Offset motion must end at visible prepared joints; it cannot read as geometry phasing through neighboring modules.
- Per-part staggering is allowed only while destinations remain obvious at mobile size.
- Anchor frames may rotate into place, but their final structural relationship must read with all magic disabled.
- Roof planes lower only after the offset masonry shell and anchor frames are stable.
- Fit-out begins only after weather closure is complete.
- Magic confirms the fitted ward network; it does not create stone, timber, metal, glass, cloth, or tools.
- The footprint, entrance, focus anchor, and activity sockets remain fixed from `OffsetShellRaised` onward.

## Stable operational motion

### Always-safe presentation

- One nearly dormant core or chimney value indicating availability.
- Minimal protected cloth response that does not change the strategic silhouette.
- A shutter or clamp settling once after selection or state entry.

### Scheduled presentation

- One full veil-convergence confirmation.
- One technician handling a reliquary or darkglass tool at an approved socket.
- One short ring or clamp adjustment that returns to rest.
- One contained transfer between the table and a single side reliquary.

Use long quiet intervals and a district-level scheduler so several buildings never converge in synchronization.

### Selected and cutaway state

- The two roof planes may fade or hide as deterministic occlusion groups.
- Pale cyan lifted roofs in the reference sheet explain ownership only; roofs do not hover in the world.
- Selection cannot move the entrance, table, anchors, focus point, navigation, or activity sockets.
- The convergence event may continue during inspection only when it does not obscure touch targets.
- Restoring roof visibility must not restart the operational event.

## Camera, distance, and quality behavior

| Presentation | Retain | Reduce or remove |
| --- | --- | --- |
| Close selected inspection | Four anchors, short inward routes, eclipse ring, one core-to-chimney confirmation, one optional technician, roof cutaway | Broad smoke, repeated events, multiple lights, unrelated prop motion |
| Normal district view | Four anchor wakes, one central closure, one chimney confirmation | Fine tools, hand motion, small clamps, interior shelf activity |
| Strategic castle view | Offset roof silhouette, protected entrance void, four small anchor values, one central rim, one chimney wink | Traveled orb, detailed route lines, technician, particles, light spill |
| Far proxy or off-screen | Static silhouette proxy or no renderer update | All activity, transparent effects, lights, particles, audio, and per-object updates |

Remove secondary content before weakening the primary construction state or silhouette.

## Reduced-motion and sensory safety

- Snap between persistent construction states instead of playing staggered transform paths.
- Remove traveled energy, eclipse-ring rotation, and core-to-chimney motion.
- Replace the event with a gentle static confirmation value across the grounded core and chimney.
- Do not use camera shake, haptics by default, strobing, repeated flashes, rapid luminance oscillation, or screen-wide darkening.
- Keep the brightest transparent response inside roughly the central quarter of the building.
- Avoid a full-frame exposure shift; the near-black eclipse is a local material and rim event.
- Preserve the non-color story: four sockets, converging carved channels, central ring, chimney, and long return to stillness.

## Runtime hierarchy and ownership

Use one stable building root with separately addressable groups:

- `FoundationAndBoundary`
- `OffsetMasonryShell`
- `EntranceAndSidePassage`
- `VeilAnchorFrames`
- `RoofOcclusionWest`
- `RoofOcclusionEast`
- `WardChimney`
- `DarkglassCore`
- `GroundedWardRoutes`
- `InteriorFitout`
- `ActivityProps`
- `ConstructionScaffold`
- `ConstructionSupplies`
- `VFX_ConvergenceOrb`
- `VFX_EclipseRing`
- `VFX_ChimneyConfirm`
- `SelectionProxy`
- `FocusAnchor`
- `EntranceAnchor`
- `WardAnchor_00` through `WardAnchor_03`
- named activity and interaction sockets from the modular-construction envelope

Exact production names may follow the final prefab convention, but construction, settled structure, occlusion, selection, LOD, ward activity, and interaction cannot compete for the same transform ownership.

## Mobile implementation guidance

- Reuse `ArchitectureConstructionAnimationController` and one `ArchitectureConstructionAnimationProfile`.
- Keep the Umbral-only activity in `UmbralVeilwrightStableActivity`; do not create a parallel state machine.
- Use one convergence orb, one eclipse-ring transform, four fixed anchors, one core point, and one chimney point.
- Do not attach a continuously updating `Animator` to every anchor, route, prop, or material.
- Use shared materials plus property blocks rather than runtime material instances.
- Pool any optional dust, contact, or restrained residue effects.
- Completed non-looping buildings disable their controller after presentation.
- Schedule rare operational events at district level and prevent synchronized activations.
- Merge or remove roof fasteners, canopy fringe, shelves, tools, small reliquaries, secondary anchor details, and fine channels before changing the main silhouette.
- Profile transparent overdraw, emission, light count, and multiple visible ateliers on representative iOS hardware before claiming a final device budget.

## Acceptance checks

- The motion reference passes visual review at `95 / 100`.
- Construction remains understandable with workers, particles, glow, sound, and camera effects disabled.
- Every persistent state has a believable physical load path.
- The same fixed footprint and entrance survive all six states.
- The four anchor sources, central destination, and chimney resolution are authored and visible.
- The strongest effect remains contained within the workshop footprint.
- Completed masonry, anchor frames, roofs, chimney, and fit-out remain motionless during stable operation.
- The final silhouette matches the approved Umbral veilwright atelier.
- Reduced-motion mode removes traveled energy and ring rotation.
- Strategic and far views animate fewer elements than close inspection.
- The event returns to resting values and can sleep.
- Several visible ateliers do not activate together.
- Off-screen and proxy buildings perform no unnecessary animation work.

## Open implementation decisions

- Final production-model pivots, renderers, route geometry, and material response.
- Exact district scheduling interval and maximum simultaneous events.
- Final light, particle, audio, haptic, and accessibility limits.
- Whether a technician is a population actor or presentation-only agent.
- Damage, disrupted ward, repair, and resealing behavior.
- Per-quality-tier route, light, particle, and active-building limits.
- Representative-device performance results.
