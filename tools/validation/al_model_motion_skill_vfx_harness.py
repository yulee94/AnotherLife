#!/usr/bin/env python3
"""CLI entry for the model/motion/skill-VFX validation harness."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "unity" / "SharedContracts" / "Tests"))

import model_motion_skill_vfx_harness as harness  # noqa: E402


if __name__ == "__main__":
    raise SystemExit(harness.main())
