# First-User Localization Semantics and Privacy Boundaries

## Packet authority

\`\`\`text
packetId: a3-first-user-localization-semantics-2026-08-12-v001
baseline: main@6b79dcbbeb2f9917ae30b42548742b7fc70307b0
status: ProvisionalPlanningSource
finalLocalizedValuesApproved: false
runtimeOrSaveAuthority: false
implementationRouteApproved: false
appearanceVoicePronounAuthority: false
creditsUsed: 0
\`\`\`

A1 granted full planning approval to this packet's semantic model. This packet fixes semantic referents, key intent, typed interpolation boundaries, retry/cancel meaning, privacy constraints, and continuity references. It authors no localized value and grants no downstream implementation or user approval.

Current-main realm catalog evidence:
- Existing draft localization key IDs: \`realm.lock.warning\`, \`realm.crownlands.selection.line\`, \`realm.stonehold.selection.line\`, \`realm.eldergrove.selection.line\`, \`realm.umbral.selection.line\`.
- Their current English \`localizationDrafts\` are retained draft evidence only, not final-copy approval.
- Every other key below is a proposed planning key and is not claimed merged.
- Current realm \`displayName\` values are not a replacement for a versioned localization key.

## Classification vocabulary

- \`MO-SR\`: machine-only; source-resolved; copy not applicable; never sent to localization or rendered.
- \`PF-SR-CB\`: player-facing semantic is source-resolved; all localized values remain copy-blocked.
- \`PF-REF-CB\`: player-facing reference/placement intent only; source asset, exact narrative value, or placement still blocked; localized value blocked.
- \`EXISTING-DRAFT\`: exact key ID exists on current main, but its value remains non-final.
- \`PROPOSED\`: planning key ID only; no repository-presence claim.

## Global interpolation and privacy rules

Typed variables permitted by this packet:

| Variable | Type/source | Rule |
|---|---|---|
| \`realmName\` | localized label resolved from exact realm ID | Never use raw \`realmId\` as fallback. |
| \`identityName\` | localized label resolved from exact identity ID | Owner-visible only; never a diagnostic/log value. |
| \`firstParentName\` | localized label from immutable canonical pair order | Never reorder the stored pair or make it realm-relative. |
| \`secondParentName\` | localized label from immutable canonical pair order | Never derive from selected realm. |
| \`canonicalFirstParentPercent\` | integer 30..70 from validated Half draft | Locale-format for display; persistence remains exact integer. |
| \`secondParentPercent\` | integer \`100 - canonicalFirstParentPercent\` | Display derivation only; never persisted as competing authority. |
| \`publicHandle\` | owner-visible escaped draft value | Render as a separate value component, not interpolated into error/status copy. Bidi-isolate and never log. |

No other interpolation is authorized. In particular, player-facing templates may never receive:
- \`ProfileId\`, \`AccountId\`, \`CharacterId\`
- \`onboardingOperationId\` / raw \`Idempotency-Key\`
- \`semanticRequestFingerprint\`
- \`receiptId\`, \`commitId\`, revisions, receipt/ledger digests, event ranges
- reservation/migration/auth tokens
- raw realm/origin/presentation/catalog IDs
- raw exception, HTTP/database status, save path/content, device identifier, or support diagnostic

Missing localization never falls back to a machine ID or raw exception. Missing realm-lock, account-scope, percent-meaning, or irreversible-submit semantics blocks pre-submit confirmation. After submit, a missing specialized value must use an approved privacy-safe generic status surface; it never returns the player to editable success or changes authority.

Owner-visible realm/origin/percent/presentation/handle selections are permitted on their review surfaces but prohibited from ordinary logs, analytics payloads, crash messages, and support strings unless a later privacy policy explicitly authorizes a protected channel.

## Retry/cancel consequence codes

- \`RC-LAUNCH\`: readiness/media failure stays before Loading Complete; retry only failed readiness. Continue is idempotent. Exit creates no onboarding mutation.
- \`RC-REALM\`: browsing/review is uncommitted. Back may preserve a bounded in-session draft. Cancel discards realm and dependent drafts. Realm change revalidates origin; no Gem/resource/quest mutation.
- \`RC-ORIGIN\`: invalid input rejects in place with no normalization/default. Cancel clears origin and downstream handle draft. Half \`50\` may exist only as an in-memory draft after explicit eligible-pair selection.
- \`RC-PRESENTATION\`: retry changes only the body-presentation draft. Cancel clears uncommitted character/handle state. No voice or pronoun inference.
- \`RC-HANDLE\`: only handle remains editable on conflict. Service failure preserves other drafts. Expired reservation requires revalidation. Back/cancel best-effort releases reservation; uncertain release is not reusable authority.
- \`RC-PRECOMMIT\`: before submit, back/cancel is legal and creates no server identity. Submission requires all critical semantics present.
- \`RC-SERVER\`: after submit, stop waiting is not cancellation. Reconcile with the unchanged \`{onboardingOperationId == Idempotency-Key, semanticRequestFingerprint}\`; no new key, blind resubmit, replacement IDs, or editable-success claim.
- \`RC-PROJECTION\`: server receipt remains authoritative. Retry/repair local receipt-to-ProfileId projection only; never rollback, compensate, delete, recreate, or recommit the server character.
- \`RC-PROLOGUE\`: after receipt and local projection verify, retry only scene handoff; never recommit or reproject.
- \`RC-REFERENCE\`: no command or authority consequence; reference key alone cannot mutate state.

## Continuity reference codes

- \`CT-LAUNCH\`: terminal launch beat -> explicit Loading Complete interaction -> realm draft; no automatic transition.
- \`CT-REALM\`: exact account realm lock; sub-characters conform to that realm; pre-commit preview grants nothing.
- \`CT-ORIGIN\`: exact ten IDs, literal 16-valid/24-invalid realm grid, Pure percent omission, Half integer 30..70, canonical first-parent orientation.
- \`CT-PRESENTATION\`: exact \`male|female\` body-presentation IDs only; no voice/pronoun inference and no appearance authority.
- \`CT-HANDLE\`: public handle is account-facing, not login credential, character name, or any technical ID; normalization/rename/privacy policy remains unresolved.
- \`CT-COMMIT\`: one exact operation binding; server receipt establishes AccountId/CharacterId/realm/origin/presentation/handle; no ProfileId or appearance authority.
- \`CT-PROJECTION\`: separate verified local projection binds immutable receipt to local ProfileId/revision; failure is CommittedProjectionPending/RecoveryRequired.
- \`CT-NVS\`: prologue context \`POST_REALM_PROLOGUE\`; \`OMEN_1\` offer action \`SELECT_VALERIUS\`; no auto-accept; no \`C1\` on entry; completion unlock target \`CH1_REALM_INTRO\`.
- \`CT-WISH\`: eight Gems remain reference-only; \`wishgate_eightfold_concordance\` and \`wishgate.vaeloryn.name\` do not authorize possession, reward, meeting, invocation, display-name copy, or equivalence to any realm dragon/guardian.

## Machine-only glossary

All entries in this section are \`MO-SR\`, have no interpolation, use the applicable retry code only as machine-state semantics, and are forbidden from player copy.

### State IDs

| Machine key | Consequence | Continuity |
|---|---|---|
| \`first_user.loading_complete\` | RC-LAUNCH | CT-LAUNCH |
| \`first_user.realm_draft\` | RC-REALM | CT-REALM |
| \`first_user.character_identity_draft\` | RC-ORIGIN / RC-PRESENTATION | CT-ORIGIN / CT-PRESENTATION |
| \`first_user.public_handle_draft\` | RC-HANDLE | CT-HANDLE |
| \`first_user.commit_ready\` | RC-PRECOMMIT | CT-COMMIT |
| \`first_user.commit_pending\` | RC-SERVER | CT-COMMIT |
| \`first_user.commit_recovery\` | RC-SERVER | CT-COMMIT |
| \`first_user.committed_projection_pending\` | RC-PROJECTION | CT-PROJECTION |
| \`first_user.recovery_required\` | RC-PROJECTION | CT-PROJECTION |
| \`first_user.commit_verified\` | RC-PROLOGUE | CT-COMMIT / CT-PROJECTION |
| \`first_user.prologue_transition\` | RC-PROLOGUE | CT-NVS |
| \`first_user.prologue_active\` | RC-PROLOGUE | CT-NVS |

### Authority and recovery fields

\`onboardingOperationId\` is exactly the byte-for-byte A6 \`Idempotency-Key\`; \`semanticRequestFingerprint\` is the second binding element. Both are machine-only. \`receiptId\`, \`commitId\`, \`ProfileId\`, \`AccountId\`, \`CharacterId\`, expected/applied revisions, receipt digest, ledger references, handle-reservation references, and migration provenance are also machine-only.

\`onboardingOperationId\` is header/transport-only and never a genesis-body member, receipt member, internal database transaction ID, player identity, localized value, or raw log value.

### Realm ID -> localization-key map

| Machine realm ID | Player name key | Selection-line key | Classification |
|---|---|---|---|
| \`crownlands\` | \`realm.crownlands.name\` | \`realm.crownlands.selection.line\` | name PF-SR-CB/PROPOSED; line PF-SR-CB/EXISTING-DRAFT |
| \`stonehold\` | \`realm.stonehold.name\` | \`realm.stonehold.selection.line\` | name PF-SR-CB/PROPOSED; line PF-SR-CB/EXISTING-DRAFT |
| \`eldergrove\` | \`realm.eldergrove.name\` | \`realm.eldergrove.selection.line\` | name PF-SR-CB/PROPOSED; line PF-SR-CB/EXISTING-DRAFT |
| \`umbral\` | \`realm.umbral.name\` | \`realm.umbral.selection.line\` | name PF-SR-CB/PROPOSED; line PF-SR-CB/EXISTING-DRAFT |

All eight player keys use no interpolation, \`RC-REALM\`, and \`CT-REALM\`. Selection lines are optional continuity flavor; minimal-prose onboarding and irreversible comprehension must not depend on showing them.

### Origin ID -> localization-key / percent map

Every machine ID is \`MO-SR\`. Every label key is \`PF-SR-CB/PROPOSED\`, has no interpolation, uses \`RC-ORIGIN\`, and references \`CT-ORIGIN\`.

| Exact machine identity ID | Kind | Player label key | Canonical parent semantics |
|---|---|---|---|
| \`race_human\` | Pure | \`origin.race_human.name\` | percent member absent |
| \`race_dwarf\` | Pure | \`origin.race_dwarf.name\` | percent member absent |
| \`race_elf\` | Pure | \`origin.race_elf.name\` | percent member absent |
| \`race_dark_elf\` | Pure | \`origin.race_dark_elf.name\` | percent member absent |
| \`heritage_half_human_dwarf\` | Half | \`origin.heritage_half_human_dwarf.name\` | first Human; second Dwarf |
| \`heritage_half_human_elf\` | Half | \`origin.heritage_half_human_elf.name\` | first Human; second Elf |
| \`heritage_half_human_dark_elf\` | Half | \`origin.heritage_half_human_dark_elf.name\` | first Human; second Dark Elf |
| \`heritage_half_dwarf_elf\` | Half | \`origin.heritage_half_dwarf_elf.name\` | first Dwarf; second Elf |
| \`heritage_half_dwarf_dark_elf\` | Half | \`origin.heritage_half_dwarf_dark_elf.name\` | first Dwarf; second Dark Elf |
| \`heritage_half_elf_dark_elf\` | Half | \`origin.heritage_half_elf_dark_elf.name\` | first Elf; second Dark Elf |

Aliases, reversed IDs, case/space/separator variants, and \`LegacyRaceUnspecified\` are not localization fallbacks or selectable labels.

### Body-presentation ID map

| Machine ID | Player key | Classification | Variables | Consequence | Continuity |
|---|---|---|---|---|---|
| \`male\` | \`first_user.presentation.male\` | PF-SR-CB/PROPOSED | none | RC-PRESENTATION | CT-PRESENTATION |
| \`female\` | \`first_user.presentation.female\` | PF-SR-CB/PROPOSED | none | RC-PRESENTATION | CT-PRESENTATION |

These labels identify body presentation only. No key or localized value may claim voice, pronoun, body-size, tail, appearance module, or gameplay effect.

## Player-facing key inventory

Every key below has privacy rule “no technical ID/raw error fallback.” Additional restrictions are noted.

### Terminal launch and Loading Complete

| Key | Class/repo | Variables | Retry/cancel | Continuity / privacy intent |
|---|---|---|---|---|
| \`first_user.launch.logo.accessibility_label\` | PF-REF-CB/PROPOSED | none | RC-REFERENCE | CT-LAUNCH; reserve only if the final AL mark is semantically meaningful. Decorative-vs-meaningful role and asset remain visual/user blocked. |
| \`first_user.launch.wish_line\` | PF-REF-CB/PROPOSED | none | RC-REFERENCE | CT-WISH; short-line slot only. No final line, lore event, reward, Gem custody, or Vaeloryn encounter claim. |
| \`first_user.loading_complete.title\` | PF-SR-CB/PROPOSED | none | RC-LAUNCH | CT-LAUNCH; means required launch readiness is verified and an explicit interaction is available. It is not a fabricated progress value. |
| \`first_user.loading_complete.continue\` | PF-SR-CB/PROPOSED | none | RC-LAUNCH | CT-LAUNCH; one accepted interaction, duplicate is a no-op. |
| \`first_user.loading_complete.readiness_unavailable\` | PF-SR-CB/PROPOSED | none | RC-LAUNCH | CT-LAUNCH; remain before gate and expose only safe retry. No percent or diagnostic interpolation. |

### Realm draft and review

| Key | Class/repo | Variables | Retry/cancel | Continuity / privacy intent |
|---|---|---|---|---|
| \`first_user.realm.title\` | PF-SR-CB/PROPOSED | none | RC-REALM | CT-REALM; selection is draft, not commitment. |
| \`first_user.realm.confirm\` | PF-SR-CB/PROPOSED | none | RC-REALM | CT-REALM; confirms the realm draft only, not server lock. |
| \`first_user.realm.review\` | PF-SR-CB/PROPOSED | none | RC-PRECOMMIT | CT-REALM; render the localized realm name as a separate value component. |
| \`realm.lock.warning\` | PF-SR-CB/EXISTING-DRAFT | none | RC-PRECOMMIT | CT-REALM / CT-COMMIT; required before irreversible submit, account scope and same-realm sub-character consequence explicit. Existing English remains draft. |
| \`first_user.realm.catalog_unavailable\` | PF-SR-CB/PROPOSED | none | RC-REALM | CT-REALM; no selection authority until exact catalog is available. |
| \`first_user.realm.selection_invalid\` | PF-SR-CB/PROPOSED | none | RC-REALM | CT-REALM; reject in place without raw ID or alias hint. |

The four name keys and four existing selection-line keys are classified in the realm map above.

### Origin identity and parent percentages

| Key | Class/repo | Variables | Retry/cancel | Continuity / privacy intent |
|---|---|---|---|---|
| \`first_user.origin.title\` | PF-SR-CB/PROPOSED | none | RC-ORIGIN | CT-ORIGIN |
| \`first_user.origin.percent.title\` | PF-SR-CB/PROPOSED | none | RC-ORIGIN | CT-ORIGIN; Half only. |
| \`first_user.origin.percent.first_parent\` | PF-SR-CB/PROPOSED | none | RC-ORIGIN | CT-ORIGIN; “first” always means canonical ID order, never realm-relative. |
| \`first_user.origin.percent.second_parent\` | PF-SR-CB/PROPOSED | none | RC-ORIGIN | CT-ORIGIN; complement is display-derived. |
| \`first_user.origin.percent.meaning\` | PF-SR-CB/PROPOSED | \`firstParentName\`, \`canonicalFirstParentPercent\`, \`secondParentName\`, \`secondParentPercent\` | RC-ORIGIN | CT-ORIGIN; locale may reorder wording but not pair orientation/value. Owner-visible only; no telemetry. |
| \`first_user.origin.percent.required\` | PF-SR-CB/PROPOSED | none | RC-ORIGIN | CT-ORIGIN; missing Half value rejects; never imply a default. |
| \`first_user.origin.percent.range_error\` | PF-SR-CB/PROPOSED | none | RC-ORIGIN | CT-ORIGIN; valid exact integer range is 30..70; no submitted-value echo. |
| \`first_user.origin.percent.pure_forbidden\` | PF-SR-CB/PROPOSED | none | RC-ORIGIN | CT-ORIGIN; Pure must omit the member. |
| \`first_user.origin.realm_ineligible\` | PF-SR-CB/PROPOSED | none | RC-ORIGIN | CT-ORIGIN; no raw cell/ID display. |
| \`first_user.origin.invalid\` | PF-SR-CB/PROPOSED | none | RC-ORIGIN | CT-ORIGIN; no normalization, alias suggestion, or LegacyRaceUnspecified fallback. |

The ten label keys are classified in the origin map above.

### Body presentation

| Key | Class/repo | Variables | Retry/cancel | Continuity / privacy intent |
|---|---|---|---|---|
| \`first_user.presentation.title\` | PF-SR-CB/PROPOSED | none | RC-PRESENTATION | CT-PRESENTATION; body-presentation scope only. |
| \`first_user.presentation.male\` | PF-SR-CB/PROPOSED | none | RC-PRESENTATION | CT-PRESENTATION; no voice/pronoun/appearance inference. |
| \`first_user.presentation.female\` | PF-SR-CB/PROPOSED | none | RC-PRESENTATION | CT-PRESENTATION; no voice/pronoun/appearance inference. |

### Public handle / username account scope

“Username” on this journey means the \`requestedPublicHandle\` account-facing draft. It is not a login credential, \`AccountId\`, \`ProfileId\`, \`CharacterId\`, or character name. Exact normalization, case, rename, privacy, and uniqueness policy remain unapproved.

| Key | Class/repo | Variables | Retry/cancel | Continuity / privacy intent |
|---|---|---|---|---|
| \`first_user.handle.title\` | PF-SR-CB/PROPOSED | none | RC-HANDLE | CT-HANDLE |
| \`first_user.handle.account_scope\` | PF-SR-CB/PROPOSED | none | RC-HANDLE | CT-HANDLE; required before confirmation. |
| \`first_user.handle.review\` | PF-SR-CB/PROPOSED | none | RC-PRECOMMIT | CT-HANDLE; render escaped \`publicHandle\` separately, bidi-isolated. |
| \`first_user.handle.checking\` | PF-SR-CB/PROPOSED | none | RC-HANDLE | CT-HANDLE; availability is not reservation. |
| \`first_user.handle.available\` | PF-SR-CB/PROPOSED | none | RC-HANDLE | CT-HANDLE; must not overclaim reservation/commit. |
| \`first_user.handle.unavailable\` | PF-SR-CB/PROPOSED | none | RC-HANDLE | CT-HANDLE; no competing-handle disclosure. |
| \`first_user.handle.invalid\` | PF-SR-CB/PROPOSED | none | RC-HANDLE | CT-HANDLE; policy-specific details remain blocked. |
| \`first_user.handle.service_unavailable\` | PF-SR-CB/PROPOSED | none | RC-HANDLE | CT-HANDLE; preserve other drafts, retry handle authority only. |
| \`first_user.handle.reservation_expired\` | PF-SR-CB/PROPOSED | none | RC-HANDLE | CT-HANDLE; revalidate, never treat old reference as authority. |
| \`first_user.handle.rate_limited\` | PF-SR-CB/PROPOSED | none | RC-HANDLE | CT-HANDLE; no internal timing/limit interpolation until policy exists. |
| \`first_user.handle.confirm\` | PF-SR-CB/PROPOSED | none | RC-HANDLE | CT-HANDLE; confirms handle draft/reservation, not genesis. |

### Commit review, server pending, and reconciliation

| Key | Class/repo | Variables | Retry/cancel | Continuity / privacy intent |
|---|---|---|---|---|
| \`first_user.commit.review\` | PF-SR-CB/PROPOSED | none | RC-PRECOMMIT | CT-COMMIT; selected values render as separate protected components. |
| \`first_user.commit.submit\` | PF-SR-CB/PROPOSED | none | RC-PRECOMMIT | CT-COMMIT; irreversible server command boundary. |
| \`first_user.commit.pending\` | PF-SR-CB/PROPOSED | none | RC-SERVER | CT-COMMIT; no cancellation implication. |
| \`first_user.commit.stop_waiting\` | PF-SR-CB/PROPOSED | none | RC-SERVER | CT-COMMIT; explicitly UI deferral only, never transaction cancellation. |
| \`first_user.commit.recovering\` | PF-SR-CB/PROPOSED | none | RC-SERVER | CT-COMMIT; same exact operation binding; no technical-key interpolation. |
| \`first_user.commit.retry_reconcile\` | PF-SR-CB/PROPOSED | none | RC-SERVER | CT-COMMIT; reconciliation only, never new-key submit. |
| \`first_user.commit.service_unavailable\` | PF-SR-CB/PROPOSED | none | RC-SERVER | CT-COMMIT; authority remains unresolved and fail-closed. |

### Server committed / local projection pending

| Key | Class/repo | Variables | Retry/cancel | Continuity / privacy intent |
|---|---|---|---|---|
| \`first_user.projection.pending\` | PF-SR-CB/PROPOSED | none | RC-PROJECTION | CT-PROJECTION; server character exists, local binding is finishing. Must not imply rollback/loss or expose ProfileId/receipt IDs. |
| \`first_user.projection.retry\` | PF-SR-CB/PROPOSED | none | RC-PROJECTION | CT-PROJECTION; retry local projection only. |
| \`first_user.projection.recovery_required\` | PF-SR-CB/PROPOSED | none | RC-PROJECTION | CT-PROJECTION; server authority is preserved. No destructive reset, recreate, or raw diagnostic claim. |

Machine state mapping:
- \`CommittedProjectionPending\` -> \`first_user.projection.pending\`
- \`RecoveryRequired\` -> \`first_user.projection.recovery_required\`
- Neither state may reuse \`first_user.commit.service_unavailable\` in a way that obscures the already-committed server result.

### Prologue handoff

| Key | Class/repo | Variables | Retry/cancel | Continuity / privacy intent |
|---|---|---|---|---|
| \`first_user.prologue.loading\` | PF-SR-CB/PROPOSED | none | RC-PROLOGUE | CT-NVS; both server receipt and local projection are verified before handoff. |
| \`first_user.prologue.unavailable\` | PF-SR-CB/PROPOSED | none | RC-PROLOGUE | CT-NVS; scene failure does not alter committed identity. |
| \`first_user.prologue.retry\` | PF-SR-CB/PROPOSED | none | RC-PROLOGUE | CT-NVS; scene retry only. |

## Continuity-only machine references

All are \`MO-SR\`, never interpolated or localized through their raw value:

- \`POST_REALM_PROLOGUE\`
- \`OMEN_1\`
- \`SELECT_VALERIUS\`
- \`CH1_REALM_INTRO\`
- \`C1\` as forbidden chapter-on-entry
- \`gem_crownlands_sun\`, \`gem_crownlands_oath\`
- \`gem_stonehold_forge\`, \`gem_stonehold_depth\`
- \`gem_eldergrove_root\`, \`gem_eldergrove_moon\`
- \`gem_umbral_veil\`, \`gem_umbral_ember\`
- \`wishgate_eightfold_concordance\`
- \`wishgate.vaeloryn.name\`

The eight Gem IDs are exactly two per realm/eight total and reference-only here. No first-user key may claim custody, collection, eligibility, reward, or wishgate completion. \`wishgate.vaeloryn.name\` remains a name-key reference, not approval of a final localized display value. It must not be replaced by the four realm-dragon IDs or the guardian aliases Aurelius, Ferrum, Virens, or Nox.

## Minimal-prose and fail-closed rules

1. Each surface uses at most one title, one necessary warning/status, and the required command labels; selected values render as structured rows, not explanatory paragraphs.
2. Realm selection-line keys are optional flavor and never substitute for \`realm.lock.warning\`.
3. Machine IDs never appear as fallback labels.
4. Pure origin never renders percent keys. Half origin always renders canonical first/second meaning before irreversible submit.
5. Presentation labels never add voice/pronoun/appearance explanation.
6. Handle errors do not echo the handle or another user's information.
7. Server reconciliation, local projection, and scene recovery use three distinct semantic surfaces.
8. No appearance reference/digest, final wish line, AL logo role, visual asset, reward, or production route is approved by this packet.

## Acceptance / handoff

- Semantic key/glossary packet: COMPLETE for A1 planning disposition.
- Final localized strings, translation policy, locale inventory, typography/layout, accessibility wording, visual role, identity/handle policy, runtime/save/backend implementation, and player/release approval: NOT GRANTED.
- Repo/files/Git/GitHub/Unity/database/cloud: unchanged.
- A2/PR #369: untouched.
- Shared locks: none.
- Meshy/paid operations: none; zero credits.

Publication records the approved planning semantics only. It does not advance any runtime, copy, accessibility, visual, player, or release gate.