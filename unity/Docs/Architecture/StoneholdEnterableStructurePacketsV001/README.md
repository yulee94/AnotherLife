# Stonehold Enterable Structure 2D Handoff Packets v001

Status: **PENDING OWNER REVIEW**. Return `APPROVE`, `REVISE`, or `REJECT` for each of the 27 packets before any Meshy, Blender, Unity-prefab, or other 3D production begins.

## Scope and evidence

- Enterable structure packets: **27 / 27**.
- Taxonomy accounting: **40 / 40** across the two approved Stonehold architecture concept packets.
- Shared door/gate/traversal families: **11 / 11**.
- Interior room-module families: **21 / 21**.
- Prop/decor families accounted: **65 / 65**; the event-only banner is explicitly excluded from permanent Stonehold furnishing.
- Artifact records verified: **97**.
- New 3D jobs submitted: **0**.

Open `review/README.md` for the complete GitHub-rendered review register, `index.html` for the local dashboard, the five review-index PNGs for compact contact sheets, or the nine full-pair QA PNGs for every exterior/interior source pair. Every structure folder contains one exterior sheet, one every-floor/section sheet, and one machine-readable packet.

The merged owner-approved PR #701 Stonehold civic/fort furnishing packet supplies exact dimensions and protected-opening fit for its 16 prop families, plus the shared Stonehold material, atlas, LOD, collider and wear precedent. This packet set binds that authority explicitly after reconciling the latest `origin/main`.

## Binding decisions already fixed

1. Every applicable Stonehold building/structure is genuinely enterable and end-to-end traversable with developed furnished rooms. No facade-only or solid-shell substitute is allowed.
2. Main castle/fortress/fort/city perimeter walls are non-enterable and impassable. Defendable walltops and designated routes are separate traversal surfaces.
3. Every gate, door, window frame, glass group and shutter is a separate object. An intact main gate never becomes a physical opening through the wall.
4. Intact main-gate paired-anchor teleport is referenced only. Hostile break is referenced only under `t_c8ea885d`.
5. Small interiors are seamless. Large combat interiors use separate asynchronously loaded scenes behind physical portals with loading-cover, occlusion, NavMesh and unload boundaries.
6. Runtime VFX stay separate from clean architecture geometry.
7. Author on the approved `0.5 m` sub-grid; combine static pieces by room/exterior visibility cell and shared atlas for runtime.

## Review register

