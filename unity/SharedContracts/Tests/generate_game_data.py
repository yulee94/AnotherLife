#!/usr/bin/env python3
"""Generate the canonical StreamingAssets/GameData catalog set for Another Life.

t_1ec2c64f lands the six-family runtime loader: six flat-envelope files
plus a 6-artifact catalog-set.json (D1=A). Content catalogs stay on their
existing AL runtime paths and are also copied to the Unity StreamingAssets root.

    unity/Assets/StreamingAssets/GameData/

Behavior:
  1. Copies the 12 existing GameData catalogs byte-for-byte from the legacy
     AL/StreamingAssets/GameData root to the canonical root, EXCEPT it corrects
     the known inventory defect #5 in al_world_event_content_catalog.json
     (notification.world_event.* dotted placeholders -> canonical
     al_notify_world_event_started).
  2. Generates six flat loader envelopes ({realms,buildings,research,troops,
     champions,skills}.v1.json) plus the combined al_six_family_catalog.json
     (schema al-six-family) from the observed legacy values.
  3. Generates al_canonical_contracts.json (schema al-canonical-contracts).
  4. Generates a 6-artifact catalog-set.json with sourceMode "generated"
     and SHA-256 pins. catalogSetId is lower_snake_case (loader policy).

Usage:  uv run --with jsonschema generate_game_data.py
"""

import hashlib
import json
import pathlib
import re
import shutil

from jsonschema import Draft202012Validator

UNITY = pathlib.Path(__file__).resolve().parent.parent.parent  # unity/
LEGACY_GAMEDATA = UNITY / "Assets" / "AL" / "StreamingAssets" / "GameData"
CANONICAL_GAMEDATA = UNITY / "Assets" / "StreamingAssets" / "GameData"
SCHEMAS_DIR = UNITY / "SharedContracts" / "Schemas"

SOURCE_REVISION = "t_1ec2c64f"
CONTENT_VERSION = "1.0.0"
GAME_ID = "another-life"

# schema name (no .schema.json) -> canonical file name
CONTENT_SCHEMA_TO_FILE = {
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
    "al-character-customization": "al_character_customization_catalog.json",
    "al-skill-weather": "al_skill_weather_catalog.json",
}

# Binding order matches GameDataSixFamilySchemas.FamilyOrder.
SIX_FAMILY_ORDER = (
    "realms",
    "buildings",
    "research",
    "troops",
    "champions",
    "skills",
)


