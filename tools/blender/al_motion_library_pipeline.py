#!/usr/bin/env python3
"""Author deterministic representative motion actions inside Blender 5.2."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path
from typing import Any

import bpy
from mathutils import Euler, Matrix, Quaternion, Vector

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from al_motion_library_contract import (
    expected_binding_keys,
    load_json,
    resolve_motion_rule,
    sha256_file,
    stable_clip_id,
    stable_json_bytes,
)
from al_rig_cleanup_pipeline import (
    action_paths,
    bone_records,
    configure_deterministic_fbx_export,
)


class MotionLibraryBuildError(RuntimeError):
    """Raised when motion authoring or validation cannot complete safely."""


def _arguments() -> argparse.Namespace:
    values = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--source-plan", type=Path, required=True)
    parser.add_argument("--representative", required=True)
    parser.add_argument("--output-root", type=Path)
    return parser.parse_args(values)


def _repo_path(repo_root: Path, relative: str) -> Path:
    result = (repo_root / relative).resolve()
    try:
        result.relative_to(repo_root.resolve())
    except ValueError as error:
        raise MotionLibraryBuildError(f"PathEscapesRepository: {relative}") from error
    return result


def _output_path(
    repo_root: Path,
    output_root: Path | None,
    relative: str,
) -> Path:
    if output_root is None:
        return _repo_path(repo_root, relative)
    result = (output_root.resolve() / relative).resolve()
    try:
        result.relative_to(output_root.resolve())
    except ValueError as error:
        raise MotionLibraryBuildError(f"PathEscapesOutputRoot: {relative}") from error
    return result


def _round(value: float) -> float:
    result = round(float(value), 7)
    return 0.0 if result == -0.0 else result


def _quat_angle_degrees(first: Quaternion, second: Quaternion) -> float:
    first = first.normalized()
    second = second.normalized()
    dot = max(-1.0, min(1.0, abs(first.dot(second))))
    return math.degrees(2.0 * math.acos(dot))


def _matrix_pose(matrix: Matrix) -> tuple[Vector, Quaternion]:
    location, rotation, _scale = matrix.decompose()
    return location, rotation


def _phase_hash(motion_key: str) -> float:
    digest = hashlib.sha256(motion_key.encode("utf-8")).digest()
    return (int.from_bytes(digest[:2], "big") / 65535.0 - 0.5) * 0.3


def _available(armature: bpy.types.Object, names: list[str]) -> list[str]:
    return [name for name in names if armature.pose.bones.get(name) is not None]


def _humanoid_controls(armature: bpy.types.Object) -> dict[str, list[str]]:
    return {
        "root": _available(armature, ["motion_root"]),
        "body": _available(
            armature, ["pelvis", "spine_01", "spine_02", "chest", "neck", "head"]
        ),
        "arms_l": _available(
            armature, ["clavicle_l", "upper_arm_l", "lower_arm_l", "hand_l"]
        ),
        "arms_r": _available(
            armature, ["clavicle_r", "upper_arm_r", "lower_arm_r", "hand_r"]
        ),
        "legs_l": _available(
            armature, ["upper_leg_l", "lower_leg_l", "foot_l", "toe_l"]
        ),
        "legs_r": _available(
            armature, ["upper_leg_r", "lower_leg_r", "foot_r", "toe_r"]
        ),
    }


def _beast_controls(armature: bpy.types.Object) -> dict[str, list[str]]:
    return {
        "root": _available(armature, ["motion_root"]),
        "body": _available(
            armature,
            [
                "body_root",
                "spine_01",
                "spine_02",
                "spine_03",
                "spine_04",
                "neck_01",
                "head",
                "tail_01",
            ],
        ),
        "arms_l": _available(
            armature,
            [
                "limb_front_01_l",
                "limb_front_02_l",
                "limb_front_03_l",
                "contact_front_l",
            ],
        ),
        "arms_r": _available(
            armature,
            [
                "limb_front_01_r",
                "limb_front_02_r",
                "limb_front_03_r",
                "contact_front_r",
            ],
        ),
        "legs_l": _available(
            armature,
            ["limb_rear_01_l", "limb_rear_02_l", "limb_rear_03_l", "contact_rear_l"],
        ),
        "legs_r": _available(
            armature,
            ["limb_rear_01_r", "limb_rear_02_r", "limb_rear_03_r", "contact_rear_r"],
        ),
    }


def _all_controls(groups: dict[str, list[str]]) -> list[str]:
    return sorted({name for values in groups.values() for name in values})


def _set_rotation(
    armature: bpy.types.Object,
    bone_names: list[str],
    x: float = 0.0,
    y: float = 0.0,
    z: float = 0.0,
    scale: float = 1.0,
) -> None:
    for index, bone_name in enumerate(bone_names):
        falloff = scale / (1.0 + index * 0.35)
        pose_bone = armature.pose.bones[bone_name]
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.rotation_quaternion = Euler(
            (x * falloff, y * falloff, z * falloff), "XYZ"
        ).to_quaternion()


def _reset_pose(armature: bpy.types.Object, controls: list[str]) -> None:
    for bone_name in controls:
        pose_bone = armature.pose.bones[bone_name]
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.location = Vector((0.0, 0.0, 0.0))
        pose_bone.rotation_quaternion = Quaternion((1.0, 0.0, 0.0, 0.0))
        pose_bone.scale = Vector((1.0, 1.0, 1.0))


def _apply_motion_pose(
    armature: bpy.types.Object,
    groups: dict[str, list[str]],
    style: str,
    motion_key: str,
    normalized: float,
) -> None:
    controls = _all_controls(groups)
    _reset_pose(armature, controls)
    phase = normalized * math.tau
    variant = _phase_hash(motion_key)
    root_name = groups["root"][0] if groups["root"] else None
    body_root = groups["body"][0] if groups["body"] else None

    if style == "locomotion_cycle":
        stride = math.sin(phase)
        lift = max(0.0, math.sin(phase))
        amplitude = 0.25 + variant * 0.15
        if motion_key.endswith("walk"):
            amplitude *= 0.7
        elif motion_key.endswith("sprint"):
            amplitude *= 1.25
        elif "strafe" in motion_key:
            amplitude *= 0.8
        _set_rotation(armature, groups["legs_l"][:2], x=amplitude * stride)
        _set_rotation(armature, groups["legs_r"][:2], x=-amplitude * stride)
        _set_rotation(armature, groups["arms_l"][:3], x=-amplitude * stride * 0.8)
        _set_rotation(armature, groups["arms_r"][:3], x=amplitude * stride * 0.8)
        if len(groups["legs_l"]) > 1:
            _set_rotation(armature, groups["legs_l"][1:2], x=0.18 * lift)
            _set_rotation(armature, groups["legs_r"][1:2], x=0.18 * max(0.0, -stride))
        if body_root:
            armature.pose.bones[body_root].location.z = 0.004 * (
                1.0 - math.cos(phase * 2.0)
            )
            armature.pose.bones[body_root].rotation_quaternion = Euler(
                (0.0, 0.025 * math.sin(phase), 0.0), "XYZ"
            ).to_quaternion()
    elif style in {"idle_cycle", "channel_cycle"}:
        breath = math.sin(phase)
        amount = 0.035 if style == "idle_cycle" else 0.09
        _set_rotation(armature, groups["body"][1:5], x=amount * breath)
        _set_rotation(armature, groups["arms_l"], y=amount * 0.7 * breath)
        _set_rotation(armature, groups["arms_r"], y=-amount * 0.7 * breath)
    elif style == "attack_action":
        envelope = math.sin(math.pi * normalized)
        follow = math.sin(math.pi * min(1.0, normalized * 1.35))
        direction = -1.0 if hashlib.sha256(motion_key.encode()).digest()[0] & 1 else 1.0
        _set_rotation(
            armature,
            groups["body"][1:5],
            y=0.24 * envelope,
            z=0.18 * direction * envelope,
        )
        _set_rotation(
            armature, groups["arms_l"], x=-0.5 * follow, y=0.2 * direction * envelope
        )
        _set_rotation(
            armature, groups["arms_r"], x=-0.6 * follow, y=-0.2 * direction * envelope
        )
        _set_rotation(armature, groups["legs_l"][:2], x=0.18 * envelope)
        _set_rotation(armature, groups["legs_r"][:2], x=-0.12 * envelope)
    elif style in {"collapse_action", "rise_action"}:
        progress = normalized if style == "collapse_action" else 1.0 - normalized
        eased = progress * progress * (3.0 - 2.0 * progress)
        _set_rotation(armature, groups["body"], x=0.85 * eased, z=0.18 * eased)
        _set_rotation(armature, groups["arms_l"], x=-0.5 * eased, z=0.3 * eased)
        _set_rotation(armature, groups["arms_r"], x=-0.5 * eased, z=-0.3 * eased)
        _set_rotation(armature, groups["legs_l"][:2], x=0.45 * eased)
        _set_rotation(armature, groups["legs_r"][:2], x=0.3 * eased)
        if body_root:
            armature.pose.bones[body_root].location.z = -0.012 * eased
    elif style == "vertical_action":
        arc = math.sin(math.pi * normalized)
        _set_rotation(armature, groups["body"][1:4], x=-0.18 * arc)
        _set_rotation(armature, groups["legs_l"][:2], x=0.35 * arc)
        _set_rotation(armature, groups["legs_r"][:2], x=0.35 * arc)
        if root_name:
            if motion_key.endswith("jump"):
                armature.pose.bones[root_name].location.z = 0.12 * arc
            elif motion_key.endswith("fall"):
                armature.pose.bones[root_name].location.z = -0.04 + 0.012 * math.sin(
                    phase
                )
            else:
                armature.pose.bones[root_name].location.z = 0.025 * arc
    else:
        envelope = math.sin(math.pi * normalized)
        _set_rotation(
            armature, groups["body"][1:5], y=0.12 * envelope, z=variant * envelope
        )
        _set_rotation(armature, groups["arms_l"], x=-0.28 * envelope, z=0.16 * envelope)
        _set_rotation(
            armature, groups["arms_r"], x=-0.24 * envelope, z=-0.16 * envelope
        )


def _insert_pose_keys(
    armature: bpy.types.Object,
    controls: list[str],
    frame: int,
) -> None:
    for bone_name in controls:
        pose_bone = armature.pose.bones[bone_name]
        pose_bone.keyframe_insert(data_path="location", frame=frame, group=bone_name)
        pose_bone.keyframe_insert(
            data_path="rotation_quaternion", frame=frame, group=bone_name
        )
        pose_bone.keyframe_insert(data_path="scale", frame=frame, group=bone_name)


def _events_for_clip(
    source_plan: dict[str, Any],
    required_manifest: dict[str, Any],
    representative: dict[str, Any],
    motion_key: str,
    rule: dict[str, Any],
    frame_count: int,
) -> list[dict[str, Any]]:
    templates = source_plan["eventTemplates"]
    events = [dict(row) for row in templates[rule["eventTemplate"]]]
    required_row = next(
        row for row in required_manifest["motionKeys"] if row["key"] == motion_key
    )
    defaults = {
        "al.motion.audio.request": 0.45,
        "al.motion.contact.begin": 0.2,
        "al.motion.contact.end": 0.3,
        "al.motion.hitbox.request_begin": 0.36,
        "al.motion.hitbox.request_end": 0.62,
        "al.motion.interruptible.begin": 0.72,
        "al.motion.interruptible.end": 0.95,
        "al.motion.phase.enter": 0.0,
        "al.motion.phase.exit": 1.0,
        "al.motion.vfx.request": 0.45,
    }
    existing = {event["eventName"] for event in events}
    for event_name in required_row["requiredEventNames"]:
        if event_name not in existing:
            events.append(
                {
                    "eventName": event_name,
                    "normalizedTime": defaults.get(event_name, 0.5),
                }
            )
    events.sort(key=lambda row: (row["normalizedTime"], row["eventName"]))
    contact_bones = representative["contactBones"]
    result = []
    for ordinal, event in enumerate(events):
        row = dict(event)
        normalized = float(row.pop("normalizedTime"))
        row["frame"] = 1 + round(normalized * (frame_count - 1))
        row["normalizedTime"] = _round(normalized)
        row["eventOrdinal"] = ordinal
        if "contactIndex" in row:
            row["contactId"] = contact_bones[
                int(row.pop("contactIndex")) % len(contact_bones)
            ]
        row.setdefault("phase", motion_key)
        result.append(row)
    return result


def _hitbox_windows(events: list[dict[str, Any]]) -> list[dict[str, Any]]:
    opened: dict[str, int] = {}
    result = []
    for event in events:
        if event["eventName"] == "al.motion.hitbox.request_begin":
            opened[event.get("windowId", "primary")] = event["frame"]
        elif event["eventName"] == "al.motion.hitbox.request_end":
            window_id = event.get("windowId", "primary")
            if window_id in opened:
                result.append(
                    {
                        "windowId": window_id,
                        "openFrame": opened.pop(window_id),
                        "closeFrame": event["frame"],
                    }
                )
    if opened:
        raise MotionLibraryBuildError(f"UnclosedHitboxWindows: {sorted(opened)}")
    return result


def _action_curve_signature(action: bpy.types.Action) -> str:
    rows = []
    curves = {
        (curve.data_path, curve.array_index): curve for curve in action_paths(action)
    }
    for (data_path, array_index), curve in sorted(curves.items()):
        rows.append(
            {
                "arrayIndex": array_index,
                "dataPath": data_path,
                "keys": [
                    [_round(point.co.x), _round(point.co.y), point.interpolation]
                    for point in curve.keyframe_points
                ],
            }
        )
    return hashlib.sha256(stable_json_bytes(rows)).hexdigest()


def _sample_world_pose(
    armature: bpy.types.Object,
    action: bpy.types.Action,
    bone_names: list[str],
    frame: int,
) -> dict[str, tuple[Vector, Quaternion]]:
    armature.animation_data.action = action
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    return {
        name: _matrix_pose(armature.matrix_world @ armature.pose.bones[name].matrix)
        for name in bone_names
    }


def _measured_cleanup(
    armature: bpy.types.Object,
    action: bpy.types.Action,
    controls: list[str],
    contact_bones: list[str],
    events: list[dict[str, Any]],
    authored_samples: dict[int, dict[str, tuple[Vector, Quaternion]]],
    frame_count: int,
    loop: bool,
    thresholds: dict[str, float],
) -> dict[str, Any]:
    sampled = authored_samples
    finite = all(
        math.isfinite(component)
        for pose in sampled.values()
        for location, rotation in pose.values()
        for component in (*location, *rotation)
    )
    first = sampled[1]
    last = sampled[frame_count]
    loop_position = max((first[name][0] - last[name][0]).length for name in controls)
    loop_rotation = max(
        _quat_angle_degrees(first[name][1], last[name][1]) for name in controls
    )
    root_name = "motion_root" if "motion_root" in controls else controls[0]
    transition_position = 0.0
    transition_rotation = 0.0
    for frame in range(1, frame_count):
        transition_position = max(
            transition_position,
            (sampled[frame][root_name][0] - sampled[frame + 1][root_name][0]).length,
        )
        for name in controls:
            transition_rotation = max(
                transition_rotation,
                _quat_angle_degrees(
                    sampled[frame][name][1], sampled[frame + 1][name][1]
                ),
            )
    contact_drift = 0.0
    begins: dict[str, int] = {}
    contact_frames = {
        event["frame"] for event in events if event.get("contactId") in contact_bones
    }
    world_contacts = {
        frame: _sample_world_pose(armature, action, contact_bones, frame)
        for frame in contact_frames
    }
    for event in events:
        contact_id = event.get("contactId")
        if not contact_id or contact_id not in contact_bones:
            continue
        if event["eventName"] == "al.motion.contact.begin":
            begins[contact_id] = event["frame"]
        elif event["eventName"] == "al.motion.contact.end" and contact_id in begins:
            contact_drift = max(
                contact_drift,
                (
                    world_contacts[begins.pop(contact_id)][contact_id][0]
                    - world_contacts[event["frame"]][contact_id][0]
                ).length,
            )
    result = {
        "loopPositionErrorMeters": _round(loop_position if loop else 0.0),
        "loopRotationErrorDegrees": _round(loop_rotation if loop else 0.0),
        "contactDriftMeters": _round(contact_drift),
        "transitionPositionDeltaMeters": _round(transition_position),
        "transitionRotationDeltaDegrees": _round(transition_rotation),
        "poseContinuity": "closed" if loop else "declared_transition",
        "finiteTransforms": finite,
    }
    comparisons = (
        ("loopPositionErrorMeters", "maximumLoopPositionErrorMeters"),
        ("loopRotationErrorDegrees", "maximumLoopRotationErrorDegrees"),
        ("contactDriftMeters", "maximumContactDriftMeters"),
        ("transitionPositionDeltaMeters", "maximumTransitionPositionDeltaMeters"),
        ("transitionRotationDeltaDegrees", "maximumTransitionRotationDeltaDegrees"),
    )
    exceeded = [
        field for field, maximum in comparisons if result[field] > thresholds[maximum]
    ]
    if exceeded or not finite:
        raise MotionLibraryBuildError(
            f"CleanupMetricsFailed: {action.name}: exceeded={exceeded} metrics={result}"
        )
    return result


def _author_actions(
    source_plan: dict[str, Any],
    required_manifest: dict[str, Any],
    representative: dict[str, Any],
    armature: bpy.types.Object,
) -> list[dict[str, Any]]:
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = None
    for existing in list(bpy.data.actions):
        bpy.data.actions.remove(existing, do_unlink=True)
    expected = expected_binding_keys(source_plan, required_manifest)
    keys = set(expected[representative["representativeProfileId"]])
    if representative["catalogSource"]:
        keys.update(
            motion_key
            for row in source_plan["representatives"]
            if row["skeletonProfileId"] == representative["skeletonProfileId"]
            for motion_key in expected[row["representativeProfileId"]]
        )
    keys = sorted(keys, key=lambda value: value.encode("utf-8"))
    groups = (
        _beast_controls(armature)
        if representative["representativeProfileId"].endswith("slagwhistle_v001")
        else _humanoid_controls(armature)
    )
    controls = _all_controls(groups)
    missing_contacts = sorted(set(representative["contactBones"]) - set(controls))
    if missing_contacts:
        raise MotionLibraryBuildError(f"MissingContactBones: {missing_contacts}")
    if not groups["root"] or not groups["body"] or len(controls) < 8:
        raise MotionLibraryBuildError(f"InsufficientMotionControls: {controls}")

    motion_definitions = {row["key"]: row for row in required_manifest["motionKeys"]}
    action_rows = []
    for motion_key in keys:
        rule = resolve_motion_rule(source_plan, motion_key)
        loop_policy = motion_definitions[motion_key]["loopPolicy"]
        if loop_policy == "must_loop":
            loop = True
        elif loop_policy == "must_not_loop":
            loop = False
        else:
            loop = bool(rule["loop"])
        frame_count = int(rule["durationFrames"]) + 1
        clip_id = stable_clip_id(representative["representativeProfileId"], motion_key)
        action_name = f"ANIM_{clip_id}"
        action = bpy.data.actions.new(name=action_name)
        action.use_fake_user = True
        armature.animation_data.action = action
        authored_samples: dict[int, dict[str, tuple[Vector, Quaternion]]] = {}
        for frame in range(1, frame_count + 1):
            normalized = (frame - 1) / (frame_count - 1)
            _apply_motion_pose(
                armature,
                groups,
                rule["style"],
                motion_key,
                normalized,
            )
            authored_samples[frame] = {
                name: (
                    armature.pose.bones[name].location.copy(),
                    armature.pose.bones[name].rotation_quaternion.copy(),
                )
                for name in controls
            }
            _insert_pose_keys(armature, controls, frame)
        for curve in action_paths(action):
            for point in curve.keyframe_points:
                point.interpolation = "LINEAR"
        action["al_clip_id"] = clip_id
        action["al_motion_key"] = motion_key
        action["al_loop"] = loop
        action["al_root_treatment"] = rule["rootTreatment"]
        action["al_sample_rate_hz"] = int(source_plan["sampleRateHz"])
        action.frame_start = 1
        action.frame_end = frame_count
        events = _events_for_clip(
            source_plan,
            required_manifest,
            representative,
            motion_key,
            rule,
            frame_count,
        )
        action_rows.append(
            {
                "clipId": clip_id,
                "motionKey": motion_key,
                "actionName": action_name,
                "frameCount": frame_count,
                "sampleRateHz": source_plan["sampleRateHz"],
                "durationSeconds": _round(
                    (frame_count - 1) / source_plan["sampleRateHz"]
                ),
                "loop": loop,
                "rootTreatment": rule["rootTreatment"],
                "generatorRuleId": rule["id"],
                "generatorStyle": rule["style"],
                "events": events,
                "hitboxWindows": _hitbox_windows(events),
                "clipSignature": _action_curve_signature(action),
                "measuredCleanup": _measured_cleanup(
                    armature,
                    action,
                    controls,
                    representative["contactBones"],
                    events,
                    authored_samples,
                    frame_count,
                    loop,
                    source_plan["cleanupThresholds"],
                ),
            }
        )
    armature.animation_data.action = None
    _reset_pose(armature, controls)
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = max(row["frameCount"] for row in action_rows)
    bpy.context.scene.frame_set(1)
    return action_rows


def _skeleton_signature(armature: bpy.types.Object) -> str:
    rows = bone_records(armature)
    return hashlib.sha256(stable_json_bytes(rows)).hexdigest()


def _select(objects: list[bpy.types.Object], active: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active


def _export_fbx(
    output: Path,
    armature: bpy.types.Object,
    meshes: list[bpy.types.Object],
    export_preset: dict[str, Any],
) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    _select([armature, *meshes], armature)
    configure_deterministic_fbx_export()
    result = bpy.ops.export_scene.fbx(
        filepath=str(output),
        check_existing=False,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        global_scale=export_preset["globalScale"],
        apply_unit_scale=export_preset["applyUnitScale"],
        apply_scale_options=export_preset["applyScaleOptions"],
        use_space_transform=export_preset["useSpaceTransform"],
        bake_space_transform=export_preset["bakeSpaceTransform"],
        axis_forward=export_preset["axisForward"],
        axis_up=export_preset["axisUp"],
        use_mesh_modifiers=export_preset["useMeshModifiers"],
        mesh_smooth_type=export_preset["meshSmoothing"],
        use_triangles=export_preset["useTriangulateFaces"],
        add_leaf_bones=export_preset["addLeafBones"],
        primary_bone_axis=export_preset["primaryBoneAxis"],
        secondary_bone_axis=export_preset["secondaryBoneAxis"],
        armature_nodetype=export_preset["armatureNodeType"],
        use_armature_deform_only=export_preset["useArmatureDeformOnly"],
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode=export_preset["pathMode"],
        embed_textures=export_preset["embedTextures"],
    )
    if "FINISHED" not in result or not output.is_file():
        raise MotionLibraryBuildError(f"FbxExportFailed: {output}")


def _roundtrip_fbx(
    output: Path,
    expected_bones: set[str],
    expected_action_names: set[str],
) -> dict[str, Any]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.wm.fbx_import(filepath=str(output), use_anim=True)
    issues = []
    if "FINISHED" not in result:
        issues.append("FbxRoundTripImportFailed")
    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        issues.append(f"FbxRoundTripArmatureCount:{len(armatures)}")
        actual_bones: set[str] = set()
    else:
        actual_bones = {bone.name for bone in armatures[0].data.bones}
        if actual_bones != expected_bones:
            issues.append("FbxRoundTripBoneMismatch")
    actual_actions = {action.name for action in bpy.data.actions}
    missing_actions = sorted(
        expected
        for expected in expected_action_names
        if not any(
            actual == expected or actual.endswith((f"|{expected}", expected))
            for actual in actual_actions
        )
    )
    if missing_actions:
        issues.append(
            f"FbxRoundTripMissingActions:{missing_actions}:actual={sorted(actual_actions)}"
        )
    if issues:
        raise MotionLibraryBuildError(issues)
    return {
        "status": "passed",
        "armatureCount": len(armatures),
        "boneCount": len(actual_bones),
        "expectedActionCount": len(expected_action_names),
        "importedActionCount": len(actual_actions),
        "missingActionCount": 0,
    }


def main() -> int:
    args = _arguments()
    repo_root = args.repo_root.resolve()
    source_plan_path = args.source_plan.resolve()
    source_plan = load_json(source_plan_path)
    required_manifest = load_json(
        _repo_path(repo_root, source_plan["requiredManifestPath"])
    )
    rig_manifest = load_json(_repo_path(repo_root, source_plan["rigManifestPath"]))
    representative = next(
        row
        for row in source_plan["representatives"]
        if row["representativeProfileId"] == args.representative
    )
    source_blend = _repo_path(repo_root, representative["sourceBlendPath"])
    bpy.ops.wm.open_mainfile(filepath=str(source_blend))
    armature = bpy.data.objects.get(representative["armatureObject"])
    if armature is None or armature.type != "ARMATURE":
        raise MotionLibraryBuildError(
            f"MissingArmature: {representative['armatureObject']}"
        )
    meshes = sorted(
        [
            obj
            for obj in bpy.data.objects
            if obj.type == "MESH"
            and any(
                modifier.type == "ARMATURE" and modifier.object == armature
                for modifier in obj.modifiers
            )
        ],
        key=lambda obj: obj.name.encode("utf-8"),
    )
    if not meshes:
        raise MotionLibraryBuildError("MissingSkinnedMeshes")

    actions = _author_actions(source_plan, required_manifest, representative, armature)
    expected_bones = {bone.name for bone in armature.data.bones}
    expected_action_names = {row["actionName"] for row in actions}
    skeleton_hash = _skeleton_signature(armature)
    semantic_signature = hashlib.sha256(
        stable_json_bytes(
            {
                "assetId": representative["assetId"],
                "skeletonSignature": skeleton_hash,
                "actions": actions,
            }
        )
    ).hexdigest()

    output_blend = _output_path(
        repo_root, args.output_root, representative["outputBlendPath"]
    )
    output_fbx = _output_path(
        repo_root, args.output_root, representative["outputFbxPath"]
    )
    output_sidecar = _output_path(
        repo_root, args.output_root, representative["sidecarPath"]
    )
    for path in (output_blend, output_fbx, output_sidecar):
        path.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene["al_motion_library_id"] = source_plan["libraryId"]
    bpy.context.scene["al_motion_asset_id"] = representative["assetId"]
    bpy.context.scene["al_motion_semantic_signature"] = semantic_signature
    bpy.ops.wm.save_as_mainfile(filepath=str(output_blend), check_existing=False)
    export_preset = rig_manifest["exportPreset"]
    _export_fbx(output_fbx, armature, meshes, export_preset)
    fbx_hash = sha256_file(output_fbx)
    blend_hash = sha256_file(output_blend)
    roundtrip = _roundtrip_fbx(output_fbx, expected_bones, expected_action_names)

    sidecar = {
        "schemaVersion": 1,
        "libraryId": source_plan["libraryId"],
        "assetId": representative["assetId"],
        "representativeProfileId": representative["representativeProfileId"],
        "skeletonProfileId": representative["skeletonProfileId"],
        "authorityState": source_plan["authorityState"],
        "sourceBlendPath": representative["sourceBlendPath"],
        "blendPath": representative["outputBlendPath"],
        "fbxPath": representative["outputFbxPath"],
        "sourcePlanSha256": sha256_file(source_plan_path),
        "sourceBlendSha256": sha256_file(source_blend),
        "blendSha256": blend_hash,
        "fbxSha256": fbx_hash,
        "skeletonSignature": skeleton_hash,
        "actionSignature": hashlib.sha256(
            stable_json_bytes(
                [
                    {
                        "actionName": row["actionName"],
                        "clipSignature": row["clipSignature"],
                    }
                    for row in actions
                ]
            )
        ).hexdigest(),
        "semanticSignature": semantic_signature,
        "sampleRateHz": source_plan["sampleRateHz"],
        "contactBones": representative["contactBones"],
        "licensingEvidence": representative["licensingEvidence"],
        "sourceRightsState": representative["sourceRightsState"],
        "knownRestrictions": representative["knownRestrictions"],
        "cleanupThresholds": source_plan["cleanupThresholds"],
        "actions": actions,
        "roundTrip": roundtrip,
    }
    output_sidecar.write_text(
        json.dumps(sidecar, indent=2, sort_keys=False) + "\n", encoding="utf-8"
    )
    print(
        json.dumps(
            {
                "status": "passed",
                "assetId": representative["assetId"],
                "actions": len(actions),
                "semanticSignature": semantic_signature,
                "blend": str(output_blend),
                "fbx": str(output_fbx),
                "sidecar": str(output_sidecar),
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
