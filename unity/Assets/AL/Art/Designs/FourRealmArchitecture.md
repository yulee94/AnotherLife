# Four-Realm Architecture

**Status:** All four peer-realm detail and motion directions owner-approved; shared runtime and four isolated graybox prototypes implemented; final-model and live-kingdom integration pending

**Version:** 0.1

**Design contract:** Root `DESIGN.md`

**Asset category:** Realm architecture, strategic-map settlements, and character-scale traversal spaces

**Runtime priority:** Mobile-first, scalable to PC and promotional presentation

**Approved landmark anchor:** [Four-realm landmark anchor v001](../Architecture/ConceptSheets/architecture_four_realm_landmark_anchor_v001.png)

**Approved settlement kit:** [Four-realm settlement kit v001](../Architecture/ConceptSheets/architecture_four_realm_settlement_kit_v001.png)

**Approved castle layout:** [Four-realm castle layout v001](../Architecture/ConceptSheets/architecture_four_realm_castle_layout_v001.png)

**Approved controlled-tilt interaction:** [Crownlands continuous interaction v001](../Architecture/ConceptSheets/architecture_crownlands_continuous_interaction_v001.png)

**Approved modular construction master:** [Four-realm modular construction master v001](../Architecture/ConceptSheets/architecture_four_realm_modular_construction_master_v001.png)

**Approved Stonehold workshop detail:** [Stonehold modular workshop detail v001](../Architecture/ConceptSheets/architecture_stonehold_modular_workshop_detail_v001.png)

**Approved Stonehold motion reference:** [Stonehold animation reference v001](../Architecture/ConceptSheets/architecture_stonehold_animation_reference_v001.png)

**Stonehold motion contract:** [Stonehold Architecture Animation Contract](../../../../Docs/Architecture/Stonehold_Architecture_Animation_Contract.md)

**Approved Eldergrove workshop detail:** [Eldergrove modular workshop detail v001](../Architecture/ConceptSheets/architecture_eldergrove_modular_workshop_detail_v001.png)

**Eldergrove Level 0–10 review candidate:** [Eldergrove Workshop Level Progression](EldergroveWorkshopLevelProgression.md)

**Approved Eldergrove motion reference:** [Eldergrove animation reference v001](../Architecture/ConceptSheets/architecture_eldergrove_animation_reference_v001.png)

**Eldergrove motion contract:** [Eldergrove Architecture Animation Contract](../../../../Docs/Architecture/Eldergrove_Architecture_Animation_Contract.md)

**Approved Crownlands workshop detail:** [Crownlands modular stormwright detail v001](../Architecture/ConceptSheets/architecture_crownlands_modular_stormwright_detail_v001.png)

**Approved Crownlands motion reference:** [Crownlands animation reference v001](../Architecture/ConceptSheets/architecture_crownlands_animation_reference_v001.png)

**Crownlands motion contract:** [Crownlands Architecture Animation Contract](../../../../Docs/Architecture/Crownlands_Architecture_Animation_Contract.md)

**Crownlands graybox handoff:** [Crownlands Animation Prototype Handoff](../../../../Docs/Architecture/Crownlands_Animation_Prototype_Handoff.md)

**Approved Umbral workshop detail:** [Umbral modular veilwright detail v001](../Architecture/ConceptSheets/architecture_umbral_modular_veilwright_detail_v001.png)

**Approved Umbral motion reference:** [Umbral animation reference v001](../Architecture/ConceptSheets/architecture_umbral_animation_reference_v001.png)

**Umbral motion contract:** [Umbral Architecture Animation Contract](../../../../Docs/Architecture/Umbral_Architecture_Animation_Contract.md)

**Umbral graybox handoff:** [Umbral Animation Prototype Handoff](../../../../Docs/Architecture/Umbral_Animation_Prototype_Handoff.md)

**Shared runtime handoff:** [Reusable Architecture Construction-State System](../../../../Docs/Architecture/Reusable_Architecture_Construction_State_System.md)

**Provisional production envelope:** [Four-Realm Modular Construction Envelope](../../../../Docs/Architecture/FourRealm_Modular_Construction_Envelope.md)

**Generation record:** [Source prompts and provenance](../Architecture/ConceptSheets/Architecture_Source_Prompts_And_Provenance.md)

**Approved realm heraldry:** [Four-Realm Heraldry — Arcane Axis](FourRealmHeraldry.md)

## Purpose

Define one shared construction language for Another Life while keeping Stonehold, Eldergrove, Crownlands, and Umbral readable through silhouette, structural logic, material, and controlled magic. Architecture must support both elevated 2.5D kingdom views and character-scale 3D traversal without becoming a collection of unrelated beauty renders.

