#!/usr/bin/env python3
"""Fail-closed validation of CrownlandsConceptP1 V001 dimensions, hashes, links, and counts."""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent
DOCS = ROOT.parents[3] / "Docs" / "Environment" / "World"
NATIVE = (1248, 832)
AUTHORING = (7680, 5120)
FAMILIES = [
    "fam_crownlands_capital",
    "fam_crownlands_city_kit",
    "fam_crownlands_inner_cave_dungeon",
    "fam_crownlands_outer_cave_dungeon",
]
SHEETS = [
    ("fam_crownlands_capital", "crownlands_capital_district_plan_v001"),
    ("fam_crownlands_capital", "crownlands_capital_skyline_north_south_v001"),
    ("fam_crownlands_capital", "crownlands_capital_skyline_east_west_v001"),
    ("fam_crownlands_capital", "crownlands_capital_keep_shell_v001"),
    ("fam_crownlands_capital", "crownlands_capital_keep_section_v001"),
    ("fam_crownlands_capital", "crownlands_capital_keep_ground_plan_v001"),
    ("fam_crownlands_capital", "crownlands_capital_keep_upper_circulation_v001"),
    ("fam_crownlands_city_kit", "crownlands_city_street_grammar_v001"),
    ("fam_crownlands_city_kit", "crownlands_city_dwelling_shell_v001"),
    ("fam_crownlands_city_kit", "crownlands_city_dwelling_interior_v001"),
    ("fam_crownlands_city_kit", "crownlands_city_market_service_public_hall_kit_v001"),
    ("fam_crownlands_inner_cave_dungeon", "crownlands_inner_cave_mouth_v001"),
    ("fam_crownlands_inner_cave_dungeon", "crownlands_inner_cave_section_circulation_v001"),
    ("fam_crownlands_outer_cave_dungeon", "crownlands_outer_cave_mouth_v001"),
    ("fam_crownlands_outer_cave_dungeon", "crownlands_outer_cave_loop_choke_section_v001"),
    ("fam_crownlands_inner_cave_dungeon", "crownlands_cave_chamber_fitting_module_v001"),
]
ALLOWED_CAPITAL = "capital_crownspire"
INVENTED_CITY_ID_RE = re.compile(
    r"\bcity_(?!kit\b|ids\b|street_|dwelling_|house_|shop_|service_|workshop_|market_)[a-z0-9_]+\b",
    re.I,
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def fail(errors: list[str], msg: str) -> None:
    errors.append(msg)


def main() -> int:
    errors: list[str] = []
    manifest_path = ROOT / "crownlands_concept_p1_manifest_v001.json"
    qa_path = ROOT / "visual_qa_v001.json"
    if not manifest_path.is_file():
        print("MISSING manifest")
        return 1
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    qa = json.loads(qa_path.read_text(encoding="utf-8"))

    if manifest.get("asset_count") != 16:
        fail(errors, f"asset_count {manifest.get('asset_count')} != 16")
    if manifest.get("families") != FAMILIES:
        fail(errors, f"families mismatch {manifest.get('families')}")
    if manifest.get("identity", {}).get("capitalId") != "capital_crownspire":
        fail(errors, "capitalId drifted from capital_crownspire")
    if manifest.get("identity", {}).get("cityIds"):
        fail(errors, f"invented city IDs {manifest['identity']['cityIds']}")
    if manifest.get("dimensional_control_not_ai_numerals", {}).get("city_ids_invented") is not False:
        fail(errors, "city_ids_invented must be false")
    if "2.5" not in str(manifest.get("dimensional_control_not_ai_numerals", {}).get("public_hall_opening_m")) and manifest.get(
        "dimensional_control_not_ai_numerals", {}
    ).get("public_hall_opening_m") != [2.5, 3.0]:
        fail(errors, "public_hall_opening_m must be [2.5, 3.0]")

    run_path = ROOT / "generation_run.json"
    if run_path.is_file():
        run = json.loads(run_path.read_text(encoding="utf-8"))
        if len(run.get("results") or []) != 16:
            fail(errors, f"generation_run results {len(run.get('results') or [])}")
        if any(not row.get("ok") for row in run.get("results") or []):
            fail(errors, "generation_run contains failed sheets")
        run_fallback = any(row.get("fallback") for row in run.get("results") or [])
        if bool(run.get("fallback_used")) != run_fallback:
            fail(errors, "generation_run fallback_used disagrees with results")
        if bool(manifest.get("fallback_used")) != run_fallback:
            fail(errors, "manifest fallback_used disagrees with generation_run")
    else:
        fail(errors, "missing generation_run.json")

    assets = manifest.get("assets") or []
    if len(assets) != 16:
        fail(errors, f"assets len {len(assets)} != 16")
    if len(qa) != 16:
        fail(errors, f"visual_qa len {len(qa)} != 16")

    expected_stems = [stem for _, stem in SHEETS]
    seen = []
    grok_count = 0
    fallback_count = 0
    for family, stem in SHEETS:
        source = ROOT / f"{stem}.png"
        authoring = ROOT / f"{stem}_8k_authoring.jpg"
        if not source.is_file():
            fail(errors, f"missing source {source.name}")
            continue
        if not authoring.is_file():
            fail(errors, f"missing authoring {authoring.name}")
            continue
        with Image.open(source) as img:
            if img.size != NATIVE:
                fail(errors, f"{source.name} size {img.size} != {NATIVE}")
        with Image.open(authoring) as img:
            if img.size != AUTHORING:
                fail(errors, f"{authoring.name} size {img.size} != {AUTHORING}")
        rec = next((row for row in assets if row.get("source") == source.name), None)
        if rec is None:
            fail(errors, f"manifest missing {source.name}")
            continue
        seen.append(stem)
        if rec.get("familyId") != family:
            fail(errors, f"{stem} family {rec.get('familyId')} != {family}")
        if rec.get("source_sha256") != sha256(source):
            fail(errors, f"{stem} source hash mismatch")
        if rec.get("authoring_sha256") != sha256(authoring):
            fail(errors, f"{stem} authoring hash mismatch")
        if rec.get("source_bytes") != source.stat().st_size:
            fail(errors, f"{stem} source bytes mismatch")
        if rec.get("authoring_bytes") != authoring.stat().st_size:
            fail(errors, f"{stem} authoring bytes mismatch")
        if rec.get("source_resolution") != list(NATIVE):
            fail(errors, f"{stem} source_resolution")
        if rec.get("authoring_resolution") != list(AUTHORING):
            fail(errors, f"{stem} authoring_resolution")
        if "Lanczos" not in str(rec.get("authoring_provenance", "")):
            fail(errors, f"{stem} authoring provenance not Lanczos")
        if rec.get("fallback"):
            fallback_count += 1
            if rec.get("fallback_provider") != "openai-codex":
                fail(errors, f"{stem} fallback provider")
            if rec.get("image_model") not in ("gpt-image-1", "gpt-5.6-sol"):
                fail(errors, f"{stem} fallback image_model {rec.get('image_model')}")
        else:
            grok_count += 1
            if rec.get("provider") != "xai-oauth" or rec.get("image_model") != "grok-imagine-image-2.0":
                fail(errors, f"{stem} provider/model")
        qa_row = qa.get(stem)
        if not qa_row:
            fail(errors, f"visual_qa missing {stem}")
        elif qa_row.get("verdict") != "PASS":
            fail(errors, f"{stem} visual_qa {qa_row.get('verdict')}")

    extra = [row["source"] for row in assets if Path(row["source"]).stem not in expected_stems]
    if extra:
        fail(errors, f"unexpected assets {extra}")
    if grok_count + fallback_count != 16:
        fail(errors, f"provider counts {grok_count}+{fallback_count} != 16")

    contact = ROOT / "crownlands_concept_p1_contact_sheet_v001.jpg"
    if not contact.is_file():
        fail(errors, "missing contact sheet")
    else:
        if manifest.get("contact_sheet_sha256") != sha256(contact):
            fail(errors, "contact hash mismatch")
        if manifest.get("contact_sheet_bytes") != contact.stat().st_size:
            fail(errors, "contact bytes mismatch")
        with Image.open(contact) as img:
            if list(img.size) != manifest.get("contact_sheet_resolution"):
                fail(errors, f"contact resolution {img.size}")

    rejected_dir = ROOT / "rejected"
    rejected_files = sorted(p.name for p in rejected_dir.glob("*.png")) if rejected_dir.is_dir() else []
    expected_rejected = [Path(item).name for item in manifest.get("rejected_attempts") or []]
    if sorted(expected_rejected) != rejected_files:
        fail(errors, f"rejected_attempts mismatch disk={rejected_files} manifest={expected_rejected}")
    for item in manifest.get("rejected_attempts") or []:
        if not (ROOT / item).is_file():
            fail(errors, f"missing rejected {item}")

    html = DOCS / "Crownlands_Concept_P1_Packet_V001.html"
    md = DOCS / "Crownlands_Concept_P1_Packet_V001.md"
    decision = DOCS / "Crownlands_Concept_P1_Packet_V001_DECISION.md"
    for doc in (html, md, decision):
        if not doc.is_file():
            fail(errors, f"missing doc {doc.name}")
            continue
        text = doc.read_text(encoding="utf-8")
        if "capital_crownspire" not in text:
            fail(errors, f"{doc.name} missing capital_crownspire")
        invented = [m.group(0) for m in INVENTED_CITY_ID_RE.finditer(text)]
        if invented:
            fail(errors, f"{doc.name} invented city tokens {invented}")
        capitals = re.findall(r"\bcapital_[a-z0-9_]+\b", text, flags=re.I)
        bad_capitals = [tok for tok in capitals if tok != ALLOWED_CAPITAL]
        if bad_capitals:
            fail(errors, f"{doc.name} capital id drift {bad_capitals}")

    if html.is_file():
        html_text = html.read_text(encoding="utf-8")
        for _, stem in SHEETS:
            rel = f"ArtSource/Environment/CrownlandsConceptP1/V001/{stem}.png"
            if rel not in html_text:
                fail(errors, f"html missing link {rel}")
            img_path = ROOT / f"{stem}.png"
            if not img_path.is_file():
                fail(errors, f"html target missing {stem}.png")

    pycache = list(ROOT.rglob("__pycache__"))
    if pycache:
        fail(errors, f"__pycache__ present {pycache}")

    qa_fail = [name for name, row in qa.items() if row.get("verdict") != "PASS"]
    expected_status = (
        "AUTO_APPROVED_FOR_DETERMINISTIC_BLENDER_SUBJECT_TO_OWNER_INSPECTION"
        if not qa_fail
        else "REVISE"
    )
    if manifest.get("status") != expected_status:
        fail(errors, f"status {manifest.get('status')} != {expected_status}")

    print("SHEETS", len(SHEETS))
    print("ASSETS", len(assets))
    print("QA_PASS", 16 - len(qa_fail), "/16")
    print("GROK", grok_count, "FALLBACK", fallback_count)
    print("STATUS", manifest.get("status"))
    if errors:
        print("FAIL", len(errors))
        for item in errors:
            print(" -", item)
        return 1
    print("PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
