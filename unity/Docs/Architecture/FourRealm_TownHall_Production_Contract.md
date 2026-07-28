# Four-Realm Town Hall Production Contract

**Status:** Shared production direction approved; all four realm source designs
and Level `1`/`6`/`10` graybox gates passed; Stonehold, Eldergrove, and
Crownlands final production and live-binding proofs passed

**Date:** 2026-07-28

**Building identity:** `TownHall`

**Stable slot identity:** `kingdom.slot.town-hall`

**Design authority:** Root `DESIGN.md`,
`Assets/AL/Art/Designs/FourRealmArchitecture.md`, and the four approved realm
construction-motion contracts

This packet defines Town Hall as the four-realm production building family
after Workshop parity. It records the shared scope proven by all four graybox
gates without inventing economy, save, timer, quest, worker, or narrative
rules. Exact final production-mesh measurements remain subject to live kingdom
camera review and representative-device profiling.

## Approved Stonehold source

- Source sheet:
  `Assets/AL/Art/Architecture/ConceptSheets/architecture_stonehold_townhall_level_progression_v001.png`
- Owner decision: approved on 2026-07-27 with direction to continue.
- Approved anchors: Level `1`, `6`, and `10`; one fixed center entrance and
  footprint; correctly pitched heavy roofs; cumulative low civic wings; and
  the grounded static Oathstone Crown.
- Authority boundary: source-only visual direction. It does not enter the
  runtime catalog or establish gameplay costs, duration, save data, economy,
  final mesh topology, collider implementation, LOD thresholds, or device
  performance.
- Graybox handoff:
  `Stonehold_TownHall_Level_Blockout_Handoff.md`

## Approved Eldergrove source

- Source sheet:
  `Assets/AL/Art/Architecture/ConceptSheets/architecture_eldergrove_townhall_level_progression_v001.png`
- Owner decision: approved on 2026-07-27 after candidate review.
- Approved anchors: Level `1`, `6`, and `10`; one fixed center entrance and
  open civic court; exactly three primary authored root arches; cumulative
  unequal galleries; and the grounded static Open Crown Arbor.
- Production status: final dimensions, cumulative topology, Town Hall-specific
  atlas, exactly two colliders, four LOD bands, Open Crown Arbor, and exact
  `RealmId.Eldergrove + BuildingId.TownHall` live binding now pass. This does
  not establish gameplay costs, duration, save data, economy, or measured
  device performance.
- Graybox handoff:
  `Eldergrove_TownHall_Level_Blockout_Handoff.md`
- Final handoff:
  `Eldergrove_TownHall_Final_Model_And_Runtime_Binding.md`

## Approved Crownlands source

- Source sheet:
  `Assets/AL/Art/Architecture/ConceptSheets/architecture_crownlands_townhall_level_progression_v001.png`
- Owner decision: approved on 2026-07-28 after a focused category correction
  removed cathedral and royal-monument drift.
- Approved anchors: Level `1`, `6`, and `10`; one fixed axial entrance and
  broad civic mass; cumulative balanced wings; exactly two lower grounded
  civic towers; and the shallow static Concord Meridian.
- Production status: final dimensions, cumulative topology, Town Hall-specific
  atlas, exactly two colliders, four LOD bands, Concord Meridian, and exact
  `RealmId.Crownlands + BuildingId.TownHall` live binding now pass. This does
  not establish gameplay costs, duration, save data, economy, or measured
  device performance.
- Graybox handoff:
  `Crownlands_TownHall_Level_Blockout_Handoff.md`
- Final handoff:
  `Crownlands_TownHall_Final_Model_And_Runtime_Binding.md`

## Approved Umbral source

- Source sheet:
  `Assets/AL/Art/Architecture/ConceptSheets/architecture_umbral_townhall_level_progression_v001.png`
- Owner decision: approved on 2026-07-28 after a focused silhouette correction
  removed portal, gallows, and oversized-cage drift.
- Approved anchors: Level `1`, `6`, and `10`; one fixed oblique but readable
  public entrance; cumulative offset protected civic masses; exactly four
  grounded boundary piers with local roof braces; and the compact static
  Veiled Accord Yoke around a short truly empty upper council slit.
