## Summary

Describe the single major completion in this pull request.

## Primary Codex mode

Select exactly one, or justify a mixed-mode exception:

- [ ] Codex coordination/specification/review — planning, specification, review, risk, roadmap, issue/PR triage, or governance documentation
- [ ] Codex narrative/content — quests, dialogue, lore, localization-facing source, continuity, or narrative fidelity
- [ ] Codex terrestrial design — terrestrial concepts, silhouettes, materials, motion intent, design source, or design fidelity
- [ ] Codex engineering — Android, Unity, runtime, gameplay, assets, build, save, contracts, tests, CI, or tooling

Mixed-mode exception, if any:

```text
None
```

## Roadmap phase and upstream dependency

Link the issue, user decision, source packet, design packet, specification, prerequisite PR, or write `None` only for a root coordination change.

## Ownership declaration

- Narrative/content source changed: `yes / no`
- Terrestrial design source changed: `yes / no`
- Android or Unity runtime/gameplay changed: `yes / no`
- Assets, scenes, importers, or generated artifacts changed: `yes / no`
- Shared contracts or catalogs changed: `yes / no`
- Save data, migration, recovery, or deletion changed: `yes / no`
- Workflow, dependencies, or repository settings changed: `yes / no`
- Unrelated cleanup included: `no`

Explain every `yes` answer and identify the approved source/specification consumed.

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
- [ ] The diff stays within the declared primary mode and file scope, or the mixed-mode exception is justified.
- [ ] Narrative meaning was not rewritten outside Codex narrative/content mode.
- [ ] Terrestrial visual intent was not redesigned outside Codex terrestrial-design mode.
- [ ] Engineering consumes approved source/design rather than creating parallel hard-coded authority.
- [ ] New save fields have backward-compatible defaults when applicable.
- [ ] Existing service registrations, source packets, designs, assets, and unrelated systems are preserved.
- [ ] Invalid data, unavailable dependencies, retries, and duplicate delivery are handled as required.
- [ ] Stale GPT, Android Studio, Gemini, or other retired ownership labels are not introduced as active gates.

## Validation

List exact commands, suites, validators, editor checks, source-reference reviews, design-fidelity checks, and manual scenarios with results.

```text
Not run: explain the exact blocker when applicable.
```

## Conflict and readiness checks

- [ ] Started from current `main`.
- [ ] Inspected open and relevant closed PRs/issues for overlap, dependencies, stale ownership labels, duplicated work, and regression history.
- [ ] Read `unity/Docs/Ownership_Decision_Record.md` before changing ownership-sensitive files.
- [ ] Checked for duplicate authority, duplicate implementation paths, and stale owner boundaries.
- [ ] Declared all shared files before editing.
- [ ] Rebased onto latest `main` before final review.
- [ ] No existing work was overwritten or force-pushed away.
- [ ] Branch prefix matches `codex/spec-`, `codex/coordination-`, `codex/narrative-`, `codex/terrestrial-`, or `codex/`.

## Review gates

- Codex coordination/specification/review disposition: `BLOCKED / READY FOR SOURCE-MODE REVIEW / READY TO MERGE / NOT APPLICABLE`
- Codex narrative/content fidelity disposition, when applicable: `pending / pass / changes required / not applicable`
- Codex terrestrial-design fidelity disposition, when applicable: `pending / pass / changes required / not applicable`
- User approval required: `yes / no`, with decision link when already recorded
