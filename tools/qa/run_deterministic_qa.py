#!/usr/bin/env python3
"""Unified fail-closed deterministic QA runner for AnotherLife.

The runner owns deterministic harness inputs, evidence normalization, artifact layout,
and manual-result comparison. The full profile delegates runtime behavior to the
reviewed Python, Unity Test Framework, reproducible-build, and packaged-player tools.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
import platform
import re
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any, Callable


REQUIRED_CONTRACTS = (
    "unit",
    "integration",
    "play-mode",
    "build-smoke",
    "scene-manifest",
    "content-manifest",
    "save-round-trip",
    "save-migration",
    "save-downgrade-rejection",
    "save-corruption-recovery",
    "save-crash-recovery",
    "packaged-narrative",
)
DEFAULT_POLICY = Path(__file__).with_name("deterministic_qa_policy.json")
HEX_64 = re.compile(r"^[0-9a-f]{64}$")


class QaContractError(RuntimeError):
    """A stable suite configuration, execution, or evidence failure."""


def canonical_json(payload: Any) -> bytes:
    return (
        json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
        + "\n"
    ).encode("utf-8")


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def sha256_file(path: Path, *, normalize_lf: bool = False) -> str:
    payload = Path(path).read_bytes()
    if normalize_lf:
        payload = payload.replace(b"\r\n", b"\n")
    return sha256_bytes(payload)


def _load_json(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise QaContractError(f"QA_CONFIG_INVALID: {path}: {error}") from error
    if not isinstance(payload, dict):
        raise QaContractError(f"QA_CONFIG_INVALID: JSON object required: {path}")
    return payload


def load_policy(path: Path = DEFAULT_POLICY) -> dict[str, Any]:
    path = Path(path).resolve()
    policy = _load_json(path)
    required_fields = {
        "schemaVersion",
        "suiteId",
        "fixtureVersion",
        "seed",
        "clockUtc",
        "artifactRoot",
        "manualBaseline",
        "provenance",
        "profiles",
        "contracts",
    }
    if policy.get("schemaVersion") != 1 or not required_fields.issubset(policy):
        raise QaContractError("QA_POLICY_SCHEMA: deterministic QA policy is incomplete")
    contracts = policy.get("contracts")
    if not isinstance(contracts, list):
        raise QaContractError("QA_POLICY_SCHEMA: contracts must be an array")
    ids = [contract.get("id") for contract in contracts if isinstance(contract, dict)]
    if len(ids) != len(set(ids)):
        raise QaContractError("QA_POLICY_DUPLICATE_CONTRACT: contract ids must be unique")
    if set(ids) != set(REQUIRED_CONTRACTS):
        missing = sorted(set(REQUIRED_CONTRACTS) - set(ids))
        extra = sorted(set(ids) - set(REQUIRED_CONTRACTS))
        raise QaContractError(
            f"QA_POLICY_COVERAGE: missing={missing} extra={extra}"
        )
    for contract in contracts:
        if not re.fullmatch(r"QA_[A-Z0-9_]+", str(contract.get("failureCode", ""))):
            raise QaContractError(
                f"QA_POLICY_FAILURE_CODE: {contract.get('id')} has no stable failure code"
            )
        repeat = contract.get("repeat")
        if not isinstance(repeat, int) or repeat < 1:
            raise QaContractError(
                f"QA_POLICY_REPEAT: {contract.get('id')} repeat must be positive"
            )
    for profile_name, profile_ids in policy.get("profiles", {}).items():
        if not isinstance(profile_ids, list) or not profile_ids:
            raise QaContractError(f"QA_POLICY_PROFILE: {profile_name} is empty")
        unknown = sorted(set(profile_ids) - set(ids))
        if unknown or len(profile_ids) != len(set(profile_ids)):
            raise QaContractError(
                f"QA_POLICY_PROFILE: {profile_name} unknown={unknown} or duplicate ids"
            )
    if policy.get("profiles", {}).get("full") != list(REQUIRED_CONTRACTS):
        raise QaContractError("QA_POLICY_COVERAGE: full profile order is not canonical")
    policy["_policyPath"] = str(path)
    return policy


def derive_run_identity(
    fixture_version: str, seed: int, clock_utc: str, source_revision: str
) -> str:
    digest = sha256_bytes(
        canonical_json(
            {
                "clockUtc": clock_utc,
                "fixtureVersion": fixture_version,
                "seed": seed,
                "sourceRevision": source_revision,
            }
        )
    )
    return "qa-" + digest[:20]


def _git(repo_root: Path, *arguments: str) -> str:
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
    return completed.stdout.strip()


def _required_path(repo_root: Path, relative: str) -> Path:
    path = repo_root / relative
    if not path.is_file():
        raise QaContractError(f"QA_PROVENANCE_MISSING: {relative}")
    return path


def collect_repository_provenance(
    repo_root: Path, policy: dict[str, Any]
) -> dict[str, Any]:
    repo_root = Path(repo_root).resolve()
    paths = policy["provenance"]
    project_version_path = _required_path(repo_root, paths["projectVersion"])
    project_version_text = project_version_path.read_text(encoding="utf-8")
    version_match = re.search(r"(?m)^m_EditorVersion:\s*(\S+)\s*$", project_version_text)
    revision_match = re.search(
        r"(?m)^m_EditorVersionWithRevision:\s*\S+\s+\(([^)]+)\)\s*$",
        project_version_text,
    )
    settings_path = _required_path(repo_root, paths["projectSettings"])
    bundle_match = re.search(
        r"(?m)^\s*bundleVersion:\s*(\S+)\s*$",
        settings_path.read_text(encoding="utf-8"),
    )
    save_manifest_path = _required_path(repo_root, paths["saveFixtureManifest"])
    save_manifest = _load_json(save_manifest_path)
    if not version_match or not revision_match or not bundle_match:
        raise QaContractError("QA_PROVENANCE_INVALID: project version/settings incomplete")
    return {
        "sourceRevision": _git(repo_root, "rev-parse", "HEAD"),
        "sourceDirty": bool(
            _git(repo_root, "status", "--porcelain", "--untracked-files=no")
        ),
        "suite": {
            "policySha256": sha256_file(Path(policy["_policyPath"])),
            "runnerSha256": sha256_file(
                _required_path(repo_root, paths["runner"])
            ),
            "manualBaselineSha256": sha256_file(
                _required_path(repo_root, policy["manualBaseline"])
            ),
            "evidenceSchemaSha256": sha256_file(
                _required_path(repo_root, paths["evidenceSchema"])
            ),
        },
        "unity": {
            "version": version_match.group(1),
            "revision": revision_match.group(1),
        },
        "build": {
            "version": bundle_match.group(1),
            "manifestSha256": None,
            "artifactTreeSha256": None,
        },
        "scene": {
            "enabledManifestSha256": sha256_file(
                _required_path(repo_root, paths["enabledSceneManifest"])
            ),
            "generatedManifestSha256": sha256_file(
                _required_path(repo_root, paths["generatedSceneManifest"])
            ),
        },
        "content": {
            "worldCatalogSha256": sha256_file(
                _required_path(repo_root, paths["worldCatalog"])
            ),
            "narrativeCatalogSha256": sha256_file(
                _required_path(repo_root, paths["narrativeCatalog"]),
                normalize_lf=True,
            ),
        },
        "save": {
            "formatId": save_manifest.get("saveFormatId"),
            "schemaVersion": save_manifest.get("currentSchemaVersion"),
            "fixtureVersion": policy["fixtureVersion"],
            "fixtureManifestSha256": sha256_file(save_manifest_path),
        },
    }


def _extract_pattern_evidence(
    contract: dict[str, Any], stdout: str, stderr: str
) -> dict[str, str] | None:
    pattern = contract.get("evidencePattern")
    if not pattern:
        return {}
    match = re.search(pattern, (stdout or "") + "\n" + (stderr or ""), re.DOTALL)
    return match.groupdict() if match else None


def evaluate_contract_attempts(
    contract: dict[str, Any], attempts: list[dict[str, Any]]
) -> dict[str, Any]:
    base = {
        "id": contract["id"],
        "failureCode": contract["failureCode"],
        "attemptCount": len(attempts),
    }
    expected_attempts = int(contract.get("repeat", 1))
    if len(attempts) != expected_attempts:
        return {
            **base,
            "status": "stop_ship",
            "reasonCode": "missing_evidence",
            "evidence": {},
        }
    normalized: list[dict[str, Any]] = []
    for attempt in attempts:
        if int(attempt.get("exitCode", 1)) != 0:
            return {
                **base,
                "status": "stop_ship",
                "reasonCode": attempt.get("reasonCode", "command_failed"),
                "evidence": attempt.get("evidence") or {},
            }
        explicit = attempt.get("evidence")
        evidence = (
            explicit
            if isinstance(explicit, dict)
            else _extract_pattern_evidence(
                contract,
                str(attempt.get("stdout", "")),
                str(attempt.get("stderr", "")),
            )
        )
        if evidence is None:
            return {
                **base,
                "status": "stop_ship",
                "reasonCode": "missing_evidence",
                "evidence": {},
            }
        normalized.append(evidence)
    if any(evidence != normalized[0] for evidence in normalized[1:]):
        return {
            **base,
            "status": "stop_ship",
            "reasonCode": "nondeterministic_evidence",
            "evidence": {},
            "attemptEvidence": normalized,
        }
    return {
        **base,
        "status": "passed",
        "reasonCode": "contract_passed",
        "evidence": normalized[0],
    }


def compare_manual_results(
    results: list[dict[str, Any]],
    baseline: dict[str, Any],
    provenance: dict[str, Any] | None = None,
) -> dict[str, Any]:
    if baseline.get("schemaVersion") != 1 or not isinstance(
        baseline.get("results"), list
    ):
        return {"status": "stop_ship", "reasonCode": "manual_evidence_invalid"}
    expected = {row.get("id"): row for row in baseline["results"]}
    missing = sorted(result["id"] for result in results if result["id"] not in expected)
    if missing:
        return {
            "status": "stop_ship",
            "reasonCode": "manual_evidence_missing",
            "missingContracts": missing,
        }
    diverged = sorted(
        result["id"]
        for result in results
        if result.get("status") != expected[result["id"]].get("expectedStatus")
    )
    if diverged:
        return {
            "status": "stop_ship",
            "reasonCode": "manual_result_divergence",
            "divergedContracts": diverged,
        }
    provenance_differences: list[str] = []
    if provenance is not None:
        actual_material = {
            "unityVersion": provenance["unity"]["version"],
            "buildVersion": provenance["build"]["version"],
            "enabledSceneManifestSha256": provenance["scene"]["enabledManifestSha256"],
            "generatedSceneManifestSha256": provenance["scene"]["generatedManifestSha256"],
            "worldCatalogSha256": provenance["content"]["worldCatalogSha256"],
            "narrativeCatalogSha256": provenance["content"]["narrativeCatalogSha256"],
            "saveFormatId": provenance["save"]["formatId"],
            "saveSchemaVersion": provenance["save"]["schemaVersion"],
            "saveFixtureManifestSha256": provenance["save"]["fixtureManifestSha256"],
        }
        manual_material = baseline.get("materialProvenance")
        if not isinstance(manual_material, dict):
            return {
                "status": "stop_ship",
                "reasonCode": "manual_evidence_missing",
                "missingFields": ["materialProvenance"],
            }
        provenance_differences = sorted(
            key
            for key in set(actual_material) | set(manual_material)
            if actual_material.get(key) != manual_material.get(key)
        )
    if provenance_differences:
        return {
            "status": "stop_ship",
            "reasonCode": "manual_material_divergence",
            "divergedFields": provenance_differences,
        }
    return {
        "status": "passed",
        "reasonCode": "automated_manual_equivalent",
        "baselineId": baseline.get("baselineId"),
    }


def _expand_command(command: list[str], context: dict[str, str]) -> list[str]:
    return [str(part).format_map(context) for part in command]


def _deterministic_environment(policy: dict[str, Any]) -> dict[str, str]:
    environment = os.environ.copy()
    environment.update(
        {
            "AL_QA_SEED": str(policy["seed"]),
            "AL_QA_CLOCK_UTC": str(policy["clockUtc"]),
            "AL_QA_FIXTURE_VERSION": str(policy["fixtureVersion"]),
            "PYTHONHASHSEED": str(policy["seed"]),
            "TZ": "UTC",
        }
    )
    return environment


def _run_process(
    command: list[str], repo_root: Path, environment: dict[str, str]
) -> dict[str, Any]:
    completed = subprocess.run(
        command,
        cwd=repo_root,
        check=False,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=environment,
    )
    return {
        "exitCode": completed.returncode,
        "stdout": completed.stdout,
        "stderr": completed.stderr,
    }


def _wait_for_unity_release(project_root: Path, timeout_seconds: int = 90) -> bool:
    lock = project_root / "Temp/UnityLockfile"
    deadline = time.monotonic() + timeout_seconds
    while lock.exists() and time.monotonic() < deadline:
        time.sleep(0.5)
    return not lock.exists()


def _parse_unity_results(results_path: Path) -> dict[str, str]:
    try:
        root = ET.parse(results_path).getroot()
    except (OSError, ET.ParseError) as error:
        raise QaContractError(f"QA_UNITY_RESULTS_INVALID: {results_path}: {error}") from error
    cases = sorted(
        (
            str(node.attrib.get("fullname") or node.attrib.get("name") or ""),
            str(node.attrib.get("result") or ""),
        )
        for node in root.iter("test-case")
    )
    total = int(root.attrib.get("total", root.attrib.get("testcasecount", len(cases))))
    passed = int(root.attrib.get("passed", sum(result == "Passed" for _, result in cases)))
    failed = int(root.attrib.get("failed", sum(result == "Failed" for _, result in cases)))
    skipped = int(root.attrib.get("skipped", root.attrib.get("inconclusive", 0)))
    return {
        "total": str(total),
        "passed": str(passed),
        "failed": str(failed),
        "skipped": str(skipped),
        "fingerprint": sha256_bytes(canonical_json(cases)),
    }


def _run_unity_test(
    contract: dict[str, Any],
    attempt_index: int,
    repo_root: Path,
    output_dir: Path,
    unity_exe: Path | None,
    environment: dict[str, str],
) -> dict[str, Any]:
    if unity_exe is None or not unity_exe.is_file():
        return {
            "exitCode": 2,
            "stdout": "",
            "stderr": "Unity executable is required for full QA",
            "reasonCode": "prerequisite_missing",
        }
    results_path = output_dir / "xml" / f"{contract['id']}-{attempt_index}.xml"
    unity_log = output_dir / "unity" / f"{contract['id']}-{attempt_index}.log"
    results_path.parent.mkdir(parents=True, exist_ok=True)
    unity_log.parent.mkdir(parents=True, exist_ok=True)
    command = [
        str(unity_exe.resolve()),
        "-batchmode",
        "-nographics",
        "-projectPath",
        str((repo_root / "unity").resolve()),
        "-runTests",
        "-testPlatform",
        contract["platform"],
        "-assemblyNames",
        contract["assembly"],
        "-testFilter",
        contract["filter"],
        "-testResults",
        str(results_path.resolve()),
        "-logFile",
        str(unity_log.resolve()),
    ]
    attempt = _run_process(command, repo_root, environment)
    _wait_for_unity_release(repo_root / "unity")
    if not results_path.is_file() and attempt["exitCode"] == 0:
        attempt = _run_process(command, repo_root, environment)
        _wait_for_unity_release(repo_root / "unity")
    attempt["unityLog"] = str(unity_log)
    attempt["resultsXml"] = str(results_path)
    if results_path.is_file():
        evidence = _parse_unity_results(results_path)
        attempt["evidence"] = evidence
        if int(evidence["failed"]) != 0 or int(evidence["total"]) == 0:
            attempt["exitCode"] = attempt["exitCode"] or 1
    return attempt


def _run_build_smoke(
    repo_root: Path,
    output_dir: Path,
    unity_exe: Path | None,
    environment: dict[str, str],
) -> dict[str, Any]:
    if unity_exe is None or not unity_exe.is_file():
        return {
            "exitCode": 2,
            "stdout": "",
            "stderr": "Unity executable is required for build smoke",
            "reasonCode": "prerequisite_missing",
        }
    manifest = output_dir / "build" / "windows64-development.json"
    manifest.parent.mkdir(parents=True, exist_ok=True)
    command = [
        sys.executable,
        str(repo_root / "tools/reproducible_build.py"),
        "--repo-root",
        str(repo_root),
        "build",
        "--target",
        "windows64-development",
        "--unity-exe",
        str(unity_exe.resolve()),
        "--manifest",
        str(manifest.resolve()),
    ]
    attempt = _run_process(command, repo_root, environment)
    attempt["buildManifest"] = str(manifest)
    if manifest.is_file():
        payload = _load_json(manifest)
        attempt["evidence"] = {
            "status": str(payload.get("status") or ""),
            "manifestSha256": str(payload.get("manifestSha256") or ""),
            "artifactTreeSha256": str(
                payload.get("artifacts", {}).get("reproducibleTreeSha256") or ""
            ),
        }
        if payload.get("status") != "succeeded":
            attempt["exitCode"] = attempt["exitCode"] or 1
    return attempt


def _run_packaged_narrative(
    repo_root: Path,
    output_dir: Path,
    environment: dict[str, str],
) -> dict[str, Any]:
    manifest = output_dir / "build/windows64-development.json"
    player = repo_root / "unity/Builds/Validation/Windows64/AnotherLifeUnity.exe"
    evidence_path = output_dir / "narrative/packaged-evidence.json"
    evidence_path.parent.mkdir(parents=True, exist_ok=True)
    command = [
        sys.executable,
        str(repo_root / "tools/narrative/packaged_narrative_smoke.py"),
        "--player",
        str(player),
        "--output",
        str(evidence_path),
        "--build-manifest",
        str(manifest),
    ]
    if not manifest.is_file() or not player.is_file():
        return {
            "exitCode": 2,
            "stdout": "",
            "stderr": "successful build-smoke artifacts are required",
            "reasonCode": "prerequisite_missing",
        }
    attempt = _run_process(command, repo_root, environment)
    try:
        payload = json.loads(attempt["stdout"])
    except json.JSONDecodeError:
        payload = {}
    packaged = payload.get("evidence", {}) if isinstance(payload, dict) else {}
    attempt["evidence"] = {
        "status": str(payload.get("status") or ""),
        "reasonCode": str(payload.get("reasonCode") or ""),
        "enabledSceneManifestSha256": str(
            packaged.get("enabledSceneManifestSha256") or ""
        ),
        "generatedSceneManifestSha256": str(
            packaged.get("generatedSceneManifestSha256") or ""
        ),
        "narrativeCatalogSha256": str(packaged.get("narrativeCatalogSha256") or ""),
        "entryQuestId": str(packaged.get("entryQuestId") or ""),
        "resumedQuestStateId": str(packaged.get("resumedQuestStateId") or ""),
    }
    if payload.get("status") != "passed":
        attempt["exitCode"] = attempt["exitCode"] or 1
    return attempt


def _fixture_attempt(contract: dict[str, Any], provenance: dict[str, Any]) -> dict[str, Any]:
    return {
        "exitCode": 0,
        "stdout": f"fixture {contract['id']} passed\n",
        "stderr": "",
        "evidence": {
            "fixtureVersion": provenance["save"]["fixtureVersion"],
            "contract": contract["id"],
        },
    }


def _write_attempt_log(
    output_dir: Path, contract_id: str, index: int, attempt: dict[str, Any]
) -> str:
    relative = Path("logs") / f"{contract_id}-attempt-{index}.log"
    path = output_dir / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    command = attempt.get("command") or []
    stdout = str(attempt.get("stdout", "")).replace("\r\n", "\n").rstrip("\n")
    stderr = str(attempt.get("stderr", "")).replace("\r\n", "\n").rstrip("\n")
    text = "command=" + json.dumps(command, ensure_ascii=False) + "\n"
    text += f"exitCode={attempt.get('exitCode')}\n"
    text += "--- stdout ---\n"
    if stdout:
        text += stdout + "\n"
    text += "--- stderr ---\n"
    if stderr:
        text += stderr + "\n"
    path.write_text(text, encoding="utf-8", newline="\n")
    return relative.as_posix()


def _write_junit(report: dict[str, Any], path: Path) -> None:
    failures = sum(result["status"] != "passed" for result in report["contracts"])
    suite = ET.Element(
        "testsuite",
        {
            "name": report["suiteId"],
            "tests": str(len(report["contracts"])),
            "failures": str(failures),
            "errors": "0",
            "time": "0",
        },
    )
    for result in report["contracts"]:
        case = ET.SubElement(
            suite,
            "testcase",
            {"classname": report["suiteId"], "name": result["id"], "time": "0"},
        )
        if result["status"] != "passed":
            failure = ET.SubElement(
                case,
                "failure",
                {
                    "type": result["failureCode"],
                    "message": result["reasonCode"],
                },
            )
            failure.text = json.dumps(result.get("evidence", {}), sort_keys=True)
    path.parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(suite).write(path, encoding="utf-8", xml_declaration=True)


def _write_report(payload: dict[str, Any], path: Path) -> dict[str, Any]:
    unsigned = copy.deepcopy(payload)
    unsigned.pop("reportSha256", None)
    report = copy.deepcopy(unsigned)
    report["reportSha256"] = sha256_bytes(canonical_json(unsigned))
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_bytes(canonical_json(report))
    os.replace(temporary, path)
    return report


def verify_report(path: Path) -> dict[str, Any]:
    report = _load_json(path)
    recorded = report.get("reportSha256")
    unsigned = copy.deepcopy(report)
    unsigned.pop("reportSha256", None)
    actual = sha256_bytes(canonical_json(unsigned))
    if not isinstance(recorded, str) or recorded != actual:
        raise QaContractError(f"QA_REPORT_HASH: invalid report digest: {path}")
    return report


def contract_execution_order(profile_name: str, profile: list[str]) -> list[str]:
    """Run the clean-tree build preflight before Unity can normalize tracked settings."""
    ordered = list(profile)
    if profile_name == "full":
        ordered.remove("build-smoke")
        ordered.insert(ordered.index("play-mode"), "build-smoke")
    return ordered


def run_suite(
    repo_root: Path,
    policy: dict[str, Any],
    profile_name: str,
    output_dir: Path,
    *,
    unity_exe: Path | None = None,
    inject_failure: str | None = None,
    process_runner: Callable[[list[str], Path, dict[str, str]], dict[str, Any]] = _run_process,
) -> dict[str, Any]:
    repo_root = Path(repo_root).resolve()
    output_dir = Path(output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    provenance = collect_repository_provenance(repo_root, policy)
    profile = policy.get("profiles", {}).get(profile_name)
    if not isinstance(profile, list):
        raise QaContractError(f"QA_PROFILE_UNKNOWN: {profile_name}")
    contract_index = {contract["id"]: contract for contract in policy["contracts"]}
    environment = _deterministic_environment(policy)
    context = {"python": sys.executable, "repo": str(repo_root)}
    results: list[dict[str, Any]] = []
    log_paths: list[str] = []

    for contract_id in contract_execution_order(profile_name, profile):
        contract = contract_index[contract_id]
        attempts: list[dict[str, Any]] = []
        for attempt_index in range(1, int(contract["repeat"]) + 1):
            if contract_id == inject_failure:
                attempt = {
                    "exitCode": 97,
                    "stdout": "",
                    "stderr": f"intentional failure fixture for {contract_id}",
                    "reasonCode": "intentional_failure_fixture",
                    "evidence": {"contract": contract_id},
                }
            elif profile_name == "contract":
                attempt = _fixture_attempt(contract, provenance)
            elif contract["kind"] == "subprocess":
                command = _expand_command(contract["command"], context)
                attempt = process_runner(command, repo_root, environment)
                attempt["command"] = command
            elif contract["kind"] == "unity-test":
                attempt = _run_unity_test(
                    contract,
                    attempt_index,
                    repo_root,
                    output_dir,
                    unity_exe,
                    environment,
                )
            elif contract["kind"] == "build-smoke":
                attempt = _run_build_smoke(
                    repo_root, output_dir, unity_exe, environment
                )
            elif contract["kind"] == "packaged-narrative":
                attempt = _run_packaged_narrative(repo_root, output_dir, environment)
            else:
                attempt = {
                    "exitCode": 2,
                    "stdout": "",
                    "stderr": f"unsupported contract kind: {contract['kind']}",
                    "reasonCode": "policy_invalid",
                }
            log_path = _write_attempt_log(
                output_dir, contract_id, attempt_index, attempt
            )
            log_paths.append(log_path)
            attempt["log"] = log_path
            attempt["logSha256"] = sha256_file(output_dir / log_path)
            attempts.append(attempt)
        result = evaluate_contract_attempts(contract, attempts)
        result["attempts"] = [
            {
                "exitCode": attempt.get("exitCode"),
                "log": attempt["log"],
                "logSha256": attempt["logSha256"],
            }
            for attempt in attempts
        ]
        results.append(result)

    result_index = {result["id"]: result for result in results}
    results = [result_index[contract_id] for contract_id in profile]

    build_result = next(
        (result for result in results if result["id"] == "build-smoke"),
        None,
    )
    if build_result and build_result["status"] == "passed":
        provenance["build"]["manifestSha256"] = build_result["evidence"].get(
            "manifestSha256"
        )
        provenance["build"]["artifactTreeSha256"] = build_result["evidence"].get(
            "artifactTreeSha256"
        )

    manual_baseline_path = _required_path(repo_root, policy["manualBaseline"])
    manual = compare_manual_results(
        results, _load_json(manual_baseline_path), provenance
    )
    suite_status = (
        "passed"
        if all(result["status"] == "passed" for result in results)
        and manual["status"] == "passed"
        else "stop_ship"
    )
    run_identity = derive_run_identity(
        policy["fixtureVersion"],
        int(policy["seed"]),
        str(policy["clockUtc"]),
        provenance["sourceRevision"],
    )
    report = {
        "schemaVersion": 1,
        "suiteId": policy["suiteId"],
        "profile": profile_name,
        "status": suite_status,
        "run": {
            "id": run_identity,
            "seed": policy["seed"],
            "clockUtc": policy["clockUtc"],
            "fixtureVersion": policy["fixtureVersion"],
        },
        "environment": {
            "os": platform.system(),
            "architecture": platform.machine(),
            "pythonVersion": platform.python_version(),
            "unityExecutable": unity_exe.resolve().as_posix() if unity_exe else None,
            "variables": {
                "AL_QA_SEED": str(policy["seed"]),
                "AL_QA_CLOCK_UTC": str(policy["clockUtc"]),
                "AL_QA_FIXTURE_VERSION": str(policy["fixtureVersion"]),
                "PYTHONHASHSEED": str(policy["seed"]),
                "TZ": "UTC",
            },
        },
        "provenance": provenance,
        "authorityReferences": policy.get("authorityReferences", {}),
        "contracts": results,
        "manualComparison": manual,
        "stopShipPolicy": {
            "statuses": [
                "material_divergence",
                "flake",
                "missing_evidence",
                "nondeterministic_manifest",
                "contract_failure",
            ],
            "action": "return_nonzero_and_block_release_until_new_evidence_or_owner-approved_baseline",
        },
        "artifacts": {
            "root": ".",
            "report": "report.json",
            "junit": "junit.xml",
            "logs": log_paths,
        },
    }
    report_path = output_dir / "report.json"
    written = _write_report(report, report_path)
    _write_junit(written, output_dir / "junit.xml")
    return written


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    parser.add_argument("--profile", default="full")
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--unity-exe", type=Path)
    parser.add_argument("--inject-failure", choices=REQUIRED_CONTRACTS)
    args = parser.parse_args(argv)
    try:
        policy = load_policy(args.policy)
        output_dir = args.output_dir or Path(args.repo_root) / policy["artifactRoot"]
        report = run_suite(
            args.repo_root,
            policy,
            args.profile,
            output_dir,
            unity_exe=args.unity_exe,
            inject_failure=args.inject_failure,
        )
    except (QaContractError, OSError, subprocess.SubprocessError) as error:
        print(f"deterministic-qa: {error}", file=sys.stderr)
        return 2
    print(
        f"QA_{report['status'].upper()} profile={report['profile']} "
        f"run={report['run']['id']} report={Path(output_dir) / 'report.json'}"
    )
    for result in report["contracts"]:
        if result["status"] != "passed":
            print(
                f"{result['failureCode']}: {result['id']}: {result['reasonCode']}",
                file=sys.stderr,
            )
    if report["manualComparison"]["status"] != "passed":
        print(
            "QA_MANUAL_COMPARISON: "
            + report["manualComparison"].get("reasonCode", "failed"),
            file=sys.stderr,
        )
    return 0 if report["status"] == "passed" else 2


if __name__ == "__main__":
    raise SystemExit(main())
