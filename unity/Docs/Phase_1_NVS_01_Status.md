# Phase 1 NVS-01 Status

**Status date:** 2026-07-14  
**Roadmap phase:** Phase 1 — one approved quest line end to end  
**Status:** Active — documentation transition ready; user D1–D16 approval and clean A1 packet remain required

`AGENTS.md` is authoritative. Use this record with:

- `unity/Docs/Phase_0_Build_Health_Status.md`
- `unity/Docs/Phase_1_NVS_01_Risk_Register.md`
- `unity/Docs/NVS_01_A1_Packet_Template.md`
- `unity/Docs/NVS_01_G1_Specification_Template.md`
- `unity/Docs/NVS_01_Review_and_Acceptance_Checklists.md`
- issue #138
- issues #128, #133, and #134

## Phase entry

Phase 0 is green based on integrated-main evidence:

- Android unit tests passed.
- Android debug assembly passed.
- Unity `2022.3.62f3` import and C# compilation passed.
- EditMode smoke tests passed 3/3.
- PlayMode availability was reported accurately: no committed tests existed at validation time.
- `Assets/Test.unity` entered and exited Play Mode through a temporary validation probe.
- Issue #117 is complete.
- No Phase 0 blocker or shared-file lock remains.

PR #123 contains the detailed closeout evidence and Phase 1 documentation package.

## Milestone goal

Prove that one bounded, user-approved quest can move from Android Studio-owned narrative source to a playable, persistent runtime loop without:

- duplicated story authority,
- ownership drift,
- invalid references,
- unsafe old-save behavior,
- duplicate or partial consequences,
- silent runtime fallback,
- or uncontrolled merge overlap.

Selected candidate:

```text
Milestone: NVS-01
Quest: OMEN_1
Title: The First Signal
```

The candidate selection is not narrative approval.

## Active dependency chain

```text
PR #123 — merge Phase 0 closeout and Phase 1 controls
        ↓
#138 — user approves D1–D16 narrative intent
        ↓
#128 — Android Studio produces clean A1 packet
        ↓
#133 — GPT publishes approved G1 specification
        ↓
#134 — Codex implements C1–C4
        ↓
G2 — GPT technical/integration review
        ↓
A2 — Android Studio narrative-fidelity review
        ↓
U1 — user playtest and milestone acceptance
```

No downstream stage may start by inventing missing upstream intent.

## User decision gate: #138

**Owner:** User  
**Encoding owner after approval:** Android Studio  
**Blocks:** A1 completion, G1, and runtime implementation

Issue #138 requires explicit answers for:

- **D1:** dialogue-to-arena handoff,
- **D2:** arena failure recovery,
- **D3:** `FAILED` state meaning,
- **D4:** Valerius affinity trigger/repeatability,
- **D5:** Gold and Celestial Tear trigger/repeatability,
- **D6:** quest completion timing,
- **D7:** localization/source-text policy,
- **D8:** gameplay-hook status,
- **D9:** cancellation/abandonment,
- **D10:** chapter/realm placement,
- **D11:** Valerius role and speaker scope,
- **D12:** location presentation, realm prerequisites, and post-completion destination,
- **D13:** Celestial Tear acquisition/delivery/retention meaning,
- **D14:** report interaction,
- **D15:** quest-start trigger,
- **D16:** dialogue, handoff/arena, and success-before-report resume intent.

Required consistency:

```text
D2 agrees with D3.
D4–D6 agree on consequence/completion order.
D10 agrees with D11 and D12.
D13 agrees with D5 and report wording.
D14 agrees with D6 and the final objective.
D15 creates one deterministic start transition.
D16 agrees with failure, reward, report, and completion timing.
```

GPT and Codex must not choose these answers.

## A1 — clean narrative packet

**Issue:** #128  
**Owner:** Android Studio narrative workflow  
**Required branch:** `android-studio/nvs-01-a1-clean`  
**Template:** `unity/Docs/NVS_01_A1_Packet_Template.md`

### Goal

Produce exactly one bounded `OMEN_1` packet from fetched current `main`, encoding approved #138 decisions without runtime redesign.

### Required content

- D1–D16 approval traceability,
- stable milestone/context/quest/state/objective/dialogue/NPC/artifact/hook/location/event/localization IDs,
- purpose, scope, and non-goals,
- deterministic entry/start transition,
- complete states and transitions,
- objective activation/progress/completion,
- dialogue, choices, terminal, and semantic actions,
- encounter request and success/failure/cancel meaning,
- consequence timing and repeatability,
- Celestial Tear meaning,
- report interaction,
- failure/retry/cancellation/recovery,
- D16 resume intent,
- localization or approved source-text exceptions,
- external dependency declaration,
- packet validation tests,
- Android build evidence,
- completion report and exact GPT handoff.

