# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-15  
**Audited current-main head:** `55128d21a6dbf9402eb78396dbe59f8d7e4bcac9`  
**Active control state:** Phase 1 paused behind #145 compilation and #156 serialized-asset recovery  
**Approved product intent:** issue #138 D1–D16  
**Active narrative gate after recovery:** issue #128

This register describes verified current-source risk. It supersedes assumptions based solely on issue closure, PR merge state, source-file presence, or one-platform validation.

Use with:

- `AGENTS.md`
- `unity/Docs/Phase_1_NVS_01_Status.md`
- `unity/Docs/Project_Progression_Roadmap.md`
- `unity/Docs/Three_Way_Collaboration_Plan.md`
- active issues #127, #128, #133–#137, #145, #148, #150, #152, #153, #155, and #156

## Severity and status

- **Critical:** build break, serialized-asset loss, profile corruption, invalid milestone authority, or uncontrolled integration.
- **High:** incomplete/non-deterministic player path, persistence failure, bootstrap failure, packaging blocker, or false completion.
- **Medium:** compatibility, diagnostics, UX integration, governance, or reproducibility gap with a bounded workaround.
- **Low:** non-blocking build hygiene or quality debt.

Status values:

- **Open:** actionable now or after the named prerequisite.
- **Blocked:** cannot start until an upstream artifact is approved.
- **Contained:** present but prevented from becoming authority or an active merge path.
- **Deferred:** intentionally scheduled after the current milestone.
- **Mitigated:** partially controlled; full acceptance evidence remains incomplete.
- **Closed:** all stated acceptance evidence is complete.

## Current risks

