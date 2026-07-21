# NVS-01 A1 Narrative Packet Template

Use this template for the Codex narrative/content A1 deliverable tracked by issue #128.

This document defines structure, evidence, and ownership requirements only. It does not author dialogue, choose creative outcomes, or prescribe runtime implementation. Every placeholder must be replaced with user-approved narrative intent before A1 review.

## Document control

```text
Milestone: NVS-01
Task: A1
Quest: OMEN_1 / The First Signal
Packet version:
Codex narrative branch: codex/narrative-nvs-01-a1-clean
Codex narrative commit:
Upstream main commit:
User decision issue: #138
User approval comment/reference:
Narrative owner:
Codex coordination/review status: Blocked / Ready / Approved
```

## 1. Approval gate

A1 is not complete until all of the following are true:

- [ ] Issue #138 contains an explicit, internally consistent user answer for D1–D16.
- [ ] This packet records those answers exactly; it does not reinterpret them.
- [ ] The branch started from fetched current `main`.
- [ ] The packet contains exactly one bounded `OMEN_1` quest.
- [ ] No Unity runtime-owned service, interface, scene, save, or contract file changed.
- [ ] No Android runtime model, navigation, Gradle, or unrelated UI file changed.
- [ ] All internal IDs and references resolve.
- [ ] External semantic dependencies are clearly marked requested or verified.
- [ ] Relevant validation passes on the clean branch, with Android unit/debug validation included when Android files or generated Android-facing outputs change.

If any approval or reference is missing, leave the packet blocked and list the exact deficiency. Do not ask Codex engineering to invent the answer.

## 2. User-approved decision record

Copy the approved answer from issue #138 and link the exact approval comment. Custom answers are allowed.

| Decision | Approved answer | Approval reference | Packet sections affected |
| --- | --- | --- | --- |
| D1 — dialogue-to-arena handoff | | | dialogue, transition, handoff |
| D2 — arena failure recovery | | | state, objective, retry |
| D3 — `FAILED` state meaning | | | state and recovery |
| D4 — Valerius affinity trigger and repeatability | | | consequence intent |
| D5 — Gold and Celestial Tear timing/repeatability | | | reward and consequence intent |
| D6 — quest completion timing | | | objective and terminal transition |
| D7 — localization/source-text policy | | | localization inventory |
| D8 — gameplay-hook status | | | external dependency declaration |
| D9 — cancellation/abandonment | | | cancellation and cleanup |
| D10 — chapter/realm placement | | | entry and continuity |
| D11 — Valerius role and speaker scope | | | speaker and realm scope |
| D12 — location presentation, realm prerequisites, post-completion destination | | | location, entry, completion |
| D13 — Celestial Tear acquisition/delivery/retention meaning | | | objective, artifact, consequence |
| D14 — report interaction | | | report objective and dialogue |
| D15 — quest-start trigger | | | unlock and first transition |
| D16 — dialogue/arena/success resume intent | | | persistence and recovery meaning |

### Consistency assertions

Record `pass` or explain the contradiction:

```text
D2 agrees with D3:
D4–D6 agree on consequence and completion order:
D10 agrees with D11 and D12:
D13 agrees with D5 and report wording:
D14 agrees with the final objective and D6:
D15 produces one deterministic initial transition:
D16 agrees with D2/D3/D5/D6/D14:
```

## 3. Purpose and bounded scope

### Player-facing purpose

```text
<What emotional, strategic, tutorial, or continuity purpose does this one quest serve?>
```

### Included content

```text
<Exactly which OMEN_1 states, objectives, dialogue nodes, choices, consequences, and semantic requests are included?>
```

### Explicit non-goals

At minimum, confirm that this packet does not implement:

- the complete Chapter 1 four-realm spine,
- broad realm/building/research/world-atlas hooks,
- global ID governance or authoring tooling,
- Unity quest/runtime services,
- save infrastructure,
- an Android↔Unity embedded bridge,
- unrelated Android model/navigation/UI work.

### Narrative definition of done

```text
<What must the player have experienced, learned, obtained, delivered, retained, or unlocked when OMEN_1 ends?>
```

