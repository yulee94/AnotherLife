# Another Life

Another Life is a high-fantasy kingdom war game prototype that combines an Android native shell with a Unity gameplay project. The current direction is a mobile/PC game with four realms, kingdom management, narrative progression, 3D champion combat, world objectives, boss encounters, weather, character customization, and Fable-compatible shared data contracts.

## Repository Overview

| Path | Purpose |
| --- | --- |
| `app/` | Android native shell built with Kotlin, Jetpack Compose, Navigation, and Material UI. |
| `unity/` | Unity 2022.3 LTS gameplay project and prototype runtime. Open this folder in Unity Hub. |
| `unity/Assets/AL/Scripts/` | Unity C# gameplay, services, UI, battle, realm war, champion mode, and editor utilities. |
| `unity/Assets/AL/StreamingAssets/GameData/` | JSON catalogs shared by Unity and external tools. |
| `unity/SharedContracts/` | JSON schemas and Fable/F# contracts for non-Unity tooling. |
| `unity/Docs/` | Project handoff docs, including the Android Studio/Codex collaboration prompt. |
| `gradle/`, `build.gradle.kts`, `settings.gradle.kts` | Android/Gradle build configuration. |

## Local Project Layout

Use exactly one canonical local project for day-to-day work:

- GitHub repository checkout: `C:\Users\MY\AndroidStudioProjects\AnotherLife`
- Android Studio project: open `C:\Users\MY\AndroidStudioProjects\AnotherLife`
- Unity Hub project: open `C:\Users\MY\AndroidStudioProjects\AnotherLife\unity`

Do not use `AnotherLifeUnity`, `AnotherLife-codex-*`, `_CodexWorktrees`, or timestamped repositories such as `AnotherLife-YYYY-MM-DD_*` as active projects. Those were cleanup/temporary folders and should not be recreated unless you are intentionally making an offline backup.

The root GitHub repository is `yulee94/AnotherLife`, and `main` is the only active branch collaborators should review first.

## Quick Start

### Android Studio

1. Open the repository root in Android Studio.
2. Use the `app` module for Android shell, navigation, and native UI work.
3. Before starting work, fetch latest `main` and check open GitHub PRs.

### Unity Hub

1. Open `C:\Users\MY\AndroidStudioProjects\AnotherLife\unity` in Unity Hub.
2. Let Unity import the project.
3. Use the `Another Life > Generate Design Assets` editor menu to create starter modular character, skill VFX, weather, and material assets.
4. Run the test/champion scene to enter the 3D Champion Arena prototype.

### Fable / External Tools

Use `unity/SharedContracts/` and `unity/Assets/AL/StreamingAssets/GameData/` for cross-tool data. Shared contracts intentionally avoid UnityEngine types so Fable tools can validate catalogs, build editors, or preview configuration data without loading Unity.

## Current Prototype Systems

- Offline service stack and local save data.
- Realm selection and realm-specific resource identity.
- Kingdom simulation foundations for buildings, research, resources, troops, territory, and warzone credits.
- Android Studio-owned narrative expansion with chapters, quests, NPC affinity/reputation, factions, persona data, artifacts, and story hooks.
- Champion arena with movement, combat, auto modes, catalog-driven skill buttons, an atmospheric Obsidian Citadel combat presentation, layered procedural champion and boss models, combat camera feedback, boss encounters with live HP/break/enrage HUD, RvR bot crowds, world objective markers, runtime weather, and modular character customization.
- Boss loot service and owned-equipment save support.
- Generated design assets for character blockouts, skill effects, and weather.
- Shared JSON schemas and Fable contracts for customization and skill/weather catalogs.

## Current Runtime Snapshot

- Champion arena HUD shows shared-catalog skill names, mana costs, boss HP, boss break state, enrage state, and boss defeat state.
- Champion Mode's first playable screen is being treated as the premium adult-facing vertical slice: darker arena lighting, stronger layered boss silhouette, cleaner combat HUD, a more intentional plated hero silhouette, camera shake/floating combat feedback, and customization controls contained in an appearance rack.
- Skill/weather catalogs live in `unity/Assets/AL/StreamingAssets/GameData/` and are mirrored by schemas/Fable contracts in `unity/SharedContracts/`.
- Narrative, NPC, quest, dialogue, chapter, and story content remain Android Studio-owned so Unity runtime work and narrative expansion can move in parallel.

## Collaboration Rules

Android Studio owns narrative content and narrative progression:

- NPCs, advisors, personas, affinity/reputation content.
- Main quests, side quests, hidden quests, quest hooks, dialogue, chapters, storylines, lore, artifacts, boss lore, and narrative ScriptableObject generation.

Codex owns runtime implementation and Unity gameplay systems:

- Unity runtime services, bootstrapping, combat simulation, boss runtime behavior, loot runtime, character customization, 3D prototype models, skill VFX, weather, world atlas consumption, performance work, editor generators, and shared contracts.

Shared files require extra care:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

If a merge conflict happens in a shared file, preserve both systems whenever possible. Service registration conflicts should keep all services. Save-data changes should stay backward compatible through default initialization.

## Branches

The repository uses `main` as the single active branch. The old `master`, completed `codex/*`, and completed `narrative/*` branches were consolidated into `main` and removed from GitHub so co-developers always see the current project from the repository front page.

## PR Workflow

1. Fetch latest `main`.
2. Check open GitHub PRs before editing.
3. Use focused branches, such as `android-studio/<scope>` or `codex/<scope>`.
4. Keep each PR scoped to one major completion.
5. In the PR body, list ownership areas touched and validation performed.
6. Rebase onto latest `main` instead of overwriting collaborator work.

For a copy-paste coordination prompt, see:

`unity/Docs/AndroidStudio_Codex_Collaboration_Prompt.md`
