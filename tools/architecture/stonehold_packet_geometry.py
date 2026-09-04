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

FACADE_TOLERANCE = 0.35
CUT_APERTURE_TOLERANCE = 0.5
CORE_XY_TOLERANCE = 0.05
ENTRY_WALL_TOLERANCE = 0.65
WINDOW_SPAN_TOLERANCE = 0.45
CORE_ROOM_TOKENS = ("stair", "landing", "core", "lift", "ramp", "accessible")
VERTICAL_CORE_KIND = "vertical_circulation"


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


def contains_rect(container: dict[str, Any], item: dict[str, Any], tolerance: float = 0.002) -> bool:
    return (
        item.get("width", 0) > 0
        and item.get("depth", 0) > 0
        and item["x"] >= container["x"] - tolerance
        and item["z"] >= container["z"] - tolerance
        and item["x"] + item["width"] <= container["x"] + container["width"] + tolerance
        and item["z"] + item["depth"] <= container["z"] + container["depth"] + tolerance
    )


def overlaps_rect(left: dict[str, Any], right: dict[str, Any], gap: float = 0.0) -> bool:
    epsilon = 1e-6
    return not (
        left["x"] + left["width"] + gap <= right["x"] + epsilon
        or right["x"] + right["width"] + gap <= left["x"] + epsilon
        or left["z"] + left["depth"] + gap <= right["z"] + epsilon
        or right["z"] + right["depth"] + gap <= left["z"] + epsilon
    )


def subtract_rect(room: dict[str, Any], hole: dict[str, Any], min_span: float = 1.2) -> list[dict[str, Any]]:
    ix0 = max(room["x"], hole["x"])
    iz0 = max(room["z"], hole["z"])
    ix1 = min(room["x"] + room["width"], hole["x"] + hole["width"])
    iz1 = min(room["z"] + room["depth"], hole["z"] + hole["depth"])
    if ix1 <= ix0 or iz1 <= iz0:
        return [room]
    parts: list[dict[str, Any]] = []

    def add(x: float, z: float, width: float, depth: float) -> None:
        if width >= min_span and depth >= min_span:
            part = rect(room["id"], x, z, width, depth, room.get("kind", "room"))
            if "purpose" in room:
                part["purpose"] = room["purpose"]
            parts.append(part)

    add(room["x"], room["z"], ix0 - room["x"], room["depth"])
    add(ix1, room["z"], room["x"] + room["width"] - ix1, room["depth"])
    add(ix0, room["z"], ix1 - ix0, iz0 - room["z"])
    add(ix0, iz1, ix1 - ix0, room["z"] + room["depth"] - iz1)
    return parts


def inject_fixed_room(rooms: list[dict[str, Any]], hall: dict[str, Any], hall_id: str) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for room in rooms:
        if room["id"] == hall_id:
            continue
        remainders = subtract_rect(room, hall)
        if not remainders:
            continue
        largest = max(remainders, key=lambda item: item["width"] * item["depth"])
        largest["purpose"] = room.get("purpose", room["id"].replace("_", " "))
        result.append(largest)
    hall_room = rect(hall_id, hall["x"], hall["z"], hall["width"], hall["depth"], "room")
    hall_room["purpose"] = hall_id.replace("_", " ")
    result.append(hall_room)
    return result


def clip_rooms_from_voids(rooms: list[dict[str, Any]], voids: list[dict[str, Any]]) -> list[dict[str, Any]]:
    clipped = rooms
    for void in voids:
        if void.get("kind") != "open_to_sky":
            continue
        next_rooms: list[dict[str, Any]] = []
        for room in clipped:
            remainders = subtract_rect(room, void)
            if remainders:
                largest = max(remainders, key=lambda item: item["width"] * item["depth"])
                largest["purpose"] = room.get("purpose", room["id"].replace("_", " "))
                next_rooms.append(largest)
        clipped = next_rooms
    return [room for room in clipped if room["width"] >= 0.9 and room["depth"] >= 0.9]


def facade_rooms(rooms: list[dict[str, Any]], side: str, width: float, depth: float, wall: float = ENTRY_WALL_TOLERANCE) -> list[dict[str, Any]]:
    found = []
    for room in rooms:
        if side == "south" and room["z"] <= wall:
            found.append(room)
        elif side == "north" and room["z"] + room["depth"] >= depth - wall:
            found.append(room)
        elif side == "west" and room["x"] <= wall:
            found.append(room)
        elif side == "east" and room["x"] + room["width"] >= width - wall:
            found.append(room)
    return found


def point_on_room_facade(room: dict[str, Any], side: str, width: float, depth: float) -> tuple[float, float, str]:
    if side == "south":
        return room["x"] + room["width"] / 2, max(0.0, room["z"]), "horizontal"
    if side == "north":
        return room["x"] + room["width"] / 2, min(depth, room["z"] + room["depth"]), "horizontal"
    if side == "west":
        return max(0.0, room["x"]), room["z"] + room["depth"] / 2, "vertical"
    return min(width, room["x"] + room["width"]), room["z"] + room["depth"] / 2, "vertical"


def entry_on_destination_wall(entry: dict[str, Any], room: dict[str, Any], width: float, depth: float, tolerance: float = ENTRY_WALL_TOLERANCE) -> bool:
    side = entry.get("side")
    x, z = entry["x"], entry["z"]
    if side == "south":
        return abs(z - room["z"]) <= tolerance and room["x"] - tolerance <= x <= room["x"] + room["width"] + tolerance
    if side == "north":
        return abs(z - (room["z"] + room["depth"])) <= tolerance and room["x"] - tolerance <= x <= room["x"] + room["width"] + tolerance
    if side == "west":
        return abs(x - room["x"]) <= tolerance and room["z"] - tolerance <= z <= room["z"] + room["depth"] + tolerance
    if side == "east":
        return abs(x - (room["x"] + room["width"])) <= tolerance and room["z"] - tolerance <= z <= room["z"] + room["depth"] + tolerance
    return False


def window_on_served_room_span(window: dict[str, Any], room: dict[str, Any], tolerance: float = WINDOW_SPAN_TOLERANCE) -> bool:
    side = window.get("side")
    if side in {"south", "north"}:
        return room["x"] - tolerance <= window["x"] <= room["x"] + room["width"] + tolerance
    if side in {"east", "west"}:
        return room["z"] - tolerance <= window["z"] <= room["z"] + room["depth"] + tolerance
    return False


def rooms_on_facade_at_cut(rooms: list[dict[str, Any]], side: str, along: float, width: float, depth: float) -> list[dict[str, Any]]:
    found = []
    for room in facade_rooms(rooms, side, width, depth):
        if side in {"south", "north"} and room["x"] - WINDOW_SPAN_TOLERANCE <= along <= room["x"] + room["width"] + WINDOW_SPAN_TOLERANCE:
            found.append(room)
        elif side in {"east", "west"} and room["z"] - WINDOW_SPAN_TOLERANCE <= along <= room["z"] + room["depth"] + WINDOW_SPAN_TOLERANCE:
            found.append(room)
    return found


def choose_entry_room(rooms: list[dict[str, Any]], side: str, width: float, depth: float, tokens: tuple[str, ...]) -> dict[str, Any]:
    on_side = facade_rooms(rooms, side, width, depth)
    named = [room for room in on_side if any(token in room["id"] for token in tokens)]
    pool = named or on_side or rooms
    return max(pool, key=lambda room: room["width"] * room["depth"])


def orthogonal_points(*points: list[float]) -> list[list[float]]:
    route: list[list[float]] = []
    for point in points:
        rounded = [round(point[0], 3), round(point[1], 3)]
        if not route:
            route.append(rounded)
            continue
        prev = route[-1]
        if abs(prev[0] - rounded[0]) > 0.05 and abs(prev[1] - rounded[1]) > 0.05:
            route.append([round(prev[0], 3), round(rounded[1], 3)])
        if route[-1] != rounded:
            route.append(rounded)
    return route


def hall_id_for_rooms(room_ids: list[str]) -> str:
    for token in CORE_ROOM_TOKENS:
        for room_id in room_ids:
            if token in room_id:
                return room_id
    return "vertical_core_hall"


def center_of(item: dict[str, Any]) -> tuple[float, float]:
    return item["x"] + item["width"] / 2, item["z"] + item["depth"] / 2


def room_exterior_sides(room: dict[str, Any], width: float, depth: float, wall: float = 0.55) -> list[str]:
    sides = []
    if room["z"] <= wall:
        sides.append("south")
    if room["z"] + room["depth"] >= depth - wall:
        sides.append("north")
    if room["x"] <= wall:
        sides.append("west")
    if room["x"] + room["width"] >= width - wall:
        sides.append("east")
    return sides