## 4. Source-of-truth declaration

### Authoritative packet files

```text
<List only narrative-owned files in the clean A1 PR.>
```

### User approval source

```text
Issue #138 comment/reference:
Approved D1–D16 snapshot date:
```

### Archived material intentionally reused

```text
<List preserved title, lore, dialogue, objective, or consequence wording from draft PR #124.>
```

### Corrections from the archive

| Correction | Reason | Decision/reference |
| --- | --- | --- |
| | | |

### Deferred archive disposition

```text
Chapter 1 four-realm spine: #129
Realm/building/dossier/world hooks: #130
ID/localization/authoring governance: #131
Android↔Unity runtime bridge: #135
Crash-safe save hardening: #137
Draft PR #124: archive only; never merge
```

## 5. Stable ID inventory

IDs must be stable, nonblank, unique for their meaning, and listed once. A proposed ID does not prove a runtime capability exists.

### Milestone, context, and quest

| Category | ID | Meaning | New/reused | External mapping required? |
| --- | --- | --- | --- | --- |
| Milestone | `NVS-01` | | | |
| Chapter/context | | | | |
| Quest | `OMEN_1` | | | |
| Location, if approved | | | | |

### Objectives

| ID | Player-facing purpose | Activates when | Completes when | Failure/retry effect |
| --- | --- | --- | --- | --- |
| | | | | |

### States

| ID | Player-facing meaning | Entry trigger | Allowed exit triggers | Terminal/transient/recovery |
| --- | --- | --- | --- | --- |
| | | | | |

### Dialogue nodes

| ID | Stable speaker ID | Purpose | Internal next targets | Terminal or semantic action |
| --- | --- | --- | --- | --- |
| | | | | |

### NPC/advisor

| ID | Display/localization key | Canonical role | Realm scope | Existing/new |
| --- | --- | --- | --- | --- |
| | | | | |

### Reward/artifact

| ID | Narrative meaning | Acquired/delivered/retained/consumed | Trigger | One-time/repeatable |
| --- | --- | --- | --- | --- |
| | | | | |

### Gameplay hook and return events

| ID | Type | Requested or verified | Narrative meaning | Expected source/destination | Verification evidence |
| --- | --- | --- | --- | --- | --- |
| | Hook / success / failure / cancel | | | | |

### Localization keys

| Key | Field | Source-text location | Exception approved? |
| --- | --- | --- | --- |
| | | | |

## 6. Entry, placement, and unlock

Translate D10–D12 and D15 without adding new intent.

```text
Realm-selection relationship:
Approved chapter/context:
Eligible realms:
Canonical speaker for each eligible realm:
Location presentation:
Location access prerequisites:
Authoritative quest-start trigger:
Initial state:
First active objective:
Post-completion narrative destination or explicitly deferred unlock:
```

### Start-transition proof

| Starting condition | Trigger | Resulting state | Active objective | Dialogue/action |
| --- | --- | --- | --- | --- |
| | | | | |

There must be one deterministic start path. `INACTIVE` may not remain disconnected.

## 7. State definitions and transition table

Every state requires a player-facing meaning, entry rule, exit rule, and D16 resume meaning. Remove unused states.

| State | Player-facing meaning | Entry event | Allowed exit events | Persist/resume meaning | Terminal? |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

### Reserved dialogue terminal

```text
<Define `end` as an approved reserved terminal, or define another explicit terminal action. A missing arbitrary node is never a terminal.>
```

### Complete transition table

| Current state | Narrative event | Preconditions | Next state | Objective updates | Dialogue/action | Consequence intent | Invalid-event meaning |
| --- | --- | --- | --- | --- | --- | --- | --- |
| | | | | | | | |

The table must cover:

- D15 unlock/start,
- first advisor interaction,
- optional lore branch,
- D1 arena handoff,
- arena success,
- D2/D3 failure and recovery,
- retry,
- D14 report interaction,
- D4/D5 consequence points,
- D6 completion,
- D9 cancellation when allowed,
- every D16 load/resume case.

## 8. Objective progression

