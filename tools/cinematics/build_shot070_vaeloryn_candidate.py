"""Build and audit the zero-spend Shot070 Vaeloryn motion-source candidate.

Run with Blender 5.2 or later from the repository root:

    blender --background --python tools/cinematics/build_shot070_vaeloryn_candidate.py -- audit
"""

from __future__ import annotations

import argparse
import bmesh
import hashlib
import json
import math
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any

import bpy
from mathutils import Vector

SCRIPT_PATH = Path(__file__).resolve()
REPOSITORY_ROOT = SCRIPT_PATH.parents[2]
SOURCE_RELATIVE = Path(
    "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/Models/"
    "wish_dragon_vaeloryn/wish_dragon_vaeloryn_source_v001.fbx"
)
ART_ROOT_RELATIVE = Path("unity/ArtSource/Cinematics/Shot070VaelorynSourceV002")
DOC_ROOT_RELATIVE = Path("unity/Docs/Cinematics/Shot070VaelorynSourceV002")
TEXTURE_ROOT_RELATIVE = Path(
    "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/Textures/"
    "wish_dragon_vaeloryn"
)
BLEND_RELATIVE = ART_ROOT_RELATIVE / "shot070_vaeloryn_motion_source_v002.blend"
GLB_RELATIVE = ART_ROOT_RELATIVE / "shot070_vaeloryn_motion_source_v002.glb"
AUDIT_RELATIVE = DOC_ROOT_RELATIVE / "source_audit_v002.json"
RIG_REPORT_RELATIVE = DOC_ROOT_RELATIVE / "rig_articulation_report_v002.json"
LANDSCAPE_RELATIVE = DOC_ROOT_RELATIVE / "shot070_vaeloryn_frame_16x9_v002.png"
PORTRAIT_RELATIVE = DOC_ROOT_RELATIVE / "shot070_vaeloryn_frame_9x16_v002.png"
MOTION_RELATIVE = DOC_ROOT_RELATIVE / "shot070_vaeloryn_motion_review_v002.mp4"
CONTACT_RELATIVE = DOC_ROOT_RELATIVE / "shot070_vaeloryn_motion_contact_v002.png"
MANIFEST_RELATIVE = DOC_ROOT_RELATIVE / "shot070_vaeloryn_source_manifest_v002.json"
SEMANTIC_REGIONS = (
    "body",
    "head",
    "jaw",
    "eye_l",
    "eye_r",
    "wing_arm_l",
    "wing_arm_r",
    "wing_membrane_l",
    "wing_membrane_r",
    "tail",
)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("audit", "build", "verify"))
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(blender_args)


def connected_components(mesh: bpy.types.Mesh) -> list[list[int]]:
    adjacency = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        first, second = edge.vertices
        adjacency[first].add(second)
        adjacency[second].add(first)
    seen: set[int] = set()
    components: list[list[int]] = []
    for start in range(len(mesh.vertices)):
        if start in seen:
            continue
        stack = [start]
        seen.add(start)
        component: list[int] = []
        while stack:
            vertex = stack.pop()
            component.append(vertex)
            for neighbor in adjacency[vertex]:
                if neighbor not in seen:
                    seen.add(neighbor)
                    stack.append(neighbor)
        components.append(component)
    return sorted(components, key=len, reverse=True)


def mesh_record(obj: bpy.types.Object) -> dict[str, Any]:
    mesh = obj.data
    mesh.calc_loop_triangles()
    components = connected_components(mesh)
    world_vertices = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    edge_face_counts = [0] * len(mesh.edges)
    edge_lookup = {
        tuple(sorted(edge.vertices)): index for index, edge in enumerate(mesh.edges)
    }
    for polygon in mesh.polygons:
        vertices = list(polygon.vertices)
        for index, first in enumerate(vertices):
            second = vertices[(index + 1) % len(vertices)]
            edge_face_counts[edge_lookup[tuple(sorted((first, second)))]] += 1
    component_records = []
    for component in components[:30]:
        points = [world_vertices[index] for index in component]
        component_records.append(
            {
                "vertices": len(component),
                "minimum": [min(point[axis] for point in points) for axis in range(3)],
                "maximum": [max(point[axis] for point in points) for axis in range(3)],
                "centroid": [
                    sum(point[axis] for point in points) / len(points)
                    for axis in range(3)
                ],
            }
        )
    return {
        "name": obj.name,
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "polygons": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "materials": [material.name if material else None for material in mesh.materials],
        "materialPolygonCounts": {
            str(index): sum(polygon.material_index == index for polygon in mesh.polygons)
            for index in range(max(1, len(mesh.materials)))
        },
        "uvLayers": [layer.name for layer in mesh.uv_layers],
        "componentCount": len(components),
        "largestComponentVertices": [len(component) for component in components[:30]],
        "components": component_records,
        "boundaryEdgeCount": sum(count == 1 for count in edge_face_counts),
        "nonManifoldEdgeCount": sum(count != 2 for count in edge_face_counts),
        "vertexGroupCount": len(obj.vertex_groups),
        "armatureModifiers": [
            modifier.object.name if modifier.object else None
            for modifier in obj.modifiers
            if modifier.type == "ARMATURE"
        ],
        "worldBounds": {
            "minimum": [min(vertex[axis] for vertex in world_vertices) for axis in range(3)],
            "maximum": [max(vertex[axis] for vertex in world_vertices) for axis in range(3)],
        },
        "matrixWorld": [list(row) for row in obj.matrix_world],
        "axisSamples": {
            axis: {
                "minimum": min(vertex[index] for vertex in world_vertices),
                "maximum": max(vertex[index] for vertex in world_vertices),
            }
            for index, axis in enumerate(("x", "y", "z"))
        },
    }


