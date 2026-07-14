# Phase 0 Build Health Status

**Status date:** 2026-07-14  
**Roadmap phase:** Phase 0 — Collaboration Baseline and Build Health  
**Snapshot reviewed:** `main` at merge commit `35fb8fe1e4d0b6315916af03d6e458372fcbcd15`

This is a coordination and evidence record. It does not replace `AGENTS.md`, author narrative, or implement runtime behavior.

## Executive status

Phase 0 is **red**. The collaboration baseline is merged, but current `main` is not Android-compilable because the two independent fixes for issue #111 were both merged and left duplicate `Quest` constructor properties.

The immediate execution order is:

1. Codex resolves issue #118 through PR #119 and proves Android unit tests plus debug assembly on latest `main`.
2. GPT reviews the fix for scope, constructor compatibility, validation evidence, and duplicate-work prevention.
3. Codex completes issue #117 by validating latest `main` in Unity 2022.3.62f3.
4. GPT records the Phase 0 gate decision.
5. Only after the gate is green does Android Studio begin the mergeable NVS-01 narrative packet.

## Verified repository state

- Canonical workspace documentation is merged and consistently uses `D:\260711\MY\AndroidStudioProjects\AnotherLife`.
- Root `AGENTS.md`, standalone role prompts, the staged roadmap, the NVS-01 plan, and the pull-request template are merged.
- No shared runtime integration file was declared by an open pull request at the start of this audit.
- Issue #111 was addressed by both PR #113 and PR #114.
- Both PR #113 and PR #114 were merged.
- Current `KingdomModels.kt` declares `mode` and `mapMarkerId` twice in the `Quest` primary constructor.
- PR #114 reported `:app:testDebugUnitTest` passing on its pre-merge branch, but that evidence does not prove the later combined `main` state because PR #113 and PR #114 were both merged.
- No current-main `:app:assembleDebug` result is recorded after the combined duplicate-field state.
- The Unity project declares editor version `2022.3.62f3`.
- No current-main Unity import, compilation, EditMode, PlayMode, or representative scene result is recorded after the latest merges.

## Active issues, pull requests, and ownership

### Issue #118 / PR #119 — Android duplicate Quest metadata blocker

**Classification:** Blocker  
**Owner:** Codex  
**GPT review status:** Changes requested by comment for constructor-order compatibility.

Required result:

- Keep exactly one `Quest.mode` and one `Quest.mapMarkerId`.
- Preserve existing positional constructor semantics by placing the new defaulted metadata after `isCompleted` and `isClaimed`.
- Retain the existing `QuestMode` enum and focused metadata tests.
- Run and report:

```powershell
$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
$env:Path = "$env:JAVA_HOME\bin;$env:Path"
.\gradlew.bat :app:testDebugUnitTest :app:assembleDebug
```

### Issue #116 — Duplicate coordination issue

**Classification:** Duplicate of issue #118  
**Owner:** GPT to close as duplicate after this status record is published.

Issue #116 captured the same Android duplicate-metadata root cause with stricter constructor-order acceptance criteria. The active implementation path is now PR #119 for issue #118, with GPT's constructor-order review comment carrying that compatibility requirement forward.

### Issue #117 — Phase 0 gate: current-main Unity validation

**Classification:** Required after #118 / PR #119  
**Owner:** Codex

Required result:

- Validate latest `main` after PR #119 merges using Unity 2022.3.62f3.
- Record import and C# compilation results.
- Run practical EditMode and PlayMode tests, or state the exact reason they could not run.
- Enter a representative scene in Play Mode, or create focused issues for each blocker.

## Phase 0 gate checklist

| Gate | Status | Evidence or owner |
| --- | --- | --- |
| Canonical workspace and agent rules merged | Pass | PR #112 and PR #115 merged |
| One active implementation path per issue | Caution | PR #119 is the active path for #118; #116 is being closed as duplicate |
| Android unit tests on latest `main` | Blocked | Issue #118 / PR #119 — Codex |
| Android debug assembly on latest `main` | Unverified | Issue #118 / PR #119 — Codex |
| Unity opens and compiles on latest `main` | Unverified | Issue #117 — Codex after #119 |
| Remaining blockers have explicit owners | Pass | #118/#119 and #117 assigned |
| No undeclared shared-file lock | Pass at audit time | No open PR declared a shared runtime file |

Phase 0 must not be marked green until the Android and Unity evidence is produced against the integrated `main` state.

## Duplicate-implementation incident rule

The #111 incident demonstrates that mergeability alone does not make two parallel fixes safe to merge together.

For all future issues:

1. The first open implementation PR becomes the active path unless the user explicitly requests alternatives.
2. A later PR for the same root problem must be marked as an alternative, compared against the active PR, and must not merge independently.
3. Before merging an alternative, GPT must choose one implementation or define a consolidation branch.
4. If both branches touched the same model, contract, constructor, save object, service registration, or dependency list, a combined-diff review is mandatory.
5. Validation from either isolated branch does not prove the combined `main` result.
6. After every merge sequence involving overlapping fixes, rerun validation on the integrated branch.

## Workstream constraints while Phase 0 is red

### GPT

- Triage issue #118, PR #119, and issue #117.
- Review PR #119 for constructor compatibility and validation evidence; do not create a parallel code fix.
- Close issue #116 as duplicate after this status record is aligned.
- Confirm exact integrated validation before closing #118 or #117.
- Do not prepare the NVS-01 runtime specification until Android Studio supplies an approved narrative packet and Phase 0 is green.

### Codex

- Work only on PR #119 / issue #118 until it is merged and current-main Android validation passes.
- Then execute issue #117.
- Do not start NVS-01 runtime implementation while either Phase 0 gate item is unresolved.

### Android Studio narrative workflow

- May inspect existing narrative IDs, references, and candidate quest-line scope on an isolated branch.
- Must not merge content that depends on unresolved runtime behavior.
- Must not modify `KingdomModels.kt`, Android build dependencies, Unity bootstrapping, save infrastructure, or runtime services to bypass Phase 0 failures.
- After Phase 0 is green, select exactly one user-approved bounded quest line for the A1 narrative packet.

### User

- Approve the first NVS-01 quest-line selection after Android Studio presents the bounded packet scope.
- Do not merge two technical PRs for the same issue without a GPT consolidation decision.

## Review checklist for PR #119

GPT should clear the blocker only when all are true:

- The branch started from current `main` containing both earlier merges.
- Only the duplicate declarations and directly related tests changed.
- `QuestMode` remains available to `QuestScreen`.
- The retained constructor order preserves the original Boolean parameter positions.
- Existing quest IDs, titles, descriptions, targets, outcomes, and navigation behavior are unchanged.
- `QuestModelTest` passes.
- `:app:testDebugUnitTest` passes.
- `:app:assembleDebug` passes.
- Exact command output or an unambiguous result summary is in the PR.
- No shared runtime integration file is touched.
- No second open PR targets the same implementation path.

## Phase transition rule

GPT may declare Phase 0 green only after:

- PR #119 is merged and issue #118 is closed with current-main Android test and assembly evidence.
- Issue #117 is closed with current-main Unity evidence, or every discovered Unity failure has a focused, owned blocker issue and the user explicitly accepts proceeding under that exception.
- No duplicate implementation PR remains open.
- No shared-file lock remains undeclared.

The next unblocked Phase 1 task is then:

```text
A1 — Android Studio selects and completes one bounded, user-approved NVS-01 narrative packet on android-studio/nvs-01-narrative-packet.
```