The architecture program has two layers:

1. **Monumental landmarks:** rare gates, sanctuaries, keeps, bridges, and ritual structures that provide realm-scale identity.
2. **Settlement kits:** repeatable homes, workshops, civic buildings, walls, gates, towers, streets, and crossings that make functioning places.

The landmark anchor controls grandeur and realm identity. It does not require ordinary settlement buildings to repeat landmark-scale ornament or complexity.

## Owner decision record

- 2026-07-24: Project owner approved the four-realm landmark anchor as the monumental architecture direction.
- 2026-07-24: Project owner selected a separate mobile-first settlement kit as the next architecture design stage.
- 2026-07-24: Project owner approved the four-realm settlement-kit visual direction and requested a mobile castle-view placement study before modular construction design.
- 2026-07-24: Project owner rejected a player-facing three-band zoom model in favor of freer continuous navigation and direct interaction with castle elements.
- 2026-07-24: Project owner approved continuous zoom and pan with direct element interaction and a controlled elevated camera tilt. Unrestricted orbit is outside the approved direction.
- 2026-07-24: Project owner approved the shared hidden grid and compatible footprint-family direction beneath realm-specific construction.
- 2026-07-24: Project owner approved the four-realm modular construction master and directed work to continue.
- 2026-07-24: Project owner approved the Stonehold modular workshop detail direction and requested that construction and stable-state animation reference the approved design.
- 2026-07-24: Project owner accepted the Stonehold motion direction and directed work to continue to the next realm.
- 2026-07-24: Project owner approved the elevated Eldergrove modular workshop detail and directed development of its construction and stable-state animation.
- 2026-07-24: Project owner accepted the Eldergrove motion direction and directed work to continue to Crownlands.
- 2026-07-24: Project owner approved the Crownlands modular stormwright detail and directed development of its construction and stable-state animation.
- 2026-07-24: Project owner approved the Crownlands light treatment and motion direction and directed animation work to continue.
- 2026-07-24: Project owner approved one reusable construction-state system with realm-specific motion profiles for all era buildings.
- 2026-07-24: Project owner directed Umbral realm-detail design to follow the established Stonehold, Eldergrove, and Crownlands workshop-sheet structure.
- 2026-07-24: Project owner approved the Umbral veilwright architecture and directed development of a distinctive fourth-realm construction and stable-state animation while retaining a visual-safety score above `90 / 100`.
- 2026-07-24: Project owner clarified that Umbral is the fourth peer realm, not a progression tier or more advanced era. Realm identity may differ in intensity and mood, but no realm is inherently an upgrade over another.
- 2026-07-27 review candidate: an Eldergrove Workshop Level `0`–`10` progression sheet passed the visual gate at `93 / 100`. It is packaged for owner review and does not become production-model authority until explicitly approved.
- Not yet approved: exact gameplay animation timing, final-model calibration binding, worker rules, damage and repair motion, live kingdom integration, element relocation rules, exact grid dimensions, production meshes, topology, materials, colliders, LOD thresholds, atlases, lighting exposure, or measured device performance. The linked envelope, animation contracts, and graybox handoff contain provisional implementation guidance.

## Shared construction rules

- Use one six-state runtime lifecycle for all realms while retaining realm-specific player-facing names, motion profiles, and bounded operational activity.
- Start with believable load paths, foundations, weather protection, entrances, circulation, and repair.
- Use large structural masses and two or three dominant material families before ornament.
- Preserve strong roofline, gate, tower, and bridge silhouettes at strategic-map distance.
- Concentrate realm magic at one functional location such as a forge, root join, storm conductor, ward, or rift seal.
- Make living space, craft, storage, trade, drainage, and defense visible enough that settlements feel inhabited.
- Use the approved Arcane Axis symbols only where realm ownership or institution is intentionally communicated.
- Avoid floating fragments, unsupported cantilevers, decorative spike fields, dense dangling props, and full-surface emission.

## Realm construction grammar

### Stonehold

- Broad, low, compressed masses; stepped basalt foundations; thick buttresses and clipped-corner openings.
- Soot-aged iron, dark timber, heavy masonry, bronze repairs, and localized forge amber.
- Identity survives without smoke, sparks, banners, or glowing cracks.

#### Stonehold architecture motion

