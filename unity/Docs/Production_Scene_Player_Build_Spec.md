# Production Scene, Build Settings, and Player Launch-Smoke Specification

> Historical baseline notice: scene-count and Build Settings applicability statements in this
> 2026-07-15 specification are superseded by owner-approved `DEC-SCENE-DELIVERY-001`, its
> non-shipping inventory amendment `DEC-SCENE-DELIVERY-002`, and the
> current `ProductionSceneDescriptor`. The authoritative direct order is now Boot,
> RealmSelection, CharacterCreation, ChampionArena, Kingdom; the 78 catalog-generated scenes are
> local Addressables. See `Architecture/Deterministic_Scene_Content_Delivery_Decision.md`.

**Status date:** 2026-07-15  
**Tracking issue:** #150  
**Scene-authoring prerequisite:** #223  
**Specification owner:** GPT  
**Implementation owner:** Codex engineering mode  
**Audited baseline:** `0cbd5ec91a6513167ad8a5922e5f5d7acd742c2f`  
**Validated Unity target:** `2022.3.62f3`  
**Canonical workspace:** `C:\Users\MY\Documents\AnotherLife\unity`
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

## 1. Goal

Establish the first deterministic, source-controlled, packageable Unity Player shell for AnotherLife without shipping the representative test scene, promoting unsafe prototype gameplay, touching a real developer profile, or treating a successful Editor scene load as Player-build evidence.

This specification separates:

```text
production scene asset authority          #223
→ production Build Settings/profile       #150
→ development Player build                #150
→ isolated launch/first-transition smoke   #150
→ later Champion gameplay scene enablement #178/#180 follow-up
→ later Android Unity export/host          #135
→ final release acceptance                 user
```

The first #150 Player build is a **technical shell build**, not a release candidate and not proof that realm selection, Kingdom progression, Champion combat, saves, catalogs, narrative, or balance are product-complete.

## 2. Binding decisions

1. **`Assets/Test.unity` remains test-only.** It is never used as the Player entry point or enabled in normal Build Settings.
2. **The only committed scene in the audited repository is `Assets/Test.unity`.** The intended four-scene vertical slice exists only as editor-generator intent, not committed assets.
3. **Issue #223 owns scene asset creation and generator hardening.** #150 does not generate or edit scene content.
4. **The initial normal Player scene list is exactly three scenes:** Boot, RealmSelection, Kingdom.
5. **ChampionArena is a committed deferred candidate after #223, but is initially excluded from normal Build Settings.** It is not enabled until #178 removes all release-reachable prototype grants/unsafe entry paths and #180 accepts encounter/combat/result lifecycle.
6. **An asset's existence does not mean it ships.** Scene-authority inventory and Build Settings applicability are separate.
7. **Boot is build index 0 and the sole normal Player entry.** No direct Kingdom or Champion entry is configured in normal Build Settings.
8. **Every enabled scene path and name is exact, unique, committed, and validated.** String loading never resolves by an ambiguous duplicate name.
9. **Build Settings are maintained from one reviewed production descriptor.** Hard-coded lists in generator, validator, build script, and tests may not drift independently.
10. **The scene-authoring generator no longer owns normal Build Settings.** #150 applies the descriptor explicitly and deterministically.
11. **The Build Settings diff is exact.** No unrelated scene, test scene, development scene, or disabled stale entry is retained.
12. **The normal build uses the exact enabled Build Settings list.** Validation builds may not silently add hidden test scenes.
13. **A custom build script returns a typed `BuildReport`-derived result and fails the batch process on any non-success result.** A produced executable without a successful report is not acceptance evidence.
14. **The first supported validation target is `StandaloneWindows64`.** The canonical environment is Windows and already hosts Unity 2022.3.62f3.
15. **The first build is a Unity Development build, not a release build.** Development success proves packaging/launch only.
16. **Player output is written under an ignored validation directory and is never committed.**
17. **Launch smoke uses a disposable OS/test profile or another proven isolated Player data environment.** It may never read or mutate the developer's normal profile.
18. **The launch smoke proves Boot starts and reaches RealmSelection for a fresh isolated profile.** It does not click/commit a realm or test product realm-selection semantics.
19. **Kingdom is validated structurally, by Editor tests, and by inclusion in the Player build.** A selected-realm Boot → Kingdom Player smoke is deferred until a safe isolated fixture exists under #137/#173 or an equivalent reviewed seam.
20. **External process termination after the expected marker is allowed for the first technical smoke.** It must be reported as external termination, not a graceful quit/lifecycle test.
21. **Pause/quit save behavior is not inferred from an externally killed smoke.** #153 and later lifecycle/Player tests own graceful pause/quit evidence.
22. **A stable production-scene startup marker is required.** It reports exact scene ID/path/role without gameplay mutation and lets external launch validation distinguish successful scene activation from a requested transition.
23. **The marker is not scene authority by itself.** It is validated against the reviewed production descriptor.
24. **The first Build Settings profile is the `ShellFoundation` profile.** It contains Boot, RealmSelection, and Kingdom only.
25. **ChampionArena uses a separate later applicability gate.** Its current string field may remain as documented deferred intent only when #178 proves no normal release path invokes it.
26. **The unsafe Kingdom reset-to-Boot path is not an accepted production transition.** #178/#137 must remove or replace it before #150 validation.
27. **Build/launch evidence does not accept unsafe hidden behavior merely because the path was not exercised.** Required upstream containment must merge first.
28. **#150 depends on accepted #223 and the first containment result of #178.** #223 itself depends on #156 and the accepted #153 scene-lifecycle contract.
29. **#209 must be corrected before its PlayMode result is used as supporting evidence.** A Player build can still be technically built, but final #150 readiness includes the safe PlayMode suite.
30. **#183 catalog integration is not required to create the initial technical shell build.** When integrated later, scene startup must expose catalog pending/unavailable state rather than silent fallback.
31. **No narrative, balance, save algorithm, combat design, scene visual redesign, Android embedding, or product identity rebrand is bundled into #150.**
32. **Current `DefaultCompany`/`AnotherLifeUnity` Player settings are not silently changed in this build-health PR.** Release/application identity is a separate explicit decision unless a build blocker requires a focused issue.
33. **A successful Player build is not user release approval.** Final release remains user-gated.

