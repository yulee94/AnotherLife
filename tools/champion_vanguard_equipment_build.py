"""
Model Crownlands Champion Vanguard equipment modules into the shared working file.

Equipment: shoulders (Shoulder_L/R), cape (Cape), main-hand longsword (Weapon_Main),
off-hand kite shield (Shield_Off), realm-ornament (Realm_Ornament).

Frame: Blender Z-up, character faces -Y (Unity +Z forward after FBX), feet at Z=0,
~1.80 m athletic adult (7.75 heads). Each module is a separate object, parented to
the shared 'root', assigned to its slot collection, with neutral placeholder materials.
"""
import bpy
import bmesh
from math import pi, cos, sin

# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------

def mat(name, color, metallic=0.0, roughness=0.6):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    return m

MAT_STEEL   = mat("M_Steel",   (0.60, 0.62, 0.66), metallic=0.90, roughness=0.35)
MAT_CLOTH   = mat("M_Cloth",   (0.10, 0.16, 0.38), metallic=0.00, roughness=0.82)
MAT_LEATHER = mat("M_Leather", (0.20, 0.13, 0.09), metallic=0.00, roughness=0.85)
MAT_GOLD    = mat("M_Gold",    (0.72, 0.60, 0.28), metallic=0.85, roughness=0.30)
MAT_ACCENT  = mat("M_Accent",  (0.28, 0.34, 0.72), metallic=0.25, roughness=0.40)

def assign(obj, material):
    if obj.data.materials:
        obj.data.materials[0] = material
    else:
        obj.data.materials.append(material)

def smooth(obj):
    for p in obj.data.polygons:
        p.use_smooth = True

def slot_collection(name):
    coll = bpy.data.collections.get(name)
    if coll is None:
        coll = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(coll)
    return coll

def link_to(obj, coll_name, parent=None):
    coll = slot_collection(coll_name)
    for c in list(obj.users_collection):
        if c.name != coll_name:
            c.objects.unlink(obj)
    if obj.name not in coll.objects:
        coll.objects.link(obj)
    if parent is not None:
        obj.parent = parent

ROOT = bpy.data.objects.get("root")

# remove any prior equipment so the script is idempotent
EQUIP_NAMES = ("Shoulder_L", "Shoulder_R", "Cape", "Weapon_Main",
               "Shield_Off", "Realm_Ornament")
for oname in EQUIP_NAMES:
    o = bpy.data.objects.get(oname)
    if o is not None:
        bpy.data.objects.remove(o, do_unlink=True)

# ensure a valid object-mode context (scene loads in OBJECT mode already)

# ---------------------------------------------------------------------------
# Shoulders (pauldrons) — engineered plate, layered, heraldic
# ---------------------------------------------------------------------------
def build_pauldron(name):
    # main dome
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1.0,
                                         location=(0, 0, 0))
    dome = bpy.context.active_object
    dome.name = name
    dome.scale = (0.15, 0.13, 0.09)
    bpy.ops.object.transform_apply(scale=True)
    # keep upper dome only (remove lower half)
    bm = bmesh.new()
    bm.from_mesh(dome.data)
    to_del = [v for v in bm.verts if v.co.z < -0.03]
    bmesh.ops.delete(bm, geom=to_del, context="VERTS")
    bm.to_mesh(dome.data)
    bm.free()
    smooth(dome)

    # flared base plate (thin ellipsoid disc)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=8, radius=1.0,
                                         location=(0, 0, -0.01))
    base = bpy.context.active_object
    base.scale = (0.19, 0.165, 0.045)
    bpy.ops.object.transform_apply(scale=True)
    bm = bmesh.new()
    bm.from_mesh(base.data)
    to_del = [v for v in bm.verts if v.co.z < -0.045]
    bmesh.ops.delete(bm, geom=to_del, context="VERTS")
    bm.to_mesh(base.data)
    bm.free()
    smooth(base)

    # join
    bpy.ops.object.select_all(action="DESELECT")
    dome.select_set(True)
    base.select_set(True)
    bpy.context.view_layer.objects.active = dome
    bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = name
    return joined

shoulder_l = build_pauldron("Shoulder_L")
assign(shoulder_l, MAT_STEEL)
shoulder_l.location = (-0.25, 0.0, 1.42)
link_to(shoulder_l, "shoulders", ROOT)

