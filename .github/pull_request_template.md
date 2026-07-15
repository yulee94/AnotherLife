## Summary

Describe the single major completion in this pull request.

## Workstream owner

- [ ] GPT — planning, specification, review, or coordination documentation
- [ ] Android Studio — narrative content or narrative-owned progression logic
- [ ] Gemini — terrestrial creature/fauna visual design, design sources, or design manifests
- [ ] Codex — runtime implementation, tests, tooling, or technical contracts

Select exactly one primary workstream owner. A downstream integration PR may require additional owner dispositions without changing its primary owner.

## Upstream artifact or dependency

Link the narrative packet, terrestrial design package, implementation specification, issue, or pull request this work consumes. Write `None` only for a root coordination change.

## Ownership declaration

- Narrative content changed: `yes / no`
- Terrestrial visual design changed: `yes / no`
- Runtime gameplay code changed: `yes / no`
- Shared contracts or catalogs changed: `yes / no`
- Save data or migration behavior changed: `yes / no`
- Unrelated cleanup included: `no`

Explain any `yes` answer and confirm that the owning workstream made or approved the change.

For terrestrial work, identify issue #194 or its approved successor, the Gemini design package/version, source/provenance, and whether the PR is design-only or Codex runtime integration. Codex must not claim original terrestrial visual authorship.

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
- [ ] Approved narrative text, meaning, and outcomes were not rewritten outside the Android Studio workstream.
- [ ] Terrestrial creature/fauna visual design was not authored or redesigned outside the Gemini workstream.
- [ ] A Codex terrestrial integration consumes an approved Gemini package and records any fidelity deviation for Gemini/user review.
- [ ] New save fields have backward-compatible defaults when applicable.
- [ ] Existing service registrations, approved assets, and unrelated systems are preserved.

## Validation

List exact commands, test suites, design-manifest checks, source/provenance review, editor checks, or manual scenarios and their results.

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
- [ ] Terrestrial work follows issue #194 and `unity/Docs/Gemini_Terrestrial_Design_Prompt.md`.