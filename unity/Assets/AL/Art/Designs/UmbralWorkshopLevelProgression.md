# Umbral Workshop Level Progression

**Status:** Active production direction

**Date:** 2026-07-27

**Category:** Kingdom building / Workshop

**Stable identity:** `RealmId.Umbral + BuildingId.Workshop`

**Approved visual source:**
[`architecture_umbral_modular_veilwright_detail_v001.png`](../Architecture/ConceptSheets/architecture_umbral_modular_veilwright_detail_v001.png)

**Approved motion source:**
[`architecture_umbral_animation_reference_v001.png`](../Architecture/ConceptSheets/architecture_umbral_animation_reference_v001.png)

**Final model/runtime contract:**
[`Umbral_Workshop_Final_Model_And_Runtime_Binding.md`](../../../../Docs/Architecture/Umbral_Workshop_Final_Model_And_Runtime_Binding.md)

## Purpose

Define the Umbral Workshop Level `0` through Level `10` production ladder
without changing the approved veilwright identity, offset construction grammar,
stable placement slot, or gameplay-authoritative level contract.

The approved veilwright sheet remains the authority for readable graphite
masonry, offset twin roofs, the sheltered oblique entrance, protected central
negative space, low ward chimney, aubergine side shelter, grounded darkglass
table, four physical anchors, and restrained violet focus. This ladder is a
cumulative production interpretation of that source.

## Protected identity

- Offset, inhabitable graphite-stone mass with a fixed entrance on local `-Z`.
- Two unequal steep roof groups frame deliberate negative space rather than a
  symmetric tower.
- The oblique pointed threshold, low offset chimney, grounded sealing table,
  and four physical anchor cues make the workshop function readable.
- Ash joints, smoked iron, tarnished brass, aubergine cloth, and controlled
  midtones keep the structure legible without relying on purple emission.
- No permanent portal, floating assembly, unsupported ring, screen-wide
  darkness, procedural folding, continuous smoke, or full-building glow.

## Level ladder

| Level | Production beat | Cumulative read |
| ---: | --- | --- |
| `0` | Reserved plot | Stable Workshop slot, entrance marker, and no production model |
| `1` | Compact veilwright | Complete small atelier with graphite foundation, offset shell, oblique entrance, split roofs, low chimney, central sealing table, and one sheltered work bay |
| `2` | Bound boundary | Four physical ground locks and pale ash courses establish the fixed ward network |
| `3` | Expanded | West aubergine reliquary shelter adds protected tools and residue handling |
| `4` | Established | Layered pointed threshold and side passage formalize the sheltered entrance |
| `5` | District Anchor | Four grounded anchor pylons and short carved channels converge on the central table |
| `6` | Advanced | Rear work gallery, stronger offset roof rhythm, and a raised but still low ward chimney support heavier work |
| `7` | Signature | East reliquary annex balances capacity without erasing the asymmetrical plan |
| `8` | Masterwork | Restrained roof battens and fixed void braces clarify the split-roof silhouette |
| `9` | Prestige | Service apron, outer ward piers, and practical handling rails complete circulation |
| `10` | Landmark | Bound Eclipse Yoke, four grounded finials, and one contained violet seal make the protected void the final silhouette |

Every level is additive. A later level does not replace the building with a
different prefab, move the root, rotate the entrance, or change its function.

## Level 10 capstone

The Level `10` capstone is the **Bound Eclipse Yoke**:

- two unequal gable forks physically carried by the established roof and wall
  load paths;
- one fixed crosspiece that frames a narrow empty vertical slit;
- four grounded anchor finials that connect the landmark back to the workshop
  boundary;
- the existing offset ward chimney as the restrained confirmation point;
- one contained violet seal at the base of the void.

The empty slit is deliberate architectural negative space, not a permanent
portal. The capstone remains static in the production model. It does not
introduce levitation, unsupported rings, continuous motion, repeated eclipse
events, screen-wide darkness, smoke, or new gameplay authority.

## Runtime authority

```text
confirmed BuildingState.Level
→ stable RealmId.Umbral + BuildingId.Workshop lookup
→ one Umbral production prefab
→ cumulative Level 1…N mesh deltas
```

Level `0` remains the reserved plot. During an upgrade, the model stays at the
confirmed current level; the target delta is not shown until gameplay confirms
the new level. No separate visual stage is persisted.

## Major direction gates

Owner review is required before:

- replacing the Bound Eclipse Yoke with a glowing portal, floating ring,
  vertical magic tower, screen-wide eclipse, or continuously active effect;
- changing the stable entrance, slot identity, footprint family, offset
  twin-roof rhythm, protected void, or four-anchor identity;
- crushing the graphite material family into featureless black or making
  violet emission the primary silhouette;
- adding a separate saved visual level;
- raising materials, colliders, LOD cost, transparency, active light, or
  animation budgets;
- adding final damaged, destroyed, repairing, or selected-state model swaps.
