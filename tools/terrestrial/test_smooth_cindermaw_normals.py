import math
import unittest

from tools.terrestrial.smooth_cindermaw_normals import (
    build_smoothing_report,
    should_mark_edge_sharp,
)


class SmoothCindermawNormalsTests(unittest.TestCase):
    def test_report_remains_fail_closed_and_records_smoothing_reduction(self):
        report = build_smoothing_report(
            input_path="unity/input.fbx",
            input_sha256="a" * 64,
            output_path="unity/output.fbx",
            output_sha256="b" * 64,
            blend_path="unity/output.blend",
            blend_sha256="c" * 64,
            metrics={
                "vertices": 27690,
                "polygons": 55334,
                "uvLayer": "UVMap_Clean",
                "sharpEdgesBefore": 53054,
                "sharpEdgesAfter": 812,
                "customNormalsRemoved": True,
            },
        )
        self.assertFalse(report["productionReady"])
        self.assertEqual("Blocked", report["runtimeIntegrationState"])
        self.assertIn("smoothing_pass", report["status"])
        self.assertEqual([], report["diagnostics"])
        self.assertEqual("c" * 64, report["editableBlendSha256"])

    def test_preserves_boundaries_and_only_hardens_deliberate_angles(self):
        self.assertTrue(should_mark_edge_sharp(face_angle=None, is_boundary=True))
        self.assertFalse(
            should_mark_edge_sharp(face_angle=math.radians(30.0), is_boundary=False)
        )
        self.assertTrue(
            should_mark_edge_sharp(face_angle=math.radians(80.0), is_boundary=False)
        )


if __name__ == "__main__":
    unittest.main()
