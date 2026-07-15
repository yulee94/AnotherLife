# AnotherLife Agent Instructions

These instructions apply to the entire repository. They define the working agreement for GPT, Codex, and the user.

## Canonical Repository And Workspace

- GitHub repository: `https://github.com/yulee94/AnotherLife`
- Integration branch: `main`
- Only active Windows checkout: `D:\260711\MY\AndroidStudioProjects\AnotherLife`
- Android Studio may be used as an IDE by Codex, but it is no longer an owner, agent, branch prefix, or approval gate.
- Unity Hub: open `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity`

Do not create or use `AnotherLifeUnity`, `AnotherLife-codex-*`, `_CodexWorktrees`, timestamped repository copies, or any other duplicate checkout as an active project. Offline backups are allowed, but agents must not edit or publish from them.

## Required Session Context

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

## Ownership Model

### GPT Owns Coordination, Specification, And Review

GPT is responsible for milestone planning, task decomposition, dependency ordering, state/contract/test specifications, PR review, shared-file sequencing, risk documentation, and decision records.

GPT must not author narrative, rewrite lore or dialogue, create production designs, or implement gameplay unless the user explicitly assigns a narrow exception.

### Codex Owns Narrative/Content, Engineering, And Design

The user has transferred all former Android Studio narrative workflow responsibilities and all design workload to Codex. Codex has three separately reviewable modes:

1. **Codex narrative/content mode** authors user-approved narrative source packets and content.
2. **Codex engineering mode** implements runtime, Android, Unity, build, save, tooling, tests, contracts, and generated consumers.
3. **Codex design/asset mode** creates, revises, implements, and integrates production designs and assets when needed for the active issue or user-approved direction.

Codex narrative/content mode owns:

- Main quests, side quests, hidden quests, quest hooks, chapter definitions, and story progression.
- Dialogue, NPCs, advisors, personas, affinity, loyalty, reputation, factions, and narrative outcomes.
- Storylines, lore, artifacts, boss lore, localization-facing narrative text, and content IDs.
- Narrative consequence definitions, continuity, and narrative-fidelity corrections.

Codex engineering mode owns:

- Unity runtime services, scene bootstrapping, gameplay integration, combat, bosses, loot, champion controls, customization, weather, and world systems.
- Android shell runtime, Android build behavior, and Android source changes.
- Loading, validating, and consuming approved narrative data through interfaces, JSON, schemas, generated assets, or catalogs.
- Save integration and backward-compatible migrations.
- Editor generators, automated tests, build fixes, performance work, diagnostics, CI support, and shared technical contracts.
- Fable-compatible contracts that remain free of `UnityEngine` types.

Codex design/asset mode owns:

- Characters, monsters, terrestrials, items, gear, skills, visual effects, world presentation, and supporting asset concepts.
- Visual language, silhouettes, palettes, surface/material references, habitat or environment presentation, motion/animation intent, design sheets, and implementation-ready asset specifications.
- Tier/grade-aware item, gear, skill, and effect visual treatment when visual systems are in scope.

Codex may perform these modes in one session, but narrative/content, engineering, and design/asset changes normally use separate branches and pull requests with GPT review between them. A mixed PR must explicitly declare the reason and review risk.

### User Owns Final Direction And Approval

The user retains final product, creative, design, and playtest approval.

## Source-Of-Truth And Handoff Rules

The standard NVS/content handoff is:

1. Codex narrative/content mode produces a user-approved narrative packet.
2. GPT reviews it and publishes an implementation specification without changing narrative intent.
3. Codex engineering mode implements the approved specification and provides validation evidence.
4. GPT reviews the implementation against the packet and acceptance criteria.
5. Codex narrative/content mode verifies narrative fidelity, and the user performs the final playtest and approval.

Runtime code must consume narrative and design data rather than duplicate it as hard-coded authority. Shared schemas and contracts belong in `unity/SharedContracts/`, and shared catalogs belong in `unity/Assets/AL/StreamingAssets/GameData/` when those formats are appropriate.

The first coordinated milestone is defined in `unity/Docs/Three_Way_Collaboration_Plan.md`. Long-range progression and phase gates are defined in `unity/Docs/Project_Progression_Roadmap.md`.

Do not advance a later-phase feature while the current phase gate is blocked unless the user explicitly reprioritizes the roadmap. Broken `main`, save/data risk, and active milestone blockers take priority over speculative expansion.

## Branch And Pull-Request Rules

Use these branch prefixes:

- `gpt/<short-scope>` for planning, specifications, reviews, and coordination documentation.
- `codex/<short-scope>` for Codex narrative/content, design/assets, runtime implementation, tests, tooling, and technical contracts.

`android-studio/<short-scope>` and `gemini/<short-scope>` are not used in the active ownership model.

Each pull request must:

- Represent one major completion and exclude unrelated cleanup.
- Identify the workstream owner, roadmap phase, and upstream artifact or dependency.
- State whether narrative content, design/assets, shared contracts, save data, or shared files changed.
- List every shared file touched.
- Include validation performed and any validation that could not be performed.
- Rebase onto the latest `main` before final review instead of overwriting collaborator work.
- Avoid force-pushing over another collaborator's commits.
- Avoid duplicating an issue already addressed by another open pull request unless the user explicitly requests an alternative and the PR explains the comparison plan.

Use `.github/pull_request_template.md` for the required declaration.

## Shared-File Lock And Conflict Rules

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
6. Merge conflicts must not be resolved by deleting unfamiliar systems, interfaces, generated assets, contracts, registrations, or approved assets.
7. The later branch rebases and resolves conflicts. It must not overwrite the earlier branch or force-push shared history.
8. GPT resolves technical sequencing disputes; the user resolves creative or product-direction disputes.

## Validation Expectations

Documentation-only work must verify that the canonical workspace is consistent, links and paths are valid, Markdown structure is sound, and no gameplay, narrative, design, or runtime content changed.

Narrative/content work must validate unique IDs, complete references, branch outcomes, recovery paths, and alignment with user-approved intent.

Design/asset work must identify source assets or generated assets, implementation scope, tier/grade visual rules when applicable, and validation evidence.

Runtime work must run the most relevant available compilation and automated tests, report exact commands and results, validate old-save compatibility when save data changes, validate asset/import/performance fidelity when integrating designs, and clearly disclose anything that could not be tested.

Every task must end with the current phase, acceptance-criteria status, PR or issue status, shared-file status, and the next unblocked owner-specific step.
