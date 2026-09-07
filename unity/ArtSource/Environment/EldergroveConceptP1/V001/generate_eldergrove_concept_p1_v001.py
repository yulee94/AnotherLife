#!/usr/bin/env python3
"""Generate Grok Imagine 2D concept sheets for EldergroveConceptP1 V001.

Never prints credentials. Grok 4.6 High / grok-imagine-image-2.0 first.
GPT-5.6 Sol is invoked only if Grok returns no answer.
"""

from __future__ import annotations

import argparse
import base64
import http.client
import json
import os
import sys
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

AUTH_PATH = Path(os.environ.get("LOCALAPPDATA", "")) / "hermes" / "auth.json"
XAI_API = "https://api.x.ai/v1"
OPENAI_API = "https://api.openai.com/v1"
OUT_DIR = Path(__file__).resolve().parent
MODEL = "grok-imagine-image-2.0"
CHAT_MODEL = "grok-4.6"
FALLBACK_CHAT_MODEL = "gpt-5.6-sol"
FALLBACK_IMAGE_MODEL = "gpt-image-1"
QUALITY = "medium"
ASPECT = "3:2"

COMMON = (
    "Premium AA dark-high-fantasy stylized-realistic game-art production sheet. "
    "Geometry-readable orthographic architectural drawing. Strict orthographic cameras, no perspective, no 3/4 hero shot, no isometric dollhouse cutaway. "
    "Neutral seamless gray studio, even studio lighting, no sky, no landscape backdrop except the subject terrain that belongs to the building or cave. "
    "Clean unoccluded views, generous empty gray space between panels, no overlapping objects, no collage, no moodboard, no photobash. "
    "No watermarks, no logos, no UI, no modern objects, no vehicles, no firearms, no plastic, no neon, no glass curtain walls. "
    "No magic VFX, no particles, no lightning bolts, no glow sprites, no baked volumetrics, no bioluminescence. "
    "No animals, no real-world wildlife, no dragons, no bosses, no people except one optional 1.8 m featureless gray mannequin for scale. "
    "Eldergrove only. Moonroot Vigil identity: pale mineral stone, dark timber, aged bronze root collars, desaturated bark, restrained moon-silver and pale-gold edgework. "
    "Asymmetric root-held masses, broad low curves, spaced oldgrowth. Crafted architecture first; a few grounded roots seat into prepared bronze collars. "
    "No bright-green-only cue, no neon bioluminescence, no root portal, no cute sprites, no dense canopy hiding traversal, no floating island, no Open Crown Arbor Town Hall spectacle, no giant civic oculus. "
    "No Stonehold Embermist, no basalt-iron fortress language, no lava, no soot-forge, no Crownlands limestone palace, no Umbral violet fog, no Accordant cherry blossoms, no fifth realm. "
    "No fake facade shells: every building shown is a real enterable volume with readable wall thickness and punched door/window apertures. "
    "Include a simple unlabeled metric scale bar with five equal tick marks and a faint 0.5 m floor grid where the subject is object-scale. "
    "PBR materials, Black-Desert-inspired finish bar, not cartoon, not anime. "
    "Same identity, identical scale in every panel of this sheet."
)