- Authority boundary: source-only visual direction. It does not enter the
  runtime catalog or establish gameplay costs, duration, save data, economy,
  final mesh topology, collider implementation, LOD thresholds, or device
  performance.
- Graybox handoff:
  `Umbral_TownHall_Level_Blockout_Handoff.md`

## Major design-direction decisions

1. Town Hall is a **hero-scale civic anchor**, not the castle keep, palace,
   throne room, or realm landmark. It must make the center of the working
   settlement readable without stealing the protected-high-point role.
2. The existing stable center slot is authoritative in all four realms:
   `kingdom.slot.town-hall` at grid position `(0, 0)`, entrance rotation `0`.
   Save-list order never affects placement.
3. All realms use one shared Level `1`–`10` functional ladder. Realm identity
   changes silhouette, load path, materials, and motion—not level meaning.
4. A confirmed gameplay level remains the only visual-level authority.
   Construction motion is session-only and confirmation-driven. No Town Hall
   visual stage is added to saves.
5. The working Level `10` capstone names below are visual-production labels,
   not narrative canon, powers, institutions, or progression promises.
6. Stonehold was the first proof because its rigid construction grammar
   exposed footprint, load-path, entrance, and occlusion errors most clearly.
   Its approved topology pattern established the shared gate subsequently
   passed by Eldergrove, Crownlands, and Umbral.

Changing the civic-not-keep role, stable center slot, shared level ladder,
gameplay-authoritative visual level, or grounded capstone rule requires owner
approval.

## Shared spatial envelope

`1 Unity unit = 1 meter`. The root pivot is the footprint center on the
finished ground plane. The entrance faces local `-Z`.

| Item | Candidate value |
| --- | ---: |
| Footprint class | Hero civic |
| Stable slot envelope | `16.0 m W × 16.0 m D × 13.0 m H` |
| Maximum art envelope | `15.2 m W × 14.2 m D × 12.6 m H` |
| Level 1 target envelope | approximately `9.5 m W × 8.5 m D × 6.8 m H` |
| Level 6 target envelope | approximately `12.5 m W × 11.5 m D × 9.4 m H` |
| Level 10 target envelope | within the maximum art envelope |
| Live strategic-board scale | `0.09` |
| Minimum clear entrance | `2.0 m W × 3.0 m H` |
| Selection padding inside slot | minimum `0.4 m` per horizontal side |

The Level `1` building is complete and operational. Later levels add cumulative
modules without moving the pivot, entrance, focus anchor, navigation boundary,
or stable slot. Realm-specific stairs, roots, buttresses, shelters, and civic
aprons may vary inside the envelope.

## Shared Level 1–10 topology ladder

Every final prefab uses ten cumulative deltas for LOD0–2 and milestone
collapses at LOD3.

| Level | Shared functional read | Required cumulative change |
| ---: | --- | --- |
| `0` | Reserved civic plot | No production model; stable plot and entrance reservation only |
| `1` | Operational hall | Complete weatherproof public hall, clear entrance, council/work floor, roof, drainage, and one realm structural cue |
| `2` | Grounded | Foundation locks, corners, retaining edges, or root collars make the civic mass more permanent |
| `3` | Working wing | One records, stores, steward, or service annex expands daily function without inventing gameplay |
| `4` | Public threshold | Entrance, steps, canopy, or sheltered approach clarifies civic access |
| `5` | Realm structure | One approved realm-defining load path becomes unmistakable at strategic distance |
| `6` | District capacity | Second wing, gallery, courtyard edge, or protected assembly bay increases occupied mass |
| `7` | Upper authority | Roof, ridge, canopy, or tower group strengthens the skyline while remaining below the capstone |
| `8` | Service integration | Rear circulation, drainage, stores, staff access, or utility structures complete believable operation |
| `9` | Civic integration | Forecourt edges, notice structure, ceremonial threshold, or district connections complete the center |
| `10` | Grounded civic capstone | One static realm-specific crown extends existing structure without changing function |

The Town Hall does not receive a larger gameplay footprint at later levels.
Only approved art may approach the fixed slot boundary.

## Realm translations

