# ADR 0004 — Modular deployable, asynchronous persistence, and explicit ownership

Status: accepted technical boundary

Date: 2026-08-31

Decision owner: game owner for material architecture, capacity, exposure, and operational changes

Review state: implementation and independent contract review required before `t_9ba5dbe2` completion

## Context

The foundation has an engine-free authority prototype but no production backend.
Premature service decomposition would add network, consistency, deployment, and
owner-plus-AI operational failure modes before measured need. Conversely, putting
databases, queues, object stores, or global control planes in the simulation tick
would make external failure a gameplay-time failure.

## Decision

Begin with one modular regional server deployable containing explicit gateway,
session, placement integration, simulation, persistence-intent, and observation
modules. Preserve module boundaries and stable contracts, but split a module into a
separate service only when measured scaling, security isolation, independent
lifecycle, failure containment, or ownership requires it.

- Active simulation state lives in the sole authoritative process owner.
- Durable value state lives in one regional transactional authority.
- Simulation emits bounded asynchronous persistence intents; the movement/combat
  tick never synchronously waits for SQL, cache, event bus, object storage,
  analytics, or a global control plane.
- Domain mutation plus outbox record commits atomically. Consumers deduplicate by
  event/operation ID. Direct dual-write is prohibited.
- Presence, route hints, and query projections are reconstructible.
- Large snapshots, replays, and artifacts are asynchronous blobs with hashes and
  metadata, not character/economy authority.
- External process placement and orchestration manage lifecycle only. They are not
  the ownership oracle, packet router, or gameplay transaction protocol.
- Cross-cell ownership uses monotonic fencing epochs and explicit handoff; no state
  is writable in two cells.

## Consequences

Positive:

- local calls serve the first vertical slice while contracts remain extractable;
- simulation correctness is independent of provider and persistence latency;
- backpressure and failure are explicit instead of hidden in unbounded queues;
- future service extraction has a measured boundary and compatibility contract.

Costs:

- one deployable requires disciplined module ownership and bounded queues;
- persistence backlog, admission, and fail-closed value behavior must be designed;
- extraction later needs protocol fixtures and migration evidence.

## Rejected alternatives

1. Service per domain from day one — rejected without measured scaling/isolation
   evidence and because distributed failure would arrive before product proof.
2. Database as per-tick world state — rejected because consensus/storage latency
   would enter movement/combat correctness.
3. Cache or event bus as durable authority — rejected because eviction,
   duplication, asynchronous replication, and replay cannot preserve ledger truth.
4. Orchestrator as ownership oracle — rejected because process placement does not
   provide entity/objective single-writer semantics.
5. Best-effort dual-write to database and broker — rejected due to split success.

## Extraction gate

A module may become a service only when the evidence packet identifies:

- the measured bottleneck or isolation requirement;
- sole state owner and transaction boundary;
- versioned request/event contract and compatibility window;
- idempotency, ordering, retry, timeout ambiguity, and reconciliation behavior;
- bounded queues, backpressure, load shedding, and degraded behavior;
- observability, failure injection, rollback, and data migration;
- owner-plus-AI operational impact and explicit owner approval where material.

## Failure and rollback

Queue pressure cannot block the tick indefinitely. Expired/non-critical work is
shed according to the domain policy; durable value mutations fail closed when their
intent cannot remain recoverable. A failed extraction rolls traffic back to the
in-process module using the last compatible contract and reconciles operation and
outbox positions. It does not restore a second authority or destructively rewrite
owner-reserved state.

## Unresolved owner decisions

- exact transport, persistence, cache, event, object, orchestration, and
  observability implementations;
- exact service-extraction points and deployment topology;
- exact queue/service/recovery objectives and capacity thresholds;
- any provider, vendor, commitment, spend, cost ceiling, or production exposure.
