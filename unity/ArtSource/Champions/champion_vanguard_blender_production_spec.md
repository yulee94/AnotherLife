# Champion Vanguard — Blender Production Spec (extracted)

**Purpose:** Single authoritative checklist of hard requirements for the shared
four-realm Champion Vanguard, extracted for downstream modeling tasks.
This sidecar travels with `champion_vanguard_working_v001.blend`.

**Extraction date:** 2026-08-18

**Sources (read in full):**
- `unity/Docs/Champion_Character_Sheets_Blender_Handoff.md` (55 lines)
- `unity/Assets/AL/Art/Designs/ModularChampionCustomization.md` (114 lines)
- `unity/Assets/AL/Art/Designs/FourRealmChampionAnchor.md` (254 lines)
- `unity/Docs/champion-character-sheets-blender-handoff.v1.json` (81 lines)
- `unity/Assets/AL/Scripts/ChampionMode/Customization/ProceduralChampionModelBuilder.cs` (861 lines; part-naming reference)
- `unity/Assets/AL/Art/Champions/ConceptSheets/champion_crownlands_vanguard_turnaround_v001.png` (present; 1635×962, SHA-256 `b6fd94b914d7ce90d245865808b47bc216ebde1468f6892e327ac552a506c5b4`)

## 1. Scene units & scale

- [ ] **1 Unity unit = 1 meter** (scale factor 1.0; no `0.01`/`100` scale hacks).
- [ ] Blender scene: Unit System = **Metric**, Length Unit = **Meters**, Unit Scale = **1.0**.
      (The working file is pre-set this way; keep it when authoring.)
- [ ] 7.75-heads-tall adult: overall body height ≈ 1.75 m to 1.9 m in meters, with the
      head roughly 1/7.75 of total height. Verify with the reference figure before modeling.

## 2. Orientation & pivot

