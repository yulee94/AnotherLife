#!/usr/bin/env python3
"""Emit six-family slice catalogs from Phase C registries + content maps.

Does not invent combat/economy numbers. Realm/building identity comes from
GameDataRealmReferences / GameDataBuildingProgressionRegistry (mirrored here
as already-reviewed tuples). Champion runtime stats are relocated from the
retired LocalGameDataService archetype block. Research/troops are not emitted:
content maps mark them name-only or not_authored_unavailable.
"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "unity" / "Assets" / "AL" / "StreamingAssets" / "GameData"
CONTENT_MAP = ROOT / "unity" / "Docs" / "Narrative" / "GameData" / "phase-c-six-family-content-map.json"

SOURCE_REVISION = "t_5e063078"
CONTENT_VERSION = "1.0.0"
GAME_ID = "another-life"
MIGRATION_ISSUE = "#183"

# Phase C3A / GameDataRealmReferences.Entries (authored order).
REALMS = [
    {
        "id": "crownlands",
        "legacy_realm_id": "Crownlands",
        "legacy_realm_value": 3,
        "name_ref": "realm.crownlands.name",
        "description_ref": "realm.crownlands.description",
        "inner_realm_id": "inner_crownlands",
        "main_gate_id": "gate_crownlands_meridian",
        "outer_warzone_id": "warzone_crownlands",
        "rare_resource_id": "royal_sigil",
        "capability_profile_ids": ["battle_realm_crownlands"],
        "asset_ref": (
            "Assets/AL/Art/Heraldry/RuntimeExports/"
            "S_ArcaneAxis_Crownlands_Flat_256_v001.png"
        ),
    },
    {
        "id": "stonehold",
        "legacy_realm_id": "Stonehold",
        "legacy_realm_value": 1,
        "name_ref": "realm.stonehold.name",
        "description_ref": "realm.stonehold.description",
        "inner_realm_id": "inner_stonehold",
        "main_gate_id": "gate_stonehold_faultline",
        "outer_warzone_id": "warzone_stonehold",
        "rare_resource_id": "deep_ore",
        "capability_profile_ids": ["battle_realm_stonehold"],
        "asset_ref": (
            "Assets/AL/Art/Heraldry/RuntimeExports/"
            "S_ArcaneAxis_Stonehold_Flat_256_v001.png"
        ),
    },
    {
        "id": "eldergrove",
        "legacy_realm_id": "Eldergrove",
        "legacy_realm_value": 2,
        "name_ref": "realm.eldergrove.name",
        "description_ref": "realm.eldergrove.description",
        "inner_realm_id": "inner_eldergrove",
        "main_gate_id": "gate_eldergrove_greenveil",
        "outer_warzone_id": "warzone_eldergrove",
        "rare_resource_id": "world_sap",
        "capability_profile_ids": ["battle_realm_eldergrove"],
        "asset_ref": (
            "Assets/AL/Art/Heraldry/RuntimeExports/"
            "S_ArcaneAxis_Eldergrove_Flat_256_v001.png"
        ),
    },
    {
        "id": "umbral",
        "legacy_realm_id": "Umbral",
        "legacy_realm_value": 4,
        "name_ref": "realm.umbral.name",
        "description_ref": "realm.umbral.description",
        "inner_realm_id": "inner_umbral",
        "main_gate_id": "gate_umbral_ashvein",
        "outer_warzone_id": "warzone_umbral",
        "rare_resource_id": "dark_crystal",
        "capability_profile_ids": ["battle_realm_umbral"],
        "asset_ref": (
            "Assets/AL/Art/Heraldry/RuntimeExports/"
            "S_ArcaneAxis_Umbral_Flat_256_v001.png"
        ),
    },
]

# Phase C4A / GameDataBuildingProgressionRegistry.Entries.
BUILDINGS = [
    ("town_hall", "TownHall", "building.town_hall.name", "building_upgrade_cost_town_hall"),
    ("farm", "Farm", "building.farm.name", "building_upgrade_cost_farm"),
    ("lumber_mill", "LumberMill", "building.lumber_mill.name", "building_upgrade_cost_lumber_mill"),
    ("quarry", "Quarry", "building.quarry.name", "building_upgrade_cost_quarry"),
    ("gold_mine", "GoldMine", "building.gold_mine.name", "building_upgrade_cost_gold_mine"),
    ("barracks", "Barracks", "building.barracks.name", "building_upgrade_cost_barracks"),
    ("academy", "Academy", "building.academy.name", "building_upgrade_cost_academy"),
    ("market", "Market", "building.market.name", "building_upgrade_cost_market"),
    ("storehouse", "Storehouse", "building.storehouse.name", "building_upgrade_cost_storehouse"),
    ("forge", "Forge", "building.forge.name", "building_upgrade_cost_forge"),
    ("stable", "Stable", "building.stable.name", "building_upgrade_cost_stable"),
    ("workshop", "Workshop", "building.workshop.name", "building_upgrade_cost_workshop"),
    ("embassy", "Embassy", "building.embassy.name", "building_upgrade_cost_embassy"),
    ("wall", "Wall", "building.wall.name", "building_upgrade_cost_wall"),
    ("watchtower", "Watchtower", "building.watchtower.name", "building_upgrade_cost_watchtower"),
]

# Relocated from LocalGameDataService.InitializeChampionArchetypes (not invented).
# SpecialPower/DefendMitigation relocated from SliceChampionProfile.CreateDefault
# (the production duel already used those two fields for every confirmed champion).
CHAMPION_RUNTIME = [
    {
        "id": "champion_stonehold_vanguard",
        "display_name": "Bronn Ironhide",
        "realm_id": "stonehold",
        "class_family_id": "warrior",
        "subclass_id": "vanguard",
        "weapon_style_id": "greataxe",
        "offhand_style_id": "towershield",
        "max_health": 1250,
        "max_mana": 80,
        "attack": 55,
        "defense": 45,
        "speed": 8,
        "crit_rate": 5,
        "special_power": 90,
        "defend_mitigation": 0.5,
        "skills": [
            {"id": "skill_iron_bulwark", "display_name": "Iron Bulwark", "target_type": "self", "cooldown_seconds": 6.0, "power": 1.0},
            {"id": "skill_shield_slam", "display_name": "Shield Slam", "target_type": "single", "cooldown_seconds": 6.0, "power": 1.0},
        ],
        "portrait_asset_ref": "Assets/AL/Art/Champions/ConceptSheets/champion_stonehold_vanguard_turnaround_v001.png",
        "model_asset_ref": "Assets/AL/Art/Champions/ConceptSheets/champion_stonehold_vanguard_turnaround_v001.png",
        "name_ref": "champion.stonehold.vanguard.name",
        "stat_profile_id": "champion_stat_stonehold_vanguard",
    },
    {
        "id": "champion_eldergrove_archmage",
        "display_name": "Lyra Moonshadow",
        "realm_id": "eldergrove",
        "class_family_id": "mage",
        "subclass_id": "archmage",
        "weapon_style_id": "staff",
        "offhand_style_id": "tome",
        "max_health": 820,
        "max_mana": 150,
        "attack": 78,
        "defense": 18,
        "speed": 10,
        "crit_rate": 8,
        "special_power": 90,
        "defend_mitigation": 0.5,
        "skills": [
            {"id": "skill_arcane_bolt", "display_name": "Arcane Bolt", "target_type": "single", "cooldown_seconds": 6.0, "power": 1.0},
            {"id": "skill_verdant_nova", "display_name": "Verdant Nova", "target_type": "aoe", "cooldown_seconds": 6.0, "power": 1.0},
        ],
        "portrait_asset_ref": "Assets/AL/Art/Champions/ConceptSheets/champion_eldergrove_vanguard_turnaround_v001.png",
        "model_asset_ref": "Assets/AL/Art/Champions/ConceptSheets/champion_eldergrove_vanguard_turnaround_v001.png",
        "name_ref": "champion.eldergrove.archmage.name",
        "stat_profile_id": "champion_stat_eldergrove_archmage",
    },
    {
        "id": "champion_crownlands_sharpshooter",
        "display_name": "Aurelia Dawnblade",
        "realm_id": "crownlands",
        "class_family_id": "ranger",
        "subclass_id": "sharpshooter",
        "weapon_style_id": "longbow",
        "offhand_style_id": "quiver",
        "max_health": 900,
        "max_mana": 110,
        "attack": 62,
        "defense": 26,
        "speed": 15,
        "crit_rate": 20,
        "special_power": 90,
        "defend_mitigation": 0.5,
        "skills": [
            {"id": "skill_piercing_shot", "display_name": "Piercing Shot", "target_type": "single", "cooldown_seconds": 6.0, "power": 1.0},
            {"id": "skill_hawk_eye", "display_name": "Hawk Eye", "target_type": "self", "cooldown_seconds": 6.0, "power": 1.0},
        ],
        "portrait_asset_ref": "Assets/AL/Art/Champions/ConceptSheets/champion_crownlands_vanguard_turnaround_v001.png",
        "model_asset_ref": "Assets/AL/Art/Champions/ConceptSheets/champion_crownlands_vanguard_turnaround_v001.png",
        "name_ref": "champion.crownlands.sharpshooter.name",
        "stat_profile_id": "champion_stat_crownlands_sharpshooter",
    },
    {
        "id": "champion_umbral_shadowblade",
        "display_name": "Vex Nocturne",
        "realm_id": "umbral",
        "class_family_id": "assassin",
        "subclass_id": "shadowblade",
        "weapon_style_id": "twinblades",
        "offhand_style_id": "shroud",
        "max_health": 850,
        "max_mana": 100,
        "attack": 72,
        "defense": 16,
        "speed": 22,
        "crit_rate": 30,
        "special_power": 90,
        "defend_mitigation": 0.5,
        "skills": [
            {"id": "skill_shadowstep", "display_name": "Shadowstep", "target_type": "self", "cooldown_seconds": 6.0, "power": 1.0},
            {"id": "skill_umbral_execute", "display_name": "Umbral Execute", "target_type": "single", "cooldown_seconds": 6.0, "power": 1.0},
        ],
        "portrait_asset_ref": "Assets/AL/Art/Champions/ConceptSheets/champion_umbral_vanguard_turnaround_v001.png",
        "model_asset_ref": "Assets/AL/Art/Champions/ConceptSheets/champion_umbral_vanguard_turnaround_v001.png",
        "name_ref": "champion.umbral.shadowblade.name",
        "stat_profile_id": "champion_stat_umbral_shadowblade",
    },
]


def envelope(family: str, records: list, aliases: list) -> dict:
    return {
        "gameId": GAME_ID,
        "catalogId": family + "_v1",
        "family": family,
        "schemaVersion": 1,
        "contentVersion": CONTENT_VERSION,
        "sourceRevision": SOURCE_REVISION,
        "records": records,
        "aliases": aliases,
    }


def alias(legacy_id: str, canonical_id: str) -> dict:
    return {
        "legacyId": legacy_id,
        "canonicalId": canonical_id,
        "introducedVersion": 1,
        "retirementVersion": None,
        "migrationIssue": MIGRATION_ISSUE,
    }


def write(name: str, payload: dict) -> None:
    path = OUT / name
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("wrote", path.relative_to(ROOT), "records", len(payload.get("records", [])))


def assert_content_map_realms() -> None:
    data = json.loads(CONTENT_MAP.read_text(encoding="utf-8"))
    realm_family = next(f for f in data["families"] if f["family"] == "realms")
    keys = set()
    for entry in realm_family["entries"]:
        for item in entry.get("content", []):
            keys.add(item["key"])
    expected = {r["name_ref"] for r in REALMS} | {r["description_ref"] for r in REALMS}
    missing = expected - keys
    if missing:
        raise SystemExit("content map missing realm refs: " + ", ".join(sorted(missing)))


def main() -> int:
    assert_content_map_realms()
    OUT.mkdir(parents=True, exist_ok=True)

    write(
        "realms.json",
        envelope(
            "realms",
            REALMS,
            [alias(r["legacy_realm_id"], r["id"]) for r in REALMS],
        ),
    )

    building_records = []
    building_aliases = []
    for stable_id, legacy_id, name_ref, cost_id in BUILDINGS:
        building_records.append(
            {
                "id": stable_id,
                "legacy_building_id": legacy_id,
                "name_ref": name_ref,
                "initial_level": 0,
                "max_level": 10,
                "production_profile_ids": ["resource_output"],
                "cost_profile_id": cost_id,
                "duration_profile_id": "building_upgrade_duration_common",
                "prerequisite_profile_id": "building_prerequisite_none",
                "realm_eligibility_profile_id": "building_realm_eligibility_all",
                "asset_ref": "Assets/AL/Art/Buildings/" + stable_id + ".png",
            }
        )
        building_aliases.append(alias(legacy_id, stable_id))
    write("buildings.json", envelope("buildings", building_records, building_aliases))

    champion_records = []
    for row in CHAMPION_RUNTIME:
        champion_records.append(
            {
                "id": row["id"],
                "name_ref": row["name_ref"],
                "realm_id": row["realm_id"],
                "class_family_id": row["class_family_id"],
                "portrait_asset_ref": row["portrait_asset_ref"],
                "model_asset_ref": row["model_asset_ref"],
                "base_skill_ids": [skill["id"] for skill in row["skills"]],
                "stat_profile_id": row["stat_profile_id"],
            }
        )
    write("champions.json", envelope("champions", champion_records, []))

    skill_records = []
    seen_skills = set()
    for row in CHAMPION_RUNTIME:
        for skill in row["skills"]:
            if skill["id"] in seen_skills:
                continue
            seen_skills.add(skill["id"])
            skill_records.append(
                {
                    "id": skill["id"],
                    "name_ref": "skill." + skill["id"].replace("skill_", "", 1) + ".name",
                    "behavior_profile_id": "skill_behavior_" + skill["id"].replace("skill_", "", 1),
                    "presentation_profile_id": "skill_presentation_" + skill["id"].replace("skill_", "", 1),
                    "target_type": skill["target_type"],
                    "cooldown_seconds": skill["cooldown_seconds"],
                    "power": skill["power"],
                    "mana_cost": 0.0,
                    "cast_time_seconds": 0.0,
                    "range_meters": 0.0,
                    "vfx_asset_ref": row["portrait_asset_ref"],
                    "audio_asset_ref": row["portrait_asset_ref"],
                }
            )
    write("skills.json", envelope("skills", skill_records, []))

    runtime_records = []
    for row in CHAMPION_RUNTIME:
        runtime_records.append(
            {
                "id": row["id"],
                "display_name": row["display_name"],
                "realm_id": row["realm_id"],
                "class_family_id": row["class_family_id"],
                "subclass_id": row["subclass_id"],
                "weapon_style_id": row["weapon_style_id"],
                "offhand_style_id": row["offhand_style_id"],
                "max_health": row["max_health"],
                "max_mana": row["max_mana"],
                "attack": row["attack"],
                "defense": row["defense"],
                "speed": row["speed"],
                "crit_rate": row["crit_rate"],
                "special_power": row["special_power"],
                "defend_mitigation": row["defend_mitigation"],
                "skills": row["skills"],
            }
        )
    write(
        "champion_runtime.json",
        {
            "schemaVersion": 1,
            "contentVersion": CONTENT_VERSION,
            "sourceRevision": SOURCE_REVISION,
            "default_champion_id": "champion_stonehold_vanguard",
            "records": runtime_records,
        },
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
