#!/usr/bin/env python3
"""Validate the immutable boss-to-reward technical source catalog.

This gate audits bindings, profiles, equipment snapshots, hashes, and
references. It never applies rewards, writes saves, or activates mutation.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

DEFAULT_CATALOG_PATH = Path(
    "unity/Assets/AL/StreamingAssets/GameData/al_boss_reward_source_catalog.json"
)
SCHEMA_VERSION = "boss_reward_schema_v1"
CATALOG_ID = "al_boss_reward_source_catalog"
GAME_ID = "another_life"
CATALOG_SET_ID = "catalog_set_boss_reward_source_v001"
REVISION = "boss_reward_source_v001"
AUTHORITY = "technical_boss_reward_source"
MUTATION_ACTIVATION = "blocked"
REPRESENTATIVE_BOSS_ID = "boss_stonehold_fault_crowned_colossus"
REPRESENTATIVE_PROFILE_ID = "reward_profile_stonehold_fault_crowned_colossus"
REPRESENTATIVE_EQUIPMENT_ID = "equipment_stonehold_fault_crowned_colossus_core"
TECHNICAL_ID = re.compile(r"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")
CONTENT_KEY = re.compile(r"^[a-z][a-z0-9]*(?:[._][a-z0-9]+)*$")
SHA256_HEX = re.compile(r"^[0-9a-f]{64}$")
ANNOUNCEMENT_POLICY_IDS = [
    "boss_reward.item_acquired",
    "boss_reward.credits_committed",
    "boss_reward.explicit_no_reward",
]
ROOT_KEYS = [
    "schemaVersion",
    "catalogId",
    "gameId",
    "catalogSetId",
    "revision",
    "authority",
    "mutationActivation",
    "approval",
    "announcementPolicyIds",
    "bindings",
    "profiles",
    "equipmentDefinitions",
]
APPROVAL_KEYS = [
    "mode",
    "issue",
    "representativeBossId",
    "sourceQualificationIssue",
]
BINDING_KEYS = [
    "bossDefinitionId",
    "bossDefinitionContentVersion",
    "rewardProfileId",
    "rewardProfileContentVersion",
]
PROFILE_KEYS = [
    "gameId",
    "catalogSetId",
    "id",
    "schemaVersion",
    "contentVersion",
    "warzoneCredits",
    "isExplicitNoReward",
    "entries",
    "sourceRevision",
    "rawSha256",
]
ENTRY_KEYS = [
    "equipmentDefinitionId",
    "dropChanceMicros",
    "quantity",
    "acquisitionAnnouncementPolicyId",
]
EQUIPMENT_KEYS = [
    "equipmentDefinitionId",
    "schemaVersion",
    "contentVersion",
    "slotId",
    "attackBonus",
    "defenseBonus",
    "healthBonus",
    "stackPolicyId",
    "acquisitionSnapshotPolicyId",
    "presentationContentKey",
    "sourceRevision",
    "rawSha256",
]
EXPECTED_SOURCE_BYTE_LENGTH = 2282
EXPECTED_SOURCE_SHA256 = (
    "5d6d8cfaf7a2253ec3885c3398024572aed1bac1c109eedbe0ac279e9a50633e"
)
MAXIMUM_CATALOG_BYTES = 65536
MICROS_PER_UNIT = 1_000_000
BOUNDED_CREDITS = 250
BOUNDED_QUANTITY = 1
BOUNDED_CHANCE_MICROS = MICROS_PER_UNIT


class ValidationError(RuntimeError):
    """Raised when the boss-reward technical source is unavailable or invalid."""


@dataclass(frozen=True)
class ResolvedBossReward:
    boss_definition_id: str
    reward_profile_id: str
    equipment_definition_ids: tuple[str, ...]
    warzone_credits: int
    quantities: tuple[int, ...]
    drop_chance_micros: tuple[int, ...]
    attack_bonus: int
    defense_bonus: int
    health_bonus: int
    presentation_content_key: str


@dataclass(frozen=True)
class ValidationResult:
    mutation_activation: str
    allows_mutation: bool
    activation_targets: list[str]
    resolved: ResolvedBossReward
    source_sha256: str
    source_byte_length: int


def fail(message: str) -> None:
    raise ValidationError(f"boss reward source validation failed: {message}")


def sha256_hex(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def write_u32(buffer: bytearray, value: int) -> None:
    buffer.extend((value & 0xFFFFFFFF).to_bytes(4, "big"))


def write_i32(buffer: bytearray, value: int) -> None:
    write_u32(buffer, value)


def write_bool(buffer: bytearray, value: bool) -> None:
    buffer.append(1 if value else 0)


def write_string(buffer: bytearray, value: str) -> None:
    data = value.encode("utf-8")
    write_u32(buffer, len(data))
    buffer.extend(data)


def require_object(value: Any, location: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        fail(f"{location} must be an object")
    return value


def require_exact_keys(value: Any, expected: list[str], location: str) -> dict[str, Any]:
    obj = require_object(value, location)
    if list(obj) != expected:
        fail(f"{location} properties must be exactly {expected}; found {list(obj)}")
    return obj


def require_string(value: Any, location: str) -> str:
    if not isinstance(value, str) or not value:
        fail(f"{location} must be a non-empty string")
    if value != value.strip():
        fail(f"{location} must not include surrounding whitespace")
    return value


def require_technical_id(value: Any, location: str) -> str:
    text = require_string(value, location)
    if not TECHNICAL_ID.fullmatch(text):
        fail(f"{location} is not a canonical technical id")
    return text


def require_content_key(value: Any, location: str) -> str:
    text = require_string(value, location)
    if not CONTENT_KEY.fullmatch(text):
        fail(f"{location} is not a bounded content key")
    return text


def require_bool(value: Any, location: str) -> bool:
    if not isinstance(value, bool):
        fail(f"{location} must be a boolean")
    return value


def require_int(value: Any, location: str, minimum: int, maximum: int) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        fail(f"{location} must be an integer")
    if value < minimum or value > maximum:
        fail(f"{location} is outside {minimum}..{maximum}")
    return value


def _reject_duplicate_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    output: dict[str, Any] = {}
    for key, value in pairs:
        if key in output:
            raise ValidationError(f"duplicate JSON member: {key}")
        output[key] = value
    return output


def load_json_strict(path: Path, label: str) -> tuple[dict[str, Any], bytes]:
    try:
        raw = path.read_bytes()
    except OSError as error:
        fail(f"{label} unavailable: {path}: {error}")
    if not raw:
        fail(f"{label} is empty")
    if len(raw) > MAXIMUM_CATALOG_BYTES:
        fail(f"{label} exceeds the byte ceiling")
    if raw.startswith(b"\xef\xbb\xbf"):
        fail(f"{label} must not include a UTF-8 BOM")
    try:
        text = raw.decode("utf-8")
        value = json.loads(text, object_pairs_hook=_reject_duplicate_pairs)
    except (UnicodeDecodeError, json.JSONDecodeError, ValidationError) as error:
        fail(f"{label} malformed: {path}: {error}")
    if not isinstance(value, dict):
        fail(f"{label} root must be an object")
    return value, raw


def profile_sha256(profile: dict[str, Any]) -> str:
    buffer = bytearray()
    write_string(buffer, "boss_reward_profile_v1")
    write_string(buffer, profile["gameId"])
    write_string(buffer, profile["catalogSetId"])
    write_string(buffer, profile["id"])
    write_string(buffer, profile["schemaVersion"])
    write_string(buffer, profile["contentVersion"])
    write_i32(buffer, profile["warzoneCredits"])
    write_bool(buffer, profile["isExplicitNoReward"])
    write_string(buffer, profile["sourceRevision"])
    entries = profile["entries"]
    write_u32(buffer, len(entries))
    for entry in entries:
        write_string(buffer, entry["equipmentDefinitionId"])
        write_i32(buffer, entry["dropChanceMicros"])
        write_i32(buffer, entry["quantity"])
        write_string(buffer, entry["acquisitionAnnouncementPolicyId"])
    return sha256_hex(bytes(buffer))


def equipment_sha256(definition: dict[str, Any]) -> str:
    buffer = bytearray()
    write_string(buffer, "boss_equipment_definition_v1")
    write_string(buffer, definition["equipmentDefinitionId"])
    write_string(buffer, definition["schemaVersion"])
    write_string(buffer, definition["contentVersion"])
    write_string(buffer, definition["slotId"])
    write_i32(buffer, definition["attackBonus"])
    write_i32(buffer, definition["defenseBonus"])
    write_i32(buffer, definition["healthBonus"])
    write_string(buffer, definition["stackPolicyId"])
    write_string(buffer, definition["acquisitionSnapshotPolicyId"])
    write_string(buffer, definition["presentationContentKey"])
    write_string(buffer, definition["sourceRevision"])
    return sha256_hex(bytes(buffer))


def resolve_boss(catalog: dict[str, Any], boss_definition_id: str) -> ResolvedBossReward:
    if not isinstance(boss_definition_id, str) or not TECHNICAL_ID.fullmatch(boss_definition_id):
        fail("invalid boss identity")
    matches = [
        binding
        for binding in catalog["bindings"]
        if binding["bossDefinitionId"] == boss_definition_id
    ]
    if not matches:
        fail(f"unknown boss: {boss_definition_id}")
    if len(matches) != 1:
        fail(f"duplicate binding: {boss_definition_id}")
    binding = matches[0]
    profiles = [
        profile
        for profile in catalog["profiles"]
        if profile["id"] == binding["rewardProfileId"]
        and profile["contentVersion"] == binding["rewardProfileContentVersion"]
    ]
    if not profiles:
        fail("missing profile reference")
    if len(profiles) != 1:
        fail("duplicate profile")
    profile = profiles[0]
    equipment_ids: list[str] = []
    quantities: list[int] = []
    chances: list[int] = []
    presentation = ""
    attack = defense = health = 0
    for entry in profile["entries"]:
        equipment_id = entry["equipmentDefinitionId"]
        definitions = [
            item
            for item in catalog["equipmentDefinitions"]
            if item["equipmentDefinitionId"] == equipment_id
        ]
        if not definitions:
            fail(f"missing equipment reference: {equipment_id}")
        if len(definitions) != 1:
            fail(f"duplicate equipment: {equipment_id}")
        definition = definitions[0]
        equipment_ids.append(equipment_id)
        quantities.append(entry["quantity"])
        chances.append(entry["dropChanceMicros"])
        presentation = definition["presentationContentKey"]
        attack = definition["attackBonus"]
        defense = definition["defenseBonus"]
        health = definition["healthBonus"]
    return ResolvedBossReward(
        boss_definition_id=binding["bossDefinitionId"],
        reward_profile_id=profile["id"],
        equipment_definition_ids=tuple(equipment_ids),
        warzone_credits=profile["warzoneCredits"],
        quantities=tuple(quantities),
        drop_chance_micros=tuple(chances),
        attack_bonus=attack,
        defense_bonus=defense,
        health_bonus=health,
        presentation_content_key=presentation,
    )


def validate_catalog(catalog: dict[str, Any]) -> ResolvedBossReward:
    require_exact_keys(catalog, ROOT_KEYS, "$")
    if catalog["schemaVersion"] != SCHEMA_VERSION:
        fail("unsupported schema version")
    if catalog["catalogId"] != CATALOG_ID:
        fail("unexpected catalogId")
    if catalog["gameId"] != GAME_ID:
        fail("unexpected gameId")
    if catalog["catalogSetId"] != CATALOG_SET_ID:
        fail("unexpected catalogSetId")
    if catalog["revision"] != REVISION:
        fail("unexpected revision")
    if catalog["authority"] != AUTHORITY:
        fail("unexpected authority")
    if catalog["mutationActivation"] != MUTATION_ACTIVATION:
        fail("mutationActivation must remain blocked")

    approval = require_exact_keys(catalog["approval"], APPROVAL_KEYS, "$.approval")
    if approval["mode"] != "autonomous_bounded_recommendation":
        fail("unexpected approval mode")
    if approval["issue"] != "#168":
        fail("unexpected approval issue")
    if approval["representativeBossId"] != REPRESENTATIVE_BOSS_ID:
        fail("unexpected representative boss")
    if approval["sourceQualificationIssue"] != "#259":
        fail("unexpected source qualification issue")

    policies = catalog["announcementPolicyIds"]
    if policies != ANNOUNCEMENT_POLICY_IDS:
        fail("announcementPolicyIds drifted")

    bindings = catalog["bindings"]
    if not isinstance(bindings, list) or not bindings:
        fail("bindings must contain the representative row")
    binding_ids: set[str] = set()
    for index, binding in enumerate(bindings):
        row = require_exact_keys(binding, BINDING_KEYS, f"$.bindings[{index}]")
        boss_id = require_technical_id(row["bossDefinitionId"], f"$.bindings[{index}].bossDefinitionId")
        require_technical_id(
            row["bossDefinitionContentVersion"],
            f"$.bindings[{index}].bossDefinitionContentVersion",
        )
        require_technical_id(row["rewardProfileId"], f"$.bindings[{index}].rewardProfileId")
        require_technical_id(
            row["rewardProfileContentVersion"],
            f"$.bindings[{index}].rewardProfileContentVersion",
        )
        if boss_id in binding_ids:
            fail(f"duplicate binding: {boss_id}")
        binding_ids.add(boss_id)
    if len(bindings) != 1:
        fail("this slice admits one representative binding")

    profiles = catalog["profiles"]
    if not isinstance(profiles, list) or not profiles:
        fail("profiles must contain the representative row")
    profile_keys: set[tuple[str, str]] = set()
    profile_by_key: dict[tuple[str, str], dict[str, Any]] = {}
    for index, profile in enumerate(profiles):
        row = require_exact_keys(profile, PROFILE_KEYS, f"$.profiles[{index}]")
        if row["gameId"] != GAME_ID or row["catalogSetId"] != CATALOG_SET_ID:
            fail("profile identity does not match the catalog")
        profile_id = require_technical_id(row["id"], f"$.profiles[{index}].id")
        if row["schemaVersion"] != SCHEMA_VERSION:
            fail("unsupported profile schema version")
        content_version = require_technical_id(
            row["contentVersion"],
            f"$.profiles[{index}].contentVersion",
        )
        credits = require_int(
            row["warzoneCredits"],
            f"$.profiles[{index}].warzoneCredits",
            0,
            BOUNDED_CREDITS,
        )
        explicit_no_reward = require_bool(
            row["isExplicitNoReward"],
            f"$.profiles[{index}].isExplicitNoReward",
        )
        require_technical_id(row["sourceRevision"], f"$.profiles[{index}].sourceRevision")
        declared = require_string(row["rawSha256"], f"$.profiles[{index}].rawSha256")
        if not SHA256_HEX.fullmatch(declared):
            fail("profile hash is malformed")
        entries = row["entries"]
        if not isinstance(entries, list):
            fail("profile entries must be an array")
        if explicit_no_reward and (credits != 0 or entries):
            fail("explicit no-reward profile cannot grant credits or items")
        if not explicit_no_reward and credits == 0 and not entries:
            fail("empty zero-credit profile must declare no reward")
        seen_entries: set[str] = set()
        for entry_index, entry in enumerate(entries):
            item = require_exact_keys(
                entry,
                ENTRY_KEYS,
                f"$.profiles[{index}].entries[{entry_index}]",
            )
            equipment_id = require_technical_id(
                item["equipmentDefinitionId"],
                f"$.profiles[{index}].entries[{entry_index}].equipmentDefinitionId",
            )
            if equipment_id in seen_entries:
                fail(f"duplicate profile entry: {equipment_id}")
            seen_entries.add(equipment_id)
            require_int(
                item["dropChanceMicros"],
                f"$.profiles[{index}].entries[{entry_index}].dropChanceMicros",
                0,
                MICROS_PER_UNIT,
            )
            require_int(
                item["quantity"],
                f"$.profiles[{index}].entries[{entry_index}].quantity",
                1,
                BOUNDED_QUANTITY,
            )
            policy = require_content_key(
                item["acquisitionAnnouncementPolicyId"],
                f"$.profiles[{index}].entries[{entry_index}].acquisitionAnnouncementPolicyId",
            )
            if policy not in ANNOUNCEMENT_POLICY_IDS:
                fail("unknown announcement policy")
        if declared != profile_sha256(row):
            fail("profile hash mismatch")
        key = (profile_id, content_version)
        if key in profile_keys:
            fail(f"duplicate profile: {profile_id}")
        profile_keys.add(key)
        profile_by_key[key] = row

    equipment = catalog["equipmentDefinitions"]
    if not isinstance(equipment, list) or not equipment:
        fail("equipmentDefinitions must contain the representative row")
    equipment_ids: set[str] = set()
    equipment_by_id: dict[str, dict[str, Any]] = {}
    for index, definition in enumerate(equipment):
        row = require_exact_keys(
            definition,
            EQUIPMENT_KEYS,
            f"$.equipmentDefinitions[{index}]",
        )
        equipment_id = require_technical_id(
            row["equipmentDefinitionId"],
            f"$.equipmentDefinitions[{index}].equipmentDefinitionId",
        )
        if row["schemaVersion"] != SCHEMA_VERSION:
            fail("unsupported equipment schema version")
        require_technical_id(
            row["contentVersion"],
            f"$.equipmentDefinitions[{index}].contentVersion",
        )
        require_technical_id(row["slotId"], f"$.equipmentDefinitions[{index}].slotId")
        require_int(row["attackBonus"], f"$.equipmentDefinitions[{index}].attackBonus", 0, 0)
        require_int(row["defenseBonus"], f"$.equipmentDefinitions[{index}].defenseBonus", 0, 0)
        require_int(row["healthBonus"], f"$.equipmentDefinitions[{index}].healthBonus", 0, 0)
        if row["stackPolicyId"] != "stack_quantity":
            fail("unsupported stack policy")
        if row["acquisitionSnapshotPolicyId"] != "acquisition_snapshot_v1":
            fail("unsupported acquisition snapshot policy")
        presentation = require_content_key(
            row["presentationContentKey"],
            f"$.equipmentDefinitions[{index}].presentationContentKey",
        )
        if presentation == equipment_id:
            fail("presentation content key must stay separate from the equipment id")
        require_technical_id(
            row["sourceRevision"],
            f"$.equipmentDefinitions[{index}].sourceRevision",
        )
        declared = require_string(
            row["rawSha256"],
            f"$.equipmentDefinitions[{index}].rawSha256",
        )
        if not SHA256_HEX.fullmatch(declared):
            fail("equipment hash is malformed")
        if declared != equipment_sha256(row):
            fail("equipment hash mismatch")
        if equipment_id in equipment_ids:
            fail(f"duplicate equipment: {equipment_id}")
        equipment_ids.add(equipment_id)
        equipment_by_id[equipment_id] = row

    profile_ids = {profile_id for profile_id, _version in profile_keys}
    for index, binding in enumerate(bindings):
        profile_id = binding["rewardProfileId"]
        if profile_id not in profile_ids:
            fail("missing profile reference")
        key = (profile_id, binding["rewardProfileContentVersion"])
        if key not in profile_by_key:
            fail("profile version mismatch")
        profile = profile_by_key[key]
        for entry in profile["entries"]:
            equipment_id = entry["equipmentDefinitionId"]
            if equipment_id not in equipment_by_id:
                fail("missing equipment reference")

    resolved = resolve_boss(catalog, REPRESENTATIVE_BOSS_ID)
    if resolved.reward_profile_id != REPRESENTATIVE_PROFILE_ID:
        fail("representative profile drifted")
    if resolved.equipment_definition_ids != (REPRESENTATIVE_EQUIPMENT_ID,):
        fail("representative equipment drifted")
    if resolved.warzone_credits != BOUNDED_CREDITS:
        fail("bounded credits drifted")
    if resolved.quantities != (BOUNDED_QUANTITY,):
        fail("bounded quantity drifted")
    if resolved.drop_chance_micros != (BOUNDED_CHANCE_MICROS,):
        fail("bounded drop chance drifted")
    return resolved


def validate_catalog_file(
    path: Path,
    repo_root: Path,
    require_pinned_source: bool = True,
) -> ValidationResult:
    resolved_path = path if path.is_absolute() else repo_root / path
    catalog, raw = load_json_strict(resolved_path, "boss reward source catalog")
    digest = sha256_hex(raw)
    if require_pinned_source:
        if EXPECTED_SOURCE_BYTE_LENGTH <= 0 or not EXPECTED_SOURCE_SHA256:
            fail("pinned source hash is not installed")
        if len(raw) != EXPECTED_SOURCE_BYTE_LENGTH or digest != EXPECTED_SOURCE_SHA256:
            fail("source hash mismatch")
    resolved = validate_catalog(catalog)
    return ValidationResult(
        mutation_activation=MUTATION_ACTIVATION,
        allows_mutation=False,
        activation_targets=[],
        resolved=resolved,
        source_sha256=digest,
        source_byte_length=len(raw),
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--catalog",
        default=str(DEFAULT_CATALOG_PATH),
        help="Repository-relative catalog path",
    )
    parser.add_argument(
        "--print-source-pin",
        action="store_true",
        help="Print byte length and SHA-256 without requiring the installed pin",
    )
    args = parser.parse_args(argv)
    repo_root = Path(__file__).resolve().parents[2]
    if args.print_source_pin:
        result = validate_catalog_file(
            Path(args.catalog),
            repo_root,
            require_pinned_source=False,
        )
        print(f"sourceByteLength={result.source_byte_length}")
        print(f"sourceSha256={result.source_sha256}")
        print(f"mutationActivation={result.mutation_activation}")
        return 0
    result = validate_catalog_file(Path(args.catalog), repo_root)
    print(
        "boss reward source: PASS "
        f"boss={result.resolved.boss_definition_id} "
        f"mutation={result.mutation_activation}"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ValidationError as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
