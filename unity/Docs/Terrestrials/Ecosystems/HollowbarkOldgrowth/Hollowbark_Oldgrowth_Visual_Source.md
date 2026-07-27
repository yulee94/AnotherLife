# Hollowbark Oldgrowth Visual Source

## Decision Record

- Issue: `#259`
- Source version: `tdf-eco-hollowbark-2026-07-27-v001`
- Parent roster: `tdf-eco-2026-07-27-v001`
- Foundation normalization:
  `tdf-foundation-fauna-normalization-2026-07-27-v001`
- Primary mode: Codex terrestrial design
- Habitat: `tdf_habitat_eldergrove_hollowbark_oldgrowth`
- Environment kit: `tdf_envkit_eldergrove_hollowbark_oldgrowth`
- Placement fauna: `tdf_grove_strider`
- Habitat state: `ReadyForUserReview`
- Grove Strider placement evidence: `ReadyForUserReview`
- Overall QA: `PassWithConcern`
- User decision: `NotRequested`
- Runtime integration: `Blocked`

This packet pairs one bounded Eldergrove environment kit with an immutable
existing fauna identity. It provides appearance, qualitative scale/placement
intent, representative contact, depth-reduction intent, material hierarchy,
and reuse intent without granting gameplay or runtime authority.

## Protected Habitat Identity

The parent source's exact non-color identity is:

`open_understory_trunk_spacing_root_arch_openings`

The visual source protects these elements together:

1. widely spaced ancient trunk masses rather than a continuous branch wall;
2. open navigable ground-plane gaps visible through foreground and midground;
3. irregular hollow trunk cavities formed by age, loss, and decay;
4. buttress-root ramps and broken root spans with multiple ways around them;
5. a dry-to-wet vertical gradient from browsing shelf to drainage channel;
6. strong foreground, midground, and background separation without effects;
7. sparse focal ferns, lichen, deadwood, and hanging growth rather than carpet.

Color, mist, canopy shafts, particles, bioluminescence, wet sparkle, and
dynamic light cannot carry navigation, depth, or habitat identity.

## Landform And Composition

- Review composition unit: one provisional `128 m × 128 m` cell.
- Primary structure: ancient spaced trunks over irregular buttress-root beds.
- Traversable appearance: eroded loam lanes, natural root ramps, hollow
  bypasses, and broken fallen-root crossings with alternate ground routes.
- Elevated zone: dry, relatively open browsing shelves.
- Sheltered zone: dark hollow bases with exterior bounce light at the mouth.
- Descending zone: narrow wet root channels that visually approach Moonroot.
- Ground: dark matte loam, exposed roots, sparse leaf litter, and weathered
  stone beneath roots.
- Required particles: `0`.
- Required dynamic or shadowed lights: `0`.
- Required simulated water: `0`; wet channels are material/value evidence only.

The parent source allows one natural landmark, but no stable Hollowbark
landmark ID exists. This packet does not invent one: the largest spaced trunks
remain compositional anchors rather than a named runtime landmark.

Round portals, identical root arches, cathedral aisles, doors, windows,
carved steps, bridges with railings, and repeated modular tunnel cadence are
rejected. Every opening must remain visibly organic, asymmetric, and
interruptible by decay or erosion.

## Materials

| Role | Review color | Requirement |
| --- | --- | --- |
| primary bark | `#413A31` | rough gray-brown, broad age breakup |
| dry root | `#564A3A` | directional grain, matte contact |
| loam | `#302A24` | dark compact soil with bounded leaf litter |
| pale lichen | `#9A9B82` | sparse colonies, never dominant white noise |
| protected moss | `#555B38` | muted olive only in sheltered planes |
| wet root | `#393A32` | restrained roughness shift, no mirror gloss |
| fern | `#4A5134` | opaque grouped fronds, no transparency dependence |
| deadwood | `#625646` | dry value accent, no bright cut timber |

