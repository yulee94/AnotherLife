# AnotherLife Quality Gate Proof and Merge Controls

Issue: #155
Evidence cutoff: 2026-07-30

This record is the repository-local authority for the implemented quality-gate checks, protected `main` integration path, completed live proof PRs, retained artifact evidence, and remaining blockers. It replaces the obsolete Phase A plan and PR #231-only snapshot that previously occupied this file.

## Current disposition

- The four stable GitHub Actions contexts are implemented and required on pull requests to `main`.
- Classic `main` branch protection is enabled, strict/current-base, administrator-enforced, PR-only, conversation-resolved, non-force-pushable, and non-deletable.
- An ordinary exact-head integration through the protected path is proven.
- Live Android, hygiene, shared-file, stacked-base, release-applicability, failed-artifact, and sanitizer proofs are complete.
- Hosted Unity validation is still manual and is not a required context.
- Issue #155 remains open for the hosted Unity runner decision and the policy-depth follow-ups listed below.
- No narrative, terrestrial-design, product-runtime, save, catalog, asset, scene, or shared-lock authority is changed by this evidence record.

## Stable required contexts

Every pull request to `main` must report these exact contexts:

| Stable context | Always present | Work performed |
| --- | --- | --- |
| `policy / classify` | yes | Exact event-range and PR declaration/path policy |
| `repository / hygiene` | yes | Repository hygiene plus deterministic quality-gate fixtures |
| `android / unit-debug` | yes | Android JVM tests and debug assembly |
| `android / release` | yes | Applicability calculation; release lint/assembly only when applicable |

`android / release` is always a required successful job. For a non-applicable change, the applicability step succeeds, JDK/Gradle/release-build steps skip, and the evidence upload still succeeds. A skipped internal build step is not represented as a release build.

The workflow checks out the exact event head with full history and without persisted credentials. Pull-request and push ranges require full commit identities, exist locally, and match the checked-out head. Remote actions use immutable 40-hex pins. Job/step timeouts preserve upload headroom, and artifacts are retained for 90 days.

## Effective `main` protection

The effective classic protection rule was written and read back through the GitHub API on 2026-07-30:

- branch metadata reports `protected: true`;
- strict/current-base required-status policy is enabled;
- enforcement applies to everyone, including repository administrators;
- all four stable contexts above are required and pinned to GitHub Actions App ID `15368`;
- pull-request integration is required;
- mandatory human approval count is zero, preserving the A1 exact-head review workflow without adding an unrelated external-person dependency;
- review-conversation resolution is required;
- stale-review dismissal, code-owner review, and last-push approval are disabled;
- force pushes and branch deletion are disabled;
- no standing bypass allowance is configured;
- branch lock and required-linear-history are disabled, preserving reviewed squash integration.

The rejected organization-only payload returned HTTP 422 without mutating protection. The corrected personal-repository payload and the subsequent GET are the authoritative settings evidence. No direct-push, force-push, or deletion mutation was used as a destructive proof.

## Protected positive integration

PR #397 was the first ordinary integration after protection activation.

| Field | Evidence |
| --- | --- |
| Exact base | `4bd458086c63da42f2a76d8219a2ee18cb1a5b50` |
| Exact reviewed head | `b41b5c57c21c39599426ea844d0c911a971e50d2` |
| Pull-request run | `30508051043` |
| Jobs | hygiene `90761978020`; unit/debug `90761978034`; classify `90761978063`; release `90761978077` |
| A1 exact-head review | `4814696561` |
| Protected squash result | `main@8f145c780045373769e68b193897627bc7fc2b12` |

All four contexts passed before GitHub accepted the merge. The documentation-only release job remained present and successful while its JDK, Gradle, lint, and assembly steps correctly skipped.

Post-merge push run `30508657891` passed on the exact merged SHA: classify `90763795155`, hygiene `90763795183`, release `90763795218`, and unit/debug `90763795229`.

Documentation-only classification is also proven by PR #375 at head `ae6dfe9487f1fa01724f875e165aec8f25f1ff93`, run `30427138038`, including classify job `90495931266`. That run predates the stable release job; PR #397 above is the release-non-applicability proof.

## Completed live proof matrix

Every proof PR below was explicitly marked disposable, closed without merge after terminal evidence, and had its remote proof branch deleted. A required failing context made each negative head ineligible for protected integration.

