# traversal_stonehold_roads_bridges_v001

**Packet ID:** `traversal_stonehold_roads_bridges_v001`

**Catalog families:** 9

**Owner status:** `APPROVE` (planning and concept direction, 2026-09-03)

**Generation / activation:** `HELD`

## 1. Decision identity

**Question:** Accept the taxonomy metric widths as engineering constraints and
leave Stonehold road/bridge visual language `OPEN` until a dedicated 2D sheet
exists?

**Owner answer (2026-09-03):** APPROVE this packet as planning/concept direction.
Metrics may be used by later engineering. Visual language stays without invented
sheets. ComfyUI Local chosen. No Meshy.

**Already approved / reserved (metrics, not look):**

- Primary road 6 m; service road 4 m; modular bridges in 4 m spans (taxonomy §7).
- Adjacent realm bridges = 180 m / 30 s travel (world-map memory); those lengths are topology, not this packet’s mesh look.
- Stonehold material grammar from DESIGN.md.

**Still undecided:**

- Paving vs packed iron-soil vs cut-stone road finish.
- Bridge parapet/support language (masonry vs iron vs mixed).
- Rope-suspension use in Stonehold (taxonomy family exists; realm fit OPEN).
- Natural root/log crossings — likely rare in Stonehold; do not import Eldergrove defaults.
- New ComfyUI Local road/bridge sheets (none generated in this lane).

## 2. Required brief

| Field | Value |
| --- | --- |
| Families | `waf_traversal_road_primary`, `road_service`, `trail_path`, `road_edge_curb`, `bridge_modular`, `bridge_natural`, `bridge_rope_suspension`, `ford_culvert`, `fence_barrier` |
| Purpose | Principal travel, local service, foot trails, crossings |
| Scale | 6 m / 4 m / 4 m-span as above; curb/edge OPEN; fence height OPEN |
| Camera use | Gameplay traversal; auto-quest follows roads/gates later |
| Primary silhouette | OPEN. Do not default every Stonehold bridge to a slab clone of Slagfall fault slabs |
| Materials | Directional basalt + iron; exact paving OPEN |
| Required views | Front/side/back modular bay sheets — absent |
| Exclusions | Floating unsupported decks; decorative spike rails; copied franchise bridges; using quarry rafts as road mesh |

## 3. Variants / gameplay / mobile

Straight, bend, intersection, grade, damaged, realm-to-shared transition for
primary roads. Intact/damaged spans for bridges. Ford/culvert flooded/closed
states are gameplay-owned.

Mobile: merge static road bays per visibility cell; no per-tile renderer;
protect route-edge non-color cues.

## 4. Provenance

Human compilation from taxonomy + DESIGN.md. No new images. Slagfall is not
road authority.

## 5. Owner ruling

**Recorded 2026-09-03:** `APPROVE` as planning and concept direction. Metrics may
be used by later engineering; visuals stay without invented sheets. Meshy
unauthorized.
