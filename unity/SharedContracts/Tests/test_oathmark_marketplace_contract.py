#!/usr/bin/env python3
"""Focused fail-closed checks for the Oathmark Marketplace settlement contract."""

import json
import pathlib
import unittest

from jsonschema import Draft202012Validator


class OathmarkMarketplaceContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = pathlib.Path(__file__).resolve().parents[3]
        cls.schema_path = (
            cls.repo_root
            / "unity"
            / "SharedContracts"
            / "Schemas"
            / "al-oathmark-marketplace.schema.json"
        )
        cls.catalog_path = (
            cls.repo_root
            / "unity"
            / "Assets"
            / "AL"
            / "StreamingAssets"
            / "GameData"
            / "al_oathmark_marketplace_policy.json"
        )
        cls.fixtures = pathlib.Path(__file__).resolve().parent / "fixtures"
        cls.schema = json.loads(cls.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.validator = Draft202012Validator(cls.schema)

    def test_real_catalog_and_valid_fixture_match_and_validate(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))
        fixture = json.loads(
            (self.fixtures / "valid" / "al-oathmark-marketplace.valid.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(fixture, catalog)
        self.assertEqual([], list(self.validator.iter_errors(catalog)))

    def test_every_invalid_fixture_is_rejected(self):
        invalid_paths = sorted(
            (self.fixtures / "invalid").glob("al-oathmark-marketplace.invalid.*.json")
        )
        self.assertGreaterEqual(len(invalid_paths), 8)
        for path in invalid_paths:
            with self.subTest(path=path.name):
                instance = json.loads(path.read_text(encoding="utf-8"))
                errors = list(self.validator.iter_errors(instance))
                self.assertEqual(1, len(errors), [error.message for error in errors])

    def test_oathmark_identity_settlement_and_boundary_are_exact(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))

        self.assertEqual("oathmark", catalog["currency"]["technicalId"])
        self.assertEqual("Oathmark", catalog["currency"]["playerFacingSingular"])
        self.assertEqual("Oathmarks", catalog["currency"]["playerFacingPlural"])
        self.assertEqual(1, catalog["currency"]["integerUnitScale"])
        self.assertFalse(catalog["currency"]["fractionalUnits"])
        self.assertEqual("forbidden", catalog["currency"]["conversion"])
        self.assertEqual("forbidden", catalog["currency"]["premiumOrRealMoney"])
        self.assertEqual(10, catalog["listing"]["minimumPrice"])
        self.assertEqual(9223372036854775807, catalog["listing"]["maximumPrice"])
        self.assertEqual(72, catalog["listing"]["durationHours"])
        self.assertEqual(
            "seller_only_before_reservation", catalog["listing"]["cancellation"]
        )
        self.assertEqual(
            "floor_listed_price_divided_by_ten",
            catalog["settlement"]["tax"]["method"],
        )
        self.assertEqual(10, catalog["settlement"]["tax"]["divisor"])
        self.assertEqual("destroyed", catalog["settlement"]["tax"]["destination"])
        self.assertEqual([], catalog["settlement"]["tax"]["creditedWallets"])
        self.assertEqual(
            "engine_free_snapshot_only",
            catalog["authorityBoundary"]["plannerScope"],
        )
        self.assertIn("network", catalog["authorityBoundary"]["excludedImplementations"])
        self.assertIn("save", catalog["authorityBoundary"]["excludedImplementations"])
        self.assertIn("repair", catalog["authorityBoundary"]["excludedImplementations"])
        self.assertIn(
            "consumable", catalog["authorityBoundary"]["excludedImplementations"]
        )
        self.assertIn(
            "earning_source", catalog["authorityBoundary"]["excludedImplementations"]
        )


if __name__ == "__main__":
    unittest.main()
