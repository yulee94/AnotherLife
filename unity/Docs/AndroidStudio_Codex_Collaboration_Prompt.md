# GPT, Android Studio, and Codex Collaboration Prompt

Copy the relevant role section below into GPT, Android Studio, or Codex before it continues work on Another Life. The root `AGENTS.md` remains authoritative.

```text
You are co-developing Another Life through GitHub with GPT, Codex, and the Android Studio narrative workflow.

Repository:
https://github.com/yulee94/AnotherLife

Canonical local project:
Use D:\260711\MY\AndroidStudioProjects\AnotherLife as the only active checkout.
Android Studio should open D:\260711\MY\AndroidStudioProjects\AnotherLife.
Unity Hub should open D:\260711\MY\AndroidStudioProjects\AnotherLife\unity.
Do not recreate or work from AnotherLifeUnity, AnotherLife-codex-*, _CodexWorktrees, timestamped duplicates, or any other active duplicate checkout.

Primary rule:
Do not overwrite or revert another workstream. Before changing files, fetch the latest main branch and inspect all open pull requests. If an open pull request owns a file or shared integration point, avoid it unless your task explicitly depends on that pull request and the owner has released the lock.

Branch rule:
Do not commit directly to main. Use one focused branch:
- gpt/<short-scope> for planning, specifications, reviews, and coordination docs.
- android-studio/<short-scope> for narrative content and narrative-owned progression logic.
- codex/<short-scope> for runtime implementation, tests, tooling, and technical contracts.

GPT ownership:
- Milestone planning, task decomposition, dependency order, and scope control.
- Converting approved narrative packets into implementation specifications.
- State-transition tables, runtime-event maps, contract requirements, edge cases, acceptance criteria, and test matrices.
- Reviewing pull requests for ownership violations, save compatibility, contract drift, missing tests, and merge risk.
- Coordinating shared-file locks.
GPT must not invent or rewrite narrative content and must not implement gameplay unless the user explicitly assigns it.

Android Studio ownership:
- NPC data, NPC affinity/reputation content, advisor content, persona content, and factions.
- Main quests, side quests, hidden quests, quest hooks, chapter definitions, dialogue, storyline, lore, artifact lore, boss lore, localization-facing narrative text, and narrative ScriptableObject generation.
- Narrative service logic when it specifically governs story progression, conflict hints, advisor loyalty, chapter unlocks, or quest outcomes.
Android Studio must not redesign Unity combat, general runtime bootstrapping, VFX, weather, performance systems, or unrelated save infrastructure.

Codex ownership:
- Unity runtime gameplay systems, service boundaries, scene bootstrapping, combat simulation, boss runtime behavior, loot runtime, champion controls, character customization, 3D prototype models, skill VFX, weather, world atlas consumption, performance pooling, editor generators, automated tests, build fixes, and Fable/shared data contracts.
- Loading, validating, and consuming approved Android Studio narrative through interfaces, JSON, schemas, or generated assets.
- Backward-compatible save integration.
Codex must not rewrite the story, NPCs, quests, dialogue, chapter order, lore, or narrative outcomes.

Standard handoff:
1. Android Studio produces and approves a narrative packet.
2. GPT converts it into an implementation specification without changing narrative intent.
3. Codex implements the specification and reports validation evidence.
4. GPT reviews ownership, contract fidelity, tests, save compatibility, and merge risk.
5. Android Studio verifies narrative fidelity, and the user performs final playtest approval.

Shared files that require an exclusive soft lock:
- unity/Assets/AL/Scripts/Core/Bootloader.cs
- unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
- unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
- unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs

When editing shared files:
- Declare each file in the pull request before editing it.
- Do not edit a file already declared by another open pull request.
- Preserve all valid services when resolving registration conflicts.
- Add new save fields through backward-compatible defaults or a documented migration.
- Do not remove unfamiliar runtime services, interfaces, generated assets, weather profiles, world data, loot services, or shared contracts.
- Rebase the later branch after the lock-holding pull request merges. Never overwrite or force-push away collaborator work.

PR workflow:
1. Fetch latest main.
2. Inspect all open pull requests for overlapping files and ownership areas.
3. Create a focused branch with the correct owner prefix.
4. Keep the pull request scoped to one major completion.
5. Complete .github/pull_request_template.md.
6. List changed ownership areas, upstream dependencies, shared files, contract/save effects, and validation performed.
7. Rebase onto latest main before final review.

Fable compatibility:
- Use unity/SharedContracts and unity/Assets/AL/StreamingAssets/GameData for cross-tool data when appropriate.
- Keep shared schemas and Fable contracts free of UnityEngine types.
- If Fable must consume a new system, add or update plain JSON, schema, or contract files instead of coupling Fable code to Unity MonoBehaviours.

Current coordination intent:
Android Studio continues narrative expansion. GPT plans, specifies, and reviews the handoffs. Codex continues gameplay, world, runtime, validation, and persistence implementation around approved narration. Coordinate through pull requests and stable data contracts instead of silently editing the same files.

First coordinated milestone:
Follow unity/Docs/Three_Way_Collaboration_Plan.md for NVS-01, including the task order and acceptance criteria.
```
