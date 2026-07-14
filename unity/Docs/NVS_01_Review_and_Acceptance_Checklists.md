# NVS-01 Review and Acceptance Checklists

Use these checklists after A1 and G1 are approved and Codex opens the NVS-01 implementation PR.

They define evidence and ownership boundaries. They do not change narrative intent or implementation requirements.

## Document control

```text
Milestone: NVS-01
A1 issue/PR/commit:
User narrative approval:
G1 issue/PR/commit:
Codex issue/PR/commit:
Validated main/base commit:
Shared-file locks declared:
Current review stage: G2 / A2 / U1 / Closeout
```

## Evidence package required from Codex

Before G2 starts, the implementation PR must contain:

- root cause/technical goal,
- exact A1 and G1 inputs consumed,
- changed files and why,
- required/optional/shared file declarations,
- contract/schema decisions,
- loader/validator behavior,
- state/event implementation mapping,
- save/default/migration behavior,
- consequence idempotency behavior,
- encounter handoff and return behavior,
- diagnostics/error behavior,
- exact compile/test/manual commands and results,
- known limitations,
- unperformed validation and reason,
- rollback/recovery considerations,
- confirmation that narrative text/meaning/outcomes were not rewritten.

A PR without this evidence is not ready for G2 even if code compiles.

# G2 — GPT Technical and Integration Review

**Owner:** GPT  
**Input:** approved A1, approved G1, Codex PR/evidence  
**Output:** approve, request changes tied to requirements, or block with explicit evidence gap

## G2.1 Dependency and scope

- [ ] A1 PR/commit exactly matches the approved narrative packet.
- [ ] User narrative approval is linked.
- [ ] G1 PR/commit exactly matches the implementation handoff.
- [ ] Codex branch started from the required base.
- [ ] No parallel implementation PR targets the same completion.
- [ ] Diff contains one major completion and excludes unrelated cleanup.
- [ ] Later Chapter 1, cross-system, governance, or Android↔Unity bridge scope is absent unless explicitly approved.
- [ ] No A1 narrative file was rewritten by Codex.

## G2.2 Changed-file ownership

For every changed file, record:

| File | Workstream owner | Required by G1? | Shared lock? | Review result |
| --- | --- | --- | --- | --- |
| | | | | |

Checks:

- [ ] Every runtime/test/tool/contract file is within Codex ownership.
- [ ] Every generated file is deterministic and necessary.
- [ ] No dialogue, NPC characterization, lore, choice, reward intent, failure meaning, or chapter placement changed.
- [ ] No file absent from G1’s required/optional impact list changed without explanation and review.
- [ ] No shared file was edited before its soft lock was declared.

## G2.3 Contract and source of truth

- [ ] One authoritative OMEN_1 content source is identified.
- [ ] Android, Unity, and external tools do not maintain conflicting authoritative copies.
- [ ] Contract version and supported versions match G1.
- [ ] Required/optional fields match G1.
- [ ] Stable IDs match A1 exactly.
- [ ] Internal references resolve.
- [ ] External dependencies are not falsely marked implemented.
- [ ] Shared contracts contain no `UnityEngine` types where Fable compatibility applies.
- [ ] Runtime does not silently substitute unrelated fallback story data.

## G2.4 Validation/error behavior

Verify tests/evidence for:

- [ ] missing catalog/data,
- [ ] malformed data,
- [ ] unsupported version,
- [ ] duplicate IDs,
- [ ] missing dialogue target,
- [ ] invalid terminal target,
- [ ] missing objective,
- [ ] invalid/unreachable transition,
- [ ] unknown/unavailable hook,
- [ ] unknown location,
- [ ] invalid consequence target,
- [ ] corrupted/partial persisted state.

For every failure:

- [ ] diagnostics are clear,
- [ ] quest does not silently complete,
- [ ] rewards/consequences are not applied,
- [ ] player/runtime enters the G1-approved safe state.

## G2.5 State machine fidelity

Compare every G1 transition to code/tests:

| G1 transition | Implementation location | Test | Result |
| --- | --- | --- | --- |
| | | | |

Checks:

- [ ] entry/unlock transition is deterministic,
- [ ] optional lore/choice branch is preserved,
- [ ] every state is reachable only through approved paths,
- [ ] no extra narrative branch was added,
- [ ] terminal/transient/recovery semantics match A1/G1,
- [ ] invalid events are rejected visibly,
- [ ] objective activation/completion matches G1,
- [ ] reserved dialogue terminal is distinguished from missing targets.

## G2.6 Encounter handoff

- [ ] Existing Champion arena is reused; no duplicate combat implementation exists.
- [ ] Quest launch carries the approved request/context IDs.
- [ ] Free/non-quest Champion entry remains operational.
- [ ] Success result maps to the approved success event/state.
- [ ] Failure result maps to the approved failure/recovery event/state.
- [ ] Cancel/unavailable/interruption behavior matches G1.
- [ ] Context survives required scene transitions/reloads.
- [ ] Duplicate result delivery is suppressed/handled idempotently.
- [ ] Missing hook/scene/context fails visibly.
- [ ] Android `UnityView` placeholder is not represented as a completed bridge unless #135 was explicitly brought into scope.

