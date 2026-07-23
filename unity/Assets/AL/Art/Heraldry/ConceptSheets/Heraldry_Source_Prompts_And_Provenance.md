# Four-Realm Heraldry — Source Prompts and Provenance

## Shared status

- Generated: 2026-07-23
- Tool path: Codex built-in image generation
- Source type: AI-assisted visual-direction proposal
- Selected direction: Direction B — Arcane Axis Seals
- Owner status: Direction B and the refined production-review master sheet approved on 2026-07-23
- Design authority: Root `DESIGN.md` and [`FourRealmHeraldry.md`](../../Designs/FourRealmHeraldry.md)
- Realm presentation reference: [`champion_four_realm_anchor_v001.png`](../../Champions/ConceptSheets/champion_four_realm_anchor_v001.png)
- External reference: owner-supplied approved `App_icon.png`, used for medieval-mystic finish and presentation only
- Runtime authority: None. This image does not provide deterministic vector paths, individual sprites, catalog mappings, import settings, shaders, VFX, atlas budgets, or measured mobile performance.

## Selected file and validation

| File | Dimensions | Alpha | SHA-256 | Visual verdict |
| --- | ---: | --- | --- | ---: |
| `heraldry_four_realm_arcane_axis_master_v001.png` | `1254 × 1254` | No | `a76f3a315952799080046aff4beb349501f9cf056b8f15a58effb04f99c37158` | `95 / 100` |

Manual `390 × 390` sheet review passed in color and grayscale. All four large identities and their flat/inverse comparisons remain distinct. The embedded micro examples remain directionally readable, but individual vector exports still require exact `24 px` and `32 px` grid validation before runtime approval.

The final verdict passed after:

- Correcting the Stonehold inverse treatment so it preserves the same negative-space geometry.
- Removing Umbral satellite dots and small detached ornaments from flat, inverse, and micro variants.
- Reducing Umbral's micro treatment to the offset eclipse, diagonal absence, and broad broken arcs.
- Confirming consistent realm order, optical footprint, and mobile-readable test hierarchy.

Remaining vector-stage notes:

- Open Stonehold's smallest inner corners slightly in the `24 px` master.
- Enlarge Eldergrove's central seed void slightly in the `24 px` master.
- Treat the master sheet as protected visual identity, not as an automatic tracing source.

## Direction-selection record

Three coordinated abstract families were compared:

1. **Monumental Bastion Glyphs** — strongest architectural and shield mass.
2. **Arcane Axis Seals** — strongest shared magical geometry and cross-application flexibility.
3. **Ancient Oath Marks** — strongest archaeological and ruin presentation.

The project owner selected Arcane Axis Seals. The unselected previews are not retained as competing repository authority.

## Selected direction generation prompt

```text
Use case: logo-brand
Asset type: Another Life mobile-game four-realm heraldry concept sheet, preview direction B — ARCANE AXIS SEALS
Input images: Image 1 is the approved app-icon style reference for medieval mysticism, dark navy stone, restrained gold filigree, and violet arcane light. Image 2 is the approved Champion realm-order and palette reference: Stonehold, Eldergrove, Crownlands, Umbral.
Primary request: create exactly four original abstract realm sigils as a coordinated family built from circles, axes, voids, and interlocking sacred geometry. Arrange a clean 2x2 grid: top-left Stonehold, top-right Eldergrove, bottom-left Crownlands, bottom-right Umbral. No written labels.
Symbol concepts: Stonehold is a square tectonic knot with a stable central ember void and four compressed strata blocks; Eldergrove is three asymmetric growth-arcs orbiting a seed-shaped void, abstract and non-botanical; Crownlands is a precise celestial meridian made from a four-point star, two balanced orbital arcs, and an open upward axis; Umbral is an offset eclipse ring crossed by a diagonal void, with two broken orbit fragments that imply spatial distortion.
Style/medium: premium medieval-fantasy arcane seal design, vector-friendly flat construction, medium-weight geometry, elegant negative space, less architectural than Direction A, restrained beveled-metal presentation inspired by the approved references. Dark midnight stone background with sparse thin geometric construction lines.
Color palette: Stonehold dark steel and ember amber; Eldergrove antiqued bronze and deep moss with one green focal light; Crownlands silver and pale gold with deep blue; Umbral blackened silver and violet.
Composition/framing: four equal large seal fields with generous separation. Beneath each large rendered seal, include one small flat single-color silhouette of the exact same mark at mobile-icon scale. Normalize all small stamps to the same optical footprint and weight.
Constraints: exactly four main seals and four matching small stamps; one coordinated design language; readable at 24–32 px; no literal animals, people, faces, crowns, weapons, shields, trees, leaves, flowers, letters, words, pseudo-text, runic inscriptions, or trademarks; no extra icons; no watermark. Avoid generic elemental icons and avoid copying real-world occult or religious symbols. Keep every core mark simple enough for clean vector reconstruction.
```

Initial selected-direction visual verdict: `92 / 100`. It passed as a direction proposal, with Crownlands orbit thickness and Umbral micro detail reserved for production simplification.

## Production-review master prompt

