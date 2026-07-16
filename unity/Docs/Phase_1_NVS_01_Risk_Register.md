# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-16  
**Audited current-main head:** `371cc019c7a4526b8b20c145104c994d5c49a056`  
**Active control state:** Phase 1 is paused behind reopened #156 and the red Phase 0/1 foundation gate  
**Approved product intent:** issue #138 D1–D16  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

This register records verified current-source and delivery risk. It supersedes assumptions based only on automatic issue closure, merged PR state, branch descriptions, passing tests that encode the wrong policy, source presence, generated files, compilation, workflow success, LFS pointers, or one-platform evidence.

## 1. Current risks

| ID | Severity | Risk | Current evidence | Owner | Tracking/status |
| --- | --- | --- | --- | --- | --- |
| R1 | Critical | QuestDefinition authority can miss malformed or non-importable serialized assets | merged PR #189 still uses `AssetDatabase.FindAssets("t:QuestDefinition")`, main-asset loading, blank-ID skipping, and no Force-Text `m_Script`/subasset/schema fixture matrix | Codex engineering + GPT | **Active — #156 reopened; PR #218 contract not implemented** |
| R2 | Critical | Merged or automatically closed work may be mistaken for accepted behavior | post-merge audit found blocking defects in PRs #189/#203/#214/#211/#212/#209/#208/#195/#210/#231; issue #155 was closed again by partial PR #232 despite its own incomplete disposition | GPT + Codex engineering + maintainer | **Active control — reopened issues and current-main review required** |
| R3 | Critical | Archived OMEN_1 may become a false narrative/runtime authority | archive conflicts with approved offer, deployment, failure, Tear, report, atomic consequence, abandonment, localization, and resume decisions | Codex narrative/content + GPT | **Contained — #128 starts only after #156** |
| R4 | Critical | NVS-01 consequences still lack one durable atomic/idempotent transaction | Gold, affinity, quest completion, artifact/Tear, chapter unlock, encounter result, ledger, and notification domains are not composed through one accepted candidate transaction | GPT + Codex engineering | **Blocked — #133/#134 after foundations** |
| R5 | Critical | Save validation can destroy corruption evidence or fabricate a valid-looking profile | merged PR #231 deletes malformed rows/strings, creates objects, seeds resource balances, defaults chapter, and validates only top-level non-null state | Codex engineering + GPT | **Active — #137 reopened; correction required** |
| R6 | Critical | Save recovery can discard the only valid generation | startup deletes temp unconditionally and deletes previous whenever primary merely exists; no primary/backup/previous/temp candidate ranking | Codex engineering | **Active — #137 reopened** |
| R7 | Critical | Save disk and memory can diverge while status claims previous data was preserved | final backup/copy/verification or rollback failure can leave a new disk primary with old in-memory state; rollback/delete failures are swallowed | Codex engineering | **Active — #137 reopened; commit-uncertain state required** |
| R8 | Critical | Full profile deletion can be falsely reported | `DeleteSave()` ignores deletion failures, clears current state, and logs deletion even if files/quarantines remain | Codex engineering | **Active — #137 reopened; typed post-verification required** |
| R9 | Critical | Quest compatibility destroys or activates ambiguous save state | merged PR #212 removes null/blank rows, keeps first duplicate, seeds Q1–Q5, exposes unsupported side quests, and can save rejected/no-change paths | Codex engineering + GPT | **Active — #152 reopened** |
| R10 | High | Relationship old-save compatibility is unproven | merged PR #211 manually nulls new objects but has no omitted-field JSON fixture, repeated normalization, unrelated-field preservation, or serialize/reload idempotency | Codex engineering + GPT | **Active — #136 reopened** |
| R11 | Critical | Economy reads and wrappers can mutate, fabricate, or ambiguously combine value | merged PR #214 repairs negative/null state, sums duplicates, clamps overflow, saves through wrappers, and lacks accepted typed no-save candidate primitives | Codex engineering + GPT | **Active — #163 reopened** |
| R12 | Critical | Offline progress can operate on invalid economy/progression state | merged save code adds to first resource match with unchecked arithmetic, creates missing rows, increments timers without catalog/max-level validation, and uses direct wall clock | Codex engineering + GPT | **Blocked — #137 depends corrected #163/#165 semantics** |
| R13 | Critical | Bootloader stack publication/load/save lifecycle is not transaction-safe | load token commits before `Load`; save can target a replacement; final verification cannot rollback; malformed marker access can throw; marker/type inventories remain mutable | Codex engineering + GPT | **Active — #153 reopened** |
| R14 | Critical | Service lifecycle owner disappears on normal scene transition | intended Boot scene is the only owner; no approved persistent or marker-safe per-scene component maintains tick/pause/quit/drift behavior after `LoadScene` | Codex engineering + GPT | **Active — #153; blocks #223/#150** |
| R15 | High | PlayMode tests can restore a developer profile while scene callbacks remain alive | merged PR #209 restores files after deferred destroys, never unloads the representative scene, and helper teardown can restore uncaptured `Time.timeScale = 0` | Codex engineering | **Active — #127 reopened** |
| R16 | Critical | Production controller paths still mutate saves and economy invisibly | Kingdom Start loads, Update completes building/research, dashboard reads seed state, and Champion Arena grants recurring proximity credits | Codex engineering | **Active — #178 reopened** |
| R17 | High | Android release fallback may be technically correct but invisible | the first sanitization sets a notice, then the sanitized-stack effect pass immediately clears it; no stable Compose/reducer proof exists | Codex engineering | **Active — #161 reopened** |
| R18 | High | Green repository workflow can hide policy gaps | PR #232 added useful local failure fixtures and a proof/protection plan, but machine-readable policy is still unused; diff fallback is open; exact ranges, classifier/security/artifact coverage, live failures, Unity model, and protection remain incomplete | Codex engineering + GPT + maintainer | **Active — #155 reopened; PR #232 accepted partial only** |
| R19 | High | Duplicate-workspace Unity evidence may be cited as canonical | recent merged and open work reports `C:\Users\MY\Documents\AnotherLife\unity`; canonical workspace is `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity` | Codex engineering + GPT | **Active — canonical reruns required** |
| R20 | High | Production scenes have no committed authority | only `Assets/Test.unity` is committed; Build Settings are empty; generator intent is destructive and unvalidated | Codex engineering + GPT | **Active — #223 after #156/#153** |
| R21 | Critical | Scene generator can overwrite authored assets and packaging policy | current generator recreates four scenes and replaces all Build Settings without dry run, stable GUID preservation, rollback, drift validator, or idempotency proof | Codex engineering + GPT | **Active — #223** |
| R22 | High | Player build artifacts can be accepted without trustworthy launch evidence | stale output, wrong scene list, missing BuildReport, developer-profile launch, severe logs, or external kill before ordered markers can look successful | Codex engineering + GPT | **Controlled by PR #224 contract; implementation pending #150** |
| R23 | High | Game-data definitions remain mutable, nullable, incomplete, and silently fallback-prone | current service creates runtime ScriptableObjects, exposes live values, discards story objects, omits IDs/research query, and returns null for troop/champion/skill | Codex engineering + source modes + GPT | **Active — #183 after #156** |
| R24 | Critical | Progression may invent or consume unauthoritative definitions | Kingdom references absent `ManaShrine`/`Mine`; research uses display strings; troop definitions are absent; current saves/services create state on reads | Codex engineering + GPT | **Active — #165/#183; fail closed only** |
| R25 | Critical | Battle simulation can turn invalid input into a rewarded result and mutate progression | current simulator defaults null/lists/realm/seed, permits empty or malformed armies, reads global research state, uses process-runtime randomness, labels proposed rewards as earned, and directly increments WinBattle quest | Codex engineering + GPT | **Active — #174; binding PR #234 contract merged, pure phase allowed** |
| R26 | Critical | Boss rewards can be fabricated, duplicated, or partially committed | current service grants fallback item, saves credits before equipment, uses process-local randomness, exposes mutable inventory, and boss catch grants synthetic/double value | Codex engineering + GPT | **Active — #168; binding PR #230 contract merged, pure phase allowed** |
| R27 | High | Notification success can be inferred from Console output | current service accepts raw strings and logs only; no queue, typed receipt, visible presenter, acknowledgement, dedupe, or durable outbox | Codex engineering + narrative/content | **Active — #177; pure contract/queue phase allowed** |
| R28 | High | Relationship mutations can crash, overflow, save independently, and misclassify malformed state | current services select first duplicates, create arbitrary IDs, accept nonfinite/unchecked values, hard-code labels, and cannot compose NVS affinity atomically | Codex engineering + narrative/content + GPT | **Active — #176; pure planner allowed, production blocked** |
| R29 | High | World-state events can announce effects that are neither persistent nor consumed | current service holds raw enum/string/float state, uses hard-coded copy, no accepted effect consumers, no ledger, and no atomic persistence | Codex engineering + narrative/content + GPT | **Active — #172; pure planner allowed, production blocked** |
| R30 | High | Terrestrial preview source may be mistaken for approved creative/runtime authority | PR #217 has three base sheets for nine variants, incomplete normalized media identity/validation, no direct exact-source review, and no user approval | Codex terrestrial-design + GPT + user | **Active — #194 / PR #217 blocked** |
| R31 | High | LFS pointer/prose may substitute for actual pixel review | repository review surfaces may expose pointers while exact full-resolution binary pixels tied to hashes remain unavailable | Codex terrestrial-design + GPT + user | **Active — PR #221 contract requires exact render/retrieval** |
| R32 | High | Realm identity can be invalid or overwritten without accepted persistence/catalog support | `None`, undefined values, missing definitions, and existing-profile replacement require one-time durable selection semantics | Codex engineering + GPT | **Specification merged PR #202; #173 pending #137/#183** |
| R33 | Critical | Territory, Realm Gem, Wishgate, Warmaster, and progression rewards remain exploitable or ambiguously persisted | repeated capture, unsafe entitlements, nested saves, hard-coded IDs/costs, and missing committed result identities remain | Codex engineering + GPT | **Blocked/open — #166/#169/#171/#165** |
| R34 | High | Android↔Unity integration remains unproven | no packaged Unity export, mounted host, request/result/session identity, lifecycle, or device evidence | Codex engineering + GPT | **Deferred — #135 after standalone Player path** |
| R35 | Medium | Status/metadata can drift during concurrent merges | ten implementations merged and auto-closed issues while blocking source defects remained; PR #231 merged during active review; partial PR #232 reclosed #155 | GPT + maintainer | **Mitigated by current-main post-merge audits and correction status refreshes** |
| R36 | Critical | Ownership chronology could be inverted again | an earlier Gemini/Android Studio instruction was previously treated as newer than the final Codex reassignment | GPT | **Resolved/controlled — PR #205 + dated decision record** |
| R37 | Critical | Champion combat state and encounter completion can be poisoned, duplicated, or mistaken for durable success | health/mana/boss values accept invalid floats; skills are hybrid/slot-authoritative; realm falls back to Crownlands; AI/targeting use scene scans; boss death performs rewards; Arena uses booleans/object-null/mutable callbacks and direct credits as encounter/result authority | Codex engineering + source modes + GPT | **Active — #180; binding PR #235 contract merged, pure phase allowed** |

