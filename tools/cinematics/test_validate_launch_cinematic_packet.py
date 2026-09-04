#!/usr/bin/env python3
"""Fail-closed tests for launch-cinematic packaging.

The committed packet must stay honestly blocked: no approved 60-second master,
no Shot070 review clip promotion, no rejected Meshy reuse, and no still-image
motion substitute. Encode promotion is tested only as a failure path.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("validate_launch_cinematic_packet.py")
SPEC = importlib.util.spec_from_file_location("launch_cinematic_packet", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Cannot import validator from {SCRIPT_PATH}")
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


def _record(path: Path, payload: bytes) -> dict[str, object]:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)
    return {
        "path": path.as_posix(),
        "bytes": len(payload),
        "sha256": hashlib.sha256(payload).hexdigest(),
    }


class LaunchCinematicPacketValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.packet_dir = self.root / "unity/Docs/Cinematics/LaunchCinematicPacketV001"
        self.packet_dir.mkdir(parents=True)
        self.catalog_path = (
            self.root / "unity/Assets/AL/StreamingAssets/GameData/al_launch_cinematic_runtime.v1.json"
        )
        self.shot070_manifest = (
            self.root
            / "unity/Docs/Cinematics/Shot070VaelorynSourceV002/shot070_vaeloryn_source_manifest_v002.json"
        )

    def tearDown(self) -> None:
        self.temp_dir.cleanup()

    def catalog_payload(self) -> bytes:
        return json.dumps(self.catalog_body(), sort_keys=True, indent=2).encode("utf-8") + b"\n"

    def catalog_body(self) -> dict[str, object]:
        return {
            "schema": "al.launch.cinematic.runtime",
            "version": 1,
            "cinematicId": "launch_omen_01",
            "authorityStatus": "PACKAGING_BLOCKED_NO_APPROVED_MASTER",
            "approvedForProduction": False,
            "probeEvidenceApproved": False,
            "reducedMotionFallbackOnly": True,
            "ownerVisualApprovalRequired": True,
            "runtimeAuthority": False,
            "gameplayAuthority": False,
            "finalCinematicApproval": False,
            "platforms": [
                {
                    "platform": "Desktop",
                    "streamingAssetsPath": "LaunchCinematic/Desktop/launch_omen_01.mp4",
                    "container": "mp4",
                    "codecProfile": "h264-high",
                    "width": 1920,
                    "height": 1080,
                    "framesPerSecond": 24,
                    "frameCount": 1440,
                    "durationSeconds": 60.0,
                    "byteLength": 0,
                    "sha256": "",
                    "prepareTimeoutSeconds": 8.0,
                    "skipEligibilityFrame": 120,
                    "encodePresent": False,
                },
                {
                    "platform": "Android",
                    "streamingAssetsPath": "LaunchCinematic/Android/launch_omen_01.mp4",
                    "container": "mp4",
                    "codecProfile": "h264-main",
                    "width": 1280,
                    "height": 720,
                    "framesPerSecond": 24,
                    "frameCount": 1440,
                    "durationSeconds": 60.0,
                    "byteLength": 0,
                    "sha256": "",
                    "prepareTimeoutSeconds": 8.0,
                    "skipEligibilityFrame": 120,
                    "encodePresent": False,
                },
            ],
        }

    def write_catalog(self) -> dict[str, object]:
        return _record(self.catalog_path, self.catalog_payload())

    def write_shot070_manifest(self) -> None:
        self.shot070_manifest.parent.mkdir(parents=True, exist_ok=True)
        self.shot070_manifest.write_text(
            json.dumps(
                {
                    "authority": {
                        "status": "MOTION_REVIEW_CANDIDATE",
                        "runtimeAuthority": False,
                        "gameplayAuthority": False,
                        "finalCinematicApproval": False,
                    },
                    "motionProof": {
                        "durationSeconds": 7.0,
                        "frameCount": 168,
                        "file": {
                            "path": "unity/Docs/Cinematics/Shot070VaelorynSourceV002/shot070_vaeloryn_motion_review_v002.mp4",
                            "sha256": VALIDATOR.SHOT070_REVIEW_SHA256,
                            "bytes": 246640,
                        },
                    },
                },
                sort_keys=True,
            ),
            encoding="utf-8",
        )

    def manifest(self) -> dict[str, object]:
        catalog = self.write_catalog()
        catalog["path"] = "unity/Assets/AL/StreamingAssets/GameData/al_launch_cinematic_runtime.v1.json"
        self.write_shot070_manifest()
        return {
            "schemaVersion": 1,
            "packetId": "tdf_packet_launch_cinematic_packaging_v001",
            "sourceVersion": "tdf-launch-cinematic-packaging-2026-09-04-v001",
            "authority": {
                "status": "PACKAGING_BLOCKED_NO_APPROVED_MASTER",
                "runtimeAuthority": False,
                "gameplayAuthority": False,
                "finalCinematicApproval": False,
                "ownerVisualApprovalRequired": True,
                "approvedForProduction": False,
                "probeEvidenceApproved": False,
            },
            "encodeContract": {
                "durationSeconds": 60.0,
                "fps": 24,
                "frameCount": 1440,
                "desktop": {
                    "width": 1920,
                    "height": 1080,
                    "codecProfile": "h264-high",
                    "maximumBytes": 95000000,
                },
                "android": {
                    "width": 1280,
                    "height": 720,
                    "codecProfile": "h264-main",
                    "maximumBytes": 42000000,
                },
            },
            "forbiddenSources": {
                "rejectedMeshyGlbSha256": VALIDATOR.REJECTED_SOURCE_SHA256,
                "shot070ReviewMp4Sha256": VALIDATOR.SHOT070_REVIEW_SHA256,
                "stillImageMotionSubstitute": False,
            },
            "runtimeCatalog": catalog,
            "encodes": {
                "desktop": None,
                "android": None,
            },
            "reducedMotionFallback": {
                "required": True,
                "bootPresentation": "static-fallback",
                "controllerBinding": "LaunchCinematicPlaybackCoordinator",
            },
            "windowsEvidence": {
                "presentationPath": "static-fallback",
                "packagedLaunchMp4Count": 0,
                "decodeOfLaunchMaster": "NOT_PERFORMED_NO_APPROVED_MASTER",
            },
            "androidEvidence": {
                "presentationPath": "static-fallback",
                "packagedLaunchMp4Count": 0,
                "decodeOfLaunchMaster": "NOT_PERFORMED_NO_APPROVED_MASTER",
            },
        }

    def test_committed_packet_stays_honestly_blocked(self) -> None:
        summary = VALIDATOR.validate_committed_packet()
        self.assertEqual(summary["status"], "PASS")
        self.assertEqual(summary["authorityStatus"], "PACKAGING_BLOCKED_NO_APPROVED_MASTER")
        self.assertFalse(summary["runtimeAuthority"])
        self.assertFalse(summary["approvedForProduction"])
        self.assertEqual(summary["packagedLaunchMp4Count"], 0)
        self.assertEqual(summary["desktopEncode"], None)
        self.assertEqual(summary["androidEncode"], None)

    def test_accepts_blocked_packaging_manifest(self) -> None:
        summary = VALIDATOR.validate_manifest(self.root, self.manifest())
        self.assertEqual(summary["status"], "PASS")
        self.assertEqual(summary["packagedLaunchMp4Count"], 0)

    def test_rejects_shot070_review_clip_as_launch_master(self) -> None:
        manifest = self.manifest()
        manifest["encodes"]["desktop"] = {
            "path": "unity/Assets/AL/StreamingAssets/LaunchCinematic/Desktop/launch_omen_01.mp4",
            "sha256": VALIDATOR.SHOT070_REVIEW_SHA256,
            "bytes": 246640,
            "width": 1920,
            "height": 1080,
            "codecProfile": "h264-high",
            "fps": 24,
            "frameCount": 168,
            "durationSeconds": 7.0,
        }
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "Shot070IsNotLaunchMaster"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_rejected_meshy_source_reuse(self) -> None:
        manifest = self.manifest()
        manifest["forbiddenSources"]["rejectedMeshyGlbSha256"] = "0" * 64
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "RejectedSourceReuse"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_still_image_motion_substitute(self) -> None:
        manifest = self.manifest()
        manifest["forbiddenSources"]["stillImageMotionSubstitute"] = True
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "StillImageMotionForbidden"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_runtime_or_final_cinematic_authority_leak(self) -> None:
        manifest = self.manifest()
        manifest["authority"]["runtimeAuthority"] = True
        manifest["authority"]["finalCinematicApproval"] = True
        manifest["authority"]["approvedForProduction"] = True
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "AuthorityLeak"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_approved_production_flag_without_encodes(self) -> None:
        manifest = self.manifest()
        body = self.catalog_body()
        body["approvedForProduction"] = True
        payload = json.dumps(body, sort_keys=True, indent=2).encode("utf-8") + b"\n"
        self.catalog_path.write_bytes(payload)
        manifest["runtimeCatalog"] = {
            "path": "unity/Assets/AL/StreamingAssets/GameData/al_launch_cinematic_runtime.v1.json",
            "bytes": len(payload),
            "sha256": hashlib.sha256(payload).hexdigest(),
        }
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "UnapprovedPromotion"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_packaged_launch_mp4_while_blocked(self) -> None:
        manifest = self.manifest()
        planted = (
            self.root
            / "unity/Assets/AL/StreamingAssets/LaunchCinematic/Desktop/launch_omen_01.mp4"
        )
        planted.parent.mkdir(parents=True)
        planted.write_bytes(b"not-a-master")
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "PackagedLaunchMediaWhileBlocked"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_tampered_runtime_catalog_hash(self) -> None:
        manifest = self.manifest()
        self.catalog_path.write_bytes(b"tampered-catalog\n")
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "ArtifactHashMismatch"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_path_traversal(self) -> None:
        manifest = self.manifest()
        manifest["runtimeCatalog"]["path"] = "../secret.json"
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "UnsafeArtifactPath"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_package_encodes_fail_closed_without_approved_master(self) -> None:
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "NoApprovedMaster"):
            VALIDATOR.package_encodes(self.root, self.manifest())

    def test_package_encodes_rejects_shot070_as_source(self) -> None:
        manifest = self.manifest()
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "Shot070IsNotLaunchMaster"):
            VALIDATOR.package_encodes(
                self.root,
                manifest,
                source_sha256=VALIDATOR.SHOT070_REVIEW_SHA256,
            )

    def test_package_encodes_rejects_rejected_glb_source(self) -> None:
        manifest = self.manifest()
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "RejectedSourceReuse"):
            VALIDATOR.package_encodes(
                self.root,
                manifest,
                source_sha256=VALIDATOR.REJECTED_SOURCE_SHA256,
            )

    def test_live_shot070_authority_cannot_be_promoted(self) -> None:
        manifest = json.loads(VALIDATOR.DEFAULT_MANIFEST.read_text(encoding="utf-8"))
        manifest["authority"]["status"] = "MOTION_REVIEW_CANDIDATE"
        manifest["authority"]["runtimeAuthority"] = True
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "AuthorityLeak"):
            VALIDATOR.validate_manifest(VALIDATOR.REPOSITORY_ROOT, manifest)

    def test_rejected_and_shot070_digests_are_locked(self) -> None:
        self.assertEqual(
            VALIDATOR.REJECTED_SOURCE_SHA256,
            "5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270",
        )
        self.assertEqual(
            VALIDATOR.SHOT070_REVIEW_SHA256,
            "0f7b66dc3fd6450405cec9cbf5840ba82fd1589ab5fbe73148b1381527169122",
        )


if __name__ == "__main__":
    unittest.main()
