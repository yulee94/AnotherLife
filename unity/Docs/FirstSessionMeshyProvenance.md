# First-session Meshy provenance

Date: 2026-08-24

This record covers AI-assisted source assets admitted or reviewed for the authored first-session visual packet. It contains no credentials or signed download URLs.

## Crownlands customizable champion bases

Approved design source:

- `Assets/AL/Art/Champions/ConceptSheets/champion_crownlands_vanguard_turnaround_v001.png`

Accepted generated lineage:

| Purpose | Meshy task | Reported cost | Disposition |
|---|---|---:|---|
| Clean male front/side/back source | `01a03158-0812-738d-96f9-245d39e1d0b2` | 9 credits | Accepted as 3D input |
| Clean female front/side/back source | `01a03158-0d31-7ab9-9eff-8d131d2cacd1` | 9 credits | Accepted as 3D input |
| Male Meshy 6 base | `01a03159-77bc-73de-835e-824f43bfbec2` | 30 credits | Accepted after Blender processing |
| Female Meshy 6 base | `01a03159-7f80-750a-aa7d-34aa2e3d77c6` | 30 credits | Accepted after Blender processing |
| Male auto-rig | `01a03177-d4a1-7b01-81d1-059edf7b52b6` | 5 credits | Accepted with weight-cleanup gap |
| Female auto-rig | `01a03177-2c46-78af-812e-d016bc29ea9e` | 5 credits | Accepted with weight-cleanup gap |

Accepted-pipeline total: **88 credits**.

Rejected/excluded champion tasks:

- `01a0314f-4536-78bd-ab09-4e23404bfc45` — first armored male reconstruction; rejected as the customization base because it was monolithic, unrigged, visually weak at the head, omitted equipment, and exceeded the requested topology target.
- `01a03150-42b8-72f9-9536-dec6e45e3bd2` — duplicate/orphan multi-image task; excluded from the production packet.

Blender processing:

- Script: `ArtSource/Champions/author_crownlands_customizable_bases.py`
- Male source: `ArtSource/Champions/crownlands_champion_male_base_working_v001.blend`
- Female source: `ArtSource/Champions/crownlands_champion_female_base_working_v001.blend`
- Blender: 5.2 portable
- Shared capabilities: 24-bone rig, four material regions (`Skin`, `Hair`, `Cloth`, `Metal`), four body-build blendshapes (`Slim`, `Broad`, `Tall`, `Stout`), and sockets for main weapon, offhand, head, and back.
- Root-cause correction: all newly created shape keys initially defaulted to weight 1.0 and produced spike/tower deformation. The authoring script now explicitly zeros each key; regenerated FBXs were re-imported and visually re-audited.

Unity production outputs:

- `Assets/AL/Art/Production/FirstUserOnboarding/Characters/Crownlands_Champion_Male_Base_Meshy6_Rigged_v001.fbx`
- `Assets/AL/Art/Production/FirstUserOnboarding/Characters/Crownlands_Champion_Female_Base_Meshy6_Rigged_v001.fbx`
- Embedded `ChampionWalk` clips and five PBR maps per base.

Verified Unity import contract:

- distinct male and female model assets
- one skinned mesh per base
- four named material regions
- four body-build blendshapes
- four equipment/head/back sockets
- valid male and female walk clips
- normal/data textures use correct import color spaces and a 2048-pixel cap

## Realm-hall and floor tasks created by Kanban run #230

The task ledger shows these later card-owned generations. Their credits are not included in the 88-credit champion budget and their assets remain subject to in-context visual acceptance:

- `01a03167-a2cc-7620-bb41-dedbd602ce12` — realm hall image-to-3D
- `01a03167-a94a-7621-93bd-91458ecfa693` — realm hall image-to-3D
- `01a03167-af05-7622-b090-90e6e0845b96` — realm hall image-to-3D
- `01a03167-b4cf-77e9-90a0-fcf715827fc4` — realm hall image-to-3D
- `01a0317c-8cc0-79e9-ad9e-31e98541122e` — seamless covenant flagstone texture image task

## Realm panoramic skyboxes

Approved additional budget: **36 credits**.

| Realm | Meshy task | Reported cost | Disposition |
|---|---|---:|---|
| Stonehold | `01a031ef-0534-7dca-8645-de1143c61343` | 9 credits | Accepted after 2:1 crop and seam blend |
| Eldergrove | `01a031ef-0660-79aa-88e3-e170fa7ac3e5` | 9 credits | Accepted after 2:1 crop and seam blend |
| Crownlands | `01a031ef-071b-7dcb-bf24-dfea18739115` | 9 credits | Accepted with reduced Unity exposure |
| Umbral | `01a031ef-07e1-79ab-8853-a9a4a0246ddc` | 9 credits | Accepted after 2:1 crop and seam blend |

Unity production outputs:

- `Assets/AL/Art/Production/FirstUserOnboarding/Environment/Stonehold_PanoramicSky_Meshy_v001.png`
- `Assets/AL/Art/Production/FirstUserOnboarding/Environment/Eldergrove_PanoramicSky_Meshy_v001.png`
- `Assets/AL/Art/Production/FirstUserOnboarding/Environment/Crownlands_PanoramicSky_Meshy_v001.png`
- `Assets/AL/Art/Production/FirstUserOnboarding/Environment/Umbral_PanoramicSky_Meshy_v001.png`

Processing and validation:

- source images generated at 16:9 because the Meshy image API has no native 2:1 option
- center-cropped locally to exact 2:1
- left/right edge blended and inspected in duplicated wrap diagnostics
- imported as sRGB repeat-wrapped mipmapped 2D textures, exact NPOT dimensions retained, 2048 cap
- bound through the typed first-session realm visual catalog and Unity `Skybox/Panoramic`

## Account observations

- Balance before the accepted champion generation sequence: 6,287 credits.
- Balance before the approved panoramic skybox sequence: 6,000 credits.
- Four panoramic tasks reported 9 credits each; projected balance from this card alone: 5,964 credits.
- Observed balance after concurrent downstream 2.5D/HUD icon generation also ran: 5,805 credits.
- The additional 159-credit delta belongs to the concurrent `t_d8cc0361` icon-generation lane, not the four panoramic tasks above.
- Do not infer per-task charges from the balance delta where a task did not report its own consumed-credit value.
