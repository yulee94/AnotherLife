# MMO Provider Decision Log v1

Decision ID: `MMO-PROVIDER-DECISION-20260901-001`

Status: recommendation recorded; owner decision not granted

Recommendation: `no_selection`

Owner approval is required before this recommendation becomes a decision and before any provider, managed-versus-custom boundary, spend, commitment, quota posture, residency exposure, production use, or residual risk is accepted.

## Decision

Neither Amazon GameLift nor Microsoft PlayFab can be recommended from the retained evidence. Both current runs are blocked in all 16 predeclared scenarios, no provider sandbox scenario was measured, and the pair validator rejects the two records because the `server_artifact_fingerprint` differs. The reversible recommendation is therefore `no_selection`, pending explicit owner approval. This is not a finding that the providers are equivalent, unsuitable, compliant, or noncompliant.

The provider-neutral contracts, regional authority model, data map, threat model, adapter seams, and disabled disposable spikes remain in force. There is no production MMO provider and no production use is authorized.

## Claim discipline

- Requirement: `MMO-CONTRACTS-v1.0.0`, ADRs 0001–0004, the ten data classes, eight threats, 16 common scenarios, and nine unknown-limit dimensions define what must be proved. Requirements are not observed provider behavior.
- Measured result: on repository commit `956f452a30dcf028fd35f0c9af0fbc210b9b8d92`, both fail-closed runners completed local contract/core work, recorded 16 blocked scenarios, attempted no provider mutation, and created no provider resource. These are local preflight and rollback observations, not provider performance measurements.
- Vendor-documented fact: dated provider documentation URLs and scoped summaries are retained in each packet. They support follow-up planning but are not measured limits, residency proof, capacity proof, pricing evidence, or contract compliance.
- Assumption: the four-realm topology, account-population requirements, and future concurrency targets are approved planning inputs. They are not achieved capacity. No candidate-specific performance, cost, quota, latency, residency, or device-tier assumption is used in this recommendation.
- Blocker: GameLift has no resolved AWS credential or authenticated inventory. PlayFab has no authorized title, credential, build, two-region configuration, or live mutation executor. Both lack provider-dependent measurements. The artifact-bound common inputs also differ, so the two blocked records cannot pass paired equivalence validation.
- Owner judgment: provider/no-selection, component boundaries, spend, commitment, quotas, regions, exposure, objectives, and residual risk remain unset. This document recommends a reversible posture but does not exercise owner authority.

## Raw evidence traceability

The current comparison artifact is `mmo-provider-decision-evidence-956f452a.zip`, SHA-256 `d06406db914d8c4f0ddb3a2b8863e99d2dec0d4d98e54c799781e04e58f88c46`, attached to Kanban task `t_aed15bbd` at completion. It contains both packets plus the paired-validator output (SHA-256 `d4bc18699d74acdecd453aa04bf7b6308adfe25e01fab1056f694630da83abb6`).

- GameLift current run record: `gamelift-20260831-234935z`, SHA-256 `44f66ef865a38ecce1cdf3d80d67d13f0a55663d87ffad6ec9005aef1b6a4bc6`.
- PlayFab current run record: `playfab-blocked-956f452a30dc`, SHA-256 `9f4b6979a8cb733c21ed1ba7797e88ae91a14ebf6d1b0a67fba672cdf7124def`.
- GameLift source packet from task `t_ff702849`: archive SHA-256 `b08ec175bc3472a02db6f15156a941eb765215e3849c329e9726cda402db52e1`, merged by PR 645.
- PlayFab source packet from task `t_27759e01`: archive SHA-256 `4a9bd58c059706a9fc254c57f6344ef6e7a9c7c7fa179a893e8cdbb210788b40`, merged by PR 659.

Each run record has a SHA-256 manifest for retained raw files. Reproduce and validate current blocked packets with:

    python tools/architecture/run_gamelift_spike.py . --output evidence/amazon_gamelift/current
    python tools/architecture/run_playfab_spike.py --packet evidence/microsoft_playfab/current
    python tools/architecture/validate_mmo_bakeoff_plan.py . --record evidence/amazon_gamelift/current/run-record.json
    python tools/architecture/validate_mmo_bakeoff_plan.py . --record evidence/microsoft_playfab/current/run-record.json

The paired validator currently fails with `paired comparison common-driver input drifted: server_artifact_fingerprint`. That failure is retained as comparison evidence, not hidden.

## Equal-criteria comparison

Both candidates are assessed with ADR 0003’s same ten dimensions. `blocked` means the provider-dependent evidence needed to assess the dimension is absent; it does not mean fail.

