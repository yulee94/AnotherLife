# AnotherLife Main Quest Line Narrative Completion Report

## Control

- Tracked issue: `#274`
- Primary Codex mode: narrative/content
- Packet version: `anotherlife-main-quest-line-2026-07-23-v001`
- Canonical manifest: `unity/Docs/Narrative/MainQuestLine/ANOTHERLIFE_MAIN_QUEST_LINE.packet.json`
- Starting `main`: `529312b82f1004e56ab16a97b04a3cde79e39dde`

## Completion statement

The canonical narrative source is complete from the existing `OMEN_1` prologue through the eight-gem final wish. The manifest binds 15 immutable chapter components by path, chapter identity, order, and SHA-256. Together they define:

- 15 ordered chapters and 15 critical-path quests;
- 30 optional supporting side quests, two per chapter;
- 153 objectives and 410 localization-facing authorities;
- seven four-realm variant quest families preserving 29 legacy chapter references;
- eight named realm gems with temporary custody and mandatory return rules;
- Edras Veyr, the Hollow Regent, as the central antagonist;
- Vaeloryn, Accordant Isle, Dragon's Concordance, the final wish, and persistent postgame invariants.

This is narrative-source completion. It does not claim that the complete line is already wired into Unity, persisted, balanced, packaged, or accepted through an end-to-end user playtest.

## Story thesis

> Unity has meaning only when distinct peoples choose it freely.

Edras Veyr, first commander of the Veil Watch, concluded that choice made peace impossible. As the Hollow Regent, he manufactures border evidence, corrupts sacred symbols, uses celestial routes, and pursues all eight gems to force one realm, one voice, and one will. The player's victory proves that cooperation can remain voluntary without erasing rivalry, language, culture, or sovereignty.

## Campaign spine

1. `CH00_FIRST_SIGNAL` / `OMEN_1` — The First Signal.
2. `CH01_PROOF_OF_WORTH` / `MQ_C1_PROOF_OF_WORTH` — realm-specific proof and guardian covenant.
3. `CH02_BORDER_OATHS` / `MQ_C2_BORDER_OATHS` — ring conspiracy and manufactured border conflict.
4. `CH03_FIRST_RESONANCE` / `MQ_C3_FIRST_RESONANCE` — first realm gem and eightfold foreshadowing.
5. `CH04_KINGDOM_UNDER_OATH` / `MQ_C4_KINGDOM_UNDER_OATH` — 2.5D kingdom growth, research, troops, and economy.
6. `CH05_ROADS_TO_THE_GATE` / `MQ_C5_ROADS_TO_THE_GATE` — return to 3D exploration and main-gate approach.
7. `CH06_COMPANY_WE_KEEP` / `MQ_C6_COMPANY_WE_KEEP` — party hunting and fair support contribution.
8. `CH07_ANCIENT_LEGACY` / `MQ_C7_ANCIENT_LEGACY` — founder relic and restored cross-realm oath.
9. `CH08_GATE_UNSEALED` / `MQ_C8_GATE_UNSEALED` — outer-warzone entry and one active save pillar.
10. `CH09_BRIDGES_OF_RIVALRY` / `MQ_C9_BRIDGES_OF_RIVALRY` — bridge, gate, and crossroads RvR objectives.
11. `CH10_CELESTIAL_RIFT` / `MQ_C10_CELESTIAL_RIFT` — second realm gem and celestial route.
12. `CH11_HIGH_SKY_TRIALS` / `MQ_C11_HIGH_SKY_TRIALS` — Warmaster purpose and Edras reveal.
13. `CH12_EIGHT_LIGHTS` / `MQ_C12_EIGHT_LIGHTS` — level 50, Warzone points, Warmaster gear, True Warmaster, and all eight gems.
14. `CH13_ACCORDANT_ISLE` / `MQ_C13_ACCORDANT_ISLE` — neutral island, cross-realm trade, shared language, and Vaeloryn.
15. `CH14_FINAL_WISH` / `MQ_C14_FINAL_WISH` — Hollow Regent defeat, voluntary wish, gem return, and postgame.

## Side-quest contract

Every chapter contains two optional side quests. Each explains a system, reveals history, humanizes a rival, foreshadows a later reveal, resolves a local consequence, or returns an emotional payoff to the main line. They are not filler and never replace a required main objective.

Optional feedback is limited to additional dialogue, witnesses, epilogue cards, cosmetics, route hints, or training convenience. Side quests cannot grant or gate critical-path access, required levels, realm access, gem custody, Warmaster or True Warmaster eligibility, final-wish access, or the canonical ending.

Examples include:

- `SQ_OMEN_UNBURNED_PAGES`, which foreshadows Edras Veyr;
- `SQ_C3_KEEPERS_FIRST_LIGHT`, which teaches temporary gem custody;
- `SQ_C6_NO_VICTORY_ALONE`, which explains healer and buffer contribution;
- `SQ_C8_PILLARS_REMEMBER`, which makes the one-active-pillar rule meaningful;
- `SQ_C12_CUSTODIANS_NOT_OWNERS`, which explains eight-gem assembly without conquest;
- `SQ_C13_WORDS_WITHOUT_SURRENDER`, which demonstrates the story thesis;
- `SQ_C14_THOSE_WHO_CAME`, which returns optional allies for finale payoff.

## Final-act identities and invariants

- Wish Dragon: `NPC_VAELORYN` — **Vaeloryn, Keeper of Unmade Wishes**.
- Center island: `LOCATION_ACCORDANT_ISLE` — **Accordant Isle**, the only neutral non-PvP outer-warzone zone.
- Shared language: `EFFECT_DRAGONS_CONCORDANCE` — **Dragon's Concordance**, local and consensual.
- Antagonist: `NPC_EDRAS_VEYR` — **Edras Veyr, the Hollow Regent**.

The three wish emphases—Bridges, Vigil, and Renewal—change epilogue emphasis and cosmetic remembrance only. Every ending preserves distinct realms, returns all eight gems, keeps Accordant Isle neutral, keeps Dragon's Concordance local and consensual, leaves the warzone playable, and defeats the Hollow Regent.

## Validation

Run:

```text
python tools/narrative/test_main_quest_line_packet.py
```

Result:

```text
Main quest packet accepted: components=15, chapters=15, mainQuests=15, sideQuests=30, objectives=153, realmGems=8, localizationAuthorities=410, negativeFixtures=13
```

The validator verifies component hashes, packet/index parity, contiguous order, the critical chain, unique IDs, localization authority, two optional side quests per chapter, non-gating feedback, all product milestones, two gems per realm, realm variants, `OMEN_1` authority, final-act identities, and ending invariants. Thirteen deliberately broken fixtures are rejected.

## Boundaries and handoff

Changed files are limited to one manifest, 15 chapter components, one focused validator, and this report. No Unity or Android runtime code, scene, save schema, catalog, shared contract, asset, balance value, workflow, dependency, build setting, or designated shared file changed. Runtime performance, memory, package size, install size, and device compatibility are unchanged.

Codex coordination/review should next split implementation into dependency-ordered specifications. Engineering must consume this source rather than re-authoring story text in services. Every runtime slice still requires current catalog, save, progression, battle, scene, notification, packaging, fidelity-review, and user-playtest evidence.
