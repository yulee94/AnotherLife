#!/usr/bin/env python3
"""Assemble 8K authoring upscales, contact sheet, hashes, and records for CrownlandsConceptP1 V001."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent
AUTHORING_SIZE = (7680, 5120)

SHEETS = [
    ("fam_crownlands_capital", "crownlands_capital_district_plan_v001", "Capital Crownspire top-down district plan"),
    ("fam_crownlands_capital", "crownlands_capital_skyline_north_south_v001", "Capital north/south skyline elevations"),
    ("fam_crownlands_capital", "crownlands_capital_skyline_east_west_v001", "Capital east/west skyline elevations"),
    ("fam_crownlands_capital", "crownlands_capital_keep_shell_v001", "Crownspire keep shell orthos"),
    ("fam_crownlands_capital", "crownlands_capital_keep_section_v001", "Keep longitudinal/cross section"),
    ("fam_crownlands_capital", "crownlands_capital_keep_ground_plan_v001", "Furnished keep ground plan"),
    ("fam_crownlands_capital", "crownlands_capital_keep_upper_circulation_v001", "Furnished upper/walltop circulation"),
    ("fam_crownlands_city_kit", "crownlands_city_street_grammar_v001", "6 m city street grammar + elevations"),
    ("fam_crownlands_city_kit", "crownlands_city_dwelling_shell_v001", "Dwelling shell orthos"),
    ("fam_crownlands_city_kit", "crownlands_city_dwelling_interior_v001", "Dwelling furnished interior"),
    ("fam_crownlands_city_kit", "crownlands_city_market_service_public_hall_kit_v001", "Market/service/public-hall kit 2.5x3.0 m"),
    ("fam_crownlands_inner_cave_dungeon", "crownlands_inner_cave_mouth_v001", "Inner non-dragon cave mouth"),
    ("fam_crownlands_inner_cave_dungeon", "crownlands_inner_cave_section_circulation_v001", "Inner cave section + circulation"),
    ("fam_crownlands_outer_cave_dungeon", "crownlands_outer_cave_mouth_v001", "Outer warzone non-dragon cave mouth"),
    ("fam_crownlands_outer_cave_dungeon", "crownlands_outer_cave_loop_choke_section_v001", "Outer cave loop/choke section"),
    ("fam_crownlands_inner_cave_dungeon", "crownlands_cave_chamber_fitting_module_v001", "Cave chamber/fitting module kit"),
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
    draw.text((margin, margin), "CROWNLANDS CONCEPT P1  ·  V001  ·  MERIDIAN OATHROAD", fill="#e9e2d5", font=title_font)
    draw.text(
        (margin, margin + 36),
        "Visual/spatial program only. V013 / capital_crownspire remain topology authority. 8K copies are Lanczos upscales.",
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
    target = ROOT / "crownlands_concept_p1_contact_sheet_v001.jpg"
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
