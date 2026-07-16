# Phase 1 NVS-01 Status

**Status date:** 2026-07-16  
**Integration branch:** `main`  
**Audited current-main head:** `371cc019c7a4526b8b20c145104c994d5c49a056`  
**Roadmap state:** Phase 1 remains paused behind reopened QuestDefinition authority issue #156 and the red Phase 0/1 foundation gate  
**Approved product intent:** issue #138 D1–D16  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

`AGENTS.md` is authoritative. This record separates specification, source presence, merge state, issue state, validation evidence, accepted behavior, creative approval, packaging evidence, and player-visible completion.

A merged PR, closed issue, green test suite, generated asset, Console log, uploaded LFS pointer, Player executable, or passing workflow is not acceptance by itself. Post-merge source verification supersedes stale PR descriptions and automatic issue closure.

## 1. Current control summary

- The active product milestone remains NVS-01.
- No approved A1 narrative packet is active. The archived OMEN_1 packet remains historical reference only.
- #156 is reopened and remains the first trusted-Unity-content gate. Merged PR #189 did not implement the binding Force-Text YAML/subasset/schema validator from PR #218.
- Nine other implementation issues remain reopened after their merged source was found to retain blocking defects: #153, #163, #136, #152, #127, #178, #161, #155, and #137.
- The only open pull request is draft terrestrial source PR #217. Correction and pure-planner work is issue-authorized but no correction/planner implementation PR is currently open.
- No designated shared file is currently locked. A correction/integration PR must declare its lock before editing `Bootloader.cs`, `SaveGameData.cs`, `LocalGameDataService.cs`, or `ProjectInitializer.cs`.
- The canonical Unity workspace is `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity`. Evidence from `C:\Users\MY\Documents\AnotherLife\unity` is noncanonical and cannot close a Unity gate.
- The only committed `.unity` scene remains test-only `Assets/Test.unity`. Normal Build Settings remain empty.
- Merged PR #230 supplies the binding deterministic boss-reward transaction contract. It does not implement boss rewards.
- Merged PR #231 supplies useful save-file scaffolding but is not accepted #137 completion; #137 is reopened for candidate ranking, rollback certainty, semantic preservation, fault coverage, and verified deletion.
- Merged PR #232 supplies accepted partial quality-gate fixtures, terrestrial path coverage, and proof/protection documentation. #155 remains open because policy authority, exact event/diff behavior, complete classifiers/security/artifacts, live failure proofs, and configured protection are unfinished.
- Merged PR #234 supplies the binding pure deterministic battle computation/result-application contract. It does not implement or reconnect battle simulation.
- Merged PR #235 supplies the binding Champion combat/skill/boss/encounter integrity contract. It does not implement or package Champion combat.

## 2. Ownership state

The latest user decision assigns all delivery to separately declared Codex modes while preserving GPT review and user approval:

- **GPT:** coordination, specifications, state/contract/save/test design, technical review, risk, sequencing, and merge readiness;
- **Codex narrative/content:** narrative source, player-facing copy, localization meaning, named-item/event/relationship/skill/boss meaning, and fidelity;
- **Codex terrestrial-design:** terrestrial visual-design source and fidelity;
- **Codex engineering:** Android, Unity, runtime, assets/import, saves, builds, tests, CI, tooling, and generated technical artifacts;
- **User:** final product, creative, visual-design, balance, irreversible-profile, integrated playtest, milestone, and release approval.

Android Studio and Unity are tools. `android-studio/` and `gemini/` are retired branch/workstream prefixes for new work.

## 3. Binding merged specifications

A merged specification is a contract, not implementation completion.

