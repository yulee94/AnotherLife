# Eldergrove Workshop Level Progression

**Status:** Owner-approved production source; source gate passed at `93 / 100`; Unity Level 1/6/10 blockout gate passed at `92 / 100`

**Asset ID:** `building_eldergrove_workshop_v001`

**Category:** Kingdom building / Workshop

**Created:** 2026-07-27

**Design authority:** Root [`DESIGN.md`](../../../../../DESIGN.md)

**Building-system authority:** [`Kingdom_Building_Level_And_Placement_Design.md`](../../../../Docs/Architecture/Kingdom_Building_Level_And_Placement_Design.md)

**Realm architecture authority:** [`FourRealmArchitecture.md`](FourRealmArchitecture.md)

**Motion authority:** [`Eldergrove_Architecture_Animation_Contract.md`](../../../../Docs/Architecture/Eldergrove_Architecture_Animation_Contract.md)

**Review sheet:** [`architecture_eldergrove_workshop_level_progression_v001.png`](../Architecture/ConceptSheets/architecture_eldergrove_workshop_level_progression_v001.png)

**Unity handoff:** [`Eldergrove_Workshop_Level_Blockout_Handoff.md`](../../../../Docs/Architecture/Eldergrove_Workshop_Level_Blockout_Handoff.md)

## Purpose

Define the first production-readable Level `0` through Level `10` model ladder for the Eldergrove Workshop while preserving the approved stable slot, shared building-level contract, Eldergrove motion grammar, and mobile-safety target above `90 / 100`.

The packet controls visual progression and model handoff only. It does not set costs, durations, production output, prerequisites, quest effects, cancellation, refunds, save fields, or transaction behavior.

## Approval boundary

The linked sheet is an AI-assisted visual source approved by the project owner on 2026-07-27. It is now visual authority for the Eldergrove Workshop level family, but it is not topology, metric, material, collider, LOD, animation-timing, save, economy, or gameplay authority.

The following are proposed production decisions:

1. Use the Eldergrove Workshop as the first complete Level `0`–`10` building family.
2. Preserve one stable entrance direction, foundation family, and Workshop identity across all levels.
3. Build later levels as cumulative module deltas rather than eleven unrelated prefabs.
4. Use a root-vault opening, cultivated central work area, and restrained lantern/capstone rhythm as protected identity cues.
5. Keep the Workshop within the common-building mobile envelope even at Level `10`.

The project owner approved the candidate with “Like this! Lets keep going” on 2026-07-27. Changing the approved silhouette ladder, shared level ladder, realm construction grammar, stable placement contract, or common-building performance class is now a major design-direction change.

## Production brief

| Field | Direction |
| --- | --- |
| Purpose | Make the kingdom Workshop readable as an active Eldergrove craft and cultivation structure at strategic and inspection cameras |
| Realm | Eldergrove |
| Stable building identity | `Workshop` |
| Stable slot identity | `kingdom.slot.workshop` in layout version `kingdom.layout.v1` |
| Approval state | Owner-approved production source; runtime candidate not yet approved |
| Scale | Medium common-building family; exact meter footprint remains `OPEN` |
| Camera use | Strategic 2.5D kingdom, normal gameplay, selected cutaway, limited inspection |
| Primary silhouette | Broad stone plinth beneath one load-bearing living-root entrance vault, a layered roof, and a restrained vertical lantern/capstone |
| Protected negative space | Open Workshop entrance beneath the root vault |
| Construction logic | Masonry and timber establish the crafted shell; a small number of grounded roots lock into prepared sockets and bronze collars; rigid roofs and fit-out follow |
| Materials | Aged pale stone, dark timber and living root, shared weathered-bronze accents, restrained moss/lichen, localized cultivation material |
| Palette | Bark umber, aged stone, muted green, subdued leaf gold, small warm living accents |
| Magic source | Local biological circulation through the cultivation mechanism; never whole-building emission |
| Runtime tier | Common building; mobile-first, scalable upward |
| Accessibility | Level changes survive grayscale, reduced motion, no particles, and compact strategic presentation |
| Exclusions | Giant treehouse, rustic cottage weakness, tangled branch canopy, floating assembly, palette-only changes, uncontrolled foliage, full-surface green glow, miniature-diorama cuteness |

## Level module plan

Each level adds the named delta to every confirmed lower-level module. Module names are source and prefab-hierarchy identifiers, not gameplay IDs.

| Level | Stable state | Required module delta | Protected strategic read |
| ---: | --- | --- | --- |
| `0` | Reserved plot | `L00_SiteBoundary`, `L00_EntranceMarker`, `L00_RootGuide` | Intentional empty Workshop footprint and entrance direction |
| `1` | Foundational | `L01_StonePlinth`, `L01_CraftedShell`, `L01_RootVault`, `L01_PrimaryRoof`, `L01_CultivationCore` | Complete operational Workshop with the root-vault entrance |
| `2` | Reinforced | `L02_RootBracePair`, `L02_WeatherShield`, `L02_DrainageEdge` | Broader grounded side support and deeper roof protection |
| `3` | Expanded | `L03_ServiceAnnex`, `L03_StorageBay` | First clear side-volume expansion |
| `4` | Established | `L04_RoofRidge`, `L04_CultivationVent` | Stronger roofline and one modest vertical element |
| `5` | District Anchor | `L05_PublicWorkbench`, `L05_ApproachCanopy` | Workshop begins addressing the road and neighboring district |
| `6` | Advanced | `L06_UpperGraftBay`, `L06_SecondaryRoof` | Meaningful upper mass without hiding the entrance |
| `7` | Signature | `L07_GuidedGrowthFrame`, `L07_CirculationCollar` | Eldergrove mechanism becomes structural and unmistakable without effects |
| `8` | Masterwork | `L08_RepairArcade`, `L08_BronzeJoinery`, `L08_GutterSpine` | Refined envelope and visible accumulated craft |
| `9` | Prestige | `L09_LogisticsBay`, `L09_CourtyardStorage`, `L09_ServiceApproach` | Complete Workshop logistics and stronger district frontage |
| `10` | Landmark | `L10_SeedLanternCapstone`, `L10_CrownRoof`, `L10_FinalRootLock` | Maximum Workshop silhouette with one restrained capstone |

