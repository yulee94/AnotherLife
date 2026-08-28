"""Author the reversible neutral first-session terrain-landmark kit candidate.

The source is a technical/art review candidate only. It is conventionally Z-up,
uses the measured Neutral Covenant Hall material and modular scale language, keeps
all runtime-facing objects at a base-center origin, and separates render LODs,
collision, navigation exclusions, and sockets. The command refuses to overwrite
either its versioned source or receipt.

Run with Blender 5.2 or newer:

    blender --background --factory-startup --python-exit-code 1 \
      --python tools/blender/author_neutral_terrain_landmark_kit.py -- \
      --output unity/ArtSource/FirstUserOnboarding/neutral_covenant_terrain_landmark_kit_working_v001.blend \
      --receipt archive/local-run/blender/neutral-terrain-landmark-kit-authoring-v001.json
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from collections.abc import Iterable
from pathlib import Path
from typing import Any

import bpy
from mathutils import Matrix

SCRIPT_PATH = Path(__file__).resolve()
REPOSITORY_ROOT = SCRIPT_PATH.parents[2]
SOURCE_ID = "neutral-covenant-terrain-landmark-kit-working-v001"
HALL_REFERENCE_PATH = (
    "unity/ArtSource/FirstUserOnboarding/neutral_covenant_hall_working_v001.blend"
)
HALL_REFERENCE_SHA256 = (
    "b807a8ec7d5332a70774405ccf240a16e8555787c9de0778303d0ebe54d85a5c"
)
COLLECTION_NAMES = (
    "AL_RENDER",
    "AL_COLLISION",
    "AL_NAVIGATION",
    "AL_SOCKETS",
)
MATERIAL_CONTRACTS = {
    "M_CovenantHall_Floor": {
        "baseColor": (0.12, 0.14, 0.16, 1.0),
        "metallic": 0.08,
        "roughness": 0.72,
    },
    "M_CovenantHall_Wall": {
        "baseColor": (0.23, 0.25, 0.27, 1.0),
        "metallic": 0.02,
        "roughness": 0.78,
    },
    "M_CovenantHall_Trim": {
        "baseColor": (0.055, 0.07, 0.08, 1.0),
        "metallic": 0.72,
        "roughness": 0.32,
    },
}


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--receipt", required=True, type=Path)
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(blender_args)


def _resolve(path: Path) -> Path:
    return path if path.is_absolute() else REPOSITORY_ROOT / path


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _material(
    name: str, contract: dict[str, tuple[float, ...] | float]
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    if shader is None:
        raise RuntimeError(f"Principled BSDF is unavailable for {name}")
    shader.inputs["Base Color"].default_value = contract["baseColor"]
    shader.inputs["Metallic"].default_value = contract["metallic"]
    shader.inputs["Roughness"].default_value = contract["roughness"]
    material["al_schema_version"] = 1
    material["al_asset_source_id"] = SOURCE_ID
    material["al_aesthetic_reference"] = HALL_REFERENCE_PATH
    return material


def _box_part(
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    material: bpy.types.Material,
    bevel: float,
    segments: int,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0.0:
        modifier = obj.modifiers.new("Deterministic edge break", "BEVEL")
        modifier.width = bevel
        modifier.segments = segments
        modifier.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        result = bpy.ops.object.modifier_apply(modifier=modifier.name)
        if "FINISHED" not in result:
            raise RuntimeError(f"Could not apply bevel to {name}")
    obj.data.materials.append(material)
    return obj


def _join_render_asset(
    name: str,
    parts: list[bpy.types.Object],
    collection: bpy.types.Collection,
    family: str,
    lod_index: int,
) -> bpy.types.Object:
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        result = bpy.ops.object.join()
        if "FINISHED" not in result:
            raise RuntimeError(f"Could not join render asset {name}")
        obj = bpy.context.object
    else:
        obj = parts[0]
    obj.name = name
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)
    result = bpy.ops.object.origin_set(type="ORIGIN_CURSOR", center="MEDIAN")
    if "FINISHED" not in result:
        raise RuntimeError(f"Could not set base-center pivot for {name}")
    result = bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if "FINISHED" not in result:
        raise RuntimeError(f"Could not apply transforms for {name}")
    triangulate = obj.modifiers.new("Deterministic triangulation", "TRIANGULATE")
    triangulate.quad_method = "BEAUTY"
    triangulate.ngon_method = "BEAUTY"
    result = bpy.ops.object.modifier_apply(modifier=triangulate.name)
    if "FINISHED" not in result:
        raise RuntimeError(f"Could not triangulate {name}")
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    obj.data.name = f"{name}_Mesh"
    for current_collection in list(obj.users_collection):
        current_collection.objects.unlink(obj)
    collection.objects.link(obj)
    obj["al_schema_version"] = 1
    obj["al_asset_source_id"] = SOURCE_ID
    obj["al_local_element_id"] = name
    obj["al_role"] = f"render.lod{lod_index}"
    obj["al_asset_family"] = family
    obj["al_lod_index"] = lod_index
    obj["al_aesthetic_reference"] = HALL_REFERENCE_PATH
    obj.hide_render = lod_index > 0
    return obj


def _box_mesh(
    name: str,
    dimensions: tuple[float, float, float],
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    half_x, half_y, height = (
        dimensions[0] * 0.5,
        dimensions[1] * 0.5,
        dimensions[2],
    )
    vertices = [
        (-half_x, -half_y, 0.0),
        (half_x, -half_y, 0.0),
        (half_x, half_y, 0.0),
        (-half_x, half_y, 0.0),
        (-half_x, -half_y, height),
        (half_x, -half_y, height),
        (half_x, half_y, height),
        (-half_x, half_y, height),
    ]
    faces = [
        (0, 3, 2, 1),
        (4, 5, 6, 7),
        (0, 1, 5, 4),
        (3, 7, 6, 2),
        (0, 4, 7, 3),
        (1, 2, 6, 5),
    ]
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=False, clean_customdata=False)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.hide_render = True
    obj.display_type = "WIRE"
    obj.show_in_front = True
    return obj


def _nav_exclusion(
    name: str,
    dimensions: tuple[float, float],
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    half_x = dimensions[0] * 0.5
    half_y = dimensions[1] * 0.5
    vertices = [
        (-half_x, -half_y, 0.0),
        (half_x, -half_y, 0.0),
        (half_x, half_y, 0.0),
        (-half_x, half_y, 0.0),
    ]
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], [(0, 1, 2, 3)])
    mesh.validate(verbose=False, clean_customdata=False)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.hide_render = True
    obj.display_type = "WIRE"
    obj.show_in_front = True
    return obj


def _technical_properties(
    obj: bpy.types.Object,
    role: str,
    derived_from: str,
    method: str,
) -> None:
    obj["al_schema_version"] = 1
    obj["al_asset_source_id"] = SOURCE_ID
    obj["al_local_element_id"] = obj.name
    obj["al_role"] = role
    obj["al_derived_from"] = derived_from
    obj["al_derivation"] = method


def _empty(
    name: str,
    location: tuple[float, float, float],
    rotation_z: float,
    collection: bpy.types.Collection,
    role: str,
    derived_from: str,
) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler[2] = rotation_z
    obj.empty_display_type = "ARROWS"
    obj.empty_display_size = 0.35
    obj.show_in_front = True
    obj.hide_render = True
    _technical_properties(
        obj,
        role,
        derived_from,
        "authored-local-socket-transform",
    )
    obj["al_forward_axis"] = "-Y"
    return obj


def _collection(name: str) -> bpy.types.Collection:
    collection = bpy.data.collections.new(name)
    collection["al_schema_version"] = 1
    collection["al_asset_source_id"] = SOURCE_ID
    bpy.context.scene.collection.children.link(collection)
    return collection


def _triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def _bounds(obj: bpy.types.Object) -> tuple[list[float], list[float]]:
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    return (
        [min(point[index] for point in points) for index in range(3)],
        [max(point[index] for point in points) for index in range(3)],
    )


def _matrix(value: Matrix) -> list[list[float]]:
    return [
        [round(float(component), 9) for component in row]
        for row in value
    ]


def _semantic_receipt(objects: Iterable[bpy.types.Object]) -> dict[str, Any]:
    object_receipts: dict[str, Any] = {}
    for obj in sorted(objects, key=lambda item: item.name):
        record: dict[str, Any] = {
            "type": obj.type,
            "collections": sorted(collection.name for collection in obj.users_collection),
            "matrixWorld": _matrix(obj.matrix_world),
            "role": obj.get("al_role"),
        }
        if obj.type == "MESH":
            record.update(
                {
                    "vertices": len(obj.data.vertices),
                    "triangles": _triangle_count(obj),
                    "bounds": _bounds(obj),
                    "materials": [
                        material.name if material is not None else None
                        for material in obj.data.materials
                    ],
                }
            )
        object_receipts[obj.name] = record
    serialized = json.dumps(object_receipts, sort_keys=True, separators=(",", ":"))
    return {
        "sha256": hashlib.sha256(serialized.encode("utf-8")).hexdigest(),
        "objects": object_receipts,
    }


def _create_source() -> dict[str, Any]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "AL_NeutralTerrainLandmarkKit_ReviewCandidate_v001"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene["al_schema_version"] = 1
    scene["al_asset_source_id"] = SOURCE_ID
    scene["al_approval_state"] = "review-candidate"
    scene["al_aesthetic_reference"] = HALL_REFERENCE_PATH
    scene["al_aesthetic_reference_sha256"] = HALL_REFERENCE_SHA256
    scene["al_runtime_authority"] = False

    collections = {name: _collection(name) for name in COLLECTION_NAMES}
    materials = {
        name: _material(name, contract)
        for name, contract in MATERIAL_CONTRACTS.items()
    }
    floor = materials["M_CovenantHall_Floor"]
    wall = materials["M_CovenantHall_Wall"]
    trim = materials["M_CovenantHall_Trim"]

    render_specs: list[tuple[str, str, int, list[bpy.types.Object]]] = []
    render_specs.append(
        (
            "M_Neutral_CovenantPathBeacon_LOD0",
            "path-beacon",
            0,
            [
                _box_part("BeaconBase", (0, 0, 0.09), (1.2, 0.9, 0.18), wall, 0.05, 2),
                _box_part("BeaconPlinth", (0, 0, 0.34), (0.9, 0.68, 0.32), floor, 0.045, 2),
                _box_part("BeaconShaft", (0, 0, 1.55), (0.42, 0.36, 2.1), wall, 0.04, 2),
                _box_part("BeaconBand", (0, 0, 1.05), (0.58, 0.48, 0.16), trim, 0.025, 1),
                _box_part("BeaconCap", (0, 0, 2.7), (0.78, 0.62, 0.2), wall, 0.035, 2),
                _box_part("BeaconForkL", (-0.22, 0, 3.08), (0.18, 0.28, 0.62), trim, 0.025, 2),
                _box_part("BeaconForkR", (0.22, 0, 3.08), (0.18, 0.28, 0.62), trim, 0.025, 2),
                _box_part("BeaconCrown", (0, 0, 2.93), (0.64, 0.22, 0.12), trim, 0.02, 1),
            ],
        )
    )
    render_specs.append(
        (
            "M_Neutral_CovenantPathBeacon_LOD1",
            "path-beacon",
            1,
            [
                _box_part("BeaconL1Base", (0, 0, 0.14), (1.05, 0.8, 0.28), wall, 0.035, 1),
                _box_part("BeaconL1Shaft", (0, 0, 1.54), (0.46, 0.4, 2.52), wall, 0.03, 1),
                _box_part("BeaconL1Cap", (0, 0, 2.78), (0.72, 0.56, 0.18), trim, 0.025, 1),
                _box_part("BeaconL1ForkL", (-0.2, 0, 3.08), (0.18, 0.26, 0.62), trim, 0.02, 1),
                _box_part("BeaconL1ForkR", (0.2, 0, 3.08), (0.18, 0.26, 0.62), trim, 0.02, 1),
            ],
        )
    )
    render_specs.append(
        (
            "M_Neutral_CovenantPathBeacon_LOD2",
            "path-beacon",
            2,
            [
                _box_part("BeaconL2Base", (0, 0, 0.15), (0.9, 0.7, 0.3), wall, 0.0, 1),
                _box_part("BeaconL2Shaft", (0, 0, 1.425), (0.5, 0.4, 2.25), wall, 0.0, 1),
                _box_part("BeaconL2ForkL", (-0.18, 0, 2.925), (0.18, 0.28, 0.75), trim, 0.0, 1),
                _box_part("BeaconL2ForkR", (0.18, 0, 2.925), (0.18, 0.28, 0.75), trim, 0.0, 1),
            ],
        )
    )

    render_specs.append(
        (
            "M_Neutral_CovenantTrailPost_LOD0",
            "trail-post",
            0,
            [
                _box_part("PostBase", (0, 0, 0.08), (0.55, 0.55, 0.16), wall, 0.035, 2),
                _box_part("PostShaft", (0, 0, 0.62), (0.2, 0.2, 0.92), trim, 0.025, 2),
                _box_part("PostBand", (0, 0, 0.94), (0.34, 0.34, 0.14), wall, 0.02, 1),
                _box_part("PostCap", (0, 0, 1.16), (0.42, 0.42, 0.18), wall, 0.03, 2),
                _box_part("PostInset", (0, -0.125, 0.73), (0.24, 0.05, 0.28), floor, 0.012, 1),
            ],
        )
    )
    render_specs.append(
        (
            "M_Neutral_CovenantTrailPost_LOD1",
            "trail-post",
            1,
            [
                _box_part("PostL1Base", (0, 0, 0.08), (0.5, 0.5, 0.16), wall, 0.025, 1),
                _box_part("PostL1Shaft", (0, 0, 0.61), (0.22, 0.22, 0.9), trim, 0.018, 1),
                _box_part("PostL1Cap", (0, 0, 1.1), (0.36, 0.36, 0.16), wall, 0.02, 1),
            ],
        )
    )
    render_specs.append(
        (
            "M_Neutral_CovenantTrailPost_LOD2",
            "trail-post",
            2,
            [
                _box_part("PostL2Base", (0, 0, 0.075), (0.45, 0.45, 0.15), wall, 0.0, 1),
                _box_part("PostL2Shaft", (0, 0, 0.65), (0.28, 0.28, 1.15), wall, 0.0, 1),
            ],
        )
    )

    render_specs.append(
        (
            "M_Neutral_CovenantBoundaryWall_LOD0",
            "boundary-wall",
            0,
            [
                _box_part("WallCore", (0, 0, 0.36), (4.0, 0.42, 0.72), wall, 0.045, 2),
                _box_part("WallCap", (0, 0, 0.81), (4.2, 0.58, 0.18), floor, 0.035, 2),
                _box_part("WallPillarL", (-1.8, 0, 0.55), (0.58, 0.62, 1.1), wall, 0.045, 2),
                _box_part("WallPillarR", (1.8, 0, 0.55), (0.58, 0.62, 1.1), wall, 0.045, 2),
                _box_part("WallPillarCapL", (-1.8, 0, 1.15), (0.72, 0.72, 0.18), trim, 0.03, 2),
                _box_part("WallPillarCapR", (1.8, 0, 1.15), (0.72, 0.72, 0.18), trim, 0.03, 2),
                _box_part("WallTrimRail", (0, -0.25, 0.55), (2.8, 0.08, 0.14), trim, 0.02, 1),
            ],
        )
    )
    render_specs.append(
        (
            "M_Neutral_CovenantBoundaryWall_LOD1",
            "boundary-wall",
            1,
            [
                _box_part("WallL1Core", (0, 0, 0.38), (4.0, 0.44, 0.76), wall, 0.03, 1),
                _box_part("WallL1Cap", (0, 0, 0.82), (4.2, 0.56, 0.16), floor, 0.025, 1),
                _box_part("WallL1PillarL", (-1.8, 0, 0.59), (0.58, 0.6, 1.18), wall, 0.03, 1),
                _box_part("WallL1PillarR", (1.8, 0, 0.59), (0.58, 0.6, 1.18), wall, 0.03, 1),
            ],
        )
    )
    render_specs.append(
        (
            "M_Neutral_CovenantBoundaryWall_LOD2",
            "boundary-wall",
            2,
            [
                _box_part("WallL2Core", (0, 0, 0.48), (4.2, 0.55, 0.96), wall, 0.0, 1),
            ],
        )
    )

    render_objects = [
        _join_render_asset(name, parts, collections["AL_RENDER"], family, lod_index)
        for name, family, lod_index, parts in render_specs
    ]

    collision_specs = (
        (
            "COL_NeutralCovenantPathBeacon_Body_00",
            (1.2, 0.9, 3.4),
            "M_Neutral_CovenantPathBeacon_LOD0",
        ),
        (
            "COL_NeutralCovenantTrailPost_Body_00",
            (0.55, 0.55, 1.3),
            "M_Neutral_CovenantTrailPost_LOD0",
        ),
        (
            "COL_NeutralCovenantBoundaryWall_Body_00",
            (4.2, 0.72, 1.25),
            "M_Neutral_CovenantBoundaryWall_LOD0",
        ),
    )
    collision_objects = []
    for name, dimensions, derived_from in collision_specs:
        obj = _box_mesh(name, dimensions, collections["AL_COLLISION"])
        _technical_properties(
            obj,
            "collision.static-proxy",
            derived_from,
            "authored-primitive-envelope",
        )
        collision_objects.append(obj)

    nav_specs = (
        (
            "NAVEX_NeutralCovenantPathBeacon_Footprint_00",
            (1.3, 1.0),
            "COL_NeutralCovenantPathBeacon_Body_00",
        ),
        (
            "NAVEX_NeutralCovenantTrailPost_Footprint_00",
            (0.65, 0.65),
            "COL_NeutralCovenantTrailPost_Body_00",
        ),
        (
            "NAVEX_NeutralCovenantBoundaryWall_Footprint_00",
            (4.3, 0.82),
            "COL_NeutralCovenantBoundaryWall_Body_00",
        ),
    )
    nav_objects = []
    for name, dimensions, derived_from in nav_specs:
        obj = _nav_exclusion(name, dimensions, collections["AL_NAVIGATION"])
        _technical_properties(
            obj,
            "navigation.static-exclusion",
            derived_from,
            "collision-footprint-plus-clearance",
        )
        nav_objects.append(obj)

    sockets = [
        _empty(
            "SOCKET_PathBeacon_Interaction_00",
            (0.0, -0.9, 0.0),
            0.0,
            collections["AL_SOCKETS"],
            "socket.interaction",
            "M_Neutral_CovenantPathBeacon_LOD0",
        ),
        _empty(
            "SOCKET_TrailPost_Interaction_00",
            (0.0, -0.55, 0.0),
            0.0,
            collections["AL_SOCKETS"],
            "socket.interaction",
            "M_Neutral_CovenantTrailPost_LOD0",
        ),
        _empty(
            "SOCKET_BoundaryWall_End_A",
            (-2.1, 0.0, 0.0),
            -math.pi * 0.5,
            collections["AL_SOCKETS"],
            "socket.modular-end",
            "M_Neutral_CovenantBoundaryWall_LOD0",
        ),
        _empty(
            "SOCKET_BoundaryWall_End_B",
            (2.1, 0.0, 0.0),
            math.pi * 0.5,
            collections["AL_SOCKETS"],
            "socket.modular-end",
            "M_Neutral_CovenantBoundaryWall_LOD0",
        ),
    ]

    all_objects = render_objects + collision_objects + nav_objects + sockets
    semantic = _semantic_receipt(all_objects)
    lod_receipts = {
        family: [
            {
                "name": obj.name,
                "lodIndex": int(obj["al_lod_index"]),
                "triangles": _triangle_count(obj),
                "bounds": _bounds(obj),
                "materials": [material.name for material in obj.data.materials],
            }
            for obj in sorted(
                (item for item in render_objects if item["al_asset_family"] == family),
                key=lambda item: int(item["al_lod_index"]),
            )
        ]
        for family in ("path-beacon", "trail-post", "boundary-wall")
    }
    for family, lods in lod_receipts.items():
        triangles = [lod["triangles"] for lod in lods]
        if not all(
            triangles[index] > triangles[index + 1]
            for index in range(len(triangles) - 1)
        ):
            raise RuntimeError(f"LOD triangle counts do not strictly reduce for {family}")
    return {
        "allObjects": all_objects,
        "semantic": semantic,
        "lods": lod_receipts,
        "materials": {
            name: {
                "baseColor": list(contract["baseColor"]),
                "metallic": contract["metallic"],
                "roughness": contract["roughness"],
            }
            for name, contract in MATERIAL_CONTRACTS.items()
        },
    }


def main() -> int:
    args = _arguments()
    output_path = _resolve(args.output)
    receipt_path = _resolve(args.receipt)
    if output_path.suffix.lower() != ".blend":
        print("AL terrain-landmark authoring: output must be .blend", file=sys.stderr)
        return 4
    if output_path.exists() or receipt_path.exists():
        print(
            "AL terrain-landmark authoring: refusing to overwrite source or receipt",
            file=sys.stderr,
        )
        return 4

    result = _create_source()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    save_result = bpy.ops.wm.save_as_mainfile(
        filepath=str(output_path),
        check_existing=False,
        compress=True,
    )
    if "FINISHED" not in save_result:
        print("AL terrain-landmark authoring: source save failed", file=sys.stderr)
        return 1
    source_hash = _sha256(output_path)
    receipt = {
        "schemaVersion": 1,
        "status": "review_candidate_authored",
        "sourceId": SOURCE_ID,
        "sourcePath": (
            output_path.relative_to(REPOSITORY_ROOT).as_posix()
            if output_path.is_relative_to(REPOSITORY_ROOT)
            else output_path.as_posix()
        ),
        "sourceSha256": source_hash,
        "blenderVersion": bpy.app.version_string,
        "toolPath": SCRIPT_PATH.relative_to(REPOSITORY_ROOT).as_posix(),
        "toolSha256": _sha256(SCRIPT_PATH),
        "approvalState": "review-candidate",
        "runtimeAuthority": False,
        "aestheticReference": {
            "sourcePath": HALL_REFERENCE_PATH,
            "sourceSha256": HALL_REFERENCE_SHA256,
            "copiedMaterialParameters": True,
        },
        "coordinateSystem": {
            "unitSystem": "METRIC",
            "unitScale": 1.0,
            "upAxis": "Z",
            "forwardAxis": "-Y",
            "pivot": "asset base center at source origin",
        },
        "collections": list(COLLECTION_NAMES),
        "semantic": result["semantic"],
        "lods": result["lods"],
        "materials": result["materials"],
        "openReview": [
            "Owner approval of forked path-beacon silhouette and neutral Covenant role",
            "Owner approval of the trail-post and boundary-wall kit entering the first-session terrain",
            "Final texture atlas, wear, decals, palette, vegetation adjacency, and lighting",
            "Runtime collider/nav/socket import binding and representative traversal profiling",
        ],
    }
    receipt_path.parent.mkdir(parents=True, exist_ok=True)
    receipt_path.write_text(
        json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    print(f"AL terrain-landmark source: {output_path}")
    print(f"AL terrain-landmark receipt: {receipt_path}")
    print(
        "AL terrain-landmark authoring: review candidate created; "
        f"sha256={source_hash}; semantic={result['semantic']['sha256']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