The parent material family
`tdf_matfam_eldergrove_wet_bark_root` remains authoritative. Green is
secondary; the habitat must survive grayscale and effects-off review.

## Neighbor Transition Evidence

Sunmane shared boundary — no stable transition ID exists in the current parent
source, so this packet does not mint one:

- trunks thin and the ground plane brightens toward a broad dry browsing shelf;
- fern density falls while low grass and seed-head presence may increase;
- long sightline pockets replace root-channel compression;
- the exact meadow boundary remains text-guided and is not fully pictured.

Moonroot shared boundary —
`tdf_transition_eldergrove_moonroot_floodbasin_to_hollowbark_oldgrowth`
is reused from the Moonroot companion rather than duplicated under a reversed
ID:

- root channels descend, widen, and gather stable dark water values;
- exposed buttress ramps break into root islands;
- wet bark and protected moss increase without green glow or fog wall;
- the generated sheets picture this direction more clearly than Sunmane.

These are appearance constraints only. They do not define zone boundaries,
runtime routes, navmesh, streaming cells, encounters, or spawn rules.

## Environment Kit

Exactly eight proposed natural families:

1. `tdf_prop_eldergrove_hollowbark_hollow_trunk_base`;
2. `tdf_prop_eldergrove_hollowbark_buttress_root_ramp`;
3. `tdf_prop_eldergrove_hollowbark_broken_fallen_root_span`;
4. `tdf_prop_eldergrove_hollowbark_eroded_loam_shelf`;
5. `tdf_prop_eldergrove_hollowbark_drainage_root_cluster`;
6. `tdf_prop_eldergrove_hollowbark_fern_lichen_clump`;
7. `tdf_prop_eldergrove_hollowbark_deadwood_scatter`;
8. `tdf_prop_eldergrove_hollowbark_distant_canopy_proxy`.

Trunk, ramp, span, shelf, and root-cluster families should share a bounded
bark/root material grammar while varying silhouette through authored
asymmetry, rotation, approved nonuniform scale, break states, and contact
decals. A root span never implies a required route and must have an alternate
ground path in representative compositions.

## Grove Strider Placement

The exact creature identity remains governed by
`unity/Docs/Terrestrials/Ecosystems/FoundationFauna/`.

Placement evidence protects:

- head height `1.8`, shoulder height `1.4`, and length `1.9` Champion units;
- long arched neck, narrow torso and legs, and open leg negative space;
- paired lateral leaf-like ear fins rooted behind the jaw;
- split flexible hoof contact and grounded diagonal support;
- bark-like dorsal ridges aligned to anatomy;
- restrained neck and tail tendrils;
- animal muzzle and warm amber eye;
- subordinate browser presence rather than landmark, mount, or guardian read.

The sheets introduce proportional drift between some generated panels:
several secondary Striders are smaller, shorter-necked, or more plate-heavy
than the canonical sheet. Only the canonical identity and numeric scale above
are protected. Generated animals are placement evidence, not replacement
turnarounds or production anatomy. Some ear fins also drift toward radial
crowns or antlers, and some dorsal ridges drift toward sharp armor; both reads
are rejected.

No sheet establishes population, juvenile state, spawn density, temperament,
herd behavior, route ownership, hostility, domestication, rewards, habitat
exclusivity, or the unpictured `late_autumn` and `mist` variants.

The neutral silhouettes in both the canonical and generated sheets are not
measured orthographic evidence. Their pictured comparison can appear shorter
than the written `1.8` head-height target. The written normalized scale remains
the source constraint until a measured orthographic capture supersedes it.

## Exact Source Set

