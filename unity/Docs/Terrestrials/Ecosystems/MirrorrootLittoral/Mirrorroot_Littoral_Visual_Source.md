# Mirrorroot Littoral Visual Source

## Decision Record

- Issue: `#259`
- Source version: `tdf-eco-mirrorroot-2026-07-27-v001`
- Parent roster: `tdf-eco-2026-07-27-v001`
- Foundation normalization:
  `tdf-foundation-fauna-normalization-2026-07-27-v001`
- Moonroot boundary source: `tdf-eco-moonroot-2026-07-27-v001`
- Primary mode: Codex terrestrial design
- Habitat: `tdf_habitat_eldergrove_mirrorroot_littoral`
- Environment kit: `tdf_envkit_eldergrove_mirrorroot_littoral`
- Placement fauna: `tdf_mire_lumenback`
- Habitat state: `ReadyForUserReview`
- Mire Lumenback placement evidence: `ReadyForUserReview`
- Overall QA: `PassWithConcern`
- User decision: `NotRequested`
- Runtime integration: `Blocked`

This packet pairs one bounded Eldergrove shoreline kit with an immutable
existing fauna identity. It provides appearance, qualitative scale/contact,
shoreline-depth and transition intent, material hierarchy, quality-reduction
intent, and reuse constraints without granting gameplay or runtime authority.

## Protected Habitat Identity

The parent source's exact non-color identity is:

`shoreline_shallow_deep_value_root_shelf`

The visual source protects these elements together:

1. one low lake horizon with a distant broken tree line;
2. an irregular shore edge rather than a straight or constructed boundary;
3. dry/rising shore, pale safe-shallow shelf, and darker deep-water bands;
4. flooded root shelves and root-island interruptions;
5. mud fans and pale water-worn stone that clarify contact and depth;
6. sparse reed/leaf clusters with open shoreline sight gaps;
7. stable grayscale depth separation when reflection and motion are disabled.

Color, cyan light, specular highlights, reflection, ripple, caustics, fog,
particles, reed motion, and dynamic light cannot carry safe-shallow, deep,
route-like gap, or habitat identity.

## Landform And Composition

- Review composition unit: one provisional `128 m × 128 m` cell.
- Primary structure: low lake edge broken by flooded root shelves and mud fans.
- Dry band: gently rising exposed soil and sparse grassward growth.
- Shallow band: pale submerged stones, visible mud, exposed root contact, and
  bounded clear-water value.
- Deep band: darker stable water value broken by a few low root islands.
- Horizon: water plane below a distant irregular tree line.
- Organic dressing: sparse reeds, submerged stems, broad low leaves, algae
  film, and restrained floating growth.
- Required particles: `0`.
- Required dynamic or shadowed lights: `0`.
- Required emission families: `0`.
- Required active water-surface families: `1`.

Straight seawalls, docks, bridges, stairs, piers, boats, carved shelves,
symmetrical root gates, dense reed walls, fantasy fog banks, and luminous
depth outlines are rejected.

The parent allows one natural landmark family but defines no stable Mirrorroot
landmark ID. This packet does not invent one. Flooded root shelves and the low
shoreline remain compositional anchors rather than named runtime landmarks.

## Materials

| Role | Review color | Requirement |
| --- | --- | --- |
| wet root bark | `#34332E` | rough charcoal-brown, restrained wet response |
| mud fan | `#4B4035` | matte wet umber with stable silhouette edge |
| water-worn stone | `#9A9689` | pale gray-beige, readable below static water |
| reed | `#75684A` | sparse opaque tan groups |
| shallow water | `#69766F` | muted silver-green value, bounded reflection |
| deep water | `#39454A` | darker stable value, never magical blue |
| algae film | `#5A5B3E` | sparse dull olive surface breakup |
| broad leaf | `#4B503B` | opaque restrained green, no transparency identity |

The parent material families
`tdf_matfam_eldergrove_wet_bark_root` and `tdf_matfam_wet_hide` remain
authoritative. Water depth and Lumenback identity must survive grayscale,
reflection-off, and emission-off review.

## Neighbor Transition Evidence

Moonroot shared boundary —
`tdf_transition_eldergrove_moonroot_floodbasin_to_mirrorroot_littoral`
is reused from the Moonroot companion:

- the deep channel broadens and becomes a lower open water horizon;
- root islands reduce into flooded shelves and sparse reed interruptions;
- submerged pale stone becomes more continuous in the shallow margin;
- overhead root-canopy compression gives way to reflected open sky.

Sunmane shared boundary — no stable transition ID exists in the current
parent source, so this packet does not mint one:

- the shore rises through exposed mud fans into drier soil;
- reeds, submerged stems, and floating growth reduce;
- low grass, long sightline pockets, and warm dry values increase;
- water remains behind as a low orientation plane rather than a route marker.

The generated spatial sheet suggests both directions by landform and value,
but proves neither named neighbor identity: the rising shore lacks decisive
Sunmane meadow character and long sightline evidence, while the deeper side
lacks decisive Moonroot high canopy, broad channels, and root-island evidence.
It does not label or measure either transition. These are appearance
constraints only, not zone boundaries, routes, navmesh, swimming space,
streaming cells, encounters, or spawn rules.

## Environment Kit

Exactly eight proposed natural families:

1. `tdf_prop_eldergrove_mirrorroot_flooded_root_shelf`;
2. `tdf_prop_eldergrove_mirrorroot_shallow_mud_fan`;
3. `tdf_prop_eldergrove_mirrorroot_sparse_reed_island`;
4. `tdf_prop_eldergrove_mirrorroot_waterworn_stone_cluster`;
5. `tdf_prop_eldergrove_mirrorroot_submerged_stem_cluster`;
6. `tdf_prop_eldergrove_mirrorroot_broad_low_leaf_clump`;
7. `tdf_prop_eldergrove_mirrorroot_floating_growth_cluster`;
8. `tdf_prop_eldergrove_mirrorroot_distant_shoreline_proxy`.

Root shelf, mud fan, stone, and organic families should share bounded
materials, atlases, masks, and static-water contact grammar. Silhouette
variation comes from authored asymmetry, approved rotation/scale bounds, wet
contact, and sparse grouping—not one unique material per object.

## Mire Lumenback Placement

The exact creature identity remains governed by
`unity/Docs/Terrestrials/Ecosystems/FoundationFauna/`.

Placement evidence protects:

- shoulder height `0.45` and length `1.2` Champion units;
- one continuous low rounded torso and low belly clearance;
- broad paddle feet with splayed grounded digits;
- large bounded clay-orange throat pouch beneath a shallow animal head;
- short tapering tail and paired mouth-rooted flexible feelers;
- embedded dorsal ring structures that remain readable with emission off;
- dark peat pebbled hide, opaque muted blue-green flank, and stable contact.

Generated Lumenbacks are representative placement redraws only. Their bright
cyan ring contrast can read emissive even without painted light spill, and
some distant specimens lose pouch, foot, feeler, or exact ring detail. The
canonical sheet and normalized values remain authoritative wherever a
generated specimen differs. Every pictured animal is a perspective view of
the same standard adult; apparent size differences establish no age, sex,
life stage, population role, or variant.

No sheet establishes swimming anatomy, buoyancy, population, spawn density,
age, juvenile state, group behavior, temperament, hostility, domestication,
rewards, habitat exclusivity, or the unpictured `clay` and `night` variants.
`night` cannot derive identity from stronger emission.

## Motion Intent Retained, Not Proved

Future motion evidence must cover throat breathing, low shuffle, short
scramble, alert freeze, nose-down forage, shallow-water push, turn, stop, and
recovery. Feelers react after head motion and never lead like combat tentacles.

Reduced motion removes ring pulsing, feeler vibration, splash particles,
reflected flicker, and skin ripple while retaining pouch volume, direction,
ground/water contact, foot placement, and stop.

The generated stills prove no clip, gait, swimming, motion, recovery, or
reduced-motion behavior.

## Exact Source Set

| Asset ID | Review role |
| --- | --- |
| `tdf_asset_habitat_eldergrove_mirrorroot_littoral_establishing_master_v001` | establishing shoreline, grayscale depth, static-water reduction, and representative Lumenback contact |
| `tdf_asset_habitat_eldergrove_mirrorroot_littoral_layout_transition_depth_v001` | illustrative dry/shallow/deep spatial grammar, both neighbor directions, waterline comparison, and effects-off value study |
| `tdf_asset_habitat_eldergrove_mirrorroot_littoral_material_lod_v001` | eight kit families, material groups, static-water intent, progressive reduction, and distant habitat/fauna silhouette |

