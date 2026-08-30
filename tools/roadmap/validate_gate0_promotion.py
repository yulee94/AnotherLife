#!/usr/bin/env python3
"""Validate the owner-approved Gate 0 roadmap baseline promotion."""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path


CANDIDATE_ID = "RC-20260828-002"
PARENT_BASELINE_ID = "RB-20260828-001"
PROMOTED_BASELINE_ID = "RB-20260828-002"
CONTROL_ID = "GOV-G0-v1.0.0"
APPROVAL_ID = "AP-GR-GT-00-RC-20260828-002-003-001"
GATE_RECORD_ID = "GR-GT-00-RC-20260828-002-003"
OWNER_COMMENT_CREATED_AT = "1788129995"
EXACT_DECISION = (
    "OWNER DECISION (2026-08-31): APPROVE RC-20260828-002 as "
    "RB-20260828-002, including the exact integrated roadmap, authority "
    "matrix, gate ordering, and unresolved/fail-closed ledger."
)


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def canonical_sha256(path: Path) -> str:
    """Hash UTF-8 text with LF newlines, matching canonical Git content."""
    return hashlib.sha256(read(path).encode("utf-8")).hexdigest()


def parse_hash_inventory(text: str) -> dict[str, str]:
    return dict(
        re.findall(
            r"^\| `(unity/Docs/Roadmap/[^`]+)` \| `([0-9a-f]{64})` \|$",
            text,
            re.MULTILINE,
        )
    )


def main() -> int:
    arguments = [argument for argument in sys.argv[1:] if argument != "--print-hashes"]
    print_hashes = "--print-hashes" in sys.argv[1:]
    root = Path(arguments[0]).resolve() if arguments else Path(__file__).resolve().parents[2]
    roadmap = root / "unity" / "Docs" / "Roadmap"
    paths = {
        "candidate_change_set": roadmap / "Candidates" / CANDIDATE_ID / "change-set.md",
        "parent_baseline": roadmap / "Baselines" / PARENT_BASELINE_ID / "manifest.md",
        "promoted_baseline": roadmap / "Baselines" / PROMOTED_BASELINE_ID / "manifest.md",
        "approval": roadmap
        / "Approvals"
        / "GT-00"
        / CANDIDATE_ID
        / f"{APPROVAL_ID}.md",
        "gate": roadmap
        / "Gates"
        / "GT-00"
        / CANDIDATE_ID
        / f"{GATE_RECORD_ID}.md",
        "stop_ship": roadmap / "StopShip" / "SS-20260828-001" / "record.md",
        "audit": roadmap / "Gate0_Traceability_And_Authority_Audit_v3.md",
        "evidence": roadmap
        / "Evidence"
        / "GT-00"
        / CANDIDATE_ID
        / "EV-GT-00-RC-20260828-002-001"
        / "manifest.md",
    }
    errors: list[str] = []
    missing = [str(path) for path in paths.values() if not path.is_file()]
    if missing:
        print(json.dumps({"ok": False, "errors": [f"missing {path}" for path in missing]}, indent=2))
        return 1

    texts = {name: read(path) for name, path in paths.items()}
    candidate_inventory = parse_hash_inventory(texts["candidate_change_set"])
    promotion_inventory = parse_hash_inventory(texts["promoted_baseline"])

    if len(candidate_inventory) != 8:
        errors.append(f"expected 8 frozen candidate hashes, found {len(candidate_inventory)}")
    if len(promotion_inventory) != 5:
        errors.append(f"expected 5 promotion evidence hashes, found {len(promotion_inventory)}")

    all_inventories = {
        "candidate": candidate_inventory,
        "promotion": promotion_inventory,
    }
    if print_hashes:
        print(json.dumps(all_inventories, indent=2, sort_keys=True))
        return 0

    for inventory_name, inventory in all_inventories.items():
        for relative_path, expected_hash in inventory.items():
            artifact = root / relative_path
            if not artifact.is_file():
                errors.append(f"{inventory_name} artifact missing: {relative_path}")
                continue
            actual_hash = canonical_sha256(artifact)
            if actual_hash != expected_hash:
                errors.append(
                    f"{inventory_name} hash mismatch {relative_path}: "
                    f"{actual_hash} != {expected_hash}"
                )

    for identity in (CANDIDATE_ID, PARENT_BASELINE_ID, PROMOTED_BASELINE_ID, CONTROL_ID):
        if identity not in texts["promoted_baseline"]:
            errors.append(f"promoted baseline missing identity {identity}")
    for identity in (CANDIDATE_ID, PARENT_BASELINE_ID, APPROVAL_ID, GATE_RECORD_ID):
        if identity not in texts["approval"] or identity not in texts["gate"]:
            errors.append(f"approval/gate records missing identity {identity}")

    if EXACT_DECISION not in texts["approval"] or EXACT_DECISION not in texts["stop_ship"]:
        errors.append("verbatim owner decision missing from approval or stop-ship record")
    if OWNER_COMMENT_CREATED_AT not in texts["approval"] or OWNER_COMMENT_CREATED_AT not in texts["gate"]:
        errors.append("authoritative owner comment event identity missing")
    if "APPROVED — CURRENT AND IMMUTABLE" not in texts["promoted_baseline"]:
        errors.append("promoted baseline is not marked approved and immutable")
    if "APPROVED — GT-00 PASSED" not in texts["gate"]:
        errors.append("final Gate 0 record does not record approval and passage")
    if "CLOSED — RC-20260828-002 APPROVED AND PROMOTED" not in texts["stop_ship"]:
        errors.append("stop-ship record lacks append-only closure disposition")
    if "APPROVED — RETAINED" not in texts["parent_baseline"]:
        errors.append("parent baseline is not retained")
    if "U-01` through `U-12` remain" not in texts["promoted_baseline"]:
        errors.append("promoted baseline does not preserve the fail-closed unresolved ledger")
    if "t_0648ce23" not in texts["promoted_baseline"]:
        errors.append("promoted baseline does not identify the completed prior approval dependency")

    result = {
        "ok": not errors,
        "errors": errors,
        "candidate": CANDIDATE_ID,
        "parent_baseline": PARENT_BASELINE_ID,
        "promoted_baseline": PROMOTED_BASELINE_ID,
        "control": CONTROL_ID,
        "candidate_hashes_verified": len(candidate_inventory),
        "promotion_hashes_verified": len(promotion_inventory),
        "approval_record": APPROVAL_ID,
        "gate_record": GATE_RECORD_ID,
        "stop_ship": "SS-20260828-001",
    }
    print(json.dumps(result, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
