#!/usr/bin/env python3
"""Render Cindermaw v005 neutral close-up and full-body hero reviews."""
from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Any, Sequence


def _bootstrap() -> None:
    root = Path(__file__).resolve().parents[2]
    if str(root) not in sys.path:
        sys.path.insert(0, str(root))


_bootstrap()

from tools.terrestrial.cindermaw_visual_polish_v005 import review_specs


def _load_image(bpy: Any, path: Path, *, non_color: bool, limit: int = 2048) -> Any:
    image = bpy.data.images.load(str(path))
    if image.size[0] > limit or image.size[1] > limit:
        image.scale(limit, limit)
    if non_color:
        image.colorspace_settings.name = "Non-Color"
    return image


def _assign_material(bpy: Any, obj: Any, texture_root: Path) -> None:
    material = bpy.data.materials.new("Cindermaw_VisualPolish_V005")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    base = nodes.new("ShaderNodeTexImage")
    base.image = _load_image(bpy, texture_root / "base_color.png", non_color=False)
    ao = nodes.new("ShaderNodeTexImage")
    ao.image = _load_image(bpy, texture_root / "ao.png", non_color=True)
    multiply = nodes.new("ShaderNodeMixRGB")
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 0.22
    roughness = nodes.new("ShaderNodeTexImage")
    roughness.image = _load_image(bpy, texture_root / "roughness.png", non_color=True)
    metallic = nodes.new("ShaderNodeTexImage")
    metallic.image = _load_image(bpy, texture_root / "metallic.png", non_color=True)
    normal_texture = nodes.new("ShaderNodeTexImage")
    normal_texture.image = _load_image(bpy, texture_root / "normal.png", non_color=True)
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 1.0
    links.new(base.outputs["Color"], multiply.inputs[1])
    links.new(ao.outputs["Color"], multiply.inputs[2])
    links.new(multiply.outputs["Color"], shader.inputs["Base Color"])
    links.new(roughness.outputs["Color"], shader.inputs["Roughness"])
    links.new(metallic.outputs["Color"], shader.inputs["Metallic"])
    links.new(normal_texture.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    obj.data.materials.clear()
    obj.data.materials.append(material)


def _point_at(item: Any, target: Any) -> None:
    item.rotation_euler = (target - item.location).to_track_quat("-Z", "Y").to_euler()


def render_reviews(repo_root: Path) -> list[Path]:
    import bpy
    from mathutils import Vector

    specs = review_specs()
    model_path = repo_root / specs[0]["modelPath"]
    texture_root = repo_root / specs[0]["textureRoot"]
    required = [
        model_path,
        texture_root / "base_color.png",
        texture_root / "roughness.png",
        texture_root / "metallic.png",
        texture_root / "ao.png",
        texture_root / "normal.png",
    ]
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise RuntimeError(f"missing review inputs: {missing}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(model_path))
    obj = max(
        (item for item in bpy.context.scene.objects if item.type == "MESH"),
        key=lambda item: len(item.data.polygons),
    )
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    _assign_material(bpy, obj, texture_root)

    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector((min(item.x for item in corners), min(item.y for item in corners), min(item.z for item in corners)))
    maximum = Vector((max(item.x for item in corners), max(item.y for item in corners), max(item.z for item in corners)))
    center = (minimum + maximum) * 0.5
    extent = maximum - minimum
    scale = max(extent)
    snout = Vector((center.x, minimum.y + 0.12 * extent.y, center.z + 0.08 * extent.z))

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new("CindermawV005ReviewWorld")
    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    written: list[Path] = []
    for spec in specs:
        for light in [item for item in scene.objects if item.type == "LIGHT"]:
            bpy.data.objects.remove(light, do_unlink=True)
        if spec["lighting"] == "neutral":
            scene.world.color = (0.18, 0.18, 0.19)
            lights = (
                ("Key", (-0.55, -1.15, 0.85), 280.0, (1.0, 0.98, 0.94), 2.4),
                ("Fill", (0.95, -0.35, 0.35), 90.0, (0.86, 0.90, 0.96), 3.0),
                ("Top", (0.05, 0.15, 1.35), 70.0, (0.95, 0.95, 0.97), 2.6),
            )
            camera.location = snout + Vector((0.42, -0.62, 0.16)) * scale
            camera.data.lens = 72.0
            _point_at(camera, snout)
            scene.view_settings.look = "None"
        else:
            scene.world.color = (0.012, 0.014, 0.018)
            lights = (
                ("CoolKey", (-0.95, -0.85, 0.90), 420.0, (0.72, 0.80, 0.92), 1.9),
                ("WarmFill", (0.85, -0.15, 0.30), 110.0, (0.55, 0.42, 0.32), 2.4),
                ("Rim", (0.05, 1.05, 0.55), 160.0, (0.70, 0.28, 0.16), 1.4),
            )
            camera.location = center + Vector((0.70, -1.05, 0.42)) * scale
            camera.data.lens = 55.0
            _point_at(camera, center + Vector((0.0, -0.08 * scale, 0.02 * scale)))
            scene.view_settings.look = "AgX - Medium High Contrast"
        for name, offset, energy, color, size in lights:
            data = bpy.data.lights.new(name, "AREA")
            data.energy = energy
            data.color = color
            data.shape = "DISK"
            data.size = size
            light = bpy.data.objects.new(name, data)
            scene.collection.objects.link(light)
            aim = snout if spec["lighting"] == "neutral" else center
            light.location = aim + Vector(offset) * scale
            _point_at(light, aim)
        output_path = repo_root / spec["path"]
        output_path.parent.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(output_path)
        bpy.ops.render.render(write_still=True)
        if not output_path.is_file() or output_path.stat().st_size < 80_000:
            raise RuntimeError(f"review render missing or too small: {output_path}")
        written.append(output_path)
        print(str(output_path))
    print("CINDERMAW_V005_REVIEW_COMPLETE")
    return written


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    args = parser.parse_args(argv)
    render_reviews(args.repo_root)
    return 0


if __name__ == "__main__":
    script_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else sys.argv[1:]
    raise SystemExit(main(script_args))
