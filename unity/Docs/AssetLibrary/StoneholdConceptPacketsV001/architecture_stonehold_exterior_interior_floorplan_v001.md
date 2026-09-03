# architecture_stonehold_exterior_interior_floorplan_v001

**Packet ID:** `architecture_stonehold_exterior_interior_floorplan_v001`

**Catalog families:** 21 interior modules

**Owner status:** `PARTIAL`

**Generation / activation:** `HELD`

## 1. Decision identity

**Question:** Treat the shared civic-hall and fort-gatehouse plans/sections as
the only locked exterior-to-interior continuity, and leave castle-keep and other
building floor plans OPEN on `t_c748138b`?

**Already approved (shared, not Stonehold-skinned):**

Civic hall ground: public hall, records, stores, steward office, service stair,
council workroom. Upper: open gallery with void, archives, staff landing, council
chamber, rear archive, upper service. Clearances: interior doors 1.2×2.4 m;
stair 1.5 m wide with 1.5 m landing; furniture aisle ≥1.2 m; ground clear 2.8 m;
upper clear 2.7 m.

Fort gatehouse: side guard/inspection wings, stairs, barracks, wallwalk access,
upper control gallery. Left/right stairs align with upper landings. Central gate
slot impassable. Aperture count 14.

Door policy: shells contain open apertures only. Door leaves/frames/hinges/
colliders/interaction are a separate family.

**Still undecided:**

- Stonehold cladding of these interiors.
- Castle-keep, prison/dungeon, throne, mine-cave, religious room plans.
- Combat/camera clearance numbers beyond the civic aisle/door figures.
- Accessibility ramps/lifts (taxonomy has lift interactable; civic hall uses stairs).

## 2. Required brief

| Field | Value |
| --- | --- |
| Purpose | Bind exterior massing to furnished interiors; no solid-shell substitute |
| Scale | Civic envelope and clearances above. Other buildings OPEN |
| Required views | Floor plans every level, longitudinal and cross sections — present for civic hall and fort gatehouse only |
| Construction | 0.5 m grid modules; room purpose + adjacency documented in layout JSON |
| Streaming | Later; portals/streaming bounds are technical helpers, not designed here |
| Exclusions | Inventing keep plans; making walls enterable; copying franchise interiors |

## 3. Interior family mapping

Named rooms in taxonomy (`waf_interior_*`) stay `packet_authored_owner_decision_required`
except where the civic/fort layouts already name an equivalent zone (great/council
hall, barracks room, stair landing, storage, utility, entry vestibule, door/window
threshold, shell wall/floor/ceiling, courtyard/balcony as gallery).

Those equivalences are **zoning hints**, not extra visual approval.

Still OPEN with no plan: bedroom/living, kitchen/dining, library/archive beyond
civic records, market/shop, mine/cave room, prison/dungeon, religious, throne,
guild hall, forge/workshop interior as world-space (kingdom Workshop is separate),
cutaway occlusion set.

## 4. Owner ruling

Recommended: APPROVE civic/fort continuity as the only locked floor-plan
authority; REVISE if the owner wants Stonehold-specific interior finish sheets
before `t_c748138b` resumes. Meshy unauthorized.
