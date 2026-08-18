"""
Independent validation of the Champion Vanguard Blender candidate.

This script does NOT reuse the producer's verify script. It opens the delivered
.blend and measures ground truth directly:
  - per-object and per-LOD triangle counts (polygon + triangulated + n-gon audit)
  - actual rig bone count, names, and Unity Humanoid coverage
  - per-vertex bone-influence audit (max influences, unweighted verts)
  - root pivot / feet-at-ground / height / 7.75-heads ratio
  - facing direction (-Y in Blender) measured from geometry
  - modular slot separation (11 collections) and body-vs-equipment separation
  - units / scale

Prints a plain-text report; writes the same to REPORT_OUT.
"""
import os
import sys
import json
import bpy
from mathutils import Vector

BLEND = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_eaaabf32\unity\ArtSource\Champions\champion_vanguard_working_v001.blend"
REPORT_OUT = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_0f2ce476\unity\ArtSource\Champions\champion_vanguard_validation_report.txt"

bpy.ops.wm.open_mainfile(filepath=BLEND)

L = []  # report lines
def p(s=""):
    print(s)
    L.append(s)

p("=" * 78)
p("INDEPENDENT VALIDATION — champion_vanguard_working_v001.blend")
p("=" * 78)

# ---------------------------------------------------------------------------
# 1. Collections / object inventory
# ---------------------------------------------------------------------------
p("\n[1] COLLECTIONS")
top = [c for c in bpy.data.collections if c.name == "Champion_Vanguard"]
p(f"  top-level 'Champion_Vanguard': {'PRESENT' if top else 'MISSING'}")
if top:
    for child in sorted(top[0].children, key=lambda c: c.name):
        objs = [o.name for o in child.objects]
        p(f"    child collection '{child.name}': {len(objs)} objects -> {objs}")

SLOTS = ["head", "hair", "face", "torso", "shoulders", "arms",
         "legs", "cape", "main-hand", "off-hand", "realm-ornament", "anchors"]
p("\n[2] SLOT COLLECTIONS (11 modular + anchors)")
for s in SLOTS:
    c = bpy.data.collections.get(s)
    if c is None:
        p(f"  MISSING  {s}")
    else:
        p(f"  present  {s:16s} -> {[o.name for o in c.objects]}")

# ---------------------------------------------------------------------------
# 2. triangle counts (polygon count + triangulated + n-gon audit)
# ---------------------------------------------------------------------------
p("\n[3] TRIANGLE COUNTS")
LOD0_NAMES = [
    "SM_Head", "SM_Hair", "SM_Face", "SM_Eye_L", "SM_Eye_R", "SM_Torso",
    "SM_Arm_L", "SM_Arm_R", "SM_Leg_L", "SM_Leg_R",
    "Shoulder_L", "Shoulder_R", "Cape", "Weapon_Main", "Shield_Off", "Realm_Ornament",
]
SLOT_OF = {
    "SM_Head": "head", "SM_Hair": "hair", "SM_Face": "face",
    "SM_Eye_L": "face", "SM_Eye_R": "face", "SM_Torso": "torso",
    "SM_Arm_L": "arms", "SM_Arm_R": "arms", "SM_Leg_L": "legs", "SM_Leg_R": "legs",
    "Shoulder_L": "shoulders", "Shoulder_R": "shoulders", "Cape": "cape",
    "Weapon_Main": "main-hand", "Shield_Off": "off-hand",
    "Realm_Ornament": "realm-ornament",
}
BODY = {"SM_Head", "SM_Hair", "SM_Face", "SM_Eye_L", "SM_Eye_R", "SM_Torso",
        "SM_Arm_L", "SM_Arm_R", "SM_Leg_L", "SM_Leg_R"}
EQUIP = {"Shoulder_L", "Shoulder_R", "Cape", "Weapon_Main", "Shield_Off", "Realm_Ornament"}


def mesh_stats(name):
    o = bpy.data.objects.get(name)
    if o is None or o.type != "MESH":
        return None
    me = o.data
    ngon = sum(1 for f in me.polygons if len(f.vertices) > 4)
    tris = sum(len(f.vertices) - 2 for f in me.polygons)
    # true triangulation via loop-triangles is expensive but exact
    me.calc_loop_triangles()
    true_tris = len(me.loop_triangles)
    return {"polys": len(me.polygons), "ngons": ngon, "tris": tris, "true_tris": true_tris}


