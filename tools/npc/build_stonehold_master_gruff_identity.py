"""Inspect remesh vs raw, then bind the intact identity sculpt to AL_MasterRig."""
from __future__ import annotations

import json
import os

import bmesh
import bpy
from mathutils import Vector

ROOT = r"D:/AnotherLife/.worktrees/t_0df259d8"
RAW = os.path.join(
    ROOT,
    "unity/ArtSource/NPCs/rct_stonehold_npc_service_v001/Generation/meshy_multi_image_raw_v001.glb",
)
REMESH = os.path.join(
    ROOT,
    "unity/ArtSource/NPCs/rct_stonehold_npc_service_v001/Generation/meshy_remesh_24k_v001.glb",
)
TEX_DST = os.path.join(ROOT, "unity/ArtSource/NPCs/rct_stonehold_npc_service_v001/Textures")
ART = os.path.join(ROOT, "unity/ArtSource/NPCs/rct_stonehold_npc_service_v001")
EXPORTS = os.path.join(ART, "Exports")
BLEND_OUT = os.path.join(ART, "rct_stonehold_npc_service_humanoid_v001.blend")
FBX_OUT = os.path.join(EXPORTS, "rct_stonehold_npc_service_humanoid_v001.fbx")
REVIEW = os.path.join(
    ROOT, "unity/Docs/AssetLibrary/StoneholdMasterGruffNpc3DSourceV001/Review"
)
REPORT = os.path.join(
    ROOT,
    "unity/Docs/AssetLibrary/StoneholdMasterGruffNpc3DSourceV001/build_report_v001.json",
)
HEIGHT = 1.43
LOD0_NAMES = ["SM_Identity", "SM_BodyMask"]


def tris(obj) -> int:
    return sum(max(0, len(p.vertices) - 2) for p in obj.data.polygons)


def world_bounds(objs):
    mins = Vector((1e9, 1e9, 1e9))
    maxs = Vector((-1e9, -1e9, -1e9))
    for obj in objs:
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            mins.x, mins.y, mins.z = min(mins.x, world.x), min(mins.y, world.y), min(mins.z, world.z)
            maxs.x, maxs.y, maxs.z = max(maxs.x, world.x), max(maxs.y, world.y), max(maxs.z, world.z)
    return mins, maxs


def apply_object(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def plant(obj):
    mins, maxs = world_bounds([obj])
    scale = HEIGHT / max(maxs.z - mins.z, 1e-6)
    obj.scale = (scale, scale, scale)
    apply_object(obj)
    mins, maxs = world_bounds([obj])
    obj.location.x -= (mins.x + maxs.x) * 0.5
    obj.location.y -= (mins.y + maxs.y) * 0.5
    obj.location.z -= mins.z
    apply_object(obj)
    return world_bounds([obj])


def decimate_to(obj, target):
    current = max(tris(obj), 1)
    if current <= target:
        return
    dec = obj.modifiers.new("Decimate", "DECIMATE")
    dec.decimate_type = "COLLAPSE"
    dec.ratio = max(0.05, target / current)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier="Decimate")


def add_collection(name, parent):
    coll = bpy.data.collections.new(name)
    parent.children.link(coll)
    return coll


def link_only(obj, collection):
    for coll in list(obj.users_collection):
        coll.objects.unlink(obj)
    collection.objects.link(obj)


def make_principled(name, tex, normal, metallic=0.04, roughness=0.5):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    principled = nt.nodes.get("Principled BSDF")
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = tex
    nt.links.new(img.outputs["Color"], principled.inputs["Base Color"])
    ntex = nt.nodes.new("ShaderNodeTexImage")
    ntex.image = normal
    ntex.image.colorspace_settings.name = "Non-Color"
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], principled.inputs["Normal"])
    packed_path = os.path.join(TEX_DST, "metallic_smoothness_2k.png")
    if os.path.exists(packed_path):
        packed = bpy.data.images.load(packed_path)
        packed.colorspace_settings.name = "Non-Color"
        ptex = nt.nodes.new("ShaderNodeTexImage")
        ptex.image = packed
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(ptex.outputs["Color"], sep.inputs["Color"])
        nt.links.new(sep.outputs["Red"], principled.inputs["Metallic"])
        if "Alpha" in ptex.outputs:
            invert = nt.nodes.new("ShaderNodeInvert")
            nt.links.new(ptex.outputs["Alpha"], principled.inputs["Roughness"])
    return mat


