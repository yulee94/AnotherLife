# NVS-01 Review and Acceptance Checklists

Use these evidence-driven checklists after A1 and G1 are approved and Codex opens the NVS-01 implementation PR.

They define review ownership and milestone evidence. They do not change approved narrative intent or implementation requirements.

## Document control

```text
Milestone: NVS-01
User decision issue/comment: #138 / 
A1 issue/PR/commit: #128 / / 
G1 issue/PR/commit: #133 / / 
Codex issue/PR/commit: #134 / / 
Validated base/main commit:
Shared-file locks:
Current stage: G2 / A2 / U1 / Closeout
```

## Required implementation evidence package

Before G2 begins, the implementation PR must contain:

- exact A1 and G1 commits consumed,
- issue #138 D1–D16 approval reference,
- technical goal and bounded non-goals,
- changed files and why,
- required/optional/prohibited file declarations,
- shared-file locks,
- contract/schema/version decisions,
- loader/validator behavior,
- state/objective/dialogue/event implementation map,
- encounter request/result behavior,
- chapter/realm/location/start/report/artifact mapping,
- save/default/migration/D16 resume behavior,
- consequence ordering, atomicity, and idempotency,
- diagnostics and player-visible error behavior,
- exact compile/test/manual commands and results,
- known limitations and unperformed validation,
- rollback/data-safety notes,
- confirmation that narrative text, meaning, choices, and outcomes were not rewritten.

A compiling PR without this evidence is not review-ready.

# G2 — Codex Coordination/Review Technical and Integration Review

**Owner:** Codex coordination/review mode
**Inputs:** approved #138 decisions, approved A1, approved G1, Codex PR/evidence  
**Output:** approval, requirement-linked requested changes, or an explicit block

## G2.1 Dependency and scope

- [ ] Issue #138 D1–D16 approval is linked and unchanged.
- [ ] A1 PR/commit matches the approved packet.
- [ ] G1 PR/commit matches the implementation handoff.
- [ ] Codex branch uses the approved base.
- [ ] No parallel PR implements the same completion.
- [ ] Diff is one reviewable completion with no unrelated cleanup.
- [ ] Full Chapter 1, broad hooks/governance, #135 bridge work, and #137 save hardening remain out of scope unless explicitly approved.
- [ ] No Codex narrative/content source was rewritten by engineering without a source-mode update.

## G2.2 D1–D16 implementation traceability

For every decision, record the implementation and evidence.

| Decision | Approved meaning | Implementation location | Test/evidence | Result |
| --- | --- | --- | --- | --- |
| D1 — handoff | | | | |
| D2 — failure recovery | | | | |
| D3 — `FAILED` meaning | | | | |
| D4 — affinity | | | | |
| D5 — Gold/Tear timing | | | | |
| D6 — completion | | | | |
| D7 — localization policy | | | | |
| D8 — hook status | | | | |
| D9 — cancellation | | | | |
| D10 — chapter/realm placement | | | | |
| D11 — speaker scope | | | | |
| D12 — location/access/destination | | | | |
| D13 — Celestial Tear meaning | | | | |
| D14 — report interaction | | | | |
| D15 — start trigger | | | | |
| D16 — resume/interruption | | | | |

Any mismatch is a blocking fidelity defect, not a discretionary runtime choice.

## G2.3 Changed-file ownership

| File | Workstream owner | Required by G1? | Shared lock | Review result |
| --- | --- | --- | --- | --- |
| | | | | |

Checks:

- [ ] Runtime, test, tooling, and contract files are within Codex ownership.
- [ ] Generated files are deterministic and necessary.
- [ ] No dialogue, characterization, lore, choice, reward meaning, failure meaning, or chapter placement changed.
- [ ] Files outside G1’s impact plan have a reviewed justification.
- [ ] Shared files were declared before editing.
- [ ] Existing service registrations and unrelated save fields were preserved.

