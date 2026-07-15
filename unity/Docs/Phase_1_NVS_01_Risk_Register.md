# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-15  
**Audited current-main head:** `dd865f077aed5a5543eab8dfff5138e7fbc9e9d4`  
**Active control state:** Phase 1 is paused behind #156 and the red Phase 0/1 foundation gate  
**Approved product intent:** issue #138 D1–D16  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

This register describes verified source and delivery risk. It supersedes assumptions based only on issue closure, PR merge state, source presence, specification presence, compilation, LFS pointer presence, or one-platform validation.

## Current risks

| ID | Severity | Risk | Evidence | Owner | Tracking/status |
| --- | --- | --- | --- | --- | --- |
| R1 | Critical | QuestDefinition serialized identity and malformed-asset coverage are not yet trusted | authority/type/GUID/schema direction is accepted and PR #218 merged the complete Force-Text YAML/subasset validator contract, but PR #189 has not implemented or canonically validated it | Codex engineering + GPT | **Active — #156 / PR #189 blocked; spec merged PR #218** |
| R2 | Critical | Archived OMEN_1 may be mistaken for approved A1/runtime | archive conflicts with approved start, failure, reward, report, abandonment, localization, and resume | Codex narrative/content + GPT | **Contained — #128 starts only after #156** |
| R3 | Critical | NVS-01 consequences lack one atomic/idempotent transaction | resource, affinity, quest, artifact, and chapter domains save independently | GPT + Codex engineering | **Blocked — #133/#134 and foundations** |
| R4 | Critical | Save rotation/recovery can destroy last-known-good data | candidate validation, backup ranking, repair, deletion, offline progress, and publication remain incomplete | Codex engineering | **Blocked/open — #137 after #136/#152/#163** |
| R5 | High | Save semantic implementations may diverge from merged policy | #211 lacks required old-JSON/idempotency evidence; #212 and #214 perform prohibited data-changing repair | GPT + Codex engineering | **Active — PRs #211/#212/#214 blocked** |
| R6 | High | PlayMode validation can modify the developer profile or leak global state | PR #209 restores files before deferred scene teardown completes and helper teardown can set `Time.timeScale` to zero | Codex engineering | **Active — #127 / PR #209 blocked** |
| R7 | High | Bootloader lifecycle implementation is not transaction-safe | load token commits before successful load, save can cross-wire, post-install verification cannot rollback, marker inputs remain mutable/unsafe | Codex engineering + GPT | **Blocked — #153 / PR #203; lock held** |
| R8 | High | Production Unity Player lacks an authoritative scene flow | normal Build Settings do not yet prove production launch and named scene transitions | Codex engineering + GPT | **Blocked — #150 after #156** |
| R9 | High | Android↔Unity bridge is not end to end | no packaged export, mounted host, Unity consumer/result producer, or session identity | GPT + Codex engineering | **Deferred — #135** |
| R10 | High | Repository quality gates are only partially implemented | PR #210 has one green run, but policy source, event/range semantics, classifier coverage, security hardening, failure proofs, and protection remain incomplete | Codex engineering + GPT | **Active — #155 / PR #210 blocked** |
| R11 | Medium | Android dependency resolution could drift without repository changes | dynamic aliases were present on the former baseline | Codex engineering | **Resolved — #159 / PR #191 merged; locking/verification follows #155** |
| R12 | Medium | Release shell debug rejection may be invisible after fallback | PR #195 sanitizes the route but the subsequent Compose pass can clear the rejection notice | Codex engineering | **Active — #161 / PR #195 blocked** |
| R13 | Critical | Economy implementation may exploit or fabricate balances while claiming repair | PR #214 deletes null rows, sums duplicates, clamps negative balances to zero and duplicate overflow to `long.MaxValue`, mutates reads, lacks typed no-save primitives, and leaves production unsafe | Codex engineering + GPT | **Active — #163 contract merged PR #215; PR #214 blocked** |
| R14 | High | Realm identity can be invalid or overwritten without migration policy | `None`/undefined and existing-profile replacement require durable one-time selection semantics | Codex engineering + GPT | **Specification merged #202; implementation pending #173** |
| R15 | Critical | Battle simulation accepts invalid armies and mutates progression | null/empty request can win; simulation has side effects and weak encounter lifecycle | Codex engineering | **Blocked/open — #174** |
| R16 | Critical | Boss loot can fabricate, duplicate, or partially commit rewards | fallback loot, no result identity, credits before equipment persistence | Codex engineering | **Blocked/open — #168** |
| R17 | High | Territory/progression/reward domains trust malformed state and incomplete definitions | repeated capture, unsafe costs/counts/timers, duplicate identities, nested saves; current authority lacks `ManaShrine`/`Mine`, research query, and troop definitions | Codex engineering + GPT | **Blocked/open — #165/#166/#169/#171; full #165 depends #183 implementation** |
| R18 | High | World/relationship/notification domains are non-persistent or parallel content authorities | hard-coded copy, nested saves, missing idempotency, and no visible typed delivery | Codex narrative/content + engineering | **Blocked/open — #172/#176/#177** |
| R19 | Critical | Production controllers remain mutating despite command containment | PR #208 removes visible Kingdom cheats but update/refresh/start paths mutate; Champion Arena recurring proximity credits remain reachable after clear | Codex engineering | **Active — #178 / PR #208 blocked** |
| R20 | High | Game-data/runtime definitions remain mutable, nullable, incomplete, and silently fallback-prone | PR #220 merged the catalog/immutable-query authority contract, but current `LocalGameDataService` still creates mutable definitions, discards story objects, omits IDs, and returns null for troop/champion/skill | GPT + Codex engineering + source modes | **Active — #183 spec merged PR #220; implementation blocked by #156** |
| R21 | Medium | Old terrestrial prototype branches may be treated as design authority | stacked procedural visual work inherited rejected ancestors | Codex terrestrial-design + GPT | **Contained — #194; PR #162 reference-only** |
| R22 | Critical | Ownership chronology could be inverted again | earlier Gemini instruction was once treated as newer than the later Codex reassignment | GPT | **Resolved/controlled — PR #205 + dated decision record** |
| R23 | Medium | Status and issue metadata drift during concurrent work | merged PRs #220/#221 added binding authority contracts after the prior status refresh | GPT | **Mitigated — recurring current-main refresh** |
| R24 | High | Duplicate-workspace Unity evidence can be mistaken for acceptance | PRs #189, #203, #208, #209, #211, #212, and #214 report `C:\Users\MY\Documents\AnotherLife\unity`; #214’s latest run exited 199 with no XML | Codex engineering + GPT | **Active — canonical reruns required** |
| R25 | Critical | Quest compatibility repair can discard or activate ambiguous state | PR #212 removes duplicate/null/blank rows, keeps the first duplicate, seeds Q1–Q5, and exposes unknown side quests | Codex engineering + GPT | **Active — #152 / PR #212 blocked** |
| R26 | High | Relationship normalization may appear complete without real old-JSON coverage | PR #211 manually nulls fields after construction but does not prove omitted Unity JSON, repeated normalization, or unrelated-field preservation | Codex engineering + GPT | **Active — #136 / PR #211 blocked** |
| R27 | High | Green CI may give false confidence before policy proof and protection | positive run exists, but intentional failures and required branch settings are not verified | Codex engineering + user/maintainer + GPT | **Active — #155 remains open** |
| R28 | Critical | Progression can invent or use unauthoritative definitions | current consumers reference absent IDs, research uses display strings and query mutation, all troop lookups are null, and current mutable definitions have no version/hash/provenance | GPT + Codex engineering + source modes | **Active — #183 contract merged PR #220; catalog foundation blocked by #156; full #165 blocked** |
| R29 | High | Terrestrial previews may be mistaken for approved creative or runtime authority | PR #217 uses `Fixes #194`, has three delivered base sheets but nine variants, lacks normalized media/LFS identity, direct binary render links, clean retrieval proof, schema/semantic validation, canonical import evidence, and user approval | Codex terrestrial-design + GPT + user | **Active — #194 / PR #217 blocked; spec merged PR #221** |
| R30 | High | LFS pointer/source prose may substitute for actual pixel review | repository file responses expose pointer identity while PR #217 does not present exact rendered full-resolution sheets tied to hashes; silhouette/anatomy/material/scale cannot be independently reviewed | Codex terrestrial-design + GPT + user | **Active — direct exact-source review required by PR #221 contract** |

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

