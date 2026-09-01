#!/usr/bin/env python3
"""Validate AnotherLife's authoritative rig and required-motion contracts."""

from __future__ import annotations

import argparse
import json
from collections.abc import Iterable
from pathlib import Path
from typing import Any

STANDARD_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_rig_motion_standard.json"
)
MANIFEST_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_required_motion_manifest.json"
)
STANDARD_SCHEMA_PATH = Path(
    "unity/SharedContracts/Schemas/al-rig-motion-standard.schema.json"
)
MANIFEST_SCHEMA_PATH = Path(
    "unity/SharedContracts/Schemas/al-required-motion-manifest.schema.json"
)
TEMPLATE_PATH = Path(
    "unity/Docs/ArtPipeline/Templates/Rig_Motion_Asset_Binding_Template_v1.json"
)

EXPECTED_PHASES = (
    "anticipation",
    "cast",
    "channel_start",
    "channel_loop",
    "commit",
    "release",
    "impact",
    "recovery",
    "interruption",
    "cancellation",
)
EXPECTED_ROOT_CATEGORIES = {
    "cinematic",
    "combat",
    "interaction",
    "locomotion",
    "npc_life",
    "reaction",
    "skill",
    "traversal",
}
COMMON_EVENT_FIELDS = {
    "schemaVersion",
    "eventId",
    "actionSequence",
    "eventOrdinal",
    "normalizedTime",
}
COMMON_EVENT_FIELD_TYPES = {
    "schemaVersion": "integer",
    "eventId": "string",
    "actionSequence": "integer",
    "eventOrdinal": "integer",
    "normalizedTime": "number",
}
EXPECTED_BUDGETS = {
    "rmc_budget_beast_mobile_floor_v001": (25000, 42, 16, 4, 2),
    "rmc_budget_boss_mobile_floor_v001": (45000, 96, 48, 16, 4),
    "rmc_budget_champion_mobile_floor_v001": (60000, 89, 48, 12, 4),
    "rmc_budget_monster_mobile_floor_v001": (45000, 72, 32, 8, 3),
    "rmc_budget_npc_mobile_floor_v001": (45000, 72, 32, 8, 3),
}
EXPECTED_REPRESENTATIVES = {
    "rmc_representative_beast_slagwhistle_v001": "beast",
    "rmc_representative_champion_vanguard_v001": "champion",
    "rmc_representative_npc_covenant_sentinel_v001": "npc",
}
SLAGWHISTLE_SET = {
    "idle.neutral",
    "interaction.cut",
    "locomotion.stop",
    "locomotion.turn",
    "locomotion.walk",
    "reaction.spoil_push",
}
SLAGWHISTLE_BLOCKED = {"attack.basic", "attack.special", "defeat"}


class RigMotionValidationError(RuntimeError):
    """Raised when the rig/motion contract fails closed."""

    def __init__(self, issues: Iterable[str]):
        self.issues = sorted(set(issues))
        super().__init__("\n".join(self.issues))


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _schema_errors(schema: dict[str, Any], instance: dict[str, Any]) -> list[str]:
    try:
        from jsonschema import Draft202012Validator, FormatChecker
    except ImportError as error:
        raise RigMotionValidationError(
            ["DependencyMissing: install jsonschema"]
        ) from error

    Draft202012Validator.check_schema(schema)
    errors = sorted(
        Draft202012Validator(schema, format_checker=FormatChecker()).iter_errors(
            instance
        ),
        key=lambda error: list(error.absolute_path),
    )
    result = []
    for error in errors:
        location = ".".join(str(part) for part in error.absolute_path) or "<root>"
        result.append(f"SchemaViolation: {location}: {error.message}")
    return result


