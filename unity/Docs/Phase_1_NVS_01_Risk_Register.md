# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-16
**Audited current-main head:** `cbdb09f99c3f803a282e8582cd7375680ead3693`
**Active control state:** Phase 1 remains paused behind reopened #156 and the red Phase 0/1 foundation gate
**Approved product intent:** issue #138 D1–D16
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`
**Product direction:** `unity/Docs/Product_Direction.md`

This register records verified current-source and delivery risk. It supersedes assumptions based only on automatic issue closure, merged PR state, branch descriptions, passing tests that encode the wrong policy, source presence, generated files, compilation, workflow success, LFS pointers, or one-platform evidence.

The latest user decision assigns every future project task and risk disposition to Codex. Historical GPT findings remain technical evidence, but no risk may wait for another GPT response or approval.

## 1. Current risks

| ID | Severity | Risk | Current evidence | Codex owner | Tracking/status |
| --- | --- | --- | --- | --- | --- |
| R1 | Critical | QuestDefinition authority can still false-pass malformed serialized assets | draft PR #237 improves disk/YAML discovery but does not yet enforce all field types, exact `m_Script` mapping, valid-candidate accounting, YAML class/root, or production-assembly filtering | engineering + coordination/review | **Active — #156 / PR #237 blocked** |
| R2 | Critical | Merged or automatically closed work may be mistaken for accepted behavior | post-merge audit found blocking defects in PRs #189/#203/#214/#211/#212/#209/#208/#195/#210/#231; later partial PRs can still auto-close issues | coordination/review + engineering | **Active control — current-source review required** |
| R3 | Critical | Archived OMEN_1 may become false narrative/runtime authority | archive conflicts with approved offer, deployment, failure, Tear, report, atomic consequence, abandonment, localization, and resume decisions | narrative/content + coordination/review | **Contained — #128 starts only after #156** |
| R4 | Critical | NVS-01 consequences lack one durable atomic/idempotent transaction | Gold, affinity, quest completion, retained Tear, chapter unlock, encounter result, ledger, outbox, and notification are not yet composed through one accepted candidate operation | coordination/review + engineering | **Blocked — #133/#134 after foundations** |
| R5 | Critical | Save validation can destroy corruption evidence or fabricate valid-looking state | merged PR #231 removes malformed rows/strings, creates objects/resources, defaults chapter, and validates mainly non-null top-level shape | engineering + coordination/review | **Active — #137 correction required** |
| R6 | Critical | Save recovery can discard the only valid generation | temp is deleted unconditionally and previous can be deleted because primary merely exists; no complete primary/backup/previous/temp candidate ranking | engineering | **Active — #137** |
| R7 | Critical | Save disk and memory can diverge while status claims preservation | final backup/copy/verification or rollback failure can leave new disk state with old memory; rollback/cleanup failures are swallowed | engineering | **Active — #137; commit-uncertain required** |
| R8 | Critical | Full profile deletion can be falsely reported | deletion failures are ignored before current state/status is cleared | engineering + user privacy approval | **Active — #137; typed post-verification required** |
| R9 | Critical | Quest compatibility can discard or activate ambiguous state | current service removes null/blank rows, keeps first duplicates, seeds prototype quests, accepts unsupported side quests, and can save rejected/no-change paths | engineering + coordination/review | **Active — #152 correction** |
| R10 | High | Relationship old-save compatibility remains unproven | no real omitted-field Unity JSON fixture, repeated normalization, unrelated-field preservation, or round-trip idempotency | engineering + coordination/review | **Active — #136 correction** |
| R11 | Critical | Economy reads/wrappers can mutate, fabricate, or ambiguously combine value | negative/null state is repaired, duplicates are summed/clamped, wrappers save independently, and accepted no-save candidate operations do not exist | engineering + coordination/review | **Active — #163 correction** |
| R12 | Critical | Offline progress can operate on invalid economy/progression state | save load adds resources and completes timers with direct wall clock, unchecked/parallel rules, state creation, and no accepted definition/economy snapshots | engineering + coordination/review | **Blocked — #137 consumes #163/#165 contracts** |
| R13 | Critical | Bootloader load/marker/save lifecycle remains unsafe | draft PR #241 improves retry/drift handling but accepts weak load outcomes, can throw on malformed markers, collapses diagnostics, and does not verify save status | engineering + coordination/review | **Active — #153 / PR #241 blocked** |
| R14 | Critical | Multiple/cross-scene lifecycle owners can duplicate or disappear | two Bootloaders can both tick/save; no owner token distinguishes load-in-progress; Boot-only owner is destroyed on transition while static services survive | engineering + coordination/review | **Active — #153; blocks #223/#150** |
| R15 | High | PlayMode tests can restore a developer profile while callbacks remain alive | representative scene is not fully unloaded/quiescent before restore and global/fault/second-run coverage is incomplete | engineering | **Active — #127 correction** |
| R16 | Critical | Production controller paths still mutate saves/economy invisibly | Kingdom starts another load, periodically completes progression, dashboard reads seed state, and Champion Arena grants recurring credits | engineering | **Active — #178 correction** |
| R17 | High | Android release fallback notice can disappear | sanitized back-stack’s next Compose pass can clear the technical rejection notice | engineering | **Active — #161 correction** |
| R18 | High | Green repository workflow can hide policy gaps | policy YAML is not fully authoritative; ranges/classifiers/security/transcripts/live failure proofs/Unity model/protection are incomplete | engineering + coordination/review + maintainer | **Active — #155; PR #232 partial only** |
| R19 | High | Retired GPT or Android Studio ownership can persist in templates, branches, or review expectations | pre-handoff governance allowed `gpt/` branches and mandatory GPT dispositions | coordination/review | **Controlled by single-agent governance/policy handoff; verify after merge** |
| R20 | High | Duplicate-workspace Unity evidence may be cited as current | older drafts and issues cite different worktrees; evidence is trustworthy only when exact path, base/head, branch state, and dirty/clean state are reported | engineering + coordination/review | **Active — exact workspace evidence required** |
| R21 | High | Production scenes have no committed authority | only test scene is committed and normal Build Settings are empty | engineering + coordination/review | **Active — #223 after #156/#153** |
| R22 | Critical | Scene generator can overwrite authored assets and packaging policy | current generator recreates scenes and replaces Build Settings without stable-GUID protection, dry run, rollback, drift validation, or idempotency proof | engineering + coordination/review | **Active — #223** |
| R23 | High | Player artifacts can be accepted without trustworthy launch evidence | stale output, wrong scene list, missing BuildReport, developer-profile launch, severe logs, or premature process kill can appear successful | engineering + coordination/review | **Controlled by #224 contract; #150 pending** |
| R24 | High | Game-data definitions remain mutable, nullable, incomplete, and fallback-prone | current service creates runtime ScriptableObjects, discards story objects, omits research/troop/champion/skill authority, and lacks version/hash/provenance | engineering + source modes + coordination/review | **Active — #183 after #156** |
| R25 | Critical | Progression may invent or consume unauthoritative definitions/state | building/research/troop queries create rows; IDs/levels/costs/timers are weak; online/offline rules diverge | engineering + coordination/review | **Contract merged #238; production #165 blocked** |
| R26 | Critical | Battle computation can accept invalid input or mutate progression | current simulator normalizes empty armies, reads global research, uses process-local randomness, and updates WinBattle from computation | engineering + coordination/review | **Contract merged #234; #174 pure lane authorized** |
| R27 | Critical | Champion combat/encounter can be poisoned or duplicate consequences | raw floats, hybrid skill catalogs, Crownlands fallback, slot-index behavior, weak action identity, boss-side rewards, and object-null clear authority remain | engineering + coordination/review | **Contract merged #235; #180 pure lane authorized** |
| R28 | Critical | Boss rewards can be fabricated, duplicated, or partially committed | current service uses fallback loot/process-local randomness/nested saves and boss catch paths can grant synthetic value | engineering + coordination/review | **Contract merged #230; #168 pure lane authorized** |
| R29 | High | Notification success can be inferred from Console output | raw strings/logging lack queue, typed receipt, visible presenter, acknowledgement, dedupe, and durable outbox | engineering + narrative/content | **Contract merged #226; #177 pure lane authorized** |
| R30 | High | Relationship mutations can crash, overflow, save independently, or misclassify malformed state | first-duplicate selection, arbitrary IDs, nonfinite/unchecked values, hard-coded labels, and no candidate composition | engineering + narrative/content + coordination/review | **Contract merged #227; #176 pure lane authorized** |
| R31 | High | World-state events can announce unpersisted/unconsumed effects | raw enum/string/float state, hard-coded copy, no accepted effect consumers, ledger, or atomic persistence | engineering + narrative/content + coordination/review | **Contract merged #228; #172 pure lane authorized** |
| R32 | Critical | Territory capture/income can farm rewards and mutate on read | current reads seed T1–T5, same-owner capture repeats +100 credits/quest progress, events precede persistence, and income sums mutable saved bonuses through six live queries | engineering + coordination/review | **Contract merged #240; #166 pure lane authorized** |
| R33 | High | Champion customization can destroy future IDs or persist unapplied appearance | controller edits live save, saves before normalization/application, async catalog can arrive late, hard-coded/catalog sources hybridize, queries mutate, and invalid colors/scales clamp | engineering + narrative/design + coordination/review | **Contract merged #239; #184 pure lane authorized** |
| R34 | High | Realm identity can be invalid or overwritten without accepted persistence/catalog support | `None`, undefined values, missing definitions, and existing-profile replacement require durable one-time semantics | engineering + coordination/review | **PR #202 contract; #173 pending #137/#183** |
| R35 | Critical | Realm Gem, Wishgate, Warmaster, territory, and progression rewards remain exploitable or ambiguously persisted | repeated actions, unsafe entitlements, nested saves, hard-coded IDs/costs, and missing committed result identity remain | engineering + coordination/review | **Blocked — #165/#166/#169/#171** |
| R36 | High | Terrestrial preview source can be mistaken for approved creative/runtime authority | PR #217 has incomplete normalized media/version/hash/LFS/provenance/variant evidence and no user approval | terrestrial-design + coordination/review + user | **Active — #194 / PR #217 blocked** |
| R37 | High | LFS pointer or prose can substitute for pixel review | exact full-resolution binary media tied to immutable hashes and clean retrieval is not yet presented | terrestrial-design + coordination/review + user | **Active — PR #221 contract** |
| R38 | High | Android↔Unity integration remains unproven | no packaged export, mounted host, request/result/session identity, lifecycle, or device evidence | engineering + coordination/review | **Deferred — #135 after standalone Player path** |
| R39 | High | Status and issue metadata can drift during concurrent merges | partial work and documentation merges can change issue/PR metadata faster than acceptance records | coordination/review + maintainer | **Mitigated — recurring current-main audits** |
| R40 | High | Shared-file overlap can invalidate concurrent work | draft PR #241 exclusively owns `Bootloader.cs`; overlapping changes would create lifecycle/conflict risk | coordination/review + engineering | **Active lock — no other PR may edit the file** |
| R41 | High | Unity Hub playable experience can remain a debug toy instead of the requested adult realm-war game | current prototype still contains placeholder/demo arena, primitive objects, and incomplete launch→realm→kingdom→warzone objective flow | coordination/review + engineering + source modes | **Active — product direction now binding; visual/UX modernization and end-to-end gates required** |
| R42 | Critical | Final warzone objective path can fragment across unsafe economy/save/boss/gem/Warmaster systems | dragon/boss/gem/Warmaster/final-wish loop depends on durable realm identity, committed rewards, anti-duplication, objective state, and save recovery | coordination/review + engineering | **Blocked behind #137/#163/#166/#168/#169/#171/#173/#180/#183 and later warzone objective spine** |

## 2. D1–D16 controls

A1, G1, and runtime must preserve:

- authored deployment node before arena request;
- transient encouraging failure/retry and nonterminal `FAILED`;
- Tear acquired once on arena success and retained;
- manual report to Valerius;
- 500 Gold, +5 Valerius affinity, quest completion, retained artifact state, and selected-realm Chapter 1 unlock exactly once at report conclusion;
- complete localization inventory;
- honest requested-capability classification;
- abandonment only outside an active encounter;
- universal post-realm eligibility and Veil Watch Valerius;
- offered rather than auto-accepted start;
- exact dialogue-node resume and duplicate-safe encounter/report recovery.

## 3. Dependency controls

### 3.1 Trusted Unity and NVS

```text
#156 accepted full QuestDefinition YAML authority
  ↓
