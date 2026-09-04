#!/usr/bin/env python3
"""Deterministic Cindermaw v005 source-visual polish: localized snout, material separation."""
from __future__ import annotations

from typing import Any, Mapping, Sequence

import numpy as np


MODEL_ID = "elite_umbral_cindermaw_salamander"
VERSION = "v005"
EXPECTED_VERTICES = 27690
EXPECTED_TRIANGLES = 55334
V004_MODEL_SHA256 = "9486be45241afe61ba04b4f2fedc4d751819acfd2e0d6181a97c1e4cddb2b9d6"
CONCEPT_SHEET_SHA256 = "61a5ea43950826a19dc344c3e8f0413cd78457b33cb85c0aeff52a2e9eb872ee"
CONCEPT_SHEET_PATH = (
    "unity/Docs/Terrestrials/RealmBossesAndElites/ConceptSheets/"
    "tdf_elite_umbral_cindermaw_salamander_concept_sheet_v001.png"
)
PACKET_ROOT = "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001"
V004_MODEL_PATH = (
    f"{PACKET_ROOT}/Models/elite_umbral_cindermaw_salamander/"
    "elite_umbral_cindermaw_salamander_source_v004.fbx"
)
V005_MODEL_PATH = (
    f"{PACKET_ROOT}/Models/elite_umbral_cindermaw_salamander/"
    "elite_umbral_cindermaw_salamander_source_v005.fbx"
)
V005_BLEND_PATH = (
    f"{PACKET_ROOT}/DCC/elite_umbral_cindermaw_salamander_visual_polish_v005.blend"
)
V005_TEXTURE_ROOT = (
    f"{PACKET_ROOT}/Textures/elite_umbral_cindermaw_salamander/retexture_uvclean_visualpolish_v005"
)
STATUS = (
    "clean_geometry_pass_uv_bake_pass_smoothing_pass_normal_detail_pass_"
    "visual_polish_v005_pass_rigging_required"
)

_REGION_TARGETS = {
    "hide": {
        "albedo": np.array([0.045, 0.048, 0.052], dtype=np.float64),
        "roughness": 0.36,
        "metallic": 0.02,
        "mix": 0.55,
    },
    "fins": {
        "albedo": np.array([0.022, 0.024, 0.030], dtype=np.float64),
        "roughness": 0.11,
        "metallic": 0.22,
        "mix": 0.86,
    },
    "scars": {
        "albedo": np.array([0.46, 0.41, 0.34], dtype=np.float64),
        "roughness": 0.54,
        "metallic": 0.01,
        "mix": 0.72,
    },
    "underside": {
        "albedo": np.array([0.31, 0.28, 0.23], dtype=np.float64),
        "roughness": 0.68,
        "metallic": 0.01,
        "mix": 0.74,
    },
    "ember": {
        "albedo": np.array([0.22, 0.055, 0.035], dtype=np.float64),
        "roughness": 0.48,
        "metallic": 0.0,
        "mix": 0.80,
    },
}


def _normalized(points: np.ndarray, bounds_min: Sequence[float], bounds_max: Sequence[float]) -> np.ndarray:
    span = np.maximum(np.asarray(bounds_max, dtype=np.float64) - np.asarray(bounds_min, dtype=np.float64), 1e-9)
    return (np.asarray(points, dtype=np.float64) - np.asarray(bounds_min, dtype=np.float64)) / span


def _smooth01(values: np.ndarray) -> np.ndarray:
    clipped = np.clip(values, 0.0, 1.0)
    return clipped * clipped * (3.0 - 2.0 * clipped)


def snout_influence(points: np.ndarray, bounds_min: Sequence[float], bounds_max: Sequence[float]) -> np.ndarray:
    longitudinal = _normalized(points, bounds_min, bounds_max)[:, 1]
    return _smooth01((0.28 - longitudinal) / 0.28)


