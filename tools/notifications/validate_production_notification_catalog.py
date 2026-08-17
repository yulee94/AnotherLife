#!/usr/bin/env python3
"""Validate the authoritative production notification catalog manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any


MANIFEST_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/"
    "al_notification_production_catalog.json"
)
SOURCE_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/"
    "al_notification_content_catalog.json"
)
ENTRY_ID = re.compile(r"^al_notify_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
BLOCKED_ID = re.compile(r"^notification_requirement_[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
ISSUE = re.compile(r"^#[1-9][0-9]*$")
EXPECTED_ROOT_KEYS = [
    "schemaVersion",
    "catalogId",
    "authority",
    "source",
    "requiredDefinitionIds",
    "entries",
    "blockedRequirements",
    "resolutionPolicy",
]
EXPECTED_ENTRY_KEYS = ["id", "sourceId", "consumers", "availability"]
EXPECTED_BLOCKED_KEYS = [
    "requirementId",
    "consumers",
    "dependencies",
    "reason",
    "requiredBeforeActivation",
]
EXPECTED_SOURCE_KEYS = [
    "path",
    "catalogId",
    "version",
    "sourcePacketId",
    "byteLength",
    "sha256",
]
EXPECTED_AVAILABLE_IDS = [
    "al_notify_save_recovered_backup",
    "al_notify_save_profile_degraded",
    "al_notify_save_unrecoverable",
    "al_notify_operation_unavailable",
    "al_notify_reward_committed",
    "al_notify_reward_failed",
    "al_notify_world_event_started",
    "al_notify_world_event_ended",
    "al_notify_bridge_unavailable",
    "al_notify_catalog_unavailable",
    "al_notify_content_unavailable",
]
EXPECTED_BLOCKED_REQUIREMENT_IDS = [
    "notification_requirement_production_packaging",
    "notification_requirement_durable_outbox",
    "notification_requirement_boss_reward_intents",
    "notification_requirement_wishgate_outcomes",
    "notification_requirement_world_event_cancelled",
    "notification_requirement_relationship_commit",
]


class ValidationError(RuntimeError):
    """Raised when notification authority is unavailable or invalid."""


@dataclass(frozen=True)
class ValidationResult:
    available_definition_ids: tuple[str, ...]
    available_definition_count: int
    blocked_requirement_count: int


def fail(message: str) -> None:
    raise ValidationError(f"production notification catalog validation failed: {message}")


def require_exact_keys(value: Any, expected: list[str], location: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        fail(f"{location} must be an object")
    if list(value) != expected:
        fail(f"{location} properties must be exactly {expected}; found {list(value)}")
    return value


def require_nonempty_string(value: Any, location: str) -> str:
    if not isinstance(value, str) or not value:
        fail(f"{location} must be a non-empty string")
    return value


def require_issues(value: Any, location: str) -> list[str]:
    if not isinstance(value, list) or not value:
        fail(f"{location} must name at least one issue")
    if any(not isinstance(item, str) or not ISSUE.fullmatch(item) for item in value):
        fail(f"{location} contains a malformed issue reference")
    if len(value) != len(set(value)):
        fail(f"{location} contains duplicate issue references")
    return value


def load_json_strict(path: Path, label: str) -> tuple[dict[str, Any], bytes]:
    try:
        raw = path.read_bytes()
    except OSError as error:
        fail(f"{label} unavailable: {path}: {error}")
    try:
        text = raw.decode("utf-8")
        value = json.loads(text, object_pairs_hook=_reject_duplicate_pairs)
    except (UnicodeDecodeError, json.JSONDecodeError, ValidationError) as error:
        fail(f"{label} malformed: {path}: {error}")
    if not isinstance(value, dict):
        fail(f"{label} root must be an object")
    return value, raw


def _reject_duplicate_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    output: dict[str, Any] = {}
    for key, value in pairs:
        if key in output:
            raise ValidationError(f"duplicate JSON member: {key}")
        output[key] = value
    return output


def validate_manifest(manifest: dict[str, Any], repo_root: Path) -> ValidationResult:
    require_exact_keys(manifest, EXPECTED_ROOT_KEYS, "$")
    if manifest["schemaVersion"] != 1:
        fail("unsupported manifest schemaVersion")
    if manifest["catalogId"] != "al_notification_production_catalog":
        fail("unexpected manifest catalogId")
    if manifest["authority"] != "production_notification_source_of_truth":
        fail("unexpected authority declaration")

    source = require_exact_keys(manifest["source"], EXPECTED_SOURCE_KEYS, "$.source")
    source_path = require_nonempty_string(source["path"], "$.source.path")
    if source_path != SOURCE_PATH.as_posix():
        fail(f"unexpected source path: {source_path}")
    source_file = repo_root / source_path
    source_document, source_raw = load_json_strict(source_file, "source content")
    digest = hashlib.sha256(source_raw).hexdigest()
    if (
        source["catalogId"] != source_document.get("catalogId")
        or source["version"] != source_document.get("version")
        or source["sourcePacketId"] != source_document.get("sourcePacketId")
        or source["byteLength"] != len(source_raw)
        or source["sha256"] != digest
    ):
        fail("source content identity mismatch; no fallback is permitted")

    source_definitions = source_document.get("definitions")
    if not isinstance(source_definitions, list):
        fail("source definitions must be an array")
    source_ids: list[str] = []
    source_ids_seen: set[str] = set()
    source_by_id: dict[str, dict[str, Any]] = {}
    for index, row in enumerate(source_definitions):
        if not isinstance(row, dict):
            fail(f"source definition {index} must be an object")
        definition_id = row.get("id")
        if not isinstance(definition_id, str) or not ENTRY_ID.fullmatch(definition_id):
            fail(f"malformed source definition id at index {index}")
        if definition_id in source_ids_seen:
            fail(f"duplicate source definition id: {definition_id}")
        source_ids_seen.add(definition_id)
        source_ids.append(definition_id)
        source_by_id[definition_id] = row

    required = manifest["requiredDefinitionIds"]
    if not isinstance(required, list) or any(not isinstance(item, str) for item in required):
        fail("requiredDefinitionIds must be an array of strings")
    if len(required) != len(set(required)):
        fail("requiredDefinitionIds contains duplicates")
    if required != EXPECTED_AVAILABLE_IDS:
        missing_expected = next((item for item in EXPECTED_AVAILABLE_IDS if item not in required), None)
        if missing_expected:
            fail(f"missing required entry: {missing_expected}")
        fail("requiredDefinitionIds contains an unreviewed entry")

    entries = manifest["entries"]
    if not isinstance(entries, list):
        fail("entries must be an array")
    entry_ids: list[str] = []
    seen_entries: set[str] = set()
    for index, raw_entry in enumerate(entries):
        entry = require_exact_keys(raw_entry, EXPECTED_ENTRY_KEYS, f"$.entries[{index}]")
        entry_id = require_nonempty_string(entry["id"], f"$.entries[{index}].id")
        if not ENTRY_ID.fullmatch(entry_id):
            fail(f"malformed entry id: {entry_id}")
        if entry_id in seen_entries:
            fail(f"duplicate entry id: {entry_id}")
        seen_entries.add(entry_id)
        entry_ids.append(entry_id)
        if entry_id not in source_by_id:
            fail(f"entry does not resolve in source content: {entry_id}")
        source_id = require_nonempty_string(entry["sourceId"], f"$.entries[{index}].sourceId")
        if source_by_id[entry_id].get("sourceId") != source_id:
            fail(f"entry sourceId drift: {entry_id}")
        require_issues(entry["consumers"], f"$.entries[{index}].consumers")
        if entry["availability"] != "available":
            fail(f"entry must be explicitly available: {entry_id}")

    for required_id in required:
        if required_id not in seen_entries:
            fail(f"missing required entry: {required_id}")
    if entry_ids != required or source_ids != required:
        fail("entry/source order or membership drifted from requiredDefinitionIds")

    blocked = manifest["blockedRequirements"]
    if not isinstance(blocked, list):
        fail("blockedRequirements must be an array")
    blocked_ids = [
        row.get("requirementId") if isinstance(row, dict) else None
        for row in blocked
    ]
    for expected_id in EXPECTED_BLOCKED_REQUIREMENT_IDS:
        if expected_id not in blocked_ids:
            fail(f"missing blocked requirement: {expected_id}")
    if blocked_ids != EXPECTED_BLOCKED_REQUIREMENT_IDS:
        fail("blockedRequirements contains an unreviewed entry or order drift")
    blocked_seen: set[str] = set()
    for index, raw_blocked in enumerate(blocked):
        row = require_exact_keys(raw_blocked, EXPECTED_BLOCKED_KEYS, f"$.blockedRequirements[{index}]")
        requirement_id = require_nonempty_string(
            row["requirementId"], f"$.blockedRequirements[{index}].requirementId"
        )
        if not BLOCKED_ID.fullmatch(requirement_id):
            fail(f"malformed blocked requirement id: {requirement_id}")
        if requirement_id in blocked_seen:
            fail(f"duplicate blocked requirement id: {requirement_id}")
        blocked_seen.add(requirement_id)
        require_issues(row["consumers"], f"$.blockedRequirements[{index}].consumers")
        dependencies = row["dependencies"]
        if not isinstance(dependencies, list) or not dependencies:
            fail(f"blocked requirement {requirement_id} must name at least one dependency")
        require_issues(dependencies, f"$.blockedRequirements[{index}].dependencies")
        require_nonempty_string(row["reason"], f"$.blockedRequirements[{index}].reason")
        if row["requiredBeforeActivation"] is not True:
            fail(f"blocked requirement {requirement_id} must remain required before activation")

    policy = manifest["resolutionPolicy"]
    expected_policy = {
        "unknownId": "explicit_failure",
        "missingSource": "explicit_failure",
        "sourceIdentityMismatch": "explicit_failure",
        "blockedRequirement": "explicit_failure",
        "fallbackContent": "prohibited",
    }
    if policy != expected_policy:
        fail("resolutionPolicy must remain fail-closed and prohibit fallback content")

    return ValidationResult(tuple(entry_ids), len(entry_ids), len(blocked))


def validate_repository(repo_root: Path) -> ValidationResult:
    manifest, _ = load_json_strict(repo_root / MANIFEST_PATH, "production manifest")
    return validate_manifest(manifest, repo_root)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    try:
        result = validate_repository(args.repo_root.resolve())
    except ValidationError as error:
        print(error)
        return 1
    print(
        "PASS: production notification catalog; "
        f"available={result.available_definition_count}; "
        f"blocked={result.blocked_requirement_count}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
