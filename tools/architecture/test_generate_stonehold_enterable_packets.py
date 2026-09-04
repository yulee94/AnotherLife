import copy
import json
import unittest
from pathlib import Path

from tools.architecture.generate_stonehold_enterable_packets import (
    COVERAGE_PATH,
    CIVIC_LAYOUT_PATH,
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
        self.assertTrue(self.report["checks"]["fullResolutionVisualQa54of54"])
        self.assertEqual(len(self.report["fullResolutionVisualQa"]), 54)
        self.assertEqual(len(self.manifest["reviewArtifacts"]), 16)

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

    def test_every_packet_has_a_named_packet_specific_blueprint(self) -> None:
        signatures = set()
        for source in STRUCTURES:
            packet = packet_document(source, self.prop_ids)
            design = packet["architecturalDesign"]
            self.assertEqual(design["packetSlug"], source["slug"])
            self.assertTrue(design["layoutArchetype"])
            self.assertTrue(design["exteriorMasses"])
            self.assertTrue(design["roofVolumes"])
            signatures.add((design["layoutArchetype"], design["planSignature"]))
        self.assertEqual(len(signatures), len(STRUCTURES))

    def test_exterior_entries_are_explicit_separate_and_connected(self) -> None:
        for source in STRUCTURES:
            packet = packet_document(source, self.prop_ids)
            entries = [entry for floor in packet["floorPlans"] for entry in floor["exteriorEntrances"]]
            self.assertTrue(entries, packet["packetId"])
            for entry in entries:
                self.assertEqual(entry["from"], "outside")
                self.assertTrue(entry["separateObject"])
                self.assertIn(entry["to"], {room["id"] for floor in packet["floorPlans"] for room in floor["rooms"]})
                self.assertIn(entry["swing"], {"in", "out", "double_out", "sliding"})
                self.assertGreaterEqual(entry["clearOpeningMeters"][0], 1.2)

    def test_vertical_cores_are_placed_and_connect_every_level(self) -> None:
        for source in STRUCTURES:
            packet = packet_document(source, self.prop_ids)
            level_ids = [floor["id"] for floor in packet["floorPlans"]]
            cores = packet["verticalCirculation"]
            self.assertTrue(cores, packet["packetId"])
            connected = set()
            for core in cores:
                self.assertGreater(core["footprint"]["width"], 0)
                self.assertGreater(core["footprint"]["depth"], 0)
                self.assertTrue(core["landings"])
                connected.update(core["connectsLevels"])
                for floor in packet["floorPlans"]:
                    if floor["id"] in core["connectsLevels"]:
                        self.assertIn(core["id"], {item["coreId"] for item in floor["verticalCoreFootprints"]})
            self.assertEqual(connected, set(level_ids), packet["packetId"])

    def test_sections_are_tied_to_plan_cut_lines_and_level_geometry(self) -> None:
        for source in STRUCTURES:
            packet = packet_document(source, self.prop_ids)
            expected_levels = [floor["id"] for floor in packet["floorPlans"]]
            for section_name, section in packet["sections"].items():
                self.assertIn(section["cutLineId"], {line["id"] for floor in packet["floorPlans"] for line in floor["sectionCutLines"]})
                self.assertEqual([level["id"] for level in section["levels"]], expected_levels)
                self.assertTrue(section["roomSlices"], f"{packet['packetId']} {section_name}")
                self.assertTrue(section["slabs"])
                self.assertTrue(section["roofProfile"])
                self.assertTrue(section["foundation"])

    def test_furnishing_clearances_and_socket_positions_are_geometric(self) -> None:
        for source in STRUCTURES:
            packet = packet_document(source, self.prop_ids)
            for floor in packet["floorPlans"]:
                room_ids = {room["id"] for room in floor["rooms"]}
                self.assertEqual({item["roomId"] for item in floor["furnishingLayouts"]}, room_ids)
                self.assertTrue(all(item["footprints"] for item in floor["furnishingLayouts"]))
                self.assertTrue(all(item["protectedClearances"] for item in floor["furnishingLayouts"]))
                self.assertTrue(floor["clearanceZones"])
                self.assertTrue(floor["socketPlacements"])

    def test_fail_closed_on_geometric_continuity_regressions(self) -> None:
        packet = packet_document(STRUCTURES[0], self.prop_ids)
        packet["floorPlans"][1]["verticalCoreFootprints"] = []
        for floor in packet["floorPlans"]:
            floor["exteriorEntrances"] = []
        packet["sections"]["longitudinal"]["roomSlices"] = []
        packet["floorPlans"][0]["furnishingLayouts"][0]["footprints"] = []
        errors = packet_shape_errors(packet)
        self.assertIn("every level must place each connecting vertical core and landing", errors)
        self.assertIn("at least one outside-to-entry transition with a separate door is required", errors)
        self.assertIn("sections must contain plan-tied room, slab, foundation and roof geometry", errors)
        self.assertIn("every room needs room-specific furnishing footprints and protected clearances", errors)

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
        approved = json.loads(CIVIC_LAYOUT_PATH.read_text(encoding="utf-8"))
        generated = [
            {"floor": floor["id"], "between": door["between"], "orientation": door["orientation"], "x": door["x"], "z": door["z"], "width": door["clearOpeningMeters"][0]}
            for floor in packet["floorPlans"]
            for door in floor["doorOpenings"]
            if door["id"].startswith("approved_")
        ]
        self.assertEqual(generated, approved["doorOpenings"])

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

    def test_windows_lie_on_claimed_facade(self) -> None:
        from tools.architecture.stonehold_packet_geometry import FACADE_TOLERANCE
        for source in STRUCTURES:
            packet = packet_document(source, self.prop_ids)
            width = packet["envelopeMeters"]["width"]
            depth = packet["envelopeMeters"]["depth"]
            for floor in packet["floorPlans"]:
                for window in floor["windowOpenings"]:
                    side = window["side"]
                    if side == "north":
                        self.assertAlmostEqual(window["z"], depth, delta=FACADE_TOLERANCE, msg=packet["packetId"])
                    elif side == "south":
                        self.assertAlmostEqual(window["z"], 0.0, delta=FACADE_TOLERANCE, msg=packet["packetId"])
                    elif side == "east":
                        self.assertAlmostEqual(window["x"], width, delta=FACADE_TOLERANCE, msg=packet["packetId"])
                    elif side == "west":
                        self.assertAlmostEqual(window["x"], 0.0, delta=FACADE_TOLERANCE, msg=packet["packetId"])
                    else:
                        self.fail(f"{packet['packetId']} window {window['id']} has no facade side")

    def test_vertical_cores_are_contained_by_recorded_room(self) -> None:
        from tools.architecture.stonehold_packet_geometry import contains_rect
        for source in STRUCTURES:
            packet = packet_document(source, self.prop_ids)
            for floor in packet["floorPlans"]:
                rooms = {room["id"]: room for room in floor["rooms"]}
                self.assertTrue(floor["verticalCoreFootprints"], packet["packetId"])
                for core in floor["verticalCoreFootprints"]:
                    room = rooms[core["locatedInRoomId"]]
                    self.assertTrue(contains_rect(room, core["footprint"]), f"{packet['packetId']} {floor['id']} {core['coreId']}")

    def test_section_apertures_intersect_named_cut(self) -> None:
        from tools.architecture.stonehold_packet_geometry import CUT_APERTURE_TOLERANCE
        for source in STRUCTURES:
            packet = packet_document(source, self.prop_ids)
            for name, section in packet["sections"].items():
                self.assertTrue(section["apertureSlices"], f"{packet['packetId']} {name}")
                for aperture in section["apertureSlices"]:
                    self.assertLessEqual(
                        abs(aperture["orthogonalMeters"] - section["cutCoordinateMeters"]),
                        CUT_APERTURE_TOLERANCE,
                        f"{packet['packetId']} {name} {aperture['openingId']}",
                    )

    def test_forge_academy_mill_and_castle_have_profession_geometry(self) -> None:
        forge = packet_document(next(item for item in STRUCTURES if item["slug"] == "forge"), self.prop_ids)
        mill = packet_document(next(item for item in STRUCTURES if item["slug"] == "mill_wind_water"), self.prop_ids)
        academy = packet_document(next(item for item in STRUCTURES if item["slug"] == "academy"), self.prop_ids)
        castle = packet_document(next(item for item in STRUCTURES if item["slug"] == "castle_enterable"), self.prop_ids)
        forge_kinds = {item["kind"] for floor in forge["floorPlans"] for layout in floor["furnishingLayouts"] for item in layout["footprints"]} | {item["kind"] for item in forge["architecturalDesign"]["criticalFeatures"]}
        mill_kinds = {item["kind"] for floor in mill["floorPlans"] for layout in floor["furnishingLayouts"] for item in layout["footprints"]} | {item["kind"] for item in mill["architecturalDesign"]["criticalFeatures"]}
        self.assertTrue({"furnace", "anvil", "quench_trough", "hood_flue"} <= forge_kinds)
        self.assertTrue({"waterwheel", "race_channel", "mill_shaft", "gearing"} <= mill_kinds)
        lab = next(layout for floor in academy["floorPlans"] for layout in floor["furnishingLayouts"] if layout["roomId"] == "practice_lab")
        teaching = next(layout for floor in academy["floorPlans"] for layout in floor["furnishingLayouts"] if layout["roomId"] == "teaching_hall")
        self.assertGreaterEqual(len(lab["footprints"]), 3)
        self.assertTrue(any(item["kind"] in {"lecture_table", "chalkboard", "bench_row"} for item in teaching["footprints"]))
        for section in castle["sections"].values():
            self.assertTrue(any(void.get("openToSky") for void in section["voidSlices"]), section["cutLineId"])

    def test_fail_closed_when_window_leaves_its_facade(self) -> None:
        packet = packet_document(STRUCTURES[0], self.prop_ids)
        window = next(item for floor in packet["floorPlans"] for item in floor["windowOpenings"])
        window["x"] = packet["envelopeMeters"]["width"] / 2
        window["z"] = packet["envelopeMeters"]["depth"] / 2
        self.assertIn("every window must sit on its claimed facade", packet_shape_errors(packet))

    def test_fail_closed_when_core_escapes_recorded_room(self) -> None:
        packet = packet_document(STRUCTURES[0], self.prop_ids)
        core = packet["floorPlans"][0]["verticalCoreFootprints"][0]
        core["footprint"] = {**core["footprint"], "x": 0.0, "z": 0.0, "width": packet["envelopeMeters"]["width"], "depth": packet["envelopeMeters"]["depth"]}
        self.assertIn("every vertical core must be contained by its recorded room", packet_shape_errors(packet))

    def test_fail_closed_when_section_aperture_misses_cut(self) -> None:
        packet = packet_document(STRUCTURES[0], self.prop_ids)
        packet["sections"]["longitudinal"]["apertureSlices"][0]["orthogonalMeters"] = 999.0
        self.assertIn(
            "every section aperture slice must intersect the named cut, and castle/fortress cuts must keep the open courtyard",
            packet_shape_errors(packet),
        )

    def test_vertical_cores_share_shaft_xy_and_do_not_overlap(self) -> None:
        from tools.architecture.stonehold_packet_geometry import CORE_XY_TOLERANCE, overlaps_rect
        for source in STRUCTURES:
            packet = packet_document(source, self.prop_ids)
            by_id: dict[str, list] = {}
            for floor in packet["floorPlans"]:
                footprints = [item["footprint"] for item in floor["verticalCoreFootprints"]]
                for index, left in enumerate(footprints):
                    for right in footprints[index + 1:]:
                        self.assertFalse(overlaps_rect(left, right), f"{packet['packetId']} {floor['id']}")
                for item in floor["verticalCoreFootprints"]:
                    by_id.setdefault(item["coreId"], []).append(item["footprint"])
            for core_id, footprints in by_id.items():
                origin = footprints[0]
                for footprint in footprints[1:]:
                    self.assertAlmostEqual(footprint["x"], origin["x"], delta=CORE_XY_TOLERANCE, msg=f"{packet['packetId']} {core_id}")
                    self.assertAlmostEqual(footprint["z"], origin["z"], delta=CORE_XY_TOLERANCE, msg=f"{packet['packetId']} {core_id}")

    def test_courtyard_voids_stay_open_on_every_level(self) -> None:
        from tools.architecture.stonehold_packet_geometry import overlaps_rect
        for slug in ("castle_enterable", "fortress_enterable", "forge", "city_capital_kit", "religious_cultural_structure"):
            packet = packet_document(next(item for item in STRUCTURES if item["slug"] == slug), self.prop_ids)
            for floor in packet["floorPlans"]:
                courts = [void for void in floor["exteriorVoidsAndCourts"] if void.get("kind") == "open_to_sky"]
                self.assertTrue(courts, f"{packet['packetId']} {floor['id']}")
                self.assertFalse(any(overlaps_rect(room, courts[0]) for room in floor["rooms"]), f"{packet['packetId']} {floor['id']}")
            for section in packet["sections"].values():
                self.assertTrue(any(void.get("openToSky") for void in section["voidSlices"]), f"{packet['packetId']} {section['cutLineId']}")

    def test_entries_and_cut_windows_sit_on_served_room_walls(self) -> None:
        from tools.architecture.stonehold_packet_geometry import entry_on_destination_wall, window_on_served_room_span
        for source in STRUCTURES:
            packet = packet_document(source, self.prop_ids)
            width = packet["envelopeMeters"]["width"]
            depth = packet["envelopeMeters"]["depth"]
            for floor in packet["floorPlans"]:
                rooms = {room["id"]: room for room in floor["rooms"]}
                for entry in floor["exteriorEntrances"]:
                    self.assertTrue(entry_on_destination_wall(entry, rooms[entry["to"]], width, depth), f"{packet['packetId']} {entry['id']}")
                for window in floor["windowOpenings"]:
                    self.assertTrue(window_on_served_room_span(window, rooms[window["serves"]]), f"{packet['packetId']} {window['id']}")

    def test_fail_closed_when_core_xy_drifts(self) -> None:
        packet = packet_document(next(item for item in STRUCTURES if item["slug"] == "castle_enterable"), self.prop_ids)
        packet["floorPlans"][-1]["verticalCoreFootprints"][0]["footprint"]["x"] += 8.0
        self.assertIn("vertical cores must keep the same shaft XY across connected levels", packet_shape_errors(packet))

    def test_fail_closed_when_stair_overlaps_lift(self) -> None:
        packet = packet_document(next(item for item in STRUCTURES if item["slug"] == "castle_enterable"), self.prop_ids)
        ground = packet["floorPlans"][1]
        ground["verticalCoreFootprints"][1]["footprint"] = dict(ground["verticalCoreFootprints"][0]["footprint"])
        self.assertIn("stair and lift footprints must not overlap on the same floor", packet_shape_errors(packet))

    def test_fail_closed_when_courtyard_is_filled(self) -> None:
        packet = packet_document(next(item for item in STRUCTURES if item["slug"] == "castle_enterable"), self.prop_ids)
        packet["floorPlans"][-1]["exteriorVoidsAndCourts"] = []
        self.assertIn("open-to-sky courtyard voids must exist on every level and match both sections", packet_shape_errors(packet))

    def test_fail_closed_when_entry_misses_room_wall(self) -> None:
        packet = packet_document(next(item for item in STRUCTURES if item["slug"] == "mill_wind_water"), self.prop_ids)
        entry = packet["floorPlans"][1]["exteriorEntrances"][0]
        entry["x"] = packet["envelopeMeters"]["width"] / 2
        entry["z"] = packet["envelopeMeters"]["depth"] / 2
        self.assertIn("every exterior entrance must sit on the destination room wall", packet_shape_errors(packet))

    def test_fail_closed_when_cut_window_misses_room_span(self) -> None:
        packet = packet_document(next(item for item in STRUCTURES if item["slug"] == "academy"), self.prop_ids)
        window = next(item for floor in packet["floorPlans"] for item in floor["windowOpenings"] if item.get("cutAligned"))
        window["x"] = packet["envelopeMeters"]["width"] / 2
        window["z"] = packet["envelopeMeters"]["depth"] / 2
        window["side"] = "south"
        errors = packet_shape_errors(packet)
        self.assertTrue(
            "every window must sit on the served room wall span" in errors or "every window must sit on its claimed facade" in errors
        )

    def test_output_contains_no_3d_asset_files(self) -> None:
        prohibited = {".fbx", ".blend", ".obj", ".glb", ".gltf"}
        found = [path for path in OUTPUT_ROOT.rglob("*") if path.suffix.lower() in prohibited]
        self.assertEqual(found, [])


if __name__ == "__main__":
    unittest.main()
