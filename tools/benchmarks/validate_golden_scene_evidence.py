#!/usr/bin/env python3
"""Fail-closed validation for AnotherLife golden-scene result packages."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence

SOURCE_MANIFEST_ID = "al.postmvp.graphics_benchmark_sources.2026-08-25"
RENDER_PIPELINE = "Built-in Render Pipeline"
SCENE_IDS = ("GS-01", "GS-02", "GS-03", "GS-04", "GS-05")
REQUIRED_FILES = (
    "runtime-identity.json",
    "telemetry.json",
    "capture-manifest.json",
    "scorecard.json",
    "scorecard.md",
    "benchmark-result.json",
)
REQUIRED_ARTIFACTS = {
    "still": ("image/png", ".png"),
    "video": ("video/mp4", ".mp4"),
    "profiler": ("application/vnd.unity.profiler", ".raw"),
    "telemetry": ("application/json", ".json"),
}
REQUIRED_AGGREGATES = (
    "frame.delivered_time",
    "frame.cpu_time",
    "frame.gpu_time",
    "memory.system_used",
    "memory.unity_used",
    "memory.graphics_used",
    "allocation.managed_in_frame",
    "allocation.native_count",
    "allocation.gc_collection_count",
    "render.draw_calls",
    "render.batches",
    "render.triangles",
    "render.vertices",
    "render.active_renderers",
    "streaming.texture_requests",
    "streaming.texture_bytes",
    "streaming.asset_stall_time",
    "streaming.shader_compilation_events",
    "lod.active_groups",
    "lod.transitions",
    "density.vfx_sources",
    "density.particles",
    "density.actors_full",
    "density.actors_fallback",
    "density.actors_nameplate",
    "quality.render_scale",
    "quality.lod_bias",
    "quality.vfx_density",
)
REQUIRED_DEVICE_CAPABILITIES = (
    "device.battery_level",
    "device.temperature",
    "device.thermal_state",
)
IDENTITY_REQUIRED = (
    "schemaVersion",
    "configurationFingerprint",
    "catalogFingerprint",
    "buildId",
    "sourceCommit",
    "unityVersion",
    "platform",
    "isPlayerBuild",
    "isEditor",
    "deviceModel",
    "operatingSystem",
    "processorType",
    "graphicsDeviceName",
    "systemMemoryMb",
    "graphicsMemoryMb",
    "graphicsApi",
    "deviceIdentityHash",
    "sceneId",
    "sceneRevision",
    "scenarioId",
    "unitySceneId",
    "unitySceneName",
    "seed",
    "anchorId",
    "anchorPosition",
    "anchorEulerAngles",
    "projection",
    "fieldOfViewDegrees",
    "orthographicSize",
    "nearClipMeters",
    "farClipMeters",
    "qualityPresetId",
    "qualityPresetRevision",
    "targetFrameRate",
    "renderScale",
    "shadowDistanceMeters",
    "lodBias",
    "textureMipmapLimit",
    "pixelLightCount",
    "vfxDensity",
    "runId",
    "captureId",
    "capturedAtUtc",
    "operator",
    "captureTool",
    "captureToolVersion",
    "durationSeconds",
)
REPEAT_IDENTITY_KEYS = (
    "configurationFingerprint",
    "catalogFingerprint",
    "buildId",
    "sourceCommit",
    "unityVersion",
    "platform",
    "deviceIdentityHash",
    "sceneId",
    "sceneRevision",
    "scenarioId",
    "unitySceneId",
    "unitySceneName",
    "seed",
    "anchorId",
    "anchorPosition",
    "anchorEulerAngles",
    "projection",
    "fieldOfViewDegrees",
    "orthographicSize",
    "nearClipMeters",
    "farClipMeters",
    "qualityPresetId",
    "qualityPresetRevision",
    "targetFrameRate",
    "renderScale",
    "shadowDistanceMeters",
    "lodBias",
    "textureMipmapLimit",
    "pixelLightCount",
    "vfxDensity",
    "durationSeconds",
)


@dataclass(frozen=True)
class ValidatedPackage:
    path: Path
    identity: dict[str, Any]
    telemetry: dict[str, Any]
    manifest: dict[str, Any]
    result: dict[str, Any]

    @property
    def scene_id(self) -> str:
        return str(self.identity.get("sceneId", ""))


def _is_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def _is_lower_hex(value: Any, lengths: Sequence[int]) -> bool:
    return (
        isinstance(value, str)
        and len(value) in lengths
        and all(character in "0123456789abcdef" for character in value)
    )


def _load_json(path: Path, errors: list[str]) -> dict[str, Any]:
    if not path.is_file() or path.stat().st_size <= 0:
        errors.append(f"{path}: missing or empty JSON file")
        return {}
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exception:
        errors.append(f"{path}: invalid JSON: {exception}")
        return {}
    if not isinstance(value, dict):
        errors.append(f"{path}: JSON root must be an object")
        return {}
    return value


def _require_fields(document: dict[str, Any], fields: Iterable[str], label: str, errors: list[str]) -> None:
    for field in fields:
        if field not in document or document[field] is None or document[field] == "":
            errors.append(f"{label}: required field is missing: {field}")


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _png_dimensions(path: Path) -> tuple[int, int] | None:
    try:
        with path.open("rb") as stream:
            header = stream.read(24)
    except OSError:
        return None
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        return None
    return struct.unpack(">II", header[16:24])


def _nearest_rank(values: list[float], percentile: float) -> float:
    ordered = sorted(values)
    rank = math.ceil(percentile * len(ordered))
    return ordered[max(0, min(len(ordered) - 1, rank - 1))]


def _raw_metric_values(telemetry: dict[str, Any], metric_id: str) -> list[float]:
    values: list[float] = []
    field_by_metric = {
        "frame.delivered_time": "deliveredFrameTimeMs",
        "frame.cpu_time": "cpuFrameTimeMs",
        "frame.gpu_time": "gpuFrameTimeMs",
    }
    for sample in telemetry.get("rawSamples", []):
        if not isinstance(sample, dict) or sample.get("interval") != "measured":
            continue
        if metric_id in field_by_metric:
            value = sample.get(field_by_metric[metric_id])
        else:
            counters = sample.get("counters", {})
            value = counters.get(metric_id) if isinstance(counters, dict) else None
        if _is_number(value) and value >= 0:
            values.append(float(value))
    return values


def _validate_distribution(
    metric_id: str,
    aggregate: Any,
    raw_values: list[float],
    label: str,
    errors: list[str],
) -> None:
    if not isinstance(aggregate, dict):
        errors.append(f"{label}: aggregate {metric_id} must be an object")
        return
    required = ("unit", "percentileMethod", "sampleCount", "minimum", "p50", "p90", "p95", "p99", "maximum")
    _require_fields(aggregate, required, f"{label}.aggregates.{metric_id}", errors)
    if aggregate.get("percentileMethod") != "nearest-rank":
        errors.append(f"{label}: aggregate {metric_id} uses an unsupported percentile method")
    sample_count = aggregate.get("sampleCount")
    if not isinstance(sample_count, int) or isinstance(sample_count, bool) or sample_count <= 0:
        errors.append(f"{label}: aggregate {metric_id} sampleCount must be a positive integer")
        return
    names = ("minimum", "p50", "p90", "p95", "p99", "maximum")
    numbers = [aggregate.get(name) for name in names]
    if any(not _is_number(value) or value < 0 for value in numbers):
        errors.append(f"{label}: aggregate {metric_id} percentiles must be finite and non-negative")
        return
    if numbers != sorted(numbers):
        errors.append(f"{label}: aggregate {metric_id} percentiles are not monotonic")
    if not raw_values:
        errors.append(f"{label}: aggregate {metric_id} has no linked measured raw samples")
        return
    if sample_count != len(raw_values):
        errors.append(
            f"{label}: aggregate {metric_id} sampleCount {sample_count} does not match raw count {len(raw_values)}"
        )
        return
    expected = (
        min(raw_values),
        _nearest_rank(raw_values, 0.50),
        _nearest_rank(raw_values, 0.90),
        _nearest_rank(raw_values, 0.95),
        _nearest_rank(raw_values, 0.99),
        max(raw_values),
    )
    for name, actual, wanted in zip(names, numbers, expected):
        if not math.isclose(float(actual), wanted, rel_tol=1e-9, abs_tol=1e-9):
            errors.append(f"{label}: aggregate {metric_id}.{name} does not reproduce from raw samples")


def _validate_identity(identity: dict[str, Any], label: str, errors: list[str]) -> None:
    _require_fields(identity, IDENTITY_REQUIRED, label, errors)
    if identity.get("schemaVersion") != "1.0.0":
        errors.append(f"{label}: unsupported identity schemaVersion")
    if identity.get("sceneId") not in SCENE_IDS:
        errors.append(f"{label}: unknown golden scene")
    if identity.get("isPlayerBuild") is not True or identity.get("isEditor") is not False:
        errors.append(f"{label}: Editor-only or non-Player evidence cannot certify a target platform")
    if not _is_lower_hex(identity.get("sourceCommit"), (40, 64)):
        errors.append(f"{label}: sourceCommit must be canonical lowercase hex")
    for field in ("configurationFingerprint", "catalogFingerprint", "deviceIdentityHash"):
        if not _is_lower_hex(identity.get(field), (64,)):
            errors.append(f"{label}: {field} must be a canonical SHA-256")
    for field in ("anchorPosition", "anchorEulerAngles"):
        value = identity.get(field)
        if not isinstance(value, list) or len(value) != 3 or any(not _is_number(item) for item in value):
            errors.append(f"{label}: {field} must contain three finite numbers")
    for field in ("systemMemoryMb", "targetFrameRate", "durationSeconds"):
        value = identity.get(field)
        if not _is_number(value) or value <= 0:
            errors.append(f"{label}: {field} must be positive")


def _validate_telemetry(telemetry: dict[str, Any], label: str, errors: list[str]) -> None:
    required = (
        "schemaVersion",
        "collectionStartedAtUtc",
        "collectionEndedAtUtc",
        "actualDurationSeconds",
        "warmupSeconds",
        "measurementSeconds",
        "targetFrameRate",
        "isPlayerBuild",
        "warmupSampleCount",
        "measuredSampleCount",
        "deviceStart",
        "deviceEnd",
        "deviceSamples",
        "framePacing",
        "hitches",
        "capabilities",
        "aggregates",
        "rawSamples",
    )
    _require_fields(telemetry, required, label, errors)
    if telemetry.get("schemaVersion") != "1.0.0" or telemetry.get("isPlayerBuild") is not True:
        errors.append(f"{label}: telemetry is not Player-build schema 1.0.0 evidence")
    raw_samples = telemetry.get("rawSamples")
    if not isinstance(raw_samples, list) or not raw_samples:
        errors.append(f"{label}: rawSamples are missing")
        raw_samples = []
    sequences: list[int] = []
    elapsed: list[float] = []
    measured_count = 0
    warmup_count = 0
    for index, sample in enumerate(raw_samples):
        if not isinstance(sample, dict):
            errors.append(f"{label}: rawSamples[{index}] must be an object")
            continue
        sequence = sample.get("sequence")
        elapsed_value = sample.get("elapsedSeconds")
        interval = sample.get("interval")
        if not isinstance(sequence, int) or isinstance(sequence, bool):
            errors.append(f"{label}: rawSamples[{index}].sequence must be an integer")
        else:
            sequences.append(sequence)
        if not _is_number(elapsed_value) or elapsed_value < 0:
            errors.append(f"{label}: rawSamples[{index}].elapsedSeconds is invalid")
        else:
            elapsed.append(float(elapsed_value))
        if interval == "measured":
            measured_count += 1
        elif interval == "warmup":
            warmup_count += 1
        else:
            errors.append(f"{label}: rawSamples[{index}].interval is invalid")
    if sequences and sequences != sorted(set(sequences)):
        errors.append(f"{label}: raw sample sequences are not strictly increasing")
    if elapsed and elapsed != sorted(elapsed):
        errors.append(f"{label}: raw sample elapsed times decrease")
    if telemetry.get("measuredSampleCount") != measured_count or telemetry.get("warmupSampleCount") != warmup_count:
        errors.append(f"{label}: raw sample interval counts do not match summary counts")
    actual = telemetry.get("actualDurationSeconds")
    warmup = telemetry.get("warmupSeconds")
    measurement = telemetry.get("measurementSeconds")
    if any(not _is_number(value) or value < 0 for value in (actual, warmup, measurement)):
        errors.append(f"{label}: telemetry durations must be finite and non-negative")
    elif measurement <= 0 or actual + 1e-9 < warmup + measurement:
        errors.append(f"{label}: telemetry duration does not cover warmup plus measurement")

    aggregates = telemetry.get("aggregates")
    if not isinstance(aggregates, dict):
        errors.append(f"{label}: aggregates must be an object")
        aggregates = {}
    for metric_id in REQUIRED_AGGREGATES:
        if metric_id not in aggregates:
            errors.append(f"{label}: required aggregate is missing: {metric_id}")
            continue
        _validate_distribution(metric_id, aggregates[metric_id], _raw_metric_values(telemetry, metric_id), label, errors)

    capabilities = telemetry.get("capabilities")
    if not isinstance(capabilities, list):
        errors.append(f"{label}: capabilities must be an array")
        capabilities = []
    capability_map: dict[str, dict[str, Any]] = {}
    for capability in capabilities:
        if isinstance(capability, dict) and isinstance(capability.get("metricId"), str):
            if capability["metricId"] in capability_map:
                errors.append(f"{label}: duplicate capability: {capability['metricId']}")
            capability_map[capability["metricId"]] = capability
    for metric_id in (*REQUIRED_AGGREGATES, *REQUIRED_DEVICE_CAPABILITIES):
        capability = capability_map.get(metric_id)
        if capability is None:
            errors.append(f"{label}: required capability is missing: {metric_id}")
        elif capability.get("status") != "supported" or not isinstance(capability.get("sampleCount"), int) or capability["sampleCount"] <= 0:
            errors.append(f"{label}: required capability is not supported with samples: {metric_id}")


def _validate_manifest_and_artifacts(
    package: Path,
    identity: dict[str, Any],
    manifest: dict[str, Any],
    result: dict[str, Any],
    label: str,
    errors: list[str],
) -> None:
    required = (
        "schemaVersion",
        "runId",
        "captureId",
        "sceneId",
        "seed",
        "anchorId",
        "configurationFingerprint",
        "captureStartedAtUtc",
        "captureEndedAtUtc",
        "captureDurationSeconds",
        "isComplete",
        "hasAllRequiredArtifacts",
        "durationRequirementMet",
        "requiredVideoFrameCount",
        "videoFrameRequirementMet",
        "sourceManifestId",
        "thirdPartyMediaIncluded",
        "identity",
        "mediaSettings",
        "anchorConsistency",
        "artifacts",
    )
    _require_fields(manifest, required, label, errors)
    for field in ("runId", "captureId", "sceneId", "seed", "anchorId", "configurationFingerprint"):
        if manifest.get(field) != identity.get(field):
            errors.append(f"{label}: manifest identity linkage mismatch: {field}")
    embedded = manifest.get("identity")
    if not isinstance(embedded, dict):
        errors.append(f"{label}: embedded manifest identity is missing")
    else:
        for field in IDENTITY_REQUIRED:
            if embedded.get(field) != identity.get(field):
                errors.append(f"{label}: embedded manifest identity mismatch: {field}")
    if manifest.get("sourceManifestId") != SOURCE_MANIFEST_ID or manifest.get("thirdPartyMediaIncluded") is not False:
        errors.append(f"{label}: provenance boundary violation")
    if manifest.get("isComplete") is not True or manifest.get("hasAllRequiredArtifacts") is not True:
        errors.append(f"{label}: capture manifest is incomplete")
    if manifest.get("durationRequirementMet") is not True or manifest.get("videoFrameRequirementMet") is not True:
        errors.append(f"{label}: capture duration or video-frame requirement is not met")
    consistency = manifest.get("anchorConsistency")
    if not isinstance(consistency, dict) or consistency.get("isConsistent") is not True or consistency.get("driftFailureCount") != 0:
        errors.append(f"{label}: camera-anchor consistency failed")
    media = manifest.get("mediaSettings")
    if not isinstance(media, dict):
        errors.append(f"{label}: mediaSettings are missing")
        media = {}
    for field in ("width", "height", "videoFrameRate", "videoDurationSeconds"):
        if not _is_number(media.get(field)) or media[field] <= 0:
            errors.append(f"{label}: invalid media setting: {field}")

    artifacts = manifest.get("artifacts")
    if not isinstance(artifacts, list):
        errors.append(f"{label}: artifacts must be an array")
        artifacts = []
    by_kind: dict[str, dict[str, Any]] = {}
    result_references = result.get("artifactReferences")
    if not isinstance(result_references, list):
        errors.append(f"{label}: benchmark result artifactReferences are missing")
        result_references = []
    references = {
        item.get("artifactId"): item
        for item in result_references
        if isinstance(item, dict) and isinstance(item.get("artifactId"), str)
    }
    for artifact in artifacts:
        if not isinstance(artifact, dict) or not isinstance(artifact.get("kind"), str):
            errors.append(f"{label}: malformed artifact record")
            continue
        kind = artifact["kind"]
        if kind in by_kind:
            errors.append(f"{label}: duplicate artifact kind: {kind}")
        by_kind[kind] = artifact
    if set(by_kind) != set(REQUIRED_ARTIFACTS):
        errors.append(f"{label}: required artifact set is incomplete or contains extras")
    for kind, (expected_format, expected_suffix) in REQUIRED_ARTIFACTS.items():
        artifact = by_kind.get(kind)
        if artifact is None:
            continue
        if artifact.get("status") != "captured":
            errors.append(f"{label}: required artifact is not captured: {kind}")
            continue
        for field in ("sceneId", "seed", "anchorId", "runId", "configurationFingerprint"):
            if artifact.get(field) != identity.get(field):
                errors.append(f"{label}: artifact {kind} linkage mismatch: {field}")
        relative = artifact.get("relativePath")
        if not isinstance(relative, str) or Path(relative).name != relative or not relative.endswith(expected_suffix):
            errors.append(f"{label}: artifact {kind} has an unsafe or unexpected path")
            continue
        if artifact.get("format") != expected_format:
            errors.append(f"{label}: artifact {kind} format mismatch")
        path = package / relative
        if not path.is_file() or path.stat().st_size <= 0:
            errors.append(f"{label}: artifact file is missing or empty: {relative}")
            continue
        if artifact.get("byteSize") != path.stat().st_size:
            errors.append(f"{label}: artifact byte size mismatch: {relative}")
        if not _is_lower_hex(artifact.get("sha256"), (64,)) or artifact.get("sha256") != _sha256(path):
            errors.append(f"{label}: artifact hash mismatch: {relative}")
        reference = references.get(kind)
        if not isinstance(reference, dict):
            errors.append(f"{label}: artifact is unlinked from benchmark-result.json: {kind}")
        else:
            for reference_field, artifact_field in (
                ("path", "relativePath"),
                ("status", "status"),
                ("byteLength", "byteSize"),
                ("sha256", "sha256"),
            ):
                if reference.get(reference_field) != artifact.get(artifact_field):
                    errors.append(f"{label}: result reference mismatch for {kind}.{reference_field}")
        if kind == "still":
            dimensions = _png_dimensions(path)
            expected_dimensions = (media.get("width"), media.get("height"))
            if dimensions is None or dimensions != expected_dimensions:
                errors.append(f"{label}: still framing does not match declared media dimensions")


def validate_package(package: Path) -> tuple[ValidatedPackage | None, list[str]]:
    package = package.resolve()
    errors: list[str] = []
    label = str(package)
    if not package.is_dir():
        return None, [f"{package}: package directory does not exist"]
    for name in REQUIRED_FILES:
        path = package / name
        if not path.is_file() or path.stat().st_size <= 0:
            errors.append(f"{label}: required package file is missing or empty: {name}")
    identity = _load_json(package / "runtime-identity.json", errors)
    telemetry = _load_json(package / "telemetry.json", errors)
    manifest = _load_json(package / "capture-manifest.json", errors)
    scorecard = _load_json(package / "scorecard.json", errors)
    result = _load_json(package / "benchmark-result.json", errors)
    _validate_identity(identity, f"{label}/runtime-identity.json", errors)
    _validate_telemetry(telemetry, f"{label}/telemetry.json", errors)

    result_identity = result.get("identity")
    if not isinstance(result_identity, dict):
        errors.append(f"{label}: benchmark result identity is missing")
    else:
        for field in (
            "sceneId",
            "anchorId",
            "qualityId",
            "runId",
            "buildId",
            "sourceCommit",
            "catalogFingerprint",
            "unityVersion",
        ):
            identity_field = "qualityPresetId" if field == "qualityId" else field
            if result_identity.get(field) != identity.get(identity_field):
                errors.append(f"{label}: benchmark result identity mismatch: {field}")
        if result_identity.get("renderPipeline") != RENDER_PIPELINE:
            errors.append(f"{label}: render pipeline changed from the Built-in baseline")
        if not _is_lower_hex(result_identity.get("applicationBuildGuid"), (32,)):
            errors.append(f"{label}: Player Application.buildGUID is invalid")
    provenance = result.get("provenance")
    if not isinstance(provenance, dict) or provenance.get("sourceManifestId") != SOURCE_MANIFEST_ID or provenance.get("thirdPartyMediaIncluded") is not False:
        errors.append(f"{label}: benchmark result provenance boundary violation")
    result_scorecard = result.get("scorecard")
    certification = scorecard.get("certificationStatus")
    if certification != "target-platform-evidence-ready-for-review":
        errors.append(f"{label}: scorecard is not target-platform evidence ready for review")
    if not isinstance(result_scorecard, dict) or result_scorecard.get("certificationStatus") != certification:
        errors.append(f"{label}: benchmark result scorecard linkage mismatch")
    _validate_manifest_and_artifacts(package, identity, manifest, result, label, errors)
    if errors:
        return None, errors
    return ValidatedPackage(package, identity, telemetry, manifest, result), []


def compare_repetitions(packages: Sequence[ValidatedPackage], scene_id: str) -> list[str]:
    matches = [package for package in packages if package.scene_id == scene_id]
    if len(matches) < 2:
        return [f"{scene_id}: at least two valid repetitions are required"]
    baseline = matches[0]
    errors: list[str] = []
    baseline_media = baseline.manifest.get("mediaSettings")
    baseline_metrics = set(baseline.telemetry.get("aggregates", {}))
    baseline_capabilities = {
        (item.get("metricId"), item.get("unit"), item.get("status"), item.get("sampleScope"))
        for item in baseline.telemetry.get("capabilities", [])
        if isinstance(item, dict)
    }
    for candidate in matches[1:]:
        for key in REPEAT_IDENTITY_KEYS:
            if candidate.identity.get(key) != baseline.identity.get(key):
                errors.append(f"{scene_id}: repetition identity/camera/quality mismatch: {key}")
        if candidate.manifest.get("mediaSettings") != baseline_media:
            errors.append(f"{scene_id}: repetition media framing/settings mismatch")
        if set(candidate.telemetry.get("aggregates", {})) != baseline_metrics:
            errors.append(f"{scene_id}: repetition aggregate metric schema mismatch")
        candidate_capabilities = {
            (item.get("metricId"), item.get("unit"), item.get("status"), item.get("sampleScope"))
            for item in candidate.telemetry.get("capabilities", [])
            if isinstance(item, dict)
        }
        if candidate_capabilities != baseline_capabilities:
            errors.append(f"{scene_id}: repetition capability schema mismatch")
    return errors


def discover_packages(paths: Sequence[Path]) -> list[Path]:
    discovered: set[Path] = set()
    for path in paths:
        resolved = path.resolve()
        if (resolved / "benchmark-result.json").is_file():
            discovered.add(resolved)
            continue
        if resolved.is_dir():
            for result in resolved.rglob("benchmark-result.json"):
                discovered.add(result.parent.resolve())
    return sorted(discovered)


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="+", type=Path, help="Package directories or roots containing packages")
    parser.add_argument(
        "--require-scenes",
        default="",
        help="Comma-separated exact scene IDs that must be present",
    )
    parser.add_argument(
        "--require-repeat",
        action="append",
        default=[],
        choices=SCENE_IDS,
        help="Scene requiring at least two comparable valid repetitions; may be repeated",
    )
    arguments = parser.parse_args(argv)
    package_paths = discover_packages(arguments.paths)
    errors: list[str] = []
    validated: list[ValidatedPackage] = []
    if not package_paths:
        errors.append("No benchmark-result.json packages were found")
    for package_path in package_paths:
        package, package_errors = validate_package(package_path)
        errors.extend(package_errors)
        if package is not None:
            validated.append(package)
    required_scenes = [item for item in arguments.require_scenes.split(",") if item]
    invalid_scene_ids = sorted(set(required_scenes) - set(SCENE_IDS))
    if invalid_scene_ids:
        errors.append("Unknown required scene IDs: " + ", ".join(invalid_scene_ids))
    present_scenes = {package.scene_id for package in validated}
    for scene_id in required_scenes:
        if scene_id not in present_scenes:
            errors.append(f"{scene_id}: no valid certifying package was found")
    for scene_id in arguments.require_repeat:
        errors.extend(compare_repetitions(validated, scene_id))
    if errors:
        print("Golden-scene evidence validation FAILED", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1
    print(
        "Golden-scene evidence validation PASSED: "
        f"{len(validated)} package(s), scenes={','.join(sorted(present_scenes))}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
