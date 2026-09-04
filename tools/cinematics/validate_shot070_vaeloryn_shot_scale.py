#!/usr/bin/env python3
"""Fail-closed contract validator for Shot070 Vaeloryn first-run shot scale-out."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path, PurePosixPath
from typing import Any


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_PACKET_ROOT = (
    REPOSITORY_ROOT / "unity/Docs/Cinematics/Shot070VaelorynShotScaleV001"
)
DEFAULT_MANIFEST = DEFAULT_PACKET_ROOT / "shot070_vaeloryn_shot_scale_manifest_v001.json"
REJECTED_SOURCE_SHA256 = (
    "5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270"
)
LOCKED_BLEND_RELATIVE = (
    "unity/ArtSource/Cinematics/Shot070VaelorynSourceV002/"
    "shot070_vaeloryn_motion_source_v002.blend"
)
LOCKED_BLEND_SHA256 = "10bf9f96380632c983b523172913de8aa31b3187b785bd0b35b23757c7681b89"
LOCKED_BLEND_BYTES = 18966857
LOCKED_LANDSCAPE_RELATIVE = (
    "unity/Docs/Cinematics/Shot070VaelorynSourceV002/"
    "shot070_vaeloryn_motion_review_v002.mp4"
)
LOCKED_LANDSCAPE_SHA256 = (
    "0f7b66dc3fd6450405cec9cbf5840ba82fd1589ab5fbe73148b1381527169122"
)
LOCKED_LANDSCAPE_BYTES = 246640
LOCKED_CONTACT_RELATIVE = (
    "unity/Docs/Cinematics/Shot070VaelorynSourceV002/"
    "shot070_vaeloryn_motion_contact_v002.png"
)
LOCKED_CONTACT_SHA256 = (
    "57c1112cc92934d8f6f75475a515b799f1f54cdde55cb6cbe67e5978cadf3eb7"
)
LOCKED_CONTACT_BYTES = 155113
LOCKED_SOURCE_PACKET_ID = "tdf_packet_vaeloryn_wish_dragon_shot070_source_v002"
FIRST_RUN_SHOT_IDS = (
    "Shot010",
    "Shot020",
    "Shot030",
    "Shot040",
    "Shot050",
    "Shot060",
    "Shot070",
    "Shot080",
)
ELIGIBLE_SHOT_IDS = {"Shot070"}
INELIGIBLE_SHOT_IDS = tuple(
    shot_id for shot_id in FIRST_RUN_SHOT_IDS if shot_id not in ELIGIBLE_SHOT_IDS
)
EXPECTED_ANIMATED_BONES = {"neck_01", "jaw", "wing_l_01", "wing_r_01", "tail_01"}
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")


class ValidationError(RuntimeError):
    """Raised when the shot-scale packet violates one or more hard gates."""

    def __init__(self, issues: list[str]):
        self.issues = sorted(set(issues))
        super().__init__("\n".join(self.issues))


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _safe_path(root: Path, value: Any, issues: list[str]) -> Path | None:
    if not isinstance(value, str) or not value or "\\" in value:
        issues.append(f"UnsafeArtifactPath: {value}")
        return None
    parsed = PurePosixPath(value)
    if parsed.is_absolute() or ".." in parsed.parts or ":" in parsed.parts[0]:
        issues.append(f"UnsafeArtifactPath: {value}")
        return None
    path = (root / Path(*parsed.parts)).resolve()
    try:
        path.relative_to(root.resolve())
    except ValueError:
        issues.append(f"UnsafeArtifactPath: {value}")
        return None
    return path


def _validate_record(root: Path, record: Any, issues: list[str]) -> None:
    if not isinstance(record, dict):
        issues.append("ArtifactRecordMissing")
        return
    path = _safe_path(root, record.get("path"), issues)
    expected_hash = record.get("sha256")
    expected_bytes = record.get("bytes")
    if not isinstance(expected_hash, str) or not SHA256_PATTERN.fullmatch(expected_hash):
        issues.append(f"ArtifactHashInvalid: {record.get('path')}")
    if not isinstance(expected_bytes, int) or expected_bytes <= 0:
        issues.append(f"ArtifactByteLengthInvalid: {record.get('path')}")
    if path is None:
        return
    if not path.is_file():
        issues.append(f"ArtifactMissing: {record.get('path')}")
        return
    if path.stat().st_size != expected_bytes:
        issues.append(f"ArtifactByteLengthMismatch: {record.get('path')}")
    if _sha256(path) != expected_hash:
        issues.append(f"ArtifactHashMismatch: {record.get('path')}")


def _validate_reuse(manifest: dict[str, Any], issues: list[str]) -> list[str]:
    rows = manifest.get("firstRunReuse")
    if not isinstance(rows, list) or [row.get("shotId") for row in rows] != list(
        FIRST_RUN_SHOT_IDS
    ):
        issues.append("FirstRunReuseLedgerIncomplete")
        return []
    eligible: list[str] = []
    for row in rows:
        if not isinstance(row, dict):
            issues.append("FirstRunReuseLedgerIncomplete")
            continue
        shot_id = row.get("shotId")
        is_eligible = row.get("eligible") is True
        bound = row.get("boundCandidate")
        if shot_id in INELIGIBLE_SHOT_IDS and (
            is_eligible or bound is not None
        ):
            issues.append("IneligibleShotReuse")
        if shot_id in ELIGIBLE_SHOT_IDS:
            if not is_eligible or bound != LOCKED_SOURCE_PACKET_ID:
                issues.append("EligibleShotMissing")
            else:
                eligible.append(shot_id)
    if "Shot070" not in eligible:
        issues.append("EligibleShotMissing")
    return eligible


def _validate_remaining_shots(
    root: Path, manifest: dict[str, Any], issues: list[str]
) -> int:
    remaining = manifest.get("remainingShots")
    if not isinstance(remaining, list) or len(remaining) != 1:
        issues.append("RemainingShotSetMismatch")
        return 0
    shot = remaining[0]
    if not isinstance(shot, dict):
        issues.append("RemainingShotSetMismatch")
        return 0
    if shot.get("newPixelGeneration") is not False:
        issues.append("LockedSourceMutation")
    if (
        shot.get("shotId") != "Shot070"
        or shot.get("beatId") != "CTMA-BEAT-07"
        or shot.get("clipId") != "AL_FR_MOTION_SRC_070_EIGHTFOLD_WISH_V001"
        or shot.get("aspect") != "16:9"
        or shot.get("width") != 960
        or shot.get("height") != 540
        or shot.get("fps") != 24
        or shot.get("frameCount") != 168
        or shot.get("durationSeconds") != 7.0
        or shot.get("codec") != "h264"
        or shot.get("genuineArticulation") is not True
        or shot.get("stillImageMotionSubstitute") is not False
        or shot.get("derivedFromLockedV002Action") is not True
        or shot.get("croppedFromLandscape") is not False
        or not EXPECTED_ANIMATED_BONES.issubset(set(shot.get("animatedBones") or []))
    ):
        issues.append("MotionProofFailed")
    _validate_record(root, shot.get("file"), issues)
    _validate_record(root, shot.get("contactSheet"), issues)
    return 1


def validate_manifest(root: Path, manifest: dict[str, Any]) -> dict[str, Any]:
    root = root.resolve()
    issues: list[str] = []

    if manifest.get("schemaVersion") != 1 or manifest.get("packetId") != (
        "tdf_packet_vaeloryn_wish_dragon_shot_scale_v001"
    ):
        issues.append("ManifestIdentityMismatch")

    authority = manifest.get("authority") or {}
    if (
        authority.get("status") != "MOTION_REVIEW_CANDIDATE"
        or authority.get("runtimeAuthority") is not False
        or authority.get("gameplayAuthority") is not False
        or authority.get("finalCinematicApproval") is not False
        or authority.get("ownerVisualApprovalRequired") is not True
        or authority.get("runtimeVfxSeparate") is not True
    ):
        issues.append("AuthorityLeak")
    if authority.get("didNotRegenerateLockedSource") is not True:
        issues.append("LockedSourceMutation")

    cost = manifest.get("cost") or {}
    if (
        cost.get("incrementalUsd") != 0.0
        or cost.get("paidProviderCalls") != 0
        or cost.get("rechargeOrBillingMutation") is not False
    ):
        issues.append("ZeroSpendViolation")

    locked = manifest.get("lockedSource") or {}
    if locked.get("packetId") != LOCKED_SOURCE_PACKET_ID:
        issues.append("LockedSourceBindingMismatch")
    _validate_record(root, locked.get("blend"), issues)
    _validate_record(root, locked.get("landscapeMotion"), issues)

    rejected = manifest.get("rejectedSource") or {}
    if (
        rejected.get("sha256") != REJECTED_SOURCE_SHA256
        or rejected.get("inputEligible") is not False
        or rejected.get("usedAsInput") is not False
        or rejected.get("disposition") != "REJECTED_FOR_EXACT_SOURCE_FIDELITY"
    ):
        issues.append("RejectedSourceReuse")
    required_negative = {
        "duplicate_head",
        "fused_monolithic_mesh",
        "unskinned",
        "single_material",
        "identity_wing_emission_drift",
        "lineage_gap",
    }
    if not required_negative.issubset(set(rejected.get("negativeChecks") or [])):
        issues.append("RejectedSourceNegativeProofIncomplete")

    eligible = _validate_reuse(manifest, issues)
    remaining_count = _validate_remaining_shots(root, manifest, issues)

    if issues:
        raise ValidationError(issues)
    return {
        "status": "PASS",
        "packetId": manifest["packetId"],
        "remainingShotCount": remaining_count,
        "eligibleShotIds": eligible,
        "incrementalUsd": cost["incrementalUsd"],
        "runtimeAuthority": authority["runtimeAuthority"],
        "finalCinematicApproval": authority["finalCinematicApproval"],
    }


def validate_committed_packet(repo_root: Path | None = None) -> dict[str, Any]:
    repo_root = (repo_root or REPOSITORY_ROOT).resolve()
    manifest_path = (
        repo_root
        / "unity/Docs/Cinematics/Shot070VaelorynShotScaleV001/"
        "shot070_vaeloryn_shot_scale_manifest_v001.json"
    )
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    summary = validate_manifest(repo_root, manifest)
    issues: list[str] = []
    blend = repo_root / LOCKED_BLEND_RELATIVE
    landscape = repo_root / LOCKED_LANDSCAPE_RELATIVE
    if not blend.is_file():
        issues.append("LockedBlendMissing")
    elif blend.stat().st_size != LOCKED_BLEND_BYTES or _sha256(blend) != LOCKED_BLEND_SHA256:
        issues.append("LockedBlendHashMismatch")
    if not landscape.is_file():
        issues.append("LockedLandscapeMissing")
    elif (
        landscape.stat().st_size != LOCKED_LANDSCAPE_BYTES
        or _sha256(landscape) != LOCKED_LANDSCAPE_SHA256
    ):
        issues.append("LockedLandscapeHashMismatch")
    declared_blend = (manifest.get("lockedSource") or {}).get("blend") or {}
    declared_landscape = (manifest.get("lockedSource") or {}).get("landscapeMotion") or {}
    if declared_blend.get("path") != LOCKED_BLEND_RELATIVE:
        issues.append("LockedBlendPathMismatch")
    if declared_blend.get("sha256") != LOCKED_BLEND_SHA256:
        issues.append("LockedBlendHashMismatch")
    if declared_landscape.get("path") != LOCKED_LANDSCAPE_RELATIVE:
        issues.append("LockedLandscapePathMismatch")
    if declared_landscape.get("sha256") != LOCKED_LANDSCAPE_SHA256:
        issues.append("LockedLandscapeHashMismatch")
    remaining = (manifest.get("remainingShots") or [{}])[0]
    remaining_file = remaining.get("file") or {}
    remaining_contact = remaining.get("contactSheet") or {}
    if remaining_file.get("path") != LOCKED_LANDSCAPE_RELATIVE:
        issues.append("RemainingShotMustReuseLockedLandscape")
    if remaining_file.get("sha256") != LOCKED_LANDSCAPE_SHA256:
        issues.append("LockedLandscapeHashMismatch")
    if remaining_contact.get("path") != LOCKED_CONTACT_RELATIVE:
        issues.append("RemainingShotMustReuseLockedContact")
    if remaining_contact.get("sha256") != LOCKED_CONTACT_SHA256:
        issues.append("LockedContactHashMismatch")
    if issues:
        raise ValidationError(issues)
    return summary


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--root", type=Path, default=REPOSITORY_ROOT)
    args = parser.parse_args()
    try:
        if (
            args.manifest.resolve() == DEFAULT_MANIFEST.resolve()
            and args.root.resolve() == REPOSITORY_ROOT.resolve()
        ):
            summary = validate_committed_packet(args.root)
        else:
            summary = validate_manifest(
                args.root, json.loads(args.manifest.read_text(encoding="utf-8"))
            )
    except ValidationError as error:
        for issue in error.issues:
            print(f"ERROR: {issue}")
        return 1
    print(json.dumps(summary, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
