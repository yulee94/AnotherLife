# MMO Provider Bake-off Runbooks v1

Template ID: `MMO-BAKEOFF-RUNBOOKS-v1.0.0`

Plan: `MMO-BAKEOFF-v1.0.0`

Use: copy into each candidate evidence packet, replace bracketed fields before injection, and preserve commands plus evidence handles after execution. An unfilled or untested action is a blocker, never a pass. These runbooks operate only on synthetic non-production resources.

Common fields for every execution:

- Candidate: `[amazon_gamelift | microsoft_playfab]`
- Run ID: `[immutable run id]`
- Scenario IDs: `[SCN-*]`
- Operator and authorization reference: `[reference, not credential]`
- Started/ended UTC: `[timestamps]`
- Region and four realm IDs: `[values]`
- Server artifact, adapter, configuration, compatibility, and workload fingerprints: `[hashes]`
- Active operation IDs, route leases, and owner epochs: `[sanitized inventory handle]`
- Neutral configuration and regional synthetic-state hashes before action: `[hashes]`
- Candidate resource and credential-reference inventory: `[evidence handle]`
- Exact commands/API calls and results: `[commands.txt and raw evidence handles]`
- Decision/status: `[pass | fail | blocked]`

Never paste secret values into this document, shell history, evidence filenames, logs, screenshots, or tickets.

## RB-OUTAGE-01 — Provider or control-plane outage

### trigger

Use when provider lifecycle/placement/control APIs are unavailable, a region is isolated, health signals disagree, or `SCN-08` injects the condition. Record whether the established regional data plane is still reachable separately from the control plane.

### safety_and_authority_checks

1. Confirm synthetic non-production scope and the approved fault seam.
2. Capture current regional simulation owner, monotonic epoch, active routes, pending operations, and regional state hash.
3. Confirm the neutral route/configuration is available but do not create a second writer.
4. Confirm no automatic cross-region placement, restore, or realm reassignment is enabled.
5. Set the evidence gap to `blocked` if the outage cannot be safely injected or distinguished from client/network failure.

### containment

1. Stop new candidate placements and lifecycle mutations with the preconfigured reversible control: `[command]`.
2. Freeze value mutations whose completion cannot be reconciled; do not stop established simulation merely because the control plane is unknown.
3. Reject implicit cross-region fallback and duplicate-owner startup.
4. Preserve bounded logs, traces, metrics, provider events, and operation journals.

### diagnosis

1. Query provider control-plane health: `[command]`.
2. Query independent regional process and endpoint health: `[command]`.
3. Query every pending operation/receipt by stable operation ID: `[command]`.
4. Compare provider state, fenced lease journal, and regional owner epoch; the regional contract remains authoritative.
5. Classify each path as `healthy`, `degraded`, `unavailable`, `ambiguous_completion`, or `blocked`.

### actions

1. Allow only the explicitly approved continuation of established sessions; record disconnect/reconnect behavior.
2. Attempt the manifest-defined new session and placement; expect explicit failure/degradation, not a new region/realm.
3. Keep bounded simulation/persistence queues independent of the failed control plane.
4. If ownership becomes ambiguous, stop exposure and fence all but the last proved regional owner.
5. Do not modify durable identity, realm membership, gameplay, economy, or social records to match provider state.

### recovery

1. Observe provider recovery through both provider and independent health signals.
2. Reconcile all pending lifecycle/placement operations before retry.
3. Verify one owner and correct epoch, then rebuild only reconstructible routes/projections.
4. Admit new work gradually with the unchanged workload/configuration and capture counters.

### rollback

1. If recovery or reconciliation fails, invoke `RB-REVERT-01`.
2. Restore the neutral adapter/path only after the current owner is drained or safely retained.
3. Revoke/rotate credentials if outage evidence suggests compromise.
4. Delete failed partial candidate resources after evidence export.

### verification

- exactly one regional writer exists;
- region and durable realm memberships are unchanged;
- no implicit cross-region placement/write occurred;
- established and new-work behavior is separately evidenced;
- ambiguous operations are reconciled or remain frozen;
- regional state and neutral-configuration hashes match expected values;
- core authority and regional-isolation checks pass.

### evidence

Attach health timelines, fault command, operation reconciliation, owner/epoch inventory, stable result counts, provider events, independent metrics, state hashes, limitations, blocker, rollback result, and residual-resource inventory.

## RB-QUOTA-01 — Quota exhaustion

### trigger

