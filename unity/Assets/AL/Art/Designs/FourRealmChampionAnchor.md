# Four-Realm Champion Anchor

**Status:** Owner-approved visual source direction; production implementation not yet approved

**Version:** 0.2

**Design contract:** Root `DESIGN.md`

**Asset category:** Champion / major character

**Runtime priority:** Mobile-first, scalable to PC and promotional presentation

**Approved visual anchor:** [Four-realm Champion anchor v001](../Champions/ConceptSheets/champion_four_realm_anchor_v001.png)

**Generation record:** [Source prompts and provenance](../Champions/ConceptSheets/Champion_Anchor_Source_Prompts_And_Provenance.md)

**Approved multi-angle source sheets:**

- [Stonehold Vanguard turnaround v001](../Champions/ConceptSheets/champion_stonehold_vanguard_turnaround_v001.png)
- [Eldergrove Vanguard turnaround v001](../Champions/ConceptSheets/champion_eldergrove_vanguard_turnaround_v001.png)
- [Crownlands Vanguard turnaround v001](../Champions/ConceptSheets/champion_crownlands_vanguard_turnaround_v001.png)
- [Umbral Vanguard turnaround v001](../Champions/ConceptSheets/champion_umbral_vanguard_turnaround_v001.png)
- [Turnaround prompts, checksums, and validation](../Champions/ConceptSheets/Champion_Turnaround_Source_Prompts_And_Provenance.md)

## Purpose

Establish one readable Champion foundation that translates Another Life's mystical medieval naturalism into four unmistakable realm identities. This anchor compares the same adult Vanguard role, scale, camera, and approximate rig envelope across Stonehold, Eldergrove, Crownlands, and Umbral.

The anchor is intended to settle:

- Shared adult proportions and major equipment zones.
- Realm silhouette, construction, material, and magical-source differences.
- Mobile gameplay and portrait readability.
- A reusable visual reference for human and AI-assisted contributors.

It does not approve final face options, body-type range, production topology, narrative identity, exact palette tokens, or a final runtime model.

## Owner decision record

- 2026-07-23: Project owner accepted the four-realm anchor direction and requested creature-sheet-quality surface treatment plus additional model angles.
- 2026-07-23: Project owner accepted all four detailed turnaround and material sheets and authorized publication to the shared repository.
- Approval scope: realm silhouettes, shared Vanguard comparison strategy, overall mystical medieval finish, and progression to detailed sheets.
- Approved source scope: Stonehold, Eldergrove, Crownlands, and Umbral armor construction, shield/weapon direction, macro material families, controlled magical focal points, surface hierarchy, and multi-angle visual source.
- Not yet approved: final production meshes, exact surface maps, topology, rig, body/identity range, orthographic artist corrections, shader implementation, or measured runtime budgets.

## Shared Champion foundation

- Realistic adult human anatomy at approximately `7.75` heads tall.
- Athletic enough for combat, without exaggerated shoulders, waist, chest, hips, hands, or weapons.
- Same overall height, stance, limb proportions, camera, and neutral lighting in all four presentations.
- Face remains visible and naturally proportioned so the character reads as a person before equipment.
- Armor leaves believable room for movement at the neck, shoulders, elbows, waist, hips, knees, and ankles.
- One-handed Vanguard weapon and defensive off-hand remain recognizable at mobile gameplay distance.
- Equipment aligns to the existing modular zones: head, hair, face, chest, shoulders, arms, legs, cape/mantle, main hand, off hand, and realm ornament.
- The initial anchor uses one consistent body and rig envelope to isolate realm design. Body and identity diversity require a later compatibility sheet before production finalization.

## Mobile-first visual rules

- Preserve one clean full-body silhouette and two or three large internal value groups per realm.
- Use the `70 / 20 / 10` hierarchy: primary body and armor masses, secondary equipment forms, restrained tertiary engraving and wear.
- Keep the face, weapon, shield, hands, and major realm cue readable in a character-creation portrait and normal gameplay view.
- Avoid thin dangling chains, dense layered belts, fragile spikes, loose floor-length capes, excessive feathers, floating ornaments, and transparency-dependent identity.
- Use no more than three dominant material families in the primary read. Small accents may reuse packed material channels.
- Concentrate emission in one primary and, at most, one secondary focal area. Magic must not outline every armor edge.
- Realm recognition must survive grayscale, reduced particles, reduced texture resolution, and removal of emission.
- Design every protected identity cue to survive LOD reduction without adding geometry back through VFX.

## Surface-texture strategy

Match the approved terrestrial sheets' tactile material separation while keeping runtime geometry economical. Surface richness must reinforce the large armor and body forms rather than compete with them.

### Texture hierarchy

1. **Macro read:** Large metal, stone, bark, textile, leather, and magical-material zones visible at gameplay distance.
2. **Meso read:** Plate direction, forging, growth grain, weave scale, repairs, edge condition, and contact wear visible during Champion presentation.
3. **Micro read:** Pores, scratches, fibers, pitting, fine grain, and engraving reserved for close inspection and higher texture mips.

