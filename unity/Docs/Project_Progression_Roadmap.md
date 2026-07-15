# AnotherLife Project Progression Roadmap

This roadmap defines how GPT, Codex, the Android Studio narrative workflow, and the user build Another Life from the current prototype into a stable, content-scalable release candidate. It controls sequencing and gates; it does not author narrative or implement gameplay.

## Authority and supporting documents

Use these documents together:

1. `AGENTS.md` — repository-wide ownership and conflict policy.
2. `unity/Docs/Agent_Role_Prompts.md` — standalone startup prompt for each workstream.
3. This roadmap — long-range phases, priorities, and exit gates.
4. `unity/Docs/Three_Way_Collaboration_Plan.md` — detailed NVS-01 execution plan.
5. `.github/pull_request_template.md` — required PR ownership, dependency, lock, and validation declaration.

When documents conflict, `AGENTS.md` is authoritative. The user decides product and creative direction. GPT resolves technical sequencing within that direction.

## Operating principles

- Keep `main` buildable before expanding scope.
- Deliver one thin, complete vertical slice before scaling content.
- Android Studio owns narrative source material; Codex consumes it; GPT specifies and reviews the handoff.
- Use stable IDs and data contracts instead of duplicating narrative in runtime code.
- Preserve old saves and existing service registrations.
- Prefer focused PRs with measurable acceptance evidence over large mixed changes.
- Do not maintain parallel PRs for the same issue unless the user explicitly requests alternatives.
- Do not advance to a later phase while the current phase gate is failing.

## Priority order inside every phase

1. Broken `main`, failed compilation, or unavailable core workflow.
2. Data loss, save corruption, security, or irreversible migration risk.
3. Blockers to the active phase gate.
4. Missing contracts, integration, automated tests, or diagnostics.
5. User-facing clarity and polish required by the active milestone.
6. Optional content expansion and speculative systems.

## Phase 0 — Collaboration Baseline and Build Health

### Goal

Establish one canonical workspace, one ownership model, one active implementation path per issue, and reliable Android and Unity validation commands.

### GPT workload

- Maintain `AGENTS.md`, role prompts, roadmap, PR template, and ownership decisions.
- Triage open issues and PRs for duplicate work and merge order.
- Identify build blockers, shared-file locks, and missing validation.
- Keep documentation aligned with the canonical workspace.

### Codex workload

- Reproduce and fix Android, Gradle, Unity, contract, and test blockers without changing narrative meaning.
- Add focused regression tests for repaired failures.
- Document exact build and test commands.
- Consolidate on one implementation when duplicate technical PRs exist.

### Android Studio workload

- Keep narrative work on isolated branches while build contracts are unstable.
- Do not merge narrative content that depends on unresolved or undocumented runtime behavior.
- Validate existing narrative IDs and references in preparation for NVS-01.

### User workload

- Choose between materially different duplicate implementations when technical review does not produce a clear winner.
- Confirm the intended creative direction for the first narrative slice.

### Exit gate

- Canonical workspace and agent rules are merged into `main`.
- Android unit tests and debug assembly have a known passing command, or every remaining blocker has an owned issue.
- Unity opens and compiles, or every remaining blocker has an owned issue.
- No unresolved duplicate PRs target the same root problem.
- No undeclared shared-file lock exists.

## Phase 1 — NVS-01: One Approved Quest Line End to End

### Goal

Prove one bounded, user-approved quest line can move from narrative source to a playable and persistent runtime loop without duplicated story logic.

The detailed task order and acceptance criteria live in `unity/Docs/Three_Way_Collaboration_Plan.md`.

### Android Studio workload

- Select one bounded quest line approved by the user.
- Produce the complete narrative packet: stable IDs, prerequisites, states, objectives, choices, consequences, gameplay handoff, completion/failure, retry/recovery, and localization references.

### GPT workload

- Verify packet completeness.
- Produce the state machine, runtime event map, contract changes, save/resume semantics, error behavior, expected file impacts, shared-file locks, and test matrix.

### Codex workload

