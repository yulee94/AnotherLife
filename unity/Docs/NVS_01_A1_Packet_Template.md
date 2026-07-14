# NVS-01 A1 Narrative Packet Template

Use this template for the Android Studio-owned A1 deliverable tracked by issue #128.

This file defines structure and review requirements only. It does not author dialogue, select creative outcomes, or define runtime implementation. Replace every placeholder with user-approved narrative intent before A1 review.

## Document control

```text
Milestone: NVS-01
Task: A1
Quest candidate: OMEN_1
Packet version:
Android Studio branch:
Android Studio commit:
Upstream main commit:
User approval reference:
Narrative owner:
GPT review status:
```

## 1. Purpose and bounded scope

### Player-facing purpose

```text
<What emotional, strategic, or tutorial purpose does this one quest serve?>
```

### Included content

```text
<Exactly what is part of OMEN_1?>
```

### Explicit non-goals

```text
<What is deliberately deferred: full Chapter 1, broad realm hooks, runtime implementation, etc.?>
```

### Definition of narrative completion

```text
<What must the player have experienced or understood when the quest concludes?>
```

## 2. User-approved decision record

Record the exact approved option or custom decision. Do not leave a decision implicit.

| Decision | Approved answer | Approval reference | Notes |
| --- | --- | --- | --- |
| D1 — dialogue-to-arena handoff | | | |
| D2 — arena failure recovery | | | |
| D3 — `FAILED` state meaning | | | |
| D4 — Valerius affinity timing/repeatability | | | |
| D5 — Gold/Tear reward timing/repeatability | | | |
| D6 — quest completion timing | | | |
| D7 — localization/source-text policy | | | |
| D8 — gameplay hook status | | | |
| D9 — cancellation/abandonment | | | |
| D10 — chapter/realm placement | | | |
| D11 — Valerius role and speaker scope | | | |
| D12 — realm/location prerequisites and post-completion destination | | | |

## 3. Source-of-truth declaration

### Authoritative packet file(s)

```text
<List only the narrative-owned files that define this packet.>
```

### Existing source text reused

```text
<List intentionally preserved dialogue, objective text, title, lore, or continuity notes from draft PR #124.>
```

### Intentional corrections from the archived draft

```text
<List each correction and tie it to D1–D12 or a reference-integrity defect.>
```

### Deferred archived content

```text
Chapter 1 ideas: issue #129
Cross-system narrative hooks: issue #130
Governance/localization/templates: issue #131
Android↔Unity bridge: issue #135
```

## 4. Stable ID inventory

IDs must be stable, nonblank, and unique within their declared meaning. Use the approved ID exactly; do not create aliases without documenting compatibility.

### Milestone

| ID | Meaning | New/reused |
| --- | --- | --- |
| `NVS-01` | | |

### Chapter/context

| ID | Meaning | New/reused | External mapping required? |
| --- | --- | --- | --- |
| | | | |

### Quest

| ID | Meaning | New/reused |
| --- | --- | --- |
| `OMEN_1` | | |

### Objectives

| ID | Player-facing purpose | Activation state/event | Completion state/event |
| --- | --- | --- | --- |
| | | | |

### Dialogue nodes

| ID | Stable speaker ID | Purpose | Internal next targets | Terminal/handoff behavior |
| --- | --- | --- | --- | --- |
| | | | | |

### NPC/advisor

| ID | Display/localization reference | Canonical role | Realm scope |
| --- | --- | --- | --- |
| | | | |

### Reward/artifact

| ID | Narrative meaning | Acquired/consumed/retained | Trigger | One-time/repeatable |
| --- | --- | --- | --- | --- |
| | | | | |

### Gameplay hook

| ID | Requested or verified | Narrative meaning | Evidence when verified |
| --- | --- | --- | --- |
| | | | |

### External location dependency

| ID | Requested or verified | Narrative meaning | Realm/access scope | Evidence when verified |
| --- | --- | --- | --- | --- |
| | | | | |

### Return events

| ID | Success/failure/cancel | Narrative meaning | Expected source | Expected destination |
| --- | --- | --- | --- | --- |
| | | | | |

### Localization keys

| Key | Field | Source text reference | Exception? |
| --- | --- | --- | --- |
| | | | |

## 5. Entry, prerequisites, and unlock

### Realm-selection relationship

```text
<Before realm selection / after realm selection / realm-specific / realm-adapted.>
```

### Required chapter/context

```text
<Approved narrative chapter/context ID and meaning.>
```

### Prerequisites

Use an empty list or explicit prose for no prerequisites. Do not use a literal `NONE` ID unless it is an approved reserved value.

