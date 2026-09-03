import unittest

from tools.terrestrial.validate_realm_creature_uv import (
    triangle_overlap_area,
    validate_uv_metrics,
)


class RealmCreatureUvValidationTests(unittest.TestCase):
    def test_reports_positive_area_for_overlapping_triangles(self):
        left = ((0.0, 0.0), (1.0, 0.0), (0.0, 1.0))
        right = ((0.2, 0.2), (1.0, 0.2), (0.2, 1.0))
        self.assertGreater(triangle_overlap_area(left, right), 0.0)

    def test_shared_edge_is_not_reported_as_overlap(self):
        left = ((0.0, 0.0), (1.0, 0.0), (0.0, 1.0))
        right = ((1.0, 0.0), (1.0, 1.0), (0.0, 1.0))
        self.assertEqual(0.0, triangle_overlap_area(left, right))

    def test_accepts_clean_uv_metrics(self):
        metrics = {
            "uvFacesOutsideUnit": 0,
            "uvZeroAreaFaces": 0,
            "uvOverlappingFaces": 0,
        }
        self.assertEqual([], validate_uv_metrics(metrics))

    def test_rejects_overlap_and_out_of_bounds(self):
        metrics = {
            "uvFacesOutsideUnit": 1,
            "uvZeroAreaFaces": 0,
            "uvOverlappingFaces": 2,
        }
        diagnostics = validate_uv_metrics(metrics)
        self.assertTrue(any("outside" in item for item in diagnostics))
        self.assertTrue(any("overlapping" in item for item in diagnostics))


if __name__ == "__main__":
    unittest.main()
