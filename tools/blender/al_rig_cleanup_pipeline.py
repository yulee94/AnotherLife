#!/usr/bin/env python3
"""Deterministic, fail-closed Blender rig cleanup and FBX export pipeline.

Run with Blender 5.2 or later, for example:
  blender --background --python tools/blender/al_rig_cleanup_pipeline.py -- build --asset <id>
"""

from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import math
import re
import sys
from pathlib import Path
from typing import Any

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

sys.dont_write_bytecode = True
SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from al_rig_pipeline_contract import (
    SIGNATURE_ALGORITHM,
    cleaned_content_signature,
    skeleton_signature,
)

PIPELINE_ID = "rmc_pipeline_blender_rig_cleanup_v001"
BONE_NAME_PATTERN = re.compile(r"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
IDENTITY = Matrix.Identity(4)
EPSILON = 1e-6
WEIGHT_FLOOR = 0.001
WEIGHT_TOLERANCE = 0.0001
MAX_INFLUENCES = 4


class PipelineError(RuntimeError):
    """A deterministic pipeline contract violation."""

    def __init__(self, issues: list[str] | str):
        self.issues = [issues] if isinstance(issues, str) else sorted(set(issues))
        super().__init__("\n".join(self.issues))


def stable_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=True, indent=2, sort_keys=True) + "\n"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(stable_json(value), encoding="utf-8", newline="\n")


def repo_path(repo_root: Path, relative: str) -> Path:
    path = (repo_root / relative).resolve()
    try:
        path.relative_to(repo_root.resolve())
    except ValueError as error:
        raise PipelineError(f"PathEscapesRepository: {relative}") from error
    return path


def load_context(
    repo_root: Path, manifest_relative: str, asset_id: str
) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any], dict[str, Any]]:
    manifest = load_json(repo_path(repo_root, manifest_relative))
    if manifest.get("schemaVersion") != 1 or manifest.get("pipelineId") != PIPELINE_ID:
        raise PipelineError("ManifestIdentityMismatch")
    if tuple(bpy.app.version) < tuple(manifest["minimumBlenderVersion"]):
        raise PipelineError(
            f"BlenderVersionTooOld: {bpy.app.version_string} < {manifest['minimumBlenderVersion']}"
        )
    matches = [asset for asset in manifest["assets"] if asset["id"] == asset_id]
    if len(matches) != 1:
        raise PipelineError(f"AssetIdentityNotUnique: {asset_id}")
    standard = load_json(repo_path(repo_root, manifest["standardPath"]))
    provenance = load_json(repo_path(repo_root, manifest["provenancePath"]))
    return manifest, matches[0], standard, provenance


def find_by_id(rows: list[dict[str, Any]], identifier: str, section: str) -> dict[str, Any]:
    matches = [row for row in rows if row.get("id") == identifier]
    if len(matches) != 1:
        raise PipelineError(f"ReferenceNotUnique: {section}.{identifier}")
    return matches[0]


def validate_source(repo_root: Path, asset: dict[str, Any], provenance: dict[str, Any]) -> Path:
    source = asset["source"]
    path = repo_path(repo_root, source["path"])
    if not path.is_file():
        raise PipelineError(f"MissingLocalSource: {source['path']}")
    actual_hash = sha256_file(path)
    if actual_hash != source["sha256"]:
        raise PipelineError(
            f"SourceHashMismatch: expected={source['sha256']} actual={actual_hash}"
        )
    record = find_by_id(provenance["records"], source["provenanceId"], "provenance")
    issues = []
    if record.get("sourcePath") != source["path"]:
        issues.append("ProvenanceSourcePathMismatch")
    if record.get("sourceSha256") != source["sha256"]:
        issues.append("ProvenanceSourceHashMismatch")
    if record.get("catalogAssetId") != asset["catalogAssetId"]:
        issues.append("ProvenanceCatalogIdentityMismatch")
    if not record.get("sourceMaterialOnly") or not record.get("localSourceRequired"):
        issues.append("UnsafeSourceAuthority")
    if not record.get("rightsEvidence"):
        issues.append("MissingRightsEvidence")
    for relative in record.get("rightsEvidence", []):
        if not repo_path(repo_root, relative).is_file():
            issues.append(f"MissingRightsEvidencePath: {relative}")
    if issues:
        raise PipelineError(issues)
    return path


def load_source(path: Path, source_type: str) -> None:
    if source_type == "blend":
        bpy.ops.wm.open_mainfile(filepath=str(path))
    elif source_type == "fbx":
        bpy.ops.wm.read_factory_settings(use_empty=True)
        result = bpy.ops.wm.fbx_import(filepath=str(path), use_anim=True)
        if "FINISHED" not in result:
            raise PipelineError(f"FbxImportFailed: {path}")
    else:
        raise PipelineError(f"UnsupportedSourceType: {source_type}")


def resolve_objects(asset: dict[str, Any]) -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    armature = bpy.data.objects.get(asset["armatureObject"])
    if armature is None or armature.type != "ARMATURE":
        raise PipelineError(f"MissingArmatureObject: {asset['armatureObject']}")
    selector = asset["meshObjects"]
    if selector == "all_meshes":
        meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    else:
        missing = [name for name in selector if bpy.data.objects.get(name) is None]
        if missing:
            raise PipelineError(f"MissingMeshObjects: {missing}")
        meshes = [bpy.data.objects[name] for name in selector]
        if any(obj.type != "MESH" for obj in meshes):
            raise PipelineError("ConfiguredMeshObjectIsNotMesh")
    meshes = sorted(set(meshes), key=lambda obj: obj.name.encode("utf-8"))
    if not meshes:
        raise PipelineError("NoMeshObjectsSelected")
    return armature, meshes


def remove_unselected_objects(
    armature: bpy.types.Object,
    meshes: list[bpy.types.Object],
) -> list[str]:
    keep = {armature, *meshes}
    removed = []
    for obj in sorted(bpy.data.objects, key=lambda item: item.name.encode("utf-8")):
        if obj in keep:
            continue
        for child in list(obj.children):
            if child in keep:
                world = child.matrix_world.copy()
                child.parent = None
                child.matrix_world = world
        removed.append(obj.name)
        bpy.data.objects.remove(obj, do_unlink=True)
    return removed


def make_single_collection(
    armature: bpy.types.Object, meshes: list[bpy.types.Object], asset_id: str
) -> None:
    scene = bpy.context.scene
    collection = bpy.data.collections.new(f"COL_{asset_id}")
    scene.collection.children.link(collection)
    keep = [armature, *meshes]
    for obj in keep:
        for existing in list(obj.users_collection):
            existing.objects.unlink(obj)
        collection.objects.link(obj)
    for existing in list(bpy.data.collections):
        if existing == collection:
            continue
        if existing.users == 0 or not existing.objects:
            bpy.data.collections.remove(existing)


