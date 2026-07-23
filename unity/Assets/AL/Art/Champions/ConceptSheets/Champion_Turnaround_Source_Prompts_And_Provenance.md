# Champion Turnarounds — Source Prompts and Provenance

## Shared status

- Generated: 2026-07-23
- Tool path: Codex built-in image generation
- Source type: AI-assisted visual-direction proposals
- Reference image: `champion_four_realm_anchor_v001.png`
- Design authority: Root `DESIGN.md` and `unity/Assets/AL/Art/Designs/FourRealmChampionAnchor.md`
- Runtime authority: None. These images do not prove topology, textures, UVs, rigging, animation, LODs, materials, or measured mobile performance.
- Owner status: Multi-angle and surface expansion requested; individual sheets await project-owner review.

## Selected files and validation

| Realm | File | SHA-256 | Visual verdict | 390 px color | 390 px grayscale |
| --- | --- | --- | ---: | --- | --- |
| Stonehold | `champion_stonehold_vanguard_turnaround_v001.png` | `4a53de4ccb4993e92db42ea17e3b1b4cc9970df4e39de28c0bb230fe738f259c` | `92 / 100` | Pass | Pass |
| Eldergrove | `champion_eldergrove_vanguard_turnaround_v001.png` | `a5c9423bd6edf0362c99aed3aa68d39e277c747d6d767ec8eda62d38eb96830a` | `91 / 100` | Pass | Pass |
| Crownlands | `champion_crownlands_vanguard_turnaround_v001.png` | `b6fd94b914d7ce90d245865808b47bc216ebde1468f6892e327ac552a506c5b4` | `94 / 100` | Pass | Pass |
| Umbral | `champion_umbral_vanguard_turnaround_v001.png` | `f3934fc6229f992eb2d2d0855f15df48bfc3877f3b08156bc4cc7ea5ea6e7637` | `93 / 100` | Pass | Pass |

All compact previews retained full-body, shield, weapon, and major surface grouping. Grayscale checks confirm that realm construction and silhouette remain readable without magical accent color.

## Stonehold generation prompt

```text
Use case: stylized-concept
Asset type: mobile game character multi-angle model sheet with surface-texture references
Input image: the provided four-realm anchor is the approved direction reference. Use ONLY the LEFTMOST Stonehold Vanguard as the identity, armor, shield, weapon, palette, and material reference. Create a NEW dedicated Stonehold sheet; do not alter the source image.
Primary request: show the exact same Stonehold Vanguard consistently from four angles in a single landscape sheet, ordered left to right: FRONT, THREE-QUARTER FRONT, TRUE SIDE, BACK. Preserve the same adult face, short dark hair, realistic 7.75-head body proportions, broad grounded silhouette, layered basalt and soot-aged iron armor, square shoulders, short heavy mantle, leather belt structure, clipped-corner shield, compact broad sword, bronze repair details, and one tiny forge-amber focal seam.
Pose/equipment: neutral modeling stance with feet planted and arms slightly separated enough to reveal armor joints. Keep face visible and no helmet. In the front and three-quarter views, hold the shield and sword at rest without obscuring the torso. In the side and back views, place the shield slightly away from the body or mounted consistently so the side and back armor construction remains visible. The shield and sword design must remain identical in every view.
Surface texture: tactile high-end realistic materials like the approved creature concepts. Macro: clear dark basalt, aged iron, brown leather, and restrained bronze zones. Meso: broad forge hammering, directional soot, repair bands, chipped mineral edges, contact wear at knees, boots, shield rim, and grips. Micro: sparse iron pitting, leather grain, fine stone pores, subtle copper oxidation. Keep microdetail subordinate to large shapes and suitable for normal/roughness maps rather than modeled geometry. No uniform grunge.
Material callouts: add four large unlabeled square or circular close-up swatches along a clean bottom strip: basalt plate, soot-aged iron, worn heavy leather with bronze fastener, and the restrained amber mineral seam embedded in physical material. Swatches must not overlap the figures.
Style/medium: premium realistic fantasy production concept art, mystical medieval naturalism, physically plausible armor mobility and material response, serious adult tone, near-orthographic long-lens model-sheet rendering.
Composition/framing: wide landscape, four equal full-body views at identical scale, head to toe, generous safe padding, straight horizon, minimal perspective distortion. Dark neutral charcoal studio background with faint aged-stone texture and restrained gothic framing. No atmospheric smoke hiding forms.
Mobile optimization: strong 70/20/10 hierarchy, large uninterrupted armor masses, two-to-three large value groups, limited small straps and seams, no silhouette-dependent micro-ornament, no emission outlining edges. Protected cues are broad shoulder mass, layered basalt/iron value break, clipped-corner shield, and one warm amber focal seam.
Constraints: exact cross-view consistency; no redesign between angles; no text, labels, letters, logos, watermark, extra characters, helmets, capes longer than the approved short mantle, scenery, floating props, excessive spikes, excessive filigree, cartoon style, low-poly final look, copied franchise motifs, or cropped body/equipment.
```

## Eldergrove generation prompt

