# AnotherLife Production UI Token and Composition System

**Status:** Implemented production-system candidate; objective verification required before owner creative disposition

**System IDs:** `al.ui.production.v1`, `al.ui.hud.compositions.v1`

**Runtime assets:**

- `Assets/AL/Resources/UI/DesignSystem/AL_UI_ProductionDesignTokens.json`
- `Assets/AL/Resources/UI/DesignSystem/AL_UI_HudResponsiveCompositions.json`

**Runtime components:**

- `AL.UI.DesignSystem.UiProductionDesignTokens`
- `AL.UI.DesignSystem.HudResponsiveCompositionSet`
- `AL.UI.DesignSystem.HudCompositionPreviewRenderer`

## Authority boundary

This system defines presentation tokens, authored HUD slots, accessibility variants, and a reusable renderer. It does not define combat values, objectives, routes, party state, allegiance, realm ownership, map disclosure, rewards, or save data. Runtime features must project those values from their existing authoritative services and GameData catalogs.

The candidate is original AnotherLife work derived from the canonical mystical-medieval-naturalism and restrained-dark-fantasy-luxury direction. Comparator products informed only the quality questions in the governing benchmark. No comparator media, UI skin, layout, font, icon, trademark, or branded interaction was used as an implementation input.

The owner retains the creative disposition. The two named typography families are original commissioned-family specifications, not bundled font binaries. `HudCompositionPreviewRenderer` uses the existing shared font resolver only for deterministic composition review; it does not promote that fallback as shipping typography.

## Visual thesis: Ashen Reliquary

The UI reads as equipment made for a long war rather than ornament laid over a game:

- low-gloss carved stone and smoke-dark foundations;
- narrow brushed-electrum rails at hierarchy changes;
- leather or woven-fiber inserts only where touch, selection, or ownership benefits from tactile separation;
- smoke glass only for transient overlays that must preserve the world beneath;
- luminous realm glyphs as bounded identity seals, never full-panel glow;
- asymmetry for danger, stale state, and damage; measured symmetry for authority and confirmation;
- one primary read, one state read, then material detail.

Ornament must contract before type, hostile cues, target state, objectives, or touch size. Realm tint is supplied by approved runtime glyph assets and authority-backed realm identity. The neutral system does not invent final realm palette values.

## Typography

| Role | Family specification | Base size | Weight | Line height | Tracking | Use |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Display | AL Reliquary Display | 40 | 650 | 1.05 | 0.035 | rare screen or encounter heading |
| Title | AL Reliquary Display | 26 | 600 | 1.12 | 0.020 | panel and major state title |
| Body | AL Wayfarer Text | 16 | 450 | 1.40 | 0 | readable localized prose |
| Action | AL Wayfarer Text | 16 | 650 | 1.20 | 0.040 | verbs and immediate legal actions |
| Caption | AL Wayfarer Text | 13 | 550 | 1.25 | 0.045 | compact labels with a non-color cue |
| Numeric | AL Wayfarer Text Tabular | 18 | 650 | 1.00 | 0.010 | health, resource, timer, and count changes |

Rules:

- Text scales continuously from 85% to 200% within the selected authored composition.
- Type never shrinks below its approved role minimum to make content fit.
- At large text, secondary rows wrap, aggregate, or collapse before critical rows.
- All-caps is limited to short display, title, action, and caption labels.
- Numbers that change during combat use tabular metrics and stable alignment.
- Icon-only presentation is never the sole explanation of an unfamiliar action or state.
- The eventual commissioned family must cover every shipped locale and pass phone readability, diacritic, CJK, Arabic shaping, and fallback-transition review before owner approval.

## Icon and realm-glyph grammar

Original functional icons use a solid primary silhouette, one protected void, broad negative space, and a 2.25-unit optical stroke at the 24-unit master size. Each icon needs filled, outline, inverse, and micro variants. State must survive grayscale and bloom-off presentation.

Realm glyphs use the approved Arcane Axis white-alpha runtime exports. Consuming UI owns bounded tint and luminance. The mark remains a realm identity seal, not gameplay authority. Glyph treatment uses:

- 24% default glow opacity at the focal seal only;
- no continuous pulsing in standard or reduced-motion modes;
- no bloom-dependent edge definition;
- a physical socket, inlay, banner, or framed void beneath the luminance;
- shape, label, position, and material in addition to realm tint.

Do not use literal weapons, beasts, crowns, copied heraldry, pseudo-text, comparator symbols, or color-only allegiance.

## Material, spacing, size, and depth tokens

Spacing follows an authored 4-unit rhythm: `4, 8, 12, 16, 24, 32, 48, 64`.

- Minimum hit target: 56 reference units.
- Small corner radius: 3 units; reserved for compact insets and control tracks.
- Large corner radius: 8 units; reserved for transient or touch surfaces.
- Most authority and combat frames remain squared, not pill-shaped.
- Surface opacity: 92% for persistent plates.
- Persistent plates avoid stacked transparency.
- Elevation levels: `0, 2, 6, 12, 20`; elevation changes use edge contrast and restrained shadow, not panel-size inflation.
- Decorative rails are 1 unit; active/focus frames 2 units; hostile or warning state may use 2 units plus a shape cue.

