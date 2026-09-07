#!/usr/bin/env python3
"""Assemble 8K authoring upscales, contact sheet, hashes, and records for StoneholdConceptP1 V001."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent
AUTHORING_SIZE = (7680, 5120)

SHEETS = [
    ("fam_stonehold_capital", "stonehold_capital_district_plan_v001", "Capital Anvildeep top-down district plan"),
    ("fam_stonehold_capital", "stonehold_capital_skyline_north_south_v001", "Capital north/south skyline elevations"),
    ("fam_stonehold_capital", "stonehold_capital_skyline_east_west_v001", "Capital east/west skyline elevations"),
    ("fam_stonehold_capital", "stonehold_capital_hero_keep_shell_v001", "Enterable hero keep shell orthos"),
    ("fam_stonehold_capital", "stonehold_capital_keep_interior_program_v001", "Keep furnished interior program plans"),
    ("fam_stonehold_city_kit", "stonehold_city_street_plan_elevation_v001", "City street plan and elevation"),
    ("fam_stonehold_city_kit", "stonehold_city_house_module_v001", "House module exterior + interior"),
    ("fam_stonehold_city_kit", "stonehold_city_shop_module_v001", "Shop module exterior + interior"),
    ("fam_stonehold_city_kit", "stonehold_city_service_module_v001", "Service module exterior + interior"),
    ("fam_stonehold_city_kit", "stonehold_city_workshop_module_v001", "Workshop module exterior + interior"),
    ("fam_stonehold_inner_cave_dungeon", "stonehold_inner_cave_mouth_v001", "Inner non-dragon cave mouth"),
    ("fam_stonehold_inner_cave_dungeon", "stonehold_inner_cave_section_circulation_v001", "Inner cave section + circulation"),
    ("fam_stonehold_inner_cave_dungeon", "stonehold_inner_cave_chamber_kit_v001", "Inner cave chamber kit"),
    ("fam_stonehold_outer_cave_dungeon", "stonehold_outer_cave_mouth_v001", "Outer warzone non-dragon cave mouth"),
    ("fam_stonehold_outer_cave_dungeon", "stonehold_outer_cave_section_combat_circulation_v001", "Outer cave section + combat circulation"),
    ("fam_stonehold_outer_cave_dungeon", "stonehold_outer_cave_chamber_kit_v001", "Outer cave combat chamber kit"),
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
    draw.text((margin, margin), "STONEHOLD CONCEPT P1  ·  V001  ·  GROK 4.6 HIGH", fill="#e9e2d5", font=title_font)
    draw.text(
        (margin, margin + 36),
        "Visual/spatial program only. V013 / catalog IDs remain topology authority. 8K copies are Lanczos upscales.",
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
    target = ROOT / "stonehold_concept_p1_contact_sheet_v001.jpg"
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
        contact_sources.append((stem.replace("_v001", "").replace("stonehold_", "").replace("_", " "), source))
    contact = make_contact_sheet(contact_sources)
    print(f"ASSETS={len(records)} CONTACT={contact.name} SIZE={contact.stat().st_size}")
    (ROOT / "_assemble_records.json").write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
