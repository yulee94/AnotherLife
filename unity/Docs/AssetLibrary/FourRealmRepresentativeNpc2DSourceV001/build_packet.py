from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

sys.dont_write_bytecode = True

from PIL import Image, ImageDraw, ImageEnhance, ImageFont, ImageOps, ImageStat

ROOT = Path(__file__).resolve().parent
IMAGE_SIZE = (1024, 1024)
SHEET_SIZE = (3840, 2160)
SOURCE_AUTHORITIES = [
    "DESIGN.md",
    "unity/Assets/AL/StreamingAssets/GameData/al_four_realm_production_taxonomy.json",
    "unity/Assets/AL/StreamingAssets/GameData/al_stonehold_realm_character_taxonomy.json",
    "unity/Assets/AL/StreamingAssets/GameData/al_eldergrove_realm_character_taxonomy.json",
    "unity/Assets/AL/StreamingAssets/GameData/al_crownlands_realm_character_taxonomy.json",
    "unity/Assets/AL/StreamingAssets/GameData/al_umbral_realm_character_taxonomy.json",
    "unity/Assets/AL/Art/Designs/FourRealmChampionAnchor.md",
    "unity/Assets/AL/Art/Designs/ModularChampionCustomization.md",
    "unity/Docs/Champion_Character_Sheets_Blender_Handoff.md",
    "unity/Docs/PostMVP_Graphics_And_UI_Quality_Standard.md",
]

