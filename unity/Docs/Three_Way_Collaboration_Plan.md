# Collaboration Plan

This legacy path defines the first coordinated delivery milestone for GPT, Codex, and the user. It is a process and acceptance specification only; it does not create gameplay or author narrative content.

`AGENTS.md` is authoritative. Historical references to Android Studio mean the former narrative workflow now owned by Codex narrative/content mode. Android Studio may still be used as an IDE, but it is not an owner, branch prefix, or approval gate.

## Goal

Prove that one approved narrative quest line can move from authored content to a playable, persistent runtime loop without overlapping ownership or duplicating story logic.

## Milestone NVS-01: One Approved Quest Line, End to End

NVS-01 uses one bounded quest line selected in Codex narrative/content mode and approved by the user. GPT must not select, rewrite, or expand the story.

The vertical slice must demonstrate:

- An approved quest start condition.
- At least one tracked objective and state transition.
- At least one approved player choice or conditional branch.
- At least one approved NPC affinity, loyalty, reputation, or faction consequence.
- One handoff to an existing gameplay objective, encounter, boss gate, or kingdom-state hook.
- One approved completion or failure outcome.
- Save, reload, and resume behavior across the quest states used by the slice.
- Traceability from narrative IDs to runtime events and test evidence.

### Out of scope

- Rewriting or extending narrative content outside Codex narrative/content mode.
- New combat mechanics, boss redesigns, scenes, 3D assets, VFX, weather, or UI redesigns.
- Broad service refactors unrelated to the selected quest line.
- Integrating multiple chapters or multiple unrelated quest lines.
- Hard-coding dialogue or story outcomes in runtime code.

## Required handoff artifacts

### Narrative packet from Codex narrative/content mode

The packet must identify:

- Stable chapter, quest, objective, dialogue, NPC, faction, reward, and hook IDs used by the slice.
- Entry conditions, prerequisites, and unlock rules.
- Quest states and allowed transitions.
- Dialogue references and player-choice branches.
- Affinity, loyalty, reputation, faction, resource, reward, and world-state consequences.
- Completion, failure, retry, and recovery behavior.
- The intended gameplay handoff and the event that returns control to narrative progression.
- Any localization keys or authored text references.
- Narrative files changed and confirmation that no runtime-owned systems were redesigned.

### Implementation specification from GPT

The specification must include:

- A state-transition table that preserves the approved narrative packet.
- Runtime event names, producers, consumers, and payload requirements.
- Data-contract or schema changes, including compatibility expectations.
- Save fields, defaults, migration behavior, and resume semantics.
- Validation and error-reporting rules for duplicate or missing references.
- Exact ownership boundaries and an expected file-impact list.
- Shared files that require a soft lock.
- Acceptance tests and negative tests.
- Unresolved decisions that block implementation.

### Implementation report from Codex

The report must include:

- Files changed and why each change was necessary.
- Approved narrative inputs consumed.
- Shared files touched and lock status.
- Contract and save-compatibility decisions.
- Compilation, automated-test, and manual-validation evidence.
- Known limitations or validation that could not be completed.
- Confirmation that narrative text and outcomes were not rewritten.

## Ordered task plan

### G0 — Establish the collaboration baseline

**Owner:** GPT  
**Dependency:** None  
**Deliverable:** Root agent instructions, corrected workspace documentation, this milestone plan, and the pull-request declaration template.

**Acceptance criteria:**

- The canonical workspace is consistently documented as `D:\260711\MY\AndroidStudioProjects\AnotherLife`.
- GPT, Codex narrative/content, Codex engineering, Codex design/asset, and user approval boundaries are explicit.
- Branch, pull-request, shared-file, save-compatibility, and conflict rules are documented.
- No gameplay code or narrative content changes are included.

### A1 — Select and complete the NVS-01 narrative packet

**Owner:** Codex narrative/content mode
**Dependency:** G0 merged  
**Recommended branch:** `codex/nvs-01-narrative-packet`

**Acceptance criteria:**

- Exactly one bounded quest line is selected for the vertical slice.
- Every ID used by the packet is stable and unique.
- Entry, objective, choice, consequence, completion, failure, retry, and recovery behavior are explicit.
- The runtime handoff is described semantically without redesigning gameplay implementation.
- All authored dialogue and narrative outcomes remain in Codex narrative/content source files.
- No Codex-owned gameplay system or shared integration file is modified unless separately declared and approved.

### G1 — Convert the packet into an implementation specification

**Owner:** GPT  
**Dependency:** A1 available for review  
**Recommended branch:** `gpt/nvs-01-integration-spec`

**Acceptance criteria:**

- The state machine covers every allowed path in the narrative packet.
- Runtime events and contract fields map back to stable narrative IDs.
- Save and resume behavior is defined for every persisted state.
- Required and optional file impacts are separated.
- Shared-file locks and merge order are declared.
- Happy-path, branch, failure, reload, and invalid-data tests are specified.
- No dialogue, characterization, lore, chapter order, or narrative outcome is changed.