#128 Codex narrative/content A1
  ↓
#133 Codex coordination/review G1
  ↓
accepted save/economy/notification/relationship/scene/result foundations
  ↓
#134 Codex engineering C1–C4
  ↓
G2 Codex coordination/review → A2 Codex narrative/content → U1 user
```

### 3.2 Save and economy

```text
#136 real omitted-JSON/idempotent relationship compatibility
          +
#152 non-mutating quest compatibility
          +
#163 typed non-repairing economy/no-save operations
          ↓
#137 candidate ranking, rollback certainty, explicit repair,
     verified deletion, ledger/outbox, crash-safe persistence
```

Mandatory controls:

- preserve stable unknown and malformed evidence;
- disable ambiguous duplicate domains;
- never first/max/sum/clamp as silent repair;
- prefer the cleanest validated candidate;
- retain rollback through final verification;
- clone → validate → persist/verify → publish;
- return commit-uncertain rather than guessing durability.

### 3.3 Game data, progression, territory, customization

```text
#156 trusted asset baseline
  ↓
#183 immutable versioned catalog foundation
  ↓
approved source catalogs and focused service migrations
  ↓
#173 realm selection
#165 progression orders/production snapshots
#166 territory ownership/capture/income
#169 Realm Gem/Wishgate
#171 Warmaster
#184 customization catalog/model/save integration
```

Pure first phases may model and validate the contracts now, but production integration must not invent source records, balance, content, aliases, or fallback authority.

### 3.4 Battle, Champion, and rewards

```text
accepted #183 source snapshots
        +