def animation_frames(armature: bpy.types.Object) -> list[int]:
    frames: set[int] = {int(bpy.context.scene.frame_current)}
    actions = sorted(bpy.data.actions, key=lambda action: action.name.encode("utf-8"))
    for action in actions:
        start, end = (round(value) for value in action.frame_range)
        frames.update({start, end})
        if end > start:
            frames.update(
                round(start + (end - start) * step / 4.0) for step in range(1, 4)
            )
        for layer in getattr(action, "layers", []):
            for strip in layer.strips:
                for channelbag in getattr(strip, "channelbags", []):
                    for fcurve in channelbag.fcurves:
                        frames.update(
                            round(point.co.x) for point in fcurve.keyframe_points
                        )
        for fcurve in getattr(action, "fcurves", []):
            frames.update(round(point.co.x) for point in fcurve.keyframe_points)
    bounded = sorted(frames)
    if len(bounded) > 1001:
        raise PipelineError(f"AnimationFrameBudgetExceeded: {len(bounded)}")
    if len(bpy.data.actions) and bounded:
        first, last = bounded[0], bounded[-1]
        if last - first > 1000:
            raise PipelineError(f"AnimationDurationBudgetExceeded: {first}->{last}")
        return list(range(first, last + 1))
    return bounded


def geometry_coordinates(
    armature: bpy.types.Object,
    meshes: list[bpy.types.Object],
    frames: list[int],
    pose_position: str = "POSE",
) -> list[tuple[float, float, float]]:
    coordinates: list[tuple[float, float, float]] = []
    scene = bpy.context.scene
    dependency_graph = bpy.context.evaluated_depsgraph_get()
    original_frame = scene.frame_current
    original_pose_position = armature.data.pose_position
    try:
        armature.data.pose_position = pose_position
        for frame in frames:
            scene.frame_set(frame)
            dependency_graph.update()
            samples: list[Vector] = []
            minimum_z = math.inf
            for obj in meshes:
                evaluated = obj.evaluated_get(dependency_graph)
                mesh = evaluated.to_mesh(preserve_all_data_layers=False, depsgraph=dependency_graph)
                try:
                    for vertex in mesh.vertices:
                        world = evaluated.matrix_world @ vertex.co
                        minimum_z = min(minimum_z, world.z)
                        samples.append(world.copy())
                finally:
                    evaluated.to_mesh_clear()
            coordinates.extend(
                (
                    float(world.x),
                    float(world.y),
                    float(world.z - minimum_z),
                )
                for world in samples
            )
    finally:
        armature.data.pose_position = original_pose_position
        scene.frame_set(original_frame)
    return coordinates


def geometry_signature(coordinates: list[tuple[float, float, float]]) -> str:
    digest = hashlib.sha256()
    for x, y, z in coordinates:
        digest.update(f"{x:.5f}|{y:.5f}|{z:.5f}\n".encode())
    return digest.hexdigest()


def maximum_geometry_drift(
    before: list[tuple[float, float, float]],
    after: list[tuple[float, float, float]],
) -> float:
    if len(before) != len(after):
        raise PipelineError(
            f"GeometryVertexCountChanged: before={len(before)} after={len(after)}"
        )
    return max(
        (Vector(left) - Vector(right)).length for left, right in zip(before, after)
    )


def select_objects(objects: list[bpy.types.Object], active: bpy.types.Object) -> None:
    if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active


def bake_world_transforms(
    armature: bpy.types.Object, meshes: list[bpy.types.Object]
) -> Matrix:
    unsupported_curves = []
    animated_object_curves = []
    for action in bpy.data.actions:
        for curve in action_paths(action):
            if curve.data_path.startswith("pose.bones["):
                continue
            if curve.data_path not in {
                "location",
                "rotation_euler",
                "rotation_quaternion",
                "scale",
            }:
                unsupported_curves.append(curve.data_path)
                continue
            values = [point.co.y for point in curve.keyframe_points]
            if values and max(values) - min(values) > EPSILON:
                animated_object_curves.append(
                    f"{action.name}:{curve.data_path}[{curve.array_index}]"
                )
    if unsupported_curves:
        raise PipelineError(
            f"UnsupportedObjectAnimationCurves: {sorted(set(unsupported_curves))}"
        )
    if animated_object_curves:
        raise PipelineError(
            f"AnimatedObjectTransformRequiresMotionRootBake: {sorted(animated_object_curves)}"
        )
    armature_world = armature.matrix_world.copy()
    _, _, source_scale = armature_world.decompose()
    if max(source_scale) - min(source_scale) > EPSILON:
        raise PipelineError(f"NonUniformArmatureScale: {tuple(source_scale)}")
    location_scale = sum(source_scale) / 3.0
    if abs(location_scale - 1.0) > EPSILON:
        for action in bpy.data.actions:
            for curve in action_paths(action):
                if not (
                    curve.data_path.startswith('pose.bones["')
                    and curve.data_path.endswith(".location")
                ):
                    continue
                for point in curve.keyframe_points:
                    point.co.y *= location_scale
                    point.handle_left.y *= location_scale
                    point.handle_right.y *= location_scale
    for action in list(bpy.data.actions):
        remove_object_transform_curves(action)
    bpy.context.view_layer.update()
    select_objects([armature, *meshes], armature)
    result = bpy.ops.object.transform_apply(
        location=True,
        rotation=True,
        scale=True,
        properties=False,
    )
    if "FINISHED" not in result:
        raise PipelineError("TransformApplyFailed")
    return armature_world


def world_minimum_z(meshes: list[bpy.types.Object]) -> float:
    result = math.inf
    for obj in meshes:
        for vertex in obj.data.vertices:
            result = min(result, (obj.matrix_world @ vertex.co).z)
    if not math.isfinite(result):
        raise PipelineError("GroundMeasurementFailed")
    return result


def ground_and_apply(
    armature: bpy.types.Object, meshes: list[bpy.types.Object]
) -> float:
    bake_world_transforms(armature, meshes)
    offset = world_minimum_z(meshes)
    translation = Matrix.Translation((0.0, 0.0, -offset))
    armature.data.transform(translation)
    for mesh in meshes:
        mesh.data.transform(translation)
    return offset


def action_paths(action: bpy.types.Action) -> list[Any]:
    curves = list(getattr(action, "fcurves", []))
    for layer in getattr(action, "layers", []):
        for strip in layer.strips:
            for channelbag in getattr(strip, "channelbags", []):
                curves.extend(channelbag.fcurves)
    return curves


def remove_object_transform_curves(action: bpy.types.Action) -> None:
    allowed = {"location", "rotation_euler", "rotation_quaternion", "scale"}
    if action.layers:
        for layer in action.layers:
            for strip in layer.strips:
                for channelbag in getattr(strip, "channelbags", []):
                    for curve in list(channelbag.fcurves):
                        if curve.data_path in allowed:
                            channelbag.fcurves.remove(curve)
    else:
        for curve in list(action.fcurves):
            if curve.data_path in allowed:
                action.fcurves.remove(curve)
