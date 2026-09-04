#!/usr/bin/env python3
"""Focused fail-closed checks for the PvP harmful-effect gate contract."""

import json
import pathlib
import unittest

from jsonschema import Draft202012Validator


class PvpHarmfulEffectGateContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = pathlib.Path(__file__).resolve().parents[3]
        cls.schema_path = (
            cls.repo_root
            / "unity"
            / "SharedContracts"
            / "Schemas"
            / "al-pvp-harmful-effect-gate.schema.json"
        )
        cls.catalog_path = (
            cls.repo_root
            / "unity"
            / "Assets"
            / "AL"
            / "StreamingAssets"
            / "GameData"
            / "al_pvp_harmful_effect_gate_policy.json"
        )
        cls.fixtures = pathlib.Path(__file__).resolve().parent / "fixtures"
        cls.schema = json.loads(cls.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.validator = Draft202012Validator(cls.schema)

    def test_real_catalog_and_valid_fixture_match_and_validate(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))
        fixture = json.loads(
            (self.fixtures / "valid" / "al-pvp-harmful-effect-gate.valid.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(fixture, catalog)
        self.assertEqual([], list(self.validator.iter_errors(catalog)))

    def test_every_invalid_fixture_is_rejected(self):
        invalid_paths = sorted(
            (self.fixtures / "invalid").glob("al-pvp-harmful-effect-gate.invalid.*.json")
        )
        self.assertGreaterEqual(len(invalid_paths), 4)
        for path in invalid_paths:
            with self.subTest(path=path.name):
                instance = json.loads(path.read_text(encoding="utf-8"))
                errors = list(self.validator.iter_errors(instance))
                self.assertEqual(1, len(errors), [error.message for error in errors])

    def test_precedence_revalidation_and_non_authority_are_exact(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))

        self.assertEqual("PvpHarmfulEffectGate", catalog["machineIdentity"])
        self.assertEqual("same_realm_only", catalog["scope"]["realmBinding"])
        self.assertFalse(catalog["scope"]["clientAuthority"])
        self.assertFalse(catalog["scope"]["healthMutation"])
        self.assertFalse(catalog["scope"]["hardcodedZones"])
        self.assertEqual(
            "al_guild_membership_policy", catalog["consumedContracts"]["guildCatalogId"]
        )
        self.assertEqual(
            "al_alliance_war_policy", catalog["consumedContracts"]["allianceCatalogId"]
        )
        self.assertEqual(
            [
                "direct_hit",
                "projectile",
                "area_of_effect",
                "damage_over_time_tick",
                "chain",
                "trap",
                "pet_summon",
                "reflect",
                "splash",
                "environmental",
                "crowd_control",
            ],
            catalog["effectKinds"],
        )
        self.assertEqual(
            [
                "invalid_stale_unknown_or_cross_realm",
                "forced_safe_zone",
                "same_guild",
                "same_effective_alliance",
                "both_toggles_on_or_opposing_active_war",
            ],
            catalog["precedence"],
        )
        self.assertEqual("ACTIVE", catalog["hostility"]["forceHostilityWarState"])
        self.assertEqual(24, catalog["hostility"]["warNoticeHours"])
        self.assertEqual(168, catalog["hostility"]["warActiveHours"])
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
        self.assertFalse(catalog["presentation"]["nameplateAuthoritative"])
        self.assertFalse(catalog["presentation"]["warIconAuthoritative"])
        self.assertEqual(
            "engine_free_snapshot_only", catalog["authorityBoundary"]["plannerScope"]
        )


if __name__ == "__main__":
    unittest.main()
