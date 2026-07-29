# A7 Static Player-Experience Audit and First Visual Prototype

**Status date:** 2026-07-29
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
| Medium | HUD information hierarchy is over-instrumented: health/mana, skills, cast state, target lock, pressure, feed, goals, timer, appearance forge, actions, navigation, inspection, damage edges, and results coexist. At 1600x900 the Forge overlaps Combat Actions and the Kingdom button, the boss label runs beneath its bars, and loadout copy intrudes into the skill row. | `ChampionArenaSceneController.BuildHud`; retained `realm_crownlands_hud.png` reproduces the layout defects. | Define combat-state visibility rules, separate customization from active combat, repair the boss/loadout layout, and validate at 16:9, ultrawide, 16:10, and mobile safe areas. | **Medium.** Requires screenshot/playtest evidence and resolution matrix. **A1 approval required** for hierarchy redesign. |
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

**Player-facing outcome:** the Champion Arena prototype now sits inside a textured atmospheric citadel basin with a connected skyline, faceted geology, sparse wind-carved vegetation, and quality-tiered silhouette budgets rather than ending at a flat sandbox boundary. The combat camera resolves its final smoothed and shaken position before intersecting world geometry.

Acceptance criteria:

- deterministic terrain geometry and set-dressing counts;
- high and reduced-quality geometry budgets;
- presentation terrain has no physics collider and keeps a 12 m inner combat clearance below the arena floor;
- a shared chamfered-stone mesh replaces hard cube edges in arena architecture;
- one shared 64 px mipmapped texture releases its CPU copy after upload;
- two shared surface-material variants use per-renderer property blocks instead of per-object material instances;
- the high tier uses 2,401 terrain vertices, 34 rocks, 28 trees, 11 towers, and 10 curtain-wall spans;
- the reduced tier uses 625 terrain vertices, 16 rocks, 14 trees, 7 towers, and 6 curtain-wall spans;
- `mobile_low`, `mobile_standard`, and `desktop_low` select the reduced presentation budget;
- no quest, combat, reward, balance, save, or narrative mutation;
- no new package or external runtime dependency;
- camera obstruction resolution uses bounded sphere casts after smoothing and shake, without forcing a minimum distance through close walls;
- camera shake is reduced by default without removing event feedback;
- scene exit restores prior sky, ambient, reflection, and fog state and releases generated meshes, materials, texture, and sky material;
- broad redesign remains in a draft PR and is not production-approved pending A1 review.

## Original AnotherLife adaptation

The implementation uses a dusk citadel basin, weathered monolith silhouettes, restrained blue/ember atmosphere, and AnotherLife’s existing realm-accent language. The reference principle is MMO-scale environmental depth and immediately readable combat space—not any World of Warcraft asset, map, UI layout, icon, texture, character, animation, terminology, or camera sequence.

## Visual evidence

- [Player-scale arena frame](Architecture/Previews/a7_champion_arena_gameplay_v001.png)
- [Environment overview](Architecture/Previews/a7_champion_arena_world_foundation_v001.png)
- [Desktop/standard non-reduced arena](Architecture/Previews/A7_Presentation_Evidence_v002/arena_desktop_high.png)
- [Mobile/reduced-tier simulation](Architecture/Previews/A7_Presentation_Evidence_v002/arena_mobile_reduced.png)
- [Staged Crownlands-themed Champion HUD](Architecture/Previews/A7_Presentation_Evidence_v002/realm_crownlands_hud.png)
- [Close-obstruction camera pull-in](Architecture/Previews/A7_Presentation_Evidence_v002/camera_close_obstruction.png)
- [Smooth obstruction recovery](Architecture/Previews/A7_Presentation_Evidence_v002/camera_recovery.png)
- [Post-shake collision resolution](Architecture/Previews/A7_Presentation_Evidence_v002/camera_post_shake_collision.png)
- [Machine-readable metrics](Architecture/Previews/A7_Presentation_Evidence_v002/a7_presentation_evidence_metrics.json)
- [Human-readable metrics](Architecture/Previews/A7_Presentation_Evidence_v002/a7_presentation_evidence_metrics.txt)
- [A7 18-case NUnit XML](Architecture/Previews/A7_Presentation_Evidence_v002/A7-RuntimeWorldPresentation-18cases-20260729.xml)
- [A7 18-case Unity log](Architecture/Previews/A7_Presentation_Evidence_v002/A7-RuntimeWorldPresentation-18cases-20260729.log)
- [Champion Realm 11-case NUnit XML](Architecture/Previews/A7_Presentation_Evidence_v002/A7-ChampionRealmContext-11cases-20260729.xml)
- [Champion Realm 11-case Unity log](Architecture/Previews/A7_Presentation_Evidence_v002/A7-ChampionRealmContext-11cases-20260729.log)
- [Capture Unity log](Architecture/Previews/A7_Presentation_Evidence_v002/A7-PresentationEvidence-v002-20260729.log)
- [Blocked PlayMode attempt log](Architecture/Previews/A7_Presentation_Evidence_v002/A7-CameraFollow-PlayMode-1case-20260729.log)