def rename_bones_and_groups(
    armature: bpy.types.Object,
    meshes: list[bpy.types.Object],
    rename_map: dict[str, str],
) -> None:
    existing = {bone.name for bone in armature.data.bones}
    missing = sorted(set(rename_map) - existing)
    if missing:
        raise PipelineError(f"MissingSourceBones: {missing}")
    targets = list(rename_map.values())
    if len(targets) != len(set(targets)):
        raise PipelineError("DuplicateBoneRenameTarget")
    collisions = sorted((set(targets) & existing) - set(rename_map))
    if collisions:
        raise PipelineError(f"BoneRenameCollision: {collisions}")

    temporary: dict[str, str] = {}
    for index, source in enumerate(sorted(rename_map, key=lambda value: value.encode("utf-8"))):
        temp = f"al_tmp_bone_{index:03d}"
        armature.data.bones[source].name = temp
        temporary[source] = temp
    for source, temp in temporary.items():
        armature.data.bones[temp].name = rename_map[source]

    for mesh in meshes:
        original_names = [group.name for group in mesh.vertex_groups]
        for index, name in enumerate(original_names):
            if name in rename_map:
                mesh.vertex_groups[index].name = rename_map[name]

    for action in bpy.data.actions:
        for curve in action_paths(action):
            path = curve.data_path
            for source, target in rename_map.items():
                path = path.replace(f'pose.bones["{source}"]', f'pose.bones["{target}"]')
            curve.data_path = path


def canonicalize_action(
    armature: bpy.types.Object,
    frames: list[int],
    asset: dict[str, Any],
) -> dict[str, Any] | None:
    """Bake source motion into one stable, linearly sampled Blender action."""

    if not bpy.data.actions:
        return None
    if len(bpy.data.actions) != 1:
        raise PipelineError(f"MultipleSourceActionsUnsupported: {len(bpy.data.actions)}")

    source_action = bpy.data.actions[0]
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = source_action
    animated_bones = sorted(
        {
            match.group(1)
            for curve in action_paths(source_action)
            if (match := re.match(r'^pose\.bones\["([^"\\]+)"\]\.', curve.data_path))
        }
    )
    if not animated_bones:
        raise PipelineError("SourceActionContainsNoPoseBoneChannels")
    unknown = sorted(set(animated_bones) - set(armature.pose.bones.keys()))
    if unknown:
        raise PipelineError(f"SourceActionUnknownBones: {unknown}")

    scene = bpy.context.scene
    original_frame = int(scene.frame_current)
    samples: dict[int, dict[str, tuple[Vector, Quaternion, Vector]]] = {}
    prior_quaternions: dict[str, Quaternion] = {}
    for frame in frames:
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        frame_samples: dict[str, tuple[Vector, Quaternion, Vector]] = {}
        for bone_name in animated_bones:
            location, rotation, scale = armature.pose.bones[bone_name].matrix_basis.decompose()
            prior = prior_quaternions.get(bone_name)
            if prior is not None and prior.dot(rotation) < 0.0:
                rotation.negate()
            prior_quaternions[bone_name] = rotation.copy()
            frame_samples[bone_name] = (location.copy(), rotation.copy(), scale.copy())
        samples[frame] = frame_samples

    armature.animation_data.action = None
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action, do_unlink=True)
    action_name = f"ANIM_{asset['id']}_source_v001"
    output_action = bpy.data.actions.new(name=action_name)
    armature.animation_data.action = output_action
    for frame in frames:
        for bone_name in animated_bones:
            pose_bone = armature.pose.bones[bone_name]
            location, rotation, scale = samples[frame][bone_name]
            pose_bone.rotation_mode = "QUATERNION"
            pose_bone.location = location
            pose_bone.rotation_quaternion = rotation
            pose_bone.scale = scale
            pose_bone.keyframe_insert(data_path="location", frame=frame, group=bone_name)
            pose_bone.keyframe_insert(
                data_path="rotation_quaternion", frame=frame, group=bone_name
            )
            pose_bone.keyframe_insert(data_path="scale", frame=frame, group=bone_name)

    unique_curves = {
        (curve.data_path, curve.array_index): curve for curve in action_paths(output_action)
    }
    for curve in unique_curves.values():
        for point in curve.keyframe_points:
            point.interpolation = "LINEAR"
    scene.frame_start = frames[0]
    scene.frame_end = frames[-1]
    scene.frame_set(original_frame if original_frame in frames else frames[0])
    bpy.context.view_layer.update()
    return {
        "actionName": action_name,
        "animatedBones": animated_bones,
        "frameCount": len(frames),
        "frameStart": frames[0],
        "frameEnd": frames[-1],
    }


def action_signature() -> str:
    rows: list[dict[str, Any]] = []
    for action in sorted(bpy.data.actions, key=lambda item: item.name):
        unique_curves = {
            (curve.data_path, curve.array_index): curve for curve in action_paths(action)
        }
        for (data_path, array_index), curve in sorted(unique_curves.items()):
            rows.append(
                {
                    "action": action.name,
                    "arrayIndex": array_index,
                    "dataPath": data_path,
                    "keys": [
                        [
                            round(float(point.co[0]), 6),
                            round(float(point.co[1]), 7),
                            point.interpolation,
                        ]
                        for point in curve.keyframe_points
                    ],
                }
            )
    return hashlib.sha256(stable_json(rows).encode("utf-8")).hexdigest()


def add_roots_hierarchy_and_sockets(
    armature: bpy.types.Object,
    asset: dict[str, Any],
) -> None:
    select_objects([armature], armature)
    bpy.ops.object.mode_set(mode="EDIT")
    edit_bones = armature.data.edit_bones
    issues = []
    for required in set(asset["requiredBones"]) - {"root", "motion_root"}:
        if edit_bones.get(required) is None and required not in asset["sockets"]:
            issues.append(f"MissingRequiredBoneBeforeSocketBuild: {required}")
    if edit_bones.get("root") is not None or edit_bones.get("motion_root") is not None:
        issues.append("ReservedRootsAlreadyExist")
    if issues:
        bpy.ops.object.mode_set(mode="OBJECT")
        raise PipelineError(issues)

    root = edit_bones.new("root")
    root.head = (0.0, 0.0, 0.0)
    root.tail = (0.0, 0.1, 0.0)
    root.use_deform = False
    motion = edit_bones.new("motion_root")
    motion.head = (0.0, 0.0, 0.0)
    motion.tail = (0.0, 0.1, 0.0)
    motion.parent = root
    motion.use_connect = False
    motion.use_deform = False

    body = edit_bones.get(asset["bodyRootBone"])
    if body is None:
        bpy.ops.object.mode_set(mode="OBJECT")
        raise PipelineError(f"MissingBodyRootBone: {asset['bodyRootBone']}")
    body.parent = motion
    body.use_connect = False

    for child_name, parent_name in sorted(asset["hierarchyOverrides"].items()):
        child = edit_bones.get(child_name)
        parent = edit_bones.get(parent_name)
        if child is None or parent is None:
            issues.append(f"HierarchyOverrideMissingBone: {child_name}->{parent_name}")
            continue
        child.parent = parent
        child.use_connect = False

    armature_extent = max(
        0.01,
        max((bone.head - bone.tail).length for bone in edit_bones) * 0.05,
    )
    for socket_name, parent_name in sorted(
        asset["sockets"].items(), key=lambda item: item[0].encode("utf-8")
    ):
        if edit_bones.get(socket_name) is not None:
            issues.append(f"SocketAlreadyExists: {socket_name}")
            continue
        parent = edit_bones.get(parent_name)
        if parent is None:
            issues.append(f"SocketParentMissing: {socket_name}->{parent_name}")
            continue
        socket = edit_bones.new(socket_name)
        socket.head = parent.tail.copy()
        socket.tail = socket.head + Vector((0.0, armature_extent, 0.0))
        socket.parent = parent
        socket.use_connect = False
        socket.use_deform = False

    bpy.ops.object.mode_set(mode="OBJECT")
    if issues:
        raise PipelineError(issues)


