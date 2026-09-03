#!/usr/bin/env python3
"""Assemble and verify approval-ready AnotherLife release evidence packages."""

from __future__ import annotations

import argparse
import copy
import hashlib
import importlib.util
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any


SCRIPT_PATH = Path(__file__).resolve()
DEFAULT_POLICY = SCRIPT_PATH.with_name("release_evidence_policy.v1.json")
HEX_40 = set("0123456789abcdef")
REQUIRED_STOP_SHIP = {
    "unreproducible_build",
    "editor_exporter_incompatibility",
    "save_loss_or_silent_downgrade",
    "missing_required_scene",
    "nondeterministic_content_manifest",
    "narrative_runtime_disconnected",
    "automated_manual_divergence",
    "missing_or_malformed_evidence",
}
REQUIRED_REOPEN = {
    "editor_or_exporter_change",
    "save_schema_change",
    "scene_or_catalog_addition",
    "narrative_runtime_change",
    "failed_migration_telemetry",
}


class ReleaseEvidenceError(RuntimeError):
    """A release package prerequisite or evidence integrity failure."""


def canonical_json(payload: Any) -> bytes:
    return (
        json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
        + "\n"
    ).encode("utf-8")


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(Path(path).read_bytes())


def _is_hex(value: Any, length: int) -> bool:
    return isinstance(value, str) and len(value) == length and all(ch in HEX_40 for ch in value)