## 3. Verified current scene inventory

### 3.1 Committed scenes

The unification first-parent inventory imported exactly one `.unity` asset:

```text
unity/Assets/Test.unity
```

No later scene-related pull request added a scene asset.

Classification:

| Path | Scene name | Purpose | Build applicability |
| --- | --- | --- | --- |
| `Assets/Test.unity` | `Test` | representative editor/PlayMode integration scene | test-only; never normal Build Settings |

`Test.unity` currently contains a `Demo_Manager`, a camera, a directional light, and runtime utility scripts. It is not a Boot, onboarding, Kingdom, or production Player entry scene.

### 3.2 Empty Build Settings

Current:

```text
unity/ProjectSettings/EditorBuildSettings.asset
```

contains:

```yaml
m_Scenes: []
```

A normal Player currently has no configured entry scene.

### 3.3 Existing editor generator

```text
unity/Assets/AL/Scripts/Editor/ALVerticalSliceSceneGenerator.cs
```

currently defines:

```text
Assets/AL/Scenes/Boot.unity
Assets/AL/Scenes/RealmSelection.unity
Assets/AL/Scenes/Kingdom.unity
Assets/AL/Scenes/ChampionArena.unity
```

and unconditionally replaces Build Settings with all four scenes.

Issue #223 now owns making this source-authoring path safe, non-destructive, deterministic, and separate from packaging.

## 4. Verified current transition graph

### 4.1 Boot

`BootController` exact serialized defaults:

```text
_realmSelectionScene = "RealmSelection"
_kingdomScene = "Kingdom"
```

Current conceptual behavior:

```text
no selected realm → RealmSelection
selected realm    → Kingdom
```

The Player launch smoke uses only the fresh isolated first branch.

### 4.2 Realm selection

`RealmSelectionController` exact serialized default:

```text
_nextScene = "Kingdom"
```

The controller currently calls realm mutation directly. #173 owns durable one-time realm-selection semantics. #150 does not exercise or approve that mutation in the first launch smoke.

### 4.3 Kingdom

`KingdomSceneController` exact serialized arena default:

```text
_arenaSceneName = "ChampionArena"
```

Current main also contains an unsafe direct reset load:

```text
SceneManager.LoadScene("Boot")
```

The current arena and reset routes are not accepted ShellFoundation transitions.

- #178 must remove/unwire the production Champion command and unsafe reset path before #150.
- ChampionArena remains disabled in normal Build Settings.
- reset-to-Boot is not entered in the production descriptor.

### 4.4 Champion Arena

`ChampionArenaSceneController` exact serialized return default:

```text
_kingdomSceneName = "Kingdom"
```

This is a deferred gameplay transition. The scene is not enabled in the initial normal Player build.

## 5. Approved scene authority after #223

Issue #223 commits these stable assets and `.meta` files:

```text
Assets/AL/Scenes/Boot.unity
Assets/AL/Scenes/RealmSelection.unity
Assets/AL/Scenes/Kingdom.unity
Assets/AL/Scenes/ChampionArena.unity
```

The production descriptor includes all four records but separates authoring status from build applicability.

Recommended immutable descriptor fields:

```text
sceneId
assetPath
sceneName
role
assetGuid
requiredControllerType
startupMarkerId
transitionTargets
buildProfiles
requiredUpstreamIssues
status
```

Recommended IDs:

```text
al_scene_boot
al_scene_realm_selection
al_scene_kingdom
al_scene_champion_arena
al_scene_test_representative
```

The Test record exists only in inventory/test policy and has no production build profile.

## 6. Scene roles and build applicability

### 6.1 Boot

```text
sceneId: al_scene_boot
path: Assets/AL/Scenes/Boot.unity
name: Boot
role: production_entry
ShellFoundation: required/index 0
```

Required scene structure follows #223 and accepted #153 lifecycle ownership.

### 6.2 RealmSelection

```text
sceneId: al_scene_realm_selection
path: Assets/AL/Scenes/RealmSelection.unity
name: RealmSelection
role: onboarding_selection
ShellFoundation: required/index 1
```

This scene is packaged even though the first smoke does not commit a realm.

### 6.3 Kingdom

```text
sceneId: al_scene_kingdom
path: Assets/AL/Scenes/Kingdom.unity
name: Kingdom
role: production_hub
ShellFoundation: required/index 2
```

Kingdom is included structurally. Current unsafe command/lifecycle behavior remains blocked by #178 and other domain issues; inclusion is not gameplay acceptance.

### 6.4 ChampionArena

```text
sceneId: al_scene_champion_arena
path: Assets/AL/Scenes/ChampionArena.unity
name: ChampionArena
role: deferred_gameplay
ShellFoundation: excluded
future Champion profile: gated by #178 and #180
```

It must not appear as an enabled or disabled stale Build Settings entry in the first profile. It is absent from `EditorBuildSettings.asset` entirely until enabled through a later reviewed profile change.

### 6.5 Test

```text
sceneId: al_scene_test_representative
path: Assets/Test.unity
name: Test
role: representative_test_only
normal build profiles: prohibited
```

## 7. ShellFoundation Build Settings

After #223 merges, #150 commits exactly:

```yaml
m_Scenes:
- enabled: 1
  path: Assets/AL/Scenes/Boot.unity
- enabled: 1
  path: Assets/AL/Scenes/RealmSelection.unity
- enabled: 1
  path: Assets/AL/Scenes/Kingdom.unity
```

Unity YAML also contains the corresponding stable scene GUID fields according to Unity's normal serialized format.

Rules:

- exact order shown above;
- all three enabled;
- no disabled stale entries;
- no `Assets/Test.unity`;
- no `ChampionArena.unity` in the first profile;
- no duplicate scene name or path;
- no missing path;
- descriptor, Build Settings, validator, and Player build script agree exactly;
- no editor utility rewrites the list during import or test execution;
- a later profile change appending ChampionArena is a separate reviewed PR after its gates.

## 8. Deferred-transition policy

A serialized scene name that is absent from the active build profile is permitted only when all of these are true:

