# Phase 1 NVS-01 Status

**Status date:** 2026-07-15  
**Current integration branch:** `main`  
**Audited current-main head:** `55128d21a6dbf9402eb78396dbe59f8d7e4bcac9`  
**Roadmap state:** Phase 1 is paused behind Phase 0 compilation and serialized-asset recovery  
**Immediate owner:** Codex under issue #145 and draft PR #147

`AGENTS.md` is authoritative. This record distinguishes source presence, issue state, merge state, validation evidence, and actual player-visible completion.

Use this document with:

- `AGENTS.md`
- `unity/Docs/Project_Progression_Roadmap.md`
- `unity/Docs/Three_Way_Collaboration_Plan.md`
- `unity/Docs/Phase_1_NVS_01_Risk_Register.md`
- issue #138 for the approved D1–D16 product decisions
- active recovery issues #127, #128, #133–#137, #145, #148, #150, #152, #153, #155, and #156

## Current repository state

The following incident and remediation changes are present on `main`:

- PR #141 merged the KSP bump, partial save implementation, save tests, and PlayMode test infrastructure despite a blocking review and without its required local Unity validation.
- PR #124 merged an archive explicitly described as “must not be merged,” including the bounded packet, broad Chapter 1 material, Android UI/model changes, and Unity definition/namespace changes.
- PR #143 removed `Assets/Test.unity` from normal Build Settings and changed the PlayMode smoke to load the scene by editor path.
- PR #144 added an Android reflection-based `UnityPlayer` host and bridge documentation.
- PR #146 merged the first truthful current-main recovery status and risk records.

Current pull-request state:

- PR #147 is an open draft for #145. Its `LocalStoryService.cs` namespace fix is correct, but it remains blocked until the unrelated #127 PlayMode test edit is removed and its description links `Fixes #145` / `Refs #156`.
- PR #149 was closed unmerged. Its tiered-loot/VFX prototype is preserved under deferred issue #151 and holds no shared-file lock.

Closed or merged status is not completion evidence. Every gate below is based on current source and the acceptance criteria of the owning issue.

## Phase 0 recovery gates

### Gate A — #145: restore trusted Unity compilation

**Status:** Open; draft PR #147 blocked pending scope correction  
**Owner:** Codex  
**Scope:** one mechanical namespace/build repair

Current source moved `DialogueNode` and `DialogueChoice` to `AL.Data.Definitions.Narrative`, while `LocalStoryService.cs` still constructs choices using the removed old type:

```csharp
new AL.Data.Definitions.DialogueChoice { ... }
```

PR #147 records the expected pre-fix `CS0234`, a post-fix successful compile, and EditMode 6/6. It must contain only the `LocalStoryService.cs` correction. Exact startup-log expectations in `RepresentativeSceneSmokeTests.cs` do not belong in #145 and do not satisfy #127.

### Gate B — #156: consolidate QuestDefinition authority and serialized GUIDs

**Status:** Open; starts after the #145 one-file compile fix  
**Owner:** Codex  
**Scope:** technical definition/asset migration without narrative changes

Before PR #124, two `QuestDefinition` scripts existed:

```text
AL.Data.Definitions.QuestDefinition
GUID 226022aa7500f3e4abc8ac3757707ad8

AL.Data.Definitions.Narrative.QuestDefinition
GUID c385b2b183b74184ca75eeffbe2256ef
```

PR #124 removed the root type and changed `LocalQuestService` to the narrative type. A clean compile does not prove serialized assets referencing the removed GUID remain valid. #156 must inventory both GUID populations, select one technical authority, migrate tracked assets deterministically, and add regression validation.

No A1 packet or production Player build may claim a trusted Unity base until #145 and #156 are complete.

## Verified source audit

### Android shell and preview boundary

The Android app creates separate in-memory `KingdomState` and `NarrativeState` objects in Compose. It does not load the Unity save, quest service, or story service.

`Route.Quest` exists and has an entry-provider branch, but bottom navigation does not expose it. `Route.Champion` renders `AcademyScreen`, not `UnityView`. `UnityView` is imported but not mounted.

