# ADR 0003 — Evidence-gated managed service versus custom service boundaries

Status: accepted decision process; all component selections remain unresolved

Date: 2026-08-31

Decision owner: game owner for vendor, managed/custom selection, spend, commitment, exposure, and residual risk

Review state: implementation and independent contract review required before `t_9ba5dbe2` completion

## Context

Managed services can reduce operational burden but may impose data, identity,
lifecycle, quota, failure, observability, and exit constraints. Custom/self-operated
components can preserve control but add owner-plus-AI operational burden. The
approved roadmap forbids assuming either direction or selecting a provider without
equivalent measured evidence.

## Decision

No component receives a managed or custom default. Every candidate must implement
the stable seam in `MMO-CONTRACTS-v1.0.0` and pass an equivalent evidence packet.
The contract remains fixed during comparison; a candidate that requires provider-
specific gameplay behavior records a contract violation.

Required decision dimensions for each capability:

1. correctness and state-ownership fit;
2. regional-data and backup isolation;
3. idempotency, ambiguous completion, reconciliation, and retry behavior;
4. failure containment, degraded mode, recovery, and rollback;
5. quotas, capacity signals, admission behavior, and observed limitations;
6. security, least privilege, credential rotation, audit, and privacy;
7. observability completeness, raw evidence access, and exportability;
8. schema/API compatibility, migration, data export, adapter removal, and exit;
9. owner-plus-AI deployment, maintenance, incident, and support burden;
10. measured total and unit cost evidence at the approved gate, without a preset
    ceiling in this ADR.

Applies independently to identity, placement/lifecycle, durable persistence,
cache/presence, event pipeline, object/content storage, social/chat/voice,
commerce/platform verification, observability/incident, secrets, deployment, and
backup/recovery.

## Consequences

Positive:

- decisions use repeatable workload/failure evidence rather than feature lists;
- no-selection is valid when candidates are incomparable or violate contracts;
- managed and custom alternatives remain replaceable behind the same seam;
- owner burden and exit cost are first-class evidence.

Costs:

- sandbox access, reproducible setup, failure injection, and rollback proof are
  required before selection;
- feature-rich candidates cannot bypass missing regional, correctness, or exit
  evidence;
- more than one adapter prototype may be thrown away.

## Rejected alternatives

1. Managed-by-default — rejected because operational convenience does not prove
   authority, regional isolation, recovery, or exit.
2. Custom-by-default — rejected because control does not prove sustainable
   owner-plus-AI operations, security, or cost.
3. Lowest quoted cost wins — rejected because no owner-approved cost ceiling exists
   here and correctness/exit evidence is non-substitutable.
4. One aggregate score — rejected because hard failures cannot be averaged away.
5. Candidate-specific scenarios — rejected because results would be incomparable.

## Required bake-off packet

- exact adapter/artifact/configuration fingerprints;
- reproducible setup and teardown;
- common success, duplicate, ambiguity, throttle/quota, outage, restart, regional
  isolation, credential rotation, telemetry loss, lifecycle, and rollback cases;
- raw measurements, logs, evidence handles, limitations, and contract violations;
- operation burden and exit/adapter-removal result;
- explicit unsupported scenarios and blockers;
- no production capacity extrapolation from sandbox evidence.

## Failure and rollback

A missing credential, inaccessible sandbox, unknown quota, incompatible regional
posture, incomplete failure evidence, or contract violation yields blocked or no-
selection. The spike is disabled/deleted, credentials revoked, provider resources
removed, neutral interfaces restored, operation IDs reconciled, and durable state
verified unchanged.

## Unresolved owner decisions

All component selections are unresolved, including provider, managed/custom split,
commitment, quota posture, spend, cost ceiling, service objectives, recovery
objectives, deployment exposure, and acceptable residual risk. The owner decides
only after both common evidence packets and the comparison/contingency task are
complete.
