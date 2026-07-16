# Ownership Decision Record

**Status date:** 2026-07-16
**Decision owner:** User
**Coordination owner:** Codex

## Final Instruction Chronology

The project received successive ownership instructions in the active AnotherLife conversation:

1. Earlier: exclude terrestrial design from Codex and assign it to Gemini.
2. Later: "nevermind gemini is useless, give all responsibility and workload back to codex again."
3. Later: the user stated they will work only with Codex on AnotherLife and will no longer use GPT or Android Studio for this project.
4. Latest: the user stated Codex will be the only one to work on the project, must review everything from the start, follow the plan, and handle current issues and PRs one by one.

The latest instruction supersedes earlier assignments. Any document, issue, pull request, or comment that treats GPT, Android Studio, Gemini, or another tool as an active owner, approval gate, or required reviewer is stale unless the user gives a newer explicit instruction.

## Authoritative Ownership

- Codex owns coordination, planning, dependency ordering, specifications, state/event/contract/save/test design, PR review, shared-file sequencing, status/risk records, merge-readiness decisions, issue/PR triage, and governance maintenance.
- Codex narrative/content mode owns all narrative source and fidelity correction.
- Codex terrestrial-design mode owns all terrestrial creature/fauna visual-design source and fidelity correction.
- Codex engineering mode owns Android, Unity, runtime, gameplay, assets/import, scenes, saves/migrations/recovery, contracts/catalogs, builds, tests, CI, tooling, performance, and accessibility mechanics.
- The user owns final creative, product, visual-design, balance, irreversible-profile, milestone, playtest, and release approval.

Android Studio, Unity, GPT, Gemini, and similar named systems are tools or retired labels only. They are not active agents or ownership workstreams. `gpt/`, `android-studio/`, and `gemini/` are retired branch prefixes for new work.

## Handoff Rule

Codex keeps mode boundaries even though one agent now owns all work. Narrative and terrestrial-design source normally precede engineering implementation in separate Codex-mode branches and PRs. Codex coordination/specification/review mode defines technical handoff requirements and merge readiness. Engineering must consume approved source rather than silently rewrite or redesign it.

## Superseded Governance

- PR #196's Gemini assignment was superseded by later user instruction.
- PR #201 correctly encoded the return of terrestrial design to Codex for that point in time.
- PR #204 incorrectly treated the earlier Gemini instruction as latest and is superseded.
- Any older GPT or Android Studio ownership language is superseded by the 2026-07-16 Codex-only instruction.

Technical specifications merged independently through earlier PRs remain usable as source material, but their active owner labels must be interpreted as Codex coordination/specification/review responsibilities.

## Change Control

A future ownership change requires a new explicit user instruction dated after this record. Agents must compare instruction chronology before reverting governance. Earlier statements may not override a later statement merely because they are quoted in a newer pull request.
