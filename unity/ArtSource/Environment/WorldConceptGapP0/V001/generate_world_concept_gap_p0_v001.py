#!/usr/bin/env python3
"""Generate Grok Imagine 2D concept-gap sheets for WorldConceptGapP0 V001.

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
    "Geometry-readable orthographic turnaround. Strict orthographic cameras, no perspective, no 3/4 hero shot. "
    "Neutral seamless gray studio, even studio lighting, no environment scenery, no sky, no landscape backdrop. "
    "Clean unoccluded views, generous empty gray space between panels, no overlapping objects, no collage, no moodboard. "
    "No watermarks, no logos, no UI, no modern objects, no vehicles, no firearms, no plastic, no neon. "
    "No magic VFX, no particles, no lightning bolts, no glow sprites, no baked volumetrics. "
    "No animals, no real-world wildlife, no dragons, no bosses, no people except one optional 1.8 m featureless gray mannequin for scale. "
    "No Accordant Isle, no cherry blossoms, no fifth realm, no extra continents. "
    "Four realms only: Stonehold, Eldergrove, Crownlands, Umbral. "
    "Include a simple unlabeled metric scale bar with five equal tick marks (0.5 m intervals, 2 m total) and a faint 0.5 m floor grid. "
    "PBR materials, Black-Desert-inspired finish bar, not cartoon, not anime, not photobash."
)

SHEETS: dict[str, str] = {
    "shared_door_hardware_orthos_v001": (
        COMMON
        + " SUBJECT: shared civic door hardware family, crest-free, reusable across four realms. "
        "Three isolated panels left-to-right: FRONT ELEVATION of a single closed door leaf in its frame; "
        "RIGHT SIDE ELEVATION showing leaf thickness, frame depth, and strap-hinge barrels; "
        "BACK ELEVATION of the same leaf showing hinge straps, latch bar, and pivot. "
        "Same identity, identical scale in every panel. "
        "Door sized for a 1.2 m wide by 2.4 m tall interior opening on a 0.5 m construction grid; wall thickness 0.3 m. "
        "Neutral dark timber leaf, soot-aged iron strap hinges (three), iron latch bar, simple pintle pivots, plain stone/timber frame. "
        "No realm crests, no runes as language, no carved heraldry, no stained glass. "
        "Hardware must be geometry-readable: hinge knuckles, pintle pins, latch throw, frame rebate."
    ),
    "stonehold_ceremonial_gate_leaf_v001": (
        COMMON
        + " SUBJECT: Stonehold Tempered Embermist ceremonial sequential-gate LEAF PAIR only, not a fortress, not a wall complex. "
        "Three isolated panels left-to-right: FRONT ELEVATION of the closed paired leaves; RIGHT SIDE ELEVATION of one leaf thickness and hinge side; BACK ELEVATION of the inner face. "
        "Same identity, identical scale. One pair only — do not place outer and inner barriers side by side. "
        "Leaves fill an approximately 8 m wide by 6 m tall major-gate opening. "
        "Paired dark-iron layered leaves locked into faultline buttress edges, mechanical weight over ornament, matte basalt edge framing, heat-darkened iron plates, restrained iron-gold edgework, soot and ash wear. "
        "Heavy pintle hinges readable in side view. Compressed openings, tectonic horizontal banding. "
        "Forbidden: dwarven caricature, lava theme, climbable talus, dragons, runes as readable language."
    ),
    "eldergrove_ceremonial_gate_leaf_v001": (
        COMMON
        + " SUBJECT: Eldergrove Moonroot Vigil ceremonial sequential-gate LEAF PAIR only, not a fortress, not a forest scene. "
        "Three isolated panels left-to-right: FRONT ELEVATION of the closed paired leaves; RIGHT SIDE ELEVATION of one leaf thickness and hinge side; BACK ELEVATION of the inner face. "
        "Same identity, identical scale. One pair only — do not place outer and inner barriers side by side. "
        "Leaves fill an approximately 8 m wide by 6 m tall major-gate opening. "
        "Crafted pale mineral-stone and dark timber leaves framed by bounded living-root ribs seated in aged bronze collars. "
        "Desaturated root bark, restrained moon-silver practical metal, no neon bioluminescence. "
        "Heavy pintle hinges readable in side view. Broad low curves, asymmetric root-held mass. "
        "Forbidden: root portal, glowing mushrooms, dense canopy, animals, dragons, readable language."
    ),
    "crownlands_ceremonial_gate_leaf_v001": (
        COMMON
        + " SUBJECT: Crownlands Meridian Oathroad ceremonial sequential-gate LEAF PAIR only, not a cathedral, not a palace. "
        "Three isolated panels left-to-right: FRONT ELEVATION of the closed paired leaves; RIGHT SIDE ELEVATION of one leaf thickness and hinge side; BACK ELEVATION of the inner face. "
        "Same identity, identical scale. One pair only — do not place outer and inner barriers side by side. "
        "Leaves fill an approximately 8 m wide by 6 m tall major-gate opening. "
        "Pale limestone leaves with cool silver ribs, weathered blue-slate banding, restrained bronze/gold edge only. "
        "Segmented meridian geometry, axial panel rhythm, storm-pressed wear. Heavy pintle hinges readable in side view. "
        "Forbidden: generic gothic cathedral, excessive gold, fragile needle spires, ungrounded white-marble palace, dragons, readable language."
    ),
    "umbral_ceremonial_gate_leaf_v001": (
        COMMON
        + " SUBJECT: Umbral Three-Fault Ashvein ceremonial sequential-gate LEAF PAIR only, not a fortress, not a portal. "
        "Three isolated panels left-to-right: FRONT ELEVATION of the closed paired leaves; RIGHT SIDE ELEVATION of one leaf thickness and hinge side; BACK ELEVATION of the inner face. "
        "Same identity, identical scale. One pair only — do not place outer and inner barriers side by side. "
        "Leaves fill an approximately 8 m wide by 6 m tall major-gate opening. "
        "Graphite sequential-barrier leaves with grounded ash-timber yokes, smoked-glass slits, dull ember only in hairline cracks, never as glow sprites. "
        "Broad obsidian ribs, three converging fault directions as surface relief only. Heavy pintle hinges readable in side view. "
        "Forbidden: portal language, violet glow dependence, black fog, uniformly crushed values, dragons, readable language."
    ),
    "shared_save_pillar_hero_form_v001": (
        COMMON
        + " SUBJECT: shared warzone SAVE PILLAR hero form, crest-free silhouette used by all four realms. "
        "Three isolated panels left-to-right: FRONT, RIGHT SIDE, BACK orthographic elevations of the same pillar. "
        "Optional fourth small top-down plan in the lower-right corner, same object, not a collage of different designs. "
        "Height approximately 4.5 m; flared blocking base about 1.8 m across; interaction plinth ring at 1.1 m; tapered shaft; capped head with an empty circular VFX socket (geometry only, no glow). "
        "Neutral dark stone and iron, no realm crests, no runes as language, no gravestone, no Egyptian obelisk, no waystone split-basalt menhir. "
        "This is a unique game landmark: standing bind-pillar monument, not a lamp post, not a totem pole, not a crucifix. "
        "1.8 m featureless gray mannequin standing beside the FRONT panel for scale."
    ),
    "stonehold_save_pillar_ornament_v001": (
        COMMON
        + " SUBJECT: Stonehold Tempered Embermist SAVE PILLAR — MUST KEEP the shared hero silhouette exactly: "
        "a single tapered octagonal stone shaft about 4.5 m tall, flared octagonal blocking base, TOP-FACING open circular cap socket like a chimney opening looking straight up, no side-facing holes, no shelves, no brackets, no cannon muzzle. "
        "Three isolated panels left-to-right: FRONT, RIGHT SIDE, BACK. Identical scale. Optional 1.8 m gray mannequin beside FRONT. "
        "Change materials and surface ornament only: matte basalt, heat-darkened iron banding, restrained iron-gold edgework, soot and ash wear, shallow tectonic grooves flush to the shaft. "
        "Empty circular VFX socket on the TOP cap, no glow. 1.1 m interaction ring around the base. "
        "Do not add shelves, fins that change silhouette, or rotate the socket to the side. No dwarven caricature, no lava, no animals, no dragons."
    ),
    "eldergrove_save_pillar_ornament_v001": (
        COMMON
        + " SUBJECT: Eldergrove Moonroot Vigil SAVE PILLAR — same shared 4.5 m hero silhouette as the crest-free bind-pillar, unique ornament and materials only. "
        "Three isolated panels left-to-right: FRONT, RIGHT SIDE, BACK. Identical scale. "
        "Pale mineral stone shaft, dark timber collars, aged bronze root-rib overlay seated in bronze bands, desaturated bark, restrained moon-silver metal. "
        "Empty circular VFX socket at the cap, no glow and no bioluminescence. Flared blocking base, 1.1 m interaction ring. "
        "Do not change the overall silhouette. No neon plants, no animals, no dragons, no portal."
    ),
    "crownlands_save_pillar_ornament_v001": (
        COMMON
        + " SUBJECT: Crownlands Meridian Oathroad SAVE PILLAR — MUST KEEP the shared hero silhouette exactly: "
        "a single tapered octagonal stone shaft about 4.5 m tall, flared octagonal blocking base, TOP-FACING open circular cap socket like a chimney opening looking straight up, no side-facing holes, no shelves. "
        "Three isolated panels left-to-right: FRONT, RIGHT SIDE, BACK. Identical scale. Optional 1.8 m gray mannequin beside FRONT. "
        "Change materials and surface ornament only: pale limestone shaft, cool silver meridian ribs flush to the faces, weathered blue-slate cap band, restrained bronze edge. "
        "Empty circular VFX socket on the TOP cap, no glow. 1.1 m interaction ring around the base. "
        "Do not rotate the socket to the side. No cathedral spire, no excessive gold, no animals, no dragons."
    ),
    "umbral_save_pillar_ornament_v001": (
        COMMON
        + " SUBJECT: Umbral Three-Fault Ashvein SAVE PILLAR — same shared 4.5 m hero silhouette as the crest-free bind-pillar, unique ornament and materials only. "
        "Three isolated panels left-to-right: FRONT, RIGHT SIDE, BACK. Identical scale. "
        "Graphite/obsidian shaft, ash-timber yoke bands, smoked-glass inlay slits, dull ember only as hairline material cracks, never glow sprites. "
        "Empty circular VFX socket at the cap, no portal. Flared blocking base, 1.1 m interaction ring. "
        "Do not change the overall silhouette. No violet fog, no animals, no dragons."
    ),
    "shared_adjacent_realm_bridge_kit_v001": (
        COMMON
        + " SUBJECT: shared 180 m adjacent-realm BRIDGE visual construction kit, crest-free deck family. "
        "Three isolated technical panels of the SAME bridge, not four different designs: "
        "LEFT: PLAN view of the full 180 m span as a long thin deck over a dark Worldscar void, with 6 m walkable deck width and 4 m modular bay ticks; "
        "CENTER: SIDE ELEVATION of the full 180 m span, rails 1.1 m high, player-scale 1.8 m mannequin on the deck, Worldscar as empty dark gulf under the span (no lightning); "
        "RIGHT: CROSS SECTION through the deck showing 6 m walkable width, 1.1 m rails, deck thickness, and impassable rail colliders as solid rail geometry. "
        "Neutral weathered stone deck, dark timber/iron rails, no realm crests. "
        "Eight identical adjacent-realm bridges exist in the world; this sheet is the shared kit only. "
        "Do not show Accordant spokes, cherry blossoms, islands, fortresses, or a fifth realm. "
        "Do not invent ramps off the deck, swim paths, or flying creatures."
    ),
    "stonehold_bridge_abutment_v001": (
        COMMON
        + " SUBJECT: Stonehold Tempered Embermist abutment skin on the SHARED 180 m adjacent-realm bridge. "
        "Two or three isolated panels of ONE abutment: FRONT facing the Worldscar, SIDE showing the deck stub leaving the abutment, and a closer MATERIAL/ORNAMENT callout of the same abutment. "
        "Shared crest-free 6 m stone deck and 1.1 m rails continue unchanged; only the landfall tower/pier uses Stonehold materials: battered basalt fins, heat-darkened iron plates, restrained iron-gold edgework, soot. "
        "Abutment sits on solid ground at the Worldscar brink; the span leaves toward empty dark gulf. "
        "No fortress, no gate complex, no lava, no animals, no dragons, no fifth realm."
    ),
    "eldergrove_bridge_abutment_v001": (
        COMMON
        + " SUBJECT: Eldergrove Moonroot Vigil abutment skin on the SHARED 180 m adjacent-realm bridge. "
        "Two or three isolated panels of ONE abutment: FRONT facing the Worldscar, SIDE showing the deck stub leaving the abutment, and a closer MATERIAL/ORNAMENT callout of the same abutment. "
        "Shared crest-free 6 m stone deck and 1.1 m rails continue unchanged; only the landfall tower/pier uses Eldergrove materials: pale mineral stone, dark timber, aged bronze root collars, desaturated bark. "
        "Roots remain structural overlay on the abutment, never a climbable exterior ladder and never a portal. "
        "Abutment sits on solid ground at the Worldscar brink; the span leaves toward empty dark gulf. "
        "No animals, no dragons, no neon, no fifth realm."
    ),
    "crownlands_bridge_abutment_v001": (
        COMMON
        + " SUBJECT: Crownlands Meridian Oathroad abutment skin on the SHARED 180 m adjacent-realm bridge. "
        "Two or three isolated panels of ONE abutment: FRONT facing the Worldscar, SIDE showing the deck stub leaving the abutment, and a closer MATERIAL/ORNAMENT callout of the same abutment. "
        "Shared crest-free 6 m stone deck and 1.1 m rails continue unchanged; only the landfall tower/pier uses Crownlands materials: pale limestone, cool silver ribs, weathered blue slate, restrained bronze. "
        "Abutment sits on solid ground at the Worldscar brink; the span leaves toward empty dark gulf. "
        "No cathedral, no excessive gold, no animals, no dragons, no fifth realm."
    ),
    "umbral_bridge_abutment_v001": (
        COMMON
        + " SUBJECT: Umbral Three-Fault Ashvein abutment skin on the SHARED 180 m adjacent-realm bridge. "
        "Two or three isolated panels of ONE abutment: FRONT facing the Worldscar, SIDE showing the deck stub leaving the abutment, and a closer MATERIAL/ORNAMENT callout of the same abutment. "
        "Shared crest-free 6 m stone deck and 1.1 m rails continue unchanged; only the landfall tower/pier uses Umbral materials: graphite/obsidian ribs, ash-timber yokes, smoked-glass slits, dull ember hairline cracks only. "
        "Abutment sits on solid ground at the Worldscar brink; the span leaves toward empty dark gulf. "
        "No portal, no violet fog, no animals, no dragons, no fifth realm."
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
        body = ext.read() if False else exc.read().decode("utf-8", errors="replace")
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
            futs = {
                pool.submit(generate_one, token, name, SHEETS[name]): name for name in names
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
                    flush=True,
                )
                if row.get("error"):
                    print("ERROR_SNIP", str(row["error"])[:400], flush=True)
    results.sort(key=lambda r: names.index(r["name"]) if r["name"] in names else 99)
    run_log = {
        "provider": "xai-oauth",
        "chat_model": CHAT_MODEL,
        "image_model": MODEL,
        "quality": QUALITY,
        "aspect_ratio": ASPECT,
        "fallback_policy": "GPT-5.6 Sol only if Grok returns no answer",
        "fallback_used": False,
        "results": [{k: v for k, v in row.items() if k != "error"} for row in results],
        "errors": {row["name"]: row.get("error") for row in results if row.get("error")},
    }
    (OUT_DIR / "generation_run.json").write_text(
        json.dumps(run_log, indent=2) + "\n", encoding="utf-8"
    )
    failed = [row["name"] for row in results if not row["ok"]]
    print("FAILED", ",".join(failed) if failed else "none")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
