#!/usr/bin/env python
"""Adversarial tests for the reversible MMO provider decision package."""

from __future__ import annotations

import copy
import hashlib
import json
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


THIS_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = THIS_DIR.parents[1]
sys.path.insert(0, str(THIS_DIR))

import validate_mmo_provider_decision as target  # noqa: E402


class ProviderDecisionValidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        architecture = REPOSITORY_ROOT / "unity" / "Docs" / "Architecture"
        cls.record = json.loads(
            (architecture / "MMO_Provider_Decision_Record_v1.json").read_text(
                encoding="utf-8"
            )
        )
        cls.log = (architecture / "MMO_Provider_Decision_Log_v1.md").read_text(
            encoding="utf-8"
        )
        cls.contingency = (
            architecture / "MMO_Provider_No_Selection_Contingency_v1.md"
        ).read_text(encoding="utf-8")

    def validate(
        self,
        *,
        record: dict | None = None,
        log: str | None = None,
        contingency: str | None = None,
    ) -> None:
        target.validate_documents(
            copy.deepcopy(self.record if record is None else record),
            self.log if log is None else log,
            self.contingency if contingency is None else contingency,
        )

    def run_cli(self, root: Path, evidence_root: Path) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(THIS_DIR / "validate_mmo_provider_decision.py"),
                str(root),
                "--evidence-root",
                str(evidence_root),
            ],
            check=False,
            capture_output=True,
            text=True,
        )

    def copy_package(self, destination: Path) -> Path:
        architecture = destination / "unity" / "Docs" / "Architecture"
        architecture.parent.mkdir(parents=True)
        shutil.copytree(
            REPOSITORY_ROOT / "unity" / "Docs" / "Architecture",
            architecture,
        )
        return destination

    def copy_evidence(self, destination: Path) -> Path:
        evidence = destination / "evidence"
        shutil.copytree(REPOSITORY_ROOT / ".hermes_artifacts", evidence)
        return evidence

    def rebind_candidate_record(
        self,
        package_root: Path,
        evidence_root: Path,
        candidate_id: str,
        record: dict,
    ) -> None:
        directory = {
            "amazon_gamelift": "gamelift-current",
            "microsoft_playfab": "playfab-current",
        }[candidate_id]
        run_record_path = evidence_root / directory / "run-record.json"
        run_record_path.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
        digest = hashlib.sha256(run_record_path.read_bytes()).hexdigest()

        decision_path = (
            package_root
            / "unity"
            / "Docs"
            / "Architecture"
            / "MMO_Provider_Decision_Record_v1.json"
        )
        decision = json.loads(decision_path.read_text(encoding="utf-8"))
        candidate = next(item for item in decision["candidates"] if item["id"] == candidate_id)
        old_digest = candidate["current_rerun"]["run_record_sha256"]
        candidate["current_rerun"]["run_record_sha256"] = digest
        decision_path.write_text(json.dumps(decision, indent=2) + "\n", encoding="utf-8")

        log_path = (
            package_root
            / "unity"
            / "Docs"
            / "Architecture"
            / "MMO_Provider_Decision_Log_v1.md"
        )
        log = log_path.read_text(encoding="utf-8")
        self.assertIn(old_digest, log)
        log_path.write_text(log.replace(old_digest, digest, 1), encoding="utf-8")

        manifest_path = evidence_root / "comparison" / "manifest.sha256"
        manifest = manifest_path.read_text(encoding="utf-8")
        manifest = re.sub(
            rf"^[0-9a-f]{{64}} (\*\.hermes_artifacts/{re.escape(directory)}/run-record\.json)$",
            rf"{digest} \1",
            manifest,
            flags=re.MULTILINE,
        )
        manifest_path.write_text(manifest, encoding="utf-8")

    def rebind_manifest_member(
        self,
        package_root: Path,
        evidence_root: Path,
        candidate_id: str,
        relative_path: str,
        content: str,
    ) -> None:
        directory = {
            "amazon_gamelift": "gamelift-current",
            "microsoft_playfab": "playfab-current",
        }[candidate_id]
        member_path = evidence_root / directory / relative_path
        member_path.write_text(content, encoding="utf-8")
        record_path = evidence_root / directory / "run-record.json"
        record = json.loads(record_path.read_text(encoding="utf-8"))
        member = next(
            item for item in record["raw_evidence_manifest"] if item["path"] == relative_path
        )
        member["bytes"] = member_path.stat().st_size
        member["sha256"] = hashlib.sha256(member_path.read_bytes()).hexdigest()
        self.rebind_candidate_record(package_root, evidence_root, candidate_id, record)

    def test_canonical_package_passes(self) -> None:
        self.validate()

    def test_cli_preserves_unresolved_evidence_root_for_symlink_validation(self) -> None:
        with mock.patch.object(target, "validate") as validate:
            exit_code = target.main([".", "--evidence-root", "relative-evidence"])
        self.assertEqual(0, exit_code)
        self.assertEqual(Path("relative-evidence"), validate.call_args.args[1])

    def test_both_candidates_are_required(self) -> None:
        record = copy.deepcopy(self.record)
        record["candidates"].pop()
        with self.assertRaisesRegex(target.ValidationFailure, "candidate set"):
            self.validate(record=record)

    def test_candidate_cannot_be_selected_without_owner_approval(self) -> None:
        record = copy.deepcopy(self.record)
        record["recommendation"] = "select_amazon_gamelift"
        record["owner_decision"] = "select_amazon_gamelift"
        record["owner_approval"]["status"] = "not_granted"
        with self.assertRaisesRegex(target.ValidationFailure, "selection without owner approval"):
            self.validate(record=record)

    def test_current_recommendation_remains_no_selection(self) -> None:
        record = copy.deepcopy(self.record)
        record["recommendation"] = "blocked_pending_evidence"
        with self.assertRaisesRegex(target.ValidationFailure, "recommendation"):
            self.validate(record=record)

    def test_all_common_scenarios_must_remain_blocked(self) -> None:
        record = copy.deepcopy(self.record)
        record["candidates"][0]["scenario_summary"]["blocked"] = 15
        record["candidates"][0]["scenario_summary"]["pass"] = 1
        with self.assertRaisesRegex(target.ValidationFailure, "scenario summary"):
            self.validate(record=record)

    def test_vendor_documentation_cannot_be_promoted_to_measurement(self) -> None:
        record = copy.deepcopy(self.record)
        record["candidates"][0]["vendor_documentation"]["claim_class"] = "measured_limit"
        with self.assertRaisesRegex(target.ValidationFailure, "vendor-document claim class"):
            self.validate(record=record)

    def test_unknown_limits_cannot_be_silently_resolved(self) -> None:
        record = copy.deepcopy(self.record)
        record["candidates"][1]["unknown_limits"][0]["status"] = "pass"
        with self.assertRaisesRegex(target.ValidationFailure, "unknown limit"):
            self.validate(record=record)

    def test_same_decision_criteria_are_required_for_both_candidates(self) -> None:
        record = copy.deepcopy(self.record)
        record["criteria"][0]["microsoft_playfab"] = "pass"
        with self.assertRaisesRegex(target.ValidationFailure, "criterion must remain blocked"):
            self.validate(record=record)

    def test_incomparable_pair_cannot_be_reported_equivalent(self) -> None:
        record = copy.deepcopy(self.record)
        record["comparison_equivalence"]["pair_validator_status"] = "pass"
        with self.assertRaisesRegex(target.ValidationFailure, "pair validator"):
            self.validate(record=record)

    def test_contract_compliance_cannot_be_inferred_from_blocked_runs(self) -> None:
        record = copy.deepcopy(self.record)
        record["candidates"][0]["contract_assessment"] = "pass"
        with self.assertRaisesRegex(target.ValidationFailure, "contract assessment"):
            self.validate(record=record)

    def test_rollback_must_remain_viable_for_both_candidates(self) -> None:
        record = copy.deepcopy(self.record)
        record["candidates"][1]["rollback"]["neutral_hash_restored"] = False
        with self.assertRaisesRegex(target.ValidationFailure, "rollback"):
            self.validate(record=record)

    def test_all_owner_reserved_boundaries_remain_unresolved(self) -> None:
        record = copy.deepcopy(self.record)
        record["owner_reserved_boundaries"][0]["status"] = "approved"
        with self.assertRaisesRegex(target.ValidationFailure, "owner-reserved boundary"):
            self.validate(record=record)

    def test_required_runbooks_have_final_sections_and_no_placeholders(self) -> None:
        contingency = self.contingency.replace("## RB-OUTAGE-01", "## REMOVED", 1)
        with self.assertRaisesRegex(target.ValidationFailure, "missing runbook"):
            self.validate(contingency=contingency)
        with self.assertRaisesRegex(target.ValidationFailure, "placeholder"):
            self.validate(contingency=self.contingency + "\n[command]\n")

    def test_decision_log_requires_claim_classes_and_owner_gate(self) -> None:
        log = self.log.replace("Measured result", "Removed result class", 1)
        with self.assertRaisesRegex(target.ValidationFailure, "decision log missing"):
            self.validate(log=log)
        log = self.log.replace("Owner approval is required", "Owner review is optional", 1)
        with self.assertRaisesRegex(target.ValidationFailure, "decision log missing"):
            self.validate(log=log)

    def test_contradictory_selection_prose_is_rejected(self) -> None:
        log = self.log + "\nDecision: Amazon GameLift is selected and approved for production.\n"
        with self.assertRaisesRegex(target.ValidationFailure, "contradictory"):
            self.validate(log=log)

    def test_html_comments_cannot_hide_decision_or_runbook_content(self) -> None:
        with self.assertRaisesRegex(target.ValidationFailure, "HTML comment"):
            self.validate(log=f"<!--\n{self.log}\n-->\n")
        with self.assertRaisesRegex(target.ValidationFailure, "HTML comment"):
            self.validate(contingency=f"<!--\n{self.contingency}\n-->\n")

    def test_fenced_blocks_cannot_hide_decision_or_runbook_content(self) -> None:
        with self.assertRaisesRegex(target.ValidationFailure, "fenced"):
            self.validate(log=f"```markdown\n{self.log}\n```\n")
        with self.assertRaisesRegex(target.ValidationFailure, "fenced"):
            self.validate(contingency=f"~~~markdown\n{self.contingency}\n~~~\n")

    def test_decision_log_must_bind_the_recorded_artifact_digest(self) -> None:
        digest = self.record["raw_evidence"]["artifact_sha256"]
        log = self.log.replace(digest, "0" * 64, 1)
        with self.assertRaisesRegex(target.ValidationFailure, "traceability"):
            self.validate(log=log)

    def test_effectively_empty_runbook_is_rejected(self) -> None:
        empty_outage = """## RB-OUTAGE-01 — Provider or control-plane outage

### Trigger

### Current-state containment

### Diagnosis and commands

### Recovery decision

### Rollback and evidence

"""
        contingency = re.sub(
            r"## RB-OUTAGE-01\b.*?(?=## RB-QUOTA-01\b)",
            empty_outage,
            self.contingency,
            flags=re.DOTALL,
        )
        with self.assertRaisesRegex(target.ValidationFailure, "empty runbook"):
            self.validate(contingency=contingency)

    def test_every_comparison_row_must_be_symmetrically_blocked(self) -> None:
        log = self.log.replace(
            "| Approved-gate cost evidence | blocked | blocked |",
            "| Approved-gate cost evidence | blocked | pass |",
            1,
        )
        with self.assertRaisesRegex(target.ValidationFailure, "comparison row"):
            self.validate(log=log)

    def test_omitted_observed_drift_is_rejected_against_candidate_records(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_root = Path(temporary)
            package_root = self.copy_package(temporary_root / "package")
            decision_path = (
                package_root
                / "unity"
                / "Docs"
                / "Architecture"
                / "MMO_Provider_Decision_Record_v1.json"
            )
            decision = json.loads(decision_path.read_text(encoding="utf-8"))
            decision["comparison_equivalence"]["other_observed_input_drift"].remove(
                "request_envelopes"
            )
            decision_path.write_text(json.dumps(decision, indent=2) + "\n", encoding="utf-8")
            result = self.run_cli(package_root, REPOSITORY_ROOT / ".hermes_artifacts")

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("observed input drift", result.stderr)

    def test_fake_evidence_archive_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            evidence_root = self.copy_evidence(Path(temporary))
            archive = evidence_root / "mmo-provider-decision-evidence-956f452a.zip"
            archive.write_bytes(archive.read_bytes() + b"fake")
            result = self.run_cli(REPOSITORY_ROOT, evidence_root)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("artifact SHA-256", result.stderr)

    def test_fake_run_record_hash_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            evidence_root = self.copy_evidence(Path(temporary))
            record_path = evidence_root / "gamelift-current" / "run-record.json"
            record_path.write_bytes(record_path.read_bytes() + b" ")
            result = self.run_cli(REPOSITORY_ROOT, evidence_root)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("run record SHA-256", result.stderr)

    def test_fake_manifest_member_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            evidence_root = self.copy_evidence(Path(temporary))
            commands = evidence_root / "playfab-current" / "commands.txt"
            commands.write_text("fabricated command evidence\n", encoding="utf-8")
            result = self.run_cli(REPOSITORY_ROOT, evidence_root)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("manifest", result.stderr)

    def test_rehashed_extracted_packet_cannot_diverge_from_retained_archive(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_root = Path(temporary)
            package_root = self.copy_package(temporary_root / "package")
            evidence_root = self.copy_evidence(temporary_root)
            self.rebind_manifest_member(
                package_root,
                evidence_root,
                "amazon_gamelift",
                "limitations.md",
                "Changed but internally rehashed limitation evidence.\n",
            )
            result = self.run_cli(package_root, evidence_root)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("artifact member", result.stderr)

    def test_symlinked_evidence_member_is_rejected_before_hashing(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_root = Path(temporary)
            evidence_root = self.copy_evidence(temporary_root)
            member = evidence_root / "gamelift-current" / "limitations.md"
            target_file = temporary_root / "outside-limitations.md"
            target_file.write_bytes(member.read_bytes())
            member.unlink()
            try:
                member.symlink_to(target_file)
            except OSError as error:
                self.skipTest(f"symlink creation unavailable: {error}")
            result = self.run_cli(REPOSITORY_ROOT, evidence_root)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("symlink", result.stderr)

    def test_symlinked_evidence_root_and_archive_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_root = Path(temporary)
            real_root = self.copy_evidence(temporary_root / "real")
            linked_root = temporary_root / "linked-evidence"
            try:
                linked_root.symlink_to(real_root, target_is_directory=True)
            except OSError as error:
                self.skipTest(f"symlink creation unavailable: {error}")
            result = self.run_cli(REPOSITORY_ROOT, linked_root)
            self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("symlink", result.stderr)

            archive = real_root / self.record["raw_evidence"]["artifact_filename"]
            real_archive = real_root / "retained-real.zip"
            archive.replace(real_archive)
            archive.symlink_to(real_archive)
            result = self.run_cli(REPOSITORY_ROOT, real_root)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("symlink", result.stderr)

    def test_incomplete_scenario_inventory_is_rejected_even_when_rehashed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_root = Path(temporary)
            package_root = self.copy_package(temporary_root / "package")
            evidence_root = self.copy_evidence(temporary_root)
            record_path = evidence_root / "gamelift-current" / "run-record.json"
            record = json.loads(record_path.read_text(encoding="utf-8"))
            record["scenario_results"].pop()
            self.rebind_candidate_record(package_root, evidence_root, "amazon_gamelift", record)
            result = self.run_cli(package_root, evidence_root)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("scenario inventory", result.stderr)

    def test_false_rollback_hashes_are_rejected_even_when_rehashed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_root = Path(temporary)
            package_root = self.copy_package(temporary_root / "package")
            evidence_root = self.copy_evidence(temporary_root)
            rollback_path = evidence_root / "playfab-current" / "rollback.json"
            rollback = json.loads(rollback_path.read_text(encoding="utf-8"))
            rollback["neutral_configuration_hash_after"] = "0" * 64
            self.rebind_manifest_member(
                package_root,
                evidence_root,
                "microsoft_playfab",
                "rollback.json",
                json.dumps(rollback, indent=2) + "\n",
            )
            result = self.run_cli(package_root, evidence_root)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("rollback hashes", result.stderr)

    def test_fake_test_evidence_is_rejected_even_when_rehashed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_root = Path(temporary)
            package_root = self.copy_package(temporary_root / "package")
            evidence_root = self.copy_evidence(temporary_root)
            self.rebind_manifest_member(
                package_root,
                evidence_root,
                "amazon_gamelift",
                "logs/core-tests.log",
                "fabricated passing test evidence\n",
            )
            result = self.run_cli(package_root, evidence_root)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("test evidence", result.stderr)


if __name__ == "__main__":
    unittest.main()
