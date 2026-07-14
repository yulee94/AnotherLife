# Phase 1 NVS-01 Status

**Status date:** 2026-07-14  
**Roadmap phase:** Phase 1 — NVS-01: One Approved Quest Line End to End  
**Status:** Active — A1 clean narrative packet and user decisions required

This document is the current Phase 1 coordination record. `AGENTS.md` remains authoritative. Use it with:

- `unity/Docs/Three_Way_Collaboration_Plan.md`
- `unity/Docs/NVS_01_A1_Packet_Template.md`
- `unity/Docs/NVS_01_G1_Specification_Template.md`
- `unity/Docs/Phase_1_NVS_01_Risk_Register.md`

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

The archived OMEN_1 source identity, blob SHA, objectives, dialogue beats, choices, consequences, and continuity intent are recorded in issue #128 for preservation review.

### Required packet structure

Use:

```text
unity/Docs/NVS_01_A1_Packet_Template.md
```

The clean packet must include:

- stable milestone, chapter, quest, objective, dialogue, NPC, reward/artifact, hook, location, event, and localization IDs,
- purpose and bounded scope,
- realm/chapter placement,
- entry conditions, prerequisites, and unlock rule,
- complete states and allowed transitions,
- objective activation/progress/completion rules,
- dialogue and choices,
- affinity/reputation/faction/resource/reward/world-state intent for this quest only,
- semantic gameplay handoff request,
- success and failure return events,
- completion, failure, retry, cancellation, and recovery behavior,
- one-time versus repeatable intent for every consequence,
- localization keys or approved source-text exceptions,
- continuity notes and external dependencies,
- focused packet validation tests,
- completion report and exact GPT handoff request.

### User/Android Studio decisions still required

GPT and Codex must not choose these narrative answers.

- **D1:** dialogue-to-arena sequence.
- **D2:** arena failure recovery.
- **D3:** `FAILED` state meaning.
- **D4:** Valerius affinity timing and repeatability.
- **D5:** Gold and Celestial Tear timing/repeatability.
- **D6:** quest completion timing.
- **D7:** localization/source-text policy.
- **D8:** gameplay hook status.
- **D9:** cancellation/abandonment.
- **D10:** chapter/realm placement.
- **D11:** Valerius role and speaker scope.
- **D12:** location/access prerequisites and post-completion destination.

Also resolve:

- missing `DLG_OMEN_1_ARENA_START`,
- reserved `end` terminal convention,
- authoritative quest-start transition,
- objective activation/completion moments,
- report-dialogue timing,
- Celestial Tear acquisition/delivery/retention meaning,
- stable speaker ID versus display name,
- and whether `SKY_CASTLE` is narrative-only or a requested atlas/location capability.

### A1 validation

The clean PR must add focused packet tests covering:

- ID uniqueness,
- dialogue target resolution,
- reserved terminal handling,
- state/transition integrity and reachability,
- objective activation/completion integrity,
- consequence target/trigger/repeatability declarations,
- success/failure event distinction,
- external dependency status,
- localization coverage/exceptions,
- and at least one negative missing-reference or invalid-transition case.

It must also run:

```text
:app:testDebugUnitTest
:app:assembleDebug
```

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
- shared technical contracts,
- or the real Android↔Unity bridge.

## Blocked task: G1

**Issue:** #133  
**Owner:** GPT  
**Status:** Blocked by A1 and user approval

After A1 approval, GPT must use:

```text
unity/Docs/NVS_01_G1_Specification_Template.md
```

The specification must cover:

- A1/user decision traceability,
- verified architecture inventory,
- one authoritative content source and consumption design,
- versioned contract/schema,
- strict validation and error behavior,
- complete state/objective/dialogue model,
- encounter request/result context,
- chapter/realm progression mapping,
- consequence orchestration and idempotency,
- artifact representation,
- save fields/defaults/migration/resume matrix,
- Android authoring/preview versus Unity runtime boundary,
- localization scope,
- diagnostics/correlation,
- required/optional/prohibited file impacts,
- shared-file locks,
- full happy/branch/failure/retry/reload/fault/invalid-data tests,
- C1–C4 decomposition and merge order,
- rollback/data-safety considerations,
- definition of done,
- unresolved blockers,
- and a complete Codex handoff prompt.

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

## Required foundation before consequence implementation

### Issue #136 — narrative save defaults

Current save normalization does not initialize missing:

- `Reputation`,
- `FactionReputations`,
- `LordPersona`.

#136 is a generic Codex compatibility fix and may proceed independently, but it must remain a separate PR. It must be complete before #134 applies affinity/faction/persona consequences.

## Archived and deferred scope

The broad content removed from A1 is preserved in dedicated backlog issues:

- **#129 — Phase 2:** Chapter 1 four-realm narrative spine.
- **#130 — Phase 3:** realm, building, dossier, and world narrative hooks.
- **#131 — Phase 4:** narrative ID governance, localization, and authoring templates.
- **#135 — deferred integration:** replace Android `UnityView` placeholder with a real Unity runtime bridge.
- **#137 — Phase 5:** crash-safe local save writes, backup, and recovery.

These issues preserve required future work without authorizing early implementation.

## Non-blocking technical follow-ups

The following issues are separate from A1 and must not be mixed into the narrative PR:

- **#126:** recurring post-success KSP/AWT diagnostic.
- **#127:** add committed Unity PlayMode smoke coverage.
- **#132 / PR #125:** update deprecated `QuestScreen` progress-indicator overload.

Codex may handle them only as separate focused work that does not disrupt the active dependency chain.

## Pull-request state

### PR #123

Phase 0 closeout, Phase 1 transition, A1/G1 templates, and risk register. Ready and mergeable.

### Draft PR #124

Archived mixed narrative branch. Never merge. Close as superseded only after the clean A1 PR is open and GPT verifies content preservation.

### PR #125

Independent one-line Android UI deprecation cleanup. GPT reviewed with no requested changes. It does not overlap A1.

### Future clean A1 PR

Must:

- link issue #128,
- start from latest `main`,
- use the A1 packet template,
- contain only bounded narrative-owned scope,
- report all IDs and reference validation,
- include focused packet tests,
- include exact Android commands/results,
- include Android Studio’s completion report,
- link user decisions,
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
| One bounded quest candidate | Selected; narrative approval pending | `OMEN_1` |
| D1–D12 decisions | Pending | Android Studio + user, #128 |
| Clean A1 branch from latest `main` | Pending | Android Studio, #128 |
| A1 internal references resolve | Pending | Android Studio, #128 |
| A1 state/objective/recovery/consequence intent complete | Pending | Android Studio + user |
| A1 packet tests and Android build pass | Pending | Android Studio, #128 |
| A1 ownership/completeness review | Pending | GPT |
| Save defaults foundation | Open | Codex, #136; required before consequences in #134 |
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
Deliverable: one corrected, user-approved OMEN_1 A1 narrative packet and focused PR
```

Parallel foundation work may proceed only in separate Codex PRs for #127, #132, or #136 without overlapping A1 files.

Do not start substantive G1 or NVS-01 runtime implementation until A1 is complete and user-approved.
