# Repository Quality Gate Policy

**Status date:** 2026-07-15  
**Policy owner:** GPT  
**Implementation owner:** Codex  
**Tracking issue:** #155  
**Baseline `main`:** `e1497ceb0ab666f28477ae814a17da06560d54c7`  
**Current phase:** Phase 0/1 foundation and continuous verification

This decision record defines which checks block a merge, which checks apply only to matching paths, which evidence remains informational, and which approvals remain manual. It does not implement workflows or change runtime, narrative, save, Android, Unity, or build behavior.

`AGENTS.md` remains authoritative for ownership, branch rules, shared-file locks, the canonical workspace, and the A1 → G1 → C1–C4 handoff.

## 1. Decisions

1. **Phase A repository and Android checks become the first required automated merge gate.**
2. **The Android unit-test and debug-assembly job runs on every pull request**, including documentation-only work. The repository is small enough that a universal baseline is preferable to another period in which Android evidence is missing or incorrectly borrowed from a different branch.
3. **Unity validation is not represented as passing while licensing or runner infrastructure is unavailable.** The interim model is retained manual/repository-dispatch evidence from the canonical licensed workspace. It is a manual merge gate for Unity-sensitive PRs, not a green CI status.
4. **A locked-down self-hosted Windows runner is the target Unity model**, but it becomes required only after the runner, #127 profile-safe PlayMode coverage, and #150 production Player build have each been proven on passing and intentionally failing test PRs.
5. **Path classification is automated but does not replace ownership review.** Shared, save-sensitive, narrative, workflow, dependency, and production-scene changes require explicit human disposition.
6. **No skipped, unavailable, cancelled, `continue-on-error`, or “not applicable” execution may be cited as passing product validation.**
7. **The repository currently uses one GitHub identity for the user and all agents.** GitHub cannot count that same identity as an independent approving reviewer on its own PR. Until a separate trusted reviewer identity or GitHub App exists, GPT and Android Studio review evidence is a documented manual gate rather than cryptographically independent branch-protection approval.

## 2. Gate categories

### 2.1 Required on every pull request

A failure blocks merge. These checks always execute and return a real result:

- `policy / classify`
- `repository / hygiene`
- `android / unit-debug`

### 2.2 Required when matching paths change

The applicability check always executes. When applicable, the underlying validation must execute and pass. When not applicable, the result must explicitly say `not applicable`; that result proves only path classification, not product validation.

- `android / release`
- `contracts / validate`
- `unity / compile`
- `unity / editmode`
- `unity / playmode`
- `unity / player-build`

### 2.3 Informational checks

These report risk and ownership but do not independently authorize merge:

- changed ownership area summary;
- designated shared-file summary;
- save-sensitive file summary;
- dependency/workflow change summary;
- generated-artifact or catalog drift summary;
- test and artifact inventory;
- local/manual Unity evidence status before the Unity runner is required.

An informational warning can still cause GPT to block a PR through review when it reveals an ownership, safety, or scope violation.

### 2.4 Manual gates

These cannot be replaced by a passing build:

- GPT contract, persistence, ownership, and integration review;
- Android Studio narrative-fidelity review for narrative-owned content;
- user U1 playtest and milestone acceptance;
- supported-device or release-signing validation;
- emergency override authorization;
- Unity validation while the runner remains in the manual-evidence stage.

## 3. Stable check names

Branch protection and documentation must use stable names. Workflow refactors may change job internals but not these external status names without a migration PR.

| Check | Initial state | Applies to | Merge effect |
| --- | --- | --- | --- |
| `policy / classify` | Phase A | every PR | required |
| `repository / hygiene` | Phase A | every PR | required |
| `android / unit-debug` | Phase A | every PR | required |
| `android / release` | Phase A | Android release/build-sensitive paths | required when applicable |
| `contracts / validate` | Phase A | shared contracts, schemas, catalogs, generated contract consumers | required when applicable |
| `unity / compile` | manual first; automated Phase B | Unity source, packages, project settings, shared contracts/catalogs consumed by Unity | manual gate, then required |
| `unity / editmode` | manual first; automated Phase B | Unity source/assets/editor/tests/project settings | manual gate, then required |
| `unity / playmode` | after #127 | runtime, scenes, boot, save, integration, Champion/kingdom flows | manual gate, then required |
| `unity / player-build` | after #150 | scenes, packages, project settings, production runtime, Android export dependencies | manual/release gate, then required |

## 4. `policy / classify`

This job uses the pull-request event payload, base/head refs, and changed-file list. It must not rely only on free-form prose.

### Required validations

