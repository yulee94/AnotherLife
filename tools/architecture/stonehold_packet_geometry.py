"""Packet-specific 2D architecture geometry for Stonehold handoff sheets.

All coordinates are meters in local plan space: +X east, +Z north, front is south.
The data is authoring evidence only and intentionally has no runtime/3D behavior.
"""

from __future__ import annotations

import math
from typing import Any


# Each taxonomy family has an explicit architectural archetype, approach, section
# placement and vertical-circulation decision. Repeated helper primitives are the
# intended modular language; the profile combination and resulting plan are unique.
LAYOUT_PROFILES: dict[str, dict[str, Any]] = {
    "city_capital_kit": {"archetype": "four enterable blocks around a service court", "patterns": ["service_grid", "courtyard_four", "gallery_ring"], "entry": "south", "core": (0.82, 0.72), "lift": "public_platform", "court": True, "masses": 4},
    "settlement_village_kit": {"archetype": "three detached shells around a work yard", "patterns": ["detached_three", "detached_lofts"], "entry": "south", "core": (0.78, 0.68), "lift": "service_platform", "yard": True, "masses": 3},
    "dwelling": {"archetype": "domestic two-bay house with rear service stoop", "patterns": ["domestic_front_back", "private_bedrooms"], "entry": "south", "core": (0.78, 0.62), "lift": None, "masses": 1},
    "ruin_structure": {"archetype": "broken service hall with isolated collapse zones", "patterns": ["cellar_ramp", "broken_hall"], "entry": "west", "core": (0.24, 0.62), "ramp": True, "masses": 1},
    "well_fountain_cistern": {"archetype": "square headhouse over ringed maintenance cistern", "patterns": ["cistern_ring", "pump_hall"], "entry": "south", "core": (0.76, 0.70), "ramp": True, "masses": 1},
    "academy": {"archetype": "paired teaching wings on an archive spine", "patterns": ["twin_wing", "archive_gallery"], "entry": "south", "core": (0.86, 0.52), "lift": "public_platform", "masses": 2},
    "barracks": {"archetype": "muster hall with armory wing and rear ready egress", "patterns": ["muster_spine", "bunk_crossbar"], "entry": "south", "core": (0.84, 0.58), "lift": "public_platform", "masses": 1},
    "embassy": {"archetype": "formal public front with offset secure service mass", "patterns": ["public_private", "diplomatic_suite"], "entry": "south", "core": (0.82, 0.55), "lift": "public_platform", "masses": 2},
    "farm": {"archetype": "cart-through processing barn with side service cells", "patterns": ["barn_side_aisle", "store_loft"], "entry": "south", "core": (0.82, 0.68), "lift": "goods_platform", "yard": True, "masses": 1},
    "forge": {"archetype": "hot-work bay split from public counter and quench court", "patterns": ["hot_cold_bays", "pattern_mezzanine"], "entry": "south", "core": (0.84, 0.70), "lift": "goods_platform", "court": True, "masses": 2},
    "gold_mine": {"archetype": "assay wing on a portal-to-headframe working axis", "patterns": ["portal_staging", "portal_axis", "winch_gallery"], "entry": "south", "core": (0.77, 0.55), "lift": "industrial_lift", "masses": 2},
    "lumber_mill": {"archetype": "saw hall beside an open loading canopy", "patterns": ["saw_canopy", "pattern_mezzanine"], "entry": "east", "core": (0.80, 0.65), "lift": "goods_platform", "yard": True, "masses": 2},
    "market": {"archetype": "perimeter shop cells around a covered trading hall", "patterns": ["market_ring", "merchant_gallery"], "entry": "south", "core": (0.88, 0.62), "lift": "goods_platform", "masses": 1},
    "quarry": {"archetype": "stepped cutting works with overlook and lower service", "patterns": ["stepped_service", "cutting_hall", "overlook_gallery"], "entry": "east", "core": (0.78, 0.60), "lift": "industrial_lift", "yard": True, "masses": 2},
    "stable": {"archetype": "through-aisle stable with tack and feed side rooms", "patterns": ["stable_aisle", "hay_loft"], "entry": "south", "core": (0.84, 0.68), "lift": "goods_platform", "yard": True, "masses": 1},
    "storehouse": {"archetype": "secure cells arranged on a through-loading spine", "patterns": ["loading_spine", "guarded_store"], "entry": "south", "core": (0.82, 0.68), "lift": "goods_platform", "masses": 1},
    "town_hall": {"archetype": "owner-approved civic hall and open gallery", "patterns": ["approved_civic_ground", "approved_civic_upper"], "entry": "south", "core": (0.82, 0.60), "lift": "public_platform", "masses": 1},
    "watchtower": {"archetype": "tapered defensive tower around a compact protected core", "patterns": ["tower_quadrants", "tower_quadrants", "tower_crown"], "entry": "south", "core": (0.50, 0.50), "lift": "quest_platform", "masses": 1},
    "workshop": {"archetype": "service counter beside a rear-load work bay", "patterns": ["workshop_bay", "pattern_mezzanine"], "entry": "south", "core": (0.82, 0.66), "lift": "goods_platform", "masses": 1},
    "castle_enterable": {"archetype": "keep and tower masses around an open inner court", "patterns": ["keep_undercroft", "keep_courtyard", "keep_upper", "tower_roof"], "entry": "south", "core": (0.72, 0.55), "lift": "public_lift", "court": True, "perimeter": True, "masses": 5},
    "fortress_enterable": {"archetype": "three enterable buildings around a capture court", "patterns": ["fort_undercroft", "fort_courtyard", "fort_upper", "tower_roof"], "entry": "south", "core": (0.76, 0.58), "lift": "combat_lift", "court": True, "perimeter": True, "masses": 3},
    "guardpost_watch": {"archetype": "inspection front split from rear guard-ready room", "patterns": ["inspection_split", "watch_loft"], "entry": "south", "core": (0.78, 0.60), "lift": "duty_platform", "masses": 1},
    "inn_tavern": {"archetype": "public common hall with isolated kitchen/service route", "patterns": ["cellar_service", "tavern_public_service", "guest_corridor"], "entry": "south", "core": (0.82, 0.62), "lift": "guest_lift", "masses": 2},
    "mill_wind_water": {"archetype": "dry mill house beside guarded wheel channel", "patterns": ["wheel_service", "milling_axis", "grain_loft"], "entry": "east", "core": (0.78, 0.62), "lift": "goods_platform", "masses": 2},
    "religious_cultural_structure": {"archetype": "assembly hall and archive around a quiet court", "patterns": ["memory_hall_court", "archive_daylight"], "entry": "south", "core": (0.84, 0.60), "lift": "public_platform", "court": True, "masses": 2},
    "shop_service": {"archetype": "recessed shop front with independent rear workshop", "patterns": ["shop_front_back", "living_over_shop"], "entry": "south", "core": (0.80, 0.60), "lift": None, "masses": 1},
    "warehouse_barn": {"archetype": "clear-span loading hall with secure rear cells", "patterns": ["clear_span", "guard_mezzanine"], "entry": "south", "core": (0.86, 0.65), "lift": "goods_platform", "yard": True, "masses": 1},
}