The shell also hard-codes a Valerius intro and silently closes the overlay when a dialogue target cannot be found. It is therefore a non-authoritative preview/demo path until #133 defines the boundary and #135 later completes packaging and embedding.

### Current `OMEN_1` packet

`NVS_01_Packet.kt` is still the archived pre-approval packet and conflicts with issue #138:

- `CH0_PROLOGUE`, no realm prerequisite, and automatic start;
- missing `DLG_OMEN_1_ARENA_START`;
- `+5` affinity at acceptance instead of report completion;
- automatic success dialogue instead of manual report;
- Tear granted at report completion instead of arena success;
- contradictory failure consequence and retry constant;
- abandonment prohibited;
- incomplete localization inventory;
- no complete D16 resume model.

Issue #128 remains blocked by #145 and #156. The merged archive is source history, not an approved A1 artifact.

### Unity quest runtime

`LocalQuestService` registers Q1–Q5 only. It does not load `OMEN_1`, consume an approved catalog, validate narrative references, issue a typed arena request, process a typed result, or execute the report-completion transaction.

`QuestState` contains only quest ID, scalar progress, completion, and claim flags. `SaveGameData` has no persisted NVS-01 dialogue node, objective state, handoff correlation, pending Tear, recovery state, or applied-consequence ledger.

Issues #133 and #134 remain blocked in their original order.

### Quest-state save compatibility — #152

`SaveGameData.Quests` is normalized only at the list level. Current quest and side-quest services dereference null entries and blank IDs, and `ClaimReward` indexes `_definitions[questId]` without a checked definition lookup.

#152 must define and test null, blank, unknown, and duplicate quest-state behavior. Unknown future/legacy IDs must be preserved safely and must never throw or grant rewards.

### Save compatibility and recovery

Current source includes the three #136 default initializations and limited reflection-based tests. #136 remains open until real reputation, faction, and persona service mutations persist through save/reload and normalization is proven idempotent.

Current #137 code remains partial. Verified gaps include:

- unvalidated primary bytes can overwrite the last-known-good backup;
- ordinary `IOException` failures are treated as unsupported-operation fallback;
- load/recovery and save status are conflated;
- recovery messages can be overwritten by an internal save;
- first successful save does not establish the documented backup generation;
- installed primary and backup are not fully revalidated;
- `DeleteSave()` leaves previous/quarantine artifacts;
- offline progress mutates before durable persistence without rollback;
- current fallback writes `save.json.previous` while the approved model uses `save.previous.json`;
- quarantine failure can lead toward an unsafe new-profile path;
- the fault-injection and deletion matrix is absent.

#137 starts after both #136 and the quest-state compatibility policy from #152 are integrated.

### PlayMode coverage — #127

The current test loads `Assets/Test.unity` by editor path, fixing the shipped-Build-Settings problem. It still lacks:

- developer-profile snapshot/isolation/restoration;
- protection for both `save.previous.json` and legacy `save.json.previous`;
- bounded load timeout;
- guaranteed restoration of global log/time state;
- static `ServiceLocator` cleanup;
- proof that no extra save artifacts remain;
- stable structural startup assertions rather than exact ordinary-log sequencing;
- an accepted current-main PlayMode XML result.

#127 starts after #145.

### Bootloader service integrity — #153

`Bootloader.InitializeIfMissing()` treats a single registered `IResourceService` as proof that the entire offline service graph exists. A partial or stale static registry can therefore skip initialization and fail later on save, story, quest, or pause/quit paths.

#153 must replace the single-service sentinel with deterministic full-stack readiness, coherent root dependencies, visible failure, and empty/full/partial/repeated initialization tests. It requires a soft lock on `Bootloader.cs`.

### Android constructor compatibility — #148

PR #124 reordered `Quest` constructor parameters and regressed the compatibility guarantee from PR #119. `isCompleted` and `isClaimed` must again immediately follow `target`, before defaulted `mode` and `mapMarkerId`, with a positional-construction test.

#148 is Android-only and may proceed independently after checking open PR overlap.

### Production Player scene list — #150

`unity/ProjectSettings/EditorBuildSettings.asset` currently has `m_Scenes: []`, while runtime controllers load scenes such as `RealmSelection` and `Kingdom` by name.

