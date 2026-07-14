# NVS-01 G1 Runtime Integration Specification Template

Use this template only after the clean A1 narrative packet is complete and the user has approved its narrative intent.

This is a GPT-owned technical specification structure. It must translate A1 into implementable requirements without rewriting dialogue, choices, characters, lore, reward intent, failure meaning, chapter placement, or outcomes.

## Document control

```text
Milestone: NVS-01
Task: G1
Specification version:
GPT branch:
GPT commit:
A1 issue: #128
A1 PR:
A1 commit:
A1 packet version:
User narrative approval reference:
Current main commit inspected:
Risk register version/commit:
Specification status: Draft / Blocked / Approved
Codex implementation issue: #134
```

## 1. Approval and completeness gate

G1 may proceed only when all are true:

- [ ] Clean A1 branch started from current `main`.
- [ ] A1 contains exactly one bounded `OMEN_1` packet.
- [ ] D1–D12 user decisions are recorded.
- [ ] All internal A1 references resolve.
- [ ] State, objective, failure, retry, cancellation, recovery, and consequence intent are complete.
- [ ] External dependencies are marked requested or verified with evidence.
- [ ] User approved the narrative intent.
- [ ] GPT verified A1 ownership and completeness.
- [ ] No runtime-owned change is mixed into A1.

If any item is false, stop and list the blocker. Do not fill the missing narrative intent in this specification.

## 2. Executive technical goal

### Goal

```text
<Describe the exact playable/runtime capability that implements the approved A1 packet.>
```

### Player-observable definition

```text
<Describe what the player can start, see, choose, hand off to gameplay, recover from, complete, save, reload, and resume.>
```

### Explicit non-goals

```text
<List excluded work: full Chapter 1, Android↔Unity embedding if deferred, broad localization, general quest refactor, unrelated UI/VFX/combat, etc.>
```

### Delivery target

Choose and justify based on user-approved scope and current architecture:

```text
Standalone Unity runtime vertical slice / Android preview + standalone Unity / true embedded Android↔Unity flow / another approved target
```

Do not claim the Android placeholder `UnityView` is a real embedded bridge.

## 3. Upstream narrative traceability

### Source-of-truth files

| File | Commit | Narrative responsibility |
| --- | --- | --- |
| | | |

### User decision traceability

| Decision | Approved A1 answer | G1 technical consequence | Narrative unchanged? |
| --- | --- | --- | --- |
| D1 | | | yes/no |
| D2 | | | yes/no |
| D3 | | | yes/no |
| D4 | | | yes/no |
| D5 | | | yes/no |
| D6 | | | yes/no |
| D7 | | | yes/no |
| D8 | | | yes/no |
| D9 | | | yes/no |
| D10 | | | yes/no |
| D11 | | | yes/no |
| D12 | | | yes/no |

Every “no” is a specification defect requiring return to A1/user approval.

## 4. Stable ID inventory and ownership

List every ID exactly as approved.

| Category | ID | Source owner | Runtime consumer(s) | Persisted? | External dependency? |
| --- | --- | --- | --- | --- | --- |
| Milestone | | Android Studio | | | |
| Chapter/context | | Android Studio | | | |
| Quest | | Android Studio | | | |
| Objective | | Android Studio | | | |
| Dialogue | | Android Studio | | | |
| NPC/advisor | | Android Studio | | | |
| Reward/artifact | | Android Studio | | | |
| Gameplay hook | | Android Studio intent / GPT contract | | | |
| Location | | Android Studio intent | | | |
| Success event | | Android Studio intent / GPT contract | | | |
| Failure event | | Android Studio intent / GPT contract | | | |
| Localization key | | Android Studio | | | |
| Idempotency key | | GPT technical | | yes | no |

### Alias/mapping rules

```text
<Document any approved mapping between CH0_PROLOGUE, C1, realm chapter IDs, display names, legacy IDs, or existing runtime IDs. Do not create aliases without migration/validation.>
```

## 5. Current architecture inventory

Record verified current-main behavior relevant to the implementation.

### Existing reusable services

| Service/interface | Verified capability | Gap for NVS-01 | Reuse/extend/adapter decision |
| --- | --- | --- | --- |
| `IStoryService` | | | |
| `IQuestService` | | | |
| `IResourceService` | | | |
| `IReputationService` | | | |
| `IFactionService` | | | |
| `IWorldStateService` | | | |
| `IWorldAtlasService` | | | |
| `ISaveGameService` | | | |
| `IGameDataService` | | | |
| Champion/scene flow | | | |

