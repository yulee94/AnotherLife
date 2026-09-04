#!/usr/bin/env python3
"""Fail-closed tests for one boss + one skill presentation profile."""

from __future__ import annotations

import copy
import importlib.util
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("validate_boss_skill_presentation.py")
SPEC = importlib.util.spec_from_file_location("boss_skill_presentation", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Cannot import validator from {SCRIPT_PATH}")
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


class BossSkillPresentationTests(unittest.TestCase):
    def test_committed_colossus_and_faultline_slam_pass(self) -> None:
        report = VALIDATOR.validate_default_catalog()

        self.assertEqual("PASS", report["overall"])
        self.assertEqual("presentation_catalog_runtime_contract", report["scope"])
        self.assertEqual("boss_stonehold_fault_crowned_colossus", report["boss"]["modelId"])
        self.assertEqual(
            "tdf_boss_stonehold_fault_crowned_colossus",
            report["boss"]["sourceProfileId"],
        )
        self.assertEqual("boss_faultline_slam", report["skill"]["skillId"])
        self.assertEqual("PASS", report["harness"]["motionAxes"])
        self.assertEqual("PASS", report["harness"]["skill"])
        self.assertFalse(report["gameplayAuthority"])
        self.assertFalse(report["runtimeSpawn"])
        self.assertIn("device_evidence", report["deferred"])
        self.assertIn("user_readability", report["deferred"])
        self.assertIn("scale_out", report["deferred"])

    def test_quality_and_distance_tiers_are_complete_and_do_not_change_gameplay(self) -> None:
        report = VALIDATOR.validate_default_catalog()
        snapshot = report["runtimeSnapshot"]
        baseline = dict(snapshot["gameplay"])

        for quality in ("low", "balanced", "high"):
            for distance in ("hero", "nearby", "distant"):
                resolved = VALIDATOR.resolve_presentation(
                    VALIDATOR.load_default_catalog(),
                    quality=quality,
                    distance=distance,
                )
                self.assertEqual(baseline, resolved["gameplay"])
                self.assertEqual(quality, resolved["quality"])
                self.assertEqual(distance, resolved["distance"])
                self.assertTrue(resolved["protectedCuesPreserved"])
                self.assertGreaterEqual(resolved["pool"]["maxActive"], 1)

    def test_forbidden_gameplay_or_slot_fields_fail_closed(self) -> None:
        catalog = VALIDATOR.load_default_catalog()
        catalog["skillProfiles"][0]["slot"] = 0
        report = VALIDATOR.validate_catalog_payload(catalog)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("ForbiddenField:slot" in issue for issue in report["issues"]))

        catalog = VALIDATOR.load_default_catalog()
        catalog["bossProfiles"][0]["ItemGrade"] = "legendary"
        report = VALIDATOR.validate_catalog_payload(catalog)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("ForbiddenField:ItemGrade" in issue for issue in report["issues"]))

        catalog = VALIDATOR.load_default_catalog()
        catalog["skillProfiles"][0]["power"] = 260
        report = VALIDATOR.validate_catalog_payload(catalog)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("ForbiddenField:power" in issue for issue in report["issues"]))

    def test_missing_unknown_duplicate_and_hash_mismatch_fail_closed(self) -> None:
        catalog = VALIDATOR.load_default_catalog()
        catalog["bossProfiles"] = []
        report = VALIDATOR.validate_catalog_payload(catalog)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("MissingBossProfile" in issue for issue in report["issues"]))

        catalog = VALIDATOR.load_default_catalog()
        catalog["skillProfiles"][0]["skillId"] = "unknown_skill"
        report = VALIDATOR.validate_catalog_payload(catalog)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("UnknownSkillId" in issue for issue in report["issues"]))

        catalog = VALIDATOR.load_default_catalog()
        catalog["bossProfiles"].append(copy.deepcopy(catalog["bossProfiles"][0]))
        report = VALIDATOR.validate_catalog_payload(catalog)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("DuplicateBossProfile" in issue for issue in report["issues"]))

        catalog = VALIDATOR.load_default_catalog()
        catalog["bossProfiles"][0]["sourceSha256"] = "0" * 64
        report = VALIDATOR.validate_catalog_payload(catalog)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("SourceHashMismatch" in issue for issue in report["issues"]))

    def test_unsupported_schema_version_fails_closed(self) -> None:
        catalog = VALIDATOR.load_default_catalog()
        catalog["schemaVersion"] = 2
        report = VALIDATOR.validate_catalog_payload(catalog)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("UnsupportedSchemaVersion" in issue for issue in report["issues"]))

    def test_missing_accessibility_or_pooling_fails_closed(self) -> None:
        catalog = VALIDATOR.load_default_catalog()
        del catalog["skillProfiles"][0]["accessibility"]
        report = VALIDATOR.validate_catalog_payload(catalog)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("MissingAccessibility" in issue for issue in report["issues"]))

        catalog = VALIDATOR.load_default_catalog()
        del catalog["bossProfiles"][0]["pooling"]
        report = VALIDATOR.validate_catalog_payload(catalog)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("MissingPooling" in issue for issue in report["issues"]))

    def test_pooled_acquire_release_is_stable(self) -> None:
        pool = VALIDATOR.PresentationPool(max_active=2, max_pooled=4)
        first = pool.acquire("boss_faultline_slam")
        pool.release(first)
        second = pool.acquire("boss_faultline_slam")
        self.assertEqual(first["instanceId"], second["instanceId"])
        self.assertEqual(1, pool.created)
        pool.release(second)
        self.assertEqual(0, pool.active)
        extra = [pool.acquire("boss_faultline_slam") for _ in range(2)]
        overflow = pool.acquire("boss_faultline_slam")
        self.assertIsNone(overflow)
        self.assertEqual(2, pool.active)
        for item in extra:
            pool.release(item)

    def test_harness_skill_packet_passes_without_weighted_score(self) -> None:
        report = VALIDATOR.validate_default_catalog()
        harness_report = report["harness"]["packetReport"]
        self.assertEqual("PASS", harness_report["skills"][0]["verdict"])
        self.assertIsNone(harness_report["weightedScore"])
        self.assertTrue(
            all(
                check["verdict"] == "PASS"
                for check in harness_report["skills"][0]["checks"]
            )
        )

    def test_live_four_kind_harness_stays_honest_and_non_pass(self) -> None:
        live = VALIDATOR.evaluate_live_model_harness()
        self.assertNotEqual("PASS", live["overall"])
        kinds = {row["kind"] for row in live["models"]}
        self.assertEqual({"champion", "npc", "beast", "monster"}, kinds)

    def test_sabotage_missing_skill_phases_fails_then_restore_passes(self) -> None:
        catalog = VALIDATOR.load_default_catalog()
        catalog["skillProfiles"][0]["phases"]["cast"] = ""
        sabotaged = VALIDATOR.validate_catalog_payload(catalog)
        self.assertEqual("FAIL", sabotaged["overall"])
        self.assertTrue(
            any("missing_skill_motion:cast" in issue for issue in sabotaged["issues"])
            or any("MissingSkillPhase:cast" in issue for issue in sabotaged["issues"])
        )
        restored = VALIDATOR.validate_default_catalog()
        self.assertEqual("PASS", restored["overall"])


if __name__ == "__main__":
    unittest.main()