| # | Taxonomy family | Exterior packet | Physical levels | Owner decision |
| ---: | --- | --- | ---: | --- |
| 01 | `waf_architecture_city_capital_kit` | [city_capital_kit](packets/city_capital_kit/city_capital_kit_exterior_handoff_v001.png) | 3 | PENDING |
| 02 | `waf_architecture_settlement_village_kit` | [settlement_village_kit](packets/settlement_village_kit/settlement_village_kit_exterior_handoff_v001.png) | 2 | PENDING |
| 03 | `waf_architecture_dwelling` | [dwelling](packets/dwelling/dwelling_exterior_handoff_v001.png) | 2 | PENDING |
| 04 | `waf_architecture_ruin_structure` | [ruin_structure](packets/ruin_structure/ruin_structure_exterior_handoff_v001.png) | 2 | PENDING |
| 05 | `waf_architecture_well_fountain_cistern` | [well_fountain_cistern](packets/well_fountain_cistern/well_fountain_cistern_exterior_handoff_v001.png) | 2 | PENDING |
| 06 | `waf_architecture_building_academy` | [academy](packets/academy/academy_exterior_handoff_v001.png) | 2 | PENDING |
| 07 | `waf_architecture_building_barracks` | [barracks](packets/barracks/barracks_exterior_handoff_v001.png) | 2 | PENDING |
| 08 | `waf_architecture_building_embassy` | [embassy](packets/embassy/embassy_exterior_handoff_v001.png) | 2 | PENDING |
| 09 | `waf_architecture_building_farm` | [farm](packets/farm/farm_exterior_handoff_v001.png) | 2 | PENDING |
| 10 | `waf_architecture_building_forge` | [forge](packets/forge/forge_exterior_handoff_v001.png) | 2 | PENDING |
| 11 | `waf_architecture_building_gold_mine` | [gold_mine](packets/gold_mine/gold_mine_exterior_handoff_v001.png) | 3 | PENDING |
| 12 | `waf_architecture_building_lumber_mill` | [lumber_mill](packets/lumber_mill/lumber_mill_exterior_handoff_v001.png) | 2 | PENDING |
| 13 | `waf_architecture_building_market` | [market](packets/market/market_exterior_handoff_v001.png) | 2 | PENDING |
| 14 | `waf_architecture_building_quarry` | [quarry](packets/quarry/quarry_exterior_handoff_v001.png) | 3 | PENDING |
| 15 | `waf_architecture_building_stable` | [stable](packets/stable/stable_exterior_handoff_v001.png) | 2 | PENDING |
| 16 | `waf_architecture_building_storehouse` | [storehouse](packets/storehouse/storehouse_exterior_handoff_v001.png) | 2 | PENDING |
| 17 | `waf_architecture_building_town_hall` | [town_hall](packets/town_hall/town_hall_exterior_handoff_v001.png) | 2 | PENDING |
| 18 | `waf_architecture_building_watchtower` | [watchtower](packets/watchtower/watchtower_exterior_handoff_v001.png) | 3 | PENDING |
| 19 | `waf_architecture_building_workshop` | [workshop](packets/workshop/workshop_exterior_handoff_v001.png) | 2 | PENDING |
| 20 | `waf_architecture_castle_enterable` | [castle_enterable](packets/castle_enterable/castle_enterable_exterior_handoff_v001.png) | 4 | PENDING |
| 21 | `waf_architecture_fortress_enterable` | [fortress_enterable](packets/fortress_enterable/fortress_enterable_exterior_handoff_v001.png) | 4 | PENDING |
| 22 | `waf_architecture_guardpost_watch` | [guardpost_watch](packets/guardpost_watch/guardpost_watch_exterior_handoff_v001.png) | 2 | PENDING |
| 23 | `waf_architecture_inn_tavern` | [inn_tavern](packets/inn_tavern/inn_tavern_exterior_handoff_v001.png) | 3 | PENDING |
| 24 | `waf_architecture_mill_wind_water` | [mill_wind_water](packets/mill_wind_water/mill_wind_water_exterior_handoff_v001.png) | 3 | PENDING |
| 25 | `waf_architecture_religious_cultural_structure` | [religious_cultural_structure](packets/religious_cultural_structure/religious_cultural_structure_exterior_handoff_v001.png) | 2 | PENDING |
| 26 | `waf_architecture_shop_service` | [shop_service](packets/shop_service/shop_service_exterior_handoff_v001.png) | 2 | PENDING |
| 27 | `waf_architecture_warehouse_barn` | [warehouse_barn](packets/warehouse_barn/warehouse_barn_exterior_handoff_v001.png) | 2 | PENDING |

For each row, review the linked exterior sheet, the adjacent interior sheet in the same folder, and the five explicit decision gates in the JSON. The packet remains non-authoritative for 3D until its owner decision is recorded.

## Authority and exclusions

The approved PR #664 civic-hall and fort-gatehouse plans remain shared spatial/module authority. The Stonehold sheets here add realm-specific recommendations and all-family coverage but do not silently alter the locked civic-hall envelope. The four-realm boards are directional only. Slagfall Quarry is not architecture-visual authorization. Religious/royal symbols, localized text, heraldry and exact ritual meaning remain narrative/owner gated.

Rollback is deletion or one squash revert. This package is documentation and source-art evidence only; it changes no runtime catalog, save schema, scene, prefab, gameplay, balance, or release configuration.