SHEETS: dict[str, str] = {
    "eldergrove_capital_district_plan_v001": (
        COMMON
        + " SUBJECT: Eldergrove inner-realm capital Worldroot as a SINGLE architectural SITE PLAN. "
        "Camera is STRAIGHT DOWN at 90 degrees like a CAD roof plan. Roofs are FLAT 2D footprints only. "
        "NO facades, NO walls in elevation, NO axonometric, NO isometric, NO 3/4, NO floating island diorama, NO cliff sides visible, NO water-ring island. "
        "Organic inner-safe-pocket street grain following spaced oldgrowth and pale mineral shelves: civic spine, herbal quarter, residential clusters, market street, hero-keep precinct. "
        "Not a perfect circle, not a Renaissance grid, not a rectangular walled box, not an oval crater rim. "
        "Walkable pale-stone streets as corridors between dark-timber roof footprints. Every building footprint has a small door-tick gap on the street edge proving enterability. "
        "Hero keep is the largest irregular-but-broad roof at the civic head, held by a few bronze-collar root seats drawn as plan ticks, not a wrapping tree canopy. "
        "One controlled approach road enters from a SINGLE inner-gate edge; do NOT draw outer and inner sequential barriers side by side. "
        "Do not label or invent city names or city IDs. Capital identity is Worldroot only. "
        "Gray studio paper background with faint grid. No countryside, no Worldscar, no bridges, no fortresses, no 30 m apron diagram, no dense canopy overlay."
    ),
    "eldergrove_capital_skyline_north_south_v001": (
        COMMON
        + " SUBJECT: Eldergrove capital Worldroot as TWO FLAT orthographic CITY ELEVATIONS, like architectural drawings of a street wall. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT: NORTH elevation. RIGHT: SOUTH elevation of the same city, same scale, generous gray between. "
        "Composition MUST be a HORIZONTAL ROW: two-storey pale-stone houses with dark timber roofs, then a wide low keep block in the center, then more two-storey houses. "
        "The skyline is a low BROAD curve, about three storeys at the keep and two storeys at the houses. SOUTH may show the main keep door; NORTH the rear walltop. "
        "A few bronze-collared root ribs seat into keep and house corners; they do not hide doors or streets. Spaced pale trunks behind, not a dense canopy wall. "
        "FORBIDDEN: stepped pyramid, ziggurat, volcano, mountain, triangular silhouette, floating island, needle spires, Open Crown oculus, Town Hall spectacle, wrapping treehouse city. "
        "Materials: pale mineral stone, dark timber, aged bronze collars, desaturated bark, moon-silver edges. No dragons, no neon, no Embermist iron."
    ),
    "eldergrove_capital_skyline_east_west_v001": (
        COMMON
        + " SUBJECT: Eldergrove capital Worldroot as TWO FLAT orthographic CITY ELEVATIONS, like architectural drawings of a street wall. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT: EAST elevation. RIGHT: WEST elevation of the same city, same scale, generous gray between. "
        "Composition MUST be a HORIZONTAL ROW: two-storey pale-stone houses with dark timber roofs, then a wide low keep block in the center, then more two-storey houses. "
        "The skyline is a low BROAD curve, about three storeys at the keep and two storeys at the houses. "
        "A few bronze-collared root ribs seat into corners; they do not hide doors. Spaced pale trunks, not dense canopy. "
        "FORBIDDEN: stepped pyramid, ziggurat, volcano, mountain, triangular silhouette, floating island, needle spires, Open Crown oculus, Town Hall spectacle. "
        "Materials: pale mineral stone, dark timber, aged bronze collars, desaturated bark, moon-silver edges. No dragons, no neon, no Embermist iron."
    ),
    "eldergrove_capital_hero_keep_shell_v001": (
        COMMON
        + " SUBJECT: Eldergrove capital HERO KEEP SHELL only, enterable warden-keep volume, not a city, not a sequential dual-gate, not Town Hall. "
        "Three isolated panels left-to-right of the SAME keep: ROOF PLAN; FRONT ELEVATION; RIGHT SIDE ELEVATION. "
        "Low broad warden-keep about 52 m across and 16 m to walltop, three storeys plus defendable walltop walk. Asymmetric but readable mass. "
        "Real wall thickness about 1.2 m, pale mineral stone, dark timber banding, aged bronze root collars at corners, compressed pointed-arch door and window apertures punched fully through the wall. "
        "One obvious main door opening about 3.6 m wide by 4.2 m tall; interior door ticks on plan; stair bulkhead to walltop. "
        "Roots are few, grounded, seated in bronze collars — not a wrapping tree, not a portal, not an Open Crown oculus. "
        "Optional 1.8 m gray mannequin beside FRONT. Empty gray between panels. "
        "No dollhouse cutaway, no missing back wall, no fake facade, no neon, no dragons."
    ),
    "eldergrove_capital_keep_interior_program_v001": (
        COMMON
        + " SUBJECT: developed FURNISHED INTERIOR PROGRAM of the same Eldergrove hero keep. Large streamed combat interior. "
        "TWO isolated TRUE TOP-DOWN FLOOR PLANS with generous gray between them, not a 3/4 cutaway, not a dollhouse, not stacked isometric floors. "
        "LEFT: GROUND FLOOR PLAN — circular moot chamber on axis with a round map table, herbal preparation benches, stores, a walkable root-maintenance alcove that does not block the path, TWO stair cores. "
        "RIGHT: UPPER FLOOR PLAN — archive niches, warden bunks, additional stores. "
        "CRITICAL: the TWO stair cores occupy the EXACT SAME plan coordinates on both floors (for example north-east core and south-west core). Upper stairs sit directly above ground stairs. No missing stairs, no extra unaligned stairs. "
        "Doors as 1.2 m swings, 0.3 m interior partitions, 1.2 m exterior wall, circulation clear for a 1.8 m mannequin path through every room including the root-maintenance alcove. "
        "Furniture unoccluded: map table, herbal benches, jars as simple geometry, archive shelves, bunks, barrels, bronze collar seats as objects not glow. "
        "Show wing breaks as thick walls, not floating rooms. Keep is enterable throughout. Same outer wall silhouette on both floors. "
        "Forbidden: roof ripped off 3/4 view, impossible stacked rooms, missing stairs, misaligned stairs, neon mushrooms, dragons, Open Crown oculus, Town Hall spectacle."
    ),
    "eldergrove_city_street_plan_elevation_v001": (
        COMMON
        + " SUBJECT: typical Eldergrove INNER-CITY STREET KIT, one short block of the same street. Not a named city; no city IDs. "
        "TWO isolated panels of the SAME street with generous gray: LEFT true TOP-DOWN STREET PLAN; RIGHT true STREET ELEVATION looking at the building fronts. "
        "Walkable street about 6 m between facing facades, pale mineral paving, shallow gutters, door ticks on every building. "
        "Mix of house, shop, service, and workshop footprints along one oldgrowth-shelf street, unique Moonroot Vigil cladding, not palette-swaps of Stonehold. "
        "Elevation shows two-storey pale stone, dark timber lintels, bronze collar seats at corners, restrained moon-silver edges, no hanging tavern logos. "
        "No capital keep, no fortress, no sequential gate, no 3/4 view, no people, no dragons, no dense canopy hiding doors."
    ),
    "eldergrove_city_house_module_v001": (
        COMMON
        + " SUBJECT: Eldergrove city HOUSE MODULE, one enterable dwelling, small seamless interior. "
        "Three isolated panels of the SAME house: FRONT ELEVATION; RIGHT SIDE ELEVATION; INTERIOR GROUND PLAN (true top-down, furnished). "
        "Footprint about 8 m by 10 m, two storeys, wall thickness 0.4 m, interior door 1.2 m by 2.4 m, stair to loft marked on plan. "
        "Pale mineral masonry, dark timber lintels, bronze drip collars, desaturated bark seating at one corner only, moon-silver latch. "
        "Plan furniture: hearth, table, benches, storage chests, herbal shelf, beds upstairs indicated by stair, all unoccluded and traversable. "
        "No missing walls, no 3/4 cutaway, no modern kitchen, no dragons, no neon, no glass curtain."
    ),
    "eldergrove_city_shop_module_v001": (
        COMMON
        + " SUBJECT: Eldergrove city SHOP MODULE, one enterable herbal-and-stores storefront, small seamless interior. "
        "Three isolated panels of the SAME shop: FRONT ELEVATION with a wide serving hatch and dark-timber shutter; RIGHT SIDE ELEVATION; INTERIOR PLAN (true top-down). "
        "Footprint about 8 m by 12 m, counter, public floor, rear stores, stair to keeper loft. "
        "Moonroot materials: pale stone, dark timber shutter, bronze hatch bar, simple jar/shelf geometry, no glass shopfront. "
        "Plan shows counter clearance, crate stores, door to street and door to rear, traversable aisles. "
        "No 3/4 cutaway, no neon, no logos, no cute potion sprites, no dragons."
    ),
    "eldergrove_city_service_module_v001": (
        COMMON
        + " SUBJECT: Eldergrove city SERVICE MODULE, one enterable public cistern-and-herb-stores house, small seamless interior. "
        "Three isolated panels of the SAME building: FRONT ELEVATION; RIGHT SIDE ELEVATION; INTERIOR PLAN (true top-down). "
        "Footprint about 10 m by 12 m. Plan: covered pale-stone cistern basin, bucket bench, dry-herb stores, service counter, rear yard door. "
        "Pale mineral stone, dark timber roof, aged bronze grate, no pipes as modern plumbing, no ceramic tile bathrooms. "
        "Traversable aisles, 1.2 m doors, unoccluded fixtures. No 3/4 cutaway, no dragons, no neon, no root portal."
    ),
    "eldergrove_city_workshop_module_v001": (
        COMMON
        + " SUBJECT: Eldergrove city WORKSHOP MODULE, one enterable root-maintenance and herbal-prep workshop, small seamless interior. "
        "Three isolated panels of the SAME workshop: FRONT ELEVATION with wagon-wide work door about 2.4 m; RIGHT SIDE ELEVATION with timber vent hood; INTERIOR PLAN (true top-down). "
        "Footprint about 12 m by 14 m. Plan: two workbenches, herbal still as a copper/bronze vessel not a magic cauldron, bark-lashing rack, bronze-collar joinery jig, dried-herb racks, clear 2 m work aisle. "
        "Pale stone hearth mass, dark timber hood, aged bronze clamps, desaturated bark stock as material not a living portal. "
        "NOT a smithy, no anvils, no coal bunker, no magma, no 3/4 cutaway, no dragons, no neon."
    ),
    "eldergrove_inner_cave_mouth_v001": (
        COMMON
        + " SUBJECT: Eldergrove NON-DRAGON INNER-REALM cave MOUTH only, moonroot refuge-gallery language, not a warzone, not a lair. "
        "Two isolated panels of the SAME mouth: FRONT ELEVATION of the opening in a pale mineral shelf; RIGHT SIDE ELEVATION showing overhang depth and lintel thickness. "
        "Human-scale mouth about 4.5 m wide by 3.6 m tall, dark-timber and bronze-collar square lintel, shallow cut-stone threshold, walkable packed-silt floor. "
        "Organic inner safe-pocket geology: layered pale mineral, desaturated root ribs seated in bronze clamps, no carved dragon skull, no treasure hoard, no eggs, no bones, no neon mushrooms. "
        "Optional 1.8 m gray mannequin at the threshold. No fortress, no sequential gate, no 30 m apron diagram, no water portal."
    ),
    "eldergrove_inner_cave_section_circulation_v001": (
        COMMON
        + " SUBJECT: Eldergrove NON-DRAGON INNER cave, SAME volume as a refuge moonroot gallery. "
        "Two isolated panels with generous gray: LEFT LONGITUDINAL SECTION; RIGHT CIRCULATION PLAN (true top-down). "
        "Organic pocket topology: entry gallery, two side alcoves, a roundish main chamber, a rear store niche, all connected by walkable floors with no unauthored holes. "
        "Ceiling heights readable in section (gallery 3.2 m, main chamber 5.5 m). Stair or ramp only if it lands. "
        "Plan shows a single obvious loop-free refuge path plus one return alcove; doors/portcullis optional at mouth only. "
        "Packed-silt floors, pale mineral ribs, dark timber props, bronze clamps, practical moon-silver lamps as objects not glow sprites. "
        "Forbidden: dragon nest, boss arena, neon, 3/4 cutaway collage, impossible stacked voids, flying ledges without path, root portal."
    ),
    "eldergrove_inner_cave_chamber_kit_v001": (
        COMMON
        + " SUBJECT: Eldergrove NON-DRAGON INNER cave CHAMBER KIT, three isolated modular pieces of the SAME construction language, identical scale, generous gray, not overlapping. "
        "LEFT: straight gallery bay 8 m long, 4 m wide, 3.2 m high, timber props, pale mineral ribs, bronze clamps. "
        "CENTER: node chamber about 10 m across, 5.5 m high, one entrance and two exits at 120-degree offsets, walkable floor. "
        "RIGHT: side alcove 4 m deep with store shelves and a lamp object. "
        "True orthographic FRONT SECTION of each module, like a construction kit, not three different biomes. "
        "No dragons, no neon, no collage overlap, no people except optional mannequin in the gallery bay."
    ),
    "eldergrove_outer_cave_mouth_v001": (
        COMMON
        + " SUBJECT: Eldergrove NON-DRAGON OUTER-WARZONE cave MOUTH only, combat-scarred mineral cut, not a dragon lair, not a fortress. "
        "Two isolated panels of the SAME mouth: FRONT ELEVATION; RIGHT SIDE ELEVATION. "
        "Mouth about 6 m wide by 4.2 m tall, broken bronze-collar lintel, spalled pale mineral, silt slope that is NOT a climbable talus ramp onto a wall. "
        "War-worn but still a human dungeon entrance. Optional 1.8 m mannequin. "
        "Do not show a fortress, sequential gate, 30 m apron, Worldscar, or capital. About three minutes from the dual-gate in V013 — do not draw the gate. "
        "No dragon skull, no eggs, no neon, no modern sandbags."
    ),
    "eldergrove_outer_cave_section_combat_circulation_v001": (
        COMMON
        + " SUBJECT: Eldergrove NON-DRAGON OUTER-WARZONE cave combat circulation of ONE dungeon. "
        "Two isolated panels: LEFT LONGITUDINAL SECTION; RIGHT COMBAT CIRCULATION PLAN (true top-down). "
        "Plan: mouth choke, split gallery, cross-chamber with cover blocks (mineral ribs, overturned timber carts as geometry not modern vehicles), looping return corridor, rear hold. "
        "Sightlines and 2 m combat clearance readable. No boss podium, no circular arena with a dragon perch. "
        "Section shows 3.5–6 m ceilings, no unauthored pits, ramps that land. "
        "Pale mineral, desaturated bark shoring, aged bronze clamps, moon-silver only on clamp plates. "
        "Forbidden: dragons, neon, 3/4 dollhouse, fortress walls, sequential dual-gate, root portal."
    ),
    "eldergrove_outer_cave_chamber_kit_v001": (
        COMMON
        + " SUBJECT: Eldergrove NON-DRAGON OUTER-WARZONE cave CHAMBER KIT, three isolated modular pieces, identical scale, generous gray. "
        "LEFT: combat gallery bay 10 m long with side cover ribs. "
        "CENTER: cross-chamber node about 14 m across with two sightline corners and a 2 m clear middle. "
        "RIGHT: hold/alcove with bronze-shored stores and a barred inner door. "
        "True orthographic FRONT SECTION of each module, same Moonroot Vigil pale-stone / dark-timber / bronze-collar language. "
        "No dragon nest, no neon, no overlapping collage, no fortress apron, no people except optional mannequin in the gallery."
    ),
}


