## Summary

Describe the single major completion in this pull request.

## Primary owner mode

Select exactly one:

- [ ] GPT — planning, specification, review, risk, or coordination documentation
- [ ] Codex narrative/content — quests, dialogue, lore, localization-facing source, continuity, or narrative fidelity
- [ ] Codex terrestrial design — terrestrial concepts, silhouettes, materials, motion intent, design source, or design fidelity
- [ ] Codex engineering — Android, Unity, runtime, gameplay, assets, build, save, contracts, tests, CI, or tooling

A mixed-mode PR requires an explicit GPT specification and a written reason that separate PRs are impractical.

## Roadmap phase and upstream dependency

Link the issue, user decision, source packet, design packet, specification, or prerequisite PR. Write `None` only for a root coordination change.

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
- [ ] The diff stays within the declared primary mode and file scope.
- [ ] Narrative meaning was not rewritten outside Codex narrative/content mode.
- [ ] Terrestrial visual intent was not redesigned outside Codex terrestrial-design mode.
- [ ] Engineering consumes approved source/design rather than creating parallel hard-coded authority.
- [ ] New save fields have backward-compatible defaults when applicable.
- [ ] Existing service registrations, source packets, designs, assets, and unrelated systems are preserved.
- [ ] Invalid data, unavailable dependencies, retries, and duplicate delivery are handled as required.

## Validation

List exact commands, suites, validators, editor checks, source-reference reviews, design-fidelity checks, and manual scenarios with results.

```text
Not run: explain the exact blocker when applicable.
```

## Conflict and readiness checks

- [ ] Started from current `main`.
- [ ] Inspected open PRs for overlap, dependencies, and ownership modes.
- [ ] Declared all shared files before editing.
- [ ] Rebased onto latest `main` before final review.
- [ ] No collaborator work was overwritten or force-pushed away.
- [ ] Branch prefix matches `gpt/`, `codex/narrative-`, `codex/terrestrial-`, or `codex/` engineering.
- [ ] Documentation uses `D:\260711\MY\AndroidStudioProjects\AnotherLife` as the canonical workspace.

## Review gates

- GPT disposition: `BLOCKED / READY FOR SOURCE-MODE REVIEW / READY TO MERGE`
- Codex narrative/content fidelity disposition, when applicable: `pending / pass / changes required / not applicable`
- Codex terrestrial-design fidelity disposition, when applicable: `pending / pass / changes required / not applicable`
- User approval required: `yes / no`, with decision link when already recorded