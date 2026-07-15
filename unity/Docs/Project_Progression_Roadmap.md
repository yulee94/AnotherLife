# AnotherLife Project Progression Roadmap

This roadmap defines how GPT, Codex, and the user move Another Life from prototype to release candidate. It controls sequence and gates; it does not itself author source or implement gameplay.

## Authority

Use together:

1. `AGENTS.md` — ownership, branches, locks, and conflict policy.
2. `unity/Docs/Agent_Role_Prompts.md` — standalone GPT and Codex prompts.
3. This roadmap — phase order and exit gates.
4. `unity/Docs/Three_Way_Collaboration_Plan.md` — NVS-01 plan; legacy filename retained.
5. `.github/pull_request_template.md` — required PR declaration.

`AGENTS.md` wins conflicts. The user decides creative/product direction. GPT resolves technical sequencing. Codex performs all source-authoring, design, implementation, build, asset, and test work through declared modes.

## Operating principles

- Keep `main` buildable before expansion.
- Deliver thin complete slices before scaling.
- Codex narrative/content and terrestrial-design source precede Codex engineering implementation.
- GPT specifies and reviews handoffs but does not author source.
- Use stable IDs and validated data instead of duplicated hard-coded authority.
- Preserve old saves and service registrations.
- One focused PR per major completion and primary Codex mode.
- Do not advance while the current phase gate is red unless the user reprioritizes.

## Priority inside every phase

1. Broken `main` or unavailable core workflow.
2. Data loss, save corruption, security, economy, or irreversible migration risk.
3. Active phase blockers.
4. Missing contracts, integration, tests, and diagnostics.
5. Required user-facing clarity and accessibility.
6. Optional expansion and polish.

## Phase 0 — Governance and Build Health

### Goal

Establish one workspace, the GPT–Codex–user model, one active implementation path per issue, and reliable validation commands.

### GPT

- Maintain governance, roadmap, status, risk, and gate policy.
- Triage issues/PRs, dependencies, and locks.
- Review build blockers and evidence.

### Codex narrative/content mode

- Keep content isolated while runtime contracts are unstable.
- Validate current IDs and references without broad expansion.

### Codex terrestrial-design mode

- Keep design experiments isolated and deferred unless a user-approved active task needs them.
- Do not mix concept/design source into build-health PRs.

### Codex engineering mode

- Reproduce and fix Android, Gradle, Unity, asset, contract, and test blockers.
- Add focused regression tests and exact commands.
- Consolidate duplicate technical paths.

### User

- Resolve materially different product/design options when review cannot establish a clear technical answer.

### Exit gate

- Governance and workspace rules are on `main`.
- Android and Unity have known passing commands or owned blockers.
- No duplicate PR targets the same root problem.
- No undeclared shared-file lock exists.

## Phase 1 — NVS-01: One Approved Quest End to End

### Goal

Prove one bounded user-approved quest can move from Codex narrative source to a playable persistent runtime loop.

### Codex narrative/content mode

- Produce A1 with stable IDs, states, objectives, dialogue, choices, consequences, handoff, failure/retry/recovery, report, abandonment, resume, and localization.
- Perform A2 narrative-fidelity disposition after implementation.

### GPT

- Review A1.
- Publish G1 state/event/contract/persistence/test specification.
- Perform G2 integration and merge-risk review.

### Codex engineering mode

- Implement versioned content loading and strict validation.
- Implement deterministic state transitions and gameplay handoff.
- Implement persistence, migration, idempotency, recovery, and tests.

### Codex terrestrial-design mode

- No default Phase 1 workload. Terrestrial design starts only if the user explicitly makes it a dependency of the approved slice; it remains a separate source PR.

### User

- Approve source intent and complete U1 integrated playtest.

### Exit gate

- Quest start, progression, branch, handoff, resolution, save, reload, and resume work.
- Consequences occur once and remain stable.
- A2 confirms source fidelity, GPT confirms technical acceptance, and the user accepts U1.

## Phase 2 — Chapter 1 Playable Spine

### Goal

Generalize NVS-01 into a complete Chapter 1 flow without quest-specific runtime branching.

### Codex narrative/content mode

- Define Chapter 1 structure, critical path, optional content, NPC arcs, continuity, entry, and close conditions.
- Deliver bounded source packets with stable dependencies.

### GPT

- Split Chapter 1 into dependency-ordered milestones.
- Define reusable state patterns, contracts, and tests.
- Prevent NVS-specific shortcuts from becoming architecture.

### Codex engineering mode

- Generalize quest, objective, reward, handoff, chapter, persistence, and validation systems.
- Add cross-quest and old-save regression coverage.

### Codex terrestrial-design mode

- Author terrestrial designs only for user-approved Chapter 1 subjects and hand them to engineering through separate packets.

### Exit gate

- A new profile can enter, progress through, save/reload within, and complete the approved Chapter 1 spine.
- Invalid references and impossible transitions fail visibly.
- Ordinary new quests do not require runtime code edits.

