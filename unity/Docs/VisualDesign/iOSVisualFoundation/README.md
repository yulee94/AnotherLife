# Another Life — iOS visual foundation 04

This review prototype covers the opening iOS experience and the first realm-management loop: launch identity, realm selection, Champion creation, inner-realm arrival, all four domains, and the outer-realm world map.

> **Repository status:** Codex coordination/review design evidence. This package records a user-directed interaction and visual target; it is not a Unity scene, gameplay implementation, economy authority, multiplayer contract, or production iOS build.

See [`AUDIT.md`](AUDIT.md) for the reviewed scope, acceptance evidence, production blockers, and repository impact. See [`previews/README.md`](previews/README.md) for the curated visual proof set.

## Direction

- Preserve the approved AL app icon and four Arcane Axis realm marks exactly.
- Use a threshold/arch motif as the signature element for opening screens.
- Keep the visual language adult, tactile, ceremonial, and readable.
- Express realm identity through emblem shape, title, descriptor, selection state, and color.
- Carry one oath-thread motif from realm selection through the Champion seal and first quest marker.
- Treat the world as a gigantic persistent multiplayer space: every castle is one user, the map is designed for 300+ player castles, and the phone shows a bounded sector rather than pretending the whole population fits in one viewport.
- Give each kingdom a different spatial grammar: Stonehold's braced grid, Eldergrove's cultivated root network, Crownlands' civic axis, and Umbral's offset eclipse court.
- Use a compact city-builder resource scan pattern—material icon plus amount only in the top ledger—without importing competitor artwork, store buttons, or decorative chrome.
- Treat the onboarding realm choice as locked during normal play: there is no realm switcher after selection.
- Preserve the approved construction trust model: selecting a building never spends resources; the quote and authorization action remain separate.
- Keep the Champion and arrival landmark deliberately structural until final character and environment art is approved.
- Respect iOS safe areas, 44-point minimum controls, larger text previews, and reduced-motion preferences.

## Scope boundary

This is a review artifact, not a production Unity asset. Its repository presence deliberately changes no gameplay, save behavior, authentication, economy authority, scene, package, or build setting. The production translation should use Unity UI and the existing iOS export path after owner visual approval and the relevant domain contracts are ready.

### Realm-change commerce rule

- Players cannot change realms directly from the kingdom, profile, settings, or another self-service control.
- A separate paid realm-change service may be sold through the shop.
- Buying that service must not immediately expose a realm picker or directly mutate the player's realm; it begins a controlled realm-change process handled outside normal player controls.
- The service name, price, eligibility, processing time, confirmation language, and gameplay consequences remain intentionally undefined until the shop flow is designed.
- Design-review URL parameters remain preview tooling only and are not part of the player-facing realm-change process.

This is a future commerce concept only. The current milestone does not authorize in-place realm transfer, and this artifact does not approve a price, entitlement, migration path, reset policy, or runtime transaction.

## Preview

Open `index.html` in a browser. Use **Enter the veil** to choose one realm, continue into Champion creation, mark the first path, then enter that realm's domain. The chosen realm persists through every following screen. The desktop controls can jump between screens or preview text scaling. For design review only, the kingdom view supports direct URLs such as `?screen=kingdom&realm=eldergrove`; this is not an in-game realm switch.

Within the chosen domain, selecting Town Hall, Farm, Lumber Mill, Quarry, Gold Mine, or Barracks requests its current quote. Accepting the upgrade deducts that exact quote from the realm treasury, and the remaining construction time ticks down once per second. The global **Build queue** shows the soonest order and active count, expands to every building currently under construction, and lets each row navigate back to its building. Completion raises that building to the target level, removes its queue entry, then immediately refreshes the following level, duration, resource quote, affordability state, and upgrade action. Each countdown, level, queue, and treasury belongs to its selected realm.

Each building also previews its current-to-next gameplay benefits before the cost: kingdom power and structure cap for Town Hall; hourly production, storage, and power for resource buildings; troop capacity, training speed, and power for Barracks. These numbers are illustrative review balance until the production economy is approved.

Selecting Food, Wood, Stone, or Gold opens an inventory sheet with the full resource name, current balance, gameplay purpose, and five pack tiers: 100, 1,000, 10K, 100K, and 1M. Selecting a held tier enables an explicit **Use** action that opens a second confirmation popup. Its left-to-right slider pin and right-side numeric field stay synchronized and cannot exceed the selected tier's held count. The final button names both the number of packs consumed and the exact resources added to the locked realm treasury. The per-tier counts are illustrative review data. Pack purchase remains outside this surface and is not represented.