- Construction is physical and modular: rigid stone seats downward or into prepared joints through leverage, hoists, rollers, braces, and short forceful contact.
- Iron clamps and locks finish structural joins; dust and forge light confirm contact or operation but never create the building.
- Construction uses persistent progress states so the correct foundation, shell, roof, fit-out, or operational stage can resume after streaming or offline progress.
- The completed masonry, roof, chimney, buttresses, and fixed braces remain still during stable operation.
- Ambient life comes from restrained functional motion: low-frequency chimney smoke, contained forge-light variation, bellows, hammering, vents, shutters, rare sparks, and approved worker sockets.
- Workers, scaffolds, ropes, loose supplies, smoke density, heat shimmer, and sparks are removable distance and quality layers.
- Roof occlusion groups fade or hide for inspection; they do not hover as an in-world idle animation.
- The full state, hierarchy, mobile reduction, reduced-motion, and acceptance rules are defined in the linked Stonehold animation contract.

### Eldergrove

- Tall but open structures; thick living-root frames used as credible beams and braces; breathable courtyards and crossings.
- Pale weathered stone, dark timber, bronze joins, moss, lichen, and controlled green-gold living repair.
- Use a few large roots rather than dense branch or vine noise.

#### Eldergrove architecture motion

- Stone, timber, drainage, sockets, and guide frames are physically assembled before the primary living roots carry load.
- Two or three grounded roots follow deterministic authored paths into prepared stone sockets and bronze graft collars.
- Root arcs perform one restrained elastic recovery when they meet, then become visually stable load-bearing structure.
- Rigid roof and lantern modules are installed only after the root vault has settled.
- Mature masonry, roof, lantern, bronze joints, and structural roots remain still during stable operation.
- Ambient life comes from localized sap circulation, basin water, small nonstructural foliage, herbs, attendants, and rare graft repair.
- Root-growth tips, attendants, water, leaves, herbs, guide frames, and repair effects are removable distance and quality layers.
- Roof, lantern, and high-root occlusion groups fade or hide for inspection; they do not hover in-world.
- The full state, guided-growth, hierarchy, mobile reduction, reduced-motion, and acceptance rules are defined in the linked Eldergrove animation contract.

#### Eldergrove Workshop level-production candidate

- The linked Level `0`–`10` packet is the first production-readable building-family proposal to consume the approved shared level ladder.
- It preserves one stable Workshop footprint and entrance, then adds cumulative root-brace, annex, roof, district, signature, logistics, and capstone modules.
- Level changes remain readable through form without particles, continuous foliage motion, or green emission.
- The candidate retains the common-building mobile envelope at Level `10`; progression does not silently grant a hero-building budget.
- The sheet is source-only and passed a `93 / 100` visual gate. Owner approval, DCC blockout, exact metrics, model pivots, LODs, materials, colliders, and device profiling remain open.

### Crownlands

- Upright balanced proportions; disciplined bays, gables, arcades, drainage, and civic symmetry.
- Pale stone, silvered steel, blue slate or textile, restrained gold repair, and localized celestial conductors.
- Authority comes from order and proportion rather than excessive towers or gold.

#### Crownlands architecture motion

- Pale-stone foundation, masonry shell, and paired corner piers are physically assembled before the silver structural frame is installed.
- Paired silver ribs follow synchronized ordered arcs into prepared joints and lock mechanically.
- Rigid roof wings and the raised conductor lantern are lowered and fastened only after the frame is stable.
- The grounded calibration engine, side workstations, insulators, and broad conductors are installed as connected practical equipment.
- Initial activation uses one measured pulse from the engine through the frame to the lantern and back to ground, followed by a long quiet hold.
- Mature masonry, piers, roof, lantern shell, fixed frame, and calibration engine remain still during stable operation.
- Ambient life comes from occasional instrument or ring movement, a shutter or isolator adjustment, a technician at a workbench, and a scheduled calibration pulse.
- Technicians, fine instruments, scaffold detail, small conductors, sparks, and secondary indicators are removable distance and quality layers.
- Roof wings and lantern occlusion groups fade or hide for inspection; they do not hover in-world.
- The full state, authored calibration, hierarchy, mobile reduction, reduced-motion, and acceptance rules are defined in the linked Crownlands animation contract.

### Umbral

- Narrow offset masses; sheltered passages; asymmetrical split planes; deliberate voids and oblique circulation.
- Blackened stone, obsidian-like facing, ash timber, aubergine cloth, and localized cold-violet sealing fractures.
- Maintain readable midtones and inhabitable construction; avoid spike forests and undifferentiated black silhouettes.

#### Approved Umbral veilwright detail

