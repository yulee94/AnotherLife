#!/usr/bin/env python3
"""Generate Grok Imagine 2D concept sheets for StoneholdConceptP1 V001.

Never prints credentials. Grok 4.6 High / grok-imagine-image-2.0 first.
GPT-5.6 Sol is not invoked unless Grok returns no answer (handled by caller).
"""

from __future__ import annotations

import argparse
import base64
import json
import os
import sys
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

AUTH_PATH = Path(os.environ.get("LOCALAPPDATA", "")) / "hermes" / "auth.json"
API = "https://api.x.ai/v1"
OUT_DIR = Path(__file__).resolve().parent
MODEL = "grok-imagine-image-2.0"
CHAT_MODEL = "grok-4.6"
QUALITY = "medium"
ASPECT = "3:2"

COMMON = (
    "Premium AA dark-high-fantasy stylized-realistic game-art production sheet. "
    "Geometry-readable orthographic architectural drawing. Strict orthographic cameras, no perspective, no 3/4 hero shot, no isometric dollhouse cutaway. "
    "Neutral seamless gray studio, even studio lighting, no sky, no landscape backdrop except the subject terrain that belongs to the building or cave. "
    "Clean unoccluded views, generous empty gray space between panels, no overlapping objects, no collage, no moodboard, no photobash. "
    "No watermarks, no logos, no UI, no modern objects, no vehicles, no firearms, no plastic, no neon, no glass curtain walls. "
    "No magic VFX, no particles, no lightning bolts, no glow sprites, no baked volumetrics. "
    "No animals, no real-world wildlife, no dragons, no bosses, no people except one optional 1.8 m featureless gray mannequin for scale. "
    "Stonehold only. Tempered Embermist / Faultline Bastion identity: matte dark basalt, heat-darkened iron plates, restrained iron-gold edgework, soot and ash wear, tectonic horizontal mass, battered basalt fins, compressed openings, mechanical weight over ornament, localized warm practical brazier light as material not sprites. "
    "No dwarven caricature, no lava theme, no magma rivers, no runes as readable language, no crests copied from other realms, no Accordant Isle, no cherry blossoms, no fifth realm, no Crownlands limestone, no Eldergrove roots, no Umbral violet fog. "
    "No fake facade shells: every building shown is a real enterable volume with readable wall thickness and punched door/window apertures. "
    "Include a simple unlabeled metric scale bar with five equal tick marks and a faint 0.5 m floor grid where the subject is object-scale. "
    "PBR materials, Black-Desert-inspired finish bar, not cartoon, not anime. "
    "Same identity, identical scale in every panel of this sheet."
)

