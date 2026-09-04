import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

import numpy as np
from PIL import Image

from tools.terrestrial.build_cindermaw_normal_detail import (
    anatomical_detail_strength,
    bounded_normal_gutter,
    build_normal_atlas,
    cellular_plate_with_gradient,
    cellular_pebble_with_gradient,
    cindermaw_height_gradient,
    export_surface_from_blender,
    main,
    relief_octaves,
    tangent_normals_from_object_gradient,
    triangle_pixel_samples,
    value_noise_with_gradient,
)


class CindermawNormalDetailTests(unittest.TestCase):
    def test_bounded_normal_gutter_keeps_far_atlas_space_neutral(self):
        rgba = np.zeros((9, 9, 4), dtype=np.uint8)
        rgba[4, 4] = [100, 150, 240, 255]

        rgb = bounded_normal_gutter(rgba, radius=2.0)

        np.testing.assert_array_equal(rgb[4, 4], [100, 150, 240])
        np.testing.assert_array_equal(rgb[4, 6], [100, 150, 240])
        np.testing.assert_array_equal(rgb[0, 0], [128, 128, 255])
        self.assertEqual(9 * 9 * 3, rgb.size)

    def test_anatomical_strength_prioritizes_torso_and_dorsal_surfaces(self):
        bounds_min = np.array([-0.42, -0.95, -0.25])
        bounds_max = np.array([0.43, 0.95, 0.25])
        points = np.array(
            [
                [0.0, -0.85, 0.15],
                [0.0, 0.00, 0.15],
                [0.0, 0.85, 0.15],
                [0.0, 0.00, -0.20],
            ]
        )
        strength, _ = anatomical_detail_strength(points, bounds_min, bounds_max)
        self.assertGreater(strength[1], strength[0])
        self.assertGreater(strength[1], strength[2])
        self.assertGreater(strength[1], strength[3])
        self.assertGreater(strength[3], 0.35)

    def test_cellular_plate_gradient_matches_finite_difference(self):
        points = np.array(
            [
                [0.137, -0.211, 0.083],
                [-0.293, 0.174, -0.121],
            ],
            dtype=np.float64,
        )
        rotation = relief_octaves()[1][3]
        values, gradients = cellular_plate_with_gradient(
            points,
            frequency=7.0,
            seed=9173,
            rotation=rotation,
            crease_width=0.28,
        )
        self.assertTrue(np.all((values >= 0.0) & (values <= 1.0)))
        epsilon = 1e-6
        for axis in range(3):
            delta = np.zeros_like(points)
            delta[:, axis] = epsilon
            plus, _ = cellular_plate_with_gradient(
                points + delta,
                frequency=7.0,
                seed=9173,
                rotation=rotation,
                crease_width=0.28,
            )
            minus, _ = cellular_plate_with_gradient(
                points - delta,
                frequency=7.0,
                seed=9173,
                rotation=rotation,
                crease_width=0.28,
            )
            finite_difference = (plus - minus) / (2.0 * epsilon)
            np.testing.assert_allclose(gradients[:, axis], finite_difference, atol=3e-4, rtol=3e-4)

    def test_cellular_pebble_gradient_matches_finite_difference(self):
        points = np.array([[0.113, -0.271, 0.079], [-0.204, 0.381, 0.142]])
        rotation = relief_octaves()[0][3]

        values, gradients = cellular_pebble_with_gradient(
            points, frequency=14.0, seed=4317, rotation=rotation
        )

        epsilon = 1e-6
        for axis in range(3):
            offset = np.zeros(3)
            offset[axis] = epsilon
            positive, _ = cellular_pebble_with_gradient(
                points + offset, frequency=14.0, seed=4317, rotation=rotation
            )
            negative, _ = cellular_pebble_with_gradient(
                points - offset, frequency=14.0, seed=4317, rotation=rotation
            )
            finite_difference = (positive - negative) / (2.0 * epsilon)
            np.testing.assert_allclose(gradients[:, axis], finite_difference, atol=2e-4)
        self.assertTrue(np.all((values >= 0.0) & (values <= 1.0)))

    def test_relief_octaves_use_rotated_isotropic_physical_space(self):
        octaves = relief_octaves()

        self.assertEqual([item[0] for item in octaves], [18.0, 36.0, 72.0])
        for _, _, _, rotation in octaves:
            np.testing.assert_allclose(rotation.T @ rotation, np.eye(3), atol=1e-12)
            self.assertAlmostEqual(float(np.linalg.det(rotation)), 1.0, places=12)
            self.assertLess(float(np.max(np.abs(rotation))), 0.98)

    def test_direct_script_build_resolves_sibling_dilation_module(self):
        uv = np.array([[[0.10, 0.10], [0.90, 0.10], [0.10, 0.90]]])
        positions = np.array(
            [[[-0.4, -0.8, 0.15], [0.4, -0.8, 0.15], [-0.4, 0.8, 0.15]]]
        )
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            surface_path = root / "surface.npz"
            output_path = root / "normal.png"
            metrics_path = root / "metrics.json"
            np.savez_compressed(
                surface_path,
                uv=uv,
                positions=positions,
                tangents=np.tile(np.array([1.0, 0.0, 0.0]), (1, 3, 1)),
                bitangents=np.tile(np.array([0.0, 1.0, 0.0]), (1, 3, 1)),
                bounds_min=np.array([-0.45, -0.95, -0.25]),
                bounds_max=np.array([0.45, 0.95, 0.25]),
                model_path=np.array("unity/example.fbx"),
                model_sha256=np.array("a" * 64),
                object_name=np.array("cindermaw"),
            )

            completed = subprocess.run(
                [
                    sys.executable,
                    str(Path(__file__).with_name("build_cindermaw_normal_detail.py")),
                    "build",
                    "--surface",
                    str(surface_path),
                    "--output",
                    str(output_path),
                    "--metrics",
                    str(metrics_path),
                    "--resolution",
                    "32",
                ],
                capture_output=True,
                text=True,
            )

            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertTrue(output_path.is_file())
            metrics = json.loads(metrics_path.read_text(encoding="utf-8"))
            self.assertEqual(metrics["strength"], 0.010)

    def test_blender_export_binds_triangle_surface_to_exact_model(self):
        loops = [
            SimpleNamespace(vertex_index=0, tangent=(1, 0, 0), bitangent=(0, 1, 0)),
            SimpleNamespace(vertex_index=1, tangent=(1, 0, 0), bitangent=(0, 1, 0)),
            SimpleNamespace(vertex_index=2, tangent=(1, 0, 0), bitangent=(0, 1, 0)),
        ]
        mesh = SimpleNamespace(
            polygons=[SimpleNamespace(loop_indices=[0, 1, 2])],
            loops=loops,
            vertices=[
                SimpleNamespace(co=(-0.4, -0.8, -0.2)),
                SimpleNamespace(co=(0.4, -0.8, 0.2)),
                SimpleNamespace(co=(-0.4, 0.8, 0.2)),
            ],
            uv_layers=SimpleNamespace(
                active=SimpleNamespace(
                    name="UVMap_Clean",
                    data=[
                        SimpleNamespace(uv=(0.1, 0.1)),
                        SimpleNamespace(uv=(0.9, 0.1)),
                        SimpleNamespace(uv=(0.1, 0.9)),
                    ],
                )
            ),
            calc_tangents=mock.Mock(),
        )
        world_matrix = np.array(
            [
                [1.0, 0.0, 0.0, 0.0],
                [0.0, 0.0, 1.0, 0.0],
                [0.0, -1.0, 0.0, 0.0],
                [0.0, 0.0, 0.0, 1.0],
            ]
        )
        fake_object = SimpleNamespace(
            type="MESH", name="cindermaw", data=mesh, matrix_world=world_matrix
        )
        fake_bpy = SimpleNamespace(
            ops=SimpleNamespace(
                wm=SimpleNamespace(read_factory_settings=mock.Mock()),
                import_scene=SimpleNamespace(fbx=mock.Mock()),
            ),
            context=SimpleNamespace(scene=SimpleNamespace(objects=[fake_object])),
        )
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            model_path = root / "model.fbx"
            model_path.write_bytes(b"exact model bytes")
            output_path = root / "surface.npz"
            with mock.patch.dict(sys.modules, {"bpy": fake_bpy}):
                export_surface_from_blender(
                    model_path,
                    output_path,
                    portable_model_path="unity/model.fbx",
                )
                with self.assertRaisesRegex(RuntimeError, "vertex count mismatch"):
                    export_surface_from_blender(
                        model_path,
                        root / "rejected_surface.npz",
                        portable_model_path="unity/model.fbx",
                        expected_vertices=4,
                        expected_triangles=1,
                    )

            with np.load(output_path, allow_pickle=False) as surface:
                self.assertEqual(surface["uv"].shape, (1, 3, 2))
                self.assertEqual(surface["positions"].shape, (1, 3, 3))
                np.testing.assert_allclose(surface["positions"][0, 0], [-0.4, -0.2, 0.8])
                np.testing.assert_allclose(surface["tangents"][0, 0], [1.0, 0.0, 0.0])
                np.testing.assert_allclose(surface["bitangents"][0, 0], [0.0, 0.0, -1.0])
                self.assertEqual(surface["model_path"].item(), "unity/model.fbx")
                self.assertEqual(len(surface["model_sha256"].item()), 64)
                self.assertEqual(surface["object_name"].item(), "cindermaw")
            mesh.calc_tangents.assert_called_once_with(uvmap="UVMap_Clean")

    def test_build_cli_writes_dilated_4k_ready_normal_and_metrics(self):
        uv = np.array([[[0.10, 0.10], [0.90, 0.10], [0.10, 0.90]]])
        positions = np.array(
            [[[-0.4, -0.8, 0.15], [0.4, -0.8, 0.15], [-0.4, 0.8, 0.15]]]
        )
        tangents = np.tile(np.array([1.0, 0.0, 0.0]), (1, 3, 1))
        bitangents = np.tile(np.array([0.0, 1.0, 0.0]), (1, 3, 1))
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            surface_path = root / "surface.npz"
            output_path = root / "normal.png"
            metrics_path = root / "metrics.json"
            np.savez_compressed(
                surface_path,
                uv=uv,
                positions=positions,
                tangents=tangents,
                bitangents=bitangents,
                bounds_min=np.array([-0.45, -0.95, -0.25]),
                bounds_max=np.array([0.45, 0.95, 0.25]),
                model_path=np.array("unity/example.fbx"),
                model_sha256=np.array("a" * 64),
                object_name=np.array("cindermaw"),
            )

            result = main(
                [
                    "build",
                    "--surface",
                    str(surface_path),
                    "--output",
                    str(output_path),
                    "--metrics",
                    str(metrics_path),
                    "--resolution",
                    "32",
                    "--strength",
                    "0.012",
                ]
            )

            self.assertEqual(result, 0)
            with Image.open(output_path) as image:
                self.assertEqual(image.mode, "RGB")
                self.assertEqual(image.size, (32, 32))
                self.assertEqual(image.getpixel((31, 0)), (128, 128, 255))
            metrics = json.loads(metrics_path.read_text(encoding="utf-8"))
            self.assertEqual(metrics["status"], "PASS")
            self.assertEqual(metrics["modelSha256"], "a" * 64)
            self.assertEqual(metrics["dimensions"], [32, 32])
            self.assertEqual(metrics.get("gutterRadiusPixels"), 2.0)
            self.assertEqual(metrics.get("atlasBackground"), "neutral_tangent")
            self.assertEqual(len(metrics["outputSha256"]), 64)
            coordinate_frame = metrics.get("coordinateFrame", {})
            self.assertEqual(coordinate_frame.get("longitudinalAxis"), "world Y")
            self.assertEqual(coordinate_frame.get("dorsalAxis"), "world Z")
            np.testing.assert_allclose(
                coordinate_frame.get("span", []),
                [0.9, 1.9, 0.5],
            )

    def test_build_cli_rejects_surface_whose_long_axis_is_not_world_y(self):
        uv = np.array([[[0.10, 0.10], [0.90, 0.10], [0.10, 0.90]]])
        positions = np.array(
            [[[-0.4, -0.2, -0.8], [0.4, -0.2, -0.8], [-0.4, 0.2, 0.8]]]
        )
        tangents = np.tile(np.array([1.0, 0.0, 0.0]), (1, 3, 1))
        bitangents = np.tile(np.array([0.0, 1.0, 0.0]), (1, 3, 1))
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            surface_path = root / "surface.npz"
            np.savez_compressed(
                surface_path,
                uv=uv,
                positions=positions,
                tangents=tangents,
                bitangents=bitangents,
                bounds_min=np.array([-0.45, -0.25, -0.95]),
                bounds_max=np.array([0.45, 0.25, 0.95]),
                model_path=np.array("unity/example.fbx"),
                model_sha256=np.array("a" * 64),
                object_name=np.array("cindermaw"),
            )

            with self.assertRaisesRegex(ValueError, "world Y must be longitudinal"):
                main(
                    [
                        "build",
                        "--surface",
                        str(surface_path),
                        "--output",
                        str(root / "normal.png"),
                        "--metrics",
                        str(root / "metrics.json"),
                        "--resolution",
                        "32",
                    ]
                )

    def test_build_normal_atlas_writes_non_neutral_unit_relief_inside_uvs(self):
        uv = np.array([[[0.10, 0.10], [0.90, 0.10], [0.10, 0.90]]])
        positions = np.array(
            [[[-0.4, -0.8, 0.15], [0.4, -0.8, 0.15], [-0.4, 0.8, 0.15]]]
        )
        tangents = np.tile(np.array([1.0, 0.0, 0.0]), (1, 3, 1))
        bitangents = np.tile(np.array([0.0, 1.0, 0.0]), (1, 3, 1))

        rgba, metrics = build_normal_atlas(
            uv,
            positions,
            tangents,
            bitangents,
            bounds_min=np.array([-0.45, -0.95, -0.25]),
            bounds_max=np.array([0.45, 0.95, 0.25]),
            resolution=32,
            strength=0.012,
        )

        covered = rgba[:, :, 3] > 0
        self.assertGreater(int(covered.sum()), 200)
        self.assertFalse(np.all(rgba[:, :, :3][covered] == np.array([128, 128, 255])))
        self.assertGreater(metrics["angularP95Degrees"], 2.0)
        self.assertLess(metrics["angularP95Degrees"], 30.0)
        self.assertLess(metrics["unitLengthMaxError"], 0.01)

    def test_triangle_pixel_samples_follow_bottom_left_uv_coordinates(self):
        uv = np.array([[0.25, 0.25], [0.75, 0.25], [0.25, 0.75]])

        rows, columns, barycentric = triangle_pixel_samples(uv, resolution=16)

        self.assertGreater(len(rows), 0)
        self.assertTrue(np.all(barycentric >= -1e-10))
        np.testing.assert_allclose(barycentric.sum(axis=1), 1.0, atol=1e-12)
        sampled_uv = np.column_stack(
            ((columns + 0.5) / 16.0, 1.0 - (rows + 0.5) / 16.0)
        )
        np.testing.assert_allclose(barycentric @ uv, sampled_uv, atol=1e-12)

    def test_cindermaw_relief_is_deterministic_and_stronger_dorsally(self):
        samples = [(x, y) for x in np.linspace(-0.3, 0.3, 5) for y in np.linspace(-0.8, 0.8, 7)]
        points = np.array(
            [[x, y, 0.18] for x, y in samples] + [[x, y, -0.18] for x, y in samples]
        )
        bounds_min = np.array([-0.45, -0.95, -0.25])
        bounds_max = np.array([0.45, 0.95, 0.25])

        heights, gradients = cindermaw_height_gradient(points, bounds_min, bounds_max)
        repeated_heights, repeated_gradients = cindermaw_height_gradient(
            points, bounds_min, bounds_max
        )

        np.testing.assert_array_equal(heights, repeated_heights)
        np.testing.assert_array_equal(gradients, repeated_gradients)
        self.assertTrue(np.isfinite(heights).all())
        self.assertTrue(np.isfinite(gradients).all())
        half = len(samples)
        self.assertTrue(np.all(heights[:half] > heights[half:]))
        self.assertGreater(
            np.mean(np.linalg.norm(gradients[:half], axis=1)),
            np.mean(np.linalg.norm(gradients[half:], axis=1)),
        )

    def test_object_gradient_encodes_as_unit_tangent_normal(self):
        gradients = np.array([[0.0, 0.0, 0.0], [8.0, -4.0, 2.0]])
        tangents = np.array([[1.0, 0.0, 0.0], [1.0, 0.0, 0.0]])
        bitangents = np.array([[0.0, 1.0, 0.0], [0.0, 1.0, 0.0]])

        normals = tangent_normals_from_object_gradient(
            gradients,
            tangents,
            bitangents,
            strength=0.025,
        )

        np.testing.assert_allclose(normals[0], [0.0, 0.0, 1.0], atol=1e-12)
        np.testing.assert_allclose(np.linalg.norm(normals, axis=1), [1.0, 1.0], atol=1e-12)
        self.assertLess(normals[1, 0], 0.0)
        self.assertGreater(normals[1, 1], 0.0)
        self.assertGreater(normals[1, 2], 0.95)

    def test_tangent_projection_reorthonormalizes_the_interpolated_basis(self):
        normals = tangent_normals_from_object_gradient(
            np.array([[1.0, 0.0, 0.0]]),
            np.array([[1.0, 0.0, 0.0]]),
            np.array([[1.0, 1.0, 0.0]]),
            strength=0.1,
        )

        self.assertLess(normals[0, 0], 0.0)
        self.assertAlmostEqual(0.0, normals[0, 1], places=12)
        np.testing.assert_allclose(np.linalg.norm(normals, axis=1), [1.0], atol=1e-12)

    def test_value_noise_gradient_matches_finite_difference(self):
        points = np.array(
            [
                [0.173, 0.421, 0.689],
                [1.317, -0.284, 0.932],
                [-0.731, 0.118, 1.513],
            ],
            dtype=np.float64,
        )
        frequency = np.array([3.0, 4.0, 5.0], dtype=np.float64)

        values, gradients = value_noise_with_gradient(points, frequency, seed=1701)

        epsilon = 1e-5
        numerical = np.zeros_like(gradients)
        for axis in range(3):
            offset = np.zeros(3, dtype=np.float64)
            offset[axis] = epsilon
            high, _ = value_noise_with_gradient(points + offset, frequency, seed=1701)
            low, _ = value_noise_with_gradient(points - offset, frequency, seed=1701)
            numerical[:, axis] = (high - low) / (2.0 * epsilon)
        repeated, repeated_gradients = value_noise_with_gradient(points, frequency, seed=1701)

        np.testing.assert_allclose(gradients, numerical, atol=2e-4, rtol=2e-4)
        np.testing.assert_array_equal(values, repeated)
        np.testing.assert_array_equal(gradients, repeated_gradients)


if __name__ == "__main__":
    unittest.main()
