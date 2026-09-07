#!/usr/bin/env python3
"""Generate Grok Imagine 2D concept sheets for StoneholdConceptGapP2 V001.

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
    "Geometry-readable orthographic architectural / construction drawing. Strict orthographic cameras, no perspective, no 3/4 hero shot, no isometric dollhouse cutaway, no axonometric island diorama. "
    "Neutral seamless gray studio, even studio lighting, no sky, no landscape backdrop except the subject terrain that belongs to the sheet. "
    "Clean unoccluded views, generous empty gray space between panels, no overlapping objects, no collage, no moodboard, no photobash, no exploded 3/4 kit dump. "
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
    "stonehold_sequential_gate_section_plan_v001": (
        COMMON
        + " SUBJECT: Stonehold sequential TWO-BARRIER wall-complex VISUAL LANGUAGE only. "
        "Topology is already locked and must be illustrated, not changed: outer realm then outer barrier then controlled passage then inner barrier then inner realm. "
        "Stable IDs remain gate_stonehold_faultline (inner) and gate_stonehold_faultline_outer. One continuous wall complex, not two unrelated walls. "
        "TWO isolated panels with generous gray between them. "
        "LEFT: TRUE TOP-DOWN PLAN like a CAD drawing of a HORIZONTAL SAUSAGE-SHAPED WALL BAND, not a plus-shaped courtyard, not a crossing plaza. A long dark curtain-wall runs LEFT TO RIGHT. A pale road crosses it left-to-right. TWO thin barrier lines cross the road: outer gate-leaf pair on the LEFT, then an 18 m dark rectangular passage corridor, then inner gate-leaf pair on the RIGHT. Stairs exist ONLY on the north/defender edge of the wall band, never south/attacker. "
        "RIGHT: LONGITUDINAL SECTION cutting along the road, like a subway-tunnel section: left outer leaves, then a roofed 18 m passage with a mannequin inside, then inner leaves, then inner realm. Two vertical gate planes, one tunnel. Camera infinitely far, ZERO perspective. "
        "FORBIDDEN: plus-shaped courtyard; single chamber cutaway; two front elevations of twin gates; exploded 3/4 dump; exterior attacker stairs; climbable talus; lava; magma; dragons; changing the sequence."
    ),
    "stonehold_sequential_wall_module_kit_v001": (
        COMMON
        + " SUBJECT: Stonehold sequential-gate WALL MODULE KIT, three isolated modular pieces of the SAME construction language, identical scale, generous gray, not overlapping, not an exploded 3/4 dump. "
        "True orthographic FRONT ELEVATIONS, camera on the horizon, ZERO perspective, ZERO 3/4. "
        "LEFT: 12 m curtain-wall bay, tectonic horizontal mass, battered basalt FINS not random rubble, heat-darkened IRON-PLATE banding, compressed parapet, no door, cut ends show 1.8 m wall thickness as a dark rectangle. "
        "CENTER: faultline buttress plus ONE pair of DARK-IRON LAYERED GATE LEAVES filling an approximately 8 m wide by 6 m tall rectangular opening. Mechanical plates, not timber barn doors, not an arch of wood. One pair only. Heavy pintle hinges readable in the leaf edge. No fire sprites. "
        "RIGHT: wall-end termination: a clean battered block that would seat into a CLIFF, outer face blank and unclimbable. Do NOT pile climbable rubble or talus against it. A flat cliff mass may touch the end but must not form a ramp. "
        "Optional 1.8 m gray mannequin beside LEFT. "
        "Forbidden: 3/4 exploded kit, two gates as twins, climbable talus, lava, magma, timber barn doors, dragons."
    ),
    "stonehold_walltop_defender_modules_v001": (
        COMMON
        + " SUBJECT: Stonehold DEFENDABLE WALLTOP MODULES for the sequential wall complex. NO LAVA. NO MAGMA. NO GLOWING CRACKS. NO LADDERS ON THE OUTER FACE. "
        "Three isolated panels of the SAME walltop language, identical scale, generous gray, not overlapping. "
        "LEFT: WALLTOP WALK PLAN, true top-down of a LONG NARROW walk, not a square room. Walk about 2.4 m wide and 12 m long, merlon ticks on the OUTER long edge, inner parapet on the other long edge, defender stair hatch on the INNER side only. "
        "CENTER: FRONT ELEVATION of merlon rhythm with a 1.8 m gray mannequin standing ON the walk behind merlons. Outer face is battered basalt and iron plates, COLD stone, no fire, no lava. "
        "RIGHT: DEFENDER STAIR as SIDE ELEVATION of a wall cut: LEFT side of this panel is INNER courtyard with a stone stair rising to walltop; RIGHT side of this panel is OUTER attacker face — a BLANK battered wall, zero treads, zero ladders, zero holds, zero talus. "
        "True orthographic cameras. Forbidden: square room plan, lava, magma, exterior ladders, climbable outer stairs, 3/4 dump, dragons."
    ),
    "stonehold_fortress_plan_apron_v001": (
        COMMON
        + " SUBJECT: Stonehold ONE-GATE fortress as a SINGLE architectural SITE PLAN. "
        "Camera is STRAIGHT DOWN at 90 degrees like a CAD roof plan. Roofs and walls are FLAT 2D footprints only, like paper cutouts. "
        "NO facades, NO visible outer wall faces, NO axonometric, NO isometric, NO 3/4, NO floating island diorama, NO cliff sides. "
        "One connected irregular polygonal perimeter drawn as a thick dark ring. EXACTLY ONE gated entrance as a GAP in the ring, flush with the wall, NO stairs sticking into the apron. "
        "Low-broad keep as a rectangular/octagonal ROOF footprint toward the rear. Central FLAG-ANCHOR: a 4 m circle with a tiny mast-dot in the middle, NOT a fountain, NOT a drain, NOT a fire pit. "
        "Around the entire perimeter: a 30 m EMPTY APRON of flat packed ash with NOTHING on it — no rocks, no stairs, no trees, no talus, no debris, no foliage, no props, no climb-assist. Faint dashed offset line. Courtyard paving is inside the walls only. "
        "Gray studio paper background. No sequential dual-gate, no Worldscar, no capital city, no second gate."
    ),
    "stonehold_fortress_elevations_v001": (
        COMMON
        + " SUBJECT: Stonehold ONE-GATE fortress as TWO FLAT orthographic ELEVATIONS of the SAME fortress. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT: FRONT elevation of a LONG LOW HORIZONTAL curtain-wall, about 80 m of wall shown, EXACTLY ONE gated opening in the middle, wall continuing far to both sides like a dam. Keep is a LOW BROAD RECTANGULAR block only slightly taller than the wall, sitting BEHIND the wall, NO round tower, NO drum keep, NO octagonal turret. RIGHT: RIGHT SIDE elevation of the SAME long wall stretching left-right with the rectangular keep mass behind it, proving one connected perimeter and no second gate. Generous gray between. "
        "Silhouette MUST be a wide short rectangle, tectonic horizontal mass, NOT a castle with a central tower. Walls two storeys plus walltop; keep three storeys but BROAD not tall. Battered basalt; iron-plate banding; compressed openings. "
        "One obvious gate opening. No exterior stairs, no climbable talus, no buttress steps that become ramps, no roots, no debris piled against the wall. Apron in front of the wall is empty flat ground. "
        "FORBIDDEN: cathedral keep, needle spires, ziggurat, oval floating island, lava glow in openings, two gates, sequential dual-barrier collage, 3/4 axonometric, exterior stairs, climbable talus."
    ),
    "stonehold_fortress_keep_flag_anchor_v001": (
        COMMON
        + " SUBJECT: Stonehold fortress KEEP SHELL plus CENTRAL FLAG-ANCHOR, not a city, not a sequential dual-gate. "
        "Three isolated panels left-to-right of the SAME keep language with generous gray. "
        "LEFT: KEEP ROOF PLAN, true top-down, walltop walk, punched interior door ticks; a separate courtyard FLAG PLINTH drawn in front of the keep as a 4 m circle with a mast socket. "
        "CENTER: FRONT ELEVATION of the low broad forge-keep, about 36 m across and 16 m to walltop, real wall thickness about 1.2 m, battered fins, compressed door about 3.6 m by 4.2 m punched through. Flag mast is a SEPARATE object in the courtyard, not fused into the keep. Optional 1.8 m mannequin. "
        "RIGHT: isolated FLAG-ANCHOR: FRONT ELEVATION of a 6 m heat-darkened iron-and-basalt mast on a 4 m blocking plinth, empty banner socket as geometry only with no cloth and no VFX, 1.8 m mannequin beside it. "
        "No cathedral, no lava, no magma, no dragons, no second gate, no climb-assist, no 3/4 diorama."
    ),
    "stonehold_terrain_geology_worldscar_v001": (
        COMMON
        + " SUBJECT: authored Stonehold TERRAIN GEOLOGY and WORLDSCAR brink, material-and-form sheet, not a fortress, not a map. "
        "Two isolated panels with generous gray. "
        "LEFT: VERTICAL GEOLOGIC SECTION of continent crust. NONPERIODIC fractured basalt strata: irregular block sizes, offset beds, mineral veins, ash partings, heat-darkened iron staining. NOT repeating stripes, NOT a layered cake, NOT procedural noise bands, NOT primitive cones. Worldscar brink at the far right as a sheer authored cliff into empty gray void — no lightning, no magma, no baked volumetrics. "
        "RIGHT: WORLDSCAR BRINK FRONT ELEVATION at player scale: fractured cliff with three readable scales — macro mass greater than 5 m, meso fracture 0.25 to 5 m, micro surface breakup under 0.25 m. Optional 1.8 m gray mannequin at the brink. "
        "Materials: matte basalt, iron grit, soot, ash. Forbidden: repeating texture motifs three times, smooth ramps, isolated cone spires, lava, magma, glowing cracks, dragons, fortress apron, sequential gate."
    ),
    "stonehold_terrain_route_bed_rocks_v001": (
        COMMON
        + " SUBJECT: authored Stonehold ROUTE BED and PLAYER-SCALE ROCKS, not a continent map, not a fortress apron. "
        "Three isolated panels of the SAME route language, identical scale, generous gray. "
        "LEFT: CROSS SECTION of the 14.4 km-class route, TRUE SIDE CUT like a civil-engineering drawing: packed-basalt BED, two worn wheel RUTS as depressions, raised SHOULDERS, shallow drainage gutter, verge. Not a 3/4 slab, not a crate, not metal rails, not a smooth ramp, not a painted texture stripe. "
        "CENTER: TRUE TOP-DOWN of about 12 m of packed-ash road showing nonperiodic ruts and repair patches. Shoulder rocks only outside a 4 m travel lane. NO metal tram rails, NO modern curbs. "
        "RIGHT: PLAYER-SCALE ROCK KIT, three isolated unique ANGULAR basalt rocks sitting on a ground line: 0.4 m faceted cobble, 1.2 m blocky fractured slab, 2.4 m irregular boulder. Not cones, not smooth eggs, not copies of each other scaled up. 1.8 m gray mannequin beside the boulder. "
        "Forbidden: repeating strata stripes, primitive cones, stick vegetation, picket-fence rocks, climb-assist talus against a wall, lava, dragons."
    ),
    "stonehold_ecosystem_family_orthos_v001": (
        COMMON
        + " SUBJECT: volumetric Embermist ECOSYSTEM FAMILY sheet: three vegetation families plus two rock/ground families. "
        "FIVE isolated grounded clusters on one ground line, identical scale, generous gray between them, NOT overlapping, NOT a collage. True FRONT ELEVATIONS. "
        "1 EmberCrown Sedge: black-green iron sedge CLUMP, a volume of many blades wind-combed, 0.6 to 1.1 m tall, not a stick, not a spike, not a fence. "
        "2 Faultlace Fern: low fern MASS with overlapping fronds, 0.4 to 0.8 m, not a single spike. "
        "3 Ironbell Bloom: squat mineral-bell cluster, bronze-iron bells hanging inside a bushy volume 0.5 to 0.9 m, not fence posts, not a trophy of hanging bells, not flowers as daisies. "
        "4 Basalt FractureFan: LOW radiating FIN cluster lying close to the ground like a broken fan, 0.4 to 0.9 m high and 1.2 to 1.6 m wide, NOT vertical crystals, NOT a palisade, NOT spikes. "
        "5 Basalt TalusNest: nested angular shards in a grounded PILE, 0.6 to 1.4 m, not a cairn tower, not a picket fence. "
        "1.8 m gray mannequin at far left for scale. No animals, no dragons, no stick/spike/fence drift, no cards, no alpha slivers."
    ),
    "stonehold_ecosystem_composition_plots_v001": (
        COMMON
        + " SUBJECT: Embermist ECOSYSTEM COMPOSITION, four isolated 8 m-wide ground plots as true orthographic FRONT ELEVATION strips, generous gray, not overlapping. "
        "Plot 1 exposed ridge: sparse EmberCrown sedge plus basalt fracture fans, wind-combed, lots of bare rock. "
        "Plot 2 sheltered fault: denser Faultlace fern plus Ironbell bloom in a lee pocket. "
        "Plot 3 route verge: sedge along a packed-basalt road edge with a 2 m clear travel lane, no obstruction of the route. "
        "Plot 4 Worldscar exposure: stunted sedge plus talus nest, ash-scoured, not piled as a climb ramp. "
        "Every cluster grounded, volumetric clumps, condition variation, no identical copies, no stick/spike/fence drift, no animals, no dragons, no fortress apron dressing, no lava."
    ),
    "stonehold_interior_guard_forge_rooms_v001": (
        COMMON
        + " SUBJECT: developed FURNISHED INTERIOR KIT, guard and forge rooms of the Stonehold forge-guard program. "
        "TWO isolated TRUE TOP-DOWN FLOOR PLANS with generous gray between them, not a 3/4 cutaway, not a dollhouse, not stacked isometric floors, not a ripped-off roof. "
        "LEFT: GUARD / READY ROOM about 10 m by 12 m. Empty weapon racks, benches, duty desk, 1.2 m door swings, 1.5 m circulation aisle, stair bulkhead to walltop on the inner side. Exterior wall 1.2 m thick, interior partitions 0.3 m. Complete four walls, ceiling implied by wall thickness, no missing back wall. "
        "RIGHT: SMITHING FLOOR about 14 m by 16 m. Two anvils, quench trough, coal bunker as packed fuel NOT lava, iron hood as geometry not glow, 2 m work aisle, wagon door 2.4 m, stores along one wall. Traversable. Warm practical braziers as objects. "
        "Furniture unoccluded. Forbidden: dollhouse cutaway, fake facade shell, magma forge, dragons, people."
    ),
    "stonehold_interior_service_civic_rooms_v001": (
        COMMON
        + " SUBJECT: developed FURNISHED INTERIOR KIT, service and civic rooms of the Stonehold forge-guard / civic program. "
        "TWO isolated TRUE TOP-DOWN FLOOR PLANS with generous gray, not a 3/4 cutaway, not a dollhouse, not missing walls. "
        "LEFT: SERVICE / STORES about 10 m by 12 m. Supply racks 0.55 m deep, 1.25 m access run, stackable crates, covered cistern basin, service counter, rear door. Traversable aisles. "
        "RIGHT: CIVIC WORKROOM / CHAMBER about 12 m by 14 m. 2.8 m council table, six movable chairs, steward desk, record shelf bays, blank notice board, 1.20 m center aisle, 1.25 m clearance at side openings, 1.2 m door swings. "
        "Exterior wall 1.2 m, partitions 0.3 m. Complete enterable volumes. No fake shells, no 3/4, no modern office, no readable writing on boards, no dragons, no lava."
    ),
    "stonehold_civic_props_orthos_v001": (
        COMMON
        + " SUBJECT: Stonehold CIVIC PROP KIT, eight isolated furnishings, player scale, functional orthographic FRONT ELEVATIONS. "
        "Arrange as a 2-row by 4-column grid with GENEROUS empty gray between every object. No overlapping, no collage, no photobash, no shared ground plane merging them into a room. "
        "Objects, left-to-right then second row: council table 2.8 m long; steward desk 1.6 m; clerk counter 1.8 m; public bench 1.8 m; chair 0.55 m; record shelf bay 0.9 m wide by 2.0 m tall; secure chest 1.0 m; wall notice board 1.2 m with a BLANK surface and no text. "
        "One 1.8 m featureless gray mannequin standing in a corner for scale. "
        "Materials: dark heavy timber, basalt feet, soot-aged iron, quiet worn wool, restrained bronze repair accents. "
        "Forbidden: writing, heraldry, logos, modern office plastic, collage rooms, dragons."
    ),
    "stonehold_fort_props_orthos_v001": (
        COMMON
        + " SUBJECT: Stonehold FORT PROP KIT, eight isolated furnishings, player scale, functional orthographic FRONT ELEVATIONS. "
        "Arrange as a 2-row by 4-column grid with GENEROUS empty gray between every object. No overlapping, no collage, no room diorama. "
        "Objects: single barracks cot 2.1 m (ONE low cot, NOT a double bunk); staff locker 0.6 m by 1.8 m tall; empty armory rack 1.6 m with NO weapons; supply rack 1.2 m; utility cabinet 0.6 m; ONE supply crate 0.8 by 0.6 by 0.6 m; inspection counter 1.8 m; gate-control mount 2.4 m as a behaviorless shell with empty sockets and no machinery, no chains in motion, no gameplay. "
        "One 1.8 m featureless gray mannequin for scale. Embermist timber, basalt, soot-aged iron. "
        "Forbidden: double bunks, weapons on the rack, modern lockers, collage, dragons, lava, readable text."
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
