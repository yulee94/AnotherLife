"""Validate retained AnotherLife Blender sources without modifying them.

Run from any checkout location with Blender 5.2 or newer:

    blender --background --python-exit-code 1 \
      --python tools/blender/validate_al_asset_sources.py -- \
      --output archive/local-run/blender/al-blender-source-validation.json

The validator distinguishes hard source-contract errors from known promotion gaps.
By default only hard errors produce a non-zero exit code; pass ``--fail-on-gaps``
when evaluating production promotion rather than continued MVP iteration.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import sys
from collections.abc import Iterable
from pathlib import Path
from typing import Any

import bpy
from mathutils import Vector

SCRIPT_PATH = Path(__file__).resolve()
REPOSITORY_ROOT = SCRIPT_PATH.parents[2]
DEFAULT_MANIFEST = (
    REPOSITORY_ROOT / "unity" / "ArtSource" / "al_blender_source_validation.v1.json"
)
AXIS_INDEX = {"X": 0, "Y": 1, "Z": 2}
EXPORTABLE_TYPES = {"MESH", "ARMATURE", "EMPTY"}
SOURCE_ID_PATTERN = re.compile(r"^[a-z0-9]+(?:[-_][a-z0-9+]+)*$")
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
PROMOTION_OBJECT_CONTRACTS = {
    "COL_": ("MESH", "AL_COLLISION"),
    "NAV_": ("MESH", "AL_NAVIGATION"),
    "NAVEX_": ("MESH", "AL_NAVIGATION"),
    "SOCKET_": ("EMPTY", "AL_SOCKETS"),
}
TECHNICAL_OBJECT_PROPERTIES = (
    "al_asset_source_id",
    "al_derivation",
    "al_derived_from",
    "al_local_element_id",
    "al_role",
    "al_schema_version",
)


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--output", type=Path)
    parser.add_argument(
        "--source",
        action="append",
        default=[],
        help="Validate only this source id. May be supplied more than once.",
    )
    parser.add_argument("--fail-on-gaps", action="store_true")
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(blender_args)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _issue(code: str, message: str, **evidence: Any) -> dict[str, Any]:
    result: dict[str, Any] = {"code": code, "message": message}
    if evidence:
        result["evidence"] = evidence
    return result


def _close_enough(actual: float, expected: float, tolerance: float) -> bool:
    return math.isclose(actual, expected, rel_tol=0.0, abs_tol=tolerance)


def _manifest_diagnostics(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    """Validate cross-field rules that JSON Schema cannot express portably."""
    diagnostics: list[dict[str, Any]] = []
    if manifest.get("schemaVersion") != 1:
        diagnostics.append(
            _issue("manifest_schema_version", "Manifest schemaVersion must be 1.")
        )

    sources = manifest.get("sources")
    if not isinstance(sources, list) or not sources:
        return diagnostics + [
            _issue("manifest_sources", "Manifest must declare at least one source.")
        ]

    ids: set[str] = set()
    paths: set[str] = set()
    for index, source in enumerate(sources):
        source_id = source.get("id", "")
        path = source.get("path", "")
        if not isinstance(source_id, str) or not SOURCE_ID_PATTERN.fullmatch(source_id):
            diagnostics.append(
                _issue(
                    "manifest_source_id",
                    "Source id must use stable lowercase kebab/snake tokens.",
                    sourceIndex=index,
                    value=source_id,
                )
            )
        elif source_id in ids:
            diagnostics.append(
                _issue(
                    "manifest_source_id_duplicate",
                    "Source ids must be unique.",
                    id=source_id,
                )
            )
        ids.add(source_id)

        if (
            not isinstance(path, str)
            or not path.startswith("unity/ArtSource/")
            or ".." in Path(path).parts
        ):
            diagnostics.append(
                _issue(
                    "manifest_source_path",
                    "Source path must stay below unity/ArtSource.",
                    id=source_id,
                    path=path,
                )
            )
        elif path in paths:
            diagnostics.append(
                _issue(
                    "manifest_source_path_duplicate",
                    "Source paths must be unique.",
                    path=path,
                )
            )
        paths.add(path)

        sha256 = source.get("sha256", "")
        if not isinstance(sha256, str) or not SHA256_PATTERN.fullmatch(sha256):
            diagnostics.append(
                _issue(
                    "manifest_source_hash",
                    "Source sha256 must be 64 lowercase hexadecimal characters.",
                    id=source_id,
                )
            )

        lod_ids: set[str] = set()
        for lod in source.get("lods", []):
            lod_id = lod.get("id", "")
            if lod_id in lod_ids:
                diagnostics.append(
                    _issue(
                        "manifest_lod_duplicate",
                        "LOD ids must be unique within a source.",
                        id=source_id,
                        lod=lod_id,
                    )
                )
            lod_ids.add(lod_id)
            if int(lod.get("minimumTriangles", 0)) > int(
                lod.get("maximumTriangles", 0)
            ):
                diagnostics.append(
                    _issue(
                        "manifest_lod_budget",
                        "LOD minimumTriangles may not exceed maximumTriangles.",
                        id=source_id,
                        lod=lod_id,
                    )
                )

        export_ids: set[str] = set()
        for export_set in source.get("exportSets", []):
            export_id = export_set.get("id", "")
            if export_id in export_ids:
                diagnostics.append(
                    _issue(
                        "manifest_export_set_duplicate",
                        "Export-set ids must be unique within a source.",
                        id=source_id,
                        exportSet=export_id,
                    )
                )
            export_ids.add(export_id)
            unknown_lods = sorted(set(export_set.get("objectsFromLods", [])) - lod_ids)
            if unknown_lods:
                diagnostics.append(
                    _issue(
                        "manifest_export_set_lod",
                        "Export set references unknown LOD ids.",
                        id=source_id,
                        exportSet=export_id,
                        lods=unknown_lods,
                    )
                )

        armature = source.get("armature")
        if armature and int(armature.get("minimumBones", 0)) > int(
            armature.get("maximumBones", 0)
        ):
            diagnostics.append(
                _issue(
                    "manifest_bone_budget",
                    "minimumBones may not exceed maximumBones.",
                    id=source_id,
                )
            )

    return diagnostics


def _object_bounds(names: Iterable[str]) -> tuple[list[float], list[float]] | None:
    points: list[Vector] = []
    for name in names:
        obj = bpy.data.objects.get(name)
        if obj is None or obj.type != "MESH":
            continue
        points.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    if not points:
        return None
    minimum = [min(point[index] for point in points) for index in range(3)]
    maximum = [max(point[index] for point in points) for index in range(3)]
    return minimum, maximum


def _centroid_coordinate(object_name: str, axis: str) -> float | None:
    obj = bpy.data.objects.get(object_name)
    if obj is None or obj.type != "MESH" or not obj.data.vertices:
        return None
    index = AXIS_INDEX[axis]
    coordinates = [
        (obj.matrix_world @ vertex.co)[index] for vertex in obj.data.vertices
    ]
    return sum(coordinates) / len(coordinates)


def _promotion_object_contract(name: str) -> tuple[str, str] | None:
    for prefix, contract in PROMOTION_OBJECT_CONTRACTS.items():
        if name.startswith(prefix):
            return contract
    return None


def _promotion_object_metrics(
    source: dict[str, Any],
    object_names: Iterable[str],
    gaps: list[dict[str, Any]],
) -> dict[str, Any]:
    """Validate engine-neutral promotion helpers without making them runtime data."""
    metrics: dict[str, Any] = {}
    up_axis = source["sourceUpAxis"]
    up_index = AXIS_INDEX[up_axis]
    for name in sorted(object_names):
        obj = bpy.data.objects.get(name)
        if obj is None:
            continue
        contract = _promotion_object_contract(name)
        if contract is None:
            continue
        expected_type, expected_collection = contract
        collections = sorted(collection.name for collection in obj.users_collection)
        problems: dict[str, Any] = {}
        if obj.type != expected_type:
            problems["expectedType"] = expected_type
            problems["actualType"] = obj.type
        if expected_collection not in collections:
            problems["expectedCollection"] = expected_collection
            problems["actualCollections"] = collections
        if not obj.hide_render:
            problems["hideRender"] = False
        missing_properties = sorted(
            property_name
            for property_name in TECHNICAL_OBJECT_PROPERTIES
            if property_name not in obj
        )
        if missing_properties:
            problems["missingTechnicalProperties"] = missing_properties
        if obj.get("al_asset_source_id") not in (None, source["id"]):
            problems["assetSourceId"] = obj.get("al_asset_source_id")
        if obj.get("al_local_element_id") not in (None, name):
            problems["localElementId"] = obj.get("al_local_element_id")

        object_metric: dict[str, Any] = {
            "type": obj.type,
            "collections": collections,
            "hideRender": bool(obj.hide_render),
            "worldBounds": _object_bounds([name]),
            "technicalProperties": {
                property_name: obj.get(property_name)
                for property_name in TECHNICAL_OBJECT_PROPERTIES
                if property_name in obj
            },
        }
        if obj.type == "MESH":
            vertices = len(obj.data.vertices)
            polygons = len(obj.data.polygons)
            object_metric.update(
                {
                    "vertices": vertices,
                    "polygons": polygons,
                    "materials": len(obj.data.materials),
                }
            )
            if vertices == 0 or polygons == 0:
                problems["emptyGeometry"] = {
                    "vertices": vertices,
                    "polygons": polygons,
                }
            if name.startswith("NAV_") and polygons:
                normal_matrix = obj.matrix_world.to_3x3().inverted_safe().transposed()
                upward_components: list[float] = []
                for polygon in obj.data.polygons:
                    world_normal = normal_matrix @ polygon.normal
                    if world_normal.length_squared > 0.0:
                        world_normal.normalize()
                    upward_components.append(float(world_normal[up_index]))
                minimum_upward = min(upward_components)
                object_metric["minimumUpwardNormal"] = minimum_upward
                if minimum_upward <= 1e-6:
                    problems["minimumUpwardNormal"] = {
                        "axis": up_axis,
                        "actual": minimum_upward,
                        "minimumExclusive": 1e-6,
                    }
        metrics[name] = object_metric
        if problems:
            gaps.append(
                _issue(
                    "promotion_object_contract",
                    "Production-promotion helper violates its technical contract.",
                    object=name,
                    **problems,
                )
            )
    return metrics


def _promotion_collection_metrics(
    source: dict[str, Any],
    collection_names: Iterable[str],
    gaps: list[dict[str, Any]],
) -> dict[str, Any]:
    metrics: dict[str, Any] = {}
    for name in sorted(collection_names):
        collection = bpy.data.collections.get(name)
        if collection is None:
            continue
        problems: dict[str, Any] = {}
        if collection.get("al_schema_version") != 1:
            problems["schemaVersion"] = collection.get("al_schema_version")
        if collection.get("al_asset_source_id") != source["id"]:
            problems["assetSourceId"] = collection.get("al_asset_source_id")
        if collection.users <= 0:
            problems["linkedIntoSource"] = False
        metrics[name] = {
            "assetSourceId": collection.get("al_asset_source_id"),
            "schemaVersion": collection.get("al_schema_version"),
            "objects": sorted(obj.name for obj in collection.objects),
            "users": int(collection.users),
        }
        if problems:
            gaps.append(
                _issue(
                    "promotion_collection_contract",
                    "Production-promotion collection violates its technical contract.",
                    collection=name,
                    **problems,
                )
            )
    return metrics


def _triangle_count(names: Iterable[str]) -> int:
    total = 0
    for name in names:
        obj = bpy.data.objects.get(name)
        if obj is None or obj.type != "MESH":
            continue
        obj.data.calc_loop_triangles()
        total += len(obj.data.loop_triangles)
    return total


def _action_metrics() -> dict[str, Any]:
    """Return Blender-5-compatible action evidence, including actual keyframes."""
    assignments: dict[str, set[str]] = {action.name: set() for action in bpy.data.actions}
    for obj in bpy.data.objects:
        animation_data = obj.animation_data
        if animation_data is None:
            continue
        if animation_data.action is not None:
            assignments.setdefault(animation_data.action.name, set()).add(obj.name)
        for track in animation_data.nla_tracks:
            for strip in track.strips:
                if strip.action is not None:
                    assignments.setdefault(strip.action.name, set()).add(obj.name)

    result: dict[str, Any] = {}
    for action in sorted(bpy.data.actions, key=lambda item: item.name):
        fcurves: list[Any] = []
        legacy_fcurves = getattr(action, "fcurves", None)
        if legacy_fcurves is not None:
            fcurves.extend(legacy_fcurves)
        for layer in getattr(action, "layers", []):
            for strip in layer.strips:
                for channelbag in getattr(strip, "channelbags", []):
                    fcurves.extend(channelbag.fcurves)
        # A curve can be reachable through both compatibility and layered APIs.
        unique_fcurves = {curve.as_pointer(): curve for curve in fcurves}
        result[action.name] = {
            "frameRange": [float(value) for value in action.frame_range],
            "fCurves": len(unique_fcurves),
            "keyframes": sum(
                len(curve.keyframe_points) for curve in unique_fcurves.values()
            ),
            "slots": [
                {
                    "identifier": slot.identifier,
                    "targetIdType": slot.target_id_type,
                }
                for slot in getattr(action, "slots", [])
            ],
            "assignedObjects": sorted(assignments.get(action.name, set())),
            "users": int(action.users),
        }
    return result


def _resolved_lods(source: dict[str, Any]) -> dict[str, list[str]]:
    resolved: dict[str, list[str]] = {}
    pending = list(source.get("lods", []))
    while pending:
        progress = False
        for lod in list(pending):
            if "objects" in lod:
                names = list(lod["objects"])
            else:
                parent = lod.get("objectsFromLod")
                if parent not in resolved:
                    continue
                suffix = lod.get("suffix", "")
                names = [f"{name}{suffix}" for name in resolved[parent]]
            resolved[lod["id"]] = names
            pending.remove(lod)
            progress = True
        if not progress:
            unresolved = [lod.get("id", "<missing-id>") for lod in pending]
            raise ValueError(f"Unresolvable LOD references: {unresolved}")
    return resolved


def _names_from_reference(
    contract: dict[str, Any], lods: dict[str, list[str]], key: str = "objects"
) -> list[str]:
    if key in contract:
        return list(contract[key])
    lod_key = contract.get(f"{key}FromLod")
    if lod_key:
        return list(lods.get(lod_key, []))
    return []


def _armature_metrics(
    source: dict[str, Any],
    lods: dict[str, list[str]],
    errors: list[dict[str, Any]],
) -> dict[str, Any] | None:
    contract = source.get("armature")
    if not contract:
        return None

    armature = bpy.data.objects.get(contract["object"])
    if armature is None or armature.type != "ARMATURE":
        errors.append(
            _issue(
                "armature_missing",
                f"Required armature {contract['object']} is absent or not an armature.",
            )
        )
        return None

    bones = list(armature.data.bones)
    bone_names = {bone.name for bone in bones}
    deform_bone_names = {bone.name for bone in bones if bone.use_deform}
    missing_bones = sorted(set(contract.get("requiredBones", [])) - bone_names)
    if missing_bones:
        errors.append(
            _issue(
                "armature_required_bones_missing",
                "Required deformation/retarget bones are missing.",
                missing=missing_bones,
            )
        )

    bone_count = len(bones)
    minimum = int(contract.get("minimumBones", 0))
    maximum = int(contract.get("maximumBones", 2**31 - 1))
    if not minimum <= bone_count <= maximum:
        errors.append(
            _issue(
                "armature_bone_budget",
                f"Bone count {bone_count} is outside [{minimum}, {maximum}].",
                actual=bone_count,
                minimum=minimum,
                maximum=maximum,
            )
        )

    skin_names: list[str] = []
    for lod_id in contract.get("skinnedObjectsFromLods", []):
        skin_names.extend(lods.get(lod_id, []))

    max_influences = 0
    unweighted_vertices: dict[str, int] = {}
    missing_armature_modifier: list[str] = []
    influence_limit = int(contract.get("maximumInfluencesPerVertex", 4))
    vertices_over_limit: dict[str, int] = {}
    non_normalized_vertices: dict[str, int] = {}
    discarded_weights: list[float] = []
    per_object_metrics: dict[str, Any] = {}
    for name in skin_names:
        obj = bpy.data.objects.get(name)
        if obj is None or obj.type != "MESH":
            continue
        if not any(
            modifier.type == "ARMATURE" and modifier.object == armature
            for modifier in obj.modifiers
        ):
            missing_armature_modifier.append(name)
        zero = 0
        over = 0
        non_normalized = 0
        object_max_influences = 0
        object_discarded_weights: list[float] = []
        for vertex in obj.data.vertices:
            weights = sorted(
                (
                    float(group.weight)
                    for group in vertex.groups
                    if group.weight > 1e-6
                    and obj.vertex_groups[group.group].name in deform_bone_names
                ),
                reverse=True,
            )
            influence_count = len(weights)
            max_influences = max(max_influences, influence_count)
            object_max_influences = max(object_max_influences, influence_count)
            if influence_count == 0:
                zero += 1
            elif abs(sum(weights) - 1.0) > 1e-4:
                non_normalized += 1
            if influence_count > influence_limit:
                over += 1
                discarded = sum(weights[influence_limit:])
                discarded_weights.append(discarded)
                object_discarded_weights.append(discarded)
        if zero:
            unweighted_vertices[name] = zero
        if over:
            vertices_over_limit[name] = over
        if non_normalized:
            non_normalized_vertices[name] = non_normalized
        sorted_object_discarded = sorted(object_discarded_weights)
        object_percentile_index = (
            min(
                len(sorted_object_discarded) - 1,
                math.floor(len(sorted_object_discarded) * 0.95),
            )
            if sorted_object_discarded
            else 0
        )
        per_object_metrics[name] = {
            "vertices": len(obj.data.vertices),
            "maximumInfluences": object_max_influences,
            "verticesOverInfluenceLimit": over,
            "unweightedVertices": zero,
            "nonNormalizedVertices": non_normalized,
            "prunePreview": {
                "maximumDiscardedWeight": max(object_discarded_weights, default=0.0),
                "meanDiscardedWeight": (
                    sum(object_discarded_weights) / len(object_discarded_weights)
                    if object_discarded_weights
                    else 0.0
                ),
                "p95DiscardedWeight": (
                    sorted_object_discarded[object_percentile_index]
                    if sorted_object_discarded
                    else 0.0
                ),
            },
        }

    if missing_armature_modifier:
        errors.append(
            _issue(
                "skin_armature_modifier_missing",
                "Skinned source meshes are not bound to the declared armature.",
                objects=sorted(missing_armature_modifier),
            )
        )
    if unweighted_vertices:
        errors.append(
            _issue(
                "skin_unweighted_vertices",
                "Skinned source meshes contain unweighted vertices.",
                objects=unweighted_vertices,
            )
        )
    if vertices_over_limit:
        errors.append(
            _issue(
                "skin_influence_budget",
                f"Vertices exceed the {influence_limit}-influence ceiling.",
                objects=vertices_over_limit,
            )
        )
    if non_normalized_vertices:
        errors.append(
            _issue(
                "skin_weight_normalization",
                "Skinned vertices must have normalized deformation weights.",
                objects=non_normalized_vertices,
                tolerance=1e-4,
            )
        )

    sorted_discarded = sorted(discarded_weights)
    percentile_index = (
        min(len(sorted_discarded) - 1, math.floor(len(sorted_discarded) * 0.95))
        if sorted_discarded
        else 0
    )

    return {
        "object": armature.name,
        "bones": bone_count,
        "deformBones": sum(1 for bone in bones if bone.use_deform),
        "rootBones": sorted(bone.name for bone in bones if bone.parent is None),
        "maxInfluencesPerVertex": max_influences,
        "skinnedMeshCount": len(skin_names),
        "verticesOverInfluenceLimit": sum(vertices_over_limit.values()),
        "nonNormalizedVertices": sum(non_normalized_vertices.values()),
        "perSkinnedObject": per_object_metrics,
        "influencePrunePreview": {
            "verticesAffected": len(discarded_weights),
            "maximumDiscardedWeight": max(discarded_weights, default=0.0),
            "meanDiscardedWeight": (
                sum(discarded_weights) / len(discarded_weights)
                if discarded_weights
                else 0.0
            ),
            "p95DiscardedWeight": (
                sorted_discarded[percentile_index] if sorted_discarded else 0.0
            ),
        },
    }


def _validate_source(
    source: dict[str, Any], manifest: dict[str, Any]
) -> dict[str, Any]:
    errors: list[dict[str, Any]] = []
    gaps: list[dict[str, Any]] = []
    warnings: list[dict[str, Any]] = []
    source_path = REPOSITORY_ROOT / source["path"]

    if not source_path.is_file():
        errors.append(
            _issue("source_missing", f"Source file does not exist: {source['path']}")
        )
        return {
            "id": source["id"],
            "category": source["category"],
            "approvalState": source["approvalState"],
            "path": source["path"],
            "status": "invalid",
            "errors": errors,
            "gaps": gaps,
            "warnings": warnings,
            "openReview": source.get("openReview", []),
            "metrics": {},
        }

    actual_hash = _sha256(source_path)
    if actual_hash != source["sha256"]:
        errors.append(
            _issue(
                "source_hash_mismatch",
                "Retained source bytes differ from the version-pinned manifest.",
                expected=source["sha256"],
                actual=actual_hash,
            )
        )

    bpy.ops.wm.open_mainfile(filepath=str(source_path), load_ui=False)

    unit_settings = bpy.context.scene.unit_settings
    if unit_settings.system != source["unitSystem"]:
        errors.append(
            _issue(
                "unit_system",
                f"Unit system is {unit_settings.system}, expected {source['unitSystem']}.",
            )
        )
    if not _close_enough(unit_settings.scale_length, float(source["unitScale"]), 1e-6):
        errors.append(
            _issue(
                "unit_scale",
                f"Unit scale is {unit_settings.scale_length}, expected {source['unitScale']}.",
            )
        )

    default_up = manifest["defaultSourceUpAxis"]
    source_up = source["sourceUpAxis"]
    if source_up != default_up:
        exception = source.get("axisException")
        if not exception:
            errors.append(
                _issue(
                    "axis_exception_undocumented",
                    f"Source up axis {source_up} differs from default {default_up} without a declared exception.",
                )
            )
        else:
            warnings.append(
                _issue(
                    "declared_axis_exception",
                    exception,
                    sourceUpAxis=source_up,
                    defaultSourceUpAxis=default_up,
                )
            )

    missing_objects = sorted(
        name
        for name in source.get("requiredObjects", [])
        if bpy.data.objects.get(name) is None
    )
    if missing_objects:
        errors.append(
            _issue(
                "required_objects_missing",
                "Required source objects are missing.",
                objects=missing_objects,
            )
        )
    missing_collections = sorted(
        name
        for name in source.get("requiredCollections", [])
        if bpy.data.collections.get(name) is None
    )
    if missing_collections:
        errors.append(
            _issue(
                "required_collections_missing",
                "Required modular collections are missing.",
                collections=missing_collections,
            )
        )

    non_finite_transforms: list[str] = []
    non_invertible_transforms: list[str] = []
    for obj in bpy.data.objects:
        if obj.type not in EXPORTABLE_TYPES:
            continue
        matrix_values = [float(value) for row in obj.matrix_world for value in row]
        if not all(math.isfinite(value) for value in matrix_values):
            non_finite_transforms.append(obj.name)
        elif abs(float(obj.matrix_world.determinant())) <= 1e-12:
            non_invertible_transforms.append(obj.name)
    if non_finite_transforms:
        errors.append(
            _issue(
                "non_finite_transform",
                "Exportable objects contain non-finite world transforms.",
                objects=sorted(non_finite_transforms),
            )
        )
    if non_invertible_transforms:
        errors.append(
            _issue(
                "non_invertible_transform",
                "Exportable objects contain singular world transforms.",
                objects=sorted(non_invertible_transforms),
            )
        )

    if source.get("identityScaleObjects") == "all-exportable":
        scaled = {
            obj.name: [round(component, 6) for component in obj.scale]
            for obj in bpy.data.objects
            if obj.type in EXPORTABLE_TYPES
            and any(abs(component - 1.0) > 1e-5 for component in obj.scale)
        }
        if scaled:
            errors.append(
                _issue(
                    "unapplied_scale",
                    "Exportable source objects have non-identity scale.",
                    objects=scaled,
                )
            )

    rotated = {}
    for name in source.get("identityRotationObjects", []):
        obj = bpy.data.objects.get(name)
        if obj is None:
            continue
        euler = [float(component) for component in obj.rotation_euler]
        if any(abs(component) > 1e-5 for component in euler):
            rotated[name] = [round(component, 6) for component in euler]
    if rotated:
        errors.append(
            _issue(
                "unapplied_rotation",
                "Declared export roots/meshes have non-identity rotation.",
                objects=rotated,
            )
        )

    lods = _resolved_lods(source)
    lod_metrics: dict[str, Any] = {}
    previous_lod_by_family: dict[str, tuple[str, int]] = {}
    for lod_contract in source.get("lods", []):
        lod_id = lod_contract["id"]
        family = lod_contract.get("family", "default")
        names = lods[lod_id]
        missing = sorted(
            name
            for name in names
            if bpy.data.objects.get(name) is None
            or bpy.data.objects[name].type != "MESH"
        )
        if missing:
            errors.append(
                _issue(
                    "lod_meshes_missing",
                    f"{lod_id} source meshes are missing.",
                    objects=missing,
                )
            )
        triangles = _triangle_count(names)
        minimum = int(lod_contract["minimumTriangles"])
        maximum = int(lod_contract["maximumTriangles"])
        if not minimum <= triangles <= maximum:
            errors.append(
                _issue(
                    "lod_triangle_budget",
                    f"{lod_id} has {triangles} triangles, outside [{minimum}, {maximum}].",
                    lod=lod_id,
                    actual=triangles,
                    minimum=minimum,
                    maximum=maximum,
                )
            )
        previous = previous_lod_by_family.get(family)
        previous_lod_id = previous[0] if previous is not None else None
        previous_triangle_count = previous[1] if previous is not None else None
        ratio_to_previous = (
            triangles / previous_triangle_count
            if previous_triangle_count is not None and previous_triangle_count > 0
            else None
        )
        if previous_triangle_count is not None and triangles >= previous_triangle_count:
            errors.append(
                _issue(
                    "lod_not_reduced",
                    f"{lod_id} must contain fewer triangles than {previous_lod_id}.",
                    lod=lod_id,
                    family=family,
                    previousLod=previous_lod_id,
                    actual=triangles,
                    previous=previous_triangle_count,
                )
            )
        lod_metrics[lod_id] = {
            "family": family,
            "meshCount": len(names) - len(missing),
            "triangles": triangles,
            "minimumTriangles": minimum,
            "maximumTriangles": maximum,
            "ratioToPrevious": ratio_to_previous,
        }
        previous_lod_by_family[family] = (lod_id, triangles)

    mesh_objects = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    non_finite_mesh_vertices: dict[str, int] = {}
    for obj in mesh_objects:
        invalid = sum(
            1
            for vertex in obj.data.vertices
            if not all(math.isfinite(float(component)) for component in vertex.co)
        )
        if invalid:
            non_finite_mesh_vertices[obj.name] = invalid
    if non_finite_mesh_vertices:
        errors.append(
            _issue(
                "non_finite_mesh_geometry",
                "Mesh source contains non-finite vertex coordinates.",
                objects=non_finite_mesh_vertices,
            )
        )
    material_names = sorted(
        {
            material.name
            for obj in mesh_objects
            for material in obj.data.materials
            if material is not None
        }
    )
    maximum_meshes = source.get("maximumMeshObjects")
    if maximum_meshes is not None and len(mesh_objects) > int(maximum_meshes):
        errors.append(
            _issue(
                "mesh_object_budget",
                f"Source has {len(mesh_objects)} mesh objects, over {maximum_meshes}.",
            )
        )
    maximum_materials = source.get("maximumMaterialNames")
    if maximum_materials is not None and len(material_names) > int(maximum_materials):
        errors.append(
            _issue(
                "material_budget",
                f"Source has {len(material_names)} materials, over {maximum_materials}.",
                materials=material_names,
            )
        )
    maximum_slots = source.get("maximumMaterialSlotsPerMesh")
    max_slots_actual = max((len(obj.data.materials) for obj in mesh_objects), default=0)
    if maximum_slots is not None and max_slots_actual > int(maximum_slots):
        errors.append(
            _issue(
                "material_slot_budget",
                f"A source mesh has {max_slots_actual} material slots, over {maximum_slots}.",
                actual=max_slots_actual,
                maximum=maximum_slots,
            )
        )

    export_set_metrics: dict[str, Any] = {}
    for export_set in source.get("exportSets", []):
        selected_names: list[str] = []
        for lod_id in export_set.get("objectsFromLods", []):
            selected_names.extend(lods.get(lod_id, []))
        selected_names.extend(export_set.get("includeObjects", []))
        selected_names = sorted(set(selected_names))
        missing = sorted(
            name for name in selected_names if bpy.data.objects.get(name) is None
        )
        if missing:
            errors.append(
                _issue(
                    "export_set_objects_missing",
                    "Export set references source objects that are absent.",
                    exportSet=export_set.get("id"),
                    objects=missing,
                )
            )
        if not selected_names:
            errors.append(
                _issue(
                    "export_set_empty",
                    "Export set must resolve at least one source object.",
                    exportSet=export_set.get("id"),
                )
            )
        export_mesh_names = [
            name
            for name in selected_names
            if bpy.data.objects.get(name) is not None
            and bpy.data.objects[name].type == "MESH"
        ]
        export_set_metrics[export_set.get("id", "<missing-id>")] = {
            "format": export_set.get("format"),
            "includeAnimations": bool(export_set.get("includeAnimations")),
            "objects": selected_names,
            "meshObjects": len(export_mesh_names),
            "triangles": _triangle_count(export_mesh_names),
        }

    ngon_count = sum(
        1
        for obj in mesh_objects
        for polygon in obj.data.polygons
        if len(polygon.vertices) > 4
    )
    if ngon_count:
        warnings.append(
            _issue(
                "ngon_topology",
                "Source contains n-gons; exported triangulation needs visual review.",
                count=ngon_count,
            )
        )

    root_contract = source.get("root")
    if root_contract:
        root = bpy.data.objects.get(root_contract["object"])
        if root is not None:
            expected = root_contract["expectedLocation"]
            tolerance = float(root_contract["tolerance"])
            actual = list(root.location)
            if any(
                not _close_enough(
                    float(actual[index]), float(expected[index]), tolerance
                )
                for index in range(3)
            ):
                errors.append(
                    _issue(
                        "root_location",
                        "Root is not at the declared source origin.",
                        actual=actual,
                        expected=expected,
                    )
                )

    pivot_contract = source.get("pivots")
    pivot_metrics: dict[str, list[float]] = {}
    if pivot_contract:
        expected = pivot_contract["expectedLocation"]
        tolerance = float(pivot_contract["tolerance"])
        invalid_pivots: dict[str, Any] = {}
        for name in pivot_contract["objects"]:
            obj = bpy.data.objects.get(name)
            if obj is None:
                continue
            actual = [float(component) for component in obj.matrix_world.translation]
            pivot_metrics[name] = actual
            if any(
                not _close_enough(actual[index], float(expected[index]), tolerance)
                for index in range(3)
            ):
                invalid_pivots[name] = actual
        if invalid_pivots:
            errors.append(
                _issue(
                    "pivot_location",
                    "Declared asset pivots do not resolve to the expected source location.",
                    objects=invalid_pivots,
                    expected=expected,
                    tolerance=tolerance,
                )
            )

    ground_contract = source.get("ground")
    ground_metric: dict[str, Any] | None = None
    if ground_contract:
        names = _names_from_reference(ground_contract, lods)
        bounds = _object_bounds(names)
        if bounds is None:
            errors.append(
                _issue("ground_bounds", "Grounding source bounds are unavailable.")
            )
        else:
            minimum_bounds, maximum_bounds = bounds
            axis_index = AXIS_INDEX[ground_contract["axis"]]
            edge = ground_contract["edge"]
            actual = (
                minimum_bounds[axis_index]
                if edge == "minimum"
                else maximum_bounds[axis_index]
            )
            expected = float(ground_contract["expected"])
            tolerance = float(ground_contract["tolerance"])
            if not _close_enough(actual, expected, tolerance):
                errors.append(
                    _issue(
                        "ground_plane",
                        f"Declared ground edge is {actual:.6f}, expected {expected:.6f}.",
                        axis=ground_contract["axis"],
                        edge=edge,
                        actual=actual,
                        expected=expected,
                        tolerance=tolerance,
                    )
                )
            ground_metric = {
                "axis": ground_contract["axis"],
                "edge": edge,
                "actual": actual,
                "expected": expected,
            }

    dimension_contract = source.get("dimensions")
    dimension_metrics: dict[str, float] = {}
    if dimension_contract:
        names = _names_from_reference(dimension_contract, lods)
        bounds = _object_bounds(names)
        if bounds is None:
            errors.append(
                _issue("dimension_bounds", "Dimension source bounds are unavailable.")
            )
        else:
            minimum_bounds, maximum_bounds = bounds
            tolerance = float(dimension_contract["tolerance"])
            for axis, expected in dimension_contract["expected"].items():
                index = AXIS_INDEX[axis]
                actual = maximum_bounds[index] - minimum_bounds[index]
                dimension_metrics[axis] = actual
                if not _close_enough(actual, float(expected), tolerance):
                    errors.append(
                        _issue(
                            "source_dimension",
                            f"{axis} dimension {actual:.6f} differs from {expected:.6f}.",
                            axis=axis,
                            actual=actual,
                            expected=expected,
                            tolerance=tolerance,
                        )
                    )

    height_contract = source.get("height")
    height_metric: float | None = None
    if height_contract:
        names = _names_from_reference(height_contract, lods)
        bounds = _object_bounds(names)
        if bounds is None:
            errors.append(
                _issue("height_bounds", "Height source bounds are unavailable.")
            )
        else:
            minimum_bounds, maximum_bounds = bounds
            index = AXIS_INDEX[height_contract["axis"]]
            height_metric = maximum_bounds[index] - minimum_bounds[index]
            minimum = float(height_contract["minimum"])
            maximum = float(height_contract["maximum"])
            if not minimum <= height_metric <= maximum:
                errors.append(
                    _issue(
                        "source_height",
                        f"Height {height_metric:.6f} is outside [{minimum}, {maximum}].",
                    )
                )

    orientation_contract = source.get("orientation")
    orientation_metric: dict[str, Any] | None = None
    if orientation_contract:
        axis = orientation_contract["axis"]
        head = _centroid_coordinate(orientation_contract["headObject"], axis)
        forward = _centroid_coordinate(orientation_contract["forwardObject"], axis)
        if head is None or forward is None:
            errors.append(
                _issue(
                    "orientation_geometry",
                    "Orientation reference geometry is unavailable.",
                )
            )
        else:
            delta = forward - head
            separation = float(orientation_contract.get("minimumSeparation", 0.0))
            correct = (
                delta <= -separation
                if orientation_contract["direction"] == "negative"
                else delta >= separation
            )
            if not correct:
                errors.append(
                    _issue(
                        "source_orientation",
                        "Geometry does not face the declared source direction.",
                        axis=axis,
                        direction=orientation_contract["direction"],
                        delta=delta,
                    )
                )
            orientation_metric = {"axis": axis, "centroidDelta": delta}

    bone_orientation_contract = source.get("boneOrientation")
    bone_orientation_metric: dict[str, Any] | None = None
    if bone_orientation_contract:
        armature = bpy.data.objects.get(bone_orientation_contract["armatureObject"])
        origin_bone = (
            armature.data.bones.get(bone_orientation_contract["originBone"])
            if armature is not None and armature.type == "ARMATURE"
            else None
        )
        forward_bone = (
            armature.data.bones.get(bone_orientation_contract["forwardBone"])
            if armature is not None and armature.type == "ARMATURE"
            else None
        )
        if armature is None or origin_bone is None or forward_bone is None:
            errors.append(
                _issue(
                    "bone_orientation",
                    "Bone orientation references are unavailable.",
                )
            )
        else:
            axis = bone_orientation_contract["axis"]
            index = AXIS_INDEX[axis]
            origin = armature.matrix_world @ origin_bone.head_local
            forward = armature.matrix_world @ forward_bone.head_local
            delta = forward[index] - origin[index]
            separation = float(bone_orientation_contract.get("minimumSeparation", 0.0))
            correct = (
                delta <= -separation
                if bone_orientation_contract["direction"] == "negative"
                else delta >= separation
            )
            if not correct:
                errors.append(
                    _issue(
                        "source_bone_orientation",
                        "Rig does not face the declared source direction.",
                        axis=axis,
                        direction=bone_orientation_contract["direction"],
                        delta=delta,
                    )
                )
            bone_orientation_metric = {"axis": axis, "boneDelta": delta}

    armature_metric = _armature_metrics(source, lods, errors)

    promotion = source.get("promotion", {})
    action_details = _action_metrics()
    action_count = len(action_details)
    usable_action_count = sum(
        1 for details in action_details.values() if details["keyframes"] > 0
    )
    minimum_actions = promotion.get("minimumActions")
    if minimum_actions is not None and usable_action_count < int(minimum_actions):
        gaps.append(
            _issue(
                "promotion_animation_floor",
                f"Source has {usable_action_count} keyed actions; promotion requires {minimum_actions}.",
                actual=usable_action_count,
                dataBlocks=action_count,
                minimum=minimum_actions,
            )
        )
    missing_promotion_objects = sorted(
        name
        for name in promotion.get("requiredObjectNames", [])
        if bpy.data.objects.get(name) is None
    )
    if missing_promotion_objects:
        gaps.append(
            _issue(
                "promotion_objects",
                "Production-promotion source objects are not authored yet.",
                objects=missing_promotion_objects,
            )
        )
    missing_promotion_collections = sorted(
        name
        for name in promotion.get("requiredCollectionNames", [])
        if bpy.data.collections.get(name) is None
    )
    if missing_promotion_collections:
        gaps.append(
            _issue(
                "promotion_collections",
                "Production-promotion source collections are not authored yet.",
                collections=missing_promotion_collections,
            )
        )
    promotion_object_metrics = _promotion_object_metrics(
        source,
        promotion.get("requiredObjectNames", []),
        gaps,
    )
    promotion_collection_metrics = _promotion_collection_metrics(
        source,
        promotion.get("requiredCollectionNames", []),
        gaps,
    )
    promotion_representation_metrics: dict[str, Any] = {}
    lod0_triangles = lod_metrics.get("LOD0", {}).get("triangles")
    for token in promotion.get("requiredMeshNameTokens", []):
        matches = sorted(
            obj.name for obj in mesh_objects if token.lower() in obj.name.lower()
        )
        triangles = _triangle_count(matches)
        promotion_representation_metrics[token] = {
            "objects": matches,
            "triangles": triangles,
            "ratioToLod0": (
                triangles / lod0_triangles
                if lod0_triangles is not None and lod0_triangles > 0
                else None
            ),
        }
        if not matches:
            gaps.append(
                _issue(
                    "promotion_mesh_representation",
                    f"No mesh representation contains required token '{token}'.",
                    token=token,
                )
            )
        elif triangles <= 0:
            gaps.append(
                _issue(
                    "promotion_mesh_representation_empty",
                    f"Mesh representation '{token}' has no triangles.",
                    token=token,
                    objects=matches,
                )
            )
        elif lod0_triangles is not None and triangles >= lod0_triangles:
            gaps.append(
                _issue(
                    "promotion_mesh_representation_not_reduced",
                    f"Mesh representation '{token}' is not cheaper than LOD0.",
                    token=token,
                    objects=matches,
                    triangles=triangles,
                    lod0Triangles=lod0_triangles,
                )
            )

    missing_external_files: list[str] = []
    for image in bpy.data.images:
        if (
            image.source != "FILE"
            or image.packed_file is not None
            or not image.filepath
        ):
            continue
        resolved = Path(bpy.path.abspath(image.filepath, library=image.library))
        if not resolved.is_file():
            missing_external_files.append(image.name)
    if missing_external_files:
        warnings.append(
            _issue(
                "external_images_missing",
                "Working source references images not available at their recorded paths.",
                images=sorted(missing_external_files),
            )
        )

    status = (
        "invalid" if errors else "candidate_with_gaps" if gaps else "candidate_valid"
    )
    return {
        "id": source["id"],
        "category": source["category"],
        "approvalState": source["approvalState"],
        "path": source["path"],
        "sha256": actual_hash,
        "status": status,
        "errors": errors,
        "gaps": gaps,
        "warnings": warnings,
        "openReview": source.get("openReview", []),
        "metrics": {
            "unitSystem": unit_settings.system,
            "unitScale": unit_settings.scale_length,
            "sourceUpAxis": source_up,
            "meshObjects": len(mesh_objects),
            "materials": material_names,
            "maximumMaterialSlotsPerMesh": max_slots_actual,
            "meshesWithoutUv": sorted(
                obj.name for obj in mesh_objects if len(obj.data.uv_layers) == 0
            ),
            "shapeKeys": {
                obj.name: len(obj.data.shape_keys.key_blocks)
                for obj in mesh_objects
                if obj.data.shape_keys is not None
            },
            "ngons": ngon_count,
            "actions": action_count,
            "usableActions": usable_action_count,
            "actionNames": sorted(action.name for action in bpy.data.actions),
            "actionDetails": action_details,
            "lods": lod_metrics,
            "exportSets": export_set_metrics,
            "ground": ground_metric,
            "pivots": pivot_metrics,
            "dimensions": dimension_metrics,
            "height": height_metric,
            "orientation": orientation_metric,
            "boneOrientation": bone_orientation_metric,
            "armature": armature_metric,
            "promotionObjects": promotion_object_metrics,
            "promotionCollections": promotion_collection_metrics,
            "promotionRepresentations": promotion_representation_metrics,
        },
    }


def main() -> int:
    args = _arguments()
    manifest_path = args.manifest
    if not manifest_path.is_absolute():
        manifest_path = REPOSITORY_ROOT / manifest_path
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

    manifest_errors = _manifest_diagnostics(manifest)
    if manifest_errors:
        report = {
            "schemaVersion": 1,
            "contractId": manifest.get("contractId", "<invalid-manifest>"),
            "manifestPath": (
                manifest_path.relative_to(REPOSITORY_ROOT).as_posix()
                if manifest_path.is_relative_to(REPOSITORY_ROOT)
                else manifest_path.as_posix()
            ),
            "manifestSha256": _sha256(manifest_path),
            "blenderVersion": bpy.app.version_string,
            "minimumBlenderVersion": manifest.get("minimumBlenderVersion", []),
            "status": "invalid",
            "summary": {
                "sources": 0,
                "errors": len(manifest_errors),
                "promotionGaps": 0,
                "warnings": 0,
            },
            "globalErrors": manifest_errors,
            "sources": [],
        }
        serialized = json.dumps(report, indent=2, sort_keys=True) + "\n"
        if args.output:
            output = args.output
            if not output.is_absolute():
                output = REPOSITORY_ROOT / output
            output.parent.mkdir(parents=True, exist_ok=True)
            output.write_text(serialized, encoding="utf-8")
            print(f"AL Blender validation report: {output}")
        else:
            print(serialized, end="")
        print(
            "AL Blender source validation: invalid manifest; "
            f"{len(manifest_errors)} errors"
        )
        return 1

    minimum_version = tuple(int(value) for value in manifest["minimumBlenderVersion"])
    current_version = tuple(int(value) for value in bpy.app.version[:3])
    selected = set(args.source)
    sources = [
        source
        for source in manifest["sources"]
        if not selected or source["id"] in selected
    ]
    missing_source_ids = sorted(selected - {source["id"] for source in sources})
    if missing_source_ids:
        print(f"Unknown source ids: {', '.join(missing_source_ids)}", file=sys.stderr)
        return 1

    results = [_validate_source(source, manifest) for source in sources]
    version_error = current_version < minimum_version
    error_count = sum(len(result["errors"]) for result in results) + int(version_error)
    gap_count = sum(len(result["gaps"]) for result in results)
    warning_count = sum(len(result["warnings"]) for result in results)
    if version_error or error_count:
        overall_status = "invalid"
    elif gap_count:
        overall_status = "candidate_with_gaps"
    else:
        overall_status = "candidate_valid"

    report = {
        "schemaVersion": 1,
        "contractId": manifest["contractId"],
        "manifestPath": (
            manifest_path.relative_to(REPOSITORY_ROOT).as_posix()
            if manifest_path.is_relative_to(REPOSITORY_ROOT)
            else manifest_path.as_posix()
        ),
        "manifestSha256": _sha256(manifest_path),
        "blenderVersion": bpy.app.version_string,
        "minimumBlenderVersion": list(minimum_version),
        "status": overall_status,
        "summary": {
            "sources": len(results),
            "errors": error_count,
            "promotionGaps": gap_count,
            "warnings": warning_count,
        },
        "globalErrors": (
            [
                _issue(
                    "blender_version",
                    f"Blender {bpy.app.version_string} is older than {minimum_version}.",
                )
            ]
            if version_error
            else []
        ),
        "sources": results,
    }

    serialized = json.dumps(report, indent=2, sort_keys=True) + "\n"
    if args.output:
        output = args.output
        if not output.is_absolute():
            output = REPOSITORY_ROOT / output
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(serialized, encoding="utf-8")
        print(f"AL Blender validation report: {output}")
    else:
        print(serialized, end="")

    print(
        "AL Blender source validation: "
        f"{overall_status}; {len(results)} sources; {error_count} errors; "
        f"{gap_count} promotion gaps; {warning_count} warnings"
    )
    for result in results:
        print(
            f"  {result['id']}: {result['status']} "
            f"({len(result['errors'])} errors, {len(result['gaps'])} gaps, "
            f"{len(result['warnings'])} warnings)"
        )

    if error_count:
        return 1
    if args.fail_on_gaps and gap_count:
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
