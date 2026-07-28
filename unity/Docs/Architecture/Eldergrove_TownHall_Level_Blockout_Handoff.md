# Eldergrove Town Hall Level Blockout Handoff

**Status:** Graybox proof passed; final production model and live binding
implemented

**Date:** 2026-07-27

**Building identity:** `TownHall`

**Stable slot identity:** `kingdom.slot.town-hall`

This handoff records the owner-approved Eldergrove Town Hall source and the
deterministic Level `1`, `6`, and `10` Unity proof. The subsequent final model
now enters the runtime catalog under the same spatial and visual contracts
without inventing gameplay progression, economy, save, timer, quest, worker,
or narrative authority.

## Review assets

- Source:
  `Assets/AL/Art/Architecture/ConceptSheets/architecture_eldergrove_townhall_level_progression_v001.png`
- Builder:
  `Assets/AL/Scripts/Editor/Architecture/EldergroveTownHallLevelBlockoutBuilder.cs`
- Level `1`:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/TownHall/Eldergrove_TownHall_Level01_Blockout.prefab`
- Level `6`:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/TownHall/Eldergrove_TownHall_Level06_Blockout.prefab`
- Level `10`:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/TownHall/Eldergrove_TownHall_Level10_Blockout.prefab`
- Review scene:
  `Assets/AL/Scenes/Prototypes/EldergroveTownHallLevelBlockout.unity`

The review scene is excluded from build settings. The prefabs have no
dependency on the source concept sheet.

The final production implementation is recorded in
`Eldergrove_TownHall_Final_Model_And_Runtime_Binding.md`.

## Major design-direction decisions

1. Level `1` is a complete open civic court with one fixed centered entrance,
   pale-stone public floor, dark timber galleries and canopy, and breathable
   sightlines through the front threshold.
2. Exactly three primary living-root arches carry the civic structure: one
   front arch and two side arches. Their authored count and grounded load paths
   must not become a procedural root tangle.
3. Levels `2`–`6` add cumulative root feet, drainage, an unequal steward wing,
   a sheltered public threshold, a council ridge, and an unequal records wing
   without moving the entrance, pivot, or stable slot.
4. Levels `7`–`9` add upper civic authority, rear service circulation, and
   forecourt integration while retaining the open court and countable primary
   arches.
5. Level `10` adds the Open Crown Arbor. One bent continuation from each
   established arch directly supports a fixed horizontal bronze ring. The
   oculus stays empty, static, and open to the sky; it is not the Workshop seed
   lantern, an orb, portal, shrine focus, or permanent green effect.
6. The graybox stays collider-free. Named selection and navigation volume
   previews validate candidate coverage without becoming live physics or save
   authority.
7. Five opaque instanced graybox material families prove value separation.
   Final production art should merge them into the approved target of one
   architectural atlas plus one localized accent material.

Changing the civic role, fixed entrance, exactly-three-arch identity, open
court, cumulative ladder, empty supported crown, or gameplay-authoritative
level rule requires owner approval.

## Verified engineering properties

- Cumulative groups: Level `1` contains one delta, Level `6` contains six, and
  Level `10` contains all ten.
- Renderer counts: `47`, `77`, and `116`; each stays within the static proof
  ceiling of `120`.
- Materials: five instancing-enabled opaque review materials.
- Static behavior: no `MonoBehaviour`, Animator, particles, audio, light,
  collider, motion vector, light-probe, reflection-probe, or shadow work inside
  the review prefabs.
- Roof safety: both Level `1` roof ridges are mathematically higher than their
  corresponding eaves.
- Spatial safety: all three anchors pass their approved width, depth, and
  height envelopes; Level `10` stays inside `15.2 m × 14.2 m × 12.6 m`.
- Identity: `Entrance`, `CameraFocus`, `Activity_00`, `Output_00`, and both
  collider-review volumes remain stable across anchors.
- Protected form: all anchors retain exactly three primary root arches; the
  Level `10` ring has three grounded supports and contains no central object.
- Dependencies: the source sheet is not a prefab dependency and the review
  scene is outside build settings.

## Validation

| Gate | Result |
| --- | --- |
| Visual verdict | `92 / 100`, pass |
| Static mobile safety | `94 / 100`, pass |
| Eldergrove Town Hall focused EditMode | `18 / 18` |
| Android Architecture EditMode | `166 / 166` |
| iOS Architecture EditMode | `166 / 166` |
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
review assets remain unbound; the separately validated final production prefab
now supplies the live visual binding without adding save data or upgrade
commands.

## Production follow-through

- Final Level `1`–`10` topology, the exactly-three-arch load path, Town
  Hall-specific RGB atlas, non-emissive localized accent, exactly two root
  colliders, four LOD bands, and exact live catalog binding are complete.
- Measure compact iPhone and representative Android framing in the live kingdom
  scene.
- Profile populated-district memory, draw calls, triangles, texture residency,
  and package-size impact on representative devices.
- Keep live upgrade commands blocked on the open progression, economy, save,
  and game-data authority work.

## Realm sequence status

The Crownlands and Umbral Town Hall source translations and graybox gates have
passed under the same shared civic ladder and center-slot contract. Stonehold,
Eldergrove, and Crownlands final production models and live bindings now pass;
Umbral remains separately gated.
