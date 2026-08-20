"""Verify the body-module build in the saved working file (read-only)."""
import bpy

print("=== VERIFY: collections ===")
parent = bpy.data.collections.get("Champion_Vanguard")
if parent:
    print("Champion_Vanguard children:", sorted(c.name for c in parent.children))

print("\n=== VERIFY: body modules ===")
total = 0
total_quad = 0
total_tri = 0
total_ngon = 0
for obj in sorted(bpy.data.objects, key=lambda o: o.name):
    if not obj.name.startswith("SM_"):
        continue
    n_quad = sum(1 for p in obj.data.polygons if len(p.vertices) == 4)
    n_tri = sum(1 for p in obj.data.polygons if len(p.vertices) == 3)
    n_ngon = sum(1 for p in obj.data.polygons if len(p.vertices) > 4)
    tris = sum(max(len(p.vertices) - 2, 0) for p in obj.data.polygons)
    total += tris
    total_quad += n_quad
    total_tri += n_tri
    total_ngon += n_ngon
    cols = [c.name for c in obj.users_collection]
    parent_name = obj.parent.name if obj.parent else "-"
    loc = obj.location
    print("  %-12s tris=%5d quads=%4d tris_faces=%4d ngons=%3d parent=%s cols=%s loc=(%.2f,%.2f,%.2f)"
          % (obj.name, tris, n_quad, n_tri, n_ngon, parent_name, cols, loc.x, loc.y, loc.z))

print("\n  TOTAL tris=%d | quad faces=%d | tri faces=%d | ngon faces=%d" % (total, total_quad, total_tri, total_ngon))
print("  quad-dominant: %s" % ("YES" if total_quad > (total_tri + total_ngon) else "NO"))

print("\n=== VERIFY: bounds (world) ===")
import mathutils
minx = miny = minz = 1e9
maxx = maxy = maxz = -1e9
for obj in bpy.data.objects:
    if not obj.name.startswith("SM_"):
        continue
    for v in obj.data.vertices:
        w = obj.matrix_world @ v.co
        minx = min(minx, w.x); maxx = max(maxx, w.x)
        miny = min(miny, w.y); maxy = max(maxy, w.y)
        minz = min(minz, w.z); maxz = max(maxz, w.z)
print("  X [%.3f .. %.3f] width=%.3f" % (minx, maxx, maxx - minx))
print("  Y [%.3f .. %.3f] depth=%.3f  (front=-Y)" % (miny, maxy, maxy - miny))
print("  Z [%.3f .. %.3f] height=%.3f  (ground Z=0)" % (minz, maxz, maxz - minz))

print("\n=== VERIFY: root & markers ===")
r = bpy.data.objects.get("root")
if r:
    print("  root at %s, type=%s" % (tuple(round(v, 4) for v in r.location), r.type))
fwd = bpy.data.objects.get("FORWARD_Unity+Z")
if fwd:
    print("  FORWARD_Unity+Z at %s" % (tuple(round(v, 4) for v in fwd.location),))
