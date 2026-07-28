# Eldergrove Atelier Animation Prototype Handoff

**Status:** Approved motion direction implemented as an isolated Unity graybox; Workshop production successor delivered

**Date:** 2026-07-27

**Unity version:** 2022.3.62f3

**Motion contract:** `Docs/Architecture/Eldergrove_Architecture_Animation_Contract.md`

This slice completes the missing Eldergrove proof within the shared four-realm
construction system. It demonstrates crafted preparation, deterministic
grounded root growth, a settled root vault, correctly pitched rigid roof
installation, roof-and-lantern cutaway ownership, and one contained cultivation
cycle. It is not production art and is not connected to gameplay construction,
saves, economy, navigation, or the live kingdom.

The Workshop production successor is documented in
`Docs/Architecture/Eldergrove_Workshop_Final_Model_And_Runtime_Binding.md`.
That separate prefab owns the final Level `1`–`10` geometry and live-kingdom
binding; this Atelier prototype remains isolated motion evidence.

## Delivered assets

- Eldergrove activity component:
  `Assets/AL/Scripts/Kingdom/Visuals/Architecture/EldergroveAtelierStableActivity.cs`
- Rebuild tool:
  `Assets/AL/Scripts/Editor/Architecture/EldergroveAtelierAnimationPrototypeBuilder.cs`
- Generated prefab:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Eldergrove_Atelier_AnimationPrototype.prefab`
- Isolated preview scene:
  `Assets/AL/Scenes/Prototypes/EldergroveAtelierAnimationPrototype.unity`
- Focused EditMode tests:
  `Assets/AL/Tests/EditMode/Architecture/EldergroveAtelierAnimationPrototypeTests.cs`

The preview scene remains outside production Player build settings.

## Demonstrated sequence

| Persistent state | Graybox proof |
| --- | --- |
| `PlotPrepared` | Drained pale-stone footprint, prepared bronze sockets, entrance step, and bounded timber supply |
| `CraftFrameSet` | Stone plinth and wall shell establish the crafted support boundary before biological structure |
| `GuidedRootGrowth` | Two primary supports advance on fixed authored paths from grounded bases, with no random branching |
| `RootVaultSettled` | Mirrored upper arcs meet at one restrained grafted vault and remain structurally still |
| `RoofAndLanternSet` | Two rigid planes rise from outer eaves to the lantern ridge; ten bounded ribs and three cutaway groups close the atelier |
| `CultivationOperational` | A fitted basin receives one slow sap pulse, restrained water response, and protected-leaf unfurl while mature roots stay still |

The profile duration is an art-review rhythm only and does not set gameplay
construction time.

## Mobile-safety statement

The prototype retains the existing `94 / 100` static-readiness target:

- one shared construction controller and one bounded activity component;
- deterministic authored root segments rather than procedural generation;
- no per-module Animator, particle system, audio source, or collider;
- one localized shadowless cultivation light;
- nine shared instancing-enabled materials with no assigned runtime textures;
- motion vectors, light probes, and reflection probes disabled;
- reduced motion snaps construction stages and replaces flowing activity with
  stable values;
- scene excluded from Player build settings;
- no package, plugin, native code, save, catalog, or build-setting change.

The editor-only review tool writes a six-stage construction process sheet to
`.omx/state/eldergrove-graybox/process-sheet.png`.

This is static prototype evidence, not measured device frame-rate, memory,
thermal, or battery evidence.

## Major direction gates

Project-owner approval remains required before:

- replacing fixed authored roots with unrestricted procedural growth;
- allowing mature structural roots, roof planes, lantern, masonry, or bronze
  grafts to move continuously;
- choosing final gameplay construction timing or district activity frequency;
- adding persistent workers, broad foliage simulation, damage, pruning,
  regrowth, or repair;
- weakening the protected root-vault and raised-lantern silhouette to meet a
  budget before removing secondary detail;
- raising renderer, material, light, transparency, or active-building budgets
  without representative Android and iOS device evidence;
- connecting this animation prototype directly to production saves, economy,
  navigation, or live kingdom progression instead of using the approved
  production model/catalog boundary.

## Rebuild

Run:

`Another Life > Architecture > Build Eldergrove Atelier Animation Prototype`

Open:

`Assets/AL/Scenes/Prototypes/EldergroveAtelierAnimationPrototype.unity`
