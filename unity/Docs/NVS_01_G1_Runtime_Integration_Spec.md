# NVS-01 G1 Runtime Integration Specification

## 1. Control and activation

- Milestone/task: NVS-01 / G1
- Specification version/status: `nvs01-g1-2026-07-22-v001` / draft for user approval
- Primary mode: Codex coordination/review
- Current `main` inspected: `be7304ed6505dae4f557472f1dc480e328404520`
- A1: issue #128, PR #256, merge `be7304ed6505dae4f557472f1dc480e328404520`
- A1 packet: `omen1-a1-2026-07-22-v001`
- Product decision: issue #138 comment `4966062298`
- Downstream implementation: issue #134

A1 contains exactly one internally valid `OMEN_1` packet. Its eight negative fixtures, Android unit/debug build, repository classification, hygiene, and GitHub Actions #121 passed. No runtime file was mixed into A1. The user approved the A1 source before PR #256 merged. G1 is therefore active.

## 2. Goal, player outcome, and delivery target

Implement the approved packet as one deterministic, persistent Unity quest loop. A player with exactly one committed playable realm can select Valerius, accept or defer the offer, follow dialogue (including the lore branch), deploy into the existing Champion arena with typed quest context, fail and retry without penalty, acquire the Celestial Tear once on success, manually report, and atomically receive 500 Gold, +5 Valerius affinity, quest completion, and the selected realm's Chapter 1 unlock.

The delivery target is the standalone Unity vertical slice. Android may preview the same generated content but is not authoritative and the deferred #135 embedded bridge is out of scope.

Non-goals: full Chapter 1, broad quest tooling, unrelated combat/VFX/UI, general #137 save repair, global localization runtime, bridge work, broad service refactors, or narrative edits.

## 3. Source and D1-D16 traceability

The sole narrative source is `unity/Docs/Narrative/NVS_01/OMEN_1_A1.packet.json`. A deterministic build/editor step may copy or transform it to a runtime JSON artifact, but must preserve the source hash, stable ordering, source version, and every source string/key. Generated output is never hand-edited.

| Decisions | Binding technical consequence |
| --- | --- |
| D1 | `DLG_OMEN_1_ARENA_START` commits dialogue position before emitting one correlated arena request. |
| D2-D3 | Failure records transient `FAILED`, presents failure dialogue, then only explicit Retry returns to `INVESTIGATE_SKY_CASTLE`; it is never terminal. |
| D4-D6 | Tear is committed on arena success; Gold, affinity, completion, and chapter unlock form one report-completion transaction. |
| D7 | Every title, description, objective, speaker, line, choice, artifact, and reward resolves through the packet key inventory. |
| D8 | Marker, deploy action, request, and typed results are new contracts; missing capability is visible and non-mutating. |
| D9 | Abandon is accepted only outside an active encounter, resets to `OFFERED`, and clears active/unearned state without deleting earned effects. |
| D10-D12 | A durable realm is required; all four realms use Valerius and the same quest; completion unlocks that realm's `CH1_REALM_INTRO` and returns to Kingdom. |
| D13-D14 | Success owns the retained Tear; report starts only from a deliberate Valerius interaction. |
| D15 | Selecting Valerius reveals/offers; acceptance is explicit and there is one `OFFERED -> TALK_TO_VALERIUS` start. |
| D16 | Exact dialogue node/choice position persists; encounter snapshot resumes when authoritative, otherwise penalty-free Retry; post-success resume retains Tear with report effects pending. |

No row changes narrative meaning.

## 4. Verified current architecture and decisions

| Current path | Verified state | G1 decision |
| --- | --- | --- |
| `IQuestService` / `LocalQuestService` | Numeric Q1-Q5 progress, hard-coded definitions, direct reward/save, and generic `AdvanceStory()` event | Preserve for legacy quests; add an NVS-specific versioned quest runtime behind a narrow interface. Do not overload numeric `QuestState`. |
| `IStoryService` / `LocalStoryService` | Hard-coded fallback dialogue; `AdvanceStory()` does not mutate chapter | Do not use as OMEN_1 authority or completion. Add a packet-backed dialogue/query adapter. |
| `SaveGameData` | Realm, resources, generic quests, reputation, chapter; no NVS state/ledger | Add one backward-compatible NVS progress aggregate only in C3, under an exclusive lock. |
| `ISaveGameService` / `LocalSaveGameService` | Local persistence and partial recovery; #137 remains open | Use its durable save boundary, but #134 cannot claim full milestone acceptance until required #137 failure semantics are satisfied or a focused prerequisite closes the exact risks. |
| Realm selection | Scene calls `CreateNewSave(realm)` then Kingdom; #173 remains open | Require a non-`None`, validated committed realm. Do not invent a second realm field. #173 is an implementation prerequisite for release acceptance. |
| Champion arena | Existing scene, free entry, retry, clear/defeat UI, direct Kingdom loads; no typed quest context/result | Add an adapter/context service; preserve free entry. Do not duplicate the arena. |
| `INotificationService` | String-only messages/errors | Use a typed NVS diagnostic/result internally and map to visible localized text; #177 remains broader follow-up. |
| Android archive/preview | Hard-coded `NVS_01_Packet.kt` and UI references | Historical preview only. It must consume generated read-only data or display a drift error; it never writes quest state. |