- The base branch is `main`, unless the PR declares one already-approved prerequisite branch and links the approving issue/PR.
- A PR based on a closed, rejected, or unapproved feature branch fails classification.
- The PR links an issue or explicitly declares a root coordination change.
- Exactly one primary workstream owner is declared: GPT, Android Studio, or Codex.
- Narrative, runtime, shared-contract/catalog, save/migration, and unrelated-cleanup declarations are present.
- Every changed designated shared file appears in the shared-file declaration.
- The PR does not claim a shared-file lock already held by another open PR.
- A test-only PR that changes normal production Build Settings fails.
- A narrative-owned path mixed with runtime implementation requires an approved integration specification and both ownership dispositions.
- A PR that claims completion of an issue without `Fixes #...` or an explicit completion link fails.
- A draft PR is reported as not merge-ready even if all executable jobs pass.
- The head is rebased or otherwise up to date with the protected base before final merge.

### Machine-readable policy source

Codex should add a small reviewed file such as:

```text
.github/anotherlife-policy.yml
```

It should contain:

- workstream path groups;
- designated shared files;
- save-sensitive files and directories;
- narrative-owned paths;
- build-settings and test-scene rules;
- contract/catalog paths;
- forbidden tracked artifact patterns;
- stable status names.

The policy file is technical configuration. It does not transfer narrative ownership to Codex.

## 5. `repository / hygiene`

This required job runs with read-only repository permissions and enough history to compare the PR base and head.

### Required failures

- `git diff --check` failure for the PR range;
- tracked Unity `Library/`, `Temp/`, `Logs/`, `obj/`, build output, local cache, editor crash dump, or machine-specific generated artifact;
- tracked secrets, credentials, license files, signing material, or sensitive local paths detected by the reviewed scanner;
- duplicate GUID values among tracked Unity `.meta` files, excluding an explicit reviewed fixture used only to test the scanner;
- enabled Build Settings scene path that does not exist;
- `Assets/Test.unity` enabled in normal production Build Settings;
- duplicate enabled scene names that make string loading ambiguous;
- malformed deterministic JSON catalogs or JSON schemas;
- schema/catalog files whose committed validator fails;
- canonical-workspace documentation changed to a duplicate active checkout;
- executable workflow using unpinned third-party actions without an approved exception;
- PR workflow granting broader token permissions than required;
- use of `pull_request_target` to execute untrusted PR code with write credentials.

### Required reports

- all designated shared files changed;
- all save-sensitive files changed;
- all workflow/dependency files changed;
- all narrative-owned files changed;
- all production scene/build-setting files changed;
- all generated/schema/catalog files changed.

These reports feed review; they do not make an unsafe mixed scope acceptable.

## 6. `android / unit-debug`

This required job runs for every pull request:

```text
./gradlew :app:testDebugUnitTest :app:assembleDebug --no-daemon
```

### Requirements

- use a supported pinned JDK;
- use the committed Gradle wrapper;
- cache only reviewed Gradle inputs and never cache credentials;
- dependency resolution, repository, cache corruption, and network failures fail the job;
- scan logs for the known KSP/AWT failure signatures without suppressing unrelated failures;
- upload unit-test reports and the debug APK or deterministic build output;
- upload logs on both success and failure;
- do not use `continue-on-error`;
- record Java, Gradle, AGP, Kotlin, KSP, and Android SDK versions in the job summary.

### Artifact retention

- ordinary PR logs, reports, and APK artifacts: at least 14 days;
- release-candidate evidence: at least 90 days or the repository’s longer release-retention policy.

## 7. `android / release`

This job becomes required when any of the following changes:

- `app/**` runtime or UI code;
- `AndroidManifest.xml`;
- Gradle settings, plugins, dependencies, packaging, ProGuard/R8, resources, signing configuration, or build types;
- Android↔Unity host/export packaging;
- release/debug route gating;
- shared contracts/catalogs packaged into the Android application.

Minimum command:

```text
./gradlew :app:assembleRelease --no-daemon
```

Signing secrets are not required for an unsigned validation artifact. If the project’s release task requires unavailable signing material, the job must fail with a specific tracked blocker until a safe unsigned validation variant is provided. It must not silently downgrade to debug assembly.

## 8. `contracts / validate`

This job applies to:

```text
unity/SharedContracts/**
unity/Assets/AL/StreamingAssets/GameData/**
JSON schemas
Fable-compatible contracts
Android or Unity generated contract consumers
```

It must validate, where relevant:

- JSON/schema syntax;
- schema and catalog IDs and versions;
- duplicate and blank stable IDs;
- internal references;
- deterministic generation with a clean post-generation diff;
- absence of `UnityEngine` types from Fable-compatible contracts;
- Android/Unity consumer compatibility;
- generated-file provenance and source hash when the format supports it.