## Phase 3 — Connected Kingdom, Realm, Champion, and World Consequences

### Goal

Make approved choices visibly affect existing gameplay systems while preserving explicit source and runtime authority.

### Codex narrative/content mode

- Define narrative meaning for realm, faction, advisor, artifact, boss, reward, and world-state consequences.
- Specify semantic hooks and return events.

### Codex terrestrial-design mode

- Define user-approved terrestrial fauna/creature/habitat presentation and design-fidelity criteria.
- Keep visual design separate from combat stats, AI, and runtime implementation.

### GPT

- Define event ownership, payloads, idempotency, rollback, save boundaries, and sequence.

### Codex engineering mode

- Connect approved events and designs to kingdom, realm, champion, encounter, loot, objective, world-state, AI, rendering, and asset systems.
- Add integration, reload, performance, and accessibility tests.

### Exit gate

- Approved narrative decisions and terrestrial designs produce deterministic visible persistent results.
- Retries, reloads, and duplicate delivery cannot repeat consequences.
- Unrelated gameplay remains operational.

## Phase 4 — Scalable Authoring and Asset Pipeline

### Goal

Scale content and terrestrial designs without ID drift, broken references, inconsistent assets, or manual import fragility.

### Codex narrative/content mode

- Establish naming, IDs, localization keys, continuity rules, reusable content structures, and source validation.

### Codex terrestrial-design mode

- Establish terrestrial design taxonomies, naming, variation rules, scale references, material conventions, motion briefs, LOD/readability expectations, and source-asset packaging.

### GPT

- Define governance, schema evolution, versioning, compatibility, review gates, and tool requirements.

### Codex engineering mode

- Build validators, importers, generators, editor diagnostics, localization/reference checks, asset-pipeline automation, and catalog performance tests.

### Exit gate

- New approved content/design can enter through documented source steps without ordinary runtime edits.
- Duplicate IDs, missing references, unsupported versions, localization gaps, and asset drift fail before runtime.
- Generated outputs are deterministic.

## Phase 5 — Quality, Compatibility, Performance, and Recovery

### Goal

Harden the game against old saves, invalid source, device differences, performance limits, accessibility failures, and interrupted flows.

### Codex narrative/content mode

- Review pacing, clarity, continuity, recovery copy, save/resume meaning, and localization-facing defects.

### Codex terrestrial-design mode

- Review silhouette/readability at target distances, color-independent recognition, motion clarity, variant consistency, reduced-motion compatibility, and LOD fidelity.

### GPT

- Maintain risk and release-quality matrices.
- Prioritize defects and prevent scope/ownership regression.

### Codex engineering mode

- Expand Android, Unity, asset, contract, save, device, performance, accessibility, and recovery tests.
- Improve diagnostics and safe failure behavior.

### Exit gate

- Supported old saves load/migrate safely.
- Critical paths have regression coverage.
- Performance/device budgets are met.
- Invalid source and interrupted flows fail visibly without duplicated rewards or silent progression.
- Narrative and terrestrial-design clarity blockers are resolved or accepted by the user.

## Phase 6 — Release Candidate

### Goal

Produce a reproducible traceable release candidate with frozen source and user approval.

### Codex narrative/content mode

- Freeze release narrative, continuity, and localization-facing source.
- Sign off narrative fidelity.

### Codex terrestrial-design mode

- Freeze terrestrial design source and sign off integrated design fidelity.

### GPT

- Freeze scope, review traceability, coordinate blockers, and publish readiness/risk reports.

### Codex engineering mode

- Produce builds, packaging, signing configuration, diagnostics, full validation, and release-blocking fixes.

### User

- Perform final acceptance and approve or reject the release candidate.

### Exit gate

- Reproducible builds and required checks pass.
- No blocker, undeclared lock, duplicate PR, or unreviewed migration remains.
- Narrative, terrestrial design, product direction, and player experience are accepted by the user.
- Accepted commit is identified and tagged.

## Recurring delivery cycle

1. **Orient** — phase, issue, upstream source, dependencies, PRs, locks.
2. **Author/design** — Codex narrative or terrestrial mode creates bounded source when needed.
3. **Specify** — GPT defines implementation and acceptance.
4. **Branch** — one focused branch with the correct mode.
5. **Implement** — Codex engineering stays within scope.
6. **Validate** — exact relevant checks and evidence.
7. **Review** — GPT technical/source fidelity; Codex source-mode disposition.
8. **Playtest** — user milestone acceptance.
9. **Merge/close** — dependency order, release locks, update status.

## Milestone readiness

A milestone is complete only when the upstream source is approved, declared ownership modes are respected, locks are released, save/contract compatibility is addressed, exact tests exist, source fidelity is checked, no duplicate PR remains, the integrated state is on `main`, and the user makes the required acceptance decision.

## Selecting the next task

GPT selects the next unblocked task from the active gate. Codex must not self-assign later-phase work while an earlier gate is red. Classify new work as blocker, required, follow-up, or deferred in the issue/spec/PR.