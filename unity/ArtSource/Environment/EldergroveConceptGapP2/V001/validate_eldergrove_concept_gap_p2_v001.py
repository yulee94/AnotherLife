#!/usr/bin/env python3
"""Fail-closed validation of EldergroveConceptGapP2 V001 dimensions, hashes, links, and counts."""

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
    "fam_eldergrove_sequential_gate_wall_complex",
    "fam_eldergrove_fortress_single_gate",
    "fam_eldergrove_terrain",
    "fam_eldergrove_ecosystem_dressing",
    "fam_eldergrove_interior_furnishings",
]
SHEETS = [
    ("fam_eldergrove_sequential_gate_wall_complex", "eldergrove_sequential_gate_outer_face_v001"),
    ("fam_eldergrove_sequential_gate_wall_complex", "eldergrove_sequential_gate_inner_face_v001"),
    ("fam_eldergrove_sequential_gate_wall_complex", "eldergrove_sequential_gate_longitudinal_section_v001"),
    ("fam_eldergrove_sequential_gate_wall_complex", "eldergrove_sequential_wall_walltop_modules_v001"),
    ("fam_eldergrove_fortress_single_gate", "eldergrove_fortress_plan_apron_v001"),
    ("fam_eldergrove_fortress_single_gate", "eldergrove_fortress_elevations_v001"),
    ("fam_eldergrove_fortress_single_gate", "eldergrove_fortress_keep_flag_mast_v001"),
    ("fam_eldergrove_terrain", "eldergrove_terrain_grades_worldscar_wallend_v001"),
    ("fam_eldergrove_terrain", "eldergrove_terrain_route_bed_v001"),
    ("fam_eldergrove_terrain", "eldergrove_terrain_outer_biome_reads_v001"),
    ("fam_eldergrove_ecosystem_dressing", "eldergrove_ecosystem_family_orthos_v001"),
    ("fam_eldergrove_ecosystem_dressing", "eldergrove_ecosystem_composition_plots_v001"),
    ("fam_eldergrove_interior_furnishings", "eldergrove_interior_room_plates_v001"),
    ("fam_eldergrove_interior_furnishings", "eldergrove_interior_civic_furniture_v001"),
]
LOCKED_IDS = [
    "gate_eldergrove_greenveil",
    "gate_eldergrove_greenveil_outer",
    "wall_complex_eldergrove_greenveil",
]
FORTRESS_PLACEHOLDERS = [
    "fortress_eldergrove_01",
    "fortress_eldergrove_02",
    "fortress_eldergrove_03",
    "fortress_eldergrove_04",
]
FORBIDDEN_PATH_TOKENS = [
    "EldergroveConceptP1",
    "StoneholdConceptGapP2",
    "StoneholdConceptP1",
]


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
    manifest_path = ROOT / "eldergrove_concept_gap_p2_manifest_v001.json"
    qa_path = ROOT / "visual_qa_v001.json"
    if not manifest_path.is_file():
        print("MISSING manifest")
        return 1
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    qa = json.loads(qa_path.read_text(encoding="utf-8"))

    if manifest.get("asset_count") != 14:
        fail(errors, f"asset_count {manifest.get('asset_count')} != 14")
    if manifest.get("families") != FAMILIES:
        fail(errors, f"families mismatch {manifest.get('families')}")
    if manifest.get("authority", {}).get("stableGateIds") != LOCKED_IDS:
        fail(errors, f"stableGateIds drifted {manifest.get('authority', {}).get('stableGateIds')}")
    if manifest.get("identity", {}).get("fortressIds") != FORTRESS_PLACEHOLDERS:
        fail(errors, f"fortressIds drifted {manifest.get('identity', {}).get('fortressIds')}")
    if manifest.get("identity", {}).get("fortressIdsArePlaceholders") is not True:
        fail(errors, "fortressIdsArePlaceholders must be true")
    if manifest.get("chat_model") != "grok-4.6":
        fail(errors, f"chat_model {manifest.get('chat_model')}")
    if manifest.get("image_model") != "grok-imagine-image-2.0":
        fail(errors, f"image_model {manifest.get('image_model')}")
    if manifest.get("fallback_policy") != "GPT-5.6 Sol only if Grok returns no answer":
        fail(errors, "fallback_policy mismatch")
    run_path = ROOT / "generation_run.json"
    if run_path.is_file():
        run = json.loads(run_path.read_text(encoding="utf-8"))
        if run.get("fallback_used") != manifest.get("fallback_used"):
            fail(errors, "generation_run fallback_used disagrees with manifest")
        if len(run.get("results") or []) != 14:
            fail(errors, f"generation_run results {len(run.get('results') or [])}")
        if any(not row.get("ok") for row in run.get("results") or []):
            fail(errors, "generation_run contains failed sheets")
        if manifest.get("fallback_used") is False:
            if any(row.get("fallback") for row in run.get("results") or []):
                fail(errors, "generation_run contains fallback sheets while fallback_used is false")
            if manifest.get("fallback_provider") is not None:
                fail(errors, "fallback_provider must be null when unused")
        else:
            if manifest.get("fallback_provider") != "openai-codex":
                fail(errors, "fallback_provider must be openai-codex when used")
            if not any(row.get("fallback") for row in run.get("results") or []):
                fail(errors, "fallback_used true but no fallback results")
    else:
        fail(errors, "missing generation_run.json")

    assets = manifest.get("assets") or []
    if len(assets) != 14:
        fail(errors, f"assets len {len(assets)} != 14")
    if len(qa) != 14:
        fail(errors, f"visual_qa len {len(qa)} != 14")

    expected_stems = [stem for _, stem in SHEETS]
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
        if rec.get("image_model") not in ("grok-imagine-image-2.0", "gpt-image-1"):
            fail(errors, f"{stem} image_model {rec.get('image_model')}")
        if rec.get("fallback"):
            if rec.get("provider") != "openai-codex" or rec.get("image_model") != "gpt-image-1":
                fail(errors, f"{stem} fallback provider/model")
        else:
            if rec.get("provider") != "xai-oauth" or rec.get("image_model") != "grok-imagine-image-2.0":
                fail(errors, f"{stem} provider/model")
        qa_row = qa.get(stem)
        if not qa_row:
            fail(errors, f"visual_qa missing {stem}")
        elif qa_row.get("verdict") not in ("PASS", "REVISE"):
            fail(errors, f"{stem} visual_qa {qa_row.get('verdict')}")

    extra = [row["source"] for row in assets if Path(row["source"]).stem not in expected_stems]
    if extra:
        fail(errors, f"unexpected assets {extra}")

    contact = ROOT / "eldergrove_concept_gap_p2_contact_sheet_v001.jpg"
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
    rejected_listed = manifest.get("rejected_attempts") or []
    if not isinstance(rejected_listed, list):
        fail(errors, "rejected_attempts not a list")
    else:
        for rel in rejected_listed:
            path = ROOT / rel
            if not path.is_file():
                fail(errors, f"missing rejected {rel}")
            if "rejected/" not in rel.replace("\\", "/"):
                fail(errors, f"rejected path not under rejected/ {rel}")
        if rejected_dir.is_dir():
            on_disk = sorted(p.relative_to(ROOT).as_posix() for p in rejected_dir.glob("*.png"))
            listed = sorted(rel.replace("\\", "/") for rel in rejected_listed)
            if on_disk != listed:
                fail(errors, f"rejected archive mismatch disk={on_disk} listed={listed}")

    html = DOCS / "Eldergrove_Concept_Gap_P2_Packet_V001.html"
    md = DOCS / "Eldergrove_Concept_Gap_P2_Packet_V001.md"
    decision = DOCS / "Eldergrove_Concept_Gap_P2_Packet_V001_DECISION.md"
    for doc in (html, md, decision):
        if not doc.is_file():
            fail(errors, f"missing doc {doc.name}")
            continue
        text = doc.read_text(encoding="utf-8")
        for token in LOCKED_IDS:
            if token not in text:
                fail(errors, f"{doc.name} missing {token}")
        for token in ("Moonroot Vigil", "30 m", "18 m"):
            if token not in text:
                fail(errors, f"{doc.name} missing {token}")
        for family in FAMILIES:
            if family not in text:
                fail(errors, f"{doc.name} missing {family}")
        if "EldergroveConceptP1" in text and "do not" not in text.lower():
            pass
        remote = re.findall(r"https?://", text)
        if remote and doc.suffix == ".html":
            fail(errors, f"{doc.name} remote urls {remote}")

    if html.is_file():
        html_text = html.read_text(encoding="utf-8")
        for _, stem in SHEETS:
            rel = f"ArtSource/Environment/EldergroveConceptGapP2/V001/{stem}.png"
            if rel not in html_text:
                fail(errors, f"html missing link {rel}")
            img_path = ROOT / f"{stem}.png"
            if not img_path.is_file():
                fail(errors, f"html target missing {stem}.png")
        for token in FORBIDDEN_PATH_TOKENS:
            if f"ArtSource/Environment/{token}" in html_text:
                fail(errors, f"html links into {token}")

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
    if qa_fail and expected_status != "REVISE":
        fail(errors, "non-PASS QA must force REVISE")

    print("SHEETS", len(SHEETS))
    print("ASSETS", len(assets))
    print("QA_PASS", 14 - len(qa_fail), "/14")
    print("STATUS", manifest.get("status"))
    print("FALLBACK_USED", manifest.get("fallback_used"))
    if errors:
        print("FAIL", len(errors))
        for item in errors:
            print(" -", item)
        return 1
    print("PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
