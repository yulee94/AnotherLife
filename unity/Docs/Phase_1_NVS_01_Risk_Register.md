# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-15  
**Audited current-main head:** `3c695ae289acabcfd8750bd6a2f0811ebdfb24cd`  
**Active control state:** #145 compilation recovery is complete; Phase 1 is paused behind #156 serialized-asset recovery  
**Approved product intent:** issue #138 D1–D16  
**Active narrative gate after recovery:** issue #128

This register describes verified current-source risk. It supersedes assumptions based solely on issue closure, PR merge state, source-file presence, compilation, or one-platform validation.

**Ownership update:** As of 2026-07-15, Android Studio is no longer a separate owner or narrative approval gate. Former Android Studio narrative/content responsibilities transfer to Codex narrative/content mode. Codex also owns design/asset workload, including terrestrial designs, under user approval.

Use with:

- `AGENTS.md`
- `unity/Docs/Phase_1_NVS_01_Status.md`
- `unity/Docs/Project_Progression_Roadmap.md`
- `unity/Docs/Three_Way_Collaboration_Plan.md`
- the focused issues named in the table below

## Severity and status

- **Critical:** build break, serialized-asset loss, profile/economy corruption, reward duplication, invalid milestone authority, or uncontrolled integration.
- **High:** incomplete/non-deterministic player path, persistence failure, bootstrap failure, packaging blocker, combat-state failure, or false player-visible completion.
- **Medium:** compatibility, diagnostics, accessibility, UX integration, governance, or reproducibility gap with a bounded workaround.
- **Low:** non-blocking build hygiene or quality debt.

Status values:

- **Open:** actionable now or after the named prerequisite.
- **Blocked:** cannot start until an upstream artifact or decision is approved.
- **Contained:** present but prevented from becoming authority or an active merge path.
- **Deferred:** intentionally scheduled after the current milestone.
- **Mitigated:** partially controlled; full acceptance evidence remains incomplete.
- **Closed:** all stated acceptance evidence is complete.

## Current risks