## G2.4 Source of truth and contract

- [ ] One authoritative OMEN_1 content source exists.
- [ ] Android, Unity, and external tools do not maintain conflicting authoritative copies.
- [ ] Contract/schema ID and version match G1.
- [ ] Required/optional fields match G1.
- [ ] Stable IDs match A1 exactly.
- [ ] Internal references resolve.
- [ ] External dependencies remain honestly requested or are verified with evidence.
- [ ] Shared/Fable records contain no `UnityEngine` types where applicable.
- [ ] Invalid/missing authoritative content does not silently fall back to unrelated story data.

## G2.5 Validation and error behavior

Verify automated evidence for:

- [ ] missing catalog,
- [ ] malformed catalog,
- [ ] unsupported version,
- [ ] duplicate IDs,
- [ ] missing internal reference,
- [ ] missing dialogue target or speaker,
- [ ] invalid terminal,
- [ ] missing state/objective,
- [ ] unreachable or invalid transition,
- [ ] unknown hook/location/event,
- [ ] unavailable dependency,
- [ ] invalid artifact/consequence target,
- [ ] corrupted or partial persisted state,
- [ ] duplicate, late, or mismatched encounter result.

For every failure:

- [ ] diagnostics are specific,
- [ ] player/runtime enters the G1-approved safe state,
- [ ] quest does not silently complete,
- [ ] chapter does not falsely advance,
- [ ] affinity/reward/artifact is not applied.

## G2.6 State, objective, and dialogue fidelity

| G1 transition/objective/node | Implementation | Test | Result |
| --- | --- | --- | --- |
| | | | |

- [ ] D15 start transition is deterministic.
- [ ] Optional lore/choice path is preserved.
- [ ] Every state is reachable only through approved paths.
- [ ] No extra narrative branch exists.
- [ ] D2/D3 terminal/transient/recovery semantics match.
- [ ] Invalid events are rejected visibly.
- [ ] Objective activation/completion matches A1/G1.
- [ ] Reserved terminal is distinct from a missing target.
- [ ] D14 report interaction requires the approved player action.
- [ ] Replayed dialogue does not duplicate consequences.

## G2.7 Encounter handoff and result

- [ ] Existing Champion arena is reused rather than duplicated.
- [ ] Quest launch carries approved quest/state/objective/hook/location/realm context.
- [ ] Free/non-quest Champion entry still works.
- [ ] D1 request behavior matches A1.
- [ ] Success maps to the approved event/state.
- [ ] D2/D3 failure maps to the approved recovery path.
- [ ] D9 cancel/unavailable behavior matches G1.
- [ ] Context survives required scene transitions.
- [ ] Duplicate request/result is idempotent.
- [ ] Late or mismatched results fail safely.
- [ ] Missing scene/hook/context is visible.
- [ ] Android `UnityView` is not represented as a completed bridge unless #135 is in scope.

## G2.8 Chapter, realm, speaker, location, and start

- [ ] D10 chapter/realm placement matches A1.
- [ ] D11 speaker/Valerius scope matches A1.
- [ ] D12 location presentation and access rules match A1.
- [ ] D15 start trigger is implemented once and tested.
- [ ] Saved chapter/context mutation is real and testable.
- [ ] Generic `AdvanceStory()` event emission is not mistaken for mutation.
- [ ] Legacy/invalid chapter IDs follow G1 migration/error rules.
- [ ] Post-completion destination/unlock matches D12.

## G2.9 Celestial Tear and report

- [ ] D13 acquisition/delivery/retention/consumption meaning is preserved.
- [ ] Artifact definition lookup is stable and validated.
- [ ] Ownership or kingdom/story representation is persisted when required.
- [ ] Equipment/boss-loot models are reused only with G1 justification.
- [ ] D14 manual/automatic/custom report behavior matches A1.
- [ ] Objective wording agrees with D13 and D14.
- [ ] Missing artifact definition fails visibly without false completion.
- [ ] Duplicate acquisition/delivery is prevented.

