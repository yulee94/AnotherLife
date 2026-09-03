#!/usr/bin/env python3
"""Generate and validate the Stonehold enterable-architecture 2D packet set."""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import math
import shutil
import textwrap
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont

REPO_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_ROOT = REPO_ROOT / "unity/Docs/Architecture/StoneholdEnterableStructurePacketsV001"
PACKET_ROOT = OUTPUT_ROOT / "packets"
REVIEW_ROOT = OUTPUT_ROOT / "review"
COVERAGE_PATH = REPO_ROOT / "unity/Docs/AssetLibrary/StoneholdConceptPacketsV001/stonehold_concept_packet_coverage_v001.json"
CIVIC_LAYOUT_PATH = REPO_ROOT / "unity/Docs/Architecture/WorldSpaceEnterableV001/CivicHall/2d/plans/shared_civic_hall_layout_v001.json"
PACKET_SET_ID = "stonehold_enterable_structure_packets_v001"
VERSION = "v001"
SHEET_SIZE = (2400, 1500)
INTERIOR_SIZE = (3000, 1900)

BG = "#111820"
PANEL = "#19242c"
PANEL_ALT = "#202e38"
LINE = "#91a1aa"
TEXT = "#e8edf0"
MUTED = "#a9b6bd"
ACCENT = "#d59736"
AMBER = "#efb24b"
ROUTE = "#67c9c3"
PORTAL = "#b77ae8"
DANGER = "#e16b63"
STONE = "#353c40"
IRON = "#242a2d"
TIMBER = "#3e3028"
BRONZE = "#755033"

SHELL = "waf_interior_shell_wall_floor_ceiling"
DOOR = "waf_interior_door_window_threshold"
ENTRY = "waf_interior_entry_vestibule"
CORRIDOR = "waf_interior_corridor_junction"
STAIR = "waf_interior_stair_landing"
CUT = "waf_interior_cutaway_occlusion_set"
STORE = "waf_interior_storage_pantry_cellar"
UTILITY = "waf_interior_utility_service_room"

SHARED_SUPPORT_FAMILIES = [
    "waf_interactable_door_hatch",
    "waf_interactable_gate_teleport_control",
    "waf_traversal_gate_breakable_war",
    "waf_traversal_gate_local_doorway",
    "waf_traversal_gate_main_teleport",
    "waf_traversal_ladder_climb",
    "waf_traversal_platform_walkway",
    "waf_traversal_ramp_slope",
    "waf_traversal_stair_step",
    "waf_traversal_teleport_pad_portal_anchor",
    "waf_traversal_wall_fortification",
]

PROP_PACK_RULES = {
    "common": ["waf_prop_seating_", "waf_prop_surface_", "waf_prop_textile_", "waf_prop_clutter_personal_items", "waf_prop_clutter_pottery_bottles"],
    "civic": ["waf_banner_civic_pennant", "waf_banner_realm_standard", "waf_sign_", "waf_prop_royal_council_lectern"],
    "archive": ["waf_prop_clutter_books_scrolls_papers", "waf_prop_storage_shelf_bookcase", "waf_sign_notice_contract_board"],
    "military": ["waf_prop_military_", "waf_banner_war_objective"],
    "industrial": ["waf_prop_forge_", "waf_interactable_service_station"],
    "market": ["waf_prop_market_", "waf_interactable_container_loot"],
    "kitchen": ["waf_prop_kitchen_", "waf_prop_clutter_food_goods"],
    "lodging": ["waf_prop_sleep_", "waf_prop_clutter_textile_bedding"],
    "storage": ["waf_prop_storage_", "waf_interactable_container_loot"],
    "royal": ["waf_prop_royal_"],
    "religious": ["waf_prop_religious_", "waf_prop_cultural_instrument_artifact"],
    "mine": ["waf_prop_utility_rope_chain", "waf_prop_utility_scaffold_ladder", "waf_prop_utility_tools_workset", "waf_prop_clutter_debris_rubble"],
    "farm": ["waf_prop_utility_cart_wagon", "waf_prop_utility_tools_workset", "waf_prop_utility_bucket_tub_washbasin", "waf_prop_utility_firewood_fuel"],
    "guild": ["waf_prop_guild_", "waf_banner_guild"],
    "utility": ["waf_prop_lighting_", "waf_prop_utility_", "waf_interactable_lever_switch", "waf_interactable_lift_platform", "waf_interactable_quest_objective", "waf_interactable_seat_use"],
}

BINDING_POLICIES = {
    "gridMeters": 0.5,
    "placementCellMeters": 2.0,
    "structuralBayMeters": 4.0,
    "verticalTierMeters": 1.0,
    "doorsAndGates": "Every door leaf, frame, shutter, glass group, local gate and main gate is a separate object from shells and perimeter walls.",
    "mainGate": "An intact main gate stays physically impassable. Future interaction teleports atomically between paired anchors; this packet implements no teleport.",
    "hostileGateBreak": "Reference t_c8ea885d only. This packet implements no break logic and authorizes no destructible 3D geometry.",
    "perimeterWall": "Main castle, fortress, fort and city perimeter walls stay non-enterable and impassable; only defendable walltops and designated routes are traversable.",
    "interiorScale": "Small interiors are seamless. Large combat interiors use separate asynchronously loaded scenes behind physical portals.",
    "runtimeVfx": "Particles, smoke, sparks, auras, volumetrics and magic remain runtime-separate from clean geometry.",
    "staticAssembly": "Combine opaque structural and finish modules per room or exterior visibility cell and atlas; never retain one renderer or collider per source tile at runtime.",
}

SHARED_MODULE_REGISTER = {
    "structural": ["foundation_3m", "floor_3m", "ceiling_3m", "wall_solid_3m", "wall_opening_3m", "inside_corner", "outside_corner", "pillar", "beam_lintel", "roof_slope", "roof_ridge", "roof_hip_valley", "roof_gable_end", "roof_fascia"],
    "aperturesSeparate": ["door_leaf_1_2x2_4", "public_door_leaf_2_5x3_0", "window_frame_1_2x1_5", "glass_group", "shutter_pair", "local_gate_4m", "major_gate_8m", "main_gate_face", "gate_control"],
    "traversal": ["stair_1_5m", "landing_1_5m", "ramp_1_to_12", "lift_platform", "ladder_fixed", "walkway_2m", "walltop_route_2m", "bridge_4m_bay"],
    "technical": ["streaming_portal", "loading_cover", "occlusion_cell", "navmesh_link", "unload_boundary", "lighting_socket", "interaction_socket", "quest_socket", "audio_socket", "vfx_anchor"],
}

PROVENANCE = [
    "DESIGN.md",
    "unity/Assets/AL/Art/Designs/FourRealmArchitecture.md",
    "unity/Docs/Architecture/WorldSpaceEnterableV001/README.md",
    "unity/Docs/Architecture/WorldSpaceEnterableV001/CivicHall/civic_hall_2d_manifest_v001.json",
    "unity/Docs/Architecture/WorldSpaceEnterableV001/FortGatehouse/fort_gatehouse_2d_manifest_v001.json",
    "unity/Docs/AssetLibrary/StoneholdConceptPacketsV001/stonehold_concept_packet_coverage_v001.json",
    "unity/Docs/AssetLibrary/StoneholdConceptPacketsV001/architecture_stonehold_enterable_structures_v001.md",
    "unity/Docs/AssetLibrary/StoneholdConceptPacketsV001/architecture_stonehold_exterior_interior_floorplan_v001.md",
    "unity/Docs/AssetLibrary/StoneholdConceptPacketsV001/architecture_stonehold_settlement_silhouettes_v001.md",
    "unity/Docs/AssetLibrary/StoneholdConceptPacketsV001/prop_stonehold_interior_decor_v001.md",
]

AVOID = [
    "Solid decorative shell or facade-only substitute.",
    "Enterable main perimeter walls.",
    "Gate, door, window glass or shutter fused into a wall/building mesh.",
    "Unmarked inaccessible room or painted-on aperture.",
    "Dwarf pastiche or copied BDO / Infinity Kingdom architecture.",
    "Crownlands vertical civic silhouette or Eldergrove root grammar.",
    "Glowing cracks, smoke or VFX used as required silhouette.",
    "Slagfall Quarry treated as architecture visual authorization.",
    "Invented religious, royal, heraldic or textual meaning.",
    "One renderer, material or collider per modular tile.",
    "Unique 4K runtime textures for common structures.",
]


def lv(level_id: str, elevation: float, clear: float, *rooms: str) -> dict[str, Any]:
    return {"id": level_id, "elevationMeters": elevation, "clearHeightMeters": clear, "rooms": list(rooms)}


def spec(slug: str, taxonomy: str, title: str, category: str, envelope: tuple[float, float, float], roof: str, massing: str, levels: list[dict[str, Any]], streaming: str, access: str, modules: list[str], packs: list[str], gate: str) -> dict[str, Any]:
    large = category in {"capital_kit", "defensive_complex", "castle"}
    return {
        "packetId": f"stonehold_{slug}_2d_handoff_v001",
        "taxonomyId": taxonomy,
        "slug": slug,
        "title": title,
        "category": category,
        "status": "pending_owner_review",
        "envelopeMeters": {"width": envelope[0], "depth": envelope[1], "height": envelope[2]},
        "massingRecommendation": massing,
        "roofRecommendation": roof,
        "materialRecommendation": "Stepped basalt base; dark iron ribs; soot-aged dark timber; sparse bronze repairs; localized forge-amber practical light only.",
        "levels": levels,
        "streamingPolicy": streaming,
        "accessibilityRoute": access,
        "clearancesMeters": {
            "publicEntrance": [2.5, 3.0],
            "interiorDoor": [1.2, 2.4],
            "minimumFurnitureAisle": 1.2,
            "primaryCirculationWidth": 1.8 if large else 1.5,
            "combatClearDiameter": 6.0 if large else 4.0,
            "cameraBackoff": 3.5 if large else 2.5,
            "stairWidth": 1.5,
            "landingDepth": 1.5,
            "rampMaximumSlope": "1:12",
        },
        "interiorModuleIds": list(dict.fromkeys([SHELL, DOOR, ENTRY, CORRIDOR, STAIR, CUT, *modules])),
        "propPacks": packs,
        "socketSets": ["lighting", "interaction", "quest", "audio", "vfx_anchor_runtime_separate", "portal_streaming"],
        "variants": ["clean", "used", "damaged"],
        "familySpecificGate": gate,
    }


