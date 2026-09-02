#!/usr/bin/env python3
"""Tests for the approved deterministic scene/content manifest contract."""

from __future__ import annotations

import copy
import importlib.util
import json
import unittest
from pathlib import Path

SCRIPT = Path(__file__).with_name("scene_content_manifest.py")
REPO_ROOT = Path(__file__).resolve().parents[2]


def load_module():
    spec = importlib.util.spec_from_file_location("scene_content_manifest", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class SceneContentManifestTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.module = load_module()
        cls.enabled, cls.generated = cls.module.generate_manifests(REPO_ROOT)

    def test_every_known_scene_is_accounted_for(self) -> None:
        self.assertEqual(5, len(self.enabled["enabledScenes"]))
        self.assertEqual(21, len(self.enabled["excludedScenes"]))
        self.assertEqual(78, len(self.generated["generatedScenes"]))
        accounted = {
            record["assetPath"]
            for record in self.enabled["enabledScenes"]
            + self.enabled["excludedScenes"]
            + self.generated["generatedScenes"]
        }
        actual = set(self.module.discover_scene_paths(REPO_ROOT))
        self.assertEqual(104, len(actual))
        self.assertEqual(actual, accounted)

    def test_owner_approved_slagfall_review_scene_is_non_shipping(self) -> None:
        self.assertEqual("DEC-SCENE-DELIVERY-002", self.enabled["decisionId"])
        self.assertEqual("DEC-SCENE-DELIVERY-001", self.enabled["supersedesDecisionId"])
        review = next(
            record
            for record in self.enabled["excludedScenes"]
            if record["assetPath"]
            == "Assets/AL/Scenes/Review/Terrestrials/SlagfallEnvironmentKitReview.unity"
        )
        self.assertEqual("terrestrial_environment_review", review["purpose"])
        self.assertEqual({"domain": "terrestrials"}, review["ownership"])
        self.assertEqual("non_shipping", review["shippingStatus"])
        self.assertEqual(
            {"entry": False, "mode": "not_runtime_reachable"},
            review["reachability"],
        )

    def test_shipping_order_and_local_addressable_identity_are_exact(self) -> None:
        self.assertEqual(
            [
                "Assets/AL/Scenes/Boot.unity",
                "Assets/AL/Scenes/RealmSelection.unity",
                "Assets/AL/Scenes/CharacterCreation.unity",
                "Assets/AL/Scenes/ChampionArena.unity",
                "Assets/AL/Scenes/Kingdom.unity",
            ],
            [record["assetPath"] for record in self.enabled["enabledScenes"]],
        )
        generated = self.generated["generatedScenes"]
        self.assertEqual(
            sorted(record["chunkId"] for record in generated),
            [record["chunkId"] for record in generated],
        )
        for record in generated:
            self.assertEqual("local_addressable", record["shippingStatus"])
            self.assertEqual(
                f"AL.World.{record['ownership']['worldId']}",
                record["addressables"]["group"],
            )
            self.assertEqual(
                f"scene/{record['ownership']['worldId']}/{record['chunkId']}",
                record["addressables"]["address"],
            )
            self.assertEqual("LocalBuildPath", record["addressables"]["buildPath"])
            self.assertEqual("LocalLoadPath", record["addressables"]["loadPath"])

    def test_addressables_package_is_pinned_for_unity_6000_3(self) -> None:
        manifest = json.loads(
            (REPO_ROOT / "unity/Packages/manifest.json").read_text(encoding="utf-8")
        )
        package_lock = json.loads(
            (REPO_ROOT / "unity/Packages/packages-lock.json").read_text(encoding="utf-8")
        )
        self.assertEqual("2.9.1", manifest["dependencies"]["com.unity.addressables"])
        self.assertEqual(
            "2.9.1",
            package_lock["dependencies"]["com.unity.addressables"]["version"],
        )

    def test_addressable_authoring_matches_generated_manifest(self) -> None:
        self.module.validate_addressables_configuration(self.generated, REPO_ROOT)

    def test_identical_inputs_yield_byte_identical_manifests(self) -> None:
        first = self.module.render_manifests(*self.module.generate_manifests(REPO_ROOT))
        second = self.module.render_manifests(*self.module.generate_manifests(REPO_ROOT))
        self.assertEqual(first, second)

    def test_missing_required_scene_fails_closed(self) -> None:
        enabled = copy.deepcopy(self.enabled)
        enabled["enabledScenes"].pop(0)
        with self.assertRaisesRegex(self.module.ManifestError, "MISSING_REQUIRED_SCENE"):
            self.module.validate_manifest_payloads(enabled, self.generated, REPO_ROOT)

    def test_unexpected_enabled_scene_fails_closed(self) -> None:
        enabled = copy.deepcopy(self.enabled)
        enabled["enabledScenes"].append(copy.deepcopy(enabled["excludedScenes"][0]))
        with self.assertRaisesRegex(self.module.ManifestError, "UNEXPECTED_ENABLED_SCENE"):
            self.module.validate_manifest_payloads(enabled, self.generated, REPO_ROOT)

    def test_duplicate_identity_fails_closed(self) -> None:
        generated = copy.deepcopy(self.generated)
        generated["generatedScenes"][1]["chunkId"] = generated["generatedScenes"][0]["chunkId"]
        with self.assertRaisesRegex(self.module.ManifestError, "DUPLICATE_IDENTITY"):
            self.module.validate_manifest_payloads(self.enabled, generated, REPO_ROOT)

    def test_nondeterministic_order_fails_closed(self) -> None:
        generated = copy.deepcopy(self.generated)
        generated["generatedScenes"][0], generated["generatedScenes"][1] = (
            generated["generatedScenes"][1],
            generated["generatedScenes"][0],
        )
        with self.assertRaisesRegex(self.module.ManifestError, "NONDETERMINISTIC_ORDER"):
            self.module.validate_manifest_payloads(self.enabled, generated, REPO_ROOT)

    def test_new_scene_requires_owner_review(self) -> None:
        actual = self.module.discover_scene_paths(REPO_ROOT) + ["Assets/NewScene.unity"]
        with self.assertRaisesRegex(self.module.ManifestError, "SCENE_SET_REVIEW_REQUIRED"):
            self.module.validate_scene_accounting(self.enabled, self.generated, actual)

    def test_committed_manifest_hash_drift_fails_closed(self) -> None:
        enabled = copy.deepcopy(self.enabled)
        enabled["enabledScenes"][0]["sceneSha256"] = "0" * 64
        with self.assertRaisesRegex(self.module.ManifestError, "HASH_DRIFT"):
            self.module.validate_manifest_payloads(enabled, self.generated, REPO_ROOT)

    def test_committed_manifests_match_current_inputs(self) -> None:
        result = self.module.validate_repository(REPO_ROOT)
        self.assertEqual(5, result.enabled_count)
        self.assertEqual(78, result.generated_count)
        self.assertEqual(21, result.excluded_count)
        self.assertEqual(104, result.accounted_count)


if __name__ == "__main__":
    unittest.main()
