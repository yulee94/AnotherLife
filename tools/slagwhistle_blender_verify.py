"""Independent reopen of the Slagwhistle working blend. Print contract counts."""
import json
import os

import bpy

BLEND = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_1690c393\unity\ArtSource\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\tdf_fauna_stonehold_slagwhistle_burrower_working_v001.blend"
OUT = r"C:\Users\MY\AppData\Local\Temp\slagwhistle_cleanup\verify_blend.json"

bpy.ops.wm.open_mainfile(filepath=BLEND)

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
report = {
    "blend": BLEND,
    "objects": [{"name": o.name, "type": o.type, "loc": list(o.location)} for o in bpy.data.objects],
    "meshes": [],
    "armatures": [],
    "materials": [m.name for m in bpy.data.materials],
    "images": [{"name": i.name, "size": list(i.size)} for i in bpy.data.images if i.size[0] > 0],
    "actions": [a.name for a in bpy.data.actions],
    "unit_system": bpy.context.scene.unit_settings.system,
    "unit_scale": bpy.context.scene.unit_settings.scale_length,
}
for obj in meshes:
    me = obj.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    mw = obj.matrix_world
    pts = [mw @ v.co for v in me.vertices]
    report["meshes"].append(
        {
            "name": obj.name,
            "verts": len(me.vertices),
            "tris": tris,
            "materials": [m.name if m else None for m in me.materials],
            "uv": [uv.name for uv in me.uv_layers],
            "groups": len(obj.vertex_groups),
            "world_zmin": min(p.z for p in pts),
            "world_zmax": max(p.z for p in pts),
            "world_ymin": min(p.y for p in pts),
            "world_ymax": max(p.y for p in pts),
            "world_xmin": min(p.x for p in pts),
            "world_xmax": max(p.x for p in pts),
            "parent": obj.parent.name if obj.parent else None,
        }
    )
for arm in arms:
    report["armatures"].append(
        {
            "name": arm.name,
            "bones": len(arm.data.bones),
            "deform": sum(1 for b in arm.data.bones if b.use_deform),
            "names": [b.name for b in arm.data.bones],
            "parent": arm.parent.name if arm.parent else None,
            "location": list(arm.location),
        }
    )
root = bpy.data.objects.get("root")
report["root"] = None if root is None else list(root.location)

with open(OUT, "w", encoding="utf-8") as fh:
    json.dump(report, fh, indent=2)
    fh.write("\n")
print(json.dumps(report, indent=2))