Use on a stable `throttled` result, provider quota alarm, bounded queue pressure caused by provider refusal, or the first `SCN-07` ladder stop. Unknown or inaccessible quota information also invokes this runbook as a measurement blocker.

### safety_and_authority_checks

1. Confirm `WF-QUOTA-LADDER-v1` and identical step inputs are active.
2. Confirm the sandbox spend/safety guard and lowest mutually authorized boundary.
3. Capture current attempted, accepted, pending, succeeded, rejected, throttled, unavailable, ambiguous, and cancelled counts.
4. Confirm authoritative simulation does not wait synchronously on the quota-limited boundary.
5. Do not request a paid increase or alter account limits without owner authority.

### containment

1. Stop the current ladder and new nonessential requests: `[command]`.
2. Bound the producer queue and apply the provider-neutral admission/backpressure path.
3. Preserve already accepted work and stable operation IDs.
4. Do not hide overload by slowing authoritative game time, inventing a player-facing cap, or dropping value mutations without a recoverable record.

### diagnosis

1. Capture raw provider response, stable translation, headers/signals, dashboard/export, and retrieval UTC.
2. Identify the scope: account, project/title, region, fleet/server, build/artifact, API, operation, or unknown.
3. Distinguish rate, burst, concurrency, resource-count, artifact, trial, billing, and administrative limits.
4. Query dated vendor documentation and record whether the limit is documented, measured, or unknown.
5. Record reset/recovery timing and whether visibility arrives before refusal.

### actions

1. Drain accepted and pending work without changing payload or operation identity.
2. Retry only after an explicit provider/capacity signal and according to the neutral retry disposition.
3. Record one controlled recovery step; do not resume the full ladder automatically.
4. If the other candidate cannot run the same step, mark the dimension incomparable rather than superior/inferior.

### recovery

1. Verify stable success at the last common safe step.
2. Verify queues return to baseline and no operation remains ambiguous.
3. Reopen admissions only through the existing policy seam.
4. Keep the discovered value scoped to sandbox/account/region/time; make no production-capacity claim.

### rollback

1. Cancel remaining candidate work by stable operation ID.
2. Drain or retire temporary capacity and restore baseline configuration.
3. Invoke `RB-REVERT-01` if quota recovery cannot be observed or the adapter bypasses bounded admission.

### verification

- throttle maps to `throttled`, not a provider-specific core error;
- queues and memory remain bounded;
- accepted work is reconciled and no value mutation duplicates;
- recovery signal and timing have raw evidence or remain unknown;
- both candidates use the same authorized ladder steps;
- no CCU, latency, cost, or production-capacity claim is inferred.

### evidence

Attach request-step manifest, every counter, raw refusal/retry signal, provider export, independent queue/process measurements, vendor source and retrieval UTC, recovery observation, unknowns, limitations, manual effort, and rollback result.

## RB-CREDENTIAL-01 — Credential compromise or planned rotation

### trigger

Use for scheduled rotation, expiry, suspected disclosure, unexpected privilege/audit event, provider revocation notice, or `SCN-13`. Actual secret material must never enter the packet.

### safety_and_authority_checks

1. Identify credential by opaque reference/version and approved least-privilege scope.
2. Record dependent adapter/process resources and active operations without logging the secret.
3. Capture synthetic regional-state and neutral-configuration hashes.
4. Ensure a neutral adapter/path or replacement credential is ready.
5. For suspected compromise, stop privileged candidate actions before diagnosis.

### containment

1. Disable new candidate mutations/placements within the affected scope: `[command]`.
2. Revoke the suspected credential immediately when compromise is plausible: `[command using secure reference]`.
3. Isolate affected adapter resources and preserve access-controlled audit evidence.
4. Freeze ambiguous privileged/value operations for reconciliation.

### diagnosis

1. Review provider and independent audit events by credential reference, principal, capability, region, time, and sanitized operation ID.
2. Scan source, config, logs, traces, metrics, commands, screenshots, and evidence names for secret material: `[command]`.
3. Compare observed actions with the least-privilege policy; record unexpected scope separately.
4. Reconcile every operation during the exposure window against regional authority.

### actions

1. Issue replacement credential with the same or narrower approved scope: `[secure command]`.
2. Update only the adapter's secret reference/configuration; no gameplay payload or domain schema changes.
3. Start/reload the adapter and run one neutral health/placement probe.
4. Revoke old credential, then perform an explicit negative old-credential test.
5. If compromise occurred, rotate dependent credentials and containment references as authorized; otherwise block and escalate the required owner action.

