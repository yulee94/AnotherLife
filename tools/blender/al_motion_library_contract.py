#!/usr/bin/env python3
"""Fail-closed contracts for the representative Blender motion library."""

from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path
from typing import Any

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SOURCE_PLAN_PATH = Path(
    "unity/ArtSource/MotionLibrary/al_motion_library_source.v1.json"
)
CATALOG_PATH = Path("unity/ArtSource/MotionLibrary/al_motion_library_catalog.v1.json")
REPEATABILITY_PATH = Path(
    "unity/ArtSource/MotionLibrary/al_motion_library_repeatability.v1.json"
)
REQUIRED_MANIFEST_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_required_motion_manifest.json"
)
ID_PATTERN = re.compile(r"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
HASH_PATTERN = re.compile(r"^[0-9a-f]{64}$")
EXPECTED_REPRESENTATIVES = {
    "rmc_representative_champion_vanguard_v001",
    "rmc_representative_npc_covenant_sentinel_v001",
    "rmc_representative_beast_slagwhistle_v001",
}
BEAST_PROFILE = "rmc_representative_beast_slagwhistle_v001"
CHAMPION_PROFILE = "rmc_representative_champion_vanguard_v001"
NPC_PROFILE = "rmc_representative_npc_covenant_sentinel_v001"
BLOCKING_STATUSES = {"blocked_owner_authorization", "blocked_missing_source"}


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def stable_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=True,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _safe_path(repo_root: Path, relative: str | Path) -> Path:
    candidate = (repo_root / relative).resolve()
    try:
        candidate.relative_to(repo_root.resolve())
    except ValueError as error:
        raise ValueError(f"PathEscapesRepository: {relative}") from error
    return candidate


def _slug(motion_key: str) -> str:
    return re.sub(r"[^a-z0-9]+", "_", motion_key).strip("_")


def stable_clip_id(representative_profile_id: str, motion_key: str) -> str:
    family = "slagwhistle" if representative_profile_id == BEAST_PROFILE else "humanoid"
    return f"rmc_clip_{family}_{_slug(motion_key)}_v001"


def _required_sets(required_manifest: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {row["id"]: row for row in required_manifest["requiredSets"]}


def expected_binding_keys(
    source_plan: dict[str, Any],
    required_manifest: dict[str, Any],
) -> dict[str, set[str]]:
    required_sets = _required_sets(required_manifest)
    result: dict[str, set[str]] = {}
    for representative in source_plan["representatives"]:
        required_set = required_sets[representative["requiredSetId"]]
        keys = set(required_set["requiredMotionKeys"])
        if representative["coverageMode"] == "required_and_conditional":
            keys.update(required_set["conditionalMotionKeys"])
        result[representative["representativeProfileId"]] = keys
    return result


def resolve_motion_rule(source_plan: dict[str, Any], motion_key: str) -> dict[str, Any]:
    for rule in source_plan["motionRules"]:
        if re.fullmatch(rule["pattern"], motion_key):
            return rule
    raise KeyError(f"NoMotionRule: {motion_key}")


def _coverage_profile(
    required_manifest: dict[str, Any], representative_profile_id: str
) -> dict[str, Any]:
    return next(
        row
        for row in required_manifest["representativeCoverage"]
        if row["representativeProfileId"] == representative_profile_id
    )


def validate_source_plan(repo_root: Path, source_plan: dict[str, Any]) -> list[str]:
    issues: list[str] = []
    if source_plan.get("schemaVersion") != 1:
        issues.append("UnsupportedSchemaVersion: source plan")
    if source_plan.get("authorityState") != "bounded_engineering":
        issues.append(
            "AuthorityEscalation: motion library must remain bounded_engineering"
        )
    if not ID_PATTERN.fullmatch(source_plan.get("libraryId", "")):
        issues.append("InvalidStableId: libraryId")

    required_path = _safe_path(repo_root, source_plan["requiredManifestPath"])
    rig_manifest_path = _safe_path(repo_root, source_plan["rigManifestPath"])
    if not required_path.is_file():
        issues.append(f"MissingRequiredManifest: {required_path}")
        return sorted(set(issues))
    if not rig_manifest_path.is_file():
        issues.append(f"MissingRigManifest: {rig_manifest_path}")
    required_manifest = load_json(required_path)
    expected = expected_binding_keys(source_plan, required_manifest)

    representatives = source_plan.get("representatives", [])
    profile_ids = [row.get("representativeProfileId") for row in representatives]
    if set(profile_ids) != EXPECTED_REPRESENTATIVES or len(profile_ids) != 3:
        issues.append(f"RepresentativeCoverageMismatch: {profile_ids}")
    if len(profile_ids) != len(set(profile_ids)):
        issues.append("DuplicateRepresentativeProfileId")

    catalog_source_skeletons: dict[str, int] = {}
    for representative in representatives:
        profile_id = representative["representativeProfileId"]
        skeleton_id = representative["skeletonProfileId"]
        if representative["catalogSource"]:
            catalog_source_skeletons[skeleton_id] = (
                catalog_source_skeletons.get(skeleton_id, 0) + 1
            )
        for field in ("sourceBlendPath", "armatureObject", "sourceRightsState"):
            if not str(representative.get(field, "")).strip():
                issues.append(f"MissingSourceField: {profile_id}.{field}")
        for field in (
            "sourceBlendPath",
            "outputBlendPath",
            "outputFbxPath",
            "sidecarPath",
        ):
            try:
                path = _safe_path(repo_root, representative[field])
            except ValueError as error:
                issues.append(str(error))
                continue
            if field == "sourceBlendPath" and not path.is_file():
                issues.append(f"MissingSourceBlend: {profile_id}: {path}")
        for evidence in representative.get("licensingEvidence", []):
            if not _safe_path(repo_root, evidence).is_file():
                issues.append(f"MissingLicensingEvidence: {profile_id}: {evidence}")
        if not representative.get("knownRestrictions"):
            issues.append(f"MissingKnownRestrictions: {profile_id}")
        if not representative.get("contactBones"):
            issues.append(f"MissingContactBones: {profile_id}")
        for motion_key in expected.get(profile_id, set()):
            try:
                resolve_motion_rule(source_plan, motion_key)
            except KeyError as error:
                issues.append(str(error))

    if catalog_source_skeletons != {
        "rmc_skeleton_humanoid_shared_v001": 1,
        "rmc_skeleton_nonhumanoid_grounded_v001": 1,
    }:
        issues.append(f"CatalogSourceSkeletonMismatch: {catalog_source_skeletons}")

    beast = next(
        (
            row
            for row in representatives
            if row.get("representativeProfileId") == BEAST_PROFILE
        ),
        None,
    )
    if beast is not None:
        coverage = _coverage_profile(required_manifest, BEAST_PROFILE)
        declared_blocked = {
            row["motionKey"]
            for row in coverage["requirements"]
            if row["status"] in BLOCKING_STATUSES
        }
        if set(beast.get("explainedBlockedMotionKeys", [])) != declared_blocked:
            issues.append(
                "SourceBoundedBlockMismatch: "
                f"{sorted(beast.get('explainedBlockedMotionKeys', []))} != {sorted(declared_blocked)}"
            )
        if (
            beast["coverageMode"] != "required_only"
            or len(expected[BEAST_PROFILE]) != 6
        ):
            issues.append(
                "SourceBoundedMotionCountMismatch: Slagwhistle must remain exactly six"
            )

    event_names = {row["eventName"] for row in required_manifest["eventDefinitions"]}
    for template_id, events in source_plan.get("eventTemplates", {}).items():
        prior = -1.0
        for event in events:
            normalized = event.get("normalizedTime", -1.0)
            if event.get("eventName") not in event_names:
                issues.append(
                    f"UnknownEventName: {template_id}.{event.get('eventName')}"
                )
            if not 0.0 <= normalized <= 1.0 or normalized < prior:
                issues.append(f"InvalidEventOrder: {template_id}")
            prior = normalized

    thresholds = source_plan.get("cleanupThresholds", {})
    for field in (
        "maximumLoopPositionErrorMeters",
        "maximumLoopRotationErrorDegrees",
        "maximumContactDriftMeters",
        "maximumTransitionPositionDeltaMeters",
        "maximumTransitionRotationDeltaDegrees",
    ):
        if not isinstance(thresholds.get(field), (int, float)) or thresholds[field] < 0:
            issues.append(f"InvalidCleanupThreshold: {field}")
    return sorted(set(issues))


def _catalog_signature(catalog: dict[str, Any]) -> str:
    payload = {
        key: value for key, value in catalog.items() if key != "contentSignature"
    }
    return sha256_bytes(stable_json_bytes(payload))


def _expected_required_events(required_manifest: dict[str, Any]) -> dict[str, set[str]]:
    return {
        row["key"]: set(row["requiredEventNames"])
        for row in required_manifest["motionKeys"]
    }


def validate_built_catalog(
    repo_root: Path,
    source_plan: dict[str, Any],
    required_manifest: dict[str, Any],
    catalog: dict[str, Any],
) -> list[str]:
    issues = validate_source_plan(repo_root, source_plan)
    if catalog.get("schemaVersion") != 1:
        issues.append("UnsupportedSchemaVersion: catalog")
    if catalog.get("authorityState") != "bounded_engineering":
        issues.append("AuthorityEscalation: catalog")
    expected_source_hash = sha256_file(_safe_path(repo_root, SOURCE_PLAN_PATH))
    if catalog.get("sourcePlanSha256") != expected_source_hash:
        issues.append("SourcePlanHashMismatch")
    if catalog.get("contentSignature") != _catalog_signature(catalog):
        issues.append("CatalogContentSignatureMismatch")

    expected = expected_binding_keys(source_plan, required_manifest)
    expected_pairs = {
        (representative, motion_key)
        for representative, motion_keys in expected.items()
        for motion_key in motion_keys
    }
    bindings = catalog.get("bindings", [])
    binding_pairs = {
        (row.get("representativeProfileId"), row.get("motionKey")) for row in bindings
    }
    for pair in sorted(expected_pairs - binding_pairs):
        issues.append(f"MissingBinding: {pair[0]}:{pair[1]}")
    for pair in sorted(binding_pairs - expected_pairs):
        issues.append(f"UnexpectedBinding: {pair[0]}:{pair[1]}")
    if len(bindings) != len(binding_pairs):
        issues.append("DuplicateBinding")

    clips = catalog.get("clips", [])
    clips_by_id = {row.get("id"): row for row in clips}
    if len(clips_by_id) != len(clips):
        issues.append("DuplicateClipId")
    required_events = _expected_required_events(required_manifest)
    valid_event_names = {
        row["eventName"] for row in required_manifest["eventDefinitions"]
    }

    for binding in bindings:
        profile_id = binding.get("representativeProfileId", "")
        motion_key = binding.get("motionKey", "")
        expected_id = stable_clip_id(profile_id, motion_key)
        if binding.get("clipId") != expected_id:
            issues.append(f"UnstableClipBinding: {profile_id}:{motion_key}")
        if binding.get("qualificationState") != "qualified_engineering":
            issues.append(f"UnqualifiedBinding: {profile_id}:{motion_key}")
        clip = clips_by_id.get(expected_id)
        if clip is None:
            issues.append(f"MissingClip: {expected_id}")
            continue
        if clip.get("motionKey") != motion_key:
            issues.append(f"ClipMotionKeyMismatch: {expected_id}")

    for clip in clips:
        clip_id = clip.get("id", "")
        if not ID_PATTERN.fullmatch(clip_id):
            issues.append(f"InvalidStableId: {clip_id}")
        frame_count = clip.get("frameCount", 0)
        if not isinstance(frame_count, int) or frame_count < 2:
            issues.append(f"InvalidFrameCount: {clip_id}")
            continue
        if clip.get("sampleRateHz") != source_plan.get("sampleRateHz"):
            issues.append(f"SampleRateMismatch: {clip_id}")
        if clip.get("rootTreatment") not in {
            "in_place_motor_owned",
            "vertical_root_visual_horizontal_motor",
        }:
            issues.append(f"InvalidRootTreatment: {clip_id}")
        motion_definition = next(
            (
                row
                for row in required_manifest["motionKeys"]
                if row["key"] == clip.get("motionKey")
            ),
            None,
        )
        if motion_definition is not None:
            loop_policy = motion_definition.get("loopPolicy")
            if loop_policy == "must_loop" and not clip.get("loop"):
                issues.append(f"LoopPolicyMismatch: {clip_id}")
            if loop_policy == "must_not_loop" and clip.get("loop"):
                issues.append(f"LoopPolicyMismatch: {clip_id}")
            expected_style = resolve_motion_rule(source_plan, clip["motionKey"])[
                "style"
            ]
            if clip.get("generatorStyle") != expected_style:
                issues.append(
                    f"GeneratorStyleMismatch: {clip_id}:{clip.get('generatorStyle')}!={expected_style}"
                )
        if not clip.get("supportedSkeletonProfileIds"):
            issues.append(f"MissingSkeletonSupport: {clip_id}")
        if not clip.get("knownRestrictions"):
            issues.append(f"MissingKnownRestrictions: {clip_id}")
        if not HASH_PATTERN.fullmatch(clip.get("clipSignature", "")):
            issues.append(f"InvalidClipSignature: {clip_id}")

        events = clip.get("events", [])
        event_names = {event.get("eventName") for event in events}
        unknown_events = event_names - valid_event_names
        if unknown_events:
            issues.append(f"UnknownEvent: {clip_id}:{sorted(unknown_events)}")
        missing_events = (
            required_events.get(clip.get("motionKey", ""), set()) - event_names
        )
        if missing_events:
            issues.append(f"MissingRequiredEvent: {clip_id}:{sorted(missing_events)}")
        prior = -1
        for ordinal, event in enumerate(events):
            frame = event.get("frame", -1)
            if (
                event.get("eventOrdinal") != ordinal
                or not 1 <= frame <= frame_count
                or frame < prior
            ):
                issues.append(f"InvalidEventOrder: {clip_id}")
            prior = frame
        for window in clip.get("hitboxWindows", []):
            if (
                not 1
                <= window.get("openFrame", -1)
                < window.get("closeFrame", -1)
                <= frame_count
            ):
                issues.append(
                    f"InvalidHitboxWindow: {clip_id}:{window.get('windowId')}"
                )

    for coverage in catalog.get("coverage", []):
        profile_id = coverage.get("representativeProfileId")
        expected_keys = expected.get(profile_id, set())
        if set(coverage.get("expectedMotionKeys", [])) != expected_keys:
            issues.append(f"CoverageExpectationMismatch: {profile_id}")
        if set(coverage.get("boundMotionKeys", [])) != expected_keys:
            issues.append(f"UnexplainedCatalogGap: {profile_id}")
        declared = set(coverage.get("explainedBlocked", []))
        if profile_id == BEAST_PROFILE:
            source_beast = next(
                row
                for row in source_plan["representatives"]
                if row["representativeProfileId"] == BEAST_PROFILE
            )
            if declared != set(source_beast["explainedBlockedMotionKeys"]):
                issues.append(f"UnexplainedCatalogGap: {profile_id}:blocked")
        elif declared:
            issues.append(f"UnexpectedBlockedMotion: {profile_id}")

    coverage_profiles = {
        row.get("representativeProfileId") for row in catalog.get("coverage", [])
    }
    if coverage_profiles != EXPECTED_REPRESENTATIVES:
        issues.append(f"CoverageProfileMismatch: {sorted(coverage_profiles)}")

    overlap = expected.get(CHAMPION_PROFILE, set()) & expected.get(NPC_PROFILE, set())
    binding_lookup = {
        (row["representativeProfileId"], row["motionKey"]): row["clipId"]
        for row in bindings
    }
    for motion_key in overlap:
        if binding_lookup.get((CHAMPION_PROFILE, motion_key)) != binding_lookup.get(
            (NPC_PROFILE, motion_key)
        ):
            issues.append(f"HumanoidClipNotReused: {motion_key}")
    return sorted(set(issues))


def validate_artifacts(repo_root: Path, catalog: dict[str, Any]) -> list[str]:
    issues: list[str] = []
    expected_by_rep: dict[str, set[str]] = {}
    for binding in catalog.get("bindings", []):
        expected_by_rep.setdefault(binding["representativeProfileId"], set()).add(
            binding["motionKey"]
        )
    thresholds = catalog.get("cleanupThresholds", {})

    for asset in catalog.get("assets", []):
        asset_id = asset.get("id", "")
        blend_path = _safe_path(repo_root, asset["blendPath"])
        fbx_path = _safe_path(repo_root, asset["fbxPath"])
        sidecar_path = _safe_path(repo_root, asset["sidecarPath"])
        for label, path, minimum in (
            ("Blend", blend_path, 1024),
            ("Fbx", fbx_path, 1024),
            ("Sidecar", sidecar_path, 128),
        ):
            if not path.is_file() or path.stat().st_size < minimum:
                issues.append(f"MissingOrEmpty{label}: {asset_id}:{path}")
        if not sidecar_path.is_file():
            continue
        sidecar = load_json(sidecar_path)
        if sidecar.get("assetId") != asset_id:
            issues.append(f"SidecarAssetMismatch: {asset_id}")
        if blend_path.is_file() and sidecar.get("blendSha256") != sha256_file(
            blend_path
        ):
            issues.append(f"BlendHashMismatch: {asset_id}")
        if fbx_path.is_file() and sidecar.get("fbxSha256") != sha256_file(fbx_path):
            issues.append(f"FbxHashMismatch: {asset_id}")
        for field in ("skeletonSignature", "actionSignature"):
            if not HASH_PATTERN.fullmatch(sidecar.get(field, "")):
                issues.append(f"Invalid{field}: {asset_id}")
        action_keys = {row.get("motionKey") for row in sidecar.get("actions", [])}
        expected_keys = expected_by_rep.get(asset["representativeProfileId"], set())
        if not expected_keys.issubset(action_keys):
            issues.append(f"ActionCoverageMismatch: {asset_id}")
        for action in sidecar.get("actions", []):
            motion_key = action.get("motionKey", "")
            metrics = action.get("measuredCleanup", {})
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
                if (
                    not isinstance(value, (int, float))
                    or value < 0
                    or value > thresholds[maximum]
                ):
                    issues.append(
                        f"CleanupMetricExceeded: {asset_id}:{motion_key}:{measured}={value}"
                    )
            if action.get("loop") and metrics.get("poseContinuity") != "closed":
                issues.append(f"LoopNotClosed: {asset_id}:{motion_key}")
            if metrics.get("finiteTransforms") is not True:
                issues.append(f"NonFiniteTransform: {asset_id}:{motion_key}")
    return sorted(set(issues))


def validate_repeatability(repo_root: Path, catalog: dict[str, Any]) -> list[str]:
    issues: list[str] = []
    receipt_path = _safe_path(repo_root, REPEATABILITY_PATH)
    if not receipt_path.is_file():
        return [f"MissingRepeatabilityReceipt: {receipt_path}"]
    receipt = load_json(receipt_path)
    catalog_path = _safe_path(repo_root, CATALOG_PATH)
    if receipt.get("catalogSha256") != sha256_file(catalog_path):
        issues.append("RepeatabilityCatalogHashMismatch")
    if receipt.get("semanticSignatureRunOne") != receipt.get("semanticSignatureRunTwo"):
        issues.append("SemanticRepeatabilityMismatch")
    if receipt.get("status") != "passed":
        issues.append("RepeatabilityStatusNotPassed")
    current = {
        asset["id"]: {
            "blendSha256": sha256_file(_safe_path(repo_root, asset["blendPath"])),
            "fbxSha256": sha256_file(_safe_path(repo_root, asset["fbxPath"])),
            "sidecarSha256": sha256_file(_safe_path(repo_root, asset["sidecarPath"])),
        }
        for asset in catalog.get("assets", [])
        if all(
            _safe_path(repo_root, asset[field]).is_file()
            for field in ("blendPath", "fbxPath", "sidecarPath")
        )
    }
    if receipt.get("artifacts") != current:
        issues.append("RepeatabilityArtifactHashMismatch")
    return sorted(set(issues))