Parallel focused lanes are limited to existing non-overlapping PRs/issues:

```text
#127/#209   profile-safe PlayMode
#136/#211   relationship-field compatibility evidence
#152/#212   quest-state compatibility
#153/#203   Bootloader lifecycle
#155/#210   quality gates
#161/#195   Android release debug-route gating
#178/#208   Unity command containment
#163/#214   economy integrity implementation against merged PR #215 contract
#194/#217   terrestrial source-design review against merged PR #221 contract
```

No later-phase implementation should self-assign while earlier gates are red.

## Save dependency

```text
#136 accepted normalization evidence
          +
#152 non-mutating quest compatibility
          +
#163 typed non-repairing economy implementation
          ↓
#137 candidate selection, recovery, explicit repair, deletion, and crash-safe persistence
```

The merged `Save_Semantic_Compatibility_Policy.md` and `Economy_Integrity_Spec.md` control the economy/save lanes:

- preserve stable unknown data;
- disable malformed and duplicate groups;
- do not repair through ordinary queries;
- do not take first/max/sum duplicates;
- preserve raw evidence before data-changing repair;
- prefer cleaner candidates;
- use checked typed no-save primitives;
- use clone → validate → persist → publish;
- do not apply offline progress to an unvalidated candidate.

## Asset-authority dependency

