#!/usr/bin/env python3
"""Assemble 8K authoring upscales, contact sheet, hashes, and manifest for WorldConceptGapP0 V001."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent
AUTHORING_SIZE = (7680, 5120)
SOURCE_3_2 = (1248, 832)

SHEETS = [
    ("fam_shared_door_hardware", "shared_door_hardware_orthos_v001", "Shared civic door / frame / hinge orthos"),
    ("fam_shared_door_hardware", "stonehold_ceremonial_gate_leaf_v001", "Stonehold ceremonial gate-leaf family"),
    ("fam_shared_door_hardware", "eldergrove_ceremonial_gate_leaf_v001", "Eldergrove ceremonial gate-leaf family"),
    ("fam_shared_door_hardware", "crownlands_ceremonial_gate_leaf_v001", "Crownlands ceremonial gate-leaf family"),
    ("fam_shared_door_hardware", "umbral_ceremonial_gate_leaf_v001", "Umbral ceremonial gate-leaf family"),
    ("fam_shared_save_pillar", "shared_save_pillar_hero_form_v001", "Shared save-pillar hero form"),
    ("fam_shared_save_pillar", "stonehold_save_pillar_ornament_v001", "Stonehold save-pillar ornament/material"),
    ("fam_shared_save_pillar", "eldergrove_save_pillar_ornament_v001", "Eldergrove save-pillar ornament/material"),
    ("fam_shared_save_pillar", "crownlands_save_pillar_ornament_v001", "Crownlands save-pillar ornament/material"),
    ("fam_shared_save_pillar", "umbral_save_pillar_ornament_v001", "Umbral save-pillar ornament/material"),
    ("fam_shared_adjacent_realm_bridge", "shared_adjacent_realm_bridge_kit_v001", "Shared 180 m adjacent-realm bridge kit"),
    ("fam_shared_adjacent_realm_bridge", "stonehold_bridge_abutment_v001", "Stonehold bridge abutment skin"),
    ("fam_shared_adjacent_realm_bridge", "eldergrove_bridge_abutment_v001", "Eldergrove bridge abutment skin"),
    ("fam_shared_adjacent_realm_bridge", "crownlands_bridge_abutment_v001", "Crownlands bridge abutment skin"),
    ("fam_shared_adjacent_realm_bridge", "umbral_bridge_abutment_v001", "Umbral bridge abutment skin"),
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
            # letterbox/crop to 3:2 so authoring is exactly 7680x5120
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
    cols = 3
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
    draw.text((margin, margin), "WORLD CONCEPT-GAP P0  ·  V001  ·  GROK 4.6 HIGH", fill="#e9e2d5", font=title_font)
    draw.text(
        (margin, margin + 36),
        "Visual authority only. V013 / sequential-gate plates / 180 m bridge length remain topology authority. 8K copies are Lanczos upscales.",
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
    target = ROOT / "world_concept_gap_p0_contact_sheet_v001.jpg"
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
        contact_sources.append((stem.replace("_v001", "").replace("_", " "), source))
    contact = make_contact_sheet(contact_sources)
    print(f"ASSETS={len(records)} CONTACT={contact.name}")
    (ROOT / "_assemble_records.json").write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
