# PvP Hostility, Targeting, and Effect Eligibility Contract (Coordination)

Status: draft coordination policy

## 1) Scope and source dependencies

This document is **coordination/review-only**. It defines hostilities and combat-effect eligibility used by runtime systems, but does not implement gameplay.

Primary ownership references:

- Shared menu/cross-mode context: #461
- First-user spine and lordship flow: #479
- Save/profile authority: #450, #137
- Safe-zone policy source of truth: #181
- Guild membership snapshot and role contract: #481
- Alliance + war snapshot and revision contract: #484
- PvP/effect eligibility integration point: #480

## 2) Problem constraints from latest user direction

- PvP is optional by player toggle inside the same realm.
- PvP does not apply in designated safe zones (at least: city and beginner-safe areas).
- Same guild/clan and same alliance members cannot target one another, including AoE/projectile/DoT/chained effects.
- Opposing active war can create PvP regardless of personal toggle.
- Same-realm PvP should never become cross-realm combat.
- Runtime must support separate concepts:
  - `Preview/Read-only`: world-map or dungeon previews.
  - `Hostility`: eligibility decision for targeting and effect application.
  - `Travel` / `Teleport`: movement mode transitions.

## 3) Core enums and IDs

### 3.1 PvP toggle and modes

```
enum PvPToggleState {
  OFF,
  ON,
  LOCKED_OFF,
  UNKNOWN
}

enum PvPModeScope {
  REALM_BOUND,
  WORLD_BOUND,
  UNKNOWN
}
```

### 3.2 Social grouping identity

```
type GuildClanId = sha256_opaque(32)
type AllianceId = sha256_opaque(32)
type CharacterId = opaque_session_identity(32)
```

### 3.3 Snapshot versions

```
type GuildMembershipSnapshotId
type AllianceSnapshotId
type WarSnapshotId
type ZoneSnapshotId
type Revision = u64
```

## 4) Inputs

All runtime effect-hostility checks must use committed snapshots plus a zone snapshot:

- `attacker_char_id`, `target_char_id`
- `realm_id` (authoritative realm of both actors)
- `guild_membership_effective_snapshot_id`
- `guild_membership_rev`
- `guild_id(attacker)`, `guild_id(target)` (or `NULL` if no guild)
- `alliance_effective_snapshot_id`
- `alliance_rev`
- `war_effective_snapshot_id` (for effective opposing war relation)
- `zone_snapshot_id` and `zone_rev`
- `attacker_pvp_toggle_state`, `target_pvp_toggle_state`, and each toggle generation/revision
- `attacker_context` / `target_context` (combat, quest, transition, etc.) if available from downstream engine
- `session/effect id` + `action type` + `targeting pipeline stage` (single target, radial, projectile tick, DoT tick, chain tick, reflect source tick)

## 5) Hostility decision (authoritative predicate)

`CanHostilelyAffect(attacker, target, action)` is computed at:

1) hostile-acquisition time
2) every action/effect tick

It returns one of:

- `HOSTILE_AUTHORIZED`
- `HOSTILE_BLOCKED`
- `HOSTILE_INDETERMINATE`

### 5.1 Hard gate order (must be evaluated in this order)