def _defined_standard_ids(standard: dict[str, Any]) -> set[str]:
    ids = {standard["standardId"]}
    for section in (
        "skeletonProfiles",
        "bindPoses",
        "socketDefinitions",
        "facialProfiles",
        "retargetProfiles",
        "rootMotionPolicies",
        "layerProfiles",
        "qualityBudgets",
        "representativeProfiles",
    ):
        ids.update(row["id"] for row in standard[section])
    for profile in standard["layerProfiles"]:
        ids.update(row["id"] for row in profile["layers"])
    for profile in standard["representativeProfiles"]:
        ids.update(row["id"] for row in profile["exceptions"])
    return ids


def _defined_manifest_ids(manifest: dict[str, Any]) -> set[str]:
    ids = {manifest["manifestId"]}
    for section in (
        "eventDefinitions",
        "clipCandidates",
        "skillPhases",
        "motionKeys",
        "requiredSets",
        "anatomyExceptions",
    ):
        ids.update(row["id"] for row in manifest[section])
    return ids


def _all_declared_ids(document: Any) -> list[str]:
    found: list[str] = []
    if isinstance(document, dict):
        value = document.get("id")
        if isinstance(value, str):
            found.append(value)
        for child in document.values():
            found.extend(_all_declared_ids(child))
    elif isinstance(document, list):
        for child in document:
            found.extend(_all_declared_ids(child))
    return found


def _validate_record_ordering(
    standard: dict[str, Any], manifest: dict[str, Any], issues: list[str]
) -> None:
    for section in (
        "skeletonProfiles",
        "bindPoses",
        "socketDefinitions",
        "facialProfiles",
        "retargetProfiles",
        "rootMotionPolicies",
        "layerProfiles",
        "qualityBudgets",
        "representativeProfiles",
    ):
        ids = [row["id"] for row in standard[section]]
        if ids != sorted(ids, key=lambda value: value.encode("utf-8")):
            issues.append(f"NonDeterministicOrdering: standard.{section}")
    for section in (
        "eventDefinitions",
        "clipCandidates",
        "motionKeys",
        "requiredSets",
        "anatomyExceptions",
    ):
        ids = [row["id"] for row in manifest[section]]
        if ids != sorted(ids, key=lambda value: value.encode("utf-8")):
            issues.append(f"NonDeterministicOrdering: manifest.{section}")
    profile_ids = [
        row["representativeProfileId"] for row in manifest["representativeCoverage"]
    ]
    if profile_ids != sorted(profile_ids, key=lambda value: value.encode("utf-8")):
        issues.append("NonDeterministicOrdering: manifest.representativeCoverage")


def _validate_skeletons(standard: dict[str, Any], issues: list[str]) -> None:
    bind_ids = {row["id"] for row in standard["bindPoses"]}
    socket_names = {row["name"] for row in standard["socketDefinitions"]}
    for profile in standard["skeletonProfiles"]:
        profile_id = profile["id"]
        bones = profile["bones"]
        names = [bone["name"] for bone in bones]
        if len(names) != len(set(names)):
            issues.append(f"DuplicateBone: {profile_id}")
            continue
        by_name = {bone["name"]: bone for bone in bones}
        roots = [bone for bone in bones if bone["parent"] is None]
        if len(roots) != 1 or roots[0]["name"] != "root" or roots[0]["deform"]:
            issues.append(f"MalformedRoot: {profile_id}")
        motion_root = by_name.get("motion_root")
        if (
            motion_root is None
            or motion_root["parent"] != "root"
            or motion_root["deform"]
        ):
            issues.append(f"MalformedMotionRoot: {profile_id}")
        for bone in bones:
            parent = bone["parent"]
            if parent is not None and parent not in by_name:
                issues.append(
                    f"MissingBoneParent: {profile_id}.{bone['name']} -> {parent}"
                )
        for name in names:
            visited: set[str] = set()
            cursor: str | None = name
            while cursor is not None:
                if cursor in visited:
                    issues.append(f"CyclicSkeleton: {profile_id}.{name}")
                    break
                visited.add(cursor)
                parent_bone = by_name.get(cursor)
                cursor = parent_bone["parent"] if parent_bone else None
        if profile["bindPoseId"] not in bind_ids:
            issues.append(f"MissingBindPose: {profile_id}.{profile['bindPoseId']}")
        unknown_sockets = set(profile["requiredSocketNames"]) - socket_names
        if unknown_sockets:
            issues.append(
                f"MissingSocketDefinition: {profile_id}: {sorted(unknown_sockets)}"
            )