### Existing data sources

| Source | Current use | Authority for NVS-01? | Migration/deprecation plan |
| --- | --- | --- | --- |
| Android hard-coded `KingdomModels` seed | | | |
| Android A1 packet | | | |
| Unity `LocalStoryService` fallback | | | |
| Unity `LocalQuestService` Q1–Q5 | | | |
| transient `LocalGameDataService` chapters | | | |
| StreamingAssets catalogs | | | |
| ScriptableObject definitions/assets | | | |
| SharedContracts schemas/Fable | | | |

### Existing scene flow

```text
Kingdom command → deployment overlay → ChampionArena
Champion clear/defeat → direct Kingdom scene load
```

Document how quest context/result will wrap or adapt this flow without duplicating the arena.

## 6. Chosen source-of-truth and consumption design

### Authoritative content source

```text
<Approved representation: versioned JSON, generated asset, another existing path. Justify against A1, Android authoring, Unity runtime, and Fable needs.>
```

### Generation/export path

```text
<How Android Studio-owned content becomes the runtime-consumable artifact. Define generated versus hand-authored files and deterministic ordering.>
```

### Runtime loading path

```text
<File/path/API, sync/async behavior, mobile StreamingAssets considerations, initialization timing, and cache lifetime.>
```

### Preview/tooling path

```text
<How Android and external tools preview or validate the same data without becoming a second authority.>
```

### Fallback policy

Authoritative quest data must not silently fall back to a different story.

```text
<Define missing/invalid behavior: fail milestone availability visibly, diagnostics, safe UI state, no progression/reward.>
```

## 7. Contract and schema

### Contract version

```text
Catalog/schema ID:
Version:
Supported versions:
Unsupported-version behavior:
```

### Root object

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

### Quest object

| Field | Type | Required | Source | Meaning | Validation |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

### State object

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

### Transition object

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

### Objective object

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

### Dialogue object

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

### Choice/action object

| Field | Type | Required | Meaning | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

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

### Schema constraints

Specify:

- ID format and uniqueness,
- `additionalProperties` policy,
- array versus dictionary choice for `JsonUtility` compatibility,
- nonempty strings,
- allowed enum/string values,
- reserved terminal target,
- internal versus external reference rules,
- state reachability requirements,
- one authoritative trigger per consequence,
- required success/failure events,
- localization coverage/exception declarations.

### Fable/shared-contract decision

```text
Required / not required for NVS-01. Justify. If required, keep shared records free of UnityEngine types.
```

## 8. Validation pipeline

### Authoring-time validation

```text
<Android unit tests / schema validation / ID registry checks / negative fixtures.>
```

### Build-time validation

```text
<How invalid packet data fails CI/build/editor checks.>
```

### Runtime semantic validation

Required error classes:

| Error code/category | Trigger | Severity | Player behavior | Developer diagnostic |
| --- | --- | --- | --- | --- |
| Missing catalog | | | | |
| Malformed data | | | | |
| Unsupported version | | | | |
| Duplicate ID | | | | |
| Missing internal reference | | | | |
| Missing dialogue target | | | | |
| Invalid terminal target | | | | |
| Invalid/unreachable transition | | | | |
| Missing objective | | | | |
| Unknown hook/location | | | | |
| Requested dependency unavailable | | | | |
| Invalid consequence target | | | | |
| Corrupted persisted state | | | | |

Failures must not silently complete the quest, apply consequences, or substitute unrelated fallback content.

## 9. State machine

Use the approved A1 states and transitions exactly.

| Current state | Event/trigger | Preconditions | Next state | Objective updates | Dialogue/action | Consequence request | Persist before/after | Invalid-event behavior |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| | | | | | | | | |

### Reachability proof

```text
<List every state and one approved path reaching it. Identify terminal/transient/recovery states.>
```

### Reserved terminal handling

```text
<Define how `end` or the approved terminal closes dialogue without allowing arbitrary missing targets.>
```

## 10. Objective model

| Objective ID | Activation rule | Progress model | Completion rule | Failure effect | Retry effect | UI/diagnostic expectation | Persisted fields |
| --- | --- | --- | --- | --- | --- | --- | --- |
| | | | | | | | |

Specify whether progress is boolean, numeric, event-driven, or another approved model. Do not force all narrative objectives into generic numeric counters if that would distort A1 intent.

## 11. Dialogue runtime behavior

### Resolution

```text
<Lookup by stable node ID, speaker ID, localization/source text, choices, terminal/handoff action.>
```

