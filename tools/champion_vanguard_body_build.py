"""
Champion Vanguard (crownlands) — high-LOD body module builder.
Headless Blender script. Builds modular base-body meshes (head, hair, face,
torso, arms, legs) into the shared working file, quad-dominant topology,
7.75-head proportions, neutral placeholder materials, +Z-forward / ground-
center pivot (root at origin, feet at Z=0).

Run:
  blender --background champion_vanguard_working_v001.blend --python tools/_cv_body_build.py
"""
import bpy
import math
from mathutils import Vector

SUB_LEVEL = 2          # Catmull-Clark level -> all-quad from cube/cylinder input
CY_SEGMENTS = 12       # cylinder side count (subdiv -> smooth rounded tube)

# ---- proportions (7.75 heads, ~1.80 m tall adult, Vanguard = broad/athletic) ----
HEAD_H = 1.80 / 7.75   # ~0.232 m

# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------
def _unlink_from_active(obj):
    for col in list(obj.users_collection):
        col.objects.unlink(obj)

def move_to_collection(obj, module_name):
    parent = bpy.data.collections.get("Champion_Vanguard")
    col = None
    if parent is not None:
        col = parent.children.get(module_name)
    if col is None:
        col = bpy.data.collections.get(module_name)
    if col is None:
        col = bpy.data.collections.new(module_name)
        if parent is not None:
            parent.children.link(col)
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col.objects.link(obj)
    return col

def root_obj():
    r = bpy.data.objects.get("root")
    if r is None:
        r = bpy.data.objects.new("root", None)
        bpy.context.collection.objects.link(r)
    return r

def apply_transforms(obj, scale=True, rotation=True):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=rotation, scale=scale)

def box(center, size, name):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=center)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = Vector(size)
    apply_transforms(obj, scale=True, rotation=True)
    obj.location = Vector(center)
    return obj

def cyl(center, radius, depth, name, axis='Z', segments=CY_SEGMENTS):
    bpy.ops.mesh.primitive_cylinder_add(vertices=segments, radius=radius,
                                        depth=depth, location=center)
    obj = bpy.context.active_object
    obj.name = name
    if axis == 'Y':
        obj.rotation_euler = (math.radians(90), 0, 0)
    elif axis == 'X':
        obj.rotation_euler = (0, math.radians(90), 0)
    apply_transforms(obj, scale=True, rotation=True)
    obj.location = Vector(center)
    return obj

def join(objects, name):
    # select + join, returns the joined object
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.select_set(False)
    return obj

def finalize(obj, module_name, material_name, parent_to_root=True):
    # subdivision
    mod = obj.modifiers.new("Subdiv", 'SUBSURF')
    mod.levels = SUB_LEVEL
    mod.render_levels = SUB_LEVEL
    mod.subdivision_type = 'CATMULL_CLARK'
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier="Subdiv")
    bpy.ops.object.shade_smooth()
    # material
    mat = bpy.data.materials.get(material_name)
    if mat is None:
        mat = bpy.data.materials.new(material_name)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            color = {
                "M_Skin": (0.82, 0.68, 0.56, 1.0),
                "M_Hair": (0.07, 0.06, 0.055, 1.0),
                "M_Eye": (0.92, 0.92, 0.90, 1.0),
                "M_Placeholder_Neutral": (0.5, 0.5, 0.5, 1.0),
            }.get(material_name, (0.5, 0.5, 0.5, 1.0))
            bsdf.inputs["Base Color"].default_value = color
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)
    # collection
    move_to_collection(obj, module_name)
    # parent
    if parent_to_root:
        r = root_obj()
        obj.parent = r
        obj.matrix_parent_inverse = r.matrix_world.inverted()
    return obj

def cleanup():
    for obj in list(bpy.data.objects):
        if obj.name.startswith("SM_"):
            bpy.data.objects.remove(obj, do_unlink=True)

# ---------------------------------------------------------------------------
# geometry
# ---------------------------------------------------------------------------
def build_head():
    parts = []
    # skull (rounded box) — center at head mass
    parts.append(box((0, 0, 1.68), (0.155, 0.185, 0.22), "h_skull"))
    # jaw / lower face
    parts.append(box((0, 0.006, 1.585), (0.105, 0.145, 0.095), "h_jaw"))
    # neck
    parts.append(cyl((0, 0, 1.535), 0.055, 0.10, "h_neck", axis='Z'))
    # ears
    parts.append(box((-0.078, 0, 1.66), (0.012, 0.040, 0.045), "h_ear_l"))
    parts.append(box((0.078, 0, 1.66), (0.012, 0.040, 0.045), "h_ear_r"))
    o = join(parts, "SM_Head")
    return finalize(o, "head", "M_Skin")

