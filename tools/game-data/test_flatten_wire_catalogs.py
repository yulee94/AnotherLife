#!/usr/bin/env python3
"""Regression checks for tools/game-data/flatten_wire_catalogs.py."""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import flatten_wire_catalogs as flatten  # noqa: E402


def main() -> int:
    before = flatten.snapshot_protected()
    generated = flatten.generate_all()
    errors = flatten.validate_generated(generated)
    errors.extend(flatten.check_on_disk(generated))
    errors.extend(flatten.assert_protected_unchanged(before))

    expected_counts = {
        "realm_specialized": (13, 13),
        "character_customization": (114, 188),
        "skill_weather": (13, 26),
    }
    for family, (records, aliases) in expected_counts.items():
        envelope = generated[family]
        if len(envelope["records"]) != records:
            errors.append(
                f"{family}: expected {records} records, got {len(envelope['records'])}"
            )
        if len(envelope["aliases"]) != aliases:
            errors.append(
                f"{family}: expected {aliases} aliases, got {len(envelope['aliases'])}"
            )

    leftover = {"realms.v1.json", "skills.v1.json", "catalog-set.json"}
    for name in leftover:
        if (flatten.GAMEDATA / name).exists():
            errors.append(f"unexpected six-family merge artifact in AL GameData: {name}")

    if errors:
        print("FAILED:")
        for error in errors:
            print(f"  - {error}")
        return 1
    print("flatten_wire_catalogs regression OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