- Visual function: a practical atelier for smoked-glass ward seals, physical veil anchors, and contained shadow-residue handling. This remains visual context rather than gameplay or narrative canon.
- Primary recognition anchors: offset twin-roof rhythm, sheltered oblique entrance, protected central void, low ward chimney, aubergine side shelter, and one grounded violet sealing focus.
- Construction remains masonry, timber, iron, cloth, and sparse obsidian facing. Magic is localized to a ward table and short physical anchor paths.
- Graphite midtones, pale ash mortar, worn edges, and restrained metal highlights preserve form without depending on violet emission.
- The sheet provides front, rear, top, cutaway, exploded, modular-kit, material, LOD, and silhouette information in the same mobile production-reference structure as the other realms.
- Status: owner-approved realm-detail direction; visual-verdict pass at `94 / 100`.

#### Umbral architecture motion

- Construction uses the shared six-state lifecycle with Umbral-specific player-facing states: `BoundaryMarked`, `OffsetShellRaised`, `VeilAnchorsBound`, `SplitRoofsSealed`, `ReliquariesGrounded`, and `VeilConvergenceOperational`.
- Offset masonry and anchor frames use grounded rigid transforms with asymmetric staging. Magic does not create or levitate physical modules.
- The final operational event is a contained inward convergence: four authored anchor sockets wake, short grounded routes fold toward one darkglass core, a low eclipse ring closes, one narrow thread confirms at the ward chimney, and the building returns to a long silent hold.
- The climax is distinct from the other peer-realm confirmations through inward direction, closure, hierarchy, and contrast—not through superior power, repeated flashes, permanent movement, broad smoke, or full-building emission.
- Completed masonry, anchor frames, split roofs, chimney, table, and fit-out remain still during stable operation.
- Strategic presentation reduces the event to four small anchor values, one central rim, and one chimney confirmation. Far and off-screen buildings perform no activity.
- Reduced-motion mode removes traveled energy and ring rotation, replacing the sequence with a gentle static seal-confirmation value.
- The reusable Umbral profile, bounded activity component, generated prefab, and isolated preview scene are implemented without live kingdom, save, economy, navigation, or progression integration.
- Status: owner-approved motion reference at `95 / 100`; reference-aligned Unity graybox pass at `92 / 100`; `17 / 17` Architecture EditMode tests passed after the Android/iOS compatibility pass.

## Settlement-kit target

Each realm's first kit should communicate the same functional set:

- Small dwelling.
- Workshop or service building.
- Civic or market structure.
- Straight wall, corner wall, gate, and watchtower.
- Street or path surface with one transition piece.
- Short bridge or utility crossing.
- Human scale and strategic-map silhouette tests.

The concept sheet should prove coordinated function and realm distinction. It does not establish exact module count or grid size.

## Mobile castle-view composition target

Use one shared functional plan across every realm so players learn the kingdom screen once while architecture and terrain preserve realm identity:

- **Upper center / protected high point:** castle keep or approved monumental landmark, visible but not allowed to obscure the working settlement.
- **Central open plaza:** civic or market structure, advisor access, and the clearest visual circulation node.
- **One side near the wall:** workshop, forge, research, storage, and other production structures with direct service access.
- **Opposite quieter side:** dwellings and support buildings grouped into readable neighborhoods rather than scattered evenly.
- **Inner wall edge:** troop training and defensive service structures with fast access to towers and gates.
- **Lower center / camera-facing edge:** main gate and bridge, creating the strongest entrance path and a clear route back toward character-scale exploration.
- **Road hierarchy:** one broad gate-to-plaza-to-keep spine, one cross street, and short local paths. Roads must explain circulation rather than fill every empty space.
- **Open pockets:** deliberate expansion plots and upgrade space so early kingdoms do not look broken or late kingdoms become unreadable.

This placement is a visual and usability proposal until owner approval. It does not assign gameplay functions, economy values, progression gates, timers, or construction slots.

## Continuous castle exploration

The player experience is one continuous zoomable and pannable inner kingdom rather than three exposed zoom screens.

- Pinch continuously changes camera distance within bounded minimum and maximum limits.
- Dragging empty terrain pans the camera across the castle and its immediate approach.
- Tapping a valid element selects it without automatically centering the camera; a separate focus action may smoothly frame it.
- The camera preserves a controlled elevated tilt for reliable touch selection and architecture readability. Unrestricted orbit is not approved by the current direction.
- Internal LOD, culling, label density, shadow, animation, and effect transitions may occur at continuous distance thresholds, but the player should not perceive a mode switch.
- Camera movement must clamp before showing unfinished world edges and must tolerate device safe areas and finger occlusion.
- Releasing a pan or pinch uses restrained inertia and settles quickly enough for accurate selection.

## Direct element interaction

