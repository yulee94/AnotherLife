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


MODEL_ID = "elite_umbral_cindermaw_salamander"
SOURCE_STATUS = "intermediate_retexture_uv_overlap_repair_required"
EXPECTED_MAP_DIMENSIONS = {
    "base_color": [8192, 8192],
    "normal": [4096, 4096],
    "roughness": [4096, 4096],
    "metallic": [4096, 4096],
    "ao": [4096, 4096],
}


def _is_sha256(value: object) -> bool:
    return isinstance(value, str) and len(value) == 64 and all(char in "0123456789abcdef" for char in value)


def validate_uv_finalization_evidence(
    *,
    uv_validation: dict[str, Any],
    source_report: dict[str, Any],
    input_report_path: str,
    input_sha: str,
    output_report_path: str,
    output_sha: str,
) -> list[str]:
    diagnostics: list[str] = []
    if source_report.get("modelId") != MODEL_ID:
        diagnostics.append("source report modelId mismatch")
    if source_report.get("status") != SOURCE_STATUS:
        diagnostics.append("source report status mismatch")
    if source_report.get("output") != input_report_path:
        diagnostics.append("source report output path mismatch")
    if source_report.get("outputSha256") != input_sha:
        diagnostics.append("source report output SHA-256 mismatch")
    source_diagnostics = source_report.get("diagnostics")
    if not isinstance(source_diagnostics, list) or not source_diagnostics:
        diagnostics.append("source report must explicitly disclose its rejected UV-overlap diagnostics")
    source_uv = source_report.get("uvValidation")
    if not isinstance(source_uv, dict) or not isinstance(source_uv.get("uvOverlappingFaces"), int) or source_uv["uvOverlappingFaces"] <= 0:
        diagnostics.append("source report must record a positive UV overlap count for the rejected intermediate")
    if source_report.get("productionReady") is not False:
        diagnostics.append("source report productionReady must remain false")
    if source_report.get("rigged") is not False:
        diagnostics.append("source report rigged must remain false")
    if source_report.get("runtimeIntegrationState") != "Blocked":
        diagnostics.append("source report runtimeIntegrationState must remain Blocked")

    if uv_validation.get("modelId") != MODEL_ID:
        diagnostics.append("UV validation modelId mismatch")
    if uv_validation.get("input") != output_report_path:
        diagnostics.append("UV validation input path mismatch")
    if uv_validation.get("inputSha256") != output_sha:
        diagnostics.append("UV validation input SHA-256 mismatch")
    if uv_validation.get("diagnostics") != []:
        diagnostics.append("UV validation diagnostics must be an explicit empty list")
    if uv_validation.get("uvLayer") != "UVMap_Clean":
        diagnostics.append("UV validation must target UVMap_Clean")
    for field in ("uvFacesOutsideUnit", "uvZeroAreaFaces", "uvOverlappingFaces"):
        value = uv_validation.get(field)
        if value != 0:
            diagnostics.append(f"{field} must be 0; got {value!r}")
    if not _is_sha256(input_sha) or not _is_sha256(output_sha):
        diagnostics.append("input and output SHA-256 values must be lowercase digests")
    return diagnostics


def validate_baked_map_records(records: Sequence[dict[str, Any]]) -> list[str]:
    diagnostics: list[str] = []
    by_name: dict[str, dict[str, Any]] = {}
    for record in records:
        name = record.get("name")
        if name not in EXPECTED_MAP_DIMENSIONS:
            diagnostics.append(f"unexpected baked map: {name!r}")
            continue
        if name in by_name:
            diagnostics.append(f"duplicate baked map: {name}")
            continue
        by_name[name] = record
    for name, expected in EXPECTED_MAP_DIMENSIONS.items():
        record = by_name.get(name)
        if record is None:
            diagnostics.append(f"missing baked map: {name}")
            continue
        if record.get("dimensions") != expected:
            diagnostics.append(f"{name} dimensions must be {expected}; got {record.get('dimensions')!r}")
        if not _is_sha256(record.get("sha256")):
            diagnostics.append(f"{name} SHA-256 must be a lowercase digest")
        if name == "normal" and record.get("provenance") != "neutral_tangent":
            diagnostics.append("normal provenance must disclose neutral_tangent")
    return diagnostics


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
        "modelId": MODEL_ID,
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
    parser.add_argument("--source-report", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--source-task-id", action="append", required=True)
    parser.add_argument("--non-manifold-before", type=int, required=True)
    parser.add_argument("--non-manifold-after", type=int, required=True)
    args = parser.parse_args(argv)

    repo_root = args.repo_root.resolve()
    input_report_path = portable_report_path(args.input, repo_root)
    output_report_path = portable_report_path(args.output_model, repo_root)
    portable_report_path(args.blend, repo_root)
    portable_report_path(args.texture_dir, repo_root)
    portable_report_path(args.uv_validation, repo_root)
    portable_report_path(args.source_report, repo_root)
    portable_report_path(args.report, repo_root)
    input_sha = _sha256(args.input)
    output_sha = _sha256(args.output_model)
    uv_validation = json.loads(args.uv_validation.read_text(encoding="utf-8"))
    source_report = json.loads(args.source_report.read_text(encoding="utf-8"))
    evidence_diagnostics = validate_uv_finalization_evidence(
        uv_validation=uv_validation,
        source_report=source_report,
        input_report_path=input_report_path,
        input_sha=input_sha,
        output_report_path=output_report_path,
        output_sha=output_sha,
    )
    if evidence_diagnostics:
        raise RuntimeError(f"UV finalization evidence failed: {evidence_diagnostics}")

    for name, grid in (("base_color", 4), ("roughness", 2), ("metallic", 2), ("ao", 2)):
        tiles = ordered_tile_paths(args.texture_dir, name, grid)
        if tiles[0].parent.exists():
            if not all(path.is_file() for path in tiles):
                raise RuntimeError(f"incomplete staged tiles for {name}")
            output = args.texture_dir / f"{name}.png"
            finalize_staged_tiles(tiles, grid, output)
            shutil.rmtree(tiles[0].parent)

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
    map_diagnostics = validate_baked_map_records(maps)
    if map_diagnostics:
        raise RuntimeError(f"UV baked-map validation failed: {map_diagnostics}")
    report = build_uv_bake_report(
        input_path=input_report_path,
        input_sha=input_sha,
        output_path=output_report_path,
        output_sha=output_sha,
        blend_path=str(args.blend.resolve()),
        source_task_ids=args.source_task_id,
        metrics=metrics,
        baked_maps=maps,
    )
    report["editableBlend"] = portable_report_path(args.blend, repo_root)
    for record in report["bakedMaps"]:
        record["path"] = portable_report_path(Path(record["path"]), repo_root)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": "PASS", "report": str(args.report), "metrics": metrics}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(__import__("sys").argv[1:]))