def load_source() -> list[bpy.types.Object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source = REPOSITORY_ROOT / SOURCE_RELATIVE
    result = bpy.ops.wm.fbx_import(filepath=str(source), use_anim=True)
    if "FINISHED" not in result:
        raise RuntimeError(f"FBX import failed: {source}")
    return sorted(bpy.context.scene.objects, key=lambda item: item.name.encode("utf-8"))


def audit_source() -> dict[str, Any]:
    objects = load_source()
    return {
        "blenderVersion": bpy.app.version_string,
        "source": SOURCE_RELATIVE.as_posix(),
        "objects": [
            mesh_record(obj)
            if obj.type == "MESH"
            else {
                "name": obj.name,
                "type": obj.type,
                "parent": obj.parent.name if obj.parent else None,
            }
            for obj in objects
        ],
        "images": [
            {"name": image.name, "filepath": image.filepath}
            for image in sorted(bpy.data.images, key=lambda item: item.name.encode("utf-8"))
        ],
        "actions": [
            action.name
            for action in sorted(bpy.data.actions, key=lambda item: item.name.encode("utf-8"))
        ],
    }


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def file_record(path: Path) -> dict[str, Any]:
    return {
        "path": path.relative_to(REPOSITORY_ROOT).as_posix(),
        "bytes": path.stat().st_size,
        "sha256": sha256_file(path),
    }


def only_source_mesh() -> bpy.types.Object:
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"expected one source mesh, found {len(meshes)}")
    return meshes[0]


