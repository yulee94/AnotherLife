# Another Life

Another Life is a high-fantasy kingdom war game prototype combining a Kotlin/Jetpack Compose Android shell with a Unity 2022.3 LTS gameplay project. The current direction includes four realms, kingdom management, narrative progression, Champion combat, world objectives, boss encounters, weather, customization, local persistence, and shared data contracts.

## Repository overview

| Path | Purpose |
| --- | --- |
| `AGENTS.md` | Authoritative ownership, branch, lock, and handoff rules for Codex and the user. |
| `app/` | Android native shell, navigation, UI, preview tooling, and Android integration. |
| `unity/` | Unity gameplay project, runtime services, scenes, tests, assets, and editor tooling. |
| `unity/Assets/AL/StreamingAssets/GameData/` | Runtime JSON catalogs. |
| `unity/SharedContracts/` | JSON schemas and Fable-compatible contracts. |
| `unity/Docs/` | Product direction, roadmap, role prompts, milestone plans, specifications, status, risk, ownership, and handoff records. |
| `.github/` | Pull-request template and future repository quality-gate configuration. |

## Canonical local workspace

Use exactly one active checkout:

```text
C:\Users\MY\Documents\AnotherLife
```

- Use this Codex agent as the responsible owner for Android, Unity, design, narrative/content, review, CI, tooling, and documentation work.
- Open `C:\Users\MY\Documents\AnotherLife\unity` in Unity Hub when Unity editor validation or playtesting is needed.
- Do not edit or publish from duplicate worktrees, timestamped copies, backup folders, or `AnotherLife-codex-*` directories.
- `main` is the integration branch. No direct commits to `main`.

Android Studio, Unity Hub, GitHub, Fable, Gemini, and other tools are tools only. They are not repository agents, owners, approval gates, or workstreams.

## Ownership model

The final user decision is recorded in `unity/Docs/Ownership_Decision_Record.md`: **this Codex agent owns all project workload and responsibility for Another Life.**

### This Codex Agent

This Codex agent owns all project work through four separately declared modes:

1. **Coordination/review mode** — planning, dependency order, specifications, issue/PR triage, technical review, shared-file sequencing, status/risk/governance, and merge-readiness disposition.
2. **Narrative/content mode** — quests, chapters, dialogue, NPCs, lore, localization-facing copy, continuity, consequences, stable IDs, narrative packets, and fidelity correction.
3. **Terrestrial-design mode** — terrestrial creature/fauna concepts, silhouettes, anatomy, palettes, materials, motion intent, variants, design sheets, source assets, and design fidelity.
4. **Engineering mode** — Android, Gradle, Unity, runtime services, gameplay, assets/import, scenes, save/migration/recovery, catalogs, contracts, tests, CI, tooling, performance, accessibility mechanics, and UI/UX implementation.

Source authoring/design and engineering implementation normally use separate branches and PRs. Engineering consumes approved source rather than silently rewriting or redesigning it.

All modes must preserve the standing optimization mandate: keep runtime paths, data, assets, generated files, and packaging choices manageable for the broadest feasible device range with the lowest feasible install size. Higher visual tiers may add richer effects, but every tier needs an explicit performance, memory, and build-size strategy.

GPT, Android Studio, Gemini, and any other external assistant or tool receive no future project ownership, workload, approval, review-gate, or branch-stream assignment. Historical GPT-authored specifications, issue comments, PR reviews, Android Studio references, and Gemini references remain repository evidence only until this Codex agent or the user supersedes them.

### User

The user retains final product, creative, visual-design, balance, milestone, playtest, irreversible-profile, and release approval.

## Branches

Use focused short-lived branches:

```text
codex/coordination-<scope>
codex/narrative-<scope>
codex/terrestrial-<scope>
codex/<engineering-scope>
```

`gpt/`, `android-studio/`, and `gemini/` are retired for new work.

## Pull-request workflow

1. Fetch current `main`.
2. Read `AGENTS.md` and `unity/Docs/Ownership_Decision_Record.md`.
3. Inspect every open and relevant closed issue/PR for overlap, dependency, base branch, stale ownership labels, regression history, and shared-file locks.
4. Create one focused branch with exactly one primary Codex mode.
5. Complete `.github/pull_request_template.md`.
6. Declare narrative, terrestrial-design, runtime, asset, contract/catalog, save, workflow, and shared-file impact.
7. Declare performance, memory, asset, and install-size impact, including why the change remains manageable on target low-end devices.
8. Run exact relevant validation and report blocked checks honestly.
9. Update onto current `main` before final disposition.
10. Merge only after Codex coordination/review disposition and any required Codex source-mode or user fidelity gate.

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
- Launch realm selection and realm-specific resources.
- 2.5D inner-kingdom buildings, research, troops, territories, Warzone Credits, world state, Realm Gems, Wishgate, and Warmaster foundations.
- Champion arena movement, action combat, bosses, skills, weather, VFX/audio, HUD, retry/clear flows, RvR bots, and character customization.
- Boss loot and owned-equipment persistence foundations.
- StreamingAssets catalogs and shared schemas/contracts.

These are prototype capabilities, not evidence that every associated issue or release gate is complete.

## Current priority

Use:

- `unity/Docs/Product_Direction.md`
- `unity/Docs/Ownership_Decision_Record.md`
- `unity/Docs/Phase_1_NVS_01_Status.md`
- `unity/Docs/Phase_1_NVS_01_Risk_Register.md`
- `unity/Docs/Project_Progression_Roadmap.md`
- `unity/Docs/Three_Way_Collaboration_Plan.md`
- `unity/Docs/Repository_Quality_Gate_Policy.md`
- `unity/Docs/Save_Semantic_Compatibility_Policy.md`
- issue #138 — approved D1–D16 product decisions
- issue #194 — Codex terrestrial-design foundation

A closed issue, merged PR, source file, or successful compile is not by itself completion evidence. Match evidence to the owning acceptance criteria: build logs, test XML, migration/fault matrices, contract producer/consumer proof, actual Player/export builds, and user playtest where required.

Closed issues may be reopened when current source or Unity Hub play shows they still block the product direction. The long-term playable path must launch into four-realm selection, realm-specific character creation, account realm-locking with same-realm sub-characters and shared storage, unique username creation, 3D inner-realm champion questing, polished 2.5D kingdom mode, 3D inner-realm return, party-oriented neutral mob hunting where healers/buffers earn fair rewards and combat potions cannot replace support roles, outer-warzone save-pillar setup, bridge-connected realm-vs-realm warzone play, level-50 skill-tree progression, Warmaster gear/Warzone-point completion, True Warmaster unlocks, center-island neutral trade/chat under the Wish Dragon's consideration, dragon/boss/gem/Warmaster objectives, and ultimately the eight-gem final wish objective.

## Optimization Mandate

Optimization is part of every completion, not a late polish pass. This Codex agent must prefer reusable data, pooled/runtime-safe effects, compressed and deduplicated assets, deterministic generated outputs, lazy loading where appropriate, bounded catalogs, and device-conscious UI/rendering choices. Every feature or asset PR must state its expected performance, memory, package-size, and compatibility impact, plus any validation that was not yet possible.