def skin_mesh(obj, arm, max_influences=4, falloff=2.0):
    segs = []
    for bone in arm.data.bones:
        hw = arm.matrix_world @ bone.head_local
        tw = arm.matrix_world @ bone.tail_local
        segs.append((bone.name, Vector(hw), Vector(tw)))
    for vg in list(obj.vertex_groups):
        obj.vertex_groups.remove(vg)
    groups = {name: obj.vertex_groups.new(name=name) for name, _, _ in segs}
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    deformer = bm.verts.layers.deform.verify()
    mw = obj.matrix_world
    eps = 0.006
    for vert in bm.verts:
        wpos = mw @ vert.co
        dists = []
        for name, head, tail in segs:
            ab = tail - head
            length2 = ab.length_squared
            if length2 < 1e-12:
                dist = (wpos - head).length
            else:
                t = max(0.0, min(1.0, (wpos - head).dot(ab) / length2))
                dist = (wpos - (head + t * ab)).length
            dists.append((dist, name))
        dists.sort(key=lambda item: item[0])
        top = dists[:max_influences]
        weights = []
        total = 0.0
        for dist, name in top:
            weight = 1.0 / ((dist + eps) ** falloff)
            weights.append((weight, name))
            total += weight
        dvert = vert[deformer]
        for weight, name in weights:
            dvert[groups[name].index] = weight / total
    bm.to_mesh(obj.data)
    bm.free()
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    obj.parent = arm


def look_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_views(center, height, prefix):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    try:
        scene.eevee.taa_render_samples = 32
    except Exception:
        pass
    cam = scene.objects.get("ReviewCam")
    if cam is None:
        data = bpy.data.cameras.new("ReviewCam")
        cam = bpy.data.objects.new("ReviewCam", data)
        scene.collection.objects.link(cam)
        scene.camera = cam
        data.lens = 50
    radius = max(height * 1.85, 2.1)
    views = {
        "front": Vector((0.0, -1.0, 0.16)),
        "back": Vector((0.0, 1.0, 0.16)),
        "left": Vector((-1.0, 0.0, 0.16)),
        "right": Vector((1.0, 0.0, 0.16)),
        "threequarter": Vector((0.7, -0.7, 0.2)),
    }
    paths = []
    for name, direction in views.items():
        cam.location = center + direction.normalized() * radius
        look_at(cam, center)
        out = os.path.join(REVIEW, f"{prefix}_{name}_v001.png")
        scene.render.filepath = out
        bpy.ops.render.render(write_still=True)
        paths.append(out.replace("\\", "/"))
    cam.location = center + Vector((0.12, -0.42, height * 0.42))
    look_at(cam, center + Vector((0.0, 0.0, height * 0.40)))
    out = os.path.join(REVIEW, f"{prefix}_face_closeup_v001.png")
    scene.render.filepath = out
    bpy.ops.render.render(write_still=True)
    paths.append(out.replace("\\", "/"))
    cam.location = center + Vector((0.42, -0.55, -height * 0.05))
    look_at(cam, center + Vector((0.32, 0.0, -height * 0.08)))
    out = os.path.join(REVIEW, f"{prefix}_hands_closeup_v001.png")
    scene.render.filepath = out
    bpy.ops.render.render(write_still=True)
    paths.append(out.replace("\\", "/"))
    return paths


def assign_region_groups(obj, mins, height, cx, width):
    names = ["Head", "HairBeard", "Torso", "Apron", "Arm_L", "Arm_R", "Leg_L", "Leg_R", "Tools"]
    groups = {name: obj.vertex_groups.new(name=name) for name in names}
    mw = obj.matrix_world
    for vert in obj.data.vertices:
        p = mw @ vert.co
        t = (p.z - mins.z) / height
        nx = (p.x - cx) / max(width * 0.5, 1e-6)
        ny = p.y
        if t > 0.80:
            groups["HairBeard" if ny < -0.02 or t > 0.90 else "Head"].add([vert.index], 1.0, "REPLACE")
        elif t < 0.20:
            groups["Leg_L" if nx < 0 else "Leg_R"].add([vert.index], 1.0, "REPLACE")
        elif abs(nx) > 0.55 and t < 0.70:
            groups["Arm_L" if nx < 0 else "Arm_R"].add([vert.index], 1.0, "REPLACE")
        elif ny < -0.05 and 0.32 < t < 0.72:
            groups["Apron"].add([vert.index], 1.0, "REPLACE")
        elif 0.40 < t < 0.55 and ny < 0.0:
            groups["Tools"].add([vert.index], 1.0, "REPLACE")
        else:
            groups["Torso"].add([vert.index], 1.0, "REPLACE")