SHEETS: dict[str, str] = {
    "stonehold_capital_district_plan_v001": (
        COMMON
        + " SUBJECT: Stonehold inner-realm capital Anvildeep as a SINGLE architectural SITE PLAN. "
        "Camera is STRAIGHT DOWN at 90 degrees like a CAD roof plan. Roofs are FLAT 2D footprints only. "
        "NO facades, NO walls in elevation, NO axonometric, NO isometric, NO 3/4, NO floating island diorama, NO cliff sides visible. "
        "Organic inner-safe-pocket street grain following tectonic fault lines: civic spine, forge quarter, residential terraces, market street, hero-keep precinct. "
        "Not a perfect circle, not a Renaissance grid, not a rectangular walled box. "
        "Walkable streets as pale basalt corridors between dark-iron roof rectangles. Every building footprint has a small door-tick gap on the street edge proving enterability. "
        "Hero keep is the largest rectangular/octagonal roof at the civic head. One controlled approach road enters from a SINGLE inner-gate edge; do NOT draw outer and inner sequential barriers side by side. "
        "Gray studio paper background with faint grid. No countryside, no Worldscar, no bridges, no fortresses, no 30 m apron diagram."
    ),
    "stonehold_capital_skyline_north_south_v001": (
        COMMON
        + " SUBJECT: Stonehold capital Anvildeep as TWO FLAT orthographic CITY ELEVATIONS, like architectural drawings of a street wall. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT: NORTH elevation. RIGHT: SOUTH elevation of the same city, same scale, generous gray between. "
        "Composition MUST be a HORIZONTAL ROW: two-storey terrace houses, then a wide rectangular keep block in the center, then more two-storey houses. "
        "The skyline is a low BROAD rectangle, about three storeys at the keep and two storeys at the houses. SOUTH may show the main keep door; NORTH the rear walltop. "
        "FORBIDDEN: stepped pyramid, ziggurat, volcano, mountain, triangular silhouette, floating island, needle spires. "
        "Materials: matte basalt, heat-darkened iron, restrained iron-gold edges, soot. No dragons, no lava."
    ),
    "stonehold_capital_skyline_east_west_v001": (
        COMMON
        + " SUBJECT: Stonehold capital Anvildeep as TWO FLAT orthographic CITY ELEVATIONS, like architectural drawings of a street wall. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT: EAST elevation. RIGHT: WEST elevation of the same city, same scale, generous gray between. "
        "Composition MUST be a HORIZONTAL ROW: two-storey terrace houses, then a wide rectangular keep block in the center, then more two-storey houses. "
        "The skyline is a low BROAD rectangle, about three storeys at the keep and two storeys at the houses. "
        "FORBIDDEN: stepped pyramid, ziggurat, volcano, mountain, triangular silhouette, floating island, needle spires. "
        "Materials: matte basalt, heat-darkened iron, restrained iron-gold edges, soot. No dragons, no lava."
    ),
    "stonehold_capital_hero_keep_shell_v001": (
        COMMON
        + " SUBJECT: Stonehold capital HERO KEEP SHELL only, enterable fortress-keep volume, not a city, not a sequential dual-gate. "
        "Three isolated panels left-to-right of the SAME keep: ROOF PLAN; FRONT ELEVATION; RIGHT SIDE ELEVATION. "
        "Low broad forge-keep about 48 m across and 18 m to walltop, three storeys plus defendable walltop walk. "
        "Real wall thickness about 1.2 m, battered basalt fins, iron-plate banding, compressed door and window apertures punched fully through the wall. "
        "One obvious main door opening about 3.6 m wide by 4.2 m tall; interior door ticks on plan; stair bulkhead to walltop. "
        "Optional 1.8 m gray mannequin beside FRONT. Empty gray between panels. "
        "No dollhouse cutaway, no missing back wall, no fake facade, no lava, no dragons."
    ),
    "stonehold_capital_keep_interior_program_v001": (
        COMMON
        + " SUBJECT: developed FURNISHED INTERIOR PROGRAM of the same Stonehold hero keep. "
        "TWO isolated TRUE TOP-DOWN FLOOR PLANS with generous gray between them, not a 3/4 cutaway, not a dollhouse, not stacked isometric floors. "
        "LEFT: GROUND FLOOR PLAN — receiving hall on axis, smithing floor with anvils and quench troughs, stores, mess, stair cores. "
        "RIGHT: UPPER FLOOR PLAN — armory racks, barracks bunks, command room with map table, walltop stair heads in the SAME plan locations as ground stairs. "
        "Doors as 1.2 m swings, 0.3 m interior partitions, 1.2 m exterior wall, circulation clear for a 1.8 m mannequin path through every room. "
        "Furniture unoccluded: benches, racks, anvils, tables, bunks, barrels. Warm practical braziers as objects, not glow sprites. "
        "Large combat interior meant to be streamed in 3D; show wing breaks as thick walls, not floating rooms. "
        "Forbidden: roof ripped off 3/4 view, impossible stacked rooms, missing stairs, lava forges, dragons."
    ),
    "stonehold_city_street_plan_elevation_v001": (
        COMMON
        + " SUBJECT: typical Stonehold INNER-CITY STREET KIT, one short block of the same street. "
        "TWO isolated panels of the SAME street with generous gray: LEFT true TOP-DOWN STREET PLAN; RIGHT true STREET ELEVATION looking at the building fronts. "
        "Walkable street about 6 m between facing facades, basalt paving, shallow gutters, door ticks on every building. "
        "Mix of house, shop, service, and workshop footprints along one tectonic terrace street, unique Embermist cladding, not palette-swaps. "
        "Elevation shows two-storey compressed openings, iron-gold edge only, soot, no hanging tavern signs as modern logos. "
        "No capital keep, no fortress, no sequential gate, no 3/4 view, no people, no dragons."
    ),
    "stonehold_city_house_module_v001": (
        COMMON
        + " SUBJECT: Stonehold city HOUSE MODULE, one enterable dwelling, small seamless interior. "
        "Three isolated panels of the SAME house: FRONT ELEVATION; RIGHT SIDE ELEVATION; INTERIOR GROUND PLAN (true top-down, furnished). "
        "Footprint about 8 m by 10 m, two storeys, wall thickness 0.4 m, interior door 1.2 m by 2.4 m, stair to loft marked on plan. "
        "Basalt masonry, dark timber lintels, heat-darkened iron shutters, restrained iron-gold drip edges, soot. "
        "Plan furniture: hearth, table, benches, storage chests, beds upstairs indicated by stair, all unoccluded and traversable. "
        "No missing walls, no 3/4 cutaway, no modern kitchen, no dragons."
    ),
    "stonehold_city_shop_module_v001": (
        COMMON
        + " SUBJECT: Stonehold city SHOP MODULE, one enterable storefront, small seamless interior. "
        "Three isolated panels of the SAME shop: FRONT ELEVATION with a wide serving opening and shutter; RIGHT SIDE ELEVATION; INTERIOR PLAN (true top-down). "
        "Footprint about 8 m by 12 m, counter, public floor, rear stores, stair to keeper loft. "
        "Embermist materials: basalt, iron shutters, timber shelves, restrained iron-gold. "
        "Plan shows counter clearance, barrel/crate stores, door to street and door to rear, traversable aisles. "
        "No 3/4 cutaway, no neon, no glass shopfront, no logos, no dragons."
    ),
    "stonehold_city_service_module_v001": (
        COMMON
        + " SUBJECT: Stonehold city SERVICE MODULE, one enterable public cistern-and-stores house, small seamless interior. "
        "Three isolated panels of the SAME building: FRONT ELEVATION; RIGHT SIDE ELEVATION; INTERIOR PLAN (true top-down). "
        "Footprint about 10 m by 12 m. Plan: covered cistern basin, bucket bench, dry-goods stores, service counter, rear yard door. "
        "Heavy basalt, iron grates, timber roof, soot, no pipes as modern plumbing, no ceramic tile bathrooms. "
        "Traversable aisles, 1.2 m doors, unoccluded fixtures. No 3/4 cutaway, no dragons, no lava."
    ),
    "stonehold_city_workshop_module_v001": (
        COMMON
        + " SUBJECT: Stonehold city WORKSHOP MODULE, one enterable smithing workshop, small seamless interior. "
        "Three isolated panels of the SAME workshop: FRONT ELEVATION with wagon-wide work door about 2.4 m; RIGHT SIDE ELEVATION with chimney; INTERIOR PLAN (true top-down). "
        "Footprint about 12 m by 14 m. Plan: two anvils, quench trough, coal bunker as packed fuel not lava, tool wall, finished-goods rack, clear 2 m work aisle. "
        "Basalt hearth mass, heat-darkened iron hood, restrained iron-gold clamps, ash-stained floor. "
        "No magma, no open lava channel, no 3/4 cutaway, no dragons."
    ),
    "stonehold_inner_cave_mouth_v001": (
        COMMON
        + " SUBJECT: Stonehold NON-DRAGON INNER-REALM cave MOUTH only, ore-gallery refuge language, not a warzone, not a lair. "
        "Two isolated panels of the SAME mouth: FRONT ELEVATION of the opening in a basalt escarpment; RIGHT SIDE ELEVATION showing overhang depth and lintel thickness. "
        "Human-scale mouth about 4.5 m wide by 3.6 m tall, timber-and-iron square lintel, shallow cut-stone threshold, walkable packed-ash floor. "
        "Organic inner safe-pocket geology: layered basalt, soot, restrained iron-gold clamp plates, no carved dragon skull, no treasure hoard, no eggs, no bones. "
        "Optional 1.8 m gray mannequin at the threshold. No fortress, no sequential gate, no 30 m apron diagram, no lava river."
    ),
    "stonehold_inner_cave_section_circulation_v001": (
        COMMON
        + " SUBJECT: Stonehold NON-DRAGON INNER cave, SAME volume as a refuge ore-gallery. "
        "Two isolated panels with generous gray: LEFT LONGITUDINAL SECTION; RIGHT CIRCULATION PLAN (true top-down). "
        "Organic pocket topology: entry gallery, two side alcoves, a roundish main chamber, a rear store niche, all connected by walkable floors with no unauthored holes. "
        "Ceiling heights readable in section (gallery 3.2 m, main chamber 5.5 m). Stair or ramp only if it lands. "
        "Plan shows a single obvious loop-free refuge path plus one return alcove; doors/portcullis optional at mouth only. "
        "Packed-ash floors, basalt ribs, timber props, iron clamps, soot, practical braziers as objects. "
        "Forbidden: dragon nest, boss arena, lava, 3/4 cutaway collage, impossible stacked voids, flying ledges without path."
    ),
    "stonehold_inner_cave_chamber_kit_v001": (
        COMMON
        + " SUBJECT: Stonehold NON-DRAGON INNER cave CHAMBER KIT, three isolated modular pieces of the SAME construction language, identical scale, generous gray, not overlapping. "
        "LEFT: straight gallery bay 8 m long, 4 m wide, 3.2 m high, timber props, basalt ribs. "
        "CENTER: node chamber about 10 m across, 5.5 m high, one entrance and two exits at 120-degree offsets, walkable floor. "
        "RIGHT: side alcove 4 m deep with store shelves and a brazier object. "
        "True orthographic FRONT SECTION of each module, like a construction kit, not three different biomes. "
        "No dragons, no lava, no collage overlap, no people except optional mannequin in the gallery bay."
    ),
    "stonehold_outer_cave_mouth_v001": (
        COMMON
        + " SUBJECT: Stonehold NON-DRAGON OUTER-WARZONE cave MOUTH only, combat-scarred ore cut, not a dragon lair, not a fortress. "
        "Two isolated panels of the SAME mouth: FRONT ELEVATION; RIGHT SIDE ELEVATION. "
        "Mouth about 6 m wide by 4.2 m tall, broken iron lintel, blast-spalled basalt, ash slope that is NOT a climbable talus ramp onto a wall. "
        "War-worn but still a human dungeon entrance. Optional 1.8 m mannequin. "
        "Do not show a fortress, sequential gate, 30 m apron, Worldscar, or capital. "
        "No dragon skull, no eggs, no lava river, no modern sandbags."
    ),
    "stonehold_outer_cave_section_combat_circulation_v001": (
        COMMON
        + " SUBJECT: Stonehold NON-DRAGON OUTER-WARZONE cave combat circulation of ONE dungeon. "
        "Two isolated panels: LEFT LONGITUDINAL SECTION; RIGHT COMBAT CIRCULATION PLAN (true top-down). "
        "Plan: mouth choke, split gallery, cross-chamber with cover blocks (basalt ribs, overturned ore carts as geometry not modern vehicles), looping return corridor, rear hold. "
        "Sightlines and 2 m combat clearance readable. No boss podium, no circular arena with a dragon perch. "
        "Section shows 3.5–6 m ceilings, no unauthored pits, ramps that land. "
        "Ash, soot, heat-darkened iron shoring, restrained iron-gold only on clamp plates. "
        "Forbidden: dragons, lava, 3/4 dollhouse, fortress walls, sequential dual-gate."
    ),
    "stonehold_outer_cave_chamber_kit_v001": (
        COMMON
        + " SUBJECT: Stonehold NON-DRAGON OUTER-WARZONE cave CHAMBER KIT, three isolated modular pieces, identical scale, generous gray. "
        "LEFT: combat gallery bay 10 m long with side cover ribs. "
        "CENTER: cross-chamber node about 14 m across with two sightline corners and a 2 m clear middle. "
        "RIGHT: hold/alcove with iron-shored stores and a barred inner door. "
        "True orthographic FRONT SECTION of each module, same Embermist basalt/iron language. "
        "No dragon nest, no lava, no overlapping collage, no fortress apron, no people except optional mannequin in the gallery."
    ),
}