Valid interaction categories may include buildings, construction or expansion plots, walls and gates, resource or activity nodes, approved characters or advisors, and other explicitly sourced objects. The interaction system does not invent what actions each category performs.

- **Select:** one tap shows a clear ground footprint, restrained outline, or material rim plus a compact contextual action surface.
- **Inspect/focus:** an explicit focus action frames the target while preserving the player's continuous zoom freedom.
- **Act:** available actions appear locally or in an edge-safe action tray; blocked or unavailable actions remain visible only when their explanation is useful.
- **Pan safety:** dragging empty ground always navigates. Dragging across an unselected element does not activate or move it.
- **Transform safety:** relocation, rotation, demolition, or other positional/destructive actions require an explicit mode, valid placement feedback, and confirmation according to later gameplay rules.
- **Occlusion:** selected targets behind roofs, towers, vegetation, or walls receive temporary obstruction fading, cutaway, or silhouette support.
- **Density:** labels, icons, ambient characters, particles, and activity markers declutter progressively as the camera pulls back.
- **Accessibility:** selection cannot rely on color alone; important actions require readable shape, state, and touch targets.

## Camera-aware modular consequences

The modular construction sheet must now define more than strategic silhouettes:

- Complete visible sides for every camera-reachable building angle allowed by the controlled camera.
- Selection footprint, tap collider, camera focus anchor, and occlusion group per interactable module.
- LOD and prop-density behavior across continuous distance rather than three player-visible zoom stages.
- Roof, canopy, foliage, wall, and tower pieces that can fade or cut away without exposing broken geometry.
- Walkable-looking entrances, service paths, courtyards, and activity sockets that remain coherent under close inspection.
- Explicit separation between decorative modules and interaction-bearing modules.
- Expansion and construction plots that look intentional when empty, occupied, upgrading, damaged, or blocked.

## Modular construction master target

The first master sheet should compare the same construction system across Stonehold, Eldergrove, Crownlands, and Umbral without choosing exact meter dimensions.

- Use one shared proportional planning grid, compatible footprint classes, and consistent pivot logic across all realms.
- Show a small building assembled from separable foundation, wall bay, corner, entrance, roof, trim, and activity modules.
- Show straight wall, inner corner, outer corner, gate, tower, street edge, and short bridge pieces.
- Show front, controlled-tilt three-quarter, rear-oblique, and top/footprint information sufficient for the approved camera.
- Identify the selection footprint, camera focus anchor, entrance or navigation anchor, local interaction sockets, VFX socket, and roof/occlusion group through a consistent color-and-shape legend without assigning gameplay actions.
- Demonstrate a close presentation mesh, normal gameplay reduction, strategic reduction, and silhouette proxy as internal runtime representations along one continuous zoom range.
- Preserve realm identity through structure and materials while keeping compatible footprints and snapping behavior.
- Treat every number, triangle count, texture size, material count, LOD threshold, and metric dimension as open until a production envelope is separately reviewed.

### Shared construction decision

The recommended direction is a shared hidden grid and footprint family beneath realm-specific construction. This keeps castle placement, camera focus, selection, navigation, and upgrades predictable without making the four realms visual reskins.

Changing to unrelated footprint systems per realm would materially affect layout data, interaction, pathing, camera framing, construction UX, and asset count and therefore requires explicit owner approval.

## Mobile presentation constraints

- Strategic view must read from roofline, footprint, entrance, and one realm-defining structural cue.
- Design LOD0 for inspection, LOD1 for normal gameplay, LOD2 for strategic view, and a silhouette or proxy for extreme distance.
- Merge repeated trim and secondary supports into texture, vertex color, or broad geometry at lower LODs.
- Treat banners, vegetation clusters, conductors, fracture glow, smoke, sparks, and small props as quality-tier details.
- Favor shared trim sheets, atlases, material families, and modular dimensions over unique textures per building.
- Final budgets are provisional until measured in representative scenes on target devices.

## Approval gates

1. Owner approval of the settlement-kit visual direction.
2. Modular-grid and production-envelope proposal.
3. Graybox assembly and camera-distance validation.
4. Material and lighting test on representative mobile hardware.
5. Runtime kit approval after performance, collision, navigation, occlusion, and visual checks.

## Critical direction changes requiring owner approval

- Replacing mystical medieval naturalism with stylized low-poly, cartoon, photoreal-only, or recognizable franchise language.
- Changing the four realm construction grammars or using palette alone as realm identity.
- Merging monumental and ordinary settlement complexity into one universal kit.
- Making magic, particles, or emissive effects necessary for building recognition.
- Selecting final production grid dimensions, platform performance floors, or a new runtime rendering/material architecture.