```text
<Prerequisites.>
```

### Authoritative unlock event

```text
<What player-facing event makes OMEN_1 available?>
```

### Initial state and first objective

```text
<Initial state and why the first objective becomes active.>
```

### Post-completion narrative destination

```text
<Exact next narrative state/chapter/unlock, or explicit statement that a later packet owns the transition.>
```

## 6. State definitions

Every state must have a player-facing meaning, entry condition, and allowed exit. Remove unused states.

| State | Player-facing meaning | Entry trigger(s) | Allowed exit trigger(s) | Terminal? | Persist/resume meaning |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

### Reserved terminal dialogue target

```text
<Define `end` as an approved reserved terminator, or provide the stable terminal node/action used instead.>
```

## 7. State-transition table

This table expresses narrative intent. It does not prescribe C# classes, event-bus technology, save fields, or scene architecture.

| Current state | Narrative trigger/event | Preconditions | Next state | Objective effect | Dialogue/action | Consequence intent | Invalid-trigger meaning |
| --- | --- | --- | --- | --- | --- | --- | --- |
| | | | | | | | |

Required paths:

- quest unlock/start,
- first Valerius interaction,
- optional lore/choice branch,
- arena handoff request,
- arena success return,
- arena failure return,
- retry/recovery,
- report interaction,
- reward/consequence moment,
- completion,
- cancellation/abandonment when allowed,
- load/resume meaning.

## 8. Objective progression

| Objective ID | Becomes active when | Progress meaning | Completes when | Failure/retry effect | Next objective/state |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

### Report-objective clarification

```text
<Does the player manually return to Valerius, or is the report dialogue automatic after arena return?>
```

### Celestial Tear clarification

```text
<Is the Tear acquired in the arena, delivered away, retained as an artifact, converted to a research specimen, consumed, or represented another way?>
```

## 9. Dialogue and choices

Use stable speaker IDs separately from player-facing display names.

### Dialogue node template

```text
Node ID:
Speaker stable ID:
Display/localization name key:
Text key or approved source-text exception:
Narrative purpose:
Choices:
  - Choice key/source text:
    Internal next node / reserved terminal / semantic handoff:
    Consequence intent:
```

### Reference rules

- Every internal next-node ID must resolve.
- Only the approved reserved terminal may close without a node lookup.
- External semantic actions must be declared as external, not disguised as dialogue node IDs.
- A missing non-terminal target is invalid data.

## 10. Gameplay handoff request

### Handoff ID

```text
<Approved semantic hook ID.>
```

### Status

```text
Requested/unimplemented OR verified existing capability with evidence.
```

### Narrative request meaning

```text
<What encounter should the player enter and why?>
```

### Context required by narrative

```text
<Quest ID, objective ID, location, advisor, realm, or other approved context. Do not define a runtime serialization format.>
```

### Success return meaning

```text
<What does success mean to the story and which state/objective follows?>
```

### Failure return meaning

```text
<What does failure mean to the story and which recovery path follows?>
```

### Cancel/interruption meaning

```text
<What should happen narratively if the player exits or the encounter cannot start?>
```

## 11. Consequence intent and repeatability

The packet owns narrative intent. G1 owns technical idempotency and persistence.

| Consequence | Stable target ID | Authoritative trigger | One-time/repeatable | Retry behavior | Reload behavior | Narrative notes |
| --- | --- | --- | --- | --- | --- | --- |
| Affinity | | | | | | |
| Gold/resource | | | | | | |
| Artifact/reward | | | | | | |
| Quest completion | | | | | | |
| Faction/reputation, if approved | | | | | | |
| World state, if approved | | | | | | |

No consequence may have two conflicting authoritative triggers.

## 12. Failure, retry, cancellation, and recovery

### Failure sequence

```text
<Exact player-visible steps from encounter failure to the next playable state.>
```

### `FAILED` state

```text
<Terminal / transient / separately reached / removed. Explain entry and exit.>
```

### Retry sequence

```text
<What action does the player take, and which state/objective is active?>
```

### Cancellation/abandonment

```text
<Not allowed, or exact abandonment/reacceptance behavior.>
```

### Mid-dialogue resume intent

```text
<Restart current node / resume exact node / another approved behavior.>
```

### Close during handoff/arena intent

```text
<Return to pre-handoff state / recovery state / another approved behavior.>
```

### Close after success before report intent

```text
<What state and objective should the player see after reload?>
```

### Close after consequence application intent

```text
<How should the player experience already-applied one-time consequences?>
```

## 13. Localization and source-text policy

### Approved policy

```text
D7-A full keys / D7-B milestone exception / D7-C custom.
```

