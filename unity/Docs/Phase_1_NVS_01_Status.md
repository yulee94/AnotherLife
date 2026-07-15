# Phase 1 NVS-01 Status

**Status date:** 2026-07-14  
**Current integration branch:** `main`  
**Audited current-main head:** `5a7ab24fc81d40a619eb71349fa32d81b1e5047e`  
**Roadmap state:** Phase 1 is paused behind a Phase 0 build-health regression gate  
**Immediate owner:** Codex under issue #145

`AGENTS.md` is authoritative. This record replaces the stale assumption that the clean A1 → G1 → C1–C4 chain was completed merely because issues or pull requests were closed.

Use this document with:

- `AGENTS.md`
- `unity/Docs/Project_Progression_Roadmap.md`
- `unity/Docs/Three_Way_Collaboration_Plan.md`
- `unity/Docs/Phase_1_NVS_01_Risk_Register.md`
- issue #138 for the approved D1–D16 product decisions
- reopened issues #127, #128, #133, #134, #135, #136, and #137
- critical build-health issue #145

## Current repository state

The following changes are present on `main`:

- PR #141 — merged the KSP bump, partial save changes, save tests, and PlayMode test infrastructure despite a blocking review and without the required local Unity validation.
- PR #124 — merged an archive explicitly described as “must not be merged,” including the bounded packet, broad Chapter 1 material, Android UI/model changes, and Unity definition/namespace changes.
- PR #143 — removed `Assets/Test.unity` from normal Build Settings and changed the PlayMode smoke to load the scene by editor path.
- PR #144 — added an Android reflection-based `UnityPlayer` host and bridge documentation.

At the time of this audit there were no open pull requests. Closed or merged status is not completion evidence; every gate below is based on current source and acceptance criteria.

## Phase 0 regression gate

### #145 — restore trusted Unity compilation

**Status:** Fixed and validated  
**Owner:** Codex  
**Scope:** mechanical namespace/build repair only

Fixed namespace mismatches in `LocalStoryService.cs` where `DialogueChoice` was still referenced with the old fully qualified name. Validated with a clean post-fix compile (log: `unity/Logs/VerifyCompile.log`).

## Verified source audit

### Android shell

The Android app currently creates separate in-memory `KingdomState` and `NarrativeState` objects in Compose. It does not load the Unity save, quest service, or story service.

`Route.Quest` exists and has an entry-provider branch, but the current bottom navigation does not expose a Quest destination. `Route.Champion` renders `AcademyScreen`, not `UnityView`. `UnityView` is imported but not mounted by the shell.

Therefore the Android app remains a native simulation/narrative preview rather than a proven host for the integrated game loop.

### Current `OMEN_1` packet

`NVS_01_Packet.kt` is still the archived pre-approval packet and conflicts with issue #138 in material ways:

- it uses `CH0_PROLOGUE`, no realm prerequisite, and automatic start;
- it references missing `DLG_OMEN_1_ARENA_START`;
- it grants `+5` Valerius affinity at acceptance instead of report completion;
- it automatically triggers the success dialogue after arena success instead of requiring a manual report;
- it grants the Celestial Tear at report completion instead of arena success;
- its failure consequence and retry constant disagree;
- it prohibits abandonment;
- its localization inventory is incomplete;
- it does not encode the full D16 resume model.

Issue #128 is therefore reopened. The merged archive is source history, not an approved A1 artifact.

### Unity quest runtime

`LocalQuestService` currently registers Q1–Q5 only. It does not load or register `OMEN_1`, consume the Android packet, validate narrative references, issue a typed arena request, process a typed result, or execute the approved report-completion transaction.

`QuestState` contains only quest ID, scalar progress, completion, and claim flags. `SaveGameData` has no persisted NVS-01 dialogue node, objective state, handoff correlation, pending Tear, recovery state, or applied-consequence ledger.

Issues #133 and #134 are therefore reopened and remain blocked in their original order.

### Save compatibility and recovery

Current source includes the three #136 default initializations and limited reflection-based tests. The issue remains open until service mutation, save/reload round-trip, Unity compilation, and exact test evidence are complete.

