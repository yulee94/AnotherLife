# Representative PlayMode Profile-Isolation Specification

**Status date:** 2026-07-15  
**Tracking issue:** #127  
**Specification owner:** GPT  
**Implementation owner:** Codex engineering mode  
**Validated Unity target:** `2022.3.62f3`  
**Representative scene:** `Assets/Test.unity`  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

## 1. Goal

Create a committed PlayMode smoke suite that loads the representative Unity scene, observes startup through stable structural signals, fails on unexpected Error/Assert/Exception logs, and restores the developer’s local profile and global test state exactly even when a test assertion fails.

The suite is a reusable validation foundation for Bootloader, save, scene, integration, and later NVS-01 PRs. It must not change production save behavior merely to make the test pass.

## 2. Verified current state

Current `RepresentativeSceneSmokeTests.cs`:

- loads `Assets/Test.unity` through `EditorSceneManager.LoadSceneAsyncInPlayMode`;
- has no bounded load timeout;
- waits 0.5 scaled seconds;
- changes `LogAssert.ignoreFailingMessages` without restoring it;
- calls `LogAssert.NoUnexpectedReceived()`, which treats ordinary startup/offline-progress logs as failures;
- provides no save-file snapshot, isolation, restoration, or artifact verification;
- provides no `ServiceLocator` cleanup;
- provides no scene/global teardown guarantee.

The scene initializes the normal offline stack. `LocalSaveGameService` uses `Application.persistentDataPath`, applies offline progress during load, and saves again. A PlayMode run can therefore consume, rotate, quarantine, replace, or delete a developer profile unless the test isolates it externally.

Current known save-file patterns are:

```text
save.json
save.backup.json
save.tmp.json
save.previous.json          # approved/current model
save.json.previous          # legacy fallback currently produced by code
save.json.corrupt-*
save.backup.json.corrupt-*
```

The test must protect both current and legacy patterns until #137 removes the legacy path safely.

## 3. Scope

Expected implementation scope:

```text
unity/Assets/AL/Tests/PlayMode/RepresentativeSceneSmokeTests.cs
unity/Assets/AL/Tests/PlayMode/ProfileIsolation/** or one focused test helper
focused EditMode tests for the pure snapshot/file matcher when useful
matching .meta files
```

The existing PlayMode asmdef already has `UNITY_INCLUDE_TESTS`, `TestAssemblies`, and `autoReferenced: false`; retain those properties.

Prohibited by default:

- production save implementation or interfaces;
- `SaveGameData.cs`;
- `Bootloader.cs` while PR #203 holds its lock;
- scenes, prefabs, or production Build Settings;
- Android source;
- narrative/content or terrestrial-design source;
- package/dependency upgrades;
- public production reset/clear APIs added only for tests.

## 4. Authoritative artifact matching

Use one centralized matcher for local profile artifacts. It accepts only files directly under `Application.persistentDataPath` whose file names match:

```text
save.json
save.backup.json
save.tmp.json
save.previous.json
save.json.previous
save.json.corrupt-*
save.backup.json.corrupt-*
```

Rules:

- match by file name, not arbitrary substring or recursive directory scan;
- use ordinal, platform-appropriate case behavior consistently and test it;
- do not include unrelated files in the persistent directory;
- do not treat quarantine files as active saves, but preserve them exactly;
- the matcher is the single source used by snapshot, removal, cleanup, verification, and tests;
- future save-pattern additions require updating this matcher and its test matrix.

## 5. External snapshot model

Before loading the representative scene:

1. Resolve and normalize `Application.persistentDataPath`.
2. Create a unique external snapshot directory under `Path.GetTempPath()`, not beneath the persistent profile or Unity project.
3. Record every matching original artifact with:
   - exact file name;
   - full original path;
   - byte length;
   - content hash such as SHA-256;
   - exact bytes or a copied snapshot file;
   - file attributes;
   - UTC last-write timestamp.
