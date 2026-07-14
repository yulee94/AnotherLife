# Phase 1 NVS-01 Status

**Status date:** 2026-07-14  
**Roadmap phase:** Phase 1 — NVS-01: One Approved Quest Line End to End  
**Status:** Active — A1 clean narrative packet required

This document is the current Phase 1 coordination record. `AGENTS.md` remains authoritative. `unity/Docs/Three_Way_Collaboration_Plan.md` defines the full task sequence and acceptance model.

## Phase entry

Phase 0 evidence is complete:

- Android unit tests passed on integrated `main`.
- Android debug assembly passed on integrated `main`.
- Unity `2022.3.62f3` import and C# compilation passed.
- EditMode smoke tests passed 3/3.
- PlayMode runner availability was recorded accurately: no committed PlayMode tests existed.
- `Assets/Test.unity` entered and exited Play Mode successfully through a temporary validation probe.
- Issue #117 is closed.
- No shared-file lock or Phase 0 blocker remains.

The detailed evidence and gate decision are in `unity/Docs/Phase_0_Build_Health_Status.md` and PR #123.

## Phase goal

Prove that one bounded, user-approved quest line can move from Android Studio-owned narrative source to a playable, persistent runtime loop without:

- duplicated story logic,
- ownership drift,
- invalid references,
- unsafe save changes,
- duplicate consequences,
- or silent merge conflicts.

The selected candidate is:

```text
Quest: OMEN_1
Title: The First Signal
Milestone: NVS-01
```

This selection is not final narrative approval by itself. The clean A1 packet and its creative decisions still require user approval.

## Active dependency chain

```text
#128 A1 — Android Studio clean narrative packet
        ↓
#133 G1 — GPT runtime integration specification
        ↓
#134 C1–C4 — Codex implementation, persistence, and verification
        ↓
G2 — GPT implementation review
        ↓
A2 — Android Studio narrative-fidelity review
        ↓
U1 — User playtest and milestone acceptance
```

Downstream work must not begin early.

## Active task: A1

**Issue:** #128  
**Owner:** Android Studio narrative workflow  
**Required branch:** `android-studio/nvs-01-a1-clean`

### Goal

Produce exactly one clean, bounded, user-approved `OMEN_1` narrative packet from latest `main`.

### Source preservation

Draft PR #124 preserves the earlier mixed narrative branch. It is an archive/reference and must never merge.

The clean A1 branch must selectively copy or re-author only the bounded packet. It must not cherry-pick the archived broad commits wholesale.

### A1 required content

- Stable milestone, chapter, quest, objective, dialogue, NPC, reward/artifact, hook, event, and localization IDs.
- Purpose and scope.
- Entry conditions, prerequisites, and unlock rule.
- Complete states and allowed transitions.
- Objective progress rules.
- Dialogue and choices.
- Affinity/reputation/faction/resource/reward/world-state intent for this quest only.
- Semantic gameplay handoff request.
- Success and failure return events.
- Completion, failure, retry, cancellation, and recovery behavior.
- One-time versus repeatable intent for every consequence.
- Localization keys or approved source-text exceptions.
- Continuity notes and external dependencies.
- Completion report and exact GPT handoff request.

### Creative decisions still required

Android Studio and the user must make the narrative decisions. GPT and Codex must not choose them.

1. Resolve the missing `DLG_OMEN_1_ARENA_START` target by authoring the node or using an explicit semantic handoff.
2. Choose one authoritative failure/retry sequence.
3. Define or remove the `FAILED` state.
4. Define trigger and repeatability intent for Valerius affinity, gold, Celestial Tear, and completion.
5. Complete localization coverage or approve explicit exceptions.
6. Confirm `HOOK_SKY_CASTLE_ARENA` as implemented capability or label it a requested semantic dependency.

### A1 prohibited scope

Do not include:

