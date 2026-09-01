#!/usr/bin/env python3
"""Fail-closed coverage for the integrated four-realm production taxonomy."""

from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path
from unittest import mock

from jsonschema import Draft202012Validator, FormatChecker

import four_realm_production_taxonomy as integrated


CATALOG_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_four_realm_production_taxonomy.json"
)
SCHEMA_PATH = Path(
    "unity/SharedContracts/Schemas/al-four-realm-production-taxonomy.schema.json"
)


class FourRealmProductionTaxonomyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repo_root = Path(__file__).resolve().parents[3]
        cls.catalog_path = cls.repo_root / CATALOG_PATH
        cls.schema_path = cls.repo_root / SCHEMA_PATH

    def load_catalog(self) -> dict:
        return json.loads(self.catalog_path.read_text(encoding="utf-8"))

    def assert_validation_failure(self, catalog: dict, token: str) -> None:
        with self.assertRaises(integrated.FourRealmTaxonomyValidationError) as caught:
            integrated.validate_taxonomy(self.repo_root, catalog)
        self.assertIn(token, str(caught.exception))

    def test_generated_catalog_is_byte_stable_and_schema_valid(self) -> None:
        rendered = integrated.render_taxonomy(self.repo_root)
        self.assertEqual(rendered, self.catalog_path.read_text(encoding="utf-8"))
        schema = json.loads(self.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(schema)
        self.assertEqual(
            [],
            list(
                Draft202012Validator(
                    schema, format_checker=FormatChecker()
                ).iter_errors(json.loads(rendered))
            ),
        )

    def test_source_hashes_are_stable_across_checkout_line_endings(self) -> None:
        baseline = integrated.render_taxonomy(self.repo_root)
        original_read_bytes = Path.read_bytes
        source_paths = {
            (self.repo_root / path).resolve() for path in integrated.SOURCE_PATHS.values()
        }

        def read_with_crlf(path: Path) -> bytes:
            data = original_read_bytes(path)
            if path.resolve() in source_paths:
                return data.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
            return data

        with mock.patch.object(Path, "read_bytes", read_with_crlf):
            self.assertEqual(baseline, integrated.render_taxonomy(self.repo_root))

    def test_progression_provenance_is_pinned_to_the_merged_parent_source(self) -> None:
        expected_commit = "91c2bb4e4af37c8763253db373128a2c01da1563"
        expected_sha256 = "763ff7d983b8eda78cefcad4f6e71cf4f95e67e1bfe1a81e4f0efbe4efbda40a"
        for realm in ("eldergrove", "umbral"):
            source = integrated.realm_contract.load_json(
                self.repo_root / integrated.SOURCE_PATHS[realm]
            )
            record = next(
                row
                for row in source["provenance"]
                if row["id"]
                == f"rct_{realm}_provenance_approved_class_progression_v001"
            )
            with self.subTest(realm=realm):
                self.assertEqual(f"git commit {expected_commit}", record["toolVersion"])
                self.assertEqual(expected_sha256, record["sha256"])
                self.assertIn(f"sourceCommit={expected_commit}", record["notes"])
                self.assertIn(f"sourceBlobSha256={expected_sha256}", record["notes"])

    def test_master_matrices_cover_all_four_realms_without_acceptance_gaps(self) -> None:
        catalog = self.load_catalog()
        evidence = integrated.validate_taxonomy(self.repo_root, catalog)
        self.assertEqual(
            ["crownlands", "eldergrove", "stonehold", "umbral"],
            catalog["normalization"]["canonicalRealmIds"],
        )
        self.assertEqual(72, evidence["rosterRows"])
        self.assertEqual(72, evidence["rigRows"])
        self.assertEqual(68, evidence["motionRows"])
        self.assertEqual(412, evidence["skillMotionRows"])
        self.assertEqual(412, evidence["skillVfxRows"])
        self.assertEqual(72, evidence["platformRows"])
        self.assertEqual(26, evidence["budgetRows"])
        self.assertEqual(0, evidence["orphanSkillCount"])
        self.assertEqual(0, evidence["missingRequiredCellCount"])
        self.assertEqual(0, evidence["unriggableConceptCount"])
        self.assertEqual(0, evidence["undocumentedProvenanceCount"])
        self.assertEqual(0, evidence["unbudgetedMobileCostCount"])
        self.assertEqual(0, evidence["incompatibleDuplicateCount"])

    def test_sharing_is_explicit_without_claiming_cross_realm_asset_compatibility(self) -> None:
        catalog = self.load_catalog()
        sharing = {row["assetClass"]: row for row in catalog["sharingMatrix"]}
        self.assertEqual(
            {
                "body_modules",
                "equipment_slots",
                "skeletons_rigs",
                "animations",
                "skill_identities",
                "vfx",
            },
            set(sharing),
        )
        self.assertEqual(
            "contract_only",
            sharing["animations"]["sharingScope"],
        )
        self.assertEqual(
            "gameplay_identity_only",
            sharing["skill_identities"]["sharingScope"],
        )
        for asset_class in ("body_modules", "equipment_slots", "skeletons_rigs", "vfx"):
            self.assertEqual(
                "realm_scoped_no_cross_realm_compatibility_claim",
                sharing[asset_class]["sharingScope"],
            )

    def test_every_champion_has_distinct_charged_heavy_and_skill_use_motions(self) -> None:
        catalog = self.load_catalog()
        champions = [
            row
            for row in catalog["matrices"]["motionCoverage"]
            if row["entityKind"] == "champion"
        ]
        self.assertEqual(16, len(champions))
        required = {"attack.charged", "attack.heavy", "skill.use"}
        for row in champions:
            self.assertTrue(
                required.issubset(row["requiredMotionKeys"]), row["entityId"]
            )
            self.assertTrue(
                required.issubset(row["presentMotionKeys"]), row["entityId"]
            )

    def test_npc_and_creature_motion_floors_include_role_habitat_and_boss_needs(self) -> None:
        catalog = self.load_catalog()
        roster = {
            row["entityId"]: row for row in catalog["matrices"]["rosterCoverage"]
        }
        motions = catalog["matrices"]["motionCoverage"]
        npc_required = {
            "social.talk",
            "social.gesture",
            "daily.sit",
            "daily.sleep",
            "daily.work",
            "daily.carry",
            "daily.gather",
            "daily.trade",
            "daily.craft",
            "reaction.react",
            "reaction.flee",
            "combat.defend",
        }
        creature_required = {
            "idle.neutral",
            "idle.variant",
            "locomotion.turn",
            "attack.basic",
            "attack.special",
            "reaction.hit",
            "reaction.stagger",
            "defeat",
        }
        for row in motions:
            required = set(row["requiredMotionKeys"])
            present = set(row["presentMotionKeys"])
            if row["entityKind"] == "npc":
                self.assertTrue(npc_required.issubset(required), row["entityId"])
            if row["entityKind"] in {"beast", "monster"}:
                self.assertTrue(creature_required.issubset(required), row["entityId"])
            self.assertTrue(required.issubset(present), row["entityId"])
            if roster[row["entityId"]]["rank"] == "boss":
                self.assertTrue(
                    {"boss.enter", "boss.phase", "boss.transition"}.issubset(required),
                    row["entityId"],
                )

    def test_mobile_treatment_names_every_cost_dimension_and_expensive_vfx_metric(self) -> None:
        catalog = self.load_catalog()
        expected_dimensions = {
            "geometry",
            "materials",
            "textures",
            "bones",
            "physics",
            "animation",
            "vfx",
            "colliders",
            "hitboxes",
        }
        for row in catalog["matrices"]["platformVariants"]:
            self.assertEqual(expected_dimensions, set(row["costDimensionTreatments"]))
            self.assertTrue(
                all(row["costDimensionTreatments"].values()), row["entityId"]
            )
        mobile_budgets = [
            row
            for row in catalog["matrices"]["budgets"]
            if row["platformTier"] == "mobile_floor"
        ]
        required_paths = {
            "geometry.lod0Triangles",
            "materials.materialSlots",
            "textures.residentTextureMemory",
            "bones.deformingBones",
            "physics.simulatedBones",
            "animation.compressedMemoryTarget",
            "vfx.liveParticles",
            "vfx.overdrawCoveragePercent",
            "vfx.concurrentEffects",
        }
        for row in mobile_budgets:
            self.assertTrue(
                required_paths.issubset(
                    {metric["path"] for metric in row["metrics"]}
                ),
                row["budgetProfileId"],
            )

    def test_owner_questions_are_deduplicated_and_remain_held(self) -> None:
        catalog = self.load_catalog()
        packets = catalog["ownerDecisionPackets"]
        self.assertEqual(3, len(packets))
        self.assertEqual(
            {"identity", "technical_mobile", "motion_effect"},
            {packet["decisionArea"] for packet in packets},
        )
        for packet in packets:
            self.assertEqual("PENDING", packet["ownerStatus"])
            self.assertEqual(
                ["APPROVE", "REVISE", "REJECT"],
                [alternative["response"] for alternative in packet["alternatives"]],
            )
            self.assertTrue(packet["affectedCatalogIds"])
            self.assertTrue(packet["affectedSubjectIds"])
            self.assertTrue(packet["sourceDecisionIds"])
        self.assertEqual("preparation_held", catalog["authority"]["status"])
        self.assertEqual("held", catalog["authority"]["generationState"])
        self.assertEqual("held", catalog["authority"]["activationState"])

    def test_missing_skill_motion_cell_fails_closed(self) -> None:
        catalog = self.load_catalog()
        del catalog["matrices"]["skillToMotion"][0]["phases"]["cast"]
        self.assert_validation_failure(catalog, "MissingSkillMotionCell")

    def test_unriggable_concept_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["matrices"]["rigFeasibility"][0]["rigFamilyIds"] = []
        catalog["matrices"]["rigFeasibility"][0]["feasible"] = False
        self.assert_validation_failure(catalog, "UnriggableConcept")

    def test_unbudgeted_mobile_cost_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["matrices"]["platformVariants"][0]["mobileFloorBudgetProfileIds"] = []
        self.assert_validation_failure(catalog, "UnbudgetedMobileCost")

    def test_undocumented_provenance_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["sourceCatalogs"][0]["sha256"] = ""
        self.assert_validation_failure(catalog, "UndocumentedProvenance")

    def test_orphan_skill_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["matrices"]["skillToVfx"].pop()
        self.assert_validation_failure(catalog, "OrphanSkill")

    def test_incompatible_duplicate_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["duplicateAudit"][0]["classification"] = "incompatible"
        self.assert_validation_failure(catalog, "IncompatibleDuplicate")

    def test_all_integrated_claims_must_match_the_source_derived_projection(self) -> None:
        def mutate_motion(catalog: dict) -> None:
            catalog["matrices"]["motionCoverage"][0]["requiredMotionKeys"] = []

        def mutate_skill(catalog: dict) -> None:
            rows = catalog["matrices"]["skillToMotion"]
            cell = next(
                cell
                for row in rows
                for cell in row["phases"].values()
                if cell["state"] == "required"
            )
            cell.update(
                state="not_applicable",
                recordIds=[],
                rationale="Tampered self-reported disposition.",
            )

        def mutate_platform(catalog: dict) -> None:
            row = catalog["matrices"]["platformVariants"][0]
            row["costDimensionTreatments"]["geometry"] = "Tampered treatment."

        def mutate_duplicate(catalog: dict) -> None:
            catalog["duplicateAudit"][0]["resolution"] = "Tampered resolution."

        def mutate_owner_packet(catalog: dict) -> None:
            catalog["ownerDecisionPackets"][0]["question"] = "Tampered question?"

        def mutate_provenance(catalog: dict) -> None:
            catalog["matrices"]["provenance"][0]["claimIds"] = []

        def mutate_roster(catalog: dict) -> None:
            catalog["matrices"]["rosterCoverage"][0]["name"] = "Tampered name"

        def mutate_sharing(catalog: dict) -> None:
            catalog["sharingMatrix"][0]["sourceIds"] = []

        def mutate_normalization(catalog: dict) -> None:
            catalog["normalization"]["skillAliases"] = []

        mutations = {
            "motion": mutate_motion,
            "skill": mutate_skill,
            "platform": mutate_platform,
            "duplicate": mutate_duplicate,
            "owner_packet": mutate_owner_packet,
            "provenance": mutate_provenance,
            "roster": mutate_roster,
            "sharing": mutate_sharing,
            "normalization": mutate_normalization,
        }
        for label, mutate in mutations.items():
            with self.subTest(label=label):
                catalog = self.load_catalog()
                mutate(catalog)
                self.assert_validation_failure(catalog, "ProjectionMismatch")


if __name__ == "__main__":
    unittest.main()