def _validate_standard_references(
    repo_root: Path,
    standard: dict[str, Any],
    manifest: dict[str, Any],
    issues: list[str],
) -> None:
    skeletons = {row["id"]: row for row in standard["skeletonProfiles"]}
    binds = {row["id"]: row for row in standard["bindPoses"]}
    faces = {row["id"]: row for row in standard["facialProfiles"]}
    retargets = {row["id"]: row for row in standard["retargetProfiles"]}
    layers = {row["id"]: row for row in standard["layerProfiles"]}
    budgets = {row["id"]: row for row in standard["qualityBudgets"]}
    required_sets = {row["id"]: row for row in manifest["requiredSets"]}
    references = {
        "skeletonProfileId": skeletons,
        "bindPoseId": binds,
        "facialProfileId": faces,
        "retargetProfileId": retargets,
        "layerProfileId": layers,
        "budgetProfileId": budgets,
        "requiredMotionSetId": required_sets,
    }
    representatives = standard["representativeProfiles"]
    actual = {row["id"]: row["subjectKind"] for row in representatives}
    if actual != EXPECTED_REPRESENTATIVES:
        issues.append(f"RepresentativeMismatch: {actual!r}")
    if {row["subjectKind"] for row in standard["layerProfiles"]} != {
        "champion",
        "npc",
        "beast",
        "monster",
    }:
        issues.append(
            "LayerCoverageMismatch: champion, npc, beast, and monster required"
        )
    for representative in representatives:
        representative_id = representative["id"]
        subject_kind = representative["subjectKind"]
        for field, valid_ids in references.items():
            value = representative[field]
            if value not in valid_ids:
                issues.append(
                    f"InvalidReference: {representative_id}.{field} -> {value}"
                )
        if any(representative[field] not in references[field] for field in references):
            continue
        skeleton = skeletons[representative["skeletonProfileId"]]
        bind = binds[representative["bindPoseId"]]
        face = faces[representative["facialProfileId"]]
        retarget = retargets[representative["retargetProfileId"]]
        layer = layers[representative["layerProfileId"]]
        budget = budgets[representative["budgetProfileId"]]
        required_set = required_sets[representative["requiredMotionSetId"]]
        compatibility = {
            "skeletonProfileId": subject_kind in skeleton["subjectKinds"],
            "bindPoseId": subject_kind in bind["subjectKinds"],
            "facialProfileId": subject_kind in face["subjectKinds"],
            "retargetProfileId": subject_kind in retarget["targetKinds"],
            "layerProfileId": subject_kind == layer["subjectKind"],
            "budgetProfileId": subject_kind in budget["subjectKinds"],
            "requiredMotionSetId": subject_kind in required_set["subjectKinds"],
        }
        for field, compatible in compatibility.items():
            if not compatible:
                issues.append(f"IncompatibleProfile: {representative_id}.{field}")
        override_reason = representative["bindPoseOverrideReason"]
        if skeleton["bindPoseId"] != representative["bindPoseId"]:
            if not override_reason:
                issues.append(f"UndocumentedBindOverride: {representative_id}")
        elif override_reason is not None:
            issues.append(f"UnnecessaryBindOverride: {representative_id}")
        if retarget["sourceBindPoseId"] != representative["bindPoseId"]:
            issues.append(f"RetargetBindMismatch: {representative_id}")
        if (
            layer["maximumSimultaneousLayers"]
            > budget["animation"]["maximumRuntimeLayers"]
        ):
            issues.append(f"LayerBudgetMismatch: {representative_id}")
        source = repo_root / representative["sourcePath"]
        if not source.is_file():
            issues.append(
                f"MissingRepresentativeSource: {representative_id} -> {representative['sourcePath']}"
            )
        if representative["skeletonSignature"] is None and representative[
            "admissionState"
        ] not in {
            "blocked_pending_cleanup",
            "blocked_missing_required_motion",
        }:
            issues.append(f"UnsignedAdmittedSkeleton: {representative_id}")


