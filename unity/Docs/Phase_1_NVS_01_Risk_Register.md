# Phase 1 NVS-01 Risk Register

**Status date:** 2026-07-14  
**Active phase:** Phase 1 — NVS-01  
**Active decision dependency:** issue #138 D1–D16  
**Active narrative dependency:** issue #128 clean A1 packet

This register consolidates verified risks. It does not choose narrative intent or prescribe implementation before approved A1/G1 handoffs.

Use with:

- `AGENTS.md`
- `unity/Docs/Phase_1_NVS_01_Status.md`
- `unity/Docs/NVS_01_A1_Packet_Template.md`
- `unity/Docs/NVS_01_G1_Specification_Template.md`
- `unity/Docs/NVS_01_Review_and_Acceptance_Checklists.md`
- issues #138, #128, #133, and #134
- draft PR #124

## Severity

- **Critical:** can corrupt data, invalidate the milestone, or cause ownership/merge failure.
- **High:** can make the slice incomplete, non-deterministic, or falsely appear implemented.
- **Medium:** can create drift or missing validation with a bounded workaround.
- **Low:** non-blocking quality or tooling issue.

## Status

- **Open:** needs a decision or implementation.
- **Blocked:** waits for an upstream approved artifact.
- **Contained:** isolated from the active path but unresolved.
- **Deferred:** intentionally scheduled later.
- **Mitigated:** controlled by current evidence/process.
- **Closed:** acceptance evidence is complete.

## Active risks

| ID | Severity | Risk | Verified evidence | Impact | Owner | Tracking/status |
| --- | --- | --- | --- | --- | --- | --- |
| R1 | Critical | Mixed archive branch can overwrite runtime fixes and violate ownership | Draft PR #124 changes 20 files across A1, Chapter 1, Android model, and Unity runtime | Regression, lost fixes, phase mixing | GPT + Android Studio | **Contained** — archive only; #128 clean branch |
| R2 | Critical | Narrative intent is incomplete or contradictory | Missing handoff target; conflicting failure/retry; undefined `FAILED`, start, report, Tear, and resume meaning | G1/Codex would have to invent intent | User + Android Studio | **Open** — #138 D1–D16, then #128 |
| R3 | High | No single authoritative runtime narrative catalog exists | Android packet/seeds, Unity fallback dialogue, generic quests, transient chapters | Duplicate IDs/text and disconnected progression | GPT | **Blocked** — #133 |
| R4 | High | Named encounter request/result contract is absent | Current flow uses direct scene loads; no verified `HOOK_SKY_CASTLE_ARENA` contract | Quest context/results can be lost | GPT + Codex | **Blocked** — D8, #133/#134 |
| R5 | High | `SKY_CASTLE` is absent from current world atlas | Existing atlas has realm homelands and warzones, not Sky Castle | Locate/zone assumptions can fail | User + Android Studio + GPT | **Open** — D12; technical design #133 |
| R6 | High | Android Unity bridge is a placeholder | `UnityView.kt` displays text rather than `UnityPlayer` | Embedded Android→Unity→Android loop is unproven | GPT + Codex | **Deferred** — #135 |
| R7 | High | Chapter, realm, speaker, location, and start semantics conflict | Save uses `C1`; narrative uses realm chapter IDs/archived prologue; `AdvanceStory()` does not mutate chapter | Wrong eligibility or false progression | User + Android Studio, then GPT | **Open** — D10–D12/D15; **Blocked** #133 |
| R8 | High | Old saves may contain null narrative fields | Normalization omits `Reputation`, `FactionReputations`, `LordPersona` | Effects can silently fail or throw | Codex | **Open** — #136; required before #134 consequences |
| R9 | High | Multi-service consequences lack an atomic boundary | Affinity saves immediately; resources and quest paths save separately; artifact path undefined | Partial application or duplication after retry/crash | GPT + Codex | **Blocked** — D4–D6/D13/D14, #133/#134 |
| R10 | High | Celestial Tear meaning is contradictory | Archive says deliver Tear while also granting it as a reward | Wrong objective wording and persistence model | User + Android Studio | **Open** — D5/D13/D14 |
| R11 | High | Missing dialogue can fail silently | `GetDialogue` returns null; `TriggerDialogue` emits nothing | Stalled or skipped narrative with no clear error | GPT + Codex | **Blocked** — strict G1 validation/error behavior |
| R12 | High | Resume behavior can duplicate or lose progress | No approved mid-dialogue, arena, success-before-report, or partial-consequence resume model | Duplicate rewards or lost objectives | User then GPT/Codex | **Open** — D16; **Blocked** #133/#134 |
| R13 | Medium | `end` is convention, not a typed terminal | Dialogue model stores arbitrary string targets | Missing references can masquerade as completion | Android Studio + GPT | **Open** — A1 declaration; G1 validation |
| R14 | Medium | Objective lifecycle is incomplete | Archived objectives lack deterministic activation/completion mapping | UI/save/runtime cannot be specified | Android Studio | **Open** — #128, D14/D15 |
| R15 | Medium | Android QuestScreen is not authoritative or integrated | Shell does not mount it; claim/locate behavior is placeholder | Preview can drift from Unity runtime | GPT + Codex | **Blocked** — #133; no A1 runtime edits |
| R16 | Medium | Transient chapter creation is not a usable catalog | `AddChapter()` creates objects but service exposes no chapter retrieval | Adding OMEN_1 there would not create runtime authority | GPT + Codex | **Blocked** — #133/#134 |
| R17 | Medium | Localization keys have no verified runtime pipeline | No broad Unity localization support; Android strings coverage is minimal | Keys can exist without runtime consumption | Android Studio + GPT + Codex | **Open** — D7; broad tooling #131 |
| R18 | Medium | Existing shared loaders allow silent fallback | Skill catalog loader returns false/null and runtime defaults can take over | Story data could disappear or substitute silently | GPT + Codex | **Blocked** — G1 strict fallback policy |
| R19 | Medium | No committed PlayMode smoke existed at Phase 0 validation | Runner discovered zero tests; representative scene used temporary probe | Scene regressions lack repeatable automation | Codex | **Open** — #127 |
| R20 | Medium | Save writes are not crash-safe | Direct write to one `save.json`; no backup/recovery | Partial write can lose profile | GPT + Codex | **Deferred** — #137 |
| R21 | Low | KSP `2.3.5` emits a known post-success command-line exception | Official upstream fix is `2.3.6` | Noise can obscure real failures | Codex | **Open** — #126 narrow bump |
| R22 | Low | Deprecated Compose progress overload | One warning in `QuestScreen.kt` | Warning debt only | Codex | **Mitigated pending merge** — #132 / PR #125 |

