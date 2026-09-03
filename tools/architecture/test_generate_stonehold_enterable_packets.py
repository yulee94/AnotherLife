import copy
import json
import unittest
from pathlib import Path

from tools.architecture.generate_stonehold_enterable_packets import (
    COVERAGE_PATH,
    FURNISHING_PRECEDENT_PATH,
    OUTPUT_ROOT,
    SHARED_SUPPORT_FAMILIES,
    STRUCTURES,
    build,
    packet_document,
    packet_shape_errors,
    validate,
    write_readme,
)


class StoneholdEnterablePacketTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.manifest = build()
        cls.report = validate(cls.manifest)
        write_readme(cls.manifest, cls.report)
        cls.coverage = json.loads(COVERAGE_PATH.read_text(encoding="utf-8"))
        cls.prop_ids = sorted(
            record["familyId"]
            for record in cls.coverage["families"]
            if record["packetId"] == "prop_stonehold_interior_decor_v001"
        )

    def test_taxonomy_partition_is_complete_and_unique(self) -> None:
        enterable = {
            record["familyId"]
            for record in self.coverage["families"]
            if record["packetId"] == "architecture_stonehold_enterable_structures_v001"
        }
        settlement = {
            record["familyId"]
            for record in self.coverage["families"]
            if record["packetId"] == "architecture_stonehold_settlement_silhouettes_v001"
        }
        packet_ids = {item["taxonomyId"] for item in STRUCTURES}
        actual = packet_ids | set(SHARED_SUPPORT_FAMILIES) | {
            "waf_architecture_building_wall",
            "waf_architecture_event_accordant_isle",
        }
        self.assertEqual(len(STRUCTURES), 27)
        self.assertEqual(len(packet_ids), 27)
        self.assertEqual(len(SHARED_SUPPORT_FAMILIES), 11)
        self.assertEqual(actual, enterable | settlement)
        self.assertEqual(len(actual), 40)

    def test_generated_packet_set_passes_every_gate(self) -> None:
        self.assertEqual(self.report["status"], "passed")
        self.assertTrue(all(self.report["checks"].values()))
        self.assertEqual(self.report["counts"]["enterablePackets"], 27)
        self.assertEqual(self.report["counts"]["interiorModuleFamilies"], 21)
        self.assertEqual(self.report["counts"]["propDecorFamilies"], 65)

    def test_owner_approved_pr_701_furnishing_precedent_is_bound(self) -> None:
        self.assertTrue(FURNISHING_PRECEDENT_PATH.is_file())
        self.assertTrue(self.report["checks"]["approvedFurnishingPrecedentBound"])
        for entry in self.manifest["packets"]:
            packet_path = OUTPUT_ROOT / entry["artifacts"][0]["locator"]
            packet = json.loads(packet_path.read_text(encoding="utf-8"))
            self.assertIn("PR #701", packet["approvedFurnishingPrecedent"]["approvalRecord"])

    def test_each_packet_has_every_physical_floor_and_required_views(self) -> None:
        for entry in self.manifest["packets"]:
            packet_path = OUTPUT_ROOT / entry["artifacts"][0]["locator"]
            packet = json.loads(packet_path.read_text(encoding="utf-8"))
            self.assertEqual(packet_shape_errors(packet), [], packet["packetId"])
            self.assertEqual(len(packet["floorPlans"]), len(packet["levels"]))
            self.assertTrue(all(level["rooms"] for level in packet["floorPlans"]))

    def test_town_hall_retains_approved_pr_664_room_coordinates(self) -> None:
        source = next(item for item in STRUCTURES if item["slug"] == "town_hall")
        packet = packet_document(source, self.prop_ids)
        ground = packet["floorPlans"][0]
        public_hall = next(room for room in ground["rooms"] if room["id"] == "public_hall")
        self.assertEqual(
            {key: public_hall[key] for key in ["x", "z", "width", "depth"]},
            {"x": 2.1, "z": 0.3, "width": 5.3, "depth": 5.0},
        )
        self.assertIn("coordinates retained exactly", ground["sourceAuthority"])

    def test_below_grade_levels_have_no_fake_windows(self) -> None:
        source = next(item for item in STRUCTURES if item["slug"] == "castle_enterable")
        packet = packet_document(source, self.prop_ids)
        undercroft = packet["floorPlans"][0]
        self.assertLess(undercroft["elevationMeters"], 0)
        self.assertEqual(undercroft["windowOpenings"], [])
        self.assertIn("No exterior windows below grade", undercroft["windowPolicy"])

    def test_streaming_portals_are_only_marked_on_physical_portal_levels(self) -> None:
        castle_source = next(item for item in STRUCTURES if item["slug"] == "castle_enterable")
        castle = packet_document(castle_source, self.prop_ids)
        self.assertEqual(
            [level["id"] for level in castle["floorPlans"] if level["portalBoundary"] == "physical_loading_cover_and_streaming_portal"],
            ["undercroft"],
        )
        mine_source = next(item for item in STRUCTURES if item["slug"] == "gold_mine")
        mine = packet_document(mine_source, self.prop_ids)
        self.assertEqual(
            [level["id"] for level in mine["floorPlans"] if level["portalBoundary"] == "physical_loading_cover_and_streaming_portal"],
            ["lower_staging", "ground"],
        )

    def test_fail_closed_when_a_floor_plan_is_missing(self) -> None:
        packet = packet_document(STRUCTURES[0], self.prop_ids)
        packet["floorPlans"].pop()
        self.assertIn(
            "every physical level must have exactly one floor plan",
            packet_shape_errors(packet),
        )

    def test_fail_closed_when_wall_or_door_policy_is_weakened(self) -> None:
        packet = packet_document(STRUCTURES[0], self.prop_ids)
        packet["bindingPolicies"] = copy.deepcopy(packet["bindingPolicies"])
        packet["bindingPolicies"]["perimeterWall"] = "wall may contain rooms"
        packet["bindingPolicies"]["doorsAndGates"] = "door fused to wall"
        self.assertIn(
            "perimeter-wall exception and separate aperture-object policies are required",
            packet_shape_errors(packet),
        )

    def test_fail_closed_when_3d_is_authorized(self) -> None:
        packet = packet_document(STRUCTURES[0], self.prop_ids)
        packet["productionAuthorization"] = copy.deepcopy(packet["productionAuthorization"])
        packet["productionAuthorization"]["meshy"] = True
        self.assertIn(
            "3D production must remain unauthorized with zero submitted jobs",
            packet_shape_errors(packet),
        )

    def test_fail_closed_when_furnishing_precedent_is_removed(self) -> None:
        packet = packet_document(STRUCTURES[0], self.prop_ids)
        packet.pop("approvedFurnishingPrecedent")
        self.assertIn(
            "owner-approved PR #701 furnishing precedent must be bound explicitly",
            packet_shape_errors(packet),
        )

    def test_output_contains_no_3d_asset_files(self) -> None:
        prohibited = {".fbx", ".blend", ".obj", ".glb", ".gltf"}
        found = [path for path in OUTPUT_ROOT.rglob("*") if path.suffix.lower() in prohibited]
        self.assertEqual(found, [])


if __name__ == "__main__":
    unittest.main()
