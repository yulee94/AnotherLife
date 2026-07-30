# AnotherLife Agent Instructions

These instructions apply to the entire repository. They define the working agreement for this Codex agent and the user.

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

### This Codex agent — A1 coordination, narrative, and engineering owner

This Codex agent owns project coordination/review, narrative/content, and engineering responsibility through three declared modes. Effective 2026-07-30, the user's co-developer exclusively owns future A2 terrestrial design and concept work.

#### Coordination/review mode

Owns milestone and backlog planning, dependency order, specifications, state/event/contract/save/test design, issue and PR triage, integration review, shared-file sequencing, status/risk/governance records, and merge-readiness disposition.

This mode must ground decisions in current source, written requirements, retained evidence, and user decisions. It must not treat prior merge state, issue closure, or a green but incomplete test as acceptance by itself.

#### Narrative/content mode

Owns quests, chapters, dialogue, NPCs, lore, artifacts, localization-facing copy, continuity, consequences, relationships, factions, stable narrative IDs, narrative packets, and narrative-fidelity correction.

### User's co-developer — A2 terrestrial design and concept

Owns future terrestrial creature/fauna concepts, silhouettes, anatomy, palettes, materials, habitat presentation, motion intent, scale, variants, design sheets, source assets, and design-fidelity correction.

Every terrestrial-source dependency, review request, or engineering need routes through A1 to the co-developer. No Codex agent may silently absorb A2 creative authority.

#### Engineering mode

Owns Android and Unity source, runtime services, gameplay, combat, bosses, loot, terrestrial runtime integration, assets/import, scenes, save/migration/recovery, catalogs, contracts, build systems, tests, CI, tooling, diagnostics, performance, and accessibility mechanics.

Engineering mode must consume approved narrative source and co-developer terrestrial-design source rather than silently inventing or redesigning either in runtime code.

Engineering mode also owns the standing optimization requirement: runtime code, assets, generated data, VFX, UI, catalogs, and builds must remain manageable for the broadest feasible device range with the lowest feasible install size. Prefer bounded data, pooling, compression, deduplication, lazy loading, deterministic generation, and scalable quality tiers before adding heavier content.

### User

The user owns final product, creative, visual-design, balance, irreversible-profile, milestone, integrated playtest, and release approval.

Except for the user's explicitly designated co-developer in the A2 terrestrial role, GPT, Android Studio, Gemini, and other external assistants/tools receive no future project work, coordination assignment, review gate, or approval responsibility. Historical GPT-authored specifications and reviews remain repository evidence until this Codex agent or the user explicitly supersedes them.

## Mode separation and handoffs

The same Codex agent may perform the three Codex modes, but coordination/review, narrative source authoring, and engineering implementation normally use separate branches and PRs so evidence and intent remain reviewable. Terrestrial source/design is an external co-developer handoff through A1.

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
2. A1 sequencing and scope.
3. Co-developer terrestrial-design packet/source.
4. A1 coordination/review technical handoff.
5. Codex engineering implementation/integration.
6. A1 technical disposition and co-developer design-fidelity disposition.
7. User approval.

A mixed-mode PR requires a written Codex coordination/review justification explaining why separate PRs are impractical. It must still identify which source, design, engineering, and review responsibilities were performed; this rule never transfers A2 creative authority to Codex.

## Branches and PRs

Allowed prefixes:

- `codex/coordination-<scope>` — planning, specification, review, governance, status, and risk.
- `codex/narrative-<scope>` — narrative/content mode.
- `codex/<scope>` — engineering mode.

Existing `codex/terrestrial-*` branches are historical or frozen only. Do not create a new A2 branch or PR until A1 records the co-developer's actual branch/mode convention and synchronizes the PR template and machine policy; do not invent a prefix.

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
- Terrestrial design/fidelity (co-developer): verify scope, views, silhouette, scale, material, motion, variants, readability, and source identity. A1 verifies the technical handoff and absence of hidden gameplay authority.
- Engineering: run relevant builds/tests, report exact commands/results, preserve old saves and approved source, and disclose every blocked check.
- Optimization: for any runtime, asset, VFX, UI, catalog, dependency, or packaging change, declare expected performance, memory, build-size, install-size, and device-compatibility impact plus any unperformed measurement.

Every task ends with the current phase, acceptance status, PR/issue state, shared locks, unresolved validation, and the next Codex mode, co-developer source step, or user step.
