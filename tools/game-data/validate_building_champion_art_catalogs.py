"""Validate landed building/champion art catalogs against schemas + C# ID rules."""

from __future__ import annotations

import hashlib
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
UNITY = ROOT / "unity"
SCHEMA_DIR = UNITY / "SharedContracts" / "Schemas"
CATALOG_DIR = UNITY / "Assets" / "AL" / "StreamingAssets" / "GameData"

BUILDING_SCHEMA = SCHEMA_DIR / "al-building.schema.json"
CHAMPION_SCHEMA = SCHEMA_DIR / "al-champion.schema.json"
BUILDING_CATALOG = CATALOG_DIR / "al_building_catalog.json"
CHAMPION_CATALOG = CATALOG_DIR / "al_champion_catalog.json"

LEGACY_PAIRS = {
    "town_hall": "TownHall",
    "farm": "Farm",
    "lumber_mill": "LumberMill",
    "quarry": "Quarry",
    "gold_mine": "GoldMine",
    "barracks": "Barracks",
    "academy": "Academy",
    "market": "Market",
    "storehouse": "Storehouse",
    "forge": "Forge",
    "stable": "Stable",
    "workshop": "Workshop",
    "embassy": "Embassy",
    "wall": "Wall",
    "watchtower": "Watchtower",
}

BYTE_STABLE = {
    "unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json":
        "33321936662b98f9c18edf4122ad163053d1aff3017b06556cad694420e9e8d8",
    "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataRealmReferences.cs":
        "4bb8457c9831756a8cf6c2ddf3f14a5fd5c51866370c870cb074a53313bbdf4f",
    "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataBuildingProgressionRegistry.cs":
        "319cb9f97cff850c3e0f79c30ae877c2876ecab6cf70d9fa681a672be4b430c4",
    "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/GameDataSixFamilySchemas.cs":
        "3c759d9ea2f1b2d6aca53d1e5f213bf0edb057eb0751bf3c9bfe9ae94b15d9bb",
}


def is_canonical_stable_id(value: str) -> bool:
    if not value or len(value) > 128 or value[0] < "a" or value[0] > "z":
        return False
    previous_underscore = False
    for index, character in enumerate(value[1:], start=1):
        is_lower = "a" <= character <= "z"
        is_digit = "0" <= character <= "9"
        if is_lower or is_digit:
            previous_underscore = False
            continue
        if character != "_" or previous_underscore or index == len(value) - 1:
            return False
        previous_underscore = True
    return True


def is_canonical_content_reference(value: str) -> bool:
    if not value or value.strip() != value:
        return False
    segments = value.split(".")
    return len(segments) >= 2 and all(is_canonical_stable_id(part) for part in segments)