### Progression

```text
<How a choice resolves to another node, terminal, or semantic action.>
```

### Missing node

```text
<Visible failure, diagnostic, no consequence/progression.>
```

### Resume

```text
<Approved mid-dialogue behavior from A1 translated into persisted/runtime requirements.>
```

### Existing fallback coexistence

```text
<How OMEN_1 avoids duplication/conflict with LocalStoryService fallback dialogue.>
```

## 12. Encounter handoff and result contract

### Request context

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| Request/event ID | | | |
| Quest ID | | | |
| Objective ID | | | |
| Hook ID | | | |
| Location ID | | | |
| Realm ID/context | | | |
| Expected success event | | | |
| Expected failure event | | | |
| Return destination/state | | | |
| Correlation/idempotency ID | | | |

### Request producer

```text
<Service/controller/action that publishes the request.>
```

### Request consumer

```text
<Adapter around existing Champion deployment/arena flow.>
```

### Context lifetime

```text
<Memory, persisted save, scene-transition carrier, reload behavior.>
```

### Success payload

| Field | Meaning | Used by narrative? | Persisted? |
| --- | --- | --- | --- |
| | | | |

### Failure payload

| Field | Meaning | Used by narrative? | Persisted? |
| --- | --- | --- | --- |
| | | | |

### Cancel/unavailable behavior

```text
<What occurs if the scene/hook cannot load, player exits, application closes, or context is invalid.>
```

### Free Champion-mode compatibility

```text
<How ordinary non-quest arena entry remains unchanged.>
```

### Android bridge scope

```text
<Explicitly state whether #135 is out of scope. Do not imply the placeholder UnityView carries the context.>
```

## 13. Chapter and realm progression

### Entry mapping

```text
<How approved A1 chapter/context maps to current save IDs and selected realm.>
```

### Completion mutation

```text
<Chapter change / unlock flag / completed prologue / another approved result.>
```

### `AdvanceStory()` relationship

Current generic `AdvanceStory()` does not mutate the chapter. Specify whether it is extended, adapted, bypassed, or only emitted after a real mutation.

### Invalid mapping

```text
<Visible error and safe state.>
```

### Old-save behavior

```text
<Default/migration when current chapter or realm context predates NVS-01.>
```

## 14. Consequence orchestration and idempotency

Use A1-approved timing and repeatability exactly.

| Consequence | Target | Trigger | Idempotency key | Precondition | Apply operation | Persist/ledger order | Duplicate behavior | Failure recovery |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Affinity | | | | | | | | |
| Gold/resource | | | | | | | | |
| Artifact/reward | | | | | | | | |
| Quest completion | | | | | | | | |
| Faction/reputation if approved | | | | | | | | |
| World state if approved | | | | | | | | |

### Transaction-like boundary

Current services save at different times. Define:

- validation before side effects,
- deterministic operation order,
- ledger write timing,
- save timing,
- partial-failure recovery,
- replay after crash,
- duplicate event processing,
- rollback or completion-forward policy.

The observable state after recovery must apply each one-time effect exactly once.

## 15. Artifact/reward representation

### Approved narrative meaning

```text
<Retained artifact / delivered specimen / consumed token / other approved meaning.>
```

### Definition lookup

```text
<ArtifactDefinition, catalog, generated asset, or another approved source.>
```

### Ownership/persistence

```text
<New or existing state/service. Justify why equipment/boss-loot models are or are not appropriate.>
```

### Acquisition/consumption

```text
<Exact trigger and state transition.>
```

### Duplicate behavior

```text
<Exactly-once acquisition/consumption and reload behavior.>
```

## 16. Persistence model

### Existing fields reused

| Field | Why safe | Default/compatibility |
| --- | --- | --- |
| | | |

### New fields proposed

| Field | Type | Owner object | Default for old save | Migration | Reason |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

Potential needs to evaluate, not assume:

- quest narrative state,
- active objective(s),
- dialogue resume node,
- selected branch,
- encounter context/status,
- correlation/result acknowledgement,
- applied-consequence keys,
- artifact ownership/status,
- chapter/unlock state.

### Save normalization

Issue #136 must be considered for reputation/faction/persona fields.

```text
<Define required defaults and tests.>
```

### Old-save migration

```text
<Missing fields, legacy quest IDs, chapter mappings, malformed partial state.>
```

### Resume matrix