| Asset ID | Review role |
| --- | --- |
| `tdf_asset_habitat_eldergrove_hollowbark_oldgrowth_establishing_master_v001` | establishing composition, habitat silhouette, grayscale/far read, reduced atmosphere, Grove Strider scale and contact |
| `tdf_asset_habitat_eldergrove_hollowbark_oldgrowth_layout_transition_v001` | elevated spatial grammar, dry/wet depth zones, representative clearance and placement, effects-off read |
| `tdf_asset_habitat_eldergrove_hollowbark_oldgrowth_material_lod_v001` | eight kit families, material groups, progressive quality reduction, distant habitat and fauna silhouette |

## Provisional Low/Mobile Envelope

- Unique trunk/root/ground prop families: `5`
- Organic dressing families: `2`
- Distant proxy families: `1`
- Visible surface/material families: `4`
- Required particles: `0`
- Required dynamic lights: `0`
- Required simulated water families: `0`
- Target unique compressed habitat content: `6–8 MiB`
- Hard parent ceiling: `12 MiB`
- Preferred shared material instances: `2`
- Maximum simultaneous material families: `4`
- Maximum dominant trunk masses in one representative low/mobile view: `5`
- Maximum secondary fern/lichen clumps in that view: `12`

These are source targets, not measured runtime budgets. The Grove Strider's
separate provisional `5–7 MiB` creature target is inherited, not added to the
habitat target or reauthorized here.

The normalized Strider source also inherits `7–9` core clips, while the parent
generic low/mobile lane lists `5–6` clips and `3–4 MiB` with a `6 MiB`
maximum. This packet records that conflict and does not silently choose a
production budget. Coordination must reconcile it after user source approval.

The generated layout and reduction panels are illustrative paintovers. They
do not prove plan dimensions, slope, headroom, collision, safe crossing,
PBR ranges, texel density, alpha policy, LOD geometry, screen thresholds, or
device performance.

## Quality Reduction

Remove in this order:

1. moss strands, pale lichen speckle, tiny rootlets, wet sparkle, and leaf
   litter variation;
2. secondary fern cards, minor deadwood, small hollow-edge breakup, and haze;
3. distal root subdivisions, background branch layers, and nonessential
   surface roughness.

Never remove:

- spaced trunk masses and open understory gaps;
- recognizable hollow-base silhouettes;
- buttress-root ramp direction and alternate ground opening;
- dry shelf to wet-channel value gradient;
- foreground/midground/background separation;
- Grove Strider neck arc, narrow leg negative space, paired ear fins, and
  grounded hoof contact when the family is meant to remain visible.

Reduced quality uses no particles, emission, dynamic light, layered
transparency, simulated water, or fog dependency.

## Reuse And Exclusions

Allowed semantic reuse:

- `tdf_matfam_eldergrove_wet_bark_root`;
- shared opaque hide/keratin and vegetation grammar;
- instanced root/loam modules within approved repetition limits;
- one distant opaque canopy proxy;
- canonical Grove Strider identity and existing source sheet.

Prohibited reuse:

- Hollowbark Stalker anatomy as tree structure;
- cathedral, gate, archway, bridge, stair, wall, or settlement grammar;
- Moonroot water-plane identity as the Hollowbark primary read;
- generated pixels as production textures;
- duplicated canonical Grove Strider source bytes;
- representative placement interpreted as spawn or gameplay authority.

The existing `tdf_elite_eldergrove_hollowbark_stalker` is context-only. Its
separate `ReadyForUserReview / ProvisionalPass` source remains user-unapproved
and does not authorize anatomy, habitat density, encounter, or production reuse
inside this packet.

## Non-Authority And Handoff

This packet does not decide player-facing names, lore, cultural meaning,
routes, terrain topology, navmesh, collision, spawn, AI, combat, stats,
rewards, quests, saves, streaming, pooling, shaders, VFX, scenes, prefabs,
Addressables, device floors, or builds.

After exact user approval, coordination/review must define measured plan and
section evidence, supported camera/pixel coverage, production scale and
clearance, topology and LOD thresholds, terrain/prop density, repetition
audits, streaming, pooling, memory, fallback behavior, and acceptance
captures. Engineering may then consume approved source without redesigning it.
