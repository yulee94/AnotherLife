# OMEN_1 A1 Narrative Completion Report

## Control

- Milestone: NVS-01, A1
- Packet: `OMEN_1_A1.packet.json`, version `omen1-a1-2026-08-13-v004`
- Primary mode: Codex narrative/content
- Upstream decision: issue #138, comment `4966062298`
- Tracked task: issue #128
- v004 amendment base: `238c7e32d2f3d33e4da6e186ae34ed279b09f35e`
- Accepted dependency: draft PR #479 at `ac56c77f08a5fe46a76458f2b91b5240bc2ae382`

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

Version `v003` replaces only the four eligible realm identifiers with the
canonical lowercase launch IDs: `crownlands`, `stonehold`, `eldergrove`, and
`umbral`. Realm eligibility and every approved quest, dialogue, choice,
objective, encounter, Retry, artifact, reward, report, chapter, and
localization meaning remain unchanged. G1, generated runtime data, validation,
adapters, and production integration require separate downstream
synchronization under issue #365.

Version `v004` removes direct `KINGDOM_COMMAND_VIEW` completion authority and
hands completion to `CH1_REALM_INTRO`. It also deletes only the premature
"My lord, " address from `dialogue.omen1.offer`; the key and the remainder of
the approved source sentence are unchanged. The offer remains `OFFERED`,
requires `SELECT_VALERIUS`, and cannot auto-accept. The final localized value
remains a separate copy-approval surface. Lord appointment, kingdom grant, and
Kingdom Management access are earned later through the appended Chapter 1
objectives accepted through PR #479.

The v004 source packet is 8,247 bytes, Git blob
`93f4eab24aac17fed83179bae19c2c4c8c71f16e`, and SHA-256
`25a5170334fca571abe1035eacf448955e8eab1124ff08643f7d16be9a1b69dd`.

## Boundaries

No Android runtime model, navigation, UI, Gradle, Unity runtime service, scene, save file, shared integration file, complete Chapter 1 content, or Android↔Unity bridge is changed. The existing Kotlin archive remains historical/runtime preview material and is not made authoritative by this packet.

The focused PowerShell validator is included only to satisfy #128's packet acceptance matrix. It validates IDs, internal references, state/objective/dialogue targets, D1–D16 presence, localization coverage, exact v004 placement and capability inventory, requested external classification, reachability, consequence-trigger conflicts, and the pre-appointment title boundary. It exercises seventeen negative fixtures and is not a general authoring framework.

## External dependencies and limitations

The Sky Castle marker, Deploy Champion action, arena request/results, artifact persistence, realm Chapter 1 handoff, localization runtime, and save/resume mechanics remain requested engineering capabilities. `KINGDOM_COMMAND_VIEW` is no longer an OMEN_1 completion capability. G1 must specify technical contracts, persistence, atomicity, correlation, validation, and idempotency before implementation.

Final localized copy remains unapproved. The unchanged legacy `500 Gold` reward wording and effect remain a separate #477 reconciliation dependency; v004 does not reinterpret or migrate them.

## Validation and handoff

Run:

```powershell
pwsh -NoProfile -File tools/narrative/Test-Omen1A1Packet.ps1
```

Exact results are recorded in the PR description after execution.

Codex coordination/review: review this clean A1 packet against issue #138 D1–D16, issue #128, PR #479, AGENTS.md, the Phase 1 risk register, and ownership boundaries. Engineering must synchronize the generated runtime catalog and contracts in a separate downstream change; this source report does not claim runtime integration, user approval, or release readiness.