def _validate_budgets(standard: dict[str, Any], issues: list[str]) -> None:
    budgets = {row["id"]: row for row in standard["qualityBudgets"]}
    if set(budgets) != set(EXPECTED_BUDGETS):
        issues.append(f"BudgetSetMismatch: {sorted(budgets)}")
    for budget_id, expected in EXPECTED_BUDGETS.items():
        budget = budgets.get(budget_id)
        if budget is None:
            continue
        actual = (
            budget["topology"]["maximumLod0Triangles"],
            budget["skinning"]["maximumDeformingBones"],
            budget["animation"]["maximumResidentClipCount"],
            budget["animation"]["maximumCompressedMemoryMiB"],
            budget["animation"]["maximumRuntimeLayers"],
        )
        if actual != expected:
            issues.append(f"BudgetDrift: {budget_id} expected {expected}, got {actual}")
        if budget["skinning"]["maximumInfluencesPerVertex"] != 4:
            issues.append(f"InfluenceBudgetDrift: {budget_id}")
        if (
            budget["skinning"]["maximumAnimatedTransforms"]
            < budget["skinning"]["maximumDeformingBones"]
        ):
            issues.append(f"ImpossibleSkinningBudget: {budget_id}")
        if (
            budget["animation"]["minimumSampleRateHz"]
            > budget["animation"]["maximumSampleRateHz"]
        ):
            issues.append(f"ImpossibleSampleRateBudget: {budget_id}")
        contacts = budget["contacts"]
        if (
            contacts["maximumPlantedHorizontalDriftMeters"] > 0.02
            or contacts["maximumPlantedVerticalErrorMeters"] > 0.01
            or contacts["maximumLoopPositionErrorMeters"] > 0.01
            or contacts["maximumLoopRotationErrorDegrees"] > 1
            or contacts["maximumStrideErrorPercent"] > 2
        ):
            issues.append(f"ContactBudgetTooLoose: {budget_id}")


def _validate_layers(standard: dict[str, Any], issues: list[str]) -> None:
    skeletons = standard["skeletonProfiles"]
    for profile in standard["layerProfiles"]:
        profile_id = profile["id"]
        priorities = [layer["priority"] for layer in profile["layers"]]
        if len(priorities) != len(set(priorities)):
            issues.append(f"DuplicateLayerPriority: {profile_id}")
        if profile["maximumSimultaneousLayers"] > len(profile["layers"]):
            issues.append(f"ImpossibleLayerCount: {profile_id}")
        compatible_skeletons = [
            skeleton
            for skeleton in skeletons
            if profile["subjectKind"] in skeleton["subjectKinds"]
        ]
        for layer in profile["layers"]:
            for mask_path in layer["maskPaths"]:
                segments = mask_path.split("/")
                path_matches = False
                for skeleton in compatible_skeletons:
                    bones = {bone["name"]: bone for bone in skeleton["bones"]}
                    if all(segment in bones for segment in segments) and all(
                        bones[segments[index]]["parent"] == segments[index - 1]
                        for index in range(1, len(segments))
                    ):
                        path_matches = True
                        break
                if not path_matches:
                    issues.append(
                        f"InvalidLayerMaskPath: {profile_id}.{layer['id']} -> {mask_path}"
                    )