| Save point/interruption | Expected loaded state | Dialogue/objective behavior | Consequences applied? | Player action |
| --- | --- | --- | --- | --- |
| Before quest start | | | | |
| Mid-dialogue | | | | |
| After acceptance before handoff | | | | |
| During handoff/arena | | | | |
| After failure before retry | | | | |
| After success before report | | | | |
| During success dialogue | | | | |
| After some consequence steps | | | | |
| Completed | | | | |

### Crash-safe save scope

Issue #137 is deferred. State whether NVS-01 uses current save writes with transaction-like idempotency, or whether a narrower prerequisite is unavoidable. Do not absorb the full Phase 5 backup/recovery milestone without user reprioritization.

## 17. Android authoring/preview relationship

### Source-of-truth rule

```text
<How Android Studio owns content while avoiding a second independent quest engine.>
```

### Existing hard-coded Android quest seed

```text
<Temporary mapping/removal/generation plan for OMEN_1 title/description/mode/marker.>
```

### QuestScreen scope

```text
<Required for NVS-01 preview/runtime or explicitly out of scope.>
```

### Dialogue overlay scope

```text
<Required preview behavior versus standalone Unity runtime behavior.>
```

### Real Android↔Unity bridge

```text
<Out of scope under #135 unless user explicitly expands this milestone.>
```

## 18. Localization strategy for this milestone

### A1 policy consumed

```text
<Full keys / source-text exceptions / phased policy.>
```

### Runtime lookup

```text
<Implemented now / metadata-only for future migration / another approved choice.>
```

### Missing key behavior

```text
<Visible fallback/diagnostic; no silent unrelated text.>
```

### Broader tooling

Issue #131 remains the later localization/governance milestone. Define what NVS-01 does without pretending the full pipeline exists.

## 19. Diagnostics and observability

Define structured diagnostics for:

- packet load result/version,
- validation summary,
- quest state transitions,
- objective activation/completion,
- dialogue resolution,
- handoff request/context,
- success/failure result reception,
- consequence application/duplicate suppression,
- save/migration/recovery,
- unavailable dependency,
- and terminal completion.

Specify severity and avoid logging player-facing narrative text or sensitive save content unnecessarily.

### Correlation

```text
<How logs/events for one quest run/handoff are correlated.>
```

## 20. File-impact plan and ownership

### Required files

| File/path | Owner | Change reason | Shared lock? | Validation |
| --- | --- | --- | --- | --- |
| | | | | |

### Optional files

| File/path | Why optional | Decision condition |
| --- | --- | --- |
| | | |

### Prohibited files/content

```text
<List A1 narrative text files Codex must not rewrite, unrelated systems, Android embedding, broad Chapter 1, etc.>
```

### Shared-file lock declaration

For each proposed shared file:

| File | Required? | Existing open lock? | Lock holder/PR | Merge order |
| --- | --- | --- | --- | --- |
| Bootloader.cs | | | | |
| SaveGameData.cs | | | | |
| LocalGameDataService.cs | | | | |
| ProjectInitializer.cs | | | | |

Do not list a shared file as required without justification.

## 21. Test matrix

### Contract/validation tests

| Test | Input | Expected result |
| --- | --- | --- |
| Valid packet | | |
| Missing catalog | | |
| Malformed JSON/data | | |
| Unsupported version | | |
| Duplicate ID | | |
| Missing dialogue target | | |
| Invalid terminal target | | |
| Invalid/unreachable transition | | |
| Missing objective | | |
| Unknown hook/location | | |
| Invalid consequence target | | |

### State-machine tests

| Path | Key assertions |
| --- | --- |
| Happy path | |
| Optional lore/choice branch | |
| Handoff request | |
| Arena success | |
| Arena failure | |
| Retry/recovery | |
| Cancellation/abandonment | |
| Invalid event in each key state | |

### Persistence tests

| Scenario | Key assertions |
| --- | --- |
| Old save missing new fields | |
| Mid-dialogue reload | |
| Pre-handoff reload | |
| During-handoff recovery | |
| After failure reload | |
| After success before report | |
| During consequence application | |
| Completed-state reload | |
| Corrupted/partial narrative state | |

### Idempotency/fault tests

| Scenario | Key assertions |
| --- | --- |
| Duplicate success event | |
| Duplicate failure event | |
| Dialogue replay | |
| Retry after affinity | |
| Crash after affinity before gold | |
| Crash after gold before artifact | |
| Crash after artifact before completion | |
| Repeated load/recovery | |

### Integration tests

