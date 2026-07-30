# Ecosystem Creative Review Matrix

## Control

- Issue: `#259`
- Source version: `tdf-eco-2026-07-27-v001`
- Companion visual source: `tdf-eco-soarer-2026-07-27-v001`
- Companion habitat/fauna source: `tdf-eco-moonroot-2026-07-27-v001`
- Foundation normalization source:
  `tdf-foundation-fauna-normalization-2026-07-27-v001`
- Stonehold habitat companion: `tdf-eco-faultroad-2026-07-27-v001`
- Eldergrove habitat companion: `tdf-eco-hollowbark-2026-07-27-v001`
- Eldergrove littoral companion: `tdf-eco-mirrorroot-2026-07-27-v001`
- Primary Codex mode: `terrestrial-design`
- Review state: `RosterProposed` habitats; mixed fauna source states
- User decision requested by this PR: no
- Runtime integration: blocked

This matrix prevents a merged roster proposal from being mistaken for approved visual source. A habitat or fauna identity becomes user-reviewable only after its exact pixels or equivalent source, immutable identity, provenance, scale, views, material callouts, motion intent, and accessibility evidence exist.

## Readiness Vocabulary

| State | Meaning |
| --- | --- |
| `RosterProposed` | Stable design identity and relationships exist; no exact visual source is supplied |
| `ProposedTextOnly` | Fauna design exists only as text and catalog metadata |
| `LegacyMergedProposal` | Earlier pixels exist, but user approval is absent and source-package normalization remains |
| `ReadyForUserReview` | Exact source pixels and technical evidence are available; user has not approved them |
| `UserCreativeApproved` | User approved an exact source version, IDs, variants, and hashes |
| `TechnicalHandoffReady` | Coordination specification accepts approved source for bounded engineering |
| `RuntimeIntegrated` | Engineering mapping exists and has passed technical plus A2 fidelity review |

Only `RosterProposed`, `ProposedTextOnly`, `LegacyMergedProposal`, and
companion/inherited `ReadyForUserReview` states occur in the current source
family.

## Habitat Matrix

All sixteen habitat entries have stable design IDs and connected transition
intent. Five now have exact companion concept source; eleven remain
roster-only. None has authored production terrain, runtime integration, or
user approval.

| Habitat ID | State | Exact visual source | Primary future review question |
| --- | --- | --- | --- |
| `tdf_habitat_stonehold_faultroad_escarpment` | `ReadyForUserReview` | establishing/scale/reduction, layout/transition, and material/kit/LOD sheets | Resolve remaining masonry-like joints, replace illustrative section/plinth with measured orthographic evidence, and lock prop dimension/repetition bands. |
| `tdf_habitat_stonehold_rimecut_pass` | `RosterProposed` | none | Is the pass notch navigable and distinctive without a whiteout or ice sparkle? |
| `tdf_habitat_stonehold_ore_gallery_mouths` | `RosterProposed` | none | Do cave mouths, columns, and ground plane remain readable without colored fog or glowing ore? |
| `tdf_habitat_stonehold_slagfall_quarry` | `ReadyForUserReview` | v002 establishing, plan, section, transition, material-kit, grayscale, and distant-reduction master | Preserve irregular raft breakup, braided runoff, broad gallery throats, and the no-spire/no-masonry rule through measured production blockout. |
| `tdf_habitat_eldergrove_hollowbark_oldgrowth` | `ReadyForUserReview` | establishing/placement/reduction, illustrative spatial/transition, and material/kit/LOD-intent sheets | Break portal-like root arches and repeated cavities; replace illustrative layout/scale/LOD with measured evidence; preserve a clear open understory and exact Grove Strider identity. |
| `tdf_habitat_eldergrove_mirrorroot_littoral` | `ReadyForUserReview` | establishing/placement, illustrative depth/transition, and material/kit/reduction sheets | Strengthen realm-specific shoreline identity; prove both named transitions and dry/shallow/deep recognition in measured views with reflection, ripple, flicker, specular response, and emission disabled; correct root-engineering and creature-reduction concerns. |
| `tdf_habitat_eldergrove_sunmane_edge_meadow` | `RosterProposed` | none | Does the meadow remain adult and naturalistic without flower/pollen spectacle? |
| `tdf_habitat_eldergrove_moonroot_floodbasin` | `ReadyForUserReview` | Moonroot establishing, layout/transition/depth, material/reduced-atmosphere/LOD, and shared contact sheets | Resolve constructed-shelf and gate-like split-buttress concerns while preserving depth and route read without effects. |
| `tdf_habitat_crownlands_crownstep_chalkland` | `RosterProposed` | none | Does disciplined landform rhythm carry Crownlands identity without gold paint or banners? |
| `tdf_habitat_crownlands_galegrain_roadbelt` | `RosterProposed` | none | Do road, field, and shelter lines remain clear at reduced crop density? |
| `tdf_habitat_crownlands_reliquary_crypt_garden` | `RosterProposed` | none | Can ruins and garden materials read distinctly without inventing religious meaning? |
| `tdf_habitat_crownlands_meridian_storm_shelf` | `RosterProposed` | none | Does the shelf remain recognizable and safe to read when lightning and rain are disabled? |
| `tdf_habitat_umbral_ashvein_three_fault_rift` | `RosterProposed` | none | Do three fault directions remain visible through midtones without violet outlines? |
| `tdf_habitat_umbral_cinder_runoff_shelf` | `RosterProposed` | none | Are hot, wet, and cooled surfaces distinguishable without red emission? |
| `tdf_habitat_umbral_ashwood_veil_ravine` | `RosterProposed` | none | Does the dark canyon keep route and ledge separation without becoming a spike field? |
| `tdf_habitat_umbral_graveglass_cavern_vale` | `RosterProposed` | none | Do cave arches and the vale retain readable midtones without backlight or fog? |

