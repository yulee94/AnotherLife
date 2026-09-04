#!/usr/bin/env python3
"""Focused fail-closed checks for the Guild city season / ownership contract."""

import json
import pathlib
import unittest

from jsonschema import Draft202012Validator


class GuildCitySeasonContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = pathlib.Path(__file__).resolve().parents[3]
        cls.schema_path = (
            cls.repo_root
            / "unity"
            / "SharedContracts"
            / "Schemas"
            / "al-guild-city-season.schema.json"
        )
        cls.catalog_path = (
            cls.repo_root
            / "unity"
            / "Assets"
            / "AL"
            / "StreamingAssets"
            / "GameData"
            / "al_guild_city_season_policy.json"
        )
        cls.fixtures = pathlib.Path(__file__).resolve().parent / "fixtures"
        cls.schema = json.loads(cls.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.validator = Draft202012Validator(cls.schema)

    def test_real_catalog_and_valid_fixture_match_and_validate(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))
        fixture = json.loads(
            (self.fixtures / "valid" / "al-guild-city-season.valid.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(fixture, catalog)
        self.assertEqual([], list(self.validator.iter_errors(catalog)))

    def test_every_invalid_fixture_is_rejected(self):
        invalid_paths = sorted(
            (self.fixtures / "invalid").glob("al-guild-city-season.invalid.*.json")
        )
        self.assertGreaterEqual(len(invalid_paths), 4)
        for path in invalid_paths:
            with self.subTest(path=path.name):
                instance = json.loads(path.read_text(encoding="utf-8"))
                errors = list(self.validator.iter_errors(instance))
                self.assertEqual(1, len(errors), [error.message for error in errors])

    def test_week_slots_banner_benefits_and_authority_separation_are_exact(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))

        self.assertEqual("Guild", catalog["machineIdentity"])
        self.assertEqual("Guild", catalog["playerFacingTerms"]["primary"])
        self.assertEqual("Clan", catalog["playerFacingTerms"]["presentationAlias"])
        self.assertEqual("localization_only", catalog["playerFacingTerms"]["aliasScope"])
        self.assertEqual(
            "al_guild_membership_policy", catalog["consumedContracts"]["guildCatalogId"]
        )
        self.assertEqual("al_realm_catalog", catalog["consumedContracts"]["realmCatalogId"])
        self.assertEqual("monday_00_00_utc", catalog["seasonPolicy"]["weekBoundary"])
        self.assertEqual(3, catalog["seasonPolicy"]["citiesPerRealm"])
        self.assertFalse(catalog["seasonPolicy"]["capitalsContestable"])
        self.assertTrue(catalog["seasonPolicy"]["neutralizeBeforeContest"])
        self.assertEqual(1, catalog["seasonPolicy"]["ownersPerCityPerWeek"])
        self.assertTrue(catalog["seasonPolicy"]["sameRealmParticipantsOnly"])
        self.assertEqual(4, len(catalog["realms"]))
        for realm in catalog["realms"]:
            self.assertEqual(3, len(realm["cityIds"]))
            self.assertNotIn(realm["capitalId"], realm["cityIds"])
            self.assertEqual(
                [
                    realm["realmId"] + "_guild_city_01",
                    realm["realmId"] + "_guild_city_02",
                    realm["realmId"] + "_guild_city_03",
                ],
                realm["cityIds"],
            )
        self.assertEqual("realm_symbol", catalog["bannerPolicy"]["neutralPresentation"])
        self.assertEqual("never", catalog["bannerPolicy"]["foreignBanner"])
        self.assertEqual("safe_text_mark", catalog["bannerPolicy"]["invalidFallback"])
        self.assertEqual(
            "downstream_3d_dungeon_reward_modifier", catalog["benefitPolicy"]["consumer"]
        )
        self.assertFalse(catalog["benefitPolicy"]["mintOathmarksIn25d"])
        self.assertFalse(catalog["benefitPolicy"]["mintOathmarksInKingdomManagement"])
        self.assertEqual(
            ["castle_capture_stronghold"],
            catalog["reservedAuthorities"]["strongholdCastleCapture"],
        )
        self.assertEqual("xor", catalog["recovery"]["terminalResolution"])
        self.assertEqual("no_owner", catalog["recovery"]["tie"])
        self.assertEqual(
            "engine_free_snapshot_only", catalog["authorityBoundary"]["plannerScope"]
        )
        self.assertEqual(
            [
                "persistence",
                "network",
                "save",
                "combat",
                "ui",
                "economy",
                "runtime_map",
                "oathmark_mint",
                "stronghold_capture",
                "public_realm_dungeon",
            ],
            catalog["authorityBoundary"]["excludedImplementations"],
        )


if __name__ == "__main__":
    unittest.main()
