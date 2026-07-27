# Avian Soarer Executed Generation Prompts And Provenance

## Record State

- Issue/source: `#259`, `tdf-eco-soarer-2026-07-27-v001`
- Mode/generator: Codex terrestrial-design; Codex built-in image generation
- Model/version: unavailable to operator
- External, marketplace, named-IP, font, logo, or game-art inputs: none
- Editable source: unavailable; generator exposed raster PNG only
- User creative approval: not requested

Timestamps are immutable-output UTC save times. Prompts are retained as
executed. Generation does not grant approval.

## `tdf_generation_fauna_stonehold_rimefan_kite_turnaround_base_v001`

- UTC: `2026-07-27T01:35:46.8198891Z`
- Inputs: none
- Output: `tdf_asset_input_fauna_stonehold_rimefan_kite_turnaround_base_v001`
- Retained because it directly produced the selected refinement.

```text
Use case: stylized-concept
Asset type: AnotherLife terrestrial-design source sheet for 3D sculpt, rig, material, LOD, and gameplay-readability review
Primary request: create the exact first visual-source candidate for `tdf_fauna_stonehold_rimefan_kite`, a severe adult alpine cliff scavenger native to Stonehold
Scene/backdrop: landscape 3:2 professional zoological creature sheet on a perfectly plain neutral cool-gray seamless studio backdrop; no habitat scene
Subject: one consistent original creature identity across all views, with exactly two feathered wings, exactly two gripping legs, one short wedge tail, and one head. Wingspan is about 2.3 adult-human body heights. The defining body plan is a broad DIAMOND-shaped wing silhouette with strongly tapered tips, deep insulated chest, compact shoulders, short wedge tail, side-on cliff-bracing feet, pale hooked beak beneath a low continuous brow shield, and biologically small recessed eyes. It is an efficient wind-holding scavenger, not a heroic eagle.
Style/medium: premium feature-film creature-maquette realism, physically plausible avian anatomy and feather attachment, adult grounded dark-fantasy MMO source art, clean production turnaround rather than painterly scene art
Composition/framing: dominant full-body three-quarter view with wings extended enough to show the diamond plan; clean side perched view with hard folded wings; clean front and rear views; one solid black side-and-spread silhouette; one small neutral adult human scale silhouette; two restrained detail crops for brow/beak/feet and feather/down layering; one small motion inset showing a two-step launch hop into ridge-soar and a side-on cliff brace. Keep all wing tips, feet, beak, and tail fully inside frame with generous separation. Every view must depict the same proportions and damage pattern.
Lighting/mood: neutral broad studio key and controlled rim, readable roughness, no dramatic atmosphere
Color palette: blue-gray opaque feather masses, darker insulated down, pale worn keratin, restrained iron-dust staining at feet and beak
Materials/textures: practical layered feather groups, frayed high-wear edges, dense matte down, weathered keratin, scarred scale on feet; no glass feathers and no self-emission
Constraints: identity must read in pure black silhouette through the diamond wing plan, deep chest, short wedge tail, and cliff-bracing feet; exactly two wings and two legs in every view; adult anatomical consistency; no text, captions, labels, measurement marks, decorative border, presentation card, logo, signature, or watermark
Avoid: eagle, hawk, owl, condor, vulture copy; dragon or griffin anatomy; heroic heraldic pose; crown, armor, saddle, jewelry, runes, realm flag, magic, glow, snowstorm backdrop, cartoon, anime, mascot, juvenile face, oversized eyes, toy, figurine base, low-poly faceting, waxy plastic, cropped anatomy, extra wings or legs, long fan tail, inconsistent identity between views
```

## `tdf_generation_fauna_stonehold_rimefan_kite_turnaround_refinement_v001`

- UTC: `2026-07-27T01:37:52.8186070Z`
- Input: `tdf_asset_input_fauna_stonehold_rimefan_kite_turnaround_base_v001`
- Output: `tdf_asset_fauna_stonehold_rimefan_kite_turnaround_v001`

