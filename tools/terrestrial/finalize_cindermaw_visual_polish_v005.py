#!/usr/bin/env python3
"""Finalize Cindermaw's v005 visual-polish packet fail-closed."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any, Mapping

from PIL import Image

from tools.terrestrial.cindermaw_visual_polish_v005 import (
    MODEL_ID,
    STATUS,
    V004_MODEL_SHA256,
    validate_readiness,
)


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


def _portable(path: Path, repo_root: Path) -> str:
    return path.resolve().relative_to(repo_root.resolve()).as_posix()


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


def finalize_visual_polish_packet(
    *,
    repo_root: Path,
    input_model_path: Path,
    output_model_path: Path,
    editable_blend_path: Path,
    texture_dir: Path,
    reviews: Mapping[str, Path],
    output_report_path: Path,
    geometry_metrics: Mapping[str, Any],
    expected_base_resolution: int = 8192,
    expected_support_resolution: int = 4096,
) -> dict[str, Any]:
    diagnostics: list[str] = []
    if not input_model_path.is_file():
        diagnostics.append("v004 input model is missing")
    elif _sha256(input_model_path) != V004_MODEL_SHA256 and input_model_path.name.endswith("_source_v004.fbx"):
        diagnostics.append("v004 inputSha256 does not match the immutable source")
    if not output_model_path.is_file():
        diagnostics.append("v005 output model is missing")
    if not editable_blend_path.is_file():
        diagnostics.append("v005 editable blend is missing")
    expected_maps = {
        "base_color": [expected_base_resolution, expected_base_resolution],
        "normal": [expected_support_resolution, expected_support_resolution],
        "roughness": [expected_support_resolution, expected_support_resolution],
        "metallic": [expected_support_resolution, expected_support_resolution],
        "ao": [expected_support_resolution, expected_support_resolution],
    }
    for name, dimensions in expected_maps.items():
        path = texture_dir / f"{name}.png"
        if not path.is_file():
            diagnostics.append(f"support map is missing: {name}")
            continue
        with Image.open(path) as image:
            if list(image.size) != dimensions:
                diagnostics.append(f"support map dimensions mismatch: {name}")
    baked_maps = [
        _image_record(texture_dir / name, repo_root, Path(name).stem)
        for name in (
            "base_color.png",
            "normal.png",
            "roughness.png",
            "metallic.png",
            "ao.png",
        )
    ]
    next(item for item in baked_maps if item["name"] == "normal")["provenance"] = (
        "object_space_procedural_height_to_clean_uv_tangent_normal_v001"
    )
    review_records = []
    for name, path in reviews.items():
        if not path.is_file():
            diagnostics.append(f"review render is missing: {name}")
            continue
        review_records.append(_image_record(path, repo_root, name))
    if geometry_metrics.get("vertices") != 27690:
        diagnostics.append("geometry vertices must remain 27690")
    if geometry_metrics.get("polygons") != 55334:
        diagnostics.append("geometry polygons must remain 55334")
    for key in ("uvFacesOutsideUnit", "uvZeroAreaFaces", "uvOverlappingFaces"):
        if geometry_metrics.get(key) != 0:
            diagnostics.append(f"{key} must be zero")
    if geometry_metrics.get("uvLayer") != "UVMap_Clean":
        diagnostics.append("uvLayer must remain UVMap_Clean")
    report = {
        "modelId": MODEL_ID,
        "sourceTaskIds": SOURCE_TASK_IDS,
        "input": _portable(input_model_path, repo_root),
        "inputSha256": _sha256(input_model_path) if input_model_path.is_file() else "",
        "output": _portable(output_model_path, repo_root),
        "outputSha256": _sha256(output_model_path) if output_model_path.is_file() else "",
        "editableBlend": _portable(editable_blend_path, repo_root),
        "editableBlendSha256": _sha256(editable_blend_path) if editable_blend_path.is_file() else "",
        "status": STATUS,
        "productionReady": False,
        "rigged": False,
        "runtimeIntegrationState": "Blocked",
        "operations": [
            "localized snout vertex offsets for nostril pits, wedge taper, mouth crease, and dorsal ridge",
            "strengthened wet soot-black hide, glossy chipped obsidian fins, pale heat scars, and ash-paste underside",
            "kept dull ember tissue confined to approved mouth/fin-root seams",
            "preserved v004 topology, UVMap_Clean, and separate runtime heat/steam/distortion VFX",
        ],
        "metrics": dict(geometry_metrics),
        "reviews": review_records,
        "bakedMaps": baked_maps,
        "diagnostics": diagnostics,
        "conceptSheetSha256": "61a5ea43950826a19dc344c3e8f0413cd78457b33cb85c0aeff52a2e9eb872ee",
        "preservedV004ModelSha256": V004_MODEL_SHA256,
    }
    diagnostics.extend(validate_readiness(report))
    if diagnostics:
        raise RuntimeError(f"Cindermaw v005 visual-polish evidence failed: {diagnostics}")
    output_report_path.parent.mkdir(parents=True, exist_ok=True)
    output_report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report