## G2.7 Chapter/realm progression

- [ ] Entry mapping matches A1-approved D10–D12.
- [ ] Realm/speaker scope matches A1.
- [ ] Saved chapter/context mutation is real and testable.
- [ ] Generic `AdvanceStory()` is not mistaken for mutation unless implementation changed according to G1.
- [ ] Invalid/legacy chapter IDs are handled according to migration rules.
- [ ] Post-completion destination/unlock matches A1.

## G2.8 Persistence/defaults/migration

- [ ] Persisted fields match G1.
- [ ] Old-save defaults are present.
- [ ] Issue #136 or equivalent required normalization is complete before affinity/faction/persona consequences.
- [ ] Existing non-null old-save data is preserved.
- [ ] Mid-dialogue resume matches A1/G1.
- [ ] Pre-handoff reload matches G1.
- [ ] Mid-handoff interruption/recovery matches G1.
- [ ] Failure/retry reload matches G1.
- [ ] Success-before-report reload matches G1.
- [ ] Completed-state reload remains completed.
- [ ] Corrupted/partial narrative state has visible recovery/failure.
- [ ] Existing unrelated save fields are unchanged.

## G2.9 Consequence idempotency and atomicity

For each consequence:

| Consequence | Approved trigger | Idempotency key | Fault/reload tests | Result |
| --- | --- | --- | --- | --- |
| Affinity | | | | |
| Gold/resource | | | | |
| Artifact/reward | | | | |
| Completion | | | | |
| Faction/world state if approved | | | | |

Checks:

- [ ] operation order matches G1,
- [ ] duplicate success event applies nothing twice,
- [ ] dialogue replay applies nothing twice,
- [ ] retry applies nothing twice,
- [ ] partial failure after each consequence boundary recovers deterministically,
- [ ] ledger/save state agrees with applied effects,
- [ ] one-time effects remain one-time after reload,
- [ ] intentionally repeatable effects repeat only under approved conditions.

## G2.10 Artifact/reward implementation

- [ ] Celestial Tear representation matches A1-approved meaning.
- [ ] Definition lookup is stable and validated.
- [ ] Ownership/consumption is persisted if required.
- [ ] Equipment/boss-loot models were not reused without G1 justification.
- [ ] acquisition/delivery/retention timing matches A1.
- [ ] duplicate acquisition is prevented.
- [ ] missing definition fails visibly without false completion.

## G2.11 Regression and validation evidence

Required exact evidence:

- [ ] Unity batch import/C# compile.
- [ ] EditMode test totals/results.
- [ ] PlayMode test totals/results.
- [ ] Existing representative scene smoke.
- [ ] NVS-01 happy path.
- [ ] NVS-01 branch path.
- [ ] failure/retry path.
- [ ] reload/resume matrix.
- [ ] invalid-data matrix.
- [ ] duplicate/fault-injection matrix.
- [ ] Android unit tests.
- [ ] Android debug assembly.
- [ ] schema/Fable validation when applicable.
- [ ] exact tested branch and SHA.
- [ ] final worktree/diff cleanliness.

No result may be inferred from a different branch or pre-merge state.

## G2.12 G2 decision

```text
Decision: Approve / Changes requested / Blocked
Requirements violated or satisfied:
Evidence gaps:
Shared-lock status:
Required next owner:
```

Requested changes must cite A1, G1, acceptance criteria, or verified regression risk. GPT must not request narrative changes based on preference.

# A2 — Android Studio Narrative-Fidelity Review

**Owner:** Android Studio narrative workflow  
**Input:** approved A1 packet, integrated implementation build  
**Output:** narrative fidelity approval or implementation discrepancies

## A2.1 Narrative source integrity

- [ ] Dialogue text/key references match approved A1.
- [ ] Speaker identity, display name, role, and realm scope match A1.
- [ ] Quest title/description/objectives match A1.
- [ ] Player choices and ordering match A1.
- [ ] Lore/continuity meaning is unchanged.
- [ ] No runtime fallback displays contradictory story content.
- [ ] Localization/source-text exceptions match A1.

## A2.2 Player-flow fidelity

- [ ] Quest becomes available at the approved chapter/realm moment.
- [ ] Initial conversation begins as approved.
- [ ] Optional lore branch is available and returns correctly.
- [ ] Arena handoff occurs at the approved narrative beat.
- [ ] Success return/report sequence matches A1.
- [ ] Failure dialogue and recovery path match A1.
- [ ] Cancellation/abandonment matches A1.
- [ ] Completion timing matches A1.
- [ ] Post-completion narrative destination matches A1.

## A2.3 Consequence fidelity