## 2. D1–D16 controls

A1, G1, and runtime must preserve:

- authored deployment node before arena request;
- transient encouraging failure/retry and nonterminal `FAILED`;
- Tear acquired once on arena success;
- manual report to Valerius;
- 500 Gold, +5 Valerius affinity, quest completion, retained artifact state, selected-realm Chapter 1 unlock, encounter result, ledger, and notification exactly once at report conclusion;
- complete localization inventory;
- honest requested-capability classification;
- abandonment only outside an active encounter;
- universal post-realm eligibility and Veil Watch Valerius;
- offered rather than auto-accepted start;
- exact dialogue-node resume and duplicate-safe encounter/report recovery.

## 3. Dependency controls

### 3.1 Trusted Unity and NVS

```text
#156 complete Force-Text QuestDefinition authority
  ↓
#128 clean Codex narrative/content A1
  ↓
#133 GPT G1
  ↓
accepted save/economy/notification/relationship/scene/battle/encounter/result foundations
  ↓
#134 Codex engineering C1–C4
  ↓
G2 GPT → A2 Codex narrative/content → U1 user
```

### 3.2 Save and economy

```text
#136 real omitted-JSON normalization evidence
          +
#152 non-mutating quest compatibility
          +
#163 typed non-repairing economy/no-save primitives
          ↓
#137 candidate ranking, rollback certainty, explicit repair, verified deletion, crash-safe persistence
```

