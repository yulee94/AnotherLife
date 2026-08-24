import json
import math
import os
import sys
from pathlib import Path

import bpy


ARGS = sys.argv[sys.argv.index("--") + 1 :]
if len(ARGS) != 5:
    raise SystemExit(
        "usage: blender --background --python script.py -- "
        "input.fbx texture_dir output.blend output.fbx report.json"
    )

input_fbx, texture_dir, output_blend, output_fbx, report_path = map(Path, ARGS)
for path in (output_blend, output_fbx, report_path):
    path.parent.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(input_fbx))
mesh_object = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
mesh = mesh_object.data
active_action = (
    armature.animation_data.action
    if armature.animation_data is not None
    else None
)
if active_action is not None:
    active_action.name = "ChampionWalk"
mesh_object.name = "ChampionBase"
mesh.name = "ChampionBaseMesh"

images = {}
for name in ("base_color", "normal", "metallic", "roughness", "emission"):
    image = bpy.data.images.load(str(texture_dir / f"{name}.png"), check_existing=False)
    image.name = f"Champion_{name}"
    if name != "base_color":
        image.colorspace_settings.name = "Non-Color"
    images[name] = image


def socket(name, bone, location=(0.0, 0.0, 0.0)):
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_type = "PLAIN_AXES"
    empty.empty_display_size = 0.05
    empty.parent = armature
    empty.parent_type = "BONE"
    empty.parent_bone = bone
    empty.location = location
    bpy.context.collection.objects.link(empty)
    return empty


socket("Socket_WeaponMain", "RightHand")
socket("Socket_Offhand", "LeftHand")
socket("Socket_Head", "Head")
socket("Socket_Back", "Spine02", (0.0, -0.08, 0.0))


def shader_input(shader, *names):
    for name in names:
        value = shader.inputs.get(name)
        if value is not None:
            return value
    return None


def create_material(name):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (1.0, 1.0, 1.0, 1.0)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    base = nodes.new("ShaderNodeTexImage")
    base.image = images["base_color"]
    links.new(base.outputs["Color"], shader.inputs["Base Color"])

    metallic = nodes.new("ShaderNodeTexImage")
    metallic.image = images["metallic"]
    links.new(metallic.outputs["Color"], shader.inputs["Metallic"])

    roughness = nodes.new("ShaderNodeTexImage")
    roughness.image = images["roughness"]
    links.new(roughness.outputs["Color"], shader.inputs["Roughness"])

    normal_tex = nodes.new("ShaderNodeTexImage")
    normal_tex.image = images["normal"]
    normal = nodes.new("ShaderNodeNormalMap")
    links.new(normal_tex.outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs["Normal"], shader.inputs["Normal"])

    emission = nodes.new("ShaderNodeTexImage")
    emission.image = images["emission"]
    emission_input = shader_input(shader, "Emission Color", "Emission")
    if emission_input is not None:
        links.new(emission.outputs["Color"], emission_input)
    emission_strength = shader_input(shader, "Emission Strength")
    if emission_strength is not None:
        emission_strength.default_value = 0.25
    return material


material_names = [
    "M_Champion_Skin",
    "M_Champion_Hair",
    "M_Champion_Cloth",
    "M_Champion_Metal",
]
mesh.materials.clear()
for material_name in material_names:
    mesh.materials.append(create_material(material_name))

uv_layer = mesh.uv_layers.active
base_pixels = images["base_color"].pixels[:]
metal_pixels = images["metallic"].pixels[:]
base_width = images["base_color"].size[0]
base_height = images["base_color"].size[1]
metal_width = images["metallic"].size[0]
metal_height = images["metallic"].size[1]


def sample(pixels, width, height, uv):
    x = max(0, min(width - 1, int((uv.x % 1.0) * (width - 1))))
    y = max(0, min(height - 1, int((uv.y % 1.0) * (height - 1))))
    offset = (y * width + x) * 4
    return pixels[offset], pixels[offset + 1], pixels[offset + 2]


