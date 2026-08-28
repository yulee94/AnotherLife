"""Export a manifest-declared AnotherLife Blender review candidate.

The exporter never edits or overwrites a retained source. It fails closed on hard
source-validation errors, blocks promotion gaps unless explicitly allowed for an
MVP/review export, exports only the named set, reimports the GLB, and writes a
hash-addressed receipt.

Example:

    blender --background --python-exit-code 1 \
      --python tools/blender/export_al_asset_candidate.py -- \
      --source neutral-covenant-hall-working-v001 \
      --export-set mvp-render \
      --output archive/local-run/blender/exports/neutral_covenant_hall_v001.glb \
      --allow-promotion-gaps
"""

from __future__ import annotations

import argparse
import json
import math
import re
import struct
import sys
from collections.abc import Iterable
from pathlib import Path
from typing import Any

import bpy
from mathutils import Vector

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

BOUNDS_TOLERANCE_METERS = 1e-4
VERSION_TOKEN_PATTERN = re.compile(r"(?:^|[_-])(v[0-9]{3})(?:$|[_-])")
VALIDATOR_PATH = Path(_validate_source.__code__.co_filename).resolve()
PROMOTION_ELIGIBLE_APPROVAL_STATES = {
    "production-candidate",
    "mvp-runtime-candidate",
    "lod0-production-pilot",
}


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--source", required=True)
    parser.add_argument("--export-set", required=True)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--receipt", type=Path)
    parser.add_argument("--allow-promotion-gaps", action="store_true")
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(blender_args)


def _resolve(path: Path) -> Path:
    return path if path.is_absolute() else REPOSITORY_ROOT / path


def _bounds(
    objects: Iterable[bpy.types.Object],
) -> tuple[list[float], list[float]] | None:
    points: list[Vector] = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        points.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    if not points:
        return None
    return (
        [min(point[index] for point in points) for index in range(3)],
        [max(point[index] for point in points) for index in range(3)],
    )


def _triangles(objects: Iterable[bpy.types.Object]) -> int:
    total = 0
    for obj in objects:
        if obj.type != "MESH":
            continue
        obj.data.calc_loop_triangles()
        total += len(obj.data.loop_triangles)
    return total


def _matrix(value: Any) -> list[list[float]]:
    return [[round(float(component), 8) for component in row] for row in value]


def _object_receipt(obj: bpy.types.Object) -> dict[str, Any]:
    result: dict[str, Any] = {
        "name": obj.name,
        "type": obj.type,
        "parent": obj.parent.name if obj.parent else None,
        "collections": sorted(collection.name for collection in obj.users_collection),
        "matrixWorld": _matrix(obj.matrix_world),
    }
    if obj.type == "MESH":
        obj.data.calc_loop_triangles()
        result.update(
            {
                "vertices": len(obj.data.vertices),
                "triangles": len(obj.data.loop_triangles),
                "uvLayers": len(obj.data.uv_layers),
                "materialSlots": [
                    material.name if material is not None else None
                    for material in obj.data.materials
                ],
            }
        )
    elif obj.type == "ARMATURE":
        result.update(
            {
                "bones": len(obj.data.bones),
                "deformBones": sum(1 for bone in obj.data.bones if bone.use_deform),
            }
        )
    return result


def _close_bounds(
    expected: tuple[list[float], list[float]] | None,
    actual: tuple[list[float], list[float]] | None,
) -> tuple[bool, float]:
    if expected is None or actual is None:
        return expected is actual, math.inf if expected is not actual else 0.0
    maximum_delta = max(
        abs(expected[edge][axis] - actual[edge][axis])
        for edge in range(2)
        for axis in range(3)
    )
    return maximum_delta <= BOUNDS_TOLERANCE_METERS, maximum_delta


def _write_receipt(path: Path, receipt: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )


def _read_glb_json(path: Path) -> dict[str, Any]:
    with path.open("rb") as stream:
        magic, version, _ = struct.unpack("<4sII", stream.read(12))
        if magic != b"glTF" or version != 2:
            raise ValueError("Artifact is not a glTF 2.0 binary.")
        chunk_length, chunk_type = struct.unpack("<II", stream.read(8))
        if chunk_type != 0x4E4F534A:
            raise ValueError("First GLB chunk is not JSON.")
        payload = stream.read(chunk_length).decode("utf-8").rstrip("\x00 ")
    return json.loads(payload)


def _blocked_receipt(
    manifest_path: Path,
    source_id: str,
    export_set_id: str,
    status: str,
    diagnostics: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "schemaVersion": 1,
        "status": status,
        "sourceId": source_id,
        "exportSetId": export_set_id,
        "manifestPath": (
            manifest_path.relative_to(REPOSITORY_ROOT).as_posix()
            if manifest_path.is_relative_to(REPOSITORY_ROOT)
            else manifest_path.as_posix()
        ),
        "manifestSha256": _sha256(manifest_path),
        "blenderVersion": bpy.app.version_string,
        "tooling": {
            "exporterSha256": _sha256(SCRIPT_PATH),
            "validatorSha256": _sha256(VALIDATOR_PATH),
        },
        "diagnostics": diagnostics,
    }


