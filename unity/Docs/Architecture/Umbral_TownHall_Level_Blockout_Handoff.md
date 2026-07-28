# Umbral Town Hall Level Blockout Handoff

**Status:** Graybox proof passed; final production model not started

**Date:** 2026-07-28

**Building identity:** `TownHall`

**Stable slot identity:** `kingdom.slot.town-hall`

This handoff records the owner-approved Umbral Town Hall source and the
deterministic Level `1`, `6`, and `10` Unity proof. It validates the shared
Town Hall spatial and visual contracts without entering the runtime catalog or
inventing gameplay progression, economy, save, timer, quest, worker, or
narrative authority.

## Review assets

- Source:
  `Assets/AL/Art/Architecture/ConceptSheets/architecture_umbral_townhall_level_progression_v001.png`
- Builder:
  `Assets/AL/Scripts/Editor/Architecture/UmbralTownHallLevelBlockoutBuilder.cs`
- Level `1`:
  `Assets/AL/Art/Generated/Architecture/Umbral/Production/TownHall/Umbral_TownHall_Level01_Blockout.prefab`
- Level `6`:
  `Assets/AL/Art/Generated/Architecture/Umbral/Production/TownHall/Umbral_TownHall_Level06_Blockout.prefab`
- Level `10`:
  `Assets/AL/Art/Generated/Architecture/Umbral/Production/TownHall/Umbral_TownHall_Level10_Blockout.prefab`
- Review scene:
  `Assets/AL/Scenes/Prototypes/UmbralTownHallLevelBlockout.unity`

The review scene is excluded from build settings. The prefabs have no
dependency on the source concept sheet.

## Major design-direction decisions

1. Level `1` is a complete protected civic hall built from two deliberately
   offset graphite masses, one fixed oblique but readable public entrance,
   safe steps, split supported roof planes, ash-timber gallery structure,
   sparse brass hierarchy, and one small non-emissive darkglass civic inset.
   It is not a portal, ritual shrine, temple, gallows, keep, palace, workshop,
   prison, throne room, or magical machine.
2. Levels `2`–`6` add cumulative foundation locks, unequal records and steward
   galleries, a broader sheltered public threshold, and exactly four thick
   grounded boundary piers. Each pier braces locally into the occupied civic
   roof; pier-to-pier portal frames and perimeter cages are forbidden.
3. Levels `7`–`9` add a restrained upper council course, rear service
   circulation, and oblique forecourt integration without moving the pivot,
   public entrance, camera focus, or collider-review volumes.
4. Level `10` adds the Veiled Accord Yoke. Four explicit load rails carry a
   compact asymmetrical fixed double crossframe close above the occupied roof.
   Its short upper council slit remains truly empty negative space. The yoke is
   not a portal, doorway, gallows, floating ring, darkness effect, bound-
   eclipse apparatus, or full-building violet emission.
5. The graybox stays collider-free. Named selection and navigation volume
   previews validate candidate coverage without becoming live physics or save
   authority.
6. Five opaque instanced graybox material families prove value separation.
   Final production art should merge them into the approved target of one
   architectural atlas plus one tightly localized accent material.

Changing the civic role, fixed oblique entrance, four-pier load path,
cumulative ladder, compact empty-slit yoke, or gameplay-authoritative level
rule requires owner approval.

## Verified engineering properties

- Cumulative groups: Level `1` contains one delta, Level `6` contains six, and
  Level `10` contains all ten.
- Renderer counts: `23`, `60`, and `93`; each stays within the static proof
  ceiling of `120`.
- Materials: five instancing-enabled opaque review materials.
- Static behavior: no `MonoBehaviour`, Animator, particles, audio, light,
  collider, motion vector, light-probe, reflection-probe, or shadow work inside
  the review prefabs.
- Roof safety: both Level `1` main roof slopes rise mathematically from their
  outer eaves toward the ridge.
- Spatial safety: all three anchors pass their approved width, depth, and
  height envelopes; Level `10` stays inside `15.2 m × 14.2 m × 12.6 m`.
- Identity: `Entrance`, `CameraFocus`, `Activity_00`, `Output_00`, and both
  collider-review volumes remain stable across anchors.
- Protected form: Levels `6` and `10` retain exactly four grounded boundary
  piers with four local pier-to-roof braces. Level `10` retains four explicit
  yoke load rails, two compact crossframes, and an empty upper council slit.
- Dependencies: the source sheet is not a prefab dependency and the review
  scene is outside build settings.

## Validation

| Gate | Result |
| --- | --- |
| Visual verdict | `91 / 100`, pass |
| Static mobile safety | `95 / 100`, pass |
| Umbral Town Hall focused EditMode | `17 / 17` |
| Android Architecture EditMode | `200 / 200` |
| iOS Architecture EditMode | `200 / 200` |
| iOS deployment floor | `15.0` |
| iOS simulator architecture | ARM64 |
| Local Apple toolchain | Xcode `26.6`; iOS `26.5` Simulator available |

The concept and deterministic graybox intentionally use different rendering
media, so a pixel diff would measure abstraction and lighting rather than
design fidelity. The structured visual verdict is authoritative for silhouette,
load path, progression, entrance, and protected-category comparison.

The static mobile-safety score is:

| Category | Score |
| --- | ---: |
| Renderer and batching discipline | `19 / 20` |
| Opaque instanced material discipline | `19 / 20` |
| Sleeping/static effect discipline | `20 / 20` |
| Cumulative hierarchy and LOD readiness | `19 / 20` |
| Spatial, selection, and strategic readability | `18 / 20` |
| **Total** | **`95 / 100`** |

The score applies to graybox structure, not final runtime performance. The
review assets are not bound into the packaged catalog, so this change adds no
live district draw calls, texture residency, save data, or upgrade commands.

## Open production work

- Author final Level `1`–`10` topology while preserving the offset protected
  masses, fixed entrance, exactly four grounded piers, local roof braces, and
  compact empty-slit yoke.
- Create the Umbral Town Hall atlas and localized non-emissive darkglass
  accent; violet color, glow, fog, and particles must remain nonessential.
- Replace review volume previews with exactly two final root colliders.
- Author the approved LOD0–3 hierarchy and validate the Town Hall triangle,
  renderer, and material ceilings.
- Measure compact iPhone and representative Android framing in the live
  kingdom scene.
- Profile populated-district memory, draw calls, triangles, texture residency,
  and package-size impact on representative devices.
- Bind the final model directly to confirmed `BuildingState.Level` only after
  the production model and catalog entry pass.
- Keep live upgrade commands blocked on the open progression, economy, save,
  and game-data authority work.

## Realm sequence status

Stonehold, Eldergrove, Crownlands, and Umbral Town Hall source translations
and Level `1`/`6`/`10` graybox gates have now passed under one shared civic
ladder and center-slot contract. The next authorized branch is final
production-model construction and direct live binding, not another realm
graybox.
