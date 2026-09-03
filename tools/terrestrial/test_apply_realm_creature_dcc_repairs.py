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
)


class ApplyRealmCreatureDccRepairsTests(unittest.TestCase):
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

    def test_applies_v002_packet_revision_and_honest_texture_disposition(self):
        manifest = {
            "sourceVersion": "al-rcreature-2026-09-02-v001",
            "createdAtUtc": "old",
            "qualityBar": {"coverageDisposition": "old"},
            "provenance": {"editingSteps": ["old"], "editableSourceAvailability": "old"},
        }
        apply_packet_revision(manifest, "2026-09-03T05:06:40Z")
        self.assertEqual("al-rcreature-2026-09-03-v002", manifest["sourceVersion"])
        self.assertIn("Cindermaw", manifest["qualityBar"]["coverageDisposition"])
        self.assertIn("neutral", manifest["qualityBar"]["coverageDisposition"])
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
