#!/usr/bin/env python3
"""Focused fail-closed checks for the Alliance and war snapshot contract."""

import json
import pathlib
import unittest

from jsonschema import Draft202012Validator


class AllianceWarContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = pathlib.Path(__file__).resolve().parents[3]
        cls.schema_path = (
            cls.repo_root
            / "unity"
            / "SharedContracts"
            / "Schemas"
            / "al-alliance-war.schema.json"
        )
        cls.catalog_path = (
            cls.repo_root
            / "unity"
            / "Assets"
            / "AL"
            / "StreamingAssets"
            / "GameData"
            / "al_alliance_war_policy.json"
        )
        cls.fixtures = pathlib.Path(__file__).resolve().parent / "fixtures"
        cls.schema = json.loads(cls.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.validator = Draft202012Validator(cls.schema)

    def test_real_catalog_and_valid_fixture_match_and_validate(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))
        fixture = json.loads(
            (self.fixtures / "valid" / "al-alliance-war.valid.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(fixture, catalog)
        self.assertEqual([], list(self.validator.iter_errors(catalog)))

    def test_every_invalid_fixture_is_rejected(self):
        invalid_paths = sorted(
            (self.fixtures / "invalid").glob("al-alliance-war.invalid.*.json")
        )
        self.assertGreaterEqual(len(invalid_paths), 4)
        for path in invalid_paths:
            with self.subTest(path=path.name):
                instance = json.loads(path.read_text(encoding="utf-8"))
                errors = list(self.validator.iter_errors(instance))
                self.assertEqual(1, len(errors), [error.message for error in errors])

    def test_master_only_same_realm_war_window_and_hostility_precedence_are_exact(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))

        self.assertEqual("Alliance", catalog["machineIdentity"])
        self.assertEqual("same_realm_only", catalog["scope"]["realmBinding"])
        self.assertEqual("guild", catalog["scope"]["memberSubject"])
        self.assertEqual("guild_master_only", catalog["scope"]["proposalAuthority"])
        self.assertEqual(
            "invited_guild_master_only", catalog["scope"]["acceptanceAuthority"]
        )
        self.assertEqual(
            "derived_from_member_master", catalog["scope"]["leadership"]
        )
        self.assertEqual(
            "derived_alliance_leader_only",
            catalog["scope"]["warDeclarationAuthority"],
        )
        self.assertFalse(catalog["scope"]["officersCanFormAlliancesOrDeclareWar"])
        self.assertEqual(24, catalog["durations"]["warNoticeHours"])
        self.assertEqual(168, catalog["durations"]["warActiveHours"])
        self.assertEqual("ACTIVE", catalog["hostility"]["forceHostilityWarState"])
        self.assertEqual(
            [
                "same_guild",
                "same_alliance",
                "city",
                "beginner",
                "accordant",
                "forced_safe",
            ],
            catalog["hostility"]["neverOverride"],
        )
        self.assertEqual(
            "accept_exact_alliance_and_guild_revisions",
            catalog["pendingRequestPolicy"]["membershipChangeFence"],
        )
        self.assertEqual(
            "accept_xor_decline",
            catalog["pendingRequestPolicy"]["terminalResolution"],
        )
        self.assertEqual(
            "fail_closed_until_reconciled", catalog["replayPolicy"]["unknownOutcome"]
        )
        self.assertEqual(
            "external_guild_snapshot", catalog["consumedContracts"]["guildIdentity"]
        )
        self.assertEqual(
            "external_guild_roles", catalog["consumedContracts"]["guildRoles"]
        )
        self.assertEqual(
            "al_guild_membership_policy", catalog["consumedContracts"]["guildCatalogId"]
        )


if __name__ == "__main__":
    unittest.main()
