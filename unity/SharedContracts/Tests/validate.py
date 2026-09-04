#!/usr/bin/env python3
"""Validate Another Life SharedContracts JSON Schemas and fixtures.

Usage:
  uv run --with jsonschema validate.py
  (or) python validate.py   # if `jsonschema` is already importable

Behavior:
  1. Loads every *.schema.json under ../Schemas and asserts each is a valid
     draft 2020-12 schema (compiles without error).
  2. Validates each real StreamingAssets/GameData catalog against its schema
     and reports PASS/FAIL (honest report; see EXPECTED_DEFECTS).
  3. Validates Tests/fixtures/valid/*.json (must PASS) and
     Tests/fixtures/invalid/*.json (must FAIL) against the schema named by the
     fixture filename prefix.

Exit code is 0 only when every schema compiles, every valid fixture passes,
and every invalid fixture fails. Real-catalog failures listed in
EXPECTED_DEFECTS do not fail the run (they are the known data defects the
downstream data-generation task must correct).
"""

import copy
import io
import json
import pathlib
import sys
import unittest
from decimal import Decimal

from jsonschema import Draft202012Validator, FormatChecker, SchemaError, ValidationError

import world_asset_inventory
import test_four_realm_production_taxonomy
import test_model_motion_skill_vfx_harness
import test_realm_character_taxonomy
import test_rig_motion_standard

ROOT = pathlib.Path(__file__).resolve().parent.parent  # unity/SharedContracts
SCHEMAS_DIR = ROOT / "Schemas"
FIXTURES_DIR = pathlib.Path(__file__).resolve().parent / "fixtures"
GAMEDATA_DIR = (
    ROOT.parent / "Assets" / "AL" / "StreamingAssets" / "GameData"
)

# schema-name -> registered real catalog file
REAL_CATALOGS = {
    "al-item-power-ladders": "al_item_power_ladders_catalog.json",
    "al-alliance-war": "al_alliance_war_policy.json",
    "al-pvp-harmful-effect-gate": "al_pvp_harmful_effect_gate_policy.json",
    "al-guild-membership": "al_guild_membership_policy.json",
    "al-guild-progression": "al_guild_progression_policy.json",
    "al-guild-raid-muster": "al_guild_raid_muster_policy.json",
    "al-guild-city-season": "al_guild_city_season_policy.json",
    "al-oathmark-marketplace": "al_oathmark_marketplace_policy.json",
    "al-map-disclosure": "al_map_disclosure_catalog.json",
    "al-realm": "al_realm_catalog.json",
    "al-realm-gem-wishgate-content": "al_realm_gem_wishgate_content_catalog.json",
    "al-notification-content": "al_notification_content_catalog.json",
    "al-notification-production": "al_notification_production_catalog.json",
    "al-character-customization-content": "al_character_customization_content_catalog.json",
    "al-warmaster-content": "al_warmaster_content_catalog.json",
    "al-quest-preview-content": "al_quest_preview_content_catalog.json",
    "al-relationship-authority-content": "al_relationship_authority_content_catalog.json",
    "al-world-atlas-narrative": "al_world_atlas_narrative_catalog.json",
    "al-world-event-content": "al_world_event_content_catalog.json",
    "al-main-quest-line-runtime": "al_main_quest_line_runtime.v1.json",
    "al-building": "al_building_catalog.json",
    "al-champion": "al_champion_catalog.json",
    "al-golden-scene": "al_golden_scene_catalog.json",
    "al-first-session-terrain": "al_first_session_terrain_catalog.json",
    "al-world-asset-inventory": "al_world_asset_inventory.json",
    "al-four-realm-production-taxonomy": "al_four_realm_production_taxonomy.json",
    "al-required-motion-manifest": "al_required_motion_manifest.json",
    "al-rig-motion-standard": "al_rig_motion_standard.json",
    "al-model-motion-skill-vfx-harness": "al_model_motion_skill_vfx_harness.v1.json",
    "al-boss-skill-presentation": "al_boss_skill_presentation_catalog.v1.json",
    "al-boss-reward-source": "al_boss_reward_source_catalog.json",
}

# Known source-data defects that legitimately fail their schema today. These are
# the canonical decisions being enforced; the downstream data-generation task
# (t_46749a9c) corrects the data, not the schema.
EXPECTED_DEFECTS = {
    "al-world-event-content": [
        "eventDefinitions[].notificationDefinitionId uses notification.world_event.* "
        "dotted placeholders instead of canonical al_notify_* IDs (inventory conflict #5)."
    ],
}


