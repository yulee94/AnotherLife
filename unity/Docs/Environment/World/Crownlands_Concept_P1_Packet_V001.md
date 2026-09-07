# AnotherLife Crownlands Concept Packet P1 V001

**Status:** AUTO-APPROVED FOR DETERMINISTIC BLENDER (owner inspection retained) — see `Crownlands_Concept_P1_Packet_V001_DECISION.md`<br>
**Provider:** Grok 4.6 High directing `grok-imagine-image-2.0` (xAI OAuth). GPT-5.6 Sol was not used.<br>
**Visual package:** `unity/ArtSource/Environment/CrownlandsConceptP1/V001/`<br>
**Does not overwrite:** World 2D-to-3D Production Review V001, V013, catalogs, saves, gameplay, Stonehold packets, Eldergrove packets, inventory JSON, Town Hall/Workshop, Meshy, Blender, Unity, Android.

## Authority and limits

- This packet is **visual / spatial program only**.
- V013 remains topology authority: northeast Crownlands, organic inner 33.3333% safe pocket, outer 66.6667% warzone, `capital_crownspire`, three inner-city pads (no invented city IDs), one inner cave, one outer cave ~3 minutes / 180 s from the dual-gate complex.
- Sequential-gate plates remain topology/security authority. Do not treat any capital approach as outer+inner barriers side by side.
- Catalog IDs control instance identity. Concepts do not rename IDs. `capital_crownspire` is preserved. City-kit `stableIds` stay empty.
- Realm dragons and bosses are excluded. Accordant Isle is not in this packet.
- Native generations are 3:2 (observed 1248×832). Each source has a 7680×5120 `_8k_authoring.jpg` Lanczos upscale, **not** a native 8K generation.
- 30 m fortress apron is a fortress-validator metric. This packet does not depict fortresses; cave-mouth rubble is not an apron diagram.
- Architecture language only: `unity/ArtSource/Environment/World2DProductionGuides/V001/crownlands_architecture_source.png`. Direction **B — Meridian Oathroad**.
- Existing Town Hall / Workshop footprints, pivots, anchors, LOD policy, and stable model/catalog IDs remain unchanged.

## Families covered

| Family | Inventory id | Sheets |
|---|---|---|
| Capital Crownspire | `fam_crownlands_capital` | district plan, N/S skyline, E/W skyline, keep shell orthos, keep longitudinal/cross section, furnished ground plan, furnished upper/walltop circulation |
| City kit | `fam_crownlands_city_kit` | 6 m street grammar, dwelling shell, dwelling furnished interior, market/service/public-hall modular kit with 2.5×3.0 m public aperture |
| Inner cave (non-dragon) | `fam_crownlands_inner_cave_dungeon` | mouth, section+circulation, chamber/fitting module sheet (inner row; outer fittings on the same sheet) |
| Outer warzone cave (non-dragon) | `fam_crownlands_outer_cave_dungeon` | mouth, actual loop/choke section |

Inventory listed these families as concept-gaps. This packet is an isolated Crownlands P1 concept drop. The inventory JSON is not rewritten by this packet.

## Enterable rule

- Where a building is represented, it is a real volume with wall thickness and apertures — no fake shells.
- Dwelling / market / service / public-hall interiors are **small seamless** volumes.
- Hero-keep interior is a **large streamed combat** volume. Rebuild wing breaks as thick walls.
- Skyline keep block is city silhouette only. Rebuild the enterable keep from the shell orthos + section + interior plans.

## Dimensional control (not copied from AI numerals)

AI scale-bar labels are frequently garbled and are **not** metric authority.

| Item | Controlling value | Source |
|---|---|---|
| Player height | 1.8 m | world convention |
| Civic interior door | 1.2 × 2.4 m | civic hall apertures |
| Public hall opening | 2.5 × 3.0 m | WorldSharedKit public-hall family |
| City street width | ~6 m | this packet visual intent |
| Keep visual intent | ~56 m across / ~18 m to walltop | this packet; rebuild in Blender from numbers |
| Dwelling footprint | ~8 × 10 m | this packet intent |
| Inner cave mouth | ~4.5 × 3.6 m | this packet intent |
| Outer cave mouth | ~6.0 × 4.2 m | this packet intent |
| Capital id | `capital_crownspire` | inventory / V013 |
| Inner cities / inner cave / outer cave | 3 pads (unnamed) / 1 / 1 | V013; no invented city IDs |
| Fortress apron | ≥30 m | fortress contract; not measured from these pixels |
| Outer cave from dual-gate | ~180 s / 1,080 m | V013; do not draw the gate |

## Realm identity used

**Crownlands — Meridian Oathroad:** grounded chalk-gold ashlar, deep blue slate, brass meridian ribs and compass-rose crests, cool silver edgework, restrained bronze, rain-dark mortar fractures. Broad civic masses, stout axial drums with blue-slate pavilion roofs, readable construction. No delicate needle spires, generic cathedral, excess gold, ungrounded white-marble palace, color-only variants, fake shells, inaccessible interiors, copied proprietary BDO forms, Embermist palette swap, or Moonroot root collars.

Keep program: formal vestibule, record galleries, strategy chamber, court, clerks' rooms, guarded vault, service circulation, walltop.

## 3D handoff (after decision PASS)

Deterministic Blender only. No Meshy. No Unity/Android in this packet. Do not start any 3D generation from this packet.

1. Rebuild from written dimensions and V013. Use these sheets for material, ornament, silhouette, and room program.
2. Capital streets follow organic corner-enclosing broken offset terraces / chalk-rib grain as an L-/wedge pocket against two impassable cliff edges with an open opposite approach; do not copy oval, ring, circular-island, or radial Renaissance plans as continent topology.
3. City modules are unique Meridian Oathroad cladding, not palette-swaps. Doors stay separate prefabs. Public-hall aperture is 2.5 × 3.0 m.
4. Inner cave is a reliquary chalk gallery. Outer cave is combat loop with choke and cover — not a boss arena and not a dragon lair.
5. Do not redesign Town Hall or Workshop.

## Source inventory

See `unity/ArtSource/Environment/CrownlandsConceptP1/V001/crownlands_concept_p1_manifest_v001.json` for hashes, byte lengths, per-sheet provenance, and 8K provenance.
Review surface: `Crownlands_Concept_P1_Packet_V001.html`.