def sha256_hex(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def write_json(path, obj):
    # LF-only bytes so SHA-256 pins match the C# strict reader on Windows.
    path.write_bytes((json.dumps(obj, indent=2) + "\n").encode("utf-8"))


# ---------------------------------------------------------------------------
# 1. Copy the 12 legacy catalogs byte-for-byte, then correct world-event #5.
# ---------------------------------------------------------------------------
def copy_content_catalogs():
    CANONICAL_GAMEDATA.mkdir(parents=True, exist_ok=True)
    copied = []
    for schema_name, filename in CONTENT_SCHEMA_TO_FILE.items():
        src = LEGACY_GAMEDATA / filename
        dst = CANONICAL_GAMEDATA / filename
        shutil.copy2(src, dst)
        copied.append(dst)

    # Correct defect #5: dotted placeholders -> canonical started notification.
    world_event = CANONICAL_GAMEDATA / "al_world_event_content_catalog.json"
    text = world_event.read_text(encoding="utf-8")
    text = re.sub(
        r'"notificationDefinitionId": "notification\.world_event\.[a-z_]+"',
        '"notificationDefinitionId": "al_notify_world_event_started"',
        text,
    )
    world_event.write_bytes(text.encode("utf-8"))
    return copied


# ---------------------------------------------------------------------------
# 2. Six-family catalog (converts hardcoded + registry + test-only data).
# ---------------------------------------------------------------------------
REALMS = [
    ("crownlands", "Crownlands", 3, "inner_crownlands",
     "gate_crownlands_meridian", "warzone_crownlands", "royal_sigil",
     "battle_realm_crownlands",
     "Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Crownlands_Flat_256_v001.png"),
    ("stonehold", "Stonehold", 1, "inner_stonehold",
     "gate_stonehold_faultline", "warzone_stonehold", "deep_ore",
     "battle_realm_stonehold",
     "Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Stonehold_Flat_256_v001.png"),
    ("eldergrove", "Eldergrove", 2, "inner_eldergrove",
     "gate_eldergrove_greenveil", "warzone_eldergrove", "world_sap",
     "battle_realm_eldergrove",
     "Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Eldergrove_Flat_256_v001.png"),
    ("umbral", "Umbral", 4, "inner_umbral",
     "gate_umbral_ashvein", "warzone_umbral", "dark_crystal",
     "battle_realm_umbral",
     "Assets/AL/Art/Heraldry/RuntimeExports/S_ArcaneAxis_Umbral_Flat_256_v001.png"),
]

BUILDINGS = [
    ("town_hall", "TownHall"), ("farm", "Farm"), ("lumber_mill", "LumberMill"),
    ("quarry", "Quarry"), ("gold_mine", "GoldMine"), ("barracks", "Barracks"),
    ("academy", "Academy"), ("market", "Market"), ("storehouse", "Storehouse"),
    ("forge", "Forge"), ("stable", "Stable"), ("workshop", "Workshop"),
    ("embassy", "Embassy"), ("wall", "Wall"), ("watchtower", "Watchtower"),
]

# (canonical_id, name_ref, effect_ids)  — effects are only observed for the two
# that LocalResearchService maps; the rest carry a derived placeholder effect.
RESEARCH = [
    ("steel_forging", "research.steel_forging.name", ["effect_attack"]),
    ("plate_armor", "research.plate_armor.name", ["effect_defense"]),
    ("masonry", "research.advanced_masonry.name", ["effect_masonry"]),
    ("irrigation", "research.irrigation.name", ["effect_irrigation"]),
    ("ballistics", "research.ballistics.name", ["effect_ballistics"]),
    ("logistics", "research.logistics.name", ["effect_logistics"]),
    ("trade_routes", "research.trade_routes.name", ["effect_trade_routes"]),
    ("arcane_study", "research.arcane_study.name", ["effect_arcane_study"]),
]

# (id, legacy_type, legacy_value, base_power) from TroopType enum + battle sim.
TROOPS = [
    ("troop_infantry", "Infantry", 0, 10),
    ("troop_cavalry", "Cavalry", 1, 15),
    ("troop_ranged", "Ranged", 2, 12),
    ("troop_siege", "Siege", 3, 20),
]

# (id, name, cooldown, mana, cast, range, power, target_type, vfx_key)
SKILLS = [
    ("skill_realm_strike", "Realm Strike", 4.0, 20.0, 0.05, 2.6, 150.0, "enemy", "realm_slash"),
    ("skill_renewing_guard", "Renewing Guard", 8.0, 30.0, 0.35, 0.0, 180.0, "self", "renewing_guard"),
    ("skill_warzone_burst", "Warzone Burst", 10.0, 45.0, 0.45, 4.2, 115.0, "aoe", "warzone_shockwave"),
    ("skill_warmaster_breaker", "Warmaster Breaker", 14.0, 60.0, 0.65, 3.4, 260.0, "enemy", "warmaster_breaker"),
    ("skill_iron_bulwark", "Iron Bulwark", 6.0, 0.0, 0.0, 0.0, 1.0, "self", "iron_bulwark"),
    ("skill_shield_slam", "Shield Slam", 6.0, 0.0, 0.0, 0.0, 1.0, "single", "shield_slam"),
    ("skill_arcane_bolt", "Arcane Bolt", 6.0, 0.0, 0.0, 0.0, 1.0, "single", "arcane_bolt"),
    ("skill_verdant_nova", "Verdant Nova", 6.0, 0.0, 0.0, 0.0, 1.0, "aoe", "verdant_nova"),
    ("skill_piercing_shot", "Piercing Shot", 6.0, 0.0, 0.0, 0.0, 1.0, "single", "piercing_shot"),
    ("skill_hawk_eye", "Hawk Eye", 6.0, 0.0, 0.0, 0.0, 1.0, "self", "hawk_eye"),
    ("skill_shadowstep", "Shadowstep", 6.0, 0.0, 0.0, 0.0, 1.0, "self", "shadowstep"),
    ("skill_umbral_execute", "Umbral Execute", 6.0, 0.0, 0.0, 0.0, 1.0, "single", "umbral_execute"),
]


def alias_row(legacy_id, canonical_id):
    return {
        "legacyId": legacy_id,
        "canonicalId": canonical_id,
        "introducedVersion": 1,
        "retirementVersion": None,
        "migrationIssue": "#183",
    }


def build_six_family_records():
    realm_records = []
    realm_aliases = []
    for (rid, legacy, val, inner, gate, war, rare, cap, asset) in REALMS:
        realm_records.append({
            "id": rid,
            "legacy_realm_id": legacy,
            "legacy_realm_value": val,
            "name_ref": f"realm.{rid}.name",
            "description_ref": f"realm.{rid}.description",
            "inner_realm_id": inner,
            "main_gate_id": gate,
            "outer_warzone_id": war,
            "rare_resource_id": rare,
            "capability_profile_ids": [cap],
            "asset_ref": asset,
        })
        realm_aliases.append(alias_row(legacy, rid))

    building_records = []
    building_aliases = []
    for sid, legacy in BUILDINGS:
        building_records.append({
            "id": sid,
            "legacy_building_id": legacy,
            "name_ref": f"building.{sid}.name",
            "initial_level": 0,
            "max_level": 10,
            "production_profile_ids": [f"production_{sid}"],
            "cost_profile_id": f"building_upgrade_cost_{sid}",
            "duration_profile_id": "building_upgrade_duration_common",
            "prerequisite_profile_id": "building_prerequisite_none",
            "realm_eligibility_profile_id": "building_realm_eligibility_all",
            "asset_ref": f"Assets/AL/Art/Buildings/{sid}.png",
        })
        building_aliases.append(alias_row(legacy, sid))

    research_records = []
    research_aliases = []
    for sid, name_ref, effects in RESEARCH:
        canonical = f"research_{sid}"
        research_records.append({
            "id": canonical,
            "name_ref": name_ref,
            "max_level": 5,
            "cost_profile_id": "research_cost_common",
            "duration_profile_id": "research_duration_common",
            "effect_ids": effects,
            "prerequisite_research_ids": [],
        })
        # LocalGameDataService used display-string IDs ("Steel Forging").
        display = " ".join(part.capitalize() for part in sid.split("_"))
        if sid == "masonry":
            display = "Advanced Masonry"
        research_aliases.append(alias_row(display, canonical))

    troop_records = []
    troop_aliases = []
    for sid, legacy, val, power in TROOPS:
        troop_records.append({
            "id": sid,
            "legacy_troop_type": legacy,
            "legacy_troop_value": val,
            "name_ref": f"troop.{sid.removeprefix('troop_')}.name",
            "base_attack": power,
            "base_defense": power,
            "training_profile_id": f"training_{sid.removeprefix('troop_')}",
            "asset_ref": f"Assets/AL/Art/Troops/{sid.removeprefix('troop_')}.png",
        })
        troop_aliases.append(alias_row(legacy, sid))

    # Observed greybox slice champions from LocalGameDataService (character-creation PR).
    champion_records = [
        {
            "id": "champion_stonehold_vanguard",
            "name_ref": "champion.stonehold_vanguard.name",
            "realm_id": "stonehold",
            "class_family_id": "warrior",
            "portrait_asset_ref": "Assets/AL/Art/Champions/stonehold_vanguard_portrait.png",
            "model_asset_ref": "Assets/AL/Art/Champions/stonehold_vanguard_model.png",
            "base_skill_ids": ["skill_iron_bulwark", "skill_shield_slam"],
            "stat_profile_id": "champion_stat_stonehold_vanguard",
        },
        {
            "id": "champion_eldergrove_archmage",
            "name_ref": "champion.eldergrove_archmage.name",
            "realm_id": "eldergrove",
            "class_family_id": "mage",
            "portrait_asset_ref": "Assets/AL/Art/Champions/eldergrove_archmage_portrait.png",
            "model_asset_ref": "Assets/AL/Art/Champions/eldergrove_archmage_model.png",
            "base_skill_ids": ["skill_arcane_bolt", "skill_verdant_nova"],
            "stat_profile_id": "champion_stat_eldergrove_archmage",
        },
        {
            "id": "champion_crownlands_sharpshooter",
            "name_ref": "champion.crownlands_sharpshooter.name",
            "realm_id": "crownlands",
            "class_family_id": "ranger",
            "portrait_asset_ref": "Assets/AL/Art/Champions/crownlands_sharpshooter_portrait.png",
            "model_asset_ref": "Assets/AL/Art/Champions/crownlands_sharpshooter_model.png",
            "base_skill_ids": ["skill_piercing_shot", "skill_hawk_eye"],
            "stat_profile_id": "champion_stat_crownlands_sharpshooter",
        },
        {
            "id": "champion_umbral_shadowblade",
            "name_ref": "champion.umbral_shadowblade.name",
            "realm_id": "umbral",
            "class_family_id": "assassin",
            "portrait_asset_ref": "Assets/AL/Art/Champions/umbral_shadowblade_portrait.png",
            "model_asset_ref": "Assets/AL/Art/Champions/umbral_shadowblade_model.png",
            "base_skill_ids": ["skill_shadowstep", "skill_umbral_execute"],
            "stat_profile_id": "champion_stat_umbral_shadowblade",
        },
    ]
    champion_aliases = [
        alias_row("Bronn Ironhide", "champion_stonehold_vanguard"),
        alias_row("Lyra Moonshadow", "champion_eldergrove_archmage"),
        alias_row("Aurelia Dawnblade", "champion_crownlands_sharpshooter"),
        alias_row("Vex Nocturne", "champion_umbral_shadowblade"),
    ]

    skill_records = []
    skill_aliases = []
    for sid, name, cd, mana, cast, rng, power, target, vfx in SKILLS:
        base = sid.removeprefix("skill_")
        skill_records.append({
            "id": sid,
            "name_ref": f"skill.{base}.name",
            "behavior_profile_id": f"skill_behavior_{base}",
            "presentation_profile_id": f"skill_presentation_{base}",
            "target_type": target,
            "cooldown_seconds": cd,
            "power": power,
            "mana_cost": mana,
            "cast_time_seconds": cast,
            "range_meters": rng,
            "vfx_asset_ref": f"Assets/AL/Art/VFX/{vfx}.vfx",
            "audio_asset_ref": f"Assets/AL/Audio/{base}.wav",
        })
        skill_aliases.append(alias_row(name, sid))

    return {
        "realms": (realm_records, realm_aliases),
        "buildings": (building_records, building_aliases),
        "research": (research_records, research_aliases),
        "troops": (troop_records, troop_aliases),
        "champions": (champion_records, champion_aliases),
        "skills": (skill_records, skill_aliases),
    }


def build_six_family():
    families = {}
    for family, (records, _aliases) in build_six_family_records().items():
        families[family] = {
            "schemaVersion": 1,
            "contentVersion": CONTENT_VERSION,
            "records": records,
        }
    return {"schemaVersion": 1, "families": families}


def build_flat_family(family, records, aliases):
    return {
        "gameId": GAME_ID,
        "catalogId": f"{family}_v1",
        "family": family,
        "schemaVersion": 1,
        "contentVersion": CONTENT_VERSION,
        "sourceRevision": SOURCE_REVISION,
        "records": records,
        "aliases": aliases,
    }


# ---------------------------------------------------------------------------
# 3. Canonical contracts card.
# ---------------------------------------------------------------------------
def build_canonical_contracts():
    return {
        "schemaVersion": 1,
        "realmIds": ["crownlands", "stonehold", "eldergrove", "umbral"],
        "legacyRealmNames": ["Crownlands", "Stonehold", "Eldergrove", "Umbral"],
        "gemIds": [
            "gem_crownlands_sun", "gem_crownlands_oath",
            "gem_stonehold_forge", "gem_stonehold_depth",
            "gem_eldergrove_root", "gem_eldergrove_moon",
            "gem_umbral_veil", "gem_umbral_ember",
        ],
        "buildingIds": [b[0] for b in BUILDINGS],
        "legacyBuildingIds": [b[1] for b in BUILDINGS],
        "warzoneCredits": 0,
        "territoryIncomePerMinute": 50,
        "chapter1Ids": [
            "ch01_proof_of_worth", "ch01_stonehold", "ch01_eldergrove",
            "ch01_crownlands", "ch01_umbral",
        ],
        "legacyChapterReferences": [
            "C1", "C1_SH", "C1_EG", "C1_CL", "C1_UM", "CH01_PROOF_OF_WORTH",
        ],
    }


# ---------------------------------------------------------------------------
# 4. catalog-set.json manifest — six-family loader authority only.
# ---------------------------------------------------------------------------
def family_filename(family):
    return f"{family}.v1.json"


def build_manifest():
    artifacts = []
    for family in SIX_FAMILY_ORDER:
        filename = family_filename(family)
        path = CANONICAL_GAMEDATA / filename
        artifacts.append({
            "family": family,
            "catalogId": f"{family}_v1",
            "relativePath": filename,
            "schemaVersion": 1,
            "contentVersion": CONTENT_VERSION,
            "required": True,
            "sha256": sha256_hex(path),
            "mediaType": "application/json",
            "sourceMode": "generated",
            "sourceRevision": SOURCE_REVISION,
        })
    return {
        "gameId": GAME_ID,
        "catalogSetId": "six_family_catalog_set",
        "schemaVersion": 1,
        "contentVersion": CONTENT_VERSION,
        "minimumRuntimeCatalogVersion": 1,
        "sourceRevision": SOURCE_REVISION,
        "artifacts": artifacts,
    }


def write_six_family_files():
    families = build_six_family_records()
    write_json(CANONICAL_GAMEDATA / "al_six_family_catalog.json", build_six_family())
    for family in SIX_FAMILY_ORDER:
        records, aliases = families[family]
        write_json(
            CANONICAL_GAMEDATA / family_filename(family),
            build_flat_family(family, records, aliases),
        )


# ---------------------------------------------------------------------------
# Validation.
# ---------------------------------------------------------------------------
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


def validate_flat_envelopes():
    failures = []
    for family in SIX_FAMILY_ORDER:
        filename = family_filename(family)
        instance = json.loads((CANONICAL_GAMEDATA / filename).read_text(encoding="utf-8"))
        missing = [field for field in FLAT_ENVELOPE_FIELDS if field not in instance]
        if missing:
            failures.append((filename, f"missing envelope fields {missing}", []))
            print(f"  [FAIL] {filename}: missing envelope fields {missing}")
            continue
        if instance["gameId"] != GAME_ID or instance["family"] != family:
            failures.append((filename, "gameId/family mismatch", []))
            print(f"  [FAIL] {filename}: gameId/family mismatch")
            continue
        if instance["sourceRevision"] != SOURCE_REVISION:
            failures.append((filename, "sourceRevision mismatch", []))
            print(f"  [FAIL] {filename}: sourceRevision mismatch")
            continue
        if not instance["records"]:
            failures.append((filename, "records must not be empty", []))
            print(f"  [FAIL] {filename}: records must not be empty")
            continue
        print(f"  [ OK ] {filename} ({len(instance['records'])} records, {len(instance['aliases'])} aliases)")
    return failures


def validate_all():
    schemas = {}
    for schema_path in sorted(SCHEMAS_DIR.glob("*.schema.json")):
        schemas[schema_path.name[: -len(".schema.json")]] = json.loads(
            schema_path.read_text(encoding="utf-8")
        )

    # canonical file -> schema name
    checks = {filename: schema for schema, filename in CONTENT_SCHEMA_TO_FILE.items()}
    checks["al_six_family_catalog.json"] = "al-six-family"
    checks["al_canonical_contracts.json"] = "al-canonical-contracts"

    failures = []
    for filename, schema_name in sorted(checks.items()):
        schema = schemas[schema_name]
        validator = Draft202012Validator(schema)
        instance = json.loads((CANONICAL_GAMEDATA / filename).read_text(encoding="utf-8"))
        errors = list(validator.iter_errors(instance))
        if errors:
            failures.append((filename, errors[0].message, list(errors[0].absolute_path)))
            print(f"  [FAIL] {filename}: {errors[0].message} @ {list(errors[0].absolute_path)}")
        else:
            print(f"  [ OK ] {filename}")

    failures.extend(validate_flat_envelopes())

    # catalog-set.json has no schema; assert six-family pins and sourceMode.
    manifest = json.loads((CANONICAL_GAMEDATA / "catalog-set.json").read_text(encoding="utf-8"))
    artifacts = manifest["artifacts"]
    if len(artifacts) != len(SIX_FAMILY_ORDER):
        failures.append(("catalog-set.json", f"expected {len(SIX_FAMILY_ORDER)} artifacts", []))
        print(f"  [FAIL] catalog-set.json: expected {len(SIX_FAMILY_ORDER)} artifacts")
    elif any(item["sourceMode"] != "generated" for item in artifacts):
        failures.append(("catalog-set.json", "sourceMode must be generated", []))
        print("  [FAIL] catalog-set.json: sourceMode must be generated")
    elif manifest["catalogSetId"] != "six_family_catalog_set":
        failures.append(("catalog-set.json", "catalogSetId must be six_family_catalog_set", []))
        print("  [FAIL] catalog-set.json: catalogSetId must be six_family_catalog_set")
    else:
        print(f"  [ OK ] catalog-set.json ({len(artifacts)} artifacts)")

    return failures


def main():
    print("== copy content catalogs ==")
    copy_content_catalogs()
    print("== generate six-family (combined + 6 flat envelopes) ==")
    write_six_family_files()
    print("== generate canonical-contracts ==")
    write_json(CANONICAL_GAMEDATA / "al_canonical_contracts.json", build_canonical_contracts())
    print("== generate six-family manifest ==")
    write_json(CANONICAL_GAMEDATA / "catalog-set.json", build_manifest())
    print("\n== validate ==")
    failures = validate_all()
    if failures:
        print(f"\nFAILED: {len(failures)} file(s) did not validate")
        return 1
    print("\nALL CANONICAL GAMEDATA FILES VALIDATE")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
