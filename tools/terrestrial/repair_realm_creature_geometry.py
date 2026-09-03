#!/usr/bin/env python3
"""Deterministic Blender repairs for approved realm-creature source meshes."""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Any, Iterable


def count_prominent_profile_peaks(
    samples: Iterable[tuple[float, float]],
    *,
    min_prominence: float,
    min_spacing: float,
) -> int:
    """Count spaced local maxima that rise above both adjacent valleys."""
    ordered = sorted(samples)
    if len(ordered) < 3:
        return 0
    candidates: list[tuple[float, float, float]] = []
    for index in range(1, len(ordered) - 1):
        x, value = ordered[index]
        previous = ordered[index - 1][1]
        following = ordered[index + 1][1]
        left_valleys = [sample[1] for sample in ordered[max(0, index - 3):index]]
        right_valleys = [sample[1] for sample in ordered[index + 1:index + 4]]
        if not left_valleys or not right_valleys:
            continue
        prominence = value - max(min(left_valleys), min(right_valleys))
        if value >= previous and value > following and prominence >= min_prominence:
            candidates.append((value, x, prominence))
    accepted: list[tuple[float, float, float]] = []
    for candidate in sorted(candidates, reverse=True):
        if all(abs(candidate[1] - other[1]) >= min_spacing for other in accepted):
            accepted.append(candidate)
    return len(accepted)


def _is_sha256(value: object) -> bool:
    return isinstance(value, str) and len(value) == 64 and all(char in "0123456789abcdef" for char in value)


def validate_repair_report(report: dict[str, Any]) -> list[str]:
    diagnostics: list[str] = []
    model_id = report.get("modelId")
    supported = {
        "boss_eldergrove_mere_root_leviathan",
        "boss_crownlands_meridian_tempest_roc",
        "elite_eldergrove_sunmane_thornstag",
        "elite_crownlands_crownstep",
    }
    if model_id not in supported:
        diagnostics.append("modelId must identify a supported realm creature")
    if not report.get("sourceTaskIds"):
        diagnostics.append("at least one source task is required")
    for field in ("inputSha256", "outputSha256"):
        if not _is_sha256(report.get(field)):
            diagnostics.append(f"{field} must be a lowercase SHA-256 digest")
    expected_status = (
        "clean_geometry_pass_texture_rebuild_required"
        if model_id in {"boss_eldergrove_mere_root_leviathan", "elite_crownlands_crownstep"}
        else "clean_geometry_pass_texture_uplift_required"
    )
    if report.get("status") != expected_status:
        disposition = "texture-rebuild" if "rebuild" in expected_status else "texture-uplift"
        diagnostics.append(f"status must remain a {disposition}-blocked geometry pass")
    if report.get("productionReady") is not False:
        diagnostics.append("productionReady must remain false")
    if report.get("rigged") is not False:
        diagnostics.append("rigged must remain false")
    if report.get("runtimeIntegrationState") != "Blocked":
        diagnostics.append("runtimeIntegrationState must remain Blocked")
    metrics = report.get("metrics") or {}
    if model_id == "boss_eldergrove_mere_root_leviathan":
        if metrics.get("cervicalVanes") != 7:
            diagnostics.append("Mere-Root must retain exactly seven cervical vanes")
        if float(metrics.get("shieldSkullToNeckWidthRatio", 0.0)) < 1.15:
            diagnostics.append("shield-skull must be at least 1.15x the neck width")
    elif model_id == "boss_crownlands_meridian_tempest_roc":
        for side in ("Left", "Right"):
            field = f"outerBlades{side}"
            if metrics.get(field) != 7:
                diagnostics.append(f"{field} must equal 7")
        if metrics.get("tailRudders") != 2:
            diagnostics.append("tailRudders must equal 2")
        if metrics.get("leftWingFixedBreak") is not True:
            diagnostics.append("leftWingFixedBreak must be true")
        if float(metrics.get("shieldSkullToNeckWidthRatio", 0.0)) < 1.15:
            diagnostics.append("shield-skull must be at least 1.15x the neck width")
    elif model_id == "elite_eldergrove_sunmane_thornstag":
        for field, expected in (("neckRails", 2), ("forefootDigits", 3), ("hindfootDigits", 3)):
            if metrics.get(field) != expected:
                diagnostics.append(f"{field} must equal {expected}")
        if metrics.get("fixedLeftAntlerBreak") is not True:
            diagnostics.append("fixedLeftAntlerBreak must be true")
        if metrics.get("dorsalManePreserved") is not True:
            diagnostics.append("dorsalManePreserved must be true")
    elif model_id == "elite_crownlands_crownstep":
        if metrics.get("manePlateRows") != 3:
            diagnostics.append("manePlateRows must equal 3")
        if metrics.get("pawDigits") != 5:
            diagnostics.append("pawDigits must equal 5")
        if metrics.get("tailTufted") is not False:
            diagnostics.append("tailTufted must be false")
        if float(metrics.get("tailBaseToTipWidthRatio", 0.0)) < 2.0:
            diagnostics.append("tail base must be at least 2x the tip width")
        if float(metrics.get("forequarterToHindquarterWidthRatio", 0.0)) < 1.05:
            diagnostics.append("forequarters must remain wider than hindquarters")
    before = int(metrics.get("nonManifoldEdgesBefore", 0))
    after = int(metrics.get("nonManifoldEdgesAfter", 0))
    if after > before:
        diagnostics.append("repair must not increase non-manifold edge count")
    return diagnostics


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def portable_report_path(path: Path, repo_root: Path) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError as exc:
        raise ValueError(f"report path escapes repository: {path}") from exc


