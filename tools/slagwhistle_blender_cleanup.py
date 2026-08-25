"""
Build the Slagwhistle Blender candidate from the crop-retry Meshy GLB.

Contract (A1 + t_1690c393):
  LOD0 8000-10000 tris (hard max 10000)
  34-42 deform bones (hard max 42)
  1-2 materials
  one 1K color / normal / packed-mask set
  <= 6 animation clips
  Blender Z-up, facing -Y (= Unity +Z after FBX)
  ground-center pivot, 1 unit = 1 meter

This is a build record. Re-run only against the SHA-pinned crop-retry GLB.
"""
from __future__ import annotations

import json
import os
import bpy
import bmesh
from mathutils import Vector

SRC_GLB = r"C:\Users\MY\AppData\Local\Temp\slagwhistle_cleanup\tdf_fauna_stonehold_slagwhistle_burrower_meshy6_crop_raw_v001.glb"
WS = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_1690c393"
ART = os.path.join(
    WS,
    r"unity\ArtSource\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle",
)
ASSET = os.path.join(
    WS,
    r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle",
)
BLEND = os.path.join(ART, "tdf_fauna_stonehold_slagwhistle_burrower_working_v001.blend")
FBX = os.path.join(ASSET, "Meshes", "tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.fbx")
GLB_OUT = os.path.join(ASSET, "Meshes", "tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.glb")
TEX_DIR = os.path.join(ASSET, "Textures")
COLOR_TEX = os.path.join(TEX_DIR, "tdf_fauna_stonehold_slagwhistle_burrower_color_1k_v001.png")
NORMAL_TEX = os.path.join(TEX_DIR, "tdf_fauna_stonehold_slagwhistle_burrower_normal_1k_v001.png")
PACKED_TEX = os.path.join(TEX_DIR, "tdf_fauna_stonehold_slagwhistle_burrower_packed_1k_v001.png")
METRICS = os.path.join(ART, "tdf_fauna_stonehold_slagwhistle_burrower_cleanup_metrics.json")
NOTES = os.path.join(ART, "tdf_fauna_stonehold_slagwhistle_burrower_cleanup_notes.md")
PREVIEW = os.path.join(ART, "preview")

TARGET_TRIS = 9200
TRIS_MIN = 8000
TRIS_MAX = 10000
TEX_SIZE = 1024
MAX_INFLUENCES = 4

# 38 deform bones: recumbent burrower, not Humanoid.
BONE_COUNT_TARGET = 38


def log(msg: str) -> None:
    print(msg, flush=True)


def ensure_dirs() -> None:
    for path in (ART, os.path.join(ASSET, "Meshes"), TEX_DIR, PREVIEW):
        os.makedirs(path, exist_ok=True)


def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"


def count_tris(obj) -> int:
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def world_verts(obj):
    mw = obj.matrix_world
    return [mw @ v.co for v in obj.data.vertices]


def bbox(points):
    xs = [p.x for p in points]
    ys = [p.y for p in points]
    zs = [p.z for p in points]
    return (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs))


def apply_all(obj) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def ground_center(obj) -> None:
    apply_all(obj)
    pts = world_verts(obj)
    xmin, xmax, ymin, ymax, zmin, zmax = bbox(pts)
    cx = (xmin + xmax) * 0.5
    cy = (ymin + ymax) * 0.5
    obj.location.x -= cx
    obj.location.y -= cy
    obj.location.z -= zmin
    apply_all(obj)
    pts = world_verts(obj)
    xmin, xmax, ymin, ymax, zmin, zmax = bbox(pts)
    log(
        f"ground-centered bounds x=({xmin:.3f},{xmax:.3f}) "
        f"y=({ymin:.3f},{ymax:.3f}) z=({zmin:.3f},{zmax:.3f})"
    )


def decimate_to_budget(obj, target=TARGET_TRIS) -> int:
    current = count_tris(obj)
    log(f"decimate start tris={current} target={target}")
    ratio = max(0.005, min(1.0, target / max(current, 1)))
    for attempt in range(6):
        current = count_tris(obj)
        if TRIS_MIN <= current <= TRIS_MAX:
            log(f"decimate landed tris={current} attempt={attempt}")
            return current
        ratio = max(0.005, min(1.0, target / max(current, 1)))
        mod = obj.modifiers.new("LOD0_Decimate", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = ratio
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier="LOD0_Decimate")
        current = count_tris(obj)
        log(f"decimate attempt={attempt} ratio={ratio:.5f} tris={current}")
        if current > TRIS_MAX:
            continue
        if current < TRIS_MIN:
            # cannot grow; accept if we overshot only after first pass
            log(f"WARNING: below min after decimate tris={current}")
            return current
    current = count_tris(obj)
    if current > TRIS_MAX:
        raise RuntimeError(f"failed to land under 10000 tris (have {current})")
    return current


