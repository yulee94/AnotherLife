# AnotherLife Competitive Experience Benchmark

**Status date:** 2026-07-16
**Primary owner mode:** Codex engineering documentation
**User direction:** Benchmark AION 2 graphics/quality, Lords Mobile and Infinity Kingdom for Kingdom 2.5D play mode, and Regnum Online/Champions of Regnum for major gameplay and goal structure.
**Canonical workspace:** `D:\260711\MY\AndroidStudioProjects\AnotherLife`
**Scope:** Product-quality benchmark and implementation guardrails only. No runtime, asset, scene, save, catalog, narrative, terrestrial creature design, balance, monetization, or third-party IP is changed by this document.

## 1. Purpose

This benchmark defines the quality bar AnotherLife should aim toward without copying protected names, art, UI skins, icons, lore, classes, factions, monetization patterns, or exact system designs from the reference products.

Use the references by layer:

| AnotherLife layer | Primary benchmark | What to learn |
| --- | --- | --- |
| Premium fantasy graphics and 3D presentation | AION 2 | High-end fantasy readability, vertical spectacle, character-expression depth, boss/dungeon presentation, high-tier VFX intensity. |
| Kingdom 2.5D play mode | Lords Mobile and Infinity Kingdom | Dense kingdom status, readable upgrades, troops/heroes/alliances, timers, world-map objectives, stylized strategy readability. |
| Major gameplay and long-term goal | Regnum Online / Champions of Regnum | Three-realm identity, open realm war, forts/castles, invasions, realm pride, group PvP as the durable reason to return. |

## 2. Source Baseline

Sources checked on 2026-07-16:

- AION 2 official global/Japan site: `https://aion2.ncsoft.jp/ja/contents`
- AION 2 Steam page: `https://store.steampowered.com/app/3393110/AION_2/`
- Lords Mobile Steam page: `https://store.steampowered.com/app/1041320/Lords_Mobile/`
- Lords Mobile official site: `https://lordsmobile.igg.com/`
- Infinity Kingdom official site recorded in existing visual direction: `https://infinitykingdom.gtarcade.com/m/en/`
- Infinity Kingdom Google Play page recorded in existing visual direction: `https://play.google.com/store/apps/details?id=com.gtarcade.ioe.global`
- Champions of Regnum Steam page: `https://store.steampowered.com/app/222520/Champions_of_Regnum/`
- Champions of Regnum official site: `https://www.championsofregnum.com/`

Observed reference facts used by this benchmark:

- AION 2 is positioned as a PC Unreal Engine 5 fantasy MMORPG with aerial combat, strong verticality, a 36-times-larger world claim versus the original AION, over 200 dungeons, and over 200 character customization options on Steam.
- AION 2's Steam page lists FHD "Very Low" minimum around GTX 1050 Ti 4 GB / 8 GB RAM and FHD "Low" recommended around RTX 2070 8 GB / 16 GB RAM, which means its presentation target is visually aspirational but hardware-heavy.
- Lords Mobile's Steam page frames the loop around building a kingdom, upgrading buildings, research, troop training, hero leveling, troop formations, guild alliances, online PvP, and cross-platform multiplayer.
- Champions of Regnum's Steam page frames the core game as a free-to-play MMORPG focused on Realm versus Realm and PvP battles, with realm choice and large-scale player-driven battlefields as the main identity.
- Infinity Kingdom is used here as a stylized 2.5D kingdom benchmark for readable fantasy city presentation, immortal/hero identity, dragon spectacle, and cleaner mobile-strategy visual density. The existing project visual-direction doc already records its official and Google Play reference links.

## 3. Non-Copy Rules

AnotherLife must not copy:

- AION, Daeva, Elyos, Asmodian, Atreia, Abyss, class names, wing silhouettes, logos, icons, UI skin, dungeon names, or exact character designs.
- Lords Mobile kingdom layout, hero names, troop icons, event names, monetization surfaces, item icons, or UI arrangement.
- Infinity Kingdom immortal names, dragon designs, city layout, UI skin, iconography, or faction presentation.
- Regnum/Champions of Regnum realm names, race/class names, fort/castle layouts, invasion rules, boss names, or reward structure.

Allowed use:

- Benchmarking quality, density, readability, progression clarity, goal loops, performance budgets, and validation methods.
- Designing original AnotherLife realms, factions, heroes, bosses, terrestrial creatures, VFX, kingdom surfaces, and war goals through Codex-owned source packets and GPT-reviewed engineering handoffs.

## 4. AION 2 Graphics And Quality Benchmark

### 4.1 Quality Intent

AION 2 sets the aspirational bar for "premium fantasy first impression":

