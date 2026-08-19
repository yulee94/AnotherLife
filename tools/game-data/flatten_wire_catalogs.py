#!/usr/bin/env python3
"""Regenerate the 3 WIRE families as six-family option-C envelopes.

t_d4892ee5: flatten only families that already have a Unity production reader.
Sources stay byte-identical (loaders keep working until t_a9097b56). SKIP
families are never opened for write.

    python tools/game-data/flatten_wire_catalogs.py
    python tools/game-data/flatten_wire_catalogs.py --check

Envelope (matches t_3edf1eec six-family MVP):

    {gameId, catalogId, family, schemaVersion, contentVersion,
     sourceRevision, records, aliases}

Do not merge into realms.v1.json or skills.v1.json.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any

REPO = Path(__file__).resolve().parents[2]
GAMEDATA = REPO / "unity" / "Assets" / "AL" / "StreamingAssets" / "GameData"

GAME_ID = "another-life"
CONTENT_VERSION = "1.0.0"
SOURCE_REVISION = "t_d4892ee5"
SCHEMA_VERSION = 1

FLAT_ENVELOPE_FIELDS = (
    "gameId",
    "catalogId",
    "family",
    "schemaVersion",
    "contentVersion",
    "sourceRevision",
    "records",
    "aliases",
)

WIRE_FAMILIES = (
    "realm_specialized",
    "character_customization",
    "skill_weather",
)

SOURCE_BY_FAMILY = {
    "realm_specialized": "al_realm_catalog.json",
    "character_customization": "al_character_customization_catalog.json",
    "skill_weather": "al_skill_weather_catalog.json",
}

OUTPUT_BY_FAMILY = {
    "realm_specialized": "realm_specialized.v1.json",
    "character_customization": "character_customization.v1.json",
    "skill_weather": "skill_weather.v1.json",
}

# Inventory SKIP list — must remain byte-identical.
SKIP_FILES = (
    "al_character_customization_content_catalog.json",
    "al_notification_content_catalog.json",
    "al_notification_production_catalog.json",
    "al_quest_preview_content_catalog.json",
    "al_realm_gem_wishgate_content_catalog.json",
    "al_relationship_authority_content_catalog.json",
    "al_warmaster_content_catalog.json",
    "al_world_atlas_narrative_catalog.json",
    "al_world_event_content_catalog.json",
)

# Source WIRE files are inputs, not outputs.
WIRE_SOURCE_FILES = tuple(SOURCE_BY_FAMILY.values())

_CAMEL_1 = re.compile(r"(.)([A-Z][a-z]+)")
_CAMEL_2 = re.compile(r"([a-z0-9])([A-Z])")


def camel_to_snake(name: str) -> str:
    return _CAMEL_2.sub(r"\1_\2", _CAMEL_1.sub(r"\1_\2", name)).lower()


def snake_keys(obj: Any) -> Any:
    if isinstance(obj, dict):
        return {camel_to_snake(str(key)): snake_keys(value) for key, value in obj.items()}
    if isinstance(obj, list):
        return [snake_keys(item) for item in obj]
    return obj


def write_json(path: Path, obj: dict[str, Any]) -> None:
    path.write_bytes((json.dumps(obj, indent=2) + "\n").encode("utf-8"))


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def alias_row(legacy_id: str, canonical_id: str) -> dict[str, Any]:
    return {
        "legacyId": legacy_id,
        "canonicalId": canonical_id,
        "introducedVersion": 1,
        "retirementVersion": None,
        "migrationIssue": "#183",
    }


class AliasSet:
    def __init__(self) -> None:
        self._claimed: dict[str, str | None] = {}

    def add(self, legacy_id: str | None, canonical_id: str) -> None:
        if not legacy_id or legacy_id == canonical_id:
            return
        existing = self._claimed.get(legacy_id)
        if existing is None and legacy_id not in self._claimed:
            self._claimed[legacy_id] = canonical_id
            return
        if existing != canonical_id:
            self._claimed[legacy_id] = None

    def rows(self) -> list[dict[str, Any]]:
        rows = [
            alias_row(legacy, canonical)
            for legacy, canonical in self._claimed.items()
            if canonical
        ]
        rows.sort(key=lambda row: (row["legacyId"], row["canonicalId"]))
        return rows


def build_flat_family(family: str, records: list[dict[str, Any]], aliases: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "gameId": GAME_ID,
        "catalogId": f"{family}_v1",
        "family": family,
        "schemaVersion": SCHEMA_VERSION,
        "contentVersion": CONTENT_VERSION,
        "sourceRevision": SOURCE_REVISION,
        "records": records,
        "aliases": aliases,
    }


def _record(kind: str, record_id: str, payload: dict[str, Any], *, legacy_id: str | None = None) -> dict[str, Any]:
    body = snake_keys(payload)
    body.pop("id", None)
    body.pop("kind", None)
    body.pop("legacy_id", None)
    out = {"id": record_id, "kind": kind}
    if legacy_id is not None and legacy_id != record_id:
        out["legacy_id"] = legacy_id
    out.update(body)
    return out


def build_realm_specialized(src: dict[str, Any]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    records: list[dict[str, Any]] = []
    aliases = AliasSet()

    records.append(_record("selection_policy", "selection_policy", src["selectionPolicy"]))
    records.append(_record("narrative_continuity", "narrative_continuity", src["narrativeContinuity"]))

    realm_order = list(src["realmOrder"])
    records.append(_record("realm_order", "realm_order", {"realm_ids": realm_order}))

    by_id = {realm["id"]: realm for realm in src["realms"]}
    for index, realm_id in enumerate(realm_order):
        realm = dict(by_id[realm_id])
        realm["sort_order"] = index
        records.append(_record("realm", realm_id, realm))
        aliases.add(realm.get("legacyRuntimeId"), realm_id)
        aliases.add(realm.get("displayName"), realm_id)
        aliases.add(f"realm.{realm_id}", realm_id)

    for draft in src.get("localizationDrafts") or []:
        key = draft["key"]
        records.append(_record("localization_draft", key, draft))
        aliases.add(f"localization.{key}", key)

    records.append(_record("engineering_handoff", "engineering_handoff", src["engineeringHandoff"]))
    return records, aliases.rows()


def _kind_id(kind: str, source_id: str) -> str:
    return f"{kind}.{source_id}"


def build_character_customization(src: dict[str, Any]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    records: list[dict[str, Any]] = []
    aliases = AliasSet()

    def emit(kind: str, source_id: str, payload: dict[str, Any], *extra_legacy: str) -> None:
        canonical = _kind_id(kind, source_id)
        records.append(_record(kind, canonical, payload, legacy_id=source_id))
        aliases.add(source_id, canonical)
        aliases.add(f"{kind}.{source_id}", canonical)
        for value in extra_legacy:
            aliases.add(value, canonical)

    for slot in src["characterSlots"]:
        emit("character_slot", slot, {"slot": slot}, slot)

    option_groups = (
        ("body_preset", "bodyPresets"),
        ("hair_style", "hairStyles"),
        ("armor_style", "armorStyles"),
        ("primary_color", "primaryColors"),
        ("hair_color", "hairColors"),
        ("skin_color", "skinColors"),
        ("eye_color", "eyeColors"),
        ("accent_color", "accentColors"),
        ("face_mark", "faceMarks"),
        ("weapon_style", "weaponStyles"),
        ("offhand_style", "offhandStyles"),
        ("forge_preset", "forgePresets"),
    )
    for kind, key in option_groups:
        for item in src[key]:
            emit(kind, item["id"], item, item.get("displayName", ""))

    for item in src["realms"]:
        source_id = item["id"]
        emit("realm_customization", source_id, item, item.get("displayName", ""))

    records.append(_record("quality_targets", "quality_targets", src["qualityTargets"]))
    return records, aliases.rows()


def build_skill_weather(src: dict[str, Any]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    records: list[dict[str, Any]] = []
    aliases = AliasSet()

    for item in src["skillLoadouts"]:
        skill_id = item["id"]
        records.append(_record("skill_loadout", skill_id, item))
        aliases.add(item.get("displayName"), skill_id)
        aliases.add(f"skill_loadout.{skill_id}", skill_id)
        aliases.add(str(item["slot"]), skill_id)

    for item in src["skillEffects"]:
        effect_id = item["key"]
        records.append(_record("skill_effect", effect_id, item, legacy_id=effect_id))
        aliases.add(f"skill_effect.{effect_id}", effect_id)

    for item in src["weatherProfiles"]:
        weather_id = item["key"]
        records.append(_record("weather_profile", weather_id, item, legacy_id=weather_id))
        aliases.add(item.get("displayName"), weather_id)
        aliases.add(f"weather_profile.{weather_id}", weather_id)

    return records, aliases.rows()


BUILDERS = {
    "realm_specialized": build_realm_specialized,
    "character_customization": build_character_customization,
    "skill_weather": build_skill_weather,
}


def generate_family(family: str) -> dict[str, Any]:
    source_path = GAMEDATA / SOURCE_BY_FAMILY[family]
    records, aliases = BUILDERS[family](load_json(source_path))
    return build_flat_family(family, records, aliases)


def generate_all() -> dict[str, dict[str, Any]]:
    return {family: generate_family(family) for family in WIRE_FAMILIES}


def output_path(family: str) -> Path:
    return GAMEDATA / OUTPUT_BY_FAMILY[family]


def validate_envelope(family: str, envelope: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    missing = [field for field in FLAT_ENVELOPE_FIELDS if field not in envelope]
    if missing:
        errors.append(f"{family}: missing envelope fields {missing}")
        return errors
    if envelope["gameId"] != GAME_ID or envelope["family"] != family:
        errors.append(f"{family}: gameId/family mismatch")
    if envelope["catalogId"] != f"{family}_v1":
        errors.append(f"{family}: catalogId must be {family}_v1")
    if envelope["sourceRevision"] != SOURCE_REVISION:
        errors.append(f"{family}: sourceRevision mismatch")
    if envelope["schemaVersion"] != SCHEMA_VERSION:
        errors.append(f"{family}: schemaVersion mismatch")
    records = envelope["records"]
    aliases = envelope["aliases"]
    if not records:
        errors.append(f"{family}: records must not be empty")
    ids = [record.get("id") for record in records]
    if any(not record_id for record_id in ids):
        errors.append(f"{family}: every record needs an id")
    if len(ids) != len(set(ids)):
        errors.append(f"{family}: record ids are not unique")
    leftover_wrappers = {"realms", "skillLoadouts", "skillEffects", "weatherProfiles", "bodyPresets", "forgePresets"}
    if leftover_wrappers.intersection(envelope):
        errors.append(f"{family}: leftover nested catalog wrappers {sorted(leftover_wrappers.intersection(envelope))}")
    extra = [key for key in envelope if key not in FLAT_ENVELOPE_FIELDS]
    if extra:
        errors.append(f"{family}: unexpected envelope keys {extra}")
    seen_legacy: set[str] = set()
    for row in aliases:
        legacy = row.get("legacyId")
        canonical = row.get("canonicalId")
        if not legacy or not canonical:
            errors.append(f"{family}: alias missing legacyId/canonicalId")
            continue
        if legacy in seen_legacy:
            errors.append(f"{family}: duplicate alias legacyId {legacy!r}")
        seen_legacy.add(legacy)
        if canonical not in ids:
            errors.append(f"{family}: alias {legacy!r} points at missing id {canonical!r}")
    return errors


def validate_identity(family: str, envelope: dict[str, Any], source: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    by_id = {record["id"]: record for record in envelope["records"]}
    alias_map = {row["legacyId"]: row["canonicalId"] for row in envelope["aliases"]}

    if family == "realm_specialized":
        for realm in source["realms"]:
            if realm["id"] not in by_id:
                errors.append(f"realm id {realm['id']!r} missing from records")
            if alias_map.get(realm["legacyRuntimeId"]) != realm["id"]:
                errors.append(f"realm alias {realm['legacyRuntimeId']!r} unstable")
        if [record["id"] for record in envelope["records"] if record["kind"] == "realm"] != list(source["realmOrder"]):
            errors.append("realm record order does not match realmOrder")
        policy = by_id.get("selection_policy") or {}
        if policy.get("selection_mode") != source["selectionPolicy"]["selectionMode"]:
            errors.append("selection_policy.selection_mode not preserved")

    if family == "skill_weather":
        for item in source["skillLoadouts"]:
            record = by_id.get(item["id"])
            if record is None:
                errors.append(f"skill id {item['id']!r} missing")
                continue
            if record.get("slot") != item["slot"] or record.get("power") != item["power"]:
                errors.append(f"skill {item['id']!r} slot/power changed")
            if alias_map.get(item["displayName"]) != item["id"]:
                errors.append(f"skill alias {item['displayName']!r} unstable")
        for item in source["skillEffects"]:
            if item["key"] not in by_id:
                errors.append(f"skill effect {item['key']!r} missing")
        for item in source["weatherProfiles"]:
            if item["key"] not in by_id:
                errors.append(f"weather profile {item['key']!r} missing")

    if family == "character_customization":
        for item in source["bodyPresets"]:
            canonical = f"body_preset.{item['id']}"
            if canonical not in by_id:
                errors.append(f"body preset {item['id']!r} missing")
            elif by_id[canonical].get("legacy_id") != item["id"]:
                errors.append(f"body preset legacy_id drifted for {item['id']!r}")
        for item in source["forgePresets"]:
            canonical = f"forge_preset.{item['id']}"
            if canonical not in by_id:
                errors.append(f"forge preset {item['id']!r} missing")
        for slot in source["characterSlots"]:
            if f"character_slot.{slot}" not in by_id:
                errors.append(f"character slot {slot!r} missing")
        if "quality_targets" not in by_id:
            errors.append("quality_targets record missing")
        # Colliding short ids must not silently pick one winner.
        if "grove_green" in alias_map:
            errors.append("ambiguous grove_green must not be a bare alias")
        if "duelist" in alias_map:
            errors.append("ambiguous duelist must not be a bare alias")

    return errors


def snapshot_protected() -> dict[str, str]:
    hashes: dict[str, str] = {}
    for name in (*SKIP_FILES, *WIRE_SOURCE_FILES):
        path = GAMEDATA / name
        if not path.is_file():
            raise FileNotFoundError(path)
        hashes[name] = sha256_file(path)
    return hashes


def assert_protected_unchanged(before: dict[str, str]) -> list[str]:
    errors: list[str] = []
    after = snapshot_protected()
    for name, digest in before.items():
        if after[name] != digest:
            errors.append(f"protected file changed: {name}")
    return errors


def write_all(generated: dict[str, dict[str, Any]]) -> None:
    for family, envelope in generated.items():
        write_json(output_path(family), envelope)


def validate_generated(generated: dict[str, dict[str, Any]]) -> list[str]:
    errors: list[str] = []
    for family, envelope in generated.items():
        source = load_json(GAMEDATA / SOURCE_BY_FAMILY[family])
        errors.extend(validate_envelope(family, envelope))
        errors.extend(validate_identity(family, envelope, source))
    return errors


def check_on_disk(generated: dict[str, dict[str, Any]]) -> list[str]:
    errors: list[str] = []
    for family, envelope in generated.items():
        path = output_path(family)
        if not path.is_file():
            errors.append(f"missing output {path.name}")
            continue
        disk = json.loads(path.read_text(encoding="utf-8"))
        if disk != envelope:
            errors.append(f"{path.name} is stale; re-run flatten_wire_catalogs.py")
    return errors


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="validate existing outputs without writing",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    before = snapshot_protected()
    generated = generate_all()
    errors = validate_generated(generated)
    if args.check:
        errors.extend(check_on_disk(generated))
    elif not errors:
        write_all(generated)
    errors.extend(assert_protected_unchanged(before))
    if errors:
        print("FAILED:")
        for error in errors:
            print(f"  - {error}")
        return 1
    for family, envelope in generated.items():
        print(
            f"[ OK ] {OUTPUT_BY_FAMILY[family]} "
            f"({len(envelope['records'])} records, {len(envelope['aliases'])} aliases)"
        )
    print("SKIP + WIRE source files unchanged")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
