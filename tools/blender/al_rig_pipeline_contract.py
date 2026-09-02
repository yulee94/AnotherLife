#!/usr/bin/env python3
"""Pure-Python contract checks for the Blender rig cleanup pipeline."""

from __future__ import annotations

import hashlib
import json
import re
from collections.abc import Iterable
from pathlib import Path
from typing import Any

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_MANIFEST = (
    REPOSITORY_ROOT
    / "unity"
    / "ArtSource"
    / "RigPipeline"
    / "al_rig_cleanup_manifest.v1.json"
)
DEFAULT_SCHEMA = (
    REPOSITORY_ROOT
    / "unity"
    / "SharedContracts"
    / "Schemas"
    / "al-rig-cleanup-pipeline.schema.json"
)
BONE_NAME_PATTERN = re.compile(r"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
SIGNATURE_ALGORITHM = "sha256_canonical_parent_path_bind_matrix_deform_flag_v1"
EXPECTED_SUBJECTS = {"champion", "npc", "beast"}
EXPECTED_SUBJECT_ORDER = ["champion", "npc", "beast"]


class RigCleanupContractError(RuntimeError):
    """Raised when a manifest or generated rig artifact fails closed."""

    def __init__(self, issues: Iterable[str]):
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


def stable_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=True,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")


def skeleton_signature(records: list[dict[str, Any]]) -> str:
    canonical = []
    for record in records:
        canonical.append(
            {
                "path": record["path"],
                "parentPath": record["parentPath"],
                "localBindMatrix": [
                    [round(float(component), 6) for component in row]
                    for row in record["localBindMatrix"]
                ],
                "deform": bool(record["deform"]),
            }
        )
    canonical.sort(key=lambda row: row["path"].encode("utf-8"))
    return hashlib.sha256(stable_json_bytes(canonical)).hexdigest()


def cleaned_content_signature(
    asset_id: str,
    skeleton_hash: str,
    meshes: list[dict[str, Any]],
    preflight: dict[str, Any],
) -> str:
    payload = {
        "assetId": asset_id,
        "skeletonSignature": skeleton_hash,
        "meshes": meshes,
        "preflight": preflight,
    }
    return hashlib.sha256(stable_json_bytes(payload)).hexdigest()


def _schema_issues(schema: dict[str, Any], instance: dict[str, Any]) -> list[str]:
    try:
        from jsonschema import Draft202012Validator
    except ImportError as error:
        raise RigCleanupContractError(
            ["DependencyMissing: install jsonschema to validate the rig pipeline manifest"]
        ) from error
    Draft202012Validator.check_schema(schema)
    errors = sorted(
        Draft202012Validator(schema).iter_errors(instance),
        key=lambda error: list(error.absolute_path),
    )
    issues = []
    for error in errors:
        location = ".".join(str(part) for part in error.absolute_path) or "<root>"
        issues.append(f"SchemaViolation: {location}: {error.message}")
    return issues


def _id_map(rows: list[dict[str, Any]], section: str, issues: list[str]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for row in rows:
        identifier = row.get("id")
        if identifier in result:
            issues.append(f"DuplicateId: {section}.{identifier}")
        result[identifier] = row
    return result


def _path(repo_root: Path, relative: str) -> Path:
    path = (repo_root / relative).resolve()
    try:
        path.relative_to(repo_root.resolve())
    except ValueError as error:
        raise RigCleanupContractError([f"PathEscapesRepository: {relative}"]) from error
    return path


def validate_manifest(
    repo_root: Path,
    manifest: dict[str, Any] | None = None,
    schema: dict[str, Any] | None = None,
    standard: dict[str, Any] | None = None,
    provenance: dict[str, Any] | None = None,
) -> dict[str, int]:
    repo_root = repo_root.resolve()
    manifest_path = repo_root / DEFAULT_MANIFEST.relative_to(REPOSITORY_ROOT)
    schema_path = repo_root / DEFAULT_SCHEMA.relative_to(REPOSITORY_ROOT)
    manifest = load_json(manifest_path) if manifest is None else manifest
    schema = load_json(schema_path) if schema is None else schema
    issues = _schema_issues(schema, manifest)
    if issues:
        raise RigCleanupContractError(issues)

    standard_path = _path(repo_root, manifest["standardPath"])
    provenance_path = _path(repo_root, manifest["provenancePath"])
    standard = load_json(standard_path) if standard is None else standard
    provenance = load_json(provenance_path) if provenance is None else provenance

    assets = manifest["assets"]
    asset_ids = [row["id"] for row in assets]
    if len(asset_ids) != len(set(asset_ids)):
        issues.append("DuplicateId: assets")
    if [row["subjectKind"] for row in assets] != EXPECTED_SUBJECT_ORDER:
        issues.append("NonDeterministicOrdering: assets")

    subject_kinds = {row["subjectKind"] for row in assets}
    if subject_kinds != EXPECTED_SUBJECTS:
        issues.append(f"RepresentativeCoverageMismatch: {sorted(subject_kinds)}")
    if len(assets) != len(subject_kinds):
        issues.append("RepresentativeCoverageMismatch: exactly one asset per required subject")

    provenance_by_id = _id_map(provenance.get("records", []), "provenance", issues)
    representatives = _id_map(
        standard.get("representativeProfiles", []), "representatives", issues
    )
    skeletons = _id_map(standard.get("skeletonProfiles", []), "skeletons", issues)
    binds = _id_map(standard.get("bindPoses", []), "binds", issues)
    retargets = _id_map(standard.get("retargetProfiles", []), "retargets", issues)
    faces = _id_map(standard.get("facialProfiles", []), "faces", issues)
    budgets = _id_map(standard.get("qualityBudgets", []), "budgets", issues)

    source_paths: set[Path] = set()
    output_paths: set[Path] = set()
    unresolved_rights = 0
    evidence_paths = 0
    for asset in assets:
        asset_id = asset["id"]
        source = asset["source"]
        source_path = _path(repo_root, source["path"])
        if source_path in source_paths:
            issues.append(f"DuplicateSourcePath: {source['path']}")
        source_paths.add(source_path)
        if not source_path.is_file():
            issues.append(f"MissingLocalSource: {asset_id} -> {source['path']}")
        elif sha256_file(source_path) != source["sha256"]:
            issues.append(f"SourceHashMismatch: {asset_id}")

        record = provenance_by_id.get(source["provenanceId"])
        if record is None:
            issues.append(f"MissingProvenance: {asset_id}")
        else:
            if not record.get("localSourceRequired") or not record.get(
                "sourceMaterialOnly"
            ):
                issues.append(f"UnsafeSourceAuthority: {asset_id}")
            if (
                record.get("sourcePath") != source["path"]
                or record.get("sourceSha256") != source["sha256"]
                or record.get("catalogAssetId") != asset["catalogAssetId"]
            ):
                issues.append(f"ProvenanceBindingMismatch: {asset_id}")
            rights = record.get("rightsEvidence", [])
            if not rights:
                issues.append(f"MissingRightsEvidence: {asset_id}")
            for evidence in rights:
                evidence_paths += 1
                if not _path(repo_root, evidence).is_file():
                    issues.append(f"MissingRightsEvidencePath: {asset_id} -> {evidence}")
            if not record.get("productionRightsCleared"):
                unresolved_rights += 1

        for output in asset["output"].values():
            output_path = _path(repo_root, output)
            if output_path in output_paths:
                issues.append(f"DuplicateOutputPath: {output}")
            output_paths.add(output_path)
            if not output.startswith("unity/ArtSource/RigPipeline/"):
                issues.append(f"OutputOutsideRigPipeline: {asset_id} -> {output}")

        representative = representatives.get(asset["representativeProfileId"])
        if representative is None:
            issues.append(f"MissingRepresentativeProfile: {asset_id}")
        else:
            expected = {
                "subjectKind": asset["subjectKind"],
                "assetId": asset["catalogAssetId"],
                "skeletonProfileId": asset["skeletonProfileId"],
                "bindPoseId": asset["bindPoseId"],
                "retargetProfileId": asset["retargetProfileId"],
                "facialProfileId": asset["facialProfileId"],
                "budgetProfileId": asset["budgetProfileId"],
                "requiredMotionSetId": asset["requiredMotionSetId"],
            }
            for field, value in expected.items():
                if representative.get(field) != value:
                    issues.append(
                        f"RepresentativeBindingMismatch: {asset_id}.{field}"
                    )

        references = {
            "skeletonProfileId": skeletons,
            "bindPoseId": binds,
            "retargetProfileId": retargets,
            "facialProfileId": faces,
            "budgetProfileId": budgets,
        }
        for field, valid in references.items():
            if asset[field] not in valid:
                issues.append(f"InvalidReference: {asset_id}.{field}")

        rename_targets = list(asset["boneRenameMap"].values())
        if len(rename_targets) != len(set(rename_targets)):
            issues.append(f"DuplicateBoneRenameTarget: {asset_id}")
        if "root" in rename_targets or "motion_root" in rename_targets:
            issues.append(f"ReservedRootRename: {asset_id}")
        invalid_names = sorted(
            name
            for name in rename_targets
            + asset["requiredBones"]
            + list(asset["sockets"])
            if not BONE_NAME_PATTERN.fullmatch(name)
        )
        if invalid_names:
            issues.append(f"InvalidBoneName: {asset_id} -> {invalid_names}")
        expected_roots = {"root", "motion_root", asset["bodyRootBone"]}
        if not expected_roots.issubset(asset["requiredBones"]):
            issues.append(f"MissingRootContract: {asset_id}")
        unknown_socket_parents = sorted(
            set(asset["sockets"].values())
            - (set(rename_targets) | {"root", "motion_root"})
        )
        if unknown_socket_parents:
            issues.append(
                f"UnknownSocketParent: {asset_id} -> {unknown_socket_parents}"
            )
        unknown_overrides = sorted(
            (
                set(asset["hierarchyOverrides"])
                | set(asset["hierarchyOverrides"].values())
            )
            - set(rename_targets)
        )
        if unknown_overrides:
            issues.append(f"UnknownHierarchyOverride: {asset_id} -> {unknown_overrides}")

        skeleton = skeletons.get(asset["skeletonProfileId"])
        if skeleton is not None:
            profile_required = {bone["name"] for bone in skeleton["bones"]}
            if asset["subjectKind"] in {"champion", "npc"} and not profile_required.issubset(
                asset["requiredBones"]
            ):
                issues.append(f"IncompleteHumanoidSkeleton: {asset_id}")
            if not set(skeleton["requiredSocketNames"]).issubset(asset["sockets"]):
                issues.append(f"IncompleteSocketSet: {asset_id}")

    for alias in sorted(source_paths & output_paths):
        issues.append(f"SourceOutputAlias: {alias.relative_to(repo_root).as_posix()}")

    preset = manifest["exportPreset"]
    if preset["addLeafBones"] or preset["bakeSpaceTransform"]:
        issues.append("UnsafeFbxPreset: leaf bones and baked space transforms are forbidden")
    if not preset["metadataSidecarRequired"]:
        issues.append("UnsafeFbxPreset: metadata sidecar is mandatory")

    if issues:
        raise RigCleanupContractError(issues)
    return {
        "assets": len(assets),
        "representativeSubjects": len(subject_kinds),
        "localSources": len(source_paths),
        "provenanceRecords": len(provenance_by_id),
        "rightsEvidencePaths": evidence_paths,
        "unresolvedProductionRights": unresolved_rights,
        "declaredSockets": sum(len(asset["sockets"]) for asset in assets),
        "declaredBoneRenames": sum(len(asset["boneRenameMap"]) for asset in assets),
    }


def validate_generated_sidecar(
    sidecar: dict[str, Any], asset: dict[str, Any]
) -> dict[str, int]:
    issues: list[str] = []
    if sidecar.get("schemaVersion") != 1:
        issues.append("SidecarSchemaVersion")
    if sidecar.get("pipelineId") != "rmc_pipeline_blender_rig_cleanup_v001":
        issues.append("SidecarPipelineIdentity")
    if sidecar.get("assetId") != asset["id"]:
        issues.append("SidecarAssetIdentity")
    if sidecar.get("status") != "technical_candidate_valid":
        issues.append("SidecarStatus")
    if sidecar.get("errors"):
        issues.append("SidecarHasErrors")
    records = sidecar.get("skeleton", {}).get("records", [])
    actual_signature = skeleton_signature(records) if records else None
    if actual_signature != sidecar.get("skeleton", {}).get("signature"):
        issues.append("SkeletonSignatureMismatch")
    if sidecar.get("skeleton", {}).get("algorithm") != SIGNATURE_ALGORITHM:
        issues.append("SkeletonSignatureAlgorithm")
    names = {record.get("name") for record in records}
    if not set(asset["requiredBones"]).issubset(names):
        issues.append("GeneratedRequiredBonesMissing")
    if not set(asset["sockets"]).issubset(names):
        issues.append("GeneratedSocketsMissing")
    metrics = sidecar.get("preflight", {})
    expected_content_signature = cleaned_content_signature(
        asset["id"], actual_signature or "", sidecar.get("meshes", []), metrics
    )
    if (
        sidecar.get("output", {}).get("blendContentSignature")
        != expected_content_signature
    ):
        issues.append("BlendContentSignatureMismatch")
    if metrics.get("maximumInfluencesPerVertex", 99) > 4:
        issues.append("GeneratedInfluenceBudget")
    if metrics.get("unweightedVertices", 1) != 0:
        issues.append("GeneratedUnweightedVertices")
    if metrics.get("nonNormalizedVertices", 1) != 0:
        issues.append("GeneratedWeightNormalization")
    if metrics.get("ngons", 1) != 0 or metrics.get("nonTriFaces", 1) != 0:
        issues.append("GeneratedTopologyNotTriangulated")
    if metrics.get("degenerateTriangles", 1) != 0:
        issues.append("GeneratedDegenerateTriangles")
    if sidecar.get("productionEligible"):
        issues.append("BoundedRepresentativeCannotPromote")
    if sorted(sidecar.get("productionGaps", [])) != sorted(asset["productionGaps"]):
        issues.append("ProductionGapMismatch")
    if issues:
        raise RigCleanupContractError(issues)
    return {
        "bones": len(records),
        "meshes": len(sidecar.get("meshes", [])),
        "triangles": sum(mesh.get("triangles", 0) for mesh in sidecar.get("meshes", [])),
        "productionGaps": len(sidecar.get("productionGaps", [])),
    }


def main() -> int:
    evidence = validate_manifest(REPOSITORY_ROOT)
    print("PASS: Blender rig cleanup manifest validates")
    print(json.dumps(evidence, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
