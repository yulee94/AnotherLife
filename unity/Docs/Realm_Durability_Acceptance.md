# Realm Durability Acceptance

**Issue:** [#173 — Enforce one durable realm per profile](https://github.com/yulee94/AnotherLife/issues/173)
**Primary delivery mode:** Codex engineering
**Validated:** 2026-08-17 with Unity 2022.3.62f3 on macOS 26.6 arm64

## Scope

This suite exercises the integrated profile-bound realm authority rather than an isolated model. It verifies both the published `CurrentSave` state and the durable `save.json` realm receipt where the production filesystem is available.

The Player test uses the production filesystem implementation in an isolated `realm-durability-acceptance` directory below `Application.persistentDataPath`; it never deletes or replaces the developer's normal save files.

The `RealmDurabilityAcceptance` category covers:

- clean identity-aware profile creation and first realm commit;
- failures before the staged write, after the staged write, and after an uncertain replace/rollback boundary;
- reload after interrupted and malformed writes;
- exact legacy schema-1 migration while preserving chapter and resources;
- staged non-authoritative legacy metadata;
- stale `save.lock` evidence;
- duplicate transaction replay and duplicate-event suppression;
- subscriber failure with a durable pending event;
- concurrent same-transaction and different-realm requests;
- independent profile roots and profile switching;
- repeated process-style service reloads; and
- an actual macOS Player build that creates, commits, reloads, replays, rejects a conflicting realm, and reads the on-disk receipt.

## Reproducible commands

Run from the repository root. The result paths are intentionally outside `unity/Assets` so Unity does not import generated evidence.

### EditMode fault and recovery suite

```bash
mkdir -p TestResults
"/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "$PWD/unity" \
  -runTests -testPlatform EditMode \
  -testCategory RealmDurabilityAcceptance \
  -testResults "$PWD/TestResults/realm-durability-editmode.xml" \
  -logFile "$PWD/TestResults/realm-durability-editmode.log"
```

Expected: 16 tests pass. The XML must report `result="Passed"`, `passed="16"`, and `failed="0"`.

### Editor PlayMode production-path test

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "$PWD/unity" \
  -runTests -testPlatform PlayMode \
  -testCategory RealmDurabilityAcceptance \
  -testResults "$PWD/TestResults/realm-durability-playmode.xml" \
  -logFile "$PWD/TestResults/realm-durability-playmode.log"
```

Expected: one test passes and verifies the primary save receipt after commit, reload, duplicate replay, and a conflicting realm request.

### Built macOS Player

Build the Player-with-tests:

```bash
rm -rf "$PWD/TestResults/RealmDurabilityBuiltPlayer"
"/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "$PWD/unity" \
  -runTests -testPlatform StandaloneOSX -buildTarget StandaloneOSX \
  -testCategory RealmDurabilityAcceptance \
  -buildPlayerPath "$PWD/TestResults/RealmDurabilityBuiltPlayer" \
  -testResults "$PWD/TestResults/realm-durability-player.xml" \
  -logFile "$PWD/TestResults/realm-durability-player-build.log" \
  -playerHeartbeatTimeout 60
```

Unity 2022.3.62f3 can build the Player successfully but time out waiting for its remote test-controller heartbeat in headless macOS sessions. When that happens, run the produced Player directly:

```bash
"$PWD/TestResults/RealmDurabilityBuiltPlayer/PlayerWithTests/PlayerWithTests.app/Contents/MacOS/AnotherLifeUnity" \
  -batchmode -nographics \
  -logFile "$PWD/TestResults/realm-durability-player-manual.log"
```

Expected: exit code `0` and the log marker:

```text
AL-REALM-DURABILITY-PLAYER-ACCEPTANCE-PASSED
```

The marker is emitted only after the Player has verified that:

1. a clean production save is identity-aware and writable;
2. one Stonehold transaction is durably committed and emits one committed event;
3. `save.json` contains the same profile, realm, transaction, and event IDs;
4. a new service instance reloads that exact durable authority;
5. replay returns the original receipt without another event;
6. an Umbral request is rejected without mutation; and
7. no staged or previous transaction artifact remains.

## Retained result

The 2026-08-17 acceptance run produced:

- EditMode: **16 passed, 0 failed**;
- Editor PlayMode: **1 passed, 0 failed**;
- StandaloneOSX build: **Build Finished, Result: Success**;
- direct built Player: **exit 0** with `AL-REALM-DURABILITY-PLAYER-ACCEPTANCE-PASSED`.

The standalone Unity test controller timed out waiting for the headless Player heartbeat and therefore did not write Player XML. This is a runner transport limitation, not a test failure; the exact built artifact was launched directly and completed the same embedded test with its explicit pass marker.

## Impact

The acceptance additions allocate only inside tests and add no runtime asset weight. The default-save correction adds one profile ID string at clean-profile creation. The runtime catalog relocation removes a duplicate StreamingAssets destination that prevented Player builds; it does not duplicate catalog bytes. No new package, scene, shader, texture, or runtime loop is introduced, so expected gameplay performance, steady-state memory, install size, and device compatibility are unchanged.

## Approval state

This evidence proves the engineering invariant on the tested Editor and macOS Player paths. It does not replace the user's integrated playtest, product approval, milestone approval, or release approval.