### Key pattern

```text
<Approved key convention for this packet.>
```

### Player-facing field coverage

| Field | Stable key | Approved source text location | Exception reason, if any |
| --- | --- | --- | --- |
| Quest title | | | |
| Quest description | | | |
| Objective text | | | |
| Speaker display name | | | |
| Dialogue lines | | | |
| Choice text | | | |
| Failure/retry text | | | |
| Reward/artifact display/lore | | | |

A1 defines keys and authoring policy. It does not claim a Unity/Android localization runtime already exists.

## 14. External semantic dependencies

List every required capability or ID not implemented by the packet.

| Dependency ID | Type | Requested behavior | Existing evidence | Current status | Downstream owner |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

Examples may include:

- encounter hook,
- success/failure return event,
- Sky Castle location/marker,
- artifact ownership,
- chapter-ID mapping,
- Android preview consumption.

Do not mark a dependency resolved merely because its proposed ID exists.

## 15. Continuity and cross-packet notes

```text
<What does OMEN_1 establish for later content?>
```

```text
<What later content must not be assumed or implemented in A1?>
```

```text
<How does this packet relate to deferred issues #129, #130, #131, and #135?>
```

## 16. Changed files and ownership

| File | Why required | Narrative-owned? | Runtime/model impact? |
| --- | --- | --- | --- |
| | | | |

Required declaration:

```text
Unity runtime files changed: no
Android runtime-model files changed: no
Save infrastructure changed: no
Service registration changed: no
Shared integration files changed: none
Full Chapter 1/later-phase scope included: no
```

## 17. Packet validation

### Automated checks

Record the focused test names/results for:

- ID uniqueness,
- dialogue target resolution,
- reserved terminal handling,
- state/transition integrity,
- objective activation/completion integrity,
- consequence declaration integrity,
- external dependency status,
- localization coverage/exceptions,
- negative missing-reference or invalid-transition rejection.

### Android build validation

```powershell
$repo = "D:\260711\MY\AndroidStudioProjects\AnotherLife"
$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
$env:ANDROID_HOME = "C:\Users\MY\AppData\Local\Android\Sdk"
$env:Path = "$env:JAVA_HOME\bin;$env:Path"

& "$repo\gradlew.bat" `
    -p $repo `
    :app:testDebugUnitTest `
    :app:assembleDebug
```

```text
Exact result:
Packet test count/result:
Warnings relevant to A1:
```

### Branch identity

```text
Branch:
HEAD:
origin/main:
Ahead/behind:
Working tree clean:
```

## 18. Android Studio completion report

```text
Narrative scope completed:
User-approved decisions:
IDs added/reused:
References validated:
State/branch/recovery paths validated:
Consequence timing/repeatability intent:
Semantic gameplay dependencies requested:
Localization coverage/exceptions:
Archived content preserved/deferred:
Files changed:
Validation commands/results:
Unresolved creative decisions:
PR/dependency status:
```

## 19. Exact handoff request for GPT

```text
Review this A1 packet for completeness, ownership, reference integrity, internal consistency, and narrative/runtime separation.

Do not rewrite approved narrative intent.

If A1 and the user approval record are complete, activate issue #133 and produce G1 with the state machine, event map, contract/schema requirements, persistence/resume semantics, consequence idempotency, validation/error behavior, file impacts, shared-file locks, tests, and merge order.
```

## 20. A1 readiness checklist

- [ ] User decisions D1–D12 are recorded.
- [ ] Exactly one bounded quest packet is present.
- [ ] Every internal ID/reference resolves.
- [ ] Reserved terminal behavior is explicit.
- [ ] Entry/unlock and post-completion destination are explicit.
- [ ] Every state has entry/exit meaning.
- [ ] Every objective has activation/completion meaning.
- [ ] Handoff request and success/failure return meanings are explicit.
- [ ] Failure/retry/cancellation/recovery are non-contradictory.
- [ ] Artifact/reward narrative meaning is coherent.
- [ ] Every consequence trigger and repeatability intent is explicit.
- [ ] Localization coverage or approved exceptions are complete.
- [ ] External dependencies are marked requested or verified with evidence.
- [ ] No Unity runtime-owned file changed.
- [ ] No Android runtime-model/navigation/Gradle file changed.
- [ ] No save/service/shared contract file changed.
- [ ] No full Chapter 1 or later-phase expansion is included.
- [ ] Focused packet tests pass, including a negative test.
- [ ] Android unit tests and debug assembly pass.
- [ ] Branch identity and exact results are reported.
- [ ] Android Studio completion report is complete.
- [ ] Exact GPT handoff request is present.