| PR | Exact head | Run | Intended result | Retained evidence |
| --- | --- | ---: | --- | --- |
| #398 | `47f4d33356b2f220eebb6124cc7a0151e07af0f5` | `30509788151` | `android / unit-debug` failed at job `90767150455`; classify, hygiene, and release passed | unit/debug artifact `8746699730`, 10,754,204 bytes, `sha256:6142b4fcacff8775c47fb500008233507fc607c2f7ced466525b4082d1650b29`; failed-run upload passed |
| #399 | `35b03cd3c4e66110d333e0cd013ee486de13096e` | `30509880425` | hygiene job `90767430224` detected duplicate GUID, unsafe production test scene, missing enabled scene, mutable action reference, and overbroad `permissions: write-all`; other jobs passed | quality artifact `8746668845`, 690 bytes, `sha256:d0031bc14538c49dd3ab248aec8e9cdfe318c982a0600b5abb0f6372cae16c98` |
| #400 | `4bd3122ff58bcbc18ff1fbf2c42d8d2866bc83aa` | `30509943643` | classify job `90767625666` rejected an undeclared designated shared-file change; other jobs passed | quality artifact `8746699430`, `sha256:8c6d95fcc62da7014e4501dfc1cb4fdca86b0caa9a33c9a6b6ef2eb027300011` |
| #401 | `7a568bbb32959d6c9837dd4ccf4c652df5ce0cf1` | `30510080656` | classify job `90768170345` rejected a non-main base with no stacked/dependency declaration; other jobs passed | quality artifact `8746764520`, `sha256:769bc1848fbf16451b6f1a62ad1ad9b59aed22321756338cf84117e7bf6785e8` |
| #402 | `1d774d5951c8925e63834e6189aa3a7d4c686778` | `30510029020` | valid declared non-main relationship passed all four jobs | classify `90767882083`; hygiene `90767882099`; unit/debug `90767882117`; release `90767882096` |
| #406 | `dacafccd8ce01cf705822371b21036f4aa163e34` | `30515852333` | post-sanitizer hygiene failure retained actionable redacted evidence; classify, unit/debug, and release passed | quality artifact `8748830383`; exact byte evidence below |

PR #399 exposed an absolute hosted checkout root in the retained failure transcript. That disclosure invalidated any claim that the initial live-failure tranche had completed diagnostic redaction, so A1 corrected the sanitizer before finalizing this record.

## Diagnostic sanitizer correction

PR #404 added one fail-closed diagnostic boundary for quality-gate dynamic output and merged as `main@0510dc2c479724683b8c6bdf2c3f4c37573ce74b`.

| Field | Evidence |
| --- | --- |
| Exact base | `f670a63c60b04791e5a00ae560f5bfe36929e9a3` |
| Exact reviewed head | `c708c52c2a291f328b6ad1cdca121dd3c8ad0721` |
| Exact-head hosted run | `30515155022` |
| Jobs | classify `90783221125`; hygiene `90783221192`; unit/debug `90783221141`; release `90783221181` |
| A1 exact-head review | `4815483499` |
| Merge result | `0510dc2c479724683b8c6bdf2c3f4c37573ce74b` |

The correction redacts supported GitHub token shapes, bearer/basic/token authorization values, and exact repository roots. Windows drive/UNC roots are case-insensitive; Unix roots remain case-sensitive. Native, forward, reverse, and mixed separators are handled without masking sibling paths that merely share a root prefix. Sanitizer failure returns a fixed sentinel rather than raw input.

The deterministic `DiagnosticSanitization` fixture covers:

- token-shaped `BaseRef` and invalid `Mode` inputs;
- quoted authorization values;
- `Add-Failure` and top-level exception routes;
- native, forward, reverse, and mixed root separators;
- sibling roots using hyphen, plus, or space;
- sentence punctuation boundaries;
- Unix reverse separators and case-distinct paths;
- full-suite inclusion.

## Post-merge retained redaction proof

Disposable PR #406 changed only `unity/ProjectSettings/EditorBuildSettings.asset` to reference one intentionally missing scene whose path contained a synthetic hosted-root variant and a fake token canary.

Run `30515852333` produced:

| Job | ID | Result |
| --- | ---: | --- |
| `policy / classify` | `90785367511` | success |
| `repository / hygiene` | `90785367501` | intentional failure; evidence upload success |
| `android / unit-debug` | `90785367499` | success |
| `android / release` | `90785367520` | success; applicability false and build steps skipped |

Retained artifact proof:

- artifact `quality-gate-evidence-1`, ID `8748830383`;
- ZIP size `513` bytes;
- GitHub/local ZIP digest `sha256:2732e2444e9b908a17a3d38aa8b144c379cca7fd523f773e38cb7ca6da5493a4`;
- expiry `2026-10-28T05:09:50Z`;
- one retained file, `repository-hygiene.txt`, `552` bytes;
- transcript digest `sha256:ac56475c031d3fb9763d9f849b433227078e4eeeb74f833ea2db07d89667d390`.

Every extracted artifact file was checked. Required markers were present:

- `<repo>`;
- `<redacted-token>`;
- the typed missing-scene diagnostic ending in `QualityGateProofMissing.unity`;
- workflow wrapper `Repository hygiene exited with code 1.`

Forbidden values were absent:

- the raw fake token canary;
- native and forward hosted repository roots;
- canonical and A1 local repository roots;
- `<diagnostic-redaction-failed>`.

The hosted job log contains the final `1 quality gate failure(s)` summary. Direct `[Console]::Error` is not retained by the workflow's `Tee-Object`, so that summary is absent from the artifact while the actionable typed diagnostic and wrapper are retained. This is a disclosed transcript-capture limitation and a policy-depth follow-up, not a failed redaction assertion.

PR #406 was closed unmerged at the exact failing head. Its remote and local disposable branches were deleted without a corrective commit; the GitHub artifact remains under its 90-day retention.

## Repeatable local fixture coverage

From the repository root:

```powershell
./tools/ci/Test-AnotherLifeQualityGateFixtures.ps1 -Scenario All
```

The current suite creates disposable temporary repositories and checks:

- duplicate Unity GUID;
- production test scene and missing enabled scene;
- malformed tracked JSON;
- mutable, branch, tag, or short-SHA action references;
- diagnostic token, authorization, root, sibling, and Unix-case behavior;
- mixed source/engineering scope;
- coordination-only classification;
- retired branch prefixes;
- machine-policy authority;
- exact pull-request and protected-main push ranges;
- invalid and declared stacked bases;
- path additions, deletions, and renames;
- deleted designated shared files;
- Android release applicability for add/delete/rename/documentation changes.

Fixtures are deterministic local guards. Live PRs above prove the hosted workflow and protection behavior that local fixtures cannot prove.

## Ownership boundary

Terrestrial path classification is a policy signal only. It does not grant terrestrial creative authority.

Effective 2026-07-30, future A2 Terrestrial Design & Concept work belongs to the user's co-developer. A1 may coordinate dependencies and governance, but this task and other agents must not touch the former A2 worktree, PR #369, terrestrial source branches, or unpublished Sunmane/Rimecut/Ore Gallery packets. No machine-policy branch or mode convention for the new co-developer may be invented without the user's explicit convention.

## Hosted Unity blocker

Unity validation remains manual and unrequired because no repository runner/licensing model is approved and proven.

Before a Unity context can become required, one isolated model must prove:

- exact Unity `2022.3.62f3`;
- batch import and C# compile;
- EditMode XML;
- PlayMode XML after its production prerequisite;
- Player build smoke after its production prerequisite;
- logs/XML retained on success and failure;
- no committed or printed license, signing, profile, or developer-state material;
- one passing and one intentionally failing hosted proof.

Do not represent unavailable or skipped Unity execution as passing Unity validation.

## Remaining policy-depth work

Issue #155 remains open for:

1. hosted Unity runner/licensing/isolation approval and proof;
2. linked stacked-base authority: the classifier recognizes declaration text but does not prove the prerequisite PR identity, state, exact head, or A1 disposition;
3. semantic workflow-permission analysis beyond the already proven top-level `permissions: write-all` case, followed by a retained live proof for the strengthened rule; the current scanner also catches `pull_request_target` but not every YAML-equivalent overbroad permission;
4. an immutable digest policy if `docker://` actions are introduced;
5. deciding whether the final direct-stderr failure summary must also be retained in the quality artifact;
6. recording the user's exact co-developer branch/mode convention before changing the historical terrestrial classifier/prefix.

## Phase and next step

- Current phase: protected repository/Android quality gates with completed live failure and redaction evidence.
- Acceptance: four stable contexts, protection, protected integration, live negative/positive matrix, failure uploads, and sanitizer reproof accepted.
- Shared locks: none.
- User approval: not required for this evidence record; user milestone/playtest/release authority is unchanged.
- Unresolved validation: hosted Unity and the policy-depth items above.
- Next Codex mode: coordination/review selects one focused remaining #155 slice; engineering implements it separately if required.