## Semantic state treatments

Color is always paired with a frame, pattern, and label prefix.

| State | Non-color cue | Surface pattern | Prefix | Intent |
| --- | --- | --- | --- | --- |
| Neutral | double rail | brushed metal | em dash | stable information |
| Friendly | rounded shield | woven fiber | hollow diamond | ally or support relationship |
| Hostile | sawtooth frame | scored stone | double exclamation | actionable threat |
| Warning | diamond notch | diagonal cut | triangle | time-sensitive risk |
| Success | upward chevron | rising weave | filled triangle | confirmed positive result |
| Disabled | crossed bar | cross-hatch | multiplication mark | unavailable action |
| Stale | broken frame | interrupted grain | approximation mark | reconnecting or non-authoritative view |
| Focused | corner brackets | fine inlay | opposing corners | keyboard/controller/touch focus |

A state treatment never substitutes for copy that explains a blocking, stale, or failed condition.

## Motion, flash, and VFX

Standard motion communicates causality:

- focus transition: 140 ms;
- panel transition: 240 ms;
- focus confirmation hold: 420 ms;
- no decorative loop is required to understand state;
- flash opacity ceiling: 28%;
- VFX density scale: 100%.

Reduced variants are runtime-resolved from the same token asset:

- reduced motion: 40 ms focus, 60 ms panel, zero ambient motion, and a 550 ms static focus hold;
- reduced flash: maximum 8% flash opacity;
- reduced VFX: maximum 35% density;
- combined mode preserves every frame, pattern, prefix, label, slot, and authoritative value.

No accessibility mode changes information, target selection, route, objective, allegiance, timing, or legal action state.

## Fixed authored HUD hierarchy

All layouts contain exactly these reusable slots:

1. Hostile telegraphs — transparent world-cue layer; never a plate.
2. Current target — cast, defense, break, and actionable status.
3. Player vitals — resources, control state, and immediate readiness.
4. Objectives — owner, contest, progress, and timer.
5. Party/support — health, revive, and role-critical support state.
6. Route — next navigation anchor and route confidence.
7. Allegiance — realm/alliance identity and commander marker.

Secondary rewards, logs, ambient notices, chat, and decoration are not protected slots. They must collapse or queue before any listed slot. Child HUD components bind content into these slots without moving the authored composition at runtime.

## Protected central PvP scan path

Every composition declares `ProtectedScanPath` as a normalized safe-area rectangle. Persistent plates must not overlap it. `HostileTelegraphs` intentionally occupies the same bounds as a transparent world-cue layer so attacks, damage direction, target motion, and the legal action field remain visible.

| Form factor | Reference | Protected scan path `(x, y, w, h)` |
| --- | ---: | --- |
| Phone landscape | 2400×1080 | `0.28, 0.18, 0.44, 0.64` |
| Tablet landscape | 2732×2048 | `0.26, 0.16, 0.48, 0.68` |
| PC 16:9 | 1920×1080 | `0.27, 0.18, 0.46, 0.64` |
| PC ultrawide | 3440×1440 | `0.34, 0.18, 0.32, 0.64` |

The ultrawide layout keeps actionable HUD information inside a centered 16:9 reading band rather than pushing critical state to the physical corners. Phone and tablet reserve larger proportional world space and use touch-safe edge rails. The four signatures differ by authored slot coordinates; they are not scaled clones.

## Safe areas and composition selection

- Safe-area projection occurs before normalized slots are converted to pixels.
- Phone and tablet selection requires `touchPrimary=true`; desktop selection requires `touchPrimary=false`.
- Touch uses phone at aspect ratios of 1.8 or wider and tablet below 1.8.
- Desktop uses ultrawide at aspect ratios of 2.0 or wider and 16:9 below 2.0.
- Input mode may change focus and prompts but not the authoritative content available.
- Physical notches, cutouts, and overscan reduce the projection area; they never move a slot into the protected scan path.
- Requested text scale is clamped to the authored 85–200% range.

## Runtime use

```text
load UiProductionDesignTokens
load HudResponsiveCompositionSet
resolve composition from viewport and primary input class
project authored slots into the current safe area
resolve accessibility presentation
bind authoritative feature components into their fixed slot
```

`HudCompositionPreviewRenderer` creates a renderable hierarchy for deterministic review and integration tests. Production HUD components may replace the preview labels and surfaces while retaining the same slot transforms, state tokens, focus treatment, and protected scan path.

## Originality and provenance

- Author: Hermes Agent for AnotherLife.
- Date: 2026-09-01.
- Inputs: repository-owned governing documents and existing AnotherLife presentation code only.
- External visual, font, icon, screenshot, or comparator binary inputs: none.
- Generated imagery: none.
- Comparator reproduction: prohibited by contract and not present in the token or composition assets.
- Creative disposition: reserved for the project owner after objective rendering, accessibility, and gameplay-readability evidence.