def build_hair():
    parts = []
    # short dark hair cap over skull top/back/sides
    parts.append(box((0, 0.010, 1.71), (0.175, 0.195, 0.17), "hair_cap"))
    # fringe over forehead (front, slight -Y)
    parts.append(box((0, -0.055, 1.735), (0.145, 0.045, 0.05), "hair_fringe"))
    o = join(parts, "SM_Hair")
    return finalize(o, "hair", "M_Hair")

def build_face():
    parts = []
    # nose
    parts.append(box((0, -0.095, 1.625), (0.022, 0.030, 0.048), "f_nose"))
    # mouth
    parts.append(box((0, -0.092, 1.575), (0.048, 0.008, 0.012), "f_mouth"))
    # brows
    parts.append(box((-0.033, -0.090, 1.672), (0.030, 0.010, 0.010), "f_brow_l"))
    parts.append(box((0.033, -0.090, 1.672), (0.030, 0.010, 0.010), "f_brow_r"))
    o = join(parts, "SM_Face")
    return finalize(o, "face", "M_Skin")

def build_eyes():
    eyes = []
    for side, sx in (("L", -1), ("R", 1)):
        e = box((sx * 0.033, -0.082, 1.645), (0.022, 0.022, 0.017), "e_%s" % side)
        e = finalize(e, "face", "M_Eye")
        e.name = "SM_Eye_%s" % side
        eyes.append(e)
    return eyes

def build_torso():
    parts = []
    # ribcage / chest (broad)
    parts.append(box((0, 0, 1.32), (0.365, 0.26, 0.30), "t_chest"))
    # abdomen / waist
    parts.append(box((0, 0, 1.13), (0.27, 0.20, 0.17), "t_waist"))
    # pelvis / hips
    parts.append(box((0, 0, 0.975), (0.335, 0.24, 0.145), "t_pelvis"))
    o = join(parts, "SM_Torso")
    return finalize(o, "torso", "M_Skin")

def build_arm(side, sx):
    parts = []
    # upper arm (shoulder -> elbow)
    parts.append(cyl((sx * 0.245, 0, 1.33), 0.046, 0.28, "a_upper", axis='Z'))
    # elbow sphere-ish
    parts.append(box((sx * 0.255, 0, 1.19), (0.048, 0.048, 0.048), "a_elbow"))
    # forearm (elbow -> wrist)
    parts.append(cyl((sx * 0.26, 0, 0.95), 0.035, 0.34, "a_fore", axis='Z'))
    # hand
    parts.append(box((sx * 0.265, -0.02, 0.72), (0.045, 0.075, 0.085), "a_hand"))
    o = join(parts, "SM_Arm_%s" % side)
    return finalize(o, "arms", "M_Skin")

def build_leg(side, sx):
    parts = []
    # thigh (hip -> knee)
    parts.append(cyl((sx * 0.095, 0, 0.72), 0.066, 0.42, "l_thigh", axis='Z'))
    # knee
    parts.append(box((sx * 0.095, 0.005, 0.505), (0.07, 0.07, 0.06), "l_knee"))
    # calf (knee -> ankle)
    parts.append(cyl((sx * 0.095, 0, 0.29), 0.050, 0.36, "l_calf", axis='Z'))
    # foot (extends forward -Y, sole on ground Z=0)
    parts.append(box((sx * 0.095, -0.045, 0.025), (0.055, 0.135, 0.05), "l_foot"))
    o = join(parts, "SM_Leg_%s" % side)
    return finalize(o, "legs", "M_Skin")

# ---------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------
cleanup()
root = root_obj()

print("=== BUILDING BODY MODULES ===")
build_head()
build_hair()
build_face()
build_eyes()
build_torso()
build_arm("L", -1)
build_arm("R", 1)
build_leg("L", -1)
build_leg("R", 1)

# ---- report ----
def tri_count(obj):
    return sum(max(len(p.vertices) - 2, 0) for p in obj.data.polygons)

print("=== MODULE REPORT ===")
total = 0
for obj in sorted(bpy.data.objects, key=lambda o: o.name):
    if obj.name.startswith("SM_"):
        cols = [c.name for c in obj.users_collection]
        t = tri_count(obj)
        total += t
        print("  %-12s tris=%6d  loc=(%.3f, %.3f, %.3f)  cols=%s"
              % (obj.name, t, obj.location.x, obj.location.y, obj.location.z, cols))
print("  TOTAL body triangles: %d" % total)

# ---- orientation / pivot verification ----
print("=== ORIENTATION / PIVOT ===")
r = root_obj()
print("  root location: %s" % (tuple(round(v, 4) for v in r.location),))
fwd = bpy.data.objects.get("FORWARD_Unity+Z")
if fwd is not None:
    print("  FORWARD_Unity+Z empty: loc=%s (authoring marker, +Z maps to Unity forward)"
          % (tuple(round(v, 4) for v in fwd.location),))

# save
bpy.ops.wm.save_mainfile()
print("=== SAVED ===")
