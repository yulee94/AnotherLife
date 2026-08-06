# Ownership Decision Record

**Status date:** 2026-08-06
**Decision owner:** User
**Responsible coordination/integration owner-agent:** A1, this Codex agent

## Final instruction chronology

The project received successive ownership instructions in the active AnotherLife conversation:

1. Terrestrial design was temporarily assigned outside Codex.
2. The user later returned all delivery responsibility to Codex while retaining GPT coordination/review.
3. On 2026-07-16, the user assigned all project workload and responsibility to this Codex agent; GPT, Android Studio, Gemini, and other assistants/tools ceased to be project owners or workload holders.
4. Effective 2026-07-30, the user ended the current Codex A2 Terrestrial Design & Concept assignment and transferred all future A2 terrestrial-source, design, and fidelity workload to the user's co-developer.
5. On 2026-08-06, after reviewing the co-developer's work in a meeting, the user granted the co-developer full project access and authority across coordination/review, narrative/content, terrestrial design, engineering, validation, publication, and integration, and instructed A1 to continue building on the merged work.

The fifth instruction supersedes the fourth instruction's A2-only limitation and the earlier prohibition on assigning non-A2 work to the co-developer. A1 remains the lead sequencer, coordinator, architect, reviewer, and integrator; A3-A7 remain available in their assigned support roles. The user retains every final product and approval gate. NVS-01 milestone terminology named `A2` still means narrative-fidelity review and is not changed by this contributor-authority decision.

## Authoritative ownership

Project delivery may be performed by Codex or the authorized co-developer through the declared modes. The contributor must declare the actual mode and follow its branch, source, validation, and handoff rules:

- **Codex coordination/review mode:** planning, dependency ordering, specifications, state/event/contract/save/test design, issue/PR triage, technical and integration review, shared-file sequencing, status/risk/governance records, and merge-readiness disposition.
- **Codex narrative/content mode:** all narrative source, localization-facing meaning, continuity, and narrative-fidelity correction.
- **A2 terrestrial design and concept:** terrestrial creature/fauna visual-design source, design packets, source assets, and design-fidelity correction remain the co-developer's source responsibility.
- **Codex engineering mode:** Android, Unity, runtime, gameplay, assets/import, scenes, saves/migrations/recovery, contracts/catalogs, builds, tests, CI, tooling, diagnostics, performance, and accessibility mechanics.
- **User:** final creative, product, visual-design, balance, irreversible-profile, milestone, integrated playtest, and release approval.

All owners share the user's standing optimization mandate: every project file, source packet, asset, generated artifact, dependency, runtime system, UI, and build choice must support broad device reach, low memory pressure, manageable performance, and the lowest feasible install size. Visual quality may scale upward by tier, but richer effects require an explicit quality/performance strategy.

The authorized co-developer may perform work in all declared modes. GPT, Android Studio, Gemini, and other assistants/tools do not independently receive project ownership, a workload, a review gate, or approval responsibility. Android implementation or tooling remains engineering-mode work regardless of whether Codex or the authorized co-developer performs it.

Full access does not waive focused branches/PRs, shared locks, evidence, source-fidelity, compatibility, optimization, or blocker-first sequencing. It does not satisfy a user creative, visual-design, balance, irreversible-profile, integrated-playtest, milestone, or release gate unless the user explicitly records that decision.

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

Narrative and terrestrial-design source normally precede engineering implementation in separate, reviewable changes. The authorized co-developer may contribute in any declared mode, while A1 coordinates ordering, locks, collision avoidance, integration review, and handoffs. Every terrestrial-source dependency still routes through A1 to the co-developer's A2 source role; no runtime change may silently redesign approved terrestrial source.

The terrestrial flow is:

1. user design goal;
2. A1 sequencing and scope;
3. co-developer terrestrial source/design packet;
4. A1 technical handoff;
5. Codex engineering integration;
6. A1 technical disposition plus co-developer design-fidelity disposition;
7. user creative/playtest approval.

Engineering must consume approved source rather than silently rewrite or redesign it. Separation relies on focused changes, explicit source/specification references, exact validation, preserved review history, and user approval where required.

## Accepted baseline and active handoff state

- The former A2 task, assigned A2 worktree, terrestrial branches, and source assets are no-touch for Codex.
- PR #369 remains frozen historical lineage and must not be edited, rebased, or accidentally merged.
- The co-developer-authored changes merged through `main@e0cbf6c1845489be6bf1032bb8c4d3a8e6dc7103`, including Slagfall v002 source PR #418 at exact source head `53dc2096fe4c9ac2bada8f05f88640788b8d938f`, are accepted by the user as the development baseline. This does not approve open drafts, production activation, integrated playtest, or release.
- The unpublished Sunmane, Rimecut, and Ore Gallery branches remain draft material for the new owner to reassess under A1 sequencing.
- New A2 source uses branch `a2/terrestrial-<scope>` and primary mode `A2 terrestrial design`; non-A2 co-developer work uses the matching `codex/` prefix and declared mode.

## Branch and review change

New Codex branches use these prefixes:

```text
codex/coordination-<scope>
codex/narrative-<scope>
codex/<engineering-scope>
a2/terrestrial-<scope>
```

Existing `codex/terrestrial-*` branches are historical or frozen. They do not authorize new Codex A2 work; `a2/terrestrial-*` is the only prefix for new co-developer A2 source.

`gpt/`, `android-studio/`, and `gemini/` are retired for new work.

## Superseded governance

All prior governance that limits the authorized co-developer to A2-only work, describes a mandatory GPT–Codex–user operating model, assigns GPT a mandatory review/specification role, treats Android Studio as an agent/workstream, or assigns future A2 terrestrial creative work to Codex is superseded by this record.

Technical specifications merged before this decision remain valid technical contracts until Codex coordination/review mode or the user supersedes them. Ownership changes do not by themselves waive their acceptance criteria.

## Change control

A future ownership change requires a new explicit user instruction dated after this record. Instruction chronology must be compared before reverting governance. Earlier statements may not override this decision merely because they are quoted in a later pull request.