def load_auth() -> dict:
    return json.loads(AUTH_PATH.read_text(encoding="utf-8"))


def load_xai_token(auth: dict) -> str:
    token = auth["providers"]["xai-oauth"]["tokens"]["access_token"]
    if not token:
        raise SystemExit("missing xai-oauth access_token")
    return token


def load_openai_token(auth: dict) -> str | None:
    provider = auth.get("providers", {}).get("openai-codex") or {}
    tokens = provider.get("tokens") or {}
    token = tokens.get("access_token") or provider.get("api_key")
    return token or None


def post_json(url: str, token: str, payload: dict, extra_headers: dict | None = None) -> tuple[int, dict | str]:
    data = json.dumps(payload).encode("utf-8")
    headers = {
        "Authorization": "Bearer " + token,
        "Content-Type": "application/json",
        "Accept": "application/json",
    }
    if extra_headers:
        headers.update(extra_headers)
    req = urllib.request.Request(url, data=data, method="POST", headers=headers)
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
    except (http.client.RemoteDisconnected, http.client.IncompleteRead, TimeoutError, OSError) as exc:
        return 0, f"urlerror:{type(exc).__name__}:{exc}"


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


def write_image_payload(dest: Path, data0: dict) -> tuple[int, str | None]:
    url = data0.get("url")
    b64 = data0.get("b64_json")
    revised = data0.get("revised_prompt")
    if b64:
        blob = base64.b64decode(b64)
        dest.write_bytes(blob)
        return len(blob), revised
    if url:
        return download(url, dest), revised
    return 0, revised