```text
merged PR #218 validator specification
          ↓
PR #189 Force-Text YAML/subasset/schema implementation + canonical evidence
          ↓
#156 trusted QuestDefinition/Unity asset baseline
```

The narrative QuestDefinition type/GUID and historical field equivalence are accepted. #156 is not complete until the merged validator specification is implemented and passes canonical import/reimport/malformed-fixture evidence.

## Game-data authority dependency

```text
#156 trusted asset baseline
          ↓
merged PR #220 versioned immutable game-data authority specification
          ↓
#183 catalog foundation with no production switch/shared-file claim
          ↓
approved source catalogs
          ↓
LocalGameDataService migration with declared lock
          ↓
focused consumer migrations
```

The first #183 implementation is limited to manifest/envelope models, typed lifecycle/load/query/diagnostics, immutable snapshots, strict validators, packaged file/UnityWebRequest seams, hash/schema tests, and current source/consumer inventory. It may not edit `Bootloader.cs`, claim `LocalGameDataService.cs`, author content, switch production authority, repair saves, or promote terrestrial source.

## Definition/progression dependency

```text
#156 trusted QuestDefinition/asset baseline
          ↓
#183 versioned immutable game-data implementation
          ↓
#165 definition-backed building/research/training integrity
```

Before #183 implementation, #165 may fail closed but must not introduce temporary definitions, IDs, names, maximum levels, troop records, research mapping, or balance.

## Terrestrial source dependency

```text
merged PR #221 source-packet validation specification
          ↓
PR #217 exact-source technical completion
          ↓
user approval for exact source version/profile/variant IDs
          ↓
#156 + #183 + owning runtime issue
          ↓
separate engineering integration + GPT technical review + Codex design-fidelity review
          ↓
user integrated acceptance
```

Until then, terrestrial working labels, profile/variant IDs, biome tags, concept images, hashes, and source versions are source-review evidence only—not spawn, AI, combat, reward, save, narrative, or runtime authority.

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

The first approved open PR declaring one holds the lock. Save fields require defaults, migration, old-save tests, semantic validation, fault recovery, and duplicate-safety evidence. The #183 catalog-foundation PR does not claim `LocalGameDataService.cs`; a later service-migration PR must declare it.

## Evidence policy

- build risk → exact commands, exit codes, complete error scan;
- asset risk → GUID/reference inventory, LFS binary retrieval, import/reimport, malformed/missing-script scan, media type, hashes, dimensions, and field preservation;
- test risk → discovered totals and retained XML/log artifacts;
- save/economy/reward risk → normal, recovery, fault, deletion, semantic, overflow, duplicate, reload, event/save-count, and idempotency matrices;
- catalog risk → manifest/envelope identity, schema/content version, raw hashes, provenance, immutable query results, lifecycle, generated-contract drift, packaging, and implemented consumer proof;
- contract risk → valid/invalid cases and implemented producer/consumer proof;
- packaging risk → actual Player/export build and launch transition;
- narrative/design risk → rendered exact-source packet fidelity, provenance, immutable source-version/hash mapping, variant state, accessibility, technical disposition, and user decision;
- integration risk → route/session/result/lifecycle evidence;
- player-experience risk → integrated user playtest.

Skipped, unavailable, duplicate-workspace, pointer-only media, compile-only, stale-base, development-fallback, or `continue-on-error` checks are not passing evidence.

## Immediate mitigation

```text
1. Implement merged PR #218 in PR #189 and run canonical Unity evidence.
2. Rewrite PR #214 against the merged economy specification; prohibit service-local repair and validate canonically.
3. Correct PR #209 teardown ordering before any branch consumes PlayMode evidence.
4. Harden PR #210, run the proof matrix, and keep #155 open through protection evidence.
5. Bring PRs #211 and #212 into exact save-policy compliance before #137 starts.
6. Remove PR #208 hidden controller/direct-credit mutation paths and prove release non-mutation.
7. Fix PR #195 durable visible fallback.
8. Complete PR #203 transaction, marker, lifecycle, and fault semantics while retaining the lock.
9. Correct PR #217 against merged PR #221 before user creative review.
10. After #156, start only the contract-limited #183 catalog foundation; do not claim production authority/shared files early.
11. Keep #165 reconnection, A1/G1/runtime, #137/#134, terrestrial integration, and Player claims behind their prerequisites.
```