def window_on_facade(room: dict[str, Any], side: str, width: float, depth: float, identifier: str) -> dict[str, Any]:
    if side == "north":
        x, z = room["x"] + room["width"] / 2, depth
        orientation = "horizontal"
    elif side == "south":
        x, z = room["x"] + room["width"] / 2, 0.0
        orientation = "horizontal"
    elif side == "east":
        x, z = width, room["z"] + room["depth"] / 2
        orientation = "vertical"
    else:
        x, z = 0.0, room["z"] + room["depth"] / 2
        orientation = "vertical"
    return {
        "id": identifier, "separateFrameGlassShutter": True, "serves": room["id"], "side": side,
        "x": round(x, 3), "z": round(z, 3), "orientation": orientation, "openingMeters": [1.2, 1.5],
    }


def nearest_room_to_point(rooms: list[dict[str, Any]], x: float, z: float) -> dict[str, Any]:
    return min(rooms, key=lambda room: (room["x"] + room["width"] / 2 - x) ** 2 + (room["z"] + room["depth"] / 2 - z) ** 2)


def choose_core_room(rooms: list[dict[str, Any]], core: dict[str, Any], voids: list[dict[str, Any]] | None = None) -> dict[str, Any]:
    court = next((item for item in (voids or []) if item.get("kind") == "open_to_sky"), None)
    if court:
        wing_rooms = [
            room for room in rooms
            if room["z"] < court["z"] + court["depth"] and room["z"] + room["depth"] > court["z"]
            and (room["x"] + room["width"] <= court["x"] + 0.15 or room["x"] >= court["x"] + court["width"] - 0.15)
        ]
        if wing_rooms:
            named = [room for room in wing_rooms if any(token in room["id"] for token in CORE_ROOM_TOKENS)]
            return max(named or wing_rooms, key=lambda room: room["width"] * room["depth"])
    named = [room for room in rooms if any(token in room["id"] for token in CORE_ROOM_TOKENS)]
    candidates = named or rooms
    cx, cz = center_of(core["footprint"])

    def score(room: dict[str, Any]) -> tuple[float, float, float]:
        rcx, rcz = center_of(room)
        named_bonus = 1.0 if any(token in room["id"] for token in CORE_ROOM_TOKENS) else 0.0
        area = room["width"] * room["depth"]
        return (named_bonus, area, -((rcx - cx) ** 2 + (rcz - cz) ** 2))

    return max(candidates, key=score)


def fit_core_in_room(footprint: dict[str, Any], room: dict[str, Any], pad: float = 0.12, voids: list[dict[str, Any]] | None = None) -> dict[str, Any]:
    usable_w = max(0.9, room["width"] - 2 * pad)
    usable_d = max(0.9, room["depth"] - 2 * pad)
    width = min(max(1.2, footprint["width"]), usable_w)
    depth = min(max(1.5, footprint["depth"]), usable_d)
    width = min(width, usable_w)
    depth = min(depth, usable_d)
    x = max(room["x"] + pad, min(footprint["x"], room["x"] + room["width"] - pad - width))
    z = max(room["z"] + pad, min(footprint["z"], room["z"] + room["depth"] - pad - depth))
    court = next((item for item in (voids or []) if item.get("kind") == "open_to_sky"), None)
    if court:
        if room["x"] >= court["x"] + court["width"] - 0.2:
            x = room["x"] + pad
        elif room["x"] + room["width"] <= court["x"] + 0.2:
            x = room["x"] + room["width"] - pad - width
    return rect(footprint.get("id", "stair_footprint"), x, z, width, depth, VERTICAL_CORE_KIND)


def opening_intersects_cut(opening: dict[str, Any], axis: str, coordinate: float, tolerance: float = CUT_APERTURE_TOLERANCE) -> bool:
    orthogonal = opening["z"] if axis == "x" else opening["x"]
    return abs(orthogonal - coordinate) <= tolerance


def footprint_set(profile: dict[str, Any], width: float, depth: float, slug: str = "") -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    wall = 0.3
    inner_w, inner_d = width - wall * 2, depth - wall * 2
    masses = profile.get("masses", 1)
    voids: list[dict[str, Any]] = []
    if slug == "castle_enterable":
        tower = min(width, depth) * 0.16
        keep = rect("keep", width * 0.30, depth * 0.40, width * 0.40, depth * 0.42)
        gatehouse = rect("gatehouse_mass", width * 0.34, wall, width * 0.32, depth * 0.22)
        court_z = gatehouse["z"] + gatehouse["depth"] + 0.35
        court_d = max(3.2, keep["z"] - court_z - 0.25)
        voids.append(rect("open_courtyard", keep["x"], court_z, keep["width"], court_d, "open_to_sky"))
        footprints = [
            keep, gatehouse,
            rect("tower_sw", wall, wall, tower, tower),
            rect("tower_se", width - wall - tower, wall, tower, tower),
            rect("tower_nw", wall, depth - wall - tower, tower, tower),
            rect("tower_ne", width - wall - tower, depth - wall - tower, tower, tower),
        ]
    elif slug == "fortress_enterable":
        command = rect("command_mass", width * 0.34, depth * 0.48, width * 0.32, depth * 0.38)
        west = rect("barracks_mass", wall, wall, width * 0.30, depth * 0.44)
        east = rect("service_mass", width - wall - width * 0.30, wall, width * 0.30, depth * 0.44)
        court = rect("capture_court", command["x"], west["z"] + 0.8, command["width"], max(4.0, command["z"] - wall - 1.2), "open_to_sky")
        voids.append(court)
        footprints = [command, west, east]
    elif slug == "forge":
        hot = rect("hot_work_mass", wall, wall, inner_w * 0.58, inner_d)
        public = rect("public_counter_mass", wall + hot["width"] + width * 0.04, wall, inner_w - hot["width"] - width * 0.04, inner_d * 0.58)
        quench = rect(
            "quench_court",
            public["x"],
            public["z"] + public["depth"] + 0.25,
            public["width"],
            max(2.4, depth - wall - (public["z"] + public["depth"] + 0.25)),
            "open_to_sky",
        )
        voids.append(quench)
        footprints = [hot, public]
    elif slug == "mill_wind_water":
        wheel = rect("wheel_channel_mass", wall, depth * 0.22, width * 0.22, depth * 0.56)
        mill = rect("mill_house", wheel["x"] + wheel["width"] + 0.35, wall, width - wall - (wheel["x"] + wheel["width"] + 0.35), inner_d)
        footprints = [mill, wheel]
    elif profile.get("court"):
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


def distribute_rooms_to_masses(room_ids: list[str], footprints: list[dict[str, Any]]) -> list[dict[str, Any]]:
    if not room_ids or not footprints:
        return []
    groups: list[list[str]] = [[] for _ in footprints]
    for room_id in room_ids:
        assigned = False
        for index, mass in enumerate(footprints):
            tokens = [token for token in mass["id"].split("_") if token not in {"mass", "shell"}]
            if tokens and any(token in room_id for token in tokens):
                groups[index].append(room_id)
                assigned = True
                break
        if not assigned:
            groups[min(range(len(footprints)), key=lambda index: len(groups[index]))].append(room_id)
    rooms = []
    for mass, ids in zip(footprints, groups):
        if not ids:
            continue
        rooms.extend(split_strip(ids, mass, horizontal=mass["width"] >= mass["depth"]))
    return rooms


def place_rooms(pattern: str, room_ids: list[str], width: float, depth: float, footprints: list[dict[str, Any]], voids: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], dict[str, Any], list[list[float]]]:
    wall = 0.3
    interior = rect("interior", wall, wall, width - 2 * wall, depth - 2 * wall)
    primary = choose_primary(room_ids)
    others = [item for item in room_ids if item != primary]
    corridor_points: list[list[float]]
    court = next((item for item in voids if item.get("kind") == "open_to_sky"), None)
    four_wing = bool(footprints) and footprints[0]["id"] == "south_wing"

    if pattern in {"detached_three", "detached_lofts"}:
        groups = [[], [], []]
        for index, room_id in enumerate(room_ids):
            groups[index % 3].append(room_id)
        rooms = []
        for shell, ids in zip(footprints[:3], groups):
            rooms.extend(split_strip(ids, shell, horizontal=False))
        corridor_points = [[wall, depth * 0.50], [width - wall, depth * 0.50]]
    elif pattern in {"tower_quadrants", "tower_crown"}:
        core_gap = min(width, depth) * 0.34
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
    elif court and (four_wing or pattern in {"courtyard_four", "keep_courtyard", "fort_courtyard", "memory_hall_court", "market_ring", "gallery_ring", "keep_upper", "tower_roof", "keep_undercroft", "fort_undercroft", "fort_upper"}):
        if four_wing:
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
            corridor_points = [[wall, court["z"]], [court["x"], court["z"]], [court["x"] + court["width"], court["z"]], [width - wall, court["z"]]]
        else:
            rooms = distribute_rooms_to_masses(room_ids, footprints)
            corridor_points = [[footprints[0]["x"], footprints[0]["z"]], [footprints[0]["x"] + footprints[0]["width"] / 2, footprints[0]["z"] + footprints[0]["depth"] / 2]]
    elif len(footprints) >= 2:
        rooms = distribute_rooms_to_masses(room_ids, footprints)
        corridor_points = [[footprints[0]["x"] + footprints[0]["width"] / 2, footprints[0]["z"]], [footprints[0]["x"] + footprints[0]["width"] / 2, footprints[0]["z"] + footprints[0]["depth"]]]
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
        columns = 3 if width >= 16 else 2
        rooms = grid_rooms(room_ids, interior, columns)
        corridor_points = [[wall, depth * 0.52], [width - wall, depth * 0.52]]

    rooms = clip_rooms_from_voids(rooms, voids)
    for room in rooms:
        room["purpose"] = room["id"].replace("_", " ")
    if not rooms:
        rooms = [rect(room_ids[0], wall, wall, max(2.4, width * 0.3), max(2.4, depth * 0.3), "room")]
        rooms[0]["purpose"] = room_ids[0].replace("_", " ")
    min_x = min(point[0] for point in corridor_points)
    max_x = max(point[0] for point in corridor_points)
    min_z = min(point[1] for point in corridor_points)
    max_z = max(point[1] for point in corridor_points)
    circulation = rect("primary_circulation", min_x, min_z, max(1.5, max_x - min_x), max(1.5, max_z - min_z), "accessible_route_envelope")
    return rooms, circulation, corridor_points


