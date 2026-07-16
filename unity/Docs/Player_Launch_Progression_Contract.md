# Player Launch and Progression Contract

**Status date:** 2026-07-16
**Primary Codex mode:** coordination/review
**Source:** User launch-flow, progression, party, squad, and warzone direction from the active AnotherLife conversation.

## Purpose

This contract converts the user's required first-session and long-term progression experience into dependency-ordered, testable requirements. It does not implement runtime behavior, author final narrative copy, finalize balance values, or approve visual design. Later Codex narrative/content, terrestrial-design, and engineering PRs must consume this contract instead of inventing a parallel launch flow.

## Experience Benchmarks

- Champion-scale 3D presentation and combat readability benchmark: AION 2-quality adult fantasy direction, with strong character identity, high-impact skill effects, readable class/loadout state, and non-childlike UI.
- Kingdom 2.5D benchmark: Lords Mobile and Infinity Kingdom-style realm management density, clear economy progression, and repeated-use operational UI rather than a toy-like layout.
- Major RvR gameplay goal benchmark: Regnum Online-style realm identity, contested warzone routes, party/squad PvP, campable crossroads, realm objectives, boss/gem theft, and endgame realm conflict.

Benchmarks are direction references, not permission to copy protected assets, names, UI, icons, lore, or proprietary systems.

## Required Player Flow

1. The game launches to realm selection with exactly four realm choices.
2. Character creation follows realm selection and must expose realm-specific identity options plus player customization.
3. Username creation happens before entering gameplay. Duplicate usernames are rejected before profile finalization.
4. A new player starts in 3D champion mode inside the selected inner realm and follows the main questline.
5. The main questline naturally introduces 2.5D inner-realm kingdom management and growth.
6. After the 2.5D kingdom questline gate, the player returns to 3D champion mode, continues inner-realm questing, and proceeds toward the outer realm through the main gate.
7. Leaving the inner realm introduces warzone save pillars. A player may have only one active warzone spawn savepoint.
8. The active warzone savepoint can be changed only through direct interaction with a save pillar/savepoint.
9. The final introductory questline routes the player through different realm warzone areas.
10. Realm warzones are connected by main bridges between each realm continent.
11. The introductory outer-realm questline ends at the center island, the only neutral zone in the outer realm.
12. The center island forces non-PvP mode, allows cross-realm chat and trade through the wish dragon's aid, and hosts NPCs for potions, consumables, event items, cash items, and related services.

## Progression Contract

- Initial level cap: 50.
- The main questline to the outer-realm warzone should leave a typical player around level 40.
- Level 40 to 50 should require at least about one month of coordinated party hunting for an ordinary player.
- Skill trees are visible from level 1, while skill points are earned through leveling and progression.
- Levels 10, 20, 30, 40, and 50 are turning points with meaningfully stronger or more defining skills.
- True Warmaster progression unlocks only after level 50, sufficient warzone points, and a complete Warmaster gear requirement.
- True Warmaster skills may be overpowering in RvR context, but they require explicit balance, counterplay, readability, cooldown, and performance review before implementation.

## Account and Character Contract

- Players may create subcharacters under the same account/profile.
- Shared storage is allowed across subcharacters.
- An account cannot create characters across multiple realms. Realm allegiance is account-wide unless a later user-approved irreversible migration/reset policy is specified.
- Username uniqueness must be enforced before character creation completes.
- Duplicate username rejection must be deterministic, visible, and non-destructive to the in-progress character editor state.

## Party, Squad, and Reward Contract

- Normal hunting party size target: 4 to 5 players.
- Squad maximum size: 10 players.
- Party is the default grouping model for leveling, gold, and mob loot.
- Party members share experience, gold, and loot from mob kills according to later balance rules.
- Party membership does not share warzone points.
- Squad is used for RvR, party PvP, objective fights, and warzone coordination.
- Squad distributes warzone points only to squad members who contributed to the kill or objective.
- Contribution must be explicit and auditable. Examples include damage, healing, shielding, buffing, debuffing, control, objective interaction, or other approved combat support within a valid proximity/time window.
- Non-contributing squad members receive no warzone point distribution from that kill/objective.

## Combat, Hunting, and Sustain Contract

- Solo grinding should not be easy or efficient without powerful gear.
- The main questline may provide useful gear and accessories, but only enough to make party grinding viable, not enough to trivialize solo progression.
- Healers must be important for party sustainability.
- Buffers, healers, and support roles can earn experience, gold, resources, and valid contribution credit through healing, shielding, buffing, and other approved ally support.
- Potions provide health regeneration only while out of combat unless a later explicit item design creates a bounded exception.
- Combat-state transitions must prevent potion abuse through rapid in/out combat toggling.

