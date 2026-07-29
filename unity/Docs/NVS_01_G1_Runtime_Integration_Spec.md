# NVS-01 G1 Runtime Integration Specification

## 1. Control and activation

- Milestone/task: NVS-01 / G1
- Specification version/status: `nvs01-g1-2026-07-29-v004` / approved; synchronized to A1 v003; runtime synchronization remains issue #365 step 3
- Primary mode: Codex coordination/review
- Current `main` inspected: `a97d5e5cf0afc0701ff57d6d110ce056632b4eec`
- A1 source chain: issue #128; source PR #256 merge `be7304ed6505dae4f557472f1dc480e328404520`; authority-correction PR #258 merge `cc3a331b9dcd655c95ed38ff6fbdd79e3f585e8e`; realm-identity issue #365 / source PR #367 merge `ca5888afbd22c2b5a626174cd0194ce18db72260`
- A1 packet: `omen1-a1-2026-07-29-v003`; committed canonical UTF-8/LF/no-BOM bytes: 8,317; SHA-256 `8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732`
- Product decision: issue #138 comment `4966062298`
- Downstream implementation: issue #134

A1 contains exactly one internally valid `OMEN_1` v003 packet. PR #367 changed only `packetVersion` and `placement.eligibleRealmIds`, preserving all player-facing text, quest meaning, ordering, and the 8,317-byte size; semantic no-drift proof, the packet validator, eleven structural negative fixtures, focused identity negatives, repository gates, and hosted run `30419923467` passed. No runtime file was mixed into A1. G1 v004 is approved as the binding coordination contract. It retains the v003 runtime requirements and adds only the bounded Android debug-preview identity exception in section 4 because that source set consumes the same generated Unity catalog and its regression gate cannot remain pinned to v002 while validating v003.

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
| D7 | Every title, description, objective, speaker, line, choice, artifact, and reward resolves through the packet key inventory; localization entries are the sole player-facing source-text authority. |
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
| `IGameDataService` / `LocalGameDataService` chapter setup | `InitializeStoryData()` creates `ChapterDefinition` objects, but `AddChapter()` discards them and the interface exposes no chapter lookup | C3 adds a realm-tagged immutable chapter dictionary and lookup; no new OMEN_1 content is seeded there. |
| Android archive/preview | The debug source set packages `Assets/StreamingAssets/AL/Narrative`; its read-only parser and regression tests pin the catalog version/hash, while release UI remains unavailable. | Issue #365 step 3 may atomically synchronize only the debug parser's expected packet version/hash, its focused tests, and the preview content catalog's two source-version references when the Unity catalog identity changes. No copy, UI, route, action, progress, save, or runtime behavior may change. #186 owns every broader preview-consumer or source-fidelity change; Android never writes authoritative quest state. |

The existing `CH0_PROLOGUE`, `C_OMEN`, and generic `C1` are not aliases for `POST_REALM_PROLOGUE` or `CH1_REALM_INTRO`. C3 must validate and persist the approved abstract unlock through the existing realm chapter definitions: Crownlands `C1_CL`, Stonehold `C1_SH`, Eldergrove `C1_EG`, and Umbral `C1_UM`. Missing or mismatched definitions make the report transaction unavailable; G1 does not invent a parallel chapter ID family.

The step 3 Android exception is limited to these paths:

- `app/src/debug/java/com/example/anotherlife/data/contracts/Nvs01PreviewContracts.kt`
- `app/src/test/java/com/example/anotherlife/data/contracts/Nvs01PreviewParserTest.kt`
- `app/src/test/java/com/example/anotherlife/data/contracts/QuestPreviewContentParserTest.kt`
- `unity/Assets/AL/StreamingAssets/GameData/al_quest_preview_content_catalog.json`

This exception exists because `app/build.gradle.kts` supplies the generated Unity NVS catalog directly to the Android debug asset set and `Nvs01PreviewParserTest` reads that tracked file. Leaving the parser at v002 after the catalog becomes v003 makes the required Android regression gate fail rather than producing a separately deployable stale preview. The exception does not make Android authoritative, enable the release route, or supersede #186.

