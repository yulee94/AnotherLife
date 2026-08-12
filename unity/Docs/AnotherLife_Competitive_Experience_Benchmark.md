# AnotherLife Cross-Genre Competitive Experience Benchmark

**Status date:** 2026-08-12

**Primary delivery mode:** Codex coordination/review

**Upstream:** [#471](https://github.com/yulee94/AnotherLife/issues/471)

**Authority:** This file updates and supersedes the narrower 2026-07-16 benchmark at this same path. It does not supersede `Product_Direction.md`, approved narrative source, co-developer terrestrial-design source, runtime specifications, or user decisions.

**Source manifest:** `Benchmarks/Cross_Genre_Benchmark_Source_Manifest_2026-08-12.json`

## 1. Decision and non-copy boundary

AnotherLife uses successful games to study general design principles, measurable quality, failure modes, and validation methods. It must remain an original adult high-fantasy realm-war game.

Reference products do **not** authorize copying their art, layout, iconography, fonts, terminology, characters, classes, lore, maps, camera sequences, effects, formulas, source code, audio, monetization, or branded interaction patterns. Publisher media is reference-only. This change stores URLs and provenance, not third-party media binaries.

External evidence is classified as:

1. `OfficialDeveloper` — publisher/developer material. Use for documented intent, rules, and presentation evidence, while pinning dates and patches.
2. `MaintainedCommunity` — established, maintained community reference. Use to form tests; do not treat it as a developer contract.
3. `CommunityReverseEngineering` — player-derived behavior or formulas. Use only as a hypothesis requiring independent verification.
4. `ResearchStandard` — peer-reviewed research or a published statistical/matchmaking specification.

Every coefficient, threshold, target, sample size, score weight, timing budget, and performance budget proposed for AnotherLife is a **HYPOTHESIS / PROPOSED TARGET** until simulation, telemetry, representative playtest, A1 technical disposition, and user balance approval. Statistical passage never replaces accessibility, visual-readability, creative, integrated-playtest, or release gates.

## 2. Benchmark portfolio

### 2.1 3D MMORPG and mass-PvP track

| Reference | Evidence-backed principle | Original AnotherLife application |
| --- | --- | --- |
| World of Warcraft | A configurable HUD can emphasize health, target, objective, and immediate actions while allowing saved layouts and accessible interaction options. Battleground rules illustrate explicit objective timers and interaction windows. `[MMO-WOW-UI-001]` `[MMO-WOW-BLITZ-001]` | Protect the central combat scan path. Make secondary information contextual, support saved HUD profiles, and expose objective state without reproducing Blizzard's layout or skin. |
| Black Desert | Large-war presentation and combat tuning use mode/stat limits and broad hit-count, accuracy, evasion, mitigation, and damage-system revisions. `[MMO-BDO-NODEWAR-001]` `[MMO-BDO-COMBAT-001]` `[MMO-BDO-COMBAT-002]` | Separate combat profiles by mode, audit every cap bypass, and count cosmetic, tactical, and hostile-telegraph effects separately. Premium spectacle may not hide threats. |
| Guild Wars 2 WvW | Crowd presentation can degrade from full model to fallback/nameplate while preserving gameplay-relevant actors. WvW communicates ownership, siege, supply, fronts, and objectives at map scale. `[MMO-GW2-CULL-001]` `[MMO-GW2-WVW-001]` | Degrade fidelity before threat truth. Preserve allegiance, nameplate, targetability, cast/telegraph, collision, and objective state at every crowd tier. |
| The Elder Scrolls Online Cyrodiil | Reduced combat complexity and normalized battle profiles have been used to increase supported large-war population and improve performance. `[MMO-ESO-VENGEANCE-001]` | Profile actor rendering, ability/proc evaluation, server simulation, network rate, and client frame time as one system; do not optimize models while leaving effect evaluation unbounded. |
| Throne and Liberty | Large sieges reinforce allegiance with color and leader markers, and source-scoped display settings distinguish all, guild, party, and self VFX. `[MMO-TL-SIEGE-001]` `[MMO-TL-READABILITY-001]` | Provide color-independent allegiance shapes/icons plus separately configurable source categories, then validate that hostile telegraphs, objectives, and useful allied fields remain protected. |
| Albion Online | Map ownership, attack direction, transport, supply, fortification, zerg pressure, focus-fire protection, and AoE behavior form a coherent mass-war information model. `[MMO-ALBION-PROVINCES-001]` `[MMO-ALBION-ZERG-001]` `[MMO-ALBION-PERF-001]` | Treat `find front → travel → join → understand → contribute` as a measurable journey. Use several bounded pressure controls instead of a secret, overwhelming underdog buff. |
| Champions of Regnum | The developer-provided store description centers long-term play on choosing a realm and joining large-scale, player-driven Realm-versus-Realm and PvP conflict. `[MMO-REGNUM-RVR-001]` | Preserve realm identity and a durable group-war reason to return while keeping AnotherLife's realms, objectives, maps, narrative, combat, and rewards original. |
| New World: Aeternum | Competitive maps have been revised for route parity, visibility, collision clarity, lighting, and reduced vegetation; wars use progressive objectives and siege. `[MMO-NEWWORLD-MAP-001]` `[MMO-NEWWORLD-WAR-001]` | Give every war route a visibility and travel-time budget. Environment art must predict collision and preserve silhouettes in every supported weather/lighting state. |
| Aion / Aion 2 | Current developer material separates PvE/PvP coefficients and constrains consecutive immunity effects; promoted modes include arena, battlefield, rift, and large-force conflict. `[MMO-AION2-COMBAT-001]` `[MMO-AION2-MODES-001]` | Use Aion 2 as an aspirational rendering/action-impact reference, not proof of supported AnotherLife scale. Keep PvE/PvP math and immunity-chain telemetry explicit. |

### 2.2 2.5D kingdom-management track

| Reference | Evidence-backed principle | Original AnotherLife application |
| --- | --- | --- |
| Infinity Kingdom | City reconstruction, composition layers, and alliance territory connect visible city growth to shared map control, rallies, assistance, research, and taxes. `[KING-INFINITY-STORE-001]` `[KING-INFINITY-ALLIANCE-001]` | Show meaningful world-state change after construction and connect realm territory to legible shared benefits. Do not copy city layout, heroes, dragons, or UI. |
| Lords Mobile | The advertised loop connects buildings, research, troops, heroes, formations, counters, guilds, rallies, and war formats. `[KING-LORDS-STORE-001]` | Keep the command loop compact and expose counter/readiness information. Reject paid automation or acceleration as a requirement for competitive participation. |
| Clash of Clans | A compact build–attack–upgrade loop, friendly/practice modes, replays, shared construction, and published balance discussion make actions and learning legible. `[KING-CLASH-STORE-001]` `[KING-CLASH-BALANCE-001]` `[KING-CLASH-CAPITAL-001]` | Teach one complete loop, support risk-free rehearsal, measure composition concentration as well as outcomes, and allow small alliance contributions without copying base layouts. |
| Age of Empires Mobile | Official material emphasizes troop control, siege targets, gates, central structures, terrain/weather, alliance scale, and a city that is both home and battlefield. `[KING-AOEM-OFFICIAL-001]` `[KING-AOEM-STORE-001]` | Use terrain, route, gate, and siege readability as the scale benchmark. Quality tiers must keep selection, route, ETA, threat, and objective state legible under load. |
| Rise of Kingdoms | Storefront material presents a continuous city/world map, zoom-based context, terrain/passes, and armies that can be redirected while moving. `[KING-ROK-STORE-001]` | Preserve spatial continuity when technically safe, use progressive disclosure by zoom, and specify replace/queue/cancel semantics for march orders. |
| Whiteout Survival | Current update notes emphasize rally preparation, the player's own squad, reward previews, facility labels, alliance recommendations, and contextual messages. `[KING-WHITEOUT-UPDATE-001]` | Make rallies self-locating and honest: initiator, target, preparation, capacity, own squad, travel, risk, withdrawal, and reward conditions. |
| Boom Beach | Official material frames a scout–plan–attack loop, HQ dependency spine, layouts/trials, and returning-player authentication before a forced tutorial. `[KING-BOOM-OFFICIAL-001]` `[KING-BOOM-ONBOARD-001]` | Provide scouting or simulation before permanent loss, clear dependency progression, and recovery/login before forced onboarding replay. |

## 3. One connected AnotherLife experience

The benchmarks serve one product journey, not two unrelated games:

```text
3D champion-scale introduction
→ 2.5D inner-kingdom command and preparation
→ 3D inner-realm return
→ outer-warzone entry
→ readable objective-driven realm conflict
→ kingdom consequences and recovery
```

The kingdom layer should explain what is being prepared, where pressure exists, what a decision costs, and when it resolves. The 3D layer should let the player personally experience those consequences with readable control, targets, objectives, allies, threats, and recovery. A state change must not be claimed in one mode until its authoritative result is accepted and projected in the other.

## 4. Original AnotherLife presentation rules

### 4.1 Visual identity

- Adult high fantasy, grounded materials, strong realm silhouettes, restrained ornament, and deliberate spectacle.
- Realm identity uses silhouette, heraldry, material, motion, and sound in addition to hue.
- High-tier effects add authored shape, timing, impact, and environmental response; they do not simply add particles or screen coverage.
- Prototype primitives remain labeled development placeholders and are never production visual evidence.

### 4.2 Combat information hierarchy

Protected information, in order:

1. Player health/resource/control state and immediate legal action.
2. Current target, cast, defense, and actionable status.
3. Hostile telegraphs, damage direction, and area hazards.
4. Party/squad health, role-critical effects, and revive state.
5. Objective owner, progress, contest state, timer, and route.
6. Realm/alliance identity and commander markers.
7. Rewards, ambient effects, damage numbers, and decorative presentation.

Lower-priority layers may be reduced before a higher-priority layer. No quality tier may cull an actionable hostile actor without retaining a truthful fallback marker.

### 4.3 Crowd and VFX degradation

Proposed quality tiers must independently control:

- full/fallback/nameplate-only character presentation;
- animation update distance and frequency;
- self, target, party, squad, realm, hostile, and ambient VFX;
- damage-number aggregation;
- shadow, weather, decal, foliage, and post-processing density;
- nameplate population, detail, and opacity;
- audio voice limits and priority.

Self, target, hostile telegraphs, objectives, collision truth, and critical support fields are protected. Accessibility settings can reduce motion, shake, flashes, trails, decals, and nonessential particles without reducing semantic information.

### 4.4 Camera

- Exploration camera prioritizes stable horizon, player visibility, obstruction recovery, indoor framing, and user-controlled recentering.
- Combat camera preserves target and ground-telegraph visibility; shake is event-scaled, collision-safe after shake, and reducible to zero.
- Large-objective framing reveals the objective and approach routes without removing responsive direct control.
- Kingdom camera uses predictable pan/zoom, stable selection, progressive labels, and no forced orbit when opening an inspector.
- Camera behavior must be tested at low frame rate, with touch/controller/mouse, reduced motion, and close geometry.

## 5. 2.5D kingdom interaction benchmark

### 5.1 City readability

- One approved city anchor communicates overall progression without copying a comparator's landmark.
- Every selectable building has a distinct overview silhouette and non-color-only states for unbuilt, available, upgrading, blocked, damaged, completed, capstone, and unavailable.
- Selection from the world and command deck converges on one stable building identity and inspector.
- At each zoom tier, show only actionable labels; facility, level, ownership, threat, and objective detail appear progressively.

### 5.2 Construction truth

The existing `Architecture/Live_Kingdom_Construction_UX_Design.md` remains authoritative. Benchmarking reinforces its sequence:

```text
select stable target
→ receive authoritative quote
→ inspect current/next state, full cost, sufficiency, duration, and consequence
→ explicitly commit once
→ render accepted/rejected/rolled-back/unresolved result
```

The discovery tap never spends. The UI never recomputes gameplay costs or optimistically persists a level. Cancellation, queues, speedups, prerequisites, demolition, and premium acceleration remain absent until separately approved.

### 5.3 March, rally, and map truth

Every future march must communicate owner, destination, route, stance, departure, ETA, and state. Re-tasking must state whether the new order replaces, queues after, or cancels the current order. Friendly, allied, neutral, hostile, contested, and selected states require hue plus shape/outline/pattern.

Every future rally must communicate initiator, target, preparation deadline, capacity/fill, the player's own contribution, travel ETA, withdrawal consequence, risk, and reward conditions. External chat must not be necessary to understand or join the basic interaction.

### 5.4 Onboarding and ethical operation

- Teach one complete loop before exposing the full metagame: inspect need → quote/upgrade → observe result → scout → march → resolve → return.
- Allow authentication/account recovery before forcing a returning player through onboarding.
- Skip and replay cannot corrupt progression or forfeit required rewards.
- Do not interrupt the first complete kingdom loop with a store, random-item, or paid acceleration prompt.
- Reject paid competitive strength, paid exclusive counters, randomized PvP power, opaque `Power` scores, chore/red-dot accumulation, deliberately painful timers, and unhealthy alliance attendance pressure.
- Offline loss, if ever approved, requires warning, limits, protection, recovery, and fair retaliation rules.

These anti-patterns are product guardrails, not claims that any one referenced title is wholly defined by them.

## 6. Measurement scorecard

All values below are proposed measurement categories. Numeric targets must be versioned in an accepted implementation or balance specification.

### 6.1 Visual and comprehension

- Five-second still test: identify player health, target, objective, immediate legal action, and highest threat.
- Crowd test: count visible actionable threats versus server-authoritative threats at each density/quality tier.
- Telegraph test: record tell-to-impact time, occlusion, contrast, shape recognition, and audio-off parity.
- Allegiance test: recognize self/party/squad/realm/hostile/contested states in color, grayscale, and common color-vision simulations.
- Kingdom test: identify selected building, state, cost, duration, next result, active march, active front, and war alert without opening unrelated panels.

### 6.2 Performance

Record distributions rather than average FPS alone:

- client CPU and GPU frame time p50/p90/p99;
- server simulation and event-processing time;
- input-to-visible-response latency;
- network event rate, latency, packet loss, and reconnect behavior;
- draw calls, triangles, animation cost, particle count, overdraw, texture memory, and allocations;
- visible full/fallback/nameplate actor counts;
- idle city, rapid zoom, 20/50/100 march, mass rally, siege, chat overlay, and thermal-endurance scenarios;
- download, installed size, memory, battery, and thermal impact as separate fields.

Stable 60 FPS where supported and a deliberate stable 30 FPS lower tier are proposed platform goals, not evidence of current performance. Frame budgets are 16.7 ms and 33.3 ms respectively; actual supported devices and pass thresholds require A1/user acceptance. `[PLATFORM-ANDROID-FRAME-001]`

### 6.3 Accessibility

- UI scale/text-size support, safe areas, focus order/restoration, keyboard/controller/touch parity, and rebinding.
- Non-color-only state, readable contrast, captions/subtitle background, audio-off parity, and assistive-technology labels.
- Reduced shake, reduced motion, flash control, VFX density, and camera sensitivity/inversion.
- Touch targets and text sizes follow target-platform guidance, then are validated on real supported devices. `[PLATFORM-APPLE-GAMES-001]`
- No screenshot-only audit can certify WCAG, input parity, assistive technology, photosensitivity, or device performance.

## 7. Priority acceptance criteria

### P0 — required before broad implementation claims

- One source manifest row supports every external benchmark observation.
- Threat, objective, selection, cost, duration, and result truth are protected across quality tiers.
- No gameplay-relevant actor disappears without a truthful fallback.
- Construction and future march/rally operations expose accepted, rejected, rolled-back, and unresolved states.
- Accessibility semantics survive reduced effects and audio-off use.
- Performance evidence separates client, server, network, and input costs.
- Third-party reference media is never imported as a production asset.

### P1 — required for a credible integrated slice

- Identical anchors captured at low/medium/high quality and representative aspect ratios.
- City/world/champion/warzone transitions preserve state, focus, camera, and recovery.
- Crowd-density, VFX filtering, map-front discovery, route parity, and time-to-action test scripts pass at accepted targets.
- Combat and objective telemetry uses a versioned balance profile and privacy-safe identifiers.
- Physical mobile plus keyboard/mouse and controller evidence exists where supported.

### P2 — polish and scale

- Player-configurable HUD profiles, deeper map filters, replay/spectator tooling, and broader device/thermal matrices.
- Long-duration mass-war and kingdom endurance, localization stress, assistive-technology testing, and user preference studies.
- Tiered audiovisual reward presentation after profiler and user visual approval.

## 8. Evidence and approval limits

This benchmark is planning evidence. It approves no implementation, asset, source identity, visual direction, balance value, monetization, matchmaking policy, save/network/catalog change, supported population, supported device, player acceptance, milestone, production state, or release.

The companion `Benchmarks/Combat_Balance_Methods_2026-08-12.md` provides original parameterized methods and proposed starting hypotheses. It is not a proprietary formula reproduction and is not production tuning. The user retains final visual-design, balance, integrated-playtest, product, milestone, and release approval.
