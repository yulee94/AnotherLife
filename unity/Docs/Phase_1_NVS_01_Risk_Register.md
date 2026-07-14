# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-14  
**Audited current-main head:** `5a7ab24fc81d40a619eb71349fa32d81b1e5047e`  
**Active control state:** Phase 1 paused behind #145 build-health recovery  
**Approved product intent:** issue #138 D1–D16  
**Active narrative gate after recovery:** issue #128

This register describes verified current-source risk. It supersedes the former assumption that merged PRs #124 and #141 completed the work they claimed.

Use with:

- `AGENTS.md`
- `unity/Docs/Phase_1_NVS_01_Status.md`
- `unity/Docs/Project_Progression_Roadmap.md`
- `unity/Docs/Three_Way_Collaboration_Plan.md`
- reopened issues #127, #128, #133, #134, #135, #136, and #137
- critical issue #145

## Severity and status

- **Critical:** build break, data-loss risk, invalid milestone authority, or uncontrolled integration.
- **High:** incomplete or non-deterministic player path, persistence failure, duplicate consequences, or false completion.
- **Medium:** test, diagnostics, UX integration, or governance gap with a bounded workaround.
- **Low:** build hygiene or non-blocking quality debt.

Status values:

- **Open:** actionable now or after the named prerequisite.
- **Blocked:** cannot start until an upstream artifact is approved.
- **Contained:** present on `main` but prevented from becoming authority.
- **Deferred:** intentionally scheduled after the current milestone.
- **Mitigated:** partially controlled, but acceptance evidence is incomplete.
- **Closed:** all stated acceptance evidence is complete.

## Current risks

| ID | Severity | Risk | Current evidence | Owner | Tracking/status |
| --- | --- | --- | --- | --- | --- |
| R1 | Critical | Current Unity compilation is not trusted after the archive namespace migration | `DialogueModels.cs` now defines types in `AL.Data.Definitions.Narrative`; `LocalStoryService.cs` still constructs `AL.Data.Definitions.DialogueChoice` | Codex | **Open — #145; blocks all Unity-dependent evidence** |
| R2 | Critical | Merged archive content is being mistaken for approved A1/G1/runtime completion | PR #124 was marked “must not be merged” but was merged; current packet still conflicts with #138 | GPT + Android Studio | **Open/contained — #128 reopened; archive is not authority** |
| R3 | Critical | Save rotation can overwrite the last-known-good backup with an unvalidated primary | `File.Copy(SavePath, BackupPath, true)` occurs before primary validation | Codex | **Open — #137 after #136** |
| R4 | Critical | Phase status and issue state can falsely authorize downstream work | #128/#133/#134 were closed although their artifacts are absent | GPT | **Mitigated — issues reopened; status docs corrected** |
| R5 | High | No authoritative runtime narrative catalog exists | Android packet, Android seed state, Unity fallback dialogue, and generic Unity quests remain separate | GPT | **Blocked — #133 after approved #128** |
| R6 | High | `OMEN_1` packet contradicts approved D1–D16 | Wrong start context, dangling node, wrong consequence timing, automatic report, failure conflict, incomplete localization/resume | Android Studio | **Open — #128** |
| R7 | High | Unity does not register or execute `OMEN_1` | `LocalQuestService` registers Q1–Q5 only; no packet loader or state machine | Codex | **Blocked — #134 after #133** |
| R8 | High | Encounter request/result contract is absent | No typed producer/consumer/correlation path for `HOOK_SKY_CASTLE_ARENA` | GPT + Codex | **Blocked — #133/#134** |
| R9 | High | NVS-01 persistence model is absent | `QuestState` lacks objective/dialogue/handoff/recovery/ledger state; `SaveGameData` lacks NVS-01 fields | GPT + Codex | **Blocked — #133/#134** |
| R10 | High | Approved consequences cannot be atomic or duplicate-safe in the current service layout | resource, affinity, quest, artifact, and chapter operations have no shared ledger/transaction boundary | GPT + Codex | **Blocked — #133/#134** |
| R11 | High | Old saves can be only partially proven safe for reputation/faction/persona | defaults exist, but mutation and round-trip evidence are incomplete | Codex | **Open — #136** |
| R12 | High | Save load/recovery and save status are conflated | one `LastPersistenceMessage`; recovery can be overwritten by internal save; interface exposes neither status model | Codex | **Open — #137** |
| R13 | High | Offline progress may duplicate after a failed repair save | data is mutated before durable persistence without clone/rollback semantics | Codex | **Open — #137** |
| R14 | High | Full local-profile deletion retains player data | previous and quarantine artifacts are not removed by `DeleteSave()` | Codex | **Open — #137** |
| R15 | High | PlayMode smoke can alter a developer profile | scene boots normal save services; test has no profile isolation/restoration | Codex | **Open — #127** |
| R16 | High | PlayMode smoke lacks deterministic cleanup and proof | no bounded load timeout, ServiceLocator cleanup, global-state restoration, or successful XML evidence | Codex | **Open — #127** |
| R17 | High | Android↔Unity embedding is not end-to-end | reflection host exists, but no Unity export is packaged, shell does not mount it, Unity consumer/result producer is unproven | GPT + Codex | **Deferred/open — #135 after NVS-01** |
| R18 | Medium | Android shell presents a parallel non-authoritative game state | Compose `remember` state is disconnected from Unity save/services | GPT + Codex | **Blocked — define boundary in #133; bridge later #135** |
| R19 | Medium | Quest route exists but is not reachable from current bottom navigation | `Route.Quest` entry exists; no navigation item selects it | Codex after product/spec decision | **Contained — do not patch before #128/#133 defines preview role** |
| R20 | Medium | `UnityView` is imported but unused by the shell | no mounted runtime host route | Codex | **Deferred — #135** |
| R21 | Medium | Sky Castle marker and Deploy action remain requested capabilities | current atlas has no approved NVS-01 location/result integration | Android Studio intent; GPT/Codex technical | **Blocked — #128/#133/#134** |
| R22 | Medium | Missing dialogue references can silently collapse UI progression | Android shell clears dialogue when lookup fails; Unity story trigger ignores missing nodes | GPT + Codex | **Blocked — strict validation/error behavior in #133/#134** |
| R23 | Medium | `end` is an untyped string convention | arbitrary `NextNodeId` can masquerade as terminal or missing target | Android Studio + GPT | **Open — #128 declaration, #133 validation** |
| R24 | Medium | Merged Chapter 1/hook/governance files can be treated as approved future content | files exist on `main` without phase review or user acceptance | GPT + Android Studio | **Contained — #129–#131 are not accepted runtime authority** |
| R25 | Medium | Unity export/device behavior is untested | PR #144 reports Android Gradle success but no packaged Unity device smoke | Codex | **Deferred — #135** |
| R26 | Low | KSP fix evidence is incomplete even though the pinned version is updated | `2.3.6` is present; no current recorded diagnostic scan/config-cache matrix in this audit | Codex | **Mitigated — reopen only if diagnostic reproduces** |
| R27 | Low | Documentation historically drifted behind GitHub/source state | old status still claimed clean A1 was active and archive unmerged | GPT | **Mitigated by current control PR** |

