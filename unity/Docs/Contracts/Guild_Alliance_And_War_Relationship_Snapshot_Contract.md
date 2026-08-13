# Guild Alliance and War Relationship Snapshot Contract

Status: coordination/review only
Status of readiness: PREPARED

This contract defines immutable social-relationship authority inputs for Guild/Clan social graph features (membership already defined in #481).
It is intended for use by a future PvP hostility/effect gate as an input-only source of effective relations.

## 1) Scope and non-goals

### In scope
- Alliance proposal/acceptance lifecycle and effective revisioned snapshots.
- War declaration/acceptance lifecycle and effective revisioned snapshots.
- Operation identity + idempotency + terminal XOR + unknown-outcome reconciliation model.
- Explicit ordering and precedence constraints for downstream combat eligibility.
- Fixture families for duplicate/fork/stale/malformed/cross-scope recovery.

### Not in scope
- Runtime targeting math or damage/effect math.
- Safe-zone policy, city control, teleport/reentry, dungeon entry, loot, cooldown pricing, economy, perks, perks-on-war, or menu/HUD.
- Save schema changes, network transport, persistence tables, account/auth service implementation.
- Alliance/war visuals, icons, names, color semantics, UI wording.

## 2) Upstream dependencies

Consumes:
- `#478` (social architecture boundary)
- `#481` (Guild_Membership_And_Role_Snapshot_Contract)
- `#475` (operation ingress / replay / unknown-outcome model)
- `#181` (forced-non-PvP zone-policy IDs as upstream precedence input)

Produces input snapshots for:
- `#480` hostile-target/effect gate

## 3) Domain identifiers

- `GuildId` opaque (32-byte)
- `AllianceSnapshotId` = sha256 over canonical alliance row multiset + revision
- `WarSnapshotId` = sha256 over canonical war row multiset + revision
- `OperationCommitment` = sha256 of request fields
- `RelationSnapshotRevision` = monotonic u64
- `TenantId` / `CharacterBindingId` / `AccountBinding` are opaque keys bound externally by service owner.

## 4) Canonical state machine (effective relation snapshots)

`GuildAllianceSnapshot`:
- `ABSENT`
- `ACTIVE`
- `PENDING`
- `DISSOLVING`
- `DISBANDED`

`GuildWarSnapshot`:
- `ABSENT`
- `PROPOSED`
- `ACCEPTED`
- `ACTIVE`
- `ENDORSEMENT_PENDING`
- `ENDING`
- `ENDED`
- `COOLDOWN`
- `COOLDOWN_EXPIRED`

Invariant:
- Snapshot rows are immutable by revision.
- For a given unordered guild pair, at most one effective `ACTIVE` relation row per relation kind at one revision.
- War/Alliance relation for each row must be bounded by valid membership snapshot revisions.

## 5) Operation model

Each operation has:
- fixed ordered tuple `(tenantScope, initiatorBinding, guildA, guildB, relationKind, expectedRevision)`
- operation commitment and semantic fingerprint
- ingress binding state: `REGISTERED | IN_PROGRESS | TERMINAL_REJECTED | TERMINAL_COMMITTED`
- terminal result immutably recorded once

Unknown outcome handling:
- `INDETERMINATE` before commit confirmation is permitted and must reconcile against operation ledger.
- reconciliation returns either committed terminal or terminal rejection with zero speculative effects.

## 6) Snapshot contract

Alliance snapshot row fields include:
- `rowId`
- `pairId` (unordered guild pair)
- `guildA`, `guildB` (non-null, ordered-normalized)
- `state`
- `relationMembershipRevision`
- `effectiveFromRevision`
- `effectiveToRevision`
- `operationCommitment`
- `issuedBy`
- `issuedAtEpochMs`

War snapshot row fields include:
- `rowId`
- `pairId` (unordered guild pair)
- `guildA`, `guildB` (non-null, ordered-normalized)
- `state`
- `startWindowEpoch`
- `endWindowEpoch`
- `relationMembershipRevision`
- `cooldownWindowEpoch`
- `operationCommitment`
- `issuedBy`
- `issuedAtEpochMs`

Each snapshot row must carry:
- `snapshotRevision`
- `snapshotDigest`
- `schemaVersion`
- `priorSnapshotDigest`

Missing/stale snapshot references fail closed.

## 7) Precedence matrix for hostile gate consumption

The snapshot does not apply effects directly.
It only constrains predicates consumed by downstream combat/effect logic.

- Same guild pair: never hostile.
- Same effective alliance: no hostile-efficacious interaction.
- `ACTIVE` war may mark relation as `WAR_ACTIVE` but cannot override safe-zone or same-guild/same-alliance immunity.
- War precedence applies only after same-guild/alignment checks and only against confirmed row+revision.
- Unknown/ambiguous/malformed relation data has no hostile authority.

## 8) Concurrency and replay rules

- Same `(tenantScope, initiatorBinding, operationCommitment)` with different fingerprints is a nondisclosing collision and no mutation.
- Same operation replay with equal fingerprint is deterministic idempotent.
- Cross-tenant/boundary mismatch never returns relation deltas.
- Unknown/partial outcomes must never create relation side effects.

## 9) Minimal fixture set

1. duplicate replay idempotence
2. mismatch fingerprint rejection
3. same-op different fingerprint collision
4. stale revision rejection
5. malformed unordered pair rejection
6. cross-scope mismatch nondisclosure
7. war pending→accepted→active
8. war active→ending→ended→cooldown
9. alliance proposal→accept
10. alliance cancel / disband
11. active relation unknown outcome reconciliation
12. safe snapshot gaps rejected

## 10) Consumption contract

Downstream consumers accept only:
- valid committed snapshot ids
- tuple `(snapshotId, snapshotRevision, snapshotDigest)`
- and explicit relation revision match with membership snapshot revision.

Any call using uncommitted/stale/unknown snapshots is denied.

## 11) Implementation fence (future)

This document defines only contracts and tests.
No service, save, DB, UI, runtime, economy, movement, travel, or PvP damage code is included.