### Stonehold

- Broad stepped basalt council mass with a clipped central entrance, two low
  unequal civic wings, heavy roof plates, iron locks, and a visible grounded
  public threshold.
- Level `5` emphasizes paired load-bearing buttress towers and a continuous
  lintel path rather than forge machinery.
- Working Level `10` direction: **Oathstone Crown** — a grounded stepped
  belfry/crown carried by the established central wall, one fixed iron oath
  plate, and one narrow contained amber slit. It is not a forge chimney, weapon,
  throne, floating crown, or continuously moving bell.

### Eldergrove

- Open pale-stone civic court carried by three authored living-root arches,
  bronze collars, dark timber galleries, drainage, and a sheltered but
  breathable public entrance.
- Level `5` completes a deterministic council-canopy load path; roots do not
  procedurally grow or obscure the entrance.
- Working Level `10` direction: **Open Crown Arbor** — three grounded mature
  root arches and a fixed bronze ring frame an open sky oculus above the hall.
  It contains no seed lantern, floating orb, dense foliage cloud, or permanent
  green emission.

### Crownlands

- Upright symmetrical civic hall with pale masonry, disciplined bays, paired
  entrance piers, blue-slate roof groups, silver structural ribs, and a clear
  axial public approach.
- Level `5` establishes the central civic meridian and balanced side galleries
  without turning the building into a palace.
- Working Level `10` direction: **Concord Meridian** — two grounded civic
  towers support one fixed shallow silver meridian arch and one solid unmarked
  apex block. It is not a clock mechanic, lightning device, royal throne,
  levitating ring, royal symbol, or continuously rotating instrument.

### Umbral

- Offset graphite civic masses organize a protected central chamber, oblique
  but readable entrance, split roof planes, ash-timber galleries, sparse brass
  hierarchy, and one contained darkglass civic focus.
- Level `5` makes four grounded boundary piers and their physical sheltering
  relationship countable without creating a ritual portal.
- Working Level `10` direction: **Veiled Accord Yoke** — four grounded offset
  piers carry an asymmetrical fixed crossframe around a narrow empty council
  slit. It is not a portal, gallows, floating ring, darkness effect, or
  full-building violet emission.

## Production topology and hierarchy

The candidate root shape is:

```text
<Realm>_TownHall_Production
├── LOD0
│   ├── L01_Delta … L10_Delta
├── LOD1
│   ├── L01_Delta … L10_Delta
├── LOD2
│   ├── L01_Delta … L10_Delta
└── LOD3
    ├── L01_Delta
    ├── L06_Delta
    └── L10_Delta
```

LOD0–2 preserve all ten activation thresholds so the gameplay-confirmed level
is visually exact. LOD3 may collapse the same state into Level `1`, `6`, and
`10` silhouette milestones. All levels remain cumulative.

Candidate mobile ceilings:

| LOD | Screen transition | Triangle ceiling | Renderer ceiling | Material policy |
| --- | ---: | ---: | ---: | --- |
| `0` | `0.60` | `12,000` | `10` | target `2`, maximum `3` |
| `1` | `0.30` | `6,000` | `10` | target `2` |
| `2` | `0.12` | `2,500` | `10` | prefer `1–2` |
| `3` | `0.04` | `800` | `3` | `1` |

These are ceilings, not targets. Cross-fading remains disabled to avoid
double-render overlap in a populated mobile kingdom. Normal district play
should prefer LOD1; LOD0 is for close inspection.

## Materials and atlas

- Target two opaque, instancing-enabled visible materials per realm Town Hall:
  one realm architectural atlas and one tightly localized accent.
- A third material is allowed only when the Stonehold proof demonstrates a
  legibility need that cannot be resolved in the atlas without harming batching
  or mask quality.
- Start with one `1024 × 1024` RGB atlas per realm Town Hall, mipmapped,
  compressed, no alpha, CPU read/write disabled.
- Reuse approved realm material ranges and trim proportions. Do not reuse the
  Workshop's building-specific atlas as if it were a universal realm atlas.
- Transparent windows, cloth, smoke, particles, animated emissive surfaces,
  and unique `2K` maps require measured value and a separate mobile review.
