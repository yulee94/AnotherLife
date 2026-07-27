# Four-Realm Ecosystem Source Packet

## Status

- Issue: `#259`
- Primary Codex mode: `terrestrial-design`
- Source version: `tdf-eco-2026-07-27-v001`
- Packet state: `RosterProposed`
- User creative approval: `NotRequested`
- Runtime integration: `Blocked`
- Narrative naming/localization: `WorkingLabelsOnly`

This folder contains the A2 terrestrial-design proposal for coherent fauna and habitat identity across Stonehold, Eldergrove, Crownlands, and Umbral. It is source-design documentation and catalog metadata only. It does not authorize runtime zones, spawn tables, combat, AI, rewards, quests, save data, scenes, prefabs, shaders, Addressables, or bundle implementation.

## Packet Contents

- `Four_Realm_Ecosystem_And_Habitat_Source.md` — category decisions, connected habitat loops, habitat identity, and sixteen supporting-fauna briefs.
- `Creature_Diversity_And_Terrestrial_Optimization_Source.md` — user creature/environment reference consolidation, production-safe family lanes, sight-range intent, and next visual-source selection rules.
- `Ecosystem_Source_Budgets_And_Asset_Layout.md` — mobile-to-PC source envelopes, memory targets, deduplication rules, and proposed source/package layout.
- `Ecosystem_Creative_Review_Matrix.md` — exact readiness and user-review state for habitats, supporting fauna, and inherited boss/elite anchors.
- `ecosystem_habitat_profiles_manifest.json` — stable design IDs, references, state, asset-family intent, and numeric source budgets.
- `ecosystem_habitat_source_packet.schema.json` — retained structural schema for the manifest.
- `AvianSoarers/` — companion source `tdf-eco-soarer-2026-07-27-v001`
  with exact visual, provenance, QA, schema, and manifest evidence for three
  shared-rig supporting fauna.
- `MoonrootFloodbasin/` — companion source
  `tdf-eco-moonroot-2026-07-27-v001` with exact habitat, environment-kit,
  flood-season Moonshell Cicada, provenance, QA, schema, and manifest evidence.

The parent roster adds no concept art or runtime asset. Its companion packets
advance one habitat, one pictured Moonshell ecotype, and three avian-soarer
identities to `ReadyForUserReview`. The Moonshell dry-season ecotype and nine
other new fauna families remain `ProposedTextOnly`. Foundation proposals remain
unapproved.

## Upstream Sources Consumed

- Canonical realm IDs and realm identity: `unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json`
- Existing boss and elite visual source: `unity/Docs/Terrestrials/RealmBossesAndElites/`
- Existing foundation proposals: `unity/Docs/Terrestrials/Terrestrial_Design_Brief.md`
- Approved architecture boundary: `unity/Assets/AL/Art/Designs/FourRealmArchitecture.md`
- Product and optimization direction: `unity/Docs/Product_Direction.md`

Habitat suitability links to existing creatures are visual/ecological design references only. They are not spawn, encounter, or gameplay authority.

## Counts

- Canonical realms: `4`
- Habitat modules: `16` (`4` per realm)
- Connected realm habitat loops: `4`
- Supporting fauna families: `16` (`4` per realm)
- Reused foundation family IDs: `3`
- New exact visual-review family IDs: `3`
- New exact habitat visual-review IDs: `1`
- New exact flood-season fauna visual-review IDs: `1`
- Remaining new text-only family IDs: `9`
- Avian-soarer finals/retained inputs: `8 / 3`
- Moonroot finals/retained inputs: `6 / 3`
- Avian-soarer Player/install bytes: `0`
- Moonroot Player/install bytes: `0`
- Existing boss/elite anchors referenced: `16`
- Runtime or shared-file changes: `0`

## Required Handoff Sequence

```text
A2 roster and habitat proposal
→ exact visual source for selected habitats/families
→ A2 fidelity and source-package validation
→ user approval of exact source version and IDs
→ Codex coordination/review technical specification
→ Codex engineering integration
→ coordination plus A2 fidelity disposition
→ user integrated approval
```

Merging this proposal does not skip any later gate.