def ensure_armature_bindings(
    armature: bpy.types.Object, meshes: list[bpy.types.Object]
) -> None:
    issues = []
    for mesh in meshes:
        modifiers = [modifier for modifier in mesh.modifiers if modifier.type == "ARMATURE"]
        if not modifiers:
            modifier = mesh.modifiers.new(name="Armature", type="ARMATURE")
            modifier.object = armature
            modifiers = [modifier]
        for modifier in modifiers:
            if modifier.object is None:
                modifier.object = armature
            if modifier.object != armature:
                issues.append(f"ForeignArmatureModifier: {mesh.name}.{modifier.name}")
        if len(modifiers) != 1:
            issues.append(f"ArmatureModifierCount: {mesh.name}={len(modifiers)}")
    if issues:
        raise PipelineError(issues)


def weight_statistics(
    armature: bpy.types.Object, meshes: list[bpy.types.Object]
) -> dict[str, int]:
    deform_names = {bone.name for bone in armature.data.bones if bone.use_deform}
    maximum = 0
    unweighted = 0
    non_normalized = 0
    below_floor = 0
    over_budget = 0
    vertices = 0
    for mesh in meshes:
        index_to_name = {group.index: group.name for group in mesh.vertex_groups}
        for vertex in mesh.data.vertices:
            vertices += 1
            weights = [
                assignment.weight
                for assignment in vertex.groups
                if index_to_name.get(assignment.group) in deform_names
                and assignment.weight > 0.0
            ]
            maximum = max(maximum, len(weights))
            if not weights:
                unweighted += 1
                continue
            if len(weights) > MAX_INFLUENCES:
                over_budget += 1
            below_floor += sum(weight < WEIGHT_FLOOR for weight in weights)
            if abs(sum(weights) - 1.0) > WEIGHT_TOLERANCE:
                non_normalized += 1
    return {
        "vertices": vertices,
        "maximumInfluencesPerVertex": maximum,
        "verticesOverInfluenceBudget": over_budget,
        "unweightedVertices": unweighted,
        "nonNormalizedVertices": non_normalized,
        "weightsBelowFloor": below_floor,
    }


def clean_weights(armature: bpy.types.Object, meshes: list[bpy.types.Object]) -> None:
    for mesh in meshes:
        select_objects([mesh], mesh)
        result = bpy.ops.object.vertex_group_clean(
            group_select_mode="BONE_DEFORM",
            limit=WEIGHT_FLOOR,
            keep_single=True,
        )
        if "FINISHED" not in result:
            raise PipelineError(f"VertexGroupCleanFailed: {mesh.name}")
        result = bpy.ops.object.vertex_group_limit_total(
            group_select_mode="BONE_DEFORM", limit=MAX_INFLUENCES
        )
        if "FINISHED" not in result:
            raise PipelineError(f"VertexGroupLimitFailed: {mesh.name}")
        result = bpy.ops.object.vertex_group_normalize_all(
            group_select_mode="BONE_DEFORM", lock_active=False
        )
        if "FINISHED" not in result:
            raise PipelineError(f"VertexGroupNormalizeFailed: {mesh.name}")


def triangulate_meshes(meshes: list[bpy.types.Object]) -> dict[str, int]:
    non_tri_converted = 0
    duplicate_faces_removed = 0
    degenerate_faces_removed = 0
    for mesh in meshes:
        if mesh.data.shape_keys is not None:
            raise PipelineError(f"ShapeKeyTopologyRequiresManualReview: {mesh.name}")
        before = sum(len(polygon.vertices) != 3 for polygon in mesh.data.polygons)
        editable = bmesh.new()
        editable.from_mesh(mesh.data)
        try:
            if before:
                bmesh.ops.triangulate(
                    editable,
                    faces=list(editable.faces),
                    quad_method="FIXED",
                    ngon_method="BEAUTY",
                )
                non_tri_converted += before
            editable.faces.ensure_lookup_table()
            signatures: set[tuple[tuple[int, int, int], ...]] = set()
            duplicates = []
            degenerates = []
            for face in editable.faces:
                if face.calc_area() <= 1e-10:
                    degenerates.append(face)
                    continue
                signature = tuple(
                    sorted(
                        tuple(round(component * 10_000_000) for component in vertex.co)
                        for vertex in face.verts
                    )
                )
                if signature in signatures:
                    duplicates.append(face)
                else:
                    signatures.add(signature)
            if duplicates or degenerates:
                bmesh.ops.delete(
                    editable,
                    geom=[*duplicates, *degenerates],
                    context="FACES",
                )
                duplicate_faces_removed += len(duplicates)
                degenerate_faces_removed += len(degenerates)
            editable.to_mesh(mesh.data)
            mesh.data.update()
        finally:
            editable.free()
        for polygon in mesh.data.polygons:
            polygon.use_smooth = True
    return {
        "nonTriFacesConverted": non_tri_converted,
        "duplicateFacesRemoved": duplicate_faces_removed,
        "degenerateFacesRemoved": degenerate_faces_removed,
    }


def normalize_names(
    armature: bpy.types.Object, meshes: list[bpy.types.Object], asset: dict[str, Any]
) -> None:
    armature.name = asset["outputArmatureObject"]
    armature.data.name = f"{asset['outputArmatureObject']}_DATA"
    rename_map = asset.get("meshRenameMap", {})
    for mesh in meshes:
        if mesh.name in rename_map:
            mesh.name = rename_map[mesh.name]
        mesh.data.name = f"{mesh.name}_MESH"


def configure_scene(manifest: dict[str, Any], asset: dict[str, Any]) -> None:
    scene = bpy.context.scene
    bpy.context.preferences.filepaths.save_version = 0
    coordinates = manifest["coordinateSystem"]
    scene.unit_settings.system = coordinates["unitSystem"]
    scene.unit_settings.scale_length = coordinates["unitScale"]
    scene.render.engine = "BLENDER_EEVEE"
    scene.frame_start = 0
    scene["al_pipeline_id"] = PIPELINE_ID
    scene["al_asset_id"] = asset["id"]
    scene["al_catalog_asset_id"] = asset["catalogAssetId"]
    scene["al_source_sha256"] = asset["source"]["sha256"]
    scene["al_export_preset_id"] = manifest["exportPreset"]["id"]