```text
Use case: precise-object-edit
Input images: Image 1 is the Rimefan Kite turnaround draft and the only edit target.
Primary request: refine this exact sheet into the accepted visual-source candidate for `tdf_fauna_stonehold_rimefan_kite` by removing its familiar eagle/raptor identity while preserving the same neutral studio layout, image quality, adult-human scale, blue-gray material family, all fully visible views, black silhouettes, detail crops, and launch/cliff-brace motion evidence.
Change consistently in every view:
- Replace the eagle-like head with a compact low flattened avian skull under one broad continuous weathered keratin brow shelf. Use tiny deeply recessed lateral eyes and a shorter, thicker pale hooked cutting beak with a blunt base; no eagle crown line, proud brow expression, or long raptor face.
- Make the spread wing plan unmistakably DIAMOND shaped: greatest chord and mass at mid-span, strongly tapered inner and outer edges, exactly five broad terminal silhouette-primary groups per wing, without long separated eagle fingers.
- Shorten the tail to one compact rigid wedge. It must never become a long fan.
- Deepen the insulated chest and shorten the visible neck.
- Make the feet short, broad cliff braces with exactly three forward toes and one powerful rear toe, not long eagle talons.
Preserve invariants: exactly two feathered wings, two legs, one head, one short wedge tail; same individual proportions, brow damage, feather wear, and colors in every view; all anatomy fully in frame; neutral broad studio light; no environmental backdrop.
Constraints: black silhouette must read through diamond wing, deep chest, short wedge tail, and broad brace feet; no text, labels, arrows, measurement marks, logo, signature, border, card, or watermark.
Avoid: eagle, hawk, owl, condor, vulture, falcon, griffin, dragon, heroic heraldic pose, crown, armor, runes, glow, snowstorm, cartoon, mascot, toy, extra anatomy, inconsistent views, cropped wing tips or feet.
```

## `tdf_generation_fauna_stonehold_rimefan_kite_motion_base_v001`

- UTC: `2026-07-27T01:39:17.8398336Z`
- Input: selected Rimefan turnaround
- Output: `tdf_asset_input_fauna_stonehold_rimefan_kite_motion_base_v001`
- Retained because it directly produced the selected refinement.

```text
Use case: stylized-concept
Input images: Image 1 is the exact identity reference for `tdf_fauna_stonehold_rimefan_kite`; preserve its proportions, broad keratin brow shelf, compact beak, blue-gray feather pattern, short wedge tail, broad brace feet, and damage/wear consistently.
Asset type: companion motion-and-material source sheet for 3D rigging, animation, material, LOD, and reduced-motion review
Primary request: create a new landscape 3:2 neutral cool-gray studio sheet focused on the same Rimefan Kite’s physically plausible movement and surface construction, not another species redesign.
Composition/framing: six clean sequential full-body motion studies with generous separation: settled ridge stand, anticipatory weight shift, two-step launch hop, first corrective downstroke, hoverless wind-hold glide, side-on cliff brace followed by hard fold and settled recovery. Include one top-down wing-spread anatomy study that clearly shows the diamond wing plan and exactly five broad terminal silhouette-primary groups per wing; one underside shoulder/elbow/wrist fold study; and three restrained material crops showing opaque feather layering, dense insulated down, weathered pale keratin brow/beak, and iron-dust foot wear. Include one small solid-black motion-key strip demonstrating anticipation, contact, direction, and recovery with decorative feather flutter removed.
Style/medium: premium feature-film creature-maquette realism and production animation study, physically plausible avian keel/shoulder/pelvis/wing-fold/contact, adult grounded dark-fantasy MMO art
Lighting/mood: neutral broad studio key, controlled rim, readable roughness; no dramatic environment
Constraints: same individual and same proportions in every pose; exactly two wings, two legs, one compact head, one short wedge tail; all wing tips and feet fully in frame; motion must show weight, ground contact, and recovery rather than spectacle; no text, captions, labels, arrows, measurement marks, logo, signature, card, border, or watermark
Avoid: eagle or owl redesign, long raptor fingers, long tail fan, hovering, magic, snowstorm, aura, particles, armor, cartoon, mascot, toy, low-poly, waxy plastic, duplicated limbs, inconsistent anatomy, cropped parts
```

## `tdf_generation_fauna_stonehold_rimefan_kite_motion_refinement_v001`