4. Copy every artifact to the snapshot directory using a collision-safe one-to-one name.
5. Reopen every snapshot copy and verify its hash before touching the original.
6. Record the exact original file-name set.
7. Write the external recovery directory path to the test log before original files are removed.

If snapshot creation, copy, close, hash verification, or enumeration fails:

- fail before loading the scene;
- do not delete or alter any original artifact;
- retain the snapshot directory when it may help manual recovery;
- report the exact original and snapshot paths without exposing them in player-facing production UI.

## 6. Isolation before startup

After the external snapshot is verified:

1. Delete every matching artifact from the persistent directory.
2. Re-enumerate and require zero matching artifacts.
3. Clear pre-existing static `ServiceLocator` state through a narrow test-only reflection helper consistent with current test practice.
4. Assert the service registry is empty before scene load.
5. Capture global test/runtime state that the suite may alter:
   - `LogAssert.ignoreFailingMessages`;
   - `Time.timeScale`;
   - active scene identity;
   - registered log callback state owned by the helper.

If any artifact cannot be removed:

- restore any already removed original files from the verified snapshot;
- verify restoration;
- fail before scene load;
- never continue with a partially isolated profile.

Do not call production `DeleteSave()` for isolation because it is incomplete and intentionally belongs to #137.

## 7. Scene loading and timeout

Editor PlayMode loads:

```csharp
EditorSceneManager.LoadSceneAsyncInPlayMode(
    "Assets/Test.unity",
    new LoadSceneParameters(LoadSceneMode.Single));
```

Requirements:

- the load call must not throw and the returned operation must be non-null;
- use realtime rather than scaled game time for timeout accounting;
- maximum load time: 15 seconds unless the implementation PR justifies another bounded value;
- each frame checks elapsed realtime and fails with a stable timeout diagnostic;
- do not use an unbounded `while (!load.isDone)` loop;
- after completion require:

```text
SceneManager.GetActiveScene().path == Assets/Test.unity
```

`Assets/Test.unity` remains excluded from normal production Build Settings. The test must load it by asset path rather than adding it to the Player scene list.

## 8. Structural startup signals

Do not assert every ordinary log line. The smoke should prove stable structure instead.

Minimum startup signals:

1. Active scene path is exactly `Assets/Test.unity`.
2. `GameObject.Find("Demo_Manager")` returns an active scene object.
3. The current service stack exposes at least:
   - `ISaveGameService`;
   - `IResourceService`;
   through a non-throwing/reflection probe compatible with current `ServiceLocator` and PR #203’s future marker design.
4. When a completion marker exists after #203, the smoke may additionally assert marker-consistent readiness, but it must not hard-depend on an unmerged branch.
5. Observe at least five rendered/update frames and 0.5 seconds of realtime after readiness, with a bounded total observation time.

Do not depend on:

- exact welcome/cosmetic text;
- exact ordinary log ordering;
- random loot;
- frame rate;
- animation timing;
- current resource totals;
- a pre-existing primary save;
- Crownlands fallback or other uncommitted gameplay defaults.

## 9. Log policy

Unexpected `LogType.Error`, `LogType.Assert`, and `LogType.Exception` fail the test.

Requirements:

- capture the original `LogAssert.ignoreFailingMessages` value;
- set it to `false` only for the test and always restore it;
- do **not** call `LogAssert.NoUnexpectedReceived()` as a blanket assertion over ordinary `Log` messages;
- ordinary startup and warning logs are allowed unless a test explicitly declares one unacceptable;
- optionally register one test-owned `Application.logMessageReceived` callback to collect Error/Assert/Exception entries with condition/stack;
- unregister exactly that callback in cleanup;
- known errors may be expected only by the test that intentionally triggers them and must use exact/stable matching rather than broad regex suppression;
- failure output lists each unexpected severe log once.

The smoke must not be changed merely to whitelist whatever logs the current scene happens to produce.

## 10. Cleanup order