def matrix_rows(matrix: Matrix) -> list[list[float]]:
    return [[round(float(value), 6) for value in row] for row in matrix]


def bone_records(armature: bpy.types.Object) -> list[dict[str, Any]]:
    def path_for(bone: bpy.types.Bone) -> str:
        parts = [bone.name]
        parent = bone.parent
        while parent is not None:
            parts.append(parent.name)
            parent = parent.parent
        return "/".join(reversed(parts))

    records = []
    for bone in armature.data.bones:
        parent_matrix = bone.parent.matrix_local if bone.parent else IDENTITY
        local = parent_matrix.inverted_safe() @ bone.matrix_local
        parent_path = path_for(bone.parent) if bone.parent else ""
        records.append(
            {
                "name": bone.name,
                "path": path_for(bone),
                "parentPath": parent_path,
                "localBindMatrix": matrix_rows(local),
                "deform": bool(bone.use_deform),
            }
        )
    return sorted(records, key=lambda row: row["path"].encode("utf-8"))


def mesh_statistics(mesh: bpy.types.Object) -> dict[str, Any]:
    data = mesh.data
    data.calc_loop_triangles()
    bmesh_data = bmesh.new()
    bmesh_data.from_mesh(data)
    try:
        bmesh_data.faces.ensure_lookup_table()
        bmesh_data.edges.ensure_lookup_table()
        non_manifold = sum(not edge.is_manifold for edge in bmesh_data.edges)
        degenerate = sum(face.calc_area() <= 1e-10 for face in bmesh_data.faces)
    finally:
        bmesh_data.free()
    minimum_z = min((mesh.matrix_world @ vertex.co).z for vertex in data.vertices)
    maximum_z = max((mesh.matrix_world @ vertex.co).z for vertex in data.vertices)
    return {
        "name": mesh.name,
        "vertices": len(data.vertices),
        "edges": len(data.edges),
        "faces": len(data.polygons),
        "triangles": len(data.loop_triangles),
        "nonTriFaces": sum(len(polygon.vertices) != 3 for polygon in data.polygons),
        "ngons": sum(len(polygon.vertices) > 4 for polygon in data.polygons),
        "degenerateTriangles": degenerate,
        "nonManifoldEdges": non_manifold,
        "materialSlots": len(mesh.material_slots),
        "minimumZ": round(minimum_z, 6),
        "maximumZ": round(maximum_z, 6),
    }


def object_is_identity(obj: bpy.types.Object, tolerance: float) -> bool:
    return all(
        abs(float(obj.matrix_world[row][column]) - float(IDENTITY[row][column]))
        <= tolerance
        for row in range(4)
        for column in range(4)
    )


def preflight(
    manifest: dict[str, Any],
    asset: dict[str, Any],
    standard: dict[str, Any],
) -> tuple[dict[str, Any], list[dict[str, Any]], bpy.types.Object, list[bpy.types.Object]]:
    armature = bpy.data.objects.get(asset["outputArmatureObject"])
    if armature is None or armature.type != "ARMATURE":
        raise PipelineError(f"CleanArmatureMissing: {asset['outputArmatureObject']}")
    meshes = sorted(
        [obj for obj in bpy.data.objects if obj.type == "MESH"],
        key=lambda obj: obj.name.encode("utf-8"),
    )
    if not meshes:
        raise PipelineError("CleanMeshesMissing")

    tolerance = manifest["coordinateSystem"]["transformTolerance"]
    errors: list[str] = []
    warnings: list[str] = []
    if bpy.context.scene.unit_settings.system != "METRIC":
        errors.append("SceneUnitsNotMetric")
    if abs(bpy.context.scene.unit_settings.scale_length - 1.0) > tolerance:
        errors.append("SceneUnitScaleNotOne")
    for obj in [armature, *meshes]:
        if not object_is_identity(obj, tolerance):
            errors.append(f"ObjectTransformNotIdentity: {obj.name}")

    names = {bone.name for bone in armature.data.bones}
    missing = sorted((set(asset["requiredBones"]) | set(asset["sockets"])) - names)
    if missing:
        errors.append(f"RequiredBonesMissing: {missing}")
    invalid_names = sorted(name for name in names if not BONE_NAME_PATTERN.fullmatch(name))
    if invalid_names:
        errors.append(f"InvalidBoneNames: {invalid_names}")
    root = armature.data.bones.get("root")
    motion = armature.data.bones.get("motion_root")
    body = armature.data.bones.get(asset["bodyRootBone"])
    if root is None or root.parent is not None or root.use_deform:
        errors.append("RootContractViolation")
    if motion is None or motion.parent != root or motion.use_deform:
        errors.append("MotionRootContractViolation")
    if body is None or body.parent != motion:
        errors.append("BodyRootContractViolation")
    for socket, parent in asset["sockets"].items():
        bone = armature.data.bones.get(socket)
        if bone is None:
            continue
        if bone.use_deform or bone.parent is None or bone.parent.name != parent:
            errors.append(f"SocketContractViolation: {socket}")
    for child_name, parent_name in asset["hierarchyOverrides"].items():
        child = armature.data.bones.get(child_name)
        if child is None or child.parent is None or child.parent.name != parent_name:
            errors.append(f"HierarchyContractViolation: {child_name}->{parent_name}")
    for bone in armature.data.bones:
        if bone.length <= EPSILON:
            errors.append(f"ZeroLengthBone: {bone.name}")

    budget = find_by_id(standard["qualityBudgets"], asset["budgetProfileId"], "budget")
    deform_count = sum(bone.use_deform for bone in armature.data.bones)
    if deform_count > budget["skinning"]["maximumDeformingBones"]:
        errors.append(f"DeformingBoneBudget: {deform_count}")
    if len(armature.data.bones) > budget["skinning"]["maximumAnimatedTransforms"]:
        errors.append(f"AnimatedTransformBudget: {len(armature.data.bones)}")

    mesh_rows = [mesh_statistics(mesh) for mesh in meshes]
    for row in mesh_rows:
        if row["nonTriFaces"] or row["ngons"]:
            errors.append(f"TopologyNotTriangulated: {row['name']}")
        if row["degenerateTriangles"]:
            errors.append(f"DegenerateTriangles: {row['name']}={row['degenerateTriangles']}")
        if row["triangles"] > budget["topology"]["maximumLod0Triangles"]:
            errors.append(f"TriangleBudget: {row['name']}={row['triangles']}")
        if row["materialSlots"] > budget["topology"]["maximumMaterialSlots"]:
            errors.append(f"MaterialSlotBudget: {row['name']}={row['materialSlots']}")
        if row["nonManifoldEdges"]:
            warnings.append(
                f"DocumentedModuleSeams: {row['name']}={row['nonManifoldEdges']} non-manifold edges"
            )
    minimum_z = min(row["minimumZ"] for row in mesh_rows)
    if abs(minimum_z) > manifest["coordinateSystem"]["groundToleranceMeters"]:
        errors.append(f"GroundContactError: minimumZ={minimum_z}")

    weights = weight_statistics(armature, meshes)
    if weights["maximumInfluencesPerVertex"] > MAX_INFLUENCES:
        errors.append("InfluenceBudgetViolation")
    if weights["unweightedVertices"]:
        errors.append(f"UnweightedVertices: {weights['unweightedVertices']}")
    if weights["nonNormalizedVertices"]:
        errors.append(f"NonNormalizedVertices: {weights['nonNormalizedVertices']}")
    if weights["weightsBelowFloor"]:
        errors.append(f"WeightsBelowFloor: {weights['weightsBelowFloor']}")
    if len(bpy.data.actions) < asset["minimumSourceActions"]:
        errors.append(
            f"RequiredSourceActionMissing: minimum={asset['minimumSourceActions']} "
            f"actual={len(bpy.data.actions)}"
        )
    if not asset["preserveActions"] and bpy.data.actions:
        errors.append(f"UnexpectedActions: {len(bpy.data.actions)}")

    result = {
        "maximumInfluencesPerVertex": weights["maximumInfluencesPerVertex"],
        "verticesOverInfluenceBudget": weights["verticesOverInfluenceBudget"],
        "unweightedVertices": weights["unweightedVertices"],
        "nonNormalizedVertices": weights["nonNormalizedVertices"],
        "weightsBelowFloor": weights["weightsBelowFloor"],
        "vertices": weights["vertices"],
        "deformingBones": deform_count,
        "animatedTransforms": len(armature.data.bones),
        "nonTriFaces": sum(row["nonTriFaces"] for row in mesh_rows),
        "ngons": sum(row["ngons"] for row in mesh_rows),
        "degenerateTriangles": sum(row["degenerateTriangles"] for row in mesh_rows),
        "nonManifoldEdges": sum(row["nonManifoldEdges"] for row in mesh_rows),
        "minimumZ": minimum_z,
        "actions": len(bpy.data.actions),
        "actionSignature": action_signature(),
        "warnings": warnings,
        "errors": sorted(errors),
    }
    return result, mesh_rows, armature, meshes