Current #137 code is only a partial implementation. Verified gaps include:

- current primary bytes can be copied over the backup before the primary is validated;
- ordinary `IOException` paths are treated as unsupported-operation fallback;
- load/recovery status and save status are not separate observable contracts;
- recovery messages can be overwritten by an internal `Save()`;
- the first successful save does not establish the documented backup generation;
- installed primary and backup are not fully revalidated after rotation;
- `DeleteSave()` leaves previous/quarantine profile artifacts;
- offline progress is mutated before durable persistence without a rollback strategy;
- the required fault-injection and deletion matrix is absent.

Issue #137 is reopened and remains dependent on #136.

### PlayMode coverage

The current PlayMode test loads `Assets/Test.unity` by editor path, which fixes the shipped-Build-Settings problem. It still lacks:

- developer profile snapshot/isolation/restoration;
- bounded scene-load timeout;
- guaranteed restoration of global log/time state;
- static `ServiceLocator` cleanup;
- proof that no extra save artifacts remain;
- a recorded successful Unity PlayMode XML result on current `main`.

Issue #127 is reopened.

### Android↔Unity bridge

`UnityView.kt` now contains a reflection-based host, lifecycle forwarding, route JSON, and a callback parser. However:

- the Gradle project includes only `:app`;
- no `unityLibrary` module or Unity AAR/native export is packaged;
- the shell does not mount `UnityView`;
- Unity-side `AndroidBridge.SetRouteContext` consumption and result production are not proven;
- lifecycle, configuration, back, device, memory, and end-to-end route tests are absent.

Issue #135 is reopened and remains deferred until the standalone NVS-01 pipeline is proven.

## Correct dependency order

```text
#145 — restore and prove Unity compilation
        ↓
#127 — safe representative-scene PlayMode coverage     #136 — complete old-save defaults evidence
                                                        ↓
                                                     #137 — independent save hardening
        ↓
#128 — clean, D1–D16-faithful A1 packet
        ↓
#133 — GPT G1 runtime integration specification
        ↓
#134 — Codex C1–C4 implementation and verification
        ↓
G2 — GPT technical/integration review
        ↓
A2 — Android Studio narrative-fidelity review
        ↓
U1 — user playtest and NVS-01 acceptance
        ↓
#135 — production Android↔Unity embedding milestone
```

#127 and #136 may proceed independently after #145 restores a trusted Unity base. #137 starts after #136. #133 must not start before a clean, approved #128 artifact exists. #134 must not start before approved G1.

## Deferred archive content

Merged files associated with #129–#131 preserve future ideas but do not prove those milestones complete.

- Chapter 1 packet files are unapproved future content until Phase 2 review and user acceptance.
- Realm/building/dossier/world hooks are proposals until event ownership, payloads, persistence, idempotency, and tests are specified.
- Narrative governance/templates are proposals until repeated packet needs justify a versioned policy and validator.

These files must not be treated as runtime authority for NVS-01.

## Evidence rules

A task is complete only when its own acceptance criteria are met. The following are not sufficient by themselves:

- an issue being closed;
- a pull request being merged;
- Android compilation succeeding for a Unity-only change;
- a test source file existing without a recorded runner result;
- a document describing a contract that no producer or consumer implements;
- code compiling without persistence, duplicate-safety, or player-visible behavior evidence.

Every completion report must identify exact base/head SHA, files changed, commands, exit codes, test totals, unperformed validation, ownership boundaries, and remaining risk.

## Pull-request and shared-file state

- Open pull requests at audit: none.
- Current shared-file soft locks: none.
- No direct commits to `main` are authorized.

Potential shared integration files remain:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

The first approved implementation PR declaring one holds its soft lock.

## Current next action

```text
Owner: Codex
Issue: #145
Base: fetched current main at or after 5a7ab24fc81d40a619eb71349fa32d81b1e5047e
Branch: codex/fix-narrative-namespace-compile
Deliverable: pre-fix compiler evidence, narrow mechanical fix, clean Unity compile, EditMode totals, metadata verification, and focused PR
```

Android Studio may inspect #128 and prepare narrative work, but no A1 PR should claim a trusted base until #145 has established current-main Unity build health.