#150 must inventory committed scenes and string-loaded dependencies, define the minimal production flow, keep `Assets/Test.unity` excluded, add build-settings validation, and prove a development Player build. It starts after #145 and #156.

### Android↔Unity bridge — #135

`UnityView.kt` contains a reflection-based host, lifecycle forwarding, route JSON, and callback parsing. However:

- Gradle includes only `:app`;
- no Unity export is packaged;
- the shell does not mount `UnityView`;
- Unity-side request consumption and result production are unproven;
- duplicate suppression is keyed only by route string, so a second launch of the same route can have all outcomes ignored;
- no session/correlation ID distinguishes duplicate, late, or retried results;
- lifecycle, configuration, back, device, memory, and end-to-end tests are absent.

#135 remains deferred until standalone NVS-01 and #150 are complete.

### Repository quality gate — #155

No repository CI workflow or required status check protects `main`. #155 must add staged repository/Android checks, a reviewed Unity runner model, metadata/build-settings guards, artifacts, path-aware ownership reporting, and verified branch/merge controls.

The Android/repository phase may begin after #145; Unity checks become required only after they are reliable.

## Correct dependency order

```text
#145 — one-file Unity compile repair
        ↓
#156 — QuestDefinition authority and serialized-asset migration
        ↓
trusted Unity source/asset baseline

Independent or parallel foundations after #145:
  #127 — profile-safe PlayMode smoke
  #136 — reputation/faction/persona mutation and round trip
  #152 — null/unknown/duplicate quest-state compatibility
  #153 — coherent Bootloader service-stack readiness
  #155 — repository/Android quality gates; staged Unity runner

#136 + #152
        ↓
#137 — validated crash-safe save persistence

Independent Android repair:
  #148 — Quest positional constructor compatibility

After #145 + #156:
  #150 — production scene inventory, Build Settings, and Player smoke
  #128 — clean D1–D16-faithful A1 packet

#128
  ↓
#133 — GPT G1 runtime integration specification
  ↓
#134 — Codex C1–C4 only after G1 and required foundations (#127, #136, #137, #152, #153)
  ↓
G2 — GPT technical/integration review
  ↓
A2 — Android Studio narrative-fidelity review
  ↓
U1 — user playtest and NVS-01 acceptance
  ↓
#135 — packaged Android↔Unity bridge
```

#150 is required before release or Android Unity export. #155 should protect the large #134 integration before it merges.

## Deferred archive and prototype content

Merged files associated with #129–#131 preserve future ideas but do not prove those milestones complete. Those issues are closed as `not_planned` for the active phase.

PR #149 is closed unmerged. Its item-grade/loot-reveal proposal is preserved under deferred issue #151, also closed `not_planned`, until build and save foundations are complete.

Neither archive nor prototype content is runtime authority for NVS-01.

## Evidence rules

A task is complete only when its own acceptance criteria are met. The following are insufficient by themselves:

- an issue being closed;
- a pull request being merged;
- a source or test file existing;
- Android compilation for a Unity change;
- a skipped or unavailable job represented as passing;
- a document whose producer/consumer is not implemented;
- compilation without asset, persistence, duplicate-safety, build, or player-visible evidence.

Every completion report must identify exact base/head SHA, files changed, commands, exit codes, test totals, artifacts, unperformed validation, ownership boundaries, shared locks, and remaining risk.

## Pull-request and shared-file state

- Open PR: #147, draft, blocked pending one-file scope correction.
- Current designated shared-file soft locks: none.
- PR #149 is closed and holds no lock.
- No direct commits to `main` are authorized.

Designated shared files:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

The first approved open PR declaring one holds the lock.

## Current next action

```text
Owner: Codex
Issue: #145
PR: #147
Required revision:
  - remove RepresentativeSceneSmokeTests.cs from the diff;
  - retain only LocalStoryService.cs namespace correction;
  - update body with Fixes #145 / Refs #156;
  - retain pre-fix CS0234, post-fix compile, and EditMode 6/6 evidence;
  - leave draft for GPT re-review.
```

After #145 merges, Codex takes #156. Android Studio may prepare #128, but no A1 PR should claim a trusted base until both recovery gates are complete.