def generate_grok(token: str, name: str, prompt: str) -> dict:
    dest = OUT_DIR / f"{name}.png"
    payload = {
        "model": MODEL,
        "prompt": prompt,
        "n": 1,
        "aspect_ratio": ASPECT,
        "quality": QUALITY,
        "response_format": "b64_json",
    }
    status, body = post_json(XAI_API + "/images/generations", token, payload)
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
        "fallback_provider": None,
        "fallback_model": None,
    }
    if status != 200:
        result["error"] = body if isinstance(body, str) else json.dumps(body)[:800]
        return result
    data = body.get("data") if isinstance(body, dict) else None
    if not data:
        result["error"] = "no data"
        return result
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    try:
        nbytes, revised = write_image_payload(dest, data[0])
    except urllib.error.HTTPError as exc:
        result["error"] = f"download {exc.code}"
        return result
    if revised:
        result["revised_prompt"] = revised[:400]
    result["bytes"] = nbytes
    result["ok"] = nbytes > 1000
    if not result["ok"]:
        result["error"] = "no url or b64"
    return result


def generate_gpt_sol(token: str, name: str, prompt: str) -> dict:
    dest = OUT_DIR / f"{name}.png"
    payload = {
        "model": FALLBACK_IMAGE_MODEL,
        "prompt": prompt,
        "n": 1,
        "size": "1536x1024",
        "quality": "high",
    }
    status, body = post_json(OPENAI_API + "/images/generations", token, payload)
    result = {
        "name": name,
        "http": status,
        "path": str(dest),
        "bytes": 0,
        "ok": False,
        "provider": "openai-codex",
        "chat_model": FALLBACK_CHAT_MODEL,
        "image_model": FALLBACK_IMAGE_MODEL,
        "quality": "high",
        "aspect_ratio": ASPECT,
        "fallback": True,
        "fallback_provider": "openai-codex",
        "fallback_model": FALLBACK_CHAT_MODEL,
    }
    if status != 200:
        result["error"] = body if isinstance(body, str) else json.dumps(body)[:800]
        return result
    data = body.get("data") if isinstance(body, dict) else None
    if not data:
        result["error"] = "gpt-sol no data"
        return result
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    b64 = data[0].get("b64_json")
    url = data[0].get("url")
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
            result["error"] = f"gpt-sol download {exc.code}"
            return result
    result["error"] = "gpt-sol no url or b64"
    return result


