# Android Studio and Codex Collaboration Prompt

Copy the prompt below into Android Studio before it continues work on Another Life.

```text
You are co-developing Another Life with Codex through GitHub.

Repository:
https://github.com/yulee94/AnotherLife

Primary rule:
Do not overwrite or revert Codex work. Before changing code, fetch the latest main branch and inspect open pull requests. If a file is being changed by an open Codex PR, avoid that file unless the change is required for your narrative work.

Branch rule:
Use `main` as the only default branch. The old `master` branch was consolidated into `main` and removed from GitHub so the repository front page always shows the current project.

Android Studio ownership:
- NPC data, NPC affinity/reputation content, advisor content, and persona content.
- Main quests, side quests, hidden quests, quest hooks, chapter definitions, dialogue, storyline, lore, artifact lore, boss lore, and narrative ScriptableObject generation.
- Narrative service logic when it is specifically about story progression, conflict hints, advisor loyalty, chapter unlocks, or quest outcomes.

Codex ownership:
- Unity runtime gameplay systems, service boundaries, scene bootstrapping, combat simulation, boss runtime behavior, loot runtime, champion controls, character customization, 3D prototype models, skill VFX, weather, world atlas consumption, performance pooling, editor generators, and Fable/shared data contracts.
- Codex may consume Android Studio narration through interfaces such as IStoryService and data definitions, but should not rewrite the story, NPCs, quests, or dialogue.

Shared files that require care:
- unity/Assets/AL/Scripts/Core/Bootloader.cs
- unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
- unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
- unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs

When editing shared files:
- Keep both systems if a merge conflict happens. For example, service registration conflicts should preserve all registered services from both Android Studio and Codex.
- Add new fields through backward-compatible save defaults.
- Do not remove Codex interfaces, runtime services, generated design assets, weather profiles, world atlas data, boss loot services, or shared contract files.

PR workflow:
1. Fetch latest main.
2. Run or inspect `gh pr list --state open` if GitHub CLI is available.
3. Create a focused branch, preferably `android-studio/<short-scope>`.
4. Keep each PR scoped to one major completion.
5. In the PR body, list changed ownership areas and mention whether any shared files were touched.
6. After opening a PR, check open Codex PRs and rebase if needed instead of force-pushing over unrelated work.

Fable compatibility:
- Use `unity/SharedContracts` and `unity/Assets/AL/StreamingAssets/GameData` for cross-tool data.
- Keep shared schemas and Fable contracts free of UnityEngine types.
- If a new system needs to be consumed by Fable, add or update plain JSON/schema/contract files rather than coupling Fable code to Unity MonoBehaviours.

Current coordination intent:
Android Studio continues narrative expansion. Codex continues gameplay/world/runtime implementation around that narration. If cooperation is needed, expose data through interfaces or JSON contracts and coordinate through a PR instead of editing the same narrative/runtime file silently.
```
