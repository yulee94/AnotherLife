# Architecture Mobile Compatibility Handoff

**Status:** Android and iOS prototype-readiness contract

**Date:** 2026-07-24

**Unity baseline:** `2022.3.62f3`

**Scope:** Four-realm settlement design source, shared construction-state runtime, Crownlands Stormwright graybox, and Umbral Veilwright graybox

## Outcome

The approved architecture package is platform-neutral and safe to share with Android and iOS work. The shipped runtime receives no concept-sheet or preview-video dependency, both prototype scenes remain outside Player build settings, and the shared animation code uses only Unity APIs supported by Android and iOS Player builds.

This is a prototype-readiness statement, not final-device performance approval. The grayboxes prove the control pattern and visual direction; production meshes, textures, LODs, shaders, simultaneous-building density, and real devices still require profiling.

## Applied mobile optimizations

- Construction entry transforms are cached once instead of rebuilding offsets, rotations, and scales every frame.
- Visibility and cutaway ownership update only when an object's active state actually changes.
- The Umbral activity no longer writes resting emission and then overwrites the same renderers during the same evaluation.
- All generated graybox materials enable GPU instancing.
- Generated graybox renderers opt out of motion vectors, light probes, and reflection probes that the isolated prototypes do not use.
- Static modules use no per-object `Animator`, particles, audio sources, or colliders.
- Each prototype uses at most one localized, shadowless activity light.
- Concept sheets remain non-readable source references with mipmaps disabled.
- Concept sheets, contact sheets, and videos are not referenced by runtime prefabs.
- Crownlands and Umbral prototype scenes remain excluded from production build settings.

## Android and iOS compatibility boundary

| Area | Android | iOS |
| --- | --- | --- |
| Runtime C# | Platform-neutral Unity API surface; no Java, Gradle, Android SDK, or conditional platform branch | Platform-neutral Unity API surface; no Objective-C, Swift, Xcode API, or conditional platform branch |
| Rendering | Shared opaque materials, instancing enabled, bounded localized light, no prototype texture dependency | Shared opaque materials, instancing enabled, bounded localized light, no prototype texture dependency |
| Animation | One deterministic controller active only during an authored transition; reduced-motion supported | One deterministic controller active only during an authored transition; reduced-motion supported |
| Packaging | Prototype scenes and design references are not in build settings or prefab dependencies | Prototype scenes and design references are not in build settings or prefab dependencies |
| Production requirement | Validate GLES3/Vulkan target devices after final meshes, LODs, materials, and district density exist | Validate Metal target devices after final meshes, LODs, materials, and district density exist |

No platform-specific package, native plugin, save change, catalog change, or build-setting mutation is required by this architecture package.

## Static readiness score

**Prototype mobile safety: `94 / 100`**

The score covers platform-neutral code, stable-state sleep, reduced motion, bounded renderers/materials/lights, GPU-instancing eligibility, unused-render-feature removal, build exclusion, source/runtime separation, and automated regression coverage.

The focused Architecture EditMode suite was re-imported and passed with Unity actively targeting Android and iOS. The remaining six points are withheld because full Player packaging and representative-device profiling cannot be meaningful until production geometry and district density exist. Do not reinterpret `94 / 100` as a measured frame-rate, memory, thermal, or battery result.

## Automated acceptance

`AL.Tests.EditMode.Architecture.ArchitectureMobileReadinessTests` verifies:

- renderer and material ceilings;
- instancing on every shared prototype material;
- no assigned runtime textures;
- no per-object animators, particles, audio sources, or colliders;
- no motion-vector, light-probe, or reflection-probe work;
- at most one shadowless activity light;
- no prototype scene in enabled Player build settings;
- no concept-sheet dependency from either prefab;
- non-readable concept-sheet imports with mipmaps disabled and native NPOT sizing.

The `17 / 17` passing Architecture suite was run under both the Android and iOS Unity build targets. It also verifies the six-state shared lifecycle, direct persistent-state initialization, stable-state sleep, reduced motion, cutaway ownership, fixed Umbral convergence, and visual renderer ceilings.

## Production handoff

1. Bind production meshes to the approved construction groups and preserve the protected realm silhouettes.
2. Author LOD0 through far proxy using `FourRealm_Modular_Construction_Envelope.md`; normal mobile play should use LOD1 rather than inspection LOD0.
3. Replace graybox material families with approved atlased or trim-sheet production materials and platform texture overrides.
4. Schedule rare operational events at district level; off-screen and far-proxy buildings stay static.
5. Profile multiple visible buildings on representative Android GLES3/Vulkan and iOS Metal devices.
6. Record triangles, visible renderers, draw calls, material switches, texture memory, overdraw, shadow cost, CPU time, GPU time, thermals, and build-size change before production approval.

## Critical direction choices

No new creative direction is required for this compatibility pass.

Project-owner approval is required before:

- weakening a protected silhouette to meet a budget instead of first removing secondary detail;
- replacing the shared lifecycle with realm-specific state machines;
- adding continuous structural motion, screen-wide effects, procedural lightning, or unrestricted procedural growth;
- making a concept sheet or preview render a runtime texture;
- raising mobile renderer, material, light, transparency, or active-building limits without device evidence.
