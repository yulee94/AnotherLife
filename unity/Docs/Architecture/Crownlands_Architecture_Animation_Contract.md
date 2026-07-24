# Crownlands Architecture Animation Contract

**Status:** Owner-approved motion direction; final-model timing and runtime budgets require Unity validation

**Date:** 2026-07-24

**Design authority:** Root `DESIGN.md` and `unity/Assets/AL/Art/Designs/FourRealmArchitecture.md`

**Approved model source:** `unity/Assets/AL/Art/Architecture/ConceptSheets/architecture_crownlands_modular_stormwright_detail_v001.png`

**Approved motion reference:** `unity/Assets/AL/Art/Architecture/ConceptSheets/architecture_crownlands_animation_reference_v001.png`

**Implemented graybox:** `unity/Docs/Architecture/Crownlands_Animation_Prototype_Handoff.md`

**Shared runtime system:** `unity/Docs/Architecture/Reusable_Architecture_Construction_State_System.md`

This contract defines how Crownlands architecture moves without weakening its approved civic precision or the shared four-realm mobile system. It does not set gameplay construction duration, resource cost, worker count, upgrade rules, destruction rules, or final performance ceilings.

## Motion thesis

Crownlands motion is **synchronized placement, ordered arcs, measured calibration, controlled radiant lines, and long precise holds**.

- Pale masonry establishes a credible civic foundation before the silver structural frame is installed.
- Paired components move in mirrored or deliberately sequenced relationships rather than arriving chaotically.
- Broad conductors, insulators, shutters, and instrument rings communicate engineered magical practice.
- Indigo energy follows fixed, grounded paths and confirms calibration only after physical assembly is complete.
- Completed masonry, piers, roof, lantern shell, and fixed frame remain visually stable.
- Occasional instrument, technician, shutter, and pulse activity provides operational life without turning the atelier into perpetual machinery.

Changing Crownlands to random lightning, hovering self-assembly, continuous electrical discharge, perpetual mechanisms, or full-building emission would redefine the realm and requires project-owner approval.

## State-driven construction

Construction must support a credible persistent appearance at any approved progress state. A short transition may play when progress advances on-screen, but returning after streaming or offline progress must initialize directly into the correct stable stage.

| State | Persistent visual | On-screen transition |
| --- | --- | --- |
| `PlotPrepared` | Drained pale-stone footprint, buried conductor channels, grounded sockets, and sorted masonry and metal | Ground preparation, channel closure, restrained dust and tool activity |
| `CivicFrameRaised` | Stone plinth, wall shell, paired corner piers, and temporary hoists or braces | Rigid masonry groups are physically placed, seated, and checked |
| `SilverRibsLocked` | Complete broad silver entrance frame and paired structural ribs | Mirrored ribs rotate or lower along ordered arcs, meet prepared joints, and lock mechanically |
| `RoofAndLanternSet` | Stepped blue roof wings, weather closure, and raised conductor lantern | Rigid roof and dome groups lower into place; clamps, fasteners, and broad conductors are fitted |
| `InstrumentsGrounded` | Central calibration engine, side benches, storage, shutters, insulators, and connected conductor network | Fit-out is carried in, seated, and visibly connected to prepared ground paths |
| `CalibratedOperational` | Finished stormwright atelier in a long stable hold | One measured pulse travels from the grounded engine through the broad frame to the lantern and returns to ground |

Gameplay time remains separate from animation time. Do not equate the duration of a construction clip with a construction timer.

## Ordered calibration implementation

The recommended mobile implementation is a deterministic authored conductor path, not runtime procedural lightning.

- Use one or a small fixed set of approved pulse routes with explicit source, destination, return, and ground points.
- The primary route begins at the central calibration engine, follows the broad silver frame, reaches the lantern, and resolves through an authored return or grounding path.
- The pulse is a short scheduled event followed by a substantially longer quiet hold.
- Conductor geometry remains physically connected and readable with the effect disabled.
- Use restrained material-property animation, a ribbon or line renderer on an authored spline, or an equivalent low-overdraw solution.
- Avoid random forks, screen-filling flashes, rapid luminance spikes, and collision-driven electrical simulation.
- Do not run pathfinding or procedural arc generation for ordinary stable activity.
- Suspend the pulse controller when off-screen, beneath useful screen size, or after the authored event completes.
- At strategic distance, replace traveled energy with one subtle lantern or engine value cue if motion is not useful.

