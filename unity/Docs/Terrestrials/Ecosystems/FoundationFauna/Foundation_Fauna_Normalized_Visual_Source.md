# Foundation Fauna Normalized Visual Source

## Decision Record

- Issue: `#259`
- Normalization source: `tdf-foundation-fauna-normalization-2026-07-27-v001`
- Legacy source: `tdf-2026-07-15-v001`
- Parent ecosystem source: `tdf-eco-2026-07-27-v001`
- Primary mode: Codex terrestrial design
- Exact source state: `ReadyForUserReview`
- Overall QA: `PassWithConcern`
- User decision: `NotRequested`
- Runtime integration: `Blocked`

This packet keeps the three merged concept sheets immutable and supplies the
missing current review vocabulary: exact binary identity, protected non-color
shape, anatomy boundaries, scale, material hierarchy, motion intent, variant
state, quality reduction, and explicit production blocks.

## Cross-Family Separation

| Profile | Protected non-color identity | Primary habitat reference | Scale |
| --- | --- | --- | --- |
| `tdf_basalt_grazer` | `broad_shoulder_plate_low_head_short_weight_bearing_legs` | `tdf_habitat_stonehold_faultroad_escarpment` | shoulder `1.4`, length `3.0` Champion units |
| `tdf_grove_strider` | `long_neck_narrow_legs_leaf_like_ear_fins` | `tdf_habitat_eldergrove_hollowbark_oldgrowth` | head `1.8`, shoulder `1.4`, length `1.9` |
| `tdf_mire_lumenback` | `low_rounded_torso_paddle_feet_throat_pouch` | `tdf_habitat_eldergrove_mirrorroot_littoral` | shoulder `0.45`, length `1.2` |

No profile may borrow another profile's dominant mass, gait footprint, head
shape, or contact language. Color and surface treatment are secondary.

## Basalt Grazer

### Shape And Anatomy

- heavy, low quadruped with a broad shield shoulder;
- blunt wedge head held below the shell crown;
- short load-bearing limbs with broad four-toe contact;
- overlapping irregular mineral plates following shoulder and spine;
- short tapering tail mass, never an ankylosaur club;
- herbivore mouth block, never raptor beak or tusked predator;
- no saddle, straps, masonry, carved rune, or artificial armor cadence.

The current sheet clearly proves front, side, three-quarter, Champion scale,
black silhouette, and material breakup. Rear, top, underside, plate-root
section, and unobstructed limb origins remain unproved.

### Materials

| Role | Intent |
| --- | --- |
| primary plate | charcoal, rough, fractured mineral surface |
| underbody | warm ochre matte hide |
| foot/head keratin | dull worn horn |
| seam accent | sparse cool mineral inclusion, non-emissive by default |

The mineral seam cannot become a magical state indicator. Plate cracks must
not imply separate floating rocks or a golem.

### Motion Intent

Required future evidence: weighted idle, graze lowering, four-beat slow walk,
wide planted turn, alert head raise, heavy acceleration, deceleration, and
ground recovery. At least three feet remain planted through slow turns.

Reduced motion removes seam shimmer, dust, plate chatter, and idle micro-shake
while retaining mass transfer, direction, footfall, and stop.

### Variant State

`tdf_basalt_grazer_standard` is the pictured base. `ashen` and `mineral` remain
`ProposedTextOnly`; palette-only swaps cannot become approved production
variants without structural/material validation.

## Grove Strider

### Shape And Anatomy

- tall browsing quadruped with long arched neck and high negative leg space;
- narrow torso and split flexible hoof contact;
- paired lateral leaf-like ear fins rooted behind the jaw;
- bark-like dorsal ridges following anatomy rather than forming armor;
- restrained hanging tendrils concentrated at neck and tail;
- animal muzzle and eye placement, never humanoid face or decorative crown;
- no antlers, reins, saddle, faction ornament, or architecture graft.

The sheet proves front, side, three-quarter, Champion scale, black silhouette,
and surface families. Rear, top, underside, hoof mechanics, ear-fin roots, and
tendril attachment remain unproved.

### Materials

| Role | Intent |
| --- | --- |
| primary hide | matte gray-brown short hide |
| dorsal ridge | dry bark-like keratin |
| lichen | pale sparse surface colony |
| ear/tendril | flexible opaque leaf-like tissue |
| eye accent | restrained warm amber |

Green cannot carry family identity. Tendrils cannot read as hair, clothing, or
plant spell effects.

### Motion Intent

Required future evidence: cautious browse step, neck sweep, ear-fin alert,
look-back, narrow planted turn, gentle trot, stop, and group-spacing pause.
Long legs require clear diagonal support and hoof rollover.

