# A7 Static Player-Experience Audit and First Visual Prototype

**Status date:** 2026-07-27
**Primary mode:** Codex engineering (A7 visual experience)
**Evidence base:** current `origin/main`, open issues and PRs, production scene YAML, runtime UI/camera code, focused Unity EditMode tests, and isolated renderer captures. This is a static first-pass audit, not a substitute for an integrated player playtest.
**Scope:** Boot, Realm Selection, Kingdom, Champion Arena, UI architecture, camera architecture, feedback, accessibility, and presentation performance.

## Current-state assessment

AnotherLife already has unusually broad runtime presentation logic: Boot and realm-selection flows, a 2.5D Kingdom command board, a playable Champion Arena, combat HUD, target feedback, result panels, runtime weather, quality tiers, and cinematic intro cues. The dominant quality problem is not feature absence. It is that production visuals are assembled mostly from runtime-created Unity primitives, flat colors, generated UI panels, and solid-color camera backgrounds. This makes otherwise substantial systems read as a sandbox prototype.

The first safe improvement is therefore a presentation foundation around the playable Champion Arena. It improves visible depth, surface response, camera reliability, and atmosphere without changing combat, quests, saves, catalogs, narrative, balance, or shared integration files.

## Prioritized findings

| Priority | Problem and player impact | Evidence and systems | Recommended solution | Risk / dependencies / A1 approval |
|---|---|---|---|---|
| Critical | The world and combatants visibly expose Unity primitives, making the playable slice read as a construction sandbox rather than a commercial RPG. This weakens immersion and perceived product maturity immediately. | `ChampionArenaSceneController.BuildArena`, `BuildArenaEnvironment`, boss/dummy construction, `ProceduralChampionModelBuilder`; repeated `GameObject.CreatePrimitive` calls. | Establish a reusable world-presentation layer, replace hero assets in staged passes, use authored silhouettes/materials, and retain procedural fallbacks only for missing assets. | **High.** Depends on approved character/environment source assets and import budgets. **A1 approval required** for broad asset replacement. |
| Critical | Camera obstruction is not resolved; walls or terrain can sit between the player and camera. This can hide combat information and cause clipping. | `CameraFollow.LateUpdate` previously positioned the camera directly at its desired orbit point with no cast or obstruction correction. | Sphere-cast from the player pivot, pull the camera in with padding, preserve smoothing, and retain deterministic restoration after cinematics. | **Medium.** Physics-mask tuning required per scene. No shared files. **A1 approval not required** for the isolated safety fix. |
| High | Arena depth ends at a circular primitive platform against a flat background. The world has little scale, horizon, terrain, or geographic context. | `ChampionArenaSceneController` used `CameraClearFlags.SolidColor`; arena set dressing stops near the combat boundary. | Add deterministic basin terrain, distant citadel silhouette, atmospheric sky, fog, and bounded quality-tier scatter outside gameplay space. | **Medium.** Rendering and collider budgets; no gameplay dependency. **A1 approval required** before expanding this into a full world-art direction. |
| High | Most world surfaces use uniform colors without albedo variation, so forms read as plastic blocks even where lighting is present. | `ApplyMaterial` creates a Standard material and sets only color, metallic, smoothness, and occasional emission. | Introduce reusable tiled surface variation now; later replace with approved authored PBR material sets and normal/roughness maps. | **Medium.** Runtime texture generation has bounded startup and memory cost. Authored replacement needs asset-source approval. **A1 approval required** for the final material language. |
| High | The Kingdom screen presents a dense command dashboard immediately, competing with the 2.5D world board and producing permanent screen coverage. | `KingdomSceneController.BuildRuntimeUi` builds several simultaneous status panels plus a large command deck. | Move to contextual panels, preserve a compact realm/resource strip, let selected districts open focused detail, and retain explicit unavailable states. | **High.** Crosses multiple interaction flows and input states. **A1 approval required** for redesign. |
| High | Quest and dialogue data exist, but no complete player-facing dialogue camera/presentation system is visible in the production flow. Major and minor conversations therefore lack differentiated staging. | Narrative definitions and NVS runtime exist; production UI files do not expose a reusable dialogue presenter/camera director. | Specify lightweight world dialogue and major-scene cinematic dialogue as two modes, with skip, interruption, subtitle, focus, and restoration contracts. | **High.** Depends on NVS source/state events and camera ownership. **A1 approval required** before architecture work. |
| High | Inventory, character, map, quest log, pause, settings, rebinding, and save/load screens are not represented as complete production surfaces in the Unity UI inventory. Players cannot yet navigate a coherent full journey. | Repository UI inventory is concentrated in Boot, Realm Selection, Kingdom, Arena, and Warmaster folders; no complete production menu suite was found. | Build one shared navigation shell and design tokens, then deliver each menu as a separate focused slice. | **High.** Depends on authoritative data/query contracts and input policy. **A1 approval required** for shell direction. |
| High | Camera shake exists across attacks and boss events but had no player-facing comfort scale. Repeated shakes can reduce comfort and aim readability. | `SkillEffectFactory`, `BossDummyAI`, and `ChampionController` call `CameraFollow.AddShake`. | Apply a conservative default scale immediately; expose Off/Reduced/Full in accessibility settings later. | **Low now / Medium later.** No save change in this slice. Persisted settings need a dedicated contract. **A1 approval not required** for conservative default reduction; required for settings architecture. |
| Medium | Realm Selection is readable but built from fixed runtime dimensions and legacy text components. Long localized copy, controller focus, and narrow aspect ratios need explicit verification. | `RealmSelectionController` creates fixed-size cards and legacy `UnityEngine.UI.Text`; no localization stress tests were found. | Add layout-content sizing, controller-first focus order, safe-area handling, and pseudo-localization tests. | **Medium.** Depends on localization and input contracts. **A1 approval not required** for compatibility fixes. |
| Medium | HUD information hierarchy is extensive but potentially over-instrumented: health/mana, skills, cast state, target lock, pressure, feed, goals, timer, intro, inspection, damage edges, and results all coexist. | `ChampionArenaSceneController` contains a large runtime HUD builder and many simultaneous state layers. | Define combat-state visibility rules, collapse secondary text outside relevant states, and validate at 16:9, ultrawide, 16:10, and mobile safe areas. | **Medium.** Requires screenshot/playtest evidence and resolution matrix. **A1 approval required** for hierarchy redesign. |
| Medium | Runtime UI construction duplicates panel, button, typography, and texture creation logic across screens, increasing inconsistency and canvas/material churn. | Separate helper stacks exist in `BootController`, `RealmSelectionController`, `KingdomSceneController`, and `ChampionArenaSceneController`. | Create a data-driven UI style asset and reusable components after the active phase gate permits it; avoid a one-shot rewrite. | **High migration risk.** Depends on stable screens and tests. **A1 approval required** for system migration. |
| Medium | Input is a mix of legacy axes, direct key checks, touch checks, and runtime Button events. Predictable controller focus restoration and rebinding are not yet a unified contract. | `CameraFollow`, `ChampionController`, combat UI, and scene controllers use direct `Input` APIs and EventSystem checks. | Specify action maps and focus rules, then migrate one flow at a time while preserving touch. | **High.** Input paradigm change. **A1 approval required.** |
| Medium | Performance scales bots, dummies, particles, markers, shadows, and texture mip limits, but runtime-created materials/textures and many individual renderers can increase draw calls and allocations. | `RuntimePlatformQualityController`; numerous runtime primitives/material instances; generated surface textures. | Cache textures/material families, use shared materials where state permits, introduce LOD/static batching for authored assets, and profile representative devices. | **Medium.** Requires Unity Profiler/device evidence. **A1 approval not required** for isolated optimization. |
| Low | Main-menu identity is currently expressed as a boot/loading sequence rather than a complete menu with Continue/New/Settings/Accessibility/Exit. | `BootController` transitions automatically after splash. | Deliver a dedicated main-menu proposal after save/profile contracts stabilize. | **Medium.** Depends on profile/save policy. **A1 approval required** for direction. |
| Low | Cinematic cues and letterboxing exist, but reusable shot definitions, skip contracts, and restoration tests are not yet a general framework. | Arena intro uses controller-owned cues and `CameraFollow.SetCinematicShot`; no project-wide camera-shot asset system was found. | First harden restoration/skip behavior in the existing intro; generalize only after two validated sequences demonstrate the common contract. | **Medium.** Timeline/Cinemachine dependency is optional and currently absent. **A1 approval required** for framework replacement. |

