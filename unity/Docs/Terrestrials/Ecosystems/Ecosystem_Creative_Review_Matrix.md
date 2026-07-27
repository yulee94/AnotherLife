# Ecosystem Creative Review Matrix

## Control

- Issue: `#259`
- Source version: `tdf-eco-2026-07-27-v001`
- Companion visual source: `tdf-eco-soarer-2026-07-27-v001`
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

Only `RosterProposed`, `ProposedTextOnly`, `LegacyMergedProposal`, and inherited `ReadyForUserReview` states occur in this packet.

## Habitat Matrix

All sixteen habitat entries have stable design IDs and connected transition intent. None has a concept sheet, authored terrain source, production asset, or user approval.

| Habitat ID | State | Exact visual source | Primary future review question |
| --- | --- | --- | --- |
| `tdf_habitat_stonehold_faultroad_escarpment` | `RosterProposed` | none | Does the fault-road and compressed crown ridge read as Stonehold without dust, snow, or forge light? |
| `tdf_habitat_stonehold_rimecut_pass` | `RosterProposed` | none | Is the pass notch navigable and distinctive without a whiteout or ice sparkle? |
| `tdf_habitat_stonehold_ore_gallery_mouths` | `RosterProposed` | none | Do cave mouths, columns, and ground plane remain readable without colored fog or glowing ore? |
| `tdf_habitat_stonehold_slagfall_quarry` | `RosterProposed` | none | Do cooled slag terraces look physically settled rather than like a permanent lava theme? |
| `tdf_habitat_eldergrove_hollowbark_oldgrowth` | `RosterProposed` | none | Does old-growth scale coexist with a clear traversable understory? |
| `tdf_habitat_eldergrove_mirrorroot_littoral` | `RosterProposed` | none | Are shore depth, roots, and water legible when reflection and reed motion are reduced? |
| `tdf_habitat_eldergrove_sunmane_edge_meadow` | `RosterProposed` | none | Does the meadow remain adult and naturalistic without flower/pollen spectacle? |
| `tdf_habitat_eldergrove_moonroot_floodbasin` | `RosterProposed` | none | Can deep water, root islands, and openings read without magical glow? |
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
| `tdf_basalt_grazer` | `LegacyMergedProposal` | base concept sheet exists | pending | normalize packet identity/readiness, then exact habitat-placement review |
| `tdf_fauna_stonehold_rimefan_kite` | `ReadyForUserReview` | avian-soarer turnaround and motion/material sheets | not requested | resolve `PassWithConcern` skull, wing-group, and wedge-tail consistency |
| `tdf_fauna_stonehold_oreveil_isopod` | `ProposedTextOnly` | none | not requested | top/side/front anatomy, plate count, curl, locomotion/contact sheet |
| `tdf_fauna_stonehold_slagwhistle_burrower` | `ProposedTextOnly` | none | not requested | turnaround, foreclaw/ear-fold callouts, dig/contact sheet |
| `tdf_grove_strider` | `LegacyMergedProposal` | base concept sheet exists | pending | normalize packet identity/readiness, then exact habitat-placement review |
| `tdf_mire_lumenback` | `LegacyMergedProposal` | base concept sheet exists | pending | normalize packet identity/readiness, then exact habitat-placement review |
| `tdf_fauna_eldergrove_thornburrow_hare` | `ProposedTextOnly` | none | not requested | adult proportion turnaround, tusk/root contact, bound/landing sheet |
| `tdf_fauna_eldergrove_moonshell_cicada` | `ProposedTextOnly` | none | not requested | wing-roof anatomy, scale, trunk contact, launch/fold sheet |
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