STRUCTURES = [
    spec("city_capital_kit", "waf_architecture_city_capital_kit", "Stonehold Capital Enterable Block Kit", "capital_kit", (30, 24, 11.2), "Linked low hips and clipped gables around a stepped civic court.", "Four individually enterable compressed-mass shells around a 10 m service court; never a fused city-block shell.", [lv("undercroft", -3.2, 2.8, "block_a_store", "block_b_cellar", "shared_service_tunnel", "loading_hold"), lv("street", 0, 3.0, "block_a_public_hall", "block_b_shop", "block_c_service_hall", "block_d_guard_post", "covered_arcade", "service_court"), lv("upper", 3.4, 2.8, "block_a_records", "block_b_living", "block_c_workroom", "block_d_barracks", "cross_gallery")], "Seamless per building; district/chunk boundary remains outside this packet.", "Step-free street entries to every shell; 1:12 court ramp; lift-platform sockets to upper public rooms.", [STORE, UTILITY, "waf_interior_courtyard_balcony", "waf_interior_great_council_hall", "waf_interior_market_shop", "waf_interior_bedroom_living", "waf_interior_barracks_room"], ["common", "civic", "market", "storage", "military", "lodging", "utility"], "Approve the four-shell block composition and court-to-arcade hierarchy."),
    spec("settlement_village_kit", "waf_architecture_settlement_village_kit", "Stonehold Village Enterable Cluster Kit", "settlement_kit", (24, 20, 7.2), "Three low clipped gables with shared iron-ridge language.", "Three detached enterable shells—dwelling, service shop and communal hall—around a work yard.", [lv("ground", 0, 2.9, "dwelling_living", "service_shop", "communal_hall", "shared_store", "work_yard", "covered_walk"), lv("lofts", 3.2, 2.5, "dwelling_sleeping", "shop_stock_loft", "hall_meeting_loft", "service_landing")], "Seamless small interiors.", "Step-free ground entries and yard; lofts use stairs with reserved lift-platform sockets.", [STORE, UTILITY, "waf_interior_bedroom_living", "waf_interior_market_shop", "waf_interior_kitchen_dining", "waf_interior_courtyard_balcony"], ["common", "market", "lodging", "kitchen", "storage", "farm", "utility"], "Approve the three-shell village mix and shared-yard density."),
    spec("dwelling", "waf_architecture_dwelling", "Stonehold Dwelling", "residential", (8, 7, 6.8), "Low clipped gable with one offset stone chimney.", "Broad two-bay house on a stepped basalt plinth with recessed threshold and rear service stoop.", [lv("ground", 0, 2.8, "entry", "living_hearth", "kitchen", "pantry", "wash_service"), lv("upper", 3.2, 2.6, "family_bedroom", "secondary_bedroom", "landing_storage")], "Seamless small interior.", "Step-free front entry; compact rear ramp in plinth; upper floor is a stair-only private route.", [STORE, UTILITY, "waf_interior_bedroom_living", "waf_interior_kitchen_dining"], ["common", "lodging", "kitchen", "storage", "utility"], "Approve a two-bedroom common dwelling rather than a one-room cottage."),
    spec("ruin_structure", "waf_architecture_ruin_structure", "Stonehold Enterable Ruin", "ruin", (12, 9, 6.0), "Partial collapsed clipped gable with retained iron-ridge fragment.", "Recognizable former service hall with one safe traversable bay and collapsed non-route bays.", [lv("cellar", -3.0, 2.6, "sealed_store", "safe_excavated_cell", "collapse_buffer"), lv("ground", 0, 3.0, "open_hall", "surviving_side_room", "safe_route", "blocked_collapse_zone")], "Seamless small interior.", "Graded breach route to ground hall; cellar gets a ramp shaft where quest-critical.", [STORE, "waf_interior_prison_dungeon"], ["common", "storage", "mine", "utility"], "Approve the former service-hall identity and retained safe cellar."),
    spec("well_fountain_cistern", "waf_architecture_well_fountain_cistern", "Stonehold Enterable Cistern Pavilion", "civic_service", (9, 9, 7.0), "Low octagonal iron-ribbed canopy over a square basalt headhouse.", "Compact public pump pavilion above a traversable maintenance cistern; fountain remains a separate surface prop.", [lv("cistern", -4.0, 3.2, "water_chamber", "maintenance_walk", "valve_room", "dry_service_bay"), lv("surface", 0, 3.0, "pump_hall", "public_draw_zone", "keeper_store", "ramp_landing")], "Seamless small interior.", "1:12 switchback maintenance ramp; public draw zone step-free; guarded water edge.", [STORE, UTILITY, "waf_interior_courtyard_balcony"], ["common", "storage", "utility", "civic"], "Approve the enterable cistern headhouse; fountain/well dressing stays separate."),
    spec("academy", "waf_architecture_building_academy", "Stonehold Academy", "civic", (16, 12, 8.2), "Paired clipped gables over a central archive ridge.", "Two broad teaching wings braced by a lower basalt archive spine.", [lv("ground", 0, 3.2, "entry_vestibule", "teaching_hall", "practice_lab", "tool_store", "accessible_service", "service_core"), lv("upper", 3.5, 2.9, "library_archive", "study_gallery", "instructor_office", "records_room", "landing")], "Seamless small interior.", "Step-free main entry and ground teaching suite; lift-platform core to archive level.", [STORE, UTILITY, "waf_interior_library_archive", "waf_interior_great_council_hall", "waf_interior_guild_hall"], ["common", "civic", "archive", "guild", "storage", "utility"], "Approve a practical trade academy, not a magical-university silhouette."),
    spec("barracks", "waf_architecture_building_barracks", "Stonehold Barracks", "military", (18, 11, 7.8), "Long low iron-ribbed gable with two vent crowns.", "Defensive rectangular lodge with thick end buttresses and recessed muster porch.", [lv("ground", 0, 3.1, "muster_hall", "armory", "gear_issue", "sergeant_room", "wash_service", "rear_ready_exit"), lv("upper", 3.4, 2.7, "bunk_room_a", "bunk_room_b", "officer_room", "landing_lockers", "night_watch_gallery")], "Seamless small interior.", "Step-free muster/armory route; lift-platform reserve to upper bunks; two egress routes.", [STORE, UTILITY, "waf_interior_barracks_room", "waf_interior_bedroom_living"], ["common", "military", "lodging", "storage", "utility"], "Approve the two-company bunk split and rear ready exit."),
    spec("embassy", "waf_architecture_building_embassy", "Stonehold Embassy", "civic", (15, 11, 8.5), "Formal clipped hip with low iron lantern monitor.", "Symmetrical public front on a stepped plinth; service mass is offset and lower.", [lv("ground", 0, 3.2, "security_vestibule", "public_reception", "petition_room", "records", "service_pantry", "secure_stair"), lv("upper", 3.5, 2.9, "diplomatic_chamber", "guest_suite", "private_office", "archive", "staff_landing")], "Seamless small interior.", "Step-free reception/petition rooms; enclosed lift-platform reserve to upper diplomatic room.", [STORE, UTILITY, "waf_interior_great_council_hall", "waf_interior_bedroom_living", "waf_interior_library_archive", "waf_interior_kitchen_dining"], ["common", "civic", "archive", "lodging", "kitchen", "storage", "utility"], "Approve restrained diplomatic symmetry without Crownlands verticality."),
    spec("farm", "waf_architecture_building_farm", "Stonehold Farmstead Service Hall", "service", (14, 10, 7.0), "Low clipped gable with ventilated hay ridge.", "Enterable stone service barn joined to an open work yard; field plots remain exterior systems.", [lv("ground", 0, 3.2, "processing_floor", "tool_issue", "feed_store", "cart_bay", "wash_service", "yard_threshold"), lv("loft", 3.5, 2.6, "drying_loft", "seasonal_store", "staff_rest", "landing")], "Seamless small interior.", "Step-free cart/staff entries; wide work aisle; platform lift for goods to loft.", [STORE, UTILITY], ["common", "farm", "storage", "utility"], "Approve the barn-service hall; fields are outside interior floor-plan scope."),
    spec("forge", "waf_architecture_building_forge", "Stonehold Forge", "industrial", (13, 10, 7.4), "Sawtooth iron vent roof over a lower clipped-gable customer bay.", "Buttressed hot-work hall with chimney mass, side quench court and separated public counter.", [lv("ground", 0, 3.6, "public_counter", "forge_workfloor", "hearth_bay", "quench_bay", "material_store", "safe_service_route"), lv("mezzanine", 3.9, 2.5, "pattern_archive", "tool_gallery", "foreman_desk", "landing")], "Seamless small interior.", "Step-free public/work entries; heat-safe 1.8 m route; goods-hoist and lift sockets.", [STORE, UTILITY, "waf_interior_forge_workshop", "waf_interior_library_archive", "waf_interior_courtyard_balcony"], ["common", "industrial", "storage", "utility", "archive"], "Approve the side quench court and sawtooth vent roof."),
    spec("gold_mine", "waf_architecture_building_gold_mine", "Stonehold Gold-Mine Headhouse", "industrial", (16, 12, 11.0), "Stepped iron headframe roof with low enclosed assay wing.", "Enterable assay/service headhouse framing a physical mine portal; never a blocked decorative tunnel.", [lv("lower_staging", -3.2, 3.0, "ore_receiving", "secure_hold", "pump_service", "portal_loading_cover"), lv("ground", 0, 3.3, "muster", "tool_issue", "assay_room", "ore_sort", "warden_office", "mine_portal_airlock"), lv("upper", 3.6, 2.7, "winch_gallery", "records", "watch_platform", "landing")], "Hybrid: headhouse seamless; mine beyond portal is a separately loaded unrestricted public interior.", "Step-free ground services; industrial lift to lower staging and upper winch gallery.", [STORE, UTILITY, "waf_interior_mine_cave_room", "waf_interior_library_archive"], ["common", "mine", "industrial", "storage", "military", "utility", "archive"], "Approve the headhouse-to-streamed-mine portal sequence and secure assay wing."),
    spec("lumber_mill", "waf_architecture_building_lumber_mill", "Stonehold Lumber Mill", "industrial", (18, 12, 7.2), "Long clipped gable with open iron-ribbed saw canopy.", "Broad processing hall faces a log yard; enclosed service rooms remain enterable.", [lv("ground", 0, 3.5, "saw_floor", "timber_sort", "tool_room", "dry_store", "crew_room", "yard_loading_threshold"), lv("loft", 3.8, 2.4, "blade_store", "pattern_rack", "foreman_gallery", "landing")], "Seamless small interior.", "Step-free loading route and crew entry; overhead goods hoist; protected saw-zone walkway.", [STORE, UTILITY, "waf_interior_forge_workshop"], ["common", "farm", "industrial", "storage", "utility"], "Approve the open saw canopy attached to the enclosed processing hall."),
    spec("market", "waf_architecture_building_market", "Stonehold Covered Market", "civic_service", (20, 14, 7.2), "Segmented low canopies around a central smoke-free lantern slot.", "Enterable shop cells ring a covered public trading hall and rear loading lane.", [lv("ground", 0, 3.2, "central_market_hall", "stall_row_a", "stall_row_b", "service_counter", "rear_loading", "public_rest_bay"), lv("upper", 3.5, 2.6, "merchant_offices", "stock_gallery", "records", "staff_landing")], "Seamless small interior.", "Step-free public loop with 1.8 m pinch minimum; rear goods ramp; lift to stock gallery.", [STORE, UTILITY, "waf_interior_market_shop", "waf_interior_courtyard_balcony", "waf_interior_library_archive"], ["common", "market", "civic", "storage", "utility", "archive"], "Approve a covered hall with enterable perimeter shop cells, not open stalls only."),
    spec("quarry", "waf_architecture_building_quarry", "Stonehold Quarry Works", "industrial", (18, 13, 10.5), "Low shed roofs stepped with the cut; one iron crusher monitor.", "Enterable cutting/service hall at quarry edge; excavation is external and not visual authority here.", [lv("lower_service", -3.0, 2.8, "machine_service", "secure_tool_hold", "drainage_room", "loading_cover"), lv("ground", 0, 3.4, "stone_cutting", "sorting_floor", "tool_issue", "crew_room", "yard_threshold", "overlook_route"), lv("upper", 3.7, 2.5, "foreman_office", "drawing_archive", "watch_gallery", "landing")], "Hybrid: building seamless; quarry/cave portal streams only when combat scale requires.", "Step-free yard/cutting hall; industrial lift to lower service; guarded accessible overlook.", [STORE, UTILITY, "waf_interior_mine_cave_room", "waf_interior_library_archive"], ["common", "mine", "industrial", "storage", "utility", "archive"], "Approve stepped quarry-edge works without deriving visuals from Slagfall Quarry."),
    spec("stable", "waf_architecture_building_stable", "Stonehold Stable", "service", (17, 11, 7.0), "Vented clipped gable with low rear feed lean-to.", "Broad central-aisle stable with enterable tack/feed/service rooms and fenced exercise yard.", [lv("ground", 0, 3.3, "central_aisle", "stall_row_a", "stall_row_b", "tack_room", "feed_store", "wash_bay"), lv("loft", 3.6, 2.5, "hay_loft", "staff_rest", "gear_store", "landing")], "Seamless small interior.", "Step-free through-aisle; 2.5 m shared doors; goods lift for feed loft.", [STORE, UTILITY], ["common", "farm", "storage", "lodging", "utility"], "Approve the through-aisle plan and external exercise-yard connection."),
    spec("storehouse", "waf_architecture_building_storehouse", "Stonehold Storehouse", "service", (15, 11, 8.0), "Low iron-ribbed gable with protected loading hood.", "Thick buttressed shell split into secure cells around a clear loading spine.", [lv("ground", 0, 3.4, "loading_spine", "bulk_store_a", "bulk_store_b", "secure_cage", "clerk_station", "rear_dispatch"), lv("upper", 3.7, 2.7, "dry_store", "records", "guard_gallery", "goods_landing")], "Seamless small interior.", "Step-free loading spine; 1:12 dock ramp; goods lift to upper store.", [STORE, UTILITY, "waf_interior_library_archive"], ["common", "storage", "military", "utility", "archive"], "Approve the split secure-cell plan and upper dry store."),
    spec("town_hall", "waf_architecture_building_town_hall", "Stonehold Civic Hall", "civic", (9.5, 8.5, 6.8), "Low clipped hip with restrained iron ridge and one stepped civic buttress crown.", "Stonehold cladding wraps the owner-approved shared 9.5 x 8.5 m civic layout; interior dimensions remain unchanged.", [lv("ground", 0, 2.8, "public_hall", "records_room", "stores", "steward_office", "stair_and_service", "council_workroom"), lv("upper", 3.2, 2.7, "open_gallery", "upper_archive", "landing_and_staff", "council_chamber", "rear_archive", "upper_service")], "Seamless small interior.", "Approved plan retained; reversible 1:12 plinth ramp and lift reserve cannot shrink public aisle.", [STORE, UTILITY, "waf_interior_great_council_hall", "waf_interior_library_archive", "waf_interior_courtyard_balcony"], ["common", "civic", "archive", "storage", "guild", "utility"], "Approve Stonehold exterior cladding while preserving PR #664 interior geometry."),
    spec("watchtower", "waf_architecture_building_watchtower", "Stonehold Watchtower", "defensive", (8, 8, 12.0), "Low crenellated observation crown with iron weather hood.", "Square tapered basalt tower with clipped corners, external buttresses and independent entry door.", [lv("ground", 0, 3.0, "guard_entry", "secure_store", "alarm_station", "stair_core"), lv("mid", 3.3, 2.8, "rest_room", "signal_store", "landing", "arrow_gallery"), lv("crown", 6.4, 3.0, "observation_deck", "covered_signal_bay", "wallwalk_threshold", "safe_parapet_route")], "Seamless small interior.", "Ground accessible; lift-platform reserve serves crown where quest-critical; stair remains defensive route.", [STORE, UTILITY, "waf_interior_barracks_room", "waf_interior_courtyard_balcony"], ["common", "military", "storage", "utility"], "Approve the lift-platform reserve inside the compact tower core."),
    spec("workshop", "waf_architecture_building_workshop", "Stonehold World-Space Workshop", "industrial", (12, 9, 7.0), "Asymmetric clipped gable with removable anvil-crown accent.", "Enterable world-space service workshop derived from, but not identical to, kingdom Workshop language.", [lv("ground", 0, 3.4, "public_service_counter", "workfloor", "tool_wall", "material_store", "rear_loading", "safe_aisle"), lv("mezzanine", 3.7, 2.4, "pattern_store", "foreman_desk", "parts_gallery", "landing")], "Seamless small interior.", "Step-free public/work entries; goods-lift socket to parts gallery.", [STORE, UTILITY, "waf_interior_forge_workshop"], ["common", "industrial", "storage", "utility", "archive"], "Approve the world-space variant as distinct from the kingdom Workshop footprint."),
    spec("castle_enterable", "waf_architecture_castle_enterable", "Stonehold Castle Keep", "castle", (34, 28, 17.0), "Layered stepped keep roofs with a low central iron crown; no needle spires.", "Enterable keep and courtyard buildings sit inside a separate non-enterable perimeter wall; four clipped-corner towers frame broad terraces.", [lv("undercroft", -3.5, 3.0, "service_cellar", "secure_archive", "detention_cells", "guard_room", "loading_tunnel", "streaming_airlock"), lv("ground", 0, 3.5, "public_entry_hall", "great_council_hall", "service_kitchen", "guard_muster", "courtyard_gallery", "accessible_core"), lv("upper", 3.8, 3.2, "command_chamber", "audience_hall", "library_archive", "guest_suite", "staff_service", "upper_gallery"), lv("roof_and_towers", 7.3, 3.0, "tower_room_a", "tower_room_b", "signal_room", "roof_access", "defensive_gallery", "walltop_gate_threshold")], "Separate streamed large-combat interior behind physical keep portal; courtyard exterior stays in world scene.", "Step-free public route; lift core reaches public levels and undercroft; defensive stairs are independent.", [STORE, UTILITY, "waf_interior_great_council_hall", "waf_interior_throne_royal_hall", "waf_interior_prison_dungeon", "waf_interior_kitchen_dining", "waf_interior_library_archive", "waf_interior_bedroom_living", "waf_interior_barracks_room", "waf_interior_courtyard_balcony"], ["common", "civic", "military", "royal", "archive", "lodging", "kitchen", "storage", "utility"], "Approve the civic-command keep program; throne symbolism remains narrative-gated."),
    spec("fortress_enterable", "waf_architecture_fortress_enterable", "Stonehold Fortress Interior Complex", "defensive_complex", (38, 30, 15.0), "Low stepped command roofs and tower weather hoods behind separate battlements.", "Enterable command, barracks and service buildings sit inside a separate one-gate non-enterable perimeter; capture court stays open.", [lv("undercroft", -3.4, 3.0, "supply_hold", "detention", "sally_service", "repair_store", "streaming_airlock"), lv("ground", 0, 3.5, "command_hall", "muster", "armory", "infirmary_service", "capture_court_gallery", "accessible_core"), lv("upper", 3.8, 3.0, "officer_room", "barracks", "war_map_room", "signal_gallery", "wallwalk_access"), lv("tower_level", 7.1, 2.8, "tower_guard_a", "tower_guard_b", "siege_store", "roof_route", "parapet_threshold")], "Separate streamed large-combat interior; exterior capture court and perimeter stay in world scene.", "Step-free command/capture route; protected lift core; independent combat stair loops and two egress portals.", [STORE, UTILITY, "waf_interior_barracks_room", "waf_interior_prison_dungeon", "waf_interior_great_council_hall", "waf_interior_courtyard_balcony"], ["common", "military", "civic", "storage", "utility"], "Approve the three-building interior complex around the central capture court."),
    spec("guardpost_watch", "waf_architecture_guardpost_watch", "Stonehold Guardpost", "defensive", (10, 8, 6.8), "Low clipped gable with one protected signal hood.", "Compact roadside post with defensible public inspection room and rear ready exit.", [lv("ground", 0, 3.0, "inspection_room", "guard_muster", "secure_store", "public_wait", "rear_ready_exit"), lv("upper", 3.3, 2.5, "rest_bunks", "watch_gallery", "signal_store", "landing")], "Seamless small interior.", "Step-free inspection route; upper duty space has stair and lift-platform reserve.", [STORE, "waf_interior_barracks_room", "waf_interior_bedroom_living"], ["common", "military", "lodging", "storage", "utility"], "Approve a public inspection room rather than an exterior-only booth."),
    spec("inn_tavern", "waf_architecture_inn_tavern", "Stonehold Inn and Tavern", "hospitality", (17, 12, 10.0), "Broad clipped gable with offset kitchen chimney and recessed sign beam.", "Two-story public house on stepped plinth; rear service yard and optional detached stable threshold.", [lv("cellar", -3.0, 2.7, "drink_store", "food_store", "cold_room", "service_stair", "loading_cover"), lv("ground", 0, 3.2, "entry", "common_room", "service_bar", "kitchen", "accessible_guest_room", "rear_service"), lv("upper", 3.5, 2.7, "guest_room_a", "guest_room_b", "guest_room_c", "wash_room", "landing_lounge")], "Seamless small interior.", "Step-free common room and ground guest room; lift to upper rooms; cellar served by goods lift.", [STORE, UTILITY, "waf_interior_kitchen_dining", "waf_interior_bedroom_living", "waf_interior_courtyard_balcony"], ["common", "lodging", "kitchen", "market", "storage", "utility"], "Approve the ground-floor accessible guest room and optional stable connection."),
    spec("mill_wind_water", "waf_architecture_mill_wind_water", "Stonehold Water Mill", "industrial", (15, 10, 11.0), "Low clipped gable over an exposed but guarded iron waterwheel bay.", "Terraced mill house with wheel channel on one side and dry service entry on the other.", [lv("lower", -2.8, 2.8, "wheel_service", "gear_room", "drainage_walk", "maintenance_store"), lv("ground", 0, 3.3, "milling_floor", "grain_receiving", "bagging", "tool_room", "dry_entry"), lv("upper", 3.6, 2.5, "grain_store", "foreman_desk", "inspection_gallery", "landing")], "Seamless small interior.", "Step-free ground processing route; guarded lower maintenance ramp; goods lift to grain store.", [STORE, UTILITY, "waf_interior_forge_workshop"], ["common", "farm", "industrial", "storage", "utility"], "Approve water-mill primary variant; wind variant stays a geography-specific branch."),
    spec("religious_cultural_structure", "waf_architecture_religious_cultural_structure", "Stonehold Cultural Memory Hall", "cultural", (18, 13, 9.0), "Low stepped hall roof with a restrained central daylight slot.", "Broad civic-cultural hall with side archive and quiet court; no invented faith iconography.", [lv("ground", 0, 3.6, "entry_vestibule", "assembly_hall", "memory_gallery", "quiet_room", "service_store", "accessible_court"), lv("upper", 3.9, 2.8, "archive", "teaching_gallery", "custodian_room", "landing", "daylight_walk")], "Seamless small interior.", "Step-free assembly/quiet rooms; lift to archive; no ritual route until narrative approval.", [STORE, UTILITY, "waf_interior_religious_cultural_room", "waf_interior_library_archive", "waf_interior_courtyard_balcony"], ["common", "civic", "religious", "archive", "storage", "utility"], "Approve a non-sectarian cultural memory hall; shrine/chapel identity needs narrative approval."),
    spec("shop_service", "waf_architecture_shop_service", "Stonehold Shop and Service Shell", "service", (10, 8, 6.8), "Low clipped gable with projecting iron sign beam.", "Two-bay shop with recessed public front, rear workroom and independent service door.", [lv("ground", 0, 3.0, "public_shop", "service_counter", "display_bay", "rear_workroom", "stockroom", "service_entry"), lv("upper", 3.3, 2.5, "office", "staff_living", "secure_stock", "landing")], "Seamless small interior.", "Step-free public/service entries; upper private route uses stair with reserved compact lift shaft.", [STORE, UTILITY, "waf_interior_market_shop", "waf_interior_bedroom_living"], ["common", "market", "storage", "lodging", "utility"], "Approve mixed shop-over-living as the reusable medium shell."),
    spec("warehouse_barn", "waf_architecture_warehouse_barn", "Stonehold Warehouse and Barn Shell", "service", (20, 14, 9.0), "Long low iron-ribbed gable with modular loading hoods.", "Clear-span enterable shell divided into loading, bulk storage, secure storage and staff-service cells.", [lv("ground", 0, 4.0, "loading_hall", "bulk_store_a", "bulk_store_b", "secure_store", "staff_service", "rear_yard_threshold"), lv("mezzanine", 4.3, 2.7, "light_goods_store", "records", "guard_walk", "goods_landing")], "Seamless small interior.", "Step-free loading loop; dock ramp; goods lift to mezzanine; 4 m clear cross-aisle.", [STORE, UTILITY, "waf_interior_library_archive"], ["common", "storage", "military", "utility", "archive"], "Approve clear-span warehouse as base; barn and shed are modular reductions."),
]


