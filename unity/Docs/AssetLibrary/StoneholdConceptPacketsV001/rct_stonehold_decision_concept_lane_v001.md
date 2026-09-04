# rct_stonehold_decision_concept_lane_v001

**Packet ID:** `rct_stonehold_decision_concept_lane_v001`

**Catalog ID:** `rct_stonehold_catalog_concept_lane_v001`

**Owner status:** `APPROVE` (planning and concept direction, 2026-09-03)

**Final decision authority:** Project owner

**Generation state:** `HELD`

**Activation state:** `HELD`

**Date opened (UTC):** 2026-09-03

**Requested-by:** `t_a4734797`

## 1. Decision identity

| Field | Value |
| --- | --- |
| Realm | `stonehold` |
| Subject stable IDs | All 242 `waf_*` family records, mapped in `stonehold_concept_packet_coverage_v001.json` |
| Decision dimensions | Morphology / silhouette / material grammar / habitat identity for environment and architecture concept packets |
| Accountable implementers | Concept, architecture, technical art, gameplay, accessibility, performance, QA |

## 2. Decision question

**Question:** For each Stonehold concept packet, return APPROVE, REVISE, or REJECT. Separately choose ComfyUI Local or Cloud before any new images.

**Owner answer (2026-09-03):** APPROVE the complete packet set as planning and concept direction. Use ComfyUI Local for new concept-image development. Not blanket Meshy authorization.

**Why a decision was required:** Taxonomy families cannot proceed to Meshy/model production without owner packet rulings. Missing looks must not be guessed. ComfyUI was blocked until Local versus Cloud was chosen.

**Already approved and cannot change in this packet:**

- DESIGN.md core style lock and Stonehold realm identity matrix.
- Slagfall Quarry eight environment families (2026-08-31), hash-bound, profiling-scale only.
- Shared civic-hall and fort-gatehouse 2D spatial authority (2026-09-01 / PR #664). Stonehold civic exterior remains outside that package.
- Kingdom Stonehold Workshop production binding.
- Generation and activation remain held in `al_world_asset_inventory.json`.

**Still OPEN after concept-direction APPROVE:**

- Family concept sheets that do not exist yet (do not invent looks).
- Stonehold civic-hall and fort/castle exteriors; castle-keep interiors; door/glass/shutter families (`t_c748138b`).
- Realm-wide flora, crystals, mineables, roads/bridges look, props/decor, 2.5D derivatives, non-Slagfall habitats.
- Final Slagfall dimensions, navigation, and placement.
- Meshy / production geometry.

## 3. Source and provenance

| Provenance ID | Source kind | Path | Rights | SHA-256 | Notes |
| --- | --- | --- | --- | --- | --- |
| `rct_stonehold_provenance_design_v001` | repo_document | `DESIGN.md` | project_internal | not hashed this lane | Style contract |
| `rct_stonehold_provenance_taxonomy_v001` | repo_document | `unity/Docs/AssetLibrary/PostMVP_World_Asset_Taxonomy_v1.md` | project_internal | not hashed this lane | 242 families |
| `rct_stonehold_provenance_inventory_v001` | runtime_catalog | `unity/Assets/AL/StreamingAssets/GameData/al_world_asset_inventory.json` | project_internal | parent acceptance `79eb8b5fef5c9f8ed56eb1b627428007122c67c4a66956c9900909816538f828` | preparation_held |
| `rct_stonehold_provenance_slagfall_v001` | ai_generated + human review | `unity/Docs/AI/Meshy/meshy_execution_slagfall_environment_2026-08-31_v001.json` | project_internal | record-level hashes inside file | Owner approved eight families |
| `rct_stonehold_provenance_civic_v001` | human_authored | `unity/Docs/Architecture/WorldSpaceEnterableV001/CivicHall/civic_hall_2d_manifest_v001.json` | project_internal | per-artifact hashes in manifest | Shared 2D; Stonehold exterior excluded |
| `rct_stonehold_provenance_gatehouse_v001` | human_authored | `unity/Docs/Architecture/WorldSpaceEnterableV001/FortGatehouse/fort_gatehouse_2d_manifest_v001.json` | project_internal | per-artifact hashes in manifest | Shared 2D |

Benchmarks (BDO / Infinity Kingdom / Wuthering Waves / Throne & Liberty) are directional quality bars only. They are not approval authority and must not be copied.

## 4. Alternatives

### Alternative `accept_partial_map_hold_remainder`

**Summary:** Accept the 242-family map, the Slagfall partial geology approval, and the civic/fort shared 2D split. Keep every other family PENDING. No ComfyUI, no Meshy.

**Approved facts preserved:** All bullets in section 2.

**Proposed choices:** Coverage registry becomes the living approval index. `t_c748138b` remains the enterable-architecture 2D remainder. `t_bfde752c` stays unreleased until remaining natural-kit packets are approved.

**Production implications:** Modeling/rigging/VFX/gameplay stay held. Accessibility and mobile strategy documented as intent only.

**Risks:** Slow visual progress. Reversible: owner can later APPROVE individual packets.

### Alternative `authorize_comfyui_after_local_or_cloud`

**Summary:** Same as above, plus the owner chooses Local or Cloud so a later card may generate missing 2D sheets. Still no Meshy until those sheets are shown and approved.

**Use when:** The owner wants new family sheets. Visibility gate still applies.

### Alternative `retain_hold`

**Summary:** Keep affected records at owner_decision_required; do not treat this documentation as any packet APPROVE.

**Downstream:** Map still exists for audit; no child production starts.

## 5. Recommendation

**Recommended alternative:** `accept_partial_map_hold_remainder`

**Reason:** Matches the resume instruction: prepare decision-independent material, do not guess, do not use ComfyUI yet.

**Uncertainty:** Owner may instead want immediate 2D generation after choosing Local versus Cloud.

## 6. Owner ruling

**Recorded 2026-09-03.**

- Lane: `APPROVE` as planning and concept direction.
- Per-packet: `APPROVE` (see `README.md`).
- ComfyUI: Local.
- Meshy: not authorized.
- Enterable interiors: every applicable 3D building, civic/service structure, city structure, fort, fortress, or castle must be enterable and traversable with a furnished interior planned together with its exterior. Representative-only shells are forbidden. Natural assets and freestanding props are excluded. Perimeter walls stay non-enterable and impassable except defendable walltops and designated routes; gates and doors are separate objects.

`approvedAlternativeId`: `owner_approve_concept_direction_comfyui_local`

Generation and activation remain held. Meshy remains unauthorized (`meshyAuthorized=0` in the coverage registry).
