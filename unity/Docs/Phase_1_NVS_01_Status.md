# Phase 1 NVS-01 Status

**Status date:** 2026-07-16  
**Integration branch:** `main`  
**Audited current-main head:** `c2ef0c2c89a90f6d0c9bb91fa6f7ac552100ebbc`  
**Roadmap state:** Phase 1 remains paused behind #156 and the red Phase 0/1 foundation gate  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

`AGENTS.md` is authoritative. This record distinguishes source presence, specification state, implementation state, validation evidence, creative approval, packaging evidence, and user acceptance. None of the following is sufficient by itself: issue closure, PR merge, source/file presence, generated-but-uncommitted scenes, test source, compilation, LFS pointer, Player executable, console log, or one-platform validation.

## 1. Current control summary

- The active product milestone is NVS-01.
- No approved A1 narrative packet is active; archived OMEN_1 material is historical reference only.
- #156 is the first blocking technical gate. The authority/schema direction and validator specification are merged, but PR #189 has not implemented or canonically validated them.
- The canonical Unity workspace is `D:\260711\MY\AndroidStudioProjects\AnotherLife\unity`. Duplicate-workspace evidence is blocked.
- PR #203 holds the only active designated lock: `Bootloader.cs`.
- The only committed Unity scene remains test-only `Assets/Test.unity`; normal Build Settings remain empty.
- Android dependency reproducibility #159 is complete through PR #191.
- Economy (#163), game-data authority (#183), terrestrial source validation (#194), production scenes/Player build (#150), notifications (#177), relationships (#176), and world-state lifecycle (#172) now have merged GPT contracts. Their implementation/user gates remain open.
- Ten implementation/tooling/source-design PRs are open. All remain draft/blocked.

## 2. Ownership model

- **GPT:** coordination, specifications, state/contract/save/test design, review, status, risk, sequencing, and merge readiness.
- **Codex narrative/content:** narrative source, player-facing copy, localization source, and fidelity.
- **Codex terrestrial-design:** terrestrial visual-design source and fidelity.
- **Codex engineering:** Android, Unity, runtime, assets/import, saves, builds, tests, CI, and tooling.
- **User:** final product, creative, visual-design, balance, irreversible-profile, playtest, milestone, and release approval.

Android Studio and Unity are tools. `android-studio/` and `gemini/` are retired prefixes for new work.

## 3. Binding specifications and completed foundation records

| Area | Merged authority |
| --- | --- |
| Unity compilation baseline | #145 / PR #147 |
| Android `Quest` positional compatibility | PR #190 |
| Repository quality-gate policy | PR #192 |
| Save semantic compatibility/candidate selection | PR #197 |
| Bootloader stack/lifecycle specification | PR #198 |
| Unity command containment | PR #200 |
| Durable realm selection | PR #202 |
| Ownership decision record | PR #205 |
| Profile-safe PlayMode specification | PR #207 |
| Android dependency reproducibility | #159 / PR #191 |
| Economy integrity | `Economy_Integrity_Spec.md`, PR #215 |
| QuestDefinition YAML/subasset validation | `QuestDefinition_Asset_Authority_Validation_Spec.md`, PR #218 |
| Game-data catalog/immutable queries | `Game_Data_Catalog_Authority_Spec.md`, PR #220 |
| Terrestrial source packet validation | `Terrestrial_Source_Packet_Validation_Spec.md`, PR #221 |
| Production scene/Player build | `Production_Scene_Player_Build_Spec.md`, PR #224 |
| Typed notification delivery | `Notification_Delivery_Contract_Spec.md`, PR #226 |
| Relationship integrity/transaction planning | `Relationship_Integrity_Transaction_Spec.md`, PR #227 |
| World-state lifecycle/effects | `World_State_Lifecycle_Transaction_Spec.md`, PR #228 |

A merged specification is not implementation completion.

## 4. Open pull requests

| PR | Issue | Scope | Current disposition | Shared lock |
| --- | --- | --- | --- | --- |
| #189 | #156 | QuestDefinition authority safeguards | **Draft / blocked** | none |
| #195 | #161 | Android debug-route release gating | **Draft / blocked** | none |
| #203 | #153 | Bootloader stack and cross-scene lifecycle | **Draft / blocked** | `Bootloader.cs` |
| #208 | #178 | Unity command/transition containment | **Draft / blocked** | none |
| #209 | #127 | profile-safe representative PlayMode smoke | **Draft / blocked** | none |
| #210 | #155 | repository/Android quality gates | **Draft / blocked** | none |
| #211 | #136 | relationship-field old-save regression evidence | **Draft / blocked** | none |
| #212 | #152 | quest save compatibility | **Draft / blocked** | none |
| #214 | #163 | economy resource/credit integrity | **Draft / blocked** | none |
| #217 | #194 | terrestrial design source foundation | **Draft / blocked** | none |

## 5. Live blocker by open PR

### #189 — QuestDefinition authority

Accepted: narrative type/GUID, exact historical 12-field equivalence, one production type, and removed-root GUID prohibition.

Still required:

- consume the merged Force-Text YAML/subasset validator specification;
- detect unloadable/missing/zero/removed/unrelated script references by disk scan and local file ID;
- validate exact field schema, IDs, type/GUID, malformed fixtures, valid create/import/reimport round trip;
- rebase and run canonical compile/EditMode/reimport/missing-script/GUID evidence.

No A1, #183 implementation, #223 scene authoring, or Player work may claim a trusted Unity asset baseline before #156 completes.

### #195 — Android release debug-route gating

Accepted: compile-time gate, typed route rejection, back-stack sanitization, resolved entry identity, stable preview seam.

Still required: preserve the visible rejection notice through the second Compose pass, add stabilized UI/state coverage, rebase, and rerun unit/debug/release evidence.

### #203 — Bootloader stack and lifecycle

Still required:

- commit load ownership only after successful load;
- retain rollback-capable publication/post-install verification;
- immutable marker/inventory and nonthrowing malformed-marker behavior;
- validate save-service identity at pause/quit;
- choose and prove one cross-scene lifecycle owner model;
- keep exactly one valid owner and exact service references through Boot → RealmSelection → Kingdom → ChampionArena → Kingdom;
- prove load once, continued production ticking, continued pause/quit saves, duplicate-owner handling, failed-load retry, scene unload ordering;
- canonical complete fault/lifecycle validation.

The generated Boot scene is currently the only intended scene containing `Bootloader`; the first scene load would destroy the lifecycle owner while static services remain. #223/#150 are blocked until this is resolved under PR #203's lock.

### #208 — command/transition containment

Visible prototype commands were removed, but:

- controller `Update`/dashboard/start paths still mutate or seed state;
- Champion Arena proximity credits remain reachable after clear;
- reset-to-Boot remains unsafe;
- release reachability, controller lifecycle, missing-service, accessibility, reload/idempotency, and Player hierarchy evidence are incomplete;
- branch/evidence remain stale/duplicate-workspace.

ShellFoundation requires ChampionArena transition and reset handler to be unavailable/unreachable without hidden bypass.

### #209 — profile-safe PlayMode

External snapshot direction is correct, but:

- teardown can restore globals never captured;
- profile restoration can race deferred scene destruction;
- representative scene unload is not proved;
- cleanup timeout/failure, assertion/log failure, second-run, file-operation faults, timestamps/attributes, incoming scene, and final service-state tests are incomplete;
- canonical rebase/evidence is missing.

No other PR may cite #209 as passing PlayMode evidence until corrected.

### #210 — quality gates

One live run passed the three Phase A jobs, but the PR still needs:

- machine policy as actual source or deterministic drift tests;
- correct PR/push range and event semantics;
- complete owner/impact/shared-lock/current-head/ownership/build-settings/scene-profile reporting;
- fail-closed diff discovery;
- stronger action/permission/credential/timeout/Android transcript/cache/KSP checks;
- `Refs #155`, current-main rebase, intentional pass/fail proof PR matrix, and branch-protection evidence;
- later consumption of shared scene/catalog/source validators rather than copied lists.

### #211 — relationship-field normalization evidence

Useful real service mutation/reload test exists, but #136 still requires omitted-field Unity JSON fixtures, repeated normalization/serialization idempotency, unrelated-field preservation, current rebase, and canonical complete Unity evidence.

### #212 — quest compatibility

Current implementation still violates the merged save policy by deleting malformed rows, choosing/removing duplicates, seeding Q1–Q5 from an empty legacy list, allowing unsupported side-quest behavior, and leaving side effects on rejected paths. It needs a nonmutating compatibility view, disabled duplicate groups, exact preservation/idempotency/definition-return tests, and canonical evidence.

### #214 — economy integrity

Current implementation still performs prohibited service-local repair: deleting null rows, summing duplicates, clamping negative balances, fabricating `long.MaxValue`, mutating reads, and retaining nested credit saves. It lacks typed no-save primitives, core/optional authority, pure reads, atomic production/remainders, complete tests, and canonical XML. A contract-first rewrite is required.

### #217 — terrestrial source

Written source boundaries are useful, but technical user-review readiness still requires:

- `Refs #194`, current rebase;
- retained schema/semantic/media validator;
- normalized source asset identity including SHA-256/LFS/dimensions/provenance/review links;
- clean LFS retrieval and direct rendered full-resolution sheets;
- six text-only variants marked proposed or supplied visual source;
- working labels/tags explicitly nonlocalized/nonruntime;
- complete generation/edit/input/license record;
- docs-only media placement or canonical Unity import/no-runtime-package proof;
- separate technical, creative, naming, and runtime states.

GPT has not inspected/approved the actual pixels; user creative approval remains pending after technical completion.

## 6. Unblocked pure contract/planner implementation lanes

These lanes may proceed as focused, nonmutating PRs from current main when they do not conflict with #156 validation or another open PR.

### #177 — typed session notification queue

Branch:

```text
codex/notification-contract-queue
```

Scope only: immutable definition/request/enqueue/receipt/action models, injected resolver/clock, deterministic 64-record session queue, correlation dedupe/capacity/state machine, fake presenter registration, legacy wrapper inventory, EditMode tests.

No UI, content, scenes, save persistence, caller migration, Android, `Bootloader.cs`, or `ServiceLocator.cs`.

### #176 — relationship snapshot/planner

Branch:

```text
codex/relationship-contract-planner
```

Scope only: immutable identity/policy/query/classification/plan models, pure affinity/faction/persona validators and planners, honest persona tie/all-zero result, stale-plan/fake target seams, tests/inventory.

No production service mutation, saves, callers, content, scenes, Android, or shared files.

### #172 — world-state lifecycle/effect planner

Branch:

```text
codex/world-state-contract-planner
```

Scope only: immutable definition/effect/instance/snapshot/request/plan models, injected UTC clock/resolver, one-active-global start/end/cancel/reconcile planning, fake effect consumers, stale/idempotency/fake target tests, inventory.

No production service mutation, saves, real consumers, notifications/content, scenes, Android, effect/balance values, or shared files.

## 7. Core dependency chains

### 7.1 Save/persistence

```text
#136 accepted old-save normalization evidence
          +
#152 nonmutating quest compatibility
          +
#163 typed nonrepairing economy implementation
          ↓
#137 crash-safe candidate selection, recovery, repair, deletion, persistence
```

All lanes follow `Save_Semantic_Compatibility_Policy.md`: preserve stable unknown data, disable malformed/duplicate groups, no ordinary-query repair, quarantine before data-changing repair, cleaner candidate preference, clone → validate → persist → publish, and no offline mutation on an unvalidated candidate.

### 7.2 Asset/game-data/progression

```text
#156 trusted QuestDefinition/asset baseline
          ↓
#183 catalog foundation (no production switch/shared-file claim)
          ↓
approved source catalogs
          ↓
LocalGameDataService migration with declared lock
          ↓
#165/#173/#180/#168/#184/#181 and chapter/quest migrations
```

The first #183 PR is infrastructure only: manifest/envelopes, typed load/query/diagnostics, immutable snapshots, validators, packaged source seams, hashes/schema tests, inventory.

### 7.3 Production scenes/Player

```text
#156 trusted asset baseline
          +
#153 accepted cross-scene lifecycle owner
          ↓
#223 non-destructive generator + four committed stable scenes
          ↓
#178 ShellFoundation Champion/reset unreachability
          +
corrected #127 PlayMode evidence
          ↓
#150 exact three-scene Build Settings + Windows64 build + isolated Boot→RealmSelection smoke
          ↓
#135 Android Unity export/host
```

ShellFoundation contains Boot, RealmSelection, Kingdom only. `Assets/Test.unity` and ChampionArena are excluded.

### 7.4 Relationship/NVS transaction

```text
#176 pure planner
          +
#163 typed economy
          +
#152 quest compatibility
          +
#137 clone/persist/publish
          +
#177 typed notification/outbox
          ↓
#133 G1 atomic/idempotent report contract
          ↓
#134 implementation
```

The approved future report composes +500 Gold, +5 Valerius affinity, quest completion, selected-realm Chapter 1 unlock, operation ledger, and notification outbox once. No unapproved faction/persona consequence is invented.

### 7.5 World-state

```text
#172 pure lifecycle/effect planner
          +
#183 definitions/effect profiles
          +
#137 persistence/history/ledger
          +
#153 runtime lifecycle
          +
#177 notifications
          +
required consumer contracts (#163/#165/#166/#174/...)
          ↓
committed world-event service and integrations
```

Initial policy permits one active `global_primary` event. Current enum/string/float countdown and hard-coded announcements are legacy only.

### 7.6 Terrestrial source

```text
PR #217 exact-source technical completion
          ↓
user approval for exact source version/profile/variant IDs
          ↓
#156 + #183 + owning runtime issue
          ↓
separate engineering integration + GPT review + Codex design-fidelity review
          ↓
user integrated acceptance
```

Source IDs, working labels, variant intent, biome tags, images, and hashes are not spawn/AI/combat/reward/save/lore/runtime authority.

## 8. NVS-01 sequence

```text
#156 trusted QuestDefinition authority
  ↓
#128 Codex narrative/content A1
  ↓
#133 GPT G1
  ↓
accepted focused foundations
  ↓
#134 Codex engineering C1–C4
  ↓
G2 GPT → A2 Codex narrative/content → U1 user
```

A1 must preserve issue #138 D1–D16: offer/acceptance, deployment node, transient failure/retry, Tear acquisition, manual report, atomic consequences, abandonment, localization, exact resume, and duplicate-safe recovery.

## 9. Shared-file state

Current lock:

```text
unity/Assets/AL/Scripts/Core/Bootloader.cs — PR #203
```

Currently unlocked designated files:

```text
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs
```

The first approved open PR declaring a designated file holds the lock. Planner/contract PRs do not claim these files. #223/#150 may not edit or bypass `Bootloader.cs`; later #183 service migration and #137 save integration must declare their own locks.

## 10. Evidence rules

- **Build:** exact commands, exit codes, compiler scan, current BuildReport, output inventory, stale-output exclusion.
- **Assets/scenes:** complete inventory, stable GUIDs, Force-Text/malformed/missing-script checks, import/reimport, descriptor drift, generator non-overwrite/idempotency, Build Settings ownership.
- **Tests:** discovered totals and retained XML/log artifacts.
- **Save/economy/reward/relationships/world state:** normal, malformed, recovery, fault, duplicate, overflow/nonfinite, stale plan, reload, event/save-count, notification, and idempotency matrices.
- **Catalog/contracts:** identity, schema/content version, source revision, raw hashes, provenance, immutable query results, generated-contract drift, packaging, and consumer proof.
- **Player packaging:** exact enabled scene profile, current successful BuildReport/output, disposable profile, ordered markers, severe-log scan, timeout/exit, honest external termination.
- **Source/design:** direct rendered exact source, immutable source version/hash mapping, provenance, accessibility, technical disposition, and explicit user decision.
- **Integration/player experience:** route/session/result/lifecycle evidence and user playtest.

Skipped, unavailable, duplicate-workspace, pointer-only media, stale Player output, missing XML/BuildReport, development fallback, console log represented as delivery, float countdown represented as persistence, or `continue-on-error` is not passing evidence.

## 11. Immediate next actions

```text
1. Implement PR #218's validator contract in PR #189 and clear #156 with canonical evidence.
2. Complete PR #203's stack transaction safety and explicit cross-scene lifecycle owner.
3. After #156/#153, implement #223 with no Build Settings change.
4. Rewrite PR #214 against the economy contract.
5. Correct PR #209 cleanup ordering and failure matrix.
6. Harden PR #210 and run the proof/protection matrix.
7. Correct PRs #211/#212 before #137 starts.
8. Correct PR #208 controller/direct-credit/reset/Champion reachability.
9. Fix PR #195 durable rejection notice.
10. Correct PR #217 and request user creative review only after technical completion.
11. Start focused pure planner lanes #177/#176/#172 without touching persistence or production callers.
12. After #156, begin only the contract-limited #183 catalog foundation.
13. After #223/#178/#127, implement #150's three-scene Windows64 shell build and isolated launch smoke.
14. Keep #165 reconnection, A1/G1, #137/#134, terrestrial integration, Champion packaging, Android export, and release claims behind their prerequisites.
```
