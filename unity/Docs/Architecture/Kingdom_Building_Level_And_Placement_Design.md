# Kingdom Building Level, Placement, and Presentation Design

**Status:** Owner-approved design direction

**Date:** 2026-07-27

**Primary owner:** Project owner / creative director

**Design authority:** Root `DESIGN.md`

**Runtime dependencies:** Save hardening #137, economy integrity #163, progression integrity #165, and game-data authority #183

This document defines how live kingdom buildings occupy stable locations, progress from an unbuilt plot through Level 10, and consume the approved four-realm construction motion system. It is a visual and interaction contract. It does not approve costs, durations, production rates, prerequisites, save schema fields, or final model assets.

## Approved decisions

1. The Stonehold, Eldergrove, Crownlands, and Umbral prototypes are **realm construction-motion grammars**. They are not final models and are not one-to-one definitions for every building.
2. Gameplay level and the authoritative active construction order determine presentation. A separate visual construction stage is never persisted.
3. Every kingdom location has stable slot identity. List order never determines placement.
4. `Level 0` is an unbuilt reserved plot. `Level 1` is the first complete operational building and preserves the current built baseline. Existing Level 1 state does not visually regress.
5. Final presentation resolves through stable realm, building-definition, level, and quality-tier identities while preserving the mobile-safety requirement above `90/100`.

## Identity and ownership

The production boundary separates four concerns:

| Concern | Owns | Must not own |
| --- | --- | --- |
| Building definition | Function, supported level range, footprint class, module plan, final-model references | Player progress or placement |
| Building progression | Confirmed level and authoritative active order | Visual clip time or realm motion style |
| Building slot | Stable location, grid coordinate, footprint, rotation, entrance orientation | Upgrade level or economy |
| Presentation adapter | Resolving confirmed gameplay state into model modules and realm motion | Saves, resource mutation, construction completion, or repair |

`BuildingSlotId` is the conceptual stable spatial identity. The exact runtime field and migration belong to the progression/save contracts. If relocation is not supported, the versioned kingdom layout definition remains placement authority. If relocation is introduced later, a validated placement transaction becomes authority. Both paths preserve the same slot identity.

## Level 0–10 visual progression

The level ladder is shared across building categories, but each building expresses it through its own function. A Farm adds cultivation, storage, and logistics modules; a Barracks adds training, defense, and muster modules. Realm grammar changes construction and material expression, not the gameplay meaning of the level.

| Level | Stable visual state | Required readable change |
| ---: | --- | --- |
| **0 — Unbuilt** | Reserved plot with realm ground treatment, footprint boundary, entrance orientation, and an intentional empty-state marker | Reads as an available or locked site without implying a functioning building |
| **1 — Foundational** | First complete operational building; minimum credible enclosure, roofline, entrance, and function area | Establishes the building's permanent identity and current built baseline |
| **2 — Reinforced** | Structural supports, weather protection, or service reinforcement | Adds one clear secondary construction cue without changing the core footprint read |
| **3 — Expanded** | First functional bay, yard, lean-to, annex, or storage extension | Expands capacity visibly at strategic distance |
| **4 — Established** | Stronger roofline, vertical element, perimeter treatment, or organized work area | Makes the building read as established rather than temporary |
| **5 — District Anchor** | Secondary public, production, storage, or coordination module | Gives the building a stronger relationship to roads and neighboring slots |
| **6 — Advanced** | Additional structural bay or upper mass with improved operational organization | Creates a meaningful silhouette step while preserving entrance and selection clarity |
| **7 — Signature** | The building category's signature realm mechanism or craft element becomes fully expressed | Combines function and realm identity through structure before VFX |
| **8 — Masterwork** | Refined envelope, durable material upgrade, repair history, and controlled tertiary craft | Shows mastery through construction and material truth, not glow density |
| **9 — Prestige** | Complete logistics, civic, ceremonial, or defensive integration appropriate to the building | Strengthens skyline and district presence without becoming the capstone |
| **10 — Landmark** | Final capstone module and strongest approved silhouette/material refinement | Reads as the maximum-level form while settling into restrained stable operation |

Every adjacent level must remain distinguishable at the strategic kingdom camera through a primary or secondary form change. Materials, activity, icons, and VFX may reinforce the change but cannot be the only difference.

The ladder is implemented as cumulative modular deltas, not eleven unrelated complete models. Level 10 does not automatically convert a common building into a hero-building budget. Asset category and measured device performance still control geometry, materials, lights, particles, and update cost.

