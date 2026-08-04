# Design

## Source of truth

- Status: Active review artifact
- Last refreshed: 2026-08-04
- Primary product surfaces: iOS launch, realm selection, Champion creation, arrival, kingdom management, outer-realm world map, attack confirmation, close-up Champion battlefield
- Evidence reviewed: `index.html`, `README.md`, approved AL and Arcane Axis marks in `assets/`, prior review captures, project architecture and kingdom-construction UX notes, official App Store listings for Rise of Kingdoms, Lords Mobile, and Infinity Kingdom; user-confirmed world-scale rule from 2026-08-04: the world is gigantic, every castle represents one user, and the map must be designed for more than 300 concurrent player castles

## Brand

- Personality: Adult, mystical, ceremonial, deliberate, quietly powerful
- Trust signals: Exact approved marks, explicit state labels, authoritative construction quotes, visible realm continuity, deliberate world dispatch, restrained motion
- Avoid: Cartoon gloss, generic fantasy chrome, palette-only realm identity, noisy event badges, copied competitor assets, hidden spending

## Product goals

- Goals: Make realm identity unmistakable; keep kingdom resources scannable; make construction spending trustworthy; keep all active construction visible from the city; let held packs refill the locked realm treasury deliberately; extend the locked realm into a gigantic persistent 2.5D world designed for 300+ player castles; make every castle a selectable representation of one real user; keep local exploration readable through sector loading and camera-aware density; let players choose a bird's-eye, quarter, or 3D camera without losing map state; let attacks against players, bandits, gates, and realm targets transition into a close-up battlefield where the Champion moves and fights directly; make every upgrade's gameplay value visible; preserve iPhone readability
- Non-goals: Reproduce another game's interface; treat prototype benefit values as final economy balance; invent premium currencies, pack-purchase flows, or shop shortcuts; provide player-operated realm switching; render every one of 300+ castles as permanent DOM/UI objects inside one phone viewport
- Success signals: Four resources recognized from icon and amount alone; resource purpose and all five pack tiers are available on demand in every realm; held packs update inventory and treasury exactly; concurrent upgrades remain visible and navigable from one queue; the World control opens the same locked realm; the oversized map pans by drag or keyboard and exposes all three camera angles; the current sector reports local castle density and world scale; every castle marker announces one owner, alliance, distance, and power; battle-capable player, bandit, gate, and realm targets expose a separate attack action; attack opens a target-specific confirmation before entering; the battlefield preserves the selected target and locked realm, exposes Champion health plus independent health for every visible enemy, accepts touch joystick plus WASD/Arrow movement, and returns to the same world state only after all encounter enemies fall and the reward is claimed; camera changes preserve the current target, bookmark, march, and pan position; selectable resource/world nodes still expose type, distance, expected return, and explicit dispatch; both strategic and close-up gameplay remain legible at compact iPhone height

## Personas and jobs

- Primary personas: Mobile strategy players entering a populated persistent world; returning rulers checking kingdom readiness, nearby players, and territorial changes
- User jobs: Read treasury state quickly; use an owned pack when more material is needed; identify a building; understand what the next level improves; inspect its quote; authorize construction deliberately; monitor every active order; move between Domain and World; pan between populated sectors; distinguish the user's own castle from hundreds of other player castles; inspect owner, alliance, distance, and power; choose the camera angle that fits tactical scanning or spatial exploration; inspect a gathering, hostile, or discovery target; dispatch and track one march
- Key contexts of use: Short portrait-phone sessions, one-handed scanning, variable lighting, reduced-motion settings, rapidly changing player density, and server-populated world sectors

## Information architecture

- Primary navigation: Ceremonial onboarding into the selected realm, then a realm-bound Domain ↔ World loop
- Core routes/screens: Launch → realm selection → Champion → arrival → Domain ↔ World → attack confirmation → Battlefield → victory reward → World
- Content hierarchy: Realm identity → Domain treasury/construction loop or World sector/camera context → selected player castle/combat target → player identity, target power, and attack action → entry confirmation → close-up objective, Champion health, enemy roster/selected target health, movement, and abilities → all enemies defeated → victory reward/claim → preserved World state

## Design principles