def load_font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    names = [
        Path("C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"),
        Path("C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf"),
    ]
    for candidate in names:
        if candidate.is_file():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


FONTS = {name: load_font(size, bold) for name, size, bold in [
    ("title", 48, True), ("h1", 34, True), ("h2", 26, True), ("body", 21, False),
    ("small", 18, False), ("tiny", 15, False), ("mono", 17, False),
]}


def wrap(text: str, width: int) -> list[str]:
    return textwrap.wrap(text, max(8, width), break_long_words=False, break_on_hyphens=False) or [""]


def draw_wrapped(draw: ImageDraw.ImageDraw, xy: tuple[int, int], text: str, width: int, font: str = "body", fill: str = TEXT, spacing: int = 5, max_lines: int | None = None) -> int:
    lines = wrap(text, width)
    if max_lines is not None:
        lines = lines[:max_lines]
    x, y = xy
    line_h = FONTS[font].getbbox("Ag")[3] + spacing
    for line in lines:
        draw.text((x, y), line, font=FONTS[font], fill=fill)
        y += line_h
    return y


def panel(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], title: str) -> tuple[int, int, int, int]:
    draw.rounded_rectangle(box, radius=18, fill=PANEL, outline="#35434b", width=2)
    x0, y0, x1, y1 = box
    draw.text((x0 + 20, y0 + 15), title, font=FONTS["h2"], fill=ACCENT)
    draw.line((x0 + 20, y0 + 54, x1 - 20, y0 + 54), fill="#35434b", width=2)
    return x0 + 20, y0 + 68, x1 - 20, y1 - 18


