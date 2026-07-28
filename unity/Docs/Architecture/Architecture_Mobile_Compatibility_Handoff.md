# Architecture Mobile Compatibility Handoff

**Status:** Android and iOS prototype-readiness contract

**Date:** 2026-07-24

**Platform baseline updated:** 2026-07-28

**Unity baseline:** `2022.3.62f3`

**Scope:** Four-realm settlement design source, shared construction-state
runtime, four realm-motion grayboxes, all four Workshop Level 1–10 production
models and direct live-kingdom bindings, plus all four Town Hall Level
`1`/`6`/`10` production-direction grayboxes

## Outcome

The approved architecture package is platform-neutral and safe to share with Android and iOS work. The shipped runtime receives no concept-sheet or preview-video dependency, all prototype and production-review scenes remain outside Player build settings, and the shared animation and production-model code uses only Unity APIs supported by Android and iOS Player builds.

This is a static production-readiness statement, not final-device performance approval. The Eldergrove, Stonehold, Crownlands, and Umbral Workshops now supply production meshes, atlas materials, LODs, colliders, and direct binding; simultaneous-building density and representative devices still require profiling.

## Applied mobile optimizations

- Construction entry transforms are cached once instead of rebuilding offsets, rotations, and scales every frame.
- Visibility and cutaway ownership update only when an object's active state actually changes.
- The Umbral activity no longer writes resting emission and then overwrites the same renderers during the same evaluation.
- All generated graybox materials enable GPU instancing.
- Generated graybox renderers opt out of motion vectors, light probes, and reflection probes that the isolated prototypes do not use.
- Static modules use no per-object `Animator`, particles, audio sources, or colliders.
- Eldergrove structural roots use fixed authored segments and stop moving after
  their construction stage settles.
- Eldergrove Level 1, 6, and 10 blockouts grow cumulatively and remain beneath
  a `120`-renderer graybox ceiling.
- The Eldergrove live production model combines each Level 1–10 delta into one
  renderer per active LOD band and uses `10 / 10 / 10 / 3` renderers across
  LOD0–3.
- Eldergrove production topology measures
  `4,984 / 2,852 / 1,200 / 672` triangles across LOD0–3.
- The Stonehold live production model uses the same cumulative renderer
  structure and measures `1,872 / 912 / 504 / 276` triangles across LOD0–3.
- The Crownlands live production model uses the same cumulative renderer
  structure and measures `2,400 / 1,420 / 828 / 316` triangles across LOD0–3.
- The Umbral live production model uses the same cumulative renderer structure
  and measures `1,476 / 816 / 396 / 180` triangles across LOD0–3.
- Each production family uses one non-readable mipmapped RGB 1024 atlas, one
  localized opaque accent material, and exactly two root box colliders.
- The packaged model catalog loads once per city layout engine and binds the
  correct realm prefab from stable `RealmId + BuildingId`; no visual level is
  persisted.
- The packaged catalog also binds one realm motion profile per Workshop. A
  newly confirmed adjacent level animates only its new delta, uses at most four
  cached LOD transforms, and sleeps after at most `1.25` seconds.
- Initial load, stream-in, reconnect, offline reconciliation, same-level
  refresh, and multi-level jump remain settled and do not replay motion.
- Each prototype uses at most one localized, shadowless activity light.
- Concept sheets remain non-readable source references with mipmaps disabled.
- Concept sheets, contact sheets, and videos are not referenced by runtime prefabs.
- Stonehold, Eldergrove, Crownlands, and Umbral prototype scenes remain excluded from production build settings.
- Stonehold, Eldergrove, Crownlands, and Umbral Town Hall grayboxes keep fixed
  anchors, cumulative level groups, opaque instanced review materials, no
  runtime behavior, and no concept-sheet prefab dependency.

## Android and iOS compatibility boundary

| Area | Android | iOS |
| --- | --- | --- |
| Runtime C# | Platform-neutral Unity API surface; no Java, Gradle, Android SDK, or conditional platform branch | Platform-neutral Unity API surface; no Objective-C, Swift, Xcode API, or conditional platform branch |
| Rendering | Shared opaque instanced materials, one RGB atlas, four static LODs, no source texture dependency | Shared opaque instanced materials, one RGB atlas, four static LODs, no source texture dependency |
| Animation | One deterministic controller active only during an authored transition; reduced-motion supported | One deterministic controller active only during an authored transition; reduced-motion supported |
| Packaging | Prototype scenes and design references are not in build settings or prefab dependencies | Prototype scenes and design references are not in build settings or prefab dependencies |
| Production requirement | Validate GLES3/Vulkan target devices with the final Workshop assets at representative district density | Validate Metal target devices with the final Workshop assets at representative district density |

