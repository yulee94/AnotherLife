# OMEN_1 v003 C3A Durable Progression Narrative Fidelity Disposition

**Status date:** 2026-08-06
**Primary Codex mode:** narrative/content
**Source packet:** `omen1-a1-2026-07-29-v003`
**Coordination specification:** `nvs01-g1-2026-07-29-v004`
**Canonical SHA-256:** `8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732`
**Tracking:** issue #134 C3A; merged PR #383
**Reviewed implementation head:** `320fda546d4f12dd1e25452ce9788fa4ef720853`
**Original review baseline:** `main@406b4d9c582113915a8eceaa7bb7a6398a612b2e`
**Post-train publication baseline:** `main@a00a09a1a4050b9fd3fff6254237505321c6bae4`
**C3A disposition:** `PASS WITH EVIDENCE CONCERNS FOR BOUNDED DURABLE PROGRESSION`
**Complete C3/C4, final G2/A2, and U1 disposition:** `NOT REACHED`

## Review Boundary

This review evaluates whether the merged C3A save-backed OMEN_1 progression
slice preserves the accepted v003 narrative identity and continuity. It covers
the backward-compatible save aggregate, strict encode/decode boundary,
candidate-save mutation seam, production Kingdom hydration, and the retained
catalog/runtime/persistence tests that apply to that bounded slice.

The reviewed C3A files were unchanged between PR #383's accepted head
`320fda546d4f12dd1e25452ce9788fa4ef720853` and the original review baseline.
The later integration train is dispositioned separately below because PR #433
changed the shared `LocalSaveGameService` authority boundary without changing
the OMEN_1 packet, generated catalog, codec, presenter, or narrative source.
The source packet and generated runtime catalog remain unchanged and
byte-identical on the post-train publication baseline.

This document does not claim the complete C3 transaction/consequence phase.
In particular, it does not claim an operational Sky Castle/Champion route,
authoritative encounter resumption, Celestial Tear ownership, Gold or affinity
mutation, quest-completion effects, Chapter 1 unlock, partial-commit
reconciliation, PlayMode/Player/device acceptance, final G2/A2, or user U1
approval.

## Post-Train Delta Audit

| Merge | Exact delta relevant to this review | Narrative-fidelity result |
| --- | --- | --- |
| PR #432, merge `147f4895eb7d7b91e8c40dffc10ceb69f55315a2` | Changed only `unity/.gitattributes` and `tools/game-data/migrate_byte_stable_sources.py`. | No OMEN_1 packet, catalog, codec, presenter, save, or narrative-source change. |
| PR #433, merge `f33f5c4dbed709156a608786dc6f27db3f9b579c` | Added dormant live-authority containment and changed `LocalSaveGameService` plus its bounded consumers/tests. | No OMEN_1 packet, catalog, codec, presenter, or narrative-source change. The retained exact-head full EditMode evidence is `1,908/1,908`; this narrative PR does not rerun or broaden that engineering suite. |
| PR #434, merge `a00a09a1a4050b9fd3fff6254237505321c6bae4` | Changed exactly five Android Unity-host lifecycle, test, and bridge-document paths. | No NVS-01 or save-path overlap. A4 did not rerun Unity, so this row relies only on file-scope inspection and its Android validation. |

These non-overlapping merges do not complete C3/C4, prove a production
Champion route, satisfy final G2/A2, or consume the user's U1 approval gate.

## Canonical Narrative Identity

| Contract item | Accepted value |
| --- | --- |
| Narrative source | `unity/Docs/Narrative/NVS_01/OMEN_1_A1.packet.json` |
| Generated runtime catalog | `unity/Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json` |
| Packet version | `omen1-a1-2026-07-29-v003` |
| Canonical byte length | `8,317` |
| Canonical SHA-256 | `8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732` |
| Eligible realm order | `crownlands`, `stonehold`, `eldergrove`, `umbral` |
| State order | `OFFERED`, `TALK_TO_VALERIUS`, `INVESTIGATE_SKY_CASTLE`, `FAILED`, `REPORT_TO_VALERIUS`, `COMPLETED` |
| Objective order | `OBJ_OMEN_1_TALK`, `OBJ_OMEN_1_ARENA`, `OBJ_OMEN_1_REPORT` |
| Dialogue nodes | Eight accepted `DLG_OMEN_1_*` nodes, unchanged |
| Consequences | `ACQUIRE_CELESTIAL_TEAR`, `GRANT_GOLD_500`, `GRANT_VALERIUS_AFFINITY_5`, `COMPLETE_OMEN_1`, `UNLOCK_REALM_CHAPTER_1` |
| Localization | 28 source entries, unchanged |

All ten external capabilities remain `requested`. The production capability
snapshot intentionally mounts only the Kingdom completion destination, so
missing Sky Castle/Champion capabilities remain visible and non-mutating.

