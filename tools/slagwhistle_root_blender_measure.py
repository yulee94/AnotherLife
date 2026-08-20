"""Root-task independent Blender measure of Slagwhistle LOD0 (t_b8b483e2).

Does not import producer or t_ec244ffa modules. Opens blend, then FBX, then GLB.
"""
import json
import os

import bpy

ROOT = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_b8b483e2"
BLEND = os.path.join(
    ROOT,
    r"unity\ArtSource\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\tdf_fauna_stonehold_slagwhistle_burrower_working_v001.blend",
)
FBX = os.path.join(
    ROOT,
    r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Meshes\tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.fbx",
)
GLB = os.path.join(
    ROOT,
    r"unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Meshes\tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.glb",
)
OUT = os.path.join(
    ROOT,
    r"unity\ArtSource\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\tdf_fauna_stonehold_slagwhistle_burrower_root_blender.json",
)


def _mesh_stats(obj):
    me = obj.data
    me.calc_loop_triangles()
    loop_tris = len(me.loop_triangles)
    fan_tris = sum(max(len(p.vertices) - 2, 0) for p in me.polygons)
    ngons = sum(1 for p in me.polygons if len(p.vertices) > 4)
    mw = obj.matrix_world
    pts = [mw @ v.co for v in me.vertices] if me.vertices else []
    aabb = None
    if pts:
        xs = [p.x for p in pts]
        ys = [p.y for p in pts]
        zs = [p.z for p in pts]
        aabb = {
            "xmin": min(xs),
            "xmax": max(xs),
            "ymin": min(ys),
            "ymax": max(ys),
            "zmin": min(zs),
            "zmax": max(zs),
            "size": [max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)],
        }
    max_w = 0
    over4 = 0
    for v in me.vertices:
        n = len(v.groups)
        if n > max_w:
            max_w = n
        if n > 4:
            over4 += 1
    return {
        "name": obj.name,
        "verts": len(me.vertices),
        "polygons": len(me.polygons),
        "loop_triangles": loop_tris,
        "fan_triangles": fan_tris,
        "ngons": ngons,
        "materials": [slot.name if slot else None for slot in me.materials],
        "vertex_groups": len(obj.vertex_groups),
        "max_influences": max_w,
        "verts_over_4_influences": over4,
        "parent": obj.parent.name if obj.parent else None,
        "location": list(obj.location),
        "scale": list(obj.scale),
        "aabb_world": aabb,
    }


def _armature_stats(obj):
    bones = list(obj.data.bones)
    deform = [b for b in bones if b.use_deform]
    return {
        "name": obj.name,
        "bone_count": len(bones),
        "deform_count": len(deform),
        "deform_names": [b.name for b in deform],
        "non_deform_names": [b.name for b in bones if not b.use_deform],
        "root_bones": [b.name for b in bones if b.parent is None],
        "location": list(obj.location),
        "scale": list(obj.scale),
    }


def _measure(label):
    rec = {
        "label": label,
        "objects": [{"name": o.name, "type": o.type} for o in bpy.data.objects],
        "meshes": [],
        "armatures": [],
        "materials": [m.name for m in bpy.data.materials],
        "images": [
            {"name": img.name, "size": [int(img.size[0]), int(img.size[1])]}
            for img in bpy.data.images
            if img.size[0] > 0
        ],
        "actions": [a.name for a in bpy.data.actions],
        "unit_system": bpy.context.scene.unit_settings.system,
        "unit_scale": bpy.context.scene.unit_settings.scale_length,
        "lights": [o.name for o in bpy.data.objects if o.type == "LIGHT"],
        "particles": [],
        "lod_named_objects": [],
    }
    for obj in bpy.data.objects:
        lname = obj.name.lower()
        if any(k in lname for k in ("lod1", "lod2", "impostor", "imposter")):
            rec["lod_named_objects"].append(obj.name)
        if obj.particle_systems:
            rec["particles"].extend([f"{obj.name}:{ps.name}" for ps in obj.particle_systems])
        if obj.type == "MESH":
            rec["meshes"].append(_mesh_stats(obj))
        if obj.type == "ARMATURE":
            rec["armatures"].append(_armature_stats(obj))
    rec["totals"] = {
        "loop_triangles": sum(m["loop_triangles"] for m in rec["meshes"]),
        "fan_triangles": sum(m["fan_triangles"] for m in rec["meshes"]),
        "deform_bones": sum(a["deform_count"] for a in rec["armatures"]),
        "all_bones": sum(a["bone_count"] for a in rec["armatures"]),
        "materials": len(rec["materials"]),
        "actions": len(rec["actions"]),
    }
    rec["facing"] = None
    for arm in bpy.data.objects:
        if arm.type != "ARMATURE":
            continue
        head = arm.data.bones.get("Head")
        if head is None:
            continue
        head_world = arm.matrix_world @ head.head_local
        rec["facing"] = {
            "armature": arm.name,
            "head_world": list(head_world),
            "head_more_negative_y": head_world.y < 0,
            "interpretation": "Blender -Y / Unity +Z" if head_world.y < 0 else "NOT -Y",
        }
    rec["root_empty"] = None
    root = bpy.data.objects.get("root")
    if root is not None:
        rec["root_empty"] = {"name": root.name, "location": list(root.location), "type": root.type}
    return rec


def main():
    payload = {
        "task": "t_b8b483e2",
        "blender": bpy.app.version_string,
        "sources": {"blend": BLEND, "fbx": FBX, "glb": GLB},
    }

    bpy.ops.wm.open_mainfile(filepath=BLEND)
    payload["blend"] = _measure("blend")
    payload["blend"]["filepath"] = BLEND

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=FBX, automatic_bone_orientation=True)
    payload["fbx"] = _measure("fbx")
    payload["fbx"]["filepath"] = FBX

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=GLB)
    payload["glb"] = _measure("glb")
    payload["glb"]["filepath"] = GLB

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, indent=2)
    print("WROTE", OUT)
    print("BLEND_TRIS", payload["blend"]["totals"]["loop_triangles"])
    print("BLEND_BONES", payload["blend"]["totals"]["deform_bones"])
    print("BLEND_MATS", payload["blend"]["totals"]["materials"])
    print("BLEND_CLIPS", payload["blend"]["totals"]["actions"])
    print("FBX_TRIS", payload["fbx"]["totals"]["loop_triangles"])
    print("FBX_BONES", payload["fbx"]["totals"]["deform_bones"])
    print("GLB_TRIS", payload["glb"]["totals"]["loop_triangles"])
    print("GLB_MESHES", [m["name"] for m in payload["glb"]["meshes"]])


if __name__ == "__main__":
    main()
