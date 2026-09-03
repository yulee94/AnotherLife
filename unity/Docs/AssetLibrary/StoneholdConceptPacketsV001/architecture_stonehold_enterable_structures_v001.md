# architecture_stonehold_enterable_structures_v001

**Packet ID:** `architecture_stonehold_enterable_structures_v001`

**Catalog families:** 34

**Owner status:** `PARTIAL`

**Generation / activation:** `HELD`

**Downstream 2D remainder:** `t_c748138b` (do not duplicate here)

## 1. Decision identity

**Question:** Confirm the split: shared civic-hall and fort-gatehouse 2D spatial
authority stands; Stonehold-specific exteriors, castle-keep interiors, remaining
service buildings, and door/glass/shutter families stay unapproved and routed to
`t_c748138b`.

**Already approved:**

- Civic hall 2D (2026-09-01): shared two-floor layout 9.5×8.5×6.8 m, 0.5 m snap, apertures only (no door meshes), reusable shell/roof modules, layered finish tiles. Eldergrove/Crownlands exteriors are massing/material references only. Stonehold exterior is outside that package.
- Fort gatehouse 2D (2026-09-01): enterable side wings and upper gallery; central gate slot physically impassable; stair alignment validated; 14 apertures.
- Kingdom Stonehold Workshop production binding and modular workshop detail sheet — not world-space environment approval.
- Wall/gate rules: main castle/fort/city perimeter walls are impassable; each gate/door is a separate object; intact main gates teleport later; hostile break is `t_c8ea885d`, not this packet.
- Slagfall Quarry is **not** architecture-visual authorization.

**Still undecided (do not guess):**

- Stonehold civic-hall exterior massing/material sheet.
- Realm-specific fort/castle exteriors.
- Enterable castle-keep plans/interiors.
- Remaining civic/service buildings (academy, farm, forge, market, inn, shop, mill, warehouse, quarry building, gold mine, lumber mill, embassy, religious — deferred_unapproved).
- Separate door, glass, shutter families.
- ComfyUI Local versus Cloud.

## 2. Required brief

| Field | Value |
| --- | --- |
| Purpose | Enterable, traversable structures with planned interiors |
| Scale | Civic hall 9.5×8.5×6.8 m; wall thickness 0.3 m; floor-to-floor 3.2 m; public entrance 2.5×3.0 m. Workshop L10 9.18×6.64×6.48 m (kingdom). Castle/fort envelopes OPEN |
| Construction | Reusable panels on 0.5 m grid; 1.5–3 m realm cladding; 0.5–1 m interior tiles; combine static opaque geometry per room/cell at runtime |
| Materials | Stonehold: basalt, soot-aged iron, dark timber, bronze repairs, localized forge amber. Exact cladding OPEN |
| Magic / VFX | One functional location later; none baked into shell |
| Required views | Civic/fort plans, sections, modules exist (shared). Stonehold front/side/back exteriors absent |
| Animation | Stonehold motion contract for kingdom construction; world-space idle remains still mass + removable smoke/workers |
| Exclusions | Solid decorative blocks; enterable main walls; fused gate-in-wall mesh; dwarf pastiche; copied BDO/IK castles |

## 3. Gate / wall policy (binding, not implemented here)

- Perimeter walls: non-enterable, impassable.
- Gate/door: separate cataloged object.
- Intact main gate: later atomic teleport between paired anchors.
- Hostile break: reference `t_c8ea885d` only.
- Building modules must never open a physical route through an intact gate.

## 4. Family split

**Shared 2D spatial (not Stonehold exterior):**
`waf_architecture_building_town_hall`, fortress/castle/guardpost/watchtower/wall,
`waf_traversal_wall_fortification`, gate families, `waf_interactable_door_hatch`,
`waf_interactable_gate_teleport_control`.

**Kingdom workshop inheritance:** `waf_architecture_building_workshop`.

**PENDING, no invented look:** academy, barracks, embassy, farm, forge, gold mine,
lumber mill, market, quarry, stable, storehouse, inn, mill, shop, warehouse,
religious (deferred_unapproved), stairs/ramps/ladders/platforms/teleport pads.

## 5. Mobile / provenance

Preserve silhouette at LOD; drop workers, scaffolds, smoke first. Combine tiles
per cell. Provenance: PR #664 manifests + FourRealmArchitecture + DESIGN.md.
No new 3D jobs.

## 6. Owner ruling

Recommended: APPROVE the routing/split; keep Stonehold exteriors PENDING on
`t_c748138b`. Meshy unauthorized.