### C1 — Implement contract loading and validation

**Owner:** Codex  
**Dependency:** G1 approved  
**Recommended branch:** `codex/nvs-01-runtime-integration`

**Acceptance criteria:**

- Runtime code consumes the approved packet through existing interfaces, JSON, schemas, generated assets, or a narrowly justified extension.
- Shared contracts remain free of `UnityEngine` types where Fable compatibility applies.
- Duplicate IDs, missing references, invalid transitions, and unknown hooks produce clear validation failures.
- Narrative text and outcomes are not copied into or rewritten in runtime code.
- Any contract change is backward compatible or includes a documented migration plan.

### C2 — Implement quest-state and gameplay-handoff integration

**Owner:** Codex  
**Dependency:** C1 complete on the same focused branch or an approved predecessor PR

**Acceptance criteria:**

- The approved start, objective, choice, consequence, completion, and failure transitions execute deterministically.
- Runtime behavior uses approved IDs and events rather than quest-specific hard-coded branches.
- The selected existing gameplay hook can receive control and return an outcome to narrative progression.
- Existing service registrations and unrelated gameplay behavior remain intact.
- Invalid or unavailable hooks fail visibly and do not silently complete the quest.

### C3 — Implement persistence and compatibility

**Owner:** Codex  
**Dependency:** C2 complete

**Acceptance criteria:**

- Quest progress, selected branch, relevant relationship or reputation effects, gameplay-handoff state, and final outcome persist as required by G1.
- Saves created before NVS-01 still load through default initialization or a documented migration.
- Save and reload resume the quest at the correct state without duplicating rewards or consequences.
- Changes to `SaveGameData.cs` or local save services follow the shared-file lock rules.

### C4 — Add verification and publish the runtime PR

**Owner:** Codex  
**Dependency:** C1–C3 complete

**Acceptance criteria:**

- Automated tests cover the happy path, the approved branch, save/reload, duplicate or missing references, an invalid transition, and reward/consequence idempotency.
- Relevant Unity compilation and available test suites pass.
- The pull request uses the repository template and reports exact validation evidence.
- The diff contains no narrative rewrites, unrelated refactors, or undeclared shared-file edits.

### G2 — Review implementation and integration risk

**Owner:** GPT  
**Dependency:** Codex runtime PR open

**Acceptance criteria:**

- The implementation is checked against A1 and G1, not against inferred story intent.
- Ownership boundaries, contract fidelity, save compatibility, validation coverage, and shared-file declarations are reviewed.
- Any requested change points to a violated requirement or acceptance criterion.
- Narrative preferences are routed to Codex narrative/content mode or the user rather than rewritten by GPT.

### A2 — Verify narrative fidelity

**Owner:** Codex narrative/content mode
**Dependency:** Codex changes available in an integrated build

**Acceptance criteria:**

- Dialogue order, player choices, NPC or faction consequences, quest outcomes, and authored meaning match A1.
- Any runtime discrepancy is reported as an implementation issue without silently changing the source narrative.
- The narrative packet is updated only when the user intentionally approves a creative change.

### U1 — Final playtest and milestone acceptance

**Owner:** User  
**Dependency:** G2 and A2 complete

**Acceptance criteria:**

- The selected quest line can be started, progressed, branched, handed to gameplay, resolved, saved, reloaded, and resumed.
- Rewards and consequences occur once and remain consistent after reload.
- The runtime experience matches the approved narrative intent.
- All NVS-01 pull requests are merged in dependency order and no shared-file lock remains open.

## Pull-request and merge order

1. G0 coordination baseline.
2. A1 narrative packet.
3. G1 implementation specification.
4. C1–C4 Codex runtime integration.
5. G2 review fixes, if required, on the Codex branch.
6. A2 narrative-fidelity fixes in Codex narrative/content mode.
7. U1 acceptance and milestone closeout.

A dependent pull request must not merge before its upstream artifact is approved. When a change to an upstream narrative packet is necessary, update A1 first, then revise G1, and only then change Codex implementation.

## Shared-file conflict protocol

1. Search open pull requests before editing.
2. Declare each shared file in the new pull request using `.github/pull_request_template.md`.
3. Treat the declaration as an exclusive soft lock.
4. If another open pull request already holds the lock, do not edit the file in parallel.
5. Rebase the later branch after the lock-holding pull request merges.
6. Preserve all valid service registrations and initialize new save fields with compatible defaults.
7. Never resolve a conflict by discarding unfamiliar systems or by force-pushing over collaborator commits.
8. Ask GPT to resolve technical sequencing; ask the user to resolve creative intent.

## Milestone definition of done

NVS-01 is complete only when:

- Every task above meets its acceptance criteria.
- The canonical narrative remains owned by Codex narrative/content mode.
- The runtime consumes approved data without duplicating story logic.
- Save and reload are proven for all states used by the slice.
- Validation and automated tests cover expected and invalid inputs.
- All shared-file locks are released.
- The final integrated state is on `main` and the user has approved the playtest.
