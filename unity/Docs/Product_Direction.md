# AnotherLife Product Direction

**Status date:** 2026-07-16
**Owner-agent:** this Codex agent
**Final approval:** user

This document records the active player-experience direction that all issue triage, reopened-issue decisions, Unity Hub checks, visuals, UX, and gameplay implementation must respect.

## Target Experience

AnotherLife is an adult high-fantasy realm-war game, not a childlike prototype or debug sandbox. The playable flow must mature toward:

1. **Realm selection at launch.** A new user starts by choosing one of four realms. Realm identity is a durable commitment with clear visual, narrative, character-creation, language, and gameplay consequences.
2. **Realm-specific character creation.** After realm selection, the user creates a champion/lord/character using realm-specific visual language while retaining enough customization depth to feel personally unique. The character creator must support user-edited appearance choices, durable profile persistence, and future catalog-safe expansion.
3. **Account realm lock and sub-characters.** A user may create sub-characters under the same account, and the account can provide shared storage across those characters. However, an account is locked to one realm. Users cannot create characters or sub-accounts across multiple realms on the same account. Cross-realm account creation, shared-storage leakage, transfer exploits, and realm-switch bypasses must be rejected by authoritative account/profile rules.
4. **Unique username creation.** The user chooses a champion/lord username before entering the world. Duplicate usernames are disallowed. The username check must be authoritative, race-safe, recoverable, and unavailable-state aware; a local prototype may simulate this, but production must not rely on client-only uniqueness.
5. **3D inner-realm champion start.** The first playable mode after creation is direct-control 3D champion mode inside the user's chosen inner realm. The user follows main questline guidance, learns movement/combat/interactions, and experiences realm identity at character scale before seeing the strategic layer.
6. **2.5D inner-kingdom mode.** The main questline then leads the user into polished 2.5D inner-realm kingdom content for buildings, research, troops, advisors, economy, preparation, and strategic decisions. This mode should benchmark the clarity and density of Lords Mobile and Infinity Kingdom without copying their art or systems.
7. **Return to 3D inner-realm exploration.** After completing the initial 2.5D kingdom questline, the user returns to 3D champion mode to explore the inner realm, continue questline objectives, and proceed toward the main gate.
8. **Outer-realm warzone entry.** Passing through the main gate transitions the user into the 3D outer-realm warzone. This mode should support direct control of the champion/lord/character in a serious MMO-style 3D space, benchmarked against the large-objective feel of Regnum Online and the combat presentation quality target of AION2.
9. **Warzone save pillars.** On entering the outer warzone, quest guidance should lead the user to save pillars or save points where the user can set their death respawn location. A user may have only one active warzone save point. It can be changed only through direct interaction with another valid save pillar/point, not through menus, remote commands, debug paths, or side effects.
10. **Realm-vs-realm gate conflict.** Main gates of each realm are major conflict locations where players clash, defend, raid, scout, and create social PvP moments.
11. **Connected realm continents.** Each realm's warzone area is connected by main bridges between realm continents. The final questline should move players through different realms' warzone territories using these major routes.
12. **Warzone objectives.** Outer-warzone objectives include stealing rival realm dragons or unique inner-realm bosses, building PvP points for Warmaster progression, camping crossroads for spontaneous conflict, stealing other realms' gems, and collecting all eight realm gems.
13. **Center neutral island.** The final questline should end at the center island of the entire outer map. This is the only neutral zone outside the inner realms, placed between the four outer-realm areas. It must force non-PvP mode, provide NPCs for potions, consumables, event items, cash items, and other approved commerce, and become the only place where different realms can chat and trade with each other.
14. **Wish Dragon consideration and shared language.** Normal realm language barriers should prevent cross-realm communication. On the center island, the Wish Dragon's consideration permits different realms to speak and trade through one shared language. Permanent naming for the Wish Dragon, the shared language, and the center island belongs to Codex narrative/content mode and must preserve existing realm-dragon and Veil Watch continuity.
15. **Final wish goal.** Collecting all eight gems enables the global/final wish to the dragon. This is a major long-term game objective and must be protected by durable save, economy, reward, notification, world-state, PvP-state, trade, chat, spawn, and anti-duplication rules.

## Character Progression Direction