| Objective ID | Becomes active when | Player progress meaning | Completes when | Failure/retry behavior | Next state/objective |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

### Report objective

```text
<Manual return, automatic dialogue, or custom D14 behavior. State the exact player action.>
```

### Celestial Tear objective wording

```text
<Make the objective agree with D13: obtain, deliver, retain, transfer to kingdom research, consume, or another approved meaning.>
```

## 9. Dialogue and choices

Use stable speaker IDs separately from display names.

For every node:

```text
Node ID:
Speaker stable ID:
Speaker display/localization key:
Text key or approved source-text exception:
Narrative purpose:
Choices:
  - Choice key/source text:
    Target: internal node / reserved terminal / semantic action
    Consequence intent:
```

Reference rules:

- every internal node target resolves,
- every speaker ID resolves,
- only the declared terminal may close without lookup,
- semantic handoff actions are not disguised as dialogue IDs,
- missing non-terminal targets are invalid packet data,
- D1 and D14 behavior is explicit.

## 10. Gameplay handoff and return meaning

```text
Hook ID:
Status: requested/unimplemented OR verified with exact evidence
Narrative encounter meaning:
Required narrative context:
Expected success event:
Expected failure event:
Expected cancel/unavailable event:
Success destination state/objective:
Failure destination state/objective:
Cancel/interruption destination:
```

A1 defines narrative meaning only. It does not select event-bus technology, C# classes, serialization shape, scene architecture, or Android embedding.

## 11. Consequence intent and ordering

| Consequence | Stable target ID | Authoritative trigger | One-time/repeatable | Retry behavior | Reload behavior | Narrative notes |
| --- | --- | --- | --- | --- | --- | --- |
| Valerius affinity | | | | | | |
| Gold/resource | | | | | | |
| Celestial Tear/artifact | | | | | | |
| Quest completion | `OMEN_1` | | | | | |
| Faction/world state, if approved | | | | | | |

### Narrative ordering

```text
<Describe the approved order of success, artifact acquisition/delivery, report interaction, reward, affinity, and completion.>
```

No consequence may have two authoritative triggers. A1 owns intent; G1 owns technical atomicity, persistence, and idempotency.

## 12. Failure, cancellation, and resume intent

Translate D2, D3, D9, and D16 exactly.

```text
Encounter failure sequence:
FAILED-state meaning and exit:
Retry player action:
Retry active state/objective:
Cancellation/abandonment allowed?:
Cancellation cleanup and reacceptance:
Mid-dialogue close/reload:
Close during handoff:
Close during arena:
Close after success before report:
Close after report before consequence application:
Close after one-time consequences:
Completed-state reload:
```

The packet must not claim exact-runtime resume when the underlying capability is only requested. In that case, state the desired narrative outcome and mark the technical dependency external.

## 13. Localization and source-text policy

```text
Approved D7 option:
Key convention:
Runtime localization status: requested / verified
```

| Field | Stable key | Approved source text location | Exception and approval |
| --- | --- | --- | --- |
| Quest title | | | |
| Quest description | | | |
| Objective text | | | |
| Speaker name | | | |
| Dialogue line | | | |
| Choice text | | | |
| Failure/retry text | | | |
| Reward/artifact display and lore | | | |

A1 may define keys without claiming a Unity or Android localization runtime already exists.

## 14. External semantic dependencies

List every capability not implemented by this narrative packet.

| Dependency ID | Type | Requested behavior | Existing evidence | Status | Downstream owner |
| --- | --- | --- | --- | --- | --- |
| | | | | requested/verified | |

Potential entries include:

- named encounter handoff,
- typed success/failure/cancel result,
- Sky Castle location or marker,
- artifact ownership/consumption model,
- persisted chapter mapping,
- dialogue/localization runtime,
- Android preview consumption,
- Android↔Unity bridge.

## 15. Continuity and deferred content

```text
What OMEN_1 establishes for later content:
What later content must not be assumed or implemented in A1:
Relationship to #129, #130, #131, #135, and #137:
Compatibility with existing fallback content:
```

## 16. Changed files and ownership