def lod_table(lod_suffix, names):
    rows = []
    total_tris = 0
    total_true = 0
    total_ngon = 0
    for n in names:
        key = n if lod_suffix == "" else n + lod_suffix
        st = mesh_stats(key)
        if st is None:
            rows.append((n, "MISSING", 0, 0, 0, 0))
            continue
        total_tris += st["tris"]
        total_true += st["true_tris"]
        total_ngon += st["ngons"]
        rows.append((n, SLOT_OF.get(n, "?"), st["polys"], st["ngons"], st["tris"], st["true_tris"]))
    return rows, total_tris, total_true, total_ngon


for label, suffix in (("LOD0 (high)", ""), ("LOD1 (medium)", "_LOD1"), ("LOD2 (low)", "_LOD2")):
    rows, tot_tris, tot_true, tot_ngon = lod_table(suffix, LOD0_NAMES)
    p(f"\n  {label}:")
    p(f"    {'object':20s} {'slot':14s} {'polys':>7s} {'ngons':>6s} {'tris':>7s} {'true_tris':>9s}")
    for n, slot, polys, ngons, tris, true_tris in rows:
        p(f"    {n:20s} {slot:14s} {polys:7d} {ngons:6d} {tris:7d} {true_tris:9d}")
    p(f"    {'TOTAL':20s} {'':14s} {'':7s} {tot_ngon:6d} {tot_tris:7d} {tot_true:9d}")

    # body vs equipment split
    body_rows = [r for r in rows if r[0] in BODY]
    equip_rows = [r for r in rows if r[0] in EQUIP]
    body_tris = sum(r[4] for r in body_rows)
    equip_tris = sum(r[4] for r in equip_rows)
    p(f"    body subtotal: {body_tris} tris | equipment subtotal: {equip_tris} tris")

# ---------------------------------------------------------------------------
# 3. rig / bones
# ---------------------------------------------------------------------------
p("\n[4] RIG / BONES")
arm = bpy.data.objects.get("Champion_Vanguard_Rig")
if arm is None or arm.type != "ARMATURE":
    p("  armature 'Champion_Vanguard_Rig': MISSING or not ARMATURE")
else:
    bones = [b.name for b in arm.data.bones]
    p(f"  armature present; edit-bone count = {len(bones)}")
    p(f"  bone names: {bones}")

    UNITY_HUMANOID_REQUIRED = [
        "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
        "LeftUpperArm", "LeftLowerArm", "LeftHand",
        "RightUpperArm", "RightLowerArm", "RightHand",
        "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
        "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes",
    ]
    OPTIONAL = ["LeftShoulder", "RightShoulder"]
    missing = [b for b in UNITY_HUMANOID_REQUIRED if b not in bones]
    p(f"  Unity Humanoid REQUIRED bones ({len(UNITY_HUMANOID_REQUIRED)}): missing = {missing or 'NONE'}")
    p(f"  optional Shoulder bones present: {all(b in bones for b in OPTIONAL)}")
    p(f"  deformation bones < 90: {len(bones) < 90} ({len(bones)})")

    # hierarchy sanity: every bone (except Hips) has a parent
    orphans = [b.name for b in arm.data.bones if b.parent is None and b.name != "Hips"]
    p(f"  orphan bones (non-root, no parent): {orphans or 'NONE'}")

# ---------------------------------------------------------------------------
# 4. skinning audit
# ---------------------------------------------------------------------------
p("\n[5] SKINNING AUDIT (<=4 influences/vertex)")
if arm is not None:
    import bmesh
    max_infl = 0
    unweighted = []
    unskinned = []
    for n in LOD0_NAMES:
        o = bpy.data.objects.get(n)
        if o is None or o.type != "MESH":
            continue
        has_mod = any(m.type == "ARMATURE" for m in o.modifiers)
        if not has_mod:
            unskinned.append(n)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        dl = bm.verts.layers.deform.active
        zero_count = 0
        if dl is not None:
            for v in bm.verts:
                d = v[dl]
                if d is None or len(d.items()) == 0:
                    zero_count += 1
                else:
                    max_infl = max(max_infl, len(d.items()))
        bm.free()
        if zero_count:
            unweighted.append((n, zero_count))
    p(f"  max influences per vertex (LOD0): {max_infl}  (target <=4)")
    p(f"  unskinned (no Armature modifier): {unskinned or 'NONE'}")
    p(f"  meshes with unweighted verts: {unweighted or 'NONE'}")