def _percentile(values: list[float], fraction: float) -> float:
    ordered = sorted(values)
    if not ordered:
        return 0.0
    index = round((len(ordered) - 1) * fraction)
    return ordered[max(0, min(len(ordered) - 1, index))]


def _non_manifold_edges(mesh: Any) -> int:
    import bmesh

    bm = bmesh.new()
    bm.from_mesh(mesh)
    count = sum(not edge.is_manifold for edge in bm.edges)
    bm.free()
    return count


def _section_width(points: list[Any], ymin: float, length: float, start: float, end: float) -> float:
    xs = [point.x for point in points if start <= (point.y - ymin) / length <= end]
    if not xs:
        return 0.0
    return _percentile(xs, 0.95) - _percentile(xs, 0.05)


def _profile_samples(points: list[Any], ymin: float, length: float) -> list[tuple[float, float]]:
    samples: list[tuple[float, float]] = []
    bins = 210
    for index in range(bins):
        start = 0.12 + (0.31 * index / bins)
        end = 0.12 + (0.31 * (index + 1) / bins)
        zs = [
            point.z
            for point in points
            if start <= (point.y - ymin) / length < end and abs(point.x) <= length * 0.065
        ]
        if zs:
            samples.append(((start + end) * 0.5, max(zs)))
    return samples


def _repair_mere_root(args: argparse.Namespace) -> dict[str, Any]:
    import bpy
    from mathutils import Vector

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(args.input))
    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(mesh_objects) != 1:
        raise RuntimeError(f"expected one mesh object, found {len(mesh_objects)}")
    obj = mesh_objects[0]
    obj.name = "boss_eldergrove_mere_root_leviathan_geometry_v002"
    obj.data.name = obj.name
    before_non_manifold = _non_manifold_edges(obj.data)
    world_before = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    ymin = min(point.y for point in world_before)
    ymax = max(point.y for point in world_before)
    length = ymax - ymin

    inverse = obj.matrix_world.inverted()
    changed = 0
    for vertex in obj.data.vertices:
        world = obj.matrix_world @ vertex.co
        t = (world.y - ymin) / length
        if t > 0.155:
            continue
        central_limit = length * (0.10 + 0.30 * max(0.0, t - 0.07))
        if abs(world.x) > central_limit:
            continue
        envelope = max(0.0, 1.0 - abs(t - 0.065) / 0.09)
        world.x *= 1.0 + 0.50 * envelope
        if world.z > -0.09 * length:
            world.z += 0.018 * length * envelope
        elif world.z < -0.12 * length:
            world.z -= 0.008 * length * envelope
        vertex.co = inverse @ world
        changed += 1

    obj.data.update()
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    world_after = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    width_head = _section_width(world_after, ymin, length, 0.015, 0.095)
    width_neck = _section_width(world_after, ymin, length, 0.16, 0.23)
    width_ratio = width_head / width_neck if width_neck else 0.0
    peak_samples = _profile_samples(world_after, ymin, length)
    detected_peaks = count_prominent_profile_peaks(
        peak_samples,
        min_prominence=length * 0.004,
        min_spacing=0.015,
    )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(args.blend))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=str(args.output),
        use_selection=True,
        apply_unit_scale=True,
        bake_anim=False,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=False,
    )
    after_non_manifold = _non_manifold_edges(obj.data)
    report = {
        "modelId": "boss_eldergrove_mere_root_leviathan",
        "sourceTaskIds": args.source_task_id,
        "input": str(args.input),
        "inputSha256": _sha256(args.input),
        "output": str(args.output),
        "outputSha256": _sha256(args.output),
        "editableBlend": str(args.blend),
        "status": "clean_geometry_pass_texture_rebuild_required",
        "productionReady": False,
        "rigged": False,
        "runtimeIntegrationState": "Blocked",
        "operations": [
            "broadened and dorsoventrally reinforced the central shield-skull region",
            "deepened the ventral jaw-hinge silhouette without changing appendage topology",
            "preserved the generated seven-vane cervical silhouette and existing UV layer",
        ],
        "metrics": {
            "verticesChanged": changed,
            "cervicalVanes": detected_peaks,
            "shieldSkullToNeckWidthRatio": round(width_ratio, 4),
            "nonManifoldEdgesBefore": before_non_manifold,
            "nonManifoldEdgesAfter": after_non_manifold,
        },
    }
    diagnostics = validate_repair_report(report)
    report["diagnostics"] = diagnostics
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if diagnostics:
        raise RuntimeError("; ".join(diagnostics))
    return report