A successful parser alone is not enough when the owning issue requires semantic validation.

## 9. Unity validation model

### 9.1 Interim model: retained manual/repository-dispatch evidence

Until licensing and runner stability are proven, Unity-sensitive PRs require manual evidence produced from:

```text
D:\260711\MY\AndroidStudioProjects\AnotherLife\unity
Unity 2022.3.62f3
```

The evidence package must identify:

- base and head SHA;
- exact Unity executable version;
- exact commands;
- exit codes;
- complete compile log and `error CS` scan result;
- EditMode XML and totals when applicable;
- PlayMode XML and totals after #127 when applicable;
- Player build output, target, and launch smoke after #150 when applicable;
- missing-script/import diagnostics for serialized-asset work;
- final repository status;
- anything that could not run.

Logs and XML should be attached to the PR or uploaded by a manual dispatch workflow. They must not be committed as repository source.

A licensing IPC failure, unavailable runner, skipped test, absent XML, or exit code 199 is a **blocked validation**, not a pass.

### 9.2 Target model: locked-down self-hosted Windows runner

The target runner labels should identify at least:

```text
self-hosted
windows
anotherlife-unity
unity-2022.3.62f3
```

Before installation, `AGENTS.md` must explicitly permit an ephemeral CI-only checkout distinct from the one manual active workspace. Until that amendment is reviewed, the runner must not create a second editable project checkout.

Runner requirements:

- dedicated non-administrator service account;
- no access to the developer’s real `Application.persistentDataPath` profile;
- isolated working and temporary directories;
- one Unity job at a time through a concurrency group;
- locked Unity version;
- least-privilege secrets and documented revocation;
- no Unity license material committed or printed;
- clean workspace verification before and after every job;
- artifacts uploaded even when the command fails;
- runner disabled when patching, licensing, or integrity is uncertain.

### 9.3 When Unity checks become required

`unity / compile` and `unity / editmode` become protected required checks only after:

1. one current-main passing run;
2. one intentionally failing compiler/test PR is correctly rejected;
3. failure logs and XML are retained;
4. licensing and runner restart behavior are documented;
5. no developer profile or untracked workspace data is consumed.

`unity / playmode` additionally requires #127 complete and proven.

`unity / player-build` additionally requires #150 complete and proven.

## 10. Path-aware Unity matrix

| Changed area | Compile | EditMode | PlayMode | Player build |
| --- | --- | --- | --- | --- |
| Unity C# runtime/interface/data source | required | required | when runtime/startup/integration affected | when production flow/package affected |
| Unity editor/tooling/tests only | required | required | only when PlayMode infrastructure changes | no, unless build tooling changes |
| Unity scene/prefab/project settings | required | required validators | required | required |
| Save/service boot/integration | required | required | required after #127 | required after #150 when packaged flow changes |
| Shared contract/catalog consumed by Unity | required | required validation | when runtime load behavior changes | when packaging changes |
| Documentation only | not applicable | not applicable | not applicable | not applicable |

A `not applicable` result must identify the path decision and cannot be cited as Unity validation.

## 11. Manual ownership and review gates

### GPT disposition required

A GPT review comment tied to the current head SHA is required for:

- designated shared-file changes;
- save format, migration, recovery, deletion, or idempotency changes;
- runtime event, schema, contract, state-machine, or integration changes;
- issue closure where acceptance depends on more than compilation;
- stacked or dependency-sensitive PRs;
- workflow and branch-protection policy changes;
- narrative/runtime mixed integrations.

The disposition is one of:

```text
BLOCKED
READY FOR OWNER REVIEW
READY TO MERGE
```

It must list unresolved validation and shared-file state. A label alone is not review evidence.

### Android Studio disposition required

Android Studio narrative-fidelity review is required when a PR changes:

- dialogue, quest meaning, chapter placement, lore, NPC characterization, affinity/faction/persona meaning, narrative outcomes, localization-facing story copy, or narrative stable IDs;
- generated runtime content whose source is an Android Studio-owned packet.

### User disposition required

User approval is required for:

- creative/product decisions not already recorded;
- U1 milestone acceptance;
- supported-device/release-candidate acceptance;
- changes that intentionally alter player experience, balance, monetization, or irreversible profile behavior.

## 12. Branch protection for `main`

After Phase A passes one success PR and the required failure fixtures, configure:

- pull requests required for all changes;
- `policy / classify`, `repository / hygiene`, and `android / unit-debug` required;
- applicable required checks must pass before merge;
- branches must be up to date with `main` before merge, or use a proven merge queue;
- conversation resolution required;
- direct pushes, force pushes, and branch deletion blocked;
- administrators included in normal enforcement;
- squash merge as the default focused-PR method;
- auto-merge disabled for shared, save-sensitive, narrative, workflow, dependency, scene, and release-sensitive changes.