def rect(identifier: str, x: float, z: float, width: float, depth: float, kind: str = "building") -> dict[str, Any]:
    return {"id": identifier, "x": round(x, 3), "z": round(z, 3), "width": round(width, 3), "depth": round(depth, 3), "kind": kind}


def room_score(room_id: str) -> float:
    if any(token in room_id for token in ("hall", "court", "floor", "muster", "loading", "gallery", "common", "processing", "market", "water_chamber", "living_hearth", "assembly", "barracks")):
        return 2.2
    if any(token in room_id for token in ("store", "office", "landing", "service", "archive", "pantry", "wash", "desk", "station", "records")):
        return 0.9
    return 1.3


def split_strip(room_ids: list[str], area: dict[str, Any], horizontal: bool = True) -> list[dict[str, Any]]:
    if not room_ids:
        return []
    weights = [room_score(item) for item in room_ids]
    total = sum(weights)
    records = []
    cursor = area["x"] if horizontal else area["z"]
    for room_id, weight in zip(room_ids, weights):
        span = (area["width"] if horizontal else area["depth"]) * weight / total
        if horizontal:
            records.append(rect(room_id, cursor, area["z"], span, area["depth"], "room"))
        else:
            records.append(rect(room_id, area["x"], cursor, area["width"], span, "room"))
        cursor += span
    return records


def grid_rooms(room_ids: list[str], area: dict[str, Any], columns: int) -> list[dict[str, Any]]:
    columns = max(1, min(columns, len(room_ids)))
    rows = math.ceil(len(room_ids) / columns)
    result = []
    for row_index in range(rows):
        row_ids = room_ids[row_index * columns:(row_index + 1) * columns]
        row_depth = area["depth"] / rows
        row_area = rect("row", area["x"], area["z"] + row_index * row_depth, area["width"], row_depth)
        result.extend(split_strip(row_ids, row_area, horizontal=True))
    return result


def choose_primary(room_ids: list[str]) -> str:
    return max(room_ids, key=lambda item: (room_score(item), -room_ids.index(item)))


def footprint_set(profile: dict[str, Any], width: float, depth: float) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    wall = 0.3
    inner_w, inner_d = width - wall * 2, depth - wall * 2
    masses = profile.get("masses", 1)
    voids: list[dict[str, Any]] = []
    if profile.get("court"):
        court_w, court_d = width * 0.28, depth * 0.28
        cx, cz = (width - court_w) / 2, (depth - court_d) / 2
        voids.append(rect("open_courtyard", cx, cz, court_w, court_d, "open_to_sky"))
        footprints = [
            rect("south_wing", wall, wall, inner_w, max(2.2, cz - wall)),
            rect("north_wing", wall, cz + court_d, inner_w, max(2.2, depth - wall - (cz + court_d))),
            rect("west_wing", wall, cz, max(2.2, cx - wall), court_d),
            rect("east_wing", cx + court_w, cz, max(2.2, width - wall - (cx + court_w)), court_d),
        ]
    elif masses == 3:
        gap = width * 0.055
        shell_w = (inner_w - 2 * gap) / 3
        footprints = [rect(f"shell_{i + 1}", wall + i * (shell_w + gap), wall, shell_w, inner_d) for i in range(3)]
        voids.append(rect("shared_work_yard", wall + shell_w, depth * 0.38, shell_w + 2 * gap, depth * 0.25, "exterior_yard"))
    elif masses == 2:
        gap = width * 0.06
        left_w = inner_w * 0.60
        footprints = [rect("primary_mass", wall, wall, left_w, inner_d), rect("offset_service_mass", wall + left_w + gap, depth * 0.18, inner_w - left_w - gap, depth * 0.64)]
    elif masses >= 4:
        footprints = [rect("central_keep", width * 0.20, depth * 0.16, width * 0.60, depth * 0.68)]
        tower = min(width, depth) * 0.16
        footprints.extend([
            rect("tower_sw", wall, wall, tower, tower), rect("tower_se", width - wall - tower, wall, tower, tower),
            rect("tower_nw", wall, depth - wall - tower, tower, tower), rect("tower_ne", width - wall - tower, depth - wall - tower, tower, tower),
        ])
    else:
        footprints = [rect("main_shell", wall, wall, inner_w, inner_d)]
    return footprints, voids