def main() -> int:
    args = _arguments()
    manifest_path = _resolve(args.manifest)
    output = _resolve(args.output)
    receipt_path = (
        _resolve(args.receipt)
        if args.receipt
        else output.with_suffix(output.suffix + ".receipt.json")
    )

    if output.suffix.lower() != ".glb":
        print("AL Blender export: output must use the .glb extension", file=sys.stderr)
        return 4
    if output.exists() or receipt_path.exists():
        print(
            "AL Blender export: refusing to overwrite an existing artifact or receipt",
            file=sys.stderr,
        )
        return 4

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest_errors = _manifest_diagnostics(manifest)
    if manifest_errors:
        _write_receipt(
            receipt_path,
            _blocked_receipt(
                manifest_path,
                args.source,
                args.export_set,
                "blocked_invalid_manifest",
                manifest_errors,
            ),
        )
        print("AL Blender export: manifest is invalid", file=sys.stderr)
        return 1

    source = next(
        (item for item in manifest["sources"] if item["id"] == args.source), None
    )
    if source is None:
        print(f"AL Blender export: unknown source {args.source}", file=sys.stderr)
        return 4
    source_versions = VERSION_TOKEN_PATTERN.findall(Path(source["path"]).stem)
    if not source_versions or source_versions[-1] not in output.stem:
        print(
            "AL Blender export: output filename must retain the source version token",
            file=sys.stderr,
        )
        return 4
    export_set = next(
        (item for item in source["exportSets"] if item["id"] == args.export_set),
        None,
    )
    if export_set is None:
        print(
            f"AL Blender export: unknown export set {args.export_set}",
            file=sys.stderr,
        )
        return 4

    validation = _validate_source(source, manifest)
    if validation["errors"]:
        _write_receipt(
            receipt_path,
            _blocked_receipt(
                manifest_path,
                args.source,
                args.export_set,
                "blocked_source_errors",
                validation["errors"],
            ),
        )
        print("AL Blender export: source has hard validation errors", file=sys.stderr)
        return 1
    if validation["gaps"] and not args.allow_promotion_gaps:
        _write_receipt(
            receipt_path,
            _blocked_receipt(
                manifest_path,
                args.source,
                args.export_set,
                "blocked_promotion_gaps",
                validation["gaps"],
            ),
        )
        print(
            "AL Blender export: source has promotion gaps; use "
            "--allow-promotion-gaps only for an explicit review/MVP artifact",
            file=sys.stderr,
        )
        return 2

    lods = _resolved_lods(source)
    selected_names: list[str] = []
    for lod_id in export_set["objectsFromLods"]:
        selected_names.extend(lods[lod_id])
    selected_names.extend(export_set["includeObjects"])
    selected_names = sorted(set(selected_names))
    selected_objects = [bpy.data.objects[name] for name in selected_names]
    selected_meshes = [obj for obj in selected_objects if obj.type == "MESH"]
    selected_mesh_names = {obj.name for obj in selected_meshes}
    selected_mesh_data_names = {obj.data.name for obj in selected_meshes}
    if not selected_meshes:
        print("AL Blender export: export set contains no meshes", file=sys.stderr)
        return 4

    expected_triangles = _triangles(selected_meshes)
    expected_bounds = _bounds(selected_meshes)
    source_object_receipts = [_object_receipt(obj) for obj in selected_objects]

    bpy.ops.object.select_all(action="DESELECT")
    for obj in selected_objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = selected_objects[0]

    output.parent.mkdir(parents=True, exist_ok=True)
    export_result = bpy.ops.export_scene.gltf(
        filepath=str(output),
        check_existing=False,
        export_format="GLB",
        use_selection=True,
        use_visible=False,
        use_renderable=False,
        export_apply=False,
        export_yup=True,
        export_cameras=False,
        export_lights=False,
        export_extras=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_texcoords=True,
        export_normals=True,
        export_tangents=False,
        export_skins=True,
        export_all_influences=False,
        export_influence_nb=4,
        export_morph=True,
        export_animations=bool(export_set["includeAnimations"]),
        export_animation_mode="ACTIONS",
        export_leaf_bone=False,
        export_rest_position_armature=True,
        export_draco_mesh_compression_enable=False,
        will_save_settings=False,
    )
    if "FINISHED" not in export_result or not output.is_file():
        print(
            "AL Blender export: GLB exporter did not create an artifact",
            file=sys.stderr,
        )
        return 3

    artifact_sha256 = _sha256(output)
    artifact_bytes = output.stat().st_size
    artifact_document = _read_glb_json(output)
    artifact_mesh_names = {
        mesh.get("name", "") for mesh in artifact_document.get("meshes", [])
    }
    artifact_node_names = {
        node.get("name", "") for node in artifact_document.get("nodes", [])
    }
    artifact_mesh_set_match = artifact_mesh_names == selected_mesh_data_names
    artifact_nodes_include_selection = set(selected_names).issubset(artifact_node_names)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    import_result = bpy.ops.import_scene.gltf(filepath=str(output))
    all_imported_meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    imported_meshes = [
        obj for obj in all_imported_meshes if obj.name in selected_mesh_names
    ]
    importer_helper_meshes = sorted(
        obj.name for obj in all_imported_meshes if obj.name not in selected_mesh_names
    )
    imported_triangles = _triangles(imported_meshes)
    imported_bounds = _bounds(imported_meshes)
    bounds_match, maximum_bounds_delta = _close_bounds(expected_bounds, imported_bounds)
    triangles_match = imported_triangles == expected_triangles
    round_trip_passed = (
        "FINISHED" in import_result
        and bounds_match
        and triangles_match
        and artifact_mesh_set_match
        and artifact_nodes_include_selection
    )

    receipt = {
        "schemaVersion": 1,
        "status": "review_export_valid" if round_trip_passed else "round_trip_invalid",
        "approvalState": source["approvalState"],
        "approvalAllowsPromotion": source["approvalState"]
        in PROMOTION_ELIGIBLE_APPROVAL_STATES,
        "promotionEligible": not validation["gaps"]
        and source["approvalState"] in PROMOTION_ELIGIBLE_APPROVAL_STATES,
        "promotionGapsExplicitlyAllowed": bool(args.allow_promotion_gaps),
        "sourceId": source["id"],
        "sourcePath": source["path"],
        "sourceSha256": source["sha256"],
        "manifestPath": (
            manifest_path.relative_to(REPOSITORY_ROOT).as_posix()
            if manifest_path.is_relative_to(REPOSITORY_ROOT)
            else manifest_path.as_posix()
        ),
        "manifestSha256": _sha256(manifest_path),
        "contractId": manifest["contractId"],
        "blenderVersion": bpy.app.version_string,
        "tooling": {
            "exporterPath": SCRIPT_PATH.relative_to(REPOSITORY_ROOT).as_posix(),
            "exporterSha256": _sha256(SCRIPT_PATH),
            "validatorPath": VALIDATOR_PATH.relative_to(REPOSITORY_ROOT).as_posix(),
            "validatorSha256": _sha256(VALIDATOR_PATH),
        },
        "coordinateSystem": {
            "sourceUpAxis": source["sourceUpAxis"],
            "unitSystem": source["unitSystem"],
            "unitScale": source["unitScale"],
            "runtimeUpAxis": manifest["unityImport"]["upAxis"],
            "runtimeForwardAxis": manifest["unityImport"]["forwardAxis"],
        },
        "exportSet": export_set,
        "selectedObjects": source_object_receipts,
        "validation": {
            "status": validation["status"],
            "errors": validation["errors"],
            "gaps": validation["gaps"],
            "warnings": validation["warnings"],
        },
        "artifact": {
            "path": (
                output.relative_to(REPOSITORY_ROOT).as_posix()
                if output.is_relative_to(REPOSITORY_ROOT)
                else output.as_posix()
            ),
            "sha256": artifact_sha256,
            "bytes": artifact_bytes,
            "format": "GLB",
        },
        "roundTrip": {
            "passed": round_trip_passed,
            "expectedMeshObjects": len(selected_meshes),
            "importedMeshObjects": len(imported_meshes),
            "importerHelperMeshesIgnored": importer_helper_meshes,
            "expectedArtifactMeshDataNames": sorted(selected_mesh_data_names),
            "artifactMeshDataNames": sorted(artifact_mesh_names),
            "artifactMeshSetMatch": artifact_mesh_set_match,
            "artifactNodesIncludeSelection": artifact_nodes_include_selection,
            "expectedTriangles": expected_triangles,
            "importedTriangles": imported_triangles,
            "trianglesMatch": triangles_match,
            "expectedBounds": expected_bounds,
            "importedBounds": imported_bounds,
            "maximumBoundsDeltaMeters": maximum_bounds_delta,
            "boundsToleranceMeters": BOUNDS_TOLERANCE_METERS,
            "boundsMatch": bounds_match,
        },
    }
    _write_receipt(receipt_path, receipt)
    print(f"AL Blender export artifact: {output}")
    print(f"AL Blender export receipt: {receipt_path}")
    print(
        "AL Blender export: "
        f"{'valid' if round_trip_passed else 'invalid'}; "
        f"{len(selected_meshes)} meshes; {expected_triangles} triangles; "
        f"sha256={artifact_sha256}"
    )
    return 0 if round_trip_passed else 3


if __name__ == "__main__":
    raise SystemExit(main())