- UTC: `2026-07-27T01:42:21.9079861Z`
- Input: retained Rimefan motion base
- Output: `tdf_asset_fauna_stonehold_rimefan_kite_motion_material_v001`

```text
Use case: precise-object-edit
Input images: Image 1 is the Rimefan Kite motion/material companion sheet and the only edit target.
Primary request: preserve the exact sheet layout, studio background, motion sequence, material crops, creature identity, scale, lighting, and production realism, but correct three anatomy inconsistencies consistently across every pose and anatomy view.
1. Make every spread wing a true broad DIAMOND plan with the greatest chord at mid-span and strongly tapered leading and trailing edges. Consolidate the outer edge into exactly five broad terminal silhouette-primary GROUPS per wing; remove the many long eagle-like finger feathers.
2. Replace every long rounded tail fan with one very short compact wedge tail ending close behind the pelvis.
3. Keep the head compact under one low continuous keratin brow shelf, with tiny recessed eyes, a short thick beak, a deep insulated chest, and short broad cliff-bracing feet. Do not make it more eagle-like.
Preserve exactly two wings, two legs, one head, and one tail in every pose; keep all anatomy fully inside frame. The top-down dorsal and underside studies must match each other structurally. The motion strip must still show settled stand, weight shift, two-step launch, corrective downstroke, hoverless wind hold, cliff brace, hard fold, and recovery.
Constraints: no text, labels, arrows, measurement marks, logo, signature, border, card, or watermark.
Avoid: eagle/hawk/owl/condor/vulture wing fingers, long fan tail, heroic raptor face, extra anatomy, inconsistent primary count, cropped tips, magic, armor, cartoon, toy.
```

## `tdf_generation_fauna_crownlands_stormglass_swift_turnaround_v001`

- UTC: `2026-07-27T01:44:08.5267623Z`
- Inputs: none
- Output: `tdf_asset_fauna_crownlands_stormglass_swift_turnaround_v001`

```text
Use case: stylized-concept
Asset type: AnotherLife terrestrial-design turnaround/anatomy source sheet for 3D sculpt, rig, material, LOD, and gameplay-readability review
Primary request: create the exact first visual-source candidate for `tdf_fauna_crownlands_stormglass_swift`, a small severe adult storm-front aerial insectivore native to the Crownlands Meridian shelf
Scene/backdrop: landscape 3:2 professional zoological creature sheet on a plain neutral cool-gray seamless studio backdrop; no habitat scene
Subject: one consistent original small avian identity across every view, with exactly two feathered wings, exactly two narrow gripping legs, one head, and one single-root rigid fork tail. Wingspan about 0.95 adult-human body heights, perched height 0.22–0.30, body length 0.30–0.38. Defining body plan: very high-aspect continuous CRESCENT wing with swept leading edge and no more than three small terminal notches, compact deep chest, tiny recessed eyes, short narrow pressure-cutting beak, narrow shelf-gripping feet, and a short stiff tail that forks only after one clear root. It must read as an original swift-like pressure-line flier, never a small raptor or reduced Tempest Roc.
Style/medium: premium feature-film creature-maquette realism, physically plausible avian keel/shoulder/pelvis/wing-fold anatomy, adult grounded dark-fantasy MMO source art, clean production turnaround rather than painterly scene art
Composition/framing: dominant full-body three-quarter spread view; clean top-down full wingspread plan showing the continuous crescent; clean front spread; clean side folded/perched; rear folded view; solid black spread and folded silhouettes; small neutral adult human scale silhouette; underside shoulder-root and single-root tail-fork anatomy insets; two restrained material crops for opaque metallic edge feathers and rain-dark chest/keratin. Keep every wing tip, foot, beak, and tail point fully inside frame with generous separation. Every view depicts the same proportions and feather damage.
Lighting/mood: neutral broad studio key and controlled rim, readable roughness, no dramatic atmosphere
Color palette: charcoal feather mass, opaque desaturated metallic-blue edge feathers, pale weathered keratin, rain-dark chest down
Materials/textures: compact aerodynamic feather groups, matte body feathers, opaque metallic edge wear rather than glow, scarred small feet; no transparent feathers or self-emission
Constraints: identity reads in black silhouette through the continuous crescent wing, compact chest, and rigid fork tail; exactly two wings and two legs in every view; no more than three small terminal wing notches; no text, captions, labels, measurement marks, decorative border, presentation card, logo, signature, or watermark
Avoid: eagle, hawk, falcon, owl, swallow copy, Tempest Roc miniature, long raptor fingers, seven blade primaries, long crane legs, double-root tail fans, shield skull, lightning, static glow, armor, heraldry, runes, crown, cartoon, anime, mascot, juvenile face, oversized eyes, toy, low-poly, waxy plastic, cropped anatomy, extra wings/legs/tails, inconsistent views
```

