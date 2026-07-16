# AnotherLife Agent Instructions

These instructions apply to the entire repository. They define the working agreement for Codex and the user.

`unity/Docs/Ownership_Decision_Record.md` records the final user instruction chronology and controls ownership conflicts.

## Canonical workspace

- Repository: `https://github.com/yulee94/AnotherLife`
- Integration branch: `main`
- Active Codex checkout: `C:\Users\MY\Documents\AnotherLife`
- Active Codex Unity project: `C:\Users\MY\Documents\AnotherLife\unity`

Historical paths whose directory names include `AndroidStudioProjects` may remain in older issues, PRs, logs, and archived evidence. Those names do not assign ownership or require Android Studio use. Android code and tooling, when still in scope, are handled by Codex.

Do not edit or publish from duplicate worktrees, timestamped copies, or backup repositories. Codex must not commit directly to `main`.

## Required startup

Before work:

1. Read this file, `unity/Docs/Ownership_Decision_Record.md`, the Codex prompt in `unity/Docs/Agent_Role_Prompts.md`, and the active gate in `unity/Docs/Project_Progression_Roadmap.md`.
2. Read `unity/Docs/Three_Way_Collaboration_Plan.md` for NVS-01; the legacy filename is retained for link stability.
3. Fetch current `main` and inspect all open issues/PRs plus relevant closed issues/PRs for overlap, dependencies, review findings, stale ownership labels, duplicate work, regression history, and shared-file locks.
4. Create one focused branch and declare the goal, non-goals, primary Codex mode, file scope, acceptance criteria, validation, and blocked checks.

## Ownership

### Codex — sole project agent

Codex owns all project work through four declared modes.

#### Coordination/review mode

Owns milestone and backlog planning, dependency order, specifications, state/event/contract/save/test design, issue and PR triage, integration review, shared-file sequencing, status/risk/governance records, and merge-readiness disposition.

This mode must ground decisions in current source, written requirements, retained evidence, and user decisions. It must not treat prior merge state, issue closure, or a green but incomplete test as acceptance by itself.

#### Narrative/content mode

Owns quests, chapters, dialogue, NPCs, lore, artifacts, localization-facing copy, continuity, consequences, relationships, factions, stable narrative IDs, narrative packets, and narrative-fidelity correction.

#### Terrestrial-design mode

Owns terrestrial creature/fauna concepts, silhouettes, anatomy, palettes, materials, habitat presentation, motion intent, scale, variants, design sheets, source assets, and design-fidelity correction.

#### Engineering mode

Owns Android and Unity source, runtime services, gameplay, combat, bosses, loot, terrestrial runtime integration, assets/import, scenes, save/migration/recovery, catalogs, contracts, build systems, tests, CI, tooling, diagnostics, performance, and accessibility mechanics.

Engineering mode must consume approved narrative and terrestrial-design source rather than silently inventing or redesigning it in runtime code.

### User

The user owns final product, creative, visual-design, balance, irreversible-profile, milestone, integrated playtest, and release approval.

GPT and Android Studio receive no future project work, coordination assignment, review gate, or approval responsibility. Historical GPT-authored specifications and reviews remain repository evidence until Codex coordination/review mode or the user explicitly supersedes them.

## Mode separation and handoffs

The same Codex agent may perform every mode, but coordination/review, source authoring/design, and engineering implementation normally use separate branches and PRs so evidence and intent remain reviewable.

Narrative flow:

1. User decisions.
2. Codex narrative/content packet.
3. Codex coordination/review specification.
4. Codex engineering implementation.
5. Codex coordination/review integration disposition.
6. Codex narrative/content fidelity disposition.
7. User playtest and approval.

Terrestrial-design flow:

1. User design goal.
2. Codex terrestrial-design packet/source.
3. Codex coordination/review technical handoff.
4. Codex engineering implementation/integration.
5. Codex coordination/review technical disposition and Codex terrestrial-design fidelity disposition.
6. User approval.

A mixed-mode PR requires a written Codex coordination/review justification explaining why separate PRs are impractical. It must still identify which source, design, engineering, and review responsibilities were performed.

## Branches and PRs

Allowed prefixes:

- `codex/coordination-<scope>` — planning, specification, review, governance, status, and risk.
- `codex/narrative-<scope>` — narrative/content mode.
- `codex/terrestrial-<scope>` — terrestrial-design mode.
- `codex/<scope>` — engineering mode.

`gpt/`, `android-studio/`, and `gemini/` are retired for new work.

Every PR must:

- represent one major completion;
- declare exactly one primary Codex mode;
- link the upstream issue, user decision, source packet, design packet, or specification;
- declare narrative, terrestrial design, runtime, contracts/catalogs, save, assets, workflow, and shared-file impact;
- list exact validation and every unperformed or unavailable check;
- stay focused and preserve collaborator work;
- update onto current `main` before final disposition;
- distinguish implementation, evidence, review, user approval, and release completion.

Use `.github/pull_request_template.md`.

## Shared-file locks

These require an exclusive soft lock:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

The first approved open PR declaring a file holds the lock. Later work must wait, depend on it, or use another integration point. New save fields require backward-compatible defaults. Conflict resolution must preserve valid services, fields, assets, contracts, tests, and registrations.

## Validation

- Coordination/review: verify current main, issue/PR state, dependencies, locks, source claims, acceptance criteria, evidence quality, and no stale completion claim.
- Narrative/content: verify stable IDs, references, branches, consequences, localization, failure/retry/recovery, and user-approved intent.
- Terrestrial design: verify scope, views, silhouette, scale, material, motion, variants, readability, source identity, and a clear engineering handoff without hidden gameplay authority.
- Engineering: run relevant builds/tests, report exact commands/results, preserve old saves and approved source, and disclose every blocked check.

Every task ends with the current phase, acceptance status, PR/issue state, shared locks, unresolved validation, and the next Codex mode or user step.
