#!/usr/bin/env python3
"""Validate the qualified source-only Fault-Crowned Colossus production slice."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
SLICE_DIR = (
    REPO_ROOT
    / "unity"
    / "Docs"
    / "Terrestrials"
    / "RealmCreatureProductionSourceV001"
    / "ProductionSlices"
    / "FaultCrownedColossusV001"
)
CINDERMAW_SLICE_DIR = (
    REPO_ROOT
    / "unity"
    / "Docs"
    / "Terrestrials"
    / "RealmCreatureProductionSourceV001"
    / "ProductionSlices"
    / "CindermawSalamanderV001"
)
DEFAULT_PLAN = SLICE_DIR / "fault_crowned_colossus_production_slice_plan_v001.json"
DEFAULT_SCHEMA = SLICE_DIR / "realm_creature_production_slice.schema.json"
DEFAULT_QUALIFICATION = SLICE_DIR / "fault_crowned_colossus_qualification_manifest_v001.json"
DEFAULT_CINDERMAW_PLAN = (
    CINDERMAW_SLICE_DIR / "cindermaw_salamander_production_slice_plan_v001.json"
)
DEFAULT_CINDERMAW_SCHEMA = CINDERMAW_SLICE_DIR / "realm_creature_production_slice.schema.json"
DEFAULT_CINDERMAW_QUALIFICATION = (
    CINDERMAW_SLICE_DIR / "cindermaw_salamander_qualification_manifest_v001.json"
)
SOURCE_MANIFEST = (
    REPO_ROOT
    / "unity"
    / "Docs"
    / "Terrestrials"
    / "RealmCreatureProductionSourceV001"
    / "realm_creature_3d_source_manifest_v001.json"
)
HASH_PATTERN = re.compile(r"^[0-9a-f]{64}$")
EXPECTED_LODS = ("LOD0", "LOD1", "LOD2")
EXPECTED_REQUIRED_MOTIONS = {
    "locomotion.walk",
    "locomotion.run",
    "attack.basic",
    "attack.special",
    "skill.anticipation",
}


def review_slug_from_plan(plan: dict[str, Any]) -> str:
    lod0 = str((plan.get("lodPolicy") or {}).get("levels", [{}])[0].get("object") or "")
    name = lod0[4:] if lod0.startswith("GEO_") else lod0
    if name.endswith("_LOD0"):
        name = name[:-5]
    return name


def expected_review_files(slug: str) -> dict[str, str]:
    return {
        "lod0_bind": f"{slug}_lod0_bind_v001.png",
        "lod1_bind": f"{slug}_lod1_bind_v001.png",
        "lod2_bind": f"{slug}_lod2_bind_v001.png",
        "locomotion_walk": f"{slug}_locomotion_walk_v001.png",
        "attack_basic": f"{slug}_attack_basic_v001.png",
        "attack_special": f"{slug}_attack_special_v001.png",
        "skill_anticipation": f"{slug}_skill_anticipation_v001.png",
    }


EXPECTED_REVIEW_FILES = expected_review_files("fault_crowned_colossus")


class SliceValidationError(RuntimeError):
    """Raised when callers require a qualified slice and validation fails."""

    def __init__(self, issues: list[str]):
        self.issues = sorted(set(issues))
        super().__init__("\n".join(self.issues))


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _safe_path(repo_root: Path, relative: str) -> Path:
    candidate = (repo_root / relative).resolve()
    try:
        candidate.relative_to(repo_root.resolve())
    except ValueError as error:
        raise SliceValidationError([f"PathEscapesRepository:{relative}"]) from error
    return candidate


def _png_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"NotPng:{path}")
    return struct.unpack(">II", header[16:24])


def _schema_issues(plan: dict[str, Any], schema_path: Path) -> list[str]:
    try:
        from jsonschema import Draft202012Validator
    except ImportError:
        return ["DependencyMissing:jsonschema"]
    schema = load_json(schema_path)
    issues = []
    for error in Draft202012Validator(schema).iter_errors(plan):
        location = ".".join(str(part) for part in error.absolute_path) or "<root>"
        issues.append(f"SchemaViolation:{location}:{error.message}")
    return issues


def _verify_file(
    repo_root: Path,
    record: dict[str, Any],
    label: str,
    issues: list[str],
    *,
    require_png_dimensions: bool = False,
) -> Path | None:
    path_value = record.get("path")
    if not isinstance(path_value, str) or not path_value:
        issues.append(f"MissingPath:{label}")
        return None
    try:
        path = _safe_path(repo_root, path_value)
    except SliceValidationError as error:
        issues.extend(error.issues)
        return None
    if not path.is_file():
        issues.append(f"MissingFile:{label}:{path_value}")
        return None
    actual_bytes = path.stat().st_size
    if actual_bytes != record.get("bytes"):
        issues.append(f"ByteCountMismatch:{label}:{actual_bytes}")
    expected_hash = record.get("sha256")
    if not isinstance(expected_hash, str) or not HASH_PATTERN.fullmatch(expected_hash):
        issues.append(f"InvalidHash:{label}")
    elif sha256_file(path) != expected_hash:
        issues.append(f"HashMismatch:{label}")
    if require_png_dimensions:
        try:
            actual_dimensions = list(_png_dimensions(path))
        except ValueError as error:
            issues.append(str(error))
        else:
            if actual_dimensions != record.get("dimensions"):
                issues.append(f"ImageDimensionsMismatch:{label}:{actual_dimensions}")
    return path


def _validate_authority_and_sources(
    repo_root: Path,
    plan: dict[str, Any],
    issues: list[str],
) -> None:
    authority = plan.get("authority") or {}
    for field in ("runtimeAuthority", "gameplayAuthority", "spawnActivation"):
        if authority.get(field) is not False:
            issues.append(f"AuthorityLeak:{field}")
    if authority.get("sourceQualificationOnly") is not True:
        issues.append("AuthorityLeak:sourceQualificationOnly")
    if authority.get("runtimeVfxSeparate") is not True:
        issues.append("VfxBoundaryViolation")

    outputs = plan.get("outputs") or {}
    for field in ("blend", "fbx", "dccReport", "qualificationManifest", "reviewDirectory"):
        value = outputs.get(field)
        if not isinstance(value, str):
            continue
        normalized = value.replace("\\", "/").casefold()
        if normalized.startswith("unity/assets/"):
            issues.append(f"RuntimePathForbidden:outputs.{field}")

    _verify_file(repo_root, plan.get("source") or {}, "selectedSource", issues)
    approved = plan.get("approved2D") or {}
    _verify_file(
        repo_root,
        approved.get("source") or {},
        "approved2D.source",
        issues,
        require_png_dimensions=True,
    )
    manifest_path = approved.get("manifestPath")
    if not isinstance(manifest_path, str):
        issues.append("MissingApprovalManifest")
    else:
        try:
            manifest = load_json(_safe_path(repo_root, manifest_path))
        except (FileNotFoundError, json.JSONDecodeError, SliceValidationError) as error:
            issues.append(f"InvalidApprovalManifest:{error}")
        else:
            entry = next(
                (
                    row
                    for row in manifest.get("entries", [])
                    if row.get("id") == approved.get("entryId")
                ),
                None,
            )
            if entry is None or entry.get("status") != "APPROVED_2D":
                issues.append("Approved2DBindingMissing")
            elif not any(
                row.get("path") == approved["source"].get("path")
                and row.get("sha256") == approved["source"].get("sha256")
                for row in entry.get("sources", [])
            ):
                issues.append("Approved2DSourceMismatch")

    try:
        source_manifest = load_json(SOURCE_MANIFEST)
    except (FileNotFoundError, json.JSONDecodeError) as error:
        issues.append(f"InvalidSourceManifest:{error}")
    else:
        model = next(
            (
                row
                for row in source_manifest.get("models", [])
                if row.get("modelId") == authority.get("modelId")
            ),
            None,
        )
        source_record = plan.get("source") or {}
        expected_suffix = str(source_record.get("path") or "").split(
            "RealmCreatureProductionSourceV001/", 1
        )[-1]
        if model is None:
            issues.append("SourceManifestModelMissing")
        elif (
            model.get("source2dId") != authority.get("source2dId")
            or model.get("selectedSource", {}).get("path") != expected_suffix
            or model.get("selectedSource", {}).get("sha256") != source_record.get("sha256")
        ):
            issues.append("SourceManifestBindingMismatch")

    textures = (plan.get("materialPolicy") or {}).get("textures", [])
    texture_roles = [texture.get("role") for texture in textures]
    if set(texture_roles) != {
        "base_color",
        "normal",
        "metallic_smoothness",
        "ambient_occlusion",
    } or len(texture_roles) != 4:
        issues.append("MaterialTextureRolesMismatch")
    for texture in textures:
        _verify_file(
            repo_root,
            texture,
            f"texture:{texture.get('role', 'unknown')}",
            issues,
            require_png_dimensions=True,
        )


def _validate_qualification(
    repo_root: Path,
    plan: dict[str, Any],
    qualification: dict[str, Any],
    issues: list[str],
) -> None:
    authority = plan["authority"]
    expected_scalar = {
        "schemaVersion": 1,
        "qualificationId": plan["qualificationId"],
        "sourceVersion": plan["sourceVersion"],
        "modelId": authority["modelId"],
        "source2dId": authority["source2dId"],
        "sourceQualification": "PASS",
        "runtimeIntegration": "BLOCKED",
        "deviceQualification": "BLOCKED",
        "gameplayOrSpawnActivation": False,
    }
    for field, expected in expected_scalar.items():
        if qualification.get(field) != expected:
            issues.append(f"QualificationFieldMismatch:{field}")

    if qualification.get("sourceSha256") != plan["source"]["sha256"]:
        issues.append("QualificationSourceHashMismatch")
    if qualification.get("logicalBuildSignature") != qualification.get(
        "repeatBuildLogicalSignature"
    ):
        issues.append("RepeatabilityMismatch")
    if not HASH_PATTERN.fullmatch(str(qualification.get("logicalBuildSignature") or "")):
        issues.append("InvalidLogicalBuildSignature")

    artifacts = qualification.get("artifacts") or {}
    verified_artifact_paths: dict[str, Path] = {}
    for key in ("blend", "fbx", "dccReport"):
        record = artifacts.get(key)
        if not isinstance(record, dict):
            issues.append(f"MissingArtifactRecord:{key}")
            continue
        artifact_path = str(record.get("path") or "").replace("\\", "/")
        if artifact_path.casefold().startswith("unity/assets/"):
            issues.append(f"RuntimeArtifactPathForbidden:{key}")
        expected_path = str(plan["outputs"][key]).replace("\\", "/")
        if artifact_path != expected_path:
            issues.append(f"ArtifactPathMismatch:{key}")
        path = _verify_file(repo_root, record, f"artifact:{key}", issues)
        if path is not None:
            verified_artifact_paths[key] = path
    dcc_path = verified_artifact_paths.get("dccReport")
    if dcc_path is not None:
        try:
            dcc_report = load_json(dcc_path)
        except json.JSONDecodeError as error:
            issues.append(f"InvalidDccReport:{error}")
        else:
            if dcc_report.get("status") != "PASS":
                issues.append("DccReportNotPass")
            dcc_bindings = {
                "qualificationId": qualification.get("qualificationId"),
                "logicalBuildSignature": qualification.get("logicalBuildSignature"),
                "repeatBuildLogicalSignature": qualification.get(
                    "repeatBuildLogicalSignature"
                ),
            }
            for field, expected in dcc_bindings.items():
                if dcc_report.get(field) != expected:
                    issues.append(f"DccReportBindingMismatch:{field}")
            for field in ("rig", "lods", "material", "motions", "deformation", "roundTrip"):
                if dcc_report.get(field) != qualification.get(field):
                    issues.append(f"DccReportBindingMismatch:{field}")
    reviews = artifacts.get("reviewImages") or []
    if len(reviews) < 4:
        issues.append("InsufficientReviewImages")
    review_paths = [str(record.get("path") or "") for record in reviews]
    if len(review_paths) != len(set(review_paths)):
        issues.append("DuplicateReviewImage")
    review_names = {
        path.replace("\\", "/").rsplit("/", 1)[-1]
        for path in review_paths
    }
    required_reviews = expected_review_files(review_slug_from_plan(plan))
    for label, expected_name in required_reviews.items():
        if expected_name not in review_names:
            issues.append(f"RequiredReviewEvidenceMissing:{label}")
    review_root = str(plan["outputs"]["reviewDirectory"]).replace("\\", "/").rstrip("/") + "/"
    for index, record in enumerate(reviews):
        review_path = str(record.get("path") or "").replace("\\", "/")
        if review_path.casefold().startswith("unity/assets/"):
            issues.append(f"RuntimeReviewPathForbidden:{index}")
        if not review_path.startswith(review_root):
            issues.append(f"ReviewPathMismatch:{index}")
        _verify_file(
            repo_root,
            record,
            f"reviewImage:{index}",
            issues,
            require_png_dimensions=True,
        )

    rig = qualification.get("rig") or {}
    expected_bones = set(plan["rig"]["rootBones"] + plan["rig"]["deformBones"] + plan["rig"]["socketBones"])
    actual_bones = set(rig.get("boneNames") or [])
    if rig.get("armatureObject") != plan["rig"]["armatureObject"]:
        issues.append("ArmatureObjectMismatch")
    if not expected_bones.issubset(actual_bones):
        issues.append("RequiredBonesMissing")
    if rig.get("parentlessBones") != ["root"]:
        issues.append("RootHierarchyInvalid")
    if rig.get("maximumInfluencesPerVertex", 999) > plan["rig"]["maximumInfluencesPerVertex"]:
        issues.append("InfluenceBudgetExceeded")
    if rig.get("unweightedVertices") != 0:
        issues.append("UnweightedVertices")
    if not HASH_PATTERN.fullmatch(str(rig.get("skeletonSignature") or "")):
        issues.append("InvalidSkeletonSignature")

    lods = qualification.get("lods") or []
    if [row.get("id") for row in lods] != list(EXPECTED_LODS):
        issues.append("LodOrderingMismatch")
    plan_lods = {row["id"]: row for row in plan["lodPolicy"]["levels"]}
    triangles = []
    for row in lods:
        lod_id = row.get("id")
        triangles.append(row.get("triangles", 0))
        if lod_id not in plan_lods:
            continue
        if row.get("object") != plan_lods[lod_id]["object"]:
            issues.append(f"LodObjectMismatch:{lod_id}")
        if row.get("triangles", 0) <= 0 or row.get("triangles", 0) > plan_lods[lod_id]["maximumTriangles"]:
            issues.append(f"LodBudgetExceeded:{lod_id}")
        if set(row.get("protectedIdentityCues") or []) != set(plan["protectedIdentityCues"]):
            issues.append(f"ProtectedCueCoverageMismatch:{lod_id}")
        if row.get("materialSlots", 999) > plan["materialPolicy"]["maximumSlots"]:
            issues.append(f"MaterialSlotBudgetExceeded:{lod_id}")
    if len(triangles) == 3 and not triangles[0] > triangles[1] > triangles[2]:
        issues.append("LodTriangleOrderInvalid")

    material = qualification.get("material") or {}
    if material.get("id") != plan["materialPolicy"]["materialId"]:
        issues.append("MaterialIdMismatch")
    if material.get("runtimeVfxSeparate") is not True or material.get("emissionBakedIntoCleanMesh") is not False:
        issues.append("MaterialVfxBoundaryViolation")
    if material.get("maximumTextureLongEdge", 99999) > plan["materialPolicy"]["maximumTextureLongEdge"]:
        issues.append("TextureBudgetExceeded")

    motion_rows = qualification.get("motions") or []
    motion_keys = {row.get("motionKey") for row in motion_rows}
    if not EXPECTED_REQUIRED_MOTIONS.issubset(motion_keys):
        issues.append("RequiredMotionCoverageMissing")
    planned = {row["motionKey"]: row for row in plan["motionPolicy"]["motions"]}
    if motion_keys != set(planned):
        issues.append("MotionSetMismatch")
    action_names = [row.get("actionName") for row in motion_rows]
    if len(action_names) != len(set(action_names)):
        issues.append("DuplicateActionName")
    for row in motion_rows:
        expected = planned.get(row.get("motionKey"))
        if expected is None:
            continue
        if row.get("actionName") != expected["actionName"] or row.get("frameCount") != expected["durationFrames"]:
            issues.append(f"MotionBindingMismatch:{row.get('motionKey')}")
        if row.get("sampleRateHz") != plan["motionPolicy"]["sampleRateHz"]:
            issues.append(f"MotionSampleRateMismatch:{row.get('motionKey')}")
        if row.get("finiteTransforms") is not True:
            issues.append(f"NonFiniteMotion:{row.get('motionKey')}")
        if row.get("loop") and (
            row.get("loopPositionErrorMeters", 999) > 0.01
            or row.get("loopRotationErrorDegrees", 999) > 1.0
        ):
            issues.append(f"MotionLoopMismatch:{row.get('motionKey')}")

    deformation = qualification.get("deformation") or {}
    if deformation.get("poseCount", 0) < plan["rig"]["minimumDeformationPoses"]:
        issues.append("InsufficientDeformationPoses")
    if deformation.get("nonFiniteVertices") != 0:
        issues.append("NonFiniteDeformation")
    if deformation.get("invertedTriangles") != 0:
        issues.append("InvertedTriangles")
    if deformation.get("maximumBoundsExpansionRatio", 999) > 1.35:
        issues.append("DeformationBoundsExpansionExceeded")

    round_trip = qualification.get("roundTrip") or {}
    for field in ("fbxImportPassed", "skeletonSignatureMatched", "lodTrianglesMatched", "actionsMatched", "materialsMatched"):
        if round_trip.get(field) is not True:
            issues.append(f"RoundTripFailed:{field}")


def validate_slice(
    repo_root: Path = REPO_ROOT,
    plan_path: Path = DEFAULT_PLAN,
    schema_path: Path = DEFAULT_SCHEMA,
    qualification_path: Path = DEFAULT_QUALIFICATION,
    *,
    require_pass: bool = False,
) -> dict[str, Any]:
    repo_root = repo_root.resolve()
    issues: list[str] = []
    try:
        plan = load_json(plan_path)
    except (FileNotFoundError, json.JSONDecodeError) as error:
        issues.append(f"InvalidPlan:{error}")
        plan = {}
    if plan:
        issues.extend(_schema_issues(plan, schema_path))
        _validate_authority_and_sources(repo_root, plan, issues)
    try:
        qualification = load_json(qualification_path)
    except (FileNotFoundError, json.JSONDecodeError) as error:
        issues.append(f"InvalidQualification:{error}")
        qualification = {}
    if plan and qualification:
        _validate_qualification(repo_root, plan, qualification, issues)
    report = {
        "overall": "PASS" if not issues else "FAIL",
        "modelId": (plan.get("authority") or {}).get("modelId"),
        "sourceQualification": qualification.get("sourceQualification", "FAIL"),
        "runtimeIntegration": qualification.get("runtimeIntegration", "BLOCKED"),
        "deviceQualification": qualification.get("deviceQualification", "BLOCKED"),
        "gameplayOrSpawnActivation": qualification.get("gameplayOrSpawnActivation", False),
        "issues": sorted(set(issues)),
    }
    if require_pass and issues:
        raise SliceValidationError(report["issues"])
    return report


def validate_default_slice() -> dict[str, Any]:
    return validate_slice()


def validate_cindermaw_slice() -> dict[str, Any]:
    return validate_slice(
        plan_path=DEFAULT_CINDERMAW_PLAN,
        schema_path=DEFAULT_CINDERMAW_SCHEMA,
        qualification_path=DEFAULT_CINDERMAW_QUALIFICATION,
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=REPO_ROOT)
    parser.add_argument("--plan", type=Path, default=DEFAULT_PLAN)
    parser.add_argument("--schema", type=Path, default=DEFAULT_SCHEMA)
    parser.add_argument("--qualification", type=Path, default=DEFAULT_QUALIFICATION)
    args = parser.parse_args(argv)
    report = validate_slice(
        args.repo_root,
        args.plan,
        args.schema,
        args.qualification,
    )
    print(json.dumps(report, indent=2))
    return 0 if report["overall"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
