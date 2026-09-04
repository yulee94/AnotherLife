from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


THIS_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = THIS_DIR.parents[1]
MODULE_PATH = THIS_DIR / "run_gamelift_spike.py"
SPEC = importlib.util.spec_from_file_location("run_gamelift_spike", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
TARGET = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(TARGET)

VALIDATOR_SPEC = importlib.util.spec_from_file_location(
    "validate_mmo_bakeoff_plan", THIS_DIR / "validate_mmo_bakeoff_plan.py"
)
assert VALIDATOR_SPEC is not None and VALIDATOR_SPEC.loader is not None
VALIDATOR = importlib.util.module_from_spec(VALIDATOR_SPEC)
VALIDATOR_SPEC.loader.exec_module(VALIDATOR)


class GameLiftSpikePacketTests(unittest.TestCase):
    def test_local_contract_test_count_is_parsed_from_cargo_output(self) -> None:
        self.assertEqual(
            TARGET.passed_test_count("test result: ok. 8 passed; 0 failed; 0 ignored"),
            8,
        )

    def test_blocker_matches_credential_preflight_state(self) -> None:
        blocker = TARGET.preflight_blocker(
            {
                "credential_resolved": True,
                "sts_attempted": True,
                "sts_authenticated": False,
            }
        )

        self.assertIn("STS authentication failed", blocker)
        self.assertNotIn("No AWS credential resolved", blocker)

    def test_command_evidence_uses_reproducible_relative_paths(self) -> None:
        command = TARGET.sanitized_command(
            [
                "C:/Users/MY/.cargo/bin/cargo.exe",
                "test",
                "--manifest-path",
                str(REPOSITORY_ROOT / "server" / "Cargo.toml"),
            ],
            REPOSITORY_ROOT,
        )

        self.assertEqual(command, "cargo test --manifest-path server/Cargo.toml")
        self.assertNotIn("Users/MY", command)

    def test_packet_driver_must_be_the_commit_containing_sources(self) -> None:
        with self.assertRaisesRegex(ValueError, "committed source state"):
            TARGET.require_committed_source_state(
                REPOSITORY_ROOT,
                "0" * 40,
            )

    def test_blocked_packet_resolves_every_scenario_and_validates(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-gamelift-packet-") as temporary:
            packet = Path(temporary)
            record_path = TARGET.write_packet(
                repository_root=REPOSITORY_ROOT,
                packet_root=packet,
                run_id="gamelift-no-credentials-test",
                started_utc="2026-08-31T00:00:00Z",
                ended_utc="2026-08-31T00:01:00Z",
                driver_commit="a" * 40,
                preflight={
                    "credential_resolved": False,
                    "configured_region": None,
                    "sdk": "boto3-test",
                    "service_regions": ["ap-northeast-2", "us-east-1"],
                    "sts_attempted": False,
                    "gamelift_inventory_attempted": False,
                },
                local_contract_log="7 passed; 0 failed\n",
                core_test_log="core tests passed\n",
                command_log=["cargo test local", "cargo test core"],
                automated_duration_seconds=60,
            )
            record = json.loads(record_path.read_text(encoding="utf-8"))
            plan = json.loads(
                (
                    REPOSITORY_ROOT
                    / "unity"
                    / "Docs"
                    / "Architecture"
                    / "MMO_Provider_Bakeoff_Scenarios_v1.json"
                ).read_text(encoding="utf-8")
            )

            self.assertEqual(record["run_status"], "blocked")
            self.assertIsNone(record["selection_recommendation"])
            self.assertEqual(len(record["scenario_results"]), 16)
            self.assertTrue(
                all(result["status"] == "blocked" for result in record["scenario_results"])
            )
            self.assertTrue(all(result["blockers"] for result in record["scenario_results"]))
            self.assertTrue(
                all(
                    result["rollback_status"] == "blocked"
                    for result in record["scenario_results"]
                )
            )
            self.assertEqual(record["rollback_result"]["status"], "blocked")
            self.assertEqual(record["driver_source_state"], "source_manifest_fingerprinted")
            source_manifest = json.loads(
                (packet / "raw" / "server-artifact-manifest.json").read_text(
                    encoding="utf-8"
                )
            )
            self.assertIn("tools/architecture/run_gamelift_spike.py", source_manifest)
            self.assertIn("server/al_provider_adapter_gamelift_spike/Cargo.toml", source_manifest)
            VALIDATOR.validate_completed_run_record(
                plan, record, evidence_root=packet
            )

    def test_packet_contains_no_credential_material_fields(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-gamelift-secret-") as temporary:
            record_path = TARGET.write_packet(
                repository_root=REPOSITORY_ROOT,
                packet_root=Path(temporary),
                run_id="gamelift-secret-test",
                started_utc="2026-08-31T00:00:00Z",
                ended_utc="2026-08-31T00:01:00Z",
                driver_commit="b" * 40,
                preflight={
                    "credential_resolved": False,
                    "configured_region": None,
                    "sdk": "boto3-test",
                    "service_regions": [],
                    "sts_attempted": False,
                    "gamelift_inventory_attempted": False,
                },
                local_contract_log=f"{REPOSITORY_ROOT} 7 passed; 0 failed\n",
                core_test_log=f"{Path.home()} core tests passed\n",
                command_log=["cargo test"],
                automated_duration_seconds=60,
            )
            packet_text = "\n".join(
                path.read_text(encoding="utf-8", errors="replace")
                for path in record_path.parent.rglob("*")
                if path.is_file()
            ).lower()
            self.assertNotIn("aws_access_key_id", packet_text)
            self.assertNotIn("aws_secret_access_key", packet_text)
            self.assertNotIn("aws_session_token", packet_text)
            self.assertNotIn(str(REPOSITORY_ROOT).lower(), packet_text)
            self.assertNotIn(str(Path.home()).lower(), packet_text)

    def test_packet_rejects_credential_field_content(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-gamelift-leak-") as temporary:
            with self.assertRaisesRegex(ValueError, "secret scan"):
                TARGET.write_packet(
                    repository_root=REPOSITORY_ROOT,
                    packet_root=Path(temporary),
                    run_id="gamelift-leak-test",
                    started_utc="2026-08-31T00:00:00Z",
                    ended_utc="2026-08-31T00:01:00Z",
                    driver_commit="c" * 40,
                    preflight={"credential_resolved": False},
                    local_contract_log="7 passed; 0 failed AKIA1234567890ABCDEF\n",
                    core_test_log="core tests passed\n",
                    command_log=["cargo test"],
                    automated_duration_seconds=60,
                )

    def test_packet_rejects_arbitrary_absolute_paths(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-gamelift-path-") as temporary:
            with self.assertRaisesRegex(ValueError, "secret scan"):
                TARGET.write_packet(
                    repository_root=REPOSITORY_ROOT,
                    packet_root=Path(temporary),
                    run_id="gamelift-path-test",
                    started_utc="2026-08-31T00:00:00Z",
                    ended_utc="2026-08-31T00:01:00Z",
                    driver_commit="d" * 40,
                    preflight={"credential_resolved": False},
                    local_contract_log="7 passed; 0 failed\n",
                    core_test_log="built by /opt/private/toolchain/cargo\n",
                    command_log=["cargo test"],
                    automated_duration_seconds=60,
                )

    def test_packet_rejects_delimiter_adjacent_paths_and_pii(self) -> None:
        prohibited_values = (
            "/secret",
            "path=/etc/passwd",
            "(C:/Users/Other/private.txt)",
            "user@example.com",
            "arn:aws:iam::123456789012:user/private",
        )
        for index, prohibited in enumerate(prohibited_values):
            with self.subTest(prohibited=prohibited):
                with tempfile.TemporaryDirectory(
                    prefix=f"al-gamelift-sensitive-{index}-"
                ) as temporary:
                    with self.assertRaisesRegex(ValueError, "secret scan"):
                        TARGET.write_packet(
                            repository_root=REPOSITORY_ROOT,
                            packet_root=Path(temporary) / "packet",
                            run_id=prohibited,
                            started_utc="2026-08-31T00:00:00Z",
                            ended_utc="2026-08-31T00:01:00Z",
                            driver_commit="f" * 40,
                            preflight={"credential_resolved": False},
                            local_contract_log="7 passed; 0 failed\n",
                            core_test_log="core tests passed\n",
                            command_log=["cargo test"],
                            automated_duration_seconds=60,
                        )

    def test_rejected_packet_removes_prohibited_content(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-gamelift-cleanup-") as temporary:
            packet = Path(temporary) / "packet"
            with self.assertRaisesRegex(ValueError, "secret scan"):
                TARGET.write_packet(
                    repository_root=REPOSITORY_ROOT,
                    packet_root=packet,
                    run_id="gamelift-cleanup-test",
                    started_utc="2026-08-31T00:00:00Z",
                    ended_utc="2026-08-31T00:01:00Z",
                    driver_commit="0" * 40,
                    preflight={"credential_resolved": False},
                    local_contract_log="7 passed; 0 failed aws_access_key_id=\n",
                    core_test_log="core tests passed\n",
                    command_log=["cargo test"],
                    automated_duration_seconds=60,
                )

            self.assertFalse(packet.exists())

    def test_final_run_record_is_included_in_packet_scan(self) -> None:
        with tempfile.TemporaryDirectory(prefix="al-gamelift-final-scan-") as temporary:
            with self.assertRaisesRegex(ValueError, "secret scan"):
                TARGET.write_packet(
                    repository_root=REPOSITORY_ROOT,
                    packet_root=Path(temporary),
                    run_id="C:\\Program Files\\leak",
                    started_utc="2026-08-31T00:00:00Z",
                    ended_utc="2026-08-31T00:01:00Z",
                    driver_commit="e" * 40,
                    preflight={"credential_resolved": False},
                    local_contract_log="7 passed; 0 failed\n",
                    core_test_log="core tests passed\n",
                    command_log=["cargo test"],
                    automated_duration_seconds=60,
                )


if __name__ == "__main__":
    unittest.main()
