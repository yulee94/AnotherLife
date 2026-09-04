#!/usr/bin/env python3
"""Fail-closed contract validator for the Shot070 Vaeloryn motion-source candidate."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path, PurePosixPath
from typing import Any

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_PACKET_ROOT = (
    REPOSITORY_ROOT / "unity/Docs/Cinematics/Shot070VaelorynSourceV002"
)
DEFAULT_MANIFEST = DEFAULT_PACKET_ROOT / "shot070_vaeloryn_source_manifest_v002.json"
REJECTED_SOURCE_SHA256 = (
    "5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270"
)
EXPECTED_APPROVED_2D = {
    "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001/ConceptSheets/55_vaeloryn_multiview_01_v001.png": (
        "b3453b1e23b6ab911fe33fb0820c05e2f9b5d9db0e34ef89875edad83a8f8b55"
    ),
    "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001/ConceptSheets/56_vaeloryn_multiview_02_v001.png": (
        "ccdb03cd2e4bc2547e95497e251bbd698d52b4c19a1b179b0707993709bd897d"
    ),
}
EXPECTED_CANDIDATE_INPUT = {
    "path": "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/Models/wish_dragon_vaeloryn/wish_dragon_vaeloryn_source_v001.fbx",
    "sha256": "80bcc74a2cf95cb2626437bba3d3ba805d6087f1498e64b1603cb256f43e68cb",
    "meshyTaskId": "01a05b2c-92c6-7329-939f-a538fdaa859b",
}
EXPECTED_SEMANTIC_REGIONS = {
    "body",
    "head",
    "jaw",
    "eye_l",
    "eye_r",
    "wing_arm_l",
    "wing_arm_r",
    "wing_membrane_l",
    "wing_membrane_r",
    "tail",
}
EXPECTED_BONES = {
    "root",
    "pelvis",
    "spine_01",
    "neck_01",
    "head",
    "jaw",
    "wing_l_01",
    "wing_l_02",
    "wing_r_01",
    "wing_r_02",
    "tail_01",
    "tail_02",
    "leg_fl_01",
    "leg_fr_01",
    "leg_bl_01",
    "leg_br_01",
}
EXPECTED_ANIMATED_BONES = {"neck_01", "jaw", "wing_l_01", "wing_r_01", "tail_01"}
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")


class ValidationError(RuntimeError):
    """Raised when a Shot070 source packet violates one or more hard gates."""

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


def validate_manifest(root: Path, manifest: dict[str, Any]) -> dict[str, Any]:
    root = root.resolve()
    issues: list[str] = []

    if manifest.get("schemaVersion") != 1 or manifest.get("packetId") != (
        "tdf_packet_vaeloryn_wish_dragon_shot070_source_v002"
    ):
        issues.append("ManifestIdentityMismatch")

    shot = manifest.get("shotBinding") or {}
    if (
        shot.get("beatId") != "CTMA-BEAT-07"
        or shot.get("shotId") != "Shot070"
        or shot.get("frameInterval") != [1080, 1248]
        or shot.get("localFrameCount") != 168
        or shot.get("fps") != 24
        or shot.get("durationSeconds") != 7.0
    ):
        issues.append("ShotBindingMismatch")

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

    cost = manifest.get("cost") or {}
    if (
        cost.get("incrementalUsd") != 0.0
        or cost.get("paidProviderCalls") != 0
        or cost.get("rechargeOrBillingMutation") is not False
    ):
        issues.append("ZeroSpendViolation")

    approved = manifest.get("approved2DSources")
    approved_map = {
        row.get("path"): row.get("sha256")
        for row in approved or []
        if isinstance(row, dict) and row.get("authority") == "APPROVED_2D"
    }
    if approved_map != EXPECTED_APPROVED_2D:
        issues.append("Approved2DSourceBindingMismatch")

    derivation = manifest.get("candidateDerivation") or {}
    if derivation.get("input") != EXPECTED_CANDIDATE_INPUT:
        issues.append("CandidateInputBindingMismatch")
    operations = derivation.get("operations")
    if not isinstance(operations, list) or len(operations) < 3:
        issues.append("CandidateDerivationMissing")
    candidate_files = derivation.get("candidateFiles")
    if not isinstance(candidate_files, list) or len(candidate_files) != 2:
        issues.append("CandidateArtifactSetMismatch")
    else:
        for record in candidate_files:
            _validate_record(root, record, issues)

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

    anatomy = manifest.get("anatomy") or {}
    if (
        anatomy.get("headCount") != 1
        or anatomy.get("legCount") != 4
        or anatomy.get("wingPairCount") != 1
        or anatomy.get("tailCount") != 1
    ):
        issues.append("AnatomyCountMismatch")
    semantic_regions = set(anatomy.get("semanticRegions") or [])
    topology = manifest.get("topology") or {}
    if (
        not EXPECTED_SEMANTIC_REGIONS.issubset(semantic_regions)
        or anatomy.get("semanticRegionCount", 0) < len(EXPECTED_SEMANTIC_REGIONS)
        or topology.get("meshObjectCount", 0) < 1
        or len(set(topology.get("materialSlots") or [])) < 4
        or topology.get("independentMaterialRegionCount", 0) < 4
    ):
        issues.append("SeparationContractFailed")
    if (
        not isinstance(topology.get("triangles"), int)
        or not 1 <= topology["triangles"] <= 75000
        or not isinstance(topology.get("vertices"), int)
        or topology["vertices"] <= 0
        or topology.get("uvLayerCount", 0) < 1
        or not isinstance(topology.get("nonManifoldEdgeCount"), int)
        or not 0 <= topology["nonManifoldEdgeCount"] <= 4
        or topology.get("boundaryEdgeCount") != 0
    ):
        issues.append("TopologyContractFailed")

    rig = manifest.get("rig") or {}
    if (
        rig.get("rigged") is not True
        or rig.get("armatureCount") != 1
        or not 16 <= rig.get("deformBoneCount", 0) <= 96
        or rig.get("maxVertexInfluences", 99) > 4
        or rig.get("unweightedVertexCount") != 0
        or not EXPECTED_BONES.issubset(set(rig.get("requiredBones") or []))
    ):
        issues.append("RigContractFailed")
    _validate_record(root, rig.get("report"), issues)

    motion = manifest.get("motionProof") or {}
    if (
        motion.get("codec") != "h264"
        or motion.get("width") != 960
        or motion.get("height") != 540
        or motion.get("fps") != 24
        or motion.get("frameCount") != 168
        or motion.get("durationSeconds") != 7.0
        or motion.get("genuineArticulation") is not True
        or motion.get("stillImageMotionSubstitute") is not False
        or not EXPECTED_ANIMATED_BONES.issubset(set(motion.get("animatedBones") or []))
    ):
        issues.append("MotionProofFailed")
    _validate_record(root, motion.get("file"), issues)
    _validate_record(root, motion.get("contactSheet"), issues)

    framings = manifest.get("framingProofs")
    expected_frames = {("16:9", 1920, 1080), ("9:16", 1080, 1920)}
    actual_frames = {
        (row.get("aspect"), row.get("width"), row.get("height"))
        for row in framings or []
        if isinstance(row, dict)
    }
    if actual_frames != expected_frames or len(framings or []) != 2:
        issues.append("FramingProofFailed")
    for framing in framings or []:
        if isinstance(framing, dict):
            _validate_record(root, framing.get("file"), issues)

    if issues:
        raise ValidationError(issues)
    return {
        "status": "PASS",
        "packetId": manifest["packetId"],
        "frameCount": motion["frameCount"],
        "framingProofs": len(framings),
        "incrementalUsd": cost["incrementalUsd"],
        "runtimeAuthority": authority["runtimeAuthority"],
    }


def validate_committed_packet(repo_root: Path | None = None) -> dict[str, Any]:
    repo_root = (repo_root or REPOSITORY_ROOT).resolve()
    manifest_path = (
        repo_root / "unity/Docs/Cinematics/Shot070VaelorynSourceV002/shot070_vaeloryn_source_manifest_v002.json"
    )
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    summary = validate_manifest(repo_root, manifest)
    issues: list[str] = []
    for relative, expected_hash in EXPECTED_APPROVED_2D.items():
        path = repo_root / relative
        if not path.is_file():
            issues.append(f"Approved2DMissing: {relative}")
        elif _sha256(path) != expected_hash:
            issues.append(f"Approved2DHashMismatch: {relative}")
    input_path = repo_root / EXPECTED_CANDIDATE_INPUT["path"]
    if not input_path.is_file():
        issues.append("CandidateInputMissing")
    elif _sha256(input_path) != EXPECTED_CANDIDATE_INPUT["sha256"]:
        issues.append("CandidateInputHashMismatch")
    if issues:
        raise ValidationError(issues)
    return summary


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--root", type=Path, default=REPOSITORY_ROOT)
    args = parser.parse_args()
    try:
        if args.manifest.resolve() == DEFAULT_MANIFEST.resolve() and args.root.resolve() == REPOSITORY_ROOT.resolve():
            summary = validate_committed_packet(args.root)
        else:
            summary = validate_manifest(args.root, json.loads(args.manifest.read_text(encoding="utf-8")))
    except ValidationError as error:
        for issue in error.issues:
            print(f"ERROR: {issue}")
        return 1
    print(json.dumps(summary, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
