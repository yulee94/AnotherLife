# Reproducible Build Foundation

## Scope and platform decision

The current supported target is PC Windows under the canonical Unity editor:

- Unity `6000.3.22f1` (`1c726e1fb402`)
- Windows Standalone support
- Mono scripting backend
- x86_64
- Development Player with Unity's `NoUniqueIdentifier` build option
- five ordered ShellFoundation scenes from `EditorBuildSettings.asset`

The owner moved Android implementation and physical-device certification to deferred task
`t_7b530af7`. Android Build Support is not installed for the canonical editor on this machine,
and this card does not claim mobile readiness. The prior `2022.3.62f3` exporter investigation
remains recorded as incompatible: opening the Unity 6 project in that editor is not authorized.
When mobile is reactivated, export must use the same canonical Unity editor with an explicitly
pinned target API.

Release criteria remain owned by `t_4a5b066c`; capacity/SLO criteria remain owned by
`t_7f6be100`. This foundation references those authorities without copying their numeric gates.

## Recon coverage

The initial bounded build corpus contained 22 files and 11,286 lines across the Unity
exporters/builders/launch-smoke evaluator/tests, Android packaging helper/tests, package and
build settings, Gradle/Android configuration, CI, and build-health specifications. 10,289 lines
were read in full (91.16%). `ProjectSettings.asset` (950 lines) and `EditorSettings.asset`
(47 lines) were inspected through targeted field searches. The resumed PC pass additionally
read the complete build runner, policy, test suite, build workflow, Player builder, and relevant
launch-smoke evaluator sections. Binary assets and unrelated gameplay files were excluded.

## Machine-readable policy

`tools/builds/reproducible_build_policy.json` is the committed authority. It contains:

- the exact project editor version and revision;
- the supported Windows build target, backend, architecture, options, execute method, output,
  and cleanup boundary;
- exact ordered Boot-to-RealmSelection launch evidence and failure tokens;
- the deferred Android owner/task, unavailable module state, rejected legacy exporter, and
  future same-editor/API/backend/architecture requirements;
- release and capacity authority references;
- exact artifact-tree equivalence policy.

## Build-input and manifest contract

The runner hashes every Git-tracked file under:

- `unity/Assets`
- `unity/Packages`
- `unity/ProjectSettings`

It also hashes the CI workflow, Gradle/Android files, package verifier, build policy, and build
runner. Modified or untracked configured inputs block a reproducibility build. Enabled scenes
and all canonical `Assets/AL/StreamingAssets/GameData` catalogs form the narrower content tree.

Every signed-ready build manifest records:

- source revision, source-tree digest, dirty state, and per-input path/size/SHA-256;
- actual project editor version/revision and serialized Player settings;
- Unity executable/version, package lock, Gradle wrapper/catalog versions, and the observed
  embedded Android SDK/NDK/JDK availability inventory;
- target settings, scripting backend, architecture, options, and ordered scene list;
- content paths, sizes, hashes, and content-tree digest;
- artifact paths, raw sizes/hashes/tree digest, reproducible sizes/hashes/tree digest,
  declared per-file normalization, and structural smoke result;
- run timestamps, host, log path, and clean-Library choice;
- release/capacity authority references.

`manifestSha256` is SHA-256 over canonical JSON with that member omitted. The adjacent
`.json.sha256` is a detached-signing input; it is not a claim that a signer has signed it.

## Commands

Run from a clean committed checkout.

Inventory only:

```powershell
python tools/reproducible_build.py --repo-root . inventory `
  --output unity/Logs/ReproducibleBuilds/inventory.json
```

Exact-editor preflight:

```powershell
python tools/reproducible_build.py --repo-root . preflight `
  --target windows64-development `
  --unity-exe 'C:\Users\MY\AppData\Local\Programs\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe'
```

Two clean Windows builds:

```powershell
python tools/reproducible_build.py --repo-root . build `
  --target windows64-development `
  --unity-exe 'C:\Users\MY\AppData\Local\Programs\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe' `
  --manifest unity/Logs/ReproducibleBuilds/windows64-a.json `
  --clean-library

