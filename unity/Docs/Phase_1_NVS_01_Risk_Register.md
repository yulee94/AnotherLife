# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-14  
**Active phase:** Phase 1 — NVS-01  
**Active dependency:** Issue #128 A1 narrative packet

This register consolidates verified risks discovered during GPT orientation. It does not choose narrative intent or prescribe implementation before the approved A1/G1 handoff.

Use with:

- `AGENTS.md`
- `unity/Docs/Phase_1_NVS_01_Status.md`
- `unity/Docs/NVS_01_A1_Packet_Template.md`
- issue #128
- issue #133
- issue #134
- draft PR #124

## Severity and status definitions

### Severity

- **Critical:** Can corrupt data, invalidate the active milestone, or cause ownership/merge failure.
- **High:** Can make the vertical slice incomplete, non-deterministic, or falsely appear implemented.
- **Medium:** Can create drift, technical debt, or missing validation but has a bounded workaround.
- **Low:** Non-blocking quality or tooling issue.

### Status

- **Open:** Needs an owner decision or implementation.
- **Blocked:** Cannot proceed until an upstream artifact is approved.
- **Contained:** Risk is isolated from the active path but not resolved.
- **Deferred:** Intentionally scheduled for a later roadmap phase.
- **Mitigated:** Evidence or process currently controls the risk.
- **Closed:** Acceptance evidence is complete.

## Active risks

| ID | Severity | Risk | Verified evidence | Impact | Owner | Tracking/status |
| --- | --- | --- | --- | --- | --- | --- |
| R1 | Critical | Mixed narrative branch could overwrite runtime fixes and violate ownership | Draft PR #124 is diverged and changes 20 files across A1, Chapter 1, Android model, and Unity runtime | Merge could regress `Quest` compatibility, remove definitions, or mix phases | GPT + Android Studio | **Contained** — PR #124 archive only; #128 requires clean branch |
| R2 | Critical | A1 has unresolved creative contradictions | Missing arena dialogue target; conflicting failure/retry; unused `FAILED`; reward/artifact timing unclear | G1 would have to invent story intent or implement contradictory paths | Android Studio + user | **Open** — D1–D12 in #128 |
| R3 | High | No single authoritative runtime narrative catalog exists | Android packet, hard-coded Android quest seeds, `LocalStoryService` fallback, generic Unity quests, transient chapters | Duplicate text/IDs and disconnected progression | GPT after A1 | **Blocked** — #133 |
| R4 | High | Named encounter handoff is not implemented | No inspected story/quest contract for `HOOK_SKY_CASTLE_ARENA`; current scene flow is direct `LoadScene` | Quest context and success/failure result can be lost | GPT/Codex | **Blocked** — #133/#134 |
| R5 | High | `SKY_CASTLE` is not in current world atlas | Current atlas registers realm homelands and five warzones, not Sky Castle | Locate action/zone assumption may fail | Android Studio + GPT | **Open** narrative decision in #128; technical design blocked in #133 |
| R6 | High | Android Unity bridge is only a placeholder | `UnityView.kt` displays text and does not instantiate `UnityPlayer` | Android shell cannot currently prove embedded quest→arena→result loop | GPT/Codex | **Deferred** — #135; NVS-01 should not absorb full embedding by default |
| R7 | High | Chapter IDs and advancement semantics conflict | Save defaults `C1`; narrative IDs include `C1_CL` etc.; archived packet uses `CH0_PROLOGUE`; `AdvanceStory()` does not mutate chapter | Completion may emit an event without persisted progression | Android Studio/user then GPT | **Open** D10–D12 / **Blocked** #133 |
| R8 | High | Old saves may have null narrative fields | `EnsureSaveDefaults` omits `Reputation`, `FactionReputations`, `LordPersona` | Affinity/faction/persona effects may silently fail or throw | Codex | **Open** — #136; required before #134 consequence implementation |
| R9 | High | Multi-service consequences lack one atomic boundary | Affinity saves immediately; resources mutate memory; quest paths save separately; artifact path undefined | Partial completion and duplicate consequences after crash/retry | GPT/Codex | **Blocked** — #133/#134; fault-boundary tests required |
| R10 | High | Celestial Tear narrative and ownership are undefined | Objective says deliver Tear; archived consequence adds Tear as reward; no verified artifact inventory service | Story inconsistency and wrong persistence model | Android Studio + user | **Open** D5/D6 and artifact clarification in #128 |
| R11 | High | Missing dialogue currently fails silently | `GetDialogue` returns null; `TriggerDialogue` emits nothing | Invalid packet may stall or silently skip presentation | GPT/Codex | **Blocked** — strict validation/error behavior in #133/#134 |
| R12 | Medium | `end` is convention, not typed contract | Fallback dialogues use `NextNodeId = "end"`; model stores plain string | Missing targets could be mistaken for terminal | Android Studio + GPT | **Open** A1 declaration; G1 validation rule |
| R13 | Medium | Objective lifecycle is not defined in archived A1 | Three objectives exist without activation/completion mapping | UI/progress/save behavior cannot be specified deterministically | Android Studio | **Open** — #128/template |
| R14 | Medium | Android QuestScreen is not an integrated authoritative quest flow | Main shell does not mount it; claim is placeholder; reward text fixed; locate is callback only | Android preview could drift from Unity runtime | GPT/Codex | **Blocked** — #133; do not change in A1 |
| R15 | Medium | Existing `InitializeStoryData()` creates unregistered chapters | `AddChapter()` creates transient definitions; `IGameDataService` has no chapter/quest/dialogue retrieval | Adding OMEN_1 there would not create a usable catalog | GPT/Codex | **Blocked** — #133/#134 |
| R16 | Medium | Localization metadata has no runtime pipeline | No Unity Localization package; Android strings only app name | Keys can be authored but not consumed yet | Android Studio/GPT/Codex | **Open** D7; broad tooling deferred #131 |
| R17 | Medium | Shared JSON loaders currently allow silent fallback | Skill catalog loader returns false/null and runtime defaults can take over | Authoritative story could silently substitute or disappear | GPT/Codex | **Blocked** — #133 strict error policy |
| R18 | Medium | No committed PlayMode tests | Phase 0 runner found zero tests; representative scene used temporary probe | Future regressions lack committed automated scene coverage | Codex | **Open** — #127 |
| R19 | Medium | Save writes are not crash-safe | Direct `File.WriteAllText` to one `save.json`; no backup/recovery | Partial write can lose profile | Codex/GPT | **Deferred** — #137 Phase 5; G1 must not worsen |
| R20 | Low | Recurring post-success KSP/AWT diagnostic | Gradle succeeds but background NPE appears | Tool noise may hide real failures or affect reliability | Codex | **Open** — #126 |
| R21 | Low | Deprecated Compose progress overload | Build warning in `QuestScreen.kt` | Warning debt only | Codex | **Mitigated pending merge** — #132 / PR #125 |