- Realm identity is structural: emblem, architecture, spatial rhythm, naming, and state reinforce one another.
- Resources are a compact index, not a storefront: the top rail shows only icon and amount; explanation, pack inventory, and the explicit use action appear after selection; this surface consumes owned inventory and does not sell it.
- Pack use is realm-bound and deliberate: the selected tier opens a separate quantity confirmation; a slider and typed number share the same held-inventory maximum; confirmation consumes that exact quantity, adds denomination × quantity to the locked realm treasury, and refreshes both counts together.
- Selection never spends: the quote precedes the separate construction authorization; accepting the quoted order deducts its exact resources and refreshes the realm treasury.
- Value before cost: current-to-next benefits appear before the resource quote, using compact comparison cards and explicitly provisional review balance.
- Construction time is state, not decoration: an accepted order owns a realm-and-building-specific deadline, the visible timer updates once per second, and zero advances that building to its target level before refreshing the following level, duration, cost quote, and action.
- Construction progress is global within the realm: the city-level queue shows the soonest order and total active count, expands to every active building, and uses each entry as navigation back to that building.
- World selection never dispatches: choosing a map node only updates its target sheet; the separate action starts a single realm-bound march, exposes the live remaining time, and applies the stated return only at completion.
- The world is larger than its viewport: terrain, routes, and targets live on an oversized realm canvas; drag, wheel, and arrow-key movement reveal its edges while objective, capacity, camera, compass, and target controls remain anchored.
- One castle is one user: the player's Domain and every other castle marker represent persistent player-owned entities; castle names, owners, alliance tags, power, protection/conflict state, and map location must come from authoritative player data rather than decorative generation.
- Hundreds of players require spatial hierarchy: the phone loads and labels a bounded nearby sector, summarizes larger-world density, and changes label detail by camera level; production must query, cluster, and recycle off-screen castles instead of constructing 300+ simultaneous controls.
- Combat changes scale deliberately: the World remains the strategic layer for target selection and travel; accepting an attack creates one close-up battlefield instance centered on the Champion, local terrain, and the chosen player/bandit/gate/realm objective.
- Target selection never starts combat: player profile/scouting and attack remain separate; attack opens a confirmation naming opponent, mode, and recommended power; only `Enter battlefield` transitions into direct control.
- Direct control must feel readable before it feels complex: the close-up battlefield uses one movement stick, keyboard fallback, a primary attack, two ability slots, a selectable enemy roster, per-enemy health, and proximity feedback; additional RPG systems remain outside this visual slice.
- Every visible hostile is real: the scene may show a variable number of enemies, but it cannot render decorative hostile silhouettes; tapping a living enemy selects it, Attack and Dash operate on that enemy's position, its attached health bar reflects damage, and a defeated enemy leaves the remaining roster intact.
- Victory requires every enemy plus acknowledgement: only the final hit against the final living enemy stops movement and combat, opens a target-specific reward card, and keeps the defeated battlefield visible beneath it; the single `Collect & Leave` action claims the named reward exactly once and returns to the preserved World target.
- Camera angle changes presentation, not game state: Bird's-eye favors tactical scanning, Quarter is the balanced default, and 3D increases depth and marker extrusion; target selection, bookmark, pan position, active march, and rewards remain unchanged.
- Domain and World are two views of one realm: moving between them cannot change realm identity, treasury ownership, construction state, bookmarks, or active march state.
- Tradeoff: Management information is denser than the opening screens, but the city remains the dominant visual field.

## Visual language

- Color: Midnight Slate `#071017`, Deep Ink `#0d1620`, Moon Ivory `#eee6d2`, Aged Gold `#b99355`, realm-specific accent/deep pairs; muted material cues for food, wood, stone, and gold
- Typography: Iowan Old Style/Georgia for ceremonial titles; SF Pro/system sans for body; compact uppercase utility text for resources and state
- Spacing/layout rhythm: 4–6px internal HUD rhythm, 12–16px component separation, safe-area-aware outer spacing
- Shape/radius/elevation: Cut-corner or softly chamfered dark panels, hairline metallic borders, low-blur shadows, no plastic gloss
- Motion: One deliberate realm/selection transition plus a restrained camera-angle interpolation and active-route motion; effectively static under reduced motion
- Imagery/iconography: Exact realm marks; original code-native resource and world-node glyphs; structural kingdom and world-map grayboxes until production art approval

## Components