## Coverage of the requested 20 surfaces

1. Main menu — incomplete production menu; Boot splash only.
2. In-game HUD — extensive, functional foundation; hierarchy and resolution validation needed.
3. Quest presentation — runtime state exists; complete tracker/log flow remains incomplete.
4. Dialogue presentation — data exists; reusable production presenter not found.
5. NPC interaction — no complete production prompt-to-dialogue flow found.
6. Interaction prompts — contextual command buttons exist; world prompt system remains incomplete.
7. Inventory and character screens — no complete production surfaces found.
8. Map and navigation — objective markers exist; full map/minimap/navigation flow remains incomplete.
9. Exploration camera — board and arena cameras exist; no complete free-world exploration camera.
10. Combat camera — functional orbit/cinematic base; obstruction fix delivered in this slice.
11. Dialogue camera — no reusable production system found.
12. Cinematic camera — arena-specific base exists; general skip/restoration framework incomplete.
13. Scene transitions — Boot → Realm Selection/Kingdom exists; broader fades/loading/error recovery need validation.
14. Player feedback — combat HUD, shake, floating text, pressure, defeat, and reward feedback are substantial.
15. Visual consistency — colors and panel motifs recur, but duplicated builders and primitive world art reduce cohesion.
16. Accessibility — touch and reduced shake baseline exist; settings, text scale, contrast, subtitles, and reduced motion remain incomplete.
17. Input navigation — mouse/keyboard/touch exist; controller focus and rebinding remain incomplete.
18. Performance risks — quality tiers exist; renderer/material/canvas costs require profiling.
19. UI architecture — runtime imperative Canvas construction, duplicated helpers, no central style authority.
20. Camera architecture — board camera plus arena follow/cinematic mode; no general camera service or shot assets.

