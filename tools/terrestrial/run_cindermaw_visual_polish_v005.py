#!/usr/bin/env python3
"""Orchestrate Cindermaw v005 visual-polish sources, maps, reviews, and evidence."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Sequence

import numpy as np
from PIL import Image

from tools.terrestrial.cindermaw_visual_polish_v005 import (
    CONCEPT_SHEET_PATH,
    CONCEPT_SHEET_SHA256,
    EXPECTED_TRIANGLES,
    EXPECTED_VERTICES,
    PACKET_ROOT,
    V004_MODEL_PATH,
    V004_MODEL_SHA256,
    V005_BLEND_PATH,
    V005_MODEL_PATH,
    V005_TEXTURE_ROOT,
    polish_support_maps,
    rasterize_region_weights,
    review_specs,
)
from tools.terrestrial.finalize_cindermaw_visual_polish_v005 import finalize_visual_polish_packet


BLENDER = Path(os.environ.get("AL_BLENDER_EXECUTABLE", r"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe"))
V004_TEXTURE_ROOT = (
    f"{PACKET_ROOT}/Textures/elite_umbral_cindermaw_salamander/retexture_uvclean_normaldetail_v004"
)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _run_blender(script: Path, args: Sequence[str], log_path: Path) -> None:
    command = [
        str(BLENDER),
        "--background",
        "--factory-startup",
        "--python-exit-code",
        "1",
        "--python",
        str(script),
        "--",
        *args,
    ]
    log_path.parent.mkdir(parents=True, exist_ok=True)
    completed = subprocess.run(command, capture_output=True, text=True)
    log_path.write_text(completed.stdout + "\n" + completed.stderr, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(f"blender failed ({completed.returncode}): {log_path}")


def polish_textures(repo_root: Path, surface_path: Path, weight_resolution: int = 1024) -> None:
    source_dir = repo_root / V004_TEXTURE_ROOT
    output_dir = repo_root / V005_TEXTURE_ROOT
    output_dir.mkdir(parents=True, exist_ok=True)
    with np.load(surface_path, allow_pickle=False) as surface:
        raster = rasterize_region_weights(
            surface["uv"],
            surface["positions"],
            surface["bounds_min"],
            surface["bounds_max"],
            weight_resolution,
        )
    with Image.open(source_dir / "base_color.png") as image:
        base = np.asarray(image.convert("RGB"), dtype=np.uint8)
    with Image.open(source_dir / "roughness.png") as image:
        roughness = np.asarray(image.convert("L"), dtype=np.uint8)
    with Image.open(source_dir / "metallic.png") as image:
        metallic = np.asarray(image.convert("L"), dtype=np.uint8)

    names = ("hide", "fins", "scars", "underside", "ember")
    support_size = roughness.shape[0]
    weights_support = {
        name: np.asarray(
            Image.fromarray(np.clip(raster[name] * 255.0, 0, 255).astype(np.uint8), mode="L").resize(
                (support_size, support_size), Image.Resampling.BILINEAR
            ),
            dtype=np.float32,
        )
        / np.float32(255.0)
        for name in names
    }
    dummy_color = np.repeat(roughness[..., None], 3, axis=2)
    polished_support = polish_support_maps(dummy_color, roughness, metallic, weights_support)
    Image.fromarray(polished_support["roughness"], mode="L").save(output_dir / "roughness.png")
    Image.fromarray(polished_support["metallic"], mode="L").save(output_dir / "metallic.png")
    del dummy_color, polished_support, weights_support

    color_size = base.shape[0]
    weights_u8 = {
        name: np.asarray(
            Image.fromarray(np.clip(raster[name] * 255.0, 0, 255).astype(np.uint8), mode="L").resize(
                (color_size, color_size), Image.Resampling.BILINEAR
            ),
            dtype=np.uint8,
        )
        for name in names
    }
    out_color = np.empty_like(base)
    tile = 2048
    height, width = base.shape[:2]
    for row in range(0, height, tile):
        row_end = min(row + tile, height)
        weights_row = {
            name: weights_u8[name][row:row_end].astype(np.float32) / np.float32(255.0)
            for name in names
        }
        dummy_rough = np.full((row_end - row, width), 128, dtype=np.uint8)
        dummy_metal = np.zeros((row_end - row, width), dtype=np.uint8)
        out_color[row:row_end] = polish_support_maps(
            base[row:row_end], dummy_rough, dummy_metal, weights_row
        )["base_color"]
    Image.fromarray(out_color, mode="RGB").save(output_dir / "base_color.png")
    shutil.copy2(source_dir / "normal.png", output_dir / "normal.png")
    shutil.copy2(source_dir / "ao.png", output_dir / "ao.png")


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--skip-geometry", action="store_true")
    parser.add_argument("--skip-textures", action="store_true")
    parser.add_argument("--skip-reviews", action="store_true")
    args = parser.parse_args(argv)
    repo_root = args.repo_root.resolve()
    model_v004 = repo_root / V004_MODEL_PATH
    if _sha256(model_v004) != V004_MODEL_SHA256:
        raise RuntimeError("v004 model hash mismatch; refusing to polish a mutated source")
    concept = repo_root / CONCEPT_SHEET_PATH
    if _sha256(concept) != CONCEPT_SHEET_SHA256:
        raise RuntimeError("approved Cindermaw concept sheet hash mismatch")

    logs = repo_root / "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001/DCCReports"
    surface_path = repo_root / "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/DCC/elite_umbral_cindermaw_salamander_surface_v004.npz"
    metrics_path = logs / "elite_umbral_cindermaw_salamander_visual_polish_geometry_v005.json"
    if not args.skip_geometry:
        _run_blender(
            repo_root / "tools/terrestrial/apply_cindermaw_visual_polish_v005.py",
            [
                "--input-model",
                str(model_v004),
                "--output-model",
                str(repo_root / V005_MODEL_PATH),
                "--output-blend",
                str(repo_root / V005_BLEND_PATH),
                "--metrics",
                str(metrics_path),
            ],
            logs / "elite_umbral_cindermaw_salamander_visual_polish_geometry_v005.log",
        )
        _run_blender(
            repo_root / "tools/terrestrial/build_cindermaw_normal_detail.py",
            [
                "export-surface",
                "--model",
                str(model_v004),
                "--output",
                str(surface_path),
                "--portable-model-path",
                V004_MODEL_PATH,
                "--expected-vertices",
                str(EXPECTED_VERTICES),
                "--expected-triangles",
                str(EXPECTED_TRIANGLES),
            ],
            logs / "elite_umbral_cindermaw_salamander_surface_export_v004.log",
        )
        log = (logs / "elite_umbral_cindermaw_salamander_visual_polish_geometry_v005.log").read_text(encoding="utf-8")
        if "CINDERMAW_V005_GEOMETRY_COMPLETE" not in log:
            raise RuntimeError("geometry completion marker missing")

    if not args.skip_textures:
        polish_textures(repo_root, surface_path)

    if not args.skip_reviews:
        _run_blender(
            repo_root / "tools/terrestrial/render_cindermaw_visual_polish_v005.py",
            ["--repo-root", str(repo_root)],
            logs / "elite_umbral_cindermaw_salamander_visual_polish_review_v005.log",
        )
        log = (logs / "elite_umbral_cindermaw_salamander_visual_polish_review_v005.log").read_text(encoding="utf-8")
        if "CINDERMAW_V005_REVIEW_COMPLETE" not in log:
            raise RuntimeError("review completion marker missing")

    geometry_metrics = json.loads(metrics_path.read_text(encoding="utf-8"))
    reviews = {spec["name"]: repo_root / spec["path"] for spec in review_specs()}
    report = finalize_visual_polish_packet(
        repo_root=repo_root,
        input_model_path=model_v004,
        output_model_path=repo_root / V005_MODEL_PATH,
        editable_blend_path=repo_root / V005_BLEND_PATH,
        texture_dir=repo_root / V005_TEXTURE_ROOT,
        reviews=reviews,
        output_report_path=logs / "elite_umbral_cindermaw_salamander_visual_polish_v005.json",
        geometry_metrics=geometry_metrics,
    )
    print(json.dumps({"status": report["status"], "outputSha256": report["outputSha256"]}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