| Area | Binding artifact | Merged PR | Implementation state |
| --- | --- | --- | --- |
| save semantics | `Save_Semantic_Compatibility_Policy.md` | #197 | current quest/economy/save implementations still violate it |
| Bootloader stack | `Bootloader_Service_Stack_Integrity_Spec.md` | #198 | #153 reopened after PR #203 source audit |
| release command containment | `Unity_Production_Command_Containment_Spec.md` | #200 | #178 reopened after PR #208 source audit |
| realm selection | durable one-time realm-selection specification | #202 | implementation remains pending #173 prerequisites |
| profile-safe PlayMode | representative smoke isolation specification | #207 | #127 reopened after PR #209 source audit |
| economy | `Economy_Integrity_Spec.md` | #215 | #163 reopened after PR #214 source audit |
| QuestDefinition assets | `QuestDefinition_Asset_Authority_Validation_Spec.md` | #218 | #156 reopened after PR #189 source audit |
| game-data authority | `Game_Data_Catalog_Authority_Spec.md` | #220 | #183 implementation blocked by #156 |
| terrestrial source review | `Terrestrial_Source_Packet_Validation_Spec.md` | #221 | PR #217 remains draft/blocked |
| production scenes/Player | `Production_Scene_Player_Build_Spec.md` | #224 | #223/#150 blocked by reopened prerequisites |
| notification delivery | `Notification_Delivery_Contract_Spec.md` | #226 | pure session-contract/queue phase may proceed |
| relationship transactions | `Relationship_Integrity_Transaction_Spec.md` | #227 | pure snapshot/planner phase may proceed |
| world-state lifecycle | `World_State_Lifecycle_Transaction_Spec.md` | #228 | pure lifecycle/effect planner phase may proceed |
| boss rewards | `Boss_Loot_Result_Transaction_Spec.md` | #230 | pure computation/application-planner phase may proceed |
| battle computation/results | `Battle_Computation_Result_Transaction_Spec.md` | #234 | pure contract/validator/computation phase may proceed |
| Champion combat/encounter | `Champion_Combat_Encounter_Integrity_Spec.md` | #235 | pure contract/validator/transition-planner phase may proceed |

## 4. Post-merge implementation audit

The following PRs are merged in Git history but are not accepted completion. Their issues are reopened against current source.

| Merged PR | Reopened issue | Accepted direction retained | Live blocking defect | Authorized correction branch |
| --- | --- | --- | --- | --- |
| #189 | #156 | narrative QuestDefinition type/GUID direction and basic duplicate type scan | no Force-Text YAML `m_Script`/local-file-ID/subasset/schema/malformed-fixture validator | `codex/quest-definition-yaml-validator-correction` |
| #203 | #153 | construct-before-publication, marker-last intent, typed initialization result | load token commits before successful load; save can cross-wire; no post-verification rollback; mutable/throwing marker validation; no cross-scene owner | `codex/bootloader-lifecycle-contract-correction` |
| #214 | #163 | checked arithmetic helpers and some wallet tests | reads repair state; null/duplicate/negative rows are deleted/summed/clamped; no typed no-save primitives; production staging incomplete | `codex/economy-integrity-contract-correction` |
| #211 | #136 | isolated relationship mutation/save/reload test | no real omitted-old-JSON fixture, repeated normalization, unrelated-field preservation, or round-trip idempotency | `codex/narrative-save-default-regression-correction` |
| #212 | #152 | null-safe service guard intent | live queries delete malformed rows, keep first duplicate, seed Q1–Q5, accept unsupported side quests, and save rejected/no-change paths | `codex/quest-compatibility-nonmutating-correction` |
| #209 | #127 | external profile snapshot intent and explicit editor scene load | representative scene not unloaded before profile restore; deferred callbacks can write late; uncaptured helper teardown can set `Time.timeScale` to zero; fault/second-run matrix absent | `codex/playmode-profile-lifecycle-correction` |
| #208 | #178 | obvious cheat buttons removed and fail-closed command descriptors added | scene startup loads again; periodic controller completes progression; dashboard reads seed state; Champion grants recurring credits | `codex/release-controller-containment-correction` |
| #195 | #161 | compile-time debug gate, typed route policy, sanitized stack, stable preview test seam | second sanitized Compose pass can immediately clear the visible rejection notice | `codex/android-debug-route-notice-correction` |
| #210 + #232 | #155 | three stable workflow jobs, one positive run, terrestrial path coverage, local failure fixtures, and a documented live-proof/protection plan | policy YAML unused; unsafe event/diff fallback; incomplete classifiers/security/artifacts; live failing PR matrix, Unity model, and configured protection absent | `codex/repository-quality-gate-policy-correction` |
| #231 | #137 | durable temp write, file-operation seam, separate statuses, primary validation, clone-before-publish | destructive normalization; weak candidate semantics; valid previous generation can be discarded; commit uncertainty mislabeled; rollback/delete failures swallowed; no fault matrix | `codex/crash-safe-save-candidate-correction` |

No downstream issue may cite the merge or automatic closure of these PRs as accepted behavior.

## 5. Open pull requests

| PR | Issue | Scope | Current disposition | Shared lock |
| --- | --- | --- | --- | --- |
| #217 | #194 | terrestrial design-source foundation | **Draft / technically blocked / user creative review not ready** | none |

PR #217 must satisfy merged PR #221 before user review:

- replace `Fixes #194` with `Refs #194`;
- rebase onto current `main`;
- add retained schema and deterministic manifest/media validation;
- normalize source version, media type, dimensions, byte length, SHA-256, LFS OID/size, prompt/generation/license references, and direct rendered review links;
- prove clean binary LFS retrieval and actual hashes/dimensions;
- classify six text-only variants as proposed or provide exact visual source;
- mark labels/biome tags as nonlocalized, non-player-facing, non-runtime source intent;
- complete canonical Unity import evidence or move review-only media outside `Assets`;
- separate technical, user-creative, narrative-naming, and runtime-integration states.

## 6. Active correction gates

### 6.1 #156 — QuestDefinition authority

Binding contract:

```text
unity/Docs/QuestDefinition_Asset_Authority_Validation_Spec.md
```

Completion requires:

- Force-Text disk discovery independent of `AssetDatabase.FindAssets("t:QuestDefinition")`;
- exact `m_Script` fileID/GUID/type classification;
- every YAML document/subasset by local file ID;
- exact type/path/GUID/menu and historical 12-field schema lock;
- blank/duplicate ID and missing/unexpected-field rejection;
- complete non-imported malformed fixture matrix;
- one valid full-field create/import/reimport round trip;
- canonical compile, focused/full EditMode, reimport, missing-script, GUID, and final-status evidence.

No A1, #183 production authority, #223 scene authority, or #150 Player packaging may claim a trusted Unity baseline before #156 is complete.

### 6.2 #153 — service stack and scene lifecycle

Completion requires:

- explicit load state committed only after `Load()` succeeds;
- typed failure/retry behavior;
- marker-validated pause/quit save boundary;
- rollback-capable publication through final verification;
- nonthrowing, exact, immutable marker/type inventory validation;
- separate missing/mismatched/version/phase/service diagnostics;
- one approved persistent or marker-safe per-scene lifecycle owner;
- deterministic owner identity through Boot → RealmSelection → Kingdom → ChampionArena → Kingdom;
- construction/publication/load/save/drift/two-owner/scene-transition fault matrix;
- canonical compile/EditMode and corrected #127 PlayMode evidence.

The next correction PR must declare the `Bootloader.cs` lock.

### 6.3 #163 — economy integrity

Completion requires:

- pure typed resource and Warzone Credit reads;
- preservation and mutation disablement for null/blank/duplicate/negative/unknown state;
- no first/max/sum/clamp repair in ordinary services;
- exact supported/core/optional rare-resource authority;
- checked no-save candidate mutation primitives;
- compatibility wrappers with exact save/event behavior;
- atomic production contribution/remainder/wallet staging;
- current caller inventory and no reward authorization;
- complete malformed/overflow/reload/event/save-count evidence.

#168, #174, #180, #137, #165, and NVS transactions require the accepted no-save economy boundary where they apply.

### 6.4 #136 + #152 + #137 — save semantics and persistence

```text
#136 real omitted-JSON/idempotent relationship normalization
          +
#152 non-mutating quest compatibility view
          +
#163 typed non-repairing economy semantics
          ↓
#137 candidate inventory/ranking, recovery, explicit repair, verified deletion, crash-safe persistence
```

PR #231 is partial scaffolding only. #137 completion additionally requires:

- immutable candidate inventory and exact generation ranking;
- no destructive normalization during validation;
- preservation of every potentially valid primary/backup/previous/temp generation before cleanup;
- rollback retained through final primary/backup/cleanup verification;
- typed commit-uncertain/recovery-required state;
- explicit quarantine-failure stop behavior;
- typed post-verified deletion;
- deterministic file-operation and clock failure seams;
- checked domain-safe offline progress;
- full normal/recovery/fault/preservation/duplicate/deletion/lifecycle matrix.

### 6.5 #127 + #178 + #223 + #150 — safe scene and Player path

```text
#156 trusted Unity asset baseline
          +
#153 accepted cross-scene lifecycle owner
          ↓
#223 non-destructive generator + four committed stable production scene assets
          +
#178 non-mutating ShellFoundation controller/transition containment
          +
#127 lifecycle-safe profile-isolated PlayMode evidence
          ↓
#150 exact Build Settings + Windows64 Development Player + disposable-profile launch smoke
          ↓
#135 Android export/host packaging
```

The first Player profile remains exactly:

```text
ShellFoundation
0 Assets/AL/Scenes/Boot.unity
1 Assets/AL/Scenes/RealmSelection.unity
2 Assets/AL/Scenes/Kingdom.unity
```

Excluded:

```text
Assets/Test.unity
Assets/AL/Scenes/ChampionArena.unity
```

ChampionArena remains deferred until corrected #178, accepted #180 implementation, and a separate approved scene-profile change.

