# AnotherLife Product Direction

**Status date:** 2026-07-16
**Owner-agent:** this Codex agent
**Final approval:** user

This document records the active player-experience direction that all issue triage, reopened-issue decisions, Unity Hub checks, visuals, UX, and gameplay implementation must respect.

## Target Experience

AnotherLife is an adult high-fantasy realm-war game, not a childlike prototype or debug sandbox. The playable flow must mature toward:

1. **Realm selection at launch.** A new user starts by choosing the realm they want to join. Realm identity is a durable commitment with clear visual, narrative, and gameplay consequences.
2. **2.5D inner-kingdom mode.** After realm selection, the user enters a polished kingdom mode for inner-realm progression, buildings, research, troops, advisors, main questlines, realm economy, preparation, and strategic decisions. This mode should benchmark the clarity and density of Lords Mobile and Infinity Kingdom without copying their art or systems.
3. **3D outer-kingdom warzone.** The most exciting play opens when the user proceeds beyond the kingdom into the realm-vs-realm warzone. This mode should support direct control of the champion/lord/character in a serious MMO-style 3D space, benchmarked against the large-objective feel of Regnum Online and the combat presentation quality target of AION2.
4. **Realm-vs-realm gate conflict.** Main gates of each realm are major conflict locations where players clash, defend, raid, scout, and create social PvP moments.
5. **Warzone objectives.** Outer-warzone objectives include stealing rival realm dragons or unique inner-realm bosses, building PvP points for Warmaster progression, camping crossroads for spontaneous conflict, stealing other realms' gems, and collecting all eight realm gems.
6. **Final wish goal.** Collecting all eight gems enables the global/final wish to the dragon. This is a major long-term game objective and must be protected by durable save, economy, reward, notification, world-state, and anti-duplication rules.

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
→ 2.5D inner-kingdom progression and main questline access
→ 3D outer-kingdom warzone entry
→ realm-vs-realm objective play
→ dragon/boss/gem/Warmaster progression
→ all eight realm gems collected
→ final wish to the dragon
```

Any missing or simulated segment must be explicitly marked as incomplete until it is playable, persistent, performant, and accepted by the user.