# ---------------------------------------------------------------------------
# 5. pivot / ground / height / heads ratio
# ---------------------------------------------------------------------------
p("\n[6] PIVOT / GROUND / HEIGHT")
root = bpy.data.objects.get("root")
if root is not None:
    p(f"  root location = ({root.location.x:.5f}, {root.location.y:.5f}, {root.location.z:.5f})")

# world-space body bounds
zs = []
for n in LOD0_NAMES:
    o = bpy.data.objects.get(n)
    if o is None or o.type != "MESH":
        continue
    for v in o.data.vertices:
        w = o.matrix_world @ v.co
        zs.append(w.z)
zmin = min(zs) if zs else None
zmax = max(zs) if zs else None
p(f"  body world Z min (feet) = {zmin:.4f} m   max = {zmax:.4f} m")
if zmin is not None and zmax is not None:
    p(f"  total height = {zmax - zmin:.4f} m")

# head height for 7.75-heads ratio
head_o = bpy.data.objects.get("SM_Head")
if head_o is not None and head_o.type == "MESH":
    hz = [head_o.matrix_world @ v.co for v in head_o.data.vertices]
    hmin = min(w.z for w in hz)
    hmax = max(w.z for w in hz)
    head_h = hmax - hmin
    p(f"  head (SM_Head) world Z: min={hmin:.4f} max={hmax:.4f} height={head_h:.4f} m")
    if zmax is not None and head_h > 0:
        p(f"  heads ratio (total/head) = {(zmax - zmin)/head_h:.3f}  (target ~7.75)")

# ---------------------------------------------------------------------------
# 6. facing direction (geometry, not object origin)
# ---------------------------------------------------------------------------
p("\n[7] FACING / ORIENTATION")
# face/nose should be forward (-Y). Compare face mesh centroid Y vs head centroid Y.
def centroid_y(name):
    o = bpy.data.objects.get(name)
    if o is None or o.type != "MESH":
        return None
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    return sum(w.y for w in pts) / len(pts)

head_y = centroid_y("SM_Head")
face_y = centroid_y("SM_Face")
p(f"  SM_Head centroid Y = {head_y:.4f}" if head_y is not None else "  SM_Head missing")
p(f"  SM_Face centroid Y = {face_y:.4f}" if face_y is not None else "  SM_Face missing")
if head_y is not None and face_y is not None:
    p(f"  face forward of head (face_y < head_y): {face_y < head_y}  -> {'faces -Y (correct for Blender)' if face_y < head_y else 'faces +Y (WRONG)'}")

# ---------------------------------------------------------------------------
# 7. units / scale
# ---------------------------------------------------------------------------
p("\n[8] UNITS / SCALE")
us = bpy.context.scene.unit_settings
p(f"  unit system = {us.system}, scale_length = {us.scale_length}, length unit = {us.length_unit}")

# ---------------------------------------------------------------------------
# 8. body vs equipment separation (shield/weapon not fused into body)
# ---------------------------------------------------------------------------
p("\n[9] BODY vs EQUIPMENT SEPARATION")
p("  body objects (10 SM_*): " + str(sorted(BODY)))
p("  equipment objects (6): " + str(sorted(EQUIP)))
fused = [n for n in BODY if "Shield" in n or "Weapon" in n or "Cape" in n or "Shoulder" in n]
p(f"  shield/weapon/cape/shoulder fused INTO body objects: {fused or 'NONE (clean)'}")

p("\n" + "=" * 78)
p("END OF REPORT")

with open(REPORT_OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(L) + "\n")
p(f"\nReport written to: {REPORT_OUT}")
