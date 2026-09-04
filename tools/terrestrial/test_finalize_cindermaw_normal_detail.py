import hashlib
import json
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image

from tools.terrestrial.finalize_cindermaw_normal_detail import (
    finalize_normal_detail_packet,
    validate_normal_detail_evidence,
)


class FinalizeCindermawNormalDetailTests(unittest.TestCase):
    def test_packages_bound_support_maps_and_fail_closed_report(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            model = root / "unity/model_v004.fbx"
            blend = root / "unity/model_v004.blend"
            source_textures = root / "unity/textures_v003"
            output_textures = root / "unity/textures_v004"
            review = root / "unity/review.png"
            uv_report_path = root / "unity/uv_v003.json"
            smoothing_report_path = root / "unity/smoothing_v004.json"
            output_report = root / "unity/normal_v004.json"
            for path in (model, blend):
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(path.name.encode("utf-8"))
            source_textures.mkdir(parents=True)
            output_textures.mkdir(parents=True)
            for name, size in (
                ("base_color.png", 16),
                ("roughness.png", 8),
                ("metallic.png", 8),
                ("ao.png", 8),
            ):
                Image.new("RGB", (size, size), (64, 72, 80)).save(source_textures / name)
            normal = output_textures / "normal.png"
            pixels = np.zeros((8, 8, 3), dtype=np.uint8)
            pixels[..., 0] = np.tile(np.array([112, 144], dtype=np.uint8), (8, 4))
            pixels[..., 1] = 128
            pixels[..., 2] = 253
            Image.fromarray(pixels, mode="RGB").save(normal)
            Image.new("RGB", (8, 8), (20, 24, 30)).save(review)
            model_hash = hashlib.sha256(model.read_bytes()).hexdigest()
            uv_report_path.write_text(
                json.dumps(
                    {
                        "input": model.relative_to(root).as_posix(),
                        "inputSha256": model_hash,
                        "uvLayer": "UVMap_Clean",
                        "uvFacesOutsideUnit": 0,
                        "uvZeroAreaFaces": 0,
                        "uvOverlappingFaces": 0,
                        "diagnostics": [],
                    }
                ),
                encoding="utf-8",
            )
            smoothing_report_path.write_text(
                json.dumps(
                    {
                        "diagnostics": [],
                        "outputSha256": model_hash,
                        "editableBlend": blend.relative_to(root).as_posix(),
                        "editableBlendSha256": hashlib.sha256(blend.read_bytes()).hexdigest(),
                        "metrics": {
                            "sharpEdgesBefore": 53054,
                            "sharpEdgesAfter": 631,
                            "customNormalsRemoved": True,
                        },
                    }
                ),
                encoding="utf-8",
            )
            for evidence_path in (uv_report_path, smoothing_report_path):
                evidence_path.write_bytes(evidence_path.read_bytes() + b"\r\n")
            metrics = {
                "status": "PASS",
                "method": "object_space_procedural_height_to_clean_uv_tangent_normal_v001",
                "dimensions": [8, 8],
                "strength": 0.010,
                "gutterRadiusPixels": 2.0,
                "atlasBackground": "neutral_tangent",
                "orientation": "OpenGL +Y",
                "runtimeVfxSeparate": True,
                "authoredNormalDetail": True,
                "coordinateFrame": {
                    "lateralAxis": "world X",
                    "longitudinalAxis": "world Y",
                    "dorsalAxis": "world Z",
                    "span": [0.9, 1.9, 0.5],
                },
                "modelSha256": model_hash,
                "outputSha256": hashlib.sha256(normal.read_bytes()).hexdigest(),
                "metrics": {
                    "angularP50Degrees": 4.0,
                    "angularP95Degrees": 9.0,
                    "angularMaxDegrees": 18.0,
                    "unitLengthMaxError": 1e-12,
                },
            }

            report = finalize_normal_detail_packet(
                repo_root=root,
                model_path=model,
                source_texture_dir=source_textures,
                output_texture_dir=output_textures,
                metrics=metrics,
                uv_report_path=uv_report_path,
                smoothing_report_path=smoothing_report_path,
                review_path=review,
                output_report_path=output_report,
                expected_base_resolution=16,
                expected_support_resolution=8,
            )

            self.assertFalse(report["productionReady"])
            self.assertEqual("Blocked", report["runtimeIntegrationState"])
            self.assertEqual(
                hashlib.sha256(blend.read_bytes()).hexdigest(),
                report.get("editableBlendSha256"),
            )
            self.assertIn("normal_detail_pass", report["status"])
            self.assertEqual([], report["diagnostics"])
            normalized_uv_hash = hashlib.sha256(
                uv_report_path.read_bytes().replace(b"\r\n", b"\n")
            ).hexdigest()
            normalized_smoothing_hash = hashlib.sha256(
                smoothing_report_path.read_bytes().replace(b"\r\n", b"\n")
            ).hexdigest()
            self.assertEqual(normalized_uv_hash, report["sourceUvEvidence"]["sha256"])
            self.assertEqual(
                normalized_smoothing_hash,
                report["smoothingEvidence"]["sha256"],
            )
            records = {item["name"]: item for item in report["bakedMaps"]}
            self.assertEqual(
                "object_space_procedural_height_to_clean_uv_tangent_normal_v001",
                records["normal"]["provenance"],
            )
            self.assertEqual(
                (source_textures / "base_color.png").read_bytes(),
                (output_textures / "base_color.png").read_bytes(),
            )
            self.assertTrue(output_report.is_file())

    def test_accepts_bound_non_neutral_normal_and_rejects_neutral_or_wrong_model(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            model = root / "model.fbx"
            normal = root / "normal.png"
            model.write_bytes(b"model")
            pixels = np.zeros((8, 8, 3), dtype=np.uint8)
            pixels[..., 0] = np.tile(np.array([112, 144], dtype=np.uint8), (8, 4))
            pixels[..., 1] = 128
            pixels[..., 2] = 253
            Image.fromarray(pixels, mode="RGB").save(normal)
            metrics = {
                "status": "PASS",
                "method": "object_space_procedural_height_to_clean_uv_tangent_normal_v001",
                "dimensions": [8, 8],
                "strength": 0.010,
                "gutterRadiusPixels": 2.0,
                "atlasBackground": "neutral_tangent",
                "orientation": "OpenGL +Y",
                "runtimeVfxSeparate": True,
                "authoredNormalDetail": True,
                "coordinateFrame": {
                    "lateralAxis": "world X",
                    "longitudinalAxis": "world Y",
                    "dorsalAxis": "world Z",
                    "span": [0.9, 1.9, 0.5],
                },
                "modelSha256": hashlib.sha256(b"model").hexdigest(),
                "outputSha256": hashlib.sha256(normal.read_bytes()).hexdigest(),
                "metrics": {
                    "angularP50Degrees": 4.0,
                    "angularP95Degrees": 9.0,
                    "angularMaxDegrees": 18.0,
                    "unitLengthMaxError": 1e-12,
                },
            }

            self.assertEqual(
                [],
                validate_normal_detail_evidence(
                    metrics,
                    normal_path=normal,
                    model_path=model,
                    expected_resolution=8,
                ),
            )

            metrics["coordinateFrame"]["longitudinalAxis"] = "world Z"
            diagnostics = validate_normal_detail_evidence(
                metrics,
                normal_path=normal,
                model_path=model,
                expected_resolution=8,
            )
            self.assertIn("metrics coordinateFrame axes are invalid", diagnostics)
            metrics["coordinateFrame"]["longitudinalAxis"] = "world Y"

            metrics["gutterRadiusPixels"] = 3.0
            diagnostics = validate_normal_detail_evidence(
                metrics,
                normal_path=normal,
                model_path=model,
                expected_resolution=8,
                expected_strength=0.01,
            )
            self.assertIn("normal gutter radius must be exactly two pixels", diagnostics)
            metrics["gutterRadiusPixels"] = 2.0

            metrics["atlasBackground"] = "nearest_island"
            diagnostics = validate_normal_detail_evidence(
                metrics,
                normal_path=normal,
                model_path=model,
                expected_resolution=8,
                expected_strength=0.01,
            )
            self.assertIn("normal atlas background must stay neutral tangent", diagnostics)
            metrics["atlasBackground"] = "neutral_tangent"

            overstrong = np.zeros((8, 8, 3), dtype=np.uint8)
            overstrong[..., 0] = 191
            overstrong[..., 1] = 128
            overstrong[..., 2] = 238
            Image.fromarray(overstrong, mode="RGB").save(normal)
            metrics["outputSha256"] = hashlib.sha256(normal.read_bytes()).hexdigest()
            diagnostics = validate_normal_detail_evidence(
                metrics,
                normal_path=normal,
                model_path=model,
                expected_resolution=8,
            )
            self.assertIn("normal map pixel P95 angle exceeds 20 degrees", diagnostics)

            sparse_spike = pixels.copy()
            sparse_spike[0, 0] = np.array([218, 128, 218], dtype=np.uint8)
            Image.fromarray(sparse_spike, mode="RGB").save(normal)
            metrics["outputSha256"] = hashlib.sha256(normal.read_bytes()).hexdigest()
            diagnostics = validate_normal_detail_evidence(
                metrics,
                normal_path=normal,
                model_path=model,
                expected_resolution=8,
            )
            self.assertIn("normal map pixel maximum angle exceeds 35 degrees", diagnostics)

            neutral = np.zeros((8, 8, 3), dtype=np.uint8)
            neutral[..., 0] = 128
            neutral[..., 1] = 128
            neutral[..., 2] = 255
            Image.fromarray(neutral, mode="RGB").save(normal)
            metrics["outputSha256"] = hashlib.sha256(normal.read_bytes()).hexdigest()
            diagnostics = validate_normal_detail_evidence(
                metrics,
                normal_path=normal,
                model_path=model,
                expected_resolution=8,
            )
            self.assertIn("normal map is effectively neutral", diagnostics)

            model.write_bytes(b"different")
            self.assertIn(
                "metrics modelSha256 does not match selected source",
                validate_normal_detail_evidence(
                    metrics,
                    normal_path=normal,
                    model_path=model,
                    expected_resolution=8,
                ),
            )


if __name__ == "__main__":
    unittest.main()
