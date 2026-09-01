#!/usr/bin/env python
"""Tests for the reproducible PlayFab spike packet generator."""

from __future__ import annotations

import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import run_playfab_spike as target
import validate_mmo_bakeoff_plan


ROOT = Path(__file__).resolve().parents[2]
PLAN = json.loads(
    (ROOT / "unity/Docs/Architecture/MMO_Provider_Bakeoff_Scenarios_v1.json").read_text(
        encoding="utf-8"
    )
)


class PlayFabSpikeTests(unittest.TestCase):
    def test_source_manifest_rejects_uncommitted_driver_sources(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-playfab-source-") as temporary:
            root = Path(temporary)
            source = root / "driver.py"
            source.write_text("version = 1\n", encoding="utf-8")
            git = target.git_executable()
            for command in (
                [git, "init"],
                [git, "config", "user.email", "spike@example.invalid"],
                [git, "config", "user.name", "Spike Test"],
                [git, "add", "driver.py"],
                [git, "commit", "-m", "fixture"],
            ):
                subprocess.run(command, cwd=root, check=True, capture_output=True, text=True)
            manifest = target.source_manifest(root, [source], enforce_committed=True)
            self.assertEqual(manifest["source_state"], "committed")

            source.write_text("version = 2\n", encoding="utf-8")
            with self.assertRaisesRegex(RuntimeError, "uncommitted"):
                target.source_manifest(root, [source], enforce_committed=True)

    def test_missing_sandbox_configuration_yields_complete_blocked_packet(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-playfab-spike-") as temporary:
            packet = Path(temporary) / "packet"
            with mock.patch.dict(
                os.environ,
                {name: "" for name in target.REQUIRED_ENV},
                clear=False,
            ):
                record_path = target.run_blocked_preflight(
                    ROOT,
                    packet,
                    now=lambda: "2026-08-31T00:00:00Z",
                    public_status={"indicator": "none", "description": "All Systems Operational"},
                    enforce_committed_sources=False,
                )
            record = json.loads(record_path.read_text(encoding="utf-8"))
            self.assertEqual(record["candidate_id"], "microsoft_playfab")
            self.assertEqual(record["run_status"], "blocked")
            self.assertEqual(len(record["scenario_results"]), 16)
            self.assertEqual({item["status"] for item in record["scenario_results"]}, {"blocked"})
            self.assertTrue(
                all(item["stable_result_counts"]["attempted"] == 0 for item in record["scenario_results"])
            )
            self.assertTrue(
                all(item["operation_ids"][0].startswith("preflight-") for item in record["scenario_results"])
            )
            self.assertTrue(
                all(item["correlation_ids"][0].startswith("blocked-correlation-") for item in record["scenario_results"])
            )
            self.assertEqual(
                len({item["blockers"][0] for item in record["scenario_results"]}),
                16,
            )
            self.assertNotIn("not_run", record_path.read_text(encoding="utf-8"))
            self.assertIsNone(record["selection_recommendation"])
            source_manifest = json.loads(
                (packet / "provider-exports/source-manifest.json").read_text(encoding="utf-8")
            )
            source_paths = {item["path"] for item in source_manifest["files"]}
            self.assertTrue(
                {
                    "tools/architecture/validate_mmo_bakeoff_plan.py",
                    "unity/Docs/Architecture/MMO_Provider_Bakeoff_Evidence_Plan_v1.md",
                    "unity/Docs/Architecture/MMO_Provider_Bakeoff_Scenarios_v1.json",
                    "unity/Docs/Architecture/Templates/MMO_Provider_Bakeoff_Runbooks_v1.md",
                    "unity/Docs/Architecture/Templates/MMO_Provider_Spike_Run_Record_v1.json",
                }.issubset(source_paths)
            )
            validate_mmo_bakeoff_plan.validate_completed_run_record(
                PLAN, record, evidence_root=packet
            )

    def test_packet_never_serializes_secret_values_and_restores_neutral_hashes(self) -> None:
        secret = "super-secret-playfab-value"
        with tempfile.TemporaryDirectory(prefix="al-playfab-secret-") as temporary:
            packet = Path(temporary) / "packet"
            with mock.patch.dict(
                os.environ,
                {
                    **{name: "" for name in target.REQUIRED_ENV},
                    "PLAYFAB_TITLE_ID": "ABCDE",
                    "PLAYFAB_SECRET_KEY": secret,
                },
                clear=False,
            ):
                record_path = target.run_blocked_preflight(
                    ROOT,
                    packet,
                    now=lambda: "2026-08-31T00:00:00Z",
                    public_status={"indicator": "none", "description": "All Systems Operational"},
                    enforce_committed_sources=False,
                )
            serialized = "\n".join(
                path.read_text(encoding="utf-8", errors="replace")
                for path in packet.rglob("*")
                if path.is_file()
            )
            self.assertNotIn(secret, serialized)
            record = json.loads(record_path.read_text(encoding="utf-8"))
            preflight = json.loads((packet / "raw/preflight.json").read_text(encoding="utf-8"))
            self.assertTrue(preflight["credential_values_loaded_from_environment"])
            self.assertFalse(preflight["credential_values_emitted"])
            self.assertFalse(preflight["credential_values_sent_to_provider"])
            rollback = record["rollback_result"]
            self.assertEqual(rollback["status"], "blocked")
            rollback_detail = json.loads((packet / "rollback.json").read_text(encoding="utf-8"))
            self.assertTrue(rollback_detail["candidate_adapter_enabled_before"])
            self.assertFalse(rollback_detail["candidate_adapter_enabled_after"])
            self.assertEqual(
                rollback["neutral_configuration_hash_before"],
                rollback["neutral_configuration_hash_after"],
            )
            self.assertEqual(
                rollback["regional_state_hash_before"],
                rollback["regional_state_hash_after"],
            )
            self.assertEqual(rollback["core_tests"], "pass")
            self.assertIn(
                "disabled_adapter_rejects_new_work_but_allows_existing_cleanup",
                (packet / "logs/neutral-core-tests.log").read_text(encoding="utf-8"),
            )

    def test_core_test_children_receive_no_playfab_environment(self) -> None:
        completed = subprocess.CompletedProcess(args=["cargo"], returncode=0, stdout="ok", stderr="")
        with tempfile.TemporaryDirectory(prefix="al-playfab-child-env-") as temporary:
            with mock.patch.dict(
                os.environ,
                {
                    "PLAYFAB_TITLE_ID": "ABCDE",
                    "PLAYFAB_SECRET_KEY": "must-not-reach-child",
                    "PLAYFAB_SPIKE_LIVE_AUTHORIZED": "1",
                    "SAFE_CONTROL": "retained",
                },
                clear=False,
            ), mock.patch.object(target, "cargo_command", return_value="cargo"), mock.patch.object(
                target.subprocess, "run", return_value=completed
            ) as run:
                self.assertTrue(
                    target.run_core_tests(ROOT, Path(temporary) / "neutral-core-tests.log")
                )

        self.assertEqual(run.call_count, 2)
        for call in run.call_args_list:
            child_environment = call.kwargs["env"]
            self.assertEqual(child_environment["SAFE_CONTROL"], "retained")
            self.assertFalse(any(name.startswith("PLAYFAB_") for name in child_environment))

    def test_workload_manifest_retains_exact_common_equal_input_shape(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-playfab-workload-") as temporary:
            packet = Path(temporary) / "packet"
            with mock.patch.dict(
                os.environ,
                {name: "" for name in target.REQUIRED_ENV},
                clear=False,
            ):
                target.run_blocked_preflight(
                    ROOT,
                    packet,
                    now=lambda: "2026-08-31T00:00:00Z",
                    public_status={"indicator": "none", "description": "All Systems Operational"},
                    enforce_committed_sources=False,
                )
            workload = json.loads(
                (packet / "workload-manifest.json").read_text(encoding="utf-8")
            )
            self.assertEqual(
                set(workload), validate_mmo_bakeoff_plan.COMMON_DRIVER_EQUAL_INPUTS
            )
            self.assertEqual(workload["topology"]["realms"], 4)
            self.assertEqual(
                workload["topology"]["logical_regions"],
                ["home_region", "forbidden_region"],
            )


if __name__ == "__main__":
    unittest.main()