Choosing procedural runtime lightning would materially change art control, readability, determinism, VFX cost, accessibility, and mobile performance and therefore requires a separate owner and engineering decision.

## Construction transform rules

- Masonry, piers, silver ribs, roof wings, lantern sections, benches, and instruments use rigid transforms around production pivots.
- Paired ribs and symmetric fittings may move simultaneously only when their destinations and contacts remain readable.
- No structural part appears from empty air or floats without a visible hoist, brace, socket, or authored installation path.
- The broad silver frame locks only after the supporting masonry and paired piers are stable.
- Roof wings and the lantern install only after the main frame is locked.
- The calibration engine and workstations install only after weather closure and grounded channels are available.
- Magic confirms connection and calibration; it does not lift masonry, assemble the roof, or create instruments.
- Scaffolds, hoists, workers, loose supplies, test sparks, and small tools are optional presentation layers rather than required construction-state communication.
- The shared footprint remains fixed from `CivicFrameRaised` onward.

## Stable operational motion

Completed masonry, paired piers, fixed silver frame, roof wings, lantern shell, drainage, and grounded calibration engine remain still.

### Always-safe functional motion

- One restrained engine or lantern value change indicating an available calibrated state.
- Very slow instrument-needle settling when the building first enters view or changes state.
- Minimal cloth or protected shutter response where it does not alter the silhouette.

### Scheduled activity motion

- One measured calibration sweep through a fixed broad conductor route.
- A technician adjusting a practical bench, insulator, lens, or instrument.
- One calibrated ring or armature rotating through a short controlled arc and returning to rest.
- One shutter, vent, or ceramic isolator performing a clear functional adjustment.
- A rare short transfer of contained energy between the central engine and one side workstation.

Use long quiet intervals and district-level scheduling so multiple ateliers do not pulse or calibrate in synchronization.

### Selected and cutaway state

- Selection retains the approved footprint and restrained rim.
- Roof wings and lantern occlusion groups fade, dissolve, or hide to expose the connected working interior.
- The pale-cyan lifted groups in the visual sheet explain ownership only; runtime architecture must not hover above the building.
- The silver frame, paired piers, central engine, entrance, and interaction anchors do not shift during selection.
- Technician and instrument motion may continue only when it does not compete with touch targets.
- Restoring the complete building cannot move the focus, entrance, navigation, selection, or activity anchors.

### Damage and recalibration

Damage and repair remain proposals until gameplay authorizes them.

- Damage replaces authored modules or exposes designed breaks; it does not cause random electrical deformation.
- A disconnected conductor becomes inactive and visibly grounded before repair begins.
- Repair physically replaces stone, metal, roof, or instrument modules before a restrained recalibration pulse confirms the result.
- Electrical or magical effects do not reconstruct physical materials.
- Structural collision and navigation update at approved stable repair stages.
- Final damaged, disabled, repaired, and recalibrated sequences require a separate gameplay and performance review.

## Camera and distance behavior

| Presentation | Retain | Reduce or remove |
| --- | --- | --- |
| Close selected inspection | Technician, one instrument sequence, fixed-route pulse, subtle indicators, roof and lantern cutaway | Multiple simultaneous pulses, dense sparks, repeated ring motion, broad bloom |
| Normal district view | At most one scheduled calibration action and one restrained engine or lantern cue | Fine hands, needles, small insulators, minor bench activity |
| Strategic castle view | Broad arch, paired piers, blue roof wings, raised lantern, and one low-frequency value cue | Technicians, instruments, fine conductors, shutters, sparks, and interior props |
| Far proxy or off-screen | Static silhouette proxy or no renderer update | All animation, particles, audio emitters, and per-object update work |

Distance transitions reduce density inside one continuous world. The player must not perceive a separate animation mode.

## Reduced-motion and low-quality behavior