def localized_snout_offsets(
    points: np.ndarray,
    bounds_min: Sequence[float],
    bounds_max: Sequence[float],
) -> np.ndarray:
    """Move only the snout: nostril pits, wedge taper, mouth crease, dorsal ridge."""
    points = np.asarray(points, dtype=np.float64)
    bounds_min = np.asarray(bounds_min, dtype=np.float64)
    bounds_max = np.asarray(bounds_max, dtype=np.float64)
    span = np.maximum(bounds_max - bounds_min, 1e-9)
    uvw = _normalized(points, bounds_min, bounds_max)
    lateral = (uvw[:, 0] - 0.5) * 2.0
    longitudinal = uvw[:, 1]
    vertical = uvw[:, 2]
    influence = snout_influence(points, bounds_min, bounds_max)[:, None]
    offsets = np.zeros_like(points)

    def _pit(center_x: float) -> np.ndarray:
        dx = (points[:, 0] - center_x) / (0.045 * span[0] / 0.846)
        dy = (longitudinal - 0.10) / 0.07
        dz = (vertical - 0.70) / 0.10
        return np.exp(-(dx * dx + dy * dy + dz * dz))

    pits = _pit(-0.09) + _pit(0.09)
    ridge = np.exp(-(lateral * lateral) / 0.18) * np.exp(-((vertical - 0.72) ** 2) / 0.08)
    mouth = (
        np.exp(-(lateral * lateral) / 0.22)
        * np.exp(-((vertical - 0.32) ** 2) / 0.05)
        * np.exp(-((longitudinal - 0.05) ** 2) / 0.04)
    )
    wedge = np.clip(1.0 - longitudinal / 0.22, 0.0, 1.0) * np.abs(lateral)

    offsets[:, 2] -= 0.014 * pits
    offsets[:, 2] += 0.0075 * ridge
    offsets[:, 2] -= 0.0065 * mouth
    offsets[:, 0] -= 0.016 * np.sign(points[:, 0] + 1e-12) * wedge
    offsets[:, 1] += 0.0030 * influence[:, 0] * (0.18 - longitudinal)
    return offsets * influence


def material_region_weights(
    points: np.ndarray,
    bounds_min: Sequence[float],
    bounds_max: Sequence[float],
) -> dict[str, np.ndarray]:
    uvw = _normalized(points, bounds_min, bounds_max)
    lateral = (uvw[:, 0] - 0.5) * 2.0
    longitudinal = uvw[:, 1]
    vertical = uvw[:, 2]
    torso = _smooth01((longitudinal - 0.22) / 0.10) * (1.0 - _smooth01((longitudinal - 0.70) / 0.10))
    dorsal = _smooth01((vertical - 0.62) / 0.16)
    ventral = _smooth01((0.40 - vertical) / 0.40)
    midline = np.exp(-(lateral * lateral) / 0.55)
    fins = np.clip(dorsal * torso * (0.45 + 0.55 * midline), 0.0, 1.0)
    scars = np.clip(torso * _smooth01((vertical - 0.52) / 0.22) * midline * (1.0 - 0.35 * fins), 0.0, 1.0)
    underside = np.clip(ventral * (1.0 - 0.25 * fins), 0.0, 1.0)
    mouth = (
        _smooth01((0.12 - longitudinal) / 0.12)
        * np.exp(-(lateral * lateral) / 0.28)
        * np.exp(-((vertical - 0.30) ** 2) / 0.06)
    )
    fin_root = torso * _smooth01((vertical - 0.68) / 0.12) * midline * 0.15
    ember = np.clip(0.85 * mouth + fin_root, 0.0, 1.0)
    hide = np.clip(1.0 - 0.85 * fins - 0.70 * underside - 0.80 * ember - 0.45 * scars, 0.0, 1.0)
    return {
        "hide": hide,
        "fins": fins,
        "scars": scars,
        "underside": underside,
        "ember": ember,
    }