## C3A Continuity Results

| Area | Result | Evidence and limit |
| --- | --- | --- |
| Source and runtime identity | Pass | The A1 source and generated catalog are byte-identical at the accepted version, length, hash, realm order, state order, objectives, dialogue, consequences, and localization count. |
| Backward-compatible save shape | Pass for C3A | Version `0` represents neutral absent progress. The versioned aggregate retains packet identity, realm, revision, state, objectives, exact dialogue position, pending action, current/last encounter identity and outcome, last operation, and consequence intents. |
| Production hydration | Pass for C3A | `KingdomSceneController` decodes the persisted aggregate through `Nvs01ProgressCodec.TryDecode` and constructs the runtime with `Nvs01SaveGameMutationCommitter`. It does not use a development factory or duplicate narrative text. |
| Offer and accepted-choice reload | Pass | A production-wiring fixture commits the accepted choice, reloads the save service from disk, recreates the panel, and resumes `TALK_TO_VALERIUS` with the exact authored choices. |
| Fail-closed persisted identity | Pass | Forward progress versions and inconsistent blank-state, inactive-encounter, or inactive-operation aggregates expose read-only/unavailable evidence and no actions. Exact v002 identities and invalid realm/correlation state remain rejected by the retained runtime matrix. |
| Dialogue/state/realm meaning | Pass | C3A changes no approved text, choice, state order, objective meaning, realm eligibility, Retry meaning, abandonment rule, or consequence ordering. |
| Consequence intent preservation | Pass for C3A only | Runtime success/report paths retain the five catalog-backed intent identities in approved order and remain duplicate-safe in memory. Durable application is not claimed. |
| Durable consequence state | Correctly unavailable in C3A | Encode writes empty acquired-artifact and applied-effect collections plus an empty chapter ID. Decode rejects any non-empty values as `unsupported consequence state`; this prevents C3A from fabricating C4 authority. |

## D16 Resume Evidence Matrix

| Interruption | C3A disposition | Remaining evidence or implementation need |
| --- | --- | --- |
| Neutral/old save | Pass | Neutral defaults, historical-field normalization, candidate selection, and reload preservation are covered. |
| Offer deferred or accepted | Pass for runtime; accepted path passes production disk reload | The exact accepted transition is rehydrated from disk. A separate production disk fixture for deferral is not retained. |
| Mid-dialogue | Evidence concern | Exact node and pending-choice fields exist and runtime dialogue tests pass, but no production disk round-trip fixture covers every dialogue node/choice. |
| Before encounter request | Evidence concern | Runtime state and Deploy semantics pass; no production disk round-trip fixture pins this interruption. |
| Request saved before scene entry | Evidence concern | The save model and codec carry the full correlated request shape, but the production arena route is unavailable and no disk resume/cancel fixture proves this row. |
| Arena active | Blocked beyond C3A | Resume requires a verified authoritative Champion snapshot. The production capability remains unavailable. |
| Failure and explicit Retry | Evidence concern | The in-memory state/outcome/Retry matrix passes; a production disk failure/retry round trip is not retained. |
| Success before report | Blocked beyond C3A | Runtime reaches `REPORT_TO_VALERIUS` and emits the Tear intent, but C3A deliberately does not persist or grant Tear ownership. |
| During report | Evidence concern | Runtime preserves manual-report sequencing; no production disk fixture covers every report node/choice. |
| Partial report commit | Blocked beyond C3A | No C4 domain transaction, effect ledger, or deterministic reconcile/rollback path is implemented here. |
| Completed | Blocked beyond C3A | Tear plus the four report effects and selected-realm Chapter 1 unlock are not durably applied by C3A. |
| Abandon/reaccept | Evidence concern | In-memory tests preserve earned tombstones and reject abandonment during an active encounter; the production disk round trip is absent. |
| Invalid/forward state | Pass | Current tests prove read-only/unavailable handling without actions or silent repair. |

The G1 file plan names a dedicated `Nvs01PersistenceTests.cs` matrix for every
D16 row and fault boundary. No such focused fixture exists on the reviewed
baseline. Generic save regression tests strongly cover candidate identity,
atomic publication, recovery evidence, forward data, and JSON round trips,
but they do not substitute for the missing OMEN_1-specific production
disk-reload matrix. This is the principal evidence concern preventing a
complete C3 disposition.

## Narrative Fidelity Findings

- No source, localization, realm, state, objective, dialogue, consequence, or
  abandonment drift was found.
- The current save aggregate retains enough typed identity for the bounded
  quest progression represented by C3A; it does not silently infer gameplay
  or consequence authority.
- The fail-closed consequence boundary is narratively important: a saved
  consequence intent is not represented as an acquired Tear, paid reward,
  changed relationship, completed quest, or unlocked chapter.
