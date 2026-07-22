# OMEN_1 A1 Narrative Completion Report

## Control

- Milestone: NVS-01, A1
- Packet: `OMEN_1_A1.packet.json`, version `omen1-a1-2026-07-22-v002`
- Primary mode: Codex narrative/content
- Upstream decision: issue #138, comment `4966062298`
- Tracked task: issue #128
- Starting main: `1cfceef816f27616e1fb9c1d8d90fa79ccce11e8`

## Scope and fidelity

This packet contains exactly one quest, `OMEN_1` / “The First Signal.” It encodes D1–D16 without changing their product meaning: a post-realm offer from Veil Watch liaison Valerius; explicit Champion deployment; an encouraging transient failure and Retry loop; the Tear earned on arena success; a manual report; and Gold, affinity, completion, and the selected realm's Chapter 1 unlock at report conclusion.

The tone is urgent, mysterious, hopeful, and non-punitive. The player always has a visible next action and no failure creates missable permanent content.

## Stable narrative contract

- Entry: one committed Crownlands, Stonehold, Eldergrove, or Umbral realm; select Valerius to receive the offer.
- States: `OFFERED`, `TALK_TO_VALERIUS`, `INVESTIGATE_SKY_CASTLE`, transient `FAILED`, `REPORT_TO_VALERIUS`, `COMPLETED`.
- Objectives: talk, investigate/deploy, and “Present the Celestial Tear to Valerius.”
- Handoff: `HOOK_SKY_CASTLE_ARENA` and typed success/failure/cancel meanings are requested capabilities, not claimed runtime behavior.
- Consequence order: arena success acquires and retains the Tear once; report conclusion grants 500 Gold and +5 Valerius affinity, completes `OMEN_1`, and unlocks selected-realm `CH1_REALM_INTRO`, each once.
- Resume: exact dialogue node and unselected choice; authoritative encounter snapshot when available, otherwise penalty-free Retry; post-success reload retains the Tear while report consequences remain pending.
- Abandonment: permitted only outside an active encounter, returning to `OFFERED` and clearing active/unearned progress without deleting earned consequences.

Every dialogue line, choice, objective, speaker field, title, and description has a stable localization key with initial English source text in the packet.

Version `v002` removes the report objective's redundant inline `sourceText`. The unchanged English objective remains exclusively at localization key `objective.omen1.report`, preserving D7 and D13 while eliminating duplicate localization authority. No dialogue, choice, state, consequence, or player-facing text changed.

## Boundaries

No Android runtime model, navigation, UI, Gradle, Unity runtime service, scene, save file, shared integration file, complete Chapter 1 content, or Android↔Unity bridge is changed. The existing Kotlin archive remains historical/runtime preview material and is not made authoritative by this packet.

The focused PowerShell validator is included only to satisfy #128's packet acceptance matrix. It validates IDs, internal references, state/objective/dialogue targets, D1–D16 presence, localization coverage, requested external classification, reachability, and consequence-trigger conflicts, and exercises eight negative fixtures. It is not a general authoring framework.

## External dependencies and limitations

The Sky Castle marker, Deploy Champion action, arena request/results, artifact persistence, realm Chapter 1 unlock, command-view return, localization runtime, and save/resume mechanics remain requested engineering capabilities. A1 defines their narrative meaning only. G1 must specify technical contracts, persistence, atomicity, correlation, validation, and idempotency before implementation.

No unresolved creative decision remains within the approved A1 scope.

## Validation and handoff

Run:

```powershell
pwsh -NoProfile -File tools/narrative/Test-Omen1A1Packet.ps1
./gradlew.bat :app:testDebugUnitTest :app:assembleDebug --no-daemon
```

Exact results are recorded in the PR description after execution.

Codex coordination/review: review this clean A1 packet against issue #138 D1–D16, issue #128, AGENTS.md, the Phase 1 risk register, and ownership boundaries. Do not implement or rewrite narrative in this review. If complete and user-approved, activate #133 and produce G1 from `NVS_01_G1_Specification_Template.md`.
