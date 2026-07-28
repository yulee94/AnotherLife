# Kingdom Building Level, Placement, and Presentation Design

**Status:** Owner-approved design direction

**Date:** 2026-07-28

**Primary owner:** Project owner / creative director

**Design authority:** Root `DESIGN.md`

**Runtime dependencies:** Integrated local save hardening, economy integrity, progression authority, and game-data authority

This document defines how live kingdom buildings occupy stable locations, progress from an unbuilt plot through Level 10, and consume the approved four-realm construction motion system. It now also records the first gameplay-authoritative local construction transaction. Production rates, cross-building prerequisites, cancellation/refunds, network order identity, and unlisted final-model families remain outside this approval.

## Approved decisions

1. The Stonehold, Eldergrove, Crownlands, and Umbral prototypes are **realm construction-motion grammars**. They are not final models and are not one-to-one definitions for every building.
2. Gameplay level and the authoritative active construction order determine presentation. A separate visual construction stage is never persisted.
3. Every kingdom location has stable slot identity. List order never determines placement.
4. `Level 0` is an unbuilt reserved plot. `Level 1` is the first complete operational building and preserves the current built baseline. Existing Level 1 state does not visually regress.
5. Final presentation resolves through stable realm, building-definition, level, and quality-tier identities while preserving the mobile-safety requirement above `90/100`.
6. Building definitions own exact Level `1`–`10` recipes and durations. The building service is the only authority allowed to accept, persist, complete, or reject a construction order.
7. One active order is allowed per building. Costs are paid in full at acceptance; a known save failure rolls back both wallet and building state, while commit uncertainty freezes additional orders for reconciliation.
8. Missing rows remain Level `0` and query-safe. Existing rows—including the current Level `1` baseline—are never reseeded or silently promoted.

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
- The current production binding keeps the final Level `1` delta hidden until
  gameplay confirms Level `1`.
- Confirmation may apply one short realm-profile settle to the Level `1`
  delta. A first load, stream-in, reconnect, or offline reconciliation displays
  the confirmed model settled and does not replay the transition.
- A future order-progress contract may expand the localized work site across
  all six resolved presentation states without persisting visual stage.

### Upgrade construction: Level N → Level N+1

- The confirmed Level N structure remains visible and load-bearing.
- While the authoritative order is active, the current production binding
  shows only the confirmed Level N model and localized worksite feedback. It
  does not reveal the target delta early.
- When gameplay confirms Level N+1 in the current live session, only the new
  level delta performs one short rigid settle drawn from the realm motion
  profile. Levels `1–2`, `3–4`, `5–6`, `7–8`, and `9–10` map to the five
  persistent realm-motion bands.
- `SitePrepared` means a localized work zone, access change, scaffold, root guide, conductor line, or ward boundary around that delta; it does not erase the building.
- Returning after streaming, loading, reconnecting, or offline time initializes
  directly at the confirmed settled level without replaying completed work.
- When no valid active order exists, the building renders its last confirmed level in a settled state.
- A failed, unavailable, or contradictory gameplay snapshot must not be visually presented as a completed upgrade.

Realm profiles own the character of motion. Building definitions and final
model packets own which modules change at each level. The current confirmation
settle is clamped to `0.35–1.25` seconds and remains independent from gameplay
construction duration.

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
- Only an accepted progression transaction may start, complete, or reconcile construction.
- Resource spend, level change, order/result identity, quest consequences, and persistence follow the owning atomic transaction contracts.
- Presentation observes immutable results and never calls save, economy, quest, or completion services.
- The first local transaction intentionally reuses the existing `BuildingState.Level`, `IsUpgrading`, and `UpgradeCompleteTimestamp` fields. It adds no save field or schema migration and never persists presentation stage.
- The live confirmed-level transition tracker is session-only. It remembers
  only the last observed level per stable realm/slot identity and is discarded
  with the board; it is not a save field or gameplay order.

## Gameplay-authoritative live construction

The accepted local transaction is:

```text
stable BuildingId
+ confirmed BuildingState.Level (missing row = 0)
+ exact next-level definition
+ authoritative wallet snapshot
+ accepted UTC completion timestamp
→ one persisted active construction order
→ runtime-owner reconciliation
→ one persisted confirmed level
→ presentation observes the result
```