shoulder_r = build_pauldron("Shoulder_R")
assign(shoulder_r, MAT_STEEL)
shoulder_r.location = (0.25, 0.0, 1.42)
link_to(shoulder_r, "shoulders", ROOT)

# ---------------------------------------------------------------------------
# Cape — short rigid segmented mantle (curved panel behind body)
# ---------------------------------------------------------------------------
def build_cape():
    rows, cols = 5, 9
    top_z, bot_z = 1.42, 0.90
    top_hw, bot_hw = 0.24, 0.17          # half-width
    top_y, bot_y = 0.22, 0.32            # back offset at center
    top_wrap, bot_wrap = 0.06, 0.10      # how far edges wrap forward
    thick = 0.016

    bm = bmesh.new()
    grid = []
    for r in range(rows):
        t = r / (rows - 1)
        z = top_z + (bot_z - top_z) * t
        y0 = top_y + (bot_y - top_y) * t
        hw = top_hw + (bot_hw - top_hw) * t
        wrap = top_wrap + (bot_wrap - top_wrap) * t
        row = []
        for c in range(cols):
            u = c / (cols - 1)
            x = -hw + 2 * hw * u
            edge = 1 - abs(2 * u - 1)     # 0 at edges, 1 at center
            y = y0 - (1 - edge) * wrap    # edges wrap toward the body
            row.append(bm.verts.new((x, y, z)))
        grid.append(row)

    for r in range(rows - 1):
        for c in range(cols - 1):
            a, b = grid[r][c], grid[r][c + 1]
            d, e = grid[r + 1][c], grid[r + 1][c + 1]
            bm.faces.new((a, b, e, d))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # thickness toward the back (+Y)
    ret = bmesh.ops.extrude_face_region(bm, geom=list(bm.faces))
    for v in ret["geom"]:
        if isinstance(v, bmesh.types.BMVert):
            v.co.y += thick

    me = bpy.data.meshes.new("Cape_mesh")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Cape", me)
    bpy.context.collection.objects.link(obj)
    smooth(obj)
    return obj

cape = build_cape()
assign(cape, MAT_CLOTH)
cape.location = (0, 0, 0)
link_to(cape, "cape", ROOT)

# ---------------------------------------------------------------------------
# Main-hand — straight longsword (single joined object, point-down at rest)
# ---------------------------------------------------------------------------
def build_sword():
    # Local frame: origin at grip center (hand). +Z toward pommel, -Z toward tip.
    # grip (cylinder, Z axis), centered at Z=+0.05
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.022, depth=0.18,
                                        location=(0, 0, 0.05))
    grip = bpy.context.active_object
    assign(grip, MAT_LEATHER)

    # pommel (flattened sphere) at Z=+0.17
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=0.035,
                                         location=(0, 0, 0.17))
    pommel = bpy.context.active_object
    pommel.scale = (1.0, 1.0, 0.7)
    bpy.ops.object.transform_apply(scale=True)
    assign(pommel, MAT_GOLD)

    # crossguard (box) at Z=-0.05
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, -0.05))
    guard = bpy.context.active_object
    guard.scale = (0.13, 0.018, 0.022)
    bpy.ops.object.transform_apply(scale=True)
    assign(guard, MAT_STEEL)

    # blade: straight tapered blade, 0.80 m (size-1 cube half-height 0.40),
    # base at Z=-0.07, point tip at Z=-0.87
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, -0.47))
    blade = bpy.context.active_object
    blade.scale = (0.021, 0.003, 0.80)
    bpy.ops.object.transform_apply(scale=True)
    bm = bmesh.new()
    bm.from_mesh(blade.data)
    minz = min(v.co.z for v in bm.verts)
    bottom = [v for v in bm.verts if v.co.z < minz + 0.001]
    # remove the bottom cap face, then collapse bottom verts into a point tip
    bmesh.ops.delete(bm, geom=[f for f in bm.faces
                               if all(v in bottom for v in f.verts)], context="FACES")
    for v in bottom:
        v.co = (0.0, 0.0, minz)
    bmesh.ops.remove_doubles(bm, verts=bottom, dist=0.0001)
    bm.to_mesh(blade.data)
    bm.free()
    smooth(blade)
    assign(blade, MAT_STEEL)

    bpy.ops.object.select_all(action="DESELECT")
    for o in (grip, pommel, guard, blade):
        o.select_set(True)
    bpy.context.view_layer.objects.active = grip
    bpy.ops.object.join()
    sword = bpy.context.active_object
    sword.name = "Weapon_Main"
    return sword