def _validate_manifest(
    repo_root: Path,
    standard: dict[str, Any],
    manifest: dict[str, Any],
    issues: list[str],
) -> dict[str, int]:
    event_names = [row["eventName"] for row in manifest["eventDefinitions"]]
    if len(event_names) != len(set(event_names)):
        issues.append("DuplicateEventName: eventDefinitions")
    event_name_set = set(event_names)
    for event in manifest["eventDefinitions"]:
        field_rows = event["payloadFields"]
        field_names = [row["name"] for row in field_rows]
        if len(field_names) != len(set(field_names)):
            issues.append(f"DuplicateEventPayloadField: {event['id']}")
        fields = {row["name"] for row in field_rows if row["required"]}
        if not COMMON_EVENT_FIELDS.issubset(fields):
            issues.append(f"MissingCommonEventPayload: {event['id']}")
        types_by_name = {row["name"]: row["type"] for row in field_rows}
        for field_name, expected_type in COMMON_EVENT_FIELD_TYPES.items():
            if types_by_name.get(field_name) != expected_type:
                issues.append(
                    f"InvalidCommonEventPayloadType: {event['id']}.{field_name}"
                )

    phases = sorted(manifest["skillPhases"], key=lambda row: row["order"])
    phase_names = tuple(row["phase"] for row in phases)
    if phase_names != EXPECTED_PHASES or [row["order"] for row in phases] != list(
        range(len(EXPECTED_PHASES))
    ):
        issues.append(f"SkillPhaseMismatch: {phase_names!r}")
    for phase in phases:
        for field in ("entryEvent", "exitEvent"):
            if phase[field] not in event_name_set:
                issues.append(
                    f"InvalidPhaseEvent: {phase['id']}.{field} -> {phase[field]}"
                )

    root_policy_ids = {row["id"] for row in standard["rootMotionPolicies"]}
    root_categories = {row["category"] for row in standard["rootMotionPolicies"]}
    if root_categories != EXPECTED_ROOT_CATEGORIES:
        issues.append(f"RootPolicyCategoryMismatch: {sorted(root_categories)}")

    motion_rows = manifest["motionKeys"]
    motions_by_key = {row["key"]: row for row in motion_rows}
    motion_keys = [row["key"] for row in motion_rows]
    motion_key_set = set(motion_keys)
    if len(motion_keys) != len(motion_key_set):
        issues.append("DuplicateMotionKey: motionKeys")
    for motion in motion_rows:
        if motion["defaultRootPolicyId"] not in root_policy_ids:
            issues.append(
                f"InvalidRootPolicy: {motion['key']} -> {motion['defaultRootPolicyId']}"
            )
        unknown_events = set(motion["requiredEventNames"]) - event_name_set
        if unknown_events:
            issues.append(
                f"InvalidEventReference: {motion['key']} -> {sorted(unknown_events)}"
            )
        fallback = motion["fallbackKey"]
        if fallback is not None and fallback not in motion_key_set:
            issues.append(f"InvalidFallback: {motion['key']} -> {fallback}")
        elif fallback is not None and not set(motion["applicableKinds"]).issubset(
            motions_by_key[fallback]["applicableKinds"]
        ):
            issues.append(f"InapplicableFallback: {motion['key']} -> {fallback}")

    undefined_required = 0
    required_sets = {row["id"]: row for row in manifest["requiredSets"]}
    expected_skill_keys = {f"skill.{phase}" for phase in EXPECTED_PHASES}
    for required_set in manifest["requiredSets"]:
        keys = (
            required_set["requiredMotionKeys"] + required_set["conditionalMotionKeys"]
        )
        undefined = set(keys) - motion_key_set
        undefined_required += len(undefined)
        if undefined:
            issues.append(
                f"UndefinedRequiredMotion: {required_set['id']} -> {sorted(undefined)}"
            )
        if len(keys) != len(set(keys)):
            issues.append(f"DuplicateSetMotion: {required_set['id']}")
        if required_set[
            "skillPhasePolicy"
        ] == "all_declared_phases" and not expected_skill_keys.issubset(keys):
            issues.append(f"IncompleteSkillPhaseSet: {required_set['id']}")
        for key in set(keys) & motion_key_set:
            if not set(required_set["subjectKinds"]).issubset(
                motions_by_key[key]["applicableKinds"]
            ):
                issues.append(
                    f"InapplicableRequiredMotion: {required_set['id']} -> {key}"
                )

    standard_profiles = {row["id"]: row for row in standard["representativeProfiles"]}
    profile_ids = set(standard_profiles)
    skeleton_ids = {row["id"] for row in standard["skeletonProfiles"]}
    clip_ids = {row["id"] for row in manifest["clipCandidates"]}
    clips_by_id = {row["id"]: row for row in manifest["clipCandidates"]}
    for clip in manifest["clipCandidates"]:
        owner_profile_id = clip["representativeProfileId"]
        if owner_profile_id not in profile_ids:
            issues.append(
                f"InvalidClipRepresentative: {clip['id']} -> {owner_profile_id}"
            )
        if clip["skeletonProfileId"] not in skeleton_ids:
            issues.append(
                f"InvalidClipSkeleton: {clip['id']} -> {clip['skeletonProfileId']}"
            )
        if clip["motionKey"] not in motion_key_set:
            issues.append(f"InvalidClipMotion: {clip['id']} -> {clip['motionKey']}")
        if not (repo_root / clip["sourcePath"]).is_file():
            issues.append(f"MissingClipSource: {clip['id']} -> {clip['sourcePath']}")
        if clip["qualificationState"] == "qualified" and (
            clip["skeletonSignature"] is None or clip["clipSignature"] is None
        ):
            issues.append(f"UnsignedQualifiedClip: {clip['id']}")
        if (
            owner_profile_id in standard_profiles
            and clip["skeletonProfileId"]
            != standard_profiles[owner_profile_id]["skeletonProfileId"]
        ):
            issues.append(f"ClipSkeletonMismatch: {clip['id']}")

    coverage_rows = manifest["representativeCoverage"]
    coverage_by_profile = {row["representativeProfileId"]: row for row in coverage_rows}
    if (
        len(coverage_by_profile) != len(coverage_rows)
        or set(coverage_by_profile) != profile_ids
    ):
        issues.append(
            "RepresentativeCoverageMismatch: coverage must be exactly one row per representative"
        )

    unclassified = 0
    invalid_references = 0
    for profile_id, coverage in coverage_by_profile.items():
        if (
            profile_id in standard_profiles
            and coverage["requiredSetId"]
            != standard_profiles[profile_id]["requiredMotionSetId"]
        ):
            issues.append(
                f"RepresentativeSetMismatch: {profile_id} -> {coverage['requiredSetId']}"
            )
        required_set = required_sets.get(coverage["requiredSetId"])
        if required_set is None:
            issues.append(
                f"InvalidCoverageSet: {profile_id} -> {coverage['requiredSetId']}"
            )
            invalid_references += 1
            continue
        requirements = coverage["requirements"]
        requirement_keys = [row["motionKey"] for row in requirements]
        if len(requirement_keys) != len(set(requirement_keys)):
            issues.append(f"DuplicateCoverageMotion: {profile_id}")
        required = set(required_set["requiredMotionKeys"])
        covered = set(requirement_keys)
        missing = required - covered
        unclassified += len(missing)
        if missing:
            issues.append(f"UnclassifiedRequirement: {profile_id} -> {sorted(missing)}")
        for requirement in requirements:
            key = requirement["motionKey"]
            if key not in motion_key_set:
                issues.append(f"InvalidCoverageMotion: {profile_id} -> {key}")
                invalid_references += 1
            clip_id = requirement["clipId"]
            if clip_id is not None and clip_id not in clip_ids:
                issues.append(f"InvalidCoverageClip: {profile_id} -> {clip_id}")
                invalid_references += 1
            if requirement["status"] == "available_source_candidate":
                if clip_id is None:
                    issues.append(f"AvailableCoverageWithoutClip: {profile_id}.{key}")
                elif (
                    clip_id in clips_by_id
                    and clips_by_id[clip_id]["representativeProfileId"] != profile_id
                ):
                    issues.append(f"CrossRepresentativeClip: {profile_id}.{key}")
            elif clip_id is not None:
                issues.append(f"UnavailableCoverageWithClip: {profile_id}.{key}")
            if (
                key not in required
                and requirement["status"] != "blocked_owner_authorization"
            ):
                issues.append(f"UnscopedCoverageMotion: {profile_id} -> {key}")

    slag_set = required_sets.get("rmc_set_slagwhistle_source_bounded_v001")
    slag_coverage = coverage_by_profile.get("rmc_representative_beast_slagwhistle_v001")
    if slag_set is None or set(slag_set["requiredMotionKeys"]) != SLAGWHISTLE_SET:
        issues.append("SlagwhistleAuthorizationDrift: required six-key set changed")
    if slag_coverage is None or slag_coverage["clipCeiling"] != 6:
        issues.append("SlagwhistleAuthorizationDrift: clip ceiling must be six")
    elif {
        row["motionKey"]
        for row in slag_coverage["requirements"]
        if row["status"] == "blocked_owner_authorization"
    } != SLAGWHISTLE_BLOCKED:
        issues.append("SlagwhistleAuthorizationDrift: blocked motions changed")
    if slag_coverage is not None:
        slag_requirements = {
            row["motionKey"]: row for row in slag_coverage["requirements"]
        }
        if any(
            slag_requirements.get(key, {}).get("status")
            not in {"available_source_candidate", "required_missing"}
            for key in SLAGWHISTLE_SET
        ):
            issues.append(
                "SlagwhistleAuthorizationDrift: authorized slot status changed"
            )
    slag_clips = [
        clip
        for clip in manifest["clipCandidates"]
        if clip["representativeProfileId"]
        == "rmc_representative_beast_slagwhistle_v001"
    ]
    if len(slag_clips) > 6 or any(
        clip["motionKey"] not in SLAGWHISTLE_SET for clip in slag_clips
    ):
        issues.append(
            "SlagwhistleAuthorizationDrift: clip candidates exceed authorization"
        )
    if any("burrow" in key for key in motion_key_set):
        issues.append("SlagwhistleAuthorizationDrift: burrow motion is not authorized")

    return {
        "undefinedRequiredMotionKeyCount": undefined_required,
        "unclassifiedRepresentativeRequirementCount": unclassified,
        "duplicateIdCount": 0,
        "invalidReferenceCount": invalid_references,
        "representativeCount": len(coverage_rows),
        "skillPhaseCount": len(phases),
    }


