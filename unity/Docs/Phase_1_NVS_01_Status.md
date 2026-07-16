# Phase 1 NVS-01 Status

**Status date:** 2026-07-16  
**Integration branch:** `main`  
**Audited current-main head:** `cbdb09f99c3f803a282e8582cd7375680ead3693`  
**Roadmap state:** Phase 1 remains paused behind reopened QuestDefinition authority issue #156 and the red Phase 0/1 foundation gate  
**Approved product intent:** issue #138 D1–D16  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

`AGENTS.md` is authoritative. This record separates specification, source presence, merge state, issue state, validation evidence, accepted behavior, creative approval, packaging evidence, and player-visible completion.

A merged PR, closed issue, green test suite, generated asset, Console log, uploaded LFS pointer, Player executable, or passing workflow is not acceptance by itself. Current-source verification and matching evidence supersede stale PR descriptions and automatic issue closure.

## 1. Current control summary

- The active product milestone remains **NVS-01**.
- The latest user decision assigns all future project work to **Codex only**. GPT and Android Studio receive no future task, review, or approval assignment.
- Codex now operates through coordination/review, narrative/content, terrestrial-design, and engineering modes. The user retains final creative, product, balance, irreversible-profile, playtest, milestone, and release approval.
- Historical GPT-authored specifications and review comments remain technical evidence. Open work must not wait for another GPT response; Codex coordination/review mode owns all future disposition and merge-readiness decisions.
- No approved A1 narrative packet is active. Archived OMEN_1 material remains historical reference only.
- #156 remains the first trusted-Unity-content gate. Draft correction PR #237 exists; its current blocking review identifies false-pass paths and canonical validation is still missing.
- #153 has active draft correction PR #241 and currently holds the exclusive `Bootloader.cs` soft lock. Its current blocking review identifies duplicate runtime ownership, malformed-marker safety, exact rollback, load/save success, cross-scene lifecycle, and fault-matrix gaps.
- The other post-merge correction issues remain open: #163, #136, #152, #127, #178, #161, #155, and #137.
- Three pull requests are open: correction drafts #237 and #241, and terrestrial source draft #217.
- The only committed Unity scene remains test-only `Assets/Test.unity`; normal Build Settings remain empty.
- The canonical Unity workspace remains `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity`. The directory name is historical and does not require Android Studio. Evidence from `C:\Users\MY\Documents\AnotherLife\unity` is development feedback, not closure evidence.
- PR #232 is accepted as partial #155 progress: terrestrial path classification, disposable local failure fixtures, and a proof/protection plan. #155 remains open through policy authority, correct event/range semantics, complete security/classification/artifacts, live failure proofs, a Unity validation model, and verified branch protection.
- PRs #238, #240, and #239 added binding **specifications**, not implementations, for progression, territory, and Champion customization.

## 2. Codex-only ownership state

### Codex coordination/review mode

Owns planning, dependency ordering, specifications, state/event/contract/save/test design, issue/PR triage, integration review, shared-file sequencing, status/risk/governance records, and merge readiness.

### Codex narrative/content mode

Owns narrative source, localization-facing meaning, continuity, stable narrative IDs, and narrative-fidelity correction.

### Codex terrestrial-design mode

Owns terrestrial creature/fauna visual-design source, source identity, and design-fidelity correction.

### Codex engineering mode

Owns Android and Unity code, runtime, gameplay, assets/import, scenes, saves/migrations/recovery, contracts/catalogs, builds, tests, CI, tooling, diagnostics, performance, and accessibility mechanics.

### User

Owns final product, creative, visual-design, balance, irreversible-profile, integrated playtest, milestone, and release approval.

New branches use only:

```text
codex/coordination-<scope>
codex/narrative-<scope>
codex/terrestrial-<scope>
codex/<engineering-scope>
```

`gpt/`, `android-studio/`, and `gemini/` are retired for new work.

## 3. Binding merged specifications

A merged specification is a contract, not implementation completion. Historical GPT authorship does not require future GPT approval; Codex coordination/review mode consumes or supersedes the artifact.

