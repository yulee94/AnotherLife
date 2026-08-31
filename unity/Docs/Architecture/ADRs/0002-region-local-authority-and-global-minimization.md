# ADR 0002 — Region-local authority and minimized global identity

Status: accepted technical boundary

Date: 2026-08-31

Decision owner: game owner for region, platform, data-residency, recovery, and exposure changes

Review state: implementation and independent contract review required before `t_9ba5dbe2` completion

## Context

The approved roadmap requires canonical cross-platform identity while preserving
region-local authoritative gameplay, economy, social, and backups. A convenient
global database or provider account model could silently merge regional economies,
social graphs, realm membership, or gameplay state. Regional failure handling also
cannot create two writable owners.

## Decision

Separate a minimized global identity/link plane from regional authoritative data
planes.

Global plane MAY store only:

- canonical account identity;
- external platform-link references;
- eligibility, restriction, and consent references required to route/authenticate;
- minimized entitlement references needed before regional reconciliation;
- owning region and durable realm references, not their gameplay aggregates.

Regional plane owns:

- characters, progression, inventory, kingdoms, territory, objectives, rewards;
- currency, market, trade, economy settlement, and granted entitlements;
- guild/alliance/social graph, moderation cases, and regional communications state;
- operation deduplication, transactional outbox/inbox, backups, recovery, and audit;
- active simulation checkpoints and committed outcomes.

A regional aggregate has one home/owner. Routine cross-region synchronous writes,
global consensus in the simulation path, and replication that merges regional
social/economy graphs are prohibited. Cross-region public/minimized events are
asynchronous projections with explicit conflict behavior and no authority.

## Consequences

Positive:

- identity can span platforms without platform or provider IDs entering gameplay;
- regional economy/social isolation is enforceable and testable;
- a global control-plane outage does not become a synchronous battle dependency;
- recovery can prove one writable regional owner.

Costs:

- account hydration and entitlement reconciliation cross an explicit boundary;
- support and analytics need privacy-scoped projections rather than global joins;
- region moves, if ever approved after 1.0, require a separate migration protocol.

## Rejected alternatives

1. Globally replicated gameplay database — rejected because routine regional play
   would depend on cross-region consensus/conflict resolution.
2. Provider account ID as canonical account ID — rejected because linking and
   provider replacement would rewrite identity references.
3. Global social/economy projection promoted to writable state — rejected because
   asynchronous replication cannot preserve one regional authority.
4. Placement choosing realm/region — rejected because placement is routing, not
   durable identity authority.

## Failure and rollback

- Global-plane loss blocks new verification/routing unless an approved valid
  session continuation exists; it cannot alter regional state.
- Regional isolation rejects cross-region writes and preserves the region's sole
  owner/recovery policy.
- Ambiguous operations reconcile by regional operation ID before replay.
- Restore proves region, schema, owner epoch, deduplication, outbox position, and
  aggregate invariants before writes resume.
- Adapter rollback never moves realm membership or copies regional aggregates.

## Verification

- deny region-mismatched aggregate writes and placements;
- prove platform/provider identifiers are absent from gameplay aggregate keys;
- prove global projections cannot write regional ledgers/social graphs;
- inject global control-plane loss and regional isolation;
- restore a region and verify one writer plus operation/outbox continuity.

## Unresolved owner decisions

- exact identity linking, recovery, deletion, consent, and entitlement policies;
- exact Korea and North America launch subregion topology, plus any future expansion;
  the `REG-02` launch sequence remains Korea first, then North America;
- exact retention, recovery, restore, and migration compatibility objectives;
- any post-1.0 realm/region transfer policy;
- managed versus custom identity and regional datastore components.
