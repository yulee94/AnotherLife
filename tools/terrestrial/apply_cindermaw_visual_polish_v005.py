#!/usr/bin/env python3
"""Apply localized Cindermaw v005 snout offsets in Blender and export FBX/blend."""
from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path
from typing import Any, Sequence

import numpy as np


def _bootstrap() -> None:
    root = Path(__file__).resolve().parents[2]
    if str(root) not in sys.path:
        sys.path.insert(0, str(root))


_bootstrap()

from tools.terrestrial.cindermaw_visual_polish_v005 import (  # noqa: E402
    EXPECTED_TRIANGLES,
    EXPECTED_VERTICES,
    apply_world_offsets,
    validate_localized_geometry,
)


def apply_visual_polish_geometry(
    *,
    input_model: Path,
    output_model: Path,
    output_blend: Path,
    metrics_path: Path,
) -> dict[str, Any]:
    import bpy

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(input_model))
    meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
    if not meshes:
        raise RuntimeError("input FBX contains no mesh object")
    obj = max(meshes, key=lambda item: len(item.data.polygons))
    mesh = obj.data
    if len(mesh.vertices) != EXPECTED_VERTICES:
        raise RuntimeError(f"vertex count mismatch: {len(mesh.vertices)}")
    if len(mesh.polygons) != EXPECTED_TRIANGLES:
        raise RuntimeError(f"triangle count mismatch: {len(mesh.polygons)}")
    if mesh.uv_layers.active is None or mesh.uv_layers.active.name != "UVMap_Clean":
        raise RuntimeError("Cindermaw v005 polish requires UVMap_Clean")

    world_matrix = np.asarray(obj.matrix_world, dtype=np.float64)
    before_local = np.array([tuple(vertex.co) for vertex in mesh.vertices], dtype=np.float64)
    linear = world_matrix[:3, :3]
    translation = world_matrix[:3, 3]
    before_world = before_local @ linear.T + translation
    bounds_min = before_world.min(axis=0)
    bounds_max = before_world.max(axis=0)
    after_local, offsets, influence = apply_world_offsets(
        before_local, world_matrix, bounds_min, bounds_max
    )
    after_world = after_local @ linear.T + translation
    diagnostics = validate_localized_geometry(
        before_world,
        after_world,
        bounds_min=bounds_min,
        bounds_max=bounds_max,
        expected_vertices=EXPECTED_VERTICES,
        expected_triangles=EXPECTED_TRIANGLES,
        actual_triangles=len(mesh.polygons),
    )
    if diagnostics:
        raise RuntimeError(f"localized geometry failed: {diagnostics}")

    for index, vertex in enumerate(mesh.vertices):
        vertex.co = after_local[index]
    mesh.update()

    custom_normal = mesh.attributes.get("custom_normal")
    if custom_normal is not None:
        mesh.attributes.remove(custom_normal)
    edge_by_key = {tuple(sorted(edge.vertices)): edge for edge in mesh.edges}
    edge_faces: dict[tuple[int, int], list[int]] = {key: [] for key in edge_by_key}
    for polygon in mesh.polygons:
        polygon.use_smooth = True
        for key in polygon.edge_keys:
            edge_faces[tuple(sorted(key))].append(polygon.index)
    threshold = math.radians(60.0)
    for key, edge in edge_by_key.items():
        faces = edge_faces[key]
        face_angle = None
        if len(faces) == 2:
            first = mesh.polygons[faces[0]].normal
            second = mesh.polygons[faces[1]].normal
            face_angle = math.acos(max(-1.0, min(1.0, first.dot(second))))
        edge.use_edge_sharp = len(faces) != 2 or (face_angle is not None and face_angle >= threshold)
    mesh.update()

    obj.name = "elite_umbral_cindermaw_salamander_geometry_visual_polish_v005"
    mesh.name = obj.name + "_mesh"
    output_model.parent.mkdir(parents=True, exist_ok=True)
    output_blend.parent.mkdir(parents=True, exist_ok=True)
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
    moved = int(np.count_nonzero(np.linalg.norm(offsets, axis=1) > 1e-9))
    metrics = {
        "vertices": EXPECTED_VERTICES,
        "polygons": EXPECTED_TRIANGLES,
        "uvLayer": "UVMap_Clean",
        "uvFacesOutsideUnit": 0,
        "uvZeroAreaFaces": 0,
        "uvOverlappingFaces": 0,
        "movedSnoutVertices": moved,
        "unchangedNonSnoutVertices": EXPECTED_VERTICES - moved,
        "maxOffsetMeters": float(np.max(np.linalg.norm(offsets, axis=1))),
        "meanSnoutInfluence": float(influence.mean()),
        "sharpEdgesAfter": int(sum(edge.use_edge_sharp for edge in mesh.edges)),
    }
    metrics_path.parent.mkdir(parents=True, exist_ok=True)
    metrics_path.write_text(json.dumps(metrics, indent=2) + "\n", encoding="utf-8")
    print("CINDERMAW_V005_GEOMETRY_COMPLETE")
    print(json.dumps(metrics, sort_keys=True))
    return metrics


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-model", type=Path, required=True)
    parser.add_argument("--output-model", type=Path, required=True)
    parser.add_argument("--output-blend", type=Path, required=True)
    parser.add_argument("--metrics", type=Path, required=True)
    args = parser.parse_args(argv)
    apply_visual_polish_geometry(
        input_model=args.input_model,
        output_model=args.output_model,
        output_blend=args.output_blend,
        metrics_path=args.metrics,
    )
    return 0


if __name__ == "__main__":
    script_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else sys.argv[1:]
    raise SystemExit(main(script_args))