```text
Use case: precise-object-edit
Asset type: Another Life selected four-realm Arcane Axis heraldry production-review master sheet
Input images: Image 1 is the selected Direction B edit target and exact symbol-identity reference. Image 2 is the approved medieval-mystic app-icon finish reference. Image 3 is the approved realm palette and realm-order reference.
Primary request: refine Image 1 into a cleaner production-review sheet while preserving the exact core identity and geometry of all four selected sigils. Maintain the 2x2 order: top-left Stonehold, top-right Eldergrove, bottom-left Crownlands, bottom-right Umbral. No written labels.
Layout inside every quadrant: one large premium rendered metal sigil centered in the upper portion; directly below it, three evenly spaced tests of that exact same geometry: (1) flat realm-color silhouette on dark, (2) solid white silhouette inside a plain black square showing inverse readability, (3) a very small simplified micro-mark inside a neutral circular UI-token boundary showing the intended 24–32 px read. Keep all four quadrants consistent and aligned.
Targeted production refinements: preserve Stonehold and Eldergrove geometry. Broaden Crownlands' thinnest orbit arcs and central negative spaces without changing its four-point celestial-meridian identity. Preserve Umbral's offset eclipse and diagonal void, but remove the two tiny satellite dots from its flat and micro variants; retain at most the major broken orbit fragments. Normalize every flat and micro variant to the same optical height, footprint, and stroke mass.
Style/medium: premium medieval-mystic heraldry source sheet, vector-reconstructable marks, clean presentation, restrained beveled metal and glow only on the large versions. Dark midnight stone backdrop, subtle gold grid dividers, no decorative clutter.
Color palette: Stonehold dark steel with amber; Eldergrove aged bronze and deep moss with green; Crownlands silver and pale gold with blue-white; Umbral blackened silver with violet.
Constraints: exactly four realm systems; every variant within a quadrant must visibly match its large sigil; no words, labels, letters, pseudo-text, animals, people, faces, crowns, weapons, shields, buildings, trees, leaves, flowers, runic inscriptions, real-world occult/religious symbols, trademarks, extra icons, or watermark. Flat, inverse, and micro tests must contain no bevel, texture, glow, or fine engraving. Keep all marks readable and clearly distinct at mobile size. Change only the layout and listed production refinements; do not redesign the selected core symbols.
```

The first production-review result scored `87 / 100` and required correction because the Stonehold inverse treatment and Umbral micro variants did not yet preserve the approved flat identity.

## Final correction prompt

```text
Use case: precise-object-edit
Asset type: Another Life selected Arcane Axis heraldry production-review master sheet
Input images: Image 1 is the edit target. Image 2 is the selected Direction B geometry reference.
Primary request: preserve Image 1's complete 2x2 layout, every large rendered sigil, realm order, background, divider grid, colors, spacing, and all variants except for the exact corrections below.
Correction 1 — Stonehold inverse test: in the top-left quadrant, change only the middle test inside the black square. It must be the exact same Stonehold line-and-block geometry as the flat gray test immediately to its left, rendered entirely as a clean solid white symbol on pure black. Preserve all interior and exterior negative spaces as black. Do not create a white background tile and do not fill the four surrounding square voids.
Correction 2 — Umbral non-rendered variants: in the bottom-right quadrant, remove both tiny satellite dots, their tiny rings, and any tiny star ornaments from all three tests beneath the large rendered crest. The flat realm-color mark, white-on-black inverse mark, and circular micro mark must use the same simplified geometry: one offset eclipse ring, one bold diagonal void/slash, and two broad broken orbit arcs only. No dots or small detached ornaments.
Correction 3 — Umbral micro simplification: make the far-right circular micro mark optically equal in size and stroke mass to the other three micro marks, with generous negative space and no hairline arcs.
Constraints: change only these test variants; keep all four large rendered sigils completely unchanged. Preserve all other variants and layout. Exactly four quadrants. No text, labels, letters, extra icons, or watermark.
```

## Final visual verdict

```json
{
  "score": 95,
  "verdict": "pass",
  "category_match": true,
  "differences": [
    "Stonehold remains the densest micro mark in the family.",
    "Eldergrove's central seed void is slightly smaller in the micro treatment than in the large and flat treatments."
  ],
  "suggestions": [
    "During vector construction, open Stonehold's inner corners by one grid unit for the 24 px master.",
    "During vector construction, enlarge Eldergrove's central seed void slightly in the micro master."
  ],
  "reasoning": "The selected Arcane Axis family now preserves identity across rendered, flat, inverse, and micro applications, with strong realm distinction and mobile readability."
}
```

## Production cautions

- The raster sheet is a direction and identity source, not a deterministic vector master.
- Flat and inverse marks must be reconstructed from protected geometry, not separated from this raster and shipped unchanged.
- The circular micro containers are presentation tests and are not part of the realm marks.
- Large ceremonial material, glow, and orbit-node treatment may not be required in ordinary UI.
- Final sprite sizes, atlas grouping, compression, memory, mip behavior, and overdraw require representative iPhone profiling.
- Realm IDs and catalog mappings require a separate coordination and engineering handoff.
- No formal trademark or emblem-conflict search has been performed; commercial clearance remains a separate pre-release gate.