### Review-count limitation

Do not claim independent approval enforcement while all work uses the same GitHub identity. Setting a required approving-review count would block self-authored PRs without proving independent GPT or Android Studio review.

Until a separate trusted identity or GitHub App exists:

- use the manual disposition comments above;
- require automated checks;
- record the limitation in each shared/integration PR;
- do not represent a self-applied label as independent approval.

When a separate identity exists, add a focused branch-protection PR that requires the appropriate reviewer or status App.

## 13. Emergency override

An override is allowed only for an actively broken `main`, an imminent data-loss/security problem, or a release-blocking incident whose delay is more dangerous than the skipped gate.

Before merge, unless GitHub itself is unavailable, create an incident issue containing:

- affected commit and symptoms;
- exact skipped/failed check;
- reason the normal path is unsafe;
- narrow fix scope;
- rollback commit or procedure;
- owner and required post-merge validation;
- data, save, narrative, and release impact.

Override rules:

- no feature or cleanup scope;
- one narrowly scoped PR;
- unresolved validation is stated in the merge message;
- protection changes are restored immediately after the emergency merge;
- no unrelated PR merges until the incident’s missing validation is completed and recorded;
- a follow-up issue remains open if the root cause is not removed.

## 14. Workflow security

- default workflow token permissions are read-only;
- grant write permission only to a narrowly reviewed job that genuinely needs it;
- third-party actions are pinned to immutable commit SHAs;
- fork/untrusted PR code receives no secrets;
- `pull_request_target` must not checkout and execute the untrusted head;
- caches contain no credentials, keystores, licenses, or local profiles;
- logs redact secrets and do not print raw environment dumps;
- workflow artifacts contain no personal save/profile data;
- branch-protection or repository-setting mutation is never performed by an untrusted PR job.

## 15. Required proof PRs

Codex must prove the workflow using disposable branches and PRs:

1. current-main passing repository/Android PR;
2. intentional Android unit-test failure;
3. duplicate Unity `.meta` GUID fixture;
4. `Assets/Test.unity` enabled in Build Settings fixture;
5. missing enabled scene path fixture;
6. undeclared shared-file change fixture;
7. invalid/stacked base fixture;
8. artifact upload on failed job;
9. documentation-only PR with correct classification;
10. Android release-sensitive PR invoking `android / release`.

Failure-fixture PRs are closed unmerged after evidence is captured. Fixtures must not be merged into `main` merely to test CI.

## 16. Implementation order

1. Merge this policy record.
2. Codex adds `.github/anotherlife-policy.yml`, Phase A workflow, and deterministic validation scripts on `codex/repository-quality-gates`.
3. Run the proof PR matrix and attach run URLs, job IDs, and artifacts to #155.
4. GPT reviews false-positive/false-negative behavior and stable status names.
5. Configure and verify Phase A branch protection.
6. Add the Unity manual-evidence manifest/dispatch path.
7. Complete #127 and #150.
8. Amend `AGENTS.md` only if an ephemeral CI-only Unity checkout is approved.
9. Install and prove the self-hosted Unity runner.
10. Make Unity checks required through a separate reviewed protection change.

## 17. Expected implementation boundaries

Likely Codex-owned files:

```text
.github/workflows/quality-gate.yml
.github/anotherlife-policy.yml
scripts/ci/**
unity/Assets/AL/Tests/EditMode/** only for focused deterministic validators
README or agent documentation links required by #155
```

No gameplay, narrative, save, economy, combat, scene content, or broad dependency changes are authorized by this policy.

## 18. Acceptance mapping for #155

- Gate categories: defined in sections 2–3.
- Repository hygiene: defined in sections 4–5.
- Android tests/build and artifacts: defined in sections 6–7.
- Unity runner model: selected and staged in section 9.
- Duplicate GUID and Build Settings failures: required in sections 5 and 15.
- Ownership/shared-file classification: defined in sections 4 and 11.
- Passing/failing workflow proof: section 15.
- Branch controls: section 12.
- Secret/profile safety: sections 9 and 14.
- Documentation and commands: this record plus the future implementation PR.

# GPT handoff to Codex

```text
Codex: implement Phase A of issue #155 from current main using unity/Docs/Repository_Quality_Gate_Policy.md. Add one focused repository/Android workflow, machine-readable policy, and deterministic validators. Do not add Unity licensing secrets or pretend Unity is automated. Prove the required passing and failing fixtures, retain artifacts, and leave the implementation PR for GPT review before branch protection changes.
```