| Area | Binding artifact / PR | Current implementation state |
| --- | --- | --- |
| save semantics | `Save_Semantic_Compatibility_Policy.md` / #197 | current quest/economy/save implementations still violate the policy |
| Bootloader stack | `Bootloader_Service_Stack_Integrity_Spec.md` / #198 | #153 open; draft PR #241 blocked |
| release command containment | `Unity_Production_Command_Containment_Spec.md` / #200 | #178 open after current-source audit |
| durable realm selection | merged PR #202 | #173 remains pending save/catalog prerequisites |
| profile-safe PlayMode | merged PR #207 | #127 open after current-source audit |
| economy | `Economy_Integrity_Spec.md` / #215 | #163 open after current-source audit |
| QuestDefinition assets | `QuestDefinition_Asset_Authority_Validation_Spec.md` / #218 | #156 open; draft PR #237 blocked |
| game-data authority | `Game_Data_Catalog_Authority_Spec.md` / #220 | #183 implementation blocked by #156 |
| terrestrial source review | `Terrestrial_Source_Packet_Validation_Spec.md` / #221 | PR #217 draft/blocked; user review not ready |
| production scenes/Player | `Production_Scene_Player_Build_Spec.md` / #224 | #223/#150 blocked by #156/#153/#178/#127 |
| notification delivery | `Notification_Delivery_Contract_Spec.md` / #226 | pure queue/session phase authorized |
| relationship transactions | `Relationship_Integrity_Transaction_Spec.md` / #227 | pure planner phase authorized |
| world-state lifecycle | `World_State_Lifecycle_Transaction_Spec.md` / #228 | pure lifecycle/effect planner authorized |
| boss rewards | `Boss_Loot_Result_Transaction_Spec.md` / #230 | pure computation/application planner authorized |
| battle computation/results | `Battle_Computation_Result_Transaction_Spec.md` / #234 | pure deterministic simulator/planner authorized |
| Champion combat/encounter | `Champion_Combat_Encounter_Integrity_Spec.md` / #235 | pure actor/action/encounter planner authorized |
| progression orders | `Progression_Definition_Order_Transaction_Spec.md` / #238 | pure definition/state/order/reconciliation planner authorized |
| territory ownership/income | `Territory_Ownership_Income_Transaction_Spec.md` / #240 | pure definition/state/capture/income planner authorized |
| Champion customization | `Champion_Customization_Integrity_Spec.md` / #239 | pure catalog/state/draft/reversible-commit planner authorized |

## 4. Open pull requests and locks

| PR | Issue | Scope | Codex coordination/review disposition | Shared lock |
| --- | --- | --- | --- | --- |
| #237 | #156 | QuestDefinition Force-Text YAML authority validator | **Draft / blocked / changes required** | none |
| #241 | #153 | Bootloader load, marker, rollback, and lifecycle correction | **Draft / blocked / changes required** | `unity/Assets/AL/Scripts/Core/Bootloader.cs` |
| #217 | #194 | terrestrial source-design packet | **Draft / technically blocked / user review not ready** | none |

### PR #237 — current blockers

The architecture is materially improved, but the current head still requires:

- exact ordered field name and type validation for all twelve serialized QuestDefinition fields;
- exact `m_Script` mapping validation: `fileID: 11500000`, `type: 3`, valid 32-hex GUID, deterministic duplicate/extra-key handling;
- candidate-local validity accounting so malformed candidates never increment `ValidAssetCount`;
- YAML class/root-object validation and deliberate production-assembly filtering;
- exact current-head authority record and source inventory;
- update onto current `main` and complete canonical compile/EditMode/import/reimport/missing-script/GUID evidence.

Codex coordination/review mode owns future re-review. No A1, #183 production authority, #223 scene authority, or #150 Player packaging may cite #237 until the corrected head is accepted.

### PR #241 — current blockers

The branch fixes several prior defects but remains unsafe because:

- multiple Bootloaders can all tick production and save; load claiming does not elect one runtime owner;
- `TryBeginLoad() == false` conflates already-loaded and load-in-progress states;
- `Load()` success accepts invalid/nonterminal results and does not revalidate identity after loading;
- malformed/null/missing marker maps can still throw because root keys are directly indexed;
- required type and snapshot inventories remain mutable or under-validated;
- final-verification rollback cannot restore the exact prior registry snapshot;
- missing/mismatched diagnostics are collapsed and result lists are not immutable;
- pause/quit does not inspect typed save failure/status;
- cross-scene owner identity and the complete construction/publication/load/two-owner/fault matrix remain absent;
- all Unity evidence remains noncanonical.

Codex coordination/review mode owns future re-review. The `Bootloader.cs` lock remains active while #241 is open. #223, #150, lifecycle-sensitive #137 work, and NVS runtime remain blocked by #153.

### PR #217 — current blockers

Before user creative review, #217 must:

- use `Refs #194`, not close the issue;
- update onto current `main`;
- provide normalized source/version/profile/variant/media/hash/LFS/provenance identity;
- provide retained schema and deterministic manifest/media validation;
- prove clean binary retrieval and actual dimensions/hashes;
- distinguish delivered visual variants from text-only proposals;
- provide direct exact-source rendered review links;
- complete canonical Unity import evidence or move review-only media outside runtime `Assets`;
- separate technical, creative, naming/content, and later runtime-integration states.

