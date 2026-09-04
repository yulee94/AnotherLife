#!/usr/bin/env python3
"""Build deterministic tangent-space relief for Cindermaw's clean UV source."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys
from typing import Sequence

import numpy as np


def bounded_normal_gutter(rgba: np.ndarray, *, radius: float = 2.0) -> np.ndarray:
    """Copy normal texels into a bounded gutter and keep distant atlas space neutral."""
    from scipy.ndimage import distance_transform_edt

    pixels = np.asarray(rgba, dtype=np.uint8)
    if pixels.ndim != 3 or pixels.shape[2] != 4:
        raise ValueError("normal gutter input must be an RGBA image")
    if radius < 0.0:
        raise ValueError("normal gutter radius must be non-negative")
    occupied = pixels[:, :, 3] > 0
    if not np.any(occupied):
        raise ValueError("normal gutter input has no occupied texels")

    output = np.empty(pixels.shape[:2] + (3,), dtype=np.uint8)
    output[:, :] = (128, 128, 255)
    output[occupied] = pixels[:, :, :3][occupied]
    if radius == 0.0:
        return output

    distance, indices = distance_transform_edt(
        ~occupied,
        return_distances=True,
        return_indices=True,
    )
    gutter = (~occupied) & (distance <= radius)
    output[gutter] = pixels[indices[0][gutter], indices[1][gutter], :3]
    return output


def _euler_rotation(x_degrees: float, y_degrees: float, z_degrees: float) -> np.ndarray:
    x, y, z = np.radians([x_degrees, y_degrees, z_degrees])
    rotate_x = np.array(
        [[1.0, 0.0, 0.0], [0.0, np.cos(x), -np.sin(x)], [0.0, np.sin(x), np.cos(x)]]
    )
    rotate_y = np.array(
        [[np.cos(y), 0.0, np.sin(y)], [0.0, 1.0, 0.0], [-np.sin(y), 0.0, np.cos(y)]]
    )
    rotate_z = np.array(
        [[np.cos(z), -np.sin(z), 0.0], [np.sin(z), np.cos(z), 0.0], [0.0, 0.0, 1.0]]
    )
    return rotate_z @ rotate_y @ rotate_x


def relief_octaves() -> tuple[tuple[float, int, float, np.ndarray], ...]:
    """Return the fixed multiscale, non-axis-aligned stone-hide spectrum."""
    return (
        (18.0, 2301, 0.10, _euler_rotation(17.0, 31.0, 11.0)),
        (36.0, 2302, 0.035, _euler_rotation(-23.0, 19.0, 37.0)),
        (72.0, 2303, 0.015, _euler_rotation(29.0, -17.0, 43.0)),
    )


def cellular_pebble_with_gradient(
    points: np.ndarray,
    *,
    frequency: float,
    seed: int,
    rotation: np.ndarray,
) -> tuple[np.ndarray, np.ndarray]:
    """Return rounded 3D cellular pebbles and their object-space gradient."""
    coordinates = np.asarray(points, dtype=np.float64)
    rotation = np.asarray(rotation, dtype=np.float64)
    rotated = (coordinates @ rotation.T) * frequency
    base = np.floor(rotated).astype(np.int64)
    best_distance_squared = np.full(len(coordinates), np.inf, dtype=np.float64)
    best_delta = np.zeros((len(coordinates), 3), dtype=np.float64)
    for offset_x in (-1, 0, 1):
        for offset_y in (-1, 0, 1):
            for offset_z in (-1, 0, 1):
                cell = base + np.array([offset_x, offset_y, offset_z], dtype=np.int64)
                feature = cell.astype(np.float64) + np.column_stack(
                    (
                        _lattice_hash(cell[:, 0], cell[:, 1], cell[:, 2], seed),
                        _lattice_hash(cell[:, 0], cell[:, 1], cell[:, 2], seed + 1013),
                        _lattice_hash(cell[:, 0], cell[:, 1], cell[:, 2], seed + 2027),
                    )
                )
                delta = rotated - feature
                distance_squared = np.sum(delta * delta, axis=1)
                closer = distance_squared < best_distance_squared
                best_distance_squared[closer] = distance_squared[closer]
                best_delta[closer] = delta[closer]

    distance = np.sqrt(best_distance_squared)
    radius = 0.90
    scaled = np.clip(distance / radius, 0.0, 1.0)
    values = (1.0 - scaled * scaled) ** 2
    derivative = np.zeros(len(coordinates), dtype=np.float64)
    active = (distance > 1e-12) & (distance < radius)
    derivative[active] = (
        -4.0
        * scaled[active]
        * (1.0 - scaled[active] * scaled[active])
        / radius
    )
    gradient_rotated = np.zeros_like(best_delta)
    gradient_rotated[active] = (
        derivative[active, None] * best_delta[active] / distance[active, None]
    )
    gradients = gradient_rotated @ rotation * frequency
    return values, gradients


def cellular_plate_with_gradient(
    points: np.ndarray,
    *,
    frequency: float,
    seed: int,
    rotation: np.ndarray,
    crease_width: float,
) -> tuple[np.ndarray, np.ndarray]:
    """Return flat irregular plates separated by smooth Voronoi creases."""
    points = np.asarray(points, dtype=np.float64)
    rotation = np.asarray(rotation, dtype=np.float64)
    if points.ndim != 2 or points.shape[1] != 3:
        raise ValueError("points must have shape (N, 3)")
    if rotation.shape != (3, 3):
        raise ValueError("rotation must have shape (3, 3)")
    if frequency <= 0.0 or crease_width <= 0.0:
        raise ValueError("frequency and crease_width must be positive")

    rotated = points @ rotation.T
    scaled = rotated * frequency
    lattice = np.floor(scaled).astype(np.int64)
    nearest_distance_squared = np.full(len(points), np.inf, dtype=np.float64)
    second_distance_squared = np.full(len(points), np.inf, dtype=np.float64)
    nearest_delta = np.zeros_like(points)
    second_delta = np.zeros_like(points)
    for x_offset in (-1, 0, 1):
        for y_offset in (-1, 0, 1):
            for z_offset in (-1, 0, 1):
                cell = lattice + np.array([x_offset, y_offset, z_offset], dtype=np.int64)
                feature = cell.astype(np.float64) + np.column_stack(
                    (
                        _lattice_hash(cell[:, 0], cell[:, 1], cell[:, 2], seed),
                        _lattice_hash(cell[:, 0], cell[:, 1], cell[:, 2], seed + 1013),
                        _lattice_hash(cell[:, 0], cell[:, 1], cell[:, 2], seed + 2027),
                    )
                )
                delta = scaled - feature
                distance_squared = np.einsum("ij,ij->i", delta, delta)
                becomes_nearest = distance_squared < nearest_distance_squared
                previous_nearest_distance = nearest_distance_squared.copy()
                previous_nearest_delta = nearest_delta.copy()
                nearest_distance_squared = np.where(
                    becomes_nearest,
                    distance_squared,
                    nearest_distance_squared,
                )
                nearest_delta = np.where(becomes_nearest[:, None], delta, nearest_delta)
                second_distance_squared = np.where(
                    becomes_nearest,
                    previous_nearest_distance,
                    second_distance_squared,
                )
                second_delta = np.where(
                    becomes_nearest[:, None],
                    previous_nearest_delta,
                    second_delta,
                )
                becomes_second = (~becomes_nearest) & (distance_squared < second_distance_squared)
                second_distance_squared = np.where(
                    becomes_second,
                    distance_squared,
                    second_distance_squared,
                )
                second_delta = np.where(becomes_second[:, None], delta, second_delta)

    nearest_distance = np.sqrt(nearest_distance_squared)
    second_distance = np.sqrt(second_distance_squared)
    edge_distance = second_distance - nearest_distance
    normalized = np.clip(edge_distance / crease_width, 0.0, 1.0)
    values = normalized * normalized * (3.0 - 2.0 * normalized)
    derivative = np.where(
        (edge_distance > 0.0) & (edge_distance < crease_width),
        6.0 * normalized * (1.0 - normalized) / crease_width,
        0.0,
    )
    nearest_direction = nearest_delta / np.maximum(nearest_distance[:, None], 1e-12)
    second_direction = second_delta / np.maximum(second_distance[:, None], 1e-12)
    gradients_rotated = derivative[:, None] * (second_direction - nearest_direction)
    gradients = gradients_rotated @ rotation * frequency
    return values, gradients


def _histogram_percentile(counts: np.ndarray, edges: np.ndarray, percentile: float) -> float:
    cumulative = np.cumsum(counts)
    if not len(cumulative) or cumulative[-1] == 0:
        return 0.0
    threshold = percentile * cumulative[-1]
    index = min(int(np.searchsorted(cumulative, threshold, side="left")), len(edges) - 2)
    return float(edges[index + 1])


def build_normal_atlas(
    uv_triangles: np.ndarray,
    position_triangles: np.ndarray,
    tangent_triangles: np.ndarray,
    bitangent_triangles: np.ndarray,
    *,
    bounds_min: np.ndarray,
    bounds_max: np.ndarray,
    resolution: int,
    strength: float,
) -> tuple[np.ndarray, dict[str, float | int]]:
    """Rasterize deterministic object-space relief into clean-UV tangent normals."""
    uv_triangles = np.asarray(uv_triangles, dtype=np.float64)
    position_triangles = np.asarray(position_triangles, dtype=np.float64)
    tangent_triangles = np.asarray(tangent_triangles, dtype=np.float64)
    bitangent_triangles = np.asarray(bitangent_triangles, dtype=np.float64)
    expected_3d_shape = (len(uv_triangles), 3, 3)
    if uv_triangles.shape[1:] != (3, 2):
        raise ValueError("uv triangles must have shape (triangle_count, 3, 2)")
    for name, values in (
        ("positions", position_triangles),
        ("tangents", tangent_triangles),
        ("bitangents", bitangent_triangles),
    ):
        if values.shape != expected_3d_shape:
            raise ValueError(f"{name} must have shape {expected_3d_shape}")

    rgba = np.zeros((resolution, resolution, 4), dtype=np.uint8)
    angle_edges = np.linspace(0.0, 90.0, 901)
    angle_counts = np.zeros(len(angle_edges) - 1, dtype=np.int64)
    maximum_angle = 0.0
    maximum_unit_error = 0.0
    overlap_pixels = 0
    batch_rows: list[np.ndarray] = []
    batch_columns: list[np.ndarray] = []
    batch_points: list[np.ndarray] = []
    batch_tangents: list[np.ndarray] = []
    batch_bitangents: list[np.ndarray] = []
    batched_pixels = 0

    def flush_batch() -> None:
        nonlocal maximum_angle, maximum_unit_error, overlap_pixels, batched_pixels
        if not batched_pixels:
            return
        rows = np.concatenate(batch_rows)
        columns = np.concatenate(batch_columns)
        points = np.concatenate(batch_points)
        interpolated_tangents = np.concatenate(batch_tangents)
        interpolated_bitangents = np.concatenate(batch_bitangents)
        interpolated_tangents /= np.linalg.norm(interpolated_tangents, axis=1, keepdims=True)
        interpolated_bitangents /= np.linalg.norm(
            interpolated_bitangents, axis=1, keepdims=True
        )
        _, gradients = cindermaw_height_gradient(points, bounds_min, bounds_max)
        normals = tangent_normals_from_object_gradient(
            gradients,
            interpolated_tangents,
            interpolated_bitangents,
            strength=strength,
        )
        lengths = np.linalg.norm(normals, axis=1)
        maximum_unit_error = max(maximum_unit_error, float(np.max(np.abs(lengths - 1.0))))
        angles = np.degrees(np.arccos(np.clip(normals[:, 2], -1.0, 1.0)))
        angle_counts[:] += np.histogram(angles, bins=angle_edges)[0]
        maximum_angle = max(maximum_angle, float(np.max(angles)))
        encoded = np.rint((normals * 0.5 + 0.5) * 255.0).clip(0, 255).astype(np.uint8)
        overlap_pixels += int(np.count_nonzero(rgba[rows, columns, 3]))
        rgba[rows, columns, :3] = encoded
        rgba[rows, columns, 3] = 255
        batch_rows.clear()
        batch_columns.clear()
        batch_points.clear()
        batch_tangents.clear()
        batch_bitangents.clear()
        batched_pixels = 0

    for uv, positions, tangents, bitangents in zip(
        uv_triangles,
        position_triangles,
        tangent_triangles,
        bitangent_triangles,
        strict=True,
    ):
        rows, columns, barycentric = triangle_pixel_samples(uv, resolution=resolution)
        if not len(rows):
            continue
        batch_rows.append(rows)
        batch_columns.append(columns)
        batch_points.append(barycentric @ positions)
        batch_tangents.append(barycentric @ tangents)
        batch_bitangents.append(barycentric @ bitangents)
        batched_pixels += len(rows)
        if batched_pixels >= 262_144:
            flush_batch()

    flush_batch()

    covered_pixels = int(np.count_nonzero(rgba[:, :, 3]))
    metrics: dict[str, float | int] = {
        "resolution": resolution,
        "coveredPixels": covered_pixels,
        "coverageFraction": covered_pixels / float(resolution * resolution),
        "overlapPixelWrites": overlap_pixels,
        "angularP50Degrees": _histogram_percentile(angle_counts, angle_edges, 0.50),
        "angularP95Degrees": _histogram_percentile(angle_counts, angle_edges, 0.95),
        "angularMaxDegrees": maximum_angle,
        "unitLengthMaxError": maximum_unit_error,
    }
    return rgba, metrics


def triangle_pixel_samples(
    uv: np.ndarray,
    *,
    resolution: int,
) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Return covered atlas texel centers and barycentrics for one UV triangle."""
    coordinates = np.asarray(uv, dtype=np.float64)
    if coordinates.shape != (3, 2):
        raise ValueError("uv must have shape (3, 2)")
    if resolution <= 0:
        raise ValueError("resolution must be positive")

    pixel_vertices = np.column_stack(
        (coordinates[:, 0] * resolution, (1.0 - coordinates[:, 1]) * resolution)
    )
    minimum = np.ceil(pixel_vertices.min(axis=0) - 0.5).astype(int)
    maximum = np.floor(pixel_vertices.max(axis=0) - 0.5).astype(int)
    minimum = np.maximum(minimum, 0)
    maximum = np.minimum(maximum, resolution - 1)
    if np.any(maximum < minimum):
        empty_int = np.empty(0, dtype=np.int64)
        return empty_int, empty_int.copy(), np.empty((0, 3), dtype=np.float64)

    columns, rows = np.meshgrid(
        np.arange(minimum[0], maximum[0] + 1),
        np.arange(minimum[1], maximum[1] + 1),
    )
    samples = np.column_stack((columns.ravel() + 0.5, rows.ravel() + 0.5))
    basis = np.column_stack(
        (pixel_vertices[1] - pixel_vertices[0], pixel_vertices[2] - pixel_vertices[0])
    )
    determinant = np.linalg.det(basis)
    if abs(determinant) <= 1e-14:
        empty_int = np.empty(0, dtype=np.int64)
        return empty_int, empty_int.copy(), np.empty((0, 3), dtype=np.float64)
    weights_12 = (np.linalg.inv(basis) @ (samples - pixel_vertices[0]).T).T
    barycentric = np.column_stack(
        (1.0 - weights_12[:, 0] - weights_12[:, 1], weights_12)
    )
    inside = np.all(barycentric >= -1e-10, axis=1)
    return rows.ravel()[inside], columns.ravel()[inside], barycentric[inside]