Controls:

- preserve stable unknown data;
- preserve malformed/duplicate evidence;
- disable ambiguous domains;
- never take first/max/sum/clamp as silent repair;
- prefer the cleanest validated candidate;
- retain rollback through final verification;
- use clone → validate → persist/verify → publish;
- do not apply offline progress to an invalid candidate;
- return commit-uncertain rather than guessing durability.

### 3.3 Scenes and Player

```text
#156 trusted assets + #153 accepted cross-scene lifecycle
          ↓
#223 stable non-destructive production scene assets
          +
#178 non-mutating ShellFoundation controllers
          +
#127 lifecycle-safe profile-isolated PlayMode
          ↓
#150 exact three-scene Windows64 Development Player and disposable launch smoke
          ↓
#135 Android export/host
```

### 3.4 Game data and progression

```text
#156
  ↓
#183 immutable versioned catalog foundation/source/service migration
  ↓
#173 realm + #165 progression + #166 territory + #169 gems/wish + #171 Warmaster
```

### 3.5 Battle result integrity

```text
pure #174 battle contract/validator/computation
          ↓
#183 troop/rules/terrain/reward source
          +
#165 troop inventory/loss mutation
          +
corrected #152 quest operation
          +
accepted #163 economy operation
          +
accepted #137 candidate ledger/outbox
          ↓
authoritative battle-result application
          +
#166 territory consequence / #168 boss reward / #177 visible receipt as applicable
```

