# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-16  
**Audited current-main head:** `c2ef0c2c89a90f6d0c9bb91fa6f7ac552100ebbc`  
**Active control state:** Phase 1 is paused behind #156 and the red Phase 0/1 foundation gate  
**Approved product intent:** issue #138 D1–D16  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

This register supersedes assumptions based only on issue closure, PR merge, source/specification presence, generated scenes, compilation, test source, LFS pointers, Player output, console logs, or one-platform validation.

## 1. Active risks

| ID | Severity | Risk | Evidence/control state | Owner | Tracking |
| --- | --- | --- | --- | --- | --- |
| R1 | Critical | QuestDefinition serialized identity/malformed assets remain untrusted | Authority/schema and PR #218 validator contract are merged; PR #189 has not implemented Force-Text YAML/subasset/malformed-fixture validation or canonical evidence | Codex engineering + GPT | **#156 / PR #189 blocked** |
| R2 | Critical | Archived OMEN_1 may be mistaken for approved A1/runtime | Archive conflicts with approved D1–D16 start/failure/reward/report/abandonment/localization/resume semantics | Codex narrative/content + GPT | **Contained; #128 starts after #156** |
| R3 | Critical | NVS consequences lack one atomic/idempotent transaction | Gold, relationship, quest, artifact, chapter, ledger, and notification domains currently save/emit independently | GPT + Codex engineering | **#133/#134 blocked by foundations** |
| R4 | Critical | Save rotation/recovery can destroy last-known-good data | Candidate validation, backup ranking, semantic repair, deletion, offline progress, and publish ordering remain incomplete | Codex engineering | **#137 blocked by #136/#152/#163** |
| R5 | High | Open save-domain PRs violate merged semantic policy | #211 lacks real omitted-JSON/idempotency evidence; #212/#214 perform prohibited service/query repair | GPT + Codex engineering | **PRs #211/#212/#214 blocked** |
| R6 | High | PlayMode validation can modify profile or leak globals | PR #209 restores before deferred scene destruction and can restore uncaptured globals; failure/second-run/fault matrix incomplete | Codex engineering | **#127 / PR #209 blocked** |
| R7 | Critical | Bootloader service and cross-scene lifecycle are not transaction-safe | Load token/publication/marker/save identity faults remain; intended Boot scene is only lifecycle owner and first scene load destroys it while static services remain | Codex engineering + GPT | **#153 / PR #203 blocked; lock held** |
| R8 | High | No committed production scene flow or packageable Player | Only `Assets/Test.unity` exists; Build Settings empty; destructive generator intent not committed/hardened | Codex engineering + GPT | **#223 then #150; contracts merged** |
| R9 | High | Android↔Unity bridge is not end to end | No packaged export, mounted host, route/session/result producer-consumer proof | GPT + Codex engineering | **#135 deferred** |
| R10 | High | Quality gates can give false confidence | One green run exists; policy source, ranges/events, classification, security, failure fixtures, scene/catalog/source validators, and branch protection remain incomplete | Codex engineering + GPT + user/maintainer | **#155 / PR #210 blocked** |
| R11 | Medium | Android dependency resolution drift | Dynamic versions were removed and validated | Codex engineering | **Resolved by #159 / PR #191; locking follows #155** |
| R12 | Medium | Release shell debug-route rejection may be invisible | Sanitization can clear the notice on the second Compose pass | Codex engineering | **#161 / PR #195 blocked** |
| R13 | Critical | Economy implementation may fabricate/repair balances | PR #214 deletes/sums/clamps malformed rows, mutates reads, lacks typed no-save primitives, and leaves production batch/remainders unsafe | Codex engineering + GPT | **#163 contract merged; PR #214 blocked** |
| R14 | High | Realm identity can be invalid/overwritten | Durable one-time selection implementation awaits #137/#183 | Codex engineering + GPT | **#173 specification merged; implementation pending** |
| R15 | Critical | Battle simulation can accept invalid armies and mutate progression | Weak request/result/side-effect lifecycle | Codex engineering | **#174 open** |
| R16 | Critical | Boss loot can fabricate, duplicate, or partially commit rewards | Fallback rewards, no result identity, nested credit save, mutable inventory, precommit success copy | Codex engineering + GPT | **#168 open** |
| R17 | High | Progression/territory/reward domains trust malformed state and incomplete definitions | Repeated capture, unsafe arithmetic/timers, duplicate IDs, nested saves; catalog lacks consumer-referenced IDs and troop/research authority | Codex engineering + GPT | **#165/#166/#169/#171; #165 also depends #183** |
| R18 | High | Player-visible notification delivery is currently fictional | Raw strings/void methods only; service logs to Console; no queue, definition, localization, presenter, acknowledgement, receipt, or persistence | Codex engineering + narrative/content + GPT | **#177 contract merged PR #226; implementation pending** |
| R19 | Critical | Release controllers remain mutating despite command containment | Hidden lifecycle mutation, Champion post-clear credits, unsafe reset-to-Boot, incomplete reachability tests | Codex engineering | **#178 / PR #208 blocked** |
| R20 | High | Game-data authority remains mutable/incomplete/silent-fallback | Current service creates mutable definitions, discards story objects, omits IDs, returns null for troop/champion/skill; PR #220 contract not implemented | Codex engineering + source modes + GPT | **#183 blocked by #156** |
| R21 | Medium | Old terrestrial prototypes may be mistaken for source authority | Stacked procedural branches inherited rejected ancestors | Codex terrestrial-design + GPT | **Contained; #162 reference-only** |
| R22 | Critical | Ownership chronology could be inverted again | Earlier Gemini instruction was once treated as later authority | GPT | **Controlled by PR #205 + decision record** |
| R23 | Medium | Status metadata drifts under concurrent agents | Specifications/PRs/issues change within one cycle | GPT | **Mitigated by recurring current-main refresh** |
| R24 | High | Duplicate-workspace evidence may be cited as acceptance | Seven Unity PRs report `C:\Users\MY\Documents\AnotherLife\unity`; #214 latest exit 199/no XML | Codex engineering + GPT | **Canonical reruns required** |
| R25 | Critical | Quest compatibility can discard/activate ambiguous state | PR #212 deletes malformed rows, chooses duplicates, seeds generic quests, exposes unsupported side quests | Codex engineering + GPT | **#152 / PR #212 blocked** |
| R26 | High | Relationship normalization may appear complete without real old-save proof | PR #211 mutates constructed data rather than omitted Unity JSON; no repeated idempotency/unrelated-field proof | Codex engineering + GPT | **#136 / PR #211 blocked** |
| R27 | Critical | Progression may invent unauthoritative definitions | Missing `ManaShrine`/`Mine`, no public research authority, all troop lookups null, mutable definitions lack version/hash/provenance | Codex engineering + source modes + GPT | **#183 implementation then #165** |
| R28 | High | Terrestrial previews may be mistaken for approved creative/runtime authority | Three base sheets but nine variants; pointer-only review, incomplete schema/media/LFS/import evidence, no user approval | Codex terrestrial-design + GPT + user | **#194 / PR #217 blocked; PR #221 contract merged** |
| R29 | High | LFS pointer/prose may substitute for pixel review | Actual full-resolution exact-hash sheets are not directly rendered in current PR review surface | Codex terrestrial-design + GPT + user | **Direct exact-source review required** |
| R30 | Critical | Scene generator can overwrite assets and packaging policy | It recreates four scenes and replaces all Build Settings without dry run, GUID protection, rollback, drift validation, or idempotency proof | Codex engineering + GPT | **#223 blocked by #156/#153** |
| R31 | High | Player output may be cited without trustworthy current build/launch evidence | Stale output, missing BuildReport, wrong scene list, developer-profile launch, severe logs, or early kill can look successful | Codex engineering + GPT | **Controlled by PR #224; #150 pending** |
| R32 | High | Relationship services can corrupt/overclaim state | Arbitrary IDs, null/duplicate first-row selection, NaN rank fallthrough, unchecked ints, nested saves, hard-coded labels, dishonest persona ties | Codex engineering + narrative/content + GPT | **#176 contract merged PR #227; planner/service work pending** |
| R33 | High | World events announce nonexistent effects and vanish on reload | Raw enum/string/float countdown, hard-coded copy, no consumers, no persistence/correlation/recovery/tick owner | Codex engineering + narrative/content + GPT | **#172 contract merged PR #228; planner/service work pending** |