| File | Why required | Narrative-owned? | Runtime/model impact | Review result |
| --- | --- | --- | --- | --- |
| | | yes/no | none required for A1 | |

Prohibited in A1 include, at minimum:

- `app/src/main/java/com/example/anotherlife/data/simulation/KingdomModels.kt`,
- Android navigation or Gradle files,
- Unity `IStoryService`, `IQuestService`, definitions, local services, scenes, or save files,
- the four designated shared integration files,
- broad Chapter 1/governance/hook files from draft PR #124.

## 17. Packet validation

### Required reference checks

- [ ] IDs are nonblank and unique.
- [ ] Every internal dialogue target resolves.
- [ ] Every objective/state/transition reference resolves.
- [ ] Every speaker/reward/hook/event/location reference is internal or explicitly external.
- [ ] Every state is reachable or intentionally terminal.
- [ ] D1–D16 are represented in the relevant tables.
- [ ] D2/D3, D4–D6, D10–D12, and D13–D16 are internally consistent.
- [ ] No consequence has conflicting triggers.
- [ ] Requested capabilities are not labeled implemented.

### Required packet tests

| Test | Expected result | Evidence |
| --- | --- | --- |
| Duplicate ID fixture | rejected | |
| Missing dialogue target | rejected | |
| Invalid state target | rejected | |
| Missing objective | rejected | |
| Invalid hook/location reference classification | rejected or marked external | |
| Unreachable state | rejected | |
| Conflicting consequence trigger | rejected | |
| Missing D1–D16 approval | packet remains blocked | |
| Valid OMEN_1 packet | accepted | |

Do not add a broad new authoring framework merely to satisfy A1 tests.

## 18. Branch and Android validation

```powershell
$repo = "C:\Users\MY\Documents\AnotherLife"
$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
$env:ANDROID_HOME = "C:\Users\MY\AppData\Local\Android\Sdk"
$env:Path = "$env:JAVA_HOME\bin;$env:Path"

git -C $repo status -sb
git -C $repo rev-parse HEAD
git -C $repo rev-parse origin/main

& "$repo\gradlew.bat" `
    -p $repo `
    :app:testDebugUnitTest `
    :app:assembleDebug
```

Record exact command output, exit codes, branch SHA, and `origin/main` SHA. A successful historical build on draft PR #124 is not evidence for the clean A1 branch.

## 19. Codex narrative/content completion report

```text
Narrative scope completed:
User approval reference for D1–D16:
Approved decisions encoded without reinterpretation:
IDs added/reused:
Internal references validated:
External semantic dependencies:
State/objective/dialogue paths validated:
Consequence ordering and repeatability:
Failure/cancellation/resume intent:
Localization coverage/exceptions:
Files changed:
Prohibited areas confirmed untouched:
Validation commands and results:
Known limitations:
Unresolved creative decisions:
Exact handoff request for Codex coordination/review:
```

## 20. A1 acceptance checklist

- [ ] Exactly one bounded `OMEN_1` packet is present.
- [ ] Issue #138 D1–D16 approval is linked.
- [ ] All D1–D16 decisions are encoded consistently.
- [ ] All stable IDs are listed and unique.
- [ ] Every internal reference resolves.
- [ ] States and objectives are complete and deterministic.
- [ ] Dialogue, choices, handoff, report, reward, artifact, and completion meaning agree.
- [ ] Failure, retry, cancellation, and resume intent are explicit.
- [ ] External dependencies are honest.
- [ ] Localization coverage or approved exceptions are complete.
- [ ] No runtime-owned or unrelated file changed.
- [ ] Relevant validation passes.
- [ ] Completion report and exact Codex coordination/review handoff are present.

## 21. Handoff to Codex coordination/review

When every item above passes, request:

```text
Codex coordination/review: review this clean A1 packet against issue #138 D1–D16, issue #128, AGENTS.md, the Phase 1 risk register, and ownership boundaries. Do not implement or rewrite narrative in this review. If complete and user-approved, activate #133 and produce G1 from NVS_01_G1_Specification_Template.md.
```

Codex engineering remains blocked until the resulting G1 specification is approved.