## G2.10 Persistence, defaults, migration, and D16

- [ ] Persisted fields match G1.
- [ ] Old-save defaults are present.
- [ ] #136 or equivalent normalization is complete before affinity/faction/persona effects.
- [ ] Existing non-null values are preserved.
- [ ] Mid-dialogue reload matches D16.
- [ ] Pre-handoff reload matches D16.
- [ ] Mid-handoff and mid-arena interruption match D16.
- [ ] Failure/retry reload matches D2/D3/D16.
- [ ] Success-before-report reload matches D5/D6/D14/D16.
- [ ] During-report reload matches D14/D16.
- [ ] Partial-consequence reload recovers deterministically.
- [ ] Completed-state reload remains completed.
- [ ] Corrupted/partial state has visible safe behavior.

## G2.11 Consequence atomicity and idempotency

| Consequence | Approved trigger | Idempotency key | Fault/reload tests | Result |
| --- | --- | --- | --- | --- |
| Affinity | | | | |
| Gold/resource | | | | |
| Celestial Tear | | | | |
| Completion | | | | |
| Chapter/unlock | | | | |
| Faction/world state, if approved | | | | |

- [ ] Operation order matches D4–D6, D13, and D14.
- [ ] Duplicate success result applies nothing twice.
- [ ] Dialogue replay applies nothing twice.
- [ ] Retry applies nothing twice.
- [ ] Fault injection after every boundary recovers deterministically.
- [ ] Applied-effect ledger and actual service state agree.
- [ ] One-time effects stay one-time after reload.
- [ ] Repeatable effects repeat only under approved conditions.

## G2.12 Regression evidence

Required exact evidence:

- [ ] Unity batch import/C# compile.
- [ ] EditMode totals/results.
- [ ] PlayMode totals/results.
- [ ] Representative scene smoke.
- [ ] Free Champion path.
- [ ] NVS-01 happy path.
- [ ] Optional branch path.
- [ ] Failure/retry path.
- [ ] Cancellation/unavailable path when applicable.
- [ ] D16 reload/resume matrix.
- [ ] Invalid-data matrix.
- [ ] Duplicate/fault-injection matrix.
- [ ] Android unit tests.
- [ ] Android debug assembly.
- [ ] Schema/Fable validation when applicable.
- [ ] Exact tested branch and SHA.
- [ ] Final diff/worktree cleanliness.

## G2 decision

```text
Result: Approved / Changes requested / Blocked
Requirement/evidence references:
Shared locks released?:
Handoff to A2:
```

# A2 — Codex Narrative/Content Fidelity Review

**Owner:** Codex narrative/content mode
**Inputs:** issue #138, approved A1/G1, implemented runtime build  
**Output:** fidelity approval or precise narrative mismatch report

## A2.1 Decision fidelity

- [ ] D1–D16 each match the approved issue #138 answer.
- [ ] No runtime wording changes the approved tone or meaning.
- [ ] No speaker substitution or realm scope was invented.
- [ ] No reward, artifact, failure, report, completion, or resume meaning drifted.

Use the same D1–D16 table from G2 and record `match/mismatch/not testable` with evidence.

## A2.2 Narrative content

- [ ] Quest title and description match A1.
- [ ] Objective text and ordering match A1.
- [ ] Speaker IDs/display names match A1.
- [ ] Dialogue lines and choices match A1.
- [ ] Optional lore branch remains optional and coherent.
- [ ] Failure/retry text matches the approved recovery meaning.
- [ ] Celestial Tear wording agrees with D13.
- [ ] Report wording agrees with D14.
- [ ] Post-completion continuity agrees with D12.

## A2.3 State and consequence meaning