## Approved D1–D16 controls

Issue #138 remains authoritative for product experience. A1, G1, and runtime work must preserve:

- an authored deployment node before arena request;
- a transient, encouraging failure/retry loop;
- nonterminal recovery-only `FAILED`;
- Celestial Tear acquired exactly once on arena success;
- manual report to Valerius;
- 500 Gold, +5 affinity, quest completion, and selected-realm Chapter 1 unlock exactly once at report conclusion;
- complete localization-key inventory;
- honest classification of the Sky Castle marker/hook/results as requested until implemented;
- abandonment only outside an active encounter;
- universal post-realm eligibility for all four realms;
- Valerius as inter-realm Veil Watch liaison;
- retained Tear presented to Valerius and kept;
- quest offered rather than auto-accepted;
- exact-node dialogue resume and duplicate-safe encounter/report recovery.

These decisions close creative ambiguity. They do not prove an A1 packet, runtime contract, implementation, persistence, or playtest.

## Merge and execution order

### Gate 0 — build health

1. #145 captures the current compiler failure and restores a clean Unity compile.
2. Current-main EditMode evidence is recorded after the fix.
3. No downstream Unity completion claim is accepted before this gate.

### Independent foundations after Gate 0

- #127: safe, deterministic PlayMode smoke with profile preservation.
- #136: complete old-save default, mutation, and round-trip evidence.
- #137: starts only after #136 and remains a focused save PR.

### NVS-01 chain

1. #128: Android Studio produces one approved, internally valid A1 packet.
2. #133: GPT publishes G1 without changing narrative intent.
3. #134: Codex implements C1–C4 against approved A1/G1.
4. GPT performs G2.
5. Android Studio performs A2.
6. User performs U1.

### Post-NVS embedding

- #135 packages and mounts the Unity runtime, implements both sides of the route/result contract, and completes lifecycle/device validation.

## Shared-file risk

Potential shared integration files:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

Current soft-lock state: none.

Rules:

1. #145 should not need a designated shared file.
2. #128 may not edit shared runtime files.
3. #133 must justify every required shared-file impact.
4. The first approved implementation PR declaring a file holds the lock.
5. Save fields require defaults, migration, old-save tests, and duplicate-safety evidence.
6. Service conflicts preserve all valid registrations.
7. Generated artifacts must be deterministic and reviewable.

## Acceptance-evidence policy

Do not close a risk solely because a source file exists, a PR merged, or one platform compiled.

Required evidence must match the risk:

- build risk → exact compiler command, exit code, and log;
- test risk → discovered test totals and result artifact;
- save risk → normal, recovery, fault, deletion, and duplicate-safety matrix;
- contract risk → valid and invalid data tests plus producer/consumer proof;
- narrative risk → approved packet fidelity and reference validation;
- integration risk → actual route, lifecycle, result, and supported-device evidence;
- player-experience risk → integrated playtest.

## Immediate mitigation action

```text
Priority 1: #145
Owner: Codex
Expected branch: codex/fix-narrative-namespace-compile
Required outcome: trusted current-main Unity compile and EditMode baseline
```

After #145 is merged, this register must be updated with the exact validated head and newly unblocked work.