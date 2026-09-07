"""Verify shared reference custody; no DCC launch or production approval."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]


def verify(records: list[dict], root: Path) -> list[str]:
    errors = []
    seen = set()
    for record in records:
        rel = record["path"]
        if rel in seen:
            errors.append(f"duplicate: {rel}")
            continue
        seen.add(rel)
        path = (root / rel).resolve()
        if not path.is_relative_to(root.resolve()):
            errors.append(f"outside repository: {rel}")
            continue
        if not path.is_file():
            errors.append(f"missing: {rel}")
            continue
        data = path.read_bytes()
        if data.startswith(b"version https://git-lfs.github.com/spec/v1\n"):
            errors.append(f"LFS download required: {rel}")
            continue
        if record.get("hashMode") == "utf8-lf":
            data = data.replace(b"\r\n", b"\n")
        if len(data) != record["bytes"]:
            errors.append(f"size mismatch: {rel}")
        if hashlib.sha256(data).hexdigest() != record["sha256"]:
            errors.append(f"hash mismatch: {rel}")
    return errors


def main() -> int:
    inventory = json.loads((HERE / "inventory.json").read_text(encoding="utf-8"))
    records = inventory["files"]
    errors = verify(records, REPO)
    if len(records) != inventory["fileCount"]:
        errors.append("inventory count mismatch")
    if errors:
        print("FAIL\n" + "\n".join(errors))
        return 1
    print(f"PASS: {len(records)} shared reference files; custody only, not runtime acceptance")
    return 0


if __name__ == "__main__":
    sys.exit(main())
