# Stonehold Architecture Animation Contract

**Status:** Owner-approved motion direction; isolated Unity graybox implemented; production budgets require device validation

**Date:** 2026-07-24

**Design authority:** Root `DESIGN.md` and `unity/Assets/AL/Art/Designs/FourRealmArchitecture.md`

**Approved model source:** `unity/Assets/AL/Art/Architecture/ConceptSheets/architecture_stonehold_modular_workshop_detail_v001.png`

**Approved motion reference:** `unity/Assets/AL/Art/Architecture/ConceptSheets/architecture_stonehold_animation_reference_v001.png`

**Implemented graybox:** `unity/Docs/Architecture/Stonehold_Animation_Prototype_Handoff.md`

**Shared runtime system:** `unity/Docs/Architecture/Reusable_Architecture_Construction_State_System.md`

This contract defines how Stonehold architecture moves without changing its approved construction identity. It does not set gameplay construction duration, resource cost, worker count, upgrade rules, destruction rules, or final performance ceilings.

## Motion thesis

Stonehold motion is **pressure, leverage, impact, and long stability**.

- Construction feels physically assembled from rigid, heavy modules.
- Stone seats downward or into a prepared joint; it does not stretch, grow, inflate, or float.
- Timber hoists, braces, wedges, rollers, and short manual lever actions explain how weight moves.
- Iron locks finish load-bearing joins with brief, forceful closure.
- Dust, sparks, heat, and forge light confirm contact or operation; they do not create the structure.
- Once complete, the building mass is immovable. Life comes from functional activity inside and around it.

Changing Stonehold to magical levitation, self-growing masonry, continuously moving walls, or broad elastic motion would redefine the realm's motion identity and requires project-owner approval.

## State-driven construction

Construction should be implemented as resumable visual states rather than one mandatory cutscene. A game may advance over minutes or hours, resume after the app was closed, or complete off-screen. The building must therefore render a credible static state at any approved progress point and use a short transition only when a threshold is crossed on-screen.

| State | Persistent visual | On-screen transition |
| --- | --- | --- |
| `PlotPrepared` | Leveled stepped footprint, sorted stone, compact timber and iron supply groups | Short ground-settling dust pulse; no structure appears |
| `FoundationSeated` | Complete foundation and first stable course | One or two rigid block groups lower, contact, and settle |
| `WallShellLocked` | Corners, wall bays, clipped arch, and complete load path | Wall groups lever into sockets; lintel seats; iron catch closes |
| `RoofAndChimneySet` | Roof beams, roof slabs, chimney, and weather closure | Broad roof groups lower; clamps engage; dust falls from contact edges |
| `FittedOut` | Forge, workbench, storage, bellows, shutters, and approved activity props | Props arrive through sockets or appear between sessions; no clutter cascade |
| `Operational` | Approved finished workshop silhouette | Forge ignites locally, one completion thud, brief dust settle, then a long quiet hold |

The complete user-facing transition may be compressed for presentation, but gameplay time and economy remain separate systems. Do not map animation seconds directly to construction timers.

### Construction transform rules

- Animate authored rigid groups around production pivots; never scale the complete building from zero.
- Prefer short vertical seating, constrained rotation from a believable hoist, or lateral sliding along rollers.
- Keep the footprint fixed from `FoundationSeated` onward so selection, navigation, and camera focus do not jump.
- Major stone contacts end without elastic bounce. At most, allow a tiny single settling correction.
- Scaffolds, workers, loose supplies, rope, dust, and sparks are optional presentation layers, not required state communication.
- The strategic view may cross-fade between stable stage silhouettes. Close inspection may play the physical transition.
- If progress changes while off-screen, initialize directly into the correct persistent state.

## Stable operational motion

The completed building itself does not idle. Masonry, roof slabs, chimney, buttresses, and fixed iron braces remain still.

### Always-safe functional motion

- A slow, broad chimney plume with restrained opacity and no constant particle shower.
- Low-amplitude forge-light variation contained inside the forge, doorway, and nearby window.
- Very subtle heat distortion near the furnace at close inspection only.

### Scheduled activity motion

- One bellows compression and recovery.
- One or two short hammer strokes at an approved activity socket.
- A shutter, vent, or iron catch opening and closing once.
- A rare compact spark burst at the forge or anvil.
- A worker entering, using, or leaving an approved activity socket when character population rules permit.

Use irregular quiet intervals so multiple workshops do not pulse in synchronization. Do not make every activity loop continuous.

### Selected and cutaway state

- Selection may add the approved ground footprint and restrained material rim.
- Roof and canopy occlusion groups fade, dissolve, or hide to expose a fully finished interior.
- The pale-cyan lifted roof in the visual sheet explains group ownership; the runtime roof must not physically hover above the building as an idle effect.
- Interior functional motion may continue during inspection, but selection feedback stays clearer than sparks, smoke, or worker activity.
- Returning to the stable view restores the roof without shifting the building root or activity sockets.

