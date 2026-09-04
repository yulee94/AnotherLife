#!/usr/bin/env python3
"""Derive bounded 2048 Unity PBR maps from Cindermaw v005 authoring textures."""

from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path

from PIL import Image

if __package__:
    from tools.terrestrial.finalize_cindermaw_retexture import compose_metallic_smoothness
else:
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
    from tools.terrestrial.finalize_cindermaw_retexture import compose_metallic_smoothness


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = (
    REPO_ROOT
    / "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001"
    / "Textures/elite_umbral_cindermaw_salamander/retexture_uvclean_visualpolish_v005"
)
OUTPUT_DIR = (
    REPO_ROOT
    / "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001"
    / "Textures/elite_umbral_cindermaw_salamander/runtime_2k_v005"
)
RUNTIME_EDGE = 2048


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def resize_to_runtime(image: Image.Image) -> Image.Image:
    if image.size[0] != image.size[1]:
        raise ValueError(f"non-square texture: {image.size}")
    if image.size[0] < RUNTIME_EDGE:
        raise ValueError(f"authoring edge {image.size[0]} is below runtime {RUNTIME_EDGE}")
    if image.size == (RUNTIME_EDGE, RUNTIME_EDGE):
        return image
    return image.resize((RUNTIME_EDGE, RUNTIME_EDGE), Image.Resampling.LANCZOS)


def build_runtime_maps(
    source_dir: Path = SOURCE_DIR,
    output_dir: Path = OUTPUT_DIR,
) -> dict[str, dict[str, object]]:
    output_dir.mkdir(parents=True, exist_ok=True)
    with Image.open(source_dir / "base_color.png") as image:
        resize_to_runtime(image.convert("RGB")).save(output_dir / "base_color.png")
    with Image.open(source_dir / "normal.png") as image:
        resize_to_runtime(image.convert("RGB")).save(output_dir / "normal.png")
    with Image.open(source_dir / "ao.png") as image:
        resize_to_runtime(image.convert("L")).save(output_dir / "ao.png")
    with Image.open(source_dir / "metallic.png") as metallic, Image.open(
        source_dir / "roughness.png"
    ) as roughness:
        packed = compose_metallic_smoothness(
            resize_to_runtime(metallic.convert("L")),
            resize_to_runtime(roughness.convert("L")),
        )
        packed.save(output_dir / "metallic_smoothness.png")

    records = {}
    for name, role in (
        ("base_color.png", "base_color"),
        ("normal.png", "normal"),
        ("metallic_smoothness.png", "metallic_smoothness"),
        ("ao.png", "ambient_occlusion"),
    ):
        path = output_dir / name
        with Image.open(path) as image:
            dimensions = list(image.size)
        if dimensions != [RUNTIME_EDGE, RUNTIME_EDGE]:
            raise ValueError(f"{name} is {dimensions}, expected {RUNTIME_EDGE}")
        records[role] = {
            "role": role,
            "path": path.as_posix(),
            "bytes": path.stat().st_size,
            "sha256": sha256_file(path),
            "dimensions": dimensions,
        }
    return records


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=SOURCE_DIR)
    parser.add_argument("--output-dir", type=Path, default=OUTPUT_DIR)
    args = parser.parse_args()
    records = build_runtime_maps(args.source_dir, args.output_dir)
    for role, record in records.items():
        print(f"{role} bytes={record['bytes']} sha256={record['sha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