def build_sidecar(
    repo_root: Path,
    manifest: dict[str, Any],
    asset: dict[str, Any],
    standard: dict[str, Any],
    provenance: dict[str, Any],
    output_blend: Path,
) -> dict[str, Any]:
    preflight_row, mesh_rows, armature, _ = preflight(manifest, asset, standard)
    records = bone_records(armature)
    provenance_record = find_by_id(
        provenance["records"], asset["source"]["provenanceId"], "provenance"
    )
    evidence = json.loads(bpy.context.scene.get("al_cleanup_evidence", "{}"))
    errors = preflight_row.pop("errors")
    status = "technical_candidate_valid" if not errors else "rejected"
    skeleton_hash = skeleton_signature(records)
    content_hash = cleaned_content_signature(
        asset["id"], skeleton_hash, mesh_rows, preflight_row
    )
    sidecar = {
        "schemaVersion": 1,
        "pipelineId": PIPELINE_ID,
        "assetId": asset["id"],
        "catalogAssetId": asset["catalogAssetId"],
        "subjectKind": asset["subjectKind"],
        "status": status,
        "approvalState": asset["approvalState"],
        "productionEligible": False,
        "productionGaps": asset["productionGaps"],
        "source": {
            "path": asset["source"]["path"],
            "sha256": asset["source"]["sha256"],
            "provenanceId": asset["source"]["provenanceId"],
            "rightsState": provenance_record["rightsState"],
            "productionRightsCleared": provenance_record["productionRightsCleared"],
            "localSourceRequired": True,
            "sourceMaterialOnly": True,
        },
        "output": {
            "blendPath": asset["output"]["blendPath"],
            "blendContentSignature": content_hash,
            "blenderVersion": bpy.app.version_string,
        },
        "bindings": {
            "representativeProfileId": asset["representativeProfileId"],
            "skeletonProfileId": asset["skeletonProfileId"],
            "bindPoseId": asset["bindPoseId"],
            "retargetProfileId": asset["retargetProfileId"],
            "facialProfileId": asset["facialProfileId"],
            "budgetProfileId": asset["budgetProfileId"],
            "requiredMotionSetId": asset["requiredMotionSetId"],
        },
        "skeleton": {
            "algorithm": SIGNATURE_ALGORITHM,
            "signature": skeleton_hash,
            "records": records,
        },
        "meshes": mesh_rows,
        "preflight": preflight_row,
        "cleanupEvidence": evidence,
        "errors": errors,
    }
    return sidecar