## Shared-file risk

Potential future NVS-01 implementation may affect:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

No shared file is currently locked for A1.

Rules:

1. A1 may not edit these files.
2. G1 must justify required versus optional impact.
3. The first implementation PR declaring a file holds its soft lock.
4. Save fields require backward-compatible defaults and old-save tests.
5. Service registration conflicts preserve all valid registrations.
6. Generated assets or catalog changes must be deterministic and reviewable.

## Decision dependencies

### User/Android Studio decisions required before G1

- D1 dialogue-to-arena sequence.
- D2 failure recovery.
- D3 `FAILED` meaning.
- D4 affinity timing/repeatability.
- D5 reward timing/repeatability.
- D6 completion timing.
- D7 localization policy.
- D8 hook status.
- D9 cancellation.
- D10 chapter/realm placement.
- D11 Valerius scope.
- D12 location/access/post-completion destination.
- Celestial Tear narrative meaning.
- Explicit objective lifecycle.

### GPT decisions allowed only after A1 approval

- Runtime source-of-truth representation.
- Contract/schema version and fields.
- Loader/validator design.
- Encounter context/result contract.
- Save state and migration.
- Consequence orchestration/idempotency.
- Android preview/Unity runtime relationship.
- Required/optional files and locks.
- Test architecture and merge order.

### Codex decisions allowed only after G1 approval

- Narrow implementation details within the approved interfaces/contracts.
- Test implementation and diagnostics.
- Safe migrations/defaults.
- Reuse/refactor limited to required scope.

## Current mitigation plan

1. Merge PR #123 to place Phase 0 closeout, Phase 1 status, template, and risk register on `main`.
2. Merge independent PR #125 or otherwise keep it separate from A1.
3. Create clean `android-studio/nvs-01-a1-clean` from updated `main`.
4. Complete #128 using user-approved D1–D12 and packet tests.
5. Preserve PR #124 until GPT confirms content transfer, then close without merge.
6. Activate #133 only after A1 approval.
7. Resolve #136 before #134 applies affinity/faction/persona consequences.
8. Activate #134 only after G1 approval and shared-file declaration.
9. Perform G2, A2, and U1 in order.

## Risk review cadence

GPT updates this register when:

- A1 resolves a decision or changes scope,
- a new implementation PR opens,
- a shared file is declared,
- validation discovers a new root cause,
- a risk is accepted/deferred by the user,
- or a phase gate changes.

No risk is considered closed solely because code compiles. Close it only against its acceptance evidence and ownership review.
