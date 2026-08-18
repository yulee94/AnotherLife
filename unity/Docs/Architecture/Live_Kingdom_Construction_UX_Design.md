# Live Kingdom Construction UX Design

**Status:** Active design draft; implementation held for owner review

**Date:** 2026-07-28

**Primary owner:** Project owner / creative director

**Design authority:** Root `DESIGN.md`

**Gameplay authority:** `IBuildingService` construction quote and result
contracts, confirmed `BuildingState`, and exact building definitions

This packet designs the player-facing construction experience around the
implemented gameplay-authoritative single-order system. It does not add or
approve a mechanic. The existing Level `0`–`10` definitions, resource costs,
durations, stable slots, save behavior, and confirmed-level presentation
remain the only gameplay facts.

> **2026-08-14 private-kingdom supersession:** The hold list and stable-slot UX
> below describe the legacy local single-order implementation. The owner has
> now approved, specifically for the isolated account-owned
> `PRIVATE_KINGDOM`, a bounded unlocked-cell grid, pre-placement rotation,
> active-construction cancellation, queue capacity `1` growing to at most `3`,
> and authoritative Oathmark-only Rush. The canonical layout is a bottom-center
> construction dock plus right-side selected-building inspector with at least
> 60% clear viewport. Cosmetic workers never change duration. The complete
> authority and migration boundary is
> `Private_Kingdom_Save_And_State_Synchronization_Architecture.md`.

## Evidence reviewed

Observed implementation and approved contracts:

- root `DESIGN.md` for kingdom interaction, accessibility, motion, and mobile
  safety direction;
- `Kingdom_Building_Level_And_Placement_Design.md` for stable identity, Level
  `0`–`10`, economy, save, and confirmed-level presentation;
- `Reusable_Architecture_Construction_State_System.md` for the rule that
  gameplay state resolves presentation without a separately persisted visual
  stage;
- `IBuildingService` quote and result contracts for authoritative costs,
  eligibility, duration, acceptance, rollback, and uncertainty;
- the current `KingdomSceneController` BUILD deck, where a supported entry
  submits construction directly.

The direct-spend BUILD interaction is an observed current implementation, not
an approved final UX. The select → quote → explicit action flow below is a
design recommendation and remains unimplemented.

## Historical legacy-implementation hold boundary

The following elements remain out of the legacy fixed-slot implementation.
The private-kingdom supersession above governs its separately approved future
product surface:

- cancellation and refunds;
- builder rosters/capacity and local or global build queues;
- prerequisite graphs, district locks, and Town Hall gating;
- premium or earned speedups;
- server-issued construction order identity and cross-device resolution;
- demolition;
- relocation, rotation, and slot swapping.

Held elements are absent, not teased. The live interface must not show them as
locked buttons, disabled tabs, empty queue slots, premium badges, countdown
shortcuts, or future-feature copy. This keeps the current experience honest
and prevents placeholder UI from silently establishing product direction.

## Product goal

Let a player understand and deliberately authorize one building construction
order without guessing:

- which stable building is affected;
- whether it is unbuilt or already built;
- the confirmed current and exact next level;
- the resource cost and current treasury sufficiency;
- the authoritative duration;
- whether an order is already active;
- whether persistence accepted, rejected, rolled back, or cannot yet resolve
  the request.

The experience should feel like issuing a grounded kingdom order, not buying a
generic mobile upgrade.

## Non-goals

- No construction center, queue screen, builder roster, or global capacity
  system.
- No future-level carousel or multi-level purchase.
- No yield, unlock, power, or production preview until those values have an
  approved definition contract.
- No target-level model preview during an active order.
- No visual progress stage persisted separately from gameplay.
- No monetization, acceleration, social, or network language.
- No destructive or positional building controls.

## Recommended interaction decision

Construction discovery and construction commitment are separate actions.

1. The player taps a stable world building/plot or a BUILD deck entry.
2. Both entry points select the same stable `BuildingId` and open the same
   local construction inspector.
3. The inspector requests an authoritative quote.
4. The player reviews current level, next level, cost, treasury sufficiency,
   and duration.
5. One explicit primary action—`Construct Level 1` or
   `Upgrade to Level N`—submits the order.
6. The returned gameplay result, not optimistic UI, determines the visible
   success, rejection, rollback, or unresolved state.

The first tap never spends. A second confirmation modal is unnecessary because
selection plus the explicit labeled action already supplies two deliberate
steps. This is the recommended replacement for the current direct-spend BUILD
buttons and is a major interaction-direction choice requiring owner review
before implementation.

## Entry points and information architecture

### World selection

- Tapping a stable built building or Level `0` reserved plot selects it and
  opens the inspector.
- Selection remains visible through a ground boundary, outline, or equivalent
  non-color cue.
- The inspector does not cover the selected footprint or neighboring targets.
- Camera pan and zoom remain navigation only.

