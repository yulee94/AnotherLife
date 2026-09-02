#!/usr/bin/env python3
"""Fail-closed Blender-to-Unity representative round-trip validation."""

from __future__ import annotations

import json
import re
from collections.abc import Iterable
from pathlib import Path
from typing import Any

BONE_NAME_PATTERN = re.compile(r"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
EXPECTED_REPRESENTATIVES = {
    "rmc_representative_champion_vanguard_v001",
    "rmc_representative_npc_covenant_sentinel_v001",
    "rmc_representative_beast_slagwhistle_v001",
}
VALID_ROOT_TREATMENTS = {
    "in_place_motor_owned",
    "vertical_root_visual_horizontal_motor",
}


class MotionRoundTripValidationError(RuntimeError):
    """Raised when round-trip source or Unity evidence fails closed."""

    def __init__(self, issues: Iterable[str]):
        self.issues = sorted(set(issues))
        super().__init__("\n".join(self.issues))


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _stable_clip_id(representative_profile_id: str, motion_key: str) -> str:
    family = (
        "slagwhistle"
        if representative_profile_id == "rmc_representative_beast_slagwhistle_v001"
        else "humanoid"
    )
    slug = re.sub(r"[^a-z0-9]+", "_", motion_key).strip("_")
    return f"rmc_clip_{family}_{slug}_v001"


def _record_issues(
    asset_id: str,
    asset: dict[str, Any],
    sidecar: dict[str, Any],
    receipt: dict[str, Any],
    budgets: dict[str, dict[str, Any]],
) -> list[str]:
    issues: list[str] = []
    records = sidecar.get("skeleton", {}).get("records", [])
    paths = {row.get("path") for row in records}
    names = [row.get("name") for row in records]

    invalid_names = sorted(
        str(name) for name in names if not isinstance(name, str) or not BONE_NAME_PATTERN.fullmatch(name)
    )
    if invalid_names:
        issues.append(f"InvalidBoneName: {asset_id}: {invalid_names}")

    roots = [row for row in records if row.get("parentPath") == ""]
    if len(roots) != 1 or roots[0].get("name") != "root":
        issues.append(f"MissingRequiredRoot: {asset_id}: root")
    if "motion_root" not in names:
        issues.append(f"MissingRequiredRoot: {asset_id}: motion_root")
    for row in records:
        parent_path = row.get("parentPath")
        if parent_path and parent_path not in paths:
            issues.append(
                f"InvalidBoneHierarchy: {asset_id}:{row.get('path')} -> {parent_path}"
            )

    missing_sockets = sorted(set(asset.get("sockets", {})) - set(names))
    if missing_sockets:
        issues.append(f"MissingRequiredSocket: {asset_id}: {missing_sockets}")

    budget = budgets.get(asset.get("budgetProfileId"))
    if budget is None:
        issues.append(f"MissingBudgetProfile: {asset_id}:{asset.get('budgetProfileId')}")
    else:
        preflight = sidecar.get("preflight", {})
        skinning = budget["skinning"]
        if preflight.get("maximumInfluencesPerVertex", 10**9) > skinning[
            "maximumInfluencesPerVertex"
        ]:
            issues.append(f"SkinInfluenceBudgetExceeded: {asset_id}")
        if preflight.get("deformingBones", 10**9) > skinning["maximumDeformingBones"]:
            issues.append(f"DeformingBoneBudgetExceeded: {asset_id}")
        if preflight.get("animatedTransforms", 10**9) > skinning[
            "maximumAnimatedTransforms"
        ]:
            issues.append(f"AnimatedTransformBudgetExceeded: {asset_id}")
        if preflight.get("unweightedVertices", 1) != 0:
            issues.append(f"UnweightedVertices: {asset_id}")

    export = receipt.get("export", {})
    if abs(float(export.get("globalScale", 0.0)) - 1.0) > 0.0001:
        issues.append(f"InvalidExportScale: {asset_id}")
    if export.get("axisForward") != "-Z" or export.get("axisUp") != "Y":
        issues.append(f"InvalidExportAxes: {asset_id}")
    if export.get("addLeafBones") is not False:
        issues.append(f"InvalidExportLeafBones: {asset_id}")
    if receipt.get("status") != "export_valid" or receipt.get("roundTrip", {}).get(
        "errors"
    ):
        issues.append(f"BlenderRoundTripFailure: {asset_id}")
    return issues


def _required_motion_issues(
    standard: dict[str, Any],
    required_manifest: dict[str, Any],
    motion_catalog: dict[str, Any],
) -> list[str]:
    issues: list[str] = []
    required_sets = {row["id"]: row for row in required_manifest.get("requiredSets", [])}
    coverage_by_id = {
        row.get("representativeProfileId"): row
        for row in motion_catalog.get("coverage", [])
    }
    bound_by_id: dict[str, set[str]] = {}
    for binding in motion_catalog.get("bindings", []):
        bound_by_id.setdefault(binding.get("representativeProfileId"), set()).add(
            binding.get("motionKey")
        )
    profiles = [
        row
        for row in standard.get("representativeProfiles", [])
        if row.get("id") in EXPECTED_REPRESENTATIVES
    ]
    if {row.get("id") for row in profiles} != EXPECTED_REPRESENTATIVES:
        issues.append("RepresentativeCoverageMismatch: standard")
    for profile in profiles:
        profile_id = profile.get("id", "")
        set_id = profile.get("requiredMotionSetId")
        required_set = required_sets.get(set_id)
        if required_set is None:
            issues.append(f"MissingRequiredMotionSet: {profile_id}:{set_id}")
            continue
        required_keys = set(required_set.get("requiredMotionKeys", []))
        bound = bound_by_id.get(profile_id, set())
        blocked = set((coverage_by_id.get(profile_id) or {}).get("explainedBlocked") or [])
        missing = sorted(key for key in required_keys - bound - blocked if key)
        if missing:
            issues.append(f"MissingRequiredMotion: {profile_id}: {missing}")
        skill_keys = sorted(
            key for key in required_keys if isinstance(key, str) and key.startswith("skill.")
        )
        missing_phases = [key for key in skill_keys if key not in bound]
        if missing_phases:
            issues.append(f"MissingRequiredSkillPhase: {profile_id}: {missing_phases}")
        coverage = coverage_by_id.get(profile_id) or {}
        expected = set(coverage.get("expectedMotionKeys") or [])
        unexplained = sorted(expected - bound - blocked)
        if unexplained or coverage.get("catalogGapCount") != len(unexplained):
            issues.append(f"UnexplainedCatalogGap: {profile_id}")
    return issues


def _catalog_issues(
    required_manifest: dict[str, Any], motion_catalog: dict[str, Any]
) -> list[str]:
    issues: list[str] = []
    clips = motion_catalog.get("clips", [])
    bindings = motion_catalog.get("bindings", [])
    clip_ids = [row.get("id") for row in clips]
    if len(clip_ids) != len(set(clip_ids)):
        issues.append("DuplicateCatalogId: clips")
    binding_pairs = [
        (row.get("representativeProfileId"), row.get("motionKey")) for row in bindings
    ]
    if len(binding_pairs) != len(set(binding_pairs)):
        issues.append("DuplicateCatalogBinding")
    for binding in bindings:
        expected = _stable_clip_id(
            binding.get("representativeProfileId", ""), binding.get("motionKey", "")
        )
        if binding.get("clipId") != expected:
            issues.append(
                "UnstableCatalogBinding: "
                f"{binding.get('representativeProfileId')}:{binding.get('motionKey')}"
            )

    required_events = {
        row["key"]: set(row["requiredEventNames"])
        for row in required_manifest.get("motionKeys", [])
    }
    thresholds = motion_catalog.get("cleanupThresholds", {})
    for clip in clips:
        clip_id = clip.get("id", "")
        motion_key = clip.get("motionKey", "")
        events = clip.get("events", [])
        event_names = {row.get("eventName") for row in events}
        missing = required_events.get(motion_key, set()) - event_names
        if missing:
            issues.append(f"MissingRequiredEvent: {clip_id}:{sorted(missing)}")
        prior_frame = -1
        for ordinal, event in enumerate(events):
            frame = event.get("frame", -1)
            if event.get("eventOrdinal") != ordinal or frame < prior_frame:
                issues.append(f"InvalidEventOrder: {clip_id}")
            prior_frame = frame
        for window in clip.get("hitboxWindows", []):
            open_frame = window.get("openFrame", -1)
            close_frame = window.get("closeFrame", -1)
            frame_count = clip.get("frameCount", 0)
            if not 1 <= open_frame < close_frame <= frame_count:
                issues.append(
                    f"InvalidHitboxWindow: {clip_id}:{window.get('windowId')}"
                )
            begins = [
                row
                for row in events
                if row.get("eventName") == "al.motion.hitbox.request_begin"
                and row.get("windowId") == window.get("windowId")
                and row.get("frame") == open_frame
            ]
            ends = [
                row
                for row in events
                if row.get("eventName") == "al.motion.hitbox.request_end"
                and row.get("windowId") == window.get("windowId")
                and row.get("frame") == close_frame
            ]
            if len(begins) != 1 or len(ends) != 1:
                issues.append(
                    f"InvalidHitboxWindowEvents: {clip_id}:{window.get('windowId')}"
                )
        if clip.get("rootTreatment") not in VALID_ROOT_TREATMENTS:
            issues.append(f"IncompatibleRootMotion: {clip_id}")
        metrics = clip.get("measuredCleanup", {})
        checks = (
            ("loopPositionErrorMeters", "maximumLoopPositionErrorMeters"),
            ("loopRotationErrorDegrees", "maximumLoopRotationErrorDegrees"),
            ("contactDriftMeters", "maximumContactDriftMeters"),
            (
                "transitionPositionDeltaMeters",
                "maximumTransitionPositionDeltaMeters",
            ),
            (
                "transitionRotationDeltaDegrees",
                "maximumTransitionRotationDeltaDegrees",
            ),
        )
        for measured, maximum in checks:
            value = metrics.get(measured)
            limit = thresholds.get(maximum)
            if (
                not isinstance(value, (int, float))
                or not isinstance(limit, (int, float))
                or value < 0
                or value > limit
            ):
                issues.append(f"MotionQualityBudgetExceeded: {clip_id}:{measured}")

    coverage = motion_catalog.get("coverage", [])
    if {row.get("representativeProfileId") for row in coverage} != EXPECTED_REPRESENTATIVES:
        issues.append("RepresentativeCoverageMismatch: motion catalog")
    return issues


def validate_source_artifacts(
    repo_root: Path,
    standard: dict[str, Any],
    required_manifest: dict[str, Any],
    rig_manifest: dict[str, Any],
    motion_catalog: dict[str, Any],
    sidecars: dict[str, dict[str, Any]],
    receipts: dict[str, dict[str, Any]],
) -> dict[str, int]:
    del repo_root
    issues = _catalog_issues(required_manifest, motion_catalog)
    issues.extend(_required_motion_issues(standard, required_manifest, motion_catalog))
    profiles = {
        row["id"]: row for row in standard.get("representativeProfiles", [])
    }
    skeletons = {row["id"] for row in standard.get("skeletonProfiles", [])}
    budgets = {row["id"]: row for row in standard.get("qualityBudgets", [])}
    assets = rig_manifest.get("assets", [])
    if {row.get("representativeProfileId") for row in assets} != EXPECTED_REPRESENTATIVES:
        issues.append("RepresentativeCoverageMismatch: rig manifest")

    for asset in assets:
        asset_id = asset.get("id", "")
        profile_id = asset.get("representativeProfileId", "")
        profile = profiles.get(profile_id)
        if profile is None:
            issues.append(f"MissingRepresentativeProfile: {asset_id}")
        if asset.get("skeletonProfileId") not in skeletons:
            issues.append(
                f"UnsupportedSkeletonProfile: {asset_id}:{asset.get('skeletonProfileId')}"
            )
        elif profile is not None and asset.get("skeletonProfileId") != profile.get(
            "skeletonProfileId"
        ):
            issues.append(f"UnsupportedSkeletonProfile: {asset_id}:profile mismatch")
        sidecar = sidecars.get(asset_id)
        receipt = receipts.get(asset_id)
        if sidecar is None:
            issues.append(f"MissingRigSidecar: {asset_id}")
            continue
        if receipt is None:
            issues.append(f"MissingFbxReceipt: {asset_id}")
            continue
        issues.extend(_record_issues(asset_id, asset, sidecar, receipt, budgets))

    if issues:
        raise MotionRoundTripValidationError(issues)
    return {
        "representatives": len(assets),
        "clips": len(motion_catalog.get("clips", [])),
        "bindings": len(motion_catalog.get("bindings", [])),
        "catalogGaps": sum(
            row.get("catalogGapCount", 0) for row in motion_catalog.get("coverage", [])
        ),
        "skillPhases": len(required_manifest.get("skillPhases", [])),
    }


def validate_unity_report(
    standard: dict[str, Any], report: dict[str, Any]
) -> dict[str, int]:
    issues: list[str] = []
    if report.get("schemaVersion") != 1 or report.get("pipelineId") != (
        "rmc_pipeline_unity_roundtrip_acceptance_v001"
    ):
        issues.append("ReportIdentityMismatch")
    if report.get("status") != "passed":
        issues.append("UnityRoundTripStatusNotPassed")
    if not str(report.get("scenePath", "")).endswith(".unity"):
        issues.append("MissingAcceptanceScene")

    profiles = {
        row["id"]: row for row in standard.get("representativeProfiles", [])
    }
    skeletons = {row["id"]: row for row in standard.get("skeletonProfiles", [])}
    budgets = {row["id"]: row for row in standard.get("qualityBudgets", [])}
    rows = report.get("representatives", [])
    row_ids = [row.get("representativeProfileId") for row in rows]
    if set(row_ids) != EXPECTED_REPRESENTATIVES or len(row_ids) != 3:
        issues.append(f"RepresentativeCoverageMismatch: {row_ids}")

    for row in rows:
        profile_id = row.get("representativeProfileId", "")
        profile = profiles.get(profile_id)
        prefix = profile_id or "<missing>"
        if profile is None:
            issues.append(f"MissingRepresentativeProfile: {prefix}")
            continue
        if row.get("subjectKind") != profile.get("subjectKind"):
            issues.append(f"SubjectKindMismatch: {prefix}")
        if row.get("skeletonProfileId") != profile.get("skeletonProfileId"):
            issues.append(f"UnsupportedSkeletonProfile: {prefix}")
        if row.get("budgetProfileId") != profile.get("budgetProfileId"):
            issues.append(f"BudgetProfileMismatch: {prefix}")
        if row.get("freshImport") is not True:
            issues.append(f"FreshImportMissing: {prefix}")

        rig = row.get("rig", {})
        animation = row.get("animation", {})
        runtime = row.get("runtime", {})
        skeleton = skeletons.get(profile["skeletonProfileId"], {})
        budget = budgets.get(profile["budgetProfileId"])
        if rig.get("avatarValid") is not True:
            issues.append(f"InvalidAvatar: {prefix}")
        expected_human = skeleton.get("classification") == "humanoid"
        if rig.get("isHuman") is not expected_human:
            issues.append(f"AvatarClassificationMismatch: {prefix}")
        if rig.get("rootCount") != 1 or rig.get("hasRoot") is not True:
            issues.append(f"MissingRequiredRoot: {prefix}:root")
        if rig.get("hasMotionRoot") is not True:
            issues.append(f"MissingRequiredRoot: {prefix}:motion_root")
        if rig.get("missingSockets"):
            issues.append(f"MissingRequiredSocket: {prefix}")
        if rig.get("invalidBoneNames"):
            issues.append(f"InvalidBoneName: {prefix}")
        if rig.get("invalidHierarchyCount") != 0:
            issues.append(f"InvalidBoneHierarchy: {prefix}")
        if abs(float(rig.get("uniformScale", 0.0)) - 1.0) > 0.0001:
            issues.append(f"InvalidImportScale: {prefix}")
        if abs(float(rig.get("axisErrorDegrees", 999.0))) > 0.1:
            issues.append(f"InvalidImportAxis: {prefix}")
        if float(rig.get("heightMeters", 0.0)) <= 0:
            issues.append(f"InvalidImportedBounds: {prefix}")

        if budget is None:
            issues.append(f"MissingBudgetProfile: {prefix}")
            continue
        skinning = budget["skinning"]
        animation_budget = budget["animation"]
        contacts = budget["contacts"]
        if rig.get("maximumInfluencesPerVertex", 10**9) > skinning[
            "maximumInfluencesPerVertex"
        ]:
            issues.append(f"SkinInfluenceBudgetExceeded: {prefix}")
        if rig.get("deformingBones", 10**9) > skinning["maximumDeformingBones"]:
            issues.append(f"DeformingBoneBudgetExceeded: {prefix}")
        if rig.get("animatedTransforms", 10**9) > skinning[
            "maximumAnimatedTransforms"
        ]:
            issues.append(f"AnimatedTransformBudgetExceeded: {prefix}")
        if rig.get("unweightedVertices", 1) != 0:
            issues.append(f"UnweightedVertices: {prefix}")
        if animation.get("residentClipCount", 10**9) > animation_budget[
            "maximumResidentClipCount"
        ]:
            issues.append(f"ResidentClipBudgetExceeded: {prefix}")
        if animation.get("compressedMemoryMiB", float("inf")) > animation_budget[
            "maximumCompressedMemoryMiB"
        ]:
            issues.append(f"AnimationMemoryBudgetExceeded: {prefix}")
        if animation.get("compression") != "Optimal":
            issues.append(f"AnimationCompressionMismatch: {prefix}")
        if animation.get("missingMotionKeys"):
            issues.append(f"MissingRequiredMotion: {prefix}")
        if animation.get("missingEvents"):
            issues.append(f"MissingRequiredEvent: {prefix}")
        if animation.get("duplicateEvents") != 0:
            issues.append(f"DuplicateEvent: {prefix}")
        if animation.get("invalidEventOrder") != 0:
            issues.append(f"InvalidEventOrder: {prefix}")
        if animation.get("invalidHitboxWindows") != 0:
            issues.append(f"InvalidHitboxWindow: {prefix}")
        if animation.get("droppedEvents") != 0:
            issues.append(f"DroppedEvent: {prefix}")
        if animation.get("incompatibleRootMotion") != 0:
            issues.append(f"IncompatibleRootMotion: {prefix}")
        if animation.get("trajectoryErrorMeters", float("inf")) > contacts[
            "maximumLoopPositionErrorMeters"
        ]:
            issues.append(f"TrajectoryConsistencyExceeded: {prefix}")
        if animation.get("footSlidingMeters", float("inf")) > contacts[
            "maximumPlantedHorizontalDriftMeters"
        ]:
            issues.append(f"FootSlidingExceeded: {prefix}")
        if animation.get("contactDriftMeters", float("inf")) > contacts[
            "maximumPlantedHorizontalDriftMeters"
        ]:
            issues.append(f"ContactDriftExceeded: {prefix}")
        if animation.get("transitionPositionDeltaMeters", float("inf")) > 0.03 or (
            animation.get("transitionRotationDeltaDegrees", float("inf")) > 6.0
        ):
            issues.append(f"TransitionDiscontinuity: {prefix}")

        runtime_checks = (
            ("controllerConfigured", "RuntimeControllerFailure"),
            ("graphValid", "RuntimeGraphFailure"),
            ("safePoseLoaded", "SafePoseFailure"),
            ("fallbackPassed", "FallbackFailure"),
            ("transitionPassed", "TransitionFailure"),
            ("recoveryPassed", "RecoveryFailure"),
            ("attachmentsPassed", "BrokenAttachment"),
        )
        for field, token in runtime_checks:
            if runtime.get(field) is not True:
                issues.append(f"{token}: {prefix}")
        if runtime.get("tPoseDetected") is not False:
            issues.append(f"TposeDetected: {prefix}")

    if issues:
        raise MotionRoundTripValidationError(issues)
    return {"representatives": len(rows), "acceptanceFailures": 0}


def load_repo_artifacts(repo_root: Path) -> tuple[
    dict[str, Any],
    dict[str, Any],
    dict[str, Any],
    dict[str, Any],
    dict[str, dict[str, Any]],
    dict[str, dict[str, Any]],
]:
    standard = load_json(
        repo_root / "unity/Assets/AL/StreamingAssets/GameData/al_rig_motion_standard.json"
    )
    required_manifest = load_json(
        repo_root
        / "unity/Assets/AL/StreamingAssets/GameData/al_required_motion_manifest.json"
    )
    rig_manifest = load_json(
        repo_root / "unity/ArtSource/RigPipeline/al_rig_cleanup_manifest.v1.json"
    )
    motion_catalog = load_json(
        repo_root / "unity/ArtSource/MotionLibrary/al_motion_library_catalog.v1.json"
    )
    sidecars = {
        asset["id"]: load_json(repo_root / asset["output"]["sidecarPath"])
        for asset in rig_manifest["assets"]
    }
    receipts = {
        asset["id"]: load_json(repo_root / asset["output"]["fbxReceiptPath"])
        for asset in rig_manifest["assets"]
    }
    return standard, required_manifest, rig_manifest, motion_catalog, sidecars, receipts


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    (
        standard,
        required_manifest,
        rig_manifest,
        motion_catalog,
        sidecars,
        receipts,
    ) = load_repo_artifacts(repo_root)
    evidence = validate_source_artifacts(
        repo_root,
        standard,
        required_manifest,
        rig_manifest,
        motion_catalog,
        sidecars,
        receipts,
    )
    print(json.dumps({"status": "passed", **evidence}, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
