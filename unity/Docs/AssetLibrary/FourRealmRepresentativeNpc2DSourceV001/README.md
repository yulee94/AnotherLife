# Four-Realm Representative NPC 2D Source V001

Status: 2D production-source packet only. No mesh, rig, runtime prefab, terrain, world-map, or release authority is granted.

## Roster and identity lock

- Stonehold — `rct_stonehold_npc_service_v001`, Master Gruff — Stonehold Service Worker. A short, square, weathered forge steward whose layered apron, compact heat guards, cared-for beard, and period smithing tools communicate skilled structural craft rather than Champion rank.
- Eldergrove — `rct_eldergrove_npc_caretaker_v001`, Eldergrove Caretaker. A tall adult elf with calm posture, organic textile layering, bark-tanned leather, seed satchel, herb wraps, vials, and a closed care kit; no blade or ranger shorthand.
- Crownlands — `rct_crownlands_npc_service_v001`, Crownlands Service Worker. A disciplined adult civic clerk in practical tailored blue-and-ivory wool with geometric brass closures, ledger satchel, document tube, seal case, and keys; dignified but non-royal.
- Umbral — `rct_umbral_npc_archivist_v001`, Umbral Archivist. A lean mature dark elf with readable face and hands, ash cloth, restrained graveglass/fracture fittings, scroll cases, keys, and indexing pouches; eerie civic precision without assassin, warlock, or gore cues.

The lineup uses one front camera treatment and measured common scale. Realm recognition must survive labels, emission, and color reduction: Stonehold uses compressed mass and a forge apron; Eldergrove uses vertical organic layering and care satchels; Crownlands uses balanced tailored geometry and civic document gear; Umbral uses narrow layered negative space and archive modules.

## Modeling and modularity handoff

All four are intended for one Unity-Humanoid-compatible skeleton and bind-pose strategy, UV-stable adult base families, constrained face/body morphs, compact replicated parameters, and intentional body-mask seams. Catalog-backed profile IDs are preserved verbatim in the manifest. A missing catalog profile remains explicitly unresolved for downstream technical binding rather than receiving a fabricated ID.

Required separations:

- body/head/ears/hands/feet as anatomy modules appropriate to the realm base;
- hair, beard, and brows separate from skin and head geometry;
- torso base, sleeves, trousers, boots, gloves, belt, outer garment, and occupation gear as deliberate modules;
- rigid tools, keys, tubes, scroll cases, vials, clasps, and pouches separated from deforming cloth;
- body masks beneath layered clothing, with seams hidden under collars, cuffs, belts, boot tops, and garment overlaps;
- clean geometry may expose material masks and sockets only; particles, aura, smoke, glow trails, and spell effects remain separate runtime assets.

## 3D conversion risks

Generated views are production-readable 2D direction, not orthographic truth. The modeler must reconcile small cross-view differences in tool placement, garment overlap, braid/hair fall, shoulder depth, pouch thickness, finger spacing, facial asymmetry, and hidden back surfaces. Do not project one beauty image directly onto a fused mesh. Validate hand topology, shoulder clearance, elbow and knee range, boot volume, cloth thickness, beard/hair deformation, and prop collision before rig approval.

The concept images establish identity and material hierarchy. The front/back/left/right views establish visible construction intent. Neither proves topology, UVs, bind compatibility, facial deformation, collider coverage, texture packing, or performance.

## Runtime material and segmentation intent

Use a small Standard-lit PBR family with skin/eyes, hair, cloth/leather, and metal/mineral response kept materially distinct. Merge slots only when customization and material truth survive. Suggested authoring groups are:

- skin, eyes, and mouth;
- hair/brows/beard with alpha clipping only where silhouette value justifies it;
- primary cloth and secondary cloth/leather;
- rigid metal, stone, graveglass, botanical fasteners, and occupation props;
- optional restrained emissive mask channel reserved for later runtime authority, disabled in the clean base.

Cloth segmentation should support low-cost bone-driven fallback before simulation: coat tails, apron skirt, caretaker overskirt, and archivist robe panels. Hair and beard should use grouped cards or strips with a rigid/bone fallback. Long cloth and hair may not obscure hands, face, interaction sockets, or gameplay silhouette.

## Face and accessibility requirements

Preserve adult facial planes, age cues, natural asymmetry, readable eyes, and mouth shapes for blink/talk readiness. Stonehold beard volume must not erase jaw motion. Eldergrove and Umbral ears must remain compatible with hair and head modules. Umbral facial marking is a faint non-glowing localized accent, not a full-face effect.

At mobile gameplay distance, identity must remain legible by silhouette, material grouping, posture, and occupation equipment rather than color alone. Lower tiers remove micro-scratches, tiny fasteners, secondary hair cards, and nonessential pouch detail before protected realm and role cues. Reduced-motion and no-emission states retain the same identity.

## Platform intent

Mobile-floor uses the appropriate reduced LOD, packed 1K-class material sets where measured, opaque materials, simplified cloth/hair bones, and simple compound colliders. Mobile-high may retain selected 2K inspection surfaces and more facial/hair detail. PC-high may use the highest approved LOD and source texture detail, but cannot change identity, garment construction, or gameplay truth. Final triangle, bone, material, texture, collider, and facial budgets remain downstream measured gates.

## Approval boundary

The task owner delegated recommended 2D concept decisions for this bounded packet. The manifest records approval only after all selected PNGs are visible in the lineup/handoff sheets, packet validation passes, and independent review is clean. Approval does not authorize image-to-3D, Blender cleanup, Unity import, runtime activation, balance, narrative, terrain, or release.