Macro identity must survive when meso and micro information disappear.

### Mobile implementation intent

- Keep silhouette-changing damage, major plate overlaps, shield openings, and protected realm cues in geometry.
- Move shallow engraving, hammered metal, bark grain, weave, leather grain, fine scratches, and mineral pores into normal, color, and material-response maps.
- Use large value and roughness separation before relying on high-frequency normal detail.
- Reuse tiled or trim-based material families where they preserve the approved craft language.
- Pack compatible grayscale masks when it reduces memory and sampling without obscuring ownership.
- Author high-resolution source textures only when they produce measured runtime derivatives.
- Prefer one reusable realm material family plus asset-specific masks over a unique shader for each armor piece.
- Avoid parallax, deep layered transparency, animated micro-displacement, and full-surface emission as identity requirements.
- Ensure mip reduction removes micro-noise cleanly instead of turning armor into shimmer.
- Keep face and hair texture budgets independent from armor when customization or inspection requires it, but merge materials in lower LODs where practical.

### Realm surface anchors

| Realm | Primary surface response | Meso detail | Micro detail | Mobile protection |
| --- | --- | --- | --- | --- |
| **Stonehold** | Matte basalt against low-to-medium roughness aged iron | Forge hammering, repair bands, soot direction, chipped mineral edges | Fine iron pitting, leather grain, restrained copper oxidation | Basalt/iron value break and one warm mineral seam |
| **Eldergrove** | Dry bark and woven fiber against softly reflective weathered bronze | Growth flow, lichen grouping, grown joins, fiber direction | Bark pores, fine weave, moss softness, bronze patina | Broad living-wood planes, bronze spine, branching negative spaces |
| **Crownlands** | Controlled steel reflections against deep woven blue textile | Disciplined plate brushing, tailored seams, restrained heraldic engraving | Fine steel wear, textile weave, leather pores, subtle gold age | Silver/blue value blocks, kite shield, celestial focal point |
| **Umbral** | Satin blackened steel and glassy obsidian against absorbent ash cloth | Fracture planes, smoke wear, offset plate edges, restrained glass transition | Fine ash fibers, cool metal scratches, sparse glass inclusions | Charcoal/aubergine separation, shield split, single violet fracture |

## Multi-angle model-sheet requirements

Create one dedicated landscape sheet per realm rather than compressing sixteen views into a single image.

Each realm sheet must include:

- Front, three-quarter, side, and back full-body views.
- Identical body proportions, face, hair, armor construction, equipment, and material placement across all views.
- Near-orthographic or long-lens presentation with minimal perspective distortion.
- A neutral modeling stance that reveals joints, modular seams, and load-bearing construction.
- Shield and weapon placement adjusted only as needed to expose the torso and back; equipment design must remain unchanged.
- Three or four unlabeled material close-ups or swatches showing the primary surface families and controlled magical response.
- Neutral studio lighting for material judgment, with no atmospheric effect hiding the forms.
- Safe padding around every view and no crop through the weapon, shield, feet, hair, or armor.
- A compact grayscale preview and a color preview at phone width.

The sheet is concept and modeling guidance. It must not imply that generated cross-view consistency is sufficient for production modeling without artist correction.

## Realm anchors

### Stonehold Vanguard

**Primary silhouette:** Broad, grounded, compressed, and defensive; the lowest visual center of gravity of the four.

**Construction:** Overlapping basalt and aged-iron plates with practical forge joins, square shoulder protection, a short heavy mantle, and visible repair bands.

**Materials:** Dark basalt, soot-aged iron, heavy brown leather, restrained copper or bronze fastening.

**Palette:** Charcoal, iron brown, ash, and a small forge-amber mineral accent.

**Equipment language:** Broad sword or compact hammer-like sword profile; thick rectangular or clipped-corner shield.

**Magic source:** Heat and mineral pressure escaping through a few protected seams near the weapon, shield boss, or chest fastening.

**Protected mobile cues:** Wide shoulders, layered plate rhythm, low shield mass, warm mineral seam.

**Avoid:** A generic dwarf costume, orange glow on every edge, excessive blockiness, or immobile armor.

### Eldergrove Vanguard

**Primary silhouette:** Tall, open, branching, and flowing; the most vertical and organically asymmetrical of the four.

**Construction:** Interlocked living-wood and weathered-bronze protection over woven fiber and layered cloth, shaped as grown reinforcement rather than leaves glued to armor.

**Materials:** Bark, dark woven fiber, weathered bronze, lichen, and small polished bone or crystal only where functional.

**Palette:** Deep green, bark umber, muted leaf gold, and a restrained living green-gold accent.

**Equipment language:** Leaf-tapered sword or short spear-like blade; curved shield grown around a strong central spine.

**Magic source:** Living repair and germination concentrated around one shoulder, shield spine, or weapon root.

**Protected mobile cues:** Vertical shoulder rhythm, curved shield edge, branching negative space, one living-growth focal point.

**Avoid:** Cute forest styling, uncontrolled foliage, antlers as a default shortcut, or bright green as the only realm cue.

### Crownlands Vanguard

