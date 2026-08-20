#!/usr/bin/env python3
"""Generate SharedContracts test fixtures (valid + invalid).

Run once and commit the emitted JSON under Tests/fixtures/. Invalid fixtures are
deep-copies of a complete valid document with exactly one canonical-decision
violation injected, so each one fails for the intended reason.

Usage: uv run generate_fixtures.py  (or python generate_fixtures.py)
"""

import copy
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent          # Tests/
FIXTURES = ROOT / "fixtures"
VALID = FIXTURES / "valid"
INVALID = FIXTURES / "invalid"
GAMEDATA = ROOT.parent.parent / "Assets" / "AL" / "StreamingAssets" / "GameData"


def load(p):
    with open(p, "r", encoding="utf-8") as fh:
        return json.load(fh)


def write(path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, indent=2) + "\n", encoding="utf-8")
    print("wrote", path.relative_to(ROOT.parent.parent))


def mutate(base, **changes):
    d = copy.deepcopy(base)
    for k, v in changes.items():
        d[k] = v
    return d


REALM_IDS = ["crownlands", "stonehold", "eldergrove", "umbral"]
LEGACY_REALM_NAMES = ["Crownlands", "Stonehold", "Eldergrove", "Umbral"]
GEM_IDS = [
    "gem_crownlands_sun", "gem_crownlands_oath",
    "gem_stonehold_forge", "gem_stonehold_depth",
    "gem_eldergrove_root", "gem_eldergrove_moon",
    "gem_umbral_veil", "gem_umbral_ember",
]
BUILDINGS = [
    ("town_hall", "TownHall"), ("farm", "Farm"), ("lumber_mill", "LumberMill"),
    ("quarry", "Quarry"), ("gold_mine", "GoldMine"), ("barracks", "Barracks"),
    ("academy", "Academy"), ("market", "Market"), ("storehouse", "Storehouse"),
    ("forge", "Forge"), ("stable", "Stable"), ("workshop", "Workshop"),
    ("embassy", "Embassy"), ("wall", "Wall"), ("watchtower", "Watchtower"),
]


# ---------------------------------------------------------------------------
# Valid: canonical contracts card
# ---------------------------------------------------------------------------
contracts_valid = {
    "schemaVersion": 1,
    "realmIds": REALM_IDS,
    "legacyRealmNames": LEGACY_REALM_NAMES,
    "gemIds": GEM_IDS,
    "buildingIds": [b[0] for b in BUILDINGS],
    "legacyBuildingIds": [b[1] for b in BUILDINGS],
    "warzoneCredits": 123456,
    "territoryIncomePerMinute": 50,
    "chapter1Ids": [
        "ch01_proof_of_worth", "ch01_stonehold", "ch01_eldergrove",
        "ch01_crownlands", "ch01_umbral",
    ],
    "legacyChapterReferences": ["C1", "C1_SH", "C1_EG", "C1_CL", "C1_UM", "CH01_PROOF_OF_WORTH"],
}
write(VALID / "al-canonical-contracts.valid.json", contracts_valid)