- [ ] **Forward = +Z** for the final Unity import.
- [ ] In Blender, author the character **facing -Y** (Blender's front axis). The standard
      Blender→FBX→Unity conversion maps Blender -Y to Unity +Z and Blender +Z(up) to Unity +Y(up).
      The `FORWARD_Unity+Z` empty in the scene marks this facing.
- [ ] **Pivot at ground center**: root object at world origin `(0, 0, 0)`, feet/ground at `Z=0`.
- [ ] All modules parented under the shared `root` so they share the ground-center pivot.

## 3. Rig requirements (Unity Humanoid)

- [ ] **Unity Humanoid-compatible** skeleton (retargetable via Unity's Humanoid Avatar).
- [ ] **Shared skeleton** across all body/realm variants (one rig envelope, four realms).
- [ ] Begin under **90 deformation bones**.
- [ ] **≤ 4 bone influences per vertex** (fewer where deformation allows).
- [ ] Rig is a production candidate only — final rig requires user approval.

## 4. Modular slot list (11 required collections)

Collections in the working file use these exact lowercase kebab-case names:

1. `head`
2. `hair`
3. `face`
4. `torso`
5. `shoulders`
6. `arms`
7. `legs`
8. `cape`
9. `main-hand`
10. `off-hand`
11. `realm-ornament`

Plus `anchors` (non-slot): `PetAnchor`, `MountAnchor`, `VFX_ChestAnchor`,
`VFX_Hand_L`, `VFX_Hand_R` (from the runtime model builder).

**Mapping to source docs** (reconcile names when wiring to runtime):
- handoff "chest" / modular-doc "ArmorChest" → `torso`
- handoff "main hand" / modular-doc "WeaponMain" → `main-hand`
- handoff "off hand" / modular-doc "WeaponOff" → `off-hand`
- handoff "realm ornament" → `realm-ornament`
- modular-doc extras not in the 11: `BodyPreset`, `Eyes`, `Skin`, `Gloves`, `Boots`,
  `BackAttachment`, `PetAnchor`, `MountAnchor` (map into the 11 + `anchors` as needed).

## 5. Naming conventions

- **Blender collections**: lowercase kebab-case (the 11 slot names above).
- **Runtime part names** (ProceduralChampionModelBuilder, PascalCase + `_` sub-parts):
  `Skin_Head`, `Hair_Short`, `ChestArmor`, `Shoulder_L`, `Armor_UpperArm_L`, `Glove_L`,
  `Boot_L`, `Cape`, `Weapon_Main`, `Shield_Off`, `Helmet`, `BackAttachment`, `FaceMark`.
- **Unity mesh assets**: `M_<Realm>_<Family>_LOD<n>_L<nn>` (e.g. `M_Crownlands_ChampionVanguard_LOD0_L01`).
- **Project data IDs / JSON keys**: lowercase snake_case. C# identifiers: PascalCase.

## 6. LOD triangle budgets

| Level | Triangle intent |
| --- | ---: |
| Player high detail | 8,000 – 18,000 |
| Medium LOD | 3,000 – 6,000 |
| Low LOD | 800 – 1,500 |
| Far crowd | banner / silhouette / realm icon / colored marker |

Supplementary provisional envelope (FourRealmChampionAnchor.md) — validate on device:
inspection LOD0 up to 60k, mobile-high 30–36k, mobile-normal 12–18k, far 3–6k or impostor.

## 7. Modeling rules (hard)

- [ ] **Body and equipment as separate reviewable components.** Shield and weapon must
      **not** be fused into the body mesh.
- [ ] Preserve a **common body + rig envelope** across all four realms; realm identity comes
      from armor, equipment, materials, silhouette, and restrained magical focal points only.
- [ ] **Large silhouette/armor masses first**; engraving, scratches, weave, pores, pitting,
      and fine growth detail belong in texture/material (normal/color), not default geometry.
- [ ] Realistic adult anatomy at 7.75 heads; athletic but **not exaggerated** shoulders,
      waist, chest, hips, hands, or weapons.
- [ ] Face visible and naturally proportioned (reads as a person before equipment).
- [ ] Armor leaves believable room for movement at neck, shoulders, elbows, waist, hips,
      knees, ankles.
- [ ] `70 / 20 / 10` value hierarchy; ≤ 3 dominant material families in the primary read.
- [ ] Emission: one primary + at most one secondary focal area; magic must not outline edges.
- [ ] Realm recognition must survive grayscale, reduced particles/texture/emission.
- [ ] Avoid: thin dangling chains, dense layered belts, fragile spikes, loose floor-length
      capes, excessive feathers, floating ornaments, transparency-dependent identity.
- [ ] Cape: **short skinned mantle or rigid segmented cape** (no full cloth simulation).
- [ ] Keep colliders/gameplay hit volumes separate from visual topology.
- [ ] Materials: **neutral/placeholder only** for modeling; do not finalize textures.
- [ ] Lower LODs remove tertiary ornament, interior layers, small fasteners, secondary hair
      cards, and non-protected emission geometry first.

## 8. Crownlands Vanguard (realm reference)

- Silhouette: balanced, upright, heraldic, authoritative (classical heroic read).
- Construction: engineered plate, disciplined panel breaks, tailored textile layers,
  tall practical collar, large heraldic surfaces reserved for approved symbols.
- Materials: aged silver / polished steel, royal-blue textile, dark leather, restrained
  gold/brass engraving.
- Palette: steel, midnight royal blue, parchment, restrained gold, indigo-celestial accent.
- Equipment: straight longsword + proportionate **kite shield** with clean central field.
- Magic: celestial authority through shield center, weapon fuller, or single chest emblem.
- Protected mobile cues: balanced shoulder line, kite shield, blue textile block,
  controlled precious-metal highlight.
- Avoid: generic pristine paladin, excessive gold, copied real-world heraldry, filigree.

## 9. Approval boundary

This scaffold and any model made from it is a **production candidate only**. Final face
identity, production topology, UVs, textures, rig, animation, and measured mobile
performance require **user approval** before promotion.
