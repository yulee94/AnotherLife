import unittest

from tools.terrestrial.repair_cindermaw_uv_bake import (
    atlas_finalization_mode,
    export_uv_layer_names,
    normal_bake_strategy,
    triangle_overlap_area,
    uv_topology_strategy,
    uv_pack_strategy,
    validate_uv_bake_report,
)


class CindermawUvBakeTests(unittest.TestCase):
    def test_defers_large_atlas_finalization_until_blender_releases_memory(self):
        self.assertEqual("external_process", atlas_finalization_mode())

    def test_uses_non_overlapping_lightmap_pack_for_triangulated_source(self):
        self.assertEqual("triangulated_lightmap_pack", uv_pack_strategy())

    def test_triangulates_before_unwrap_to_prevent_concave_quad_overlap(self):
        self.assertEqual("triangulate_before_unwrap", uv_topology_strategy())

    def test_exports_only_the_clean_uv_authority(self):
        self.assertEqual(("UVMap_Clean",), export_uv_layer_names())

    def test_uses_neutral_tangent_fallback_instead_of_corrupt_selected_to_active_bake(self):
        self.assertEqual("neutral_tangent", normal_bake_strategy())


    @staticmethod
    def _valid_report():
        return {
            "modelId": "elite_umbral_cindermaw_salamander",
            "sourceTaskIds": ["task-a", "task-b"],
            "inputSha256": "a" * 64,
            "outputSha256": "b" * 64,
            "status": "clean_geometry_pass_uv_bake_complete_normal_detail_rebuild_required",
            "productionReady": False,
            "rigged": False,
            "runtimeIntegrationState": "Blocked",
            "metrics": {
                "uvLayer": "UVMap_Clean",
                "uvFacesOutsideUnit": 0,
                "uvZeroAreaFaces": 0,
                "uvOverlappingFaces": 0,
                "nonManifoldEdgesBefore": 53,
                "nonManifoldEdgesAfter": 53,
                "polygonalProjectionBlockerResolved": True,
            },
            "bakedMaps": [
                {"name": "base_color", "dimensions": [8192, 8192], "sha256": "c" * 64},
                {"name": "normal", "dimensions": [4096, 4096], "sha256": "d" * 64},
                {"name": "roughness", "dimensions": [4096, 4096], "sha256": "e" * 64},
                {"name": "metallic", "dimensions": [4096, 4096], "sha256": "f" * 64},
                {"name": "ao", "dimensions": [4096, 4096], "sha256": "1" * 64},
            ],
        }

    def test_accepts_complete_fail_closed_uv_bake_report(self):
        self.assertEqual([], validate_uv_bake_report(self._valid_report()))

    def test_rejects_overlap_missing_maps_and_false_readiness(self):
        report = self._valid_report()
        report["productionReady"] = True
        report["metrics"]["uvOverlappingFaces"] = 14
        report["metrics"]["polygonalProjectionBlockerResolved"] = False
        report["bakedMaps"] = report["bakedMaps"][:-1]

        errors = validate_uv_bake_report(report)

        self.assertIn("productionReady must remain false", errors)
        self.assertIn("uvOverlappingFaces must equal 0", errors)
        self.assertIn("polygonalProjectionBlockerResolved must be true", errors)
        self.assertIn("baked maps must include ao, base_color, metallic, normal, and roughness", errors)

    def test_rejects_downsampled_owner_tier_maps(self):
        report = self._valid_report()
        report["bakedMaps"][0]["dimensions"] = [4096, 4096]
        report["bakedMaps"][1]["dimensions"] = [2048, 2048]

        errors = validate_uv_bake_report(report)

        self.assertIn("base_color must be 8192x8192", errors)
        self.assertIn("normal must be 4096x4096", errors)

    def test_triangle_overlap_ignores_touching_edges(self):
        first = ((0.0, 0.0), (1.0, 0.0), (0.0, 1.0))
        second = ((1.0, 0.0), (1.0, 1.0), (0.0, 1.0))

        self.assertAlmostEqual(0.0, triangle_overlap_area(first, second))

    def test_triangle_overlap_measures_positive_intersection(self):
        first = ((0.0, 0.0), (1.0, 0.0), (0.0, 1.0))
        second = ((0.2, 0.2), (0.6, 0.2), (0.2, 0.6))

        self.assertAlmostEqual(0.08, triangle_overlap_area(first, second), places=6)


if __name__ == "__main__":
    unittest.main()
