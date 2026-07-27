# Eldergrove Workshop Level Blockout Handoff

**Status:** Owner-approved Level 0–10 source translated into isolated Level 1,
Level 6, and Level 10 Unity blockouts

**Date:** 2026-07-27

**Unity version:** `2022.3.62f3`

**Production source:**
`Assets/AL/Art/Designs/EldergroveWorkshopLevelProgression.md`

**Live-building contract:**
`Docs/Architecture/Kingdom_Building_Level_And_Placement_Design.md`

## Outcome

The approved Eldergrove Workshop progression now has three deterministic Unity
review anchors:

- Level 1 proves the first complete operational building and protected
  root-vault entrance.
- Level 6 proves cumulative lateral growth plus an occupied upper mass without
  erasing the Level 1 structure.
- Level 10 proves the restrained vertical seed-lantern landmark, crown roof,
  and final structural root locks.

These are construction and modeling blockouts. They do not replace the
approved source sheet, become final models, or connect themselves to kingdom
progression, saves, economy, navigation, selection, or live placement.

## Delivered assets

- Deterministic editor builder:
  `Assets/AL/Scripts/Editor/Architecture/EldergroveWorkshopLevelBlockoutBuilder.cs`
- Level 1 blockout:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/Eldergrove_Workshop_Level01_Blockout.prefab`
- Level 6 blockout:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/Eldergrove_Workshop_Level06_Blockout.prefab`
- Level 10 blockout:
  `Assets/AL/Art/Generated/Architecture/Eldergrove/Production/Eldergrove_Workshop_Level10_Blockout.prefab`
- Isolated review scene:
  `Assets/AL/Scenes/Prototypes/EldergroveWorkshopLevelBlockout.unity`
- Focused EditMode tests:
  `Assets/AL/Tests/EditMode/Architecture/EldergroveWorkshopLevelBlockoutTests.cs`

The review scene remains outside Player build settings.

## Validation result

The generated scene was compared against the approved Level 1, Level 6, and
Level 10 source anchors. A first review scored `87 / 100` because the scene
lighting compressed the material hierarchy. One presentation-only revision
introduced a blue-charcoal field, warmer key light, and stronger fill. The
second structured visual verdict passed at `92 / 100`.

The pass confirms:

- a clear foundational, advanced, and landmark silhouette ladder;
- roof planes rising from outer eaves to the structural ridge;
- one protected open root-vault entrance at every built anchor;
- cumulative modules rather than unrelated replacement buildings;
- stone, timber, root, roof, and restrained living-accent separation at mobile
  review scale.

Final curved roof meshes, carved root surfaces, workshop dressing, topology,
metrics, UVs, atlas layout, colliders, and LODs remain final-model work.

## Mobile-safety boundary

The blockouts retain the architecture package's `94 / 100` static-readiness
target:

- `57`, `85`, and `115` renderers at Levels 1, 6, and 10 respectively;
- no `MonoBehaviour`, per-object Animator, particle system, audio source,
  collider, or prefab light;
- no runtime texture or concept-sheet dependency;
- eight or fewer shared instancing-enabled materials;
- shadows, motion vectors, light probes, and reflection probes disabled on
  blockout renderers;
- review scene excluded from Player builds;
- no package, native plugin, save, economy, catalog, or build-setting change.

The `120`-renderer ceiling is a graybox regression guard, not a final art
budget. Final geometry must still be evaluated with LODs, renderer combining,
production materials, populated-district density, and representative Android
and iOS Metal devices. The score is not measured frame-rate, memory, thermal,
or battery evidence.

## Live-production boundary

The next runtime integration must consume authoritative gameplay state:

```text
stable slot identity
+ building definition identity
+ confirmed gameplay level
+ active construction order progress
+ quality tier
→ cumulative module set + Eldergrove motion profile
```

It must not persist a separate visual level or animation stage. Level 0 remains
an unbuilt reserved plot; Level 1 remains the current first built state.

## Major direction gates

Project-owner approval remains required before:

- changing the approved Level 0–10 silhouette ladder or protected root-vault
  entrance;
- changing the Workshop footprint, entrance orientation, or stable slot
  identity;
- selecting final metric dimensions, DCC topology, material/atlas strategy,
  collider boundaries, LOD thresholds, or model-loading strategy;
- retaining or replacing the Level 10 seed-lantern capstone with a different
  landmark;
- raising the common-building mobile class or renderer/material/effect budget;
- allowing visual state to mutate saves, economy, progression, or construction
  completion;
- binding these review prefabs directly as final live-kingdom models.

## Rebuild and review

Build through:

`Another Life > Architecture > Build Eldergrove Workshop Level Blockouts`

Then open:

`Assets/AL/Scenes/Prototypes/EldergroveWorkshopLevelBlockout.unity`

The command-line render path must keep graphics enabled; `-nographics` selects
Unity's null graphics device and cannot produce review evidence.
