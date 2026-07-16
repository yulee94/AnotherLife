# AnotherLife Quality Gate Proof and Merge Controls

Issue: #155

This record defines the current proof evidence, repeatable fixture checks, required repository settings, and remaining Unity-runner blocker for the staged AnotherLife quality gate.

## Required checks

Required on every pull request to `main`:

- `policy / classify`
- `repository / hygiene`
- `android / unit-debug`

Manual until the Unity runner model is approved and stable:

- Unity batch compile
- Unity EditMode XML
- Unity PlayMode XML after #127
- production Player build smoke after #150

Do not treat skipped or unavailable Unity CI as passing Unity evidence.

## Current passing evidence

Recent current-main PR evidence after the Phase A workflow landed:

| PR | Head | Workflow run | Result |
| --- | --- | --- | --- |
| #231 | `20f1ec42c75b05aa82b9eb20c49589eabd4694f6` | AnotherLife Quality Gates #11 / run `29468323751` | success |

Run #11 passed:

- `policy / classify`
- `repository / hygiene`
- `android / unit-debug`

Local rebased validation for the same PR also passed:

- `git diff --check origin/main...HEAD`
- Unity EditMode `AL.EditMode.Tests`: `35` total, `35` passed, `0` failed, `0` skipped

## Fixture failure proof

Run from the repository root:

```powershell
./tools/ci/Test-AnotherLifeQualityGateFixtures.ps1
```

The fixture script creates disposable temporary Git repositories and verifies expected failures without changing the working tree.

The hygiene fixture proves these failures are detected:

- duplicate Unity `.meta` GUID
- `Assets/Test.unity` enabled in production Build Settings
- missing enabled Build Settings scene
- malformed tracked JSON
- mutable major-version GitHub Action tag

The classification fixture proves these failures/signals are detected:

- current terrestrial source path `unity/Docs/Terrestrials/**`
- engineering path `unity/Assets/AL/Scripts/**`
- mixed source-mode and engineering paths without explicit mixed-mode justification

The script must be rerun after meaningful policy or classifier changes. It is not a substitute for temporary live failing PRs, but it gives a deterministic local guard before those PRs are opened.

## Branch Protection

Configure the default branch `main` with a ruleset or branch-protection rule that applies to direct pushes and pull requests.

Recommended required settings:

- require a pull request before merging;
- block direct pushes to `main`;
- block force pushes and branch deletion;
- require branches to be up to date before merge, or enable a merge queue if available;
- require status checks to pass before merge:
  - `policy / classify`
  - `repository / hygiene`
  - `android / unit-debug`
- require status checks to be current on the latest commit;
- require conversation resolution before merge;
- block merge of draft pull requests through repository settings where available and through the human review gate otherwise;
- require administrator bypasses to be logged in an incident issue with follow-up validation;
- do not require Unity checks until the Unity runner model is implemented and proven.

Manual maintainer verification should record either:

- a screenshot of the active ruleset/branch-protection settings, or
- API evidence listing the required checks and bypass policy.

## Temporary Failing PR Proofs

Use short-lived branches to prove live GitHub behavior after this document and fixture script merge. Close each PR after evidence is captured.

Recommended branches:

| Branch | Intentional failure |
| --- | --- |
| `codex/quality-gate-proof-android-failure` | add one failing Android unit test |
| `codex/quality-gate-proof-duplicate-meta` | add two temporary `.meta` files with the same GUID |
| `codex/quality-gate-proof-buildsettings-test-scene` | enable `Assets/Test.unity` in Build Settings |
| `codex/quality-gate-proof-missing-scene` | enable a missing scene path in Build Settings |
| `codex/quality-gate-proof-mixed-scope` | mix terrestrial source and runtime paths without justification |

Each proof PR must:

- target `main`;
- state that it is intentionally failing and must not merge;
- link #155;
- capture the failing check name, run ID, and failure excerpt;
- be closed without merge after evidence is recorded.

## Unity Runner Blocker

Unity validation remains manual because no stable repository runner/licensing path has been approved. A future #155 PR may choose one model:

- GitHub-hosted runner with reviewed Unity activation;
- locked-down self-hosted Windows runner matching Unity `2022.3.62f3`;
- repository-dispatched/manual evidence workflow.

Required future Unity checks:

- exact Unity version;
- batch import/C# compile;
- EditMode XML;
- PlayMode XML after #127;
- Player build smoke after #150;
- logs and XML retained as artifacts even on failure;
- no use of the developer's real profile outside isolated test paths.
