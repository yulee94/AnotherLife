# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-15  
**Audited current-main head:** `a6232e63c807f055cc43b302ad4e62b846c236ca`  
**Active control state:** compilation recovery is complete; Phase 1 is paused behind #156  
**Approved product intent:** issue #138 D1–D16  
**Ownership transition:** issue #193, GPT–Codex–user model

This register describes verified current-source and delivery risk. It supersedes assumptions based only on issue closure, PR merge state, source presence, compilation, or one-platform validation.

## Severity and status

- **Critical:** build/asset loss, profile/economy corruption, duplicate rewards, invalid authority, or uncontrolled integration.
- **High:** non-deterministic player path, persistence/bootstrap failure, packaging blocker, combat-state failure, or false player-visible completion.
- **Medium:** compatibility, diagnostics, accessibility, UX, governance, or reproducibility debt with a bounded workaround.
- **Low:** non-blocking hygiene or evidence debt.

Status values: **Open**, **Blocked**, **Contained**, **Deferred**, **Mitigated**, and **Closed**.

## Current risks

| ID | Severity | Risk | Current evidence | Owner | Tracking/status |
| --- | --- | --- | --- | --- | --- |
| R1 | Critical | Unity compilation regression after narrative namespace migration | obsolete `DialogueChoice` type reference | Codex + GPT | **Closed — #145 / PR #147** |
| R2 | Critical | QuestDefinition serialized identity is ambiguous | removed root GUID and surviving narrative GUID require complete project inventory and guards | Codex engineering + GPT | **Active — #156 / draft PR #189** |
| R3 | Critical | Archived OMEN_1 source can be mistaken for approved A1/runtime | merged archive conflicts with D1–D16 | Codex narrative/content + GPT | **Contained/blocked — #128 after #156** |
| R4 | Critical | Save rotation may overwrite a valid backup with unvalidated primary bytes | current persistence order is unsafe | Codex engineering | **Blocked/open — #137 after foundations** |
| R5 | High | Save candidate and malformed-data policies could diverge across issues | #136/#152/#163/#137 had overlapping repair decisions | GPT + Codex | **Mitigated — policy PR #197 merged; implementations pending** |
| R6 | High | PlayMode smoke may consume or alter a developer profile | no profile isolation/restoration or deterministic cleanup | Codex engineering | **Ready — #127** |
| R7 | High | Bootloader can treat a partial registry as complete | one-service readiness sentinel | Codex engineering | **Ready — #153; shared lock required** |
| R8 | High | No authoritative runtime narrative catalog/state machine exists | Android preview, archive packet, Unity fallback story, and quest service remain separate | GPT + Codex | **Blocked — #133/#134** |
| R9 | High | `OMEN_1` approved paths are not encoded | start, deployment, failure/retry, Tear, report, abandonment, localization, resume conflict | Codex narrative/content | **Blocked — #128 after #156** |
| R10 | Critical | NVS consequences lack one atomic/idempotent boundary | resource, affinity, quest, artifact, and chapter services save independently | GPT + Codex engineering | **Blocked — #133/#134 and save/economy/relationship foundations** |
| R11 | High | Null/blank/unknown/duplicate quest states can crash or reward incorrectly | unsafe enumeration and definition indexing | Codex engineering | **Ready — #152** |
| R12 | High | Relationship fields are only partially proven backward compatible | defaults exist; mutation and save/reload evidence incomplete | Codex engineering | **Ready — #136** |
| R13 | Critical | Resource/Warzone Credit mutations accept unsafe signed and overflow operations | negative spend/consume can add value; malformed entries can throw | Codex engineering | **Ready/open — #163** |
| R14 | High | Full profile deletion leaves previous/quarantine artifacts | current delete path is incomplete | Codex engineering | **Open — #137** |
| R15 | High | Offline progress can duplicate or be lost on failed persistence | mutation occurs before durable publish/rollback | Codex engineering | **Open — #137** |
| R16 | High | Production Unity Player has no authoritative scene list | normal Build Settings empty while code loads scenes by name | Codex engineering + GPT | **Blocked — #150 after #156** |
| R17 | High | Android↔Unity bridge is not end to end | no packaged export, mounted route, Unity consumer/result producer, or session identity | GPT + Codex engineering | **Deferred — #135 after NVS-01/#150** |
| R18 | High | Repository gates are specified but not implemented/proven | required workflow, fixtures, runner, and protection are absent | Codex engineering + GPT | **Open — #155** |
| R19 | Medium | Android dependency resolution was dynamic | consumed `+` dependency and unused dynamic aliases | Codex engineering | **Draft PR #191** |
| R20 | Medium | Release Android shell exposes narrative debug route/trigger surface | build-flavor gate absent on `main` | Codex engineering | **Draft PR #195** |
| R21 | High | Android quest preview can show invalid progress and false actions | unsafe ratio, unsupported Start, no-op hard-coded claim | Codex engineering + GPT | **Blocked — #186 after #128/#133** |
| R22 | Critical | Production Kingdom UI exposes prototype grants and destructive commands | direct credits, fixed gems/wishes, mutating drill, one-click reset | Codex engineering | **Open — #178** |
| R23 | High | Realm identity can be invalid or overwritten without migration policy | `None`/undefined accepted and current profile replaced | Codex engineering + GPT | **Blocked/open — #173 after persistence contract** |
| R24 | Critical | Battle simulation accepts invalid armies and mutates progression | null/empty request can win; simulation has side effects | Codex engineering | **Blocked/open — #174 after relevant foundations** |
| R25 | Critical | Boss loot can fabricate, duplicate, or partially commit rewards | fallback loot, no result identity, credits before equipment persistence | Codex engineering | **Blocked/open — #168 after persistence/economy** |
| R26 | High | Territory capture can farm rewards and passive income trusts malformed state | same-owner capture repeats consequence path | Codex engineering | **Blocked/open — #166 after #163** |
| R27 | High | Building/research/training state accepts invalid identities and arithmetic | query-time creation, negative/overflow levels/counts | Codex engineering | **Blocked/open — #165 after #163** |
| R28 | High | Realm Gem/Wishgate state can contradict or lose entitlement | independent flags and non-transactional reward consumption | Codex engineering + Codex narrative/content | **Blocked/open — #169 after persistence** |
| R29 | High | Warmaster purchase can charge without durable entitlement | separate saves, caller price, raw duplicate-count threshold | Codex engineering | **Blocked/open — #171 after persistence/#163** |
| R30 | High | World-state lifecycle is in-memory and a parallel content authority | duration unused, no save/resume, hard-coded player copy | Codex engineering + Codex narrative/content | **Blocked/open — #172 after persistence** |
| R31 | High | Relationship mutation lacks finite/overflow/idempotency/transaction rules | affinity, faction, persona validate poorly and force nested saves | Codex engineering + Codex narrative/content | **Blocked/open — #176 after #136/#137 seam** |
| R32 | High | Player notifications are console-only | no visible typed/localized/deduplicated delivery | Codex engineering + Codex narrative/content | **Blocked/open — #177 after persistence boundary where needed** |
| R33 | High | Champion/boss/skill state accepts non-finite values and lacks one encounter lifecycle | NaN poisoning, partial catalog fallback, silent Crownlands substitution | Codex engineering | **Blocked/open — #180 after #173 and foundations** |
| R34 | High | World atlas is mutable, unversioned, weakly validated, and a parallel content authority | fallback service hard-codes zones/objectives/text/rewards | Codex engineering + Codex narrative/content | **Blocked/open — #181 after #156/#173** |
| R35 | Critical | Game-data authority is incomplete/conflicting | null lookups, mutable generated ScriptableObjects, duplicate overwrite, hard-coded content | Codex engineering + Codex narrative/content | **Blocked/open — #183 after #156** |
| R36 | High | Customization can overwrite future IDs before authoritative catalog load | live save mutation and async fallback normalization | Codex engineering + Codex terrestrial-design | **Blocked/open — #184 after #137/#183** |
| R37 | Medium | Terrestrial prototype branches may be mistaken for authority | old stacked procedural visual work inherited rejected ancestors | Codex terrestrial-design + GPT | **Contained — #194; old PR #162 reference-only** |
| R38 | High | Merged PR #196 established a now-revoked Gemini/Android Studio ownership model | authoritative docs and branch rules conflict with latest user decision | GPT | **Active mitigation — #193 governance correction** |
| R39 | Medium | Documentation can drift behind current main and open PRs | prior status claimed no open PRs and older head | GPT | **Mitigated in #193 branch; merge pending** |