def generate_one(xai_token: str, openai_token: str | None, name: str, prompt: str) -> dict:
    try:
        row = generate_grok(xai_token, name, prompt)
    except Exception as exc:  # noqa: BLE001 — fail closed into fallback
        row = {
            "name": name,
            "http": 0,
            "path": str(OUT_DIR / f"{name}.png"),
            "bytes": 0,
            "ok": False,
            "provider": "xai-oauth",
            "chat_model": CHAT_MODEL,
            "image_model": MODEL,
            "quality": QUALITY,
            "aspect_ratio": ASPECT,
            "fallback": False,
            "error": f"urlerror:{type(exc).__name__}:{exc}",
        }
    grok_no_answer = (not row["ok"]) and (
        row.get("http") in (0, 401, 403, 429, 500, 502, 503, 529)
        or str(row.get("error", "")).startswith("urlerror:")
        or row.get("error") in ("no data", "no url or b64")
    )
    if grok_no_answer and openai_token:
        try:
            fallback = generate_gpt_sol(openai_token, name, prompt)
        except Exception as exc:  # noqa: BLE001
            fallback = {
                "name": name,
                "http": 0,
                "path": str(OUT_DIR / f"{name}.png"),
                "bytes": 0,
                "ok": False,
                "provider": "openai-codex",
                "chat_model": FALLBACK_CHAT_MODEL,
                "image_model": FALLBACK_IMAGE_MODEL,
                "quality": "high",
                "aspect_ratio": ASPECT,
                "fallback": True,
                "fallback_provider": "openai-codex",
                "fallback_model": FALLBACK_CHAT_MODEL,
                "error": f"urlerror:{type(exc).__name__}:{exc}",
            }
        fallback["grok_error"] = str(row.get("error", ""))[:400]
        fallback["grok_http"] = row.get("http")
        return fallback
    return row


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
    fallback_used = any(row.get("fallback") for row in ordered)
    return {
        "provider": "xai-oauth",
        "chat_model": CHAT_MODEL,
        "image_model": MODEL,
        "quality": QUALITY,
        "aspect_ratio": ASPECT,
        "fallback_policy": "GPT-5.6 Sol only if Grok returns no answer",
        "fallback_used": fallback_used,
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
    auth = load_auth()
    xai_token = load_xai_token(auth)
    openai_token = load_openai_token(auth)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    results: list[dict] = []
    print("GENERATE_START", ",".join(names), flush=True)
    workers = max(1, min(args.workers, len(names)))
    if workers == 1:
        for name in names:
            print("GENERATE", name, flush=True)
            row = generate_one(xai_token, openai_token, name, SHEETS[name])
            results.append(row)
            print(
                "RESULT",
                name,
                "ok" if row["ok"] else "FAIL",
                "http",
                row["http"],
                "bytes",
                row["bytes"],
                "fallback" if row.get("fallback") else "grok",
                flush=True,
            )
            if row.get("error"):
                print("ERROR_SNIP", str(row["error"])[:400], flush=True)
    else:
        with ThreadPoolExecutor(max_workers=workers) as pool:
            futs = {
                pool.submit(generate_one, xai_token, openai_token, name, SHEETS[name]): name
                for name in names
            }
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
                    "fallback" if row.get("fallback") else "grok",
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