## 5. Runtime catalog contract

Runtime representation: UTF-8 JSON catalog `al.narrative.nvs01`, schema version `1`, exact supported content version `omen1-a1-2026-07-29-v003`, with SHA-256 of the canonical A1 bytes. Canonical bytes are the committed UTF-8, LF, no-BOM packet bytes, including its final LF. C1 must apply the repository's existing `unity-json` LF attribute to both `Docs/Narrative/NVS_01/*.packet.json` and `Assets/StreamingAssets/AL/Narrative/*.catalog.json`. The exporter reads strict UTF-8 bytes, rejects a BOM or bare CR, deterministically converts any checkout-provided CRLF pairs to LF, requires the final LF and exact canonical hash, then writes those canonical bytes without JSON reserialization. The tracked artifact and bytes presented to the Player build must be byte-for-byte identical to the 8,317-byte committed blob; drift verification runs after checkout and immediately before packaging. Mobile loading must use the supported StreamingAssets API; parse once, retain one immutable catalog, and allocate no per-frame objects.

Required root fields: `schemaVersion`, `packetVersion`, `milestoneId`, `questId`, `titleKey`, `descriptionKey`, `approval`, `placement`, `speaker`, `states`, `objectives`, `dialogue`, `transitions`, `externalCapabilities`, `consequences`, `abandonment`, and `localization`. Unknown root or record properties fail closed for version 1. Schema-v1 objective records contain exactly `id`, `textKey`, `activatesIn`, and `completesOn`; `sourceText` and every other inline player-facing text property are prohibited and reject the catalog. Strings are nonblank; IDs and property names are ordinal/case-sensitive; category IDs are unique; arrays use source order. `titleKey` and `descriptionKey` must resolve in `localization`, just like every other player-facing key.

Validation must prove:

- approval contains exactly D1-D16 and comment `4966062298`;
- all internal state, objective, dialogue, speaker, action, consequence, and localization references resolve;
- only literal `end` is the reserved dialogue terminal;
- every state is reachable from `OFFERED`; `COMPLETED` alone is terminal and `FAILED` is transient;
- every consequence has one source-declared trigger and every trigger is valid;
- success, failure, cancel, and unavailable result IDs exist as requested external capabilities;
- external dependencies remain `requested` until an implementation adapter proves support;
- eligible realms are exactly, ordinally, and in source order: `crownlands`, `stonehold`, `eldergrove`, `umbral`; case-folding, culture normalization, aliases, reordering, missing values, or extras are prohibited;
- unsupported schema/content versions, duplicate keys, integer overflow, malformed UTF-8, or hash drift fail visibly without fallback.

### 5.1 v002 compatibility and canonical realm identity

The accepted v003 realm sequence is exactly `crownlands`, `stonehold`, `eldergrove`, `umbral`. It must be supplied by a `CommittedValid` #173 realm identity and compared ordinally. Runtime code must not lowercase, case-fold, culture-normalize, or map an alternate spelling.

The v002 packet identity `omen1-a1-2026-07-22-v002` / `b22c166310617657cf9716f988e697d4c4992b4d1877b6fd4d0a3311af9a9a1f` and its uppercase `CROWNLANDS`, `STONEHOLD`, `ELDERGROVE`, `UMBRAL` values are development-only legacy evidence, not v003 aliases:

1. A v002 packet, catalog, hash, snapshot, request, result, or receipt presented to the v003 contract is unsupported/invalid, mutates nothing, and grants no reward, chapter unlock, encounter result, or completion.
2. Historical v002 review and fidelity documents remain immutable evidence; they are not rewritten to claim v003 validation.
3. There is no accepted production save integration or production route known to contain a durable v002 OMEN_1 aggregate.
4. If durable v002 profile evidence is later discovered, #137 preserves it read-only and a separately reviewed explicit migration is required; casing alone never authorizes inferred migration.
5. The generated/runtime v002 catalog remains visibly unavailable until issue #365 step 3 regenerates and validates the exact v003 bytes.

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
| `OFFERED` | explicit accept | `TALK_TO_VALERIUS` | complete already-active `OBJ_OMEN_1_TALK`, persist current start node and pending choice |
| `TALK_TO_VALERIUS` | `REQUEST_SKY_CASTLE_ARENA` after arena-start node | `INVESTIGATE_SKY_CASTLE` | activate arena objective and persist request context before scene load; talk was already completed on acceptance |
| `INVESTIGATE_SKY_CASTLE` | failure | `FAILED` | persist result once and open failure node |
| `FAILED` | explicit Retry | `INVESTIGATE_SKY_CASTLE` | new correlation ID; no reward/progress penalty |
| `INVESTIGATE_SKY_CASTLE` | cancel/unavailable | same | clear active request; expose Retry/unavailable |
| `INVESTIGATE_SKY_CASTLE` | success | `REPORT_TO_VALERIUS` | complete arena objective and commit retained Tear once |
| `REPORT_TO_VALERIUS` | select Valerius | same | open report node; no consequence yet |
| `REPORT_TO_VALERIUS` | report conclusion | `COMPLETED` | one atomic report transaction |

Arbitrary missing dialogue targets are errors, never completion. `end` closes only the current conversation. Persist `CurrentDialogueNodeId` before presentation and `PendingChoice=true` until a valid choice commits. Reopening a node is read-only. Dialogue replay never emits a second consequence.

Objectives are event-based, not numeric counters. `OBJ_OMEN_1_TALK` is active while the quest is `OFFERED` and completes only on explicit acceptance, exactly as A1 declares; acceptance then enters `TALK_TO_VALERIUS`. Persist only current/complete status for the three approved IDs. Offer deferral changes nothing. Abandon outside `EncounterActive` clears dialogue, objectives, request context, failure state, and unearned report transaction, then returns to `OFFERED` with `OBJ_OMEN_1_TALK` active again; it cannot revoke an already committed Tear or completed effects.

## 8. Encounter request/result contract

`NvsEncounterRequest` requires: contract version, request/correlation GUID, `OMEN_1`, current state/objective, `HOOK_SKY_CASTLE_ARENA`, `LOCATION_SKY_CASTLE_MARKER`, committed realm ID emitted ordinally as exactly one of `crownlands`, `stonehold`, `eldergrove`, or `umbral`, expected success/failure/cancel/unavailable event IDs, and return scene `Kingdom`. The quest runtime produces it; a Champion adapter validates and consumes it once.

`NvsEncounterResult` requires: contract version, the same correlation ID, quest/hook/realm, outcome enum (`Success`, `Failure`, `Cancelled`, `Unavailable`), matching event ID, and optional snapshot version/reference. The arena adapter produces it; the quest runtime consumes it.

The committed-realm adapter consumes only a `CommittedValid` #173 identity. `None`, undefined, unavailable, uncommitted, uppercase, mixed-case, unknown, wrong-realm, stale-version, and hash-mismatched input is rejected visibly and non-mutatingly; free arena entry remains isolated.

Request context is persisted before scene load and survives scene changes. A duplicate request returns the existing context. A duplicate result returns the prior committed disposition. Late or mismatched results never progress. Free arena entry carries no request and produces no quest result. The arena must publish result intent before returning to Kingdom; the quest service durably commits it before UI advancement.

Every non-arena command/event also has a typed envelope containing contract version, operation ID, quest ID, expected current state, expected quest revision, actor/context ID, and timestamp used only for diagnostics. Each successful mutation increments a checked monotonic `QuestRevision`. An event whose expected revision is lower or higher than the persisted revision is stale or premature and cannot mutate even if abandon/reaccept later returns to the same named state. The current revision's operation ID and typed disposition are persisted before publishing UI changes.