| Test | Expected result |
| --- | --- |
| Existing free Champion entry | unchanged |
| Quest-launched Champion entry | |
| Success return to narrative | |
| Failure return to narrative | |
| Unavailable scene/hook | visible failure, no completion/reward |
| Existing Q1–Q5 generic quests | unchanged |
| Existing realm selection | unchanged |
| Existing story fallback | no OMEN_1 duplication/conflict |

### Regression commands

Specify exact:

- Unity batch compile,
- EditMode tests,
- PlayMode tests,
- Android unit tests,
- Android debug assembly,
- schema/Fable tests when applicable,
- manual scene/playtest scenarios.

## 22. Implementation decomposition and merge order

### C1 — contract loading and validation

```text
<Branch/PR scope, dependencies, files, tests, no shared locks unless justified.>
```

### C2 — quest state and encounter handoff

```text
<Dependency on C1, files, events, scene adapter, tests.>
```

### C3 — persistence and compatibility

```text
<Dependency on approved state model, #136, shared locks, old-save tests.>
```

### C4 — verification and publication

```text
<Full regression matrix, diagnostics, PR report.>
```

### Merge order

```text
<Exact dependency order, rebase points, shared-lock release.>
```

Avoid parallel PRs that modify the same model/contract/save/service registration.

## 23. Rollback and recovery plan

Define:

- how to disable/remove NVS-01 runtime availability without damaging saves,
- behavior when packet version is unsupported,
- behavior when encounter integration is unavailable,
- compatibility after reverting implementation code with saves containing new fields,
- and how generated artifacts are rolled back/reproduced.

## 24. Security and data-safety considerations

Consider:

- untrusted/invalid catalog data,
- path traversal or arbitrary file access avoidance,
- excessive payload sizes,
- malformed IDs/strings,
- save tampering implications,
- log redaction,
- and Android↔Unity message trust only if #135 is explicitly in scope.

Do not overstate threat controls; document actual mitigations and residual risk.

## 25. Definition of done

NVS-01 runtime integration is done only when:

- [ ] A1 narrative intent is unchanged and traceable.
- [ ] Packet loads and validates through the approved source.
- [ ] All approved state/choice/failure/retry paths execute deterministically.
- [ ] Existing Champion gameplay is reused without unrelated redesign.
- [ ] Quest context and success/failure returns survive required transitions/reloads.
- [ ] Old saves load with backward-compatible defaults/migration.
- [ ] Every one-time consequence is applied exactly once.
- [ ] Invalid data/dependencies fail visibly without completion/reward.
- [ ] All specified automated and manual checks pass with exact evidence.
- [ ] Shared files were locked, reviewed, and released.
- [ ] GPT G2 review passes.
- [ ] Android Studio narrative-fidelity review passes.
- [ ] User playtest accepts the complete loop.
- [ ] Final integrated state is on `main`.

## 26. Unresolved decisions and blockers

| ID | Question | Owner | Blocking section/task | Required evidence/decision |
| --- | --- | --- | --- | --- |
| | | | | |

Do not hide blockers inside prose. A blocking unresolved decision prevents Codex handoff.

## 27. Codex handoff prompt

After approval, include a complete copy-paste prompt containing:

- repository/workspace,
- exact A1/G1 PRs and commits,
- goal/non-goals,
- required files/optional files/prohibited files,
- contracts and state/event tables,
- save/migration/idempotency requirements,
- shared locks and merge order,
- exact test matrix,
- PR/report requirements,
- and stop conditions when narrative/spec data is contradictory.

## 28. G1 approval checklist

- [ ] Upstream A1 and user approval are exact and immutable for this implementation.
- [ ] Every A1 path maps to the state machine.
- [ ] Every event has producer, consumer, payload, and duplicate behavior.
- [ ] Every contract field maps to stable IDs.
- [ ] Authoritative source and fallback policy are explicit.
- [ ] Chapter/realm mapping and progression mutation are explicit.
- [ ] Encounter handoff reuses existing arena and preserves free mode.
- [ ] Android/Unity scope is honest about the placeholder bridge.
- [ ] Persistence/defaults/migration cover every state.
- [ ] Consequence orchestration is transaction-like and idempotent.
- [ ] Artifact representation matches approved narrative meaning.
- [ ] Invalid data and missing dependencies fail visibly.
- [ ] Required/optional/prohibited file impacts are separated.
- [ ] Shared locks and merge order are explicit.
- [ ] Full happy/branch/failure/retry/reload/duplicate/fault/invalid-data tests are specified.
- [ ] Rollback and residual risks are documented.
- [ ] No narrative content was rewritten.
- [ ] No blocker remains before #134 starts.