The existing `CH0_PROLOGUE`, `C_OMEN`, and generic `C1` are not aliases for `POST_REALM_PROLOGUE` or `CH1_REALM_INTRO`. C3 must validate and persist the approved abstract unlock through the existing realm chapter definitions: Crownlands `C1_CL`, Stonehold `C1_SH`, Eldergrove `C1_EG`, and Umbral `C1_UM`. Missing or mismatched definitions make the report transaction unavailable; G1 does not invent a parallel chapter ID family.

## 5. Runtime catalog contract

Runtime representation: UTF-8 JSON catalog `al.narrative.nvs01`, schema version `1`, content version equal to the A1 packet version, with SHA-256 of the canonical A1 bytes. The build copies it into `StreamingAssets/AL/Narrative` using stable property and array order. Mobile loading must use the supported StreamingAssets API; parse once, retain one immutable catalog, and allocate no per-frame objects.

Required root fields: `schemaVersion`, `packetVersion`, `milestoneId`, `questId`, `approval`, `placement`, `speaker`, `states`, `objectives`, `dialogue`, `transitions`, `externalCapabilities`, `consequences`, `abandonment`, and `localization`. Unknown root or record properties fail closed for version 1. Strings are nonblank; IDs are ordinal/case-sensitive; category IDs are unique; arrays use source order.

Validation must prove:

- approval contains exactly D1-D16 and comment `4966062298`;
- all internal state, objective, dialogue, speaker, action, consequence, and localization references resolve;
- only literal `end` is the reserved dialogue terminal;
- every state is reachable from `OFFERED`; `COMPLETED` alone is terminal and `FAILED` is transient;
- every consequence has one source-declared trigger and every trigger is valid;
- success, failure, cancel, and unavailable result IDs exist as requested external capabilities;
- external dependencies remain `requested` until an implementation adapter proves support;
- eligible realms are exactly the four approved values;
- unsupported schema/content versions, duplicate keys, integer overflow, malformed UTF-8, or hash drift fail visibly without fallback.

Fable/shared contracts are not required for the standalone Unity slice. The encounter request/result records must be plain C# data without `UnityEngine` types so a later bridge can reuse their meaning.

## 6. Error taxonomy

Stable codes use prefix `AL-NVS01-`:

| Code | Trigger | Required behavior |
| --- | --- | --- |
| `CATALOG-MISSING`, `CATALOG-MALFORMED`, `VERSION-UNSUPPORTED`, `HASH-DRIFT` | load failure | Disable OMEN_1 offer, show localized unavailable state, mutate nothing. |
| `ID-DUPLICATE`, `REFERENCE-MISSING`, `TRANSITION-INVALID`, `STATE-UNREACHABLE` | semantic validation | Reject the entire packet; never use legacy fallback dialogue. |
| `DEPENDENCY-UNAVAILABLE` | marker, deploy, arena, artifact, realm unlock, or localization adapter absent | Preserve current state; permit safe return/Retry; grant nothing. |
| `EVENT-DUPLICATE` | already committed request/result/event | Idempotent no-op with diagnostic. |
| `EVENT-MISMATCH` | wrong quest/hook/state/correlation/realm | Reject and retain current state. |
| `SAVE-FAILED`, `COMMIT-UNCERTAIN` | durable boundary fails | Publish no new in-memory state; show recoverable error and reconcile from disk before retry. |
| `PERSISTED-STATE-INVALID` | unknown state/node/objective/version | Quarantine/preserve through save policy, disable mutation, and expose recovery diagnostic. |

Diagnostics must include code, packet version/hash, quest/state/event IDs, and correlation ID, but not player-authored or device-secret data.

## 7. State, objective, and dialogue runtime