accepted #163 no-save economy
        +
accepted #137 candidate ledger/outbox
        +
accepted #165 troop/research snapshots
        +
#174 pure battle result/application
        +
#180 Champion encounter completion
        ↓
#168 deterministic persisted boss reward
        ↓
#177 committed-result delivery
```

### 3.5 Scenes and Player

```text
#156 accepted assets + #153 accepted single cross-scene owner
          ↓
#223 stable non-destructive production scenes
          +
#178 non-mutating ShellFoundation controllers
          +
#127 lifecycle-safe profile-isolated PlayMode
          ↓
#150 exact three-scene Windows64 Development Player + disposable launch smoke
          ↓
#135 Android export/host
```

### 3.6 Source/design

```text
PR #217 exact-source technical completion under PR #221 contract
          ↓
user approval of exact source version/profile/variant IDs and hashes
          ↓
#156 + #183 + owning runtime issue
          ↓
separate engineering integration + coordination/review technical disposition
+ terrestrial-design fidelity review
          ↓
user integrated acceptance
```

Until then, terrestrial IDs, labels, biome tags, variants, images, hashes, and versions are source-review evidence only—not gameplay, spawn, AI, combat, reward, save, narrative, or runtime authority.

## 4. Allowed parallel lanes

### Correction lanes

```text
#156  codex/quest-definition-yaml-validator-correction       [open PR #237]
#153  codex/bootloader-lifecycle-contract-correction         [open PR #241; Bootloader lock]
#163  codex/economy-integrity-contract-correction
#136  codex/narrative-save-default-regression-correction
#152  codex/quest-compatibility-nonmutating-correction
#127  codex/playmode-profile-lifecycle-correction
#178  codex/release-controller-containment-correction
#161  codex/android-debug-route-notice-correction
#155  codex/repository-quality-gate-policy-correction
#137  codex/crash-safe-save-candidate-correction
#194  codex/terrestrial-design-foundation                    [open PR #217]
```

### Pure nonmutating lanes

```text
#177  codex/notification-contract-queue
#176  codex/relationship-contract-planner
#172  codex/world-state-contract-planner
#168  codex/boss-loot-contract-planner
#174  codex/battle-contract-simulator
#180  codex/champion-combat-contract-planner
#165  codex/progression-contract-planner
#166  codex/territory-contract-planner
#184  codex/customization-contract-planner
```

No lane may broaden into NVS, source authorship, balance, scenes, Android embedding, production save schema, or another designated shared file without a Codex coordination/review dependency and lock decision.

## 5. Shared-file risk

Current exclusive lock:

```text
unity/Assets/AL/Scripts/Core/Bootloader.cs
  owner: draft PR #241