def main():
    os.makedirs(EXPORTS, exist_ok=True)
    os.makedirs(REVIEW, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    bpy.ops.import_scene.gltf(filepath=REMESH)
    remesh = [obj for obj in bpy.data.objects if obj.type == "MESH"][0]
    remesh_tris = tris(remesh)
    identity = remesh
    identity.name = "SM_Identity"
    source_used = "meshy_remesh_24k"
    raw_tris = 116484
    plant(identity)
    source_used = "meshy_remesh_planted"
    mins, maxs = world_bounds([identity])
    width, depth, height = maxs.x - mins.x, maxs.y - mins.y, maxs.z - mins.z
    cx = (mins.x + maxs.x) * 0.5

    root_coll = bpy.data.collections.new("MasterGruff")
    scene.collection.children.link(root_coll)
    slots = {}
    for name in ("torso", "head", "anchors", "rig", "LOD_Medium", "LOD_Low"):
        slots[name] = add_collection(name, root_coll)
    link_only(identity, slots["torso"])

    bpy.ops.mesh.primitive_cylinder_add(
        radius=0.16, depth=height * 0.48, vertices=12, location=(0, 0, mins.z + height * 0.46)
    )
    body = bpy.context.active_object
    body.name = "SM_BodyMask"
    apply_object(body)
    body.hide_render = True
    body.display_type = "WIRE"
    link_only(body, slots["torso"])

    # Keep Meshy remesh PBR; do not rebuild UVs or replace materials.
    if not identity.data.materials:
        base_img = bpy.data.images.load(os.path.join(TEX_DST, "base_color_2k.png"))
        nrm_img = bpy.data.images.load(os.path.join(TEX_DST, "normal_2k.png"))
        nrm_img.colorspace_settings.name = "Non-Color"
        mat = make_principled("M_MasterGruff", base_img, nrm_img)
        identity.data.materials.append(mat)
    assign_region_groups(identity, mins, height, cx, width)

    root = bpy.data.objects.new("root", None)
    slots["anchors"].objects.link(root)
    hip_z = mins.z + height * 0.52
    chest_z = mins.z + height * 0.72
    neck_z = mins.z + height * 0.80
    head_z = mins.z + height * 0.88
    shoulder_x = width * 0.22
    hand_x = width * 0.44
    foot_x = width * 0.12
    bone_spec = [
        ("Hips", None, (0, 0, hip_z), (0, 0, hip_z + 0.07)),
        ("Spine", "Hips", (0, 0, hip_z + 0.07), (0, 0, mins.z + height * 0.62)),
        ("Chest", "Spine", (0, 0, mins.z + height * 0.62), (0, 0, chest_z)),
        ("UpperChest", "Chest", (0, 0, chest_z), (0, 0, neck_z)),
        ("Neck", "UpperChest", (0, 0, neck_z), (0, 0, head_z)),
        ("Head", "Neck", (0, 0, head_z), (0, 0, mins.z + height * 0.99)),
        ("LeftShoulder", "UpperChest", (0.03, 0, neck_z), (-shoulder_x, 0, neck_z)),
        ("LeftUpperArm", "LeftShoulder", (-shoulder_x, 0, neck_z - 0.02), (-shoulder_x, 0, hip_z + 0.08)),
        ("LeftLowerArm", "LeftUpperArm", (-shoulder_x, 0, hip_z + 0.08), (-hand_x + 0.06, 0, mins.z + height * 0.38)),
        ("LeftHand", "LeftLowerArm", (-hand_x + 0.06, 0, mins.z + height * 0.38), (-hand_x, 0, mins.z + height * 0.32)),
        ("RightShoulder", "UpperChest", (0.03, 0, neck_z), (shoulder_x, 0, neck_z)),
        ("RightUpperArm", "RightShoulder", (shoulder_x, 0, neck_z - 0.02), (shoulder_x, 0, hip_z + 0.08)),
        ("RightLowerArm", "RightUpperArm", (shoulder_x, 0, hip_z + 0.08), (hand_x - 0.06, 0, mins.z + height * 0.38)),
        ("RightHand", "RightLowerArm", (hand_x - 0.06, 0, mins.z + height * 0.38), (hand_x, 0, mins.z + height * 0.32)),
        ("LeftUpperLeg", "Hips", (-foot_x, 0, hip_z), (-foot_x, 0, mins.z + height * 0.28)),
        ("LeftLowerLeg", "LeftUpperLeg", (-foot_x, 0, mins.z + height * 0.28), (-foot_x, 0, mins.z + 0.08)),
        ("LeftFoot", "LeftLowerLeg", (-foot_x, 0, mins.z + 0.08), (-foot_x, -0.12, mins.z + 0.03)),
        ("LeftToes", "LeftFoot", (-foot_x, -0.12, mins.z + 0.03), (-foot_x, -0.18, mins.z + 0.03)),
        ("RightUpperLeg", "Hips", (foot_x, 0, hip_z), (foot_x, 0, mins.z + height * 0.28)),
        ("RightLowerLeg", "RightUpperLeg", (foot_x, 0, mins.z + height * 0.28), (foot_x, 0, mins.z + 0.08)),
        ("RightFoot", "RightLowerLeg", (foot_x, 0, mins.z + 0.08), (foot_x, -0.12, mins.z + 0.03)),
        ("RightToes", "RightFoot", (foot_x, -0.12, mins.z + 0.03), (foot_x, -0.18, mins.z + 0.03)),
        ("Beard", "Head", (0, -0.04, head_z), (0, -0.12, chest_z + 0.02)),
        ("Apron_L", "Spine", (-0.08, -0.06, hip_z), (-0.10, -0.10, mins.z + height * 0.34)),
        ("Apron_R", "Spine", (0.08, -0.06, hip_z), (0.10, -0.10, mins.z + height * 0.34)),
    ]
    arm_data = bpy.data.armatures.new("AL_MasterRig")
    arm = bpy.data.objects.new("AL_MasterRig", arm_data)
    slots["rig"].objects.link(arm)
    arm.parent = root
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    ebones = arm_data.edit_bones
    for name, parent, head, tail in bone_spec:
        bone = ebones.new(name)
        bone.head = head
        bone.tail = tail
        if parent:
            bone.parent = ebones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    skin_mesh(identity, arm)
    skin_mesh(body, arm)

    lod0_total = tris(identity) + tris(body)

    def build_lod(suffix, collection, target):
        ratio = max(0.08, min(1.0, target / max(tris(identity), 1)))
        total = 0
        for src_obj in (identity, body):
            bpy.ops.object.select_all(action="DESELECT")
            src_obj.select_set(True)
            bpy.context.view_layer.objects.active = src_obj
            bpy.ops.object.duplicate()
            dup = bpy.context.active_object
            dup.name = src_obj.name + suffix
            link_only(dup, collection)
            if dup.data.shape_keys is not None:
                dup.shape_key_clear()
            for modifier in list(dup.modifiers):
                if modifier.type == "ARMATURE":
                    dup.modifiers.remove(modifier)
            dec = dup.modifiers.new("LOD_Decimate", "DECIMATE")
            dec.decimate_type = "COLLAPSE"
            dec.ratio = ratio
            bpy.context.view_layer.objects.active = dup
            bpy.ops.object.modifier_apply(modifier="LOD_Decimate")
            arm_mod = dup.modifiers.new("Armature", "ARMATURE")
            arm_mod.object = arm
            dup.parent = arm
            if src_obj is body:
                dup.hide_render = True
            total += tris(dup)
        return total

    lod1_total = build_lod("_LOD1", slots["LOD_Medium"], 7000)
    lod2_total = build_lod("_LOD2", slots["LOD_Low"], 1800)

    identity.shape_key_add(name="Basis")
    blink = identity.shape_key_add(name="blink")
    jaw = identity.shape_key_add(name="viseme_AA")
    mw = identity.matrix_world
    for vert in identity.data.vertices:
        co = mw @ vert.co
        if co.z > mins.z + height * 0.90 and abs(co.x) < 0.08:
            blink.data[vert.index].co.z -= 0.003
        if mins.z + height * 0.78 < co.z < mins.z + height * 0.88 and co.y < -0.02:
            jaw.data[vert.index].co.y -= 0.006

    sockets = {
        "VFX_ChestAnchor": (0.0, -0.16, chest_z),
        "VFX_Hand_L": (-hand_x, -0.04, mins.z + height * 0.34),
        "VFX_Hand_R": (hand_x, -0.04, mins.z + height * 0.34),
        "Socket_Talk": (0.0, -0.10, head_z),
        "Socket_Belt_R": (0.13, -0.12, mins.z + height * 0.46),
        "Socket_Belt_C": (0.0, -0.12, mins.z + height * 0.46),
        "Socket_Belt_L": (-0.12, -0.12, mins.z + height * 0.46),
        "PetAnchor": (-0.55, 0.18, hip_z),
        "MountAnchor": (0.0, 0.0, mins.z + 0.22),
    }
    for name, loc in sockets.items():
        empty = bpy.data.objects.new(name, None)
        empty.empty_display_size = 0.05
        empty.location = loc
        empty.parent = root
        slots["anchors"].objects.link(empty)

    def collider(name, loc, scale):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
        obj = bpy.context.active_object
        obj.name = name
        obj.scale = scale
        apply_object(obj)
        obj.display_type = "WIRE"
        obj.hide_render = True
        obj.parent = root
        link_only(obj, slots["anchors"])

    collider("UCX_Body", (0, 0, hip_z), (0.28, 0.18, 0.38))
    collider("UCX_Head", (0, 0, head_z), (0.14, 0.16, 0.14))
    collider("UCX_Legs", (0, 0, mins.z + height * 0.22), (0.22, 0.14, 0.22))

    key = bpy.data.lights.new("Key", "AREA")
    key.energy = 400
    key.size = 2.4
    key_obj = bpy.data.objects.new("Key", key)
    scene.collection.objects.link(key_obj)
    key_obj.location = (1.4, -1.8, height)
    fill = bpy.data.lights.new("Fill", "AREA")
    fill.energy = 110
    fill.size = 3.0
    fill_obj = bpy.data.objects.new("Fill", fill)
    scene.collection.objects.link(fill_obj)
    fill_obj.location = (-1.6, -0.6, height * 0.7)
    rim = bpy.data.lights.new("Rim", "AREA")
    rim.energy = 160
    rim.size = 2.0
    rim_obj = bpy.data.objects.new("Rim", rim)
    scene.collection.objects.link(rim_obj)
    rim_obj.location = (0.2, 1.8, height * 1.1)

    center = Vector((0.0, 0.0, height * 0.5))
    renders = render_views(center, height, "foundation")
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    pbone = arm.pose.bones["LeftUpperArm"]
    pbone.rotation_mode = "XYZ"
    pbone.rotation_euler[0] = 1.2
    bpy.ops.object.mode_set(mode="OBJECT")
    pose_paths = render_views(center, height, "foundation_pose_leftarm")
    bpy.ops.object.mode_set(mode="POSE")
    pbone.rotation_euler[0] = 0.0
    bpy.ops.object.mode_set(mode="OBJECT")

    bpy.ops.object.select_all(action="DESELECT")
    identity.select_set(True)
    body.select_set(True)
    arm.select_set(True)
    root.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=FBX_OUT,
        use_selection=True,
        add_leaf_bones=False,
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        object_types={"ARMATURE", "MESH", "EMPTY"},
        mesh_smooth_type="FACE",
        use_tspace=False,
        path_mode="COPY",
        embed_textures=False,
    )
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    report = {
        "rosterId": "rct_stonehold_npc_service_v001",
        "builder": "tools/npc/build_stonehold_master_gruff_identity.py",
        "sourceUsed": source_used,
        "rawTris": raw_tris,
        "remeshTris": remesh_tris,
        "heightMeters": round(height, 4),
        "lod0Tris": lod0_total,
        "lod1Tris": lod1_total,
        "lod2Tris": lod2_total,
        "identityTris": tris(identity),
        "humanoidBones": 22,
        "extraBones": ["Beard", "Apron_L", "Apron_R"],
        "geometricModuleSeparation": "incomplete_vertex_groups_only",
        "blend": BLEND_OUT.replace("\\", "/"),
        "fbx": FBX_OUT.replace("\\", "/"),
        "renders": renders + pose_paths,
        "completionMarker": "MASTER_GRUFF_BUILD_OK",
    }
    with open(REPORT, "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2)
    print("MASTER_GRUFF_BUILD_OK")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print("BUILD_FAILED", exc)
        raise
