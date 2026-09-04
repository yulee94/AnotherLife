#!/usr/bin/env python3
"""Repair Cindermaw's imported split normals without changing topology or UVs."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
from typing import Any, Sequence


MODEL_ID = "elite_umbral_cindermaw_salamander"
SOURCE_TASK_IDS = [
    "01a05f90-dc1f-723e-9e7a-4e3feb8f3dbc",
    "01a05fa3-16b8-70f5-a0bd-cca9f316e455",
    "01a06569-2956-73a2-a51e-bade35802fba",
]


def should_mark_edge_sharp(
    *,
    face_angle: float | None,
    is_boundary: bool,
    threshold_radians: float = math.radians(60.0),
) -> bool:
    """Keep open boundaries and deliberate hard angles sharp."""
    return is_boundary or (face_angle is not None and face_angle >= threshold_radians)


def build_smoothing_report(
    *,
    input_path: str,
    input_sha256: str,
    output_path: str,
    output_sha256: str,
    blend_path: str,
    blend_sha256: str,
    metrics: dict[str, Any],
) -> dict[str, Any]:
    return {
        "modelId": MODEL_ID,
        "sourceTaskIds": SOURCE_TASK_IDS,
        "input": input_path,
        "inputSha256": input_sha256,
        "output": output_path,
        "outputSha256": output_sha256,
        "editableBlend": blend_path,
        "editableBlendSha256": blend_sha256,
        "status": "clean_geometry_pass_uv_bake_pass_smoothing_pass_normal_detail_rebuild_required",
        "productionReady": False,
        "rigged": False,
        "runtimeIntegrationState": "Blocked",
        "operations": [
            "removed imported per-corner custom normals that preserved faceted triangle shading",
            "recomputed smooth shading while retaining boundaries and deliberate 60-degree hard edges",
            "preserved topology, clean UV coordinates, dimensions, and runtime-VFX separation",
        ],
        "metrics": metrics,
        "diagnostics": [],
    }


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _portable(path: Path, repo_root: Path) -> str:
    return path.resolve().relative_to(repo_root.resolve()).as_posix()


def repair_smoothing(
    *,
    input_model: Path,
    output_model: Path,
    output_blend: Path,
    report_path: Path,
    repo_root: Path,
    angle_degrees: float,
) -> dict[str, Any]:
    import bpy

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(input_model))
    meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
    if not meshes:
        raise RuntimeError("input FBX contains no mesh object")
    obj = max(meshes, key=lambda item: len(item.data.polygons))
    mesh = obj.data
    if len(mesh.uv_layers) != 1 or mesh.uv_layers.active.name != "UVMap_Clean":
        raise RuntimeError("Cindermaw smoothing requires exactly UVMap_Clean")

    vertices_before = len(mesh.vertices)
    polygons_before = len(mesh.polygons)
    sharp_before = sum(edge.use_edge_sharp for edge in mesh.edges)
    custom_normal = mesh.attributes.get("custom_normal")
    custom_normals_removed = custom_normal is not None
    if custom_normal is not None:
        mesh.attributes.remove(custom_normal)

    edge_by_key = {tuple(sorted(edge.vertices)): edge for edge in mesh.edges}
    edge_faces: dict[tuple[int, int], list[int]] = {key: [] for key in edge_by_key}
    for polygon in mesh.polygons:
        polygon.use_smooth = True
        for key in polygon.edge_keys:
            edge_faces[tuple(sorted(key))].append(polygon.index)

    threshold = math.radians(angle_degrees)
    for key, edge in edge_by_key.items():
        faces = edge_faces[key]
        face_angle = None
        if len(faces) == 2:
            first = mesh.polygons[faces[0]].normal
            second = mesh.polygons[faces[1]].normal
            face_angle = math.acos(max(-1.0, min(1.0, first.dot(second))))
        edge.use_edge_sharp = should_mark_edge_sharp(
            face_angle=face_angle,
            is_boundary=len(faces) != 2,
            threshold_radians=threshold,
        )
    mesh.update()
    obj.name = "elite_umbral_cindermaw_salamander_geometry_uv_smooth_v004"
    mesh.name = obj.name + "_mesh"

    output_model.parent.mkdir(parents=True, exist_ok=True)
    output_blend.parent.mkdir(parents=True, exist_ok=True)
    report_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output_blend))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=str(output_model),
        use_selection=True,
        apply_unit_scale=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="EDGE",
        use_tspace=True,
        add_leaf_bones=False,
        path_mode="AUTO",
    )

    metrics = {
        "vertices": vertices_before,
        "polygons": polygons_before,
        "uvLayer": "UVMap_Clean",
        "sharpEdgesBefore": sharp_before,
        "sharpEdgesAfter": sum(edge.use_edge_sharp for edge in mesh.edges),
        "smoothingAngleDegrees": angle_degrees,
        "customNormalsRemoved": custom_normals_removed,
    }
    report = build_smoothing_report(
        input_path=_portable(input_model, repo_root),
        input_sha256=_sha256(input_model),
        output_path=_portable(output_model, repo_root),
        output_sha256=_sha256(output_model),
        blend_path=_portable(output_blend, repo_root),
        blend_sha256=_sha256(output_blend),
        metrics=metrics,
    )
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-model", type=Path, required=True)
    parser.add_argument("--output-model", type=Path, required=True)
    parser.add_argument("--output-blend", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--angle-degrees", type=float, default=60.0)
    args = parser.parse_args(argv)
    report = repair_smoothing(
        input_model=args.input_model,
        output_model=args.output_model,
        output_blend=args.output_blend,
        report_path=args.report,
        repo_root=args.repo_root,
        angle_degrees=args.angle_degrees,
    )
    print(json.dumps(report["metrics"], sort_keys=True))
    return 0


if __name__ == "__main__":
    script_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else sys.argv[1:]
    raise SystemExit(main(script_args))