def polish_support_maps(
    base_color: np.ndarray,
    roughness: np.ndarray,
    metallic: np.ndarray,
    weights: Mapping[str, np.ndarray],
) -> dict[str, np.ndarray]:
    rgb = np.asarray(base_color, dtype=np.float32) / np.float32(255.0)
    if rgb.ndim == 2:
        rgb = np.repeat(rgb[..., None], 3, axis=2)
    rough = np.asarray(roughness, dtype=np.float32)
    if rough.ndim == 3:
        rough = rough[..., 0]
    rough = rough / np.float32(255.0)
    metal = np.asarray(metallic, dtype=np.float32)
    if metal.ndim == 3:
        metal = metal[..., 0]
    metal = metal / np.float32(255.0)

    albedo = rgb.copy()
    rough_out = rough.copy()
    metal_out = metal.copy()
    for name, target in _REGION_TARGETS.items():
        weight = np.clip(np.asarray(weights[name], dtype=np.float64), 0.0, 1.0) * target["mix"]
        if weight.ndim == 2:
            albedo = albedo * (1.0 - weight[..., None]) + target["albedo"] * weight[..., None]
        else:
            albedo = albedo * (1.0 - weight) + target["albedo"] * weight
        rough_out = rough_out * (1.0 - weight) + target["roughness"] * weight
        metal_out = metal_out * (1.0 - weight) + target["metallic"] * weight

    return {
        "base_color": np.clip(np.rint(albedo * 255.0), 0, 255).astype(np.uint8),
        "roughness": np.clip(np.rint(rough_out * 255.0), 0, 255).astype(np.uint8),
        "metallic": np.clip(np.rint(metal_out * 255.0), 0, 255).astype(np.uint8),
    }


def rasterize_region_weights(
    uv: np.ndarray,
    positions: np.ndarray,
    bounds_min: Sequence[float],
    bounds_max: Sequence[float],
    resolution: int,
) -> dict[str, np.ndarray]:
    from tools.terrestrial.build_cindermaw_normal_detail import triangle_pixel_samples

    uv = np.asarray(uv, dtype=np.float64)
    positions = np.asarray(positions, dtype=np.float64)
    if uv.ndim != 3 or uv.shape[1:] != (3, 2):
        raise ValueError("uv must have shape (triangles, 3, 2)")
    if positions.shape != uv.shape[:2] + (3,):
        raise ValueError("positions must have shape (triangles, 3, 3)")
    vertex_weights = material_region_weights(positions.reshape(-1, 3), bounds_min, bounds_max)
    names = tuple(vertex_weights)
    stacked = {name: vertex_weights[name].reshape(len(uv), 3) for name in names}
    accum = {name: np.zeros((resolution, resolution), dtype=np.float64) for name in names}
    coverage = np.zeros((resolution, resolution), dtype=np.float64)
    for triangle_index in range(len(uv)):
        rows, columns, barycentric = triangle_pixel_samples(uv[triangle_index], resolution=resolution)
        if len(rows) == 0:
            continue
        coverage[rows, columns] += 1.0
        for name in names:
            accum[name][rows, columns] += barycentric @ stacked[name][triangle_index]
    occupied = coverage > 0.0
    rasterized: dict[str, np.ndarray] = {}
    for name in names:
        values = np.zeros((resolution, resolution), dtype=np.float64)
        values[occupied] = accum[name][occupied] / coverage[occupied]
        rasterized[name] = np.clip(values, 0.0, 1.0)
    rasterized["coverage"] = occupied.astype(np.float64)
    return rasterized


