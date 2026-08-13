# Champion Character Sheets — Blender Handoff

**Status:** Owner-approved visual-source navigation; Blender execution and modeling remain held pending an item-specific topology, body-compatibility, and technical authorization

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
- Compare all four realm variants against the one common body and approximate rig envelope used by these sheets. That envelope isolates realm design only; it does not approve a production body range or body-preset compatibility. Realm identity comes from armor, equipment, materials, silhouette, and restrained magical focal points.
- Keep the head, hair, face, chest, shoulders, arms, legs, cape or mantle, main hand, off hand, and realm ornament separable for later modular customization.
- Build large silhouette and armor masses first. Treat engraving, scratches, weave, pores, pitting, and fine growth detail as texture or material information rather than default geometry.
- Keep the pivot at ground center and the final Unity import facing positive Z at a scale of one Unity unit per meter.
- Model the body and equipment as separate reviewable components. The shield and weapon obscure parts of the concept views and should not be fused into the body mesh.

## Topology authority boundary

This navigation handoff does not select or approve a numeric topology budget. Its two design authorities describe distinct deliverables that must not be collapsed into one machine-readable production envelope:

- [`FourRealmChampionAnchor.md`](../Assets/AL/Art/Designs/FourRealmChampionAnchor.md) records a provisional inspection-to-far presentation ladder for the approved realm anchor.
- [`ModularChampionCustomization.md`](../Assets/AL/Art/Designs/ModularChampionCustomization.md) records a lower mobile-first direction for a later modular runtime derivative.

Before any Blender construction begins, an item-specific A1 handoff must name the intended deliverable (`source-review model` or `runtime derivative`), the controlling authority, the numeric target and outer ceiling, material and texture budgets, validation distance, and measured device gate. If that handoff is absent or the two authorities conflict for the proposed output, stop; do not infer that the lower ladder caps the source model or that the source-review ladder approves a Player asset.

Unity Humanoid compatibility and shared-skeleton compatibility remain future technical requirements, not approvals in this packet. Final texture sizes, material packing, shader features, rig, topology, derivative strategy, and LOD thresholds require separate engineering authorization and measured mobile validation.

## Body and runtime exclusions

The shared body visible in these sheets exists only to compare realm silhouettes and equipment on a consistent anchor. This handoff does not approve or bind:

- body-preset shapes, body-type range, or preset-to-mesh mapping;
- blendshape, morph-target, deformation, or fitting strategy;
- compatibility across body scales, armor, animation, colliders, or the shared skeleton;
- first-user test-mode appearance, customization runtime, production save data, or Player/runtime routing.

Those decisions require their own approved compatibility source and engineering handoff. A model or manifest produced from these sheets must not be presented as the current first-user body-preset visual correction.

## Source authority and cautions

- Visual-design authority: [`FourRealmChampionAnchor.md`](../Assets/AL/Art/Designs/FourRealmChampionAnchor.md)
- Modular-character direction: [`ModularChampionCustomization.md`](../Assets/AL/Art/Designs/ModularChampionCustomization.md)
- Anchor provenance: [`Champion_Anchor_Source_Prompts_And_Provenance.md`](../Assets/AL/Art/Champions/ConceptSheets/Champion_Anchor_Source_Prompts_And_Provenance.md)
- Turnaround provenance: [`Champion_Turnaround_Source_Prompts_And_Provenance.md`](../Assets/AL/Art/Champions/ConceptSheets/Champion_Turnaround_Source_Prompts_And_Provenance.md)

These AI-assisted sheets establish approved visual direction, not exact orthographic truth. Cross-view anatomy, hidden surfaces, equipment separation, final face identity, body compatibility, topology, UVs, textures, rigging, animation, and measured device performance still require artist correction and review. This document alone does not authorize a Blender run. Any later authorized model remains a private review candidate until its exact source, sculpt, turntable, technical evidence, and user-owned visual gate are dispositioned.