| Criterion | Amazon GameLift | Microsoft PlayFab | Evidence boundary |
| --- | --- | --- | --- |
| Correctness and state-ownership fit | blocked | blocked | Local adapter tests exercise neutral translation; provider paths did not run. |
| Regional data and backup isolation | blocked | blocked | No authenticated store, log, backup, support, export, restore, or deletion inventory. |
| Idempotency, ambiguity, reconciliation, retry | blocked | blocked | Synthetic adapters exercise seams; no provider operation was committed or reconciled. |
| Failure containment, recovery, rollback | blocked | blocked | Neutral source/state hashes were restored; provider outage and teardown remain untested. |
| Quota, capacity signal, admission | blocked | blocked | UL-01 through UL-09 remain `unknown_measurement_required`; no ladder started. |
| Security, credentials, audit, privacy | blocked | blocked | Packet secret scans passed; provider credential rotation and audit paths did not run. |
| Observability and raw-evidence export | blocked | blocked | Local evidence is retained; provider telemetry/export behavior is unmeasured. |
| Compatibility, migration, removal, exit | blocked | blocked | Neutral adapter removal remains viable; authenticated provider residuals are unknown. |
| Owner-plus-AI operational burden | blocked | blocked | Automated local preflight duration is not production operating burden. |
| Approved-gate cost evidence | blocked | blocked | No approved cost gate, quoted comparison, or measured workload cost exists. |

## Contract violations and compliance boundary

Neither record reports an observed contract violation. That is not a pass: provider-dependent scenarios were blocked before the behaviors needed to detect authority, regional, retry, credential, quota, telemetry, lifecycle, or exit violations could run. Contract assessment for both candidates is `unknown_blocked`.

A future hard-gate violation cannot be averaged away. Provider features do not justify changing the neutral authority, residency, idempotency, or removal contract.

## Operational, residency, quota, and lock-in risks

- Operational risk is unknown for both candidates. Local automated preflight time, public status, and synthetic adapters do not establish deployment, maintenance, incident, support, recovery, or on-call burden.
- Residency risk is unresolved. GameLift’s configured logical home/forbidden regions and PlayFab’s unconfigured region are test inputs, not proof of control-plane, data-plane, log, backup, support, export, deletion, or restore location.
- Quota and capacity risk is unresolved. Every UL-01 through UL-09 item remains `unknown_measurement_required`; no CCU, throughput, latency, or production-capacity claim follows.
- Lock-in is partly contained structurally: provider SDK/resource/error/credential types remain adapter-private and the neutral core tests pass after candidate disablement. Actual export, deletion, residual-resource, retention, account-closure, and operational exit behavior remains unmeasured.
- Credential risk is contained only by non-use. Secret scans passed, but issuance, least privilege, overlap, propagation, revocation, negative old-credential tests, and audit remain blocked.

## Managed versus custom implications

No managed or custom component boundary is selected. GameLift and PlayFab may reduce some future operational work, but this evidence does not measure that benefit or the associated quota, residency, failure, support, or exit constraints. The provider-neutral modular server, contracts, and adapters are a reversible implementation seam, not a decision to self-host production infrastructure.

The no-selection contingency permits continued provider-neutral domain, protocol, test, and local vertical-slice work. It blocks provider-specific gameplay behavior, production integration, production data, spend, commitment, and any component decision that would bypass the evidence gate.

## Follow-up evidence required

1. Owner authorizes isolated non-production sandboxes, synthetic data, regions, fault seams, and any material spend.
2. Implement reviewed live executors that preserve credential secrecy and provider-neutral boundaries.
3. Normalize both runners to one committed server artifact, workload manifest, configuration shape, payload shape, request envelope set, operation identity scheme, teardown assertions, topology, warmup rule, and runbook version.
4. Rerun `SCN-01` through `SCN-16` for both candidates with the same common inputs.
5. Measure or explicitly retain unknown all UL-01 through UL-09 dimensions, including residency, quota, credential, telemetry, lifecycle, backup, export, deletion, and residual behavior.
6. Validate each packet and the two-record pair; retain any pair mismatch as a blocker.
7. Independently review raw evidence, contract violations, rollback, operating burden, and confidence gaps.
8. Request explicit owner approval for `select_amazon_gamelift`, `select_microsoft_playfab`, or `no_selection`, plus each managed/custom boundary.

## Reversibility and runbooks

`MMO_Provider_No_Selection_Contingency_v1.md` is the finalized current-state contingency for provider outage, quota exhaustion, credential rotation/compromise, and reverting provider-specific experiments. It contains no live provider mutation command because none is authorized or integrated. Every future provider-specific command remains blocked until an approved sandbox packet supplies and tests it.

Rollback remains viable at the present boundary: both candidates created zero provider resources; neutral configuration and synthetic regional-state hashes were unchanged; core tests passed; provider IDs are not canonical; and neither provider process is authoritative. Authenticated provider teardown remains unknown rather than passed.

## Epic exit assessment

Reviewed ADRs, provider-neutral contracts, the data map, threat model, blocked results, decision log, and no-selection contingency are present and traceable. The epic cannot honestly claim a completed measured provider comparison or owner decision. Exit status is `blocked_pending_evidence_and_owner_approval`; reversible provider-neutral work may continue under the contingency.