Reduced motion freezes tendril flutter and leaf-fin oscillation while retaining
head direction, foot support, alert pose, and gait phase.

### Variant State

`tdf_grove_strider_standard` is the pictured base. `late_autumn` and `mist`
remain `ProposedTextOnly`; neither may be approved as a palette-only ecotype.

## Mire Lumenback

### Shape And Anatomy

- compact low amphibious quadruped with one continuous rounded back;
- broad paddle feet with splayed grounded digits;
- large bounded throat pouch below a shallow animal head;
- short tapering tail and low belly clearance;
- paired flexible facial feelers rooted beside the mouth;
- ring structures embedded in dorsal hide, never written glyphs;
- no shell, armor straps, weaponized whiskers, or translucent body requirement.

The sheet proves front, side, three-quarter, Champion scale, black silhouette,
feet, pouch, and material breakup. Rear, top, underside, pouch mechanics,
feeler roots, and swimming contact remain unproved.

### Materials

| Role | Intent |
| --- | --- |
| dorsal hide | dark peat, pebbled and wet |
| flank | muted blue-green opaque skin |
| throat pouch | clay-orange flexible skin |
| feet | dull rubbery dark skin |
| ring accent | pale cyan, readable as shape without emission |

The family must survive grayscale and emission-off review. Wetness is a bounded
roughness response, not a requirement for translucency or expensive layered
water shaders.

### Motion Intent

Required future evidence: throat breathing, low shuffle, short scramble,
alert freeze, nose-down forage, shallow-water push, turn, stop, and recovery.
Feelers react after head motion and never lead like combat tentacles.

Reduced motion removes ring pulsing, feeler vibration, splash particles, and
skin ripple while retaining pouch volume state, direction, contact, and stop.

### Variant State

`tdf_mire_lumenback_standard` is the pictured base. `clay` and `night` remain
`ProposedTextOnly`; `night` cannot depend on stronger emission for identity.

## Shared Production Envelope

These are provisional source targets, not runtime authorization.

| Profile | LOD0 skinned triangles | Deform bones | Materials | Core clips | Unique compressed target |
| --- | ---: | ---: | ---: | ---: | ---: |
| Basalt Grazer | `12k–16k` | `34–44` | `1–2` | `7–9` | `5–7 MiB` |
| Grove Strider | `10k–14k` | `40–52` | `1–2` | `7–9` | `5–7 MiB` |
| Mire Lumenback | `7k–10k` | `28–38` | `1–2` | `7–9` | `3–5 MiB` |

Shared rules:

- one `1K` color/normal/packed-mask set per low/mobile family;
- LOD1 target `55–60%`, LOD2 `20–25%`, distant `5–8%` or authored opaque
  proxy;
- one shared opaque-hide/keratin shader grammar, with emission optional and
  disabled at low tier;
- no required particles or dynamic lights;
- pool any later ambient instances and share contact/VFX families;
- variants reuse topology only when their proposed difference is real;
- no duplicate concept textures in Player content.

## Quality Reduction

Remove first:

1. mineral seam, lichen, ring emission, wet highlights, and micro-surface;
2. minor plate cracks, small tendrils, feeler segments, and distal toes;
3. secondary controls and nonessential idle motion.

Never remove:

- Grazer shoulder shield, low head, and short planted legs;
- Strider neck arc, leg negative space, and paired ear fins;
- Lumenback low dome, throat pouch, and paddle-foot footprint.

The current sheets are not measured distance tests. Future coordination must
set `96 px`, `64 px`, and `32 px` silhouette captures and supported camera
distances per family.

## Existing Asset Placement Risk

The exact sheets currently live under `unity/Assets`, use default texture
importers, and have mipmaps enabled. No project asset references their GUIDs,
so repository evidence indicates they are review source rather than used
runtime dependencies. This packet adds no new raster or importer change.

Before production, coordination/engineering must decide whether to relocate
the exact review sheets under `unity/Docs`, mark them Editor-only, or retain
them with an explicit build dependency audit. A2 does not claim an unmeasured
Player build exclusion.

## Non-Authority And Handoff

This packet does not decide player-facing names, lore, realm ownership, spawn
tables, temperament, hostility, collection, combat, stats, loot, quests,
population, AI, colliders, shaders, VFX, prefabs, scenes, saves, Addressables,
or builds.

After exact user approval, a separate Codex coordination/review specification
must define production topology, measured LOD thresholds, importer/placement
policy, streaming, pooling, memory, failure behavior, and acceptance captures.
