"""Render deterministic LOD and technical review sheets for a manifest source.

The source is opened read-only and never saved. The renderer requires explicit LOD
families, lays them out as a front-view LOD matrix, then produces a top-down
collision/nav/socket sheet. Both PNGs and their receipt stay outside Unity read
paths and are review evidence only.
"""

from __future__ import annotations

import argparse
import json
import math
import re
import sys
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

VERSION_TOKEN_PATTERN = re.compile(r"(?:^|[_-])(v[0-9]{3})(?:$|[_-])")


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--technical-output", required=True, type=Path)
    parser.add_argument("--receipt", required=True, type=Path)
    parser.add_argument("--width", type=int, default=1920)
    parser.add_argument("--height", type=int, default=1080)
    parser.add_argument("--samples", type=int, default=32)
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(blender_args)


def _resolve(path: Path) -> Path:
    return path if path.is_absolute() else REPOSITORY_ROOT / path


def _receipt_path(path: Path) -> str:
    return (
        path.relative_to(REPOSITORY_ROOT).as_posix()
        if path.is_relative_to(REPOSITORY_ROOT)
        else path.as_posix()
    )


def _material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float = 0.0,
    roughness: float = 0.65,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    if shader is None:
        raise RuntimeError(f"Principled BSDF is unavailable for {name}")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return material


def _look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def _new_camera(
    collection: bpy.types.Collection,
    location: tuple[float, float, float],
    target: tuple[float, float, float],
    scale: float,
) -> bpy.types.Object:
    data = bpy.data.cameras.new("AL_ReviewCamera")
    data.type = "ORTHO"
    data.ortho_scale = scale
    camera = bpy.data.objects.new("AL_ReviewCamera", data)
    collection.objects.link(camera)
    camera.location = location
    _look_at(camera, Vector(target))
    bpy.context.scene.camera = camera
    return camera


def _new_area_light(
    collection: bpy.types.Collection,
    name: str,
    location: tuple[float, float, float],
    target: tuple[float, float, float],
    energy: float,
    size: float,
) -> bpy.types.Object:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    collection.objects.link(light)
    light.location = location
    _look_at(light, Vector(target))
    return light


def _copy_mesh(
    source: bpy.types.Object,
    collection: bpy.types.Collection,
    location: tuple[float, float, float],
    rotation_z: float = 0.0,
) -> bpy.types.Object:
    obj = source.copy()
    obj.data = source.data.copy()
    obj.animation_data_clear()
    collection.objects.link(obj)
    obj.hide_viewport = False
    obj.hide_render = False
    obj.hide_set(False)
    obj.location = location
    obj.rotation_euler[2] = rotation_z
    return obj