def place_rooms(pattern: str, room_ids: list[str], width: float, depth: float, footprints: list[dict[str, Any]], voids: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], dict[str, Any], list[list[float]]]:
    wall = 0.3
    interior = rect("interior", wall, wall, width - 2 * wall, depth - 2 * wall)
    primary = choose_primary(room_ids)
    others = [item for item in room_ids if item != primary]
    corridor_points: list[list[float]]

    if pattern in {"detached_three", "detached_lofts"}:
        groups = [[], [], []]
        for index, room_id in enumerate(room_ids):
            groups[index % 3].append(room_id)
        rooms = []
        for shell, ids in zip(footprints[:3], groups):
            rooms.extend(split_strip(ids, shell, horizontal=False))
        corridor_points = [[wall, depth * 0.50], [width - wall, depth * 0.50]]
    elif pattern in {"tower_quadrants", "tower_crown"}:
        core_gap = min(width, depth) * 0.28
        cx, cz = width / 2, depth / 2
        zones = [
            rect("sw", wall, wall, cx - core_gap / 2 - wall, cz - core_gap / 2 - wall),
            rect("se", cx + core_gap / 2, wall, width - wall - cx - core_gap / 2, cz - core_gap / 2 - wall),
            rect("nw", wall, cz + core_gap / 2, cx - core_gap / 2 - wall, depth - wall - cz - core_gap / 2),
            rect("ne", cx + core_gap / 2, cz + core_gap / 2, width - wall - cx - core_gap / 2, depth - wall - cz - core_gap / 2),
        ]
        rooms = []
        for index, room_id in enumerate(room_ids):
            rooms.extend(split_strip([room_id], zones[index % 4]))
        corridor_points = [[width / 2, wall], [width / 2, depth - wall]]
    elif pattern in {"courtyard_four", "keep_courtyard", "fort_courtyard", "memory_hall_court", "market_ring", "gallery_ring"}:
        court = voids[0] if voids else rect("court", width * 0.36, depth * 0.36, width * 0.28, depth * 0.28)
        zones = [
            rect("south", wall, wall, width - 2 * wall, court["z"] - wall),
            rect("north", wall, court["z"] + court["depth"], width - 2 * wall, depth - wall - court["z"] - court["depth"]),
            rect("west", wall, court["z"], court["x"] - wall, court["depth"]),
            rect("east", court["x"] + court["width"], court["z"], width - wall - court["x"] - court["width"], court["depth"]),
        ]
        rooms = []
        for index, room_id in enumerate(room_ids):
            zone = zones[index % len(zones)]
            same_zone = [room_ids[j] for j in range(index % len(zones), len(room_ids), len(zones))]
            local = same_zone.index(room_id)
            slices = split_strip(same_zone, zone, horizontal=index % 2 == 0)
            rooms.append(slices[local])
        corridor_points = [[wall, court["z"]], [court["x"], court["z"]], [court["x"], court["z"] + court["depth"]], [court["x"] + court["width"], court["z"] + court["depth"]], [court["x"] + court["width"], court["z"]], [width - wall, court["z"]]]
    elif pattern in {"domestic_front_back", "private_bedrooms", "public_private", "diplomatic_suite", "shop_front_back", "living_over_shop", "tavern_public_service", "guest_corridor", "inspection_split"}:
        front_count = max(1, math.ceil(len(room_ids) * 0.55))
        front = rect("front_zone", wall, wall, width - 2 * wall, depth * 0.56 - wall)
        rear = rect("rear_zone", wall, depth * 0.56, width - 2 * wall, depth - wall - depth * 0.56)
        rooms = split_strip(room_ids[:front_count], front) + split_strip(room_ids[front_count:], rear)
        corridor_points = [[width / 2, wall], [width / 2, depth - wall]]
    elif pattern in {"portal_axis", "portal_staging", "keep_undercroft", "fort_undercroft", "cellar_service", "cellar_ramp", "cistern_ring", "wheel_service", "stepped_service"}:
        portal_candidates = [item for item in room_ids if any(token in item for token in ("portal", "airlock", "loading_cover", "tunnel", "water_chamber", "wheel_service"))]
        terminal = portal_candidates[0] if portal_candidates else primary
        side_ids = [item for item in room_ids if item != terminal]
        terminal_area = rect("terminal", width * 0.25, depth * 0.58, width * 0.50, depth * 0.34)
        side_area = rect("service", wall, wall, width - 2 * wall, depth * 0.50)
        rooms = split_strip(side_ids, side_area) + split_strip([terminal], terminal_area)
        corridor_points = [[width / 2, wall], [width / 2, depth * 0.83]]
    elif pattern in {"stable_aisle", "loading_spine", "muster_spine", "milling_axis", "clear_span", "barn_side_aisle", "saw_canopy"}:
        aisle_w = max(1.8, width * 0.18)
        left = rect("west_cells", wall, wall, (width - aisle_w) / 2 - wall, depth - 2 * wall)
        right = rect("east_cells", (width + aisle_w) / 2, wall, (width - aisle_w) / 2 - wall, depth - 2 * wall)
        split = math.ceil(len(room_ids) / 2)
        rooms = split_strip(room_ids[:split], left, horizontal=False) + split_strip(room_ids[split:], right, horizontal=False)
        corridor_points = [[width / 2, wall], [width / 2, depth - wall]]
    elif pattern in {"hot_cold_bays", "workshop_bay", "cutting_hall", "pump_hall", "twin_wing", "archive_gallery", "memory_hall_court"}:
        main_area = rect("main_bay", wall, wall, width * 0.62 - wall, depth - 2 * wall)
        support = rect("support", width * 0.62, wall, width - wall - width * 0.62, depth - 2 * wall)
        rooms = split_strip([primary], main_area) + split_strip(others, support, horizontal=False)
        corridor_points = [[wall, depth * 0.52], [width - wall, depth * 0.52]]
    else:
        # Packet profiles deliberately select this only for upper galleries/stores.
        columns = 3 if width >= 16 else 2
        rooms = grid_rooms(room_ids, interior, columns)
        corridor_points = [[wall, depth * 0.52], [width - wall, depth * 0.52]]

    for room in rooms:
        room["purpose"] = room["id"].replace("_", " ")
    min_x = min(point[0] for point in corridor_points)
    max_x = max(point[0] for point in corridor_points)
    min_z = min(point[1] for point in corridor_points)
    max_z = max(point[1] for point in corridor_points)
    circulation = rect("primary_circulation", min_x, min_z, max(1.5, max_x - min_x), max(1.5, max_z - min_z), "accessible_route_envelope")
    return rooms, circulation, corridor_points


