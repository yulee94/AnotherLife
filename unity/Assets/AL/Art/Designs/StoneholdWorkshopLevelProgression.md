# Stonehold Workshop Level Progression

**Status:** Active production direction

**Date:** 2026-07-27

**Category:** Kingdom building / Workshop

**Stable identity:** `RealmId.Stonehold + BuildingId.Workshop`

**Approved visual source:**
[`architecture_stonehold_modular_workshop_detail_v001.png`](../Architecture/ConceptSheets/architecture_stonehold_modular_workshop_detail_v001.png)

**Approved motion source:**
[`architecture_stonehold_animation_reference_v001.png`](../Architecture/ConceptSheets/architecture_stonehold_animation_reference_v001.png)

**Final model/runtime contract:**
[`Stonehold_Workshop_Final_Model_And_Runtime_Binding.md`](../../../../Docs/Architecture/Stonehold_Workshop_Final_Model_And_Runtime_Binding.md)

## Purpose

Define the Stonehold Workshop Level `0` through Level `10` production ladder
without changing the approved workshop identity, rigid construction motion
grammar, stable placement slot, or gameplay-authoritative level contract.

The existing approved workshop sheet remains the visual authority for
Stonehold mass, roof construction, entrance, chimney, material hierarchy, and
forge focal point. This progression is a cumulative production interpretation
of that source, not a replacement concept.

## Protected identity

- Broad, low masonry mass with a fixed entrance on local `-Z`.
- Paired heavy roof plates, iron pressure bands, and a strong ridge.
- One off-center working chimney and one contained forge-amber focal point.
- Rigid load paths: foundation, masonry, buttresses, annexes, locks, and roof
  plates remain physically supported.
- No levitation, self-growing masonry, elastic stone, or continuous structural
  idle motion.

## Level ladder

| Level | Production beat | Cumulative read |
| ---: | --- | --- |
| `0` | Reserved plot | Stable Workshop slot, entrance marker, and no production model |
| `1` | Foundational forge | Complete compact masonry shell, paired plate roof, entrance, chimney, forge, bench, and anvil |
| `2` | Reinforced | Paired buttresses, pressure bands, and side locks make the load path explicit |
| `3` | Expanded | East service annex adds practical covered production area |
| `4` | Established | Outer iron-and-stone portal strengthens the public Workshop entrance |
| `5` | District Anchor | Front work apron and grounded material hoist address the service road |
| `6` | Advanced | Taller vent stack and rear service platform support heavier output |
| `7` | Signature | West storage bay balances the silhouette and secures raw material |
| `8` | Masterwork | Roof crown spine and cross-locks unify the expanded structure |
| `9` | Prestige | Loading quay, ore rack, and canopy complete Workshop logistics |
| `10` | Landmark | Anvil-crown chimney, contained ember slit, and paired landmark pressure locks create the final Stonehold silhouette |

Every level is additive. A later level does not replace the building with a
different prefab, move the root, rotate the entrance, or alter its function.

## Level 10 capstone

The Level `10` capstone is the **anvil-crown forge chimney**:

- a three-tier masonry and dark-iron crown carried by the existing chimney;
- paired cap plates forming a restrained anvil-like silhouette;
- one narrow ember slit contained within the crown;
- paired rear pressure-lock pylons that visually resolve the expanded mass.

The capstone is a structural prestige cue, not a larger magical event. It does
not emit across the building, move continuously, float, or imply a gameplay
power that the Workshop does not own.

## Runtime authority

```text
confirmed BuildingState.Level
→ stable RealmId.Stonehold + BuildingId.Workshop lookup
→ one Stonehold production prefab
→ cumulative Level 1…N mesh deltas
```

Level `0` remains the reserved plot. While an upgrade is in progress, the
model stays at the confirmed current level; the target delta is not shown until
gameplay confirms the new level. No separate visual stage is persisted.

## Major direction gates

Owner review is required before:

- replacing the anvil-crown chimney with a magical, floating, or animated
  capstone;
- changing the stable entrance, slot identity, footprint family, or broad roof
  mass;
- adding a separate saved visual level;
- raising materials, colliders, LOD cost, transparency, or active animation
  budgets;
- adding final damaged, destroyed, repairing, or selected-state model swaps.
