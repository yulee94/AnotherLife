#!/usr/bin/env python3
"""Re-unwrap and rebake Cindermaw onto a clean non-overlapping UV atlas."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path
from typing import Any, Iterable, Sequence

Point2 = tuple[float, float]
Triangle2 = tuple[Point2, Point2, Point2]


def normal_bake_strategy() -> str:
    return "neutral_tangent"


def export_uv_layer_names() -> tuple[str, ...]:
    return ("UVMap_Clean",)


def uv_topology_strategy() -> str:
    return "triangulate_before_unwrap"


def uv_pack_strategy() -> str:
    return "triangulated_lightmap_pack"


def atlas_finalization_mode() -> str:
    return "external_process"


def _signed_area(points: Sequence[Point2]) -> float:
    return 0.5 * sum(
        points[index][0] * points[(index + 1) % len(points)][1]
        - points[(index + 1) % len(points)][0] * points[index][1]
        for index in range(len(points))
    )


def _line_intersection(start: Point2, end: Point2, first: Point2, second: Point2) -> Point2:
    direction = (end[0] - start[0], end[1] - start[1])
    clip = (second[0] - first[0], second[1] - first[1])
    denominator = direction[0] * clip[1] - direction[1] * clip[0]
    if abs(denominator) < 1e-12:
        return end
    offset = (first[0] - start[0], first[1] - start[1])
    distance = (offset[0] * clip[1] - offset[1] * clip[0]) / denominator
    return (start[0] + direction[0] * distance, start[1] + direction[1] * distance)


def triangle_overlap_area(first: Triangle2, second: Triangle2) -> float:
    """Return positive intersection area; edge/vertex contact returns zero."""
    subject = list(first if _signed_area(first) >= 0.0 else tuple(reversed(first)))
    clip = list(second if _signed_area(second) >= 0.0 else tuple(reversed(second)))
    for index, clip_start in enumerate(clip):
        clip_end = clip[(index + 1) % len(clip)]
        if not subject:
            return 0.0
        output: list[Point2] = []
        previous = subject[-1]
        previous_inside = (
            (clip_end[0] - clip_start[0]) * (previous[1] - clip_start[1])
            - (clip_end[1] - clip_start[1]) * (previous[0] - clip_start[0])
        ) >= -1e-12
        for current in subject:
            current_inside = (
                (clip_end[0] - clip_start[0]) * (current[1] - clip_start[1])
                - (clip_end[1] - clip_start[1]) * (current[0] - clip_start[0])
            ) >= -1e-12
            if current_inside != previous_inside:
                output.append(_line_intersection(previous, current, clip_start, clip_end))
            if current_inside:
                output.append(current)
            previous = current
            previous_inside = current_inside
        subject = output
    return abs(_signed_area(subject)) if len(subject) >= 3 else 0.0


def _is_sha256(value: object) -> bool:
    return isinstance(value, str) and len(value) == 64 and all(char in "0123456789abcdef" for char in value)


def validate_uv_bake_report(report: dict[str, Any]) -> list[str]:
    diagnostics: list[str] = []
    if report.get("modelId") != "elite_umbral_cindermaw_salamander":
        diagnostics.append("modelId must identify Cindermaw Salamander")
    if not report.get("sourceTaskIds"):
        diagnostics.append("at least one source task is required")
    for field in ("inputSha256", "outputSha256"):
        if not _is_sha256(report.get(field)):
            diagnostics.append(f"{field} must be a lowercase SHA-256 digest")
    if report.get("status") != "clean_geometry_pass_uv_bake_complete_normal_detail_rebuild_required":
        diagnostics.append("status must identify UV-bake completion while keeping normal-detail rebuilding fail-closed")
    if report.get("productionReady") is not False:
        diagnostics.append("productionReady must remain false")
    if report.get("rigged") is not False:
        diagnostics.append("rigged must remain false")
    if report.get("runtimeIntegrationState") != "Blocked":
        diagnostics.append("runtimeIntegrationState must remain Blocked")

    metrics = report.get("metrics") or {}
    for field, expected in (
        ("uvLayer", "UVMap_Clean"),
        ("uvFacesOutsideUnit", 0),
        ("uvZeroAreaFaces", 0),
        ("uvOverlappingFaces", 0),
        ("polygonalProjectionBlockerResolved", True),
    ):
        if metrics.get(field) != expected:
            if field == "polygonalProjectionBlockerResolved":
                diagnostics.append("polygonalProjectionBlockerResolved must be true")
            else:
                diagnostics.append(f"{field} must equal {expected}")
    before = int(metrics.get("nonManifoldEdgesBefore", 0))
    after = int(metrics.get("nonManifoldEdgesAfter", 0))
    if after > before:
        diagnostics.append("UV repair must not increase non-manifold edge count")

    required = {
        "base_color": [8192, 8192],
        "normal": [4096, 4096],
        "roughness": [4096, 4096],
        "metallic": [4096, 4096],
        "ao": [4096, 4096],
    }
    maps = {entry.get("name"): entry for entry in report.get("bakedMaps", [])}
    if set(required) - set(maps):
        diagnostics.append("baked maps must include ao, base_color, metallic, normal, and roughness")
    for name, dimensions in required.items():
        entry = maps.get(name)
        if entry is None:
            continue
        if entry.get("dimensions") != dimensions:
            diagnostics.append(f"{name} must be {dimensions[0]}x{dimensions[1]}")
        if not _is_sha256(entry.get("sha256")):
            diagnostics.append(f"{name} must include a lowercase SHA-256 digest")
    return diagnostics


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _non_manifold_edges(mesh: Any) -> int:
    import bmesh

    bm = bmesh.new()
    bm.from_mesh(mesh)
    count = sum(not edge.is_manifold for edge in bm.edges)
    bm.free()
    return count


def _clean_uv_metrics(mesh: Any, layer_name: str) -> dict[str, int | str]:
    uv_layer = mesh.uv_layers[layer_name]
    outside = 0
    zero_area = 0
    triangles: list[tuple[int, Triangle2]] = []
    for polygon in mesh.polygons:
        coordinates = [tuple(uv_layer.data[index].uv) for index in polygon.loop_indices]
        if any(point[0] < -1e-7 or point[0] > 1.0000001 or point[1] < -1e-7 or point[1] > 1.0000001 for point in coordinates):
            outside += 1
        if len(coordinates) != 3:
            continue
        triangle = (coordinates[0], coordinates[1], coordinates[2])
        if abs(_signed_area(triangle)) <= 1e-12:
            zero_area += 1
        else:
            triangles.append((polygon.index, triangle))

    grid_size = 96
    buckets: dict[tuple[int, int], list[int]] = {}
    by_index = {index: triangle for index, triangle in triangles}
    for index, triangle in triangles:
        min_u = max(0, min(grid_size - 1, int(min(point[0] for point in triangle) * grid_size)))
        max_u = max(0, min(grid_size - 1, int(max(point[0] for point in triangle) * grid_size)))
        min_v = max(0, min(grid_size - 1, int(min(point[1] for point in triangle) * grid_size)))
        max_v = max(0, min(grid_size - 1, int(max(point[1] for point in triangle) * grid_size)))
        for cell_u in range(min_u, max_u + 1):
            for cell_v in range(min_v, max_v + 1):
                buckets.setdefault((cell_u, cell_v), []).append(index)
    checked: set[tuple[int, int]] = set()
    overlapping: set[int] = set()
    for indexes in buckets.values():
        for offset, first_index in enumerate(indexes):
            for second_index in indexes[offset + 1:]:
                pair = (min(first_index, second_index), max(first_index, second_index))
                if pair in checked:
                    continue
                checked.add(pair)
                if triangle_overlap_area(by_index[first_index], by_index[second_index]) > 1e-10:
                    overlapping.update(pair)
    return {
        "uvLayer": layer_name,
        "uvFacesOutsideUnit": outside,
        "uvZeroAreaFaces": zero_area,
        "uvOverlappingFaces": len(overlapping),
    }


def _new_material(name: str) -> Any:
    import bpy

    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.node_tree.nodes.clear()
    return material


def _source_emit_material(texture_path: Path, non_color: bool) -> Any:
    import bpy

    material = _new_material(f"BakeSource_{texture_path.stem}")
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(str(texture_path), check_existing=False)
    if non_color:
        texture.image.colorspace_settings.name = "Non-Color"
    emission = nodes.new("ShaderNodeEmission")
    output = nodes.new("ShaderNodeOutputMaterial")
    links.new(texture.outputs["Color"], emission.inputs["Color"])
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


def _assign_material(obj: Any, material: Any) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0


def _flattened_uv_object(target: Any) -> Any:
    import bpy

    legacy = target.data.uv_layers["UVMap_Legacy"]
    clean = target.data.uv_layers["UVMap_Clean"]
    vertices: list[tuple[float, float, float]] = []
    faces: list[list[int]] = []
    legacy_uvs: list[tuple[float, float]] = []
    for polygon in target.data.polygons:
        face: list[int] = []
        for loop_index in polygon.loop_indices:
            clean_uv = clean.data[loop_index].uv
            face.append(len(vertices))
            vertices.append((clean_uv.x, clean_uv.y, 0.0))
            legacy_uv = legacy.data[loop_index].uv
            legacy_uvs.append((legacy_uv.x, legacy_uv.y))
        faces.append(face)
    mesh = bpy.data.meshes.new("Cindermaw_UVTransferMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    sample_layer = mesh.uv_layers.new(name="LegacySample")
    sample_layer.active_render = True
    for loop_index, uv in enumerate(legacy_uvs):
        sample_layer.data[loop_index].uv = uv
    flat = bpy.data.objects.new("Cindermaw_UVTransfer", mesh)
    bpy.context.scene.collection.objects.link(flat)
    return flat


def _render_uv_transfer(target: Any, source_path: Path, output_path: Path, name: str, size: int) -> dict[str, Any]:
    import bpy

    scene = bpy.context.scene
    hidden_states = [(obj, obj.hide_render) for obj in scene.objects]
    for obj, _ in hidden_states:
        obj.hide_render = True
    flat = _flattened_uv_object(target)
    flat.hide_render = False
    _assign_material(flat, _source_emit_material(source_path, name != "base_color"))
    camera_data = bpy.data.cameras.new("Cindermaw_UVTransferCamera")
    camera = bpy.data.objects.new("Cindermaw_UVTransferCamera", camera_data)
    scene.collection.objects.link(camera)
    camera.rotation_euler = (0.0, 0.0, 0.0)
    camera.data.type = "ORTHO"
    scene.camera = camera
    scene.render.engine = "BLENDER_EEVEE"
    tile_size = min(2048, size)
    tile_grid = size // tile_size
    if tile_grid * tile_size != size:
        raise RuntimeError(f"texture size {size} must be divisible by tile size {tile_size}")
    scene.render.resolution_x = tile_size
    scene.render.resolution_y = tile_size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard" if name == "base_color" else "Raw"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    output_path.parent.mkdir(parents=True, exist_ok=True)
    tile_dir = output_path.parent / f".{name}_tiles"
    tile_dir.mkdir(parents=True, exist_ok=True)
    tile_paths: list[str] = []
    span = 1.0 / tile_grid
    camera.data.ortho_scale = span
    for tile_y in range(tile_grid):
        for tile_x in range(tile_grid):
            camera.location = ((tile_x + 0.5) * span, (tile_y + 0.5) * span, 1.0)
            tile_path = tile_dir / f"{tile_y:02d}_{tile_x:02d}.png"
            scene.render.filepath = str(tile_path)
            bpy.ops.render.render(write_still=True)
            tile_paths.append(str(tile_path))

    bpy.data.objects.remove(flat, do_unlink=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    for obj, hidden in hidden_states:
        if obj.name in bpy.data.objects:
            obj.hide_render = hidden
    return {
        "name": name,
        "path": str(output_path),
        "dimensions": [size, size],
        "tiles": tile_paths,
        "tileGrid": tile_grid,
    }


def _bake_map(source: Any, target: Any, source_path: Path, output_path: Path, name: str, size: int) -> dict[str, Any]:
    import bpy

    if name != "normal":
        return _render_uv_transfer(target, source_path, output_path, name, size)
    image = bpy.data.images.new(f"Cindermaw_{name}_v002", width=size, height=size, alpha=False, float_buffer=False)
    image.generated_color = (0.5, 0.5, 1.0, 1.0)
    image.colorspace_settings.name = "Non-Color"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    image.filepath_raw = str(output_path)
    image.file_format = "PNG"
    image.save()
    return {
        "name": name,
        "path": str(output_path),
        "dimensions": [size, size],
        "sha256": _sha256(output_path),
        "provenance": normal_bake_strategy(),
    }


def _repair(args: argparse.Namespace) -> dict[str, Any]:
    import bpy

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(args.input))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"expected one mesh object, found {len(meshes)}")
    source = meshes[0]
    source.name = "Cindermaw_BakeSource_LegacyUV"
    before_non_manifold = _non_manifold_edges(source.data)
    target = source.copy()
    target.data = source.data.copy()
    target.name = "elite_umbral_cindermaw_salamander_geometry_uv_v002"
    target.data.name = target.name
    bpy.context.scene.collection.objects.link(target)
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    triangulate = target.modifiers.new("DCC_TriangulateBeforeUnwrap", "TRIANGULATE")
    triangulate.keep_custom_normals = True
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    for polygon in target.data.polygons:
        polygon.use_smooth = True

    for layer in target.data.uv_layers:
        layer.name = "UVMap_Legacy"
    clean_layer = target.data.uv_layers.new(name="UVMap_Clean")
    target.data.uv_layers.active = clean_layer
    clean_layer.active_render = True
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.lightmap_pack(
        PREF_CONTEXT="ALL_FACES",
        PREF_PACK_IN_ONE=True,
        PREF_NEW_UVLAYER=False,
        PREF_BOX_DIV=48,
        PREF_MARGIN_DIV=0.1,
    )
    bpy.ops.object.mode_set(mode="OBJECT")
    target.data.uv_layers.active = target.data.uv_layers["UVMap_Clean"]
    target.data.uv_layers["UVMap_Clean"].active_render = True
    uv_metrics = _clean_uv_metrics(target.data, "UVMap_Clean")
    if any(uv_metrics[field] for field in ("uvFacesOutsideUnit", "uvZeroAreaFaces", "uvOverlappingFaces")):
        raise RuntimeError(f"clean UV validation failed: {uv_metrics}")

    maps = []
    for name, size in (("base_color", 8192), ("normal", 4096), ("roughness", 4096), ("metallic", 4096), ("ao", 4096)):
        maps.append(
            _bake_map(
                source,
                target,
                args.texture_dir / f"{name}.png",
                args.output_texture_dir / f"{name}.png",
                name,
                size,
            )
        )

    source.hide_render = True
    source.hide_viewport = True
    for layer in list(target.data.uv_layers):
        if layer.name not in export_uv_layer_names():
            target.data.uv_layers.remove(layer)
    target.data.uv_layers.active = target.data.uv_layers["UVMap_Clean"]
    target.data.uv_layers["UVMap_Clean"].active_render = True
    target.data.materials.clear()

    args.blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(args.blend))
    bpy.ops.object.select_all(action="DESELECT")
    target.hide_viewport = False
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    args.output_model.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(args.output_model),
        use_selection=True,
        apply_unit_scale=True,
        bake_anim=False,
        add_leaf_bones=False,
        path_mode="RELATIVE",
        embed_textures=False,
    )
    after_non_manifold = _non_manifold_edges(target.data)
    output_model_sha256 = _sha256(args.output_model)
    stage = {
        "modelId": "elite_umbral_cindermaw_salamander",
        "sourceTaskIds": args.source_task_id,
        "input": str(args.input),
        "inputSha256": _sha256(args.input),
        "output": str(args.output_model),
        "outputSha256": output_model_sha256,
        "editableBlend": str(args.blend),
        "metrics": {
            **uv_metrics,
            "nonManifoldEdgesBefore": before_non_manifold,
            "nonManifoldEdgesAfter": after_non_manifold,
        },
        "stagedMaps": maps,
        "finalizationMode": atlas_finalization_mode(),
    }
    stage_path = args.report.with_suffix(".stage.json")
    stage_path.parent.mkdir(parents=True, exist_ok=True)
    stage_path.write_text(json.dumps(stage, indent=2) + "\n", encoding="utf-8")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return stage


def _parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--texture-dir", type=Path, required=True)
    parser.add_argument("--output-model", type=Path, required=True)
    parser.add_argument("--output-texture-dir", type=Path, required=True)
    parser.add_argument("--blend", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--source-task-id", action="append", required=True)
    return parser.parse_args(argv)


def main() -> int:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    args = _parse_args(argv)
    stage = _repair(args)
    print(json.dumps({"status": "STAGED", "stage": str(args.report.with_suffix('.stage.json')), "metrics": stage["metrics"]}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
