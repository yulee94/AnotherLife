"""
Headless verification of the Champion Vanguard LOD+rig candidate.
Re-opens the candidate .blend and asserts every acceptance criterion.
Prints a PASS/FAIL report; exits non-zero on any FAIL.
"""
import os
import sys
import bpy
from mathutils import Vector

BLEND = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_eaaabf32\unity\ArtSource\Champions\champion_vanguard_working_v001.blend"

FAILS = []


def check(cond, msg):
    status = "PASS" if cond else "FAIL"
    print(f"[{status}] {msg}")
    if not cond:
        FAILS.append(msg)


bpy.ops.wm.open_mainfile(filepath=BLEND)

cv = bpy.data.collections.get("Champion_Vanguard")
check(cv is not None, "top-level 'Champion_Vanguard' collection exists")

SLOTS = ["head", "hair", "face", "torso", "shoulders", "arms", "legs",
         "cape", "main-hand", "off-hand", "realm-ornament", "anchors"]
for s in SLOTS:
    check(bpy.data.collections.get(s) is not None, f"slot collection '{s}' exists")

for extra in ("rig", "LOD_Medium", "LOD_Low"):
    check(bpy.data.collections.get(extra) is not None, f"collection '{extra}' exists")

# LOD0 object inventory
LOD0_NAMES = [
    "SM_Head", "SM_Hair", "SM_Face", "SM_Eye_L", "SM_Eye_R", "SM_Torso",
    "SM_Arm_L", "SM_Arm_R", "SM_Leg_L", "SM_Leg_R",
    "Shoulder_L", "Shoulder_R", "Cape", "Weapon_Main", "Shield_Off", "Realm_Ornament",
]
missing = [n for n in LOD0_NAMES if n not in bpy.data.objects]
check(not missing, f"all 16 LOD0 objects present (missing: {missing})")


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


lod0 = sum(tris(bpy.data.objects[n]) for n in LOD0_NAMES if n in bpy.data.objects)
check(8000 <= lod0 <= 18000, f"LOD0 total {lod0} tris in 8k-18k")

lod1 = sum(tris(bpy.data.objects[n + "_LOD1"]) for n in LOD0_NAMES)
check(3000 <= lod1 <= 6000, f"LOD1 (medium) {lod1} tris in 3k-6k")

lod2 = sum(tris(bpy.data.objects[n + "_LOD2"]) for n in LOD0_NAMES)
check(800 <= lod2 <= 1500, f"LOD2 (low) {lod2} tris in 800-1500")

# armature
arm = bpy.data.objects.get("Champion_Vanguard_Rig")
check(arm is not None and arm.type == "ARMATURE", "armature 'Champion_Vanguard_Rig' present")
if arm is not None:
    bone_names = [b.name for b in arm.data.bones]
    required = [
        "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
        "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
        "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
        "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
        "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes",
    ]
    missing_b = [b for b in required if b not in bone_names]
    check(not missing_b, f"all 22 Humanoid bones present (missing: {missing_b})")
    check(len(arm.data.bones) < 90, f"deformation bone count {len(arm.data.bones)} < 90")

    # skinning: each LOD0 mesh has armature modifier + vertex groups with <=4 influences
    max_infl = 0
    unskinned = []
    for n in LOD0_NAMES:
        o = bpy.data.objects[n]
        has_mod = any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers)
        if not has_mod:
            unskinned.append(n)
        # count max influences per vertex via vertex group weights
        if o.type == "MESH":
            import bmesh
            bm = bmesh.new()
            bm.from_mesh(o.data)
            dl = bm.verts.layers.deform.active
            if dl is not None:
                for v in bm.verts:
                    d = v[dl]
                    if d is not None:
                        max_infl = max(max_infl, len(d.items()))
            bm.free()
    check(not unskinned, f"all LOD0 meshes skinned to armature (unskinned: {unskinned})")
    check(max_infl <= 4, f"max bone influences per vertex = {max_infl} (<= 4)")

# root / pivot / orientation / scale
root = bpy.data.objects.get("root")
check(root is not None, "'root' empty exists")
if root is not None:
    check(abs(root.location.x) + abs(root.location.y) + abs(root.location.z) < 1e-6,
          f"root at origin (loc={tuple(round(v, 4) for v in root.location)})")

# feet at ground: body bounds min Z ~ 0
zs = [bpy.data.objects[n].matrix_world @ Vector((v.co[0], v.co[1], v.co[2]))
      for n in LOD0_NAMES for v in bpy.data.objects[n].data.vertices]
zmin = min(p.z for p in zs)
zmax = max(p.z for p in zs)
check(abs(zmin) < 0.05, f"feet/ground at Z=0 (min body Z={zmin:.4f})")
check(1.70 <= zmax <= 1.95, f"height ~1.78 m (max body Z={zmax:.3f} m)")

# facing -Y: head/face features are in front of the body center (front = -Y)
head = bpy.data.objects.get("SM_Head")
if head is not None:
    # face (SM_Face) sits forward of the head center -> -Y
    face = bpy.data.objects.get("SM_Face")
    check(face is not None and face.location.y < head.location.y,
          "character faces -Y (face forward of head)")

# anchors reconciled (parented to root, at expected Blender positions)
ANCHOR_EXPECT = {
    "VFX_ChestAnchor": (0.00, -0.24, 1.34),
    "VFX_Hand_L":      (-0.34, -0.12, 0.95),
    "VFX_Hand_R":      (0.30, -0.05, 0.92),
    "PetAnchor":       (-0.95, 0.20, 0.62),
    "MountAnchor":     (0.00, 0.00, 0.24),
}
for name, expect in ANCHOR_EXPECT.items():
    a = bpy.data.objects.get(name)
    if a is None:
        check(False, f"anchor '{name}' exists")
        continue
    ok_pos = all(abs(a.location[i] - expect[i]) < 0.02 for i in range(3))
    ok_parent = a.parent == root
    check(ok_pos and ok_parent,
          f"anchor '{name}' reconciled pos={tuple(round(v, 3) for v in a.location)} parented_to_root={ok_parent}")

# modular separation preserved in LODs (one object per module, body vs equipment)
equip_lod1 = all((n + "_LOD1") in bpy.data.objects for n in
                 ["Shoulder_L", "Cape", "Weapon_Main", "Shield_Off", "Realm_Ornament"])
check(equip_lod1, "LOD1 preserves equipment modular separation")

# units
sc = bpy.context.scene.unit_settings
check(sc.system == "METRIC" and abs(sc.scale_length - 1.0) < 1e-6,
      f"units metric / scale 1.0 (system={sc.system})")

print("\n" + "=" * 60)
if FAILS:
    print(f"RESULT: FAIL ({len(FAILS)} failures)")
    for f in FAILS:
        print("  -", f)
    sys.exit(1)
else:
    print("RESULT: ALL CHECKS PASSED")
    sys.exit(0)