## Supporting-Fauna Matrix

| Family ID | State | Existing exact source | User approval | Required next source |
| --- | --- | --- | --- | --- |
| `tdf_basalt_grazer` | `ReadyForUserReview` | normalized immutable base sheet plus exact Faultroad scale/contact/reduced-placement evidence | not requested | rear/top/underside, plate/limb roots, motion/contact, and measured fauna LOD; habitat placement concern now bounded by Faultroad QA |
| `tdf_fauna_stonehold_rimefan_kite` | `ReadyForUserReview` | avian-soarer turnaround and motion/material sheets | not requested | resolve `PassWithConcern` skull, wing-group, and wedge-tail consistency |
| `tdf_fauna_stonehold_oreveil_isopod` | `ProposedTextOnly` | none | not requested | top/side/front anatomy, plate count, curl, locomotion/contact sheet |
| `tdf_fauna_stonehold_slagwhistle_burrower` | `ReadyForUserReview` | v002 identity/anatomy/scale/LOD sheet plus plant/cut/push/scurry/vent contact sequence | not requested | measured sculpt and rig blockout preserving the scapular yoke, one fused shovel palm plus two stabilizers per forefoot, flattened tail, and zero mandatory airborne effects |
| `tdf_grove_strider` | `ReadyForUserReview` | normalized immutable base sheet plus exact Hollowbark placement/reduction evidence | not requested | rear/top/underside, hoof/ear/tendril roots, motion/contact, measured scale/LOD, and correction of generated crown/armor/proportion drift; no juvenile or variant authority |
| `tdf_mire_lumenback` | `ReadyForUserReview` | normalized immutable base sheet plus concerned Mirrorroot placement/reduction evidence | not requested | rear/top/underside, pouch/feeler/swim contact, motion, and emission-off measured LOD; preserve feet/contact before ring detail and grant no juvenile, population, or unpictured-variant authority |
| `tdf_fauna_eldergrove_thornburrow_hare` | `ProposedTextOnly` | none | not requested | adult proportion turnaround, tusk/root contact, bound/landing sheet |
| `tdf_fauna_eldergrove_moonshell_cicada` | `ReadyForUserReview` | flood-season turnaround, motion/material, and shared contact/scale sheets | not requested | resolve rostrum, unobstructed six-leg/four-wing continuity, and distant presence-proxy concerns; dry-season ecotype remains text-only |
| `tdf_fauna_crownlands_broadcrest_aurochs` | `ProposedTextOnly` | none | not requested | original skull/horn-root turnaround, herd turn, shoulder-load sheet |
| `tdf_fauna_crownlands_grainveil_covey` | `ProposedTextOnly` | none | not requested | group scale, wedge/fan silhouette, ground motion and scatter sheet |
| `tdf_fauna_crownlands_reliquary_shellback` | `ProposedTextOnly` | none | not requested | shell/probe anatomy, clamp range, wall-step/contact sheet |
| `tdf_fauna_crownlands_stormglass_swift` | `ReadyForUserReview` | avian-soarer turnaround and motion/material sheets | not requested | verify measured `0.95` span and final fork proportion |
| `tdf_fauna_umbral_sootsail_carrioner` | `ReadyForUserReview` | avian-soarer turnaround and motion/material sheets | not requested | resolve `PassWithConcern` hood, wing-group, and terrace-toe consistency |
| `tdf_fauna_umbral_cinderplate_scarab` | `ProposedTextOnly` | none | not requested | six-limb anatomy, wingcase/shovel callouts, push/burrow sheet |
| `tdf_fauna_umbral_ashstep_bounder` | `ProposedTextOnly` | none | not requested | non-rabbit skull/body turnaround, ledge descent and landing sheet |
| `tdf_fauna_umbral_graveglass_sheller` | `ProposedTextOnly` | none | not requested | opaque shell/body anatomy, compression, wall-turn sheet |

The three new reviewable profiles are governed by
`unity/Docs/Terrestrials/Ecosystems/AvianSoarers/`. Their unpictured ecotypes
remain `ProposedTextOnly`; user and production states remain blocked.

