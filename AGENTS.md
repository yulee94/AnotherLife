# AnotherLife Agent Instructions

These instructions apply to the entire repository. They define the working agreement for GPT, Codex, and the Android Studio narrative workflow.

## Canonical repository and workspace

- GitHub repository: `https://github.com/yulee94/AnotherLife`
- Integration branch: `main`
- Only active Windows checkout: `D:\260711\MY\AndroidStudioProjects\AnotherLife`
- Android Studio: open `D:\260711\MY\AndroidStudioProjects\AnotherLife`
- Unity Hub: open `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity`

Do not create or use `AnotherLifeUnity`, `AnotherLife-codex-*`, `_CodexWorktrees`, timestamped repository copies, or any other duplicate checkout as an active project. Offline backups are allowed, but agents must not edit or publish from them.

## Required session context

Before beginning work, every agent must read:

1. This root `AGENTS.md`.
2. Its matching standalone prompt in `unity/Docs/Agent_Role_Prompts.md`.
3. The active phase and gate in `unity/Docs/Project_Progression_Roadmap.md`.
4. `unity/Docs/Three_Way_Collaboration_Plan.md` when working on NVS-01.
5. The relevant issue, upstream artifact, and open pull requests.

Before starting a task:

1. Fetch the latest `main`.
2. Inspect all open pull requests for duplicate work, overlapping files, dependencies, and ownership areas.
3. Create a focused branch from current `main`.
4. Confirm the task owner, inputs, outputs, active roadmap phase, and acceptance criteria.
5. Declare any shared files in the pull request before editing them.
6. Stop or coordinate when another open pull request already addresses the same issue; do not silently create a parallel implementation.

No agent may commit directly to `main`.

## Three-way ownership

### GPT owns coordination and specification

GPT is responsible for:

- Milestone planning, task decomposition, dependency ordering, and scope control.
- Converting approved narrative packets into implementation specifications.
- Defining state transitions, runtime events, data-contract requirements, edge cases, and acceptance tests.
- Reviewing pull requests for ownership violations, contract drift, save compatibility, missing validation, and merge risk.
- Coordinating exclusive access to shared files.
- Maintaining collaboration documentation and decision records.

GPT must not invent or rewrite narrative content and must not implement gameplay unless the user explicitly reassigns that work.

### Android Studio owns narrative source material

The Android Studio narrative workflow is the source of truth for:

- Main quests, side quests, hidden quests, quest hooks, chapter definitions, and story progression.
- Dialogue, NPCs, advisors, personas, affinity, loyalty, reputation, factions, and narrative outcomes.
- Storylines, lore, artifacts, boss lore, localization-facing narrative text, and content IDs.
- Narrative-specific generation and service logic when it directly governs chapter unlocks, quest outcomes, advisor loyalty, or conflict hints.

Narrative ownership follows the content, regardless of the directory in which a file lives. Android Studio must not redesign Unity combat, runtime bootstrapping, general save infrastructure, VFX, weather, or unrelated gameplay systems.

### Codex owns runtime implementation

Codex is responsible for:

- Unity runtime services, scene bootstrapping, gameplay integration, combat, bosses, loot, champion controls, customization, weather, and world systems.
- Android shell runtime and build-compatibility fixes that do not alter narrative meaning.
- Loading, validating, and consuming approved narrative data through interfaces, JSON, schemas, or generated assets.
- Save integration and backward-compatible migrations.
- Editor generators, automated tests, build fixes, performance work, and shared technical contracts.
- Fable-compatible contracts that remain free of `UnityEngine` types.

Codex may consume approved narrative data but must not rewrite dialogue, NPC characterization, quest meaning, chapter order, lore, or narrative outcomes.

## Source-of-truth and handoff rules

The standard handoff is:

1. Android Studio produces an approved narrative packet.
2. GPT reviews it and publishes an implementation specification without changing narrative intent.
3. Codex implements the approved specification and provides validation evidence.
4. GPT reviews the implementation against the packet and acceptance criteria.
5. Android Studio verifies narrative fidelity, and the user performs the final playtest and approval.

Runtime code must consume narrative data rather than duplicate it as hard-coded story text. Shared schemas and contracts belong in `unity/SharedContracts/` and shared catalogs belong in `unity/Assets/AL/StreamingAssets/GameData/` when those formats are appropriate.

The first coordinated milestone is defined in `unity/Docs/Three_Way_Collaboration_Plan.md`. Long-range progression and phase gates are defined in `unity/Docs/Project_Progression_Roadmap.md`.

Do not advance a later-phase feature while the current phase gate is blocked unless the user explicitly reprioritizes the roadmap. Broken `main`, save/data risk, and active milestone blockers take priority over speculative expansion.

## Branch and pull-request rules

Use these branch prefixes:

- `gpt/<short-scope>` for planning, specifications, reviews, and coordination documentation.
- `android-studio/<short-scope>` for narrative content and narrative-owned logic.
- `codex/<short-scope>` for runtime implementation, tests, tooling, and technical contracts.

Each pull request must:

- Represent one major completion and exclude unrelated cleanup.
- Identify the workstream owner, roadmap phase, and upstream artifact or dependency.
- State whether narrative content, shared contracts, save data, or shared files changed.
- List every shared file touched.
- Include validation performed and any validation that could not be performed.
- Rebase onto the latest `main` before final review instead of overwriting collaborator work.
- Avoid force-pushing over another collaborator's commits.
- Avoid duplicating an issue already addressed by another open pull request unless the user explicitly requests an alternative and the PR explains the comparison plan.

Use `.github/pull_request_template.md` for the required declaration.

## Shared-file lock and conflict rules

These files are shared integration points and require an explicit soft lock:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

Rules:

1. The first open pull request that declares a shared file holds the soft lock until the pull request is merged, closed, or explicitly releases it.
2. A second task must not edit that file in parallel. It must depend on the first pull request, choose another integration point, or be rescheduled by GPT.
3. Before editing a shared file, refresh `main` and re-check open pull requests.
4. Service-registration conflicts must preserve all valid services from every workstream.
5. New save fields must have backward-compatible defaults, and old saves must continue to load.
6. Merge conflicts must not be resolved by deleting unfamiliar systems, interfaces, generated assets, contracts, or registrations.
7. The later branch rebases and resolves conflicts. It must not overwrite the earlier branch or force-push shared history.
8. GPT resolves technical ownership disputes; the user resolves creative or product-direction disputes.

## Validation expectations

Documentation-only work must verify that the canonical workspace is consistent, links and paths are valid, Markdown structure is sound, and no gameplay or narrative content changed.

Narrative work must validate unique IDs, complete references, branch outcomes, recovery paths, and alignment with the approved narrative packet.

Runtime work must run the most relevant available compilation and automated tests, report exact commands and results, validate old-save compatibility when save data changes, and clearly disclose anything that could not be tested.

Every task must end with the current phase, acceptance-criteria status, PR or issue status, shared-file status, and the next unblocked owner-specific step.