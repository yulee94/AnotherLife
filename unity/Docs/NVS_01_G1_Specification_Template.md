# NVS-01 G1 Runtime Integration Specification Template

Use this template only after the clean A1 narrative packet is complete, Codex coordination/review has verified its ownership and internal consistency, and the user has approved D1–D16 in issue #138.

This is a Codex coordination/review technical specification. It translates approved narrative intent into implementable runtime requirements without rewriting dialogue, characters, choices, lore, reward meaning, chapter placement, failure meaning, or outcomes.

## Document control

```text
Milestone: NVS-01
Task: G1
Specification version:
Specification status: Blocked / Draft / Approved
Codex coordination branch: codex/coordination-nvs-01-integration-spec
Codex coordination commit:
Current main commit inspected:
A1 issue/PR/commit: #128 / / 
A1 packet version:
User decision issue/comment: #138 / 
Risk register commit:
Codex implementation issue: #134
Approved implementation base:
```

## 1. G1 activation gate

G1 may proceed only when all items pass:

- [ ] The A1 branch started from fetched current `main`.
- [ ] A1 contains exactly one bounded `OMEN_1` packet.
- [ ] Issue #138 records explicit D1–D16 user decisions.
- [ ] A1 records D1–D16 exactly and passes its consistency assertions.
- [ ] All internal A1 IDs and references resolve.
- [ ] States, objectives, dialogue, failure, retry, cancellation, report, artifact, consequence, completion, and resume intent are complete.
- [ ] External dependencies are marked requested or verified with evidence.
- [ ] Codex narrative/content supplied its completion report and exact Codex coordination/review handoff request.
- [ ] Codex coordination/review verified narrative ownership and completeness.
- [ ] No runtime-owned implementation is mixed into A1.

If any item is false, keep #133 blocked and list the exact missing upstream evidence. Do not fill narrative gaps in G1.

## 2. Executive technical goal

### Goal

```text
<Describe the exact runtime capability required to implement the approved A1 packet.>
```

### Player-observable definition

```text
<Describe what the player can unlock, start, read, choose, hand off to gameplay, fail, retry, report, receive, complete, save, reload, and resume.>
```

### Delivery target

Choose and justify one:

```text
Standalone Unity vertical slice / Android preview plus standalone Unity / true Android↔Unity embedded path / another explicitly approved target
```

Do not represent Android `UnityView` as a real embedded bridge unless issue #135 is explicitly brought into scope and implemented.

### Non-goals

At minimum address:

- full Chapter 1,
- broad realm/building/world systems,
- global narrative tooling,
- unrelated combat/VFX/UI work,
- general save hardening from #137,
- broad localization runtime,
- Android↔Unity embedding when deferred,
- unrelated refactors.

## 3. Upstream traceability

### Source-of-truth files

| File | Commit | Owner | Narrative responsibility |
| --- | --- | --- | --- |
| | | Codex narrative/content | |

### D1–D16 traceability

Every row must preserve the approved answer.

| Decision | Approved A1 answer | Required technical consequence | Tests/evidence | Narrative unchanged? |
| --- | --- | --- | --- | --- |
| D1 — handoff | | | | yes/no |
| D2 — failure recovery | | | | yes/no |
| D3 — `FAILED` meaning | | | | yes/no |
| D4 — affinity | | | | yes/no |
| D5 — Gold/Tear timing | | | | yes/no |
| D6 — completion timing | | | | yes/no |
| D7 — localization policy | | | | yes/no |
| D8 — hook status | | | | yes/no |
| D9 — cancellation | | | | yes/no |
| D10 — chapter/realm placement | | | | yes/no |
| D11 — Valerius/speaker scope | | | | yes/no |
| D12 — location/access/post-completion | | | | yes/no |
| D13 — Celestial Tear meaning | | | | yes/no |
| D14 — report interaction | | | | yes/no |
| D15 — quest-start trigger | | | | yes/no |
| D16 — resume/interruption | | | | yes/no |

Any `no` blocks implementation and returns the requirement to A1/user approval.

## 4. Stable ID and ownership inventory

List IDs exactly as approved. A runtime-only idempotency/correlation ID may be added only in the technical namespace and must not replace narrative IDs.

