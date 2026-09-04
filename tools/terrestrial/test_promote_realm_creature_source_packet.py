import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from tools.terrestrial.promote_realm_creature_source_packet import (
    PacketError,
    copy_asset,
    file_record,
    validate_input_manifests,
    validate_promoted_files,
)
from tools.terrestrial.build_realm_creature_source_packet import copy_schema


class RealmCreaturePromotionTests(unittest.TestCase):
    def make_manifests(self):
        approval = {
            "rosterCount": 21,
            "approvedCount": 21,
            "blockedCount": 0,
            "entries": [{"id": f"source_{i}"} for i in range(21)],
        }
        readiness = {
            "rosterCount": 21,
            "models": [
                {
                    "id": f"model_{i}",
                    "status": (
                        "clean_textured_base_pass"
                        if i < 16
                        else "clean_geometry_pass_8k_texture_manual_uv_bake_required"
                        if i == 16
                        else "manual_dcc_required"
                    ),
                    "blocker": (
                        None
                        if i < 16
                        else "manual UV bake required"
                        if i == 16
                        else "manual DCC repair required"
                    ),
                    "file": f"model_{i}.fbx" if i < 17 else None,
                    "bestBase": f"model_{i}.fbx" if i >= 17 else None,
                }
                for i in range(21)
            ],
        }
        mapping = {f"source_{i}": f"model_{i}" for i in range(21)}
        return approval, readiness, mapping

    def test_rejects_incomplete_approval_roster(self):
        approval, readiness, mapping = self.make_manifests()
        approval["approvedCount"] = 20
        with self.assertRaisesRegex(PacketError, "21 approved 2D entries"):
            validate_input_manifests(approval, readiness, mapping)

    def test_rejects_missing_3d_mapping(self):
        approval, readiness, mapping = self.make_manifests()
        mapping.pop("source_20")
        with self.assertRaisesRegex(PacketError, "mapping must cover every approved 2D entry"):
            validate_input_manifests(approval, readiness, mapping)

    def test_accepts_honest_blocked_models_without_promoting_runtime_state(self):
        approval, readiness, mapping = self.make_manifests()
        result = validate_input_manifests(approval, readiness, mapping)
        self.assertEqual(result["approved2D"], 21)
        self.assertEqual(result["structuralPass"], 17)
        self.assertEqual(result["blocked3D"], 5)
        self.assertEqual(result["runtimeIntegrationState"], "Blocked")

    def test_file_record_hashes_exact_bytes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            path = root / "model.fbx"
            payload = b"realm-creature-source-bytes"
            path.write_bytes(payload)
            record = file_record(path, root)
            self.assertEqual(record["path"], "model.fbx")
            self.assertEqual(record["bytes"], len(payload))
            self.assertEqual(record["sha256"], hashlib.sha256(payload).hexdigest())

    def test_copy_asset_preserves_bytes_and_records_destination(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source = root / "scratch" / "model.fbx"
            destination = root / "packet" / "Models" / "model.fbx"
            source.parent.mkdir(parents=True)
            payload = b"selected-source-model"
            source.write_bytes(payload)
            record = copy_asset(source, destination, root / "packet")
            self.assertEqual(destination.read_bytes(), payload)
            self.assertEqual(record["path"], "Models/model.fbx")
            self.assertEqual(record["sha256"], hashlib.sha256(payload).hexdigest())

    def test_validate_promoted_files_rejects_tampering(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            path = root / "Models" / "model.fbx"
            path.parent.mkdir(parents=True)
            path.write_bytes(b"before")
            record = file_record(path, root)
            validate_promoted_files(root, [record])
            path.write_bytes(b"afters")
            with self.assertRaisesRegex(PacketError, "hash mismatch"):
                validate_promoted_files(root, [record])

    def test_copy_schema_retains_the_tracked_contract(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source = root / "templates" / "packet.schema.json"
            source.parent.mkdir(parents=True)
            source.write_bytes(b'{"schemaVersion": 1}\n')
            destination = copy_schema(source, root / "docs")
            self.assertEqual(destination.name, "realm_creature_3d_source_manifest.schema.json")
            self.assertEqual(destination.read_bytes(), source.read_bytes())


if __name__ == "__main__":
    unittest.main()
