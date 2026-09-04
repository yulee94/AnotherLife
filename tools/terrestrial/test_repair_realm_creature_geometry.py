import unittest
from pathlib import Path

from tools.terrestrial.repair_realm_creature_geometry import (
    count_prominent_profile_peaks,
    portable_report_path,
    validate_repair_report,
)


class RealmCreatureGeometryRepairTests(unittest.TestCase):
    def test_report_paths_are_repo_relative_and_reject_external_inputs(self):
        root = Path("D:/AnotherLife")
        self.assertEqual(
            "unity/example.fbx",
            portable_report_path(root / "unity" / "example.fbx", root),
        )
        with self.assertRaises(ValueError):
            portable_report_path(Path("C:/Temp/raw.fbx"), root)

    @staticmethod
    def _valid_report():
        return {
            "modelId": "boss_eldergrove_mere_root_leviathan",
            "sourceTaskIds": ["task-v001", "task-v002"],
            "inputSha256": "a" * 64,
            "outputSha256": "b" * 64,
            "status": "clean_geometry_pass_texture_uplift_required",
            "productionReady": False,
            "rigged": False,
            "runtimeIntegrationState": "Blocked",
            "metrics": {},
        }

    def test_counts_seven_spaced_prominent_profile_peaks(self):
        samples = [
            (0.00, 0.10), (0.02, 0.44), (0.04, 0.12),
            (0.10, 0.47), (0.12, 0.11),
            (0.18, 0.51), (0.20, 0.13),
            (0.27, 0.49), (0.29, 0.12),
            (0.36, 0.46), (0.38, 0.10),
            (0.46, 0.42), (0.48, 0.11),
            (0.56, 0.39), (0.58, 0.10),
        ]
        self.assertEqual(
            count_prominent_profile_peaks(samples, min_prominence=0.20, min_spacing=0.05),
            7,
        )

    def test_ignores_shallow_noise_and_duplicate_peak_samples(self):
        samples = [
            (0.00, 0.10), (0.02, 0.41), (0.025, 0.40), (0.04, 0.11),
            (0.10, 0.16), (0.12, 0.10),
            (0.20, 0.45), (0.22, 0.12),
        ]
        self.assertEqual(
            count_prominent_profile_peaks(samples, min_prominence=0.20, min_spacing=0.05),
            2,
        )

    def test_accepts_fail_closed_mere_root_geometry_report(self):
        report = {
            "modelId": "boss_eldergrove_mere_root_leviathan",
            "sourceTaskIds": ["task-v001", "task-v002"],
            "inputSha256": "a" * 64,
            "outputSha256": "b" * 64,
            "status": "clean_geometry_pass_texture_rebuild_required",
            "productionReady": False,
            "rigged": False,
            "runtimeIntegrationState": "Blocked",
            "metrics": {
                "cervicalVanes": 7,
                "shieldSkullToNeckWidthRatio": 1.20,
                "nonManifoldEdgesBefore": 182,
                "nonManifoldEdgesAfter": 182,
            },
        }
        self.assertEqual([], validate_repair_report(report))

    def test_rejects_roc_without_fixed_break_and_exact_blade_counts(self):
        report = self._valid_report()
        report["modelId"] = "boss_crownlands_meridian_tempest_roc"
        report["metrics"] = {
            "outerBladesLeft": 6,
            "outerBladesRight": 7,
            "tailRudders": 2,
            "leftWingFixedBreak": False,
            "shieldSkullToNeckWidthRatio": 1.2,
            "nonManifoldEdgesBefore": 14,
            "nonManifoldEdgesAfter": 14,
        }

        errors = validate_repair_report(report)

        self.assertIn("outerBladesLeft must equal 7", errors)
        self.assertIn("leftWingFixedBreak must be true", errors)

    def test_accepts_complete_roc_repair_report(self):
        report = self._valid_report()
        report["modelId"] = "boss_crownlands_meridian_tempest_roc"
        report["metrics"] = {
            "outerBladesLeft": 7,
            "outerBladesRight": 7,
            "tailRudders": 2,
            "leftWingFixedBreak": True,
            "shieldSkullToNeckWidthRatio": 1.18,
            "nonManifoldEdgesBefore": 14,
            "nonManifoldEdgesAfter": 14,
        }

        self.assertEqual([], validate_repair_report(report))

    def test_accepts_complete_sunmane_structural_audit(self):
        report = self._valid_report()
        report["modelId"] = "elite_eldergrove_sunmane_thornstag"
        report["metrics"] = {
            "neckRails": 2,
            "forefootDigits": 3,
            "hindfootDigits": 3,
            "fixedLeftAntlerBreak": True,
            "dorsalManePreserved": True,
            "nonManifoldEdgesBefore": 8,
            "nonManifoldEdgesAfter": 8,
        }

        self.assertEqual([], validate_repair_report(report))

    def test_rejects_sunmane_with_lost_rails_or_generic_feet(self):
        report = self._valid_report()
        report["modelId"] = "elite_eldergrove_sunmane_thornstag"
        report["metrics"] = {
            "neckRails": 1,
            "forefootDigits": 2,
            "hindfootDigits": 3,
            "fixedLeftAntlerBreak": False,
            "dorsalManePreserved": True,
            "nonManifoldEdgesBefore": 8,
            "nonManifoldEdgesAfter": 8,
        }

        errors = validate_repair_report(report)

        self.assertIn("neckRails must equal 2", errors)
        self.assertIn("forefootDigits must equal 3", errors)
        self.assertIn("fixedLeftAntlerBreak must be true", errors)

    def test_accepts_complete_crownstep_repair_report(self):
        report = self._valid_report()
        report["modelId"] = "elite_crownlands_crownstep"
        report["status"] = "clean_geometry_pass_texture_rebuild_required"
        report["metrics"] = {
            "manePlateRows": 3,
            "pawDigits": 5,
            "tailTufted": False,
            "tailBaseToTipWidthRatio": 2.4,
            "forequarterToHindquarterWidthRatio": 1.08,
            "nonManifoldEdgesBefore": 0,
            "nonManifoldEdgesAfter": 0,
        }

        self.assertEqual([], validate_repair_report(report))

    def test_rejects_uplift_status_for_retopologized_sources(self):
        for model_id in (
            "boss_eldergrove_mere_root_leviathan",
            "elite_crownlands_crownstep",
        ):
            with self.subTest(model_id=model_id):
                report = self._valid_report()
                report["modelId"] = model_id
                report["metrics"] = (
                    {
                        "cervicalVanes": 7,
                        "shieldSkullToNeckWidthRatio": 1.2,
                        "nonManifoldEdgesBefore": 1,
                        "nonManifoldEdgesAfter": 1,
                    }
                    if model_id.startswith("boss_")
                    else {
                        "manePlateRows": 3,
                        "pawDigits": 5,
                        "tailTufted": False,
                        "tailBaseToTipWidthRatio": 2.2,
                        "forequarterToHindquarterWidthRatio": 1.1,
                        "nonManifoldEdgesBefore": 1,
                        "nonManifoldEdgesAfter": 1,
                    }
                )
                diagnostics = validate_repair_report(report)
                self.assertTrue(any("texture-rebuild" in item for item in diagnostics))

    def test_rejects_generic_crownstep_structure(self):
        report = self._valid_report()
        report["modelId"] = "elite_crownlands_crownstep"
        report["metrics"] = {
            "manePlateRows": 1,
            "pawDigits": 4,
            "tailTufted": True,
            "tailBaseToTipWidthRatio": 1.1,
            "forequarterToHindquarterWidthRatio": 0.9,
            "nonManifoldEdgesBefore": 0,
            "nonManifoldEdgesAfter": 0,
        }

        errors = validate_repair_report(report)

        self.assertIn("manePlateRows must equal 3", errors)
        self.assertIn("pawDigits must equal 5", errors)
        self.assertIn("tailTufted must be false", errors)

    def test_rejects_misleading_geometry_report(self):
        report = {
            "modelId": "boss_eldergrove_mere_root_leviathan",
            "sourceTaskIds": [],
            "inputSha256": "bad",
            "outputSha256": "bad",
            "status": "production_ready",
            "productionReady": True,
            "rigged": False,
            "runtimeIntegrationState": "Ready",
            "metrics": {
                "cervicalVanes": 6,
                "shieldSkullToNeckWidthRatio": 1.0,
                "nonManifoldEdgesBefore": 10,
                "nonManifoldEdgesAfter": 20,
            },
        }
        diagnostics = validate_repair_report(report)
        self.assertTrue(any("source task" in item for item in diagnostics))
        self.assertTrue(any("seven cervical vanes" in item for item in diagnostics))
        self.assertTrue(any("shield-skull" in item for item in diagnostics))
        self.assertTrue(any("non-manifold" in item for item in diagnostics))
        self.assertTrue(any("productionReady" in item for item in diagnostics))
        self.assertTrue(any("runtimeIntegrationState" in item for item in diagnostics))


if __name__ == "__main__":
    unittest.main()
