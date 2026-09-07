# AnotherLife World 2D-to-3D Production Guide V001

**Status:** Automatically approved for 3D production under the owner's overnight decision authority; retained for morning visual inspection.

## Authority and limits

- V013 remains the world topology authority. These sheets define visual language only.
- They do not rename catalog/save IDs, alter topology, invent capture mechanics, or modify Town Hall/Workshop assets.
- Realm dragons and bosses are excluded.
- The target is premium-AA dark-fantasy stylized realism inspired by the visual finish bar of Black Desert Online, not literal parity or imitation.
- Generated 1536×1024 originals are preserved unchanged. Each approved source also has a 7680×5120 authoring copy. The 8K copy is a production reference/upscale, not a claim of newly generated native detail.

## Shared versus unique rule

### Shared construction family

May be reused across realms after realm-appropriate material variants:

- Neutral timber/stone structural blocks and connectors
- Basic plantation beds and trellises
- Crates, barrels, carts, sacks, benches, shelves, tools, fences, and utility lamps
- Technical pivots, sockets, collision conventions, texel-density rules, LOD naming, and trim-sheet layouts

Shared objects must remain free of realm crests and defining ornament. Color alone must never be the only realm difference.

### Realm-specific authored families

Must not be copied between realms:

- Sequential outer/inner gate barriers and their controlled passage
- Fortress perimeter, gatehouse, castle silhouette, and central objective architecture
- Capital and hero-building silhouettes
- Primary roofline, buttress, window, doorway, column, stair, walltop, and parapet language
- Signature trim, emblem, threshold, signage, and ceremonial hardware
- Interior spatial hierarchy, civic function, focal fixture, and realm-specific furnishing clusters
- Hero material response, terrain seating, skyline rhythm, and ecosystem framing

## Universal gate and fortress contract

- Realm boundary: one continuous wall complex with two sequential barriers, not two unrelated walls.
- Authorized route crosses both gate faces approximately perpendicularly.
- Defender-only stairs reach walltops without creating an attacker route into the inner realm.
- Wall ends terminate in impassable terrain.
- Stable IDs remain unchanged, including `gate_stonehold_faultline`, `gate_crownlands_meridian`, `gate_eldergrove_greenveil`, and `gate_umbral_ashvein`.
- Every fortress has one connected perimeter, exactly one gated entrance, defendable walltops, an enclosed castle-like structure, and a central objective/flag anchor.
- Maintain at least 30 m of flat or outward-descending empty apron beyond every fortress wall. Reject elevated terrain, roots, spires, rocks, foliage, debris, clutter, stairs, or props that could assist entry.

## Realm identities

### Stonehold — Tempered Embermist

- **Silhouette:** tectonic horizontal mass, battered basalt fins, heavy iron plates, compressed openings.
- **Gate:** paired dark-iron barriers locked into faultline buttresses; mechanical weight over ornament.
- **Fortress:** low, broad, one-gate bastion with forge keep and ash-worn defensive faces.
- **Interior:** traversable forge-guard program—receiving hall, armory, smithing floor, stores, mess, barracks, command room, walltop routes.
- **Materials:** matte basalt, heat-darkened iron, restrained iron-gold edgework, soot, ash and localized warm practical light.
- **Forbidden drift:** dwarven caricature, lava-theme saturation, climbable talus in the apron.

### Crownlands — Meridian Oathroad

- **Silhouette:** chalk ribs, segmented meridian architecture, axial towers, blue-slate crowns, and a storm-pressed oathroad.
- **Gate:** one continuous Meridian complex in pale masonry with silver ribs and restrained brass/gold.
- **Fortress:** one-gate highland citadel with ordered tower rhythm, rain-dark fractures, and clear walltop command lines.
- **Interior:** council-and-archives program—formal vestibule, record galleries, strategy chamber, court, clerks' rooms, guarded vault, service circulation.
- **Materials:** pale limestone, cool silver, weathered blue slate, restrained bronze and deep blue textile accents.
- **Forbidden drift:** generic cathedral, excessive gold, fragile needle spires, ungrounded white-marble palace.

### Eldergrove — Moonroot Vigil

- **Silhouette:** asymmetric root-held masses, pale shelves, spaced oldgrowth canopy, broad low curves.
- **Gate:** crafted pale stone and dark timber framed by bounded living roots seated in bronze collars.
- **Fortress:** one-gate warden stronghold; structural roots remain inside the perimeter or within wall fabric.
- **Interior:** warden-council workshop—circular moot chamber, herbal preparation, map table, archive niches, bunks, stores and root-maintenance access.
- **Materials:** pale mineral stone, dark timber, aged bronze, desaturated root bark, restrained moon-silver/pale-gold practical light.
- **Forbidden drift:** root portal, neon bioluminescence, dense canopy hiding traversal, any exterior root that violates the clear apron.

