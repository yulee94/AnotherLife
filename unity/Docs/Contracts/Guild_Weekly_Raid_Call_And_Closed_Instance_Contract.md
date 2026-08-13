# Guild Weekly Raid Call And Closed Instance Contract

## Scope and ownership

- Parent issue: https://github.com/yulee94/AnotherLife/issues/478
- Child issue: https://github.com/yulee94/AnotherLife/issues/482
- Primary delivery mode: A1 coordination/review contracts
- Related contracts: #481 (membership/roles snapshot), #484 (alliance/war snapshots), #480 (hostility/effect eligibility), #181 (world zone policy), #477 (economy), #456 (open dungeon lore), #458 (realm perks)

This contract is planning-only source documentation. It does not implement runtime, UI, scenes, saves, networking, or server storage.

## Design constraints

1. One exact parent guild must initiate raid content by explicit command.
2. Membership must use accepted #481 snapshots; no implicit role inference.
3. Raid participation is opt-in and explicit.
4. A weekly raid is a closed instance event, separate from open inner-realm geometry and from #456 dungeons unless explicitly linked by later source decisions.
5. Targeting/hostility logic and safe-zone protections are consumed from #480 and #181.
6. No direct rewards, perks, economy minting, or Oathmark accounting in this contract.
7. Replay-safe, idempotent, and crash-consistent semantics.

## Constants and IDs

Machine constants are explicit and opaque for this lane:

- `GUILD_RAID_WINDOW_SECONDS`: 1800 (30 minutes)
- `GUILD_RAID_MIN_PARTICIPANTS`: integer >= 1
- `GUILD_RAID_MAX_PARTICIPANTS`: integer > `GUILD_RAID_MIN_PARTICIPANTS`
- `GUILD_RAID_ROSTER_FREEZE_WINDOW_SECONDS`: integer >= 0
- `GUILD_RAID_CLOSED_INSTANCE_TIMEOUT_SECONDS`: integer > 0
- `GUILD_RAID_BOSS_PROFILE_COUNT`: 4

All constants are source-decided values; only the above are enumerated in this contract as required names.

## Immutable entity model

### `GuildRaidCallSnapshot`

