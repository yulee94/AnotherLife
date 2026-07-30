## Summary

Describe the single major completion in this pull request.

## Primary delivery mode

Select exactly one:

- [ ] Codex coordination/review — planning, specification, triage, review, risk, governance, or status
- [ ] Codex narrative/content — quests, dialogue, lore, localization-facing source, continuity, or narrative fidelity
- [ ] A2 terrestrial design — co-developer terrestrial concepts, silhouettes, materials, motion intent, design source, or design fidelity
- [ ] Codex engineering — Android, Unity, runtime, gameplay, assets, build, save, contracts, tests, CI, or tooling

A mixed-mode PR requires a written Codex coordination/review justification explaining why separate PRs are impractical.

## Roadmap phase and upstream dependency

Link the issue, user decision, source packet, design packet, specification, or prerequisite PR. Write `None` only for a root governance change.

If a closed issue is relevant, state whether the current evidence confirms it remains solved or whether it should be reopened/followed up.

## Ownership declaration

- Narrative/content source changed: `yes / no`
- Terrestrial design source changed: `yes / no`
- Android or Unity runtime/gameplay changed: `yes / no`
- Assets, scenes, importers, or generated artifacts changed: `yes / no`
- Shared contracts or catalogs changed: `yes / no`
- Save data, migration, recovery, or deletion changed: `yes / no`
- Workflow, dependencies, or repository settings changed: `yes / no`
- Performance, memory, package size, install size, or device compatibility changed: `yes / no`
- Unrelated cleanup included: `no`

Explain every `yes` answer and identify the approved source, design, decision, or specification consumed.

## Shared-file lock

List every shared file touched, or write `None`:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

Confirm no other open PR holds the lock.

## What changed

- 

## Acceptance criteria

- [ ] Task-specific acceptance criteria are listed or linked.
- [ ] The diff stays within the declared primary mode and file scope.
- [ ] Narrative meaning was not rewritten outside Codex narrative/content mode.
- [ ] Terrestrial visual intent was not redesigned outside A2 terrestrial-design mode.
- [ ] Engineering consumes approved source/design rather than creating parallel hard-coded authority.
- [ ] Coordination/review claims are grounded in current source, issues/PRs, and retained evidence.
- [ ] New save fields have backward-compatible defaults when applicable.
- [ ] Existing service registrations, source packets, designs, assets, tests, and unrelated systems are preserved.
- [ ] Invalid data, unavailable dependencies, retries, and duplicate delivery are handled as required.
- [ ] Performance, memory, asset duplication, dependency weight, package size, install size, and low-end-device impact were assessed or explicitly marked not applicable.

## Validation

List exact commands, suites, validators, editor checks, source-reference reviews, design-fidelity checks, and manual scenarios with results.

```text
Not run: explain the exact blocker when applicable.
```

## Conflict and readiness checks

- [ ] Started from current `main`.
- [ ] Inspected all open PRs/issues plus relevant closed PRs/issues for overlap, dependencies, review findings, stale ownership labels, duplicated work, and regression history.
- [ ] Reopened or created follow-up issues when closed issue evidence no longer matches current source or Unity Hub play.
- [ ] Read `unity/Docs/Ownership_Decision_Record.md` before changing ownership-sensitive files.
- [ ] Declared all shared files before editing.
- [ ] Updated onto latest `main` before final disposition.
- [ ] No collaborator work was overwritten or force-pushed away.
- [ ] Branch prefix matches `codex/coordination-`, `codex/narrative-`, `codex/` engineering, or `a2/terrestrial-`.
- [ ] No future GPT, Android Studio, Gemini, or external-agent action is required by this PR.
- [ ] Documentation names the active Codex workspace/evidence path when workspace-specific validation is reported.
- [ ] No unnecessary generated, duplicate, cache, build output, oversized binary, or machine-local artifact is included.

## Review gates

- Codex coordination/review disposition: `BLOCKED / READY FOR SOURCE-MODE REVIEW / READY TO MERGE`
- Codex narrative/content fidelity disposition, when applicable: `pending / pass / changes required / not applicable`
- A2 terrestrial-design fidelity disposition, when applicable: `pending / pass / changes required / not applicable`
- User approval required: `yes / no`, with decision link when already recorded