# ---------------------------------------------------------------------------
# Valid: six-family
# ---------------------------------------------------------------------------
realm_meta = [
    ("crownlands", "Crownlands", 3, "inner_crownlands", "gate_crownlands_meridian", "warzone_crownlands"),
    ("stonehold", "Stonehold", 1, "inner_stonehold", "gate_stonehold_faultline", "warzone_stonehold"),
    ("eldergrove", "Eldergrove", 2, "inner_eldergrove", "gate_eldergrove_greenveil", "warzone_eldergrove"),
    ("umbral", "Umbral", 4, "inner_umbral", "gate_umbral_ashvein", "warzone_umbral"),
]
realm_records = [
    {
        "id": rid, "legacy_realm_id": legacy, "legacy_realm_value": val,
        "name_ref": f"realm.{rid}.name", "description_ref": f"realm.{rid}.description",
        "inner_realm_id": inner, "main_gate_id": gate, "outer_warzone_id": war,
        "rare_resource_id": "royal_sigil", "capability_profile_ids": [f"realm_capability_{rid}"],
        "asset_ref": f"Assets/AL/Art/Heraldry/{rid}.png",
    }
    for rid, legacy, val, inner, gate, war in realm_meta
]
building_records = [
    {
        "id": sid, "legacy_building_id": legacy, "name_ref": f"building.{sid}.name",
        "initial_level": 0, "max_level": 10,
        "production_profile_ids": [f"production_{sid}"],
        "cost_profile_id": f"building_upgrade_cost_{sid}",
        "duration_profile_id": "building_upgrade_duration_common",
        "prerequisite_profile_id": "building_prerequisite_none",
        "realm_eligibility_profile_id": "building_realm_eligibility_all",
        "asset_ref": f"Assets/AL/Art/Buildings/{sid}.png",
    }
    for sid, legacy in BUILDINGS
]
sixfamily_valid = {
    "schemaVersion": 1,
    "families": {
        "realms": {"schemaVersion": 1, "contentVersion": "1.0.0", "records": realm_records},
        "buildings": {"schemaVersion": 1, "contentVersion": "1.0.0", "records": building_records},
        "research": {
            "schemaVersion": 1, "contentVersion": "1.0.0",
            "records": [{
                "id": "research_steel_forging", "name_ref": "research.steel_forging.name",
                "max_level": 5, "cost_profile_id": "research_cost_common",
                "duration_profile_id": "research_duration_common",
                "effect_ids": ["effect_attack"], "prerequisite_research_ids": [],
            }],
        },
        "troops": {
            "schemaVersion": 1, "contentVersion": "1.0.0",
            "records": [{
                "id": "troop_infantry", "legacy_troop_type": "Infantry", "legacy_troop_value": 0,
                "name_ref": "troop.infantry.name", "base_attack": 10, "base_defense": 10,
                "training_profile_id": "training_infantry",
                "asset_ref": "Assets/AL/Art/Troops/infantry.png",
            }],
        },
        "champions": {
            "schemaVersion": 1, "contentVersion": "1.0.0",
            "records": [{
                "id": "champion_warden", "name_ref": "champion.warden.name",
                "realm_id": "stonehold", "class_family_id": "warrior",
                "portrait_asset_ref": "Assets/AL/Art/Champions/warden_portrait.png",
                "model_asset_ref": "Assets/AL/Art/Champions/warden_model.png",
                "base_skill_ids": ["skill_realm_strike"], "stat_profile_id": "champion_stat_warden",
            }],
        },
        "skills": {
            "schemaVersion": 1, "contentVersion": "1.0.0",
            "records": [{
                "id": "skill_realm_strike", "name_ref": "skill.realm_strike.name",
                "behavior_profile_id": "skill_behavior_strike",
                "presentation_profile_id": "skill_presentation_strike",
                "target_type": "enemy", "cooldown_seconds": 8.0, "power": 100.0,
                "mana_cost": 30.0, "cast_time_seconds": 0.5, "range_meters": 12.0,
                "vfx_asset_ref": "Assets/AL/Art/VFX/strike.vfx",
                "audio_asset_ref": "Assets/AL/Audio/strike.wav",
            }],
        },
    },
}
write(VALID / "al-six-family.valid.json", sixfamily_valid)

# ---------------------------------------------------------------------------
# Valid: world-event (corrected notification ids)
# ---------------------------------------------------------------------------
world_event = load(GAMEDATA / "al_world_event_content_catalog.json")
for ev in world_event["eventDefinitions"]:
    ev["notificationDefinitionId"] = "al_notify_world_event_started"
write(VALID / "al-world-event-content.valid.json", world_event)

# ---------------------------------------------------------------------------
# Invalid: canonical-contracts (each violates one decision)
# ---------------------------------------------------------------------------
write(INVALID / "al-canonical-contracts.invalid.mana_shrine.json",
      mutate(contracts_valid, buildingIds=[b[0] for b in BUILDINGS] + ["mana_shrine"]))
write(INVALID / "al-canonical-contracts.invalid.float_credits.json",
      mutate(contracts_valid, warzoneCredits=1.5))