### Umbral — Three-Fault Ashvein

- **Silhouette:** three converging fault directions, broad obsidian ribs, walkable ash terraces, one pressure basin, and supported offset roofs.
- **Gate:** one continuous Ashvein complex with sequential graphite barriers, grounded ash-timber yokes, and smoked-glass slits.
- **Fortress:** one-gate records stronghold with defensible offset volumes but an unambiguous authorized route.
- **Interior:** veiled council-and-records program—screened entry, witness chamber, tiered archive, private council, secure stacks, stores and service stairs.
- **Materials:** graphite/ash/smoked-glass separation with dull ember restricted to active cracks.
- **Forbidden drift:** portal language, violet glow dependence, black fog, uniformly crushed values, unsupported roofs, unsafe blind traversal.

### Accordant Isle — Petal Concord

- **Silhouette:** low neutral council ring beneath an asymmetric cherry canopy with four equal realm-facing thresholds.
- **Approaches:** all four are separately identifiable and show two off-event denial reads: retracted/missing span plus a closed grounded blossom-stone seal.
- **Civic structure:** not a normal-realm occupation fortress; ceremonial defense and neutral assembly are equally legible.
- **Interior:** traversable accord assembly—central round chamber, four delegation galleries, mediation rooms, archive, stores and discreet security circulation.
- **Materials:** neutral weathered stone, dark timber, restrained aged bronze, muted blossom medallions, and warm practical light.
- **Forbidden drift:** screen-filling petals, theme-park pink, open off-event bridges, permanent fifth-capital cues, one realm visually dominating, combat-fortress aggression.

## 3D handoff requirements

For every realm-specific family, the model packet must include:

1. Front/side silhouette check and player-scale reference.
2. Modular breakdown with pivots, sockets and reusable spans.
3. Traversable floor plan and stair/door clearances.
4. Terrain-seating and wall-end termination solution.
5. Shared trim/material use plus unique hero-material slots.
6. LOD0–LOD3 strategy, collision plan and occlusion/streaming boundaries.
7. Exterior, interior and walltop camera checks at PC quality.
8. Gate security and 30 m fortress-apron validation before dressing.

## Visual-audit disposition

- The shared common-object sheet and all five realm style sheets are accepted as visual direction.
- Fifteen focused gate/fortress/interior sources were generated: three for each realm/island.
- Focused fortress sources provide distinct silhouettes, one obvious entrance, and visually clear aprons; they do not prove a measured 30 m clearance. The 3D validators remain mandatory.
- Focused interiors provide differentiated furnishing and circulation programs; exact door, stair and room dimensions must be rebuilt from gameplay metrics rather than copied from AI imagery.
- The focused AI gate images repeatedly read the outer and inner barriers side-by-side. They are therefore **style-only**, not topology authority.
- Four deterministic gate plates correct that ambiguity and control the realm sequence: outer realm → outer barrier → controlled passage → inner barrier → inner realm. Accordant's separate deterministic plate instead controls its off-event two-part denial: retracted/missing span plus closed grounded seal.

## Source inventory

| Asset | Generated original | 8K authoring copy | Decision |
|---|---|---|---|
| Shared modular kit | `shared_modular_kit_source.png` | `shared_modular_kit_source_8k_authoring.jpg` | Approved as shared baseline |
| Stonehold architecture | `stonehold_architecture_source.png` | `stonehold_architecture_source_8k_authoring.jpg` | Approved direction |
| Crownlands architecture | `crownlands_architecture_source.png` | `crownlands_architecture_source_8k_authoring.jpg` | Approved direction |
| Eldergrove architecture | `eldergrove_architecture_source.png` | `eldergrove_architecture_source_8k_authoring.jpg` | Approved direction |
| Umbral architecture | `umbral_architecture_source.png` | `umbral_architecture_source_8k_authoring.jpg` | Approved direction |
| Accordant architecture | `accordant_architecture_source.png` | `accordant_architecture_source_8k_authoring.jpg` | Approved direction |

Each Stonehold, Crownlands, Eldergrove, Umbral and Accordant subdirectory additionally contains:

- Realm-specific `gate_v001.png`, `fortress_v001.png`, and `interior_v001.png` sources
- Corresponding 7680×5120 `_8k_authoring.jpg` copies
- A combined realm production contact sheet
- Deterministic PNG and editable SVG gate/approach technical plates

The generated images are visual-direction sources, not literal geometry blueprints. Builders must follow the written security, traversal, topology, and interior contracts—and the deterministic gate plates—whenever an image is ambiguous.
