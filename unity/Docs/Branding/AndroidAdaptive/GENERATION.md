# Android Adaptive Icon Generation Record

**Date:** 2026-07-23

**Tool:** OpenAI built-in image generation

**Edit authority:** Approved `App_Icon_Mystic_Medieval_AL.png`

**Post-process:** OpenAI image-generation chroma-key removal helper, then deterministic crop, scale, alpha composition, monochrome derivation, and mask-preview export with Pillow

## Foreground prompt

```text
Use case: background-extraction
Asset type: Android adaptive launcher icon foreground layer, square 1024 x 1024 source
Input image: Image 1 is the edit target and approved brand authority.
Primary request: Isolate the exact central engraved metallic monogram reading "AL" from Image 1. Preserve the recognizable A and L letterforms, their silver-and-gold medieval engraving, beveled metal, filigree, proportions, and left/right relationship as faithfully as possible. Remove everything else: no Gothic arch, no stone, no circular frame, no runes, no vines, no stars, no compass ornament, no background scenery.
Composition: Center the complete AL monogram. Its full bounding box must fit comfortably within the middle 58 percent of the canvas in both width and height, with generous even padding on every side. The monogram must not touch or approach the outer 18 percent border; it must remain legible when a circle, squircle, rounded square, or teardrop launcher mask is applied.
Backdrop: perfectly flat uniform solid #ff00ff chroma-key background for later removal. The background must contain no shadows, gradients, texture, reflections, floor plane, glow, haze, or lighting variation.
Text: exactly "AL". Do not change it to AI, A1, ΛL, or any other characters. No extra text.
Constraints: preserve the approved monogram identity; crisp clean edges; no cast shadow; no contact shadow; no outer glow; no watermark; no added ornament outside the letterforms; do not use #ff00ff anywhere in the monogram.
```

## Background prompt

```text
Use case: precise-object-edit
Asset type: Android adaptive launcher icon background layer, square full-bleed 1024 x 1024 source
Input image: Image 1 is the approved brand and style reference. It is not the desired final composition.
Primary request: Create a separate full-bleed medieval-mystic background layer derived from the atmosphere and materials of Image 1. Remove the AL monogram and every literal letter. Remove the large Gothic arch, compass ornament, border, pillars, vines, and any foreground emblem. Replace them with an abstract midnight-indigo celestial field embedded in subtly carved dark stone and restrained antique-gold arcane filigree traces.
Composition: seamless full-bleed square. Keep the central 66 percent calm, dark, and readable behind a metallic foreground logo. Place only subtle radial geometry, faint runic arcs, controlled violet starlight, and stone texture. Important details must continue beyond all edges so circle, squircle, rounded-square, and teardrop launcher masks still look intentional. No hard frame and no edge-dependent ornament.
Lighting and palette: deep navy, midnight indigo, charcoal stone, restrained antique gold, small controlled celestial violet accents. Rich but not noisy. Slightly brighter behind center for contrast, darker toward edges, without a visible spotlight circle.
Constraints: opaque background; no transparency; no letters; no AL; no readable text; no monogram; no central crest; no character; no creature; no weapon; no watermark; no app-icon mask baked into the artwork; no rounded corners; no cast shadow.
```

## Source stages

| File | Purpose |
| --- | --- |
| `Sources/App_Icon_Android_Adaptive_Foreground_AL_Chroma_Source_1254_v001.png` | Immediate generated foreground source with flat chroma background |
| `Sources/App_Icon_Android_Adaptive_Foreground_AL_Alpha_Source_1254_v001.png` | Chroma-removed transparent source |
| `Sources/App_Icon_Android_Adaptive_Background_Source_1254_v001.png` | Immediate generated full-bleed background source |

The generated service returned `1254 × 1254` sources. The runtime files are deterministic `432 × 432` derivatives. The foreground is alpha-trimmed, scaled to a maximum visible dimension of `258` pixels, and centered on a transparent canvas. The monochrome source reuses the exact foreground alpha and replaces visible RGB with white.

## Chroma removal

```text
remove_chroma_key.py
--auto-key border
--soft-matte
--transparent-threshold 12
--opaque-threshold 220
--despill
```

Detected border key: `#f50ded`.

## Source integrity

```text
93c41795a62c53358a50d3c158f1e6e1b1116b95c7eca5c122c9b97488fe0569  Sources/App_Icon_Android_Adaptive_Background_Source_1254_v001.png
13c6a6bb812acadacfa8a9c028eb9c13829314fccc5093db26ea3390eb987116  Sources/App_Icon_Android_Adaptive_Foreground_AL_Alpha_Source_1254_v001.png
639299351ee2a8666b4e694495a582cdaec80cf9f1a1d65bd235557c5143f773  Sources/App_Icon_Android_Adaptive_Foreground_AL_Chroma_Source_1254_v001.png
```