Cleanup is required in both:

- a `try/finally` around the scene smoke; and
- an idempotent `[UnityTearDown]` fallback.

Recommended order:

1. Stop test-owned callbacks/coroutines where possible.
2. Disable or destroy root GameObjects in the representative scene so `OnApplicationPause`, `OnDisable`, `OnDestroy`, or late updates cannot write after restoration.
3. Load or create a temporary empty test scene in Single mode and wait with a bounded realtime timeout.
4. Clear `ServiceLocator` through the test-only helper and verify empty state.
5. Delete every matching artifact created by the test from the persistent directory.
6. Restore every original artifact from the verified external snapshot.
7. Restore attributes and last-write UTC timestamp where supported.
8. Reopen and hash every restored artifact.
9. Verify the final matching file-name set exactly equals the original set and every hash/length matches.
10. Restore `Time.timeScale`, `LogAssert.ignoreFailingMessages`, and other captured global state.
11. Delete the external snapshot directory only after all restoration verification succeeds.

If restoration verification fails:

- retain the external snapshot directory;
- fail with a recovery-oriented message containing its path;
- do not mask the original test failure, but report both the test and restoration failures.

Cleanup must be idempotent: a second invocation after partial or complete cleanup must not corrupt restored files.

## 11. Hard-crash limitation

`finally` and `UnityTearDown` cannot run after an editor/process/OS crash or forced termination.

The PR must state this limitation explicitly. Validation should use a disposable OS/test profile or isolated CI account where available. The external snapshot path is logged before deletion so a surviving snapshot can be restored manually after a crash.

Do not claim absolute crash-proof protection from test teardown alone.

## 12. Build Settings immutability

At test or validation setup, snapshot the bytes/hash of:

```text
unity/ProjectSettings/EditorBuildSettings.asset
```

After the suite:

- require byte-for-byte equality;
- require `Assets/Test.unity` remains absent from enabled production scenes;
- report any editor mutation as failure.

The implementation PR must not contain a diff to `EditorBuildSettings.asset`.

## 13. Required helper tests

### Artifact matcher

- every exact active/temp/previous name matches;
- current and legacy previous paths match;
- primary and backup quarantine prefixes match;
- unrelated files, nested files, similar prefixes, and directories do not match.

### Snapshot/restore

Using a temporary fake persistent root, not the developer profile:

- no original files;
- each pattern individually;
- all patterns together;
- zero-length and binary/non-UTF8 bytes;
- file attributes/timestamps where supported;
- test-created extra artifacts removed;
- exact bytes and final set restored;
- repeated cleanup is safe;
- copy/hash/delete/restore failure surfaces the recovery path and preserves available evidence.

### PlayMode lifecycle

- successful representative load/startup/observation;
- bounded scene-load timeout through a test seam;
- forced assertion after scene load still restores files and globals;
- severe log fails and cleanup still runs;
- normal logs do not fail;
- `ServiceLocator` empty before and after;
- a second smoke in the same runner does not inherit services or profile artifacts;
- `Time.timeScale` and log state restore;
- Build Settings bytes remain unchanged.

Failure-path tests must not deliberately damage the real profile. Use injectable/fake roots and deterministic seams for pure file-operation faults.

## 14. Interaction with open PRs

### PR #203 / #153 Bootloader

- #203 holds the `Bootloader.cs` soft lock; #127 must not edit it.
- The PlayMode suite must tolerate the current stack and the future marker through a narrow probe.
- #203 may report PlayMode blocked until #127 merges; it may not weaken #127 or use duplicate-workspace/profile-noise evidence as a pass.

### #137 save hardening

- #127 protects all current and approved future artifact names until #137 standardizes them.
- #127 does not implement save candidate selection, backup rotation, recovery, or deletion.
- When #137 changes file patterns, it must update the centralized matcher/tests in a focused dependent PR.

### #150 production scenes

- the representative scene remains test-only;
- #150 separately defines production Player scenes and launch smoke.

