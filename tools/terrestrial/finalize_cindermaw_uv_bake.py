#!/usr/bin/env python3
"""Finalize staged Cindermaw UV atlases after Blender releases render memory."""
from __future__ import annotations

import argparse
import gc
import hashlib
import json
import shutil
import sys
from pathlib import Path
from typing import Any, Sequence

from PIL import Image

if __package__:
    from tools.terrestrial.dilate_uv_atlas import dilate_transparent_pixels, stitch_tiles
    from tools.terrestrial.repair_realm_creature_geometry import portable_report_path
else:
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
    from tools.terrestrial.dilate_uv_atlas import dilate_transparent_pixels, stitch_tiles
    from tools.terrestrial.repair_realm_creature_geometry import portable_report_path


def ordered_tile_paths(root: Path, name: str, grid: int) -> list[Path]:
    tile_root = root / f".{name}_tiles"
    return [tile_root / f"{row:02d}_{column:02d}.png" for row in range(grid) for column in range(grid)]


def finalize_staged_tiles(tile_paths: Sequence[Path], grid: int, output: Path) -> None:
    images = [Image.open(path) for path in tile_paths]
    stitched = stitch_tiles(images, grid=grid)
    for image in images:
        image.close()
    stitched.save(output, format="PNG", optimize=False)
    stitched.close()
    gc.collect()
    with Image.open(output) as image:
        dilated = dilate_transparent_pixels(image)
    dilated.save(output, format="PNG", optimize=False)
    dilated.close()
    gc.collect()


def build_uv_bake_report(
    *,
    input_path: str,
    input_sha: str,
    output_path: str,
    output_sha: str,
    blend_path: str,
    source_task_ids: Sequence[str],
    metrics: dict[str, Any],
    baked_maps: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "modelId": "elite_umbral_cindermaw_salamander",
        "sourceTaskIds": list(source_task_ids),
        "input": input_path,
        "inputSha256": input_sha,
        "output": output_path,
        "outputSha256": output_sha,
        "editableBlend": blend_path,
        "status": "clean_geometry_pass_uv_bake_complete_normal_detail_rebuild_required",
        "productionReady": False,
        "rigged": False,
        "runtimeIntegrationState": "Blocked",
        "operations": [
            "triangulated concave source quads before deterministic lightmap UV packing",
            "exported only the clean UV atlas and verified no out-of-bounds, zero-area, or overlapping faces",
            "rebaked 8K base color plus 4K roughness, metallic, and AO from the accepted Meshy-7 retexture",
            "replaced the corrupted selected-to-active normal bake with a neutral 4K tangent fallback",
            "kept normal microdetail rebuilding, rigging, LODs, runtime integration, and runtime VFX fail-closed",
        ],
        "metrics": metrics,
        "bakedMaps": baked_maps,
        "diagnostics": [],
    }


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _map_record(path: Path, name: str) -> dict[str, Any]:
    with Image.open(path) as image:
        dimensions = list(image.size)
    record: dict[str, Any] = {
        "name": name,
        "path": str(path.resolve()),
        "dimensions": dimensions,
        "sha256": _sha256(path),
    }
    if name == "normal":
        record["provenance"] = "neutral_tangent"
    return record


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output-model", type=Path, required=True)
    parser.add_argument("--blend", type=Path, required=True)
    parser.add_argument("--texture-dir", type=Path, required=True)
    parser.add_argument("--uv-validation", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--source-task-id", action="append", required=True)
    parser.add_argument("--non-manifold-before", type=int, required=True)
    parser.add_argument("--non-manifold-after", type=int, required=True)
    args = parser.parse_args(argv)

    for name, grid in (("base_color", 4), ("roughness", 2), ("metallic", 2), ("ao", 2)):
        tiles = ordered_tile_paths(args.texture_dir, name, grid)
        if tiles[0].parent.exists():
            if not all(path.is_file() for path in tiles):
                raise RuntimeError(f"incomplete staged tiles for {name}")
            output = args.texture_dir / f"{name}.png"
            finalize_staged_tiles(tiles, grid, output)
            shutil.rmtree(tiles[0].parent)

    uv_validation = json.loads(args.uv_validation.read_text(encoding="utf-8"))
    if uv_validation.get("diagnostics"):
        raise RuntimeError(f"UV validation failed: {uv_validation['diagnostics']}")
    metrics = {
        "uvLayer": uv_validation["uvLayer"],
        "uvFacesOutsideUnit": uv_validation["uvFacesOutsideUnit"],
        "uvZeroAreaFaces": uv_validation["uvZeroAreaFaces"],
        "uvOverlappingFaces": uv_validation["uvOverlappingFaces"],
        "nonManifoldEdgesBefore": args.non_manifold_before,
        "nonManifoldEdgesAfter": args.non_manifold_after,
        "polygonalProjectionBlockerResolved": True,
    }
    maps = [
        _map_record(args.texture_dir / f"{name}.png", name)
        for name in ("base_color", "normal", "roughness", "metallic", "ao")
    ]
    report = build_uv_bake_report(
        input_path=str(args.input.resolve()),
        input_sha=_sha256(args.input),
        output_path=str(args.output_model.resolve()),
        output_sha=_sha256(args.output_model),
        blend_path=str(args.blend.resolve()),
        source_task_ids=args.source_task_id,
        metrics=metrics,
        baked_maps=maps,
    )
    for key in ("input", "output", "editableBlend"):
        report[key] = portable_report_path(Path(report[key]), args.repo_root)
    for record in report["bakedMaps"]:
        record["path"] = portable_report_path(Path(record["path"]), args.repo_root)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": "PASS", "report": str(args.report), "metrics": metrics}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(__import__("sys").argv[1:]))
