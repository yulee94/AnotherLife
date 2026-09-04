#!/usr/bin/env python3
"""Render Cindermaw's v004 smoothed authored-normal review candidate."""
from __future__ import annotations

import argparse
from pathlib import Path
import sys
from typing import Any, Sequence


MODEL = "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/Models/elite_umbral_cindermaw_salamander/elite_umbral_cindermaw_salamander_source_v004.fbx"
TEXTURE_ROOT = "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/Textures/elite_umbral_cindermaw_salamander/retexture_uvclean_normaldetail_v004"
OUTPUT = "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/Review/elite_umbral_cindermaw_salamander_threequarter_v004.png"


def review_spec() -> dict[str, Any]:
    return {
        "modelPath": MODEL,
        "textureRoot": TEXTURE_ROOT,
        "outputPath": OUTPUT,
        "reviewTextureLimit": 2048,
        "normalStrength": 1.0,
        "runtimeVfxIncluded": False,
    }


def _load_image(bpy: Any, path: Path, *, non_color: bool, limit: int = 2048) -> Any:
    image = bpy.data.images.load(str(path))
    if image.size[0] > limit or image.size[1] > limit:
        image.scale(limit, limit)
    if non_color:
        image.colorspace_settings.name = "Non-Color"
    return image


def render_review(repo_root: Path) -> Path:
    import bpy
    from mathutils import Vector

    spec = review_spec()
    model_path = repo_root / spec["modelPath"]
    texture_root = repo_root / spec["textureRoot"]
    output_path = repo_root / spec["outputPath"]
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

    material = bpy.data.materials.new("Cindermaw_NormalDetail_V004")
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
    multiply.inputs[0].default_value = 0.35
    roughness = nodes.new("ShaderNodeTexImage")
    roughness.image = _load_image(bpy, texture_root / "roughness.png", non_color=True)
    metallic = nodes.new("ShaderNodeTexImage")
    metallic.image = _load_image(bpy, texture_root / "metallic.png", non_color=True)
    normal_texture = nodes.new("ShaderNodeTexImage")
    normal_texture.image = _load_image(bpy, texture_root / "normal.png", non_color=True)
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = spec["normalStrength"]
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

    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector((min(item.x for item in corners), min(item.y for item in corners), min(item.z for item in corners)))
    maximum = Vector((max(item.x for item in corners), max(item.y for item in corners), max(item.z for item in corners)))
    center = (minimum + maximum) * 0.5
    extent = maximum - minimum
    scale = max(extent)

    def point_at(item: Any, target: Vector) -> None:
        item.rotation_euler = (target - item.location).to_track_quat("-Z", "Y").to_euler()

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new("CindermawReviewWorld")
    scene.world.color = (0.006, 0.008, 0.014)
    for name, offset, energy, color, size in (
        ("CoolKey", (-1.10, -0.75, 1.00), 650.0, (0.64, 0.76, 1.0), 1.8),
        ("BlueFill", (0.90, -0.20, 0.45), 220.0, (0.22, 0.34, 0.70), 2.2),
        ("EmberRim", (0.10, 1.10, 0.75), 450.0, (1.0, 0.10, 0.025), 1.2),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = center + Vector(offset) * scale
        point_at(light, center)

    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.location = center + Vector((0.67, -0.92, 0.38)) * scale
    camera.data.lens = 62.0
    point_at(camera, center + Vector((0.0, -0.12 * scale, 0.0)))
    scene.view_settings.look = "AgX - Medium High Contrast"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)
    if not output_path.is_file() or output_path.stat().st_size < 100_000:
        raise RuntimeError("review render was not written or is suspiciously small")
    print(str(output_path))
    return output_path


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    args = parser.parse_args(argv)
    render_review(args.repo_root)
    return 0


if __name__ == "__main__":
    script_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else sys.argv[1:]
    raise SystemExit(main(script_args))