def build_asset(
    repo_root: Path,
    manifest: dict[str, Any],
    asset: dict[str, Any],
    standard: dict[str, Any],
    provenance: dict[str, Any],
) -> dict[str, Any]:
    source_path = validate_source(repo_root, asset, provenance)
    load_source(source_path, asset["source"]["type"])
    armature, meshes = resolve_objects(asset)
    frames = animation_frames(armature)
    action_count_before = len(bpy.data.actions)
    coordinates_before = geometry_coordinates(armature, meshes, frames)
    signature_before = geometry_signature(coordinates_before)
    weights_before = weight_statistics(armature, meshes)
    removed = remove_unselected_objects(armature, meshes)
    ground_offset = ground_and_apply(armature, meshes)
    rename_bones_and_groups(armature, meshes, asset["boneRenameMap"])
    add_roots_hierarchy_and_sockets(armature, asset)
    ensure_armature_bindings(armature, meshes)
    normalize_names(armature, meshes, asset)
    configure_scene(manifest, asset)
    make_single_collection(armature, meshes, asset["id"])
    canonical_action = canonicalize_action(armature, frames, asset)
    coordinates_after = geometry_coordinates(armature, meshes, frames)
    signature_after = geometry_signature(coordinates_after)
    maximum_drift = maximum_geometry_drift(coordinates_before, coordinates_after)
    if maximum_drift > manifest["coordinateSystem"]["transformTolerance"]:
        raise PipelineError(
            f"GeometryDriftAfterNormalization: maximum={maximum_drift:.9f} "
            f"tolerance={manifest['coordinateSystem']['transformTolerance']:.9f}"
        )
    clean_weights(armature, meshes)
    topology_cleanup = triangulate_meshes(meshes)
    coordinates_cleaned = geometry_coordinates(armature, meshes, frames)
    signature_cleaned = geometry_signature(coordinates_cleaned)
    maximum_cleanup_drift = maximum_geometry_drift(
        coordinates_after, coordinates_cleaned
    )
    cleanup_tolerance = asset["deformationCleanupToleranceMeters"]
    if maximum_cleanup_drift > cleanup_tolerance:
        raise PipelineError(
            f"DeformationDriftAfterCleanup: maximum={maximum_cleanup_drift:.9f} "
            f"tolerance={cleanup_tolerance:.9f}"
        )
    weights_after = weight_statistics(armature, meshes)
    if asset["preserveActions"] and len(bpy.data.actions) != action_count_before:
        raise PipelineError(
            f"ActionCountChanged: before={action_count_before} after={len(bpy.data.actions)}"
        )
    evidence = {
        "sourceActionCount": action_count_before,
        "canonicalAction": canonical_action,
        "actionSignature": action_signature(),
        "constantObjectTransformCurvesRemoved": True,
        "sampleFrames": frames,
        "geometrySignatureBefore": signature_before,
        "geometrySignatureAfter": signature_after,
        "geometryMaximumDriftMeters": round(maximum_drift, 9),
        "cleanedGeometrySignature": signature_cleaned,
        "deformationCleanupMaximumDriftMeters": round(maximum_cleanup_drift, 9),
        "deformationCleanupToleranceMeters": cleanup_tolerance,
        "sourceGroundOffsetMeters": round(ground_offset, 6),
        "removedObjects": removed,
        "boneRenames": len(asset["boneRenameMap"]),
        "socketsAdded": len(asset["sockets"]),
        "sourceWeights": weights_before,
        "cleanedWeights": weights_after,
        "topologyCleanup": topology_cleanup,
    }
    bpy.context.scene["al_cleanup_evidence"] = json.dumps(evidence, sort_keys=True)
    armature["al_skeleton_profile_id"] = asset["skeletonProfileId"]
    armature["al_bind_pose_id"] = asset["bindPoseId"]
    armature["al_retarget_profile_id"] = asset["retargetProfileId"]
    armature["al_facial_profile_id"] = asset["facialProfileId"]
    armature["al_budget_profile_id"] = asset["budgetProfileId"]
    armature["al_required_motion_set_id"] = asset["requiredMotionSetId"]

    output_blend = repo_path(repo_root, asset["output"]["blendPath"])
    output_blend.parent.mkdir(parents=True, exist_ok=True)
    select_objects([armature, *meshes], armature)
    result = bpy.ops.wm.save_as_mainfile(
        filepath=str(output_blend), check_existing=False, compress=True
    )
    if "FINISHED" not in result:
        raise PipelineError(f"BlendSaveFailed: {output_blend}")
    sidecar = build_sidecar(
        repo_root, manifest, asset, standard, provenance, output_blend
    )
    sidecar_path = repo_path(repo_root, asset["output"]["sidecarPath"])
    write_json(sidecar_path, sidecar)
    if sidecar["errors"]:
        raise PipelineError(sidecar["errors"])
    return sidecar


def load_clean_blend(repo_root: Path, asset: dict[str, Any]) -> Path:
    path = repo_path(repo_root, asset["output"]["blendPath"])
    if not path.is_file():
        raise PipelineError(f"CleanBlendMissing: {path}")
    bpy.ops.wm.open_mainfile(filepath=str(path))
    if bpy.context.scene.get("al_asset_id") != asset["id"]:
        raise PipelineError("CleanBlendIdentityMismatch")
    return path


def dimensions_signature(objects: list[bpy.types.Object]) -> list[list[float]]:
    result = []
    for obj in objects:
        values = sorted(round(abs(float(value)), 4) for value in obj.dimensions)
        result.append(values)
    return sorted(result)


def configure_deterministic_fbx_export() -> None:
    from io_scene_fbx import export_fbx_bin as fallback_export_module
    from io_scene_fbx import fbx_utils as fallback_utils_module

    utility_modules = {
        fallback_utils_module,
        *(
            module
            for name, module in sys.modules.items()
            if name.endswith("io_scene_fbx.fbx_utils") and module is not None
        ),
    }
    export_modules = {
        fallback_export_module,
        *(
            module
            for name, module in sys.modules.items()
            if name.endswith("io_scene_fbx.export_fbx_bin") and module is not None
        ),
    }
    for fbx_utils in utility_modules:
        def deterministic_uuid(
            uuids: dict[int, Any], key: Any, module: Any = fbx_utils
        ) -> Any:
            if isinstance(key, int) and 0 <= key < 2**63:
                value = key
            else:
                digest = hashlib.sha256(repr(key).encode("utf-8")).digest()
                value = int.from_bytes(digest[:8], "big") & ((1 << 63) - 1)
            while value in uuids:
                value = (value + 1) & ((1 << 63) - 1)
            return module.UUID(value)

        fbx_utils._keys_to_uuids.clear()
        fbx_utils._uuids_to_keys.clear()
        fbx_utils._key_to_uuid = deterministic_uuid

    fixed_time = datetime.datetime(2000, 1, 1, 0, 0, 0, 0, tzinfo=datetime.timezone.utc)
    for export_module in export_modules:
        original_header = export_module.fbx_header_elements

        def deterministic_header(
            root: Any,
            scene_data: Any,
            time: Any = None,
            header: Any = original_header,
        ) -> Any:
            del time
            return header(root, scene_data, fixed_time)

        export_module.fbx_header_elements = deterministic_header