- `REPORT_TO_VALERIUS` therefore remains a pending narrative state, not proof
  that the report transaction happened.
- Missing Champion/Sky Castle production capability is presented as
  unavailable rather than bypassed. Issue #180 and its reviewed integration
  sequence still own the authoritative encounter lifecycle needed by OMEN_1.
- C4 must extend persistence transactionally and idempotently; it must not
  weaken or normalize away C3A's rejection of unsupported consequence state.
- Before any post-migration profile may publish `Writable` or C4 activates,
  `Nvs01SaveGameMutationCommitter` plans and exact replay must bind the same
  `ProfileId`, `AuthorityEpoch` (the save-service epoch), and expected
  generation fingerprint. This is the existing prerequisite in
  `Save_Profile_Identity_And_Write_Authority_Spec.md` section 8.6 and ordered
  implementation section 12, item 3; domain operation and payload fingerprints
  do not replace save authority. Schema-v1 profiles remain
  `MigrationRequired` until the witnessed migration and current-mutator cutover
  are accepted.

## Validation

Fresh static evidence on post-train publication baseline
`main@a00a09a1a4050b9fd3fff6254237505321c6bae4`:

- canonical source and runtime catalog: `8,317` bytes each, byte-identical, with
  SHA-256
  `8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732`;
- `tools/narrative/Test-Omen1A1Packet.ps1`: accepted the canonical packet and
  rejected all `11/11` negative fixtures;
- post-train merge inspection: #432, #433, and #434 match the bounded deltas
  recorded above and leave the canonical packet/catalog identity unchanged;
- PR scope: one narrative document, zero runtime/save/test/asset/shared-lock
  paths; classification, repository hygiene, and `git diff --check` pass.

The historical Unity evidence below was run on the original review baseline
`main@406b4d9c582113915a8eceaa7bb7a6398a612b2e` using Unity `2022.3.62f3`:

- static source/runtime identity and semantic assertions: pass;
- canonical source/runtime bytes: `8,317` each and byte-identical;
- canonical SHA-256:
  `8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732`;
- asserted inventory: 4 realms, 6 states, 3 objectives, 8 dialogue
  nodes, 5 consequences, 28 localization entries, and 26 C3A save fields;
- reviewed C3A paths: unchanged from PR #383 head
  `320fda546d4f12dd1e25452ce9788fa4ef720853` through the reviewed baseline;
- focused EditMode filter:
  `AL.Tests.EditMode.Narrative`,
  `SaveSemanticCandidateValidationTests`,
  `SavePersistenceRegressionTests`, and
  `SaveCandidateInventoryIntegrationTests`;
- result: `281/281` passed, `0` failed, skipped, or inconclusive in
  `12.5513788` seconds;
- fixture breakdown: catalog `21/21`, presenter `6/6`, production Kingdom
  wiring `10/10`, quest runtime `35/35`, candidate inventory `14/14`, save
  persistence regression `114/114`, and semantic candidate validation
  `81/81`.

The separate retained A6 exact-head full EditMode evidence for PR #433 is
`1,908/1,908`. PR #434 changed only Android lifecycle/test/documentation paths
and did not rerun Unity. Neither retained result is relabeled as fresh A3 Unity
evidence.

Not run or not available for this narrative documentation review: a new full
EditMode suite, PlayMode, Player build, installed package, authoritative
Champion scene round trip, every D16 production disk-reload row, consequence
fault injection/reconciliation, runtime performance/memory/GC profiling,
physical mobile or low-end device testing, accessibility presentation review,
integrated player playtest, and U1.

## Decision and Handoff

C3A passes narrative fidelity for its bounded durable-progression scope with
the evidence concerns listed above. It preserves the accepted v003 identity,
rehydrates the accepted-choice production path, rejects incompatible or
inconsistent saved state, and does not overclaim or prematurely apply C4
consequences.

The post-train audit does not authorize `Writable` publication or C4
activation. Both remain blocked until NVS plans and exact replay bind the same
profile, authority epoch, and expected generation fingerprint through the
accepted save-authority boundary; schema-v1 remains `MigrationRequired`.

This disposition does not complete C3. Engineering must next provide the
missing OMEN_1-specific D16 disk-reload/fault matrix and the ordered
Champion/consequence integration prerequisites. After C4 supplies the
authoritative encounter route and atomic Tear/report transaction, narrative
mode must re-review the four integrated paths: failure/Retry, success with
retained Tear, manual report completion, and selected-realm Chapter 1 unlock.
Coordination/review must then assemble final G2 evidence. The user retains U1
playtest, creative, balance, milestone, and release approval.

## Impact

This disposition adds documentation only. It changes no narrative source,
runtime catalog, contract, save, service, scene, test, asset, dependency,
workflow, performance behavior, memory use, package/install size, device
compatibility, Android surface, terrestrial source, Champion source, or shared
file.
