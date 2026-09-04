#!/usr/bin/env python3
"""Blender-side unit tests for the realm-creature production-slice builder."""

from __future__ import annotations

import importlib.util
import math
import unittest
from pathlib import Path

import bpy


SCRIPT_PATH = Path(__file__).with_name("build_realm_creature_production_slice.py")
SPEC = importlib.util.spec_from_file_location("realm_creature_production_slice_builder", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Cannot import builder from {SCRIPT_PATH}")
BUILDER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BUILDER)


class RealmCreatureProductionSliceBuilderTests(unittest.TestCase):
    def setUp(self) -> None:
        bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        for action in list(bpy.data.actions):
            bpy.data.actions.remove(action, do_unlink=True)

    def test_action_endpoint_errors_measure_non_looping_pose_delta(self) -> None:
        self.assertTrue(hasattr(BUILDER, "action_endpoint_errors"))
        bpy.ops.object.armature_add()
        armature = bpy.context.object
        bone = armature.pose.bones[0]
        bone.rotation_mode = "QUATERNION"
        action = bpy.data.actions.new("endpoint_delta")
        armature.animation_data_create()
        armature.animation_data.action = action
        bone.location = (0.0, 0.0, 0.0)
        bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        bone.keyframe_insert("location", frame=1)
        bone.keyframe_insert("rotation_quaternion", frame=1)
        bone.location = (0.25, 0.0, 0.0)
        bone.rotation_quaternion = (math.cos(math.pi / 8), 0.0, 0.0, math.sin(math.pi / 8))
        bone.keyframe_insert("location", frame=10)
        bone.keyframe_insert("rotation_quaternion", frame=10)
        action.frame_start = 1
        action.frame_end = 10

        position_error, rotation_error = BUILDER.action_endpoint_errors(
            armature,
            action,
            [bone.name],
        )

        self.assertAlmostEqual(0.25, position_error, places=5)
        self.assertAlmostEqual(45.0, rotation_error, places=4)

    def test_try_rotate_skips_missing_bones(self) -> None:
        bpy.ops.object.armature_add()
        armature = bpy.context.object
        BUILDER.try_rotate(armature, "missing_bone", y=0.5)
        bone = armature.pose.bones[0]
        BUILDER.try_rotate(armature, bone.name, y=0.2)
        self.assertLess(bone.rotation_quaternion.w, 0.999)

    def test_triangle_vertex_order_check_rejects_reordered_indices(self) -> None:
        self.assertTrue(hasattr(BUILDER, "triangle_vertex_order_preserved"))
        self.assertTrue(
            BUILDER.triangle_vertex_order_preserved((1, 2, 3), (1, 2, 3))
        )
        self.assertFalse(
            BUILDER.triangle_vertex_order_preserved((1, 2, 3), (1, 3, 2))
        )


if __name__ == "__main__":
    unittest.main(argv=[__file__])
