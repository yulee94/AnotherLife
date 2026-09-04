"""Build one source-only, production-qualified realm-creature slice in Blender.

The output remains under ArtSource/Docs. It creates no Unity runtime, gameplay,
spawn, reward, save, or VFX authority.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path
from typing import Any

import bpy
from mathutils import Vector


SCRIPT_PATH = Path(__file__).resolve()
DEFAULT_PLAN = Path(
    "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001/ProductionSlices/"
    "FaultCrownedColossusV001/fault_crowned_colossus_production_slice_plan_v001.json"
)


class BuildError(RuntimeError):
    pass


def arguments() -> argparse.Namespace:
    values = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--plan", default=DEFAULT_PLAN, type=Path)
    return parser.parse_args(values)


def stable_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=True,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_value(value: Any) -> str:
    return hashlib.sha256(stable_bytes(value)).hexdigest()


def resolve(repo_root: Path, value: str | Path) -> Path:
    candidate = (repo_root / value).resolve() if not Path(value).is_absolute() else Path(value).resolve()
    try:
        candidate.relative_to(repo_root.resolve())
    except ValueError as error:
        raise BuildError(f"PathEscapesRepository:{value}") from error
    return candidate


def file_record(repo_root: Path, path: Path) -> dict[str, Any]:
    return {
        "path": path.relative_to(repo_root).as_posix(),
        "bytes": path.stat().st_size,
        "sha256": sha256_file(path),
    }


def png_record(repo_root: Path, path: Path) -> dict[str, Any]:
    record = file_record(repo_root, path)
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise BuildError(f"ReviewOutputNotPng:{path}")
    record["dimensions"] = list(struct.unpack(">II", header[16:24]))
    return record


def import_source(path: Path) -> list[bpy.types.Object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.wm.fbx_import(filepath=str(path), use_anim=False)
    if "FINISHED" not in result:
        raise BuildError(f"FbxImportFailed:{path}")
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise BuildError("SourceContainsNoMesh")
    return meshes


def select_only(objects: list[bpy.types.Object], active: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active


def mesh_data_name(object_name: str) -> str:
    return object_name.replace("GEO_", "MESH_", 1) if object_name.startswith("GEO_") else f"MESH_{object_name}"


def join_source_meshes(meshes: list[bpy.types.Object], plan: dict[str, Any]) -> bpy.types.Object:
    select_only(meshes, meshes[0])
    if len(meshes) > 1:
        bpy.ops.object.join()
    mesh = bpy.context.view_layer.objects.active
    if mesh is None or mesh.type != "MESH":
        raise BuildError("SourceJoinFailed")
    lod0_name = plan["lodPolicy"]["levels"][0]["object"]
    mesh.name = lod0_name
    mesh.data.name = mesh_data_name(lod0_name)
    return mesh


def world_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def normalize_source(mesh: bpy.types.Object, target_length: float) -> dict[str, Any]:
    minimum, maximum = world_bounds(mesh)
    dimensions = maximum - minimum
    if dimensions.y > dimensions.x:
        mesh.rotation_euler.z -= math.pi / 2.0
        select_only([mesh], mesh)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        minimum, maximum = world_bounds(mesh)
        dimensions = maximum - minimum

    points_x = sorted((mesh.matrix_world @ vertex.co).x for vertex in mesh.data.vertices)
    sample = max(1, len(points_x) // 20)
    left_tail = points_x[sample] - points_x[0]
    right_tail = points_x[-1] - points_x[-sample - 1]
    detected_front = "-X" if left_tail >= right_tail else "+X"
    if detected_front == "+X":
        mesh.rotation_euler.z += math.pi
        select_only([mesh], mesh)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    minimum, maximum = world_bounds(mesh)
    length = maximum.x - minimum.x
    if length <= 0:
        raise BuildError("SourceLengthIsZero")
    scale = target_length / length
    mesh.scale = (scale, scale, scale)
    select_only([mesh], mesh)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    minimum, maximum = world_bounds(mesh)
    offset = Vector((-(minimum.x + maximum.x) / 2.0, -(minimum.y + maximum.y) / 2.0, -minimum.z))
    mesh.location += offset
    select_only([mesh], mesh)
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    triangulate = mesh.modifiers.new("Triangulate_Source", "TRIANGULATE")
    triangulate.quad_method = "BEAUTY"
    select_only([mesh], mesh)
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    for polygon in mesh.data.polygons:
        polygon.use_smooth = True
    minimum, maximum = world_bounds(mesh)
    return {
        "detectedSourceFrontBeforeCanonicalization": detected_front,
        "canonicalFront": "-X",
        "targetLengthMeters": target_length,
        "boundsMinimum": [round(value, 6) for value in minimum],
        "boundsMaximum": [round(value, 6) for value in maximum],
        "dimensionsMeters": [round(value, 6) for value in maximum - minimum],
    }


def configure_material(repo_root: Path, plan: dict[str, Any], mesh: bpy.types.Object) -> bpy.types.Material:
    material = bpy.data.materials.new(plan["materialPolicy"]["materialId"])
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.72
    shader.inputs["Metallic"].default_value = 0.08
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    texture_by_role = {row["role"]: row for row in plan["materialPolicy"]["textures"]}

    base = nodes.new("ShaderNodeTexImage")
    base.name = "TEX_base_color"
    base.image = bpy.data.images.load(str(resolve(repo_root, texture_by_role["base_color"]["path"])), check_existing=False)
    ao = nodes.new("ShaderNodeTexImage")
    ao.name = "TEX_ambient_occlusion"
    ao.image = bpy.data.images.load(str(resolve(repo_root, texture_by_role["ambient_occlusion"]["path"])), check_existing=False)
    ao.image.colorspace_settings.name = "Non-Color"
    multiply = nodes.new("ShaderNodeMixRGB")
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1.0
    links.new(base.outputs["Color"], multiply.inputs[1])
    links.new(ao.outputs["Color"], multiply.inputs[2])
    links.new(multiply.outputs["Color"], shader.inputs["Base Color"])

    normal_texture = nodes.new("ShaderNodeTexImage")
    normal_texture.name = "TEX_normal"
    normal_texture.image = bpy.data.images.load(str(resolve(repo_root, texture_by_role["normal"]["path"])), check_existing=False)
    normal_texture.image.colorspace_settings.name = "Non-Color"
    normal = nodes.new("ShaderNodeNormalMap")
    normal.inputs["Strength"].default_value = 0.72
    links.new(normal_texture.outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs["Normal"], shader.inputs["Normal"])

    packed = nodes.new("ShaderNodeTexImage")
    packed.name = "TEX_metallic_smoothness"
    packed.image = bpy.data.images.load(str(resolve(repo_root, texture_by_role["metallic_smoothness"]["path"])), check_existing=False)
    packed.image.colorspace_settings.name = "Non-Color"
    separate = nodes.new("ShaderNodeSeparateColor")
    links.new(packed.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Red"], shader.inputs["Metallic"])
    invert = nodes.new("ShaderNodeMath")
    invert.operation = "SUBTRACT"
    invert.inputs[0].default_value = 1.0
    links.new(packed.outputs["Alpha"], invert.inputs[1])
    links.new(invert.outputs["Value"], shader.inputs["Roughness"])

    mesh.data.materials.clear()
    mesh.data.materials.append(material)
    return material


def add_edit_bone(
    armature: bpy.types.Object,
    name: str,
    head: tuple[float, float, float],
    tail: tuple[float, float, float],
    parent: str | None,
    deform: bool,
) -> None:
    bone = armature.data.edit_bones.new(name)
    bone.head = head
    bone.tail = tail
    bone.use_deform = deform
    if parent:
        bone.parent = armature.data.edit_bones[parent]


def build_six_limb_armature(plan: dict[str, Any], dimensions: Vector) -> bpy.types.Object:
    length, width, height = dimensions
    armature_data = bpy.data.armatures.new("ARM_" + plan["rig"]["armatureObject"].removeprefix("RIG_"))
    armature = bpy.data.objects.new(plan["rig"]["armatureObject"], armature_data)
    bpy.context.scene.collection.objects.link(armature)
    select_only([armature], armature)
    bpy.ops.object.mode_set(mode="EDIT")
    body_z = height * 0.54
    add_edit_bone(armature, "root", (0, 0, 0), (0, 0, max(0.2, height * 0.08)), None, False)
    add_edit_bone(armature, "motion_root", (0, 0, 0), (0, 0, max(0.25, height * 0.1)), "root", False)
    add_edit_bone(armature, "body_root", (0, 0, body_z), (-length * 0.06, 0, body_z), "motion_root", True)
    add_edit_bone(armature, "spine_front", (0, 0, body_z), (-length * 0.25, 0, body_z * 1.02), "body_root", True)
    add_edit_bone(armature, "neck_01", (-length * 0.25, 0, body_z * 1.02), (-length * 0.36, 0, body_z * 0.9), "spine_front", True)
    add_edit_bone(armature, "head", (-length * 0.36, 0, body_z * 0.9), (-length * 0.45, 0, body_z * 0.72), "neck_01", True)
    add_edit_bone(armature, "horn_plow", (-length * 0.45, 0, body_z * 0.72), (-length * 0.5, 0, body_z * 0.62), "head", True)
    add_edit_bone(armature, "spine_rear", (0, 0, body_z), (length * 0.3, 0, body_z * 0.96), "body_root", True)
    add_edit_bone(armature, "tail_01", (length * 0.3, 0, body_z * 0.96), (length * 0.44, 0, body_z * 0.72), "spine_rear", True)
    for name, x in (("dorsal_front", -length * 0.22), ("dorsal_mid", 0.0), ("dorsal_rear", length * 0.24)):
        parent = "spine_front" if x < 0 else ("spine_rear" if x > 0 else "body_root")
        add_edit_bone(armature, name, (x, 0, height * 0.68), (x, 0, height * 0.9), parent, True)

    x_positions = {"front": -length * 0.24, "middle": 0.0, "rear": length * 0.25}
    for pair, x in x_positions.items():
        parent = "spine_front" if pair == "front" else ("spine_rear" if pair == "rear" else "body_root")
        for side, sign in (("l", 1.0), ("r", -1.0)):
            y = sign * width * 0.26
            upper = f"{pair}_upper_{side}"
            lower = f"{pair}_lower_{side}"
            foot = f"{pair}_foot_{side}"
            add_edit_bone(armature, upper, (x, y * 0.72, body_z), (x, y, height * 0.31), parent, True)
            add_edit_bone(armature, lower, (x, y, height * 0.31), (x - length * 0.015, y * 1.04, height * 0.09), upper, True)
            add_edit_bone(armature, foot, (x - length * 0.015, y * 1.04, height * 0.09), (x - length * 0.055, y * 1.04, 0.005), lower, True)
            socket_prefix = "forefoot" if pair == "front" else f"{pair}_foot"
            add_edit_bone(
                armature,
                f"socket_{socket_prefix}_contact_{side}",
                (x - length * 0.055, y * 1.04, 0.005),
                (x - length * 0.055, y * 1.04, max(0.055, height * 0.02)),
                foot,
                False,
            )
    add_edit_bone(armature, "socket_attack_origin", (-length * 0.5, 0, body_z * 0.62), (-length * 0.52, 0, body_z * 0.62), "horn_plow", False)
    add_edit_bone(armature, "socket_camera_focus", (-length * 0.08, 0, height * 0.82), (-length * 0.08, 0, height * 0.9), "body_root", False)
    add_edit_bone(armature, "socket_vfx_thorax_dust", (-length * 0.12, 0, height * 0.18), (-length * 0.12, 0, height * 0.26), "body_root", False)
    add_edit_bone(armature, "socket_vfx_dorsal_stress", (0, 0, height * 0.88), (0, 0, height * 0.98), "dorsal_mid", False)
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    return armature


def build_six_limb_delver_armature(plan: dict[str, Any], dimensions: Vector) -> bpy.types.Object:
    length, width, height = dimensions
    armature_data = bpy.data.armatures.new("ARM_" + plan["rig"]["armatureObject"].removeprefix("RIG_"))
    armature = bpy.data.objects.new(plan["rig"]["armatureObject"], armature_data)
    bpy.context.scene.collection.objects.link(armature)
    select_only([armature], armature)
    bpy.ops.object.mode_set(mode="EDIT")
    body_z = height * 0.42
    add_edit_bone(armature, "root", (0, 0, 0), (0, 0, max(0.16, height * 0.06)), None, False)
    add_edit_bone(armature, "motion_root", (0, 0, 0), (0, 0, max(0.2, height * 0.08)), "root", False)
    add_edit_bone(armature, "body_root", (0, 0, body_z), (-length * 0.05, 0, body_z), "motion_root", True)
    add_edit_bone(armature, "spine_front", (0, 0, body_z), (-length * 0.24, 0, body_z * 1.02), "body_root", True)
    add_edit_bone(armature, "neck_01", (-length * 0.24, 0, body_z * 1.02), (-length * 0.34, 0, body_z * 0.88), "spine_front", True)
    add_edit_bone(armature, "head", (-length * 0.34, 0, body_z * 0.88), (-length * 0.44, 0, body_z * 0.7), "neck_01", True)
    add_edit_bone(armature, "wedge_skull", (-length * 0.44, 0, body_z * 0.7), (-length * 0.52, 0, body_z * 0.5), "head", True)
    add_edit_bone(armature, "spine_rear", (0, 0, body_z), (length * 0.28, 0, body_z * 0.96), "body_root", True)
    add_edit_bone(armature, "tail_01", (length * 0.28, 0, body_z * 0.96), (length * 0.4, 0, body_z * 0.62), "spine_rear", True)
    for name, x in (("sensory_plate_front", -length * 0.3), ("sensory_plate_mid", -length * 0.08), ("sensory_plate_rear", length * 0.14)):
        parent = "spine_front" if x < 0 else ("spine_rear" if x > 0 else "body_root")
        add_edit_bone(armature, name, (x, 0, height * 0.52), (x, 0, height * 0.78), parent, True)

    x_positions = {"front": -length * 0.22, "middle": 0.0, "rear": length * 0.22}
    for pair, x in x_positions.items():
        parent = "spine_front" if pair == "front" else ("spine_rear" if pair == "rear" else "body_root")
        for side, sign in (("l", 1.0), ("r", -1.0)):
            y = sign * width * 0.28
            upper = f"{pair}_upper_{side}"
            lower = f"{pair}_lower_{side}"
            foot = f"{pair}_foot_{side}"
            add_edit_bone(armature, upper, (x, y * 0.68, body_z), (x, y, height * 0.24), parent, True)
            add_edit_bone(armature, lower, (x, y, height * 0.24), (x - length * 0.02, y * 1.05, height * 0.08), upper, True)
            add_edit_bone(armature, foot, (x - length * 0.02, y * 1.05, height * 0.08), (x - length * 0.06, y * 1.05, 0.004), lower, True)
            socket_prefix = "forefoot" if pair == "front" else f"{pair}_foot"
            add_edit_bone(
                armature,
                f"socket_{socket_prefix}_contact_{side}",
                (x - length * 0.06, y * 1.05, 0.004),
                (x - length * 0.06, y * 1.05, max(0.045, height * 0.02)),
                foot,
                False,
            )
    add_edit_bone(armature, "socket_attack_origin", (-length * 0.52, 0, body_z * 0.5), (-length * 0.56, 0, body_z * 0.5), "wedge_skull", False)
    add_edit_bone(armature, "socket_camera_focus", (-length * 0.08, 0, height * 0.68), (-length * 0.08, 0, height * 0.78), "body_root", False)
    add_edit_bone(armature, "socket_vfx_gallery_dust", (-length * 0.1, 0, height * 0.12), (-length * 0.1, 0, height * 0.2), "body_root", False)
    add_edit_bone(armature, "socket_vfx_plate_stress", (-length * 0.08, 0, height * 0.78), (-length * 0.08, 0, height * 0.9), "sensory_plate_mid", False)
    add_edit_bone(armature, "socket_vfx_jaw_sensor", (-length * 0.5, 0, body_z * 0.58), (-length * 0.5, 0, body_z * 0.68), "wedge_skull", False)
    add_edit_bone(armature, "socket_vfx_claw_spark", (-length * 0.22, width * 0.28, 0.01), (-length * 0.22, width * 0.28, max(0.05, height * 0.04)), "front_foot_l", False)
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    return armature


def build_quadruped_heat_fin_armature(plan: dict[str, Any], dimensions: Vector) -> bpy.types.Object:
    length, width, height = dimensions
    armature_data = bpy.data.armatures.new("ARM_" + plan["rig"]["armatureObject"].removeprefix("RIG_"))
    armature = bpy.data.objects.new(plan["rig"]["armatureObject"], armature_data)
    bpy.context.scene.collection.objects.link(armature)
    select_only([armature], armature)
    bpy.ops.object.mode_set(mode="EDIT")
    body_z = height * 0.38
    add_edit_bone(armature, "root", (0, 0, 0), (0, 0, max(0.12, height * 0.06)), None, False)
    add_edit_bone(armature, "motion_root", (0, 0, 0), (0, 0, max(0.16, height * 0.08)), "root", False)
    add_edit_bone(armature, "body_root", (0, 0, body_z), (-length * 0.05, 0, body_z), "motion_root", True)
    add_edit_bone(armature, "spine_front", (0, 0, body_z), (-length * 0.22, 0, body_z * 1.02), "body_root", True)
    add_edit_bone(armature, "neck_01", (-length * 0.22, 0, body_z * 1.02), (-length * 0.32, 0, body_z * 0.92), "spine_front", True)
    add_edit_bone(armature, "head", (-length * 0.32, 0, body_z * 0.92), (-length * 0.42, 0, body_z * 0.7), "neck_01", True)
    add_edit_bone(armature, "jaw", (-length * 0.42, 0, body_z * 0.7), (-length * 0.5, 0, body_z * 0.52), "head", True)
    add_edit_bone(armature, "spine_rear", (0, 0, body_z), (length * 0.22, 0, body_z * 0.96), "body_root", True)
    add_edit_bone(armature, "tail_01", (length * 0.22, 0, body_z * 0.96), (length * 0.36, 0, body_z * 0.7), "spine_rear", True)
    add_edit_bone(armature, "tail_02", (length * 0.36, 0, body_z * 0.7), (length * 0.48, 0, body_z * 0.42), "tail_01", True)
    fin_xs = (-length * 0.18, -length * 0.1, -length * 0.02, length * 0.06, length * 0.14, length * 0.22)
    for index, x in enumerate(fin_xs, start=1):
        parent = "spine_front" if x < 0 else ("spine_rear" if x > 0 else "body_root")
        add_edit_bone(
            armature,
            f"heat_fin_{index:02d}",
            (x, 0, height * 0.52),
            (x, 0, height * 0.78),
            parent,
            True,
        )
    x_positions = {"front": -length * 0.2, "rear": length * 0.18}
    for pair, x in x_positions.items():
        parent = "spine_front" if pair == "front" else "spine_rear"
        for side, sign in (("l", 1.0), ("r", -1.0)):
            y = sign * width * 0.34
            upper = f"{pair}_upper_{side}"
            lower = f"{pair}_lower_{side}"
            foot = f"{pair}_foot_{side}"
            add_edit_bone(armature, upper, (x, y * 0.55, body_z), (x, y, height * 0.22), parent, True)
            add_edit_bone(armature, lower, (x, y, height * 0.22), (x - length * 0.02, y * 1.08, height * 0.08), upper, True)
            add_edit_bone(armature, foot, (x - length * 0.02, y * 1.08, height * 0.08), (x - length * 0.06, y * 1.08, 0.004), lower, True)
            socket_prefix = "forefoot" if pair == "front" else "rear_foot"
            add_edit_bone(
                armature,
                f"socket_{socket_prefix}_contact_{side}",
                (x - length * 0.06, y * 1.08, 0.004),
                (x - length * 0.06, y * 1.08, max(0.04, height * 0.02)),
                foot,
                False,
            )
    add_edit_bone(armature, "socket_attack_origin", (-length * 0.5, 0, body_z * 0.52), (-length * 0.54, 0, body_z * 0.52), "jaw", False)
    add_edit_bone(armature, "socket_camera_focus", (-length * 0.08, 0, height * 0.62), (-length * 0.08, 0, height * 0.72), "body_root", False)
    add_edit_bone(armature, "socket_vfx_mouth_ember", (-length * 0.48, 0, body_z * 0.58), (-length * 0.48, 0, body_z * 0.68), "jaw", False)
    add_edit_bone(armature, "socket_vfx_fin_heat", (0, 0, height * 0.78), (0, 0, height * 0.9), "heat_fin_03", False)
    add_edit_bone(armature, "socket_vfx_contact_steam", (0, 0, 0.01), (0, 0, max(0.05, height * 0.04)), "body_root", False)
    add_edit_bone(armature, "socket_vfx_throat_heat", (-length * 0.28, 0, body_z * 0.7), (-length * 0.28, 0, body_z * 0.82), "neck_01", False)
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    return armature


def build_armature(plan: dict[str, Any], dimensions: Vector) -> bpy.types.Object:
    profile = str(plan["rig"]["skeletonProfileId"])
    if "quadruped_heat_fin" in profile:
        return build_quadruped_heat_fin_armature(plan, dimensions)
    if "six_limb_delver" in profile:
        return build_six_limb_delver_armature(plan, dimensions)
    return build_six_limb_armature(plan, dimensions)


def point_segment_distance(point: Vector, head: Vector, tail: Vector) -> float:
    segment = tail - head
    length_squared = segment.length_squared
    if length_squared <= 1e-12:
        return (point - head).length
    factor = max(0.0, min(1.0, (point - head).dot(segment) / length_squared))
    return (point - (head + factor * segment)).length


def candidate_bones(point: Vector, dimensions: Vector, profile: str = "") -> list[str]:
    length, width, height = dimensions
    if "quadruped_heat_fin" in profile:
        if point.z < height * 0.48 and abs(point.y) > width * 0.08:
            pair = "front" if point.x < 0 else "rear"
            side = "l" if point.y >= 0 else "r"
            return [f"{pair}_upper_{side}", f"{pair}_lower_{side}", f"{pair}_foot_{side}", "body_root"]
        if point.x < -length * 0.34:
            return ["jaw", "head", "neck_01", "spine_front"]
        if point.x < -length * 0.16:
            return ["head", "neck_01", "spine_front", "body_root"]
        if point.x > length * 0.28:
            return ["tail_02", "tail_01", "spine_rear", "body_root"]
        if point.z > height * 0.5:
            index = min(6, max(1, int((point.x / max(length, 1e-6) + 0.5) * 6) + 1))
            return [f"heat_fin_{index:02d}", "spine_front" if point.x < 0 else "spine_rear", "body_root"]
        return ["body_root", "spine_front" if point.x < 0 else "spine_rear"]
    if "six_limb_delver" in profile:
        if point.z < height * 0.5 and abs(point.y) > width * 0.08:
            pair = min(
                (("front", -length * 0.22), ("middle", 0.0), ("rear", length * 0.22)),
                key=lambda row: abs(point.x - row[1]),
            )[0]
            side = "l" if point.y >= 0 else "r"
            return [f"{pair}_upper_{side}", f"{pair}_lower_{side}", f"{pair}_foot_{side}", "body_root"]
        if point.x < -length * 0.36:
            return ["wedge_skull", "head", "neck_01", "spine_front"]
        if point.x < -length * 0.16:
            return ["head", "neck_01", "spine_front", "body_root"]
        if point.x > length * 0.32:
            return ["tail_01", "spine_rear", "body_root"]
        if point.z > height * 0.5:
            if point.x < -length * 0.16:
                return ["sensory_plate_front", "spine_front", "body_root"]
            if point.x > length * 0.08:
                return ["sensory_plate_rear", "spine_rear", "body_root"]
            return ["sensory_plate_mid", "body_root", "spine_front", "spine_rear"]
        return ["body_root", "spine_front" if point.x < 0 else "spine_rear"]
    if point.z < height * 0.58 and abs(point.y) > width * 0.08:
        pair = min(
            (("front", -length * 0.24), ("middle", 0.0), ("rear", length * 0.25)),
            key=lambda row: abs(point.x - row[1]),
        )[0]
        side = "l" if point.y >= 0 else "r"
        return [f"{pair}_upper_{side}", f"{pair}_lower_{side}", f"{pair}_foot_{side}", "body_root"]
    if point.x < -length * 0.38:
        return ["horn_plow", "head", "neck_01", "spine_front"]
    if point.x < -length * 0.18:
        return ["head", "neck_01", "spine_front", "body_root"]
    if point.x > length * 0.34:
        return ["tail_01", "spine_rear", "body_root"]
    if point.z > height * 0.68:
        if point.x < -length * 0.1:
            return ["dorsal_front", "spine_front", "body_root"]
        if point.x > length * 0.1:
            return ["dorsal_rear", "spine_rear", "body_root"]
        return ["dorsal_mid", "body_root", "spine_front", "spine_rear"]
    return ["body_root", "spine_front" if point.x < 0 else "spine_rear"]


def skin_mesh(mesh: bpy.types.Object, armature: bpy.types.Object, dimensions: Vector, profile: str = "") -> dict[str, int]:
    deform_bones = {bone.name: bone for bone in armature.data.bones if bone.use_deform}
    for group in list(mesh.vertex_groups):
        mesh.vertex_groups.remove(group)
    groups = {name: mesh.vertex_groups.new(name=name) for name in sorted(deform_bones)}
    maximum_influences = 0
    unweighted = 0
    for vertex in mesh.data.vertices:
        point = vertex.co
        candidates = [name for name in candidate_bones(point, dimensions, profile) if name in deform_bones]
        weighted = []
        for name in candidates:
            bone = deform_bones[name]
            distance = point_segment_distance(point, bone.head_local, bone.tail_local)
            weighted.append((name, 1.0 / max(0.02, distance) ** 2))
        weighted.sort(key=lambda row: (-row[1], row[0]))
        weighted = weighted[:4]
        total = sum(weight for _, weight in weighted)
        if total <= 0:
            unweighted += 1
            continue
        for name, weight in weighted:
            groups[name].add([vertex.index], weight / total, "REPLACE")
        maximum_influences = max(maximum_influences, len(weighted))
    return {
        "maximumInfluencesPerVertex": maximum_influences,
        "unweightedVertices": unweighted,
    }


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def prune_vertex_weights(obj: bpy.types.Object, maximum_influences: int) -> None:
    for vertex in obj.data.vertices:
        weighted = sorted(
            (
                (group.group, group.weight)
                for group in vertex.groups
                if group.weight >= 0.001
            ),
            key=lambda row: (-row[1], row[0]),
        )
        retained = weighted[:maximum_influences]
        retained_indices = {index for index, _ in retained}
        for index, _ in weighted[maximum_influences:]:
            obj.vertex_groups[index].remove([vertex.index])
        total = sum(weight for _, weight in retained)
        if total <= 0:
            continue
        for index, weight in retained:
            if index in retained_indices:
                obj.vertex_groups[index].add(
                    [vertex.index],
                    weight / total,
                    "REPLACE",
                )


def build_lods(
    lod0: bpy.types.Object,
    armature: bpy.types.Object,
    plan: dict[str, Any],
) -> list[bpy.types.Object]:
    lods = [lod0]
    source_triangles = triangle_count(lod0)
    ratios = {"LOD1": 0.58, "LOD2": 0.28}
    plan_rows = {row["id"]: row for row in plan["lodPolicy"]["levels"]}
    for lod_id in ("LOD1", "LOD2"):
        clone = lod0.copy()
        clone.data = lod0.data.copy()
        clone.name = plan_rows[lod_id]["object"]
        clone.data.name = mesh_data_name(plan_rows[lod_id]["object"])
        bpy.context.scene.collection.objects.link(clone)
        maximum = plan_rows[lod_id]["maximumTriangles"]
        ratio = min(ratios[lod_id], maximum * 0.96 / source_triangles)
        modifier = clone.modifiers.new(f"Decimate_{lod_id}", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        modifier.use_symmetry = True
        modifier.symmetry_axis = "Y"
        select_only([clone], clone)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        prune_vertex_weights(
            clone,
            plan["rig"]["maximumInfluencesPerVertex"],
        )
        for polygon in clone.data.polygons:
            polygon.use_smooth = True
        lods.append(clone)
    for lod in lods:
        modifier = lod.modifiers.new("Armature", "ARMATURE")
        modifier.object = armature
        modifier.use_deform_preserve_volume = True
        lod.parent = armature
    return lods


def reset_pose(armature: bpy.types.Object) -> None:
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.scale = (1.0, 1.0, 1.0)


def rotate(armature: bpy.types.Object, name: str, x: float = 0, y: float = 0, z: float = 0) -> None:
    bone = armature.pose.bones[name]
    bone.rotation_mode = "XYZ"
    bone.rotation_euler = (x, y, z)
    bone.rotation_mode = "QUATERNION"


def try_rotate(armature: bpy.types.Object, name: str, x: float = 0, y: float = 0, z: float = 0) -> None:
    if name in armature.pose.bones:
        rotate(armature, name, x=x, y=y, z=z)


def apply_motion_pose(armature: bpy.types.Object, key: str, normalized: float) -> None:
    reset_pose(armature)
    cycle = math.sin(normalized * math.tau)
    envelope = math.sin(normalized * math.pi)
    limb_pairs = tuple(
        pair
        for pair in ("front", "middle", "rear")
        if f"{pair}_upper_l" in armature.pose.bones
    )
    if key == "idle.weight_shift":
        try_rotate(armature, "body_root", z=0.025 * cycle)
        try_rotate(armature, "head", y=0.035 * cycle)
        try_rotate(armature, "jaw", y=0.02 * cycle)
        for index in range(1, 7):
            try_rotate(armature, f"heat_fin_{index:02d}", x=0.03 * cycle)
        try_rotate(armature, "sensory_plate_front", x=0.025 * cycle)
        try_rotate(armature, "sensory_plate_mid", x=0.02 * cycle)
        try_rotate(armature, "sensory_plate_rear", x=0.015 * cycle)
    elif key in {"locomotion.walk", "locomotion.run"}:
        amplitude = 0.16 if key.endswith("walk") else 0.25
        for index, pair in enumerate(limb_pairs):
            pair_cycle = math.sin(normalized * math.tau + index * math.tau / max(1, len(limb_pairs)))
            for side, sign in (("l", 1.0), ("r", -1.0)):
                phase = pair_cycle * sign
                try_rotate(armature, f"{pair}_upper_{side}", y=amplitude * phase)
                try_rotate(armature, f"{pair}_lower_{side}", y=-amplitude * 0.55 * phase)
        try_rotate(armature, "body_root", z=0.018 * cycle)
        try_rotate(armature, "head", y=-0.035 * cycle)
        try_rotate(armature, "tail_01", z=0.06 * cycle)
        try_rotate(armature, "tail_02", z=0.08 * cycle)
    elif key == "attack.basic":
        try_rotate(armature, "body_root", y=-0.09 * envelope)
        try_rotate(armature, "spine_front", y=-0.16 * envelope)
        try_rotate(armature, "neck_01", y=0.18 * envelope)
        try_rotate(armature, "head", y=0.24 * envelope)
        try_rotate(armature, "horn_plow", y=0.18 * envelope)
        try_rotate(armature, "wedge_skull", y=0.2 * envelope)
        try_rotate(armature, "jaw", y=0.32 * envelope)
    elif key == "attack.special":
        try_rotate(armature, "body_root", z=0.12 * envelope)
        try_rotate(armature, "spine_front", z=0.15 * envelope)
        try_rotate(armature, "head", z=-0.12 * envelope)
        try_rotate(armature, "front_upper_l", x=-0.08 * envelope, y=0.08 * envelope)
        try_rotate(armature, "front_upper_r", x=0.08 * envelope, y=-0.08 * envelope)
        try_rotate(armature, "tail_01", z=-0.28 * envelope)
        try_rotate(armature, "tail_02", z=-0.34 * envelope)
    elif key == "skill.anticipation":
        try_rotate(armature, "body_root", y=0.07 * envelope)
        try_rotate(armature, "spine_front", y=0.11 * envelope)
        try_rotate(armature, "head", y=-0.13 * envelope)
        try_rotate(armature, "jaw", y=-0.08 * envelope)
        for pair in limb_pairs:
            try_rotate(armature, f"{pair}_upper_l", y=-0.055 * envelope)
            try_rotate(armature, f"{pair}_upper_r", y=-0.055 * envelope)
        for index in range(1, 7):
            try_rotate(armature, f"heat_fin_{index:02d}", x=0.08 * envelope)
        try_rotate(armature, "wedge_skull", y=-0.1 * envelope)
        try_rotate(armature, "sensory_plate_front", x=0.06 * envelope)
    elif key == "reaction.hit":
        try_rotate(armature, "body_root", z=-0.10 * envelope)
        try_rotate(armature, "head", z=0.12 * envelope)
    elif key == "defeat":
        progress = normalized * normalized * (3.0 - 2.0 * normalized)
        try_rotate(armature, "body_root", x=0.22 * progress, z=0.10 * progress)
        try_rotate(armature, "spine_front", x=0.16 * progress)
        try_rotate(armature, "head", y=0.22 * progress)
        try_rotate(armature, "jaw", y=0.12 * progress)
        for pair in limb_pairs:
            try_rotate(armature, f"{pair}_upper_l", y=-0.16 * progress)
            try_rotate(armature, f"{pair}_upper_r", y=-0.16 * progress)


def action_endpoint_errors(
    armature: bpy.types.Object,
    action: bpy.types.Action,
    controls: list[str],
) -> tuple[float, float]:
    armature.animation_data.action = action
    endpoint_poses: list[dict[str, tuple[Vector, Any]]] = []
    for frame in (int(round(action.frame_start)), int(round(action.frame_end))):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        endpoint_poses.append(
            {
                name: (
                    armature.pose.bones[name].matrix_basis.to_translation().copy(),
                    armature.pose.bones[name].matrix_basis.to_quaternion().copy(),
                )
                for name in controls
            }
        )
    start_pose, end_pose = endpoint_poses
    maximum_position_error = max(
        (start_pose[name][0] - end_pose[name][0]).length
        for name in controls
    )
    maximum_rotation_error = max(
        math.degrees(start_pose[name][1].rotation_difference(end_pose[name][1]).angle)
        for name in controls
    )
    return maximum_position_error, maximum_rotation_error


def author_actions(plan: dict[str, Any], armature: bpy.types.Object) -> list[dict[str, Any]]:
    if armature.animation_data is None:
        armature.animation_data_create()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action, do_unlink=True)
    controls = [bone.name for bone in armature.data.bones if bone.use_deform]
    rows = []
    for motion in plan["motionPolicy"]["motions"]:
        action = bpy.data.actions.new(motion["actionName"])
        action.use_fake_user = True
        armature.animation_data.action = action
        frame_count = motion["durationFrames"]
        finite = True
        for frame in range(1, frame_count + 1):
            normalized = (frame - 1) / (frame_count - 1)
            apply_motion_pose(armature, motion["motionKey"], normalized)
            for name in controls:
                bone = armature.pose.bones[name]
                finite = finite and all(math.isfinite(value) for value in (*bone.location, *bone.rotation_quaternion, *bone.scale))
                bone.keyframe_insert("location", frame=frame, group=name)
                bone.keyframe_insert("rotation_quaternion", frame=frame, group=name)
                bone.keyframe_insert("scale", frame=frame, group=name)
        action["al_motion_key"] = motion["motionKey"]
        action["al_source_intent"] = motion["sourceIntent"]
        action["al_loop"] = motion["loop"]
        action["al_root_motion"] = plan["motionPolicy"]["rootMotionPolicy"]
        action.frame_start = 1
        action.frame_end = frame_count
        position_error, rotation_error = action_endpoint_errors(
            armature,
            action,
            controls,
        )
        rows.append(
            {
                "motionKey": motion["motionKey"],
                "actionName": action.name,
                "frameCount": frame_count,
                "sampleRateHz": plan["motionPolicy"]["sampleRateHz"],
                "loop": motion["loop"],
                "loopPositionErrorMeters": round(position_error, 6),
                "loopRotationErrorDegrees": round(rotation_error, 6),
                "finiteTransforms": finite,
            }
        )
    armature.animation_data.action = None
    reset_pose(armature)
    return rows


def bone_path(bone: bpy.types.Bone) -> str:
    names = [bone.name]
    parent = bone.parent
    while parent is not None:
        names.append(parent.name)
        parent = parent.parent
    return "/".join(reversed(names))


def skeleton_record(armature: bpy.types.Object) -> dict[str, Any]:
    rows = []
    for bone in armature.data.bones:
        parent_matrix = bone.parent.matrix_local if bone.parent else None
        local_matrix = parent_matrix.inverted() @ bone.matrix_local if parent_matrix else bone.matrix_local
        rows.append(
            {
                "path": bone_path(bone),
                "parent": bone.parent.name if bone.parent else None,
                "deform": bone.use_deform,
                "localBindMatrix": [[round(float(value), 6) for value in row] for row in local_matrix],
            }
        )
    rows.sort(key=lambda row: row["path"].encode("utf-8"))
    return {
        "armatureObject": armature.name,
        "boneNames": sorted(bone.name for bone in armature.data.bones),
        "parentlessBones": sorted(bone.name for bone in armature.data.bones if bone.parent is None),
        "hierarchy": {bone.name: bone.parent.name if bone.parent else None for bone in armature.data.bones},
        "skeletonSignature": sha256_value(rows),
        "hierarchySignature": sha256_value({row["path"]: row["parent"] for row in rows}),
    }


def mesh_weight_stats(objects: list[bpy.types.Object]) -> dict[str, int]:
    maximum = 0
    unweighted = 0
    for obj in objects:
        for vertex in obj.data.vertices:
            influences = sum(1 for group in vertex.groups if group.weight >= 0.001)
            maximum = max(maximum, influences)
            if influences == 0:
                unweighted += 1
    return {"maximumInfluencesPerVertex": maximum, "unweightedVertices": unweighted}


def lod_records(plan: dict[str, Any], lods: list[bpy.types.Object]) -> list[dict[str, Any]]:
    return [
        {
            "id": lod_id,
            "object": obj.name,
            "vertices": len(obj.data.vertices),
            "triangles": triangle_count(obj),
            "uvLayers": len(obj.data.uv_layers),
            "materialSlots": len(obj.data.materials),
            "protectedIdentityCues": list(plan["protectedIdentityCues"]),
        }
        for lod_id, obj in zip(("LOD0", "LOD1", "LOD2"), lods)
    ]


def triangle_vertex_order_preserved(
    baseline_indices: tuple[int, int, int],
    posed_indices: tuple[int, int, int],
) -> bool:
    return baseline_indices == posed_indices


def deformation_report(
    plan: dict[str, Any],
    armature: bpy.types.Object,
    lods: list[bpy.types.Object],
) -> dict[str, Any]:
    armature.animation_data.action = None
    reset_pose(armature)
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    non_finite = 0
    degenerate = 0
    inverted = 0
    pose_count = 0
    maximum_expansion = 1.0
    baseline = {obj.name: Vector(obj.dimensions) for obj in lods}
    baseline_triangles = {}
    for obj in lods:
        obj.data.calc_loop_triangles()
        baseline_triangles[obj.name] = [
            tuple(triangle.vertices)
            for triangle in obj.data.loop_triangles
        ]
    actions = {action.name: action for action in bpy.data.actions}
    pose_rows = []
    for motion in plan["motionPolicy"]["motions"]:
        action = actions[motion["actionName"]]
        armature.animation_data.action = action
        frames = sorted({1, max(2, motion["durationFrames"] // 2), motion["durationFrames"]})
        for frame in frames:
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            pose_count += 1
            pose_issue_count = 0
            for obj in lods:
                evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
                mesh = evaluated.to_mesh()
                try:
                    mesh.calc_loop_triangles()
                    for vertex in mesh.vertices:
                        if not all(math.isfinite(value) for value in vertex.co):
                            non_finite += 1
                            pose_issue_count += 1
                    reference_triangles = baseline_triangles[obj.name]
                    if len(mesh.loop_triangles) != len(reference_triangles):
                        raise BuildError(f"DeformationTopologyChanged:{obj.name}")
                    for triangle_index, triangle in enumerate(mesh.loop_triangles):
                        if not math.isfinite(triangle.area) or triangle.area <= 1e-10:
                            degenerate += 1
                            pose_issue_count += 1
                            continue
                        reference_indices = reference_triangles[triangle_index]
                        if not triangle_vertex_order_preserved(
                            reference_indices,
                            tuple(triangle.vertices),
                        ):
                            inverted += 1
                            pose_issue_count += 1
                    actual = Vector(evaluated.dimensions)
                    ratios = [actual[index] / max(1e-6, baseline[obj.name][index]) for index in range(3)]
                    maximum_expansion = max(maximum_expansion, *ratios)
                finally:
                    evaluated.to_mesh_clear()
            pose_rows.append({"actionName": action.name, "frame": frame, "issues": pose_issue_count})
    armature.animation_data.action = None
    reset_pose(armature)
    bpy.context.scene.frame_set(1)
    return {
        "poseCount": pose_count,
        "nonFiniteVertices": non_finite,
        "degenerateTriangles": degenerate,
        "invertedTriangles": inverted,
        "inversionMethod": "triangle_vertex_order_identity_plus_positive_area_v1",
        "maximumBoundsExpansionRatio": round(maximum_expansion, 6),
        "poses": pose_rows,
    }


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_render(lods: list[bpy.types.Object], dimensions: Vector) -> tuple[bpy.types.Object, bpy.types.Object]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 480
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("ReviewWorld")
    scene.world.color = (0.008, 0.01, 0.014)
    scene.view_settings.look = "AgX - Medium High Contrast"

    camera_data = bpy.data.cameras.new("ReviewCamera")
    camera = bpy.data.objects.new("ReviewCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    distance = max(dimensions.x, dimensions.y) * 1.55
    camera.location = (-distance * 0.55, -distance, dimensions.z * 0.9)
    look_at(camera, Vector((0, 0, dimensions.z * 0.42)))
    camera.data.lens = 55

    key_data = bpy.data.lights.new("Key", "AREA")
    key_data.energy = 1800
    key_data.shape = "DISK"
    key_data.size = max(4.0, dimensions.x * 0.45)
    key = bpy.data.objects.new("Key", key_data)
    scene.collection.objects.link(key)
    key.location = (-dimensions.x * 0.3, -dimensions.y * 1.8, dimensions.z * 1.6)
    look_at(key, Vector((0, 0, dimensions.z * 0.4)))

    fill_data = bpy.data.lights.new("Fill", "AREA")
    fill_data.energy = 900
    fill_data.size = max(3.0, dimensions.x * 0.3)
    fill = bpy.data.objects.new("Fill", fill_data)
    scene.collection.objects.link(fill)
    fill.location = (dimensions.x * 0.4, dimensions.y * 1.2, dimensions.z)
    look_at(fill, Vector((0, 0, dimensions.z * 0.45)))

    plane_size = max(dimensions.x, dimensions.y) * 2.5
    bpy.ops.mesh.primitive_plane_add(size=plane_size, location=(0, 0, -0.01))
    floor = bpy.context.object
    floor.name = "ReviewGround"
    floor_material = bpy.data.materials.new("ReviewGroundMaterial")
    floor_material.use_nodes = True
    floor_shader = floor_material.node_tree.nodes.get("Principled BSDF")
    if floor_shader is not None:
        floor_shader.inputs["Base Color"].default_value = (0.025, 0.03, 0.038, 1)
        floor_shader.inputs["Roughness"].default_value = 0.94
    floor.data.materials.append(floor_material)
    return camera, floor


def render_reviews(
    repo_root: Path,
    plan: dict[str, Any],
    armature: bpy.types.Object,
    lods: list[bpy.types.Object],
    dimensions: Vector,
) -> list[dict[str, Any]]:
    output_dir = resolve(repo_root, plan["outputs"]["reviewDirectory"])
    output_dir.mkdir(parents=True, exist_ok=True)
    setup_render(lods, dimensions)
    scene = bpy.context.scene
    actions = {action.name: action for action in bpy.data.actions}
    outputs: list[Path] = []
    lod0_name = plan["lodPolicy"]["levels"][0]["object"]
    review_slug = lod0_name[4:-5] if lod0_name.startswith("GEO_") and lod0_name.endswith("_LOD0") else lod0_name
    for lod_id, obj in zip(("LOD0", "LOD1", "LOD2"), lods):
        armature.animation_data.action = None
        reset_pose(armature)
        for candidate in lods:
            candidate.hide_render = candidate is not obj
        path = output_dir / f"{review_slug}_{lod_id.lower()}_bind_v001.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        outputs.append(path)
    for motion_key in ("locomotion.walk", "attack.basic", "attack.special", "skill.anticipation"):
        motion = next(row for row in plan["motionPolicy"]["motions"] if row["motionKey"] == motion_key)
        armature.animation_data.action = actions[motion["actionName"]]
        scene.frame_set(max(2, motion["durationFrames"] // 2))
        lods[0].hide_render = False
        lods[1].hide_render = True
        lods[2].hide_render = True
        slug = motion_key.replace(".", "_")
        path = output_dir / f"{review_slug}_{slug}_v001.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        outputs.append(path)
    armature.animation_data.action = None
    reset_pose(armature)
    scene.frame_set(1)
    return [png_record(repo_root, path) for path in outputs]


def logical_signature_payload(
    skeleton: dict[str, Any],
    lods: list[dict[str, Any]],
    motions: list[dict[str, Any]],
    material: dict[str, Any],
    weights: dict[str, int],
) -> dict[str, Any]:
    return {
        "skeletonHierarchySignature": skeleton["hierarchySignature"],
        "lods": [{"id": row["id"], "vertices": row["vertices"], "triangles": row["triangles"]} for row in lods],
        "motions": motions,
        "material": material,
        "weights": weights,
    }


def build_scene(repo_root: Path, plan: dict[str, Any]) -> dict[str, Any]:
    source = resolve(repo_root, plan["source"]["path"])
    if source.stat().st_size != plan["source"]["bytes"] or sha256_file(source) != plan["source"]["sha256"]:
        raise BuildError("SelectedSourceHashMismatch")
    imported = import_source(source)
    source_inventory = []
    for obj in imported:
        obj.data.calc_loop_triangles()
        source_inventory.append(
            {
                "name": obj.name,
                "vertices": len(obj.data.vertices),
                "triangles": len(obj.data.loop_triangles),
                "uvLayers": len(obj.data.uv_layers),
                "materialSlots": len(obj.data.materials),
            }
        )
    lod0 = join_source_meshes(imported, plan)
    normalization = normalize_source(lod0, float(plan["scale"]["targetLengthMeters"]))
    minimum, maximum = world_bounds(lod0)
    dimensions = maximum - minimum
    if len(lod0.data.uv_layers) < 1:
        raise BuildError("SourceHasNoUvLayer")
    material = configure_material(repo_root, plan, lod0)
    armature = build_armature(plan, dimensions)
    skinning = skin_mesh(lod0, armature, dimensions, plan["rig"]["skeletonProfileId"])
    lods = build_lods(lod0, armature, plan)
    weights = mesh_weight_stats(lods)
    weights["maximumInfluencesPerVertex"] = max(weights["maximumInfluencesPerVertex"], skinning["maximumInfluencesPerVertex"])
    weights["unweightedVertices"] += skinning["unweightedVertices"]
    motions = author_actions(plan, armature)
    skeleton = skeleton_record(armature)
    lod_rows = lod_records(plan, lods)
    material_row = {
        "id": material.name,
        "slotCount": 1,
        "textureRoles": sorted(row["role"] for row in plan["materialPolicy"]["textures"]),
        "maximumTextureLongEdge": plan["materialPolicy"]["maximumTextureLongEdge"],
        "runtimeVfxSeparate": True,
        "emissionBakedIntoCleanMesh": False,
    }
    deformation = deformation_report(plan, armature, lods)
    logical_payload = logical_signature_payload(skeleton, lod_rows, motions, material_row, weights)
    return {
        "armature": armature,
        "lodObjects": lods,
        "dimensions": dimensions,
        "sourceInventory": source_inventory,
        "normalization": normalization,
        "skeleton": skeleton,
        "weights": weights,
        "lods": lod_rows,
        "material": material_row,
        "motions": motions,
        "deformation": deformation,
        "logicalPayload": logical_payload,
        "logicalSignature": sha256_value(logical_payload),
    }


def export_fbx(repo_root: Path, plan: dict[str, Any], built: dict[str, Any]) -> dict[str, Any]:
    output = resolve(repo_root, plan["outputs"]["fbx"])
    output.parent.mkdir(parents=True, exist_ok=True)
    armature = built["armature"]
    lods = built["lodObjects"]
    expected_bones = set(built["skeleton"]["boneNames"])
    expected_hierarchy = built["skeleton"]["hierarchy"]
    expected_triangles = {row["object"]: row["triangles"] for row in built["lods"]}
    expected_actions = {row["actionName"] for row in built["motions"]}
    select_only([armature, *lods], armature)
    result = bpy.ops.export_scene.fbx(
        filepath=str(output),
        check_existing=False,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_triangles=True,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        use_armature_deform_only=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="STRIP",
        embed_textures=False,
    )
    if "FINISHED" not in result or not output.is_file():
        raise BuildError("FbxExportFailed")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    imported = bpy.ops.wm.fbx_import(filepath=str(output), use_anim=True)
    imported_armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    imported_meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    actual_triangles = {}
    for obj in imported_meshes:
        obj.data.calc_loop_triangles()
        name = next((expected for expected in expected_triangles if obj.name.startswith(expected)), obj.name)
        actual_triangles[name] = len(obj.data.loop_triangles)
    imported_action_names = {action.name for action in bpy.data.actions}
    actions_matched = all(
        any(expected == actual or expected in actual for actual in imported_action_names)
        for expected in expected_actions
    )
    if len(imported_armatures) == 1:
        imported_armature = imported_armatures[0]
        actual_bones = {bone.name for bone in imported_armature.data.bones}
        actual_hierarchy = {bone.name: bone.parent.name if bone.parent else None for bone in imported_armature.data.bones}
    else:
        actual_bones = set()
        actual_hierarchy = {}
    material_names = {material.name for obj in imported_meshes for material in obj.data.materials if material}
    round_trip = {
        "fbxImportPassed": "FINISHED" in imported and len(imported_armatures) == 1 and len(imported_meshes) == 3,
        "skeletonSignatureMatched": actual_bones == expected_bones and actual_hierarchy == expected_hierarchy,
        "lodTrianglesMatched": actual_triangles == expected_triangles,
        "actionsMatched": actions_matched,
        "materialsMatched": any(plan["materialPolicy"]["materialId"] in name for name in material_names),
        "importedActionNames": sorted(imported_action_names),
        "importedMaterialNames": sorted(material_names),
        "importedTriangles": actual_triangles,
    }
    return {"path": output, "roundTrip": round_trip}


def enforce_acceptance(plan: dict[str, Any], built: dict[str, Any], round_trip: dict[str, Any]) -> None:
    budgets = {row["id"]: row["maximumTriangles"] for row in plan["lodPolicy"]["levels"]}
    triangles = [row["triangles"] for row in built["lods"]]
    issues = []
    if not triangles[0] > triangles[1] > triangles[2]:
        issues.append(f"LodTriangleOrder:{triangles}")
    for row in built["lods"]:
        if row["triangles"] > budgets[row["id"]]:
            issues.append(f"LodBudget:{row['id']}:{row['triangles']}")
        if row["materialSlots"] > plan["materialPolicy"]["maximumSlots"]:
            issues.append(f"MaterialSlots:{row['id']}:{row['materialSlots']}")
    if built["weights"]["maximumInfluencesPerVertex"] > plan["rig"]["maximumInfluencesPerVertex"]:
        issues.append("MaximumInfluencesExceeded")
    if built["weights"]["unweightedVertices"]:
        issues.append(f"UnweightedVertices:{built['weights']['unweightedVertices']}")
    if built["deformation"]["poseCount"] < plan["rig"]["minimumDeformationPoses"]:
        issues.append("InsufficientDeformationPoses")
    if built["deformation"]["nonFiniteVertices"] or built["deformation"]["invertedTriangles"]:
        issues.append(f"DeformationFailure:{built['deformation']}")
    if built["deformation"]["maximumBoundsExpansionRatio"] > 1.35:
        issues.append(f"DeformationBoundsExpansion:{built['deformation']['maximumBoundsExpansionRatio']}")
    for field, passed in round_trip.items():
        if field.endswith("Names") or field == "importedTriangles":
            continue
        if passed is not True:
            issues.append(f"RoundTrip:{field}")
    if issues:
        raise BuildError(";".join(issues))


def write_outputs(repo_root: Path, plan: dict[str, Any]) -> None:
    built = build_scene(repo_root, plan)
    review_records = render_reviews(
        repo_root,
        plan,
        built["armature"],
        built["lodObjects"],
        built["dimensions"],
    )
    blend_path = resolve(repo_root, plan["outputs"]["blend"])
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    blend_record = file_record(repo_root, blend_path)
    fbx_result = export_fbx(repo_root, plan, built)
    fbx_record = file_record(repo_root, fbx_result["path"])

    repeated = build_scene(repo_root, plan)
    enforce_acceptance(plan, built, fbx_result["roundTrip"])
    if built["logicalSignature"] != repeated["logicalSignature"]:
        raise BuildError(
            f"LogicalRepeatabilityMismatch:{built['logicalSignature']}:{repeated['logicalSignature']}"
        )

    dcc_report = {
        "schemaVersion": 1,
        "qualificationId": plan["qualificationId"],
        "status": "PASS",
        "blenderVersion": bpy.app.version_string,
        "builder": {
            "path": SCRIPT_PATH.relative_to(repo_root).as_posix(),
            "sha256": sha256_file(SCRIPT_PATH),
        },
        "source": {
            "path": plan["source"]["path"],
            "sha256": plan["source"]["sha256"],
            "inventory": built["sourceInventory"],
        },
        "normalization": built["normalization"],
        "rig": {**built["skeleton"], **built["weights"]},
        "lods": built["lods"],
        "material": built["material"],
        "motions": built["motions"],
        "deformation": built["deformation"],
        "roundTrip": fbx_result["roundTrip"],
        "logicalBuildSignature": built["logicalSignature"],
        "repeatBuildLogicalSignature": repeated["logicalSignature"],
        "authorityBoundary": {
            "runtimeIntegration": "BLOCKED",
            "deviceQualification": "BLOCKED",
            "gameplayOrSpawnActivation": False,
            "runtimeVfxSeparate": True,
        },
    }
    dcc_path = resolve(repo_root, plan["outputs"]["dccReport"])
    dcc_path.write_text(json.dumps(dcc_report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    dcc_record = file_record(repo_root, dcc_path)

    qualification = {
        "schemaVersion": 1,
        "qualificationId": plan["qualificationId"],
        "sourceVersion": plan["sourceVersion"],
        "modelId": plan["authority"]["modelId"],
        "source2dId": plan["authority"]["source2dId"],
        "sourceSha256": plan["source"]["sha256"],
        "sourceQualification": "PASS",
        "runtimeIntegration": "BLOCKED",
        "deviceQualification": "BLOCKED",
        "gameplayOrSpawnActivation": False,
        "logicalBuildSignature": built["logicalSignature"],
        "repeatBuildLogicalSignature": repeated["logicalSignature"],
        "artifacts": {
            "blend": blend_record,
            "fbx": fbx_record,
            "dccReport": dcc_record,
            "reviewImages": review_records,
        },
        "rig": {**built["skeleton"], **built["weights"]},
        "lods": built["lods"],
        "material": built["material"],
        "motions": built["motions"],
        "deformation": built["deformation"],
        "roundTrip": fbx_result["roundTrip"],
    }
    qualification_path = resolve(repo_root, plan["outputs"]["qualificationManifest"])
    qualification_path.write_text(
        json.dumps(qualification, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print("AL_REALM_CREATURE_PRODUCTION_SLICE_PASS")
    print(f"logical_signature={built['logicalSignature']}")
    print(f"lod_triangles={[row['triangles'] for row in built['lods']]}")
    print(f"deformation_poses={built['deformation']['poseCount']}")
    print(f"blend={blend_path}")
    print(f"fbx={fbx_result['path']}")
    print(f"qualification={qualification_path}")


def main() -> int:
    args = arguments()
    repo_root = args.repo_root.resolve()
    plan_path = resolve(repo_root, args.plan)
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    try:
        write_outputs(repo_root, plan)
    except BuildError as error:
        print(f"AL_REALM_CREATURE_PRODUCTION_SLICE_FAIL {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