**Primary silhouette:** Balanced, upright, heraldic, and authoritative; the clearest classical heroic read of the four.

**Construction:** Engineered plate with disciplined panel breaks, tailored textile layers, a tall but practical collar, and large heraldic surfaces reserved for approved symbols.

**Materials:** Aged silver and polished steel, royal-blue textile, dark leather, and restrained gold or brass engraving.

**Palette:** Steel, midnight royal blue, parchment, restrained gold, and a focused indigo-celestial accent.

**Equipment language:** Straight longsword and proportionate kite shield with a clean central field.

**Magic source:** Celestial authority focused through the shield center, weapon fuller, or a single chest emblem.

**Protected mobile cues:** Balanced shoulder line, kite shield, blue textile block, controlled precious-metal highlight.

**Avoid:** Generic pristine paladin armor, excessive gold, copied real-world heraldry, or decorative filigree that overwhelms the main plate shapes.

### Umbral Vanguard

**Primary silhouette:** Lean, narrow, fractured, and predatory; the sharpest negative spaces and most asymmetrical armor rhythm of the four.

**Construction:** Blackened steel and obsidian-like protection arranged around deliberate gaps, offset plate overlaps, and cloth that appears smoke-worn rather than weightless.

**Materials:** Smoked metal, obsidian, blackened leather, ash cloth, and a small glassy corruption surface.

**Palette:** Aubergine, charcoal, cold indigo, muted ash, and a restrained violet fracture accent.

**Equipment language:** Narrow angular sword and asymmetric shield with a readable broken-crescent or split-plane outline.

**Magic source:** Absorption or folding space localized to one fracture path, shield gap, or weapon channel.

**Protected mobile cues:** Narrow shoulders-to-weapon rhythm, asymmetric shield gap, fractured plate line, delayed violet focal trail.

**Avoid:** Featureless black, purple emission on every edge, gratuitous gore, excessive spikes, or visual disappearance in dark scenes.

## Direction-sheet composition

Create a landscape comparison sheet with four equal columns in this order:

1. Stonehold
2. Eldergrove
3. Crownlands
4. Umbral

Requirements:

- Full body, head to toe, all four Champions at identical scale.
- Near-orthographic or long-lens three-quarter presentation with minimal perspective distortion.
- Neutral grounded stance with weapons held safely at rest and shields visible.
- Dark neutral studio background with restrained medieval-gothic panel framing inspired by the approved app icon.
- One soft key light and controlled realm accent lighting; no deep shadows that conceal construction.
- No text, labels, logos, watermarks, environmental scenery, crowds, or unrelated props inside the generated visual.
- No helmet in the first comparison, so face scale and armor-to-person balance remain reviewable.

## Provisional mobile production envelope

These values inherit the provisional ceilings in `DESIGN.md` and must be validated on representative iOS hardware before production approval.

| Presentation | Triangle intent | Material slots | Texture intent | Protected result |
| --- | ---: | ---: | --- | --- |
| Inspection / PC-high LOD0 | Up to `60k` | Up to `3` | Up to `2K` primary packed sets | Face, material response, modular seams, signature craft |
| Mobile-high gameplay LOD1 | Approximately `30–36k` | Prefer `2` | `1K–2K`, measured by memory and camera | Full silhouette, face block, weapon/shield, realm material grouping |
| Mobile-normal gameplay LOD2 | Approximately `12–18k` | Prefer `1–2` | Usually `1K` packed | Primary silhouette, realm cue, attack origin, major value groups |
| Far combat / strategic LOD3 | Approximately `3–6k` or approved impostor | Prefer `1` | Shared/atlased where practical | Realm silhouette, role, allegiance, weapon direction |

Additional starting constraints:

- Begin under `90` deformation bones.
- Use no more than four bone influences per vertex and fewer where deformation allows.
- Prefer opaque materials; use alpha clipping selectively for hair and only when silhouette value justifies it.
- Use a short skinned mantle or rigid segmented cape for the anchor rather than full cloth simulation.
- Lower LODs should remove tertiary ornament, interior layers, small fasteners, secondary hair cards, and non-protected emission geometry first.
- Keep colliders and gameplay hit volumes separate from visual topology.
- Profile character creation, normal combat, and multi-character scenes on the lowest supported iPhone before converting these intentions into permanent budgets.

## Approval sequence

1. Compare the four black silhouettes at phone-preview size.
2. Approve or revise realm construction and material separation.
3. Approve the shared body, face visibility, and equipment proportions.
4. Select one realm for a full front/side/back/modeling sheet.
5. Validate that detailed sheet against the mobile production envelope.
6. Only then begin production mesh, rig, and modular-equipment work.

## Critical direction decisions

These decisions remain open and require project-owner approval before production multiplication:

- Champion body-type, age, gender-presentation, face, hair, and skin-tone ranges.
- Whether helmets are optional, class-dependent, or required in combat.
- Which realm becomes the first detailed production-model pilot.
- Exact realm palette tokens and approved heraldic symbols.
- Minimum supported iPhone and the final measured Champion budget for that device.