- full Chapter 1,
- realm-wide identity/stat systems,
- building/research narrative hooks,
- world-atlas expansion,
- broad dossier expansion,
- global authoring templates/governance,
- Android runtime-model changes,
- Android navigation or Gradle changes,
- Unity service/interface/definition/path changes,
- save infrastructure,
- service registration,
- shared technical contracts.

## Blocked task: G1

**Issue:** #133  
**Owner:** GPT  
**Status:** Blocked by A1 and user approval

After A1 approval, GPT will produce:

- goal and non-goals,
- source-of-truth and stable-ID inventory,
- state-transition table,
- runtime-event producer/consumer/payload map,
- contract/schema requirements,
- validation and error behavior,
- save fields/defaults/migration/resume semantics,
- consequence idempotency,
- required/optional/prohibited file impacts,
- shared-file locks,
- full test matrix,
- merge order,
- definition of done,
- unresolved blockers.

GPT must not write the substantive G1 specification before A1 is authoritative.

## Blocked task: C1–C4

**Issue:** #134  
**Owner:** Codex  
**Status:** Blocked by approved G1

Codex will later own:

- contract loading and validation,
- deterministic quest-state integration,
- gameplay handoff and return processing,
- persistence and old-save compatibility,
- consequence idempotency,
- diagnostics,
- automated tests,
- exact compilation/test evidence.

Codex must not implement NVS-01 before G1 is approved and must not rewrite narrative content.

## Archived and deferred scope

The broad content removed from A1 is preserved in dedicated backlog issues:

- **#129 — Phase 2:** Chapter 1 four-realm narrative spine.
- **#130 — Phase 3:** realm, building, dossier, and world narrative hooks.
- **#131 — Phase 4:** narrative ID governance, localization, and authoring templates.

These issues preserve ideas without authorizing early implementation.

## Non-blocking technical follow-ups

The following issues are separate from A1 and must not be mixed into the narrative PR:

- **#126:** recurring post-success KSP/AWT diagnostic.
- **#127:** add committed Unity PlayMode smoke coverage.
- **#132:** update deprecated `QuestScreen` progress-indicator overload.

Codex may handle them only as separate focused work that does not disrupt the active dependency chain.

## Pull-request state

### PR #123

Phase 0 closeout and Phase 1 transition documentation. Merge-ready after review.

### Draft PR #124

Archived mixed narrative branch. Never merge. Close as superseded only after the clean A1 PR is open and GPT verifies content preservation.

### Future clean A1 PR

Must:

- link issue #128,
- start from latest `main`,
- contain only bounded narrative-owned scope,
- report all IDs and reference validation,
- include exact Android commands/results,
- include Android Studio’s completion report,
- request GPT review.

## Shared-file status

No shared integration file is currently locked for NVS-01.

Potential shared files remain:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

A1 must not edit them. G1 must justify any future need. The first implementation PR declaring one holds the soft lock.

## Phase 1 gate checklist

| Gate | Status | Owner/evidence |
| --- | --- | --- |
| Phase 0 green | Pass | PR #123 closeout evidence |
| One bounded quest selected | Candidate selected; user approval pending | `OMEN_1` |
| Clean A1 branch from latest `main` | Pending | Android Studio, #128 |
| A1 internal references resolve | Pending | Android Studio, #128 |
| A1 state/recovery/consequence intent complete | Pending | Android Studio + user |
| A1 ownership and validation pass | Pending | Android Studio + GPT |
| G1 specification approved | Blocked | GPT, #133 |
| C1–C4 implementation and tests | Blocked | Codex, #134 |
| G2 technical review | Blocked | GPT |
| A2 narrative fidelity | Blocked | Android Studio |
| U1 playtest acceptance | Blocked | User |
| Shared locks released | Pass currently | None open |

## Current next action

```text
Owner: Android Studio narrative workflow
Issue: #128
Branch: android-studio/nvs-01-a1-clean
Deliverable: one corrected OMEN_1 A1 narrative packet and focused PR
```

Do not start G1 or Codex implementation until that packet is complete and user-approved.