def _smoothstep_with_derivative(values: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    clipped = np.clip(values, 0.0, 1.0)
    smoothed = clipped * clipped * (3.0 - 2.0 * clipped)
    derivative = np.where(
        (values > 0.0) & (values < 1.0),
        6.0 * clipped * (1.0 - clipped),
        0.0,
    )
    return smoothed, derivative


def anatomical_detail_strength(
    points: np.ndarray,
    bounds_min: Sequence[float],
    bounds_max: Sequence[float],
) -> tuple[np.ndarray, np.ndarray]:
    """Return anatomy-aware relief strength and its object-space gradient."""
    points = np.asarray(points, dtype=np.float64)
    minimum = np.asarray(bounds_min, dtype=np.float64)
    maximum = np.asarray(bounds_max, dtype=np.float64)
    span = np.maximum(maximum - minimum, 1e-9)

    longitudinal = (points[:, 1] - minimum[1]) / span[1]
    head_argument = (longitudinal - 0.08) / 0.25
    head_curve, head_curve_derivative = _smoothstep_with_derivative(head_argument)
    head_strength = 0.65 + 0.35 * head_curve
    head_derivative_y = 0.35 * head_curve_derivative / (0.25 * span[1])

    tail_argument = (longitudinal - 0.72) / 0.28
    tail_curve, tail_curve_derivative = _smoothstep_with_derivative(tail_argument)
    tail_strength = 1.0 - 0.28 * tail_curve
    tail_derivative_y = -0.28 * tail_curve_derivative / (0.28 * span[1])

    vertical = (points[:, 2] - minimum[2]) / span[2]
    dorsal_argument = (vertical - 0.15) / 0.80
    dorsal_curve, dorsal_curve_derivative = _smoothstep_with_derivative(dorsal_argument)
    dorsal_strength = 0.45 + 0.55 * dorsal_curve
    dorsal_derivative_z = 0.55 * dorsal_curve_derivative / (0.80 * span[2])

    strength = head_strength * tail_strength * dorsal_strength
    gradients = np.zeros_like(points)
    gradients[:, 1] = (
        (head_derivative_y * tail_strength + head_strength * tail_derivative_y)
        * dorsal_strength
    )
    gradients[:, 2] = head_strength * tail_strength * dorsal_derivative_z
    return strength, gradients


def cindermaw_height_gradient(
    points: np.ndarray,
    bounds_min: np.ndarray,
    bounds_max: np.ndarray,
) -> tuple[np.ndarray, np.ndarray]:
    """Return anatomy-aware plates, pebbles, and pores with a continuous gradient."""
    span = np.asarray(bounds_max, dtype=np.float64) - np.asarray(bounds_min, dtype=np.float64)
    if np.any(span <= 0.0):
        raise ValueError("mesh bounds must have positive extent on every axis")

    center = (np.asarray(bounds_min, dtype=np.float64) + np.asarray(bounds_max, dtype=np.float64)) * 0.5
    physical = np.asarray(points, dtype=np.float64) - center
    normalized = physical / span
    mirrored = physical.copy()
    mirrored[:, 2] = np.abs(mirrored[:, 2])

    plate_height, plate_gradients = cellular_plate_with_gradient(
        mirrored,
        frequency=7.0,
        seed=9173,
        rotation=_euler_rotation(-11.0, 23.0, 37.0),
        crease_width=0.28,
    )
    pebble_height, pebble_gradients = cellular_pebble_with_gradient(
        mirrored,
        frequency=18.0,
        seed=4317,
        rotation=_euler_rotation(13.0, -27.0, 41.0),
    )
    heights = 0.55 * plate_height + 0.30 * pebble_height
    physical_gradients = 0.55 * plate_gradients + 0.30 * pebble_gradients
    for frequency, seed, weight, rotation in relief_octaves():
        rotated = mirrored @ rotation.T
        values, rotated_gradients = value_noise_with_gradient(rotated, frequency, seed=seed)
        shaped = values * values * (3.0 - 2.0 * values)
        shaped_derivative = 6.0 * values * (1.0 - values)
        heights += weight * shaped
        physical_gradients += (
            weight * shaped_derivative[:, None] * (rotated_gradients @ rotation)
        )

    z_sign = np.where(normalized[:, 2] < 0.0, -1.0, 1.0)
    physical_gradients[:, 2] *= z_sign

    anatomy_strength, anatomy_gradients = anatomical_detail_strength(
        points,
        bounds_min,
        bounds_max,
    )
    gradients = physical_gradients * anatomy_strength[:, None]
    gradients += heights[:, None] * anatomy_gradients
    return heights * anatomy_strength, gradients


def tangent_normals_from_object_gradient(
    gradients: np.ndarray,
    tangents: np.ndarray,
    bitangents: np.ndarray,
    *,
    strength: float,
) -> np.ndarray:
    """Project object-space height gradients into a tangent-space normal."""
    tangent_basis = np.asarray(tangents, dtype=np.float64)
    tangent_length = np.linalg.norm(tangent_basis, axis=1, keepdims=True)
    if np.any(tangent_length <= 1e-12):
        raise ValueError("tangent basis contains a zero-length tangent")
    tangent_basis = tangent_basis / tangent_length

    bitangent_basis = np.asarray(bitangents, dtype=np.float64)
    bitangent_basis = bitangent_basis - (
        np.sum(bitangent_basis * tangent_basis, axis=1, keepdims=True) * tangent_basis
    )
    bitangent_length = np.linalg.norm(bitangent_basis, axis=1, keepdims=True)
    if np.any(bitangent_length <= 1e-12):
        raise ValueError("tangent basis contains a degenerate bitangent")
    bitangent_basis = bitangent_basis / bitangent_length

    tangent_slopes = np.sum(gradients * tangent_basis, axis=1)
    bitangent_slopes = np.sum(gradients * bitangent_basis, axis=1)
    normals = np.column_stack(
        (-strength * tangent_slopes, -strength * bitangent_slopes, np.ones(len(gradients)))
    )
    return normals / np.linalg.norm(normals, axis=1, keepdims=True)


def _lattice_hash(x: np.ndarray, y: np.ndarray, z: np.ndarray, seed: int) -> np.ndarray:
    value = (
        x.astype(np.int64) * 374_761_393
        + y.astype(np.int64) * 668_265_263
        + z.astype(np.int64) * 2_147_483_647
        + np.int64(seed) * 1_274_126_177
    )
    value = np.bitwise_and(value, np.int64(0xFFFFFFFF))
    value = np.bitwise_xor(value, np.right_shift(value, 13))
    value = np.bitwise_and(value * np.int64(1_274_126_177), np.int64(0xFFFFFFFF))
    value = np.bitwise_xor(value, np.right_shift(value, 16))
    return value.astype(np.float64) / float(0xFFFFFFFF)


def value_noise_with_gradient(
    points: np.ndarray,
    frequency: np.ndarray | Sequence[float] | float,
    *,
    seed: int,
) -> tuple[np.ndarray, np.ndarray]:
    """Return smooth deterministic 3D value noise and its world-space gradient."""
    coordinates = np.asarray(points, dtype=np.float64)
    frequencies = np.broadcast_to(np.asarray(frequency, dtype=np.float64), (3,))
    scaled = coordinates * frequencies
    base = np.floor(scaled).astype(np.int64)
    fraction = scaled - base
    weight = fraction * fraction * (3.0 - 2.0 * fraction)
    weight_derivative = 6.0 * fraction * (1.0 - fraction)

    corner = np.empty((len(coordinates), 2, 2, 2), dtype=np.float64)
    for offset_x in range(2):
        for offset_y in range(2):
            for offset_z in range(2):
                corner[:, offset_x, offset_y, offset_z] = _lattice_hash(
                    base[:, 0] + offset_x,
                    base[:, 1] + offset_y,
                    base[:, 2] + offset_z,
                    seed,
                )

    wx, wy, wz = weight[:, 0], weight[:, 1], weight[:, 2]
    x00 = corner[:, 0, 0, 0] * (1.0 - wx) + corner[:, 1, 0, 0] * wx
    x10 = corner[:, 0, 1, 0] * (1.0 - wx) + corner[:, 1, 1, 0] * wx
    x01 = corner[:, 0, 0, 1] * (1.0 - wx) + corner[:, 1, 0, 1] * wx
    x11 = corner[:, 0, 1, 1] * (1.0 - wx) + corner[:, 1, 1, 1] * wx
    y0 = x00 * (1.0 - wy) + x10 * wy
    y1 = x01 * (1.0 - wy) + x11 * wy
    values = y0 * (1.0 - wz) + y1 * wz

    dx00 = corner[:, 1, 0, 0] - corner[:, 0, 0, 0]
    dx10 = corner[:, 1, 1, 0] - corner[:, 0, 1, 0]
    dx01 = corner[:, 1, 0, 1] - corner[:, 0, 0, 1]
    dx11 = corner[:, 1, 1, 1] - corner[:, 0, 1, 1]
    derivative_x = (
        (dx00 * (1.0 - wy) + dx10 * wy) * (1.0 - wz)
        + (dx01 * (1.0 - wy) + dx11 * wy) * wz
    ) * weight_derivative[:, 0] * frequencies[0]

    derivative_y = (
        ((x10 - x00) * (1.0 - wz) + (x11 - x01) * wz)
        * weight_derivative[:, 1]
        * frequencies[1]
    )
    derivative_z = (y1 - y0) * weight_derivative[:, 2] * frequencies[2]
    gradients = np.column_stack((derivative_x, derivative_y, derivative_z))
    return values, gradients


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def export_surface_from_blender(
    model_path: Path,
    output_path: Path,
    *,
    portable_model_path: str,
    expected_vertices: int | None = None,
    expected_triangles: int | None = None,
) -> None:
    """Import the clean FBX in Blender and persist its exact tangent surface."""
    import bpy  # type: ignore[import-not-found]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(model_path))
    candidates = [item for item in bpy.context.scene.objects if item.type == "MESH"]
    if not candidates:
        raise ValueError("FBX import produced no mesh objects")
    obj = max(candidates, key=lambda item: len(item.data.polygons))
    mesh = obj.data
    if expected_vertices is not None and len(mesh.vertices) != expected_vertices:
        raise RuntimeError(
            f"vertex count mismatch: expected {expected_vertices}, found {len(mesh.vertices)}"
        )
    if expected_triangles is not None and len(mesh.polygons) != expected_triangles:
        raise RuntimeError(
            f"triangle count mismatch: expected {expected_triangles}, found {len(mesh.polygons)}"
        )
    if any(len(polygon.loop_indices) != 3 for polygon in mesh.polygons):
        raise ValueError("normal-detail source mesh must be triangulated")
    if mesh.uv_layers.active is None or mesh.uv_layers.active.name != "UVMap_Clean":
        active_name = mesh.uv_layers.active.name if mesh.uv_layers.active else None
        raise ValueError(f"expected active UVMap_Clean, found {active_name!r}")
    mesh.calc_tangents(uvmap="UVMap_Clean")
    uv_data = mesh.uv_layers.active.data
    world_matrix = np.asarray(obj.matrix_world, dtype=np.float64)
    if world_matrix.shape != (4, 4):
        raise ValueError("mesh object must expose a 4x4 world transform")
    linear_transform = world_matrix[:3, :3]
    translation = world_matrix[:3, 3]

    triangle_count = len(mesh.polygons)
    uv = np.empty((triangle_count, 3, 2), dtype=np.float32)
    positions = np.empty((triangle_count, 3, 3), dtype=np.float32)
    tangents = np.empty((triangle_count, 3, 3), dtype=np.float32)
    bitangents = np.empty((triangle_count, 3, 3), dtype=np.float32)
    for triangle_index, polygon in enumerate(mesh.polygons):
        for corner_index, loop_index in enumerate(polygon.loop_indices):
            loop = mesh.loops[loop_index]
            uv[triangle_index, corner_index] = tuple(uv_data[loop_index].uv)
            local_position = np.asarray(mesh.vertices[loop.vertex_index].co, dtype=np.float64)
            world_position = linear_transform @ local_position + translation
            world_tangent = linear_transform @ np.asarray(loop.tangent, dtype=np.float64)
            world_bitangent = linear_transform @ np.asarray(loop.bitangent, dtype=np.float64)
            positions[triangle_index, corner_index] = world_position
            tangents[triangle_index, corner_index] = world_tangent / np.linalg.norm(world_tangent)
            bitangents[triangle_index, corner_index] = world_bitangent / np.linalg.norm(
                world_bitangent
            )

    bounds_min = positions.reshape(-1, 3).min(axis=0)
    bounds_max = positions.reshape(-1, 3).max(axis=0)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    np.savez_compressed(
        output_path,
        uv=uv,
        positions=positions,
        tangents=tangents,
        bitangents=bitangents,
        bounds_min=bounds_min,
        bounds_max=bounds_max,
        model_path=np.array(portable_model_path),
        model_sha256=np.array(_sha256(model_path)),
        object_name=np.array(obj.name),
        object_matrix=world_matrix,
    )


def _build_from_surface(args: argparse.Namespace) -> int:
    from PIL import Image

    with np.load(args.surface, allow_pickle=False) as surface:
        bounds_min = np.asarray(surface["bounds_min"], dtype=np.float64)
        bounds_max = np.asarray(surface["bounds_max"], dtype=np.float64)
        bounds_span = bounds_max - bounds_min
        if int(np.argmax(bounds_span)) != 1:
            raise ValueError(
                "Cindermaw surface coordinate frame is invalid: world Y must be longitudinal"
            )
        rgba, metrics = build_normal_atlas(
            surface["uv"],
            surface["positions"],
            surface["tangents"],
            surface["bitangents"],
            bounds_min=bounds_min,
            bounds_max=bounds_max,
            resolution=args.resolution,
            strength=args.strength,
        )
        model_path = str(surface["model_path"].item())
        model_sha256 = str(surface["model_sha256"].item())
        object_name = str(surface["object_name"].item())

    if metrics["coveredPixels"] <= 0:
        raise ValueError("surface archive produced no covered texels")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    gutter_radius = 2.0
    output_pixels = bounded_normal_gutter(rgba, radius=gutter_radius)
    Image.fromarray(output_pixels, mode="RGB").save(
        args.output,
        format="PNG",
        optimize=False,
    )

    status = "PASS"
    if not 2.0 <= float(metrics["angularP95Degrees"]) <= 30.0:
        status = "REJECTED"
    report = {
        "status": status,
        "method": "object_space_procedural_height_to_clean_uv_tangent_normal_v001",
        "authoredNormalDetail": True,
        "runtimeVfxSeparate": True,
        "orientation": "OpenGL +Y",
        "modelPath": model_path,
        "modelSha256": model_sha256,
        "objectName": object_name,
        "outputPath": args.output.as_posix(),
        "outputSha256": _sha256(args.output),
        "dimensions": [args.resolution, args.resolution],
        "strength": args.strength,
        "gutterRadiusPixels": gutter_radius,
        "atlasBackground": "neutral_tangent",
        "coordinateFrame": {
            "lateralAxis": "world X",
            "longitudinalAxis": "world Y",
            "dorsalAxis": "world Z",
            "boundsMin": bounds_min.tolist(),
            "boundsMax": bounds_max.tolist(),
            "span": bounds_span.tolist(),
        },
        "metrics": metrics,
    }
    args.metrics.parent.mkdir(parents=True, exist_ok=True)
    args.metrics.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return 0 if status == "PASS" else 1


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)
    build = subparsers.add_parser("build")
    build.add_argument("--surface", type=Path, required=True)
    build.add_argument("--output", type=Path, required=True)
    build.add_argument("--metrics", type=Path, required=True)
    build.add_argument("--resolution", type=int, default=4096)
    build.add_argument("--strength", type=float, default=0.010)
    export = subparsers.add_parser("export-surface")
    export.add_argument("--model", type=Path, required=True)
    export.add_argument("--output", type=Path, required=True)
    export.add_argument("--portable-model-path", required=True)
    export.add_argument("--expected-vertices", type=int, default=27_690)
    export.add_argument("--expected-triangles", type=int, default=55_334)
    args = parser.parse_args(argv)
    if args.command == "build":
        return _build_from_surface(args)
    if args.command == "export-surface":
        export_surface_from_blender(
            args.model,
            args.output,
            portable_model_path=args.portable_model_path,
            expected_vertices=args.expected_vertices,
            expected_triangles=args.expected_triangles,
        )
        return 0
    raise AssertionError(f"unhandled command {args.command}")


if __name__ == "__main__":
    script_arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else None
    raise SystemExit(main(script_arguments))
