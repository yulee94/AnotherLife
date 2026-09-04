import unittest

from tools.terrestrial.render_cindermaw_normal_detail_review import review_spec


class RenderCindermawNormalDetailReviewTests(unittest.TestCase):
    def test_review_uses_v004_source_and_never_bakes_runtime_vfx(self):
        spec = review_spec()
        self.assertTrue(spec["modelPath"].endswith("_source_v004.fbx"))
        self.assertIn("normaldetail_v004", spec["textureRoot"])
        self.assertTrue(spec["outputPath"].endswith("_threequarter_v004.png"))
        self.assertEqual(2048, spec["reviewTextureLimit"])
        self.assertEqual(1.0, spec["normalStrength"])
        self.assertFalse(spec["runtimeVfxIncluded"])


if __name__ == "__main__":
    unittest.main()