## `tdf_generation_fauna_crownlands_stormglass_swift_motion_material_v001`

- UTC: `2026-07-27T01:45:34.0576298Z`
- Input: selected Stormglass turnaround
- Output: `tdf_asset_fauna_crownlands_stormglass_swift_motion_material_v001`

```text
Use case: stylized-concept
Input images: Image 1 is the exact identity reference for `tdf_fauna_crownlands_stormglass_swift`; preserve its compact body, continuous high-aspect crescent wings, single-root rigid fork tail, small head/beak, charcoal and opaque metallic-blue feather pattern, and feather damage consistently.
Asset type: companion motion-and-material source sheet for animation, rig, material, LOD, and reduced-motion review
Primary request: create a new landscape 3:2 neutral cool-gray studio sheet showing the same Stormglass Swift’s separately authored fast flight cadence and physical surface construction.
Composition/framing: seven clean sequential full-body motion studies with generous separation: shelf-edge wind brace, anticipatory chest compression, three short acceleration strokes represented by distinct key poses, long pressure-line bank, fast controlled stoop, tail-fork braking turn, narrow-foot shelf perch and settled recovery. Include a full top-down continuous-crescent wing plan and an underside keel/shoulder/elbow/wrist/fold study; a close tail-root study proving one shared root before the rigid fork; three material crops showing matte charcoal feather mass, opaque desaturated metallic-blue edge feathers, pale keratin, rain-dark chest down, and small scarred gripping feet. Include a small solid-black reduced-motion key strip preserving anticipation, flight direction, braking, contact, and recovery while removing feather flutter and weather effects.
Style/medium: premium feature-film creature-maquette realism and production animation study, physically plausible adult avian anatomy, grounded dark-fantasy MMO source art
Lighting/mood: neutral broad studio light, readable roughness, no storm environment
Constraints: same individual, proportions, fork, feather wear, and colors in every pose; exactly two wings, two legs, one head, and one tail; high-aspect crescent with no more than three small terminal notches and no long raptor fingers; all anatomy fully inside frame; no text, captions, labels, arrows, measurement marks, logo, signature, border, presentation card, or watermark
Avoid: eagle, hawk, falcon, swallow copy, reduced Tempest Roc, seven blade primaries, long crane legs, double-root rudder fans, shield skull, lightning, glow, particles, hovering, armor, heraldry, cartoon, mascot, toy, extra anatomy, inconsistent views, cropped tips
```

## `tdf_generation_fauna_umbral_sootsail_carrioner_turnaround_base_v001`

- UTC: `2026-07-27T01:46:57.0902737Z`
- Inputs: none
- Output: `tdf_asset_input_fauna_umbral_sootsail_carrioner_turnaround_base_v001`
- Retained because it directly produced the selected refinement.