def dimension(draw: ImageDraw.ImageDraw, start: tuple[int, int], end: tuple[int, int], label: str) -> None:
    draw.line((*start, *end), fill=LINE, width=2)
    sx, sy = start
    ex, ey = end
    if sy == ey:
        draw.line((sx, sy - 8, sx, sy + 8), fill=LINE, width=2)
        draw.line((ex, ey - 8, ex, ey + 8), fill=LINE, width=2)
        tx = (sx + ex) // 2
        ty = sy - 26
    else:
        draw.line((sx - 8, sy, sx + 8, sy), fill=LINE, width=2)
        draw.line((ex - 8, ey, ex + 8, ey), fill=LINE, width=2)
        tx = sx + 10
        ty = (sy + ey) // 2
    draw.text((tx, ty), label, font=FONTS["tiny"], fill=MUTED, anchor="mm")


def draw_elevation(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], view: str, packet: dict[str, Any]) -> None:
    x0, y0, x1, y1 = box
    w = x1 - x0
    h = y1 - y0
    base_y = y1 - 55
    slug = packet["slug"]
    env = packet["envelopeMeters"]
    shown = env["width"] if view in {"FRONT", "REAR"} else env["depth"]

    def mass(cx_fraction: float, width_fraction: float, height_fraction: float, roof_fraction: float = 0.12, battlement: bool = False) -> tuple[int, int, int, int]:
        cx = x0 + int(w * cx_fraction)
        mass_w = int(w * width_fraction)
        left = cx - mass_w // 2
        right = cx + mass_w // 2
        top = base_y - int(h * height_fraction)
        peak = top - int(h * roof_fraction)
        draw.rectangle((left, top, right, base_y), fill=STONE, outline=LINE, width=3)
        if battlement:
            draw.rectangle((left - 3, top - 12, right + 3, top + 8), fill=IRON, outline=ACCENT, width=2)
            for bx in range(left, right, 30):
                draw.rectangle((bx, top - 24, min(bx + 15, right), top - 8), fill=IRON, outline=ACCENT)
        else:
            draw.polygon([(left - 10, top), (cx, peak), (right + 10, top)], fill=IRON, outline=ACCENT)
        for offset in (0.08, 0.92):
            bx = int(left + (right - left) * offset)
            draw.polygon([(bx - 8, top), (bx + 8, top), (bx + 15, base_y), (bx - 15, base_y)], fill="#2d3438", outline="#59666d")
        return left, top, right, base_y

    if slug in {"city_capital_kit", "settlement_village_kit"}:
        masses = [mass(0.27, 0.28, 0.34, 0.10), mass(0.52, 0.34, 0.46, 0.12), mass(0.77, 0.27, 0.30, 0.09)]
    elif slug in {"castle_enterable", "fortress_enterable"}:
        masses = [mass(0.50, 0.46, 0.55, 0.10, True), mass(0.20, 0.20, 0.68, 0.06, True), mass(0.80, 0.20, 0.68, 0.06, True)]
        draw.line((masses[1][2], base_y - int(h * 0.28), masses[2][0], base_y - int(h * 0.28)), fill=ACCENT, width=5)
    elif slug == "watchtower":
        masses = [mass(0.50, 0.34, 0.72, 0.05, True)]
    elif slug == "well_fountain_cistern":
        masses = [mass(0.50, 0.48, 0.30, 0.17)]
        draw.rectangle((masses[0][0] + 35, base_y + 4, masses[0][2] - 35, base_y + 28), outline=PORTAL, width=3)
        draw.text(((masses[0][0] + masses[0][2]) // 2, base_y + 40), "cistern below", font=FONTS["tiny"], fill=PORTAL, anchor="mm")
    elif slug == "gold_mine":
        masses = [mass(0.38, 0.48, 0.40, 0.10), mass(0.73, 0.26, 0.30, 0.07)]
        hx = x0 + int(w * 0.42)
        draw.line((hx - 45, masses[0][1], hx, y0 + 55, hx + 45, masses[0][1]), fill=IRON, width=12, joint="curve")
        draw.line((hx - 40, masses[0][1], hx + 40, masses[0][1]), fill=ACCENT, width=3)
    elif slug == "mill_wind_water":
        masses = [mass(0.48, 0.58, 0.42, 0.11)]
        wheel_x = masses[0][2] + 22
        wheel_y = base_y - 70
        draw.ellipse((wheel_x - 52, wheel_y - 52, wheel_x + 52, wheel_y + 52), outline=ACCENT, width=6)
        for angle in range(0, 180, 30):
            dx = int(math.cos(math.radians(angle)) * 48)
            dy = int(math.sin(math.radians(angle)) * 48)
            draw.line((wheel_x - dx, wheel_y - dy, wheel_x + dx, wheel_y + dy), fill=LINE, width=2)
    elif slug == "ruin_structure":
        masses = [mass(0.50, 0.62, 0.39, 0.09)]
        draw.polygon([(masses[0][0] + 120, masses[0][1] - 45), (masses[0][0] + 170, masses[0][1] + 25), (masses[0][0] + 220, masses[0][1] - 5)], fill=BG)
        draw.line((masses[0][0] + 120, masses[0][1] - 45, masses[0][0] + 170, masses[0][1] + 25, masses[0][0] + 220, masses[0][1] - 5), fill=DANGER, width=3)
    else:
        aspect_height = max(0.30, min(0.58, 0.34 + 0.24 * env["height"] / max(1.0, shown)))
        width_fraction = 0.72 if packet["category"] in {"industrial", "service", "civic_service"} else 0.62
        masses = [mass(0.50, width_fraction, aspect_height, 0.11)]
    body_l = min(item[0] for item in masses)
    body_r = max(item[2] for item in masses)
    body_t = min(item[1] for item in masses)
    if slug in {"forge", "workshop", "dwelling", "inn_tavern"}:
        chimney_x = body_r - 55
        draw.rectangle((chimney_x, body_t - 55, chimney_x + 28, body_t + 10), fill=STONE, outline=ACCENT, width=2)
    if slug == "market":
        for canopy_x in (body_l + 65, (body_l + body_r) // 2, body_r - 65):
            draw.line((canopy_x - 45, body_t + 45, canopy_x, body_t + 18, canopy_x + 45, body_t + 45), fill=ACCENT, width=3)
    if view == "FRONT":
        door_w = max(32, int(w * 0.09))
        door_h = int(h * 0.26)
        cx = (body_l + body_r) // 2
        draw.rectangle((cx - door_w // 2, base_y - door_h, cx + door_w // 2, base_y), fill="#111518", outline=AMBER, width=4)
        draw.text((cx, base_y - door_h // 2), "SEPARATE\nDOOR", font=FONTS["tiny"], fill=AMBER, anchor="mm", align="center")
    elif view == "REAR":
        door_w = max(24, int(w * 0.065))
        draw.rectangle((body_r - 85, base_y - 80, body_r - 85 + door_w, base_y), fill="#111518", outline=AMBER, width=3)
        draw.text((body_r - 70, base_y - 105), "service", font=FONTS["tiny"], fill=MUTED, anchor="mm")
    for item in masses:
        left, top, right, _ = item
        for i in range(2):
            wx = left + int((i + 1) * (right - left) / 3)
            window_top = min(base_y - 62, top + 45)
            draw.rectangle((wx - 14, window_top, wx + 14, window_top + 38), fill="#151c20", outline="#8ecbe0", width=2)
    draw.line((x0 + 25, base_y, x1 - 25, base_y), fill="#56656d", width=3)
    dimension(draw, (body_l, base_y + 30), (body_r, base_y + 30), f"{shown:g} m")
    draw.text((x0 + 12, y0 + 8), view, font=FONTS["small"], fill=TEXT)


def draw_roof_study(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], packet: dict[str, Any]) -> None:
    x0, y0, x1, y1 = box
    draw.text((x0 + 12, y0 + 8), "ROOF + MATERIAL STUDY", font=FONTS["small"], fill=TEXT)
    rx0, ry0, rx1, ry1 = x0 + 25, y0 + 55, x0 + (x1 - x0) // 2, y1 - 45
    slug = packet["slug"]
    if slug in {"city_capital_kit", "settlement_village_kit", "castle_enterable", "fortress_enterable"}:
        roof_boxes = [(rx0, ry0 + 40, rx0 + 75, ry1 - 20), (rx0 + 85, ry0, rx1 - 85, ry1), (rx1 - 75, ry0 + 40, rx1, ry1 - 20)]
    else:
        roof_boxes = [(rx0, ry0, rx1, ry1)]
    for roof_box in roof_boxes:
        draw.rectangle(roof_box, fill=STONE, outline=LINE, width=3)
        draw.line((roof_box[0], (roof_box[1] + roof_box[3]) // 2, roof_box[2], (roof_box[1] + roof_box[3]) // 2), fill=ACCENT, width=4)
        for x in range(roof_box[0] + 25, roof_box[2], 50):
            draw.line((x, roof_box[1], x, roof_box[3]), fill="#4b555a", width=1)
    if slug == "forge":
        draw.rectangle((rx1 - 55, ry0 - 10, rx1 - 15, ry0 + 35), fill=IRON, outline=ACCENT, width=2)
    if slug == "ruin_structure":
        draw.line((rx0 + 70, ry0, rx0 + 120, ry0 + 65, rx0 + 165, ry0 + 20), fill=DANGER, width=5)
    draw.text(((rx0 + rx1) // 2, ry1 + 20), "top plan / ridge", font=FONTS["tiny"], fill=MUTED, anchor="mm")
    swatches = [("BASALT", STONE), ("IRON", IRON), ("TIMBER", TIMBER), ("BRONZE", BRONZE), ("AMBER", AMBER)]
    sx = rx1 + 35
    sy = ry0
    sw = max(70, (x1 - sx - 20) // 2)
    for i, (name, color) in enumerate(swatches):
        cx = sx + (i % 2) * (sw + 12)
        cy = sy + (i // 2) * 70
        draw.rectangle((cx, cy, cx + sw, cy + 42), fill=color, outline="#75828a")
        draw.text((cx + sw // 2, cy + 52), name, font=FONTS["tiny"], fill=MUTED, anchor="mm")


def prop_matches(prop_id: str, rules: list[str]) -> bool:
    return any(prop_id == rule or (rule.endswith("_") and prop_id.startswith(rule)) for rule in rules)


def expand_props(packet: dict[str, Any], prop_ids: list[str]) -> list[str]:
    found = []
    for pack in packet["propPacks"]:
        for prop_id in prop_ids:
            if prop_matches(prop_id, PROP_PACK_RULES[pack]) and prop_id != "waf_banner_event":
                found.append(prop_id)
    return sorted(set(found))


def build_level_layout(packet: dict[str, Any], level: dict[str, Any]) -> dict[str, Any]:
    width = packet["envelopeMeters"]["width"]
    depth = packet["envelopeMeters"]["depth"]
    wall = 0.3
    corridor = packet["clearancesMeters"]["primaryCirculationWidth"]
    rooms = level["rooms"]
    if packet["slug"] == "town_hall":
        approved = json.loads(CIVIC_LAYOUT_PATH.read_text(encoding="utf-8"))
        approved_rooms = approved["groundFloor" if level["id"] == "ground" else "upperFloor"]
        room_records = [
            {
                "id": room["id"],
                "purpose": room["id"].replace("_", " "),
                "x": room["x"],
                "z": room["z"],
                "width": room["width"],
                "depth": room["depth"],
                "furnitureZones": room.get("furniture", []),
                **({"void": room["void"]} if "void" in room else {}),
            }
            for room in approved_rooms
        ]
        floor_name = "ground" if level["id"] == "ground" else "upper"
        doors = [
            {"id": f"approved_{floor_name}_door_{index:02d}", "separateObject": True, **door}
            for index, door in enumerate((item for item in approved["doorOpenings"] if item["floor"] == floor_name), 1)
        ]
        windows = [
            {"id": f"window_{level['id']}_{room['id']}", "separateFrameGlassShutter": True, "serves": room["id"], "openingMeters": [1.2, 1.5]}
            for room in room_records
        ]
        return {
            **level,
            "rooms": room_records,
            "circulation": {"id": "approved_primary_circulation", "x": 2.1, "z": 2.2, "width": 5.3, "depth": 1.2},
            "adjacency": [door["between"] for door in doors],
            "doorOpenings": doors,
            "windowOpenings": windows,
            "windowPolicy": "Real recessed openings use separate frame, glass and shutter objects.",
            "verticalCore": {"stairMeters": [1.5, 1.5], "liftPlatformReserved": True, "separateObjects": True},
            "portalBoundary": "none",
            "sourceAuthority": "PR #664 shared_civic_hall_layout_v001.json coordinates retained exactly",
        }
    top_count = math.ceil(len(rooms) / 2)
    bottom_count = len(rooms) - top_count
    clear_depth = depth - 2 * wall - corridor
    top_depth = clear_depth / 2
    bottom_depth = clear_depth - top_depth
    room_records = []
    def room_weight(room_id: str) -> float:
        if any(token in room_id for token in ["hall", "court", "workfloor", "floor", "muster", "loading", "gallery", "common", "processing", "market", "water_chamber", "living_hearth"]):
            return 1.7
        if any(token in room_id for token in ["store", "office", "landing", "service", "archive", "pantry", "wash", "desk", "station"]):
            return 0.85
        return 1.15
    for idx, room_id in enumerate(rooms):
        top = idx < top_count
        local_idx = idx if top else idx - top_count
        row_rooms = rooms[:top_count] if top else rooms[top_count:]
        weights = [room_weight(item) for item in row_rooms]
        row_width = width - 2 * wall
        rw = row_width * weights[local_idx] / sum(weights)
        rx = wall + row_width * sum(weights[:local_idx]) / sum(weights)
        room_records.append({
            "id": room_id,
            "purpose": room_id.replace("_", " "),
            "x": round(rx, 3),
            "z": round(wall + (top_depth + corridor if top else 0), 3),
            "width": round(rw, 3),
            "depth": round(top_depth if top else bottom_depth, 3),
            "furnitureZones": [f"{room_id}_primary", f"{room_id}_storage", "clear_route_edge"],
        })
    circulation = {"id": "primary_circulation", "x": wall, "z": round(wall + bottom_depth, 3), "width": round(width - 2 * wall, 3), "depth": corridor}
    doors = [{"id": f"door_{level['id']}_{room['id']}", "separateObject": True, "between": [room["id"], "primary_circulation"], "clearOpeningMeters": [1.2, 2.4]} for room in room_records]
    windows = [] if level["elevationMeters"] < 0 else [{"id": f"window_{level['id']}_{room['id']}", "separateFrameGlassShutter": True, "serves": room["id"], "openingMeters": [1.2, 1.5]} for room in room_records]
    adjacency = [[room["id"], "primary_circulation"] for room in room_records]
    has_portal_room = any(any(token in room["id"] for token in ["portal", "airlock", "loading_cover"]) for room in room_records)
    return {
        **level,
        "rooms": room_records,
        "circulation": circulation,
        "adjacency": adjacency,
        "doorOpenings": doors,
        "windowOpenings": windows,
        "windowPolicy": "No exterior windows below grade; separate ventilation and emergency shafts." if level["elevationMeters"] < 0 else "Real recessed openings use separate frame, glass and shutter objects.",
        "verticalCore": {"stairMeters": [1.5, 1.5], "liftPlatformReserved": True, "separateObjects": True},
        "portalBoundary": "physical_loading_cover_and_streaming_portal" if has_portal_room else "none_on_this_level",
    }


def draw_floor_plan(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], layout: dict[str, Any], packet: dict[str, Any]) -> None:
    x0, y0, x1, y1 = box
    draw.text((x0 + 8, y0 + 5), f"{layout['id'].upper()}  elev {layout['elevationMeters']:+g} m / clear {layout['clearHeightMeters']:g} m", font=FONTS["small"], fill=TEXT)
    px0, py0, px1, py1 = x0 + 25, y0 + 45, x1 - 35, y1 - 38
    env = packet["envelopeMeters"]
    sx = (px1 - px0) / env["width"]
    sz = (py1 - py0) / env["depth"]
    for index, room in enumerate(layout["rooms"]):
        rx0 = px0 + room["x"] * sx
        rz0 = py0 + room["z"] * sz
        rx1 = rx0 + room["width"] * sx
        rz1 = rz0 + room["depth"] * sz
        fill = PANEL_ALT if index % 2 else "#263842"
        draw.rectangle((rx0, rz0, rx1, rz1), fill=fill, outline=LINE, width=2)
        furnish_w = min(42, max(18, int((rx1 - rx0) * 0.18)))
        draw.rectangle((rx0 + 10, rz0 + 10, rx0 + 10 + furnish_w, rz0 + 28), fill=BRONZE, outline="#9b7249")
        draw.rectangle((rx1 - 10 - furnish_w, rz1 - 28, rx1 - 10, rz1 - 10), fill=TIMBER, outline="#756053")
        if "void" in room:
            void = room["void"]
            vx0 = px0 + void["x"] * sx
            vz0 = py0 + void["z"] * sz
            vx1 = vx0 + void["width"] * sx
            vz1 = vz0 + void["depth"] * sz
            draw.rectangle((vx0, vz0, vx1, vz1), fill=BG, outline=PORTAL, width=3)
            draw.text(((vx0 + vx1) / 2, (vz0 + vz1) / 2), "OPEN VOID", font=FONTS["tiny"], fill=PORTAL, anchor="mm")
        label = room["purpose"].replace(" ", "\n")
        draw.multiline_text(((rx0 + rx1) / 2, (rz0 + rz1) / 2), label, font=FONTS["tiny"], fill=TEXT, anchor="mm", align="center", spacing=2)
    route = layout["circulation"]
    cy = py0 + (route["z"] + route["depth"] / 2) * sz
    draw.line((px0 + 4, cy, px1 - 4, cy), fill=ROUTE, width=8)
    draw.polygon([(px1 - 12, cy - 10), (px1 + 5, cy), (px1 - 12, cy + 10)], fill=ROUTE)
    for room in layout["rooms"]:
        cx = px0 + (room["x"] + room["width"] / 2) * sx
        room_above = room["z"] > route["z"]
        door_y = py0 + (room["z"] if room_above else room["z"] + room["depth"]) * sz
        draw.line((cx - 14, door_y, cx + 14, door_y), fill=AMBER, width=7)
        if layout["windowOpenings"]:
            window_y = py0 + (room["z"] + room["depth"] if room_above else room["z"]) * sz
            draw.line((cx - 12, window_y, cx + 12, window_y), fill="#8ecbe0", width=5)
    if layout["portalBoundary"] == "physical_loading_cover_and_streaming_portal":
        portal_room = next((room for room in layout["rooms"] if any(token in room["id"] for token in ["portal", "airlock", "loading_cover"])), layout["rooms"][-1])
        rx0 = px0 + portal_room["x"] * sx
        rz0 = py0 + portal_room["z"] * sz
        rx1 = rx0 + portal_room["width"] * sx
        rz1 = rz0 + portal_room["depth"] * sz
        draw.rectangle((rx0 + 5, rz0 + 5, rx1 - 5, rz1 - 5), outline=PORTAL, width=7)
        draw.text((rx1 - 12, rz0 + 12), "STREAM PORTAL", font=FONTS["tiny"], fill=PORTAL, anchor="ra")
    draw.rectangle((px0, py0, px1, py1), outline=ACCENT, width=4)
    dimension(draw, (px0, py1 + 20), (px1, py1 + 20), f"{env['width']:g} m")
    dimension(draw, (px1 + 18, py0), (px1 + 18, py1), f"{env['depth']:g} m")


def packet_document(source: dict[str, Any], prop_ids: list[str]) -> dict[str, Any]:
    layouts = [build_level_layout(source, level) for level in source["levels"]]
    prop_families = expand_props(source, prop_ids)
    owner_gates = [
        {"id": "massing", "question": "Approve the recommended exterior massing?", "recommendation": source["massingRecommendation"], "choices": ["APPROVE", "REVISE", "REJECT"], "decision": None},
        {"id": "roof", "question": "Approve the recommended roof profile?", "recommendation": source["roofRecommendation"], "choices": ["APPROVE", "REVISE", "REJECT"], "decision": None},
        {"id": "materials", "question": "Approve the shown Stonehold material balance?", "recommendation": "Basalt dominant, iron secondary, timber interior, bronze repair sparse.", "choices": ["APPROVE", "REVISE", "REJECT"], "decision": None},
        {"id": "access", "question": "Approve the entry, circulation and accessibility sequence?", "recommendation": source["accessibilityRoute"], "choices": ["APPROVE", "REVISE", "REJECT"], "decision": None},
        {"id": "family_specific", "question": source["familySpecificGate"], "recommendation": "The option drawn and documented in this packet.", "choices": ["APPROVE", "REVISE", "REJECT"], "decision": None},
    ]
    return {
        "schema": "anotherlife.stonehold-enterable-structure-2d-handoff.v1",
        "packetSetId": PACKET_SET_ID,
        **source,
        "units": "meters",
        "views": ["front_elevation", "side_elevation", "rear_elevation", "roof_plan", "material_study", "every_floor_plan", "longitudinal_section", "cross_section"],
        "floorPlans": layouts,
        "sections": {
            "longitudinal": {"spanMeters": source["envelopeMeters"]["width"], "levels": [{"id": x["id"], "elevationMeters": x["elevationMeters"], "clearHeightMeters": x["clearHeightMeters"]} for x in layouts]},
            "cross": {"spanMeters": source["envelopeMeters"]["depth"], "levels": [{"id": x["id"], "elevationMeters": x["elevationMeters"], "clearHeightMeters": x["clearHeightMeters"]} for x in layouts]},
        },
        "roomPurposeAndAdjacency": "Each room has a purpose label and connects to the dimensioned primary circulation spine; adjacency and separate openings are explicit in every floor plan.",
        "sharedModules": SHARED_MODULE_REGISTER,
        "furnishedRoomModules": source["interiorModuleIds"],
        "propDecorFamilies": prop_families,
        "propVisualAuthority": "Room-fit placeholders and taxonomy bindings only. Individual prop looks remain owner-review gated by prop_stonehold_interior_decor_v001.",
        "sockets": {name: {"required": True, "separateFromCleanGeometry": name == "vfx_anchor_runtime_separate"} for name in source["socketSets"]},
        "variants": {name: {"required": True, "sharedGeometryPreferred": True} for name in source["variants"]},
        "bindingPolicies": BINDING_POLICIES,
        "mobilePolicy": {
            "lodIntent": ["LOD0 authored source", "LOD1 50-60% silhouette-preserving", "LOD2 20-30%", "HLOD 5-10% or chunk proxy"],
            "atlasIntent": ["one shared opaque Stonehold exterior atlas", "one shared opaque interior atlas", "minimum separate glass renderer set"],
            "protectedCues": ["compressed roofline", "stepped basalt foundation", "thick buttress rhythm", "clipped-corner aperture"],
            "dropFirst": ["workers", "secondary clutter", "scaffolds", "smoke", "sparks", "small banners"],
        },
        "provenance": PROVENANCE,
        "avoid": AVOID,
        "ownerReview": {"required": True, "allowedDecisions": ["APPROVE", "REVISE", "REJECT"], "decision": None, "gates": owner_gates},
        "productionAuthorization": {"comfyuiLocal": "approved_for_2d_only", "meshy": False, "blender": False, "runtime3d": False, "new3dJobsSubmitted": 0},
    }


def packet_shape_errors(packet: dict[str, Any]) -> list[str]:
    """Return fail-closed errors for one generated structure packet."""
    errors = []
    required_views = {"front_elevation", "side_elevation", "rear_elevation", "roof_plan", "material_study", "every_floor_plan", "longitudinal_section", "cross_section"}
    required_modules = {SHELL, DOOR, ENTRY, CORRIDOR, STAIR, CUT}
    if packet.get("status") != "pending_owner_review":
        errors.append("status must remain pending_owner_review")
    if not required_views <= set(packet.get("views", [])):
        errors.append("required exterior, roof, every-floor and section views are incomplete")
    floor_plans = packet.get("floorPlans", [])
    if not floor_plans or len(floor_plans) != len(packet.get("levels", [])):
        errors.append("every physical level must have exactly one floor plan")
    elif not all(level.get("rooms") and level.get("adjacency") and level.get("doorOpenings") and "windowOpenings" in level and level.get("windowPolicy") for level in floor_plans):
        errors.append("each floor needs rooms, adjacency, separate doors and an explicit window policy")
    else:
        for level in floor_plans:
            has_portal_room = any(any(token in room["id"] for token in ["portal", "airlock", "loading_cover"]) for room in level["rooms"])
            if has_portal_room != (level.get("portalBoundary") == "physical_loading_cover_and_streaming_portal"):
                errors.append("physical portal rooms and streaming-boundary markers must match per level")
                break
    if not required_modules <= set(packet.get("furnishedRoomModules", [])):
        errors.append("shared shell, aperture, entry, corridor, stair and cutaway modules are required")
    if not packet.get("propDecorFamilies"):
        errors.append("at least one furnished prop/decor family is required")
    review = packet.get("ownerReview", {})
    gates = review.get("gates", [])
    if review.get("decision") is not None or len(gates) != 5:
        errors.append("owner review must remain undecided with five explicit gates")
    elif not all(gate.get("decision") is None and gate.get("choices") == ["APPROVE", "REVISE", "REJECT"] for gate in gates):
        errors.append("every creative gate must expose APPROVE/REVISE/REJECT and remain undecided")
    authorization = packet.get("productionAuthorization", {})
    if authorization.get("new3dJobsSubmitted") != 0 or any(authorization.get(key) for key in ["meshy", "blender", "runtime3d"]):
        errors.append("3D production must remain unauthorized with zero submitted jobs")
    policies = packet.get("bindingPolicies", {})
    if "non-enterable" not in policies.get("perimeterWall", "") or "separate object" not in policies.get("doorsAndGates", ""):
        errors.append("perimeter-wall exception and separate aperture-object policies are required")
    return errors


def draw_exterior_sheet(packet: dict[str, Any], destination: Path) -> None:
    image = Image.new("RGB", SHEET_SIZE, BG)
    draw = ImageDraw.Draw(image)
    draw.text((70, 45), packet["title"], font=FONTS["title"], fill=TEXT)
    draw.text((70, 105), packet["taxonomyId"], font=FONTS["small"], fill=MUTED)
    draw.text((2300, 65), "v001 · PENDING OWNER", font=FONTS["h2"], fill=AMBER, anchor="ra")
    boxes = [(55, 155, 620, 720), (645, 155, 1210, 720), (1235, 155, 1800, 720), (1825, 155, 2345, 720)]
    for box, view in zip(boxes[:3], ["FRONT", "SIDE", "REAR"]):
        panel(draw, box, view)
        draw_elevation(draw, (box[0] + 15, box[1] + 65, box[2] - 15, box[3] - 15), view, packet)
    panel(draw, boxes[3], "ROOF / MATERIAL")
    draw_roof_study(draw, (boxes[3][0] + 15, boxes[3][1] + 65, boxes[3][2] - 15, boxes[3][3] - 15), packet)
    left = panel(draw, (55, 755, 1200, 1415), "EXTERIOR → INTERIOR CONTINUITY")
    x, y, x1, _ = left
    y = draw_wrapped(draw, (x, y), packet["massingRecommendation"], 88, "body", TEXT)
    y += 12
    y = draw_wrapped(draw, (x, y), "Roof: " + packet["roofRecommendation"], 88, "body", MUTED)
    y += 12
    env = packet["envelopeMeters"]
    draw.text((x, y), f"Envelope  {env['width']:g} × {env['depth']:g} × {env['height']:g} m", font=FONTS["h2"], fill=ACCENT)
    y += 48
    y = draw_wrapped(draw, (x, y), "Separate objects: door leaves/frames, glass, shutters, local gates and main gates. Windows are real recesses; no painted apertures.", 88, "body", TEXT)
    y += 10
    draw.text((x, y), "VARIANTS", font=FONTS["h2"], fill=ACCENT)
    draw.text((x + 165, y + 2), "CLEAN  ·  USED  ·  DAMAGED", font=FONTS["body"], fill=TEXT)
    y += 48
    draw.text((x, y), "MODULES", font=FONTS["h2"], fill=ACCENT)
    y += 38
    draw_wrapped(draw, (x, y), " · ".join(SHARED_MODULE_REGISTER["structural"][:10]), 95, "small", MUTED, max_lines=4)
    right = panel(draw, (1225, 755, 2345, 1415), "REVIEW GATES / BINDING RULES")
    x, y, x1, _ = right
    for gate in packet["ownerReview"]["gates"]:
        draw.text((x, y), "□ APPROVE   □ REVISE   □ REJECT", font=FONTS["small"], fill=AMBER)
        y += 28
        y = draw_wrapped(draw, (x + 18, y), gate["question"], 82, "small", TEXT, max_lines=2)
        y += 8
    draw.line((x, y, x1, y), fill="#42515a", width=2)
    y += 16
    for rule in [BINDING_POLICIES["perimeterWall"], BINDING_POLICIES["mainGate"], BINDING_POLICIES["hostileGateBreak"]]:
        y = draw_wrapped(draw, (x, y), "• " + rule, 86, "tiny", MUTED, max_lines=3) + 5
    image.save(destination, optimize=True)


def draw_section(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], packet: dict[str, Any], section_name: str) -> None:
    x0, y0, x1, y1 = box
    draw.text((x0 + 8, y0 + 6), section_name, font=FONTS["small"], fill=TEXT)
    base = y1 - 35
    left = x0 + 40
    right = x1 - 40
    levels = packet["floorPlans"]
    minimum_elevation = min(0.0, *(level["elevationMeters"] for level in levels))
    maximum_elevation = max(packet["envelopeMeters"]["height"], *(level["elevationMeters"] + level["clearHeightMeters"] for level in levels))
    total = max(1.0, maximum_elevation - minimum_elevation)
    top = y0 + 62
    scale = (base - top) / total
    def elevation_y(value: float) -> float:
        return base - (value - minimum_elevation) * scale
    ground_y = elevation_y(0.0)
    roof_y = elevation_y(maximum_elevation)
    draw.rectangle((left, roof_y, right, base), fill="#202a30", outline=ACCENT, width=3)
    draw.polygon([(left - 8, roof_y), ((left + right) // 2, max(y0 + 46, roof_y - 30)), (right + 8, roof_y)], fill=IRON, outline=ACCENT)
    if minimum_elevation < 0:
        draw.rectangle((left, ground_y, right, base), fill="#182127", outline=PORTAL, width=2)
        draw.text((right - 12, base - 20), "BELOW GRADE", font=FONTS["tiny"], fill=PORTAL, anchor="ra")
    for level in levels:
        floor_y = elevation_y(level["elevationMeters"])
        draw.line((left, floor_y, right, floor_y), fill=LINE, width=3)
        draw.text((left + 10, floor_y - 24), f"{level['id']} {level['elevationMeters']:+g} m", font=FONTS["tiny"], fill=MUTED)
    cx = (left + right) // 2
    draw.line((cx - 30, base, cx + 30, roof_y + 8), fill=ROUTE, width=7)
    draw.text((cx + 38, (base + roof_y) // 2), "stair / lift core", font=FONTS["tiny"], fill=ROUTE)
    dimension(draw, (left, base + 18), (right, base + 18), f"{packet['sections']['longitudinal' if section_name.startswith('LONG') else 'cross']['spanMeters']:g} m")


def draw_interior_sheet(packet: dict[str, Any], destination: Path) -> None:
    image = Image.new("RGB", INTERIOR_SIZE, BG)
    draw = ImageDraw.Draw(image)
    draw.text((70, 38), packet["title"] + " — EVERY FLOOR + SECTIONS", font=FONTS["title"], fill=TEXT)
    draw.text((70, 98), "Room purpose · adjacency · circulation · clearance · accessibility · streaming/portal boundaries", font=FONTS["small"], fill=MUTED)
    draw.text((2910, 62), "v001 · PENDING OWNER", font=FONTS["h2"], fill=AMBER, anchor="ra")
    level_count = len(packet["floorPlans"])
    cols = 2
    rows = math.ceil(level_count / cols)
    floor_area = (55, 145, 2020, 1425)
    cell_w = (floor_area[2] - floor_area[0] - 25) // cols
    cell_h = (floor_area[3] - floor_area[1] - 20 * (rows - 1)) // rows
    for index, layout in enumerate(packet["floorPlans"]):
        col = index % cols
        row = index // cols
        box = (floor_area[0] + col * (cell_w + 25), floor_area[1] + row * (cell_h + 20), floor_area[0] + col * (cell_w + 25) + cell_w, floor_area[1] + row * (cell_h + 20) + cell_h)
        draw.rounded_rectangle(box, radius=14, fill=PANEL, outline="#35434b", width=2)
        draw_floor_plan(draw, (box[0] + 8, box[1] + 5, box[2] - 8, box[3] - 8), layout, packet)
    sec1 = (2050, 145, 2945, 610)
    sec2 = (2050, 635, 2945, 1100)
    draw.rounded_rectangle(sec1, radius=14, fill=PANEL, outline="#35434b", width=2)
    draw.rounded_rectangle(sec2, radius=14, fill=PANEL, outline="#35434b", width=2)
    draw_section(draw, (sec1[0] + 12, sec1[1] + 10, sec1[2] - 12, sec1[3] - 10), packet, "LONGITUDINAL SECTION")
    draw_section(draw, (sec2[0] + 12, sec2[1] + 10, sec2[2] - 12, sec2[3] - 10), packet, "CROSS SECTION")
    info = panel(draw, (2050, 1125, 2945, 1835), "CIRCULATION / STREAMING / FURNISHING")
    x, y, _, _ = info
    y = draw_wrapped(draw, (x, y), "ACCESS  " + packet["accessibilityRoute"], 68, "small", TEXT, max_lines=5) + 8
    y = draw_wrapped(draw, (x, y), "STREAM  " + packet["streamingPolicy"], 68, "small", PORTAL, max_lines=4) + 8
    c = packet["clearancesMeters"]
    y = draw_wrapped(draw, (x, y), f"CLEAR  aisle ≥ {c['minimumFurnitureAisle']} m · circulation {c['primaryCirculationWidth']} m · combat diameter {c['combatClearDiameter']} m · camera backoff {c['cameraBackoff']} m", 68, "small", ROUTE, max_lines=4) + 8
    draw.text((x, y), "ROOM MODULES", font=FONTS["small"], fill=ACCENT)
    y += 30
    y = draw_wrapped(draw, (x, y), " · ".join(item.removeprefix("waf_interior_") for item in packet["furnishedRoomModules"]), 70, "tiny", MUTED, max_lines=6) + 10
    draw.text((x, y), f"PROP/DECOR TAXONOMY BINDINGS  {len(packet['propDecorFamilies'])}", font=FONTS["small"], fill=ACCENT)
    y += 30
    draw_wrapped(draw, (x, y), " · ".join(item.removeprefix("waf_") for item in packet["propDecorFamilies"][:18]), 72, "tiny", MUTED, max_lines=7)
    legend = panel(draw, (55, 1455, 2020, 1835), "LEGEND / HARD RULES")
    x, y, x1, _ = legend
    draw.line((x, y + 10, x + 120, y + 10), fill=ROUTE, width=8)
    draw.text((x + 135, y), "primary accessible circulation", font=FONTS["small"], fill=TEXT)
    draw.rectangle((x + 510, y - 5, x + 560, y + 25), outline=AMBER, width=3)
    draw.text((x + 575, y), "separate door/window object", font=FONTS["small"], fill=TEXT)
    draw.rectangle((x + 1040, y - 5, x + 1090, y + 25), outline=PORTAL, width=3)
    draw.text((x + 1105, y), "physical streaming portal", font=FONTS["small"], fill=TEXT)
    y += 52
    rules = [
        "Every labeled room is built and reachable; no solid-shell substitute.",
        "Perimeter wall is non-enterable/impassable; walltop/designated routes only.",
        "Gate/door/glass/shutter are separate objects; intact main gate never opens a physical route.",
        "Lighting, interaction, quest, audio and VFX sockets are separate semantic anchors.",
        "Runtime VFX are removable quality layers; no magic is baked into clean geometry.",
    ]
    for i, rule in enumerate(rules):
        col = i % 2
        row = i // 2
        draw_wrapped(draw, (x + col * 930, y + row * 72), "• " + rule, 68, "small", MUTED, max_lines=3)
    image.save(destination, optimize=True)


def file_record(path: Path) -> dict[str, Any]:
    raw = path.read_bytes()
    record: dict[str, Any] = {
        "locator": path.relative_to(OUTPUT_ROOT).as_posix(),
        "byteLength": len(raw),
        "sha256": hashlib.sha256(raw).hexdigest(),
    }
    if path.suffix.lower() == ".png":
        with Image.open(path) as image:
            record["pixelDimensions"] = list(image.size)
    return record


def write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")


def build_review_pages(packet_docs: list[dict[str, Any]]) -> list[Path]:
    pages = []
    for page_index in range(math.ceil(len(packet_docs) / 6)):
        page_packets = packet_docs[page_index * 6:(page_index + 1) * 6]
        image = Image.new("RGB", (2400, 1700), BG)
        draw = ImageDraw.Draw(image)
        draw.text((60, 40), "STONEHOLD ENTERABLE STRUCTURE PACKETS — REVIEW INDEX", font=FONTS["title"], fill=TEXT)
        draw.text((60, 100), f"v001 · page {page_index + 1}/{math.ceil(len(packet_docs) / 6)} · choose APPROVE / REVISE / REJECT per packet", font=FONTS["small"], fill=AMBER)
        for local_index, packet in enumerate(page_packets):
            col = local_index % 2
            row = local_index // 2
            x0 = 55 + col * 1175
            y0 = 150 + row * 495
            box = (x0, y0, x0 + 1120, y0 + 455)
            draw.rounded_rectangle(box, radius=18, fill=PANEL, outline="#35434b", width=2)
            draw.text((x0 + 22, y0 + 18), f"{page_index * 6 + local_index + 1:02d}  {packet['title']}", font=FONTS["h2"], fill=TEXT)
            draw.text((x0 + 22, y0 + 56), packet["taxonomyId"], font=FONTS["tiny"], fill=MUTED)
            thumb_path = PACKET_ROOT / packet["slug"] / f"{packet['slug']}_exterior_handoff_v001.png"
            thumb = Image.open(thumb_path).convert("RGB")
            thumb.thumbnail((520, 320), Image.Resampling.LANCZOS)
            image.paste(thumb, (x0 + 22, y0 + 92))
            tx = x0 + 570
            ty = y0 + 98
            draw_wrapped(draw, (tx, ty), packet["massingRecommendation"], 45, "small", TEXT, max_lines=5)
            ty += 135
            draw_wrapped(draw, (tx, ty), f"{len(packet['floorPlans'])} physical levels · {len(packet['propDecorFamilies'])} prop bindings", 45, "small", ROUTE, max_lines=2)
            ty += 65
            draw.text((tx, ty), "□ APPROVE", font=FONTS["small"], fill=AMBER)
            draw.text((tx + 170, ty), "□ REVISE", font=FONTS["small"], fill=AMBER)
            draw.text((tx + 330, ty), "□ REJECT", font=FONTS["small"], fill=AMBER)
        destination = REVIEW_ROOT / f"stonehold_enterable_packets_review_index_{page_index + 1:02d}_v001.png"
        image.save(destination, optimize=True)
        pages.append(destination)
    markdown_lines = [
        "# Stonehold enterable structure packets — owner review v001",
        "",
        "Record exactly one decision per packet: `APPROVE`, `REVISE`, or `REJECT`.",
        "No packet authorizes 3D production while its decision remains pending.",
        "",
    ]
    for index, packet in enumerate(packet_docs, start=1):
        base = f"../packets/{packet['slug']}"
        markdown_lines.extend([
            f"<details><summary><strong>{index:02d}. {packet['title']}</strong> — {packet['taxonomyId']}</summary>",
            "",
            "Decision: [ ] APPROVE  [ ] REVISE  [ ] REJECT",
            "",
            f"![{packet['title']} exterior]({base}/{packet['slug']}_exterior_handoff_v001.png)",
            "",
            f"![{packet['title']} every-floor and sections]({base}/{packet['slug']}_interior_handoff_v001.png)",
            "",
            f"[Machine-readable packet]({base}/{packet['slug']}_2d_handoff_v001.json)",
            "",
            "</details>",
            "",
        ])
    markdown_path = REVIEW_ROOT / "README.md"
    markdown_path.write_text("\n".join(markdown_lines), encoding="utf-8", newline="\n")
    pages.append(markdown_path)
    return pages


def build_html(packet_docs: list[dict[str, Any]], review_pages: list[Path]) -> str:
    cards = []
    for packet in packet_docs:
        base = f"packets/{packet['slug']}"
        cards.append(f"""
<article>
  <h2>{html.escape(packet['title'])}</h2>
  <code>{html.escape(packet['taxonomyId'])}</code>
  <p>{html.escape(packet['massingRecommendation'])}</p>
  <p><strong>{len(packet['floorPlans'])} physical levels</strong> · {len(packet['propDecorFamilies'])} prop/decor bindings · PENDING OWNER REVIEW</p>
  <a href="{base}/{packet['slug']}_exterior_handoff_v001.png">Exterior sheet</a>
  <a href="{base}/{packet['slug']}_interior_handoff_v001.png">Every-floor + sections</a>
  <a href="{base}/{packet['slug']}_2d_handoff_v001.json">Machine-readable packet</a>
  <p class="decision">□ APPROVE &nbsp; □ REVISE &nbsp; □ REJECT</p>
</article>""")
    page_links = "".join(
        f'<a href="review/{p.name}">Review index {i + 1}</a>'
        for i, p in enumerate(path for path in review_pages if path.suffix.lower() == ".png")
    ) + '<a href="review/README.md">Full packet review</a>'
    return f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Stonehold Enterable Structure Packets v001</title>
<style>body{{background:#0f151b;color:#e8edf0;font:17px Segoe UI,Arial;margin:32px}}header{{max-width:1100px}}.rules{{background:#19242c;padding:20px;border-left:5px solid #d59736}}.grid{{display:grid;grid-template-columns:repeat(auto-fit,minmax(360px,1fr));gap:18px}}article{{background:#19242c;border:1px solid #35434b;border-radius:14px;padding:20px}}a{{color:#67c9c3;margin-right:15px}}code{{color:#a9b6bd}}.decision{{color:#efb24b;font-weight:700}}</style></head>
<body><header><h1>Stonehold Enterable Structure Packets v001</h1><p>27 complete 2D handoff packets. Each binds exterior massing to furnished, traversable every-floor plans and two sections. No Meshy, Blender or runtime 3D is authorized.</p><p>{page_links}</p><div class="rules"><strong>Hard rules:</strong> main city/castle/fortress perimeter walls are non-enterable and impassable; gates and doors are separate objects; intact main-gate teleport and hostile break are referenced only; small interiors are seamless and large combat interiors are separately streamed.</div></header><main class="grid">{''.join(cards)}</main></body></html>"""


def build() -> dict[str, Any]:
    coverage = json.loads(COVERAGE_PATH.read_text(encoding="utf-8"))
    prop_ids = sorted(record["familyId"] for record in coverage["families"] if record["packetId"] == "prop_stonehold_interior_decor_v001")
    if OUTPUT_ROOT.exists():
        shutil.rmtree(OUTPUT_ROOT)
    PACKET_ROOT.mkdir(parents=True)
    REVIEW_ROOT.mkdir(parents=True)
    packet_docs = []
    packet_artifacts = []
    for source in STRUCTURES:
        packet = packet_document(source, prop_ids)
        packet_docs.append(packet)
        folder = PACKET_ROOT / packet["slug"]
        folder.mkdir(parents=True)
        json_path = folder / f"{packet['slug']}_2d_handoff_v001.json"
        exterior_path = folder / f"{packet['slug']}_exterior_handoff_v001.png"
        interior_path = folder / f"{packet['slug']}_interior_handoff_v001.png"
        write_json(json_path, packet)
        draw_exterior_sheet(packet, exterior_path)
        draw_interior_sheet(packet, interior_path)
        packet_artifacts.append({
            "packetId": packet["packetId"],
            "taxonomyId": packet["taxonomyId"],
            "status": packet["status"],
            "physicalLevelCount": len(packet["floorPlans"]),
            "artifacts": [file_record(json_path), file_record(exterior_path), file_record(interior_path)],
        })
    review_pages = build_review_pages(packet_docs)
    index_path = OUTPUT_ROOT / "index.html"
    index_path.write_text(build_html(packet_docs, review_pages), encoding="utf-8", newline="\n")
    manifest = {
        "schema": "anotherlife.stonehold-enterable-structure-packet-set.v1",
        "packetSetId": PACKET_SET_ID,
        "version": VERSION,
        "status": "pending_owner_review",
        "ownerDecisionRequired": "APPROVE_REVISE_REJECT_per_packet",
        "enterablePacketCount": len(packet_docs),
        "sharedModuleFamilyCount": len(SHARED_SUPPORT_FAMILIES),
        "new3dJobsSubmitted": 0,
        "coverage": {
            "enterablePackets": [packet["taxonomyId"] for packet in packet_docs],
            "sharedModuleFamilies": SHARED_SUPPORT_FAMILIES,
            "nonEnterableException": ["waf_architecture_building_wall"],
            "nonStoneholdExclusion": ["waf_architecture_event_accordant_isle"],
        },
        "bindingPolicies": BINDING_POLICIES,
        "sharedModuleRegister": SHARED_MODULE_REGISTER,
        "propFamilyCoverage": {
            "mapped": sorted(set(item for packet in packet_docs for item in packet["propDecorFamilies"])),
            "excluded": ["waf_banner_event"],
        },
        "interiorModuleCoverage": sorted(set(item for packet in packet_docs for item in packet["furnishedRoomModules"])),
        "provenance": PROVENANCE,
        "avoid": AVOID,
        "packets": packet_artifacts,
        "reviewArtifacts": [file_record(path) for path in review_pages] + [file_record(index_path)],
        "productionAuthorization": {"comfyuiLocal": "approved_for_2d_only_not_used_by_this_deterministic_packet", "meshy": False, "blender": False, "runtime3d": False},
    }
    manifest_path = OUTPUT_ROOT / "stonehold_enterable_structure_packet_set_manifest_v001.json"
    write_json(manifest_path, manifest)
    return manifest


def validate(manifest: dict[str, Any] | None = None) -> dict[str, Any]:
    if manifest is None:
        manifest = json.loads((OUTPUT_ROOT / "stonehold_enterable_structure_packet_set_manifest_v001.json").read_text(encoding="utf-8"))
    coverage = json.loads(COVERAGE_PATH.read_text(encoding="utf-8"))
    enterable = {record["familyId"] for record in coverage["families"] if record["packetId"] == "architecture_stonehold_enterable_structures_v001"}
    settlement = {record["familyId"] for record in coverage["families"] if record["packetId"] == "architecture_stonehold_settlement_silhouettes_v001"}
    expected = enterable | settlement
    actual = set(manifest["coverage"]["enterablePackets"]) | set(manifest["coverage"]["sharedModuleFamilies"]) | set(manifest["coverage"]["nonEnterableException"]) | set(manifest["coverage"]["nonStoneholdExclusion"])
    checks: dict[str, Any] = {}
    checks["taxonomyCoverage40of40"] = actual == expected and len(expected) == 40
    checks["enterablePackets27of27"] = len(manifest["packets"]) == 27 and len(set(manifest["coverage"]["enterablePackets"])) == 27
    checks["sharedModuleFamilies11of11"] = set(manifest["coverage"]["sharedModuleFamilies"]) == set(SHARED_SUPPORT_FAMILIES)
    checks["wallExceptionExplicit"] = manifest["coverage"]["nonEnterableException"] == ["waf_architecture_building_wall"] and "non-enterable" in manifest["bindingPolicies"]["perimeterWall"]
    checks["accordantExcluded"] = manifest["coverage"]["nonStoneholdExclusion"] == ["waf_architecture_event_accordant_isle"]
    checks["gateRulesReferenceOnly"] = "implements no teleport" in manifest["bindingPolicies"]["mainGate"] and "t_c8ea885d" in manifest["bindingPolicies"]["hostileGateBreak"]
    checks["no3dAuthorization"] = manifest["new3dJobsSubmitted"] == 0 and not any(manifest["productionAuthorization"][key] for key in ["meshy", "blender", "runtime3d"])
    packet_checks = []
    artifact_ok = True
    no_3d_files = True
    for entry in manifest["packets"]:
        packet_file = OUTPUT_ROOT / entry["artifacts"][0]["locator"]
        packet = json.loads(packet_file.read_text(encoding="utf-8"))
        packet_ok = not packet_shape_errors(packet)
        packet_checks.append(packet_ok)
        for artifact in entry["artifacts"]:
            artifact_path = OUTPUT_ROOT / artifact["locator"]
            no_3d_files = no_3d_files and artifact_path.suffix.lower() not in {".fbx", ".blend", ".obj", ".glb", ".gltf"}
            if not artifact_path.is_file():
                artifact_ok = False
                continue
            raw = artifact_path.read_bytes()
            artifact_ok = artifact_ok and len(raw) == artifact["byteLength"] and hashlib.sha256(raw).hexdigest() == artifact["sha256"]
            if artifact_path.suffix.lower() == ".png":
                with Image.open(artifact_path) as image:
                    artifact_ok = artifact_ok and list(image.size) == artifact["pixelDimensions"]
    for artifact in manifest["reviewArtifacts"]:
        artifact_path = OUTPUT_ROOT / artifact["locator"]
        if not artifact_path.is_file():
            artifact_ok = False
            continue
        raw = artifact_path.read_bytes()
        artifact_ok = artifact_ok and len(raw) == artifact["byteLength"] and hashlib.sha256(raw).hexdigest() == artifact["sha256"]
        if artifact_path.suffix.lower() == ".png":
            with Image.open(artifact_path) as image:
                artifact_ok = artifact_ok and list(image.size) == artifact["pixelDimensions"]
    all_interior_ids = {record["familyId"] for record in coverage["families"] if record["packetId"] == "architecture_stonehold_exterior_interior_floorplan_v001"}
    all_prop_ids = {record["familyId"] for record in coverage["families"] if record["packetId"] == "prop_stonehold_interior_decor_v001"}
    checks["allPacketsComplete"] = all(packet_checks) and len(packet_checks) == 27
    checks["interiorModules21of21"] = set(manifest["interiorModuleCoverage"]) == all_interior_ids and len(all_interior_ids) == 21
    checks["propFamilies65Accounted"] = set(manifest["propFamilyCoverage"]["mapped"]) | set(manifest["propFamilyCoverage"]["excluded"]) == all_prop_ids and len(all_prop_ids) == 65
    checks["artifactsHashAndDimensions"] = artifact_ok
    checks["no3dFiles"] = no_3d_files and not any(path.suffix.lower() in {".fbx", ".blend", ".obj", ".glb", ".gltf"} for path in OUTPUT_ROOT.rglob("*"))
    review_locators = {item["locator"] for item in manifest["reviewArtifacts"]}
    checks["reviewArtifactsPresent"] = (
        len(manifest["reviewArtifacts"]) == 7
        and "review/README.md" in review_locators
        and "index.html" in review_locators
        and len([item for item in review_locators if item.endswith(".png")]) == 5
        and all((OUTPUT_ROOT / item["locator"]).is_file() for item in manifest["reviewArtifacts"])
    )
    passed = all(checks.values())
    report = {
        "schema": "anotherlife.stonehold-enterable-structure-packet-validation.v1",
        "status": "passed" if passed else "failed",
        "packetSetId": PACKET_SET_ID,
        "counts": {"taxonomyFamilies": len(expected), "enterablePackets": len(manifest["packets"]), "sharedModuleFamilies": len(SHARED_SUPPORT_FAMILIES), "interiorModuleFamilies": len(all_interior_ids), "propDecorFamilies": len(all_prop_ids), "artifactRecords": sum(len(item["artifacts"]) for item in manifest["packets"]) + len(manifest["reviewArtifacts"])},
        "checks": checks,
    }
    write_json(OUTPUT_ROOT / "validation_report_v001.json", report)
    if not passed:
        failed = [key for key, value in checks.items() if not value]
        raise ValueError("Stonehold packet validation failed: " + ", ".join(failed))
    return report


def write_readme(manifest: dict[str, Any], report: dict[str, Any]) -> None:
    rows = []
    for index, entry in enumerate(manifest["packets"], 1):
        slug = entry["packetId"].removeprefix("stonehold_").removesuffix("_2d_handoff_v001")
        rows.append(f"| {index:02d} | `{entry['taxonomyId']}` | [{slug}](packets/{slug}/{slug}_exterior_handoff_v001.png) | {entry['physicalLevelCount']} | PENDING |")
    content = f"""# Stonehold Enterable Structure 2D Handoff Packets v001

Status: **PENDING OWNER REVIEW**. Return `APPROVE`, `REVISE`, or `REJECT` for each of the 27 packets before any Meshy, Blender, Unity-prefab, or other 3D production begins.

## Scope and evidence

- Enterable structure packets: **{manifest['enterablePacketCount']} / 27**.
- Taxonomy accounting: **{report['counts']['taxonomyFamilies']} / 40** across the two approved Stonehold architecture concept packets.
- Shared door/gate/traversal families: **{manifest['sharedModuleFamilyCount']} / 11**.
- Interior room-module families: **{report['counts']['interiorModuleFamilies']} / 21**.
- Prop/decor families accounted: **{report['counts']['propDecorFamilies']} / 65**; the event-only banner is explicitly excluded from permanent Stonehold furnishing.
- Artifact records verified: **{report['counts']['artifactRecords']}**.
- New 3D jobs submitted: **0**.

Open `review/README.md` for the complete GitHub-rendered review register, `index.html` for the local dashboard, or the five PNG files under `review/` for compact contact sheets. Every structure folder contains one exterior sheet, one every-floor/section sheet, and one machine-readable packet.

## Binding decisions already fixed

1. Every applicable Stonehold building/structure is genuinely enterable and end-to-end traversable with developed furnished rooms. No facade-only or solid-shell substitute is allowed.
2. Main castle/fortress/fort/city perimeter walls are non-enterable and impassable. Defendable walltops and designated routes are separate traversal surfaces.
3. Every gate, door, window frame, glass group and shutter is a separate object. An intact main gate never becomes a physical opening through the wall.
4. Intact main-gate paired-anchor teleport is referenced only. Hostile break is referenced only under `t_c8ea885d`.
5. Small interiors are seamless. Large combat interiors use separate asynchronously loaded scenes behind physical portals with loading-cover, occlusion, NavMesh and unload boundaries.
6. Runtime VFX stay separate from clean architecture geometry.
7. Author on the approved `0.5 m` sub-grid; combine static pieces by room/exterior visibility cell and shared atlas for runtime.

## Review register

| # | Taxonomy family | Exterior packet | Physical levels | Owner decision |
| ---: | --- | --- | ---: | --- |
{chr(10).join(rows)}

For each row, review the linked exterior sheet, the adjacent interior sheet in the same folder, and the five explicit decision gates in the JSON. The packet remains non-authoritative for 3D until its owner decision is recorded.

## Authority and exclusions

The approved PR #664 civic-hall and fort-gatehouse plans remain shared spatial/module authority. The Stonehold sheets here add realm-specific recommendations and all-family coverage but do not silently alter the locked civic-hall envelope. The four-realm boards are directional only. Slagfall Quarry is not architecture-visual authorization. Religious/royal symbols, localized text, heraldry and exact ritual meaning remain narrative/owner gated.

Rollback is deletion or one squash revert. This package is documentation and source-art evidence only; it changes no runtime catalog, save schema, scene, prefab, gameplay, balance, or release configuration.
"""
    (OUTPUT_ROOT / "README.md").write_text(content, encoding="utf-8", newline="\n")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args()
    if args.validate_only:
        report = validate()
        print(json.dumps(report, indent=2))
        return
    manifest = build()
    report = validate(manifest)
    write_readme(manifest, report)
    print(json.dumps({"packetSetId": PACKET_SET_ID, "packets": len(manifest["packets"]), "status": report["status"], "reviewArtifacts": len(manifest["reviewArtifacts"]), "output": str(OUTPUT_ROOT)}))


if __name__ == "__main__":
    main()