| ID | Severity | Risk | Current evidence | Owner | Tracking/status |
| --- | --- | --- | --- | --- | --- |
| R1 | Critical | Unity compilation was broken after the narrative namespace migration | `LocalStoryService` referenced removed `AL.Data.Definitions.DialogueChoice` | Codex + GPT | **Closed — #145 / PR #147 / main `3c695ae…`** |
| R2 | Critical | QuestDefinition serialized assets may reference a removed script GUID | old root GUID `2260…` and surviving narrative GUID `c385…` have not been fully inventoried | Codex + GPT | **Active — #156** |
| R3 | Critical | Merged archive can be mistaken for approved A1/G1/runtime completion | PR #124 said it must not merge; packet still conflicts with #138 | GPT + Android Studio | **Contained/blocked — #128 after #156** |
| R4 | Critical | Save rotation can overwrite the last-known-good backup with unvalidated primary bytes | current backup copy can occur before semantic validation | Codex | **Open — #137 after #136/#152** |
| R5 | Critical | Repository labels and merge state can falsely authorize downstream work | historical issues were closed without their required artifacts | GPT | **Mitigated — issues reopened; docs and titles corrected** |
| R6 | High | Unsafe PlayMode changes were mixed into the compile repair | original PR #147 edited ordinary startup-log expectations | GPT + Codex | **Closed/contained — edit reverted before merge; #127 remains** |
| R7 | High | No authoritative runtime narrative catalog exists | Android packet, Compose demo state, Unity fallback dialogue, and generic quest data remain separate | GPT | **Blocked — #133 after #128** |
| R8 | High | `OMEN_1` packet contradicts approved D1–D16 | wrong start context, dangling node, wrong consequence timing, automatic report, failure conflict, incomplete localization/resume | Android Studio | **Blocked — #128 after #156** |
| R9 | High | Unity does not register or execute `OMEN_1` | `LocalQuestService` registers Q1–Q5 only; no approved catalog/state machine | Codex | **Blocked — #134 after #133** |
| R10 | High | Encounter request/result contract is absent | no typed producer/consumer/session/correlation path for `HOOK_SKY_CASTLE_ARENA` | GPT + Codex | **Blocked — #133/#134** |
| R11 | High | NVS-01 persistence model is absent | no objective/dialogue/handoff/recovery/Tear/applied-ledger state | GPT + Codex | **Blocked — #133/#134** |
| R12 | Critical | Approved consequences have no atomic or duplicate-safe boundary | resource, affinity, quest, artifact, and chapter services save separately | GPT + Codex | **Blocked — #133/#134; foundations #137/#163/#176** |
| R13 | High | Old reputation/faction/persona data is only partially proven safe | defaults exist; real-service mutation and round trip are missing | Codex | **Ready — #136** |
| R14 | High | Null, blank, unknown, or duplicate quest states can crash or reward incorrectly | unguarded LINQ dereferences and unsafe definition indexing | Codex | **Ready — #152** |
| R15 | High | Save load/recovery and save status are conflated | one persistence message can lose recovery observability | Codex | **Open — #137** |
| R16 | High | Offline progress may duplicate or be lost after a failed repair save | data mutates before durable persistence without clone/rollback | Codex | **Open — #137** |
| R17 | High | Full profile deletion leaves player data | previous and quarantine artifacts are not fully removed | Codex | **Open — #137** |
| R18 | High | Save file model is internally inconsistent | code uses `save.json.previous`; approved model uses `save.previous.json` | Codex | **Open — #137; #127 protects both meanwhile** |
| R19 | High | Quarantine failure can continue toward unsafe profile recreation | move failure can be logged while load continues | Codex | **Open — #137** |
| R20 | High | PlayMode smoke can consume or alter a developer profile | scene starts normal save stack without isolation/restoration | Codex | **Ready — #127** |
| R21 | High | PlayMode test is non-deterministic and not self-cleaning | no bounded timeout, ServiceLocator cleanup, global-state restoration, or accepted XML | Codex | **Ready — #127** |
| R22 | High | Bootloader can mistake a partial registry for a complete service stack | only `IResourceService` is checked before initialization is skipped | Codex | **Ready — #153; Bootloader lock required** |
| R23 | High | Normal Unity Player build has no configured scenes | `EditorBuildSettings.asset` has `m_Scenes: []`; controllers load named scenes | Codex + GPT | **Blocked — #150 after #156** |
| R24 | High | Android↔Unity embedding is not end to end | no Unity export, mounted host route, Unity consumer/result producer, or device evidence | GPT + Codex | **Deferred — #135 after NVS-01/#150** |
| R25 | High | Same-route bridge retries can suppress all later outcomes | deduplication is keyed only by route string; no session identity | GPT + Codex | **Deferred — #135** |
| R26 | High | `main` has no automated repository/Android/Unity merge gate | no required workflow/status checks or reliable Unity runner | GPT + Codex | **Open — #155** |
| R27 | Medium | Android `Quest` positional constructor compatibility regressed | archive reordered metadata before legacy Boolean slots | Codex | **Open — #148 independent** |
| R28 | Medium | Android dependency resolution is non-reproducible | version catalog contains `+` versions | Codex | **Open — #159** |
| R29 | Medium | Release Android shell exposes narrative debug routing and arbitrary node triggers | Debug route is not build-flavor gated | Codex + Android Studio | **Open — #161** |
| R30 | Medium | Android shell is a parallel non-authoritative game state | Compose state is disconnected from Unity save/services | GPT + Codex | **Blocked — boundary in #133; bridge later #135** |
| R31 | High | Android quest preview can show invalid progress and false Start/Claim actions | division by zero/negative targets, unsupported starts, hard-coded no-op 500 Gold claim | Codex + GPT | **Blocked — #186 after #128/#133** |
| R32 | Medium | Missing dialogue references silently collapse preview/runtime progression | Android closes overlay; Unity ignores unknown node | GPT + Codex | **Blocked — #128/#133/#134** |
| R33 | Medium | `end` is an untyped string convention | arbitrary target can masquerade as terminal or missing reference | Android Studio + GPT | **Open — #128 declaration, #133 validation** |
| R34 | Critical | Resource and Warzone Credit methods accept unsafe signed/overflow operations | negative consume/spend can add value; additions can wrap; malformed wallet entries can throw | Codex | **Open — #163** |
| R35 | High | Building, research, and troop state can be malformed or mutated through unsafe requests | negative/overflow levels/counts, query-time state creation, unvalidated IDs | Codex | **Blocked/open — #165 after #163** |
| R36 | High | Territory recapture can farm quest progress and credits | same-owner capture repeats reward path; passive income trusts malformed data | Codex | **Blocked/open — #166 after #163** |
| R37 | Critical | Boss-loot computation and application can duplicate or partially commit rewards | invalid requests fabricate fallback loot; no result identity; credits save before equipment | Codex | **Blocked/open — #168 after #163 and persistence design** |
| R38 | High | Realm Gem custody and Wishgate entitlement can enter contradictory or lossy state | independent flags, blind empty-list seeding, reward selection consumes entitlement without transaction | Codex + Android Studio | **Open — #169 after persistence design** |
| R39 | High | Warmaster purchases can charge without durable entitlement or unlock from duplicate IDs | caller-supplied prices, separate credit/state saves, raw list-count thresholds | Codex | **Blocked/open — #171 after #163/persistence** |
| R40 | High | World-state events are in-memory, non-expiring, and a hard-coded narrative authority | duration unused; no save/resume; technical service owns player copy | Codex + Android Studio | **Open — #172 after persistence contract** |
| R41 | High | Realm identity can be invalid or overwritten without migration policy | `None`/undefined realms accepted; existing profile is overwritten directly | Codex + GPT | **Open — #173 after persistence contract** |
| R42 | Critical | Strategic battle simulation accepts invalid armies and mutates quest progress | null/empty request can yield a winner; simulation has side effects and no result identity | Codex | **Open — #174 after relevant state/economy contracts** |
| R43 | High | Relationship mutations can accept malformed IDs/non-finite data and force nested saves | affinity/faction/persona services lack validation, overflow/idempotency, and transaction seam | Codex + Android Studio | **Blocked/open — #176 after #136/persistence seam** |
| R44 | High | Player notifications are only Console logs | no visible queue, typed severity, localization, deduplication, accessibility, or delivery result | Codex + Android Studio | **Open — #177** |
| R45 | Critical | Production Kingdom UI exposes direct grants, test mutations, and one-click reset | unlimited credits, fixed gem/wish actions, mutating war drill, unconfirmed delete | Codex | **Open — #178** |
| R46 | High | Champion/boss/skill state accepts non-finite values and lacks one encounter lifecycle | NaN can poison health/mana; partial catalog silently falls back; Crownlands is substituted | Codex | **Blocked/open — #180 after #173 and related foundations** |
| R47 | High | World atlas is mutable, unversioned, weakly validated, and a parallel narrative authority | fallback service hard-codes zones/objectives/text/rewards and exposes backing objects | Codex + Android Studio | **Blocked/open — #181 after #156/#173** |
| R48 | Critical | Game-data authority is incomplete and conflicting | troop/champion/skill lookups return null; runtime ScriptableObjects are mutable; duplicates overwrite; narrative copy is hard-coded | Codex + Android Studio | **Blocked/open — #183 after #156** |
| R49 | High | Customization can persist invalid/future IDs before authoritative catalog load | save-backed state mutates before validation; async fallback normalization can destroy future IDs | Codex + Android Studio | **Blocked/open — #184 after #137/#183** |
| R50 | Medium | Visual prototype branches could be mistaken for active implementation | stacked drafts inherited closed/rejected ancestors and compile-only evidence | GPT + Codex | **Contained — PRs #149/#154/#157/#162/#164/#167/#170/#175/#179/#182/#185 closed; #151/#160/#187 not planned** |
| R51 | Medium | Current chapter helpers are not progression authority | chapter objects are instantiated/discarded; `AdvanceStory` does not persist chapter mutation | GPT + Codex | **Blocked — #133/#134/#183** |
| R52 | Medium | Merged Chapter 1/hook/governance files can be treated as approved content | files exist without phase review or user acceptance | GPT + Android Studio | **Contained — #129–#131 not planned for active phase** |
| R53 | Low | KSP fix evidence is incomplete although `2.3.6` is present | current Android builds pass; original diagnostic has not reappeared in accepted evidence | Codex | **Mitigated — reopen #126 only on reproduction** |
| R54 | Low | Documentation historically drifted behind source and GitHub state | prior records lagged merges and blockers | GPT | **Mitigated by PRs #146/#158 and this update** |

