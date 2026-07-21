# Ownership Decision Record

**Status date:** 2026-07-16
**Decision owner:** User
**Responsible project owner-agent:** this Codex agent

## Final instruction chronology

The project received successive ownership instructions in the active AnotherLife conversation:

1. Terrestrial design was temporarily assigned outside Codex.
2. The user later returned all delivery responsibility to Codex while retaining GPT coordination/review.
3. The latest instruction is: this Codex agent owns all project workload and responsibility; GPT, Android Studio, Gemini, and other assistants/tools will not be used as project owners or workload holders.

The third instruction supersedes every earlier ownership model. Any document, issue, pull request, branch convention, or comment that requires a future GPT, Android Studio, Gemini, or external-assistant action is stale after this record unless the user later changes ownership again.

## Authoritative ownership

This Codex agent owns all project workload and responsibility through four declared modes:

- **Codex coordination/review mode:** planning, dependency ordering, specifications, state/event/contract/save/test design, issue/PR triage, technical and integration review, shared-file sequencing, status/risk/governance records, and merge-readiness disposition.
- **Codex narrative/content mode:** all narrative source, localization-facing meaning, continuity, and narrative-fidelity correction.
- **Codex terrestrial-design mode:** all terrestrial creature/fauna visual-design source and design-fidelity correction.
- **Codex engineering mode:** Android, Unity, runtime, gameplay, assets/import, scenes, saves/migrations/recovery, contracts/catalogs, builds, tests, CI, tooling, diagnostics, performance, and accessibility mechanics.
- **User:** final creative, product, visual-design, balance, irreversible-profile, milestone, integrated playtest, and release approval.

All modes share the user's standing optimization mandate: every project file, source packet, asset, generated artifact, dependency, runtime system, UI, and build choice must support broad device reach, low memory pressure, manageable performance, and the lowest feasible install size. Visual quality may scale upward by tier, but richer effects require an explicit quality/performance strategy.

GPT, Android Studio, Gemini, and other external assistants/tools receive no future planning, specification, review, merge-readiness, status, risk, agent/workstream, workload, or approval assignment. Android implementation or tooling that remains in project scope belongs to Codex engineering mode.

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

Narrative and terrestrial-design source normally precede engineering implementation in separate Codex-mode branches and PRs. Codex coordination/review mode specifies and dispositions the handoff between source and implementation. Engineering must consume approved source rather than silently rewrite or redesign it.

Because the same Codex agent may perform multiple modes, separation relies on focused PRs, explicit source/specification references, exact validation, preserved review history, and user approval where required—not on an independent GPT gate.

## Branch and review change

New branches use only Codex prefixes:

```text
codex/coordination-<scope>
codex/narrative-<scope>
codex/terrestrial-<scope>
codex/<engineering-scope>
```

`gpt/`, `android-studio/`, and `gemini/` are retired for new work.

## Superseded governance

All prior governance that describes a GPT–Codex–user operating model, assigns GPT a mandatory review/specification role, or treats Android Studio as an agent/workstream is superseded by this record.

Technical specifications merged before this decision remain valid technical contracts until Codex coordination/review mode or the user supersedes them. Ownership changes do not by themselves waive their acceptance criteria.

## Change control

A future ownership change requires a new explicit user instruction dated after this record. Instruction chronology must be compared before reverting governance. Earlier statements may not override this decision merely because they are quoted in a later pull request.
