#!/usr/bin/env python
"""Validate the common MMO provider bake-off evidence package."""

from __future__ import annotations

import hashlib
import json
import math
import re
import sys
from datetime import datetime
from pathlib import Path, PurePosixPath
from typing import Any


PLAN_ID = "MMO-BAKEOFF-v1.0.0"
EXPECTED_PLAN_DIGEST = "6830b39511605c4a4f8e1ee7581f7ce297540c6373cf05bc2587f1c331bea269"
EXPECTED_RUN_RECORD_DIGEST = "dfcfc2cc4c96ad4c748cd2592770e671f76b5eb9430a27b90cd633946bc43fd7"
EXPECTED_DOCUMENT_DIGEST = "2f6772ec2ca96a74730b1ef8a6b3e3aa7d7668f9991dd7c7e1451fedfa52cd59"
EXPECTED_RUNBOOKS_DIGEST = "b57057af9554560c07df7b9e186b1c04953db73aa3175bd6c268f6e056248775"
CANDIDATE_TASK_BY_ID = {
    "amazon_gamelift": "t_ff702849",
    "microsoft_playfab": "t_27759e01",
}
CANDIDATES = set(CANDIDATE_TASK_BY_ID)
DATA_POLICY = {
    "D-GID-01": ("global_minimized", "identity_domain", {"canonical_account_id", "platform_link_reference", "eligibility_reference", "restriction_reference", "consent_reference", "owning_region_id", "durable_realm_reference"}, "opaque_adapter_mapping_only"),
    "D-ENT-01": ("global_minimized", "identity_platform_reconciliation_boundary", {"opaque_entitlement_evidence_reference", "owning_region_id", "reconciliation_state"}, "opaque_evidence_handle_only"),
    "D-SES-01": ("region_local", "regional_gateway_session_and_placement_controllers", {"non_authoritative_owning_region_hint"}, "ephemeral_adapter_private_allocation_mapping"),
    "D-PLY-01": ("region_local", "regional_persistence_and_single_active_simulation_owner", {"approved_minimized_read_only_public_projection"}, "none_authoritative"),
    "D-ECO-01": ("region_local", "regional_transactional_economy_ledger", {"approved_minimized_read_only_aggregate"}, "none_authoritative"),
    "D-SOC-01": ("region_local", "regional_social_domain", {"approved_minimized_read_only_public_projection"}, "transport_receipt_only"),
    "D-BAK-01": ("region_local", "regional_recovery_domain", {"approved_backup_health_summary_without_payload"}, "only_if_same_approved_region_boundary_and_observed"),
    "D-AUD-01": ("region_local", "producing_regional_domain_and_append_only_audit_boundary", {"approved_sanitized_incident_summary"}, "sanitized_evidence_handle_only"),
    "D-TEL-01": ("region_privacy_scoped", "producing_service_until_bounded_pipeline_acceptance", {"approved_low_cardinality_aggregate"}, "sanitized_bounded_observation_only"),
    "D-PRV-01": ("adapter_private_regional", "disposable_candidate_adapter", set(), "environment_and_region_scoped_adapter_metadata"),
}
DATA_PROHIBITED_POLICY = {
    "D-GID-01": {"character", "progression", "inventory", "currency", "market", "guild_membership", "moderation_case", "communications_content", "raw_assertion", "receipt_payload", "credential_material"},
    "D-ENT-01": {"granted_value", "ledger_entries", "inventory_mutation", "settlement_history", "raw_receipt", "credential_material"},
    "D-SES-01": {"session_secret", "writable_route", "lease_authority", "realm_assignment"},
    "D-PLY-01": {"character", "progression", "inventory", "kingdom", "territory", "objective", "reward", "checkpoint", "committed_outcome"},
    "D-ECO-01": {"currency", "market_order", "trade", "inventory_value", "settlement", "granted_entitlement", "outbox", "deduplication_record"},
    "D-SOC-01": {"guild_membership", "alliance_membership", "role", "moderation_case", "sanction", "regional_communications_state"},
    "D-BAK-01": {"regional_snapshot", "operation_log", "outbox_position", "restore_payload"},
    "D-AUD-01": {"private_payload", "raw_payment_data", "raw_assertion", "credential_material"},
    "D-TEL-01": {"account_id_label", "character_id_label", "item_id_label", "message_id_label", "receipt_label", "assertion_label", "credential_material", "endpoint_label"},
    "D-PRV-01": {"canonical_account_id_as_provider_id", "durable_realm_id_as_allocation", "gameplay_result"},
}
DATA_SCENARIO_POLICY = {
    "D-GID-01": {"SCN-03", "SCN-10", "SCN-15", "SCN-16"},
    "D-ENT-01": {"SCN-03", "SCN-04", "SCN-06", "SCN-10", "SCN-15"},
    "D-SES-01": {"SCN-02", "SCN-03", "SCN-08", "SCN-11", "SCN-12", "SCN-15"},
    "D-PLY-01": {"SCN-08", "SCN-10", "SCN-11", "SCN-12", "SCN-15", "SCN-16"},
    "D-ECO-01": {"SCN-04", "SCN-05", "SCN-06", "SCN-10", "SCN-15"},
    "D-SOC-01": {"SCN-08", "SCN-10", "SCN-14", "SCN-15"},
    "D-BAK-01": {"SCN-08", "SCN-10", "SCN-16"},
    "D-AUD-01": {"SCN-06", "SCN-10", "SCN-13", "SCN-14", "SCN-16"},
    "D-TEL-01": {"SCN-07", "SCN-08", "SCN-14", "SCN-16"},
    "D-PRV-01": {"SCN-01", "SCN-02", "SCN-13", "SCN-15", "SCN-16"},
}
DATA_CLASSES = set(DATA_POLICY)
GLOBAL_MINIMIZED = {"D-GID-01", "D-ENT-01"}
THREAT_POLICY = {
    "TM-01": ("provider_lock_in", {"SCN-02", "SCN-15", "SCN-16"}),
    "TM-02": ("regional_data_leakage", {"SCN-01", "SCN-10", "SCN-14", "SCN-16"}),
    "TM-03": ("hidden_global_state_coupling", {"SCN-03", "SCN-08", "SCN-12"}),
    "TM-04": ("incompatible_authority_model", {"SCN-04", "SCN-05", "SCN-06", "SCN-11"}),
    "TM-05": ("quota_surprise", {"SCN-01", "SCN-07"}),
    "TM-06": ("failed_region_control_plane_dependence", {"SCN-08", "SCN-09"}),
    "TM-07": ("credential_compromise_or_rotation", {"SCN-01", "SCN-13", "SCN-16"}),
    "TM-08": ("prototype_assumption_leaks_into_production", {"SCN-01", "SCN-02", "SCN-15", "SCN-16"}),
}
THREATS = set(THREAT_POLICY)
SCENARIO_POLICY = {
    "SCN-01": ("preflight_inventory", "WF-FUNCTIONAL-v1", {"C-PLC-01", "C-CAP-01", "C-SEC-01", "C-OPS-01"}, "inventory_regions_features_quotas_credentials_resources_data_paths", "all_inventory_fields_have_evidence_or_unknown_measurement_required"),
    "SCN-02": ("clean_lifecycle_and_placement", "WF-FUNCTIONAL-v1", {"C-PLC-01", "C-DEP-01", "C-OPS-01"}, "provision", "provider_chooses_neither_region_nor_realm"),
    "SCN-03": ("identity_session_region_and_realm_preservation", "WF-FUNCTIONAL-v1", {"C-IDN-01", "C-PLC-01", "C-ECO-01"}, "isolate_global_plane", "global_loss_cannot_rewrite_regional_state"),
    "SCN-04": ("duplicate_operation_same_payload", "WF-FUNCTIONAL-v1", {"C-PLC-01", "C-ECO-01", "C-DEP-01"}, "repeat_same_operation_and_payload", "no_duplicate_lifecycle_or_value_mutation"),
    "SCN-05": ("operation_payload_or_scope_drift", "WF-FUNCTIONAL-v1", {"C-PLC-01", "C-ECO-01", "C-SEC-01"}, "reuse_operation_id_with_changed_payload", "all_drift_fails_closed_as_conflict"),
    "SCN-06": ("ambiguous_completion_reconciliation", "WF-FUNCTIONAL-v1", {"C-PLC-01", "C-ECO-01", "C-DEP-01", "C-OPS-01"}, "drop_response_at_commit_boundary", "no_blind_value_retry"),
    "SCN-07": ("quota_throttle_and_recovery", "WF-QUOTA-LADDER-v1", {"C-PLC-01", "C-CAP-01", "C-OPS-01"}, "run_identical_request_ladder", "no_sandbox_to_production_extrapolation"),
    "SCN-08": ("control_plane_outage_with_established_owner", "WF-FUNCTIONAL-v1", {"C-IDN-01", "C-PLC-01", "C-SIM-01", "C-SOC-01", "C-OPS-01"}, "isolate_provider_or_global_control_plane", "no_second_owner"),
    "SCN-09": ("failed_and_partial_lifecycle", "WF-FUNCTIONAL-v1", {"C-PLC-01", "C-DEP-01", "C-OPS-01"}, "inject_launch_failure", "no_placement_to_unready_artifact"),
    "SCN-10": ("regional_isolation_and_residency", "WF-FUNCTIONAL-v1", {"C-IDN-01", "C-PLC-01", "C-PER-01", "C-SOC-01", "C-ECO-01", "C-SEC-01"}, "attempt_cross_region_write", "no_authoritative_cross_region_copy"),
    "SCN-11": ("lease_epoch_and_duplicate_owner", "WF-FUNCTIONAL-v1", {"C-PLC-01", "C-SIM-01"}, "attempt_duplicate_active_route_and_owner", "exactly_one_writer_remains"),
    "SCN-12": ("adapter_restart_and_reconciliation", "WF-FUNCTIONAL-v1", {"C-PLC-01", "C-DEP-01", "C-OPS-01"}, "restart_adapter", "no_duplicate_owner_or_mutation"),
    "SCN-13": ("credential_rotation_and_compromise_containment", "WF-FUNCTIONAL-v1", {"C-SEC-01", "C-DEP-01", "C-OPS-01"}, "negative_test_old_credential", "old_credential_fails"),
    "SCN-14": ("telemetry_failure_and_redaction", "WF-FUNCTIONAL-v1", {"C-CAP-01", "C-SEC-01", "C-OPS-01"}, "drop_delay_duplicate_and_reorder_telemetry", "prohibited_labels_and_payloads_absent"),
    "SCN-15": ("adapter_removal_and_neutral_restore", "WF-FUNCTIONAL-v1", {"C-IDN-01", "C-PLC-01", "C-PER-01", "C-SIM-01", "C-SOC-01", "C-ECO-01", "C-DEP-01", "C-SEC-01", "C-OPS-01"}, "disable_or_delete_adapter", "neutral_path_operates"),
    "SCN-16": ("teardown_export_deletion_and_lock_in_inventory", "WF-FUNCTIONAL-v1", {"C-CAP-01", "C-DEP-01", "C-SEC-01", "C-OPS-01"}, "inventory_residuals_manual_steps_and_exit_constraints", "neutral_core_and_state_do_not_depend_on_residuals"),
}
SCENARIOS = set(SCENARIO_POLICY)
RUNBOOKS = {
    "RB-OUTAGE-01",
    "RB-QUOTA-01",
    "RB-CREDENTIAL-01",
    "RB-REVERT-01",
}
UNKNOWN_LIMIT_POLICY = {
    "UL-01": "feature_specific_regions_and_data_locations",
    "UL-02": "resource_and_concurrent_process_quotas",
    "UL-03": "placement_lifecycle_identity_and_admin_api_rates",
    "UL-04": "burst_retry_reset_and_backpressure_behavior",
    "UL-05": "artifact_deployment_drain_shutdown_startup_update_limits",
    "UL-06": "credential_overlap_propagation_revocation_audit_and_scope",
    "UL-07": "telemetry_retention_delay_loss_cardinality_export_and_deletion",
    "UL-08": "sandbox_expiration_feature_parity_billing_and_fault_injection",
    "UL-09": "data_export_deletion_backup_restore_residual_and_account_closure",
}
UNKNOWN_LIMITS = set(UNKNOWN_LIMIT_POLICY)
ALLOWED_OUTCOMES = {
    "select_amazon_gamelift",
    "select_microsoft_playfab",
    "no_selection",
    "blocked_pending_evidence",
}
CLAIM_CLASSES = {
    "requirement",
    "observed_sandbox_fact",
    "vendor_documented_limit",
    "measured_limit",
    "unknown_measurement_required",
    "unproven_scale_target",
}
COMMON_DRIVER_EQUAL_INPUTS = {
    "driver_commit",
    "server_artifact_fingerprint",
    "adapter_contract_version",
    "workload_manifest_hash",
    "configuration_shape",
    "synthetic_fixture_seed",
    "request_envelopes",
    "operation_ids",
    "payload_bytes",
    "topology",
    "fault_schedule",
    "observation_schema",
    "repetitions",
    "warmup_rule",
    "teardown_assertions",
}
QUOTA_DIMENSIONS = {
    "concurrent_neutral_operations",
    "placement_requests",
    "lifecycle_requests",
    "administrative_requests",
}
QUOTA_COUNTERS = {
    "attempted",
    "accepted",
    "pending",
    "succeeded",
    "duplicate",
    "reconciled",
    "rejected",
    "throttled",
    "unavailable",
    "ambiguous",
    "cancelled",
}
QUOTA_STOP_CONDITIONS = {
    "throttled",
    "unavailable",
    "safety_or_budget_guard",
    "sandbox_limit",
    "missing_authorization",
}
REQUIRED_EVIDENCE_FIELDS = {
    "record_schema_version",
    "plan_id",
    "candidate_id",
    "spike_task_id",
    "run_id",
    "run_status",
    "claim_class",
    "started_utc",
    "ended_utc",
    "driver_commit",
    "server_artifact_fingerprint",
    "adapter_fingerprint",
    "configuration_hash",
    "workload_manifest_hash",
    "region_id",
    "realm_ids",
    "synthetic_fixture_seed",
    "scenario_results",
    "data_residency_inventory",
    "quota_inventory",
    "credential_inventory",
    "raw_evidence_manifest",
    "limitations",
    "blockers",
    "contract_violations",
    "rollback_result",
    "teardown_inventory",
    "operator_effort",
    "secret_scan_result",
}
REQUIRED_SCENARIO_RESULT_FIELDS = {
    "scenario_id",
    "status",
    "started_utc",
    "ended_utc",
    "operation_ids",
    "correlation_ids",
    "stable_result_counts",
    "measurement_handles",
    "raw_evidence_handles",
    "limitations",
    "blockers",
    "contract_violations",
    "rollback_status",
    "rollback_evidence_handles",
}
REQUIRED_EVIDENCE_MANIFEST_ENTRY_FIELDS = {
    "path",
    "sha256",
    "bytes",
    "content_type",
    "classification",
    "scenario_ids",
}
SCENARIO_LIST_FIELDS = (
    "contracts",
    "candidates",
    "preconditions",
    "actions",
    "required_observations",
    "pass_conditions",
    "rollback",
)
RUNBOOK_SECTIONS = (
    "trigger",
    "safety_and_authority_checks",
    "containment",
    "diagnosis",
    "actions",
    "recovery",
    "rollback",
    "verification",
    "evidence",
)
DOC_SECTIONS = (
    "## 1. Scope and claim discipline",
    "## 2. Data classification and residency map",
    "### 2.1 Prohibited data paths",
    "### 2.2 Residency evidence procedure",
    "## 3. Threat and failure model",
    "## 4. Equivalent workload protocol",
    "## 5. Instrumentation and evidence capture",
    "## 6. Pass/fail and comparison interpretation",
    "## 7. Rollback contract",
    "## 8. Runbook use",
    "## 9. Unknown vendor limits to measure",
    "## 10. Acceptance traceability",
)