## Provisional Low/Mobile Envelope

- Unique root/shore/stone prop families: `4`
- Organic dressing families: `3`
- Distant proxy families: `1`
- Active water-surface families: `1`
- Visible surface/material families: `4`
- Required particles: `0`
- Required dynamic lights: `0`
- Required emission families: `0`
- Target unique compressed habitat content: `6–8 MiB`
- Hard parent ceiling: `12 MiB`
- Preferred shared material instances: `2`
- Maximum simultaneous material families: `4`
- Maximum dominant flooded-root masses in one low/mobile view: `5`
- Maximum secondary organic clumps in that view: `12`

These are source targets, not measured runtime budgets. The Mire Lumenback's
separate provisional `3–5 MiB` creature target and `7–9` core clips are
inherited, not added to the habitat target or reauthorized here.

The generic low/mobile lane lists `3–4 MiB` and `5–6` clips. The specialized
Lumenback target fits the generic `6 MiB` maximum but exceeds the generic
target/clip lane at its upper end. This packet records that conflict and does
not silently choose a production budget; coordination must reconcile it after
user source approval.

## Quality Reduction

Remove in this order:

1. water ripple, reflected flicker, algae microdetail, wet sparkle, and tiny
   stone variation;
2. secondary reeds, small stems, minor floating growth, and distal feeler
   detail;
3. root subdivisions, close-only hide breakup, ring microdetail, and
   nonessential surface roughness.

Never remove:

- irregular shoreline and low water horizon;
- dry/shallow/deep value hierarchy;
- flooded root-shelf silhouette and open shore gaps;
- pale shallow-stone band against darker deep water;
- Lumenback low dome, throat pouch, paddle-foot footprint, head direction,
  and stable contact when the family remains visible.

Reduced quality uses one static water-value family and no particles, emission,
dynamic light, layered transparency, caustics, or fog dependency.

The generated layout and reduction panels are illustrative paintings. They do
not prove measured depth, safe water, plan dimensions, slope, shoreline
collision, buoyancy, PBR ranges, texel density, alpha policy, LOD geometry,
screen thresholds, or device performance. Every pictured water treatment
still uses reflection or specular value; the requested fully
reflection/ripple/flicker/specular-disabled comparison was not achieved. The
lowest creature study also retains prominent cyan rings after feet and contact
weaken, reversing the required reduction order. Neither defect is approved as
production behavior.

## Reuse And Exclusions

Allowed semantic reuse:

- `tdf_matfam_eldergrove_wet_bark_root`;
- `tdf_matfam_wet_hide`;
- shared path/shore-edge, packed-mask, opaque vegetation, and distant-proxy
  grammar;
- instanced root/stone/organic families within approved repetition limits;
- canonical Mire Lumenback identity and existing source sheet.

Prohibited reuse:

- Mirrorfin Lurker anatomy as root, stone, or shoreline structure;
- architecture, docks, boats, bridges, stairs, gates, or settlement grammar;
- Moonroot's deep flooded-root canopy as Mirrorroot's primary open-horizon read;
- generated pixels as production textures;
- duplicated canonical Lumenback source bytes;
- representative contact interpreted as swimming, spawn, or gameplay authority.

The existing `tdf_elite_eldergrove_mirrorfin_lurker` is context-only. Its
separate source remains user-unapproved and grants no anatomy, encounter,
population, or production reuse here.

## Non-Authority And Handoff

This packet does not decide player-facing names, lore, cultural meaning,
routes, water gameplay, terrain topology, navmesh, collision, swimming,
buoyancy, spawn, AI, combat, stats, rewards, quests, saves, streaming, pooling,
shaders, VFX, scenes, prefabs, Addressables, device floors, or builds.

After exact user approval, coordination/review must define measured plan,
section, depth and safety evidence; supported camera/pixel coverage; water and
shoreline representation; topology and LOD thresholds; density and repetition;
streaming; pooling; memory; reflection/static fallback behavior; and
acceptance captures. Engineering may then consume approved source without
redesigning it.
