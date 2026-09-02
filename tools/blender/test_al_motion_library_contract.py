from __future__ import annotations

import json
import sys
import unittest
from copy import deepcopy
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))

from al_motion_library_contract import (
    CATALOG_PATH,
    REQUIRED_MANIFEST_PATH,
    SOURCE_PLAN_PATH,
    expected_binding_keys,
    resolve_motion_rule,
    validate_artifacts,
    validate_built_catalog,
    validate_repeatability,
    validate_source_plan,
)


class MotionLibraryContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.source_plan = json.loads(
            (REPO_ROOT / SOURCE_PLAN_PATH).read_text(encoding="utf-8")
        )
        cls.required_manifest = json.loads(
            (REPO_ROOT / REQUIRED_MANIFEST_PATH).read_text(encoding="utf-8")
        )
        cls.catalog = json.loads((REPO_ROOT / CATALOG_PATH).read_text(encoding="utf-8"))

    def test_source_plan_is_complete_and_source_bounded(self) -> None:
        self.assertEqual([], validate_source_plan(REPO_ROOT, self.source_plan))

    def test_expected_bindings_reuse_shared_humanoid_clips(self) -> None:
        expected = expected_binding_keys(self.source_plan, self.required_manifest)
        champion = expected["rmc_representative_champion_vanguard_v001"]
        npc = expected["rmc_representative_npc_covenant_sentinel_v001"]
        beast = expected["rmc_representative_beast_slagwhistle_v001"]
        self.assertGreaterEqual(len(champion), 40)
        self.assertGreaterEqual(len(npc), 50)
        self.assertEqual(6, len(beast))
        self.assertGreaterEqual(len(champion & npc), 30)

    def test_attack_and_interaction_rules_select_authored_styles(self) -> None:
        expected = {
            "attack.basic": "attack_action",
            "daily.sleep": "idle_cycle",
            "idle.neutral": "idle_cycle",
            "idle.variant": "idle_cycle",
            "interaction.cut": "attack_action",
            "locomotion.fall": "vertical_action",
            "locomotion.jump": "vertical_action",
            "skill.commit": "attack_action",
            "social.talk": "idle_cycle",
        }
        for motion_key, style in expected.items():
            with self.subTest(motion_key=motion_key):
                self.assertEqual(
                    style,
                    resolve_motion_rule(self.source_plan, motion_key)["style"],
                )

    def test_built_catalog_has_stable_complete_bindings(self) -> None:
        self.assertEqual(
            [],
            validate_built_catalog(
                REPO_ROOT,
                self.source_plan,
                self.required_manifest,
                self.catalog,
            ),
        )

    def test_catalog_validation_fails_closed_on_missing_binding(self) -> None:
        malformed = deepcopy(self.catalog)
        malformed["bindings"].pop()
        issues = validate_built_catalog(
            REPO_ROOT,
            self.source_plan,
            self.required_manifest,
            malformed,
        )
        self.assertTrue(any("MissingBinding" in issue for issue in issues), issues)

    def test_catalog_validation_fails_closed_on_bad_event_order(self) -> None:
        malformed = deepcopy(self.catalog)
        attack = next(clip for clip in malformed["clips"] if clip["hitboxWindows"])
        attack["hitboxWindows"][0]["closeFrame"] = attack["hitboxWindows"][0][
            "openFrame"
        ]
        issues = validate_built_catalog(
            REPO_ROOT,
            self.source_plan,
            self.required_manifest,
            malformed,
        )
        self.assertTrue(any("InvalidHitboxWindow" in issue for issue in issues), issues)

    def test_catalog_validation_fails_closed_on_loop_policy_mismatch(self) -> None:
        malformed = deepcopy(self.catalog)
        walk = next(
            clip
            for clip in malformed["clips"]
            if clip["motionKey"] == "locomotion.walk"
        )
        walk["loop"] = False
        issues = validate_built_catalog(
            REPO_ROOT,
            self.source_plan,
            self.required_manifest,
            malformed,
        )
        self.assertTrue(any("LoopPolicyMismatch" in issue for issue in issues), issues)

    def test_catalog_validation_fails_closed_on_unexplained_beast_motion(self) -> None:
        malformed = deepcopy(self.catalog)
        coverage = next(
            row
            for row in malformed["coverage"]
            if row["representativeProfileId"]
            == "rmc_representative_beast_slagwhistle_v001"
        )
        coverage["explainedBlocked"].pop()
        issues = validate_built_catalog(
            REPO_ROOT,
            self.source_plan,
            self.required_manifest,
            malformed,
        )
        self.assertTrue(
            any("UnexplainedCatalogGap" in issue for issue in issues), issues
        )

    def test_blender_outputs_and_measured_cleanup_receipts_are_valid(self) -> None:
        self.assertEqual([], validate_artifacts(REPO_ROOT, self.catalog))

    def test_repeatability_receipt_matches_catalog_and_sidecars(self) -> None:
        self.assertEqual([], validate_repeatability(REPO_ROOT, self.catalog))


if __name__ == "__main__":
    unittest.main()
