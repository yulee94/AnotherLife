# Phase 1 NVS-01 Status

**Status date:** 2026-07-15  
**Integration branch:** `main`  
**Audited current-main head:** `46f441ec5019d6432f83a6e92c6d18c7b815cb09`  
**Roadmap state:** Phase 1 remains paused behind QuestDefinition authority issue #156  
**Ownership authority:** `unity/Docs/Ownership_Decision_Record.md`

`AGENTS.md` is authoritative after the ownership-restoration PR merges. This record separates source presence, issue state, merge state, validation evidence, and player-visible completion.

## Ownership incident

The active conversation contained two successive terrestrial-ownership instructions. The later instruction returned all responsibility to Codex. PR #201 encoded that final instruction, but PR #204 quoted the earlier instruction and reverted it.

The active restoration branch:

```text
gpt/restore-latest-codex-ownership
```

restores the GPT–Codex–user model and adds `Ownership_Decision_Record.md` so instruction chronology cannot be inverted again.

Final model:

- GPT — coordination, specification, review, status, risk, and sequencing;
- Codex narrative/content — all narrative source and fidelity;
- Codex terrestrial-design — all terrestrial visual-design source and fidelity;
- Codex engineering — Android, Unity, runtime, assets/import, save, build, tests, CI, and tooling;
- user — final product, creative, playtest, and release approval.

Android Studio and Unity are tools. `android-studio/` and `gemini/` are retired branch prefixes.

## Completed foundation records

- #145 / PR #147 — Unity dialogue-choice compilation repair.
- PR #190 — Android `Quest` positional compatibility.
- PR #192 — staged repository quality-gate policy.
- PR #197 — save semantic compatibility/candidate-selection policy.
- PR #198 — Bootloader service-stack integrity specification.
- PR #200 — Unity release-command containment specification.
- PR #202 — durable realm-selection specification.

A merged specification is not implementation completion.

## Open pull requests

### #189 — QuestDefinition authority safeguards

- Issue: #156
- Owner: Codex engineering
- State: draft/blocked
- Shared locks: none

Still required:

- permit valid quest assets to reference the authoritative script GUID;
- search all project quest assets;
- enforce exactly one production `QuestDefinition` type;
- publish the complete GUID/source/asset/generator/importer/schema/catalog inventory;
- rebase current `main`;
- pass canonical-workspace Unity compile, corrected EditMode/editor tests, reimport, and missing-script scan.

### #191 — Android dependency reproducibility

- Issue: #159
- Owner: Codex engineering
- State: draft/near-ready
- Shared locks: none

Diff direction is accepted. It still needs a current-main rebase, current declarations, unit/debug rerun, release assembly, final no-dynamic-version proof, and current base/head evidence.

### #195 — Android narrative-debug release gating

- Issue: #161
- Owner: Codex engineering
- State: draft/one UX blocker
- Shared locks: none

The typed route result, sanitization, stable preview seam, and current Android builds are present. The rejection notice is currently erased by the sanitization-triggered second Compose effect pass; it needs deterministic persistence/dismissal and a shell/Compose test.

### #203 — Bootloader service-stack implementation

- Issue: #153
- Owner: Codex engineering
- State: draft/blocked
- Shared lock: `Bootloader.cs`

Direction is correct, but load-once failure semantics, save-boundary marker validation, post-install rollback, malformed-marker safety, immutable marker/inventory, exact diagnostics, fault seams, lifecycle tests, and canonical-workspace validation are incomplete. No other task may edit `Bootloader.cs` while #203 holds the lock.

## Active gate: #156

Before PR #124 there were two QuestDefinition identities:

```text
AL.Data.Definitions.QuestDefinition
GUID 226022aa7500f3e4abc8ac3757707ad8

AL.Data.Definitions.Narrative.QuestDefinition
GUID c385b2b183b74184ca75eeffbe2256ef
```

Expected final authority is the surviving narrative type/GUID when the complete inventory proves no tracked asset requires the removed identity. Completion requires full project inventory, deterministic type/GUID/missing-script/duplicate-ID guards, and Unity compile/reimport/editor evidence.

No A1 packet or production Player build may claim a trusted asset baseline before #156 completes.

## Parallel foundations

May proceed only through focused non-overlapping PRs:

- #127 — profile-safe representative PlayMode smoke;
- #136 — relationship-field normalization and round trip;
- #152 — malformed/unknown/duplicate quest-state compatibility;
- #153 — Bootloader stack through PR #203;
- #155 — repository/Android quality gates;
- #159 — dependency reproducibility through PR #191;
- #161 — release debug-route gating through PR #195;
- #163 — resource/Warzone Credit integrity;
- #178 — release prototype-command containment.

## Save dependency

```text
#136 + #152 + #163-compatible semantic rules
                 ↓
#137 crash-safe persistence implementation
```

Implementations must consume `Save_Semantic_Compatibility_Policy.md`; save candidate selection, deterministic repair, unknown-data preservation, and clone → persist → publish are shared rules.

## NVS-01 chain

```text
#156 trusted asset authority
  ↓
#128 Codex narrative/content A1
  ↓
#133 GPT G1
  ↓
required focused technical foundations
  ↓
#134 Codex engineering C1–C4
  ↓
G2 GPT → A2 Codex narrative/content → U1 user
```

The merged archive packet is history, not approved A1. A1 must encode D1–D16, including deployment node, transient failure/retry, Tear on arena success, manual report, atomic report consequences, abandonment, localization, and exact resume.

## Production lanes

- #150 remains blocked by #156; normal Unity Build Settings still lack the approved production scene flow.
- #135 remains deferred until standalone NVS-01 and #150 are proven; the reflection host is not an end-to-end packaged bridge.

## Evidence rules

Issue closure, PR merge, source presence, one-platform compilation, skipped checks, or documentation alone are insufficient. Evidence must match the risk: compiler logs, test XML/totals, GUID/import scans, old-save/fault matrices, producer/consumer proof, Player/export builds, device/lifecycle validation, source fidelity, and user playtest.

## Immediate next actions

```text
1. Merge the ownership-restoration PR that supersedes #204.
2. Keep #189 draft until corrected tests, full inventory, and Unity evidence pass.
3. Finish the single remaining #195 notice-persistence blocker.
4. Rebase/revalidate #191.
5. Correct #203 transaction/lifecycle defects while retaining the Bootloader lock.
6. Publish and implement #127 profile-safe PlayMode specification.
```