- [ ] Player-visible state sequence matches A1.
- [ ] Failure/retry/cancellation meaning matches D2/D3/D9.
- [ ] Affinity, Gold, Tear, and completion occur at approved narrative moments.
- [ ] No consequence is presented twice.
- [ ] Reload/resume presentation matches D16.
- [ ] Technical errors do not masquerade as narrative outcomes.

## A2.4 Localization/source text

- [ ] D7 policy is followed.
- [ ] Keys match A1.
- [ ] Approved source-text exceptions are the only embedded player-facing text.
- [ ] Missing text/key is visible as a defect rather than silently replaced.

## A2 decision

```text
Result: Approved / Changes requested / Blocked
Narrative mismatches:
Evidence:
Handoff to U1:
```

# U1 — User Playtest and Acceptance

**Owner:** User  
**Inputs:** G2-approved build, A2 fidelity approval, test instructions  
**Output:** accept NVS-01, reject with observed defect, or defer

## U1.1 Test setup

Record:

```text
Build/branch/commit:
Platform/device:
Save fixture: new / old / migrated
Realm/context:
Entry path:
Known limitations disclosed:
```

## U1.2 Core playtest

- [ ] D15 quest start is understandable and works.
- [ ] Initial advisor interaction is correct.
- [ ] Optional lore choice behaves as expected.
- [ ] D1 handoff is clear and enters the intended encounter.
- [ ] Arena can succeed and return correctly.
- [ ] D2/D3 failure and retry feel coherent.
- [ ] D9 cancellation/unavailable behavior is acceptable when applicable.
- [ ] D14 report interaction requires the intended action.
- [ ] D13 Celestial Tear meaning is clear.
- [ ] D4–D6 affinity/reward/completion occur at the intended moments.
- [ ] D12 post-completion destination is correct.

## U1.3 Reload and duplicate-safety playtest

Test practical D16 points:

- [ ] close/reload during dialogue,
- [ ] close/reload before handoff,
- [ ] close/reload during encounter or its supported recovery point,
- [ ] close/reload after failure,
- [ ] close/reload after success before report,
- [ ] close/reload during/after report,
- [ ] reload completed quest.

Confirm:

- [ ] no duplicate affinity,
- [ ] no duplicate Gold,
- [ ] no duplicate Tear,
- [ ] no duplicate completion/unlock,
- [ ] no lost approved progress,
- [ ] no false success after an error.

## U1.4 Presentation and clarity

- [ ] Quest purpose is understandable.
- [ ] Speaker/realm/location context is coherent.
- [ ] Dialogue choices communicate their effect.
- [ ] Failure and retry instructions are clear.
- [ ] Reward/artifact presentation matches the approved meaning.
- [ ] Error/unavailable states are understandable.
- [ ] Localization/source text is acceptable for the milestone.

## U1 decision

```text
Result: Accepted / Rejected / Deferred
Observed defects:
Narrative concerns:
Technical concerns:
Required follow-up issues:
Acceptance date/reference:
```

# Final milestone closeout

NVS-01 may close only when:

- [ ] #138 D1–D16 approval is recorded.
- [ ] A1 and G1 are approved and merged.
- [ ] Codex implementation is merged.
- [ ] G2 is approved.
- [ ] A2 is approved.
- [ ] U1 is accepted.
- [ ] All shared locks are released.
- [ ] Required save/default/migration evidence is recorded.
- [ ] No known Critical/High unaccepted defect remains.
- [ ] Deferred work has focused issues and owners.
- [ ] Phase status and risk register are updated.
- [ ] Draft PR #124 is closed as superseded only after preservation is confirmed.

## Closeout report

```text
Milestone: NVS-01
Approved #138 decision reference:
A1 PR/commit:
G1 PR/commit:
Implementation PR/commit:
G2 result:
A2 result:
U1 result:
Tests/builds:
Save compatibility:
Shared locks:
Closed risks:
Accepted risks:
Deferred issues:
Next roadmap phase/task:
```
