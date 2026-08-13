# Guild Inner-Realm City Season, Reset, and Ownership Banner Contract

## Scope and ownership

- Parent issue: https://github.com/yulee94/AnotherLife/issues/478
- Child issue: https://github.com/yulee94/AnotherLife/issues/483
- Primary delivery mode: A1 coordination/review contracts
- Related contracts: #478, #181, #481, #484, #477, #456, #459

This contract is documentation-only and defines the machine model for weekly inner-realm city competition, neutral reset, winner binding, and presentation ownership. No implementation, runtime, scene, save schema, UI, or shared-lock edits.

## Canonical constraints

1. City season is same-realm only.
2. Exactly one controlled owner snapshot per city per weekly cycle.
3. All existing controlled cities become neutral before committing the next contest ownership.
4. City ownership provides an effect hook only for later 3D reward-path modifiers.
5. Ownership and presentation are replayable, monotonic, idempotent, and crash-safe.
6. No direct Oathmark or resource mint in 2.5D mode.

## Required immutable IDs from #181

This contract consumes stable zone and policy IDs from #181 for:

- realm boundaries
- safe/beginner/protected zones
- city identity set and region adjacency constraints

No city geometry may be inferred from scene names.

## Core entities

### `CitySeasonHead`

- `citySeasonId`
- `weekWindowId` (stable weekly boundary)
- `phase`:
  - `CLOSING`
  - `RESET_STAGED`
  - `RESET_COMMITTED`
  - `CONTEST_OPEN`
  - `SETTLING`
  - `CLOSED`
- `revision`
- `allCitiesViewRevision`
- `contestsRevision`
- `ownerSnapshotRevision`

### `CityControlRecord`

- `cityId`
- `realmId`
- `weekWindowId`
- `ownerGuildId` (nullable)
- `controlState`:
  - `NEUTRAL`
  - `CONTESTING`
  - `CONTROLLED`
  - `REVERTING`
- `contestId`
- `winnerGuildId` (nullable)
- `benefitProfileRef`
- `bannerRef`
- `ownedBy` (immutable guild or neutral marker)
- `contestResultReceipt`

## Season boundary and reset sequence

Every weekly boundary executes fixed order:

1. `CLOSING`: close open contests from prior week
2. `RESET_STAGED`: construct full neutral city snapshot intent
3. `RESET_COMMITTED`: atomically commit all previously controlled cities to neutral in one CAS step
4. `CONTEST_OPEN`: contest intake window opens for that week
5. `SETTLING`: contest results aggregate and winner arbitration occurs
6. `CLOSED`: active week result is committed

Invariant:

- During `RESET_STAGED` and `RESET_COMMITTED`, no city can be reported as controlled by a previous guild.
- `RESET_COMMITTED` must be complete and visible before any new ownership result is accepted.

## `CitySeasonState`

Per city, `CityControlRecord` lifecycle:

- `UNKNOWN` -> `NEUTRAL`
- `CONTEST_OPEN` -> `CONTESTING`
- `CONTESTING` -> `CONTROLLED` (winner committed)
- `CONTROLLED` -> `REVERTING` (on weekly boundary)
- `REVERTING` -> `NEUTRAL`

`CONTROLLED` to winner transition can only occur from `CONTESTING` and only once per week per city.

## Ownership guarantees

- One owner limit: exact one guild owner per city at any point in a given `weekWindowId`.
- No shared ownership when contest tie/ambiguity exists; ties become neutral until next week unless a tie-breaker contract exists.
- `REVERTING` is authoritative and irreversible once boundary commit starts.

## Banner and visual ownership binding

- `city-control` includes mutable `bannerRef` but replayed only on `controlState=CONTROLLED`.
- Missing/unknown/stale banner or moderation block falls back to `realmSafeBannerRef` (safe symbol).
- Neutral cities render approved realm symbol.
- Banner copy/asset is non-authoritative for gameplay logic.

## Inputs and commands

1. `StartCitySeasonBoundary`
   - Input: `weekWindowId`, `realmScope`, `sourceDigest`
   - Precondition: not already in active boundary for same week
   - Output: `CLOSING` head snapshot

2. `CommitNeutralReset`
   - Input: `weekWindowId`, `expectedRevision`, `allCityIds`
   - Output: `RESET_COMMITTED` and ownership snapshot containing only neutral entries

3. `OpenCityContestWindow`
   - Input: `weekWindowId`, `contestPolicyRef`, `deadline`
   - Output: `CONTEST_OPEN`

4. `SubmitCityContribution`
   - Input: `cityId`, `guildId`, `realmId`, `contributionDigest`
   - State: accepted only during `CONTEST_OPEN`

5. `FinalizeCityResult`
   - Input: `cityId`, `winnerGuildId`, `winnerReasonDigest`
   - Preconditions: week in `SETTLING`, all inputs and revisions fresh
   - Output: `CONTROLLED` record with optional `benefitProfileRef`

6. `RecordBannerChange`
   - Input: `cityId`, `weekWindowId`, `ownerGuildId`, `bannerRef`
   - Guard: only when city not in reset state and owner has control

7. `CloseSeasonForWeek`
   - Input: `weekWindowId`
   - Output: `CLOSED` final receipt

## Safety and anti-ambiguity rules

- Contributions from stale weekly cycles are ignored.
- Same city cannot have two winner commits in same week.
- All transitions are deterministic and produce terminal immutability receipts.
- Unknown outcomes produce explicit reconcile command and zero gameplay mutation.
- City ownership never bypasses realm-safe zoning rules.
- No city ownership changes outside week boundary unless replaying an already committed snapshot.

## Inter-system boundaries

- This contract does not define:
  - #180 combat, damage, or PvP logic
  - #181 safe-zone definitions themselves
  - #461 mode-switch behavior
  - #460/#450 save schemas
  - #477 Oathmark minting policy
- It only defines ownership record and banner binding required by later downstream contracts.

## Per-city contest/perk semantics (deferred)

City control may provide a 3D-dungeon reward modifier profile in later source contracts, but:

- no city-control mint in 2.5D
- no direct Oathmark mint in this lane
- no kingdom building, training, or research cost changes in this lane

## Test matrix

1. `CITY-001`: week boundary transition sequence order is strict
2. `CITY-002`: neutral reset applies to all prior controls atomically
3. `CITY-003`: no mixed owner at same city/week
4. `CITY-004`: tie/no-result yields neutral owner
5. `CITY-005`: stale contest record replay is rejected/ignored
6. `CITY-006`: banner missing -> safe fallback
7. `CITY-007`: banner drift without authority -> no visual authority change
8. `CITY-008`: boundary commit idempotence under retry
9. `CITY-009`: replay of winner digest is deterministic
10. `CITY-010`: ownership receipt mismatch is terminal unknown requiring reconcile

## Nonclaims

- No runtime guild UI, map map-mode toggles, movement, relocation, or alliance war logic.
- No performance, package, or device claims.
- No runtime implementation in this PR.
