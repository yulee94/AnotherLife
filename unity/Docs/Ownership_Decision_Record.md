# Ownership Decision Record

**Status date:** 2026-07-15  
**Decision owner:** User  
**Coordination owner:** GPT

## Final instruction chronology

The project received two successive terrestrial-ownership instructions in the active AnotherLife conversation:

1. Earlier: exclude terrestrial design from Codex and assign it to Gemini.
2. Later: “nevermind gemini is useless, give all responsibility and workload back to codex again.”

The later instruction supersedes the earlier one. Any document, issue, pull request, or comment that treats the earlier Gemini assignment as the latest decision is stale.

## Authoritative ownership

- GPT owns planning, dependency ordering, specifications, state/event/contract/save/test design, PR review, shared-file sequencing, status/risk records, and merge-readiness decisions.
- Codex narrative/content mode owns all narrative source and fidelity correction.
- Codex terrestrial-design mode owns all terrestrial creature/fauna visual-design source and fidelity correction.
- Codex engineering mode owns Android, Unity, runtime, gameplay, assets/import, scenes, saves/migrations/recovery, contracts/catalogs, builds, tests, CI, tooling, performance, and accessibility mechanics.
- The user owns final creative, product, visual-design, balance, irreversible-profile, milestone, playtest, and release approval.

Android Studio and Unity are tools, not agents or ownership workstreams. `android-studio/` and `gemini/` are retired branch prefixes for new work.

## Handoff rule

Narrative and terrestrial-design source normally precede engineering implementation in separate Codex-mode branches and PRs. GPT reviews/specifies between source and implementation. Engineering must consume approved source rather than silently rewrite or redesign it.

## Superseded governance

- PR #196’s Gemini assignment was superseded by the later user instruction.
- PR #201 correctly encoded the later instruction.
- PR #204 incorrectly treated the earlier instruction as latest and is superseded by this record and its restoring PR.

Technical specifications merged independently through PRs #197, #198, #200, and #202 remain valid and are not affected by this ownership correction.

## Change control

A future ownership change requires a new explicit user instruction dated after this record. Agents must compare instruction chronology before reverting governance. Earlier statements may not override a later statement merely because they are quoted in a newer pull request.