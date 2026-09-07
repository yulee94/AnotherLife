#!/usr/bin/env python3
"""Generate Grok Imagine 2D concept sheets for CrownlandsConceptP1 V001.

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
    "Crownlands only. Meridian Oathroad identity: grounded chalk-gold ashlar limestone, deep blue slate roofs, brass meridian ribs and compass-rose crests, cool silver edgework, restrained bronze, rain-dark mortar fractures. "
    "Broad civic masses, readable construction, stout axial drums with blue-slate pavilion roofs — NOT delicate needle spires, NOT a gothic cathedral, NOT an ungrounded white-marble palace, NOT excess gold leaf. "
    "Segmented meridian arches, ordered tower rhythm, chalk ribs, storm-pressed oathroad grit at ground only. "
    "No Stonehold Embermist, no basalt-iron fortress language, no lava, no soot-forge, no Eldergrove roots or bronze root collars, no Moonroot canopy, no Umbral violet fog or smoked-glass, no Accordant cherry blossoms, no fifth realm. "
    "No fake facade shells: every building shown is a real enterable volume with readable wall thickness and punched door/window apertures. "
    "No copied proprietary Black Desert Online building forms; BDO is finish-bar inspiration only. "
    "No Town Hall spectacle, no Workshop redesign, no kingdom-management prefab language. "
    "Include a simple unlabeled metric scale bar with five equal tick marks and a faint 0.5 m floor grid where the subject is object-scale. "
    "Clear short English panel titles only (PLAN, NORTH, SOUTH, FRONT, SIDE, SECTION). No garbled numerals. "
    "PBR materials, Black-Desert-inspired finish bar, not cartoon, not anime. "
    "Same identity, identical scale in every panel of this sheet."
)

SHEETS: dict[str, str] = {
    "crownlands_capital_district_plan_v001": (
        COMMON
        + " SUBJECT: Crownlands inner-realm capital Crownspire as a SINGLE architectural SITE PLAN. "
        "Camera STRAIGHT DOWN at 90 degrees, CAD roof plan. Roofs are FLAT 2D footprints only. "
        "NO facades, NO elevation walls, NO axonometric, NO isometric, NO 3/4 hero, NO floating island diorama. "
        "COMPOSITION IS THE HERO REQUIREMENT. The city is an ORGANIC CORNER-ENCLOSING IRREGULAR L-SHAPED or WEDGE-SHAPED urban pocket, a hillside town built into a quarry elbow, NOT a round mesa. "
        "NORTH: one long IMPASSABLE chalk-cliff / terrain wall, irregular broken edge, running left-to-right, NOT a circular arc. "
        "EAST: a second IMPASSABLE chalk-cliff / terrain wall running top-to-bottom, meeting the north cliff in a rough right-angle or obtuse CORNER. THESE TWO CLIFFS ONLY. They do NOT wrap south. They do NOT wrap west. They do NOT complete a loop. "
        "SOUTH and WEST are OPEN: broken offset terraces and streets spill toward EMPTY gray studio paper. One controlled approach road enters from the SOUTH-WEST open side. No cliff, no ring-wall, no moat on south or west. "
        "The built mass hugs the north+east cliff corner and occupies an L, a 7, a boot, or a thick wedge. The BOTTOM-RIGHT third of the sheet is EMPTY gray paper. The city does NOT float in the center as a blob. "
        "Overall silhouette of built-plus-cliff mass: L / wedge / boot. NEVER a circle, oval, egg, potato, disk, island, donut, crater, or round mesa. NEVER a cliff perimeter all around. "
        "STREETS: broken OFFSET terraces, doglegs, pinches, jogs, nonradial grain. L-shaped and staggered blocks. Walkable chalk-gold ashlar corridors between deep-blue-slate roof footprints. Door-tick gap on every street edge. "
        "ABSOLUTELY FORBIDDEN GEOMETRY: circular island, oval island, floating disk, ring perimeter, cliff all around, crater rim, round wall, oval wall, concentric rings, radial spokes, compass-rose street plan, Renaissance ideal city, Palmanova, keep-centered ring road, cloister. "
        "Hero keep Crownspire is the largest BROAD RECTANGULAR roof parked at the NORTH-EAST cliff-head (inside the L), not the geometric center of a round town: stout rectangular-plus-blunt-octagon-drum mass, NOT a needle-spire cluster, NOT a cathedral nave, NOT a round dome in a round city. Brass meridian crest ticks on the keep roof only. "
        "One controlled approach from the open south-west. Do NOT draw outer and inner sequential barriers side by side. "
        "The ONLY allowed identity label is capital_crownspire. Do not label or invent city names or city IDs. No other text names. "
        "Gray studio paper, faint grid. No countryside panorama, no Worldscar, no bridges, no fortresses, no 30 m apron diagram."
    ),
    "crownlands_capital_skyline_north_south_v001": (
        COMMON
        + " SUBJECT: Crownlands capital Crownspire as TWO FLAT orthographic CITY ELEVATIONS, like architectural drawings of a street wall. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT panel title NORTH. RIGHT panel title SOUTH. Same city, same scale, generous gray between. "
        "Composition MUST be a HORIZONTAL ROW of BROAD LOW civic masses: varied two-storey chalk-gold ashlar houses with STEPPED deep-blue-slate HIPPED roofs of different widths, then a WIDE LOW keep block, then more two-storey houses that are NOT a mirror of the first group. "
        "Keep is a broad horizontal chalk-gold civic hall about three to four storeys, hip-roofed, with BLUNT observatory drums — short thick cylinders with FLAT or very-low pavilion caps and thin brass meridian ribs. SOUTH shows the main keep door under a SEGMENTAL (not pointed) meridian arch; NORTH is the rear walltop and MUST be asymmetric to south. "
        "REQUIRED ROOFS: stepped blue-slate hipped roofs with hips and ridges. NO cones. NO witch-hat caps. NO church gables. NO needle finials. "
        "ABSOLUTELY FORBIDDEN: Gothic cathedral, pointed arches, rose windows, wheel windows, paired conical turrets, matching twin towers, needle finials, pinnacles, flying buttresses, lancet windows, mirror bilateral symmetry, chapel front, gold-leaf spectacle. "
        "Houses are ordinary civic dwellings with rectangular punched windows and doors, not chapel bays. Restrained brass and cool silver only. "
        "Materials: chalk-gold ashlar, deep blue slate, brass meridian ribs, cool silver edges, rain-dark mortar. No dragons, no neon, no Embermist iron, no root collars."
    ),
    "crownlands_capital_skyline_east_west_v001": (
        COMMON
        + " SUBJECT: Crownlands capital Crownspire as TWO FLAT orthographic CITY ELEVATIONS, like architectural drawings of a street wall. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT panel title EAST. RIGHT panel title WEST. Same city, same scale, generous gray between. "
        "Composition MUST be a HORIZONTAL ROW of BROAD LOW civic masses: varied two-storey chalk-gold ashlar houses with STEPPED deep-blue-slate HIPPED roofs of different widths, then a WIDE LOW keep block, then more two-storey houses that are NOT a mirror of the first group. "
        "Keep is a broad horizontal chalk-gold civic hall about three to four storeys, hip-roofed, with BLUNT observatory drums — short thick cylinders with FLAT or very-low pavilion caps and thin brass meridian ribs. EAST and WEST must be related but NOT identical copies; door placement and house counts may differ. "
        "REQUIRED ROOFS: stepped blue-slate hipped roofs with hips and ridges. NO cones. NO witch-hat caps. NO church gables. NO needle finials. "
        "ABSOLUTELY FORBIDDEN: Gothic cathedral, pointed arches, rose windows, wheel windows, paired conical turrets, matching twin towers, needle finials, pinnacles, flying buttresses, lancet windows, mirror bilateral symmetry, chapel front, gold-leaf spectacle. "
        "Houses are ordinary civic dwellings with rectangular punched windows and doors, not chapel bays. Restrained brass and cool silver only. "
        "Materials: chalk-gold ashlar, deep blue slate, brass meridian ribs, cool silver edges, rain-dark mortar. No dragons, no neon, no Embermist iron, no root collars."
    ),
    "crownlands_capital_keep_shell_v001": (
        COMMON
        + " SUBJECT: Crownlands capital CROWN SPIRE HERO KEEP SHELL — a BROAD LOW RECTANGULAR OFFSET CIVIC COUNCIL-KEEP matching the furnished ground/upper plans. "
        "NOT a castle, NOT a fortress, NOT a cathedral, NOT Town Hall, NOT a fairy-tale keep, NOT a city. "
        "Three isolated panels left-to-right of the SAME keep: ROOF PLAN; FRONT ELEVATION; RIGHT SIDE ELEVATION. "
        "Massing: a WIDE LOW horizontal chalk-gold ashlar civic hall about 56 m across and 18 m to walltop, three storeys plus defendable walltop walk. Think palazzo / civic record-hall, not Disney castle. "
        "FOUR BLUNT OCTAGONAL corner drums/pavilions: short thick eight-sided shafts, faceted, not cylinders. Each wears a LOW STEPPED or HIPPED deep-blue-slate pavilion cap — almost a shallow hip/pyramid, NEVER a cone, NEVER a witch-hat, NEVER a needle. "
        "Main roof is a LOW STEPPED/HIPPED deep-blue-slate roof with hips and ridges, horizontal, not a steep church gable. Thin brass meridian ribs on the octagon corners and hall corners. Cool silver edges. "
        "ROOF PLAN must match the walltop program: filled rectangular walltop with merlon ticks, SW and NE stair bulkheads, FOUR chamfered OCTAGONAL corner pavilions with shallow blue-slate hipped caps. Stair bulkheads prove interior floors exist. NOT a hollow square courtyard donut. NOT two round towers. "
        "FRONT: calm grid of RECTANGULAR punched windows with flat lintels, three storeys. South main door about 3.6 m wide by 4.2 m tall under a SEGMENTAL (shallow round, not pointed) brass-ribbed meridian arch. Real ~1.4 m wall thickness. Punched dark opening. Optional 1.8 m gray mannequin. "
        "RIGHT SIDE: same keep, same scale, long hall plus octagonal drums, low hipped caps. "
        "ABSOLUTELY FORBIDDEN: round cylindrical towers, conical roofs, witch-hat caps, needle finials, gold spikes, ball-and-spike finials, Gothic cathedral, pointed arches, lancet windows, rose windows, flying buttresses, pinnacles, paired matching round turrets, hollow courtyard castle, steep nave gable as the hero silhouette. "
        "ZERO vertical spikes on any roof. Pavilion caps are truncated blunt hips, as if the peak was cut off flat. If a brass compass exists it is a FLAT disc lying on the roof, never a finial. "
        "Empty gray between panels. No dollhouse cutaway, no missing back wall, no fake facade, no neon, no dragons."
    ),
    "crownlands_capital_keep_section_v001": (
        COMMON
        + " SUBJECT: Crownlands Crownspire hero keep as TWO TRUE ARCHITECTURAL SECTIONS of the SAME enterable CIVIC keep as the shell and ground/upper plans. "
        "LEFT: LONGITUDINAL SECTION cutting the long axis through vestibule, court, and rear vault. "
        "RIGHT: CROSS SECTION cutting the short axis through the strategy chamber and stair core. "
        "SAME OUTER SILHOUETTE as the civic keep shell: BROAD LOW RECTANGULAR chalk-gold hall, about 56 m across and 18 m to walltop, three occupied floors plus walltop. "
        "Ends show BLUNT OCTAGONAL drums as short thick eight-sided palazzo pavilions with TRUNCATED LOW HIPPED blue-slate caps — the cap peak is cut off almost flat, like a stump hip, NOT a cone, NOT a witch-hat, NOT a needle, NOT a round turret. "
        "Roofs in section are LOW STEPPED/HIPPED blue-slate of real thickness. The CROSS SECTION roof is a LOW HIP with a SHORT HORIZONTAL ridge, eaves left and right — NEVER a steep church gable triangle, NEVER a Gothic vault, NEVER a pointed nave. "
        "Generous gray between. Strict orthographic section, hatched wall thickness ~1.4 m, floors as slabs, stairs that LAND on each floor. "
        "Punched window openings in section as true voids, not painted glass. Brass meridian rib as a structural band, not a glow. Compass-rose only as a FLAT roof crest object, not a needle spire or gold spike. "
        "Windows in section are RECTANGULAR punched voids with flat lintels, never lancets. "
        "ZERO vertical spikes: no needle finials, no gold crosses, no ball-and-spike, no crockets. Compass-rose only as a FLAT disc on a hip, never a peak spike. Pavilion caps truncated blunt. "
        "No 3/4 dollhouse, no ripped-off roof isometric, no missing floors, no cathedral vault spectacle, no conical turrets, no people except optional 1.8 m mannequin."
    ),
    "crownlands_capital_keep_ground_plan_v001": (
        COMMON
        + " SUBJECT: developed FURNISHED GROUND-FLOOR PLAN of the same Crownlands Crownspire hero keep. Large streamed combat interior. "
        "ONE isolated TRUE TOP-DOWN GROUND FLOOR PLAN filling the sheet with generous gray margin, not a 3/4 cutaway, not a dollhouse, not stacked isometric floors. "
        "Program: formal vestibule on the south door axis, court hall with a round strategy table, clerks' desks to the sides, service corridor, guarded vault room with a circular steel door as geometry not a bank vault logo, TWO stair cores. "
        "Doors as 1.2 m swings, 0.3 m interior partitions, 1.4 m exterior wall, circulation clear for a 1.8 m mannequin path through every room including the vault anteroom. "
        "Furniture unoccluded: round table, benches, clerk desks, archive carts, barrels, brass meridian floor inlay as a flat disc not glow. "
        "Show wing breaks as thick walls, not floating rooms. Keep is enterable throughout. "
        "Stair cores labeled only as geometry (stair rectangles) at north-east and south-west — these same coordinates must be rebuildable on the upper sheet. "
        "Forbidden: roof ripped off 3/4 view, impossible stacked rooms, missing stairs, neon, dragons, cathedral pews, Town Hall spectacle."
    ),
    "crownlands_capital_keep_upper_circulation_v001": (
        COMMON
        + " SUBJECT: developed FURNISHED UPPER FLOOR plus WALLTOP CIRCULATION of the same Crownlands Crownspire hero keep. "
        "TWO isolated TRUE TOP-DOWN PLANS with generous gray between them, not a 3/4 cutaway, not a dollhouse. "
        "LEFT: UPPER FLOOR PLAN — record galleries with shelves, strategy chamber, clerks' overflow, same TWO stair cores at the EXACT SAME plan coordinates as a ground floor (north-east core and south-west core). Upper stairs sit directly above ground stairs. No missing stairs, no extra unaligned stairs. "
        "RIGHT: WALLTOP PLAN — defendable walk, merlon ticks, stair bulkheads landing from the same two cores, brass meridian crest as a roof object not a glow. "
        "Doors as 1.2 m swings, 0.3 m interior partitions, 1.4 m exterior wall, circulation clear. "
        "Same outer wall silhouette as the ground plan and keep shell. "
        "Forbidden: roof ripped off 3/4 view, contradictory stairs, needle spires, neon, dragons, Open Crown, Town Hall spectacle."
    ),
    "crownlands_city_street_grammar_v001": (
        COMMON
        + " SUBJECT: typical Crownlands INNER-CITY STREET GRAMMAR, one short block of the same 6 m street. Not a named city; no city IDs. "
        "TWO isolated panels of the SAME street with generous gray: LEFT true TOP-DOWN STREET PLAN; RIGHT true STREET ELEVATION looking at the building fronts. "
        "Walkable street about 6 m between facing facades, chalk-gold ashlar paving, shallow gutters, OFFSET / staggered building line (not one palace wall). Door ticks on every building. "
        "CRITICAL: FOUR clearly SEPARATE asymmetric enterable modules along the street, each a different width and eaves height, with visible party-wall joints or gaps: "
        "(1) DWELLING — two-storey house, ordinary rectangular punched door and windows; "
        "(2) MARKET — wide serving hatch and shutter, shop counter visible, not a church; "
        "(3) SERVICE — utilitarian cistern or yard door, bucket bench, no chapel; "
        "(4) PUBLIC HALL — wider civic door in a hall mass, still a hall not a chapel. "
        "Elevation shows ordinary rectangular punched openings, chalk-gold ashlar, deep blue slate HIPPED roofs, brass meridian rib at corners, restrained silver edges. "
        "ABSOLUTELY FORBIDDEN: church frontage, chapel, cathedral, row of pointed gables, rose windows, unified palatial terrace, single civic palace, needle finials, gothic tracery, compass-rose as church ornament. "
        "No capital keep, no fortress, no sequential gate, no 3/4 view, no people, no dragons, no Town Hall, no Workshop redesign, no needle spires."
    ),
    "crownlands_city_dwelling_shell_v001": (
        COMMON
        + " SUBJECT: Crownlands city DWELLING SHELL only, one enterable house volume, not furnished. "
        "Three isolated panels of the SAME house: ROOF PLAN; FRONT ELEVATION; RIGHT SIDE ELEVATION. "
        "Footprint about 8 m by 10 m, two storeys, wall thickness 0.4 m, interior door 1.2 m by 2.4 m, stair bulkhead on roof plan proving an interior void. "
        "Chalk-gold ashlar masonry, deep blue slate roof, brass drip rib at one corner only, silver latch. Punched door and windows fully through the wall. "
        "Optional 1.8 m gray mannequin at FRONT. Empty gray between panels. "
        "No missing walls, no 3/4 cutaway, no furniture in this sheet, no modern kitchen, no dragons, no neon, no glass curtain, no needle spire."
    ),
    "crownlands_city_dwelling_interior_v001": (
        COMMON
        + " SUBJECT: Crownlands city DWELLING FURNISHED INTERIOR of the SAME 8 m by 10 m two-storey house. Small seamless interior. "
        "ONLY TWO isolated TRUE TOP-DOWN PLANS with generous gray between them. No extra elevations, no extra sections, no material swatch row. "
        "LEFT panel title GROUND PLAN. RIGHT panel title UPPER PLAN. "
        "CRITICAL STAIR RULE: one single stair rectangle occupies the EAST wall on BOTH floors — the upper stair sits directly above the ground stair at the identical plan coordinate. "
        "If ground stair is against the right/east wall, upper stair is also against the right/east wall. No west-side stair. No extra unaligned stair. "
        "Ground: west hearth, center table and benches, east stair, storage chests, south door swing. "
        "Upper: east stair landing, two beds, chests, same outer wall rectangle. "
        "Wall thickness 0.4 m, doors 1.2 m, all furniture unoccluded and traversable. Same rectangular outer wall silhouette on both floors. "
        "Chalk-gold ashlar, timber furniture, brass latch hardware as objects not glow. Hip roof implied only by wall rectangle, not drawn as a chapel gable. "
        "No 3/4 cutaway, no missing walls, no modern kitchen, no contradictory stairs, no chapel front, no dragons, no neon."
    ),
    "crownlands_city_market_service_public_hall_kit_v001": (
        COMMON
        + " SUBJECT: Crownlands city MARKET / SERVICE / PUBLIC-HALL modular kit, three isolated enterable modules, identical scale, generous gray, not overlapping. "
        "NOT a Town Hall redesign. NOT a Workshop redesign. NOT a kingdom-management prefab. "
        "LEFT column labeled MARKET: front elevation with a wide serving hatch and deep-blue-slate shutter plus a small interior plan. Counter, public floor, rear stores. Chalk-gold ashlar. Traversable aisles. "
        "CENTER column labeled SERVICE: front elevation plus interior plan. Public cistern basin, bucket bench, dry stores, service counter, rear yard door. No modern plumbing. Traversable aisles. "
        "RIGHT column labeled PUBLIC HALL: front elevation plus interior plan of a single public room with benches, a clerk table, and a traversable aisle. "
        "PUBLIC HALL APERTURE IS THE HERO DETAIL and must be readable at a glance: a CLEAR OPENING labeled in English '2.5 m W x 3.0 m H' with dimension arrows on the door itself. "
        "Paired timber leaves (two door boards). Each leaf has LONG iron strap hinges that run most of the way across that leaf. A sliding iron latch bar crosses both leaves. Stone-block frame: ashlar jambs and lintel as distinct blocks. "
        "True orthographic FRONT plus a small true top-down plan under each elevation, same Meridian Oathroad cladding. Three modules must be visually distinct in both elevation and plan. "
        "No 3/4 dollhouse, no Town Hall spectacle, no smithy, no anvils, no needle spires, no dragons, no neon, no logos, no church, no rose window."
    ),
    "crownlands_inner_cave_mouth_v001": (
        COMMON
        + " SUBJECT: Crownlands NON-DRAGON INNER-REALM cave MOUTH only, reliquary-crypt chalk gallery language, not a warzone, not a lair, not a cathedral crypt spectacle. "
        "Two isolated panels of the SAME mouth: FRONT ELEVATION of the opening in a chalk rib shelf; RIGHT SIDE ELEVATION showing overhang depth and lintel thickness. "
        "Human-scale mouth about 4.5 m wide by 3.6 m tall, chalk-gold ashlar square lintel with a modest brass meridian tick, shallow cut-stone threshold, walkable packed-chalk floor. "
        "Organic inner safe-pocket geology: layered pale chalk, rain-dark fractures, no carved dragon skull, no treasure hoard, no eggs, no bones, no neon, no stained-glass rose. "
        "Optional 1.8 m gray mannequin at the threshold. No fortress, no sequential gate, no 30 m apron diagram."
    ),
    "crownlands_inner_cave_section_circulation_v001": (
        COMMON
        + " SUBJECT: Crownlands NON-DRAGON INNER cave, SAME volume as a reliquary chalk gallery. "
        "Two isolated panels with generous gray: LEFT LONGITUDINAL SECTION; RIGHT CIRCULATION PLAN (true top-down). "
        "Organic pocket topology: entry gallery, two side alcoves, a roundish main chamber, a rear store niche, all connected by walkable floors with no unauthored holes. "
        "Ceiling heights readable in section (gallery 3.2 m, main chamber 5.5 m). Stair or ramp only if it lands. "
        "Plan shows a single obvious loop-free refuge path plus one return alcove; doors/portcullis optional at mouth only. "
        "Packed-chalk floors, pale chalk ribs, timber props, brass clamp plates, practical lamps as objects not glow sprites. "
        "Forbidden: dragon nest, boss arena, neon, 3/4 cutaway collage, impossible stacked voids, flying ledges without path, cathedral crypt nave."
    ),
    "crownlands_outer_cave_mouth_v001": (
        COMMON
        + " SUBJECT: Crownlands NON-DRAGON OUTER-WARZONE cave MOUTH only, storm-shelf combat-scarred chalk cut, not a dragon lair, not a fortress. "
        "Two isolated panels of the SAME mouth: FRONT ELEVATION; RIGHT SIDE ELEVATION. "
        "Mouth about 6 m wide by 4.2 m tall, broken brass-rib lintel, spalled chalk-gold ashlar, silt slope that is NOT a climbable talus ramp onto a wall. "
        "War-worn but still a human dungeon entrance. Optional 1.8 m mannequin. "
        "Do not show a fortress, sequential gate, 30 m apron, Worldscar, or capital. About three minutes from the dual-gate in V013 — do not draw the gate. "
        "No dragon skull, no eggs, no neon, no modern sandbags, no needle spires."
    ),
    "crownlands_outer_cave_loop_choke_section_v001": (
        COMMON
        + " SUBJECT: Crownlands NON-DRAGON OUTER-WARZONE cave, ONE dungeon, actual combat LOOP with CHOKE. "
        "Two isolated panels: LEFT LONGITUDINAL SECTION; RIGHT COMBAT CIRCULATION PLAN (true top-down). "
        "Plan MUST show: mouth choke, split gallery, cross-chamber with cover blocks (chalk ribs, overturned timber carts as geometry not modern vehicles), looping return corridor that reconnects, rear hold. "
        "The loop is a real closed circuit a player can run; the choke is a narrowed mouth/gallery that is not a dead-end. "
        "Sightlines and 2 m combat clearance readable. No boss podium, no circular arena with a dragon perch. "
        "Section shows 3.5–6 m ceilings, no unauthored pits, ramps that land. "
        "Chalk-gold mineral, rain-dark fractures, aged brass clamps, deep-blue-slate shoring plates only as objects. "
        "Forbidden: dragons, neon, 3/4 dollhouse, fortress walls, sequential dual-gate, cathedral crypt."
    ),
    "crownlands_cave_chamber_fitting_module_v001": (
        COMMON
        + " SUBJECT: Crownlands NON-DRAGON cave CHAMBER and FITTING MODULE kit, six isolated modular pieces of the SAME construction language split into two rows, identical scale, generous gray, not overlapping. "
        "TOP ROW INNER refuge: LEFT straight gallery bay 8 m long, 4 m wide, 3.2 m high, timber props, chalk ribs, brass clamps. CENTER node chamber about 10 m across, 5.5 m high, one entrance and two exits at 120-degree offsets, walkable floor. RIGHT side alcove 4 m deep with store shelves and a lamp object. "
        "BOTTOM ROW OUTER combat: LEFT combat gallery bay 10 m long with side cover ribs. CENTER cross-chamber node about 14 m across with two sightline corners and a 2 m clear middle. RIGHT hold/alcove with brass-shored stores and a barred inner door. "
        "True orthographic FRONT SECTION of each module, like a construction kit, not six different biomes, not a collage overlap. "
        "No dragons, no neon, no people except optional mannequin in the inner gallery bay. No fortress apron. No Town Hall. No cathedral."
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
