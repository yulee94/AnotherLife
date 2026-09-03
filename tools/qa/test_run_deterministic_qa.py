#!/usr/bin/env python3
"""Contract tests for the integrated deterministic QA entry point."""

from __future__ import annotations

import copy
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("run_deterministic_qa.py")
POLICY = Path(__file__).with_name("deterministic_qa_policy.json")
REPO_ROOT = Path(__file__).resolve().parents[2]
EVIDENCE_SCHEMA = REPO_ROOT / "unity/SharedContracts/integrated-qa-evidence.schema.json"


def load_module():
    spec = importlib.util.spec_from_file_location("run_deterministic_qa", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class DeterministicQaTests(unittest.TestCase):
    def test_shared_evidence_schema_exposes_fail_closed_consumer_contract(self):
        schema = json.loads(EVIDENCE_SCHEMA.read_text(encoding="utf-8"))

        self.assertEqual(schema["$schema"], "https://json-schema.org/draft/2020-12/schema")
        self.assertTrue(schema["$id"].endswith("integrated-qa-evidence.schema.json"))
        self.assertFalse(schema["additionalProperties"])
        self.assertEqual(
            set(schema["properties"]["status"]["enum"]),
            {"passed", "stop_ship"},
        )
        self.assertIn("reportSha256", schema["required"])
        self.assertIn("provenance", schema["required"])
        self.assertIn("contracts", schema["required"])

    def test_policy_covers_every_required_contract_with_versioned_fixtures(self):
        module = load_module()
        policy = module.load_policy(POLICY)

        self.assertEqual(policy["schemaVersion"], 1)
        self.assertEqual(policy["fixtureVersion"], "qa-fixtures-v1")
        self.assertEqual(
            set(module.REQUIRED_CONTRACTS),
            {contract["id"] for contract in policy["contracts"]},
        )
        self.assertEqual(
            list(module.REQUIRED_CONTRACTS),
            policy["profiles"]["full"],
        )
        for contract in policy["contracts"]:
            self.assertRegex(contract["failureCode"], r"^QA_[A-Z0-9_]+$")
            self.assertGreaterEqual(contract["repeat"], 1)

    def test_run_identity_is_stable_for_fixed_seed_clock_and_source(self):
        module = load_module()
        first = module.derive_run_identity(
            "qa-fixtures-v1", 1618033988, "2026-01-01T00:00:00Z", "a" * 40
        )
        second = module.derive_run_identity(
            "qa-fixtures-v1", 1618033988, "2026-01-01T00:00:00Z", "a" * 40
        )

        self.assertEqual(first, second)
        self.assertRegex(first, r"^qa-[0-9a-f]{20}$")
        self.assertNotEqual(
            first,
            module.derive_run_identity(
                "qa-fixtures-v1", 1618033989, "2026-01-01T00:00:00Z", "a" * 40
            ),
        )

    def test_repository_provenance_binds_build_scene_content_and_save_versions(self):
        module = load_module()
        policy = module.load_policy(POLICY)

        provenance = module.collect_repository_provenance(REPO_ROOT, policy)

        self.assertRegex(provenance["sourceRevision"], r"^[0-9a-f]{40}$")
        self.assertIsInstance(provenance["sourceDirty"], bool)
        self.assertRegex(provenance["suite"]["policySha256"], r"^[0-9a-f]{64}$")
        self.assertRegex(provenance["suite"]["runnerSha256"], r"^[0-9a-f]{64}$")
        self.assertRegex(provenance["suite"]["manualBaselineSha256"], r"^[0-9a-f]{64}$")
        self.assertRegex(provenance["suite"]["evidenceSchemaSha256"], r"^[0-9a-f]{64}$")
        self.assertEqual(provenance["unity"]["version"], "6000.3.22f1")
        self.assertTrue(provenance["build"]["version"])
        self.assertRegex(provenance["scene"]["enabledManifestSha256"], r"^[0-9a-f]{64}$")
        self.assertRegex(provenance["scene"]["generatedManifestSha256"], r"^[0-9a-f]{64}$")
        self.assertRegex(provenance["content"]["worldCatalogSha256"], r"^[0-9a-f]{64}$")
        self.assertRegex(provenance["content"]["narrativeCatalogSha256"], r"^[0-9a-f]{64}$")
        self.assertEqual(provenance["save"]["formatId"], "anotherlife.local-save")
        self.assertEqual(provenance["save"]["schemaVersion"], 1)
        self.assertRegex(provenance["save"]["fixtureManifestSha256"], r"^[0-9a-f]{64}$")

    def test_repeated_attempts_normalize_logs_and_detect_nondeterminism(self):
        module = load_module()
        contract = {
            "id": "scene-manifest",
            "failureCode": "QA_SCENE_MANIFEST",
            "repeat": 2,
            "evidencePattern": r"digest=(?P<digest>[0-9a-f]{64})",
        }

        stable = module.evaluate_contract_attempts(
            contract,
            [
                {"exitCode": 0, "stdout": "duration=1 digest=" + "a" * 64, "stderr": ""},
                {"exitCode": 0, "stdout": "duration=9 digest=" + "a" * 64, "stderr": ""},
            ],
        )
        self.assertEqual(stable["status"], "passed")
        self.assertEqual(stable["evidence"], {"digest": "a" * 64})

        divergent = module.evaluate_contract_attempts(
            contract,
            [
                {"exitCode": 0, "stdout": "digest=" + "a" * 64, "stderr": ""},
                {"exitCode": 0, "stdout": "digest=" + "b" * 64, "stderr": ""},
            ],
        )
        self.assertEqual(divergent["status"], "stop_ship")
        self.assertEqual(divergent["reasonCode"], "nondeterministic_evidence")
        self.assertEqual(divergent["failureCode"], "QA_SCENE_MANIFEST")

    def test_nonzero_and_missing_evidence_identify_the_violated_contract(self):
        module = load_module()
        contract = {
            "id": "save-migration",
            "failureCode": "QA_SAVE_MIGRATION",
            "repeat": 1,
            "evidencePattern": r"passed=(?P<passed>\d+)",
        }

        failed = module.evaluate_contract_attempts(
            contract,
            [{"exitCode": 7, "stdout": "", "stderr": "migration failed"}],
        )
        self.assertEqual(failed["status"], "stop_ship")
        self.assertEqual(failed["reasonCode"], "command_failed")
        self.assertEqual(failed["failureCode"], "QA_SAVE_MIGRATION")

        missing = module.evaluate_contract_attempts(
            contract,
            [{"exitCode": 0, "stdout": "success without totals", "stderr": ""}],
        )
        self.assertEqual(missing["status"], "stop_ship")
        self.assertEqual(missing["reasonCode"], "missing_evidence")

    def test_manual_comparison_fails_closed_on_material_divergence_or_missing_row(self):
        module = load_module()
        results = [
            {"id": "unit", "status": "passed", "evidence": {"passed": "3"}},
            {"id": "integration", "status": "passed", "evidence": {"passed": "2"}},
        ]
        baseline = {
            "schemaVersion": 1,
            "results": [
                {"id": "unit", "expectedStatus": "passed"},
                {"id": "integration", "expectedStatus": "passed"},
            ],
        }
        self.assertEqual(module.compare_manual_results(results, baseline)["status"], "passed")

        diverged = copy.deepcopy(baseline)
        diverged["results"][0]["expectedStatus"] = "stop_ship"
        comparison = module.compare_manual_results(results, diverged)
        self.assertEqual(comparison["status"], "stop_ship")
        self.assertEqual(comparison["reasonCode"], "manual_result_divergence")

        missing = copy.deepcopy(baseline)
        missing["results"].pop()
        comparison = module.compare_manual_results(results, missing)
        self.assertEqual(comparison["status"], "stop_ship")
        self.assertEqual(comparison["reasonCode"], "manual_evidence_missing")

    def test_manual_material_identity_mismatch_is_stop_ship(self):
        module = load_module()
        policy = module.load_policy(POLICY)
        provenance = module.collect_repository_provenance(REPO_ROOT, policy)
        results = [{"id": "unit", "status": "passed", "evidence": {}}]
        baseline = {
            "schemaVersion": 1,
            "results": [{"id": "unit", "expectedStatus": "passed"}],
            "materialProvenance": {
                "unityVersion": provenance["unity"]["version"],
                "buildVersion": provenance["build"]["version"],
                "enabledSceneManifestSha256": provenance["scene"]["enabledManifestSha256"],
                "generatedSceneManifestSha256": provenance["scene"]["generatedManifestSha256"],
                "worldCatalogSha256": provenance["content"]["worldCatalogSha256"],
                "narrativeCatalogSha256": "0" * 64,
                "saveFormatId": provenance["save"]["formatId"],
                "saveSchemaVersion": provenance["save"]["schemaVersion"],
                "saveFixtureManifestSha256": provenance["save"]["fixtureManifestSha256"],
            },
        }
        comparison = module.compare_manual_results(results, baseline, provenance)
        self.assertEqual(comparison["status"], "stop_ship")
        self.assertEqual(comparison["reasonCode"], "manual_material_divergence")
        self.assertIn("narrativeCatalogSha256", comparison["divergedFields"])

    def test_representative_green_and_intentional_failure_runs_are_captured(self):
        module = load_module()
        green = module.verify_report(
            REPO_ROOT / "tools/qa/evidence/representative-green/report.json"
        )
        failed = module.verify_report(
            REPO_ROOT / "tools/qa/evidence/representative-intentional-failure/report.json"
        )

        self.assertEqual(green["status"], "passed")
        self.assertEqual(green["profile"], "contract")
        self.assertEqual(
            [item["id"] for item in green["contracts"]],
            list(module.REQUIRED_CONTRACTS),
        )
        self.assertEqual(green["manualComparison"]["reasonCode"], "automated_manual_equivalent")
        self.assertEqual(green["provenance"]["unity"]["version"], "6000.3.22f1")
        self.assertEqual(green["provenance"]["save"]["schemaVersion"], 1)
        self.assertTrue(
            (REPO_ROOT / "tools/qa/evidence/representative-green/junit.xml").is_file()
        )

        self.assertEqual(failed["status"], "stop_ship")
        self.assertEqual(failed["profile"], "contract")
        violated = next(item for item in failed["contracts"] if item["id"] == "scene-manifest")
        self.assertEqual(violated["failureCode"], "QA_SCENE_MANIFEST")
        self.assertEqual(violated["reasonCode"], "intentional_failure_fixture")
        self.assertNotEqual(failed.get("reportSha256"), green.get("reportSha256"))

    def test_fixture_profile_writes_verifiable_json_junit_logs_and_nonzero_failure(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "green"
            exit_code = module.main([
                "--repo-root", str(REPO_ROOT),
                "--policy", str(POLICY),
                "--profile", "contract",
                "--output-dir", str(output),
            ])
            self.assertEqual(exit_code, 0)
            report = module.verify_report(output / "report.json")
            self.assertEqual(report["status"], "passed")
            self.assertEqual(
                set(module.REQUIRED_CONTRACTS),
                {result["id"] for result in report["contracts"]},
            )
            self.assertTrue((output / "junit.xml").is_file())
            self.assertTrue(all((output / path).is_file() for path in report["artifacts"]["logs"]))

            failed_output = Path(temporary) / "failed"
            exit_code = module.main([
                "--repo-root", str(REPO_ROOT),
                "--policy", str(POLICY),
                "--profile", "contract",
                "--output-dir", str(failed_output),
                "--inject-failure", "scene-manifest",
            ])
            self.assertNotEqual(exit_code, 0)
            report = module.verify_report(failed_output / "report.json")
            self.assertEqual(report["status"], "stop_ship")
            violated = next(item for item in report["contracts"] if item["id"] == "scene-manifest")
            self.assertEqual(violated["failureCode"], "QA_SCENE_MANIFEST")
            self.assertEqual(violated["reasonCode"], "intentional_failure_fixture")


if __name__ == "__main__":
    unittest.main()