- Quotes are read-only and never seed a save row.
- A start request validates the building definition, exact next-level recipe,
  current state, maximum level, wallet integrity, and save writability before
  it can become visible as active work.
- Resource costs and the active-order state are committed in one save attempt.
  A known failed save restores both. A commit-uncertain result is not guessed
  backward or forward; the candidate remains and further orders fail closed.
- The single Bootloader runtime owner reconciles due orders once per UTC second.
  It also reconciles immediately after load and before the ready-profile marker
  is published, so completed offline work first appears at its settled
  confirmed level and does not replay the session-only construction settle.
- Completion increments exactly one level, clears the active timer, and saves.
  It never derives or stores an independent visual level.
- The live command deck issues orders only for `TownHall`, `Farm`,
  `LumberMill`, `Quarry`, `GoldMine`, and `Barracks`. `ManaShrine` and `Mine`
  remain stable reserved slots with explicit unavailable state because the
  game-data catalog does not define them.

### Initial Level 1–10 tuning baseline

The following exact base budgets and UTC durations are now definition data.
They are balance-tuning values, not visual timing; the short realm settle still
uses its separate `0.35–1.25` second presentation envelope.

| Target level | Base budget | Duration |
| ---: | ---: | ---: |
| 1 | 100 | 10 seconds |
| 2 | 175 | 30 seconds |
| 3 | 300 | 2 minutes |
| 4 | 475 | 5 minutes |
| 5 | 700 | 15 minutes |
| 6 | 1,000 | 30 minutes |
| 7 | 1,400 | 1 hour |
| 8 | 1,900 | 2 hours |
| 9 | 2,500 | 4 hours |
| 10 | 3,250 | 8 hours |

Each building applies an authored cost scale and resource mix. Town Hall uses
Stone/Wood/Gold at `45/35/20`; Farm and Lumber Mill use Wood/Stone at `70/30`;
Quarry and Gold Mine use Wood/Stone at `40/60`; Barracks uses
Stone/Wood/Gold at `55/30/15`. Other supported definitions already carry
their own exact recipes for later UI exposure.

### Major design-direction flags

The following are deliberately not inferred from this slice and require an
explicit owner decision before implementation:

- cancellation or partial/full refunds after an accepted spend;
- global builders, parallel-build limits, or queued orders across buildings;
- prerequisite graphs, district locks, or Town Hall gating;
- premium currency speedups or time purchases;
- server-issued order IDs, cross-device conflict resolution, or live-service
  catalog retuning of an already active order;
- demolition, relocation, rotation, or slot swapping.

## Implementation validation

| Gate | Result |
| --- | --- |
| Gameplay-authoritative construction EditMode | `11 / 11`, pass |
| Related command, containment, save-semantic, and economy EditMode | `170 / 170`, pass |
| Android-targeted Architecture EditMode | `286 / 286`, pass |
| iOS-targeted Architecture EditMode | `286 / 286`, pass |
| Static architecture mobile safety | `94 / 100`, pass |
| Unity iOS Player export | pass |
| Xcode 26.6 unsigned ARM64 native build | pass |
| Native deployment floor | `arm64-apple-ios15.0` |

The static score is unchanged because this slice adds platform-neutral
gameplay/UI logic and no renderer, material, texture, light, collider, LOD, or
loading-budget expansion. It is not a physical-device performance claim.

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
- Later upgrades animate only their newly confirmed module delta.
- Missing definitions, models, slots, or snapshots fail visibly without mutating state.
- Final assets preserve the mobile, LOD, reduced-motion, selection, entrance, and footprint contracts.

## Deferred production decisions

- Cross-building prerequisites, production rates, cancellation, and refund rules.
- Network order identity, cross-device conflict resolution, and any migration
  required by a future server-authoritative construction record.
- Whether a future authoritative order-progress snapshot should expose
  in-progress target-delta construction before gameplay confirms the level.
- Which building definitions support which footprints and level-specific module deltas.
- Whether player relocation is included in the first production kingdom release.
- Final model source format, loading strategy, asset bundle/addressable policy, and device-tier thresholds.