The city-board **World** control opens the outer-realm map without changing the player's locked realm. Its current prototype terrain is an oversized 1,320 × 1,200 local sector inside the phone viewport: drag across blank terrain, use a mouse wheel, or focus the map and use the Arrow keys to explore; Home recenters it. The sector HUD shows ten nearby representative player castles against the 300+ world-scale target. Every castle is a selectable user entity with a castle name, owner, alliance tag, distance, and power; opening its player preview does not consume the march slot. Production must populate these records from authoritative spatial/player data and recycle or cluster off-screen castles rather than render the entire world at once. The **Camera** control cycles Bird's-eye, Quarter, and 3D views while preserving pan position, selected player/target, bookmark, and active march. Food, stone, hostile, ruin, and home-city nodes remain independently selectable; their target sheet still shows type, level, distance, purpose, expected return, bookmark state, and a separate dispatch action. One march slot is available in this slice. An active march exposes its destination and second-by-second timer, blocks conflicting dispatches, animates its route, and applies the stated reward to the same realm treasury only when complete. **Domain** returns to the city with construction and treasury state intact.

Player castles, Bandit camps, fortified Gates, and rival Realm targets now expose a separate close-battle action. That action first opens an explicit **Enter battlefield** confirmation with the encounter type, target, Champion, and recommended power; canceling keeps the user on the same selected map target. Entering changes scale from the large strategic world to a local 2.5D battlefield while preserving realm, world position, camera, selection, bookmarks, and march state. The player directly controls the Champion with the touch movement stick, WASD, or Arrow keys; **Dash** closes distance without overshooting the selected enemy, **Ward** restores protection, and **Attack** enforces range before applying damage. Every visible hostile is a real selectable enemy with an attached health bar and its own health state—there are no decorative enemy silhouettes. Current encounter composition is two castle defenders for Player siege and three enemies for Bandit, Gate, and Realm battles. Defeating one enemy keeps the battle active, removes only that enemy from targeting, updates the enemies-left count, and selects the closest living target. Only defeating every enemy updates the objective to **Battle won**, locks movement and every battle/exit control, and opens the required victory reward. Escape cannot bypass it. **Collect & Leave** applies the reward once to the locked realm treasury and returns to the exact World selection. Encounter counts and reward values are illustrative; authoritative PvP synchronization, AI behavior, encounter composition, damage formulas, production rewards, respawn, and server reconciliation are not yet defined.

For direct design review, add `camera=bird`, `camera=quarter`, or `camera=three` to a World preview URL. These parameters are prototype tooling only, like the existing screen and realm preview parameters.

## Validation completed

