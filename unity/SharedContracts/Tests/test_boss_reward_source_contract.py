#!/usr/bin/env python3
"""Focused schema checks for the boss-reward technical source catalog."""

import json
import pathlib
import unittest

from jsonschema import Draft202012Validator


class BossRewardSourceContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = pathlib.Path(__file__).resolve().parents[3]
        cls.schema_path = (
            cls.repo_root
            / "unity"
            / "SharedContracts"
            / "Schemas"
            / "al-boss-reward-source.schema.json"
        )
        cls.catalog_path = (
            cls.repo_root
            / "unity"
            / "Assets"
            / "AL"
            / "StreamingAssets"
            / "GameData"
            / "al_boss_reward_source_catalog.json"
        )
        cls.fixtures = pathlib.Path(__file__).resolve().parent / "fixtures"
        cls.schema = json.loads(cls.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.validator = Draft202012Validator(cls.schema)

    def test_real_catalog_and_valid_fixture_match_and_validate(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))
        fixture = json.loads(
            (self.fixtures / "valid" / "al-boss-reward-source.valid.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertEqual(fixture, catalog)
        self.assertEqual([], list(self.validator.iter_errors(catalog)))
        self.assertEqual("blocked", catalog["mutationActivation"])
        self.assertNotEqual(
            catalog["equipmentDefinitions"][0]["equipmentDefinitionId"],
            catalog["equipmentDefinitions"][0]["presentationContentKey"],
        )

    def test_every_invalid_fixture_is_rejected(self):
        invalid_paths = sorted(
            (self.fixtures / "invalid").glob("al-boss-reward-source.invalid.*.json")
        )
        self.assertGreaterEqual(len(invalid_paths), 4)
        for path in invalid_paths:
            with self.subTest(path=path.name):
                instance = json.loads(path.read_text(encoding="utf-8"))
                errors = list(self.validator.iter_errors(instance))
                self.assertGreaterEqual(len(errors), 1, path.name)


if __name__ == "__main__":
    unittest.main()