- readable high-fantasy silhouettes at combat distance;
- strong lighting, material contrast, and emissive accents;
- aerial and vertical composition that makes the world feel layered;
- boss/dungeon scenes that look authored rather than assembled from prototype primitives;
- character customization depth that players can recognize immediately;
- skill and item VFX that escalate visibly with rarity and power.

AnotherLife target: use AION 2 as the upper visual reference for 3D Champion, boss, realm, and dungeon presentation, while preserving AnotherLife's own realm-command identity.

### 4.2 AnotherLife Application

- Champion Mode should keep pushing from procedural prototype toward authored mesh, material, animation, VFX, and icon pipelines once #180 and catalog contracts stabilize.
- Character customization should grow toward catalog-backed options, preview safety, and visible identity depth through the merged `Champion_Customization_Integrity_Spec.md`.
- Bosses should have strong silhouettes, clear telegraphs, distinct arena language, readable phases, and VFX escalation that can be understood without text.
- Verticality should appear first as vistas, sky layers, elevated arenas, flying spell effects, and readable height cues. Full aerial traversal/combat is not implied by this benchmark unless separately specified.

### 4.3 Performance Guardrail

Do not chase AION 2's apparent hardware profile blindly. AnotherLife should define scalable Unity targets:

| Tier | Intent |
| --- | --- |
| Low | Stable gameplay, reduced particles, simplified shadows, lower texture budget, readable UI and telegraphs preserved. |
| Medium | Default development target with full gameplay readability and moderate VFX. |
| High | Premium lighting, stronger VFX layering, higher texture detail, richer post-processing, still gameplay-readable. |
| Ultra/future | Aspirational screenshots and trailers only after profiler evidence proves stability. |

## 5. Kingdom 2.5D Play Mode Benchmark

### 5.1 Lords Mobile Learnings

Lords Mobile is useful for the compact kingdom-management loop:

- the city/kingdom screen exposes upgrades, research, troop training, hero progression, and resources quickly;
- formations and troop-type counters create an accessible strategy language;
- guild/alliance and world events make the kingdom feel connected to other players;
- map objectives and timed activities make the kingdom screen a launchpad, not a static menu.

AnotherLife target: Kingdom 2.5D should be a dense command surface where the player reads realm status, construction, training, heroes/champions, resources, war alerts, quests, and faction pressure in one glance.

### 5.2 Infinity Kingdom Learnings

Infinity Kingdom is useful for stylized fantasy readability:

- kingdom buildings should have distinct silhouettes and upgrade states;
- hero/immortal/dragon-style units show that strategy surfaces can still feel characterful;
- readable color coding and controlled stylization can make a dense city easier to scan;
- fantasy spectacle can exist in 2.5D without turning the kingdom UI into a combat scene.

AnotherLife target: Kingdom 2.5D should use original buildings, realm banners, hero/champion anchors, terrestrial habitats, and VFX accents to communicate function and progression.

### 5.3 2.5D Presentation Requirements

Kingdom play mode should eventually support:

- fixed or semi-fixed 2.5D camera with stable touch and mouse interactions;
- layered parallax, readable building silhouettes, and non-overlapping labels;
- realm-color accents that identify ownership without one-note palettes;
- construction/research/training timers with compact status and clear completion feedback;
- resource deltas and passive income visibility;
- war readiness, territory ownership, and defense warnings;
- tiered visual upgrades for buildings, gear facilities, champions, and world objectives;
- accessible states for disabled, blocked, insufficient-resource, ready, damaged, upgrading, and completed actions.

### 5.4 Kingdom UI Density Rule

The kingdom screen is a working command interface, not a landing page. It should favor:

- compact panels;
- predictable icon controls;
- scan-friendly resource bars;
- map/status tabs;
- clear alerts;
- short labels;
- no decorative card nesting;
- no text explaining how the UI works inside the UI itself.

## 6. Regnum Online Major Gameplay Benchmark

### 6.1 Core Goal

Regnum's durable value is not modern graphical quality. Its benchmark value is the long-term goal structure:

```text
choose realm
-> grow character and group identity
-> fight in contested warzone
-> capture forts/castles
-> create invasion opportunity
-> steal/defend high-value realm objectives
-> reinforce realm pride and repeat
```

AnotherLife should use this as the major gameplay north star: a realm-driven conflict where Kingdom management, Champion combat, territory control, bosses, loot, and narrative consequences all feed the feeling that the player's realm is advancing in a living war.

### 6.2 AnotherLife Realm-War Translation

AnotherLife should define original equivalents:

| Regnum reference function | AnotherLife direction |
| --- | --- |
| Realm selection | Player commits to an original AnotherLife realm identity with durable profile consequences. |
| Warzone | Contested territory layer connected to Kingdom 2.5D and Champion encounters. |
| Forts/castles | Original strongholds, gates, sanctums, citadels, and relic sites. |
| Invasion pressure | Multi-step realm offensive requiring territory control, resources, champions, and event windows. |
| Realm gems/relics | Original high-value realm objectives with strict custody, entitlement, and idempotency contracts. |
| Group PvP identity | Future multiplayer/async/social design, not silently assumed in current single-player prototype. |

### 6.3 Design Guardrails

- Current project phase remains NVS-01 and foundation hardening. This benchmark does not authorize broad MMO multiplayer implementation.
- Realm war must be built from validated services, durable saves, and idempotent transactions. Existing issues #166, #169, #171, #173, #180, and #184 are directly relevant to this goal.
- A realm-war feature is not accepted unless repeated/stale events cannot duplicate rewards, corrupt ownership, or lose entitlement.
- Large-scale combat fantasy can be staged first through PvE, async simulation, events, and Champion encounters before any real multiplayer commitment.

## 7. Tiered VFX And Visual Reward Benchmark

The user direction is binding for items, gear, skill effects, and related progression: higher tier or grade must produce stronger visual effects.

| Grade | Visual benchmark | Required restraint |
| --- | --- | --- |
| Common | Clean model/icon, small hit spark or utility glow. | Never hide gameplay state. |
| Uncommon | Subtle color accent, small trail, light idle shimmer. | Keep readable on low settings. |
| Rare | Distinct hue, sharper impact flash, short trail, small particles. | One primary effect language only. |
| Epic | Layered particles, stronger emissive trim, animated icon/skill accent, more pronounced impact. | Cap screen coverage and duration. |
| Legendary | Unique silhouette accent, persistent but controlled aura, impact distortion, environmental response, stronger sound hook later. | Must remain colorblind-readable and cannot obscure telegraphs. |
| Mythic/future | Signature animation, realm-reactive effect, multi-stage cast/impact, trophy-grade presentation. | Requires profiler budget and user approval. |

Every VFX tier must have:

- low/medium/high quality variants;
- particle count and lifetime limits;
- colorblind-safe secondary shape/motion cue;
- combat-readability review;
- screenshot or video capture evidence before promotion to production.

## 8. Benchmark Acceptance Gates

### 8.1 Visual Quality Gates

- Screenshot set: Kingdom 2.5D, Champion arena, boss encounter, character customization, item/gear tier showcase, skill VFX tier showcase.
- Capture set: low, medium, high quality settings from identical camera anchors.
- Readability review: silhouettes, ownership state, action availability, cooldown, danger, interactable buildings, realm identity, tier/grade.
- Accessibility review: contrast, colorblind cues, text size, motion/flash intensity, particle density.
- IP review: no copied names, logos, icons, silhouettes, UI skins, or lore.

### 8.2 Technical Gates

- Unity profiler capture for target scene and device class.
- CPU frame time, GPU frame time, draw calls, batches, SetPass calls, triangles, particle count, overdraw hotspot, texture memory, and shader variant count recorded.
- VFX pooling or lifecycle policy for recurring combat and kingdom effects.
- LOD/culling policy for kingdom buildings, terrain, bosses, champions, terrestrials, and VFX.
- Save and catalog impact declared before any item/gear/skill tier data becomes persistent authority.

### 8.3 Gameplay Gates

- Kingdom 2.5D actions must have typed result states, not silent mutation.
- Realm-war objectives must be duplicate-safe and recoverable after save/load.
- Champion combat results must hand off through the #180 contract, not direct reward mutation.
- Territory, gem, wishgate, warmaster, customization, and economy systems must preserve old/future data and reject invalid state according to their owning specs/issues.

## 9. Implementation Priority

1. Keep this document as the current benchmark and non-copy guardrail.
2. Use the merged customization spec and #180 combat spec to harden character, skill, boss, and encounter data before final art/VFX import.
3. Define a Kingdom 2.5D source/design packet separately before implementing production UI or scene changes.
4. Define a realm-war goal specification that maps Regnum-like durable motivation into original AnotherLife systems without assuming real-time multiplayer.
5. Add a screenshot/profiler benchmark harness only after production scene authority (#223/#150 path) is ready.
6. Promote tiered item/gear/skill VFX only through catalog-backed, profiler-validated, accessible variants.

## 10. Acceptance For This Document

- References are recorded as benchmarks, not copied source.
- AION 2, Lords Mobile, Infinity Kingdom, and Regnum/Champions of Regnum are separated by product layer.
- Kingdom 2.5D and major realm-war goals are explicitly included.
- Tiered item, gear, and skill VFX escalation is captured.
- No runtime, asset, scene, save, catalog, narrative, terrestrial design, or balance change is included.
- No shared-file lock is required.
