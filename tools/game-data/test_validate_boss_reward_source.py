#!/usr/bin/env python3
"""Fail-closed tests for the immutable boss-to-reward technical source."""

from __future__ import annotations

import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

THIS_DIR = Path(__file__).resolve().parent
REPOSITORY_ROOT = THIS_DIR.parents[1]
sys.path.insert(0, str(THIS_DIR))

import validate_boss_reward_source as target  # noqa: E402


class BossRewardSourceCatalogTests(unittest.TestCase):
    def test_production_catalog_resolves_approved_boss_without_mutation(self) -> None:
        result = target.validate_catalog_file(target.DEFAULT_CATALOG_PATH, REPOSITORY_ROOT)

        self.assertEqual("blocked", result.mutation_activation)
        self.assertFalse(result.allows_mutation)
        self.assertEqual([], result.activation_targets)
        self.assertEqual(
            "boss_stonehold_fault_crowned_colossus",
            result.resolved.boss_definition_id,
        )
        self.assertEqual(
            "reward_profile_stonehold_fault_crowned_colossus",
            result.resolved.reward_profile_id,
        )
        self.assertEqual(
            "equipment_stonehold_fault_crowned_colossus_core",
            result.resolved.equipment_definition_ids[0],
        )
        self.assertEqual(250, result.resolved.warzone_credits)
        self.assertEqual(1, result.resolved.quantities[0])
        self.assertEqual(1_000_000, result.resolved.drop_chance_micros[0])
        self.assertEqual(0, result.resolved.attack_bonus)
        self.assertEqual(0, result.resolved.defense_bonus)
        self.assertEqual(0, result.resolved.health_bonus)
        self.assertNotEqual(
            result.resolved.equipment_definition_ids[0],
            result.resolved.presentation_content_key,
        )

    def test_resolve_unknown_boss_is_explicit(self) -> None:
        catalog = self.load_catalog()
        with self.assertRaisesRegex(target.ValidationError, "unknown boss"):
            target.resolve_boss(catalog, "boss_unknown_placeholder")

    def test_missing_catalog_file_is_explicit(self) -> None:
        missing = REPOSITORY_ROOT / "unity" / "Assets" / "AL" / "StreamingAssets" / "GameData" / "missing_boss_reward_source.json"
        with self.assertRaisesRegex(target.ValidationError, "unavailable"):
            target.validate_catalog_file(missing, REPOSITORY_ROOT)

    def test_duplicate_binding_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["bindings"].append(copy.deepcopy(catalog["bindings"][0]))
        with self.assertRaisesRegex(target.ValidationError, "duplicate binding"):
            target.validate_catalog(catalog)

    def test_duplicate_profile_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["profiles"].append(copy.deepcopy(catalog["profiles"][0]))
        with self.assertRaisesRegex(target.ValidationError, "duplicate profile"):
            target.validate_catalog(catalog)

    def test_duplicate_equipment_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["equipmentDefinitions"].append(
            copy.deepcopy(catalog["equipmentDefinitions"][0])
        )
        with self.assertRaisesRegex(target.ValidationError, "duplicate equipment"):
            target.validate_catalog(catalog)

    def test_unsupported_schema_version_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["schemaVersion"] = "boss_reward_schema_v2"
        with self.assertRaisesRegex(target.ValidationError, "unsupported schema version"):
            target.validate_catalog(catalog)

    def test_profile_hash_mismatch_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["profiles"][0]["rawSha256"] = "0" * 64
        with self.assertRaisesRegex(target.ValidationError, "profile hash"):
            target.validate_catalog(catalog)

    def test_equipment_hash_mismatch_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["equipmentDefinitions"][0]["rawSha256"] = "0" * 64
        with self.assertRaisesRegex(target.ValidationError, "equipment hash"):
            target.validate_catalog(catalog)

    def test_missing_equipment_reference_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["profiles"][0]["entries"][0]["equipmentDefinitionId"] = (
            "equipment_missing_reference"
        )
        catalog["profiles"][0]["rawSha256"] = target.profile_sha256(catalog["profiles"][0])
        with self.assertRaisesRegex(target.ValidationError, "missing equipment reference"):
            target.validate_catalog(catalog)

    def test_missing_profile_reference_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["bindings"][0]["rewardProfileId"] = "reward_profile_missing"
        with self.assertRaisesRegex(target.ValidationError, "missing profile reference"):
            target.validate_catalog(catalog)

    def test_binding_profile_version_mismatch_fails_closed(self) -> None:
        catalog = self.load_catalog()
        catalog["bindings"][0]["rewardProfileContentVersion"] = "v999"
        with self.assertRaisesRegex(target.ValidationError, "profile version"):
            target.validate_catalog(catalog)

    def test_production_file_hash_is_pinned(self) -> None:
        path = REPOSITORY_ROOT / target.DEFAULT_CATALOG_PATH
        raw = path.read_bytes()
        self.assertEqual(target.EXPECTED_SOURCE_BYTE_LENGTH, len(raw))
        self.assertEqual(target.EXPECTED_SOURCE_SHA256, target.sha256_hex(raw))

    def test_sabotaged_production_bytes_fail_hash_then_restore(self) -> None:
        path = REPOSITORY_ROOT / target.DEFAULT_CATALOG_PATH
        original = path.read_bytes()
        sabotaged = original.replace(
            b"boss_stonehold_fault_crowned_colossus",
            b"boss_stonehold_fault_crowned_colossuX",
            1,
        )
        self.assertNotEqual(original, sabotaged)
        with tempfile.TemporaryDirectory() as tmp:
            candidate = Path(tmp) / "al_boss_reward_source_catalog.json"
            candidate.write_bytes(sabotaged)
            with self.assertRaisesRegex(target.ValidationError, "source hash"):
                target.validate_catalog_file(
                    candidate,
                    REPOSITORY_ROOT,
                    require_pinned_source=True,
                )
        self.assertEqual(original, path.read_bytes())
        restored = target.validate_catalog_file(path, REPOSITORY_ROOT)
        self.assertEqual("blocked", restored.mutation_activation)

    def test_mutation_activation_cannot_be_forged(self) -> None:
        catalog = self.load_catalog()
        catalog["mutationActivation"] = "enabled"
        with self.assertRaisesRegex(target.ValidationError, "mutationActivation"):
            target.validate_catalog(catalog)

    def load_catalog(self) -> dict:
        path = REPOSITORY_ROOT / target.DEFAULT_CATALOG_PATH
        return json.loads(path.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
