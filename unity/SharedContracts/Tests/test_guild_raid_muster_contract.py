#!/usr/bin/env python3
"""Focused fail-closed checks for the Guild raid muster / closed-instance contract."""

import json
import pathlib
import unittest

from jsonschema import Draft202012Validator


class GuildRaidMusterContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = pathlib.Path(__file__).resolve().parents[3]
        cls.schema_path = (
            cls.repo_root
            / "unity"
            / "SharedContracts"
            / "Schemas"
            / "al-guild-raid-muster.schema.json"
        )
        cls.catalog_path = (
            cls.repo_root
            / "unity"
            / "Assets"
            / "AL"
            / "StreamingAssets"
            / "GameData"
            / "al_guild_raid_muster_policy.json"
        )
        cls.fixtures = pathlib.Path(__file__).resolve().parent / "fixtures"
        cls.schema = json.loads(cls.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.validator = Draft202012Validator(cls.schema)

    def test_real_catalog_and_valid_fixture_match_and_validate(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))
        fixture = json.loads(
            (self.fixtures / "valid" / "al-guild-raid-muster.valid.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(fixture, catalog)
        self.assertEqual([], list(self.validator.iter_errors(catalog)))

    def test_every_invalid_fixture_is_rejected(self):
        invalid_paths = sorted(
            (self.fixtures / "invalid").glob("al-guild-raid-muster.invalid.*.json")
        )
        self.assertGreaterEqual(len(invalid_paths), 4)
        for path in invalid_paths:
            with self.subTest(path=path.name):
                instance = json.loads(path.read_text(encoding="utf-8"))
                errors = list(self.validator.iter_errors(instance))
                self.assertEqual(1, len(errors), [error.message for error in errors])

    def test_window_consent_rotation_and_dungeon_separation_are_exact(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))

        self.assertEqual("Guild", catalog["machineIdentity"])
        self.assertEqual("Guild", catalog["playerFacingTerms"]["primary"])
        self.assertEqual("Clan", catalog["playerFacingTerms"]["presentationAlias"])
        self.assertEqual("localization_only", catalog["playerFacingTerms"]["aliasScope"])
        self.assertEqual(
            "al_guild_membership_policy", catalog["consumedContracts"]["guildCatalogId"]
        )
        self.assertEqual(
            "al_alliance_war_policy", catalog["consumedContracts"]["allianceCatalogId"]
        )
        self.assertEqual(30, catalog["callPolicy"]["windowMinutes"])
        self.assertEqual(1, catalog["callPolicy"]["callsPerGuildPerWeek"])
        self.assertEqual("master_or_officer", catalog["callPolicy"]["callerAuthority"])
        self.assertEqual(4, catalog["bossRotation"]["slotCount"])
        self.assertEqual(
            "season_epoch_plus_week_id_mod_4", catalog["bossRotation"]["formula"]
        )
        self.assertEqual(4, len(catalog["bossRotation"]["slots"]))
        self.assertEqual("closed_raid_", catalog["closedInstance"]["idPrefix"])
        self.assertEqual(
            "command_envelope_only", catalog["closedInstance"]["transferAuthority"]
        )
        self.assertEqual("no_response", catalog["consent"]["silence"])
        self.assertFalse(catalog["consent"]["warBypassesConsent"])
        self.assertFalse(catalog["consent"]["membershipInfersJoin"])
        self.assertEqual("no_reward_no_lockout", catalog["recovery"]["unknownOutcome"])
        self.assertEqual("xor", catalog["recovery"]["terminalResolution"])
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
                "city",
                "scene_teleport",
                "boss_balance",
                "public_realm_dungeon",
            ],
            catalog["authorityBoundary"]["excludedImplementations"],
        )


if __name__ == "__main__":
    unittest.main()
