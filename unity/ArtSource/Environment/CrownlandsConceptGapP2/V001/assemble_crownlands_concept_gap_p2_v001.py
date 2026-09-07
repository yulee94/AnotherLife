#!/usr/bin/env python3
"""Assemble 8K authoring upscales, contact sheet, hashes, and records for CrownlandsConceptGapP2 V001."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent
AUTHORING_SIZE = (7680, 5120)

SHEETS = [
    ("fam_crownlands_sequential_gate_wall_complex", "crownlands_sequential_gate_outer_face_v001", "Sequential outer-face elevation"),
    ("fam_crownlands_sequential_gate_wall_complex", "crownlands_sequential_gate_inner_face_v001", "Sequential inner-face elevation"),
    ("fam_crownlands_sequential_gate_wall_complex", "crownlands_sequential_gate_longitudinal_section_v001", "Sequential plan + 18 m passage section"),
    ("fam_crownlands_sequential_gate_wall_complex", "crownlands_sequential_wall_walltop_modules_v001", "Wall / walltop defender modules"),
    ("fam_crownlands_fortress_single_gate", "crownlands_fortress_plan_apron_v001", "One-gate fortress plan with 30 m clear apron"),
    ("fam_crownlands_fortress_single_gate", "crownlands_fortress_elevations_v001", "One-gate fortress front/side elevations"),
    ("fam_crownlands_fortress_single_gate", "crownlands_fortress_keep_flag_anchor_v001", "Keep shell + central flag-anchor socket"),
    ("fam_crownlands_terrain", "crownlands_terrain_grades_worldscar_wallend_v001", "Inner/outer grade, Worldscar brink, wall-end"),
    ("fam_crownlands_terrain", "crownlands_terrain_bridge_abutment_180m_v001", "180 m bridge endpoints / abutment fit"),
    ("fam_crownlands_terrain", "crownlands_terrain_route_bed_v001", "Route bed/ruts/drainage + player-scale rocks"),
    ("fam_crownlands_ecosystem_dressing", "crownlands_ecosystem_composition_habitat_v001", "Ecosystem composition and habitat without animals"),
    ("fam_crownlands_ecosystem_dressing", "crownlands_cave_exterior_language_v001", "Cave exterior landform language"),
    ("fam_crownlands_material_lod", "crownlands_capital_city_material_kits_v001", "Capital/city material kits"),
    ("fam_crownlands_material_lod", "crownlands_lod_material_lighting_reference_v001", "LOD / material / lighting reference"),
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def font(size: int) -> ImageFont.ImageFont:
    for candidate in (Path("C:/Windows/Fonts/seguisb.ttf"), Path("C:/Windows/Fonts/segoeui.ttf")):
        if candidate.is_file():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default()


def to_png_rgb(path: Path) -> tuple[int, int]:
    with Image.open(path) as opened:
        image = opened.convert("RGB")
        size = image.size
        image.save(path, format="PNG", optimize=True)
    return size


def make_8k(source: Path) -> Path:
    target = source.with_name(f"{source.stem}_8k_authoring.jpg")
    with Image.open(source) as opened:
        image = opened.convert("RGB")
        w, h = image.size
        if w * 2 != h * 3:
            target_ratio = 3 / 2
            src_ratio = w / h
            if src_ratio > target_ratio:
                new_w = int(h * target_ratio)
                left = (w - new_w) // 2
                image = image.crop((left, 0, left + new_w, h))
            else:
                new_h = int(w / target_ratio)
                top = (h - new_h) // 2
                image = image.crop((0, top, w, top + new_h))
        image = image.resize(AUTHORING_SIZE, Image.Resampling.LANCZOS)
        image = image.filter(ImageFilter.UnsharpMask(radius=1.2, percent=70, threshold=3))
        image.save(target, quality=95, subsampling=0, optimize=True, progressive=True)
    with Image.open(target) as check:
        if check.size != AUTHORING_SIZE:
            raise ValueError(f"8K verification failed for {target}: {check.size}")
    return target


def make_contact_sheet(sources: list[tuple[str, Path]]) -> Path:
    thumb_w, thumb_h = 480, 320
    cols = 4
    margin = 24
    gap = 16
    band = 56
    rows = (len(sources) + cols - 1) // cols
    width = margin * 2 + cols * thumb_w + (cols - 1) * gap
    height = margin + 72 + rows * (band + thumb_h + gap)
    canvas = Image.new("RGB", (width, height), "#0b0e12")
    draw = ImageDraw.Draw(canvas)
    title_font = font(28)
    meta_font = font(16)
    draw.text((margin, margin), "CROWNLANDS CONCEPT GAP P2  ·  V001  ·  GROK 4.6 HIGH", fill="#e9e2d5", font=title_font)
    draw.text(
        (margin, margin + 36),
        "Visual/spatial program only. Technical plate / V013 remain topology authority. 8K copies are Lanczos upscales.",
        fill="#a8b0b8",
        font=meta_font,
    )
    y0 = margin + 72
    for i, (label, source) in enumerate(sources):
        col = i % cols
        row = i // cols
        x = margin + col * (thumb_w + gap)
        y = y0 + row * (band + thumb_h + gap)
        draw.text((x, y), label.upper(), fill="#e9e2d5", font=meta_font)
        with Image.open(source) as opened:
            thumb = opened.convert("RGB").resize((thumb_w, thumb_h), Image.Resampling.LANCZOS)
        canvas.paste(thumb, (x, y + band - 20))
    target = ROOT / "crownlands_concept_gap_p2_contact_sheet_v001.jpg"
    canvas.save(target, quality=94, subsampling=0, optimize=True, progressive=True)
    return target


def main() -> None:
    records = []
    contact_sources = []
    for family, stem, title in SHEETS:
        source = ROOT / f"{stem}.png"
        if not source.is_file():
            raise FileNotFoundError(source)
        native = to_png_rgb(source)
        authoring = make_8k(source)
        records.append(
            {
                "familyId": family,
                "source": source.name,
                "title": title,
                "source_resolution": list(native),
                "source_bytes": source.stat().st_size,
                "source_sha256": sha256(source),
                "authoring": authoring.name,
                "authoring_resolution": list(AUTHORING_SIZE),
                "authoring_bytes": authoring.stat().st_size,
                "authoring_sha256": sha256(authoring),
                "authoring_provenance": "Lanczos upscale with conservative unsharp mask; not native 8K generation",
            }
        )
        contact_sources.append((stem.replace("_v001", "").replace("crownlands_", "").replace("_", " "), source))
    contact = make_contact_sheet(contact_sources)
    print(f"ASSETS={len(records)} CONTACT={contact.name} SIZE={contact.stat().st_size}")
    (ROOT / "_assemble_records.json").write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