- The initial maximum player level is 50.
- The skill tree is available from level 1 so new users can understand growth direction immediately.
- Skill points are earned through leveling and spent through the skill tree. Skill-point grants, refunds, respec limits, and invalid-spend recovery need durable, duplicate-safe rules before production implementation.
- Levels 10, 20, 30, 40, and 50 are major progression turning points. These milestone levels should unlock especially significant, powerful, or highly useful skills that reinforce the user's character/job choice and create clear gameplay identity shifts.
- The main questline should naturally bring ordinary players to roughly level 40 by the time they are ready to proceed through the main gate into the outer-realm warzone. This should feel earned through the inner-realm and 2.5D kingdom progression, not skipped through uncontrolled grinding.
- The level 40 to 50 climb should be a meaningful long-form party-hunting phase. For ordinary players, reaching level 50 from level 40 should take at least about one month of coordinated party hunting rather than a short solo grind.
- Level 50 is the first cap, not the end of competitive growth. Only after reaching level 50, collecting sufficient Warzone points, and acquiring all required Warmaster gear can a player proceed to **True Warmaster**.
- True Warmaster is the high-end RvR MMO progression state. It may grant overpowering realm-war skills, but those skills must be balanced around large-scale RvR objectives, counterplay, readability, cooldowns/resource costs, anti-exploit rules, and performance-safe VFX rather than ordinary early-game combat.
- Warmaster gear, Warzone points, True Warmaster eligibility, and True Warmaster skills must be protected by durable save/reward ledgers and anti-duplication validation.

## Party Grind And Support Role Direction

- AnotherLife should not be balanced so that ordinary players can easily solo grind, hunt, or level up without powerful gear. Solo play can exist, but efficient progression should require either strong gear or coordinated party play.
- The main questline should provide useful accessories and gear, but that gear should be only sufficient enough to let users party-grind or party-hunt neutral mobs for leveling. Main quest rewards should not trivialize solo hunting.
- Neutral mob hunting should reward group composition, positioning, sustain, and support. Healers are a core party-sustain role rather than optional convenience.
- Each realm warzone should include efficient party-hunting areas with considerably fast spawn speed and condensed monster camps. These camps should support sustained group play while still requiring safe spawn pacing, anti-farm controls, encounter readability, and performance-safe population budgets.
- A normal party should usually be balanced around 4 to 5 players for grinding and hunting.
- A **Squad** is a larger group structure for RvR or party PvP fights and should allow up to 10 players.
- Party membership shares experience, gold, and mob loot from eligible mobs according to party reward rules. Party membership does not share Warzone points.
- Squad membership is the structure that can distribute Warzone points to squad members. A squad member must have helped or contributed to the kill or objective to receive Warzone point distribution.
- Potions should grant health regeneration only while the player is in **non-combat mode**. Potions should not replace healers during active combat or allow potion-spam solo sustain.
- Healers and other buffer/support roles must gain fair experience, gold, and resource rewards from grinding/hunting through effective healing and buffing allies. Support contribution must be tracked through durable, anti-abuse participation rules so support players are not punished for not landing killing blows.
- Combat contribution, healing contribution, buff contribution, party eligibility, squad eligibility, proximity, encounter tagging, kill/objective participation, anti-leeching, reward splitting, mob-loot sharing, and Warzone point distribution need explicit contracts before production implementation.

## Quality Bar

- Unity Hub play must stop feeling like a toy/demo and move toward a premium adult fantasy UI/UX and presentation.
- Visual effects should scale by item, gear, skill, creature, boss, and reward tier: higher tier means clearer, richer, more impressive effects, while still honoring performance and install-size budgets.
- 2.5D kingdom mode should be dense, readable, strategic, and polished rather than decorative.
- 3D warzone mode should prioritize readable combat, strong silhouettes, objective clarity, responsive control, encounter feedback, and meaningful realm identity.
- Prototype primitives are acceptable only as explicitly labeled temporary development placeholders; they are not production visual proof.

## Optimization Bar

All product-direction work must preserve broad device reach and the lowest feasible install size:

- prefer scalable quality tiers over one expensive default;
- pool VFX and combat presentation objects;
- compress and deduplicate assets;
- avoid unnecessary generated binaries, duplicate catalogs, and unused imports;
- measure or disclose performance, memory, package-size, and install-size impact for every relevant PR.

## Issue Policy

Closed issues are not automatically trusted as solved. If current source, Unity Hub play, user testing, or PR review shows that a closed issue still blocks this direction, this Codex agent must reopen it or create a focused follow-up with exact evidence. The goal is not to keep issue counts tidy; the goal is to finish the game correctly.

## End-To-End Development Gate

By the end of development, this Codex agent must run and verify the game from launch through the final objective path:

```text
launch
→ realm selection
→ realm-specific character creation
→ account realm lock, optional same-realm sub-characters, and shared storage
→ unique username creation
→ 3D inner-realm champion start and main questline
→ 2.5D inner-kingdom progression
→ 3D inner-realm return and main-gate approach
→ party-oriented neutral mob hunting with healer/buffer support progression
→ outer-realm warzone entry
→ warzone save-pillar interaction
→ realm-vs-realm objective play across connected realm continents
→ level 50 progression, Warzone points, and Warmaster gear completion
→ True Warmaster unlock
→ dragon/boss/gem/Warmaster progression
→ all eight realm gems collected
→ center neutral island under Wish Dragon consideration
→ final wish to the dragon
```

Any missing or simulated segment must be explicitly marked as incomplete until it is playable, persistent, performant, and accepted by the user.
