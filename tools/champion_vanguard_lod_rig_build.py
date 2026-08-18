"""
Merge the Crownlands Champion Vanguard body + equipment modules into one candidate
Blender file, generate medium (LOD1) and low (LOD2) triangle variants while preserving
modular separation, add a Unity Humanoid-compatible armature with distance-weighted
skinning, reconcile the scaffold anchor empties into the Blender Z-up frame, and save
the deliverable.

Inputs (read-only, authored by upstream tasks):
  BODY_FILE    — body working file (10 SM_* meshes, 9600 tris)
  EQUIP_FILE   — equipment working file (6 equipment meshes, 1786 tris)

Output:
  OUT_FILE     — merged candidate: LOD0 (11 slot collections) + LOD_Medium + LOD_Low
                 + rig (22-bone Unity Humanoid) + reconciled anchors.

Frame (per spec):
  Blender Z-up, character faces -Y (= Unity +Z after FBX), root at (0,0,0),
  feet at Z=0, ~1.78 m (7.75 heads). Units metric / 1.0.

This script is a build record, not idempotent in place: it re-appends equipment from
EQUIP_FILE into a fresh open of BODY_FILE. Re-run only against the two source files.
"""
import os
import bpy
import bmesh
from mathutils import Vector

# ---------------------------------------------------------------------------
# paths
# ---------------------------------------------------------------------------
BODY_FILE = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_b15d7768\unity\ArtSource\Champions\champion_vanguard_working_v001.blend"
EQUIP_FILE = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_ac5067e9\unity\ArtSource\Champions\champion_vanguard_working_v001.blend"
OUT_FILE = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_eaaabf32\unity\ArtSource\Champions\champion_vanguard_working_v001.blend"

# ---------------------------------------------------------------------------
# 1. open body file as the merge base
# ---------------------------------------------------------------------------
bpy.ops.wm.open_mainfile(filepath=BODY_FILE)

# ---------------------------------------------------------------------------
# 2. append equipment objects and re-home them under the shared 'root'
# ---------------------------------------------------------------------------
EQUIP_NAMES = ("Shoulder_L", "Shoulder_R", "Cape", "Weapon_Main",
               "Shield_Off", "Realm_Ornament")
EQUIP_SLOT = {
    "Shoulder_L": "shoulders", "Shoulder_R": "shoulders",
    "Cape": "cape", "Weapon_Main": "main-hand",
    "Shield_Off": "off-hand", "Realm_Ornament": "realm-ornament",
}

for name in EQUIP_NAMES:
    bpy.ops.wm.append(filepath=EQUIP_FILE + "/Object/" + name,
                      directory=EQUIP_FILE + "/Object/", filename=name)

root = bpy.data.objects["root"]

for name in EQUIP_NAMES:
    o = bpy.data.objects[name]
    # unlink from whatever collection append dropped it into (Scene Collection)
    for c in list(o.users_collection):
        c.objects.unlink(o)
    # link into its canonical slot collection
    bpy.data.collections[EQUIP_SLOT[name]].objects.link(o)
    # re-parent to the shared root (append created a duplicate 'root.001')
    o.parent = root

# remove any duplicate root empties introduced by append
for oname in list(bpy.data.objects.keys()):
    if oname.startswith("root.") and bpy.data.objects[oname].type == "EMPTY":
        bpy.data.objects.remove(bpy.data.objects[oname], do_unlink=True)

# ---------------------------------------------------------------------------
# 3. reconcile anchor empties (Unity Y-up placeholder frame -> Blender Z-up)
# ---------------------------------------------------------------------------
# The scaffold anchors carried Unity Y-up coordinates copied verbatim from
# ProceduralChampionModelBuilder.cs (root-at-center placeholder). We re-home them
# to anatomically correct positions on this 1.78 m Blender model, then parent to root.
# Unity (x, y_up, z_fwd) -> Blender (x, y=-z, z=y); then hand-placed to model anatomy.
ANCHOR_POS = {
    "VFX_ChestAnchor": (0.00, -0.24, 1.34),   # chest front (Realm_Ornament emblem)
    "VFX_Hand_L":      (-0.34, -0.12, 0.95),  # off-hand (Shield_Off grip)
    "VFX_Hand_R":      (0.30, -0.05, 0.92),   # main hand (Weapon_Main grip)
    "PetAnchor":       (-0.95, 0.20, 0.62),   # side-rear pet follow point
    "MountAnchor":     (0.00, 0.00, 0.24),    # center, mount saddle reference
}
for name, pos in ANCHOR_POS.items():
    a = bpy.data.objects.get(name)
    if a is None:
        continue
    a.location = pos
    a.parent = root

