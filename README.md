# Another Life

Another Life is a high-fantasy kingdom war game prototype that combines an Android native shell with a Unity gameplay project. The current direction is a mobile/PC game with four realms, kingdom management, narrative progression, 3D champion combat, world objectives, boss encounters, weather, character customization, and Fable-compatible shared data contracts.

## Repository Overview

| Path | Purpose |
| --- | --- |
| `AGENTS.md` | Repository-wide instructions for GPT, Codex, and the Android Studio narrative workflow. |
| `app/` | Android native shell built with Kotlin, Jetpack Compose, Navigation, and Material UI. |
| `unity/` | Unity 2022.3 LTS gameplay project and prototype runtime. Open this folder in Unity Hub. |
| `unity/Assets/AL/Scripts/` | Unity C# gameplay, services, UI, battle, realm war, champion mode, and editor utilities. |
| `unity/Assets/AL/StreamingAssets/GameData/` | JSON catalogs shared by Unity and external tools. |
| `unity/SharedContracts/` | JSON schemas and Fable/F# contracts for non-Unity tooling. |
| `unity/Docs/` | Project handoff, collaboration, milestone, roadmap, and current-phase documentation. |
| `gradle/`, `build.gradle.kts`, `settings.gradle.kts` | Android/Gradle build configuration. |

## Local Project Layout

Use exactly one canonical local project for day-to-day work:

- GitHub repository checkout: `D:\260711\MY\AndroidStudioProjects\AnotherLife`
- Android Studio project: open `D:\260711\MY\AndroidStudioProjects\AnotherLife`
- Unity Hub project: open `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity`

Do not use `AnotherLifeUnity`, `AnotherLife-codex-*`, `_CodexWorktrees`, timestamped repositories such as `AnotherLife-YYYY-MM-DD_*`, or any other duplicate checkout as an active project. Those are cleanup, temporary, or backup folders and should not be recreated unless you are intentionally making an offline backup.

The root GitHub repository is `yulee94/AnotherLife`, and `main` is the integration branch collaborators should review first. All agents must read `AGENTS.md` before editing.

## Quick Start

### Android Studio

1. Open `D:\260711\MY\AndroidStudioProjects\AnotherLife` in Android Studio.
2. Use the `app` module and narrative-owned Unity data or tooling for Android shell, navigation, native UI, and narrative work.
3. Before starting work, fetch latest `main` and check open GitHub pull requests.
4. Use an `android-studio/<scope>` branch for narrative changes.

### Unity Hub

1. Open `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity` in Unity Hub.
2. Let Unity import the project.
3. Use the `Another Life > Generate Design Assets` editor menu to create starter modular character, skill VFX, weather, and material assets.
4. Run the test/champion scene to enter the 3D Champion Arena prototype.

### Fable / External Tools

Use `unity/SharedContracts/` and `unity/Assets/AL/StreamingAssets/GameData/` for cross-tool data. Shared contracts intentionally avoid UnityEngine types so Fable tools can validate catalogs, build editors, or preview configuration data without loading Unity.

## Current Prototype Systems

