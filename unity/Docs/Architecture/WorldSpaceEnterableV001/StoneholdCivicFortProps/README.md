# Stonehold Civic + Fort Furnishing Kit — 2D Candidate V001

## Status

- **Candidate only — owner review required.**
- **2D-first gate:** no 3D generation, Blender modeling, Unity import, prefab binding, runtime catalog change, or gameplay binding is authorized by this packet.
- **Scope split:** this lane covers non-terrain furnishings for the approved enterable civic hall and fort gatehouse. Terrain topology, world-map layout, roads as terrain, and overall 3D-map assembly remain assigned to another agent.
- **Creative authority:** if approved, that approval applies only to the selected forms, material direction, measured envelopes, placement examples, and variant policy explicitly shown here.

## Why this packet is first

These families make two already-approved enterable building shells legible as inhabited, functioning places. The kit deliberately reuses recordkeeping, seating, storage, readiness, inspection, barracks, and gate-control support pieces across both structures before adding lower-value decorative clutter.

## Bounded recon coverage

The directly applicable corpus was defined before authoring and read in full:

- **19/19 text/data authorities — 5,381/5,381 lines (100% of the scoped text corpus).**
- **10/10 binary sheets in `WorldSpaceEnterableV001` visually inspected.**
- **2/2 directly applicable Stonehold visual sources visually inspected:** modular workshop detail and Town Hall progression.
- **Not claimed:** whole-repository coverage, unrelated art binaries, terrain/map work, character work, weapons/gear, or runtime code not touched by this packet.

The controlling sources are the approved enterable-building layouts/manifests, root `DESIGN.md`, Stonehold/Four-Realm architecture authorities, mobile graphics standard, building catalog, and catalog-authority specification. The generated imagery never overrides those sources.

## Candidate family inventory

| # | ID | Family | Envelope W × D × H (m) | Primary reuse |
|---:|---|---|---:|---|
| 01 | `prop_stonehold_council_table_280_v001` | Council Table 2.8 m | 2.80 × 1.10 × 0.78 | Civic workrooms/chamber; holding records |
| 02 | `prop_stonehold_steward_desk_160_v001` | Steward Desk 1.6 m | 1.60 × 0.80 × 0.78 | Steward office |
| 03 | `prop_stonehold_clerk_counter_180_v001` | Clerk Counter 1.8 m | 1.80 × 0.70 × 1.05 | Records/inspection |
| 04 | `prop_stonehold_public_bench_180_v001` | Public / Guard Bench 1.8 m | 1.80 × 0.45 × 0.48 | Public hall/gallery; guard ready |
| 05 | `prop_stonehold_chair_055_v001` | Chair 0.55 m | 0.55 × 0.55 × 0.95 | Offices/chamber; movable |
| 06 | `prop_stonehold_record_shelf_090_v001` | Record Shelf Bay 0.9 m | 0.90 × 0.45 × 2.00 | Records/archive/inspection |
| 07 | `prop_stonehold_secure_chest_100_v001` | Secure Chest 1.0 m | 1.00 × 0.55 × 0.62 | Stores/records |
| 08 | `prop_stonehold_notice_board_120_v001` | Wall Notice Board 1.2 m | 1.20 × 0.20 × 1.20 | Public hall; blank surface only |
| 09 | `prop_stonehold_single_cot_210_v001` | Single Barracks Cot 2.1 m | 2.10 × 0.90 × 0.70 | Left/right barracks |
| 10 | `prop_stonehold_staff_locker_060_v001` | Staff Locker 0.6 m | 0.60 × 0.55 × 1.80 | Landings/staff/barracks |
| 11 | `prop_stonehold_empty_weapon_rack_160_v001` | Empty Armory Rack 1.6 m | 1.60 × 0.55 × 1.70 | Guard-ready/armory rooms; rack only |
| 12 | `prop_stonehold_supply_rack_120_v001` | Supply Rack 1.2 m | 1.20 × 0.55 × 1.90 | Stores/service |
| 13 | `prop_stonehold_utility_cabinet_060_v001` | Utility Cabinet 0.6 m | 0.60 × 0.50 × 1.50 | Upper/stair service |
| 14 | `prop_stonehold_supply_crate_080_v001` | Stackable Supply Crate 0.8 m | 0.80 × 0.60 × 0.60 | Stores/service |
| 15 | `prop_stonehold_inspection_counter_180_v001` | Inspection Counter 1.8 m | 1.80 × 0.70 × 1.05 | Right inspection |
| 16 | `prop_stonehold_gate_control_mount_240_v001` | Gate-Control Mount 2.4 m | 2.40 × 0.80 × 1.10 | Upper control gallery; behaviorless shell |

## Review surfaces

