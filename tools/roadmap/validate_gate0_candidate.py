#!/usr/bin/env python3
"""Validate the immutable Gate 0 roadmap candidate package."""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path


CANDIDATE_ID = "RC-20260828-001"
BASELINE_ID = "RB-20260828-001"
CONTROL_ID = "GOV-G0-v1.0.0"
AUTHORITY_PREFIXES = (
    "GOV",
    "PROD",
    "VIS",
    "ACC",
    "LOC",
    "PLAT",
    "AUTO",
    "REG",
    "CAP",
    "REL",
    "GUILD",
    "ECON",
    "MON",
    "BAL",
    "PROG",
    "WORLD",
    "LIVE",
    "COMP",
)


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def main() -> int:
    root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
    roadmap = root / "unity" / "Docs" / "Roadmap"
    paths = {
        "governance": roadmap / "Gate0_Evidence_Governance_And_Stage_Gates_v1.md",
        "register": roadmap / "Gate0_Immutable_Authority_Register_v1.md",
        "dag": roadmap / "Gate0_Integrated_Delivery_DAG_v1.md",
        "audit_v1": roadmap / "Gate0_Traceability_And_Authority_Audit_v1.md",
        "baseline": roadmap / "Baselines" / BASELINE_ID / "manifest.md",
        "package": roadmap / "Candidates" / CANDIDATE_ID / "approval-package.md",
        "change_set": roadmap / "Candidates" / CANDIDATE_ID / "change-set.md",
        "gate": roadmap / "Gates" / "GT-00" / CANDIDATE_ID / f"GR-GT-00-{CANDIDATE_ID}-001.md",
        "rollback": roadmap / "Rollbacks" / f"RR-{CANDIDATE_ID}-001" / "record.md",
        "stop_ship": roadmap / "StopShip" / "SS-20260828-001" / "record.md",
    }
    errors: list[str] = []
    missing = [str(path) for path in paths.values() if not path.is_file()]
    if missing:
        print(json.dumps({"ok": False, "errors": [f"missing {path}" for path in missing]}, indent=2))
        return 1

    texts = {name: read(path) for name, path in paths.items()}
    for name in ("governance", "register", "dag"):
        for identity in (CONTROL_ID, CANDIDATE_ID, BASELINE_ID):
            if identity not in texts[name]:
                errors.append(f"{name} missing identity {identity}")
    if "owner-approved planning baseline" in texts["register"]:
        errors.append("authority register still claims approved roadmap-baseline status")

    prefixes = "|".join(AUTHORITY_PREFIXES)
    authority_pattern = re.compile(rf"^\| `?((?:{prefixes})-\d+)`? \|", re.MULTILINE)
    register_ids = authority_pattern.findall(texts["register"])
    dag_ids = authority_pattern.findall(texts["dag"])
    if len(register_ids) != 44 or len(set(register_ids)) != 44:
        errors.append(f"authority register expected 44 unique rows, found {len(register_ids)}/{len(set(register_ids))}")
    if len(dag_ids) != 44 or set(dag_ids) != set(register_ids):
        errors.append(
            f"DAG authority mapping mismatch: rows={len(dag_ids)}, "
            f"missing={sorted(set(register_ids) - set(dag_ids))}, "
            f"extra={sorted(set(dag_ids) - set(register_ids))}"
        )

    expected_unresolved = {f"{index:02d}" for index in range(1, 13)}
    for name in ("register", "audit_v1"):
        unresolved = set(re.findall(r"^\| `?U-(\d{2})`? \|", texts[name], re.MULTILINE))
        if unresolved != expected_unresolved:
            errors.append(f"{name} unresolved IDs: {sorted(unresolved)}")

    inventory = dict(
        re.findall(
            r"^\| `(unity/Docs/Roadmap/[^`]+)` \| `([0-9a-f]{64})` \|$",
            texts["change_set"],
            re.MULTILINE,
        )
    )
    if len(inventory) != 8:
        errors.append(f"expected 8 frozen hash entries, found {len(inventory)}")
    for relative_path, expected_hash in inventory.items():
        artifact = root / relative_path
        if not artifact.is_file():
            errors.append(f"hash inventory artifact missing: {relative_path}")
            continue
        actual_hash = hashlib.sha256(artifact.read_bytes()).hexdigest()
        if actual_hash != expected_hash:
            errors.append(f"hash mismatch {relative_path}: {actual_hash} != {expected_hash}")

    for forbidden in ("PENDING-FREEZE", "TBD"):
        if forbidden in texts["change_set"]:
            errors.append(f"change set contains forbidden placeholder {forbidden}")

    required_sections = (
        "Integrated roadmap and gate ordering",
        "Authority register and traceability",
        "Unresolved-ambiguity log",
        "Rollback and prior-baseline retention",
        "Reopen triggers",
        "Required owner decision",
    )
    for section in required_sections:
        if section not in texts["package"]:
            errors.append(f"approval package missing section: {section}")
    if "Stonehold -> Eldergrove -> Crownlands -> Umbral" not in texts["package"]:
        errors.append("approval package missing fixed realm order")
    if "t_0648ce23" not in texts["package"] or "t_a4c586ff" not in texts["package"]:
        errors.append("approval package missing approval source/task references")
    if "SS-20260828-001 — OPEN" not in texts["gate"]:
        errors.append("Gate 0 record does not remain fail-closed on open stop-ship incident")

    result = {
        "ok": not errors,
        "errors": errors,
        "authority_rows": len(register_ids),
        "dag_authority_rows": len(dag_ids),
        "unresolved_rows": len(expected_unresolved),
        "hashes_verified": len(inventory),
        "candidate": CANDIDATE_ID,
        "parent_baseline": BASELINE_ID,
        "control": CONTROL_ID,
    }
    print(json.dumps(result, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