# ---------------------------------------------------------------------------
# 4. build the Unity Humanoid armature (22 bones, standard Mecanim names)
# ---------------------------------------------------------------------------
BONE_SPEC = [
    # (name, parent, head, tail)
    ("Hips",         None,         (0.0,    0.0, 0.93), (0.0,    0.0, 1.04)),
    ("Spine",        "Hips",       (0.0,    0.0, 1.04), (0.0,    0.0, 1.20)),
    ("Chest",        "Spine",      (0.0,    0.0, 1.20), (0.0,    0.0, 1.34)),
    ("UpperChest",   "Chest",      (0.0,    0.0, 1.34), (0.0,    0.0, 1.42)),
    ("Neck",         "UpperChest", (0.0,    0.0, 1.42), (0.0,    0.0, 1.50)),
    ("Head",         "Neck",       (0.0,    0.0, 1.50), (0.0,    0.0, 1.74)),

    ("LeftShoulder", "UpperChest", (0.03,   0.0, 1.42), (-0.245, 0.0, 1.42)),
    ("LeftUpperArm", "LeftShoulder", (-0.245, 0.0, 1.40), (-0.245, 0.0, 1.05)),
    ("LeftLowerArm", "LeftUpperArm", (-0.245, 0.0, 1.05), (-0.245, 0.0, 0.80)),
    ("LeftHand",     "LeftLowerArm", (-0.245, 0.0, 0.80), (-0.245, 0.0, 0.70)),
    ("RightShoulder", "UpperChest", (0.03,   0.0, 1.42), (0.245,  0.0, 1.42)),
    ("RightUpperArm", "RightShoulder", (0.245, 0.0, 1.40), (0.245, 0.0, 1.05)),
    ("RightLowerArm", "RightUpperArm", (0.245, 0.0, 1.05), (0.245, 0.0, 0.80)),
    ("RightHand",     "RightLowerArm", (0.245, 0.0, 0.80), (0.245, 0.0, 0.70)),

    ("LeftUpperLeg",  "Hips",       (-0.095, 0.0, 0.93), (-0.095, 0.0, 0.47)),
    ("LeftLowerLeg",  "LeftUpperLeg", (-0.095, 0.0, 0.47), (-0.095, 0.0, 0.09)),
    ("LeftFoot",      "LeftLowerLeg", (-0.095, 0.0, 0.09), (-0.095, -0.18, 0.04)),
    ("LeftToes",      "LeftFoot",   (-0.095, -0.18, 0.04), (-0.095, -0.24, 0.03)),
    ("RightUpperLeg", "Hips",       (0.095,  0.0, 0.93), (0.095,  0.0, 0.47)),
    ("RightLowerLeg", "RightUpperLeg", (0.095, 0.0, 0.47), (0.095, 0.0, 0.09)),
    ("RightFoot",     "RightLowerLeg", (0.095, 0.0, 0.09), (0.095, -0.18, 0.04)),
    ("RightToes",     "RightFoot",  (0.095,  -0.18, 0.04), (0.095, -0.24, 0.03)),
]

arm_data = bpy.data.armatures.new("Champion_Vanguard_Rig")
arm_obj = bpy.data.objects.new("Champion_Vanguard_Rig", arm_data)
bpy.context.collection.objects.link(arm_obj)
arm_obj.parent = root

bpy.context.view_layer.objects.active = arm_obj
bpy.ops.object.mode_set(mode="EDIT")
ebones = arm_data.edit_bones
for name, parent, head, tail in BONE_SPEC:
    eb = ebones.new(name)
    eb.head = head
    eb.tail = tail
    if parent:
        eb.parent = ebones[parent]
bpy.ops.object.mode_set(mode="OBJECT")

# ---------------------------------------------------------------------------
# 5. distance-weighted skinning (<= 4 influences / vertex, deterministic)
# ---------------------------------------------------------------------------
def point_segment_dist(p, a, b):
    ab = b - a
    l2 = ab.length_squared
    if l2 < 1e-12:
        return (p - a).length
    t = (p - a).dot(ab) / l2
    t = max(0.0, min(1.0, t))
    return (p - (a + t * ab)).length


def skin_mesh(obj, arm, max_influences=4, falloff=2.0):
    # bone segments in world space
    segs = []
    for bone in arm.data.bones:
        hw = arm.matrix_world @ bone.head_local
        tw = arm.matrix_world @ bone.tail_local
        segs.append((bone.name, Vector(hw), Vector(tw)))

    # clear existing groups, pre-create one per bone
    for vg in list(obj.vertex_groups):
        obj.vertex_groups.remove(vg)
    groups = {name: obj.vertex_groups.new(name=name) for name, _, _ in segs}

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    deformer = bm.verts.layers.deform.verify()

    mw = obj.matrix_world
    eps = 0.005
    for v in bm.verts:
        wpos = mw @ v.co
        dists = []
        for name, h, t in segs:
            d = point_segment_dist(wpos, h, t)
            dists.append((d, name))
        dists.sort(key=lambda x: x[0])
        top = dists[:max_influences]
        wsum = 0.0
        weights = []
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