def furniture_kind(room_id: str) -> tuple[str, str, int]:
    rules = [
        (("bed", "bunk", "guest", "living", "rest"), "bed_or_cot", "sleeping furniture", 2),
        (("kitchen", "pantry", "food", "drink"), "prep_table", "prep table + storage", 2),
        (("archive", "record", "library", "drawing", "pattern"), "shelf_bay", "archive shelving", 3),
        (("store", "stock", "hold", "cellar"), "storage_rack", "racks / crates", 3),
        (("forge", "hearth", "quench", "workfloor", "workroom", "workshop"), "work_station", "hearth / workbench", 2),
        (("market", "shop", "counter", "assay"), "service_counter", "counter + display", 2),
        (("office", "desk", "warden", "foreman", "clerk", "sergeant"), "desk", "desk + chair", 2),
        (("muster", "guard", "armory", "weapon", "barracks"), "weapon_rack", "weapon rack + bench", 2),
        (("stable", "stall", "feed", "hay"), "stall_partition", "stall / feed bay", 3),
        (("mill", "grain", "wheel", "machine", "winch", "pump", "ore", "cutting", "saw"), "machine_bed", "guarded machine / material bay", 2),
        (("detention", "cell", "prison"), "secure_cot", "cot + secure partition", 2),
        (("hall", "court", "gallery", "common", "assembly", "teaching", "petition", "reception"), "table_bench", "table / benches", 3),
    ]
    for tokens, kind, label, count in rules:
        if any(token in room_id for token in tokens):
            return kind, label, count
    return "utility_table", "task table + wall storage", 2


def furnish_room(room: dict[str, Any], floor_id: str) -> dict[str, Any]:
    kind, label, count = furniture_kind(room["id"])
    pad = min(0.45, room["width"] * 0.12, room["depth"] * 0.12)
    usable_w = max(0.35, room["width"] - 2 * pad)
    usable_d = max(0.35, room["depth"] - 2 * pad)
    item_w = max(0.35, min(1.8, usable_w / max(2, count)))
    item_d = max(0.30, min(0.9, usable_d * 0.24))
    footprints = []
    for index in range(count):
        x = room["x"] + pad + (usable_w - item_w) * (index / max(1, count - 1))
        z = room["z"] + pad if index % 2 == 0 else room["z"] + room["depth"] - pad - item_d
        footprints.append({
            "id": f"{floor_id}_{room['id']}_{kind}_{index + 1}", "kind": kind, "label": label,
            "x": round(x, 3), "z": round(z, 3), "width": round(item_w, 3), "depth": round(item_d, 3),
            "orientationDegrees": 0 if index % 2 == 0 else 180, "count": 1,
        })
    aisle_d = min(1.2, room["depth"])
    aisle = rect(f"{room['id']}_protected_aisle", room["x"], room["z"] + (room["depth"] - aisle_d) / 2, room["width"], aisle_d, "minimum_1_2m_aisle")
    approach_w = min(1.2, room["width"])
    approach = rect(f"{room['id']}_door_approach", room["x"] + (room["width"] - approach_w) / 2, room["z"], approach_w, min(1.2, room["depth"]), "door_swing_clearance")
    return {"roomId": room["id"], "layoutIntent": label, "footprints": footprints, "protectedClearances": [aisle, approach]}