| ID | Severity | Risk | Current evidence | Owner | Tracking/status |
| --- | --- | --- | --- | --- | --- |
| R1 | Critical | Unity compilation is broken on current `main` after namespace migration | `DialogueChoice` lives in `AL.Data.Definitions.Narrative`; `LocalStoryService` constructs removed `AL.Data.Definitions.DialogueChoice` | Codex | **Open — #145 / draft PR #147** |
| R2 | Critical | QuestDefinition serialized assets may reference a removed script GUID | pre-PR #124 root GUID `2260…` was removed; narrative GUID `c385…` remains; both populations are not inventoried | Codex + GPT | **Open — #156 after #145** |
| R3 | Critical | Merged archive can be mistaken for approved A1/G1/runtime completion | PR #124 said “must not be merged” yet merged; packet still conflicts with #138 | GPT + Android Studio | **Contained/open — #128 after #145/#156** |
| R4 | Critical | Save rotation can overwrite last-known-good backup with unvalidated primary bytes | `File.Copy(SavePath, BackupPath, true)` occurs before primary validation | Codex | **Open — #137 after #136/#152** |
| R5 | Critical | Repository state labels can falsely authorize downstream work | #128/#133/#134 had been closed without their artifacts | GPT | **Mitigated — issues reopened, titles/docs corrected** |
| R6 | High | Current critical PR mixes an unrelated unsafe PlayMode change | PR #147 edits exact startup-log expectations in #127 test | Codex + GPT | **Contained — PR stays draft until one-file diff** |
| R7 | High | No authoritative runtime narrative catalog exists | Android packet, Compose demo state, Unity fallback dialogue, and generic quest definitions are separate authorities | GPT | **Blocked — #133 after #128** |
| R8 | High | `OMEN_1` packet contradicts D1–D16 | wrong start context, dangling node, wrong consequence timing, automatic report, failure conflict, incomplete localization/resume | Android Studio | **Blocked/open — #128 after #145/#156** |
| R9 | High | Unity does not register or execute `OMEN_1` | `LocalQuestService` registers Q1–Q5 only; no catalog loader/state machine | Codex | **Blocked — #134 after #133** |
| R10 | High | Encounter request/result contract is absent | no typed producer/consumer/session/correlation path for `HOOK_SKY_CASTLE_ARENA` | GPT + Codex | **Blocked — #133/#134** |
| R11 | High | NVS-01 persistence model is absent | no objective/dialogue/handoff/recovery/Tear/ledger state | GPT + Codex | **Blocked — #133/#134** |
| R12 | High | Approved consequences have no atomic or duplicate-safe boundary | resource, affinity, quest, artifact, and chapter services save separately | GPT + Codex | **Blocked — #133/#134; depends on #137** |
| R13 | High | Old reputation/faction/persona data is only partially proven safe | defaults exist; real-service mutation, idempotency, and round trip are missing | Codex | **Open — #136 after #145** |
| R14 | High | Null, blank, unknown, or duplicate quest states can crash or reward incorrectly | unguarded LINQ dereferences; unsafe `_definitions[questId]`; side-quest `StartsWith` on nullable ID | Codex | **Open — #152 after #145** |
| R15 | High | Save load/recovery and save status are conflated | one persistence message; internal save overwrites recovery observability; interface exposes neither result model | Codex | **Open — #137** |
| R16 | High | Offline progress may duplicate or be lost after a failed repair save | current data mutates before durable persistence without clone/rollback | Codex | **Open — #137** |
| R17 | High | Full profile deletion leaves player data | previous and quarantine artifacts are not removed; delete failures are not authoritative | Codex | **Open — #137** |
| R18 | High | Save file model is internally inconsistent | current fallback creates `save.json.previous`; approved model uses `save.previous.json` | Codex | **Open — #137; #127 protects both meanwhile** |
| R19 | High | Quarantine failure can continue toward unsafe profile recreation | move failure logs but load path can continue | Codex | **Open — #137** |
| R20 | High | PlayMode smoke can consume or alter developer profile | scene starts normal save stack with no snapshot/isolation/restoration | Codex | **Open — #127 after #145** |
| R21 | High | PlayMode test is non-deterministic and not self-cleaning | no bounded timeout, ServiceLocator cleanup, global-state restoration, artifact verification, or accepted XML | Codex | **Open — #127** |
| R22 | High | Bootloader can mistake a partial service registry for a complete stack | only `IResourceService` is checked before initialization is skipped | Codex | **Open — #153 after #145; Bootloader lock required** |
| R23 | High | Normal Unity Player build has no configured scenes | `EditorBuildSettings.asset` has `m_Scenes: []`; controllers load named scenes | Codex + GPT | **Open — #150 after #145/#156** |
| R24 | High | Android↔Unity embedding is not end-to-end | no Unity export, no mounted host route, no Unity consumer/result producer or device evidence | GPT + Codex | **Deferred/open — #135 after NVS-01/#150** |
| R25 | High | Same-route bridge retries can permanently suppress outcomes | deduplication is keyed only by `routeTag`; no session/correlation ID | GPT + Codex | **Deferred/open — #135** |
| R26 | High | `main` has no automated repository/Android/Unity merge gate | no workflow/status checks; unsafe mixed PRs could be merged | GPT + Codex | **Open — #155 after #145** |
| R27 | Medium | Android `Quest` positional constructor compatibility regressed | PR #124 moved metadata before legacy Boolean slots | Codex | **Open — #148 independent Android fix** |
| R28 | Medium | Android shell is a parallel non-authoritative game state | Compose `remember` state is disconnected from Unity save/services | GPT + Codex | **Blocked — boundary in #133; bridge later #135** |
| R29 | Medium | Quest route exists but is unreachable from current bottom navigation | entry-provider case exists; no navigation item selects it | GPT + Codex | **Contained — do not patch before preview/runtime boundary** |
| R30 | Medium | Missing dialogue references silently collapse preview progression | Android closes overlay; Unity trigger ignores unknown node | GPT + Codex | **Blocked — #128 validation, #133/#134 errors** |
| R31 | Medium | `end` is an untyped string convention | arbitrary target can masquerade as terminal or missing reference | Android Studio + GPT | **Open — #128 declaration, #133 validation** |
| R32 | Medium | Current chapter helpers are not progression authority | `AddChapter` discards objects; `AdvanceStory` emits without mutating saved chapter | GPT + Codex | **Blocked — #133/#134** |
| R33 | Medium | Merged Chapter 1/hook/governance files can be treated as approved | files exist without phase review or user acceptance | GPT + Android Studio | **Contained — #129–#131 not planned for active phase** |
| R34 | Medium | Tiered loot/VFX prototype could acquire shared locks out of sequence | PR #149 stacked on blocked PR, touched save/project initializer, mixed multiple concerns | GPT + Codex | **Contained — PR #149 closed; #151 deferred/not planned** |
| R35 | Medium | Unity export/device behavior is untested | reflection host compiles without packaged runtime | Codex | **Deferred — #135 after #150** |
| R36 | Medium | Dynamic or unprotected build inputs can reduce reproducibility | no CI lock/gate currently proves dependency and metadata stability | Codex | **Open — address through #155 and focused follow-ups** |
| R37 | Low | KSP fix evidence is incomplete although `2.3.6` is present | current Android builds pass; original diagnostic has not reappeared in accepted evidence | Codex | **Mitigated — reopen #126 only on reproduction** |
| R38 | Low | Documentation historically drifted behind source/GitHub state | prior status claimed archive unmerged and gates complete | GPT | **Mitigated by PR #146 and this update** |