def sha256_file(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_json(path: pathlib.Path):
    return json.loads(path.read_text(encoding="utf-8"))


def fail(errors: list[str], message: str) -> None:
    errors.append(message)
    print("FAIL:", message)


def main() -> int:
    from jsonschema import Draft202012Validator

    errors: list[str] = []
    building_schema = load_json(BUILDING_SCHEMA)
    champion_schema = load_json(CHAMPION_SCHEMA)
    building_catalog = load_json(BUILDING_CATALOG)
    champion_catalog = load_json(CHAMPION_CATALOG)

    Draft202012Validator.check_schema(building_schema)
    Draft202012Validator.check_schema(champion_schema)
    print("PASS: both schemas are valid draft 2020-12")

    building_validator = Draft202012Validator(building_schema)
    champion_validator = Draft202012Validator(champion_schema)
    building_schema_errors = sorted(building_validator.iter_errors(building_catalog), key=lambda e: list(e.path))
    champion_schema_errors = sorted(champion_validator.iter_errors(champion_catalog), key=lambda e: list(e.path))
    if building_schema_errors:
        for err in building_schema_errors:
            fail(errors, f"building catalog schema: {list(err.path)}: {err.message}")
    else:
        print("PASS: al_building_catalog.json validates against al-building.schema.json")
    if champion_schema_errors:
        for err in champion_schema_errors:
            fail(errors, f"champion catalog schema: {list(err.path)}: {err.message}")
    else:
        print("PASS: al_champion_catalog.json validates against al-champion.schema.json")

    extra_building = dict(building_catalog)
    extra_building["unexpected"] = True
    extra_errors = list(building_validator.iter_errors(extra_building))
    if any("unexpected" in err.message for err in extra_errors):
        print("PASS: additionalProperties:false rejects unexpected top-level key")
    else:
        fail(errors, "expected additionalProperties rejection for unexpected top-level key")

    stripped = {k: v for k, v in building_catalog.items() if k != "catalogId"}
    if any("catalogId" in err.message or "required" in err.message.lower() for err in building_validator.iter_errors(stripped)):
        print("PASS: missing catalogId is rejected")
    else:
        fail(errors, "expected required catalogId rejection")

    ids = [row["id"] for row in building_catalog["buildings"]]
    if ids != list(LEGACY_PAIRS):
        fail(errors, f"building id order/set mismatch: {ids}")
    else:
        print("PASS: 15 building IDs match GameDataBuildingProgressionRegistry order")

    for row in building_catalog["buildings"]:
        if not is_canonical_stable_id(row["id"]):
            fail(errors, f"building id fails IsCanonicalStableId: {row['id']}")
        if not is_canonical_content_reference(row["name_ref"]):
            fail(errors, f"name_ref fails IsCanonicalContentReference: {row['name_ref']}")
        expected_legacy = LEGACY_PAIRS[row["id"]]
        if row["legacy_building_id"] != expected_legacy:
            fail(errors, f"legacy pairing {row['id']} -> {row['legacy_building_id']} != {expected_legacy}")
        realm_ids = []
        for model in row["models"]:
            realm_id = model["realm_id"]
            model_id = model["model_id"]
            if realm_id in realm_ids:
                fail(errors, f"duplicate realm_id {realm_id} on {row['id']}")
            realm_ids.append(realm_id)
            if not is_canonical_stable_id(model_id):
                fail(errors, f"model_id fails IsCanonicalStableId: {model_id}")
            expected_token = f"building_{realm_id}_{row['id']}_"
            if not model_id.startswith(expected_token):
                fail(errors, f"model_id {model_id} does not start with {expected_token}")
            asset = model["asset_ref"]
            rel = asset["path"]
            if not rel.startswith("Assets/"):
                fail(errors, f"path does not start with Assets/: {rel}")
            disk = UNITY / rel
            if not disk.exists():
                fail(errors, f"missing prefab: {rel}")
                continue
            actual_sha = sha256_file(disk)
            if actual_sha != asset["sha256"]:
                fail(errors, f"sha256 mismatch {rel}: catalog={asset['sha256']} disk={actual_sha}")
            meta = disk.with_suffix(disk.suffix + ".meta")
            meta_text = meta.read_text(encoding="utf-8")
            match = re.search(r"^guid: ([0-9a-f]{32})$", meta_text, re.M)
            if not match or match.group(1) != asset["guid"]:
                fail(errors, f"guid mismatch {rel}: catalog={asset['guid']} meta={match.group(1) if match else None}")

    bound = sum(len(row["models"]) for row in building_catalog["buildings"])
    print(f"PASS: {bound} building model tuples exist on disk with matching guid+sha256")

    champion_ids = []
    for row in champion_catalog["champions"]:
        if not is_canonical_stable_id(row["id"]):
            fail(errors, f"champion id fails IsCanonicalStableId: {row['id']}")
        if not is_canonical_content_reference(row["name_ref"]):
            fail(errors, f"champion name_ref fails: {row['name_ref']}")
        if row["id"] in champion_ids:
            fail(errors, f"duplicate champion id {row['id']}")
        champion_ids.append(row["id"])
        if "portrait_asset_ref" in row or "model_asset_ref" in row:
            fail(errors, f"champion {row['id']} unexpectedly has art refs")
    print(f"PASS: {len(champion_ids)} champion IDs are canonical; art refs honestly unset")

    for rel, expected in BYTE_STABLE.items():
        actual = sha256_file(ROOT / rel)
        if actual != expected:
            fail(errors, f"byte-stable hash changed {rel}: {actual} != {expected}")
    print("PASS: 4 sampled byte-stable pinned sources unchanged")

    so_path = UNITY / "Assets/AL/ScriptableObjects/Resources/KingdomBuildingModelCatalog.asset"
    if "40d5f7687fed640fd8c0d4b1868ff0ef" not in so_path.read_text(encoding="utf-8"):
        fail(errors, "KingdomBuildingModelCatalog.asset lost Crownlands TownHall guid")
    else:
        print("PASS: KingdomBuildingModelCatalog.asset left in place")

    if errors:
        print(f"RESULT: {len(errors)} failure(s)")
        return 1
    print("RESULT: all checks passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
