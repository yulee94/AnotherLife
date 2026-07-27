# Eldergrove Architecture Animation Contract

**Status:** Owner-approved motion direction; isolated Unity graybox implemented; production budgets require device validation

**Date:** 2026-07-24

**Design authority:** Root `DESIGN.md` and `unity/Assets/AL/Art/Designs/FourRealmArchitecture.md`

**Approved model source:** `unity/Assets/AL/Art/Architecture/ConceptSheets/architecture_eldergrove_modular_workshop_detail_v001.png`

**Approved motion reference:** `unity/Assets/AL/Art/Architecture/ConceptSheets/architecture_eldergrove_animation_reference_v001.png`

**Shared runtime system:** `unity/Docs/Architecture/Reusable_Architecture_Construction_State_System.md`

This contract defines how Eldergrove architecture moves without weakening its approved living-construction identity or the shared four-realm mobile system. It does not set gameplay construction duration, resource cost, worker count, upgrade rules, destruction rules, or final performance ceilings.

## Motion thesis

Eldergrove motion is **guided growth, flowing arcs, elastic recovery, biological circulation, and long stability**.

- Stone and timber provide a believable crafted frame before living structure carries load.
- A small number of grounded roots grow along intentional paths into prepared stone sockets and bronze graft collars.
- Root growth is purposeful, readable, and limited to construction, repair, or another explicit state change.
- Mature load-bearing roots settle into stability and do not sway, breathe, or continuously reshape.
- Sap, water, small nonstructural foliage, herbs, attendants, and localized living repair provide operational life.
- Magic or life energy supports biological processes; it does not instantly conjure masonry, timber, roof modules, tools, or complete buildings.

Changing Eldergrove to instantly grown treehouses, uncontrolled procedural vines, permanently moving structural roots, or full-building green emission would redefine the realm and requires project-owner approval.

## State-driven construction

Construction must support a credible persistent appearance at any approved progress state. A short transition may play when progress advances on-screen, but returning after streaming or offline progress must initialize directly into the correct stable stage.

| State | Persistent visual | On-screen transition |
| --- | --- | --- |
| `PlotPrepared` | Drained pale-stone footprint, bronze root sockets, sorted timber and masonry, planted root bases | Ground preparation, socket opening, restrained soil and dust response |
| `CraftFrameSet` | Stone plinth, wall shell, drainage, and temporary timber guide frame | Rigid stone and timber groups are physically placed and braced |
| `GuidedRootGrowth` | Two or three partially grown support roots on authored paths | Broad roots extend from fixed bases toward prepared collars with restrained active-tip light |
| `RootVaultSettled` | Complete root vault, closed bronze collars, and stable load path | Root arcs meet, interlock, perform one damped settling action, then stop |
| `RoofAndLanternSet` | Complete rigid roof, lantern, weather closure, and high-root occlusion groups | Timber ribs and roof modules lower into place and are mechanically fastened |
| `CultivationOperational` | Finished atelier with installed core and activity groups | One contained sap-circulation pulse reaches the basin; small protected growth unfurls; long quiet hold follows |

Gameplay time remains separate from animation time. Do not equate the duration of a growth clip with a construction timer.

## Guided-root implementation

The recommended mobile implementation is deterministic authored growth, not unrestricted runtime procedural generation.

- Each primary structural root has one fixed grounded base, one authored route, one prepared collar or socket, and one final settled transform.
- Use a small set of authored growth-stage meshes, a limited rig, blend shapes, or an equivalent controlled reveal that preserves taper, shadow, and volume.
- Do not use a flat vertical dissolve as the only growth cue; the root must advance spatially along its path.
- Do not generate random branches or collision at runtime.
- Active growth tips may carry one restrained life accent that disappears when the joint closes.
- Temporary roots cannot obstruct the approved entrance, selection corridor, or camera focus.
- Switch the completed root to a static settled representation and suspend its growth controller.
- At normal and strategic distances, transition between persistent stage silhouettes rather than playing every close construction detail.
- Navigation and collision changes occur at explicit stage boundaries, not every frame of growth.

Choosing fully procedural runtime root generation would materially change art control, collision, navigation, determinism, save data, animation, and mobile performance and therefore requires a separate owner and engineering decision.

## Construction transform rules

- Stone, timber, bronze, roof, and lantern modules use rigid transforms around production pivots.
- Living roots grow only from grounded planted bases; they never appear from empty air.
- Root motion follows a smooth directed curve, may overshoot once at joining, and then settles without endless elasticity.
- Bronze collars and graft plates close after the roots reach their joints.
- The final roof is physically installed after the structural vault is stable.
- Scaffolds, gardeners, tools, soil, loose supplies, active-tip light, and leaves are optional presentation layers rather than required state communication.
- The shared footprint remains fixed from `CraftFrameSet` onward.

## Stable operational motion

Completed masonry, rigid roof modules, lantern structure, bronze graft plates, and mature load-bearing roots remain still.

### Always-safe functional motion

- One slow, contained cultivation-core value cycle.
- A restrained basin-water ripple at normal or close distance.
- Minimal nonstructural foliage response at protected seams.

### Scheduled activity motion

- An attendant watering, pruning, mixing, or transferring material at an approved socket.
- One short pour, stir, or basin interaction.
- A drying-herb or light cloth group responding gently and then returning to rest.
- A rare localized bark-repair seam closing around one bronze graft collar.
- A slow sap-light passage between one root join and the cultivation core.

Use long quiet intervals and district-level scheduling so multiple ateliers do not pulse, repair, or water in synchronization.

### Selected and cutaway state