## Approved D1–D16 controls

A1, G1, and runtime work must preserve:

- authored deployment node before arena request;
- transient encouraging failure/retry loop;
- nonterminal recovery-only `FAILED`;
- Celestial Tear acquired exactly once on arena success;
- manual report to Valerius;
- 500 Gold, +5 affinity, quest completion, and selected-realm Chapter 1 unlock exactly once at report conclusion;
- complete localization-key inventory;
- honest requested-capability classification for Sky Castle marker/hook/results;
- abandonment only outside active encounter;
- universal post-realm eligibility;
- Valerius as inter-realm Veil Watch liaison;
- retained Tear presented and kept;
- quest offered rather than auto-accepted;
- exact-node dialogue resume and duplicate-safe encounter/report recovery.

These decisions close creative ambiguity only. They do not prove A1, G1, runtime, persistence, integration, or playtest.

## Execution order

```text
#193 ownership correction
  ↓
#156 QuestDefinition authority (PR #189)
  ↓
trusted source/asset baseline

Parallel focused foundations:
#127 #136 #152 #153 #155 #159/#191 #161/#195 #163

#136 + #152 + #163-compatible rules
  ↓
#137 crash-safe persistence

After #156:
#128 Codex narrative/content A1
#150 production scenes/Player build
#183 and other authority work that truly depends on #156

#128 → #133 GPT G1 → named foundations → #134 Codex engineering
→ G2 GPT → A2 Codex narrative/content → U1 user
```

## Shared-file risk

Current soft locks: **none**.

```text
Bootloader.cs
SaveGameData.cs
LocalGameDataService.cs
ProjectInitializer.cs
```

The first approved open PR declaring one holds its lock. Save fields require defaults, migration, old-save tests, semantic validation, and duplicate-safety evidence. Closed or speculative branches reserve nothing.

## Evidence policy

- source build risk → exact commands, exit codes, full error scan;
- serialized asset risk → GUID/reference inventory, reimport, missing-script scan, field preservation;
- test risk → discovered totals and retained XML/log artifacts;
- save/economy/reward risk → normal, recovery, fault, deletion, semantic, overflow, and duplicate matrices;
- contract risk → valid/invalid cases and implemented producer/consumer proof;
- packaging risk → actual Player/export build and launch transition;
- narrative/design risk → approved packet fidelity, references, provenance, readability, and user decision;
- integration risk → actual route/session/result/lifecycle evidence;
- player-experience risk → integrated playtest.

Skipped, unavailable, compile-only, or `continue-on-error` checks are not passing evidence.

## Immediate mitigation

```text
1. Merge #193 governance correction and close superseded Gemini ownership files.
2. Keep PR #189 draft until corrected tests, full inventory, current-base rebase, and Unity evidence pass.
3. Review PR #191 and #195 independently.
4. Do not activate #128 or #150 before #156.
5. Apply PR #197 save policy consistently to #136/#152/#163/#137 implementations.
```