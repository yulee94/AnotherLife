# Four-Realm Heraldry — Arcane Axis

**Status:** Owner-approved visual source direction; vector construction and runtime implementation not yet approved

**Version:** 0.1

**Design contract:** Root `DESIGN.md`

**Asset category:** Realm identity / heraldry / interface and world marking

**Runtime priority:** Mobile-first, scalable to equipment, architecture, interface, VFX, and promotional presentation

**Approved visual anchor:** [Arcane Axis master sheet v001](../Heraldry/ConceptSheets/heraldry_four_realm_arcane_axis_master_v001.png)

**Generation record:** [Source prompts and provenance](../Heraldry/ConceptSheets/Heraldry_Source_Prompts_And_Provenance.md)

## Purpose

Establish one original abstract heraldic family for Stonehold, Eldergrove, Crownlands, and Umbral. These marks give every contributor a shared realm-identity source that can survive mobile UI scale while still supporting the engraved, material-rich medieval mysticism established by the app icon and root design contract.

This packet settles:

- The Arcane Axis family as the approved heraldic direction.
- The protected structural identity of all four realm marks.
- A shared rendered, flat, inverse, and micro application hierarchy.
- Mobile readability and simplification rules.
- Boundaries between approved visual source and later vector/runtime work.

It does not add realm gameplay authority, faction rules, class meaning, production vectors, final color tokens, individual runtime sprites, shaders, VFX, import automation, or engine integration.

## Owner decision record

- 2026-07-23: Project owner selected abstract heraldry instead of literal creature, crown, character, or weapon imagery.
- 2026-07-23: Project owner selected Direction B, **Arcane Axis Seals**, over the Monumental Bastion and Ancient Oath alternatives.
- 2026-07-23: Project owner approved the refined production-review sheet after flat, inverse, and micro variants were added and corrected.
- Approval locks the four core symbol identities, their shared arcane-geometric family relationship, their realm order, and the application hierarchy shown in the approved master sheet.
- Approval does not lock raster-generation artifacts, exact future vector control points, exact pixel spacing, final sRGB values, texture maps, material response, animation, shader behavior, or runtime budgets.

The approved master is visual authority. Future vector reconstruction must preserve its protected geometry while correcting optical spacing and grid fit; it must not mechanically auto-trace incidental raster irregularities.

## Family language

Arcane Axis marks are constructed from:

- One strong center or protected void.
- Broad circular, orbital, orthogonal, or meridian structures.
- A small number of deliberate breaks that create recognizable negative space.
- A centered or controlled off-axis magical focal point.
- Consistent visual weight across all four realms without forcing identical symmetry.

The family should feel as though realm craft traditions interpret one older magical geometry. It must remain original and must not reproduce living religious, political, military, or real-world heraldic symbols.

### Shared rules

- Use abstract geometry rather than literal animals, people, crowns, weapons, buildings, trees, leaves, or flowers.
- Preserve a single-color silhouette at every supported size.
- Keep the mark identifiable without glow, texture, bevel, animation, or realm color.
- Use material treatment outside the core flat geometry; engraving must never repair a weak silhouette.
- Maintain comparable optical size and stroke mass across the four marks.
- Reserve detailed orbital nodes, surface wear, and magical bloom for larger ceremonial treatments.

## Protected realm identities

### Stonehold — Tectonic Axis

**Core construction:** Four compressed orthogonal strata blocks organized around a circular center, crossed by stable cardinal axes.

**Protected read:** Square, weighty, mechanically joined, and immovable, with an ember-like center.

**May simplify:** Interior bevels, secondary inset lines, stone/metal texture, center glow, and the smallest internal corner breaks.

**Must preserve:** Four-part orthogonal mass, the cardinal axis, central circular opening, and squared overall footprint.

**Avoid:** A fortress floor plan, letterform, directional navigation icon, or generic medical/plus symbol.

### Eldergrove — Living Orbit

**Core construction:** Three asymmetric growth arcs orbiting and protecting a seed-shaped central void.

**Protected read:** Rotational, renewing, open, and organic without becoming a literal plant.

**May simplify:** Secondary contour lines, metal edging, surface patina, tiny outer points, and focal glow.

**Must preserve:** Three-part rotation, protected seed void, uneven organic cadence, and circular overall flow.

**Avoid:** Leaves, flowers, trees, recycling symbols, pinwheels, or a generic nature-app logo.

### Crownlands — Celestial Meridian

**Core construction:** A balanced four-point celestial meridian held within open orbital arcs and a centered diamond-like focal void.

**Protected read:** Ordered, ascending, measured, and authoritative.

**May simplify:** Fine orbital divisions, small ceremonial orbit nodes, material split, inner bevels, and glow.

**Must preserve:** Four-point meridian, open circular frame, centered focal void, and vertical authority.

**Avoid:** A literal crown, church or compass copy, weapon silhouette, generic star badge, or borrowed royal heraldry.

### Umbral — Severed Eclipse

**Core construction:** An offset eclipse ring crossed by a bold diagonal absence, supported by broad broken orbit arcs.

**Protected read:** Interrupted, spatially displaced, controlled, and dangerous without becoming chaotic.

**May simplify:** Ceremonial satellites, detached stars, fine orbit lines, glass texture, and violet bloom. These details are omitted from flat and micro variants.

**Must preserve:** Offset eclipse, diagonal void, two broad broken arcs, and circular overall footprint.

**Avoid:** A prohibition sign, generic planet icon, letterform, copied occult mark, or featureless purple circle.