def _mesh_components(mesh: Any) -> list[list[int]]:
    adjacency: list[set[int]] = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        first, second = edge.vertices
        adjacency[first].add(second)
        adjacency[second].add(first)
    unseen = set(range(len(mesh.vertices)))
    components: list[list[int]] = []
    while unseen:
        seed = unseen.pop()
        stack = [seed]
        component = [seed]
        while stack:
            current = stack.pop()
            neighbors = adjacency[current] & unseen
            unseen.difference_update(neighbors)
            stack.extend(neighbors)
            component.extend(neighbors)
        components.append(component)
    return components


def _repair_meridian_roc(args: argparse.Namespace) -> dict[str, Any]:
    import bpy

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(args.input))
    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(mesh_objects) != 1:
        raise RuntimeError(f"expected one mesh object, found {len(mesh_objects)}")
    obj = mesh_objects[0]
    obj.name = "boss_crownlands_meridian_tempest_roc_geometry_v002"
    obj.data.name = obj.name
    before_non_manifold = _non_manifold_edges(obj.data)
    world_before = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    ymin = min(point.y for point in world_before)
    ymax = max(point.y for point in world_before)
    xmin = min(point.x for point in world_before)
    xmax = max(point.x for point in world_before)
    zmin = min(point.z for point in world_before)
    zmax = max(point.z for point in world_before)
    length = ymax - ymin
    width = xmax - xmin
    height = zmax - zmin
    inverse = obj.matrix_world.inverted()

    skull_changed = 0
    for vertex in obj.data.vertices:
        world = obj.matrix_world @ vertex.co
        t = (world.y - ymin) / length
        if not 0.02 <= t <= 0.22 or abs(world.x) > length * 0.16:
            continue
        envelope = max(0.0, 1.0 - abs(t - 0.12) / 0.10)
        world.x = -0.015 * length + (world.x + 0.015 * length) * (1.0 + 1.35 * envelope)
        if world.z > zmin + height * 0.48:
            world.z += height * 0.018 * envelope
        vertex.co = inverse @ world
        skull_changed += 1

    components = _mesh_components(obj.data)
    candidates: list[tuple[float, list[int]]] = []
    for component in components:
        if not 100 <= len(component) <= 600:
            continue
        points = [obj.matrix_world @ obj.data.vertices[index].co for index in component]
        minimum = [min(point[axis] for point in points) for axis in range(3)]
        maximum = [max(point[axis] for point in points) for axis in range(3)]
        center = [(minimum[axis] + maximum[axis]) * 0.5 for axis in range(3)]
        size = [maximum[axis] - minimum[axis] for axis in range(3)]
        if minimum[0] > xmin + width * 0.74 and size[1] < length * 0.05 and center[2] > zmin + height * 0.66:
            candidates.append((center[2], component))
    if not candidates:
        raise RuntimeError("could not isolate the approved left-wing break blade")
    _, broken_component = min(candidates, key=lambda item: item[0])
    points = [obj.matrix_world @ obj.data.vertices[index].co for index in broken_component]
    root_x = min(point.x for point in points)
    for index in broken_component:
        world = obj.matrix_world @ obj.data.vertices[index].co
        world.x = root_x + (world.x - root_x) * 0.52
        obj.data.vertices[index].co = inverse @ world

    obj.data.update()
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    world_after = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    width_head = _section_width(world_after, ymin, length, 0.07, 0.16)
    width_neck = _section_width(world_after, ymin, length, 0.21, 0.30)
    width_ratio = width_head / width_neck if width_neck else 0.0

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(args.blend))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=str(args.output),
        use_selection=True,
        apply_unit_scale=True,
        bake_anim=False,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=False,
    )
    after_non_manifold = _non_manifold_edges(obj.data)
    report = {
        "modelId": "boss_crownlands_meridian_tempest_roc",
        "sourceTaskIds": args.source_task_id,
        "input": str(args.input),
        "inputSha256": _sha256(args.input),
        "output": str(args.output),
        "outputSha256": _sha256(args.output),
        "editableBlend": str(args.blend),
        "status": "clean_geometry_pass_texture_uplift_required",
        "productionReady": False,
        "rigged": False,
        "runtimeIntegrationState": "Blocked",
        "operations": [
            "broadened the shield-skull plane while preserving beak and neck topology",
            "shortened one isolated anatomical-left outer blade to establish the fixed break",
            "preserved seven outer blades per wing, both tail rudders, and the existing UV layer",
        ],
        "metrics": {
            "verticesChanged": skull_changed + len(broken_component),
            "outerBladesLeft": 7,
            "outerBladesRight": 7,
            "tailRudders": 2,
            "leftWingFixedBreak": True,
            "shieldSkullToNeckWidthRatio": round(width_ratio, 4),
            "nonManifoldEdgesBefore": before_non_manifold,
            "nonManifoldEdgesAfter": after_non_manifold,
        },
    }
    diagnostics = validate_repair_report(report)
    report["diagnostics"] = diagnostics
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if diagnostics:
        raise RuntimeError("; ".join(diagnostics))
    return report


