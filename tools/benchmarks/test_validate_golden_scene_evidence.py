#!/usr/bin/env python3
"""Failure-path fixtures for the golden-scene evidence validator."""

from __future__ import annotations

import copy
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_golden_scene_evidence as validator  # noqa: E402


class GoldenSceneEvidenceValidatorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_accepts_complete_player_packages_and_comparable_repetition(self) -> None:
        first = self._write_package("run-0001")
        second = self._write_package("run-0002")
        first_package, first_errors = validator.validate_package(first)
        second_package, second_errors = validator.validate_package(second)
        self.assertEqual([], first_errors)
        self.assertEqual([], second_errors)
        self.assertEqual([], validator.compare_repetitions([first_package, second_package], "GS-03"))

    def test_rejects_missing_identity_metadata(self) -> None:
        package = self._write_package("run-metadata")
        identity = self._read(package, "runtime-identity.json")
        identity.pop("buildId")
        self._write_json(package / "runtime-identity.json", identity)
        self._assert_rejected(package, "required field is missing: buildId")

    def test_rejects_missing_raw_artifact(self) -> None:
        package = self._write_package("run-artifact")
        manifest = self._read(package, "capture-manifest.json")
        profiler = next(item for item in manifest["artifacts"] if item["kind"] == "profiler")
        (package / profiler["relativePath"]).unlink()
        self._assert_rejected(package, "artifact file is missing or empty")

    def test_rejects_malformed_percentiles(self) -> None:
        package = self._write_package("run-percentile")
        telemetry = self._read(package, "telemetry.json")
        telemetry["aggregates"]["frame.delivered_time"]["p50"] = 99
        self._write_json(package / "telemetry.json", telemetry)
        self._assert_rejected(package, "percentiles are not monotonic")

    def test_rejects_unlinked_artifact(self) -> None:
        package = self._write_package("run-unlinked")
        result = self._read(package, "benchmark-result.json")
        result["artifactReferences"] = [
            item for item in result["artifactReferences"] if item.get("artifactId") != "video"
        ]
        self._write_json(package / "benchmark-result.json", result)
        self._assert_rejected(package, "artifact is unlinked")

    def test_rejects_editor_only_evidence(self) -> None:
        package = self._write_package("run-editor")
        identity = self._read(package, "runtime-identity.json")
        identity["isPlayerBuild"] = False
        identity["isEditor"] = True
        self._write_json(package / "runtime-identity.json", identity)
        manifest = self._read(package, "capture-manifest.json")
        manifest["identity"] = copy.deepcopy(identity)
        self._write_json(package / "capture-manifest.json", manifest)
        self._assert_rejected(package, "Editor-only or non-Player evidence")

    def test_rejects_provenance_violation(self) -> None:
        package = self._write_package("run-provenance")
        result = self._read(package, "benchmark-result.json")
        result["provenance"]["thirdPartyMediaIncluded"] = True
        self._write_json(package / "benchmark-result.json", result)
        self._assert_rejected(package, "provenance boundary violation")

    def test_rejects_render_pipeline_change(self) -> None:
        package = self._write_package("run-pipeline")
        result = self._read(package, "benchmark-result.json")
        result["identity"]["renderPipeline"] = "Universal Render Pipeline"
        self._write_json(package / "benchmark-result.json", result)
        self._assert_rejected(package, "render pipeline changed")

    def test_rejects_repetition_identity_drift(self) -> None:
        first = self._write_package("run-repeat-a")
        second = self._write_package("run-repeat-b")
        identity = self._read(second, "runtime-identity.json")
        identity["buildId"] = "al-gs-different-build"
        self._write_json(second / "runtime-identity.json", identity)
        manifest = self._read(second, "capture-manifest.json")
        manifest["identity"] = copy.deepcopy(identity)
        self._write_json(second / "capture-manifest.json", manifest)
        result = self._read(second, "benchmark-result.json")
        result["identity"]["buildId"] = identity["buildId"]
        self._write_json(second / "benchmark-result.json", result)
        first_package, first_errors = validator.validate_package(first)
        second_package, second_errors = validator.validate_package(second)
        self.assertEqual([], first_errors)
        self.assertEqual([], second_errors)
        errors = validator.compare_repetitions([first_package, second_package], "GS-03")
        self.assertTrue(any("buildId" in error for error in errors), errors)

    def _assert_rejected(self, package: Path, expected: str) -> None:
        validated, errors = validator.validate_package(package)
        self.assertIsNone(validated)
        self.assertTrue(any(expected in error for error in errors), errors)

    def _write_package(self, run_id: str) -> Path:
        package = self.root / run_id
        package.mkdir()
        fingerprint = "1" * 64
        identity = {
            "schemaVersion": "1.0.0",
            "configurationFingerprint": fingerprint,
            "catalogFingerprint": "2" * 64,
            "buildId": "al-gs-20260901-test",
            "sourceCommit": "3" * 40,
            "unityVersion": "6000.3.22f1",
            "platform": "Android",
            "isPlayerBuild": True,
            "isEditor": False,
            "deviceModel": "Fixture Device",
            "operatingSystem": "Android Fixture",
            "processorType": "Fixture CPU",
            "graphicsDeviceName": "Fixture GPU",
            "systemMemoryMb": 6144,
            "graphicsMemoryMb": 1024,
            "graphicsApi": "Vulkan",
            "deviceIdentityHash": "4" * 64,
            "sceneId": "GS-03",
            "sceneRevision": "1",
            "scenarioId": "open_world_combat_major_boss",
            "unitySceneId": "al_scene_champion_arena",
            "unitySceneName": "ChampionArena",
            "seed": 903031,
            "anchorId": "boss_entry",
            "anchorPosition": [0.0, 7.0, -14.0],
            "anchorEulerAngles": [18.0, 0.0, 0.0],
            "projection": "perspective",
            "fieldOfViewDegrees": 50.0,
            "orthographicSize": 1.0,
            "nearClipMeters": 0.2,
            "farClipMeters": 500.0,
            "qualityPresetId": "android_floor_30",
            "qualityPresetRevision": "1",
            "targetFrameRate": 30,
            "renderScale": 0.85,
            "shadowDistanceMeters": 24.0,
            "lodBias": 0.72,
            "textureMipmapLimit": 1,
            "pixelLightCount": 1,
            "vfxDensity": 0.65,
            "runId": run_id,
            "captureId": f"capture-{run_id}",
            "capturedAtUtc": "2026-09-01T00:00:00.0000000Z",
            "operator": "fixture",
            "captureTool": "al-golden-scene-benchmark-runner",
            "captureToolVersion": "1.0.0",
            "durationSeconds": 1.0,
        }
        counters = {
            metric_id: 10.0
            for metric_id in validator.REQUIRED_AGGREGATES
            if not metric_id.startswith("frame.")
        }
        raw_samples = [
            {
                "sequence": index,
                "elapsedSeconds": float(index),
                "interval": "measured",
                "deliveredFrameTimeMs": 10.0,
                "cpuFrameTimeMs": 10.0,
                "gpuFrameTimeMs": 10.0,
                "frameTimingFrameStartTimestampTicks": index + 1,
                "cpuTimerFrequency": 1000,
                "counters": copy.deepcopy(counters),
            }
            for index in (1, 2)
        ]
        aggregate = {
            "unit": "fixture",
            "percentileMethod": "nearest-rank",
            "sampleCount": 2,
            "minimum": 10.0,
            "p50": 10.0,
            "p90": 10.0,
            "p95": 10.0,
            "p99": 10.0,
            "maximum": 10.0,
        }
        capability_ids = (*validator.REQUIRED_AGGREGATES, *validator.REQUIRED_DEVICE_CAPABILITIES)
        telemetry = {
            "schemaVersion": "1.0.0",
            "collectionStartedAtUtc": "2026-09-01T00:00:00.0000000Z",
            "collectionEndedAtUtc": "2026-09-01T00:00:02.0000000Z",
            "actualDurationSeconds": 2.0,
            "warmupSeconds": 0.0,
            "measurementSeconds": 1.0,
            "targetFrameRate": 30,
            "isPlayerBuild": True,
            "isTargetPlatformCertificationEligible": False,
            "certificationStatus": "player-build-telemetry-awaiting-validated-benchmark-identity",
            "warmupSampleCount": 0,
            "measuredSampleCount": 2,
            "batteryDelta": -0.01,
            "deviceStart": {"batteryLevel": 0.8, "batteryStatus": "discharging", "temperatureCelsius": 35.0, "thermalState": "none"},
            "deviceEnd": {"batteryLevel": 0.79, "batteryStatus": "discharging", "temperatureCelsius": 36.0, "thermalState": "none"},
            "deviceSamples": [
                {"elapsedSeconds": 0.0, "interval": "measured", "snapshot": {"batteryLevel": 0.8, "batteryStatus": "discharging", "temperatureCelsius": 35.0, "thermalState": "none"}},
                {"elapsedSeconds": 2.0, "interval": "measured", "snapshot": {"batteryLevel": 0.79, "batteryStatus": "discharging", "temperatureCelsius": 36.0, "thermalState": "none"}},
            ],
            "framePacing": {"sampleCount": 2, "targetFrameRate": 30, "targetFrameTimeMs": 33.333, "averageFrameTimeMs": 10.0, "standardDeviationMs": 0.0, "withinBudgetCount": 2, "overBudgetCount": 0, "withinBudgetRatio": 1.0, "longestOverBudgetRun": 0, "pacingMissCount": 0, "hitchCount": 0, "severeHitchCount": 0},
            "hitches": [],
            "capabilities": [
                {"metricId": metric_id, "unit": "fixture", "source": "fixture", "status": "supported", "reason": "", "sampleCount": 2, "sampleScope": "fixture"}
                for metric_id in capability_ids
            ],
            "aggregates": {metric_id: copy.deepcopy(aggregate) for metric_id in validator.REQUIRED_AGGREGATES},
            "rawSamples": raw_samples,
        }
        media = {
            "width": 64,
            "height": 32,
            "stillFormat": "png",
            "videoFrameRate": 1,
            "videoDurationSeconds": 1.0,
            "uiCaptureMode": "excluded",
            "uiRequirementReference": "",
        }
        artifact_payloads = {
            "still": b"\x89PNG\r\n\x1a\n" + b"\x00\x00\x00\x0dIHDR" + (64).to_bytes(4, "big") + (32).to_bytes(4, "big"),
            "video": b"fixture-mp4",
            "profiler": b"fixture-unity-profiler",
            "telemetry": json.dumps(telemetry, sort_keys=True).encode("utf-8"),
        }
        artifacts = []
        references = []
        for kind, payload in artifact_payloads.items():
            suffix = validator.REQUIRED_ARTIFACTS[kind][1]
            relative = f"scene-GS-03_seed-903031_anchor-boss_entry_run-{run_id}_{kind}{suffix}"
            (package / relative).write_bytes(payload)
            record = {
                "kind": kind,
                "status": "captured",
                "sceneId": "GS-03",
                "seed": 903031,
                "anchorId": "boss_entry",
                "runId": run_id,
                "configurationFingerprint": fingerprint,
                "relativePath": relative,
                "format": validator.REQUIRED_ARTIFACTS[kind][0],
                "sha256": hashlib.sha256(payload).hexdigest(),
                "byteSize": len(payload),
                "startedAtUtc": "2026-09-01T00:00:00.0000000Z",
                "endedAtUtc": "2026-09-01T00:00:01.0000000Z",
                "diagnosticCode": "",
                "reason": "",
            }
            artifacts.append(record)
            references.append({
                "artifactId": kind,
                "path": relative,
                "status": "captured",
                "byteLength": len(payload),
                "sha256": record["sha256"],
                "diagnosticCode": "",
                "reason": "",
            })
        manifest = {
            "schemaVersion": "1.0.0",
            "runId": run_id,
            "captureId": f"capture-{run_id}",
            "sceneId": "GS-03",
            "seed": 903031,
            "anchorId": "boss_entry",
            "configurationFingerprint": fingerprint,
            "captureStartedAtUtc": "2026-09-01T00:00:00.0000000Z",
            "captureEndedAtUtc": "2026-09-01T00:00:01.0000000Z",
            "captureDurationSeconds": 1.0,
            "isComplete": True,
            "hasAllRequiredArtifacts": True,
            "durationRequirementMet": True,
            "requiredVideoFrameCount": 1,
            "videoFrameRequirementMet": True,
            "sourceManifestId": validator.SOURCE_MANIFEST_ID,
            "thirdPartyMediaIncluded": False,
            "rightsBoundary": "fixture",
            "identity": copy.deepcopy(identity),
            "mediaSettings": media,
            "anchorConsistency": {"stillCaptureCount": 1, "videoFrameCount": 1, "driftFailureCount": 0, "isConsistent": True},
            "artifacts": artifacts,
        }
        scorecard = {"schemaVersion": "1.0.0", "certificationStatus": "target-platform-evidence-ready-for-review", "fields": []}
        result = {
            "schemaVersion": "1.0.0",
            "generatedAtUtc": "2026-09-01T00:00:02.0000000Z",
            "identity": {
                "sceneId": "GS-03",
                "anchorId": "boss_entry",
                "qualityId": "android_floor_30",
                "runId": run_id,
                "buildId": identity["buildId"],
                "sourceCommit": identity["sourceCommit"],
                "catalogFingerprint": identity["catalogFingerprint"],
                "unityVersion": identity["unityVersion"],
                "renderPipeline": validator.RENDER_PIPELINE,
                "applicationBuildGuid": "5" * 32,
                "captureStartedAtUtc": identity["capturedAtUtc"],
            },
            "telemetry": {"rawSampleCount": 2, "report": telemetry},
            "capture": {"captureManifest": "capture-manifest.json", "manifestIdentityKey": fingerprint},
            "artifactReferences": references + [
                {"artifactId": "capture-manifest", "path": "capture-manifest.json", "status": "captured"},
                {"artifactId": "scorecard-json", "path": "scorecard.json", "status": "captured"},
                {"artifactId": "scorecard-markdown", "path": "scorecard.md", "status": "captured"},
            ],
            "provenance": {"sourceManifestId": validator.SOURCE_MANIFEST_ID, "thirdPartyMediaIncluded": False},
            "scorecard": {"certificationStatus": scorecard["certificationStatus"], "scorecardJson": "scorecard.json", "scorecardMarkdown": "scorecard.md", "warning": "Editor output is development-only and cannot certify a target platform."},
        }
        self._write_json(package / "runtime-identity.json", identity)
        self._write_json(package / "telemetry.json", telemetry)
        self._write_json(package / "capture-manifest.json", manifest)
        self._write_json(package / "scorecard.json", scorecard)
        (package / "scorecard.md").write_text("# fixture\n", encoding="utf-8")
        self._write_json(package / "benchmark-result.json", result)
        return package

    @staticmethod
    def _read(package: Path, name: str) -> dict:
        return json.loads((package / name).read_text(encoding="utf-8"))

    @staticmethod
    def _write_json(path: Path, value: dict) -> None:
        path.write_text(json.dumps(value, sort_keys=True), encoding="utf-8")


if __name__ == "__main__":
    unittest.main()
