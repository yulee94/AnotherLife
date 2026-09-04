#!/usr/bin/env python
"""Run and package the disposable Amazon GameLift Servers spike."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PASS_SCENARIOS: set[str] = set()
ALL_SCENARIOS = tuple(f"SCN-{index:02d}" for index in range(1, 17))
FIXTURE_SEED = "anotherlife-mmo-bakeoff-v1"
HOME_REGION = "ap-northeast-2"
FORBIDDEN_REGION = "us-east-1"
BLOCKER = (
    "No AWS credential resolved through the standard SDK provider chain; authenticated "
    "GameLift inventory, resource lifecycle, quota, regional-copy, credential-rotation, "
    "fault-injection, and teardown API scenarios could not run."
)
PROHIBITED_PACKET_PATTERNS = {
    "redacted-secret-marker": re.compile(r"«redacted:"),
    "aws-access-key": re.compile(r"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b"),
    "aws-credential-field": re.compile(
        r"(?i)aws_(?:access_key_id|secret_access_key|session_token)\s*[:=]"
    ),
    "private-key": re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    "credential-assignment": re.compile(
        r"(?i)\b(?:password|passwd|api[_-]?key|secret|token)\s*[:=]\s*\S+"
    ),
    "windows-absolute-path": re.compile(r"(?i)(?<![A-Za-z0-9])[a-z]:[\\/]"),
    "posix-absolute-path": re.compile(
        r"(?<![A-Za-z0-9:/])/(?!/)[A-Za-z0-9._~+-]+(?:/[A-Za-z0-9._~+-]+)*"
    ),
    "email-address": re.compile(
        r"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b"
    ),
    "aws-account-arn": re.compile(
        r"\barn:aws(?:-[a-z0-9-]+)?:[^:\s]*:[^:\s]*:\d{12}:[^\s]+"
    ),
}


def canonical_json(value: Any) -> bytes:
    return json.dumps(
        value, sort_keys=True, separators=(",", ":"), ensure_ascii=False
    ).encode("utf-8")


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def passed_test_count(output: str) -> int:
    """Return the substantive Cargo test count from one successful test log."""
    matches = [int(value) for value in re.findall(r"(\d+) passed; \d+ failed", output)]
    if not matches:
        raise ValueError("Cargo test output has no pass/fail summary")
    return max(matches)


def preflight_blocker(preflight: dict[str, Any]) -> str:
    """Describe the exact provider-access state without inferring credentials."""
    if not preflight.get("credential_resolved"):
        return BLOCKER
    if not preflight.get("sts_authenticated"):
        return "An AWS credential resolved, but STS authentication failed; no GameLift scenario could run."
    if not preflight.get("gamelift_inventory_authenticated"):
        return "AWS STS authentication succeeded, but GameLift inventory access failed; no scenario could run."
    return (
        "Authenticated GameLift inventory succeeded, but this disposable runner has no explicit "
        "sandbox resource authorization or live scenario executor; provider scenarios were not run."
    )


def sanitized_command(command: list[str], repository_root: Path) -> str:
    """Render a replayable command without machine-specific absolute paths."""
    executable = Path(command[0]).name
    rendered = ["cargo" if executable.lower() == "cargo.exe" else executable]
    root = repository_root.resolve()
    for argument in command[1:]:
        path = Path(argument)
        if path.is_absolute():
            try:
                rendered.append(path.resolve().relative_to(root).as_posix())
            except ValueError:
                rendered.append(f"<external:{path.name}>")
        else:
            rendered.append(argument.replace("\\", "/"))
    return " ".join(rendered)


def sanitized_text(value: str, repository_root: Path) -> str:
    """Remove repository and user-home paths from retained command output."""
    sanitized = value
    for path, replacement in (
        (repository_root.resolve(), "<repository-root>"),
        (Path.home().resolve(), "<user-home>"),
    ):
        variants = {str(path), path.as_posix(), str(path).replace("\\", "/")}
        for variant in variants:
            sanitized = sanitized.replace(variant, replacement)
    return sanitized


def packet_scan_findings(packet_root: Path) -> list[str]:
    """Return prohibited secret, PII, and absolute-path pattern names."""
    findings: list[str] = []
    for path in packet_root.rglob("*"):
        if path.is_file():
            content = path.read_text(encoding="utf-8", errors="replace")
            findings.extend(
                name
                for name, pattern in PROHIBITED_PACKET_PATTERNS.items()
                if pattern.search(content)
            )
    return findings


def reject_prohibited_packet(packet_root: Path) -> None:
    """Remove a rejected packet so prohibited material is not retained."""
    shutil.rmtree(packet_root, ignore_errors=True)
    raise ValueError("secret scan found prohibited packet markers")


def write_json(path: Path, value: Any) -> bytes:
    payload = json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False).encode(
        "utf-8"
    ) + b"\n"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)
    return payload


def write_text(path: Path, value: str) -> bytes:
    payload = value.encode("utf-8")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)
    return payload


def source_manifest(repository_root: Path) -> dict[str, str]:
    relative_paths = (
        "server/Cargo.toml",
        "server/Cargo.lock",
        "server/al_server_core/src/provider_contracts.rs",
        "server/al_server_core/src/domain_contracts.rs",
        "server/al_provider_adapter_stub/src/lib.rs",
        "server/al_provider_adapter_gamelift_spike/Cargo.toml",
        "server/al_provider_adapter_gamelift_spike/README.md",
        "server/al_provider_adapter_gamelift_spike/src/lib.rs",
        "server/al_provider_adapter_gamelift_spike/tests/adapter_contract.rs",
        "tools/architecture/run_gamelift_spike.py",
        "tools/architecture/test_run_gamelift_spike.py",
    )
    return {
        path: sha256_bytes((repository_root / path).read_bytes())
        for path in relative_paths
    }


def blocked_reason(scenario_id: str, provider_blocker: str) -> str:
    details = {
        "SCN-01": "authenticated GameLift region, quota, credential, resource, and data-path inventory is unavailable",
        "SCN-02": "no authenticated queue, fleet, build, or safe lifecycle resource is available",
        "SCN-03": "no authenticated GameLift session placement exists for the common identity/session cycle",
        "SCN-07": "the authorized common quota ceiling is unknown and no authenticated request ladder may start",
        "SCN-08": "no established GameLift-owned data plane or approved provider outage seam exists",
        "SCN-09": "no candidate fleet exists for launch, partial lifecycle, stuck-drain, or cancellation injection",
        "SCN-10": "provider stores, logs, backup, export, support, and deletion locations cannot be inspected anonymously",
        "SCN-11": "neutral fencing tests pass, but no GameLift allocation exists for the full candidate route/owner injection",
        "SCN-12": "no durable authenticated candidate operation exists to reconcile across adapter restart",
        "SCN-13": "no candidate credential is resolved, so rotation, revocation, and old-credential denial cannot be exercised",
        "SCN-14": "local sanitized observations pass, but GameLift telemetry loss, export, retention, and deletion are unobservable",
        "SCN-16": "no authenticated account inventory can prove provider resource deletion, retention, or residual constraints",
    }
    return details.get(scenario_id, provider_blocker)


def write_packet(
    *,
    repository_root: Path,
    packet_root: Path,
    run_id: str,
    started_utc: str,
    ended_utc: str,
    driver_commit: str,
    preflight: dict[str, Any],
    local_contract_log: str,
    core_test_log: str,
    command_log: list[str],
    automated_duration_seconds: float,
) -> Path:
    """Write one immutable-shaped blocked packet and return its run record path."""
    packet_root.mkdir(parents=True, exist_ok=True)
    if any(packet_root.iterdir()):
        raise ValueError(f"packet directory is not empty: {packet_root}")
    local_contract_log = sanitized_text(local_contract_log, repository_root)
    core_test_log = sanitized_text(core_test_log, repository_root)
    command_log = [sanitized_text(command, repository_root) for command in command_log]

    adapter_source = (
        repository_root
        / "server"
        / "al_provider_adapter_gamelift_spike"
        / "src"
        / "lib.rs"
    ).read_bytes()
    artifact_manifest = source_manifest(repository_root)
    server_artifact_fingerprint = sha256_bytes(canonical_json(artifact_manifest))
    adapter_fingerprint = sha256_bytes(adapter_source)
    provider_blocker = preflight_blocker(preflight)

    configuration = {
        "environment": "nonproduction_spike",
        "candidate": "amazon_gamelift",
        "enabled": False,
        "home_region": HOME_REGION,
        "forbidden_region": FORBIDDEN_REGION,
        "realm_ids": ["realm-1", "realm-2", "realm-3", "realm-4"],
        "provider_resource_references": [],
        "mutation_guard": "disabled_without_explicit_authenticated_sandbox_setup",
        "production_permitted": False,
    }
    configuration_payload = canonical_json(configuration)
    configuration_hash = sha256_bytes(configuration_payload)
    regional_state = {
        "fixture_seed": FIXTURE_SEED,
        "realm_ids": configuration["realm_ids"],
        "authoritative_state": "synthetic-neutral-unchanged",
        "provider_identifiers": [],
    }
    regional_state_hash = sha256_bytes(canonical_json(regional_state))

    workload_manifest = {
        "driver_commit": driver_commit,
        "server_artifact_fingerprint": server_artifact_fingerprint,
        "adapter_contract_version": "MMO-CONTRACTS-v1.0.0",
        "workload_manifest_hash": "sha256-of-this-canonical-manifest",
        "configuration_shape": {
            "logical_regions": ["home_region", "forbidden_region"],
            "realm_count": 4,
            "candidate_section": "isolated_disposable_adapter",
        },
        "synthetic_fixture_seed": FIXTURE_SEED,
        "request_envelopes": [
            "identity",
            "placement",
            "lifecycle",
            "gameplay",
            "economy",
            "social",
            "operations",
        ],
        "operation_ids": "deterministic-scenario-and-repetition-identities",
        "payload_bytes": {"source": "canonical-synthetic-fixtures"},
        "topology": {
            "realms": 4,
            "logical_regions": ["home_region", "forbidden_region"],
        },
        "fault_schedule": [
            "baseline",
            "inject",
            "observe",
            "recover",
            "rollback",
        ],
        "observation_schema": "MMO-BAKEOFF-v1.0.0",
        "repetitions": {"excluded_warmups": 1, "measured": 3},
        "warmup_rule": "exclude_exactly_one_identical_warmup",
        "teardown_assertions": [
            "neutral_restore",
            "regional_state_unchanged",
            "no_provider_authority",
        ],
    }

    artifacts: dict[str, tuple[bytes, list[str], str]] = {}

    def add_json(path: str, value: Any, scenarios: list[str]) -> None:
        payload = write_json(packet_root / path, value)
        artifacts[path] = (payload, scenarios, "application/json")

    def add_text(path: str, value: str, scenarios: list[str]) -> None:
        payload = write_text(packet_root / path, value)
        artifacts[path] = (payload, scenarios, "text/plain")

    add_json("workload-manifest.json", workload_manifest, [])
    workload_manifest_hash = sha256_bytes(artifacts["workload-manifest.json"][0])
    add_json("raw/aws-preflight.json", preflight, ["SCN-01"])
    add_json("raw/server-artifact-manifest.json", artifact_manifest, ["SCN-02", "SCN-15"])
    add_json("raw/configuration.json", configuration, ["SCN-01", "SCN-15"])
    add_json("raw/regional-state.json", regional_state, ["SCN-03", "SCN-15"])
    add_text(
        "logs/local-contract-tests.log",
        local_contract_log,
        sorted(PASS_SCENARIOS),
    )
    add_text("logs/core-tests.log", core_test_log, ["SCN-11", "SCN-15"])
    add_json(
        "metrics/local-contract-tests.json",
        {
            "classification": "local_contract_measurement_not_provider_scale_evidence",
            "passed_tests": passed_test_count(local_contract_log),
            "failed_tests": 0,
            "covered_scenarios": sorted(PASS_SCENARIOS),
        },
        sorted(PASS_SCENARIOS),
    )
    add_json(
        "residency-inventory.json",
        {
            "status": "unknown_measurement_required",
            "home_region": HOME_REGION,
            "forbidden_region": FORBIDDEN_REGION,
            "authenticated_inventory": False,
            "authoritative_data_uploaded": False,
            "unknown_paths": [
                "control_plane",
                "logs",
                "backup",
                "support",
                "export",
                "deletion",
            ],
        },
        ["SCN-01", "SCN-10", "SCN-16"],
    )
    add_json(
        "quota-inventory.json",
        {
            "classification": "unknown_measurement_required",
            "authenticated_inventory": False,
            "ladder_started": False,
            "limits": [
                {"id": f"UL-{index:02d}", "status": "unknown_measurement_required"}
                for index in range(1, 10)
            ],
            "production_capacity_claim": None,
        },
        ["SCN-01", "SCN-07"],
    )
    add_json(
        "credential-inventory.json",
        {
            "credential_resolved": bool(preflight.get("credential_resolved")),
            "credential_material_retained": False,
            "rotation_attempted": False,
            "old_credential_denial_attempted": False,
            "status": "blocked_no_resolved_candidate_credential",
        },
        ["SCN-01", "SCN-13", "SCN-16"],
    )
    add_json(
        "teardown-inventory.json",
        {
            "candidate_resources_created": 0,
            "candidate_mutations_attempted": 0,
            "known_residual_resources": [],
            "authenticated_residual_inventory": False,
            "status": "blocked_account_inventory_unavailable",
            "neutral_path_required_provider_resource": False,
        },
        ["SCN-15", "SCN-16"],
    )
    add_json(
        "raw/vendor-sources.json",
        {
            "retrieved_utc": ended_utc,
            "classification": "vendor_documented_fact_not_measured_limit",
            "sources": [
                {
                    "url": "https://docs.aws.amazon.com/gameliftservers/latest/apireference/API_StartGameSessionPlacement.html",
                    "scope": "placement request, placement identity, queue, and completion states",
                },
                {
                    "url": "https://docs.aws.amazon.com/gameliftservers/latest/apireference/API_GameSession.html",
                    "scope": "game-session lifecycle and documented retention statements",
                },
                {
                    "url": "https://docs.aws.amazon.com/gameliftservers/latest/apireference/API_FleetAttributes.html",
                    "scope": "fleet attributes and compute types",
                },
            ],
        },
        ["SCN-01", "SCN-02", "SCN-16"],
    )
    add_text("commands.txt", "\n".join(command_log) + "\n", ALL_SCENARIOS.copy() if isinstance(ALL_SCENARIOS, list) else list(ALL_SCENARIOS))
    add_text(
        "environment.txt",
        "candidate=amazon_gamelift\n"
        f"home_region={HOME_REGION}\n"
        f"forbidden_region={FORBIDDEN_REGION}\n"
        f"credential_resolved={str(bool(preflight.get('credential_resolved'))).lower()}\n"
        "synthetic_only=true\nproduction_permitted=false\n",
        ["SCN-01"],
    )
    add_text(
        "limitations.md",
        "Amazon GameLift Servers spike limitations\n\n"
        f"- {provider_blocker}\n"
        "- No provider resource or synthetic authoritative data was created or uploaded.\n"
        "- Local adapter tests are contract evidence only, not GameLift availability, latency, quota, cost, residency, or capacity evidence.\n"
        "- Every provider-dependent scenario remains blocked rather than inferred from documentation or the local fake transport.\n"
        "- No provider recommendation is made before paired PlayFab evidence and owner review.\n",
        list(ALL_SCENARIOS),
    )
    rollback = {
        "status": "blocked",
        "candidate_was_enabled": False,
        "candidate_resources_created": 0,
        "neutral_configuration_hash_before": configuration_hash,
        "neutral_configuration_hash_after": configuration_hash,
        "regional_state_hash_before": regional_state_hash,
        "regional_state_hash_after": regional_state_hash,
        "core_tests": "pass",
        "provider_dependency_in_core": False,
        "provider_process_authoritative": False,
    }
    add_json("rollback.json", rollback, ["SCN-04", "SCN-05", "SCN-06", "SCN-15"])

    if packet_scan_findings(packet_root):
        reject_prohibited_packet(packet_root)
    add_text(
        "secret-scan.txt",
        "status=pass\npattern_set=packet-secret-and-path-markers-v2\nfindings=0\n",
        ["SCN-13", "SCN-14", "SCN-16"],
    )

    scenario_results = []
    for index, scenario_id in enumerate(ALL_SCENARIOS, start=1):
        passed = scenario_id in PASS_SCENARIOS
        scenario_results.append(
            {
                "scenario_id": scenario_id,
                "status": "pass" if passed else "blocked",
                "started_utc": started_utc,
                "ended_utc": ended_utc,
                "operation_ids": [f"gamelift-{scenario_id.lower()}-operation-{index}"],
                "correlation_ids": [f"gamelift-{run_id}-{scenario_id.lower()}"],
                "stable_result_counts": (
                    {"attempted": 1, "succeeded": 1}
                    if passed
                    else {"attempted": 0, "blocked": 1}
                ),
                "measurement_handles": (
                    ["metrics/local-contract-tests.json"] if passed else []
                ),
                "raw_evidence_handles": (
                    ["logs/local-contract-tests.log"] if passed else []
                ),
                "limitations": (
                    ["Local contract evidence only; no provider scale claim."]
                    if passed
                    else ["Provider-dependent observations were not available."]
                ),
                "blockers": [] if passed else [blocked_reason(scenario_id, provider_blocker)],
                "contract_violations": [],
                "rollback_status": "blocked",
                "rollback_evidence_handles": [],
            }
        )

    manifest = []
    for path, (payload, scenarios, content_type) in sorted(artifacts.items()):
        manifest.append(
            {
                "path": path,
                "sha256": sha256_bytes(payload),
                "bytes": len(payload),
                "content_type": content_type,
                "classification": "sanitized_common",
                "scenario_ids": scenarios,
            }
        )

    record = {
        "record_schema_version": "1.0.0",
        "plan_id": "MMO-BAKEOFF-v1.0.0",
        "candidate_id": "amazon_gamelift",
        "spike_task_id": "t_ff702849",
        "run_id": run_id,
        "run_status": "blocked",
        "claim_class": "observed_sandbox_fact",
        "started_utc": started_utc,
        "ended_utc": ended_utc,
        "driver_commit": driver_commit,
        "driver_commit_role": "repository_reference",
        "driver_source_state": "source_manifest_fingerprinted",
        "server_artifact_fingerprint": server_artifact_fingerprint,
        "adapter_fingerprint": adapter_fingerprint,
        "configuration_hash": configuration_hash,
        "workload_manifest_hash": workload_manifest_hash,
        "region_id": HOME_REGION,
        "realm_ids": configuration["realm_ids"],
        "synthetic_fixture_seed": FIXTURE_SEED,
        "scenario_results": scenario_results,
        "data_residency_inventory": ["residency-inventory.json"],
        "quota_inventory": ["quota-inventory.json"],
        "credential_inventory": ["credential-inventory.json"],
        "raw_evidence_manifest": manifest,
        "limitations": [
            "Local contract observations are not GameLift sandbox scale, quota, latency, cost, or residency evidence."
        ],
        "blockers": [provider_blocker],
        "contract_violations": [],
        "rollback_result": {
            "status": "blocked",
            "neutral_configuration_hash_before": configuration_hash,
            "neutral_configuration_hash_after": configuration_hash,
            "regional_state_hash_before": regional_state_hash,
            "regional_state_hash_after": regional_state_hash,
            "core_tests": "pass",
            "evidence_handles": ["rollback.json"],
        },
        "teardown_inventory": ["teardown-inventory.json"],
        "operator_effort": {
            "automated_duration_seconds": automated_duration_seconds,
            "manual_duration_seconds": 0,
            "manual_steps": [],
            "support_interactions": [],
        },
        "secret_scan_result": {
            "status": "pass",
            "command": "packet secret and absolute-path pattern scan v2",
            "evidence_handle": "secret-scan.txt",
            "findings": [],
        },
        "selection_recommendation": None,
        "notes": [
            "No provider recommendation is made before paired PlayFab evidence and owner review.",
            "This runner created no provider resources.",
        ],
    }
    record_path = packet_root / "run-record.json"
    write_json(record_path, record)
    if packet_scan_findings(packet_root):
        reject_prohibited_packet(packet_root)
    return record_path


def run_command(command: list[str], cwd: Path) -> tuple[str, float]:
    started = time.monotonic()
    result = subprocess.run(
        command,
        cwd=cwd,
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
    )
    duration = time.monotonic() - started
    if result.returncode != 0:
        raise RuntimeError(
            f"command failed with exit {result.returncode}: {' '.join(command)}\n{result.stdout}"
        )
    return result.stdout, duration


def cargo_path() -> str:
    discovered = shutil.which("cargo")
    if discovered:
        return discovered
    candidate = Path.home() / ".cargo" / "bin" / "cargo.exe"
    if candidate.is_file():
        return str(candidate)
    raise RuntimeError("cargo is unavailable")


def require_committed_source_state(repository_root: Path, driver_commit: str) -> None:
    """Require every fingerprinted source to be present in the recorded commit."""
    head = subprocess.run(
        ["git", "rev-parse", "HEAD"],
        cwd=repository_root,
        check=True,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    ).stdout.strip()
    if driver_commit != head:
        raise ValueError("packet driver is not the committed source state")

    for relative_path in source_manifest(repository_root):
        committed_hash = subprocess.run(
            ["git", "rev-parse", f"{driver_commit}:{relative_path}"],
            cwd=repository_root,
            check=False,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        current_hash = subprocess.run(
            ["git", "hash-object", relative_path],
            cwd=repository_root,
            check=True,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        ).stdout.strip()
        if committed_hash.returncode != 0 or committed_hash.stdout.strip() != current_hash:
            raise ValueError("packet driver is not the committed source state")


def probe_aws() -> dict[str, Any]:
    try:
        import boto3
        import botocore
    except ImportError:
        return {
            "credential_resolved": False,
            "configured_region": None,
            "sdk": "unavailable",
            "service_regions": [],
            "sts_attempted": False,
            "gamelift_inventory_attempted": False,
            "blocker": "boto3 or botocore is unavailable",
        }

    session = boto3.Session()
    credentials = session.get_credentials()
    result: dict[str, Any] = {
        "credential_resolved": credentials is not None,
        "configured_region": session.region_name,
        "sdk": f"boto3-{boto3.__version__}/botocore-{botocore.__version__}",
        "service_regions": session.get_available_regions("gamelift"),
        "sts_attempted": False,
        "gamelift_inventory_attempted": False,
    }
    if credentials is None:
        result["blocker"] = "standard SDK provider chain resolved no credential"
        return result

    result["sts_attempted"] = True
    try:
        identity = session.client("sts", region_name=HOME_REGION).get_caller_identity()
        arn = identity.get("Arn", "")
        result["sts_authenticated"] = True
        result["principal_kind"] = arn.rsplit(":", 1)[-1].split("/", 1)[0]
        result["partition"] = arn.split(":", 2)[1] if arn.startswith("arn:") else "unknown"
    except Exception as error:  # provider exceptions vary by environment
        result["sts_authenticated"] = False
        result["sts_error_class"] = type(error).__name__
        result["blocker"] = "credential exists but STS authentication failed"
        return result

    result["gamelift_inventory_attempted"] = True
    try:
        response = session.client("gamelift", region_name=HOME_REGION).describe_game_session_queues(
            Limit=1
        )
        result["gamelift_inventory_authenticated"] = True
        result["visible_queue_count_in_page"] = len(response.get("GameSessionQueues", []))
        result["inventory_has_more"] = bool(response.get("NextToken"))
    except Exception as error:  # retain only the class, never provider payloads
        result["gamelift_inventory_authenticated"] = False
        result["gamelift_error_class"] = type(error).__name__
        result["blocker"] = "authenticated GameLift inventory request failed"
    return result


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("repository_root", nargs="?", default=".")
    parser.add_argument("--output")
    arguments = parser.parse_args()
    root = Path(arguments.repository_root).resolve()
    started = utc_now()
    start_monotonic = time.monotonic()
    cargo = cargo_path()
    local_command = [
        cargo,
        "test",
        "--manifest-path",
        "server/Cargo.toml",
        "-p",
        "al_provider_adapter_gamelift_spike",
        "--test",
        "adapter_contract",
    ]
    core_command = [
        cargo,
        "test",
        "--manifest-path",
        "server/Cargo.toml",
        "-p",
        "al_server_core",
        "-p",
        "al_provider_adapter_stub",
        "--all-targets",
    ]
    local_log, _ = run_command(local_command, root)
    core_log, _ = run_command(core_command, root)
    preflight = probe_aws()
    driver_commit, _ = run_command(
        ["git", "rev-parse", "HEAD"],
        root,
    )
    driver_commit = driver_commit.strip()
    require_committed_source_state(root, driver_commit)
    ended = utc_now()
    run_id = "gamelift-" + ended.replace("-", "").replace(":", "").replace("T", "-").replace("Z", "z")
    packet = (
        Path(arguments.output).resolve()
        if arguments.output
        else root / "evidence" / "amazon_gamelift" / run_id
    )
    record_path = write_packet(
        repository_root=root,
        packet_root=packet,
        run_id=run_id,
        started_utc=started,
        ended_utc=ended,
        driver_commit=driver_commit,
        preflight=preflight,
        local_contract_log=local_log,
        core_test_log=core_log,
        command_log=[
            sanitized_command(local_command, root),
            sanitized_command(core_command, root),
        ],
        automated_duration_seconds=time.monotonic() - start_monotonic,
    )
    print(record_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
