import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from tools.terrestrial.apply_realm_creature_dcc_repairs import (
    REPAIRS,
    apply_summary_counts,
    apply_packet_revision,
    recompute_summary,
    update_repaired_source_record,
    validate_baked_map_bindings,
    validate_repair_evidence,
    validate_smoothing_evidence,
    validate_source_uv_evidence,
)


class ApplyRealmCreatureDccRepairsTests(unittest.TestCase):
    def test_rejects_semantically_forged_uv_and_smoothing_evidence(self):
        uv_diagnostics = validate_source_uv_evidence(
            {
                "input": "unity/wrong.fbx",
                "inputSha256": "0" * 64,
                "uvLayer": "UVMap_Clean",
                "uvFacesOutsideUnit": 0,
                "uvZeroAreaFaces": 0,
                "uvOverlappingFaces": 1,
                "diagnostics": [],
            },
            expected_model_path="unity/model_v004.fbx",
            expected_model_sha256="a" * 64,
        )
        self.assertIn("source UV evidence input path mismatch", uv_diagnostics)
        self.assertIn("source UV evidence inputSha256 mismatch", uv_diagnostics)
        self.assertIn("source UV evidence uvOverlappingFaces must be zero", uv_diagnostics)

        smoothing_diagnostics = validate_smoothing_evidence(
            {
                "modelId": "elite_umbral_cindermaw_salamander",
                "input": "unity/input_v003.fbx",
                "inputSha256": "b" * 64,
                "output": "unity/wrong.fbx",
                "outputSha256": "0" * 64,
                "editableBlend": "unity/wrong.blend",
                "editableBlendSha256": "0" * 64,
                "status": "clean_geometry_pass_uv_bake_pass_smoothing_pass_normal_detail_rebuild_required",
                "productionReady": False,
                "diagnostics": [],
                "metrics": {
                    "sharpEdgesBefore": 53054,
                    "sharpEdgesAfter": 53054,
                    "customNormalsRemoved": False,
                },
            },
            expected_input_path="unity/input_v003.fbx",
            expected_input_sha256="b" * 64,
            expected_output_path="unity/model_v004.fbx",
            expected_output_sha256="a" * 64,
            expected_blend_path="unity/model_v004.blend",
            expected_blend_sha256="c" * 64,
        )
        self.assertIn("smoothing evidence output path mismatch", smoothing_diagnostics)
        self.assertIn("smoothing evidence outputSha256 mismatch", smoothing_diagnostics)
        self.assertIn("smoothing evidence editableBlend path mismatch", smoothing_diagnostics)
        self.assertIn("smoothing evidence editableBlendSha256 mismatch", smoothing_diagnostics)
        self.assertIn("smoothing evidence sharp-edge reduction is invalid", smoothing_diagnostics)
        self.assertIn("smoothing evidence customNormalsRemoved must be true", smoothing_diagnostics)

    def test_rejects_unbound_authored_normal_nested_evidence(self):
        repair = REPAIRS["elite_umbral_cindermaw_salamander"]
        with tempfile.TemporaryDirectory() as temporary:
            repo_root = Path(temporary)
            packet_root = repo_root / "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001"
            model = packet_root / repair["model"]
            input_model = repo_root / "unity/input.fbx"
            blend = repo_root / "unity/source.blend"
            uv = repo_root / "unity/uv.json"
            smoothing = repo_root / "unity/smoothing.json"
            for path in (model, input_model, blend, uv, smoothing):
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(path.name.encode("utf-8"))
            model_hash = hashlib.sha256(model.read_bytes()).hexdigest()
            report = {
                "modelId": "elite_umbral_cindermaw_salamander",
                "sourceTaskIds": repair["tasks"],
                "input": "unity/input.fbx",
                "inputSha256": hashlib.sha256(input_model.read_bytes()).hexdigest(),
                "output": model.relative_to(repo_root).as_posix(),
                "outputSha256": model_hash,
                "editableBlend": "unity/source.blend",
                "editableBlendSha256": "0" * 64,
                "status": repair["status"],
                "productionReady": False,
                "rigged": False,
                "runtimeIntegrationState": "Blocked",
                "metrics": {
                    "uvLayer": "UVMap_Clean",
                    "uvFacesOutsideUnit": 0,
                    "uvZeroAreaFaces": 0,
                    "uvOverlappingFaces": 0,
                    "polygonalProjectionBlockerResolved": True,
                    "sharpEdgesBefore": 53054,
                    "sharpEdgesAfter": 631,
                    "normalAngularP95Degrees": 11.4,
                    "normalAngularMaxDegrees": 25.1,
                },
                "normalDetail": {
                    "status": "PASS",
                    "method": repair["normalProvenance"],
                    "authoredNormalDetail": True,
                    "runtimeVfxSeparate": True,
                },
                "sourceUvEvidence": {"path": "unity/uv.json", "sha256": "0" * 64},
                "smoothingEvidence": {"path": "unity/smoothing.json", "sha256": "1" * 64},
                "bakedMaps": [],
                "diagnostics": [],
            }
            diagnostics = validate_repair_evidence(
                model_id="elite_umbral_cindermaw_salamander",
                repair=repair,
                report=report,
                selected_source={"path": repair["model"], "sha256": model_hash},
                textures=[],
                packet_root=packet_root,
                repo_root=repo_root,
            )

            self.assertIn("sourceUvEvidence sha256 does not match path", diagnostics)
            self.assertIn("smoothingEvidence sha256 does not match path", diagnostics)
            self.assertIn(
                "report editableBlendSha256 does not match editableBlend",
                diagnostics,
            )

            uv.write_text(
                json.dumps(
                    {
                        "input": "unity/wrong.fbx",
                        "inputSha256": "0" * 64,
                        "uvLayer": "UVMap_Clean",
                        "uvFacesOutsideUnit": 0,
                        "uvZeroAreaFaces": 0,
                        "uvOverlappingFaces": 1,
                        "diagnostics": [],
                    }
                ),
                encoding="utf-8",
            )
            smoothing.write_text(
                json.dumps(
                    {
                        "modelId": "elite_umbral_cindermaw_salamander",
                        "status": "forged",
                        "productionReady": False,
                        "diagnostics": [],
                        "metrics": {
                            "sharpEdgesBefore": 53054,
                            "sharpEdgesAfter": 53054,
                            "customNormalsRemoved": False,
                        },
                    }
                ),
                encoding="utf-8",
            )
            for evidence_path in (uv, smoothing):
                evidence_path.write_bytes(evidence_path.read_bytes() + b"\r\n")
            report["sourceUvEvidence"]["sha256"] = hashlib.sha256(
                uv.read_bytes().replace(b"\r\n", b"\n")
            ).hexdigest()
            report["smoothingEvidence"]["sha256"] = hashlib.sha256(
                smoothing.read_bytes().replace(b"\r\n", b"\n")
            ).hexdigest()
            diagnostics = validate_repair_evidence(
                model_id="elite_umbral_cindermaw_salamander",
                repair=repair,
                report=report,
                selected_source={"path": repair["model"], "sha256": model_hash},
                textures=[],
                packet_root=packet_root,
                repo_root=repo_root,
            )
            self.assertIn("source UV evidence uvOverlappingFaces must be zero", diagnostics)
            self.assertIn("smoothing evidence status mismatch", diagnostics)

    def test_accepts_bound_authored_normal_provenance_when_expected(self):
        path = "unity/packet/normal.png"
        provenance = "object_space_procedural_height_to_clean_uv_tangent_normal_v001"
        reported = {
            "name": "normal",
            "path": path,
            "sha256": "a" * 64,
            "dimensions": [4096, 4096],
            "provenance": provenance,
        }
        diagnostics = validate_baked_map_bindings(
            [reported],
            {path: {"sha256": "a" * 64, "dimensions": [4096, 4096]}},
            expected_normal_provenance=provenance,
        )
        self.assertEqual([], diagnostics)

    def test_rejects_normal_map_without_neutral_tangent_provenance(self):
        path = "unity/packet/normal.png"
        reported = {
            "name": "normal",
            "path": path,
            "sha256": "a" * 64,
            "dimensions": [4096, 4096],
        }
        diagnostics = validate_baked_map_bindings(
            [reported],
            {path: {"sha256": "a" * 64, "dimensions": [4096, 4096]}},
        )
        self.assertIn("normal baked-map provenance must be neutral_tangent", diagnostics)

    def test_rejects_malformed_extra_baked_map_records(self):
        path = "unity/packet/normal.png"
        valid = {
            "name": "normal",
            "path": path,
            "sha256": "a" * 64,
            "dimensions": [4096, 4096],
        }
        diagnostics = validate_baked_map_bindings(
            [valid, {}],
            {path: {"sha256": "a" * 64, "dimensions": [4096, 4096]}},
        )
        self.assertIn("malformed baked-map record: index 1", diagnostics)

    def test_rejects_duplicate_baked_map_names_and_paths(self):
        path = "unity/packet/normal.png"
        entry = {
            "name": "normal",
            "path": path,
            "sha256": "a" * 64,
            "dimensions": [4096, 4096],
        }
        diagnostics = validate_baked_map_bindings(
            [entry, dict(entry)],
            {path: {"sha256": "a" * 64, "dimensions": [4096, 4096]}},
        )
        self.assertIn(f"duplicate baked-map path: {path}", diagnostics)
        self.assertIn("duplicate baked-map name: normal", diagnostics)

    def test_rejects_swapped_baked_map_names_with_valid_paths_and_hashes(self):
        normal_path = "unity/packet/normal.png"
        roughness_path = "unity/packet/roughness.png"
        expected = {
            normal_path: {"sha256": "a" * 64, "dimensions": [4096, 4096]},
            roughness_path: {"sha256": "b" * 64, "dimensions": [4096, 4096]},
        }
        reported = [
            {
                "name": "roughness",
                "path": normal_path,
                "sha256": "a" * 64,
                "dimensions": [4096, 4096],
            },
            {
                "name": "normal",
                "path": roughness_path,
                "sha256": "b" * 64,
                "dimensions": [4096, 4096],
            },
        ]
        diagnostics = validate_baked_map_bindings(reported, expected)
        self.assertIn(f"baked-map name mismatch: {normal_path}", diagnostics)
        self.assertIn(f"baked-map name mismatch: {roughness_path}", diagnostics)

    def test_rejects_report_with_only_matching_output_hash(self):
        repair = REPAIRS["boss_eldergrove_mere_root_leviathan"]
        diagnostics = validate_repair_evidence(
            model_id="boss_eldergrove_mere_root_leviathan",
            repair=repair,
            report={"outputSha256": "a" * 64},
            selected_source={"path": repair["model"], "sha256": "a" * 64},
            textures=[],
            packet_root=Path("unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001"),
            repo_root=Path("."),
        )
        self.assertIn("report modelId mismatch", diagnostics)
        self.assertIn("report status mismatch", diagnostics)
        self.assertIn("report diagnostics must be an explicit empty list", diagnostics)
        self.assertIn("report productionReady must remain false", diagnostics)

    def test_rejects_cindermaw_bad_uv_metrics_and_map_checksum(self):
        repair = REPAIRS["elite_umbral_cindermaw_salamander"]
        report = {
            "modelId": "elite_umbral_cindermaw_salamander",
            "sourceTaskIds": repair["tasks"],
            "input": "missing-input.fbx",
            "inputSha256": "a" * 64,
            "output": "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/Models/cinder.fbx",
            "outputSha256": "b" * 64,
            "editableBlend": "missing.blend",
            "status": repair["status"],
            "productionReady": False,
            "rigged": False,
            "runtimeIntegrationState": "Blocked",
            "metrics": {
                "uvLayer": "UVMap_Clean",
                "uvFacesOutsideUnit": 0,
                "uvZeroAreaFaces": 0,
                "uvOverlappingFaces": 4,
                "polygonalProjectionBlockerResolved": True,
                "nonManifoldEdgesBefore": 1,
                "nonManifoldEdgesAfter": 1,
            },
            "bakedMaps": [
                {
                    "name": "base_color",
                    "path": "unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001/Textures/base_color.png",
                    "dimensions": [8192, 8192],
                    "sha256": "c" * 64,
                }
            ],
            "diagnostics": [],
        }
        diagnostics = validate_repair_evidence(
            model_id="elite_umbral_cindermaw_salamander",
            repair=repair,
            report=report,
            selected_source={"path": repair["model"], "sha256": "b" * 64},
            textures=[
                {
                    "path": repair["textures"][0],
                    "sha256": "d" * 64,
                    "dimensions": [4096, 4096],
                }
            ],
            packet_root=Path("unity/ArtSource/Terrestrials/RealmCreatureProductionSourceV001"),
            repo_root=Path("."),
        )
        self.assertTrue(any("uvOverlappingFaces" in item for item in diagnostics))
        self.assertTrue(any("baked-map" in item for item in diagnostics))

    def test_applies_v003_packet_revision_and_authored_normal_disposition(self):
        manifest = {
            "sourceVersion": "al-rcreature-2026-09-02-v001",
            "createdAtUtc": "old",
            "qualityBar": {"coverageDisposition": "old"},
            "provenance": {"editingSteps": ["old"], "editableSourceAvailability": "old"},
        }
        apply_packet_revision(manifest, "2026-09-03T05:06:40Z")
        self.assertEqual("al-rcreature-2026-09-03-v003", manifest["sourceVersion"])
        self.assertIn("Cindermaw", manifest["qualityBar"]["coverageDisposition"])
        self.assertIn("authored", manifest["qualityBar"]["coverageDisposition"])
        self.assertNotIn("neutral", manifest["qualityBar"]["coverageDisposition"])
        self.assertIn("Blender", manifest["provenance"]["editableSourceAvailability"])

    def test_applies_only_schema_summary_keys_and_synchronizes_quality_bar(self):
        manifest = {
            "summary": {"approved2D": 21, "runtimeIntegrationState": "Blocked", "stale": 99},
            "qualityBar": {"ownerTierTexturePackets": 3, "belowOwnerTierTexturePackets": 18},
            "models": [{"status": "clean_geometry_pass", "blocker": None, "textures": []}],
        }
        apply_summary_counts(manifest)
        self.assertEqual(
            {"approved2D", "structuralPass", "blocked3D", "ownerTierTexturePackets", "belowOwnerTierTexturePackets", "runtimeIntegrationState"},
            set(manifest["summary"]),
        )
        self.assertEqual(0, manifest["qualityBar"]["ownerTierTexturePackets"])
        self.assertEqual(1, manifest["qualityBar"]["belowOwnerTierTexturePackets"])

    def test_sunmane_repair_uses_recorded_v002_audit_report(self):
        self.assertTrue(REPAIRS["elite_eldergrove_sunmane_thornstag"]["report"].endswith("_v002.json"))

    def test_cindermaw_repair_promotes_smoothed_authored_normal_v004(self):
        repair = REPAIRS["elite_umbral_cindermaw_salamander"]
        self.assertTrue(repair["input"].endswith("_source_v003.fbx"))
        self.assertTrue(repair["model"].endswith("_source_v004.fbx"))
        self.assertTrue(repair["blend"].endswith("_normal_smoothing_v004.blend"))
        self.assertTrue(repair["report"].endswith("_normal_detail_v004.json"))
        self.assertTrue(repair["review"].endswith("_threequarter_v004.png"))
        self.assertTrue(all("normaldetail_v004" in path for path in repair["textures"]))
        self.assertEqual(
            "object_space_procedural_height_to_clean_uv_tangent_normal_v001",
            repair["normalProvenance"],
        )
        self.assertNotIn("normal_detail_rebuild_required", repair["status"])

    def test_updates_selected_source_and_clears_only_structural_blocker(self):
        record = {
            "modelId": "boss_test",
            "status": "manual_geometry_rebuild_required",
            "blocker": "bad geometry",
            "selectedSource": {"path": "old.fbx"},
            "textures": [{"path": "old.png"}],
            "meshyTaskIds": ["old-task"],
            "rigged": False,
            "runtimeIntegrationState": "Blocked",
            "productionReady": False,
        }
        update_repaired_source_record(
            record,
            selected_source={"path": "new.fbx"},
            textures=[],
            review={"path": "review.png"},
            status="clean_geometry_pass_texture_rebuild_required",
            task_ids=["new-task"],
        )
        self.assertEqual("new.fbx", record["selectedSource"]["path"])
        self.assertEqual([], record["textures"])
        self.assertIsNone(record["blocker"])
        self.assertEqual(["old-task", "new-task"], record["meshyTaskIds"])
        self.assertFalse(record["productionReady"])
        self.assertEqual("Blocked", record["runtimeIntegrationState"])

    def test_recomputes_structural_and_texture_tier_summary(self):
        models = []
        for index in range(21):
            textures = []
            if index < 3:
                textures = [
                    {"path": "base_color.png", "dimensions": [8192, 8192]},
                    {"path": "normal.png", "dimensions": [4096, 4096]},
                    {"path": "roughness.png", "dimensions": [4096, 4096]},
                    {"path": "metallic.png", "dimensions": [4096, 4096]},
                ]
            models.append({"status": "clean_geometry_pass", "blocker": None, "textures": textures})
        summary = recompute_summary(models)
        self.assertEqual(21, summary["structuralPass"])
        self.assertEqual(0, summary["blocked3D"])
        self.assertEqual(3, summary["ownerTierTexturePackets"])
        self.assertEqual(18, summary["belowOwnerTierTexturePackets"])
        self.assertEqual(
            {"structuralPass", "blocked3D", "ownerTierTexturePackets", "belowOwnerTierTexturePackets"},
            set(summary),
        )

    def test_neutral_normal_rebuild_status_is_not_counted_as_owner_tier(self):
        model = {
            "status": "clean_geometry_pass_uv_bake_complete_normal_detail_rebuild_required",
            "blocker": None,
            "textures": [
                {"path": "base_color.png", "dimensions": [8192, 8192]},
                {"path": "normal.png", "dimensions": [4096, 4096]},
                {"path": "roughness.png", "dimensions": [4096, 4096]},
                {"path": "metallic.png", "dimensions": [4096, 4096]},
            ],
        }
        summary = recompute_summary([model])
        self.assertEqual(0, summary["ownerTierTexturePackets"])
        self.assertEqual(1, summary["belowOwnerTierTexturePackets"])

    def test_runtime_derivatives_do_not_shadow_owner_tier_source_dimensions(self):
        model = {
            "status": "clean_geometry_pass",
            "blocker": None,
            "textures": [
                {"path": "source/base_color.png", "dimensions": [8192, 8192]},
                {"path": "source/normal.png", "dimensions": [4096, 4096]},
                {"path": "source/roughness.png", "dimensions": [4096, 4096]},
                {"path": "source/metallic.png", "dimensions": [4096, 4096]},
                {"path": "runtime/base_color.png", "dimensions": [2048, 2048]},
                {"path": "runtime/normal.png", "dimensions": [2048, 2048]},
            ],
        }
        self.assertEqual(1, recompute_summary([model])["ownerTierTexturePackets"])


if __name__ == "__main__":
    unittest.main()
