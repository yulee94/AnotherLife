from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.dont_write_bytecode = True

from PIL import Image

import validate_packet as vp


GITATTRIBUTES_BODY = (
    ".gitattributes text eol=lf\n"
    "*.md text eol=lf\n"
    "*.json text eol=lf\n"
    "*.py text eol=lf\n"
    "*.png filter=lfs diff=lfs merge=lfs -text\n"
)


def _write_png(path: Path, size: tuple[int, int] = (8, 8)) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.new("RGB", size, (12, 14, 18)).save(path, format="PNG")


def _write_gitattributes(root: Path, body: str = GITATTRIBUTES_BODY) -> None:
    (root / ".gitattributes").write_bytes(body.encode("utf-8"))


class PacketPolicyTests(unittest.TestCase):
    def test_gitattributes_must_pin_png_lfs_and_text_lf(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            _write_gitattributes(root, "*.png text\n*.md text\n")
            errors = vp.check_gitattributes(root)
            self.assertTrue(any("lfs" in error.lower() for error in errors), errors)
            self.assertTrue(any("eol=lf" in error.lower() or "lf" in error.lower() for error in errors), errors)

    def test_missing_gitattributes_fails(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            errors = vp.check_gitattributes(root)
            self.assertTrue(any("gitattributes" in error.lower() for error in errors), errors)

    def test_cache_and_temp_files_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            (root / "__pycache__").mkdir()
            (root / "__pycache__" / "validate_packet.cpython-311.pyc").write_bytes(b"\0")
            (root / "scratch.tmp").write_text("x\n", encoding="utf-8")
            errors = vp.check_forbidden_files(root)
            joined = " ".join(errors).lower()
            self.assertIn("__pycache__", joined)
            self.assertTrue("tmp" in joined or "scratch.tmp" in joined, errors)

    def test_exact_file_set_rejects_unmanifested_extra(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            _write_gitattributes(root)
            (root / "README.md").write_bytes(b"ok\n")
            extra = root / "notes.txt"
            extra.write_bytes(b"nope\n")
            artifacts = [{"path": "README.md"}]
            errors = vp.check_exact_file_set(root, artifacts)
            self.assertTrue(any("notes.txt" in error for error in errors), errors)

    def test_gitattributes_is_the_only_allowed_unmanifested_file(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            _write_gitattributes(root)
            (root / "README.md").write_bytes(b"ok\n")
            artifacts = [{"path": "README.md"}]
            errors = vp.check_exact_file_set(root, artifacts)
            self.assertEqual(errors, [])

    def test_review_gates_fail_closed_on_pending(self) -> None:
        manifest = {
            "approval": {
                "decision": "PENDING",
                "independentReviewVerdict": "PENDING",
                "independentReviewId": "pending",
                "summary": "Independent visual/source review pending.",
            },
            "npcs": [],
        }
        errors = vp.check_review_gates(manifest)
        self.assertTrue(any("APPROVE" in error or "PASS" in error for error in errors), errors)
        self.assertTrue(any("pending" in error.lower() for error in errors), errors)
        self.assertFalse(any("runtime" in error.lower() and "true" in error.lower() for error in errors))


if __name__ == "__main__":
    sys.exit(unittest.main(verbosity=2))