```text
Use case: stylized-concept
Asset type: mobile game character multi-angle model sheet with surface-texture references
Input image: the provided four-realm anchor is the approved direction reference. Use ONLY the SECOND-FROM-LEFT Eldergrove Vanguard as the identity, armor, shield, weapon, palette, and material reference. Create a NEW dedicated Eldergrove sheet; do not alter the source image.
Primary request: show the exact same Eldergrove Vanguard consistently from four angles in one landscape sheet, ordered left to right: FRONT, THREE-QUARTER FRONT, TRUE SIDE, BACK. Preserve the same adult face, short dark hair, realistic 7.75-head body proportions, tall open silhouette, interlocked living-wood and weathered-bronze armor, dark woven fiber, restrained moss/lichen, curved shield with strong living central spine, leaf-tapered sword, and one small green-gold living-repair focal point.
Pose/equipment: neutral modeling stance with feet planted and arms slightly separated to reveal joints and modular construction. Face visible, no helmet. Hold weapon and shield at rest without hiding the torso in the front views. In side and back views, move or mount equipment consistently so the grown back construction and textile layers remain visible. Keep shield and sword design identical in every view.
Surface texture: tactile high-end realistic materials like the approved creature concepts. Macro: broad dry bark, dark woven textile, softly reflective weathered bronze, restrained living-growth zones. Meso: directional growth grain, large grown joins, grouped lichen, fiber direction, contact wear, subtle bronze patina. Micro: sparse bark pores, fine textile weave, moss softness, small bronze oxidation. Keep broad uninterrupted surfaces and deliberate branching negative spaces. Surface richness should live mostly in normal, color, and material-response maps rather than silhouette geometry. No tiny twig clutter or uniform foliage noise.
Material callouts: add four large unlabeled square or circular close-up swatches along a clean bottom strip: dry living bark, weathered bronze with patina, dark woven fiber with restrained lichen, and the green-gold living-repair energy embedded in physical grown material. Swatches must not overlap figures.
Style/medium: premium realistic fantasy production concept art, mystical medieval naturalism, physically plausible armor mobility and grown construction, serious adult tone, near-orthographic long-lens model-sheet rendering.
Composition/framing: wide landscape, four equal full-body views at identical scale, head to toe, generous safe padding, straight horizon, minimal perspective distortion. Dark neutral charcoal studio background with faint aged-stone texture and restrained gothic framing. No atmospheric fog hiding forms.
Mobile optimization: strong 70/20/10 hierarchy; broad bark, bronze, and textile zones; two or three clean branching negative spaces; limited small ridges and leaves; no transparency-dependent identity; one localized living focal point. Protected cues are the tall open silhouette, curved shield spine, living-wood planes, weathered bronze reinforcement, and green-gold focal source.
Constraints: exact cross-view consistency; no redesign between angles; no text, labels, letters, logos, watermark, extra characters, helmet, antlers, dense foliage, leaf-covered costume, excessive spikes, excessive filigree, floor-length cape, scenery, floating props, cartoon style, low-poly final look, copied franchise motifs, or cropped body/equipment.
```

## Crownlands generation prompt

```text
Use case: stylized-concept
Asset type: mobile game character multi-angle model sheet with surface-texture references
Input image: the provided four-realm anchor is the approved direction reference. Use ONLY the THIRD-FROM-LEFT Crownlands Vanguard as the identity, armor, shield, weapon, palette, and material reference. Create a NEW dedicated Crownlands sheet; do not alter the source image.
Primary request: show the exact same Crownlands Vanguard consistently from four angles in one landscape sheet, ordered left to right: FRONT, THREE-QUARTER FRONT, TRUE SIDE, BACK. Preserve the same adult face, short dark hair, realistic 7.75-head proportions, balanced upright heraldic silhouette, aged silver and polished steel armor, disciplined panel breaks, midnight royal-blue textile, dark leather, restrained gold engraving, clean kite shield, straight longsword, and one focused indigo celestial accent.
Pose/equipment: neutral modeling stance with feet planted and arms slightly separated to reveal armor joints and modular seams. Face visible, no helmet. Hold sword and shield at rest without obscuring the torso in front views. In side and back views, move or mount equipment consistently so the back plate, textile tailoring, shoulder assembly, and waist construction remain visible. Shield and sword design must stay identical in every view.
Surface texture: tactile high-end realistic materials like the approved creature concepts and mystical app emblem. Macro: controlled silver/steel planes, deep woven blue textile, dark leather, restrained aged gold. Meso: directional plate brushing, disciplined forge seams, tailored textile folds and stitching, contact wear, subtle large heraldic engraving. Micro: sparse steel scratches, fine textile weave, leather pores, slight gold tarnish. Keep engraving restrained and subordinate to large plate shapes. Surface richness should live mostly in normal, color, and material-response maps, not extra silhouette geometry. No pristine plastic armor.
Material callouts: add four large unlabeled square or circular close-up swatches on a clean bottom strip: aged silver/steel, midnight royal-blue woven textile, dark worn leather with restrained aged-gold fastening, and the indigo celestial focus embedded in a physical shield or weapon channel. Swatches must not overlap figures.
Style/medium: premium realistic fantasy production concept art, mystical medieval naturalism, physically plausible plate mobility and tailoring, serious adult tone, near-orthographic long-lens model-sheet rendering.
Composition/framing: wide landscape, four equal full-body views at identical scale, head to toe, generous safe padding, straight horizon, minimal perspective distortion. Dark neutral charcoal studio background with faint aged-stone texture and restrained gothic framing. Neutral light must reveal steel roughness without blowing out highlights.
Mobile optimization: strong 70/20/10 hierarchy; large silver and blue value groups; simple readable kite shield and straight weapon direction; limited engraving; one localized celestial focal point; no emission outlining armor. Protected cues are balanced shoulder line, kite shield, blue textile block, controlled precious-metal contrast, and indigo focus.
Constraints: exact cross-view consistency; no redesign between angles; no text, labels, letters, logos, watermark, extra characters, helmet, crown, excessive gold, excessive filigree, copied real-world heraldry, pristine generic paladin styling, floor-length cape, scenery, floating props, cartoon style, low-poly final look, copied franchise motifs, or cropped body/equipment.
```