## Approved D1–D16 controls

Issue #138 remains authoritative for product experience. A1, G1, and runtime work must preserve:

- an authored deployment node before arena request;
- a transient, encouraging failure/retry loop;
- nonterminal recovery-only `FAILED`;
- Celestial Tear acquired exactly once on arena success;
- manual report to Valerius;
- 500 Gold, +5 affinity, quest completion, and selected-realm Chapter 1 unlock exactly once at report conclusion;
- complete localization-key inventory;
- honest requested-capability classification for Sky Castle marker/hook/results;
- abandonment only outside active encounter;
- universal post-realm eligibility for all four realms;
- Valerius as inter-realm Veil Watch liaison;
- retained Tear presented and kept;
- quest offered rather than auto-accepted;
- exact-node dialogue resume and duplicate-safe encounter/report recovery.

These decisions close creative ambiguity only. They do not prove A1, G1, runtime implementation, persistence, integration, or playtest.

## Recovery and execution order

### Phase 0 source and asset health

1. #145 merges only the `LocalStoryService.cs` compile repair.
2. #156 inventories and migrates QuestDefinition authority/GUID references.
3. Current `main` is recompiled and reimported with no missing-script evidence.

### Parallel foundations after #145

- #127: safe PlayMode smoke with profile preservation.
- #136: real-service narrative-field mutation and save/reload.
- #152: malformed/unknown/duplicate quest-state compatibility.
- #153: coherent full service-stack readiness.
- #155: repository/Android checks and staged Unity validation.
- #148: independent Android constructor compatibility.

### Save safety

- #137 begins after #136 and #152 so semantic validation and data-preservation tests include current quest/narrative state rules.

### Source/build readiness after #156

- #150 inventories production scenes and proves a Player build without shipping `Assets/Test.unity`.
- #128 produces the clean approved A1 packet.

### NVS-01 chain

1. #128 approved A1.
2. #133 GPT G1 specification.
3. #134 Codex C1–C4 only after G1 and required foundations (#127, #136, #137, #152, #153).
4. GPT G2.
5. Android Studio A2.
6. User U1.

### Post-NVS embedding

- #135 packages/mounts Unity, implements both route/result sides with session correlation, and completes lifecycle/device validation after #150.

## Shared-file risk

Designated shared files:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

Current soft-lock state: none.

Rules:

1. #145 and #156 should not broaden into unrelated shared-file edits.
2. #153 requires an explicit Bootloader lock.
3. #128 may not edit runtime shared files.
4. #133 must justify every required shared-file impact.
5. The first approved open implementation PR declaring one holds the lock.
6. Save fields require defaults, migration, old-save tests, and duplicate-safety evidence.
7. Service conflicts preserve valid registrations and coherent root dependencies.
8. Generated artifacts and migrations must be deterministic and reviewable.
9. Stacked speculative PRs do not reserve shared files merely by existing; unapproved paths are closed/deferred.

## Acceptance-evidence policy

Do not close a risk solely because a source file exists, a PR merged, or one platform compiled.

Required evidence must match the risk:

- source build risk → exact compiler command, exit code, and complete error scan;
- serialized asset risk → GUID/reference inventory, reimport, missing-script scan, and field preservation;
- test risk → discovered totals and retained XML/log artifacts;
- save risk → normal, recovery, fault, deletion, semantic, and duplicate-safety matrix;
- contract risk → valid/invalid data tests and implemented producer/consumer proof;
- packaging risk → actual Player/export build and launch/transition evidence;
- narrative risk → approved packet fidelity, IDs, reachability, and localization validation;
- integration risk → actual route, lifecycle, result, session, and supported-device evidence;
- player-experience risk → integrated playtest.

Skipped, unavailable, or `continue-on-error` checks are not passing evidence.

## Immediate mitigation action

```text
Priority 1: revise draft PR #147
Required diff: LocalStoryService.cs only
Required links: Fixes #145; Refs #156
Required evidence: pre-fix CS0234, post-fix Unity compile, EditMode 6/6
Prohibited: RepresentativeSceneSmokeTests.cs changes
```

After #145 merges, #156 becomes the immediate Unity recovery owner. The register must then record the validated head and exact unblocked lanes.