- Implement contract loading and validation.
- Implement deterministic quest-state transitions and the approved gameplay handoff.
- Implement persistence, old-save compatibility, idempotency, and automated tests.

### User workload

- Approve the selected narrative packet and complete the final integrated playtest.

### Exit gate

- The quest can start, progress, branch, hand off to existing gameplay, resolve, save, reload, and resume.
- Rewards and consequences occur exactly as approved and do not duplicate after reload.
- Narrative fidelity is approved by Android Studio.
- GPT confirms contract, ownership, validation, and merge safety.
- The user approves the playtest.

## Phase 2 — Chapter 1 Playable Spine

### Goal

Generalize the NVS-01 path into a complete, coherent Chapter 1 flow while keeping content and runtime responsibilities separated.

### Android Studio workload

- Define the approved Chapter 1 structure, main progression, optional content, NPC arcs, factions, continuity, chapter entry, and chapter-close conditions.
- Maintain stable IDs and explicit cross-quest dependencies.
- Provide narrative packets in reviewable increments rather than one monolithic content dump.

### GPT workload

- Break Chapter 1 into dependency-ordered milestones.
- Identify reusable state patterns versus content-specific rules.
- Define contracts and acceptance tests for chapter unlocks, quest chains, optional branches, relationship effects, and recovery paths.
- Prevent NVS-specific implementation details from becoming permanent architecture.

### Codex workload

- Generalize the proven quest pipeline for multiple approved quests and chapter progression.
- Add reusable validation, persistence, objective, reward, and handoff mechanisms.
- Add regression coverage for cross-quest dependencies, chapter unlocks, and old saves.

### Exit gate

- A new profile can enter Chapter 1, complete its approved critical path, engage optional approved content, save/reload at supported states, and reach the chapter-close condition.
- No quest requires hard-coded dialogue or quest-specific runtime branching outside approved extensibility points.
- Invalid references and impossible transitions fail visibly.
- Chapter 1 narrative and runtime regression tests pass.

## Phase 3 — Connected Kingdom, Realm, Champion, and World Consequences

### Goal

Make approved narrative choices visibly affect existing gameplay systems without allowing either workstream to duplicate or own the other’s logic.

### Android Studio workload

- Define the narrative meaning and approved consequences of realm, faction, advisor, affinity, reputation, artifact, boss, and world-state choices.
- Specify semantic gameplay hooks and return events.

### GPT workload

- Define event ownership, payloads, idempotency, rollback behavior, save boundaries, and conflict resolution between systems.
- Sequence integration work to minimize shared-file contention.

### Codex workload

- Connect approved events to existing kingdom, realm, champion, encounter, loot, objective, and world-state services.
- Preserve service registrations and validate unavailable hooks.
- Add integration tests for cross-system consequences and reload behavior.

### Exit gate

- Approved narrative decisions produce deterministic, visible, persistent gameplay consequences.
- Gameplay outcomes return to narrative progression through documented events.
- Cross-system rewards and penalties cannot be duplicated by retries, reloads, or repeated event delivery.
- Existing unrelated gameplay remains operational.

## Phase 4 — Scalable Content and Authoring Pipeline

### Goal

Allow narrative and gameplay content to grow without manual reference drift, inconsistent IDs, or fragile one-off import steps.

### Android Studio workload

- Establish narrative naming conventions, ID registries, reusable content structures, continuity rules, localization keys, and authoring validation expectations.
- Expand approved content using the proven pipeline.

### GPT workload

- Define content governance, schema evolution policy, compatibility rules, versioning, review gates, and reporting requirements.
- Identify high-risk manual steps and convert them into tooling requirements.

### Codex workload

- Build or improve schema validators, catalog importers, ScriptableObject generators, editor diagnostics, localization reference checks, and batch validation.
- Keep cross-tool contracts plain and Fable-compatible where required.
- Add performance tests for larger catalogs.

### Exit gate