1. the descriptor marks the target `Deferred`;
2. the owning feature issue is linked;
3. production/release UI and handlers cannot invoke the transition;
4. tests prove the route is unreachable in the active profile;
5. no startup/automatic path loads it;
6. the disabled target is not represented as available or successful;
7. a direct load attempt fails visibly in development and is not broadly caught as success.

For ShellFoundation:

```text
Kingdom → ChampionArena
```

is deferred and must be unreachable under corrected #178.

The reset-to-Boot route is not a deferred feature; it is an unsafe prototype/reset implementation that must be removed or replaced under #178/#137.

## 9. Production scene startup marker

Issue #223 adds one narrow technical marker component or equivalent per production scene.

Required immutable fields:

```text
sceneId
expectedAssetPath
role
sourceVersion
```

On activation it validates the current scene path/name and emits one stable technical log:

```text
[AL-SCENE-ACTIVE] id=<sceneId> name=<sceneName> path=<assetPath> role=<role> version=<sourceVersion>
```

Rules:

- exactly one marker per production scene;
- no marker in `Assets/Test.unity` unless explicitly test-specific and differently classified;
- no save, service, gameplay, navigation, or UI mutation;
- no `DontDestroyOnLoad` behavior;
- no player-facing copy;
- mismatch logs an Error and prevents a false smoke pass;
- marker values are validated against the production descriptor;
- event/log subscriber failure cannot change scene behavior.

The marker supports external Player smoke evidence without adding a hidden test scene.

## 10. Build Settings validator

Add or extend an Editor/EditMode validator driven by the descriptor.

Required outcomes:

```text
Valid
MissingBuildSettings
EmptyBuildSettings
WrongEntryScene
MissingRequiredScene
UnexpectedScene
DeferredSceneEnabled
TestSceneEnabled
DisabledStaleScene
MissingPath
DuplicatePath
DuplicateName
GuidMismatch
DescriptorDrift
TransitionUnavailable
DeferredTransitionReachable
```

Validation checks:

- Build Settings exists and parses;
- index 0 is exact Boot path;
- exact ShellFoundation list/order/enabled state;
- files exist;
- asset GUIDs match descriptor/committed meta;
- names are unique;
- startup markers/controllers validate through #223;
- Test is absent;
- ChampionArena is absent;
- Boot required transitions resolve;
- RealmSelection target resolves;
- Kingdom deferred Champion transition has no active production reachability after #178;
- reset-to-Boot is absent from accepted production command reachability;
- no generator/test mutates Build Settings during validation.

The quality-gate script in #155 may consume the same descriptor later. Do not duplicate policy lists without drift tests.

## 11. Player build script

Expected Editor-only path, subject to repository organization:

```text
unity/Assets/AL/Scripts/Editor/ProductionPlayerBuilder.cs
```

Expected entry method:

```text
AL.EditorTools.ProductionPlayerBuilder.BuildWindows64Development
```

### 11.1 Preflight

Before `BuildPipeline.BuildPlayer`:

1. confirm exact Unity version;
2. validate clean descriptor/Build Settings relation;
3. validate all enabled scenes and GUIDs;
4. validate Test and ChampionArena exclusion;
5. validate no duplicate names;
6. validate no compile errors/missing scripts;
7. validate output path is ignored and outside `Assets`;
8. record current git/base/head externally in the PR report;
9. fail before build on any preflight error.

### 11.2 Build options

Initial target:

```text
BuildTarget.StandaloneWindows64
```

Output:

```text
<repo>/unity/Builds/Validation/Windows64/AnotherLifeUnity.exe
```

Options:

```text
BuildOptions.Development
```

Do not enable profiler/debugger/network options unless the PR justifies them. Do not change product/company/application IDs in this PR.

### 11.3 Build result

Return/emit equivalent data:

```text
status
target
unityVersion
outputPath
scenePaths
startedAtUtc
endedAtUtc
totalTime
totalSize
warningCount
errorCount
BuildResult
summaryMessage
```

Batch behavior:

- `Succeeded` → process exit 0;
- any other `BuildResult` → nonzero exit/exception;
- missing executable/data directory → fail even when report says success;
- no stale output may be mistaken for current success;
- clean the exact validation output before building or use a unique run directory;
- no output is committed.

### 11.4 Build transcript

Retain:

```text
PlayerBuildWindows64.log
machine-readable summary JSON or deterministic text
complete BuildReport summary
```

