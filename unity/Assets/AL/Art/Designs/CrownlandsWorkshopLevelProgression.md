# Crownlands Workshop Level Progression

**Status:** Active production direction

**Date:** 2026-07-27

**Category:** Kingdom building / Workshop

**Stable identity:** `RealmId.Crownlands + BuildingId.Workshop`

**Approved visual source:**
[`architecture_crownlands_modular_stormwright_detail_v001.png`](../Architecture/ConceptSheets/architecture_crownlands_modular_stormwright_detail_v001.png)

**Approved motion source:**
[`architecture_crownlands_animation_reference_v001.png`](../Architecture/ConceptSheets/architecture_crownlands_animation_reference_v001.png)

**Final model/runtime contract:**
[`Crownlands_Workshop_Final_Model_And_Runtime_Binding.md`](../../../../Docs/Architecture/Crownlands_Workshop_Final_Model_And_Runtime_Binding.md)

## Purpose

Define the Crownlands Workshop Level `0` through Level `10` production ladder
without changing the approved stormwright identity, synchronized construction
grammar, stable placement slot, or gameplay-authoritative level contract.

The approved stormwright sheet remains the authority for Crownlands civic
symmetry, paired piers, broad silver arch, stepped blue roof, raised conductor
lantern, central calibration engine, and restrained indigo focus. This ladder
is a cumulative production interpretation of that source.

## Protected identity

- Upright, balanced pale-stone mass with a fixed entrance on local `-Z`.
- Paired civic piers, one broad silver entrance arch, stepped blue roof wings,
  and a raised central lantern.
- Grounded conductor routes connect practical instruments to one localized
  indigo calibration focus.
- Authority comes from order, proportion, and engineered refinement rather
  than excessive height, gold, emission, or ornament.
- No random lightning, floating assembly, unsupported rings, perpetual
  mechanisms, or continuous full-building glow.

## Level ladder

| Level | Production beat | Cumulative read |
| ---: | --- | --- |
| `0` | Reserved plot | Stable Workshop slot, entrance marker, and no production model |
| `1` | Civic stormwright | Complete compact masonry shell, paired piers, broad arch, stepped roof, lantern, central calibration plinth, and practical work bay |
| `2` | Balanced | Paired side braces and conductor ground-locks strengthen the symmetric load path |
| `3` | Expanded | East instrument annex adds enclosed calibration and storage capacity |
| `4` | Established | Layered silver entrance frame and civic threshold formalize the public work bay |
| `5` | District Anchor | Front calibration court and paired grounded service rails address the road |
| `6` | Advanced | Taller lantern drum, rear instrument gallery, and expanded conductor spine support heavier work |
| `7` | Signature | West charting annex balances the plan and completes the paired workshop program |
| `8` | Masterwork | Roof meridian bands and four fixed lantern braces unify the expanded silhouette |
| `9` | Prestige | Rear service arcade, contained instrument pylons, and civic steps complete circulation |
| `10` | Landmark | Meridian Crown Lantern, paired conductor finials, and one contained indigo aperture create the final Crownlands silhouette |

Every level is additive. A later level does not replace the building with a
different prefab, move the root, rotate the entrance, or change its function.

## Level 10 capstone

The Level `10` capstone is the **Meridian Crown Lantern**:

- a taller central lantern drum physically carried by the established roof and
  four fixed silver meridian ribs;
- paired grounded conductor finials that balance the rear silhouette;
- one contained indigo calibration aperture centered within the crown;
- restrained silver and brass hierarchy that remains subordinate to the civic
  masonry mass.

The capstone is a structural prestige cue and a readable calibration landmark.
It remains static in the production model. It does not introduce procedural
lightning, levitation, continuous rotation, repeated flashing, or new gameplay
authority.

## Runtime authority

```text
confirmed BuildingState.Level
→ stable RealmId.Crownlands + BuildingId.Workshop lookup
→ one Crownlands production prefab
→ cumulative Level 1…N mesh deltas
```

Level `0` remains the reserved plot. During an upgrade, the model stays at the
confirmed current level; the target delta is not shown until gameplay confirms
the new level. No separate visual stage is persisted.

## Major direction gates

Owner review is required before:

- replacing the Meridian Crown Lantern with random lightning, a floating
  armature, continuous mechanisms, or a broader magical event;
- changing the stable entrance, slot identity, footprint family, paired-pier
  symmetry, or broad silver-arch identity;
- adding a separate saved visual level;
- raising materials, colliders, LOD cost, transparency, active light, or
  animation budgets;
- adding final damaged, destroyed, repairing, or selected-state model swaps.
