# AnotherLife Project Progression Roadmap

This roadmap defines how Codex, the user's co-developer, and the user move Another Life from prototype to release candidate. It controls sequence and gates; it does not itself author source or implement gameplay. `Ownership_Decision_Record.md` controls ownership chronology.

## Authority

Use together:

1. `AGENTS.md` — Codex modes, branches, locks, and conflict policy.
2. `unity/Docs/Ownership_Decision_Record.md` — final user ownership decision.
3. `unity/Docs/Agent_Role_Prompts.md` — standalone Codex prompt.
4. This roadmap — phase order and exit gates.
5. `unity/Docs/Product_Direction.md` — target gameplay, presentation, optimization, and end-to-end objective direction.
6. `unity/Docs/Three_Way_Collaboration_Plan.md` — NVS-01 plan; legacy filename retained.
7. `.github/pull_request_template.md` — required PR declaration.

`AGENTS.md` wins conflicts. The user decides creative and product direction. Codex A1 coordination/review mode resolves technical sequencing and acceptance disposition. Codex narrative/content and engineering modes perform their delivery; the user's co-developer owns A2 terrestrial source/design and fidelity through A1 sequencing.

## Operating principles

- Keep `main` buildable before expansion.
- Deliver thin complete slices before scaling.
- Codex narrative/content and co-developer terrestrial-design source precede Codex engineering implementation.
- Codex coordination/review mode specifies and reviews handoffs without silently rewriting source.
- Use stable IDs and validated data instead of duplicated hard-coded authority.
- Preserve old saves and service registrations.
- Optimize continuously for broad device reach, low memory pressure, scalable visual quality, and the lowest feasible install size.
- Reopen closed issues or create focused follow-ups when current source, Unity Hub play, or review evidence shows the issue still blocks the product direction.
- Use one focused PR per major completion and one primary Codex mode for Codex work. A2 source work waits for an A1-recorded co-developer branch/mode convention.
- Do not advance while the current phase gate is red unless the user reprioritizes.
- Historical GPT artifacts may be consumed as technical specifications/evidence, but no future GPT action or approval is required.

## Priority inside every phase

1. Broken `main` or unavailable core workflow.
2. Data loss, save corruption, security, economy, or irreversible migration risk.
3. Active phase blockers.
4. Missing contracts, integration, tests, and diagnostics.
5. Performance, memory, package size, device compatibility, and asset-pipeline risk.
6. Required user-facing clarity and accessibility.
7. Optional expansion and polish.

## Phase 0 — Governance and Build Health

### Goal

Establish one workspace, the A1-led ownership boundary, one active implementation path per issue, and reliable validation commands.

### Codex coordination/review mode

- Maintain governance, roadmap, status, risk, and gate policy.
- Triage issues/PRs, dependencies, review findings, and locks.
- Review build blockers and evidence against current source.

### Codex narrative/content mode

- Keep content isolated while runtime contracts are unstable.
- Validate current IDs and references without broad expansion.

### Co-developer A2 terrestrial-design owner through A1

- Keep design experiments isolated and deferred unless A1 routes a user-approved active task to the co-developer.
- Do not mix concept/design source into build-health PRs.

### Codex engineering mode

- Reproduce and fix Android, Gradle, Unity, asset, contract, and test blockers.
- Add focused regression tests and exact commands.
- Consolidate duplicate technical paths.

### User

- Resolve materially different product/design options when evidence cannot establish a clear technical answer.

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

### Codex coordination/review mode

- Review A1 against user decisions and active constraints.
- Publish G1 state/event/contract/persistence/test specification.
- Perform G2 integration, evidence, lock, and merge-risk review.

### Codex engineering mode

- Implement versioned content loading and strict validation.
- Implement deterministic state transitions and gameplay handoff.
- Implement persistence, migration, idempotency, recovery, and tests.

### Co-developer A2 terrestrial-design owner through A1

- No default Phase 1 workload. Terrestrial design starts only if the user explicitly makes it a dependency and A1 routes it to the co-developer; it remains separate source work.

### User

- Approve source intent and complete U1 integrated playtest.

### Exit gate

- Quest start, progression, branch, handoff, resolution, save, reload, and resume work.
- Consequences occur once and remain stable.
- A2 confirms source fidelity, Codex coordination/review confirms technical acceptance, and the user accepts U1.

## Phase 2 — Chapter 1 Playable Spine

### Goal

Generalize NVS-01 into a complete Chapter 1 flow without quest-specific runtime branching.

### Codex narrative/content mode

- Define Chapter 1 structure, critical path, optional content, NPC arcs, continuity, entry, and close conditions.
- Deliver bounded source packets with stable dependencies.

### Codex coordination/review mode

- Split Chapter 1 into dependency-ordered milestones.
- Define reusable state patterns, contracts, and tests.
- Prevent NVS-specific shortcuts from becoming architecture.

### Codex engineering mode

- Generalize quest, objective, reward, handoff, chapter, persistence, and validation systems.
- Add cross-quest and old-save regression coverage.

### Co-developer A2 terrestrial-design owner through A1

- Author terrestrial designs only for user-approved Chapter 1 subjects routed by A1, then return separate packets for A1 technical handoff.

### Exit gate

- A new profile can enter, progress through, save/reload within, and complete the approved Chapter 1 spine.
- Invalid references and impossible transitions fail visibly.
- Ordinary new quests do not require runtime code edits.

## Phase 3 — Connected Kingdom, Realm, Champion, and World Consequences

### Goal

Make approved choices visibly affect existing gameplay systems while preserving explicit source and runtime authority. This phase also starts aligning the playable spine with launch realm selection, 2.5D inner-kingdom progression, and 3D outer-warzone objectives.

