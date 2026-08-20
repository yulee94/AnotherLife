"""Independent Blender measurement of Slagwhistle LOD0 (t_ec244ffa).

Fresh script. Does not import producer cleanup/verify modules.
Opens the working blend, then cross-checks FBX and GLB in a cleared scene.
"""
import json
import os

import bpy
from mathutils import Vector

BLEND = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_bb2a487f\unity\ArtSource\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\tdf_fauna_stonehold_slagwhistle_burrower_working_v001.blend"
FBX = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_bb2a487f\unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Meshes\tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.fbx"
GLB = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_bb2a487f\unity\Assets\AL\Art\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\Meshes\tdf_fauna_stonehold_slagwhistle_burrower_lod0_v001.glb"
OUT = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_ec244ffa\unity\ArtSource\Terrestrials\Stonehold\SlagfallQuarry\Fauna\Slagwhistle\tdf_fauna_stonehold_slagwhistle_burrower_independent_blender.json"


def _clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def _mesh_stats(obj):
    me = obj.data
    me.calc_loop_triangles()
    loop_tris = len(me.loop_triangles)
    fan_tris = sum(max(len(p.vertices) - 2, 0) for p in me.polygons)
    ngons = sum(1 for p in me.polygons if len(p.vertices) > 4)
    quads = sum(1 for p in me.polygons if len(p.vertices) == 4)
    tris_faces = sum(1 for p in me.polygons if len(p.vertices) == 3)
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
        "tri_faces": tris_faces,
        "quads": quads,
        "ngons": ngons,
        "materials": [slot.name if slot else None for slot in me.materials],
        "uv_layers": [uv.name for uv in me.uv_layers],
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
        "location": list(obj.location),
        "scale": list(obj.scale),
        "bone_count": len(bones),
        "deform_count": len(deform),
        "deform_names": [b.name for b in deform],
        "non_deform_names": [b.name for b in bones if not b.use_deform],
        "root_bones": [b.name for b in bones if b.parent is None],
    }


def _measure_open_file(label, filepath=None):
    rec = {
        "label": label,
        "filepath": filepath,
        "objects": [],
        "meshes": [],
        "armatures": [],
        "materials": [m.name for m in bpy.data.materials],
        "images": [],
        "actions": [a.name for a in bpy.data.actions],
        "nla_tracks": [],
        "unit_system": bpy.context.scene.unit_settings.system,
        "unit_scale": bpy.context.scene.unit_settings.scale_length,
        "lights": [o.name for o in bpy.data.objects if o.type == "LIGHT"],
        "particles": [],
        "lod_named_objects": [],
    }
    for img in bpy.data.images:
        if img.size[0] > 0:
            rec["images"].append({"name": img.name, "size": [int(img.size[0]), int(img.size[1])]})
    for obj in bpy.data.objects:
        rec["objects"].append(
            {
                "name": obj.name,
                "type": obj.type,
                "location": list(obj.location),
                "scale": list(obj.scale),
                "parent": obj.parent.name if obj.parent else None,
            }
        )
        lname = obj.name.lower()
        if "lod1" in lname or "lod2" in lname or "impostor" in lname or "imposter" in lname:
            rec["lod_named_objects"].append(obj.name)
        if obj.particle_systems:
            rec["particles"].extend([f"{obj.name}:{ps.name}" for ps in obj.particle_systems])
        if obj.type == "MESH":
            rec["meshes"].append(_mesh_stats(obj))
        if obj.type == "ARMATURE":
            rec["armatures"].append(_armature_stats(obj))
        if obj.animation_data and obj.animation_data.nla_tracks:
            rec["nla_tracks"].extend([t.name for t in obj.animation_data.nla_tracks])

    rec["totals"] = {
        "loop_triangles": sum(m["loop_triangles"] for m in rec["meshes"]),
        "fan_triangles": sum(m["fan_triangles"] for m in rec["meshes"]),
        "deform_bones": sum(a["deform_count"] for a in rec["armatures"]),
        "all_bones": sum(a["bone_count"] for a in rec["armatures"]),
        "materials": len(rec["materials"]),
        "actions": len(rec["actions"]),
    }

    # Facing heuristic: Head bone world Y vs Root (Blender: -Y is Unity +Z).
    rec["facing"] = None
    for arm in bpy.data.objects:
        if arm.type != "ARMATURE":
            continue
        head = arm.data.bones.get("Head")
        root = arm.data.bones.get("Root") or arm.data.bones.get("root")
        if head is None:
            continue
        head_world = arm.matrix_world @ head.head_local
        root_world = arm.matrix_world @ (root.head_local if root else Vector((0, 0, 0)))
        delta = head_world - root_world
        rec["facing"] = {
            "armature": arm.name,
            "head_world": list(head_world),
            "root_world": list(root_world),
            "delta": list(delta),
            "head_more_negative_y": delta.y < 0,
            "interpretation": "Blender -Y / Unity +Z" if delta.y < 0 else "NOT Blender -Y",
        }
        break

    rec["root_empty"] = None
    for name in ("root", "Root"):
        obj = bpy.data.objects.get(name)
        if obj is not None:
            rec["root_empty"] = {"name": obj.name, "location": list(obj.location), "type": obj.type}
            break
    return rec


def main():
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    report = {"task": "t_ec244ffa", "blender": bpy.app.version_string, "sources": {"blend": BLEND, "fbx": FBX, "glb": GLB}}

    bpy.ops.wm.open_mainfile(filepath=BLEND)
    report["blend"] = _measure_open_file("blend", BLEND)

    _clear_scene()
    bpy.ops.import_scene.fbx(filepath=FBX, automatic_bone_orientation=False)
    report["fbx"] = _measure_open_file("fbx", FBX)

    _clear_scene()
    bpy.ops.import_scene.gltf(filepath=GLB)
    report["glb"] = _measure_open_file("glb", GLB)

    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=2)
        fh.write("\n")
    print(json.dumps({"wrote": OUT, "blend_totals": report["blend"]["totals"], "fbx_totals": report["fbx"]["totals"], "glb_totals": report["glb"]["totals"], "blend_facing": report["blend"]["facing"], "blend_root": report["blend"]["root_empty"], "blend_aabb": report["blend"]["meshes"][0]["aabb_world"] if report["blend"]["meshes"] else None}, indent=2))


if __name__ == "__main__":
    main()