- Launch, all four realm states, three catalog-backed Champion presets, name validation, arrival, path-marking, and the domain handoff render without browser errors.
- All four kingdom plans render as structurally distinct layouts rather than palette-only variants.
- The onboarding realm choice persists through Champion creation, arrival, and kingdom management, with no player-operated realm-switch control after selection.
- Six supported construction targets return catalog-derived levels, duration, and resource quotes.
- The unified oath ledger keeps Food, Wood, Stone, and Gold in a single icon-and-number row with original SVG symbols and no visible text labels or unsupported purchase controls.
- Each resource button exposes an accessible name and opens a keyboard-dismissible inventory sheet showing its purpose, full balance, and illustrative counts across the fixed 100 / 1,000 / 10K / 100K / 1M pack tiers.
- The quantity popup was exercised in both directions: typing `7` moved the slider and recalculated `+700 Food`; moving the slider to `5` updated the numeric field and final button; typing `99` clamped to the 12-pack inventory maximum.
- A five-pack confirmation was exercised: the selected 100-Food tier decreased from 12 → 7, Stonehold Food increased from 1,000 → 1,500, and the top rail pack total refreshed from 24 → 19.
- Two concurrent orders were exercised in the global queue. The trigger reported the active count and soonest completion, the expanded queue exposed both building destinations, the Farm entry disappeared at zero, and its Level 1 data refreshed while the Town Hall order remained active.
- Queue navigation was exercised from the active Town Hall row into its inspector; final completion advanced Town Hall to Level 2, refreshed its Level 2 → Level 3 data, and returned the queue to `Queue empty`.
- The two-step construction interaction was exercised from building selection through `ORDER ACCEPTED`, a visible 30 → 29 → 28 second countdown, and `CONSTRUCTION ACTIVE`.
- A complete 10-second Farm order was verified through treasury deduction, Level 0 → Level 1 advancement, and the refreshed Level 1 → Level 2 quote: 20 seconds, 100 Wood, 40 Stone, and an enabled `Upgrade to level 2` action.
- The refreshed completion state fits 390 × 844 and 375 × 667 without horizontal overflow or inspector clipping.
- The expanded global queue, pack-use sheet, and quantity popup fit 390 × 844; both the live queue and the full slider/number/confirmation card fit 375 × 667 without overflow.
- Domain → World → Domain navigation was exercised with the Stonehold realm identity preserved and no realm-selection control exposed.
- Food, stone, hostile camp, ruins, and home-city targets update the world inspector independently; bookmark state is retained for the selected realm and node.
- A complete 15-second Veiled Bandit Camp scout march was exercised from dispatch through capacity occupation, live timer, animated route, completion objective, slot release, and `+30 Gold` treasury reward (`500 → 530`).
- The ready and active-march World states fit 390 × 844; the active World map, objective/capacity HUD, nodes, target inspector, and action fit 375 × 667 without scrolling or clipping.
- The World terrain now exceeds the viewport in both axes and was exercised through direct drag, Arrow-key movement, and Home recentering; coordinates changed with movement while the selected Sunfield Grove target remained selected.
- Bird's-eye, Quarter, and 3D camera states render as distinct tactical, balanced 2.5D, and deeper perspective views. Camera cycling preserved the selected target and did not mutate realm, treasury, bookmark, or march data.
- All three camera states fit 390 × 844, and the Quarter view—including camera control, panning cue, compass, complete target sheet, and dispatch action—fits 375 × 667 without document-level overflow.
- The local World canvas was expanded to 1,320 × 1,200 and now exposes ten representative player castles within Sector 04 while explicitly carrying the 300+ castle world-scale target.
- Every representative castle exposes a unique castle name, owner, alliance tag, distance, and power. Thornspire selection switched the inspector to GreenWarden's player data and the `ONE CASTLE • ONE PLAYER` state.
- Opening the player-profile preview left the march capacity at `0 / 1`; camera cycling and direct drag preserved the selected player while coordinates changed from `X: 418 • Y: 726` to `X: 388 • Y: 740`.
- All four locked realm previews retained ten selectable player castles, the 300+ scale summary, and the three camera modes; Bird's-eye reduces unselected castle names to alliance tags to control density.
- The populated Quarter sector and full player-castle inspector fit both 390 × 844 and 375 × 667 without document-level overflow.
- Player castle, Bandit camp, fortified Gate, and rival Realm targets each expose their own attack language and open a target-specific confirmation: Player siege, Bandit skirmish, Gate assault, and Realm battle.
- The player-siege confirmation fits 390 × 844 with target, Champion power, recommended power, state-preservation note, Cancel, and Enter battlefield fully visible.
- Encounter size is now variable: Player siege renders two real defenders and hides the unused third actor, while Bandit skirmish renders three selectable enemies with no decorative hostile silhouettes.
- The three-Bandit state exposes Bandit Leader, Bandit Raider, and Bandit Archer as independent buttons; each announces its name, health percentage, and selection state and carries a visible attached health bar.
- Bandit Raider was selected independently and damaged `100% → 75%`; its actor bar and selected-enemy HUD changed while Bandit Leader and Bandit Archer remained at `100%`.
- Defeating Bandit Raider did not end the battle or show a reward: the objective changed to `2 enemies remain`, Raider became disabled, and Bandit Archer was selected as the closest living target. Defeating Archer then produced `1 enemy remains` and selected Bandit Leader.
- Twelve total close-range strikes were exercised across all three Bandits. Only the final Leader strike produced `BATTLE WON`, `0 left`, disabled movement/joystick/Dash/Ward/Attack/back, and opened the reward card.
- The Bandit victory card names Veiled Bandit Camp, Bandit skirmish, Recovered cache, and `+750 Gold`; **Collect & Leave** increased Stonehold Gold from `500 → 1,250`, returned to the Quarter World view, preserved Veiled Bandit Camp as selected, and reported `BATTLE REWARD CLAIMED`.
- The three-enemy battlefield and every attached health bar fit 390 × 844 and 375 × 667 with objective, selected-enemy HUD, terrain, Champion, touch stick, abilities, and Attack visible without scrolling or horizontal overflow.
- The completed multi-enemy and reward-return flow produced no browser-console errors.
- All six building profiles were exercised: Town Hall shows power and structure cap; Farm, Lumber Mill, Quarry, and Gold Mine show resource production, storage, and power; Barracks shows troop capacity, training speed, and power.
- Farm completion refreshes its benefit comparison from 0 → 120 Food/hour into 120 → 200 Food/hour, alongside 2.5K → 3.8K storage and 15 → 25 power for the following level.
- Primary controls are at least 44 points high.
- Standard layout fits 375 × 667 and 390 × 844 viewports without clipping.
- Type scaling was exercised at 124%; Champion creation uses bounded vertical scrolling at compact height, while the gameplay HUD remains fully visible.
- Reduced-motion mode shortens transitions to an effectively instant state change.
- Each realm activates a distinct structural arrival landmark rather than a palette-only variant.
- Visual verdict passed at 98/100 against the approved Another Life direction; remaining distance is intentional graybox character, enemy, and environment detail awaiting production asset approval.