```text
Use case: stylized-concept
Asset type: AnotherLife terrestrial-design turnaround/anatomy source sheet for 3D sculpt, rig, material, LOD, and gameplay-readability review
Primary request: create the exact first visual-source candidate for `tdf_fauna_umbral_sootsail_carrioner`, a severe adult ash-terrace scavenger native to Umbral’s Three-Fault Rift
Scene/backdrop: landscape 3:2 professional zoological creature sheet on a plain neutral cool-gray seamless studio backdrop; no habitat scene, smoke, or ash storm
Subject: one consistent original avian identity across all views, with exactly two feathered wings, exactly two heavy load-bearing legs, one head, and one tail. Wingspan about 2.0 adult-human body heights, grounded height 0.70–0.85, total length 1.0–1.2. Defining body plan: broad nearly straight PLANK-like wings with four broad terminal feather groups, deep keel chest, low hooded skull with tiny recessed eyes, broad glass-dark beak, heavy terrace-bracing feet with two dominant forward and two rearward contact toes, and one tail root that stays solid near the body before splitting distally into a long restrained V. It is a grounded heavy-launch scavenger, not a naked vulture, demon bird, bat, or reduced Tempest Roc.
Style/medium: premium feature-film creature-maquette realism, physically plausible adult avian keel/shoulder/pelvis/wing-fold and ground-contact anatomy, severe grounded dark-fantasy MMO source art, clean production turnaround rather than painterly scene art
Composition/framing: dominant full-body grounded three-quarter view with wings partly mantled; clean top-down full wingspread plan showing the nearly straight plank; clean front spread; clean side folded/standing; clean rear folded; solid black spread and folded silhouettes; one small neutral adult human scale silhouette; wing underside and shoulder-root inset; tail-root inset proving one root and only distal split; two restrained material crops for hooded facial keratin/beak and matte opaque feather/ash-worn feet. Keep every wing tip, toe, beak, and tail point fully inside frame with generous separation. Every view depicts the same proportions, facial scarring, feather damage, and tail split.
Lighting/mood: neutral broad studio key and controlled rim, readable matte roughness, no dramatic atmosphere
Color palette: matte charcoal feathers, pale scarred facial keratin, glass-dark beak, ash-gray legs and foot scales; no self-emission
Materials/textures: broad opaque feather groups, worn matte body plumage, callused terrace-contact feet, scarred face keratin; no transparent feathers, exposed ribs, or smoke aura
Constraints: identity reads in black silhouette through the straight plank wing, low hooded head, deep chest, heavy feet, and long distally split tail; exactly two wings and two legs in every view; exactly four broad terminal feather groups per wing; no text, captions, labels, measurement marks, decorative border, presentation card, logo, signature, or watermark
Avoid: bald/naked vulture caricature, eagle, raven copy, demon horns, skull face, undead ribs, bat membrane, dragon fingers, Tempest Roc miniature, long crane legs, seven blade primaries, two separately rooted rudder fans, armor, heraldry, rune, glow, smoke, magic, cartoon, anime, mascot, juvenile face, oversized eyes, toy, low-poly, waxy plastic, cropped anatomy, extra wings/legs/tails, inconsistent views
```

## `tdf_generation_fauna_umbral_sootsail_carrioner_turnaround_refinement_v001`

- UTC: `2026-07-27T01:48:13.6855152Z`
- Input: retained Sootsail turnaround base
- Output: `tdf_asset_fauna_umbral_sootsail_carrioner_turnaround_v001`

```text
Use case: precise-object-edit
Input images: Image 1 is the Sootsail Carrioner turnaround draft and the only edit target.
Primary request: preserve the exact sheet layout, plank-wing body, deep keel, heavy grounded posture, long single-root/distally split tail, matte charcoal materials, human scale cue, silhouettes, detail-crop positions, and production quality, but remove the familiar bald-vulture identity and correct the terrace feet consistently in every view.
Head correction: replace the naked vulture face and long hooked beak with an original LOW HOODED SKULL. Dense short charcoal facial feathering must form a low continuous hood over the crown and cheeks. Under it, show only a compact pale scarred keratin brow-nasal shield surrounding tiny deeply recessed lateral eyes and paired pressure pits. Use a short broad glass-dark wedge beak with a blunt crushing base and only a restrained terminal hook. No bald neck, naked face mask, wrinkled vulture skin, eagle brow, horn, or skull-face pattern.
Foot correction: each leg ends in a broad terrace-bracing foot with four heavy contact toes arranged as two dominant forward and two rearward; preserve this same arrangement in front, side, rear, dominant, and detail views.
Preserve invariants: exactly two wings, two legs, one head, one tail; nearly straight plank wings with four broad terminal groups; one tail root splitting only distally into a long V; all parts fully in frame; same scars and feather damage across views.
Constraints: no text, labels, arrows, measurements, logo, signature, border, card, or watermark.
Avoid: bald or naked vulture, eagle, raven, demon bird, skull face, undead ribs, bat, dragon, Tempest Roc miniature, extra anatomy, cropped tips, glow, smoke, armor, cartoon, toy.
```

