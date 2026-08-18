#!/usr/bin/env python3
"""Regression tests for the production notification catalog authority."""

from __future__ import annotations

import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_production_notification_catalog as validator


REPO_ROOT = Path(__file__).resolve().parents[2]
MANIFEST_PATH = REPO_ROOT / validator.MANIFEST_PATH


class ProductionNotificationCatalogValidationTests(unittest.TestCase):
    def load_manifest(self) -> dict:
        return json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))

    def test_committed_catalog_is_complete_and_resolvable(self) -> None:
        result = validator.validate_repository(REPO_ROOT)
        self.assertEqual(11, result.available_definition_count)
        self.assertEqual(6, result.blocked_requirement_count)
        self.assertEqual(
            {
                "al_notify_save_recovered_backup",
                "al_notify_save_profile_degraded",
                "al_notify_save_unrecoverable",
                "al_notify_operation_unavailable",
                "al_notify_reward_committed",
                "al_notify_reward_failed",
                "al_notify_world_event_started",
                "al_notify_world_event_ended",
                "al_notify_bridge_unavailable",
                "al_notify_catalog_unavailable",
                "al_notify_content_unavailable",
            },
            set(result.available_definition_ids),
        )

    def test_duplicate_entry_is_rejected(self) -> None:
        manifest = self.load_manifest()
        manifest["entries"].append(copy.deepcopy(manifest["entries"][0]))
        with self.assertRaisesRegex(validator.ValidationError, "duplicate entry id"):
            validator.validate_manifest(manifest, REPO_ROOT)

    def test_malformed_entry_id_is_rejected(self) -> None:
        manifest = self.load_manifest()
        manifest["entries"][0]["id"] = "AL_NOTIFY_BAD"
        with self.assertRaisesRegex(validator.ValidationError, "malformed entry id"):
            validator.validate_manifest(manifest, REPO_ROOT)

    def test_missing_required_entry_is_rejected(self) -> None:
        manifest = self.load_manifest()
        missing = manifest["entries"].pop()["id"]
        with self.assertRaisesRegex(validator.ValidationError, f"missing required entry: {missing}"):
            validator.validate_manifest(manifest, REPO_ROOT)

    def test_unknown_required_entry_is_rejected(self) -> None:
        manifest = self.load_manifest()
        manifest["requiredDefinitionIds"].append("al_notify_not_authored")
        with self.assertRaisesRegex(validator.ValidationError, "unreviewed entry"):
            validator.validate_manifest(manifest, REPO_ROOT)

    def test_unavailable_content_fails_explicitly(self) -> None:
        manifest = self.load_manifest()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaisesRegex(validator.ValidationError, "source content unavailable"):
                validator.validate_manifest(manifest, root)

    def test_source_content_drift_fails_instead_of_falling_back(self) -> None:
        manifest = self.load_manifest()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / manifest["source"]["path"]
            source.parent.mkdir(parents=True)
            source.write_bytes(b"{}\n")
            with self.assertRaisesRegex(validator.ValidationError, "source content identity mismatch"):
                validator.validate_manifest(manifest, root)

    def test_blocked_requirement_must_name_dependencies(self) -> None:
        manifest = self.load_manifest()
        manifest["blockedRequirements"][0]["dependencies"] = []
        with self.assertRaisesRegex(validator.ValidationError, "must name at least one dependency"):
            validator.validate_manifest(manifest, REPO_ROOT)

    def test_missing_known_blocked_requirement_is_rejected(self) -> None:
        manifest = self.load_manifest()
        missing = manifest["blockedRequirements"].pop()["requirementId"]
        with self.assertRaisesRegex(validator.ValidationError, f"missing blocked requirement: {missing}"):
            validator.validate_manifest(manifest, REPO_ROOT)

    def test_source_path_cannot_be_repointed(self) -> None:
        manifest = self.load_manifest()
        manifest["source"]["path"] = str(
            REPO_ROOT / "unity/Assets/AL/StreamingAssets/GameData/al_notification_content_catalog.json"
        )
        with self.assertRaisesRegex(validator.ValidationError, "unexpected source path"):
            validator.validate_manifest(manifest, REPO_ROOT)


if __name__ == "__main__":
    unittest.main()