### Damaged and repair-ready state

Damage and repair visuals remain proposals until gameplay defines them.

- Damage should remove or replace authored modules rather than bend the complete structure elastically.
- Smoke, embers, rubble, and fractured iron remain localized and must not obscure selection.
- Repair should reverse a valid modular break state through bracing, seating, and locking—not reconstruct the entire building with magic.
- Final damaged, destroyed, and repair sequences require a separate gameplay and performance review.

## Camera and distance behavior

| Presentation | Retain | Reduce or remove |
| --- | --- | --- |
| Close selected inspection | Activity prop motion, one worker if allowed, forge pulse, rare sparks, heat shimmer, roof cutaway | Dense dust, multiple simultaneous workers, broad smoke overlap |
| Normal district view | Broad smoke pulse, forge value change, at most one readable activity loop | Small tool motion, most sparks, minor shutters and rope |
| Strategic castle view | Stable silhouette and, if affordable, one low-frequency chimney cue | Workers, tool motion, heat shimmer, sparks, loose construction props |
| Far proxy or off-screen | Static proxy or no renderer update | All animation, particles, audio emitters, and per-object update work |

Distance transitions must feel like density reduction inside one continuous world, not a visible animation-mode switch.

## Reduced-motion and low-quality behavior

- Never require camera shake to communicate construction contact.
- Replace repeated impact motion with one quick state cross-fade plus a grounded dust ring or value change.
- Disable heat distortion, minor sparks, rope secondary motion, and worker loops before removing the broad functional cue.
- Preserve stage silhouette, local forge value, completion state, selection footprint, and sound/haptic alternatives.
- Avoid flashing the complete building or producing a full-screen completion pulse.

## Runtime hierarchy and ownership

Use one stable building root with separately addressable groups:

- `Foundation`
- `WallShell`
- `EntranceAndLintel`
- `RoofOcclusion`
- `Chimney`
- `InteriorFitout`
- `ActivityProps`
- `ConstructionScaffold`
- `ConstructionSupplies`
- `VFX_Dust`
- `VFX_Forge`
- `VFX_Smoke`
- `SelectionProxy`
- `FocusAnchor`
- `EntranceAnchor`
- named activity sockets from the modular-construction envelope

The exact runtime naming may follow the project's final prefab convention, but the ownership boundaries must survive import. Construction motion, selection, roof cutaway, LOD, and stable activity should not compete for the same transform.

## Mobile implementation guidance

- Do not attach an independent always-updating Animator to every static module.
- Prefer one building-state controller plus authored clip groups, timeline sampling, or equivalent centralized control.
- Schedule ambient loops through a shared district activity system so nearby buildings do not all update or trigger together.
- Pool dust, smoke, spark, and completion effects.
- Cull animation, particles, and audio when off-screen; throttle or suspend them below their useful screen size.
- Use material-property changes for contained forge variation rather than cloning materials per building.
- Keep workers and construction rigs as removable quality-tier layers.
- Profile animation cross-fades because construction and LOD transitions can briefly render both representations.

## Provisional timing character

These ratios describe rhythm, not gameplay duration:

- Major seating action: short acceleration into contact, immediate stop, brief settle.
- Iron lock: very short closure with a single mechanical response.
- Dust: quick expansion followed by a longer low-opacity falloff.
- Forge ignition: one controlled rise, a small confirmation pulse, then a stable low-frequency variation.
- Activity loop: short action followed by a noticeably longer quiet hold.
- Completion: one grounded beat; no repeating celebration loop.

Exact production seconds, easing curves, particle counts, smoke lifetime, and
randomized idle intervals remain open until production geometry, effects, and
representative Android and iOS devices are profiled.

## Acceptance checks

- The construction sequence remains understandable with all workers, particles, magic, and sound disabled.
- Every persistent stage has a believable load path and weather closure appropriate to its progress.
- No rigid stone module visibly stretches or bends.
- The operational building silhouette is identical throughout stable idle motion.
- Selection and entrances remain usable during construction, activity, and roof cutaway according to gameplay permissions.
- Normal and strategic views show fewer moving elements than close inspection.
- Reduced-motion mode preserves state meaning without repetitive impacts or camera impulses.
- Several visible workshops do not animate in synchronization.
- Off-screen and far-proxy buildings perform no unnecessary animation work.

## Open implementation decisions

- Final gameplay construction and upgrade state model.
- Whether workers are persistent population actors or temporary presentation-only agents.
- Exact transition ratios, pause/resume behavior, and completion notification rules.
- Approved audio and haptic intensity.
- Damage, destruction, repair, and downgrade behavior.
- Per-quality-tier limits for active characters, particles, smoke, and animated buildings.
