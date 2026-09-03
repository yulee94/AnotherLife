#!/usr/bin/env python3
"""Finalize the approved Meshy-7 Cindermaw retexture packet."""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path
from typing import Sequence

import numpy as np
from PIL import Image

if __package__:
    from tools.terrestrial.repair_realm_creature_geometry import portable_report_path
else:
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
    from tools.terrestrial.repair_realm_creature_geometry import portable_report_path


def grade_cindermaw_base_color(image: Image.Image) -> Image.Image:
    rgb = np.asarray(image.convert("RGB"), dtype=np.float32) / 255.0
    red = rgb[..., 0]
    green = rgb[..., 1]
    blue = rgb[..., 2]
    luma = 0.2126 * red + 0.7152 * green + 0.0722 * blue
    warm = (red > blue * 1.12) & (red > green * 1.20)

    out = np.empty_like(rgb)
    out[..., 0] = 8.0 + 20.0 * luma
    out[..., 1] = 14.0 + 28.0 * luma
    out[..., 2] = 24.0 + 45.0 * luma

    intensity = np.maximum(np.maximum(red, green), blue)
    out[..., 0] = np.where(warm, 55.0 + 100.0 * intensity, out[..., 0])
    out[..., 1] = np.where(warm, 18.0 + 30.0 * luma, out[..., 1])
    out[..., 2] = np.where(warm, 20.0 + 26.0 * luma, out[..., 2])
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB")


def compose_metallic_smoothness(metallic: Image.Image, roughness: Image.Image) -> Image.Image:
    metallic_l = metallic.convert("L")
    roughness_l = roughness.convert("L")
    if metallic_l.size != roughness_l.size:
        raise ValueError("metallic and roughness dimensions must match")
    zero = Image.new("L", metallic_l.size, 0)
    smoothness = roughness_l.point(lambda value: 255 - value)
    return Image.merge("RGBA", (metallic_l, zero, zero, smoothness))


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def file_record(path: Path, root: Path) -> dict[str, object]:
    with Image.open(path) as image:
        dimensions = list(image.size)
    return {
        "path": portable_report_path(path, root),
        "bytes": path.stat().st_size,
        "sha256": _sha256(path),
        "dimensions": dimensions,
        "mediaType": "image/png",
    }


def _require_contained(path: Path, root: Path, label: str, root_label: str) -> None:
    try:
        portable_report_path(path, root)
    except ValueError as exc:
        raise ValueError(f"{label} escapes {root_label}: {path}") from exc


def validate_retexture_paths(
    *,
    packet_root: Path,
    repo_root: Path,
    input_fbx: Path,
    input_texture_dir: Path,
    output_fbx: Path,
    output_texture_dir: Path,
    uv_validation: Path,
    report: Path,
) -> None:
    _require_contained(packet_root, repo_root, "packet root", "repository")
    for path, label in (
        (output_fbx, "output FBX"),
        (output_texture_dir, "output texture directory"),
    ):
        _require_contained(path, packet_root, label, "packet root")
    for path, label in (
        (input_fbx, "input FBX"),
        (input_texture_dir, "input texture directory"),
        (uv_validation, "UV validation"),
        (report, "report"),
    ):
        _require_contained(path, repo_root, label, "repository")


def _parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-fbx", type=Path, required=True)
    parser.add_argument("--input-texture-dir", type=Path, required=True)
    parser.add_argument("--output-fbx", type=Path, required=True)
    parser.add_argument("--output-texture-dir", type=Path, required=True)
    parser.add_argument("--packet-root", type=Path, required=True)
    parser.add_argument("--uv-validation", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--source-task-id", required=True)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = _parse_args(argv or [])
    validate_retexture_paths(
        packet_root=args.packet_root,
        repo_root=args.repo_root,
        input_fbx=args.input_fbx,
        input_texture_dir=args.input_texture_dir,
        output_fbx=args.output_fbx,
        output_texture_dir=args.output_texture_dir,
        uv_validation=args.uv_validation,
        report=args.report,
    )
    uv_validation = json.loads(args.uv_validation.read_text(encoding="utf-8"))
    expected_uv = {
        "uvFacesOutsideUnit": 0,
        "uvZeroAreaFaces": 0,
        "uvOverlappingFaces": 0,
    }
    for key, expected in expected_uv.items():
        if uv_validation.get(key) != expected:
            raise RuntimeError(f"UV validation failed: {key}={uv_validation.get(key)!r}")

    args.output_fbx.parent.mkdir(parents=True, exist_ok=True)
    args.output_texture_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(args.input_fbx, args.output_fbx)

    source_base = Image.open(args.input_texture_dir / "base_color.png")
    graded = grade_cindermaw_base_color(source_base)
    base_path = args.output_texture_dir / "base_color.png"
    graded.save(base_path, optimize=True)

    for name in ("normal", "roughness", "metallic"):
        shutil.copy2(args.input_texture_dir / f"{name}.png", args.output_texture_dir / f"{name}.png")

    support_size = Image.open(args.output_texture_dir / "normal.png").size
    ao_path = args.output_texture_dir / "ao.png"
    Image.new("L", support_size, 255).save(ao_path, optimize=True)

    metallic = Image.open(args.output_texture_dir / "metallic.png")
    roughness = Image.open(args.output_texture_dir / "roughness.png")
    packed = compose_metallic_smoothness(metallic, roughness)
    packed_path = args.output_texture_dir / "metallic_smoothness.png"
    packed.save(packed_path, optimize=True)

    runtime = args.output_texture_dir / "runtime_2k"
    runtime.mkdir(parents=True, exist_ok=True)
    runtime_sources = {
        "base_color.png": graded,
        "normal.png": Image.open(args.output_texture_dir / "normal.png"),
        "ao.png": Image.open(ao_path),
        "metallic_smoothness.png": packed,
    }
    for name, image in runtime_sources.items():
        image.resize((2048, 2048), Image.Resampling.LANCZOS).save(runtime / name, optimize=True)

    texture_paths = [
        base_path,
        args.output_texture_dir / "normal.png",
        args.output_texture_dir / "roughness.png",
        args.output_texture_dir / "metallic.png",
        ao_path,
        packed_path,
        runtime / "base_color.png",
        runtime / "normal.png",
        runtime / "ao.png",
        runtime / "metallic_smoothness.png",
    ]
    report = {
        "modelId": "elite_umbral_cindermaw_salamander",
        "sourceTaskIds": [args.source_task_id],
        "input": portable_report_path(args.input_fbx, args.repo_root),
        "inputSha256": _sha256(args.input_fbx),
        "output": portable_report_path(args.output_fbx, args.repo_root),
        "outputSha256": _sha256(args.output_fbx),
        "status": "clean_geometry_pass_retextured_uvs_texture_grade_complete_rigging_required",
        "productionReady": False,
        "rigged": False,
        "runtimeIntegrationState": "Blocked",
        "operations": [
            "accepted Meshy-7 clean retexture UVs after overlap and bounds validation",
            "graded cyan source albedo to approved charcoal/obsidian Umbral palette while retaining controlled ember accents",
            "retained 8K base color and 4K normal/roughness/metallic authoring maps",
            "generated neutral 4K AO fallback, Unity metallic-smoothness packing, and 2K runtime derivatives",
        ],
        "uvValidation": uv_validation,
        "aoProvenance": "neutral white fallback; geometry AO bake remains optional material enhancement, not a UV blocker",
        "textures": [file_record(path, args.repo_root) for path in texture_paths],
        "diagnostics": [],
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": "PASS", "report": str(args.report), "textures": len(texture_paths)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(__import__("sys").argv[1:]))
