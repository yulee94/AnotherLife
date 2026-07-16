# AnotherLife Agent Instructions

These instructions apply to the entire repository. They define the current Codex-only working agreement for AnotherLife.

`unity/Docs/Ownership_Decision_Record.md` records the user instruction chronology. The latest user instruction makes Codex the only active project agent. GPT, Android Studio, Gemini, and other named tools are not owners, approval gates, branch workstreams, or required reviewers. Android Studio and Unity are tools only.

## Canonical Workspace

- Repository: `https://github.com/yulee94/AnotherLife`
- Integration branch: `main`
- Active checkout for this Codex session: `C:\Users\MY\Documents\AnotherLife`
- Unity project for this Codex session: `C:\Users\MY\Documents\AnotherLife\unity`

Do not edit or publish from duplicate worktrees, timestamped copies, or backup repositories. Do not commit directly to `main`.

## Required Startup

Before work:

1. Read this file, `unity/Docs/Ownership_Decision_Record.md`, `unity/Docs/Agent_Role_Prompts.md`, and the active gate in `unity/Docs/Project_Progression_Roadmap.md`.
2. Read `unity/Docs/Three_Way_Collaboration_Plan.md` for NVS-01; the legacy filename is retained for link stability.
3. Fetch current `main` and inspect all open issues/PRs plus relevant closed issues/PRs for overlap, dependencies, stale ownership language, duplicated work, regression history, and shared-file locks.
4. Create or use one focused `codex/` branch and declare the goal, non-goals, Codex mode, file scope, and acceptance criteria.
5. Check for duplicated plans, duplicated authority, competing branches, or stale role boundaries before starting new feature work.

## Ownership

### Codex

Codex owns all active project work except the user's final approval. Codex may operate in separate declared modes, and one PR should normally have exactly one primary mode.

#### Coordination, Specification, and Review Mode

Owns planning, dependency order, issue/PR triage, specifications, state/event/contract/save/test design, PR review, shared-file sequencing, status/risk documentation, merge-readiness decisions, and governance cleanup.

#### Narrative/Content Mode

Owns quests, chapters, dialogue, NPCs, lore, artifacts, localization-facing copy, continuity, consequences, relationships, factions, stable narrative IDs, narrative packets, and narrative-fidelity correction.

#### Terrestrial-Design Mode

Owns terrestrial creature/fauna concepts, silhouettes, anatomy, palettes, materials, habitat presentation, motion intent, scale, variants, design sheets, source assets, and design-fidelity correction.

#### Engineering Mode

Owns Android and Unity source, runtime services, gameplay, combat, bosses, loot, terrestrial runtime integration, assets and import, scenes, save/migration/recovery, catalogs, contracts, build systems, tests, CI, tooling, diagnostics, performance, and accessibility mechanics.

Engineering mode must consume approved narrative and terrestrial-design source rather than silently inventing or redesigning it in runtime code.

### User

The user owns final product, creative, visual-design, balance, irreversible-profile, milestone, playtest, and release approval.

## Mode Separation and Handoffs

The same Codex agent may perform every Codex mode, but source authoring/design, coordination/specification/review, and engineering implementation normally use separate branches and PRs. When separation is impractical, the PR must state why and list every mixed-mode impact.

Narrative flow:

1. User decisions.
2. Codex narrative packet.
3. Codex coordination/specification/review.
4. Codex engineering implementation.
5. Codex integration review and narrative-fidelity disposition.
6. User playtest and approval.

Terrestrial-design flow:

1. User design goal.
2. Codex terrestrial-design packet/source.
3. Codex technical handoff requirements.
4. Codex engineering implementation/integration.
5. Codex technical review and design-fidelity disposition.
6. User approval.

## Branches and PRs

Allowed prefixes:

- `codex/spec-<scope>` or `codex/coordination-<scope>` for coordination, specification, review, roadmap, status, or governance work.
- `codex/narrative-<scope>` for narrative/content mode.
- `codex/terrestrial-<scope>` for terrestrial-design mode.
- `codex/<scope>` for engineering mode.

`gpt/`, `android-studio/`, and `gemini/` are retired for new work.

Every PR must:

- represent one major completion;
- declare exactly one primary Codex mode, or explicitly justify a mixed-mode exception;
- link the upstream issue/artifact/user decision;
- declare narrative, terrestrial design, runtime, contracts/catalogs, save, assets, workflow, and shared-file impact;
- list exact validation and unperformed validation;
- stay focused and preserve existing work;
- rebase onto current `main` before final review.

Use `.github/pull_request_template.md`.

## Shared-File Locks

These require an exclusive soft lock:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

The first approved open PR declaring a file holds the lock. Later work must wait, depend on it, or use another integration point. New save fields require backward-compatible defaults. Conflict resolution must preserve valid services, fields, assets, contracts, and registrations.

## Validation

- Documentation: verify paths, links, current issue/PR state, Markdown, stale ownership language, duplicate authority, and no behavior change.
- Narrative/content: verify stable IDs, references, branches, consequences, localization, failure/retry/recovery, and user-approved intent.
- Terrestrial design: verify scope, views, silhouette, scale, material, motion, variants, readability, and a clear engineering handoff without hidden gameplay authority.
- Engineering: run relevant builds/tests, report exact commands/results, preserve old saves and approved source, and disclose every blocked check.

Every task ends with the current phase, acceptance status, PR/issue state, shared locks, unresolved validation, and the next Codex-mode or user step.
