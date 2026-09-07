#!/usr/bin/env python3
"""Generate Grok Imagine 2D concept sheets for CrownlandsConceptGapP2 V001.

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
    "Geometry-readable orthographic architectural / construction drawing. Strict orthographic cameras, no perspective, no 3/4 hero shot, no isometric dollhouse cutaway, no axonometric island diorama. "
    "Neutral seamless gray studio, even studio lighting, no sky, no landscape backdrop except the subject terrain that belongs to the sheet. "
    "Clean unoccluded views, generous empty gray space between panels, no overlapping objects, no collage, no moodboard, no photobash, no exploded 3/4 kit dump. "
    "No watermarks, no logos, no UI, no modern objects, no vehicles, no firearms, no plastic, no neon, no glass curtain walls. "
    "No magic VFX, no particles, no lightning bolts, no glow sprites, no baked volumetrics, no bioluminescence. "
    "No animals, no real-world wildlife, no dragons, no bosses, no people except one optional 1.8 m featureless gray mannequin for scale. "
    "Crownlands only. Meridian Oathroad identity: grounded chalk-gold ashlar limestone, deep blue slate roofs and banding, brass meridian ribs and compass-rose crests as FLAT discs never finials, cool silver edgework, restrained bronze, rain-dark mortar fractures, storm-pressed oathroad grit at ground only. "
    "Broad civic palazzo masses, readable construction, stout axial drums with truncated/hipped blue-slate pavilion roofs — NOT delicate needle spires, NOT a gothic cathedral, NOT conical turrets, NOT witch-hat caps, NOT an ungrounded white-marble palace, NOT excess gold leaf. "
    "Segmented meridian arches (shallow round, never pointed), ordered tower rhythm, chalk ribs. "
    "No circular/radial capital, no concentric rings, no Renaissance ideal city, no Palmanova. "
    "No Stonehold Embermist, no basalt-iron fortress language, no lava, no soot-forge, no Eldergrove roots or bronze root collars, no Moonroot canopy, no Umbral violet fog or smoked-glass, no Accordant cherry blossoms, no fifth realm. "
    "No fake facade shells: every building shown is a real enterable volume with readable wall thickness and punched door/window apertures. "
    "No Town Hall spectacle, no Workshop redesign, no kingdom-management prefab language. "
    "No copied proprietary Black Desert Online building forms; BDO is finish-bar inspiration only. "
    "Preserve gate_crownlands_meridian: pale limestone leaves, cool silver ribs, weathered blue-slate banding, segmented meridian geometry, axial panel rhythm. "
    "Include a simple unlabeled metric scale bar with five equal tick marks and a faint 0.5 m floor grid where the subject is object-scale. "
    "PBR materials, Black-Desert-inspired finish bar, not cartoon, not anime, not painterly concept-illustration. "
    "Same identity, identical scale in every panel of this sheet."
)

SHEETS: dict[str, str] = {
    "crownlands_sequential_gate_outer_face_v001": (
        COMMON
        + " SUBJECT: Crownlands sequential-gate OUTER FACE, visual language only. Topology is already locked by the technical plate and must be illustrated, not changed. "
        "Stable IDs remain gate_crownlands_meridian_outer on this outer leaf pair; the complex is wall_complex_crownlands_meridian. Preserve gate_crownlands_meridian leaf language. "
        "ONE isolated TRUE FRONT ELEVATION, camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4. "
        "A LONG LOW HORIZONTAL chalk-gold ashlar curtain-wall, about 80 m of wall shown, like a dam. EXACTLY ONE ceremonial gated opening in the middle, wall continuing far to both sides. "
        "Gate opening about 8 m wide by 6 m tall under a SEGMENTAL (shallow round, never pointed) brass-ribbed meridian arch. ONE pair of leaves: pale limestone with cool silver ribs and weathered blue-slate banding, brass/bronze hardware. "
        "Optional TWO stout BLUNT octagonal drums flanking the opening: short thick eight-sided shafts with TRUNCATED LOW HIPPED blue-slate pavilion caps — peak cut off almost flat. NEVER cones, NEVER witch-hats, NEVER needle finials. "
        "CRITICAL CLIMB RULE: Outer attacker face is BLANK unclimbable masonry: no ladders, no holds, no talus ramp, no carved climbing ribs, no stairs. Ornament is brass meridian ribs, silver edges, blue-slate banding only. "
        "Merlon rhythm on the walltop. Optional 1.8 m gray mannequin at the threshold. Empty gray studio. "
        "FORBIDDEN: Gothic pointed arch; rose window; conical turrets; needle spires; two gates side by side; inner and outer barriers as twin front elevations; 3/4 hero shot; plus-shaped courtyard; sequential dual-gate collage; cathedral; dragons; changing the sequence. "
        "REGEN OVERRIDE: previous image had pointed pyramid turret caps and timber barn doors. Drum caps MUST be truncated almost-flat hips, as if the peak was cut off, NEVER a pyramid or cone, NEVER a gold spike. Gate leaves MUST be pale limestone SLABS with cool silver ribs and blue-slate banding — NOT timber barn doors, NOT iron strap-and-plank wood. Arch is a shallow SEGMENTAL curve, not a tall horseshoe."
    ),
    "crownlands_sequential_gate_inner_face_v001": (
        COMMON
        + " SUBJECT: Crownlands sequential-gate INNER FACE, visual language only. Topology is locked. "
        "Stable inner-gate ID remains gate_crownlands_meridian. This is the inner barrier face seen FROM THE INNER REALM. Preserve gate_crownlands_meridian leaf language. "
        "ONE isolated TRUE FRONT ELEVATION, camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4. "
        "A LONG LOW HORIZONTAL chalk-gold ashlar curtain-wall, about 80 m of wall shown. EXACTLY ONE inner gated opening in the middle, quieter civic language than the outer face, same materials: chalk-gold ashlar, pale limestone leaves with cool silver ribs, weathered blue-slate banding, brass meridian ribs, truncated blunt drums if present. "
        "ONE pair of inner leaves under a SEGMENTAL meridian arch, never pointed. Inner/defender side MUST show a stone stair rising to walltop on THIS face only. "
        "No attacker ladders. Optional 1.8 m gray mannequin. Empty gray studio. "
        "FORBIDDEN: drawing outer and inner barriers as two side-by-side twins; 3/4; collage; outer-face ladders; Gothic cathedral; conical turrets; needle spires; dragons; neon; topology change. "
        "REGEN OVERRIDE: previous retry STILL drew Gothic lancet tracery on the leaves. Leaves are PLAIN pale limestone rectangles with a center seam and cool silver strap ribs only. ZERO arches drawn on the leaves. ZERO pointed panels. ZERO cathedral doors. Keep the inner stone stair to walltop. Drum caps are tabletop-flat truncated hips."
    ),
    "crownlands_sequential_gate_longitudinal_section_v001": (
        COMMON
        + " SUBJECT: two-panel construction drawing of the Crownlands sequential two-barrier passage. Allowed text only: OUTER x=0, INNER x=18, and one unlabeled scale bar. "
        "LEFT = true top-down plan. RIGHT = true flat side cut. Generous gray between. "
        "LEFT PLAN: chalk-gold ashlar wall band left-to-right. Pale oathroad left-to-right through the wall. TWO separate closed double-doors: at LEFT, a pair of pale-limestone leaves with silver ribs and a visible center seam drawn as two filled pale rectangles sitting on the road, label OUTER x=0. Then 18 m of pale road under a LIGHT dashed roof outline, not a solid dark slab. At RIGHT, a second pair of pale-limestone leaves with a visible center seam as two filled pale rectangles on the road, label INNER x=18. The two door-pairs are separate objects; do not merge them into one dark inset panel. Stair hatch only on the north edge, to the RIGHT of INNER x=18. No south stairs. "
        "RIGHT SECTION: flat 2D cut through a closed shoebox tunnel. Left to right: outer ground; OUTER closed double-door shown as a vertical filled pale-limestone rectangle the full height of the opening, label OUTER x=0; dark interior cavity with roof and one gray mannequin; INNER closed double-door as a second vertical filled pale-limestone rectangle the full height of the opening, label INNER x=18; inner ground. Each door occludes the tunnel. Do not place doors on the left and right walls facing the camera. Do not draw an open proscenium. Stair only to the right of the inner door. Roofed ~18 m passage. "
        "Chalk-gold ashlar, pale limestone leaves, blue slate roof banding, brass. No courtyard, no cathedral nave, no cones. "
        "REGEN OVERRIDE: IGNORE decorative gatehouse. Draw TWO FLAT DIAGRAMS only. LEFT = CAD plan: a horizontal wall sausage, road through it, two filled door-pair rectangles labeled OUTER x=0 and INNER x=18, dashed roof between, stair hatch north of inner only. RIGHT = a 2D SIDEWAYS rectangle like a subway cut: gray earth, then a thick VERTICAL BLACK BAR, then a dark tunnel with a side-view mannequin, then a second thick VERTICAL BLACK BAR, then earth and a stair. NO door fronts. NO open hall. NO leaves facing camera. If door panels face you, fail."
    ),
    "crownlands_sequential_wall_walltop_modules_v001": (
        COMMON
        + " SUBJECT: Crownlands sequential-gate WALL and WALLTOP DEFENDER MODULES, three isolated modular pieces of the SAME construction language, identical scale, generous gray, not overlapping. "
        "True orthographic cameras, ZERO perspective, ZERO 3/4. "
        "LEFT: 12 m curtain-wall bay FRONT ELEVATION, chalk-gold ashlar masonry, blue-slate banding, brass meridian rib at corners, compressed parapet, no door, cut ends show 1.8 m wall thickness as a dark rectangle. Optional 1.8 m gray mannequin. "
        "CENTER: WALLTOP WALK PLAN, TRUE TOP-DOWN looking straight down at 90 degrees, of a LONG NARROW walk about 2.4 m wide and 12 m long, like a ladder drawn in plan. Merlon ticks on the OUTER long edge, inner parapet on the other long edge, defender stair hatch on the INNER side only. This CENTER panel is a PLAN, not a front wall, not an elevation, NO window, NO 3/4. "
        "RIGHT: DEFENDER STAIR as a FLAT SIDE ELEVATION of a wall CUT: LEFT half of this panel is INNER courtyard with a stone stair rising to walltop; RIGHT half of this panel is OUTER attacker face — a BLANK battered chalk-gold wall, zero treads, zero ladders, zero holds, zero talus, zero windows. "
        "Forbidden: square room plan, front-elevation pretending to be a walk plan, exterior ladders, climbable outer stairs, 3/4 dump, conical turrets, needle spires, Gothic, dragons, neon. "
        "REGEN OVERRIDE: previous authority FAIL — LEFT curtain face had THREE pointed Gothic blind arches. LEFT MUST be blank chalk-gold ashlar with only a FLAT horizontal blue-slate band and one FLAT circular brass compass disc. ZERO openings. ZERO niches. ZERO arcades. ZERO pointed recesses. ZERO lancets. ZERO Gothic. If any pointed or round-headed opening appears on the left face the sheet fails. CENTER remains a true top-down 2.4 m by 12 m walltop walk plan like a ladder. RIGHT remains inner-only stair versus blank unclimbable outer face."
    ),
    "crownlands_fortress_plan_apron_v001": (
        COMMON
        + " SUBJECT: Crownlands ONE-GATE fortress as a SINGLE architectural SITE PLAN. Inventory placeholders fortress_crownlands_01..04 are not drawn as four forts; this is ONE typical fortress. "
        "Camera is STRAIGHT DOWN at 90 degrees like a CAD roof plan. Roofs and walls are FLAT 2D footprints only, like paper cutouts. "
        "NO facades, NO visible outer wall faces, NO axonometric, NO isometric, NO 3/4, NO floating island diorama, NO cliff sides. "
        "One connected irregular polygonal perimeter drawn as a thick chalk-gold ashlar ring. EXACTLY ONE entrance on the south wall. That entrance is completely occupied by a CLOSED paired gate: two thick pale-limestone leaves with cool silver ribs, a center seam, and brass hinges SPAN the full gap and the full wall thickness so courtyard paving cannot be seen through the opening. The leaves are a barrier bar, not a small door drawing sitting in an open gap, not two isolated piers. NO stairs sticking into the apron. NO other breach, NO second opening. Tiny label GATE on the leaves is allowed; no other extra text except the apron label. "
        "Low-broad keep as a BROAD RECTANGULAR civic-palazzo ROOF footprint toward the rear, separate from the gate, with four chamfered blunt-octagon drum footprints — NOT a round keep, NOT a cathedral nave, NOT a circular courtyard. Central courtyard FLAG ANCHOR: a 4 m circle with a tiny mast-dot in the middle, DISTINCT from the keep, NOT on the keep roof, NOT a fountain, NOT a drain, NOT a fire pit, NOT a compass rose plaza. "
        "Around the entire perimeter: a continuous dashed offset boundary with explicit readable text label '>=30m EMPTY DEFENSIVE APRON' (this one label is required; no other text, logos, or UI). The apron band is flat packed pale chalk/oathroad grit with NOTHING on it — no rocks, no stairs, no trees, no foliage, no terrain piles, no debris, no props, no climb-assist. Courtyard paving is inside the walls only. "
        "Gray studio paper background. No sequential dual-gate, no Worldscar, no capital city, no second gate, no circular/radial plan. "
        "REGEN OVERRIDE: Draw the sole south entrance as a thick CLOSED paired limestone gate leaf/barrier spanning the entire gap. Preserve one connected irregular perimeter, low broad enclosed keep, separate central flag-anchor socket, continuous dashed boundary labeled >=30m EMPTY DEFENSIVE APRON, zero props on the apron."
    ),
    "crownlands_fortress_elevations_v001": (
        COMMON
        + " SUBJECT: Crownlands ONE-GATE fortress as TWO FLAT orthographic ELEVATIONS of the SAME fortress. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT: FRONT elevation. A LONG LOW SOLID masonry DAM-WALL, chalk-gold ashlar, about 80 m, merlon parapet, EXACTLY ONE CLOSED paired pale-limestone gate in the middle under a shallow segmental arch. Keep sits BEHIND the wall as a LOW BROAD rectangular civic palazzo with a low blue-slate hip roof. Four short octagonal drums; each drum top is a FLAT blue-slate walkable terrace — a horizontal line, not a triangle. RIGHT: SIDE elevation of the same solid wall and keep, no second gate. Generous gray between. "
        "Windows are small RECTANGLES with FLAT lintels and 90-degree corners. ZERO pointed windows. ZERO lancets. ZERO Gothic. ZERO cones. ZERO needles. ZERO gold spikes. ZERO finials. ZERO open arcades. ZERO viaducts. ZERO flying buttresses. "
        "Silhouette is a wide short rectangle. Outer face blank unclimbable ashlar. Empty flat apron. No foliage, no stairs, no talus. "
        "REGEN OVERRIDE: last retry was a fairy-tale arcade viaduct with needle spires — FAIL. Draw a SOLID fortress wall, not an arcade. Drum tops are FLAT TERRACES you could stand on. Keep roof is a low hip without a spike. Rectangular windows only. One closed gate."
    ),
    "crownlands_fortress_keep_flag_anchor_v001": (
        COMMON
        + " SUBJECT: Crownlands fortress KEEP SHELL plus a SEPARATE CENTRAL FLAG ANCHOR, not a city, not a sequential dual-gate, not capital_crownspire. "
        "Three isolated panels left-to-right of the SAME keep language with generous gray. "
        "LEFT: KEEP ROOF PLAN, true top-down. Solid enclosed roof, not a courtyard ring. Four octagon drums whose blue-slate tops are FLAT inner octagons (tabletops), never an X of ridges. Separate 4 m circular flag-anchor socket drawn BELOW the keep, not on the roof. "
        "CENTER: TRUE FLAT FRONT ELEVATION of the same keep, drawn like a 2D facade diagram. Camera exactly perpendicular to the front wall and infinitely far away: verticals vertical, horizontals horizontal, ZERO visible left side, ZERO visible right side, ZERO receding wall planes, ZERO perspective, ZERO 3/4. Low broad civic palazzo. Rectangular punched windows with flat lintels. Segmental door. Drum tops in elevation are FLAT HORIZONTAL TERRACES matching the plan tabletops — broad dark-blue octagonal top planes behind low parapets, large enough for a mannequin to stand on. Central roof is a low truncated hip with a wide flat top. ZERO cones. ZERO pyramids. ZERO finials. ZERO witch-hats. Keep is a solid enclosed volume. "
        "RIGHT: isolated thin 6 m cylindrical brass-and-chalk BANNER POLE on a separate 4 m blocking plinth, empty socket, no cloth, mannequin beside it. The pole has constant thickness and ends in a FLAT BLUNT CAP or open socket ring. It is detached from the keep. ZERO taper, ZERO spear point, ZERO pointed cap, ZERO obelisk, ZERO monument, ZERO finial. "
        "REGEN OVERRIDE: attempt7 failed because the detached mast tapered into a pointed spear/obelisk silhouette and the drums were not clearly walkable flat terraces; attempt8 fixed the pole but rendered the center keep in 3/4 perspective. ELEVATION MUST MATCH PLAN and remain a strict flat facade with no visible side planes. Draw each drum as a short octagonal tower with a broad walkable FLAT slate roof. Draw the detached flag element as a plain constant-diameter cylindrical banner pole with a blunt flat top or empty ring socket — NEVER a needle, spear, obelisk, tapered monument, or finial."
    ),
    "crownlands_terrain_grades_worldscar_wallend_v001": (
        COMMON
        + " SUBJECT: authored Crownlands TERRAIN CONSTRUCTION PLATE: inner/outer GRADE, nonperiodic WORLDSCAR brink, and WALL-END terrain termination. Not isolated props, not a fortress, not a map. "
        "THREE isolated panels with generous gray. "
        "LEFT: INNER versus OUTER GRADE as a TRUE 2D geologic SIDE CUT like a textbook cross-section. Camera in the cut plane. Left half is inner 33-percent-class safe-pocket pale chalk-gold shelves with a gentle packed-oathroad-grit grade; right half is outer warzone with more broken chalk shelves, rain-dark fractures, and wind-scoured silt. The split is an ORGANIC GRADE TRANSITION, not a wall and not a palette-swap. NONPERIODIC bedding: irregular block sizes, offset beds, NOT repeating layered-cake stripes, NOT a 3/4 boulder island. "
        "CENTER: NONPERIODIC WORLDSCAR BRINK as a FRONT ELEVATION of a sheer authored chalk-gold / rain-dark-fracture cliff into empty gray void — macro mass greater than 5 m, meso fracture 0.25 to 5 m, micro surface breakup under 0.25 m. Fractured, offset, nonperiodic. Optional 1.8 m gray mannequin at the brink. No lightning, no magma, no baked volumetrics, not a repeating texture, not a layered cake. "
        "RIGHT: WALL-END TERRAIN TERMINATION: a sequential-wall curtain END in FRONT ELEVATION, a horizontal chalk-gold masonry wall band about 12 m long seating INTO an impassable chalk-cliff mass so the wall is visibly FUSED into the cliff. The cliff is a BLANK stop, not a ramp, not climbable talus, not a decorative door stele, not a monument panel. Outer face of the wall remains unclimbable. "
        "Forbidden: three isolated prop boulders, repeating strata cake, primitive cones, decorative stelae, fortress apron dressing, sequential dual-gate collage, lava, dragons, neon, Gothic. "
        "REGEN OVERRIDE: previous LEFT was a 3/4 stone block, not a textbook cut. LEFT must be a FLAT geologic cross-section filling the panel, inner pale chalk shelves grading into broken outer shelves, offset irregular beds, NEVER a 3/4 island. CENTER is a WIDE sheer cliff FRONT ELEVATION into gray void, not a standalone pillar. RIGHT is a LONG HORIZONTAL masonry wall running into a cliff mass and fused with it — NOT a triple-arch arcade, NOT a door, NOT a stele. ZERO openings on the right panel."
    ),
    "crownlands_terrain_bridge_abutment_180m_v001": (
        COMMON
        + " SUBJECT: Crownlands Meridian Oathroad 180 m adjacent-realm BRIDGE ENDPOINTS and ABUTMENT FIT onto the SHARED crest-free deck. Visual language only. 180 m length remains V013 topology; pixels show endpoint fit, not a measured span. "
        "THREE isolated panels of the SAME abutment language, generous gray. "
        "LEFT: SIDE ELEVATION of ONE landfall abutment sitting on solid chalk-gold ground at the Worldscar brink. Shared 6 m stone deck and 1.1 m rails leave the abutment toward empty dark gulf. Crownlands abutment skin only: pale limestone / chalk-gold ashlar pier, cool silver meridian ribs flush to the faces, weathered blue-slate cap band, restrained brass edge. The shared deck continues UNCHANGED — no realm crests on the deck. No fortress, no sequential gate. "
        "CENTER: FRONT ELEVATION facing the Worldscar: abutment tower/pier as a BLUNT low civic drum or rectangular pier with truncated blue-slate cap, NEVER a needle spire, NEVER a Gothic buttress. Deck stub centered, 6 m walkable, rails 1.1 m as solid geometry. Optional 1.8 m mannequin on the deck. "
        "RIGHT: PLAN of the abutment FIT: chalk brink as a broken irregular edge, abutment footprint seating fully on solid ground, deck leaving as a long thin 6 m band over void, 4 m modular bay ticks on the first two bays only. Show how the abutment meets both the brink and the shared deck — a construction fit, not a hero monument. "
        "Forbidden: inventing a new 180 m measurement from pixels; ramps off the deck; swim paths; flying creatures; cathedral abutment; conical turret pier; Accordant spokes; fifth realm; lava."
    ),
    "crownlands_terrain_route_bed_v001": (
        COMMON
        + " SUBJECT: authored Crownlands ROUTE BED, not a continent map, not a fortress apron. 14.4 km-class oathroad visual language only. "
        "THREE isolated panels of the SAME route language, identical scale, generous gray. "
        "LEFT: CROSS SECTION of the route, TRUE SIDE CUT like a sliced loaf viewed from the cut face, camera in the cut plane, ZERO 3/4, ZERO isometric slab: packed pale chalk/oathroad-grit BED, two worn wheel RUTS as clear depressions in the cut, raised SHOULDERS, shallow drainage gutter, verge. Not a 3/4 slab, not a crate, not metal rails, not a smooth ramp, not a painted texture stripe, not a wavy mattress. "
        "CENTER: TRUE TOP-DOWN of about 12 m of packed-chalk road showing nonperiodic ruts and repair patches. Shoulder stones only outside a 4 m travel lane. Shallow drainage channels along one verge. NO metal tram rails, NO modern curbs. "
        "RIGHT: PLAYER-SCALE ROCK KIT, three isolated unique ANGULAR chalk-gold rocks sitting on a ground line: 0.4 m faceted cobble, 1.2 m blocky fractured slab, 2.4 m irregular boulder. Broken masonry / chalk-rib language, faceted, NOT cones, NOT teardrops, NOT smooth eggs, not copies of each other scaled up. 1.8 m gray mannequin beside the boulder. "
        "Forbidden: repeating strata stripes, primitive cones, teardrop rocks, stick vegetation, picket-fence rocks, climb-assist talus against a wall, metal rails, dragons. "
        "REGEN OVERRIDE: LEFT must be a FLAT sliced-loaf cross-section facing the camera, showing two distinct U-shaped wheel-rut depressions, raised shoulders, and a drainage gutter — not a 3/4 mattress slab. RIGHT rocks are faceted angular broken chalk, NOT teardrops, NOT eggs, NOT cones. Include a shallow square drainage gutter on one side of the left cut."
    ),
    "crownlands_capital_city_material_kits_v001": (
        COMMON
        + " SUBJECT: Crownlands CAPITAL / CITY MATERIAL KITS, construction swatches and cladding modules, not a city plan, not capital_crownspire layout, not Town Hall, not Workshop. "
        "True orthographic FRONT ELEVATIONS and small PLAN chips, generous gray, not a collage photobash. "
        "TOP ROW: six isolated MATERIAL SWATCH planes about 1.2 m square each, same lighting: (1) chalk-gold ashlar limestone with rain-dark mortar joints; (2) deep blue slate shingles/hips; (3) brass meridian rib section; (4) cool silver edge strip; (5) restrained bronze hardware plate; (6) packed oathroad grit / pale chalk paving. Each swatch is a FLAT sample board, not a building. "
        "BOTTOM ROW: three isolated CLADDING MODULES of identical scale, not a street: LEFT civic palazzo wall bay 6 m wide with rectangular punched window, segmental brass rib, truncated-hip blue-slate eaves; CENTER dwelling wall bay 4 m with ordinary punched door and hip eaves; RIGHT market shutter bay with deep-blue-slate hatch and serving counter lip. All chalk-gold ashlar. Optional 1.8 m mannequin at dwelling. "
        "FORBIDDEN: capital district plan, circular city, Gothic cathedral, conical turrets, needle spires, rose windows, Town Hall spectacle, Workshop redesign, inventing city IDs, 3/4 dump, dragons, neon. "
        "REGEN OVERRIDE: previous image drew recessed niche rooms instead of swatches and cloned the same niche as 'cladding'. TOP ROW must be SIX FLAT sample boards like tile chips lying on gray paper — ashlar, blue slate, brass, silver, bronze, grit — NOT rooms, NOT alcoves. BOTTOM ROW must be three different FRONT ELEVATIONS of wall bays: palazzo window bay with hip eaves; dwelling door bay; market shutter bay. Zero niches."
    ),
    "crownlands_ecosystem_composition_habitat_v001": (
        COMMON
        + " SUBJECT: Meridian Oathroad ECOSYSTEM COMPOSITION and FANTASY-BEAST HABITAT STRUCTURE without any animals. Habitat only: hollows, wallows, perches, cover. ZERO creatures, ZERO real-world wildlife, ZERO dragons. "
        "FOUR isolated 8 m-wide ground plots as true orthographic FRONT ELEVATION strips, generous gray, not overlapping, not 3/4 islands. "
        "Plot 1 Broadcrest wallow: a low chalk-dust bowl and packed-grit scrape, knee-high pale thistle-analog clumps on the rim, traversal-readable, no animal. "
        "Plot 2 Grainveil cover: volumetric desaturated oat-sedge and pale verge herb along a packed-oathroad edge, a 2 m clear travel lane empty, plants only on verges, no birds. "
        "Plot 3 Reliquary hollow: a low chalk-rib niche / shell-shaped mineral hollow under a shelf, store-like cavity as geology not a nest of bones, no turtle, no creature. "
        "Plot 4 Stormglass perch: a wind-scoured chalk shelf with a brass-rib fragment as a perch geometry, stunted sedge, not a climb ramp onto a wall. "
        "Every cluster grounded, volumetric clumps, condition variation, no identical copies, no stick/spike/fence drift, no real-world plant photographs, no animals, no dragons, no fortress apron dressing, no neon. "
        "REGEN OVERRIDE: TRUE FRONT ELEVATION strips. ZERO animals. ZERO leafless dead trees. ZERO crystal spikes. Plot 2 MUST show a clear empty 2 m packed-chalk travel lane."
    ),
    "crownlands_cave_exterior_language_v001": (
        COMMON
        + " SUBJECT: Crownlands NON-DRAGON CAVE EXTERIOR LANGUAGE, landform around mouths, not interior plans, not P1 chamber fittings, not a fortress. "
        "THREE isolated TRUE FRONT ELEVATION landform strips, camera on the horizon, generous gray. "
        "LEFT INNER exterior: human-scale mouth about 4.5 m by 3.6 m set in a layered pale chalk-rib SHELF. The opening is an UNMISTAKABLE RECTANGLE / SQUARE HOLE: a FLAT HORIZONTAL ashlar lintel beam, two vertical jambs, 90-DEGREE top corners. Modest brass meridian tick on the lintel as a FLAT disc, not an arch. Shallow cut-stone threshold, packed-chalk apron that is NOT a climb ramp. Overhang is a square geological shelf, not a cathedral portal. Optional 1.8 m mannequin. "
        "CENTER OUTER exterior: warzone mouth about 6 m by 4.2 m in a storm-scarred chalk CUT, broken brass-rib lintel, spalled ashlar, silt slope that is NOT climbable talus onto a wall. Combat-scarred, still a human dungeon entrance. Consistent with CrownlandsConceptP1 outer-cave-mouth landform: scarred cut, not a finished civic portal. "
        "RIGHT SHELF LANGUAGE callout: isolated chalk-rib bedding, rain-dark fractures, brass clamp plates as objects not glow, blue-slate shoring plate lying as a prop, no interior rooms. "
        "Forbidden: dragon skull, eggs, bones, treasure, neon, stained-glass rose, sequential gate, 30 m apron diagram, capital, 3/4 diorama, Gothic crypt facade. "
        "REGEN OVERRIDE: retry FAIL — LEFT opening was square but LEFT and CENTER were free-standing gate portals in empty space, which is gate drift, not cave landform. LEFT and CENTER MUST be CAVE MOUTHS CUT INTO SOLID CHALK HILLSIDES / SHELVES, holes in a cliff mass, NOT door frames standing alone, NOT gates, NOT portals. Surround each mouth with a bulky layered chalk-rib rock mass. LEFT INNER: a SQUARE hole in the shelf with a FLAT HORIZONTAL ashlar lintel and 90-degree corners. CENTER OUTER: a storm-scarred irregular cut in a broken chalk cliff, still human-scale, not a matching civic gate. RIGHT remains an isolated bedding/clamp shelf fragment. No skull/egg/treasure/dragon. P1 inner_cave_mouth remains interior authority."
    ),
    "crownlands_lod_material_lighting_reference_v001": (
        COMMON
        + " SUBJECT: Crownlands LOD / MATERIAL / LIGHTING REFERENCE for Meridian Oathroad construction. Same civic dwelling-scale wall bay, not a city, not Town Hall. "
        "TOP ROW four isolated FRONT ELEVATIONS of the SAME 8 m by 10 m two-storey chalk-gold dwelling with deep-blue-slate hipped roof, identical camera, generous gray: "
        "LOD0 full joints, mortar fractures, brass drip rib, silver latch, punched door and windows fully through; "
        "LOD1 simplified ashlar blocks, fewer mortar ticks, roof hips kept; "
        "LOD2 block masses, roof as a simple hip volume, door as a dark rectangle; "
        "LOD3 skyline silhouette only, hip roof outline, no windows, no needles, no cones. "
        "BOTTOM ROW: LEFT material callout strip of ashlar / blue slate / brass / silver / grit as small labeled chips with English labels ASHLAR, SLATE, BRASS, SILVER, GRIT only; "
        "RIGHT lighting reference of the LOD0 bay twice: even overcast studio versus dusk with ONE practical lantern object as geometry, warm as material not a glow sprite, no bloom, no VFX. Optional 1.8 m mannequin. "
        "FORBIDDEN: conical turrets, needle spires, Gothic, cathedral, Town Hall, Workshop, dragons, neon, baked volumetrics, changing identity across LODs. "
        "REGEN OVERRIDE: previous house had a church front-gable and round-arch windows. Roofs MUST be LOW HIPPED blue-slate with hips and ridges, NO front gable, NO church peak. Windows MUST be RECTANGULAR with flat lintels. LOD3 is a hip-roof silhouette box with no windows. Lantern is a metal object, not a bloom sprite."
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