## Approved D1–D16 controls

Issue #138 remains authoritative for the product experience. A1, G1, and runtime work must preserve:

- an authored deployment node before arena request;
- a transient, encouraging failure/retry loop;
- nonterminal recovery-only `FAILED`;
- Celestial Tear acquired exactly once on arena success;
- manual report to Valerius;
- 500 Gold, +5 affinity, quest completion, and selected-realm Chapter 1 unlock exactly once at report conclusion;
- complete localization-key inventory;
- honest requested-capability classification for Sky Castle marker/hook/results;
- abandonment only outside an active encounter;
- universal post-realm eligibility for all four realms;
- Valerius as inter-realm Veil Watch liaison;
- retained Tear presented and kept;
- quest offered rather than auto-accepted;
- exact-node dialogue resume and duplicate-safe encounter/report recovery.

These decisions close creative ambiguity only. They do not prove A1, G1, runtime implementation, persistence, integration, or playtest.

## Recovery and execution order

### Phase 0 source and asset health

1. #145 compile repair is complete at `3c695ae289acabcfd8750bd6a2f0811ebdfb24cd`.
2. #156 now inventories and resolves QuestDefinition authority/GUID references.
3. Current `main` must then be recompiled and reimported with no missing-script evidence.

### Parallel foundations available now