def _eq(kind: str, label: str, symbol: str, width: float, depth: float, section_height: float = 1.1) -> dict[str, Any]:
    return {"kind": kind, "label": label, "symbol": symbol, "width": width, "depth": depth, "sectionHeightMeters": section_height}


def equipment_spec(packet_slug: str, room_id: str) -> list[dict[str, Any]]:
    rid = room_id
    if packet_slug == "forge":
        if "hearth" in rid:
            return [_eq("furnace", "coal furnace", "FURNACE", 1.8, 1.3, 1.8), _eq("hood_flue", "smoke hood / flue", "HOOD", 1.5, 0.55, 2.4)]
        if "forge_workfloor" in rid or rid.endswith("workfloor"):
            return [_eq("anvil", "anvil station", "ANVIL", 0.8, 0.8, 0.9), _eq("work_bench", "hot-work bench", "BENCH", 1.8, 0.7, 0.9)]
        if "quench" in rid:
            return [_eq("quench_trough", "quench trough", "QUENCH", 2.0, 0.75, 0.8)]
        if "counter" in rid:
            return [_eq("service_counter", "public counter", "COUNTER", 2.0, 0.7, 1.1)]
        if "material" in rid or "store" in rid:
            return [_eq("billet_rack", "billet / bar rack", "BILLET", 1.6, 0.6, 1.4)]
        if "pattern" in rid or "archive" in rid:
            return [_eq("shelf_bay", "pattern archive", "SHELF", 1.6, 0.45, 2.0)]
        if "tool" in rid:
            return [_eq("tool_rack", "tool wall", "TOOLS", 1.8, 0.4, 1.8)]
        if "foreman" in rid or "desk" in rid:
            return [_eq("desk", "foreman desk", "DESK", 1.4, 0.7, 0.8)]
    if packet_slug == "mill_wind_water":
        if "wheel" in rid:
            return [_eq("waterwheel", "guarded waterwheel", "WHEEL", 1.6, 1.6, 3.2), _eq("race_channel", "headrace / tailrace", "RACE", 2.6, 0.6, 0.7)]
        if "gear" in rid:
            return [_eq("gearing", "pit-wheel gearing", "GEARS", 1.6, 1.2, 1.6), _eq("mill_shaft", "horizontal drive shaft", "SHAFT", 2.4, 0.35, 0.4)]
        if "milling" in rid:
            return [_eq("millstone", "millstones", "STONE", 1.4, 1.4, 0.7), _eq("hopper", "grain hopper", "HOPPER", 1.0, 0.8, 1.4)]
        if "grain_receiving" in rid or "bagging" in rid:
            return [_eq("grain_bin", "grain bin", "BIN", 1.4, 0.9, 1.2), _eq("sack_bench", "bagging bench", "SACK", 1.6, 0.6, 0.9)]
        if "grain_store" in rid:
            return [_eq("storage_rack", "grain sacks", "SACKS", 1.8, 0.7, 1.4)]
        if "drainage" in rid:
            return [_eq("race_channel", "tailrace walk", "RACE", 2.2, 0.55, 0.5)]
    if packet_slug == "academy":
        if "teaching" in rid:
            return [_eq("lecture_table", "instructor table", "LECTURE", 2.2, 0.8, 0.9), _eq("bench_row", "student benches", "BENCH", 2.4, 0.5, 0.5), _eq("chalkboard", "slate board", "BOARD", 2.0, 0.2, 1.4)]
        if "practice_lab" in rid or "lab" in rid:
            return [_eq("lab_bench", "practice bench", "LAB", 2.0, 0.8, 0.9), _eq("tool_rack", "tool rack", "TOOLS", 1.6, 0.45, 1.6), _eq("vise_station", "vise station", "VISE", 0.8, 0.8, 1.1), _eq("material_bin", "material bin", "BIN", 0.9, 0.7, 0.8)]
        if "library" in rid or "archive" in rid or "records" in rid:
            return [_eq("shelf_bay", "archive shelving", "SHELF", 1.8, 0.45, 2.2), _eq("reading_table", "reading table", "TABLE", 1.6, 0.8, 0.8)]
        if "study" in rid:
            return [_eq("study_desk", "study desks", "DESK", 1.5, 0.7, 0.8)]
        if "office" in rid:
            return [_eq("desk", "instructor desk", "DESK", 1.4, 0.7, 0.8)]
        if "tool_store" in rid:
            return [_eq("tool_rack", "stored tools", "TOOLS", 1.6, 0.45, 1.6)]
    if packet_slug == "workshop":
        if "workfloor" in rid:
            return [_eq("work_bench", "assembly bench", "BENCH", 2.0, 0.8, 0.9), _eq("vise_station", "vise", "VISE", 0.7, 0.7, 1.1)]
        if "tool_wall" in rid:
            return [_eq("tool_rack", "tool wall", "TOOLS", 2.2, 0.4, 1.8)]
        if "counter" in rid:
            return [_eq("service_counter", "public counter", "COUNTER", 2.0, 0.7, 1.1)]
        if "pattern" in rid:
            return [_eq("shelf_bay", "pattern store", "SHELF", 1.6, 0.45, 2.0)]
    if packet_slug in {"lumber_mill"}:
        if "saw" in rid:
            return [_eq("saw_bed", "saw carriage", "SAW", 2.6, 1.0, 1.2), _eq("log_deck", "log deck", "LOG", 2.4, 0.9, 0.8)]
        if "timber" in rid:
            return [_eq("timber_rack", "sorted timber", "TIMBER", 2.0, 0.8, 1.2)]
    if packet_slug == "gold_mine":
        if "assay" in rid:
            return [_eq("assay_furnace", "assay furnace", "ASSAY", 1.2, 0.9, 1.4), _eq("assay_bench", "assay bench", "BENCH", 1.6, 0.7, 0.9)]
        if "winch" in rid:
            return [_eq("winch_drum", "winch drum", "WINCH", 1.6, 1.2, 1.4)]
        if "ore" in rid:
            return [_eq("ore_bin", "ore bin", "ORE", 1.6, 1.0, 1.2)]
        if "portal" in rid or "airlock" in rid or "loading_cover" in rid:
            return [_eq("portal_cover", "physical loading cover", "PORTAL", 1.8, 1.2, 2.2)]
    if packet_slug == "quarry":
        if "cutting" in rid:
            return [_eq("cutting_frame", "stone cutting frame", "CUT", 2.2, 1.1, 1.6), _eq("block_bed", "block bed", "BLOCK", 1.8, 1.0, 0.7)]
        if "machine" in rid:
            return [_eq("crusher_bed", "machine bed", "CRUSH", 2.0, 1.1, 1.5)]
    if packet_slug == "stable":
        if "stall" in rid:
            return [_eq("stall_partition", "stall partition", "STALL", 1.8, 1.2, 1.4), _eq("manger", "manger", "FEED", 1.4, 0.45, 0.7)]
        if "tack" in rid:
            return [_eq("tack_rack", "tack rack", "TACK", 1.8, 0.45, 1.6)]
        if "hay" in rid or "feed" in rid:
            return [_eq("hay_bay", "hay / feed bay", "HAY", 1.8, 1.0, 1.2)]
        if "aisle" in rid:
            return [_eq("grooming_post", "grooming post", "GROOM", 0.6, 0.6, 1.4)]
    if packet_slug == "inn_tavern":
        if "kitchen" in rid:
            return [_eq("prep_table", "kitchen prep", "PREP", 1.8, 0.8, 0.9), _eq("hearth_range", "cooking range", "RANGE", 1.6, 0.8, 1.2)]
        if "bar" in rid:
            return [_eq("service_bar", "service bar", "BAR", 2.4, 0.7, 1.1)]
        if "common" in rid:
            return [_eq("table_bench", "common tables", "TABLE", 1.6, 0.9, 0.8), _eq("bench_row", "benches", "BENCH", 2.0, 0.45, 0.5)]
        if "guest" in rid or "bunk" in rid or "bedroom" in rid:
            return [_eq("bed_or_cot", "guest bed", "BED", 2.0, 1.0, 0.7)]
    if packet_slug in {"castle_enterable", "fortress_enterable"}:
        if "council" in rid or "audience" in rid or "command_hall" in rid:
            return [_eq("council_table", "council table", "COUNCIL", 2.6, 1.2, 0.9), _eq("bench_row", "benches", "BENCH", 2.2, 0.5, 0.5)]
        if "detention" in rid or "cell" in rid:
            return [_eq("secure_cot", "secure cot", "COT", 1.9, 0.8, 0.6), _eq("cell_partition", "cell partition", "CELL", 0.2, 1.8, 2.2)]
        if "kitchen" in rid:
            return [_eq("prep_table", "keep kitchen", "PREP", 1.8, 0.8, 0.9), _eq("hearth_range", "range", "RANGE", 1.6, 0.8, 1.2)]
        if "armory" in rid or "muster" in rid:
            return [_eq("weapon_rack", "weapon rack", "ARMS", 1.8, 0.5, 1.8), _eq("bench_row", "muster bench", "BENCH", 2.0, 0.45, 0.5)]
        if "courtyard" in rid or "capture_court" in rid:
            return [_eq("court_well", "court cistern lip", "WELL", 1.2, 1.2, 0.6)]
    if packet_slug == "barracks":
        if "bunk" in rid:
            return [_eq("bed_or_cot", "bunks", "BUNK", 2.0, 0.9, 1.6), _eq("locker_bank", "lockers", "LOCKER", 1.6, 0.5, 1.8)]
        if "armory" in rid or "weapon" in rid or "gear" in rid:
            return [_eq("weapon_rack", "weapon rack", "ARMS", 1.8, 0.5, 1.8)]
        if "muster" in rid:
            return [_eq("bench_row", "muster benches", "BENCH", 2.4, 0.5, 0.5)]
    if packet_slug == "dwelling":
        if "living" in rid or "hearth" in rid:
            return [_eq("hearth", "living hearth", "HEARTH", 1.4, 0.7, 1.3), _eq("table_bench", "family table", "TABLE", 1.6, 0.9, 0.8)]
        if "kitchen" in rid:
            return [_eq("prep_table", "kitchen prep", "PREP", 1.5, 0.7, 0.9)]
        if "bedroom" in rid:
            return [_eq("bed_or_cot", "bed", "BED", 2.0, 1.0, 0.7)]
        if "pantry" in rid:
            return [_eq("storage_rack", "pantry racks", "RACK", 1.2, 0.45, 1.6)]
    if packet_slug == "town_hall":
        if "public_hall" in rid or "council" in rid:
            return [_eq("council_table", "civic table", "TABLE", 2.2, 1.0, 0.9), _eq("bench_row", "public benches", "BENCH", 2.0, 0.45, 0.5)]
        if "records" in rid or "archive" in rid:
            return [_eq("shelf_bay", "records", "SHELF", 1.6, 0.45, 2.0)]
        if "office" in rid or "steward" in rid:
            return [_eq("desk", "steward desk", "DESK", 1.4, 0.7, 0.8)]
        if "stores" in rid:
            return [_eq("storage_rack", "civic stores", "RACK", 1.4, 0.5, 1.6)]

    rules = [
        (("bed", "bunk", "guest", "living", "rest", "sleep"), [_eq("bed_or_cot", "sleeping furniture", "BED", 2.0, 1.0, 0.7)]),
        (("kitchen", "pantry", "food", "drink"), [_eq("prep_table", "prep table", "PREP", 1.6, 0.7, 0.9)]),
        (("archive", "record", "library", "drawing", "pattern"), [_eq("shelf_bay", "archive shelving", "SHELF", 1.6, 0.45, 2.0)]),
        (("store", "stock", "hold", "cellar", "cage"), [_eq("storage_rack", "racks / crates", "RACK", 1.6, 0.6, 1.5)]),
        (("forge", "hearth", "quench", "workfloor", "workroom", "workshop"), [_eq("work_bench", "workbench", "BENCH", 1.8, 0.7, 0.9)]),
        (("market", "shop", "counter", "assay", "stall"), [_eq("service_counter", "counter + display", "COUNTER", 1.8, 0.7, 1.1)]),
        (("office", "desk", "warden", "foreman", "clerk", "sergeant", "steward"), [_eq("desk", "desk + chair", "DESK", 1.4, 0.7, 0.8)]),
        (("muster", "guard", "armory", "weapon", "barracks"), [_eq("weapon_rack", "weapon rack", "ARMS", 1.6, 0.5, 1.8)]),
        (("stable", "stall", "feed", "hay"), [_eq("stall_partition", "stall / feed bay", "STALL", 1.6, 1.0, 1.4)]),
        (("mill", "grain", "wheel", "machine", "winch", "pump", "ore", "cutting", "saw"), [_eq("machine_bed", "guarded machine", "MACHINE", 1.8, 0.9, 1.3)]),
        (("detention", "cell", "prison"), [_eq("secure_cot", "cot + partition", "COT", 1.9, 0.8, 0.6)]),
        (("hall", "court", "gallery", "common", "assembly", "teaching", "petition", "reception"), [_eq("table_bench", "table / benches", "TABLE", 1.8, 0.9, 0.8)]),
        (("wash",), [_eq("wash_trough", "wash trough", "WASH", 1.4, 0.55, 0.8)]),
        (("portal", "airlock", "loading_cover"), [_eq("portal_cover", "physical portal cover", "PORTAL", 1.6, 1.1, 2.2)]),
    ]
    for tokens, specs in rules:
        if any(token in rid for token in tokens):
            return specs
    return [_eq("utility_table", "task table + wall storage", "UTILITY", 1.2, 0.6, 0.8)]