NPCS = {
    "stonehold": {
        "rosterId": "rct_stonehold_npc_service_v001",
        "displayName": "Master Gruff — Stonehold Service Worker",
        "shortName": "MASTER GRUFF",
        "role": "service",
        "roleActionKeys": ["role.service"],
        "heightMeters": 1.43,
        "conceptSource": "stonehold_master_gruff_concept_e.png",
        "viewSources": {
            "front": "stonehold_master_gruff_front_grok.png",
            "back": "stonehold_master_gruff_back_grok.png",
            "left": "stonehold_master_gruff_left_grok.png",
            "right": "stonehold_master_gruff_right_grok.png",
        },
        "viewCrops": {},
        "tasks": {
            "concept": "01a069e7-f90b-72db-8dfe-8a56c0dc96c4",
            "frontView": "grok-imagine-edit-t123f8f3f-stonehold-front",
            "backView": "grok-imagine-edit-t123f8f3f-stonehold-back",
            "leftView": "grok-imagine-edit-t123f8f3f-stonehold-left",
            "rightView": "grok-imagine-edit-t123f8f3f-stonehold-right",
        },
        "taskModels": {
            "concept": "gpt-image-2",
            "frontView": "grok-imagine-image-2.0",
            "backView": "grok-imagine-image-2.0",
            "leftView": "grok-imagine-image-2.0",
            "rightView": "grok-imagine-image-2.0",
        },
        "taskProviders": {
            "concept": "Meshy",
            "frontView": "Grok",
            "backView": "Grok",
            "leftView": "Grok",
            "rightView": "Grok",
        },
        "taskTools": {
            "concept": "meshy_text_to_image_or_image_to_image",
            "frontView": "xai_images_edits",
            "backView": "xai_images_edits",
            "leftView": "xai_images_edits",
            "rightView": "xai_images_edits",
        },
        "conceptPrompt": "Revise only hanging belt tools in this exact Master Gruff concept. Remove every modern open-ended wrench, combination spanner, box-end wrench, and extra long bar. Replace hanging belt tools with compact medieval-fantasy closed forge tongs and hinged calipers only. Keep held forge tongs in the right hand and modest smithing hammer in the left. Preserve exact face, beard, body, apron, compact heat guards, palette, pose, camera, gray background, visible fingers and feet. No weapon, text, logo, VFX, crop, or anatomy change.",
        "turnaroundPrompt": "Preserve this Stonehold dwarf's face, body, beard, dark-iron/charcoal/oxblood palette and premium grounded PBR style. Convert to a clean production turnaround on flat neutral gray: consistent full-body front, left, back and right A-pose views, feet and hands fully visible, no perspective drift. Replace modern-looking spanners with medieval-fantasy forge tongs, smithing hammer and calipers; reduce pauldrons to service-weight heat protection. Keep apron and modular layers. No weapon, magic, text, logos, scene, crop, fused gear or anatomy errors.",
        "frontPrompt": "Edit only occupation tools and shoulder protection on this exact FRONT orthographic full-body Stonehold dwarf service-worker A-pose. Lock belt layout: character RIGHT hip closed forge tongs; center modest hammer; character LEFT hip hinged calipers plus leather pouch. No modern wrench. Compact service heat guards. Empty hands.",
        "backPrompt": "Edit only occupation tools and shoulder protection on this exact BACK orthographic full-body Stonehold dwarf service-worker A-pose. Lock belt layout to match front: tongs on character RIGHT (viewer left); hammer center; calipers and pouch on character LEFT (viewer right).",
        "leftPrompt": "Edit only occupation tools on this exact LEFT-side orthographic Stonehold service-worker A-pose facing screen-left. Visible LEFT hip: hinged calipers and leather pouch only. Tongs on far/right hip. Hammer at belt center.",
        "rightPrompt": "Create one exact full-body RIGHT-side orthographic A-pose of this same Stonehold dwarf service worker facing screen-right. Visible RIGHT hip: compact closed forge tongs only. Modest hammer at belt center. Calipers and pouch on far/LEFT hip, not on the visible right hip.",
        "identity": "Compressed square craft silhouette; forge apron and compact heat guards; period smithing tools; skilled service authority, not Champion rank.",
        "modules": ["body/head/hands/feet", "hair + beard", "quilted base + sleeves", "apron + belt", "heat guards + gauntlets", "boots", "tongs/hammer/calipers + pouches"],
        "materials": [("BASALT IRON", "aged metal, medium rough", "#454747"), ("FORGE LEATHER", "worn grain, high rough", "#6d4b35"), ("CHARCOAL WOOL", "matte quilt", "#2f3132"), ("OXBLOOD TRIM", "restrained cloth accent", "#62312f")],
        "profiles": {
            "body": ([], "UV-stable stout adult humanoid base; separate head, hands, feet, hair, and beard; constrained morphs"),
            "equipment": ([], "modular workwear, apron, compact heat guards, boots, gauntlets, belt tools, and body masks"),
            "rig": (["rct_stonehold_rig_modular_humanoid_v001"], "shared canonical humanoid bind pose and Unity Humanoid compatibility"),
            "face": ([], "adult hybrid facial deformation with blink/talk shapes; beard-cleared jaw motion"),
            "lod": ([], "LOD0 inspection, reduced mobile gameplay LODs, protected square silhouette and apron/tool read"),
            "collider": ([], "simple compound humanoid collider separate from beard, apron, and tools"),
            "platform": ([], "mobile-floor packed materials and reduced hair/cloth bones; PC-high preserves inspection detail"),
        },
    },
    "eldergrove": {
        "rosterId": "rct_eldergrove_npc_caretaker_v001",
        "displayName": "Eldergrove Caretaker",
        "shortName": "CARETAKER",
        "role": "civilian_service",
        "roleActionKeys": ["role.sanctuary_care"],
        "heightMeters": 1.82,
        "conceptSource": "eldergrove_caretaker_concept_c.png",
        "viewSources": {
            "front": "eldergrove_caretaker_turnaround_c_1.png",
            "back": "eldergrove_caretaker_turnaround_c_2.png",
            "left": "eldergrove_caretaker_left_g.png",
            "right": "eldergrove_caretaker_right_d.png",
        },
        "viewCrops": {},
        "tasks": {
            "concept": "01a06678-8985-7356-bfd5-2fdaf7f39d74",
            "frontView": "01a06675-83cd-72b6-bb2d-90ce8fc16fc7",
            "backView": "01a06675-83cd-72b6-bb2d-90ce8fc16fc7",
            "leftView": "01a069e8-0faa-74da-94fd-df5e2b89ecf4",
            "rightView": "01a0667c-67a9-738f-b770-eefcd4e1490f",
        },
        "taskModels": {"concept": "gpt-image-2", "frontView": "nano-banana-pro", "backView": "nano-banana-pro", "leftView": "gpt-image-2", "rightView": "gpt-image-2"},
        "conceptPrompt": "Using the same exact Eldergrove caretaker identity, face, ears, braid, proportions, outfit and care modules shown in the references, create one clean full-body front three-quarter A-pose concept on flat neutral warm-gray studio ground. Hands and feet unobstructed. Calm sanctuary caretaker with seed satchel, herb wraps, vials and closed care kit; layered organic textile and bark-leather construction. No blade, weapon, ranger pose, magic, text, logo, scene, crop, fused gear or anatomy error.",
        "turnaroundPrompt": "Preserve this Eldergrove elf's exact face, ears, braid, proportions, green/teal/bark-leather palette and grounded PBR quality. Produce consistent full-body front, back and exact side A-pose views on a flat neutral background. REMOVE every blade: no knife, sickle, sword, shears or visible cutting edge. Sanctuary-care role uses seed satchel, herb wraps, vials and a closed leather care kit only. Keep all hands and feet unobstructed, garments modular and identical between views. No weapon, spell VFX, text, logos, scene, crop, fused gear or anatomy drift.",
        "leftPrompt": "Revise this exact LEFT orthographic Eldergrove caretaker A-pose, still facing screen-left. Move the leaf-stamped seed satchel off the visible left hip onto the far/right hip so only its strap crosses the torso. On the visible left hip keep two glass vials and a cloth herb wrap only. Closed leather care-kit may sit at the back of the belt. Preserve exact face, ears, braid, tunic, vest, boots, proportions, black background, visible hands and feet. No blade, shears, weapon, ranger cue, VFX, text, logo, crop, or anatomy error.",
        "rightPrompt": "Create one exact full-body RIGHT-side orthographic A-pose view of this same Eldergrove caretaker. Match face/profile, ears, braid, body proportions, tunic, vest, boots, seed satchel, vials, herb wraps and care kit placement from references. Arms slightly away, hand and foot silhouettes unobstructed, flat black transparent-like studio background, no ground scene. No blade or weapon, perspective, text, logo, VFX, crop, costume drift or anatomy error.",
        "identity": "Tall calm grown-layer silhouette; organic textiles and bark leather; seed, herb, vial, and care-kit service modules; no ranger weapon.",
        "modules": ["base/head/ears/hands/feet", "braided hair", "tunic + trousers", "leather vest + bracers", "overskirt panels", "boots", "seed satchel/vials/herb wraps/care kit"],
        "materials": [("BARK LEATHER", "dry grain, high rough", "#6a5038"), ("MOSS LINEN", "matte woven", "#687052"), ("DEEP TEAL CLOTH", "soft broad folds", "#265c50"), ("AGED BRONZE", "small fasteners only", "#8a7547")],
        "profiles": {
            "body": (["rct_eldergrove_body_character_base_v001", "rct_eldergrove_body_character_ears_v001", "rct_eldergrove_body_character_feet_v001", "rct_eldergrove_body_character_hair_v001", "rct_eldergrove_body_character_hands_v001", "rct_eldergrove_body_character_head_v001"], "UV-stable tall adult humanoid body family with separate ears, head, hands, feet, and hair"),
            "equipment": (["rct_eldergrove_equipment_armor_chest_v001", "rct_eldergrove_equipment_armor_trim_v001", "rct_eldergrove_equipment_back_attachment_v001", "rct_eldergrove_equipment_belt_v001", "rct_eldergrove_equipment_boots_v001", "rct_eldergrove_equipment_cape_v001", "rct_eldergrove_equipment_gloves_v001", "rct_eldergrove_equipment_hood_v001", "rct_eldergrove_equipment_mount_anchor_v001", "rct_eldergrove_equipment_pet_anchor_v001", "rct_eldergrove_equipment_robe_v001", "rct_eldergrove_equipment_weapon_main_v001", "rct_eldergrove_equipment_weapon_off_v001"], "use only civilian garment and care-tool-compatible catalog modules; weapon slots remain unequipped"),
            "rig": (["rct_eldergrove_rig_humanoid_shared_v001"], "shared canonical humanoid bind pose and Unity Humanoid compatibility"),
            "face": (["rct_eldergrove_face_humanoid_hybrid_v001"], "adult hybrid face with blink/talk shapes and ear/hair compatibility"),
            "lod": (["rct_eldergrove_lod_character_v001"], "protect vertical organic silhouette, braid mass, satchel, and overskirt rhythm"),
            "collider": (["rct_eldergrove_collider_character_v001"], "simple compound humanoid collider separate from hair, cloth panels, and care tools"),
            "platform": (["rct_eldergrove_platform_character_mobile_floor_v001", "rct_eldergrove_platform_character_mobile_high_v001", "rct_eldergrove_platform_character_pc_high_v001"], "catalog platform tiers; mobile reduces secondary hair and cloth bones before identity"),
        },
    },
    "crownlands": {
        "rosterId": "rct_crownlands_npc_service_v001",
        "displayName": "Crownlands Service Worker",
        "shortName": "SERVICE WORKER",
        "role": "service",
        "roleActionKeys": ["role.service"],
        "heightMeters": 1.70,
        "conceptSource": "crownlands_service_worker_concept_c.png",
        "viewSources": {
            "front": "crownlands_service_worker_turnaround_b_2.png",
            "back": "crownlands_service_worker_back_g.png",
            "left": "crownlands_service_worker_left_g.png",
            "right": "crownlands_service_worker_right_g.png",
        },
        "viewCrops": {
            "front": (0, 0, 415, 768),
        },
        "tasks": {
            "concept": "01a06678-92df-773f-a26f-2824bb94aa1d",
            "frontView": "01a06672-ca4b-70bb-a29e-1f6ce1bebfb6",
            "backView": "01a069e8-1a31-71d6-bd5f-59df4172e9e4",
            "leftView": "01a069e8-24bd-75a6-be64-fc05e8ce79d4",
            "rightView": "01a069e8-3020-71bd-98d8-926626123cd3",
        },
        "taskModels": {"concept": "gpt-image-2", "frontView": "nano-banana-pro", "backView": "gpt-image-2", "leftView": "gpt-image-2", "rightView": "gpt-image-2"},
        "conceptPrompt": "Using the same exact Crownlands civic service worker identity, face, bound hair, proportions, uniform and service modules shown in the references, create one clean full-body front three-quarter A-pose concept on flat neutral warm-gray studio ground. Hands and feet unobstructed. Tailored practical deep-blue/ivory civic uniform with ledger satchel, document tube, seal case and key ring; dignified worker. No armor, weapon, magic, text, logo, scene, crop, fused gear or anatomy error.",
        "turnaroundPrompt": "Preserve this Crownlands human's face, build, bound hair and deep-blue/ivory/brass premium grounded PBR style. Convert to a clean production turnaround on flat neutral gray: consistent full-body front, left, back and right A-pose views, hands and feet visible. Strengthen civic-service function with a leather ledger satchel, document tube, seal case and key ring; keep the tailored practical uniform, remove decorative cuff creatures and scene background. Modular layers, no combat armor. No weapon, magic, text, logos, crop, fused gear or anatomy errors.",
        "backPrompt": "Revise only occupation gear in this exact BACK orthographic Crownlands civic worker A-pose. Put the leather ledger satchel on the character's LEFT hip (viewer's right). Run the document tube diagonally across the back from that satchel up toward the right shoulder. Put the small seal pouch and hanging keys on the character's RIGHT hip (viewer's left). Preserve exact hair bun, coat, boots, pose, scale, black background, visible hands and feet. No armor, weapon, royal cue, VFX, text, logo, crop, or anatomy error.",
        "leftPrompt": "Revise occupation gear in this exact LEFT orthographic Crownlands civic worker A-pose, still facing screen-left. Visible left hip: leather ledger satchel only. Keys and seal pouch belong on the far/right hip and must not hang on the left. Document tube stays on the back, not as a front rod. Remove the extra vertical staff/rod in front of the coat. Preserve exact face, bun, coat, boots, pose, black background, hands and feet. No armor, weapon, royal cue, VFX, text, logo, or crop.",
        "rightPrompt": "Revise occupation gear in this exact RIGHT orthographic Crownlands civic worker A-pose, still facing screen-right. Visible right hip: hanging key ring and small seal pouch only. Ledger satchel belongs on the far/left hip; do not place it on the right hip. Document tube rests on the back over the right shoulder. Preserve exact face, bun, coat, boots, pose, black background, hands and feet. No armor, weapon, royal cue, VFX, text, logo, crop, or anatomy error.",
        "identity": "Balanced upright civic silhouette; disciplined tailored geometry and pale underlayer; ledger, document tube, seal case, and keys; worker rather than royal Champion.",
        "modules": ["base/head/hands/feet", "bound hair", "ivory base shirt + trousers", "tailored blue long coat", "belt + boots", "ledger satchel", "document tube/seal case/key ring"],
        "materials": [("DEEP BLUE WOOL", "dense matte weave", "#28465d"), ("IVORY LINEN", "soft high rough", "#d4c9ad"), ("CIVIC LEATHER", "worn structured grain", "#6b4c36"), ("MUTED BRASS", "small geometric closures", "#9b8352")],
        "profiles": {
            "body": ([], "UV-stable balanced adult humanoid base with separate head, hands, feet, and bound hair; constrained morphs"),
            "equipment": ([], "tailored base, long coat, boots, belt, ledger satchel, tube, seal case, keys, and body masks"),
            "rig": (["rct_crownlands_rig_modular_humanoid_v001"], "shared canonical humanoid bind pose and Unity Humanoid compatibility"),
            "face": ([], "adult hybrid facial deformation with blink/talk shapes and natural asymmetry"),
            "lod": ([], "protect balanced coat silhouette, pale center line, document tube, satchel, and key-ring read"),
            "collider": ([], "simple compound humanoid collider separate from coat tails and document gear"),
            "platform": ([], "mobile-floor packed cloth/leather set and simplified coat bones; PC-high preserves tailoring detail"),
        },
    },
    "umbral": {
        "rosterId": "rct_umbral_npc_archivist_v001",
        "displayName": "Umbral Archivist",
        "shortName": "ARCHIVIST",
        "role": "service",
        "roleActionKeys": ["role.archive_service"],
        "heightMeters": 1.86,
        "conceptSource": "umbral_archivist_concept_grok.png",
        "viewSources": {
            "front": "umbral_archivist_front_grok.png",
            "back": "umbral_archivist_back_f.png",
            "left": "umbral_archivist_left_grok.png",
            "right": "umbral_archivist_right_grok.png",
        },
        "viewCrops": {},
        "tasks": {
            "concept": "grok-imagine-edit-t123f8f3f-umbral-concept",
            "frontView": "grok-imagine-edit-t123f8f3f-umbral-front",
            "backView": "01a069aa-f655-71ec-a76b-3cfbc87653a0",
            "leftView": "grok-imagine-edit-t123f8f3f-umbral-left",
            "rightView": "grok-imagine-edit-t123f8f3f-umbral-right",
        },
        "taskModels": {
            "concept": "grok-imagine-image-2.0",
            "frontView": "grok-imagine-image-2.0",
            "backView": "gpt-image-2",
            "leftView": "grok-imagine-image-2.0",
            "rightView": "grok-imagine-image-2.0",
        },
        "taskProviders": {
            "concept": "Grok",
            "frontView": "Grok",
            "backView": "Meshy",
            "leftView": "Grok",
            "rightView": "Grok",
        },
        "taskTools": {
            "concept": "xai_images_edits",
            "frontView": "xai_images_edits",
            "backView": "meshy_text_to_image_or_image_to_image",
            "leftView": "xai_images_edits",
            "rightView": "xai_images_edits",
        },
        "conceptPrompt": "Revise only the facial marking on this exact Umbral civilian archivist. Remove the matching dash at the right temple/outer canthus. Keep exactly ONE very faint, short, asymmetric, non-glowing matte mark on the LEFT temple only. No second mark, no brow/cheek stripe, no glow, curse, gore, makeup, or warlock effect. Preserve exact face, ears, tied hair, lean body, robes, scroll/key/index modules, palette, pose, camera, background, visible hands and feet. One character only. No armor, weapon, VFX, text, logo, crop, or anatomy change.",
        "turnaroundPrompt": "Preserve this Umbral dark elf's exact mature face, ears, lean proportions, ash-violet skin, tied silver-black hair and charcoal/violet grounded PBR quality. Produce consistent full-body front, back and exact side A-pose views on a flat neutral background. Civilian archivist only: remove all pointed shoulder armor and combat cues. Keep only one faint non-glowing temple mark, plus scroll cases, keys and indexing tools. Hands and feet unobstructed; modular robes identical between views. No weapon, spell VFX, text, logos, crop, fused gear or anatomy drift.",
        "frontPrompt": "Revise only the facial marking in this exact FRONT orthographic full-body Umbral civilian archivist A-pose. Remove the dark long fracture across brow and cheek. Keep one very faint, short, asymmetric, non-glowing temple mark matching the concept; it must not read as a curse, warlock effect, gore, or makeup. Preserve exact mature face, ears, tied hair, lean body, robes, scroll/key/index modules, palette, pose, scale, camera, black background, visible hands/feet. No armor, weapon, VFX, text, logo, crop, drift, anatomy error.",
        "backPrompt": "Preserve this exact BACK orthographic full-body Umbral civilian archivist A-pose and all modules. Ensure no facial fracture is visible from the back; no glowing mark, curse effect, armor, assassin or warlock cue. Preserve tied silver-black hair, ears, lean body, layered robes, scroll/key/index modules, palette, materials, pose, scale, camera, black background, visible hands and feet. One character only. No weapon, VFX, text, logo, crop, drift, or anatomy error.",
        "leftPrompt": "Revise only the facial marking in this exact LEFT-side orthographic full-body Umbral civilian archivist A-pose, still facing screen-left. Remove the dark long fracture across brow/cheek. Keep one very faint, short, asymmetric, non-glowing temple mark matching concept; no curse, warlock, gore, makeup read. Preserve exact face/profile, ears, tied hair, lean body, robes, archive modules, palette, pose, scale, camera, black background, visible hands/feet. No armor, weapon, VFX, text, logo, crop, drift, anatomy error.",
        "rightPrompt": "Revise only the facial marking in this exact RIGHT-side orthographic full-body Umbral civilian archivist A-pose, still facing screen-right. Remove the dark long fracture across brow/cheek. Keep one very faint, short, asymmetric, non-glowing temple mark matching concept; no curse, warlock, gore, makeup read. Preserve exact face/profile, ears, tied hair, lean body, robes, archive modules, palette, pose, scale, camera, black background, visible hands/feet. No armor, weapon, VFX, text, logo, crop, drift, anatomy error.",
        "identity": "Lean precise archive silhouette; layered ash cloth and restrained graveglass fittings; readable face/hands; scroll, key, and index modules without assassin or warlock cues.",
        "modules": ["base/head/ears/hands/feet + faint mark", "tied hair", "inner tunic + trousers", "layered ash robe panels", "belt + gloves + boots", "scroll harness", "keys/index pouches/archive cases"],
        "materials": [("ASH CLOTH", "absorbent matte weave", "#4e4c4f"), ("DUSTED VIOLET", "soft layered cloth", "#66556c"), ("BLACKENED LEATHER", "satin worn grain", "#343237"), ("GRAVEGLASS", "sparse rigid accent", "#77717f")],
        "profiles": {
            "body": (["rct_umbral_body_character_base_v001", "rct_umbral_body_character_curse_mark_v001", "rct_umbral_body_character_ears_v001", "rct_umbral_body_character_feet_v001", "rct_umbral_body_character_hair_v001", "rct_umbral_body_character_hands_v001", "rct_umbral_body_character_head_v001"], "UV-stable lean adult humanoid body family; curse-mark module limited to faint non-glowing temple accent"),
            "equipment": (["rct_umbral_equipment_armor_chest_v001", "rct_umbral_equipment_fracture_accent_v001", "rct_umbral_equipment_spike_attachment_v001", "rct_umbral_equipment_belt_v001", "rct_umbral_equipment_boots_v001", "rct_umbral_equipment_assassin_cloak_v001", "rct_umbral_equipment_gloves_v001", "rct_umbral_equipment_mask_v001", "rct_umbral_equipment_mount_anchor_v001", "rct_umbral_equipment_pet_anchor_v001", "rct_umbral_equipment_ash_cloth_layer_v001", "rct_umbral_equipment_weapon_main_v001", "rct_umbral_equipment_weapon_off_v001"], "use only civilian robe/fracture/belt/boot/glove-compatible catalog modules; spike, assassin cloak, mask, and weapon slots remain unequipped"),
            "rig": (["rct_umbral_rig_humanoid_shared_v001"], "shared canonical humanoid bind pose and Unity Humanoid compatibility"),
            "face": (["rct_umbral_face_humanoid_hybrid_v001"], "mature adult hybrid face with blink/talk shapes, ear/hair compatibility, and subtle mark mask"),
            "lod": (["rct_umbral_lod_character_v001"], "protect narrow layered silhouette, readable face/hands, scroll harness, and robe negative space"),
            "collider": (["rct_umbral_collider_character_v001"], "simple compound humanoid collider separate from hair, robe panels, and archive tools"),
            "platform": (["rct_umbral_platform_character_mobile_floor_v001", "rct_umbral_platform_character_mobile_high_v001", "rct_umbral_platform_character_pc_high_v001"], "catalog platform tiers; mobile preserves value separation and archive silhouette without emission"),
        },
    },
}


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = [
        Path("C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf"),
        Path("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
    ]
    for candidate in candidates:
        if candidate.is_file():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def normalize(
    src: Path,
    dst: Path,
    background: tuple[int, int, int],
    crop: tuple[int, int, int, int] | None = None,
) -> None:
    with Image.open(src) as opened:
        image = opened.convert("RGB")
    if crop is not None:
        left, top, right, bottom = crop
        if left < 0 or top < 0 or right > image.width or bottom > image.height:
            raise ValueError(f"crop {crop} exceeds source dimensions {image.size}: {src}")
        image = image.crop(crop)
    contained = ImageOps.contain(image, IMAGE_SIZE, method=Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", IMAGE_SIZE, background)
    canvas.paste(contained, ((IMAGE_SIZE[0] - contained.width) // 2, (IMAGE_SIZE[1] - contained.height) // 2))
    dst.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(dst, format="PNG", optimize=True)


def fit(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    return ImageOps.contain(image, size, method=Image.Resampling.LANCZOS)


def draw_wrapped(draw: ImageDraw.ImageDraw, text: str, box: tuple[int, int, int, int], text_font, fill, spacing: int = 8) -> int:
    left, top, right, _ = box
    words = text.split()
    lines: list[str] = []
    current = ""
    for word in words:
        candidate = f"{current} {word}".strip()
        if draw.textbbox((0, 0), candidate, font=text_font)[2] <= right - left:
            current = candidate
        else:
            if current:
                lines.append(current)
            current = word
    if current:
        lines.append(current)
    y = top
    line_height = draw.textbbox((0, 0), "Ag", font=text_font)[3] + spacing
    for line in lines:
        draw.text((left, y), line, font=text_font, fill=fill)
        y += line_height
    return y


def subject_rgba(path: Path) -> Image.Image:
    with Image.open(path) as opened:
        image = opened.convert("RGB")
    alpha = Image.new("L", image.size)
    pixels = []
    for pixel in image.get_flattened_data():
        luminance = max(pixel[:3] if isinstance(pixel, tuple) else pixel)
        pixels.append(0 if luminance < 8 else min(255, max(0, (luminance - 5) * 18)))
    alpha.putdata(pixels)
    rgba = image.convert("RGBA")
    rgba.putalpha(alpha)
    bbox = alpha.getbbox()
    return rgba.crop(bbox) if bbox else rgba


def normalize_subject_exposure(image: Image.Image, target_luminance: float = 88.0) -> Image.Image:
    alpha = image.getchannel("A")
    mean_luminance = ImageStat.Stat(image.convert("L"), mask=alpha).mean[0]
    factor = max(0.82, min(1.22, target_luminance / max(mean_luminance, 1.0)))
    adjusted = ImageEnhance.Brightness(image.convert("RGB")).enhance(factor).convert("RGBA")
    adjusted.putalpha(alpha)
    return adjusted


def create_handoff(realm: str, npc: dict) -> Path:
    canvas = Image.new("RGB", SHEET_SIZE, "#11151b")
    draw = ImageDraw.Draw(canvas)
    accent = {"stonehold": "#b78453", "eldergrove": "#91a66f", "crownlands": "#b5aa7e", "umbral": "#9a80a4"}[realm]
    draw.rectangle((0, 0, 3840, 112), fill="#0b0e13")
    draw.text((54, 26), f"{realm.upper()}  /  {npc['shortName']}  /  MODEL HANDOFF V001", font=font(46, True), fill="#f0eadf")
    draw.text((54, 86), npc["rosterId"], font=font(22), fill=accent)

    view_paths = {view: ROOT / "Views" / f"{npc['rosterId']}_view_{view}_v001.png" for view in ("front", "back", "left", "right")}
    labels = {"front": "FRONT", "back": "BACK", "left": "LEFT PROFILE", "right": "RIGHT PROFILE"}
    for index, view in enumerate(("front", "back", "left", "right")):
        x = 46 + index * 710
        draw.rounded_rectangle((x, 142, x + 676, 1422), radius=12, fill="#1b2028", outline="#424c59", width=2)
        draw.text((x + 20, 160), labels[view], font=font(27, True), fill="#e7e1d7")
        with Image.open(view_paths[view]) as opened:
            image = fit(opened.convert("RGB"), (636, 1170))
        canvas.paste(image, (x + (676 - image.width) // 2, 218 + (1170 - image.height) // 2))

    concept_path = ROOT / "Concepts" / f"{npc['rosterId']}_concept_threequarter_v001.png"
    with Image.open(concept_path) as opened:
        concept = opened.convert("RGB")
    face = concept.crop((285, 35, 760, 445))
    face = ImageOps.fit(face, (820, 520), method=Image.Resampling.LANCZOS)
    draw.rounded_rectangle((2896, 142, 3794, 716), radius=12, fill="#1b2028", outline="#424c59", width=2)
    canvas.paste(face, (2935, 176))
    draw.text((2918, 668), "FACE / HAIR / AGE / MARKING LOCK", font=font(24, True), fill="#f0eadf")

    draw.rounded_rectangle((2896, 746, 3794, 1168), radius=12, fill="#1b2028", outline="#424c59", width=2)
    draw.text((2920, 770), "MATERIAL / PBR SWATCHES", font=font(27, True), fill="#f0eadf")
    for index, (name, note, color) in enumerate(npc["materials"]):
        y = 828 + index * 80
        draw.rounded_rectangle((2920, y, 2980, y + 52), radius=6, fill=color, outline="#d0c8bc")
        draw.text((3000, y), name, font=font(22, True), fill="#ebe5da")
        draw.text((3000, y + 27), note, font=font(18), fill="#aeb7c2")

    draw.rounded_rectangle((2896, 1198, 3794, 1422), radius=12, fill="#1b2028", outline="#424c59", width=2)
    draw.text((2920, 1220), f"SCALE  {npc['heightMeters']:.2f} m", font=font(28, True), fill=accent)
    draw.line((2946, 1280, 2946, 1382), fill="#e9e2d8", width=4)
    draw.line((2924, 1280, 2968, 1280), fill="#e9e2d8", width=4)
    draw.line((2924, 1382, 2968, 1382), fill="#e9e2d8", width=4)
    draw.text((2990, 1284), "1 Unity unit = 1 meter", font=font(21), fill="#d4cdc2")
    draw.text((2990, 1322), "pivot: ground center  /  +Z forward", font=font(21), fill="#d4cdc2")
    draw.text((2990, 1360), "neutral shared humanoid bind envelope", font=font(21), fill="#d4cdc2")

    draw.rounded_rectangle((46, 1458, 1870, 2078), radius=12, fill="#171c23", outline="#424c59", width=2)
    draw.text((76, 1486), "IDENTITY + MODULAR CALLOUTS", font=font(30, True), fill=accent)
    y = draw_wrapped(draw, npc["identity"], (76, 1534, 1828, 1700), font(24), "#e2ddd3") + 12
    for module in npc["modules"]:
        draw.text((82, y), "•", font=font(23, True), fill=accent)
        y = draw_wrapped(draw, module, (112, y, 1828, y + 80), font(22), "#cbd1d8") + 5

    draw.rounded_rectangle((1904, 1458, 3794, 2078), radius=12, fill="#171c23", outline="#424c59", width=2)
    draw.text((1934, 1486), "RIG / LOD / COLLIDER / PLATFORM", font=font(30, True), fill=accent)
    notes = [
        "Shared canonical humanoid bind pose; Unity Humanoid compatible; no second skeleton.",
        "Face: blink/talk-ready adult planes; preserve age, ears, and natural asymmetry.",
        "LOD: protect realm silhouette, face/hands, garment rhythm, and occupation gear before tertiary detail.",
        "Collider: simple compound profile, separate from cloth, hair, beard, keys, tools, pouches, and scrolls.",
        "Mobile-floor reduces texture, hair/cloth bones, and micro-detail; PC-high adds inspection fidelity only.",
        "Clean body geometry contains no particles, trails, aura, smoke, spell effects, or baked runtime VFX.",
    ]
    y = 1542
    for note in notes:
        draw.text((1940, y), "•", font=font(23, True), fill=accent)
        y = draw_wrapped(draw, note, (1970, y, 3750, y + 100), font(22), "#cbd1d8") + 9

    draw.text((54, 2110), "2D SOURCE ONLY  •  generated cross-view detail requires artist reconciliation before mesh/rig approval", font=font(23, True), fill="#e0b56a")
    output = ROOT / "HandoffSheets" / f"{npc['rosterId']}_model_handoff_v001.png"
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, format="PNG", optimize=True)
    return output


def create_lineup() -> Path:
    canvas = Image.new("RGB", SHEET_SIZE, "#141820")
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0, 0, 3840, 120), fill="#0b0e13")
    draw.text((60, 28), "FOUR-REALM REPRESENTATIVE NPC LINEUP  /  COMMON SCALE + NORMALIZED EXPOSURE  /  V001", font=font(42, True), fill="#f0eadf")
    baseline = 1840
    pixels_per_meter = 710
    order = ["stonehold", "eldergrove", "crownlands", "umbral"]
    accents = ["#b78453", "#91a66f", "#b5aa7e", "#9a80a4"]
    for index, realm in enumerate(order):
        npc = NPCS[realm]
        column_left = 120 + index * 900
        column_center = column_left + 380
        front = ROOT / "Views" / f"{npc['rosterId']}_view_front_v001.png"
        subject = normalize_subject_exposure(subject_rgba(front))
        target_height = int(npc["heightMeters"] * pixels_per_meter)
        target_width = max(1, int(subject.width * target_height / subject.height))
        subject = subject.resize((target_width, target_height), Image.Resampling.LANCZOS)
        canvas.paste(subject, (column_center - target_width // 2, baseline - target_height), subject)
        draw.line((column_left, baseline, column_left + 760, baseline), fill="#697380", width=3)
        draw.text((column_left, 160), realm.upper(), font=font(34, True), fill=accents[index])
        draw.text((column_left, 204), npc["shortName"], font=font(27, True), fill="#eee8dd")
        draw.text((column_left, 242), f"{npc['heightMeters']:.2f} m  •  {npc['roleActionKeys'][0]}", font=font(20), fill="#abb4bf")
        draw.text((column_left, 1880), npc["rosterId"], font=font(18), fill="#bfc6ce")
        draw_wrapped(draw, npc["identity"], (column_left, 1914, column_left + 780, 2090), font(20), "#d5d0c7", 5)

    ruler_x = 3740
    top = baseline - int(2.0 * pixels_per_meter)
    draw.line((ruler_x, top, ruler_x, baseline), fill="#efe7da", width=5)
    for value in (0.0, 0.5, 1.0, 1.5, 2.0):
        y = baseline - int(value * pixels_per_meter)
        draw.line((ruler_x - 30, y, ruler_x + 20, y), fill="#efe7da", width=4)
        draw.text((ruler_x - 92, y - 15), f"{value:.1f}", font=font(18), fill="#efe7da")
    draw.text((3660, top - 42), "METERS", font=font(18, True), fill="#efe7da")
    draw.text((60, 2120), "Front views use a common neutral background, normalized exposure, baseline, front presentation, and measured height scale. No runtime VFX.", font=font(22), fill="#aeb7c2")
    output = ROOT / "Review" / "four_realm_representative_npc_lineup_v001.png"
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, format="PNG", optimize=True)
    return output


def provenance(task_id: str, model: str, prompt: str, consumed_credits: int, provider: str = "Meshy", tool: str = "meshy_text_to_image_or_image_to_image") -> dict:
    return {
        "provider": provider,
        "tool": tool,
        "model": model,
        "taskStatus": "SUCCEEDED",
        "consumedCredits": consumed_credits,
        "seed": None,
        "seedStatus": "provider_did_not_expose_seed",
        "taskId": task_id,
        "prompt": prompt,
    }


def artifact_record(path: Path, role: str, roster_id: str | None, provenance_text: str, **extra) -> dict:
    rel = path.relative_to(ROOT).as_posix()
    record = {
        "path": rel,
        "role": role,
        "rosterId": roster_id,
        "dimensions": None,
        "sha256": sha256(path),
        "provenance": provenance_text,
    }
    if path.suffix.lower() == ".png":
        with Image.open(path) as image:
            record["dimensions"] = [image.width, image.height]
    record.update(extra)
    return record


def build_manifest(review_status: str, review_id: str, review_summary: str) -> dict:
    npc_entries = []
    for realm, npc in NPCS.items():
        profile_bindings = {
            key: {"catalogIds": catalog_ids, "intent": intent}
            for key, (catalog_ids, intent) in npc["profiles"].items()
        }
        npc_entries.append({
            "rosterId": npc["rosterId"],
            "displayName": npc["displayName"],
            "realm": realm,
            "role": npc["role"],
            "roleActionKeys": npc["roleActionKeys"],
            "heightMeters": npc["heightMeters"],
            "sourceAuthority": SOURCE_AUTHORITIES,
            "decisionAuthority": "delegated-owner-approved-for-2D-concept" if review_status == "APPROVE" else "delegated-owner-review-pending",
            "readinessState": "approved_2d_source_only" if review_status == "APPROVE" else "review_pending_2d_only",
            "downstream3DReady": False,
            "runtimeAuthority": False,
            "intendedProfiles": profile_bindings,
            "generationProvenance": {
                key: provenance(
                    npc["tasks"][key],
                    npc["taskModels"][key],
                    {
                        "concept": npc["conceptPrompt"],
                        "frontView": npc.get("frontPrompt", npc["turnaroundPrompt"]),
                        "backView": npc.get("backPrompt", npc["turnaroundPrompt"]),
                        "leftView": npc["leftPrompt"],
                        "rightView": npc["rightPrompt"],
                    }[key],
                    0 if npc.get("taskProviders", {}).get(key, "Meshy") == "Grok" else (12 if npc["taskModels"][key] == "gpt-image-2" else 9),
                    npc.get("taskProviders", {}).get(key, "Meshy"),
                    npc.get("taskTools", {}).get(key, "meshy_text_to_image_or_image_to_image"),
                )
                for key in ("concept", "frontView", "backView", "leftView", "rightView")
            },
            "selectionReview": {
                "workerVisualInspection": "APPROVE",
                "resolvedRevisions": "candidate-A role/background issues corrected; opposite profiles added; selected concepts and all final views inspected at full resolution",
            },
        })

    artifacts: list[dict] = []
    for realm, npc in NPCS.items():
        roster_id = npc["rosterId"]
        concept = ROOT / "Concepts" / f"{roster_id}_concept_threequarter_v001.png"
        artifacts.append(artifact_record(concept, "concept", roster_id, f"{npc.get('taskProviders', {}).get('concept', 'Meshy')} task {npc['tasks']['concept']}; deterministic 1024-square normalization by build_packet.py", presentation="neutral full-body front three-quarter A-pose"))
        for view in ("front", "back", "left", "right"):
            path = ROOT / "Views" / f"{roster_id}_view_{view}_v001.png"
            task_key = f"{view}View"
            crop_note = f"; curated source crop {npc['viewCrops'][view]} removes adjacent non-subject content" if view in npc["viewCrops"] else ""
            provider = npc.get("taskProviders", {}).get(task_key, "Meshy")
            artifacts.append(artifact_record(path, "turnaround_view", roster_id, f"{provider} task {npc['tasks'][task_key]}{crop_note}; deterministic 1024-square normalization by build_packet.py", view=view))
        sheet = ROOT / "HandoffSheets" / f"{roster_id}_model_handoff_v001.png"
        artifacts.append(artifact_record(sheet, "handoff_sheet", roster_id, "deterministic Pillow composition by build_packet.py from manifest-bound selected images", callouts=["face", "modules", "materials", "scale", "rig", "lod", "collider"]))
    lineup = ROOT / "Review" / "four_realm_representative_npc_lineup_v001.png"
    artifacts.append(artifact_record(lineup, "lineup", None, "deterministic Pillow composition by build_packet.py from four manifest-bound front views", commonScale=True, commonCamera=True, commonLighting=True))
    for name, role in (
        ("README.md", "readme"),
        ("build_packet.py", "builder"),
        ("validate_packet.py", "validator"),
        ("test_validate_packet.py", "validator_self_test"),
    ):
        artifacts.append(artifact_record(ROOT / name, role, None, "task-authored packet documentation/tooling"))

    return {
        "packetId": "four_realm_representative_npc_2d_source_v001",
        "schemaVersion": 1,
        "contentVersion": "1.0.0",
        "scope": "exactly four representative civilian/service NPC identities; 2D source and model handoff only",
        "sourceAuthority": SOURCE_AUTHORITIES,
        "approval": {
            "authority": "owner delegated recommended bounded 2D decisions in kanban t_123f8f3f",
            "decision": review_status,
            "independentReviewId": review_id,
            "independentReviewVerdict": "PASS" if review_status == "APPROVE" else "PENDING",
            "summary": review_summary,
            "runtimeAuthority": False,
            "releaseAuthority": False,
        },
        "readinessBoundary": {
            "state": "approved_2d_source_only" if review_status == "APPROVE" else "review_pending_2d_only",
            "permits": ["2d identity reference", "downstream source-alignment review"],
            "forbids": ["automatic image-to-3d", "runtime activation", "terrain or world-map change", "release approval"],
        },
        "reconCoverage": {
            "method": "bounded in-process relevant-range reading; no broad subagent recon",
            "trackedCorpusFiles": 10,
            "filesOpened": 10,
            "linesRead": 2981,
            "corpusLines": 213017,
            "coveragePercent": 1.3994,
            "fullyRead": [
                {"path": "DESIGN.md", "linesRead": 983, "totalLines": 983},
                {"path": "unity/Assets/AL/Art/Designs/FourRealmChampionAnchor.md", "linesRead": 254, "totalLines": 254},
                {"path": "unity/Assets/AL/Art/Designs/ModularChampionCustomization.md", "linesRead": 114, "totalLines": 114},
                {"path": "unity/Docs/Champion_Character_Sheets_Blender_Handoff.md", "linesRead": 55, "totalLines": 55},
                {"path": "unity/Docs/PostMVP_Graphics_And_UI_Quality_Standard.md", "linesRead": 321, "totalLines": 321},
            ],
            "partiallyRead": [
                {"path": "unity/Assets/AL/StreamingAssets/GameData/al_four_realm_production_taxonomy.json", "linesRead": 504, "totalLines": 82561, "ranges": ["2600-2720", "2930-3060", "3310-3430", "3660-3790"]},
                {"path": "unity/Assets/AL/StreamingAssets/GameData/al_stonehold_realm_character_taxonomy.json", "linesRead": 150, "totalLines": 26741, "ranges": ["1867-2016"]},
                {"path": "unity/Assets/AL/StreamingAssets/GameData/al_eldergrove_realm_character_taxonomy.json", "linesRead": 225, "totalLines": 37476, "ranges": ["5680-5904"]},
                {"path": "unity/Assets/AL/StreamingAssets/GameData/al_crownlands_realm_character_taxonomy.json", "linesRead": 150, "totalLines": 26846, "ranges": ["1898-2047"]},
                {"path": "unity/Assets/AL/StreamingAssets/GameData/al_umbral_realm_character_taxonomy.json", "linesRead": 225, "totalLines": 37666, "ranges": ["5880-6104"]},
            ],
            "unread": "non-relevant portions of the five large catalog files; all task-listed documentation files were read completely",
        },
        "npcs": npc_entries,
        "artifacts": artifacts,
        "manifestSelfHashPolicy": "manifest and generated validation report are excluded from manifest artifact hashes to avoid circular self-reference; report records the final manifest SHA-256",
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path)
    parser.add_argument("--review-status", choices=("PENDING", "APPROVE"), default="PENDING")
    parser.add_argument("--review-id", default="pending")
    parser.add_argument("--review-summary", default="Independent visual/source review pending.")
    args = parser.parse_args()

    if args.source_root:
        for realm, npc in NPCS.items():
            roster_id = npc["rosterId"]
            normalize(args.source_root / npc["conceptSource"], ROOT / "Concepts" / f"{roster_id}_concept_threequarter_v001.png", (105, 101, 96))
            for view, source_name in npc["viewSources"].items():
                normalize(
                    args.source_root / source_name,
                    ROOT / "Views" / f"{roster_id}_view_{view}_v001.png",
                    (0, 0, 0),
                    npc["viewCrops"].get(view),
                )
    for npc in NPCS.values():
        concept = ROOT / "Concepts" / f"{npc['rosterId']}_concept_threequarter_v001.png"
        if not concept.is_file():
            raise FileNotFoundError(f"missing staged concept: {concept}")
        for view in ("front", "back", "left", "right"):
            path = ROOT / "Views" / f"{npc['rosterId']}_view_{view}_v001.png"
            if not path.is_file():
                raise FileNotFoundError(f"missing staged view: {path}")
    for realm, npc in NPCS.items():
        create_handoff(realm, npc)
    create_lineup()
    manifest = build_manifest(args.review_status, args.review_id, args.review_summary)
    (ROOT / "npc_2d_source_manifest_v001.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"built {len(manifest['artifacts'])} manifest-bound artifacts")


if __name__ == "__main__":
    main()
