# Ownership Decision Record

**Status date:** 2026-07-30
**Decision owner:** User
**Responsible coordination/integration owner-agent:** A1, this Codex agent

## Final instruction chronology

The project received successive ownership instructions in the active AnotherLife conversation:

1. Terrestrial design was temporarily assigned outside Codex.
2. The user later returned all delivery responsibility to Codex while retaining GPT coordination/review.
3. On 2026-07-16, the user assigned all project workload and responsibility to this Codex agent; GPT, Android Studio, Gemini, and other assistants/tools ceased to be project owners or workload holders.
4. Effective 2026-07-30, the user ended the current Codex A2 Terrestrial Design & Concept assignment and transferred all future A2 terrestrial-source, design, and fidelity workload to the user's co-developer.

The fourth instruction supersedes the third only for agent A2 terrestrial creative ownership. A1 and A3-A7 responsibilities otherwise remain unchanged. Older records that assign future terrestrial creative work to Codex are stale; NVS-01 milestone terminology named `A2` still means narrative-fidelity review and is not changed by this agent-role transfer.

## Authoritative ownership

This Codex agent owns coordination, narrative/content, and engineering responsibility through three declared modes. The user's co-developer owns the separate A2 terrestrial-design source role:

- **Codex coordination/review mode:** planning, dependency ordering, specifications, state/event/contract/save/test design, issue/PR triage, technical and integration review, shared-file sequencing, status/risk/governance records, and merge-readiness disposition.
- **Codex narrative/content mode:** all narrative source, localization-facing meaning, continuity, and narrative-fidelity correction.
- **User's co-developer — A2 terrestrial design and concept:** all future terrestrial creature/fauna visual-design source, design packets, source assets, and design-fidelity correction.
- **Codex engineering mode:** Android, Unity, runtime, gameplay, assets/import, scenes, saves/migrations/recovery, contracts/catalogs, builds, tests, CI, tooling, diagnostics, performance, and accessibility mechanics.
- **User:** final creative, product, visual-design, balance, irreversible-profile, milestone, integrated playtest, and release approval.

All owners share the user's standing optimization mandate: every project file, source packet, asset, generated artifact, dependency, runtime system, UI, and build choice must support broad device reach, low memory pressure, manageable performance, and the lowest feasible install size. Visual quality may scale upward by tier, but richer effects require an explicit quality/performance strategy.

Except for the user's explicitly designated co-developer in the A2 terrestrial role, GPT, Android Studio, Gemini, and other external assistants/tools receive no future planning, specification, review, merge-readiness, status, risk, agent/workstream, workload, or approval assignment. Android implementation or tooling that remains in project scope belongs to Codex engineering mode.

The active Codex workspace for this record is `C:\Users\MY\Documents\AnotherLife`. Historical paths may continue to contain `AndroidStudioProjects` in their directory name; that historical filesystem name does not assign ownership or require Android Studio use.

## Historical artifacts

Historical GPT-authored specifications, reviews, status records, and issue comments remain technical repository evidence. They do not create a future GPT approval gate.

Codex coordination/review mode may:

- consume them unchanged;
- correct them when current source or evidence disproves them;
- supersede them with a focused Codex coordination PR;
- carry unresolved requirements forward into implementation review.

Existing GPT review comments on open PRs remain actionable technical findings unless Codex coordination/review mode documents why a finding is resolved, obsolete, or incorrect. No PR should wait for another GPT response.

## Handoff rule

Narrative and terrestrial-design source normally precede engineering implementation in separate, reviewable changes. Narrative source remains a Codex narrative/content responsibility. Every terrestrial-source dependency, review request, or engineering need routes through A1 to the user's co-developer; no Codex agent may silently absorb A2 creative authority.

The terrestrial flow is:

1. user design goal;
2. A1 sequencing and scope;
3. co-developer terrestrial source/design packet;
4. A1 technical handoff;
5. Codex engineering integration;
6. A1 technical disposition plus co-developer design-fidelity disposition;
7. user creative/playtest approval.

Engineering must consume approved source rather than silently rewrite or redesign it. Separation relies on focused changes, explicit source/specification references, exact validation, preserved review history, and user approval where required.

## Active A2 handoff state

- The former A2 task, assigned A2 worktree, terrestrial branches, and source assets are no-touch for Codex.
- PR #369 remains frozen at its exact source head for user creative review, with `UserCreativeState: NotRequested` and `RuntimeIntegrationState: Blocked`; it must not be edited or rebased on the former A2 task's behalf.
- The unpublished Sunmane, Rimecut, and Ore Gallery branches remain draft material for the new owner to reassess under A1 sequencing.
- No new A2 branch or PR is authorized until A1 records the co-developer's actual branch/mode convention and synchronizes the PR template and machine policy. A1 must not invent that convention.

## Branch and review change

New Codex branches use these prefixes:

```text
codex/coordination-<scope>
codex/narrative-<scope>
codex/<engineering-scope>
```

Existing `codex/terrestrial-*` branches are historical or frozen. They do not authorize new Codex A2 work.

`gpt/`, `android-studio/`, and `gemini/` are retired for new work.

## Superseded governance

All prior governance that describes a GPT–Codex–user operating model, assigns GPT a mandatory review/specification role, treats Android Studio as an agent/workstream, or assigns future A2 terrestrial creative work to Codex is superseded by this record.

Technical specifications merged before this decision remain valid technical contracts until Codex coordination/review mode or the user supersedes them. Ownership changes do not by themselves waive their acceptance criteria.

## Change control

A future ownership change requires a new explicit user instruction dated after this record. Instruction chronology must be compared before reverting governance. Earlier statements may not override this decision merely because they are quoted in a later pull request.