### Prohibited A1 scope

Do not include:

- full Chapter 1,
- realm-wide strategic systems,
- broad building/research/world hooks,
- general governance/tooling,
- Android runtime model/navigation/Gradle changes,
- Unity services/interfaces/scenes/save/registration,
- shared contracts,
- Android↔Unity bridge implementation,
- unrelated maintenance.

### Archive preservation

Draft PR #124 remains source/reference only. It must never merge. Selectively preserve the bounded OMEN_1 content; defer broader work to #129–#131.

## G1 — GPT implementation specification

**Issue:** #133  
**Owner:** GPT  
**Status:** Blocked by #138 and approved A1  
**Template:** `unity/Docs/NVS_01_G1_Specification_Template.md`

G1 must define:

- D1–D16 technical traceability,
- verified current architecture,
- one authoritative runtime content source,
- versioned contract/schema,
- strict validation and error taxonomy,
- runtime state/objective/dialogue model,
- typed encounter request/result contract,
- chapter/realm/speaker/location/start mapping,
- Celestial Tear and report representation,
- save/default/migration/D16 resume matrix,
- consequence atomicity/idempotency,
- Android authoring/preview versus Unity runtime boundary,
- diagnostics,
- required/optional/prohibited files and locks,
- full happy/branch/failure/retry/cancel/reload/invalid/fault tests,
- C1–C4 order,
- rollback/data safety,
- Codex handoff.

GPT must not write substantive G1 while A1 is non-authoritative.

## C1–C4 — Codex implementation

**Issue:** #134  
**Owner:** Codex  
**Status:** Blocked by approved G1

Planned responsibilities:

- C1: contract, loading, and validation,
- C2: state, dialogue, objectives, and encounter handoff,
- C3: persistence, migration, consequence orchestration,
- C4: diagnostics, integration, and complete verification evidence.

Codex must not rewrite narrative or create a parallel implementation path.

## Required foundation and independent follow-ups

These remain separate from A1:

- **#126:** narrow KSP `2.3.5` → `2.3.6` tooling fix.
- **#127:** committed Unity PlayMode smoke coverage.
- **#132 / PR #125:** Compose progress overload cleanup.
- **#136:** initialize missing old-save reputation/faction/persona fields before #134 applies those consequences.

They may proceed only in focused, non-overlapping Codex PRs.

## Deferred scope

- **#129 — Phase 2:** Chapter 1 four-realm playable spine.
- **#130 — Phase 3:** realm/building/dossier/world narrative hooks.
- **#131 — Phase 4:** ID governance, localization, and authoring templates.
- **#135:** production Android↔Unity runtime bridge.
- **#137 — Phase 5:** crash-safe save writes, backup, and recovery.

## Pull-request state

### PR #123

Phase 0 closeout plus Phase 1 status, risk register, D1–D16-aligned A1/G1 templates, and G2/A2/U1 checklists. Documentation only. Recommended merge method: squash.

### Draft PR #124

Mixed archive. Never merge. Close as superseded only after clean A1 preservation is verified.

### PR #125

Independent one-line Android UI maintenance PR. GPT previously reviewed with no requested changes. It does not overlap the narrative dependency chain.

## Shared-file status

No designated shared file is currently locked.

Potential future files:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

A1 may not edit them. G1 must justify any required edit. The first approved implementation PR declaring a file holds its soft lock.

## Phase gate checklist

| Gate | Status | Owner/evidence |
| --- | --- | --- |
| Phase 0 green | Pass | PR #123 evidence |
| Phase 1 controls/templates merged | Pending | PR #123 |
| Bounded candidate selected | Pass as candidate | `OMEN_1` |
| D1–D16 approved | Pending | User, #138 |
| Clean A1 branch/packet | Pending | Android Studio, #128 |
| A1 references and packet tests | Pending | Android Studio |
| A1 ownership/completeness review | Blocked | GPT |
| Save defaults foundation | Open | Codex, #136 |
| G1 approved | Blocked | GPT, #133 |
| C1–C4 implemented/tested | Blocked | Codex, #134 |
| G2 approved | Blocked | GPT |
| A2 approved | Blocked | Android Studio |
| U1 accepted | Blocked | User |
| Shared locks released | Pass currently | none |

## Current next actions

```text
1. Finish review and squash-merge PR #123.
2. User answers D1–D16 in issue #138.
3. Android Studio creates android-studio/nvs-01-a1-clean from updated main.
4. Android Studio completes #128 with the A1 template and validation evidence.
5. GPT reviews A1, then activates #133 only if complete and approved.
```

Do not start substantive G1 or NVS-01 runtime implementation before those gates pass.