def _box(
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    for current_collection in list(obj.users_collection):
        current_collection.objects.unlink(obj)
    collection.objects.link(obj)
    return obj


def _label(
    body: str,
    location: tuple[float, float, float],
    size: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    rotation_x: float = math.pi * 0.5,
) -> bpy.types.Object:
    data = bpy.data.curves.new(f"{body}_Font", type="FONT")
    data.body = body
    data.align_x = "CENTER"
    data.align_y = "CENTER"
    data.size = size
    data.extrude = 0.006
    data.materials.append(material)
    obj = bpy.data.objects.new(body, data)
    collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler[0] = rotation_x
    return obj


def _socket_marker(
    socket: bpy.types.Object,
    offset: tuple[float, float, float],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    source_location = socket.matrix_world.translation
    location = (
        float(source_location.x) + offset[0],
        float(source_location.y) + offset[1],
        float(source_location.z) + offset[2] + 0.08,
    )
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.09, location=location)
    marker = bpy.context.object
    marker.name = f"REVIEW_{socket.name}"
    marker.data.materials.append(material)
    for current_collection in list(marker.users_collection):
        current_collection.objects.unlink(marker)
    collection.objects.link(marker)
    return marker


def _clear_collection(collection: bpy.types.Collection) -> None:
    for obj in list(collection.objects):
        data = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        if data is not None and data.users == 0:
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            elif isinstance(data, bpy.types.Curve):
                bpy.data.curves.remove(data)
            elif isinstance(data, bpy.types.Camera):
                bpy.data.cameras.remove(data)
            elif isinstance(data, bpy.types.Light):
                bpy.data.lights.remove(data)


def _framing_evidence(
    camera: bpy.types.Object,
    collection: bpy.types.Collection,
) -> dict[str, Any]:
    """Fail closed when any review geometry or label falls outside the camera."""

    scene = bpy.context.scene
    bpy.context.view_layer.update()
    half_width = float(camera.data.ortho_scale) * 0.5
    half_height = half_width * scene.render.resolution_y / scene.render.resolution_x
    camera_inverse = camera.matrix_world.inverted()
    points: list[Vector] = []
    framed_objects: list[str] = []
    for obj in collection.objects:
        if obj.type not in {"MESH", "FONT"} or obj.hide_render:
            continue
        framed_objects.append(obj.name)
        points.extend(
            camera_inverse @ (obj.matrix_world @ Vector(corner))
            for corner in obj.bound_box
        )
    if not points:
        raise RuntimeError("Review framing has no visible geometry")

    bounds = {
        "minimumX": min(float(point.x) for point in points),
        "maximumX": max(float(point.x) for point in points),
        "minimumY": min(float(point.y) for point in points),
        "maximumY": max(float(point.y) for point in points),
    }
    tolerance = 1e-5
    passed = (
        bounds["minimumX"] >= -half_width - tolerance
        and bounds["maximumX"] <= half_width + tolerance
        and bounds["minimumY"] >= -half_height - tolerance
        and bounds["maximumY"] <= half_height + tolerance
    )
    evidence = {
        "passed": passed,
        "cameraHalfWidth": half_width,
        "cameraHalfHeight": half_height,
        "contentCameraBounds": bounds,
        "objectsChecked": sorted(framed_objects),
    }
    if not passed:
        raise RuntimeError(f"Review content falls outside camera frame: {evidence}")
    return evidence


def _prepare_scene(width: int, height: int, samples: int) -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.image_settings.color_depth = "8"
    scene.render.resolution_percentage = 100
    if scene.world is None:
        scene.world = bpy.data.worlds.new("AL_ReviewWorld")
    scene.world.color = (0.025, 0.032, 0.04)
    scene.render.filepath = ""
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.image_settings.compression = 15
    if hasattr(scene, "eevee"):
        scene.eevee.taa_render_samples = samples


def _render(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene.render.filepath = str(path)
    result = bpy.ops.render.render(write_still=True)
    if "FINISHED" not in result or not path.is_file():
        raise RuntimeError(f"Review render failed: {path}")


def _family_layout(source: dict[str, Any]) -> list[dict[str, Any]]:
    lods = _resolved_lods(source)
    families: dict[str, list[dict[str, Any]]] = {}
    for lod in source["lods"]:
        family = lod.get("family")
        if not family:
            continue
        names = lods[lod["id"]]
        if len(names) != 1:
            raise ValueError("Contact-sheet families require one mesh object per LOD")
        families.setdefault(family, []).append(
            {"lodId": lod["id"], "object": names[0]}
        )
    if not families:
        raise ValueError("Source has no explicit LOD families")

    result = []
    for family, family_lods in families.items():
        export_set = next(
            (
                item
                for item in source["exportSets"]
                if {lod["lodId"] for lod in family_lods}.issubset(
                    set(item["objectsFromLods"])
                )
            ),
            None,
        )
        if export_set is None:
            raise ValueError(f"No review export set contains all LODs for {family}")
        result.append(
            {
                "family": family,
                "lods": family_lods,
                "technicalObjects": list(export_set["includeObjects"]),
            }
        )
    return result


def _contact_sheet(
    layout: list[dict[str, Any]],
    review_collection: bpy.types.Collection,
    materials: dict[str, bpy.types.Material],
    output: Path,
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    columns = [
        (index - (len(layout) - 1) * 0.5) * 7.0 for index in range(len(layout))
    ]
    base_heights = [11.0, 6.5, 2.0]
    placed: list[dict[str, Any]] = []
    for column, family in zip(columns, layout, strict=True):
        for lod_index, lod in enumerate(family["lods"]):
            source_obj = bpy.data.objects[lod["object"]]
            location = (column, 0.0, base_heights[lod_index])
            _copy_mesh(
                source_obj,
                review_collection,
                location,
                rotation_z=math.radians(-18.0),
            )
            _box(
                f"REVIEW_Platform_{family['family']}_{lod_index}",
                (column, 0.45, base_heights[lod_index] - 0.08),
                (5.0, 1.3, 0.12),
                materials["platform"],
                review_collection,
            )
            _label(
                f"{family['family']}  LOD{lod_index}",
                (column, -0.85, base_heights[lod_index] - 0.55),
                0.34,
                materials["label"],
                review_collection,
            )
            placed.append(
                {
                    "family": family["family"],
                    "lod": lod_index,
                    "object": lod["object"],
                    "location": list(location),
                }
            )
    # Blender's orthographic scale spans the horizontal frame for this landscape
    # render.  A 28 m scale leaves the complete three-row stack (including labels)
    # inside the narrower vertical field of view at 16:9.
    camera = _new_camera(
        review_collection,
        (0.0, -42.0, 7.8),
        (0.0, 0.0, 7.8),
        28.0,
    )
    _new_area_light(
        review_collection,
        "AL_ReviewKey",
        (-9.0, -14.0, 18.0),
        (0.0, 0.0, 7.0),
        1800.0,
        9.0,
    )
    _new_area_light(
        review_collection,
        "AL_ReviewFill",
        (10.0, -8.0, 10.0),
        (0.0, 0.0, 6.0),
        900.0,
        7.0,
    )
    framing = _framing_evidence(camera, review_collection)
    _render(output)
    return placed, framing


def _technical_sheet(
    layout: list[dict[str, Any]],
    review_collection: bpy.types.Collection,
    materials: dict[str, bpy.types.Material],
    output: Path,
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    columns = [
        (index - (len(layout) - 1) * 0.5) * 7.0 for index in range(len(layout))
    ]
    placed: list[dict[str, Any]] = []
    for column, family in zip(columns, layout, strict=True):
        lod0 = bpy.data.objects[family["lods"][0]["object"]]
        _copy_mesh(lod0, review_collection, (column, 2.3, 0.0))
        collision_names = [
            name for name in family["technicalObjects"] if name.startswith("COL_")
        ]
        nav_names = [
            name for name in family["technicalObjects"] if name.startswith("NAV")
        ]
        socket_names = [
            name for name in family["technicalObjects"] if name.startswith("SOCKET_")
        ]
        for name in collision_names:
            obj = _copy_mesh(
                bpy.data.objects[name], review_collection, (column, 2.3, 0.0)
            )
            obj.data.materials.clear()
            obj.data.materials.append(materials["collision"])
            modifier = obj.modifiers.new("Review collision wire", "WIREFRAME")
            modifier.thickness = 0.025
            modifier.use_replace = True
        for name in nav_names:
            obj = _copy_mesh(
                bpy.data.objects[name], review_collection, (column, -2.7, 0.06)
            )
            obj.data.materials.clear()
            obj.data.materials.append(materials["nav"])
            modifier = obj.modifiers.new("Review nav thickness", "SOLIDIFY")
            modifier.thickness = 0.05
        for name in socket_names:
            _socket_marker(
                bpy.data.objects[name],
                (column, -2.7, 0.0),
                materials["socket"],
                review_collection,
            )
        _label(
            f"{family['family']}  RENDER + COL",
            (column, 0.85, 0.04),
            0.3,
            materials["label"],
            review_collection,
            rotation_x=0.0,
        )
        _label(
            f"NAVEX + {len(socket_names)} SOCKET(S)",
            (column, -4.2, 0.04),
            0.28,
            materials["label"],
            review_collection,
            rotation_x=0.0,
        )
        placed.append(
            {
                "family": family["family"],
                "lod0": family["lods"][0]["object"],
                "collision": collision_names,
                "navigation": nav_names,
                "sockets": socket_names,
            }
        )
    camera = _new_camera(
        review_collection,
        (0.0, 0.0, 24.0),
        (0.0, 0.0, 0.0),
        20.0,
    )
    camera.rotation_euler[2] = 0.0
    _new_area_light(
        review_collection,
        "AL_TechnicalKey",
        (-8.0, -10.0, 18.0),
        (0.0, 0.0, 0.0),
        1700.0,
        10.0,
    )
    framing = _framing_evidence(camera, review_collection)
    _render(output)
    return placed, framing


def main() -> int:
    args = _arguments()
    manifest_path = _resolve(args.manifest)
    output_path = _resolve(args.output)
    technical_path = _resolve(args.technical_output)
    receipt_path = _resolve(args.receipt)
    outputs = (output_path, technical_path, receipt_path)
    if any(path.exists() for path in outputs):
        print("AL review render: refusing to overwrite output", file=sys.stderr)
        return 4
    if output_path.suffix.lower() != ".png" or technical_path.suffix.lower() != ".png":
        print("AL review render: image outputs must be PNG", file=sys.stderr)
        return 4
    if args.width < 320 or args.height < 180 or args.samples < 1:
        print("AL review render: invalid render dimensions/samples", file=sys.stderr)
        return 4

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest_errors = _manifest_diagnostics(manifest)
    if manifest_errors:
        print("AL review render: invalid manifest", file=sys.stderr)
        return 1
    source = next(
        (item for item in manifest["sources"] if item["id"] == args.source), None
    )
    if source is None:
        print(f"AL review render: unknown source {args.source}", file=sys.stderr)
        return 4
    version_tokens = VERSION_TOKEN_PATTERN.findall(Path(source["path"]).stem)
    if not version_tokens or any(version_tokens[-1] not in path.stem for path in outputs):
        print("AL review render: outputs must retain the source version token", file=sys.stderr)
        return 4
    validation = _validate_source(source, manifest)
    if validation["errors"] or validation["gaps"]:
        print("AL review render: source is not technically complete", file=sys.stderr)
        return 2
    try:
        layout = _family_layout(source)
    except ValueError as error:
        print(f"AL review render: {error}", file=sys.stderr)
        return 2

    for obj in bpy.data.objects:
        obj.hide_render = True
    review_collection = bpy.data.collections.new("AL_REVIEW_GENERATED")
    bpy.context.scene.collection.children.link(review_collection)
    materials = {
        "platform": _material("M_ALReview_Platform", (0.05, 0.065, 0.08, 1.0)),
        "label": _material("M_ALReview_Label", (0.72, 0.8, 0.86, 1.0), 0.0, 0.4),
        "collision": _material("M_ALReview_Collision", (1.0, 0.24, 0.05, 1.0), 0.0, 0.3),
        "nav": _material("M_ALReview_Nav", (0.08, 0.55, 0.95, 1.0), 0.0, 0.35),
        "socket": _material("M_ALReview_Socket", (0.1, 1.0, 0.65, 1.0), 0.0, 0.25),
    }
    _prepare_scene(args.width, args.height, args.samples)
    contact_layout, contact_framing = _contact_sheet(
        layout, review_collection, materials, output_path
    )
    _clear_collection(review_collection)
    technical_layout, technical_framing = _technical_sheet(
        layout, review_collection, materials, technical_path
    )

    receipt = {
        "schemaVersion": 1,
        "status": "review_contact_sheets_rendered",
        "sourceId": source["id"],
        "sourcePath": source["path"],
        "sourceSha256": source["sha256"],
        "approvalState": source["approvalState"],
        "promotionEligible": False,
        "manifestSha256": _sha256(manifest_path),
        "blenderVersion": bpy.app.version_string,
        "toolPath": SCRIPT_PATH.relative_to(REPOSITORY_ROOT).as_posix(),
        "toolSha256": _sha256(SCRIPT_PATH),
        "render": {
            "engine": bpy.context.scene.render.engine,
            "width": args.width,
            "height": args.height,
            "samples": args.samples,
        },
        "outputs": {
            "lodContactSheet": {
                "path": _receipt_path(output_path),
                "sha256": _sha256(output_path),
            },
            "technicalContactSheet": {
                "path": _receipt_path(technical_path),
                "sha256": _sha256(technical_path),
            },
        },
        "lodLayout": contact_layout,
        "technicalLayout": technical_layout,
        "framing": {
            "lodContactSheet": contact_framing,
            "technicalContactSheet": technical_framing,
        },
        "validation": {
            "status": validation["status"],
            "errors": validation["errors"],
            "gaps": validation["gaps"],
            "warnings": validation["warnings"],
        },
    }
    receipt_path.parent.mkdir(parents=True, exist_ok=True)
    receipt_path.write_text(
        json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    print(f"AL LOD contact sheet: {output_path}")
    print(f"AL technical contact sheet: {technical_path}")
    print(f"AL review receipt: {receipt_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
