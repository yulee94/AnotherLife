# Relationship Authority Source Handoff

**Packet ID:** `al_narrative_relationship_authority_source_v001`
**Primary Codex mode:** narrative/content
**Runtime content catalog:** `unity/Assets/AL/StreamingAssets/GameData/al_relationship_authority_content_catalog.json`
**Related issue:** #176

## Source Intent

This packet provides A3 narrative/content authority for relationship identity, player-facing classification labels, NPC/faction names, persona trait meanings, and localization references. It gives #176 engineering a source catalog to consume when moving hard-coded rank, affiliation, and persona labels out of technical services.

The packet is deliberately non-authoritative for mutation. It does not implement affinity, faction, or persona saves; it does not repair old data; it does not add idempotency ledgers; it does not apply the NVS-01 report transaction; and it does not change approved numeric thresholds or amounts.

## Stable Source Rules

- Supported canonical IDs are lowercase snake-case.
- Legacy Android and NVS IDs are exact aliases only. Runtime must not guess aliases from display names, case folding, trimming, or fuzzy matching.
- Sparse supported NPC affinity and faction reputation default to `0`.
- Nonzero Android simulation values are retained only as legacy preview hints, not save initialization authority.
- The only approved relationship consequence in this packet is `GRANT_VALERIUS_AFFINITY_5` targeting `npc_valerius`; it remains future transaction work.
- `Vaeloryn` and `Edras Veyr` may be referenced by relationship-aware systems, but ordinary affinity mutation is disabled for them in this packet.
- Persona all-zero and tie states must be presented honestly; engineering must not invent `Sage`, `Warlord`, or any other unique dominant trait when source state does not support it.

## Handoff Rules

Engineering should validate:

- unique canonical IDs and aliases;
- all localization keys resolve;
- affinity bands preserve the existing `[-100, 100]` threshold profile exactly;
- faction bands preserve the existing signed `int` threshold profile exactly;
- persona traits match the current `Warlord`, `Diplomat`, `Sage`, and `Rogue` enum names;
- all-zero and tie persona policies return honest typed states;
- unknown stable IDs are preserved but excluded from supported mutation;
- hard-coded English service labels are treated as legacy wrappers after this source is consumed.

## Acceptance Status

Source status: ready for Codex coordination/review and later #176 engineering consumption.
User gate: final relationship UX, balance, integrated NVS report behavior, and playtest approval remain later gates.