- `review/stonehold_civic_props_visual_source_v001.png` — eight selected civic form/material references.
- `review/stonehold_fort_props_visual_source_v001.png` — eight selected fort references; rejected double-bunk cell replaced by a verified single cot.
- `review/stonehold_civic_fort_props_measured_sheet_v001.png` — 16 candidate envelopes, pivots, material count, LOD triangle targets, and collider intent.
- `review/stonehold_civic_fort_props_room_fit_v001.png` — six measured room arrangements, 15 center-derived opening approaches, and protected circulation.
- `review/stonehold_civic_fort_props_material_runtime_v001.png` — Stonehold palette, wear-state policy, and post-approval runtime planning.
- `review/stonehold_civic_fort_props_review_contact_sheet_v001.png` — five-sheet review index; inspect the full-resolution sheets before deciding.

The generated image cells are **form/material reference only**. The JSON controls dimensions and implementation intent. Generated marks never authorize writing, heraldry, quests, inventory content, UI, or lore.

## Fit and circulation

- Civic public hall retains a continuous **1.20 m** center aisle while all six applicable opening approaches remain prop-free.
- Civic stores retain a **1.25 m** access run beside 0.55 m-deep racks.
- Civic council chamber uses a **2.80 m** table, leaving **1.25 m** at both side openings; six movable chairs avoid the centered gallery opening.
- Fort barracks retain a **1.70 m** center aisle using single cots; double bunks are not substituted.
- Fort stairs and landings retain the approved **1.50 m** clear width.
- The central **4.0 × 10.0 m** intact gate slot contains **zero prop placements** and remains physically impassable/gameplay-owned.
- The gate-control mount has empty sockets only. It supplies no linkage, actuation, collision, teleport, breach, state, or gameplay behavior.
- The crate source shows a three-unit stacking arrangement; the ID and **0.80 × 0.60 × 0.60 m** envelope always define one crate unit.

Exact room-local rectangles, packet authority keys, parent-record selectors, and the approved center-coordinate conversion are versioned in `stonehold_civic_fort_props_spec_v001.json`.

## Material and runtime intent after approval

- Stonehold direction: dark heavy timber, basalt feet/plinths, soot-aged iron, heavy leather where functional, quiet worn wool, and restrained bronze repair accents.
- One opaque material slot per prop target; shared 1K RGB atlas; mipmaps on; texture read/write off.
- No identity-critical transparency, broad emission, particles, pseudo-text, or fine filigree.
- Static noninteractive furnishings may combine per room/visibility cell after placement is final.
- Chairs, chests, and gate-control candidates stay separate only if later gameplay authority requires them.
- Simple box/compound colliders only where physical obstruction or interaction requires collision; never MeshColliders.
- LOD removes contents, fasteners, and wear before weakening the functional silhouette. Small-prop LOD0 target ceiling is 5,000 triangles.

## Variant policy

- **Maintained clean:** sound joinery, localized polish, no sterile/new-plastic finish.
- **Service worn:** contact wear, soot/scuff at use zones, visible credible repairs.
- **Damaged — review only:** an authored broken or replaced part with a credible load path. It grants no gameplay damage state.

## Source generation and disposition

- Civic task `01a0611b-c7cb-7351-ac34-0de5aa8e1423`: accepted for all eight form/material cells.
- Fort task `01a0611c-0844-7512-9238-063ac7b2c0c0`: accepted for seven cells; its double-bunk `r1c1` was rejected.
- Cot correction task `01a0611e-8ffb-71a2-822d-cde3d7ef207c`: rejected because it remained a double bunk.
- Cot correction task `01a06120-aa06-70e0-8910-491a69b56cf3`: accepted as one low single cot.
- **No Meshy 3D task was submitted.**

Full prompts, dispositions, room mapping, variant rules, performance intent, and exclusions are in the JSON specification. The original fort grid is retained only as hash-bound provenance for its seven accepted cells; its rejected `r1c1` is visibly and programmatically unreferenced. The separate rejected bunk correction remains in scratch and is excluded from this packet.

## Explicit exclusions

- Terrain and overall map work.
- Doors, shutters, glazing, or door interaction.
- Gate machinery or gate gameplay.
- Weapons or contents for the empty rack.
- Text, heraldry, records, notices, quests, inventory, economy, saves, or lore.
- 3D production, Unity integration, and runtime-catalog changes.

## Owner decision gate

Choose one after inspecting the full-resolution sheets:

- **APPROVE** — lock this V001 2D direction and permit a separately authorized downstream 3D pass.
- **REVISE** — name the family, dimension, material, placement, or variant changes.
- **REJECT** — return this slice to 2D concept revision.

Until explicit approval, this packet remains a candidate and must not be used to start 3D generation.