| Event | Producer -> consumer | Required payload | Duplicate / invalid / failure behavior |
| --- | --- | --- | --- |
| `SELECT_VALERIUS` | Kingdom presenter -> NVS runtime | operation ID, `NPC_VALERIUS`, committed realm, interaction kind (`Offer` or `Report`) | Same operation reopens the already committed node without mutation; wrong NPC/realm/state is rejected visibly. Missing catalog leaves the interaction unavailable. |
| `QUEST_ACCEPTED` | offer-dialogue choice -> NVS runtime | operation ID, `OMEN_1`, `DLG_OMEN_1_OFFER`, choice key `choice.omen1.accept` | Duplicate returns the existing `TALK_TO_VALERIUS` disposition; wrong node/choice/state changes nothing. Save failure leaves `OFFERED` and its talk objective active. |
| `DIALOGUE_CHOICE_SELECTED` | dialogue presenter -> NVS runtime | operation ID, current node, choice key, target node or semantic action | Node and pending-choice must match persisted state. Duplicate is read-only; missing target/action rejects the packet/event. Persist next node before presenting it. |
| `REQUEST_SKY_CASTLE_ARENA` | arena-start semantic action -> NVS runtime -> Champion adapter | dialogue operation ID plus the full `NvsEncounterRequest` | Duplicate returns the same request/correlation. Invalid state/context or failed request commit does not load the scene. |
| `RETRY_SKY_CASTLE_ARENA` | failure dialogue choice -> NVS runtime | operation ID, failure result ID, prior correlation, retry action ID | Duplicate returns the same retry disposition. A new correlation is created only after the retry transition saves. Invalid state remains `FAILED`. |
| `ABANDON_OMEN_1` | quest UI -> NVS runtime | operation ID, quest ID, expected state, encounter-active assertion | Duplicate is a no-op. Active/pending encounter rejects abandonment and offers recovery/cancel. Save failure preserves the pre-abandon state. |
| `DLG_OMEN_1_REPORT_CONCLUSION` | report-dialogue choice -> transaction coordinator | operation ID, node/choice, quest/state, realm, packet version/hash | Duplicate returns committed completion. Any mismatch or preparation/save/verification failure grants nothing and leaves report resumable. |

All rejected events emit a stable `AL-NVS01-*` diagnostic and a localized player-visible result. Event timestamps never determine ordering or idempotency; persisted operation IDs and expected-state comparison do.

## 9. Persistence and D16

Add `Nvs01ProgressData` with backward-compatible defaults: version `0` means absent/unoffered; packet version/hash; checked monotonic quest revision; quest state; active/completed objective IDs; current dialogue node; pending-choice flag; encounter status/current-and-last correlation plus committed outcome; acquired-artifact IDs; the fixed Tear/report applied-effect keys; selected-realm Chapter 1 unlock ID; and the current revision's operation ID/disposition. Unknown newer versions, revision overflow, or inconsistent revision/disposition are read-only/unavailable.

Under v003, any persisted v002 packet/hash/realm-bearing aggregate or correlated request/result is read-only and unavailable. It is preserved as evidence, never normalized or rewarded, unless a separately reviewed #137 migration is opened after real durable v002 profile evidence is found.

Replay protection is bounded: it stores no command history. Expected revision rejects every older/later transition command; current/last encounter correlation and outcome reject late request/result replay; fixed effect keys reject Tear/report replay; and the current revision's operation disposition answers an immediate duplicate deterministically. Abandonment and reacceptance each increment revision, so an old acceptance/dialogue/retry command cannot become valid merely because the same state name appears again.

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
| Completed | Stable completion with the Tear key plus all four report-effect keys; no replay. |
| Invalid/forward state | Read-only unavailable; preserve bytes/evidence and grant nothing. |