| Category | ID | Source owner | Runtime consumer | Persisted? | External dependency? |
| --- | --- | --- | --- | --- | --- |
| Milestone | | Codex narrative/content | | | |
| Chapter/context | | Codex narrative/content | | | |
| Quest | | Codex narrative/content | | | |
| State | | Codex narrative/content | | | |
| Objective | | Codex narrative/content | | | |
| Dialogue | | Codex narrative/content | | | |
| NPC/advisor | | Codex narrative/content | | | |
| Reward/artifact | | Codex narrative/content | | | |
| Location | | Codex narrative/content intent | | | |
| Gameplay hook | | Codex narrative/content intent / coordination contract | | | |
| Success event | | Codex narrative/content intent / coordination contract | | | |
| Failure event | | Codex narrative/content intent / coordination contract | | | |
| Cancel/unavailable event | | Codex narrative/content intent / coordination contract | | | |
| Localization key | | Codex narrative/content | | | |
| Idempotency/correlation key | | Codex coordination/review technical | | yes | no |

### Legacy mapping and aliases

```text
<Document mappings among C1, realm chapter IDs, archived CH0_PROLOGUE, display names, or existing runtime IDs. No silent aliases.>
```

## 5. Verified current architecture

Record current-main evidence, not assumptions.

### Reusable services and gaps

| Service/interface/path | Verified capability | NVS-01 gap | Reuse/extend/adapter decision |
| --- | --- | --- | --- |
| `IStoryService` / `LocalStoryService` | | | |
| `IQuestService` / `LocalQuestService` | | | |
| `IResourceService` | | | |
| `IReputationService` | | | |
| `IFactionService` | | | |
| `IPersonaService` | | | |
| `IWorldStateService` | | | |
| `IWorldAtlasService` | | | |
| `IGameDataService` | | | |
| `ISaveGameService` | | | |
| Champion deployment/arena | | | |
| Android `UnityView` | | | |

### Existing data sources

| Source | Current role | Authority for OMEN_1? | Coexistence/deprecation plan |
| --- | --- | --- | --- |
| Clean Android A1 packet | | | |
| Android hard-coded quest/dialogue seed | | | |
| Unity fallback dialogue | | | |
| Unity generic Q1–Q5 quests | | | |
| Transient chapter definitions | | | |
| StreamingAssets catalogs | | | |
| ScriptableObject definitions/assets | | | |
| SharedContracts/Fable schemas | | | |

### Existing scene flow

```text
Kingdom → deployment overlay → ChampionArena
Champion clear/defeat → Kingdom
```

Explain how the implementation carries quest context and typed result through or around this flow without duplicating the arena.

## 6. Chosen source-of-truth design

### Authoritative runtime content representation

```text
<Versioned JSON / deterministic generated asset / another justified representation.>
```

### Authoring/export path

```text
<How Codex narrative/content-owned narrative becomes the runtime artifact, including deterministic ordering and generated-file ownership.>
```

### Runtime load path

```text
<Path/API, initialization point, sync/async behavior, StreamingAssets/mobile behavior, cache lifetime.>
```

### Android/external preview path

```text
<How previews and tools consume the same content without becoming a second authority.>
```

### Fallback policy

Authoritative OMEN_1 data must never silently substitute unrelated story data.

```text
<Define visible missing/invalid behavior, diagnostics, and safe unavailable state.>
```

## 7. Contract and schema

### Version control

```text
Schema/catalog ID:
Version:
Supported versions:
Unknown-version behavior:
Backward compatibility policy:
```

### Root object

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

### Quest object

| Field | Type | Required | Source | Meaning | Validation |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

### State and transition objects

| Object | Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- | --- |
| State | | | | | |
| Transition | | | | | |

### Objective object

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

### Dialogue, choice, and semantic action objects

| Object | Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- | --- |
| Dialogue | | | | | |
| Choice | | | | | |
| Semantic action | | | | | |

### Consequence intent object

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

### External dependency object

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

### Localization metadata

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

### Required schema constraints

Specify:

- globally unique IDs by category/meaning,
- nonblank strings,
- allowed enum/string values,
- `additionalProperties` policy,
- deterministic collection ordering,
- array/dictionary choices compatible with the selected Unity parser,
- reserved dialogue terminal behavior,
- internal versus external reference rules,
- state reachability,
- one authoritative trigger per consequence,
- required success/failure/cancel events,
- D1–D16 approval metadata or packet approval hash/reference,
- localization key/exception declaration,
- unsupported-version failure.

