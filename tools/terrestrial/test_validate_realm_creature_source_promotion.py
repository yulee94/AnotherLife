import hashlib
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from tools.terrestrial.validate_realm_creature_source_promotion import validate_approval_packet, validate_packet


class RealmCreatureSourcePromotionValidationTests(unittest.TestCase):
    def make_approval_packet(self, root: Path):
        entries = []
        for index in range(21):
            path = root / "ConceptSheets" / f"source_{index}.png"
            path.parent.mkdir(parents=True, exist_ok=True)
            Image.new("RGB", (2, 3), (index, index, index)).save(path)
            payload = path.read_bytes()
            entries.append({
                "id": f"source_{index}",
                "status": "APPROVED_2D",
                "sources": [{
                    "path": path.relative_to(root).as_posix(),
                    "bytes": len(payload),
                    "sha256": hashlib.sha256(payload).hexdigest(),
                    "dimensions": [2, 3],
                }],
            })
        return {"rosterCount": 21, "approvedCount": 21, "blockedCount": 0, "entries": entries}

    def make_packet(self, root: Path):
        models = []
        for index in range(21):
            model = root / "Models" / f"model_{index}.fbx"
            review = root / "Review" / f"model_{index}.png"
            texture_root = root / "Textures" / f"model_{index}"
            textures = [texture_root / name for name in ("base_color.png", "normal.png", "roughness.png", "metallic.png")]
            files = [(model, b"model" + bytes([index])), (review, b"review" + bytes([index]))]
            files.extend((path, path.name.encode() + bytes([index])) for path in textures)
            for path, payload in files:
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(payload)
            def record(path):
                payload = path.read_bytes()
                return {"path": path.relative_to(root).as_posix(), "bytes": len(payload), "sha256": hashlib.sha256(payload).hexdigest()}
            texture_records = []
            for texture in textures:
                texture_record = record(texture)
                texture_record["dimensions"] = [8192, 8192] if texture.name == "base_color.png" else [4096, 4096]
                texture_records.append(texture_record)
            models.append({
                "source2dId": f"source_{index}", "modelId": f"model_{index}", "status": "clean_textured_base_pass",
                "blocker": None, "selectedSource": record(model), "textures": texture_records, "review": record(review),
                "meshyTaskIds": [f"task-{index}"], "rigged": False, "runtimeIntegrationState": "Blocked", "productionReady": False,
            })
        return {
            "schemaVersion": 1, "packetId": "anotherlife-realm-creature-production-source",
            "readiness": {"runtimeIntegrationState": "Blocked"},
            "summary": {"approved2D": 21, "structuralPass": 21, "blocked3D": 0, "ownerTierTexturePackets": 21, "belowOwnerTierTexturePackets": 0, "runtimeIntegrationState": "Blocked"},
            "models": models,
        }

    def test_accepts_complete_hash_verified_non_runtime_packet(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.assertEqual(validate_packet(self.make_packet(root), root), [])

    def test_accepts_complete_hash_verified_2d_approval_packet(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.assertEqual(validate_approval_packet(self.make_approval_packet(root), root), [])

    def test_rejects_tampered_2d_approval_source(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            packet = self.make_approval_packet(root)
            (root / packet["entries"][0]["sources"][0]["path"]).write_bytes(b"tampered")
            diagnostics = validate_approval_packet(packet, root)
            self.assertTrue(any("2D source" in item and "mismatch" in item for item in diagnostics))

    def test_rejects_tampered_promoted_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            packet = self.make_packet(root)
            (root / packet["models"][0]["selectedSource"]["path"]).write_bytes(b"tampered")
            diagnostics = validate_packet(packet, root)
            self.assertTrue(any("mismatch" in item for item in diagnostics))

    def test_rejects_blocker_count_that_omits_non_manual_status(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            packet = self.make_packet(root)
            packet["models"][0]["status"] = "clean_geometry_pass_8k_texture_manual_uv_bake_required"
            packet["models"][0]["blocker"] = "manual UV bake required"
            diagnostics = validate_packet(packet, root)
            self.assertIn("summary.blocked3D does not match model blockers", diagnostics)

    def test_rejects_absolute_or_traversal_asset_paths(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            packet = self.make_packet(root)
            packet["models"][0]["selectedSource"]["path"] = "../outside.fbx"
            diagnostics = validate_packet(packet, root)
            self.assertTrue(any("relative packet path" in item for item in diagnostics))

    def test_rejects_missing_generation_task_binding(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            packet = self.make_packet(root)
            packet["models"][0]["meshyTaskIds"] = []
            diagnostics = validate_packet(packet, root)
            self.assertIn("model_0: at least one Meshy task ID is required", diagnostics)

    def test_rejects_incorrect_owner_texture_tier_coverage(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            packet = self.make_packet(root)
            packet["models"][0]["textures"][0]["dimensions"] = [2048, 2048]
            diagnostics = validate_packet(packet, root)
            self.assertIn("summary owner-tier texture coverage does not match texture dimensions", diagnostics)

    def test_rejects_owner_tier_without_4k_support_maps(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            packet = self.make_packet(root)
            normal = next(texture for texture in packet["models"][0]["textures"] if texture["path"].endswith("/normal.png"))
            normal["dimensions"] = [2048, 2048]
            diagnostics = validate_packet(packet, root)
            self.assertIn("summary owner-tier texture coverage does not match texture dimensions", diagnostics)


if __name__ == "__main__":
    unittest.main()