The transcript records exact scene paths and confirms Test/Champion exclusion.

## 12. Canonical build command

```powershell
$repo = "C:\Users\MY\Documents\AnotherLife"
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"

& $unity -batchmode -quit -nographics `
  -projectPath "$repo\unity" `
  -executeMethod AL.EditorTools.ProductionPlayerBuilder.BuildWindows64Development `
  -logFile "$repo\unity\Logs\PlayerBuildWindows64.log"

if ($LASTEXITCODE -ne 0) {
  throw "Unity Player build failed with exit code $LASTEXITCODE"
}
```

The exact command used and exit code appear in the PR.

Licensing exit `199`, a missing report, missing output, or stale output is blocked/failing evidence.

## 13. Isolated Player launch smoke

### 13.1 Profile safety

Run under one reviewed isolation method:

- a disposable Windows test account;
- Windows Sandbox/ephemeral VM;
- a CI runner account with isolated application data;
- another proven platform method whose `Application.persistentDataPath` cannot resolve to the developer's normal profile.

Before launch:

- record the resolved isolated account/environment;
- verify no AnotherLife save artifacts exist there;
- verify the developer profile path is not the smoke path;
- record any hard-crash/termination cleanup limitation.

Do not use or copy the developer's live save. Do not depend on #127's Editor profile snapshot as proof for a separate Player process.

### 13.2 Launch command

Equivalent Windows command:

```powershell
$exe = "$repo\unity\Builds\Validation\Windows64\AnotherLifeUnity.exe"
$log = "$repo\unity\Logs\PlayerLaunchFreshProfile.log"

$p = Start-Process -FilePath $exe `
  -ArgumentList @(
    '-logFile', $log,
    '-screen-fullscreen', '0',
    '-screen-width', '1280',
    '-screen-height', '720'
  ) `
  -PassThru