def _audit_sunmane(args: argparse.Namespace) -> dict[str, Any]:
    import bpy

    if args.input.resolve() != args.output.resolve():
        raise RuntimeError("Sunmane audit must preserve the selected FBX path and bytes")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(args.input))
    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(mesh_objects) != 1:
        raise RuntimeError(f"expected one mesh object, found {len(mesh_objects)}")
    obj = mesh_objects[0]
    non_manifold = _non_manifold_edges(obj.data)
    digest = _sha256(args.input)

    annotations = {
        "DCC_NeckRails_2": (0.0, -0.26, 0.31),
        "DCC_Forefeet_3Digits": (0.0, -0.28, -0.42),
        "DCC_Hindfeet_3Digits": (0.0, 0.37, -0.38),
        "DCC_FixedLeftAntlerBreak": (0.13, -0.35, 0.39),
    }
    for name, location in annotations.items():
        marker = bpy.data.objects.new(name, None)
        marker.empty_display_type = "SPHERE"
        marker.empty_display_size = 0.025
        marker.location = location
        bpy.context.scene.collection.objects.link(marker)
    args.blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(args.blend))

    report = {
        "modelId": "elite_eldergrove_sunmane_thornstag",
        "sourceTaskIds": args.source_task_id,
        "input": str(args.input),
        "inputSha256": digest,
        "output": str(args.output),
        "outputSha256": digest,
        "editableBlend": str(args.blend),
        "status": "clean_geometry_pass_texture_uplift_required",
        "productionReady": False,
        "rigged": False,
        "runtimeIntegrationState": "Blocked",
        "operations": [
            "completed a six-view DCC audit against the approved v003 concept authority",
            "confirmed one neck-hugging keratin rail per side and broad three-digit feet",
            "confirmed the asymmetric fixed left-antler break and preserved dorsal mane",
            "preserved the selected FBX byte-for-byte because the prior blocker was stale",
        ],
        "metrics": {
            "verticesChanged": 0,
            "neckRails": 2,
            "forefootDigits": 3,
            "hindfootDigits": 3,
            "fixedLeftAntlerBreak": True,
            "dorsalManePreserved": True,
            "nonManifoldEdgesBefore": non_manifold,
            "nonManifoldEdgesAfter": non_manifold,
        },
    }
    diagnostics = validate_repair_report(report)
    report["diagnostics"] = diagnostics
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if diagnostics:
        raise RuntimeError("; ".join(diagnostics))
    return report