## 15. Required validation

Run from the canonical workspace:

```powershell
$repo = "D:\260711\MY\AndroidStudioProjects\AnotherLife"
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"

& $unity -batchmode -quit -nographics `
  -projectPath "$repo\unity" `
  -logFile "$repo\unity\Logs\PlayModeIsolationCompile.log"

& $unity -batchmode -nographics `
  -projectPath "$repo\unity" `
  -runTests -testPlatform EditMode `
  -testResults "$repo\unity\Logs\PlayModeIsolationEditMode.xml" `
  -logFile "$repo\unity\Logs\PlayModeIsolationEditMode.log"

& $unity -batchmode -nographics `
  -projectPath "$repo\unity" `
  -runTests -testPlatform PlayMode -assemblyNames AL.PlayMode.Tests `
  -testResults "$repo\unity\Logs\PlayModeIsolationPlayMode.xml" `
  -logFile "$repo\unity\Logs\PlayModeIsolationPlayMode.log"
```

Do not add `-quit` to Unity Test Framework `1.1.33` test commands when the local runner reproduces its known exit limitation; record the exact command that completes.

Required report:

- base/head SHA;
- Unity version and exact commands;
- compile exit/final marker and compiler-error scan;
- EditMode and PlayMode discovered/passed/failed/skipped totals;
- representative test name/result;
- original/final profile artifact name set and hashes, without publishing private bytes;
- snapshot directory cleanup result;
- severe-log count;
- `ServiceLocator` before/after count;
- global-state restoration;
- Build Settings before/after hash;
- final `git status -sb` and diff;
- anything that could not run.

## 16. Branch and PR

```text
codex/unity-playmode-representative-smoke
```

Primary mode: Codex engineering.

One focused PR linked with:

```text
Fixes #127
Refs #153
Refs #137
Refs #150
```

No designated shared-file lock is expected.

## 17. Acceptance criteria

- [ ] PlayMode runner discovers at least one committed test.
- [ ] Representative scene loads by asset path with a realtime timeout.
- [ ] Stable structural startup signals pass.
- [ ] Unexpected Error/Assert/Exception logs fail without rejecting ordinary startup logs.
- [ ] Developer profile artifacts are snapshotted externally before mutation.
- [ ] Test runs with zero matching active artifacts.
- [ ] Every original artifact is restored byte-for-byte with exact final set.
- [ ] Test-created temp/previous/quarantine artifacts are removed.
- [ ] Cleanup is idempotent and runs in `finally` plus `UnityTearDown`.
- [ ] Restoration failure retains the recovery snapshot and reports its path.
- [ ] `ServiceLocator` and test-owned callbacks are clean before and after.
- [ ] `Time.timeScale`, log state, and other globals are restored.
- [ ] `EditorBuildSettings.asset` is byte-for-byte unchanged and `Assets/Test.unity` remains production-excluded.
- [ ] Failure-path helper tests use fake roots and never intentionally corrupt the developer profile.
- [ ] Unity compile, EditMode, and PlayMode evidence passes in the canonical workspace.
- [ ] No runtime, save-domain, narrative, terrestrial-design, Android, scene, Build Settings, dependency, or shared-file change is included.

# GPT handoff to Codex

```text
Codex engineering: implement issue #127 from current main using unity/Docs/PlayMode_Profile_Isolation_Spec.md. Keep the PR test-only. Snapshot and verify every current/legacy save artifact outside persistentDataPath before scene load, remove active artifacts, load Assets/Test.unity with realtime bounds, assert structural startup, fail only on severe logs, and restore the exact profile/file/global/service state in finally plus UnityTearDown. Do not edit Bootloader.cs while PR #203 holds its lock and do not change production save behavior. Run compile/EditMode/PlayMode in D:\260711\MY\AndroidStudioProjects\AnotherLife\unity and return exact XML/log/hash evidence for GPT review.
```