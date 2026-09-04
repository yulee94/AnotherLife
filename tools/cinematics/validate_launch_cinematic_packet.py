#!/usr/bin/env python3
"""Fail-closed packaging gate for the first-run launch cinematic.

This packet does not assemble or ship a 60-second launch master. Shot070 V002 is
a MOTION_REVIEW_CANDIDATE only. Owner visual approval is still required. The
honest committed state is PACKAGING_BLOCKED_NO_APPROVED_MASTER with reduced-motion
static fallback through the existing launch controller.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path, PurePosixPath
from typing import Any

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
PACKET_DIR = REPOSITORY_ROOT / "unity/Docs/Cinematics/LaunchCinematicPacketV001"
DEFAULT_MANIFEST = PACKET_DIR / "launch_cinematic_packaging_manifest_v001.json"
RUNTIME_CATALOG_RELATIVE = (
    "unity/Assets/AL/StreamingAssets/GameData/al_launch_cinematic_runtime.v1.json"
)
SHOT070_MANIFEST_RELATIVE = (
    "unity/Docs/Cinematics/Shot070VaelorynSourceV002/shot070_vaeloryn_source_manifest_v002.json"
)
REJECTED_SOURCE_SHA256 = (
    "5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270"
)
SHOT070_REVIEW_SHA256 = (
    "0f7b66dc3fd6450405cec9cbf5840ba82fd1589ab5fbe73148b1381527169122"
)
PACKET_ID = "tdf_packet_launch_cinematic_packaging_v001"
BLOCKED_STATUS = "PACKAGING_BLOCKED_NO_APPROVED_MASTER"
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
LAUNCH_MP4_NAME = "launch_omen_01.mp4"


class ValidationError(RuntimeError):
    """Raised when launch-cinematic packaging violates one or more hard gates."""

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


def _packaged_launch_mp4s(root: Path) -> list[Path]:
    matches: list[Path] = []
    for base in (
        root / "unity/Assets/AL/StreamingAssets",
        root / "unity/Assets/StreamingAssets",
    ):
        if not base.exists():
            continue
        for path in base.rglob("*.mp4"):
            relative = path.relative_to(base).as_posix()
            if (
                path.name == LAUNCH_MP4_NAME
                or "LaunchCinematic" in relative.split("/")
                or "launch_cinematic" in relative.lower()
            ):
                matches.append(path)
    return matches


def _authority_leaked(authority: dict[str, Any]) -> bool:
    return (
        authority.get("status") != BLOCKED_STATUS
        or authority.get("runtimeAuthority") is not False
        or authority.get("gameplayAuthority") is not False
        or authority.get("finalCinematicApproval") is not False
        or authority.get("ownerVisualApprovalRequired") is not True
        or authority.get("approvedForProduction") is not False
        or authority.get("probeEvidenceApproved") is not False
    )


def validate_manifest(root: Path, manifest: dict[str, Any]) -> dict[str, Any]:
    root = root.resolve()
    issues: list[str] = []

    if manifest.get("schemaVersion") != 1 or manifest.get("packetId") != PACKET_ID:
        issues.append("ManifestIdentityMismatch")

    authority = manifest.get("authority") or {}
    if _authority_leaked(authority if isinstance(authority, dict) else {}):
        issues.append("AuthorityLeak")

    contract = manifest.get("encodeContract") or {}
    desktop = contract.get("desktop") or {}
    android = contract.get("android") or {}
    if (
        contract.get("durationSeconds") != 60.0
        or contract.get("fps") != 24
        or contract.get("frameCount") != 1440
        or desktop.get("width") != 1920
        or desktop.get("height") != 1080
        or desktop.get("codecProfile") != "h264-high"
        or desktop.get("maximumBytes") != 95000000
        or android.get("width") != 1280
        or android.get("height") != 720
        or android.get("codecProfile") != "h264-main"
        or android.get("maximumBytes") != 42000000
    ):
        issues.append("EncodeContractMismatch")

    forbidden = manifest.get("forbiddenSources") or {}
    if (
        forbidden.get("rejectedMeshyGlbSha256") != REJECTED_SOURCE_SHA256
        or forbidden.get("shot070ReviewMp4Sha256") != SHOT070_REVIEW_SHA256
    ):
        issues.append("RejectedSourceReuse")
    if forbidden.get("stillImageMotionSubstitute") is not False:
        issues.append("StillImageMotionForbidden")

    _validate_record(root, manifest.get("runtimeCatalog"), issues)

    encodes = manifest.get("encodes") or {}
    for platform_name, encode in (("desktop", encodes.get("desktop")), ("android", encodes.get("android"))):
        if encode is None:
            continue
        if not isinstance(encode, dict):
            issues.append(f"EncodeRecordInvalid: {platform_name}")
            continue
        digest = encode.get("sha256")
        duration = encode.get("durationSeconds")
        frames = encode.get("frameCount")
        if digest == SHOT070_REVIEW_SHA256 or duration == 7.0 or frames == 168:
            issues.append("Shot070IsNotLaunchMaster")
        if digest == REJECTED_SOURCE_SHA256:
            issues.append("RejectedSourceReuse")
        if encode.get("stillImageMotionSubstitute") is True:
            issues.append("StillImageMotionForbidden")
        if duration != 60.0 or frames != 1440:
            issues.append("LaunchMasterShapeMismatch")
        issues.append("PackagedLaunchMediaWhileBlocked")

    catalog_path = _safe_path(
        root,
        (manifest.get("runtimeCatalog") or {}).get("path") if isinstance(manifest.get("runtimeCatalog"), dict) else None,
        issues,
    )
    if catalog_path is not None and catalog_path.is_file():
        try:
            catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            issues.append("RuntimeCatalogUnreadable")
            catalog = None
        if isinstance(catalog, dict):
            if (
                catalog.get("approvedForProduction") is True
                or catalog.get("probeEvidenceApproved") is True
                or catalog.get("runtimeAuthority") is True
                or catalog.get("finalCinematicApproval") is True
                or catalog.get("reducedMotionFallbackOnly") is not True
                or catalog.get("authorityStatus") != BLOCKED_STATUS
            ):
                issues.append("UnapprovedPromotion")
            platforms = catalog.get("platforms") or []
            if not isinstance(platforms, list) or len(platforms) != 2:
                issues.append("RuntimeCatalogPlatformMismatch")
            else:
                names = {row.get("platform") for row in platforms if isinstance(row, dict)}
                if names != {"Desktop", "Android"}:
                    issues.append("RuntimeCatalogPlatformMismatch")
                for row in platforms:
                    if not isinstance(row, dict):
                        continue
                    if row.get("encodePresent") is True or row.get("byteLength") not in (0, None):
                        issues.append("UnapprovedPromotion")
                    if row.get("sha256"):
                        issues.append("UnapprovedPromotion")
                    if row.get("durationSeconds") != 60.0 or row.get("frameCount") != 1440:
                        issues.append("EncodeContractMismatch")

    packaged = _packaged_launch_mp4s(root)
    if packaged:
        issues.append("PackagedLaunchMediaWhileBlocked")

    fallback = manifest.get("reducedMotionFallback") or {}
    if (
        fallback.get("required") is not True
        or fallback.get("bootPresentation") != "static-fallback"
        or fallback.get("controllerBinding") != "LaunchCinematicPlaybackCoordinator"
    ):
        issues.append("ReducedMotionFallbackMissing")

    for evidence_key in ("windowsEvidence", "androidEvidence"):
        evidence = manifest.get(evidence_key) or {}
        if (
            evidence.get("presentationPath") != "static-fallback"
            or evidence.get("packagedLaunchMp4Count") != 0
            or evidence.get("decodeOfLaunchMaster") != "NOT_PERFORMED_NO_APPROVED_MASTER"
        ):
            issues.append(f"EvidenceOverclaim: {evidence_key}")

    shot070_path = root / SHOT070_MANIFEST_RELATIVE
    if shot070_path.is_file():
        try:
            shot070 = json.loads(shot070_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            issues.append("Shot070ManifestUnreadable")
        else:
            shot_authority = shot070.get("authority") or {}
            motion = shot070.get("motionProof") or {}
            motion_file = motion.get("file") or {}
            if (
                shot_authority.get("status") != "MOTION_REVIEW_CANDIDATE"
                or shot_authority.get("runtimeAuthority") is True
                or shot_authority.get("finalCinematicApproval") is True
            ):
                issues.append("Shot070IsNotLaunchMaster")
            if motion_file.get("sha256") == SHOT070_REVIEW_SHA256 and (
                motion.get("durationSeconds") != 7.0 or motion.get("frameCount") != 168
            ):
                issues.append("Shot070IsNotLaunchMaster")

    if issues:
        raise ValidationError(issues)
    return {
        "status": "PASS",
        "packetId": manifest.get("packetId"),
        "authorityStatus": authority.get("status"),
        "runtimeAuthority": False,
        "approvedForProduction": False,
        "packagedLaunchMp4Count": 0,
        "desktopEncode": None,
        "androidEncode": None,
    }


def package_encodes(
    root: Path,
    manifest: dict[str, Any],
    source_sha256: str | None = None,
) -> dict[str, Any]:
    issues: list[str] = []
    if source_sha256 == SHOT070_REVIEW_SHA256:
        issues.append("Shot070IsNotLaunchMaster")
    if source_sha256 == REJECTED_SOURCE_SHA256:
        issues.append("RejectedSourceReuse")
    authority = manifest.get("authority") or {}
    if authority.get("status") != "APPROVED_LAUNCH_MASTER" or not source_sha256:
        issues.append("NoApprovedMaster")
    if authority.get("stillImageMotionSubstitute") is True or (
        manifest.get("forbiddenSources") or {}
    ).get("stillImageMotionSubstitute") is True:
        issues.append("StillImageMotionForbidden")
    if issues:
        raise ValidationError(issues)
    raise ValidationError(["NoApprovedMaster"])


def validate_committed_packet(repo_root: Path | None = None) -> dict[str, Any]:
    repo_root = (repo_root or REPOSITORY_ROOT).resolve()
    manifest_path = repo_root / DEFAULT_MANIFEST.relative_to(REPOSITORY_ROOT)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    return validate_manifest(repo_root, manifest)


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
