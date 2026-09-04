#!/usr/bin/env python3
"""Fail-closed tests for the Shot070 Vaeloryn motion-source candidate."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).with_name("validate_shot070_vaeloryn_source.py")
SPEC = importlib.util.spec_from_file_location("shot070_vaeloryn_source", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Cannot import validator from {SCRIPT_PATH}")
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


class Shot070VaelorynSourceValidationTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.records = {}
        for name, payload in {
            "source.fbx": b"fbx-candidate",
            "source.blend": b"blend-candidate",
            "landscape.png": b"landscape-proof",
            "portrait.png": b"portrait-proof",
            "motion.mp4": b"motion-proof",
            "contact.png": b"contact-sheet",
            "rig.json": b"rig-report",
        }.items():
            path = self.root / name
            path.write_bytes(payload)
            self.records[name] = {
                "path": name,
                "bytes": len(payload),
                "sha256": hashlib.sha256(payload).hexdigest(),
            }

    def tearDown(self):
        self.temp_dir.cleanup()

    def manifest(self):
        return {
            "schemaVersion": 1,
            "packetId": "tdf_packet_vaeloryn_wish_dragon_shot070_source_v002",
            "sourceVersion": "tdf-cinematic-vaeloryn-2026-09-04-v002",
            "shotBinding": {
                "beatId": "CTMA-BEAT-07",
                "shotId": "Shot070",
                "frameInterval": [1080, 1248],
                "localFrameCount": 168,
                "fps": 24,
                "durationSeconds": 7.0,
            },
            "authority": {
                "status": "MOTION_REVIEW_CANDIDATE",
                "runtimeAuthority": False,
                "gameplayAuthority": False,
                "finalCinematicApproval": False,
                "ownerVisualApprovalRequired": True,
                "runtimeVfxSeparate": True,
            },
            "cost": {
                "incrementalUsd": 0.0,
                "paidProviderCalls": 0,
                "rechargeOrBillingMutation": False,
                "tools": ["Blender 5.2 local", "FFmpeg local"],
            },
            "approved2DSources": [
                {
                    "path": "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001/ConceptSheets/55_vaeloryn_multiview_01_v001.png",
                    "sha256": "b3453b1e23b6ab911fe33fb0820c05e2f9b5d9db0e34ef89875edad83a8f8b55",
                    "authority": "APPROVED_2D",
                },
                {
                    "path": "unity/Docs/Terrestrials/RealmCreatureProductionSourceV001/ConceptSheets/56_vaeloryn_multiview_02_v001.png",
                    "sha256": "ccdb03cd2e4bc2547e95497e251bbd698d52b4c19a1b179b0707993709bd897d",
                    "authority": "APPROVED_2D",
                },
            ],
            "candidateDerivation": {
                "input": {
                    "path": "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/Models/wish_dragon_vaeloryn/wish_dragon_vaeloryn_source_v001.fbx",
                    "sha256": "80bcc74a2cf95cb2626437bba3d3ba805d6087f1498e64b1603cb256f43e68cb",
                    "meshyTaskId": "01a05b2c-92c6-7329-939f-a538fdaa859b",
                },
                "operations": [
                    "local Blender separation",
                    "local armature and skinning",
                    "local articulation review animation",
                ],
                "candidateFiles": [self.records["source.blend"], self.records["source.fbx"]],
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
            "anatomy": {
                "headCount": 1,
                "legCount": 4,
                "wingPairCount": 1,
                "tailCount": 1,
                "semanticRegions": [
                    "body",
                    "head",
                    "jaw",
                    "eye_l",
                    "eye_r",
                    "wing_arm_l",
                    "wing_arm_r",
                    "wing_membrane_l",
                    "wing_membrane_r",
                    "tail",
                ],
                "semanticRegionCount": 10,
            },
            "topology": {
                "triangles": 60000,
                "vertices": 32000,
                "meshObjectCount": 1,
                "materialSlots": ["body", "celestial_membrane", "crown_thorn", "eyes"],
                "independentMaterialRegionCount": 4,
                "uvLayerCount": 1,
                "nonManifoldEdgeCount": 0,
                "boundaryEdgeCount": 0,
            },
            "rig": {
                "rigged": True,
                "armatureCount": 1,
                "deformBoneCount": 32,
                "maxVertexInfluences": 4,
                "unweightedVertexCount": 0,
                "requiredBones": [
                    "root",
                    "pelvis",
                    "spine_01",
                    "neck_01",
                    "head",
                    "jaw",
                    "wing_l_01",
                    "wing_l_02",
                    "wing_r_01",
                    "wing_r_02",
                    "tail_01",
                    "tail_02",
                    "leg_fl_01",
                    "leg_fr_01",
                    "leg_bl_01",
                    "leg_br_01",
                ],
                "report": self.records["rig.json"],
            },
            "motionProof": {
                "file": self.records["motion.mp4"],
                "contactSheet": self.records["contact.png"],
                "codec": "h264",
                "width": 960,
                "height": 540,
                "fps": 24,
                "frameCount": 168,
                "durationSeconds": 7.0,
                "animatedBones": ["neck_01", "jaw", "wing_l_01", "wing_r_01", "tail_01"],
                "genuineArticulation": True,
                "stillImageMotionSubstitute": False,
            },
            "framingProofs": [
                {"aspect": "16:9", "width": 1920, "height": 1080, "file": self.records["landscape.png"]},
                {"aspect": "9:16", "width": 1080, "height": 1920, "file": self.records["portrait.png"]},
            ],
        }

    def test_committed_packet_passes_source_qualification(self):
        summary = VALIDATOR.validate_committed_packet()
        self.assertEqual(summary["status"], "PASS")
        self.assertEqual(summary["frameCount"], 168)
        self.assertEqual(summary["framingProofs"], 2)
        self.assertEqual(summary["incrementalUsd"], 0.0)
        self.assertFalse(summary["runtimeAuthority"])

    def test_accepts_complete_zero_spend_review_candidate(self):
        summary = VALIDATOR.validate_manifest(self.root, self.manifest())
        self.assertEqual(summary["status"], "PASS")
        self.assertEqual(summary["frameCount"], 168)
        self.assertEqual(summary["framingProofs"], 2)

    def test_rejects_rejected_source_reuse(self):
        manifest = self.manifest()
        manifest["rejectedSource"]["usedAsInput"] = True
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "RejectedSourceReuse"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_live_manifest_rejects_rejected_source_reuse(self):
        manifest = json.loads(VALIDATOR.DEFAULT_MANIFEST.read_text(encoding="utf-8"))
        manifest["rejectedSource"]["usedAsInput"] = True
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "RejectedSourceReuse"):
            VALIDATOR.validate_manifest(VALIDATOR.REPOSITORY_ROOT, manifest)

    def test_rejects_paid_provider_or_nonzero_spend(self):
        manifest = self.manifest()
        manifest["cost"]["incrementalUsd"] = 1.0
        manifest["cost"]["paidProviderCalls"] = 1
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "ZeroSpendViolation"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_duplicate_head_or_wrong_appendage_count(self):
        manifest = self.manifest()
        manifest["anatomy"]["headCount"] = 2
        manifest["anatomy"]["wingPairCount"] = 2
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "AnatomyCountMismatch"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_fused_or_under_materialed_candidate(self):
        manifest = self.manifest()
        manifest["anatomy"]["semanticRegionCount"] = 1
        manifest["anatomy"]["semanticRegions"] = ["body"]
        manifest["topology"]["materialSlots"] = ["body"]
        manifest["topology"]["independentMaterialRegionCount"] = 1
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "SeparationContractFailed"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_accepts_bounded_closed_overloaded_source_seam(self):
        manifest = self.manifest()
        manifest["topology"]["nonManifoldEdgeCount"] = 4
        manifest["topology"]["boundaryEdgeCount"] = 0
        self.assertEqual(VALIDATOR.validate_manifest(self.root, manifest)["status"], "PASS")

    def test_rejects_open_or_excess_non_manifold_topology(self):
        manifest = self.manifest()
        manifest["topology"]["nonManifoldEdgeCount"] = 5
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "TopologyContractFailed"):
            VALIDATOR.validate_manifest(self.root, manifest)
        manifest = self.manifest()
        manifest["topology"]["nonManifoldEdgeCount"] = 1
        manifest["topology"]["boundaryEdgeCount"] = 1
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "TopologyContractFailed"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_unrigged_or_unweighted_candidate(self):
        manifest = self.manifest()
        manifest["rig"]["rigged"] = False
        manifest["rig"]["unweightedVertexCount"] = 4
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "RigContractFailed"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_fake_or_incomplete_motion_proof(self):
        manifest = self.manifest()
        manifest["motionProof"]["stillImageMotionSubstitute"] = True
        manifest["motionProof"]["frameCount"] = 24
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "MotionProofFailed"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_missing_native_landscape_or_portrait_framing(self):
        manifest = self.manifest()
        manifest["framingProofs"] = manifest["framingProofs"][:1]
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "FramingProofFailed"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_tampered_candidate_artifact(self):
        manifest = self.manifest()
        (self.root / "source.fbx").write_bytes(b"tampered")
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "ArtifactHashMismatch"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_runtime_or_final_cinematic_authority_leak(self):
        manifest = self.manifest()
        manifest["authority"]["runtimeAuthority"] = True
        manifest["authority"]["finalCinematicApproval"] = True
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "AuthorityLeak"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_rejects_path_traversal(self):
        manifest = self.manifest()
        manifest["motionProof"]["file"]["path"] = "../motion.mp4"
        with self.assertRaisesRegex(VALIDATOR.ValidationError, "UnsafeArtifactPath"):
            VALIDATOR.validate_manifest(self.root, manifest)

    def test_can_validate_manifest_loaded_from_json(self):
        manifest_path = self.root / "manifest.json"
        manifest_path.write_text(json.dumps(self.manifest()), encoding="utf-8")
        summary = VALIDATOR.validate_manifest(
            self.root, json.loads(manifest_path.read_text(encoding="utf-8"))
        )
        self.assertEqual(summary["status"], "PASS")

    def test_rejected_source_digest_is_locked(self):
        self.assertEqual(
            VALIDATOR.REJECTED_SOURCE_SHA256,
            "5a846774341c6e38a8f59df617cbec0b52135f5898a591db271094b3d4bb1270",
        )


if __name__ == "__main__":
    unittest.main()
