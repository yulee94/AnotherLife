#!/usr/bin/env python3
"""Generate Stonehold natural-world 2D family sheets via Grok Imagine.

Never prints credentials. Resumable: existing PNGs over 1000 bytes are skipped.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import io
import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

sys.dont_write_bytecode = True

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:  # pragma: no cover
    Image = None  # type: ignore
    ImageDraw = None  # type: ignore
    ImageFont = None  # type: ignore

ROOT = Path(__file__).resolve().parent
FAMILIES_PATH = ROOT / "families_v001.json"
SHEETS_DIR = ROOT / "Sheets"
REVIEW_DIR = ROOT / "Review"
MANIFEST_PATH = ROOT / "family_sheet_manifest_v001.json"
GENERATION_LOG = ROOT / "generation_log_v001.json"
AUTH_PATH = Path(os.environ.get("LOCALAPPDATA", "")) / "hermes" / "auth.json"
API = "https://api.x.ai/v1"
MODEL = "grok-imagine-image-2.0"
CHAT_MODEL = "grok-4.6"
TASK_ID = "t_2227272d"
STYLE = (
    "Premium AA dark-fantasy stylized-realistic game-art production family sheet, "
    "16:9 landscape contact board. Stonehold realm natural-world kit. Grounded PBR "
    "readability: basalt, dark iron, aged steel, soot, iron soil, mineral inclusions. "
    "Palette: charcoal, iron brown, ash, small restrained forge-amber mineral accents. "
    "Even studio lighting plus one small in-situ Stonehold hillside or quarry thumbnail. "
    "Show required variants as separate product-board tiles of the SAME family, identical "
    "identity, scale, lighting, and material language. Optional tiny 1.8 meter human "
    "silhouette ghost for scale only. "
    "Avoid: cartoon plants, generic AI jungle, Eldergrove lush roots, dwarf pastiche, "
    "orange on every edge, identical blocky silhouettes, copied Black Desert or Infinity "
    "Kingdom, animals, people except the scale ghost, logos, watermarks, UI, readable "
    "fake text, baked lightning, smoke, particles, auras, or attack VFX. "
    "Magic only as named mineral/heat accent in the material, never as a particle spray. "
    "Fantasy mineral world, never Earth wildlife."
)


def load_token() -> str:
    auth = json.loads(AUTH_PATH.read_text(encoding="utf-8"))
    token = auth["providers"]["xai-oauth"]["tokens"]["access_token"]
    if not token:
        raise SystemExit("missing xai-oauth access_token")
    return token


def sha256_bytes(blob: bytes) -> str:
    return hashlib.sha256(blob).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


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
        with urllib.request.urlopen(req, timeout=180) as resp:
            body = resp.read().decode("utf-8", errors="replace")
            return resp.status, json.loads(body)
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        try:
            parsed = json.loads(body)
        except json.JSONDecodeError:
            parsed = body[:800]
        return exc.code, parsed


def prompt_for(family: dict) -> str:
    slag = ""
    if family.get("slagfallFamily"):
        slag = (
            f" Visual continuity with approved Slagfall Quarry family "
            f"{family['slagfallFamily']} as profiling-scale identity only; "
            "this sheet is the realm-wide 2D family look, not a 3D lock. "
        )
    return (
        f"{STYLE} Family ID {family['familyId']} ({family['displayLabel']}). "
        f"Purpose: {family['purpose']}. Required variants as tiles: "
        f"{family['requiredVariants']}. Look: {family['look']}.{slag}"
        " No invented meter numbers, navigation widths, or gameplay yields."
    )


def generate_one(token: str, family: dict, dest: Path) -> dict:
    result = {
        "familyId": family["familyId"],
        "http": 0,
        "path": dest.as_posix(),
        "bytes": 0,
        "ok": False,
        "skipped": False,
        "provider": "Grok",
        "model": "4.6 High",
        "imageModel": MODEL,
        "fallback": None,
    }
    if dest.is_file() and dest.stat().st_size > 1000:
        result["bytes"] = dest.stat().st_size
        result["ok"] = True
        result["skipped"] = True
        result["http"] = 200
        return result
    payload = {
        "model": MODEL,
        "prompt": prompt_for(family),
        "n": 1,
        "aspect_ratio": "16:9",
        "quality": "medium",
        "response_format": "b64_json",
    }
    status, body = post_json(API + "/images/generations", token, payload)
    result["http"] = status
    if status != 200:
        result["error"] = body if isinstance(body, str) else json.dumps(body)[:800]
        return result
    data = body.get("data") if isinstance(body, dict) else None
    if not data:
        result["error"] = "no data"
        return result
    b64 = data[0].get("b64_json")
    if not b64:
        result["error"] = "no b64_json"
        return result
    blob = base64.b64decode(b64)
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(blob)
    result["bytes"] = len(blob)
    result["ok"] = len(blob) > 1000
    if not result["ok"]:
        result["error"] = "decoded image too small"
    return result


def png_info(path: Path) -> dict:
    info = {"width": 0, "height": 0, "mode": "unknown"}
    if Image is None:
        return info
    with Image.open(path) as im:
        info["width"], info["height"] = im.size
        info["mode"] = im.mode
    return info


def build_contact_pages(families: list[dict], sheet_paths: dict[str, Path]) -> list[Path]:
    if Image is None:
        return []
    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    categories = [
        ("terrain_geology", "Terrain / geology"),
        ("slagfall", "Slagfall district"),
        ("vegetation", "Vegetation"),
        ("water_shore", "Water / shore"),
        ("ore_crystal", "Ore / crystal"),
        ("vfx_weather", "VFX / weather dressing"),
        ("dressing", "Dressing"),
    ]
    try:
        font = ImageFont.truetype("arial.ttf", 22)
        small = ImageFont.truetype("arial.ttf", 16)
    except OSError:
        font = ImageFont.load_default()
        small = font
    written: list[Path] = []
    thumb_w, thumb_h = 640, 360
    cols = 3
    pad = 16
    header = 72
    for cat_id, cat_label in categories:
        members = [f for f in families if f["category"] == cat_id]
        if not members:
            continue
        rows = (len(members) + cols - 1) // cols
        width = cols * thumb_w + (cols + 1) * pad
        height = header + rows * (thumb_h + 48) + pad
        canvas = Image.new("RGB", (width, height), (18, 18, 20))
        draw = ImageDraw.Draw(canvas)
        draw.text((pad, 18), f"Stonehold natural-world family sheets — {cat_label}", fill=(230, 224, 214), font=font)
        draw.text((pad, 44), "Grok 4.6 High / grok-imagine-image-2.0 — 2D concept only, PENDING owner visual approval", fill=(160, 150, 140), font=small)
        for idx, family in enumerate(members):
            r, c = divmod(idx, cols)
            x = pad + c * (thumb_w + pad)
            y = header + r * (thumb_h + 48)
            src = sheet_paths.get(family["familyId"])
            if src and src.is_file():
                with Image.open(src) as im:
                    thumb = im.convert("RGB").resize((thumb_w, thumb_h))
                    canvas.paste(thumb, (x, y))
            else:
                draw.rectangle((x, y, x + thumb_w, y + thumb_h), fill=(40, 36, 34))
            draw.text((x, y + thumb_h + 8), family["familyId"], fill=(210, 200, 188), font=small)
        dest = REVIEW_DIR / f"contact_{cat_id}_v001.png"
        canvas.save(dest, format="PNG")
        written.append(dest)
    return written


def utf8_lf(path: Path) -> None:
    if path.suffix.lower() in {".json", ".md", ".py"} and path.is_file():
        text = path.read_text(encoding="utf-8")
        path.write_bytes(text.replace("\r\n", "\n").replace("\r", "\n").encode("utf-8"))


def build_manifest(families: list[dict], results: list[dict], contacts: list[Path]) -> dict:
    artifacts = []
    text_files = [
        "families_v001.json",
        "generate_sheets.py",
        "validate_packet.py",
        "test_validate_packet.py",
        "README.md",
    ]
    for rel in text_files:
        path = ROOT / rel
        if not path.is_file():
            continue
        blob = path.read_bytes()
        artifacts.append(
            {
                "path": rel,
                "kind": "text",
                "bytes": len(blob),
                "sha256": sha256_bytes(blob),
            }
        )
    sheet_records = []
    for family, row in zip(families, results):
        rel = f"Sheets/{family['familyId']}_family_sheet_v001.png"
        path = ROOT / rel
        rec = {
            "familyId": family["familyId"],
            "displayLabel": family["displayLabel"],
            "packetId": family["packetId"],
            "category": family["category"],
            "purpose": family["purpose"],
            "requiredVariants": family["requiredVariants"],
            "path": rel,
            "provider": "Grok",
            "model": "4.6 High",
            "tool": "grok-imagine-image-2.0",
            "quality": "medium",
            "aspectRatio": "16:9",
            "taskId": TASK_ID,
            "fallback": row.get("fallback"),
            "prompt": prompt_for(family),
            "ok": bool(row.get("ok")),
        }
        if family.get("slagfallFamily"):
            rec["priorSlagfallEvidence"] = {
                "slagfallFamily": family["slagfallFamily"],
                "source2dSha256": family.get("slagfallSourceSha256"),
                "scope": "profiling_scale_identity_only_not_replaced",
            }
        if path.is_file():
            rec["bytes"] = path.stat().st_size
            rec["sha256"] = sha256_file(path)
            rec.update(png_info(path))
            artifacts.append(
                {
                    "path": rel,
                    "kind": "png",
                    "bytes": rec["bytes"],
                    "sha256": rec["sha256"],
                    "width": rec.get("width"),
                    "height": rec.get("height"),
                    "mode": rec.get("mode"),
                    "familyId": family["familyId"],
                }
            )
        sheet_records.append(rec)
    contact_records = []
    for path in contacts:
        rel = path.relative_to(ROOT).as_posix()
        rec = {
            "path": rel,
            "kind": "png",
            "bytes": path.stat().st_size,
            "sha256": sha256_file(path),
        }
        rec.update(png_info(path))
        contact_records.append(rec)
        artifacts.append(
            {
                "path": rel,
                "kind": "png",
                "bytes": rec["bytes"],
                "sha256": rec["sha256"],
                "width": rec.get("width"),
                "height": rec.get("height"),
                "mode": rec.get("mode"),
            }
        )
    declared = {
        "familyCount": len(families),
        "sheetsOk": sum(1 for r in sheet_records if r.get("ok")),
        "contactPages": len(contact_records),
        "meshyAuthorized": 0,
        "blenderAuthorized": False,
        "geometryNavLocked": False,
    }
    manifest = {
        "schema": "anotherlife.stonehold-natural-world-family-sheets.manifest.v1",
        "packetId": "stonehold_natural_world_family_sheets_v001",
        "taskId": TASK_ID,
        "createdDate": "2026-09-05",
        "provider": "Grok",
        "model": "4.6 High",
        "imageModel": MODEL,
        "chatModel": CHAT_MODEL,
        "fallbackPolicy": "GPT-5.6 Sol only if Grok 4.6 does not answer; record fallback per sheet",
        "scope": "2D Stonehold natural-world family sheets only",
        "sourceAuthority": [
            "DESIGN.md",
            "unity/Docs/AssetLibrary/PostMVP_World_Asset_Taxonomy_v1.md",
            "unity/Docs/AssetLibrary/StoneholdConceptPacketsV001/README.md",
            "unity/Docs/AssetLibrary/StoneholdConceptPacketsV001/environment_stonehold_natural_ecology_v001.md",
            "unity/Docs/AssetLibrary/StoneholdConceptPacketsV001/environment_stonehold_geology_minerals_crystals_v001.md",
            "unity/Docs/AssetLibrary/StoneholdConceptPacketsV001/stonehold_concept_packet_coverage_v001.json",
            "unity/Docs/AI/Meshy/meshy_execution_slagfall_environment_2026-08-31_v001.json",
        ],
        "approval": {
            "authority": "project_owner visual ruling required",
            "decision": "PENDING",
            "independentReviewId": "pending",
            "independentReviewVerdict": "PENDING",
            "summary": "Independent visual/source review pending.",
            "runtimeAuthority": False,
            "releaseAuthority": False,
            "meshyAuthorized": False,
        },
        "readinessBoundary": {
            "state": "pending_owner_visual_approval",
            "permits": ["owner visual review of 2D family sheets"],
            "forbids": [
                "Meshy",
                "Blender",
                "3D production",
                "final balance",
                "geometry or navigation lock",
                "runtime activation",
                "release approval",
            ],
        },
        "declaredTotals": declared,
        "families": sheet_records,
        "contactSheets": contact_records,
        "artifacts": artifacts,
    }
    return manifest


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", default="", help="comma family IDs")
    parser.add_argument("--manifest-only", action="store_true")
    parser.add_argument("--max", type=int, default=0)
    args = parser.parse_args()
    payload = json.loads(FAMILIES_PATH.read_text(encoding="utf-8"))
    families = payload["families"]
    if len(families) != payload["expectedFamilyCount"]:
        raise SystemExit(f"family count {len(families)} != {payload['expectedFamilyCount']}")
    wanted = [n.strip() for n in args.only.split(",") if n.strip()]
    if wanted:
        families = [f for f in families if f["familyId"] in wanted]
    if args.max:
        families = families[: args.max]
    SHEETS_DIR.mkdir(parents=True, exist_ok=True)
    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    results: list[dict] = []
    if not args.manifest_only:
        token = load_token()
        for family in families:
            dest = SHEETS_DIR / f"{family['familyId']}_family_sheet_v001.png"
            print("GENERATE", family["familyId"], flush=True)
            row = generate_one(token, family, dest)
            results.append(row)
            print(
                "RESULT",
                family["familyId"],
                "skip" if row["skipped"] else ("ok" if row["ok"] else "FAIL"),
                "http",
                row["http"],
                "bytes",
                row["bytes"],
                flush=True,
            )
            if row.get("error"):
                print("ERROR_SNIP", str(row["error"])[:400], flush=True)
            if not row["skipped"]:
                time.sleep(0.4)
        GENERATION_LOG.write_text(
            json.dumps(
                {
                    "provider": "Grok",
                    "model": "4.6 High",
                    "imageModel": MODEL,
                    "results": [
                        {k: v for k, v in row.items() if k != "error"} for row in results
                    ],
                    "errors": [
                        {"familyId": row["familyId"], "error": row.get("error")}
                        for row in results
                        if row.get("error")
                    ],
                },
                indent=2,
            )
            + "\n",
            encoding="utf-8",
        )
        utf8_lf(GENERATION_LOG)
    else:
        all_families = json.loads(FAMILIES_PATH.read_text(encoding="utf-8"))["families"]
        families = all_families
        for family in families:
            dest = SHEETS_DIR / f"{family['familyId']}_family_sheet_v001.png"
            results.append(
                {
                    "familyId": family["familyId"],
                    "ok": dest.is_file() and dest.stat().st_size > 1000,
                    "fallback": None,
                    "bytes": dest.stat().st_size if dest.is_file() else 0,
                }
            )
    all_families = json.loads(FAMILIES_PATH.read_text(encoding="utf-8"))["families"]
    sheet_paths = {
        f["familyId"]: SHEETS_DIR / f"{f['familyId']}_family_sheet_v001.png" for f in all_families
    }
    contacts = build_contact_pages(all_families, sheet_paths)
    # Rebuild results aligned to all families for the manifest.
    aligned = []
    by_id = {row["familyId"]: row for row in results}
    for family in all_families:
        dest = sheet_paths[family["familyId"]]
        row = by_id.get(family["familyId"]) or {
            "familyId": family["familyId"],
            "ok": dest.is_file() and dest.stat().st_size > 1000,
            "fallback": None,
        }
        aligned.append(row)
    manifest = build_manifest(all_families, aligned, contacts)
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    utf8_lf(MANIFEST_PATH)
    failed = [row["familyId"] for row in aligned if not row.get("ok")]
    print("CONTACT", ",".join(p.name for p in contacts), flush=True)
    print("FAILED", ",".join(failed) if failed else "none", flush=True)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
