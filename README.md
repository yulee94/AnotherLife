# Another Life

Another Life is a high-fantasy kingdom war game prototype combining a Kotlin/Jetpack Compose Android shell with a Unity 2022.3 LTS gameplay project. The current direction includes four realms, kingdom management, narrative progression, Champion combat, world objectives, boss encounters, weather, customization, local persistence, and shared data contracts.

## Repository overview

| Path | Purpose |
| --- | --- |
| `AGENTS.md` | Authoritative ownership, branch, lock, and handoff rules for GPT, Codex, and the user. |
| `app/` | Android native shell, navigation, UI, preview tooling, and Android integration. |
| `unity/` | Unity gameplay project, runtime services, scenes, tests, assets, and editor tooling. |
| `unity/Assets/AL/StreamingAssets/GameData/` | Runtime JSON catalogs. |
| `unity/SharedContracts/` | JSON schemas and Fable-compatible contracts. |
| `unity/Docs/` | Roadmap, role prompts, milestone plans, specifications, status, risk, and handoff records. |
| `.github/` | Pull-request template and future repository quality-gate configuration. |

## Canonical local workspace

Use exactly one active checkout:

```text
D:\260711\MY\AndroidStudioProjects\AnotherLife
```

- Open the root in Android Studio when working on Android or Gradle tasks.
- Open `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity` in Unity Hub.
- Do not edit or publish from duplicate worktrees, timestamped copies, backup folders, or `AnotherLife-codex-*` directories.
- `main` is the integration branch. No direct commits to `main`.

Android Studio and Unity are tools. They are not repository agents or ownership workstreams.

## Ownership model

### GPT

GPT owns planning, dependency order, implementation specifications, state/event/contract/save/test design, PR review, shared-file sequencing, status/risk documentation, and merge-readiness decisions.

GPT does not author narrative, terrestrial visual design, or gameplay/application code unless the user explicitly reassigns a narrow task.

### Codex

Codex owns all project delivery through three separately declared modes:

1. **Narrative/content mode** — quests, chapters, dialogue, NPCs, lore, localization-facing copy, continuity, consequences, stable IDs, narrative packets, and fidelity correction.
2. **Terrestrial-design mode** — terrestrial creature/fauna concepts, silhouettes, anatomy, palettes, materials, motion intent, variants, design sheets, source assets, and design fidelity.
3. **Engineering mode** — Android, Gradle, Unity, runtime services, gameplay, assets/import, scenes, save/migration/recovery, catalogs, contracts, tests, CI, tooling, performance, and accessibility mechanics.

Source authoring/design and engineering implementation normally use separate branches and PRs. Engineering consumes approved source rather than silently rewriting or redesigning it.

### User

The user retains final product, creative, visual-design, balance, milestone, playtest, irreversible-profile, and release approval.

## Branches

Use focused short-lived branches:

```text
gpt/<scope>
codex/narrative-<scope>
codex/terrestrial-<scope>
codex/<engineering-scope>
```

`android-studio/` and `gemini/` are retired for new work.

## Pull-request workflow

1. Fetch current `main`.
2. Inspect every open PR for overlap, dependency, base branch, and shared-file locks.
3. Create one focused branch with exactly one primary owner mode.
4. Complete `.github/pull_request_template.md`.
5. Declare narrative, terrestrial-design, runtime, asset, contract/catalog, save, workflow, and shared-file impact.
6. Run exact relevant validation and report blocked checks honestly.
7. Rebase onto current `main` before final review.
8. Merge only after GPT disposition and any required Codex source-mode/user fidelity gate.

## Shared integration files

These require an explicit exclusive soft lock:

```text
unity/Assets/AL/Scripts/Core/Bootloader.cs
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs
```

The first approved open PR declaring one holds the lock. Later work must wait, depend on it, or use another integration point.

## Current prototype areas

- Android Compose shell and narrative/debug preview surfaces.
- Offline Unity service stack and local save data.
- Realm selection and realm-specific resources.
- Kingdom buildings, research, troops, territories, Warzone Credits, world state, Realm Gems, Wishgate, and Warmaster foundations.
- Champion arena movement, action combat, bosses, skills, weather, VFX/audio, HUD, retry/clear flows, RvR bots, and character customization.
- Boss loot and owned-equipment persistence foundations.
- StreamingAssets catalogs and shared schemas/contracts.

These are prototype capabilities, not evidence that every associated issue or release gate is complete.

## Current priority

The active recovery and Phase 1 sequence is tracked in:

- `unity/Docs/Phase_1_NVS_01_Status.md`
- `unity/Docs/Phase_1_NVS_01_Risk_Register.md`
- `unity/Docs/Project_Progression_Roadmap.md`
- `unity/Docs/Three_Way_Collaboration_Plan.md`
- `unity/Docs/Repository_Quality_Gate_Policy.md`
- `unity/Docs/Save_Semantic_Compatibility_Policy.md`
- issue #138 — approved D1–D16 product decisions
- issue #193 — Codex ownership consolidation
- issue #194 — Codex terrestrial-design foundation

A closed issue, merged PR, source file, or successful compile is not by itself completion evidence. Match evidence to the owning acceptance criteria: build logs, test XML, migration/fault matrices, contract producer/consumer proof, actual Player/export builds, and user playtest where required.