# body + equipment meshes (all 16 LOD0 objects)
MESH_NAMES = [
    "SM_Head", "SM_Hair", "SM_Face", "SM_Eye_L", "SM_Eye_R", "SM_Torso",
    "SM_Arm_L", "SM_Arm_R", "SM_Leg_L", "SM_Leg_R",
    "Shoulder_L", "Shoulder_R", "Cape", "Weapon_Main", "Shield_Off", "Realm_Ornament",
]

for name in MESH_NAMES:
    o = bpy.data.objects[name]
    skin_mesh(o, arm_obj)
    # bind: Armature modifier + parent to armature (parent_type OBJECT)
    mod = o.modifiers.new("Armature", "ARMATURE")
    mod.object = arm_obj
    o.parent = arm_obj

# ---------------------------------------------------------------------------
# 6. generate LOD1 / LOD2 (collapse decimation preserves vertex groups = skinning)
# ---------------------------------------------------------------------------
LOD0_TOTAL = sum(
    sum(len(p.vertices) - 2 for p in bpy.data.objects[n].data.polygons)
    for n in MESH_NAMES
)


def build_lod(level_name, target_tris):
    """Duplicate every LOD0 mesh, collapse-decimate to hit target_tris total."""
    lo, hi = (3000, 6000) if level_name == "LOD_Medium" else (800, 1500)
    ratio = max(0.01, min(1.0, target_tris / LOD0_TOTAL))
    coll = bpy.data.collections.new(level_name)
    bpy.data.collections["Champion_Vanguard"].children.link(coll)

    # do up to 3 passes to land inside budget
    for _ in range(3):
        # delete any prior attempt objects in this pass
        for oname in list(bpy.data.objects.keys()):
            o = bpy.data.objects[oname]
            if level_name in {c.name for c in o.users_collection}:
                bpy.data.objects.remove(o, do_unlink=True)

        for name in MESH_NAMES:
            src = bpy.data.objects[name]
            # duplicate the skinned LOD0 mesh (inherits parent/modifier/groups)
            bpy.ops.object.select_all(action="DESELECT")
            src.select_set(True)
            bpy.context.view_layer.objects.active = src
            bpy.ops.object.duplicate()
            dup = bpy.context.active_object
            dup.name = name + ("_LOD1" if level_name == "LOD_Medium" else "_LOD2")
            # move to LOD collection
            for c in list(dup.users_collection):
                c.objects.unlink(dup)
            coll.objects.link(dup)
            # collapse decimate (keeps a subset of original weighted verts)
            dec = dup.modifiers.new("LOD_Decimate", "DECIMATE")
            dec.decimate_type = "COLLAPSE"
            dec.ratio = ratio
            bpy.context.view_layer.objects.active = dup
            bpy.ops.object.modifier_apply(modifier="LOD_Decimate")

        total = sum(
            sum(len(p.vertices) - 2 for p in bpy.data.objects[n].data.polygons)
            for n in [m + ("_LOD1" if level_name == "LOD_Medium" else "_LOD2")
                      for m in MESH_NAMES]
        )
        if lo <= total <= hi:
            break
        # correct ratio proportionally
        ratio *= target_tris / max(total, 1)
        ratio = max(0.01, min(1.0, ratio))

    print(f"{level_name}: {total} tris (budget {lo}-{hi}, ratio {ratio:.3f})")
    return total


lod1_total = build_lod("LOD_Medium", 4500)
lod2_total = build_lod("LOD_Low", 1100)

# ---------------------------------------------------------------------------
# 7. housekeeping: rig collection + save
# ---------------------------------------------------------------------------
rig_coll = bpy.data.collections.new("rig")
bpy.data.collections["Champion_Vanguard"].children.link(rig_coll)
# move armature into rig collection
for c in list(arm_obj.users_collection):
    c.objects.unlink(arm_obj)
rig_coll.objects.link(arm_obj)

# ensure units are metric / 1.0
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0

os.makedirs(os.path.dirname(OUT_FILE), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT_FILE)

print("\n=== BUILD COMPLETE ===")
print("LOD0 total:", LOD0_TOTAL, "tris")
print("LOD1 total:", lod1_total, "tris")
print("LOD2 total:", lod2_total, "tris")
print("Bones:", len(arm_data.bones))
print("Saved:", OUT_FILE)