- Existing components to reuse: Status row, realm heading, explicit quote/return sheet, primary authorization action, live status copy
- New/changed components: Existing resource, construction, map, sector, player-castle, camera, and target-inspector components remain; `world-battle-action` appears only for battle-capable player/bandit/gate/realm targets; `battle-entry-dialog` confirms opponent, battle mode, Champion, and power recommendation; `battlefield-screen` owns the close-up terrain instance; `battle-champion`, selectable `battle-enemy` actors, attached enemy health bars, selected-enemy HUD, remaining-enemy count, objective card, `battle-joystick`, primary attack, and ability controls form the direct-control combat shell; `battle-reward-dialog` presents victory, encounter identity, the exact treasury reward, and one acknowledgement action
- Variants and states: Four realm accents and map material treatments; four selected-resource states; five pack denominations; quantity entry states; queue empty / one / multiple / completion; world node own castle / player castle / food / stone / hostile / ruin; player castle unselected / selected / bookmarked / protected / alliance / hostile / relocating / shielded in production; camera bird's-eye clustered tags / quarter names / 3D extrusion; map centered / panned / dragging / sector-loading; march ready / active countdown / capacity occupied / complete reward; battlefield enemy living / selected / damaged / defeated; encounter multiple remaining / final enemy / victory locked / reward claim / returned; compact iPhone layout
- Token/component ownership: CSS custom properties and component classes remain local to `index.html` in this review artifact

## Accessibility

- Target standard: WCAG 2.2 AA-informed mobile prototype
- Keyboard/focus behavior: Existing controls remain focusable; the map region uses Arrow keys to pan and Home to recenter; the battle-entry dialog traps focus and Escape cancels before entry; each living battlefield enemy is a native selectable button; WASD/Arrow keys move the Champion, Space attacks the selected enemy, and the on-screen joystick/actions remain native touch controls; victory disables exit/movement/actions and moves focus into the non-dismissible reward dialog; `Collect & Leave` claims once and returns focus and state to the World layer
- Contrast/readability: Top values lead with high contrast; visible resource names move into the selected-resource sheet
- Screen-reader semantics: Resource, pack, queue, timer, and benefit semantics remain intact; the pannable map is named with its drag and keyboard behavior; the sector summary announces nearby player-castle count and larger-world scale; the camera button announces the active and next view; every player castle announces its name and owner while the inspector provides alliance, distance, and power; every non-player world node announces its location and level/type; the target sheet exposes player identity or expected return before action; the march pill reports capacity, destination, and remaining time without announcing every tick; each visible enemy announces its role, health percentage, selection, and defeated state while the HUD announces the remaining enemy count; selection, damage, dispatch, and completion use polite live status; decorative map routes and terrain are hidden
- Reduced motion and sensory considerations: Existing reduced-motion rule remains authoritative; battlefield movement updates position without camera shake, flash, or forced vibration; hit feedback relies on health/state changes rather than rapid full-screen effects

## Responsive behavior

- Supported breakpoints/devices: 375×667 compact iPhone and 390×844 modern iPhone; desktop review shell
- Layout adaptations: Existing Domain layouts remain intact; the World viewport and 1,320×1,200 sector remain clipped inside the phone; battle entry and reward confirmations use compact centered cards; the Battlefield dedicates the flexible center to terrain while health/objective HUD stays at the top and movement/ability controls anchor above the bottom safe area; 375×667 reduces copy before shrinking controls
- Touch/hover differences: Each resource uses a 44-point tap target despite the visually compact rail; selecting it opens owned inventory; tier selection, amount selection, and final consumption are separate steps; the large range pin supports direct dragging while the adjacent numeric field supports exact entry; blank world terrain supports direct one-finger dragging while map controls and nodes remain independent tap targets

## Interaction states

- Loading: Not represented in this static visual foundation
- Empty: Use zero values without removing resource identity
- Error: Construction quote state owns resource shortfalls and order uncertainty
- Success: Existing pack and construction outcomes remain exact; completed world marches clear the occupied slot, advance the chapter objective, report the named return, and add the reward to the same realm treasury; each enemy reaches zero independently, battle victory occurs only when the living count reaches zero, and `Collect & Leave` applies the reward once before returning to the selected World target
- Disabled: Existing explicit pack/construction disabled states remain; while one march is active, other world targets state `March slot occupied`; the active target shows its countdown rather than an actionable dispatch; defeated enemies cannot be reselected or damaged; after total victory, movement, abilities, attack, back, and Escape cannot bypass the required reward acknowledgement
- Offline/slow network: Existing construction/march/world reconciliation rules remain; production battle entry must reserve/validate the target and mode before transition, show reconnect state without simulating authoritative damage, reconcile player/enemy position and health, and return safely to World if an instance expires or becomes invalid

