# Stonehold Workshop Animation Prototype Handoff

**Status:** Approved motion direction implemented as an isolated Unity graybox; production successor delivered

**Date:** 2026-07-24

**Unity version:** 2022.3.62f3

**Motion contract:** `Docs/Architecture/Stonehold_Architecture_Animation_Contract.md`

This slice begins Stonehold Unity validation without changing the approved
architecture direction. It proves rigid construction stages, roof cutaway
ownership, stable completed mass, a contained forge cue, and short functional
bellows and hammer events. It is not production art and is not connected to
gameplay construction, saves, economy, navigation, or the live kingdom.

The production successor is documented in
`Docs/Architecture/Stonehold_Workshop_Final_Model_And_Runtime_Binding.md`.
That separate prefab now supplies the final dimensions, cumulative Level
`1`–`10` geometry, atlas, colliders, LODs, Level `10` capstone, packaged
catalog entry, and direct gameplay-authoritative live-kingdom binding. This
prototype remains isolated animation evidence and is not used as the live
model.

## Delivered assets

- Stonehold activity component:
  `Assets/AL/Scripts/Kingdom/Visuals/Architecture/StoneholdWorkshopStableActivity.cs`
- Rebuild tool:
  `Assets/AL/Scripts/Editor/Architecture/StoneholdWorkshopAnimationPrototypeBuilder.cs`
- Generated prefab:
  `Assets/AL/Art/Generated/Architecture/Stonehold/Stonehold_Workshop_AnimationPrototype.prefab`
- Isolated preview scene:
  `Assets/AL/Scenes/Prototypes/StoneholdWorkshopAnimationPrototype.unity`
- Focused EditMode tests:
  `Assets/AL/Tests/EditMode/Architecture/StoneholdWorkshopAnimationPrototypeTests.cs`

The preview scene remains outside production Player build settings.

## Demonstrated sequence

| Persistent state | Graybox proof |
| --- | --- |
| `PlotPrepared` | Fixed stepped footprint with bounded iron and timber supplies |
| `FoundationSeated` | Broad foundation and paired plinths seat as rigid groups |
| `WallShellLocked` | Three-sided shell, buttresses, clipped entrance arch, lintel, and iron catches establish a complete load path |
| `RoofAndChimneySet` | Two independently seated heavy roof groups rise correctly from eaves to ridge; bounded ribs, ridge, chimney, and cap close the workshop |
| `FittedOut` | Forge, workbench, anvil, bellows, and hammer install as practical modules |
| `Operational` | Contained forge light, one bellows compression, and one short hammer action occur while all structural groups remain still |

The profile duration is an art-review rhythm only and does not set gameplay
construction time.

## Mobile-safety statement

The prototype retains the existing `94 / 100` static-readiness target:

- one shared construction controller and one bounded activity component;
- no per-module Animator, particle system, audio source, or collider;
- one localized shadowless light;
- seven shared instancing-enabled materials with no assigned runtime textures;
- motion vectors, light probes, and reflection probes disabled;
- reduced motion snaps stages and suppresses tool movement;
- scene excluded from Player build settings;
- no package, plugin, native code, save, catalog, or build-setting change.

The editor-only review tool writes a six-stage construction process sheet to
`.omx/state/stonehold-graybox/process-sheet.png`.

This is static prototype evidence, not measured device frame-rate, memory,
thermal, or battery evidence.

## Major direction gates

Project-owner approval remains required before:

- replacing rigid seating with levitation, elastic masonry, or continuous
  structural idle motion;
- choosing final gameplay construction timing or activity frequency;
- adding persistent workers, camera shake, damage, destruction, or repair;
- weakening the protected Stonehold silhouette to meet a budget before
  removing secondary detail;
- raising renderer, material, light, transparency, or active-building budgets
  without representative Android and iOS device evidence;
- connecting this animation prototype directly to production saves, economy,
  navigation, or live kingdom progression instead of using the approved
  production model/catalog boundary.

## Rebuild

Run:

`Another Life > Architecture > Build Stonehold Workshop Animation Prototype`

Open:

`Assets/AL/Scenes/Prototypes/StoneholdWorkshopAnimationPrototype.unity`