- #127 — safe PlayMode smoke with profile preservation;
- #136 — real-service relationship-field normalization and round trip;
- #152 — malformed/unknown/duplicate quest-state compatibility;
- #153 — coherent full service-stack readiness;
- #148 — independent Android constructor compatibility;
- #155 — repository/Android checks and staged Unity validation;
- #159 — Android dependency reproducibility;
- #161 — Android debug-route gating.

Parallel work must still respect file overlap and shared-file locks. One broad backlog PR is prohibited.

### Save safety

- #137 begins after #136 and #152.
- Economy, relationship, realm, reward, and encounter work that requires atomic composition must use the resulting persistence/transaction boundary rather than adding independent nested saves.

### Source/build readiness after #156

- #128 produces the clean approved A1 packet.
- #150 inventories production scenes and proves a Player build without shipping `Assets/Test.unity`.
- #183 and #181 may proceed only through an approved authority/schema plan and declared shared-file locks.

### NVS-01 chain

1. #128 approved A1.
2. #133 GPT G1 specification.
3. G1 identifies the exact focused technical foundations required for OMEN_1.
4. #134 Codex C1–C4 after those foundations and G1 are approved.
5. GPT G2.
6. Codex narrative/content A2.
7. User U1.

### Post-NVS embedding

- #135 packages and mounts Unity, implements both route/result sides with session correlation, and completes lifecycle/device validation after #150.

## Shared-file risk

Designated shared files:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

Current soft-lock state: **none**.

Rules:

1. #156 must not broaden into unrelated shared-file edits.
2. #153 requires an explicit `Bootloader.cs` lock.
3. #128 may not edit runtime shared files.
4. #133 must justify every required shared-file impact.
5. The first approved open implementation PR declaring a file holds the lock.
6. Save fields require defaults, migration, old-save tests, and duplicate-safety evidence.
7. Service conflicts preserve valid registrations and coherent root dependencies.
8. Generated artifacts and migrations must be deterministic and reviewable.
9. Closed or stacked speculative branches do not reserve shared files.

## Acceptance-evidence policy

Do not close a risk solely because a source file exists, a PR merged, or one platform compiled.

Required evidence must match the risk:

- source build risk → exact compiler command, exit code, and complete error scan;
- serialized asset risk → GUID/reference inventory, reimport, missing-script scan, and field preservation;
- test risk → discovered totals and retained XML/log artifacts;
- save/economy/reward risk → normal, recovery, fault, deletion, semantic, overflow, and duplicate-safety matrices;
- contract risk → valid/invalid data tests and implemented producer/consumer proof;
- packaging risk → actual Player/export build and launch/transition evidence;
- narrative risk → approved packet fidelity, IDs, reachability, and localization validation;
- integration risk → actual route, lifecycle, result, session, and supported-device evidence;
- player-experience risk → integrated playtest.

Skipped, unavailable, compile-only, or `continue-on-error` checks are not passing evidence.

## Immediate mitigation action

```text
Priority 1: #156
Base: current main 3c695ae289acabcfd8750bd6a2f0811ebdfb24cd
Owner: Codex
First evidence: complete old/new QuestDefinition GUID, asset, source, generator, schema, and catalog inventory
Decision gate: GPT approves the final technical authority and migration plan before the PR is marked ready
```

No open pull request currently implements #156 or any parallel foundation. The repository is intentionally clear of stacked visual drafts while this recovery gate proceeds.
