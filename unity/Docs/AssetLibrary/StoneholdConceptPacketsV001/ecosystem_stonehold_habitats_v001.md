# ecosystem_stonehold_habitats_v001

**Packet ID:** `ecosystem_stonehold_habitats_v001`  
**Catalog families:** 9 ecosystem classes + 4 Stonehold habitat roster IDs  
**Owner status:** `PENDING`  
**Generation / activation:** `HELD`

Fantasy beasts, never animals.

## 1. Decision identity

**Question:** Keep habitat production and all ecosystem creature families blocked
except the already-approved Slagfall Quarry environment kit, which does **not**
approve fauna, bosses, elites, or dragons?

**Already approved:**

- Slagfall Quarry eight-family environment kit (geology/terrain), 2026-08-31.
- Slagfall habitat master sheet path:
  `unity/Docs/Terrestrials/Ecosystems/SlagfallQuarryV002/ConceptSheets/tdf_habitat_stonehold_slagfall_quarry_master_v002.png`
- Taxonomy: `tdf_habitat_*` records are source-review habitat identities, not
  approved biome IDs or production terrain.
- Ecosystem-specific fantasy beasts/monsters are later-scope.

**RosterProposed habitats (not production-approved):**

| Habitat ID | Working label | Anchor (review-only) |
| --- | --- | --- |
| `tdf_habitat_stonehold_slagfall_quarry` | Slagfall Quarry | slaghide gorer / slagwhistle |
| `tdf_habitat_stonehold_ore_gallery_mouths` | Ore Gallery Mouths | oreblind delver |
| `tdf_habitat_stonehold_rimecut_pass` | Rimecut Pass | rimehorn breaker |
| `tdf_habitat_stonehold_faultroad_escarpment` | Faultroad Escarpment | fault-crowned colossus |

Material family `tdf_matfam_stonehold_slate_iron` is a roster label, not a locked
trim sheet.

**Still undecided:**

- 2D lock for Faultroad, Rimecut, Ore Gallery.
- Whether habitat sheets must exist before any further geology beyond Slagfall.
- All `waf_ecosystem_*` production (dragons deferred_unapproved; aquatic/pelagic
  deferred_unapproved; bosses/elites review-only).
- ComfyUI Local versus Cloud.

## 2. Required brief

| Field | Value |
| --- | --- |
| Purpose | Habitat identity for later fauna after worlds exist |
| Scale | OPEN |
| Silhouette | Habitat protected identities from the roster only (fault-road grade, pass notch, gallery mouths, quarry) |
| Required views | Slagfall master exists. Other habitat masters OPEN |
| Exclusions | Producing monsters from this environment lane; calling fauna animals; copying franchise creatures |

## 3. Sequencing

World-asset order: habitats after assembled worlds; beasts after habitat
approval. This packet maps families and stops.

## 4. Owner ruling

Recommended: keep `PENDING` for habitats other than Slagfall environment kit
reuse. No Meshy creatures from this card.