def load_json(path):
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh, parse_float=Decimal)


def expect_item_power_ladder_rejected(validator, canonical, name, mutate):
    sabotaged = copy.deepcopy(canonical)
    mutate(sabotaged)
    if not list(validator.iter_errors(sabotaged)):
        raise AssertionError(f"sabotage unexpectedly passed: {name}")
    print(f"PASS reject {name}")


def swap_adjacent(document, key, index):
    items = document[key]
    items[index], items[index + 1] = items[index + 1], items[index]


def validate_item_power_ladders():
    schema = load_json(SCHEMAS_DIR / "al-item-power-ladders.schema.json")
    catalog = load_json(GAMEDATA_DIR / "al_item_power_ladders_catalog.json")
    Draft202012Validator.check_schema(schema)
    validator = Draft202012Validator(schema)

    errors = list(validator.iter_errors(catalog))
    if errors:
        first = errors[0]
        path = ".".join(str(part) for part in first.absolute_path) or "<root>"
        raise AssertionError(f"canonical catalog failed at {path}: {first.message}")
    print("PASS canonical item power ladders")

    cases = [
        ("inverted PvP ranks", lambda value: swap_adjacent(value, "pvpGearLadder", 0)),
        ("outer dungeon outranks dragon", lambda value: swap_adjacent(value, "pvpGearLadder", 1)),
        ("inner dungeon outranks outer dungeon", lambda value: swap_adjacent(value, "pvpGearLadder", 2)),
        ("PvP gear is not awakened", lambda value: value["pvpGearLadder"][1].update(awakening="standard")),
        ("inverted PvE ranks", lambda value: swap_adjacent(value, "pveGearLadder", 0)),
        ("Warmaster loses PvP", lambda value: value["pvpGearLadder"][0].update(rank=2)),
        ("Warmaster loses PvE", lambda value: value["pveGearLadder"][0].update(rank=2)),
        ("inverted embed gem ranks", lambda value: swap_adjacent(value, "embedGemLadder", 0)),
        ("inverted accessory ranks", lambda value: swap_adjacent(value, "accessoryLadder", 0)),
        ("quest accessory trades", lambda value: value["accessoryLadder"][3].update(tradable=True)),
        ("special heart slots as gear", lambda value: value["specialHearts"][0].update(regularGearSlotEligible=True)),
        ("Wish Dragon heart accepts seven gems", lambda value: value["specialHearts"][0]["acquisition"].update(realmUniqueGemCount=7)),
        ("Wish Dragon heart waives arena victory", lambda value: value["specialHearts"][0]["acquisition"].update(requirementsMode="any")),
        ("world-boss heart drop is not extremely low", lambda value: value["specialHearts"][1]["acquisition"].update(dropFrequency="low")),
        ("world-boss contributor loot is unequal", lambda value: value["specialHearts"][1]["acquisition"].update(contributorLootPercentagePolicy="weighted")),
        ("dragon gem band lowered", lambda value: value["embedGemLadder"][0].update(minimumPercent=90)),
        ("crafted gem becomes a range", lambda value: value["embedGemLadder"][2].update(maximumPercent=76)),
    ]
    for name, mutate in cases:
        expect_item_power_ladder_rejected(validator, catalog, name, mutate)

    print(f"ALL ITEM POWER LADDER CHECKS PASSED ({len(cases)} sabotage cases)")


def first_error_message(errors):
    err = errors[0]
    path = ".".join(str(p) for p in err.absolute_path)
    return f"{path or '<root>'}: {err.message}"