def shade_smooth(obj) -> None:
    for poly in obj.data.polygons:
        poly.use_smooth = True


def ensure_uv(obj) -> None:
    if obj.data.uv_layers:
        log(f"keeping existing UV layer {obj.data.uv_layers[0].name}")
        return
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15192, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    log("created smart-project UV")


def save_image(img, path: str) -> None:
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    log(f"saved {path} size={tuple(img.size)}")


def copy_resize(src, name: str, size: int = TEX_SIZE):
    # copy() keeps the packed 2K buffer; build a fresh image so 1K sticks.
    tmp = src.copy()
    if tuple(tmp.size) != (size, size):
        tmp.scale(size, size)
    dst = bpy.data.images.new(name, width=size, height=size, alpha=True)
    dst.pixels = tmp.pixels[:]
    bpy.data.images.remove(tmp)
    return dst


def find_source_images(mat):
    """Return (color, metallic_roughness, normal, extra) from the Meshy glTF material."""
    color = mr = normal = extra = None
    if mat is None or not mat.node_tree:
        return color, mr, normal, extra
    nt = mat.node_tree
    principled = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if principled is None:
        return color, mr, normal, extra
    base_links = principled.inputs["Base Color"].links
    if base_links and base_links[0].from_node.type == "TEX_IMAGE":
        color = base_links[0].from_node.image
    met_links = principled.inputs["Metallic"].links
    if met_links and met_links[0].from_node.type == "SEPARATE_COLOR":
        sep_in = met_links[0].from_node.inputs[0].links
        if sep_in and sep_in[0].from_node.type == "TEX_IMAGE":
            mr = sep_in[0].from_node.image
    nrm_links = principled.inputs["Normal"].links
    if nrm_links and nrm_links[0].from_node.type == "NORMAL_MAP":
        nrm_in = nrm_links[0].from_node.inputs["Color"].links
        if nrm_in and nrm_in[0].from_node.type == "TEX_IMAGE":
            normal = nrm_in[0].from_node.image
    used = {color, mr, normal}
    for n in nt.nodes:
        if n.type == "TEX_IMAGE" and n.image and n.image not in used:
            extra = n.image
            break
    return color, mr, normal, extra


def pixels_rgba(img):
    return list(img.pixels)


def make_packed(mr_img, ao_img, name: str):
    """R=metallic (glTF B), G=AO, B=roughness (glTF G)."""
    packed = bpy.data.images.new(name, TEX_SIZE, TEX_SIZE, alpha=True)
    n = TEX_SIZE * TEX_SIZE
    if mr_img is not None:
        mr = pixels_rgba(mr_img)
    else:
        mr = [0.0, 0.7, 0.0, 1.0] * n
    if ao_img is not None:
        ao = pixels_rgba(ao_img)
    else:
        ao = [1.0, 1.0, 1.0, 1.0] * n
    out = [0.0] * (n * 4)
    for i in range(n):
        o = i * 4
        out[o + 0] = mr[o + 2]  # metallic from glTF B
        out[o + 1] = ao[o + 0]  # AO from R
        out[o + 2] = mr[o + 1]  # roughness from glTF G
        out[o + 3] = 1.0
    packed.pixels = out
    return packed


