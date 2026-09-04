#!/usr/bin/env python3
"""Finalize Cindermaw's authored normal-detail packet fail-closed."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import shutil
from typing import Any

import numpy as np
from PIL import Image


METHOD = "object_space_procedural_height_to_clean_uv_tangent_normal_v001"
MODEL_ID = "elite_umbral_cindermaw_salamander"
SOURCE_TASK_IDS = [
    "01a05f90-dc1f-723e-9e7a-4e3feb8f3dbc",
    "01a05fa3-16b8-70f5-a0bd-cca9f316e455",
    "01a06569-2956-73a2-a51e-bade35802fba",
]


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _sha256_lf_text(path: Path) -> str:
    """Hash text evidence after platform-independent newline normalization."""
    normalized = path.read_bytes().replace(b"\r\n", b"\n").replace(b"\r", b"\n")
    return hashlib.sha256(normalized).hexdigest()


def validate_normal_detail_evidence(
    metrics: dict[str, Any],
    *,
    normal_path: Path,
    model_path: Path,
    expected_resolution: int = 4096,
    expected_strength: float = 0.010,
) -> list[str]:
    diagnostics: list[str] = []
    expected_dimensions = [expected_resolution, expected_resolution]
    if metrics.get("status") != "PASS":
        diagnostics.append("normal-detail metrics status must be PASS")
    if metrics.get("method") != METHOD:
        diagnostics.append("normal-detail method mismatch")
    if metrics.get("authoredNormalDetail") is not True:
        diagnostics.append("authoredNormalDetail must be true")
    if metrics.get("runtimeVfxSeparate") is not True:
        diagnostics.append("runtimeVfxSeparate must be true")
    if metrics.get("orientation") != "OpenGL +Y":
        diagnostics.append("normal orientation must be OpenGL +Y")
    coordinate_frame = metrics.get("coordinateFrame")
    expected_axes = {
        "lateralAxis": "world X",
        "longitudinalAxis": "world Y",
        "dorsalAxis": "world Z",
    }
    if not isinstance(coordinate_frame, dict) or any(
        coordinate_frame.get(key) != value for key, value in expected_axes.items()
    ):
        diagnostics.append("metrics coordinateFrame axes are invalid")
    else:
        span = coordinate_frame.get("span")
        if (
            not isinstance(span, list)
            or len(span) != 3
            or not all(isinstance(value, (int, float)) and value > 0 for value in span)
            or int(np.argmax(span)) != 1
        ):
            diagnostics.append("metrics coordinateFrame world Y must be longitudinal")
    if metrics.get("dimensions") != expected_dimensions:
        diagnostics.append("normal-detail dimensions mismatch")
    if metrics.get("strength") != expected_strength:
        diagnostics.append("normal-detail strength mismatch")
    if float(metrics.get("gutterRadiusPixels", -1.0)) != 2.0:
        diagnostics.append("normal gutter radius must be exactly two pixels")
    if metrics.get("atlasBackground") != "neutral_tangent":
        diagnostics.append("normal atlas background must stay neutral tangent")
    if not model_path.is_file() or metrics.get("modelSha256") != _sha256(model_path):
        diagnostics.append("metrics modelSha256 does not match selected source")
    if not normal_path.is_file():
        diagnostics.append("normal map is missing")
        return diagnostics
    if metrics.get("outputSha256") != _sha256(normal_path):
        diagnostics.append("metrics outputSha256 does not match normal map")

    with Image.open(normal_path) as image:
        if image.mode != "RGB":
            diagnostics.append("normal map must be RGB")
        if list(image.size) != expected_dimensions:
            diagnostics.append("normal map dimensions mismatch")
        pixels = np.asarray(image.convert("RGB"), dtype=np.float32)
    decoded = pixels / 255.0 * 2.0 - 1.0
    angular = np.degrees(
        np.arccos(
            np.clip(
                decoded[..., 2] / np.maximum(np.linalg.norm(decoded, axis=2), 1e-12),
                -1.0,
                1.0,
            )
        )
    )
    pixel_p95 = float(np.percentile(angular, 95.0))
    if pixel_p95 < 2.0:
        diagnostics.append("normal map is effectively neutral")
    if pixel_p95 > 20.0:
        diagnostics.append("normal map pixel P95 angle exceeds 20 degrees")
    if float(np.max(angular)) > 35.0:
        diagnostics.append("normal map pixel maximum angle exceeds 35 degrees")
    if float(np.min(decoded[..., 2])) <= 0.0:
        diagnostics.append("normal map contains non-positive tangent Z")

    detail_metrics = metrics.get("metrics")
    if not isinstance(detail_metrics, dict):
        diagnostics.append("normal-detail metrics payload is missing")
        return diagnostics
    p95 = detail_metrics.get("angularP95Degrees")
    maximum = detail_metrics.get("angularMaxDegrees")
    unit_error = detail_metrics.get("unitLengthMaxError")
    if not isinstance(p95, (int, float)) or not 5.0 <= p95 <= 20.0:
        diagnostics.append("reported angularP95Degrees is outside the authored-detail gate")
    if not isinstance(maximum, (int, float)) or maximum > 35.0:
        diagnostics.append("reported angularMaxDegrees exceeds the authored-detail gate")
    if not isinstance(unit_error, (int, float)) or unit_error > 1e-9:
        diagnostics.append("reported unitLengthMaxError exceeds the tangent-normal gate")
    return diagnostics


def _portable(path: Path, repo_root: Path) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError as exc:
        raise RuntimeError(f"path escapes repository: {path}") from exc


def _image_record(path: Path, repo_root: Path, name: str) -> dict[str, Any]:
    with Image.open(path) as image:
        dimensions = list(image.size)
        image.verify()
    return {
        "name": name,
        "path": _portable(path, repo_root),
        "dimensions": dimensions,
        "sha256": _sha256(path),
    }


def finalize_normal_detail_packet(
    *,
    repo_root: Path,
    model_path: Path,
    source_texture_dir: Path,
    output_texture_dir: Path,
    metrics: dict[str, Any],
    uv_report_path: Path,
    smoothing_report_path: Path,
    review_path: Path,
    output_report_path: Path,
    expected_base_resolution: int = 8192,
    expected_support_resolution: int = 4096,
) -> dict[str, Any]:
    normal_path = output_texture_dir / "normal.png"
    diagnostics = validate_normal_detail_evidence(
        metrics,
        normal_path=normal_path,
        model_path=model_path,
        expected_resolution=expected_support_resolution,
    )
    uv_report = json.loads(uv_report_path.read_text(encoding="utf-8"))
    smoothing_report = json.loads(smoothing_report_path.read_text(encoding="utf-8"))
    if uv_report.get("diagnostics") != []:
        diagnostics.append("source UV report diagnostics must be empty")
    uv_metrics = uv_report.get("metrics") or uv_report
    expected_model_path = _portable(model_path, repo_root)
    if uv_report.get("input") != expected_model_path:
        diagnostics.append("source UV report input does not match selected source")
    if uv_report.get("inputSha256") != _sha256(model_path):
        diagnostics.append("source UV report inputSha256 does not match selected source")
    for key in ("uvFacesOutsideUnit", "uvZeroAreaFaces", "uvOverlappingFaces"):
        if uv_metrics.get(key) != 0:
            diagnostics.append(f"source UV report {key} must be zero")
    if smoothing_report.get("diagnostics") != []:
        diagnostics.append("smoothing report diagnostics must be empty")
    if smoothing_report.get("outputSha256") != _sha256(model_path):
        diagnostics.append("smoothing report outputSha256 does not match selected source")
    blend_path = smoothing_report.get("editableBlend")
    blend_sha256 = smoothing_report.get("editableBlendSha256")
    if not isinstance(blend_path, str) or not blend_path or Path(blend_path).is_absolute():
        diagnostics.append("smoothing report editableBlend is not portable")
    else:
        blend_file = (repo_root / blend_path).resolve()
        try:
            blend_file.relative_to(repo_root.resolve())
        except ValueError:
            diagnostics.append("smoothing report editableBlend escapes repository")
        else:
            if not blend_file.is_file():
                diagnostics.append("smoothing report editableBlend is missing")
            elif blend_sha256 != _sha256(blend_file):
                diagnostics.append("smoothing report editableBlendSha256 does not match")
    smoothing_metrics = smoothing_report.get("metrics") or {}
    if smoothing_metrics.get("customNormalsRemoved") is not True:
        diagnostics.append("smoothing report must confirm custom-normal removal")
    if not isinstance(smoothing_metrics.get("sharpEdgesBefore"), int):
        diagnostics.append("smoothing report lacks sharpEdgesBefore")
    if not isinstance(smoothing_metrics.get("sharpEdgesAfter"), int):
        diagnostics.append("smoothing report lacks sharpEdgesAfter")
    elif smoothing_metrics["sharpEdgesAfter"] >= smoothing_metrics.get("sharpEdgesBefore", 0):
        diagnostics.append("smoothing report does not reduce accidental sharp edges")

    expected_dimensions = {
        "base_color": [expected_base_resolution, expected_base_resolution],
        "roughness": [expected_support_resolution, expected_support_resolution],
        "metallic": [expected_support_resolution, expected_support_resolution],
        "ao": [expected_support_resolution, expected_support_resolution],
    }
    output_texture_dir.mkdir(parents=True, exist_ok=True)
    for name, dimensions in expected_dimensions.items():
        source = source_texture_dir / f"{name}.png"
        if not source.is_file():
            diagnostics.append(f"support map is missing: {name}")
            continue
        with Image.open(source) as image:
            if list(image.size) != dimensions:
                diagnostics.append(f"support map dimensions mismatch: {name}")
        if not diagnostics:
            shutil.copy2(source, output_texture_dir / source.name)

    if not review_path.is_file():
        diagnostics.append("review render is missing")
    if diagnostics:
        raise RuntimeError(f"Cindermaw normal-detail evidence failed: {diagnostics}")

    baked_maps = [
        _image_record(output_texture_dir / name, repo_root, Path(name).stem)
        for name in (
            "base_color.png",
            "normal.png",
            "roughness.png",
            "metallic.png",
            "ao.png",
        )
    ]
    next(item for item in baked_maps if item["name"] == "normal")["provenance"] = METHOD
    review_record = _image_record(review_path, repo_root, "review")
    input_path = smoothing_report.get("input") or _portable(model_path, repo_root)
    input_sha256 = smoothing_report.get("inputSha256") or _sha256(model_path)
    report = {
        "modelId": MODEL_ID,
        "sourceTaskIds": SOURCE_TASK_IDS,
        "input": input_path,
        "inputSha256": input_sha256,
        "output": _portable(model_path, repo_root),
        "outputSha256": _sha256(model_path),
        "editableBlend": blend_path,
        "editableBlendSha256": blend_sha256,
        "status": "clean_geometry_pass_uv_bake_pass_smoothing_pass_normal_detail_pass_texture_grade_pass_rigging_required",
        "productionReady": False,
        "rigged": False,
        "runtimeIntegrationState": "Blocked",
        "operations": [
            "recomputed deliberate 60-degree smoothing before tangent extraction",
            "authored anatomy-weighted macro plates, mid pebbles, and fine pores in object space",
            "rasterized the continuous field into the clean UV tangent basis at 4096 square",
            "preserved the approved charcoal/obsidian color packet and separate runtime VFX boundary",
        ],
        "metrics": {
            "uvLayer": "UVMap_Clean",
            "uvFacesOutsideUnit": 0,
            "uvZeroAreaFaces": 0,
            "uvOverlappingFaces": 0,
            "sharpEdgesBefore": smoothing_metrics["sharpEdgesBefore"],
            "sharpEdgesAfter": smoothing_metrics["sharpEdgesAfter"],
            "normalAngularP50Degrees": metrics["metrics"]["angularP50Degrees"],
            "normalAngularP95Degrees": metrics["metrics"]["angularP95Degrees"],
            "normalAngularMaxDegrees": metrics["metrics"]["angularMaxDegrees"],
            "normalStrength": metrics["strength"],
            "polygonalProjectionBlockerResolved": True,
        },
        "normalDetail": metrics,
        "sourceUvEvidence": {
            "path": _portable(uv_report_path, repo_root),
            "sha256": _sha256_lf_text(uv_report_path),
        },
        "smoothingEvidence": {
            "path": _portable(smoothing_report_path, repo_root),
            "sha256": _sha256_lf_text(smoothing_report_path),
        },
        "review": review_record,
        "bakedMaps": baked_maps,
        "diagnostics": [],
    }
    output_report_path.parent.mkdir(parents=True, exist_ok=True)
    output_report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report