### 6.6 #155 + #161 — release verification

PR #232 is accepted partial #155 work, not completion.

#155 remains open through:

1. parsed policy authority or deterministic generated-policy drift enforcement;
2. exact PR base/head and push before/after ranges with fail-closed diff behavior;
3. complete owner/impact/path/completion/readiness/shared-lock/chronology classification;
4. workflow permission/action-pin/credential/timeout/transcript/diagnostic hardening;
5. live intentional pass/fail PR matrix with retained runs/artifacts;
6. implemented or explicitly tracked Unity validation model;
7. configured and API/screenshot-verified branch protection/merge controls.

#161 requires one narrow Android shell correction so a rejected release route notice survives sanitization until an intentional consumption boundary.

Neither local fixtures, a positive CI run, nor passing pure route-policy tests prove the final release behavior.

## 7. Game-data and gameplay dependencies

### 7.1 Game-data authority

```text
#156 trusted asset baseline
          ↓
#183 catalog foundation: manifest/envelope, immutable snapshots, typed load/query/diagnostics, strict validators, file/UnityWebRequest seam, hashes/tests
          ↓
approved source catalogs
          ↓
LocalGameDataService migration with declared shared-file lock
          ↓
focused consumer migrations
```

The first #183 implementation must not edit `Bootloader.cs`, claim `LocalGameDataService.cs`, switch production authority, author content, repair saves, or promote terrestrial source.

### 7.2 Progression and realm

```text
accepted #137 + #163 + #183
          ↓
#173 durable realm-selection implementation
#165 building/research/training integrity
#166 territory integrity
#169 Realm Gem/Wishgate integrity
#171 Warmaster integrity
```

Before these sources exist, presentation may fail closed but must not invent definitions, IDs, maximum levels, costs, rewards, or balance.

### 7.3 Battle computation and result application

Binding contract:

```text
unity/Docs/Battle_Computation_Result_Transaction_Spec.md
```

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
accepted #137 candidate result ledger/outbox
          ↓
authoritative battle result application
          +
#166 territory consequence when applicable
          +
#168 boss reward handoff when applicable
          +
#177 committed-result delivery
```

The pure first branch may proceed now:

```text
codex/battle-contract-simulator
```

It is limited to immutable contracts, strict validators, fixed-point checked arithmetic, canonical SHA-256 entropy, pure round/outcome/casualty/reward-proposal computation, retained vectors, fake snapshot builders, and tests.

### 7.4 Champion combat, boss encounter, and rewards

Binding Champion contract:

```text
unity/Docs/Champion_Combat_Encounter_Integrity_Spec.md
```

```text
pure #180 Champion actor/action/boss/encounter planners
          ↓
#156 + #183 Champion/skill/boss/encounter source
          +
#173 committed realm identity
          +
corrected #153/#178/#127 lifecycle/release/test support
          ↓
production actor/caster/boss/encounter migration
          +
accepted #137 result ledger/outbox
          +
#168 deterministic persisted boss reward
          +
#177 committed-result delivery
          ↓
separately approved Champion-capable #223/#150 scene/Player profile
```

The pure first branch may proceed now:

```text
codex/champion-combat-contract-planner
```

It is limited to immutable contracts, finite scalar/vector validation, actor/action/resource/cooldown/boss/encounter transition planners, fake participants/targets, retained matrices, and tests.

Binding boss-reward contract:

```text
unity/Docs/Boss_Loot_Result_Transaction_Spec.md
```

The pure boss-reward branch remains:

```text
codex/boss-loot-contract-planner
```

No pure phase may mutate saves, production services/components, callers, catalogs, scenes, UI, Android, balance, or authored content.

## 8. Pure planner lanes that may proceed

These lanes are intentionally nonmutating and do not depend on save/service integration:

| Issue | Branch | Allowed first phase |
| --- | --- | --- |
| #177 | `codex/notification-contract-queue` | typed definitions/requests/session queue/dedupe/receipts/fake presenters/tests |
| #176 | `codex/relationship-contract-planner` | immutable identity/policy/snapshot/query/classification/mutation plans/fake targets/tests |
| #172 | `codex/world-state-contract-planner` | immutable definitions/instances/UTC lifecycle/effect plans/fake consumers/tests |
| #168 | `codex/boss-loot-contract-planner` | deterministic computation/inventory snapshots/application plans/fakes/vectors/tests |
| #174 | `codex/battle-contract-simulator` | immutable validation/fixed math/SHA-256 pure battle computation/vectors/tests |
| #180 | `codex/champion-combat-contract-planner` | finite actor/action/resource/boss/encounter transition contracts/fakes/matrices/tests |

They must not edit saves, production service/component bodies, callers, source content, scenes, UI, Android, balance, or designated shared files.

## 9. NVS-01 chain

```text
#156 trusted QuestDefinition authority
  ↓