python tools/reproducible_build.py --repo-root . build `
  --target windows64-development `
  --unity-exe 'C:\Users\MY\AppData\Local\Programs\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe' `
  --manifest unity/Logs/ReproducibleBuilds/windows64-b.json `
  --clean-library

python tools/reproducible_build.py --repo-root . compare `
  unity/Logs/ReproducibleBuilds/windows64-a.json `
  unity/Logs/ReproducibleBuilds/windows64-b.json `
  --output unity/Logs/ReproducibleBuilds/windows64-comparison.json
```

## Clean build and equivalence behavior

Before launching Unity, the runner:

1. requires the exact configured editor version;
2. rejects unsupported targets, including the deferred Android aliases;
3. requires all configured build inputs to be committed and clean;
4. removes only the guarded target output;
5. removes exactly `unity/Library` when `--clean-library` is supplied;
6. invokes the reviewed Unity execute method;
7. requires a successful BuildReport and structural artifact smoke;
8. writes the signed-ready manifest atomically.

Cleanup refuses repository root, outside-repository paths, symlinks, and reparse points.
Two builds are equivalent only when all reproducible fields match after excluding `run`,
`manifestSha256`, and the exact decimal `player-connection-guid` value Unity emits into a
development Player's `AnotherLifeUnity_Data/boot.config`. Manifests retain the raw size/hash/tree
evidence alongside the normalized digest; malformed or duplicate connection-guid lines fail closed.
Any other artifact, toolchain, setting, source, scene, or content divergence returns `stop_ship`
with exact field paths. No broad binary normalization is allowed.

The packaged golden-scene identity derives its timestamp from the source commit rather than wall
clock time. `NoUniqueIdentifier` prevents Unity from writing a fresh build GUID into each Player.
These are source-level determinism controls, not post-build binary normalization.

## Current-user launch smoke

The structural artifact smoke is not the launch smoke. Owner decision 2026-09-02 waived the
distinct-Windows-profile requirement for this PC-first card. Isolated-profile launch evidence is
NOT claimed and must not be inferred. The packaged Player smoke runs under the current
authenticated user, keeps dirty-tree/source/settings/scene/content/artifact/toolchain checks
fail-closed, and records `isolatedProfileClaimed: false`.

Boot no longer auto-routes. After the production Boot marker and `AL Boot Sequence Started...`,
the harness focuses the Player and sends keyboard Enter (the production Submit binding) until
RealmSelection appears or the timeout fires. The stale `No Realm Selected...` log is not emitted
by current Boot and is not required.

From the same authenticated user that owns the workspace, run:

```powershell
python tools/reproducible_build.py --repo-root . launch-smoke `
  --build-manifest unity/Logs/ReproducibleBuilds/windows64-b.json `
  --output unity/Logs/ReproducibleBuilds/windows64-launch.json
```

The harness verifies the manifest digest and Player executable hash, requires a new Player log,
launches the packaged artifact, and accepts only this exact ordered sequence:

1. production Boot scene marker;
2. Boot sequence started;
3. production RealmSelection scene marker after explicit Continue.

Failure tokens, wrong marker order, an unexpected scene, early exit, timeout, stale log, source or
artifact mismatch, and a false isolated-profile claim fail closed. After the transition, the harness
terminates the Player externally and makes no graceful-quit or save claim. Its signed-ready JSON
links the build-manifest digest, source revision/tree, artifact tree, observed current-user
boundary, Player-log digest, Continue attempts, and exact transition evidence. Android and
physical-device readiness remain deferred to `t_7b530af7`.

A current-user LocalLow leftover (`save.tmp.json` plus quarantine generations) is RecoveryRequired.
The harness temporarily overlays an empty product folder and restores the original files afterward;
user saves are not deleted. Isolated-profile launch evidence is still not claimed.

Observed packaged Windows Player smoke against `windows64-fix` (`35d165e4`, Unity
`6000.3.22f1`): Boot marker, `AL Boot Sequence Started...`, and the production RealmSelection
marker were observed in order after explicit Continue. First-generation create in the empty overlay
logged `AL-SAVE-CREATED-NEW`. Isolated-profile launch is still not claimed. User LocalLow leftovers
were restored after the overlay; Android/mobile remains deferred to `t_7b530af7`.