def furnish_room(room: dict[str, Any], floor_id: str, packet_slug: str) -> dict[str, Any]:
    specs = equipment_spec(packet_slug, room["id"])
    pad = min(0.35, room["width"] * 0.1, room["depth"] * 0.1)
    usable_w = max(0.4, room["width"] - 2 * pad)
    usable_d = max(0.4, room["depth"] - 2 * pad)
    footprints = []
    south_cursor = room["x"] + pad
    north_cursor = room["x"] + pad
    for index, spec in enumerate(specs):
        item_w = min(spec["width"], max(0.35, usable_w * 0.72))
        item_d = min(spec["depth"], max(0.25, usable_d * 0.32))
        if index % 2 == 0:
            x = min(south_cursor, room["x"] + room["width"] - pad - item_w)
            z = room["z"] + pad
            south_cursor = x + item_w + 0.18
            orientation = 0
        else:
            x = min(north_cursor, room["x"] + room["width"] - pad - item_w)
            z = room["z"] + room["depth"] - pad - item_d
            north_cursor = x + item_w + 0.18
            orientation = 180
        x = min(max(x, room["x"] + pad), room["x"] + room["width"] - pad - item_w)
        z = min(max(z, room["z"] + pad), room["z"] + room["depth"] - pad - item_d)
        footprints.append({
            "id": f"{floor_id}_{room['id']}_{spec['kind']}_{index + 1}",
            "kind": spec["kind"], "label": spec["label"], "symbol": spec["symbol"],
            "x": round(x, 3), "z": round(z, 3), "width": round(item_w, 3), "depth": round(item_d, 3),
            "orientationDegrees": orientation, "count": 1, "sectionHeightMeters": spec["sectionHeightMeters"],
        })
    if room["width"] >= room["depth"]:
        aisle_d = min(1.2, room["depth"])
        aisle = rect(f"{room['id']}_protected_aisle", room["x"], room["z"] + (room["depth"] - aisle_d) / 2, room["width"], aisle_d, "minimum_1_2m_aisle")
    else:
        aisle_w = min(1.2, room["width"])
        aisle = rect(f"{room['id']}_protected_aisle", room["x"] + (room["width"] - aisle_w) / 2, room["z"], aisle_w, room["depth"], "minimum_1_2m_aisle")
    approach_w = min(1.2, room["width"])
    approach = rect(f"{room['id']}_door_approach", room["x"] + (room["width"] - approach_w) / 2, room["z"], approach_w, min(1.2, room["depth"]), "door_swing_clearance")
    return {"roomId": room["id"], "layoutIntent": " + ".join(spec["label"] for spec in specs), "footprints": footprints, "protectedClearances": [aisle, approach]}


def side_point(side: str, width: float, depth: float, offset: float = 0.5) -> tuple[float, float, str]:
    if side == "north":
        return width * offset, depth, "horizontal"
    if side == "east":
        return width, depth * offset, "vertical"
    if side == "west":
        return 0.0, depth * offset, "vertical"
    return width * offset, 0.0, "horizontal"