def side_point(side: str, width: float, depth: float, offset: float = 0.5) -> tuple[float, float, str]:
    if side == "north":
        return width * offset, depth, "horizontal"
    if side == "east":
        return width, depth * offset, "vertical"
    if side == "west":
        return 0.0, depth * offset, "vertical"
    return width * offset, 0.0, "horizontal"


def vertical_circulation(packet: dict[str, Any], profile: dict[str, Any]) -> list[dict[str, Any]]:
    width, depth = packet["envelopeMeters"]["width"], packet["envelopeMeters"]["depth"]
    levels = packet["levels"]
    level_ids = [level["id"] for level in levels]
    cx, cz = profile["core"]
    stair_w = min(2.0, max(1.5, width * 0.12))
    stair_d = min(4.2, max(2.8, depth * 0.28))
    x = min(width - stair_w - 0.3, max(0.3, width * cx - stair_w / 2))
    z = min(depth - stair_d - 0.3, max(0.3, depth * cz - stair_d / 2))
    cores = [{
        "id": f"{packet['slug']}_stair_core", "type": "u_stair" if len(levels) > 2 else "dogleg_stair",
        "footprint": rect("stair_footprint", x, z, stair_w, stair_d, "vertical_circulation"),
        "clearWidthMeters": 1.5, "direction": "UP clockwise", "connectsLevels": level_ids,
        "landings": [{"levelId": level["id"], "elevationMeters": level["elevationMeters"], "depthMeters": 1.5} for level in levels],
        "accessible": False, "separateObjects": True,
    }]
    if profile.get("lift"):
        lift_w = min(2.4, max(1.8, width * 0.12))
        lift_d = min(2.4, max(1.8, depth * 0.16))
        lx = max(0.3, x - lift_w - 0.4)
        cores.append({
            "id": f"{packet['slug']}_{profile['lift']}", "type": profile["lift"],
            "footprint": rect("lift_footprint", lx, z, lift_w, lift_d, "vertical_circulation"),
            "clearWidthMeters": 1.5, "direction": "vertical platform", "connectsLevels": level_ids,
            "landings": [{"levelId": level["id"], "elevationMeters": level["elevationMeters"], "depthMeters": 1.5} for level in levels],
            "accessible": "goods" not in profile["lift"], "separateObjects": True,
        })
    if profile.get("ramp"):
        ramp_w = max(1.5, min(2.0, width * 0.18))
        cores.append({
            "id": f"{packet['slug']}_switchback_ramp", "type": "switchback_ramp_1_to_12",
            "footprint": rect("ramp_footprint", 0.5, depth * 0.18, ramp_w, min(depth * 0.62, 8.0), "vertical_circulation"),
            "clearWidthMeters": 1.5, "direction": "UP with 1.5 m intermediate landing", "connectsLevels": level_ids,
            "landings": [{"levelId": level["id"], "elevationMeters": level["elevationMeters"], "depthMeters": 1.5} for level in levels],
            "accessible": True, "separateObjects": True,
        })
    return cores