face_counts = {name: 0 for name in material_names}
for polygon in mesh.polygons:
    centers = [mesh.vertices[mesh.loops[index].vertex_index].co for index in polygon.loop_indices]
    center = sum(centers, centers[0] * 0.0) / len(centers)
    base_samples = [
        sample(base_pixels, base_width, base_height, uv_layer.data[index].uv)
        for index in polygon.loop_indices
    ]
    metal_samples = [
        sample(metal_pixels, metal_width, metal_height, uv_layer.data[index].uv)
        for index in polygon.loop_indices
    ]
    color = tuple(sum(value[channel] for value in base_samples) / len(base_samples) for channel in range(3))
    metallic = sum(sum(value) / 3.0 for value in metal_samples) / len(metal_samples)
    luminance = 0.2126 * color[0] + 0.7152 * color[1] + 0.0722 * color[2]
    skin_hue = color[0] > color[1] * 1.03 and color[1] > color[2] * 1.02
    head = center.z > 1.42 and abs(center.x) < 0.22
    hands = center.z > 0.82 and abs(center.x) > 0.55

    if (head or hands) and skin_hue and luminance > 0.18 and metallic < 0.28:
        slot = 0
    elif head and luminance < 0.24 and metallic < 0.32:
        slot = 1
    elif metallic > 0.32:
        slot = 3
    else:
        slot = 2
    polygon.material_index = slot
    face_counts[material_names[slot]] += 1

for polygon in mesh.polygons:
    polygon.use_smooth = True

if mesh_object.shape_key_add(name="Basis", from_mix=False) is None:
    raise RuntimeError("Could not create Basis shape key")


def add_shape(name, transform):
    key = mesh_object.shape_key_add(name=name, from_mix=False)
    key.value = 0.0
    for index, vertex in enumerate(mesh.vertices):
        key.data[index].co = transform(vertex.co.copy())


def slim(co):
    weight = max(0.2, min(1.0, co.z / 1.45))
    factor = 1.0 - 0.07 * weight
    co.x *= factor
    co.y *= factor
    return co


def broad(co):
    torso = max(0.0, 1.0 - abs(co.z - 1.15) / 0.65)
    co.x *= 1.0 + 0.09 * torso
    co.y *= 1.0 + 0.045 * torso
    return co


def tall(co):
    co.z *= 1.08
    return co


def stout(co):
    torso = max(0.0, 1.0 - abs(co.z - 0.95) / 0.72)
    co.x *= 1.0 + 0.10 * torso
    co.y *= 1.0 + 0.08 * torso
    co.z *= 0.96
    return co


add_shape("Body_Slim", slim)
add_shape("Body_Broad", broad)
add_shape("Body_Tall", tall)
add_shape("Body_Stout", stout)

for obj in bpy.context.scene.objects:
    obj.select_set(obj in {mesh_object, armature} or obj.name.startswith("Socket_"))
bpy.context.view_layer.objects.active = armature

bpy.ops.wm.save_as_mainfile(filepath=str(output_blend))
bpy.ops.export_scene.fbx(
    filepath=str(output_fbx),
    use_selection=True,
    object_types={"ARMATURE", "MESH", "EMPTY"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    add_leaf_bones=False,
    bake_anim=active_action is not None,
    bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,
    use_armature_deform_only=True,
    mesh_smooth_type="FACE",
    path_mode="COPY",
    embed_textures=False,
)

report = {
    "input_fbx": str(input_fbx),
    "output_blend": str(output_blend),
    "output_fbx": str(output_fbx),
    "vertices": len(mesh.vertices),
    "triangles": sum(len(p.loop_indices) - 2 for p in mesh.polygons),
    "bones": [bone.name for bone in armature.data.bones],
    "sockets": [obj.name for obj in bpy.context.scene.objects if obj.name.startswith("Socket_")],
    "shape_keys": [key.name for key in mesh.shape_keys.key_blocks],
    "material_face_counts": face_counts,
    "action": active_action.name if active_action is not None else "",
    "action_frame_range": list(active_action.frame_range) if active_action is not None else [],
}
Path(report_path).write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, separators=(",", ":")))