## Content voice

- Tone: Direct, ceremonial only where it helps identity
- Terminology: Domain, World, Outer Realm, Oathbound, march, gathering site, hostile camp, ancient site, resource names, authoritative construction labels
- Microcopy rules: Existing resource/construction rules remain; map nodes name type and level; target sheets state distance, purpose, and expected return; dispatch actions name the intended party; active/completed copy names destination, remaining time, selected enemy, independent health, enemies left, and applied reward; victory copy names the defeated encounter, exact resource amount, locked realm destination, and the `Collect & Leave` acknowledgement

## Implementation constraints

- Framework/styling system: Dependency-free HTML, CSS, and JavaScript review artifact; later translation to existing Unity UI architecture
- Design-token constraints: Extend existing variables before adding new literals; material cues remain muted
- Performance constraints: No downloaded fonts, raster HUD sheets, or animation libraries in the review artifact; production world rendering retains spatial partitioning/pooling/clustering; battlefield instances use bounded actor/effect budgets, pooled terrain/actors, fixed-rate network snapshots with local input prediction where appropriate, and no strategic-world rendering beneath the close-up scene
- Compatibility constraints: iOS 15 production floor; safe areas, reduced motion, and compact portrait layouts
- Test/screenshot expectations: Preserve existing Domain/resource/construction/world checks; capture attack confirmation, a three-enemy Bandit battlefield, damaged-enemy state, and victory reward at 390×844 and 375×667; verify player, bandit, gate, and realm targets expose attack; cancel preserves World; entry preserves realm/target; every visible enemy is selectable and owns an independent health bar; touch/keyboard movement changes bounded Champion coordinates; Attack/Dash use the selected enemy position; defeating one enemy leaves battle active and selects a living enemy; only zero living enemies locks combat and opens the reward; `Collect & Leave` applies the exact resource amount once, exits, and returns to the same World target/camera/pan; run visual verdict at 90+

## Open questions

- [ ] Confirm whether Mana Stone and Ore belong in an expanded treasury drawer or only contextual quotes.
- [ ] Confirm final production, storage, power, capacity, and training-speed balance; current inspector values are illustrative review data and do not appear in the top ledger.
- [ ] Replace the illustrative per-tier Food / Wood / Stone / Gold pack counts with authoritative player inventory data; denominations are fixed at 100, 1,000, 10K, 100K, and 1M.
- [ ] Replace local multi-pack consumption with server-authoritative inventory mutation, idempotency, and reconciliation before production integration; the review prototype applies one confirmed quantity immediately.
- [ ] Confirm whether very large production inventories need a slider cap plus `Use all`, rather than mapping every held pack directly to the range maximum.
- [ ] Define production construction deadline, background-resume reconciliation, notification, and server completion rules; the review prototype uses local elapsed time only.
- [ ] Replace illustrative world coordinates, sites, distances, march capacity, durations, and rewards with authoritative realm-map and economy data.
- [ ] Define production march composition, stamina, travel/return phases, attack confirmation, alliance territory, and server completion rules; this slice proves selection, dispatch, tracking, and return only.
- [ ] Confirm whether all four realm identities coexist on one shared 300+ castle map, occupy separate territories on one world, or use realm-specific shards; this changes castle art, diplomacy, spawning, and sector queries.
- [ ] Define authoritative world dimensions, sector size, castle spawn/relocation rules, maximum concurrency, visible-label budget, spatial update cadence, clustering thresholds, and server interest management for 300+ simultaneous player castles.
- [ ] Define player-castle diplomacy, profile, scouting, attack, rally, reinforcement, shield, relocation, alliance, and privacy states before the placeholder profile action becomes production behavior.
- [ ] Define whether player battles are synchronous PvP, asynchronous defense encounters, or both; authoritative movement, hit detection, latency handling, disconnects, spectators, rewards, losses, protection, and anti-cheat depend on this choice.
- [ ] Define battle-instance party size, Champion progression/loadout, enemy AI, terrain bounds, gates/destruction, realm-war objectives, death/respawn, retreat, time limits, and outcome reconciliation before the close-up shell becomes production combat.
- [ ] Name and specify the staff-controlled paid realm-change service before designing its shop entry.