| From | Event | To | Durable effect |
| --- | --- | --- | --- |
| `OFFERED` | explicit accept | `TALK_TO_VALERIUS` | active `OBJ_OMEN_1_TALK`, current offer/start node |
| `TALK_TO_VALERIUS` | `REQUEST_SKY_CASTLE_ARENA` after arena-start node | `INVESTIGATE_SKY_CASTLE` | complete talk, activate arena objective, persist request context before scene load |
| `INVESTIGATE_SKY_CASTLE` | failure | `FAILED` | persist result once and open failure node |
| `FAILED` | explicit Retry | `INVESTIGATE_SKY_CASTLE` | new correlation ID; no reward/progress penalty |
| `INVESTIGATE_SKY_CASTLE` | cancel/unavailable | same | clear active request; expose Retry/unavailable |
| `INVESTIGATE_SKY_CASTLE` | success | `REPORT_TO_VALERIUS` | complete arena objective and commit retained Tear once |
| `REPORT_TO_VALERIUS` | select Valerius | same | open report node; no consequence yet |
| `REPORT_TO_VALERIUS` | report conclusion | `COMPLETED` | one atomic report transaction |

Arbitrary missing dialogue targets are errors, never completion. `end` closes only the current conversation. Persist `CurrentDialogueNodeId` before presentation and `PendingChoice=true` until a valid choice commits. Reopening a node is read-only. Dialogue replay never emits a second consequence.

Objectives are event-based, not numeric counters. Persist only current/complete status for the three approved IDs. Offer deferral changes nothing. Abandon outside `EncounterActive` clears dialogue, objectives, request context, failure state, and unearned report transaction, then returns to `OFFERED`; it cannot revoke an already committed Tear or completed effects.

## 8. Encounter request/result contract

`NvsEncounterRequest` requires: contract version, request/correlation GUID, `OMEN_1`, current state/objective, `HOOK_SKY_CASTLE_ARENA`, `LOCATION_SKY_CASTLE_MARKER`, committed realm ID, expected success/failure/cancel/unavailable event IDs, and return scene `Kingdom`. The quest runtime produces it; a Champion adapter validates and consumes it once.

`NvsEncounterResult` requires: contract version, the same correlation ID, quest/hook/realm, outcome enum (`Success`, `Failure`, `Cancelled`, `Unavailable`), matching event ID, and optional snapshot version/reference. The arena adapter produces it; the quest runtime consumes it.

Request context is persisted before scene load and survives scene changes. A duplicate request returns the existing context. A duplicate result returns the prior committed disposition. Late or mismatched results never progress. Free arena entry carries no request and produces no quest result. The arena must publish result intent before returning to Kingdom; the quest service durably commits it before UI advancement.

## 9. Persistence and D16

Add `Nvs01ProgressData` with backward-compatible defaults: version `0` means absent/unoffered; packet version/hash; quest state; active/completed objective IDs; current dialogue node; pending-choice flag; encounter status/correlation/snapshot reference; acquired-artifact IDs; applied-effect keys; selected-realm Chapter 1 unlock ID; and last committed operation ID. Unknown newer versions are read-only/unavailable.

| Interruption | Required resume |
| --- | --- |
| Mid-dialogue | Exact node with choice still pending; no repeated semantic action. |
| Before request | Investigation UI with Deploy available. |
| Request saved, scene not entered | Resume/enter same correlated request or cancel to penalty-free Retry. |
| Arena active | Resume only a verified authoritative snapshot; otherwise clear request and return to Retry. |
| Failure | `FAILED` conversation, then explicit Retry. |
| Success before report | `REPORT_TO_VALERIUS`, Tear owned once; Gold/affinity/completion/unlock absent. |
| During report | Exact report node/choice; report transaction absent until conclusion. |
| Partial report commit | Reconcile operation ledger and domain values, completing missing effects or rolling back unpublished changes deterministically. |
| Completed | Stable completion and all five effect keys; no replay. |
| Invalid/forward state | Read-only unavailable; preserve bytes/evidence and grant nothing. |

All mutations use clone -> validate -> apply to clone -> durable save/verify -> publish. Failure leaves the previous published state. No valid profile may require deletion/reset.

## 10. Consequence transaction

Arena success operation key: `OMEN_1:ARENA_SUCCESS:<correlation>` commits `ARTIFACT_CELESTIAL_TEAR` and `ACQUIRE_CELESTIAL_TEAR` once.

Report operation key: `OMEN_1:REPORT_COMPLETE:v1` prepares and validates all of:

1. Gold balance +500;
2. `NPC_VALERIUS` affinity +5;
3. `OMEN_1` completed;
4. selected-realm Chapter 1 intro unlocked;
5. return destination Kingdom recorded.

