#!/usr/bin/env python3
"""Focused fail-closed checks for the Guild membership policy contract."""

import json
import pathlib
import unittest

from jsonschema import Draft202012Validator


class GuildMembershipContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = pathlib.Path(__file__).resolve().parents[3]
        cls.schema_path = (
            cls.repo_root
            / "unity"
            / "SharedContracts"
            / "Schemas"
            / "al-guild-membership.schema.json"
        )
        cls.catalog_path = (
            cls.repo_root
            / "unity"
            / "Assets"
            / "AL"
            / "StreamingAssets"
            / "GameData"
            / "al_guild_membership_policy.json"
        )
        cls.fixtures = pathlib.Path(__file__).resolve().parent / "fixtures"
        cls.schema = json.loads(cls.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.validator = Draft202012Validator(cls.schema)

    def test_real_catalog_and_valid_fixture_match_and_validate(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))
        fixture = json.loads(
            (self.fixtures / "valid" / "al-guild-membership.valid.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(fixture, catalog)
        self.assertEqual([], list(self.validator.iter_errors(catalog)))

    def test_every_invalid_fixture_is_rejected(self):
        invalid_paths = sorted(
            (self.fixtures / "invalid").glob("al-guild-membership.invalid.*.json")
        )
        self.assertGreaterEqual(len(invalid_paths), 4)
        for path in invalid_paths:
            with self.subTest(path=path.name):
                instance = json.loads(path.read_text(encoding="utf-8"))
                errors = list(self.validator.iter_errors(instance))
                self.assertEqual(1, len(errors), [error.message for error in errors])

    def test_machine_identity_scope_roles_and_effect_boundary_are_exact(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))

        self.assertEqual("Guild", catalog["machineIdentity"])
        self.assertEqual("Guild", catalog["playerFacingTerms"]["primary"])
        self.assertEqual("Clan", catalog["playerFacingTerms"]["presentationAlias"])
        self.assertEqual("account", catalog["membershipScope"]["subject"])
        self.assertTrue(catalog["membershipScope"]["sameRealmCharactersShareMembership"])
        self.assertEqual(
            ["Master", "Officer", "Member"],
            [row["role"] for row in catalog["roles"]],
        )
        officer = catalog["roles"][1]
        self.assertTrue(officer["canManageInvitations"])
        self.assertTrue(officer["canManageMembers"])
        self.assertTrue(officer["canOpenRaidCalls"])
        self.assertFalse(officer["canFormAlliancesOrDeclareWar"])
        self.assertEqual(
            ["combat", "economy", "perk", "city", "raid"],
            catalog["excludedEffectDomains"],
        )
        self.assertEqual("master_or_officer", catalog["pendingRequestPolicy"]["joinApplicationResolution"])
        self.assertEqual(
            "invited_account_or_manager_cancel",
            catalog["pendingRequestPolicy"]["invitationResolution"],
        )
        self.assertEqual(
            "accept_exact_guild_revision",
            catalog["pendingRequestPolicy"]["membershipChangeFence"],
        )


if __name__ == "__main__":
    unittest.main()