- Offline service stack and local save data.
- Realm selection and realm-specific resource identity.
- Realm selection cards now receive runtime premium styling: realm-colored accents, traces, sigil glow, null-safe prefab binding, hover/press motion for PC/mobile input, and a short command-confirmation transition before entering Kingdom Mode.
- Kingdom simulation foundations for buildings, research, resources, troops, territory, warzone credits, and a Unity-side 2.5D city board visualization.
- Android Studio-owned narrative expansion with chapters, quests, NPC affinity/reputation, factions, persona data, artifacts, and story hooks.
- Champion arena with movement, combat, auto modes, encounter intro/readiness flow, catalog-driven skill buttons, animated runtime skill VFX with charge cues, slash afterimages, cinematic impact accent columns/spokes/fins, guard plates, shockwave cracks, impact debris, lingering scorch/rim/ember aftermath, aerial shard rain, weather-linked combat flashes and atmosphere surges, generated combat audio cues, an atmospheric Obsidian Citadel combat presentation with pulsing rune floor detail, banners, boundary spines, braziers, layered procedural champion and boss models with close-up face planes, brow/nostril/neck detail, collar/gorget armor depth, battle-worn plate scoring, armor, cape, hand, boot, weapon details, prestige silhouette gear with style-aware mantle/sash/back equipment, and live hit/break/enrage/telegraph/low-health boss pressure aura material-light feedback, mobile/PC combat camera feedback, hit pause, damage-state HUD feedback, polished combat HUD, runtime combat goals, clear recap, tactical defeat recap, retry flow, readable boss telegraphs, boss encounters with live HP/break/enrage state, RvR bot crowds, world objective markers, cinematic realm weather with parallax foreground gust bands, foreground wind streaks, horizon veil banks, reactive light shafts, layered mist/haze, and combat-responsive flashes, and detailed modular character customization with saved appearance feedback, nine forge identity presets, and an in-arena inspection showcase.
- Boss loot service and owned-equipment save support.
- Generated design assets for character blockouts, skill effects, and weather.
- Shared JSON schemas and Fable contracts for customization and skill/weather catalogs.

## Current Runtime Snapshot

- Champion arena HUD shows shared-catalog skill names, mana costs, tactile role-tagged skill hotbar buttons with cooldown/mana/cast state rails, active cast-channel progress, tactile Attack/Dodge button feedback, explicit Manual/Assist/Auto control-mode feedback, boss HP, boss break state, enrage state, boss telegraph danger state, boss defeat state, a boss-following target-lock overlay with HP/guard micro-bars, break/enrage color states, and slam windup dodge prompts, a premium forge rack with active profile plate, labeled color chips, stronger inspection state chrome, a premium clear showcase with grade, credits, loot, arena pulse VFX, and tactical defeat recap with time, boss health, guard/enrage status, diagnosis text, retry, inspection, and Kingdom return actions.
- Champion Mode's first playable screen is being treated as the premium adult-facing vertical slice: darker arena lighting, stronger layered boss silhouette, pulsing citadel floor strokes, rift-gate depth architecture with boss-side tiers and foreground parapets, arena banners, braziers, cinematic realm weather fronts with parallax foreground gust bands, foreground streaks, animated horizon veils, reactive light shafts, and combat-driven atmosphere surges, distinctive skill cast/impact VFX with pooled impact columns, radial spokes, and heavy-skill fins, bordered combat HUD panels, active skill cast-channel feedback, cooldown hotbar overlays with ready glows and mana pips, a boss-following target lock with animated brackets/radar ticks, state-aware HP/guard readout, slam windup danger prompts, an in-world boss pressure aura that flares eyes/core/ring/orbit shards during telegraphs, enrage, break windows, and low-health pressure, and a live combat-pressure strip for dodge/enrage/break/critical-health readability, a more intentional plated hero silhouette with close-read face planes, brow/nostril/neck detail, collar/gorget depth, battle-worn armor scoring, prestige mantle/sash/chain/waist-wrap layering, weapon-specific back gear for sword, bow, staff, axe, and hammer styles, layered armor bevels, cape hardware, belt pouches, boot tread, style-specific weapon/offhand detail cores, subtle eye/rune/gem/relic/weapon-core surface response, procedural breathing/cape/gear motion, cinematic encounter intro shots with letterbox framing and hero/boss stage cues, camera shake/floating combat feedback, hit pause, damage flash, low-health edge feedback, encounter clear/failure feedback, tactical post-failure guidance, detailed customization controls with a premium forge rack, active profile readout, labeled color chips, stronger inspection state chrome, expanded body/color catalog options, beard/duelist scar/ash mask facial identity options, nine one-click forge identity presets, and a lit inspection showcase with orbiting stage traces, mirror frames, and detail motes.
- Kingdom Mode now opens over a premium runtime 2.5D command board: orthographic camera, PC/mobile pan and zoom, tactical grid, river and bridge terrain detail, command plaza, realm-colored beacons, pulsing command-signal routes, board frame, lit landmarks, roads, ambient supply runners/couriers/patrol markers, visual-only realm weather with drifting cloud shadows, low board mist, weather motes, breathing accent light, distinct district silhouettes, upgrade-state markers, selectable territory outposts with owner/bonus labels, level/status labels, grouped command deck with section-band chrome, compact command icons, animated action notches, and tactile hover/press feedback, top resource bar with a fixed-cell treasury ticker and a live Strategic Readiness console for build, force, lab, and war chest state, spill-safe status panels with realm-command chrome, corner brackets, bottom rails, and status pips, a live Command Dossier panel with classified command headers, status chips, animated signal bars, status-colored feedback, hover/selection pulses and command beacons for districts/outposts, richer outpost garrison markers, a Champion deployment transition before entering 3D combat, and a Board View toggle that hides the dashboard for city inspection.
- Skill/weather catalogs and Champion forge preset catalogs live in `unity/Assets/AL/StreamingAssets/GameData/` and are mirrored by schemas/Fable contracts in `unity/SharedContracts/`.
- The Boot scene now opens with a premium runtime splash: layered atmospheric lighting, animated ash/embers, citadel silhouette, gate/sigil reveal, progress/status feedback, and the same dark command-fantasy tone used by the polished runtime slices.
- Narrative, NPC, quest, dialogue, chapter, and story content remain Android Studio-owned so Unity runtime work and narrative expansion can move in parallel.