class ValidationFailure(AssertionError):
    """A fail-closed package validation error."""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValidationFailure(message)


def read_text(path: Path) -> str:
    require(path.is_file(), f"missing required file: {path}")
    return path.read_text(encoding="utf-8")


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(read_text(path))


def canonical_json_digest(value: dict[str, Any]) -> str:
    payload = json.dumps(
        value, sort_keys=True, separators=(",", ":"), ensure_ascii=False
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def text_digest(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def index_unique(
    items: list[dict[str, Any]], label: str, identity_field: str = "id"
) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for item in items:
        identity = item.get(identity_field)
        require(isinstance(identity, str) and identity, f"{label} has missing id")
        require(identity not in result, f"duplicate {label} id: {identity}")
        result[identity] = item
    return result


def validate_documents(
    plan: dict[str, Any],
    run_record: dict[str, Any],
    document: str,
    runbooks_document: str,
) -> None:
    require(plan.get("plan_id") == PLAN_ID, "plan identity drifted")
    require(plan.get("record_version") == "1.0.0", "plan version drifted")
    require(
        plan.get("status") == "common_definition_not_run",
        "plan must remain an unexecuted common definition",
    )

    candidates = index_unique(plan.get("candidates", []), "candidate")
    require(set(candidates) == CANDIDATES, "candidate set must be exactly GameLift and PlayFab")
    for candidate_id, candidate in candidates.items():
        require(
            candidate.get("spike_task_id") == CANDIDATE_TASK_BY_ID[candidate_id],
            f"candidate spike task mapping drifted for {candidate_id}",
        )
        forbidden = {"result", "score", "rank", "recommendation", "selected"} & set(candidate)
        require(not forbidden, f"candidate contains pre-comparison fields: {sorted(forbidden)}")

    decision = plan.get("decision", {})
    require(decision.get("winner_preselected") is False, "winner must not be preselected")
    require(decision.get("decision_owner") == "game_owner", "selection authority left game owner")
    require(
        decision.get("spike_workers_may_recommend_winner") is False,
        "spike workers must not recommend a winner",
    )
    require(
        set(decision.get("allowed_outcomes", [])) == ALLOWED_OUTCOMES,
        "allowed outcomes must preserve both selections, no-selection, and blocked",
    )
    require(set(plan.get("claim_classes", [])) == CLAIM_CLASSES, "claim classes drifted")

    data_classes = index_unique(plan.get("data_classes", []), "data class")
    require(set(data_classes) == DATA_CLASSES, "data-class inventory is incomplete or has extras")
    for class_id, item in data_classes.items():
        for field in (
            "name",
            "authority_scope",
            "sole_authority",
            "globally_replicable_fields",
            "prohibited_global_fields",
            "provider_copy",
            "required_scenarios",
        ):
            require(field in item, f"{class_id} missing {field}")
        require(
            set(item["required_scenarios"]) == DATA_SCENARIO_POLICY[class_id],
            f"{class_id} scenario coverage drifted",
        )
        require(
            set(item["prohibited_global_fields"]) == DATA_PROHIBITED_POLICY[class_id],
            f"{class_id} prohibited-global policy drifted",
        )
        expected_scope, expected_authority, expected_global_fields, expected_provider_copy = (
            DATA_POLICY[class_id]
        )
        require(
            item["authority_scope"] == expected_scope,
            f"{class_id} authority scope drifted from {expected_scope}",
        )
        require(
            item["sole_authority"] == expected_authority,
            f"{class_id} sole authority drifted",
        )
        require(
            set(item["globally_replicable_fields"]) == expected_global_fields,
            f"{class_id} global replication allowlist drifted",
        )
        require(
            item["provider_copy"] == expected_provider_copy,
            f"{class_id} provider-copy policy drifted",
        )
    require(
        GLOBAL_MINIMIZED
        == {
            class_id
            for class_id, item in data_classes.items()
            if item["authority_scope"] == "global_minimized"
        },
        "a non-identity/entitlement class became globally authoritative",
    )
    require(
        "granted_value" in data_classes["D-ENT-01"]["prohibited_global_fields"],
        "global entitlement reference may not contain granted value",
    )
    for prohibited in ("character", "inventory", "currency", "guild_membership"):
        require(
            prohibited in data_classes["D-GID-01"]["prohibited_global_fields"],
            f"global identity map no longer excludes {prohibited}",
        )

    profiles = index_unique(plan.get("workload_profiles", []), "workload profile")
    require(set(profiles) == {"WF-FUNCTIONAL-v1", "WF-QUOTA-LADDER-v1"}, "workload profiles drifted")
    functional = profiles["WF-FUNCTIONAL-v1"]
    require(
        functional.get("classification") == "functional_equivalence_not_capacity_evidence",
        "functional workload classification drifted",
    )
    require(functional.get("synthetic_only") is True, "functional workload must be synthetic")
    require(
        functional.get("deterministic_seed") == "anotherlife-mmo-bakeoff-v1",
        "functional deterministic seed drifted",
    )
    require(functional.get("realm_count") == 4, "functional workload must cover four realms")
    require(
        functional.get("logical_region_slots") == ["home_region", "forbidden_region"],
        "functional logical region topology drifted",
    )
    require(
        functional.get("realm_home_region_policy")
        == "all_four_realms_are_preassigned_to_home_region",
        "functional home-region policy drifted",
    )
    require(
        functional.get("cross_region_probe_policy")
        == "every_realm_is_probed_against_forbidden_region_without_reassignment",
        "functional cross-region probe policy drifted",
    )
    require(functional.get("synthetic_accounts_per_realm") == 8, "functional account count drifted")
    for field in (
        "identity_cycles_per_account",
        "gameplay_canaries_per_account",
        "economy_canaries_per_account",
        "social_canaries_per_account",
        "excluded_warmups",
    ):
        require(functional.get(field) == 1, f"functional {field} drifted")
    require(functional.get("placement_cycles_per_account") == 2, "placement cycle count drifted")
    require(functional.get("measured_repetitions") == 3, "repetition count drifted")
    require(
        functional.get("candidate_override_permitted") is False,
        "candidate-specific functional workload override is forbidden",
    )
    quota = profiles["WF-QUOTA-LADDER-v1"]
    require(
        quota.get("classification")
        == "sandbox_limit_discovery_not_production_capacity_evidence",
        "quota workload classification drifted",
    )
    require(quota.get("synthetic_only") is True, "quota workload must be synthetic")
    require(
        quota.get("candidate_override_permitted") is False,
        "candidate-specific quota ladder override is forbidden",
    )
    require(
        quota.get("step_source")
        == "identical_steps_up_to_lowest_mutually_authorized_sandbox_boundary",
        "quota ladder lost its symmetric boundary rule",
    )
    require(set(quota.get("dimensions", [])) == QUOTA_DIMENSIONS, "quota dimensions drifted")
    algorithm = quota.get("step_algorithm", {})
    require(algorithm.get("initial_units") == 1, "quota initial step drifted")
    require(algorithm.get("multiplier") == 2, "quota multiplier drifted")
    require(algorithm.get("maximum_steps_per_dimension") == 12, "quota maximum steps drifted")
    require(algorithm.get("measurement_window_seconds") == 60, "quota measurement window drifted")
    require(
        algorithm.get("recovery_probe_interval_seconds") == 5,
        "quota recovery probe interval drifted",
    )
    require(
        algorithm.get("common_ceiling_rule")
        == "minimum_of_both_documented_or_measured_authorized_sandbox_boundaries",
        "quota common ceiling rule drifted",
    )
    require(
        algorithm.get("unknown_boundary_rule")
        == "block_that_dimension_after_functional_baseline_without_extrapolation",
        "quota unknown-boundary rule drifted",
    )
    require(
        algorithm.get("final_boundary_probe")
        == "one_increment_beyond_last_common_accepted_step_only_when_both_candidates_are_authorized",
        "quota final-boundary probe drifted",
    )
    require(
        set(quota.get("required_step_counters", [])) == QUOTA_COUNTERS,
        "quota step counters drifted",
    )
    require(
        set(quota.get("stop_conditions", [])) == QUOTA_STOP_CONDITIONS,
        "quota stop conditions drifted",
    )
    driver = plan.get("common_driver", {})
    require(driver.get("same_for_every_candidate") is True, "driver must be common")
    require(
        driver.get("difference_handling") == "new_workload_id_and_both_candidates_rerun",
        "workload differences must force both candidates to rerun",
    )
    require(
        set(driver.get("required_equal_inputs", [])) == COMMON_DRIVER_EQUAL_INPUTS,
        "common driver equal-input contract drifted",
    )

    threats = index_unique(plan.get("threats", []), "threat")
    require(set(threats) == THREATS, "threat inventory must contain TM-01 through TM-08")
    for threat_id, threat in threats.items():
        expected_name, expected_scenarios = THREAT_POLICY[threat_id]
        require(threat.get("name") == expected_name, f"{threat_id} semantic name drifted")
        require(threat.get("hard_gate") is True, f"{threat_id} must remain a hard gate")
        require(
            set(threat.get("required_scenarios", [])) == expected_scenarios,
            f"{threat_id} scenario coverage drifted",
        )

    scenarios = index_unique(plan.get("scenarios", []), "scenario")
    require(set(scenarios) == SCENARIOS, "scenario set must contain SCN-01 through SCN-16")
    require(
        set(plan.get("scenario_statuses", [])) == {"pass", "fail", "blocked", "not_run"},
        "scenario statuses must fail closed",
    )
    for scenario_id, scenario in scenarios.items():
        expected_name, expected_workload, expected_contracts, required_action, required_pass = (
            SCENARIO_POLICY[scenario_id]
        )
        require(scenario.get("name") == expected_name, f"{scenario_id} semantic name drifted")
        require(
            scenario.get("workload_profile") == expected_workload,
            f"{scenario_id} workload assignment drifted",
        )
        for field in SCENARIO_LIST_FIELDS:
            value = scenario.get(field)
            require(isinstance(value, list) and value, f"{scenario_id} has empty {field}")
            require(
                all(isinstance(entry, str) and entry.strip() for entry in value),
                f"{scenario_id} has blank or non-string {field}",
            )
        require(
            set(scenario["candidates"]) == CANDIDATES,
            f"{scenario_id} must run against both candidates",
        )
        require(
            set(scenario["contracts"]) == expected_contracts,
            f"{scenario_id} contract coverage drifted",
        )
        require(
            required_action in scenario["actions"],
            f"{scenario_id} required semantic action is missing",
        )
        require(
            required_pass in scenario["pass_conditions"],
            f"{scenario_id} required pass condition is missing",
        )
    covered_scenarios = {
        scenario_id
        for threat in threats.values()
        for scenario_id in threat["required_scenarios"]
    }
    require(
        covered_scenarios == SCENARIOS,
        "every common scenario must contribute to threat coverage",
    )

    runbooks = index_unique(plan.get("runbooks", []), "runbook")
    require(set(runbooks) == RUNBOOKS, "four required runbooks are not defined")
    for runbook_id, runbook in runbooks.items():
        require(
            tuple(runbook.get("required_sections", [])) == RUNBOOK_SECTIONS,
            f"{runbook_id} section contract drifted",
        )
        marker = f"## {runbook_id}"
        require(marker in runbooks_document, f"runbook template missing {runbook_id}")
        body = runbooks_document.split(marker, 1)[1]
        following = [f"## {identity}" for identity in RUNBOOKS if f"## {identity}" in body]
        if following:
            positions = [body.index(value) for value in following]
            body = body[: min(positions)]
        for section in RUNBOOK_SECTIONS:
            section_marker = f"### {section}"
            require(
                section_marker in body,
                f"runbook template {runbook_id} missing section {section}",
            )
            section_body = body.split(section_marker, 1)[1]
            later_markers = [
                f"### {candidate}"
                for candidate in RUNBOOK_SECTIONS
                if f"### {candidate}" in section_body
            ]
            if later_markers:
                section_body = section_body[: min(section_body.index(value) for value in later_markers)]
            require(
                len(section_body.strip()) >= 80,
                f"runbook template {runbook_id} section {section} is empty or placeholder-only",
            )

    unknown_limits = index_unique(plan.get("unknown_vendor_limits", []), "unknown limit")
    require(set(unknown_limits) == UNKNOWN_LIMITS, "unknown vendor-limit inventory drifted")
    for limit_id, item in unknown_limits.items():
        require(
            item.get("name") == UNKNOWN_LIMIT_POLICY[limit_id],
            f"{limit_id} measurement definition drifted",
        )
        require(item.get("status") == "measure", f"{limit_id} must remain a measurement item")

    required_fields = set(plan.get("required_evidence_fields", []))
    require(required_fields == REQUIRED_EVIDENCE_FIELDS, "required run-record contract drifted")
    require(required_fields.issubset(run_record), "run record starter misses required top-level fields")
    require(run_record.get("record_schema_version") == "1.0.0", "run record version drifted")
    require(run_record.get("plan_id") == PLAN_ID, "run record plan identity drifted")
    require(run_record.get("candidate_id") is None, "starter must not preselect a candidate")
    require(run_record.get("run_status") == "not_run", "starter must remain not-run")
    require(
        run_record.get("selection_recommendation") is None,
        "starter must not recommend a candidate",
    )
    record_scenarios = index_unique(
        run_record.get("scenario_results", []),
        "record scenario",
        identity_field="scenario_id",
    )
    require(set(record_scenarios) == SCENARIOS, "run record must retain every scenario")
    result_fields = set(plan.get("required_scenario_result_fields", []))
    require(
        result_fields == REQUIRED_SCENARIO_RESULT_FIELDS,
        "required scenario-result contract drifted",
    )
    require(
        set(plan.get("required_evidence_manifest_entry_fields", []))
        == REQUIRED_EVIDENCE_MANIFEST_ENTRY_FIELDS,
        "required evidence-manifest entry contract drifted",
    )
    require(
        plan.get("evidence_handle_rule")
        == "every_handle_is_a_nonblank_relative_packet_path_with_one_matching_manifest_entry",
        "evidence-handle rule drifted",
    )
    for scenario_id, result in record_scenarios.items():
        require(result_fields.issubset(result), f"run record {scenario_id} misses required fields")
        require(result.get("status") == "not_run", f"starter {scenario_id} must be not-run")
        require(
            result.get("rollback_status") == "not_run",
            f"starter {scenario_id} rollback must be not-run",
        )

    targets = plan.get("unproven_scale_targets", [])
    require(len(targets) == 2, "steady and surge target records are required")
    require(
        {target.get("name") for target in targets} == {"steady_ccu", "surge_ccu"},
        "scale target names drifted",
    )
    require(
        all(target.get("claim_class") == "unproven_scale_target" for target in targets),
        "scale targets must remain unproven",
    )

    for section in DOC_SECTIONS:
        require(section in document, f"evidence plan missing section: {section}")
    for identity in DATA_CLASSES | THREATS | SCENARIOS | RUNBOOKS:
        require(identity in document, f"evidence plan missing identity: {identity}")
    for token in (
        "no candidate has been selected",
        "unknown_measurement_required",
        "is deliberately small and is not scale evidence",
        "`no_selection`",
        "Missing evidence is `blocked`, not pass.",
        "No real player, commerce, social, or production data",
    ):
        require(token.lower() in document.lower(), f"evidence plan missing claim boundary: {token}")
    require(
        canonical_json_digest(plan) == EXPECTED_PLAN_DIGEST,
        "versioned scenario plan semantic digest drifted",
    )
    require(
        canonical_json_digest(run_record) == EXPECTED_RUN_RECORD_DIGEST,
        "versioned run-record starter semantic digest drifted",
    )
    require(
        text_digest(document) == EXPECTED_DOCUMENT_DIGEST,
        "versioned evidence-plan document digest drifted",
    )
    require(
        text_digest(runbooks_document) == EXPECTED_RUNBOOKS_DIGEST,
        "versioned runbook template digest drifted",
    )


def nonempty_string(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def finite_nonnegative_number(value: Any) -> bool:
    return (
        isinstance(value, (int, float))
        and not isinstance(value, bool)
        and (not isinstance(value, float) or math.isfinite(value))
        and value >= 0
    )


def parse_utc(value: Any, label: str) -> datetime:
    require(
        isinstance(value, str)
        and re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?Z", value)
        is not None,
        f"{label} is not RFC 3339 UTC",
    )
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as error:
        raise ValidationFailure(f"{label} is not RFC 3339 UTC") from error
    return parsed


def require_sha256(value: Any, label: str) -> str:
    require(nonempty_string(value), f"{label} is blank")
    digest = value.lower()
    require(
        len(digest) == 64 and all(character in "0123456789abcdef" for character in digest),
        f"{label} must be a 64-character SHA-256 digest",
    )
    return digest


def require_git_commit(value: Any, label: str) -> str:
    require(nonempty_string(value), f"{label} is blank")
    commit = value.lower()
    require(
        len(commit) in {40, 64} and all(character in "0123456789abcdef" for character in commit),
        f"{label} must be a full 40- or 64-character Git commit ID",
    )
    return commit


def validate_evidence_handle(value: Any, label: str) -> str:
    require(nonempty_string(value), f"{label} is blank")
    require("\\" not in value, f"{label} must use forward-slash relative paths")
    path = PurePosixPath(value)
    require(not path.is_absolute() and ".." not in path.parts, f"{label} escapes the run packet")
    require(str(path) == value and value not in {".", ""}, f"{label} is not a normalized path")
    return value


def validate_completed_run_record(
    plan: dict[str, Any],
    record: dict[str, Any],
    evidence_root: Path | None = None,
) -> None:
    """Validate a filled candidate packet; blocked scenarios remain explicit evidence."""
    require(
        set(plan.get("required_evidence_fields", [])) == REQUIRED_EVIDENCE_FIELDS,
        "completed record uses a drifted top-level evidence contract",
    )
    require(
        set(plan.get("required_scenario_result_fields", []))
        == REQUIRED_SCENARIO_RESULT_FIELDS,
        "completed record uses a drifted scenario-result contract",
    )
    require(
        set(plan.get("required_evidence_manifest_entry_fields", []))
        == REQUIRED_EVIDENCE_MANIFEST_ENTRY_FIELDS,
        "completed record uses a drifted evidence-manifest contract",
    )
    require(
        REQUIRED_EVIDENCE_FIELDS.issubset(record),
        "completed record misses required top-level fields",
    )
    require(record.get("record_schema_version") == "1.0.0", "completed record version drifted")
    require(record.get("plan_id") == PLAN_ID, "completed record plan identity drifted")
    candidate_id = record.get("candidate_id")
    require(candidate_id in CANDIDATES, "completed record candidate is invalid")
    require(
        record.get("spike_task_id") == CANDIDATE_TASK_BY_ID[candidate_id],
        "completed record candidate task mapping drifted",
    )
    require(
        record.get("run_status") in {"completed", "failed", "blocked"},
        "completed record has invalid run status",
    )
    require(
        record.get("claim_class") == "observed_sandbox_fact",
        "completed record must contain sandbox observations",
    )
    for field in ("run_id", "started_utc", "ended_utc", "region_id"):
        require(nonempty_string(record.get(field)), f"completed record has empty {field}")
    require_git_commit(record.get("driver_commit"), "completed record driver_commit")
    for field in (
        "server_artifact_fingerprint",
        "adapter_fingerprint",
        "configuration_hash",
        "workload_manifest_hash",
    ):
        require_sha256(record.get(field), f"completed record {field}")
    run_started = parse_utc(record["started_utc"], "completed record started_utc")
    run_ended = parse_utc(record["ended_utc"], "completed record ended_utc")
    require(run_ended >= run_started, "completed record ends before it starts")
    require(
        record.get("synthetic_fixture_seed") == "anotherlife-mmo-bakeoff-v1",
        "completed record fixture seed drifted",
    )
    realm_ids = record.get("realm_ids")
    require(
        isinstance(realm_ids, list)
        and len(realm_ids) == 4
        and len(set(realm_ids)) == 4
        and all(nonempty_string(value) for value in realm_ids),
        "completed record must identify four distinct synthetic realms",
    )

    manifest = record.get("raw_evidence_manifest")
    require(isinstance(manifest, list) and manifest, "completed record has empty evidence manifest")
    resolved_evidence_root: Path | None = None
    if evidence_root is not None:
        try:
            resolved_evidence_root = evidence_root.resolve(strict=True)
        except OSError as error:
            raise ValidationFailure("evidence root is missing or inaccessible") from error
        require(resolved_evidence_root.is_dir(), "evidence root is not a directory")
    manifest_by_path: dict[str, dict[str, Any]] = {}
    workload_manifest_entry_count = 0
    common_driver_manifest: dict[str, Any] | None = None
    for index, entry in enumerate(manifest):
        require(isinstance(entry, dict), f"evidence manifest entry {index} is not an object")
        require(
            REQUIRED_EVIDENCE_MANIFEST_ENTRY_FIELDS.issubset(entry),
            f"evidence manifest entry {index} misses required fields",
        )
        path = validate_evidence_handle(entry.get("path"), f"evidence manifest entry {index} path")
        require(path not in manifest_by_path, f"duplicate evidence manifest path: {path}")
        digest = entry.get("sha256")
        require(
            isinstance(digest, str)
            and len(digest) == 64
            and digest == digest.lower()
            and all(character in "0123456789abcdef" for character in digest),
            f"evidence manifest entry {path} has invalid SHA-256",
        )
        size = entry.get("bytes")
        require(
            isinstance(size, int) and not isinstance(size, bool) and size >= 0,
            f"evidence manifest entry {path} has invalid byte count",
        )
        require(
            nonempty_string(entry.get("content_type")),
            f"evidence manifest entry {path} has empty content type",
        )
        require(
            entry.get("classification")
            in {"sanitized_common", "restricted_adapter_diagnostic"},
            f"evidence manifest entry {path} has invalid classification",
        )
        scenario_ids = entry.get("scenario_ids")
        require(
            isinstance(scenario_ids, list)
            and len(set(scenario_ids)) == len(scenario_ids)
            and set(scenario_ids).issubset(SCENARIOS),
            f"evidence manifest entry {path} has invalid scenario coverage",
        )
        manifest_by_path[path] = entry
        if resolved_evidence_root is not None:
            artifact = resolved_evidence_root.joinpath(*PurePosixPath(path).parts)
            try:
                resolved_artifact = artifact.resolve(strict=True)
                resolved_artifact.relative_to(resolved_evidence_root)
            except ValueError as error:
                raise ValidationFailure(
                    f"evidence artifact resolves outside the run packet: {path}"
                ) from error
            except OSError as error:
                raise ValidationFailure(f"evidence artifact is missing: {path}") from error
            require(resolved_artifact.is_file(), f"evidence artifact is missing: {path}")
            payload = resolved_artifact.read_bytes()
            require(len(payload) == size, f"evidence artifact byte count drifted: {path}")
            require(
                hashlib.sha256(payload).hexdigest() == digest,
                f"evidence artifact SHA-256 drifted: {path}",
            )
            if digest == record["workload_manifest_hash"]:
                require(
                    entry.get("content_type") == "application/json",
                    "common-driver workload manifest must be JSON",
                )
                try:
                    parsed_manifest = json.loads(payload)
                except (UnicodeDecodeError, json.JSONDecodeError) as error:
                    raise ValidationFailure(
                        "common-driver workload manifest is not valid JSON"
                    ) from error
                require(
                    isinstance(parsed_manifest, dict),
                    "common-driver workload manifest is not an object",
                )
                common_driver_manifest = parsed_manifest
        if digest == record["workload_manifest_hash"]:
            workload_manifest_entry_count += 1

    require(
        workload_manifest_entry_count == 1,
        "workload_manifest_hash must identify exactly one evidence artifact",
    )
    if resolved_evidence_root is not None:
        require(common_driver_manifest is not None, "common-driver workload manifest is missing")
        require(
            set(common_driver_manifest) == COMMON_DRIVER_EQUAL_INPUTS,
            "common-driver workload manifest input set drifted",
        )
        require(
            all(
                value is not None
                and (not isinstance(value, str) or bool(value.strip()))
                and (not isinstance(value, (list, dict)) or bool(value))
                for value in common_driver_manifest.values()
            ),
            "common-driver workload manifest contains an empty input",
        )
        require(
            common_driver_manifest["driver_commit"] == record["driver_commit"],
            "common-driver manifest driver commit disagrees with run record",
        )
        require(
            common_driver_manifest["server_artifact_fingerprint"]
            == record["server_artifact_fingerprint"],
            "common-driver manifest server artifact disagrees with run record",
        )
        require(
            common_driver_manifest["synthetic_fixture_seed"]
            == record["synthetic_fixture_seed"],
            "common-driver manifest fixture seed disagrees with run record",
        )

    referenced_handles: set[str] = set()

    def collect_handles(values: Any, label: str, require_values: bool = False) -> list[str]:
        require(isinstance(values, list), f"{label} is not a list")
        require(not require_values or bool(values), f"{label} is empty")
        handles = [
            validate_evidence_handle(value, f"{label} entry")
            for value in values
        ]
        require(len(set(handles)) == len(handles), f"{label} contains duplicates")
        referenced_handles.update(handles)
        return handles

    scenario_results = index_unique(
        record.get("scenario_results", []),
        "completed record scenario",
        identity_field="scenario_id",
    )
    require(set(scenario_results) == SCENARIOS, "completed record must retain every scenario")
    scenario_statuses: list[str] = []
    for scenario_id, result in scenario_results.items():
        require(
            REQUIRED_SCENARIO_RESULT_FIELDS.issubset(result),
            f"completed record {scenario_id} misses required fields",
        )
        status = result.get("status")
        require(
            status in {"pass", "fail", "blocked"},
            f"completed record {scenario_id} has invalid status",
        )
        scenario_statuses.append(status)
        scenario_started = parse_utc(
            result.get("started_utc"), f"completed record {scenario_id} started_utc"
        )
        scenario_ended = parse_utc(
            result.get("ended_utc"), f"completed record {scenario_id} ended_utc"
        )
        require(
            run_started <= scenario_started <= scenario_ended <= run_ended,
            f"completed record {scenario_id} timestamps are outside the run",
        )
        for field in ("operation_ids", "correlation_ids"):
            values = result.get(field)
            require(
                isinstance(values, list)
                and values
                and all(nonempty_string(value) for value in values),
                f"completed record {scenario_id} {field} is empty or invalid",
            )
        for field in ("limitations", "blockers", "contract_violations"):
            values = result.get(field)
            require(
                isinstance(values, list) and all(nonempty_string(value) for value in values),
                f"completed record {scenario_id} {field} is invalid",
            )
        measurement_handles = collect_handles(
            result.get("measurement_handles"),
            f"completed record {scenario_id} measurement handles",
            require_values=status in {"pass", "fail"},
        )
        raw_handles = collect_handles(
            result.get("raw_evidence_handles"),
            f"completed record {scenario_id} raw evidence handles",
            require_values=status in {"pass", "fail"},
        )
        rollback_handles = collect_handles(
            result.get("rollback_evidence_handles"),
            f"completed record {scenario_id} rollback evidence handles",
            require_values=status in {"pass", "fail"},
        )
        for handle in measurement_handles + raw_handles + rollback_handles:
            require(
                scenario_id in manifest_by_path.get(handle, {}).get("scenario_ids", []),
                f"evidence handle {handle} does not cover {scenario_id}",
            )
        counts = result.get("stable_result_counts")
        require(
            isinstance(counts, dict)
            and counts
            and all(
                nonempty_string(key)
                and finite_nonnegative_number(value)
                for key, value in counts.items()
            )
            and any(value > 0 for value in counts.values()),
            f"completed record {scenario_id} stable result counts are not substantive",
        )
        rollback_status = result.get("rollback_status")
        require(
            rollback_status in {"pass", "fail", "blocked"},
            f"completed record {scenario_id} has invalid rollback status",
        )
        if status == "pass":
            require(not result["blockers"], f"passed scenario {scenario_id} has blockers")
            require(
                not result["contract_violations"],
                f"passed scenario {scenario_id} has contract violations",
            )
            require(rollback_status == "pass", f"passed scenario {scenario_id} rollback did not pass")
        elif status == "fail":
            require(
                result["limitations"] or result["contract_violations"],
                f"failed scenario {scenario_id} lacks a limitation or contract violation",
            )
        else:
            require(result["blockers"], f"blocked scenario {scenario_id} lacks a named blocker")

    run_status = record["run_status"]
    if run_status == "failed":
        require("fail" in scenario_statuses, "failed run has no failed scenario")
    elif run_status == "blocked":
        require(
            "blocked" in scenario_statuses and "fail" not in scenario_statuses,
            "blocked run status disagrees with scenario statuses",
        )
    else:
        require(
            set(scenario_statuses) == {"pass"},
            "completed run must contain only passed scenarios",
        )

    for field in (
        "data_residency_inventory",
        "quota_inventory",
        "credential_inventory",
        "teardown_inventory",
    ):
        collect_handles(record.get(field), f"completed record {field}", require_values=True)
    for field in ("limitations", "blockers", "contract_violations"):
        require(
            isinstance(record.get(field), list)
            and all(nonempty_string(value) for value in record[field]),
            f"completed record {field} is invalid",
        )
    if run_status == "completed":
        require(not record["blockers"], "completed run has top-level blockers")
        require(
            not record["contract_violations"],
            "completed run has top-level contract violations",
        )
    elif run_status == "blocked":
        require(record["blockers"], "blocked run lacks a top-level named blocker")
    rollback = record.get("rollback_result", {})
    require(
        rollback.get("status") in {"pass", "fail", "blocked"},
        "completed record has invalid aggregate rollback status",
    )
    if run_status == "completed":
        require(
            rollback.get("status") == "pass",
            "completed run aggregate rollback did not pass",
        )
    for field in (
        "neutral_configuration_hash_before",
        "neutral_configuration_hash_after",
        "regional_state_hash_before",
        "regional_state_hash_after",
    ):
        require_sha256(rollback.get(field), f"completed record rollback {field}")
    require(
        rollback.get("core_tests") in {"pass", "fail", "blocked"},
        "completed record rollback core tests have invalid status",
    )
    if run_status == "completed":
        require(
            rollback["neutral_configuration_hash_before"]
            == rollback["neutral_configuration_hash_after"],
            "completed record rollback did not restore the neutral configuration hash",
        )
        require(
            rollback["regional_state_hash_before"] == rollback["regional_state_hash_after"],
            "completed record rollback changed the regional state hash",
        )
        require(
            rollback["core_tests"] == "pass",
            "completed record rollback core tests did not pass",
        )
    aggregate_rollback_handles = collect_handles(
        rollback.get("evidence_handles"),
        "completed record aggregate rollback evidence handles",
        require_values=rollback.get("status") != "blocked",
    )
    require(
        aggregate_rollback_handles or record["blockers"],
        "completed record has neither aggregate rollback evidence nor blocker",
    )
    effort = record.get("operator_effort", {})
    for field in ("automated_duration_seconds", "manual_duration_seconds"):
        require(
            finite_nonnegative_number(effort.get(field)),
            f"completed record operator effort has invalid {field}",
        )
    for field in ("manual_steps", "support_interactions"):
        require(isinstance(effort.get(field), list), f"completed record operator effort {field} is not a list")
    secret_scan = record.get("secret_scan_result", {})
    require(secret_scan.get("status") == "pass", "completed record secret scan did not pass")
    require(nonempty_string(secret_scan.get("command")), "completed record secret scan command is empty")
    secret_scan_handle = validate_evidence_handle(
        secret_scan.get("evidence_handle"), "completed record secret scan evidence handle"
    )
    referenced_handles.add(secret_scan_handle)
    require(secret_scan.get("findings") == [], "completed record secret scan has findings")
    require(
        record.get("selection_recommendation") is None,
        "completed spike record must not recommend a candidate",
    )
    unknown_handles = referenced_handles - set(manifest_by_path)
    require(not unknown_handles, f"evidence handles are absent from manifest: {sorted(unknown_handles)}")


def validate_record_pair_equivalence(records: list[dict[str, Any]]) -> None:
    """Fail closed when a two-candidate comparison used different common inputs."""
    require(len(records) == 2, "a paired comparison requires exactly two completed records")
    records_by_candidate = index_unique(
        records,
        "completed record candidate",
        identity_field="candidate_id",
    )
    require(
        set(records_by_candidate) == CANDIDATES,
        "paired comparison must include each candidate exactly once",
    )
    first, second = (
        records_by_candidate[candidate_id]
        for candidate_id in sorted(CANDIDATES)
    )
    for field in (
        "plan_id",
        "driver_commit",
        "server_artifact_fingerprint",
        "workload_manifest_hash",
        "synthetic_fixture_seed",
        "realm_ids",
    ):
        require(
            first.get(field) == second.get(field),
            f"paired comparison common-driver input drifted: {field}",
        )


def validate(root: Path) -> None:
    architecture = root / "unity" / "Docs" / "Architecture"
    plan = read_json(architecture / "MMO_Provider_Bakeoff_Scenarios_v1.json")
    run_record = read_json(
        architecture / "Templates" / "MMO_Provider_Spike_Run_Record_v1.json"
    )
    document = read_text(architecture / "MMO_Provider_Bakeoff_Evidence_Plan_v1.md")
    runbooks = read_text(
        architecture / "Templates" / "MMO_Provider_Bakeoff_Runbooks_v1.md"
    )
    validate_documents(plan, run_record, document, runbooks)


def main() -> int:
    arguments = sys.argv[1:]
    record_paths: list[Path] = []
    while "--record" in arguments:
        index = arguments.index("--record")
        if index + 1 >= len(arguments):
            print("FAIL: --record requires a JSON path", file=sys.stderr)
            return 1
        record_paths.append(Path(arguments[index + 1]).resolve())
        del arguments[index : index + 2]
    root = Path(arguments[0] if arguments else ".").resolve()
    try:
        validate(root)
        if record_paths:
            plan = read_json(
                root
                / "unity"
                / "Docs"
                / "Architecture"
                / "MMO_Provider_Bakeoff_Scenarios_v1.json"
            )
            records = []
            for record_path in record_paths:
                record = read_json(record_path)
                validate_completed_run_record(
                    plan,
                    record,
                    evidence_root=record_path.parent,
                )
                records.append(record)
            if len(records) > 1:
                validate_record_pair_equivalence(records)
    except (ValidationFailure, OSError, UnicodeError, json.JSONDecodeError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1

    print(
        "PASS: 2 equivalent candidates, 10 data classes, 8 threats, 16 common "
        "scenarios, 9 vendor-limit measurements, 4 runbooks, fail-closed run record, "
        "and valid no-selection outcome"
        + ("; completed run record(s) valid" if record_paths else "")
        + (" and common-driver inputs equivalent" if len(record_paths) == 2 else "")
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
