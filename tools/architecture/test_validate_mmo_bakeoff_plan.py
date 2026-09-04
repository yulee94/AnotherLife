#!/usr/bin/env python
"""Adversarial tests for the MMO provider bake-off evidence validator."""

from __future__ import annotations

import contextlib
import copy
import hashlib
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path, PurePosixPath
from typing import Any


THIS_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = THIS_DIR.parents[1]
sys.path.insert(0, str(THIS_DIR))

import validate_mmo_bakeoff_plan as target  # noqa: E402


class BakeoffPlanValidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        architecture = REPOSITORY_ROOT / "unity" / "Docs" / "Architecture"
        cls.plan: dict[str, Any] = json.loads(
            (architecture / "MMO_Provider_Bakeoff_Scenarios_v1.json").read_text(
                encoding="utf-8"
            )
        )
        cls.run_record: dict[str, Any] = json.loads(
            (
                architecture
                / "Templates"
                / "MMO_Provider_Spike_Run_Record_v1.json"
            ).read_text(encoding="utf-8")
        )
        cls.document = (
            architecture / "MMO_Provider_Bakeoff_Evidence_Plan_v1.md"
        ).read_text(encoding="utf-8")
        cls.runbooks = (
            architecture / "Templates" / "MMO_Provider_Bakeoff_Runbooks_v1.md"
        ).read_text(encoding="utf-8")

    def validate(
        self,
        *,
        plan: dict[str, Any] | None = None,
        run_record: dict[str, Any] | None = None,
        document: str | None = None,
        runbooks: str | None = None,
    ) -> None:
        target.validate_documents(
            copy.deepcopy(self.plan if plan is None else plan),
            copy.deepcopy(self.run_record if run_record is None else run_record),
            self.document if document is None else document,
            self.runbooks if runbooks is None else runbooks,
        )

    @staticmethod
    def common_driver_manifest(record: dict[str, Any]) -> dict[str, Any]:
        return {
            "driver_commit": record["driver_commit"],
            "server_artifact_fingerprint": record["server_artifact_fingerprint"],
            "adapter_contract_version": "MMO-CONTRACTS-v1.0.0",
            "workload_manifest_hash": "sha256-of-this-canonical-manifest",
            "configuration_shape": {
                "neutral_fields": "fixed",
                "candidate_adapter_section": "shape_only",
            },
            "synthetic_fixture_seed": record["synthetic_fixture_seed"],
            "request_envelopes": ["placement", "lifecycle", "gameplay", "economy", "social"],
            "operation_ids": "deterministic-per-scenario-and-repetition",
            "payload_bytes": {"source": "canonical-synthetic-fixtures"},
            "topology": {"realms": 4, "logical_regions": ["home_region", "forbidden_region"]},
            "fault_schedule": ["baseline", "inject", "observe", "recover", "rollback"],
            "observation_schema": "MMO-BAKEOFF-v1.0.0",
            "repetitions": {"excluded_warmups": 1, "measured": 3},
            "warmup_rule": "exclude_exactly_one_identical_warmup",
            "teardown_assertions": ["neutral_restore", "regional_state_unchanged", "no_residuals"],
        }

    def evidence_payload(self, record: dict[str, Any], path: str) -> bytes:
        if path == "evidence/common-driver-manifest.json":
            return json.dumps(
                self.common_driver_manifest(record),
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        return b"x"

    def completed_record(self) -> dict[str, Any]:
        record = copy.deepcopy(self.run_record)
        run_level_handles = {
            "evidence/residency.json": [],
            "evidence/quotas.json": [],
            "evidence/credential-references.json": [],
            "evidence/teardown.json": [],
            "evidence/rollback.json": [],
            "evidence/secret-scan.txt": [],
            "evidence/common-driver-manifest.json": [],
        }
        record.update(
            {
                "candidate_id": "amazon_gamelift",
                "spike_task_id": "t_ff702849",
                "run_id": "synthetic-review-run",
                "run_status": "completed",
                "started_utc": "2026-08-31T00:00:00Z",
                "ended_utc": "2026-08-31T00:10:00Z",
                "driver_commit": "a" * 40,
                "server_artifact_fingerprint": "b" * 64,
                "adapter_fingerprint": "c" * 64,
                "configuration_hash": "d" * 64,
                "workload_manifest_hash": "e" * 64,
                "region_id": "synthetic-home-region",
                "realm_ids": ["realm-1", "realm-2", "realm-3", "realm-4"],
                "data_residency_inventory": ["evidence/residency.json"],
                "quota_inventory": ["evidence/quotas.json"],
                "credential_inventory": ["evidence/credential-references.json"],
                "teardown_inventory": ["evidence/teardown.json"],
            }
        )
        for result in record["scenario_results"]:
            scenario_id = result["scenario_id"]
            measurement_handle = f"evidence/{scenario_id}-measurement.json"
            raw_handle = f"evidence/{scenario_id}.json"
            rollback_handle = f"evidence/{scenario_id}-rollback.json"
            run_level_handles.update(
                {
                    measurement_handle: [scenario_id],
                    raw_handle: [scenario_id],
                    rollback_handle: [scenario_id],
                }
            )
            result.update(
                {
                    "status": "pass",
                    "started_utc": "2026-08-31T00:00:00Z",
                    "ended_utc": "2026-08-31T00:01:00Z",
                    "operation_ids": [f"operation-{scenario_id}"],
                    "correlation_ids": [f"correlation-{scenario_id}"],
                    "stable_result_counts": {"succeeded": 1},
                    "measurement_handles": [measurement_handle],
                    "raw_evidence_handles": [raw_handle],
                    "rollback_status": "pass",
                    "rollback_evidence_handles": [rollback_handle],
                }
            )
        record["rollback_result"].update(
            {
                "status": "pass",
                "neutral_configuration_hash_before": "f" * 64,
                "neutral_configuration_hash_after": "f" * 64,
                "regional_state_hash_before": "1" * 64,
                "regional_state_hash_after": "1" * 64,
                "core_tests": "pass",
                "evidence_handles": ["evidence/rollback.json"],
            }
        )
        record["operator_effort"].update(
            {"automated_duration_seconds": 540, "manual_duration_seconds": 60}
        )
        record["secret_scan_result"].update(
            {
                "status": "pass",
                "command": "synthetic-secret-scan",
                "evidence_handle": "evidence/secret-scan.txt",
            }
        )
        record["workload_manifest_hash"] = hashlib.sha256(
            self.evidence_payload(record, "evidence/common-driver-manifest.json")
        ).hexdigest()
        record["raw_evidence_manifest"] = [
            {
                "path": path,
                "sha256": hashlib.sha256(self.evidence_payload(record, path)).hexdigest(),
                "bytes": len(self.evidence_payload(record, path)),
                "content_type": "application/json",
                "classification": "sanitized_common",
                "scenario_ids": scenario_ids,
            }
            for path, scenario_ids in sorted(run_level_handles.items())
        ]
        return record

    def test_canonical_package_passes(self) -> None:
        self.validate()

    def test_missing_candidate_fails_closed(self) -> None:
        plan = copy.deepcopy(self.plan)
        plan["candidates"] = [plan["candidates"][0]]
        with self.assertRaisesRegex(target.ValidationFailure, "candidate set"):
            self.validate(plan=plan)

    def test_candidate_task_mappings_cannot_be_swapped(self) -> None:
        plan = copy.deepcopy(self.plan)
        first, second = plan["candidates"]
        first["spike_task_id"], second["spike_task_id"] = (
            second["spike_task_id"],
            first["spike_task_id"],
        )
        with self.assertRaisesRegex(target.ValidationFailure, "task mapping drifted"):
            self.validate(plan=plan)

    def test_preselected_winner_fails_closed(self) -> None:
        plan = copy.deepcopy(self.plan)
        plan["decision"]["winner_preselected"] = True
        with self.assertRaisesRegex(target.ValidationFailure, "must not be preselected"):
            self.validate(plan=plan)

    def test_no_selection_outcome_cannot_be_removed(self) -> None:
        plan = copy.deepcopy(self.plan)
        plan["decision"]["allowed_outcomes"].remove("no_selection")
        with self.assertRaisesRegex(target.ValidationFailure, "allowed outcomes"):
            self.validate(plan=plan)

    def test_globalizing_economy_fails_closed(self) -> None:
        plan = copy.deepcopy(self.plan)
        economy = next(item for item in plan["data_classes"] if item["id"] == "D-ECO-01")
        economy["authority_scope"] = "global_minimized"
        with self.assertRaisesRegex(target.ValidationFailure, "D-ECO-01 authority scope drifted"):
            self.validate(plan=plan)

    def test_provider_cannot_become_economy_authority(self) -> None:
        plan = copy.deepcopy(self.plan)
        economy = next(item for item in plan["data_classes"] if item["id"] == "D-ECO-01")
        economy["sole_authority"] = "provider_global_economy"
        with self.assertRaisesRegex(target.ValidationFailure, "D-ECO-01 sole authority drifted"):
            self.validate(plan=plan)

    def test_currency_ledger_cannot_enter_global_allowlist(self) -> None:
        plan = copy.deepcopy(self.plan)
        economy = next(item for item in plan["data_classes"] if item["id"] == "D-ECO-01")
        economy["globally_replicable_fields"].append("currency_ledger")
        with self.assertRaisesRegex(target.ValidationFailure, "global replication allowlist drifted"):
            self.validate(plan=plan)

    def test_scenario_must_cover_both_candidates(self) -> None:
        plan = copy.deepcopy(self.plan)
        plan["scenarios"][0]["candidates"] = ["amazon_gamelift"]
        with self.assertRaisesRegex(target.ValidationFailure, "must run against both candidates"):
            self.validate(plan=plan)

    def test_placeholder_scenario_semantics_fail_closed(self) -> None:
        plan = copy.deepcopy(self.plan)
        scenario = plan["scenarios"][0]
        scenario["actions"] = ["placeholder"]
        scenario["pass_conditions"] = ["placeholder"]
        with self.assertRaisesRegex(target.ValidationFailure, "required semantic action"):
            self.validate(plan=plan)

    def test_threat_semantic_name_cannot_be_erased(self) -> None:
        plan = copy.deepcopy(self.plan)
        plan["threats"][0]["name"] = "placeholder"
        with self.assertRaisesRegex(target.ValidationFailure, "semantic name drifted"):
            self.validate(plan=plan)

    def test_workload_cannot_claim_production_capacity(self) -> None:
        plan = copy.deepcopy(self.plan)
        plan["workload_profiles"][0]["classification"] = "production_capacity_proven"
        with self.assertRaisesRegex(target.ValidationFailure, "classification drifted"):
            self.validate(plan=plan)

    def test_functional_canaries_and_seed_cannot_be_erased(self) -> None:
        plan = copy.deepcopy(self.plan)
        functional = plan["workload_profiles"][0]
        functional["deterministic_seed"] = "placeholder"
        functional["economy_canaries_per_account"] = 0
        with self.assertRaisesRegex(target.ValidationFailure, "deterministic seed drifted"):
            self.validate(plan=plan)

    def test_cross_region_topology_cannot_be_erased(self) -> None:
        plan = copy.deepcopy(self.plan)
        plan["workload_profiles"][0]["logical_region_slots"] = ["home_region"]
        with self.assertRaisesRegex(target.ValidationFailure, "logical region topology drifted"):
            self.validate(plan=plan)

    def test_quota_counters_and_stop_conditions_cannot_be_erased(self) -> None:
        plan = copy.deepcopy(self.plan)
        quota = plan["workload_profiles"][1]
        quota["required_step_counters"] = []
        quota["stop_conditions"] = []
        with self.assertRaisesRegex(target.ValidationFailure, "quota step counters drifted"):
            self.validate(plan=plan)

    def test_common_driver_inputs_cannot_be_replaced_with_placeholders(self) -> None:
        plan = copy.deepcopy(self.plan)
        plan["common_driver"]["required_equal_inputs"] = [
            f"placeholder-{index}" for index in range(15)
        ]
        with self.assertRaisesRegex(target.ValidationFailure, "equal-input contract drifted"):
            self.validate(plan=plan)

    def test_unknown_vendor_limit_cannot_be_silently_resolved(self) -> None:
        plan = copy.deepcopy(self.plan)
        plan["unknown_vendor_limits"][0]["status"] = "assumed_unlimited"
        with self.assertRaisesRegex(target.ValidationFailure, "must remain a measurement item"):
            self.validate(plan=plan)

    def test_unknown_vendor_limit_definition_cannot_be_erased(self) -> None:
        plan = copy.deepcopy(self.plan)
        plan["unknown_vendor_limits"][0]["name"] = "placeholder"
        with self.assertRaisesRegex(target.ValidationFailure, "measurement definition drifted"):
            self.validate(plan=plan)

    def test_data_prohibition_and_scenario_coverage_cannot_be_erased(self) -> None:
        plan = copy.deepcopy(self.plan)
        economy = next(item for item in plan["data_classes"] if item["id"] == "D-ECO-01")
        economy["prohibited_global_fields"] = []
        economy["required_scenarios"] = ["SCN-01"]
        with self.assertRaisesRegex(target.ValidationFailure, "scenario coverage drifted"):
            self.validate(plan=plan)

    def test_required_evidence_contract_cannot_be_self_redefined(self) -> None:
        plan = copy.deepcopy(self.plan)
        plan["required_evidence_fields"] = ["plan_id"]
        plan["required_scenario_result_fields"] = ["scenario_id"]
        with self.assertRaisesRegex(target.ValidationFailure, "run-record contract drifted"):
            self.validate(plan=plan)

    def test_completed_record_contract_accepts_filled_packet(self) -> None:
        target.validate_completed_run_record(self.plan, self.completed_record())

    def test_completed_record_cannot_leave_scenario_not_run(self) -> None:
        record = self.completed_record()
        record["scenario_results"][0]["status"] = "not_run"
        with self.assertRaisesRegex(target.ValidationFailure, "invalid status"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_requires_evidence_or_blocker(self) -> None:
        record = self.completed_record()
        result = record["scenario_results"][0]
        result["raw_evidence_handles"] = []
        result["blockers"] = []
        with self.assertRaisesRegex(target.ValidationFailure, "raw evidence handles is empty"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_rejects_blank_handles_and_empty_counts(self) -> None:
        record = self.completed_record()
        result = record["scenario_results"][0]
        result["raw_evidence_handles"] = [""]
        result["stable_result_counts"] = {}
        with self.assertRaisesRegex(target.ValidationFailure, "entry is blank"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_rejects_empty_operation_identity(self) -> None:
        record = self.completed_record()
        record["scenario_results"][0]["operation_ids"] = []
        with self.assertRaisesRegex(target.ValidationFailure, "operation_ids is empty"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_blocked_status_requires_named_blocker(self) -> None:
        record = self.completed_record()
        record["run_status"] = "blocked"
        for result in record["scenario_results"]:
            result.update(
                {
                    "status": "blocked",
                    "measurement_handles": [],
                    "raw_evidence_handles": [],
                    "rollback_status": "blocked",
                    "rollback_evidence_handles": [],
                    "blockers": [],
                    "stable_result_counts": {"attempted": 1},
                }
            )
        with self.assertRaisesRegex(target.ValidationFailure, "lacks a named blocker"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_rejects_blank_inventory_handle(self) -> None:
        record = self.completed_record()
        record["quota_inventory"] = [""]
        with self.assertRaisesRegex(target.ValidationFailure, "entry is blank"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_rejects_manifest_traversal(self) -> None:
        record = self.completed_record()
        record["raw_evidence_manifest"][0]["path"] = "../outside.json"
        with self.assertRaisesRegex(target.ValidationFailure, "escapes the run packet"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_rejects_timestamp_reversal(self) -> None:
        record = self.completed_record()
        record["ended_utc"] = "2026-08-30T23:59:59Z"
        with self.assertRaisesRegex(target.ValidationFailure, "ends before it starts"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_rejects_non_rfc3339_timestamp(self) -> None:
        record = self.completed_record()
        record["started_utc"] = "2026-08-31 00:00:00Z"
        with self.assertRaisesRegex(target.ValidationFailure, "not RFC 3339 UTC"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_status_rejects_blocked_scenario(self) -> None:
        record = self.completed_record()
        result = record["scenario_results"][0]
        result.update(
            {
                "status": "blocked",
                "measurement_handles": [],
                "raw_evidence_handles": [],
                "rollback_status": "blocked",
                "rollback_evidence_handles": [],
                "blockers": ["synthetic blocker"],
            }
        )
        with self.assertRaisesRegex(target.ValidationFailure, "only passed scenarios"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_status_rejects_top_level_blocker_and_failed_rollback(self) -> None:
        record = self.completed_record()
        record["blockers"] = ["synthetic blocker"]
        record["rollback_result"]["status"] = "fail"
        with self.assertRaisesRegex(target.ValidationFailure, "top-level blockers"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_status_requires_restored_hashes_and_passing_core_tests(self) -> None:
        record = self.completed_record()
        record["rollback_result"]["regional_state_hash_after"] = "2" * 64
        with self.assertRaisesRegex(target.ValidationFailure, "changed the regional state hash"):
            target.validate_completed_run_record(self.plan, record)
        record = self.completed_record()
        record["rollback_result"]["core_tests"] = "fail"
        with self.assertRaisesRegex(target.ValidationFailure, "core tests did not pass"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_rejects_malformed_commit_and_fingerprint(self) -> None:
        record = self.completed_record()
        record["driver_commit"] = "not-a-commit"
        with self.assertRaisesRegex(target.ValidationFailure, "Git commit ID"):
            target.validate_completed_run_record(self.plan, record)
        record = self.completed_record()
        record["configuration_hash"] = "not-a-digest"
        with self.assertRaisesRegex(target.ValidationFailure, "SHA-256 digest"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_rejects_boolean_operator_duration(self) -> None:
        record = self.completed_record()
        record["operator_effort"]["manual_duration_seconds"] = True
        with self.assertRaisesRegex(target.ValidationFailure, "manual_duration_seconds"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_rejects_non_finite_measurements(self) -> None:
        record = self.completed_record()
        record["scenario_results"][0]["stable_result_counts"] = {"succeeded": float("inf")}
        with self.assertRaisesRegex(target.ValidationFailure, "not substantive"):
            target.validate_completed_run_record(self.plan, record)
        record = self.completed_record()
        record["operator_effort"]["automated_duration_seconds"] = float("inf")
        with self.assertRaisesRegex(target.ValidationFailure, "automated_duration_seconds"):
            target.validate_completed_run_record(self.plan, record)

    def test_completed_record_handles_arbitrary_precision_json_integers(self) -> None:
        record = self.completed_record()
        large_integer = 10**1000
        record["scenario_results"][0]["stable_result_counts"] = {
            "succeeded": large_integer
        }
        record["operator_effort"]["automated_duration_seconds"] = large_integer
        target.validate_completed_run_record(self.plan, record)

    def test_completed_record_requires_workload_manifest_artifact(self) -> None:
        record = self.completed_record()
        record["workload_manifest_hash"] = "5" * 64
        with self.assertRaisesRegex(target.ValidationFailure, "exactly one evidence artifact"):
            target.validate_completed_run_record(self.plan, record)

    def test_common_driver_manifest_requires_complete_equal_input_set(self) -> None:
        record = self.completed_record()
        path = "evidence/common-driver-manifest.json"
        incomplete = self.common_driver_manifest(record)
        incomplete.pop("fault_schedule")
        payload = json.dumps(incomplete, sort_keys=True, separators=(",", ":")).encode("utf-8")
        digest = hashlib.sha256(payload).hexdigest()
        record["workload_manifest_hash"] = digest
        entry = next(item for item in record["raw_evidence_manifest"] if item["path"] == path)
        entry.update({"sha256": digest, "bytes": len(payload)})
        with tempfile.TemporaryDirectory(prefix="al-bakeoff-driver-") as temporary:
            packet_root = Path(temporary)
            for item in record["raw_evidence_manifest"]:
                artifact = packet_root.joinpath(*PurePosixPath(item["path"]).parts)
                artifact.parent.mkdir(parents=True, exist_ok=True)
                artifact.write_bytes(
                    payload if item["path"] == path else self.evidence_payload(record, item["path"])
                )
            with self.assertRaisesRegex(target.ValidationFailure, "input set drifted"):
                target.validate_completed_run_record(
                    self.plan,
                    record,
                    evidence_root=packet_root,
                )

    def test_completed_record_rejects_artifact_hash_mismatch(self) -> None:
        record = self.completed_record()
        with tempfile.TemporaryDirectory(prefix="al-bakeoff-hash-") as temporary:
            packet_root = Path(temporary)
            for entry in record["raw_evidence_manifest"]:
                artifact = packet_root.joinpath(*PurePosixPath(entry["path"]).parts)
                artifact.parent.mkdir(parents=True, exist_ok=True)
                artifact.write_bytes(self.evidence_payload(record, entry["path"]))
            first = record["raw_evidence_manifest"][0]
            packet_root.joinpath(*PurePosixPath(first["path"]).parts).write_bytes(b"y")
            with self.assertRaisesRegex(target.ValidationFailure, "SHA-256 drifted"):
                target.validate_completed_run_record(
                    self.plan,
                    record,
                    evidence_root=packet_root,
                )

    def test_completed_record_rejects_resolved_path_escape(self) -> None:
        record = self.completed_record()
        with tempfile.TemporaryDirectory(prefix="al-bakeoff-link-") as temporary:
            root = Path(temporary)
            packet_root = root / "packet"
            packet_root.mkdir()
            outside = root / "outside"
            outside.mkdir()
            evidence_link = packet_root / "evidence"
            try:
                evidence_link.symlink_to(outside, target_is_directory=True)
            except OSError as error:
                if os.name != "nt":
                    self.skipTest(f"directory symlinks unavailable: {error}")
                result = subprocess.run(
                    ["cmd.exe", "/c", "mklink", "/J", str(evidence_link), str(outside)],
                    check=False,
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL,
                )
                if result.returncode != 0:
                    self.skipTest("directory junctions unavailable")
            for entry in record["raw_evidence_manifest"]:
                artifact = packet_root.joinpath(*PurePosixPath(entry["path"]).parts)
                artifact.parent.mkdir(parents=True, exist_ok=True)
                artifact.write_bytes(self.evidence_payload(record, entry["path"]))
            with self.assertRaisesRegex(target.ValidationFailure, "resolves outside"):
                target.validate_completed_run_record(
                    self.plan,
                    record,
                    evidence_root=packet_root,
                )

    def test_record_pair_requires_equal_common_driver_inputs(self) -> None:
        first = self.completed_record()
        second = self.completed_record()
        second.update(
            {
                "candidate_id": "microsoft_playfab",
                "spike_task_id": "t_27759e01",
                "adapter_fingerprint": "2" * 64,
                "configuration_hash": "3" * 64,
                "region_id": "synthetic-peer-region",
            }
        )
        target.validate_record_pair_equivalence([first, second])
        second["workload_manifest_hash"] = "4" * 64
        with self.assertRaisesRegex(target.ValidationFailure, "workload_manifest_hash"):
            target.validate_record_pair_equivalence([first, second])

    def test_record_pair_requires_each_candidate_once(self) -> None:
        record = self.completed_record()
        with self.assertRaisesRegex(target.ValidationFailure, "duplicate"):
            target.validate_record_pair_equivalence([record, copy.deepcopy(record)])

    def test_completed_record_cli_path_passes(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-bakeoff-record-") as temporary:
            packet_root = Path(temporary)
            record = self.completed_record()
            for entry in record["raw_evidence_manifest"]:
                artifact = packet_root.joinpath(*PurePosixPath(entry["path"]).parts)
                artifact.parent.mkdir(parents=True, exist_ok=True)
                artifact.write_bytes(self.evidence_payload(record, entry["path"]))
            record_path = Path(temporary) / "run-record.json"
            record_path.write_text(
                json.dumps(record, indent=2) + "\n",
                encoding="utf-8",
            )
            original_arguments = sys.argv
            sys.argv = [
                str(THIS_DIR / "validate_mmo_bakeoff_plan.py"),
                str(REPOSITORY_ROOT),
                "--record",
                str(record_path),
            ]
            try:
                with contextlib.redirect_stdout(io.StringIO()) as output:
                    self.assertEqual(target.main(), 0)
                self.assertIn("completed run record(s) valid", output.getvalue())
            finally:
                sys.argv = original_arguments

    def test_completed_record_pair_cli_path_passes(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-bakeoff-pair-") as temporary:
            root = Path(temporary)
            records = [self.completed_record(), self.completed_record()]
            records[1].update(
                {
                    "candidate_id": "microsoft_playfab",
                    "spike_task_id": "t_27759e01",
                    "adapter_fingerprint": "2" * 64,
                    "configuration_hash": "3" * 64,
                    "region_id": "synthetic-peer-region",
                }
            )
            record_paths = []
            for index, record in enumerate(records):
                packet_root = root / f"packet-{index}"
                for entry in record["raw_evidence_manifest"]:
                    artifact = packet_root.joinpath(*PurePosixPath(entry["path"]).parts)
                    artifact.parent.mkdir(parents=True, exist_ok=True)
                    artifact.write_bytes(self.evidence_payload(record, entry["path"]))
                record_path = packet_root / "run-record.json"
                record_path.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
                record_paths.append(record_path)
            original_arguments = sys.argv
            sys.argv = [
                str(THIS_DIR / "validate_mmo_bakeoff_plan.py"),
                str(REPOSITORY_ROOT),
                "--record",
                str(record_paths[0]),
                "--record",
                str(record_paths[1]),
            ]
            try:
                with contextlib.redirect_stdout(io.StringIO()) as output:
                    self.assertEqual(target.main(), 0)
                self.assertIn("common-driver inputs equivalent", output.getvalue())
            finally:
                sys.argv = original_arguments

    def test_run_record_cannot_recommend_a_candidate(self) -> None:
        run_record = copy.deepcopy(self.run_record)
        run_record["selection_recommendation"] = "amazon_gamelift"
        with self.assertRaisesRegex(target.ValidationFailure, "must not recommend"):
            self.validate(run_record=run_record)

    def test_run_record_cannot_drop_a_scenario(self) -> None:
        run_record = copy.deepcopy(self.run_record)
        run_record["scenario_results"].pop()
        with self.assertRaisesRegex(target.ValidationFailure, "retain every scenario"):
            self.validate(run_record=run_record)

    def test_runbook_section_cannot_be_removed(self) -> None:
        runbooks = self.runbooks.replace("### containment", "### removed", 1)
        with self.assertRaisesRegex(target.ValidationFailure, "missing section containment"):
            self.validate(runbooks=runbooks)

    def test_runbook_section_cannot_be_empty(self) -> None:
        before, remainder = self.runbooks.split("### containment", 1)
        _, after = remainder.split("### diagnosis", 1)
        runbooks = before + "### containment\n\n### diagnosis" + after
        with self.assertRaisesRegex(target.ValidationFailure, "empty or placeholder-only"):
            self.validate(runbooks=runbooks)

    def test_runbook_section_cannot_be_padded_placeholder(self) -> None:
        before, remainder = self.runbooks.split("### containment", 1)
        _, after = remainder.split("### diagnosis", 1)
        runbooks = (
            before
            + "### containment\n\n"
            + ("placeholder " * 20)
            + "\n\n### diagnosis"
            + after
        )
        with self.assertRaisesRegex(target.ValidationFailure, "runbook template digest drifted"):
            self.validate(runbooks=runbooks)


if __name__ == "__main__":
    unittest.main()