- `snapshotId`
- `realmId`
- `guildId`
- `initiator` (must be current MASTER/authorized OFFICER by #481)
- `weeklyCycleId`
- `plannedStartTime`
- `acceptDeadline`
- `state`
- `bossProfileSetId` (reference to 4-slot roster from a separate content source)
- `activeBossProfileId` (exactly one selected per cycle after closure)
- `revision`
- `acceptPolicyDigest`
- `closedInstanceContractDigest`

### `GuildRaidInvite`

- `snapshotId`
- `memberId`
- `inviteState` (`PENDING`/`JOINED`/`DECLINED`/`WITHDRAWN`/`TIMEOUT`)
- `requestedAt`
- `responseAt`
- `responseSource` (`join`/`decline`/`withdraw`)

### `GuildRaidClosedInstanceAdmission`

- `snapshotId`
- `instanceId`
- `teleportState` (`REQUESTED`/`ENTERED`/`RETURNED`/`FAILED`)
- `returnReason`
- `preRaidLocation` (`realmId`,`sceneId`,`locationHint`)
- `entryReceiptId`
- `rollbackReceiptId`

## `RaidCallState`

Canonical terminal states:

- `DRAFT`
- `ANNOUNCED`
- `ACCEPTING`
- `READY`
- `COUNTDOWN`
- `ACTIVE`
- `COMPLETED`
- `CANCELLED`
- `FAILED`
- `EXPIRED`

State transitions:

- `DRAFT -> ANNOUNCED` when initiator creates snapshot and receives acceptance of preconditions.
- `ANNOUNCED -> ACCEPTING` when planning window opens and all final checks pass.
- `ACCEPTING -> READY` when deadline passes and accept criteria for this cycle pass.
- `READY -> COUNTDOWN` when minimum participants and closed-instance precheck pass.
- `COUNTDOWN -> ACTIVE` when all preconditions are committed and summon packet is emitted.
- `ACTIVE -> COMPLETED` when close condition and reward handoff complete.
- `ACCEPTING -> EXPIRED` when deadline passes and minimum participation or preconditions fail.
- `* -> FAILED` for irrecoverable validation/availability failure after command issuance.
- `* -> CANCELLED` on authoritative explicit cancel, duplicate-cycle conflict, or authoritative initiator revoke.

All transitions are exact single-shot; duplicates replay to committed evidence or terminal-noop.

## Commands

All commands are pure machine intentions; runtime implementations must consume these as opaque commands and commit explicit receipts.

1. `CreateRaidCall`
   - Input: `guildId`, `weeklyCycleId`, `realmId`, `initiator`, `bossProfileSetId`, `acceptWindowSeconds`
   - Preconditions:
     - Caller role is `MASTER` OR explicit role map from downstream contract for this lane.
     - `guildId` has no active `GuildRaidCall` in same `weeklyCycleId`.
     - `weeklyCycleId` is current or future and not already concluded.
     - `bossProfileSetId` references approved 4-profile set with one active selection.
   - Result: `DRAFT` snapshot with planned start/accept deadline.

2. `AdvanceRaidCall`
   - Input: `snapshotId`, optional `expectedRevision`
   - Reconcile timer/acceptance and advance deterministic state machine.

3. `RecordInviteDecision`
   - Input: `snapshotId`, `memberId`, `decision`
   - Decision: `join` | `decline` | `withdraw`
   - Preconditions:
     - `memberId` snapshot-bound membership active and role-compatible.
     - State allows participant changes.
   - Result: invite row transition and deterministic snapshot counter.

4. `AttemptEnterRaid`
   - Input: `snapshotId`, `memberId`, `characterId`
   - Preconditions:
     - `snapshotId` state `READY/COUNTDOWN`, `memberId` invited and `JOINED`.
     - Safe-zone and anti-combat-state guards at command boundary pass.
     - Closed-instance policy snapshot and roster snapshot current.
   - Result: summons to closed-instance admission `REQUESTED`.

5. `FinalizeRaidClose`
   - Input: `snapshotId`
   - Computes terminal outcome path: `COMPLETED`/`CANCELLED`/`FAILED`/`EXPIRED` and emits one immutable terminal.

6. `ReturnFromRaid`
   - Input: `snapshotId`, `memberId`, `characterId`
   - Always available when member is admitted and not failed lockout.

## Invite policy

- Opt-in model: only explicit `join` sets `JOINED`.
- Silence is retained as `PENDING`, not consent.
- Expiry transitions unresolved `PENDING` to `TIMEOUT`.
- `withdraw` allowed before instance lockstep and before terminal states.
- At least one of MASTER/OFFICER may revoke/cancel if in active phase.

## Deterministic roster semantics

- Roster snapshot is computed as of `READY` and frozen for that closed-instance cycle.
- No new members entering after freeze can gain active participation in the same cycle.
- Frozen roster includes deterministic `memberId`/`characterId` tuple and revision.
- Roster freeze mismatch is terminal failure path, never silent expansion.

## Closed-instance transfer model (closed, not same as 3D scene movement)

Transfers are explicit and reversible:

- `SummonIn`: one command path that writes `preRaidLocation`, creates `entryReceiptId`, and records `ENTERED` on success.
- `SummonOut`: one command path that restores `preRaidLocation`, writes `returnReason`, and records `RETURNED`.

No command path in this contract may perform:

- direct `Teleport` from open-world coordinates without pre-validated state,
- combat scene swap without command, or
- implicit return on any non-terminal command failure.

Transfer invariants:

- `entryReceiptId` and `rollbackReceiptId` are immutable and replay-safe.
- A member can have at most one open admission record per snapshot.
- Teleport paths do not change guild/membership state.

## Failure and safety matrix

### Missing/invalid preconditions

- malformed identifiers, stale revisions, unknown memberships, stale weekly cycle, invalid command source -> zero state, terminal rejection.

### Timer-driven outcomes

- acceptance expires -> `EXPIRED` with explicit reason.
- precheck timeout -> `FAILED` if admission policy cannot be satisfied safely.

### Concurrency and deduplication

- same command duplicate -> same committed terminal or idempotent prior non-terminal.
- conflicting commands with same snapshot -> deterministic precedence by operation timestamp and snapshot revision.
- conflicting cycle operations -> explicit conflict rejection.

### Connectivity and replay

- command replay after loss returns committed snapshot and no duplicate transfer side effects.
- unknown outcome must expose explicit reconcile command and never infer participant transfer.

## Cross-contract consumption

- Membership and permissions consume immutable #481 snapshot rows.
- Alliance/warr relations consume #484 when applicable; this lane does **not** require alliance for opt-in.
- #480 hostility/effect eligibility applies only inside instance combat systems; this lane requires only safe-mode/anti-combat prechecks for entry/return.
- Zone policy and forced-no-entry zones consume #181.
- Economy/minting effects consume #477 and occur only if later runtime contracts explicitly route them.

## Separation from open dungeon and city systems

- This lane defines the weekly guild muster + closed instance entry path only.
- It intentionally does not define #456 lore bosses, cooldowns, or identity behavior.
- It intentionally does not define #459 siege or #461 menu switching policy.
- Boss identity/rotation/content ownership are separate source-approved contracts.

## Test matrix (read-only planning fixtures)

1. `RAID-001`: legal create by Master -> ANNOUNCED
2. `RAID-002`: legal create by Officer (if policy accepted)
3. `RAID-003`: illegal create by non-role actor
4. `RAID-004`: duplicate create in same cycle rejected
5. `RAID-005`: invite/join/decline/witdraw determinism
6. `RAID-006`: silence remains PENDING (no consent)
7. `RAID-007`: accept window end causes READY/EXPIRED boundary deterministic
8. `RAID-008`: READY with zero participants -> CANCELLED or FAILED per policy
9. `RAID-009`: roster freeze mismatch failure
10. `RAID-010`: repeated summon in -> idempotent no duplicate
11. `RAID-011`: summon-in then disconnect -> recover return path
12. `RAID-012`: disconnect during ACTIVE -> preserved preRaidLocation
13. `RAID-013`: unknown outcome reconcile
14. `RAID-014`: simultaneous summon request collision
15. `RAID-015`: one-weekly-cycle close then late command rejection
16. `RAID-016`: closed-instance command with zone lock or combat lock rejection
17. `RAID-017`: terminal replay is exact once.

## Nonclaims

- No final boss identities, rewards, loot, XP, Oathmark, XP multipliers, perk values.
- No direct economy mint in contracts.
- No runtime scheduler implementation.
- No shared-file locks.
- No UI implementation.
- No build and no local test execution in this PR.

## Delivery guardrail

- Exactly one docs-only file on branch.
- No scenes, prefabs, runtime scripts, tests, save schema, or lock file edits.
- Branch must target `main` and keep one-path scope.
