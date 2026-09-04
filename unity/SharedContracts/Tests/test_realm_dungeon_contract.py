#!/usr/bin/env python3
"""Focused fail-closed checks for the public realm-dungeon catalog contract."""

import json
import pathlib
import unittest

from jsonschema import Draft202012Validator


EXPECTED_DUNGEONS = (
    (
        "realm_dungeon_crownlands_deep",
        "crownlands",
        "raid_dragon_crownlands_dawn_regent",
        "dragon_crownlands_dawn_regent",
    ),
    (
        "realm_dungeon_stonehold_deep",
        "stonehold",
        "raid_dragon_stonehold_iron_wyrm",
        "dragon_stonehold_iron_wyrm",
    ),
    (
        "realm_dungeon_eldergrove_deep",
        "eldergrove",
        "raid_dragon_eldergrove_moonbough",
        "dragon_eldergrove_moonbough",
    ),
    (
        "realm_dungeon_umbral_deep",
        "umbral",
        "raid_dragon_umbral_void_seraph",
        "dragon_umbral_void_seraph",
    ),
)

GUARDIAN_DRAGON_IDS = tuple(row[3] for row in EXPECTED_DUNGEONS)
GUILD_CLOSED_BOSS_IDS = (
    "raid_boss_iron_colossus",
    "raid_boss_ash_seraph",
    "raid_boss_thorn_wraith",
    "raid_boss_veil_regent",
)


class RealmDungeonContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo_root = pathlib.Path(__file__).resolve().parents[3]
        cls.schema_path = (
            cls.repo_root
            / "unity"
            / "SharedContracts"
            / "Schemas"
            / "al-realm-dungeon.schema.json"
        )
        cls.catalog_path = (
            cls.repo_root
            / "unity"
            / "Assets"
            / "AL"
            / "StreamingAssets"
            / "GameData"
            / "al_realm_dungeon_catalog.json"
        )
        cls.fixtures = pathlib.Path(__file__).resolve().parent / "fixtures"
        cls.schema = json.loads(cls.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.validator = Draft202012Validator(cls.schema)

    def test_real_catalog_and_valid_fixture_match_and_validate(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))
        fixture = json.loads(
            (self.fixtures / "valid" / "al-realm-dungeon.valid.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(fixture, catalog)
        self.assertEqual([], list(self.validator.iter_errors(catalog)))

    def test_every_invalid_fixture_is_rejected(self):
        invalid_paths = sorted(
            (self.fixtures / "invalid").glob("al-realm-dungeon.invalid.*.json")
        )
        self.assertGreaterEqual(len(invalid_paths), 4)
        for path in invalid_paths:
            with self.subTest(path=path.name):
                instance = json.loads(path.read_text(encoding="utf-8"))
                errors = list(self.validator.iter_errors(instance))
                self.assertEqual(1, len(errors), [error.message for error in errors])

    def test_exact_roster_cooldown_portal_and_alias_rejection_are_exact(self):
        catalog = json.loads(self.catalog_path.read_text(encoding="utf-8"))

        self.assertEqual("RealmDungeon", catalog["machineIdentity"])
        self.assertEqual("al_realm_dungeon_catalog", catalog["catalogId"])
        self.assertEqual("public_open_world_underground", catalog["scope"]["kind"])
        self.assertFalse(catalog["scope"]["clientAuthority"])
        self.assertFalse(catalog["scope"]["runtimeActivation"])
        self.assertFalse(catalog["scope"]["network"])
        self.assertFalse(catalog["scope"]["save"])
        self.assertFalse(catalog["scope"]["scenes"])
        self.assertFalse(catalog["scope"]["balance"])
        self.assertFalse(catalog["scope"]["guildClosedInstanceAlias"])
        self.assertEqual(
            "al_realm_catalog", catalog["consumedContracts"]["realmCatalogId"]
        )
        self.assertEqual(
            "al_guild_raid_muster_policy",
            catalog["consumedContracts"]["guildRaidMusterCatalogId"],
        )
        self.assertEqual("trusted_server_unix_seconds", catalog["clock"]["kind"])
        self.assertEqual(604800, catalog["clock"]["cooldownSeconds"])
        self.assertTrue(catalog["clock"]["killOnly"])
        self.assertEqual(
            "sealed_outward_only_manifestation", catalog["portal"]["kind"]
        )
        self.assertFalse(catalog["portal"]["inwardTraversal"])
        self.assertFalse(catalog["portal"]["ambientNavigation"])
        self.assertEqual("fenced_spawn_cycle", catalog["portal"]["lease"])
        self.assertFalse(catalog["productionGate"]["eligibleWithoutApprovedBundle"])
        self.assertEqual("fail_closed", catalog["productionGate"]["missingAssetPolicy"])
        self.assertFalse(catalog["productionGate"]["genericFallback"])
        self.assertFalse(catalog["identityIsolation"]["raidDefeatMutatesGuardian"])
        self.assertFalse(catalog["identityIsolation"]["guardianStateMutatesRaid"])
        self.assertFalse(catalog["identityIsolation"]["guildClosedInstanceAlias"])
        self.assertEqual(
            "engine_free_snapshot_only", catalog["authorityBoundary"]["plannerScope"]
        )
        self.assertIn("public_realm_dungeon", catalog["authorityBoundary"]["ownedDomain"])
        self.assertIn(
            "guild_closed_instance",
            catalog["authorityBoundary"]["excludedImplementations"],
        )

        dungeons = catalog["dungeons"]
        self.assertEqual(4, len(dungeons))
        raid_ids = []
        dungeon_ids = []
        for expected, dungeon in zip(EXPECTED_DUNGEONS, dungeons):
            dungeon_id, realm_id, raid_id, presentation_ref = expected
            self.assertEqual(dungeon_id, dungeon["id"])
            self.assertEqual(realm_id, dungeon["realmId"])
            self.assertFalse(dungeon["productionEligible"])
            self.assertEqual(
                [
                    f"{dungeon_id}_entrance_01",
                    f"{dungeon_id}_entrance_02",
                ],
                [entrance["id"] for entrance in dungeon["entrances"]],
            )
            self.assertEqual(2, len(dungeon["entrances"]))
            self.assertEqual(f"{dungeon_id}_portal", dungeon["portalId"])
            raid = dungeon["raidDragon"]
            self.assertEqual(raid_id, raid["id"])
            self.assertEqual(presentation_ref, raid["guardianPresentationRef"])
            self.assertNotEqual(raid["id"], raid["guardianPresentationRef"])
            self.assertFalse(raid["presentationApproved"])
            self.assertFalse(raid["productionEligible"])
            self.assertEqual("", raid["presentationBundleId"])
            raid_ids.append(raid["id"])
            dungeon_ids.append(dungeon["id"])

        self.assertEqual(4, len(set(raid_ids)))
        self.assertEqual(4, len(set(dungeon_ids)))
        for raid_id in raid_ids:
            self.assertNotIn(raid_id, GUARDIAN_DRAGON_IDS)
            self.assertNotIn(raid_id, GUILD_CLOSED_BOSS_IDS)
            self.assertFalse(raid_id.startswith("closed_raid_"))
            self.assertFalse(raid_id.startswith("dragon_"))
        for dungeon_id in dungeon_ids:
            self.assertFalse(dungeon_id.startswith("closed_raid_"))
        self.assertEqual(
            list(GUARDIAN_DRAGON_IDS),
            catalog["identityIsolation"]["guardianCatalogDragonIds"],
        )
        self.assertEqual(
            list(GUILD_CLOSED_BOSS_IDS),
            catalog["identityIsolation"]["guildClosedBossProfileIds"],
        )
        self.assertEqual(
            "closed_raid_", catalog["identityIsolation"]["guildClosedInstanceIdPrefix"]
        )


if __name__ == "__main__":
    unittest.main()