def _validate_binding_template(template: dict[str, Any], issues: list[str]) -> None:
    if template.get("templateId") != "rmc_template_asset_binding_v001":
        issues.append("TemplateIdentityMismatch: templateId")
    if template.get("templateVersion") != 1:
        issues.append("TemplateIdentityMismatch: templateVersion")
    if template.get("standardId") != "rmc_standard_rig_motion_v001":
        issues.append("TemplateIdentityMismatch: standardId")
    if template.get("manifestId") != "rmc_manifest_required_motion_v001":
        issues.append("TemplateIdentityMismatch: manifestId")
    variants = template.get("variants")
    if not isinstance(variants, list):
        issues.append("TemplateShapeMismatch: variants")
        return
    by_name = {
        row.get("variant"): row.get("binding")
        for row in variants
        if isinstance(row, dict)
    }
    expected_variants = {"humanoid_champion", "humanoid_npc", "nonhumanoid"}
    if set(by_name) != expected_variants:
        issues.append(
            "TemplateVariantMismatch: expected champion, npc, and nonhumanoid"
        )
        return
    required_fields = {
        "bindingId",
        "representativeProfileId",
        "assetId",
        "subjectKind",
        "source",
        "skeletonProfileId",
        "skeletonSignature",
        "bindPoseId",
        "bindPoseOverrideReason",
        "retargetProfileId",
        "facialProfileId",
        "layerProfileId",
        "budgetProfileId",
        "requiredMotionSetId",
        "clipBindings",
        "exceptions",
        "evidence",
    }
    for variant_name, binding in by_name.items():
        if not isinstance(binding, dict) or not required_fields.issubset(binding):
            issues.append(f"TemplateShapeMismatch: {variant_name}.binding")
            continue
        clip_bindings = binding["clipBindings"]
        if not isinstance(clip_bindings, list):
            issues.append(f"TemplateShapeMismatch: {variant_name}.clipBindings")
            continue
        for clip in clip_bindings:
            for event in clip.get("events", []):
                if "staticPayload" not in event:
                    issues.append(
                        f"TemplateEventMismatch: {variant_name} missing staticPayload"
                    )
                forbidden = {"payload", "actionSequence", "normalizedTime"} & set(event)
                if forbidden:
                    issues.append(
                        f"TemplateEventMismatch: {variant_name} authors runtime fields"
                    )


