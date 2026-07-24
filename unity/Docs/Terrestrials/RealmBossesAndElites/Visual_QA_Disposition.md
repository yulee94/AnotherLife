# Realm Boss And Elite Visual QA Disposition

## Scope And State

- Issue: #259
- Source version: `tdf-rbe-2026-07-24-v001`
- Review mode: Codex terrestrial-design
- Review date: 2026-07-24
- User creative approval: not requested
- Runtime integration approval: blocked
- Production model approval: blocked

This is a direct pixel review of the retained 1536 x 1024 PNG source sheets. The inspection used realm contact sheets at one-half linear resolution plus targeted full-resolution review for anatomy and identity concerns. It is not a review of a rig, model, normal-speed animation, material graph, VFX system, Unity import, or Player build. A `ProvisionalPass` means the raster source is coherent enough for user review. `PassWithConcern` means it remains reviewable but the named issue must be resolved before production modeling. `HoldForRefinement` means the current pixels must not become the unchanged production identity.

## Global Checks

- All sixteen final sheets are present, decode as PNG, and measure 1536 x 1024.
- No retained final sheet contains a visible title, caption, logo, signature, watermark, decorative UI, or presentation card.
- Every sheet provides a dominant body view, supporting views, black silhouette evidence, a human scale cue, material closeups, and at least one motion/contact sequence.
- The adult dark-fantasy tone, physical material response, and grounded contact are materially stronger than the rejected childlike landing-image direction.
- Multi-view consistency remains concept-art evidence only. Exact limb, horn, plate, membrane, scar, and scale continuity must be rebuilt and verified in production topology.
- None of the generated sheets may be shipped unchanged as a game model, texture, loading cinematic frame, or purchase-ready asset.

## Stonehold

| Profile | Disposition | Pixel Findings | Required Follow-Up |
| --- | --- | --- | --- |
| `tdf_boss_stonehold_fault_crowned_colossus` | `PassWithConcern` | Six-leg footprint, low shield mass, broken plow horn, scale cue, and slate-hide material hierarchy read clearly. The frontal skull can still be interpreted as rhinoceros-derived. | Preserve the three distinct limb-pair attachment zones and redesign the nasal growth bed in production sculpt so the skull does not collapse into an enlarged real animal. |
| `tdf_elite_stonehold_rimehorn_breaker` | `ProvisionalPass` | High shoulder, split shovel horn, narrow rear drive, snow contact, and coarse guard-hair hierarchy remain readable in silhouette and grayscale. | Verify horn root, central notch, and braking load in orthographic sculpt and normal-speed motion. |
| `tdf_elite_stonehold_oreblind_delver` | `ProvisionalPass` | Eyeless wedge skull, layered sensory plates, compact tail, and all three digging-limb pairs are directly readable. The cleanup removed the accidental measurement annotation. | Lock the six-limb skeletal spacing before retopology; keep sensory plates separate from armor and VFX authority. |
| `tdf_elite_stonehold_slaghide_gorer` | `PassWithConcern` | Asymmetric tusks and the fused vitrified dorsal shield make a strong contact silhouette, but the underlying suid/boar ancestry remains obvious. | Push skull, shoulder, and gait proportions farther from a normal boar during production sculpt while preserving the accepted shield and tusk asymmetry. |

## Eldergrove

| Profile | Disposition | Pixel Findings | Required Follow-Up |
| --- | --- | --- | --- |
| `tdf_boss_eldergrove_mere_root_leviathan` | `PassWithConcern` | Low aquatic axis, dominant forelimbs, long rudder tail, seven cervical vanes, wet materials, and water-contact sequence read well. The head still carries a recognizable salamander base. | Deepen jaw-hinge, vent, and forelimb-root identity in production; retain exactly seven ordered vanes with separate attachment channels. |
| `tdf_elite_eldergrove_hollowbark_stalker` | `ProvisionalPass` | Long flexible body, high shoulders, gripping feet, bark-root bands, and split tail fan form a coherent climbing predator identity. | Verify tail-fan blade count and plate-root deformation across the rig before material work. |
| `tdf_elite_eldergrove_mirrorfin_lurker` | `ProvisionalPass` | Broad amphibious body, scalloped lateral mantle, embedded scale islands, suction feet, and mud-to-water motion remain clear without emissive effects. | Standardize mantle scallop count and flare range in the production turnaround and rig. |
| `tdf_elite_eldergrove_sunmane_thornstag` | `PassWithConcern` | Deep chest, long lower-leg negative space, dorsal mane, and broken backward antler system read at gameplay scale. Antler angle and tine order vary between views and the browser ancestry is familiar. | Produce a locked orthographic antler plan with one left break and fixed rear-facing tine order before sculpt approval. |