def build_level(packet: dict[str, Any], level: dict[str, Any], index: int, profile: dict[str, Any], cores: list[dict[str, Any]], approved: dict[str, Any] | None) -> dict[str, Any]:
    width, depth = packet["envelopeMeters"]["width"], packet["envelopeMeters"]["depth"]
    pattern = profile["patterns"][index]
    footprints, voids = footprint_set(profile, width, depth)
    source_authority = "packet-specific measured blueprint"
    if approved is not None and packet["slug"] == "town_hall":
        key = "groundFloor" if level["id"] == "ground" else "upperFloor"
        rooms = []
        for source_room in approved[key]:
            room = {"id": source_room["id"], "purpose": source_room["id"].replace("_", " "), "x": source_room["x"], "z": source_room["z"], "width": source_room["width"], "depth": source_room["depth"], "kind": "room"}
            if "void" in source_room:
                room["void"] = source_room["void"]
                voids.append({"id": "approved_open_gallery_void", **source_room["void"], "kind": "open_to_below"})
            rooms.append(room)
        circulation = rect("approved_primary_circulation", 2.1, 2.2, 5.3, 1.2, "accessible_route_envelope")
        route_points = [[4.75, 0.0], [4.75, 7.9]]
        source_authority = "PR #664 shared_civic_hall_layout_v001.json coordinates retained exactly"
    else:
        rooms, circulation, route_points = place_rooms(pattern, list(level["rooms"]), width, depth, footprints, voids)

    floor_id = level["id"]
    ground_like = index == min(range(len(packet["levels"])), key=lambda i: abs(packet["levels"][i]["elevationMeters"]))
    entries = []
    doors = []
    if ground_like:
        entry_target = next((room["id"] for room in rooms if any(token in room["id"] for token in ("entry", "public", "muster", "hall", "inspection", "shop", "processing", "cutting", "market", "aisle", "floor"))), rooms[0]["id"])
        ex, ez, orientation = side_point(profile["entry"], width, depth)
        entry = {"id": f"{packet['slug']}_main_entry", "from": "outside", "to": entry_target, "side": profile["entry"], "x": round(ex, 3), "z": round(ez, 3), "orientation": orientation, "clearOpeningMeters": [2.5, 3.0], "swing": "double_out", "stepFree": True, "separateObject": True}
        entries.append(entry)
        doors.append({**entry, "between": ["outside", entry_target]})
        service_target = next((room["id"] for room in rooms if any(token in room["id"] for token in ("service", "rear", "yard", "loading", "dry_entry", "cart", "tool"))), None)
        if service_target and service_target != entry_target:
            opposite = {"south": "north", "north": "south", "east": "west", "west": "east"}[profile["entry"]]
            sx, sz, sorientation = side_point(opposite, width, depth, 0.78)
            service = {"id": f"{packet['slug']}_service_entry", "from": "outside", "to": service_target, "side": opposite, "x": round(sx, 3), "z": round(sz, 3), "orientation": sorientation, "clearOpeningMeters": [1.4, 2.4], "swing": "out", "stepFree": True, "separateObject": True}
            entries.append(service)
            doors.append({**service, "between": ["outside", service_target]})

    if approved is not None and packet["slug"] == "town_hall":
        approved_floor = "ground" if floor_id == "ground" else "upper"
        doors.extend({
            "id": f"approved_{approved_floor}_door_{door_index:02d}", "separateObject": True,
            "between": source_door["between"], "to": source_door["between"][1],
            "orientation": source_door["orientation"], "x": source_door["x"], "z": source_door["z"],
            "clearOpeningMeters": [source_door["width"], 2.4], "swing": "in",
        } for door_index, source_door in enumerate((item for item in approved["doorOpenings"] if item["floor"] == approved_floor), 1))
    else:
        for room in rooms:
            doors.append({
                "id": f"door_{floor_id}_{room['id']}", "separateObject": True,
                "between": [room["id"], "primary_circulation"], "to": room["id"],
                "orientation": "horizontal" if room["z"] > depth / 2 else "vertical",
                "x": round(room["x"] + room["width"] / 2, 3), "z": round(room["z"] + (0 if room["z"] > depth / 2 else room["depth"]), 3),
                "clearOpeningMeters": [1.2, 2.4], "swing": "in",
            })
    windows = [] if level["elevationMeters"] < 0 else [
        {"id": f"window_{floor_id}_{room['id']}", "separateFrameGlassShutter": True, "serves": room["id"], "side": "north" if i % 3 == 0 else ("east" if i % 3 == 1 else "west"), "x": round(room["x"] + room["width"] / 2, 3), "z": round(room["z"] + room["depth"] / 2, 3), "openingMeters": [1.2, 1.5]}
        for i, room in enumerate(rooms)
    ]
    core_shapes = [core["footprint"] for core in cores if floor_id in core["connectsLevels"]]

    def overlaps(left: dict[str, Any], right: dict[str, Any]) -> bool:
        return not (
            left["x"] + left["width"] <= right["x"]
            or right["x"] + right["width"] <= left["x"]
            or left["z"] + left["depth"] <= right["z"]
            or right["z"] + right["depth"] <= left["z"]
        )

    furnishing = [furnish_room(room, floor_id) for room in rooms]
    for room_layout in furnishing:
        unobstructed = [item for item in room_layout["footprints"] if not any(overlaps(item, core) for core in core_shapes)]
        if unobstructed:
            room_layout["footprints"] = unobstructed
    clearances = [rect(f"{floor_id}_accessible_route", circulation["x"], circulation["z"], circulation["width"], circulation["depth"], "primary_accessible_route")]
    largest = max(rooms, key=lambda room: room["width"] * room["depth"])
    diameter = min(packet["clearancesMeters"]["combatClearDiameter"], largest["width"], largest["depth"])
    clearances.append({"id": f"{floor_id}_combat_clear", "kind": "combat_diameter", "roomId": largest["id"], "center": [round(largest["x"] + largest["width"] / 2, 3), round(largest["z"] + largest["depth"] / 2, 3)], "diameter": round(diameter, 3)})
    clearances.append({"id": f"{floor_id}_camera_backoff", "kind": "camera_backoff", "roomId": largest["id"], "center": [round(largest["x"] + largest["width"] / 2, 3), round(largest["z"] + largest["depth"] / 2, 3)], "radius": packet["clearancesMeters"]["cameraBackoff"]})
    socket_placements = []
    for room in rooms:
        cx, cz = round(room["x"] + room["width"] / 2, 3), round(room["z"] + room["depth"] / 2, 3)
        socket_placements.extend([
            {"id": f"light_{floor_id}_{room['id']}", "type": "lighting", "roomId": room["id"], "x": cx, "z": cz},
            {"id": f"interaction_{floor_id}_{room['id']}", "type": "interaction", "roomId": room["id"], "x": round(room["x"] + min(0.6, room["width"] / 3), 3), "z": round(room["z"] + min(0.6, room["depth"] / 3), 3)},
        ])
    core_footprints = []
    for core in cores:
        if floor_id not in core["connectsLevels"]:
            continue
        footprint = core["footprint"]
        core_center = (footprint["x"] + footprint["width"] / 2, footprint["z"] + footprint["depth"] / 2)
        containing_room = next((room["id"] for room in rooms if room["x"] <= core_center[0] <= room["x"] + room["width"] and room["z"] <= core_center[1] <= room["z"] + room["depth"]), rooms[-1]["id"])
        core_footprints.append({"coreId": core["id"], "type": core["type"], "footprint": footprint, "landingDepthMeters": 1.5, "direction": core["direction"], "locatedInRoomId": containing_room})
    primary_core = core_shapes[0]
    longitudinal_z = round(primary_core["z"] + primary_core["depth"] / 2, 3)
    cross_x = round(primary_core["x"] + primary_core["width"] / 2, 3)
    section_lines = [
        {"id": "A-A", "orientation": "longitudinal", "from": [0.3, longitudinal_z], "to": [round(width - 0.3, 3), longitudinal_z], "cutsVerticalCoreId": cores[0]["id"]},
        {"id": "B-B", "orientation": "cross", "from": [cross_x, 0.3], "to": [cross_x, round(depth - 0.3, 3)], "cutsVerticalCoreId": cores[0]["id"]},
    ]
    circulation_routes = [{"id": "primary_accessible_route", "widthMeters": packet["clearancesMeters"]["primaryCirculationWidth"], "accessible": True, "points": route_points}]
    if entries:
        entry_point = [entries[0]["x"], entries[0]["z"]]
        if math.dist(entry_point, route_points[-1]) < math.dist(entry_point, route_points[0]):
            route_points = list(reversed(route_points))
        circulation_routes[0]["points"] = [entry_point, *route_points]
        if len(entries) > 1:
            service_point = [entries[1]["x"], entries[1]["z"]]
            nearest_route_point = min(route_points, key=lambda item: math.dist(service_point, item))
            circulation_routes.append({"id": "service_accessible_route", "widthMeters": 1.5, "accessible": True, "points": [service_point, nearest_route_point]})
    has_portal = any(any(token in room["id"] for token in ("portal", "airlock", "loading_cover")) for room in rooms)
    return {
        **level, "blueprintId": f"{packet['slug']}:{floor_id}:{pattern}", "layoutPattern": pattern,
        "levelMassFootprints": footprints, "exteriorVoidsAndCourts": voids, "rooms": rooms,
        "circulation": circulation, "circulationRoutes": circulation_routes,
        "adjacency": [door["between"] for door in doors], "doorOpenings": doors,
        "exteriorEntrances": entries, "windowOpenings": windows,
        "windowPolicy": "No exterior windows below grade; separate ventilation and emergency shafts." if level["elevationMeters"] < 0 else "Real recessed openings use separate frame, glass and shutter objects.",
        "verticalCoreFootprints": core_footprints, "furnishingLayouts": furnishing,
        "clearanceZones": clearances, "socketPlacements": socket_placements,
        "sectionCutLines": section_lines,
        "portalBoundary": "physical_loading_cover_and_streaming_portal" if has_portal else "none_on_this_level",
        "sourceAuthority": source_authority,
    }