def load_token() -> str:
    auth = json.loads(AUTH_PATH.read_text(encoding="utf-8"))
    token = auth["providers"]["xai-oauth"]["tokens"]["access_token"]
    if not token:
        raise SystemExit("missing xai-oauth access_token")
    return token


def post_json(url: str, token: str, payload: dict) -> tuple[int, dict | str]:
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=data,
        method="POST",
        headers={
            "Authorization": "Bearer " + token,
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=300) as resp:
            body = resp.read().decode("utf-8", errors="replace")
            return resp.status, json.loads(body)
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        try:
            parsed = json.loads(body)
        except json.JSONDecodeError:
            parsed = body[:800]
        return exc.code, parsed
    except urllib.error.URLError as exc:
        return 0, f"urlerror:{exc.reason}"


def download(url: str, dest: Path) -> int:
    req = urllib.request.Request(
        url,
        method="GET",
        headers={"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"},
    )
    with urllib.request.urlopen(req, timeout=180) as resp:
        blob = resp.read()
    dest.write_bytes(blob)
    return len(blob)


def generate_one(token: str, name: str, prompt: str) -> dict:
    dest = OUT_DIR / f"{name}.png"
    payload = {
        "model": MODEL,
        "prompt": prompt,
        "n": 1,
        "aspect_ratio": ASPECT,
        "quality": QUALITY,
        "response_format": "b64_json",
    }
    status, body = post_json(API + "/images/generations", token, payload)
    result = {
        "name": name,
        "http": status,
        "path": str(dest),
        "bytes": 0,
        "ok": False,
        "provider": "xai-oauth",
        "chat_model": CHAT_MODEL,
        "image_model": MODEL,
        "quality": QUALITY,
        "aspect_ratio": ASPECT,
        "fallback": False,
    }
    if status != 200:
        result["error"] = body if isinstance(body, str) else json.dumps(body)[:800]
        return result
    data = body.get("data") if isinstance(body, dict) else None
    if not data:
        result["error"] = "no data"
        return result
    url = data[0].get("url")
    b64 = data[0].get("b64_json")
    revised = data[0].get("revised_prompt")
    if revised:
        result["revised_prompt"] = revised[:400]
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    if b64:
        blob = base64.b64decode(b64)
        dest.write_bytes(blob)
        result["bytes"] = len(blob)
        result["ok"] = len(blob) > 1000
        return result
    if url:
        try:
            result["bytes"] = download(url, dest)
            result["ok"] = result["bytes"] > 1000
            return result
        except urllib.error.HTTPError as exc:
            result["error"] = f"download {exc.code}"
            return result
    result["error"] = "no url or b64"
    return result