`CurrentChapterId` remains the canonical saved current/unlocked chapter marker used by the existing story surface. C3 must expose immutable chapter lookup by adding `GetChapter(string id)` and `GetChapters(RealmId realm)` to `IGameDataService`. `LocalGameDataService` must retain the `ChapterDefinition` objects that `AddChapter()` currently creates and discards in an ordinal ID dictionary plus an explicit realm-to-ID index; duplicate IDs or mismatched realm/index entries fail service construction. This is a lookup-only shared-file change under its exclusive lock and must not add OMEN_1 narrative data. The report transaction resolves the selected realm through that catalog, requires the exact `C1_CL`, `C1_SH`, `C1_EG`, or `C1_UM` definition, and writes the resolved ID in the same candidate as the effect ledger.

| Existing `CurrentChapterId` | Compatibility decision before OMEN_1 | Report-completion mutation | Downgrade/rollback behavior |
| --- | --- | --- | --- |
| null/blank | Compatible old/default profile when realm is valid; offer OMEN_1 | Set selected realm's exact C1 ID | Older code reads the ordinary C1 ID. |
| `C1` | Preserve as legacy generic marker; do not treat it as completion | Replace with selected realm's exact C1 ID | No parallel alias remains. |
| `CH0_PROLOGUE` or `C_OMEN` | Preserve as historical marker; NVS progress, not this string, controls offer/resume | Replace with selected realm's exact C1 ID | A code rollback preserves a valid historical/current string. |
| selected realm's exact C1 ID | Compatible already-unlocked profile; do not regress | Leave unchanged and commit the unlock effect key | Idempotent. |
| a catalog-verified later chapter for the selected realm | Compatible advanced profile; never move backward | Leave unchanged and commit the unlock effect key | Idempotent; later progress preserved. |
| another realm's chapter, unknown ID, or forward-only catalog ID | Inconsistent/read-only; OMEN_1 mutation unavailable | None | Preserve exact value and diagnostic evidence. |

Old saves with no `Nvs01ProgressData` default to version 0 and derive no completion or reward from `CurrentChapterId`. A later chapter proves only that chapter progress exists; it does not synthesize Tear, Gold, affinity, dialogue, or quest-completion history. Forward chapter/catalog versions are preserved read-only. Tests cover every row, all four realms, a later chapter, cross-realm mismatch, missing definition, and rollback to code that ignores the NVS aggregate.

All mutations use clone -> validate -> apply to clone -> durable save/verify -> publish. Failure leaves the previous published state. No valid profile may require deletion/reset.

## 10. Consequence transaction

Arena success operation key: `OMEN_1:ARENA_SUCCESS:<correlation>` commits `ARTIFACT_CELESTIAL_TEAR` and `ACQUIRE_CELESTIAL_TEAR` once.

Report operation key: `OMEN_1:REPORT_COMPLETE:v1` prepares and validates all of:

1. Gold balance +500;
2. `NPC_VALERIUS` affinity +5;
3. `OMEN_1` completed;
4. selected-realm Chapter 1 intro unlocked.

Returning to Kingdom command view is post-commit navigation, not a fifth report consequence. It occurs only after the verified transaction and may be retried safely without mutating progression.

The operation and its per-effect keys are written in the same candidate as domain values. C3 introduces an internal `ISaveGameCandidateStore.TryCommitCandidate` seam implemented by `LocalSaveGameService`: it deep-clones the normalized current save, passes the detached candidate to one mutation callback, validates it, executes the existing temp/write/install/final-verification algorithm, and replaces `CurrentSave` only after verification. Failure returns a typed result and leaves the published object/reference unchanged. The seam accepts exactly one operation ID and rejects an already-committed ID.

`Nvs01PersistenceCoordinator` mutates the detached candidate directly through small pure helpers for wallet, affinity, artifact, quest, chapter, and ledger fields. It must not call `LocalResourceService.AddResource`, `ReputationService.ChangeAffinity`, `IStoryService.AdvanceStory`, or any other domain method during preparation because those services operate on the published save and `ChangeAffinity()` performs a nested `Save()`. After a successful candidate commit, the coordinator publishes read-only `Nvs01EffectsCommitted` notifications; resource/reputation/UI adapters refresh or raise display events without changing data or saving again. A notification failure is logged and retried as presentation only—it cannot roll back or repeat the committed effects.