## Decision dependencies

### User decisions required in #138

- D1 dialogue-to-arena handoff.
- D2 arena failure recovery.
- D3 `FAILED` meaning.
- D4 affinity trigger/repeatability.
- D5 Gold/Tear trigger/repeatability.
- D6 completion timing.
- D7 localization/source-text policy.
- D8 hook status.
- D9 cancellation/abandonment.
- D10 chapter/realm placement.
- D11 Valerius/speaker scope.
- D12 location presentation, realm prerequisites, and post-completion destination.
- D13 Celestial Tear acquisition/delivery/retention meaning.
- D14 report interaction.
- D15 quest-start trigger.
- D16 dialogue/arena/success resume intent.

Consistency requirements:

```text
D2 ↔ D3
D4–D6
D10–D12
D5 ↔ D13 ↔ D14
D6 ↔ D14
D15 ↔ initial state/objective
D16 ↔ D2/D3/D5/D6/D14
```

### Android Studio decisions after user approval

- exact approved wording and stable IDs,
- packet-local objective/state/dialogue tables,
- source-text/localization inventory,
- continuity notes,
- external dependency declarations,
- focused packet tests.

Android Studio may not redesign runtime architecture.

### GPT decisions only after approved A1

- authoritative runtime representation,
- schema/version/fields,
- loader and validator,
- encounter request/result contract,
- chapter/realm/location/start mapping,
- artifact/report runtime representation,
- persistence/defaults/migration/D16 resume,
- consequence atomicity/idempotency,
- diagnostics,
- required/optional files and locks,
- test architecture and C1–C4 order.

### Codex decisions only after approved G1

- narrow implementation details within the approved contract,
- test implementation,
- diagnostics,
- safe defaults/migrations,
- required refactoring limited to the specified scope.

## Shared-file risk

Potential future implementation may affect:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

Current status: no shared file is locked.

Rules:

1. A1 may not edit these files.
2. G1 must justify required versus optional impact.
3. The first approved implementation PR declaring a file holds the soft lock.
4. Save fields require backward-compatible defaults and old-save tests.
5. Service-registration conflicts preserve all valid registrations.
6. Generated artifacts must be deterministic and reviewable.
7. Lock release requires merged/closed PR evidence and a clean follow-up state.

## Mitigation order

1. Complete review and squash-merge PR #123.
2. Obtain explicit #138 D1–D16 user approval.
3. Create `android-studio/nvs-01-a1-clean` from updated `main`.
4. Complete #128 with the D1–D16-aligned template and packet tests.
5. Preserve PR #124 until GPT verifies transfer; then close without merge.
6. Activate #133 only after A1 approval.
7. Complete #136 before #134 applies affinity/faction/persona consequences.
8. Activate #134 only after approved G1 and shared-file declarations.
9. Perform G2, A2, and U1 in order.
10. Update this register at every gate transition.

## Risk review cadence

GPT updates this register when:

- #138 approves or changes a decision,
- A1 changes scope or resolves a risk,
- an implementation PR opens,
- a shared file is declared,
- validation discovers a new root cause,
- a risk is accepted or deferred by the user,
- or a phase gate changes.

A risk is not closed solely because code compiles. Close it only against acceptance evidence, ownership review, persistence behavior, and player-visible results.