### Fable/shared-contract decision

```text
Required / not required for NVS-01, with justification. Shared records must not use UnityEngine types.
```

## 8. Validation and error taxonomy

### Validation stages

```text
Authoring-time:
Build/CI-time:
Editor/import-time:
Runtime semantic validation:
```

### Required failures

| Error code/category | Trigger | Severity | Player/runtime behavior | Developer diagnostic |
| --- | --- | --- | --- | --- |
| Missing catalog | | | | |
| Malformed data | | | | |
| Unsupported version | | | | |
| Duplicate ID | | | | |
| Missing internal reference | | | | |
| Missing dialogue target | | | | |
| Invalid terminal target | | | | |
| Missing objective/state | | | | |
| Unreachable/invalid transition | | | | |
| Unknown hook/location | | | | |
| Requested dependency unavailable | | | | |
| Invalid consequence target | | | | |
| Invalid artifact representation | | | | |
| Corrupted/partial persisted state | | | | |
| Duplicate or mismatched result | | | | |

No failure may silently complete the quest, grant a reward, apply affinity, advance chapter, or switch to unrelated fallback content.

## 9. Runtime state machine

Use approved A1 states and transitions exactly.

| Current state | Event/trigger | Preconditions | Next state | Objective updates | Dialogue/action | Consequence request | Persist boundary | Invalid-event behavior |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| | | | | | | | | |

### Reachability and determinism

```text
<List one approved path to every state; identify terminal, transient, and recovery states. Prove D15 produces one start transition.>
```

### Reserved dialogue terminal

```text
<How the approved terminal closes dialogue without treating arbitrary missing targets as success.>
```

### Cancellation

```text
<Translate D9 into allowed events, cleanup, and reacceptance behavior.>
```

## 10. Objective runtime model

| Objective ID | Activation rule | Progress model | Completion rule | Failure/retry effect | UI/diagnostic expectation | Persisted fields |
| --- | --- | --- | --- | --- | --- | --- |
| | | | | | | |

Do not force event-driven narrative objectives into generic numeric counters when that would distort A1 intent.

## 11. Dialogue runtime behavior

```text
Lookup/resolution:
Choice progression:
Reserved terminal:
Semantic handoff action:
Missing-node behavior:
Speaker/display-name handling:
Localization/source-text handling:
D16 resume behavior:
Coexistence with fallback dialogue:
```

Repeated dialogue must not reapply one-time consequences unless A1 explicitly marks them repeatable.

## 12. Encounter request/result contract

### Request payload

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| Request/event ID | | | | |
| Correlation/idempotency ID | | | | |
| Quest ID | | | | |
| State/objective ID | | | | |
| Hook ID | | | | |
| Location ID/context | | | | |
| Realm/speaker context | | | | |
| Expected success event | | | | |
| Expected failure event | | | | |
| Expected cancel/unavailable event | | | | |
| Return destination | | | | |

### Result payload

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| Correlation/idempotency ID | | | | |
| Quest ID | | | | |
| Hook/encounter ID | | | | |
| Outcome | success/failure/cancel/unavailable | | | |
| Result event ID | | | | |
| Optional approved facts | | | | |

### Producer, consumer, and lifecycle

```text
Request producer:
Request consumer/Champion adapter:
Result producer:
Result consumer:
Context lifetime across scenes:
Free/non-quest arena entry behavior:
Duplicate request behavior:
Duplicate result behavior:
Mismatched/late result behavior:
Unavailable scene/hook behavior:
```

D8 controls whether this is a new requested capability or an adapter to verified existing support. Do not claim a named contract exists without evidence.

## 13. Chapter, realm, speaker, location, and start mapping

Translate D10–D12 and D15.

| Concern | Approved narrative answer | Runtime representation | Validation | Migration/default |
| --- | --- | --- | --- | --- |
| Realm-selection relationship | | | | |
| Eligible realms | | | | |
| Speaker/Valerius scope | | | | |
| Chapter/context ID | | | | |
| Location presentation | | | | |
| Quest-start trigger | | | | |
| Post-completion destination | | | | |