- Replace mirrored installation arcs with cross-fades between stable construction stages when necessary.
- Preserve the fixed footprint, broad frame, paired piers, roof wings, lantern, and construction progress.
- Replace traveled pulse motion with a static or gently transitioned calibrated-state value.
- Remove technicians, fine instrument movement, shutters, small conductor details, sparks, and particles before removing the broad state cue.
- Avoid camera shake, rapid luminance changes, repeated flashes, and continuous electrical emission.
- Keep selection and cutaway transitions short, predictable, and reversible.

## Runtime hierarchy and ownership

Use one stable building root with separately addressable groups:

- `FoundationAndDrainage`
- `MasonryShell`
- `TwinPiers`
- `SilverFrame`
- `RoofOcclusion`
- `LanternOcclusion`
- `ConductorNetwork`
- `CalibrationEngine`
- `InteriorFitout`
- `ActivityProps`
- `ConstructionScaffold`
- `ConstructionSupplies`
- `VFX_CalibrationPulse`
- `VFX_Lantern`
- `SelectionProxy`
- `FocusAnchor`
- `EntranceAnchor`
- named activity and conductor sockets from the modular-construction envelope

Exact names may follow the final prefab convention, but construction, settled structure, occlusion, selection, LOD, calibration, and stable activity cannot compete for the same transform ownership.

## Mobile implementation guidance

- Do not attach a continuously updating Animator to every instrument, conductor, shutter, or prop.
- Prefer one building-state controller plus authored construction clips and scheduled ambient activity.
- Use one district activity scheduler to stagger calibration pulses, technicians, shutters, and instrument sequences.
- Pool pulse, contact, dust, and restrained spark effects.
- Cull or suspend animation, particles, and audio when off-screen or below useful screen size.
- Use material-property changes rather than unique runtime material instances for engine, conductor, and lantern values.
- Keep technicians, scaffolds, hoists, loose supplies, fine instruments, sparks, and minor conductors as removable quality-tier layers.
- Merge or remove dome ribs, roof-edge conductors, corner caps, awnings, and the smallest instrument props in strategic LODs.
- Profile transparent cutaway transitions and pulse effects because they may temporarily increase overdraw.
- Completed stable buildings should be able to sleep without per-frame logic between scheduled events.

## Provisional timing character

These ratios describe rhythm rather than gameplay duration:

- Masonry placement: measured rigid action followed by a clear stability check.
- Paired rib installation: synchronized ordered arcs, short mechanical lock, then stillness.
- Roof and lantern installation: controlled lowering, fastening, and one restrained settling action.
- Instrument fit-out: sequential practical placement and visible grounding.
- Calibration: one deliberate outward-and-return pulse with no rapid repetition.
- Stable activity: brief precise adjustment followed by a much longer quiet interval.
- Recalibration after repair: less intense than initial activation and limited to the repaired route.

Exact seconds, easing curves, pulse implementation, light intensity, worker count, instrument complexity, activity intervals, and quality-tier limits remain open until a representative Unity atelier is profiled.

## Acceptance checks

- Construction remains understandable with workers, particles, glow, and sound disabled.
- Each persistent state has a believable physical load path and grounded conductor path.
- Silver ribs and roof modules reach prepared joints through controlled rigid motion.
- The completed masonry, piers, frame, roof, lantern shell, and engine remain motionless during stable operation.
- The final silhouette matches the approved Crownlands stormwright atelier.
- Selection, entrance, navigation, conductor, and activity sockets remain clear through every stage.
- The main pulse has a fixed route, visible source, visible destination, and grounded resolution.
- Strategic and far views animate fewer elements than close inspection.
- Reduced-motion mode preserves construction and calibrated-state meaning.
- Several visible ateliers do not pulse, calibrate, or activate in synchronization.
- Off-screen and proxy buildings perform no unnecessary animation work.

## Open implementation decisions

- Final gameplay construction and upgrade state model.
- Authored clips, Timeline, state-machine, or another implementation for synchronized installation.
- Exact conductor spline, pulse material, return path, grounding, and renderer choice.
- Whether technicians are population actors or presentation-only agents.
- Approved light, particle, audio, haptic, and accessibility intensity.
- Damage, disconnection, replacement, repair, and recalibration behavior.
- Per-quality-tier limits for active pulses, technicians, instruments, particles, and animated buildings.
