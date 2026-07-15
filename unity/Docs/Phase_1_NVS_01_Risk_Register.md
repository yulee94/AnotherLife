# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-15  
**Audited current-main head:** `1a3ba60f539e7b42ca675b99808e88f71bca2236`  
**Active control state:** Phase 1 is paused behind #156 and the red Phase 0/1 foundation gate  
**Approved product intent:** issue #138 D1–D16  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

This register describes verified source and delivery risk. It supersedes assumptions based only on issue closure, PR merge state, source presence, compilation, or one-platform validation.

## Current risks

| ID | Severity | Risk | Evidence | Owner | Tracking/status |
| --- | --- | --- | --- | --- | --- |
| R1 | Critical | QuestDefinition serialized identity and malformed-asset coverage are not trusted | surviving type/GUID direction is known, but malformed/missing/non-authoritative assets can escape current discovery and canonical Unity evidence is absent | Codex engineering + GPT | **Active — #156 / PR #189 blocked** |
| R2 | Critical | Archived OMEN_1 may be mistaken for approved A1/runtime | archive conflicts with approved start, failure, reward, report, abandonment, localization, and resume | Codex narrative/content + GPT | **Contained — #128 starts only after #156** |
| R3 | Critical | NVS-01 consequences lack one atomic/idempotent transaction | resource, affinity, quest, artifact, and chapter domains save independently | GPT + Codex engineering | **Blocked — #133/#134 and foundations** |
| R4 | Critical | Save rotation/recovery can destroy last-known-good data | candidate validation, backup ranking, repair, deletion, offline progress, and publication remain incomplete | Codex engineering | **Blocked/open — #137 after #136/#152/#163** |
| R5 | High | Save semantic implementations may diverge from the merged policy | #211 lacks required old-JSON/idempotency evidence; #212 performs prohibited data-changing repair | GPT + Codex engineering | **Active — PRs #211/#212 blocked** |
| R6 | High | PlayMode validation can modify the developer profile or leak global state | PR #209 restores files before deferred scene teardown completes and helper teardown can set `Time.timeScale` to zero | Codex engineering | **Active — #127 / PR #209 blocked** |
| R7 | High | Bootloader lifecycle implementation is not transaction-safe | load token commits before successful load, save can cross-wire, post-install verification cannot rollback, marker inputs remain mutable/unsafe | Codex engineering + GPT | **Blocked — #153 / PR #203; lock held** |
| R8 | High | Production Unity Player lacks an authoritative scene flow | normal Build Settings do not yet prove production launch and named scene transitions | Codex engineering + GPT | **Blocked — #150 after #156** |
| R9 | High | Android↔Unity bridge is not end to end | no packaged export, mounted host, Unity consumer/result producer, or session identity | GPT + Codex engineering | **Deferred — #135** |
| R10 | High | Repository quality gates are only partially implemented | PR #210 has one green run, but policy source, event/range semantics, classifier coverage, security hardening, failure proofs, and protection remain incomplete | Codex engineering + GPT | **Active — #155 / PR #210 blocked** |
| R11 | Medium | Android dependency resolution could drift without repository changes | dynamic aliases were present on the former baseline | Codex engineering | **Resolved — #159 / PR #191 merged; locking/verification follows #155** |
| R12 | Medium | Release shell debug rejection may be invisible after fallback | PR #195 sanitizes the route but the subsequent Compose pass can clear the rejection notice | Codex engineering | **Active — #161 / PR #195 blocked** |
| R13 | Critical | Resource/Warzone Credit mutations permit signed/overflow exploits | negative spend can add value; malformed entries can throw or fabricate economy state | Codex engineering | **Ready/open — #163** |
| R14 | High | Realm identity can be invalid or overwritten without migration policy | `None`/undefined and existing-profile replacement require durable one-time selection semantics | Codex engineering + GPT | **Specification merged #202; implementation pending #173** |
| R15 | Critical | Battle simulation accepts invalid armies and mutates progression | null/empty request can win; simulation has side effects and weak encounter lifecycle | Codex engineering | **Blocked/open — #174** |
| R16 | Critical | Boss loot can fabricate, duplicate, or partially commit rewards | fallback loot, no result identity, credits before equipment persistence | Codex engineering | **Blocked/open — #168** |
| R17 | High | Territory/progression/reward domains trust malformed state | repeated capture, unsafe costs/counts/timers, duplicate identities, and nested saves | Codex engineering | **Blocked/open — #165/#166/#169/#171** |
| R18 | High | World/relationship/notification domains are non-persistent or parallel content authorities | hard-coded copy, nested saves, missing idempotency, and no visible typed delivery | Codex narrative/content + engineering | **Blocked/open — #172/#176/#177** |
| R19 | Critical | Production Kingdom controller remains mutating despite command containment | PR #208 removes visible cheats but update/refresh/start paths still complete progress, seed state, and load saves | Codex engineering | **Active — #178 / PR #208 blocked** |
| R20 | High | Champion, atlas, game-data, and customization remain weakly validated and mutable | non-finite state, silent fallback, hard-coded authority, live save mutation | Codex modes + GPT | **Blocked/open — #180/#181/#183/#184** |
| R21 | Medium | Old terrestrial prototype branches may be treated as design authority | stacked procedural visual work inherited rejected ancestors | Codex terrestrial-design + GPT | **Contained — #194; PR #162 reference-only** |
| R22 | Critical | Ownership chronology could be inverted again | earlier Gemini instruction was once treated as newer than the later Codex reassignment | GPT | **Resolved/controlled — PR #205 + dated decision record** |
| R23 | Medium | Status and issue metadata drift during concurrent work | merged PR #191 remained listed as open and PRs #208–#212 were absent from the previous status record | GPT | **Mitigated — current GPT status refresh; recurring control remains** |
| R24 | High | Duplicate-workspace Unity evidence can be mistaken for acceptance | PRs #189, #203, #208, #209, #211, and #212 report `C:\Users\MY\Documents\AnotherLife\unity` instead of the canonical project | Codex engineering + GPT | **Active — canonical reruns required** |
| R25 | Critical | Quest compatibility repair can discard or activate ambiguous state | PR #212 removes duplicate/null/blank rows, keeps the first duplicate, seeds Q1–Q5, and exposes unknown side quests | Codex engineering + GPT | **Active — #152 / PR #212 blocked** |
| R26 | High | Relationship normalization may appear complete without real old-JSON coverage | PR #211 manually nulls fields after construction but does not prove omitted Unity JSON, repeated normalization, or unrelated-field preservation | Codex engineering + GPT | **Active — #136 / PR #211 blocked** |
| R27 | High | Green CI may give false confidence before policy proof and protection | positive run exists, but intentional failures and required branch settings are not verified | Codex engineering + user/maintainer + GPT | **Active — #155 remains open** |

