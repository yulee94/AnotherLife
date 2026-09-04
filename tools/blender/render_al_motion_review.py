#!/usr/bin/env python3
"""Render deterministic five-phase contact sheets for motion-library review."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

import bpy
from mathutils import Vector


def _arguments() -> argparse.Namespace:
    values = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--source-plan", type=Path, required=True)
    parser.add_argument("--representative", required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    return parser.parse_args(values)


def _load(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _world_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [
        obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box
    ]
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def _camera_for(meshes: list[bpy.types.Object]) -> bpy.types.Object:
    minimum, maximum = _world_bounds(meshes)
    center = (minimum + maximum) * 0.5
    extent = max((maximum - minimum).length, 0.5)
    camera_data = bpy.data.cameras.new("AL_Motion_Review_Camera")
    camera = bpy.data.objects.new("AL_Motion_Review_Camera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = center + Vector((extent * 1.25, -extent * 2.1, extent * 0.55))
    camera.rotation_euler = (
        (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    )
    camera_data.lens = 60.0
    bpy.context.scene.camera = camera
    return camera


def main() -> int:
    args = _arguments()
    repo_root = args.repo_root.resolve()
    source_plan = _load(args.source_plan.resolve())
    representative = next(
        row
        for row in source_plan["representatives"]
        if row["representativeProfileId"] == args.representative
    )
    blend_path = repo_root / representative["outputBlendPath"]
    sidecar_path = repo_root / representative["sidecarPath"]
    sidecar = _load(sidecar_path)
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    armature = bpy.data.objects[representative["armatureObject"]]
    meshes = [
        obj
        for obj in bpy.data.objects
        if obj.type == "MESH"
        and any(
            modifier.type == "ARMATURE" and modifier.object == armature
            for modifier in obj.modifiers
        )
    ]
    if not meshes:
        raise RuntimeError("MissingSkinnedMeshes")
    _camera_for(meshes)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 320
    scene.render.resolution_y = 320
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.display.shading.light = "STUDIO"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.display.shading.color_type = "MATERIAL"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("AL_Motion_Review_World")
    scene.world.color = (0.025, 0.025, 0.035)

    desired = (
        ["idle.neutral", "locomotion.walk", "attack.basic", "reaction.knockdown"]
        if "champion" in args.representative
        else ["idle.neutral", "locomotion.walk", "daily.work", "attack.basic"]
        if "npc" in args.representative
        else [
            "idle.neutral",
            "locomotion.walk",
            "interaction.cut",
            "reaction.spoil_push",
        ]
    )
    actions_by_key = {row["motionKey"]: row for row in sidecar["actions"]}
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    rendered = []
    if armature.animation_data is None:
        armature.animation_data_create()
    for motion_key in desired:
        row = actions_by_key[motion_key]
        action = bpy.data.actions[row["actionName"]]
        armature.animation_data.action = action
        last = row["frameCount"]
        frames = sorted(
            {
                1,
                1 + round((last - 1) * 0.25),
                1 + round((last - 1) * 0.5),
                1 + round((last - 1) * 0.75),
                last,
            }
        )
        for index, frame in enumerate(frames):
            scene.frame_set(frame)
            output = output_dir / (
                f"{representative['assetId']}__{motion_key.replace('.', '_')}__{index}.png"
            )
            scene.render.filepath = str(output)
            bpy.ops.render.render(write_still=True)
            rendered.append(str(output))
    print(json.dumps({"status": "passed", "rendered": rendered}, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
