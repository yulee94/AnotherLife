## Summary

Describe the single major completion in this pull request.

## Workstream owner

- [ ] GPT — planning, specification, review, or coordination documentation
- [ ] Codex narrative/content — quests, dialogue, lore, IDs, consequences, or narrative fidelity
- [ ] Codex engineering — runtime implementation, Android/Unity source, tests, tooling, or technical contracts
- [ ] Codex design/assets — characters, monsters, terrestrials, items, gear, VFX, visual direction, or asset integration

Select exactly one primary workstream owner. A mixed PR must explain why it cannot be separated and how it will be reviewed.

## Upstream artifact or dependency

Link the narrative packet, design package, implementation specification, issue, or pull request this work consumes. Write `None` only for a root coordination change.

## Ownership declaration

- Narrative content changed: `yes / no`
- Design or asset source changed: `yes / no`
- Runtime gameplay code changed: `yes / no`
- Shared contracts or catalogs changed: `yes / no`
- Save data or migration behavior changed: `yes / no`
- Unrelated cleanup included: `no`

Explain any `yes` answer and confirm that the owning workstream made or approved the change.

## Shared-file lock

List every shared file touched, or write `None`:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

For each listed file, confirm that no other open pull request holds the soft lock.

## What changed

- 

## Acceptance criteria

- [ ] The task-specific acceptance criteria are listed or linked.
- [ ] The diff stays within the declared workstream ownership.
- [ ] Approved narrative text, meaning, and outcomes were not rewritten outside Codex narrative/content mode.
- [ ] Design or asset changes are declared and scoped to the active issue or user-approved direction.
- [ ] New save fields have backward-compatible defaults when applicable.
- [ ] Existing service registrations, approved assets, and unrelated systems are preserved.

## Validation

List exact commands, test suites, design or asset checks, editor checks, or manual scenarios and their results.

```text
Not run: explain why, when applicable.
```

## Conflict and readiness checks

- [ ] Started from current `main`.
- [ ] Inspected open pull requests for overlapping files and ownership areas.
- [ ] Declared all shared files before editing them.
- [ ] Rebased onto the latest `main` before final review.
- [ ] No collaborator work was overwritten or force-pushed away.
- [ ] Documentation uses `D:\260711\MY\AndroidStudioProjects\AnotherLife` as the canonical workspace.
