# Rollback Record RR-RC-20260828-002-001

**Rollback record ID:** `RR-RC-20260828-002-001`

**Candidate:** `RC-20260828-002`

**Retained baseline:** `RB-20260828-001`

**Control:** `GOV-G0-v1.0.0`

**State:** `ARMED — NO ROLLBACK EXECUTED`

**Owner:** `default` / `t_a4c586ff`

## Trigger and scope

Execute on owner `REVISE`/`REJECT`, independent `FAIL`/`FAIL-CLOSED`, canonical hash drift, inaccessible evidence, or source/authority conflict. Isolate only unapproved RC-002 documentation and board effects; preserve `RB-20260828-001`, all source decisions, owner/audit/stop-ship history, RC-001 withdrawn history, and unrelated work.

1. Freeze RC-002 and record disposition on `t_a4c586ff`.
2. Leave the candidate PR unmerged and close/revise it; if candidate-only content merged without approval, use a scoped revert PR only. Never reset `main`, force-push, or revert unrelated work.
3. Preserve Kanban history and record corrections as new events.
4. Keep `GT-00`, `t_1ad7a8d5`, and descendants blocked from relying on RC-002.
5. Verify the parent manifest, `t_93c953eb`, `t_0648ce23`, and cited hashes remain addressable.
6. Record execution in a new rollback revision; do not mutate this armed plan into an execution claim.

No runtime, save, economy, inventory, progression, financial, Realm Gem, Wish, deployment, or destructive-state action is authorized.

## Reopen

A replacement uses a new `RC-*` identity, canonical hash inventory, independent audit, owner decision, gate/approval/baseline records, and rollback record. Unaffected approvals remain intact.