Specify real saved mutation. Do not treat `AdvanceStory()` event emission as chapter progression unless the implementation actually mutates and persists the approved destination.

## 14. Celestial Tear and report interaction

Translate D13 and D14 exactly.

```text
Artifact meaning:
Definition/source:
Ownership representation:
Acquisition event:
Delivery/transfer/consumption event:
Retention state:
Report interaction: manual / automatic / custom
Report objective completion rule:
Display/lore source:
Missing-definition behavior:
```

Do not reuse equipment or boss-loot ownership merely because those services exist unless the approved meaning and G1 design justify it.

## 15. Consequence orchestration and idempotency

### Approved order

```text
<Success result → artifact acquisition/delivery → report → affinity/reward → completion, or the exact A1-approved sequence.>
```

### Consequence table

| Consequence | Approved trigger | Target | Idempotency key | Persist-before/after | Duplicate behavior | Recovery behavior |
| --- | --- | --- | --- | --- | --- | --- |
| Valerius affinity | | | | | | |
| Gold/resource | | | | | | |
| Celestial Tear | | | | | | |
| Quest completion | | | | | | |
| Chapter/unlock | | | | | | |
| Faction/world state, if approved | | | | | | |

### Atomicity model

```text
<Define operation ordering, save boundary, applied-consequence ledger, transaction-like recovery, and visible failure.>
```

Required guarantees:

- duplicate result delivery applies nothing twice,
- dialogue replay applies nothing twice,
- retry/reload applies nothing twice,
- partial failure at each boundary recovers deterministically,
- ledger and actual side effects cannot permanently disagree,
- intentionally repeatable effects repeat only under approved conditions.

## 16. Persistence, defaults, migration, and D16 resume

### Persisted data

| Field | Type | Default for old saves | Validation | Migration | Meaning |
| --- | --- | --- | --- | --- | --- |
| Quest state | | | | | |
| Active objective/progress | | | | | |
| Dialogue position, if required | | | | | |
| Handoff status/context | | | | | |
| Failure/retry state | | | | | |
| Applied-consequence ledger | | | | | |
| Artifact state | | | | | |
| Chapter/unlock state | | | | | |

Issue #136 or equivalent normalization must be complete before approved affinity/faction/persona mutations rely on those fields.

### Resume matrix

| Interruption point | Loaded state/objective | Dialogue/UI behavior | Consequence state | Tests |
| --- | --- | --- | --- | --- |
| Mid-dialogue | | | | |
| Before handoff request | | | | |
| Handoff requested, scene not entered | | | | |
| During arena | | | | |
| Arena failure | | | | |
| Arena success before report | | | | |
| During report | | | | |
| After some consequences | | | | |
| Completed | | | | |
| Corrupted/partial state | | | | |

Translate D16; do not invent resume semantics.

## 17. Android/Unity boundary

```text
Narrative authoring owner:
Runtime content artifact:
Android preview role:
Unity runtime owner:
Standalone Unity playtest path:
Embedded bridge status (#135):
Route/result contract relationship:
```

A1 content and runtime technical state must not be duplicated as independent authorities.

## 18. Diagnostics and observability

Define structured diagnostics for:

- packet load and version,
- validation failures,
- quest start/transition rejection,
- dialogue resolution,
- encounter request/result correlation,
- unavailable hook/location,
- consequence attempt/apply/skip/rollback,
- save migration/defaulting,
- resume/recovery,
- completion.

```text
Logging categories/codes:
Player-visible unavailable/error state:
Sensitive-data considerations:
Debug/editor inspection:
```

## 19. File-impact plan and locks

### Required files

| File/path | Change | Why required | Owner | Shared lock? |
| --- | --- | --- | --- | --- |
| | | | | |

### Optional files

| File/path | Condition requiring it | Owner | Shared lock? |
| --- | --- | --- | --- |
| | | | |

### Prohibited files

List A1 narrative files, unrelated systems, and any path that must not be touched.

### Designated shared files

Do not assume these must change:

- `unity/Assets/AL/Scripts/Core/Bootloader.cs`
- `unity/Assets/AL/Scripts/Data/Runtime/SaveGameData.cs`
- `unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs`
- `unity/Assets/AL/Scripts/Utilities/ProjectInitializer.cs`

