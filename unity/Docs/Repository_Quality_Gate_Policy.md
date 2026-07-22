# Repository Quality Gate Policy

**Status date:** 2026-07-16
**Policy owner:** Codex coordination/review mode
**Implementation owner:** Codex engineering mode
**Tracking issue:** #155
**Ownership record:** `unity/Docs/Ownership_Decision_Record.md`

This record defines required automated checks, path-aware checks, manual dispositions, and branch protection for the single Codex agent responsibility model. It changes no runtime, narrative, design, save, Android, Unity, or build behavior. `AGENTS.md` is authoritative.

## 1. Decisions

1. Repository classification, hygiene, and Android unit/debug validation are the first required automated merge gates.
2. Unity unavailable/licensing failures are blocked validation, never passing evidence.
3. A locked-down self-hosted Windows runner remains the target Unity model after #127 and #150 are proven.
4. Path classification reports ownership mode but never replaces Codex coordination/review disposition.
5. Skipped, cancelled, unavailable, `continue-on-error`, or `not applicable` checks cannot be cited as product validation.
6. One GitHub identity currently represents the user and agents, so documented dispositions are evidence but not cryptographically independent approvals.
7. Codex owns all coordination/review, narrative/content, terrestrial-design, and engineering project work through separately declared modes.
8. Ownership-sensitive changes must read `Ownership_Decision_Record.md`; an earlier instruction cannot override a later user instruction.
9. PRs must declare performance, memory, package-size, install-size, dependency, and device-compatibility impact when applicable.

## 2. Stable checks

### Required on every PR

- `policy / classify`
- `repository / hygiene`
- `android / unit-debug`

### Required when applicable

- `android / release`
- `contracts / validate`
- `unity / compile`
- `unity / editmode`
- `unity / playmode`
- `unity / player-build`

A `not applicable` result proves only applicability, not product validation.

### Informational reports

- primary owner mode;
- narrative/content source changes;
- terrestrial-design source changes;
- runtime/asset changes;
- designated shared and save-sensitive files;
- workflow/dependency changes;
- generated/catalog drift;
- performance, memory, build-size, install-size, dependency, and device-compatibility impact;
- manual Unity evidence state.

## 3. `policy / classify`

Classification uses the PR event, base/head refs, body declarations, changed-file list, and ownership record. It blocks readiness when:

- base is not `main` and no approved prerequisite branch is linked;
- the base is closed, rejected, or unapproved;
- no issue/upstream artifact is linked and the PR is not a root coordination change;
- exactly one primary mode is not selected from Codex coordination/review, Codex narrative/content, Codex terrestrial-design, or Codex engineering;
- required impact declarations are missing;
- a shared file is changed but not declared, or another open PR holds its lock;
- a test-only PR changes production Build Settings;
- narrative or terrestrial-design source is mixed with engineering without explicit Codex coordination/review specification and justification;
- engineering changes player-facing narrative or terrestrial visual intent without approved source-mode input;
- issue completion is claimed without `Fixes #...` or an explicit completion link;
- performance, memory, asset/package-size, install-size, dependency, or device impact is applicable but undeclared;
- a draft PR is represented as merge-ready;
- the head is not current for final merge;
- an ownership change contradicts the latest dated user instruction in `Ownership_Decision_Record.md`.

Codex should implement machine-readable policy at:

```text
.github/anotherlife-policy.yml
```

It should define branch prefixes and mode path groups; designated shared and save-sensitive files; narrative/content and terrestrial-design source paths; runtime, scene, contract, catalog, workflow, and dependency paths; forbidden tracked artifacts; and stable status names.

The policy file is technical configuration. It does not authorize engineering mode to rewrite source owned by another Codex mode.

## 4. `repository / hygiene`

The job runs read-only with enough history for the PR range. Required failures include:

- `git diff --check` failure;
- tracked Unity `Library/`, `Temp/`, `Logs/`, `obj/`, build outputs, caches, crash dumps, or machine-specific artifacts;
- tracked credentials, licenses, signing material, or sensitive profile paths;
- duplicate GUIDs among tracked `.meta` files except explicit test fixtures;
- missing enabled Build Settings scene path;
- `Assets/Test.unity` enabled in production Build Settings;
- duplicate enabled scene names;
- malformed deterministic JSON or schemas;
- failed catalog/schema validators;
- canonical workspace changed to a duplicate active checkout;
- unpinned third-party workflow actions without an approved exception;
- overbroad workflow token permissions;
- unsafe `pull_request_target` execution of untrusted code.

Required reports include every changed shared, save-sensitive, narrative, terrestrial-design, asset/runtime, workflow/dependency, scene, generated, schema, and catalog path.

## 5. Android checks

### `android / unit-debug`

Runs on every PR:

```text
./gradlew :app:testDebugUnitTest :app:assembleDebug --no-daemon
```

Use a supported pinned JDK and committed wrapper. Dependency/network/cache failures fail. Retain unit reports, debug artifact, logs, and version information. Do not use `continue-on-error`.

### `android / release`

Required for Android runtime/UI, manifest, Gradle, packaging, resources, release/debug gating, Android↔Unity packaging, or packaged shared-data changes.

```text
./gradlew :app:assembleRelease --no-daemon
```

Unavailable signing is an explicit blocker or uses a reviewed unsigned validation variant; it never silently downgrades to debug.

## 6. Contract and catalog checks

`contracts / validate` applies to shared contracts, schemas, StreamingAssets catalogs, generated consumers, and source-mode exports. Validate syntax, IDs, versions, duplicate/blank IDs, internal references, deterministic generation, Fable compatibility, Android/Unity consumer compatibility, provenance/hash where supported, and source-to-generated mapping. Parser success alone is not semantic acceptance.