def _load_json(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ReleaseEvidenceError(f"evidence JSON invalid: {path}: {error}") from error
    if not isinstance(payload, dict):
        raise ReleaseEvidenceError(f"evidence JSON object required: {path}")
    return payload


def _ids(rows: Any, field: str) -> list[str]:
    if not isinstance(rows, list) or not rows or not all(isinstance(row, dict) for row in rows):
        raise ReleaseEvidenceError(f"policy field must be a non-empty object array: {field}")
    values = [str(row.get("id") or "") for row in rows]
    if any(not value for value in values) or len(values) != len(set(values)):
        raise ReleaseEvidenceError(f"policy field has empty or duplicate ids: {field}")
    return values


def load_policy(path: Path = DEFAULT_POLICY) -> dict[str, Any]:
    policy = _load_json(Path(path).resolve())
    required = {
        "schemaVersion",
        "packageId",
        "promotionStatus",
        "authorityReferences",
        "requiredQaProfile",
        "requiredQaContracts",
        "sourceAuthorities",
        "upstreamEvidence",
        "ownerDecisions",
        "compatibilityExceptions",
        "manualOwnerGates",
        "stopShipConditions",
        "reopenTriggers",
    }
    if policy.get("schemaVersion") != 1 or not required.issubset(policy):
        raise ReleaseEvidenceError("release evidence policy is incomplete")
    if policy.get("packageId") != "anotherlife-release-evidence":
        raise ReleaseEvidenceError("release evidence package id is invalid")
    if policy.get("promotionStatus") != "awaiting_release_owner_approval":
        raise ReleaseEvidenceError("release evidence promotion status must remain owner-gated")
    references = policy.get("authorityReferences")
    if references != {
        "approvalDependencyTask": "t_0648ce23",
        "releaseCriteriaTask": "t_4a5b066c",
        "capacityCriteriaTask": "t_7f6be100",
    }:
        raise ReleaseEvidenceError("numerical authority task references changed")
    contracts = policy.get("requiredQaContracts")
    if not isinstance(contracts, list) or len(contracts) != 12 or len(contracts) != len(set(contracts)):
        raise ReleaseEvidenceError("required QA contract list must contain 12 unique contracts")
    if set(_ids(policy["stopShipConditions"], "stopShipConditions")) != REQUIRED_STOP_SHIP:
        raise ReleaseEvidenceError("stop-ship condition coverage is incomplete")
    if set(_ids(policy["reopenTriggers"], "reopenTriggers")) != REQUIRED_REOPEN:
        raise ReleaseEvidenceError("reopen trigger coverage is incomplete")
    gate_ids = set(_ids(policy["manualOwnerGates"], "manualOwnerGates"))
    for condition in policy["stopShipConditions"]:
        automated = condition.get("automatedContracts") or []
        gates = condition.get("manualOwnerGates") or []
        if not automated and not gates:
            raise ReleaseEvidenceError(f"stop-ship condition is unmapped: {condition['id']}")
        if any(contract not in contracts for contract in automated):
            raise ReleaseEvidenceError(f"stop-ship condition has unknown contract: {condition['id']}")
        if any(gate not in gate_ids for gate in gates):
            raise ReleaseEvidenceError(f"stop-ship condition has unknown owner gate: {condition['id']}")
    source_roles = [row.get("role") for row in policy["sourceAuthorities"]]
    source_paths = [row.get("path") for row in policy["sourceAuthorities"]]
    if (
        not source_roles
        or len(source_roles) != len(set(source_roles))
        or len(source_paths) != len(set(source_paths))
        or any(not role or not path for role, path in zip(source_roles, source_paths))
    ):
        raise ReleaseEvidenceError("source authority roles and paths must be unique")
    policy["_policyPath"] = str(Path(path).resolve())
    return policy


def _load_module(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise ReleaseEvidenceError(f"cannot load evidence validator: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _git(repo_root: Path, *arguments: str) -> str:
    try:
        completed = subprocess.run(
            ["git", *arguments],
            cwd=repo_root,
            check=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
    except subprocess.CalledProcessError as error:
        raise ReleaseEvidenceError(
            f"git evidence lookup failed: {' '.join(arguments)}: {error.stderr.strip()}"
        ) from error
    return completed.stdout.strip()


def _source_identity(repo_root: Path, source_revision: str, role: str, relative: str) -> dict[str, str]:
    if Path(relative).is_absolute() or ".." in Path(relative).parts:
        raise ReleaseEvidenceError(f"unsafe source authority path: {relative}")
    tree_row = _git(repo_root, "ls-tree", source_revision, "--", relative)
    parts = tree_row.split()
    if len(parts) < 4 or parts[1] != "blob" or not _is_hex(parts[2], 40):
        raise ReleaseEvidenceError(f"source authority is not an immutable blob: {relative}")
    committed = subprocess.run(
        ["git", "show", f"{source_revision}:{relative}"],
        cwd=repo_root,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    ).stdout
    return {
        "role": role,
        "path": relative,
        "sha256": sha256_bytes(committed),
        "gitBlob": parts[2],
    }


def _matches_committed_text_hash(repo_root: Path, source_revision: str, relative: str, digest: Any) -> bool:
    if not _is_hex(digest, 64):
        return False
    committed = subprocess.run(
        ["git", "show", f"{source_revision}:{relative}"],
        cwd=repo_root,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    ).stdout
    lf = committed.replace(b"\r\n", b"\n")
    crlf = lf.replace(b"\n", b"\r\n")
    return digest in {sha256_bytes(committed), sha256_bytes(lf), sha256_bytes(crlf)}


def _verify_source_provenance(
    repo_root: Path,
    source_revision: str,
    report: dict[str, Any],
) -> None:
    provenance = report["provenance"]
    expected = {
        "tools/qa/run_deterministic_qa.py": provenance["suite"]["runnerSha256"],
        "tools/qa/deterministic_qa_policy.json": provenance["suite"]["policySha256"],
        "tools/qa/manual_results.v1.json": provenance["suite"]["manualBaselineSha256"],
        "unity/SharedContracts/integrated-qa-evidence.schema.json": provenance["suite"]["evidenceSchemaSha256"],
        "unity/Assets/AL/StreamingAssets/GameData/al_enabled_scene_manifest.v1.json": provenance["scene"]["enabledManifestSha256"],
        "unity/Assets/AL/StreamingAssets/GameData/al_generated_scene_manifest.v1.json": provenance["scene"]["generatedManifestSha256"],
        "unity/Assets/AL/StreamingAssets/GameData/al_world_streaming_catalog.json": provenance["content"]["worldCatalogSha256"],
        "unity/Assets/AL/StreamingAssets/GameData/al_main_quest_line_runtime.v1.json": provenance["content"]["narrativeCatalogSha256"],
        "unity/Assets/AL/Tests/EditMode/Fixtures/SaveSchema1/manifest.json": provenance["save"]["fixtureManifestSha256"],
    }
    diverged = sorted(
        relative
        for relative, digest in expected.items()
        if not _matches_committed_text_hash(repo_root, source_revision, relative, digest)
    )
    if diverged:
        raise ReleaseEvidenceError(f"source provenance differs from QA commit: {diverged}")


def _verify_build_manifest(path: Path, report: dict[str, Any]) -> dict[str, Any]:
    build = _load_json(path)
    recorded = build.get("manifestSha256")
    unsigned = copy.deepcopy(build)
    unsigned.pop("manifestSha256", None)
    if not _is_hex(recorded, 64) or recorded != sha256_bytes(canonical_json(unsigned)):
        raise ReleaseEvidenceError("build manifest digest is missing or invalid")
    if build.get("status") != "succeeded" or build.get("target") != "windows64-development":
        raise ReleaseEvidenceError("build manifest is not a successful Windows candidate")
    provenance = report["provenance"]
    if build.get("source", {}).get("sourceRevision") != provenance.get("sourceRevision"):
        raise ReleaseEvidenceError("build manifest source revision differs from QA evidence")
    artifact_hash = build.get("artifacts", {}).get("reproducibleTreeSha256")
    if build.get("artifacts", {}).get("smoke", {}).get("status") != "passed":
        raise ReleaseEvidenceError("build artifact structural smoke did not pass")
    if recorded != provenance.get("build", {}).get("manifestSha256"):
        raise ReleaseEvidenceError("build manifest digest differs from QA provenance")
    if artifact_hash != provenance.get("build", {}).get("artifactTreeSha256"):
        raise ReleaseEvidenceError("build artifact tree differs from QA provenance")
    contract = next((item for item in report["contracts"] if item.get("id") == "build-smoke"), None)
    if not contract or contract.get("evidence", {}).get("manifestSha256") != recorded:
        raise ReleaseEvidenceError("build-smoke contract is not bound to the build manifest")
    return build


def _verify_narrative(path: Path, report: dict[str, Any], repo_root: Path) -> dict[str, Any]:
    narrative = _load_json(path)
    module = _load_module(
        repo_root / "tools/narrative/packaged_narrative_smoke.py",
        "packaged_narrative_smoke_for_release",
    )
    evaluation = module.evaluate_evidence_document(narrative)
    if evaluation.get("status") != "passed" or narrative.get("applicationIsEditor") is not False:
        raise ReleaseEvidenceError(
            f"narrative packaged evidence is invalid: {evaluation.get('reasonCode')}"
        )
    provenance = report["provenance"]
    expected = {
        "unityVersion": provenance["unity"]["version"],
        "enabledSceneManifestSha256": provenance["scene"]["enabledManifestSha256"],
        "generatedSceneManifestSha256": provenance["scene"]["generatedManifestSha256"],
        "narrativeCatalogSha256": provenance["content"]["narrativeCatalogSha256"],
    }
    diverged = sorted(key for key, value in expected.items() if narrative.get(key) != value)
    if diverged:
        raise ReleaseEvidenceError(f"narrative packaged evidence diverged: {diverged}")
    contract = next((item for item in report["contracts"] if item.get("id") == "packaged-narrative"), None)
    if not contract:
        raise ReleaseEvidenceError("narrative QA contract is missing")
    for key in (
        "enabledSceneManifestSha256",
        "generatedSceneManifestSha256",
        "narrativeCatalogSha256",
        "entryQuestId",
        "resumedQuestStateId",
    ):
        if contract.get("evidence", {}).get(key) != narrative.get(key):
            raise ReleaseEvidenceError(f"narrative QA contract differs from packaged evidence: {key}")
    return narrative


def _copy_artifact(source: Path, destination: Path, role: str, package_root: Path) -> dict[str, str]:
    if not source.is_file():
        raise ReleaseEvidenceError(f"required evidence artifact is missing: {source}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    return {
        "role": role,
        "path": destination.relative_to(package_root).as_posix(),
        "sha256": sha256_file(destination),
    }


def _write_manifest(payload: dict[str, Any], path: Path) -> dict[str, Any]:
    unsigned = copy.deepcopy(payload)
    unsigned.pop("packageSha256", None)
    package = copy.deepcopy(unsigned)
    package["packageSha256"] = sha256_bytes(canonical_json(unsigned))
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_bytes(canonical_json(package))
    os.replace(temporary, path)
    path.with_suffix(path.suffix + ".sha256").write_text(
        f"{package['packageSha256']}  {path.name}\n",
        encoding="ascii",
    )
    return package


def assemble_package(
    repo_root: Path,
    policy: dict[str, Any],
    qa_root: Path,
    output_dir: Path,
) -> dict[str, Any]:
    repo_root = Path(repo_root).resolve()
    qa_root = Path(qa_root).resolve()
    output_dir = Path(output_dir).resolve()
    if (
        output_dir == repo_root
        or repo_root.is_relative_to(output_dir)
        or output_dir == qa_root
        or output_dir.is_relative_to(qa_root)
        or qa_root.is_relative_to(output_dir)
    ):
        raise ReleaseEvidenceError("unsafe package output overlaps the repository or QA input")
    if output_dir.exists():
        raise ReleaseEvidenceError(f"package output already exists: {output_dir}")
    qa_module = _load_module(repo_root / "tools/qa/run_deterministic_qa.py", "qa_for_release")
    try:
        report = qa_module.verify_report(qa_root / "report.json")
    except Exception as error:
        raise ReleaseEvidenceError(f"QA report is invalid: {error}") from error
    if (
        report.get("profile") != policy["requiredQaProfile"]
        or report.get("status") != "passed"
        or report.get("provenance", {}).get("sourceDirty") is not False
    ):
        raise ReleaseEvidenceError("release packaging requires a passed clean full QA report")
    source_revision = report["provenance"].get("sourceRevision")
    if not _is_hex(source_revision, 40):
        raise ReleaseEvidenceError("QA source revision is invalid")
    if not _git(repo_root, "cat-file", "-e", f"{source_revision}^{{commit}}") == "":
        raise ReleaseEvidenceError("QA source revision is not a commit")
    contracts = report.get("contracts")
    if not isinstance(contracts, list):
        raise ReleaseEvidenceError("QA contracts are missing")
    observed_ids = [item.get("id") for item in contracts]
    if observed_ids != policy["requiredQaContracts"] or any(
        item.get("status") != "passed" for item in contracts
    ):
        raise ReleaseEvidenceError("QA contract coverage or status is not release-ready")
    if report.get("manualComparison", {}).get("status") != "passed":
        raise ReleaseEvidenceError("automated/manual QA comparison did not pass")
    report_refs = report.get("authorityReferences", {})
    for key in ("releaseCriteriaTask", "capacityCriteriaTask"):
        if report_refs.get(key) != policy["authorityReferences"][key]:
            raise ReleaseEvidenceError(f"QA authority reference differs: {key}")

    build_path = qa_root / "build/windows64-development.json"
    narrative_path = qa_root / "narrative/packaged-evidence.json"
    _verify_source_provenance(repo_root, source_revision, report)
    _verify_build_manifest(build_path, report)
    _verify_narrative(narrative_path, report, repo_root)

    output_dir.mkdir(parents=True)
    artifacts = [
        _copy_artifact(qa_root / "report.json", output_dir / "evidence/qa/report.json", "qa_report", output_dir),
        _copy_artifact(qa_root / "junit.xml", output_dir / "evidence/qa/junit.xml", "qa_junit", output_dir),
        _copy_artifact(build_path, output_dir / "evidence/build/windows64-development.json", "build_manifest", output_dir),
        _copy_artifact(narrative_path, output_dir / "evidence/narrative/packaged-evidence.json", "packaged_narrative", output_dir),
    ]
    for relative in report.get("artifacts", {}).get("logs", []):
        if not isinstance(relative, str) or not relative.startswith("logs/") or ".." in Path(relative).parts:
            raise ReleaseEvidenceError(f"unsafe QA artifact path: {relative}")
        source = qa_root / relative
        expected = next(
            (
                attempt.get("logSha256")
                for contract in contracts
                for attempt in contract.get("attempts", [])
                if attempt.get("log") == relative
            ),
            None,
        )
        if not _is_hex(expected, 64) or not source.is_file() or sha256_file(source) != expected:
            raise ReleaseEvidenceError(f"QA log digest mismatch: {relative}")
        artifacts.append(
            _copy_artifact(source, output_dir / "evidence/qa" / relative, "qa_log", output_dir)
        )

    controls = [
        (Path(policy["_policyPath"]), output_dir / "controls/release_evidence_policy.v1.json", "release_policy"),
        (repo_root / "unity/SharedContracts/release-evidence-package.schema.json", output_dir / "controls/release-evidence-package.schema.json", "release_schema"),
        (repo_root / "unity/Docs/Release_Evidence_And_Rollback_Runbook.md", output_dir / "controls/Release_Evidence_And_Rollback_Runbook.md", "rollback_runbook"),
    ]
    control_artifacts = [
        _copy_artifact(source, destination, role, output_dir)
        for source, destination, role in controls
    ]
    source_authorities = [
        _source_identity(repo_root, source_revision, row["role"], row["path"])
        for row in policy["sourceAuthorities"]
    ]
    qa_contracts = [
        {
            "id": item["id"],
            "failureCode": item["failureCode"],
            "status": item["status"],
            "reasonCode": item["reasonCode"],
            "evidence": item.get("evidence") or {},
        }
        for item in contracts
    ]
    package = {
        "schemaVersion": 1,
        "packageId": policy["packageId"],
        "promotionStatus": policy["promotionStatus"],
        "sourceRevision": source_revision,
        "qaRun": {
            "id": report["run"]["id"],
            "reportSha256": report["reportSha256"],
            "profile": report["profile"],
            "status": report["status"],
        },
        "authorityReferences": policy["authorityReferences"],
        "controlArtifacts": control_artifacts,
        "sourceAuthorities": source_authorities,
        "upstreamEvidence": policy["upstreamEvidence"],
        "ownerDecisions": policy["ownerDecisions"],
        "compatibilityExceptions": policy["compatibilityExceptions"],
        "manualOwnerGates": policy["manualOwnerGates"],
        "stopShipConditions": policy["stopShipConditions"],
        "reopenTriggers": policy["reopenTriggers"],
        "qaContracts": qa_contracts,
        "artifacts": artifacts,
        "rollbackSafety": {
            "buildDataPairRestore": "same_verified_manifest_and_supported_save_schema_only",
            "saveBackupRetention": "byte_exact_until_release_owner_closes_migration_observation",
            "schemaRollback": "not_approved_without_new_bidirectional_fixture_proof",
        },
    }
    written = _write_manifest(package, output_dir / "release-evidence.json")
    return verify_package(output_dir, policy) if written else written


def verify_package(package_root: Path, policy: dict[str, Any]) -> dict[str, Any]:
    package_root = Path(package_root).resolve()
    path = package_root / "release-evidence.json"
    package = _load_json(path)
    required_fields = {
        "schemaVersion",
        "packageId",
        "promotionStatus",
        "sourceRevision",
        "qaRun",
        "authorityReferences",
        "controlArtifacts",
        "sourceAuthorities",
        "upstreamEvidence",
        "ownerDecisions",
        "compatibilityExceptions",
        "manualOwnerGates",
        "stopShipConditions",
        "reopenTriggers",
        "qaContracts",
        "artifacts",
        "rollbackSafety",
        "packageSha256",
    }
    if set(package) != required_fields:
        raise ReleaseEvidenceError("release package fields do not match the shared contract")
    recorded = package.get("packageSha256")
    unsigned = copy.deepcopy(package)
    unsigned.pop("packageSha256", None)
    if not _is_hex(recorded, 64) or recorded != sha256_bytes(canonical_json(unsigned)):
        raise ReleaseEvidenceError("release package digest is missing or invalid")
    if package.get("schemaVersion") != 1 or package.get("packageId") != policy["packageId"]:
        raise ReleaseEvidenceError("release package identity is invalid")
    if package.get("promotionStatus") != "awaiting_release_owner_approval":
        raise ReleaseEvidenceError("release package bypassed the owner promotion gate")
    if package.get("authorityReferences") != policy["authorityReferences"]:
        raise ReleaseEvidenceError("release package authority references changed")
    for field in (
        "upstreamEvidence",
        "ownerDecisions",
        "compatibilityExceptions",
        "manualOwnerGates",
        "stopShipConditions",
        "reopenTriggers",
    ):
        if package.get(field) != policy[field]:
            raise ReleaseEvidenceError(f"release package control field changed: {field}")
    qa_run = package.get("qaRun")
    if not isinstance(qa_run, dict) or qa_run.get("profile") != "full" or qa_run.get("status") != "passed":
        raise ReleaseEvidenceError("release package QA run is not a passing full profile")
    if not _is_hex(package.get("sourceRevision"), 40) or not _is_hex(qa_run.get("reportSha256"), 64):
        raise ReleaseEvidenceError("release package source or QA identity is invalid")
    control_artifacts = package.get("controlArtifacts")
    if not isinstance(control_artifacts, list) or {
        item.get("role") for item in control_artifacts if isinstance(item, dict)
    } != {"release_policy", "release_schema", "rollback_runbook"}:
        raise ReleaseEvidenceError("release package control artifact catalog is incomplete")
    artifacts = package.get("artifacts")
    if not isinstance(artifacts, list):
        raise ReleaseEvidenceError("release package artifact catalog is invalid")
    artifact_roles = [item.get("role") for item in artifacts if isinstance(item, dict)]
    if (
        artifact_roles.count("qa_report") != 1
        or artifact_roles.count("qa_junit") != 1
        or artifact_roles.count("build_manifest") != 1
        or artifact_roles.count("packaged_narrative") != 1
        or artifact_roles.count("qa_log") < 1
        or len(artifact_roles) != len(artifacts)
    ):
        raise ReleaseEvidenceError("release package artifact catalog is incomplete")
    source_authorities = package.get("sourceAuthorities")
    expected_source_roles = [row["role"] for row in policy["sourceAuthorities"]]
    if (
        not isinstance(source_authorities, list)
        or [row.get("role") for row in source_authorities if isinstance(row, dict)] != expected_source_roles
        or any(
            not _is_hex(row.get("sha256"), 64) or not _is_hex(row.get("gitBlob"), 40)
            for row in source_authorities
        )
    ):
        raise ReleaseEvidenceError("release package source authority catalog is invalid")
    for item in control_artifacts + artifacts:
        relative = item.get("path")
        if not isinstance(relative, str) or Path(relative).is_absolute() or ".." in Path(relative).parts:
            raise ReleaseEvidenceError(f"unsafe package artifact path: {relative}")
        artifact = package_root / relative
        if not artifact.is_file() or sha256_file(artifact) != item.get("sha256"):
            raise ReleaseEvidenceError(f"release package artifact digest mismatch: {relative}")
    if [item.get("id") for item in package.get("qaContracts", [])] != policy["requiredQaContracts"]:
        raise ReleaseEvidenceError("release package QA contract list changed")
    if any(item.get("status") != "passed" for item in package["qaContracts"]):
        raise ReleaseEvidenceError("release package contains a failed QA contract")
    qa_report_path = next(
        package_root / item["path"] for item in artifacts if item["role"] == "qa_report"
    )
    qa_report = _load_json(qa_report_path)
    qa_unsigned = copy.deepcopy(qa_report)
    qa_recorded = qa_unsigned.pop("reportSha256", None)
    if (
        qa_recorded != qa_run.get("reportSha256")
        or qa_recorded != sha256_bytes(canonical_json(qa_unsigned))
        or qa_report.get("run", {}).get("id") != qa_run.get("id")
    ):
        raise ReleaseEvidenceError("release package QA report identity is invalid")
    expected_log_paths = {
        "evidence/qa/" + relative
        for relative in qa_report.get("artifacts", {}).get("logs", [])
    }
    actual_log_paths = {
        item["path"] for item in artifacts if item["role"] == "qa_log"
    }
    if actual_log_paths != expected_log_paths:
        raise ReleaseEvidenceError("release package QA log catalog is incomplete")
    if package.get("rollbackSafety") != {
        "buildDataPairRestore": "same_verified_manifest_and_supported_save_schema_only",
        "saveBackupRetention": "byte_exact_until_release_owner_closes_migration_observation",
        "schemaRollback": "not_approved_without_new_bidirectional_fixture_proof",
    }:
        raise ReleaseEvidenceError("release package rollback safety policy changed")
    sidecar = path.with_suffix(path.suffix + ".sha256")
    expected_sidecar = f"{recorded}  {path.name}\n"
    if not sidecar.is_file() or sidecar.read_text(encoding="ascii") != expected_sidecar:
        raise ReleaseEvidenceError("release package sidecar is missing or invalid")
    return package


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    assemble = subparsers.add_parser("assemble")
    assemble.add_argument("--repo-root", type=Path, default=SCRIPT_PATH.parents[1])
    assemble.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    assemble.add_argument("--qa-root", type=Path, required=True)
    assemble.add_argument("--output-dir", type=Path, required=True)
    verify = subparsers.add_parser("verify")
    verify.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    verify.add_argument("--package-root", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        policy = load_policy(args.policy)
        if args.command == "assemble":
            package = assemble_package(args.repo_root, policy, args.qa_root, args.output_dir)
        else:
            package = verify_package(args.package_root, policy)
    except (ReleaseEvidenceError, OSError, subprocess.SubprocessError) as error:
        print(f"release-evidence: {error}", file=sys.stderr)
        return 2
    print(
        f"RELEASE_EVIDENCE_VERIFIED package={package['packageId']} "
        f"source={package['sourceRevision']} sha256={package['packageSha256']} "
        f"status={package['promotionStatus']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
