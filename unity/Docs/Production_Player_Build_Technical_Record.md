# Production Player Build Technical Record

**Issue:** #150

**Profile:** `ShellFoundation`

**Implementation branch:** `codex/production-scene-build-settings`

**Branch base:** `82d7409bf472d91d2d5ac866b6c519380917bb68`

**Upstream main at implementation:** `3a22b33ecdd768441ae55f87c41632cce2979aff`

**Descriptor version:** `223.1`

**Unity version:** `2022.3.62f3`

**Status date:** 2026-07-22

## Technical scope

The committed normal Player profile contains exactly these enabled scenes:

1. `Assets/AL/Scenes/Boot.unity`
2. `Assets/AL/Scenes/RealmSelection.unity`
3. `Assets/AL/Scenes/Kingdom.unity`

`Assets/Test.unity` and `Assets/AL/Scenes/ChampionArena.unity` are absent. The production descriptor remains the only scene-list/order authority. The build entry validates the committed Build Settings and fails on drift; it does not silently rewrite them.

The batch entry is:

```text
AL.EditorTools.ProductionPlayerBuilder.BuildWindows64Development
```

It targets `StandaloneWindows64`, uses `BuildOptions.Development`, and writes only to the ignored path:

```text
unity/Builds/Validation/Windows64/AnotherLifeUnity.exe
```

## Automated evidence

| Validation | Result |
| --- | --- |
| Focused #150 EditMode tests | 19 passed, 0 failed |
| Production-scene EditMode tests | 64 passed, 0 failed |
| Complete EditMode suite | 305 passed, 0 failed |
| Complete corrected PlayMode suite | 26 passed, 0 failed |
| Build Settings/scene structural preflight | Passed |
| Output ignore check | Passed (`unity/.gitignore` ignores `/Builds/`) |

The automated coverage includes empty/missing/wrong-order/disabled/unexpected settings, Test and Champion exclusion, missing paths, duplicate paths/names, GUID mismatch, exact build options, preflight short-circuiting, stale-output removal, BuildReport/output classification, and ordered fresh-profile Player log validation.

## Windows build attempt

The reviewed batch entry was invoked on the available macOS Unity editor. It returned exit code `1` before `BuildPipeline.BuildPlayer` because the editor has only `MacStandaloneSupport`; `StandaloneWindows64` support is not installed.

The failure was typed and deterministic:

```text
status=PreflightFailed
target=StandaloneWindows64
unityVersion=2022.3.62f3
BuildResult=Unknown
errorCount=1
reason=StandaloneWindows64 build support is not installed for this Unity editor.
```

The exact validation output directory was cleaned first. The attempt retained ignored JSON/text failure summaries and produced no `.exe` or `_Data` directory, so stale output cannot be reported as a successful build.

## Remaining canonical evidence

Issue #150 is not technically closed until a Windows-capable Unity 2022.3.62f3 environment performs both of these steps:

1. Run the batch entry and retain the Unity log, summary JSON, complete BuildReport text, executable/data inventory, sizes, and hashes.
2. Launch the current executable under a disposable Windows account, Sandbox, VM, or isolated CI account and prove the ordered sequence `Boot marker -> Boot sequence -> fresh-profile transition -> RealmSelection marker`, with no severe log token or developer-profile access. External termination is allowed only after the RealmSelection marker and is not graceful quit/save evidence.

## Optimization and compatibility assessment

This change adds Editor-only validation/build code and tests. It adds no runtime component, asset, package, native library, or Player scene, so it has no runtime CPU or memory cost and no direct install-size increase. The initial compatibility target remains Windows 64-bit only; Android, macOS release packaging, branding, product identifiers, and quality settings are unchanged. Actual Player size and Windows launch compatibility remain unmeasured until the canonical build succeeds.

## Rollback

Revert the #150 implementation commit. Do not delete or regenerate the four production scene assets owned by #223, and do not substitute `Assets/Test.unity` as a Player entry after a build failure.