## `tdf_generation_fauna_umbral_sootsail_carrioner_motion_material_v001`

- UTC: `2026-07-27T01:49:45.6567798Z`
- Input: selected Sootsail turnaround
- Output: `tdf_asset_fauna_umbral_sootsail_carrioner_motion_material_v001`

```text
Use case: stylized-concept
Input images: Image 1 is the exact identity reference for `tdf_fauna_umbral_sootsail_carrioner`; preserve its low feathered hood skull, compact brow-nasal shield, plank wings, deep chest, heavy feet, long single-root/distally split tail, matte charcoal materials, scars, and feather damage consistently.
Asset type: companion motion-and-material source sheet for animation, rig, material, LOD, and reduced-motion review
Primary request: create a new landscape 3:2 neutral cool-gray studio sheet showing the same Sootsail Carrioner’s deliberately heavy flight cadence and grounded terrace behavior.
Composition/framing: eight clean full-body motion key studies with generous separation: slow thermal-circle bank, controlled side-slip descent, low two-foot landing brace, ground mantle over a neutral low object without gore, settled weight shift, three-step heavy running launch shown as distinct contact keys, two forceful first downstrokes, long recovery glide, and folded recovery stance. Include a full top-down nearly straight plank-wing plan with four broad terminal groups per wing; underside keel/shoulder/elbow/wrist study; tail-root study showing one root before distal V split; four material crops showing short hood feathers over compact facial keratin, glass-dark wedge beak, matte feather/ash wear, and callused two-forward/two-rear terrace feet. Include a small black reduced-motion key strip preserving anticipation, load, direction, contact, and recovery while removing feather flutter and debris.
Style/medium: premium feature-film creature-maquette realism and production animation study, physically plausible adult avian anatomy, severe grounded dark-fantasy MMO source art
Lighting/mood: neutral broad studio light, readable matte roughness; no smoke, ash storm, dramatic environment, or gore
Constraints: same individual, proportions, hood, facial scars, tail split, feather wear, and colors in every pose; exactly two wings, two legs, one head, and one tail; all anatomy fully in frame; motion reads heavy and controlled, unlike Rimefan or Stormglass; no text, captions, labels, arrows, measurement marks, logo, signature, border, card, or watermark
Avoid: bald vulture, eagle, raven copy, demon bird, skull face, undead ribs, bat, dragon, reduced Tempest Roc, long crane legs, separate double tail roots, glow, smoke aura, magic, armor, cartoon, mascot, toy, extra anatomy, inconsistent identity, cropped tips
```

## `tdf_generation_shared_avian_soarer_scale_silhouette_master_v001`

- UTC: `2026-07-27T01:52:43.4291898Z`
- Inputs: the three selected turnarounds plus existing generated-original
  `tdf_asset_boss_crownlands_meridian_tempest_roc_concept_v002`
- Output: `tdf_asset_shared_avian_soarer_scale_silhouette_master_v001`

```text
Use case: stylized-concept
Input images: Image 1 is the Rimefan Kite identity reference; Image 2 is the Stormglass Swift identity reference; Image 3 is the Sootsail Carrioner identity reference; Image 4 is the existing Meridian Tempest Roc comparison anchor only. Do not redesign any identity and do not merge their anatomy.
Asset type: AnotherLife shared avian-soarer true-scale, normalized-silhouette, and anti-palette-swap review master
Primary request: create a landscape 3:2 neutral light-gray production comparison sheet that accurately preserves the four referenced creature identities and proves the three supporting fauna remain distinct from each other and from the colossal Roc.
Composition/framing:
- Upper band: clean true-scale side and wingspread silhouettes beside one identical neutral adult-human silhouette. Stormglass has 0.95-human wingspan; Sootsail 2.0; Rimefan 2.3; the Meridian Tempest Roc is represented only as a much larger thin neutral-gray outline fragment/scale bracket implying 18.0-human wingspan, never resized to look comparable.
- Middle band: equal-width normalized solid-black wingspread silhouettes, ordered Rimefan diamond/short wedge tail, Stormglass continuous crescent/rigid fork tail, Sootsail nearly straight plank/long distal split tail. Include matching folded/grounded black silhouettes below each.
- Lower band: two repeated miniature silhouette rows representing 96-pixel and 64-pixel review size, with clean spacing and no habitat, color, particles, or weather.
- One small neutral material swatch strip showing the shared opaque-feather/keratin grammar without painting all three the same.
Style/medium: precise high-end creature-design comparison plate, clean studio presentation, crisp silhouette edges, production-ready visual QA evidence
Constraints: preserve each reference’s head, wing plan, chest, foot, and tail identity; exactly two wings and two legs per bird; black silhouettes must be readable without color; all elements fully in frame; no text, letters, numbers, captions, labels, arrows, logos, borders, decorative cards, signatures, or watermark
Avoid: blending species, recoloring one topology three ways, shrinking the Roc into a supporting creature, changing tail classes, identical wing plans, fantasy environment, glow, lightning, smoke, snow, UI chrome, cropped silhouettes
```