## Collaboration Rules

`AGENTS.md` is the authoritative repository-wide collaboration policy.

GPT owns coordination and specification:

- Milestone planning, task decomposition, dependency order, implementation specifications, acceptance criteria, and cross-workstream review.
- GPT must not invent narrative content or implement gameplay unless the user explicitly reassigns that work.

Android Studio owns narrative content and narrative progression:

- NPCs, advisors, personas, affinity/reputation content.
- Main quests, side quests, hidden quests, quest hooks, dialogue, chapters, storylines, lore, artifacts, boss lore, and narrative ScriptableObject generation.

Codex owns runtime implementation and Unity gameplay systems:

- Unity runtime services, bootstrapping, combat simulation, boss runtime behavior, loot runtime, character customization, 3D prototype models, skill VFX, weather, world atlas consumption, performance work, editor generators, automated tests, and shared contracts.

Shared files require extra care:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

An open pull request that declares a shared file holds a soft lock on it. If a merge conflict happens in a shared file, preserve both systems whenever possible. Service registration conflicts should keep all services. Save-data changes should stay backward compatible through default initialization.

## Branches

The repository uses `main` as the integration branch. Use short-lived `gpt/<scope>`, `android-studio/<scope>`, or `codex/<scope>` branches for focused work. Completed branches should be removed after merge so collaborators see current work from the repository front page and open pull requests.

## PR Workflow

1. Fetch latest `main`.
2. Check all open GitHub pull requests before editing.
3. Create a focused `gpt/<scope>`, `android-studio/<scope>`, or `codex/<scope>` branch.
4. Keep each pull request scoped to one major completion.
5. Complete `.github/pull_request_template.md`, including ownership, shared-file locks, dependencies, and validation.
6. Rebase onto latest `main` instead of overwriting collaborator work.

For current status, packet/specification structure, risk tracking, copy-paste role guidance, the staged roadmap, and the first narrative vertical-slice milestone, see:

- `unity/Docs/Phase_1_NVS_01_Status.md`
- `unity/Docs/Phase_1_NVS_01_Risk_Register.md`
- `unity/Docs/NVS_01_A1_Packet_Template.md`
- `unity/Docs/NVS_01_G1_Specification_Template.md`
- `unity/Docs/Phase_0_Build_Health_Status.md`
- `unity/Docs/Agent_Role_Prompts.md`
- `unity/Docs/Project_Progression_Roadmap.md`
- `unity/Docs/AndroidStudio_Codex_Collaboration_Prompt.md`
- `unity/Docs/Three_Way_Collaboration_Plan.md`
