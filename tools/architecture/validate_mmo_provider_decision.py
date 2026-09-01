#!/usr/bin/env python
"""Validate the reversible MMO provider decision and no-selection package."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import zipfile
from pathlib import Path
from typing import Any


CANDIDATES = {"amazon_gamelift", "microsoft_playfab"}
CANDIDATE_EVIDENCE_DIRECTORIES = {
    "amazon_gamelift": "gamelift-current",
    "microsoft_playfab": "playfab-current",
}
CANDIDATE_TEST_LOGS = {
    "amazon_gamelift": "logs/core-tests.log",
    "microsoft_playfab": "logs/neutral-core-tests.log",
}
CRITERION_DEFINITIONS = {
    "CR-01": ("correctness_and_state_ownership_fit", "Correctness and state-ownership fit"),
    "CR-02": ("regional_data_and_backup_isolation", "Regional data and backup isolation"),
    "CR-03": (
        "idempotency_ambiguity_reconciliation_and_retry",
        "Idempotency, ambiguity, reconciliation, retry",
    ),
    "CR-04": (
        "failure_containment_recovery_and_rollback",
        "Failure containment, recovery, rollback",
    ),
    "CR-05": (
        "quota_capacity_signal_and_admission_behavior",
        "Quota, capacity signal, admission",
    ),
    "CR-06": (
        "security_credentials_audit_and_privacy",
        "Security, credentials, audit, privacy",
    ),
    "CR-07": (
        "observability_raw_evidence_and_exportability",
        "Observability and raw-evidence export",
    ),
    "CR-08": (
        "compatibility_migration_adapter_removal_and_exit",
        "Compatibility, migration, removal, exit",
    ),
    "CR-09": ("owner_plus_ai_operational_burden", "Owner-plus-AI operational burden"),
    "CR-10": ("approved_gate_cost_evidence", "Approved-gate cost evidence"),
}
CRITERIA = set(CRITERION_DEFINITIONS)
SCENARIOS = {f"SCN-{index:02d}" for index in range(1, 17)}
UNKNOWN_LIMITS = {f"UL-{index:02d}" for index in range(1, 10)}
RUNBOOK_SUBSECTIONS = {
    "RB-OUTAGE-01": {
        "Trigger",
        "Current-state containment",
        "Diagnosis and commands",
        "Recovery decision",
        "Rollback and evidence",
    },
    "RB-QUOTA-01": {
        "Trigger",
        "Current-state containment",
        "Diagnosis and commands",
        "Recovery decision",
        "Rollback and evidence",
    },
    "RB-CREDENTIAL-01": {
        "Trigger",
        "Current-state containment",
        "Diagnosis and commands",
        "Recovery decision",
        "Rollback and evidence",
    },
    "RB-REVERT-01": {
        "Trigger",
        "Current-state containment",
        "Revert commands",
        "Verification",
        "Failure disposition and evidence",
    },
}
RUNBOOKS = set(RUNBOOK_SUBSECTIONS)
OWNER_BOUNDARIES = {
    "provider_or_no_selection",
    "managed_versus_custom_per_capability",
    "spend_commitment_and_cost_ceiling",
    "quota_and_capacity_posture",
    "region_residency_and_production_exposure",
    "service_and_recovery_objectives",
    "acceptable_residual_and_lock_in_risk",
}
CLAIM_CLASSES = {
    "requirement",
    "measured_result",
    "vendor_documented_fact",
    "assumption",
    "blocker",
    "owner_judgment",
}
CLAIM_LABELS = {
    "Requirement",
    "Measured result",
    "Vendor-documented fact",
    "Assumption",
    "Blocker",
    "Owner judgment",
}
REQUIRED_LOG_SECTIONS = {
    "Decision",
    "Claim discipline",
    "Raw evidence traceability",
    "Equal-criteria comparison",
    "Contract violations and compliance boundary",
    "Operational, residency, quota, and lock-in risks",
    "Managed versus custom implications",
    "Follow-up evidence required",
    "Reversibility and runbooks",
    "Epic exit assessment",
}


class ValidationFailure(AssertionError):
    """Fail-closed package validation error."""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValidationFailure(message)


def require_sha256(value: Any, label: str) -> None:
    require(
        isinstance(value, str)
        and re.fullmatch(r"[0-9a-f]{64}", value) is not None,
        f"{label} must be a lowercase SHA-256 digest",
    )


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verify_file_hash(path: Path, expected: Any, label: str) -> None:
    require(path.is_file(), f"{label} is missing: {path}")
    require_sha256(expected, label)
    actual = sha256_file(path)
    require(actual == expected, f"{label} mismatch: expected {expected}, got {actual}")


def index_unique(items: Any, label: str) -> dict[str, dict[str, Any]]:
    require(isinstance(items, list), f"{label} inventory is not a list")
    indexed: dict[str, dict[str, Any]] = {}
    for item in items:
        require(isinstance(item, dict), f"{label} entry is not an object")
        identity = item.get("id")
        require(isinstance(identity, str) and identity, f"{label} entry has no id")
        require(identity not in indexed, f"duplicate {label}: {identity}")
        indexed[identity] = item
    return indexed


def markdown_sections(markdown: str, level: int) -> dict[str, str]:
    marker = "#" * level
    heading_pattern = re.compile(rf"^{re.escape(marker)}\s+(.+?)\s*$", re.MULTILINE)
    matches = list(heading_pattern.finditer(markdown))
    sections: dict[str, str] = {}
    for index, match in enumerate(matches):
        title = match.group(1).strip()
        require(title not in sections, f"duplicate Markdown section: {title}")
        end = matches[index + 1].start() if index + 1 < len(matches) else len(markdown)
        sections[title] = markdown[match.end() : end].strip()
    return sections


def word_count(text: str) -> int:
    return len(re.findall(r"[A-Za-z0-9][A-Za-z0-9_./-]*", text))


def exact_runbook_sections(contingency: str) -> dict[str, str]:
    sections = markdown_sections(contingency, 2)
    runbooks: dict[str, str] = {}
    for title, body in sections.items():
        match = re.match(r"^(RB-[A-Z]+-\d{2})\b", title)
        if match:
            runbook_id = match.group(1)
            require(runbook_id not in runbooks, f"duplicate runbook: {runbook_id}")
            runbooks[runbook_id] = body
    return runbooks


def validate_decision_log(record: dict[str, Any], log: str) -> None:
    require(
        "<!--" not in log and "-->" not in log,
        "decision log contains an HTML comment that can hide validated content",
    )
    require(
        re.search(r"(?m)^\s*(?:```|~~~)", log) is None,
        "decision log contains fenced content that can hide validated assertions",
    )
    recommendation_lines = re.findall(
        r"^Recommendation:\s*(.+?)\s*$", log, flags=re.MULTILINE
    )
    require(
        recommendation_lines == [f"`{record['recommendation']}`"] == ["`no_selection`"],
        "decision log recommendation contradicts the no-selection record",
    )
    statuses = re.findall(r"^Status:\s*(.+?)\s*$", log, flags=re.MULTILINE)
    require(
        statuses == ["recommendation recorded; owner decision not granted"],
        "decision log status contradicts the owner gate",
    )

    contradiction_patterns = (
        r"(?im)^(?:decision|selected provider|production provider)\s*:\s*.+$",
        r"(?i)\b(?:Amazon GameLift|Microsoft PlayFab)\s+(?:is|was|has been)\s+(?:selected|chosen|approved|recommended)\b",
        r"(?i)\bowner approval\s+(?:is|was|has been)\s+granted\b",
    )
    require(
        not any(re.search(pattern, log) for pattern in contradiction_patterns),
        "decision log missing a valid owner gate or contains contradictory selection or approval prose",
    )

    sections = markdown_sections(log, 2)
    require(
        set(sections) == REQUIRED_LOG_SECTIONS,
        "decision log required section set drifted",
    )
    for title, body in sections.items():
        require(word_count(body) >= 12, f"decision log section is effectively empty: {title}")

    claim_rows: dict[str, str] = {}
    for match in re.finditer(r"^- ([^:\n]+):\s*(.+)$", sections["Claim discipline"], re.MULTILINE):
        label, body = match.group(1).strip(), match.group(2).strip()
        require(label not in claim_rows, f"duplicate decision-log claim class: {label}")
        claim_rows[label] = body
    require(
        set(claim_rows) == CLAIM_LABELS,
        "decision log missing or invalid claim-class inventory",
    )
    for label, body in claim_rows.items():
        require(word_count(body) >= 12, f"decision log claim class is empty: {label}")

    decision_body = sections["Decision"]
    for required_pattern in (
        r"Neither Amazon GameLift nor Microsoft PlayFab can be recommended",
        r"\bno_selection\b",
        r"pending explicit owner approval",
        r"There is no production MMO provider",
        r"no production use is authorized",
    ):
        require(
            re.search(required_pattern, decision_body, re.IGNORECASE) is not None,
            f"decision log decision section missing required no-selection assertion: {required_pattern}",
        )

    criteria = index_unique(record.get("criteria"), "criterion")
    expected_rows = {
        display_name: criterion_id
        for criterion_id, (_, display_name) in CRITERION_DEFINITIONS.items()
    }
    table_rows: dict[str, list[str]] = {}
    for line in sections["Equal-criteria comparison"].splitlines():
        if not line.strip().startswith("|"):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if not cells or cells[0] == "Criterion" or all(re.fullmatch(r":?-{3,}:?", cell) for cell in cells):
            continue
        require(len(cells) == 4, "decision log comparison row must have four columns")
        criterion_name = cells[0]
        require(criterion_name not in table_rows, f"duplicate comparison row: {criterion_name}")
        table_rows[criterion_name] = cells
    require(
        set(table_rows) == set(expected_rows),
        "decision log must contain exactly all ten comparison rows",
    )
    for display_name, criterion_id in expected_rows.items():
        _, gamelift_status, playfab_status, evidence_boundary = table_rows[display_name]
        require(
            gamelift_status == playfab_status == "blocked",
            f"comparison row {criterion_id} must be symmetrically blocked",
        )
        require(
            criteria[criterion_id]["amazon_gamelift"] == gamelift_status
            and criteria[criterion_id]["microsoft_playfab"] == playfab_status,
            f"comparison row {criterion_id} contradicts the decision record",
        )
        require(
            word_count(evidence_boundary) >= 6,
            f"comparison row {criterion_id} has an empty evidence boundary",
        )

    for token in (
        "Owner approval is required",
        "paired comparison common-driver input drifted: server_artifact_fingerprint",
        "unknown_blocked",
        "blocked_pending_evidence_and_owner_approval",
    ):
        require(token.lower() in log.lower(), f"decision log missing required boundary: {token}")
    raw = record["raw_evidence"]
    traceability_digests = [raw["artifact_sha256"]]
    for candidate in record["candidates"]:
        traceability_digests.extend(
            (
                candidate["parent_packet_archive_sha256"],
                candidate["current_rerun"]["run_record_sha256"],
            )
        )
    for digest in traceability_digests:
        require(digest in log, f"decision log traceability is missing digest: {digest}")


def validate_contingency(contingency: str) -> None:
    require(
        "<!--" not in contingency and "-->" not in contingency,
        "contingency contains an HTML comment that can hide validated content",
    )
    require(
        re.search(r"(?m)^\s*(?:```|~~~)", contingency) is None,
        "contingency contains fenced content that can hide validated runbooks",
    )
    runbooks = exact_runbook_sections(contingency)
    require(set(runbooks) == RUNBOOKS, "missing runbook or unexpected runbook id")
    for runbook_id, required_subsections in RUNBOOK_SUBSECTIONS.items():
        body = runbooks[runbook_id]
        require(word_count(body) >= 80, f"empty runbook: {runbook_id}")
        subsections = markdown_sections(body, 3)
        require(
            set(subsections) == required_subsections,
            f"{runbook_id} required runbook section set drifted",
        )
        for title, subsection_body in subsections.items():
            require(
                word_count(subsection_body) >= 6,
                f"empty runbook section: {runbook_id} / {title}",
            )

    placeholder_patterns = ("[command]", "[secure command]", "[candidate]", "[run id]")
    require(
        not any(pattern.lower() in contingency.lower() for pattern in placeholder_patterns),
        "contingency contains an unresolved placeholder",
    )
    for command in (
        "validate_mmo_contracts.py",
        "validate_mmo_bakeoff_plan.py",
        "al_server_core",
        "al_provider_adapter_stub",
    ):
        require(command in contingency, f"contingency missing executable neutral command: {command}")


def validate_documents(record: dict[str, Any], log: str, contingency: str) -> None:
    require(record.get("record_version") == "1.0.0", "decision record version drifted")
    require(
        record.get("decision_id") == "MMO-PROVIDER-DECISION-20260901-001",
        "decision identity drifted",
    )
    require(record.get("plan_id") == "MMO-BAKEOFF-v1.0.0", "plan identity drifted")
    require(record.get("contract_id") == "MMO-CONTRACTS-v1.0.0", "contract identity drifted")
    approval = record.get("owner_approval", {})
    require(approval.get("required") is True, "owner approval is not mandatory")
    if record.get("owner_decision") in {
        "select_amazon_gamelift",
        "select_microsoft_playfab",
    }:
        require(
            approval.get("status") == "granted" and approval.get("approval_record"),
            "selection without owner approval is forbidden",
        )
    require(record.get("status") == "awaiting_owner_approval", "decision status drifted")
    require(record.get("recommendation") == "no_selection", "current recommendation must remain no_selection")
    require(record.get("owner_decision") is None, "owner decision must remain unset without approval")
    require(set(record.get("claim_classes", [])) == CLAIM_CLASSES, "claim classes drifted")
    require(
        approval.get("status") == "not_granted" and approval.get("approval_record") is None,
        "canonical package must not fabricate owner approval",
    )

    baseline = record.get("source_baseline", {})
    for field in ("capacity_claim", "price_claim", "latency_claim", "device_tier_claim"):
        require(baseline.get(field) is None, f"unsupported {field} appeared")

    equivalence = record.get("comparison_equivalence", {})
    require(equivalence.get("criteria_equal") is True, "criteria are not equal")
    require(equivalence.get("scenario_catalog_equal") is True, "scenario catalog is not equal")
    require(equivalence.get("scenario_count_per_candidate") == 16, "scenario count drifted")
    require(
        equivalence.get("current_rerun_source_commit_equal") is True,
        "current rerun source commit drifted",
    )
    require(
        equivalence.get("pair_validator_status") == "blocked",
        "pair validator status must preserve the blocked result",
    )
    require(
        equivalence.get("pair_validator_failure")
        == "paired comparison common-driver input drifted: server_artifact_fingerprint",
        "pair validator failure drifted",
    )
    declared_drift = equivalence.get("other_observed_input_drift")
    require(
        isinstance(declared_drift, list)
        and all(isinstance(field, str) and field for field in declared_drift)
        and len(declared_drift) == len(set(declared_drift)),
        "observed input drift declaration is invalid",
    )

    criteria = index_unique(record.get("criteria"), "criterion")
    require(set(criteria) == CRITERIA, "criterion set must contain CR-01 through CR-10")
    for criterion_id, criterion in criteria.items():
        expected_name, _ = CRITERION_DEFINITIONS[criterion_id]
        require(criterion.get("name") == expected_name, f"{criterion_id} criterion name drifted")
        for candidate_id in CANDIDATES:
            require(
                criterion.get(candidate_id) == "blocked",
                f"{criterion_id} criterion must remain blocked for {candidate_id}",
            )

    candidates = index_unique(record.get("candidates"), "candidate")
    require(set(candidates) == CANDIDATES, "candidate set must be exactly GameLift and PlayFab")
    for candidate_id, candidate in candidates.items():
        summary = candidate.get("scenario_summary", {})
        require(
            summary == {"total": 16, "pass": 0, "fail": 0, "blocked": 16},
            f"{candidate_id} scenario summary must retain 16 blocked scenarios",
        )
        measured = candidate.get("measured_results", {})
        require(measured.get("claim_class") == "measured_result", "measured-result claim class drifted")
        require(measured.get("provider_mutation_requests_attempted") == 0, "provider mutation was inferred")
        require(measured.get("provider_resources_created") == 0, "provider resource was inferred")
        require(measured.get("provider_sandbox_scenarios_measured") == 0, "provider scenario was inferred")
        require(measured.get("selection_recommendation") is None, "spike recommended a provider")
        require(
            candidate.get("vendor_documentation", {}).get("claim_class")
            == "vendor_documented_fact",
            f"{candidate_id} vendor-document claim class drifted",
        )
        require(
            candidate.get("contract_assessment") == "unknown_blocked",
            f"{candidate_id} contract assessment cannot claim pass",
        )
        require(candidate.get("contract_violations") == [], "unobserved contract violation was invented")
        limits = index_unique(candidate.get("unknown_limits"), f"{candidate_id} unknown limit")
        require(set(limits) == UNKNOWN_LIMITS, f"{candidate_id} unknown-limit set drifted")
        for limit_id, limit_record in limits.items():
            require(
                limit_record.get("status") == "unknown_measurement_required",
                f"{candidate_id} unknown limit {limit_id} was silently resolved",
            )
        rollback = candidate.get("rollback", {})
        require(
            rollback.get("provider_rollback_status") == "blocked"
            and rollback.get("neutral_hash_restored") is True
            and rollback.get("regional_state_hash_restored") is True
            and rollback.get("core_tests") == "pass"
            and rollback.get("provider_resources_created") == 0
            and rollback.get("authenticated_residual_inventory") is False,
            f"{candidate_id} rollback evidence drifted",
        )
        rerun = candidate.get("current_rerun", {})
        require(
            rerun.get("driver_commit") == equivalence.get("current_rerun_source_commit"),
            f"{candidate_id} rerun commit drifted",
        )
        for field in ("run_record_sha256", "server_artifact_fingerprint", "workload_manifest_hash"):
            require_sha256(rerun.get(field), f"{candidate_id} {field}")
        require_sha256(candidate.get("parent_packet_archive_sha256"), f"{candidate_id} parent packet")

    raw = record.get("raw_evidence", {})
    require(raw.get("task_id") == "t_aed15bbd", "raw evidence task drifted")
    require(
        raw.get("artifact_filename") == "mmo-provider-decision-evidence-956f452a.zip",
        "raw evidence artifact drifted",
    )
    require_sha256(raw.get("artifact_sha256"), "raw evidence artifact")
    require(
        isinstance(raw.get("reproduction_commands"), list)
        and len(raw["reproduction_commands"]) == 4,
        "raw evidence reproduction commands are incomplete",
    )

    boundaries = index_unique(record.get("owner_reserved_boundaries"), "owner-reserved boundary")
    require(set(boundaries) == OWNER_BOUNDARIES, "owner-reserved boundary set drifted")
    for boundary_id, boundary in boundaries.items():
        require(
            boundary.get("status") == "unresolved",
            f"owner-reserved boundary {boundary_id} was decided without approval",
        )

    no_selection = record.get("contingency", {})
    require(no_selection.get("mode") == "provider_neutral_no_selection", "contingency mode drifted")
    require(no_selection.get("production_provider") is None, "contingency selected a provider")
    require(no_selection.get("production_use_authorized") is False, "contingency authorized production")
    require(no_selection.get("rollback_viable") is True, "rollback is no longer viable")

    exit_assessment = record.get("epic_exit_assessment", {})
    require(
        exit_assessment.get("measured_provider_comparison_complete") is False
        and exit_assessment.get("owner_decision_complete") is False
        and exit_assessment.get("exit_status") == "blocked_pending_evidence_and_owner_approval",
        "epic exit assessment overclaims completion",
    )

    validate_decision_log(record, log)
    validate_contingency(contingency)


def safe_evidence_member(root: Path, relative_path: str, label: str) -> Path:
    relative = Path(relative_path)
    require(
        relative_path.replace("\\", "/") == relative_path
        and not relative.is_absolute()
        and ".." not in relative.parts,
        f"{label} has an unsafe evidence path: {relative_path}",
    )
    require(not root.is_symlink(), f"{label} evidence root must not be a symlink")
    unresolved = root
    for part in relative.parts:
        unresolved = unresolved / part
        require(not unresolved.is_symlink(), f"{label} must not be a symlink")
    resolved_root = root.resolve()
    resolved = unresolved.resolve()
    require(resolved.is_relative_to(resolved_root), f"{label} escapes the evidence root")
    return resolved


def parse_comparison_manifest(evidence_root: Path) -> dict[str, str]:
    manifest_path = evidence_root / "comparison" / "manifest.sha256"
    require(manifest_path.is_file(), "comparison manifest is missing")
    entries: dict[str, str] = {}
    for line_number, line in enumerate(manifest_path.read_text(encoding="utf-8").splitlines(), 1):
        require(line.strip(), f"comparison manifest line {line_number} is empty")
        match = re.fullmatch(r"([0-9a-f]{64}) [ *](\.hermes_artifacts/.+)", line)
        require(match is not None, f"comparison manifest line {line_number} is invalid")
        digest, reference = match.groups()
        require(reference not in entries, f"duplicate comparison manifest path: {reference}")
        entries[reference] = digest
    expected_references = {
        ".hermes_artifacts/gamelift-current/run-record.json",
        ".hermes_artifacts/playfab-current/run-record.json",
        ".hermes_artifacts/comparison/pair-validation.txt",
    }
    require(set(entries) == expected_references, "comparison manifest inventory drifted")
    return entries


def validate_manifest_members(candidate_id: str, packet_root: Path, run_record: dict[str, Any]) -> None:
    entries = run_record.get("raw_evidence_manifest")
    require(isinstance(entries, list) and entries, f"{candidate_id} raw evidence manifest is empty")
    indexed: dict[str, dict[str, Any]] = {}
    for entry in entries:
        require(isinstance(entry, dict), f"{candidate_id} manifest entry is not an object")
        relative_path = entry.get("path")
        require(isinstance(relative_path, str) and relative_path, f"{candidate_id} manifest path is empty")
        require(relative_path not in indexed, f"{candidate_id} duplicate manifest path: {relative_path}")
        indexed[relative_path] = entry
        member_path = safe_evidence_member(packet_root, relative_path, f"{candidate_id} manifest member")
        require(member_path.is_file(), f"{candidate_id} manifest member is missing: {relative_path}")
        require(
            member_path.stat().st_size == entry.get("bytes"),
            f"{candidate_id} manifest byte count mismatch: {relative_path}",
        )
        verify_file_hash(
            member_path,
            entry.get("sha256"),
            f"{candidate_id} manifest SHA-256 for {relative_path}",
        )
        scenario_ids = entry.get("scenario_ids")
        require(
            isinstance(scenario_ids, list)
            and len(scenario_ids) == len(set(scenario_ids))
            and set(scenario_ids).issubset(SCENARIOS),
            f"{candidate_id} manifest scenario inventory is invalid: {relative_path}",
        )

    actual_files = {
        path.relative_to(packet_root).as_posix()
        for path in packet_root.rglob("*")
        if path.is_file() and path.name != "run-record.json"
    }
    require(
        set(indexed) == actual_files,
        f"{candidate_id} raw evidence manifest does not cover every retained file",
    )


def validate_scenario_inventory(candidate_id: str, run_record: dict[str, Any]) -> None:
    scenarios = run_record.get("scenario_results")
    require(isinstance(scenarios, list), f"{candidate_id} scenario inventory is not a list")
    indexed: dict[str, dict[str, Any]] = {}
    for scenario in scenarios:
        require(isinstance(scenario, dict), f"{candidate_id} scenario inventory entry is invalid")
        scenario_id = scenario.get("scenario_id")
        require(isinstance(scenario_id, str), f"{candidate_id} scenario inventory entry has no id")
        require(scenario_id not in indexed, f"{candidate_id} duplicate scenario: {scenario_id}")
        indexed[scenario_id] = scenario
    require(set(indexed) == SCENARIOS, f"{candidate_id} scenario inventory must contain SCN-01 through SCN-16")
    for scenario_id, scenario in indexed.items():
        require(
            scenario.get("status") == "blocked"
            and scenario.get("rollback_status") == "blocked"
            and scenario.get("contract_violations") == []
            and isinstance(scenario.get("blockers"), list)
            and bool(scenario["blockers"])
            and scenario.get("stable_result_counts") == {"attempted": 0, "blocked": 1},
            f"{candidate_id} scenario inventory overclaims {scenario_id}",
        )


def validate_rollback_evidence(
    candidate_id: str,
    packet_root: Path,
    run_record: dict[str, Any],
) -> None:
    rollback_path = packet_root / "rollback.json"
    rollback = json.loads(rollback_path.read_text(encoding="utf-8"))
    recorded = run_record.get("rollback_result", {})
    for prefix in ("neutral_configuration", "regional_state"):
        before_field = f"{prefix}_hash_before"
        after_field = f"{prefix}_hash_after"
        require_sha256(rollback.get(before_field), f"{candidate_id} rollback {before_field}")
        require_sha256(rollback.get(after_field), f"{candidate_id} rollback {after_field}")
        require(
            rollback.get(before_field) == rollback.get(after_field),
            f"{candidate_id} rollback hashes do not restore {prefix}",
        )
        require(
            recorded.get(before_field) == rollback.get(before_field)
            and recorded.get(after_field) == rollback.get(after_field),
            f"{candidate_id} rollback hashes contradict the run record",
        )
    require(
        rollback.get("core_tests") == recorded.get("core_tests") == "pass",
        f"{candidate_id} rollback test status drifted",
    )
    require(recorded.get("status") == "blocked", f"{candidate_id} rollback status must remain blocked")


def validate_test_evidence(candidate_id: str, packet_root: Path) -> None:
    test_log_path = packet_root / CANDIDATE_TEST_LOGS[candidate_id]
    require(test_log_path.is_file(), f"{candidate_id} test evidence is missing")
    test_log = test_log_path.read_text(encoding="utf-8")
    passing_results = re.findall(
        r"test result: ok\.\s+(\d+) passed; 0 failed;", test_log, flags=re.IGNORECASE
    )
    require(
        passing_results and any(int(count) > 0 for count in passing_results),
        f"{candidate_id} test evidence does not contain a real passing test result",
    )
    require(
        "test result: FAILED" not in test_log and "error: test failed" not in test_log,
        f"{candidate_id} test evidence contains a failed test run",
    )


def validate_archive_matches_tree(archive_path: Path, evidence_root: Path) -> None:
    expected_files = {
        path.relative_to(evidence_root).as_posix()
        for directory in (*CANDIDATE_EVIDENCE_DIRECTORIES.values(), "comparison")
        for path in (evidence_root / directory).rglob("*")
        if path.is_file()
    }
    with zipfile.ZipFile(archive_path) as archive:
        file_entries = {entry.filename: entry for entry in archive.infolist() if not entry.is_dir()}
        require(
            set(file_entries) == expected_files,
            "retained evidence artifact member inventory differs from the extracted evidence tree",
        )
        for relative_path, entry in file_entries.items():
            require(
                (entry.external_attr >> 16) & 0o170000 != 0o120000,
                f"retained evidence artifact member is a symlink: {relative_path}",
            )
            extracted = safe_evidence_member(
                evidence_root,
                relative_path,
                "retained evidence artifact member",
            )
            require(
                entry.file_size == extracted.stat().st_size,
                f"retained evidence artifact member size mismatch: {relative_path}",
            )
            archive_digest = hashlib.sha256()
            with archive.open(entry) as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    archive_digest.update(chunk)
            require(
                archive_digest.hexdigest() == sha256_file(extracted),
                f"retained evidence artifact member SHA-256 mismatch: {relative_path}",
            )


def validate_evidence(record: dict[str, Any], evidence_root: Path) -> None:
    require(not evidence_root.is_symlink(), "evidence root must not be a symlink")
    require(evidence_root.is_dir(), f"evidence root is missing: {evidence_root}")
    raw = record["raw_evidence"]
    archive_path = safe_evidence_member(
        evidence_root,
        raw["artifact_filename"],
        "raw evidence artifact",
    )
    verify_file_hash(archive_path, raw["artifact_sha256"], "raw evidence artifact SHA-256")

    comparison_manifest = parse_comparison_manifest(evidence_root)
    candidates = index_unique(record.get("candidates"), "candidate")
    workloads: dict[str, dict[str, Any]] = {}
    workload_hashes: dict[str, str] = {}

    for candidate_id in sorted(CANDIDATES):
        candidate = candidates[candidate_id]
        rerun = candidate["current_rerun"]
        directory = CANDIDATE_EVIDENCE_DIRECTORIES[candidate_id]
        packet_root = evidence_root / directory
        require(packet_root.is_dir(), f"{candidate_id} evidence packet is missing")
        run_record_path = packet_root / "run-record.json"
        manifest_reference = f".hermes_artifacts/{directory}/run-record.json"
        require(
            comparison_manifest[manifest_reference] == rerun["run_record_sha256"],
            f"{candidate_id} comparison manifest run record digest contradicts the decision record",
        )
        verify_file_hash(
            run_record_path,
            rerun["run_record_sha256"],
            f"{candidate_id} run record SHA-256",
        )
        run_record = json.loads(run_record_path.read_text(encoding="utf-8"))
        require(run_record.get("candidate_id") == candidate_id, f"{candidate_id} run record identity drifted")
        require(run_record.get("run_id") == rerun.get("run_id"), f"{candidate_id} run id drifted")
        require(run_record.get("driver_commit") == rerun.get("driver_commit"), f"{candidate_id} evidence commit drifted")
        require(run_record.get("run_status") == "blocked", f"{candidate_id} run status must remain blocked")
        require(
            run_record.get("server_artifact_fingerprint") == rerun["server_artifact_fingerprint"],
            f"{candidate_id} server artifact fingerprint drifted",
        )
        require(
            run_record.get("workload_manifest_hash") == rerun["workload_manifest_hash"],
            f"{candidate_id} workload manifest hash drifted",
        )
        require(
            run_record.get("secret_scan_result", {}).get("status") == "pass",
            f"{candidate_id} secret scan evidence did not pass",
        )

        validate_manifest_members(candidate_id, packet_root, run_record)
        validate_scenario_inventory(candidate_id, run_record)
        validate_rollback_evidence(candidate_id, packet_root, run_record)
        validate_test_evidence(candidate_id, packet_root)

        workload_path = packet_root / "workload-manifest.json"
        workload_digest = sha256_file(workload_path)
        require(
            workload_digest == rerun["workload_manifest_hash"],
            f"{candidate_id} workload manifest SHA-256 mismatch",
        )
        workload = json.loads(workload_path.read_text(encoding="utf-8"))
        require(
            workload.get("driver_commit") == rerun["driver_commit"],
            f"{candidate_id} workload commit drifted",
        )
        require(
            workload.get("server_artifact_fingerprint") == rerun["server_artifact_fingerprint"],
            f"{candidate_id} workload server artifact fingerprint drifted",
        )
        workloads[candidate_id] = workload
        workload_hashes[candidate_id] = workload_digest

    pair_path = evidence_root / "comparison" / "pair-validation.txt"
    pair_reference = ".hermes_artifacts/comparison/pair-validation.txt"
    verify_file_hash(
        pair_path,
        comparison_manifest[pair_reference],
        "pair validation evidence SHA-256",
    )
    pair_lines = pair_path.read_text(encoding="utf-8").splitlines()
    require(
        pair_lines
        == [
            f"FAIL: {record['comparison_equivalence']['pair_validator_failure']}",
            "exit_code=1",
        ],
        "pair validation evidence contradicts the blocked comparison",
    )

    gamelift = workloads["amazon_gamelift"]
    playfab = workloads["microsoft_playfab"]
    common_fields = (set(gamelift) | set(playfab)) - {"workload_manifest_hash"}
    actual_drift = {
        field for field in common_fields if gamelift.get(field) != playfab.get(field)
    }
    if workload_hashes["amazon_gamelift"] != workload_hashes["microsoft_playfab"]:
        actual_drift.add("workload_manifest_hash")
    require(
        "server_artifact_fingerprint" in actual_drift,
        "pair validator failure is not supported by candidate records",
    )
    actual_other_drift = actual_drift - {"server_artifact_fingerprint"}
    declared_other_drift = set(record["comparison_equivalence"]["other_observed_input_drift"])
    require(
        declared_other_drift == actual_other_drift,
        "observed input drift does not match candidate records: "
        f"declared={sorted(declared_other_drift)}, actual={sorted(actual_other_drift)}",
    )
    validate_archive_matches_tree(archive_path, evidence_root)


def load_package(root: Path) -> tuple[dict[str, Any], str, str]:
    architecture = root / "unity" / "Docs" / "Architecture"
    record = json.loads(
        (architecture / "MMO_Provider_Decision_Record_v1.json").read_text(encoding="utf-8")
    )
    log = (architecture / "MMO_Provider_Decision_Log_v1.md").read_text(encoding="utf-8")
    contingency = (
        architecture / "MMO_Provider_No_Selection_Contingency_v1.md"
    ).read_text(encoding="utf-8")
    return record, log, contingency


def validate(root: Path, evidence_root: Path | None = None) -> None:
    record, log, contingency = load_package(root)
    validate_documents(record, log, contingency)
    if evidence_root is not None:
        validate_evidence(record, evidence_root)


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", nargs="?", default=".", type=Path)
    parser.add_argument(
        "--evidence-root",
        type=Path,
        help="retained local evidence root; when supplied, every available record fails closed",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    arguments = parse_args(sys.argv[1:] if argv is None else argv)
    root = arguments.root.resolve()
    evidence_root = arguments.evidence_root
    try:
        validate(root, evidence_root)
    except (
        ValidationFailure,
        OSError,
        UnicodeError,
        json.JSONDecodeError,
    ) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1
    evidence_status = " and retained evidence verified" if evidence_root else ""
    print(
        "PASS: equal 10-criterion blocked comparison, 2 candidates, 16 scenarios each, "
        "9 unknown limits each, owner-gated no-selection recommendation, viable neutral "
        f"rollback, and 4 substantive contingency runbooks{evidence_status}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
