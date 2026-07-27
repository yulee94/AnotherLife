# Avian Soarer Visual Source

## Control

- Issue: `#259`
- Parent source: `tdf-eco-2026-07-27-v001`
- Child source: `tdf-eco-soarer-2026-07-27-v001`
- Primary Codex mode: `terrestrial-design`
- Generator: Codex built-in image generation
- Generator model/version: unavailable to the operator
- User creative approval: not requested
- Runtime integration: blocked

This packet turns three text-only supporting-fauna proposals into exact visual
review candidates. It is not a production rig, model, animation set, gameplay
definition, or runtime catalog.

## A2 Production Decision

| Profile | Realm | Source scale | Protected non-color read |
| --- | --- | ---: | --- |
| `tdf_fauna_stonehold_rimefan_kite` | Stonehold | `2.3` Champion-height wingspan | diamond wing, deep chest, short wedge tail |
| `tdf_fauna_crownlands_stormglass_swift` | Crownlands | `0.95` wingspan | continuous crescent, compact chest, rigid fork tail |
| `tdf_fauna_umbral_sootsail_carrioner` | Umbral | `2.0` wingspan | straight plank, hooded skull, long distal split tail |

Reuse is allowed for semantic keel/shoulder/elbow/wrist/pelvis/tail/fold/contact
controls, one opaque-feather micro-normal and packed roughness library, bounded
keratin grammar, flight-blockout/export conventions, LOD rules, contact
markers, and distant silhouette material.

Reuse is prohibited for final topology, proportions, wing outline, tail mesh,
skull, beak, foot, feather grouping, final clips/cadence, realm weathering, and
palette-only identity.

The existing `tdf_boss_crownlands_meridian_tempest_roc` `v002` sheet is a
no-reuse scale anchor only. Its eighteen-height span, long legs, seven
blade-primary order, shield skull, and separately rooted double tail fans are
excluded.

## Shared Anatomy Contract

- Exactly one feathered wing pair, two hind limbs, one head, and one tail.
- Avian keel, shoulder, elbow, wrist, pelvis, tail root, fold, and foot contact
  remain physically legible.
- Major silhouette feathers are geometry intent. Mobile identity cannot depend
  on strand feathers, transparent fuzz, emission, particles, weather, or color.
- Views preserve one individual, including damage, feather groups, head planes,
  toe layout, and tail root.
- Eyes remain small and adult. No mascot, heroic heraldry, costume, armor,
  saddle, rune, or faction marking.

## Rimefan Kite

- Visualized base: `rimefan_open_shelf`.
- Target: `2.3` span, `0.65–0.75` standing height, `0.90–1.05` length.
- Diamond wing with maximum chord at mid-span and a target of five broad outer
  primary groups per wing.
- Compact neck, deep insulated chest, short wedge tail, low keratin brow shelf,
  recessed eyes, and short broad three-forward/one-rear cliff feet.
- Blue-gray matte feathers, dark down, pale keratin, and iron-dust contact wear;
  no ice crystal or glow.
- Motion: ridge soar, two-step launch, corrective downstroke, hoverless hold,
  side brace, hard fold, recovery.
- Reduced motion removes feather flutter, snow, particles, and idle bob while
  preserving anticipation, direction, contact, and recovery.
- QA: `PassWithConcern`. The head remains raptor/owlish at some angles and
  five-group/wedge-tail consistency is incomplete. Production is blocked.
- `rimefan_gallery_edge` remains `ProposedTextOnly`.

## Stormglass Swift

- Visualized base: `stormglass_high_shelf`.
- Target: `0.95` span, `0.22–0.30` perched height, `0.30–0.38` length.
- Continuous high-aspect crescent with at most three terminal notches, compact
  chest, small beak, narrow feet, and one-root rigid fork.
- Charcoal feather mass, opaque desaturated metallic-blue edges, pale keratin,
  and rain-dark down; no lightning, static, glow, or transparency.
- Motion: three short acceleration strokes, pressure-line bank, stoop,
  fork-braking turn, shelf perch, wind brace, recovery.
- Reduced motion removes edge flutter, rain, static, and bob while preserving
  acceleration, direction, braking, contact, and recovery.
- QA: `ProvisionalPass`. Scale is illustrative and the long fork remains close
  to familiar swift/swallow anatomy. Measured orthographic source is required.
- `stormglass_calm_front` remains `ProposedTextOnly`.

## Sootsail Carrioner

- Visualized base: `sootsail_rift`.
- Target: `2.0` span, `0.70–0.85` grounded height, `1.0–1.2` length.
- Nearly straight plank wing with a target of four broad terminal groups, deep
  keel, low feathered hood, compact brow-nasal shield, short wedge beak, heavy
  two-forward/two-rear feet, and one tail root splitting distally.
- Matte charcoal plumage, pale facial keratin, glass-dark beak, and ash-worn
  legs; no naked skin, exposed ribs, smoke, or emission.
- Motion: thermal circle, side-slip, landing brace, ground mantle, three-step
  running launch, two forceful downstrokes, recovery glide.
- Reduced motion removes flutter, ash, debris, and pulsing while preserving
  load, direction, contact, and recovery.
- QA: `PassWithConcern`. Head originality, toe evidence, and four-group wing
  consistency remain production blockers.
- `sootsail_ravine` remains `ProposedTextOnly`.

## Readability And LOD Gate

The shared master shows distinct diamond, crescent, and plank spread
silhouettes, folded states, miniature review rows, and the Roc only as a much
larger outline. It is qualitative evidence, not a measured scale chart.

Coordination must later define reproducible 96 px, 64 px, provisional 100 m,
grayscale, color-vision-deficiency, and reduced-motion captures.

| Profile | Low LOD0 | Bones | Low textures | Materials | Balanced | High |
| --- | ---: | ---: | --- | ---: | ---: | ---: |
| Stormglass | `8k–10k` | `32–40` | one `1K` set | `1–2` | about `20k` | below `40k` |
| Rimefan | `14k–16k` | `44–56` | one `1K` set | `1–2` | about `30k` | below `55k` |
| Sootsail | `14k–16k` | `48–56` | one `1K` set | `1–2` | about `30k` | below `55k` |

Provisional reductions: LOD1 `55–65%`, LOD2 `20–30%`, distant `5–10%`
or silhouette proxy. Remove micro-feathers and secondary controls before
protected proportions. No per-feather simulation, transparent fringe, lights,
or permanent VFX are required.

## Source Storage Budget

All retained images are 1536 x 1024, 8-bit sRGB, opaque RGB:

| Class | Count | Actual bytes | Ceiling |
| --- | ---: | ---: | ---: |
| Final review sheets | `8` | `18,328,821` | `33,554,432` |
| Retained refinement inputs | `3` | `7,259,584` | `16,777,216` |
| Wave total | `11` | `25,588,405` | `50,331,648` |

Per-sheet compressed ceiling is `4,194,304` bytes. Four-sheet decoded review
ceiling is `25,165,824` bytes. Git LFS source stays under `unity/Docs`; layered
source is unavailable. Player, install, and runtime-resident impact is `0`.

## Explicit Non-Authority

This packet does not define lore, localization, quests, aggression, AI,
navigation, spawning, population, combat, stats, hit boxes, threat, loot,
rewards, production topology, skeleton, rig, clip, material, shader, VFX,
collider, prefab, scene, habitat, runtime catalog, Addressable, bundle,
streaming, save data, device floor, frame budget, or user approval.

## Next Handoff

User review must name this source version, profile IDs, base variants, and
exact hashes. Accepted profiles may then move to a separate coordination
specification. Engineering remains prohibited until that specification exists.