- Realm identity must survive with accent emission disabled.

Expanding the packaged catalog to the Town Hall family does not by itself
authorize Addressables, AssetBundles, remote delivery, or a second loading
service. The existing `Resources` catalog remains the loading boundary until
measured residency or package-size evidence justifies a change.

## Colliders, anchors, and occlusion

Each root owns exactly two simple colliders:

| Role | Trigger | Candidate responsibility |
| --- | --- | --- |
| Selection | Yes | Covers the complete Level `10` interactable mass inside the slot |
| Navigation | No | Protects grounded occupied mass without matching upper decorative topology |

Required named anchors/groups:

- `Entrance` at the fixed local `-Z` public approach;
- `CameraFocus` near the Level `6` visual center, not at the capstone tip;
- `Activity_00` for bounded civic life with no gameplay implication;
- `Output_00` reserved but behavior-free;
- separately addressable roof/canopy/crown occlusion groups;
- no collider on individual render meshes or level deltas.

## Motion and live binding

Each Town Hall catalog entry reuses the realm motion profile already approved
for that realm. It does not reuse Workshop geometry or Workshop-specific
operational activity.

```text
authoritative BuildingState.Level
→ KingdomBuildingPresentationResolver
→ stable Town Hall slot
→ KingdomBuildingModelCatalog
→ <Realm>_TownHall_Production
→ KingdomBuildingLevelModel.ApplyConfirmedLevel
```

- Active upgrade: keep the current confirmed Town Hall settled and show only
  existing generic worksite feedback.
- Adjacent confirmed level in the current session: animate only the new level
  delta with the compatible realm motion profile.
- First load, reconnect, streaming, offline reconciliation, same-level refresh,
  invalid state, or multi-level jump: display the confirmed level settled.
- Reduced motion: settle immediately.
- Presentation never calls `StartUpgrade`, `CompleteUpgrade`, economy, save,
  quest, or timer services.

The open progression, economy, save, and game-data authority issues remain
prerequisites for enabling live upgrade commands. This model packet must not
work around those blockers.

## Validation gates

### Per-realm graybox proof gate

- deterministic Level `1`, `6`, and `10` graybox;
- stable center-slot fit and neighbor-clearance check in every realm layout;
- controlled camera framing at compact iPhone and representative Android
  aspect ratios;
- entrance, collider, focus, and occlusion review;
- Level `1`–`10` activation and adjacent-confirmation transition tests;
- visual-verdict score at or above `90 / 100`;
- Android and iOS Architecture suites pass while iOS remains minimum `15.0`
  and simulator ARM64.

### Four-realm production gate

- each realm passes its own protected-identity visual review;
- all four prefabs obey the shared root, level, collider, material, and LOD
  contracts;
- catalog bindings use exact `RealmId + BuildingId.TownHall` identity;
- Workshop bindings remain unchanged;
- the static architecture mobile-safety score stays above `90 / 100`;
- populated-kingdom physical-device profiling remains explicitly open until
  measured.

## Production sequence

Stonehold, Eldergrove, and Crownlands now prove the final shared
implementation pattern:

- ten cumulative deltas at LOD0–2 and three far milestones at LOD3;
- one Town Hall-specific RGB atlas plus one localized opaque accent material;
- exactly two root box colliders and stable behavior-free anchors;
- direct exact realm plus `BuildingId.TownHall` catalog identity;
- the realm motion profile reused only as construction motion grammar;
- confirmed gameplay level as the sole visual authority.

Umbral remains separately gated. Its production model must translate its
approved silhouette rather than copying Stonehold, Eldergrove, or Crownlands
geometry or materials.

## Deferred decisions

- Final measured dimensions for Umbral after its production camera pass.
- Final production-model surface sheets and exact surface wear beyond the
  current Stonehold, Eldergrove, and Crownlands atlas proofs.
- Final damage, disabled, repair, selected, and unavailable art.
- Bounded civic workers and ambient activity scheduling.
- Measured scene-residency threshold for replacing the packaged Resources
  catalog.
- Gameplay upgrade cost, duration, prerequisite, command, completion, refund,
  quest, notification, and save-migration rules.
