# AnotherLife Agent Instructions

These instructions apply to the entire repository. They define the working agreement for GPT, Codex, and the user.

Android Studio and Unity are tools, not agents, owners, approval gates, or branch workstreams. `unity/Docs/Ownership_Decision_Record.md` records the final user instruction chronology.

## Canonical workspace

- Repository: `https://github.com/yulee94/AnotherLife`
- Integration branch: `main`
- Active checkout: `D:\260711\MY\AndroidStudioProjects\AnotherLife`
- Unity project: `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity`

Do not edit or publish from duplicate worktrees, timestamped copies, or backup repositories. No agent may commit directly to `main`.

## Required startup

Before work:

1. Read this file, `unity/Docs/Ownership_Decision_Record.md`, the matching prompt in `unity/Docs/Agent_Role_Prompts.md`, and the active gate in `unity/Docs/Project_Progression_Roadmap.md`.
2. Read `unity/Docs/Three_Way_Collaboration_Plan.md` for NVS-01; the legacy filename is retained for link stability.
3. Fetch current `main` and inspect all open issues and PRs for overlap, dependencies, and shared-file locks.
4. Create one focused branch and declare the goal, non-goals, owner mode, file scope, and acceptance criteria.

## Ownership

### GPT

GPT owns planning, dependency order, specifications, state/event/contract/save/test design, PR review, shared-file sequencing, status and risk documentation, and merge-readiness decisions.

GPT must not author or rewrite narrative content, terrestrial designs, visual source, or gameplay/application code unless the user separately reassigns a narrow task.

### Codex

All project delivery outside GPT's duties and the user's final approval belongs to Codex. Codex uses three declared modes.

#### Narrative/content mode

Owns quests, chapters, dialogue, NPCs, lore, artifacts, localization-facing copy, continuity, consequences, relationships, factions, stable narrative IDs, narrative packets, and narrative-fidelity correction.

#### Terrestrial-design mode

Owns terrestrial creature/fauna concepts, silhouettes, anatomy, palettes, materials, habitat presentation, motion intent, scale, variants, design sheets, source assets, and design-fidelity correction.

#### Engineering mode

Owns Android and Unity source, runtime services, gameplay, combat, bosses, loot, terrestrial runtime integration, assets and import, scenes, save/migration/recovery, catalogs, contracts, build systems, tests, CI, tooling, diagnostics, performance, and accessibility mechanics.

Engineering mode must consume approved narrative and terrestrial-design source rather than silently inventing or redesigning it in runtime code.

### User

The user owns final product, creative, visual-design, balance, irreversible-profile, milestone, playtest, and release approval.

## Mode separation and handoffs

The same Codex agent may perform every Codex mode, but source authoring/design and engineering implementation normally use separate branches and PRs.

Narrative flow:

1. User decisions.
2. Codex narrative packet.
3. GPT specification/review.
4. Codex engineering implementation.
5. GPT integration review.
6. Codex narrative-fidelity disposition.
7. User playtest and approval.

Terrestrial-design flow:

1. User design goal.
2. Codex terrestrial-design packet/source.
3. GPT technical handoff requirements.
4. Codex engineering implementation/integration.
5. GPT technical review and Codex design-fidelity disposition.
6. User approval.

A mixed-mode PR requires an explicit GPT specification and a written reason that separation is impractical.

## Branches and PRs

Allowed prefixes:

- `gpt/<scope>` — coordination, specifications, review, documentation.
- `codex/narrative-<scope>` — narrative/content mode.
- `codex/terrestrial-<scope>` — terrestrial-design mode.
- `codex/<scope>` — engineering mode.

`android-studio/` and `gemini/` are retired for new work.

Every PR must:

- represent one major completion;
- declare exactly one primary mode;
- link the upstream issue/artifact;
- declare narrative, terrestrial design, runtime, contracts/catalogs, save, assets, and shared-file impact;
- list exact validation and unperformed validation;
- stay focused and preserve collaborator work;
- rebase onto current `main` before final review.

Use `.github/pull_request_template.md`.

## Shared-file locks

These require an exclusive soft lock:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

The first approved open PR declaring a file holds the lock. Later work must wait, depend on it, or use another integration point. New save fields require backward-compatible defaults. Conflict resolution must preserve valid services, fields, assets, contracts, and registrations.

## Validation

- Documentation: verify paths, links, current issue/PR state, Markdown, and no behavior change.
- Narrative/content: verify stable IDs, references, branches, consequences, localization, failure/retry/recovery, and user-approved intent.
- Terrestrial design: verify scope, views, silhouette, scale, material, motion, variants, readability, and a clear engineering handoff without hidden gameplay authority.
- Engineering: run relevant builds/tests, report exact commands/results, preserve old saves and approved source, and disclose every blocked check.

Every task ends with the current phase, acceptance status, PR/issue state, shared locks, unresolved validation, and the next GPT, Codex-mode, or user step.