Codex coordination/review and terrestrial-design modes own future technical and fidelity disposition. User creative approval remains required.

## 5. Post-merge implementations that remain unaccepted

These PRs are in Git history but do not satisfy their owning issue:

| Merged PR | Open issue | Retained useful direction | Live blocker |
| --- | --- | --- | --- |
| #189 | #156 | narrative QuestDefinition type/GUID direction | no accepted full YAML/subasset/schema validator |
| #203 | #153 | construct-before-publication and marker intent | no accepted transaction-safe load/save/cross-scene owner |
| #214 | #163 | some checked arithmetic helpers | reads repair state; no accepted typed no-save primitives |
| #211 | #136 | isolated relationship reload test | no real omitted-JSON/idempotency/unrelated-field proof |
| #212 | #152 | null-safe guard intent | destructive query repair, seeding, unsupported IDs, rejected-path saves |
| #209 | #127 | external profile snapshot intent | scene/callback quiescence, global cleanup, second-run/fault matrix absent |
| #208 | #178 | obvious command cheats removed | startup/load/update/dashboard/Champion paths still mutate |
| #195 | #161 | compile-time debug route gate | visible rejected-route notice is not durable |
| #210 + #232 | #155 | stable workflow jobs, one green run, local fixture proof | policy/event/security/live-proof/protection/Unity gaps remain |
| #231 | #137 | durable temp write and file-operation seam | destructive semantics, weak ranking, uncertain rollback/deletion, no full fault matrix |

No downstream issue may cite these merges or prior automatic closures as accepted behavior.

## 6. Active correction lanes

Correction branches may proceed independently where files do not overlap:

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

No correction may broaden into NVS, content/design authorship, balance, scenes, Android embedding, another domain, or another designated shared file without a Codex coordination/review dependency and lock decision.

## 7. Pure nonmutating implementation lanes

The following first phases may proceed now because they do not touch production services, saves, callers, catalogs, scenes, UI, Android, balance, or designated shared files:

| Issue | Branch | Allowed first phase |
| --- | --- | --- |
| #177 | `codex/notification-contract-queue` | immutable notification/session queue/dedupe/receipt/fake presenter models and tests |
| #176 | `codex/relationship-contract-planner` | immutable identity/policy/snapshot/query/classification/mutation plans and fakes |
| #172 | `codex/world-state-contract-planner` | immutable definitions/instances/UTC lifecycle/effect plans and fake consumers |
| #168 | `codex/boss-loot-contract-planner` | deterministic reward computation, inventory snapshots, application plans, vectors and fakes |
| #174 | `codex/battle-contract-simulator` | immutable requests/snapshots, validation, fixed math, deterministic pure computation and vectors |
| #180 | `codex/champion-combat-contract-planner` | finite actor/action/boss/encounter contracts, transition planners, fake participants and tests |
| #165 | `codex/progression-contract-planner` | immutable definitions/state views/orders/reconciliation/effect/production planners and fakes |
| #166 | `codex/territory-contract-planner` | immutable territory definitions/state/query/authorization/capture/income planners and fakes |
| #184 | `codex/customization-contract-planner` | immutable catalog/raw/effective/draft/plan models, strict validators and fake reversible adapters |

Each first PR must use `Refs`, not `Fixes`, and must not close its issue.

## 8. Dependency controls

### 8.1 Trusted Unity and NVS

```text
#156 accepted Force-Text QuestDefinition authority
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

A1 must implement issue #138 D1–D16 exactly. Archived OMEN_1 must not become a compatibility contract through tests, fallback catalogs, or runtime code.

### 8.2 Save and economy

```text
#136 real omitted-JSON/idempotent relationship compatibility
          +
#152 non-mutating quest compatibility
          +
#163 typed non-repairing economy reads/no-save mutations
          ↓
#137 candidate inventory/ranking, rollback certainty, explicit repair,
     verified deletion, ledger/outbox, and crash-safe persistence
```

Controls: preserve malformed/unknown evidence; disable ambiguous domains; never first/max/sum/clamp as silent repair; clone → validate → persist/verify → publish; return commit-uncertain rather than guessing durability.

### 8.3 Game data and progression/world systems

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

Before #183 authority exists, pure planners may validate models but production code must fail closed rather than invent definitions, IDs, costs, maximums, aliases, rewards, content, or appearance options.

### 8.4 Battle, Champion, and rewards

```text
accepted #183 source snapshots
        +
accepted #163 no-save economy
        +
accepted #137 candidate ledger/outbox
        +
accepted #165 troop/research snapshots
        +
#174 pure battle result/application contract
        +
#180 Champion encounter completion
        ↓
#168 deterministic persisted boss rewards
        ↓
