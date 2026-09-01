# Rollback Record RR-RC-20260828-001-001

**Rollback record ID:** `RR-RC-20260828-001-001`

**Candidate:** `RC-20260828-001`

**Retained baseline:** `RB-20260828-001`

**Control:** `GOV-G0-v1.0.0`

**State:** `ARMED — NO ROLLBACK EXECUTED`

**Owner:** `default` / `t_a4c586ff`

## Trigger

Execute this scoped record when the owner records `REVISE` or `REJECT`, independent audit records `FAIL` or `FAIL-CLOSED`, package identity drifts, required evidence becomes inaccessible, or a source/authority conflict invalidates the candidate.

## Exact rollback scope

Rollback is limited to the unapproved documentation and board effects introduced by `RC-20260828-001`. Preserve `RB-20260828-001`, its owner decision on `t_0648ce23`, all source-card decisions, audit history, stop-ship history, and unrelated repository work.

1. Freeze the candidate and record the authoritative disposition on `t_a4c586ff`.
2. If the candidate PR is unmerged, leave it unmerged and close or revise it. If candidate-only files were merged without package approval, use a scoped revert PR for only those candidate commits. Never reset `main`, force-push, or revert unrelated work.
3. Preserve Kanban history. Record any dependency/status correction as a new event; never delete the prior owner approval or audit finding.
4. Keep `GT-00`, `t_1ad7a8d5`, and descendants blocked from relying on this candidate.
5. Verify that `unity/Docs/Roadmap/Baselines/RB-20260828-001/manifest.md`, `t_93c953eb`, `t_0648ce23`, and their cited package/validation hashes remain addressable.
6. Append an execution result to a new rollback record revision. Do not mutate this armed plan into a claim that rollback ran.

No runtime, save, economy, inventory, progression, financial, Realm Gem, Wish, deployment, or destructive-state action is authorized or required. This candidate changes roadmap documentation only.

## Reopen after rollback

A replacement candidate receives a new `RC-*` identity, fresh hash inventory, independent audit, owner decision, gate record, approval record if approved, and rollback record. Unaffected approvals remain intact.