## Umbral generation prompt

```text
Use case: stylized-concept
Asset type: mobile game character multi-angle model sheet with surface-texture references
Input image: the provided four-realm anchor is the approved direction reference. Use ONLY the RIGHTMOST Umbral Vanguard as the identity, armor, shield, weapon, palette, and material reference. Create a NEW dedicated Umbral sheet; do not alter the source image.
Primary request: show the exact same Umbral Vanguard consistently from four angles in one landscape sheet, ordered left to right: FRONT, THREE-QUARTER FRONT, TRUE SIDE, BACK. Preserve the same adult face, short dark hair, realistic 7.75-head proportions, lean narrow predatory silhouette, enlarged blackened-steel and obsidian-like plates, aubergine and ash cloth, deliberate negative spaces, restrained asymmetric plate rhythm, narrow angular sword, asymmetric split-plane shield, and one localized cold-violet fracture path.
Pose/equipment: neutral modeling stance with feet planted and arms slightly separated to reveal joints and modular seams. Face visible, no helmet. Hold sword and shield at rest without hiding the torso in front views. In side and back views, move or mount equipment consistently so the back plate, cloth, shoulder assembly, shield gap, and waist construction remain visible. Shield and sword design must stay identical in every view.
Surface texture: tactile high-end realistic materials like the approved creature concepts and mystical app emblem. Macro: readable satin blackened steel, glassy obsidian-like planes, absorbent aubergine/ash cloth, and one restrained physical-to-magical fracture zone. Meso: large fracture planes, directional smoke wear, offset plate edges, sparse glass transitions, contact wear. Micro: fine ash fibers, cool metal scratches, sparse obsidian inclusions, subtle soot and edge polish. Lift charcoal and aubergine midtones enough to reveal material separation without relying on violet glow. Keep texture detail subordinate to large shapes and mostly in normal, color, and material-response maps.
Material callouts: add four large unlabeled square or circular close-up swatches on a clean bottom strip: satin blackened steel, glassy obsidian-like material with restrained inclusions, absorbent aubergine ash cloth, and the single cold-violet fracture embedded in a physical shield or weapon channel. Swatches must not overlap figures.
Style/medium: premium realistic fantasy production concept art, mystical medieval naturalism, physically plausible armor mobility, serious adult tone, near-orthographic long-lens model-sheet rendering. Dark but fully readable.
Composition/framing: wide landscape, four equal full-body views at identical scale, head to toe, generous safe padding, straight horizon, minimal perspective distortion. Dark neutral charcoal studio background with faint aged-stone texture and restrained gothic framing. Neutral key light must preserve dark-material midtones and edges.
Mobile optimization: strong 70/20/10 hierarchy; large blackened-steel, obsidian, and aubergine value zones; very few spikes; one strong split-plane shield cue; one localized violet fracture; no full-edge emission. Protected cues are lean silhouette, angular shield gap, offset plate rhythm, charcoal/aubergine separation, and one cold-violet focal path.
Constraints: exact cross-view consistency; no redesign between angles; no text, labels, letters, logos, watermark, extra characters, helmet, featureless black, excessive spikes, excessive shards, excessive purple glow, glow on every edge, gore, floor-length cape, scenery, floating props, cartoon style, low-poly final look, copied franchise motifs, or cropped body/equipment.
```

## Production cautions

- The generated faces and exact anatomy are not yet identity approvals.
- Image-to-image consistency is sufficient for direction review, not automatic orthographic modeling truth.
- Equipment-separated artist turnarounds are still required before topology and rigging.
- Surface swatches communicate material families and response; they are not tileable textures or final shader inputs.
- Fine scratches, weave, bark pores, pitting, engraving, and inclusions must degrade through mips and LODs before macro material grouping changes.
- Final material packing, texture sizes, and shader features depend on measured performance on the lowest supported iPhone.