- [ ] Affinity amount/target/timing matches A1.
- [ ] Gold amount/timing matches A1.
- [ ] Celestial Tear meaning/timing/retention matches A1.
- [ ] Quest completion meaning matches A1.
- [ ] Faction/world-state effects, if any, match A1.
- [ ] Repeatability/one-time intent is honored from the player’s perspective.

## A2.4 Recovery and continuity fidelity

- [ ] Mid-dialogue reload does not change narrative meaning.
- [ ] Arena interruption recovery matches approved meaning.
- [ ] Failure/retry preserves intended tone and progression.
- [ ] Reload after success/report does not replay contradictory dialogue.
- [ ] Completed quest remains narratively complete.
- [ ] Later Chapter 1/deferred content was not accidentally exposed.

## A2.5 A2 decision

```text
Decision: Narrative fidelity approved / Implementation discrepancy found / Creative change requested
Discrepancies tied to A1:
Creative change requiring user approval and A1 revision:
Files/data affected:
Next owner:
```

When implementation differs from A1, report an implementation issue. Do not silently edit runtime code or source narrative.

When creative intent changes, revise/approve A1 first, then revise G1, then request Codex changes.

# U1 — User Playtest and Milestone Acceptance

**Owner:** User  
**Input:** G2-approved implementation and A2-approved narrative build  
**Output:** accept, reject with classified issues, or approve with documented exception

## U1.1 Test identity

```text
Build/commit:
Platform/device:
Profile/save used:
New profile or migrated save:
Realm/context:
Date:
```

## U1.2 Start and readability

- [ ] OMEN_1 becomes available at the expected time.
- [ ] Quest title/description/objectives are understandable.
- [ ] Valerius/speaker presentation is coherent.
- [ ] Player knows what action starts the quest.
- [ ] Optional lore/choice branch is clear.
- [ ] Arena handoff action is clear and responsive.

## U1.3 Gameplay handoff

- [ ] Existing Champion arena loads successfully.
- [ ] Quest context is visibly or diagnostically correct.
- [ ] Free Champion mode remains usable outside the quest.
- [ ] Success returns to the intended narrative state.
- [ ] Failure returns to the intended recovery state.
- [ ] Cancel/back/interruption behavior is understandable.

## U1.4 Consequences

- [ ] Affinity effect feels narratively correct.
- [ ] Gold/reward timing feels correct.
- [ ] Celestial Tear behavior matches approved story meaning.
- [ ] Quest completion happens at the intended moment.
- [ ] No reward/consequence repeats after revisiting dialogue or reloading.

## U1.5 Save/reload scenarios

Test at least:

- [ ] before quest start,
- [ ] during dialogue,
- [ ] after acceptance before arena,
- [ ] after failure before retry,
- [ ] after success before report/completion,
- [ ] after completion.

For each:

```text
Save point:
Observed loaded state:
Expected state:
Duplicate/missing consequence:
Result:
```

## U1.6 Failure and recovery

- [ ] Arena defeat messaging makes sense.
- [ ] Retry action/path is clear.
- [ ] Quest cannot become silently stuck.
- [ ] Unavailable/missing content produces visible safe behavior.
- [ ] Existing unrelated gameplay remains operational.

## U1.7 User acceptance decision

Classify findings:

- **Blocker:** prevents milestone acceptance or risks data loss.
- **Required:** violates approved A1/G1 or core usability.
- **Follow-up:** valuable but not required for NVS-01 gate.
- **Deferred:** later phase or speculative expansion.

```text
Decision: Accept / Reject / Accept with documented exception
Blockers:
Required fixes:
Follow-ups:
Deferred ideas:
Creative observations:
```

User acceptance does not automatically authorize later-phase scope. New creative direction must enter the appropriate Android Studio packet/roadmap issue.

# Milestone Closeout

GPT may close NVS-01 only when:

- [ ] A1 clean narrative packet is merged.
- [ ] User narrative approval is recorded.
- [ ] G1 specification is merged.
- [ ] Codex implementation is merged in dependency order.
- [ ] G2 technical review passes.
- [ ] A2 narrative-fidelity review passes.
- [ ] U1 user acceptance passes.
- [ ] Android and Unity validation evidence is recorded on the integrated commit.
- [ ] All shared-file locks are released.
- [ ] No duplicate implementation PR remains open.
- [ ] Required issues are closed or explicitly deferred with owners.
- [ ] Final integrated commit is identified.
- [ ] Phase 1 status and risk register are updated.
- [ ] Next Phase 2 task is selected from the roadmap rather than self-assigned ad hoc.

## Closeout report

```text
Milestone: NVS-01
Final integrated commit:
A1 PR:
G1 PR:
Implementation PR(s):
G2 result:
A2 result:
U1 result:
Android validation:
Unity validation:
Save compatibility:
Shared locks released:
Closed issues:
Deferred issues:
Known residual risks:
Next roadmap phase/task:
```
