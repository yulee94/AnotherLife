#!/usr/bin/env python3
"""Fail-closed tests for the representative realm-creature production slice."""

from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("validate_realm_creature_production_slice.py")
SPEC = importlib.util.spec_from_file_location("realm_creature_production_slice", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Cannot import validator from {SCRIPT_PATH}")
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


class RealmCreatureProductionSliceTests(unittest.TestCase):
    def test_committed_stonehold_boss_slice_passes_source_qualification(self) -> None:
        report = VALIDATOR.validate_default_slice()

        self.assertEqual("PASS", report["overall"])
        self.assertEqual(
            "boss_stonehold_fault_crowned_colossus",
            report["modelId"],
        )
        self.assertEqual("PASS", report["sourceQualification"])
        self.assertEqual("BLOCKED", report["runtimeIntegration"])
        self.assertEqual("BLOCKED", report["deviceQualification"])
        self.assertFalse(report["gameplayOrSpawnActivation"])

    def test_runtime_asset_output_path_fails_closed(self) -> None:
        plan = VALIDATOR.load_json(VALIDATOR.DEFAULT_PLAN)
        plan["outputs"]["blend"] = (
            "unity/Assets/AL/Creatures/fault_crowned_colossus.blend"
        )
        with tempfile.TemporaryDirectory() as directory:
            plan_path = Path(directory) / "unsafe-plan.json"
            plan_path.write_text(json.dumps(plan), encoding="utf-8")
            report = VALIDATOR.validate_slice(plan_path=plan_path)

        self.assertEqual("FAIL", report["overall"])
        self.assertIn("RuntimePathForbidden:outputs.blend", report["issues"])

    def test_runtime_output_path_check_is_case_insensitive(self) -> None:
        plan = VALIDATOR.load_json(VALIDATOR.DEFAULT_PLAN)
        plan["outputs"]["blend"] = (
            "UNITY/ASSETS/AL/Creatures/fault_crowned_colossus.blend"
        )
        with tempfile.TemporaryDirectory() as directory:
            plan_path = Path(directory) / "unsafe-plan.json"
            plan_path.write_text(json.dumps(plan), encoding="utf-8")
            report = VALIDATOR.validate_slice(plan_path=plan_path)

        self.assertEqual("FAIL", report["overall"])
        self.assertIn("RuntimePathForbidden:outputs.blend", report["issues"])

    def test_failed_dcc_report_cannot_be_hash_rebound_as_pass(self) -> None:
        qualification = VALIDATOR.load_json(VALIDATOR.DEFAULT_QUALIFICATION)
        dcc_report = VALIDATOR.load_json(
            VALIDATOR.REPO_ROOT
            / qualification["artifacts"]["dccReport"]["path"]
        )
        dcc_report["status"] = "FAIL"
        with tempfile.TemporaryDirectory(dir=VALIDATOR.REPO_ROOT) as directory:
            directory_path = Path(directory)
            dcc_path = directory_path / "failed-dcc.json"
            dcc_path.write_text(json.dumps(dcc_report), encoding="utf-8")
            qualification["artifacts"]["dccReport"] = {
                "path": dcc_path.relative_to(VALIDATOR.REPO_ROOT).as_posix(),
                "bytes": dcc_path.stat().st_size,
                "sha256": VALIDATOR.sha256_file(dcc_path),
            }
            qualification_path = directory_path / "qualification.json"
            qualification_path.write_text(
                json.dumps(qualification),
                encoding="utf-8",
            )
            report = VALIDATOR.validate_slice(
                qualification_path=qualification_path,
            )

        self.assertEqual("FAIL", report["overall"])
        self.assertIn("DccReportNotPass", report["issues"])

    def test_runtime_file_cannot_be_rebound_as_source_artifact(self) -> None:
        qualification = VALIDATOR.load_json(VALIDATOR.DEFAULT_QUALIFICATION)
        runtime_path = (
            VALIDATOR.REPO_ROOT
            / "unity/Assets/AL/StreamingAssets/GameData/al_required_motion_manifest.json"
        )
        qualification["artifacts"]["blend"] = {
            "path": runtime_path.relative_to(VALIDATOR.REPO_ROOT).as_posix(),
            "bytes": runtime_path.stat().st_size,
            "sha256": VALIDATOR.sha256_file(runtime_path),
        }
        with tempfile.TemporaryDirectory() as directory:
            qualification_path = Path(directory) / "runtime-artifact.json"
            qualification_path.write_text(
                json.dumps(qualification),
                encoding="utf-8",
            )
            report = VALIDATOR.validate_slice(
                qualification_path=qualification_path,
            )

        self.assertEqual("FAIL", report["overall"])
        self.assertIn("RuntimeArtifactPathForbidden:blend", report["issues"])

    def test_duplicate_review_image_cannot_satisfy_evidence_count(self) -> None:
        qualification = VALIDATOR.load_json(VALIDATOR.DEFAULT_QUALIFICATION)
        first_review = qualification["artifacts"]["reviewImages"][0]
        qualification["artifacts"]["reviewImages"] = [first_review] * 4
        with tempfile.TemporaryDirectory() as directory:
            qualification_path = Path(directory) / "duplicate-reviews.json"
            qualification_path.write_text(
                json.dumps(qualification),
                encoding="utf-8",
            )
            report = VALIDATOR.validate_slice(
                qualification_path=qualification_path,
            )

        self.assertEqual("FAIL", report["overall"])
        self.assertIn("DuplicateReviewImage", report["issues"])

    def test_missing_lod2_review_evidence_fails_closed(self) -> None:
        qualification = VALIDATOR.load_json(VALIDATOR.DEFAULT_QUALIFICATION)
        qualification["artifacts"]["reviewImages"] = [
            row
            for row in qualification["artifacts"]["reviewImages"]
            if not row["path"].endswith("fault_crowned_colossus_lod2_bind_v001.png")
        ]
        with tempfile.TemporaryDirectory() as directory:
            qualification_path = Path(directory) / "missing-lod2-review.json"
            qualification_path.write_text(
                json.dumps(qualification),
                encoding="utf-8",
            )
            report = VALIDATOR.validate_slice(
                qualification_path=qualification_path,
            )

        self.assertEqual("FAIL", report["overall"])
        self.assertIn("RequiredReviewEvidenceMissing:lod2_bind", report["issues"])

    def test_missing_special_attack_motion_fails_closed(self) -> None:
        qualification = VALIDATOR.load_json(VALIDATOR.DEFAULT_QUALIFICATION)
        qualification["motions"] = [
            row
            for row in qualification["motions"]
            if row["motionKey"] != "attack.special"
        ]
        with tempfile.TemporaryDirectory() as directory:
            qualification_path = Path(directory) / "missing-motion.json"
            qualification_path.write_text(
                json.dumps(qualification),
                encoding="utf-8",
            )
            report = VALIDATOR.validate_slice(
                qualification_path=qualification_path,
            )

        self.assertEqual("FAIL", report["overall"])
        self.assertIn("RequiredMotionCoverageMissing", report["issues"])

    def test_duplicate_material_role_fails_closed(self) -> None:
        plan = VALIDATOR.load_json(VALIDATOR.DEFAULT_PLAN)
        plan["materialPolicy"]["textures"][-1]["role"] = "base_color"
        with tempfile.TemporaryDirectory() as directory:
            plan_path = Path(directory) / "duplicate-material-role.json"
            plan_path.write_text(json.dumps(plan), encoding="utf-8")
            report = VALIDATOR.validate_slice(plan_path=plan_path)

        self.assertEqual("FAIL", report["overall"])
        self.assertIn("MaterialTextureRolesMismatch", report["issues"])

    def test_selected_source_hash_tamper_fails_closed(self) -> None:
        plan = VALIDATOR.load_json(VALIDATOR.DEFAULT_PLAN)
        plan["source"]["sha256"] = "0" * 64
        with tempfile.TemporaryDirectory() as directory:
            plan_path = Path(directory) / "tampered-source.json"
            plan_path.write_text(json.dumps(plan), encoding="utf-8")
            report = VALIDATOR.validate_slice(plan_path=plan_path)

        self.assertEqual("FAIL", report["overall"])
        self.assertIn("HashMismatch:selectedSource", report["issues"])

    def test_absent_qualification_manifest_fails_closed(self) -> None:
        report = VALIDATOR.validate_slice(
            qualification_path=VALIDATOR.REPO_ROOT
            / "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001"
            / "ProductionSlices"
            / "missing-qualification.json",
        )

        self.assertEqual("FAIL", report["overall"])
        self.assertTrue(
            any(
                issue.startswith("InvalidQualification")
                for issue in report["issues"]
            )
        )


class CindermawRuntimeBenchmarkSliceTests(unittest.TestCase):
    def test_committed_cindermaw_slice_passes_source_qualification(self) -> None:
        report = VALIDATOR.validate_cindermaw_slice()

        self.assertEqual("PASS", report["overall"])
        self.assertEqual("elite_umbral_cindermaw_salamander", report["modelId"])
        self.assertEqual("PASS", report["sourceQualification"])
        self.assertEqual("BLOCKED", report["runtimeIntegration"])
        self.assertEqual("BLOCKED", report["deviceQualification"])
        self.assertFalse(report["gameplayOrSpawnActivation"])

    def test_cindermaw_source_manifest_stays_not_production_ready(self) -> None:
        manifest = VALIDATOR.load_json(VALIDATOR.SOURCE_MANIFEST)
        model = next(
            row
            for row in manifest["models"]
            if row["modelId"] == "elite_umbral_cindermaw_salamander"
        )

        self.assertFalse(model["productionReady"])
        self.assertEqual("Blocked", model["runtimeIntegrationState"])
        self.assertEqual(
            "7ad41ad8ce10aeca0919008f1d99c0d9f373a37e4100b2a490cb1bc5537e3b7b",
            model["selectedSource"]["sha256"],
        )
        self.assertTrue(
            str(model["selectedSource"]["path"]).endswith(
                "elite_umbral_cindermaw_salamander_source_v005.fbx"
            )
        )

    def test_cindermaw_vfx_sockets_stay_off_the_clean_mesh(self) -> None:
        qualification = VALIDATOR.load_json(VALIDATOR.DEFAULT_CINDERMAW_QUALIFICATION)
        bones = set(qualification["rig"]["boneNames"])

        self.assertIn("socket_vfx_mouth_ember", bones)
        self.assertIn("socket_vfx_fin_heat", bones)
        self.assertIn("socket_vfx_contact_steam", bones)
        self.assertTrue(qualification["material"]["runtimeVfxSeparate"])
        self.assertFalse(qualification["material"]["emissionBakedIntoCleanMesh"])
        self.assertFalse(qualification["gameplayOrSpawnActivation"])

    def test_cindermaw_runtime_asset_output_path_fails_closed(self) -> None:
        plan = VALIDATOR.load_json(VALIDATOR.DEFAULT_CINDERMAW_PLAN)
        plan["outputs"]["fbx"] = (
            "unity/Assets/AL/Creatures/cindermaw_salamander.fbx"
        )
        with tempfile.TemporaryDirectory() as directory:
            plan_path = Path(directory) / "unsafe-cindermaw-plan.json"
            plan_path.write_text(json.dumps(plan), encoding="utf-8")
            report = VALIDATOR.validate_slice(
                plan_path=plan_path,
                schema_path=VALIDATOR.DEFAULT_CINDERMAW_SCHEMA,
                qualification_path=VALIDATOR.DEFAULT_CINDERMAW_QUALIFICATION,
            )

        self.assertEqual("FAIL", report["overall"])
        self.assertIn("RuntimePathForbidden:outputs.fbx", report["issues"])

    def test_cindermaw_missing_heat_fin_socket_fails_closed(self) -> None:
        qualification = VALIDATOR.load_json(VALIDATOR.DEFAULT_CINDERMAW_QUALIFICATION)
        qualification["rig"]["boneNames"] = [
            name
            for name in qualification["rig"]["boneNames"]
            if name != "socket_vfx_fin_heat"
        ]
        with tempfile.TemporaryDirectory() as directory:
            qualification_path = Path(directory) / "missing-fin-socket.json"
            qualification_path.write_text(
                json.dumps(qualification),
                encoding="utf-8",
            )
            report = VALIDATOR.validate_slice(
                plan_path=VALIDATOR.DEFAULT_CINDERMAW_PLAN,
                schema_path=VALIDATOR.DEFAULT_CINDERMAW_SCHEMA,
                qualification_path=qualification_path,
            )

        self.assertEqual("FAIL", report["overall"])
        self.assertIn("RequiredBonesMissing", report["issues"])


if __name__ == "__main__":
    unittest.main()
