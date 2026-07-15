# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-15  
**Audited current-main head:** `46f441ec5019d6432f83a6e92c6d18c7b815cb09`  
**Active control state:** Phase 1 is paused behind #156 and an ownership-governance restoration  
**Approved product intent:** issue #138 D1–D16  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

This register describes verified source and delivery risk. It supersedes assumptions based only on issue closure, PR merge state, source presence, compilation, or one-platform validation.

## Current risks

| ID | Severity | Risk | Evidence | Owner | Tracking/status |
| --- | --- | --- | --- | --- | --- |
| R1 | Critical | QuestDefinition serialized identity remains ambiguous | removed root GUID and surviving narrative GUID lack complete project inventory/guards | Codex engineering + GPT | **Active — #156 / PR #189 blocked** |
| R2 | Critical | Archived OMEN_1 may be mistaken for approved A1/runtime | archive conflicts with approved start, failure, reward, report, abandonment, localization, and resume | Codex narrative/content + GPT | **Contained — #128 after #156** |
| R3 | Critical | Consequences lack one atomic/idempotent transaction | resource, affinity, quest, artifact, and chapter domains save separately | GPT + Codex engineering | **Blocked — #133/#134 and foundations** |
| R4 | Critical | Save rotation can destroy last-known-good data | unvalidated primary may rotate to backup; fallback/status/deletion/offline-progress rules incomplete | Codex engineering | **Blocked/open — #137 after foundations** |
| R5 | High | Save semantic implementations may diverge | #136/#152/#163/#137 share candidate and repair rules | GPT + Codex | **Policy mitigated by PR #197; implementation pending** |
| R6 | High | PlayMode tests use real developer profile and ordinary-log brittleness | current scene smoke has no isolation, timeout, cleanup, or accepted XML | Codex engineering | **Ready — #127 specification/implementation required** |
| R7 | High | Bootloader lifecycle implementation is not transaction-safe | load token committed before successful load, save can cross-wire, post-install verification cannot rollback | Codex engineering + GPT | **Blocked — #153 / PR #203; Bootloader lock held** |
| R8 | High | Production Unity Player lacks authoritative scene flow | normal Build Settings empty while code loads named scenes | Codex engineering + GPT | **Blocked — #150 after #156** |
| R9 | High | Android↔Unity bridge is not end to end | no packaged export, mounted host, Unity consumer/result producer, or session identity | GPT + Codex engineering | **Deferred — #135** |
| R10 | High | Repository quality gates are not implemented/proven | no required Phase A workflow, fixtures, Unity runner, or protection | Codex engineering + GPT | **Open — #155** |
| R11 | Medium | Android dependency resolution remains dynamic on `main` | PR #191 is correct in direction but stale and lacks current release validation | Codex engineering | **Draft — #191** |
| R12 | Medium | Release shell still exposes debug route on `main` | PR #195 revised most defects but rejection notice is erased by follow-up effect pass | Codex engineering | **Draft — #195 one blocker** |
| R13 | Critical | Resource/Warzone Credit mutations permit signed/overflow exploits | negative spend can add value; malformed entries can throw | Codex engineering | **Ready/open — #163** |
| R14 | High | Realm identity can be invalid or overwritten without migration policy | `None`/undefined accepted; existing profile replaced | Codex engineering + GPT | **Specification merged #202; implementation pending #173** |
| R15 | Critical | Battle simulation accepts invalid armies and mutates progression | null/empty request can win; simulation has side effects | Codex engineering | **Blocked/open — #174** |
| R16 | Critical | Boss loot can fabricate/duplicate/partially commit rewards | fallback loot, no result identity, credits before equipment persistence | Codex engineering | **Blocked/open — #168** |
| R17 | High | Territory/progression/reward domains trust malformed state | repeated capture, unsafe costs/counts/timers, duplicate identities | Codex engineering | **Blocked/open — #165/#166/#169/#171** |
| R18 | High | World/relationship/notification domains are non-persistent or parallel content authorities | hard-coded copy, nested saves, missing idempotency/visible delivery | Codex narrative/content + engineering | **Blocked/open — #172/#176/#177** |
| R19 | Critical | Production Kingdom UI exposes prototype grants and destructive commands | direct credits/gems/wishes, mutating drill, unconfirmed reset | Codex engineering | **Specification merged #200; implementation ready #178** |
| R20 | High | Champion/atlas/game-data/customization remain weakly validated and mutable | non-finite state, silent fallback, hard-coded authority, live save mutation | Codex modes + GPT | **Blocked/open — #180/#181/#183/#184** |
| R21 | Medium | Old terrestrial prototype branches may be treated as design authority | stacked procedural visual work inherited rejected ancestors | Codex terrestrial-design + GPT | **Contained — #194; PR #162 reference-only** |
| R22 | Critical | Ownership governance was reverted using an earlier instruction | PR #204 quoted the earlier Gemini assignment and ignored the later Codex reassignment | GPT | **Active mitigation — restoration PR + decision record** |
| R23 | Medium | Status/issue metadata can drift during concurrent agent work | #193/#194 and governance files changed repeatedly | GPT | **Mitigated by dated ownership record and quality-policy rule** |

## D1–D16 controls

A1/G1/runtime must preserve:

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
ownership restoration supersedes PR #204
  ↓
#156 / PR #189 trusted QuestDefinition authority
  ↓
#128 Codex narrative/content A1
  ↓
#133 GPT G1
  ↓
required focused foundations
  ↓
#134 Codex engineering C1–C4
  ↓
G2 GPT → A2 Codex narrative/content → U1 user
```

Parallel focused lanes: #127, #136, #152, #153/#203, #155, #159/#191, #161/#195, #163, and #178.

## Shared-file risk

Current lock:

```text
Bootloader.cs — held by draft PR #203
```

Other designated shared files remain unlocked:

```text
SaveGameData.cs
LocalGameDataService.cs
ProjectInitializer.cs
```

The first approved open PR declaring one holds the lock. Save fields require defaults, migration, old-save tests, semantic validation, and duplicate-safety evidence.

## Evidence policy

- build risk → exact commands, exit codes, complete error scan;
- asset risk → GUID/reference inventory, reimport, missing-script scan, field preservation;
- test risk → discovered totals and retained XML/log artifacts;
- save/economy/reward risk → normal, recovery, fault, deletion, semantic, overflow, duplicate matrices;
- contract risk → valid/invalid cases and implemented producer/consumer proof;
- packaging risk → actual Player/export build and launch transition;
- narrative/design risk → approved packet fidelity, references, provenance, readability, and user decision;
- integration risk → route/session/result/lifecycle evidence;
- player-experience risk → integrated playtest.

Skipped, unavailable, duplicate-workspace, compile-only, or `continue-on-error` checks are not passing evidence.

## Immediate mitigation

```text
1. Merge the ownership-restoration PR and supersede #204.
2. Keep #189 and #203 blocked until their structural and validation requirements pass.
3. Fix the remaining #195 notice-persistence bug.
4. Rebase and release-validate #191.
5. Publish #127 profile-isolation specification and implementation handoff.
6. Start #155 and #178 only on focused non-overlapping branches.
```