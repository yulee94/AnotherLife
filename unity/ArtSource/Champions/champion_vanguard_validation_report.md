# Champion Vanguard — Independent Validation Report

**Task:** t_0f2ce476 — Validate Champion Vanguard candidate against extracted source requirements
**Date:** 2026-08-18
**Validator:** independent measurement (Blender 5.2.0 headless, fresh script — not the producer's verify script)
**Candidate under review:** `unity/ArtSource/Champions/champion_vanguard_working_v001.blend`
(produced by task t_eaaabf32, commit 7b99bec7)

---

## 1. Verdict summary

| Requirement | Source target | Measured | Verdict |
| --- | --- | --- | --- |
| LOD0 (high) triangles | 8,000 – 18,000 | **11,386** | PASS |
| LOD1 (medium) triangles | 3,000 – 6,000 | **4,480** | PASS |
| LOD2 (low) triangles | 800 – 1,500 | **1,084** | PASS |
| Unity Humanoid required bones | all 20 present | **22 bones, 20/20 required + 2 optional shoulders** | PASS |
| Deformation bones < 90 | < 90 | **22** | PASS |
| Bone influences per vertex | ≤ 4 | **max 4** | PASS |
| Forward = +Z (Blender -Y) | faces -Y | **face forward of head (ΔY = -0.093 m)** | PASS |
| Pivot at ground center | root @ origin, feet @ Z=0 | **root (0,0,0), feet Z=0.0000** | PASS |
| Height 7.75 heads (~1.75–1.9 m) | 1.75–1.9 m | **1.7814 m total** | PASS (height) |
| Head ≈ 1/7.75 of height | ~0.23 m head | **0.283 m head mesh (6.3:1)** | **DEVIATION** (see §5) |
| 11 modular slot collections | 11 + anchors | **all 11 + anchors present** | PASS |
| Body / equipment separated | not fused | **10 body + 6 equipment objects, no fusion** | PASS |
| Turntable captures | present | **8 turntable + 4 LOD captures** | PASS |
| Units metric / scale 1.0 | metric, 1.0 | **METRIC, scale 1.0, meters** | PASS |
| Triangulated topology (no n-gons) | implied by "production topology" | **8 n-gons in LOD0/LOD1, 1 in LOD2** | **DEVIATION** (see §5) |

**Overall:** The candidate passes every quantitative budget and structural check the source
documents actually specify. Two deviations fall inside the already-declared approval boundary
(topology, proportion) and **must be reviewed by the user before promotion** — they are not
release blockers on the quantitative budget but are honest gaps in the production-readiness claim.

---

## 2. Per-LOD triangle counts (measured, not copied)

All counts are measured per object via `len(loop_triangles)` after `calc_loop_triangles()`
(Blender's exact triangulation), cross-checked against the polygon fan formula `Σ(verts-2)`.
The two methods agree to the triangle on every LOD.

### LOD0 — high detail (11,386 tris total; body 9,600 + equipment 1,786)

| Object | Slot | Polys | n-gons | Tris |
| --- | --- | ---: | ---: | ---: |
| SM_Head | head | 672 | 0 | 1,344 |
| SM_Hair | hair | 192 | 0 | 384 |
| SM_Face | face | 384 | 0 | 768 |
| SM_Eye_L | face | 96 | 0 | 192 |
| SM_Eye_R | face | 96 | 0 | 192 |
| SM_Torso | torso | 288 | 0 | 576 |
| SM_Arm_L | arms | 768 | 0 | 1,536 |
| SM_Arm_R | arms | 768 | 0 | 1,536 |
| SM_Leg_L | legs | 768 | 0 | 1,536 |
| SM_Leg_R | legs | 768 | 0 | 1,536 |
| Shoulder_L | shoulders | 312 | 0 | 576 |
| Shoulder_R | shoulders | 312 | 0 | 576 |
| Cape | cape | 88 | 0 | 176 |
| Weapon_Main | main-hand | 153 | **2** | 286 |
| Shield_Off | off-hand | 16 | **2** | 52 |
| Realm_Ornament | realm-ornament | 36 | **4** | 120 |
| **TOTAL** | | | **8 n-gons** | **11,386** |

### LOD1 — medium (4,480 tris; body 3,780 + equipment 700)

| Object | Slot | Polys | n-gons | Tris |
| --- | --- | ---: | ---: | ---: |
| SM_Head_LOD1 | head | 380 | 0 | 530 |
| SM_Hair_LOD1 | hair | 112 | 0 | 150 |
| SM_Face_LOD1 | face | 237 | 0 | 302 |
| SM_Eye_L_LOD1 | face | 53 | 0 | 74 |
| SM_Eye_R_LOD1 | face | 53 | 0 | 74 |
| SM_Torso_LOD1 | torso | 175 | 0 | 226 |
| SM_Arm_L_LOD1 | arms | 448 | 0 | 606 |
| SM_Arm_R_LOD1 | arms | 452 | 0 | 606 |
| SM_Leg_L_LOD1 | legs | 473 | 0 | 606 |
| SM_Leg_R_LOD1 | legs | 473 | 0 | 606 |
| Shoulder_L_LOD1 | shoulders | 172 | 0 | 227 |
| Shoulder_R_LOD1 | shoulders | 172 | 0 | 227 |
| Cape_LOD1 | cape | 46 | 0 | 68 |
| Weapon_Main_LOD1 | main-hand | 83 | **2** | 112 |
| Shield_Off_LOD1 | off-hand | 9 | **2** | 20 |
| Realm_Ornament_LOD1 | realm-ornament | 23 | **4** | 46 |
| **TOTAL** | | | **8 n-gons** | **4,480** |

### LOD2 — low (1,084 tris; body 920 + equipment 164)

| Object | Slot | Polys | n-gons | Tris |
| --- | --- | ---: | ---: | ---: |
| SM_Head_LOD2 | head | 110 | 0 | 128 |
| SM_Hair_LOD2 | hair | 33 | 0 | 36 |
| SM_Face_LOD2 | face | 73 | 0 | 74 |
| SM_Eye_L_LOD2 | face | 18 | 0 | 18 |
| SM_Eye_R_LOD2 | face | 18 | 0 | 18 |
| SM_Torso_LOD2 | torso | 52 | 0 | 54 |
| SM_Arm_L_LOD2 | arms | 143 | 0 | 148 |
| SM_Arm_R_LOD2 | arms | 141 | 0 | 148 |
| SM_Leg_L_LOD2 | legs | 142 | 0 | 148 |
| SM_Leg_R_LOD2 | legs | 142 | 0 | 148 |
| Shoulder_L_LOD2 | shoulders | 49 | 0 | 55 |
| Shoulder_R_LOD2 | shoulders | 49 | 0 | 55 |
| Cape_LOD2 | cape | 16 | 0 | 16 |
| Weapon_Main_LOD2 | main-hand | 26 | 0 | 26 |
| Shield_Off_LOD2 | off-hand | 1 | 0 | 2 |
| Realm_Ornament_LOD2 | realm-ornament | 7 | **1** | 10 |
| **TOTAL** | | | **1 n-gon** | **1,084** |

Per-module LOD separation is preserved: each of the 16 modules has a distinct `_LOD1` and
`_LOD2` duplicate, and every LOD keeps the body (10 objects) and equipment (6 objects) as
separate, correctly-named meshes.

---

## 3. Rig — actual bone count vs Unity Humanoid

- Armature `Champion_Vanguard_Rig` present, type ARMATURE.
- **22 edit-bones**, no orphan bones (every non-Hips bone has a parent).
- Unity Humanoid **required** set (20 bones): all present — Hips, Spine, Chest, UpperChest,
  Neck, Head, and Left/Right UpperArm, LowerArm, Hand, UpperLeg, LowerLeg, Foot, Toes.
- Optional shoulders (LeftShoulder, RightShoulder) also present → 22 total.
- 22 < 90 deformation bones. PASS.
- Skinning: every LOD0 mesh has an Armature modifier bound to the rig; **max 4 influences per
  vertex**; **no unweighted vertices** and no unskinned meshes. PASS.

Note: this is a **hand-authored, distance-weighted auto-skin** (nearest-bone falloff), not an
artist-corrected weight paint. It satisfies the numeric influence cap but is not a final
deformation rig — final rig requires user approval (matches the approval boundary).

---

## 4. Orientation, pivot, scale

- Root empty `root` at world origin (0.00000, 0.00000, 0.00000). PASS.
- Feet/ground at Z=0.0000 (world-space min body Z across all 16 LOD0 meshes). PASS.
- Total body height 1.7814 m (within the 1.75–1.9 m band). PASS.
- Facing: face mesh centroid (Y = -0.0918) is forward of the head centroid (Y = +0.0009),
  i.e. the character **faces -Y in Blender = +Z in Unity**. PASS.
- Units: METRIC / meters / scale_length 1.0. PASS (1 Unity unit = 1 m).

---

## 5. Deviations (honest gaps — require user approval)

1. **N-gon topology (not all-quads/tris).** LOD0 and LOD1 each contain 8 n-gons; LOD2 has 1.
   They are concentrated in the equipment: `Weapon_Main` (2), `Shield_Off` (2),
   `Realm_Ornament` (4→1). The body meshes are 100% clean (0 n-gons). N-gons will still
   triangulate on FBX/Unity import, but they are a production-topology smell — Unity's
   auto-triangulation of an n-gon is not artist-controlled and can differ from the preview.
   **Action:** retopologize the n-gons in Weapon_Main / Shield_Off / Realm_Ornament to quads
   before promotion, or accept Unity's triangulation after visual review.

2. **Head proportion ≈ 6.3:1, not 7.75:1.** Total height is correct (1.7814 m), but the head
   mesh (SM_Head) measures 0.283 m tall, giving a height-to-head ratio of **6.3** rather than
   the specified 7.75. The rig's Head bone (1.50→1.74 = 0.24 m) gives ~7.4:1, so the difference
   is partly mesh-vs-bone convention, but the head mesh reads larger than a canonical 7.75-head
   adult. **Action:** the user must confirm whether the head is oversized relative to the
   concept turnaround, or whether the extra height is helmet/hair volume that should be
   excluded from the anatomical head measure.

3. **Distance-weighted auto-skin is not a production deformation rig.** See §3. Fine for a
   placeholder/LOD candidate; not final.

These three items are inside the candidate's own stated approval boundary ("topology, rig,
textures require user approval before promotion"). None of them violate a hard numeric budget;
they are quality gates the user must clear.

---

## 6. Modular separation & body/equipment split

- 11 slot collections all present with correct lowercase kebab-case names and populated objects:
  head, hair, face (SM_Face + both eyes), torso, shoulders (L/R), arms (L/R), legs (L/R),
  cape, main-hand (Weapon_Main), off-hand (Shield_Off), realm-ornament (Realm_Ornament).
- `anchors` collection holds 5 anchor empties: PetAnchor, MountAnchor, VFX_ChestAnchor,
  VFX_Hand_L, VFX_Hand_R (re-parented to `root`).
- Body (10 `SM_*` meshes) and equipment (6 objects) are separate objects; shield and weapon are
  **not** fused into the body mesh. PASS.

---

## 7. Turntable captures

8 turntable frames at 45° increments (000/045/090/135/180/225/270/315) plus 4 LOD stills
(lod1/lod2 × front/threequarter) are present in `preview/`. PASS.

---

## 8. Approval checklist (user action required before promotion)

- [ ] **Proportion** — confirm the head reads as a 7.75-head adult (not oversized) against the
      Crownlands turnaround sheet; exclude helmet/hair volume from the anatomical head measure.
- [ ] **Topology** — approve or rework the 8 n-gons in Weapon_Main / Shield_Off / Realm_Ornament.
- [ ] **Rig** — approve the distance-weighted auto-skin as a placeholder, or commission artist
      weight-painting for a final deformation rig.
- [ ] **Face identity & anatomy** — approve face shape and anatomy (explicitly out of scope for
      this handoff per source docs).
- [ ] **UVs & textures** — approve/commission UV layout and final materials (current materials
      are neutral/placeholder only).
- [ ] **Animation** — approve or defer (rig not yet animation-validated).
- [ ] **Mobile performance** — run the LOD set on-device and validate the supplementary
      provisional envelope (inspection 60k / mobile-high 30–36k / normal 12–18k / far 3–6k)
      against measured frame cost.

---

## 9. Method note (honest validation)

This report was produced by an **independent headless Blender script** written for this task
(`tools/champion_vanguard_independent_validate.py`), which re-opened the delivered `.blend` and
re-derived every number from the live data-blocks. It did not reuse the producer's
`champion_vanguard_lod_rig_verify.py`. The producer's own claims (11,386 / 4,480 / 1,084 tris,
22 bones) were independently reproduced to the triangle, which is a strong cross-check. The
n-gon and head-proportion findings are *new* — the producer's verify script did not test for them.
