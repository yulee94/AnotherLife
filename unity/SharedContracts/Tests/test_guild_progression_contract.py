#!/usr/bin/env python3
"""Focused fail-closed checks for the Guild progression/research perk contract."""

import json
import pathlib
import unittest

from jsonschema import Draft202012Validator


class GuildProgressionContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = pathlib.Path(__file__).resolve().parents[3]
        cls.schema_path = (
            cls.repo_root
            / "unity"
            / "SharedContracts"
            / "Schemas"
            / "al-guild-progression.schema.json"
        )
        cls.catalog_path = (
            cls.repo_root
            / "unity"
            / "Assets"
            / "AL"
            / "StreamingAssets"
            / "GameData"
            / "al_guild_progression_policy.json"
        )
        cls.fixtures = pathlib.Path(__file__).resolve().parent / "fixtures"
        cls.schema = json.loads(cls.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.validator = Draft202012Validator(cls.schema)

    def test_real_catalog_and_valid_fixture_match_and_validate(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))
        fixture = json.loads(
            (self.fixtures / "valid" / "al-guild-progression.valid.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(fixture, catalog)
        self.assertEqual([], list(self.validator.iter_errors(catalog)))

    def test_every_invalid_fixture_is_rejected(self):
        invalid_paths = sorted(
            (self.fixtures / "invalid").glob("al-guild-progression.invalid.*.json")
        )
        self.assertGreaterEqual(len(invalid_paths), 4)
        for path in invalid_paths:
            with self.subTest(path=path.name):
                instance = json.loads(path.read_text(encoding="utf-8"))
                errors = list(self.validator.iter_errors(instance))
                self.assertEqual(1, len(errors), [error.message for error in errors])

    def test_machine_identity_perk_provenance_and_ineligible_tuning_are_exact(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))

        self.assertEqual("Guild", catalog["machineIdentity"])
        self.assertEqual("Guild", catalog["playerFacingTerms"]["primary"])
        self.assertEqual("Clan", catalog["playerFacingTerms"]["presentationAlias"])
        self.assertEqual("localization_only", catalog["playerFacingTerms"]["aliasScope"])
        self.assertEqual(
            "al_guild_membership_policy", catalog["consumedContracts"]["guildCatalogId"]
        )
        self.assertFalse(catalog["progression"]["levelCapSelected"])
        self.assertFalse(catalog["progression"]["researchTreeSelected"])
        self.assertFalse(catalog["progression"]["costsSelected"])
        self.assertFalse(catalog["progression"]["numericPerkTuningProductionEligible"])
        self.assertEqual("guild_master_only", catalog["progression"]["authority"])
        self.assertEqual("forbidden", catalog["hiddenGlobalMultipliers"])

        perk = catalog["perks"][0]
        self.assertEqual("guild_progression", perk["sourceId"])
        self.assertEqual("guild_member_character_stats", perk["profileId"])
        self.assertTrue(perk["ruleId"])
        self.assertTrue(perk["requiredLevelId"])
        self.assertTrue(perk["requiredResearchId"])
        self.assertEqual("member_character_stats", perk["scope"])
        self.assertEqual("unselected", perk["cap"]["kind"])
        self.assertFalse(perk["cap"]["productionEligible"])
        self.assertEqual("explicit_visible_only", perk["stacking"]["rule"])
        self.assertRegex(perk["sourceHash"], r"^[a-f0-9]{64}$")
        self.assertTrue(perk["statBreakdownToken"])
        self.assertFalse(perk["hiddenGlobalMultiplier"])
        self.assertFalse(perk["productionEligible"])
        self.assertFalse(perk["appliesCombatMutation"])
        self.assertEqual(
            [
                "persistence",
                "network",
                "save",
                "combat",
                "ui",
                "economy",
                "city",
                "raid",
                "oathmark_mint",
                "alliance",
            ],
            catalog["authorityBoundary"]["excludedImplementations"],
        )
        self.assertEqual(
            "production_ineligible", catalog["authorityBoundary"]["numericPerkTuning"]
        )


if __name__ == "__main__":
    unittest.main()