The operation and its per-effect keys are written in the same candidate as domain values. Publish and notify only after verified save. On reload, values and ledger are reconciled from the operation record; duplicate dialogue, request, result, notification, or retry is a no-op. Tear is presented but never transferred or consumed. Missing artifact, resource, affinity, or unlock definitions fail preparation before mutation.

## 11. File plan and locks

C1 required: runtime catalog copy/import validator; immutable packet models; loader; focused EditMode tests. C2 required: NVS quest runtime, dialogue/objective controller, encounter records/adapter, Kingdom marker/Valerius/Deploy integration, arena result adapter, tests. C3 required: persisted NVS aggregate, transaction coordinator, migrations/defaults, fault tests. C4 required: diagnostics, UI unavailable/retry states, integration/PlayMode/player evidence, Android preview drift handling.

Potential shared edits:

- `SaveGameData.cs`: required only in C3 for the aggregate; lock before edit, default-safe field, release after merge.
- `Bootloader.cs`: avoid by registering through an existing non-shared composition seam; if proven impossible, a separately declared C1 lock and minimal registration only.
- `LocalGameDataService.cs`: prohibited; packet/catalog owns OMEN_1 definitions.
- `ProjectInitializer.cs`: prohibited unless a separately reviewed deterministic asset-generation requirement proves unavoidable.

A1 packet and completion report are read-only inputs during engineering. Prohibited: narrative text/IDs, archived Kotlin packet as authority, unrelated Q1-Q5 redesign, broad Chapter 1, #135 bridge, terrestrial assets, and unrelated save/economy/combat refactors.

## 12. Test and fault matrix

C1: valid catalog; missing/malformed/unsupported/hash drift; duplicate/blank IDs; missing speaker/dialogue/objective/state/localization; invalid terminal; unreachable state; unknown hook/location/result; invalid consequence/artifact; deterministic output.

C2: offer defer/accept; lore branch; arena-start action; free arena isolation; success/failure/cancel/unavailable; explicit Retry; abandonment allowed/denied during encounter; manual report; every invalid, duplicate, late, or mismatched request/result.

C3: old save default; every D16 row; forward version; duplicate result/dialogue/report/retry; fault before and after each Tear/report transaction boundary; save failure; final verification failure; reload reconciliation; no duplicated Gold, affinity, Tear, completion, or unlock.

C4: Unity batch compile, focused/full EditMode, profile-safe PlayMode scene round trip, representative scene smoke, Player build smoke when #150 permits, Kingdom/free Champion regressions, Android unit/debug build, catalog drift check, repository policy/hygiene, and clean final diff. Every test records setup, action, saved/reloaded state, expected side effects, command, result XML/log, and unavailable checks.

## 13. Delivery order, rollback, and gates

Use dependency-ordered focused PRs where shared save work would obscure contract/state review:

1. C1 contract/loading/validation, no shared files.
2. C2 state/dialogue/handoff against in-memory test storage, no save claims.
3. C3 persistence/transaction with the `SaveGameData.cs` lock and #137/#173 prerequisite disposition.
4. C4 integration/evidence and lock release.

No parallel implementation of the same completion is allowed. A disabled feature leaves profiles readable and OMEN_1 visibly unavailable. Code/catalog rollback preserves unknown NVS data; older code ignores the default-safe aggregate. Partially committed operations reconcile from durable operation records. Newer packet/save versions remain read-only.

G2 must verify current-main implementation, locks, evidence, save safety, and no narrative drift. A2 must compare dialogue, choices, order, artifact meaning, and outcomes to A1. U1 user playtest must cover offer, both dialogue paths, handoff, failure/Retry, success, manual report, completion, save/reload, and no duplicated effects.

## 14. Definition of done and unresolved blockers

G1 is implementation-ready when this specification is approved. Engineering #134 must not claim full completion until:

- #137 supplies the required commit-certainty/offline-progress safety used by C3, or C3 proves a narrower equivalent without weakening #137;
- #173 supplies one durable validated realm per profile;
- the required scene/test gates are available or every unavailable check is explicitly retained as a blocker.

No unresolved narrative or product decision remains. These are technical dependency/evidence blockers, not permission to alter A1.

Codex engineering handoff: implement issue #134 from this approved G1 and A1 merge `be7304ed6505dae4f557472f1dc480e328404520`. Preserve narrative, old saves, service registrations, and free arena entry; declare locks; implement strict validation, deterministic correlated state/events, D16 resume, atomic/idempotent consequences, visible failure, and the complete evidence matrix; return for G2, A2, and U1.
