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

    def test_owner_approve_is_2d_source_only(self) -> None:
        if not (PACKET / "family_sheet_manifest_v001.json").is_file():
            self.skipTest("manifest not generated yet")
        manifest = json.loads((PACKET / "family_sheet_manifest_v001.json").read_text(encoding="utf-8"))
        self.assertEqual(manifest["approval"]["decision"], "APPROVE")
        self.assertEqual(manifest["approval"]["independentReviewVerdict"], "PASS")
        self.assertNotEqual(manifest["approval"]["independentReviewId"], "pending")
        self.assertFalse(manifest["approval"]["meshyAuthorized"])
        self.assertFalse(manifest["approval"]["runtimeAuthority"])
        self.assertFalse(manifest["approval"]["releaseAuthority"])
        self.assertEqual(manifest["readinessBoundary"]["state"], "approved_2d_source_only")
        self.assertEqual(manifest["declaredTotals"]["familyCount"], 54)
        self.assertEqual(manifest["provider"], "Grok")
        self.assertEqual(manifest["model"], "4.6 High")

    def test_generation_log_matches_catalog_and_manifest(self) -> None:
        catalog = json.loads((PACKET / "families_v001.json").read_text(encoding="utf-8"))
        manifest = json.loads((PACKET / "family_sheet_manifest_v001.json").read_text(encoding="utf-8"))
        log = json.loads((PACKET / "generation_log_v001.json").read_text(encoding="utf-8"))
        catalog_ids = [row["familyId"] for row in catalog["families"]]
        manifest_ids = [row["familyId"] for row in manifest["families"]]
        log_ids = [row["familyId"] for row in log["results"]]
        self.assertEqual(log_ids, catalog_ids)
        self.assertEqual(log_ids, manifest_ids)
        self.assertEqual(len(log_ids), 54)
        self.assertEqual(log["provider"], "Grok")
        self.assertEqual(log["model"], "4.6 High")
        self.assertEqual(log["errors"], [])
        for row in log["results"]:
            self.assertTrue(row["ok"])
            self.assertIsNone(row.get("fallback"))
            self.assertEqual(row["path"], f"Sheets/{row['familyId']}_family_sheet_v001.png")

    def test_validate_integrity_mode_imports(self) -> None:
        import validate_packet

        self.assertTrue(callable(validate_packet.validate))
        self.assertEqual(validate_packet.EXPECTED_FAMILY_COUNT, 54)

    def _run_on_tmp(self, tmp: Path, require_review: bool = False) -> dict:
        import validate_packet

        old_root = validate_packet.ROOT
        old_fam = validate_packet.FAMILIES_PATH
        old_man = validate_packet.MANIFEST_PATH
        old_rep = validate_packet.REPORT_PATH
        validate_packet.ROOT = tmp
        validate_packet.FAMILIES_PATH = tmp / "families_v001.json"
        validate_packet.MANIFEST_PATH = tmp / "family_sheet_manifest_v001.json"
        validate_packet.REPORT_PATH = tmp / "validation_report_v001.json"
        try:
            return validate_packet.validate(require_review=require_review)
        finally:
            validate_packet.ROOT = old_root
            validate_packet.FAMILIES_PATH = old_fam
            validate_packet.MANIFEST_PATH = old_man
            validate_packet.REPORT_PATH = old_rep

    def _copy_packet_texts(self, tmp: Path) -> None:
        for name in (
            ".gitattributes",
            "families_v001.json",
            "family_sheet_manifest_v001.json",
            "generation_log_v001.json",
        ):
            shutil.copy2(PACKET / name, tmp / name)

    def test_sabotage_approve_without_review_is_integrity_error(self) -> None:
        if not (PACKET / "family_sheet_manifest_v001.json").is_file():
            self.skipTest("manifest not generated yet")
        original = json.loads((PACKET / "family_sheet_manifest_v001.json").read_text(encoding="utf-8"))
        broken = json.loads(json.dumps(original))
        broken["approval"]["decision"] = "APPROVE"
        broken["approval"]["independentReviewId"] = "pending"
        broken["readinessBoundary"]["state"] = "approved_2d_source_only"
        tmp = Path(tempfile.mkdtemp())
        try:
            self._copy_packet_texts(tmp)
            (tmp / "family_sheet_manifest_v001.json").write_text(
                json.dumps(broken, indent=2) + "\n", encoding="utf-8"
            )
            report = self._run_on_tmp(tmp)
            joined = " ".join(report["errors"])
            self.assertFalse(report["integrityOk"])
            self.assertIn("APPROVE without independent review id", joined)
        finally:
            shutil.rmtree(tmp)

    def test_sabotage_meshy_true_is_integrity_error(self) -> None:
        if not (PACKET / "family_sheet_manifest_v001.json").is_file():
            self.skipTest("manifest not generated yet")
        original = json.loads((PACKET / "family_sheet_manifest_v001.json").read_text(encoding="utf-8"))
        broken = json.loads(json.dumps(original))
        broken["approval"]["meshyAuthorized"] = True
        tmp = Path(tempfile.mkdtemp())
        try:
            self._copy_packet_texts(tmp)
            (tmp / "family_sheet_manifest_v001.json").write_text(
                json.dumps(broken, indent=2) + "\n", encoding="utf-8"
            )
            report = self._run_on_tmp(tmp)
            self.assertFalse(report["integrityOk"])
            self.assertTrue(any("Meshy" in err for err in report["errors"]))
        finally:
            shutil.rmtree(tmp)

    def test_sabotage_truncated_generation_log_is_integrity_error(self) -> None:
        if not (PACKET / "generation_log_v001.json").is_file():
            self.skipTest("generation log not generated yet")
        original = json.loads((PACKET / "generation_log_v001.json").read_text(encoding="utf-8"))
        broken = json.loads(json.dumps(original))
        broken["results"] = broken["results"][:13]
        tmp = Path(tempfile.mkdtemp())
        try:
            self._copy_packet_texts(tmp)
            (tmp / "generation_log_v001.json").write_bytes(
                (json.dumps(broken, indent=2) + "\n").encode("utf-8")
            )
            report = self._run_on_tmp(tmp)
            joined = " ".join(report["errors"])
            self.assertFalse(report["integrityOk"])
            self.assertIn("generation-log family order/IDs do not match families_v001.json", joined)
        finally:
            shutil.rmtree(tmp)


if __name__ == "__main__":
    unittest.main()