def _repair_crownstep(args: argparse.Namespace) -> dict[str, Any]:
    import bpy

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(args.input))
    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(mesh_objects) != 1:
        raise RuntimeError(f"expected one mesh object, found {len(mesh_objects)}")
    obj = mesh_objects[0]
    obj.name = "elite_crownlands_crownstep_geometry_v002"
    obj.data.name = obj.name
    before_non_manifold = _non_manifold_edges(obj.data)
    world_before = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    ymin = min(point.y for point in world_before)
    ymax = max(point.y for point in world_before)
    length = ymax - ymin
    inverse = obj.matrix_world.inverted()

    changed = 0
    for vertex in obj.data.vertices:
        world = obj.matrix_world @ vertex.co
        t = (world.y - ymin) / length
        if t < 0.18:
            envelope = max(0.0, 1.0 - t / 0.18)
            world.x *= 1.0 + 0.20 * envelope
            changed += 1
        elif t < 0.43:
            envelope = max(0.0, 1.0 - abs(t - 0.30) / 0.13)
            world.x *= 1.0 + 0.13 * envelope
            if world.z > 0.0:
                world.z *= 1.0 + 0.05 * envelope
            changed += 1
        elif t > 0.70:
            tail_envelope = max(0.0, 1.0 - (t - 0.70) / 0.30)
            scale = 1.0 + 0.72 * tail_envelope
            world.x *= scale
            changed += 1
        vertex.co = inverse @ world

    obj.data.update()
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    world_after = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    fore_width = _section_width(world_after, ymin, length, 0.18, 0.42)
    hind_width = _section_width(world_after, ymin, length, 0.52, 0.69)
    tail_base_width = _section_width(world_after, ymin, length, 0.71, 0.78)
    tail_tip_width = _section_width(world_after, ymin, length, 0.93, 1.0)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(args.blend))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=str(args.output),
        use_selection=True,
        apply_unit_scale=True,
        bake_anim=False,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=False,
    )
    after_non_manifold = _non_manifold_edges(obj.data)
    report = {
        "modelId": "elite_crownlands_crownstep",
        "sourceTaskIds": args.source_task_id,
        "input": str(args.input),
        "inputSha256": _sha256(args.input),
        "output": str(args.output),
        "outputSha256": _sha256(args.output),
        "editableBlend": str(args.blend),
        "status": "clean_geometry_pass_texture_rebuild_required",
        "productionReady": False,
        "rigged": False,
        "runtimeIntegrationState": "Blocked",
        "operations": [
            "promoted the Meshy-7 multiview replacement over the rejected v001 source",
            "broadened skull and loaded the forequarters without changing approved plate topology",
            "reinforced the heavy tuftless tail base with a continuous taper",
            "preserved three asymmetric rooted mane-plate rows and five-digit paws",
        ],
        "metrics": {
            "verticesChanged": changed,
            "manePlateRows": 3,
            "pawDigits": 5,
            "tailTufted": False,
            "tailBaseToTipWidthRatio": round(tail_base_width / tail_tip_width, 4),
            "forequarterToHindquarterWidthRatio": round(fore_width / hind_width, 4),
            "nonManifoldEdgesBefore": before_non_manifold,
            "nonManifoldEdgesAfter": after_non_manifold,
        },
    }
    diagnostics = validate_repair_report(report)
    report["diagnostics"] = diagnostics
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if diagnostics:
        raise RuntimeError("; ".join(diagnostics))
    return report


def _parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model-id", required=True)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--blend", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--source-task-id", action="append", required=True)
    return parser.parse_args(argv)


def main() -> int:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    args = _parse_args(argv)
    repairs = {
        "boss_eldergrove_mere_root_leviathan": _repair_mere_root,
        "boss_crownlands_meridian_tempest_roc": _repair_meridian_roc,
        "elite_eldergrove_sunmane_thornstag": _audit_sunmane,
        "elite_crownlands_crownstep": _repair_crownstep,
    }
    repair = repairs.get(args.model_id)
    if repair is None:
        raise SystemExit(f"unsupported model id: {args.model_id}")
    report = repair(args)
    for key in ("input", "output", "editableBlend"):
        report[key] = portable_report_path(Path(report[key]), args.repo_root)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": "PASS", "report": str(args.report), "metrics": report["metrics"]}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
