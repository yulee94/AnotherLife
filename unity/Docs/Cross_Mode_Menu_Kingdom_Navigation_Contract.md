# Cross-Mode Menu and Kingdom Navigation Contract

## Scope and authority

- Mode: **Codex coordination/review (docs-only)**.
- Parent issue: [#461](https://github.com/yulee94/AnotherLife/issues/461)
- Dependencies: [#467](https://github.com/yulee94/AnotherLife/issues/467), [#450](https://github.com/yulee94/AnotherLife/issues/450), [#477](https://github.com/yulee94/AnotherLife/issues/477), [#479](https://github.com/yulee94/AnotherLife/pull/479), [#181](https://github.com/yulee94/AnotherLife/issues/181), [#174](https://github.com/yulee94/AnotherLife/issues/174), [#180](https://github.com/yulee94/AnotherLife/issues/180), [#478](https://github.com/yulee94/AnotherLife/issues/478), [#480](https://github.com/yulee94/AnotherLife/issues/480).
- Supersession: unmerged draft PR #485 is historical overlap, not a dependency.
  This reconciled file retains its valid shared-menu clauses, replaces its
  conflicting private-world assumptions, and must not be merged alongside it.

### Purpose

Define the contract for switching modes and navigation surfaces requested by the user:

- One shared menu entry point for all transitions between 3D play and 2.5D kingdom management.
- World map / kingdom view separation inside kingdom mode.
- Bounded future intents for portal/teleport/castle relocation/army redeploy.
- Consume the owner-decided private-kingdom authority in
  `Private_Kingdom_Save_And_State_Synchronization_Architecture.md`.
- No runtime implementation authority is defined in this document.

### Non-goals

- No runtime code, scene/prefab edits, save/DB schema changes, #450 locked lockfiles edits, economy/economy migration edits, PvP damage model changes, or movement teleport implementation in this lane.
- No direct scene-name based authority and no fake completion claims.
- No release/asset/balance approval.

## Terminology and invariants

- `Adventure3D`: existing public-world real-time combat/control game mode.
- `Kingdom2_5D`: isolated, owner-only private-kingdom management mode.
- `KingdomView`: kingdom board/management subview.
- `WorldMap`: world-map subview under kingdom mode.
- `PublicAvatarAnchor`: the server-simulated public-world avatar that remains
  vulnerable while its owner is in `Kingdom2_5D`; it is not player-controlled
  or co-rendered as a second active mode.
- `ManagementLease`: the revocable server-issued binding between one account,
  public-avatar session, and private kingdom.
- `MODE_SWITCH`: request/approval to leave one top-level mode and enter the other.
- `VIEW_SWITCH`: request/approval to switch `KingdomView <-> WorldMap` while in kingdom mode.

Invariant:

1. `Mode switch != View switch != Travel != Relocation != Redeploy != Avatar movement`
2. `WorldMap` is **preview-only** unless a dedicated travel/relocation feature is approved in separate issues.
3. Menu-only global mode switching.
4. Unlock and entry must be predicate-driven by accepted narrative/identity state, never by scene name or local guesses.
5. `Kingdom2_5D` and `Adventure3D` world instances are mutually exclusive.
   Only immutable data/asset caches and the neutral transition UI may overlap;
   one instantiated world must be fully torn down before the other is created.
6. The private kingdom has no visitors, enemies, public-world coordinates,
   physical-world buildings, or destructible public-world structures.
7. The public avatar remains under public-world combat authority. A qualifying
   interruption revokes the management lease and forces return to
   `Adventure3D`.
8. Keyboard `B` opens the construction dock only after `Kingdom2_5D` is
   active. Controller `B` remains Back. Neither input performs `MODE_SWITCH`.

## SharedMenuReadModel

Defines menu entries and their availability in each top-level mode.

### Module IDs

| ID | Machine ID | Notes |
|---|---|---|
| M1 | `MENU_MODULE_INVENTORY` | Inventory panel/module |
| M2 | `MENU_MODULE_CHARACTER_STATS_EQUIPMENT` | Character and equipment view |
| M3 | `MENU_MODULE_SKILL_SETS` | Skill and build view |
| M4 | `MENU_MODULE_QUESTS` | Active/completed quest and objective navigation |
| M5 | `MENU_MODULE_KINGDOM_MANAGEMENT` | Menu entry to enter/exit Kingdom2_5D |
| M6 | `MENU_MODULE_SETTINGS` | Settings and account actions |

### Availability reasons

`Available`, `LockedNarrative`, `BlockedTransient`, `BlockedDependency`, `Hidden`.

- `MENU_MODULE_KINGDOM_MANAGEMENT` is `LockedNarrative` until quest/crownship predicate is satisfied.
- `LockedNarrative` reason maps to user-facing lock messaging in later implementation.

## Shared-menu-only top-level transition contract

### Allowed modes

| From | To | Source predicate |
|---|---|---|
| `Adventure3D` | `Kingdom2_5D` | `LordshipUnlocked` from accepted #479 chain |
| `Kingdom2_5D` | `Adventure3D` | `CharacterModeReturnAllowed` from accepted projection/reload predicate |

### Core state machine

`MODE_SWITCH_REQUEST` can be in states:

- `IDLE`
- `VALIDATING`
- `READY_TO_SWITCH`
- `COMMITTING`
- `SUCCEEDED`
- `REJECTED`
- `CANCELLED`
- `INTERRUPTED`

### Transition input and results

| Action | Required context | Success result |
|---|---|---|
| `RequestSwitch` | menu entry focus, session context, projection snapshot, user confirmation | `SWITCH_COMMIT`
| `Cancel` | in-flight request | `SWITCH_CANCELLED`

| Failure result | Cause |
|---|---|
| `SWITCH_REJECTED_DEPENDENCY` | lock not ready, save projection missing, quest predicate absent |
| `SWITCH_REJECTED_STATE` | hostile/combat/unsafe context |
| `SWITCH_REJECTED_ZONE` | banned/city/beginner/safe-zone rule |
| `SWITCH_REJECTED_SYSTEM` | profile/save/builder unavailable |
| `SWITCH_INTERRUPTED_PUBLIC_AVATAR` | the public avatar received a qualifying interruption before or during private management |

### Required request envelope

`ModeSwitchRequestV1`

- `requestId` opaque token
- `accountId` opaque account scope
- `publicAvatarSessionId` opaque current public-avatar session
- `managementSessionId` opaque and empty only for a new entry request
- `fromMode`
- `toMode`
- `correlationId`
- `originStateDigest`
- `requestedByInputClass` (pointer/keyboard/controller)
- `timestampUtc`

### Required result envelope

`ModeSwitchResultV1`

- `requestId`
- `resultCode`
- `reasonCode`
- `fromMode`
- `toMode`
- `snapshotDigestBefore`
- `snapshotDigestAfter`
- `failedGuard`
- `managementSessionId`
- `publicAvatarGeneration`
- `interruptGeneration`

## Shared menu and mode restoration semantics

1. On successful `SUCCEEDED`, the game restores the exact validated
   camera/input state for the destination mode. It never keeps both world
   instances resident or enables both input maps together.
2. On `REJECTED`, active mode, input, camera, and context remain unchanged.
3. On `CANCELLED`, request context is invalidated with no mode drift.
4. While `Kingdom2_5D` is active, the public avatar remains server-simulated,
   public-world vulnerable, and unable to accept player movement/combat input
   from that management session.
5. Return to 3D restores the latest authoritative public-avatar snapshot; it
   never derives a transform from private-grid state and never moves a private
   building into the public world.
6. A qualifying interruption preempts placement, detail panels, and modals,
   revokes the lease, and returns to 3D immediately.
7. An already-in-target request is idempotent: it returns `ALREADY_IN_MODE`
   with no scene, save, lease, camera, or input mutation.
8. Both HUD families may crossfade only during the short transition. After
   entry settles, combat HUD collapses and only essential shared status
   remains.

## Kingdom2_5D local subview switch contract

### State machine

`KINGDOM_VIEW_STATE`: `KingdomView`, `WorldMap`.

### Envelope

`KingdomSubviewRequestV1`

- `requestId`
- `sessionScope`
- `originMode` (must be `Kingdom2_5D`)
- `fromSubView`
- `toSubView`
- `requestSource` (pointer/keyboard/controller)

### Results

- `VIEW_SWITCH_SUCCEEDED`
- `VIEW_SWITCH_REJECTED`

A failed request never edits authoritative game state.

## World map query contract (future query lane)

`WorldMapQuery` exposes read-only data only:

- visible regions
- region ownership summary
- markers/points of interest
- safe destination candidates for preview

Output: `WorldMapQueryResultV1` with:

- `regionIds[]`
- `markerIds[]`
- `ownershipState`
- `isSafeZone`

No commit or movement side effect here.

## Preview destination envelope

`WorldMapDestinationPreviewV1`

- `regionId`
- `markerId`
- `riskBand`
- `ownershipHints`
- `sourceContext`

No command to move avatar/castle.

## Deferred/intents (separate lanes)

These are intentionally out of scope and must not be invoked by this contract:

- `RequestAvatarTravel`
- `RequestPortalTravel`
- `RequestKingdomRelocation`
- `RequestArmyRedeploy`

Any future implementation must define separate issue/contract and explicit economy, confirmation, cooldown, safety, anti-abuse, and rollback.

## Lordship and unlocking policy (read-only contract use)

A 2.5D unlock is only from completed and persisted acceptance chain defined in #479 + #467.

- No kingdom-mode unlock from `HasCommittedRealm`.
- No unlock from raw realm selection, class, or save flags.

Reason codes for menu availability:

- `REASON_LOCKED_BY_NARRATIVE`
- `REASON_QUEST_PENDING`
- `REASON_DEPENDENCY_MISSING`
- `REASON_SESSION_UNREADY`

## PvP / social safety intersection (contract reference)

This contract consumes social and war-safe-zone predicates from:

- #480 safe-targeting and same-team immunity rules
- #478 family social model for guild/alliance membership
- #181 world-region safety IDs

Switching is blocked by active war/unsafe-zone predicates only where the owning domain explicitly authorizes it.

Once management is active, the avatar remains vulnerable regardless of the
private kingdom's owner-only/no-enemy presentation. At minimum, committed
positive damage, death, session replacement, forced world transition, or loss
of public-avatar authority revokes the lease. Further interrupt types remain
owned by their combat/session contracts.

## Cross-mode journey and retry behavior

### User flow (planned)

`Boot` → `Realm` → `Race/Class` → `Customization` → `Username` → `Tutorial` → `OMEN_1` → `MQ_C1_PROOF_OF_WORTH` → `LordshipUnlocked` → shared menu shows `MENU_MODULE_KINGDOM_MANAGEMENT` as Available → user invokes switch.

### Retry behavior

- One request in-flight at a time.
- Repeating the same valid request after success is idempotent and returns `ALREADY_IN_MODE`.
- Repeating after failure must re-run all guards.
- Reusing a revoked/expired management lease fails even when the kingdom
  revision is current.
- A client that ignores an interruption cannot issue further kingdom commands;
  server revocation is the authority.

## Compatibility and conflict checks

- `KingdomSceneController` must not be treated as the global gate for top-level transitions.
- Menu and mode switch behavior must be separate from cinematic, realm selection, and legacy UI surfaces.
- Existing `HasCommittedRealm` is a profile state hint only and is not authoritative for menu unlock.
- `CH01` prerequisite remains as explicit progression input until a dedicated, accepted lordship gate supersedes it.
- Private Kingdom, Guild City, and Warzone Stronghold are three distinct
  namespaces. This menu entry targets only the account-owned private kingdom.
- Private buildings exist only in the 2.5D simulation; Guild City and Warzone
  Stronghold physical-world rules cannot be inferred here.

## Required acceptance for implementation handoff

A future implementation can begin only after:

1. This contract is approved and versioned under #461.
2. #467 onboarding and C1 unlock chain accepted.
3. #450 shared lock protocol authorizes persistence/state projection.
4. #181 world-safe-zone IDs are versioned and queryable.
5. #477 economy naming and non-production currency source contract accepted.
6. The private-kingdom snapshot, lease, interruption, and delta semantics in
   `Private_Kingdom_Save_And_State_Synchronization_Architecture.md` pass
   independent review.

## Unperformed checks to record

- Runtime mode-switch unit coverage
- UI/controller/accessibility behavior
- Device parity on PC/Android
- Save projection persistence tests
- Forced-interruption and public-avatar restoration tests
- Private/Guild City/Warzone namespace-separation tests
- Build/distribution evidence

## Appendices

- A1: `ModeSwitchRequestV1` and `ModeSwitchResultV1` field list above
- A2: `KingdomSubviewRequestV1` and `KingdomSubviewResultV1` field list
- A3: `WorldMapQueryV1` and `WorldMapDestinationPreviewV1` field list
