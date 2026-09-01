#!/usr/bin/env python3
"""Coverage tests for the complete Umbral realm taxonomy catalog."""

from __future__ import annotations

import json
import unittest
from collections import defaultdict
from pathlib import Path

import build_umbral_realm_catalog as umbral_builder
import realm_character_taxonomy as contract


CATALOG_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_umbral_realm_character_taxonomy.json"
)
EXPECTED_BEASTS = {
    "Ashstep Bounder",
    "Cinderplate Scarab",
    "Graveglass Sheller",
    "Sootsail Carrioner",
}
EXPECTED_MONSTERS = {
    "Ashvein Triarch",
    "Cindermaw Salamander",
    "Gravewing Siphon",
    "Veilspine Widow",
    "Void Seraph Realm Dragon (Unresolved Reference)",
}
EXPECTED_SPECIAL_SKILLS = {
    "tdf_boss_umbral_ashvein_triarch:coordinated_neck_recoil",
    "tdf_elite_umbral_cindermaw_salamander:sudden_jaw_surge",
    "tdf_elite_umbral_gravewing_siphon:claw_brace",
    "tdf_elite_umbral_veilspine_widow:controlled_drop",
}


class UmbralRealmTaxonomyCatalogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repo_root = Path(__file__).resolve().parents[3]
        cls.catalog_path = cls.repo_root / CATALOG_PATH
        cls.catalog = json.loads(cls.catalog_path.read_text(encoding="utf-8"))

    def test_catalog_is_deterministic_and_semantically_complete(self) -> None:
        self.assertEqual(
            umbral_builder.render_catalog(self.repo_root),
            self.catalog_path.read_text(encoding="utf-8"),
        )
        evidence = contract.validate_path(self.repo_root, self.catalog_path)
        self.assertEqual(106, evidence["skillCount"])
        self.assertEqual(106, evidence["traceabilityRows"])
        self.assertEqual(0, evidence["orphanReferenceCount"])
        self.assertEqual(0, evidence["missingMotionCount"])

    def test_roster_covers_race_npc_champion_and_ecosystem_families(self) -> None:
        self.assertEqual(
            ["Umbral Dark Elves"],
            [row["displayName"] for row in self.catalog["playableRaces"]],
        )
        self.assertEqual(
            {"civilian", "service", "quest", "combat"},
            {row["role"] for row in self.catalog["npcArchetypes"]},
        )
        self.assertEqual(
            {"assassin", "mage", "ranger", "warrior"},
            {
                row["id"].removeprefix("rct_umbral_champion_").removesuffix("_v001")
                for row in self.catalog["championFamilies"]
            },
        )
        assassin = next(
            row
            for row in self.catalog["championFamilies"]
            if row["id"] == "rct_umbral_champion_assassin_v001"
        )
        self.assertIn("Vex Nocturne", assassin["displayName"])
        self.assertIn(
            "champion_runtime:champion_umbral_shadowblade",
            assassin["classSourceIds"],
        )
        self.assertTrue(
            {
                "rct_umbral_equipment_twinblades_v001",
                "rct_umbral_equipment_shroud_v001",
            }.issubset(assassin["equipmentModuleIds"])
        )
        self.assertEqual(
            EXPECTED_BEASTS,
            {row["displayName"] for row in self.catalog["beastFamilies"]},
        )
        self.assertEqual(
            EXPECTED_MONSTERS,
            {row["displayName"] for row in self.catalog["monsterFamilies"]},
        )
        self.assertEqual("preparation_held", self.catalog["authority"]["status"])
        self.assertEqual("held", self.catalog["gatePolicy"]["generationState"])
        self.assertEqual("held", self.catalog["gatePolicy"]["activationState"])

    def test_provenance_records_committed_source_and_blob_lineage(self) -> None:
        provenance = {row["id"]: row for row in self.catalog["provenance"]}
        for row in provenance.values():
            commit = row["toolVersion"].removeprefix("git commit ")
            self.assertEqual(40, len(commit), row["id"])
            self.assertTrue(all(character in "0123456789abcdef" for character in commit))
            self.assertIn(f"sourceCommit={commit}", row["notes"])
            self.assertIn(f"sourceBlobSha256={row['sha256']}", row["notes"])
            self.assertEqual(
                umbral_builder.source_blob_sha256(
                    self.repo_root,
                    commit,
                    row["sourceRef"],
                ),
                row["sha256"],
                row["id"],
            )
        umbral_authoring = provenance["rct_umbral_provenance_umbral_authoring_v001"]
        self.assertIn("lineageSourceCommit=", umbral_authoring["notes"])
        self.assertIn("lineageSourceBlobSha256=", umbral_authoring["notes"])

    def test_all_approved_progression_and_documented_umbral_skills_are_present(self) -> None:
        progression = [
            row
            for row in self.catalog["skills"]
            if row["externalSourceId"].startswith("anotherlife.class_progression.")
        ]
        self.assertEqual(96, len(progression))
        progression_by_family: dict[str, list[dict]] = defaultdict(list)
        for row in progression:
            progression_by_family[row["externalSourceId"].split(".")[2]].append(row)
            self.assertEqual("approved_fact", row["authority"]["status"])
        self.assertEqual(
            {"assassin", "mage", "ranger", "warrior"},
            set(progression_by_family),
        )
        self.assertTrue(
            all(len(rows) == 24 for rows in progression_by_family.values())
        )
        external_ids = {row["externalSourceId"] for row in self.catalog["skills"]}
        self.assertTrue(
            {
                "realm_strike",
                "renewing_guard",
                "warmaster_breaker",
                "warzone_burst",
                "skill_shadowstep",
                "skill_umbral_execute",
            }.issubset(external_ids)
        )
        self.assertTrue(EXPECTED_SPECIAL_SKILLS.issubset(external_ids))

    def test_every_entity_has_rig_budget_platform_and_owner_gate_coverage(self) -> None:
        variants = {row["id"]: row for row in self.catalog["platformVariants"]}
        platforms = {row["id"]: row["tier"] for row in self.catalog["platformProfiles"]}
        packets = {row["id"]: row for row in self.catalog["decisionPackets"]}
        dimension_names = {
            "animationPersonality": "animation_personality",
            "magicalGrammar": "magical_grammar",
        }
        for section in (
            "playableRaces",
            "npcArchetypes",
            "championFamilies",
            "beastFamilies",
            "monsterFamilies",
        ):
            for entity in self.catalog[section]:
                self.assertTrue(entity["rigFamilyIds"], entity["id"])
                self.assertTrue(entity["budgetProfileIds"], entity["id"])
                self.assertEqual(
                    {"mobile_floor", "mobile_high", "pc_high"},
                    {
                        platforms[variants[variant_id]["platformProfileId"]]
                        for variant_id in entity["platformVariantIds"]
                    },
                    entity["id"],
                )
                for name, decision in entity["creativeDecisions"].items():
                    self.assertEqual("owner_decision_required", decision["state"])
                    self.assertTrue(decision["decisionPacketIds"])
                    expected_dimension = dimension_names.get(name, name)
                    for packet_id in decision["decisionPacketIds"]:
                        self.assertIn(
                            expected_dimension,
                            packets[packet_id]["decisionDimensions"],
                            f"{entity['id']}.{name}",
                        )

    def test_every_skill_has_explicit_motion_and_vfx_disposition(self) -> None:
        traces = {row["skillId"]: row for row in self.catalog["skillTraceability"]}
        self.assertEqual(
            {row["id"] for row in self.catalog["skills"]},
            set(traces),
        )
        for skill_id, trace in traces.items():
            self.assertEqual(set(contract.SKILL_PHASES), set(trace["motionPhases"]), skill_id)
            self.assertEqual(set(contract.VFX_CATEGORIES), set(trace["effects"]), skill_id)
            for requirement in [
                *trace["motionPhases"].values(),
                *trace["effects"].values(),
            ]:
                if requirement["state"] == "required":
                    self.assertTrue(requirement["recordIds"], skill_id)
                else:
                    self.assertEqual([], requirement["recordIds"], skill_id)
                    self.assertTrue(requirement["rationale"].strip(), skill_id)


if __name__ == "__main__":
    unittest.main()