The two `v001` images are retained as baseline prototype frames. The six `v002` images are deterministic 1600x900 Editor renders using the production arena construction, lighting, HUD layout, quality-tier, and camera-collision paths after the 2026-07-29 safety/lifecycle correction. The Crownlands HUD image stages realm, label, and bar state through the capture harness; it does not claim live service resolution, which is covered separately by `ChampionRealmContextTests`. It intentionally preserves the current production layout so the overlap defects remain reviewable. The camera evidence is self-checking: close obstruction resolves 8.60 m to 3.27 m, 90 fixed 60 Hz smoothing steps recover to 8.60 m after removal, and a 1.15 m shake request still resolves to 3.27 m behind the obstruction. A failed acceptance condition aborts the evidence command instead of publishing a misleading frame.

## Validation and evidence limits

- Prior baseline passed: Unity 2022.3.62f3 import, script compilation, domain reload, both evidence renders, and focused EditMode suite `AL.Tests.EditMode.RuntimeWorldPresentationTests`: **5/5**.
- Current correction passed in Unity Test Runner: `AL.Tests.EditMode.RuntimeWorldPresentationTests`: **18/18**, including out-of-order nested `SceneLease` disposal, final-lease baseline restoration, target-hierarchy exclusion, close-wall safety, true `SmoothDamp` recovery through the production resolver, post-shake collision, mobile-standard budget selection, render-state restoration, generated-resource release, and Unity-resource construction lifecycle checks.
- Merged Champion Realm regression passed in Unity Test Runner: `AL.Tests.EditMode.ChampionRealmContextTests`: **11/11**.
- Retained evidence includes NUnit XML and full Unity logs for both suites plus the deterministic capture log in `Architecture/Previews/A7_Presentation_Evidence_v002`.
- One frame-stepped PlayMode attempt entered Play Mode but made no frame progress for more than ten minutes. It was terminated without results. The recovery regression therefore uses the production `ResolveFollowCameraPosition` path with explicit fixed delta time in EditMode; the failed PlayMode log is retained, and no PlayMode-pass claim is made.
- Passed: no shared-lock file is changed.
- Passed: updated onto current `main@c2de8a65f0b89a4cf9ce6cb236b31990f2f55807`; PRs #362 and #364 are non-overlapping narrative documents, and PR #363 changes non-overlapping Kingdom layout tests. No A7 file conflict was introduced.
- Capture host: Unity 2022.3.62f3, Windows 11, AMD Radeon integrated graphics using Direct3D 11 with 3,682 MB reported graphics memory.
- Recorded Editor CPU wall times, scene counts, Unity process memory totals, and scenario-owned resource bytes are in the retained metrics. The desktop-standard non-reduced versus mobile-low reduced construction comparison uses warmed, alternating-order medians of three samples; render and other construction times remain single diagnostic samples. The tiers use 295 versus 240 renderers, 5,808 versus 3,148 vertices, and 653,027 versus 393,939 owned-resource bytes.
- GPU timing is explicitly unavailable because synchronous Editor `Camera.Render` wall time is not a reliable cross-platform GPU measurement. Unity process totals include Editor overhead, and the CPU samples are evidence snapshots rather than device frame-time benchmarks.
- Not yet performed: physical mobile/low-spec profiling, aspect ratios beyond 1600x900 16:9, controller navigation, touch navigation, reduced-motion interaction, camera-shake settings interaction, high-contrast validation, subtitle settings, UI scaling, or integrated user playtest.
- Known prototype risks: production collision-layer tuning, missing physical-device/GPU evidence, unverified alternate input/aspect/accessibility paths, and procedural art remaining visibly simpler than approved authored assets.

## A7 status report

**Current objective:** Demonstrate an original, scalable fantasy RPG world-presentation direction that replaces the playable arena’s flat sandbox boundary.

**Current problem:** Runtime primitives, flat surfaces, and a solid background undercut otherwise functional combat, UI, and feedback systems.

**Proposed solution:** Review this isolated prototype and its evidence, then—only after approval—continue with authored environment assets and a separately scoped HUD/camera polish plan.

**World of Warcraft reference principle:** readable fantasy combat space, layered environmental scale, contextual information, and stable third-person camera behavior.

**Original AnotherLife adaptation:** realm-accented dusk citadel basin, procedural weathered geology, restrained arcane signals, and existing AnotherLife combat language.

**Systems affected:** Champion Arena presentation and lighting, deterministic runtime surface/mesh generation, combat camera collision/comfort, and focused EditMode coverage.

**Risks:** production collision-layer tuning, procedural art remaining visibly simpler than approved authored assets, missing physical-device/GPU profiling, and unverified alternate input, aspect-ratio, and accessibility paths.

**Dependencies:** A1 re-review and user visual approval; later approved environment/character source assets; no active shared-file lock.

**Testing plan:** retain the passing 18-case A7 and 11-case Champion Realm XML/logs; retain the six self-checking capture frames and CPU/memory manifest; keep PR #341 draft while A1 reviews. Remaining approval evidence is an integrated arena playthrough, physical-device/GPU profiling, four aspect ratios, controller/touch, and accessibility interaction checks.

**Approval needed from A1:** approve, revise, or reject the citadel-basin visual direction and decide whether it may remain connected to the production arena while authored environment assets are developed.

**Current status:** Proposal and executable evidence ready; draft PR #341 remains open for A1 re-review, and the visual direction is not user-approved or merge-ready.