## Implemented first prototype

**Player-facing outcome:** the Champion Arena prototype now sits inside a textured atmospheric citadel basin with a connected skyline, faceted geology, sparse wind-carved vegetation, and quality-tiered silhouette budgets rather than ending at a flat sandbox boundary. The combat camera pulls inward before intersecting world geometry.

Acceptance criteria:

- deterministic terrain geometry and set-dressing counts;
- high and reduced-quality geometry budgets;
- a shared chamfered-stone mesh replaces hard cube edges in arena architecture;
- the high tier uses 2,401 terrain vertices, 34 rocks, 28 trees, 11 towers, and 10 curtain-wall spans;
- the reduced tier uses 625 terrain vertices, 16 rocks, 14 trees, 7 towers, and 6 curtain-wall spans;
- no quest, combat, reward, balance, save, or narrative mutation;
- no new package or external runtime dependency;
- camera obstruction resolution uses bounded sphere casts;
- camera shake is reduced by default without removing event feedback;
- project imports, reloads assemblies, and runs the focused tests without compiler errors in Unity 2022.3.62f3;
- broad redesign remains in a draft PR and is not production-approved pending A1 review.

## Original AnotherLife adaptation

The implementation uses a dusk citadel basin, weathered monolith silhouettes, restrained blue/ember atmosphere, and AnotherLife’s existing realm-accent language. The reference principle is MMO-scale environmental depth and immediately readable combat space—not any World of Warcraft asset, map, UI layout, icon, texture, character, animation, terminology, or camera sequence.

## Visual evidence

- [Player-scale arena frame](Architecture/Previews/a7_champion_arena_gameplay_v001.png)
- [Environment overview](Architecture/Previews/a7_champion_arena_world_foundation_v001.png)

These are deterministic editor-rendered prototype captures using the production arena construction path and runtime lighting configuration. They are not claims of a finished authored-asset world, a representative HUD capture, or an integrated playtest.

## Validation and evidence limits

- Passed: Unity 2022.3.62f3 headless import, script compilation, domain reload, and both evidence renders; process exited `0`.
- Passed: focused EditMode suite `AL.Tests.EditMode.RuntimeWorldPresentationTests`: **5/5**.
- Covered: texture caching/bounds, deterministic high/low scene budgets, idempotent backdrop construction, shared chamfered geometry, and a camera-obstruction pull-in case.
- Passed: no shared-lock file is changed.
- Passed: open PRs #334 and #335 were inspected; neither declared this file scope.
- Not yet performed: live production-arena gameplay capture with HUD, GPU/CPU profiler capture, device matrix, controller/touch pass, or integrated user playtest.
- Known prototype risks: the presentation terrain collider still needs gameplay-boundary validation; runtime-generated materials need profiler evidence; camera collision layers need production tuning; and the procedural art remains a bridge to approved authored assets rather than final commercial world art.

## A7 status report

**Current objective:** Demonstrate an original, scalable fantasy RPG world-presentation direction that replaces the playable arena’s flat sandbox boundary.

**Current problem:** Runtime primitives, flat surfaces, and a solid background undercut otherwise functional combat, UI, and feedback systems.

**Proposed solution:** Review this isolated prototype and its evidence, then—only after approval—continue with authored environment assets and a separately scoped HUD/camera polish plan.

**World of Warcraft reference principle:** readable fantasy combat space, layered environmental scale, contextual information, and stable third-person camera behavior.

**Original AnotherLife adaptation:** realm-accented dusk citadel basin, procedural weathered geology, restrained arcane signals, and existing AnotherLife combat language.

**Systems affected:** Champion Arena presentation and lighting, deterministic runtime surface/mesh generation, combat camera collision/comfort, and focused EditMode coverage.

**Risks:** runtime material and collider cost, production collision-layer tuning, procedural art remaining visibly simpler than authored assets, and unperformed device profiling.

**Dependencies:** user/A1 visual approval; later approved environment/character source assets; no active shared-file lock.

**Testing plan:** completed focused EditMode tests and deterministic captures; still required are arena playthrough, camera obstruction cases against production layers, four aspect ratios, controller/touch, and high/low-tier profiler comparison.

**Approval needed from A1:** approve, revise, or reject the citadel-basin visual direction and decide whether it may remain connected to the production arena while authored environment assets are developed.

**Current status:** Prototype complete and draft PR open; validation is current, but the visual direction is not approved or merged.
