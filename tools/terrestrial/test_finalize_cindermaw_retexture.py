import tempfile
import unittest
from pathlib import Path

from PIL import Image

from tools.terrestrial.finalize_cindermaw_retexture import (
    compose_metallic_smoothness,
    file_record,
    grade_cindermaw_base_color,
    portable_report_path,
)


class FinalizeCindermawRetextureTests(unittest.TestCase):
    def test_file_records_use_repo_relative_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / "assets" / "map.png"
            path.parent.mkdir()
            Image.new("RGB", (2, 2), (1, 2, 3)).save(path)
            self.assertEqual("assets/map.png", file_record(path, root)["path"])

    def test_report_paths_are_repo_relative(self):
        root = Path("D:/AnotherLife")
        self.assertEqual("unity/a.fbx", portable_report_path(root / "unity" / "a.fbx", root))

    def test_grades_cool_body_to_dark_obsidian_without_losing_blue_bias(self):
        image = Image.new("RGB", (1, 1), (120, 185, 220))
        graded = grade_cindermaw_base_color(image)
        red, green, blue = graded.getpixel((0, 0))
        self.assertLessEqual(max(red, green, blue), 80)
        self.assertGreater(blue, green)
        self.assertGreater(green, red)

    def test_preserves_controlled_ember_as_red_dominant(self):
        image = Image.new("RGB", (1, 1), (220, 65, 50))
        graded = grade_cindermaw_base_color(image)
        red, green, blue = graded.getpixel((0, 0))
        self.assertGreater(red, green * 1.5)
        self.assertGreater(red, blue * 1.5)
        self.assertLessEqual(red, 155)

    def test_composes_metallic_and_inverse_roughness_channels(self):
        metallic = Image.new("L", (2, 1))
        metallic.putdata([32, 224])
        roughness = Image.new("L", (2, 1))
        roughness.putdata([64, 192])
        packed = compose_metallic_smoothness(metallic, roughness)
        self.assertEqual((32, 0, 0, 191), packed.getpixel((0, 0)))
        self.assertEqual((224, 0, 0, 63), packed.getpixel((1, 0)))

    def test_rejects_mismatched_packed_map_dimensions(self):
        with self.assertRaisesRegex(ValueError, "dimensions must match"):
            compose_metallic_smoothness(Image.new("L", (2, 2)), Image.new("L", (1, 2)))


if __name__ == "__main__":
    unittest.main()