## 2. NVS-01 controls

A1/G1/runtime must preserve:

- offered rather than auto-accepted start;
- authored deployment node before arena request;
- transient encouraging failure/retry and nonterminal `FAILED`;
- Tear acquired once on arena success;
- manual report to Valerius;
- Gold, affinity, completion, and selected-realm Chapter 1 unlock exactly once at report conclusion;
- complete localization inventory;
- honest requested-capability classification;
- abandonment only outside active encounter;
- universal post-realm eligibility and Veil Watch Valerius;
- retained Tear presented and kept;
- exact-node resume and duplicate-safe encounter/report recovery.

Sequence:

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

## 3. Foundation dependency map

### Save transaction

```text
#136 + #152 + #163 implementation
  ↓
#137 candidate selection/recovery/persistence/deletion
  ↓
#133/#134 atomic consequence composition
```

### Asset/game-data

```text
#156
  ↓
#183 catalog foundation/source/service migration
  ↓
#165/#173/#180/#168/#184/#181 and narrative consumers
```

### Scene/Player

```text
#156 + #153
  ↓
#223 committed stable scenes
  ↓
#178 + corrected #127
  ↓
#150 three-scene Windows64 shell
  ↓
#135 Android export/host
```

### Notification

```text
#177 session queue
  ↓
#183 notification definitions + narrative/content source
  ↓
#223/#150 visible presenter integration
  ↓
#137 durable outbox/history
  ↓
focused caller migrations
```

