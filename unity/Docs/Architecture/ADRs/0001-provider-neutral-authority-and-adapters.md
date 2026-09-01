# ADR 0001 — Provider-neutral ports and authoritative gameplay core

Status: accepted technical boundary

Date: 2026-08-31

Decision owner: game owner for any vendor, spend, gameplay-authority, or material architecture change; AI agents may implement reversible contract proofs

Review state: implementation and independent contract review required before `t_9ba5dbe2` completion

## Context

AnotherLife has an offline/local client stack and an engine-free Rust authority
prototype, but no production MMO backend. Candidate managed services combine
capabilities differently. Letting one candidate's resource model or SDK become the
gameplay API would make later replacement destructive and could move combat,
economy, identity, or realm authority outside the approved model.

## Decision

Adopt ports-and-adapters around a provider-neutral authoritative core.

- Gameplay/domain code owns combat, movement, objectives, rewards, progression,
  economy settlement, durable realm membership, and social membership.
- Replaceable adapters may verify opaque external evidence, place sessions, manage
  dedicated process lifecycle, report measured capacity/health, or integrate
  platform capabilities.
- The core exchanges canonical IDs, operation IDs, fenced leases, immutable
  fingerprints, normalized claims, stable failures, and sanitized observations.
- Provider resource IDs, SDK types, errors, credentials, retry defaults, and
  configuration remain inside the adapter.
- `al_server_core` cannot depend on an adapter crate. Adapter crates may depend on
  `al_server_core::provider_contracts`.
- An adapter never returns a gameplay result. It cannot mutate durable realm,
  gameplay, economy, or social state.

## Consequences

Positive:

- candidates can be compared against identical behavior and failure scenarios;
- adapter removal does not rewrite gameplay state;
- server-authoritative rules remain testable without network/provider access;
- retries, ambiguous completion, observability, and rollback have one vocabulary.

Costs:

- provider features require explicit translation;
- lowest-common-denominator interfaces are rejected, so optional capabilities need
  capability-specific ports rather than leaking SDK objects;
- reconciliation stores and contract tests are required around mutations.

## Rejected alternatives

1. Provider SDK calls from simulation/domain code — rejected because gameplay would
   branch on a candidate and credentials/errors would enter the authority core.
2. One generic untyped `invoke` adapter — rejected because it hides ownership,
   idempotency, failure, and compatibility semantics.
3. Treating provider data as a mirror of canonical gameplay state — rejected
   because conflict resolution would create two authorities.
4. Selecting a candidate before equal scenarios — rejected by `CAP-01` and `U-04`.

## Failure and rollback

Adapter unavailability blocks or degrades only its capability according to the
contract. Established simulation ownership is not duplicated. Value mutations
fail closed when completion cannot be reconciled. Rollback disables/removes the
adapter, restores the neutral adapter configuration, reconciles operation IDs, and
verifies that durable identity and regional state are unchanged.

## Verification

- compile `al_server_core` with no adapter dependency;
- compile `al_provider_adapter_stub` as a one-way dependency;
- prove duplicate request deduplication and payload-drift conflict;
- run the Section 9 failure matrix from `MMO_Provider_Neutral_Contracts_v1.md` for
  every candidate;
- delete/disable the adapter and rerun core authority tests.

## Unresolved owner decisions

- hosting/provider selection or no-selection;
- which external capabilities, if any, are adopted;
- vendor, commitment, spend, cost, quota, regional exposure, and exit decisions;
- material changes to gameplay-authority boundaries.