No level may replace the root-vault entrance, silently rotate the building, cross the stable footprint boundary, or become a different building category.

## Construction binding

### Level 0 to Level 1

The complete shared six-state lifecycle may build the Level `1` module set:

1. `SitePrepared` — site boundary, drainage, root guides, and entrance marker.
2. `BaseStructureEstablished` — plinth, floor, sockets, and crafted shell.
3. `SignatureStructureEstablished` — the authored root-vault paths seat into prepared collars.
4. `UpperStructureEstablished` — rigid primary roof and weather protection are installed.
5. `FitoutCompleted` — benches, storage, and cultivation core are fitted.
6. `Operational` — one restrained circulation confirmation settles into a quiet hold.

### Level N to Level N+1

- Keep every confirmed Level N module settled and load-bearing.
- Bind only the target level's module delta to the shared lifecycle.
- Local site preparation may add a guide frame, access boundary, brace, or graft collar around the delta; it never erases the building.
- Initialize from authoritative order progress after load, reconnect, streaming, or offline time.
- Never persist the resolved animation state.

## Presentation and resolver contract

The runtime candidate resolves from:

```text
RealmId.Eldergrove
+ BuildingId.Workshop
+ confirmed level 0...10
+ quality tier
→ Eldergrove Workshop cumulative module set
+ Eldergrove construction-motion profile
```

- `KingdomBuildingPresentationResolver` remains read-only presentation input.
- Save-list order never controls location or module order.
- Missing or contradictory state uses the existing visible unavailable presentation.
- A missing level module fails visibly; it does not substitute another level or realm.
- The concept image is source-only and cannot become a runtime texture dependency.

## Mobile and LOD handoff

The common-building ceiling in `DESIGN.md` remains the maximum, not a target.

| Tier | Protected content | Reduction direction |
| --- | --- | --- |
| Inspection / LOD0 | Root-vault opening, entrance, level delta, cultivation core, material separation | Start below `20k` triangles, two renderer material families, shared 1K trim/atlas strategy |
| Normal / LOD1 | Footprint, roof rhythm, root braces, annex/upper mass, capstone | Approximately `50–60%` of approved LOD0 after silhouette review; merge bronze fittings and secondary roots |
| Strategic / LOD2 | Root-vault opening, dominant roof/annex mass, level silhouette | Approximately `20–30%` of approved LOD0; remove loose props, fine foliage, interior hardware, and small railings |
| Far proxy | Footprint, entrance notch, roof/capstone profile | Approximately `5–10%` of approved LOD0; static opaque proxy with no activity component |

Additional constraints:

- Use one opaque shared architecture atlas/material family plus one bounded cultivation/accent family when required.
- Mobile-low removes workers, loose tools, basin motion, minor foliage, fine bronze hardware, secondary lights, particles, and transparent effects.
- Structural roots, masonry, roof, lantern, and confirmed modules remain static in stable operation.
- Reduced motion snaps construction deltas to resolved stable states.
- Selection and occlusion groups remain separate from construction transforms.
- Colliders follow gameplay/selection needs and never mirror decorative root topology.
- Profile the Level `1`, `6`, and `10` representatives together in a populated kingdom before approving final thresholds.

## Required modeling submission

- Consistent front, side, rear, top, and three-quarter views for Levels `1`, `6`, and `10`.
- Orthographic footprint overlay proving one slot, entrance, selection bounds, and road alignment.
- Exploded cumulative module inventory covering every level delta.
- Source pivots and sockets for each construction group.
- Roof/canopy occlusion groups with finished hidden surfaces.
- Material swatches and shared atlas plan.
- LOD0, LOD1, LOD2, and far-proxy silhouettes for Levels `1`, `6`, and `10`.
- Collider and navigation-boundary proposal.
- Mobile-low, mobile-high, and PC-high comparison.
- Triangle, renderer, material, texture-memory, shadow, and build-size evidence from representative devices.

## Acceptance criteria

- Exactly eleven stable presentations exist from Level `0` through Level `10`.
- Every adjacent level has a non-color structural change visible at the strategic camera.
- The Workshop remains functionally and spatially recognizable at every level.
- The stable slot, entrance, and footprint do not depend on collection order.
- Later upgrades animate only the target module delta.
- The Eldergrove motion grammar remains separate from model identity and gameplay duration.
- No source concept texture is referenced by a Player prefab.
- Mobile safety remains above `90 / 100` after model, lighting, LOD, and representative-device review.

## Open production decisions

- [x] Project owner approved the review sheet as production source on 2026-07-27.
- [x] Deterministic Level `1`, `6`, and `10` Unity blockouts passed the visual gate at `92 / 100`.
- [ ] Exact metric footprint and height envelope.
- [ ] Final module pivots, sockets, and naming after DCC blockout.
- [ ] Final shared trim/atlas layout and material-slot plan.
- [ ] Final LOD thresholds after representative iOS and Android profiling.
- [ ] Whether Level `10` retains the proposed seed-lantern capstone or uses a quieter roof crown.
- [ ] Final damaged, disabled, repairing, selected, and unavailable model treatments.