#177 committed-result presentation
```

Practice/preview computation never mutates progression or value. Authoritative consequences require stable operation/result IDs and one durable candidate transaction.

### 8.5 Scenes, PlayMode, Player, and Android

```text
#156 accepted Unity asset authority
        +
#153 accepted single cross-scene lifecycle owner
        ↓
#223 non-destructive generator + stable committed production scenes
        +
#178 non-mutating ShellFoundation controllers/transitions
        +
#127 lifecycle-safe profile-isolated PlayMode
        ↓
#150 exact three-scene Windows64 Development Player + disposable launch smoke
        ↓
#135 Android export/host lifecycle
```

The first Player profile remains exactly:

```text
0 Assets/AL/Scenes/Boot.unity
1 Assets/AL/Scenes/RealmSelection.unity
2 Assets/AL/Scenes/Kingdom.unity
```

`Assets/Test.unity` and `Assets/AL/Scenes/ChampionArena.unity` remain excluded.

### 8.6 Release verification

#155 remains open through:

1. parsed/machine-authoritative policy and exact PR/push diff ranges;
2. complete ownership/impact/shared-lock/scene/dependency/security/artifact checks;
3. intentional live pass/fail PR proof matrix;
4. approved Unity runner/manual evidence model;
5. configured and verified branch protection/merge controls.

#161 remains open until a rejected release debug route produces one durable visible technical notice and never opens the debug screen.

## 9. Shared-file state

Current exclusive soft lock:

```text
unity/Assets/AL/Scripts/Core/Bootloader.cs
  owner: draft PR #241 / codex/bootloader-lifecycle-contract-correction
```

Other designated files are currently unlocked:

```text
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs
```

The first approved open PR declaring an unlocked designated file obtains its lock. Merged/closed PRs release locks. No conflict resolution may discard valid current fields, services, tests, assets, contracts, or registrations.

## 10. Evidence rules

Evidence must match the risk:

- **coordination/review:** current main, current source, exact issues/PRs, dependencies, locks, acceptance criteria, and evidence quality;
- **build:** exact command/version/base/head/exit code, complete compiler/error scan, retained log;
- **test:** exact discovered/passed/failed/skipped totals and retained XML/logs;
- **asset:** Force-Text/GUID/script-file-ID/local-file-ID/class/root/schema inventory, import/reimport, missing-script and field-preservation proof;
- **save/economy/progression/territory/reward/customization:** immutable source/state identities, normal/recovery/fault/replay/conflict/delete/overflow/downgrade matrices, exact save/event/notification counts;
- **scene/PlayMode:** exact scene unload and callback quiescence before profile restore, service/global cleanup, second run and operation faults;
- **catalog:** schema/version/hash/provenance, immutable snapshots, valid/invalid vectors, generated-contract drift, packaged producer/consumer proof;
- **Player:** current BuildReport/output, exact profile, disposable launch environment, ordered markers, severe-log scan, honest termination;
- **Android/CI:** correct event range, current PR body/state, retained transcript/reports/APK, intentional failures, protection evidence;
- **source/design:** exact rendered full-resolution media tied to immutable hashes, binary retrieval, provenance, accessibility, technical disposition, and user decision.

Skipped, stale-base, duplicate-workspace, pointer-only, Console-only, compile-only, wrong-policy green, missing-artifact, fallback, swallowed-exception, or `continue-on-error` results are not passing evidence.

## 11. Immediate next actions

```text
1. Codex coordination/review rechecks all open PRs from current main and preserves the Bootloader lock.
2. Correct PR #237's exact type/script mapping, validity accounting, metadata/assembly filtering,
   update it, and run the complete canonical #156 matrix.
3. Correct PR #241's single-owner/load/save/marker/rollback/diagnostic/scene lifecycle,
   update it, and run canonical compile/EditMode plus corrected #127 PlayMode evidence.
4. Correct #163 to typed pure reads and checked no-save candidate mutations without repair.
5. Complete #136 real omitted-JSON/idempotency evidence and replace #152 query repair with a pure view.
6. Correct #137 candidate ranking, rollback certainty, typed verified deletion, fault injection,
   and semantic preservation after #136/#152/#163.
7. Correct #127 profile restoration and #178 hidden Kingdom/Champion mutations before scene/Player work.
8. Complete #155 policy/live-proof/protection and the narrow #161 visible-notice correction.
9. Bring PR #217 into exact PR #221 compliance before user creative review.
10. Permit the nine pure planner lanes only within their nonmutating boundaries.
11. After #156/#153, begin #223 stable scene assets; after #156, begin only the contract-limited #183 catalog foundation.
12. Do not activate A1/G1/runtime, persisted progression/territory/customization/reward/world/relationship work,
    Player packaging, Android export, or release claims before their prerequisites pass.
```

No next GPT or Android Studio action exists.