def build_sections(packet: dict[str, Any], floors: list[dict[str, Any]], cores: list[dict[str, Any]]) -> dict[str, Any]:
    width, depth = packet["envelopeMeters"]["width"], packet["envelopeMeters"]["depth"]
    primary_core = cores[0]["footprint"]
    longitudinal_z = primary_core["z"] + primary_core["depth"] / 2
    cross_x = primary_core["x"] + primary_core["width"] / 2
    sections = {}
    for name, axis, coordinate, cut_id, span in (("longitudinal", "x", longitudinal_z, "A-A", width), ("cross", "z", cross_x, "B-B", depth)):
        room_slices = []
        furniture_slices = []
        for floor in floors:
            floor_slice_count = 0
            for room in floor["rooms"]:
                intersects = room["z"] <= coordinate <= room["z"] + room["depth"] if axis == "x" else room["x"] <= coordinate <= room["x"] + room["width"]
                if intersects:
                    floor_slice_count += 1
                    room_slices.append({"levelId": floor["id"], "roomId": room["id"], "startMeters": room["x"] if axis == "x" else room["z"], "endMeters": room["x"] + room["width"] if axis == "x" else room["z"] + room["depth"], "baseElevationMeters": floor["elevationMeters"], "clearHeightMeters": floor["clearHeightMeters"]})
                    layout = next(item for item in floor["furnishingLayouts"] if item["roomId"] == room["id"])
                    furniture_slices.extend({"levelId": floor["id"], "roomId": room["id"], "kind": item["kind"], "positionMeters": item["x"] if axis == "x" else item["z"]} for item in layout["footprints"])
            if floor_slice_count == 0:
                # Detached/courtyard levels can leave the core cut in an open gap;
                # bind the nearest room edge and record its furnishings explicitly.
                room = min(floor["rooms"], key=lambda item: abs((item["z"] + item["depth"] / 2 if axis == "x" else item["x"] + item["width"] / 2) - coordinate))
                room_slices.append({"levelId": floor["id"], "roomId": room["id"], "startMeters": room["x"] if axis == "x" else room["z"], "endMeters": room["x"] + room["width"] if axis == "x" else room["z"] + room["depth"], "baseElevationMeters": floor["elevationMeters"], "clearHeightMeters": floor["clearHeightMeters"]})
                layout = next(item for item in floor["furnishingLayouts"] if item["roomId"] == room["id"])
                furniture_slices.extend({"levelId": floor["id"], "roomId": room["id"], "kind": item["kind"], "positionMeters": item["x"] if axis == "x" else item["z"]} for item in layout["footprints"])
        sections[name] = {
            "id": cut_id, "cutLineId": cut_id, "axis": axis, "cutCoordinateMeters": round(coordinate, 3), "spanMeters": span,
            "levels": [{"id": floor["id"], "elevationMeters": floor["elevationMeters"], "clearHeightMeters": floor["clearHeightMeters"]} for floor in floors],
            "roomSlices": room_slices,
            "slabs": [{"levelId": floor["id"], "elevationMeters": floor["elevationMeters"], "thicknessMeters": 0.3} for floor in floors],
            "verticalCoreSlices": [{"coreId": core["id"], "type": core["type"], "connectsLevels": core["connectsLevels"], "direction": core["direction"]} for core in cores],
            "apertureSlices": [{"levelId": floor["id"], "openingId": opening["id"], "kind": "door"} for floor in floors for opening in floor["doorOpenings"][:2]],
            "furnitureSlices": furniture_slices,
            "foundation": {"baseElevationMeters": min(floor["elevationMeters"] for floor in floors) - 0.6, "thicknessMeters": 0.6, "steppedBasalt": True},
            "roofProfile": {"baseElevationMeters": max(floor["elevationMeters"] + floor["clearHeightMeters"] for floor in floors), "peakElevationMeters": packet["envelopeMeters"]["height"], "structure": packet["roofRecommendation"], "cutRoomsAndVoids": True},
        }
    return sections


