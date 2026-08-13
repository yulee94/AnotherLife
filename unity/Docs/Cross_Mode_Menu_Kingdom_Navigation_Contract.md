# Cross-Mode Menu and Kingdom Navigation Contract

## Scope and authority

- Mode: **Codex coordination/review (docs-only)**.
- Parent issue: [#461](https://github.com/yulee94/AnotherLife/issues/461)
- Dependencies: [#467](https://github.com/yulee94/AnotherLife/issues/467), [#450](https://github.com/yulee94/AnotherLife/issues/450), [#477](https://github.com/yulee94/AnotherLife/issues/477), [#479](https://github.com/yulee94/AnotherLife/pull/479), [#181](https://github.com/yulee94/AnotherLife/issues/181), [#174](https://github.com/yulee94/AnotherLife/issues/174), [#180](https://github.com/yulee94/AnotherLife/issues/180), [#478](https://github.com/yulee94/AnotherLife/issues/478), [#480](https://github.com/yulee94/AnotherLife/issues/480).

### Purpose

Define the contract for switching modes and navigation surfaces requested by the user:

- One shared menu entry point for all transitions between 3D play and 2.5D kingdom management.
- World map / kingdom view separation inside kingdom mode.
- Bounded future intents for portal/teleport/castle relocation/army redeploy.
- No runtime implementation authority is defined in this document.

### Non-goals

- No runtime code, scene/prefab edits, save/DB schema changes, #450 locked lockfiles edits, economy/economy migration edits, PvP damage model changes, or movement teleport implementation in this lane.
- No direct scene-name based authority and no fake completion claims.
- No release/asset/balance approval.

## Terminology and invariants

- `Adventure3D`: existing real-time combat/control game mode.
- `Kingdom2_5D`: kingdom management mode view.
- `KingdomView`: kingdom board/management subview.
- `WorldMap`: world-map subview under kingdom mode.
- `MODE_SWITCH`: request/approval to leave one top-level mode and enter the other.
- `VIEW_SWITCH`: request/approval to switch `KingdomView <-> WorldMap` while in kingdom mode.

Invariant:

1. `Mode switch != View switch != Travel != Relocation != Redeploy != Avatar movement`
2. `WorldMap` is **preview-only** unless a dedicated travel/relocation feature is approved in separate issues.
3. Menu-only global mode switching.
4. Unlock and entry must be predicate-driven by accepted narrative/identity state, never by scene name or local guesses.

## SharedMenuReadModel

Defines menu entries and their availability in each top-level mode.

### Module IDs

| ID | Machine ID | Notes |
|---|---|---|
| M1 | `MENU_MODULE_INVENTORY` | Inventory inventory panel/module |
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

### Required request envelope

`ModeSwitchRequestV1`

- `requestId` opaque token
- `actorScopeId` opaque account/session scope
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

## Shared menu and mode restoration semantics

1. On successful `SUCCEEDED`, game must restore the exact validated camera/input state for destination mode.
2. On `REJECTED`, active mode, input, camera, and context remain unchanged.
3. On `CANCELLED`, request context is invalidated with no mode drift.
4. Return to 3D must produce a non-lossy reversible save-point anchor.
5. No action is idempotent for already-in-target mode requests.

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

## Cross-mode journey and retry behavior

### User flow (planned)

`Boot` → `Realm` → `Race/Class` → `Customization` → `Username` → `Tutorial` → `OMEN_1` → `MQ_C1_PROOF_OF_WORTH` → `LordshipUnlocked` → shared menu shows `MENU_MODULE_KINGDOM_MANAGEMENT` as Available → user invokes switch.

### Retry behavior

- One request in-flight at a time.
- Repeating the same valid request after success is idempotent and returns `ALREADY_IN_MODE`.
- Repeating after failure must re-run all guards.

## Compatibility and conflict checks

- `KingdomSceneController` must not be treated as the global gate for top-level transitions.
- Menu and mode switch behavior must be separate from cinematic, realm selection, and legacy UI surfaces.
- Existing `HasCommittedRealm` is a profile state hint only and is not authoritative for menu unlock.
- `CH01` prerequisite remains as explicit progression input until a dedicated, accepted lordship gate supersedes it.

## Required acceptance for implementation handoff

A future implementation can begin only after:

1. This contract is approved and versioned under #461.
2. #467 onboarding and C1 unlock chain accepted.
3. #450 shared lock protocol authorizes persistence/state projection.
4. #181 world-safe-zone IDs are versioned and queryable.
5. #477 economy naming and non-production currency source contract accepted.

## Unperformed checks to record

- Runtime mode-switch unit coverage
- UI/controller/accessibility behavior
- Device parity on PC/Android
- Save projection persistence tests
- Build/distribution evidence

## Appendices

- A1: `ModeSwitchRequestV1` and `ModeSwitchResultV1` field list above
- A2: `KingdomSubviewRequestV1` and `KingdomSubviewResultV1` field list
- A3: `WorldMapQueryV1` and `WorldMapDestinationPreviewV1` field list