#!/usr/bin/env python3
"""Generate Grok Imagine 2D concept sheets for UmbralConceptP1 V001.

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
    "No magic VFX, no particles, no lightning bolts, no glow sprites, no baked volumetrics, no bioluminescence, no violet fog, no portal. "
    "No animals, no real-world wildlife, no dragons, no bosses, no people except one optional 1.8 m featureless gray mannequin for scale. "
    "Umbral only. Three-Fault Ashvein identity: black basalt and graphite ashlar, matte soot surfaces, smoked-glass vertical slit windows, ash-timber yokes, arched X-braces as real timber/iron construction, three-fault seams used as structural joints in walls and terraces, dull ember restricted to hairline cracks only as material not glow sprites. "
    "Low broad stepped masses, supported offset roofs, grounded piers — NOT a gothic cathedral, NOT delicate needle spires, NOT a crystal city, NOT a pagoda tower spectacle, NOT an ungrounded floating keep. "
    "Arched X-brace yokes cap stout corner piers; smoked-glass slits punch fully through walls; soot-matte ashlar with graphite joints. "
    "No Stonehold Embermist, no basalt-iron lava fortress language, no lava rivers, no Eldergrove roots or bronze root collars, no Moonroot canopy, no Crownlands chalk-gold ashlar or deep-blue-slate roofs, no brass meridian compass roses, no Accordant cherry blossoms, no fifth realm. "
    "No fake facade shells: every building shown is a real enterable volume with readable wall thickness and punched door/window apertures. "
    "No copied proprietary Black Desert Online building forms; BDO is finish-bar inspiration only. "
    "No Town Hall spectacle, no Workshop redesign, no kingdom-management prefab language. "
    "Do not draw sequential dual-gates, fortresses, 30 m aprons, terrain continents, or ecosystem dressing. "
    "Include a simple unlabeled metric scale bar with five equal tick marks and a faint 0.5 m floor grid where the subject is object-scale. "
    "Clear short English panel titles only (PLAN, NORTH, SOUTH, FRONT, SIDE, SECTION). No garbled numerals. "
    "PBR materials, Black-Desert-inspired finish bar, not cartoon, not anime. "
    "Same identity, identical scale in every panel of this sheet."
)

SHEETS: dict[str, str] = {
    "umbral_capital_district_plan_v001": (
        COMMON
        + " SUBJECT: Umbral inner-realm capital Veilspire as a SINGLE architectural SITE PLAN. "
        "Camera is STRAIGHT DOWN at 90 degrees like a CAD roof plan. Roofs are FLAT 2D footprints only. "
        "NO facades, NO walls in elevation, NO axonometric, NO isometric, NO 3/4, NO floating island diorama, NO mesa rim, NO cliff sides wrapping the town. "
        "COMPOSITION IS THE HERO REQUIREMENT. The city is an ORGANIC SOUTH-EAST-CORNER-ENCLOSING IRREGULAR L-SHAPED or WEDGE-SHAPED urban pocket built into three fault wedges, NOT a round mesa, NOT an oval island, NOT a radial Y-fork town. "
        "SOUTH: one long IMPASSABLE black-basalt / three-fault cliff wall, irregular broken edge, running left-to-right along the BOTTOM of the sheet, NOT a circular arc. "
        "EAST: a second IMPASSABLE black-basalt / three-fault cliff wall running top-to-bottom along the RIGHT of the sheet, meeting the south cliff in a rough right-angle or obtuse SOUTH-EAST CORNER. THESE TWO CLIFFS ONLY. They do NOT wrap north. They do NOT wrap west. They do NOT complete a loop. "
        "NORTH and WEST are OPEN: broken offset terraces and streets spill toward EMPTY gray studio paper. One controlled approach road enters from the NORTH-WEST open corner. No cliff, no ring-wall, no moat, no island rim on north or west. "
        "The built mass hugs the south+east cliff corner and occupies an L, a 7, a boot, or a thick wedge. The TOP-LEFT third of the sheet is EMPTY gray paper. The city does NOT float in the center as a blob or island. "
        "THREE FAULT WEDGES: three dark graphite fault-seam corridors cut through the urban grain as OFFSET broken wedges — staggered, unequal widths, doglegging, following the SE corner. NOT a symmetric Y-fork in the middle of a round town. NOT three equal 120-degree radials. "
        "Overall silhouette of built-plus-cliff mass: L / wedge / boot hugging the SE corner. NEVER a circle, oval, egg, potato, disk, island, donut, crater, round mesa, clover, or three-lobed radial flower. NEVER a cliff perimeter all around. "
        "STREETS: broken OFFSET terraces, doglegs, pinches, jogs, nonradial grain. Staggered blocks of different sizes. Walkable black-basalt / soot-ash corridors between low broad stepped roof footprints. Door-tick gap on every street edge proving enterability. "
        "ABSOLUTELY FORBIDDEN GEOMETRY: circular island, oval island, floating disk, ring perimeter, cliff all around, crater rim, round wall, oval wall, concentric rings, radial spokes, Y-junction at geometric center, three equal lobes, Renaissance ideal city, Palmanova, keep-centered ring road, cloister. "
        "Hero keep Veilspire is the largest BROAD STEPPED roof parked at the SOUTH-EAST cliff-head (inside the L), not the geometric center of a round town: low rectangular-plus-pier mass with OFFSET roof plates, NOT a needle-spire cluster, NOT a cathedral nave, NOT a pagoda stack, NOT a crystal shard. Arched X-brace ticks on keep corner piers only. "
        "One controlled approach from the open NORTH-WEST. Do NOT draw outer and inner sequential barriers side by side. "
        "Do not label or invent city names or city IDs. Capital identity is Veilspire / capital_veilspire only. "
        "Gray studio paper background with faint grid. No countryside, no Worldscar, no bridges, no fortresses, no 30 m apron diagram."
    ),
    "umbral_capital_skyline_north_south_v001": (
        COMMON
        + " SUBJECT: Umbral capital Veilspire as TWO FLAT orthographic CITY ELEVATIONS, like architectural drawings of a street wall. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT panel title NORTH. RIGHT panel title SOUTH. Same city, same scale, generous gray between. "
        "CRITICAL COMPOSITION: this is a CITY SKYLINE street-wall, NOT a keep-only drawing. "
        "Each panel MUST read left-to-right as: two-storey HOUSE, two-storey HOUSE, WIDE LOW KEEP occupying about one-third of the width, two-storey HOUSE, two-storey HOUSE. "
        "Houses are separate 2-storey black-basalt soot dwellings with rectangular smoked-glass slit windows and low offset roofs. The keep is a BROAD 3-to-4 storey civic block with stout X-brace piers. "
        "SOUTH shows the main keep door as a ROUND-HEADED (semicircular) arch under an arched X-brace yoke. NORTH shows the rear walltop. "
        "ALL openings are RECTANGULAR smoked-glass slits or ROUND-HEADED semicircular arches. "
        "ABSOLUTELY FORBIDDEN: pointed gothic lancets, ogive doors, trefoils, rose windows, flying buttresses, pinnacles, cathedral nave, needle spires, crystal shards, pagoda eaves, keep-only composition with no houses. "
        "Materials: black basalt, matte soot, smoked-glass slits, ash-timber X-braces, graphite joints, dull ember hairlines in fault seams only. No dragons, no neon, no violet fog."
    ),
    "umbral_capital_skyline_east_west_v001": (
        COMMON
        + " SUBJECT: Umbral capital Veilspire as TWO FLAT orthographic CITY ELEVATIONS, like architectural drawings of a street wall. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT panel title EAST. RIGHT panel title WEST. Same city, same scale, generous gray between. "
        "Composition MUST be a HORIZONTAL ROW: two-storey black-basalt soot houses with low offset roofs and smoked-glass slits, then a WIDE LOW keep block in the center, then more two-storey houses. "
        "The skyline is a BROAD civic terrace, about three to four storeys at the keep and two storeys at the houses. "
        "Stout corner piers with arched X-brace timber/iron yokes — short, thick, grounded. FORBIDDEN: delicate needle spires, gothic pinnacles, flying buttress cathedral, rose-window spectacle, crystal shards, pagoda eaves stack, stepped pyramid, ziggurat, volcano, mountain, triangular silhouette, floating island. "
        "Materials: black basalt, matte soot, smoked-glass slits, ash-timber X-braces, graphite joints, dull ember hairlines in fault seams only. No dragons, no neon, no violet fog, no Embermist iron, no chalk-gold."
    ),
    "umbral_capital_keep_shell_v001": (
        COMMON
        + " CRITICAL FAIL POINTS — read first: (A) EVERY window is a RECTANGLE with a FLAT stone lintel. No pointed tops. (B) EVERY roof eave is a HORIZONTAL line. No crow-step gables, no church stair-step silhouette. (C) THREE huge triangular MASONRY BITES are missing from the keep volume, as obvious as three bites taken out of a sandwich. (D) Left and right are NOT mirrors. "
        " SUBJECT: Umbral capital VEILSPIRE HERO KEEP SHELL. Broad asymmetrical stepped Three-Fault Ashvein civic keep. Low wide stone record-hall. Matches east/west skyline: taller 3-to-4-storey center, unequal lower wings, about 54 m across and 16 m to walltop. "
        "Three isolated panels of the SAME keep: ROOF PLAN; FRONT ELEVATION; RIGHT SIDE ELEVATION. "
        "Roofs are FLAT offset terraces that step back like stone platforms. Horizontal eaves only. "
        "THREE DOMINANT FAULT-WEDGE CUTS: (1) a large triangular quarry bite missing from the south-west plinth; (2) a large triangular bite missing from the east wing, dropping that roof one storey; (3) a large triangular bite missing from the north-east corner so the walltop doglegs. Graphite faces inside the bites. These are missing masonry, not painted cracks, not diagonal timber. "
        "ASH-METAL RIBS: thick vertical iron straps with rivet plates clamping the ashlar beside each bite and at four corner piers. "
        "SMOKED SLITS: tall rectangular dark holes, flat lintels, flat sills, punched through. "
        "Door: SEMICIRCULAR Roman round-head under an arched X-brace yoke. Inner and outer arches are half-circles. Never pointed. "
        "Four stout corner piers with X-brace yokes. Roof plan: rectangular-plus-notches, SOUTH door tick, NORTH recess, SW stair bulkhead, NE stair bulkhead, interior void, walltop walk. "
        "FRONT: 1.8 m mannequin about one-ninth keep height. RIGHT SIDE: closed complete wall, not a cutaway, not a ruin. "
        "FORBIDDEN: pointed lancets, ogive door, crow-step gable, church front, generic symmetric box, needles, spires, crystal, pagoda, gothic, neon, dragons. Empty gray between panels."
    ),
    "umbral_capital_keep_section_v001": (
        COMMON
        + " SUBJECT: Umbral Veilspire hero keep as TWO TRUE ARCHITECTURAL SECTIONS of the SAME enterable volume. "
        "LEFT: LONGITUDINAL SECTION cutting the long axis through screened entry, witness chamber, and rear secure stacks. "
        "RIGHT: CROSS SECTION cutting the short axis through the private council chamber and service stair core. "
        "Generous gray between. Strict orthographic section, hatched wall thickness, floors as slabs, stairs that LAND on each floor. "
        "Three occupied floors plus walltop: ground, upper, walltop walk. Ceiling heights readable. Exterior wall ~1.4 m thick. "
        "Show punched smoked-glass slit openings in section as true voids, not painted glass. Offset roof construction as a real thickness on supported piers. "
        "Arched X-brace as a structural timber/iron band, not a glow. Three-fault seam as a hatched joint in the plinth. No 3/4 dollhouse, no ripped-off roof isometric, no missing floors, no cathedral vault spectacle, no people except optional 1.8 m mannequin. "
        "Same outer silhouette as the keep shell sheet. No needle spires. No pagoda. No crystal."
    ),
    "umbral_capital_keep_ground_plan_v001": (
        COMMON
        + " SUBJECT: developed FURNISHED GROUND-FLOOR PLAN of the same Umbral Veilspire hero keep. Large streamed combat interior. "
        "ONE isolated TRUE TOP-DOWN GROUND FLOOR PLAN filling the sheet with generous gray margin, not a 3/4 cutaway, not a dollhouse, not stacked isometric floors. "
        "Program: screened entry on the south door axis with a soot-basalt screen wall, witness chamber with benches, private council with a round table, archive stacks to the sides, service corridor, secure stacks room with a circular iron door as geometry not a bank vault logo, TWO stair cores. "
        "Doors as 1.2 m swings, 0.3 m interior partitions, 1.4 m exterior wall, circulation clear for a 1.8 m mannequin path through every room including the stacks anteroom. "
        "Furniture unoccluded: round table, benches, clerk desks, archive carts, barrels, smoked-glass slit ticks on walls as openings not glow. "
        "Show wing breaks as thick walls, not floating rooms. Keep is enterable throughout. "
        "Stair cores labeled only as geometry (stair rectangles) at north-east and south-west — these same coordinates must be rebuildable on the upper sheet. "
        "Forbidden: roof ripped off 3/4 view, impossible stacked rooms, missing stairs, neon, dragons, cathedral pews, Town Hall spectacle, crystal furniture, violet fog."
    ),
    "umbral_capital_keep_upper_circulation_v001": (
        COMMON
        + " SUBJECT: developed FURNISHED UPPER FLOOR plus WALLTOP plus SERVICE CIRCULATION of the same Umbral Veilspire hero keep. "
        "TWO isolated TRUE TOP-DOWN PLANS with generous gray between them, not a 3/4 cutaway, not a dollhouse. "
        "LEFT: UPPER FLOOR PLAN — tiered archive galleries with shelves, private council overflow, clerks' overflow, service corridor, same TWO stair cores at the EXACT SAME plan coordinates as a ground floor (north-east core and south-west core). Upper stairs sit directly above ground stairs. No missing stairs, no extra unaligned stairs. "
        "RIGHT: WALLTOP PLAN — defendable walk, merlon ticks, stair bulkheads landing from the same two cores, arched X-brace yokes as roof objects not glow, service hatch to walltop stores. "
        "Doors as 1.2 m swings, 0.3 m interior partitions, 1.4 m exterior wall, circulation clear. "
        "Same outer wall silhouette as the ground plan and keep shell. "
        "Forbidden: roof ripped off 3/4 view, contradictory stairs, needle spires, pagoda, crystal, neon, dragons, Town Hall spectacle."
    ),
    "umbral_city_street_grammar_v001": (
        COMMON
        + " SUBJECT: typical Umbral INNER-CITY STREET GRAMMAR, one short ASYMMETRIC block of the same 6 m street. Not a named city; no city IDs. "
        "TWO isolated panels of the SAME street with generous gray: LEFT true TOP-DOWN STREET PLAN; RIGHT true STREET ELEVATION looking at the building fronts. "
        "Walkable street about 6 m between facing facades, black-basalt soot paving, shallow gutters along a three-fault seam, door ticks on every building. "
        "ASYMMETRIC mix: dwellings of different widths on one side, market/service/public-hall footprints on the other, unique Three-Fault Ashvein cladding, not palette-swaps of Stonehold, Eldergrove, or Crownlands. "
        "Elevation shows two-storey black basalt, matte soot, smoked-glass slits, one ash-timber X-brace yoke at a corner pier, punched doors and windows, low offset roofs. "
        "No capital keep, no fortress, no sequential gate, no 3/4 view, no people, no dragons, no Town Hall, no Workshop redesign, no needle spires, no crystal, no gothic."
    ),
    "umbral_city_dwelling_shell_v001": (
        COMMON
        + " SUBJECT: Umbral city DWELLING SHELL only, one enterable TWO-STOREY house volume, not furnished. "
        "Three isolated panels of the SAME house: ROOF PLAN; FRONT ELEVATION; RIGHT SIDE ELEVATION. "
        "ALIGN TO THE EXISTING FURNISHED INTERIOR: footprint about 8 m by 10 m rectangle, wall thickness 0.4 m, SOUTH door 1.2 m by 2.4 m, EAST-WALL STAIR bulkhead on roof plan at the same east-wall coordinate as the furnished ground/upper plans. West hearth chimney as a small roof stack. "
        "CRITICAL HEIGHT: this house is VISIBLY TWO STOREYS. Ground floor PLUS upper floor. Total wall height about 6.5 to 7.5 m. The 1.8 m gray mannequin at FRONT reaches only to about the ground-floor lintel / one-quarter to one-third of the facade. "
        "FRONT ELEVATION MUST SHOW TWO CLEAR ROWS OF OPENINGS: ground-floor SOUTH timber door with a rectangular smoked-glass slit on each side; UPPER-FLOOR row of rectangular smoked-glass slits above a stone string course. A string course / floor band MUST divide ground from upper so two storeys are obvious at a glance. "
        "RIGHT SIDE: two storeys of rectangular smoked slits, east stair implied by a higher window or bulkhead, one small ash-timber X-brace drip yoke at one corner only. "
        "ROOF PLAN: 8x10 rectangle, EAST stair bulkhead opening (dark void) proving interior void and matching the interior east-wall stair, low offset roof plates, chimney west. "
        "Punched door and windows fully through the wall as DARK VOIDS, not painted glass, not fake facade. Black basalt, matte soot, iron latch. "
        "ABSOLUTELY FORBIDDEN: one-storey bungalow, door as tall as the whole facade, mannequin as tall as the eaves, missing upper-floor windows, chapel gable, gothic pointed arch, furniture in this sheet, missing walls, 3/4 cutaway, needle spire, crystal, neon, dragons. "
        "Empty gray between panels."
    ),
    "umbral_city_dwelling_interior_v001": (
        COMMON
        + " SUBJECT: Umbral city DWELLING FURNISHED INTERIOR of the SAME 8 m by 10 m two-storey house. Small seamless interior. "
        "ONLY TWO isolated TRUE TOP-DOWN PLANS with generous gray between them. No extra elevations, no extra sections, no material swatch row. "
        "LEFT panel title GROUND PLAN. RIGHT panel title UPPER PLAN. "
        "CRITICAL STAIR RULE: one single stair rectangle occupies the EAST wall on BOTH floors — the upper stair sits directly above the ground stair at the identical plan coordinate. "
        "If ground stair is against the right/east wall, upper stair is also against the right/east wall. No west-side stair. No extra unaligned stair. "
        "Ground: west hearth (soot-stone, not lava), center table and benches, east stair, storage chests, south door swing. "
        "Upper: east stair landing, two beds, chests, same outer wall rectangle. "
        "Wall thickness 0.4 m, doors 1.2 m, all furniture unoccluded and traversable. Same rectangular outer wall silhouette on both floors. "
        "Black basalt, timber furniture, iron latch hardware as objects not glow. Offset roof implied only by wall rectangle, not drawn as a chapel gable. "
        "No 3/4 cutaway, no missing walls, no modern kitchen, no contradictory stairs, no chapel front, no dragons, no neon, no crystal."
    ),
    "umbral_city_market_service_public_hall_kit_v001": (
        COMMON
        + " SUBJECT: Umbral city MARKET / SERVICE / PUBLIC-HALL modular kit, three isolated enterable modules, identical scale, generous gray, not overlapping. "
        "NOT a Town Hall redesign. NOT a Workshop redesign. NOT a kingdom-management prefab. "
        "LEFT column labeled MARKET: front elevation with a wide serving hatch and soot-basalt shutter plus a small interior plan. Counter, public floor, rear stores. Black basalt, smoked-glass slit. Traversable aisles. "
        "CENTER column labeled SERVICE: front elevation plus interior plan. Public cistern basin, bucket bench, dry stores, service counter, rear yard door. No modern plumbing. Ash-timber yoke as a small structural brace. Traversable aisles. "
        "RIGHT column labeled PUBLIC HALL: front elevation plus interior plan of a single public room with benches, a clerk table, and a traversable aisle. "
        "PUBLIC HALL APERTURE IS THE HERO DETAIL: English label exactly '2.5 m W x 3.0 m H' with dimension arrows on the door. "
        "Paired timber leaves (TWO door boards). "
        "HINGES: barn-door STRAPS. Each leaf has three iron straps that start at the jamb and run almost to the meeting edge of that leaf (about 80 percent of the leaf width), fat rivets. NOT short hinges. "
        "LATCH: one long sliding iron bar parked across both leaves, like a barn-door bolt, sliding into a keep. "
        "Stone-block ashlar jambs and lintel. "
        "ALL windows on ALL three modules are RECTANGULAR smoked slits with FLAT lintels. ZERO pointed gothic lancets on market, service, or hall. "
        "True orthographic FRONT plus a small true top-down plan under each elevation, same Three-Fault Ashvein cladding. Three modules must be visually distinct in both elevation and plan. "
        "No 3/4 dollhouse, no Town Hall spectacle, no smithy, no anvils, no needle spires, no dragons, no neon, no logos, no gothic, no crystal."
    ),
    "umbral_inner_cave_mouth_v001": (
        COMMON
        + " SUBJECT: Umbral NON-DRAGON INNER-REALM cave MOUTH only, ash-gallery refuge language, not a warzone, not a lair, not a cathedral crypt spectacle. "
        "Two isolated panels of the SAME mouth: FRONT ELEVATION of the opening in a basalt fault shelf; RIGHT SIDE ELEVATION showing overhang depth and lintel thickness. "
        "Human-scale mouth about 4.5 m wide by 3.6 m tall, black-basalt square lintel with a modest arched X-brace tick, shallow cut-stone threshold, walkable packed-ash floor. Smoked-glass slit only as a small side lantern niche object, not a glow. "
        "Organic inner safe-pocket geology: layered graphite, matte soot, three-fault seam in the lintel as a structural joint, no carved dragon skull, no treasure hoard, no eggs, no bones, no neon, no crystal geodes, no stained-glass rose. "
        "Optional 1.8 m gray mannequin at the threshold. No fortress, no sequential gate, no 30 m apron diagram."
    ),
    "umbral_inner_cave_section_circulation_v001": (
        COMMON
        + " SUBJECT: Umbral NON-DRAGON INNER cave, SAME volume as an ash-gallery refuge. "
        "Two isolated panels with generous gray: LEFT LONGITUDINAL SECTION; RIGHT CIRCULATION PLAN (true top-down). "
        "Organic pocket topology: entry gallery, two side alcoves, a roundish main chamber, a rear store niche, all connected by walkable floors with no unauthored holes. "
        "Ceiling heights readable in section (gallery 3.2 m, main chamber 5.5 m). Stair or ramp only if it lands. "
        "Plan shows a single obvious loop-free refuge path plus one return alcove; doors/portcullis optional at mouth only. "
        "Packed-ash floors, graphite ribs, timber props, iron clamp plates, practical lamps as objects not glow sprites. Three-fault seams as structural joints in the gallery walls. "
        "Forbidden: dragon nest, boss arena, neon, 3/4 cutaway collage, impossible stacked voids, flying ledges without path, cathedral crypt nave, crystal cavern, violet fog."
    ),
    "umbral_outer_cave_mouth_v001": (
        COMMON
        + " SUBJECT: Umbral NON-DRAGON OUTER-WARZONE cave MOUTH only, three-fault combat-scarred basalt cut, not a dragon lair, not a fortress. "
        "Two isolated panels of the SAME mouth: FRONT ELEVATION; RIGHT SIDE ELEVATION. "
        "Mouth about 6 m wide by 4.2 m tall, broken ash-timber X-brace lintel, spalled black basalt, silt/ash slope that is NOT a climbable talus ramp onto a wall. "
        "War-worn but still a human dungeon entrance. Optional 1.8 m mannequin. "
        "Do not show a fortress, sequential gate, 30 m apron, Worldscar, or capital. About three minutes from the dual-gate in V013 — do not draw the gate. "
        "No dragon skull, no eggs, no neon, no modern sandbags, no needle spires, no crystal geodes, no violet fog."
    ),
    "umbral_outer_cave_loop_choke_section_v001": (
        COMMON
        + " SUBJECT: Umbral NON-DRAGON OUTER-WARZONE cave, ONE dungeon, actual combat LOOP with CHOKE. "
        "Two isolated panels: LEFT LONGITUDINAL SECTION; RIGHT COMBAT CIRCULATION PLAN (true top-down). "
        "Plan MUST show: mouth choke, split gallery, cross-chamber with cover blocks (basalt ribs, overturned timber carts as geometry not modern vehicles), looping return corridor that reconnects, rear hold. "
        "The loop is a real closed circuit a player can run; the choke is a narrowed mouth/gallery that is not a dead-end. "
        "Sightlines and 2 m combat clearance readable. No boss podium, no circular arena with a dragon perch. "
        "Section shows 3.5–6 m ceilings, no unauthored pits, ramps that land. "
        "Black basalt, matte soot, three-fault seams as structural joints, aged iron clamps, ash-timber shoring plates only as objects. "
        "Forbidden: dragons, neon, 3/4 dollhouse, fortress walls, sequential dual-gate, cathedral crypt, crystal cavern, violet fog."
    ),
    "umbral_cave_chamber_fitting_module_v001": (
        COMMON
        + " SUBJECT: Umbral NON-DRAGON cave CHAMBER and FITTING MODULE kit, six isolated modular pieces of the SAME construction language split into two rows, identical scale, generous gray, not overlapping. "
        "TOP ROW INNER refuge: LEFT straight gallery bay 8 m long, 4 m wide, 3.2 m high, timber props, graphite ribs, iron clamps. CENTER node chamber about 10 m across, 5.5 m high, one entrance and two exits at 120-degree offsets, walkable floor. RIGHT side alcove 4 m deep with store shelves and a lamp object. "
        "BOTTOM ROW OUTER combat: LEFT combat gallery bay 10 m long with side cover ribs. CENTER cross-chamber node about 14 m across with two sightline corners and a 2 m clear middle. RIGHT hold/alcove with iron-shored stores and a barred inner door. "
        "True orthographic FRONT SECTION of each module, like a construction kit, not six different biomes, not a collage overlap. "
        "Three-fault seams and smoked-glass lantern niches as construction, not glow. "
        "No dragons, no neon, no people except optional mannequin in the inner gallery bay. No fortress apron. No Town Hall. No cathedral. No crystal."
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


def write_prompt_log(run_log: dict) -> None:
    by_name = {row["name"]: row for row in run_log.get("results", []) if "name" in row}
    sheets = []
    for name, prompt in SHEETS.items():
        row = by_name.get(name) or {}
        sheets.append(
            {
                "name": name,
                "prompt": prompt,
                "provider": row.get("provider"),
                "chat_model": row.get("chat_model"),
                "image_model": row.get("image_model"),
                "quality": row.get("quality"),
                "aspect_ratio": ASPECT,
                "fallback": bool(row.get("fallback")),
                "fallback_provider": row.get("fallback_provider"),
                "fallback_model": row.get("fallback_model"),
                "ok": row.get("ok"),
                "http": row.get("http"),
            }
        )
    payload = {
        "packetId": "UmbralConceptP1_V001",
        "fallback_policy": "GPT-5.6 Sol only if Grok returns no answer",
        "sheets": sheets,
    }
    (OUT_DIR / "prompt_log_v001.json").write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


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
    write_prompt_log(run_log)
    failed = [row["name"] for row in results if not row["ok"]]
    print("FAILED", ",".join(failed) if failed else "none")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