### recovery

1. Verify replacement health, scope, audit, and expected regional operation.
2. Verify old credential denial after propagation and record any overlap window.
3. Reconcile frozen operations before admission resumes.
4. Compare regional-state and neutral-configuration hashes; no state rewrite is allowed.

### rollback

1. If replacement fails, keep the old credential revoked when compromise is suspected and invoke `RB-REVERT-01`.
2. For planned rotation only, a bounded overlap fallback may use the prior credential solely when pre-approved and not compromised; record it as a limitation.
3. Disable/delete the candidate adapter if credential lifecycle cannot satisfy the neutral security contract.

### verification

- old credential fails after recorded propagation;
- replacement is least privilege and works only in approved scope;
- no secret material appears in evidence or neutral errors;
- audit records every administrative action;
- regional gameplay, economy, social, realm, and identity state is unchanged;
- no core source/type/config depends on the provider credential.

### evidence

Attach opaque credential-version timeline, policy/scope inventory, secure-command references with values redacted, audit exports, old-credential denial, secret-scan command/result, reconciled operation list, state hashes, overlap/propagation unknowns, and rollback result.

## RB-REVERT-01 — Revert a provider-specific experiment

### trigger

Use after any hard-gate failure, unresolvable blocker, data leak, authority ambiguity, failed lifecycle/rollback test, credential failure, owner stop, or completion of `SCN-15`/`SCN-16` teardown.

### safety_and_authority_checks

1. Confirm synthetic non-production scope and identify the last proved regional owner/epoch.
2. Capture candidate and neutral configuration hashes, regional-state hash, pending operation inventory, credential references, and provider resources.
3. Confirm the neutral artifact/configuration is compatible and independently tested.
4. Do not delete resources needed to reconcile an ambiguous operation until its evidence is captured and value state is safe.
5. Stop if two writers are possible; fence to one owner before proceeding.

### containment

1. Stop new candidate placements, identity links, entitlement reconciliations, lifecycle mutations, and privileged actions: `[command]`.
2. Freeze ambiguous value/lifecycle operations.
3. Drain or safely retain the one established owner; prevent a replacement writer from starting.
4. Preserve sanitized raw evidence and region-copy inventory.

### diagnosis

1. Enumerate completed, pending, ambiguous, and failed operations by stable ID.
2. Compare provider resource/allocation state with the regional lease journal and domain source of truth.
3. Identify adapter-private mappings and every provider data/resource residual.
4. Classify the revert reason by threat, scenario, contract violation, and data class.

### actions

1. Reconcile all operations or leave affected value mutations explicitly frozen.
2. Switch routing/configuration to the captured neutral adapter/path: `[command]`.
3. Disable/remove the candidate adapter from the runtime/workspace: `[command]`.
4. Rerun provider-neutral core authority, idempotency, region, credential, and state-hash checks: `[commands]`.
5. Revoke candidate credentials.
6. Export allowed evidence; delete candidate resources and synthetic data.
7. Inventory retention, delayed deletion, account-owned residuals, manual steps, and unknowns.

### recovery

1. Verify the neutral route accepts the manifest-defined functional probe.
2. Verify durable canonical identity, realm, gameplay, economy, social, backup, and audit state did not require rewrite.
3. Verify no provider process is an owner and no provider ID is canonical.
4. Resume only neutral synthetic work after all required checks pass.

### rollback

This runbook is itself the provider rollback. If the neutral path fails, keep exposure stopped, preserve the last proved single owner and compatible state, retain the candidate disabled, and mark the run failed/blocked. Do not re-enable the candidate merely to make the test pass.

### verification

- neutral configuration and artifact fingerprints match the captured baseline;
- regional synthetic-state hash matches, or every expected deterministic difference is reconciled and evidenced;
- core compiles/tests with no candidate adapter dependency;
- no gameplay/domain contract, realm mapping, or ledger history changed for provider removal;
- all candidate credentials are revoked or an explicit blocked residual is recorded;
- provider resources/data are deleted or each residual has location, retention, authority, and owner-action status;
- evidence packet remains sanitized and complete.

### evidence

Attach before/after hashes, operation reconciliation, owner/epoch inventory, exact config switch and adapter-removal commands, core test outputs, neutral probe, provider resource export/deletion receipts, credential denials, residual inventory, manual effort, limitations, blockers, and final disposition.
