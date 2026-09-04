#!/usr/bin/env python3
"""Fail-closed tests for the model/motion/skill-VFX validation harness."""

from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

import model_motion_skill_vfx_harness as harness


class ModelMotionSkillVfxHarnessTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repo_root = Path(__file__).resolve().parents[3]
        cls.catalog = harness.validate_catalog(
            harness.load_json(cls.repo_root / harness.HARNESS_PATH),
            cls.repo_root,
        )

    def evaluate(self, packet: dict) -> dict:
        return harness.evaluate_packet(self.catalog, copy.deepcopy(packet))

    def test_committed_catalog_matches_schema_and_four_kinds(self) -> None:
        self.assertEqual("mmv_harness_model_motion_skill_vfx_v001", self.catalog["harnessId"])
        self.assertEqual(["champion", "npc", "beast", "monster"], self.catalog["requiredKinds"])
        self.assertTrue(self.catalog["verdictPolicy"]["weightedScoreForbidden"])
        self.assertEqual(5, len(self.catalog["requiredMotionAxes"]))
        self.assertEqual(5, len(self.catalog["requiredSkillEffectAxes"]))

    def test_complete_champion_npc_beast_and_monster_packet_passes(self) -> None:
        report = self.evaluate(harness.complete_packet())
        self.assertEqual("PASS", report["overall"])
        self.assertEqual(4, len(report["models"]))
        self.assertEqual(4, len(report["skills"]))
        self.assertTrue(all(row["verdict"] == "PASS" for row in report["models"]))
        self.assertTrue(all(row["verdict"] == "PASS" for row in report["skills"]))
        self.assertIsNone(report["weightedScore"])

    def test_missing_walking_fails_even_when_other_axes_pass(self) -> None:
        packet = harness.complete_packet()
        champion = packet["models"][0]
        champion["presentMotionKeys"] = [
            key for key in champion["presentMotionKeys"] if key != "locomotion.walk"
        ]
        report = self.evaluate(packet)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(
            any("missing_motion_axis:walking" in reason for reason in report["reasons"])
        )
        self.assertEqual("FAIL", report["models"][0]["verdict"])
        self.assertTrue(all(row["verdict"] == "PASS" for row in report["models"][1:]))

    def test_missing_running_attack_special_and_cast_each_fail_closed(self) -> None:
        cases = (
            ("running", "locomotion.run", "npc"),
            ("attacking", "attack.basic", "beast"),
            ("special_attack", "attack.special", "monster"),
            ("cast_use", "skill.cast", "champion"),
        )
        for axis, key, kind in cases:
            packet = harness.complete_packet()
            model = next(row for row in packet["models"] if row["kind"] == kind)
            model["presentMotionKeys"] = [
                item
                for item in model["presentMotionKeys"]
                if item != key and not (axis == "cast_use" and item.startswith("skill."))
            ]
            if axis == "special_attack":
                model["presentMotionKeys"] = [
                    item
                    for item in model["presentMotionKeys"]
                    if item not in {"attack.special", "attack.heavy", "attack.charged"}
                ]
            report = self.evaluate(packet)
            self.assertEqual("FAIL", report["overall"], axis)
            self.assertTrue(
                any(f"missing_motion_axis:{axis}" in reason for reason in report["reasons"]),
                axis,
            )

    def test_missing_skill_telegraph_or_accessibility_fails_closed(self) -> None:
        packet = harness.complete_packet()
        packet["skills"][0]["effects"]["telegraph"] = ""
        report = self.evaluate(packet)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("missing_skill_effect:telegraph" in reason for reason in report["reasons"]))

        packet = harness.complete_packet()
        packet["skills"][1]["effects"]["accessibility"] = ""
        report = self.evaluate(packet)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(
            any("missing_skill_effect:accessibility" in reason for reason in report["reasons"])
        )

    def test_missing_cast_motion_phase_fails_closed(self) -> None:
        packet = harness.complete_packet()
        packet["skills"][2]["phases"]["cast"] = ""
        report = self.evaluate(packet)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("missing_skill_motion:cast" in reason for reason in report["reasons"]))

    def test_missing_monster_representative_fails_and_is_not_hidden(self) -> None:
        packet = harness.complete_packet()
        packet["models"] = [row for row in packet["models"] if row["kind"] != "monster"]
        report = self.evaluate(packet)
        self.assertEqual("FAIL", report["overall"])
        monster = next(row for row in report["models"] if row["kind"] == "monster")
        self.assertEqual("FAIL", monster["verdict"])
        self.assertTrue(any("missing_representative:monster" in reason for reason in report["reasons"]))

    def test_weighted_score_is_rejected(self) -> None:
        packet = harness.complete_packet()
        packet["weightedScore"] = 0.91
        report = self.evaluate(packet)
        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(any("weighted_score_forbidden" in reason for reason in report["reasons"]))

        packet = harness.complete_packet()
        packet["models"][0]["score"] = 88
        report = self.evaluate(packet)
        self.assertEqual("FAIL", report["overall"])
        self.assertEqual("FAIL", report["models"][0]["verdict"])

    def test_missing_player_build_evidence_is_blocked_not_pass(self) -> None:
        packet = harness.complete_packet()
        for model in packet["models"]:
            model["playerBuildVerdict"] = ""
        report = self.evaluate(packet)
        self.assertEqual("BLOCKED", report["overall"])
        self.assertTrue(all(row["verdict"] == "BLOCKED" for row in report["models"]))
        self.assertTrue(all(row["verdict"] == "PASS" for row in report["skills"]))

    def test_missing_mesh_evidence_is_blocked(self) -> None:
        packet = harness.complete_packet()
        del packet["models"][0]["checks"]["mesh_topology"]
        report = self.evaluate(packet)
        self.assertEqual("BLOCKED", report["overall"])
        self.assertEqual("BLOCKED", report["models"][0]["verdict"])
        self.assertTrue(any("missing_evidence:mesh_topology" in reason for reason in report["reasons"]))

    def test_markdown_report_lists_explicit_verdicts(self) -> None:
        report = self.evaluate(harness.complete_packet())
        markdown = harness.render_markdown(report)
        self.assertIn("Overall: **PASS**", markdown)
        self.assertIn("champion", markdown)
        self.assertIn("monster", markdown)
        self.assertIn("Weighted score: forbidden", markdown)

    def test_live_repo_evaluation_does_not_pass_missing_monster_or_player_build(self) -> None:
        report = harness.evaluate_repo(self.repo_root)
        self.assertNotEqual("PASS", report["overall"])
        kinds = {row["kind"] for row in report["models"]}
        self.assertEqual({"champion", "npc", "beast", "monster"}, kinds)
        monster = next(row for row in report["models"] if row["kind"] == "monster")
        self.assertEqual("FAIL", monster["verdict"])
        self.assertGreaterEqual(len(report["skills"]), 1)
        self.assertIsNone(report["weightedScore"])
        markdown = harness.render_markdown(report)
        self.assertIn(report["overall"], markdown)

    def test_schema_rejects_undeclared_weighted_score_on_catalog(self) -> None:
        catalog = copy.deepcopy(self.catalog)
        catalog["weightedScore"] = 1
        with self.assertRaises(harness.HarnessValidationError) as caught:
            harness.validate_catalog(catalog, self.repo_root)
        self.assertIn("SchemaViolation", str(caught.exception))


if __name__ == "__main__":
    unittest.main()