On reload, candidate values and ledger are reconciled from the operation record; duplicate dialogue, request, result, notification, or retry is a no-op. Tear is presented but never transferred or consumed. Missing artifact, resource, affinity, chapter, or unlock definitions fail preparation before mutation. Tests inject failure before clone, after each pure mutation, during temp write/install/verification, during publish, and during each post-commit notification.

## 11. File plan and locks

Exact proposed paths (a mechanically necessary `.meta` accompanies each new Unity path):

| Stage | Required path | Responsibility |
| --- | --- | --- |
| C1 | `unity/.gitattributes` | Apply the existing `unity-json` LF attribute to authoritative packet and generated runtime-catalog patterns; no broader attribute rewrite. |
| C1 | `unity/Assets/AL/Scripts/Narrative/Nvs01/Contracts/Nvs01CatalogModels.cs` | Plain immutable catalog records; no `UnityEngine` types. |
| C1 | `unity/Assets/AL/Scripts/Narrative/Nvs01/Nvs01CatalogLoader.cs` | One-time StreamingAssets load, hash/version checks, immutable cache. |
| C1 | `unity/Assets/AL/Scripts/Narrative/Nvs01/Nvs01CatalogValidator.cs` | Complete fail-closed semantic validation and stable diagnostics. |
| C1 | `unity/Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json` | Deterministic generated runtime artifact with source version/hash. |
| C1 | `unity/Assets/AL/Editor/Narrative/ExportNvs01Catalog.cs` | Deterministic A1-to-runtime export and drift check; never authors text. |
| C1 | `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01CatalogTests.cs` | Contract, invalid fixture, hash, and ordering matrix. |
| C2 | `unity/Assets/AL/Scripts/Narrative/Nvs01/INvs01QuestRuntime.cs` | Narrow query/command boundary distinct from legacy numeric quests. |
| C2 | `unity/Assets/AL/Scripts/Narrative/Nvs01/Nvs01QuestRuntime.cs` | Deterministic states, objectives, dialogue, abandonment, request/result disposition. |
| C2 | `unity/Assets/AL/Scripts/Narrative/Nvs01/Contracts/NvsEncounterContracts.cs` | Plain request/result/outcome/correlation records. |
| C2 | `unity/Assets/AL/Scripts/UI/Kingdom/Nvs01KingdomPresenter.cs` | Valerius offer/report, marker, Deploy, Retry, and visible unavailable state. |
| C2 | `unity/Assets/AL/Scripts/ChampionMode/Narrative/Nvs01ChampionEncounterAdapter.cs` | Carries context through the existing arena and publishes typed outcomes; preserves free entry. |
| C2 | `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01QuestRuntimeTests.cs` | State/dialogue/objective/request/result/duplicate matrix. |
| C3 | `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs` | One default-safe `Nvs01ProgressData` aggregate; exclusive soft lock required. |
| C3 | `unity/Assets/AL/Scripts/Services/Local/ISaveGameCandidateStore.cs` | Internal typed clone/commit/verify seam; no public gameplay mutation API. |
| C3 | `unity/Assets/AL/Scripts/Services/Local/LocalSaveGameService.cs` | Implements candidate commit using the existing hardened file algorithm; no parallel persistence path. |
| C3 | `unity/Assets/AL/Scripts/Core/Interfaces/IGameDataService.cs` | Adds immutable chapter lookup required for validated unlock/no-regression decisions. |
| C3 | `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs` | Exposes existing chapter definitions only; exclusive soft lock, no OMEN_1 content. |
| C3 | `unity/Assets/AL/Scripts/Narrative/Nvs01/Nvs01PersistenceCoordinator.cs` | Clone/validate/save/verify/publish and transaction-ledger reconciliation. |
| C3 | `unity/Assets/AL/Tests/EditMode/Narrative/Nvs01PersistenceTests.cs` | Old-save, every D16 row, fault boundary, duplicate, and recovery matrix. |
| C4 | `unity/Assets/AL/Tests/PlayMode/Nvs01VerticalSliceTests.cs` | Kingdom/arena/return/report/reload integration without developer-profile access. |