1. **Missing/invalid snapshot/identity/input data** ⇒ `HOSTILE_INDETERMINATE` (no effects).
2. **same character / self** ⇒ `HOSTILE_BLOCKED`.
3. **realm mismatch / realm unknown** ⇒ `HOSTILE_BLOCKED`.
4. **safe-zone check** (city, beginner-safe, or other `FORCED_NON_PVP` zone IDs from #181) ⇒ `HOSTILE_BLOCKED`.
5. **social protection checks**:
   - same guild/clan ⇒ `HOSTILE_BLOCKED`;
   - same alliance effective membership ⇒ `HOSTILE_BLOCKED`.
6. **war override check**:
   - if opposing relationship is committed + `ACTIVE` ⇒ evaluate next;
   - else require personal PvP toggles active for both actors (or effective sidecar policy) to progress.
7. **combat context check** (not in noncombat/trade/tutorial exception, if such exceptions exist as locked policy) ⇒ if invalid, `HOSTILE_BLOCKED`.
8. If all above pass ⇒ `HOSTILE_AUTHORIZED`.

### 5.2 War precedence

- Opposing committed `ACTIVE` war must bypass personal toggle requirement.
- War states outside committed `ACTIVE` (`DECLARED`, `PENDING`, `ENDING`, `COOLED`, etc.) do not bypass personal toggle requirements.
- War snapshot revision must be tied to explicit realm/policy versions.

## 6) Targeting and effect classes

Given each `ActionStage`:

- `DirectSingleHit`
- `DirectPersistent` (DOT/Burn/Poison with periodic ticks)
- `ProjectileImpact`
- `AoE_Radial`
- `AoE_Cone`
- `AoE_Line`
- `ChainSpread`
- `ReflectBounce`
- `TrapZone`
- `Summon/Companion Pulse`
- `Pet command / owned object impact`

The runtime must call the same `CanHostilelyAffect` check with **owner and source provenance** at each stage where damage/status is applied.

If any stage is blocked, application on that target is canceled for that stage only (or all remaining stages for that target if effect design states such coupling), and hostile ledger entries for that target are never written.

## 7) Safe-zones

### 7.1 Deterministic safe-zone IDs (canonical source: #181)

- `CITY_SAFE_ZONE`
- `BEGINNER_SAFE_ZONE`
- future identifiers may be added only by explicit source update of #181 and a contract amendment.

### 7.2 Safe-zone semantics

- All zone protections are evaluated from the target snapshot.
- If no zone snapshot is available, hostilities are blocked (`INDETERMINATE`).
- Safe-zone policy must be replayed at each stage for AoE/chained effects.

## 8) Player readout and colors

- This contract does **not** own copy/visual design for red/bright-red status.
- Red/bright status is a **presentation layer signal only** and must be fed from a resolved hostility projection.
- Presentation cannot be used as authoritative combat predicate.
- Any contradictory wording (e.g., “red means non-hostile”) is pending copy arbitration and must be resolved by narrative/UX before production.

## 9) Non-goals / explicitly forbidden in this phase

- No runtime scene/controller/attack code changes.
- No server auth/account binding in this contract.
- No guild rank/permissions, tax, costs, cooldown, sanctions, anti-tamper retention, or combat balancing.
- No world-map travel, teleport, relocation, or PvP reward logic.
- No direct implementation in `ChampionArena`, `CombatActionPlanner`, `CombatantResourcePlanner`, or shared-lock runtime files.

## 10) Coordination split and sequencing

- #481 handles membership snapshots + role semantics (guild/clan membership IDs, leadership flags, membership states).
- #484 handles alliance relation snapshots + revisioned `ACTIVE` war relation.
- #181 remains source for safe-zone IDs and zone revisioning.
- #480 consumes #481/#484/#181 and defines the admissibility matrix for target/effect and hostile targeting.
- #461 and #479 provide menu-mode and progression coupling constraints but do not define combat-hostility semantics.

## 11) Data and error handling

On missing/wrong-zone/revision mismatch/unknown IDs/replayed effect mismatch:
- result: `HOSTILE_INDETERMINATE` or `HOSTILE_BLOCKED` per call stage rules
- no destructive mutation
- no host-target ledger entry
- no damage/status application

All ambiguous/indeterminate states are fail-closed (zero effect).

## 12) Test obligations

- direct same-guild targeting block
- direct same-alliance targeting block
- enemy guild/alliance hostile with both toggles enabled
- active opposing war hostile with either toggle state
- safe-zone block for city/beginner
- same-zone/realm/projection mismatch
- target snapshot stale/missing/replaced
- AoE and DoT recheck by stage
- chain/reflect/pet/companion/summon ownership path revalidation
- stale relation revision and replay mismatch
- non-hostile user copy states remain non-authoritative
- duplicate effect reprocessing must remain idempotent under same revision pair
- unknown action stage defaults to blocked/indeterminate

## 13) Compatibility / unimplemented transitions

- PvP toggle is a toggle state only; no claim here about where/when the UI appears.
- Closed dungeons, alliances, castles, and guild rally content are governed by their own contracts and are not affected by this spec.