def stacked_shaft(packet: dict[str, Any], profile: dict[str, Any], footprints: list[dict[str, Any]], voids: list[dict[str, Any]]) -> tuple[dict[str, Any], dict[str, Any], dict[str, Any] | None, str]:
    width, depth = packet["envelopeMeters"]["width"], packet["envelopeMeters"]["depth"]
    has_lift = bool(profile.get("lift"))
    stair_w = min(1.7, max(1.35, width * 0.09))
    stair_d = min(2.6, max(2.0, depth * 0.14))
    lift_w = min(1.8, max(1.5, width * 0.09)) if has_lift else 0.0
    lift_d = min(1.8, max(1.5, depth * 0.10)) if has_lift else 0.0
    gap = 0.28 if has_lift else 0.0
    hall_w = stair_w + gap + lift_w
    hall_d = max(stair_d, lift_d, 2.0)
    stack = "x"
    if has_lift and hall_w > (width - 0.6) * 0.42:
        hall_w = max(stair_w, lift_w)
        hall_d = stair_d + 0.28 + lift_d
        stack = "z"
    if packet["slug"] == "town_hall":
        hall = rect("core_hall", 7.4, 3.4, 1.8, 3.45, VERTICAL_CORE_KIND)
        stair_fp = rect("stair_footprint", 7.4, 3.4, 1.8, 1.7, VERTICAL_CORE_KIND)
        lift_fp = rect("lift_footprint", 7.4, 5.35, 1.8, 1.5, VERTICAL_CORE_KIND) if has_lift else None
        return hall, stair_fp, lift_fp, "z"
    if packet["slug"] == "watchtower":
        hall_w, hall_d = min(3.2, width - 0.8), min(2.6, depth - 0.8)
        hx, hz = (width - hall_w) / 2, (depth - hall_d) / 2
        hall = rect("core_hall", hx, hz, hall_w, hall_d, VERTICAL_CORE_KIND)
        stair_fp = rect("stair_footprint", hx, hz, hall_w * 0.48, hall_d, VERTICAL_CORE_KIND)
        lift_fp = rect("lift_footprint", hx + hall_w * 0.52, hz, hall_w * 0.48, hall_d, VERTICAL_CORE_KIND) if has_lift else None
        return hall, stair_fp, lift_fp, "x"
    court = next((item for item in voids if item.get("kind") == "open_to_sky"), None)
    if court:
        east_facade_x = width - hall_w - 0.3
        west_facade_x = 0.3
        if east_facade_x >= court["x"] + court["width"] + 0.05:
            hx = east_facade_x
        elif west_facade_x + hall_w <= court["x"] - 0.05:
            hx = west_facade_x
        else:
            east_space = width - 0.3 - (court["x"] + court["width"])
            hx = court["x"] + court["width"] + 0.05 if east_space >= hall_w + 0.24 else max(0.3, court["x"] - hall_w - 0.05)
        hz = court["z"] + max(0.1, (court["depth"] - hall_d) / 2)
    elif footprints:
        mass = max(footprints, key=lambda item: item["width"] * item["depth"])
        hx = max(mass["x"] + 0.12, min(mass["x"] + mass["width"] - hall_w - 0.12, mass["x"] + mass["width"] - hall_w - 0.12))
        hz = max(mass["z"] + 0.12, min(mass["z"] + mass["depth"] - hall_d - 0.12, mass["z"] + (mass["depth"] - hall_d) / 2))
    else:
        hx = min(width - hall_w - 0.3, max(0.3, width * profile["core"][0] - hall_w / 2))
        hz = min(depth - hall_d - 0.3, max(0.3, depth * profile["core"][1] - hall_d / 2))
    hx = min(max(hx, 0.3), max(0.3, width - hall_w - 0.3))
    hz = min(max(hz, 0.3), max(0.3, depth - hall_d - 0.3))
    hall = rect("core_hall", hx, hz, hall_w, hall_d, VERTICAL_CORE_KIND)
    if stack == "x":
        stair_fp = rect("stair_footprint", hx, hz, stair_w if has_lift else hall_w, hall_d, VERTICAL_CORE_KIND)
        lift_fp = rect("lift_footprint", hx + stair_w + gap, hz, lift_w, hall_d, VERTICAL_CORE_KIND) if has_lift else None
    else:
        stair_fp = rect("stair_footprint", hx, hz, hall_w, stair_d, VERTICAL_CORE_KIND)
        lift_fp = rect("lift_footprint", hx, hz + stair_d + 0.28, hall_w, lift_d, VERTICAL_CORE_KIND) if has_lift else None
    return hall, stair_fp, lift_fp, stack


def vertical_circulation(packet: dict[str, Any], profile: dict[str, Any], hall: dict[str, Any], stair_fp: dict[str, Any], lift_fp: dict[str, Any] | None) -> list[dict[str, Any]]:
    levels = packet["levels"]
    level_ids = [level["id"] for level in levels]
    cores = [{
        "id": f"{packet['slug']}_stair_core", "type": "u_stair" if len(levels) > 2 else "dogleg_stair",
        "footprint": stair_fp,
        "clearWidthMeters": 1.5, "direction": "UP clockwise", "connectsLevels": level_ids,
        "landings": [{"levelId": level["id"], "elevationMeters": level["elevationMeters"], "depthMeters": 1.5} for level in levels],
        "accessible": False, "separateObjects": True,
    }]
    if lift_fp is not None and profile.get("lift"):
        cores.append({
            "id": f"{packet['slug']}_{profile['lift']}", "type": profile["lift"],
            "footprint": lift_fp,
            "clearWidthMeters": 1.5, "direction": "vertical platform", "connectsLevels": level_ids,
            "landings": [{"levelId": level["id"], "elevationMeters": level["elevationMeters"], "depthMeters": 1.5} for level in levels],
            "accessible": "goods" not in profile["lift"], "separateObjects": True,
        })
    if profile.get("ramp"):
        width, depth = packet["envelopeMeters"]["width"], packet["envelopeMeters"]["depth"]
        ramp_w = max(1.5, min(1.8, width * 0.16))
        ramp_d = min(depth * 0.40, 5.5)
        rx, rz = 0.5, max(0.5, hall["z"] - ramp_d - 0.3)
        candidate = rect("ramp_footprint", rx, rz, ramp_w, ramp_d, VERTICAL_CORE_KIND)
        if overlaps_rect(candidate, hall):
            rz = hall["z"] + hall["depth"] + 0.3
            candidate = rect("ramp_footprint", rx, min(rz, depth - ramp_d - 0.3), ramp_w, ramp_d, VERTICAL_CORE_KIND)
        cores.append({
            "id": f"{packet['slug']}_switchback_ramp", "type": "switchback_ramp_1_to_12",
            "footprint": candidate,
            "clearWidthMeters": 1.5, "direction": "UP with 1.5 m intermediate landing", "connectsLevels": level_ids,
            "landings": [{"levelId": level["id"], "elevationMeters": level["elevationMeters"], "depthMeters": 1.5} for level in levels],
            "accessible": True, "separateObjects": True,
        })
    return cores


def windows_for_level(rooms: list[dict[str, Any]], width: float, depth: float, floor_id: str, elevation: float) -> list[dict[str, Any]]:
    if elevation < 0:
        return []
    windows = []
    for room in rooms:
        sides = room_exterior_sides(room, width, depth)
        if not sides:
            continue
        side = sides[0] if len(sides) == 1 else sides[len(windows) % len(sides)]
        windows.append(window_on_facade(room, side, width, depth, f"window_{floor_id}_{room['id']}"))
    return windows


def add_cut_aligned_windows(floors: list[dict[str, Any]], cores: list[dict[str, Any]], width: float, depth: float) -> None:
    cut_a = floors[0]["sectionCutLines"][0]
    cut_b = floors[0]["sectionCutLines"][1]
    longitudinal_z = cut_a["from"][1]
    cross_x = cut_b["from"][0]
    targets = [
        ("west", 0.0, longitudinal_z, longitudinal_z),
        ("east", width, longitudinal_z, longitudinal_z),
        ("south", cross_x, 0.0, cross_x),
        ("north", cross_x, depth, cross_x),
    ]
    for floor in floors:
        if floor["elevationMeters"] < 0:
            continue
        existing = {(round(window["x"], 2), round(window["z"], 2), window["side"]) for window in floor["windowOpenings"]}
        for side, x, z, along in targets:
            served = rooms_on_facade_at_cut(floor["rooms"], side, along, width, depth)
            if not served:
                continue
            room = served[0]
            key = (round(x, 2), round(z, 2), side)
            if key in existing:
                continue
            window = {
                "id": f"window_{floor['id']}_cut_{side}",
                "separateFrameGlassShutter": True, "serves": room["id"], "side": side,
                "x": round(x, 3), "z": round(z, 3),
                "orientation": "vertical" if side in {"east", "west"} else "horizontal",
                "openingMeters": [1.2, 1.5], "cutAligned": True,
            }
            floor["windowOpenings"].append(window)
            existing.add(key)