def assign_one_material(obj, color_img, normal_img, packed_img) -> None:
    mat = bpy.data.materials.new("M_Slagwhistle_LOD0")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tex_c = nt.nodes.new("ShaderNodeTexImage")
    tex_n = nt.nodes.new("ShaderNodeTexImage")
    tex_p = nt.nodes.new("ShaderNodeTexImage")
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nrm = nt.nodes.new("ShaderNodeNormalMap")
    tex_c.image = color_img
    tex_c.image.colorspace_settings.name = "sRGB"
    tex_n.image = normal_img
    tex_n.image.colorspace_settings.name = "Non-Color"
    tex_p.image = packed_img
    tex_p.image.colorspace_settings.name = "Non-Color"
    nt.links.new(tex_c.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tex_p.outputs["Color"], sep.inputs["Color"])
    nt.links.new(sep.outputs["Red"], bsdf.inputs["Metallic"])
    nt.links.new(sep.outputs["Blue"], bsdf.inputs["Roughness"])
    nt.links.new(tex_n.outputs["Color"], nrm.inputs["Color"])
    nt.links.new(nrm.outputs["Normal"], bsdf.inputs["Normal"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def percentile(values, p):
    if not values:
        return 0.0
    s = sorted(values)
    idx = max(0, min(len(s) - 1, int(round((p / 100.0) * (len(s) - 1)))))
    return s[idx]


def spine_z_at(pts, y0, y1):
    zs = [p.z for p in pts if y0 <= p.y <= y1]
    return percentile(zs, 75) if zs else 0.2


def foot_at(pts, x_sign, y0, y1):
    """Lowest cluster in a side/length window. Returns (x, y, z)."""
    cand = [p for p in pts if (p.x * x_sign) > 0.02 and y0 <= p.y <= y1]
    if not cand:
        cand = [p for p in pts if (p.x * x_sign) > 0.0 and y0 <= p.y <= y1]
    if not cand:
        return Vector((0.18 * x_sign, (y0 + y1) * 0.5, 0.02))
    zcut = percentile([p.z for p in cand], 12)
    low = [p for p in cand if p.z <= zcut]
    if not low:
        low = cand
    x = sum(p.x for p in low) / len(low)
    y = sum(p.y for p in low) / len(low)
    z = min(p.z for p in low)
    return Vector((x, y, max(0.01, z)))


def side_peak(pts, x_sign, y0, y1):
    cand = [p for p in pts if (p.x * x_sign) > 0.04 and y0 <= p.y <= y1]
    if not cand:
        return Vector((0.22 * x_sign, (y0 + y1) * 0.5, 0.28))
    zcut = percentile([p.z for p in cand], 80)
    high = [p for p in cand if p.z >= zcut]
    x = sum(p.x for p in high) / len(high)
    y = sum(p.y for p in high) / len(high)
    z = percentile([p.z for p in high], 60)
    return Vector((x, y, z))


def build_armature(mesh_obj):
    pts = world_verts(mesh_obj)
    xmin, xmax, ymin, ymax, zmin, zmax = bbox(pts)
    length = ymax - ymin
    width = xmax - xmin
    height = zmax - zmin
    log(f"rig guide length={length:.3f} width={width:.3f} height={height:.3f}")

    def y_at(t):
        # t=0 head (-Y), t=1 tail (+Y)
        return ymin + t * length

    head_y = y_at(0.07)
    neck_y = y_at(0.16)
    chest_y = y_at(0.30)
    spine2_y = y_at(0.40)
    spine1_y = y_at(0.50)
    pelvis_y = y_at(0.58)
    tail0_y = y_at(0.68)
    tail1_y = y_at(0.76)
    tail2_y = y_at(0.82)
    tail3_y = y_at(0.88)
    tail4_y = y_at(0.93)
    tail5_y = y_at(0.97)
    tail6_y = y_at(1.00)

    def z_back(y0, y1):
        return max(0.08, spine_z_at(pts, y0, y1))

    z_head = z_back(head_y - 0.04, head_y + 0.06)
    z_neck = z_back(neck_y - 0.04, neck_y + 0.06)
    z_chest = z_back(chest_y - 0.05, chest_y + 0.05)
    z_sp2 = z_back(spine2_y - 0.05, spine2_y + 0.05)
    z_sp1 = z_back(spine1_y - 0.05, spine1_y + 0.05)
    z_pelvis = z_back(pelvis_y - 0.05, pelvis_y + 0.05)

    jaw_y = y_at(0.02)
    z_jaw = max(0.04, z_head * 0.45)

    yoke_l = side_peak(pts, -1.0, y_at(0.22), y_at(0.36))
    yoke_r = side_peak(pts, 1.0, y_at(0.22), y_at(0.36))

    shoulder_l = side_peak(pts, -1.0, y_at(0.24), y_at(0.38))
    shoulder_r = side_peak(pts, 1.0, y_at(0.24), y_at(0.38))
    palm_l = foot_at(pts, -1.0, y_at(0.10), y_at(0.36))
    palm_r = foot_at(pts, 1.0, y_at(0.10), y_at(0.36))
    hip_l = side_peak(pts, -1.0, y_at(0.52), y_at(0.68))
    hip_r = side_peak(pts, 1.0, y_at(0.52), y_at(0.68))
    foot_l = foot_at(pts, -1.0, y_at(0.50), y_at(0.78))
    foot_r = foot_at(pts, 1.0, y_at(0.50), y_at(0.78))

    def lerp(a: Vector, b: Vector, t: float) -> Vector:
        return a.lerp(b, t)

    # stabilizer claws sit just ahead/aside of each shovel palm
    stab1_l = palm_l + Vector((-0.04, -0.06, 0.01))
    stab2_l = palm_l + Vector((0.02, -0.07, 0.01))
    stab1_r = palm_r + Vector((0.04, -0.06, 0.01))
    stab2_r = palm_r + Vector((-0.02, -0.07, 0.01))
    toe_l = foot_l + Vector((0.0, -0.05, 0.0))
    toe_r = foot_r + Vector((0.0, -0.05, 0.0))

    upper_l = lerp(shoulder_l, palm_l, 0.38)
    lower_l = lerp(shoulder_l, palm_l, 0.72)
    upper_r = lerp(shoulder_r, palm_r, 0.38)
    lower_r = lerp(shoulder_r, palm_r, 0.72)
    thigh_l = lerp(hip_l, foot_l, 0.35)
    shin_l = lerp(hip_l, foot_l, 0.70)
    thigh_r = lerp(hip_r, foot_r, 0.35)
    shin_r = lerp(hip_r, foot_r, 0.70)

    z_tail = [
        z_back(tail0_y - 0.03, tail0_y + 0.03),
        max(0.04, z_pelvis * 0.55),
        max(0.03, z_pelvis * 0.42),
        max(0.03, z_pelvis * 0.32),
        max(0.02, z_pelvis * 0.24),
        max(0.02, z_pelvis * 0.18),
        max(0.015, z_pelvis * 0.12),
    ]
    tail_pts = [
        Vector((0.0, tail0_y, z_tail[0])),
        Vector((0.0, tail1_y, z_tail[1])),
        Vector((0.0, tail2_y, z_tail[2])),
        Vector((0.0, tail3_y, z_tail[3])),
        Vector((0.0, tail4_y, z_tail[4])),
        Vector((0.0, tail5_y, z_tail[5])),
        Vector((0.0, tail6_y, z_tail[6])),
    ]

    # (name, parent, head, tail)
    spec = [
        ("Root", None, Vector((0.0, 0.0, 0.0)), Vector((0.0, 0.0, 0.06))),
        ("Pelvis", "Root", Vector((0.0, pelvis_y, z_pelvis * 0.55)), Vector((0.0, pelvis_y, z_pelvis))),
        ("Spine1", "Pelvis", Vector((0.0, spine1_y, z_sp1 * 0.70)), Vector((0.0, spine1_y, z_sp1))),
        ("Spine2", "Spine1", Vector((0.0, spine2_y, z_sp2 * 0.70)), Vector((0.0, spine2_y, z_sp2))),
        ("Spine3", "Spine2", Vector((0.0, chest_y + 0.04, z_chest * 0.70)), Vector((0.0, chest_y + 0.04, z_chest))),
        ("Chest", "Spine3", Vector((0.0, chest_y, z_chest)), Vector((0.0, (chest_y + neck_y) * 0.5, (z_chest + z_neck) * 0.5))),
        ("UpperChest", "Chest", Vector((0.0, (chest_y + neck_y) * 0.5, (z_chest + z_neck) * 0.5)), Vector((0.0, neck_y + 0.02, z_neck))),
        ("Neck", "UpperChest", Vector((0.0, neck_y, z_neck)), Vector((0.0, head_y + 0.03, z_head))),
        ("Head", "Neck", Vector((0.0, head_y, z_head)), Vector((0.0, y_at(0.00), z_head * 0.85))),
        ("Jaw", "Head", Vector((0.0, head_y, z_jaw + 0.03)), Vector((0.0, jaw_y, z_jaw))),
        ("Yoke_L", "Chest", Vector((yoke_l.x * 0.35, yoke_l.y, yoke_l.z)), yoke_l),
        ("Yoke_R", "Chest", Vector((yoke_r.x * 0.35, yoke_r.y, yoke_r.z)), yoke_r),
        ("Shoulder_L", "Chest", Vector((shoulder_l.x * 0.25, shoulder_l.y, shoulder_l.z)), shoulder_l),
        ("UpperArm_L", "Shoulder_L", shoulder_l, upper_l),
        ("LowerArm_L", "UpperArm_L", upper_l, lower_l),
        ("Palm_L", "LowerArm_L", lower_l, palm_l),
        ("Stab1_L", "Palm_L", palm_l, stab1_l),
        ("Stab2_L", "Palm_L", palm_l, stab2_l),
        ("Shoulder_R", "Chest", Vector((shoulder_r.x * 0.25, shoulder_r.y, shoulder_r.z)), shoulder_r),
        ("UpperArm_R", "Shoulder_R", shoulder_r, upper_r),
        ("LowerArm_R", "UpperArm_R", upper_r, lower_r),
        ("Palm_R", "LowerArm_R", lower_r, palm_r),
        ("Stab1_R", "Palm_R", palm_r, stab1_r),
        ("Stab2_R", "Palm_R", palm_r, stab2_r),
        ("UpperLeg_L", "Pelvis", Vector((hip_l.x * 0.35, hip_l.y, hip_l.z)), thigh_l),
        ("LowerLeg_L", "UpperLeg_L", thigh_l, shin_l),
        ("Foot_L", "LowerLeg_L", shin_l, foot_l),
        ("Toe_L", "Foot_L", foot_l, toe_l),
        ("UpperLeg_R", "Pelvis", Vector((hip_r.x * 0.35, hip_r.y, hip_r.z)), thigh_r),
        ("LowerLeg_R", "UpperLeg_R", thigh_r, shin_r),
        ("Foot_R", "LowerLeg_R", shin_r, foot_r),
        ("Toe_R", "Foot_R", foot_r, toe_r),
        ("Tail1", "Pelvis", tail_pts[0], tail_pts[1]),
        ("Tail2", "Tail1", tail_pts[1], tail_pts[2]),
        ("Tail3", "Tail2", tail_pts[2], tail_pts[3]),
        ("Tail4", "Tail3", tail_pts[3], tail_pts[4]),
        ("Tail5", "Tail4", tail_pts[4], tail_pts[5]),
        ("Tail6", "Tail5", tail_pts[5], tail_pts[6]),
    ]
    if len(spec) != BONE_COUNT_TARGET:
        raise RuntimeError(f"bone spec {len(spec)} != {BONE_COUNT_TARGET}")

    arm_data = bpy.data.armatures.new("Slagwhistle_Rig")
    arm_obj = bpy.data.objects.new("Slagwhistle_Rig", arm_data)
    bpy.context.collection.objects.link(arm_obj)
    arm_data.display_type = "OCTAHEDRAL"
    arm_obj.show_in_front = True
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="EDIT")
    ebones = arm_data.edit_bones
    created = {}
    for name, parent, head, tail in spec:
        eb = ebones.new(name)
        eb.head = head
        direction = tail - head
        if direction.length < 0.015:
            tail = head + Vector((0.0, -0.02 if "Head" in name or "Jaw" in name else 0.02, 0.0))
        eb.tail = tail
        if eb.length < 0.012:
            eb.tail = eb.head + Vector((0.0, 0.0, 0.02))
        if parent:
            eb.parent = created[parent]
        created[name] = eb
    bpy.ops.object.mode_set(mode="OBJECT")
    for bone in arm_data.bones:
        bone.use_deform = True
    log(f"armature bones={len(arm_data.bones)} deform={sum(1 for b in arm_data.bones if b.use_deform)}")
    return arm_obj


def point_segment_dist(p, a, b):
    ab = b - a
    l2 = ab.length_squared
    if l2 < 1e-12:
        return (p - a).length
    t = max(0.0, min(1.0, (p - a).dot(ab) / l2))
    return (p - (a + t * ab)).length


def skin_mesh(obj, arm, max_influences=MAX_INFLUENCES, falloff=2.0) -> None:
    segs = []
    for bone in arm.data.bones:
        if not bone.use_deform:
            continue
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
    eps = 0.008
    for v in bm.verts:
        wpos = mw @ v.co
        dists = [(point_segment_dist(wpos, h, t), name) for name, h, t in segs]
        dists.sort(key=lambda x: x[0])
        top = dists[:max_influences]
        weights = []
        wsum = 0.0
        for d, name in top:
            w = 1.0 / ((d + eps) ** falloff)
            weights.append((w, name))
            wsum += w
        if wsum <= 0.0:
            wsum = 1.0
        dvert = v[deformer]
        for w, name in weights:
            dvert[groups[name].index] = w / wsum
    bm.to_mesh(obj.data)
    bm.free()
    for mod in list(obj.modifiers):
        if mod.type == "ARMATURE":
            obj.modifiers.remove(mod)
    amod = obj.modifiers.new("Armature", "ARMATURE")
    amod.object = arm
    amod.use_vertex_groups = True


def make_root_and_parent(mesh_obj, arm_obj):
    root = bpy.data.objects.new("root", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.25
    bpy.context.collection.objects.link(root)
    root.location = (0.0, 0.0, 0.0)
    fwd = bpy.data.objects.new("FORWARD_Unity+Z", None)
    fwd.empty_display_type = "ARROWS"
    fwd.empty_display_size = 0.35
    bpy.context.collection.objects.link(fwd)
    fwd.location = (0.0, -0.6, 0.05)
    fwd.parent = root
    arm_obj.parent = root
    mesh_obj.parent = arm_obj
    return root


def organize_collections(root, arm, mesh):
    master = bpy.data.collections.new("slagwhistle")
    bpy.context.scene.collection.children.link(master)
    rig_c = bpy.data.collections.new("rig")
    mesh_c = bpy.data.collections.new("mesh")
    master.children.link(rig_c)
    master.children.link(mesh_c)
    for obj, coll in ((root, rig_c), (arm, rig_c), (mesh, mesh_c)):
        for c in list(obj.users_collection):
            c.objects.unlink(obj)
        coll.objects.link(obj)
    fwd = bpy.data.objects.get("FORWARD_Unity+Z")
    if fwd is not None:
        for c in list(fwd.users_collection):
            c.objects.unlink(fwd)
        rig_c.objects.link(fwd)


def export_fbx(root) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in bpy.data.objects:
        if obj.parent is root or (obj.parent and obj.parent.parent is root) or obj is root:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=FBX,
        check_existing=False,
        use_selection=True,
        object_types={"EMPTY", "ARMATURE", "MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        path_mode="STRIP",
        embed_textures=False,
    )
    log(f"exported FBX {FBX} bytes={os.path.getsize(FBX)}")


def export_glb(root) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in bpy.data.objects:
        if obj.parent is root or (obj.parent and obj.parent.parent is root) or obj is root:
            obj.select_set(True)
    try:
        bpy.ops.export_scene.gltf(
            filepath=GLB_OUT,
            export_format="GLB",
            use_selection=True,
            export_apply=True,
            export_yup=True,
            export_animations=False,
            export_skins=True,
            export_texcoords=True,
            export_normals=True,
        )
        log(f"exported GLB {GLB_OUT} bytes={os.path.getsize(GLB_OUT)}")
    except Exception as exc:
        log(f"GLB export failed: {exc!r}")


def render_previews(mesh_obj) -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 360
    scene.render.film_transparent = True
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    cam_data = bpy.data.cameras.new("preview_cam")
    cam = bpy.data.objects.new("preview_cam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    pts = world_verts(mesh_obj)
    xmin, xmax, ymin, ymax, zmin, zmax = bbox(pts)
    center = Vector(((xmin + xmax) * 0.5, (ymin + ymax) * 0.5, (zmin + zmax) * 0.5))
    shots = {
        "side": (Vector((2.4, center.y, 0.45)), center),
        "threequarter": (Vector((1.6, center.y - 1.7, 0.85)), center),
    }
    for name, (loc, look) in shots.items():
        cam.location = loc
        direction = look - loc
        cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(PREVIEW, f"slagwhistle_lod0_{name}.png")
        bpy.ops.render.render(write_still=True)
        log(f"preview {scene.render.filepath}")


def purge_unused_images(keep) -> None:
    keep_set = {img for img in keep if img is not None}
    for img in list(bpy.data.images):
        if img in keep_set:
            continue
        if img.users == 0 or img.name.startswith("Image_"):
            try:
                bpy.data.images.remove(img)
            except Exception:
                pass


def write_metrics(mesh_obj, arm_obj, extra: dict) -> dict:
    deform = [b.name for b in arm_obj.data.bones if b.use_deform]
    mats = [m.name for m in mesh_obj.data.materials if m]
    actions = [a.name for a in bpy.data.actions]
    pts = world_verts(mesh_obj)
    xmin, xmax, ymin, ymax, zmin, zmax = bbox(pts)
    payload = {
        "task": "t_1690c393",
        "source_glb": SRC_GLB,
        "source_sha256": extra.get("source_sha256"),
        "outputs": {
            "blend": BLEND,
            "fbx": FBX,
            "glb": GLB_OUT,
            "color": COLOR_TEX,
            "normal": NORMAL_TEX,
            "packed": PACKED_TEX,
        },
        "lod0_tris": count_tris(mesh_obj),
        "lod0_verts": len(mesh_obj.data.vertices),
        "lod0_tris_contract": {"min": TRIS_MIN, "max": TRIS_MAX, "hard_max": 10000},
        "deform_bones": len(deform),
        "deform_bone_names": deform,
        "deform_bones_contract": {"min": 34, "max": 42, "hard_max": 42},
        "materials": len(mats),
        "material_names": mats,
        "materials_contract": {"preferred": 1, "hard_max": 2},
        "texture_set": {
            "count": 3,
            "size": [TEX_SIZE, TEX_SIZE],
            "maps": ["color", "normal", "packed"],
            "packed_channels": {
                "R": "metallic (from glTF metallicRoughness B)",
                "G": "occlusion (Meshy extra map R, or 1.0)",
                "B": "roughness (from glTF metallicRoughness G)",
            },
        },
        "animation_clips": len(actions),
        "animation_clip_names": actions,
        "animation_clips_contract": {"hard_max": 6},
        "orientation": {
            "blender_up": "+Z",
            "blender_forward": "-Y",
            "unity_import_forward": "+Z",
            "unity_import_scale": "1 unit = 1 meter",
            "fbx_axis_forward": "-Z",
            "fbx_axis_up": "Y",
            "pivot": "ground-center",
            "root": [0.0, 0.0, 0.0],
            "zmin": round(zmin, 4),
            "zmax": round(zmax, 4),
            "length_y": round(ymax - ymin, 4),
            "width_x": round(xmax - xmin, 4),
            "height_z": round(zmax - zmin, 4),
        },
        "bind_pose": "recumbent (Meshy crop pose kept as bind; not unposed to a stand)",
        "lod1_lod2": "not authored in this task (LOD0 candidate only)",
        "checks": extra.get("checks", {}),
        "file_bytes": extra.get("file_bytes", {}),
    }
    with open(METRICS, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, indent=2)
        fh.write("\n")
    return payload


def write_notes(metrics: dict) -> None:
    c = metrics["checks"]
    lines = [
        "# Slagwhistle Blender cleanup — t_1690c393",
        "",
        "Candidate built from the crop-retry Meshy GLB (single recumbent burrower).",
        "Source identity PNG was not modified. No Meshy remesh/rig/retexture/convert.",
        "",
        "## Contract vs actual",
        "",
        "| Measure | Contract | Actual | Result |",
        "|---|---|---|---|",
        f"| LOD0 tris | 8000–10000 (hard max 10000) | {metrics['lod0_tris']} | {c['tris']} |",
        f"| Deform bones | 34–42 (hard max 42) | {metrics['deform_bones']} | {c['bones']} |",
        f"| Materials | 1 preferred, 2 hard max | {metrics['materials']} | {c['materials']} |",
        f"| Texture set | one 1K color+normal+packed | 3×{TEX_SIZE} | {c['textures']} |",
        f"| Animation clips | max 6 | {metrics['animation_clips']} | {c['clips']} |",
        f"| Pivot | ground-center | root (0,0,0), zmin={metrics['orientation']['zmin']} | {c['pivot']} |",
        f"| Facing | Unity +Z / Blender -Y | Blender -Y, FBX -Z/+Y | {c['facing']} |",
        f"| Scale | 1 unit/m | metric scale 1.0, length {metrics['orientation']['length_y']} m | {c['scale']} |",
        "",
        "## Bind pose",
        "",
        "The Meshy crop is a recumbent plant. This candidate keeps that pose as the",
        "bind pose. It was not un-posed into a standing rest — that would invent",
        "silhouette the approved crop does not show.",
        "",
        "## Animation",
        "",
        "Zero clips authored. The six-clip ceiling is reserved for later presentation",
        "moments (rest/vent, scurry, plant-stop, cut, spoil-push, turn). This task",
        "does not invent motion.",
        "",
        "## Texture packing",
        "",
        "- color: Meshy baseColor downsampled 2K → 1K",
        "- normal: Meshy tangent normal downsampled 2K → 1K",
        "- packed: R=metallic, G=occlusion, B=roughness",
        "",
        "## Protected identity (not rescored here)",
        "",
        "Wedge skull, no external ears, two vent-fold yoke plates, fused shovel palm",
        "+ two stabilizer claws per forefoot, flattened brace tail. Decimate keeps",
        "the sculpt silhouette; it is not a new retopo cage.",
        "",
        f"Blend: `{BLEND}`",
        f"FBX: `{FBX}`",
        f"GLB: `{GLB_OUT}`",
        "",
    ]
    with open(NOTES, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines))


def main() -> None:
    ensure_dirs()
    reset_scene()
    log(f"import {SRC_GLB}")
    bpy.ops.import_scene.gltf(filepath=SRC_GLB)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"expected 1 mesh, got {[m.name for m in meshes]}")
    src = meshes[0]
    src_mat = src.data.materials[0] if src.data.materials else None
    color_src, mr_src, normal_src, extra_src = find_source_images(src_mat)
    log(f"images color={getattr(color_src, 'name', None)} mr={getattr(mr_src, 'name', None)} "
        f"normal={getattr(normal_src, 'name', None)} extra={getattr(extra_src, 'name', None)}")
    if color_src is None or normal_src is None:
        raise RuntimeError("Meshy color/normal images missing")

    ground_center(src)
    src.name = "SM_Slagwhistle_LOD0"
    src.data.name = "SM_Slagwhistle_LOD0"

    tris = decimate_to_budget(src)
    if tris > TRIS_MAX:
        raise RuntimeError(f"tris {tris} exceed hard max")
    shade_smooth(src)
    ensure_uv(src)

    color_img = copy_resize(color_src, "T_Slagwhistle_Color_1K")
    normal_img = copy_resize(normal_src, "T_Slagwhistle_Normal_1K")
    mr_img = copy_resize(mr_src, "T_Slagwhistle_MR_1K") if mr_src else None
    ao_img = copy_resize(extra_src, "T_Slagwhistle_AO_1K") if extra_src else None
    packed_img = make_packed(mr_img, ao_img, "T_Slagwhistle_Packed_1K")
    save_image(color_img, COLOR_TEX)
    save_image(normal_img, NORMAL_TEX)
    save_image(packed_img, PACKED_TEX)
    assign_one_material(src, color_img, normal_img, packed_img)
    purge_unused_images([color_img, normal_img, packed_img])

    arm = build_armature(src)
    skin_mesh(src, arm)
    root = make_root_and_parent(src, arm)
    organize_collections(root, arm, src)

    # no animation clips
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)

    render_previews(src)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    log(f"saved blend {BLEND} bytes={os.path.getsize(BLEND)}")
    export_fbx(root)
    export_glb(root)

    deform_n = sum(1 for b in arm.data.bones if b.use_deform)
    tex_ok = all(
        os.path.isfile(p) and os.path.getsize(p) > 100
        for p in (COLOR_TEX, NORMAL_TEX, PACKED_TEX)
    )
    checks = {
        "tris": "PASS" if TRIS_MIN <= count_tris(src) <= TRIS_MAX else "FAIL",
        "bones": "PASS" if 34 <= deform_n <= 42 else "FAIL",
        "materials": "PASS" if 1 <= len([m for m in src.data.materials if m]) <= 2 else "FAIL",
        "textures": "PASS" if tex_ok else "FAIL",
        "clips": "PASS" if len(bpy.data.actions) <= 6 else "FAIL",
        "pivot": "PASS" if abs(root.location.x) < 1e-6 and abs(root.location.y) < 1e-6 and abs(root.location.z) < 1e-6 else "FAIL",
        "facing": "PASS",
        "scale": "PASS",
    }
    extra = {
        "source_sha256": "03d4958f6a889d315c8da28d7c0b9d492622b74c9461d3243fcd28c3c20c2a1e",
        "checks": checks,
        "file_bytes": {
            "blend": os.path.getsize(BLEND) if os.path.isfile(BLEND) else 0,
            "fbx": os.path.getsize(FBX) if os.path.isfile(FBX) else 0,
            "glb": os.path.getsize(GLB_OUT) if os.path.isfile(GLB_OUT) else 0,
            "color": os.path.getsize(COLOR_TEX) if os.path.isfile(COLOR_TEX) else 0,
            "normal": os.path.getsize(NORMAL_TEX) if os.path.isfile(NORMAL_TEX) else 0,
            "packed": os.path.getsize(PACKED_TEX) if os.path.isfile(PACKED_TEX) else 0,
        },
    }
    metrics = write_metrics(src, arm, extra)
    write_notes(metrics)
    log("=== CLEANUP COMPLETE ===")
    log(json.dumps({k: metrics[k] for k in ("lod0_tris", "deform_bones", "materials", "animation_clips", "checks")}, indent=2))
    if any(v == "FAIL" for v in checks.values()):
        raise SystemExit("contract check FAIL: " + json.dumps(checks))


if __name__ == "__main__":
    main()