def architecture_design(packet: dict[str, Any], profile: dict[str, Any], floors: list[dict[str, Any]]) -> dict[str, Any]:
    width, depth = packet["envelopeMeters"]["width"], packet["envelopeMeters"]["depth"]
    masses = []
    for footprint in floors[min(range(len(floors)), key=lambda i: abs(floors[i]["elevationMeters"]))]["levelMassFootprints"]:
        masses.append({**footprint, "levels": [floor["id"] for floor in floors if floor["elevationMeters"] >= 0], "exteriorRole": footprint["id"].replace("_", " ")})
    roofs = [{"id": f"roof_{mass['id']}", "massId": mass["id"], "x": mass["x"], "z": mass["z"], "width": mass["width"], "depth": mass["depth"], "profile": packet["roofRecommendation"]} for mass in masses]
    openings = []
    for floor in floors:
        openings.extend({"id": entry["id"], "kind": "door", "levelId": floor["id"], "side": entry["side"], "x": entry["x"], "z": entry["z"], "separateObject": True, "planOpeningId": entry["id"]} for entry in floor["exteriorEntrances"])
        openings.extend({"id": window["id"], "kind": "window", "levelId": floor["id"], "side": window["side"], "x": window["x"], "z": window["z"], "separateObject": True, "planOpeningId": window["id"]} for window in floor["windowOpenings"])
    site = {
        "orientation": "front/local south; rear/local north", "entryApproachSide": profile["entry"],
        "courtyardsAndYards": [void for floor in floors for void in floor["exteriorVoidsAndCourts"]],
        "perimeterWall": None,
    }
    if profile.get("perimeter"):
        site["perimeterWall"] = {"id": f"{packet['slug']}_separate_perimeter", "x": 0.0, "z": 0.0, "width": width, "depth": depth, "enterable": False, "impassable": True, "walltopOnly": True, "gateSeparate": True}
    return {
        "packetSlug": packet["slug"], "layoutArchetype": profile["archetype"],
        "planSignature": f"{packet['slug']}|{'|'.join(profile['patterns'])}|{profile['entry']}|{profile['core']}",
        "exteriorMasses": masses, "roofVolumes": roofs, "exteriorOpeningRegister": openings,
        "siteContext": site,
        "continuityContract": "Every elevation opening resolves to a same-ID plan aperture; every roof volume resolves to a named mass footprint; every section resolves to marked A-A/B-B plan cuts; every labeled room is furnished and reachable.",
    }


def build_packet_geometry(packet: dict[str, Any], approved_civic_layout: dict[str, Any] | None = None) -> tuple[dict[str, Any], list[dict[str, Any]], list[dict[str, Any]], dict[str, Any]]:
    profile = LAYOUT_PROFILES[packet["slug"]]
    if len(profile["patterns"]) != len(packet["levels"]):
        raise ValueError(f"{packet['slug']} profile does not cover every physical level")
    cores = vertical_circulation(packet, profile)
    floors = [build_level(packet, level, index, profile, cores, approved_civic_layout) for index, level in enumerate(packet["levels"])]
    sections = build_sections(packet, floors, cores)
    design = architecture_design(packet, profile, floors)
    return design, floors, cores, sections
