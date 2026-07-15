# NVS-01 GPT–Codex Collaboration Plan

The legacy filename is retained for link stability. This document defines the first coordinated milestone for GPT, Codex, and the user.

## Goal

Prove that one user-approved quest line can move from Codex-authored narrative source to a playable, persistent runtime loop without duplicated story logic or collapsed review boundaries.

## Milestone NVS-01

The slice must demonstrate:

- an approved quest offer and start condition;
- tracked objectives and deterministic state transitions;
- an approved player choice or conditional branch;
- an approved affinity, reputation, faction, resource, or world consequence;
- a handoff to an existing or explicitly requested gameplay capability;
- completion, failure, retry, cancellation, and recovery behavior;
- save, reload, and resume across every used state;
- traceability from narrative IDs to runtime events and tests.

Out of scope: unrelated chapters, broad gameplay redesign, terrestrial design, new combat or boss mechanics, speculative scenes/assets/VFX, hard-coded runtime story text, and unrelated service refactors.

## Required artifacts

### A1 narrative packet — Codex narrative/content mode

The packet identifies stable IDs, prerequisites, states, objectives, dialogue, choices, consequences, gameplay handoff, return events, localization keys, completion/failure/retry/recovery, resume behavior, and unresolved creative decisions.

### G1 implementation specification — GPT

The specification maps the approved packet to runtime state, events, contracts, persistence, migration, idempotency, error behavior, file impact, locks, tests, rollback, and delivery order without rewriting narrative intent.

### C1–C4 implementation report — Codex engineering mode

The report identifies files changed, source consumed, shared locks, contracts, compatibility decisions, exact build/test evidence, limitations, and confirmation that narrative meaning was not silently rewritten.

## Ordered task plan

### G0 — Collaboration baseline

**Owner:** GPT

Acceptance:

- canonical workspace, ownership modes, branches, PR declarations, locks, and evidence rules are merged;
- no source behavior changes.

### A1 — Complete the approved OMEN_1 packet

**Owner:** Codex narrative/content mode
**Branch:** `codex/narrative-nvs-01-a1`

Acceptance:

- exactly one bounded quest line;
- D1–D16 and user-approved intent encoded without reinterpretation;
- unique stable IDs and complete internal references;
- explicit offer, acceptance, objectives, choice, arena handoff, failure/retry, manual report, consequences, abandonment, and resume;
- complete localization inventory;
- requested external capabilities labeled honestly;
- no runtime implementation or shared integration file changes.

### G1 — Publish the runtime integration specification

**Owner:** GPT
**Branch:** `gpt/nvs-01-integration-spec`

Acceptance:

- every A1 path appears in the state machine;
- events include producer, consumer, payload, correlation, duplicate behavior, and failure semantics;
- persistence, defaults, migration, atomicity, idempotency, and resume are explicit;
- required/optional/prohibited files and locks are explicit;
- positive, negative, duplicate, reload, and fault tests are specified;
- no narrative change.

### C1 — Contract loading and validation

**Owner:** Codex engineering mode
**Branch:** `codex/nvs-01-runtime-integration`

Acceptance:

- runtime consumes the approved source through a versioned representation;
- duplicate IDs, missing references, invalid transitions, unsupported versions, and unavailable hooks fail visibly;
- contracts stay compatible and do not duplicate narrative authority.

### C2 — State machine and gameplay handoff

Acceptance:

- offer, accept, objective, dialogue, choice, handoff, failure/retry, report, completion, and abandonment execute deterministically;
- quest and free/demo encounter contexts are distinct;
- late, duplicate, mismatched, and unavailable results cannot progress the quest.

### C3 — Persistence and compatibility

Acceptance:

- dialogue/objective/handoff/report/artifact/consequence state persists as specified;
- old saves load safely;
- reload resumes correctly;
- rewards and consequences cannot duplicate;
- shared-file locks and migration rules are followed.

### C4 — Verification and runtime PR

Acceptance:

- focused automated tests cover happy, branch, failure, retry, abandonment, report, reload, duplicate, malformed-data, and fault cases;
- relevant Android/Unity/contract validation has exact evidence;
- no narrative rewrite, unrelated refactor, or undeclared shared-file edit.

### G2 — Integration review

**Owner:** GPT

Acceptance:

- implementation is reviewed against A1 and G1;
- ownership, contracts, persistence, validation, locks, and merge safety are dispositioned;
- every requested change cites a violated requirement or acceptance criterion.

### A2 — Narrative-fidelity disposition

**Owner:** Codex narrative/content mode

Acceptance:

- dialogue order, choices, consequences, quest outcomes, and meaning match A1;
- implementation discrepancies are reported without silently changing source;
- any intentional creative change returns to user approval and requires A1/G1 revision before runtime changes.

A2 is not independent technical approval because the same Codex agent may have authored and implemented the work. GPT review and user acceptance remain mandatory independent gates.

### U1 — Final playtest and milestone acceptance

**Owner:** User

Acceptance:

- the quest starts, progresses, branches, hands off, resolves, saves, reloads, and resumes;
- consequences occur once and remain stable;
- the experience matches approved intent;
- all required PRs are merged in order and locks are released.

## Merge order

```text
G0 → A1 → G1 → C1–C4 → G2 → A2 → U1
```

A dependent PR must not merge before its upstream artifact is approved. Changes to narrative intent update A1 first, then G1, then engineering.

## Shared-file protocol

Search open PRs, declare locks, avoid parallel edits, rebase the later branch, preserve valid services and save fields, and never discard unfamiliar systems to resolve conflicts. GPT resolves technical sequence; the user resolves creative direction.

## Definition of done

NVS-01 is complete only when all task acceptance criteria pass, Codex-authored narrative remains the canonical source, runtime consumes approved data, save/reload and invalid-input tests pass, all locks are released, the integrated state is on `main`, and the user accepts the playtest.
