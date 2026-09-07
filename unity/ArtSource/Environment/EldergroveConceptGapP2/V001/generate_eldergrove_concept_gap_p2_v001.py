#!/usr/bin/env python3
"""Generate Grok Imagine 2D concept sheets for EldergroveConceptGapP2 V001.

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
    "Eldergrove only. Moonroot Vigil identity: pale mineral stone, dark timber, aged bronze root collars, desaturated bark, restrained moon-silver and pale-gold edgework. "
    "Asymmetric root-held masses, broad low curves, spaced oldgrowth. Crafted architecture first; only a FEW grounded roots seat into prepared bronze collars. "
    "NOT overly root-cluttered: roots do not wrap entire walls, do not hide doors, do not form a canopy, do not become a portal. "
    "No bright-green-only cue, no neon bioluminescence, no root portal, no cute sprites, no dense canopy hiding traversal, no floating island, no Open Crown Arbor Town Hall spectacle, no giant civic oculus, no fairy drift. "
    "No Stonehold Embermist palette-swap, no basalt-iron fortress language as the primary read, no lava, no soot-forge as identity, no Crownlands limestone palace, no Umbral violet fog, no Accordant cherry blossoms, no fifth realm. "
    "No fake facade shells: every building shown is a real enterable volume with readable wall thickness and punched door/window apertures. "
    "Include a simple unlabeled metric scale bar with five equal tick marks and a faint 0.5 m floor grid where the subject is object-scale. "
    "PBR materials, Black-Desert-inspired finish bar, not cartoon, not anime, not painterly concept-illustration. "
    "Same identity, identical scale in every panel of this sheet."
)

SHEETS: dict[str, str] = {
    "eldergrove_sequential_gate_outer_face_v001": (
        COMMON
        + " SUBJECT: Eldergrove sequential-gate OUTER FACE, visual language only. Topology is already locked by the technical plate and must be illustrated, not changed. "
        "Stable IDs remain gate_eldergrove_greenveil_outer on this outer leaf pair; the complex is wall_complex_eldergrove_greenveil. "
        "ONE isolated TRUE FRONT ELEVATION, camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4. "
        "A LONG LOW HORIZONTAL pale-mineral curtain-wall, about 80 m of wall shown, like a dam. EXACTLY ONE ceremonial gated opening in the middle, wall continuing far to both sides. "
        "Gate opening about 8 m wide by 6 m tall. ONE pair of leaves: outer face is dark-timber with bronze hardware; pale mineral arch with aged bronze collars. "
        "CRITICAL CLIMB RULE: ZERO giant roots. ZERO trees. ZERO A-brace trunks that could be climbed to walltop. Outer attacker face is BLANK unclimbable masonry: no ladders, no holds, no talus ramp, no root ladder, no wrapping bark. Ornament is bronze collars and dark-timber banding only. Architecture first. "
        "Merlon rhythm on the walltop. Optional 1.8 m gray mannequin at the threshold. Empty gray studio. "
        "FORBIDDEN: giant exterior roots; climb-assist trunks to walltop; two gates side by side; inner and outer barriers as twin front elevations; 3/4 hero shot; plus-shaped courtyard; sequential dual-gate collage; neon; dragons; changing the sequence. "
        "REGEN OVERRIDE: This sheet OVERRIDES any root-held-mass identity. Draw ABSOLUTELY NO TREES, NO ROOTS, NO BARK, NO STUMPS, NO VINES on or beside the wall. The outer face is a blank pale-mineral dam with one timber door. If you draw a tree the sheet fails."
    ),
    "eldergrove_sequential_gate_inner_face_v001": (
        COMMON
        + " SUBJECT: Eldergrove sequential-gate INNER FACE, visual language only. Topology is locked. "
        "Stable inner-gate ID remains gate_eldergrove_greenveil. This is the inner barrier face seen FROM THE INNER REALM. "
        "ONE isolated TRUE FRONT ELEVATION, camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4. "
        "A LONG LOW HORIZONTAL pale-mineral curtain-wall, about 80 m of wall shown. EXACTLY ONE inner gated opening in the middle, quieter civic language than the outer face, same materials: pale mineral, dark timber, aged bronze collars, restrained moon-silver. "
        "ONE pair of inner leaves. Inner/defender side MUST show a stone stair rising to walltop on THIS face only. "
        "CRITICAL ROOT BUDGET: ZERO wrapping tree trunks. ZERO root masses taller than 1.2 m. At most TWO small bronze-collar root seats, each smaller than the 1.8 m mannequin, at the base of the wall only. Architecture first. "
        "No attacker ladders. Optional 1.8 m gray mannequin. Empty gray studio. "
        "FORBIDDEN: wrapping corner trees; root stools flanking the door; drawing outer and inner barriers as two side-by-side twins; 3/4; collage; outer-face ladders; root portal; dragons; neon; topology change."
    ),
    "eldergrove_sequential_gate_longitudinal_section_v001": (
        COMMON
        + " SUBJECT: two-panel construction drawing. Allowed text only: OUTER x=0, INNER x=18, and one unlabeled scale bar. "
        "LEFT = true top-down plan. RIGHT = true flat side cut. Generous gray between. "
        "LEFT PLAN: pale-mineral wall band left-to-right. Pale road left-to-right through the wall. TWO separate closed double-doors: at LEFT, a pair of dark-timber leaves with a visible center seam drawn as two filled dark rectangles sitting on the road, label OUTER x=0. Then 18 m of pale road under a LIGHT dashed roof outline, not a solid dark slab. At RIGHT, a second pair of dark-timber leaves with a visible center seam as two filled dark rectangles on the road, label INNER x=18. The two door-pairs are separate objects; do not merge them into one dark inset panel. Stair hatch only on the north edge, to the RIGHT of INNER x=18. No south stairs. "
        "RIGHT SECTION: flat 2D cut through a closed shoebox tunnel. Left to right: outer ground; OUTER closed double-door shown as a vertical filled dark rectangle the full height of the opening, label OUTER x=0; dark interior cavity with roof and one gray mannequin; INNER closed double-door as a second vertical filled dark rectangle the full height of the opening, label INNER x=18; inner ground. Each door occludes the tunnel. Do not place doors on the left and right walls facing the camera. Do not draw an open proscenium. Stair only to the right of the inner door. "
        "Pale mineral, dark timber, bronze. No trees, no roots, no courtyard."
    ),
    "eldergrove_sequential_wall_walltop_modules_v001": (
        COMMON
        + " SUBJECT: Eldergrove sequential-gate WALL and WALLTOP DEFENDER MODULES, three isolated modular pieces of the SAME construction language, identical scale, generous gray, not overlapping. "
        "True orthographic cameras, ZERO perspective, ZERO 3/4. "
        "LEFT: 12 m curtain-wall bay FRONT ELEVATION, pale mineral masonry, dark-timber banding, compressed parapet, no door, cut ends show 1.8 m wall thickness as a dark rectangle. ZERO trees. At most two tiny bronze-collar seats at the base, shorter than 0.4 m. Optional 1.8 m gray mannequin. "
        "CENTER: WALLTOP WALK PLAN, TRUE TOP-DOWN looking straight down at 90 degrees, of a LONG NARROW walk about 2.4 m wide and 12 m long, like a ladder drawn in plan. Merlon ticks on the OUTER long edge, inner parapet on the other long edge, defender stair hatch on the INNER side only. This CENTER panel is a PLAN, not a front wall, not an elevation, NO window, NO 3/4. "
        "RIGHT: DEFENDER STAIR as a FLAT SIDE ELEVATION of a wall CUT: LEFT half of this panel is INNER courtyard with a stone stair rising to walltop; RIGHT half of this panel is OUTER attacker face — a BLANK battered pale-mineral wall, zero treads, zero ladders, zero holds, zero talus, zero climbable roots, zero windows. "
        "Forbidden: square room plan, front-elevation pretending to be a walk plan, exterior ladders, climbable outer stairs, 3/4 dump, wrapping roots, stump palisade, dragons, neon. "
        "REGEN OVERRIDE: THREE PANELS. LEFT = front wall elevation, no trees. RIGHT = flat wall-thickness cut with stair on inner left and blank outer right. CENTER IS THE CRITICAL PANEL: a CAD top-down PLAN of a walltop walk. Imagine looking straight down from a drone at 90 degrees at a long narrow paved path 2.4 m wide by 12 m long. You see ONLY the paving surface, a row of tiny merlon squares along the outer long edge, a thin inner parapet line, and one stair hatch rectangle. It must look like a ladder or hallway floor plan, NEVER like a standing wall, NEVER crenellations in elevation, NEVER a window in a wall face."
    ),
    "eldergrove_fortress_plan_apron_v001": (
        COMMON
        + " SUBJECT: Eldergrove ONE-GATE fortress as a SINGLE architectural SITE PLAN. Inventory placeholders fortress_eldergrove_01..04 are not drawn as four forts; this is ONE typical fortress. "
        "Camera is STRAIGHT DOWN at 90 degrees like a CAD roof plan. Roofs and walls are FLAT 2D footprints only, like paper cutouts. "
        "NO facades, NO visible outer wall faces, NO axonometric, NO isometric, NO 3/4, NO floating island diorama, NO cliff sides. "
        "One connected irregular polygonal perimeter drawn as a thick pale-mineral ring. EXACTLY ONE entrance on the south wall. That entrance is completely occupied by a CLOSED paired gate: two thick dark-timber leaves with a center seam and bronze hinges SPAN the full gap and the full wall thickness so courtyard paving cannot be seen through the opening. The leaves are a barrier bar, not a small door drawing sitting in an open gap, not two isolated piers. NO stairs sticking into the apron. NO other breach, NO second opening. Tiny label GATE on the leaves is allowed; no other extra text except the apron label. "
        "Low-broad keep as an irregular-but-broad enclosed ROOF footprint toward the rear, separate from the gate. Central courtyard FLAG-MAST SOCKET: a 4 m circle with a tiny mast-dot in the middle, DISTINCT from the keep, NOT on the keep roof, NOT a fountain, NOT a tree, NOT a drain, NOT a fire pit. "
        "Around the entire perimeter: a continuous dashed offset boundary with explicit readable text label '>=30m EMPTY DEFENSIVE APRON' (this one label is required; no other text, logos, or UI). The apron band is flat packed pale silt/mineral with NOTHING on it — no rocks, no stairs, no trees, no roots, no foliage, no terrain piles, no debris, no props, no climb-assist. Courtyard paving is inside the walls only. "
        "CRITICAL: ZERO ROOTS anywhere on this sheet. No roots crossing the perimeter. No roots on the wall. No roots in the courtyard. No roots on the apron. All roots forbidden on this plan because they read as climb assists. "
        "Gray studio paper background. No sequential dual-gate, no Worldscar, no capital city, no second gate. "
        "REGEN OVERRIDE: previous images still read as an open gap between piers. Draw the sole south entrance as a thick CLOSED paired timber gate leaf/barrier spanning the entire gap. Preserve one connected irregular perimeter, low broad enclosed keep, separate central flag-mast socket, continuous dashed boundary labeled >=30m EMPTY DEFENSIVE APRON, zero roots/props on the apron."
    ),
    "eldergrove_fortress_elevations_v001": (
        COMMON
        + " SUBJECT: Eldergrove ONE-GATE fortress as TWO FLAT orthographic ELEVATIONS of the SAME fortress. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT: FRONT elevation of a LONG LOW HORIZONTAL curtain-wall, about 80 m of wall shown, EXACTLY ONE gated opening in the middle, wall continuing far to both sides like a dam. Keep is a LOW BROAD irregular-but-rectangular block only slightly taller than the wall, sitting BEHIND the wall, NOT on top of the wall, NO octagonal keep, NO pagoda roof, NO round tower, NO needle spire, NO treehouse keep. RIGHT: RIGHT SIDE elevation of the SAME long wall stretching left-right with the broad keep mass behind it, proving one connected perimeter and no second gate. Generous gray between. "
        "Silhouette MUST be a wide short rectangle, broad low curves, NOT a castle with a central tower. Walls two storeys plus walltop; keep three storeys but BROAD not tall. Pale mineral; dark timber banding; aged bronze collars at a few corners; compressed openings. "
        "CRITICAL: ZERO roots on the OUTER face. ZERO sculpted root figures. ZERO climb holds. Outer face is blank pale mineral. One obvious gate opening. No exterior stairs, no climbable talus, no debris, no foliage on the apron. Apron in front of the wall is empty flat ground. "
        "FORBIDDEN: cathedral keep, octagonal pagoda keep sitting on the wall, needle spires, wrapping tree keep, outer-face root statues, oval floating island, two gates, sequential dual-barrier collage, 3/4 axonometric, exterior stairs, climbable talus, roots/foliage/terrain on the apron. "
        "REGEN OVERRIDE: ZERO roots, ZERO stumps, ZERO trees anywhere on either elevation, including corners. Outer face is blank pale mineral plus timber banding only. Keep is a low broad rectangular hip-roof block behind the wall, not a pagoda."
    ),
    "eldergrove_fortress_keep_flag_mast_v001": (
        COMMON
        + " SUBJECT: Eldergrove fortress KEEP SHELL plus a SEPARATE CENTRAL FLAG-MAST SOCKET, not a city, not a sequential dual-gate. "
        "Three isolated panels left-to-right of the SAME keep language with generous gray. "
        "LEFT: KEEP ROOF PLAN, true top-down of the keep only, walltop walk, punched interior door ticks. Do NOT draw the flag on the keep. A SEPARATE courtyard FLAG PLINTH is drawn BELOW the keep plan as its own 4 m circle with a mast socket, clearly detached, labeled by isolation not by text. "
        "CENTER: FRONT ELEVATION of the low broad warden-keep ONLY, about 36 m across and 16 m to walltop, real wall thickness about 1.2 m, pale mineral, dark timber, aged bronze collars, compressed door about 3.6 m by 4.2 m punched through. ZERO flag mast on this keep elevation. ZERO giant roots. Keep is an enclosed volume, not a hollow ring. Optional 1.8 m mannequin. "
        "RIGHT: isolated FLAG-MAST as a COMPLETELY SEPARATE OBJECT: FRONT ELEVATION of a 6 m pale-mineral-and-bronze mast on a 4 m blocking plinth, empty banner SOCKET as geometry only with no cloth and no VFX, 1.8 m mannequin beside it. This mast is not fused into the keep, not a column of the keep, not a chimney. "
        "No cathedral, no wrapping tree, no second gate, no climb-assist, no 3/4 diorama, no apron foliage, no dragons. "
        "REGEN OVERRIDE: ZERO trees and ZERO wrapping roots on the keep plan and keep elevation. Keep is a SOLID enclosed roof, not a hollow oculus ring. Flag plinth is a separate 4 m circle under the plan. Right mast is a thin pole in a socket on a blocking plinth, not a classical column, not fused to the keep."
    ),
    "eldergrove_terrain_grades_worldscar_wallend_v001": (
        COMMON
        + " SUBJECT: authored Eldergrove TERRAIN CONSTRUCTION PLATE: inner/outer GRADE, nonperiodic WORLDSCAR brink, and WALL-END terrain termination. Not isolated props, not a fortress, not a map. "
        "THREE isolated panels with generous gray. "
        "LEFT: INNER versus OUTER GRADE as a TRUE 2D geologic SIDE CUT like a textbook cross-section. Camera in the cut plane. Left half is inner 33-percent-class safe-pocket pale mineral shelves with a gentle packed-silt grade; right half is outer warzone with more broken shelves and wind-scoured silt. The split is an ORGANIC GRADE TRANSITION, not a wall and not a palette-swap. NONPERIODIC bedding: irregular block sizes, offset beds, NOT repeating layered-cake stripes, NOT a 3/4 boulder island. "
        "CENTER: NONPERIODIC WORLDSCAR BRINK as a FRONT ELEVATION of a sheer authored pale-mineral/desaturated-bark cliff into empty gray void — macro mass greater than 5 m, meso fracture 0.25 to 5 m, micro surface breakup under 0.25 m. Fractured, offset, nonperiodic. Optional 1.8 m gray mannequin at the brink. No lightning, no magma, no baked volumetrics, not a repeating texture, not a layered cake. "
        "RIGHT: WALL-END TERRAIN TERMINATION: a sequential-wall curtain END in FRONT ELEVATION, a horizontal masonry wall band about 12 m long seating INTO an impassable pale-mineral cliff mass so the wall is visibly FUSED into the cliff. The cliff is a BLANK stop, not a ramp, not climbable talus, not a root ladder, not a decorative door stele, not a monument panel. Outer face of the wall remains unclimbable. "
        "Forbidden: three isolated prop boulders, repeating strata cake, primitive cones, decorative stelae, fortress apron dressing, sequential dual-gate collage, lava, dragons, neon. "
        "REGEN OVERRIDE: LEFT is a FLAT textbook geologic cross-section filling the panel, not a 3/4 cake-layered boulder island. CENTER is a wide sheer cliff FRONT ELEVATION into gray void, nonperiodic fractures, not a standalone prop. RIGHT is a LONG HORIZONTAL masonry wall running into a cliff mass and fused with it — not a decorative door, not a stele, not roots climbing the panel. ZERO roots on all three panels. LEFT bedding is OFFSET irregular blocks, NEVER repeating layered-cake stripes. Do not draw isolated 3/4 rock sculptures."
    ),
    "eldergrove_terrain_route_bed_v001": (
        COMMON
        + " SUBJECT: authored Eldergrove ROUTE BED, not a continent map, not a fortress apron. 14.4 km-class route visual language only. "
        "THREE isolated panels of the SAME route language, identical scale, generous gray. "
        "LEFT: CROSS SECTION of the route, TRUE SIDE CUT like a sliced loaf viewed from the cut face, camera in the cut plane, ZERO 3/4, ZERO isometric slab: packed pale-mineral/silt BED, two worn wheel RUTS as clear depressions in the cut, raised SHOULDERS, shallow drainage gutter, verge. Not a 3/4 slab, not a crate, not metal rails, not a smooth ramp, not a painted texture stripe, not a wavy mattress. "
        "CENTER: TRUE TOP-DOWN of about 12 m of packed-silt road showing nonperiodic ruts and repair patches. Shoulder stones only outside a 4 m travel lane. NO metal tram rails, NO modern curbs, NO roots across the travel lane. "
        "RIGHT: PLAYER-SCALE ROCK KIT, three isolated unique ANGULAR pale-mineral rocks sitting on a ground line: 0.4 m faceted cobble, 1.2 m blocky fractured slab, 2.4 m irregular boulder. Broken masonry language, faceted, NOT cones, NOT teardrops, NOT smooth eggs, not copies of each other scaled up. 1.8 m gray mannequin beside the boulder. "
        "Forbidden: repeating strata stripes, primitive cones, teardrop rocks, stick vegetation, picket-fence rocks, climb-assist talus against a wall, metal rails, dragons. "
        "REGEN OVERRIDE: LEFT must be a FLAT sliced-loaf cross-section facing the camera, showing two distinct U-shaped wheel-rut depressions, raised shoulders, and a drainage gutter — not a 3/4 mattress slab. RIGHT rocks are faceted angular broken masonry, NOT teardrops, NOT eggs, NOT cones, three different silhouettes. The 2.4 m boulder is a squat broken angular BLOCK with a flat fractured top, never a standing egg. Include a shallow square drainage gutter on one side of the left cut."
    ),
    "eldergrove_terrain_outer_biome_reads_v001": (
        COMMON
        + " SUBJECT: Eldergrove THREE OUTER BIOME READS of the SAME Moonroot Vigil continent geology, not palette-swaps of other realms. "
        "THREE isolated orthographic FRONT ELEVATION landform STRIPS like theater backdrops or geologic outcrop elevations, about 8 m wide each, generous gray, not overlapping, not a collage. FLAT elevations, camera on the horizon, ZERO 3/4, ZERO floating islands, ZERO identical meshes recolored. "
        "LEFT labeled PURE. Outer Eldergrove warzone elevation: pale mineral shelves, desaturated bark seating as landform not a forest wall, packed silt. Unique shelf rhythm. Moonroot Vigil only. "
        "CENTER labeled UMBRAL BLEND. SAME Eldergrove shelf grammar but DIFFERENT FORM: cooler ash-dark mineral VEINS cutting through the shelves and soot in fractures at a Worldscar-adjacent contact. Still pale stone dominant. NOT Umbral violet fog, NOT ashvein iron plates, NOT a recolor of the PURE mesh, NOT a fifth realm. Form contact, not a palette swap. "
        "RIGHT labeled STONEHOLD BLEND. SAME Eldergrove shelf grammar but DIFFERENT FORM: basalt-grit POCKETS and heat-dark staining in cracks at a Stonehold-facing contact, broken blockier shelves on one side only. Still pale stone dominant. NOT Embermist iron banding, NOT lava, NOT a basalt fortress, NOT a recolor of the PURE mesh. Form contact, not a palette swap. "
        "Optional 1.8 m mannequin on PURE only. Forbidden: three 3/4 diorama islands, three copies of one mesh, Crownlands limestone, Accordant blossoms, neon, dragons, fortress apron, sequential gate."
    ),
    "eldergrove_ecosystem_family_orthos_v001": (
        COMMON
        + " SUBJECT: volumetric Moonroot Vigil ECOSYSTEM FAMILY sheet: three unique LOW GROUNDED vegetation families plus two rock/ground families. "
        "FIVE isolated grounded clusters on one ground line, identical scale, generous gray between them, NOT overlapping, NOT a collage. True FRONT ELEVATIONS. "
        "1 Hollowbark Crown: LOW leafy oldgrowth sapling clump 0.9 to 1.4 m, dense desaturated foliage covering the branches, several short trunks, a VOLUME of leaves, not a leafless stick, not climbable. "
        "2 Moonroot Sedge: pale-gray-green iron-desaturated sedge CLUMP, a volume of many blades, 0.6 to 1.1 m tall, not a stick, not a spike, not a fence, not bright-green-only. "
        "3 Sunmane Edge Fern: low fern MASS with overlapping fronds, 0.4 to 0.8 m, moon-silver underside as MATERIAL not glow, not a single spike. "
        "4 Pale Mineral Shelf: a LOW HORIZONTAL oval of squat blunt irregular dull pale-mineral cobbles, ONE ROCK HIGH, maximum height 0.45 m, about knee height on the mannequin, much wider than tall like a pancake. Rocks lie on their sides as rounded lumps. Do not stack. Do not make a cairn or pyramid. ZERO upright shards, ZERO points, ZERO needles, ZERO blades, ZERO crystals. "
        "5 Packed-silt Rootnest: low grounded silt-and-pebble nest 0.4 to 0.8 m with a FEW bronze-collar seats, not a wrapping tree, not a portal, not climbable. "
        "1.8 m gray mannequin at far left for scale. No animals, no dragons, no stick/spike/fence drift, no crystal palisade, no climb-assist trunk, no cards, no alpha slivers, no real-world plants as photographs. "
        "REGEN OVERRIDE: family 4 previous piles were still too tall or crystal-like. Make family 4 a flat knee-high cobble scatter. Keep Hollowbark leafy. Keep sedge, fern, and low rootnest."
    ),
    "eldergrove_ecosystem_composition_plots_v001": (
        COMMON
        + " SUBJECT: Moonroot Vigil ECOSYSTEM COMPOSITION, four isolated 8 m-wide ground plots as true orthographic FRONT ELEVATION strips, generous gray, not overlapping. "
        "Plot 1 inner oldgrowth shelf: sparse Hollowbark Crown plus Moonroot Sedge, lots of bare pale mineral, traversal-readable gaps. "
        "Plot 2 floodbasin verge: denser Moonroot Sedge plus Sunmane fern in a lee pocket, grounded, not a swamp wall. "
        "Plot 3 route verge: sedge along a packed-silt road edge with a 2 m clear travel lane, no obstruction of the route, no roots across the lane. "
        "Plot 4 Worldscar exposure: stunted sedge plus pale mineral shelf pile, wind-scoured, not piled as a climb ramp. "
        "Every cluster grounded, volumetric clumps, condition variation, no identical copies, no stick/spike/fence drift, no animals, no dragons, no fortress apron dressing, no dense canopy, no neon. "
        "REGEN OVERRIDE: TRUE FRONT ELEVATION strips, not 3/4 floating islands. ZERO leafless dead trees. ZERO crystal spikes. ZERO picket-fence rows. Plot 3 MUST show a clear empty 2 m packed-silt travel lane with plants only on the verges."
    ),
    "eldergrove_interior_room_plates_v001": (
        COMMON
        + " SUBJECT: developed FURNISHED INTERIOR KIT, Eldergrove-appropriate warden rooms. Dimensioned TRUE TOP-DOWN FLOOR PLANS, complete circulation, no cutaway-collage, no fake shell. "
        "TWO isolated TRUE TOP-DOWN FLOOR PLANS with generous gray between them, not a 3/4 cutaway, not a dollhouse, not stacked isometric floors, not a ripped-off roof. "
        "LEFT: GUARD / READY ROOM about 10 m by 12 m. Empty weapon racks, benches, duty desk, 1.2 m door swings, 1.5 m circulation aisle, stair bulkhead to walltop on the inner side. Exterior wall 1.2 m thick, interior partitions 0.3 m. Complete four walls. "
        "RIGHT: ROOT-MAINTENANCE / BRONZE-COLLAR JOINERY FLOOR about 14 m by 16 m (the Eldergrove forge analog, NOT a Stonehold smithy). Two workbenches, a bronze-collar jig as a WORKBENCH OBJECT, herbal still as a copper/bronze vessel not a magic cauldron, bark-lashing rack, 2 m work aisle, wagon door 2.4 m, stores along one wall. Traversable. No anvils, no coal bunker, no magma. "
        "CRITICAL: ZERO living trees inside the rooms. ZERO root portals. ZERO trees in bronze rings. Corner bronze-collar seats may be tiny wall hardware only. "
        "Furniture unoccluded. Moonroot materials: pale mineral, dark timber, aged bronze. Forbidden: dollhouse cutaway, fake facade shell, Embermist forge, living tree portal, dragons, people, neon, missing back wall. "
        "REGEN OVERRIDE: ZERO living trees, ZERO root masses, ZERO bronze-ring tree portals inside either room. The joinery jig is a WORKBENCH with bronze collars as TOOLS on the table, not a tree in a ring. Circulation aisles stay empty and walkable."
    ),
    "eldergrove_interior_civic_furniture_v001": (
        COMMON
        + " SUBJECT: Eldergrove civic/service ROOM PLATE plus UNIQUE FOCAL FURNITURE orthos. "
        "LEFT third: TWO stacked TRUE TOP-DOWN plans with gray between them, not a 3/4 cutaway. TOP: SERVICE / STORES about 10 m by 12 m, supply racks 0.55 m deep, 1.25 m access run, stackable crates, covered cistern basin, service counter, rear door, traversable aisles. BOTTOM: CIVIC MOOT / WORKROOM about 12 m by 14 m, circular 2.8 m moonroot map table, six movable chairs, steward desk, archive shelf bays, blank notice board, 1.20 m center aisle, 1.2 m door swings. Exterior wall 1.2 m, partitions 0.3 m. Complete enterable volumes. "
        "RIGHT two-thirds: UNIQUE FOCAL FURNITURE as isolated orthographic FRONT ELEVATIONS in a 2-row by 4-column grid with GENEROUS empty gray between every object. No overlapping, no collage, no shared ground plane merging them into a room. "
        "Objects: circular moonroot map table 2.8 m; herbal preparation bench 2.0 m; archive niche shelf 0.9 m wide by 2.0 m tall; bronze-collar joinery jig 1.6 m; single warden cot 2.1 m; root-maintenance clamp bench 1.8 m; blank notice board 1.2 m with NO text; steward desk 1.6 m. "
        "One 1.8 m featureless gray mannequin for scale. Pale mineral, dark timber, aged bronze, quiet worn wool. "
        "Forbidden: writing, heraldry, logos, modern office plastic, collage rooms, dragons, neon, fake shells."
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
