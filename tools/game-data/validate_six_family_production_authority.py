#!/usr/bin/env python3
"""Validate the current six-family production-authority ledger.

This validator audits immutable source tuples and evaluates generation eligibility.
It never writes catalogs, manifests, or runtime activation state.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any

DEFAULT_LEDGER_PATH = (
    "unity/Docs/GameDataCatalog/six-family-production-authority.v1.json"
)
FAMILY_ORDER = [
    "realms",
    "buildings",
    "research",
    "troops",
    "champions",
    "skills",
]
AUDITED_REVISION = "382a98f9a2f3ce6f8ee2283107cd593063243e2b"
SOURCE_SET_VERSION = "2026-09-04-v1"
REQUIRED_SOURCE_CONTRACTS = {
    "realms": [
        (
            "technical_source",
            "unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source-v003.json",
            "phase-c-v003",
            "accepted_technical_source",
        ),
        (
            "realm_decision",
            "unity/Docs/GameDataCatalog/PhaseC/Phase_C3H_Realm_V002_Technical_Source.md",
            "phase-c3h-v002",
            "accepted_decision",
        ),
        (
            "six_family_runtime_evidence",
            "unity/Assets/AL/StreamingAssets/GameData/realms.json",
            "1.0.0",
            "runtime_wire_evidence",
        ),
        (
            "specialized_runtime_evidence",
            "unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json",
            "0.1.0",
            "specialized_runtime_evidence",
        ),
    ],
    "buildings": [
        (
            "technical_source",
            "unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source-v003.json",
            "phase-c-v003",
            "accepted_technical_source",
        ),
        (
            "building_decision",
            "unity/Docs/GameDataCatalog/PhaseC/Phase_C4A_Building_Authority_Convergence.md",
            "phase-c4a-v1",
            "accepted_decision",
        ),
        (
            "six_family_runtime_evidence",
            "unity/Assets/AL/StreamingAssets/GameData/buildings.json",
            "1.0.0",
            "runtime_wire_evidence",
        ),
        (
            "building_art_evidence",
            "unity/Assets/AL/StreamingAssets/GameData/al_building_catalog.json",
            "0.1.0",
            "specialized_runtime_evidence",
        ),
    ],
    "research": [
        (
            "technical_source",
            "unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source-v003.json",
            "phase-c-v003",
            "accepted_technical_source",
        ),
        (
            "research_decision",
            "unity/Docs/GameDataCatalog/PhaseC/Phase_C5A_Research_Authority_Convergence.md",
            "phase-c5a-v1",
            "accepted_decision",
        ),
    ],
    "troops": [
        (
            "technical_source",
            "unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source-v003.json",
            "phase-c-v003",
            "accepted_technical_source",
        ),
        (
            "troop_decision",
            "unity/Docs/GameDataCatalog/PhaseC/Phase_C6A_Troop_Authority_Convergence.md",
            "phase-c6a-v1",
            "accepted_decision",
        ),
    ],
    "champions": [
        (
            "technical_source",
            "unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source-v003.json",
            "phase-c-v003",
            "accepted_technical_source",
        ),
        (
            "champion_decision",
            "unity/Docs/GameDataCatalog/PhaseC/Phase_C7A_Champion_Authority_Convergence.md",
            "phase-c7a-v1",
            "accepted_decision",
        ),
        (
            "six_family_runtime_evidence",
            "unity/Assets/AL/StreamingAssets/GameData/champions.json",
            "1.0.0",
            "runtime_wire_evidence",
        ),
        (
            "vertical_slice_runtime_evidence",
            "unity/Assets/AL/StreamingAssets/GameData/champion_runtime.json",
            "1.0.0",
            "runtime_wire_evidence",
        ),
        (
            "champion_identity_evidence",
            "unity/Assets/AL/StreamingAssets/GameData/al_champion_catalog.json",
            "0.1.0",
            "specialized_runtime_evidence",
        ),
    ],
    "skills": [
        (
            "technical_source",
            "unity/Docs/GameDataCatalog/PhaseC/phase-c-six-family-technical-source-v003.json",
            "phase-c-v003",
            "accepted_technical_source",
        ),
        (
            "skill_decision",
            "unity/Docs/GameDataCatalog/PhaseC/Phase_C8A_Skill_Authority_Convergence.md",
            "phase-c8a-v1",
            "accepted_decision",
        ),
        (
            "six_family_runtime_evidence",
            "unity/Assets/AL/StreamingAssets/GameData/skills.json",
            "1.0.0",
            "runtime_wire_evidence",
        ),
        (
            "wire_skill_runtime_evidence",
            "unity/Assets/AL/StreamingAssets/GameData/skill_weather.v1.json",
            "1.0.0",
            "runtime_wire_evidence",
        ),
        (
            "wire_schema_validator",
            "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataWireFamilySchemas.cs",
            "schema-v1",
            "schema_validator_evidence",
        ),
    ],
}
TRACKED_FAMILY_BLOCKER_CONTRACTS = {
    "realms": {},
    "buildings": {
        "buildings.production_profiles": ("behavior", "partial_runtime_only"),
        "buildings.asset_refs": ("asset", "partial"),
    },
    "research": {
        "research.max_levels": ("balance", "missing"),
        "research.cost_profiles": ("balance", "partial_migration_only"),
        "research.duration_profiles": ("balance", "partial_migration_only"),
        "research.effects": ("behavior", "partial_conflicting"),
        "research.prerequisites": ("behavior", "partial_migration_only"),
    },
    "troops": {
        "troops.records": ("identity", "missing"),
        "troops.localization": ("localization", "missing"),
        "troops.base_stats": ("balance", "missing"),
        "troops.training_profiles": ("behavior", "partial_runtime_only"),
        "troops.asset_refs": ("asset", "missing"),
    },
    "champions": {
        "champions.records": ("identity", "conflicting_runtime_only"),
        "champions.localization": ("localization", "partial_runtime_only"),
        "champions.realm_class_assignments": (
            "identity",
            "conflicting_runtime_only",
        ),
        "champions.asset_refs": ("asset", "partial_unpinned"),
        "champions.base_skill_refs": ("behavior", "conflicting_runtime_only"),
        "champions.stat_profiles": ("balance", "partial_runtime_only"),
    },
    "skills": {
        "skills.slot_policy": ("identity", "conflicting_runtime_only"),
        "skills.behavior_profiles": ("behavior", "partial_runtime_only"),
        "skills.presentation_profiles": ("behavior", "partial_runtime_only"),
        "skills.target_authority": ("behavior", "partial_runtime_only"),
        "skills.audio_asset_refs": ("asset", "missing"),
        "skills.vfx_asset_refs": ("asset", "partial_runtime_only"),
        "skills.balance_acceptance": ("balance", "partial_runtime_only"),
    },
}
TRACKED_FAMILY_BLOCKERS = {
    family: list(contracts)
    for family, contracts in TRACKED_FAMILY_BLOCKER_CONTRACTS.items()
}
HISTORICAL_FAMILY_BLOCKERS = {
    "realms": [
        "realms.rare_resource_catalog",
        "realms.capability_profiles",
        "realms.asset_refs",
    ],
    "buildings": [
        "buildings.max_level_review",
        "buildings.cost_profiles",
        "buildings.duration_profiles",
    ],
    "research": [],
    "troops": [],
    "champions": [],
    "skills": [],
}
GLOBAL_BLOCKER_ID = "approval.user_creative_balance"
BLOCKER_TYPES = {
    "identity",
    "behavior",
    "balance",
    "localization",
    "asset",
    "approval",
}
EVIDENCE_STATUSES = {
    "missing",
    "partial",
    "partial_unpinned",
    "partial_runtime_only",
    "partial_migration_only",
    "partial_conflicting",
    "conflicting_runtime_only",
    "not_requestable",
}
PROVENANCE_KINDS = {
    "accepted_technical_source",
    "accepted_decision",
    "runtime_wire_evidence",
    "specialized_runtime_evidence",
    "schema_validator_evidence",
}
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
REVISION_RE = re.compile(r"^[0-9a-f]{40}$")
STABLE_ID_RE = re.compile(r"^[a-z][a-z0-9]*(?:[._][a-z0-9]+)*$")


class ValidationError(RuntimeError):
    """Raised when ledger authority cannot be proven."""


@dataclass(frozen=True)
class ValidationResult:
    family_order: list[str]
    production_eligible: bool
    diagnostic_code: str
    output_paths: list[str]
    activation_targets: list[str]
    blocking_ids: list[str]
    checked_source_count: int
    source_set_sha256: str


def fail(message: str) -> None:
    raise ValidationError("six-family production authority validation failed: " + message)


def strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    value: dict[str, Any] = {}
    for key, item in pairs:
        if key in value:
            fail(f"duplicate property {key!r}")
        value[key] = item
    return value


def canonical_json(value: dict[str, Any]) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, separators=(",", ": "))
        + "\n"
    ).encode("utf-8")


def sha256(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def exact_keys(value: dict[str, Any], keys: list[str], label: str) -> None:
    if list(value) != keys:
        fail(f"{label} keys must be {keys}, found {list(value)}")


def require_nonblank(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value or value.strip() != value:
        fail(f"{label} must be a nonblank trimmed string")
    return value


def load_ledger_file(path: Path) -> dict[str, Any]:
    if not path.is_file():
        fail(f"ledger is missing: {path}")
    raw = path.read_bytes()
    if raw.startswith(b"\xef\xbb\xbf"):
        fail("ledger must be UTF-8 without a BOM")
    try:
        text = raw.decode("utf-8")
        value = json.loads(
            text,
            object_pairs_hook=strict_object,
            parse_constant=lambda token: fail(
                f"ledger contains non-finite number {token!r}"
            ),
        )
    except UnicodeDecodeError as error:
        fail(f"ledger is not valid UTF-8: {error}")
    except json.JSONDecodeError as error:
        fail(f"ledger is not strict JSON: {error}")
    if not isinstance(value, dict):
        fail("ledger root must be an object")
    normalized_text = text.replace("\r\n", "\n")
    if "\r" in normalized_text:
        fail("ledger contains a bare carriage return")
    if canonical_json(value) != normalized_text.encode("utf-8"):
        fail(
            "ledger bytes must be newline-normalized canonical deterministic JSON "
            "with one trailing LF"
        )
    return value


def verify_git_sources(sources: list[dict[str, Any]], repo_root: Path) -> None:
    unique: dict[tuple[str, str], str] = {}
    for source in sources:
        key = (source["sourceRevision"], source["path"])
        expected_hash = source["rawSha256"]
        prior_hash = unique.setdefault(key, expected_hash)
        if prior_hash != expected_hash:
            fail(f"conflicting source hashes for {source['path']!r}")

    requests = b"".join(
        f"{revision}:{path}\n".encode("utf-8")
        for revision, path in unique
    )
    result = subprocess.run(
        ["git", "cat-file", "--batch"],
        cwd=repo_root,
        input=requests,
        capture_output=True,
        check=False,
    )
    if result.returncode != 0:
        fail("Git could not read the pinned source set")

    offset = 0
    for (revision, relative_path), expected_hash in unique.items():
        line_end = result.stdout.find(b"\n", offset)
        if line_end < 0:
            fail(f"Git returned an incomplete header for {relative_path!r}")
        header = result.stdout[offset:line_end].decode("utf-8", errors="replace")
        parts = header.rsplit(" ", 2)
        if len(parts) != 3 or parts[1] != "blob" or not parts[2].isdigit():
            fail(
                f"source revision {revision!r} cannot resolve "
                f"{relative_path!r}: {header}"
            )
        size = int(parts[2])
        content_start = line_end + 1
        content_end = content_start + size
        if content_end >= len(result.stdout) or result.stdout[content_end] != 0x0A:
            fail(f"Git returned incomplete blob bytes for {relative_path!r}")
        if sha256(result.stdout[content_start:content_end]) != expected_hash:
            fail(
                f"source hash does not match revision {revision!r} for "
                f"{relative_path!r}"
            )
        offset = content_end + 1
    if offset != len(result.stdout):
        fail("Git returned unexpected bytes after the pinned source set")

    paths = [path for _, path in unique]
    diff = subprocess.run(
        ["git", "diff", "--quiet", AUDITED_REVISION, "--", *paths],
        cwd=repo_root,
        check=False,
    )
    if diff.returncode == 1:
        fail("current source set differs from the pinned audited revision")
    if diff.returncode != 0:
        fail("Git could not compare the pinned source set")


def validate_source(
    source: dict[str, Any],
    repo_root: Path,
    family_name: str,
    index: int,
    expected_contract: tuple[str, str, str, str],
) -> dict[str, Any]:
    label = f"{family_name}.sources[{index}]"
    exact_keys(
        source,
        [
            "role",
            "path",
            "version",
            "sourceRevision",
            "rawSha256",
            "provenance",
        ],
        label,
    )
    role = require_nonblank(source["role"], label + ".role")
    relative_path = require_nonblank(source["path"], label + ".path")
    version = require_nonblank(source["version"], label + ".version")
    revision = require_nonblank(
        source["sourceRevision"], label + ".sourceRevision"
    )
    expected_hash = require_nonblank(
        source["rawSha256"], label + ".rawSha256"
    )
    provenance = require_nonblank(source["provenance"], label + ".provenance")

    if (
        role,
        relative_path,
        version,
        provenance,
    ) != expected_contract:
        fail(
            f"{family_name} source role inventory drifted: expected "
            f"{expected_contract!r}, found "
            f"{(role, relative_path, version, provenance)!r}"
        )

    pure_path = PurePosixPath(relative_path)
    if (
        pure_path.is_absolute()
        or ".." in pure_path.parts
        or "\\" in relative_path
        or relative_path != str(pure_path)
    ):
        fail(f"{label}.path must be a canonical repository-relative POSIX path")
    if not REVISION_RE.fullmatch(revision):
        fail(f"source revision is not a 40-character lowercase Git id: {revision!r}")
    if revision != AUDITED_REVISION:
        fail(f"{label}.sourceRevision must equal auditedRevision")
    if not SHA256_RE.fullmatch(expected_hash):
        fail(f"source hash is not a lowercase SHA-256: {expected_hash!r}")
    if provenance not in PROVENANCE_KINDS:
        fail(f"{label} has unsupported provenance {provenance!r}")

    path = repo_root / relative_path
    if not path.is_file():
        fail(f"source path is missing: {relative_path!r}")

    return dict(source)


def validate_blocker(
    blocker: dict[str, Any],
    expected_id: str,
    family_name: str,
    expected_contract: tuple[str, str],
) -> None:
    exact_keys(
        blocker,
        ["id", "type", "status", "evidenceStatus", "reason", "recommendation"],
        expected_id,
    )
    if blocker["id"] != expected_id:
        fail(f"expected blocker {expected_id!r}, found {blocker['id']!r}")
    blocker_type = blocker["type"]
    if blocker_type not in BLOCKER_TYPES:
        fail(f"unsupported blocker type {blocker_type!r} for {expected_id}")
    if blocker_type == "approval" and family_name != "approval":
        fail(f"family blocker {expected_id!r} cannot use approval type")
    if blocker["status"] != "open":
        fail(f"blocker {expected_id!r} must remain open while present")
    if blocker["evidenceStatus"] not in EVIDENCE_STATUSES:
        fail(
            f"blocker {expected_id!r} has unsupported evidenceStatus "
            f"{blocker['evidenceStatus']!r}"
        )
    if (blocker_type, blocker["evidenceStatus"]) != expected_contract:
        fail(f"blocker {expected_id!r} classification drifted")
    require_nonblank(blocker["reason"], expected_id + ".reason")
    require_nonblank(blocker["recommendation"], expected_id + ".recommendation")


def validate_family(
    family: dict[str, Any],
    expected_family: str,
    repo_root: Path,
) -> tuple[list[str], list[dict[str, Any]]]:
    exact_keys(
        family,
        [
            "family",
            "disposition",
            "sources",
            "resolvedBlockerIds",
            "historicalResolvedIds",
            "blockers",
        ],
        expected_family,
    )
    if family["family"] != expected_family:
        fail(
            f"family order drifted: expected {expected_family!r}, "
            f"found {family['family']!r}"
        )
    if family["disposition"] not in {"blocked_required", "source_complete"}:
        fail(f"unsupported disposition for {expected_family}")

    expected_ids = TRACKED_FAMILY_BLOCKERS[expected_family]
    blockers = family["blockers"]
    resolved = family["resolvedBlockerIds"]
    historical = family["historicalResolvedIds"]
    if not isinstance(blockers, list) or not isinstance(resolved, list):
        fail(f"{expected_family} blocker and resolution lists must be arrays")
    if historical != HISTORICAL_FAMILY_BLOCKERS[expected_family]:
        fail(f"{expected_family}.historicalResolvedIds drifted")

    blocker_ids = [blocker.get("id") for blocker in blockers if isinstance(blocker, dict)]
    if len(blocker_ids) != len(blockers):
        fail(f"{expected_family}.blockers must contain only objects")
    if resolved:
        fail("schemaVersion 1 does not accept resolved blockers without resolution evidence")
    if blocker_ids != expected_ids:
        fail(f"{expected_family} blocker ledger omitted or reordered a tracked decision")
    for blocker, expected_id in zip(blockers, blocker_ids):
        validate_blocker(
            blocker,
            expected_id,
            expected_family,
            TRACKED_FAMILY_BLOCKER_CONTRACTS[expected_family][expected_id],
        )

    expected_disposition = "blocked_required" if blockers else "source_complete"
    if family["disposition"] != expected_disposition:
        fail(
            f"{expected_family} disposition must be {expected_disposition!r} "
            "for its blocker state"
        )

    sources = family["sources"]
    expected_sources = REQUIRED_SOURCE_CONTRACTS[expected_family]
    if not isinstance(sources, list) or len(sources) != len(expected_sources):
        fail(f"{expected_family} source role inventory drifted")
    source_rows: list[dict[str, Any]] = []
    source_keys: set[tuple[str, str]] = set()
    for index, (source, expected_source) in enumerate(
        zip(sources, expected_sources)
    ):
        if not isinstance(source, dict):
            fail(f"{expected_family}.sources[{index}] must be an object")
        key = (source.get("role"), source.get("path"))
        if key in source_keys:
            fail(f"duplicate source role/path in {expected_family}: {key!r}")
        source_keys.add(key)
        source_rows.append(
            validate_source(
                source,
                repo_root,
                expected_family,
                index,
                expected_source,
            )
        )
    return blocker_ids, source_rows


def source_set_sha256(ledger: dict[str, Any]) -> str:
    fingerprint_payload = {
        "schemaVersion": ledger["schemaVersion"],
        "ledgerId": ledger["ledgerId"],
        "auditedRevision": ledger["auditedRevision"],
        "sourceSetVersion": ledger["sourceSetVersion"],
        "familyOrder": ledger["familyOrder"],
        "families": ledger["families"],
        "approvalBlocker": ledger["approval"]["blocker"],
    }
    canonical = (
        json.dumps(
            fingerprint_payload,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("utf-8")
    return sha256(canonical)


def validate_ledger(
    ledger: dict[str, Any],
    repo_root: Path,
) -> ValidationResult:
    exact_keys(
        ledger,
        [
            "schemaVersion",
            "ledgerId",
            "auditedRevision",
            "sourceSetVersion",
            "sourceSetSha256",
            "familyOrder",
            "productionEligible",
            "families",
            "approval",
            "generationGate",
        ],
        "$",
    )
    if ledger["schemaVersion"] != 1:
        fail("schemaVersion must be 1")
    if ledger["ledgerId"] != "al_six_family_production_authority_v1":
        fail("ledgerId drifted")
    audited_revision = require_nonblank(ledger["auditedRevision"], "auditedRevision")
    if audited_revision != AUDITED_REVISION:
        fail(f"auditedRevision drifted from {AUDITED_REVISION}")
    if ledger["sourceSetVersion"] != SOURCE_SET_VERSION:
        fail(f"sourceSetVersion drifted from {SOURCE_SET_VERSION}")
    if not SHA256_RE.fullmatch(str(ledger["sourceSetSha256"])):
        fail("sourceSetSha256 must be a lowercase SHA-256")
    if ledger["familyOrder"] != FAMILY_ORDER:
        fail("familyOrder drifted")
    if not isinstance(ledger["productionEligible"], bool):
        fail("productionEligible must be Boolean")

    approval = ledger["approval"]
    if not isinstance(approval, dict):
        fail("approval must be an object")
    exact_keys(
        approval,
        ["state", "reviewedSourceSetSha256", "blocker"],
        "approval",
    )
    if approval["state"] != "pending":
        fail("schemaVersion 1 requires pending approval")
    if approval["reviewedSourceSetSha256"] is not None:
        fail("pending approval cannot identify an approved source-set hash")
    if not isinstance(approval["blocker"], dict):
        fail("pending approval must retain its typed blocker")
    validate_blocker(
        approval["blocker"],
        GLOBAL_BLOCKER_ID,
        "approval",
        ("approval", "not_requestable"),
    )

    calculated_source_set = source_set_sha256(ledger)
    if ledger["sourceSetSha256"] != calculated_source_set:
        fail(
            "source-set fingerprint drifted: expected "
            f"{calculated_source_set}, found {ledger['sourceSetSha256']}"
        )

    if ledger["productionEligible"] is not False:
        fail("schemaVersion 1 requires productionEligible=false")
    gate = ledger["generationGate"]
    if not isinstance(gate, dict):
        fail("generationGate must be an object")
    exact_keys(gate, ["status", "outputPaths", "activationTargets"], "generationGate")
    if gate["status"] != "blocked":
        fail("schemaVersion 1 requires generationGate.status='blocked'")
    if gate["outputPaths"] != [] or gate["activationTargets"] != []:
        fail("this ledger-only gate must emit zero writes and activations")

    families = ledger["families"]
    if not isinstance(families, list) or len(families) != len(FAMILY_ORDER):
        fail("families must contain exactly six rows")
    blocker_ids: list[str] = []
    source_rows: list[dict[str, Any]] = []
    for family, expected_name in zip(families, FAMILY_ORDER):
        if not isinstance(family, dict):
            fail(f"family row {expected_name!r} must be an object")
        family_blockers, family_sources = validate_family(
            family,
            expected_name,
            repo_root,
        )
        blocker_ids.extend(family_blockers)
        source_rows.extend(family_sources)

    verify_git_sources(source_rows, repo_root)
    blocker_ids.append(GLOBAL_BLOCKER_ID)

    eligible = False

    return ValidationResult(
        family_order=list(FAMILY_ORDER),
        production_eligible=eligible,
        diagnostic_code="AL-GDA-ELIGIBLE" if eligible else "AL-GDA-BLOCKED",
        output_paths=list(gate["outputPaths"]),
        activation_targets=list(gate["activationTargets"]),
        blocking_ids=blocker_ids,
        checked_source_count=len(source_rows),
        source_set_sha256=calculated_source_set,
    )


def validate_ledger_file(path: Path, repo_root: Path) -> ValidationResult:
    return validate_ledger(load_ledger_file(path), repo_root)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ledger", default=DEFAULT_LEDGER_PATH)
    parser.add_argument("--require-production-eligible", action="store_true")
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[2]
    ledger_path = Path(args.ledger)
    if not ledger_path.is_absolute():
        ledger_path = repo_root / ledger_path
    try:
        result = validate_ledger_file(ledger_path, repo_root)
    except ValidationError as error:
        print(error, file=sys.stderr)
        return 1

    print(
        f"{result.diagnostic_code}: checked {result.checked_source_count} immutable "
        f"source tuples; source set {result.source_set_sha256}"
    )
    print(
        f"{result.diagnostic_code}: {len(result.blocking_ids)} blockers; "
        "0 output paths; 0 activation targets"
    )
    if args.require_production_eligible and not result.production_eligible:
        print(
            "AL-GDA-BLOCKED: production generation refused without writes or "
            "runtime activation",
            file=sys.stderr,
        )
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
