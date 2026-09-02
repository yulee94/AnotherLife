from __future__ import annotations

import copy
import json
import sys
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import al_motion_roundtrip_contract as contract


class MotionRoundTripContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.standard = cls.load(
            "unity/Assets/AL/StreamingAssets/GameData/al_rig_motion_standard.json"
        )
        cls.required = cls.load(
            "unity/Assets/AL/StreamingAssets/GameData/al_required_motion_manifest.json"
        )
        cls.rig_manifest = cls.load(
            "unity/ArtSource/RigPipeline/al_rig_cleanup_manifest.v1.json"
        )
        cls.motion_catalog = cls.load(
            "unity/ArtSource/MotionLibrary/al_motion_library_catalog.v1.json"
        )
        cls.sidecars = {
            asset["id"]: cls.load(asset["output"]["sidecarPath"])
            for asset in cls.rig_manifest["assets"]
        }
        cls.receipts = {
            asset["id"]: cls.load(asset["output"]["fbxReceiptPath"])
            for asset in cls.rig_manifest["assets"]
        }

    @staticmethod
    def load(relative: str) -> dict:
        return json.loads((REPO_ROOT / relative).read_text(encoding="utf-8"))

    def validate_sources(
        self,
        standard: dict | None = None,
        required: dict | None = None,
        rig_manifest: dict | None = None,
        motion_catalog: dict | None = None,
        sidecars: dict | None = None,
        receipts: dict | None = None,
    ) -> dict:
        return contract.validate_source_artifacts(
            REPO_ROOT,
            copy.deepcopy(self.standard if standard is None else standard),
            copy.deepcopy(self.required if required is None else required),
            copy.deepcopy(self.rig_manifest if rig_manifest is None else rig_manifest),
            copy.deepcopy(self.motion_catalog if motion_catalog is None else motion_catalog),
            copy.deepcopy(self.sidecars if sidecars is None else sidecars),
            copy.deepcopy(self.receipts if receipts is None else receipts),
        )

    def assert_source_rejected(self, token: str, **overrides) -> None:
        with self.assertRaises(contract.MotionRoundTripValidationError) as caught:
            self.validate_sources(**overrides)
        self.assertIn(token, str(caught.exception))

    def test_committed_source_artifacts_cover_three_representatives_and_required_motion(self) -> None:
        evidence = self.validate_sources()
        self.assertEqual(3, evidence["representatives"])
        self.assertEqual(59, evidence["clips"])
        self.assertEqual(100, evidence["bindings"])
        self.assertEqual(0, evidence["catalogGaps"])
        self.assertEqual(10, evidence["skillPhases"])

    def test_duplicate_or_unstable_catalog_identifiers_fail_closed(self) -> None:
        catalog = copy.deepcopy(self.motion_catalog)
        catalog["clips"][1]["id"] = catalog["clips"][0]["id"]
        self.assert_source_rejected("DuplicateCatalogId", motion_catalog=catalog)

        catalog = copy.deepcopy(self.motion_catalog)
        catalog["bindings"][0]["clipId"] = "rmc_clip_unstable_v999"
        self.assert_source_rejected("UnstableCatalogBinding", motion_catalog=catalog)

    def test_unsupported_skeleton_and_invalid_bone_hierarchy_fail_closed(self) -> None:
        manifest = copy.deepcopy(self.rig_manifest)
        manifest["assets"][0]["skeletonProfileId"] = "rmc_skeleton_unknown_v001"
        self.assert_source_rejected("UnsupportedSkeletonProfile", rig_manifest=manifest)

        sidecars = copy.deepcopy(self.sidecars)
        sidecar = sidecars["rmc_cleanup_champion_vanguard_v002"]
        sidecar["skeleton"]["records"][2]["name"] = "Bad Bone"
        self.assert_source_rejected("InvalidBoneName", sidecars=sidecars)

        sidecars = copy.deepcopy(self.sidecars)
        sidecar = sidecars["rmc_cleanup_champion_vanguard_v002"]
        sidecar["skeleton"]["records"][2]["parentPath"] = "root/missing"
        self.assert_source_rejected("InvalidBoneHierarchy", sidecars=sidecars)

    def test_scale_axis_roots_sockets_and_mobile_skinning_fail_closed(self) -> None:
        receipts = copy.deepcopy(self.receipts)
        receipts["rmc_cleanup_champion_vanguard_v002"]["export"]["globalScale"] = 0.01
        self.assert_source_rejected("InvalidExportScale", receipts=receipts)

        receipts = copy.deepcopy(self.receipts)
        receipts["rmc_cleanup_champion_vanguard_v002"]["export"]["axisUp"] = "Z"
        self.assert_source_rejected("InvalidExportAxes", receipts=receipts)

        sidecars = copy.deepcopy(self.sidecars)
        sidecars["rmc_cleanup_champion_vanguard_v002"]["skeleton"]["records"] = [
            row
            for row in sidecars["rmc_cleanup_champion_vanguard_v002"]["skeleton"][
                "records"
            ]
            if row["name"] != "motion_root"
        ]
        self.assert_source_rejected("MissingRequiredRoot", sidecars=sidecars)

        sidecars = copy.deepcopy(self.sidecars)
        sidecar = sidecars["rmc_cleanup_champion_vanguard_v002"]
        sidecar["skeleton"]["records"] = [
            row for row in sidecar["skeleton"]["records"] if row["name"] != "socket_hair"
        ]
        self.assert_source_rejected("MissingRequiredSocket", sidecars=sidecars)

        sidecars = copy.deepcopy(self.sidecars)
        sidecars["rmc_cleanup_champion_vanguard_v002"]["preflight"][
            "maximumInfluencesPerVertex"
        ] = 5
        self.assert_source_rejected("SkinInfluenceBudgetExceeded", sidecars=sidecars)

        sidecars = copy.deepcopy(self.sidecars)
        sidecars["rmc_cleanup_champion_vanguard_v002"]["preflight"][
            "deformingBones"
        ] = 90
        self.assert_source_rejected("DeformingBoneBudgetExceeded", sidecars=sidecars)

    def test_missing_events_hitbox_windows_and_root_policy_fail_closed(self) -> None:
        catalog = copy.deepcopy(self.motion_catalog)
        attack = next(row for row in catalog["clips"] if row["motionKey"] == "attack.basic")
        attack["events"] = [
            event
            for event in attack["events"]
            if event["eventName"] != "al.motion.hitbox.request_begin"
        ]
        self.assert_source_rejected("MissingRequiredEvent", motion_catalog=catalog)

        catalog = copy.deepcopy(self.motion_catalog)
        attack = next(row for row in catalog["clips"] if row["motionKey"] == "attack.basic")
        attack["hitboxWindows"][0]["closeFrame"] = attack["hitboxWindows"][0][
            "openFrame"
        ]
        self.assert_source_rejected("InvalidHitboxWindow", motion_catalog=catalog)

        catalog = copy.deepcopy(self.motion_catalog)
        walk = next(row for row in catalog["clips"] if row["motionKey"] == "locomotion.walk")
        walk["rootTreatment"] = "authored_unbounded"
        self.assert_source_rejected("IncompatibleRootMotion", motion_catalog=catalog)

    def test_missing_required_motions_and_skill_phases_fail_closed(self) -> None:
        catalog = copy.deepcopy(self.motion_catalog)
        catalog["bindings"] = [
            row
            for row in catalog["bindings"]
            if not (
                row["representativeProfileId"]
                == "rmc_representative_champion_vanguard_v001"
                and row["motionKey"] == "skill.recovery"
            )
        ]
        self.assert_source_rejected("MissingRequiredMotion", motion_catalog=catalog)
        self.assert_source_rejected("MissingRequiredSkillPhase", motion_catalog=catalog)

        catalog = copy.deepcopy(self.motion_catalog)
        catalog["bindings"] = [
            row
            for row in catalog["bindings"]
            if row["representativeProfileId"]
            != "rmc_representative_npc_covenant_sentinel_v001"
        ]
        self.assert_source_rejected("MissingRequiredMotion", motion_catalog=catalog)


class UnityRoundTripReportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.standard = json.loads(
            (
                REPO_ROOT
                / "unity/Assets/AL/StreamingAssets/GameData/al_rig_motion_standard.json"
            ).read_text(encoding="utf-8")
        )
        cls.report = cls.valid_report()

    @classmethod
    def valid_report(cls) -> dict:
        rows = []
        definitions = (
            (
                "rmc_representative_champion_vanguard_v001",
                "champion",
                "rmc_skeleton_humanoid_shared_v001",
                "rmc_budget_champion_mobile_floor_v001",
                True,
                39,
                4,
                2.0,
            ),
            (
                "rmc_representative_npc_covenant_sentinel_v001",
                "npc",
                "rmc_skeleton_humanoid_shared_v001",
                "rmc_budget_npc_mobile_floor_v001",
                True,
                41,
                4,
                2.2,
            ),
            (
                "rmc_representative_beast_slagwhistle_v001",
                "beast",
                "rmc_skeleton_nonhumanoid_grounded_v001",
                "rmc_budget_beast_mobile_floor_v001",
                False,
                47,
                4,
                0.6,
            ),
        )
        for profile, kind, skeleton, budget, humanoid, transforms, clips, height in definitions:
            rows.append(
                {
                    "representativeProfileId": profile,
                    "subjectKind": kind,
                    "skeletonProfileId": skeleton,
                    "budgetProfileId": budget,
                    "freshImport": True,
                    "rig": {
                        "avatarValid": True,
                        "isHuman": humanoid,
                        "rootCount": 1,
                        "hasRoot": True,
                        "hasMotionRoot": True,
                        "missingSockets": [],
                        "invalidBoneNames": [],
                        "invalidHierarchyCount": 0,
                        "uniformScale": 1.0,
                        "axisErrorDegrees": 0.0,
                        "heightMeters": height,
                        "deformingBones": 38 if kind == "beast" else 24,
                        "animatedTransforms": transforms,
                        "maximumInfluencesPerVertex": 4,
                        "unweightedVertices": 0,
                    },
                    "animation": {
                        "residentClipCount": clips,
                        "compressedMemoryMiB": 1.0,
                        "compression": "Optimal",
                        "missingMotionKeys": [],
                        "missingEvents": [],
                        "duplicateEvents": 0,
                        "invalidEventOrder": 0,
                        "invalidHitboxWindows": 0,
                        "droppedEvents": 0,
                        "incompatibleRootMotion": 0,
                        "trajectoryErrorMeters": 0.0,
                        "footSlidingMeters": 0.0,
                        "contactDriftMeters": 0.0,
                        "transitionPositionDeltaMeters": 0.0,
                        "transitionRotationDeltaDegrees": 0.0,
                    },
                    "runtime": {
                        "controllerConfigured": True,
                        "graphValid": True,
                        "safePoseLoaded": True,
                        "tPoseDetected": False,
                        "fallbackPassed": True,
                        "transitionPassed": True,
                        "recoveryPassed": True,
                        "attachmentsPassed": True,
                    },
                }
            )
        return {
            "schemaVersion": 1,
            "pipelineId": "rmc_pipeline_unity_roundtrip_acceptance_v001",
            "unityVersion": "6000.3.22f1",
            "status": "passed",
            "scenePath": "Assets/AL/Generated/MotionRoundTrip/MotionRoundTripAcceptance.unity",
            "representatives": rows,
        }

    def validate(self, report: dict | None = None) -> dict:
        return contract.validate_unity_report(
            copy.deepcopy(self.standard),
            copy.deepcopy(self.report if report is None else report),
        )

    def assert_rejected(self, token: str, report: dict) -> None:
        with self.assertRaises(contract.MotionRoundTripValidationError) as caught:
            self.validate(report)
        self.assertIn(token, str(caught.exception))

    def test_valid_report_passes_all_three_representatives(self) -> None:
        evidence = self.validate()
        self.assertEqual(3, evidence["representatives"])
        self.assertEqual(0, evidence["acceptanceFailures"])

    def test_intentionally_incomplete_report_fails_closed(self) -> None:
        report = copy.deepcopy(self.report)
        report["representatives"].pop()
        self.assert_rejected("RepresentativeCoverageMismatch", report)

        report = copy.deepcopy(self.report)
        report["representatives"][0]["animation"]["missingMotionKeys"] = [
            "skill.recovery"
        ]
        self.assert_rejected("MissingRequiredMotion", report)

    def test_mobile_animation_and_skinning_budget_overages_fail_closed(self) -> None:
        report = copy.deepcopy(self.report)
        report["representatives"][0]["animation"]["compressedMemoryMiB"] = 13.0
        self.assert_rejected("AnimationMemoryBudgetExceeded", report)

        report = copy.deepcopy(self.report)
        report["representatives"][1]["animation"]["residentClipCount"] = 33
        self.assert_rejected("ResidentClipBudgetExceeded", report)

        report = copy.deepcopy(self.report)
        report["representatives"][2]["rig"]["maximumInfluencesPerVertex"] = 5
        self.assert_rejected("SkinInfluenceBudgetExceeded", report)

    def test_quality_runtime_event_attachment_and_transition_failures_are_rejected(self) -> None:
        mutations = (
            ("FootSlidingExceeded", "animation", "footSlidingMeters", 0.03),
            ("ContactDriftExceeded", "animation", "contactDriftMeters", 0.03),
            (
                "TransitionDiscontinuity",
                "animation",
                "transitionRotationDeltaDegrees",
                7.0,
            ),
            ("InvalidEventOrder", "animation", "invalidEventOrder", 1),
            ("InvalidHitboxWindow", "animation", "invalidHitboxWindows", 1),
            ("TposeDetected", "runtime", "tPoseDetected", True),
            ("BrokenAttachment", "runtime", "attachmentsPassed", False),
            ("RecoveryFailure", "runtime", "recoveryPassed", False),
        )
        for token, section, field, value in mutations:
            with self.subTest(token=token):
                report = copy.deepcopy(self.report)
                report["representatives"][0][section][field] = value
                self.assert_rejected(token, report)


if __name__ == "__main__":
    unittest.main()