## `tdf_generation_shared_avian_soarer_rig_lod_readability_v001`

- UTC: `2026-07-27T01:54:35.8772181Z`
- Inputs: the three selected turnarounds
- Output: `tdf_asset_shared_avian_soarer_rig_lod_readability_v001`

```text
Use case: scientific-educational
Input images: Image 1 is the Rimefan Kite identity reference; Image 2 is the Stormglass Swift identity reference; Image 3 is the Sootsail Carrioner identity reference. Preserve each identity and never blend their topology or proportions.
Asset type: visual-only shared rig-grammar, fold, LOD, and material-reuse concept plate for AnotherLife terrestrial design; this is not a runtime rig specification
Primary request: create a precise landscape 3:2 neutral light-gray production study demonstrating how the three distinct avian bodies can share semantic control grammar and surface libraries while retaining different anatomy, wing plans, tail classes, scales, and motion cadence.
Composition/framing: three aligned vertical columns, left Rimefan diamond wing/short wedge tail, center Stormglass crescent wing/rigid fork tail, right Sootsail plank wing/long distal split tail.
- Top row: full top-down spread views with subtle non-text joint markers at keel, shoulder, elbow, wrist, pelvis, tail root, and foot contacts. Use thin neutral articulation guide lines only; preserve real feather mass and exact body silhouettes.
- Second row: clean folded side and grounded three-quarter states showing believable wing-fold stack, shoulder root, pelvis, and perch/brace contact.
- Third row: four progressively simplified visual representations per creature: detailed grouped-feather source, reduced grouped-feather source, low-detail solid body/major silhouette-feather source, and distant pure-black silhouette. Do not make the three columns converge into one model.
- Bottom row: shared opaque-feather micro-surface, keratin, packed roughness, and per-creature weather-mask swatches, visually separated from creature geometry; one neutral adult-human scale cue.
Style/medium: premium zoological concept-design diagram with feature-film creature-maquette realism, crisp clean studio presentation, precise spacing, readable anatomical intent
Color palette: restrained gray anatomy guides; Rimefan blue-gray matte, Stormglass charcoal with opaque muted metallic-blue edge, Sootsail matte charcoal with pale compact facial keratin; no glowing joints
Constraints: exactly two wings and two legs per bird; Rimefan, Stormglass, and Sootsail remain visibly different at every simplification tier; major silhouette feathers remain geometry; no transparent fuzz dependence; no text, letters, numbers, captions, labels, arrows, UI, logo, signature, decorative border, presentation card, or watermark
Avoid: identical topology, palette-swap presentation, blending species, skeleton fantasy, humanoid shoulders, extra limbs, bat membranes, dragon fingers, armor, heraldry, runes, emissive rig dots, environment scenes, cropped anatomy
```

## Provenance Statement

No third-party image, font, logo, marketplace preview, game art, or named-IP
reference was supplied to base generation. Refinements use only their listed
generated-original inputs. The shared scale sheet also uses the existing
generated-original Roc `v002` only as a no-reuse comparison.

The generator and prompts are retained here; the model was not exposed to the
operator. Selected outputs and directly used inputs have immutable identities
in the manifest. This is technical provenance, not a legal conclusion or user
creative approval.
