# Guild Membership and Role Snapshot Contract

Status: coordination/review only
Primary lane: #478

This document defines the minimum stable social identity source for guild/clan logic in v1. It is intentionally limited to snapshot identity and membership/rule transitions.

## 1) Scope

- Define what makes a `GuildId` valid for hostility, raids, and alliance participation.
- Define role transitions (`MASTER`, `OFFICER`, `MEMBER`).
- Define one immutable snapshot format consumed by downstream combat/raid/ownership contracts.
- Do **not** define perks, war, alliance, rewards, economics, castle ownership, city control, or 3D movement.

## 2) Data model

```
type GuildId = sha256_opaque(32)
type MemberId = opaque(32)
type MembershipRecordId = sha256_opaque(32)
type MembershipSnapshotId = sha256_opaque(32)
type AccountBindingId = opaque(32)
type CharacterId = opaque(32)
type RealmId = opaque(32)
type Revision = u64
type TimestampMs = u64

enum GuildRole {
  MASTER,
  OFFICER,
  MEMBER
}

enum MembershipState {
  PENDING_ACCEPT,
  INVITE_PENDING,
  ACTIVE,
  PENDING_LEAVE,
  BANNED,
  INACTIVE
}
```

## 3) Opaque binding rule

No raw AccountId/ProfileId/handle/character-name may be authoritative. Contracts use only opaque IDs and immutable snapshot revisions.

Membership ownership binding requires:

- `accountBindingId`
- `characterId` (nullable if membership is pending)
- `guildId`
- `role`
- `realmId` (realm where the membership is evaluated)
- `membershipState`
- `isRealmBound` (boolean at snapshot issue time)
- `effectiveFromRevision`
- `snapshotRevision`

## 4) Membership operations and states

### 4.1 Allowed transitions

1. `CREATE_GUILD`: creates a new GuildId and initial membership with one MASTER.
2. `INVITE_MEMBER`: MASTER/OFFICER can invite one CharacterId into PENDING_ACCEPT.
3. `ACCEPT_INVITE`: Character accepts, state transitions to ACTIVE (`role=MEMBER` by default unless upgraded).
4. `REJECT_INVITE` / `CANCEL_INVITE`: terminally removes pending record.
5. `PROMOTE_OFFICER` / `DEMOTE_OFFICER`: MASTER only.
6. `TRANSFER_MASTER`: MASTER assigns successor MASTER (atomic); old MASTER becomes MEMBER.
7. `LEAVE_GUILD`: ACTIVE => PENDING_LEAVE (retains one revision), then INACTIVE.
7. `KICK_MEMBER`: MASTER/OFFICER can set non-master to BANNED (terminal for current cycle).
8. `DISBAND_GUILD`: MASTER can disband when membership policy permits.

### 4.2 Invariant constraints

- Exactly one active MASTER per guild snapshot.
- No transition if `revision` mismatches.
- No operation can target same `MemberId` and `GuildId` with different role in one revision.
- Same Account cannot be active member of more than one guild unless policy explicitly allows (default: single-active-membership).
- Character must resolve to committed AccountBinding before any role change that changes authority.
- Same-member duplicate submits are idempotent if same operation fingerprint; conflicting duplicates are collision and zero-mutation.

## 5) Snapshot contract

```
GuildMembershipSnapshot {
  MembershipSnapshotId snapshotId,
  Revision snapshotRevision,
  u64 issuedAtEpochMs,
  GuildId guildId,
  array<GuildMembershipRow> members,
  array<GuildRoleBoundary> roleRules,
  Revision priorRevision,
  Digest snapshotDigest
}

GuildMembershipRow {
  MemberId subjectMemberId,
  CharacterId characterId,
  AccountBindingId accountBindingId,
  GuildRole role,
  MembershipState state,
  TimestampMs joinedAtMs,
  TimestampMs updatedAtMs
}
```

Snapshots are immutable once emitted. Downstream systems consume committed snapshot ID + revision; stale IDs are rejected.

## 6) Governance and security

- All mutable operations are modeled as intent + commitment + terminal result.
- Operation outcomes are terminal XOR: either `COMMITTED` or `TERMINAL_REJECTED`.
- Unknown outcome may return `INDETERMINATE` and must reconcile to one of the two outcomes using a replayable receipt.
- Raw text names and social copy are out-of-band; machine predicates must use IDs only.

## 7) Non-goals

- No direct PvP effect logic (#480).
- No alliance/war relation math (#484).
- No safe-zone policy (#181).
- No economy/perks/XP/bonuses.
- No raid logistics.
- No UI copy or color semantics.

## 8) Integration contract

- `#480` consumes:
  - `guildId`
  - `member effective snapshot id`
  - `current membership state`
  - `role set` for same-guild immunity checks
- A runtime system may optionally project human-readable names through separate approved source lanes only.

## 9) Minimal required fixtures

- Create guild with one MASTER and two MEMBERS.
- Invite + accept and reject path.
- Promote to OFFICER and demote path.
- Duplicate op collision with same fingerprint.
- Cross-scope stale snapshot rejection.
- Revision gap rejection.
- Conflicting dual master rejection.
- Inactive/gone-character snapshot mismatch.

## 10) Dependency map

- Requires #478 stable source intent
- Consumed by #480
- Sequenced before #484/#482/#483 in social feature lanes
- Production persistence, economy, and sanctions are blocked by #450/#137 and corresponding authority stacks