## Application hierarchy

### Ceremonial render — approximately `128 px` and above

- May use physical metal, stone, patina, controlled bevel, surface history, and one restrained magical focal point.
- May include approved secondary orbit nodes where they do not change the core mark.
- Intended for title cards, banners, realm selection, shield presentation, architecture, major map panels, and marketing.
- Must still reduce cleanly to the flat master.

### Standard flat mark — approximately `48–127 px`

- One solid realm color or one neutral material color.
- No texture, bevel, glow, particle halo, hairline engraving, or detached micro-ornament.
- Intended for ordinary UI, equipment stamps, map markers, affiliation plates, and medium-distance world signage.

### Inverse mark

- Exact flat geometry rendered as one light value on a dark field or one dark value on a light field.
- Interior and exterior negative spaces must remain open.
- Inversion changes value only; it must not redraw the symbol.

### Micro mark — approximately `24–47 px`

- Use the protected core geometry only.
- Remove optional nodes, stars, hairline arcs, double contours, engraving, and glow.
- Maintain at least one clear center and two or three broad outer masses.
- Test on circular and square UI-token boundaries without allowing the container to become part of the symbol.

These sizes are starting guidance, not measured runtime budgets. Final masters require device review at the actual UI scale and display density.

## Mobile-first construction rules

- Build deterministic vector masters manually; do not ship an automatic raster trace.
- Begin from the `24 px` and `32 px` micro grids, then expand upward instead of shrinking a detailed ceremonial mark blindly.
- Target a minimum open gap of roughly `2 px` in the `32 px` master. Optical corrections may exceed the mathematical grid where necessary.
- Preserve readability in grayscale and when realm colors are indistinguishable.
- Prefer broad negative spaces over thin interior strokes.
- Keep all four marks at comparable optical area, not merely equal bounding-box dimensions.
- Allow separate ceremonial and micro vector masters when simplification is documented and protected geometry remains unchanged.
- Avoid full-surface emission, blur-dependent edges, transparency-dependent identity, animated noise, and particles as required recognition cues.
- Profile final imported sprites, atlases, mip behavior, compression, overdraw, and memory on the lowest supported iPhone before runtime approval.

## Realm color and material intent

Exact color tokens remain open. The approved source establishes relationships:

| Realm | Large rendered treatment | Flat intent | Magical focal intent |
| --- | --- | --- | --- |
| **Stonehold** | Dark steel, charcoal stone, restrained aged gold | Iron gray or forge-neutral | Small ember amber center |
| **Eldergrove** | Aged bronze, deep moss, restrained gold edge | Muted moss or weathered bronze | Small living green center |
| **Crownlands** | Pale gold, controlled silver, deep blue | Restrained gold or pale silver | Small blue-white center |
| **Umbral** | Blackened silver, aubergine, glass-dark center | Muted violet or cool dark neutral | Restrained violet center |

Color supports recognition but never replaces geometry.

## Approved uses

- Realm selection and allegiance presentation.
- Champion shield, armor, cape, clasp, or equipment stamps.
- Banners, gates, roads, ruins, institutions, and realm-controlled architecture.
- Map markers, quest affiliation, territory plates, realm tabs, and notification categories.
- Controlled spell, portal, oath, and VFX framing after a separate runtime handoff.
- Marketing and social presentation when the realm identity is relevant.

## Prohibited authority and misuse

- The marks do not define gameplay stats, morality, religion, class, race, faction hostility, quest outcomes, or mechanical ownership.
- Do not assign a mark to a runtime realm ID until coordination and engineering validate the catalog authority.
- Do not create house, guild, class, or boss variants by silently changing the core realm geometry.
- Do not combine the marks into a new fifth authority symbol without user approval.
- Do not use living cultural, political, military, or religious symbols as ornamental additions.
- Do not replace the approved abstract marks with literal mascots or crowns.

## Acceptance criteria for this source packet

- One owner-approved master sheet is retained in the repository.
- Stonehold, Eldergrove, Crownlands, and Umbral appear in the declared order.
- Each mark is structurally distinct while clearly belonging to the Arcane Axis family.
- Each mark has rendered, flat, inverse, and micro treatment guidance.
- Flat and micro identity survives without color, texture, bevel, or glow.
- Literal animals, crowns, weapons, characters, and botanical icons are excluded.
- Source prompts, reference roles, checksum, owner decision, and visual-verdict history are retained.
- Runtime, vector, color-token, and performance authority remain explicitly deferred.

## Production handoff sequence

1. Reconstruct deterministic vector masters for the four marks.
2. Produce aligned `24`, `32`, `48`, `64`, `128`, and `256 px` review exports in color, grayscale, and inverse.
3. Compare optical weight and protected geometry across the family.
4. Obtain user approval for the vector reconstruction and final color relationships.
5. Define Unity sprite/atlas/import requirements and realm-catalog mapping in a separate coordination handoff.
6. Implement and profile the approved assets in Codex engineering mode.

## Critical direction decisions still open

- Exact vector control points, corner radii, orbit thickness, gap sizes, and optical corrections.
- Final realm color tokens and accessibility alternatives.
- Whether ceremonial Crownlands orbit nodes remain in every large-scale use.
- Whether ceremonial Umbral satellites remain outside major realm-selection and architecture presentation.
- The first runtime surface: realm selection UI, Champion equipment, banners/architecture, or map markers.
- Formal trademark and emblem-conflict clearance before commercial lock.
- Minimum supported iPhone and measured atlas, memory, and overdraw budgets.