def merge_run_log(results: list[dict], names: list[str]) -> dict:
    prior_path = OUT_DIR / "generation_run.json"
    prior: dict = {}
    if prior_path.is_file():
        try:
            prior = json.loads(prior_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            prior = {}
    by_name = {row["name"]: row for row in prior.get("results", []) if "name" in row}
    for row in results:
        by_name[row["name"]] = {k: v for k, v in row.items() if k != "error"}
    ordered = []
    seen = set()
    for name in list(SHEETS):
        if name in by_name:
            ordered.append(by_name[name])
            seen.add(name)
    for name, row in by_name.items():
        if name not in seen:
            ordered.append(row)
    errors = {row["name"]: row.get("error") for row in results if row.get("error")}
    prior_errors = prior.get("errors") or {}
    if isinstance(prior_errors, dict):
        merged_errors = dict(prior_errors)
        for name in names:
            if name in errors:
                merged_errors[name] = errors[name]
            elif name in merged_errors:
                del merged_errors[name]
    else:
        merged_errors = errors
    return {
        "provider": "xai-oauth",
        "chat_model": CHAT_MODEL,
        "image_model": MODEL,
        "quality": QUALITY,
        "aspect_ratio": ASPECT,
        "fallback_policy": "GPT-5.6 Sol only if Grok returns no answer",
        "fallback_used": False,
        "results": ordered,
        "errors": merged_errors,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", default="", help="comma names")
    parser.add_argument("--workers", type=int, default=2)
    args = parser.parse_args()
    names = [n.strip() for n in args.only.split(",") if n.strip()] or list(SHEETS)
    unknown = [n for n in names if n not in SHEETS]
    if unknown:
        raise SystemExit("unknown sheets: " + ",".join(unknown))
    token = load_token()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    results: list[dict] = []
    print("GENERATE_START", ",".join(names), flush=True)
    workers = max(1, min(args.workers, len(names)))
    if workers == 1:
        for name in names:
            print("GENERATE", name, flush=True)
            row = generate_one(token, name, SHEETS[name])
            results.append(row)
            print(
                "RESULT",
                name,
                "ok" if row["ok"] else "FAIL",
                "http",
                row["http"],
                "bytes",
                row["bytes"],
                flush=True,
            )
            if row.get("error"):
                print("ERROR_SNIP", str(row["error"])[:400], flush=True)
    else:
        with ThreadPoolExecutor(max_workers=workers) as pool:
            futs = {pool.submit(generate_one, token, name, SHEETS[name]): name for name in names}
            for fut in as_completed(futs):
                name = futs[fut]
                row = fut.result()
                results.append(row)
                print(
                    "RESULT",
                    name,
                    "ok" if row["ok"] else "FAIL",
                    "http",
                    row["http"],
                    "bytes",
                    row["bytes"],
                    flush=True,
                )
                if row.get("error"):
                    print("ERROR_SNIP", str(row["error"])[:400], flush=True)
    results.sort(key=lambda r: names.index(r["name"]) if r["name"] in names else 99)
    run_log = merge_run_log(results, names)
    (OUT_DIR / "generation_run.json").write_text(
        json.dumps(run_log, indent=2) + "\n", encoding="utf-8"
    )
    failed = [row["name"] for row in results if not row["ok"]]
    print("FAILED", ",".join(failed) if failed else "none")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