## D1–D16 controls

A1, G1, and runtime must preserve:

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
- offered rather than auto-accepted start;
- exact-node dialogue resume and duplicate-safe encounter/report recovery.

## Execution order

```text
#156 / PR #189 trusted QuestDefinition authority
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

Parallel focused lanes are limited to the existing non-overlapping PRs/issues:

```text
#127/#209   profile-safe PlayMode
#136/#211   relationship-field compatibility evidence
#152/#212   quest-state compatibility
#153/#203   Bootloader lifecycle
#155/#210   quality gates
#161/#195   Android release debug-route gating
#178/#208   Unity command containment
#163        economy integrity specification/implementation path
```

No later-phase implementation should be self-assigned while these earlier gates are red.

## Save dependency

```text
#136 accepted normalization evidence
          +
#152 non-mutating quest compatibility
          +
#163 compatible resource/credit semantics
          ↓
#137 candidate selection, recovery, explicit repair, deletion, and crash-safe persistence
```

The merged `Save_Semantic_Compatibility_Policy.md` controls every lane:

- preserve stable unknown data;
- disable malformed and duplicate groups;
- do not repair through ordinary queries;
- do not take first/max/sum duplicates;
- preserve raw evidence before data-changing repair;
- prefer cleaner candidates;
- use clone → validate → persist → publish;
- do not apply offline progress to an unvalidated candidate.

## Shared-file risk

Current lock:

```text
unity/Assets/AL/Scripts/Core/Bootloader.cs — held by draft PR #203
```

Other designated shared files remain unlocked:

```text
unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs
unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs
unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs
```

The first approved open PR declaring one holds the lock. Save fields require defaults, migration, old-save tests, semantic validation, fault recovery, and duplicate-safety evidence.

## Evidence policy

- build risk → exact commands, exit codes, complete error scan;
- asset risk → GUID/reference inventory, reimport, malformed/missing-script scan, and field preservation;
- test risk → discovered totals and retained XML/log artifacts;
- save/economy/reward risk → normal, recovery, fault, deletion, semantic, overflow, duplicate, reload, and idempotency matrices;
- contract risk → valid/invalid cases and implemented producer/consumer proof;
- packaging risk → actual Player/export build and launch transition;
- narrative/design risk → approved packet fidelity, references, provenance, readability, and user decision;
- integration risk → route/session/result/lifecycle evidence;
- player-experience risk → integrated playtest.

Skipped, unavailable, duplicate-workspace, compile-only, stale-base, or `continue-on-error` checks are not passing evidence.

## Immediate mitigation

```text
1. Correct PR #189 malformed-asset coverage and run canonical Unity evidence.
2. Correct PR #209 teardown ordering before any branch consumes PlayMode evidence.
3. Harden PR #210, run the proof matrix, and keep #155 open through protection evidence.
4. Bring PRs #211 and #212 into exact save-policy compliance before #137 starts.
5. Remove PR #208 hidden controller mutation paths and prove non-mutation.
6. Fix PR #195 durable visible fallback.
7. Complete PR #203 transaction, marker, lifecycle, and fault semantics while retaining the lock.
8. Keep A1/G1/runtime/Player claims paused until their prerequisites are accepted.
```