- New approved content can be added through documented steps without editing runtime code for ordinary cases.
- Duplicate IDs, missing references, invalid hooks, unsupported schema versions, and localization gaps are detected before runtime.
- Generated artifacts are reproducible and do not create noisy unrelated diffs.

## Phase 5 — Quality, Compatibility, Performance, and Recovery

### Goal

Harden the integrated game against old saves, invalid content, device differences, failure paths, and regression risk.

### Android Studio workload

- Review narrative pacing, clarity, continuity, recovery text, accessibility of presentation, and save/resume meaning.
- Resolve approved continuity and localization-facing defects.

### GPT workload

- Maintain the risk register and release-quality acceptance matrix.
- Prioritize defects by severity and player impact.
- Verify that fixes stay within ownership and do not reopen completed architecture decisions without evidence.

### Codex workload

- Expand Android and Unity regression suites.
- Test save migration, corrupted or partial data recovery, low-memory/device constraints, performance, loading, input modes, and error reporting.
- Improve diagnostics and safe fallback behavior.

### Exit gate

- Supported old saves load or migrate safely.
- Critical paths have automated regression coverage.
- Performance budgets and supported device checks are documented and met for the release target.
- Invalid data and interrupted flows recover visibly without silent progression or duplicated rewards.
- Accessibility and narrative clarity blockers are resolved or explicitly accepted by the user.

## Phase 6 — Release Candidate and Final Acceptance

### Goal

Create a traceable, reproducible release candidate with frozen scope, known validation, and user approval.

### Android Studio workload

- Freeze release narrative content.
- Resolve release-blocking continuity and localization-facing defects.
- Sign off narrative fidelity.

### GPT workload

- Freeze milestone scope and coordinate release-blocker triage.
- Verify issue, PR, contract, save, validation, and documentation traceability.
- Produce the final readiness report and unresolved-risk list.

### Codex workload

- Produce release builds and support CI, packaging, signing configuration, diagnostics, crash investigation, and release-blocking fixes.
- Run the full approved validation matrix and record exact evidence.

### User workload

- Perform final acceptance playtests and approve or reject the release candidate.

### Exit gate

- Release builds are reproducible.
- Required checks pass with recorded evidence.
- No release-blocking issue, undeclared shared-file lock, duplicate PR, or unreviewed migration remains.
- Narrative fidelity and product direction are approved by the user.
- The accepted release commit is identified and tagged according to the chosen release process.

## Recurring delivery cycle

Every feature or fix follows this cycle:

1. **Orient** — identify roadmap phase, owner, issue, upstream artifacts, dependencies, and open PRs.
2. **Specify** — define goal, non-goals, acceptance criteria, data/save effects, risks, and validation.
3. **Branch** — create one focused branch from latest `main` with the correct prefix.
4. **Implement or author** — stay within workstream ownership and declared file scope.
5. **Validate** — run exact relevant checks and record results.
6. **Review** — GPT checks technical and ownership fidelity; Android Studio checks narrative fidelity when applicable.
7. **Playtest** — the user validates player experience at milestone gates.
8. **Merge and close** — merge in dependency order, release locks, remove completed branches, and update milestone status.

## Milestone readiness checklist

Before any milestone is declared complete:

- The upstream artifact was approved.
- The implementation or content stayed within declared ownership.
- Every changed shared file was locked and reviewed.
- Save and contract compatibility were addressed.
- Required tests and manual scenarios have exact results.
- Narrative fidelity was checked when narrative was involved.
- No duplicate open PR still targets the same completion.
- The final integrated state exists on `main`.
- The user completed the required acceptance decision.

## Selecting the next task

GPT should select the next unblocked task using the active phase gate, not by choosing the most interesting feature. Android Studio and Codex should not self-assign work from a later phase when an earlier gate remains red. When a new request arrives, classify it as:

- **Blocker:** prevents the active gate or breaks `main`.
- **Required:** necessary to satisfy the active gate.
- **Follow-up:** valuable after the gate is complete.
- **Deferred:** speculative, later-phase, or currently unowned.

This classification must appear in the issue, specification, or PR summary so future sessions can recover project intent without relying on chat history.