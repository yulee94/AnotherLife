"""Preview whether applying Blender object transforms is mechanically safe.

This tool never saves a .blend file. It applies the manifest-declared transform
normalization only in memory, then compares render vertices, shape keys, rest
bones, attachment sockets, and sampled animated pose matrices. A candidate is
safe to automate only when all protected world-space results remain within the
declared tolerance.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path
from typing import Any

import bpy

SCRIPT_PATH = Path(__file__).resolve()
if str(SCRIPT_PATH.parent) not in sys.path:
    sys.path.insert(0, str(SCRIPT_PATH.parent))

from validate_al_asset_sources import (
    DEFAULT_MANIFEST,
    EXPORTABLE_TYPES,
    REPOSITORY_ROOT,
    _manifest_diagnostics,
    _resolved_lods,
    _sha256,
    _validate_source,
)

DEFAULT_TOLERANCE_METERS = 1e-4


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", type=Path)
    parser.add_argument(
        "--tolerance-meters", type=float, default=DEFAULT_TOLERANCE_METERS
    )
    parser.add_argument("--require-safe", action="store_true")
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(blender_args)


def _resolve(path: Path) -> Path:
    return path if path.is_absolute() else REPOSITORY_ROOT / path


def _matrix_delta(left: Any, right: Any) -> float:
    return max(
        abs(float(left[row][column]) - float(right[row][column]))
        for row in range(4)
        for column in range(4)
    )


def _sample_frames(action: bpy.types.Action | None) -> list[int]:
    if action is None:
        return [int(bpy.context.scene.frame_current)]
    start = math.floor(float(action.frame_range[0]))
    end = math.ceil(float(action.frame_range[1]))
    distance = end - start
    return sorted(
        {
            start,
            start + round(distance * 0.25),
            start + round(distance * 0.50),
            start + round(distance * 0.75),
            end,
        }
    )


def _pose_matrices(
    armature: bpy.types.Object, frames: list[int]
) -> dict[int, dict[str, Any]]:
    result: dict[int, dict[str, Any]] = {}
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        result[frame] = {
            bone.name: (armature.matrix_world @ bone.matrix).copy()
            for bone in armature.pose.bones
        }
    return result


def _vertex_positions(obj: bpy.types.Object) -> list[Any]:
    return [(obj.matrix_world @ vertex.co).copy() for vertex in obj.data.vertices]


def _shape_positions(obj: bpy.types.Object) -> dict[str, list[Any]]:
    if obj.data.shape_keys is None:
        return {}
    return {
        block.name: [(obj.matrix_world @ point.co).copy() for point in block.data]
        for block in obj.data.shape_keys.key_blocks
    }


def _evaluated_positions(
    objects: list[bpy.types.Object], frames: list[int]
) -> dict[int, dict[str, list[Any]]]:
    """Sample final skinned/deformed vertices, not only source mesh bases."""
    result: dict[int, dict[str, list[Any]]] = {}
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        depsgraph = bpy.context.evaluated_depsgraph_get()
        result[frame] = {}
        for obj in objects:
            evaluated = obj.evaluated_get(depsgraph)
            mesh = evaluated.to_mesh(
                preserve_all_data_layers=False,
                depsgraph=depsgraph,
            )
            try:
                result[frame][obj.name] = [
                    (evaluated.matrix_world @ vertex.co).copy()
                    for vertex in mesh.vertices
                ]
            finally:
                evaluated.to_mesh_clear()
    return result


def main() -> int:
    args = _arguments()
    manifest_path = _resolve(args.manifest)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest_errors = _manifest_diagnostics(manifest)
    if manifest_errors:
        print("AL transform preview: invalid manifest", file=sys.stderr)
        return 1

    source = next(
        (item for item in manifest["sources"] if item["id"] == args.source), None
    )
    if source is None:
        print(f"AL transform preview: unknown source {args.source}", file=sys.stderr)
        return 1
    if not source.get("armature"):
        print("AL transform preview: source has no armature contract", file=sys.stderr)
        return 1

    validation = _validate_source(source, manifest)
    armature = bpy.data.objects.get(source["armature"]["object"])
    if armature is None or armature.type != "ARMATURE":
        print("AL transform preview: armature is unavailable", file=sys.stderr)
        return 1

    lods = _resolved_lods(source)
    skinned_names: list[str] = []
    for lod_id in source["armature"]["skinnedObjectsFromLods"]:
        skinned_names.extend(lods[lod_id])
    skinned_objects = [
        bpy.data.objects[name]
        for name in sorted(set(skinned_names))
        if bpy.data.objects.get(name) is not None
    ]
    sockets = [
        bpy.data.objects[name]
        for name in source.get("requiredObjects", [])
        if bpy.data.objects.get(name) is not None
        and bpy.data.objects[name].type == "EMPTY"
    ]

    base_vertices = {obj.name: _vertex_positions(obj) for obj in skinned_objects}
    base_shapes = {obj.name: _shape_positions(obj) for obj in skinned_objects}
    base_rest_bones = {
        bone.name: (
            (armature.matrix_world @ bone.head_local).copy(),
            (armature.matrix_world @ bone.tail_local).copy(),
        )
        for bone in armature.data.bones
    }
    base_sockets = {obj.name: obj.matrix_world.copy() for obj in sockets}
    action = armature.animation_data.action if armature.animation_data else None
    frames = _sample_frames(action)
    base_pose = _pose_matrices(armature, frames)
    base_evaluated = _evaluated_positions(skinned_objects, frames)

    normalize_names = set(source.get("identityRotationObjects", []))
    if source.get("identityScaleObjects") == "all-exportable":
        normalize_names.update(
            obj.name
            for obj in bpy.data.objects
            if obj.type in EXPORTABLE_TYPES
            and any(abs(float(component) - 1.0) > 1e-5 for component in obj.scale)
        )
    normalize_objects = [
        bpy.data.objects[name]
        for name in sorted(normalize_names)
        if bpy.data.objects.get(name) is not None
    ]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in normalize_objects:
        obj.select_set(True)
    if normalize_objects:
        bpy.context.view_layer.objects.active = normalize_objects[0]
        result = bpy.ops.object.transform_apply(
            location=False, rotation=True, scale=True
        )
        if "FINISHED" not in result:
            print("AL transform preview: transform_apply failed", file=sys.stderr)
            return 1

    vertex_delta = 0.0
    shape_delta = 0.0
    for obj in skinned_objects:
        vertex_delta = max(
            vertex_delta,
            max(
                (
                    (
                        obj.matrix_world @ vertex.co - base_vertices[obj.name][index]
                    ).length
                    for index, vertex in enumerate(obj.data.vertices)
                ),
                default=0.0,
            ),
        )
        for block_name, baseline in base_shapes[obj.name].items():
            block = obj.data.shape_keys.key_blocks[block_name]
            shape_delta = max(
                shape_delta,
                max(
                    (
                        (obj.matrix_world @ point.co - baseline[index]).length
                        for index, point in enumerate(block.data)
                    ),
                    default=0.0,
                ),
            )

    rest_bone_delta = max(
        (
            max(
                (
                    armature.matrix_world @ bone.head_local
                    - base_rest_bones[bone.name][0]
                ).length,
                (
                    armature.matrix_world @ bone.tail_local
                    - base_rest_bones[bone.name][1]
                ).length,
            )
            for bone in armature.data.bones
        ),
        default=0.0,
    )
    socket_matrix_delta = max(
        (_matrix_delta(obj.matrix_world, base_sockets[obj.name]) for obj in sockets),
        default=0.0,
    )
    pose_delta_by_frame: dict[str, float] = {}
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        pose_delta_by_frame[str(frame)] = max(
            (
                _matrix_delta(
                    armature.matrix_world @ bone.matrix,
                    base_pose[frame][bone.name],
                )
                for bone in armature.pose.bones
            ),
            default=0.0,
        )
    maximum_pose_delta = max(pose_delta_by_frame.values(), default=0.0)
    evaluated_after = _evaluated_positions(skinned_objects, frames)
    evaluated_delta_by_frame: dict[str, float] = {}
    evaluated_vertex_counts_match = True
    for frame in frames:
        frame_delta = 0.0
        for obj in skinned_objects:
            baseline = base_evaluated[frame][obj.name]
            actual = evaluated_after[frame][obj.name]
            if len(actual) != len(baseline):
                evaluated_vertex_counts_match = False
                continue
            frame_delta = max(
                frame_delta,
                max(
                    (
                        (actual[index] - baseline[index]).length
                        for index in range(len(actual))
                    ),
                    default=0.0,
                ),
            )
        evaluated_delta_by_frame[str(frame)] = frame_delta
    maximum_evaluated_delta = max(evaluated_delta_by_frame.values(), default=0.0)

    normalized_transforms = {
        obj.name: {
            "rotationEuler": [float(value) for value in obj.rotation_euler],
            "scale": [float(value) for value in obj.scale],
        }
        for obj in normalize_objects
    }
    transforms_are_identity = all(
        all(abs(value) <= 1e-5 for value in values["rotationEuler"])
        and all(abs(value - 1.0) <= 1e-5 for value in values["scale"])
        for values in normalized_transforms.values()
    )
    tolerance = float(args.tolerance_meters)
    safe = (
        transforms_are_identity
        and vertex_delta <= tolerance
        and shape_delta <= tolerance
        and rest_bone_delta <= tolerance
        and socket_matrix_delta <= tolerance
        and maximum_pose_delta <= tolerance
        and evaluated_vertex_counts_match
        and maximum_evaluated_delta <= tolerance
    )
    report = {
        "schemaVersion": 1,
        "status": "safe_to_automate" if safe else "manual_rebake_required",
        "sourceId": source["id"],
        "sourcePath": source["path"],
        "sourceSha256": source["sha256"],
        "manifestSha256": _sha256(manifest_path),
        "blenderVersion": bpy.app.version_string,
        "validationStatusBeforePreview": validation["status"],
        "normalizedObjects": normalized_transforms,
        "protectedEvidence": {
            "skinnedMeshes": [obj.name for obj in skinned_objects],
            "shapeKeys": base_shapes.keys(),
            "sockets": [obj.name for obj in sockets],
            "action": action.name if action else None,
            "sampledFrames": frames,
            "evaluatedVertexCounts": {
                str(frame): {
                    name: len(vertices)
                    for name, vertices in base_evaluated[frame].items()
                }
                for frame in frames
            },
        },
        "deltas": {
            "maximumBaseVertexMeters": vertex_delta,
            "maximumShapeKeyVertexMeters": shape_delta,
            "maximumRestBoneMeters": rest_bone_delta,
            "maximumSocketMatrixComponent": socket_matrix_delta,
            "maximumPoseMatrixComponent": maximum_pose_delta,
            "poseMatrixComponentByFrame": pose_delta_by_frame,
            "evaluatedVertexCountsMatch": evaluated_vertex_counts_match,
            "maximumEvaluatedVertexMeters": maximum_evaluated_delta,
            "evaluatedVertexMetersByFrame": evaluated_delta_by_frame,
        },
        "tolerance": tolerance,
        "safeToAutomate": safe,
    }
    # Convert dict_keys before JSON serialization while retaining a stable order.
    report["protectedEvidence"]["shapeKeys"] = {
        name: sorted(blocks.keys()) for name, blocks in base_shapes.items()
    }
    serialized = json.dumps(report, indent=2, sort_keys=True) + "\n"
    if args.output:
        output = _resolve(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(serialized, encoding="utf-8")
        print(f"AL transform preview report: {output}")
    else:
        print(serialized, end="")
    print(
        f"AL transform preview: {report['status']}; "
        f"poseDelta={maximum_pose_delta:.6f}; "
        f"socketDelta={socket_matrix_delta:.6f}"
    )
    return 0 if safe or not args.require_safe else 2


if __name__ == "__main__":
    raise SystemExit(main())
