"""Apply narrowly approved, deterministic repairs to retained Blender sources.

The tool is intentionally not a general cleanup command. Each plan is bound to one
version-pinned source and derives only objective technical helpers from existing
reviewed geometry. It refuses hash drift, partial/conflicting repairs, validation
errors, and render-object changes. Dry-run is the default; ``--apply`` is required
to save the source in place.

Example:

    blender --background --factory-startup --python-exit-code 1 \
      --python tools/blender/remediate_al_asset_sources.py -- \
      --source neutral-covenant-hall-working-v001 \
      --output archive/local-run/blender/hall-remediation-v001.json \
      --apply
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import sys
from pathlib import Path
from typing import Any

import bpy
from mathutils import Matrix, Vector

SCRIPT_PATH = Path(__file__).resolve()
if str(SCRIPT_PATH.parent) not in sys.path:
    sys.path.insert(0, str(SCRIPT_PATH.parent))

from validate_al_asset_sources import (
    DEFAULT_MANIFEST,
    REPOSITORY_ROOT,
    _manifest_diagnostics,
    _resolved_lods,
    _sha256,
    _validate_source,
)

PLAN_ID = "neutral-covenant-hall-technical-helpers-v001"
SUPPORTED_SOURCE_ID = "neutral-covenant-hall-working-v001"
FLOOR_NAME = "FloorModule"
DOORWAY_NAME = "DoorwayModule"
COLLISION_COLLECTION = "AL_COLLISION"
NAVIGATION_COLLECTION = "AL_NAVIGATION"
SOCKET_COLLECTION = "AL_SOCKETS"
COLLISION_NAME = "COL_NeutralCovenantHall_Walkable_00"
NAVIGATION_NAME = "NAV_NeutralCovenantHall_Walkable_00"
SOCKET_NAME = "SOCKET_Entrance_00"
TARGET_COLLECTIONS = (
    COLLISION_COLLECTION,
    NAVIGATION_COLLECTION,
    SOCKET_COLLECTION,
)
TARGET_OBJECTS = (COLLISION_NAME, NAVIGATION_NAME, SOCKET_NAME)
POSITION_TOLERANCE = 1e-6


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--apply", action="store_true")
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(blender_args)


def _resolve(path: Path) -> Path:
    return path if path.is_absolute() else REPOSITORY_ROOT / path


def _round_vector(value: Any) -> list[float]:
    return [round(float(component), 9) for component in value]


def _round_matrix(value: Any) -> list[list[float]]:
    return [
        [round(float(component), 9) for component in row]
        for row in value
    ]


def _bounds(obj: bpy.types.Object) -> tuple[list[float], list[float]]:
    if obj.type != "MESH" or not obj.data.vertices:
        raise ValueError(f"{obj.name} has no mesh bounds")
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    return (
        [min(point[index] for point in points) for index in range(3)],
        [max(point[index] for point in points) for index in range(3)],
    )


def _render_snapshot(source: dict[str, Any]) -> tuple[str, dict[str, Any]]:
    lods = _resolved_lods(source)
    names: set[str] = set()
    for export_set in source.get("exportSets", []):
        for lod_id in export_set.get("objectsFromLods", []):
            names.update(lods.get(lod_id, []))
        names.update(export_set.get("includeObjects", []))
    objects: dict[str, Any] = {}
    for name in sorted(names):
        obj = bpy.data.objects.get(name)
        if obj is None:
            objects[name] = None
            continue
        record: dict[str, Any] = {
            "type": obj.type,
            "parent": obj.parent.name if obj.parent else None,
            "matrixWorld": _round_matrix(obj.matrix_world),
            "collections": sorted(collection.name for collection in obj.users_collection),
        }
        if obj.type == "MESH":
            record.update(
                {
                    "vertices": [_round_vector(vertex.co) for vertex in obj.data.vertices],
                    "polygons": [
                        {
                            "vertices": list(polygon.vertices),
                            "material": int(polygon.material_index),
                            "smooth": bool(polygon.use_smooth),
                        }
                        for polygon in obj.data.polygons
                    ],
                    "materials": [
                        material.name if material is not None else None
                        for material in obj.data.materials
                    ],
                    "modifiers": [
                        {"name": modifier.name, "type": modifier.type}
                        for modifier in obj.modifiers
                    ],
                }
            )
        objects[name] = record
    serialized = json.dumps(objects, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(serialized.encode("utf-8")).hexdigest(), objects


def _derived_plan() -> dict[str, Any]:
    floor = bpy.data.objects.get(FLOOR_NAME)
    doorway = bpy.data.objects.get(DOORWAY_NAME)
    if floor is None or floor.type != "MESH":
        raise ValueError(f"required derivation mesh is unavailable: {FLOOR_NAME}")
    if doorway is None or doorway.type != "MESH":
        raise ValueError(f"required derivation mesh is unavailable: {DOORWAY_NAME}")

    floor_minimum, floor_maximum = _bounds(floor)
    doorway_minimum, doorway_maximum = _bounds(doorway)
    floor_center = Vector(
        (
            (floor_minimum[0] + floor_maximum[0]) * 0.5,
            floor_maximum[1],
            (floor_minimum[2] + floor_maximum[2]) * 0.5,
        )
    )
    doorway_center = Vector(
        (
            (doorway_minimum[0] + doorway_maximum[0]) * 0.5,
            floor_maximum[1],
            (doorway_minimum[2] + doorway_maximum[2]) * 0.5,
        )
    )
    boundary_candidates = []
    for axis in (0, 2):
        for edge, coordinate in (
            ("minimum", floor_minimum[axis]),
            ("maximum", floor_maximum[axis]),
        ):
            boundary_candidates.append(
                (abs(float(doorway_center[axis]) - coordinate), axis, edge, coordinate)
            )
    boundary_candidates.sort(key=lambda candidate: candidate[:3])
    nearest = boundary_candidates[0]
    if math.isclose(
        nearest[0], boundary_candidates[1][0], rel_tol=0.0, abs_tol=1e-4
    ):
        raise ValueError("doorway does not resolve to one unique floor boundary")

    socket_location = doorway_center.copy()
    socket_location[nearest[1]] = nearest[3]
    other_horizontal_axis = 2 if nearest[1] == 0 else 0
    socket_location[other_horizontal_axis] = max(
        floor_minimum[other_horizontal_axis],
        min(floor_maximum[other_horizontal_axis], socket_location[other_horizontal_axis]),
    )
    socket_location[1] = floor_maximum[1]
    for axis in (0, 2):
        if not (
            doorway_minimum[axis] - 1e-4
            <= socket_location[axis]
            <= doorway_maximum[axis] + 1e-4
        ):
            raise ValueError("derived entrance does not overlap DoorwayModule bounds")

    inward = floor_center - socket_location
    inward[1] = 0.0
    if inward.length <= POSITION_TOLERANCE:
        raise ValueError("derived entrance direction is degenerate")
    inward.normalize()
    up = Vector((0.0, 1.0, 0.0))
    right = up.cross(inward)
    if right.length <= POSITION_TOLERANCE:
        raise ValueError("derived entrance basis is degenerate")
    right.normalize()
    socket_matrix = Matrix((right, up, inward)).transposed().to_4x4()
    socket_matrix.translation = socket_location

    x_minimum, y_minimum, z_minimum = floor_minimum
    x_maximum, y_maximum, z_maximum = floor_maximum
    collision_vertices = [
        (x_minimum, y_minimum, z_minimum),
        (x_maximum, y_minimum, z_minimum),
        (x_maximum, y_maximum, z_minimum),
        (x_minimum, y_maximum, z_minimum),
        (x_minimum, y_minimum, z_maximum),
        (x_maximum, y_minimum, z_maximum),
        (x_maximum, y_maximum, z_maximum),
        (x_minimum, y_maximum, z_maximum),
    ]
    collision_faces = [
        (0, 3, 2, 1),
        (4, 5, 6, 7),
        (0, 1, 5, 4),
        (3, 7, 6, 2),
        (0, 4, 7, 3),
        (1, 2, 6, 5),
    ]
    navigation_vertices = [
        (x_minimum, y_maximum, z_minimum),
        (x_minimum, y_maximum, z_maximum),
        (x_maximum, y_maximum, z_maximum),
        (x_maximum, y_maximum, z_minimum),
    ]
    return {
        "floorBounds": [floor_minimum, floor_maximum],
        "doorwayBounds": [doorway_minimum, doorway_maximum],
        "entranceBoundary": {
            "axis": "X" if nearest[1] == 0 else "Z",
            "edge": nearest[2],
            "distanceFromDoorwayCenter": nearest[0],
        },
        "collisionVertices": collision_vertices,
        "collisionFaces": collision_faces,
        "navigationVertices": navigation_vertices,
        "navigationFaces": [(0, 1, 2, 3)],
        "socketLocation": socket_location,
        "socketForward": inward,
        "socketMatrix": socket_matrix,
    }


def _technical_properties(
    obj: bpy.types.Object, source_id: str, role: str, derived_from: str, method: str
) -> None:
    obj["al_schema_version"] = 1
    obj["al_asset_source_id"] = source_id
    obj["al_local_element_id"] = obj.name
    obj["al_role"] = role
    obj["al_derived_from"] = derived_from
    obj["al_derivation"] = method


def _new_collection(name: str, source_id: str) -> bpy.types.Collection:
    collection = bpy.data.collections.new(name)
    collection["al_schema_version"] = 1
    collection["al_asset_source_id"] = source_id
    bpy.context.scene.collection.children.link(collection)
    return collection


def _new_mesh_object(
    name: str,
    collection: bpy.types.Collection,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
) -> bpy.types.Object:
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


def _create_helpers(source_id: str, plan: dict[str, Any]) -> None:
    collision_collection = _new_collection(COLLISION_COLLECTION, source_id)
    navigation_collection = _new_collection(NAVIGATION_COLLECTION, source_id)
    socket_collection = _new_collection(SOCKET_COLLECTION, source_id)

    collision = _new_mesh_object(
        COLLISION_NAME,
        collision_collection,
        plan["collisionVertices"],
        plan["collisionFaces"],
    )
    _technical_properties(
        collision,
        source_id,
        "collision.walkable-floor",
        FLOOR_NAME,
        "exact-world-bounds",
    )
    navigation = _new_mesh_object(
        NAVIGATION_NAME,
        navigation_collection,
        plan["navigationVertices"],
        plan["navigationFaces"],
    )
    _technical_properties(
        navigation,
        source_id,
        "navigation.walkable-source",
        FLOOR_NAME,
        "finished-floor-bounds",
    )
    socket = bpy.data.objects.new(SOCKET_NAME, None)
    socket_collection.objects.link(socket)
    socket.matrix_world = plan["socketMatrix"]
    socket.empty_display_type = "ARROWS"
    socket.empty_display_size = 0.5
    socket.show_in_front = True
    socket.hide_render = True
    _technical_properties(
        socket,
        source_id,
        "socket.entrance",
        f"{DOORWAY_NAME},{FLOOR_NAME}",
        "nearest-overlapping-floor-boundary",
    )


def _positions_match(actual: list[Any], expected: list[Any]) -> bool:
    actual_sorted = sorted(tuple(_round_vector(value)) for value in actual)
    expected_sorted = sorted(tuple(_round_vector(value)) for value in expected)
    return actual_sorted == expected_sorted


def _helper_contract_problems(
    source_id: str, plan: dict[str, Any]
) -> list[str]:
    problems: list[str] = []
    expected = {
        COLLISION_NAME: (
            "MESH",
            COLLISION_COLLECTION,
            "collision.walkable-floor",
            FLOOR_NAME,
            "exact-world-bounds",
        ),
        NAVIGATION_NAME: (
            "MESH",
            NAVIGATION_COLLECTION,
            "navigation.walkable-source",
            FLOOR_NAME,
            "finished-floor-bounds",
        ),
        SOCKET_NAME: (
            "EMPTY",
            SOCKET_COLLECTION,
            "socket.entrance",
            f"{DOORWAY_NAME},{FLOOR_NAME}",
            "nearest-overlapping-floor-boundary",
        ),
    }
    for name, contract in expected.items():
        object_type, collection_name, role, derived_from, method = contract
        obj = bpy.data.objects.get(name)
        if obj is None:
            problems.append(f"missing object {name}")
            continue
        if obj.type != object_type:
            problems.append(f"{name} type is {obj.type}, expected {object_type}")
        if collection_name not in {
            collection.name for collection in obj.users_collection
        }:
            problems.append(f"{name} is not linked to {collection_name}")
        if not obj.hide_render:
            problems.append(f"{name} is render-visible")
        if obj.get("al_asset_source_id") != source_id:
            problems.append(f"{name} has the wrong source id")
        if obj.get("al_local_element_id") != name:
            problems.append(f"{name} has the wrong local element id")
        expected_properties = {
            "al_schema_version": 1,
            "al_asset_source_id": source_id,
            "al_local_element_id": name,
            "al_role": role,
            "al_derived_from": derived_from,
            "al_derivation": method,
        }
        for property_name, expected_value in expected_properties.items():
            if obj.get(property_name) != expected_value:
                problems.append(f"{name} has invalid {property_name}")

    for name in TARGET_COLLECTIONS:
        collection = bpy.data.collections.get(name)
        if collection is None:
            continue
        if collection.get("al_schema_version") != 1:
            problems.append(f"{name} has invalid al_schema_version")
        if collection.get("al_asset_source_id") != source_id:
            problems.append(f"{name} has invalid al_asset_source_id")
        if collection.users <= 0:
            problems.append(f"{name} is not linked into the source")

    collision = bpy.data.objects.get(COLLISION_NAME)
    if collision is not None and collision.type == "MESH":
        collision_positions = [
            collision.matrix_world @ vertex.co for vertex in collision.data.vertices
        ]
        if not _positions_match(collision_positions, plan["collisionVertices"]):
            problems.append("collision proxy no longer matches FloorModule bounds")
        if len(collision.data.polygons) != len(plan["collisionFaces"]):
            problems.append("collision proxy face count differs from the plan")

    navigation = bpy.data.objects.get(NAVIGATION_NAME)
    if navigation is not None and navigation.type == "MESH":
        navigation_positions = [
            navigation.matrix_world @ vertex.co for vertex in navigation.data.vertices
        ]
        if not _positions_match(navigation_positions, plan["navigationVertices"]):
            problems.append("navigation source no longer matches finished-floor bounds")
        minimum_upward = min(
            (navigation.matrix_world.to_3x3() @ polygon.normal).y
            for polygon in navigation.data.polygons
        )
        if minimum_upward <= 1e-6:
            problems.append("navigation source does not have upward Y normals")

    socket = bpy.data.objects.get(SOCKET_NAME)
    if socket is not None:
        matrix_delta = max(
            abs(
                float(socket.matrix_world[row][column])
                - float(plan["socketMatrix"][row][column])
            )
            for row in range(4)
            for column in range(4)
        )
        if matrix_delta > POSITION_TOLERANCE:
            problems.append("entrance socket no longer matches the derived boundary transform")
    return problems


def _json_plan(plan: dict[str, Any]) -> dict[str, Any]:
    return {
        "floorBounds": [
            _round_vector(plan["floorBounds"][0]),
            _round_vector(plan["floorBounds"][1]),
        ],
        "doorwayBounds": [
            _round_vector(plan["doorwayBounds"][0]),
            _round_vector(plan["doorwayBounds"][1]),
        ],
        "entranceBoundary": {
            **plan["entranceBoundary"],
            "distanceFromDoorwayCenter": round(
                float(plan["entranceBoundary"]["distanceFromDoorwayCenter"]), 9
            ),
        },
        "collision": {
            "name": COLLISION_NAME,
            "collection": COLLISION_COLLECTION,
            "vertices": len(plan["collisionVertices"]),
            "faces": len(plan["collisionFaces"]),
        },
        "navigation": {
            "name": NAVIGATION_NAME,
            "collection": NAVIGATION_COLLECTION,
            "vertices": len(plan["navigationVertices"]),
            "faces": len(plan["navigationFaces"]),
        },
        "entranceSocket": {
            "name": SOCKET_NAME,
            "collection": SOCKET_COLLECTION,
            "location": _round_vector(plan["socketLocation"]),
            "forward": _round_vector(plan["socketForward"]),
            "matrixWorld": _round_matrix(plan["socketMatrix"]),
        },
    }


def _write_report(path: Path, report: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )


def main() -> int:
    args = _arguments()
    manifest_path = _resolve(args.manifest)
    output_path = _resolve(args.output)
    if output_path.exists():
        print("AL Blender remediation: refusing to overwrite report", file=sys.stderr)
        return 4
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest_errors = _manifest_diagnostics(manifest)
    if manifest_errors:
        print("AL Blender remediation: invalid manifest", file=sys.stderr)
        return 1
    source = next(
        (item for item in manifest["sources"] if item["id"] == args.source), None
    )
    if source is None:
        print(f"AL Blender remediation: unknown source {args.source}", file=sys.stderr)
        return 4
    if source["id"] != SUPPORTED_SOURCE_ID:
        report = {
            "schemaVersion": 1,
            "status": "blocked_no_objective_plan",
            "planId": PLAN_ID,
            "sourceId": source["id"],
            "reason": "No source-specific, aesthetic-preserving repair plan is approved.",
        }
        _write_report(output_path, report)
        print("AL Blender remediation: no objective plan for source", file=sys.stderr)
        return 2
    if source["sourceUpAxis"] != "Y":
        print("AL Blender remediation: hall plan requires its declared Y-up exception")
        return 1

    source_path = REPOSITORY_ROOT / source["path"]
    actual_hash = _sha256(source_path)
    if actual_hash != source["sha256"]:
        report = {
            "schemaVersion": 1,
            "status": "blocked_source_hash_drift",
            "planId": PLAN_ID,
            "sourceId": source["id"],
            "expectedSha256": source["sha256"],
            "actualSha256": actual_hash,
        }
        _write_report(output_path, report)
        print("AL Blender remediation: source hash drift", file=sys.stderr)
        return 1

    validation_before = _validate_source(source, manifest)
    if validation_before["errors"]:
        report = {
            "schemaVersion": 1,
            "status": "blocked_source_errors",
            "planId": PLAN_ID,
            "sourceId": source["id"],
            "errors": validation_before["errors"],
        }
        _write_report(output_path, report)
        print("AL Blender remediation: source has hard errors", file=sys.stderr)
        return 1

    try:
        plan = _derived_plan()
    except ValueError as error:
        report = {
            "schemaVersion": 1,
            "status": "blocked_ambiguous_derivation",
            "planId": PLAN_ID,
            "sourceId": source["id"],
            "reason": str(error),
        }
        _write_report(output_path, report)
        print(f"AL Blender remediation: {error}", file=sys.stderr)
        return 2

    render_hash_before, render_snapshot_before = _render_snapshot(source)
    existing_objects = sorted(name for name in TARGET_OBJECTS if bpy.data.objects.get(name))
    existing_collections = sorted(
        name for name in TARGET_COLLECTIONS if bpy.data.collections.get(name)
    )
    base_report: dict[str, Any] = {
        "schemaVersion": 1,
        "planId": PLAN_ID,
        "sourceId": source["id"],
        "sourcePath": source["path"],
        "sourceSha256Before": actual_hash,
        "manifestSha256": _sha256(manifest_path),
        "blenderVersion": bpy.app.version_string,
        "toolSha256": _sha256(SCRIPT_PATH),
        "applied": bool(args.apply),
        "derivedPlan": _json_plan(plan),
        "renderSnapshotSha256Before": render_hash_before,
        "validationBefore": {
            "status": validation_before["status"],
            "errors": len(validation_before["errors"]),
            "promotionGaps": len(validation_before["gaps"]),
        },
    }
    all_helpers_exist = len(existing_objects) == len(TARGET_OBJECTS) and len(
        existing_collections
    ) == len(TARGET_COLLECTIONS)
    if existing_objects or existing_collections:
        if not all_helpers_exist:
            base_report.update(
                {
                    "status": "blocked_partial_or_conflicting_repair",
                    "existingObjects": existing_objects,
                    "existingCollections": existing_collections,
                }
            )
            _write_report(output_path, base_report)
            print("AL Blender remediation: partial/conflicting repair", file=sys.stderr)
            return 2
        problems = _helper_contract_problems(source["id"], plan)
        if problems:
            base_report.update(
                {
                    "status": "blocked_conflicting_repair",
                    "problems": problems,
                }
            )
            _write_report(output_path, base_report)
            print("AL Blender remediation: conflicting helpers", file=sys.stderr)
            return 2
        base_report.update(
            {
                "status": "already_compliant",
                "sourceSha256After": actual_hash,
                "renderSnapshotSha256After": render_hash_before,
                "renderSnapshotUnchanged": True,
            }
        )
        _write_report(output_path, base_report)
        print("AL Blender remediation: source is already compliant")
        return 0

    if not args.apply:
        base_report["status"] = "ready_to_apply"
        _write_report(output_path, base_report)
        print("AL Blender remediation: objective plan is ready; source was not saved")
        return 0

    _create_helpers(source["id"], plan)
    problems = _helper_contract_problems(source["id"], plan)
    render_hash_after_memory, render_snapshot_after = _render_snapshot(source)
    if render_snapshot_after != render_snapshot_before:
        problems.append("manifest-selected render objects changed in memory")
    if problems:
        base_report.update(
            {
                "status": "blocked_in_memory_postcondition",
                "problems": problems,
                "renderSnapshotSha256After": render_hash_after_memory,
            }
        )
        _write_report(output_path, base_report)
        print("AL Blender remediation: in-memory postcondition failed", file=sys.stderr)
        return 1

    bpy.context.preferences.filepaths.save_version = 0
    save_result = bpy.ops.wm.save_as_mainfile(
        filepath=str(source_path),
        check_existing=False,
        compress=True,
    )
    if "FINISHED" not in save_result:
        base_report["status"] = "save_failed"
        _write_report(output_path, base_report)
        print("AL Blender remediation: source save failed", file=sys.stderr)
        return 1

    after_hash = _sha256(source_path)
    source_after = copy.deepcopy(source)
    source_after["sha256"] = after_hash
    validation_after = _validate_source(source_after, manifest)
    render_hash_after, render_snapshot_after_reopen = _render_snapshot(source_after)
    render_unchanged = render_snapshot_after_reopen == render_snapshot_before
    base_report.update(
        {
            "status": (
                "applied_valid"
                if not validation_after["errors"]
                and not validation_after["gaps"]
                and render_unchanged
                else "applied_but_invalid"
            ),
            "sourceSha256After": after_hash,
            "renderSnapshotSha256After": render_hash_after,
            "renderSnapshotUnchanged": render_unchanged,
            "validationAfter": {
                "status": validation_after["status"],
                "errors": validation_after["errors"],
                "promotionGaps": validation_after["gaps"],
            },
        }
    )
    _write_report(output_path, base_report)
    success = base_report["status"] == "applied_valid"
    print(
        "AL Blender remediation: "
        f"{base_report['status']}; sourceSha256={after_hash}; "
        f"renderUnchanged={render_unchanged}"
    )
    return 0 if success else 1


if __name__ == "__main__":
    raise SystemExit(main())