### BUILD deck

- Each supported BUILD entry acts as a selection shortcut, not a purchase
  button.
- The deck and world never maintain separate selected-building state.
- Town Hall, Farm, Lumber Mill, Quarry, Gold Mine, and Barracks may open a live
  inspector.
- Mana Shrine and Mine remain visible in their stable slot/deck positions but
  use an honest `UNAVAILABLE` state and open no false quote.

### Local construction inspector

Information order:

1. Building name and realm affiliation.
2. State label: `UNBUILT`, `LEVEL N`, `CONSTRUCTION ACTIVE`, `CAPSTONE`, or
   `UNAVAILABLE`.
3. Current-to-next level statement.
4. Exact duration or active remaining time.
5. Resource rows with `have / required`.
6. One primary construction action or one non-action status.
7. Short persistence/result feedback.

The realm emblem is secondary affiliation. It does not replace the building
name or building-function icon.

## Component contract

### Inspector header

- Building name is the primary label.
- Realm name or approved micro emblem is secondary.
- Level is text plus number; never color alone.
- Level `10` uses `CAPSTONE · LEVEL 10`, not a purchasable-looking action.

### Level transition row

- Level `0`: `Unbuilt → Level 1`.
- Level `1`–`9`: `Level N → Level N+1`.
- Level `10`: `Level 10 · Capstone`.
- Do not show unsupported future level names, powers, or milestone rewards.

### Resource cost rows

Each row contains:

- resource icon and text name;
- current treasury amount;
- exact required amount;
- `READY` or `SHORT` text;
- a shape/value treatment in addition to color.

The UI consumes the quote's exact resource list and does not reproduce cost
formulas. If any required resource is short, the primary action is disabled
and the first shortfall receives focus/read priority.

### Duration and active progress

- Available order: show the authoritative duration in the largest meaningful
  unit while retaining exact seconds below one minute.
- Active order: show the absolute completion time when space allows and a
  locally refreshed remaining-time label.
- A simple time-progress bar may be derived from the accepted deadline and
  authored duration. It is a read-only time indicator, not a construction
  stage and never a save field.
- Do not announce or animate every elapsed second.

### Primary action

- Level `0`: `CONSTRUCT LEVEL 1`.
- Level `1`–`9`: `UPGRADE TO LEVEL N`.
- Insufficient resources: disabled `RESOURCES REQUIRED`.
- Active order: non-action `CONSTRUCTION ACTIVE`.
- Level `10`: non-action `CAPSTONE COMPLETE`.
- Unsupported or invalid: non-action `UNAVAILABLE`.
- Commit uncertain: non-action `ORDER STATUS UNRESOLVED`.

The action requires one release inside the button bounds. Press-down alone
does not spend. Repeated activation while awaiting a result is suppressed.

### Result feedback

- Accepted: `ORDER ACCEPTED` plus target level and completion time.
- Insufficient: `ORDER NOT STARTED` plus exact short resources.
- Known save failure: `ORDER NOT COMMITTED · TREASURY RESTORED`.
- Commit uncertain: `ORDER STATUS UNRESOLVED · CONSTRUCTION PAUSED FOR
  RECOVERY`.
- Completed in the current session: `LEVEL N CONFIRMED`.
- Completed while away: `LEVEL N COMPLETED WHILE AWAY`.

Result feedback is concise and paired with the persistent inspector state. A
transient banner alone is not sufficient.

## State matrix

| Gameplay state | Inspector | Primary action | World presentation |
| --- | --- | --- | --- |
| Missing row, valid definition | `UNBUILT`; Level `0 → 1`; quote | `CONSTRUCT LEVEL 1` when affordable | Reserved plot and stable slot |
| Built Level `1`–`9`, no order | Confirmed level and exact next-level quote | `UPGRADE TO LEVEL N` when affordable | Confirmed settled model |
| Active order | Target level, remaining time, paid resources summarized | `CONSTRUCTION ACTIVE` | Confirmed level plus localized worksite feedback |
| Confirmed Level `10` | `CAPSTONE · LEVEL 10` | `CAPSTONE COMPLETE` | Settled Level `10` model |
| Insufficient resources | Exact short rows emphasized | `RESOURCES REQUIRED` | Confirmed model/plot unchanged |
| Known save failure | Restored state and recovery message | Quote may be requested again | Confirmed model/plot unchanged |
| Commit uncertain | Recovery message; no guessed outcome | `ORDER STATUS UNRESOLVED` | Last in-memory candidate; no completion claim |
| Unsupported definition | Honest unavailable reason | `UNAVAILABLE` | Stable reserved placeholder |
| Malformed save state | Recovery/unavailable reason | `UNAVAILABLE` | Explicit invalid-state presentation |
| Profile/runtime unavailable | Profile unavailable message | No action | Read-only unavailable board |

## Construction motion and feedback