def ensure_cut_openings(floors: list[dict[str, Any]]) -> None:
    """Guarantee each named plan cut intersects at least one recorded opening per floor that has rooms on the cut."""
    cut_a = floors[0]["sectionCutLines"][0]
    cut_b = floors[0]["sectionCutLines"][1]
    z_cut = cut_a["from"][1]
    x_cut = cut_b["from"][0]

    def add_door(floor: dict[str, Any], room: dict[str, Any], x: float, z: float, orientation: str, tag: str) -> None:
        x = min(max(x, room["x"] + 0.15), room["x"] + room["width"] - 0.15)
        z = min(max(z, room["z"] + 0.15), room["z"] + room["depth"] - 0.15)
        door = {
            "id": f"door_{floor['id']}_cut_{tag}_{room['id']}",
            "separateObject": True,
            "between": [room["id"], "primary_circulation"],
            "to": room["id"],
            "orientation": orientation,
            "x": round(x, 3),
            "z": round(z, 3),
            "clearOpeningMeters": [1.2, 2.4],
            "swing": "in",
            "cutAligned": True,
        }
        if any(abs(existing["x"] - door["x"]) <= 0.05 and abs(existing["z"] - door["z"]) <= 0.05 for existing in floor["doorOpenings"]):
            return
        floor["doorOpenings"].append(door)

    for floor in floors:
        rooms_a = [room for room in floor["rooms"] if room["z"] - 0.05 <= z_cut <= room["z"] + room["depth"] + 0.05]
        rooms_b = [room for room in floor["rooms"] if room["x"] - 0.05 <= x_cut <= room["x"] + room["width"] + 0.05]
        if rooms_a:
            room = next((item for item in rooms_a if any(token in item["id"] for token in CORE_ROOM_TOKENS)), rooms_a[0])
            add_door(floor, room, room["x"] + room["width"] / 2, z_cut, "horizontal", "AA")
        if rooms_b:
            room = next((item for item in rooms_b if any(token in item["id"] for token in CORE_ROOM_TOKENS)), rooms_b[0])
            add_door(floor, room, x_cut, room["z"] + room["depth"] / 2, "vertical", "BB")


def unify_section_cuts(packet: dict[str, Any], floors: list[dict[str, Any]], cores: list[dict[str, Any]]) -> None:
    width, depth = packet["envelopeMeters"]["width"], packet["envelopeMeters"]["depth"]
    ground = floors[min(range(len(floors)), key=lambda index: abs(floors[index]["elevationMeters"]))]
    primary_local = next(item for item in ground["verticalCoreFootprints"] if item["coreId"] == cores[0]["id"])
    cores[0]["footprint"] = primary_local["footprint"]
    longitudinal_z = round(primary_local["footprint"]["z"] + primary_local["footprint"]["depth"] / 2, 3)
    core_x = primary_local["footprint"]["x"]
    core_w = primary_local["footprint"]["width"]
    court = next((void for floor in floors for void in floor.get("exteriorVoidsAndCourts", []) if void.get("kind") == "open_to_sky"), None)
    if court:
        cross_x = round(court["x"] + court["width"] / 2, 3)
    else:
        cross_x = round(core_x + core_w / 2, 3)
    section_lines = [
        {"id": "A-A", "orientation": "longitudinal", "from": [0.3, longitudinal_z], "to": [round(width - 0.3, 3), longitudinal_z], "cutsVerticalCoreId": cores[0]["id"]},
        {"id": "B-B", "orientation": "cross", "from": [cross_x, 0.3], "to": [cross_x, round(depth - 0.3, 3)], "cutsVerticalCoreId": cores[0]["id"]},
    ]
    for floor in floors:
        floor["sectionCutLines"] = [dict(line) for line in section_lines]


def critical_features(packet: dict[str, Any], floors: list[dict[str, Any]], cores: list[dict[str, Any]]) -> list[dict[str, Any]]:
    width, depth = packet["envelopeMeters"]["width"], packet["envelopeMeters"]["depth"]
    slug = packet["slug"]
    primary = cores[0]["footprint"]
    longitudinal_z = primary["z"] + primary["depth"] / 2
    features: list[dict[str, Any]] = []
    if slug == "mill_wind_water":
        features.extend([
            {"id": "waterwheel", "kind": "waterwheel", "symbol": "WHEEL", "x": 0.05, "z": round(max(0.4, longitudinal_z - 1.1), 3), "width": 1.7, "depth": 2.2, "sectionHeightMeters": 3.4, "sectionVisible": True},
            {"id": "headrace", "kind": "race_channel", "symbol": "RACE", "x": 0.05, "z": round(max(0.2, longitudinal_z - 0.3), 3), "width": min(6.0, width * 0.42), "depth": 0.7, "sectionHeightMeters": 0.7, "sectionVisible": True},
            {"id": "drive_shaft", "kind": "mill_shaft", "symbol": "SHAFT", "x": 1.6, "z": round(longitudinal_z - 0.15, 3), "width": min(5.5, width * 0.38), "depth": 0.35, "sectionHeightMeters": 0.4, "sectionVisible": True},
            {"id": "pit_gearing", "kind": "gearing", "symbol": "GEARS", "x": min(width * 0.34, 5.2), "z": round(longitudinal_z - 0.6, 3), "width": 1.6, "depth": 1.2, "sectionHeightMeters": 1.6, "sectionVisible": True},
        ])
    if slug == "forge":
        hearth = next((room for floor in floors for room in floor["rooms"] if "hearth" in room["id"]), None)
        if hearth:
            hx, hz = center_of(hearth)
            features.append({"id": "forge_hood_flue", "kind": "hood_flue", "symbol": "HOOD", "x": round(hx - 0.75, 3), "z": round(hz - 0.25, 3), "width": 1.5, "depth": 0.5, "sectionHeightMeters": 2.6, "sectionVisible": True})
            features.append({"id": "forge_furnace", "kind": "furnace", "symbol": "FURNACE", "x": round(hx - 0.9, 3), "z": round(hearth["z"] + 0.25, 3), "width": 1.8, "depth": 1.2, "sectionHeightMeters": 1.8, "sectionVisible": True})
        quench = next((void for floor in floors for void in floor["exteriorVoidsAndCourts"] if "quench" in void["id"] or void.get("kind") == "open_to_sky"), None)
        if quench:
            features.append({"id": "forge_quench_trough", "kind": "quench_trough", "symbol": "QUENCH", "x": round(quench["x"] + 0.3, 3), "z": round(quench["z"] + 0.3, 3), "width": 2.0, "depth": 0.75, "sectionHeightMeters": 0.8, "sectionVisible": True})
    if slug == "academy":
        lab = next((room for floor in floors for room in floor["rooms"] if "practice_lab" in room["id"]), None)
        if lab:
            lx, lz = center_of(lab)
            features.append({"id": "academy_lab_bench", "kind": "lab_bench", "symbol": "LAB", "x": round(lab["x"] + 0.3, 3), "z": round(lab["z"] + 0.25, 3), "width": min(2.0, lab["width"] - 0.6), "depth": 0.8, "sectionHeightMeters": 0.9, "sectionVisible": True})
            features.append({"id": "academy_tool_rack", "kind": "tool_rack", "symbol": "TOOLS", "x": round(lx, 3), "z": round(lab["z"] + lab["depth"] - 0.7, 3), "width": 1.6, "depth": 0.4, "sectionHeightMeters": 1.6, "sectionVisible": True})
    if slug in {"castle_enterable", "fortress_enterable"}:
        court = next((void for floor in floors for void in floor["exteriorVoidsAndCourts"] if void.get("kind") == "open_to_sky"), None)
        if court:
            features.append({**court, "kind": "open_courtyard", "symbol": "COURT", "sectionHeightMeters": packet["envelopeMeters"]["height"], "sectionVisible": True, "openToSky": True})
    return features


