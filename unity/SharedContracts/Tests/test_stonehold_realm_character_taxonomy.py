#!/usr/bin/env python3
"""Coverage tests for the complete Stonehold realm taxonomy catalog."""

from __future__ import annotations

import json
import unittest
from collections import defaultdict
from pathlib import Path

import realm_character_taxonomy as contract


CATALOG_PATH = Path("unity/Assets/AL/StreamingAssets/GameData/al_stonehold_realm_character_taxonomy.json")
COMMON_LEVELS = [1, 3, 6, 9, 14, 20, 26, 30]
SUBCLASS_LEVELS = [31, 33, 36, 39, 42, 45, 48, 50]
EXPECTED_PROGRESSION = {
    ("warrior", "common"): ["Steel Chain", "Brace", "Warstep", "Shield Bash", "Sweeping Cleave", "War Cry", "Reprisal", "Indomitable"],
    ("warrior", "berserker"): ["Fury Engine", "Rending Blow", "Blood Rush", "Terror Howl", "Rampage", "Blood Price", "Avalanche", "Crimson Zenith"],
    ("warrior", "guardian"): ["Guardian's Oath", "Sovereign Challenge", "Bulwark Wall", "Earthshaker", "Intercede", "Defiant Banner", "Last Bastion", "Dragonhold"],
    ("mage", "common"): ["Arc Bolt", "Mana Step", "Arcane Ward", "Frost Seal", "Chain Arc", "Elemental Surge", "Counterspell", "Overchannel"],
    ("mage", "elementalist"): ["Triune Attunement", "Inferno Lance", "Glacial Prison", "Tempest Chain", "Elemental Rift", "Prismatic Step", "Cataclysm", "Triune Ascendance"],
    ("mage", "enchanter"): ["Enchanter's Weave", "Mending Sigil", "Arcane Imbuement", "Beguiling Chord", "Sanctuary Weave", "Null Hex", "Soul Recall", "Grand Concord"],
    ("ranger", "common"): ["Quickshot", "Skirmisher Roll", "Hunter's Mark", "Concussive Arrow", "Split Volley", "Fieldcraft", "Piercing Shot", "Predator's Tempo"],
    ("ranger", "sharpshooter"): ["Deadeye Discipline", "Armor-Piercing Shot", "Reposition", "Thunderhead Shot", "Ricochet", "Kill Zone", "Skybreaker", "Horizon Sentence"],
    ("ranger", "warden"): ["Trail Sense", "Beast Bond", "Brushveil", "Pouncing Command", "Pack Veil", "Quarry Relay", "Warband Shroud", "Apex Covenant"],
    ("assassin", "common"): ["Twin Fang", "Shadowstep", "Open Vein", "Nerve Strike", "Fan of Blades", "Smoke Flask", "Death Feint", "Killer's Tempo"],
    ("assassin", "shadowblade"): ["Shadowform", "Backstab", "Mirror Step", "Night Garrote", "Umbral Venom", "Dance of Knives", "Eclipse Double", "No Witness"],
    ("assassin", "infiltrator"): ["Covert Network", "Sabotage", "Smoke Screen", "Somnolent Needle", "Covering Step", "Disrupting Wire", "False Opening", "Perfect Breach"],
}
EXPECTED_CLASS_SOURCES = {
    "rct_stonehold_champion_warrior_warlord_v001": {
        "anotherlife.class.warrior.levels_1_30",
        "anotherlife.subclass.berserker.levels_31_50.offensive",
        "anotherlife.subclass.guardian.levels_31_50.support",
        "anotherlife.warmaster.warlord",
    },
    "rct_stonehold_champion_mage_spellmarshal_v001": {
        "anotherlife.class.mage.levels_1_30",
        "anotherlife.subclass.elementalist.levels_31_50.offensive",
        "anotherlife.subclass.enchanter.levels_31_50.support",
        "anotherlife.warmaster.spellmarshal",
    },
    "rct_stonehold_champion_ranger_huntermarshal_v001": {
        "anotherlife.class.ranger.levels_1_30",
        "anotherlife.subclass.sharpshooter.levels_31_50.offensive",
        "anotherlife.subclass.warden.levels_31_50.support",
        "anotherlife.warmaster.huntermarshal",
    },
    "rct_stonehold_champion_assassin_veilreaver_v001": {
        "anotherlife.class.assassin.levels_1_30",
        "anotherlife.subclass.shadowblade.levels_31_50.offensive",
        "anotherlife.subclass.infiltrator.levels_31_50.support",
        "anotherlife.warmaster.veilreaver",
    },
}


class StoneholdRealmTaxonomyCatalogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repo_root = Path(__file__).resolve().parents[3]
        cls.catalog_path = cls.repo_root / CATALOG_PATH
        cls.catalog = json.loads(cls.catalog_path.read_text(encoding="utf-8"))

    def test_catalog_passes_schema_and_semantic_validation(self) -> None:
        evidence = contract.validate_path(self.repo_root, self.catalog_path)
        self.assertEqual(100, evidence["skillCount"])
        self.assertEqual(100, evidence["traceabilityRows"])
        self.assertEqual(0, evidence["orphanReferenceCount"])
        self.assertEqual(0, evidence["missingMotionCount"])

    def test_stonehold_roster_is_complete_and_explicitly_gated(self) -> None:
        self.assertEqual(["Stonehold Dwarves"], [row["displayName"] for row in self.catalog["playableRaces"]])
        self.assertEqual({"civilian", "service", "quest", "combat"}, {row["role"] for row in self.catalog["npcArchetypes"]})
        service = next(row for row in self.catalog["npcArchetypes"] if row["role"] == "service")
        self.assertEqual("Master Gruff — Stonehold Service Worker", service["displayName"])
        self.assertEqual(set(EXPECTED_CLASS_SOURCES), {row["id"] for row in self.catalog["championFamilies"]})
        self.assertEqual(
            {"Basalt Grazer", "Rimefan Kite", "Oreveil Isopod", "Slagwhistle Burrower"},
            {row["displayName"] for row in self.catalog["beastFamilies"]},
        )
        self.assertEqual(
            {"Fault-Crowned Colossus", "Rimehorn Breaker", "Oreblind Delver", "Slaghide Gorer", "Iron Wyrm"},
            {row["displayName"] for row in self.catalog["monsterFamilies"]},
        )
        self.assertEqual("preparation_held", self.catalog["authority"]["status"])
        self.assertEqual("held", self.catalog["gatePolicy"]["generationState"])
        self.assertEqual("held", self.catalog["gatePolicy"]["activationState"])

    def test_champion_and_warmaster_family_sources_are_exact(self) -> None:
        actual = {row["id"]: set(row["classSourceIds"]) for row in self.catalog["championFamilies"]}
        self.assertEqual(EXPECTED_CLASS_SOURCES, actual)
        self.assertEqual([24, 24, 24, 24], sorted(len(row["skillIds"]) for row in self.catalog["championFamilies"]))

    def test_every_documented_progression_skill_is_present_at_its_level(self) -> None:
        actual_names: dict[tuple[str, str], list[str]] = defaultdict(list)
        actual_levels: dict[tuple[str, str], list[int]] = defaultdict(list)
        progression_rows = []
        for row in self.catalog["skills"]:
            external_id = row["externalSourceId"]
            if not external_id.startswith("anotherlife.class_progression."):
                continue
            progression_rows.append(row)
            _, _, family, branch, level_token, _ = external_id.split(".", 5)
            key = (family, branch)
            actual_names[key].append(row["displayName"])
            actual_levels[key].append(int(level_token.removeprefix("level_")))
            self.assertEqual("approved_fact", row["authority"]["status"])
            self.assertIn("session:20260821_042136_f1f42e:full-1-50-progression-approved", row["authority"]["approvalEvidenceRefs"])

        self.assertEqual(96, len(progression_rows))
        self.assertEqual(set(EXPECTED_PROGRESSION), set(actual_names))
        for key, names in EXPECTED_PROGRESSION.items():
            expected_levels = COMMON_LEVELS if key[1] == "common" else SUBCLASS_LEVELS
            ordered = sorted(zip(actual_levels[key], actual_names[key]))
            self.assertEqual(list(zip(expected_levels, names)), ordered)

    def test_identity_only_phase_c_skills_remain_inventoried_without_fake_mechanics(self) -> None:
        legacy = {
            row["externalSourceId"]: row["displayName"]
            for row in self.catalog["skills"]
            if row["externalSourceId"].startswith("phase_c.skill_identity.")
        }
        self.assertEqual(
            {
                "phase_c.skill_identity.realm_strike": "Realm Strike",
                "phase_c.skill_identity.renewing_guard": "Renewing Guard",
                "phase_c.skill_identity.warzone_burst": "Warzone Burst",
                "phase_c.skill_identity.warmaster_breaker": "Warmaster Breaker",
            },
            legacy,
        )
        trace_by_skill = {row["skillId"]: row for row in self.catalog["skillTraceability"]}
        for skill in self.catalog["skills"]:
            trace = trace_by_skill[skill["id"]]
            self.assertTrue(all(item["state"] == "not_applicable" and item["recordIds"] == [] for item in trace["motionPhases"].values()))
            self.assertTrue(all(item["state"] == "not_applicable" and item["recordIds"] == [] for item in trace["effects"].values()))


if __name__ == "__main__":
    unittest.main()
