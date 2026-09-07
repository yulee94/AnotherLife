"""Failure-path tests for the sharing-only custody verifier."""
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("reference_verify", Path(__file__).with_name("verify.py"))
VERIFY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFY)


class CustodyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / "asset.txt").write_bytes(b"source\n")
        self.record = {"path": "asset.txt", "bytes": 7,
                       "sha256": hashlib.sha256(b"source\n").hexdigest(),
                       "hashMode": "utf8-lf"}

    def test_valid(self):
        self.assertEqual([], VERIFY.verify([self.record], self.root))

    def test_portable_crlf(self):
        (self.root / "asset.txt").write_bytes(b"source\r\n")
        self.assertEqual([], VERIFY.verify([self.record], self.root))

    def test_missing(self):
        (self.root / "asset.txt").unlink()
        self.assertIn("missing", VERIFY.verify([self.record], self.root)[0])

    def test_tamper_same_size(self):
        (self.root / "asset.txt").write_bytes(b"tamper\n")
        self.assertIn("hash mismatch", VERIFY.verify([self.record], self.root)[0])

    def test_size(self):
        (self.root / "asset.txt").write_bytes(b"short")
        self.assertTrue(any("size mismatch" in e for e in VERIFY.verify([self.record], self.root)))

    def test_duplicate(self):
        self.assertIn("duplicate", VERIFY.verify([self.record, self.record], self.root)[0])

    def test_escape(self):
        self.record["path"] = "../outside.txt"
        self.assertIn("outside repository", VERIFY.verify([self.record], self.root)[0])

    def test_lfs_pointer(self):
        (self.root / "asset.txt").write_bytes(b"version https://git-lfs.github.com/spec/v1\n")
        self.assertIn("LFS download required", VERIFY.verify([self.record], self.root)[0])

    def test_binary_bytes_not_normalized(self):
        self.record["hashMode"] = "raw"
        (self.root / "asset.txt").write_bytes(b"source\r\n")
        self.assertTrue(VERIFY.verify([self.record], self.root))


if __name__ == "__main__":
    unittest.main()
