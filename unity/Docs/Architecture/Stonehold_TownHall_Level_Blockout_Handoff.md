# Stonehold Town Hall Level Blockout Handoff

**Status:** Graybox proof passed; final production model not started

**Date:** 2026-07-27

**Building identity:** `TownHall`

**Stable slot identity:** `kingdom.slot.town-hall`

This handoff records the owner-approved Stonehold Town Hall source and the
deterministic Level `1`, `6`, and `10` Unity proof. It validates the shared
Town Hall spatial and visual contracts without entering the runtime catalog or
inventing gameplay progression, economy, save, timer, quest, worker, or
narrative authority.

## Review assets

- Source:
  `Assets/AL/Art/Architecture/ConceptSheets/architecture_stonehold_townhall_level_progression_v001.png`
- Builder:
  `Assets/AL/Scripts/Editor/Architecture/StoneholdTownHallLevelBlockoutBuilder.cs`
- Level `1`:
  `Assets/AL/Art/Generated/Architecture/Stonehold/Production/TownHall/Stonehold_TownHall_Level01_Blockout.prefab`
- Level `6`:
  `Assets/AL/Art/Generated/Architecture/Stonehold/Production/TownHall/Stonehold_TownHall_Level06_Blockout.prefab`
- Level `10`:
  `Assets/AL/Art/Generated/Architecture/Stonehold/Production/TownHall/Stonehold_TownHall_Level10_Blockout.prefab`
- Review scene:
  `Assets/AL/Scenes/Prototypes/StoneholdTownHallLevelBlockout.unity`

The review scene is excluded from build settings. The prefabs have no
dependency on the source concept sheet.

## Major design-direction decisions

1. Level `1` is a complete civic hall with one fixed centered clipped entrance,
   correctly pitched paired roof plates, safe steps, and a contained amber
   interior slit.
2. Levels `2`–`6` add cumulative foundation locks, one records wing, a public
   threshold, paired load-bearing buttress towers, a continuous lintel, an
   unequal assembly wing, rear service gallery, and a restrained upper council
   course.
3. Levels `7`–`9` add upper civic authority, rear service circulation, and
   forecourt integration without moving the pivot, entrance, camera focus, or
   collider-review volumes.
4. Level `10` adds the grounded Oathstone Crown through a widened stepped load
   base that intersects the established roof line. Its oath plate and narrow
   amber slit are fixed. It is not a forge chimney, weapon, throne, portal, or
   floating crown.
5. The graybox stays collider-free. Named selection and navigation volume
   previews validate candidate coverage without becoming live physics or save
   authority.
6. A Town Hall review-only amber material avoids changing the approved
   Stonehold Workshop forge material. Final production art should recover this
   value separation inside the Town Hall atlas rather than add a third runtime
   material.

Changing the civic role, fixed entrance, cumulative ladder, grounded crown, or
gameplay-authoritative level rule requires owner approval.

## Verified engineering properties

- Cumulative groups: Level `1` contains one delta, Level `6` contains six, and
  Level `10` contains all ten.
- Renderer counts: `36`, `76`, and `116`; each stays within the static proof
  ceiling of `120`.
- Materials: no more than six instancing-enabled opaque review materials.
- Static behavior: no `MonoBehaviour`, Animator, particles, audio, light,
  collider, motion vector, light-probe, reflection-probe, or shadow work inside
  the review prefabs.
- Roof safety: both Level `1` roof ridges are mathematically higher than their
  corresponding eaves.
- Spatial safety: all three anchors pass their approved width, depth, and
  height envelopes; Level `10` stays inside `15.2 m × 14.2 m × 12.6 m`.
- Identity: `Entrance`, `CameraFocus`, `Activity_00`, `Output_00`, and both
  collider-review volumes remain stable across anchors.
- Dependencies: the source sheet is not a prefab dependency and the review
  scene is outside build settings.

## Validation

| Gate | Result |
| --- | --- |
| Visual verdict | `91 / 100`, pass |
| Static mobile safety | `94 / 100`, pass |
| Stonehold Town Hall focused EditMode | `15 / 15` |
| Android Architecture EditMode | `148 / 148` |
| iOS Architecture EditMode | `148 / 148` |
| iOS deployment floor | `15.0` |
| iOS simulator architecture | ARM64 |

The static mobile-safety score is:

| Category | Score |
| --- | ---: |
| Renderer and batching discipline | `18 / 20` |
| Opaque instanced material discipline | `19 / 20` |
| Sleeping/static effect discipline | `20 / 20` |
| Cumulative hierarchy and LOD readiness | `19 / 20` |
| Spatial, selection, and strategic readability | `18 / 20` |
| **Total** | **`94 / 100`** |

The score applies to graybox structure, not final runtime performance. The
review assets are not bound into the packaged catalog, so this change adds no
live district draw calls, texture residency, save data, or upgrade commands.

## Open production work

- Author final Level `1`–`10` topology at the approved LOD ceilings.
- Create the Town Hall-specific Stonehold atlas and localized accent treatment.
- Replace review volume previews with exactly two final root colliders.
- Measure compact iPhone and representative Android framing in the live kingdom
  scene.
- Profile populated-district memory, draw calls, triangles, texture residency,
  and package-size impact on representative devices.
- Bind the final model directly to confirmed `BuildingState.Level` only after
  the production model and catalog entry pass.
- Keep live upgrade commands blocked on the open progression, economy, save,
  and game-data authority work.

## Next realm

The next source-design translation is Eldergrove Town Hall using the same
shared civic ladder and center-slot contract. It must replace Stonehold mass
and iron load paths with the approved open pale-stone court, three authored
living-root arches, bronze collars, and an open crown arbor without reusing the
Workshop seed-lantern capstone.