## Construction and upgrade presentation

The six shared presentation states remain:

1. `SitePrepared`
2. `BaseStructureEstablished`
3. `SignatureStructureEstablished`
4. `UpperStructureEstablished`
5. `FitoutCompleted`
6. `Operational`

They are resolved presentation states, not saved progression.

### New construction: Level 0 → Level 1

- The reserved plot becomes the localized work site.
- The complete realm construction grammar may play across all six resolved presentation states.
- Completion settles directly into the Level 1 operational model.

### Upgrade construction: Level N → Level N+1

- The confirmed Level N structure remains visible and load-bearing.
- Only the target level's approved module delta participates in the construction stages.
- `SitePrepared` means a localized work zone, access change, scaffold, root guide, conductor line, or ward boundary around that delta; it does not erase the building.
- Returning after streaming, loading, reconnecting, or offline time initializes directly from the authoritative order progress without replaying completed work.
- When no valid active order exists, the building renders its last confirmed level in a settled state.
- A failed, unavailable, or contradictory gameplay snapshot must not be visually presented as a completed upgrade.

Realm profiles own the character of motion. Building definitions and final model packets own which modules change at each level. Presentation clip duration remains independent from gameplay construction duration.

## Stable placement contract

- Each kingdom layout declares stable slot IDs, coordinates, footprint classes, rotation, entrance orientation, and selection bounds.
- A slot remains stable across save reloads, catalog ordering, list normalization, and visual-quality changes.
- Level 0 plots use the same slot and footprint identity that their built forms will occupy.
- Final models must fit their declared footprint and keep entrances, roads, interaction sockets, and navigation boundaries aligned at every level.
- A list reorder, missing row, catalog query order, or dictionary enumeration change must never move a building.
- Moving, swapping, rotating, or demolishing a building requires an explicit future placement mode and validated transaction. Ordinary camera navigation and selection cannot alter placement.

## Final-model resolver contract

The presentation layer resolves a production candidate using:

```text
realm identity
+ building definition identity
+ confirmed visual level
+ quality tier
→ production prefab/module set + realm motion profile
```

The realm motion profile remains separate from model identity. The same Stonehold grammar can construct a Farm, Forge, Barracks, or Town Hall through different module bindings without treating the Stonehold Workshop graybox as their final shape.

A missing or invalid production candidate uses an explicit placeholder/unavailable presentation. It never falls back to a different building, silently invents a model, or mutates gameplay state.

## Save and economy boundary

- Existing Level 1 buildings remain Level 1 and built.
- An unbuilt stable slot presents Level 0 without query-time creation of a Level 1 save row.
- Only an accepted progression transaction may start, complete, cancel, or reconcile construction.
- Resource spend, level change, order/result identity, quest consequences, and persistence follow the owning atomic transaction contracts.
- Presentation observes immutable results and never calls save, economy, quest, or completion services.
- The exact save representation for slots, active orders, and migration remains owned by #137/#165; this design does not add fields.

## Mobile and accessibility requirements

- Preserve the approved architecture mobile-safety score above `90/100`.
- Reuse module families, trims, materials, LODs, and realm motion profiles instead of multiplying complete prefabs.
- Mobile-low may remove workers, loose props, fine activity, particles, transparent layers, secondary lights, and minor module animation before weakening level, function, footprint, or realm identity.
- Far buildings render a static settled level proxy.
- Reduced motion snaps to resolved stable states and retains construction meaning without repeated impacts, flashing, continuous particles, or structural deformation.
- Level differences must survive grayscale, reduced effects, and compact-screen presentation.
- Representative Android and iOS profiling is required before final models become production-ready.

## Acceptance criteria

- Level 0 through Level 10 each have a distinct, function-preserving visual intent.
- Existing Level 1 saves retain the first built operational appearance.
- Realm grammar and building model identity remain separate.
- No persisted visual stage can disagree with gameplay.
- Placement is stable and independent of collection order.
- Later upgrades animate only their module delta.
- Missing definitions, models, slots, or snapshots fail visibly without mutating state.
- Final assets preserve the mobile, LOD, reduced-motion, selection, entrance, and footprint contracts.

## Deferred production decisions

- Exact costs, durations, prerequisites, production rates, cancellation, and refund rules.
- Exact save fields and migration mechanics for slots and progression orders.
- Which building definitions support which footprints and level-specific module deltas.
- Whether player relocation is included in the first production kingdom release.
- Final model source format, loading strategy, asset bundle/addressable policy, and device-tier thresholds.