### 3.6 Champion encounter and boss rewards

```text
pure #180 Champion actor/action/boss/encounter planners
          ↓
#156/#183 Champion/skill/boss/encounter source
          +
#173 committed realm
          +
corrected #153/#178/#127 lifecycle/release/test support
          ↓
production Champion encounter migration
          +
accepted #137 result ledger/outbox
          +
#168 deterministic persisted reward
          +
#177 committed-result presentation
          ↓
separately approved Champion-capable #223/#150 profile
```

### 3.7 World/relationship/notification

Pure planner/session phases may proceed now. Production persistence and callers remain gated:

```text
#177 session queue → presenter/content → #137 durable outbox
#176 pure planner → #136/#137/#183 production relationship integration
#172 pure planner → #137/#153/#177/#183 + approved effect consumers
```

### 3.8 Terrestrial source

```text
PR #217 exact-source technical completion under PR #221 contract
          ↓
user approval for exact source version/profile/variant IDs and hashes
          ↓
#156 + #183 + owning runtime issue
          ↓
separate engineering integration + GPT technical review + Codex design-fidelity review
          ↓
user integrated acceptance
```

Until then, terrestrial IDs, labels, biome tags, variants, images, hashes, and versions are source-review evidence only—not gameplay, spawn, AI, combat, reward, save, narrative, or runtime authority.

## 4. Allowed parallel lanes

Correction lanes may proceed independently where files do not overlap:

```text
#156  codex/quest-definition-yaml-validator-correction
#153  codex/bootloader-lifecycle-contract-correction          [Bootloader lock]
#163  codex/economy-integrity-contract-correction
#136  codex/narrative-save-default-regression-correction
#152  codex/quest-compatibility-nonmutating-correction
#127  codex/playmode-profile-lifecycle-correction
#178  codex/release-controller-containment-correction
#161  codex/android-debug-route-notice-correction
#155  codex/repository-quality-gate-policy-correction
#137  codex/crash-safe-save-candidate-correction
#194  codex/terrestrial-design-foundation                     [existing draft PR #217]
```

Pure nonmutating lanes:

```text
#177  codex/notification-contract-queue
#176  codex/relationship-contract-planner
#172  codex/world-state-contract-planner
#168  codex/boss-loot-contract-planner
#174  codex/battle-contract-simulator
#180  codex/champion-combat-contract-planner
```

No correction or planner may broaden into NVS, source authorship, balance, scenes, Android bridge, production save schema, production components/services/callers, or another designated shared file without a new declared dependency and lock.

## 5. Shared-file risk

No designated shared-file lock is active at this audited head.

```text
unity/Assets/AL/Scripts/Core/Bootloader.cs
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs
```

The first approved open PR declaring one obtains the exclusive soft lock. Merged or closed PRs release their locks. Save fields require defaults, migration, old-save fixtures, semantic validation, failure recovery, duplicate safety, deletion coverage, and rollback/downgrade policy.

## 6. Evidence policy

- **current source:** inspect production blobs after merge; PR body and issue closure are low-trust metadata;
- **build:** exact command/version/base/head/exit/log/compiler scan;
- **test:** exact discovered/passed/failed/skipped totals and retained XML/logs;
- **asset:** Force-Text/GUID/reference/local-file-ID/schema inventory, import/reimport, missing-script and field-preservation proof;
- **save:** candidate inventory/ranking, semantic outcomes, normal/recovery/fault/delete/uncertain/reload/duplicate/offline matrices;
- **economy/reward:** checked no-save operations, overflow/duplicate/malformed state, ledger/idempotency, event/save/notification counts;
- **battle/combat/encounter:** immutable source identity, finite/range matrices, state-transition tables, deterministic vectors, side-effect purity, replay/conflict, exact event/resource/cooldown/result counts;
- **scene/PlayMode:** exact unload and callback quiescence before profile restore; service/global cleanup; second run and failure paths;
- **catalog/contract:** schema/version/hash/provenance, immutable results, deterministic vectors, generated drift, packaged producer/consumer proof;
- **Player:** current BuildReport/output, exact profile, disposable launch environment, ordered markers, severe-log scan, honest termination state;
- **Android/CI:** parsed/enforced policy, exact event ranges, current PR body/state, retained transcript/reports/artifacts, intentional failures, protection evidence;
- **source/design:** actual rendered full-resolution source tied to immutable hashes, binary retrieval, provenance, accessibility, technical disposition, and user approval.

Skipped, stale, duplicate-workspace, pointer-only, Console-only, compile-only, wrong-policy green, missing-artifact, development-fallback, or `continue-on-error` results are not passing evidence.

## 7. Immediate mitigation

```text
1. Correct #156 first; no trusted Unity content claim exists yet.
2. Correct #153 before production scenes or lifecycle persistence.
3. Correct #163 before save transactions, progression, battle, Champion, or rewards consume economy state.
4. Correct #136 and #152 before accepting #137 candidate semantics.
5. Correct merged #231 through #137 candidate ranking, rollback certainty, typed deletion, and full fault tests.
6. Correct #127 before any PlayMode evidence is cited.
7. Correct #178 before ShellFoundation Player packaging or Champion integration.
8. Continue #155 from accepted PR #232 partial work into policy enforcement, live failures, Unity model, and protection.
9. Correct the narrow #161 visible-notice defect.
10. Keep PR #217 draft until exact-source technical completion and user review readiness.
11. Permit pure #177/#176/#172/#168/#174/#180 lanes without production integration or closure.
12. Keep #183, #223, #150, A1/G1/runtime, progression reconnection, persisted battle/Champion/boss/world/relationship/notification work, Android export, and release claims behind prerequisites.
```