- An accepted order does not reveal the target model delta.
- Level `0 → 1` remains a prepared site with localized realm-authored work
  feedback until confirmation.
- Level `N → N+1` keeps the confirmed Level `N` building settled and
  load-bearing.
- Current-session confirmation may play one short `0.35–1.25` second settle on
  only the new level delta.
- Offline completion, reload, reconnect, same-level refresh, and multi-level
  reconciliation appear settled and never replay construction.
- Reduced motion replaces the settle with an immediate state change and a
  static confirmation treatment.
- Sound or haptics, if later approved, confirm accepted and completed states
  only. They must not imply a result before gameplay returns it.

## Responsive behavior

### Compact landscape phone

- Use a bottom inspector occupying no more than roughly the lower third of the
  safe-area height in its collapsed state.
- Keep the building name, state, level transition, duration, first short
  resource, and primary action visible without scrolling.
- Expand resource detail within the same inspector.
- Primary touch targets are at least `48 × 48` logical pixels.
- Never place the primary action behind a safe-area inset.

### Tablet and desktop

- Use a persistent right-side inspector aligned with the command surface.
- Preserve the same information order and action labels as compact mobile.
- Hover may expose explanations but cannot carry required state.

### Camera and occlusion

- Opening the inspector must not force a camera orbit.
- If the selected target is behind the inspector, shift framing gently within
  existing camera bounds; do not move the building.
- Closing the inspector returns focus to the selected world target or BUILD
  entry.

## Accessibility

- Cost sufficiency uses text, value, icon, and shape; red/green alone is not
  sufficient.
- Timers remain readable without animation and do not flash at completion.
- Screen-reader/focus order follows the visual information order.
- Countdown labels are not announced every second. Announce selection,
  accepted/rejected result, meaningful minute/hour boundary on focused
  inspection, and completion.
- Reduced motion removes camera impulse, repeated impact, traveling energy,
  and delta settle while preserving worksite/confirmed state.
- The selected target remains recognizable in grayscale and at mobile-low
  quality.

## Content voice

Use short, specific command language:

- `CONSTRUCT LEVEL 1`
- `UPGRADE TO LEVEL 4`
- `WOOD 420 / 560 · SHORT 140`
- `COMPLETES IN 30 MIN`
- `ORDER ACCEPTED`
- `TREASURY RESTORED`
- `LEVEL 10 · CAPSTONE`

Avoid:

- `Buy now`, `rush`, `instant`, `slot`, `queue`, `builder busy`, or premium
  language;
- vague `Something went wrong`;
- lore, yields, bonuses, or unlock promises not present in approved data;
- `LOCKED` when the accurate state is unsupported or unavailable.

## Visual language

- Treat the inspector as an engraved command instrument: dark grounded plate,
  restrained realm accent, clear metal/stone edge hierarchy, and one dominant
  action.
- Use building-function iconography for the primary read and the approved realm
  emblem only as a secondary affiliation mark.
- Preserve broad negative space around numeric cost and time information.
- Avoid generic shop cards, gem-store styling, pulsing purchase buttons,
  excessive gold framing, and glow on every cost row.
- Active construction uses a quiet progress treatment. Completion earns one
  brief emphasis and then returns to the stable command-board rhythm.

## Authority and implementation boundary

- The inspector requests `BuildingConstructionQuote`; it never recomputes
  level, cost, duration, or eligibility.
- The primary action submits stable `BuildingId` once and renders the returned
  `BuildingConstructionResult`.
- Treasury display reads authoritative wallet state.
- Presentation never mutates saves, resources, timers, quests, or building
  rows.
- No UI-local optimistic level or resource deduction survives the result.
- No separate visual-stage field, queue model, or future mechanic placeholder
  is added.

## Design acceptance criteria

- World selection and BUILD selection converge on one inspector state.
- The first discovery tap cannot spend resources.
- Every live order shows the exact next level, costs, sufficiency, and duration
  before commitment.
- Level `0` says `Construct`; Level `1`–`9` says `Upgrade`; Level `10` is a
  non-action capstone.
- Unsupported, insufficient, malformed, offline, save-failed, and
  commit-uncertain states are distinct and honest.
- Active construction exposes time, not a persisted visual stage.
- No held mechanic appears in controls or copy.
- Touch, safe-area, reduced-motion, grayscale, and compact-screen checks pass.
- Mobile architecture safety remains above `90/100`.

## Implementation handoff sequence

No implementation begins until the recommended interaction decision is
approved. After approval, use this order:

1. Read-only inspector and shared world/deck selection.
2. Quote, affordability, and unavailable-state binding.
3. Explicit primary action and returned-result feedback.
4. Active-time presentation and completion feedback.
5. Compact phone, tablet, reduced-motion, and mobile-low validation.

Each stage must preserve the gameplay-authoritative transaction already in
production and must not reopen held elements.