#128 Codex narrative/content A1
  ↓
#133 GPT G1
  ↓
accepted save/economy/notification/relationship/scene/battle/encounter/result foundations
  ↓
#134 Codex engineering C1–C4
  ↓
G2 GPT → A2 Codex narrative/content → U1 user
```

A1 must encode issue #138 D1–D16 exactly, including offered start, deployment node, transient failure/retry, retained Tear, manual Valerius report, one atomic consequence, abandonment limits, localization inventory, and exact resume/idempotency.

The archived packet is not approved A1 and must not become a compatibility contract through tests or runtime fallbacks.

## 10. Shared-file state

No designated shared-file lock is active at this audited head.

Designated files:

```text
unity/Assets/AL/Scripts/Core/Bootloader.cs
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs
```

The first approved correction/integration PR declaring one obtains its exclusive soft lock. A merged PR does not retain a lock after closure. No conflict resolution may discard valid current services, fields, tests, assets, contracts, or registrations.

## 11. Evidence rules

Evidence must match the risk:

- **build:** exact command, Unity/JDK/Gradle version, current base/head, exit code, full compiler/error scan, retained logs;
- **test:** discovered/passed/failed/skipped totals and retained XML/logs;
- **asset:** Force-Text/GUID/reference/local-file-ID inventory, import/reimport, missing-script scan, field preservation;
- **save/economy/reward:** semantic candidates, normal/recovery/fault/delete/overflow/duplicate/reload/event/save-count/idempotency matrices;
- **battle/combat/encounter:** immutable source identity, finite/range matrices, state-transition tables, deterministic vectors, purity, replay/conflict, exact event/resource/cooldown/result counts;
- **catalog/contract:** schema/version/hash/provenance, valid/invalid vectors, immutable query behavior, generated-contract drift, packaged producer/consumer proof;
- **scene:** exact path/name/GUID/root/component/transition/marker inventory, generator non-overwrite/idempotency, Build Settings ownership;
- **PlayMode:** disposable profile, awaited scene teardown before restore, service/global cleanup, severe-log policy, second-run and fault proof;
- **Player:** exact scene profile, current successful BuildReport/output, disposable launch environment, ordered markers, severe-log scan, honest external termination;
- **Android:** unit/debug/release commands, current route state, retained transcript/reports/APK, failure diagnostics;
- **source/design:** actual rendered exact-source media, LFS binary retrieval, immutable hash/version mapping, provenance, accessibility, technical disposition, and user decision;
- **release:** parsed/enforced policy, exact event ranges, complete classification, implemented required checks, intentional failure proofs, and verified merge-control settings.

Skipped, unavailable, stale-base, stale-output, duplicate-workspace, pointer-only media, Console-only delivery, compile-only, wrong-policy green tests, missing XML/BuildReport, development fallback, or `continue-on-error` checks are not passing evidence.

## 12. Immediate next actions

```text
1. Implement the complete PR #218 contract in #156 correction and validate canonically.
2. Correct #153 transaction/load/marker/save/cross-scene lifecycle and declare the Bootloader lock.
3. Correct #163 to typed pure reads and no-save candidate mutations without repair.
4. Complete #136 real omitted-JSON/idempotency evidence.
5. Replace #152 destructive repair with a pure non-mutating compatibility view.
6. Correct #127 awaited scene unload/profile restoration and full fault/second-run matrix.
7. Remove #178 hidden Kingdom/Champion/load mutations and prove ShellFoundation unreachability/nonmutation.
8. Continue #155 from accepted PR #232 partial fixtures into parsed policy, exact event ranges, complete classifiers/security/artifacts, live proof PRs, and verified protection.
9. Correct #161 durable one-shot release rejection notice.
10. Correct merged #231 through #137 candidate ranking, rollback certainty, typed deletion, fault injection, and semantic preservation.
11. Bring PR #217 into exact PR #221 compliance before user creative review.
12. Permit the six pure planner/computation lanes without production integration or issue closure.
13. After #156/#153, implement #223 stable scene assets without Build Settings changes.
14. After #156, start only the contract-limited #183 catalog foundation.
15. Do not activate A1/G1/runtime, #165/#173 integration, persisted battle/Champion/boss/world/relationship/notification work, Player packaging, Android export, or release claims before prerequisites pass.
```