def export_fbx(
    repo_root: Path,
    manifest: dict[str, Any],
    asset: dict[str, Any],
    standard: dict[str, Any],
    output_override: Path | None = None,
) -> dict[str, Any]:
    preflight_row, mesh_rows, armature, meshes = preflight(manifest, asset, standard)
    if preflight_row["errors"]:
        raise PipelineError(preflight_row["errors"])
    output = output_override or repo_path(repo_root, asset["output"]["fbxPath"])
    output.parent.mkdir(parents=True, exist_ok=True)
    expected_names = {bone.name for bone in armature.data.bones}
    expected_hierarchy = {
        bone.name: bone.parent.name if bone.parent else None for bone in armature.data.bones
    }
    expected_triangles_by_name = {
        row["name"]: row["triangles"] for row in mesh_rows
    }
    expected_triangles = sum(row["triangles"] for row in mesh_rows)
    expected_dimensions = dimensions_signature(meshes)
    expected_action_count = len(bpy.data.actions)
    records = bone_records(armature)
    source_skeleton_signature = skeleton_signature(records)
    source_content_signature = cleaned_content_signature(
        asset["id"], source_skeleton_signature, mesh_rows, {
            key: value for key, value in preflight_row.items() if key != "errors"
        }
    )
    select_objects([armature, *meshes], armature)
    preset = manifest["exportPreset"]
    configure_deterministic_fbx_export()
    result = bpy.ops.export_scene.fbx(
        filepath=str(output),
        check_existing=False,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        global_scale=preset["globalScale"],
        apply_unit_scale=preset["applyUnitScale"],
        apply_scale_options=preset["applyScaleOptions"],
        use_space_transform=preset["useSpaceTransform"],
        bake_space_transform=preset["bakeSpaceTransform"],
        axis_forward=preset["axisForward"],
        axis_up=preset["axisUp"],
        use_mesh_modifiers=preset["useMeshModifiers"],
        mesh_smooth_type=preset["meshSmoothing"],
        use_triangles=preset["useTriangulateFaces"],
        add_leaf_bones=preset["addLeafBones"],
        primary_bone_axis=preset["primaryBoneAxis"],
        secondary_bone_axis=preset["secondaryBoneAxis"],
        armature_nodetype=preset["armatureNodeType"],
        use_armature_deform_only=preset["useArmatureDeformOnly"],
        bake_anim=preset["bakeAnimation"] and preset["includeAnimations"],
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=asset["preserveActions"],
        bake_anim_force_startend_keying=True,
        bake_anim_step=preset["bakeAnimationStep"],
        bake_anim_simplify_factor=preset["bakeAnimationSimplifyFactor"],
        path_mode=preset["pathMode"],
        embed_textures=preset["embedTextures"],
    )
    if "FINISHED" not in result or not output.is_file():
        raise PipelineError(f"FbxExportFailed: {output}")

    export_hash = sha256_file(output)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import_result = bpy.ops.wm.fbx_import(filepath=str(output), use_anim=True)
    roundtrip_errors = []
    if "FINISHED" not in import_result:
        roundtrip_errors.append("RoundTripImportFailed")
    imported_armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    imported_meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    if len(imported_armatures) != 1:
        roundtrip_errors.append(f"RoundTripArmatureCount: {len(imported_armatures)}")
    else:
        imported_armature = imported_armatures[0]
        actual_names = {bone.name for bone in imported_armature.data.bones}
        if actual_names != expected_names:
            roundtrip_errors.append(
                f"RoundTripBoneNames: missing={sorted(expected_names - actual_names)} extra={sorted(actual_names - expected_names)}"
            )
        actual_hierarchy = {
            bone.name: bone.parent.name if bone.parent else None
            for bone in imported_armature.data.bones
        }
        if actual_hierarchy != expected_hierarchy:
            roundtrip_errors.append("RoundTripBoneHierarchyChanged")
    imported_triangles = 0
    imported_triangles_by_name = {}
    for mesh in imported_meshes:
        mesh.data.calc_loop_triangles()
        triangle_count = len(mesh.data.loop_triangles)
        imported_triangles += triangle_count
        imported_triangles_by_name[mesh.name] = triangle_count
    if imported_triangles != expected_triangles:
        roundtrip_errors.append(
            f"RoundTripTriangleCount: expected={expected_triangles_by_name} "
            f"actual={imported_triangles_by_name}"
        )
    imported_dimensions = dimensions_signature(imported_meshes)
    if len(imported_dimensions) != len(expected_dimensions):
        roundtrip_errors.append("RoundTripMeshCountChanged")
    else:
        for expected, actual in zip(expected_dimensions, imported_dimensions):
            if any(abs(a - b) > 0.01 for a, b in zip(expected, actual)):
                roundtrip_errors.append(
                    f"RoundTripDimensionsChanged: expected={expected} actual={actual}"
                )
                break
    imported_action_count = len(bpy.data.actions)
    if expected_action_count and imported_action_count == 0:
        roundtrip_errors.append("RoundTripActionsMissing")

    receipt = {
        "schemaVersion": 1,
        "pipelineId": PIPELINE_ID,
        "assetId": asset["id"],
        "status": "export_valid" if not roundtrip_errors else "rejected",
        "source": {
            "blendPath": asset["output"]["blendPath"],
            "blendContentSignature": source_content_signature,
            "skeletonSignature": source_skeleton_signature,
            "actionSignature": preflight_row["actionSignature"],
        },
        "export": {
            "path": str(output.relative_to(repo_root)).replace("\\", "/")
            if output.is_relative_to(repo_root)
            else str(output),
            "sha256": export_hash,
            "presetId": preset["id"],
            "axisForward": preset["axisForward"],
            "axisUp": preset["axisUp"],
            "globalScale": preset["globalScale"],
            "addLeafBones": preset["addLeafBones"],
            "animationsIncluded": preset["includeAnimations"],
            "determinismLevel": "semantic" if expected_action_count else "byte_exact",
            "uuidPolicy": "best_effort_sha256_repr_int63_v1",
            "fixedCreationTimeUtc": "2000-01-01T00:00:00Z",
        },
        "roundTrip": {
            "armatures": len(imported_armatures),
            "meshes": len(imported_meshes),
            "bones": len(imported_armatures[0].data.bones) if len(imported_armatures) == 1 else 0,
            "triangles": imported_triangles,
            "trianglesByMesh": imported_triangles_by_name,
            "actions": imported_action_count,
            "errors": roundtrip_errors,
        },
    }
    if output_override is None:
        write_json(repo_path(repo_root, asset["output"]["fbxReceiptPath"]), receipt)
    if roundtrip_errors:
        raise PipelineError(roundtrip_errors)
    return receipt


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("build", "preflight", "export"))
    parser.add_argument("--asset", required=True)
    parser.add_argument("--repo-root", default=str(SCRIPT_DIR.parents[1]))
    parser.add_argument(
        "--manifest",
        default="unity/ArtSource/RigPipeline/al_rig_cleanup_manifest.v1.json",
    )
    parser.add_argument("--output-override")
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    args = parse_arguments(arguments)
    repo_root = Path(args.repo_root).resolve()
    manifest, asset, standard, provenance = load_context(
        repo_root, args.manifest, args.asset
    )
    if args.command == "build":
        result = build_asset(repo_root, manifest, asset, standard, provenance)
        print(
            f"AL_RIG_PIPELINE_PASS build {asset['id']} "
            f"{result['skeleton']['signature']}"
        )
    elif args.command == "preflight":
        output_blend = load_clean_blend(repo_root, asset)
        result = build_sidecar(
            repo_root, manifest, asset, standard, provenance, output_blend
        )
        write_json(repo_path(repo_root, asset["output"]["sidecarPath"]), result)
        if result["errors"]:
            raise PipelineError(result["errors"])
        print(
            f"AL_RIG_PIPELINE_PASS preflight {asset['id']} "
            f"{result['skeleton']['signature']}"
        )
    else:
        load_clean_blend(repo_root, asset)
        override = Path(args.output_override).resolve() if args.output_override else None
        result = export_fbx(repo_root, manifest, asset, standard, override)
        print(
            f"AL_RIG_PIPELINE_PASS export {asset['id']} "
            f"{result['export']['sha256']}"
        )
    return 0


if __name__ == "__main__":
    separator = sys.argv.index("--") + 1 if "--" in sys.argv else 1
    try:
        main(sys.argv[separator:])
    except PipelineError as error:
        for issue in error.issues:
            print(f"AL_RIG_PIPELINE_FAIL {issue}", file=sys.stderr)
        raise RuntimeError("AL rig pipeline failed closed") from error