### Relationships/NVS

```text
#176 planner + #183 identities/policies + #137 persistence
  ↓
typed service adapters/events/notifications
  ↓
#133/#134 owning ledger and atomic report
```

### World state

```text
#172 planner + #183 definitions/effects + #137 persistence
  + #153 lifecycle + #177 notifications + required consumer contracts
  ↓
committed world-event service/integrations
```

### Terrestrial source

```text
PR #217 technical completion
  ↓
exact user source approval
  ↓
#156 + #183 + owning runtime issue
  ↓
engineering integration + fidelity review + user integrated acceptance
```

## 4. Shared-file risk

Current lock:

```text
unity/Assets/AL/Scripts/Core/Bootloader.cs — PR #203
```

Unlocked designated files:

```text
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs
```

Pure planner/contract PRs claim none. #223/#150 cannot bypass the Bootloader lock. Later #137 and #183 service migrations must declare their locks explicitly.

## 5. Evidence policy

- **Build:** exact commands/exit, compiler scan, current BuildReport/output, stale-output exclusion.
- **Assets/scenes:** complete inventory, stable GUIDs, Force-Text/malformed/missing-script checks, import/reimport, descriptor/generator drift, nonoverwrite/idempotency/rollback, Build Settings ownership.
- **Tests:** discovered totals and retained XML/log artifacts.
- **Data/transactions:** normal, malformed, recovery, fault, duplicate, overflow/nonfinite, stale-plan, reload, event/save-count, notification, and idempotency matrices.
- **Catalog/contracts:** identity/version/source/hash/provenance, immutable results, generated drift, packaging, and consumer proof.
- **Player:** exact scene profile, current BuildReport/output, disposable profile, ordered markers, severe-log scan, timeout/exit/termination truth.
- **Source/design:** direct rendered exact source, immutable version/hash mapping, provenance, accessibility, technical disposition, explicit user decision.
- **Integration:** route/session/result/lifecycle and user playtest.

Not passing: skipped/unavailable, duplicate workspace, pointer-only media, stale Player output, missing XML/BuildReport, development fallback, Console log called delivery, float countdown called persistence, hard-coded label/copy called source approval, or `continue-on-error`.

## 6. Immediate mitigation

```text
1. Implement PR #218 in PR #189 and clear #156 canonically.
2. Complete PR #203 service transaction safety and cross-scene owner.
3. After #156/#153, implement #223 with no Build Settings change.
4. Rewrite PR #214 against the economy contract.
5. Correct PR #209 cleanup/failure behavior.
6. Harden PR #210 and run proof/protection evidence.
7. Correct PRs #211/#212 before #137.
8. Correct PR #208 hidden mutation/credits/reset/Champion reachability.
9. Fix PR #195 durable rejection notice.
10. Correct PR #217 before user creative review.
11. Start focused pure planners #177/#176/#172 without persistence/production callers.
12. After #156, begin only #183 catalog foundation.
13. After #223/#178/#127, implement #150 shell build/smoke.
14. Keep #165, A1/G1, #137/#134, terrestrial runtime, Champion packaging, Android export, and release claims behind prerequisites.
```
