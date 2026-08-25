"""Build the admitted first-user champion and neutral hall source assets.

Run with Blender 5.2 from the repository root:
  blender --background --python unity/ArtSource/FirstUserOnboarding/build_onboarding_asset_packet.py
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy


SCRIPT_PATH = Path(__file__).resolve()
UNITY_ROOT = SCRIPT_PATH.parents[2]
SOURCE_ROOT = UNITY_ROOT / "ArtSource"
ASSET_ROOT = UNITY_ROOT / "Assets" / "AL" / "Art" / "Production" / "FirstUserOnboarding"
CHAMPION_BLEND = SOURCE_ROOT / "Champions" / "champion_vanguard_working_v001.blend"
CHARACTER_ROOT = ASSET_ROOT / "Characters"
ENVIRONMENT_ROOT = ASSET_ROOT / "Environment"
ENVIRONMENT_BLEND = SOURCE_ROOT / "FirstUserOnboarding" / "neutral_covenant_hall_working_v001.blend"
ENEMY_FBX = ASSET_ROOT / "Enemies" / "Covenant_Sentinel_Meshy6_v001.fbx"
ENEMY_TEXTURE_ROOT = ASSET_ROOT / "Enemies" / "Covenant_Sentinel_Meshy6_v001_textures"


def ensure_directories() -> None:
    CHARACTER_ROOT.mkdir(parents=True, exist_ok=True)
    ENVIRONMENT_ROOT.mkdir(parents=True, exist_ok=True)


def select_exact(names: set[str]) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for name in sorted(names):
        obj = bpy.data.objects.get(name)
        if obj is None:
            raise RuntimeError(f"Missing authored champion object: {name}")
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.select_set(True)
    selected = [obj for obj in bpy.context.selected_objects]
    if not selected:
        raise RuntimeError("No objects selected for FBX export")
    bpy.context.view_layer.objects.active = selected[0]


def export_fbx(path: Path) -> None:
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE", "MESH", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=False,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        path_mode="AUTO",
    )


def export_champion_packet() -> None:
    bpy.ops.wm.open_mainfile(filepath=str(CHAMPION_BLEND))
    rig = {"root", "Champion_Vanguard_Rig"}
    body = {
        "SM_Arm_L", "SM_Arm_R", "SM_Eye_L", "SM_Eye_R", "SM_Face", "SM_Hair",
        "SM_Head", "SM_Leg_L", "SM_Leg_R", "SM_Torso",
    }
    armor = {"Cape", "Realm_Ornament", "Shield_Off", "Shoulder_L", "Shoulder_R"}
    weapon = {"Weapon_Main"}

    select_exact(rig | body)
    export_fbx(CHARACTER_ROOT / "Champion_Vanguard_Body_v001.fbx")
    select_exact(rig | armor)
    export_fbx(CHARACTER_ROOT / "Champion_Vanguard_BasicArmor_v001.fbx")
    select_exact(rig | weapon)
    export_fbx(CHARACTER_ROOT / "Champion_Vanguard_BasicWeapon_v001.fbx")


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def material(name: str, base_color: tuple[float, float, float, float], metallic: float, roughness: float):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = base_color
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return mat


def apply_bevel(obj: bpy.types.Object, width: float = 0.06, segments: int = 2) -> None:
    bevel = obj.modifiers.new("Authored edge wear", "BEVEL")
    bevel.width = width
    bevel.segments = segments
    bevel.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False


def box(name: str, location, scale, mat, bevel: float = 0.05) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    apply_bevel(obj, bevel)
    obj.data.materials.append(mat)
    return obj


def cylinder(name: str, location, radius: float, depth: float, mat, vertices: int = 12) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location)
    obj = bpy.context.object
    obj.name = name
    apply_bevel(obj, min(radius * 0.12, 0.05), 2)
    obj.data.materials.append(mat)
    return obj


def join(name: str, objects: list[bpy.types.Object]) -> bpy.types.Object:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    result = bpy.context.object
    result.name = name
    return result


def build_environment_packet() -> None:
    clear_scene()
    floor_mat = material("M_CovenantHall_Floor", (0.12, 0.14, 0.16, 1.0), 0.08, 0.72)
    wall_mat = material("M_CovenantHall_Wall", (0.23, 0.25, 0.27, 1.0), 0.02, 0.78)
    trim_mat = material("M_CovenantHall_Trim", (0.055, 0.07, 0.08, 1.0), 0.72, 0.32)

    authored = []
    authored.append(box("FloorModule", (0.0, -0.15, 0.0), (8.0, 0.30, 12.0), floor_mat, 0.04))

    wall_parts = [
        box("WallCore", (0.0, 1.65, 5.82), (8.0, 3.3, 0.36), wall_mat, 0.05),
        box("WallPanelL", (-2.35, 1.7, 5.60), (2.6, 2.55, 0.16), wall_mat, 0.04),
        box("WallPanelR", (2.35, 1.7, 5.60), (2.6, 2.55, 0.16), wall_mat, 0.04),
    ]
    authored.append(join("WallModule", wall_parts))

    authored.append(join("InnerCornerModule", [
        box("InnerCornerA", (-3.82, 1.45, 4.65), (0.34, 2.9, 2.25), wall_mat, 0.05),
        box("InnerCornerB", (-3.15, 1.45, 5.64), (1.35, 2.9, 0.30), wall_mat, 0.05),
    ]))
    authored.append(join("OuterCornerModule", [
        box("OuterCornerA", (3.82, 1.45, 4.65), (0.34, 2.9, 2.25), wall_mat, 0.05),
        box("OuterCornerB", (3.15, 1.45, 5.64), (1.35, 2.9, 0.30), wall_mat, 0.05),
    ]))

    doorway_parts = [
        box("DoorPillarL", (-3.82, 1.25, -1.3), (0.42, 2.5, 0.52), wall_mat, 0.06),
        box("DoorPillarR", (-3.82, 1.25, 1.3), (0.42, 2.5, 0.52), wall_mat, 0.06),
        box("DoorLintel", (-3.82, 2.75, 0.0), (0.46, 0.5, 3.1), wall_mat, 0.06),
        box("DoorKeystone", (-3.50, 2.82, 0.0), (0.18, 0.72, 0.52), trim_mat, 0.04),
    ]
    authored.append(join("DoorwayModule", doorway_parts))

    authored.append(join("CeilingBeamModule", [
        box("BeamMain", (0.0, 3.18, 1.2), (7.5, 0.28, 0.34), trim_mat, 0.05),
        box("BeamBraceL", (-3.25, 2.82, 1.2), (0.24, 0.72, 0.40), trim_mat, 0.04),
        box("BeamBraceR", (3.25, 2.82, 1.2), (0.24, 0.72, 0.40), trim_mat, 0.04),
    ]))
    authored.append(join("TrimModule", [
        box("TrimRail", (0.0, 0.55, 5.47), (6.6, 0.20, 0.14), trim_mat, 0.03),
        box("TrimCrest", (0.0, 2.60, 5.44), (1.35, 0.38, 0.18), trim_mat, 0.03),
    ]))

    brazier_parts = [
        cylinder("BrazierStem", (2.8, 0.55, 3.6), 0.15, 1.1, trim_mat),
        cylinder("BrazierBowl", (2.8, 1.12, 3.6), 0.48, 0.22, trim_mat, 16),
        box("BrazierBase", (2.8, 0.10, 3.6), (0.56, 0.20, 0.56), wall_mat, 0.05),
    ]
    authored.append(join("BrazierProp", brazier_parts))

    banner_parts = [
        cylinder("BannerPole", (-2.85, 1.25, 3.7), 0.08, 2.5, trim_mat),
        box("BannerCrossbar", (-2.85, 2.32, 3.7), (1.25, 0.10, 0.10), trim_mat, 0.025),
        box("BannerCloth", (-2.85, 1.60, 3.72), (1.55, 1.25, 0.06), floor_mat, 0.02),
        box("BannerBase", (-2.85, 0.12, 3.7), (0.54, 0.24, 0.54), wall_mat, 0.04),
    ]
    authored.append(join("BannerStandProp", banner_parts))

    crate_parts = [
        box("Crate", (2.65, 0.42, -4.3), (0.82, 0.84, 0.82), wall_mat, 0.045),
        box("CrateBandA", (2.65, 0.42, -4.72), (0.94, 0.16, 0.08), trim_mat, 0.02),
        box("CrateBandB", (2.65, 0.42, -3.88), (0.94, 0.16, 0.08), trim_mat, 0.02),
        cylinder("Barrel", (3.35, 0.55, -3.45), 0.46, 1.10, floor_mat, 12),
    ]
    authored.append(join("CrateBarrelProp", crate_parts))

    for obj in authored:
        obj["al_asset_status"] = "MVP_PRODUCTION_CANDIDATE"
        obj["al_source"] = "Blender 5.2 procedural authored neutral modular kit"

    bpy.ops.wm.save_as_mainfile(filepath=str(ENVIRONMENT_BLEND))
    select_exact({obj.name for obj in authored})
    export_fbx(ENVIRONMENT_ROOT / "Neutral_Covenant_Hall_Kit_v001.fbx")


def clean_enemy_candidate() -> None:
    """Strip Meshy's staging camera/cube/light and cap source textures at 1024."""
    if not ENEMY_FBX.exists():
        raise RuntimeError(f"Missing Meshy enemy download: {ENEMY_FBX}")

    for texture_path in sorted(ENEMY_TEXTURE_ROOT.glob("*.png")):
        image = bpy.data.images.load(str(texture_path), check_existing=False)
        longest = max(image.size)
        if longest > 1024:
            scale = 1024.0 / float(longest)
            image.scale(
                max(1, round(image.size[0] * scale)),
                max(1, round(image.size[1] * scale)),
            )
            image.filepath_raw = str(texture_path)
            image.file_format = "PNG"
            image.save()
        bpy.data.images.remove(image)

    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(ENEMY_FBX))
    mesh_objects = [obj for obj in bpy.data.objects if obj.type == "MESH" and obj.name == "Mesh_0"]
    if len(mesh_objects) != 1:
        raise RuntimeError(f"Expected one Meshy production mesh, found {len(mesh_objects)}")
    sentinel = mesh_objects[0]
    sentinel.name = "CovenantSentinel"
    bpy.ops.object.select_all(action="DESELECT")
    sentinel.select_set(True)
    bpy.context.view_layer.objects.active = sentinel
    cleaned_path = ENEMY_FBX.with_name("Covenant_Sentinel_Meshy6_v001.cleaned.fbx")
    bpy.ops.export_scene.fbx(
        filepath=str(cleaned_path),
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_anim=False,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        path_mode="RELATIVE",
        embed_textures=False,
    )
    cleaned_path.replace(ENEMY_FBX)


if __name__ == "__main__":
    ensure_directories()
    export_champion_packet()
    build_environment_packet()
    clean_enemy_candidate()
    print("AL_ONBOARDING_ASSET_PACKET_BUILT")
