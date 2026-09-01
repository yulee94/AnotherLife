#!/usr/bin/env python3
"""Contract tests for deterministic Blender rig cleanup artifacts."""

from __future__ import annotations

import copy
import sys
import unittest
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from al_rig_pipeline_contract import (
    DEFAULT_MANIFEST,
    DEFAULT_SCHEMA,
    REPOSITORY_ROOT,
    RigCleanupContractError,
    load_json,
    sha256_file,
    skeleton_signature,
    validate_generated_sidecar,
    validate_manifest,
)


class RigPipelineManifestTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.manifest = load_json(DEFAULT_MANIFEST)
        cls.schema = load_json(DEFAULT_SCHEMA)
        cls.standard = load_json(REPOSITORY_ROOT / cls.manifest["standardPath"])
        cls.provenance = load_json(REPOSITORY_ROOT / cls.manifest["provenancePath"])

    def validate_mutation(self, mutation) -> RigCleanupContractError:
        manifest = copy.deepcopy(self.manifest)
        provenance = copy.deepcopy(self.provenance)
        mutation(manifest, provenance)
        with self.assertRaises(RigCleanupContractError) as context:
            validate_manifest(
                REPOSITORY_ROOT,
                manifest=manifest,
                schema=self.schema,
                standard=self.standard,
                provenance=provenance,
            )
        return context.exception

    def test_manifest_validates_three_representatives(self) -> None:
        evidence = validate_manifest(REPOSITORY_ROOT)
        self.assertEqual(3, evidence["assets"])
        self.assertEqual(3, evidence["representativeSubjects"])
        self.assertEqual(3, evidence["localSources"])
        self.assertEqual(37, evidence["declaredSockets"])
        self.assertEqual(2, evidence["unresolvedProductionRights"])

    def test_source_hash_mismatch_fails_closed(self) -> None:
        error = self.validate_mutation(
            lambda manifest, _provenance: manifest["assets"][0]["source"].update(
                sha256="0" * 64
            )
        )
        self.assertIn("SourceHashMismatch", str(error))

    def test_provenance_binding_mismatch_fails_closed(self) -> None:
        error = self.validate_mutation(
            lambda _manifest, provenance: provenance["records"][0].update(
                catalogAssetId="wrong"
            )
        )
        self.assertIn("ProvenanceBindingMismatch", str(error))

    def test_missing_rights_evidence_fails_closed(self) -> None:
        error = self.validate_mutation(
            lambda _manifest, provenance: provenance["records"][0].update(
                rightsEvidence=[]
            )
        )
        self.assertIn("MissingRightsEvidence", str(error))

    def test_duplicate_bone_target_fails_closed(self) -> None:
        def mutate(manifest, _provenance) -> None:
            rename_map = manifest["assets"][0]["boneRenameMap"]
            first, second = list(rename_map)[:2]
            rename_map[second] = rename_map[first]

        error = self.validate_mutation(mutate)
        self.assertIn("DuplicateBoneRenameTarget", str(error))

    def test_output_path_escape_fails_schema(self) -> None:
        error = self.validate_mutation(
            lambda manifest, _provenance: manifest["assets"][0]["output"].update(
                blendPath="../escape.blend"
            )
        )
        self.assertIn("SchemaViolation", str(error))

    def test_source_output_alias_fails_closed(self) -> None:
        def mutate(manifest, _provenance) -> None:
            manifest["assets"][0]["output"]["blendPath"] = manifest["assets"][0][
                "source"
            ]["path"]

        error = self.validate_mutation(mutate)
        self.assertIn("SourceOutputAlias", str(error))

    def test_subject_coverage_fails_when_profile_is_duplicated(self) -> None:
        error = self.validate_mutation(
            lambda manifest, _provenance: manifest["assets"][2].update(
                subjectKind="npc"
            )
        )
        self.assertIn("RepresentativeCoverageMismatch", str(error))


class GeneratedRigArtifactTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.manifest = load_json(DEFAULT_MANIFEST)

    def test_all_generated_sidecars_pass_contract(self) -> None:
        totals = {"bones": 0, "meshes": 0, "triangles": 0}
        for asset in self.manifest["assets"]:
            with self.subTest(asset=asset["id"]):
                sidecar_path = REPOSITORY_ROOT / asset["output"]["sidecarPath"]
                self.assertTrue(sidecar_path.is_file())
                evidence = validate_generated_sidecar(load_json(sidecar_path), asset)
                for field in totals:
                    totals[field] += evidence[field]
        self.assertGreaterEqual(totals["bones"], 100)
        self.assertGreaterEqual(totals["meshes"], 3)
        self.assertGreater(totals["triangles"], 30_000)

    def test_all_export_receipts_match_fbx_bytes(self) -> None:
        for asset in self.manifest["assets"]:
            with self.subTest(asset=asset["id"]):
                receipt_path = REPOSITORY_ROOT / asset["output"]["fbxReceiptPath"]
                fbx_path = REPOSITORY_ROOT / asset["output"]["fbxPath"]
                receipt = load_json(receipt_path)
                sidecar = load_json(REPOSITORY_ROOT / asset["output"]["sidecarPath"])
                self.assertEqual("export_valid", receipt["status"])
                self.assertEqual([], receipt["roundTrip"]["errors"])
                self.assertEqual(sha256_file(fbx_path), receipt["export"]["sha256"])
                self.assertEqual("-Z", receipt["export"]["axisForward"])
                self.assertEqual("Y", receipt["export"]["axisUp"])
                self.assertFalse(receipt["export"]["addLeafBones"])
                self.assertEqual(
                    sidecar["skeleton"]["signature"],
                    receipt["source"]["skeletonSignature"],
                )
                self.assertEqual(
                    sidecar["preflight"]["actionSignature"],
                    receipt["source"]["actionSignature"],
                )
                expected_level = (
                    "semantic" if asset["minimumSourceActions"] else "byte_exact"
                )
                self.assertEqual(expected_level, receipt["export"]["determinismLevel"])

    def test_repeatability_report_covers_every_representative(self) -> None:
        report = load_json(
            REPOSITORY_ROOT
            / "unity/ArtSource/RigPipeline/al_rig_cleanup_validation_report.v1.json"
        )
        self.assertEqual("validated", report["status"])
        self.assertEqual(
            {asset["id"] for asset in self.manifest["assets"]},
            set(report["assets"]),
        )
        self.assertEqual(
            self.manifest["determinismPolicy"], report["determinismPolicy"]
        )

    def test_outputs_are_versioned_and_do_not_overwrite_sources(self) -> None:
        for asset in self.manifest["assets"]:
            with self.subTest(asset=asset["id"]):
                source = asset["source"]["path"]
                outputs = set(asset["output"].values())
                self.assertNotIn(source, outputs)
                for output in outputs:
                    self.assertTrue((REPOSITORY_ROOT / output).is_file(), output)

    def test_sidecar_signature_tamper_fails_closed(self) -> None:
        asset = self.manifest["assets"][0]
        sidecar = load_json(REPOSITORY_ROOT / asset["output"]["sidecarPath"])
        sidecar["skeleton"]["signature"] = "0" * 64
        with self.assertRaisesRegex(RigCleanupContractError, "SkeletonSignatureMismatch"):
            validate_generated_sidecar(sidecar, asset)

    def test_signature_is_independent_of_record_order(self) -> None:
        asset = self.manifest["assets"][0]
        sidecar = load_json(REPOSITORY_ROOT / asset["output"]["sidecarPath"])
        records = sidecar["skeleton"]["records"]
        self.assertEqual(
            skeleton_signature(records), skeleton_signature(list(reversed(records)))
        )

    def test_bounded_representatives_never_claim_production_eligibility(self) -> None:
        for asset in self.manifest["assets"]:
            sidecar = load_json(REPOSITORY_ROOT / asset["output"]["sidecarPath"])
            self.assertFalse(sidecar["productionEligible"])
            self.assertEqual(sorted(asset["productionGaps"]), sorted(sidecar["productionGaps"]))


if __name__ == "__main__":
    unittest.main()
