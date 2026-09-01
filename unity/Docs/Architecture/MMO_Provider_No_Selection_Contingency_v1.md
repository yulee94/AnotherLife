# MMO Provider No-Selection Contingency v1

Contingency ID: `MMO-PROVIDER-NO-SELECTION-v1.0.0`

Applies while decision `MMO-PROVIDER-DECISION-20260901-001` awaits owner approval and no provider is selected.

Current state: both candidate adapters are disposable, non-production, and disabled from runtime use. No provider resource was created by the recorded runs, no provider credential is active in the neutral path, and no production provider outage/quota/credential action is authorized. These runbooks are final for that current state. A selected provider requires a new owner-approved, candidate-specific operational revision proven in its sandbox before production exposure.

Common evidence commands:

    python tools/architecture/validate_mmo_contracts.py .
    python tools/architecture/validate_mmo_bakeoff_plan.py .
    cargo test --manifest-path server/Cargo.toml -p al_server_core -p al_provider_adapter_stub --all-targets

Never print, paste, serialize, or commit credential values. Never turn an unknown provider response into a pass.

## RB-OUTAGE-01 — Provider or control-plane outage

### Trigger

A candidate API/status check is unavailable, a future approved sandbox reports control-plane loss, or a provider-specific path is suspected of affecting neutral work.

### Current-state containment

1. Keep both candidate adapters disabled and start no provider placement, lifecycle, identity, entitlement, telemetry, or administrative work.
2. Do not create a fallback writer, change durable region/realm membership, or copy regional authority.
3. Freeze only ambiguous provider operations. In the current no-selection state there should be none; any nonempty inventory is a stop condition.
4. Preserve the candidate packet, neutral configuration fingerprint, regional synthetic-state fingerprint, and one-owner evidence.

### Diagnosis and commands

Run the three common evidence commands. Then regenerate only the affected candidate’s fail-closed packet without credentials on the command line:

    python tools/architecture/run_gamelift_spike.py . --output evidence/amazon_gamelift/outage-review

or:

    python tools/architecture/run_playfab_spike.py --packet evidence/microsoft_playfab/outage-review

Validate that packet with `validate_mmo_bakeoff_plan.py --record`. A public status response is supporting context only; it cannot establish regional data-plane continuity, recovery, latency, or availability.

### Recovery decision

Neutral work may continue only when core/stub tests pass, one neutral owner remains, state/configuration fingerprints are unchanged, and no provider process/resource is required. Provider-specific work remains blocked until an approved sandbox proves the outage and recovery sequence.

### Rollback and evidence

Invoke `RB-REVERT-01` if a candidate was enabled, an operation is ambiguous, source/state drift is observed, or a provider dependency enters the core. Retain command output, packet manifest, blocker, hashes, and owner/epoch inventory. Status is `blocked` when provider outage injection or recovery is unavailable.

## RB-QUOTA-01 — Quota exhaustion or unknown quota

### Trigger

Any stable `throttled` result, provider quota signal, bounded queue pressure, or an unknown UL-01 through UL-09 boundary.

### Current-state containment

1. Do not start `WF-QUOTA-LADDER-v1`; both authenticated preflight inventories and owner authorization are absent.
2. Keep provider admissions at zero and queues empty. Do not infer unlimited, zero, or production capacity.
3. Do not request a paid increase, enable billing, or alter provider limits.
4. Preserve operation IDs and stop before any unapproved provider mutation.

### Diagnosis and commands

Regenerate each fail-closed packet with the runner commands above and inspect its `quota-inventory.json`. Validate each packet separately. The expected current classification is `unknown_measurement_required` for UL-01 through UL-09 and zero provider mutation attempts.

A future ladder may start only after both authenticated inventories exist, the common artifact/workload pair validates, and the owner approves regions, spend/safety guard, and maximum scope. Both candidates then run identical authorized steps up to the lower common boundary.

### Recovery decision

There is no quota to recover in the current no-selection state. Neutral work continues when queues are empty and core/stub tests pass. Provider work remains blocked. No CCU, throughput, latency, price, or production-capacity claim may be derived.

### Rollback and evidence