- Selection retains the approved footprint and restrained rim.
- Roof, lantern cap, and high-root occlusion groups fade, dissolve, or hide to expose the finished interior.
- The pale-cyan lifted groups in the visual sheet explain ownership only; runtime architecture must not hover above the building.
- Mature structural roots stay still during selection.
- Cultivation and activity motion may continue if it does not compete with interaction targets.
- Restoring the complete building cannot move the root, focus, entrance, navigation, or activity anchors.

### Damage and living repair

Damage and repair remain proposals until gameplay authorizes them.

- Damage replaces authored modules or exposes a designed break; it does not elastically deform the entire building.
- Living repair begins only at a valid cut, collar, graft, or prepared socket.
- Repair may use guided regrowth, binding, sap circulation, and new bronze support, but must not reconstruct stone or roof modules from magic.
- Structural collision and navigation update at approved stable repair stages.
- Final damaged, destroyed, regrown, and repaired sequences require a separate gameplay and performance review.

## Camera and distance behavior

| Presentation | Retain | Reduce or remove |
| --- | --- | --- |
| Close selected inspection | Attendant, water, sap passage, herb response, one repair seam, roof cutaway | Multiple simultaneous loops, broad leaf motion, dense droplets or particles |
| Normal district view | Cultivation value, restrained water cue, at most one readable activity loop | Fine hands, individual leaves, small herbs, minor graft movement |
| Strategic castle view | Root-vault silhouette, raised lantern, stone plinth, one broad cultivation value | Attendants, water surface detail, repair, herbs, leaves, droplets |
| Far proxy or off-screen | Static silhouette proxy or no renderer update | All animation, particles, audio emitters, and per-object update work |

Distance transitions reduce density inside one continuous world. The player must not perceive a separate animation mode.

## Reduced-motion and low-quality behavior

- Replace visible root elongation with cross-fades between stable construction stages when necessary.
- Preserve the fixed root-vault silhouette, root-to-collar connection, and construction progress.
- Remove active-tip light, leaf motion, herb sway, water droplets, sap travel, and repair motion before removing the central cultivation-state cue.
- Disable attendants and secondary activity loops.
- Avoid camera shake, rapid luminance changes, repeated growth pulses, and continuous particle emission.
- Use a static value or material-state change when flowing light or water motion is reduced.

## Runtime hierarchy and ownership

Use one stable building root with separately addressable groups:

- `FoundationAndDrainage`
- `MasonryShell`
- `RootBase_00...`
- `RootGrowth_00...`
- `RootSettled_00...`
- `BronzeGraftAndCollars`
- `TimberRoofFrame`
- `RoofOcclusion`
- `LanternOcclusion`
- `InteriorFitout`
- `CultivationCore`
- `ActivityProps`
- `ConstructionGuideFrame`
- `ConstructionSupplies`
- `VFX_GrowthTip`
- `VFX_Sap`
- `VFX_Water`
- `VFX_Repair`
- `SelectionProxy`
- `FocusAnchor`
- `EntranceAnchor`
- named activity sockets from the modular-construction envelope

Exact names may follow the final prefab convention, but construction, settled structure, occlusion, selection, LOD, and stable activity cannot compete for the same transform ownership.

## Mobile implementation guidance

- Do not attach a continuously updating Animator to every root, herb, or prop.
- Disable the root-growth controller after the settled representation is active.
- Prefer one building-state controller plus authored root modules and scheduled ambient activity.
- Use a district scheduler to stagger cultivation, water, repair, attendant, herb, and foliage loops.
- Pool growth-tip, sap, water, leaf, and repair effects.
- Cull or suspend animation, particles, and audio when off-screen or below useful screen size.
- Use material-property changes rather than unique runtime material instances for cultivation and sap values.
- Keep attendants, guide frames, loose supplies, leaf response, herbs, and droplets as removable quality-tier layers.
- Profile stage and LOD cross-fades because both representations may briefly render.

## Provisional timing character

These ratios describe rhythm rather than gameplay duration:

- Stone and timber placement: short grounded action followed by a stable pause.
- Root growth: smooth directional advance with clear start and destination.
- Root joining: one restrained elastic recovery, then complete stability.
- Bronze collar: short controlled closure after biological contact.
- Cultivation activation: one slow circulation pulse and gentle settling.
- Stable activity: brief purposeful action followed by a much longer quiet interval.
- Living repair: rare, localized, and slower than ordinary workshop gestures.

Exact seconds, easing curves, growth stages, rig complexity, light intensity, water behavior, and activity intervals remain open until a representative Unity atelier is profiled.

## Acceptance checks

- Construction remains understandable with workers, particles, glow, sound, water, and leaves disabled.
- Each persistent state has a believable crafted or biological load path.
- Primary roots begin at fixed grounded bases and end in prepared structural joints.
- The mature root vault, masonry, roof, and lantern remain motionless during stable operation.
- The final silhouette matches the approved Eldergrove master sheet.
- Selection, entrance, navigation, and activity sockets remain clear through every stage.
- Strategic and far views animate fewer elements than close inspection.
- Reduced-motion mode preserves construction and operational meaning.
- Several visible ateliers do not grow, pulse, water, or repair in synchronization.
- Off-screen and proxy buildings perform no unnecessary animation work.

## Open implementation decisions

- Final gameplay construction and upgrade state model.
- Authored stage meshes versus a limited root rig or blend-shape implementation.
- Exact root-growth curves, settled-switch timing, collision, and navigation updates.
- Whether attendants are population actors or presentation-only agents.
- Approved water, sap, foliage, audio, and haptic intensity.
- Damage, pruning, graft replacement, regrowth, and repair behavior.
- Per-quality-tier limits for active roots, attendants, water, leaves, particles, and animated buildings.
