# Disposable Amazon GameLift Servers spike adapter

Status: non-production evidence adapter; not a provider selection, deployment, or capacity claim.

This crate is the only server workspace crate allowed to contain GameLift-specific names and request/response translation. It depends on `al_server_core::provider_contracts`; the authoritative core has no reverse dependency. The injected `GameLiftApi` trait keeps SDK and credential handling outside the neutral contract and supports deterministic failure injection.

Implemented evidence seams:

- preassigned region, durable realm, immutable artifact, operation, and compatibility scope validation before a provider call;
- idempotent placement and lifecycle submission;
- changed payload/scope conflict detection;
- stable provider failure translation, including throttle and ambiguous completion;
- describe-before-retry reconciliation;
- sanitized provider-neutral observations;
- candidate disable/drop followed by a working neutral stub path without synthetic state rewrite.

The crate does not claim that the local transport is GameLift. Authenticated GameLift API, quota, residency, lifecycle, telemetry, credential, and teardown evidence must come from the generated run packet. Missing access remains `blocked`.

From the repository root:

    cargo test --manifest-path server/Cargo.toml -p al_provider_adapter_gamelift_spike --test adapter_contract
    python tools/architecture/run_gamelift_spike.py .
    python tools/architecture/validate_mmo_bakeoff_plan.py . --record evidence/amazon_gamelift/<run-id>/run-record.json

The runner uses the standard Boto3 credential provider chain without reading or exporting credential material. It performs only a sanitized identity check and one read-only GameLift queue-inventory request when a credential resolves. It never creates, updates, uploads, starts, scales, or deletes a provider resource. Provider mutations require a separately approved authenticated sandbox procedure and must not be inferred from the deterministic test transport.

Rollback is deletion/disablement of this crate or adapter selection, then execution of the neutral stub/core tests. Durable identity, realm, gameplay, economy, and social schemas do not contain GameLift resource identifiers.