write(INVALID / "al-canonical-contracts.invalid.negative_credits.json",
      mutate(contracts_valid, warzoneCredits=-1))
write(INVALID / "al-canonical-contracts.invalid.income_float.json",
      mutate(contracts_valid, territoryIncomePerMinute=12.5))
write(INVALID / "al-canonical-contracts.invalid.uppercase_realm.json",
      mutate(contracts_valid, realmIds=["crownlands", "stonehold", "eldergrove", "UMBRAL"]))
write(INVALID / "al-canonical-contracts.invalid.legacy_chapter.json",
      mutate(contracts_valid,
             chapter1Ids=["ch01_proof_of_worth", "ch01_stonehold", "ch01_eldergrove",
                          "ch01_crownlands", "C1"]))

# ---------------------------------------------------------------------------
# Invalid: six-family
# ---------------------------------------------------------------------------
def mutate_family_record(base, family, index, **changes):
    d = copy.deepcopy(base)
    rec = d["families"][family]["records"][index]
    for k, v in changes.items():
        rec[k] = v
    return d


write(INVALID / "al-six-family.invalid.mana_shrine_building.json",
      mutate_family_record(sixfamily_valid, "buildings", 0, id="mana_shrine",
                           legacy_building_id="ManaShrine"))
write(INVALID / "al-six-family.invalid.uppercase_realm.json",
      mutate_family_record(sixfamily_valid, "realms", 0, id="Crownlands"))
write(INVALID / "al-six-family.invalid.float_attack.json",
      mutate_family_record(sixfamily_valid, "troops", 0, base_attack=10.5))

# ---------------------------------------------------------------------------
# Invalid: content catalogs (derived from the real files)
# ---------------------------------------------------------------------------
realm = load(GAMEDATA / "al_realm_catalog.json")
r = copy.deepcopy(realm)
r["realms"][0]["id"] = "Crownlands"
write(INVALID / "al-realm.invalid.uppercase_realm_id.json", r)

r = copy.deepcopy(realm)
r["realms"][0]["realmGemIds"] = ["gem_crownlands_sun", "gem_crownlands_oath", "gem_stonehold_forge"]
write(INVALID / "al-realm.invalid.three_gems.json", r)

r = copy.deepcopy(realm)
r["realms"][0]["realmGemIds"] = ["gem_stonehold_forge", "gem_stonehold_depth"]
write(INVALID / "al-realm.invalid.cross_realm_gem.json", r)

wishgate = load(GAMEDATA / "al_realm_gem_wishgate_content_catalog.json")
w = copy.deepcopy(wishgate)
w["realmGems"] = w["realmGems"][:7]  # only 7 gems
write(INVALID / "al-realm-gem-wishgate-content.invalid.seven_gems.json", w)

w = copy.deepcopy(wishgate)
w["realmGems"][0]["realmId"] = "stonehold"  # gem_crownlands_sun under stonehold
write(INVALID / "al-realm-gem-wishgate-content.invalid.cross_realm_gem.json", w)

notif = load(GAMEDATA / "al_notification_content_catalog.json")
n = copy.deepcopy(notif)
n["definitions"][0]["id"] = "AL_NOTIFY_SAVE_RECOVERED_BACKUP"
write(INVALID / "al-notification-content.invalid.uppercase_id.json", n)

we = load(GAMEDATA / "al_world_event_content_catalog.json")
we2 = copy.deepcopy(we)
we2["eventDefinitions"][0]["notificationDefinitionId"] = "notification.world_event.siege"
write(INVALID / "al-world-event-content.invalid.legacy_notification_id.json", we2)

rel = load(GAMEDATA / "al_relationship_authority_content_catalog.json")
rel2 = copy.deepcopy(rel)
rel2["factionRecords"][0]["parentRealmId"] = "Crownlands"
write(INVALID / "al-relationship-authority-content.invalid.uppercase_realm.json", rel2)

rel2 = copy.deepcopy(rel)
rel2["npcRecords"][0]["initialAffinity"] = 0.5
write(INVALID / "al-relationship-authority-content.invalid.float_affinity.json", rel2)

print("\nFixtures generated.")
