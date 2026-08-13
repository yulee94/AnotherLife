# Champion Character Sheets — Blender Handoff

**Status:** Owner-approved visual source direction; ready for modeling reference, not final production-model approval

**Scope:** The shared four-realm Vanguard anchor and one multi-angle Vanguard sheet per realm. Class-specific character sheets are not part of this first handoff.

## Download the source sheets

The images are canonical Git LFS assets already stored in this repository. Do not create duplicate PNG copies elsewhere in the project.

| Sheet | Purpose | Source |
| --- | --- | --- |
| Four-realm Champion anchor | Compare shared proportions and realm identity before choosing a modeling target | [`champion_four_realm_anchor_v001.png`](../Assets/AL/Art/Champions/ConceptSheets/champion_four_realm_anchor_v001.png) |
| Stonehold Vanguard | Front, three-quarter, side, back, and material reference | [`champion_stonehold_vanguard_turnaround_v001.png`](../Assets/AL/Art/Champions/ConceptSheets/champion_stonehold_vanguard_turnaround_v001.png) |
| Eldergrove Vanguard | Front, three-quarter, side, back, and material reference | [`champion_eldergrove_vanguard_turnaround_v001.png`](../Assets/AL/Art/Champions/ConceptSheets/champion_eldergrove_vanguard_turnaround_v001.png) |
| Crownlands Vanguard | Front, three-quarter, side, back, and material reference | [`champion_crownlands_vanguard_turnaround_v001.png`](../Assets/AL/Art/Champions/ConceptSheets/champion_crownlands_vanguard_turnaround_v001.png) |
| Umbral Vanguard | Front, three-quarter, side, back, and material reference | [`champion_umbral_vanguard_turnaround_v001.png`](../Assets/AL/Art/Champions/ConceptSheets/champion_umbral_vanguard_turnaround_v001.png) |

For a normal clone, install Git LFS and retrieve the PNGs:

```bash
git lfs install
git lfs pull --include="unity/Assets/AL/Art/Champions/ConceptSheets/*.png"
```

The companion [`champion-character-sheets-blender-handoff.v1.json`](champion-character-sheets-blender-handoff.v1.json) records every canonical path, pixel size, SHA-256 digest, and approval boundary.

## Modeling direction

- Use the shared adult Vanguard envelope: approximately `7.75` heads tall with realistic, mobile-readable anatomy.
- Preserve a common body and rig envelope across all four realm variants. Realm identity comes from armor, equipment, materials, silhouette, and restrained magical focal points.
- Keep the head, hair, face, chest, shoulders, arms, legs, cape or mantle, main hand, off hand, and realm ornament separable for later modular customization.
- Build large silhouette and armor masses first. Treat engraving, scratches, weave, pores, pitting, and fine growth detail as texture or material information rather than default geometry.
- Keep the pivot at ground center and the final Unity import facing positive Z at a scale of one Unity unit per meter.
- Model the body and equipment as separate reviewable components. The shield and weapon obscure parts of the concept views and should not be fused into the body mesh.

## Production targets

| Level | Triangle intent |
| --- | ---: |
| Player high detail | `8k–18k` |
| Medium LOD | `3k–6k` |
| Low LOD | `800–1,500` |
| Far crowd | Banner, silhouette, realm icon, or colored marker |

Use a Unity Humanoid-compatible skeleton and shared skeleton compatibility across body variants. Final texture sizes, material packing, shader features, rig, topology, and LOD thresholds require measured mobile validation before approval.

## Source authority and cautions

- Visual-design authority: [`FourRealmChampionAnchor.md`](../Assets/AL/Art/Designs/FourRealmChampionAnchor.md)
- Modular-character direction: [`ModularChampionCustomization.md`](../Assets/AL/Art/Designs/ModularChampionCustomization.md)
- Anchor provenance: [`Champion_Anchor_Source_Prompts_And_Provenance.md`](../Assets/AL/Art/Champions/ConceptSheets/Champion_Anchor_Source_Prompts_And_Provenance.md)
- Turnaround provenance: [`Champion_Turnaround_Source_Prompts_And_Provenance.md`](../Assets/AL/Art/Champions/ConceptSheets/Champion_Turnaround_Source_Prompts_And_Provenance.md)

These AI-assisted sheets establish approved visual direction, not exact orthographic truth. Cross-view anatomy, hidden surfaces, equipment separation, final face identity, topology, UVs, textures, rigging, animation, and measured device performance still require artist correction and review. A Blender model made from this packet is a production candidate until the user approves the resulting sculpt and turntable.