def build_level(packet: dict[str, Any], level: dict[str, Any], index: int, profile: dict[str, Any], cores: list[dict[str, Any]], approved: dict[str, Any] | None, footprints: list[dict[str, Any]], voids: list[dict[str, Any]], hall: dict[str, Any]) -> dict[str, Any]:
    width, depth = packet["envelopeMeters"]["width"], packet["envelopeMeters"]["depth"]
    pattern = profile["patterns"][index]
    source_authority = "packet-specific measured blueprint"
    if approved is not None and packet["slug"] == "town_hall":
        key = "groundFloor" if level["id"] == "ground" else "upperFloor"
        rooms = []
        floor_voids = list(voids)
        for source_room in approved[key]:
            room = {"id": source_room["id"], "purpose": source_room["id"].replace("_", " "), "x": source_room["x"], "z": source_room["z"], "width": source_room["width"], "depth": source_room["depth"], "kind": "room"}
            if "void" in source_room:
                room["void"] = source_room["void"]
                floor_voids.append({"id": "approved_open_gallery_void", **source_room["void"], "kind": "open_to_below"})
            rooms.append(room)
        circulation = rect("approved_primary_circulation", 2.1, 2.2, 5.3, 1.2, "accessible_route_envelope")
        route_points = [[4.75, 0.0], [4.75, 7.9]]
        source_authority = "PR #664 shared_civic_hall_layout_v001.json coordinates retained exactly"
        voids = floor_voids
    else:
        rooms, circulation, route_points = place_rooms(pattern, list(level["rooms"]), width, depth, footprints, voids)
        hall_id = hall_id_for_rooms([room["id"] for room in rooms] + list(level["rooms"]))
        rooms = inject_fixed_room(rooms, hall, hall_id)
        rooms = clip_rooms_from_voids(rooms, voids)
        if not any(contains_rect(room, hall) for room in rooms):
            rooms = inject_fixed_room(rooms, hall, hall_id)
        rooms = [room for room in rooms if room["width"] >= 1.2 or room["depth"] >= 1.2]
        if not rooms:
            fallback = rect(level["rooms"][0], 0.3, 0.3, max(2.4, width * 0.3), max(2.4, depth * 0.3), "room")
            fallback["purpose"] = level["rooms"][0].replace("_", " ")
            rooms = [fallback]

    floor_id = level["id"]
    ground_like = index == min(range(len(packet["levels"])), key=lambda i: abs(packet["levels"][i]["elevationMeters"]))
    entries = []
    doors = []
    if ground_like:
        public_tokens = ("entry", "public", "muster", "hall", "inspection", "shop", "processing", "cutting", "market", "aisle", "floor", "gatehouse")
        entry_room = choose_entry_room(rooms, profile["entry"], width, depth, public_tokens)
        ex, ez, orientation = point_on_room_facade(entry_room, profile["entry"], width, depth)
        entry = {"id": f"{packet['slug']}_main_entry", "from": "outside", "to": entry_room["id"], "side": profile["entry"], "x": round(ex, 3), "z": round(ez, 3), "orientation": orientation, "clearOpeningMeters": [2.5, 3.0], "swing": "double_out", "stepFree": True, "separateObject": True}
        entries.append(entry)
        doors.append({**entry, "between": ["outside", entry_room["id"]]})
        opposite = {"south": "north", "north": "south", "east": "west", "west": "east"}[profile["entry"]]
        service_tokens = ("service", "rear", "yard", "loading", "dry_entry", "cart", "tool", "kitchen")
        if facade_rooms(rooms, opposite, width, depth):
            service_room = choose_entry_room(rooms, opposite, width, depth, service_tokens)
            if service_room["id"] != entry_room["id"]:
                sx, sz, sorientation = point_on_room_facade(service_room, opposite, width, depth)
                service = {"id": f"{packet['slug']}_service_entry", "from": "outside", "to": service_room["id"], "side": opposite, "x": round(sx, 3), "z": round(sz, 3), "orientation": sorientation, "clearOpeningMeters": [1.4, 2.4], "swing": "out", "stepFree": True, "separateObject": True}
                entries.append(service)
                doors.append({**service, "between": ["outside", service_room["id"]]})

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
    windows = windows_for_level(rooms, width, depth, floor_id, level["elevationMeters"])

    def overlaps(left: dict[str, Any], right: dict[str, Any]) -> bool:
        return overlaps_rect(left, right)

    core_footprints = []
    for core in cores:
        if floor_id not in core["connectsLevels"]:
            continue
        footprint = core["footprint"]
        room = next((item for item in rooms if contains_rect(item, footprint)), None)
        if room is None:
            rooms = inject_fixed_room(rooms, hall, hall_id_for_rooms([item["id"] for item in rooms] + list(level["rooms"])))
            room = next((item for item in rooms if contains_rect(item, footprint)), rooms[-1])
        core_footprints.append({"coreId": core["id"], "type": core["type"], "footprint": dict(footprint), "landingDepthMeters": 1.5, "direction": core["direction"], "locatedInRoomId": room["id"]})

    furnishing = [furnish_room(room, floor_id, packet["slug"]) for room in rooms]
    fitted_core_shapes = [item["footprint"] for item in core_footprints]
    for room_layout in furnishing:
        unobstructed = [item for item in room_layout["footprints"] if not any(overlaps(item, core) for core in fitted_core_shapes)]
        if unobstructed:
            room_layout["footprints"] = unobstructed
        elif not room_layout["footprints"]:
            room = next(item for item in rooms if item["id"] == room_layout["roomId"])
            room_layout["footprints"] = furnish_room(room, floor_id, packet["slug"])["footprints"][:1]
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
    primary_core = core_footprints[0]["footprint"]
    longitudinal_z = round(primary_core["z"] + primary_core["depth"] / 2, 3)
    cross_x = round(primary_core["x"] + primary_core["width"] / 2, 3)
    section_lines = [
        {"id": "A-A", "orientation": "longitudinal", "from": [0.3, longitudinal_z], "to": [round(width - 0.3, 3), longitudinal_z], "cutsVerticalCoreId": cores[0]["id"]},
        {"id": "B-B", "orientation": "cross", "from": [cross_x, 0.3], "to": [cross_x, round(depth - 0.3, 3)], "cutsVerticalCoreId": cores[0]["id"]},
    ]
    circulation_routes = [{"id": "primary_accessible_route", "widthMeters": packet["clearancesMeters"]["primaryCirculationWidth"], "accessible": True, "points": route_points}]
    hall_center = [hall["x"] + hall["width"] / 2, hall["z"] + hall["depth"] / 2]
    if entries:
        entry_point = [entries[0]["x"], entries[0]["z"]]
        dest = next(room for room in rooms if room["id"] == entries[0]["to"])
        dest_center = [dest["x"] + dest["width"] / 2, dest["z"] + dest["depth"] / 2]
        circulation_routes[0]["points"] = orthogonal_points(entry_point, dest_center, hall_center)
        if len(entries) > 1:
            service_point = [entries[1]["x"], entries[1]["z"]]
            service_room = next(room for room in rooms if room["id"] == entries[1]["to"])
            service_center = [service_room["x"] + service_room["width"] / 2, service_room["z"] + service_room["depth"] / 2]
            circulation_routes.append({"id": "service_accessible_route", "widthMeters": 1.5, "accessible": True, "points": orthogonal_points(service_point, service_center, hall_center)})
    elif packet["slug"] != "town_hall":
        circulation_routes[0]["points"] = orthogonal_points(route_points[0], hall_center, route_points[-1])
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


