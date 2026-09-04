import hashlib
import json
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image

from tools.terrestrial.cindermaw_visual_polish_v005 import (
    CONCEPT_SHEET_SHA256,
    EXPECTED_TRIANGLES,
    EXPECTED_VERTICES,
    MODEL_ID,
    V004_MODEL_SHA256,
    localized_snout_offsets,
    material_region_weights,
    polish_support_maps,
    review_specs,
    validate_localized_geometry,
    validate_material_separation,
    validate_readiness,
)


BOUNDS_MIN = np.array([-0.4188709855079651, -0.9499970078468323, -0.24544595181941986])
BOUNDS_MAX = np.array([0.4271009862422943, 0.9491789937019348, 0.24447400867938995])


def _grid_points(count: int = 21) -> np.ndarray:
    xs = np.linspace(BOUNDS_MIN[0], BOUNDS_MAX[0], count)
    ys = np.linspace(BOUNDS_MIN[1], BOUNDS_MAX[1], count)
    zs = np.linspace(BOUNDS_MIN[2], BOUNDS_MAX[2], 9)
    return np.array([[x, y, z] for y in ys for z in zs for x in xs], dtype=np.float64)


class CindermawVisualPolishV005Tests(unittest.TestCase):
    def test_keeps_v004_identity_and_topology_constants(self):
        self.assertEqual("elite_umbral_cindermaw_salamander", MODEL_ID)
        self.assertEqual(27690, EXPECTED_VERTICES)
        self.assertEqual(55334, EXPECTED_TRIANGLES)
        self.assertEqual(
            "9486be45241afe61ba04b4f2fedc4d751819acfd2e0d6181a97c1e4cddb2b9d6",
            V004_MODEL_SHA256,
        )
        self.assertEqual(
            "61a5ea43950826a19dc344c3e8f0413cd78457b33cb85c0aeff52a2e9eb872ee",
            CONCEPT_SHEET_SHA256,
        )

    def test_snout_offsets_are_localized_and_leave_body_unmoved(self):
        points = _grid_points()
        offsets = localized_snout_offsets(points, BOUNDS_MIN, BOUNDS_MAX)
        span = BOUNDS_MAX - BOUNDS_MIN
        longitudinal = (points[:, 1] - BOUNDS_MIN[1]) / span[1]
        body = longitudinal > 0.32
        snout = longitudinal < 0.18
        self.assertEqual(points.shape, offsets.shape)
        self.assertTrue(np.all(offsets[body] == 0.0))
        self.assertGreater(np.max(np.linalg.norm(offsets[snout], axis=1)), 0.004)
        self.assertLess(np.max(np.linalg.norm(offsets, axis=1)), 0.02)

    def test_snout_offsets_add_nostril_pits_and_reduce_planar_width(self):
        y = BOUNDS_MIN[1] + 0.10 * (BOUNDS_MAX[1] - BOUNDS_MIN[1])
        z_dorsal = BOUNDS_MIN[2] + 0.70 * (BOUNDS_MAX[2] - BOUNDS_MIN[2])
        left = np.array([[-0.09, y, z_dorsal]])
        right = np.array([[0.09, y, z_dorsal]])
        center = np.array([[0.0, y, z_dorsal]])
        tip = np.array([[0.16, BOUNDS_MIN[1] + 0.04 * (BOUNDS_MAX[1] - BOUNDS_MIN[1]), z_dorsal]])
        left_offset = localized_snout_offsets(left, BOUNDS_MIN, BOUNDS_MAX)[0]
        right_offset = localized_snout_offsets(right, BOUNDS_MIN, BOUNDS_MAX)[0]
        center_offset = localized_snout_offsets(center, BOUNDS_MIN, BOUNDS_MAX)[0]
        tip_offset = localized_snout_offsets(tip, BOUNDS_MIN, BOUNDS_MAX)[0]
        self.assertLess(left_offset[2], -0.001)
        self.assertLess(right_offset[2], -0.001)
        self.assertGreater(center_offset[2], left_offset[2])
        self.assertLess(tip_offset[0], 0.0)

    def test_material_regions_separate_hide_fins_scars_underside_and_confine_ember(self):
        points = _grid_points()
        weights = material_region_weights(points, BOUNDS_MIN, BOUNDS_MAX)
        span = BOUNDS_MAX - BOUNDS_MIN
        ventral = (points[:, 2] - BOUNDS_MIN[2]) / span[2] < 0.28
        dorsal = (points[:, 2] - BOUNDS_MIN[2]) / span[2] > 0.78
        torso = ((points[:, 1] - BOUNDS_MIN[1]) / span[1] > 0.28) & (
            (points[:, 1] - BOUNDS_MIN[1]) / span[1] < 0.72
        )
        self.assertGreater(float(weights["underside"][ventral].mean()), 0.55)
        self.assertGreater(float(weights["fins"][dorsal & torso].mean()), 0.35)
        self.assertGreater(float(weights["hide"].mean()), 0.25)
        self.assertLess(float(weights["ember"].mean()), 0.045)
        self.assertGreater(float(weights["scars"][dorsal & torso].mean()), float(weights["scars"][ventral].mean()))

    def test_support_map_polish_strengthens_material_separation(self):
        resolution = 32
        rgb = np.full((resolution, resolution, 3), 70, dtype=np.uint8)
        roughness = np.full((resolution, resolution), 140, dtype=np.uint8)
        metallic = np.full((resolution, resolution), 8, dtype=np.uint8)
        weights = {
            "hide": np.zeros((resolution, resolution), dtype=np.float64),
            "fins": np.zeros((resolution, resolution), dtype=np.float64),
            "scars": np.zeros((resolution, resolution), dtype=np.float64),
            "underside": np.zeros((resolution, resolution), dtype=np.float64),
            "ember": np.zeros((resolution, resolution), dtype=np.float64),
        }
        weights["hide"][:, :8] = 1.0
        weights["fins"][:, 8:14] = 1.0
        weights["scars"][:, 14:20] = 1.0
        weights["underside"][:, 20:28] = 1.0
        weights["ember"][:, 28:] = 1.0
        polished = polish_support_maps(rgb, roughness, metallic, weights)
        samples = {
            name: {
                "albedo": polished["base_color"][:, slice_].astype(np.float64).mean(axis=(0, 1)) / 255.0,
                "roughness": polished["roughness"][:, slice_].astype(np.float64).mean() / 255.0,
                "metallic": polished["metallic"][:, slice_].astype(np.float64).mean() / 255.0,
            }
            for name, slice_ in (
                ("hide", slice(0, 8)),
                ("fins", slice(8, 14)),
                ("scars", slice(14, 20)),
                ("underside", slice(20, 28)),
                ("ember", slice(28, 32)),
            )
        }
        diagnostics = validate_material_separation(samples)
        self.assertEqual([], diagnostics)
        self.assertLess(samples["hide"]["albedo"].mean(), samples["scars"]["albedo"].mean())
        self.assertLess(samples["fins"]["roughness"], samples["hide"]["roughness"])
        self.assertLess(samples["fins"]["roughness"], samples["underside"]["roughness"])
        self.assertGreater(samples["fins"]["metallic"], samples["hide"]["metallic"])
        self.assertLess(samples["ember"]["albedo"][0], 0.35)

    def test_geometry_validator_rejects_generic_retopo_and_global_moves(self):
        before = _grid_points(9)
        after = before.copy()
        after[-1] += (0.01, 0.0, 0.0)
        diagnostics = validate_localized_geometry(
            before,
            after,
            bounds_min=BOUNDS_MIN,
            bounds_max=BOUNDS_MAX,
            expected_vertices=len(before),
            expected_triangles=16,
            actual_triangles=16,
        )
        self.assertTrue(any("non-snout" in item for item in diagnostics))

        diagnostics = validate_localized_geometry(
            before,
            before,
            bounds_min=BOUNDS_MIN,
            bounds_max=BOUNDS_MAX,
            expected_vertices=len(before) + 1,
            expected_triangles=16,
            actual_triangles=16,
        )
        self.assertTrue(any("vertex count" in item for item in diagnostics))

    def test_reviews_require_neutral_closeup_and_full_body_hero_without_runtime_vfx(self):
        specs = review_specs()
        names = {item["name"] for item in specs}
        self.assertEqual({"neutral_closeup", "full_body_hero"}, names)
        by_name = {item["name"]: item for item in specs}
        self.assertEqual("neutral", by_name["neutral_closeup"]["lighting"])
        self.assertEqual("hero", by_name["full_body_hero"]["lighting"])
        self.assertTrue(by_name["neutral_closeup"]["path"].endswith("_neutral_closeup_v005.png"))
        self.assertTrue(by_name["full_body_hero"]["path"].endswith("_fullbody_hero_v005.png"))
        for spec in specs:
            self.assertFalse(spec["runtimeVfxIncluded"])
            self.assertTrue(spec["modelPath"].endswith("_source_v005.fbx"))
            self.assertIn("visualpolish_v005", spec["textureRoot"])

    def test_readiness_stays_blocked_and_unrigged(self):
        diagnostics = validate_readiness(
            {
                "productionReady": False,
                "rigged": False,
                "runtimeIntegrationState": "Blocked",
                "status": "clean_geometry_pass_uv_bake_pass_smoothing_pass_normal_detail_pass_visual_polish_v005_pass_rigging_required",
            }
        )
        self.assertEqual([], diagnostics)
        self.assertIn(
            "productionReady must remain false",
            validate_readiness({"productionReady": True, "rigged": False, "runtimeIntegrationState": "Blocked", "status": "x"}),
        )