def apply_world_offsets(
    local_positions: np.ndarray,
    world_matrix: np.ndarray,
    bounds_min: Sequence[float],
    bounds_max: Sequence[float],
) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Return new local positions, world offsets, and snout influence."""
    local_positions = np.asarray(local_positions, dtype=np.float64)
    world_matrix = np.asarray(world_matrix, dtype=np.float64)
    linear = world_matrix[:3, :3]
    translation = world_matrix[:3, 3]
    world = local_positions @ linear.T + translation
    offsets = localized_snout_offsets(world, bounds_min, bounds_max)
    inverse = np.linalg.inv(world_matrix)
    homogeneous = np.column_stack((world + offsets, np.ones(len(world))))
    new_local = homogeneous @ inverse.T
    return new_local[:, :3], offsets, snout_influence(world, bounds_min, bounds_max)


def validate_material_separation(samples: Mapping[str, Mapping[str, Any]]) -> list[str]:
    diagnostics: list[str] = []
    hide = samples["hide"]
    fins = samples["fins"]
    scars = samples["scars"]
    underside = samples["underside"]
    ember = samples["ember"]
    hide_luma = float(np.mean(hide["albedo"]))
    scars_luma = float(np.mean(scars["albedo"]))
    if hide_luma >= scars_luma:
        diagnostics.append("hide albedo is not darker than pale heat scars")
    if float(fins["roughness"]) >= float(hide["roughness"]):
        diagnostics.append("obsidian fins are not glossier than pebbled hide")
    if float(fins["roughness"]) >= float(underside["roughness"]):
        diagnostics.append("obsidian fins are not glossier than ash-paste underside")
    if float(fins["metallic"]) <= float(hide["metallic"]):
        diagnostics.append("obsidian fins lack metallic separation from hide")
    ember_rgb = np.asarray(ember["albedo"], dtype=np.float64)
    if float(ember_rgb[0]) >= 0.35:
        diagnostics.append("ember tissue is too bright for a dull seam")
    if float(np.mean(ember_rgb)) >= 0.28:
        diagnostics.append("ember tissue is not confined to a dull red seam")
    if hide_luma >= 0.18:
        diagnostics.append("hide is not soot-black enough")
    if float(underside["roughness"]) <= float(hide["roughness"]):
        diagnostics.append("ash-paste underside is not rougher than wet hide")
    return diagnostics


def validate_localized_geometry(
    before: np.ndarray,
    after: np.ndarray,
    *,
    bounds_min: Sequence[float],
    bounds_max: Sequence[float],
    expected_vertices: int,
    expected_triangles: int,
    actual_triangles: int,
) -> list[str]:
    diagnostics: list[str] = []
    before = np.asarray(before, dtype=np.float64)
    after = np.asarray(after, dtype=np.float64)
    if len(before) != expected_vertices or len(after) != expected_vertices:
        diagnostics.append("vertex count must match the hash-bound v004 topology")
    if actual_triangles != expected_triangles:
        diagnostics.append("triangle count must match the hash-bound v004 topology")
    if before.shape != after.shape:
        diagnostics.append("geometry arrays must stay topology-aligned")
        return diagnostics
    longitudinal = _normalized(before, bounds_min, bounds_max)[:, 1]
    body = longitudinal > 0.32
    if np.any(np.abs(after[body] - before[body]) > 1e-9):
        diagnostics.append("non-snout vertices must remain unmoved")
    return diagnostics


def review_specs() -> list[dict[str, Any]]:
    texture_root = V005_TEXTURE_ROOT
    model_path = V005_MODEL_PATH
    review_root = f"{PACKET_ROOT}/Review"
    return [
        {
            "name": "neutral_closeup",
            "path": f"{review_root}/elite_umbral_cindermaw_salamander_neutral_closeup_v005.png",
            "modelPath": model_path,
            "textureRoot": texture_root,
            "lighting": "neutral",
            "framing": "snout_face_closeup",
            "runtimeVfxIncluded": False,
            "resolution": [1024, 1024],
        },
        {
            "name": "full_body_hero",
            "path": f"{review_root}/elite_umbral_cindermaw_salamander_fullbody_hero_v005.png",
            "modelPath": model_path,
            "textureRoot": texture_root,
            "lighting": "hero",
            "framing": "full_body_threequarter",
            "runtimeVfxIncluded": False,
            "resolution": [1024, 1024],
        },
    ]


def validate_readiness(report: Mapping[str, Any]) -> list[str]:
    diagnostics: list[str] = []
    if report.get("productionReady") is not False:
        diagnostics.append("productionReady must remain false")
    if report.get("rigged") is not False:
        diagnostics.append("rigged must remain false")
    if report.get("runtimeIntegrationState") != "Blocked":
        diagnostics.append("runtimeIntegrationState must remain Blocked")
    status = str(report.get("status", ""))
    if "visual_polish_v005_pass" not in status:
        diagnostics.append("status must record the v005 visual-polish pass")
    if "rigging_required" not in status:
        diagnostics.append("status must keep rigging required")
    return diagnostics