```

Poll with an explicit realtime timeout, recommended 30 seconds.

### 13.3 Required fresh-profile sequence

Required ordered evidence:

```text
[AL-SCENE-ACTIVE] id=al_scene_boot ...
AL Boot Sequence Started...
No Realm Selected. Transitioning to Realm Selection...
[AL-SCENE-ACTIVE] id=al_scene_realm_selection ...
```

Exact wording may change only through an updated stable marker contract. Cosmetic logs are not asserted.

### 13.4 Failure conditions

Fail on:

- process exits before RealmSelection marker;
- timeout;
- wrong entry marker;
- Kingdom marker on an actually fresh isolated profile unless a documented valid fixture explains it;
- scene missing/not in Build Settings errors;
- `ArgumentException`, `MissingReferenceException`, `MissingMethodException`, `NullReferenceException`, Assert, or unhandled Exception;
- Bootloader initialization/load failure;
- marker path/name mismatch;
- missing script/serialization error;
- Test or ChampionArena marker;
- evidence that the normal developer profile was accessed.

Warnings are inventoried and reviewed; they are not automatically ignored.

### 13.5 Termination

After the RealmSelection marker is observed, the first technical smoke may terminate the process externally.

Report:

```text
transition passed
process terminated externally for validation
no graceful quit/save claim
isolated profile may contain disposable test artifacts
```

Clean the disposable environment after retaining logs. A hard kill is never used to validate pause/quit save behavior.

## 14. Optional later Player smokes

Not required for initial #150 closure unless prerequisites become ready:

### Selected-realm Boot → Kingdom

Requires a safe isolated, validated, version-compatible selected-realm fixture under #137/#173 or another reviewed non-production seam.

### Kingdom → ChampionArena → Kingdom

Requires:

- #178 release reachability/result safety;
- #180 Champion combat/encounter lifecycle;
- ChampionArena appended through a separate reviewed build-profile change;
- no post-clear credit/loot/result duplication;
- Player build and launch evidence.

### Graceful pause/quit

Requires accepted #153 lifecycle behavior and a controllable Player harness that does not compromise production builds.

## 15. Interaction with other gates

### #156 / #223

#156 must clear the trusted Unity asset baseline. #223 then commits and validates scenes without Build Settings changes.

### #153 / PR #203

#153 owns the Bootloader lifecycle owner across scene transitions. #150 consumes it; #150 does not edit `Bootloader.cs`.

### #178 / PR #208

Before #150:

- unsafe Kingdom prototype command handlers are absent/unreachable;
- Champion transition is not production reachable;
- reset-to-Boot is absent from production command reachability;
- unavailable commands do not claim success.

### #127 / PR #209

Corrected safe PlayMode evidence supports final #150 readiness but is not replaced by the Player launch smoke.

### #183 / PR #220

The initial shell may precede catalog migration. Later catalog integration must expose readiness/unavailable state and cannot silently use development fallback in a production build.

### #137 / #173

Initial launch smoke does not commit a realm or test durable profile behavior. Selected-realm Player smoke waits for safe fixtures/semantics.

### #135

Android Unity export/host packaging repeats the scene/package inventory after the Windows Player foundation passes. #150 does not embed Unity in Android.

## 16. Required automated tests

### 16.1 Descriptor

- exact scene IDs/paths/names/roles;
- unique IDs, paths, names, GUIDs;
- Boot entry applicability;
- ShellFoundation exact order;
- Test prohibited;
- Champion deferred;
- required upstream issue IDs retained;
- descriptor and #223 validator constants cannot drift.

### 16.2 Build Settings

- empty list fails;
- missing Boot fails;
- wrong index 0 fails;
- missing RealmSelection/Kingdom fails;
- wrong order fails;
- disabled required scene fails;
- unexpected scene fails;
- disabled stale entry fails;
- Test enabled or listed fails;
- Champion listed before applicability fails;
- missing path fails;
- duplicate path/name fails;
- GUID mismatch fails;
- exact ShellFoundation passes.

### 16.3 Transition policy

- Boot RealmSelection/Kingdom names resolve to descriptor;
- RealmSelection Kingdom resolves;
- Kingdom Champion target is deferred;
- deferred target has no active production handler after #178;
- Champion return resolves in full scene inventory even while not in ShellFoundation;
- reset-to-Boot is not accepted production reachability;
- case mismatch fails.

### 16.4 Startup markers

- exactly one per production scene;
- field/path/name/role/version match descriptor;
- mismatch emits one Error;
- valid activation emits exact stable marker once;
- marker mutates no service/save/navigation state;
- Test is not represented as a production marker.

### 16.5 Build script

Using seams around the Unity build call where practical:

- exact target/output/scenes/options;
- preflight failure prevents build;
- stale output removed or isolated;
- success report plus required files returns success;
- failed/cancelled/unknown result fails;
- report success but missing output fails;
- warning/error counts reported;
- Test/Champion exclusion present in transcript;
- output path outside tracked source;
- batch entry propagates failure.

### 16.6 Player-log parser/process harness

- ordered valid Boot → RealmSelection markers pass;
- RealmSelection before Boot fails;
- missing second marker times out;
- wrong marker/profile branch fails;
- missing-scene error fails;
- severe log fails;
- ordinary known logs do not fail merely by existing;
- process early exit fails;
- external termination after success is reported distinctly;
- path/profile safety mismatch fails.

## 17. Required validation sequence

1. Fetch current `main`; confirm #156, #223, #153, and #178 prerequisites.
2. Inspect all open PRs and locks.
3. Confirm exactly four committed production scene candidates plus Test after #223.
4. Run canonical Unity compile/import.
5. Run focused production-scene/Build Settings tests.
6. Run the complete EditMode suite and retain XML/totals.
7. Run corrected #127 PlayMode suite when available.
8. Commit exact ShellFoundation Build Settings.
9. Run build script in canonical workspace.
10. Verify current output/report/log and no tracked output.
11. Run fresh isolated Player launch smoke.
12. Retain ordered marker evidence and severe-log inventory.
13. Externally terminate after success and clean disposable profile.
14. Run final diff/status/shared-lock checks.
15. Return draft PR for Codex coordination/review.

## 18. Expected implementation boundary

Likely #150 files after #223:

```text
unity/ProjectSettings/EditorBuildSettings.asset
unity/Assets/AL/Scripts/Editor/ProductionPlayerBuilder.cs
unity/Assets/AL/Scripts/Editor/ProductionPlayerBuilder.cs.meta
small build-result/profile/validation Editor types
unity/Assets/AL/Tests/EditMode/ProductionBuildSettingsTests.cs
matching .meta
optional external/local PowerShell launch-smoke script under tools/ci or tools/validation
technical production build record
```

A small startup marker belongs primarily in #223 scene authority. If it must be completed in #150, declare it before editing and keep it purely technical/non-mutating.

Prohibited by default:

```text
scene content or scene .meta regeneration after #223
Assets/Test.unity
ChampionArena Build Settings entry
Bootloader.cs / ServiceLocator.cs
runtime gameplay controllers
save implementation/data
narrative/content
terrestrial source
Android host/export
PlayerSettings branding/identity
committed Build/Builds output
```

## 19. Required PR report

- exact base/head SHA;
- prerequisite merge SHAs;
- complete scene inventory and descriptor version;
- exact Build Settings before/after;
- Test/Champion exclusion proof;
- Unity version;
- compile command/result;
- focused and complete EditMode totals/XML;
- PlayMode availability/result;
- build command and exit code;
- BuildReport result/time/size/warning/error summary;
- exact output inventory and hashes where practical;
- launch environment/profile isolation method;
- launch command, timeout, process ID/exit/termination disposition;
- ordered Boot/RealmSelection markers;
- severe-log inventory;
- disposable-profile cleanup evidence;
- final `git diff --check origin/main...HEAD`;
- final `git status --short --branch`;
- every blocked/unperformed optional smoke;
- rollback instructions.

## 20. Rollback

Rollback #150 by reverting:

- exact Build Settings list;
- build/validation Editor tooling;
- tests/scripts/record added by the PR.

Do not delete the committed scene assets from #223 as part of a #150 rollback. They remain source-controlled scene authority unless #223 itself is reverted.

A failed Player build never authorizes switching Build Settings back to Test.

## 21. Acceptance criteria

- [ ] #156 and #223 are complete, with stable committed scene assets/GUIDs.
- [ ] #153 lifecycle ownership survives the production scene flow.
- [ ] #178 removes/unwires Champion/reset prototype reachability for ShellFoundation.
- [ ] One reviewed descriptor is the source of Build Settings/build/test expectations.
- [ ] Normal Build Settings contain exactly Boot, RealmSelection, Kingdom in that order.
- [ ] Boot is the sole entry at index 0.
- [ ] Test and ChampionArena are absent from normal Build Settings.
- [ ] Every enabled path exists, is unique, and has the expected stable GUID/name/marker/controller.
- [ ] Deferred Champion transition is unreachable and visibly unavailable.
- [ ] Build Settings validation is automated and complete.
- [ ] Canonical Unity compile and complete EditMode tests pass.
- [ ] Corrected safe PlayMode evidence passes or is explicitly blocked for a reason that keeps #150 open.
- [ ] Windows 64-bit Development Player build succeeds through the reviewed build script.
- [ ] Current output/report/log are retained and no build output is tracked.
- [ ] Fresh isolated Player launch starts Boot and reaches RealmSelection within the timeout with no severe/missing-scene error.
- [ ] No developer profile is read or mutated.
- [ ] External termination is reported honestly; no graceful quit/save claim is made.
- [ ] No narrative, balance, save algorithm, combat design, Android embedding, scene redesign, branding, or unrelated change is included.
- [ ] User release approval is not inferred from technical build success.

## 22. Codex handoff

```text
Codex engineering: do not implement #150 until #156, #223, the accepted #153 scene-lifecycle contract, and the first #178 containment are merged. Then use current main and unity/Docs/Production_Scene_Player_Build_Spec.md on codex/production-scene-build-settings. Consume the shared production-scene descriptor; commit Build Settings with exactly Boot, RealmSelection, and Kingdom; keep Test and ChampionArena absent; add strict Build Settings validation and a typed Windows64 Development build script; build only into ignored validation output; and run an isolated fresh-profile Player smoke proving Boot -> RealmSelection through stable scene markers. Do not edit scenes, Bootloader, saves, gameplay, narrative, Android, branding, or add ChampionArena. Return one focused draft PR with exact canonical evidence for Codex coordination/review.
```
