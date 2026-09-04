#!/usr/bin/env python3
"""Fail-closed tests for Shot070 Vaeloryn first-run shot scale-out."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("validate_shot070_vaeloryn_shot_scale.py")
SPEC = importlib.util.spec_from_file_location("shot070_vaeloryn_shot_scale", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Cannot import validator from {SCRIPT_PATH}")
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


LOCKED_BLEND_SHA256 = "10bf9f96380632c983b523172913de8aa31b3187b785bd0b35b23757c7681b89"
LOCKED_LANDSCAPE_SHA256 = (
    "0f7b66dc3fd6450405cec9cbf5840ba82fd1589ab5fbe73148b1381527169122"
)
INELIGIBLE_SHOTS = {
    "Shot010",
    "Shot020",
    "Shot030",
    "Shot040",
    "Shot050",
    "Shot060",
    "Shot080",
}


class Shot070VaelorynShotScaleValidationTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.records = {}
        payloads = {
            "locked.blend": b"locked-v002-blend",
            "landscape.mp4": b"locked-landscape-motion",
            "contact.png": b"locked-contact-sheet",
        }
        for name, payload in payloads.items():
            path = self.root / name
            path.write_bytes(payload)
            self.records[name] = {
                "path": name,
                "bytes": len(payload),
                "sha256": hashlib.sha256(payload).hexdigest(),
            }

    def tearDown(self):
        self.temp_dir.cleanup()

    def reuse_rows(self):
        rows = []
        for shot_id in (
            "Shot010",
            "Shot020",
            "Shot030",
            "Shot040",
            "Shot050",
            "Shot060",
            "Shot070",
            "Shot080",
        ):
            eligible = shot_id == "Shot070"
            rows.append(
                {
                    "shotId": shot_id,
                    "eligible": eligible,
                    "boundCandidate": "tdf_packet_vaeloryn_wish_dragon_shot070_source_v002"
                    if eligible
                    else None,
                    "reason": (
                        "Wish Dragon / Vaeloryn locked V002 may only bind CTMA-BEAT-07 Shot070"
                        if eligible
                        else "First-run beat does not use the Wish Dragon candidate"
                    ),
                }
            )
        return rows

    def manifest(self):
        return {
            "schemaVersion": 1,
            "packetId": "tdf_packet_vaeloryn_wish_dragon_shot_scale_v001",
            "sourceVersion": "tdf-cinematic-vaeloryn-shot-scale-2026-09-04-v001",
            "authority": {
                "status": "MOTION_REVIEW_CANDIDATE",
                "runtimeAuthority": False,
                "gameplayAuthority": False,
                "finalCinematicApproval": False,
                "ownerVisualApprovalRequired": True,
                "runtimeVfxSeparate": True,
                "didNotRegenerateLockedSource": True,
            },
            "cost": {
                "incrementalUsd": 0.0,
                "paidProviderCalls": 0,
                "rechargeOrBillingMutation": False,
                "tools": ["Blender 5.2 local", "FFmpeg local"],
            },
            "lockedSource": {
                "packetId": "tdf_packet_vaeloryn_wish_dragon_shot070_source_v002",
                "blend": self.records["locked.blend"],
                "landscapeMotion": self.records["landscape.mp4"],
            },
            "rejectedSource": {
                "basename": "wish_dragon_review_master.glb",
                "sha256": VALIDATOR.REJECTED_SOURCE_SHA256,
                "inputEligible": False,
                "usedAsInput": False,
                "disposition": "REJECTED_FOR_EXACT_SOURCE_FIDELITY",
                "negativeChecks": [
                    "duplicate_head",
                    "fused_monolithic_mesh",
                    "unskinned",
                    "single_material",
                    "identity_wing_emission_drift",
                    "lineage_gap",
                ],
            },
            "firstRunReuse": self.reuse_rows(),
            "remainingShots": [
                {
                    "shotId": "Shot070",
                    "beatId": "CTMA-BEAT-07",
                    "clipId": "AL_FR_MOTION_SRC_070_EIGHTFOLD_WISH_V001",
                    "aspect": "16:9",
                    "width": 960,
                    "height": 540,
                    "fps": 24,
                    "frameCount": 168,
                    "durationSeconds": 7.0,
                    "codec": "h264",
                    "genuineArticulation": True,
                    "stillImageMotionSubstitute": False,
                    "derivedFromLockedV002Action": True,
                    "croppedFromLandscape": False,
                    "newPixelGeneration": False,
                    "animatedBones": [
                        "neck_01",
                        "jaw",
                        "wing_l_01",
                        "wing_r_01",
                        "tail_01",
                    ],
                    "file": self.records["landscape.mp4"],
                    "contactSheet": self.records["contact.png"],
                }
            ],
        }

    def test_accepts_complete_zero_spend_locked_landscape_scale_out(self):
        summary = VALIDATOR.validate_manifest(self.root, self.manifest())
        self.assertEqual(summary["status"], "PASS")
        self.assertEqual(summary["remainingShotCount"], 1)
        self.assertEqual(summary["eligibleShotIds"], ["Shot070"])
        self.assertEqual(summary["incrementalUsd"], 0.0)
        self.assertFalse(summary["runtimeAuthority"])
        self.assertFalse(summary["finalCinematicApproval"])

    def test_rejects_rejected_source_reuse(self):
        manifest = self.manifest()
        manifest["rejectedSource"]["usedAsInput"] = True
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "RejectedSourceReuse"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_ineligible_first_run_shot_binding(self):
        manifest = self.manifest()
        for row in manifest["firstRunReuse"]:
            if row["shotId"] == "Shot060":
                row["eligible"] = True
                row["boundCandidate"] = (
                    "tdf_packet_vaeloryn_wish_dragon_shot070_source_v002"
                )
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "IneligibleShotReuse"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_missing_shot070_eligibility(self):
        manifest = self.manifest()
        for row in manifest["firstRunReuse"]:
            if row["shotId"] == "Shot070":
                row["eligible"] = False
                row["boundCandidate"] = None
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "EligibleShotMissing"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_paid_provider_or_nonzero_spend(self):
        manifest = self.manifest()
        manifest["cost"]["incrementalUsd"] = 1.0
        manifest["cost"]["paidProviderCalls"] = 1
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "ZeroSpendViolation"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_locked_source_regeneration_claim(self):
        manifest = self.manifest()
        manifest["authority"]["didNotRegenerateLockedSource"] = False
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "LockedSourceMutation"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_fake_or_incomplete_motion_proof(self):
        manifest = self.manifest()
        manifest["remainingShots"][0]["stillImageMotionSubstitute"] = True
        manifest["remainingShots"][0]["frameCount"] = 24
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "MotionProofFailed"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_new_pixel_generation_or_landscape_crop(self):
        manifest = self.manifest()
        manifest["remainingShots"][0]["newPixelGeneration"] = True
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "LockedSourceMutation"):
            VALIDATOR.validate_manifest(self.root, manifest)
        manifest = self.manifest()
        manifest["remainingShots"][0]["croppedFromLandscape"] = True
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "MotionProofFailed"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_runtime_or_final_cinematic_authority_leak(self):
        manifest = self.manifest()
        manifest["authority"]["runtimeAuthority"] = True
        manifest["authority"]["finalCinematicApproval"] = True
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "AuthorityLeak"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_tampered_motion_artifact(self):
        manifest = self.manifest()
        (self.root / "landscape.mp4").write_bytes(b"tampered")
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "ArtifactHashMismatch"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_path_traversal(self):
        manifest = self.manifest()
        manifest["remainingShots"][0]["file"]["path"] = "../landscape.mp4"
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "UnsafeArtifactPath"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_binding_vaeloryn_to_end_card(self):
        manifest = self.manifest()
        for row in manifest["firstRunReuse"]:
            if row["shotId"] == "Shot080":
                row["eligible"] = True
                row["boundCandidate"] = (
                    "tdf_packet_vaeloryn_wish_dragon_shot070_source_v002"
                )
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "IneligibleShotReuse"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_committed_packet_passes_shot_scale_qualification(self):
        summary = VALIDATOR.validate_committed_packet()
        self.assertEqual(summary["status"], "PASS")
        self.assertEqual(summary["remainingShotCount"], 1)
        self.assertEqual(summary["eligibleShotIds"], ["Shot070"])
        self.assertEqual(summary["incrementalUsd"], 0.0)
        self.assertFalse(summary["runtimeAuthority"])
        self.assertFalse(summary["finalCinematicApproval"])

    def test_live_manifest_rejects_rejected_source_reuse(self):
        manifest = json.loads(VALIDATOR.DEFAULT_MANIFEST.read_text(encoding="utf-8"))
        manifest["rejectedSource"]["usedAsInput"] = True
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "RejectedSourceReuse"):
            VALIDATOR.validate_manifest(VALIDATOR.REPOSITORY_ROOT, manifest)

    def test_live_manifest_rejects_ineligible_shot_binding(self):
        manifest = json.loads(VALIDATOR.DEFAULT_MANIFEST.read_text(encoding="utf-8"))
        for row in manifest["firstRunReuse"]:
            if row["shotId"] == "Shot060":
                row["eligible"] = True
                row["boundCandidate"] = (
                    "tdf_packet_vaeloryn_wish_dragon_shot070_source_v002"
                )
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "IneligibleShotReuse"):
            VALIDATOR.validate_manifest(VALIDATOR.REPOSITORY_ROOT, manifest)

    def test_rejected_source_digest_is_locked(self):
        self.assertEqual(
            VALIDATOR.REJECTED_SOURCE_SHA256,
            "5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270",
        )
        self.assertEqual(set(INELIGIBLE_SHOTS), set(VALIDATOR.INELIGIBLE_SHOT_IDS))
        self.assertEqual(LOCKED_BLEND_SHA256, VALIDATOR.LOCKED_BLEND_SHA256)
        self.assertEqual(LOCKED_LANDSCAPE_SHA256, VALIDATOR.LOCKED_LANDSCAPE_SHA256)

    def test_can_validate_manifest_loaded_from_json(self):
        manifest_path = self.root / "manifest.json"
        manifest_path.write_text(json.dumps(self.manifest()), encoding="utf-8")
        summary = VALIDATOR.validate_manifest(
            self.root, json.loads(manifest_path.read_text(encoding="utf-8"))
        )
        self.assertEqual(summary["status"], "PASS")


if __name__ == "__main__":
    unittest.main()