For each proposed shared-file edit, provide necessity, backward compatibility, conflict check, lock owner, and release condition.

## 20. Test and fault matrix

### Contract/validation

- valid packet,
- missing/malformed/unsupported catalog,
- duplicate IDs,
- missing dialogue/speaker/objective/state/reference,
- invalid terminal,
- unreachable/invalid transition,
- unknown hook/location/event,
- invalid consequence/artifact target,
- deterministic generation/ordering,
- shared/Fable validation when applicable.

### Narrative/runtime paths

- D15 start,
- happy path,
- optional lore branch,
- D1 handoff,
- arena success,
- D2/D3 failure and retry,
- D9 cancellation/unavailable,
- D14 report,
- D13 artifact meaning,
- D4–D6 consequence/completion order,
- D10–D12 realm/chapter/location path.

### Persistence and idempotency

- every D16 resume row,
- old-save default/migration,
- completed-state reload,
- duplicate request/result,
- repeated dialogue,
- repeated retry,
- mismatched/late result,
- consequence fault injection after each boundary,
- artifact duplication prevention,
- affinity/reward/completion duplication prevention.

### Regression

- Unity batch import/C# compile,
- EditMode tests,
- committed PlayMode smoke from #127 when available,
- representative scene,
- free Champion entry,
- Kingdom flow,
- Android unit tests,
- Android debug assembly,
- final diff/worktree cleanliness.

For every test specify setup, action, expected state, expected side effects, save/reload step, and exact evidence.

## 21. C1–C4 implementation sequence

### C1 — Contract, loading, validation

```text
Files:
Behavior:
Tests:
Shared locks:
Completion evidence:
```

### C2 — State, dialogue, objectives, encounter handoff

```text
Files:
Behavior:
Tests:
Shared locks:
Completion evidence:
```

### C3 — Persistence, migration, consequence orchestration

```text
Files:
Behavior:
Tests:
Shared locks:
Completion evidence:
```

### C4 — Integration, diagnostics, regression evidence

```text
Files:
Behavior:
Tests/manual evidence:
Shared-lock release:
Completion evidence:
```

State whether one focused PR is acceptable or dependency-ordered PRs are required. No parallel implementation of the same completion.

## 22. Rollback and data safety

```text
Code rollback:
Catalog/schema rollback:
Save compatibility after rollback:
Partially applied consequence recovery:
Unknown/newer packet behavior:
Feature-disable behavior:
Shared-file rollback owner:
```

G1 must not require deletion/reset of a valid player profile to recover from ordinary integration failure.

## 23. Definition of done

- [ ] A1 and issue #138 D1–D16 are fully traceable.
- [ ] No narrative intent changed.
- [ ] One authoritative content source exists.
- [ ] Contract/schema/version and strict validation are complete.
- [ ] State/objective/dialogue/start/report/handoff/result behavior is deterministic.
- [ ] Chapter/realm/location/speaker mapping matches D10–D12 and D15.
- [ ] Celestial Tear/report implementation matches D13–D14.
- [ ] Persistence and D16 resume cover every required state.
- [ ] Consequences are atomic/idempotent across duplicate, retry, reload, and fault injection.
- [ ] Error behavior is visible and never grants false progress.
- [ ] Required/optional/prohibited files and locks are explicit.
- [ ] Test matrix and C1–C4 order are implementation-ready.
- [ ] Codex narrative/content can review narrative fidelity from the same specification.
- [ ] Codex can implement without inventing story or architecture outside the approved boundary.

## 24. Codex handoff

```text
Codex engineering: implement issue #134 from this approved G1 specification and the exact approved A1 commit. Do not rewrite narrative, broaden scope, or start a parallel path. Declare shared-file locks before editing; preserve old saves and all existing service registrations; implement strict validation, deterministic state/event behavior, typed encounter request/result handling, D16 resume, consequence idempotency, and the standing optimization requirements; run and report the full specified test matrix. Return the implementation PR for Codex coordination/review G2 and Codex narrative/content A2.
```

## 25. Unresolved decisions

List only genuine blockers. Do not select narrative answers or unapproved implementation assumptions.

| Blocker | Owner | Required evidence/decision | Downstream impact |
| --- | --- | --- | --- |
| | | | |