def main():
    schemas = {}
    for schema_path in sorted(SCHEMAS_DIR.glob("*.schema.json")):
        schema = load_json(schema_path)
        name = schema_path.stem  # e.g. "al-realm.schema" -> we strip later
        # Use the $id leaf as the canonical name key.
        sid = schema.get("$id", "")
        key = schema_path.name[: -len(".schema.json")]
        schemas[key] = (schema_path, schema)

    print(f"Loaded {len(schemas)} schemas from {SCHEMAS_DIR}")
    print()

    failures = []
    reports = {
        "schemas": [],
        "valid": [],
        "invalid": [],
        "real": [],
        "inventory": [],
        "realmTaxonomy": [],
        "fourRealmTaxonomy": [],
        "rigMotion": [],
        "modelMotionSkillVfx": [],
    }

    # 1. Compile every schema (validity of the schema itself).
    compiled = {}
    for key, (path, schema) in sorted(schemas.items()):
        try:
            Draft202012Validator.check_schema(schema)
            compiled[key] = Draft202012Validator(schema, format_checker=FormatChecker())
            reports["schemas"].append((key, True, ""))
        except SchemaError as exc:
            compiled[key] = None
            reports["schemas"].append((key, False, str(exc)))
            failures.append(f"schema[{key}] does not compile: {exc}")

    # 2. Real catalogs.
    for key, filename in sorted(REAL_CATALOGS.items()):
        validator = compiled.get(key)
        real_path = GAMEDATA_DIR / filename
        if validator is None or not real_path.exists():
            continue
        instance = load_json(real_path)
        errors = list(validator.iter_errors(instance))
        ok = len(errors) == 0
        expected = key in EXPECTED_DEFECTS
        reports["real"].append((key, ok, expected, first_error_message(errors) if errors else ""))

    # 3. Fixtures.
    valid_dir = FIXTURES_DIR / "valid"
    invalid_dir = FIXTURES_DIR / "invalid"

    for fixture_path in sorted(valid_dir.glob("*.json")):
        key = fixture_path.stem.split(".valid")[0]
        validator = compiled.get(key)
        if validator is None:
            failures.append(f"valid fixture {fixture_path.name}: no schema '{key}'")
            continue
        instance = load_json(fixture_path)
        errors = list(validator.iter_errors(instance))
        ok = len(errors) == 0
        reports["valid"].append((fixture_path.name, ok, first_error_message(errors) if errors else ""))
        if not ok:
            failures.append(f"VALID fixture {fixture_path.name} unexpectedly failed: {first_error_message(errors)}")

    for fixture_path in sorted(invalid_dir.glob("*.json")):
        # name pattern: <schema>.invalid.<reason>.json
        key = fixture_path.stem.split(".invalid")[0]
        validator = compiled.get(key)
        if validator is None:
            failures.append(f"invalid fixture {fixture_path.name}: no schema '{key}'")
            continue
        instance = load_json(fixture_path)
        errors = list(validator.iter_errors(instance))
        ok = len(errors) > 0
        reports["invalid"].append((fixture_path.name, ok, first_error_message(errors) if errors else ""))
        if not ok:
            failures.append(f"INVALID fixture {fixture_path.name} unexpectedly PASSED (should be rejected)")

    # 4. Semantic sabotage cases that invert owner-locked item ladders or permissions.
    try:
        validate_item_power_ladders()
    except Exception as exc:
        failures.append(f"item power ladder sabotage validation failed: {exc}")

    # 5. Cross-record inventory semantics and canonical byte stability.
    repo_root = ROOT.parents[1]
    try:
        _, evidence = world_asset_inventory.validate_committed_outputs(repo_root)
        reports["inventory"].append(
            (
                True,
                (
                    f"{evidence['familyCoverage']['covered']} families, "
                    f"{evidence['bindingCoverage']['verifiedPrefabTuples']} prefab tuples, "
                    f"{evidence['budgetRollup']['classCount']} budget classes"
                ),
            )
        )
    except world_asset_inventory.InventoryValidationError as exc:
        reports["inventory"].append((False, str(exc)))
        failures.append(f"world asset inventory cross-validation failed: {exc}")

    # 6. Realm character/creature cross-record and fail-closed semantics.
    suite = unittest.defaultTestLoader.loadTestsFromModule(test_realm_character_taxonomy)
    realm_test_output = io.StringIO()
    realm_result = unittest.TextTestRunner(
        stream=realm_test_output,
        verbosity=0,
    ).run(suite)
    realm_ok = realm_result.wasSuccessful()
    realm_summary = (
        f"{realm_result.testsRun} tests, "
        f"{len(realm_result.failures)} failures, {len(realm_result.errors)} errors"
    )
    reports["realmTaxonomy"].append((realm_ok, realm_summary))
    if not realm_ok:
        failures.append(
            "realm character taxonomy fail-closed tests failed: "
            + realm_test_output.getvalue().strip()
        )

    # 7. Integrated four-realm production taxonomy and acceptance audit.
    suite = unittest.defaultTestLoader.loadTestsFromModule(
        test_four_realm_production_taxonomy
    )
    integrated_test_output = io.StringIO()
    integrated_result = unittest.TextTestRunner(
        stream=integrated_test_output,
        verbosity=0,
    ).run(suite)
    integrated_ok = integrated_result.wasSuccessful()
    integrated_summary = (
        f"{integrated_result.testsRun} tests, "
        f"{len(integrated_result.failures)} failures, "
        f"{len(integrated_result.errors)} errors"
    )
    reports["fourRealmTaxonomy"].append((integrated_ok, integrated_summary))
    if not integrated_ok:
        failures.append(
            "integrated four-realm taxonomy fail-closed tests failed: "
            + integrated_test_output.getvalue().strip()
        )

    # 8. Rig, motion, anatomy-exception, and required-coverage acceptance audit.
    suite = unittest.defaultTestLoader.loadTestsFromModule(test_rig_motion_standard)
    rig_motion_test_output = io.StringIO()
    rig_motion_result = unittest.TextTestRunner(
        stream=rig_motion_test_output,
        verbosity=0,
    ).run(suite)
    rig_motion_ok = rig_motion_result.wasSuccessful()
    rig_motion_summary = (
        f"{rig_motion_result.testsRun} tests, "
        f"{len(rig_motion_result.failures)} failures, "
        f"{len(rig_motion_result.errors)} errors"
    )
    reports["rigMotion"].append((rig_motion_ok, rig_motion_summary))
    if not rig_motion_ok:
        failures.append(
            "rig and required-motion fail-closed tests failed: "
            + rig_motion_test_output.getvalue().strip()
        )

    # 9. Model/motion/skill-VFX harness: explicit PASS/FAIL/BLOCKED, no scores.
    suite = unittest.defaultTestLoader.loadTestsFromModule(
        test_model_motion_skill_vfx_harness
    )
    harness_test_output = io.StringIO()
    harness_result = unittest.TextTestRunner(
        stream=harness_test_output,
        verbosity=0,
    ).run(suite)
    harness_ok = harness_result.wasSuccessful()
    harness_summary = (
        f"{harness_result.testsRun} tests, "
        f"{len(harness_result.failures)} failures, "
        f"{len(harness_result.errors)} errors"
    )
    reports["modelMotionSkillVfx"].append((harness_ok, harness_summary))
    if not harness_ok:
        failures.append(
            "model/motion/skill-VFX harness fail-closed tests failed: "
            + harness_test_output.getvalue().strip()
        )
    # ---- Report ----
    print("== Schema compilation ==")
    for key, ok, msg in reports["schemas"]:
        print(f"  [{'OK' if ok else 'FAIL'}] {key}" + (f"  -> {msg}" if msg else ""))

    print("\n== Real catalog validation ==")
    for key, ok, expected_defect, msg in reports["real"]:
        tag = "OK" if ok else ("KNOWN-DEFECT" if expected_defect else "FAIL")
        line = f"  [{tag}] {key}"
        if msg:
            line += f"  -> {msg}"
        print(line)
        if expected_defect:
            for note in EXPECTED_DEFECTS[key]:
                print(f"       note: {note}")

    print("\n== Valid fixtures (must PASS) ==")
    for name, ok, msg in reports["valid"]:
        print(f"  [{'OK' if ok else 'FAIL'}] {name}" + (f"  -> {msg}" if msg else ""))

    print("\n== Invalid fixtures (must FAIL) ==")
    for name, ok, msg in reports["invalid"]:
        print(f"  [{'OK' if ok else 'FAIL'}] {name}" + (f"  -> {msg}" if msg else ""))

    print("\n== World-asset inventory cross-validation ==")
    for ok, msg in reports["inventory"]:
        print(f"  [{'OK' if ok else 'FAIL'}] al-world-asset-inventory  -> {msg}")

    print("\n== Realm character taxonomy cross-validation ==")
    for ok, msg in reports["realmTaxonomy"]:
        print(f"  [{'OK' if ok else 'FAIL'}] al-realm-character-taxonomy  -> {msg}")

    print("\n== Integrated four-realm taxonomy cross-validation ==")
    for ok, msg in reports["fourRealmTaxonomy"]:
        print(
            f"  [{'OK' if ok else 'FAIL'}] "
            f"al-four-realm-production-taxonomy  -> {msg}"
        )

    print("\n== Rig and required-motion cross-validation ==")
    for ok, msg in reports["rigMotion"]:
        print(
            f"  [{'OK' if ok else 'FAIL'}] "
            f"al-rig-motion-standard  -> {msg}"
        )

    print("\n== Model/motion/skill-VFX harness ==")
    for ok, msg in reports["modelMotionSkillVfx"]:
        print(
            f"  [{'OK' if ok else 'FAIL'}] "
            f"al-model-motion-skill-vfx-harness  -> {msg}"
        )

    print()
    if failures:
        print(f"FAILED: {len(failures)} problem(s)")
        for f in failures:
            print(f"  - {f}")
        return 1

    print("ALL CHECKS PASSED")
    return 0


if __name__ == "__main__":
    sys.exit(main())
