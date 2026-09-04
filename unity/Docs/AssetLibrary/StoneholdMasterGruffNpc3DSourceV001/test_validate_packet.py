"""Tests for the Stonehold Master Gruff 3D foundation packet validator."""
from __future__ import annotations

import unittest
from pathlib import Path
import runpy


PACKET = Path(__file__).resolve().parent


class ValidatePacketTests(unittest.TestCase):
    def test_validator_pass(self) -> None:
        result = runpy.run_path(str(PACKET / "validate_packet.py"))
        self.assertEqual(result["main"](), 0)


if __name__ == "__main__":
    unittest.main()