The Moonroot habitat and pictured `moonshell_flood_season` ecotype are governed
by `unity/Docs/Terrestrials/Ecosystems/MoonrootFloodbasin/`. Both are
`ReadyForUserReview` with an overall `PassWithConcern` disposition. The
`moonshell_dry_season` ecotype remains `ProposedTextOnly`; constructed-shelf,
gate-like landmark, rostrum, and distant-proxy concerns remain production
blocks.

The three normalized foundation bases are governed by
`unity/Docs/Terrestrials/Ecosystems/FoundationFauna/`. Their exact existing
sheets are `ReadyForUserReview` with `PassWithConcern`; no raster is copied or
regenerated. Six unpictured palette-led variants remain `ProposedTextOnly`.
Missing orthographic, attachment, motion/contact, measured LOD, and Player
dependency evidence remain production blocks. Faultroad, Hollowbark, and
Mirrorroot now provide concerned placement evidence for Basalt Grazer, Grove
Strider, and Mire Lumenback respectively; no companion closes the other
missing evidence.

The Faultroad habitat and Basalt Grazer placement evidence are governed by
`unity/Docs/Terrestrials/Ecosystems/FaultroadEscarpment/`. The habitat is
`ReadyForUserReview` with `PassWithConcern`; the existing Grazer identity
remains governed by `FoundationFauna/`. Remaining masonry-like joints,
illustrative rather than measured section evidence, prop modularity/repetition,
camera coverage, and production measurements remain blocking.

The Hollowbark habitat and Grove Strider placement evidence are governed by
`unity/Docs/Terrestrials/Ecosystems/HollowbarkOldgrowth/`. The habitat is
`ReadyForUserReview` with `PassWithConcern`; the canonical Strider identity
remains governed by `FoundationFauna/` and is referenced without duplicated
raster bytes. Portal-like root arches, dense foreground clutter, incomplete
Sunmane transition evidence, generated scale/anatomy/life-stage ambiguity,
illustrative rather than measured layout/LOD, camera coverage, and production
measurements remain blocking.

The Mirrorroot habitat and Mire Lumenback placement evidence are governed by
`unity/Docs/Terrestrials/Ecosystems/MirrorrootLittoral/`. The habitat is
`ReadyForUserReview` with `PassWithConcern`; the canonical Lumenback identity
remains governed by `FoundationFauna/` and is referenced without duplicated
raster bytes. Generic boreal-lake risk, unproved Moonroot/Sunmane identities,
reflection/specular dependency, engineered-looking root bundles, same-adult
perspective ambiguity, incorrect lowest-proxy detail priority, missing measured
depth/scale/LOD, and production measurements remain blocking.

## Inherited Boss And Elite Anchors

Source version `tdf-rbe-2026-07-24-v001` remains unchanged:

- profiles: `16`;
- exact concept candidates: `16`;
- inherited state: `ReadyForUserReview`;
- user creative approval: none;
- visual QA: `9 ProvisionalPass`, `7 PassWithConcern`;
- runtime integration: blocked.

The seven inherited `PassWithConcern` profiles are:

- `tdf_boss_stonehold_fault_crowned_colossus`;
- `tdf_elite_stonehold_slaghide_gorer`;
- `tdf_boss_eldergrove_mere_root_leviathan`;
- `tdf_elite_eldergrove_sunmane_thornstag`;
- `tdf_elite_crownlands_crownstep_lion`;
- `tdf_elite_crownlands_galeclaw_courser`;
- `tdf_elite_umbral_gravewing_siphon`.

Their exact production follow-ups remain authoritative in `unity/Docs/Terrestrials/RealmBossesAndElites/Visual_QA_Disposition.md`. Habitat linkage here does not resolve or weaken those concerns.

## Future Habitat Review Gate

Each habitat selected for visual development must provide:

- wide composition and horizon silhouette;
- ground-level and elevated gameplay-distance views;
- grayscale and black-shape review;
- terrain, geology, organic, water, weather, and lighting callouts;
- both neighbor transition strips;
- approved architecture adjacency without architecture redesign;
- one low/mobile reduction and one distant proxy;
- reduced-motion/reduced-atmosphere presentation;
- exact file identity, dimensions, hashes, source version, provenance, and direct review link;
- an explicit list of absent production assets and unmeasured budgets.

## Future Fauna Review Gate

Each selected family must provide:

- front, side, rear, and three-quarter views or equivalent turnaround;
- Champion scale comparison;
- black silhouette at the proposed validation distance;
- anatomy attachment and contact logic;
- material/value callouts in neutral light;
- rest, locomotion, turn, habitat interaction, and recovery intent;
- base plus any structurally distinct proposed ecotype;
- LOD and distant read;
- reduced-motion and non-color recognition;
- exact file identity, dimensions, hashes, source version, provenance, and direct review link.

No text-only family may be called `ReadyForUserReview`.

## User Approval Record Required Later

A valid approval must identify:

```text
sourceVersion
approved habitat IDs
approved family IDs
approved variant IDs, if any
exact source asset IDs and hashes
accepted concerns or required refinements
```

General approval of a branch, issue, theme, or prose roster does not approve exact future pixels or production assets.