class FinalizeCindermawVisualPolishV005Tests(unittest.TestCase):
    def test_packages_v005_maps_reviews_and_preserves_v004_input_hash(self):
        from tools.terrestrial.finalize_cindermaw_visual_polish_v005 import (
            finalize_visual_polish_packet,
        )

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            model = root / "unity/model_v005.fbx"
            blend = root / "unity/model_v005.blend"
            source_v004 = root / "unity/model_v004.fbx"
            output_textures = root / "unity/textures_v005"
            closeup = root / "unity/closeup.png"
            hero = root / "unity/hero.png"
            output_report = root / "unity/visual_polish_v005.json"
            for path, payload in (
                (model, b"v005-model"),
                (blend, b"v005-blend"),
                (source_v004, b"v004-model"),
            ):
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(payload)
            output_textures.mkdir(parents=True)
            Image.new("RGB", (16, 16), (18, 20, 24)).save(output_textures / "base_color.png")
            Image.new("RGB", (8, 8), (128, 128, 255)).save(output_textures / "normal.png")
            Image.new("L", (8, 8), 90).save(output_textures / "roughness.png")
            Image.new("L", (8, 8), 20).save(output_textures / "metallic.png")
            Image.new("L", (8, 8), 180).save(output_textures / "ao.png")
            Image.new("RGB", (8, 8), (30, 32, 36)).save(closeup)
            Image.new("RGB", (8, 8), (22, 18, 16)).save(hero)
            report = finalize_visual_polish_packet(
                repo_root=root,
                input_model_path=source_v004,
                output_model_path=model,
                editable_blend_path=blend,
                texture_dir=output_textures,
                reviews={
                    "neutral_closeup": closeup,
                    "full_body_hero": hero,
                },
                output_report_path=output_report,
                geometry_metrics={
                    "vertices": 27690,
                    "polygons": 55334,
                    "movedSnoutVertices": 812,
                    "unchangedNonSnoutVertices": 26878,
                    "uvLayer": "UVMap_Clean",
                    "uvFacesOutsideUnit": 0,
                    "uvZeroAreaFaces": 0,
                    "uvOverlappingFaces": 0,
                },
                expected_base_resolution=16,
                expected_support_resolution=8,
            )
            self.assertFalse(report["productionReady"])
            self.assertFalse(report["rigged"])
            self.assertEqual("Blocked", report["runtimeIntegrationState"])
            self.assertEqual(
                hashlib.sha256(b"v004-model").hexdigest(),
                report["inputSha256"],
            )
            self.assertTrue(output_report.is_file())
            loaded = json.loads(output_report.read_text(encoding="utf-8"))
            self.assertEqual(2, len(loaded["reviews"]))
            self.assertIn("visual_polish_v005_pass", loaded["status"])
            records = {item["name"]: item for item in loaded["bakedMaps"]}
            self.assertEqual(
                "object_space_procedural_height_to_clean_uv_tangent_normal_v001",
                records["normal"]["provenance"],
            )


if __name__ == "__main__":
    unittest.main()
