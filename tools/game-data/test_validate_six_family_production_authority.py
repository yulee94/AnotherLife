#!/usr/bin/env python3
"""Regression tests for the six-family production authority gate."""

from __future__ import annotations

import sys
import json
import tempfile
import unittest
from pathlib import Path

THIS_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = THIS_DIR.parents[1]
sys.path.insert(0, str(THIS_DIR))

import validate_six_family_production_authority as target  # noqa: E402


class SixFamilyProductionAuthorityTests(unittest.TestCase):
    def test_current_ledger_is_valid_but_emits_no_activation(self) -> None:
        result = target.validate_ledger_file(
            REPOSITORY_ROOT / target.DEFAULT_LEDGER_PATH,
            REPOSITORY_ROOT,
        )

        self.assertEqual(target.FAMILY_ORDER, result.family_order)
        self.assertFalse(result.production_eligible)
        self.assertEqual("AL-GDA-BLOCKED", result.diagnostic_code)
        self.assertEqual([], result.output_paths)
        self.assertEqual([], result.activation_targets)
        self.assertEqual(26, len(result.blocking_ids))
        self.assertEqual(22, result.checked_source_count)

    def test_current_ledger_maps_all_six_families_and_typed_blockers(self) -> None:
        ledger = self.load_ledger()

        self.assertEqual(target.FAMILY_ORDER, [row["family"] for row in ledger["families"]])
        blocker_types = {
            blocker["type"]
            for family in ledger["families"]
            for blocker in family["blockers"]
        }
        blocker_types.add(ledger["approval"]["blocker"]["type"])
        self.assertEqual(
            {"identity", "behavior", "balance", "localization", "asset", "approval"},
            blocker_types,
        )
        for family in ledger["families"]:
            self.assertGreater(len(family["sources"]), 0, family["family"])
            for source in family["sources"]:
                self.assertRegex(source["sourceRevision"], r"^[0-9a-f]{40}$")
                self.assertRegex(source["rawSha256"], r"^[0-9a-f]{64}$")

    def test_schema_v1_rejects_forged_approval(self) -> None:
        ledger = self.load_ledger()
        for family in ledger["families"]:
            family["blockers"] = []
            family["resolvedBlockerIds"] = list(
                target.TRACKED_FAMILY_BLOCKERS[family["family"]]
            )
            family["disposition"] = "source_complete"
        ledger["approval"] = {
            "state": "approved",
            "reviewedSourceSetSha256": ledger["sourceSetSha256"],
            "blocker": None,
        }
        ledger["productionEligible"] = True
        ledger["generationGate"] = {
            "status": "eligible",
            "outputPaths": [],
            "activationTargets": [],
        }

        with self.assertRaisesRegex(target.ValidationError, "requires pending approval"):
            target.validate_ledger(ledger, REPOSITORY_ROOT)

    def test_schema_v1_rejects_forged_resolution(self) -> None:
        ledger = self.load_ledger()
        family = ledger["families"][1]
        family["blockers"] = []
        family["resolvedBlockerIds"] = list(
            target.TRACKED_FAMILY_BLOCKERS[family["family"]]
        )
        family["disposition"] = "source_complete"
        ledger["sourceSetSha256"] = target.source_set_sha256(ledger)

        with self.assertRaisesRegex(target.ValidationError, "does not accept resolved blockers"):
            target.validate_ledger(ledger, REPOSITORY_ROOT)

    def test_supported_but_wrong_blocker_classification_fails_closed(self) -> None:
        ledger = self.load_ledger()
        ledger["families"][1]["blockers"][0]["type"] = "balance"
        ledger["sourceSetSha256"] = target.source_set_sha256(ledger)

        with self.assertRaisesRegex(target.ValidationError, "classification drifted"):
            target.validate_ledger(ledger, REPOSITORY_ROOT)

    def test_blocker_content_is_bound_to_source_set_fingerprint(self) -> None:
        ledger = self.load_ledger()
        ledger["families"][1]["blockers"][0]["recommendation"] += " Changed."

        with self.assertRaisesRegex(target.ValidationError, "source-set fingerprint"):
            target.validate_ledger(ledger, REPOSITORY_ROOT)

    def test_audited_revision_metadata_drift_fails_closed(self) -> None:
        ledger = self.load_ledger()
        ledger["auditedRevision"] = "0" * 40

        with self.assertRaisesRegex(target.ValidationError, "auditedRevision drifted"):
            target.validate_ledger(ledger, REPOSITORY_ROOT)

    def test_required_source_inventory_cannot_be_omitted(self) -> None:
        ledger = self.load_ledger()
        ledger["families"][0]["sources"].pop()
        ledger["sourceSetSha256"] = target.source_set_sha256(ledger)

        with self.assertRaisesRegex(target.ValidationError, "source role inventory drifted"):
            target.validate_ledger(ledger, REPOSITORY_ROOT)

    def test_source_hash_drift_fails_closed(self) -> None:
        ledger = self.load_ledger()
        ledger["families"][0]["sources"][1]["rawSha256"] = "0" * 64
        ledger["sourceSetSha256"] = target.source_set_sha256(ledger)

        with self.assertRaisesRegex(target.ValidationError, "source hash"):
            target.validate_ledger(ledger, REPOSITORY_ROOT)

    def test_stale_source_revision_fails_closed(self) -> None:
        ledger = self.load_ledger()
        ledger["families"][0]["sources"][0]["sourceRevision"] = "0" * 40
        ledger["sourceSetSha256"] = target.source_set_sha256(ledger)

        with self.assertRaisesRegex(target.ValidationError, "sourceRevision"):
            target.validate_ledger(ledger, REPOSITORY_ROOT)

    def test_unknown_blocker_type_fails_closed(self) -> None:
        ledger = self.load_ledger()
        ledger["families"][1]["blockers"][0]["type"] = "miscellaneous"
        ledger["sourceSetSha256"] = target.source_set_sha256(ledger)

        with self.assertRaisesRegex(target.ValidationError, "blocker type"):
            target.validate_ledger(ledger, REPOSITORY_ROOT)

    def test_blocked_ledger_rejects_output_or_activation_targets(self) -> None:
        for key in ("outputPaths", "activationTargets"):
            with self.subTest(key=key):
                ledger = self.load_ledger()
                ledger["generationGate"][key] = ["forbidden"]
                with self.assertRaisesRegex(target.ValidationError, "zero writes and activations"):
                    target.validate_ledger(ledger, REPOSITORY_ROOT)

    def test_global_approval_cannot_bypass_family_blockers(self) -> None:
        ledger = self.load_ledger()
        ledger["approval"] = {
            "state": "approved",
            "reviewedSourceSetSha256": ledger["sourceSetSha256"],
            "blocker": None,
        }

        with self.assertRaisesRegex(target.ValidationError, "requires pending approval"):
            target.validate_ledger(ledger, REPOSITORY_ROOT)

    def test_ledger_file_rejects_duplicate_json_properties(self) -> None:
        ledger_path = REPOSITORY_ROOT / target.DEFAULT_LEDGER_PATH
        raw = ledger_path.read_text(encoding="utf-8")
        duplicate = raw.replace(
            '"schemaVersion": 1,',
            '"schemaVersion": 1,\n  "schemaVersion": 1,',
            1,
        )
        with tempfile.TemporaryDirectory(prefix="anotherlife-authority-") as directory:
            path = Path(directory) / "duplicate.json"
            path.write_text(duplicate, encoding="utf-8")
            with self.assertRaisesRegex(target.ValidationError, "duplicate property"):
                target.validate_ledger_file(path, REPOSITORY_ROOT)

    def test_required_ci_runs_authority_tests_and_fail_closed_gate(self) -> None:
        workflow = (
            REPOSITORY_ROOT / ".github/workflows/quality-gates.yml"
        ).read_text(encoding="utf-8")

        self.assertIn(
            "python tools/game-data/test_validate_six_family_production_authority.py",
            workflow,
        )
        self.assertIn(
            "python tools/game-data/validate_six_family_production_authority.py",
            workflow,
        )
        self.assertIn("--require-production-eligible", workflow)
        self.assertIn("$LASTEXITCODE -ne 2", workflow)
        self.assertIn("$global:LASTEXITCODE = 0", workflow)

    @staticmethod
    def load_ledger() -> dict:
        path = REPOSITORY_ROOT / target.DEFAULT_LEDGER_PATH
        return json.loads(path.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