No platform-specific package, native plugin, save-schema change, or build-setting mutation is required by this architecture package. The new packaged ScriptableObject catalog contains direct prefab references only and does not own gameplay progress.

## iOS platform evaluation baseline

The approved product support floor is now **iOS 15.0**. Unity Player Settings
persist that deployment target, and local Simulator exports use the Apple
silicon `arm64` architecture rather than the legacy `x86_64` default.

The evaluation matrix separates compatibility from runtime coverage:

| Evidence | iOS 15 baseline | Current iOS runtime |
| --- | --- | --- |
| Unity compile and Player export | Required with deployment target `15.0` | Required |
| Xcode native build | Required with every application target at or above `15.0` | Required |
| Runtime launch and visual smoke | Required when an iOS 15 runtime or device is available | Required on the installed current Simulator |

Xcode 26.6 supports an iOS 15 deployment target and iOS 15 Simulator testing,
but this workstation currently has only the iOS 26.5 runtime installed. The
Xcode component service no longer offers iOS 15.0 through 15.5 for direct
download on this installation. Until an archived compatible runtime or
physical iOS 15 device is attached, the iOS 15 claim is limited to deployment-
target compilation and native linking; it is not yet an iOS 15 runtime pass.

## Static readiness score

**Prototype mobile safety: `94 / 100`**

The score covers platform-neutral code, stable-state sleep, reduced motion, bounded renderers/materials/lights, GPU-instancing eligibility, unused-render-feature removal, build exclusion, source/runtime separation, and automated regression coverage.

The focused Architecture EditMode suite was re-imported and passed with Unity actively targeting Android and iOS. The remaining six points are withheld until populated-district Player packaging and representative-device profiling are complete. Do not reinterpret `94 / 100` as a measured frame-rate, memory, thermal, or battery result.

## Automated acceptance

`AL.Tests.EditMode.Architecture.ArchitectureMobileReadinessTests`, the
Workshop production-model suites, and the Stonehold, Eldergrove, Crownlands,
and Umbral Town Hall blockout suites verify:

- renderer and material ceilings;
- instancing on every shared prototype material;
- no assigned runtime textures;
- no per-object animators, particles, audio sources, or colliders;
- no motion-vector, light-probe, or reflection-probe work;
- at most one shadowless activity light;
- no prototype scene in enabled Player build settings;
- no concept-sheet dependency from any prototype prefab;
- non-readable concept-sheet imports with mipmaps disabled and native NPOT sizing.
- exact cumulative level-group ownership for Level 1, 6, and 10;
- monotonic geometry growth beneath the Eldergrove graybox ceiling;
- static review prefabs with no gameplay component or prefab light;
- the repaired Eldergrove eave-to-ridge roof direction.
- exact atlas, material, collider, LOD, topology, and renderer contracts;
- Level 1–10 cumulative activation from confirmed gameplay state;
- direct live binding, Level 0 reserved plots, upgrade hold, and visible invalid
  catalog failure.
- one compatible realm motion profile per Workshop catalog binding;
- no transition replay on initial load, invalid data, same-level refresh, or
  multi-level reconciliation;
- one-delta adjacent-level motion, immediate reduced-motion settling, and
  automatic sleep after the bounded transition.

The focused Eldergrove production suite passes `18 / 18` and the focused
Stonehold production suite passes `19 / 19`. The Crownlands production suite
passes `19 / 19`, and the Umbral production suite passes `19 / 19`. The
focused confirmed-level transition suite passes `14 / 14`. The Umbral Town
Hall suite passes `17 / 17`. The expanded Architecture suite passes
`200 / 200` in Unity 2022.3.62f3 while actively targeting Android and again
while actively targeting iOS. All four final Workshop models and every Town
Hall graybox still require populated-kingdom profiling on representative
Android and iOS devices before measured performance approval.

## Production handoff

1. Keep normal mobile play on LOD1 rather than inspection LOD0.
2. Schedule rare operational events at district level; off-screen and far-proxy buildings stay static.
3. Profile multiple visible buildings on representative Android GLES3/Vulkan and iOS Metal devices.
4. Record triangles, visible renderers, draw calls, material switches, texture memory, overdraw, shadow cost, CPU time, GPU time, thermals, and build-size change before measured performance approval.

## Critical direction choices

No new creative direction is required for this compatibility pass.

Project-owner approval is required before:

- weakening a protected silhouette to meet a budget instead of first removing secondary detail;
- replacing the shared lifecycle with realm-specific state machines;
- adding continuous structural motion, screen-wide effects, procedural lightning, or unrestricted procedural growth;
- making a concept sheet or preview render a runtime texture;
- raising mobile renderer, material, light, transparency, or active-building limits without device evidence.