## Warzone Hunting Contract

- Each realm warzone must include efficient party hunting areas.
- Warzone hunting areas should have relatively fast spawn speed and condensed monster camps to support 4 to 5 player grinding.
- Spawn density must include performance, readability, pathing, anti-exploit, and server/client budget constraints before runtime implementation.
- Condensed camps should not become effortless solo leveling spots; monster tuning, assist behavior, sustain pressure, rewards, and respawn timing must favor coordinated groups.
- Warzone monster camps must not silently grant warzone points unless a later RvR/objective rule explicitly authorizes it.

## Outer-Realm Objective Contract

- Players should be able to fight realm-vs-realm battles at each realm's main gate.
- Outer-realm activities include stealing another realm's dragon or unique boss, stealing realm gems, earning PvP/warzone points toward Warmaster, fighting at crossroads, and contesting bridge routes.
- Collecting all eight realm gems enables the global/final wish to the dragon.
- Realm gem, wish dragon, boss, Warmaster, and objective systems must be implemented through authoritative contracts and save-safe transactions before they can be treated as complete.

## Neutral Center Island Contract

- The center island is the only neutral outer-realm zone.
- PvP must be forcibly disabled inside the neutral zone.
- Cross-realm chat and trade are allowed only under the wish dragon's language/consideration rule.
- Outside the center island, realm language separation remains the default unless a later narrative/content packet changes it.
- Neutral-zone NPC services may include potions, consumables, event items, cash items, and related vendors, but economy/cash/item rules require separate approval and implementation contracts.

## Dependency Order

1. Codex coordination/review: this contract and dependency mapping.
2. Codex narrative/content: realm-selection framing, first inner-realm questline, kingdom handoff quest, outer-realm gate quest, save-pillar instruction, bridge/center-island discovery, wish dragon language rule, and localization-facing text.
3. Codex coordination/review: implementation specifications for profile/realm lock, username uniqueness, quest gates, combat-state potion rules, party/squad contribution, warzone camps, save pillars, neutral-zone rules, and objective transactions.
4. Codex engineering: runtime contracts, save defaults/migrations, validators, UI flows, combat/grouping services, warzone systems, scene integration, tests, and performance budgets.
5. Codex coordination/review plus applicable source-mode fidelity checks: verify implementation against approved source and this contract.
6. User: playtest and product/creative/balance approval.

## Issue Mapping

- #173 realm selection and committed realm identity.
- #184 champion customization and character identity.
- #165 progression definition/order and level/skill gates.
- #163 economy safety for resources, gold, potions, and warzone credits.
- #166 territory/warzone ownership and income.
- #171 Warmaster gear and progression.
- #169 realm gems, wish gate, and global wish.
- #168 boss loot and unique boss rewards.
- #180 champion combat and support contribution.
- #156 quest definition authority and questline data validation.
- #137 save hardening and migration safety.
- #150 production scene/player build readiness.

## Acceptance Criteria for Later Implementation

- A new profile cannot enter gameplay before selecting one realm and creating one unique username.
- Realm allegiance persists across subcharacters and shared storage.
- Duplicate username attempts fail without profile corruption or editor-state loss.
- A new player can follow the required 3D inner realm to 2.5D kingdom to 3D outer gate sequence.
- The outer-gate sequence introduces exactly one active warzone savepoint controlled only by save-pillar interaction.
- The player reaches the outer warzone at approximately level 40 under expected quest completion assumptions.
- Level 40 to 50 pacing is validated against party-hunting reward/time assumptions and is not tuned for rapid solo completion.
- Party reward sharing excludes warzone points.
- Squad warzone point distribution requires contribution evidence.
- Healers and buffers receive progression credit for valid ally support.
- Potions do not heal in combat through regeneration unless an approved exception exists.
- Each warzone has party-oriented condensed hunting camps with performance and anti-exploit limits.
- Main bridges, realm gates, crossroads, center island, neutral PvP suppression, cross-realm trade/chat, and wish dragon language rules are represented in source and runtime contracts before release claims.

## Validation for This Coordination PR

- Documentation-only change.
- No runtime, save, scene, prefab, asset, catalog, or build-system file is changed.
- No shared-file lock is touched.
- Later PRs must run relevant Unity, Android, save, contract, performance, and PlayMode validation according to their implementation scope.
