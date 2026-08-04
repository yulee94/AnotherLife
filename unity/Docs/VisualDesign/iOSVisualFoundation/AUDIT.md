# iOS Visual Foundation Audit

**Audit date:** 2026-08-04

**Primary Codex mode:** coordination/review

**Disposition:** Ready for source-mode review
**Roadmap context:** The active Phase 1/NVS-01 gates remain authoritative. This artifact records user-reprioritized visual direction and does not activate deferred multiplayer, economy, combat, or realm-transfer runtime work.

## Goal

Preserve the user-directed iOS visual and interaction foundation as an executable review artifact with a small, representative evidence set. The package should let reviewers inspect the intended flow without changing Unity runtime authority.

## Non-goals

- No Unity scene, prefab, C# runtime, save schema, catalog, package, importer, build setting, or platform setting changes.
- No production economy balances, resource-pack entitlements, construction balance, combat formulas, enemy AI, rewards, multiplayer synchronization, spatial streaming, or server reconciliation.
- No player-operated realm switching. The paid realm-change idea is a future shop/service concept only and is not authorized by this package.
- No claim that the 300+ player world, close battlefield, or direct reward application is production-ready.
- No copied competitor art, icons, UI skin, names, lore, or monetization framing.

## Repository scope

All changed files are contained in `unity/Docs/VisualDesign/iOSVisualFoundation/`.

- `index.html` — dependency-free interaction prototype.
- `README.md` and `DESIGN.md` — behavior, visual direction, and production boundaries.
- `assets/` — the already-approved AL icon and four Arcane Axis realm marks, retained so the review artifact is self-contained. Their content matches existing Git LFS objects in `unity/Assets/AL/Art/`.
- `previews/` — 22 selected screenshots covering the approved flow; historical and redundant captures were excluded.
- `visual-verdict-*.json` — retained structured visual-review results.

No designated shared file is touched.

## Acceptance criteria and evidence

- [x] Launch, realm selection, Champion creation, arrival, and all four realm domains are represented.
- [x] The selected realm remains the active realm after onboarding; no self-service realm switcher appears during normal play.
- [x] The top resource ledger uses icon plus amount only, while each resource opens purpose, balance, five pack tiers, and an explicit quantity confirmation.
- [x] Construction uses a global queue, visibly ticks once per second, completes at zero, advances the building level, and refreshes the following level's benefits, duration, costs, and action.
- [x] The world is presented as a large pannable sector for a 300+ player-castle target, with one castle per user and Bird's-eye, Quarter, and 3D camera states.
- [x] Player, Bandit, Gate, and Realm targets use an explicit battlefield-entry confirmation.
- [x] Every visible battlefield enemy is independently selectable and damageable; victory waits until every enemy is defeated.
- [x] The reward card appears only after complete victory, applies once inside the prototype, and returns to the same World target and camera state.
- [x] The compact iPhone layouts retain required controls and avoid document-level horizontal overflow in the reviewed 375 × 667 and 390 × 844 views.
- [x] The artifact contains no external scripts, network calls, credentials, analytics, storage writes, or dynamic code execution.

## Audit findings

No P0 or P1 finding was identified.

1. **Resolved — evidence size:** the source review folder contained 62 PNG captures. The repository package keeps 22 representative captures and excludes the historical duplicates.
2. **Watch — spoken timer cadence:** the construction timer updates visually once per second but intentionally does not announce every second. Completion is announced through the adjacent polite status message. Production iOS validation should confirm an appropriate VoiceOver milestone cadence without creating repetitive announcements.

## Production boundaries and blockers

- Realm selection follows the one-committed-realm direction in [`../../Realm_Selection_Integrity_Spec.md`](../../Realm_Selection_Integrity_Spec.md). In-place paid transfer remains unapproved.
- Resource packs and prototype rewards do not override [`../../Economy_Integrity_Spec.md`](../../Economy_Integrity_Spec.md); positive display values are not reward or entitlement authority.
- Battle completion and reward presentation do not override [`../../Champion_Combat_Encounter_Integrity_Spec.md`](../../Champion_Combat_Encounter_Integrity_Spec.md) or [`../../Battle_Computation_Result_Transaction_Spec.md`](../../Battle_Computation_Result_Transaction_Spec.md).
- The world-map population is a presentation target, not a network architecture or current real-time multiplayer guarantee. Runtime work remains subject to [`../../Product_Direction.md`](../../Product_Direction.md) and the current roadmap gates.
- Final art, character/enemy models, environmental detail, device performance budgets, localization, color-vision checks, VoiceOver traversal, physical-device touch behavior, authoritative data, and production networking remain unresolved.

## Validation performed

- Static JavaScript syntax validation.
- Browser-console error check.
- Final screenshot-to-reference visual verdict: 96/100, pass.
- Direct browser review of the complete multi-screen interaction.
- Compact iPhone viewport review at 375 × 667 and standard modern iPhone review at 390 × 844.
- Manual state-transition checks for realm continuity, pack quantity clamping, construction countdown/completion/next-level refresh, world pan/camera persistence, independent enemy health, all-enemies victory gating, one-time reward claim, and World return.
- Review against the repository product direction, realm selection, economy, battle, cross-platform visual, and competitive benchmark documents.

## Not validated as production

- Unity import, play mode, iOS Player build, App Store packaging, physical iPhone performance, touch latency, VoiceOver on device, networking, persistence, migration, retry/reconnect, localization, or live-service security.

Those checks are intentionally not applicable to this static design-evidence PR and become required when an approved production implementation is scoped.