Existing files that C2 may modify narrowly are `unity/Assets/AL/Scripts/UI/Kingdom/KingdomSceneController.cs` and `unity/Assets/AL/Scripts/ChampionMode/ChampionArenaSceneController.cs`; their edits are limited to invoking the new adapters and must preserve all free/demo behavior. `ISaveGameService.cs` remains unchanged: the candidate seam is internal to local persistence and is shared with #137 rather than creating a second save algorithm.

Potential shared edits:

- `SaveGameData.cs`: required only in C3 for the aggregate; lock before edit, default-safe field, release after merge.
- `Bootloader.cs`: avoid by registering through an existing non-shared composition seam; if proven impossible, a separately declared C1 lock and minimal registration only.
- `LocalGameDataService.cs`: C3 lookup-only shared edit described above; acquire its exclusive soft lock together with `SaveGameData.cs`, preserve every existing definition/service behavior, and release both locks when the C3 PR merges. It remains prohibited from defining OMEN_1 content.
- `ProjectInitializer.cs`: prohibited unless a separately reviewed deterministic asset-generation requirement proves unavoidable.

A1 packet and completion report are read-only inputs during engineering. Prohibited: narrative text/IDs; `app/src/main/java/com/example/anotherlife/data/simulation/NVS_01_Packet.kt`; Android preview/UI files owned by #186 except the four identity-only step 3 paths and fields listed in section 4; any new OMEN_1 content in `LocalGameDataService.cs`; `ProjectInitializer.cs` absent a separately approved necessity; unrelated Q1-Q5 redesign; broad Chapter 1; #135 bridge; terrestrial assets; and unrelated save/economy/combat refactors.

## 12. Test and fault matrix

C1: valid v003 catalog; missing/malformed/unsupported/hash drift; duplicate/blank IDs; missing speaker/dialogue/objective/state/localization; unknown or wrong-case objective properties including inline `sourceText`; invalid terminal; unreachable state; unknown hook/location/result; invalid consequence/artifact; deterministic output. The exact ordinal realm sequence `crownlands`, `stonehold`, `eldergrove`, `umbral` is accepted; uppercase, mixed-case, culture-sensitive variants, unknown, blank, duplicate, missing, extra, and reordered values are rejected. v002 version/hash/catalog fixtures fail closed without aliasing. A Windows/`core.autocrlf=true` fixture must prove CRLF checkout input emits the same 8,317 LF bytes and `8bec0bee...` hash, while raw artifact/package verification rejects CRLF, BOM, bare CR, or any byte drift.

C2: offer defer/accept; lore branch; arena-start action; free arena isolation; success/failure/cancel/unavailable; explicit Retry; abandonment allowed/denied during encounter; manual report; all four canonical `CommittedValid` realm identities; rejection of every unavailable/uncommitted/undefined/uppercase/mixed-case/unknown/wrong-realm input; v002 snapshot/request/result rejection; and every invalid, duplicate, late, or mismatched request/result.

C3: old-save default; every chapter compatibility/migration row and all four realms; every D16 row; forward version; duplicate result/dialogue/report/retry; candidate-reference identity before/after failure and success; fault before and after each Tear/report transaction boundary; nested save prohibited; post-commit notification failure; temp/install/final-verification failure; reload reconciliation; no duplicated Gold, affinity, Tear, completion, or unlock.

C4: Unity batch compile, focused/full EditMode, profile-safe PlayMode scene round trip, representative scene smoke, Player build smoke when #150 permits, Kingdom/free Champion regressions, Android unit/debug regression build, catalog drift check, repository policy/hygiene, and clean final diff. Outside the four identity-only step 3 exceptions in section 4, #186—not #134—owns Android preview consumer and source-fidelity changes. Every test records setup, action, saved/reloaded state, expected side effects, command, result XML/log, and unavailable checks.