def build_sections(packet: dict[str, Any], floors: list[dict[str, Any]], cores: list[dict[str, Any]], features: list[dict[str, Any]]) -> dict[str, Any]:
    width, depth = packet["envelopeMeters"]["width"], packet["envelopeMeters"]["depth"]
    cut_a = floors[0]["sectionCutLines"][0]
    cut_b = floors[0]["sectionCutLines"][1]
    longitudinal_z = cut_a["from"][1]
    cross_x = cut_b["from"][0]
    sections = {}
    for name, axis, coordinate, cut_id, span in (("longitudinal", "x", longitudinal_z, "A-A", width), ("cross", "z", cross_x, "B-B", depth)):
        room_slices = []
        furniture_slices = []
        void_slices = []
        feature_slices = []
        aperture_slices = []
        for floor in floors:
            floor_slice_count = 0
            for room in floor["rooms"]:
                intersects = room["z"] - 0.05 <= coordinate <= room["z"] + room["depth"] + 0.05 if axis == "x" else room["x"] - 0.05 <= coordinate <= room["x"] + room["width"] + 0.05
                if intersects:
                    floor_slice_count += 1
                    room_slices.append({"levelId": floor["id"], "roomId": room["id"], "startMeters": room["x"] if axis == "x" else room["z"], "endMeters": room["x"] + room["width"] if axis == "x" else room["z"] + room["depth"], "baseElevationMeters": floor["elevationMeters"], "clearHeightMeters": floor["clearHeightMeters"]})
                    layout = next(item for item in floor["furnishingLayouts"] if item["roomId"] == room["id"])
                    furniture_slices.extend({
                        "levelId": floor["id"], "roomId": room["id"], "kind": item["kind"], "symbol": item.get("symbol", item["kind"]),
                        "positionMeters": item["x"] if axis == "x" else item["z"],
                        "sectionHeightMeters": item.get("sectionHeightMeters", 0.9),
                    } for item in layout["footprints"])
            if floor_slice_count == 0:
                continue
            for void in floor["exteriorVoidsAndCourts"]:
                intersects = void["z"] - 0.05 <= coordinate <= void["z"] + void["depth"] + 0.05 if axis == "x" else void["x"] - 0.05 <= coordinate <= void["x"] + void["width"] + 0.05
                if intersects:
                    void_slices.append({
                        "levelId": floor["id"], "voidId": void["id"], "kind": void.get("kind", "void"),
                        "openToSky": void.get("kind") == "open_to_sky",
                        "startMeters": void["x"] if axis == "x" else void["z"],
                        "endMeters": void["x"] + void["width"] if axis == "x" else void["z"] + void["depth"],
                        "baseElevationMeters": max(0.0, floor["elevationMeters"]),
                    })
            openings = [
                *floor.get("exteriorEntrances", []),
                *floor.get("windowOpenings", []),
                *[{**door, "kind": "door"} for door in floor.get("doorOpenings", [])],
            ]
            for opening in openings:
                if opening_intersects_cut(opening, axis, coordinate):
                    along = opening["x"] if axis == "x" else opening["z"]
                    orthogonal = opening["z"] if axis == "x" else opening["x"]
                    aperture_slices.append({
                        "levelId": floor["id"], "openingId": opening["id"],
                        "kind": opening.get("kind") or ("window" if opening["id"].startswith("window_") else "door"),
                        "side": opening.get("side"),
                        "x": opening["x"], "z": opening["z"],
                        "positionMeters": round(along, 3),
                        "orthogonalMeters": round(orthogonal, 3),
                    })
        for feature in features:
            intersects = feature["z"] <= coordinate <= feature["z"] + feature["depth"] if axis == "x" else feature["x"] <= coordinate <= feature["x"] + feature["width"]
            if intersects or feature.get("openToSky") and (feature["z"] <= coordinate <= feature["z"] + feature["depth"] if axis == "x" else feature["x"] <= coordinate <= feature["x"] + feature["width"]):
                feature_slices.append({
                    "id": feature["id"], "kind": feature["kind"], "symbol": feature.get("symbol", feature["kind"]),
                    "positionMeters": feature["x"] if axis == "x" else feature["z"],
                    "sectionHeightMeters": feature.get("sectionHeightMeters", 1.2),
                    "openToSky": feature.get("openToSky", False),
                })
        core_slices = []
        for core in cores:
            along = core["footprint"]["x"] if axis == "x" else core["footprint"]["z"]
            extent = core["footprint"]["width"] if axis == "x" else core["footprint"]["depth"]
            core_slices.append({
                "coreId": core["id"], "type": core["type"], "connectsLevels": core["connectsLevels"],
                "direction": core["direction"], "startMeters": round(along, 3), "endMeters": round(along + extent, 3),
            })
        sections[name] = {
            "id": cut_id, "cutLineId": cut_id, "axis": axis, "cutCoordinateMeters": round(coordinate, 3), "spanMeters": span,
            "levels": [{"id": floor["id"], "elevationMeters": floor["elevationMeters"], "clearHeightMeters": floor["clearHeightMeters"]} for floor in floors],
            "roomSlices": room_slices,
            "voidSlices": void_slices,
            "slabs": [{"levelId": floor["id"], "elevationMeters": floor["elevationMeters"], "thicknessMeters": 0.3} for floor in floors],
            "verticalCoreSlices": core_slices,
            "apertureSlices": aperture_slices,
            "furnitureSlices": furniture_slices,
            "featureSlices": feature_slices,
            "foundation": {"baseElevationMeters": min(floor["elevationMeters"] for floor in floors) - 0.6, "thicknessMeters": 0.6, "steppedBasalt": True},
            "roofProfile": {"baseElevationMeters": max(floor["elevationMeters"] + floor["clearHeightMeters"] for floor in floors), "peakElevationMeters": packet["envelopeMeters"]["height"], "structure": packet["roofRecommendation"], "cutRoomsAndVoids": True, "openToSkySpans": [{"startMeters": item["startMeters"], "endMeters": item["endMeters"]} for item in void_slices if item.get("openToSky")]},
        }
    return sections


def architecture_design(packet: dict[str, Any], profile: dict[str, Any], floors: list[dict[str, Any]], features: list[dict[str, Any]]) -> dict[str, Any]:
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
        "criticalFeatures": features,
        "voids": [void for floor in floors for void in floor["exteriorVoidsAndCourts"] if void.get("kind") == "open_to_sky"],
        "siteContext": site,
        "continuityContract": "Every elevation opening resolves to a same-ID plan aperture on its claimed facade; every roof volume resolves to a named mass footprint; every section resolves to marked A-A/B-B plan cuts that intersect recorded apertures; every labeled room is furnished with profession-specific equipment and is reachable.",
    }


def build_packet_geometry(packet: dict[str, Any], approved_civic_layout: dict[str, Any] | None = None) -> tuple[dict[str, Any], list[dict[str, Any]], list[dict[str, Any]], dict[str, Any]]:
    profile = LAYOUT_PROFILES[packet["slug"]]
    if len(profile["patterns"]) != len(packet["levels"]):
        raise ValueError(f"{packet['slug']} profile does not cover every physical level")
    width, depth = packet["envelopeMeters"]["width"], packet["envelopeMeters"]["depth"]
    footprints, voids = footprint_set(profile, width, depth, packet["slug"])
    hall, stair_fp, lift_fp, _stack = stacked_shaft(packet, profile, footprints, voids)
    cores = vertical_circulation(packet, profile, hall, stair_fp, lift_fp)
    floors = [build_level(packet, level, index, profile, cores, approved_civic_layout, footprints, voids, hall) for index, level in enumerate(packet["levels"])]
    unify_section_cuts(packet, floors, cores)
    add_cut_aligned_windows(floors, cores, width, depth)
    ensure_cut_openings(floors)
    features = critical_features(packet, floors, cores)
    sections = build_sections(packet, floors, cores, features)
    design = architecture_design(packet, profile, floors, features)
    return design, floors, cores, sections


def geometry_invariants(packet: dict[str, Any]) -> list[str]:
    """Fail-closed spatial contracts beyond field presence."""
    errors: list[str] = []
    floors = packet.get("floorPlans", [])
    width = packet.get("envelopeMeters", {}).get("width", 0)
    depth = packet.get("envelopeMeters", {}).get("depth", 0)
    cores_by_id: dict[str, list[dict[str, Any]]] = {}
    for floor in floors:
        for item in floor.get("verticalCoreFootprints", []):
            cores_by_id.setdefault(item["coreId"], []).append((floor["id"], item))
    for core_id, placed in cores_by_id.items():
        xs = {round(item["footprint"]["x"], 3) for _, item in placed}
        zs = {round(item["footprint"]["z"], 3) for _, item in placed}
        ws = {round(item["footprint"]["width"], 3) for _, item in placed}
        ds = {round(item["footprint"]["depth"], 3) for _, item in placed}
        if len(xs) > 1 or len(zs) > 1 or len(ws) > 1 or len(ds) > 1:
            errors.append("vertical cores must keep the same shaft XY across connected levels")
            break
        rooms_ok = True
        for floor_id, item in placed:
            floor = next(level for level in floors if level["id"] == floor_id)
            room = next((room for room in floor["rooms"] if room["id"] == item.get("locatedInRoomId")), None)
            rooms_ok = rooms_ok and room is not None and contains_rect(room, item["footprint"])
        if not rooms_ok:
            errors.append("vertical cores must keep the same shaft XY across connected levels")
            break
    for floor in floors:
        footprints = [item["footprint"] for item in floor.get("verticalCoreFootprints", [])]
        for index, left in enumerate(footprints):
            for right in footprints[index + 1:]:
                if overlaps_rect(left, right):
                    errors.append("stair and lift footprints must not overlap on the same floor")
                    break
            else:
                continue
            break
        if errors and errors[-1] == "stair and lift footprints must not overlap on the same floor":
            break
    court_slugs = {"castle_enterable", "fortress_enterable", "forge", "city_capital_kit", "religious_cultural_structure"}
    if packet.get("slug") in court_slugs:
        for floor in floors:
            courts = [void for void in floor.get("exteriorVoidsAndCourts", []) if void.get("kind") == "open_to_sky"]
            if not courts:
                errors.append("open-to-sky courtyard voids must exist on every level and match both sections")
                break
            if any(overlaps_rect(room, courts[0]) for room in floor.get("rooms", [])):
                errors.append("open-to-sky courtyard voids must exist on every level and match both sections")
                break
        else:
            for section in packet.get("sections", {}).values():
                if not any(void.get("openToSky") for void in section.get("voidSlices", [])):
                    errors.append("open-to-sky courtyard voids must exist on every level and match both sections")
                    break
    rooms_by_floor = {floor["id"]: {room["id"]: room for room in floor["rooms"]} for floor in floors}
    for floor in floors:
        for entry in floor.get("exteriorEntrances", []):
            room = rooms_by_floor[floor["id"]].get(entry.get("to"))
            if room is None or not entry_on_destination_wall(entry, room, width, depth):
                errors.append("every exterior entrance must sit on the destination room wall")
                break
        else:
            continue
        break
    for floor in floors:
        for window in floor.get("windowOpenings", []):
            room = rooms_by_floor[floor["id"]].get(window.get("serves"))
            if room is None or not window_on_served_room_span(window, room):
                errors.append("every window must sit on the served room wall span")
                break
        else:
            continue
        break
    return errors