## 7. Unity validation model

### Interim manual evidence

Until runner stability is proven, Unity-sensitive PRs require retained evidence from:

```text
C:\Users\MY\Documents\AnotherLife\unity
Unity 2022.3.62f3
```

Record base/head SHA, exact version and commands, exit codes, complete logs, compiler-error scan, EditMode/PlayMode XML and totals when applicable, Player output and launch evidence when applicable, import/missing-script diagnostics for asset work, final repository status, and every blocked check.

Exit 199, licensing IPC failure, missing XML, unavailable runner, duplicate-workspace execution, or skipped suite is blocked validation.

### Target self-hosted runner

Require a dedicated non-admin account, locked Unity version, isolated workspace/temp/profile, one job at a time, least-privilege secrets, no committed/printed license material, clean workspace checks, and failure artifacts.

`unity / compile` and `unity / editmode` become protected only after one passing and one intentionally failing proof PR, retained artifacts, documented licensing/restart behavior, and proof no developer profile is used. `unity / playmode` additionally requires #127. `unity / player-build` additionally requires #150.

## 8. Path-aware Unity matrix

| Changed area | Compile | EditMode | PlayMode | Player build |
| --- | --- | --- | --- | --- |
| Unity runtime/interface/data | required | required | when startup/integration changes | when packaged flow changes |
| Editor/tooling/tests only | required | required | only for PlayMode infrastructure | when build tooling changes |
| Scenes/prefabs/project settings | required | required validators | required | required |
| Save/service boot/integration | required | required | required after #127 | required after #150 when packaged |
| Contracts/catalogs/assets consumed by Unity | required | required validation | when runtime load changes | when packaging changes |
| Documentation only | not applicable | not applicable | not applicable | not applicable |

## 9. Manual dispositions

### Codex coordination/review disposition

Required for shared files, save/migration/recovery, contracts/state/integration, issue completion beyond compilation, stacked/dependency-sensitive PRs, workflow/protection changes, ownership changes, and mixed-mode integrations.

```text
BLOCKED
READY FOR SOURCE-MODE REVIEW
READY TO MERGE
```

The comment identifies the reviewed head SHA, unresolved validation, and lock state.

### Codex narrative/content fidelity

Required when narrative meaning, IDs, dialogue, lore, chapter placement, consequences, localization-facing copy, or generated narrative consumers change.

```text
PASS
CHANGES REQUIRED
NOT APPLICABLE
```

### Codex terrestrial-design fidelity

Required when terrestrial concepts, silhouettes, anatomy, scale, palettes, materials, motion intent, design-source assets, or generated visual consumers change.

```text
PASS
CHANGES REQUIRED
NOT APPLICABLE
```

### User approval

Required for unrecorded creative/product/design decisions, U1, release-candidate acceptance, intended player-experience/balance changes, irreversible profile behavior, and ownership changes.

## 10. Branch protection

After Phase A proof PRs:

- require PRs for all changes;
- require `policy / classify`, `repository / hygiene`, and `android / unit-debug`;
- require applicable checks;
- require current base or a proven merge queue;
- require conversation resolution;
- block direct pushes, force pushes, and branch deletion;
- include administrators;
- prefer squash merge;
- disable auto-merge for shared, save-sensitive, source-mode, workflow, dependency, scene, ownership, and release-sensitive changes.

Because all work currently uses one GitHub identity, do not claim independent approval enforcement. Use automated checks plus documented dispositions until a separate trusted identity/App exists.

## 11. Emergency override

Only for broken `main`, imminent data/security loss, or a release incident where delay is more dangerous than the skipped gate. Create an incident issue with affected SHA, symptom, skipped check, narrow scope, rollback, owner, required validation, and source/save/release impact. No feature or cleanup scope joins the override.

## 12. Workflow security

- default token read-only;
- write only for narrowly reviewed jobs;
- third-party actions pinned to immutable SHAs;
- untrusted PRs receive no secrets;
- no unsafe `pull_request_target` checkout/execution;
- no credentials, keystores, licenses, or profiles in cache/artifacts;
- logs redact secrets;
- untrusted jobs cannot mutate branch protection or repository settings.

## 13. Proof PRs

Codex must demonstrate:

1. passing current-main repository/Android PR;
2. intentional Android unit failure;
3. duplicate `.meta` GUID failure;
4. test scene enabled in Build Settings failure;
5. missing enabled scene failure;
6. undeclared shared-file failure;
7. invalid/stacked base failure;
8. artifact upload on failure;
9. documentation-only correct classification;
10. Android release-sensitive invocation;
11. narrative/content source classification;
12. terrestrial-design source classification;
13. unauthorized engineering rewrite of source-mode data failure;
14. stale ownership instruction overriding a newer decision failure.

Failure fixtures close unmerged.

## 14. Implementation order

1. Merge this ownership-aligned policy.
2. Codex engineering adds machine-readable policy, Phase A workflow, and deterministic validators on `codex/repository-quality-gates`.
3. Run proof PRs and attach run/artifact evidence to #155.
4. Codex coordination/review mode reviews false positives, false negatives, and stable names.
5. Configure and verify Phase A protection.
6. Add retained manual Unity evidence.
7. Complete #127 and #150.
8. Prove and then require the Unity runner through a separate reviewed change.

No gameplay, narrative, terrestrial design, save, economy, combat, scene content, or broad dependency change is authorized by this policy.