## 13. Optimization and device budgets

- Canonical/generated OMEN_1 JSON must remain at or below 64 KiB uncompressed and be included once; CI reports source, generated, and compressed build sizes and rejects duplicate copies.
- Catalog plus lookup indexes must retain at most 256 KiB managed memory after load on a representative low-end profile. Load occurs once outside interactive combat; no polling, reflection, file I/O, or managed allocation is permitted per frame.
- Quest, objective, effect, localization, and operation collections are bounded to validated catalog counts. Persisted data contains one OMEN_1 aggregate, at most one active encounter context, the three approved objective IDs, one Tear ID, one Tear-acquisition effect key, and the fixed four report-effect keys; unbounded history/log lists are prohibited.
- Kingdom and arena integration reuse existing scenes, fonts, materials, icons, and UI primitives. #134 adds no texture, mesh, audio, shader, native library, package, network dependency, background thread, or duplicate arena scene without a separately approved change.
- Total compressed Player/install-size delta attributable to #134 must be measured from build reports and stay at or below 128 KiB. A larger delta blocks C4 pending exact attribution and a user-approved exception.
- C4 captures catalog load time and allocations in Editor plus the lowest available target-device tier. Target: <=25 ms one-time parse/validation on the reference desktop and <=100 ms on the available low-end/mobile test tier, with zero recurring Update allocations. If no device runner is available, retain the mobile measurement as an explicit release blocker rather than claiming pass.
- No minimum OS/API, graphics tier, quality setting, or accessibility requirement may increase. Unavailable/retry/report UI remains readable with existing scaling, keyboard/controller navigation, reduced-motion behavior, and color-independent text/status cues.

## 14. Delivery order, rollback, and gates

Use dependency-ordered focused PRs where shared save work would obscure contract/state review:

1. C1 contract/loading/validation, no shared files.
2. C2 state/dialogue/handoff against in-memory test storage, no save claims.
3. C3 persistence/transaction with the `SaveGameData.cs` lock and #137/#173 prerequisite disposition.
4. C4 integration/evidence and lock release.

No parallel implementation of the same completion is allowed. A disabled feature leaves profiles readable and OMEN_1 visibly unavailable. Code/catalog rollback preserves unknown NVS data; older code ignores the default-safe aggregate. Partially committed operations reconcile from durable operation records. Newer packet/save versions remain read-only.

G2 must verify current-main implementation, locks, evidence, save safety, and no narrative drift. A2 must compare dialogue, choices, order, artifact meaning, and outcomes to A1. U1 user playtest must cover offer, both dialogue paths, handoff, failure/Retry, success, manual report, completion, save/reload, and no duplicated effects.

## 15. Definition of done and unresolved blockers

G1 is implementation-ready when this specification is approved. Engineering #134 must not claim full completion until:

- #137 supplies the required commit-certainty/offline-progress safety used by C3, or C3 proves a narrower equivalent without weakening #137;
- #173 supplies one durable validated realm per profile;
- the required scene/test gates are available or every unavailable check is explicitly retained as a blocker.

No unresolved narrative or product decision remains. These are technical dependency/evidence blockers, not permission to alter A1.

Codex engineering handoff: continue issue #134 only after issue #365 step 3 synchronizes runtime to this approved G1, A1 `omen1-a1-2026-07-29-v003`, source-correction PR #367 merge `ca5888afbd22c2b5a626174cd0194ce18db72260`, canonical SHA-256 `8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732`, and exact realm order `crownlands`, `stonehold`, `eldergrove`, `umbral`. Preserve narrative, historical v002 evidence, old saves, service registrations, and free arena entry; reject v002/uppercase identity without aliasing; declare locks; implement strict validation, deterministic correlated state/events, D16 resume, atomic/idempotent consequences, visible failure, and the complete evidence matrix; return for G2, A2, and U1.
