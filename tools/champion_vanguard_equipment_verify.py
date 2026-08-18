import bpy
from math import degrees

print("=== VERIFY: collections & parenting ===")
EQUIP = ("Shoulder_L", "Shoulder_R", "Cape", "Weapon_Main", "Shield_Off", "Realm_Ornament")
root = bpy.data.objects.get("root")
print("root exists:", root is not None, "| root location:", tuple(round(v,3) for v in root.location) if root else None)

for name in EQUIP:
    o = bpy.data.objects.get(name)
    if o is None:
        print(f"  {name}: MISSING")
        continue
    cols = [c.name for c in o.users_collection]
    parent = o.parent.name if o.parent else None
    loc = tuple(round(v, 3) for v in o.location)
    dims = tuple(round(v, 3) for v in o.dimensions)
    # world bbox
    bb = [o.matrix_world @ v.co for v in o.data.vertices]
    minx = min(v.x for v in bb); maxx = max(v.x for v in bb)
    miny = min(v.y for v in bb); maxy = max(v.y for v in bb)
    minz = min(v.z for v in bb); maxz = max(v.z for v in bb)
    print(f"  {name}: cols={cols} parent={parent}")
    print(f"      loc={loc} dims={dims}")
    print(f"      world bbox X[{minx:.3f},{maxx:.3f}] Y[{miny:.3f},{maxy:.3f}] Z[{minz:.3f},{maxz:.3f}]")

print("\n=== VERIFY: orientation (normals of key faces) ===")
# shield front face should face -Y (forward)
shield = bpy.data.objects.get("Shield_Off")
if shield:
    mesh = shield.data
    for p in mesh.polygons:
        n = shield.matrix_world.to_3x3() @ p.normal
        if abs(n.y) > 0.5:
            print(f"  Shield face normal: ({n.x:.2f},{n.y:.2f},{n.z:.2f})")
            break
orn = bpy.data.objects.get("Realm_Ornament")
if orn:
    for p in orn.data.polygons:
        n = orn.matrix_world.to_3x3() @ p.normal
        if abs(n.y) > 0.5:
            print(f"  Ornament face normal: ({n.x:.2f},{n.y:.2f},{n.z:.2f})")
            break

print("\n=== VERIFY: units & orientation markers ===")
s = bpy.context.scene
print("unit_system:", s.unit_settings.system, "| scale:", s.unit_settings.scale_length)
fwd = bpy.data.objects.get("FORWARD_Unity+Z")
print("FORWARD_Unity+Z location:", tuple(round(v,3) for v in fwd.location) if fwd else "missing")

print("\n=== VERIFY: material assignment ===")
for name in EQUIP:
    o = bpy.data.objects.get(name)
    if o and o.data.materials:
        print(f"  {name}: {[m.name for m in o.data.materials]}")

print("\n=== VERIFY: collections present ===")
for c in ("shoulders","cape","main-hand","off-hand","realm-ornament"):
    coll = bpy.data.collections.get(c)
    print(f"  {c}: {len(coll.objects) if coll else 'MISSING'} objects")