```

Unlocked designated files:

```text
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs
```

The first approved open PR declaring an unlocked file obtains its lock. Merged or closed PRs release locks. Save fields require defaults, migration, old-save fixtures, semantic validation, failure recovery, duplicate safety, deletion coverage, and rollback/downgrade policy.

## 6. Evidence policy

- **coordination/review:** current main, current source, current issues/PRs, dependencies, locks, source claims, acceptance criteria, and evidence quality;
- **build:** exact command/version/base/head/exit/log/compiler scan;
- **test:** exact discovered/passed/failed/skipped totals and retained XML/logs;
- **asset:** Force-Text/GUID/script mapping/local-file-ID/class/root/schema inventory, import/reimport, missing-script and field-preservation proof;
- **save:** candidate inventory/ranking, semantic outcomes, normal/recovery/fault/delete/uncertain/reload/duplicate/offline matrices;
- **economy/progression/territory/reward:** checked no-save operations, immutable source/state revisions, overflow/duplicate/malformed behavior, ledger/idempotency, event/save/notification counts;
- **customization:** catalog/schema/C#/Fable parity, raw/effective/draft separation, future-ID preservation, async stale-result tests, reversible adapter, and persistence rollback;
- **scene/PlayMode:** exact unload/callback quiescence before profile restore, owner identity, service/global cleanup, second run and failures;
- **Player:** current BuildReport/output, exact profile, disposable launch environment, ordered markers, severe-log scan, honest termination;
- **Android/CI:** correct event range, current PR body/state, retained transcript/reports/artifacts, intentional failures, protection evidence;
- **source/design:** exact rendered full-resolution source tied to immutable hashes, binary retrieval, provenance, accessibility, technical disposition, and user approval.

Skipped, stale, duplicate-workspace, pointer-only, Console-only, compile-only, wrong-policy green, missing-artifact, fallback, swallowed-exception, or `continue-on-error` results are not passing evidence.

## 7. Immediate mitigation

```text
1. Merge and verify the single-agent governance handoff; reject new GPT/Android Studio/Gemini ownership modes and prefixes.
2. Codex coordination/review rechecks all open PRs from current main and preserves the Bootloader lock.
3. Keep Phase 1 paused until PR #237 fixes all false-pass paths and passes canonical #156 evidence.
4. Keep the Bootloader lock on PR #241 until one owner/load/save/marker/rollback/scene lifecycle and the full canonical matrix pass.
5. Correct #163 before save transactions, progression, territory, battle, or rewards consume economy state.
6. Correct #136 and #152 before accepting #137 candidate semantics.
7. Correct #137 candidate ranking, rollback certainty, typed deletion, semantic preservation, and fault injection.
8. Correct #127 before any PlayMode evidence is cited and #178 before ShellFoundation Player packaging.
9. Complete #155 policy/live failure/protection work and the narrow #161 visible-notice correction.
10. Keep PR #217 draft until exact-source technical completion and user review readiness.
11. Permit the nine pure planner lanes without production integration, shared locks, or issue closure.
12. Keep #183, #223, #150, A1/G1/runtime, persisted progression/territory/customization/reward/world/relationship work,
    Android export, and release claims behind prerequisites.
13. Reopen closed issues or create focused follow-ups whenever current source, Unity Hub play, or validation evidence shows a closed issue still blocks the product direction.
```

No next GPT or Android Studio action exists.