def clean_and_scale_mesh(obj: bpy.types.Object) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.scale = (6.0, 6.0, 6.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    remaining = set(bm.verts)
    components: list[list[bmesh.types.BMVert]] = []
    while remaining:
        start = next(iter(remaining))
        remaining.remove(start)
        stack = [start]
        component = []
        while stack:
            vertex = stack.pop()
            component.append(vertex)
            for edge in vertex.link_edges:
                neighbor = edge.other_vert(vertex)
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    stack.append(neighbor)
        components.append(component)
    discard = [vertex for component in components if len(component) < 100 for vertex in component]
    if discard:
        bmesh.ops.delete(bm, geom=discard, context="VERTS")
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00001)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=0.000001)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    degenerate_faces = [face for face in bm.faces if face.calc_area() <= 1.0e-10]
    if degenerate_faces:
        bmesh.ops.delete(
            bm,
            geom=degenerate_faces,
            context="FACES_KEEP_BOUNDARY",
        )
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=0.000001)
    boundary_edges = [edge for edge in bm.edges if len(edge.link_faces) == 1]
    if boundary_edges:
        bmesh.ops.holes_fill(bm, edges=boundary_edges)

    # Meshy occasionally leaves overlapping micro-faces around whisker/crown
    # seams. Keep the two largest incident faces per overloaded edge, remove
    # only the smaller extras, then close the resulting local boundaries.
    for _ in range(8):
        overloaded = [edge for edge in bm.edges if len(edge.link_faces) > 2]
        if not overloaded:
            break
        extras = {
            face
            for edge in overloaded
            for face in sorted(edge.link_faces, key=lambda item: item.calc_area())[:-2]
        }
        if not extras:
            break
        bmesh.ops.delete(bm, geom=list(extras), context="FACES_KEEP_BOUNDARY")
    boundary_edges = [edge for edge in bm.edges if len(edge.link_faces) == 1]
    if boundary_edges:
        bmesh.ops.holes_fill(bm, edges=boundary_edges)
    for _ in range(64):
        boundary_edges = [edge for edge in bm.edges if len(edge.link_faces) == 1]
        if boundary_edges:
            bmesh.ops.collapse(bm, edges=boundary_edges, uvs=True)
        overloaded = [edge for edge in bm.edges if len(edge.link_faces) > 2]
        if overloaded:
            extras = {
                face
                for edge in overloaded
                for face in sorted(edge.link_faces, key=lambda item: item.calc_area())[:-2]
            }
            if extras:
                bmesh.ops.delete(bm, geom=list(extras), context="FACES_KEEP_BOUNDARY")
        if not [edge for edge in bm.edges if len(edge.link_faces) != 2]:
            break
    isolated_vertices = [vertex for vertex in bm.verts if not vertex.link_edges]
    if isolated_vertices:
        bmesh.ops.delete(bm, geom=isolated_vertices, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    obj.name = "Vaeloryn_SkinnedBody"
    mesh.name = "Vaeloryn_SkinnedBody_Mesh"


def semantic_regions(obj: bpy.types.Object) -> dict[str, list[int]]:
    regions: dict[str, list[int]] = {name: [] for name in SEMANTIC_REGIONS}
    vertices = list(obj.data.vertices)
    for vertex in vertices:
        x, y, z = vertex.co
        regions["body"].append(vertex.index)
        if y > 3.8:
            regions["head"].append(vertex.index)
        if y > 4.35 and z < 0.15:
            regions["jaw"].append(vertex.index)
        if y < -1.5 and z < 1.2:
            regions["tail"].append(vertex.index)
        if z > 0.55 and y < 3.2:
            side = "l" if x >= 0.0 else "r"
            regions[f"wing_membrane_{side}"].append(vertex.index)
            if abs(x) > 0.34 or z > 4.5:
                regions[f"wing_arm_{side}"].append(vertex.index)

    for side, comparison in (("l", lambda x: x >= 0.0), ("r", lambda x: x < 0.0)):
        eye_candidates = sorted(
            (vertex for vertex in vertices if comparison(vertex.co.x)),
            key=lambda vertex: (vertex.co.y, -abs(vertex.co.z)),
            reverse=True,
        )[:24]
        regions[f"eye_{side}"] = [vertex.index for vertex in eye_candidates]

    empty = [name for name, indices in regions.items() if not indices]
    if empty:
        raise RuntimeError(f"semantic region classification produced empty regions: {empty}")
    for name, indices in regions.items():
        group = obj.vertex_groups.new(name=f"semantic_{name}")
        group.add(indices, 1.0, "REPLACE")
    obj["al_semantic_regions"] = json.dumps(SEMANTIC_REGIONS)
    obj["al_source_identity"] = "NPC_VAELORYN"
    obj["al_shot_binding"] = "CTMA-BEAT-07/Shot070/[1080,1248)"
    return regions


def image_node(
    nodes: bpy.types.Nodes,
    path: Path,
    non_color: bool,
) -> bpy.types.Node:
    image = bpy.data.images.load(str(path), check_existing=True)
    if non_color:
        image.colorspace_settings.name = "Non-Color"
    node = nodes.new("ShaderNodeTexImage")
    node.image = image
    return node


def source_material(name: str) -> bpy.types.Material:
    texture_root = REPOSITORY_ROOT / TEXTURE_ROOT_RELATIVE
    material = bpy.data.materials.new(name)
    material.diffuse_color = (0.08, 0.18, 0.25, 1.0)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    base = image_node(nodes, texture_root / "base_color.png", False)
    metallic = image_node(nodes, texture_root / "metallic.png", True)
    roughness = image_node(nodes, texture_root / "roughness.png", True)
    normal = image_node(nodes, texture_root / "normal.png", True)
    normal_map = nodes.new("ShaderNodeNormalMap")
    links.new(base.outputs["Color"], shader.inputs["Base Color"])
    links.new(metallic.outputs["Color"], shader.inputs["Metallic"])
    links.new(roughness.outputs["Color"], shader.inputs["Roughness"])
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def flat_material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    if shader is None:
        raise RuntimeError("Principled BSDF unavailable")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    if "Emission Color" in shader.inputs:
        shader.inputs["Emission Color"].default_value = color
        shader.inputs["Emission Strength"].default_value = emission_strength
    return material


def assign_material_regions(obj: bpy.types.Object) -> list[str]:
    materials = [
        source_material("body"),
        flat_material("celestial_membrane", (0.19, 0.25, 0.56, 1.0), 0.15, 0.22, 0.18),
        flat_material("crown_thorn", (0.58, 0.53, 0.70, 1.0), 0.68, 0.24, 0.08),
        flat_material("eyes", (0.32, 0.78, 1.0, 1.0), 0.05, 0.16, 3.0),
    ]
    obj.data.materials.clear()
    for material in materials:
        obj.data.materials.append(material)
    eye_polygons = sorted(
        obj.data.polygons,
        key=lambda polygon: sum(obj.data.vertices[index].co.y for index in polygon.vertices)
        / len(polygon.vertices),
        reverse=True,
    )[:32]
    eye_indices = {polygon.index for polygon in eye_polygons}
    for polygon in obj.data.polygons:
        center = sum((obj.data.vertices[index].co for index in polygon.vertices), Vector()) / len(
            polygon.vertices
        )
        if polygon.index in eye_indices:
            polygon.material_index = 3
        elif center.y > 4.15:
            polygon.material_index = 2
        elif center.z > 0.55 and center.y < 3.2:
            polygon.material_index = 1
        else:
            polygon.material_index = 0
    return [material.name for material in materials]


BONE_SPECS = (
    ("root", None, (0.0, 0.0, -5.5), (0.0, 0.0, -4.5), False),
    ("pelvis", "root", (0.0, -1.0, -0.6), (0.0, 0.0, -0.2), True),
    ("spine_01", "pelvis", (0.0, -0.1, -0.2), (0.0, 1.2, 0.0), True),
    ("spine_02", "spine_01", (0.0, 1.2, 0.0), (0.0, 2.5, 0.1), True),
    ("neck_01", "spine_02", (0.0, 2.4, 0.1), (0.0, 3.6, 0.15), True),
    ("neck_02", "neck_01", (0.0, 3.5, 0.15), (0.0, 4.6, 0.12), True),
    ("head", "neck_02", (0.0, 4.5, 0.12), (0.0, 5.65, 0.05), True),
    ("jaw", "head", (0.0, 4.65, -0.08), (0.0, 5.55, -0.28), True),
    ("tail_01", "pelvis", (0.0, -0.8, -0.35), (0.0, -2.1, -0.45), True),
    ("tail_02", "tail_01", (0.0, -2.0, -0.45), (0.0, -3.35, -0.55), True),
    ("tail_03", "tail_02", (0.0, -3.25, -0.55), (0.0, -4.55, -0.62), True),
    ("tail_04", "tail_03", (0.0, -4.45, -0.62), (0.0, -5.7, -0.7), True),
    ("wing_l_01", "spine_01", (0.3, 0.3, 0.45), (0.55, 0.0, 2.3), True),
    ("wing_l_02", "wing_l_01", (0.55, 0.0, 2.3), (0.72, -0.35, 4.2), True),
    ("wing_l_03", "wing_l_02", (0.72, -0.35, 4.2), (0.82, -0.7, 5.65), True),
    ("wing_r_01", "spine_01", (-0.3, 0.3, 0.45), (-0.55, 0.0, 2.3), True),
    ("wing_r_02", "wing_r_01", (-0.55, 0.0, 2.3), (-0.72, -0.35, 4.2), True),
    ("wing_r_03", "wing_r_02", (-0.72, -0.35, 4.2), (-0.82, -0.7, 5.65), True),
    ("leg_fl_01", "spine_01", (0.34, 0.95, -0.25), (0.45, 0.9, -2.6), True),
    ("leg_fl_02", "leg_fl_01", (0.45, 0.9, -2.6), (0.48, 1.05, -5.25), True),
    ("leg_fr_01", "spine_01", (-0.34, 0.95, -0.25), (-0.45, 0.9, -2.6), True),
    ("leg_fr_02", "leg_fr_01", (-0.45, 0.9, -2.6), (-0.48, 1.05, -5.25), True),
    ("leg_bl_01", "pelvis", (0.4, -0.7, -0.35), (0.52, -0.75, -2.7), True),
    ("leg_bl_02", "leg_bl_01", (0.52, -0.75, -2.7), (0.55, -0.9, -5.25), True),
    ("leg_br_01", "pelvis", (-0.4, -0.7, -0.35), (-0.52, -0.75, -2.7), True),
    ("leg_br_02", "leg_br_01", (-0.52, -0.75, -2.7), (-0.55, -0.9, -5.25), True),
)


def build_armature() -> bpy.types.Object:
    data = bpy.data.armatures.new("Vaeloryn_Rig")
    armature = bpy.data.objects.new("Vaeloryn_Rig", data)
    bpy.context.scene.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    created = {}
    for name, parent_name, head, tail, deform in BONE_SPECS:
        bone = data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        bone.use_deform = deform
        if parent_name:
            bone.parent = created[parent_name]
        created[name] = bone
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    armature["al_rig_family"] = "wish_dragon_nonhumanoid_v001"
    return armature


def primary_bone(vertex: Vector) -> str:
    x, y, z = vertex
    if y > 4.35 and z < 0.15:
        return "jaw"
    if y > 4.05:
        return "head"
    if y > 3.15:
        return "neck_02"
    if y > 2.0:
        return "neck_01"
    if y < -4.4:
        return "tail_04"
    if y < -3.2:
        return "tail_03"
    if y < -2.0:
        return "tail_02"
    if y < -1.15 and z < 1.2:
        return "tail_01"
    if z > 0.55 and y < 3.2:
        side = "l" if x >= 0.0 else "r"
        segment = "03" if z > 4.1 else "02" if z > 2.25 else "01"
        return f"wing_{side}_{segment}"
    if z < -0.55 and -1.6 < y < 1.8:
        front = y >= 0.2
        side = "l" if x >= 0.0 else "r"
        prefix = f"leg_f{side}" if front else f"leg_b{side}"
        segment = "02" if z < -2.55 else "01"
        return f"{prefix}_{segment}"
    if y > 0.75:
        return "spine_02"
    if y > -0.4:
        return "spine_01"
    return "pelvis"


def skin_mesh(obj: bpy.types.Object, armature: bpy.types.Object) -> dict[str, int]:
    parent = {name: parent_name for name, parent_name, *_ in BONE_SPECS}
    deform_names = [name for name, _, _, _, deform in BONE_SPECS if deform]
    groups = {name: obj.vertex_groups.new(name=name) for name in deform_names}
    max_influences = 0
    for vertex in obj.data.vertices:
        bone = primary_bone(vertex.co)
        parent_bone = parent[bone]
        if parent_bone in groups:
            groups[bone].add([vertex.index], 0.85, "REPLACE")
            groups[parent_bone].add([vertex.index], 0.15, "REPLACE")
            max_influences = max(max_influences, 2)
        else:
            groups[bone].add([vertex.index], 1.0, "REPLACE")
            max_influences = max(max_influences, 1)
    modifier = obj.modifiers.new("Vaeloryn_Armature", "ARMATURE")
    modifier.object = armature
    return {
        "maxVertexInfluences": max_influences,
        "unweightedVertexCount": 0,
        "deformBoneCount": len(deform_names),
    }


def animate_rig(armature: bpy.types.Object) -> bpy.types.Action:
    action = bpy.data.actions.new("Shot070_Vaeloryn_Articulation_v002")
    armature.animation_data_create()
    armature.animation_data.action = action
    frames = (1, 43, 85, 127, 168)
    phases = (0.0, 1.0, 0.0, -1.0, 0.0)
    animated = ("neck_01", "jaw", "wing_l_01", "wing_r_01", "tail_01")
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "XYZ"
    for frame, phase in zip(frames, phases):
        armature.location = (0.0, 0.0, 0.25 * abs(phase))
        armature.keyframe_insert(data_path="location", frame=frame)
        armature.pose.bones["neck_01"].rotation_euler[2] = 0.18 * phase
        armature.pose.bones["jaw"].rotation_euler[0] = 0.28 * max(phase, 0.0)
        armature.pose.bones["wing_l_01"].rotation_euler[1] = 0.45 * phase
        armature.pose.bones["wing_r_01"].rotation_euler[1] = -0.45 * phase
        armature.pose.bones["tail_01"].rotation_euler[2] = -0.35 * phase
        for name in animated:
            armature.pose.bones[name].keyframe_insert(
                data_path="rotation_euler", frame=frame
            )
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 168
    bpy.context.scene.render.fps = 24
    return action


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_area_light(
    name: str,
    location: tuple[float, float, float],
    energy: float,
    size: float,
    color: tuple[float, float, float],
) -> None:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    light = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(light)
    light.location = location
    look_at(light, Vector((0.0, 0.0, 0.0)))


def create_review_stage() -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Vaeloryn_ReviewWorld")
    scene.world.color = (0.003, 0.004, 0.012)
    if scene.world.use_nodes:
        background = scene.world.node_tree.nodes.get("Background")
        if background:
            background.inputs["Color"].default_value = (0.003, 0.006, 0.025, 1.0)
            background.inputs["Strength"].default_value = 0.16

    bpy.ops.mesh.primitive_plane_add(size=36.0, location=(0.0, 0.0, -5.72))
    floor = bpy.context.object
    floor.name = "ReviewStage_Ground"
    floor.data.materials.append(
        flat_material("review_ground", (0.012, 0.018, 0.042, 1.0), 0.2, 0.62)
    )
    add_area_light("Key_L", (8.0, -8.0, 12.0), 1700.0, 7.0, (0.68, 0.76, 1.0))
    add_area_light("Fill_R", (-7.0, -3.0, 7.0), 1100.0, 6.0, (0.52, 0.36, 1.0))
    add_area_light("Rim_Back", (0.0, 9.0, 10.0), 2100.0, 5.0, (0.35, 0.70, 1.0))

    target = bpy.data.objects.new("Shot070_CameraTarget", None)
    bpy.context.scene.collection.objects.link(target)
    target.location = (0.0, 0.0, 0.0)
    camera_data = bpy.data.cameras.new("Shot070_ReviewCamera")
    camera_data.lens = 52.0
    camera = bpy.data.objects.new("Shot070_ReviewCamera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    constraint = camera.constraints.new("TRACK_TO")
    constraint.target = target
    constraint.track_axis = "TRACK_NEGATIVE_Z"
    constraint.up_axis = "UP_Y"
    scene.camera = camera
    camera.location = (20.0, -29.0, 10.5)
    camera.keyframe_insert(data_path="location", frame=1)
    camera.location = (18.0, -26.5, 11.5)
    camera.keyframe_insert(data_path="location", frame=168)
    return camera


def render_still(
    camera: bpy.types.Object,
    relative: Path,
    width: int,
    height: int,
    location: tuple[float, float, float],
) -> None:
    scene = bpy.context.scene
    output = REPOSITORY_ROOT / relative
    output.parent.mkdir(parents=True, exist_ok=True)
    scene.frame_set(85)
    camera.location = location
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    if not output.is_file():
        raise RuntimeError(f"render did not produce {output}")


def render_motion(camera: bpy.types.Object) -> dict[str, Any]:
    scene = bpy.context.scene
    motion = REPOSITORY_ROOT / MOTION_RELATIVE
    contact = REPOSITORY_ROOT / CONTACT_RELATIVE
    frames = REPOSITORY_ROOT / ".hermes" / "shot070_vaeloryn_motion_frames_v002"
    motion.parent.mkdir(parents=True, exist_ok=True)
    fingerprint = hashlib.sha256(
        (REPOSITORY_ROOT / SOURCE_RELATIVE).read_bytes()
        + b"shot070-vaeloryn-v002-triangulated-rig-v3"
    ).hexdigest()
    fingerprint_path = frames / "build_fingerprint.txt"
    if (
        fingerprint_path.is_file()
        and fingerprint_path.read_text(encoding="utf-8").strip() != fingerprint
    ):
        shutil.rmtree(frames)
    frames.mkdir(parents=True, exist_ok=True)
    fingerprint_path.write_text(fingerprint + "\n", encoding="utf-8")
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (0.45, 0.60, 0.80)
    scene.display.shading.background_type = "VIEWPORT"
    scene.display.shading.background_color = (0.01, 0.015, 0.04)
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.frame_set(1)
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    for frame in range(1, 169):
        frame_path = frames / f"frame_{frame:04d}.png"
        if frame_path.is_file() and frame_path.stat().st_size > 1024:
            continue
        scene.frame_set(frame)
        scene.render.filepath = str(frame_path)
        bpy.ops.render.render(write_still=True)
    subprocess.run(
        [
            "ffmpeg",
            "-y",
            "-v",
            "error",
            "-framerate",
            "24",
            "-start_number",
            "1",
            "-i",
            str(frames / "frame_%04d.png"),
            "-frames:v",
            "168",
            "-c:v",
            "libx264",
            "-pix_fmt",
            "yuv420p",
            "-movflags",
            "+faststart",
            str(motion),
        ],
        check=True,
    )
    if not motion.is_file():
        raise RuntimeError(f"animation render did not produce {motion}")

    select_filter = (
        "select=eq(n\\,0)+eq(n\\,41)+eq(n\\,83)+eq(n\\,125)+eq(n\\,167),"
        "scale=384:216,tile=5x1"
    )
    subprocess.run(
        [
            "ffmpeg",
            "-y",
            "-v",
            "error",
            "-i",
            str(motion),
            "-vf",
            select_filter,
            "-frames:v",
            "1",
            str(contact),
        ],
        check=True,
    )
    probe = subprocess.run(
        [
            "ffprobe",
            "-v",
            "error",
            "-count_frames",
            "-select_streams",
            "v:0",
            "-show_entries",
            "stream=codec_name,width,height,r_frame_rate,nb_read_frames,duration",
            "-of",
            "json",
            str(motion),
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    stream = json.loads(probe.stdout)["streams"][0]
    if (
        stream.get("codec_name") != "h264"
        or int(stream.get("width", 0)) != 960
        or int(stream.get("height", 0)) != 540
        or stream.get("r_frame_rate") != "24/1"
        or int(stream.get("nb_read_frames", 0)) != 168
    ):
        raise RuntimeError(f"motion probe mismatch: {stream}")
    shutil.rmtree(frames)
    return stream


def export_glb(obj: bpy.types.Object, armature: bpy.types.Object) -> None:
    output = REPOSITORY_ROOT / GLB_RELATIVE
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    result = bpy.ops.export_scene.gltf(
        filepath=str(output),
        check_existing=False,
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_force_sampling=True,
        export_frame_range=True,
        export_skins=True,
    )
    if "FINISHED" not in result or not output.is_file():
        raise RuntimeError(f"GLB export failed: {output}")


def build_manifest(
    raw_audit: dict[str, Any],
    candidate_audit: dict[str, Any],
    rig_metrics: dict[str, int],
    regions: dict[str, list[int]],
    materials: list[str],
    motion_probe: dict[str, Any],
) -> dict[str, Any]:
    blend = REPOSITORY_ROOT / BLEND_RELATIVE
    glb = REPOSITORY_ROOT / GLB_RELATIVE
    rig_report = REPOSITORY_ROOT / RIG_REPORT_RELATIVE
    motion = REPOSITORY_ROOT / MOTION_RELATIVE
    contact = REPOSITORY_ROOT / CONTACT_RELATIVE
    landscape = REPOSITORY_ROOT / LANDSCAPE_RELATIVE
    portrait = REPOSITORY_ROOT / PORTRAIT_RELATIVE
    return {
        "schemaVersion": 1,
        "packetId": "tdf_packet_vaeloryn_wish_dragon_shot070_source_v002",
        "sourceVersion": "tdf-cinematic-vaeloryn-2026-09-04-v002",
        "shotBinding": {
            "beatId": "CTMA-BEAT-07",
            "shotId": "Shot070",
            "frameInterval": [1080, 1248],
            "localFrameCount": 168,
            "fps": 24,
            "durationSeconds": 7.0,
        },
        "authority": {
            "status": "MOTION_REVIEW_CANDIDATE",
            "runtimeAuthority": False,
            "gameplayAuthority": False,
            "finalCinematicApproval": False,
            "ownerVisualApprovalRequired": True,
            "runtimeVfxSeparate": True,
        },
        "cost": {
            "incrementalUsd": 0.0,
            "paidProviderCalls": 0,
            "rechargeOrBillingMutation": False,
            "tools": ["Blender 5.2.0 LTS local", "FFmpeg 9.0 local"],
        },
        "historicalExactIdentityReference": {
            "basename": "exec-b9b4004f-1925-44b0-9032-d8e26d429113.png",
            "bytes": 2572091,
            "sha256": "0484da2fc76779601143ddac305247c503b19dc90398c35febea2c2cec3e1c97",
            "localBytesAvailable": False,
            "disposition": "APPROVED_2D_SOURCE_BYTES_NOT_RECOVERED_LOCALLY",
        },
        "approved2DSources": [
            {
                "path": "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001/ConceptSheets/55_vaeloryn_multiview_01_v001.png",
                "sha256": "b3453b1e23b6ab911fe33fb0820c05e2f9b5d9db0e34ef89875edad83a8f8b55",
                "authority": "APPROVED_2D",
            },
            {
                "path": "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001/ConceptSheets/56_vaeloryn_multiview_02_v001.png",
                "sha256": "ccdb03cd2e4bc2547e95497e251bbd698d52b4c19a1b179b0707993709bd897d",
                "authority": "APPROVED_2D",
            },
        ],
        "candidateDerivation": {
            "input": {
                "path": SOURCE_RELATIVE.as_posix(),
                "sha256": "80bcc74a2cf95cb2626437bba3d3ba805d6087f1498e64b1603cb256f43e68cb",
                "meshyTaskId": "01a05b2c-92c6-7329-939f-a538fdaa859b",
            },
            "operations": [
                "local Blender removal of the 15-vertex disconnected debris component and deterministic hole cleanup",
                "local semantic separation into ten named anatomy regions and four independent material regions",
                "local 25-deform-bone armature, bounded two-influence skinning, and five-bone articulation review action",
            ],
            "candidateFiles": [file_record(blend), file_record(glb)],
        },
        "rejectedSource": {
            "basename": "wish_dragon_review_master.glb",
            "bytes": 53457548,
            "triangles": 777338,
            "sha256": "5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270",
            "inputEligible": False,
            "usedAsInput": False,
            "disposition": "REJECTED_FOR_EXACT_SOURCE_FIDELITY",
            "negativeChecks": [
                "duplicate_head",
                "fused_monolithic_mesh",
                "unskinned",
                "single_material",
                "identity_wing_emission_drift",
                "lineage_gap",
            ],
        },
        "sourceAudit": {
            "file": file_record(REPOSITORY_ROOT / AUDIT_RELATIVE),
            "sourceTriangles": raw_audit["objects"][0]["triangles"],
            "sourceMeshObjects": 1,
            "sourceMaterials": 1,
            "sourceArmatures": 0,
            "sourceNonManifoldEdges": raw_audit["objects"][0]["nonManifoldEdgeCount"],
        },
        "anatomy": {
            "headCount": 1,
            "legCount": 4,
            "wingPairCount": 1,
            "tailCount": 1,
            "semanticRegions": list(SEMANTIC_REGIONS),
            "semanticRegionCount": len(SEMANTIC_REGIONS),
            "semanticVertexCounts": {name: len(indices) for name, indices in regions.items()},
        },
        "sourceFidelity": {
            "preserved": [
                "single crowned head",
                "four legs",
                "one independently controlled wing pair",
                "long articulated tail",
                "pearl argent pale-indigo hierarchy",
                "celestial membrane material region",
                "crown and thorn material region",
            ],
            "separateFromCleanMesh": [
                "eight Gems",
                "portal",
                "wish-space",
                "atmospheric magic",
                "attack effects",
                "audio",
            ],
            "remainingDecision": "Owner visual approval of this moving 3D candidate and final Shot070 performance remains required.",
        },
        "topology": {
            "triangles": candidate_audit["triangles"],
            "vertices": candidate_audit["vertices"],
            "meshObjectCount": 1,
            "materialSlots": materials,
            "independentMaterialRegionCount": len(materials),
            "uvLayerCount": len(candidate_audit["uvLayers"]),
            "nonManifoldEdgeCount": candidate_audit["nonManifoldEdgeCount"],
            "boundaryEdgeCount": candidate_audit["boundaryEdgeCount"],
        },
        "scale": {
            "purpose": "stable cinematic review scale only; no gameplay/runtime scale authority",
            "boundsMeters": candidate_audit["worldBounds"],
            "maximumHeightMeters": round(
                candidate_audit["worldBounds"]["maximum"][2]
                - candidate_audit["worldBounds"]["minimum"][2],
                6,
            ),
        },
        "rig": {
            "rigged": True,
            "armatureCount": 1,
            "deformBoneCount": rig_metrics["deformBoneCount"],
            "maxVertexInfluences": rig_metrics["maxVertexInfluences"],
            "unweightedVertexCount": rig_metrics["unweightedVertexCount"],
            "requiredBones": [name for name, *_ in BONE_SPECS],
            "report": file_record(rig_report),
        },
        "motionProof": {
            "file": file_record(motion),
            "contactSheet": file_record(contact),
            "codec": motion_probe["codec_name"],
            "width": int(motion_probe["width"]),
            "height": int(motion_probe["height"]),
            "fps": 24,
            "frameCount": int(motion_probe["nb_read_frames"]),
            "durationSeconds": 7.0,
            "animatedBones": ["neck_01", "jaw", "wing_l_01", "wing_r_01", "tail_01"],
            "genuineArticulation": True,
            "stillImageMotionSubstitute": False,
        },
        "framingProofs": [
            {
                "aspect": "16:9",
                "width": 1920,
                "height": 1080,
                "file": file_record(landscape),
            },
            {
                "aspect": "9:16",
                "width": 1080,
                "height": 1920,
                "file": file_record(portrait),
            },
        ],
    }


def build_candidate() -> dict[str, Any]:
    for relative in (
        ART_ROOT_RELATIVE,
        DOC_ROOT_RELATIVE,
    ):
        (REPOSITORY_ROOT / relative).mkdir(parents=True, exist_ok=True)
    raw_audit = audit_source()
    write_json(REPOSITORY_ROOT / AUDIT_RELATIVE, raw_audit)
    obj = only_source_mesh()
    clean_and_scale_mesh(obj)
    regions = semantic_regions(obj)
    materials = assign_material_regions(obj)
    armature = build_armature()
    rig_metrics = skin_mesh(obj, armature)
    action = animate_rig(armature)
    camera = create_review_stage()
    candidate_audit = mesh_record(obj)
    if (
        candidate_audit["nonManifoldEdgeCount"] > 4
        or candidate_audit["boundaryEdgeCount"] != 0
    ):
        raise RuntimeError(
            "candidate mesh is not manifold after cleanup: "
            f"{candidate_audit['nonManifoldEdgeCount']} edges "
            f"({candidate_audit['boundaryEdgeCount']} boundary, "
            f"{candidate_audit['nonManifoldEdgeCount'] - candidate_audit['boundaryEdgeCount']} overloaded)"
        )
    rig_report = {
        "schemaVersion": 1,
        "status": "PASS",
        "blenderVersion": bpy.app.version_string,
        "source": raw_audit,
        "candidate": candidate_audit,
        "semanticRegionVertexCounts": {
            name: len(indices) for name, indices in regions.items()
        },
        "materialSlots": materials,
        "armature": {
            "name": armature.name,
            "bones": [name for name, *_ in BONE_SPECS],
            **rig_metrics,
        },
        "action": {
            "name": action.name,
            "frameStart": 1,
            "frameEnd": 168,
            "fps": 24,
            "animatedBones": ["neck_01", "jaw", "wing_l_01", "wing_r_01", "tail_01"],
        },
        "authority": {
            "runtime": False,
            "gameplay": False,
            "finalCinematic": False,
            "ownerVisualApprovalRequired": True,
        },
    }
    write_json(REPOSITORY_ROOT / RIG_REPORT_RELATIVE, rig_report)

    blend = REPOSITORY_ROOT / BLEND_RELATIVE
    blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    export_glb(obj, armature)
    render_still(camera, LANDSCAPE_RELATIVE, 1920, 1080, (23.0, -33.0, 11.5))
    render_still(camera, PORTRAIT_RELATIVE, 1080, 1920, (18.0, -26.0, 8.5))
    motion_probe = render_motion(camera)
    manifest = build_manifest(
        raw_audit,
        candidate_audit,
        rig_metrics,
        regions,
        materials,
        motion_probe,
    )
    write_json(REPOSITORY_ROOT / MANIFEST_RELATIVE, manifest)
    return manifest


def verify_export() -> dict[str, Any]:
    output = REPOSITORY_ROOT / GLB_RELATIVE
    if not output.is_file():
        raise RuntimeError(f"candidate GLB is missing: {output}")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(output))
    if "FINISHED" not in result:
        raise RuntimeError("candidate GLB round-trip import failed")
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes.sort(key=lambda item: len(item.data.vertices), reverse=True)
    if (
        not meshes
        or len(armatures) != 1
        or any(len(item.data.vertices) > 64 for item in meshes[1:])
    ):
        raise RuntimeError(
            f"candidate round-trip object mismatch: meshes={len(meshes)} armatures={len(armatures)}"
        )
    main_mesh = meshes[0]
    materials = [
        slot.material.name for slot in main_mesh.material_slots if slot.material is not None
    ]
    if sorted(materials) != sorted(
        ["body", "celestial_membrane", "crown_thorn", "eyes"]
    ):
        raise RuntimeError(f"candidate GLB material mismatch: {materials}")
    main_mesh.data.calc_loop_triangles()
    export_mesh = {
        "name": main_mesh.name,
        "vertices": len(main_mesh.data.vertices),
        "triangles": len(main_mesh.data.loop_triangles),
        "materials": materials,
        "uvLayerCount": len(main_mesh.data.uv_layers),
        "armatureModifiers": [
            modifier.object.name if modifier.object else None
            for modifier in main_mesh.modifiers
            if modifier.type == "ARMATURE"
        ],
    }
    report = {
        "status": "PASS",
        "file": file_record(output),
        "mesh": export_mesh,
        "ignoredExporterHelpers": [
            {"name": item.name, "vertices": len(item.data.vertices)} for item in meshes[1:]
        ],
        "topologyAuthority": BLEND_RELATIVE.as_posix(),
        "roundTripTopologyAuthority": False,
        "armatureCount": len(armatures),
        "bones": sorted(bone.name for bone in armatures[0].data.bones),
        "actions": sorted(action.name for action in bpy.data.actions),
    }
    print("VAELORYN_EXPORT_VERIFY=" + json.dumps(report, sort_keys=True))
    return report


def main() -> int:
    args = parse_arguments()
    if args.command == "audit":
        print("VAELORYN_SOURCE_AUDIT=" + json.dumps(audit_source(), sort_keys=True))
        return 0
    if args.command == "build":
        manifest = build_candidate()
        print("VAELORYN_BUILD=" + json.dumps(manifest, sort_keys=True))
        return 0
    verify_export()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
