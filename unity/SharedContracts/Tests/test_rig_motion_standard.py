#!/usr/bin/env python3
"""Fail-closed tests for the rig and required-motion production contracts."""

from __future__ import annotations

import copy
import unittest
from pathlib import Path

import rig_motion_standard as contract


class RigMotionStandardTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repo_root = Path(__file__).resolve().parents[3]
        cls.standard = contract.load_json(cls.repo_root / contract.STANDARD_PATH)
        cls.manifest = contract.load_json(cls.repo_root / contract.MANIFEST_PATH)
        cls.template = contract.load_json(cls.repo_root / contract.TEMPLATE_PATH)

    def validate(
        self,
        standard: dict | None = None,
        manifest: dict | None = None,
        template: dict | None = None,
    ):
        return contract.validate_contracts(
            self.repo_root,
            copy.deepcopy(self.standard) if standard is None else standard,
            copy.deepcopy(self.manifest) if manifest is None else manifest,
            copy.deepcopy(self.template) if template is None else template,
        )

    def assert_rejected(
        self,
        token: str,
        standard: dict | None = None,
        manifest: dict | None = None,
        template: dict | None = None,
    ) -> None:
        with self.assertRaises(contract.RigMotionValidationError) as caught:
            self.validate(standard, manifest, template)
        self.assertIn(token, str(caught.exception))

    def test_committed_contracts_validate_with_zero_acceptance_gaps(self) -> None:
        evidence = self.validate()
        self.assertEqual(3, evidence["representativeCount"])
        self.assertEqual(10, evidence["skillPhaseCount"])
        self.assertEqual(0, evidence["undefinedRequiredMotionKeyCount"])
        self.assertEqual(0, evidence["unclassifiedRepresentativeRequirementCount"])
        self.assertEqual(0, evidence["duplicateIdCount"])
        self.assertEqual(0, evidence["invalidReferenceCount"])
        self.assertEqual(3, evidence["templateVariants"])
        self.assertGreaterEqual(evidence["motionKeys"], 40)
        self.assertGreaterEqual(evidence["classifiedRequirements"], 80)

    def test_schema_rejects_undeclared_fields(self) -> None:
        standard = copy.deepcopy(self.standard)
        standard["importOrder"] = "first_wins"
        self.assert_rejected("SchemaViolation", standard=standard)
        self.assert_rejected("SchemaViolation", standard={})
        self.assert_rejected("SchemaViolation", manifest={})

    def test_duplicate_identifiers_fail_closed(self) -> None:
        standard = copy.deepcopy(self.standard)
        standard["qualityBudgets"][1]["id"] = standard["qualityBudgets"][0]["id"]
        self.assert_rejected("DuplicateId", standard=standard)

    def test_skeleton_requires_non_deforming_motion_root_below_root(self) -> None:
        standard = copy.deepcopy(self.standard)
        skeleton = standard["skeletonProfiles"][0]
        motion_root = next(
            bone for bone in skeleton["bones"] if bone["name"] == "motion_root"
        )
        motion_root["deform"] = True
        self.assert_rejected("MalformedMotionRoot", standard=standard)

    def test_missing_parent_bone_fails_closed(self) -> None:
        standard = copy.deepcopy(self.standard)
        standard["skeletonProfiles"][1]["bones"][3]["parent"] = "missing_chain"
        self.assert_rejected("MissingBoneParent", standard=standard)

    def test_unknown_motion_key_in_required_set_fails_closed(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        manifest["requiredSets"][0]["requiredMotionKeys"].append("combat.unknown")
        self.assert_rejected("UndefinedRequiredMotion", manifest=manifest)

    def test_missing_representative_classification_fails_closed(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        champion = next(
            row
            for row in manifest["representativeCoverage"]
            if row["representativeProfileId"]
            == "rmc_representative_champion_vanguard_v001"
        )
        champion["requirements"] = champion["requirements"][1:]
        self.assert_rejected("UnclassifiedRequirement", manifest=manifest)

    def test_qualified_clip_requires_both_signatures(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        unsigned = next(
            row
            for row in manifest["clipCandidates"]
            if row["skeletonSignature"] is None or row["clipSignature"] is None
        )
        unsigned["qualificationState"] = "qualified"
        self.assert_rejected("UnsignedQualifiedClip", manifest=manifest)

    def test_event_payloads_require_ordered_action_identity_fields(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        event = manifest["eventDefinitions"][0]
        event["payloadFields"] = [
            field for field in event["payloadFields"] if field["name"] != "eventOrdinal"
        ]
        self.assert_rejected("MissingCommonEventPayload", manifest=manifest)

        manifest = copy.deepcopy(self.manifest)
        manifest["skillPhases"][0]["entryEvent"] = "al.motion.missing"
        self.assert_rejected("SchemaViolation", manifest=manifest)

    def test_representative_set_binding_and_record_order_are_deterministic(
        self,
    ) -> None:
        manifest = copy.deepcopy(self.manifest)
        champion = next(
            row
            for row in manifest["representativeCoverage"]
            if row["representativeProfileId"]
            == "rmc_representative_champion_vanguard_v001"
        )
        champion["requiredSetId"] = "rmc_set_npc_complete_v001"
        self.assert_rejected("RepresentativeSetMismatch", manifest=manifest)

        standard = copy.deepcopy(self.standard)
        standard["qualityBudgets"].reverse()
        self.assert_rejected("NonDeterministicOrdering", standard=standard)

    def test_motion_fallback_must_apply_to_every_declared_subject_kind(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        special = next(
            row for row in manifest["motionKeys"] if row["key"] == "attack.special"
        )
        special["fallbackKey"] = "combat.defend"
        self.assert_rejected("InapplicableFallback", manifest=manifest)

    def test_all_declared_skill_phase_sets_must_include_all_ten_phases(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        monster = next(
            row
            for row in manifest["requiredSets"]
            if row["id"] == "rmc_set_monster_complete_v001"
        )
        monster["conditionalMotionKeys"].remove("skill.cancellation")
        self.assert_rejected("IncompleteSkillPhaseSet", manifest=manifest)

    def test_profile_compatibility_layers_and_bind_overrides_fail_closed(self) -> None:
        standard = copy.deepcopy(self.standard)
        slag = next(
            row
            for row in standard["representativeProfiles"]
            if row["id"] == "rmc_representative_beast_slagwhistle_v001"
        )
        slag["retargetProfileId"] = "rmc_retarget_generic_exact_v001"
        self.assert_rejected("RetargetBindMismatch", standard=standard)

        standard = copy.deepcopy(self.standard)
        monster_layers = next(
            row
            for row in standard["layerProfiles"]
            if row["id"] == "rmc_layers_monster_mobile_v001"
        )
        monster_layers["layers"][0]["maskPaths"] = ["root/motion_root/not_a_bone"]
        self.assert_rejected("InvalidLayerMaskPath", standard=standard)

        standard = copy.deepcopy(self.standard)
        monster_layers = next(
            row
            for row in standard["layerProfiles"]
            if row["id"] == "rmc_layers_monster_mobile_v001"
        )
        monster_layers["subjectKind"] = "beast"
        self.assert_rejected("LayerCoverageMismatch", standard=standard)

    def test_impossible_budget_relationships_fail_closed(self) -> None:
        standard = copy.deepcopy(self.standard)
        budget = standard["qualityBudgets"][0]
        budget["skinning"]["maximumAnimatedTransforms"] = 1
        self.assert_rejected("ImpossibleSkinningBudget", standard=standard)

        standard = copy.deepcopy(self.standard)
        budget = standard["qualityBudgets"][0]
        budget["animation"]["minimumSampleRateHz"] = 60
        budget["animation"]["maximumSampleRateHz"] = 30
        self.assert_rejected("ImpossibleSampleRateBudget", standard=standard)

    def test_binding_template_authors_static_metadata_not_runtime_identity(
        self,
    ) -> None:
        template = copy.deepcopy(self.template)
        event = template["variants"][0]["binding"]["clipBindings"][0]["events"][0]
        event.pop("staticPayload")
        event["actionSequence"] = 0
        self.assert_rejected("TemplateEventMismatch", template=template)

    def test_budget_drift_and_loose_contact_limits_fail_closed(self) -> None:
        standard = copy.deepcopy(self.standard)
        standard["qualityBudgets"][0]["topology"]["maximumLod0Triangles"] += 1
        self.assert_rejected("BudgetDrift", standard=standard)

        standard = copy.deepcopy(self.standard)
        standard["qualityBudgets"][0]["contacts"][
            "maximumPlantedHorizontalDriftMeters"
        ] = 0.03
        self.assert_rejected("SchemaViolation", standard=standard)

    def test_slagwhistle_authorization_is_source_bounded(self) -> None:
        manifest = copy.deepcopy(self.manifest)
        slag_set = next(
            row
            for row in manifest["requiredSets"]
            if row["id"] == "rmc_set_slagwhistle_source_bounded_v001"
        )
        slag_set["requiredMotionKeys"].append("attack.basic")
        self.assert_rejected("SlagwhistleAuthorizationDrift", manifest=manifest)

        manifest = copy.deepcopy(self.manifest)
        manifest["motionKeys"].append(
            {
                "id": "rmc_motion_locomotion_burrow_v001",
                "key": "locomotion.burrow",
                "category": "locomotion",
                "applicableKinds": ["beast"],
                "loopPolicy": "must_loop",
                "defaultRootPolicyId": "rmc_root_locomotion_authored_v001",
                "contactRequirement": "foot_or_limb",
                "requiredEventNames": [
                    "al.motion.contact_begin",
                    "al.motion.contact_end",
                ],
                "fallbackKey": "locomotion.crawl",
            }
        )
        self.assert_rejected("burrow motion is not authorized", manifest=manifest)

        manifest = copy.deepcopy(self.manifest)
        unauthorized_clip = copy.deepcopy(manifest["clipCandidates"][0])
        unauthorized_clip["id"] = "rmc_clip_zz_slagwhistle_attack_v001"
        unauthorized_clip["representativeProfileId"] = (
            "rmc_representative_beast_slagwhistle_v001"
        )
        unauthorized_clip["skeletonProfileId"] = (
            "rmc_skeleton_nonhumanoid_grounded_v001"
        )
        unauthorized_clip["motionKey"] = "attack.basic"
        manifest["clipCandidates"].append(unauthorized_clip)
        self.assert_rejected("clip candidates exceed authorization", manifest=manifest)

    def test_representative_and_clip_source_paths_are_real(self) -> None:
        standard = copy.deepcopy(self.standard)
        standard["representativeProfiles"][0]["sourcePath"] = "missing/source.blend"
        self.assert_rejected("MissingRepresentativeSource", standard=standard)

        manifest = copy.deepcopy(self.manifest)
        manifest["clipCandidates"][0]["sourcePath"] = "missing/source.fbx"
        self.assert_rejected("MissingClipSource", manifest=manifest)

    def test_every_subject_floor_and_skill_phase_resolves_to_declared_motion(
        self,
    ) -> None:
        declared = {row["key"] for row in self.manifest["motionKeys"]}
        for required_set in self.manifest["requiredSets"]:
            with self.subTest(required_set=required_set["id"]):
                self.assertTrue(
                    set(required_set["requiredMotionKeys"]).issubset(declared)
                )
                self.assertTrue(
                    set(required_set["conditionalMotionKeys"]).issubset(declared)
                )
        self.assertEqual(
            {
                "skill.anticipation",
                "skill.cast",
                "skill.channel_start",
                "skill.channel_loop",
                "skill.commit",
                "skill.release",
                "skill.impact",
                "skill.recovery",
                "skill.interruption",
                "skill.cancellation",
            },
            {key for key in declared if key.startswith("skill.")},
        )


if __name__ == "__main__":
    unittest.main()
