#!/usr/bin/env python3
"""Validate a realm-creature FBX UV atlas in Blender."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path
from typing import Any, Sequence

if __package__:
    from tools.terrestrial.repair_realm_creature_geometry import portable_report_path
else:
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
    from tools.terrestrial.repair_realm_creature_geometry import portable_report_path

Point2 = tuple[float, float]
Triangle2 = tuple[Point2, Point2, Point2]


def _signed_area(points: Sequence[Point2]) -> float:
    return 0.5 * sum(
        points[index][0] * points[(index + 1) % len(points)][1]
        - points[(index + 1) % len(points)][0] * points[index][1]
        for index in range(len(points))
    )


def _clip_polygon(subject: list[Point2], edge_a: Point2, edge_b: Point2) -> list[Point2]:
    def inside(point: Point2) -> bool:
        return (
            (edge_b[0] - edge_a[0]) * (point[1] - edge_a[1])
            - (edge_b[1] - edge_a[1]) * (point[0] - edge_a[0])
        ) >= -1e-12

    def intersection(start: Point2, end: Point2) -> Point2:
        sx, sy = start
        ex, ey = end
        ax, ay = edge_a
        bx, by = edge_b
        dx, dy = ex - sx, ey - sy
        cx, cy = bx - ax, by - ay
        denominator = dx * cy - dy * cx
        if abs(denominator) <= 1e-15:
            return end
        factor = ((ax - sx) * cy - (ay - sy) * cx) / denominator
        return (sx + factor * dx, sy + factor * dy)

    result: list[Point2] = []
    if not subject:
        return result
    previous = subject[-1]
    for current in subject:
        if inside(current):
            if not inside(previous):
                result.append(intersection(previous, current))
            result.append(current)
        elif inside(previous):
            result.append(intersection(previous, current))
        previous = current
    return result


def triangle_overlap_area(left: Triangle2, right: Triangle2) -> float:
    clip = list(right if _signed_area(right) >= 0.0 else tuple(reversed(right)))
    result = list(left)
    for index in range(3):
        result = _clip_polygon(result, clip[index], clip[(index + 1) % 3])
        if not result:
            return 0.0
    return abs(_signed_area(result))


def validate_uv_metrics(metrics: dict[str, Any]) -> list[str]:
    diagnostics: list[str] = []
    labels = {
        "uvFacesOutsideUnit": "outside the unit UV square",
        "uvZeroAreaFaces": "zero-area UV faces",
        "uvOverlappingFaces": "overlapping UV faces",
    }
    for field, label in labels.items():
        value = metrics.get(field)
        if value != 0:
            diagnostics.append(f"{field} must be 0 ({label}); got {value!r}")
    return diagnostics


def build_uv_validation_record(
    *,
    model_id: str,
    input_path: str,
    input_sha: str,
    metrics: dict[str, Any],
) -> dict[str, Any]:
    return {
        "modelId": model_id,
        "input": input_path,
        "inputSha256": input_sha,
        **metrics,
        "diagnostics": validate_uv_metrics(metrics),
    }


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def calculate_uv_metrics(mesh: Any, layer_name: str) -> dict[str, Any]:
    uv_data = mesh.uv_layers[layer_name].data
    triangles: list[Triangle2] = []
    face_ids: list[int] = []
    outside_faces: set[int] = set()
    zero_faces: set[int] = set()
    for polygon in mesh.polygons:
        points = [(float(uv_data[index].uv.x), float(uv_data[index].uv.y)) for index in polygon.loop_indices]
        if any(u < -1e-6 or u > 1.0 + 1e-6 or v < -1e-6 or v > 1.0 + 1e-6 for u, v in points):
            outside_faces.add(polygon.index)
        if abs(_signed_area(points)) <= 1e-12:
            zero_faces.add(polygon.index)
        for index in range(1, len(points) - 1):
            triangles.append((points[0], points[index], points[index + 1]))
            face_ids.append(polygon.index)

    grid_size = 64
    buckets: dict[tuple[int, int], list[int]] = {}
    bounds: list[tuple[float, float, float, float]] = []
    for index, triangle in enumerate(triangles):
        min_u = min(point[0] for point in triangle)
        max_u = max(point[0] for point in triangle)
        min_v = min(point[1] for point in triangle)
        max_v = max(point[1] for point in triangle)
        bounds.append((min_u, max_u, min_v, max_v))
        x0 = max(0, min(grid_size - 1, int(math.floor(min_u * grid_size))))
        x1 = max(0, min(grid_size - 1, int(math.floor(max_u * grid_size))))
        y0 = max(0, min(grid_size - 1, int(math.floor(min_v * grid_size))))
        y1 = max(0, min(grid_size - 1, int(math.floor(max_v * grid_size))))
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                buckets.setdefault((x, y), []).append(index)

    checked: set[tuple[int, int]] = set()
    overlapping_faces: set[int] = set()
    for indices in buckets.values():
        for offset, left_index in enumerate(indices):
            for right_index in indices[offset + 1 :]:
                pair = (min(left_index, right_index), max(left_index, right_index))
                if pair in checked or face_ids[left_index] == face_ids[right_index]:
                    continue
                checked.add(pair)
                left_bounds = bounds[left_index]
                right_bounds = bounds[right_index]
                if (
                    left_bounds[1] <= right_bounds[0] + 1e-10
                    or right_bounds[1] <= left_bounds[0] + 1e-10
                    or left_bounds[3] <= right_bounds[2] + 1e-10
                    or right_bounds[3] <= left_bounds[2] + 1e-10
                ):
                    continue
                if triangle_overlap_area(triangles[left_index], triangles[right_index]) > 1e-10:
                    overlapping_faces.add(face_ids[left_index])
                    overlapping_faces.add(face_ids[right_index])

    return {
        "uvLayer": layer_name,
        "uvFacesOutsideUnit": len(outside_faces),
        "uvZeroAreaFaces": len(zero_faces),
        "uvOverlappingFaces": len(overlapping_faces),
    }


def main(argv: Sequence[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--model-id", required=True)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    args = parser.parse_args(argv)
    import bpy

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(args.input.resolve()))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1 or meshes[0].data.uv_layers.active is None:
        raise RuntimeError("expected exactly one mesh with an active UV layer")
    obj = meshes[0]
    metrics = calculate_uv_metrics(obj.data, obj.data.uv_layers.active.name)
    record = build_uv_validation_record(
        model_id=args.model_id,
        input_path=portable_report_path(args.input, args.repo_root),
        input_sha=_sha256(args.input),
        metrics=metrics,
    )
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(record))
    return 1 if record["diagnostics"] else 0


if __name__ == "__main__":
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else sys.argv[1:]
    raise SystemExit(main(arguments))