def validate_contracts(
    repo_root: Path,
    standard: dict[str, Any] | None = None,
    manifest: dict[str, Any] | None = None,
    template: dict[str, Any] | None = None,
) -> dict[str, int]:
    standard = load_json(repo_root / STANDARD_PATH) if standard is None else standard
    manifest = load_json(repo_root / MANIFEST_PATH) if manifest is None else manifest
    template = load_json(repo_root / TEMPLATE_PATH) if template is None else template
    issues: list[str] = []

    issues.extend(_schema_errors(load_json(repo_root / STANDARD_SCHEMA_PATH), standard))
    issues.extend(_schema_errors(load_json(repo_root / MANIFEST_SCHEMA_PATH), manifest))
    if issues:
        raise RigMotionValidationError(issues)

    if standard["motionManifestId"] != manifest["manifestId"]:
        issues.append("ManifestIdentityMismatch: standard motionManifestId")
    if manifest["standardId"] != standard["standardId"]:
        issues.append("StandardIdentityMismatch: manifest standardId")

    declared_ids = [standard["standardId"], manifest["manifestId"]]
    declared_ids += _all_declared_ids(standard) + _all_declared_ids(manifest)
    duplicate_ids = len(declared_ids) - len(set(declared_ids))
    if duplicate_ids:
        issues.append(f"DuplicateId: {duplicate_ids} duplicate declarations")

    defined_standard = _defined_standard_ids(standard)
    defined_manifest = _defined_manifest_ids(manifest)
    if len(defined_standard | defined_manifest) != len(defined_standard) + len(
        defined_manifest
    ):
        issues.append("DuplicateId: standard and manifest definition collision")

    _validate_skeletons(standard, issues)
    _validate_record_ordering(standard, manifest, issues)
    _validate_standard_references(repo_root, standard, manifest, issues)
    _validate_budgets(standard, issues)
    _validate_layers(standard, issues)
    _validate_binding_template(template, issues)
    calculated = _validate_manifest(repo_root, standard, manifest, issues)
    calculated["duplicateIdCount"] = duplicate_ids
    calculated["invalidReferenceCount"] += sum(
        issue.startswith("InvalidReference:") for issue in issues
    )

    acceptance = manifest["acceptance"]
    for key, value in calculated.items():
        if acceptance.get(key) != value:
            issues.append(
                f"AcceptanceMismatch: {key} expected {value}, got {acceptance.get(key)!r}"
            )

    if issues:
        raise RigMotionValidationError(issues)
    return {
        "standardProfiles": len(standard["representativeProfiles"]),
        "skeletonProfiles": len(standard["skeletonProfiles"]),
        "qualityBudgets": len(standard["qualityBudgets"]),
        "eventDefinitions": len(manifest["eventDefinitions"]),
        "motionKeys": len(manifest["motionKeys"]),
        "requiredSets": len(manifest["requiredSets"]),
        "templateVariants": len(template["variants"]),
        "classifiedRequirements": sum(
            len(row["requirements"]) for row in manifest["representativeCoverage"]
        ),
        **calculated,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root", type=Path, default=Path(__file__).resolve().parents[3]
    )
    args = parser.parse_args()
    evidence = validate_contracts(args.repo_root.resolve())
    print("PASS: rig and required-motion contracts validate")
    print(json.dumps(evidence, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