sword = build_sword()
sword.location = (0.30, -0.02, 0.92)
link_to(sword, "main-hand", ROOT)

# ---------------------------------------------------------------------------
# Off-hand — proportionate kite shield with clean central field
# ---------------------------------------------------------------------------
def build_shield():
    outline = [
        (-0.22, 0.30), (-0.10, 0.34), (0.0, 0.35), (0.10, 0.34), (0.22, 0.30),
        (0.21, 0.10), (0.17, -0.10), (0.10, -0.25), (0.05, -0.33), (0.0, -0.35),
        (-0.05, -0.33), (-0.10, -0.25), (-0.17, -0.10), (-0.21, 0.10),
    ]
    thick = 0.024
    bm = bmesh.new()
    vts = [bm.verts.new((x, 0.0, z)) for (x, z) in outline]
    f = bm.faces.new(vts)
    if f.normal.y > 0:                     # want front face to point -Y (forward)
        f.normal_flip()
    bmesh.ops.recalc_face_normals(bm, faces=[f])
    ret = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in ret["geom"]:
        if isinstance(v, bmesh.types.BMVert):
            v.co.y += thick

    me = bpy.data.meshes.new("Shield_Off_mesh")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Shield_Off", me)
    bpy.context.collection.objects.link(obj)
    smooth(obj)
    return obj

shield = build_shield()
assign(shield, MAT_STEEL)
shield.location = (-0.34, -0.12, 0.95)
link_to(shield, "off-hand", ROOT)

# ---------------------------------------------------------------------------
# Realm-ornament — restrained celestial chest emblem (disc + 4-point star)
# ---------------------------------------------------------------------------
def build_ornament():
    # disc backing (cylinder along Y, facing forward)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.06, depth=0.015,
                                        location=(0, 0, 0))
    disc = bpy.context.active_object
    disc.rotation_euler = (pi / 2, 0, 0)   # axis -> Y
    bpy.ops.object.transform_apply(rotation=True)
    assign(disc, MAT_GOLD)

    # 4-point celestial star (in X-Z plane, extruded slightly toward -Y front)
    pts = [
        (0.0, 0.055), (0.015, 0.015), (0.055, 0.0), (0.015, -0.015),
        (0.0, -0.055), (-0.015, -0.015), (-0.055, 0.0), (-0.015, 0.015),
    ]
    bm = bmesh.new()
    vts = [bm.verts.new((x, 0.0, z)) for (x, z) in pts]
    f = bm.faces.new(vts)
    if f.normal.y > 0:
        f.normal_flip()
    ret = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in ret["geom"]:
        if isinstance(v, bmesh.types.BMVert):
            v.co.y -= 0.010            # sit proud of disc front face
    me = bpy.data.meshes.new("Star_mesh")
    bm.to_mesh(me)
    bm.free()
    star = bpy.data.objects.new("Star", me)
    bpy.context.collection.objects.link(star)
    assign(star, MAT_ACCENT)

    bpy.ops.object.select_all(action="DESELECT")
    disc.select_set(True)
    star.select_set(True)
    bpy.context.view_layer.objects.active = disc
    bpy.ops.object.join()
    orn = bpy.context.active_object
    orn.name = "Realm_Ornament"
    return orn

ornament = build_ornament()
ornament.location = (0.0, -0.24, 1.36)
link_to(ornament, "realm-ornament", ROOT)

# ---------------------------------------------------------------------------
# report triangle counts
# ---------------------------------------------------------------------------
print("\n=== EQUIPMENT TRIANGLE COUNTS ===")
total = 0
for oname in EQUIP_NAMES:
    o = bpy.data.objects.get(oname)
    if o is None or o.type != "MESH":
        print(f"  {oname}: MISSING or not mesh")
        continue
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    total += tris
    print(f"  {oname}: {tris} tris  ({len(o.data.vertices)} verts)")
print(f"  TOTAL equipment: {total} tris")