### Codex narrative/content mode

- Define narrative meaning for realm, faction, advisor, artifact, boss, reward, and world-state consequences.
- Specify semantic hooks and return events.

### Co-developer A2 terrestrial-design owner through A1

- Define user-approved terrestrial fauna/creature/habitat presentation and design-fidelity criteria.
- Keep visual design separate from combat stats, AI, and runtime implementation.

### Codex coordination/review mode

- Define event ownership, payloads, idempotency, rollback, save boundaries, and sequence.
- Review integration against approved source and durable transaction contracts.

### Codex engineering mode

- Connect approved events and designs to kingdom, realm, champion, encounter, loot, objective, world-state, AI, rendering, and asset systems.
- Add integration, reload, performance, and accessibility tests.

### Exit gate

- Approved narrative decisions and terrestrial designs produce deterministic visible persistent results.
- Retries, reloads, and duplicate delivery cannot repeat consequences.
- Unrelated gameplay remains operational.
- Realm selection can lead into a credible inner-kingdom flow and a clearly marked path toward outer-warzone play.

## Phase 3B — Realm Warzone Objective Spine

### Goal

Build the serious MMO-style objective loop: direct 3D champion/lord control in the outer kingdom warzone, realm-vs-realm gate conflict, crossroads conflict, dragon/boss/gem objectives, Warmaster PvP point progression, and the eight-gem final wish path.

### Codex coordination/review mode

- Define durable objective contracts, realm ownership, gem theft/return rules, Warmaster point policy, dragon/boss objective identity, anti-duplication, save/reload behavior, and end-to-end validation.

### Codex narrative/content mode

- Define realm stakes, dragon/final-wish meaning, boss/realm identity, objective messaging, failure/retry/counterplay meaning, and user-facing copy.

### Co-developer A2 terrestrial-design owner through A1

- Provide approved creature/boss/realm visual source where needed, without granting runtime authority until engineering integration.

### Codex engineering mode

- Implement playable, persistent, optimized 3D objective systems only after required save, economy, catalog, battle, boss, territory, Warmaster, Realm Gem, and notification contracts are safe.

### Exit gate

- Realm-vs-realm warzone entry is playable from the kingdom flow.
- Main gate and crossroads objectives are visible and testable.
- Dragon/boss/gem/Warmaster objectives use durable IDs and duplicate-safe committed results.
- The eight-gem final wish path exists as a testable end-to-end objective or is explicitly blocked with focused issues.

## Phase 4 — Scalable Authoring and Asset Pipeline

### Goal

Scale content and terrestrial designs without ID drift, broken references, inconsistent assets, or manual import fragility across the A1/co-developer handoff.

### Codex narrative/content mode

- Establish naming, IDs, localization keys, continuity rules, reusable content structures, and source validation.

### Co-developer A2 terrestrial-design owner through A1

- Establish terrestrial design taxonomies, naming, variation rules, scale references, material conventions, motion briefs, LOD/readability expectations, and source-asset packaging.

### Codex coordination/review mode

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

### Co-developer A2 terrestrial-design owner through A1

- Review silhouette/readability at target distances, color-independent recognition, motion clarity, variant consistency, reduced-motion compatibility, and LOD fidelity.

### Codex coordination/review mode

- Maintain risk and release-quality matrices.
- Prioritize defects and prevent scope, ownership, evidence, and completion regression.

### Codex engineering mode

- Expand Android, Unity, asset, contract, save, device, performance, accessibility, and recovery tests.
- Improve diagnostics, safe failure behavior, memory behavior, asset compression, dependency weight, scalable quality settings, and build/install size.

### Exit gate

- Supported old saves load/migrate safely.
- Critical paths have regression coverage.
- Performance, memory, package-size, install-size, and device budgets are met.
- Invalid source and interrupted flows fail visibly without duplicated rewards or silent progression.
- Narrative and terrestrial-design clarity blockers are resolved or accepted by the user.

## Phase 6 — Release Candidate

### Goal

Produce a reproducible traceable release candidate with frozen source and user approval.

### Codex narrative/content mode

- Freeze release narrative, continuity, and localization-facing source.
- Sign off narrative fidelity.

### Co-developer A2 terrestrial-design owner through A1

- Freeze terrestrial design source and sign off integrated design fidelity.

### Codex coordination/review mode

- Freeze scope, review traceability, coordinate blockers, and publish readiness/risk reports.
- Verify that every acceptance claim matches current source and retained evidence.

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

1. **Orient** — Codex coordination/review identifies phase, issue, upstream source, dependencies, PRs, locks, and evidence.
2. **Author/design** — Codex narrative mode creates narrative source; A1 routes needed terrestrial source to the co-developer.
3. **Specify** — Codex coordination/review defines implementation and acceptance.
4. **Branch** — one focused Codex branch with the correct Codex mode; new A2 source waits for the recorded co-developer convention.
5. **Implement** — Codex engineering stays within scope.
6. **Validate** — exact relevant checks and retained evidence.
7. **Review** — Codex coordination/review technical disposition plus applicable Codex narrative or co-developer terrestrial-fidelity disposition.
8. **Playtest** — user milestone acceptance.
9. **Merge/close** — dependency order, release locks, update status.

## Milestone readiness

A milestone is complete only when upstream source is approved, declared Codex modes and the A2 co-developer boundary are respected, locks are released, save/contract compatibility is addressed, exact tests exist, source fidelity is checked, no duplicate PR remains, the integrated state is on `main`, and the user makes the required acceptance decision.

## Selecting the next task

Codex coordination/review mode selects the next unblocked task from the active gate. Codex must not self-assign later-phase work while an earlier gate is red. Classify new work as blocker, required, follow-up, or deferred in the issue/specification/PR.
