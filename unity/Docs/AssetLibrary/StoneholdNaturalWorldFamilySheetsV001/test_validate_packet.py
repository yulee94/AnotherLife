from __future__ import annotations

import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.dont_write_bytecode = True

PACKET = Path(__file__).resolve().parent


class PacketValidatorTests(unittest.TestCase):
    def test_families_catalog_is_exactly_54_unique(self) -> None:
        payload = json.loads((PACKET / "families_v001.json").read_text(encoding="utf-8"))
        ids = [row["familyId"] for row in payload["families"]]
        self.assertEqual(payload["expectedFamilyCount"], 54)
        self.assertEqual(len(ids), 54)
        self.assertEqual(len(set(ids)), 54)
        categories = {row["category"] for row in payload["families"]}
        self.assertEqual(
            categories,
            {
                "terrain_geology",
                "slagfall",
                "vegetation",
                "water_shore",
                "ore_crystal",
                "vfx_weather",
                "dressing",
            },
        )

    def test_gitattributes_pins_png_lfs_and_lf(self) -> None:
        raw = (PACKET / ".gitattributes").read_bytes()
        self.assertNotIn(b"\r", raw)
        text = raw.decode("utf-8")
        self.assertIn("*.png filter=lfs diff=lfs merge=lfs -text", text)
        self.assertIn("*.json text eol=lf", text)

    def test_pending_review_is_not_approve(self) -> None:
        if not (PACKET / "family_sheet_manifest_v001.json").is_file():
            self.skipTest("manifest not generated yet")
        manifest = json.loads((PACKET / "family_sheet_manifest_v001.json").read_text(encoding="utf-8"))
        self.assertEqual(manifest["approval"]["decision"], "PENDING")
        self.assertFalse(manifest["approval"]["meshyAuthorized"])
        self.assertFalse(manifest["approval"]["runtimeAuthority"])
        self.assertEqual(manifest["readinessBoundary"]["state"], "pending_owner_visual_approval")
        self.assertEqual(manifest["declaredTotals"]["familyCount"], 54)
        self.assertEqual(manifest["provider"], "Grok")
        self.assertEqual(manifest["model"], "4.6 High")

    def test_validate_integrity_mode_imports(self) -> None:
        import validate_packet

        self.assertTrue(callable(validate_packet.validate))
        self.assertEqual(validate_packet.EXPECTED_FAMILY_COUNT, 54)

    def test_sabotage_approve_without_review_is_integrity_error(self) -> None:
        import validate_packet

        if not (PACKET / "family_sheet_manifest_v001.json").is_file():
            self.skipTest("manifest not generated yet")
        original = json.loads((PACKET / "family_sheet_manifest_v001.json").read_text(encoding="utf-8"))
        broken = json.loads(json.dumps(original))
        broken["approval"]["decision"] = "APPROVE"
        broken["approval"]["independentReviewId"] = "pending"
        broken["readinessBoundary"]["state"] = "approved_2d_source_only"
        tmp = Path(tempfile.mkdtemp())
        try:
            for name in (".gitattributes", "families_v001.json"):
                shutil.copy2(PACKET / name, tmp / name)
            (tmp / "family_sheet_manifest_v001.json").write_text(json.dumps(broken, indent=2) + "\n", encoding="utf-8")
            old_root = validate_packet.ROOT
            old_fam = validate_packet.FAMILIES_PATH
            old_man = validate_packet.MANIFEST_PATH
            old_rep = validate_packet.REPORT_PATH
            validate_packet.ROOT = tmp
            validate_packet.FAMILIES_PATH = tmp / "families_v001.json"
            validate_packet.MANIFEST_PATH = tmp / "family_sheet_manifest_v001.json"
            validate_packet.REPORT_PATH = tmp / "validation_report_v001.json"
            try:
                report = validate_packet.validate(require_review=False)
            finally:
                validate_packet.ROOT = old_root
                validate_packet.FAMILIES_PATH = old_fam
                validate_packet.MANIFEST_PATH = old_man
                validate_packet.REPORT_PATH = old_rep
            joined = " ".join(report["errors"])
            self.assertFalse(report["integrityOk"])
            self.assertIn("APPROVE without independent review id", joined)
        finally:
            shutil.rmtree(tmp)

    def test_sabotage_meshy_true_is_integrity_error(self) -> None:
        import validate_packet

        if not (PACKET / "family_sheet_manifest_v001.json").is_file():
            self.skipTest("manifest not generated yet")
        original = json.loads((PACKET / "family_sheet_manifest_v001.json").read_text(encoding="utf-8"))
        broken = json.loads(json.dumps(original))
        broken["approval"]["meshyAuthorized"] = True
        tmp = Path(tempfile.mkdtemp())
        try:
            shutil.copy2(PACKET / ".gitattributes", tmp / ".gitattributes")
            shutil.copy2(PACKET / "families_v001.json", tmp / "families_v001.json")
            (tmp / "family_sheet_manifest_v001.json").write_text(json.dumps(broken, indent=2) + "\n", encoding="utf-8")
            old_root = validate_packet.ROOT
            old_fam = validate_packet.FAMILIES_PATH
            old_man = validate_packet.MANIFEST_PATH
            old_rep = validate_packet.REPORT_PATH
            validate_packet.ROOT = tmp
            validate_packet.FAMILIES_PATH = tmp / "families_v001.json"
            validate_packet.MANIFEST_PATH = tmp / "family_sheet_manifest_v001.json"
            validate_packet.REPORT_PATH = tmp / "validation_report_v001.json"
            try:
                report = validate_packet.validate(require_review=False)
            finally:
                validate_packet.ROOT = old_root
                validate_packet.FAMILIES_PATH = old_fam
                validate_packet.MANIFEST_PATH = old_man
                validate_packet.REPORT_PATH = old_rep
            self.assertFalse(report["integrityOk"])
            self.assertTrue(any("Meshy" in err for err in report["errors"]))
        finally:
            shutil.rmtree(tmp)


if __name__ == "__main__":
    unittest.main()
