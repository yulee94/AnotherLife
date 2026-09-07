#!/usr/bin/env python3
"""Generate Grok Imagine 2D concept sheets for AccordantConceptGapP1 V001.

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

from PIL import Image

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
NATIVE = (1248, 832)

COMMON = (
    "Premium AA dark-high-fantasy stylized-realistic game-art production sheet. "
    "Geometry-readable orthographic architectural / construction drawing. Strict orthographic cameras, no perspective, no 3/4 hero shot, no isometric dollhouse cutaway, no axonometric island diorama. "
    "Neutral seamless gray studio, even studio lighting, no sky, no continent map, no landscape backdrop except the subject terrain that belongs to the sheet. "
    "Clean unoccluded views, generous empty gray space between panels, no overlapping objects, no collage, no moodboard, no photobash, no exploded 3/4 kit dump. "
    "No watermarks, no logos, no UI chrome, no modern objects, no vehicles, no firearms, no plastic, no neon, no glass curtain walls. "
    "No magic VFX, no particles, no lightning, no glow sprites, no baked volumetrics, no screen-filling petals, no theme-park neon pink. "
    "No animals, no real-world wildlife, no dragons, no bosses, no wish-dragon, no people except one optional 1.8 m featureless gray mannequin for scale. "
    "ACCORDANT ISLE ONLY. Event-only Petal Concord. Central cherry-blossom civic assembly. ABSENT from realmOrder. NOT a fifth realm, NOT a capital, NOT a fortress, NOT a keep, NOT a sequential dual-gate, NOT a permanent hub, NOT a travel shortcut. "
    "Materials: neutral weathered pale-warm stone, dark timber, restrained aged bronze, muted dusty-rose blossom-stone medallions, warm practical light. Cherry canopy is asymmetric, muted, grounded. "
    "Four equal realm-facing thresholds. Off-event every approach is fail-closed with BOTH (1) physically absent/retracted span AND (2) a closed grounded blossom-stone seal. On-event the authored span is present and open. "
    "No Stonehold Embermist fortress language as the primary read, no Crownlands palace, no Eldergrove root portal, no Umbral violet fog, no 180 m adjacent-bridge copy. "
    "No fake facade shells: every building shown is a real enterable volume with readable wall thickness and punched door/window apertures. "
    "Include a simple unlabeled metric scale bar with five equal tick marks and a faint 0.5 m floor grid where the subject is object-scale. "
    "PBR materials, Black-Desert-inspired finish bar, not cartoon, not anime, not painterly concept-illustration. "
    "Same identity, identical scale in every panel of this sheet."
)


def approach_prompt(realm: str, terminal_read: str, forbidden: str) -> str:
    return (
        COMMON
        + f" SUBJECT: ONE of four independent Accordant EVENT APPROACHES — {realm.upper()} REALM TERMINAL to PETAL CONCORD ISLE ANCHOR. "
        "Not a sequential outer/inner realm gate. Not a fortress gatehouse. "
        "TWO isolated panels left-to-right, generous gray between, identical scale, true orthographic, camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4. "
        "LEFT = OFF-EVENT / DORMANT FRONT ELEVATION. Far left abutment labeled by isolation as REALM TERMINAL: "
        f"{terminal_read} "
        "Far right abutment is ISLE ANCHOR: low Petal Concord blossom-stone socket, muted dusty-rose medallion, dark timber, aged bronze, not a keep. "
        "BETWEEN them is EMPTY AIR / VOID — the crossing SPAN is PHYSICALLY ABSENT, retracted into sockets, NO walkable deck, NO hanging cables as a fake bridge, NO planks. "
        "AT GROUND on the isle-anchor side sits a CLOSED GROUNDED BLOSSOM-STONE SEAL: a thick circular stone disk/door seated in a low blocking plinth, fully shut, obviously solid. "
        "BOTH denials must be independently visible: missing span AND closed grounded seal. Optional 1.8 m gray mannequin at the terminal. "
        "RIGHT = ON-EVENT FRONT ELEVATION of the SAME two abutments. An authored blossom-stone and dark-timber SPAN is PRESENT and OPEN, a real walkable deck connecting REALM TERMINAL to ISLE ANCHOR. "
        "The blossom-stone seal is OPEN: rotated or withdrawn into a ground socket, not floating, not VFX. Deck has readable thickness, rail posts, no combat crenellations. "
        "Tiny allowed text only if needed: REALM TERMINAL and ISLE ANCHOR. No other labels. "
        "FORBIDDEN: sequential dual-gate collage; outer barrier plus inner barrier; fortress keep; murder holes; battlements as primary; fifth capital skyline; permanent occupied city; open off-event deck; theme-park pink; dragons; animals. "
        f"{forbidden}"
    )


SHEETS: dict[str, str] = {
    "accordant_civic_ring_plan_v001": (
        COMMON
        + " SUBJECT: Accordant EVENT-ISLE CIVIC RING as a SINGLE architectural SITE PLAN. Not a fortress, not a keep, not a capital city, not a fifth realm. "
        "Camera STRAIGHT DOWN at 90 degrees like a CAD roof plan. Roofs and walls are FLAT 2D footprints only, like paper cutouts. "
        "NO facades, NO axonometric, NO isometric, NO 3/4, NO floating island diorama, NO cliff sides, NO battlement plan as a castle. "
        "A LOW circular / slightly irregular RING of civic assembly buildings around a central ROUND chamber roof. Four EQUAL realm-facing THRESHOLDS at the four cardinals, identical size, identical importance, no dominant throne keep. "
        "Each threshold is a short blossom-stone porch with a closed seal-disk shown as a filled circle on the ring, not a fortress gatehouse. "
        "No curtain-wall combat perimeter. No 30 m defensive apron. No flag-mast keep. No murder-hole towers. No sequential dual-gate. "
        "Asymmetric cherry-canopy footprints as muted pale-rose hatches OUTSIDE and BETWEEN civic masses, not a solid pink blob, not hiding circulation. "
        "Four approach sockets at the ring edge as empty dashed rectangles pointing outward — spans are NOT drawn on this civic plan. "
        "Gray studio paper background. Optional tiny labels: RING, FOUR EQUAL THRESHOLDS. "
        "FORBIDDEN: castle keep, cathedral, capital streets, fortress apron, Worldscar, sequential gates, dragons, fifth continent."
    ),
    "accordant_civic_ring_threshold_elevations_v001": (
        COMMON
        + " SUBJECT: FOUR EQUAL Accordant civic-ring THRESHOLD ELEVATIONS, proving parity. Not a fortress gate set. "
        "FOUR isolated TRUE FRONT ELEVATIONS in one row, identical height, identical scale, generous gray between, camera on the horizon, ZERO perspective, ZERO 3/4. "
        "Each panel is one realm-facing ceremonial threshold of the LOW civic ring: weathered pale-warm stone, dark timber lintel, restrained aged bronze, muted blossom-stone medallion. "
        "Opening about 4.8 m wide by 4.2 m tall. A grounded blossom-stone SEAL disk is CLOSED in the opening on every panel — civic ceremonial, not a portcullis, not iron fortress doors. "
        "All four sills sit on the SAME ground line / SAME threshold elevation. No panel is taller, more royal, or more fortified. "
        "Low ring wall behind each threshold, about two storeys, hip or shallow conical civic roof, NOT a keep tower, NOT crenellated battlements as the primary silhouette. "
        "Optional 1.8 m gray mannequin on the leftmost panel only. "
        "FORBIDDEN: four different castle gates; sequential dual-gate; one dominant royal portal; murder holes; portcullis; fifth-capital palace; dragons; unequal heights."
    ),
    "accordant_civic_ring_massing_elevations_v001": (
        COMMON
        + " SUBJECT: Accordant civic RING MASSING, two FLAT orthographic ELEVATIONS of the SAME low council ring under an asymmetric cherry canopy. "
        "Camera on the horizon, infinitely far, ZERO perspective, ZERO 3/4, ZERO isometric. "
        "LEFT: FRONT elevation. A WIDE LOW HORIZONTAL civic ring, about 80 m of architecture shown, two storeys, shallow roofs, four equal threshold notches readable as similar openings, NO central keep, NO needle spire, NO cathedral, NO fortress towers. "
        "Asymmetric cherry trees sit BESIDE and BEHIND the ring as grounded trunks with muted dusty-rose canopy volumes, not a pink explosion, not hiding doors. "
        "RIGHT: RIGHT SIDE elevation of the SAME low ring, proving a ring/assembly mass not a keep. Same materials. Generous gray between. "
        "Silhouette MUST be a wide short civic band, NOT a castle with a central tower. "
        "FORBIDDEN: keep, battlements as primary, 30 m apron army field, sequential dual-gate, fifth capital skyline, theme-park pink blizzard, dragons, animals."
    ),
    "accordant_approach_crownlands_v001": approach_prompt(
        "Crownlands",
        "Crownlands Meridian Oathroad abutment only: pale chalk-gold ashlar, deep-blue-slate edge, silver-rib hardware. This language STOPS at the terminal; it does not paint the isle.",
        "No Crownlands palace or cathedral beyond the terminal abutment. Isle remains Petal Concord.",
    ),
    "accordant_approach_stonehold_v001": approach_prompt(
        "Stonehold",
        "Stonehold Tempered Embermist abutment only: matte basalt-iron plates, heat-darkened iron, restrained iron-gold edge. This language STOPS at the terminal; it does not paint the isle.",
        "No lava, no magma, no Embermist fortress keep. Isle remains Petal Concord.",
    ),
    "accordant_approach_eldergrove_v001": approach_prompt(
        "Eldergrove",
        "Eldergrove Moonroot Vigil abutment only: pale mineral stone, dark timber, a few aged bronze collars. This language STOPS at the terminal; it does not paint the isle.",
        "No root portal, no wrapping climb-trees, no neon bioluminescence. Isle remains Petal Concord.",
    ),
    "accordant_approach_umbral_v001": approach_prompt(
        "Umbral",
        "Umbral Three-Fault Ashvein abutment only: graphite/ash stone, smoked-glass slit, dull ember restricted to a crack. This language STOPS at the terminal; it does not paint the isle.",
        "No violet fog, no portal language, no ashvein fortress. Isle remains Petal Concord.",
    ),
    "accordant_assembly_floor_plan_v001": (
        COMMON
        + " SUBJECT: Accordant PETAL CONCORD ASSEMBLY interior as a TRUE TOP-DOWN FLOOR PLAN. Furnished, traversable, complete circulation. Not a fortress keep. "
        "ONE isolated TRUE TOP-DOWN plan filling the sheet with generous gray margin, camera straight down at 90 degrees, ZERO 3/4, ZERO dollhouse cutaway, ZERO ripped-off roof. "
        "Central ROUND chamber about 18 m diameter. FOUR EQUAL delegation GALLERIES at the four cardinals, identical size about 10 m by 8 m each, no realm visually dominating. "
        "Also: two mediation rooms, one archive bay, stores, and a discreet security circulation ring in the thickness of the outer wall. "
        "Exterior wall about 1.0 m thick, interior partitions 0.3 m. Complete four-around walls. Door swings 1.2 m. Center aisle 1.5 m. "
        "Furniture: circular accord table, four gallery benches, archive shelves, store crates, mediation low tables. All unoccluded. "
        "Four threshold openings in the outer ring, equal. No throne dais, no keep stair-keep, no murder holes as primary. "
        "Muted blossom medallions as FLOOR inlay geometry, not particles. "
        "FORBIDDEN: combat fortress plan, barracks cots as primary, cannon, dungeon, fake shell, fifth-capital throne, dragons, people except optional 1.8 m mannequin footprint."
    ),
    "accordant_assembly_section_v001": (
        COMMON
        + " SUBJECT: Accordant assembly LONGITUDINAL SECTION through two opposite galleries and the central round chamber. "
        "ONE true FLAT SIDE CUT like a textbook architectural section. Camera IN the cut plane. Walls are hatched thickness rectangles. Furniture is flat cut rectangles. Floor is a single horizontal line. "
        "ZERO 3/4, ZERO isometric dollhouse, ZERO stage proscenium, ZERO looking into a room, ZERO perspective floor grid receding, ZERO pagoda keep. "
        "Left gallery, central round chamber with a shallow civic roof, right gallery — EQUAL HEIGHTS, same floor level. "
        "Readable wall thickness as dark hatched bands, punched doors as vertical gaps, gallery benches cut as rectangles, central table cut, 1.8 m featureless gray mannequin as a simple silhouette on the floor. "
        "Clerestory as architecture, lamps as geometry not glow. Muted blossom medallions as carved stone, not VFX. "
        "Roof is a low civic dome/hip, two storeys, not a keep. "
        "FORBIDDEN: cathedral nave, fortress murder holes, dungeon, fifth-capital throne, dragons, particles, dollhouse cutaway. "
        "REGEN OVERRIDE: previous image was a 3/4 dollhouse stage. Draw a FLAT 2D section like an architect's drawing. If you show the floor in perspective the sheet fails."
    ),
    "accordant_assembly_furniture_orthos_v001": (
        COMMON
        + " SUBJECT: Accordant assembly UNIQUE FOCAL FURNITURE orthos plus a small furnished gallery inset. "
        "LEFT quarter: ONE TRUE TOP-DOWN of a single delegation gallery about 10 m by 8 m, benches, 1.5 m aisle, 1.2 m door swing, complete walls, traversable. "
        "RIGHT three-quarters: isolated orthographic FRONT ELEVATIONS in a 2-row by 4-column grid with GENEROUS empty gray between every object. No overlapping, no collage, no shared ground merging them into a room. "
        "Objects: circular accord table 3.2 m; delegation bench 2.4 m; archive niche 0.9 m wide by 2.0 m tall; mediation low table 1.6 m; blossom-stone seal disk 1.8 m diameter as a detached grounded object; steward desk 1.6 m; store crate stack 1.2 m; blank notice board 1.2 m with NO text. "
        "One 1.8 m featureless gray mannequin for scale. Neutral weathered stone, dark timber, aged bronze, muted blossom inlay as MATERIAL. "
        "FORBIDDEN: writing, heraldry, logos, modern office plastic, collage rooms, dragons, neon, fake shells, weapons racks as primary."
    ),
    "accordant_ecosystem_family_orthos_v001": (
        COMMON
        + " SUBJECT: volumetric Petal Concord ECOSYSTEM FAMILY sheet: cherry canopy / ground / blossom-stone kit at PLAYER SCALE. No real animals, no dragon, no boss. "
        "FIVE isolated grounded clusters on one ground line, identical scale, generous gray between them, NOT overlapping, NOT a collage. True FRONT ELEVATIONS. "
        "1 Accord Cherry Standard: asymmetric muted dusty-rose canopy tree 4.5 to 6.5 m, several trunks or one leaning trunk, a VOLUME of petals as foliage mass not a pink sphere, grounded roots, not climbable onto architecture. "
        "2 Accord Cherry Sapling: 1.6 to 2.2 m leafy young tree, muted, not neon pink. "
        "3 Petalverge Sedge: low dusty-sage and pale-stone sedge CLUMP 0.4 to 0.9 m, a volume of blades, not a stick fence. "
        "4 Blossom-Stone Sett: a LOW HORIZONTAL scatter of weathered pale-warm cobbles with muted rose veining, ONE ROCK HIGH, maximum 0.45 m, pancake-wide, not crystals, not a cairn. "
        "5 Isle Packed Terrace: low packed pale silt and blossom-stone paving sample 0.2 m thick by 2.0 m wide, with a few fallen petal-shapes as MATERIAL flecks not VFX. "
        "1.8 m gray mannequin at far left for scale. No animals, no dragons, no theme-park pink blizzard, no stick/spike/fence drift, no real-world photographs. "
        "REGEN OVERRIDE: trees must be volumetric leafy canopies, not leafless sticks. Stones are dull and low, never crystals."
    ),
    "accordant_ecosystem_composition_plots_v001": (
        COMMON
        + " SUBJECT: Petal Concord ECOSYSTEM COMPOSITION, four isolated 8 m-wide ground plots as true orthographic FRONT ELEVATION strips, generous gray, not overlapping. "
        "Plot 1 civic-ring verge: sparse Accord Cherry Standard plus sedge, lots of bare weathered stone, traversal-readable gaps, doors not hidden. "
        "Plot 2 threshold terrace: blossom-stone paving plus low sedge, 2 m clear walking lane, closed seal disk sitting on the ground as architecture not a plant. "
        "Plot 3 canopy edge: denser saplings and one standard tree, asymmetric, muted dusty rose, not a pink wall. "
        "Plot 4 isle packed ground: terrace paving, fallen petal flecks as material, wind-scoured, not piled as a climb ramp. "
        "Every cluster grounded, volumetric, condition variation, no identical copies, no animals, no dragons, no fortress apron dressing, no screen-filling petals. "
        "REGEN OVERRIDE: TRUE FRONT ELEVATION strips, not 3/4 floating islands. Keep a 2 m clear lane on plot 2."
    ),
    "accordant_spoke_bridges_on_event_v001": (
        COMMON
        + " SUBJECT: FOUR Accordant SPOKE BRIDGES ON-EVENT, authored spans PRESENT and OPEN. Lengths MAY VARY. "
        "FOUR isolated TRUE SIDE ELEVATIONS in a 2x2 grid with generous gray, camera on the horizon, ZERO perspective, ZERO 3/4. "
        "CRITICAL: EVERY panel must show a CONTINUOUS walkable DECK connecting REALM TERMINAL on the left to ISLE ANCHOR on the right. ZERO gaps. ZERO mid-air broken decks. If any spoke does not touch both abutments the sheet fails. "
        "Each panel: LEFT abutment labeled REALM TERMINAL, RIGHT abutment ISLE ANCHOR. Blossom-stone/timber DECK with readable thickness, low civic rails, no combat crenellations. "
        "Four different span lengths: short, medium, long, longest — all still fully connected. Visual intent only, not a 180 m adjacent-bridge copy. "
        "Isle-anchor language is Petal Concord on every panel. Terminals are civic abutments, not fortress keeps. "
        "Open blossom-stone seal at the isle end, withdrawn into a ground socket. Optional 1.8 m featureless gray mannequin on one deck. "
        "FORBIDDEN: any disconnected span; off-event empty air; sequential dual-gate; fortress drawbridge chains; fifth capital; dragons; four identical copies. "
        "REGEN OVERRIDE: previous image left the short spoke disconnected in mid-air. All four decks MUST land on both abutments."
    ),
    "accordant_spoke_bridges_off_event_v001": (
        COMMON
        + " SUBJECT: FOUR Accordant SPOKE BRIDGES OFF-EVENT / DORMANT. Each spoke independently fail-closed. "
        "FOUR isolated TRUE SIDE ELEVATIONS with generous gray, camera on the horizon, ZERO perspective, ZERO 3/4. Same four variable lengths as the on-event family, but the SPAN IS PHYSICALLY ABSENT. "
        "Each panel: LEFT REALM TERMINAL abutment with an empty socket / retracted stump. RIGHT ISLE ANCHOR abutment with an empty socket. BETWEEN them EMPTY AIR — no deck, no hanging chains as a walkable path, no planks. "
        "AND on the isle-anchor ground: a CLOSED GROUNDED BLOSSOM-STONE SEAL disk in a blocking plinth, fully shut. BOTH denials visible on EVERY panel: missing span AND closed seal. "
        "Tiny allowed labels: REALM TERMINAL, ISLE ANCHOR, OFF-EVENT. "
        "FORBIDDEN: any walkable deck; open seals; sequential dual-gate; fortress portcullis as the only denial; fifth capital; dragons; theme-park pink."
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


def normalize_native(path: Path) -> int:
    with Image.open(path) as opened:
        image = opened.convert("RGB")
        w, h = image.size
        target_ratio = 3 / 2
        src_ratio = w / h if h else target_ratio
        if src_ratio > target_ratio:
            new_w = int(h * target_ratio)
            left = (w - new_w) // 2
            image = image.crop((left, 0, left + new_w, h))
        elif src_ratio < target_ratio:
            new_h = int(w / target_ratio)
            top = (h - new_h) // 2
            image = image.crop((0, top, w, top + new_h))
        if image.size != NATIVE:
            image = image.resize(NATIVE, Image.Resampling.LANCZOS)
        image.save(path, format="PNG", optimize=True)
    return path.stat().st_size


def write_image_payload(dest: Path, data0: dict) -> tuple[int, str | None]:
    url = data0.get("url")
    b64 = data0.get("b64_json")
    revised = data0.get("revised_prompt")
    if b64:
        blob = base64.b64decode(b64)
        dest.write_bytes(blob)
        return normalize_native(dest), revised
    if url:
        download(url, dest)
        return normalize_native(dest), revised
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
        dest.write_bytes(base64.b64decode(b64))
        result["bytes"] = normalize_native(dest)
        result["ok"] = result["bytes"] > 1000
        return result
    if url:
        try:
            download(url, dest)
            result["bytes"] = normalize_native(dest)
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