## Crownlands

| Profile | Disposition | Pixel Findings | Required Follow-Up |
| --- | --- | --- | --- |
| `tdf_boss_crownlands_meridian_tempest_roc` | `ProvisionalPass` | The retained `v002` refinement preserves colossal scale and grounded avian load while replacing the enlarged-eagle head with a low keratin shield skull. Seven separated outer primaries and two independently readable rudder fans now survive the spread silhouette. | Lock the exact seven-primary order, left-wing damage, shield-skull plane, and double-tail roots in the production turnaround before topology begins. |
| `tdf_elite_crownlands_crownstep_lion` | `PassWithConcern` | Deep chest, low hips, heavy tail, scapular motion, and three rows of grown mane plates read clearly. The underlying lion identity remains dominant. | Increase skull and shoulder originality in sculpt while preserving biological plate roots and avoiding literal armor. |
| `tdf_elite_crownlands_galeclaw_courser` | `PassWithConcern` | The selected retry reads as a flightless bird rather than a theropod, with visible short forewings, long bird legs, and a short steering fan. It remains close to familiar cursorial-bird and terror-bird shapes. | Lock avian pelvis, four-toed ground contact, spear skull, forewing size, and short pygostyle fan; reject any production pass that drifts toward dinosaur anatomy. |
| `tdf_elite_crownlands_reliquary_basilisk` | `ProvisionalPass` | Six walking limbs, high mineral shoulder scutes, narrow shielded skull, stiff brace tail, and masonry-climb motion form a distinct low terrestrial footprint. | Confirm all three limb girdles and tail-brace contact in orthographic topology and normal-speed climbing. |

## Umbral

| Profile | Disposition | Pixel Findings | Required Follow-Up |
| --- | --- | --- | --- |
| `tdf_boss_umbral_ashvein_triarch` | `ProvisionalPass` | The retained `v002` final keeps three neck roots, four walking limbs, one wing pair, low crawl, and volcanic materials while replacing generic horned heads with a hornless impact-shield crusher, a low chisel tracker, and a high hooked tracker. All three roles separate in grayscale silhouette. | Lock the central shield width, left sealed-eye damage, right hook profile, neck-root order, and one-wing-pair anatomy in orthographic production source. |
| `tdf_elite_umbral_cindermaw_salamander` | `ProvisionalPass` | Broad wedge mouth, low amphibian load, flattened tail, dorsal heat fins, localized mouth seam, wet ash, and contact steam read coherently without a fire aura. | Lock fin count and root order, then test reduced-motion heat and steam at normal gameplay scale. |
| `tdf_elite_umbral_veilspine_widow` | `ProvisionalPass` | Eight unequal legs, high narrow stance, compact abdomen, vertical veil spines, sparse physical webbing, and controlled-drop motion produce the strongest unique elite silhouette in the Umbral set. | Preserve exact leg hierarchy and spine order; keep web and sensory color supplemental to silhouette. |
| `tdf_elite_umbral_gravewing_siphon` | `PassWithConcern` | The selected regeneration is a horizontal quadrupedal cave bat with one wing pair, two hind limbs, long thumb claws, hanging rest, grounded crawl, and no humanoid torso or clothing. Some proportional identity shifts remain between folded and spread views. | Build one production anatomy turnaround that locks keel, pelvis, nose leaf, thumb claws, and membrane-tab count before sculpt or rig approval. |

## Disposition

The sixteen final sheets are retained as `ReadyForUserReview` and linked to draft PR #285, which is their direct review surface. Schema, semantic, provenance, Git LFS, and retrieval checks passed before this transition. No source is `UserApproved`; the user remains the final creative approver.
