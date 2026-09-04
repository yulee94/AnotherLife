#!/usr/bin/env python
"""Generate a sanitized, fail-closed PlayFab MPS spike evidence packet.

The common workload may proceed only with an explicitly authorized synthetic title,
MPS build, two approved regions, and secret-key reference. This host has none of
those prerequisites, so the exercised path records every scenario as blocked,
verifies the public control-plane signal, proves neutral rollback, and never sends
credentials or creates provider resources.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
import subprocess
import time
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable


CANDIDATE_ID = "microsoft_playfab"
TASK_ID = "t_27759e01"
PLAN_ID = "MMO-BAKEOFF-v1.0.0"
FIXTURE_SEED = "anotherlife-mmo-bakeoff-v1"
REALMS = ["realm-1", "realm-2", "realm-3", "realm-4"]
REQUIRED_ENV = (
    "PLAYFAB_TITLE_ID",
    "PLAYFAB_SECRET_KEY",
    "PLAYFAB_BUILD_ID",
    "PLAYFAB_HOME_REGION",
    "PLAYFAB_FORBIDDEN_REGION",
    "PLAYFAB_SPIKE_LIVE_AUTHORIZED",
)
PUBLIC_STATUS_URL = "https://status.playfab.com/api/v2/status.json"
VENDOR_SOURCES = [
    {
        "url": "https://learn.microsoft.com/en-us/rest/api/playfab/multiplayer/multiplayer-server?view=playfab-rest",
        "scope": "MPS REST operations and title-entity-token requirement",
    },
    {
        "url": "https://learn.microsoft.com/en-us/gaming/playfab/multiplayer/servers/billing-for-thunderhead",
        "scope": "free evaluation capacity and consumption-billing boundary",
    },
    {
        "url": "https://learn.microsoft.com/en-us/gaming/playfab/multiplayer/servers/identifying-and-increasing-core-limits",
        "scope": "per-title, VM-family, and region core quotas",
    },
    {
        "url": "https://learn.microsoft.com/en-us/gaming/playfab/multiplayer/servers/multiplayer-game-server-lifecycle",
        "scope": "GSDK lifecycle, allocation, termination, and VM lifetime",
    },
    {
        "url": "https://learn.microsoft.com/en-us/gaming/playfab/gamemanager/secret-key-management",
        "scope": "title-key rotation, expiry, disable, and IP allowlist",
    },
    {
        "url": "https://learn.microsoft.com/en-us/gaming/playfab/live-service-management/service-gateway/throttling/best-practices",
        "scope": "HTTP 429 and Retry-After behavior",
    },
    {
        "url": "https://learn.microsoft.com/en-us/gaming/playfab/multiplayer/servers/archiving-and-retrieving-multiplayer-server-logs",
        "scope": "terminated-server log retrieval and documented retention",
    },
    {
        "url": "https://learn.microsoft.com/en-us/gaming/playfab/data-analytics/privacy-compliance/playfab-gdpr-deleting-and-exporting-player-data",
        "scope": "player-data export/deletion APIs; not evidence for MPS resource deletion",
    },
]


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode(
        "utf-8"
    )


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def sha256_text(value: str) -> str:
    return sha256_bytes(value.encode("utf-8"))


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def hash_tree(root: Path, paths: list[Path]) -> str:
    digest = hashlib.sha256()
    for path in sorted(paths, key=lambda item: item.as_posix()):
        relative = path.relative_to(root).as_posix().encode("utf-8")
        digest.update(len(relative).to_bytes(8, "big"))
        digest.update(relative)
        payload = path.read_bytes()
        digest.update(len(payload).to_bytes(8, "big"))
        digest.update(payload)
    return digest.hexdigest()


def git_executable() -> str:
    candidates = (
        "git",
        str(Path(os.environ.get("PROGRAMFILES", "C:/Program Files")) / "Git/cmd/git.exe"),
    )
    for candidate in candidates:
        try:
            result = subprocess.run(
                [candidate, "--version"],
                check=True,
                capture_output=True,
                text=True,
                env=non_playfab_child_environment(),
            )
            if result.returncode == 0:
                return candidate
        except (OSError, subprocess.CalledProcessError):
            continue
    raise RuntimeError("git is unavailable; evidence cannot bind to committed sources")


def git_commit(root: Path) -> str:
    result = subprocess.run(
        [git_executable(), "rev-parse", "HEAD"],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
        env=non_playfab_child_environment(),
    )
    return result.stdout.strip()


def non_playfab_child_environment() -> dict[str, str]:
    """Prevent candidate credentials and authorization from reaching child tools."""
    return {
        name: value
        for name, value in os.environ.items()
        if not name.upper().startswith("PLAYFAB_")
    }


def source_manifest(root: Path, source_paths: list[Path], *, enforce_committed: bool) -> dict[str, Any]:
    """Bind a packet to exact sources and reject dirty production runs."""
    relative_paths = [path.relative_to(root).as_posix() for path in source_paths]
    if enforce_committed:
        git = git_executable()
        status = subprocess.run(
            [git, "status", "--porcelain", "--untracked-files=all", "--", *relative_paths],
            cwd=root,
            check=True,
            capture_output=True,
            text=True,
            env=non_playfab_child_environment(),
        ).stdout.strip()
        if status:
            raise RuntimeError(
                "evidence source files are uncommitted; commit the exact adapter and driver before running"
            )
        for relative_path in relative_paths:
            tracked = subprocess.run(
                [git, "ls-files", "--error-unmatch", "--", relative_path],
                cwd=root,
                capture_output=True,
                text=True,
                env=non_playfab_child_environment(),
            )
            if tracked.returncode != 0:
                raise RuntimeError(f"evidence source is not tracked: {relative_path}")
    return {
        "source_state": "committed" if enforce_committed else "test_override",
        "files": [
            {
                "path": relative_path,
                "sha256": sha256_bytes(path.read_bytes()),
                "bytes": path.stat().st_size,
            }
            for path, relative_path in zip(source_paths, relative_paths, strict=True)
        ],
    }


def cargo_command() -> str:
    candidates = ["cargo"]
    cargo_home = os.environ.get("CARGO_HOME")
    if cargo_home:
        candidates.append(str(Path(cargo_home) / "bin" / "cargo.exe"))
    user_profile = os.environ.get("USERPROFILE")
    if user_profile:
        candidates.append(str(Path(user_profile) / ".cargo" / "bin" / "cargo.exe"))
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        candidates.append(
            str(Path(local_app_data).parent.parent / ".cargo" / "bin" / "cargo.exe")
        )
    child_environment = non_playfab_child_environment()
    for candidate in candidates:
        try:
            subprocess.run(
                [candidate, "--version"],
                check=True,
                capture_output=True,
                text=True,
                env=child_environment,
            )
            return candidate
        except (OSError, subprocess.CalledProcessError):
            continue
    raise RuntimeError("cargo is unavailable; neutral-core rollback cannot be verified")


def run_core_tests(root: Path, log_path: Path) -> bool:
    cargo = cargo_command()
    child_environment = non_playfab_child_environment()
    commands = [
        [
            cargo,
            "test",
            "--manifest-path",
            str(root / "server" / "Cargo.toml"),
            "-p",
            "al_server_core",
            "--lib",
        ],
        [
            cargo,
            "test",
            "--manifest-path",
            str(root / "server" / "Cargo.toml"),
            "-p",
            "al_provider_adapter_playfab_spike",
            "disabled_adapter_rejects_new_work_but_allows_existing_cleanup",
            "--",
            "--exact",
        ],
    ]
    results = [
        subprocess.run(
            command,
            cwd=root,
            capture_output=True,
            text=True,
            check=False,
            env=child_environment,
        )
        for command in commands
    ]
    log_path.parent.mkdir(parents=True, exist_ok=True)
    log_path.write_text(
        "\n\n".join(
            "$ " + " ".join(command[1:]) + "\n" + result.stdout + result.stderr
            for command, result in zip(commands, results, strict=True)
        ),
        encoding="utf-8",
    )
    return all(result.returncode == 0 for result in results)


def fetch_public_status() -> dict[str, Any]:
    request = urllib.request.Request(
        PUBLIC_STATUS_URL,
        headers={"User-Agent": "AnotherLife-PlayFab-Spike/1.0"},
    )
    with urllib.request.urlopen(request, timeout=20) as response:
        value = json.load(response)
    status = value.get("status", {})
    return {
        "source": PUBLIC_STATUS_URL,
        "http_observation": "success",
        "indicator": status.get("indicator", "unknown"),
        "description": status.get("description", "unknown"),
    }


def common_workload_manifest(
    driver_commit: str, server_artifact_fingerprint: str
) -> dict[str, Any]:
    return {
        "driver_commit": driver_commit,
        "server_artifact_fingerprint": server_artifact_fingerprint,
        "adapter_contract_version": "MMO-CONTRACTS-v1.0.0",
        "workload_manifest_hash": "sha256-of-retained-canonical-manifest-artifact",
        "configuration_shape": {
            "neutral_fields": [
                "contract_id",
                "operation_id",
                "correlation_id",
                "actor_id",
                "service_id",
                "authorization_context_id",
                "policy_version",
                "region_id",
                "realm_id",
                "schema_version",
                "artifact_fingerprint",
                "compatibility_fingerprint",
                "attempt",
            ],
            "candidate_adapter_section": [
                "title_reference",
                "build_reference",
                "home_region",
                "forbidden_region",
                "credential_reference",
                "enabled",
            ],
        },
        "synthetic_fixture_seed": FIXTURE_SEED,
        "request_envelopes": [
            "identity",
            "placement",
            "lifecycle",
            "gameplay_canary",
            "economy_canary",
            "social_canary",
            "capacity",
            "security",
            "observation",
        ],
        "operation_ids": "deterministic-SCN-NN-repetition-account-cycle",
        "payload_bytes": {"source": "canonical-synthetic-fixtures", "real_data": False},
        "topology": {
            "realms": 4,
            "logical_regions": ["home_region", "forbidden_region"],
            "accounts_per_realm": 8,
            "placement_cycles_per_account": 2,
        },
        "fault_schedule": ["baseline", "inject", "observe", "recover", "rollback"],
        "observation_schema": PLAN_ID,
        "repetitions": {"excluded_warmups": 1, "measured": 3},
        "warmup_rule": "exclude_exactly_one_identical_warmup_where_permitted",
        "teardown_assertions": [
            "neutral_restore",
            "regional_state_unchanged",
            "credential_revoked_or_absent",
            "resources_deleted_or_named_blocker",
        ],
    }


def _relative(packet: Path, path: Path) -> str:
    return path.relative_to(packet).as_posix()


def _manifest_entry(packet: Path, path: Path, scenario_ids: list[str]) -> dict[str, Any]:
    payload = path.read_bytes()
    suffix = path.suffix.lower()
    content_type = {
        ".json": "application/json",
        ".md": "text/markdown",
        ".txt": "text/plain",
        ".log": "text/plain",
    }.get(suffix, "application/octet-stream")
    return {
        "path": _relative(packet, path),
        "sha256": sha256_bytes(payload),
        "bytes": len(payload),
        "content_type": content_type,
        "classification": "sanitized_common",
        "scenario_ids": scenario_ids,
    }


def run_blocked_preflight(
    root: Path,
    packet: Path,
    *,
    now: Callable[[], str] | None = None,
    public_status: dict[str, Any] | None = None,
    enforce_committed_sources: bool = True,
) -> Path:
    """Run the authorized preflight boundary and write one immutable blocked packet."""
    started_monotonic = time.monotonic()
    timestamp = now or (lambda: datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"))
    started_utc = timestamp()

    driver_commit = git_commit(root)
    evidence_sources = [
        root / "server/Cargo.toml",
        root / "server/Cargo.lock",
        root / "tools/architecture/run_playfab_spike.py",
        root / "tools/architecture/test_run_playfab_spike.py",
        root / "tools/architecture/validate_mmo_bakeoff_plan.py",
        root / "unity/Docs/Architecture/MMO_Provider_Bakeoff_Evidence_Plan_v1.md",
        root / "unity/Docs/Architecture/MMO_Provider_Bakeoff_Scenarios_v1.json",
        root / "unity/Docs/Architecture/Templates/MMO_Provider_Bakeoff_Runbooks_v1.md",
        root / "unity/Docs/Architecture/Templates/MMO_Provider_Spike_Run_Record_v1.json",
    ]
    evidence_sources.extend(
        path
        for source_root in (
            root / "server/al_server_core",
            root / "server/al_provider_adapter_playfab_spike",
        )
        for path in source_root.rglob("*")
        if path.is_file()
    )
    evidence_sources = sorted(set(evidence_sources), key=lambda path: path.as_posix())
    exact_sources = source_manifest(
        root,
        evidence_sources,
        enforce_committed=enforce_committed_sources,
    )
    packet.mkdir(parents=True, exist_ok=False)
    core_files = [
        path
        for path in (root / "server" / "al_server_core").rglob("*")
        if path.is_file()
    ]
    adapter_root = root / "server" / "al_provider_adapter_playfab_spike"
    adapter_files = [path for path in adapter_root.rglob("*") if path.is_file()]
    server_fingerprint = hash_tree(root, core_files)
    adapter_fingerprint = hash_tree(root, adapter_files)

    env_presence = {name: bool(os.environ.get(name)) for name in REQUIRED_ENV}
    credential_values_loaded = bool(os.environ.get("PLAYFAB_SECRET_KEY"))
    missing = [name for name, present in env_presence.items() if not present]
    if os.environ.get("PLAYFAB_SPIKE_LIVE_AUTHORIZED") != "1" and "PLAYFAB_SPIKE_LIVE_AUTHORIZED" not in missing:
        missing.append("PLAYFAB_SPIKE_LIVE_AUTHORIZED=1")
    blocker = (
        "PlayFab synthetic sandbox execution is not authorized/configured; missing "
        + ", ".join(missing)
        if missing
        else "Live PlayFab mutation execution is intentionally unavailable in this preflight-only driver; a reviewed live executor is required before resource creation."
    )

    status = public_status if public_status is not None else fetch_public_status()
    status_record = {
        "retrieved_utc": started_utc,
        "source": PUBLIC_STATUS_URL,
        "observation": status,
        "limitation": "Public aggregate status is supporting evidence only; it does not prove title, region, build, placement, data, or quota behavior.",
    }
    write_json(packet / "logs/public-status.json", status_record)

    retrieval_date = started_utc.split("T", 1)[0]
    write_json(
        packet / "provider-exports/vendor-sources.json",
        {
            "retrieved_utc": started_utc,
            "sources": [dict(item, retrieval_date=retrieval_date) for item in VENDOR_SOURCES],
            "claim_boundary": "vendor_documented_limit only; none is a measured title limit",
        },
    )
    write_json(packet / "provider-exports/source-manifest.json", exact_sources)
    write_json(
        packet / "raw/preflight.json",
        {
            "candidate": CANDIDATE_ID,
            "synthetic_only": True,
            "production_resources_or_data": False,
            "credential_values_loaded_from_environment": credential_values_loaded,
            "credential_values_emitted": False,
            "credential_values_sent_to_provider": False,
            "required_environment_presence": env_presence,
            "provider_mutation_requests_attempted": 0,
            "public_status_request_attempted": 1,
            "blocker": blocker,
        },
    )

    inventory_unknown = "unknown_measurement_required: no authorized title/build API access"
    write_json(
        packet / "residency-inventory.json",
        {
            "configured_resource_region": inventory_unknown,
            "control_plane_location": inventory_unknown,
            "data_plane_locations": inventory_unknown,
            "logs_backups_support_exports": inventory_unknown,
            "authoritative_provider_copies": "none created or observed",
        },
    )
    write_json(
        packet / "quota-inventory.json",
        {
            f"UL-{index:02d}": {
                "status": "unknown_measurement_required",
                "reason": "no authorized PlayFab title/build API access",
            }
            for index in range(1, 10)
        },
    )
    write_json(
        packet / "credential-inventory.json",
        {
            "credential_material_in_packet": False,
            "credential_reference": (
                "present_in_preflight_process_not_serialized_or_used"
                if credential_values_loaded
                else "absent"
            ),
            "scope": "unknown_measurement_required",
            "rotation_test": "blocked_no_authorized_synthetic_title",
        },
    )
    write_json(
        packet / "teardown-inventory.json",
        {
            "provider_resources_created": 0,
            "provider_resources_deleted": 0,
            "provider_residuals_observed": 0,
            "credential_revocation": "not_applicable_no_candidate_credential_used",
            "unknown_account_owned_residuals": inventory_unknown,
        },
    )

    regional_state = {
        "fixture_seed": FIXTURE_SEED,
        "realm_ids": REALMS,
        "synthetic_accounts_per_realm": 8,
        "provider_state_authoritative": False,
    }
    neutral_files = [root / "server/Cargo.toml", root / "server/Cargo.lock", *core_files]
    neutral_hash_before = hash_tree(root, neutral_files)
    adapter_hash_before = hash_tree(root, adapter_files)
    state_hash = sha256_bytes(canonical_bytes(regional_state))
    core_test_log = packet / "logs/neutral-core-tests.log"
    core_tests_passed = run_core_tests(root, core_test_log)
    if not core_tests_passed:
        raise RuntimeError("provider-neutral core tests failed during rollback proof")
    neutral_hash_after = hash_tree(root, neutral_files)
    adapter_hash_after = hash_tree(root, adapter_files)
    if neutral_hash_before != neutral_hash_after or adapter_hash_before != adapter_hash_after:
        raise RuntimeError("rollback verification mutated neutral or candidate source state")
    write_json(
        packet / "rollback.json",
        {
            "candidate_work_stopped": True,
            "candidate_adapter_enabled_before": True,
            "candidate_adapter_enabled_after": False,
            "neutral_configuration_hash_before": neutral_hash_before,
            "neutral_configuration_hash_after": neutral_hash_after,
            "candidate_adapter_hash_before": adapter_hash_before,
            "candidate_adapter_hash_after": adapter_hash_after,
            "regional_state_hash_before": state_hash,
            "regional_state_hash_after": state_hash,
            "core_tests": "pass",
            "provider_resources_or_credentials_used": False,
            "credential_value_loaded_for_preflight_scan": credential_values_loaded,
            "adapter_rollback_test": "disabled_adapter_rejects_new_work_but_allows_existing_cleanup",
            "limitation": "The synthetic adapter fixture was enabled then disabled with cleanup permitted; source hashes were captured before and after execution; no PlayFab title resource was touched and no credential was used or sent. A configured secret value, when present, was loaded only for the exact in-process packet byte scan.",
        },
    )

    workload = common_workload_manifest(driver_commit, server_fingerprint)
    write_json(packet / "workload-manifest.json", workload)
    (packet / "environment.txt").write_text(
        "host_os=" + platform.system() + "\n"
        "python=" + platform.python_version() + "\n"
        "scope=synthetic_nonproduction\n"
        f"credential_value_loaded_from_environment={str(credential_values_loaded).lower()}\n"
        "credentials_emitted_or_sent=false\n",
        encoding="utf-8",
    )
    (packet / "commands.txt").write_text(
        "python tools/architecture/run_playfab_spike.py --packet evidence/microsoft_playfab/<run-id>\n"
        "python tools/architecture/validate_mmo_bakeoff_plan.py . --record evidence/microsoft_playfab/<run-id>/run-record.json\n"
        "cargo test --manifest-path server/Cargo.toml -p al_server_core --lib\n"
        "cargo test --manifest-path server/Cargo.toml -p al_provider_adapter_playfab_spike\n"
        "Required secure environment references: " + ", ".join(REQUIRED_ENV) + "\n",
        encoding="utf-8",
    )
    limitations = (
        "# PlayFab spike limitations\n\n"
        f"Run status: blocked. {blocker}\n\n"
        "No title-scoped PlayFab API, build, placement, lifecycle, quota ladder, fault injection, data export, credential rotation, or resource deletion was executed. "
        "The public status endpoint and Microsoft documentation are supporting evidence only. No sandbox result is extrapolated to production capacity, latency, price, residency, availability, or scale. "
        "The 10,000 steady and 20,000 surge CCU values remain unproven targets. No provider recommendation is made; comparison with GameLift remains downstream.\n"
    )
    (packet / "limitations.md").write_text(limitations, encoding="utf-8")
    template = (
        root
        / "unity/Docs/Architecture/Templates/MMO_Provider_Bakeoff_Runbooks_v1.md"
    ).read_text(encoding="utf-8")
    (packet / "runbooks.md").write_text(
        "# PlayFab blocked-run annotation\n\n"
        f"Candidate: microsoft_playfab\nStatus: blocked\nBlocker: {blocker}\n"
        "Provider-specific commands remain unexecuted and therefore blocked. The neutral rollback command was executed and is captured in logs/neutral-core-tests.log.\n\n"
        + template,
        encoding="utf-8",
    )

    secret_values = [
        value
        for name, value in os.environ.items()
        if name in REQUIRED_ENV and "SECRET" in name and value
    ]
    scanned_files = [path for path in packet.rglob("*") if path.is_file()]
    leaked = [
        path.relative_to(packet).as_posix()
        for path in scanned_files
        if any(secret.encode("utf-8") in path.read_bytes() for secret in secret_values)
    ]
    if leaked:
        raise RuntimeError(f"secret material appeared in evidence files: {leaked}")
    secret_scan_path = packet / "secret-scan.txt"
    secret_scan_path.write_text(
        "PASS: no configured secret value appeared in the packet; any configured value was loaded only for this exact in-process byte scan and was never sent or serialized.\n",
        encoding="utf-8",
    )

    scenario_catalog = json.loads(
        (
            root
            / "unity/Docs/Architecture/MMO_Provider_Bakeoff_Scenarios_v1.json"
        ).read_text(encoding="utf-8")
    )
    scenario_ids = [item["id"] for item in scenario_catalog["scenarios"]]
    scenario_results = []
    for scenario in scenario_catalog["scenarios"]:
        scenario_id = scenario["id"]
        scenario_blocker = (
            f"{scenario_id} ({scenario['name']}) cannot execute its provider runbook: {blocker}"
        )
        scenario_results.append(
            {
                "scenario_id": scenario_id,
                "status": "blocked",
                "started_utc": started_utc,
                "ended_utc": started_utc,
                "operation_ids": [f"preflight-{driver_commit[:12]}-{scenario_id.lower()}"],
                "correlation_ids": [
                    "blocked-correlation-"
                    + sha256_bytes(canonical_bytes({"scenario": scenario_id, "blocker": blocker}))[:16]
                ],
                "stable_result_counts": {"attempted": 0, "blocked": 1},
                "operation_scope": "local_preflight_harness_only; no provider mutation attempted",
                "measurement_handles": ["raw/preflight.json", "logs/public-status.json"],
                "raw_evidence_handles": [
                    "raw/preflight.json",
                    "provider-exports/vendor-sources.json",
                    "limitations.md",
                ],
                "limitations": [
                    "No title-scoped sandbox observation; public/vendor evidence is not a scenario pass."
                ],
                "blockers": [scenario_blocker],
                "contract_violations": [],
                "rollback_status": "blocked",
                "rollback_evidence_handles": ["rollback.json", "logs/neutral-core-tests.log"],
            }
        )

    all_scenarios = scenario_ids
    manifest_paths = [path for path in packet.rglob("*") if path.is_file()]
    manifest = [
        _manifest_entry(packet, path, all_scenarios)
        for path in sorted(manifest_paths, key=lambda item: item.as_posix())
    ]
    ended_utc = timestamp()
    duration = max(0.0, time.monotonic() - started_monotonic)
    record = {
        "record_schema_version": "1.0.0",
        "plan_id": PLAN_ID,
        "candidate_id": CANDIDATE_ID,
        "spike_task_id": TASK_ID,
        "run_id": f"playfab-blocked-{driver_commit[:12]}",
        "run_status": "blocked",
        "claim_class": "observed_sandbox_fact",
        "claim_class_scope": "Schema-required blocked-run classification: the observed fact is the fail-closed absence of authorized sandbox access plus local synthetic adapter execution; no PlayFab scenario result, limit, latency, residency, availability, or scale claim was observed.",
        "started_utc": started_utc,
        "ended_utc": ended_utc,
        "driver_commit": driver_commit,
        "server_artifact_fingerprint": server_fingerprint,
        "adapter_fingerprint": adapter_fingerprint,
        "configuration_hash": sha256_bytes(canonical_bytes(env_presence)),
        "workload_manifest_hash": sha256_bytes((packet / "workload-manifest.json").read_bytes()),
        "region_id": "unconfigured-playfab-home-region",
        "realm_ids": REALMS,
        "synthetic_fixture_seed": FIXTURE_SEED,
        "scenario_results": scenario_results,
        "data_residency_inventory": ["residency-inventory.json"],
        "quota_inventory": ["quota-inventory.json"],
        "credential_inventory": ["credential-inventory.json"],
        "raw_evidence_manifest": manifest,
        "limitations": [
            "Only public status, dated vendor documentation, local adapter tests, and neutral rollback were observable."
        ],
        "blockers": [blocker],
        "contract_violations": [],
        "rollback_result": {
            "status": "blocked",
            "neutral_configuration_hash_before": neutral_hash_before,
            "neutral_configuration_hash_after": neutral_hash_after,
            "regional_state_hash_before": state_hash,
            "regional_state_hash_after": state_hash,
            "core_tests": "pass",
            "evidence_handles": ["rollback.json", "logs/neutral-core-tests.log"],
        },
        "teardown_inventory": ["teardown-inventory.json"],
        "operator_effort": {
            "automated_duration_seconds": duration,
            "manual_duration_seconds": 0,
            "manual_steps": [],
            "support_interactions": [],
        },
        "secret_scan_result": {
            "status": "pass",
            "command": "in-process exact configured-secret byte scan across packet before manifest finalization",
            "evidence_handle": "secret-scan.txt",
            "findings": [],
        },
        "selection_recommendation": None,
        "notes": [
            "All 16 common scenarios have a named access/configuration blocker; none is a pass.",
            "No provider choice is proposed without paired GameLift evidence.",
        ],
    }
    record_path = packet / "run-record.json"
    write_json(record_path, record)
    return record_path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--packet", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = Path(__file__).resolve().parents[2]
    try:
        record = run_blocked_preflight(root, args.packet.resolve())
    except (OSError, RuntimeError, subprocess.CalledProcessError) as error:
        print(f"FAIL: {error}")
        return 1
    print(record)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