Invoke `RB-REVERT-01` if a provider request was attempted without the gate, if bounded admission is bypassed, or if accepted/ambiguous work cannot be reconciled. Retain inventories, counters, raw refusal/recovery signals, owner authorization, and rollback hashes.

## RB-CREDENTIAL-01 — Credential compromise or planned rotation

### Trigger

A candidate credential is unexpectedly present, suspected exposed, due for planned rotation in an approved sandbox, rejected, over-scoped, or observed in source/log/evidence scanning.

### Current-state containment

1. Keep both candidate adapters disabled and stop candidate actions.
2. Do not read or display secret values. Record only opaque reference/version and presence state through the sanitized runner.
3. Revoke the credential through the provider’s secure administrative path only by an authorized owner/operator; that external action remains blocked and unclaimed until its receipt is retained.
4. Freeze and reconcile any operation attempted during the exposure window. The expected current inventory is empty.

### Diagnosis and commands

Run both candidate packet generators. They sanitize credential presence and scan packets without serializing secret material. Run the three common evidence commands and verify `secret_scan_result.status` is `pass`, findings are empty, and no provider credential type enters the core.

Do not place provider secret values in shell arguments, documentation, tickets, screenshots, evidence filenames, or Kanban metadata.

### Recovery decision

Current neutral work may continue only when candidate adapters remain disabled, core/stub tests pass, secret scans pass, and no provider operation/resource depends on the credential. A planned provider rotation remains blocked until an approved sandbox proves replacement scope, propagation, old-credential denial, audit, and unchanged state hashes.

### Rollback and evidence

Invoke `RB-REVERT-01` on any failed scan, uncertain revocation, scope violation, or state/source drift. Retain only sanitized reference timelines, scan results, authorized revocation receipts, audit handles, reconciled operation inventory, and before/after hashes.

## RB-REVERT-01 — Revert a provider-specific experiment

### Trigger

Any hard-gate failure, unresolvable blocker, pair-equivalence failure, owner stop, provider-specific runtime dependency, unexpected credential/resource, source/state drift, or completion of a disposable experiment.

### Current-state containment

1. Stop new candidate work and keep the candidate disabled.
2. Assert there is one neutral owner and no provider process is authoritative.
3. Reconcile any operation IDs before deleting resources or evidence needed for diagnosis.
4. Preserve sanitized packet manifests and before-state hashes.

### Revert commands

Verify the provider-neutral path and source dependency direction:

    python tools/architecture/validate_mmo_contracts.py .
    cargo test --manifest-path server/Cargo.toml -p al_server_core -p al_provider_adapter_stub --all-targets
    cargo test --manifest-path server/Cargo.toml -p al_provider_adapter_gamelift_spike --test adapter_contract
    cargo test --manifest-path server/Cargo.toml -p al_provider_adapter_playfab_spike

The candidate crates remain disposable evidence code and are not runtime-selected. Do not delete source merely to hide a failed result. Disable candidate configuration, retain the reviewed evidence, and remove provider resources only through an authorized provider inventory/reconciliation procedure.

### Verification

1. Neutral configuration and regional-state hashes match their captured baselines.
2. Core/stub tests pass with no reverse dependency on either candidate.
3. Durable identity, realm, gameplay, economy, social, backup, and audit state required no rewrite.
4. Provider IDs remain adapter-private; no provider process/resource is authoritative.
5. Candidate credentials/resources are absent, or every residual is explicitly `blocked` with location, authority, retention, and owner action.
6. Each packet validates separately. A pair mismatch remains a blocker and never becomes a comparison pass.

### Failure disposition and evidence

If the neutral path fails, exposure remains stopped and the last proved single owner/state is retained. Do not re-enable a candidate to make validation pass. Retain exact commands, test output, operation reconciliation, dependency scan, hashes, packet manifests, credential/resource inventory, deletion receipts when authorized, residual unknowns, and final `pass`, `fail`, or `blocked` classification.

## Reopen and supersession

Reopen the provider decision when sandbox access, live executors, regions, provider APIs, topology, residency requirements, quotas, cost gate, or contract boundaries materially change. Supersede this contingency only with a reviewed record that preserves raw evidence, passes the common pair validator, and carries explicit owner approval for the chosen disposition and boundaries.
